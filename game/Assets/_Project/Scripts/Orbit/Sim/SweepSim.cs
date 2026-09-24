using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🔴 rev17 「궤도 청소부」 규칙 전부 (2026-09-23). 정본 = wiki/rev17-detail.md. UnityEngine 을 안 쓴다 — tools/pacing 봇이 그대로 컴파일한다.
    //    도구 셋: 집게(커서에 대면 저절로 친다) · 드론(알아서 줍는다) · 블랙홀 폭탄(누르면 모으고 떼면 터진다)
    //    흐름: 출동 → 결산 → 정비소(트리 24칸) → 청구서 8장 → 파산 → 빚 청산 → 청산 출동
    //    좌표는 시안과 같은 960×600 「화면 점」 — 지구 (480,310), 궤도는 세로로 0.6 눌린 타원.

    public enum Att { None, FuelPod, Pouch, Beacon, Magnet, Det, Ice, Cable, Armor, BBox, Tag, Gold, Rock }

    public class Junk
    {
        public int id, k, hp, max, sp = -1;                                   // sp = 종 (모습 · 등급) — SweepSim.Spc
        public Att att;
        public double a, rr, ws, x, y, vx, vy, capT, fade, hit, rot, vr, tr, frz;   // frz = 얼어 있는 시간 (냉동 빔)
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
        public int weapon, weapon2 = -1, keys, front1 = -1, front2 = -1, frontPick = -1;       // ★ 1면 조작 — 고를 기사 둘                               // ⚔ 무기 · 보조 무기 · 🔑 열쇠
        public int[] parts = { -1, -1, -1, -1, -1 };                           // 🔩 부품 칸 다섯
        public List<int> shop = new List<int>();
        public int shopSale = -1, nFuel, nDmg, nVal; public bool freeRoll = true;   // 🔩 가게 v2 — 오늘의 반값 칸 · 판마다 공짜 새로고침 · 🧃 소모품(다음 판)                             // 가게 진열 (Parts.Key = 열쇠)                                                   // ⚔ 장착한 무기 (0 집게 빔 · 1 레이저 · 2 번개)
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
        public int legend, depth, bestDepth; public bool endless;          // ★ 전설 경력(청산마다) · ∞ 무한 궤도 층 (09-24 사장님 12 · 26번)
        public double bestAuc;                                           // 고철 경매 최고 배수
        public List<string> perm = new List<string>();                    // 🔑 ◆ 핵심 칸 — 파산해도 남는다 (09-24 사장님 「열쇠가 떡벽」 → 파산하면 강해진다)
        public bool won, careerOpen, cleanReady;
        public List<NewsItem> news = new List<NewsItem>();
        public List<string> flags = new List<string>();
        public List<PastCompany> history = new List<PastCompany>();
    }

    public class Blast { public double x, y, t, R; public bool w; }     // w = 무기가 낸 폭발 (이것만 또 번진다 — 09-24 사장님 「연쇄 반응도 무기 특성으로」)
    public class Drone { public double a, cd, x, y; }
    public class Pod { public int kind; public double t, x, y; public bool up, got; }     // 지구 보급 — kind 0 연료 · 1 폭탄

    public class SweepRun
    {
        public readonly double[] chan = new double[9], chanNext = new double[9], chanX = new double[9], chanY = new double[9];   // 이어서 쏘는 확률 효과 · 그 무기의 목표
        public double volley, volleyT, volleyNext; public int consDmg, consVal;   // 🧃 이번 판 소모품                          // 🚀 전탄 발사 게이지 · 퍼붓는 중
        public double shipA = -1.57, heat = 1, next2, idleT, rockT = -1, fenceT, magHole, magX, magY; public int vacAmmo; public readonly List<Blast> mines = new List<Blast>(); public bool twin, lazyDone, tourDone; public int insiderN, meteorAt = 80, meteors, weaponKills, goldN;                            // 청소선 — 궤도 바깥에서 조준 방향으로 따라온다 · 레이저 열
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

    public enum SwEv { Supply, SupplyGet, Strike, Broke, Coin, Pop, Beam, Ring, Blast, Tier, Crit, Collapse, Warn, EventGo, Collector, Shatter, Release, RunEnd, BillPaid, Overdue, Bankrupt, News, Won, SkillReady, NodeBought, Laser, Bolt, Meteor, Tourist, Vac, Shell, Rail , Proc, Act, Volley }

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
            new OrbitDef { name = "지구", desc = "기본 궤도",                         bi = 150, bo = 296, mult = 1, att = 0.10, spin = 1,    hp = 1,   sell = 0, permit = 0,       mix = new double[] { 60, 22, 4, 2, 5, 4, 1.2 },  nMin = 127, nMax = 212, forms = new[] { 0, 0 },       events = new[] { 0, 1 } },
            new OrbitDef { name = "달",   desc = "느린 궤도 · 금고 위성이 많다",       bi = 118, bo = 277, mult = 1.5, att = 0.15, spin = 0.6,  hp = 1.1, sell = 2, permit = 1500,     mix = new double[] { 48, 22, 6, 9, 5, 4, 1.2 },  nMin = 144, nMax = 244, forms = new[] { 0, 1 },       events = new[] { 0, 3 } },
            new OrbitDef { name = "화성", desc = "모래 폭풍 · 얼음 껍질",             bi = 132, bo = 314, mult = 2.2, att = 0.25, spin = 1.1,  hp = 1.3, sell = 3, permit = 20000,    storm = true, mix = new double[] { 48, 22, 12, 3, 5, 7, 1.5 }, nMin = 172, nMax = 316, forms = new[] { 1, 2, 0 }, events = new[] { 2, 1 } },
            new OrbitDef { name = "목성", desc = "중력이 잔해를 안쪽에 모은다 · 장갑판", bi = 188, bo = 357, mult = 4.5, att = 0.30, spin = 1.3,  hp = 1.6, sell = 5, permit = 1500000,  pull = 9, mix = new double[] { 40, 18, 10, 6, 5, 8, 2.5 }, nMin = 238, nMax = 392, forms = new[] { 3, 4, 1 }, events = new[] { 3, 4 } },
            new OrbitDef { name = "토성", desc = "두 겹 고리 · 케이블 망",            bi = 150, bo = 345, mult = 6.5, att = 0.30, spin = 0.9,  hp = 2.0, sell = 6, permit = 15000000, gap = 0.28, mix = new double[] { 38, 18, 8, 8, 5, 6, 3 }, nMin = 257, nMax = 415, forms = new[] { 2, 2, 3, 4 }, events = new[] { 3, 4 } },
            new OrbitDef { name = "소행성대", desc = "암석 무리 · 단단한 잔해 · 광석",     bi = 140, bo = 330, mult = 3.2, att = 0.28, spin = 1.2,  hp = 1.45, sell = 4, permit = 150000,   mix = new double[] { 44, 20, 12, 4, 5, 7, 2 },   nMin = 205, nMax = 350, forms = new[] { 1, 3, 0 }, events = new[] { 2, 3 } },
            new OrbitDef { name = "천왕성", desc = "옆으로 누운 궤도 · 얼음 결정",       bi = 180, bo = 380, mult = 10, att = 0.35, spin = 0.8,  hp = 2.8, sell = 8, permit = 50000000, pull = 6, mix = new double[] { 34, 18, 10, 9, 5, 8, 4 }, nMin = 280, nMax = 440, forms = new[] { 2, 3, 4 }, events = new[] { 3, 4 } },
            new OrbitDef { name = "해왕성", desc = "초속 폭풍 · 무거운 잔해",           bi = 175, bo = 390, mult = 15, att = 0.38, spin = 1.4,  hp = 3.8, sell = 10, permit = 200000000, storm = true, mix = new double[] { 32, 16, 10, 10, 5, 9, 5 }, nMin = 300, nMax = 460, forms = new[] { 3, 4, 2 }, events = new[] { 4, 3 } },
            new OrbitDef { name = "카이퍼 벨트", desc = "태양계 끝 · 고대 탐사선 · 혜성",   bi = 150, bo = 400, mult = 24, att = 0.40, spin = 0.6,  hp = 5.2, sell = 14, permit = 600000000, gap = 0.2, mix = new double[] { 30, 16, 10, 12, 5, 9, 6 }, nMin = 320, nMax = 480, forms = new[] { 4, 3, 2, 1 }, events = new[] { 3, 4 } },
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
            N("d_n", "drone", "드론 격납고", "첫 칸 = 드론 두 대가 알아서 줍는다 · 그 뒤 한 대씩 더", new string[0], 1, 60, 2.2, 8, 0, 2),
            N("d_spd", "drone", "드론 속도", "더 자주 줍는다", new[] { "d_n" }, 2, 300, 1.6, 6, 1, 1),
            N("d_reach", "drone", "드론 거리", "더 멀리서 집는다", new[] { "d_n" }, 3, 660, 1.6, 5, 1, 3),
            N("d_mag", "drone", "드론 수거", "드론 몫 +25%", new[] { "d_spd" }, 3, 900, 1.6, 5, 2, 0),
            N("d_sig", "drone", "신호 증폭", "신호기 효과가 길어진다", new[] { "d_spd", "d_reach" }, 3, 700, 1.7, 3, 2, 2),
            N("d_grade", "drone", "드론 등급", "더 단단한 것도 한 방에", new[] { "d_reach" }, 5, 4500, 2.0, 3, 2, 4),
            N("d_fix", "drone", "수리 드론", "출동 +2초", new[] { "d_mag" }, 4, 2000, 1.8, 3, 3, 1),
            N("d_pair", "drone", "편대", "한 번에 둘씩", new[] { "d_mag", "d_grade" }, 6, 18000, 1, 1, 3, 3),
            N("d_fact", "drone", "드론 공장", "드론 +1 (공장제)", new[] { "d_pair" }, 6, 30000, 2.5, 2, 4, 2),
            N("b_n", "bh", "블랙홀", "집게로 칠 때 블랙홀이 저절로 열린다 — 단계마다 더 자주", new string[0], 1, 400, 2.6, 4, 0, 1),
            N("c_find", "bh", "연료 보급", "연료를 올려 보낸다", new string[0], 3, 240, 1.8, 5, 0, 3),
            N("s_speed", "bh", "재충전", "블랙홀이 더 자주 열리고 보급도 빨리 닿는다", new[] { "b_n" }, 3, 500, 1.6, 4, 1, 2),
            N("b_pr", "bh", "흡입 반경", "더 넓게 빨아들인다", new[] { "b_n" }, 3, 600, 1.6, 6, 1, 0),
            N("b_cap", "bh", "붕괴 한계", "더 많이 모아도 버틴다", new[] { "b_n" }, 3, 660, 1.6, 6, 2, 1),
            N("b_pf", "bh", "흡입 세기", "더 빨리 빨려 든다", new[] { "b_pr" }, 4, 1800, 1.6, 5, 2, 0),
            N("b_br", "bh", "폭발 반경", "더 크게 터진다", new[] { "b_cap" }, 4, 1800, 1.6, 6, 3, 1),
            N("b_chain", "bh", "폭발 연쇄", "폭발형 무기(번개 각성 …)의 폭발이 또 터질 확률 · 연쇄 한계 +15", new[] { "b_br" }, 4, 2700, 1.6, 8, 3, 3),
            N("b_pack", "bh", "압축 배율", "많이 모을수록 값 +", new[] { "b_chain", "b_pf" }, 5, 7500, 1.7, 5, 4, 2),
            N("o_wide", "eco", "궤도 확장", "궤도가 넓어진다 — 쓰레기도 는다", new string[0], 1, 20, 1.9, 6, 0, 4),
            N("e_val", "eco", "고철 시세", "모든 값이 오른다", new string[0], 1, 120, 1.9, 10, 0, 2),
            N("e_vault", "eco", "금고 감별", "금고 위성이 더 자주", new[] { "e_val" }, 2, 360, 1.6, 6, 1, 0),
            N("e_att", "eco", "부착물 감별", "부착물이 더 자주", new[] { "e_val" }, 3, 1200, 1.7, 5, 1, 2),
            N("e_quest", "eco", "의뢰 게시판", "출동마다 의뢰 카드 — 해내면 판 수입이 오른다 · 단계마다 보상 +10%", new[] { "e_val" }, 1, 250, 2.4, 4, 1, 4),
            N("e_talk", "eco", "청구서 협상", "기한 +1판", new[] { "e_vault" }, 4, 3600, 3.0, 2, 2, 0),
            N("e_tip", "eco", "제보망", "블랙박스가 더 자주", new[] { "e_att" }, 3, 1500, 1.8, 3, 2, 2),
            N("e_save", "eco", "적금", "판 끝에 이자", new[] { "e_val" }, 4, 2500, 1.9, 5, 2, 4),
            N("e_guard", "eco", "상환 조절", "빚 상환으로 떼는 몫 30% → 20%", new[] { "e_talk", "e_tip" }, 5, 9000, 1, 1, 3, 1),
            N("e_used", "eco", "중고 거래", "모든 칸 -5%", new[] { "e_save" }, 4, 6000, 2.0, 3, 3, 3),
            // 📈 증권 줄기 (09-23 사장님 「진짜 주식처럼 · 정비소에서 이점을」) — 맨 끝에 붙여 옛 저장의 칸 순서를 안 흔든다 (경매 칸 자리를 그대로 썼다)
            N("a_open", "eco", "증권 계좌", "궤도 증권이 열린다 — 판 중에도 조종실에서도 사고판다", new[] { "e_val" }, 2, 500, 1, 1, 1, 1),
            N("a_auto", "eco", "개미의 기도", "내가 산 종목이 조금씩 더 오르는 쪽으로 움직인다", new[] { "a_open" }, 2, 800, 1, 1, 1, 1),
            N("a_read", "eco", "내부자 정보", "다음 속보를 미리 안다 — 시간 · 업종 · 제목", new[] { "a_auto" }, 2, 1500, 2.5, 3, 1, 1),
            N("a_ins", "eco", "행운의 부적", "내 종목에 걸릴 나쁜 속보를 피해 간다 (60 · 70 · 80%)", new[] { "a_read" }, 3, 4000, 2.5, 3, 1, 1),
            N("a_big", "eco", "큰손 계좌", "수수료가 줄고 배당이 붙는다", new[] { "a_ins" }, 4, 20000, 3, 3, 1, 1),
            // 🪐 항로 (09-24 사장님 「청구서로 늘리는 방식 말고 정비고에서 통일」) — 옛 허가증. 값은 허가증 값 그대로
            N("p_moon", "route", "달 항로", "달 궤도에 갈 수 있다 — 값 ×1.5 · 느린 궤도 · 금고 위성이 많다", new string[0], 1, 1500, 1, 1, 0, 0),
            N("p_mars", "route", "화성 항로", "화성 궤도 — 값 ×2.2 · 모래 폭풍 · 얼음 껍질 · 큰 잔해", new[] { "p_moon" }, 1, 20000, 1, 1, 0, 0),
            N("p_jup", "route", "목성 항로", "목성 궤도 — 값 ×4.5 · 중력이 안쪽으로 모은다 · 장갑판", new[] { "p_belt" }, 1, 1500000, 1, 1, 0, 0),
            N("p_sat", "route", "토성 항로", "토성 궤도 — 값 ×6.5 · 두 겹 고리 · 케이블 망", new[] { "p_jup" }, 1, 15000000, 1, 1, 0, 0),
            N("p_belt", "route", "소행성대 항로", "화성과 목성 사이 소행성대 — 값 ×3.2 · 단단한 암석 · 광석", new[] { "p_mars" }, 1, 150000, 1, 1, 0, 0),
            N("p_ura", "route", "천왕성 항로", "천왕성 궤도 — 값 ×10 · 옆으로 누운 궤도 · 얼음 결정", new[] { "p_sat" }, 1, 50000000, 1, 1, 0, 0),
            N("p_nep", "route", "해왕성 항로", "해왕성 궤도 — 값 ×15 · 초속 폭풍 · 무거운 잔해", new[] { "p_ura" }, 1, 200000000, 1, 1, 0, 0),
            N("p_kui", "route", "카이퍼 벨트 항로", "태양계 끝 카이퍼 벨트 — 값 ×24 · 고대 탐사선 · 혜성", new[] { "p_nep" }, 1, 600000000, 1, 1, 0, 0),
            // ✦ 네 번째 고리 — 곱하기 · 무기 3단계. 목성 항로를 사야 열린다 (09-24 레벨 설계 2막 「외행성 면허」)
            N("m_claw", "claw", "✦ 과충전 포신", "화력 ×1.5 (단계마다 곱한다) — 외행성 면허", new[] { "k_claw" }, 1, 10000000, 6, 3, 0, 0),
            N("m_crit", "claw", "✦ 정밀 조준", "치명타 배수 +1 — 외행성 면허", new[] { "k_claw" }, 1, 16000000, 6, 2, 0, 0),
            N("m_fuel", "claw", "✦ 연료 탱크 증설", "연료 +25% — 외행성 면허", new[] { "k_claw" }, 1, 8000000, 5, 2, 0, 0),
            N("m_drone", "drone", "✦ 드론 공장 확장", "드론 몫 ×1.5 (곱한다) — 외행성 면허", new[] { "k_drone" }, 1, 10000000, 6, 3, 0, 0),
            N("m_dcount", "drone", "✦ 편대", "드론 +2 — 외행성 면허", new[] { "k_drone" }, 1, 12000000, 6, 2, 0, 0),
            N("m_bh", "bh", "✦ 사건의 지평선", "블랙홀 발동 +30% · 범위 +15% — 외행성 면허", new[] { "k_bh" }, 1, 10000000, 6, 3, 0, 0),
            N("m_val", "eco", "✦ 시세 조작", "모든 값 ×1.5 (곱한다) — 외행성 면허", new[] { "k_eco" }, 1, 12000000, 6, 3, 0, 0),
            N("m_route", "route", "✦ 심우주 항법", "행성 값 배수 ×1.25 (곱한다) — 외행성 면허", new[] { "k_route" }, 1, 16000000, 6, 3, 0, 0),
            N("w_laser_x", "arm", "✦ 레이저 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_laser_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_chain_x", "arm", "✦ 번개 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_chain_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_vac_x", "arm", "✦ 진공 청소기 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_vac_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_mine_x", "arm", "✦ 기뢰 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_mine_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_frz_x", "arm", "✦ 냉동 빔 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_frz_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_clus_x", "arm", "✦ 분열탄 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_clus_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_mag_x", "arm", "✦ 자석 펄스 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_mag_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_rail_x", "arm", "✦ 레일건 3단계", "발동 확률 ×2 · 위력 ×2 — 외행성 면허", new[] { "w_rail_u" }, 1, 8000000, 1, 1, 0, 0),
            // ⚔ 무기고 (09-24 설계서 2단계) — 지금 빔 칸(화력 · 크기 · 연사 · 치명 · 두 번)은 모든 무기 공통
            N("w_hub", "arm", "무기고", "산 무기는 기본 공격이 나갈 때 확률로 함께 터진다 — 많이 살수록 한 번에 여러 개", new string[0], 1, 300, 1, 1, 0, 0),
            N("w_laser", "arm", "레이저", "공격 때 12% — 조준점을 0.7초 동안 태운다", new[] { "w_hub" }, 1, 2500, 1, 1, 0, 0),
            N("w_laser_u", "arm", "레이저 강화", "1단계 확률 ×1.5 · 태우는 원 +40% · 2단계 계속 쬘수록 뜨거워진다 (최대 +80%)", new[] { "w_laser" }, 1, 6000, 4, 2, 0, 0),
            N("w_laser_a", "arm", "레이저 각성", "태우는 원 +50% · 장갑판도 녹인다", new[] { "w_laser_u" }, 1, 60000, 1, 1, 0, 0),
            N("w_chain", "arm", "번개", "공격 때 10% — 조준점에서 옆으로 다섯 번 튄다", new[] { "w_laser" }, 1, 5000, 1, 1, 0, 0),
            N("w_chain_u", "arm", "번개 강화", "1단계 확률 ×1.5 · 두 번 더 튄다 · 2단계 튈수록 세진다", new[] { "w_chain" }, 1, 9000, 4, 2, 0, 0),
            N("w_chain_a", "arm", "번개 각성", "튄 자리마다 작은 폭발 — 연쇄로 이어진다", new[] { "w_chain_u" }, 1, 90000, 1, 1, 0, 0),
            // 🔩 부품 거래 · ◆ 핵심 칸 (09-24 설계서 3단계)
            N("e_shop", "eco", "부품 거래", "부품 가게가 열린다 — 청소선 부품 칸 다섯에 사서 끼운다 · 진열은 출동마다 바뀐다", new[] { "e_save" }, 1, 1500, 1, 1, 0, 0),
            N("k_claw", "claw", "◆ 과열 사격", "모든 무기 화력 +40% — 대신 연료 −15%", new[] { "c_over" }, 1, 30000, 1, 1, 0, 0),
            N("k_drone", "drone", "◆ 벌떼", "드론 +4대 — 대신 드론 몫 −25%", new[] { "d_fact" }, 1, 60000, 1, 1, 0, 0),
            N("k_bh", "bh", "◆ 쌍둥이 블랙홀", "블랙홀이 터지면 그 자리에 한 번 더 열린다 — 대신 여는 확률 −30%", new[] { "s_speed" }, 1, 40000, 1, 1, 0, 0),
            N("k_eco", "eco", "◆ 큰손", "모든 값 +25% — 대신 청구서 +10%", new[] { "e_used" }, 1, 80000, 1, 1, 0, 0),
            N("k_route", "route", "◆ 궤도 공명", "행성 값 배수 +0.5 — 대신 잔해 체력 +20%", new[] { "p_sat" }, 1, 150000, 1, 1, 0, 0),
            N("w_slot2", "arm", "◆ 무기 공명", "모든 무기의 발동 확률 ×1.5", new[] { "w_chain" }, 1, 50000, 1, 1, 0, 0),
            // ★ 신기한 칸 (09-24 설계서 4단계) — 판 밖(주식 · 뉴스 · 행성)과 판을 잇는다
            N("q_insider", "eco", "★ 내부자 거래", "공격이 맞을 때 가끔(0.5%) 내가 산 종목 하나가 +1% — 「누군가 청소선을 보고 샀다」", new[] { "a_read" }, 1, 30000, 1, 1, 0, 0),
            N("q_rage", "eco", "★ 물린 개미의 분노", "내 주식이 손해일수록 화력이 오른다 (손해 % 만큼 · 최대 +50%)", new[] { "a_big" }, 1, 120000, 1, 1, 0, 0),
            N("q_front", "eco", "★ 1면 조작", "출동이 끝나면 궤도일보 1면을 둘 중에서 고른다 — 고른 기사가 주가를 움직인다", new[] { "e_tip" }, 1, 50000, 1, 1, 0, 0),
            N("q_debt", "eco", "★ 빚쟁이의 근성", "빚이 많을수록 화력이 오른다 (최대 +15%)", new[] { "e_guard" }, 1, 60000, 1, 1, 0, 0),
            N("q_meteor", "bh", "★ 운석 호출", "잔해를 80개 부술 때마다 운석이 떨어져 크게 터진다 — 궤도일보 1면 · 연료공사 주가 ↓", new[] { "b_chain" }, 1, 150000, 1, 1, 0, 0),
            N("q_sling", "bh", "★ 중력 새총", "블랙홀이 터질 때 빨아들인 잔해를 사방으로 쏘아 보낸다", new[] { "b_pack" }, 1, 250000, 1, 1, 0, 0),
            N("q_tour", "route", "★ 관광 명소", "한 판에 연쇄 100을 넘기면 관광객이 몰린다 — 토성 고리 관광 주가 ↑", new[] { "p_sat" }, 1, 3000000, 1, 1, 0, 0),
            N("q_rock", "route", "★ 떠돌이 소행성", "가끔 소행성이 궤도에 끼어든다 — 부수면 열쇠(40%) 또는 돈 뭉치", new[] { "p_jup" }, 1, 500000, 1, 1, 0, 0),
            N("q_gold", "hull", "★ 황금 잔해", "가끔 금빛 잔해가 섞인다 — 값 ×3 · 부수면 즉석 복권 (한 판 2장까지)", new[] { "o_wide" }, 1, 40000, 1, 1, 0, 0),
            N("q_lazy", "drone", "★ 게으름 보너스", "AUTO로 30초 넘게 손을 안 대면 그 판 드론이 한 대 더 나온다", new[] { "d_fix" }, 1, 80000, 1, 1, 0, 0),
            // ⚔ 무기 여섯 더 (09-24 설계서 4단계)
            N("w_vac", "arm", "진공 청소기", "공격 때 8% — 조준점에 소용돌이, 0.7초 동안 빨아들인다 · 삼킨 것은 값 +30%", new[] { "w_chain" }, 1, 12000, 1, 1, 0, 0),
            N("w_vac_u", "arm", "진공 청소기 강화", "1단계 확률 ×1.5 · 소용돌이 +30% · 2단계 삼킨 것 값 +60%", new[] { "w_vac" }, 1, 24000, 4, 2, 0, 0),
            N("w_vac_a", "arm", "진공 청소기 각성", "가득 차면(25개) 압축 고철탄을 조준점에 쏜다", new[] { "w_vac_u" }, 1, 120000, 1, 1, 0, 0),
            N("w_mine", "arm", "기뢰", "공격 때 8% — 조준 자리에 기뢰를 깐다 · 잔해가 지나가면 쾅 (3개까지)", new[] { "w_vac" }, 1, 30000, 1, 1, 0, 0),
            N("w_mine_u", "arm", "기뢰 강화", "1단계 확률 ×1.5 · 기뢰 +2 · 2단계 폭발 반경 +40%", new[] { "w_mine" }, 1, 60000, 4, 2, 0, 0),
            N("w_mine_a", "arm", "기뢰 각성", "기뢰끼리 레이저 울타리로 이어진다", new[] { "w_mine_u" }, 1, 300000, 1, 1, 0, 0),
            N("w_frz", "arm", "냉동 빔", "공격 때 8% — 조준점에 서리 원, 0.7초 동안 얼린다 · 언 것은 두 배 · 부서지면 산산조각", new[] { "w_mine" }, 1, 80000, 1, 1, 0, 0),
            N("w_frz_u", "arm", "냉동 빔 강화", "1단계 확률 ×1.5 · 더 오래 언다 · 2단계 언 것 ×2.5", new[] { "w_frz" }, 1, 160000, 4, 2, 0, 0),
            N("w_frz_a", "arm", "냉동 빔 각성", "산산조각 파편도 옆을 얼린다 (끝없는 연쇄)", new[] { "w_frz_u" }, 1, 800000, 1, 1, 0, 0),
            N("w_clus", "arm", "분열탄", "공격 때 6% — 조준점에 떨어져 파편 여섯으로 흩어진다", new[] { "w_frz" }, 1, 250000, 1, 1, 0, 0),
            N("w_clus_u", "arm", "분열탄 강화", "1단계 확률 ×1.5 · 파편 +3 · 2단계 파편이 가까운 잔해를 노린다", new[] { "w_clus" }, 1, 500000, 4, 2, 0, 0),
            N("w_clus_a", "arm", "분열탄 각성", "파편이 한 번 더 셋으로 갈라진다", new[] { "w_clus_u" }, 1, 2500000, 1, 1, 0, 0),
            N("w_mag", "arm", "자석 펄스", "공격 때 5% — 주변 잔해를 한 점으로 끌어모은 뒤 쾅", new[] { "w_clus" }, 1, 800000, 1, 1, 0, 0),
            N("w_mag_u", "arm", "자석 펄스 강화", "1단계 확률 ×1.5 · 끌림 반경 +40% · 2단계 모인 만큼 크게 터진다", new[] { "w_mag" }, 1, 1600000, 4, 2, 0, 0),
            N("w_mag_a", "arm", "자석 펄스 각성", "모인 자리에 블랙홀이 열린다", new[] { "w_mag_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_rail", "arm", "레일건", "공격 때 4% — 한 줄로 관통, 엄청 세고 장갑도 뚫는다", new[] { "w_mag" }, 1, 3000000, 1, 1, 0, 0),
            N("w_rail_u", "arm", "레일건 강화", "1단계 확률 ×1.5 · 2단계 뚫을수록 +15%", new[] { "w_rail" }, 1, 6000000, 4, 2, 0, 0),
            // ◇ 무기 특화 — 단계마다 효과가 커진다 (09-24 사장님 38번 「강화가 너무 적다 · 효과가 추가」)
            N("w_laser_e", "arm", "레이저 특화", "태우는 점 +20% · 위력 +15% — 3단계: 태운 자리가 가끔 터진다", new[] { "w_laser_u" }, 1, 18000, 4, 3, 0, 0),
            N("w_chain_e", "arm", "번개 특화", "튀는 수 +2", new[] { "w_chain_u" }, 1, 27000, 4, 3, 0, 0),
            N("w_vac_e", "arm", "진공 청소기 특화", "흡입 원 +20% · 삼킨 것 값 +20%", new[] { "w_vac_u" }, 1, 72000, 4, 3, 0, 0),
            N("w_mine_e", "arm", "기뢰 특화", "기뢰 +1개 · 폭발 +15%", new[] { "w_mine_u" }, 1, 180000, 4, 3, 0, 0),
            N("w_frz_e", "arm", "냉동 빔 특화", "서리 원 +20% · 어는 시간 +0.5초", new[] { "w_frz_u" }, 1, 480000, 4, 3, 0, 0),
            N("w_clus_e", "arm", "분열탄 특화", "파편 +2", new[] { "w_clus_u" }, 1, 1500000, 4, 3, 0, 0),
            N("w_mag_e", "arm", "자석 펄스 특화", "끄는 범위 +20%", new[] { "w_mag_u" }, 1, 4800000, 4, 3, 0, 0),
            N("w_rail_e", "arm", "레일건 특화", "사거리 +120 — 3단계: 한 줄 더", new[] { "w_rail_u" }, 1, 18000000, 4, 3, 0, 0),
            // 🎟 복권 — 스킬로 (09-24 사장님 33번)
            N("l_more", "eco", "복권 단골", "판마다 즉석 복권 +1장", new[] { "e_val" }, 1, 300, 3, 3, 0, 0),
            N("l_luck", "eco", "행운의 긁개", "즉석 복권 당첨 확률 +25%", new[] { "l_more" }, 1, 2000, 3, 3, 0, 0),
            N("l_free", "eco", "첫 장은 공짜", "판마다 즉석 복권 첫 장이 공짜", new[] { "l_more" }, 1, 1500, 1, 1, 0, 0),
            N("l_jack", "eco", "잭팟", "즉석 복권 당첨금 ×2", new[] { "l_luck" }, 1, 30000, 1, 1, 0, 0),
            N("w_rail_a", "arm", "레일건 각성", "띠 끝에서 튕겨 한 번 더 쏜다", new[] { "w_rail_u" }, 1, 30000000, 1, 1, 0, 0),
            // ◆ 교차 핵심 (두 방향을 다 키워야 닿는다 · 열쇠) · ∞ 무한 칸 (3막의 돈이 계속 쓸 곳)
            N("x_claw_arm", "claw", "◆ 교차: 사격 통제", "모든 무기 치명 +10% · 치명타는 ×4 (청소선 × 무기고)", new[] { "c_magnet", "w_hub" }, 1, 400000, 1, 1, 0, 0),
            N("x_arm_drone", "drone", "◆ 교차: 드론 사수", "드론 공격에서도 블랙홀 · 내부자 거래가 굴러간다 · 드론 피해 ×2 (무기고 × 드론)", new[] { "d_grade", "w_hub" }, 1, 600000, 1, 1, 0, 0),
            N("x_drone_bh", "drone", "◆ 교차: 블랙홀 견인", "블랙홀이 열려 있는 동안 드론이 두 배 빠르다 (드론 × 블랙홀)", new[] { "d_fix", "b_n" }, 1, 500000, 1, 1, 0, 0),
            N("x_bh_eco", "bh", "◆ 교차: 파산 보험", "파산하면 돈의 10% 와 제일 좋은 부품 하나를 다음 대로 가져간다 (블랙홀 × 경영)", new[] { "k_bh", "e_save" }, 1, 800000, 1, 1, 0, 0),
            N("x_eco_route", "eco", "◆ 교차: 행성 투자", "지금 궤도 행성의 종목(달 · 화성 · 목성 · 토성)을 들고 있으면 그 판 값 +20% (경영 × 항로)", new[] { "e_tip", "p_mars" }, 1, 700000, 1, 1, 0, 0),
            N("x_route_claw", "claw", "◆ 교차: 궤도 폭격", "행성이 멀수록 화력이 오른다 (달 +5% · 화성 +10% · 목성 +20% · 토성 +35%) (항로 × 청소선)", new[] { "c_double", "p_moon" }, 1, 900000, 1, 1, 0, 0),
            N("i_claw", "claw", "∞ 무한 화력", "살 때마다 모든 무기 화력 +5% — 끝이 없다", new[] { "k_claw" }, 1, 2000000, 1.35, 999, 0, 0),
            N("i_drone", "drone", "∞ 무한 드론", "살 때마다 드론 몫 +5%", new[] { "k_drone" }, 1, 2000000, 1.35, 999, 0, 0),
            N("i_bh", "bh", "∞ 무한 블랙홀", "살 때마다 블랙홀 확률 +0.1%p", new[] { "k_bh" }, 1, 2000000, 1.35, 999, 0, 0),
            N("i_eco", "eco", "∞ 무한 시세", "살 때마다 모든 값 +4%", new[] { "k_eco" }, 1, 2000000, 1.35, 999, 0, 0),
            N("i_route", "route", "∞ 무한 궤도", "살 때마다 행성 값 배수 +0.05", new[] { "k_route" }, 1, 2000000, 1.35, 999, 0, 0),
        };
        public const int NodeCount = 130;
        /// <summary>◆ 핵심 칸 — 돈 + 열쇠 하나 (부품 가게에서 산다). 각성도 여기</summary>
        public static readonly HashSet<string> KeyNodes = new HashSet<string> { "w_laser_a", "w_chain_a", "k_claw", "k_drone", "k_bh", "k_eco", "k_route", "w_slot2", "w_vac_a", "w_mine_a", "w_frz_a", "w_clus_a", "w_mag_a", "w_rail_a", "x_claw_arm", "x_arm_drone", "x_drone_bh", "x_bh_eco", "x_eco_route", "x_route_claw" };
        public static readonly string[] WeaponName = { "집게 빔", "레이저", "번개", "청소기", "기뢰", "냉동 빔", "분열탄", "자석", "레일건" };
        public static readonly string[] WeaponNode = { null, "w_laser", "w_chain", "w_vac", "w_mine", "w_frz", "w_clus", "w_mag", "w_rail" };
        public static readonly string[] PlanetNode = { null, "p_moon", "p_mars", "p_jup", "p_sat", "p_belt", "p_ura", "p_nep", "p_kui" };
        public static readonly Dictionary<string, string> IconAlias = new Dictionary<string, string> { { "l_more", "e_val" }, { "l_luck", "e_val" }, { "l_free", "e_val" }, { "l_jack", "e_val" }, { "w_laser_e", "w_laser_u" }, { "w_chain_e", "w_chain_u" }, { "w_vac_e", "w_vac_u" }, { "w_mine_e", "w_mine_u" }, { "w_frz_e", "w_frz_u" }, { "w_clus_e", "w_clus_u" }, { "w_mag_e", "w_mag_u" }, { "w_rail_e", "w_rail_u" }, { "m_claw", "c_pow" }, { "m_crit", "c_crit" }, { "m_fuel", "c_fuel" }, { "m_drone", "d_fact" }, { "m_dcount", "d_n" }, { "m_bh", "b_n" }, { "m_val", "e_val" }, { "m_route", "o_wide" }, { "w_laser_x", "w_laser_u" }, { "w_chain_x", "w_chain_u" }, { "w_vac_x", "w_vac_u" }, { "w_mine_x", "w_mine_u" }, { "w_frz_x", "w_frz_u" }, { "w_clus_x", "w_clus_u" }, { "w_mag_x", "w_mag_u" }, { "w_rail_x", "w_rail_u" } };
        public static bool Ring4(string id) => id.StartsWith("m_") || (id.StartsWith("w_") && id.EndsWith("_x"));
        public static readonly int[] OrbitOrder = { 0, 1, 2, 5, 3, 4, 6, 7, 8 };          // 가까운 → 먼 (소행성대는 번호 5지만 화성과 목성 사이)
        // ───────────────────────── 정비소 트리 자리 (손으로 격자에 놓았다 · 시안 https://claude.ai/artifact/NLZseBQWXKMGfuAmFDFJcR)
        //    par = 이어지는 앞 칸의 능력 (R = 가운데 청소선) · tile = 그 능력의 몇 번째 칸 뒤 · (x, y) 첫 칸 자리 · (dx, dy) 뻗는 방향
        //    🔴 여는 조건도 이것 — 앞 칸을 사야 이 능력의 첫 칸이 열린다 (게임 · 봇 같은 규칙)
        public struct TreeSpot { public string par; public int tile, x, y, dx, dy; }
        public static readonly Dictionary<string, TreeSpot> Layout = new Dictionary<string, TreeSpot>
        {
            { "c_pow", new TreeSpot { par = "R", tile = 0, x = 0, y = -1, dx = 0, dy = -1 } },
            { "c_rad", new TreeSpot { par = "c_pow", tile = 2, x = 1, y = -3, dx = 0, dy = -1 } },
            { "c_spd", new TreeSpot { par = "c_pow", tile = 2, x = -1, y = -3, dx = 0, dy = -1 } },
            { "c_fuel", new TreeSpot { par = "R", tile = 0, x = -1, y = 0, dx = -1, dy = 0 } },
            { "c_crit", new TreeSpot { par = "c_spd", tile = 2, x = -2, y = -5, dx = 0, dy = -1 } },
            { "c_double", new TreeSpot { par = "c_spd", tile = 4, x = -3, y = -7, dx = 0, dy = -1 } },
            { "c_magnet", new TreeSpot { par = "c_rad", tile = 3, x = 2, y = -6, dx = 0, dy = -1 } },
            { "c_over", new TreeSpot { par = "c_crit", tile = 5, x = -2, y = -10, dx = 0, dy = 0 } },
            { "d_n", new TreeSpot { par = "R", tile = 0, x = 1, y = 0, dx = 1, dy = 0 } },
            { "d_spd", new TreeSpot { par = "d_n", tile = 2, x = 3, y = 1, dx = 1, dy = 0 } },
            { "d_reach", new TreeSpot { par = "d_n", tile = 4, x = 5, y = -1, dx = 1, dy = 0 } },
            { "d_mag", new TreeSpot { par = "d_spd", tile = 3, x = 5, y = 2, dx = 1, dy = 0 } },
            { "d_sig", new TreeSpot { par = "d_spd", tile = 5, x = 8, y = 1, dx = 1, dy = 0 } },
            { "d_grade", new TreeSpot { par = "d_reach", tile = 3, x = 10, y = -1, dx = 1, dy = 0 } },
            { "d_fix", new TreeSpot { par = "d_mag", tile = 2, x = 6, y = 3, dx = 1, dy = 0 } },
            { "d_pair", new TreeSpot { par = "d_sig", tile = 3, x = 11, y = 1, dx = 0, dy = 0 } },
            { "d_fact", new TreeSpot { par = "d_pair", tile = 1, x = 12, y = 1, dx = 1, dy = 0 } },
            { "b_n", new TreeSpot { par = "R", tile = 0, x = 0, y = 1, dx = 0, dy = 1 } },
            { "c_find", new TreeSpot { par = "c_fuel", tile = 5, x = -6, y = 0, dx = -1, dy = 0 } },
            { "s_speed", new TreeSpot { par = "b_n", tile = 4, x = 0, y = 6, dx = 0, dy = 1 } },
            { "b_pr", new TreeSpot { par = "b_n", tile = 2, x = 1, y = 4, dx = 1, dy = 0 } },
            { "b_cap", new TreeSpot { par = "b_n", tile = 4, x = 1, y = 5, dx = 1, dy = 0 } },
            { "b_pf", new TreeSpot { par = "b_pr", tile = 5, x = 6, y = 5, dx = 1, dy = 0 } },
            { "b_br", new TreeSpot { par = "b_cap", tile = 3, x = 3, y = 6, dx = 0, dy = 1 } },
            { "b_chain", new TreeSpot { par = "b_br", tile = 2, x = 4, y = 7, dx = 1, dy = 0 } },
            { "b_pack", new TreeSpot { par = "b_chain", tile = 5, x = 9, y = 8, dx = 0, dy = 1 } },
            { "o_wide", new TreeSpot { par = "c_fuel", tile = 1, x = -2, y = -1, dx = -1, dy = 0 } },
            { "e_val", new TreeSpot { par = "R", tile = 0, x = -1, y = 1, dx = 0, dy = 1 } },
            { "e_vault", new TreeSpot { par = "e_val", tile = 4, x = -2, y = 4, dx = -1, dy = 0 } },
            { "e_att", new TreeSpot { par = "e_val", tile = 5, x = -2, y = 5, dx = -1, dy = 0 } },
            { "e_quest", new TreeSpot { par = "e_val", tile = 2, x = -2, y = 2, dx = -1, dy = 0 } },
            { "e_talk", new TreeSpot { par = "e_vault", tile = 5, x = -7, y = 4, dx = -1, dy = 0 } },
            { "e_tip", new TreeSpot { par = "e_att", tile = 5, x = -7, y = 5, dx = -1, dy = 0 } },
            { "e_save", new TreeSpot { par = "e_val", tile = 5, x = -1, y = 6, dx = 0, dy = 1 } },
            { "e_guard", new TreeSpot { par = "e_talk", tile = 2, x = -9, y = 4, dx = 0, dy = 0 } },
            { "e_used", new TreeSpot { par = "e_save", tile = 3, x = -2, y = 8, dx = -1, dy = 0 } },
            { "a_open", new TreeSpot { par = "e_val", tile = 3, x = -2, y = 3, dx = 0, dy = 0 } },
            { "a_auto", new TreeSpot { par = "a_open", tile = 1, x = -3, y = 3, dx = 0, dy = 0 } },
            { "a_read", new TreeSpot { par = "a_auto", tile = 1, x = -4, y = 3, dx = -1, dy = 0 } },
            { "a_ins", new TreeSpot { par = "a_read", tile = 3, x = -7, y = 3, dx = -1, dy = 0 } },
            { "a_big", new TreeSpot { par = "a_ins", tile = 3, x = -10, y = 3, dx = -1, dy = 0 } },
            { "p_moon", new TreeSpot { par = "R", tile = 1, x = -2, y = -2, dx = 0, dy = 0 } },
            { "p_mars", new TreeSpot { par = "p_moon", tile = 1, x = -4, y = -3, dx = 0, dy = 0 } },
            { "p_jup", new TreeSpot { par = "p_mars", tile = 1, x = -6, y = -4, dx = 0, dy = 0 } },
            { "p_sat", new TreeSpot { par = "p_jup", tile = 1, x = -8, y = -5, dx = 0, dy = 0 } },
            { "p_belt", new TreeSpot { par = "p_mars", tile = 1, x = -5, y = -5, dx = 0, dy = 0 } },
            { "p_ura", new TreeSpot { par = "p_sat", tile = 1, x = -10, y = -7, dx = 0, dy = 0 } },
            { "p_nep", new TreeSpot { par = "p_ura", tile = 1, x = -12, y = -8, dx = 0, dy = 0 } },
            { "p_kui", new TreeSpot { par = "p_nep", tile = 1, x = -14, y = -9, dx = 0, dy = 0 } },
            { "m_claw", new TreeSpot { par = "k_claw", tile = 1, x = -3, y = -11, dx = 0, dy = 0 } },
            { "m_crit", new TreeSpot { par = "k_claw", tile = 1, x = -3, y = -12, dx = 0, dy = 0 } },
            { "m_fuel", new TreeSpot { par = "k_claw", tile = 1, x = -1, y = -11, dx = 0, dy = 0 } },
            { "m_drone", new TreeSpot { par = "k_drone", tile = 1, x = 14, y = 2, dx = 0, dy = 0 } },
            { "m_dcount", new TreeSpot { par = "k_drone", tile = 1, x = 14, y = 0, dx = 0, dy = 0 } },
            { "m_bh", new TreeSpot { par = "k_bh", tile = 1, x = -1, y = 10, dx = 0, dy = 0 } },
            { "m_val", new TreeSpot { par = "k_eco", tile = 1, x = -5, y = 9, dx = 0, dy = 0 } },
            { "m_route", new TreeSpot { par = "k_route", tile = 1, x = -9, y = -4, dx = 0, dy = 0 } },
            { "w_laser_x", new TreeSpot { par = "w_laser_u", tile = 1, x = 6, y = -2, dx = 0, dy = 0 } },
            { "w_chain_x", new TreeSpot { par = "w_chain_u", tile = 1, x = 6, y = -3, dx = 0, dy = 0 } },
            { "w_vac_x", new TreeSpot { par = "w_vac_u", tile = 1, x = 6, y = -4, dx = 0, dy = 0 } },
            { "w_mine_x", new TreeSpot { par = "w_mine_u", tile = 1, x = 6, y = -5, dx = 0, dy = 0 } },
            { "w_frz_x", new TreeSpot { par = "w_frz_u", tile = 1, x = 6, y = -6, dx = 0, dy = 0 } },
            { "w_clus_x", new TreeSpot { par = "w_clus_u", tile = 1, x = 6, y = -7, dx = 0, dy = 0 } },
            { "w_mag_x", new TreeSpot { par = "w_mag_u", tile = 1, x = 6, y = -8, dx = 0, dy = 0 } },
            { "w_rail_x", new TreeSpot { par = "w_rail_u", tile = 1, x = 5, y = -10, dx = 0, dy = 0 } },
            { "w_hub", new TreeSpot { par = "R", tile = 0, x = 1, y = -1, dx = 0, dy = 0 } },
            { "w_laser", new TreeSpot { par = "w_hub", tile = 1, x = 4, y = -2, dx = 0, dy = 0 } },
            { "w_laser_u", new TreeSpot { par = "w_laser", tile = 1, x = 5, y = -2, dx = 1, dy = 0 } },
            { "w_laser_a", new TreeSpot { par = "w_laser_u", tile = 2, x = 7, y = -2, dx = 0, dy = 0 } },
            { "w_chain", new TreeSpot { par = "w_laser", tile = 1, x = 4, y = -3, dx = 0, dy = 0 } },
            { "w_chain_u", new TreeSpot { par = "w_chain", tile = 1, x = 5, y = -3, dx = 1, dy = 0 } },
            { "w_chain_a", new TreeSpot { par = "w_chain_u", tile = 2, x = 7, y = -3, dx = 0, dy = 0 } },
            { "e_shop", new TreeSpot { par = "e_save", tile = 2, x = -2, y = 7, dx = 0, dy = 0 } },
            { "k_claw", new TreeSpot { par = "c_over", tile = 1, x = -2, y = -11, dx = 0, dy = 0 } },
            { "k_drone", new TreeSpot { par = "d_fact", tile = 2, x = 14, y = 1, dx = 0, dy = 0 } },
            { "k_bh", new TreeSpot { par = "s_speed", tile = 4, x = 0, y = 10, dx = 0, dy = 0 } },
            { "k_eco", new TreeSpot { par = "e_used", tile = 3, x = -5, y = 8, dx = 0, dy = 0 } },
            { "k_route", new TreeSpot { par = "p_sat", tile = 1, x = -9, y = -3, dx = 0, dy = 0 } },
            { "w_slot2", new TreeSpot { par = "w_chain", tile = 1, x = 3, y = -4, dx = 0, dy = 0 } },
            { "q_insider", new TreeSpot { par = "a_read", tile = 3, x = -7, y = 2, dx = 0, dy = 0 } },
            { "q_rage", new TreeSpot { par = "a_big", tile = 3, x = -13, y = 3, dx = 0, dy = 0 } },
            { "q_front", new TreeSpot { par = "e_tip", tile = 3, x = -10, y = 5, dx = 0, dy = 0 } },
            { "q_debt", new TreeSpot { par = "e_guard", tile = 1, x = -10, y = 4, dx = 0, dy = 0 } },
            { "q_meteor", new TreeSpot { par = "b_chain", tile = 5, x = 9, y = 6, dx = 0, dy = 0 } },
            { "q_sling", new TreeSpot { par = "b_pack", tile = 5, x = 9, y = 13, dx = 0, dy = 0 } },
            { "q_tour", new TreeSpot { par = "p_sat", tile = 1, x = -10, y = -6, dx = 0, dy = 0 } },
            { "q_rock", new TreeSpot { par = "p_jup", tile = 1, x = -6, y = -6, dx = 0, dy = 0 } },
            { "q_gold", new TreeSpot { par = "o_wide", tile = 5, x = -7, y = -1, dx = 0, dy = 0 } },
            { "q_lazy", new TreeSpot { par = "d_fix", tile = 3, x = 9, y = 3, dx = 0, dy = 0 } },
            { "w_vac", new TreeSpot { par = "w_chain", tile = 1, x = 4, y = -4, dx = 0, dy = 0 } },
            { "w_vac_u", new TreeSpot { par = "w_vac", tile = 1, x = 5, y = -4, dx = 1, dy = 0 } },
            { "w_vac_a", new TreeSpot { par = "w_vac_u", tile = 2, x = 7, y = -4, dx = 0, dy = 0 } },
            { "w_mine", new TreeSpot { par = "w_vac", tile = 1, x = 4, y = -5, dx = 0, dy = 0 } },
            { "w_mine_u", new TreeSpot { par = "w_mine", tile = 1, x = 5, y = -5, dx = 1, dy = 0 } },
            { "w_mine_a", new TreeSpot { par = "w_mine_u", tile = 2, x = 7, y = -5, dx = 0, dy = 0 } },
            { "w_frz", new TreeSpot { par = "w_mine", tile = 1, x = 4, y = -6, dx = 0, dy = 0 } },
            { "w_frz_u", new TreeSpot { par = "w_frz", tile = 1, x = 5, y = -6, dx = 1, dy = 0 } },
            { "w_frz_a", new TreeSpot { par = "w_frz_u", tile = 2, x = 7, y = -6, dx = 0, dy = 0 } },
            { "w_clus", new TreeSpot { par = "w_frz", tile = 1, x = 4, y = -7, dx = 0, dy = 0 } },
            { "w_clus_u", new TreeSpot { par = "w_clus", tile = 1, x = 5, y = -7, dx = 1, dy = 0 } },
            { "w_clus_a", new TreeSpot { par = "w_clus_u", tile = 2, x = 7, y = -7, dx = 0, dy = 0 } },
            { "w_mag", new TreeSpot { par = "w_clus", tile = 1, x = 4, y = -8, dx = 0, dy = 0 } },
            { "w_mag_u", new TreeSpot { par = "w_mag", tile = 1, x = 5, y = -8, dx = 1, dy = 0 } },
            { "w_mag_a", new TreeSpot { par = "w_mag_u", tile = 2, x = 7, y = -8, dx = 0, dy = 0 } },
            { "w_rail", new TreeSpot { par = "w_mag", tile = 1, x = 4, y = -9, dx = 0, dy = 0 } },
            { "w_rail_u", new TreeSpot { par = "w_rail", tile = 1, x = 5, y = -9, dx = 1, dy = 0 } },
            { "w_laser_e", new TreeSpot { par = "w_laser_u", tile = 1, x = 6, y = -1, dx = 0, dy = 0 } },
            { "w_chain_e", new TreeSpot { par = "w_chain_u", tile = 1, x = 7, y = -1, dx = 0, dy = 0 } },
            { "w_vac_e", new TreeSpot { par = "w_vac_u", tile = 1, x = 3, y = -5, dx = 0, dy = 0 } },
            { "w_mine_e", new TreeSpot { par = "w_mine_u", tile = 1, x = 3, y = -6, dx = 0, dy = 0 } },
            { "w_frz_e", new TreeSpot { par = "w_frz_u", tile = 1, x = 3, y = -7, dx = 0, dy = 0 } },
            { "w_clus_e", new TreeSpot { par = "w_clus_u", tile = 1, x = 6, y = -9, dx = 0, dy = 0 } },
            { "w_mag_e", new TreeSpot { par = "w_mag_u", tile = 1, x = 6, y = -10, dx = 0, dy = 0 } },
            { "w_rail_e", new TreeSpot { par = "w_rail_u", tile = 1, x = 4, y = -10, dx = 0, dy = 0 } },
            { "l_more", new TreeSpot { par = "e_val", tile = 1, x = -2, y = 1, dx = 0, dy = 0 } },
            { "l_luck", new TreeSpot { par = "l_more", tile = 1, x = -3, y = 1, dx = 0, dy = 0 } },
            { "l_free", new TreeSpot { par = "l_more", tile = 1, x = -3, y = 2, dx = 0, dy = 0 } },
            { "l_jack", new TreeSpot { par = "l_luck", tile = 1, x = -4, y = 1, dx = 0, dy = 0 } },
            { "w_rail_a", new TreeSpot { par = "w_rail_u", tile = 2, x = 7, y = -9, dx = 0, dy = 0 } },
            { "x_claw_arm", new TreeSpot { par = "c_magnet", tile = 3, x = 2, y = -10, dx = 0, dy = 0 } },
            { "x_arm_drone", new TreeSpot { par = "d_grade", tile = 3, x = 13, y = -2, dx = 0, dy = 0 } },
            { "x_drone_bh", new TreeSpot { par = "d_fix", tile = 3, x = 10, y = 4, dx = 0, dy = 0 } },
            { "x_bh_eco", new TreeSpot { par = "k_bh", tile = 1, x = -1, y = 12, dx = 0, dy = 0 } },
            { "x_eco_route", new TreeSpot { par = "e_tip", tile = 3, x = -10, y = 6, dx = 0, dy = 0 } },
            { "x_route_claw", new TreeSpot { par = "c_double", tile = 5, x = -4, y = -11, dx = 0, dy = 0 } },
            { "i_claw", new TreeSpot { par = "k_claw", tile = 1, x = -2, y = -12, dx = 0, dy = 0 } },
            { "i_drone", new TreeSpot { par = "k_drone", tile = 1, x = 15, y = 1, dx = 0, dy = 0 } },
            { "i_bh", new TreeSpot { par = "k_bh", tile = 1, x = 0, y = 11, dx = 0, dy = 0 } },
            { "i_eco", new TreeSpot { par = "k_eco", tile = 1, x = -6, y = 8, dx = 0, dy = 0 } },
            { "i_route", new TreeSpot { par = "k_route", tile = 1, x = -10, y = -3, dx = 0, dy = 0 } },
        };
        public static readonly string[] IconOrder = { "c_pow", "c_rad", "c_spd", "c_fuel", "c_crit", "c_double", "c_magnet", "c_over", "o_wide", "c_find", "d_n", "d_spd", "d_reach", "d_mag", "d_sig", "d_grade", "d_fix", "d_pair", "d_fact", "b_n", "s_speed", "b_pr", "b_cap", "b_pf", "b_br", "b_chain", "b_pack", "e_val", "e_vault", "e_att", "e_quest", "e_talk", "e_tip", "e_save", "e_guard", "e_used", "R", "a_open", "a_auto", "a_read", "a_ins", "a_big", "p_moon", "p_mars", "p_jup", "p_sat", "w_hub", "w_laser", "w_laser_u", "w_laser_a", "w_chain", "w_chain_u", "w_chain_a", "e_shop", "k_claw", "k_drone", "k_bh", "k_eco", "k_route", "w_slot2", "q_insider", "q_rage", "q_front", "q_debt", "q_meteor", "q_sling", "q_tour", "q_rock", "q_gold", "q_lazy", "w_vac", "w_vac_u", "w_vac_a", "w_mine", "w_mine_u", "w_mine_a", "w_frz", "w_frz_u", "w_frz_a", "w_clus", "w_clus_u", "w_clus_a", "w_mag", "w_mag_u", "w_mag_a", "w_rail", "w_rail_u", "w_rail_a", "x_claw_arm", "x_arm_drone", "x_drone_bh", "x_bh_eco", "x_eco_route", "x_route_claw", "i_claw", "i_drone", "i_bh", "i_eco", "i_route", "p_belt", "p_ura", "p_nep", "p_kui", "w_laser_e", "w_chain_e", "w_vac_e", "w_mine_e", "w_frz_e", "w_clus_e", "w_mag_e", "w_rail_e", "l_more", "l_luck", "l_free", "l_jack", "m_claw", "m_crit", "m_fuel", "m_drone", "m_dcount", "m_bh", "m_val", "m_route", "w_laser_x", "w_chain_x", "w_vac_x", "w_mine_x", "w_frz_x", "w_clus_x", "w_mag_x", "w_rail_x" };
        public static string VisBranch(string id) => id.StartsWith("w_") ? "arm" : id.StartsWith("x_") ? "cross" : id == "c_fuel" || id == "o_wide" || id == "c_find" ? "hull" : id == "s_speed" ? "bh" : Nodes[NodeIx[id]].branch;

        static Node N(string id, string br, string name, string desc, string[] par, int seg, double first, double mult, int max, int depth, int lane)
            => new Node { id = id, branch = br, name = name, desc = desc, par = par, seg = seg, first = first, mult = mult, max = max, depth = depth, lane = lane };
        static readonly Dictionary<string, int> NodeIx = new Dictionary<string, int>();
        public static readonly string[] BranchIds = { "claw", "drone", "bh", "eco" };
        public static readonly string[] BranchNames = { "빔 · 선체", "드론 격납고", "블랙홀 · 보급", "사무실" };
        public static readonly int[] BranchNeed = { 0, 1, 2, 0 };

        // ───────────────────────── 청구서 여덟 (§7-3)
        public struct Bill { public string t, perk; public double m, credit; public int due; }
        public static readonly Bill[] Bills =                                   // 09-24 8장 → 12장 (2시간 · 약 ×3~4씩)
        {
            new Bill { t = "연료비",           m = 45,         due = 5, credit = 2,  perk = "케슬러 금융이 청소선 한 대를 믿어 주었다" },
            new Bill { t = "청소선 할부 1회",  m = 700,        due = 4, credit = 3,  perk = "창구 직원이 이름을 외웠다" },
            new Bill { t = "궤도 사용료",      m = 2500,       due = 4, credit = 5,  perk = "궤도청에 청소선이 정식 등록됐다" },
            new Bill { t = "정비비",           m = 30000,      due = 4, credit = 6,  perk = "정비소 단골이 됐다" },
            new Bill { t = "보험료",           m = 180000,     due = 5, credit = 8,  perk = "궤도 보험이 청소선을 받아 주었다" },
            new Bill { t = "청소선 할부 2회",  m = 600000,     due = 4, credit = 10, perk = "청소선 절반은 이제 내 것" },
            new Bill { t = "법인세",           m = 2500000,    due = 5, credit = 13, perk = "세금을 내는 어엿한 회사가 됐다" },
            new Bill { t = "청소선 할부 3회",  m = 10000000,   due = 5, credit = 16, perk = "케슬러 금융이 먼저 인사를 한다" },
            new Bill { t = "외행성 면허세",    m = 30000000,   due = 5, credit = 20, perk = "외행성 면허가 나왔다" },
            new Bill { t = "청소선 할부 4회",  m = 80000000,   due = 5, credit = 24, perk = "청소선 네 조각 중 셋이 내 것" },
            new Bill { t = "심우주 보험",      m = 150000000,  due = 6, credit = 28, perk = "심우주에서도 보험이 된다" },
            new Bill { t = "청소선 할부 완납", m = 350000000,  due = 6, credit = 0,  perk = "빚 청산 → 청산 출동" },
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
            new Contract { orbit = 5, kind = 2, target = 400, text = "조각 400개" },
            new Contract { orbit = 5, kind = 5, target = 20,  text = "폭발 탱크 20개" },
            new Contract { orbit = 5, kind = 4, target = 100, text = "연쇄 100" },
            new Contract { orbit = 6, kind = 0, target = 14,  text = "금고 위성 14개" },
            new Contract { orbit = 6, kind = 4, target = 250, text = "연쇄 250" },
            new Contract { orbit = 7, kind = 3, target = 60,  text = "죽은 위성 60개" },
            new Contract { orbit = 7, kind = 7, target = 5,   text = "큰 잔해 5개" },
            new Contract { orbit = 8, kind = 0, target = 18,  text = "금고 위성 18개" },
            new Contract { orbit = 8, kind = 4, target = 300, text = "연쇄 300" },
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
            if (M.perm == null) M.perm = new List<string>();
            if (S.lv == null) S.lv = new int[NodeCount];
            else if (S.lv.Length < NodeCount) { var lv = S.lv; Array.Resize(ref lv, NodeCount); S.lv = lv; }   // 칸이 늘면 산 것은 그대로 두고 뒤에 붙인다 (경매 줄기 · 09-23)
            else if (S.lv.Length > NodeCount) { var lv = S.lv; Array.Resize(ref lv, NodeCount); S.lv = lv; }
            foreach (var kid in KeyNodes) if (NodeIx.TryGetValue(kid, out int ki) && S.lv[ki] > 0 && !M.perm.Contains(kid)) M.perm.Add(kid);
            ApplyPerm();
            for (int pi = 1; pi < PlanetNode.Length; pi++) if ((S.planets & (1 << pi)) != 0 && S.lv[NodeIx[PlanetNode[pi]]] == 0) S.lv[NodeIx[PlanetNode[pi]]] = 1;
            if (S.bill >= 1 && S.lv[NodeIx["d_n"]] == 0) S.lv[NodeIx["d_n"]] = 1;        // 옛 저장 — 청구서로 받은 것들은 칸으로 옮겨 준다
            if (S.bill >= 2 && S.lv[NodeIx["b_n"]] == 0) S.lv[NodeIx["b_n"]] = 1;
            if (S.bill >= 2 && S.lv[NodeIx["e_quest"]] == 0) S.lv[NodeIx["e_quest"]] = 1;
            if (S.parts == null || S.parts.Length != 5) S.parts = new[] { -1, -1, -1, -1, -1 };
            if (ShopOpen && (S.shop == null || S.shop.Count == 0)) RollShop();
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
        public bool DronesOn => Lv("d_n") > 0;                        // 해금은 전부 정비고 (09-24) — 드론 격납고
        public bool BombsOn => Lv("b_n") > 0;
        public bool WeaponOwned(int w) => w == 0 || (w < WeaponNode.Length && Lv(WeaponNode[w]) > 0);
        public int Weapon => 0;                                                // 🔫 늘 기본 공격 (09-24 사장님 「기본 공격에 효과가 붙는 방식 · % 확률로」). S.weapon 은 옛 저장용
        public static readonly double[] ProcBase = { 0, 0.12, 0.10, 0.08, 0.08, 0.08, 0.06, 0.05, 0.04 };
        static readonly string[] ProcUp = { null, "w_laser_u", "w_chain_u", "w_vac_u", "w_mine_u", "w_frz_u", "w_clus_u", "w_mag_u", "w_rail_u" };
        public double ProcChance(int w) => w <= 0 || w >= ProcBase.Length || !WeaponOwned(w) ? 0 : ProcBase[w] * (Lv(ProcUp[w]) >= 1 ? 1.5 : 1) * (Lv("w_slot2") > 0 ? 1.5 : 1) * (Lv(ProcUp[w].Replace("_u", "_x")) > 0 ? 2 : 1);
        void FireW(int w) { wMul = w > 0 && Lv(ProcUp[w].Replace("_u", "_x")) > 0 ? 2 : 1; Fire(w); wMul = 1; }
        public int OwnedWeapons { get { int n = 0; for (int w = 1; w < ProcBase.Length; w++) if (WeaponOwned(w)) n++; return n; } }
        public bool VolleyOn => OwnedWeapons >= 2;                            // 🚀 전탄 발사 — 무기 둘부터
        public double VolleyGain => 0.012 + 0.006 * OwnedWeapons * (Lv("w_slot2") > 0 ? 1.3 : 1);
        bool PickNear(double cx, double cy, double rad, out double x, out double y)
        {
            x = cx; y = cy; Junk best = null; int seen = 0;
            foreach (var j in R.junk)
            {
                if (j.dead || j.hp <= 0) continue;
                double dx = j.x - cx, dy = j.y - cy, d2 = dx * dx + dy * dy;
                if (d2 > rad * rad || d2 < 28 * 28) continue;
                if (Rnd() * ++seen < 1) best = j;                                  // 고르게 하나
            }
            if (best == null) return false;
            x = best.x; y = best.y; return true;
        }
        void FireAt(int w, double x, double y) { var r = R; double ox = r.ax, oy = r.ay; r.ax = x; r.ay = y; FireW(w); r.ax = ox; r.ay = oy; }
        void VolleyTick(double dt)
        {
            var r = R;
            if (r.volleyT <= 0) return;
            r.volleyT -= dt; r.volleyNext -= dt;
            if (r.volleyNext > 0) return;
            r.volleyNext = 0.09;
            for (int w = 0; w < ProcBase.Length; w++)
            {
                if (w > 0 && !WeaponOwned(w)) continue;
                if (!PickNear(EX, EY, 420, out double tx, out double ty)) continue;
                Emit(SwEv.Proc, tx, ty - 20, w, 1);                                  // kk 1 = 전탄 (글자 없이 포대만 번쩍)
                FireAt(w, tx, ty);
            }
        }

        void Procs()                                                           // 기본 공격 한 번마다 산 무기들이 각자 굴린다
        {
            var r = R;
            if (VolleyOn && r.volleyT <= 0)
            {
                r.volley += VolleyGain;
                if (r.volley >= 1) { r.volley = 0; r.volleyT = 1.1; r.volleyNext = 0.3; Emit(SwEv.Volley, r.ax, r.ay, OwnedWeapons, 0, "전탄 발사!"); }
            }
            for (int w = 1; w < ProcBase.Length; w++)
            {
                double p = ProcChance(w); if (p <= 0 || Rnd() >= p) continue;
                // 🎯 포대마다 다른 목표 (09-24 사장님 전탄 B안) — 조준점 둘레의 다른 쓰레기를 골라 친다
                if (!PickNear(r.ax, r.ay, 170, out double tx, out double ty)) { tx = r.ax; ty = r.ay; }
                Emit(SwEv.Proc, tx, ty - 26, w, 0);                                   // 먼저 알린다 — 화면이 그 무기 포대에서 쏘게
                if (w == 1 || w == 3 || w == 5) { r.chan[w] = 0.7; r.chanNext[w] = 0; r.chanX[w] = tx; r.chanY[w] = ty; }   // 레이저 · 청소기 · 냉동 = 0.7초 동안 이어서
                else FireAt(w, tx, ty);
            }
        }
        public bool Equip(int w) { if (!WeaponOwned(w) || R != null && !R.over) return false; S.weapon = w; if (S.weapon2 == w) S.weapon2 = -1; return true; }
        public bool Equip2(int w) { if (Lv("w_slot2") <= 0 || w >= 0 && (!WeaponOwned(w) || w == S.weapon) || R != null && !R.over) return false; S.weapon2 = w; return true; }
        public bool ContractsOn => Lv("e_quest") > 0;
        public bool BigsOn => S.orbit >= 2;                              // 큰 잔해는 화성부터 (행성의 성격)
        public int MaxOrbit { get { int m = 0; foreach (int i in OrbitOrder) if (Open(i)) m = i; return m; } }   // 가장 먼 (순위)
        public bool Open(int i) => i == 0 || (S.planets & (1 << i)) != 0 || Lv(PlanetNode[i]) > 0;
        public bool OnSale(int i) { if (Open(i)) return false; var st = State(NodeIx[PlanetNode[i]]); return st == NodeSt.Can || st == NodeSt.Poor; }   // 정비고 항로 칸이 다음 차례
        public bool BuyPermit(int i)
        {
            if (!R.over || !OnSale(i) || S.cash < Orbits[i].permit) return false;
            return BuyTile(NodeIx[PlanetNode[i]]);
        }
        void PlanetBought(int i)
        {
            S.planets |= 1 << i;
            // 🎬 막 전환 (09-24 레벨 설계) — 목성 = 2막 외행성 · 해왕성 = 3막 심우주
            if (i == 3) { Emit(SwEv.Act, 0, 0, 2, 0, "2막 · 외행성"); AddNews(null, "외행성 면허 발급 — 청소선, 목성 너머로", "궤도청이 외행성 청소 면허를 내줬다. 정비고 바깥 고리가 열렸다는 소문이다."); }
            if (i == 7) { Emit(SwEv.Act, 0, 0, 3, 0, "3막 · 심우주"); AddNews(null, "심우주 진입 — 해왕성 궤도에 민간 청소선", "태양이 점처럼 보이는 곳까지 왔다. 마지막 청구서가 기다린다."); }
            AddNews(null, Orbits[i].name + " 청소 허가 — 민간 청소선 첫 진입", "케슬러 금융이 " + Orbits[i].name + " 궤도 청소 허가증을 내줬다. " + Orbits[i].desc + ". 값은 지구의 " + Orbits[i].mult + "배라고 한다.");
            S.orbit = i; RollContract(); Preview();
            if (Mk != null && StockOpen) { string[] sec = { "", "달", "화성", "목성", "관광", "화성", "목성", "관광", "관광" }; Mk.GameEvent("민간 청소선 " + Orbits[i].name + " 진출", "궤도 청소부가 " + Orbits[i].name + " 청소 허가를 땄다. 관련 업계가 들썩인다.", new[] { sec[i], "ship" }, null, 0.14f); }
        }
        public double FuelMax => Math.Max(12, ((30 + 3 * Lv("c_fuel") + 2 * Lv("d_fix")) * (1 + 0.2 * Cr(0)) + Part("fuel")) * (Lv("k_claw") > 0 ? 0.85 : 1)) * (1 + 0.25 * Lv("m_fuel"));
        public double Gap => Math.Max(0.3, 0.6 - 0.045 * Lv("c_spd")) / (1 + Part("spd"));
        public double ClawR => Lv("c_rad") > 0 ? (22 + 10 * Lv("c_rad")) * (1 + Part("rad")) : 0;   // 0 = 하나씩
        public bool AutoClaw => true;        // 🔴 자동이 기본 (사장님 09-23: "클릭은 빼자 오토는 기본으로")
        public const double PickR = 30;      // 범위 강화 전 — 커서 밑 하나를 잡는 거리
        public int ClawDmg => 1 + Lv("c_pow");
        public double Part(string k) => Parts.Sum(S.parts, k);
        public double DmgMul => Math.Pow(1.5, Lv("m_claw")) * wMul * RawDmgMul * (R != null ? 1 + R.consDmg / 100.0 : 1);   // ✦ 과충전 포신 · 무기 3단계 위력
        double wMul = 1;
        double RawDmgMul => 1 + Part("dmg") + (Lv("k_claw") > 0 ? 0.4 : 0) + Rage + Grit + 0.05 * Lv("i_claw") + (Lv("x_route_claw") > 0 ? new[] { 0, 0.05, 0.1, 0.2, 0.35 }[Math.Min(4, S.orbit)] : 0);
        public double Rage { get { if (Lv("q_rage") <= 0 || Mk == null) return 0; double v = 0, c = 0; foreach (var s in Mk.M.st) if (s.shares > 0) { v += s.shares * s.price; c += s.cost; } return c > 0 ? Math.Min(0.5, Math.Max(0, 1 - v / c)) : 0; } }   // ★ 물린 개미의 분노
        public double Grit => Lv("q_debt") > 0 && S.debt > 0 ? Math.Min(0.15, S.debt / Math.Max(1, BillAmount) * 0.1) : 0;   // ★ 빚쟁이의 근성
        public double Pow => ClawDmg * DmgMul * clickMul;                                  // 무기 화력 (소수는 확률로)
        int RoundP(double v) => (int)v + (Rnd() < v - (int)v ? 1 : 0);
        public double HpMul => ((1 + 0.45 * Math.Max(0, S.bill - 2)) * Orbits[S.orbit].hp) * (Lv("k_route") > 0 ? 1.2 : 1) * (M.endless ? Math.Pow(1.25, M.depth) : 1);   // 잔해 체력 배율 — 청구서 3장째부터 한 장마다 +45% (초반은 가볍게)
        public int BlastDmg => 2 + 2 * ClawDmg;                    // 폭발은 즉사가 아니라 피해
        public double Crit => 0.05 * Lv("c_crit") + Part("crit") + (Lv("x_claw_arm") > 0 ? 0.1 : 0);
        public int CritX => (Lv("x_claw_arm") > 0 ? 4 : 3) + Lv("m_crit");
        public int DroneCount => DronesOn ? 1 + Lv("d_n") + Lv("d_fact") + Cr(3) + (Lv("k_drone") > 0 ? 4 : 0) + 2 * Lv("m_dcount") : 0;   // 격납고 첫 칸 = 두 대
        public double DroneCd => Math.Max(0.4, 1 - 0.1 * Lv("d_spd")) * (Lv("x_drone_bh") > 0 && R != null && R.holding ? 0.5 : 1);
        public double Reach => 80 + 15 * Lv("d_reach");
        public int Grade => 1 + Lv("d_grade");
        public double DroneMag => (1 + 0.25 * Lv("d_mag")) * (1 + Part("drone")) * (Lv("k_drone") > 0 ? 0.75 : 1) * (1 + 0.05 * Lv("i_drone")) * Math.Pow(1.5, Lv("m_drone"));
        public int Bombs => BombsOn ? Math.Min(6, 2 + Lv("b_n") + (S.bill >= 7 ? 1 : 0) + Cr(4)) : 0;
        public double PullR => (90 + 14 * Lv("b_pr")) * (1 + 0.15 * Lv("m_bh"));                // 09-24 사장님 「블랙홀 크기 많이 줄이고」 150+20 → 90+14
        public double PullF => 1 + 0.25 * Lv("b_pf");
        public int Cap => 22 + 9 * Lv("b_cap");
        public double BlastK => 1 + 0.15 * Lv("b_br");
        public double ChainP => Math.Min(0.85, 0.3 + 0.07 * Lv("b_chain"));    // 무기 폭발이 또 번질 확률
        public double HoleCd => 16 - 1.5 * Lv("s_speed");     // (옛 시간 충전 — 이제 안 쓴다)
        public double HoleChance => R.clean ? 0.05 : Lv("b_n") <= 0 ? 0 : (0.012 + 0.002 * Lv("b_n") + 0.0025 * Lv("s_speed") + Part("hole") + 0.001 * Lv("i_bh")) * (Lv("k_bh") > 0 ? 0.7 : 1) * (1 + 0.3 * Lv("m_bh"));   // 🌀 블랙홀 — 집게가 맞힐 때 이 확률로 그 자리에 저절로 열린다 (09-24 사장님 「자동으로 바닥에 깔리는 걸로」 · Q 스킬 없앰)
        public const double HoleDur = 3;                           // 열려 있는 시간 — 끝나면 저절로 터진다
        public double PackK => 0.02 + 0.012 * Lv("b_pack");
        // 🔴 한 번 터질 때 이어지는 연쇄의 한계 — 도파민 사다리(§5)가 구간마다 한 단계씩 열리게
        public int ChainMax => R.clean ? 5000 : 40 + (S.orbit >= 1 ? 20 : 0) + (S.orbit >= 2 ? 40 : 0) + 15 * Lv("b_chain");
        // 🌪 모래 폭풍 (화성 · 해왕성) — 22초마다 4.5초. 값 ×1.5 · 왼쪽에서 고철이 몰려온다 (09-24 사장님 36번 「무의미함」)
        public bool StormOn => R != null && !R.over && !R.clean && Orbits[S.orbit].storm && R.t % 22.0 >= 15 && R.t % 22.0 < 19.5;
        public double ValMult => (1 + 0.1 * M.legend) * (M.endless ? Math.Pow(1.2, M.depth) : 1) * (StormOn ? 1.5 : 1) * (R != null ? 1 + R.consVal / 100.0 : 1) * Math.Pow(1.25, Lv("e_val")) * Math.Pow(1.3, Cr(1)) * (Orbits[S.orbit].mult + (Lv("k_route") > 0 && S.orbit > 0 ? 0.5 : 0) + (S.orbit > 0 ? 0.05 * Lv("i_route") : 0)) * Econ * (1 + Part("val")) * (Lv("k_eco") > 0 ? 1.25 : 1) * (1 + 0.04 * Lv("i_eco")) * PlanetStockBonus * Math.Pow(1.5, Lv("m_val")) * Math.Pow(1.25, Lv("m_route"));
        public double PlanetStockBonus { get { if (Lv("x_eco_route") <= 0 || Mk == null || S.orbit == 0) return 1; string[] ids = { "", "moon", "mars", "jup", "sat", "", "", "", "" }; if (ids[S.orbit] == "") return 1; for (int i = 0; i < Market.Defs.Length && i < Mk.M.st.Count; i++) if (Market.Defs[i].id == ids[S.orbit] && Mk.M.st[i].shares > 0) return 1.2; return 1; } }   // 새 행성은 종목이 없다
        public double Cut => S.debt > 0 ? Math.Max(0.1, (Lv("e_guard") > 0 || Cr(5) > 0 ? 0.2 : 0.3) - Part("cut")) : 0;   // 빚이 있으면 판 수입에서 떼어 상환
        // ── 대출 (연체 대신) — 언제든 받을 수 있다. 받은 돈 × 배수를 판 수입에서 조금씩 갚는다
        public static double LoanMult = 3;
        public void EnterEndless()
        {
            M.won = false; M.endless = true; if (M.depth < 1) M.depth = 1; if (M.bestDepth < M.depth) M.bestDepth = M.depth;
            S.orbit = MaxOrbit; RollContract(); Preview();
            AddNews(null, "무한 궤도 개장 — 청소선, 끝없는 궤도로", "빚은 끝났다. 이제 누가 더 깊이 내려가는지만 남았다.");
        }
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
        public double StockFee => Part("fee0") > 0 ? 0 : new[] { 0.01, 0.006, 0.003, 0 }[Math.Min(3, Lv("a_big"))];
        public double StockDiv => 0.0003 * Lv("a_big") + Part("div");
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
        public int ScratchMax => 3 + Lv("l_more");                                  // 🎟 복권 단골
        public int ScratchLeft => S.scratchRun == S.runs ? Math.Max(0, ScratchMax - S.scratchN) : ScratchMax;
        public double ScratchCost => Lv("l_free") > 0 && (S.scratchRun != S.runs || S.scratchN <= 0) ? 0 : ScratchPrice;   // 첫 장은 공짜
        public double ScratchPending;                                     // 긁어서 다 보이면 받는다
        /// <summary>한 장 산다 — 돌려주는 값 = 칸 아홉의 그림 (null = 못 삼). win = 당첨 그림 (-1 꽝)</summary>
        public int[] ScratchBuy(out int win)
        {
            win = -1;
            if (ScratchLeft <= 0 || S.cash < ScratchCost) return null;
            double cost = ScratchCost;
            if (S.scratchRun != S.runs) { S.scratchRun = S.runs; S.scratchN = 0; }
            S.scratchN++; S.cash -= cost;
            double u = luck.NextDouble() / (1 + 0.25 * Lv("l_luck"));         // 행운의 긁개
            win = u < 0.001 ? 4 : u < 0.009 ? 3 : u < 0.044 ? 2 : u < 0.114 ? 1 : u < 0.234 ? 0 : -1;
            var g = new int[9]; var cnt = new int[5];
            var slots = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            if (win >= 0) for (int k = 0; k < 3; k++) { int j = luck.Next(slots.Count); g[slots[j]] = win; slots.RemoveAt(j); cnt[win]++; }
            foreach (var j in slots)
            {
                int sym; do sym = luck.Next(5); while (sym == win || cnt[sym] >= 2);   // 꽝 칸은 같은 그림이 둘까지만
                g[j] = sym; cnt[sym]++;
            }
            ScratchPending = win >= 0 ? ScratchPrice * ScratchMult[win] * (Lv("l_jack") > 0 ? 2 : 1) : 0;   // 잭팟 ×2
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
        // 🔩 부품 가게 — 진열 셋, 출동이 끝날 때마다 새로. 값은 지금 청구서에 맞춰 오른다
        public bool ShopOpen => Lv("e_shop") > 0;
        /// <summary>★ 1면 조작 — 고른 기사를 증권 속보로 낸다</summary>
        public void PickFront(int k)
        {
            int id = k == 0 ? S.front1 : S.front2; S.front1 = S.front2 = -1;
            if (id < 0 || Mk == null) return;
            S.frontPick = id;                                                 // 📰 내일 1면 확정 — 다음 출동을 시작할 때 발행 (시장은 출동 중에만 흐른다)
        }
        void PublishFront()
        {
            int id = S.frontPick; S.frontPick = -1;
            if (id < 0 || id >= Market.NewsBook.Length || Mk == null) return;
            var nd = Market.NewsBook[id]; string h = nd.head.Replace("[소문] ", "");
            Mk.Publish("오늘 1면 — " + h, nd.body, nd.up, nd.down, nd.size * 1.5f, false);   // 조작한 1면은 세게 (09-24 사장님 7번 「안 되는 것 같다」)
            AddNews(null, "오늘 1면 — " + h, "궤도일보 1면. (편집장은 청소선에서 온 제보라고만 했다)");
        }
        // 🔩 가게 v2 (09-24 사장님 24번 「너무 비싸기만 하다 · 판마다 바뀌고 · 돈으로 바꾸고 · 가격 다양하게」)
        public double ShopBase => Math.Max(80, Math.Round(BillAmount * 0.15 / 10) * 10);
        public const int Cons0 = 200;
        public static readonly string[] ConsName = { "연료 캔", "복권 묶음", "과부하 탄창", "감정 할인권" };
        public static readonly string[] ConsDesc = { "다음 판 연료 +10초", "즉석 복권 +3장", "다음 판 화력 +20%", "다음 판 모든 값 +15%" };
        static readonly double[] ConsPrice = { 0.25, 0.2, 0.45, 0.5 };
        public static bool IsCons(int id) => id >= Cons0 && id < Cons0 + ConsName.Length;
        public double PartPrice(int id)
        {
            double b = ShopBase, p = id == Parts.Key ? b * 2.5 : IsCons(id) ? b * ConsPrice[id - Cons0] : b * Parts.RarPrice[Parts.Defs[id].rar];
            double jit = 1 + 0.15 * Math.Sin(id * 12.9898 + S.runs * 78.233);          // 판마다 조금씩 다른 값
            return Math.Max(10, Math.Round(p * jit / 10) * 10);
        }
        public double ShelfPrice(int k) => S.shop == null || k < 0 || k >= S.shop.Count ? 0 : Math.Max(10, Math.Round(PartPrice(S.shop[k]) * (k == S.shopSale ? 0.5 : 1) / 10) * 10);
        public double RerollPrice => S.freeRoll ? 0 : Math.Round(ShopBase * 0.25);
        public void RollShop()
        {
            if (S.shop == null) S.shop = new List<int>();
            S.shop.Clear();
            var used = new HashSet<int>(S.parts ?? new int[0]);
            bool key = false;
            for (int c = 0; c < 2; c++) { int ci; do ci = Cons0 + rng.Next(ConsName.Length); while (S.shop.Contains(ci)); S.shop.Add(ci); }   // 🧃 소모품 둘 (싸다)
            for (int n = 0; n < 4; n++)
            {
                if (!key && Rnd() < 0.18) { S.shop.Add(Parts.Key); key = true; continue; }
                for (int t = 0; t < 30; t++)
                {
                    double u = Rnd(); int rar = u < 0.68 ? 0 : u < 0.94 ? 1 : 2;
                    int id = rng.Next(Parts.Defs.Length);
                    if (Parts.Defs[id].rar != rar || used.Contains(id) || S.shop.Contains(id)) continue;
                    S.shop.Add(id); break;
                }
            }
            S.shopSale = S.shop.Count > 0 ? rng.Next(S.shop.Count) : -1;         // 오늘의 반값 한 칸
        }
        public bool BuyPart(int k)
        {
            if (!R.over || !ShopOpen || S.shop == null || k < 0 || k >= S.shop.Count) return false;
            int id = S.shop[k]; double p = ShelfPrice(k);
            if (S.cash < p) return false;
            S.cash -= p; S.shop.RemoveAt(k);
            if (k == S.shopSale) S.shopSale = -1; else if (k < S.shopSale) S.shopSale--;
            if (id == Parts.Key) { S.keys++; return true; }
            if (IsCons(id))
            {
                switch (id - Cons0)
                {
                    case 0: S.nFuel += 10; break;
                    case 1: if (S.scratchRun != S.runs) { S.scratchRun = S.runs; S.scratchN = 0; } S.scratchN -= 3; break;
                    case 2: S.nDmg += 20; break;
                    case 3: S.nVal += 15; break;
                }
                return true;
            }
            if (S.parts == null || S.parts.Length != 5) S.parts = new[] { -1, -1, -1, -1, -1 };
            S.parts[Parts.Defs[id].slot] = id;
            return true;
        }
        public bool RerollShop() { if (!R.over || !ShopOpen || S.cash < RerollPrice) return false; S.cash -= RerollPrice; S.freeRoll = false; RollShop(); return true; }
        public int TotalLv { get { int n = 0; foreach (var l in S.lv) n += l; return n; } }
        public double Widen => 1 + 0.1 * Lv("o_wide");      // 🔴 정비소에서 산다 (사장님 09-23: "맵 크기도 여기서 늘리게")
        public double Bo => Orbits[S.orbit].bi + (Orbits[S.orbit].bo - Orbits[S.orbit].bi) * Widen;
        public double BillAmount => S.bill < Bills.Length ? (S.billAmount >= 0 ? S.billAmount : Bills[S.bill].m) * (Lv("k_eco") > 0 ? 1.1 : 1) : 0;
        void ApplyPerm() { foreach (var kid in M.perm) if (NodeIx.TryGetValue(kid, out int ki) && S.lv[ki] < Nodes[ki].max) S.lv[ki] = Nodes[ki].max; }
        public int BankruptKeys => 2 + S.bill / 2;                                // 청구서 7장째 = 열쇠 5
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
        public static bool Infinite(int i) => Nodes[i].id.StartsWith("i_");
        public static int Tiles(int i) => Infinite(i) ? 1 : Math.Min(Nodes[i].max, 5);
        /// <summary>j번째 칸을 사면 되는 레벨 — 앞 칸은 작게(1레벨), 뒤로 갈수록 크게. 12레벨이면 1 · 3 · 5 · 8 · 12</summary>
        public static int TileLv(int i, int j)
        {
            if (Infinite(i)) return 1;
            int T = Tiles(i), max = Nodes[i].max;
            if (j >= T) return max;
            int v = Math.Max(j, (int)Math.Round(max * Math.Pow((double)j / T, 1.6)));
            return Math.Min(v, max - (T - j));
        }
        public int NextTile(int i) { for (int j = 1; j <= Tiles(i); j++) if (TileLv(i, j) > S.lv[i]) return j; return Tiles(i) + 1; }
        public double TileCost(int i)
        {
            if (Infinite(i)) return CostAt(i, S.lv[i]);
            int j = NextTile(i); if (j > Tiles(i)) return 0;
            double c = 0; for (int l = S.lv[i]; l < TileLv(i, j); l++) c += CostAt(i, l);
            return c;
        }
        public bool BuyTile(int i)
        {
            if (!R.over || State(i) != NodeSt.Can) return false;
            S.cash -= TileCost(i); S.lv[i] = Infinite(i) ? S.lv[i] + 1 : TileLv(i, NextTile(i));
            var id = Nodes[i].id;
            if (KeyNodes.Contains(id)) { S.keys--; if (!M.perm.Contains(id)) M.perm.Add(id); }   // 🔑 파산해도 남는다
            if (id == "e_shop") RollShop();
            int pi = Array.IndexOf(PlanetNode, id); if (pi > 0) PlanetBought(pi);
            if (id == "e_quest" && S.lv[i] == 1 && S.contract < 0) RollContract();
            Emit(SwEv.NodeBought, 0, 0, i, S.lv[i]);
            return true;
        }
        public bool BranchOpen(string br) => true;                        // 가지는 처음부터 다 보인다 — 값으로만 막는다 (09-24)
        // 🪐 행성 구역 (09-24 사장님 6·21번 「지구에선 여기까지 · 다 찍어야 다음 행성」) — 칸마다 구역(= 항로 순위).
        //    구역은 첫 가격으로 나누고 부모보다 앞설 수 없다. 항로 칸은 앞 행성 구역. 핵심 · 무한 · 네 번째 고리는 「다 찍기」에서 뺀다
        public static readonly double[] ZoneCost = { 200, 2000, 20000, 200000, 2500000 };   // 구역 칸 합 ≈ 다음 항로 값 (봇으로 맞춤)   // 지구 · 달 · 화성 · 소행성대 · 목성 · (그 위 토성)
        public static readonly string[] ZoneName = { "지구", "달", "화성", "소행성대", "목성", "토성", "천왕성", "해왕성", "카이퍼 벨트" };
        static int[] zone;
        public static int[] Zone
        {
            get
            {
                if (zone != null) return zone;
                var z = new int[Nodes.Length];
                int Calc(int i)
                {
                    if (z[i] > 0) return z[i] - 1;
                    var n = Nodes[i]; int v;
                    int pi = Array.IndexOf(PlanetNode, n.id);
                    if (pi > 0) v = Math.Max(0, Array.IndexOf(OrbitOrder, pi) - 1);
                    else
                    {
                        v = 0; while (v < ZoneCost.Length && n.first >= ZoneCost[v]) v++;
                        foreach (var p in n.par) if (!Nodes[NodeIx[p]].id.StartsWith("p_")) v = Math.Max(v, Calc(NodeIx[p]));
                    }
                    z[i] = v + 1; return v;
                }
                for (int i = 0; i < Nodes.Length; i++) Calc(i);
                for (int i = 0; i < z.Length; i++) z[i]--;
                return zone = z;
            }
        }
        public int ZoneOpen { get { int oi = 0; while (oi + 1 < OrbitOrder.Length && (S.planets & (1 << OrbitOrder[oi + 1])) != 0) oi++; return oi; } }
        public static bool ZoneNeed(int i) { var id = Nodes[i].id; return !id.StartsWith("p_") && id != "e_shop" && !KeyNodes.Contains(id) && !Infinite(i) && !Ring4(id); }
        public int ZoneLeft(int z) { int c = 0; for (int i = 0; i < Nodes.Length; i++) if (Zone[i] == z && ZoneNeed(i) && S.lv[i] <= 0) c++; return c; }

        public NodeSt State(int i)
        {
            var n = Nodes[i];
            if (!BranchOpen(n.branch)) return NodeSt.Locked;
            if (S.lv[i] >= n.max) return NodeSt.Max;
            if (Ring4(n.id) && Lv("p_jup") <= 0) return NodeSt.Locked;           // ✦ 외행성 면허 = 목성 항로
            if (Zone[i] > ZoneOpen) return NodeSt.Locked;                          // 🪐 그 행성 항로를 사야 열린다
            if (n.id.StartsWith("p_") && ZoneLeft(Zone[i]) > 0) return NodeSt.Locked; // 🪐 지금 구역 칸을 다 찍어야 다음 항로
            foreach (var p in n.par) if (S.lv[NodeIx[p]] <= 0) return NodeSt.Hidden;
            var pl = Layout[n.id];
            if (pl.par != "R" && S.lv[NodeIx[pl.par]] < TileLv(NodeIx[pl.par], pl.tile)) return NodeSt.Hidden;
            if (KeyNodes.Contains(n.id) && S.keys < 1) return NodeSt.Poor;          // ◆ 열쇠가 없다
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
            S.bill++; S.keys++;                                              // 🔑 청구서마다 열쇠 1 (파산 없이도 조금씩 열린다)
            S.overdue = false; S.overRuns = 0; S.billAmount = -1;
            S.billDue = S.bill < Bills.Length ? Bills[S.bill].due + Lv("e_talk") : 0;
            string nid = "bill" + S.bill;
            AddNews(nid);
            Emit(SwEv.BillPaid, 0, 0, S.bill, 0, b.t + " 납부 완료 — " + b.perk + " · 열쇠 +1");
            CheckClean();
        }

        public bool Bankrupt()
        {
            if (!R.over || !CanBankrupt) return false;
            M.history.Add(new PastCompany { company = M.company, bill = S.bill, runs = S.runs, minutes = (M.playSeconds - S.startedAt) / 60 });
            M.credit += S.creditPending;
            M.bankrupt++;
            AddNews(M.bankrupt == 1 ? "bankrupt1" : M.bankrupt == 2 ? "bankrupt2" : null, "궤도 청소부 (" + M.company + "대), 출동 " + S.runs + "번 만에 파산", "청구서 " + S.bill + "장을 갚고 문을 닫았다. 빚은 날아갔고, 조종사의 경력은 남았다.");
            M.company++;
            double carry = Lv("x_bh_eco") > 0 ? Math.Floor(S.cash * 0.1) : 0; int keepPart = -1;
            int bk = BankruptKeys, keptKeys = S.keys;                                                    // 🔑 파산하면 열쇠 (09-24 사장님 「파산의 가치를 늘리려고」)
            if (Lv("x_bh_eco") > 0 && S.parts != null) foreach (var pid in S.parts) if (pid >= 0 && (keepPart < 0 || Parts.Defs[pid].rar > Parts.Defs[keepPart].rar)) keepPart = pid;
            S = new SweepState { startedAt = M.playSeconds };
            if (carry > 0) S.cash += carry;
            S.keys += bk + keptKeys; ApplyPerm();                         // 남은 열쇠도 넘어간다 · ◆ 핵심 칸은 켜진 채로
            if (keepPart >= 0) S.parts[Parts.Defs[keepPart].slot] = keepPart;   // ◆ 파산 보험 — 돈 10% · 제일 좋은 부품 하나
            MakeMarket();
            M.careerOpen = true;
            Preview();
            if (M.company == 2) AddNews("company2");
            Emit(SwEv.Bankrupt, 0, 0, M.company, 0, "주식회사 궤도 청소부 (" + (M.company - 1) + "대) — 파산 · 열쇠 +" + bk);
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
            if (!R.over || S.bill >= Bills.Length && !M.cleanReady && S.debt <= 0 && !M.endless) return;   // 청구서를 다 갚아도 빚이 남았으면 갚으러 출동한다
            bool clean = M.cleanReady;
            if (clean) S.orbit = MaxOrbit;
            S.runs++; S.rerolled = false;
            var r = new SweepRun { clean = clean };
            r.max = r.fuel = clean ? 45 : FuelMax + S.nFuel;
            r.consDmg = S.nDmg; r.consVal = S.nVal; S.nFuel = S.nDmg = S.nVal = 0;   // 🧃 가게 소모품 — 이번 판
            r.maxShots = clean ? 6 : Bombs; r.shots = 0;   // 블랙홀은 스킬 — 한 칸 들고 나가서 시간 따라 찬다
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
            if (Lv("q_rock") > 0 && !clean && Rnd() < 0.35) r.rockT = Rnd(12, 26);
            // 사건 — 10~14초, 22~26초 (연료 40 넘을 때만)
            var ev = o.events;
            if (S.runs >= 2 || clean)                                     // 판 중 사건 — 세 번째 출동부터 (청구서와 상관없이)
            {
                r.ev1 = ev[rng.Next(ev.Length)]; r.ev1T = Rnd(10, 14);
                if (r.max >= 40) { r.ev2 = ev[rng.Next(ev.Length)]; r.ev2T = Rnd(22, 26); }
            }
            r.collector = false;                                        // 추심선은 대출로 바뀌며 쉰다
            // 블랙박스 — 청구서 2 뒤 · 판마다 25%
            if ((S.runs >= 2 || clean) && M.scoops < 6 && (clean || Rnd() < 0.25 + 0.1 * Lv("e_tip")))
            {
                var hosts = r.junk.FindAll(d => IsHost(d.k) && d.att == Att.None);
                if (hosts.Count > 0) hosts[rng.Next(hosts.Count)].att = Att.BBox;
            }
            if (clean) r.cleanGoal = 8000;
            if (S.runs == 6) AddNews("run6");
            PublishFront();                                                  // 📰 조작한 1면 — 출동과 함께 발행
        }

        int Alive() { int n = 0; foreach (var d in R.junk) if (!d.dead) n++; return n; }

        // 🛰 쓰레기 종 — 행동은 종류(k)가 정하고, 모습 · 등급은 종이 정한다 (09-24 사장님 「쓰레기를 더 다양하게 · 하위는 없어지게」)
        // 등급 = 행성 순위(지구 0 … 카이퍼 8). 지금 순위 ±1 이 주로 나오고, 두 단계 아래는 드물게, 그보다 아래는 안 나온다
        public struct Species { public string name, art; public int kind, tier; }
        public static readonly Species[] Spc =
        {
            new Species { name = "고철 조각",       art = "junk_chip_a",     kind = Chip,   tier = 0 },
            new Species { name = "휜 판",           art = "junk_chip_b",     kind = Chip,   tier = 1 },
            new Species { name = "태양판 조각",     art = "junk_chip_c",     kind = Chip,   tier = 1 },
            new Species { name = "광석 덩어리",     art = "junk_ore",        kind = Chip,   tier = 3 },
            new Species { name = "고리 얼음",       art = "junk_ringice",    kind = Chip,   tier = 5 },
            new Species { name = "결정체",          art = "junk_crystal",    kind = Chip,   tier = 6 },
            new Species { name = "혜성 조각",       art = "junk_comet",      kind = Chip,   tier = 8 },
            new Species { name = "죽은 위성",       art = "junk_sat",        kind = Sat,    tier = 1 },
            new Species { name = "채굴 드론 잔해",  art = "junk_minedrone",  kind = Sat,    tier = 3 },
            new Species { name = "관광선 잔해",     art = "junk_tourwreck",  kind = Sat,    tier = 5 },
            new Species { name = "폭풍 탐사선",     art = "junk_stormprobe", kind = Sat,    tier = 7 },
            new Species { name = "고대 탐사선",     art = "junk_ancient",    kind = Sat,    tier = 8 },
            new Species { name = "로켓 잔해",       art = "junk_rocket",     kind = Rocket, tier = 2 },
            new Species { name = "가스 채굴선 잔해", art = "junk_gashulk",   kind = Rocket, tier = 4 },
        };
        public int Rank => Math.Max(0, Array.IndexOf(OrbitOrder, S.orbit));      // 가까운 → 먼 순위
        int PickSpecies(int k)
        {
            int rk = R != null && R.clean ? 8 : Rank, best = -1; double sum = 0; var w = new double[Spc.Length];
            for (int i = 0; i < Spc.Length; i++)
            {
                if (Spc[i].kind != k) continue;
                int dt = Spc[i].tier - rk;
                w[i] = dt == 0 ? 2 : Math.Abs(dt) == 1 ? 1 : dt == -2 ? 0.25 : 0; sum += w[i];
                if (Spc[i].tier <= rk + 1 && (best < 0 || Spc[i].tier > Spc[best].tier)) best = i;
            }
            if (sum <= 0) return best;                                             // 창 안에 없으면 가장 높은 것
            double v = Rnd() * sum;
            for (int i = 0; i < w.Length; i++) { v -= w[i]; if (w[i] > 0 && v <= 0) return i; }
            return best;
        }

        int PickType()
        {
            var o = Orbits[S.orbit];
            double[] w = (double[])o.mix.Clone();
            w[Vault] *= (1 + 0.5 * Lv("e_vault")) * (1 + Part("vault"));
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
            list.Add(Att.Beacon); list.Add(Att.Magnet);
            if (S.orbit >= 2 || R.clean) { list.Add(Att.Det); list.Add(Att.Det); list.Add(Att.Ice); }
            if (S.orbit == 2) { list.Add(Att.Ice); list.Add(Att.Ice); }             // 화성 — 얼음 껍질
            if (Rank >= 4 || R.clean) { list.Add(Att.Armor); list.Add(Att.Armor); }          // 목성부터 (순위)
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
            if (att == null && Lv("q_gold") > 0 && IsHost(k) && Rnd() < 0.012) at = Att.Gold;
            if (att == null && at == Att.None && IsHost(k) && Rnd() < (R.clean ? 0.35 : o.att * (1 + 0.4 * Lv("e_att")) * (1 + Part("att")))) at = PickAtt();
            int hp = (int)Math.Round(Types[k].hp * (k == Fuel || k == Tank ? 1 : HpMul)) + (at == Att.Ice ? 2 : 0);   // 청구서를 갚을수록 단단해진다
            var d = new Junk { id = ++R.idc, k = k, sp = PickSpecies(k), hp = hp, max = hp, att = at, a = a, rr = rr, ws = ws > 0 ? ws : Rnd(0.92, 1.08), rot = Rnd(0, 6), vr = Rnd(-1, 1) };
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
            // ★ 게으름 보너스 — AUTO로 30초 손을 안 대면 드론 두 대 (idleT 는 게임이 손을 대면 0으로)
            r.idleT += dt;
            if (Lv("q_lazy") > 0 && !r.lazyDone && r.idleT > 30 && DronesOn) { r.lazyDone = true; r.drones.Add(new Drone { a = Rnd(0, 6.28), cd = Rnd() }); Emit(SwEv.Pop, ShipX, ShipY - 20, 0, 1, "게으름 보너스 — 드론 +1"); }
            // ★ 떠돌이 소행성
            if (r.rockT > 0 && r.t >= r.rockT) { r.rockT = -1; var o = Orbits[S.orbit]; var rk = Spawn(Big, Rnd(0, 6.28), (o.bi + Bo) / 2, Att.Rock, false, 0.7); rk.hp = rk.max = (int)Math.Round(rk.max * 2.5); Emit(SwEv.Warn, 0, 0, 0, 1, "떠돌이 소행성이 궤도에 끼어들었다!"); }
            if (aim) { r.ax = ax; r.ay = ay; }
            if (r.fuel > 0) r.fuel -= dt;

            // 🌀 블랙홀 스킬 — 누르면 그 자리에 열려 3초 빨아들이고 저절로 터진다. 칸은 시간 따라 찬다
            r.holeCd = 0;                                               // 시간으로는 안 찬다 — Strike 에서 확률로
            if (r.holding && (r.holdT >= HoleDur || r.fuel <= 0)) Release();

            Schedule(dt);
            Supply(dt);
            Motion(dt);
            if (r.holding) Pull(dt);
            if (aim && r.fuel > 0) Claw(dt);                                   // 보조 무기 칸은 없앴다 (09-24 확률 효과로)
            Mines(dt);
            if (r.magHole > 0) { r.magHole -= dt; if (r.magHole <= 0 && !r.holding && r.fuel > 0) { r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.hx = r.magX; r.hy = r.magY; r.shots++; Emit(SwEv.SkillReady, r.hx, r.hy, 1); } }   // 자석 각성 — 모인 자리에 블랙홀                           // 블랙홀이 열려 있어도 빔은 계속
            Drones(dt);

            // 💥 연쇄
            for (int i = 0; i < r.pend.Count; i++) r.pend[i].t -= dt;
            for (int i = r.pend.Count - 1; i >= 0; i--)
            {
                var p = r.pend[i];
                if (p.t > 0) continue;
                r.pend.RemoveAt(i);
                blastW = p.w; DoBlast(p.x, p.y, p.R * BlastK); blastW = false;
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
            if (StormOn && r.stormLeft <= 0) { r.stormLeft = 1; r.stormT = 0.15; }       // 폭풍 동안 고철 줄기
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
                    if (p.kind == 1) { Emit(SwEv.SupplyGet, r.ax, r.ay - 18, 0, 1, "블랙홀!"); OpenHole(); }
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
                if (d.frz > 0) d.frz -= dt;
                if (d.free)
                {
                    d.x += d.vx * dt; d.y += d.vy * dt; d.vx *= 1 - 0.9 * dt; d.vy *= 1 - 0.9 * dt;
                    if (d.capT > 0) { d.capT -= dt; if (d.capT <= 0) Recapture(d, o.bi, Bo); }
                }
                else
                {
                    if (d.frz <= 0) d.a += 0.12 * d.ws * dt * o.spin;           // 언 것은 궤도에 멈춰 선다
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
            if (Lv("q_sling") > 0 && n >= 3) Sling(r.hx, r.hy, Math.Min(12, 3 + n / 4));
            r.packed.Clear();
            Emit(SwEv.Release, r.hx, r.hy, n, 0, n >= 6 ? n + "개 압축 · ×" + mult.ToString("0.00") : null);
            DoBlast(r.hx, r.hy, (60 + n * 2.5) * BlastK, false);
            if (Lv("k_bh") > 0 && !r.twin && r.fuel > 0) { r.twin = true; r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.shots++; Emit(SwEv.SkillReady, r.hx, r.hy, 2); }   // ◆ 쌍둥이 — 그 자리에 한 번 더
            else r.twin = false;
            // 모이다 만 것들은 궤도로 돌아간다
            foreach (var d in r.junk) if (d.free && !d.dead && d.capT <= 0) d.capT = 0.6;
        }

        // ★ 중력 새총 — 블랙홀이 모은 것을 사방으로 쏜다 (줄마다 맞은 것 피해)
        void Sling(double x, double y, int rays)
        {
            for (int k = 0; k < rays; k++)
            {
                double ang = k * Math.PI * 2 / rays + Rnd(-0.1, 0.1), ux = Math.Cos(ang), uy = Math.Sin(ang) * Tilt;
                double L = Bo * 0.9;
                for (int ji = 0, jn = R.junk.Count; ji < jn && ji < R.junk.Count; ji++) { var d = R.junk[ji]; if (d.dead) continue; double px = d.x - x, py = d.y - y, t = (px * ux + py * uy) / (ux * ux + uy * uy); if (t < 0 || t > L) continue; double qx = px - ux * t, qy = py - uy * t; if (qx * qx + qy * qy > 196) continue; Hit(d, Math.Max(1, RoundP(Pow * 2)), 2, false); }
                Emit(SwEv.Laser, x, y, 5, 4, null, x + ux * L, y + uy * L);
            }
        }
        // ★ 운석 호출 — 빽빽한 곳에 떨어져 크게 터진다
        void Meteor()
        {
            var r = R; Junk c = null; int bn = -1;
            for (int t = 0; t < 16 && r.junk.Count > 0; t++) { var q = r.junk[rng.Next(r.junk.Count)]; if (q.dead) continue; int n = 0; foreach (var d in r.junk) if (!d.dead && (d.x - q.x) * (d.x - q.x) + (d.y - q.y) * (d.y - q.y) < 6400) n++; if (n > bn) { bn = n; c = q; } }
            if (c == null) return;
            Emit(SwEv.Meteor, c.x, c.y);
            r.pend.Add(new Blast { x = c.x, y = c.y, t = 0.55, R = 80 });   // 한 방만 크게 (번지지 않는다 — 번지면 옛 연쇄처럼 판을 다 먹는다)
            if (M.flags == null || !M.flags.Contains("meteor1")) { M.flags.Add("meteor1"); AddNews(null, "청소선이 부른 운석, 궤도를 쓸고 지나가", "궤도 청소부가 작은 운석을 끌어와 잔해 더미에 떨어뜨렸다. 지구 연료공사는 「보험 청구가 늘 것」이라며 울상."); }
            if (Mk != null) Mk.GameEvent("운석 낙하 — 궤도 연료 수송로 마비", "청소선이 부른 운석 여파로 연료 수송이 늦어진다.", null, new[] { "fuel" }, 0.06f);
        }


        // ───────────────────────── ⚔ 무기 여섯 더 (09-24 설계서 4단계) — 진공 청소기 · 기뢰 · 냉동 빔 · 분열탄 · 자석 펄스 · 레일건
        public static readonly double[] FireRate = { 1, 0.25, 1, 0.25, 2.2, 0.25, 1.6, 3, 3.2 };   // 무기마다 쏘는 간격 (Gap 배수)
        double vacMul = 1;                                                   // 청소기로 부순 것 — 값이 더 붙는다

        // 🌀 진공 청소기 — 청소선에서 부채꼴로 빨아들인다 (작은 것 떼에 강함 · 큰 것은 못 삼킨다)
        // 🌀 청소기 — 조준점에 소용돌이 원. 원 안의 것이 가운데로 빨려 들며 부서진다, 가운데 닿으면 흡수 (09-24 사장님 「원 영역 그리고 그 부분이 흡수되게」)
        int We(string w) => Lv("w_" + w + "_e");                            // ◇ 무기 특화 단계
        public double VacR => (46 + 0.8 * ClawR) * (Lv("w_vac_u") >= 1 ? 1.3 : 1) * (1 + 0.2 * We("vac"));
        void Vac()
        {
            var r = R; int u = Lv("w_vac_u"); bool awk = Lv("w_vac_a") > 0;
            double cx = r.ax, cy = r.ay, rad = VacR, per = Pow * 0.45;
            bool any = false;
            vacMul = (u >= 2 ? 1.6 : 1.3) + 0.2 * We("vac");
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead || Types[d.k].big) continue;
                double dx = cx - d.x, dy = cy - d.y, dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > rad + Types[d.k].r) continue;
                if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                d.capT = 0.5; d.vx += dx * 1.2; d.vy += dy * 1.2;                     // 가운데로 끌려온다
                int dmg = RoundP(per * (dist < 16 ? 4 : 1)); if (dmg <= 0) continue;   // 가운데 닿으면 흡수
                bool was = d.dead; Hit(d, dmg, 0, true); any = true;
                if (!was && d.dead && awk) { r.vacAmmo++; if (r.vacAmmo >= 25) { r.vacAmmo = 0; r.pend.Add(new Blast { x = cx, y = cy, t = 0.25, R = 70, w = true }); Emit(SwEv.Bolt, ShipX, ShipY, 0, 1, null, cx, cy); Emit(SwEv.Pop, cx, cy - 20, 0, 3, "압축 고철탄!"); } }
            }
            vacMul = 1;
            Emit(SwEv.Vac, cx, cy, rad, 0);
            if (any) OnHit(0.25);
        }

        // 💣 기뢰 — 조준 자리에 깔아 둔다. 궤도를 돌던 잔해가 지나가면 쾅 (무기 폭발이라 번질 수 있다)
        void MineLay()
        {
            var r = R; int u = Lv("w_mine_u");
            int cap = 3 + (u >= 1 ? 2 : 0) + We("mine");
            if (r.mines.Count >= cap) r.mines.RemoveAt(0);
            r.mines.Add(new Blast { x = r.ax, y = r.ay, t = 0.4, R = (45 + 0.25 * ClawR) * (u >= 2 ? 1.4 : 1) * (1 + 0.15 * We("mine")) });
            Emit(SwEv.Ring, r.ax, r.ay, 18, 3);
        }
        void Mines(double dt)
        {
            var r = R; if (r.mines.Count == 0) return;
            bool awk = Lv("w_mine_a") > 0;
            for (int i = r.mines.Count - 1; i >= 0; i--)
            {
                var m = r.mines[i];
                if (m.t > 0) { m.t -= dt; continue; }
                bool boom = false;
                foreach (var d in r.junk) { if (d.dead || d.fade < 0.35) continue; double dx = d.x - m.x, dy = d.y - m.y; if (dx * dx + dy * dy < 18 * 18) { boom = true; break; } }
                if (!boom) continue;
                r.mines.RemoveAt(i);
                r.pend.Add(new Blast { x = m.x, y = m.y, t = 0.01, R = m.R, w = true });
                OnHit(1);
            }
            // 각성 — 기뢰끼리 레이저 울타리 (지나가는 것을 태운다)
            if (awk && r.mines.Count >= 2)
            {
                r.fenceT -= dt; if (r.fenceT > 0) return; r.fenceT = 0.2;
                for (int i = 0; i + 1 < r.mines.Count; i++)
                {
                    var a = r.mines[i]; var b = r.mines[i + 1];
                    double ux = b.x - a.x, uy = b.y - a.y, L = Math.Sqrt(ux * ux + uy * uy); if (L < 1) continue; ux /= L; uy /= L;
                    for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
                    {
                        var d = r.junk[ji]; if (d.dead) continue;
                        double px = d.x - a.x, py = d.y - a.y, t = px * ux + py * uy; if (t < 0 || t > L) continue;
                        if (Math.Abs(px * uy - py * ux) > 8 + Types[d.k].r) continue;
                        Hit(d, Math.Max(1, RoundP(Pow * 0.5)), 0, true);
                    }
                    Emit(SwEv.Laser, a.x, a.y, 3, 16, null, b.x, b.y);
                }
            }
        }

        // ❄ 냉동 빔 — 맞은 것이 얼어 멈춘다. 언 것은 무엇에 맞든 두 배로 아프다. 부서지면 산산조각
        // ❄ 냉동 빔 — 빔은 조준점까지, 거기서 서리 원이 퍼진다. 원 안이 언다 (09-24 「얼음은 좀 이상해」 — 한 줄 전체가 얼던 것을 원으로)
        public double FrzR => (34 + 0.5 * ClawR) * (Lv("w_frz_u") >= 1 ? 1.3 : 1) * (1 + 0.2 * We("frz"));
        void Freeze()
        {
            var r = R; int u = Lv("w_frz_u");
            double sx = ShipX, sy = ShipY, cx = r.ax, cy = r.ay, rad = FrzR;
            bool any = false;
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead) continue;
                double dx = d.x - cx, dy = d.y - cy; if (dx * dx + dy * dy > (rad + Types[d.k].r) * (rad + Types[d.k].r)) continue;
                bool fresh = d.frz <= 0;
                d.frz = (u >= 1 ? 4 : 2.5) + 0.5 * We("frz"); any = true;
                if (fresh) Emit(SwEv.Shatter, d.x, d.y, 0, 1);
                int dmg = RoundP(Pow * 0.12); if (dmg > 0) Hit(d, dmg, 0, true);
            }
            Emit(SwEv.Laser, sx, sy, rad, 32 | 64, null, cx, cy);
            if (any) OnHit(0.25);
        }
        public double FrzMul => Lv("w_frz_u") >= 2 ? 2.5 : 2;
        void Shatter(Junk d)                                                  // 언 것이 부서질 때 — 옆을 친다 (각성: 옆도 얼린다)
        {
            bool awk = Lv("w_frz_a") > 0;
            for (int ji = 0, jn = R.junk.Count; ji < jn && ji < R.junk.Count; ji++)
            {
                var q = R.junk[ji]; if (q.dead || q == d) continue;
                double dx = q.x - d.x, dy = q.y - d.y; if (dx * dx + dy * dy > 34 * 34) continue;
                if (awk && q.frz <= 0) q.frz = 2;
                q.hp -= Math.Max(1, RoundP(Pow * 0.6)); q.hit = 0.12; if (q.hp <= 0) Kill(q, 0, 1);
            }
            Emit(SwEv.Shatter, d.x, d.y, 0, 2);
        }

        // 🎆 분열탄 — 조준점에 떨어져 터지며 파편 여섯 (강화: 아홉 · 가장 가까운 것을 노림 · 각성: 파편이 한 번 더)
        void Cluster()
        {
            var r = R; int u = Lv("w_clus_u"); bool awk = Lv("w_clus_a") > 0;
            double ax = r.ax, ay = r.ay;
            Emit(SwEv.Shell, ShipX, ShipY, 0, 0, null, ax, ay);
            r.pend.Add(new Blast { x = ax, y = ay, t = 0.35, R = 42 + 0.2 * ClawR, w = true });
            int n = 6 + (u >= 1 ? 3 : 0) + 2 * We("clus");
            var targets = new List<Junk>();
            if (u >= 2) { foreach (var d in r.junk) if (!d.dead) { double dx = d.x - ax, dy = d.y - ay; if (dx * dx + dy * dy < 150 * 150) targets.Add(d); } }
            for (int k = 0; k < n; k++)
            {
                double fx, fy;
                if (u >= 2 && targets.Count > 0) { var t = targets[rng.Next(targets.Count)]; fx = t.x; fy = t.y; }
                else { double a = k * Math.PI * 2 / n + Rnd(-0.2, 0.2), d = Rnd(55, 110); fx = ax + Math.Cos(a) * d; fy = ay + Math.Sin(a) * d * Tilt; }
                double t0 = 0.5 + k * 0.04;
                r.pend.Add(new Blast { x = fx, y = fy, t = t0, R = 22, w = true });
                if (awk) for (int j = 0; j < 3; j++) r.pend.Add(new Blast { x = fx + Rnd(-35, 35), y = fy + Rnd(-25, 25), t = t0 + 0.18 + j * 0.03, R = 14, w = true });
            }
            OnHit(1);
        }

        // 🧲 자석 펄스 — 주변을 한 점으로 끌어모은 뒤 쾅 (강화: 넓게 · 모인 만큼 크게 · 각성: 모인 자리에 블랙홀)
        void MagPulse()
        {
            var r = R; int u = Lv("w_mag_u"); bool awk = Lv("w_mag_a") > 0;
            double cx = r.ax, cy = r.ay, Rr = (110 + 0.4 * ClawR) * (u >= 1 ? 1.4 : 1) * (1 + 0.2 * We("mag"));
            int n = 0;
            foreach (var d in r.junk)
            {
                if (d.dead || Types[d.k].big) continue;
                double dx = cx - d.x, dy = cy - d.y; if (dx * dx + dy * dy > Rr * Rr) continue;
                if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                d.vx = dx * 1.9; d.vy = dy * 1.9; d.capT = 1.1; n++;
            }
            Emit(SwEv.Ring, cx, cy, Rr, 2);
            r.pend.Add(new Blast { x = cx, y = cy, t = 0.55, R = 50 + (u >= 2 ? Math.Min(60, n * 1.5) : 0), w = true });
            if (awk && !r.holding) { r.magHole = 0.6; r.magX = cx; r.magY = cy; }
            OnHit(1);
        }

        // ⚡ 레일건 — 모았다가 한 줄로 관통. 엄청 세다 · 장갑도 뚫는다 (강화: 빨리 모음 · 뚫을수록 세짐 · 각성: 튕겨 한 번 더)
        void Rail()
        {
            var r = R; int u = Lv("w_rail_u"); bool awk = Lv("w_rail_a") > 0;
            double sx = ShipX, sy = ShipY, dx0 = r.ax - sx, dy0 = r.ay - sy, L0 = Math.Sqrt(dx0 * dx0 + dy0 * dy0);
            if (L0 < 1) return;
            double ux = dx0 / L0, uy = dy0 / L0, len = L0 + 420 + 120 * We("rail");
            RailLine(sx, sy, ux, uy, len, u >= 2);
            if (awk || We("rail") >= 3) { double ex = sx + ux * (L0 + 40), ey = sy + uy * (L0 + 40), a = Math.Atan2(uy, ux) + Math.PI + Rnd(-0.6, 0.6); RailLine(ex, ey, Math.Cos(a), Math.Sin(a), 360, u >= 2); }
            OnHit(1);
        }
        void RailLine(double sx, double sy, double ux, double uy, double len, bool grow)
        {
            var r = R; double dmg = Pow * 6 * (Rnd() < Crit ? CritX : 1);
            var hit = new List<Junk>();
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead) continue;
                double px = d.x - sx, py = d.y - sy, t = px * ux + py * uy; if (t < 0 || t > len) continue;
                if (Math.Abs(px * uy - py * ux) > 10 + Types[d.k].r) continue;
                hit.Add(d);
            }
            hit.Sort((a, b) => ((a.x - sx) * ux + (a.y - sy) * uy).CompareTo((b.x - sx) * ux + (b.y - sy) * uy));
            foreach (var d in hit) { pierce = true; Hit(d, Math.Max(1, (int)Math.Round(dmg)), 0, true); pierce = false; if (grow) dmg *= 1.15; }
            Emit(SwEv.Rail, sx, sy, hit.Count, 0, null, sx + ux * len, sy + uy * len);
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
            // 청소선 — 조준 방향 쪽 바깥 궤도로 (무기는 여기서 나간다)
            {
                double want = Math.Atan2((r.ay - EY) / Tilt, r.ax - EX), da = want - r.shipA;
                while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI;
                r.shipA += da * Math.Min(1, dt * 3);
            }
            if (r.clickCd > 0) r.clickCd -= dt;
            for (int cw = 1; cw <= 5; cw += 2)                                  // 이어서 쏘는 확률 효과 (레이저 1 · 청소기 3 · 냉동 5)
                if (r.chan[cw] > 0) { r.chan[cw] -= dt; r.chanNext[cw] -= dt; if (r.chanNext[cw] <= 0) { r.chanNext[cw] = Gap * 0.25; Emit(SwEv.Proc, r.chanX[cw], r.chanY[cw], cw, 2); FireAt(cw, r.chanX[cw], r.chanY[cw]); } }   // kk 2 = 이어 쏘기 (포대만)
            VolleyTick(dt);
            if (r.next > 0) return;
            double over = Lv("c_over") > 0 && r.fuel < 5 ? 0.5 : 1;
            r.next = Gap * over * Rate(0);                                    // 기본 공격 간격
            Fire(0); Procs();
            if (Rnd() < 0.1 * Lv("c_double") + Part("dbl")) { Fire(0); Procs(); }
        }
        // 👆 수동 사격 — 누를 때마다 조준점에 한 방 더 (위력 ×1.5 · 0.2초 간격). 무기 발동 · 전탄 게이지도 굴러간다 (09-24 사장님 37번 「클릭에 요소」)
        public bool ClickShot(double x, double y)
        {
            var r = R; if (r == null || r.over || r.clickCd > 0 || r.fuel <= 0) return false;
            r.clickCd = 0.2; double ox = r.ax, oy = r.ay; r.ax = x; r.ay = y;
            clickMul = 1.5; Fire(0); clickMul = 1; Procs();
            r.ax = ox; r.ay = oy;
            return true;
        }
        double clickMul = 1;
        void Fire2(double dt)
        {
            var r = R; int w2 = S.weapon2;
            if (Lv("w_slot2") <= 0 || w2 < 0 || w2 == Weapon || !WeaponOwned(w2)) return;
            r.next2 -= dt; if (r.next2 > 0) return;
            r.next2 = Gap * 2 * Rate(w2);                                       // 보조 무기 — 절반 빠르기
            Fire(w2);
        }
        // 🔫 조종실 포구 — 무기마다 모양 · 개수가 다르다 (09-24 사장님 「조종선에서 쏜다」 · 시안 DbsvFEEy1K5ddbZsM61B2y)
        // 시안 화면(1280×720) 좌표로 (x, y, 포신 길이) 셋씩. 화면 아래 가운데 = (640, 720)
        public static readonly double[][] Mounts = {
            new double[] { 640, 654, 52 },                                                   // 집게 빔 — 큰 집게 포 하나
            new double[] { 598, 650, 74, 682, 650, 74 },                                     // 레이저 — 가는 장포신 둘 (선체 안쪽)
            new double[] { 640, 586, 0 },                                                    // 번개 — 테슬라 코일 탑
            new double[] { 640, 660, 50 },                                                   // 청소기 — 넓은 흡입구
            new double[] { 622, 640, 24, 658, 640, 24, 622, 664, 24, 658, 664, 24 },         // 기뢰 — 박격포 넷
            new double[] { 640, 654, 58 },                                                   // 냉동 빔 — 냉각 노즐
            PodMounts(),                                                                     // 분열탄 — 로켓 포드 열
            new double[] { 640, 656, 60 },                                                   // 자석 — 말굽
            new double[] { 640, 660, 108 },                                                  // 레일건 — 긴 레일
        };
        static double[] PodMounts() { var m = new List<double>(); for (int r = 0; r < 2; r++) for (int c = 0; c < 5; c++) { m.Add(596 + c * 22); m.Add(630 + r * 22); m.Add(9); } return m.ToArray(); }
        public double ViewHalf => Math.Min(7, Math.Max(3.8, Bo * 0.0145)) * 1.1;   // SweepGame 카메라 크기와 같은 식 (월드 단위, 화면 절반 높이) — 계기판 몫 10% 물림
        public const double TurS = 1.4;                                        // 포구 크기 (시안보다 1.4배 — 09-24 작아 보였다)
        public double MountK => 100 * ViewHalf / 720 * TurS;                   // 시안 1px → 시뮬 px
        public int MountCount(int w) => Mounts[Math.Min(w, Mounts.Length - 1)].Length / 3;
        public void MountPx(int w, int i, out double bx, out double by, out double tx, out double ty)
        {
            var m = Mounts[Math.Min(w, Mounts.Length - 1)]; i %= m.Length / 3; double k = MountK;
            bx = EX + (m[i * 3] - 640) * k; by = EY + ViewHalf * 1.1 * 50 - (720 - m[i * 3 + 1]) * k;   // 화면 아래 가운데 기준으로 TurS배   // 카메라가 0.1 내려가 있다
            double L = m[i * 3 + 2] * k, dx = (R != null ? R.ax : EX) - bx, dy = (R != null ? R.ay : EY) - by, d = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
            tx = bx + dx / d * L; ty = by + dy / d * L;
        }
        public double ShipX { get { MountPx(Weapon, 0, out _, out _, out var x, out _); return x; } }
        public double ShipY { get { MountPx(Weapon, 0, out _, out _, out _, out var y); return y; } }
        void Fire(int w) { switch (w) { case 1: Laser(); break; case 2: Chain(); break; case 3: Vac(); break; case 4: MineLay(); break; case 5: Freeze(); break; case 6: Cluster(); break; case 7: MagPulse(); break; case 8: Rail(); break; default: Strike(); break; } }
        double Rate(int w) => FireRate[Math.Min(w, FireRate.Length - 1)] * (w == 8 && Lv("w_rail_u") >= 1 ? 0.75 : 1);
        bool pierce;                                                       // 레이저 각성 — 장갑판 한도 무시

        // 🔴 레이저 — 청소선에서 조준점 너머까지. 네 배 자주 · 한 번은 화력의 0.3 (소수는 확률로)
        // 🔴 레이저 — 빔은 조준점에서 멈추고 끝의 작은 원만 태운다 (09-24 사장님: 화면 끝까지 한 줄로 쓸던 것이 말이 안 됐다)
        public double LaserR => (12 + 0.25 * Math.Max(ClawR, PickR * 0.6)) * (Lv("w_laser_u") >= 1 ? 1.4 : 1) * (Lv("w_laser_a") > 0 ? 1.5 : 1) * (1 + 0.2 * We("laser"));
        void Laser()
        {
            var r = R;
            int u = Lv("w_laser_u"); bool awk = Lv("w_laser_a") > 0;
            double sx = ShipX, sy = ShipY, cx = r.ax, cy = r.ay, rad = LaserR;
            bool crit = Rnd() < Crit, any = false;
            double per = Pow * 0.36 * (u >= 2 ? r.heat : 1) * (crit ? CritX : 1) * (1 + 0.15 * We("laser"));
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)   // 부서지며 조각이 새로 붙어도 괜찮게 (번호로 돈다)
            {
                var d = r.junk[ji];
                if (d.dead) continue;
                double dx = d.x - cx, dy = d.y - cy, rr = rad + Types[d.k].r;
                if (dx * dx + dy * dy > rr * rr) continue;
                int dmg = (int)per + (Rnd() < per - (int)per ? 1 : 0);
                if (dmg <= 0) continue;
                any = true; pierce = awk; Hit(d, dmg, 0, true); pierce = false;
            }
            Emit(SwEv.Laser, sx, sy, rad, (crit ? 1 : 0) + (awk ? 2 : 0) + 64, null, cx, cy);
            r.heat = any ? Math.Min(1.8, r.heat + 0.04) : 1;
            if (any && We("laser") >= 3 && Rnd() < 0.25) r.pend.Add(new Blast { x = cx, y = cy, t = 0.05, R = rad * 1.4, w = true });   // ◇ 3단계 — 태운 자리가 터진다
            if (any) OnHit(0.25);
        }

        // ⚡ 번개 — 조준점 근처 하나를 치고 가까운 것으로 튄다
        void Chain()
        {
            var r = R;
            int u = Lv("w_chain_u"); bool awk = Lv("w_chain_a") > 0;
            double reach0 = 70 + 0.5 * ClawR, hop = 115 + 0.6 * ClawR;
            Junk cur = null; double bd = reach0 * reach0;
            foreach (var d in r.junk) { if (d.dead) continue; double dx = d.x - r.ax, dy = d.y - r.ay, dd = dx * dx + dy * dy; if (dd < bd) { bd = dd; cur = d; } }
            if (cur == null) { Emit(SwEv.Strike, r.ax, r.ay, PickR, 0); return; }
            bool crit = Rnd() < Crit;
            int jumps = 5 + (u >= 1 ? 2 : 0) + 2 * We("chain");
            double dmg = Pow * 1.3 * (crit ? CritX : 1);
            double px = ShipX, py = ShipY;
            var hit = new List<Junk>();
            for (int j = 0; j <= jumps && cur != null; j++)
            {
                hit.Add(cur);
                Emit(SwEv.Bolt, px, py, j, crit ? 1 : 0, null, cur.x, cur.y);
                px = cur.x; py = cur.y;
                Hit(cur, Math.Max(1, (int)Math.Round(dmg)), 0, true);
                if (awk && r.chain < ChainMax) r.pend.Add(new Blast { x = px, y = py, t = 0.05 + 0.03 * j, R = 30, w = true });
                if (u >= 2) dmg *= 1.2;
                Junk nx = null; double nd = hop * hop;
                foreach (var d in r.junk) { if (d.dead || hit.Contains(d)) continue; double dx = d.x - px, dy = d.y - py, dd = dx * dx + dy * dy; if (dd < nd) { nd = dd; nx = d; } }
                cur = nx;
            }
            if (crit) Emit(SwEv.Crit, r.ax, r.ay - 20);
            OnHit(1);
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
                if (best != null) { Hit(best, Math.Max(1, RoundP(Pow * (c1 ? CritX : 1))), 0, true); OnHit(1); }
                Emit(SwEv.Strike, r.ax, r.ay, PickR, best != null ? 1 : 0);
                if (c1) Emit(SwEv.Crit, r.ax, r.ay - 20);
                return;
            }
            double R0 = ClawR; bool hit = false, crit = Rnd() < Crit; int dmg = Math.Max(1, RoundP(Pow * (crit ? CritX : 1)));
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
            if (hit) OnHit(1);
        }
        void HoleRoll() { if (Rnd() < HoleChance) OpenHole(); }
        /// <summary>무기가 맞았다 — 블랙홀 · ★ 내부자 거래 (share = 레이저처럼 자주 쏘는 무기는 몫을 나눈다)</summary>
        void OnHit(double share)
        {
            if (Rnd() < share) HoleRoll();
            var r = R;
            if (Lv("q_insider") > 0 && r.insiderN < 15 && Mk != null && StockOpen && Rnd() < 0.005 * share)
            {
                var own = new List<int>(); for (int i = 0; i < Mk.M.st.Count; i++) if (Mk.M.st[i].shares > 0) own.Add(i);
                if (own.Count > 0) { int i = own[rng.Next(own.Count)]; Mk.M.st[i].price *= 1.01; r.insiderN++; Emit(SwEv.Pop, ShipX, ShipY - 16, 0, 4, Market.Defs[i].name + " +1%"); }
            }
        }
        /// <summary>블랙홀이 저절로 열린다 — 조준점에서 3초 빨아들이고 터진다 (하나씩만)</summary>
        void OpenHole()
        {
            var r = R;
            if (r.holding || r.fuel <= 0 || (!BombsOn && !r.clean)) return;
            r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.hx = r.ax; r.hy = r.ay; r.shots++;   // Release 가 하나 뺀다
            Emit(SwEv.SkillReady, r.ax, r.ay, 1);
        }

        void Hit(Junk d, int dmg, int src, bool spread)
        {
            if (d.dead) return;
            if (d.att == Att.Armor && src == 0 && !pierce) dmg = Math.Min(dmg, 1);
            if (d.frz > 0) dmg = (int)Math.Round(dmg * FrzMul);                        // 언 것은 두 배
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
                    Hit(best, grade * (Lv("x_arm_drone") > 0 ? 2 : 1), 1, false);
                    if (Lv("x_arm_drone") > 0) OnHit(0.3);
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

        bool blastW; int shatterDepth;
        void Kill(Junk d, int src, double mult)
        {
            if (d.dead) return;
            d.dead = true;
            if (d.frz > 0 && Lv("w_frz") > 0 && shatterDepth < 40) { shatterDepth++; Shatter(d); shatterDepth--; }
            var r = R;
            r.broke++; M.broken++;
            if (r.clean) r.cleanKills++;
            switch (d.k) { case Vault: r.cVault++; break; case Rocket: r.cFuel++; break; case Chip: r.cChip++; break; case Sat: r.cSat++; break; case Tank: r.cTank++; break; case Big: r.cBig++; break; }
            if (d.k == Big) Record("big1", "30년 떠다닌 우주선 잔해, 드디어 사라져", "큰 잔해 하나가 궤도에서 사라졌다. 청소업계는 「보험료가 아깝지 않다」고 했다.");
            // 연쇄 = 잇달아 부순 수 (어떤 무기든 · 1.6초 안에 다음 것을 부수면 이어진다)
            {
                r.chain++; r.chainT = 1.6;
                r.chainBest = Math.Max(r.chainBest, r.chain); M.bestChain = Math.Max(M.bestChain, r.chain);
                int tier = r.chain >= 200 ? 4 : r.chain >= 80 ? 3 : r.chain >= 30 ? 2 : r.chain >= 10 ? 1 : 0;
                if (tier > r.tier) { r.tier = tier; Emit(SwEv.Tier, d.x, d.y, r.chain, tier); }
                if (r.chain == 30 || r.chain == 80 || r.chain == 200) Record("chain" + r.chain, "민간 청소선, 잔해 " + r.chain + "개 연쇄 파괴" + (r.chain >= 200 ? " — 지상에서도 보였다" : ""), "폭발이 폭발을 불렀다. 궤도일보 관측팀은 「케슬러 연쇄를 일부러 일으킨 첫 사례」라고 적었다.");
                if (src == 2 && blastW && r.chain < ChainMax && Rnd() < ChainP) r.pend.Add(new Blast { x = d.x, y = d.y, t = Rnd(0.06, 0.14), R = 40, w = true });   // 무기 폭발만 번진다
            }
            // ★ 운석 호출 — 80개마다
            if (src == 0) r.weaponKills++;
            if (Lv("q_meteor") > 0 && r.meteors < 3 && r.weaponKills >= r.meteorAt) { r.meteorAt += 80; r.meteors++; Meteor(); }   // 무기로 부순 것만 센다 · 한 판 4번 (운석이 운석을 부르지 않게)
            // ★ 관광 명소 — 한 판에 연쇄 100
            if (Lv("q_tour") > 0 && !r.tourDone && r.chain >= 100) { r.tourDone = true; Emit(SwEv.Pop, EX, EY - 120, 0, 4, "관광객이 몰려든다!"); Emit(SwEv.Tourist, 0, 0); if (Mk != null) Mk.GameEvent("토성 고리 관광객, 청소선 구경 러시", "궤도 청소부의 연쇄 파괴를 보려는 관광선이 줄을 섰다.", new[] { "sat" }, null, 0.12f); }
            double v = Types[d.k].val * ValMult * mult * vacMul;
            if (d.att == Att.Gold) { v *= 3; if (r.goldN < 2) { r.goldN++; if (S.scratchRun != S.runs) { S.scratchRun = S.runs; S.scratchN = 0; } S.scratchN--; Emit(SwEv.Pop, d.x, d.y - 14, 0, 4, "황금! 복권 +1"); } else Emit(SwEv.Pop, d.x, d.y - 14, 0, 4, "황금 ×3"); }   // 복권은 한 판 2장까지
            if (d.att == Att.Rock)
            {
                if (Rnd() < 0.4) { S.keys++; Emit(SwEv.Pop, d.x, d.y - 14, 0, 5, "열쇠 +1!"); AddNews(null, "떠돌이 소행성 속에서 이상한 열쇠가 나왔다", "청소선이 부순 소행성 속에서 반짝이는 금속 조각이 발견됐다. 케슬러 금융은 「우리 것이 아니다」라고 했다."); }
                else { double cash = ShopBase * 0.4; S.cash += cash; Emit(SwEv.Pop, d.x, d.y - 14, 0, 4, "돈 뭉치 +" + Math.Round(cash).ToString("N0")); }
            }
            if (d.att == Att.Pouch) v *= 2;
            if (src == 1) v *= DroneMag;
            v *= 1 + Math.Min(r.chain, 200 + Part("combo")) / 200.0;                    // 잇달아 부수면 값이 더 붙는다 (최대 ×2 · 어떤 무기든 — 연쇄 폭발을 무기 특성으로 옮긴 만큼)
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
                M.won = true; M.cleanReady = false; M.legend++;                   // ★ 전설 경력
                M.history.Add(new PastCompany { company = M.company, bill = S.bill, runs = S.runs, minutes = (M.playSeconds - S.startedAt) / 60, won = true });
                AddNews("clean");
                Emit(SwEv.Won);
                return;
            }
            if (M.endless)
            {   // ∞ 무한 궤도 — 판이 끝날 때마다 한 층 아래로
                M.depth++; if (M.depth > M.bestDepth) M.bestDepth = M.depth;
                if (M.depth % 5 == 0) { S.keys++; AddNews(null, "무한 궤도 " + M.depth + "층 — 심연 보급", "깊은 궤도에서 낡은 열쇠 하나가 떠올랐다. (열쇠 +1)"); }
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
            if (ShopOpen) { RollShop(); S.freeRoll = true; }                  // 🔩 가게 진열이 바뀐다 · 공짜 새로고침 한 번
            if (Lv("q_front") > 0 && Mk != null && StockOpen) { S.front1 = rng.Next(Market.NewsBook.Length); do S.front2 = rng.Next(Market.NewsBook.Length); while (S.front2 == S.front1); }
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
