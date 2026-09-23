using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🔴 rev17 「궤도 청소부」 규칙 전부 (2026-09-23). 정본 = wiki/rev17-detail.md. UnityEngine 을 안 쓴다 — tools/pacing 봇이 그대로 컴파일한다.
    //    도구 셋: 집게(커서에 대면 저절로 친다) · 드론(알아서 줍는다) · 블랙홀 폭탄(누르면 모으고 떼면 터진다)
    //    흐름: 출동 → 결산 → 정비소(트리 24칸) → 청구서 8장 → 파산 → 빚 청산 → 청산 출동
    //    좌표는 시안과 같은 960×600 「화면 점」 — 지구 (480,310), 궤도는 세로로 0.6 눌린 타원.

    public enum Att { None, FuelPod, Pouch, Beacon, Magnet, Det, Ice, Cable, Armor, BBox, Tag }

    public class Junk
    {
        public int id, k, hp, max;
        public Att att;
        public double a, rr, ws, x, y, vx, vy, capT, fade, hit, rot, vr, tr;
        public bool free, dead, convoy;
        public Junk link1, link2;
    }

    [Serializable]
    public class NewsItem { public string id, kind, head, body; public int run, company; public bool read; }

    [Serializable]
    public class PastCompany { public int company, bill, runs; public double minutes; public bool won; }

    [Serializable]
    public class LoanRec { public int kind, run; public double amt; }   // kind 0 대출 · 1 판 수입에서 자동 상환 · 2 직접 상환

    [Serializable]
    public class LottoTicket { public int a, b, c, round; public double price; }   // 🎱 궤도 로또 한 장

    [Serializable]
    public class SweepState
    {
        public int version = 22;
        public double cash, billAmount = -1, creditPending, startedAt, debt;   // debt = 갚아야 할 빚 (대출 × 배수)
        public int runs, orbit, bill, billDue = 5, overRuns, contract = -1;
        public bool overdue, rerolled;
        public int[] lv = new int[SweepSim.NodeCount];
        public int planets = 1;                                          // 연 행성 (비트) — 지구는 늘
        public MarketState market;                                       // 📈 궤도 증권 — 회사마다 (파산하면 새 장)
        public int scratchRun = -1, scratchN;                            // 🎟 즉석 복권 — 출동마다 3장
        public List<LottoTicket> lotto = new List<LottoTicket>();        // 🎱 궤도 로또 — 이번 회 내 표
        public int lottoRound = 1, lottoDrawAt = 3;                      // 다음 추첨 = 출동 번호
        public int[] lottoLast; public int lottoLastRound, lottoLastHit; public double lottoLastWin;
        public List<LoanRec> loanLog = new List<LoanRec>();          // 대출 창에 보이는 내역 (최근 30개)
        public List<double> runEarn = new List<double>();                 // 최근 출동 벌이 (조종실 브라운관 막대 · 6개)
        public double lastClaw, lastDrone, lastBlast; public int lastBroke = -1, lastChain, lastContract;   // 조종실 출동 보고 — 껐다 켜도 남게 (lastContract 0 없음 · 1 성공 · 2 실패)
    }

    [Serializable]
    public class SweepMeta
    {
        public double credit, broken, playSeconds;
        public int[] career = new int[SweepSim.CareerCount];
        public int company = 1, bankrupt, loans, bestChain, bestPack, totalRuns, scoops;
        public double bestAuc;                                           // 고철 경매 최고 배수
        public bool won, careerOpen, cleanReady;
        public List<NewsItem> news = new List<NewsItem>();
        public List<string> flags = new List<string>();
        public List<PastCompany> history = new List<PastCompany>();
    }

    public class Blast { public double x, y, t, R; }
    public class Drone { public double a, cd, x, y; }
    public class Pod { public int kind; public double t, x, y; public bool up, got; }     // 지구 보급 — kind 0 연료 · 1 폭탄

    public class SweepRun
    {
        public double hx, hy, holeCd, clickCd, fuelGot, refill, fuel, max, t, next = 0.3, endT, formT = 9, rushT, rushX, rushY, chainT, holdT, ax = 480, ay = 310, refillT;
        public double ev1T = -1, ev2T = -1, stormT; public int ev1 = -1, ev2 = -1, stormLeft; public bool ev1Warn, ev2Warn, collector;
        public int shots, maxShots, chain, chainBest, packBest, broke, tier, idc;
        public bool over, holding, clean, contractOk;
        public double earnClaw, earnDrone, earnBlast, cut, toBill, bonus, interest;
        public string contractText; public int contractProg, contractTarget;     // 지난 판 의뢰 — 결산에 성공/실패를 보여 준다
        public int cVault, cFuel, cChip, cSat, cTank, cBig, cTag;
        public int cleanKills, cleanGoal = 1;
        public readonly List<Junk> junk = new List<Junk>();
        public readonly List<Junk> packed = new List<Junk>();
        public readonly List<Blast> pend = new List<Blast>();
        public readonly List<Drone> drones = new List<Drone>();
        public readonly List<Pod> pods = new List<Pod>();
        public double Earned => earnClaw + earnDrone + earnBlast;
    }

    public enum SwEv { Supply, SupplyGet, Strike, Broke, Coin, Pop, Beam, Ring, Blast, Tier, Crit, Collapse, Warn, EventGo, Collector, Shatter, Release, RunEnd, BillPaid, Overdue, Bankrupt, News, Won, SkillReady }

    public struct SwEvent
    {
        public SwEv kind;
        public double x, y, x2, y2, v;
        public int k;
        public string text;
    }

    public enum NodeSt { Hidden, Locked, Poor, Can, Max }

    public sealed class SweepSim
    {
        public const double EX = 480, EY = 310, Tilt = 0.6;

        // ───────────────────────── 잔해 일곱 (§3-1)
        public struct DType { public string name; public int hp; public double val, r; public bool heavy, big; }
        public static readonly DType[] Types =
        {
            new DType { name = "조각",      hp = 1,  val = 1,   r = 4 },
            new DType { name = "죽은 위성", hp = 3,  val = 6,   r = 7 },
            new DType { name = "로켓 동체", hp = 5,  val = 14,  r = 9,  heavy = true },
            new DType { name = "금고 위성", hp = 4,  val = 40,  r = 8 },
            new DType { name = "연료통",    hp = 1,  val = 0,   r = 6 },
            new DType { name = "폭발 탱크", hp = 1,  val = 2,   r = 6 },
            new DType { name = "큰 잔해",   hp = 20, val = 180, r = 17, big = true },
        };
        public const int Chip = 0, Sat = 1, Rocket = 2, Vault = 3, Fuel = 4, Tank = 5, Big = 6;
        static bool IsHost(int k) => k == Sat || k == Rocket || k == Vault || k == Big;

        // ───────────────────────── 궤도 셋 (§4-1)
        // 🪐 행성 다섯 — 궤도 자리 (09-23 사장님 「여러 행성을 청소해 주는 느낌」). 청구서를 갚으면 허가증이 팔리고, 돈 내고 연다
        // spin 궤도 도는 빠르기 · hp 잔해 체력 배율 · pull 안쪽으로 끌리는 힘(목성) · gap 띠 가운데 빈 틈(토성 두 겹 고리) · storm 모래 폭풍(화성, 화면만)
        public struct OrbitDef { public string name, desc; public double bi, bo, mult, att, spin, hp, pull, gap, permit; public int sell; public bool storm; public double[] mix; public int nMin, nMax; public int[] forms, events; }
        public static readonly OrbitDef[] Orbits =
        {
            new OrbitDef { name = "지구", desc = "기본 궤도",                         bi = 150, bo = 262, mult = 1, att = 0.10, spin = 1,    hp = 1,   sell = 0, permit = 0,       mix = new double[] { 60, 22, 4, 2, 5, 4, 1.2 },  nMin = 90,  nMax = 150, forms = new[] { 0, 0 },       events = new[] { 0, 1 } },
            new OrbitDef { name = "달",   desc = "느린 궤도 · 금고 위성이 많다",       bi = 118, bo = 240, mult = 1.5, att = 0.15, spin = 0.6,  hp = 1.1, sell = 2, permit = 1500,     mix = new double[] { 48, 22, 6, 9, 5, 4, 1.2 },  nMin = 100, nMax = 170, forms = new[] { 0, 1 },       events = new[] { 0, 3 } },
            new OrbitDef { name = "화성", desc = "모래 폭풍 · 얼음 껍질",             bi = 132, bo = 272, mult = 2, att = 0.25, spin = 1.1,  hp = 1.3, sell = 3, permit = 20000,    storm = true, mix = new double[] { 48, 22, 12, 3, 5, 7, 1.5 }, nMin = 120, nMax = 220, forms = new[] { 1, 2, 0 }, events = new[] { 2, 1 } },
            new OrbitDef { name = "목성", desc = "중력이 잔해를 안쪽에 모은다 · 장갑판", bi = 188, bo = 318, mult = 3, att = 0.30, spin = 1.3,  hp = 1.6, sell = 5, permit = 1500000,  pull = 9, mix = new double[] { 40, 18, 10, 6, 5, 8, 2.5 }, nMin = 170, nMax = 280, forms = new[] { 3, 4, 1 }, events = new[] { 3, 4 } },
            new OrbitDef { name = "토성", desc = "두 겹 고리 · 케이블 망",            bi = 150, bo = 300, mult = 4.5, att = 0.30, spin = 0.9,  hp = 2.0, sell = 6, permit = 15000000, gap = 0.28, mix = new double[] { 38, 18, 8, 8, 5, 6, 3 }, nMin = 180, nMax = 290, forms = new[] { 2, 2, 3, 4 }, events = new[] { 3, 4 } },
        };
        public static readonly string[] FormNames = { "무리", "탱크 사슬", "케이블 망", "호송대", "난파 구역" };
        public static readonly string[] EventNames = { "연료 보급선", "충돌 사고", "파편 폭풍", "금고 호송대", "대충돌" };

        // ───────────────────────── 트리 스물네 칸 (§8-2)
        public struct Node { public string id, branch, name, desc; public string[] par; public int seg, max; public double first, mult; public int depth, lane; }
        // 🔴 정비고 네 칸 × 아홉 = 36칸 (사장님 09-23: "강화하는 것도 더 넓히고 더 많게"). depth = 행(0~4) · lane = 열(0~4)
        public static readonly Node[] Nodes =
        {
            N("c_pow", "claw", "빔 위력", "한 방이 세진다", new string[0], 1, 3, 1.6, 12, 0, 2),
            N("c_rad", "claw", "빔 범위", "한 번에 여러 개", new[] { "c_pow" }, 1, 9, 1.7, 8, 1, 3),
            N("c_spd", "claw", "빔 연사", "쏘는 간격이 짧아진다", new[] { "c_pow" }, 2, 40, 1.6, 7, 1, 1),
            N("c_fuel", "claw", "연료 탱크", "출동이 길어진다", new[] { "c_rad" }, 1, 20, 1.6, 10, 2, 4),
            N("c_crit", "claw", "치명타", "가끔 세 배로 쏜다", new[] { "c_spd" }, 3, 600, 1.6, 6, 3, 0),
            N("c_double", "claw", "연사", "한 번 더 쏠 확률", new[] { "c_spd" }, 3, 900, 1.7, 5, 3, 2),
            N("c_magnet", "claw", "견인 빔", "주변 조각을 끌어온다", new[] { "c_rad", "c_fuel" }, 3, 1200, 1.8, 3, 3, 4),
            N("c_over", "claw", "과부하", "판 끝 5초 두 배 빠르게", new[] { "c_crit", "c_double" }, 4, 3000, 1, 1, 4, 1),
            N("d_n", "drone", "드론 +1", "드론이 한 대 더", new string[0], 2, 240, 1.8, 8, 0, 2),
            N("d_spd", "drone", "드론 속도", "더 자주 줍는다", new[] { "d_n" }, 2, 300, 1.6, 6, 1, 1),
            N("d_reach", "drone", "드론 거리", "더 멀리서 집는다", new[] { "d_n" }, 3, 660, 1.6, 5, 1, 3),
            N("d_mag", "drone", "드론 수거", "드론 몫 +25%", new[] { "d_spd" }, 3, 900, 1.6, 5, 2, 0),
            N("d_sig", "drone", "신호 증폭", "신호기 효과가 길어진다", new[] { "d_spd", "d_reach" }, 3, 700, 1.7, 3, 2, 2),
            N("d_grade", "drone", "드론 등급", "더 단단한 것도 한 방에", new[] { "d_reach" }, 5, 4500, 2.0, 3, 2, 4),
            N("d_fix", "drone", "수리 드론", "출동 +2초", new[] { "d_mag" }, 4, 2000, 1.8, 3, 3, 1),
            N("d_pair", "drone", "편대", "한 번에 둘씩", new[] { "d_mag", "d_grade" }, 6, 18000, 1, 1, 3, 3),
            N("d_fact", "drone", "드론 공장", "드론 +1 (공장제)", new[] { "d_pair" }, 6, 30000, 2.5, 2, 4, 2),
            N("b_n", "bh", "블랙홀 충전", "블랙홀을 한 번 더 쟁여 둔다", new string[0], 3, 750, 2.2, 4, 0, 1),
            N("c_find", "bh", "연료 보급", "연료를 올려 보낸다", new string[0], 3, 240, 1.8, 5, 0, 3),
            N("s_speed", "bh", "재충전", "블랙홀이 빨리 차고 보급도 빨리 닿는다", new[] { "b_n", "c_find" }, 3, 500, 1.6, 4, 1, 2),
            N("b_pr", "bh", "흡입 반경", "더 넓게 빨아들인다", new[] { "b_n" }, 3, 600, 1.6, 6, 1, 0),
            N("b_cap", "bh", "붕괴 한계", "더 많이 모아도 버틴다", new[] { "b_n" }, 3, 660, 1.6, 6, 2, 1),
            N("b_pf", "bh", "흡입 세기", "더 빨리 빨려 든다", new[] { "b_pr" }, 4, 1800, 1.6, 5, 2, 0),
            N("b_br", "bh", "폭발 반경", "더 크게 터진다", new[] { "b_cap" }, 4, 1800, 1.6, 6, 3, 1),
            N("b_chain", "bh", "연쇄 확률", "터진 게 또 터진다", new[] { "b_br" }, 4, 2700, 1.6, 8, 3, 3),
            N("b_pack", "bh", "압축 배율", "많이 모을수록 값 +", new[] { "b_chain", "b_pf" }, 5, 7500, 1.7, 5, 4, 2),
            N("o_wide", "eco", "궤도 확장", "궤도가 넓어진다 — 쓰레기도 는다", new string[0], 1, 20, 1.9, 6, 0, 4),
            N("e_val", "eco", "고철 시세", "모든 값이 오른다", new string[0], 1, 120, 1.9, 10, 0, 2),
            N("e_vault", "eco", "금고 감별", "금고 위성이 더 자주", new[] { "e_val" }, 2, 360, 1.6, 6, 1, 0),
            N("e_att", "eco", "부착물 감별", "부착물이 더 자주", new[] { "e_val" }, 3, 1200, 1.7, 5, 1, 2),
            N("e_quest", "eco", "의뢰 보상", "의뢰 보너스가 커진다", new[] { "e_val" }, 3, 800, 1.7, 4, 1, 4),
            N("e_talk", "eco", "청구서 협상", "기한 +1판", new[] { "e_vault" }, 4, 3600, 3.0, 2, 2, 0),
            N("e_tip", "eco", "제보망", "블랙박스가 더 자주", new[] { "e_att" }, 3, 1500, 1.8, 3, 2, 2),
            N("e_save", "eco", "적금", "판 끝에 이자", new[] { "e_quest" }, 4, 2500, 1.9, 5, 2, 4),
            N("e_guard", "eco", "상환 조절", "빚 상환으로 떼는 몫 30% → 20%", new[] { "e_talk", "e_tip" }, 5, 9000, 1, 1, 3, 1),
            N("e_used", "eco", "중고 거래", "모든 칸 -5%", new[] { "e_save" }, 4, 6000, 2.0, 3, 3, 3),
            // 📈 증권 줄기 (09-23 사장님 「진짜 주식처럼 · 정비소에서 이점을」) — 맨 끝에 붙여 옛 저장의 칸 순서를 안 흔든다 (경매 칸 자리를 그대로 썼다)
            N("a_open", "eco", "증권 계좌", "궤도 증권이 열린다 — 판 중에도 조종실에서도 사고판다", new[] { "e_val" }, 2, 500, 1, 1, 1, 1),
            N("a_auto", "eco", "개미의 기도", "내가 산 종목이 조금씩 더 오르는 쪽으로 움직인다", new[] { "a_open" }, 2, 800, 1, 1, 1, 1),
            N("a_read", "eco", "내부자 정보", "다음 속보를 미리 안다 — 시간 · 업종 · 제목", new[] { "a_auto" }, 2, 1500, 2.5, 3, 1, 1),
            N("a_ins", "eco", "행운의 부적", "내 종목에 나쁜 속보가 걸리면 좋은 속보로 바뀐다 (60 · 70 · 80%)", new[] { "a_read" }, 3, 4000, 2.5, 3, 1, 1),
            N("a_big", "eco", "큰손 계좌", "수수료가 줄고 배당이 붙는다", new[] { "a_ins" }, 4, 20000, 3, 3, 1, 1),
        };
        public const int NodeCount = 41;
        // ───────────────────────── 정비소 트리 자리 (손으로 격자에 놓았다 · 시안 https://claude.ai/artifact/NLZseBQWXKMGfuAmFDFJcR)
        //    par = 이어지는 앞 칸의 능력 (R = 가운데 청소선) · tile = 그 능력의 몇 번째 칸 뒤 · (x, y) 첫 칸 자리 · (dx, dy) 뻗는 방향
        //    🔴 여는 조건도 이것 — 앞 칸을 사야 이 능력의 첫 칸이 열린다 (게임 · 봇 같은 규칙)
        public struct TreeSpot { public string par; public int tile, x, y, dx, dy; }
        public static readonly Dictionary<string, TreeSpot> Layout = new Dictionary<string, TreeSpot>
        {
            { "c_pow", new TreeSpot { par = "R", tile = 0, x = 0, y = -1, dx = 0, dy = -1 } },
            { "c_spd", new TreeSpot { par = "c_pow", tile = 1, x = -1, y = -2, dx = -1, dy = 0 } },
            { "c_rad", new TreeSpot { par = "c_pow", tile = 1, x = 1, y = -2, dx = 1, dy = 0 } },
            { "c_crit", new TreeSpot { par = "c_spd", tile = 2, x = -2, y = -3, dx = 0, dy = -1 } },
            { "c_double", new TreeSpot { par = "c_spd", tile = 4, x = -4, y = -3, dx = 0, dy = -1 } },
            { "c_over", new TreeSpot { par = "c_crit", tile = 5, x = -3, y = -8, dx = 0, dy = -1 } },
            { "c_magnet", new TreeSpot { par = "c_rad", tile = 3, x = 3, y = -3, dx = 0, dy = -1 } },
            { "c_fuel", new TreeSpot { par = "R", tile = 0, x = -1, y = 0, dx = -1, dy = 0 } },
            { "o_wide", new TreeSpot { par = "c_fuel", tile = 1, x = -2, y = -1, dx = -1, dy = 0 } },
            { "c_find", new TreeSpot { par = "c_fuel", tile = 5, x = -6, y = 0, dx = -1, dy = 0 } },
            { "d_n", new TreeSpot { par = "R", tile = 0, x = 1, y = 0, dx = 1, dy = 0 } },
            { "d_spd", new TreeSpot { par = "d_n", tile = 2, x = 3, y = -1, dx = 1, dy = 0 } },
            { "d_reach", new TreeSpot { par = "d_n", tile = 2, x = 3, y = 1, dx = 1, dy = 0 } },
            { "d_mag", new TreeSpot { par = "d_spd", tile = 5, x = 7, y = -2, dx = 0, dy = -1 } },
            { "d_fix", new TreeSpot { par = "d_mag", tile = 1, x = 8, y = -3, dx = 1, dy = 0 } },
            { "d_sig", new TreeSpot { par = "d_n", tile = 5, x = 6, y = 0, dx = 1, dy = 0 } },
            { "d_grade", new TreeSpot { par = "d_reach", tile = 3, x = 6, y = 2, dx = 1, dy = 0 } },
            { "d_pair", new TreeSpot { par = "d_sig", tile = 3, x = 9, y = -1, dx = 0, dy = -1 } },
            { "d_fact", new TreeSpot { par = "d_pair", tile = 1, x = 10, y = -1, dx = 1, dy = 0 } },
            { "b_n", new TreeSpot { par = "R", tile = 0, x = 0, y = 1, dx = 0, dy = 1 } },
            { "s_speed", new TreeSpot { par = "b_n", tile = 4, x = 0, y = 5, dx = 0, dy = 1 } },
            { "b_pr", new TreeSpot { par = "b_n", tile = 2, x = 1, y = 3, dx = 1, dy = 0 } },
            { "b_cap", new TreeSpot { par = "b_n", tile = 4, x = 1, y = 5, dx = 1, dy = 0 } },
            { "b_pf", new TreeSpot { par = "b_pr", tile = 5, x = 6, y = 3, dx = 1, dy = 0 } },
            { "b_br", new TreeSpot { par = "b_cap", tile = 3, x = 3, y = 6, dx = 0, dy = 1 } },
            { "b_chain", new TreeSpot { par = "b_br", tile = 2, x = 4, y = 7, dx = 1, dy = 0 } },
            { "b_pack", new TreeSpot { par = "b_chain", tile = 5, x = 8, y = 8, dx = 0, dy = 1 } },
            { "e_val", new TreeSpot { par = "R", tile = 0, x = -1, y = 1, dx = 0, dy = 1 } },
            { "e_vault", new TreeSpot { par = "e_val", tile = 2, x = -2, y = 2, dx = -1, dy = 0 } },
            { "e_att", new TreeSpot { par = "e_val", tile = 4, x = -2, y = 4, dx = -1, dy = 0 } },
            { "e_quest", new TreeSpot { par = "e_val", tile = 5, x = -2, y = 6, dx = -1, dy = 0 } },
            { "e_talk", new TreeSpot { par = "e_vault", tile = 3, x = -4, y = 1, dx = -1, dy = 0 } },
            { "e_tip", new TreeSpot { par = "e_att", tile = 3, x = -4, y = 5, dx = -1, dy = 0 } },
            { "e_save", new TreeSpot { par = "e_val", tile = 5, x = -1, y = 6, dx = 0, dy = 1 } },
            { "e_guard", new TreeSpot { par = "e_talk", tile = 2, x = -6, y = 1, dx = -1, dy = 0 } },
            { "e_used", new TreeSpot { par = "e_save", tile = 3, x = -2, y = 8, dx = -1, dy = 0 } },
            { "a_open", new TreeSpot { par = "e_val", tile = 3, x = -2, y = 3, dx = -1, dy = 0 } },
            { "a_auto", new TreeSpot { par = "a_open", tile = 1, x = -3, y = 3, dx = -1, dy = 0 } },
            { "a_read", new TreeSpot { par = "a_auto", tile = 1, x = -4, y = 3, dx = -1, dy = 0 } },
            { "a_ins", new TreeSpot { par = "a_read", tile = 3, x = -7, y = 3, dx = -1, dy = 0 } },
            { "a_big", new TreeSpot { par = "a_ins", tile = 3, x = -10, y = 3, dx = -1, dy = 0 } },
        };
        public static readonly string[] IconOrder = { "c_pow", "c_rad", "c_spd", "c_fuel", "c_crit", "c_double", "c_magnet", "c_over", "o_wide", "c_find", "d_n", "d_spd", "d_reach", "d_mag", "d_sig", "d_grade", "d_fix", "d_pair", "d_fact", "b_n", "s_speed", "b_pr", "b_cap", "b_pf", "b_br", "b_chain", "b_pack", "e_val", "e_vault", "e_att", "e_quest", "e_talk", "e_tip", "e_save", "e_guard", "e_used", "R", "a_open", "a_auto", "a_read", "a_ins", "a_big" };
        public static string VisBranch(string id) => id == "c_fuel" || id == "o_wide" || id == "c_find" ? "hull" : id == "s_speed" ? "bh" : Nodes[NodeIx[id]].branch;

        static Node N(string id, string br, string name, string desc, string[] par, int seg, double first, double mult, int max, int depth, int lane)
            => new Node { id = id, branch = br, name = name, desc = desc, par = par, seg = seg, first = first, mult = mult, max = max, depth = depth, lane = lane };
        static readonly Dictionary<string, int> NodeIx = new Dictionary<string, int>();
        public static readonly string[] BranchIds = { "claw", "drone", "bh", "eco" };
        public static readonly string[] BranchNames = { "빔 · 선체", "드론 격납고", "블랙홀 · 보급", "사무실" };
        public static readonly int[] BranchNeed = { 0, 1, 2, 0 };

        // ───────────────────────── 청구서 여덟 (§7-3)
        public struct Bill { public string t, perk; public double m, credit; public int due; }
        public static readonly Bill[] Bills =
        {
            new Bill { t = "연료비",           m = 45,     due = 5, credit = 2,  perk = "드론 2대 · 금고 위성 · 부착물이 나온다" },
            new Bill { t = "청소선 할부 1회",  m = 700,    due = 4, credit = 3,  perk = "블랙홀 스킬 · 🌙 달 허가증 판매 (×1.5)" },
            new Bill { t = "궤도 사용료",      m = 2500,   due = 4, credit = 5,  perk = "🔴 화성 허가증 판매 (값 ×2)" },
            new Bill { t = "보험료",           m = 180000, due = 5, credit = 8,  perk = "큰 잔해 등장 · 연쇄 +10%" },
            new Bill { t = "청소선 할부 2회",  m = 300000, due = 4, credit = 12, perk = "드론 등급 +1 · 🪐 목성 허가증 판매 (×3)" },
            new Bill { t = "법인세",           m = 4000000, due = 5, credit = 18, perk = "💫 토성 허가증 판매 (값 ×4.5)" },
            new Bill { t = "청소선 할부 3회",  m = 20000000, due = 5, credit = 26, perk = "폭탄 +1 · 붕괴 한계 +50%" },
            new Bill { t = "청소선 할부 완납", m = 60000000, due = 6, credit = 0,  perk = "빚 청산 → 청산 출동" },
        };

        // ───────────────────────── 경력 (§9-4) — 파산할 때만 산다
        public struct Career { public string id, name, desc; public int[] cost; }
        public static readonly Career[] Careers =
        {
            new Career { id = "pilot",  name = "베테랑 조종사", desc = "연료 +20%",            cost = new[] { 3, 6, 10 } },
            new Career { id = "seed",   name = "단골 고객",     desc = "모든 값 ×1.3",         cost = new[] { 2, 4, 7 } },
            new Career { id = "wrench", name = "정비 요령",     desc = "트리 -15%",            cost = new[] { 4, 7, 11 } },
            new Career { id = "dronef", name = "드론 공장",     desc = "드론 +1 (해금 뒤)",    cost = new[] { 4, 8 } },
            new Career { id = "bhole",  name = "블랙홀 연구",   desc = "폭탄 +1 (해금 뒤)",    cost = new[] { 5, 10 } },
            new Career { id = "friend", name = "추심원과 친구", desc = "추심 20% · 연체료 절반", cost = new[] { 6 } },
        };
        public const int CareerCount = 6;

        // ───────────────────────── 의뢰 (§4-4) — kind: 0 금고 1 연료통 2 조각 3 위성 4 연쇄 5 탱크 6 압축 7 큰 잔해 8 압류
        public struct Contract { public int orbit, kind, target; public string text; }
        public static readonly Contract[] Contracts =
        {
            new Contract { orbit = 0, kind = 0, target = 2,   text = "금고 위성 2개" },
            new Contract { orbit = 0, kind = 1, target = 6,   text = "로켓 동체 6개" },
            new Contract { orbit = 0, kind = 2, target = 150, text = "조각 150개" },
            new Contract { orbit = 0, kind = 3, target = 20,  text = "죽은 위성 20개" },
            new Contract { orbit = 1, kind = 0, target = 5,   text = "금고 위성 5개" },
            new Contract { orbit = 1, kind = 2, target = 250, text = "조각 250개" },
            new Contract { orbit = 1, kind = 3, target = 30,  text = "죽은 위성 30개" },
            new Contract { orbit = 2, kind = 4, target = 40,  text = "연쇄 40" },
            new Contract { orbit = 2, kind = 5, target = 15,  text = "폭발 탱크 15개" },
            new Contract { orbit = 2, kind = 6, target = 25,  text = "한 번에 25개 압축" },
            new Contract { orbit = 3, kind = 7, target = 3,   text = "큰 잔해 3개" },
            new Contract { orbit = 3, kind = 4, target = 150, text = "연쇄 150" },
            new Contract { orbit = 3, kind = 6, target = 60,  text = "한 번에 60개 압축" },
            new Contract { orbit = 4, kind = 0, target = 12,  text = "금고 위성 12개" },
            new Contract { orbit = 4, kind = 4, target = 200, text = "연쇄 200" },
            new Contract { orbit = 4, kind = 7, target = 4,   text = "큰 잔해 4개" },
            new Contract { orbit = -1, kind = 8, target = 5,  text = "압류 딱지 5개" },
        };

        public SweepState S;
        public SweepMeta M;
        public SweepRun R = new SweepRun { over = true };
        public readonly Queue<SwEvent> Events = new Queue<SwEvent>();
        readonly Random rng;
        public static double Econ = 0.096;             // 봇 · 시험용 값 배율
        public static double RefillRate = 0.5;     // 1초에 목표 수의 절반씩 스며든다 — 초반엔 도구가, 후반엔 이 속도가 천장

        static SweepSim() { for (int i = 0; i < Nodes.Length; i++) NodeIx[Nodes[i].id] = i; }

        public SweepSim(SweepState s = null, SweepMeta m = null, int seed = 0)
        {
            rng = seed == 0 ? new Random() : new Random(seed);
            M = m ?? new SweepMeta();
            if (M.career == null || M.career.Length != CareerCount) M.career = new int[CareerCount];
            if (s != null && s.version == 20 && s.lv != null && s.lv.Length == 35)
            {
                // 판 20 → 21: 「궤도 확장」 칸이 「고철 시세」 앞에 끼었다 — 레벨을 한 칸씩 밀어 옮긴다 (사장님 저장을 지키려고)
                int at = NodeIx["o_wide"]; var nl = new int[NodeCount];
                for (int i = 0; i < 35; i++) nl[i < at ? i : i + 1] = s.lv[i];
                s.lv = nl; s.version = 21;
            }
            if (s != null && s.version == 21)
            {
                s.planets = 1 | (s.bill >= 3 ? 2 : 0) | (s.bill >= 6 ? 4 : 0);
                s.version = 22;
            }
            if (s == null || s.version != 22) { S = new SweepState(); S.startedAt = M.playSeconds; }
            else S = s;
            MakeMarket();
            if (S.lv == null) S.lv = new int[NodeCount];
            else if (S.lv.Length < NodeCount) { var lv = S.lv; Array.Resize(ref lv, NodeCount); S.lv = lv; }   // 칸이 늘면 산 것은 그대로 두고 뒤에 붙인다 (경매 줄기 · 09-23)
            else if (S.lv.Length > NodeCount) { var lv = S.lv; Array.Resize(ref lv, NodeCount); S.lv = lv; }
            if (M.news.Count == 0) AddNews("first_run");
            if (ContractsOn && (S.contract < 0 || S.contract < Contracts.Length && Contracts[S.contract].orbit < 0)) RollContract();     // 의뢰가 비었거나 이제 없는 압류 딱지 의뢰면 새로
            Preview();
        }

        double Rnd() => rng.NextDouble();
        double Rnd(double a, double b) => a + rng.NextDouble() * (b - a);
        public int Lv(string id) => S.lv[NodeIx[id]];
        int Cr(int i) => M.career[i];
        void Emit(SwEv k, double x = 0, double y = 0, double v = 0, int kk = 0, string text = null, double x2 = 0, double y2 = 0)
        { if (Events.Count < 4000) Events.Enqueue(new SwEvent { kind = k, x = x, y = y, v = v, k = kk, text = text, x2 = x2, y2 = y2 }); }

        // ───────────────────────── 파생 숫자
        public int Seg => Math.Min(8, S.bill + 1);
        public bool DronesOn => S.bill >= 1;
        public bool BombsOn => S.bill >= 2;
        public bool ContractsOn => S.bill >= 2;
        public bool BigsOn => S.bill >= 4;
        public int MaxOrbit { get { int m = 0; for (int i = 0; i < Orbits.Length; i++) if (Open(i)) m = i; return m; } }
        public bool Open(int i) => i == 0 || (S.planets & (1 << i)) != 0;
        public bool OnSale(int i) => !Open(i) && S.bill >= Orbits[i].sell;
        public bool BuyPermit(int i)
        {
            if (!R.over || !OnSale(i) || S.cash < Orbits[i].permit) return false;
            S.cash -= Orbits[i].permit; S.planets |= 1 << i;
            AddNews(null, Orbits[i].name + " 청소 허가 — 민간 청소선 첫 진입", "케슬러 금융이 " + Orbits[i].name + " 궤도 청소 허가증을 내줬다. " + Orbits[i].desc + ". 값은 지구의 " + Orbits[i].mult + "배라고 한다.");
            S.orbit = i; RollContract(); Preview();
            if (Mk != null && StockOpen) { string[] sec = { "", "달", "화성", "목성", "관광" }; Mk.GameEvent("민간 청소선 " + Orbits[i].name + " 진출", "궤도 청소부가 " + Orbits[i].name + " 청소 허가를 땄다. 관련 업계가 들썩인다.", new[] { sec[i], "ship" }, null, 0.14f); }
            return true;
        }
        public double FuelMax => (30 + 3 * Lv("c_fuel") + 2 * Lv("d_fix")) * (1 + 0.2 * Cr(0));
        public double Gap => Math.Max(0.3, 0.6 - 0.045 * Lv("c_spd"));
        public double ClawR => Lv("c_rad") > 0 ? 22 + 10 * Lv("c_rad") : 0;   // 0 = 하나씩
        public bool AutoClaw => true;        // 🔴 자동이 기본 (사장님 09-23: "클릭은 빼자 오토는 기본으로")
        public const double PickR = 30;      // 범위 강화 전 — 커서 밑 하나를 잡는 거리
        public int ClawDmg => 1 + Lv("c_pow");
        public double HpMul => (1 + 0.45 * Math.Max(0, S.bill - 2)) * Orbits[S.orbit].hp;   // 잔해 체력 배율 — 청구서 3장째부터 한 장마다 +45% (초반은 가볍게)
        public int BlastDmg => 2 + 2 * ClawDmg;                    // 폭발은 즉사가 아니라 피해
        public double Crit => 0.05 * Lv("c_crit");
        public int DroneCount => DronesOn ? 2 + Lv("d_n") + Lv("d_fact") + Cr(3) : 0;
        public double DroneCd => Math.Max(0.4, 1 - 0.1 * Lv("d_spd"));
        public double Reach => 80 + 15 * Lv("d_reach");
        public int Grade => 1 + Lv("d_grade") + (S.bill >= 5 ? 1 : 0);
        public double DroneMag => 1 + 0.25 * Lv("d_mag");
        public int Bombs => BombsOn ? Math.Min(6, 2 + Lv("b_n") + (S.bill >= 7 ? 1 : 0) + Cr(4)) : 0;
        public double PullR => 150 + 20 * Lv("b_pr");
        public double PullF => 1 + 0.25 * Lv("b_pf");
        public int Cap => (int)Math.Round((18 + 8 * Lv("b_cap")) * (S.bill >= 7 ? 1.5 : 1));
        public double BlastK => 1 + 0.15 * Lv("b_br");
        public double ChainP => Math.Min(0.93, 0.25 + 0.07 * Lv("b_chain") + (S.bill >= 4 ? 0.1 : 0));
        public double HoleCd => 16 - 1.5 * Lv("s_speed");     // 블랙홀 스킬 — 한 칸 차는 시간
        public const double HoleDur = 3;                           // 열려 있는 시간 — 끝나면 저절로 터진다
        public double PackK => 0.02 + 0.012 * Lv("b_pack");
        // 🔴 한 번 터질 때 이어지는 연쇄의 한계 — 도파민 사다리(§5)가 구간마다 한 단계씩 열리게
        public int ChainMax => R.clean ? 5000 : 25 + (S.orbit >= 1 ? 20 : 0) + (S.orbit >= 2 ? 40 : 0) + 12 * Lv("b_chain") + (S.bill >= 4 ? 10 : 0) + (S.bill >= 7 ? 30 : 0);
        public double ValMult => Math.Pow(1.25, Lv("e_val")) * Math.Pow(1.3, Cr(1)) * Orbits[S.orbit].mult * Econ;
        public double Cut => S.debt > 0 ? (Lv("e_guard") > 0 || Cr(5) > 0 ? 0.2 : 0.3) : 0;   // 빚이 있으면 판 수입에서 떼어 상환
        // ── 대출 (연체 대신) — 언제든 받을 수 있다. 받은 돈 × 배수를 판 수입에서 조금씩 갚는다
        public static double LoanMult = 3;
        void CheckClean() { if (S.bill >= Bills.Length && S.debt <= 0.5) { S.debt = 0; M.cleanReady = true; } }   // 청구서도 빚도 다 갚아야 청산 출동
        public double LoanCap => M.cleanReady || S.bill >= Bills.Length - 1 ? 0 :   // 마지막 할부(완납)엔 대출이 안 된다 — 빚으로 빚을 끝내면 끝없이 갚기만 한다
             Math.Max(0, Math.Floor(BillAmount - S.debt / LoanMult));   // 한도 = 지금 청구서 금액 − 남은 원금
        public bool TakeLoan(double amt)
        {
            amt = Math.Min(Math.Ceiling(amt), LoanCap);
            if (amt <= 0) return false;
            S.cash += amt; S.debt += amt * LoanMult; M.loans++; LogLoan(0, amt);
            return true;
        }
        void LogLoan(int kind, double amt)
        {
            if (S.loanLog == null) S.loanLog = new List<LoanRec>();
            S.loanLog.Add(new LoanRec { kind = kind, amt = amt, run = S.runs });
            if (S.loanLog.Count > 30) S.loanLog.RemoveAt(0);
        }
        // 📈 궤도 증권 — 규칙은 Market.cs. 여기선 돈 · 칸과 잇는다
        public Market Mk;
        public bool StockOpen => Lv("a_open") > 0;
        public double StockFee => new[] { 0.01, 0.006, 0.003, 0 }[Math.Min(3, Lv("a_big"))];
        public double StockDiv => 0.0003 * Lv("a_big");
        public double LuckAt => new[] { 0, 0.6, 0.7, 0.8 }[Math.Min(3, Lv("a_ins"))];   // 🍀 행운의 부적 — 내 종목 나쁜 속보를 좋은 속보로 (09-24 사장님 「자동 매도 말고 오를 확률」)
        void MakeMarket()
        {
            if (S.market == null) S.market = new MarketState { seed = 1 + (int)(M.playSeconds * 7) % 100000 + M.company * 131 };
            Mk = new Market(S.market);
        }
        /// <summary>시장 시간 — 판 중이든 조종실이든 (계좌를 열었을 때만). 배당 · 자동 매도 돈은 바로 들어온다</summary>
        public void MarketTick(double dt)
        {
            if (!StockOpen || Mk == null) return;
            S.cash += Mk.Update(dt, Lv("a_auto") > 0 ? 0.0008 : 0, LuckAt, StockFee, StockDiv);
        }
        public void StockBuy(int i, double frac) { if (!StockOpen) return; double money = Math.Floor(S.cash * frac); if (money < 1) return; S.cash -= Mk.Buy(i, money, StockFee); }
        public void StockSell(int i, double frac) { if (!StockOpen) return; S.cash += Math.Floor(Mk.Sell(i, frac, StockFee)); }

        // 🎟 즉석 복권 · 🎱 궤도 로또 (09-23 사장님 「복권 두 가지 다」) — 게임 안 돈만. 평균 기대값 0.7 안팎 (복권답게 손해)
        readonly Random luck = new Random();                              // 게임 난수와 따로 — 봇 영향 없음
        public static readonly string[] ScratchSym = { "고철", "위성", "금고", "행성", "황금" };
        public static readonly int[] ScratchMult = { 1, 2, 5, 20, 100 };
        public double ScratchPrice => Math.Max(10, Math.Round(BillAmount * 0.02));
        public int ScratchLeft => S.scratchRun == S.runs ? Math.Max(0, 3 - S.scratchN) : 3;
        public double ScratchPending;                                     // 긁어서 다 보이면 받는다
        /// <summary>한 장 산다 — 돌려주는 값 = 칸 아홉의 그림 (null = 못 삼). win = 당첨 그림 (-1 꽝)</summary>
        public int[] ScratchBuy(out int win)
        {
            win = -1;
            if (ScratchLeft <= 0 || S.cash < ScratchPrice) return null;
            if (S.scratchRun != S.runs) { S.scratchRun = S.runs; S.scratchN = 0; }
            S.scratchN++; S.cash -= ScratchPrice;
            double u = luck.NextDouble();
            win = u < 0.001 ? 4 : u < 0.009 ? 3 : u < 0.044 ? 2 : u < 0.114 ? 1 : u < 0.234 ? 0 : -1;
            var g = new int[9]; var cnt = new int[5];
            var slots = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            if (win >= 0) for (int k = 0; k < 3; k++) { int j = luck.Next(slots.Count); g[slots[j]] = win; slots.RemoveAt(j); cnt[win]++; }
            foreach (var j in slots)
            {
                int sym; do sym = luck.Next(5); while (sym == win || cnt[sym] >= 2);   // 꽝 칸은 같은 그림이 둘까지만
                g[j] = sym; cnt[sym]++;
            }
            ScratchPending = win >= 0 ? ScratchPrice * ScratchMult[win] : 0;
            return g;
        }
        public double ScratchClaim() { double w = ScratchPending; S.cash += w; ScratchPending = 0; return w; }

        public double LottoPrice => Math.Max(20, Math.Round(BillAmount * 0.03));
        public int LottoMine => S.lotto.Count;
        public bool LottoBuy(int a, int b, int c)
        {
            if (S.lotto.Count >= 3 || S.cash < LottoPrice || a == b || b == c || a == c) return false;
            S.cash -= LottoPrice;
            S.lotto.Add(new LottoTicket { a = a, b = b, c = c, round = S.lottoRound, price = LottoPrice });
            return true;
        }
        /// <summary>출동이 끝날 때 — 추첨 날이면 번호를 뽑고 당첨금을 준다</summary>
        void LottoDraw()
        {
            if (S.lotto.Count == 0) return;
            // 🎱 궤도 로또는 뺐다 (09-24) — 옛 저장에 남은 표는 값을 돌려준다
            foreach (var t in S.lotto) S.cash += t.price;
            S.lotto.Clear();
            return;
#pragma warning disable CS0162
            var pool = new List<int>(); for (int i = 1; i <= 12; i++) pool.Add(i);
            var d = new int[3]; for (int k = 0; k < 3; k++) { int j = luck.Next(pool.Count); d[k] = pool[j]; pool.RemoveAt(j); }
            Array.Sort(d);
            int best = 0; double win = 0;
            foreach (var t in S.lotto)
            {
                int hit = 0; foreach (var x in new[] { t.a, t.b, t.c }) if (x == d[0] || x == d[1] || x == d[2]) hit++;
                best = Math.Max(best, hit);
                win += t.price * (hit == 3 ? 60 : hit == 2 ? 2 : hit == 1 ? 0.3 : 0);
            }
            win = Math.Floor(win); S.cash += win;
            S.lottoLast = d; S.lottoLastRound = S.lottoRound; S.lottoLastHit = S.lotto.Count > 0 ? best : -1; S.lottoLastWin = win;
            AddNews(null, "궤도 로또 " + S.lottoRound + "회 — " + d[0] + " · " + d[1] + " · " + d[2], best == 3 ? "세 개를 다 맞힌 사람이 나왔다! 주식회사 궤도 청소부라는 소문이다." : "이번 회 당첨 번호는 " + d[0] + ", " + d[1] + ", " + d[2] + ".");
            S.lotto.Clear(); S.lottoRound++; S.lottoDrawAt = S.runs + 3;
        }

        public bool LoanAndPay()
        {
            if (S.cash >= BillAmount) return PayBill();
            double need = BillAmount - S.cash;
            if (need > LoanCap) return false;
            TakeLoan(need);
            return PayBill();
        }
        public bool RepayDebt()
        {
            double p = Math.Min(S.cash, S.debt);
            if (p <= 0) return false;
            S.cash -= p; S.debt -= p; LogLoan(2, p);
            CheckClean();
            return true;
        }
        public int TotalLv { get { int n = 0; foreach (var l in S.lv) n += l; return n; } }
        public double Widen => 1 + 0.1 * Lv("o_wide");      // 🔴 정비소에서 산다 (사장님 09-23: "맵 크기도 여기서 늘리게")
        public double Bo => Orbits[S.orbit].bi + (Orbits[S.orbit].bo - Orbits[S.orbit].bi) * Widen;
        public double BillAmount => S.bill < Bills.Length ? (S.billAmount >= 0 ? S.billAmount : Bills[S.bill].m) : 0;
        public bool CanBankrupt => !M.cleanReady && S.bill < Bills.Length && (S.bill >= 3 || S.overdue && S.bill >= 1);
        public int CareerCost(int i) => M.career[i] < Careers[i].cost.Length ? Careers[i].cost[M.career[i]] : -1;
        public int Unread { get { int n = 0; foreach (var it in M.news) if (!it.read) n++; return n; } }
        public Contract? CurContract => ContractsOn && S.contract >= 0 && S.contract < Contracts.Length ? Contracts[S.contract] : (Contract?)null;
        public int JunkTarget
        {
            get
            {
                var o = Orbits[S.orbit];
                int n = new[] { 90, 110, 130, 150, 175, 200, 230, 260, 260 }[Math.Min(8, S.bill)];
                double area = (Bo * Bo - o.bi * o.bi) / (o.bo * o.bo - o.bi * o.bi);       // 넓어진 만큼 더 많이
                return (int)(Math.Max(o.nMin, Math.Min(o.nMax, n)) * area);
            }
        }

        // ───────────────────────── 트리
        public double Cost(int i) => CostAt(i, S.lv[i]);
        double CostAt(int i, int l) { var n = Nodes[i]; return Math.Ceiling(n.first * Math.Pow(n.mult, l) * (1 - 0.15 * Cr(2)) * (1 - 0.05 * Lv("e_used"))); }

        // 🔴 칸 = 한 번 사기 (사장님 09-23: "한 칸에 1/3 이런식 말고 무조건 다음칸으로 넘어가지는 방식")
        //    레벨이 여럿인 칸은 많아야 셋으로 나눈다 — 한 칸이 여러 레벨을 한꺼번에 올리고, 가격은 그 레벨들 값을 합친 것
        public static int Tiles(int i) => Math.Min(Nodes[i].max, 5);
        /// <summary>j번째 칸을 사면 되는 레벨 — 앞 칸은 작게(1레벨), 뒤로 갈수록 크게. 12레벨이면 1 · 3 · 5 · 8 · 12</summary>
        public static int TileLv(int i, int j)
        {
            int T = Tiles(i), max = Nodes[i].max;
            if (j >= T) return max;
            int v = Math.Max(j, (int)Math.Round(max * Math.Pow((double)j / T, 1.6)));
            return Math.Min(v, max - (T - j));
        }
        public int NextTile(int i) { for (int j = 1; j <= Tiles(i); j++) if (TileLv(i, j) > S.lv[i]) return j; return Tiles(i) + 1; }
        public double TileCost(int i)
        {
            int j = NextTile(i); if (j > Tiles(i)) return 0;
            double c = 0; for (int l = S.lv[i]; l < TileLv(i, j); l++) c += CostAt(i, l);
            return c;
        }
        public bool BuyTile(int i)
        {
            if (!R.over || State(i) != NodeSt.Can) return false;
            S.cash -= TileCost(i); S.lv[i] = TileLv(i, NextTile(i));
            return true;
        }
        public bool BranchOpen(string br) { int b = Array.IndexOf(BranchIds, br); return S.bill >= BranchNeed[b]; }
        public NodeSt State(int i)
        {
            var n = Nodes[i];
            if (!BranchOpen(n.branch)) return NodeSt.Locked;
            if (S.lv[i] >= n.max) return NodeSt.Max;
            foreach (var p in n.par) if (S.lv[NodeIx[p]] <= 0) return NodeSt.Hidden;
            var pl = Layout[n.id];
            if (pl.par != "R" && S.lv[NodeIx[pl.par]] < TileLv(NodeIx[pl.par], pl.tile)) return NodeSt.Hidden;
            if (n.seg > Seg) return NodeSt.Hidden;
            return S.cash >= TileCost(i) ? NodeSt.Can : NodeSt.Poor;
        }
        public bool Buy(int i) => BuyTile(i);

        // ───────────────────────── 청구서 · 파산 · 경력
        public bool PayBill()
        {
            if (!R.over || S.bill >= Bills.Length || S.cash < BillAmount) return false;
            S.cash -= BillAmount;
            FinishBill();
            return true;
        }

        void FinishBill()
        {
            var b = Bills[S.bill];
            S.creditPending += b.credit;
            S.bill++;
            S.overdue = false; S.overRuns = 0; S.billAmount = -1;
            S.billDue = S.bill < Bills.Length ? Bills[S.bill].due + Lv("e_talk") : 0;
            string nid = "bill" + S.bill;
            AddNews(nid);
            Emit(SwEv.BillPaid, 0, 0, S.bill, 0, b.t + " 납부 완료 — " + b.perk);
            CheckClean();
            if (S.bill == 2 && S.contract < 0) RollContract();
        }

        public bool Bankrupt()
        {
            if (!R.over || !CanBankrupt) return false;
            M.history.Add(new PastCompany { company = M.company, bill = S.bill, runs = S.runs, minutes = (M.playSeconds - S.startedAt) / 60 });
            M.credit += S.creditPending;
            M.bankrupt++;
            AddNews(M.bankrupt == 1 ? "bankrupt1" : M.bankrupt == 2 ? "bankrupt2" : null, "궤도 청소부 (" + M.company + "대), 출동 " + S.runs + "번 만에 파산", "청구서 " + S.bill + "장을 갚고 문을 닫았다. 빚은 날아갔고, 조종사의 경력은 남았다.");
            M.company++;
            S = new SweepState { startedAt = M.playSeconds };
            MakeMarket();
            M.careerOpen = true;
            Preview();
            if (M.company == 2) AddNews("company2");
            Emit(SwEv.Bankrupt, 0, 0, M.company, 0, "주식회사 궤도 청소부 (" + (M.company - 1) + "대) — 파산");
            return true;
        }

        public bool BuyCareer(int i)
        {
            int c = CareerCost(i);
            if (c < 0 || M.credit < c) return false;
            M.credit -= c; M.career[i]++;
            return true;
        }

        public void CloseCareer() { M.careerOpen = false; }

        public void SetOrbit(int i) { if (R.over && i >= 0 && i < Orbits.Length && Open(i) && i != S.orbit) { S.orbit = i; RollContract(); Preview(); } }

        public void RollContract()
        {
            if (!ContractsOn) { S.contract = -1; return; }
            var pool = new List<int>();
            for (int i = 0; i < Contracts.Length; i++)
                if (Contracts[i].orbit == S.orbit) pool.Add(i);                  // 압류 딱지 의뢰(orbit -1)는 추심선이 쉬어서 뺐다 — 딱지가 안 붙으니 못 이룬다
            int prev = S.contract;
            for (int t = 0; t < 6; t++) { S.contract = pool[rng.Next(pool.Count)]; if (S.contract != prev || pool.Count == 1) break; }
        }

        public bool Reroll() { if (!R.over || S.rerolled || !ContractsOn) return false; S.rerolled = true; RollContract(); return true; }

        public int ContractProgress(SweepRun r)
        {
            var c = CurContract; if (c == null) return 0;
            switch (c.Value.kind)
            {
                case 0: return r.cVault; case 1: return r.cFuel; case 2: return r.cChip; case 3: return r.cSat;
                case 4: return r.chainBest; case 5: return r.cTank; case 6: return r.packBest; case 7: return r.cBig; default: return r.cTag;
            }
        }

        // ───────────────────────── 뉴스 — 궤도일보 (§11)
        public struct Story { public string id, kind, head, body; }
        public static readonly Story[] Stories =
        {
            new Story { id = "first_run", kind = "us", head = "폐업 청소업체, 새 주인 찾아", body = "궤도 청소부가 문을 닫은 지 석 달. 녹슨 청소선 한 척과 두툼한 할부 계약서가 새 주인에게 넘어갔다. 채권자 칸에는 낯익은 이름이 찍혀 있다 — 케슬러 금융. 새 사장은 「일단 연료비부터」라고만 말했다." },
            new Story { id = "bill1", kind = "us", head = "중고 드론 두 대, 청소선에 실려", body = "「줍는 건 드론, 돈 버는 건 사람」 — 드론을 판 중고상의 말이다. 드론은 한 방에 부서지는 작은 조각만 줍는다. 단단한 건 여전히 집게 몫이다." },
            new Story { id = "run6", kind = "world", head = "저궤도 쓰레기, 작년보다 40% 늘어", body = "우주청은 원인을 「불명」이라고 밝혔다. 한편 올해 발사 횟수 1위는 케슬러 발사로, 2위와의 차이는 세 배가 넘는다. 케슬러 발사 측은 「우연의 일치」라고 답했다." },
            new Story { id = "bill2", kind = "us", head = "중력 폭탄, 민간 판매 첫 허가", body = "잔해를 한데 빨아들였다 터뜨리는 중력 폭탄이 청소업체에 처음 팔렸다. 누르고 있으면 모이고, 놓으면 터진다. 제조사는 이미 폐업했고, 남은 재고는 케슬러 금융의 담보 창고에서 나왔다." },
            new Story { id = "bill3", kind = "us", head = "중궤도 청소 허가 — 폭발 탱크 주의보", body = "중궤도에는 옛 연료 탱크 수천 개가 그대로 떠 있다. 하나가 터지면 옆의 것도 터진다. 청소업계는 「조심하라」고 했지만, 몇몇 조종사는 「그게 좋다」고 했다." },
            new Story { id = "overdue1", kind = "us", head = "케슬러 금융, 연체 업체에 추심선 파견", body = "케슬러 금융은 기한을 넘긴 청소업체 궤도에 추심선을 보낸다고 밝혔다. 「압류 딱지가 붙은 잔해는 채권자 소유」라는 설명이다. 업계에서는 「딱지 붙은 걸 부수면 빚이 준다」는 말이 돈다." },
            new Story { id = "bankrupt1", kind = "us", head = "궤도 청소부 (1대) 파산… 청소선은 경매로", body = "경매에 나온 청소선의 낙찰자는 케슬러 금융. 같은 배는 다음 날 아침 다시 할부 상품으로 올라왔다. 이름도 그대로다." },
            new Story { id = "company2", kind = "us", head = "망한 이름 그대로, 다시 문 열어", body = "「이번엔 다르다.」 주식회사 궤도 청소부 (2대)가 같은 배, 같은 빚, 조금 더 능숙한 조종사로 다시 출범했다." },
            new Story { id = "bill4", kind = "us", head = "청소선 보험료 두 배로 — 보험사는 케슬러 보험", body = "큰 잔해를 건드리려면 보험이 필수다. 업계 유일의 청소선 보험사는 케슬러 보험. 보험료는 올해만 두 번 올랐다." },
            new Story { id = "bill5", kind = "world", head = "청소선 조종사, 인기 직업 7위", body = "「빚만 없으면 1위」라는 댓글이 가장 많은 추천을 받았다." },
            new Story { id = "bill6", kind = "us", head = "정지궤도 금고 위성, 주인은 누구?", body = "정지궤도를 가득 메운 금빛 위성들. 등록부에는 소유자 대신 담보 번호만 적혀 있다." },
            new Story { id = "bankrupt2", kind = "us", head = "궤도 청소부 (2대)도 파산 — 「이 회사는 망해도 온다」", body = "업계 반응은 엇갈린다. 「미련하다」와 「무섭다」. 케슬러 금융은 논평을 거부했다." },
            new Story { id = "bill7", kind = "us", head = "케슬러 금융, 할부 금리 인상 — 「한 업체 때문」", body = "케슬러 금융은 업체 이름을 밝히지 않았다. 다만 「망해도 다시 오는 회사가 있다」고 덧붙였다." },
            new Story { id = "bill8", kind = "us", head = "청소선 할부 완납 — 케슬러 금융 창사 이래 처음", body = "마지막 할부금이 들어왔다. 청소선은 이제 조종사의 것이다. 케슬러 금융은 「확인 중」이라고만 답했다." },
            new Story { id = "clean", kind = "extra", head = "궤도 청소율 100%", body = "지구 둘레에 쓰레기가 하나도 없다. 케슬러 발사는 이번 분기 발사 계획이 없다고 밝혔고, 케슬러 금융은 청소선 할부 사업 철수를 검토한다." },
            new Story { id = "s1", kind = "scoop", head = "추심선, 궤도에 잔해 버리는 모습 포착", body = "한 청소선의 블랙박스에 찍힌 영상이다. 붉은 추심선이 중궤도를 지나며 폐연료 탱크 수십 개를 떨어뜨린다. 버린 자리는 다음 날 「청소 구역」으로 지정됐다." },
            new Story { id = "s2", kind = "scoop", head = "케슬러 발사 · 금융 · 보험, 등기 주소가 같다", body = "세 회사는 같은 건물 12층, 13층, 14층을 쓴다. 엘리베이터는 하나다." },
            new Story { id = "s3", kind = "scoop", head = "궤도 청소부 (0대) 사장 인터뷰", body = "「할부는 끝까지 갚아라. 그래야 배가 네 것이 된다. 나는 못 했다.」 — 첫 번째 궤도 청소부 사장이 처음으로 입을 열었다." },
            new Story { id = "s4", kind = "scoop", head = "금고 위성 속은 비어 있었다 — 담보 서류만", body = "금고 위성 수백 기가 대출 담보로 궤도에 「보관」 중이다. 부서진 금고 안에서 나온 건 금이 아니라 서류 뭉치였다." },
            new Story { id = "s5", kind = "scoop", head = "내부 문서: 「쓰레기 1% 늘 때 대출 3% 는다」", body = "케슬러 그룹 전략 회의록이 새어 나왔다. 궤도가 더러울수록 청소선이 팔리고, 청소선이 팔릴수록 대출이 나간다." },
            new Story { id = "s6", kind = "scoop", head = "추심선 선장 익명 인터뷰 — 「우린 치우러 가는 게 아니다」", body = "「딱지를 붙이고, 가끔은 떨어뜨리고 온다. 회사가 그러라고 한다.」 그는 이번 달을 끝으로 그만둔다고 했다." },
        };
        public static readonly string[] World =
        {
            "우주 정거장 화장실 고장 3주째", "달 기지 김치 반입 허가", "궤도 위 고양이 사진 위성, 알고 보니 쓰레기", "화성 이주 신청자, 올해도 0명",
            "인공위성 이름 짓기 대회 1등: 「위성이」", "청소선 뒤 따라다니는 드론 떼, 관광 상품으로", "우주청, 「쓰레기 줍기 캠페인」 포스터 공개 — 인쇄는 케슬러 인쇄",
            "위성 인터넷 끊김, 원인은 조각 하나", "지구 사진 대회 대상: 「쓰레기 없는 부분만 찍었다」", "우주 택배 파업 이틀째",
        };

        public void AddNews(string id, string head = null, string body = null)
        {
            if (id != null)
            {
                if (M.flags.Contains("n:" + id)) return;
                M.flags.Add("n:" + id);
                foreach (var st in Stories)
                    if (st.id == id) { head = st.head; body = st.body; Push(id, st.kind, head, body); return; }
                return;
            }
            if (head != null) Push("rec", "us", head, body ?? "");
        }

        void Push(string id, string kind, string head, string body)
        {
            M.news.Add(new NewsItem { id = id, kind = kind, head = head, body = body, run = S != null ? S.runs : 0, company = M.company });
            Emit(SwEv.News, 0, 0, 0, kind == "scoop" ? 1 : 0, head);
        }

        void Record(string flag, string head, string body)
        {
            if (M.flags.Contains(flag)) return;
            M.flags.Add(flag);
            Push("rec", "us", head, body);
        }

        // ───────────────────────── 출동
        public void StartRun()
        {
            if (!R.over || S.bill >= Bills.Length && !M.cleanReady && S.debt <= 0) return;   // 청구서를 다 갚아도 빚이 남았으면 갚으러 출동한다
            bool clean = M.cleanReady;
            if (clean) S.orbit = MaxOrbit;
            S.runs++; S.rerolled = false;
            var r = new SweepRun { clean = clean };
            r.max = r.fuel = clean ? 45 : FuelMax;
            r.maxShots = clean ? 6 : Bombs; r.shots = clean ? 6 : Math.Min(1, Bombs);   // 블랙홀은 스킬 — 한 칸 들고 나가서 시간 따라 찬다
            int nfuel = clean ? 0 : Lv("c_find");
            for (int i = 0; i < nfuel; i++) r.pods.Add(new Pod { kind = 0, t = 9 + i * 7 });
            R = r;
            var o = Orbits[S.orbit];
            var forms = o.forms;
            int nf = Math.Min(forms.Length, 2 + (Rnd() < 0.5 ? 1 : 0));
            for (int i = 0; i < nf; i++) Formation(forms[i], i * 2.1 + Rnd(0, 1.5));
            if (S.runs == 1 && M.company == 1) { Formation(0, 0.5); Formation(0, 1.0); }   // 첫 판 — 커서 가까이 무리 둘 (§10)
            int target = clean ? 400 : JunkTarget;
            while (Alive() < target) Spawn(-1, -1, -1, null, false);
            foreach (var d in r.junk) d.fade = 1;
            for (int i = 0; i < DroneCount; i++) r.drones.Add(new Drone { a = i * Math.PI * 2 / Math.Max(1, DroneCount), cd = Rnd() });
            // 사건 — 10~14초, 22~26초 (연료 40 넘을 때만)
            var ev = o.events;
            if (S.bill >= 2 || clean)                                     // 판 중 사건은 청구서 2 뒤부터
            {
                r.ev1 = ev[rng.Next(ev.Length)]; r.ev1T = Rnd(10, 14);
                if (r.max >= 40) { r.ev2 = ev[rng.Next(ev.Length)]; r.ev2T = Rnd(22, 26); }
            }
            r.collector = false;                                        // 추심선은 대출로 바뀌며 쉰다
            // 블랙박스 — 청구서 2 뒤 · 판마다 25%
            if ((S.bill >= 2 || clean) && M.scoops < 6 && (clean || Rnd() < 0.25 + 0.1 * Lv("e_tip")))
            {
                var hosts = r.junk.FindAll(d => IsHost(d.k) && d.att == Att.None);
                if (hosts.Count > 0) hosts[rng.Next(hosts.Count)].att = Att.BBox;
            }
            if (clean) r.cleanGoal = 8000;
            if (S.runs == 6) AddNews("run6");
        }

        int Alive() { int n = 0; foreach (var d in R.junk) if (!d.dead) n++; return n; }

        int PickType()
        {
            var o = Orbits[S.orbit];
            double[] w = (double[])o.mix.Clone();
            w[Vault] *= 1 + 0.5 * Lv("e_vault");
            w[Fuel] = 0;                                                  // 🔴 연료는 궤도에 안 떠 있다 — 지구에서 보급한다
            if (!R.clean && S.bill < 1) w[Vault] = 0;                     // 금고는 청구서 1 뒤
            if (!R.clean && S.bill < 2 && S.orbit == 0) w[Tank] = 0;      // 폭발 탱크는 폭탄이 온 뒤
            if (!BigsOn && !R.clean) w[Big] = 0;
            double sum = 0; foreach (var x in w) sum += x;
            double v = Rnd() * sum;
            for (int i = 0; i < w.Length; i++) { v -= w[i]; if (v <= 0) return i; }
            return Chip;
        }

        Att PickAtt()
        {
            var list = new List<Att> { Att.Pouch, Att.Pouch };
            if (S.bill >= 1) list.Add(Att.Beacon);
            if (S.bill >= 2) list.Add(Att.Magnet);
            if (S.orbit >= 2 || R.clean) { list.Add(Att.Det); list.Add(Att.Det); list.Add(Att.Ice); }
            if (S.orbit == 2) { list.Add(Att.Ice); list.Add(Att.Ice); }             // 화성 — 얼음 껍질
            if (S.orbit >= 3 || R.clean) { list.Add(Att.Armor); list.Add(Att.Armor); }
            if (S.orbit == 3) list.Add(Att.Armor);                                   // 목성 — 장갑판
            return list[rng.Next(list.Count)];
        }

        Junk Spawn(int k, double a, double rr, Att? att, bool edge, double ws = -1)
        {
            var o = Orbits[S.orbit];
            if (k < 0) k = PickType();
            if (a < 0) a = Rnd(0, Math.PI * 2);
            if (rr < 0) { double u = Rnd(); if (o.gap > 0) u = u < 0.5 ? u * (1 - o.gap) : 0.5 * (1 - o.gap) + o.gap + (u - 0.5) * (1 - o.gap); rr = o.bi + u * (Bo - o.bi); }   // 토성 — 가운데 틈을 비운다                  // 띠 안 아무 곳에서 서서히 나타난다 (가장자리에서만 들어오면 바깥에 쏠린다)
            Att at = att ?? Att.None;
            if (att == null && IsHost(k) && (R.clean || S.bill >= 1) && Rnd() < (R.clean ? 0.35 : o.att * (1 + 0.4 * Lv("e_att")))) at = PickAtt();
            int hp = (int)Math.Round(Types[k].hp * (k == Fuel || k == Tank ? 1 : HpMul)) + (at == Att.Ice ? 2 : 0);   // 청구서를 갚을수록 단단해진다
            var d = new Junk { id = ++R.idc, k = k, hp = hp, max = hp, att = at, a = a, rr = rr, ws = ws > 0 ? ws : Rnd(0.92, 1.08), rot = Rnd(0, 6), vr = Rnd(-1, 1) };
            Place(d);
            R.junk.Add(d);
            return d;
        }

        static void Place(Junk d) { if (!d.free) { d.x = EX + Math.Cos(d.a) * d.rr; d.y = EY + Math.Sin(d.a) * d.rr * Tilt; } }

        Junk SpawnFree(int k, double x, double y, double vx, double vy, double capT, Att att = Att.None)
        {
            var d = Spawn(k, 0, 200, att, false);
            d.free = true; d.x = x; d.y = y; d.vx = vx; d.vy = vy; d.capT = capT; d.fade = 1;
            return d;
        }

        void Formation(int kind, double a0)
        {
            var o = Orbits[S.orbit]; double mid = (o.bi + Bo) / 2;
            switch (kind)
            {
                case 0: Spawn(Sat, a0, mid, null, false, 1); for (int i = 0; i < 13; i++) Spawn(Chip, a0 + Rnd(-0.11, 0.11), mid + Rnd(-22, 22), Att.None, false, 1); break;
                case 1: for (int i = 0; i < 8; i++) Spawn(Tank, a0 + i * 0.1, mid + 12, Att.None, false, 1); break;
                case 2:
                {
                    var ring = new Junk[6];
                    for (int i = 0; i < 6; i++) { double an = i / 6.0 * Math.PI * 2; ring[i] = Spawn(Sat, a0 + Math.Cos(an) * 0.15, mid + Math.Sin(an) * 38, i == 0 ? Att.Magnet : Att.Cable, false, 1); }
                    for (int i = 0; i < 6; i++) { ring[i].link1 = ring[(i + 1) % 6]; ring[i].link2 = ring[(i + 5) % 6]; }
                    break;
                }
                case 3:
                    for (int i = 0; i < 5; i++) { var v = Spawn(Vault, a0 + i * 0.08, Bo - 26, Att.None, false, 3.2); v.convoy = true; }
                    Spawn(Sat, a0 - 0.08, Bo - 26, Att.Armor, false, 3.2).convoy = true;
                    Spawn(Sat, a0 + 0.42, Bo - 26, Att.Armor, false, 3.2).convoy = true;
                    break;
                case 4:
                    Spawn(BigsOn || R.clean ? Big : Rocket, a0, mid, Att.Ice, false, 1);
                    Spawn(BigsOn || R.clean ? Big : Rocket, a0 + 0.2, mid + 18, Att.Det, false, 1);
                    for (int i = 0; i < 10; i++) Spawn(Rnd() < 0.5 ? Sat : Rocket, a0 + Rnd(-0.2, 0.4), mid + Rnd(-40, 40), Rnd() < 0.3 ? Att.Armor : Rnd() < 0.3 ? Att.Det : Att.None, false, 1);
                    break;
            }
        }

        // ───────────────────────── 한 걸음
        public void Tick(double dt, double ax, double ay, bool aim, bool hold)
        {
            var r = R; if (r.over) return;
            M.playSeconds += dt; r.t += dt;
            if (aim) { r.ax = ax; r.ay = ay; }
            if (r.fuel > 0) r.fuel -= dt;
            if (r.clean) { r.refillT += dt; if (r.refillT > 2) { r.refillT = 0; if (r.shots < r.maxShots) r.shots++; } }

            // 🌀 블랙홀 스킬 — 누르면 그 자리에 열려 3초 빨아들이고 저절로 터진다. 칸은 시간 따라 찬다
            if (!r.clean && r.shots < r.maxShots) { r.holeCd += dt; if (r.holeCd >= HoleCd) { r.holeCd = 0; r.shots++; Emit(SwEv.SkillReady, r.ax, r.ay, r.shots); } }
            else r.holeCd = 0;
            if (hold && !r.holding && r.shots > 0 && r.fuel > 0) { r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.hx = r.ax; r.hy = r.ay; }
            if (r.holding && (r.holdT >= HoleDur || r.fuel <= 0)) Release();

            Schedule(dt);
            Supply(dt);
            Motion(dt);
            if (r.holding) Pull(dt);
            if (aim && r.fuel > 0) Claw(dt);                           // 블랙홀이 열려 있어도 빔은 계속
            Drones(dt);

            // 💥 연쇄
            for (int i = 0; i < r.pend.Count; i++) r.pend[i].t -= dt;
            for (int i = r.pend.Count - 1; i >= 0; i--)
            {
                var p = r.pend[i];
                if (p.t > 0) continue;
                r.pend.RemoveAt(i);
                DoBlast(p.x, p.y, p.R * BlastK);
            }
            r.chainT -= dt;
            if (r.chainT <= 0 && r.pend.Count == 0 && !r.holding) { r.chain = 0; r.tier = 0; }

            // 치우기 · 다시 채우기 (띠 안 아무 곳에서 스며든다)
            r.junk.RemoveAll(d => d.dead && !r.packed.Contains(d));
            int target = r.clean ? 400 : JunkTarget, alive = Alive();
            r.refill = Math.Min(6, r.refill + dt * target * RefillRate);
            while (alive < target && r.refill >= 1) { Spawn(-1, -1, -1, null, true); alive++; r.refill -= 1; }
            r.formT -= dt;
            if (r.formT <= 0) { r.formT = 9; if (alive < target + 40) { var f = Orbits[S.orbit].forms; Formation(f[rng.Next(f.Length)], Rnd(0, Math.PI * 2)); } }

            if (r.fuel <= 0 && !r.holding && r.pend.Count == 0)
            {
                if (r.clean) { r.cleanKills = Math.Max(r.cleanKills, r.cleanGoal); DoBlast(EX, EY, 420); EndRun(); return; }   // 청산 출동은 연료를 다 쓰면 끝 — 늘 성공
                r.fuel = 0; r.endT += dt;
                if (r.endT > 1.3) EndRun();
            }
        }

        void Schedule(double dt)
        {
            var r = R;
            void Check(ref double t, ref bool warned, int ev)
            {
                if (t < 0 || ev < 0) return;
                if (!warned && r.t >= t - 2) { warned = true; Emit(SwEv.Warn, 0, 0, ev, ev == 2 ? 0 : 1, "⚠ " + EventNames[ev] + (ev == 2 ? " — 왼쪽" : ev == 4 || ev == 1 ? " — 오른쪽 위" : "")); }
                if (r.t >= t) { t = -1; FireEvent(ev); }
            }
            Check(ref r.ev1T, ref r.ev1Warn, r.ev1);
            Check(ref r.ev2T, ref r.ev2Warn, r.ev2);
            if (r.collector && r.t > 6) { r.collector = false; Collector(); }
            if (r.stormLeft > 0)
            {
                r.stormT -= dt;
                while (r.stormT <= 0 && r.stormLeft > 0)
                {
                    r.stormT += 0.12; r.stormLeft--;
                    var o = Orbits[S.orbit];
                    SpawnFree(Chip, -10, EY + Rnd(-Bo * Tilt, Bo * Tilt), Rnd(170, 240), Rnd(-20, 20), 2.6);
                }
            }
        }

        void FireEvent(int ev)
        {
            var r = R; var o = Orbits[S.orbit]; double mid = (o.bi + Bo) / 2;
            Emit(SwEv.EventGo, 0, 0, ev, 0, EventNames[ev]);
            switch (ev)
            {
                case 0: for (int i = 0; i < 5; i++) SpawnFree(Fuel, -10 - i * 40, EY + 40 + i * 8, 150, 0, 3.2); break;
                case 1:
                case 4:
                {
                    double a = -0.5, x = EX + Math.Cos(a) * mid, y = EY + Math.Sin(a) * mid * Tilt;
                    int n = ev == 1 ? 30 : 60;
                    Emit(SwEv.Ring, x, y, ev == 1 ? 70 : 120, 1);
                    for (int i = 0; i < n; i++)
                    {
                        int k = Rnd() < 0.7 ? Chip : Rnd() < 0.5 ? Sat : Tank;
                        SpawnFree(k, x, y, Rnd(-220, 220), Rnd(-160, 160), Rnd(1, 1.8), ev == 4 && k == Sat && Rnd() < 0.5 ? Att.Det : Att.None);
                    }
                    break;
                }
                case 2: r.stormLeft = 40; r.stormT = 0; break;
                case 3: Formation(3, Rnd(0, Math.PI * 2)); break;
            }
        }

        void Supply(double dt)
        {
            var r = R;
            foreach (var p in r.pods)
            {
                if (p.got) continue;
                if (!p.up)
                {
                    if (r.t < p.t) continue;
                    p.up = true;
                    double a = Math.Atan2(r.ay - EY, r.ax - EX);
                    p.x = EX + Math.Cos(a) * 40; p.y = EY + Math.Sin(a) * 40;
                    Emit(SwEv.Supply, p.x, p.y, 0, p.kind, p.kind == 1 ? "지구 보급 — 폭탄" : "지구 보급 — 연료");
                    continue;
                }
                double dx = r.ax - p.x, dy = r.ay - p.y, d = Math.Sqrt(dx * dx + dy * dy);
                double sp = (240 + 60 * Lv("s_speed")) * dt;
                if (d <= Math.Max(14, sp))
                {
                    p.got = true;
                    if (p.kind == 1) { r.shots = Math.Min(6, r.shots + 1); Emit(SwEv.SupplyGet, r.ax, r.ay - 18, 0, 1, "폭탄 +1"); }
                    else { double s2 = Math.Min(4, r.max - r.fuel); if (s2 > 0) r.fuel += s2; Emit(SwEv.SupplyGet, r.ax, r.ay - 18, 0, 0, "연료 +4초"); }
                    continue;
                }
                p.x += dx / d * sp; p.y += dy / d * sp;
            }
        }

        void Collector()
        {
            var hosts = R.junk.FindAll(d => !d.dead && IsHost(d.k) && d.att == Att.None);
            hosts.Sort((p, q) => Types[q.k].val.CompareTo(Types[p.k].val));
            for (int i = 0; i < Math.Min(6, hosts.Count); i++) { hosts[i].att = Att.Tag; Emit(SwEv.Ring, hosts[i].x, hosts[i].y, 18, 1); }
            Emit(SwEv.Collector, 0, 0, 0, 0, "추심선이 압류 딱지를 붙이고 간다 — 부수면 빚이 준다");
        }

        void Motion(double dt)
        {
            var o = Orbits[S.orbit];
            foreach (var d in R.junk)
            {
                if (d.dead) continue;
                d.fade = Math.Min(1, d.fade + dt * 1.4); d.hit = Math.Max(0, d.hit - dt); d.rot += d.vr * dt;
                if (d.free)
                {
                    d.x += d.vx * dt; d.y += d.vy * dt; d.vx *= 1 - 0.9 * dt; d.vy *= 1 - 0.9 * dt;
                    if (d.capT > 0) { d.capT -= dt; if (d.capT <= 0) Recapture(d, o.bi, Bo); }
                }
                else
                {
                    d.a += 0.12 * d.ws * dt * o.spin;
                    if (o.pull > 0 && d.tr <= 0 && d.rr > o.bi + 6) d.rr = Math.Max(o.bi + 6, d.rr - o.pull * dt * (Types[d.k].heavy ? 0.5 : 1));   // 목성 — 안쪽으로 끌린다
                    if (d.tr > 0) { double step = (25 + 20 * (d.ws - 0.92) / 0.16) * dt; if (Math.Abs(d.tr - d.rr) <= step) { d.rr = d.tr; d.tr = 0; } else d.rr += Math.Sign(d.tr - d.rr) * step; }
                    Place(d);
                }
            }
        }

        void Recapture(Junk d, double bi, double bo)
        {
            d.free = false; d.capT = 0; d.vx = d.vy = 0; d.tr = 0;
            d.a = Math.Atan2((d.y - EY) / Tilt, d.x - EX);
            double rad = Math.Sqrt((d.x - EX) * (d.x - EX) + (d.y - EY) / Tilt * (d.y - EY) / Tilt);
            // 띠 밖에서 붙잡히면 끝에 들러붙지 않고 띠 안 아무 자리로 천천히 내려간다 (바깥 테두리에만 쌓이던 것)
            if (rad > bo || rad < bi) { d.rr = Math.Max(bi * 0.8, Math.Min(bo + 60, rad)); d.tr = bi + Rnd() * (bo - bi); }
            else d.rr = rad;
        }

        void Pull(double dt)
        {
            var r = R; r.holdT += dt;
            double pr = PullR, pf = PullF;
            foreach (var d in r.junk)
            {
                if (d.dead || Types[d.k].big || d.att == Att.Armor) continue;
                double dx = r.hx - d.x, dy = r.hy - d.y, dist = Math.Sqrt(dx * dx + dy * dy) + 1;
                if (dist > pr) continue;
                if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                d.capT = 0;
                double f = 26000 * pf / (dist + 40) / (Types[d.k].heavy ? 2.5 : 1);
                d.vx += (dx / dist * f - dy / dist * f * 0.35) * dt;
                d.vy += (dy / dist * f + dx / dist * f * 0.35) * dt;
                d.vx *= 1 - 1.6 * dt; d.vy *= 1 - 1.6 * dt;              // 끌려 드는 동안 감겨 들어간다 (빙빙 돌기만 하지 않게)
                if (dist < 22)
                {
                    d.dead = true; r.packed.Add(d);
                    foreach (var l in new[] { d.link1, d.link2 })
                        if (l != null && !l.dead && !l.free) { l.free = true; l.vx = (r.hx - l.x) * 1.5; l.vy = (r.hy - l.y) * 1.5; }
                }
            }
            if (r.packed.Count > Cap)
            {
                // 붕괴 — 절반은 절반 값으로 흩어지고, 나머지는 그 자리에서 터진다
                int lost = r.packed.Count / 2;
                for (int i = 0; i < lost; i++) { var d = r.packed[0]; r.packed.RemoveAt(0); Pay(d, 2, Types[d.k].val * ValMult * 0.5, r.hx, r.hy, false); }
                Emit(SwEv.Collapse, r.hx, r.hy, lost);
                Release();
            }
        }

        void Release()
        {
            var r = R;
            r.holding = false; r.shots--;
            int n = r.packed.Count;
            r.packBest = Math.Max(r.packBest, n); M.bestPack = Math.Max(M.bestPack, n);
            if (n >= 50) Record("pack50", "중력 폭탄 하나에 잔해 " + n + "개 — 제조사 「그렇게 쓰라고 만든 게 아닌데」", "민간 청소선이 중력 폭탄 하나로 잔해 " + n + "개를 한데 모아 터뜨렸다.");
            if (n >= 100) Record("pack100", "잔해 " + n + "개를 한 점에 — 「작은 블랙홀을 봤다」", "지상 관측소에서도 한 점으로 빨려 드는 빛이 보였다고 한다.");
            double mult = 1 + n * PackK;
            foreach (var d in r.packed)
            {
                d.x = r.hx + Rnd(-8, 8); d.y = r.hy + Rnd(-8, 8); d.dead = false;
                Kill(d, 2, mult);
            }
            r.packed.Clear();
            Emit(SwEv.Release, r.hx, r.hy, n, 0, n >= 6 ? n + "개 압축 · ×" + mult.ToString("0.00") : null);
            DoBlast(r.hx, r.hy, (60 + n * 2.5) * BlastK, false);
            // 모이다 만 것들은 궤도로 돌아간다
            foreach (var d in r.junk) if (d.free && !d.dead && d.capT <= 0) d.capT = 0.6;
        }

        void Claw(double dt)
        {
            var r = R;
            r.next -= dt;
            if (Lv("c_magnet") > 0)
            {
                double mr = Math.Max(ClawR, PickR) + 40 + 20 * Lv("c_magnet");
                foreach (var d in r.junk)
                {
                    if (d.dead || d.k != Chip) continue;
                    double dx = r.ax - d.x, dy = r.ay - d.y, dd = dx * dx + dy * dy;
                    if (dd > mr * mr || dd < 100) continue;
                    if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                    d.capT = 0.5; d.vx += dx * 2.5 * dt; d.vy += dy * 2.5 * dt;
                }
            }
            if (r.next > 0) return;
            r.next = Gap * (Lv("c_over") > 0 && r.fuel < 5 ? 0.5 : 1);
            Strike();
            if (Rnd() < 0.1 * Lv("c_double")) Strike();
        }

        void Strike()
        {
            var r = R;
            if (ClawR <= 0)
            {
                // 범위가 없으면 커서 밑의 하나만
                Junk best = null; double bd = double.MaxValue;
                foreach (var d in r.junk)
                {
                    if (d.dead) continue;
                    double dx = d.x - r.ax, dy = d.y - r.ay, dd = dx * dx + dy * dy, lim = PickR + Types[d.k].r;
                    if (dd < lim * lim && dd < bd) { bd = dd; best = d; }
                }
                bool c1 = best != null && Rnd() < Crit;
                if (best != null) Hit(best, ClawDmg * (c1 ? 3 : 1), 0, true);
                Emit(SwEv.Strike, r.ax, r.ay, PickR, best != null ? 1 : 0);
                if (c1) Emit(SwEv.Crit, r.ax, r.ay - 20);
                return;
            }
            double R0 = ClawR; bool hit = false, crit = Rnd() < Crit; int dmg = ClawDmg * (crit ? 3 : 1);
            var list = r.junk;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.dead) continue;
                double dx = d.x - r.ax, dy = d.y - r.ay;
                if (dx * dx + dy * dy > (R0 + Types[d.k].r) * (R0 + Types[d.k].r)) continue;
                hit = true; Hit(d, dmg, 0, true);
            }
            Emit(SwEv.Strike, r.ax, r.ay, R0, hit ? 1 : 0);
            if (hit && crit) Emit(SwEv.Crit, r.ax, r.ay - R0 - 8);
        }

        void Hit(Junk d, int dmg, int src, bool spread)
        {
            if (d.dead) return;
            if (d.att == Att.Armor && src == 0) dmg = Math.Min(dmg, 1);
            d.hp -= dmg; d.hit = 0.12;
            if (d.att == Att.Ice && d.hp <= d.max - 2)
            {
                d.att = Att.None;
                for (int i = 0; i < 4; i++) SpawnFree(Chip, d.x, d.y, Rnd(-90, 90), Rnd(-90, 90), 1.2);
                Emit(SwEv.Shatter, d.x, d.y);
            }
            if (spread && d.att == Att.Cable)
                foreach (var l in new[] { d.link1, d.link2 }) if (l != null && !l.dead) Hit(l, Math.Max(1, dmg / 2), src, false);
            if (d.hp <= 0) Kill(d, src, 1);
        }

        void Drones(double dt)
        {
            var r = R; var o = Orbits[S.orbit];
            if (r.rushT > 0) r.rushT -= dt;
            double mid = (o.bi + Bo) / 2, reach = Reach, cd = DroneCd; int grade = Grade, per = Lv("d_pair") > 0 ? 2 : 1;
            foreach (var dr in r.drones)
            {
                dr.a += dt * 0.45;
                double tx = EX + Math.Cos(dr.a) * mid, ty = EY + Math.Sin(dr.a) * mid * Tilt;
                if (r.rushT > 0) { tx = r.rushX + Math.Cos(dr.a * 3) * 30; ty = r.rushY + Math.Sin(dr.a * 3) * 20; }
                if (dr.x == 0 && dr.y == 0) { dr.x = tx; dr.y = ty; }
                double kk = Math.Min(1, dt * (r.rushT > 0 ? 4 : 6)); dr.x += (tx - dr.x) * kk; dr.y += (ty - dr.y) * kk;
                dr.cd -= dt; if (dr.cd > 0) continue;
                dr.cd = r.rushT > 0 ? cd * 0.35 : cd;
                for (int p = 0; p < per; p++)
                {
                    Junk best = null; double bd = reach * reach;
                    foreach (var d in r.junk)
                    {
                        if (d.dead || d.hp > grade || Types[d.k].big || d.k == Fuel || d.att == Att.Armor || d.att == Att.FuelPod) continue;   // 🔴 한 방에 부술 수 있는 것만 줍는다
                        double dx = d.x - dr.x, dy = d.y - dr.y, dd = dx * dx + dy * dy;
                        if (dd < bd) { bd = dd; best = d; }
                    }
                    if (best == null) break;
                    Emit(SwEv.Beam, dr.x, dr.y, 0, 0, null, best.x, best.y);
                    Hit(best, grade, 1, false);
                }
            }
        }

        void DoBlast(double x, double y, double Rb, bool chainable = true)
        {
            Emit(SwEv.Blast, x, y, Rb);
            var list = R.junk;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.dead || d.fade < 0.35) continue;              // 막 스며든 것은 아직 안 맞는다
                double dx = d.x - x, dy = d.y - y, rr = Rb + Types[d.k].r;
                if (dx * dx + dy * dy > rr * rr) continue;
                if (Types[d.k].big) { d.hp -= 3; d.hit = 0.15; if (d.hp <= 0) Kill(d, 2, 1); }
                else { d.hp -= BlastDmg; d.hit = 0.15; if (d.hp <= 0) Kill(d, 2, 1); }
            }
        }

        void Kill(Junk d, int src, double mult)
        {
            if (d.dead) return;
            d.dead = true;
            var r = R;
            r.broke++; M.broken++;
            if (r.clean) r.cleanKills++;
            switch (d.k) { case Vault: r.cVault++; break; case Rocket: r.cFuel++; break; case Chip: r.cChip++; break; case Sat: r.cSat++; break; case Tank: r.cTank++; break; case Big: r.cBig++; break; }
            if (d.k == Big) Record("big1", "30년 떠다닌 우주선 잔해, 드디어 사라져", "큰 잔해 하나가 궤도에서 사라졌다. 청소업계는 「보험료가 아깝지 않다」고 했다.");
            if (src == 2)
            {
                r.chain++; r.chainT = 1.6;
                r.chainBest = Math.Max(r.chainBest, r.chain); M.bestChain = Math.Max(M.bestChain, r.chain);
                int tier = r.chain >= 200 ? 4 : r.chain >= 80 ? 3 : r.chain >= 30 ? 2 : r.chain >= 10 ? 1 : 0;
                if (tier > r.tier) { r.tier = tier; Emit(SwEv.Tier, d.x, d.y, r.chain, tier); }
                if (r.chain == 30 || r.chain == 80 || r.chain == 200) Record("chain" + r.chain, "민간 청소선, 잔해 " + r.chain + "개 연쇄 파괴" + (r.chain >= 200 ? " — 지상에서도 보였다" : ""), "폭발이 폭발을 불렀다. 궤도일보 관측팀은 「케슬러 연쇄를 일부러 일으킨 첫 사례」라고 적었다.");
                if (r.chain < ChainMax && Rnd() < ChainP) r.pend.Add(new Blast { x = d.x, y = d.y, t = Rnd(0.06, 0.14), R = 40 });
            }
            double v = Types[d.k].val * ValMult * mult;
            if (d.att == Att.Pouch) v *= 2;
            if (src == 1) v *= DroneMag;
            if (src == 2) v *= 1 + Math.Min(r.chain, 100) / 200.0;       // 연쇄가 길면 값이 조금 더 붙는다 (최대 ×1.5)
            Pay(d, src, v, d.x, d.y, true);
            Emit(SwEv.Broke, d.x, d.y, 0, d.k * 100 + (int)d.att);
            if (d.k == Fuel && AddFuel(3) > 0) Emit(SwEv.Pop, d.x, d.y, 0, 1, "연료 +3초");
            if (d.k == Tank && r.chain < ChainMax) r.pend.Add(new Blast { x = d.x, y = d.y, t = 0.05, R = 58 });
            switch (d.att)
            {
                case Att.FuelPod: if (AddFuel(2) > 0) Emit(SwEv.Pop, d.x, d.y - 10, 0, 1, "연료 +2초"); break;
                case Att.Det: if (r.chain < ChainMax) r.pend.Add(new Blast { x = d.x, y = d.y, t = 0.07, R = 50 }); break;
                case Att.Ice: for (int i = 0; i < 4; i++) SpawnFree(Chip, d.x, d.y, Rnd(-90, 90), Rnd(-90, 90), 1.2); break;
                case Att.Magnet:
                    Emit(SwEv.Ring, d.x, d.y, 90, 2);
                    foreach (var q in r.junk) if (!q.dead && q.k == Chip) { double dx = d.x - q.x, dy = d.y - q.y; if (dx * dx + dy * dy < 8100) { q.free = true; q.vx = dx * 2.2; q.vy = dy * 2.2; q.capT = 1; } }
                    break;
                case Att.Beacon: r.rushT = 3 + 2 * Lv("d_sig"); r.rushX = d.x; r.rushY = d.y; Emit(SwEv.Ring, d.x, d.y, 40, 2); break;
                case Att.BBox:
                    M.scoops++; S.creditPending += 1;
                    AddNews("s" + M.scoops);
                    Emit(SwEv.Pop, d.x, d.y - 14, 0, 3, "블랙박스 — 특종 제보!");
                    break;
            }
        }

        // 연료는 탱크를 넘지 않고, 한 판에 되찾는 양도 탱크의 60%까지 — 판이 끝없이 길어지지 않게
        double AddFuel(double s) { var r = R; double room = Math.Max(0, r.max * 0.6 - r.fuelGot); s = Math.Min(s, Math.Min(room, r.max - r.fuel)); if (s <= 0) return 0; r.fuelGot += s; r.fuel += s; return s; }

        void Pay(Junk d, int src, double v, double x, double y, bool coin)
        {
            var r = R;
            if (v <= 0) return;
            if (d.att == Att.Tag && S.bill < Bills.Length)
            {
                double toBill = v * 1.5;
                r.toBill += toBill; r.cTag++;
                S.billAmount = Math.Max(0, BillAmount - toBill);
                Emit(SwEv.Coin, x, y, toBill, 3);
                return;
            }
            double cutAmt = Math.Min(v * Cut, S.debt), got = v - cutAmt;
            S.cash += got; r.cut += cutAmt; S.debt -= cutAmt;
            if (src == 0) r.earnClaw += got; else if (src == 1) r.earnDrone += got; else r.earnBlast += got;
            if (coin) Emit(SwEv.Coin, x, y, got, src + (cutAmt > 0 ? 10 : 0));
        }

        void EndRun()
        {
            var r = R;
            r.over = true; M.totalRuns++;
            if (r.clean)
            {
                M.won = true; M.cleanReady = false;
                M.history.Add(new PastCompany { company = M.company, bill = S.bill, runs = S.runs, minutes = (M.playSeconds - S.startedAt) / 60, won = true });
                AddNews("clean");
                Emit(SwEv.Won);
                return;
            }
            var c = CurContract;
            if (c != null) { r.contractText = c.Value.text; r.contractTarget = c.Value.target; r.contractProg = Math.Min(ContractProgress(r), c.Value.target); }
            if (c != null && ContractProgress(r) >= c.Value.target) { r.contractOk = true; r.bonus = Math.Ceiling(r.Earned * (0.25 + 0.1 * Lv("e_quest"))); S.cash += r.bonus; }
            if (Lv("e_save") > 0) { r.interest = Math.Min(r.Earned, Math.Floor(S.cash * 0.02 * Lv("e_save"))); S.cash += r.interest; }
            if (S.bill < Bills.Length)
            {
                if (BillAmount <= 0) FinishBill();              // 압류 딱지로 다 갚았다
                else if (!S.overdue)
                {
                    S.billDue--;
                    if (S.billDue <= 0) { S.overdue = true; S.overRuns = 0; Emit(SwEv.Overdue); }   // 납부일 — 갚거나 · 대출받아 갚거나 · 파산 (연체 이자 · 추심은 없앴다)
                }
                else S.overRuns++;
            }
            if (r.cut > 0) LogLoan(1, r.cut);
            S.lastClaw = r.earnClaw; S.lastDrone = r.earnDrone; S.lastBlast = r.earnBlast; S.runEarn.Add(r.earnClaw + r.earnDrone + r.earnBlast); if (S.runEarn.Count > 6) S.runEarn.RemoveAt(0); S.lastBroke = r.broke; S.lastChain = r.chainBest;
            S.lastContract = r.contractText == null ? 0 : r.contractOk ? 1 : 2;
            LottoDraw();                                                // 🎱 추첨 날이면
            CheckClean();                                               // 판 수입에서 떼어 빚을 다 갚았을 수도
            RollContract();
            Emit(SwEv.RunEnd);
        }

        /// <summary>출동 사이 — 조종실 창밖에서 궤도와 드론이 계속 돈다 (규칙은 안 움직인다)</summary>
        /// <summary>조종실 창밖에 보일 궤도 — 출동 전에도 쓰레기와 드론이 떠 있다</summary>
        public void Preview()
        {
            R = new SweepRun { over = true };
            int n = JunkTarget;
            for (int i = 0; i < n; i++) Spawn(-1, -1, -1, null, false).fade = 1;
            for (int i = 0; i < DroneCount; i++) R.drones.Add(new Drone { a = i * Math.PI * 2 / Math.Max(1, DroneCount) });
        }

        public void IdleTick(double dt)
        {
            var o = Orbits[S.orbit];
            foreach (var d in R.junk)
            {
                if (d.dead) continue;
                d.hit = 0; d.fade = 1; d.rot += d.vr * dt;
                if (d.free) Recapture(d, o.bi, Bo);
                d.a += 0.12 * d.ws * dt * o.spin; Place(d);                  // 행성마다 도는 빠르기 (창밖도)
            }
            double mid = (o.bi + Bo) / 2;
            foreach (var dr in R.drones) { dr.a += dt * 0.45; dr.x = EX + Math.Cos(dr.a) * mid; dr.y = EY + Math.Sin(dr.a) * mid * Tilt; }
        }

        public void ReadAll() { foreach (var n in M.news) n.read = true; }
    }
}
