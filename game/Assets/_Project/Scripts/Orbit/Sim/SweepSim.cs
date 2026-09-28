using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🔴 rev17 「궤도 청소부」 규칙 전부 (2026-09-23). 정본 = wiki/rev17-detail.md. UnityEngine 을 안 쓴다 — tools/pacing 봇이 그대로 컴파일한다.
    //    도구 셋: 집게(커서에 대면 저절로 친다) · 드론(알아서 줍는다) · 블랙홀 폭탄(누르면 모으고 떼면 터진다)
    //    흐름: 출동 → 결산 → 정비소(트리 24칸) → 청구서 8장 → 파산 → 빚 청산 → 청산 출동
    //    좌표는 시안과 같은 960×600 「화면 점」 — 지구 (480,310), 궤도는 세로로 0.6 눌린 타원.

    public sealed partial class SweepSim
    {
        public const double EX = 480, EY = 310, Tilt = 0.6;

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
            if (M.up == null || M.up.Length != UpCount) { var u = M.up ?? new int[0]; Array.Resize(ref u, UpCount); M.up = u; }
            for (int i = 0; i < CareerCount; i++) { for (int l = 0; l < M.career[i] && l < Careers[i].cost.Length; l++) M.credit += Careers[i].cost[l]; M.career[i] = 0; }   // 옛 경력 → 신용 환불 (09-26)
            if (M.shipsOwned == 0) M.shipsOwned = 1;
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
            M.perm.Clear();                                               // 🔑 열쇠 칸 이월은 없앴다 (09-26 §7) — 지금 회사가 산 칸은 S.lv 에 그대로 있다
            if (S.layout < 2 && S.lv != null && (S.lv.Length == 126 || S.lv.Length == 130))
            {   // 🩹 09-24 밤 배치(새 칸이 106번에 끼어 있던 판)로 저장된 것 — 그 판에서 보이던 그대로, 칸 이름 기준으로 자리만 옮긴다 (09-25)
                //    밀린 배치: 0~105 같음 · 106~113 무기 특화 8 · (130이면) 114~117 복권 4 · 그 뒤 원래 106~117
                int ins = S.lv.Length - 118;
                var nl = new int[NodeCount];
                for (int k = 0; k < 106; k++) nl[k] = S.lv[k];
                for (int j = 0; j < 12; j++) nl[106 + j] = S.lv[106 + ins + j];      // 레일건 각성 · 교차 핵심 · 무한
                for (int j = 0; j < 8; j++) nl[118 + j] = S.lv[106 + j];             // 무기 특화
                if (ins == 12) for (int j = 0; j < 4; j++) nl[126 + j] = S.lv[114 + j];   // 복권
                S.lv = nl;
            }
            S.layout = 2;
            if (S.lv == null) S.lv = new int[NodeCount];
            else if (S.lv.Length < NodeCount) { var lv = S.lv; Array.Resize(ref lv, NodeCount); S.lv = lv; }   // 칸이 늘면 산 것은 그대로 두고 뒤에 붙인다 (경매 줄기 · 09-23)
            else if (S.lv.Length > NodeCount) { var lv = S.lv; Array.Resize(ref lv, NodeCount); S.lv = lv; }
            for (int i = 0; i < NodeCount; i++) if (S.lv[i] > Nodes[i].max) S.lv[i] = Nodes[i].max;   // 예전 ∞ 칸 — 5단계로
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
        int Cr(int i) => 0;                                                     // 옛 경력 — 영구 강화(Up)로 바꿨다 (09-26)
        void Emit(SwEv k, double x = 0, double y = 0, double v = 0, int kk = 0, string text = null, double x2 = 0, double y2 = 0)
        { if (Events.Count < 4000) Events.Enqueue(new SwEvent { kind = k, x = x, y = y, v = v, k = kk, text = text, x2 = x2, y2 = y2 }); }

        // ───────────────────────── 파생 숫자
        public int Seg => Math.Min(8, S.bill + 1);
        public bool DronesOn => Lv("d_n") > 0;                        // 해금은 전부 정비고 (09-24) — 드론 격납고
        public bool BombsOn => Lv("b_n") > 0;
        public bool WeaponOwned(int w) => w == 0 || (w < WeaponNode.Length && Lv(WeaponNode[w]) > 0);
        public int Weapon => 0;                                                // 🔫 늘 기본 공격 (09-24 사장님 「기본 공격에 효과가 붙는 방식 · % 확률로」). S.weapon 은 옛 저장용
        public static readonly double[] ProcBase = { 0, 0.12, 0.10, 0.08, 0.08, 0.08, 0.06, 0.05, 0.04 };
        public const double ProcK = 1.8;                                          // 🔫 09-26 밤 사장님 「무기들이 너무 약한 것 같아」 — 발동 확률 ×1.8 (0.5초 한 발 · 한 판 30초면 몇 번 안 터졌다)
        static readonly string[] ProcUp = { null, "w_laser_u", "w_chain_u", "w_vac_u", "w_mine_u", "w_frz_u", "w_clus_u", "w_mag_u", "w_rail_u" };
        public double ProcChance(int w) => w <= 0 || w >= ProcBase.Length || !WeaponOwned(w) ? 0 : ProcK * ProcBase[w] * (Lv(ProcUp[w]) >= 1 ? 1.5 : 1) * (Lv("w_slot2") > 0 ? 1.5 : 1) * (Lv(ProcUp[w].Replace("_u", "_x")) > 0 ? 2 : 1);
        void FireW(int w) { wMul = w > 0 && Lv(ProcUp[w].Replace("_u", "_x")) > 0 ? 2 : 1; int sv = dmgCat; dmgCat = w; Fire(w); dmgCat = sv; wMul = 1; }
        // 📊 피해를 준 곳 — 0 주 무기 · 1~8 무기고(레이저 · 번개 · 진공 · 기뢰 · 냉동 · 분열탄 · 자석 · 레일건) · 9 드론 · 10 폭발·연쇄 · 11 블랙홀 · 12 행성 특성 (09-27 사장님 「출동 결과에 무기별 피해 원그래프」)
        public const int DmgCatN = 13;
        public static readonly string[] DmgCatName = { Loc.T("주 무기"), Loc.T("레이저"), Loc.T("번개"), Loc.T("진공"), Loc.T("기뢰"), Loc.T("냉동"), Loc.T("분열탄"), Loc.T("자석"), Loc.T("레일건"), Loc.T("드론"), Loc.T("폭발·연쇄"), Loc.T("블랙홀"), Loc.T("행성 특성") };
        int dmgCat = -1;                                                         // 지금 피해를 주는 곳 (-1 = src 로 정한다)
        public static readonly double[] DbgCat = new double[DmgCatN];           // 봇 진단 — 모든 판 합
        void Tally(double amt, int src)
        {
            if (amt <= 0 || R == null) return;
            int c = dmgCat >= 0 ? dmgCat : src == 1 ? 9 : src == 2 ? 10 : 0;
            R.dmgBy[c] += amt; DbgCat[c] += amt;
        }
        public int OwnedWeapons { get { int n = 0; for (int w = 1; w < ProcBase.Length; w++) if (WeaponOwned(w)) n++; return n; } }
        public int VolleyLv => Math.Min(3, Lv("v_volley"));
        static readonly double[] VolleyDur = { 0, 0.5, 0.8, 1.1 }, VolleyGap = { 0, 0.16, 0.12, 0.09 }, VolleyFill = { 0, 0.5, 0.75, 1 };   // 3단계 = 09-26 전의 세기
        public bool VolleyOn => OwnedWeapons >= 2 && VolleyLv > 0;                            // 🚀 전탄 발사 — 무기 둘부터
        public double VolleyGain => (0.012 + 0.006 * OwnedWeapons * (Lv("w_slot2") > 0 ? 1.3 : 1)) * VolleyFill[VolleyLv];
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
        /// <summary>🚀 전탄 발사를 스킬로 (09-26 사장님 「수동으로 누르게」) — 게이지가 차면 멈춰 기다리고, Space · 단추로 쏜다. 봇 · 자동은 false</summary>
        public bool VolleyManual;
        public bool VolleyReady => VolleyOn && R != null && !R.over && R.volleyT <= 0 && R.volley >= 1;
        public bool FireVolley()
        {
            if (!VolleyReady) return false;
            var r = R; r.fuel = Math.Max(0, r.fuel - VolleyFuel); r.volley = 0; r.volleyT = VolleyDur[VolleyLv]; r.volleyNext = 0.3; Emit(SwEv.Volley, r.ax, r.ay, OwnedWeapons, 0, Loc.T("전탄 발사!"));
            return true;
        }
        void VolleyTick(double dt)
        {
            var r = R;
            if (r.volleyT <= 0) return;
            r.volleyT -= dt; r.volleyNext -= dt;
            if (r.volleyNext > 0) return;
            r.volleyNext = VolleyGap[VolleyLv];
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
            if (VolleyOn && r.volleyT <= 0 && r.volley < 1)
            {
                r.volley += VolleyGain;
                if (r.volley >= 1) { r.volley = 1; if (VolleyManual) Emit(SwEv.SkillReady, r.ax, r.ay, 0, 1); else FireVolley(); }   // 차면 기다린다 — 사람이 누른다 (봇 · 자동은 바로)
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
        public bool ContractsOn => false;                                       // 의뢰는 없앴다 (09-26 사장님 「의미 없다」) — e_quest 칸은 트리에서 뺀다
        public static bool Retired(int i) => Nodes[i].id == "e_quest";
        public bool BigsOn => S.orbit >= 2;                              // 큰 잔해는 화성부터 (행성의 성격)
        public int MaxOrbit { get { int m = 0; foreach (int i in OrbitOrder) if (Open(i)) m = i; return m; } }   // 가장 먼 (순위)
        public bool Open(int i) => i == 0 || (S.planets & (1 << i)) != 0 || Lv(PlanetNode[i]) > 0;
        public bool OnSale(int i) => false;                                      // 🛰 허가증은 없앴다 — 관문을 부숴야 열린다
        public double FuelMax => Math.Max(10, Math.Min(FuelCap, FuelRaw) * (1 + FuelTankPct * Lv("c_fuel") + 0.05 * Up(1)) + Part("fuel") + Lv("d_fix"));   // ⛽ 연료 탱크 = 용량 +6% (상한 위에 곱한다 — 09-26 사장님 「용량이 몇 퍼센트 늘었다가 맞을 듯」)
        public const double FuelTankPct = 0.03;   // ⛽ 09-26 사장님 「30초도 김」 — 한 판은 20초 남짓으로 묶는다
        public const double FuelCap = 22;
        double FuelRaw => Math.Max(12, 20.0 * (1 + 0.2 * Cr(0)) * (Lv("k_claw") > 0 ? 0.85 : 1)) * (1 + 0.25 * Lv("m_fuel"));   // ⚠ 트리 연료 칸은 상한에 막혀 거의 안 듣는다 — 트리 압축 때 다른 효과로
        public double Gap => Ship == 3 ? TeslaGap : Ship == 4 ? MissileGap : Ship == 5 ? FrostGap : Ship == 6 ? ClusGap : 0.5;                        // 🚀 레일건선은 느리게 (09-27)                                              // 연사 속도는 없앴다 — 수동 공격 (09-26). 빔 증폭(c_spd)은 화력으로
        public double ClawR => Lv("c_rad") > 0 ? (20 + 5 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2)) : 0;   // 0 = 하나씩 · 09-26 사장님 「범위가 너무 커진다」 — 최대 102 → 60 (단계 수는 그대로)
        public bool AutoClaw => true;        // 🔴 자동이 기본 (사장님 09-23: "클릭은 빼자 오토는 기본으로")
        public const double PickR = 30;      // 범위 강화 전 — 커서 밑 하나를 잡는 거리
        public int ClawDmg => 1 + Lv("c_pow");
        public double Part(string k) => Parts.Sum(S.parts, k);
        public double DmgMul => Math.Pow(1.5, Lv("m_claw")) * wMul * RawDmgMul * (R != null ? 1 + R.consDmg / 100.0 : 1);   // ✦ 과충전 포신 · 무기 3단계 위력
        double wMul = 1;
        double RawDmgMul => 1 + 0.08 * Up(0) + 0.08 * Lv("c_spd") + Part("dmg") + Part("spd") + (Lv("k_claw") > 0 ? 0.4 : 0) + TapBonus + Rage + Grit + 0.10 * Lv("i_claw") + (Lv("x_route_claw") > 0 ? new[] { 0, 0.05, 0.1, 0.2, 0.35 }[Math.Min(4, S.orbit)] : 0);
        public double TapBonus => Lv("q_lazy") > 0 && R != null ? 0.03 * R.tapCombo : 0;   // ★ 연타 장인 (예전 자리 · id 는 q_lazy 그대로 — 저장 호환)
        public double Rage { get { if (Lv("q_rage") <= 0 || Mk == null) return 0; double v = 0, c = 0; foreach (var s in Mk.M.st) if (s.shares > 0) { v += s.shares * s.price; c += s.cost; } return c > 0 ? Math.Min(0.5, Math.Max(0, 1 - v / c)) : 0; } }   // ★ 물린 개미의 분노
        public double Grit => Lv("q_debt") > 0 && S.debt > 0 ? Math.Min(0.15, S.debt / Math.Max(1, BillAmount) * 0.1) : 0;   // ★ 빚쟁이의 근성
        public double Pow => ClawDmg * DmgMul * clickMul;                                  // 무기 화력 (소수는 확률로)
        int RoundP(double v) => (int)v + (Rnd() < v - (int)v ? 1 : 0);
        public double HpMul => ((1 + 0.35 * S.bill) * Orbits[S.orbit].hp) * (Lv("k_route") > 0 ? 1.2 : 1) * (M.endless ? Math.Pow(1.15, M.depth) : 1);   // 잔해 체력 배율 — 청구서 한 장마다 +35% (09-26 사장님 「너무 쉽게 부서진다」 — 한 방 비율 80~97% 였다)
        public int BlastDmg => 2 + (int)Math.Round(2 * ClawDmg * Math.Pow(DmgMul, BlastPowK));   // 폭발은 즉사가 아니라 피해 · 09-27 화력 배율도 따른다 (예전엔 집게 위력만 — 뒤 행성에서 기뢰 · 분열탄 · 자석이 거의 못 깎았다)
        public static double BlastPowK = 1;
        public double Crit => 0.03 * Up(4) + 0.05 * Lv("c_crit") + Part("crit") + (Lv("x_claw_arm") > 0 ? 0.1 : 0);
        public int CritX => (Lv("x_claw_arm") > 0 ? 4 : 3) + Lv("m_crit");
        public int DroneCount => DronesOn ? 1 + Lv("d_n") + Lv("d_fact") + Cr(3) + (Lv("k_drone") > 0 ? 4 : 0) + 2 * Lv("m_dcount") : 0;   // 격납고 첫 칸 = 두 대
        public double DroneCd => Math.Max(0.4, 1 - 0.1 * Lv("d_spd")) * (Lv("x_drone_bh") > 0 && R != null && R.holding ? 0.5 : 1);
        public double Reach => 80 + 15 * Lv("d_reach");
        public int Grade => 1 + Lv("d_grade");
        public int DroneDmg => (int)Math.Ceiling(2 * Grade * HpMul) * (Lv("x_arm_drone") > 0 ? 2 : 1);   // 🛸 09-27 드론 한 방도 잔해 단단함을 따라 — 등급 1 조각 · 2 죽은 위성 · 3 금고 · 4 로켓 (예전엔 1~4 고정이라 화성부터 칠 게 없었다)
        public double DroneMag => (1 + 0.25 * Lv("d_mag")) * (1 + Part("drone")) * (Lv("k_drone") > 0 ? 0.75 : 1) * (1 + 0.10 * Lv("i_drone")) * Math.Pow(1.5, Lv("m_drone"));
        public static double DroneValK = 0.4;                                     // 🛸 09-27 드론이 부순 값 몫 — 드론이 다시 일하게 되면서 난이도를 지키려고 (봇 끝 시간 맞춤)
        public int Bombs => BombsOn ? Math.Min(6, 2 + Lv("b_n") + (S.bill >= 7 ? 1 : 0) + Cr(4)) : 0;
        public double PullR => (260 + 30 * Lv("b_pr")) * (1 + 0.15 * Lv("m_bh"));                // 🌀 09-26 밤 「뭐든지 다 빨아들이게」 → 「범위가 너무 넓어」 — 260 + 30×칸 (예전 90 + 14 · 한때 화면 전체)
        public double PullBase => 500;                                                         // 원 가장자리에서도 끌려오는 기본 힘
        public double PullF => 1 + 0.25 * Lv("b_pf");
        public int Cap => 40 + 15 * Lv("b_cap");
        public double BlastK => (1 + 0.15 * Lv("b_br")) * (1 + 0.06 * Up(10));
        public double ChainP => Math.Min(0.85, 0.3 + 0.07 * Lv("b_chain"));    // 무기 폭발이 또 번질 확률
        public double HoleCd => 16 - 1.5 * Lv("s_speed");     // (옛 시간 충전 — 이제 안 쓴다)
        public double HoleChance => R.clean ? 0.05 : Lv("b_n") <= 0 ? 0 : (0.012 + 0.002 * Lv("b_n") + 0.0025 * Lv("s_speed") + Part("hole") + 0.002 * Lv("i_bh")) * (Lv("k_bh") > 0 ? 0.7 : 1) * (1 + 0.3 * Lv("m_bh"));   // 🌀 블랙홀 — 집게가 맞힐 때 이 확률로 그 자리에 저절로 열린다 (09-24 사장님 「자동으로 바닥에 깔리는 걸로」 · Q 스킬 없앰)
        public const double HoleDur = 3;                           // 열려 있는 시간 — 끝나면 저절로 터진다
        public double PackK => 0.02 + 0.012 * Lv("b_pack");
        // 🔴 한 번 터질 때 이어지는 연쇄의 한계 — 도파민 사다리(§5)가 구간마다 한 단계씩 열리게
        public static double TankR = 30, DetR = 26;   // 09-27 저녁 「아무것도 안 해도 계속 폭발」 — 58 · 50 → 40 · 35                              // 💥 폭발 탱크 · 기폭 장치 반경 (09-27 75 · 65 로 넓혔다가 「너무 터진다」 — 되돌림)
        public const int RedGenMax = 1;                                         // 09-27 밤 「그래도 너무 셈」 — 휩쓸린 폭탄은 안 터진다 (직접 쏜 것만)                                         // 빨간 폭발이 옆 폭탄을 터뜨리는 대 — 직접 부순 것 1 · 그 폭발로 2 · 한 번 더 3 (09-27 「너무 터진다 · 말이 안 된다」)
        public const int PendCap = 80;
        public static double RedBlastVal = 1.5;
        public static double PouchK = 2.5;                                      // 봇 24판: ×4 는 82분 · 파산 2.1, ×2.5 는 137분 · 3.1
        static string KFmt(double v) => Loc.En ? Loc.Num(v) : v >= 1e12 ? (v / 1e12).ToString("0.#") + Loc.T("조") : v >= 1e8 ? (v / 1e8).ToString("0.#") + Loc.T("억") : v >= 1e4 ? (v / 1e4).ToString("0.#") + Loc.T("만") : Math.Round(v).ToString("0");                                 // 봇 24판: ×2 는 파산 2.8 · ×1.5 는 3.3 (바꾸기 전과 같음)                                          // 터질 차례를 기다리는 폭발 한도 — 렉 막기
        public int ChainMax => R.clean ? 5000 : 40 + (S.orbit >= 1 ? 20 : 0) + (S.orbit >= 2 ? 40 : 0) + 15 * Lv("b_chain");
        // 🌪 모래 폭풍 (화성 · 해왕성) — 22초마다 4.5초. 값 ×1.5 · 왼쪽에서 고철이 몰려온다 (09-24 사장님 36번 「무의미함」)
        public bool StormOn => R != null && !R.over && !R.clean && Orbits[S.orbit].storm && R.t % 13.0 >= 6 && R.t % 13.0 < 10.5;   // 09-26 판이 20초 남짓 — 판 중간에 한 번
        public const double ValBase = 1.8;
        // 📈 09-26 사장님 「행성을 넘어갈수록 벌이를 기하급수로 · 스킬도 넘어갈 때 기하급수로 올려 막는다」
        public const double PlanetBase = 3, ZoneCostBase = 2.3;
        public static readonly double[] ZoneCostK = { 1.2, 1.7, 7.4, 51, 100, 175, 400, 3400, 22560, 76500, 139000, 199000 };   // 🪜 09-27 밤 사장님 「싸지는 구간」 — 소행성대 16→51 (목성 앞에서 확 싸졌다) · 카이퍼부터 3600~6000 → 2.3만~20만 (벌이는 행성마다 열 배 넘게 느는데 배수는 거의 그대로라 트리가 공짜처럼). 봇 24판 128분 · 파산 4.3 · 트리 97%
          // 💰 09-27 칸 값 배수 — 열린 관문 수마다 (봇으로 「가장 싼 칸 ≈ 판 벌이 0.6판」에 맞춤 · 예전 2.3^관문)                   // 행성 한 칸 = 벌이 ×3 · 관문을 부술 때마다 아직 안 산 칸 값 ×3 (봇으로 맞춤)
        public static double PlanetMul(int rank) => Math.Pow(PlanetBase, rank);
        public static double PlanetMulOf(int orbit) => PlanetMul(Math.Max(0, Array.IndexOf(OrbitOrder, orbit)));                                   // 💰 09-26 고철 시세 묶음 · 의뢰 폐지로 줄어든 벌이를 되돌린다 (봇으로 맞춤)
        public double ValMult => ValBase * (1 + 0.06 * Up(5)) * (1 + 0.1 * M.legend) * (M.endless ? Math.Pow(1.15, M.depth) : 1) * (StormOn ? 1.5 : 1) * (R != null ? 1 + R.consVal / 100.0 : 1) * Math.Pow(1.25, Lv("e_val")) * Math.Pow(1.3, Cr(1)) * (PlanetMul(Rank) * (1 + (Lv("k_route") > 0 && S.orbit > 0 ? 0.15 : 0) + (S.orbit > 0 ? 0.05 * Lv("i_route") : 0))) * Econ * (1 + Part("val")) * (Lv("k_eco") > 0 ? 1.25 : 1) * (1 + 0.08 * Lv("i_eco")) * PlanetStockBonus * Math.Pow(1.5, Lv("m_val")) * Math.Pow(1.25, Lv("m_route"));
        public double PlanetStockBonus { get { if (Lv("x_eco_route") <= 0 || Mk == null || S.orbit == 0) return 1; string[] ids = { "", "moon", "mars", "jup", "sat", "", "", "", "", "", "", "" }; if (ids[S.orbit] == "") return 1; for (int i = 0; i < Market.Defs.Length && i < Mk.M.st.Count; i++) if (Market.Defs[i].id == ids[S.orbit] && Mk.M.st[i].shares > 0) return 1.2; return 1; } }   // 새 행성은 종목이 없다
        public double Cut => S.debt > 0 ? Math.Max(0.1, (Lv("e_guard") > 0 || Cr(5) > 0 ? 0.2 : 0.3) - Part("cut")) : 0;   // 빚이 있으면 판 수입에서 떼어 상환
    }
}
