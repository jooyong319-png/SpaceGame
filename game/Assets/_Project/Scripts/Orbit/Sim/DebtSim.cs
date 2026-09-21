using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🔴 rev16 「빚 갚는 청소선」 규칙 전부 (2026-09-22). UnityEngine 을 안 쓴다 — tools/pacing 봇이 그대로 컴파일한다.
    //    흐름: ① 출동 ② 정비소 ③ 청구서 ④ 파산 ⑤ 빚 청산 — 기준은 Bills Must Be Paid (wiki/rev16-design.md).
    //    숫자는 브라우저 시안(압축판)과 같다. 2시간 흐름으로 늘리는 건 봇으로 다음에.
    //
    //    좌표는 시안과 같은 960×600 「화면 점」 단위 — 지구 (480,300), 궤도는 세로로 0.62 눌린 타원.

    [Serializable]
    public class Debris
    {
        public int k, hp, max;
        public double a, rr, sp, hit, fade;
    }

    [Serializable]
    public class DebtState
    {
        public int version = 16;
        public double cash;
        public int runs, slots = 1, maxOrbit, orbit, bill, billDue = 3, overRuns;
        public bool overdue, vaults, wrecks, won;
        public int perkFuel, chainHops = 1;
        public int[] lv = new int[DebtSim.TreeCount];
        public List<int> equip = new List<int>();
        public double billAmount = -1;         // 연체료가 붙은 지금 금액 (-1 = 원래 금액)
    }

    [Serializable]
    public class DebtMeta
    {
        public double credit;
        public int[] career = new int[DebtSim.CareerCount];
        public int company = 1, bankrupt, bestChain, totalRuns;
        public double broken, playSeconds;
    }

    public class DebtRun
    {
        public int fuel, max, broke, vault, chainBest, rainCount;
        public double t, next = 0.6, earned, cut, droneT, endT;
        public bool over;
        public readonly List<Debris> debris = new List<Debris>();
    }

    public enum DebtEv { Slam, Broke, Pop, Bolt, Ring, Coin, RunEnd, BillPaid, Overdue, Bankrupt, Won, Crit }

    public struct DebtEvent
    {
        public DebtEv kind;
        public double x, y, x2, y2, v;
        public int k;
        public string text;
    }

    public sealed class DebtSim
    {
        // ───────────────────────── 정의 (시안과 같은 값)
        public struct DType { public string name; public int hp; public double val, r, w; }
        public static readonly DType[] Types =
        {
            new DType { name = "조각",      hp = 1,  val = 2,   r = 5,  w = 60 },
            new DType { name = "죽은 위성", hp = 3,  val = 12,  r = 10, w = 22 },
            new DType { name = "로켓 동체", hp = 5,  val = 24,  r = 13, w = 10 },
            new DType { name = "금고 위성", hp = 6,  val = 90,  r = 11, w = 0 },
            new DType { name = "연료통",    hp = 1,  val = 0,   r = 8,  w = 5 },
            new DType { name = "폭발 탱크", hp = 2,  val = 3,   r = 9,  w = 5 },
            new DType { name = "큰 잔해",   hp = 25, val = 400, r = 26, w = 0 },
        };
        public const int Chip = 0, Sat = 1, Rocket = 2, Vault = 3, Fuel = 4, Bomb = 5, Wreck = 6;

        public struct OrbitDef { public string name; public double r0, r1, mult; }
        public static readonly OrbitDef[] Orbits =
        {
            new OrbitDef { name = "저궤도",   r0 = 120, r1 = 230, mult = 1 },
            new OrbitDef { name = "중궤도",   r0 = 150, r1 = 260, mult = 4 },
            new OrbitDef { name = "정지궤도", r0 = 170, r1 = 280, mult = 16 },
        };

        public struct Node { public string id, branch, name, desc; public double cost; public int max; }
        public static readonly Node[] Tree =
        {
            new Node { id = "pow",    branch = "집게", name = "집게 위력", desc = "한 대에 +1",                       cost = 20,  max = 10 },
            new Node { id = "rad",    branch = "집게", name = "집게 범위", desc = "둘레 +12",                         cost = 30,  max = 8 },
            new Node { id = "spd",    branch = "집게", name = "타격 간격", desc = "0.08초 빨라진다",                  cost = 45,  max = 7 },
            new Node { id = "tank",   branch = "연료", name = "연료 탱크", desc = "연료 +8",                          cost = 25,  max = 10 },
            new Node { id = "refuel", branch = "연료", name = "연료 회수", desc = "부술 때 연료 +1 확률 +6%",         cost = 60,  max = 6 },
            new Node { id = "dens",   branch = "연료", name = "잔해 밀도", desc = "궤도에 잔해 +5",                   cost = 40,  max = 6 },
            new Node { id = "crit",   branch = "운",   name = "치명타",    desc = "한 대가 3배로 · 확률 +6%",         cost = 50,  max = 6 },
            new Node { id = "vault",  branch = "운",   name = "금고 감별", desc = "금고 위성이 더 자주",              cost = 80,  max = 5 },
            new Node { id = "val",    branch = "운",   name = "고철 시세", desc = "모든 값 ×1.3",                     cost = 70,  max = 8 },
            new Node { id = "chain",  branch = "연쇄", name = "연쇄 충돌", desc = "부서진 조각이 옆을 맞힐 확률 +12%", cost = 90,  max = 6 },
            new Node { id = "boom",   branch = "연쇄", name = "폭발 위력", desc = "폭발 탱크 반경 +25%",              cost = 70,  max = 4 },
            new Node { id = "rain",   branch = "장비", name = "파편 비",   desc = "3번에 한 번 하늘에서 파편이 쏟아진다", cost = 400, max = 1 },
            new Node { id = "zap",    branch = "장비", name = "전기 집게", desc = "맞은 것 둘레 3개가 감전",          cost = 600, max = 1 },
            new Node { id = "net",    branch = "장비", name = "중력 그물", desc = "잔해가 집게 쪽으로 끌려온다",      cost = 500, max = 1 },
            new Node { id = "drone",  branch = "장비", name = "보조 드론", desc = "작게 저절로 친다 (연료 안 씀)",    cost = 900, max = 1 },
        };
        public const int TreeCount = 15;
        public static readonly string[] Branches = { "집게", "연료", "운", "연쇄", "장비" };

        public struct Bill { public string t, perk; public double m; public int due; }
        public static readonly Bill[] Bills =
        {
            new Bill { t = "연료비",           m = 150,    due = 3, perk = "연료 +5" },
            new Bill { t = "청소선 할부 1회",  m = 500,    due = 3, perk = "금고 위성이 나온다" },
            new Bill { t = "궤도 사용료",      m = 1500,   due = 4, perk = "중궤도 면허" },
            new Bill { t = "보험료",           m = 4000,   due = 4, perk = "큰 잔해가 나온다" },
            new Bill { t = "청소선 할부 2회",  m = 12000,  due = 4, perk = "장비 칸 +1" },
            new Bill { t = "법인세",           m = 30000,  due = 4, perk = "정지궤도 면허" },
            new Bill { t = "청소선 할부 3회",  m = 90000,  due = 5, perk = "연쇄가 두 번 튄다" },
            new Bill { t = "청소선 할부 완납", m = 260000, due = 5, perk = "빚 청산" },
        };

        public struct Career { public string id, name, desc; public double cost; public int max; }
        public static readonly Career[] Careers =
        {
            new Career { id = "pilot",  name = "베테랑 조종사", desc = "연료 +20%",         cost = 3, max = 3 },
            new Career { id = "seed",   name = "단골 고객",     desc = "시작 자금 +300",    cost = 2, max = 3 },
            new Career { id = "wrench", name = "정비 요령",     desc = "트리 값 -10%",      cost = 4, max = 3 },
            new Career { id = "talk",   name = "협상가",        desc = "청구서 기한 +1판",  cost = 5, max = 2 },
            new Career { id = "friend", name = "추심원과 친구", desc = "추심 30% → 20%",    cost = 4, max = 1 },
            new Career { id = "charm",  name = "행운의 부적",   desc = "금고 위성 +50%",    cost = 3, max = 2 },
        };
        public const int CareerCount = 6;

        public const double EX = 480, EY = 300, Squash = 0.62;

        // ───────────────────────── 상태
        public DebtState S;
        public DebtMeta M;
        public DebtRun R;
        public readonly Queue<DebtEvent> Events = new Queue<DebtEvent>();
        readonly Random rng;

        public DebtSim(DebtState s = null, DebtMeta m = null, int seed = 0)
        {
            M = m ?? new DebtMeta();
            if (M.career == null || M.career.Length != CareerCount) M.career = new int[CareerCount];
            S = s ?? Fresh();
            if (S.lv == null || S.lv.Length != TreeCount) S.lv = new int[TreeCount];
            if (S.equip == null) S.equip = new List<int>();
            rng = seed == 0 ? new Random() : new Random(seed);
            R = new DebtRun { over = true };
        }

        DebtState Fresh() => new DebtState { cash = Cr("seed") * 300 };

        public static int TreeIndex(string id) { for (int i = 0; i < Tree.Length; i++) if (Tree[i].id == id) return i; return -1; }
        static int CareerIndex(string id) { for (int i = 0; i < Careers.Length; i++) if (Careers[i].id == id) return i; return -1; }
        public int L(string id) => S.lv[TreeIndex(id)];
        public int Cr(string id) => M.career[CareerIndex(id)];
        public bool Equipped(string id) => S.equip.Contains(TreeIndex(id));

        public double Pow => 1 + L("pow");
        public double Rad => 42 + L("rad") * 12;
        public double Gap => Math.Max(0.35, 1.0 - L("spd") * 0.08);
        public int FuelMax => (int)Math.Round((30 + L("tank") * 8 + S.perkFuel) * (1 + 0.2 * Cr("pilot")));
        public double CritChance => L("crit") * 0.06;
        public double ValMult => Math.Pow(1.3, L("val")) * Orbits[S.orbit].mult;
        public double ChainChance => L("chain") * 0.12;
        public double Cut => S.overdue ? (Cr("friend") > 0 ? 0.2 : 0.3) : 0;
        public double Cost(int i) => Math.Ceiling(Tree[i].cost * Math.Pow(1.75, S.lv[i]) * (1 - 0.1 * Cr("wrench")));
        public double BillAmount => S.bill < Bills.Length ? (S.billAmount > 0 ? S.billAmount : Bills[S.bill].m) : 0;
        public double CreditFor(double amount) => Math.Max(1, Math.Round(Math.Sqrt(amount) / 6));
        public bool CanBankrupt => S.overdue || S.bill >= 2;

        public static void Pos(Debris d, out double x, out double y)
        {
            x = EX + Math.Cos(d.a) * d.rr;
            y = EY + Math.Sin(d.a) * d.rr * Squash;
        }

        // ───────────────────────── 출동

        public void StartRun()
        {
            S.runs++; M.totalRuns++;
            R = new DebtRun { fuel = FuelMax, max = FuelMax };
            int n = 24 + L("dens") * 5;
            for (int i = 0; i < n; i++) Spawn(true);
        }

        int PickType()
        {
            double[] w = new double[Types.Length];
            double sum = 0;
            for (int i = 0; i < Types.Length; i++)
            {
                double x = Types[i].w;
                if (i == Vault && S.vaults) x = (3 + L("vault") * 2) * (1 + 0.5 * Cr("charm"));
                if (i == Wreck && S.wrecks) x = 1.2;
                w[i] = x; sum += x;
            }
            double r = rng.NextDouble() * sum;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0) return i; }
            return Chip;
        }

        void Spawn(bool anywhere)
        {
            var o = Orbits[S.orbit];
            int k = PickType();
            int hp = (int)Math.Ceiling(Types[k].hp * (1 + S.orbit * 0.8));
            R.debris.Add(new Debris
            {
                k = k, hp = hp, max = hp, a = rng.NextDouble() * 6.283, rr = o.r0 + rng.NextDouble() * (o.r1 - o.r0),
                sp = (0.05 + rng.NextDouble() * 0.1) * (rng.NextDouble() < 0.5 ? 1 : -1), fade = anywhere ? 1 : 0
            });
        }

        void Push(DebtEv k, double x = 0, double y = 0, string text = null, double v = 0, int type = 0, double x2 = 0, double y2 = 0)
        {
            if (Events.Count > 400) Events.Dequeue();
            Events.Enqueue(new DebtEvent { kind = k, x = x, y = y, text = text, v = v, k = type, x2 = x2, y2 = y2 });
        }

        void Damage(Debris d, double dmg, int depth)
        {
            if (d.hp <= 0) return;
            d.hp -= (int)Math.Ceiling(dmg); d.hit = 0.15;
            if (d.hp > 0) return;
            Pos(d, out double x, out double y);
            var T = Types[d.k];
            R.broke++; M.broken++;
            double v = T.val * ValMult;
            if (d.k == Vault) R.vault++;
            Push(DebtEv.Broke, x, y, null, v, d.k);
            if (d.k == Fuel) { R.fuel += 5; Push(DebtEv.Pop, x, y, "연료 +5", 0, 1); }
            if (d.k == Bomb)
            {
                double rad = 70 * (1 + 0.25 * L("boom"));
                Push(DebtEv.Ring, x, y, null, rad, Bomb);
                foreach (var e in R.debris.ToArray())
                {
                    Pos(e, out double ex, out double ey);
                    if (e != d && Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y)) < rad) Damage(e, Pow * 2, depth + 1);
                }
            }
            if (v > 0) Earn(v, x, y);
            if (rng.NextDouble() < L("refuel") * 0.06) R.fuel += 1;

            // 🔴 연쇄 — 부서진 조각이 옆 잔해를 맞힌다 (케슬러가 무기다)
            int hops = S.chainHops;
            while (hops-- > 0 && rng.NextDouble() < ChainChance)
            {
                Debris best = null; double bd = 110;
                foreach (var e in R.debris)
                {
                    if (e.hp <= 0) continue;
                    Pos(e, out double ex, out double ey);
                    double dd = Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y));
                    if (dd < bd) { bd = dd; best = e; }
                }
                if (best == null) break;
                Pos(best, out double bx, out double by);
                Push(DebtEv.Bolt, x, y, null, 0, 1, bx, by);
                R.chainBest = Math.Max(R.chainBest, depth + 1);
                Damage(best, Pow, depth + 1);
            }
        }

        void Earn(double v, double x, double y)
        {
            double cut = v * Cut;
            R.earned += v - cut; R.cut += cut; S.cash += v - cut;
            Push(DebtEv.Coin, x, y, null, v - cut, cut > 0 ? 1 : 0);
        }

        /// <summary>집게가 한 번 내리친다 — 연료 1. 헛쳐도 1.</summary>
        void Strike(double ax, double ay)
        {
            if (R.fuel <= 0) return;
            R.fuel--;
            double dmg = Pow;
            bool crit = rng.NextDouble() < CritChance;
            if (crit) dmg *= 3;
            var hitList = new List<Debris>();
            foreach (var d in R.debris)
            {
                if (d.hp <= 0) continue;
                Pos(d, out double x, out double y);
                if (Math.Sqrt((x - ax) * (x - ax) + (y - ay) * (y - ay)) < Rad + Types[d.k].r) hitList.Add(d);
            }
            foreach (var d in hitList) Damage(d, dmg, 0);
            bool hit = hitList.Count > 0;
            if (crit && hit) Push(DebtEv.Crit, ax, ay - 30);

            if (Equipped("zap") && hit)
            {
                var near = new List<(Debris d, double dist, double x, double y)>();
                foreach (var d in R.debris)
                {
                    if (d.hp <= 0 || hitList.Contains(d)) continue;
                    Pos(d, out double x, out double y);
                    near.Add((d, Math.Sqrt((x - ax) * (x - ax) + (y - ay) * (y - ay)), x, y));
                }
                near.Sort((p, q) => p.dist.CompareTo(q.dist));
                for (int i = 0; i < Math.Min(3, near.Count); i++)
                {
                    Push(DebtEv.Bolt, ax, ay, null, 0, 2, near[i].x, near[i].y);
                    Damage(near[i].d, Math.Max(1, Math.Floor(dmg / 2)), 0);
                }
            }
            if (Equipped("rain") && ++R.rainCount % 3 == 0)
            {
                for (int i = 0; i < 4 && R.debris.Count > 0; i++)
                {
                    var d = R.debris[rng.Next(R.debris.Count)];
                    if (d.hp <= 0) continue;
                    Pos(d, out double x, out double y);
                    Push(DebtEv.Bolt, x + 40, y - 160, null, 0, 3, x, y);
                    Damage(d, dmg, 0);
                }
            }
            Push(DebtEv.Slam, ax, ay, null, Rad, hit ? 1 : 0);
        }

        /// <summary>한 프레임. aimOn = 커서가 궤도 위에 있다 (없으면 집게가 안 친다).</summary>
        public void Tick(double dt, double ax, double ay, bool aimOn)
        {
            if (R.over) return;
            R.t += dt;
            M.playSeconds += dt;
            foreach (var d in R.debris)
            {
                d.a += d.sp * dt; d.hit = Math.Max(0, d.hit - dt); d.fade = Math.Min(1, d.fade + dt * 1.5);
                if (Equipped("net") && aimOn && d.hp > 0)
                {
                    Pos(d, out double x, out double y);
                    if (Math.Sqrt((x - ax) * (x - ax) + (y - ay) * (y - ay)) < 180)
                    {
                        double ta = Math.Atan2((ay - EY) / Squash, ax - EX), da = ta - d.a;
                        da = Math.Atan2(Math.Sin(da), Math.Cos(da));
                        d.a += da * dt * 0.8;
                    }
                }
            }
            int before = R.debris.Count;
            R.debris.RemoveAll(d => d.hp <= 0);
            for (int i = R.debris.Count; i < before; i++) Spawn(false);

            if (aimOn && R.fuel > 0) { R.next -= dt; if (R.next <= 0) { R.next += Gap; Strike(ax, ay); } }
            if (Equipped("drone") && R.debris.Count > 0)
            {
                R.droneT -= dt;
                if (R.droneT <= 0)
                {
                    R.droneT = 1.2;
                    var d = R.debris[rng.Next(R.debris.Count)];
                    Pos(d, out double x, out double y);
                    Push(DebtEv.Bolt, EX, EY - 62, null, 0, 4, x, y);
                    Damage(d, Math.Max(1, Math.Floor(Pow / 2)), 0);
                }
            }
            if (R.fuel <= 0) { R.endT += dt; if (R.endT > 1.2) EndRun(); }
        }

        void EndRun()
        {
            R.over = true;
            M.bestChain = Math.Max(M.bestChain, R.chainBest);
            if (!S.won && S.bill < Bills.Length)
            {
                if (!S.overdue)
                {
                    S.billDue--;
                    if (S.billDue <= 0) { S.overdue = true; Push(DebtEv.Overdue); }
                }
                else { S.billAmount = Math.Ceiling(BillAmount * 1.1); S.overRuns++; }   // 연체료
            }
            Push(DebtEv.RunEnd);
        }

        // ───────────────────────── 정비소

        public bool Buy(int i)
        {
            var n = Tree[i];
            if (S.lv[i] >= n.max) return false;
            double c = Cost(i);
            if (S.cash < c) return false;
            S.cash -= c;
            S.lv[i]++;
            if (n.branch == "장비" && S.equip.Count < S.slots && !S.equip.Contains(i)) S.equip.Add(i);
            return true;
        }

        public void ToggleEquip(int i)
        {
            if (S.lv[i] == 0) return;
            if (S.equip.Contains(i)) S.equip.Remove(i);
            else if (S.equip.Count < S.slots) S.equip.Add(i);
        }

        public bool PayBill()
        {
            if (S.won || S.bill >= Bills.Length) return false;
            double m = BillAmount;
            if (S.cash < m) return false;
            S.cash -= m;
            M.credit += CreditFor(Bills[S.bill].m);
            switch (S.bill)
            {
                case 0: S.perkFuel += 5; break;
                case 1: S.vaults = true; break;
                case 2: S.maxOrbit = Math.Max(S.maxOrbit, 1); break;
                case 3: S.wrecks = true; break;
                case 4: S.slots = 2; break;
                case 5: S.maxOrbit = 2; break;
                case 6: S.chainHops = 2; break;
                case 7: S.won = true; break;
            }
            Push(DebtEv.BillPaid, 0, 0, Bills[S.bill].t + " 납부 — " + Bills[S.bill].perk);
            S.overdue = false; S.overRuns = 0; S.billAmount = -1;
            if (S.won) { Push(DebtEv.Won); return true; }
            S.bill++;
            S.billDue = (S.bill < Bills.Length ? Bills[S.bill].due : 5) + Cr("talk");
            return true;
        }

        public void Bankrupt()
        {
            M.bankrupt++; M.company++;
            S = Fresh();
            R = new DebtRun { over = true };
            Push(DebtEv.Bankrupt, 0, 0, "주식회사 궤도 청소부 (" + (M.company - 1) + "대) — 파산");
        }

        public bool BuyCareer(int i)
        {
            var c = Careers[i];
            if (M.credit < c.cost || M.career[i] >= c.max) return false;
            M.credit -= c.cost;
            M.career[i]++;
            return true;
        }

        public void SetOrbit(int o) { if (o <= S.maxOrbit) S.orbit = o; }
    }
}
