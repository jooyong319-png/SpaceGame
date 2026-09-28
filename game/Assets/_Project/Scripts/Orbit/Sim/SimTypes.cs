using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🧩 규칙에서 쓰는 데이터 모양 — 잔해 · 저장(회사 · 경력) · 한 판 · 사건 (09-28 SweepSim.cs 에서 나눔)

    public enum Att { None, FuelPod, Pouch, Beacon, Magnet, Det, Ice, Cable, Armor, BBox, Tag, Gold, Rock }

    public class Junk
    {
        public int id, k, hp, max, sp = -1;                                   // sp = 종 (모습 · 등급) — SweepSim.Spc
        public Att att;
        public double a, rr, ws, x, y, vx, vy, capT, fade, hit, rot, vr, tr, frz;   // frz = 얼어 있는 시간 (냉동 빔)
        public bool free, dead, convoy;
        public Junk link1, link2;
        public int sig, grp; public double sigCd;                               // 🪐 행성 특성 (SweepTraits.cs) — 특성 번호 · 무리 · 광석 흘리기 간격
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
        public int shopSale = -1, nFuel, nDmg, nVal; public bool freeRoll = true;
        public double runAvg; public int rolls;
        public double gateFrac = 1, gateMax, dmgAvg; public int gateNext;   // gateMax = 이 관문 체력(처음 뜰 때 정한다) · dmgAvg = 판당 준 피해 평균                           // 🛰 관문 — 남은 체력(판을 넘어 남는다) · 부순 판이 끝나면 열 행성 (09-26)                                   // 💰 최근 판당 수입(가게 값 기준) · 이번 판 새로고침 횟수 (09-26)
        public int layout;                                               // 칸 배치 판 — 2 = 새 칸 12개가 맨 뒤 (09-25). 0 이면 옮겨 준다   // 🔩 가게 v2 — 오늘의 반값 칸 · 판마다 공짜 새로고침 · 🧃 소모품(다음 판)                             // 가게 진열 (Parts.Key = 열쇠)                                                   // ⚔ 장착한 무기 (0 집게 빔 · 1 레이저 · 2 번개)
        public double cash, billAmount = -1, creditPending, startedAt, debt;   // debt = 갚아야 할 빚 (대출 × 배수)
        public int runs, orbit, bill, billDue = 5, overRuns, contract = -1;
        public bool overdue, rerolled;
        public int[] lv = new int[SweepSim.NodeCount];
        public int planets = 1;                                          // 연 행성 (비트) — 지구는 늘
        public MarketState market;                                       // 📈 궤도 증권 — 회사마다 (파산하면 새 장)
        public int scratchRun = -1, scratchN;                            // 🎟 즉석 복권 — 출동마다 3장
        public int freeTix;                                              // 🎟 공짜 표 (황금 잔해 · 복권 묶음 — 출동해도 남는다 · 09-27)
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
        public int[] career = new int[SweepSim.CareerCount];              // 옛 경력 (09-26 영구 강화로 바꿨다 — 불러올 때 신용으로 돌려준다)
        public int[] up = new int[SweepSim.UpCount];                      // 🛠 격납고 영구 강화 (09-26 사장님 「뱀서처럼」)
        public int ship, shipsOwned = 1;                                   // 🚀 고른 청소선 · 가진 배 (비트)
        public int company = 1, bankrupt, loans, bestChain, bestPack, totalRuns, scoops;
        public int legend, depth, bestDepth; public bool endless;          // ★ 전설 경력(청산마다) · ∞ 무한 궤도 층 (09-24 사장님 12 · 26번)
        public double bestAuc;                                           // 고철 경매 최고 배수
        public List<string> perm = new List<string>();                    // 🔑 ◆ 핵심 칸 — 파산해도 남는다 (09-24 사장님 「열쇠가 떡벽」 → 파산하면 강해진다)
        public bool won, careerOpen, cleanReady;
        public List<NewsItem> news = new List<NewsItem>();
        public List<string> flags = new List<string>();
        public List<PastCompany> history = new List<PastCompany>();
    }

    public class Blast { public double x, y, t, R, dk; public bool w, red; public int gen; public int wid = -1; }   // wid = 피해를 세어 줄 곳 (DmgCat) — 기뢰 · 분열탄처럼 늦게 터져도 제 무기로     // w = 무기가 낸 폭발 (이것만 또 번진다 — 09-24 사장님 「연쇄 반응도 무기 특성으로」)
    public class Drone { public double a, cd, x, y; }
    public class Missile { public double x, y, vx, vy, life; public Junk tg; }   // 🚀 미사일선 (09-27)
    public class Orb { public double x, y, tx, ty, vx, vy, t, zap; public bool there; }   // ⚡ 전격선 구체 (09-27)
    public class Pod { public int kind; public double t, x, y, a, rr; public bool up, got; }     // 지구 보급 — kind 0 연료 · 1 폭탄

    public class SweepRun
    {
        public readonly double[] chan = new double[9], chanNext = new double[9], chanX = new double[9], chanY = new double[9];   // 이어서 쏘는 확률 효과 · 그 무기의 목표
        public readonly double[] dmgBy = new double[SweepSim.DmgCatN];     // 📊 이번 판 피해 — 곳별 (결과 화면 원그래프 · 09-27)
        public double volley, volleyT, volleyNext; public int consDmg, consVal;   // 🧃 이번 판 소모품                          // 🚀 전탄 발사 게이지 · 퍼붓는 중
        public double shipA = -1.57, heat = 1, next2, idleT, rockT = -1, fenceT, magHole, magX, magY; public int vacAmmo; public readonly List<Blast> mines = new List<Blast>(); public bool twin, lazyDone, tourDone; public int insiderN, meteorAt = 80, meteors, weaponKills, goldN;                            // 청소선 — 궤도 바깥에서 조준 방향으로 따라온다 · 레이저 열
        public double hx, hy, holeCd, clickCd, fuelGot, refill, fuel, max, t, next = 0.3, endT, formT = 9, rushT, rushX, rushY, chainT, holdT, ax = 480, ay = 310, refillT;
        public double ev1T = -1, ev2T = -1, stormT; public int ev1 = -1, ev2 = -1, stormLeft; public bool ev1Warn, ev2Warn, collector;
        public int shots, maxShots, chain, chainBest, packBest, broke, tier, idc;
        public bool over, holding, clean, contractOk;
        public double earnClaw, earnDrone, earnBlast, cut, toBill, bonus, interest;
        public string contractText; public int contractProg, contractTarget;     // 지난 판 의뢰 — 결산에 성공/실패를 보여 준다
        public int cVault, cFuel, cChip, cSat, cTank, cBig, cTag;
        public int cleanKills, cleanGoal = 1; public double dmgDone, lastShot = -9;
        public int tapCombo; public double lastPress = -9;
        public double chainBank, chainX = 480, chainY = 300;                     // 💰 연쇄 보너스 — 연쇄 동안 모았다가 끝날 때 한꺼번에 (09-27)                   // ★ 연타 장인 (09-27) — 0.3초 안에 다시 누른 수
        public readonly List<Junk> junk = new List<Junk>();
        public readonly List<Junk> packed = new List<Junk>();
        public readonly List<Blast> pend = new List<Blast>();
        public readonly List<Drone> drones = new List<Drone>();
        public readonly List<Pod> pods = new List<Pod>();
        public readonly List<Orb> orbs = new List<Orb>();                        // ⚡ 전격선 구체
        public readonly List<Missile> missiles = new List<Missile>();            // 🚀 미사일선
        public double sigT, spotA, gustT, gustA, gustLeft; public bool roverUp; public int grpId, spotEaten; public Junk comet;   // 🪐 행성 특성
        public readonly List<SigKill> sigKills = new List<SigKill>();
        public double Earned => earnClaw + earnDrone + earnBlast;
    }

    public enum SwEv { Supply, SupplyGet, Strike, Broke, Coin, Pop, Beam, Ring, Blast, Tier, Crit, Collapse, Warn, EventGo, Collector, Shatter, Release, RunEnd, BillPaid, Overdue, Bankrupt, News, Won, SkillReady, NodeBought, Laser, Bolt, Meteor, Tourist, Vac, Shell, Rail , Proc, Act, Volley, TraitFx, ChainPay }

    public struct SwEvent
    {
        public SwEv kind;
        public double x, y, x2, y2, v;
        public int k;
        public string text;
    }

    public enum NodeSt { Hidden, Locked, Poor, Can, Max }

}
