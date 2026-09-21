using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🔴 이 폴더(Sim/)는 UnityEngine 을 쓰지 않는다.
    //    같은 파일을 tools/pacing 의 dotnet 봇이 그대로 컴파일해서 45분 흐름을 잰다.
    //    규칙을 두 벌로 복사하지 않기 위해서다 (Wiki_gm verification.md ⑥ 「식을 복사하지 말 것」).
    //
    //    설계 정본: wiki/rev15-design.md. 여기 숫자는 전부 「모양만」이고 손으로 돌려서 맞춘다.

    [Serializable]
    public class OrbitData
    {
        public double D, D0;
        public int drones;
        public bool open, locked, warned, claimed;
        public double lockedAt = -1;
        public double rampStart = -1, rampFrom = -1;   // 3막 봉쇄까지 차오르는 곡선
        public double angle;            // 인양 표적 등이 도는 기준 (보기용)
    }

    [Serializable]
    public class Transit
    {
        public int from, to, count;
        public double left, total;
    }

    [Serializable]
    public class Contract
    {
        public bool active, accepted;
        public int kind;                // 0 회수 · 1 발사 대행
        public int orbit;
        public double target, progress, reward, debris;
        public double offerLeft, timeLeft;
    }

    [Serializable]
    public class Salvage
    {
        public bool active, big;
        public int orbit, hp, hpMax;
        public double angle, life, value;
    }

    [Serializable]
    public class PendingNews
    {
        public string id, text;
        public double at;
        public bool company;
    }

    [Serializable]
    public class SimState
    {
        public int version = 1;
        public double t;
        public double credits, earned, peak;
        public double held, heldValue;      // 회수했지만 아직 안 판 것
        public double collected;            // 누적 회수
        public int manual;                  // 손으로 주운 수
        public int bought;                  // 드론 구매 수 (값 계산용)
        public OrbitData[] orbits;
        public List<Transit> transit = new List<Transit>();
        public List<string> owned = new List<string>();
        public List<string> fired = new List<string>();
        public List<PendingNews> pending = new List<PendingNews>();
        public string newsLine = "";
        public double newsAt = -999;
        public string pinned = "";
        public Contract contract = new Contract();
        public double contractTimer = 45;
        public Salvage salvage = new Salvage();
        public double salvageTimer = 20;
        public int home;
        public bool autoBuy = true;
        public double growTimer;
        public int act = 1;
        public double act2At = -1, act3At = -1;
        public bool regulated;
        public int ending;                  // 0 없음 · 1 회수 극대화 · 2 방지망 · 3 탈출선 · 4 전부 봉쇄
        public double endingAt = -1, endAt = -1;
        public double made, avoidable;      // 내가 만든 파편 / 그중 안 만들어도 됐던 것
        public int dronesLost;
        public double insuredAt = -1;
        public long rng = 2463534242;
        public int contractsDone, salvaged;
        public bool endingsOpen;
        public double endingCostMax, endingCostShip;
        public int claimed = -1;
        public double blastCharge = 3;      // 직접 파쇄 충전 (09-22 사장님: 「수동의 요소를 조금만」)
    }

    public enum SimEventKind { News, Collision, Warn, Lock, Unlock, ContractDone, Salvaged, Act, Ending, DroneLost, StationHit }

    public struct SimEvent
    {
        public SimEventKind kind;
        public int orbit;
        public string text;
    }

    public sealed class Unlock
    {
        public string id, name, desc;
        public Func<OrbitSim, double> cost;
        public Func<OrbitSim, bool> ready;     // 선행 조건 (보일 자격)
        public Action<OrbitSim> onBuy;
        public bool ending;
    }

    public sealed class OrbitSim
    {
        // ─────────────────────────────────────── 수치 (모양만)
        public static readonly string[] Names = { "저궤도", "중궤도", "정지궤도" };
        public static readonly double[] Speed = { 0.35, 0.20, 0.12 };       // 보기용 각속도 (rad/s)
        static readonly double[] StartD = { 20000, 9000, 5000 };            // 합 34,000 — 실제 추적되는 10cm↑ 파편 수
        static readonly double[] OrbitPrice = { 1, 1.6, 3 };
        static readonly double[] LaunchShare = { 0.5, 0.33, 0.17 };
        static readonly double[] Kessler = { 2.5e-8, 6e-8, 1.2e-7 };          // k·D² — 파편이 파편을 낳는다
        public static readonly double[] LockAt = { 250000, 450000, 700000 };

        public const double BaseRate = 0.8;       // 드론 1대가 처음 밀도에서 초당 줍는 개수
        public const double Saturation = 3;       // 밀도가 이 배를 넘으면 더 빨리는 못 줍는다
        const double BasePrice = 3;
        const double DroneBase = 30, DroneGrowth = 1.10;
        const double CaptureLoss = 0.03;          // 줍다 놓쳐 생기는 파편 — 피할 수 없다
        public const double TravelTime = 10;      // 함대가 궤도를 옮기는 동안은 논다 — 20초는 답답했다 (09-22 「그대로 두되 편하게」)
        const double CleanFrac = 0.12;            // 저궤도가 이 밑으로 내려가면 「정화 완료」
        const double LaunchBase = 110, LaunchTau = 480;   // 🔴 2막 초반부터 비운 궤도가 2~3분에 다시 찬다 — 그래야 옮길 이유가 생긴다 (09-21 투어: 20분에 세 궤도가 다 휑했다)
        public const int SellUnlockAt = 5;
        public const double Act3Fallback = 22 * 60;   // 2막 시작부터 — 끝까지 안 누르면 여기서 3막

        public SimState S;
        public readonly Queue<SimEvent> Events = new Queue<SimEvent>();
        public static readonly List<Unlock> Unlocks = BuildUnlocks();

        public OrbitSim(SimState s = null)
        {
            S = s ?? NewState();
            if (S.orbits == null || S.orbits.Length != 3) S.orbits = NewState().orbits;
        }

        public static SimState NewState()
        {
            var s = new SimState { orbits = new OrbitData[3] };
            for (int i = 0; i < 3; i++)
                s.orbits[i] = new OrbitData { D = StartD[i], D0 = StartD[i], open = i == 0 };
            return s;
        }

        // ─────────────────────────────────────── 조회

        public bool Has(string id) => S.owned.Contains(id);
        public double TotalD { get { double d = 0; foreach (var o in S.orbits) d += o.D; return d; } }
        public int FleetTotal
        {
            get { int n = 0; foreach (var o in S.orbits) n += o.drones; foreach (var tr in S.transit) n += tr.count; return n; }
        }
        public int InTransit { get { int n = 0; foreach (var tr in S.transit) n += tr.count; return n; } }
        public bool Finished => S.endAt >= 0;
        public bool SellVisible => S.manual >= SellUnlockAt || S.collected >= SellUnlockAt;

        public double Eff
        {
            get
            {
                double e = 1;
                if (Has("tow")) e *= 1.5;
                if (Has("salvager")) e *= 2;
                if (Has("grade2")) e *= 2;
                if (Has("grade3")) e *= 2;
                if (Has("shatter")) e *= 1.4;
                if (Has("fleetshatter")) e *= 2;
                if (S.ending == 1) e *= 10;
                return e;
            }
        }

        public double Price(int i)
        {
            double p = BasePrice * OrbitPrice[i];
            if (Has("yard")) p *= 2;
            if (Has("scanner")) p *= 1.5;
            if (Has("wreck")) p *= 1.5;
            if (Has("recycle")) p *= 2;
            if (Has("refinery")) p *= 3;
            if (Has("fleetshatter")) p *= 1.5;
            if (Has("autolaunch")) p *= 2;
            if (S.orbits[i].claimed) p *= 3;
            if (S.regulated) p *= 0.7;
            return p;
        }

        public double Density(int i) => Math.Min(S.orbits[i].D / S.orbits[i].D0, Saturation);

        public double Rate(int i)
        {
            var o = S.orbits[i];
            if (!o.open || o.locked || o.drones == 0) return 0;
            return o.drones * BaseRate * Eff * Density(i);
        }

        /// <summary>🔴 궤도 보험업 — D 가 높을수록 들어온다. 아무것도 안 눌러도.</summary>
        public double InsuranceRate
        {
            get
            {
                if (!Has("insurance") || S.ending == 2) return 0;
                double d = TotalD;
                double insured = 1 + (S.t - S.insuredAt) / 120.0;       // 가입 위성이 늘어난다
                return 0.6 * insured * d * Math.Max(1, d / 40000);        // 위험이 클수록 보험료가 비싸다
            }
        }

        public double CollectIncome { get { double c = 0; for (int i = 0; i < 3; i++) c += Rate(i) * Price(i); return c; } }
        public double IncomeRate => (Has("autosell") ? CollectIncome : 0) + InsuranceRate;
        public double CompanyValue => S.credits + (CollectIncome + InsuranceRate) * 900;

        /// <summary>
        /// 궤도 위험도 0..1 — 2막 후반의 긴장. 시간이 가도 오르고, 선을 넘는 해금을 살 때마다 크게 오른다.
        /// </summary>
        public double Danger
        {
            get
            {
                if (S.act >= 3) return 1;
                if (S.act < 2) return 0;
                double time = Math.Min(1, (S.t - S.act2At) / Act3Fallback);
                int steps = (Has("claim") ? 1 : 0) + (Has("fleetshatter") ? 1 : 0) + (Has("autolaunch") ? 1 : 0);
                return Math.Min(0.99, time * 0.55 + steps * 0.15);
            }
        }

        void StartAct3()
        {
            if (S.act >= 3) return;
            S.act = 3; S.act3At = S.t;
            Fire("act3", "연쇄 충돌 시작 — 저궤도 위성 다수 손실", false);
            Push(SimEventKind.Act, 3);
            S.contract.active = false;
        }

        public double DronePrice => Math.Ceiling(DroneBase * Math.Pow(DroneGrowth, S.bought));

        // ─────────────────────────────────────── 한 프레임

        public void Tick(double dt)
        {
            if (Finished) return;
            S.t += dt;

            UpdateTransit(dt);
            Collect(dt);
            Grow(dt);
            UpdateLocks();
            UpdateActs();
            UpdateEnding(dt);
            UpdateContract(dt);
            UpdateSalvage(dt);
            Automation(dt);
            FlushNews();

            for (int i = 0; i < 3; i++) S.orbits[i].angle += Speed[i] * dt;
            S.blastCharge = Math.Min(BlastMax, S.blastCharge + dt / BlastRecharge);
            if (S.credits > S.peak) S.peak = S.credits;
        }

        void UpdateTransit(double dt)
        {
            for (int k = S.transit.Count - 1; k >= 0; k--)
            {
                var tr = S.transit[k];
                tr.left -= dt;
                if (tr.left > 0) continue;
                var o = S.orbits[tr.to];
                if (o.locked) { S.dronesLost += tr.count; Push(SimEventKind.DroneLost, tr.to); }
                else o.drones += tr.count;
                S.transit.RemoveAt(k);
            }
        }

        void Collect(double dt)
        {
            for (int i = 0; i < 3; i++)
            {
                var o = S.orbits[i];
                double r = Math.Min(Rate(i) * dt, o.D);
                if (r <= 0) continue;
                o.D -= r;
                Gain(r, i);

                // 줍는 행위 자체가 파편을 낳는다
                double loss = r * CaptureLoss;
                o.D += loss; S.made += loss;
                if (Has("shatter") || Has("fleetshatter"))
                {
                    double extra = r * ((Has("shatter") ? 0.35 : 0) + (Has("fleetshatter") ? 0.5 : 0));
                    o.D += extra; S.made += extra; S.avoidable += extra;
                }

                var c = S.contract;
                if (c.active && c.accepted && c.kind == 0 && c.orbit == i) c.progress += r;
            }
        }

        void Gain(double count, int orbit)
        {
            S.collected += count;
            if (Has("autosell")) Earn(count * Price(orbit));
            else { S.held += count; S.heldValue += count * Price(orbit); }
        }

        void Earn(double v)
        {
            S.credits += v;
            S.earned += v;
        }

        void Grow(double dt)
        {
            // 보험 수입
            double ins = InsuranceRate * dt;
            if (ins > 0) Earn(ins);

            if (S.ending == 2)
            {
                // 봉쇄 방지망 — 궤도가 천천히 맑아진다
                foreach (var o in S.orbits) if (!o.locked) o.D *= Math.Pow(0.97, dt);
                return;
            }

            // 2막부터: 궤도가 맑아지니 세상이 쏘아 올린다 — 발사가 파편을 남긴다
            if (S.act >= 2)
            {
                double launches = LaunchBase * Math.Exp((S.t - S.act2At) / LaunchTau);
                if (Has("autolaunch")) { launches *= 3; S.made += launches * 2 / 3 * dt; S.avoidable += launches * 2 / 3 * dt; }
                if (S.orbits[0].locked) launches *= 0.2;         // 저궤도가 막히면 발사가 끊긴다
                for (int i = 0; i < 3; i++)
                    if (S.orbits[i].open && !S.orbits[i].locked) S.orbits[i].D += launches * LaunchShare[i] * dt;
            }

            for (int i = 0; i < 3; i++)
            {
                var o = S.orbits[i];
                if (!o.open) continue;
                if (o.locked)
                {
                    // 막힌 궤도가 옆 궤도로 흘러넘친다
                    if (i + 1 < 3 && !S.orbits[i + 1].locked) S.orbits[i + 1].D += LockAt[i] * 0.0015 * dt;
                    continue;
                }
                // 1막엔 안 돈다 — 1막은 「치우는 기분」이어야 한다 (09-21 첫 화면 투어에서 방치하니 D 가 늘었다)
                double k = S.act >= 2 ? Kessler[i] * o.D * o.D * dt : 0;
                o.D += k;
                if (o.claimed)
                {
                    // 채굴권 구역은 정화를 멈춘 곳이다 — 알아서 불어난다
                    double g = o.D * 0.006 * dt;
                    o.D += g; S.made += g; S.avoidable += g;
                }

                if (S.act >= 2 && k / dt > 3)
                {
                    if (!S.fired.Contains("collision1")) Push(SimEventKind.Collision, i);
                    Fire("collision1", "저궤도서 위성 2기 충돌 — 원인 불명", true);
                }
            }
        }

        void UpdateLocks()
        {
            for (int i = 0; i < 3; i++)
            {
                var o = S.orbits[i];
                if (!o.open || o.locked) continue;

                // 2막에서 버려둔 궤도는 「포화」에서 멈춘다 — 숫자가 터무니없어지지 않게
                if (S.act < 3) { if (o.D > o.D0 * 8) o.D = o.D0 * 8; continue; }

                // 🔴 3막 봉쇄는 정해진 박자로 온다. 함대가 막을 수 없다 — 케슬러는 되돌릴 수 없다 (설계 34 → 40 → 42분)
                //    파편은 그 박자에 맞춰 차오르고, 봉쇄 1분 전에 경고가 뜬다. 대피는 고를 수 있다 (wiki 4회차)
                double T = LockTime(i);
                if (T == double.MaxValue) { if (o.D > LockAt[i] * 0.5) o.D = LockAt[i] * 0.5; continue; }
                if (o.rampStart < 0) { o.rampStart = S.t; o.rampFrom = Math.Max(o.D, o.D0); }
                double k = Math.Min(1, Math.Max(0, (S.t - o.rampStart) / Math.Max(1, T - o.rampStart)));
                double target = o.rampFrom * Math.Pow(LockAt[i] / o.rampFrom, k * k);
                if (o.D < target) o.D = target;
                if (o.D > LockAt[i]) o.D = LockAt[i];     // k·D² 는 봉쇄 전에 무한대로 발산한다 — 상한을 건다 (09-21 봇에서 NaN)

                if (!o.warned && T - S.t <= 60)
                {
                    o.warned = true;
                    Push(SimEventKind.Warn, i);
                }
                if (S.t >= T) Lock(i);
            }
        }

        /// <summary>고향이 먼저 죽는다 — 저궤도 → 중궤도(5분 뒤) → 정지궤도(2분 뒤).</summary>
        double LockTime(int i)
        {
            if (S.act < 3) return double.MaxValue;
            if (i == 0) return S.act3At + 300;
            var inner = S.orbits[i - 1];
            if (!inner.locked) return double.MaxValue;
            return inner.lockedAt + (i == 1 ? 300 : 120);
        }

        void Lock(int i)
        {
            var o = S.orbits[i];
            o.locked = true;
            o.lockedAt = S.t;
            if (o.drones > 0) { S.dronesLost += o.drones; Push(SimEventKind.DroneLost, i); }
            o.drones = 0;
            o.D = LockAt[i];
            if (S.home == i) S.home = FirstOpen();
            Push(SimEventKind.Lock, i);
            if (i == 0)
            {
                Fire("lock0", "저궤도 운용 중단 — 위성 발사 전면 보류", false);
                S.pinned = "저궤도 운용 중단 — 위성 발사 전면 보류";
                Later("gps", "GPS 오차 확대 — 항공편 무더기 지연", 50);
                Later("weather", "기상위성 두절 — 태풍 경로 예측 불가", 110);
                Later("comm", "국제 통신망 30% 손실", 170);
                Later("meteor", "오늘 밤 전국에서 유성우 — 올해 들어 11번째", 230);
            }
            if (i == 1) Fire("lock1", "중궤도 봉쇄", false);
            if (i == 2) Fire("lock2", "정지궤도 봉쇄 — 인류, 우주 접근 수단 상실", false);
        }

        int FirstOpen()
        {
            for (int i = 0; i < 3; i++) if (S.orbits[i].open && !S.orbits[i].locked) return i;
            return 2;
        }

        void UpdateActs()
        {
            var leo = S.orbits[0];
            if (S.act == 1)
            {
                if (leo.D < leo.D0 * 0.5) Fire("leo50", "저궤도 통행량 회복세", true);
                if (leo.D < leo.D0 * 0.3) Fire("leo30", "민간 위성 12기 발사 성공 — 궤도 정리 덕분", true);
                if (leo.D <= leo.D0 * CleanFrac)
                {
                    // 🔴 1막 끝. 큰 보상 — 그리고 깨끗해진 궤도에는 주울 게 없다
                    S.act = 2; S.act2At = S.t;
                    Earn(Math.Max(5000, S.earned * 0.25));
                    Fire("act2", "저궤도 정화 완료 — 10년 만에 안전 등급", true);
                    Push(SimEventKind.Act, 0);
                }
                return;
            }

            if (S.act == 2)
            {
                if (!S.regulated && !Has("lobby") && TotalD > 34000 * 1.25 && Has("geo"))
                {
                    S.regulated = true;
                    Fire("reg", "정부, 파편 배출 상한 규제 검토", true);
                }
                if (TotalD >= 50000 && S.t - S.act2At > 120) Fire("d50k", "파편 5만개 돌파 — 10년 전 수준으로", true);

                // 🔴 2막 끝은 「뭔가 온다」가 보여야 한다 (09-21 사장님: "2->3막 가는 구간이 너무 할 게 없고 기다리는 시간만 너무 길어")
                double dg = Danger;
                if (dg >= 0.25) Fire("dg25", "궤도 위험 등급 「주의」로 상향", false);
                if (dg >= 0.45) Fire("dg45", "위성 3기 연속 충돌 — 전문가 「임계점 근접」", false);
                if (dg >= 0.6 && !S.fired.Contains("station"))
                {
                    S.fired.Add("station");
                    Push(SimEventKind.StationHit, 1);
                    Later("station2", "우주정거장 파편 충돌 — 승무원 긴급 대피", 4);
                }
                if (dg >= 0.8) Fire("dg80", "국제우주기구, 전 궤도 사용 자제 권고", false);

                // 🔴 3막은 플레이어가 연다 — 「역대 최대 계약」을 누르는 순간 (설계: 「2막→3막은 스스로 넘어가야 한다」)
                //    안 누르고 버티면 안전장치 시각에 온다 — 다시는 멈추지 않게 (09-21 첫 판: 3막이 영영 안 왔다)
                if (S.t >= S.act2At + Act3Fallback) StartAct3();
                return;
            }

            if (S.act == 3 && !S.endingsOpen && S.ending == 0)
            {
                // 마지막 기술 셋 — 저궤도가 막히고 조금 지나면
                if (leo.locked && S.t >= leo.lockedAt + 180)
                {
                    S.endingsOpen = true;
                    double income = Math.Max(IncomeRate, 1);
                    S.endingCostMax = Math.Ceiling(income * 40);
                    S.endingCostShip = Math.Ceiling(income * 90);
                    Push(SimEventKind.Unlock, -1);
                }
            }
        }

        void UpdateEnding(double dt)
        {
            if (S.ending == 0)
            {
                bool anyOpen = false;
                foreach (var o in S.orbits) if (o.open && !o.locked) anyOpen = true;
                if (!anyOpen) BeginEnding(4);
                return;
            }
            double since = S.t - S.endingAt;
            switch (S.ending)
            {
                case 1:
                    // 회수 극대화 — 끝까지 빨아먹는다. 그리고 궤도가 차례로 닫힌다
                    foreach (var o in S.orbits) if (!o.locked) o.D *= Math.Pow(1.05, dt);
                    if (since > 60) End();
                    break;
                case 2: if (since > 30) End(); break;
                case 3: if (since > 10) End(); break;
                case 4: if (since > 6) End(); break;
            }
        }

        void BeginEnding(int e)
        {
            if (S.ending != 0) return;
            S.ending = e;
            S.endingAt = S.t;
            S.contract.active = false;
            Push(SimEventKind.Ending, e);
        }

        void End()
        {
            S.endAt = S.t;
            // 회수 극대화 · 전부 봉쇄 — 「궤도는 완전히 닫혔다」와 화면이 맞게 남은 궤도도 닫는다
            if (S.ending == 1 || S.ending == 4)
                for (int i = 0; i < 3; i++)
                    if (S.orbits[i].open && !S.orbits[i].locked) { S.orbits[i].locked = true; S.orbits[i].lockedAt = S.t; S.orbits[i].D = LockAt[i]; }
        }

        // ─────────────────────────────────────── 계약

        void UpdateContract(double dt)
        {
            var c = S.contract;
            if (!Has("board") || S.act >= 3 || S.ending != 0) { c.active = false; return; }

            if (!c.active)
            {
                S.contractTimer -= dt;
                if (S.contractTimer > 0) return;
                NewContract();
                return;
            }
            if (!c.accepted)
            {
                c.offerLeft -= dt;
                if (c.offerLeft <= 0) CloseContract();
                return;
            }
            c.timeLeft -= dt;
            if (c.progress >= c.target)
            {
                Earn(c.reward);
                S.contractsDone++;
                Push(SimEventKind.ContractDone, c.orbit);
                CloseContract();
            }
            else if (c.timeLeft <= 0) CloseContract();
        }

        void NewContract()
        {
            var c = S.contract;
            c.active = true; c.accepted = false; c.progress = 0; c.offerLeft = 30;
            double income = Math.Max(IncomeRate, CollectIncome);
            if (Has("launch") && Rand() < 0.45)
            {
                c.kind = 1;
                c.orbit = Rand() < 0.6 ? 0 : 1;
                c.reward = Math.Ceiling(Math.Max(300, income * 150));
                c.debris = Math.Ceiling(1500 * (1 + Math.Max(0, S.t - S.act2At) / 600));
                c.target = 0; c.timeLeft = 0;
                return;
            }
            c.kind = 0;
            var open = new List<int>();
            for (int i = 0; i < 3; i++) if (S.orbits[i].open && !S.orbits[i].locked) open.Add(i);
            c.orbit = open[(int)(Rand() * open.Count) % open.Count];
            double fleetRate = Math.Max(1, FleetTotal) * BaseRate * Eff * Math.Max(0.3, Density(c.orbit));
            c.target = Math.Ceiling(Math.Min(S.orbits[c.orbit].D * 0.5, fleetRate * 45));
            c.timeLeft = 150;
            c.reward = Math.Ceiling(Math.Max(80, income * 100));
        }

        void CloseContract()
        {
            S.contract.active = false;
            S.contractTimer = 60 + Rand() * 60;
        }

        public void AcceptContract()
        {
            var c = S.contract;
            if (!c.active || c.accepted) return;
            if (c.kind == 1)
            {
                // 발사 대행 — 돈이 크다. 그리고 발사는 파편을 남긴다 (적혀 있다)
                Earn(c.reward);
                var o = S.orbits[c.orbit];
                if (!o.locked) o.D += c.debris;
                S.made += c.debris; S.avoidable += c.debris;
                S.contractsDone++;
                Push(SimEventKind.ContractDone, c.orbit);
                CloseContract();
                return;
            }
            c.accepted = true;
        }

        public void DeclineContract()
        {
            if (S.contract.active && !S.contract.accepted) CloseContract();
        }

        // ─────────────────────────────────────── 인양 표적 (죽은 위성)

        void UpdateSalvage(double dt)
        {
            var v = S.salvage;
            if (!Has("wreck") || S.ending != 0) { v.active = false; return; }
            if (v.active)
            {
                v.life -= dt;
                v.angle += Speed[v.orbit] * 0.8 * dt;
                if (v.life <= 0 || S.orbits[v.orbit].locked) { v.active = false; S.salvageTimer = 35 + Rand() * 25; }
                return;
            }
            S.salvageTimer -= dt;
            if (S.salvageTimer > 0) return;
            var open = new List<int>();
            for (int i = 0; i < 3; i++) if (S.orbits[i].open && !S.orbits[i].locked) open.Add(i);
            if (open.Count == 0) return;
            v.active = true;
            v.orbit = open[(int)(Rand() * open.Count) % open.Count];
            v.angle = Rand() * Math.PI * 2;
            // 2막부터는 여러 번 두드려야 하는 큰 우주선 잔해도 나온다
            v.big = S.act >= 2 && Rand() < 0.4;
            v.hpMax = v.hp = v.big ? 10 : (S.act >= 2 ? 5 : 3);
            v.life = v.big ? 35 : 28;
            v.value = Math.Ceiling(Math.Max(60, Math.Max(IncomeRate, CollectIncome) * (v.big ? 90 : 30)));
        }

        /// <summary>
        /// 큰 잔해를 한 번 두드린다. 두드릴 때마다 조각값이 들어오고 마지막 한 방에 나머지 절반이 한꺼번에 들어온다.
        /// 돌려주는 값 = 이번에 번 크레딧. 부서졌으면 broke = true.
        /// </summary>
        public double HitSalvage(out bool broke)
        {
            broke = false;
            var v = S.salvage;
            if (!v.active || Finished) return 0;
            if (v.hpMax <= 0) v.hpMax = v.hp = 1;          // 옛 저장
            double chunk = Math.Ceiling(v.value * 0.5 / v.hpMax);
            Earn(chunk);
            v.hp--;
            if (v.hp > 0) return chunk;
            double fin = Math.Ceiling(v.value * 0.5);
            Earn(fin);
            broke = true;
            S.salvaged++;
            v.active = false;
            S.salvageTimer = 35 + Rand() * 25;
            Push(SimEventKind.Salvaged, v.orbit);
            if (S.act < 3) Fire("salv1", "대형 잔해 통째 인양 — 민간 업체 최초", true);
            return chunk + fin;
        }

        /// <summary>봇용 — 부서질 때까지 두드린다.</summary>
        public void ClaimSalvage()
        {
            int guard = 0;
            while (S.salvage.active && guard++ < 50) HitSalvage(out _);
        }

        // ─────────────────────────────────────── 직접 파쇄 — 누르면 그 자리가 터진다 (09-22)

        public int BlastMax => Has("charges") ? 5 : 3;
        public double BlastRecharge => Has("charges") ? 2 : 3;

        /// <summary>한 방의 크기 — 지금 함대가 1.5초 동안 줍는 양. 그래서 처음부터 끝까지 누를 값어치가 있다.</summary>
        public double BlastPower
        {
            get
            {
                double fleet = 0;
                for (int i = 0; i < 3; i++) fleet += Rate(i);
                double p = Math.Max(1, fleet * 1.5);   // 2.5 는 부지런히 누르면 1막이 6분으로 줄었다 (09-22 봇)
                if (Has("chainblast")) p *= 3;
                if (S.act >= 3) p *= 2;
                return Math.Ceiling(p);
            }
        }

        /// <summary>드론을 사기 전엔 충전 없이 한 번에 하나씩 줍는다 (첫 60초 그대로).</summary>
        public bool BlastUsesCharge => S.bought > 0;

        public double Blast(int orbit)
        {
            var o = S.orbits[orbit];
            if (!o.open || o.locked || o.D < 1 || Finished) return 0;
            if (BlastUsesCharge)
            {
                if (S.blastCharge < 1) return 0;
                S.blastCharge -= 1;
            }
            double amt = Math.Min(o.D, BlastPower);
            o.D -= amt;
            S.manual++;
            Gain(amt, orbit);
            double loss = amt * CaptureLoss;
            o.D += loss; S.made += loss;
            return amt;
        }

        // ─────────────────────────────────────── 자동화

        void Automation(double dt)
        {
            if (Has("manager") && S.autoBuy && S.ending == 0)
            {
                int guard = 0;
                while (DronePrice <= S.credits * 0.25 && guard++ < 50) BuyDrone();
            }
            if (Has("swarm") && S.ending == 0)
            {
                S.growTimer += dt;
                while (S.growTimer >= 4)
                {
                    S.growTimer -= 4;
                    var o = S.orbits[S.home];
                    if (!o.locked) o.drones++;
                }
            }
        }

        // ─────────────────────────────────────── 손이 하는 것

        public bool Pick(int orbit)
        {
            var o = S.orbits[orbit];
            if (!o.open || o.locked || o.D < 1 || Finished) return false;
            o.D -= 1;
            S.manual++;
            Gain(1, orbit);
            return true;
        }

        public void Sell()
        {
            if (S.held <= 0) return;
            bool first = S.earned == 0;
            Earn(S.heldValue);
            S.held = 0; S.heldValue = 0;
            if (first) Fire("start", "저궤도 파편 34,000개 — 국제우주기구, 민간에 정리 위탁", true);
        }

        public bool BuyDrone()
        {
            double p = DronePrice;
            if (S.credits < p || S.ending != 0) return false;
            S.credits -= p;
            S.bought++;
            if (S.orbits[S.home].locked) S.home = FirstOpen();
            S.orbits[S.home].drones++;
            if (S.bought == 1) Fire("drone1", "무인 회수 드론 실전 투입", true);
            if (S.bought == 5) Fire("firm1", "신생 청소업체 첫 회수 성공", true);
            return true;
        }

        /// <summary>
        /// 🔴 이 게임에서 손이 하는 일 — 함대를 옮긴다. 옮기는 동안은 논다 (wiki 8회차).
        /// half = 다른 궤도에서 절반씩만 보낸다.
        /// </summary>
        public void Move(int to, bool half)
        {
            var dest = S.orbits[to];
            if (!dest.open || dest.locked || S.ending == 2) return;
            int sent = 0, from = -1, most = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == to) continue;
                var o = S.orbits[i];
                int n = half ? o.drones / 2 : o.drones;
                if (n <= 0) continue;
                o.drones -= n;
                sent += n;
                if (n > most) { most = n; from = i; }
            }
            if (sent > 0) S.transit.Add(new Transit { from = from, to = to, count = sent, left = TravelTime, total = TravelTime });
            S.home = to;
        }

        public bool CanBuy(Unlock u) => !Has(u.id) && u.ready(this) && S.credits >= u.cost(this) && S.ending == 0;

        /// <summary>보일 자격 — 선행이 풀렸고 값의 40%쯤 모였을 때. 🔴 못 사는 걸 먼저 보여준다.</summary>
        public bool Visible(Unlock u)
        {
            if (Has(u.id) || !u.ready(this)) return false;
            if (u.ending) return S.endingsOpen && S.ending == 0;
            if (u.cost(this) <= 0) return true;
            return S.credits >= u.cost(this) * 0.4 || S.earned >= u.cost(this) * 0.4;
        }

        public bool Buy(string id)
        {
            var u = Unlocks.Find(x => x.id == id);
            if (u == null || !CanBuy(u)) return false;
            S.credits -= u.cost(this);
            S.owned.Add(u.id);
            u.onBuy?.Invoke(this);
            Push(SimEventKind.Unlock, -1, u.id);
            return true;
        }

        // ─────────────────────────────────────── 해금 표

        static bool Always(OrbitSim s) => true;

        static List<Unlock> BuildUnlocks()
        {
            var L = new List<Unlock>();
            void U(string id, string name, string desc, double cost, Func<OrbitSim, bool> ready, Action<OrbitSim> onBuy = null)
                => L.Add(new Unlock { id = id, name = name, desc = desc, cost = _ => cost, ready = ready, onBuy = onBuy });

            // 1막 · 청소부 — 손 떼기 사다리: 줍기 → 파는 것 → 사는 것
            U("yard", "해체장", "파편을 고철로 판다 · 값 ×2", 150, s => s.S.bought >= 2,
              s => s.Fire("yard", "해체장 가동 — 파편에서 금속 회수", true));
            U("autosell", "자동 판매", "회수하는 대로 바로 판다", 450, s => s.Has("yard"));
            U("tow", "견인 용량", "드론 효율 ×1.5", 1000, s => s.S.bought >= 5);
            U("scanner", "궤도 스캐너", "종류별로 골라 판다 · 값 ×1.5", 2400, s => s.Has("autosell"));
            U("wreck", "회로 회수", "죽은 위성이 가끔 뜬다 — 눌러서 통째로 인양 · 값 ×1.5", 5000, s => s.Has("scanner"));
            U("manager", "관리자 고용", "드론을 알아서 산다", 9000, s => s.Has("tow"));
            U("charges", "파쇄 장약", "직접 파쇄 충전 5칸 · 더 빨리 찬다", 700, s => s.S.bought >= 3);
            U("board", "계약 게시판", "계약이 들어온다", 16000, s => s.Has("manager"));
            U("salvager", "대형 인양선", "드론 효율 ×2", 30000, s => s.Has("board"));

            // 2막 · 사업
            U("meo", "중궤도 진출", "새 궤도 · 함대를 옮길 수 있다", 30000, s => s.S.act >= 2,
              s => { s.S.orbits[1].open = true; s.Fire("meo", "중궤도 정리 사업자 공모", true); });
            U("launch", "발사 대행", "위성 발사를 대신 해준다 · 계약에 발사가 섞인다", 80000, s => s.Has("meo"),
              s => s.Fire("launch", "발사 대행 수주 — 업계 진출", true));
            U("recycle", "재활용 공장", "값 ×2", 150000, s => s.Has("meo"));
            U("chainblast", "연쇄 기폭", "직접 파쇄 한 방 ×3 · 터지면 옆으로 번진다", 120000, s => s.Has("meo"));
            U("grade2", "드론 등급 2", "효율 ×2", 300000, s => s.Has("recycle"));
            U("geo", "정지궤도 진출", "제일 값비싼 궤도 · 값 ×3", 500000, s => s.Has("grade2"),
              s => s.S.orbits[2].open = true);
            // 🔴 유혹 셋 — 일반 해금과 **똑같이 생긴다**. 수치만 정직하게 적는다 (wiki 3회차)
            U("shatter", "파쇄 로켓", "대형 위성을 깨서 회수 · 효율 ×1.4 · 회수할 때마다 파편 +35%", 800000, s => s.Has("recycle"),
              s => s.Fire("shatter", "대형 잔해 해체 신공법 도입 — 처리 속도 5배", true));
            U("insurance", "궤도 보험업", "충돌 보험을 판다 · 파편이 많을수록 보험료가 오른다", 1200000, s => s.Has("grade2"),
              s =>
              {
                  s.S.insuredAt = s.S.t;
                  s.Fire("ins", "궤도 충돌 보험 시장 개설", true);
                  s.Later("ins2", "보험료 사상 최고 — 가입 문의 폭주", 60, true);
              });
            U("cap", "시가총액 공시", "회사 가치를 본다", 15000000, s => s.Has("geo"),
              s => s.Later("top1", "궤도환경 업계 1위 등극", 40, true));
            U("swarm", "함대 자동 증식", "4초마다 드론 +1", 2000000, s => s.Has("geo"));
            U("lobby", "로비", "규제를 돈으로 민다", 6000000, s => s.S.regulated,
              s => { s.S.regulated = false; s.Later("lobby", "규제안 무기한 보류", 45, true); });
            U("grade3", "드론 등급 3", "효율 ×2", 4000000, s => s.Has("swarm"));
            L.Add(new Unlock
            {
                id = "claim", name = "궤도 채굴권 매입",
                desc = "제일 빽빽한 궤도를 사서 정화를 멈춘다 · 그 궤도 값 ×3 · 저절로 불어난다",
                cost = _ => 10000000, ready = s => s.Has("grade3"),
                onBuy = s =>
                {
                    int best = -1; double bd = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        var o = s.S.orbits[i];
                        if (o.open && !o.locked && o.D / o.D0 > bd) { bd = o.D / o.D0; best = i; }
                    }
                    if (best < 0) return;
                    s.S.orbits[best].claimed = true;
                    s.S.claimed = best;
                    s.Fire("claim", Names[best] + " 채굴권 낙찰 — 정화 무기한 연기", true);
                }
            });
            U("refinery", "궤도 정련소", "값 ×3", 30000000, s => s.Has("grade3"));

            // 🔴 「선 넘기」 사다리 — 2막 끝의 발전 요소가 곧 3막으로 가는 길이다 (09-21)
            U("fleetshatter", "대량 파쇄 편대", "파쇄 로켓을 함대로 · 효율 ×2 · 값 ×1.5 · 회수할 때마다 파편 +50%", 60000000,
              s => s.Has("refinery"), s => s.Fire("fleet", "대형 위성 연쇄 해체 — 업계 「속도 신기록」", true));
            U("autolaunch", "궤도 자동 발사장", "발사를 쉬지 않고 대신 해준다 · 값 ×2 · 발사 ×3", 150000000,
              s => s.Has("fleetshatter"), s => s.Fire("autol", "민간 발사 하루 100회 돌파", true));
            L.Add(new Unlock
            {
                id = "finalcontract", name = "역대 최대 계약 — 전 궤도 동시 파쇄",
                desc = "모든 궤도의 대형 잔해를 한꺼번에 깬다 · 보상은 지금 수입의 30분치",
                cost = _ => 0, ready = s => s.Has("autolaunch") && s.S.act == 2,
                onBuy = s =>
                {
                    s.Earn(Math.Ceiling(Math.Max(1, s.IncomeRate) * 1800));
                    for (int i = 0; i < 3; i++) if (s.S.orbits[i].open && !s.S.orbits[i].locked) s.S.orbits[i].D += s.S.orbits[i].D0 * 2;
                    s.StartAct3();
                }
            });

            // 3막 — 마지막 기술 셋. 하나만 산다 (= 엔딩)
            L.Add(new Unlock
            {
                id = "end_max", name = "회수 극대화", desc = "끝까지 빨아먹는다", ending = true,
                cost = s => s.S.endingCostMax, ready = s => s.S.endingsOpen, onBuy = s => s.BeginEnding(1)
            });
            L.Add(new Unlock
            {
                id = "end_net", name = "궤도 봉쇄 방지망", desc = "전 재산을 턴다", ending = true,
                cost = s => Math.Max(s.S.credits, s.S.endingCostMax), ready = s => s.S.endingsOpen,
                onBuy = s => { s.S.credits = 0; s.BeginEnding(2); }
            });
            L.Add(new Unlock
            {
                id = "end_ship", name = "탈출선", desc = "지구를 뜬다", ending = true,
                cost = s => s.S.endingCostShip, ready = s => s.S.endingsOpen, onBuy = s => s.BeginEnding(3)
            });
            return L;
        }

        // ─────────────────────────────────────── 뉴스

        /// <summary>
        /// 뉴스 한 줄. 🔴 평가하지 않는다 · 3막에서 회사 소식은 안 나온다 (wiki 7회차).
        /// company = 회사가 주어인 줄.
        /// </summary>
        void Fire(string id, string text, bool company)
        {
            if (S.fired.Contains(id)) return;
            S.fired.Add(id);
            if (company && S.act >= 3) return;
            S.newsLine = text;
            S.newsAt = S.t;
            Push(SimEventKind.News, -1, text);
        }

        void Later(string id, string text, double delay, bool company = false)
        {
            if (S.fired.Contains(id)) return;
            foreach (var p in S.pending) if (p.id == id) return;
            S.pending.Add(new PendingNews { id = id, text = text, at = S.t + delay, company = company });
        }

        void FlushNews()
        {
            for (int k = S.pending.Count - 1; k >= 0; k--)
            {
                var p = S.pending[k];
                if (S.t < p.at) continue;
                S.pending.RemoveAt(k);
                Fire(p.id, p.text, p.company);
            }
        }

        // ─────────────────────────────────────── 기타

        void Push(SimEventKind k, int orbit, string text = null)
        {
            if (Events.Count > 200) Events.Dequeue();
            Events.Enqueue(new SimEvent { kind = k, orbit = orbit, text = text });
        }

        double Rand()
        {
            uint x = (uint)S.rng;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            S.rng = x;
            return x / 4294967296.0;
        }

        public static string EndingTitle(int e)
        {
            switch (e)
            {
                case 1: return "회수 극대화";
                case 2: return "궤도 봉쇄 방지망";
                case 3: return "탈출선";
                default: return "모든 궤도 봉쇄";
            }
        }

        public static string[] EndingLines(int e)
        {
            switch (e)
            {
                case 1: return new[] { "마지막 한 조각까지 회수했다.", "궤도는 완전히 닫혔다." };
                case 2: return new[] { "회사는 파산했다.", "궤도는 천천히 맑아지고 있다." };
                case 3: return new[] { "지구를 떠났다.", "뒤에서 궤도가 닫혔다." };
                default: return new[] { "모든 궤도가 닫혔다.", "오늘도 궤도는 깨끗합니다." };
            }
        }
    }
}
