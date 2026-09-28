using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🌳 트리 — 칸 값 · 칸 상태 · 구역 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ───────────────────────── 트리
        public double Cost(int i) => CostAt(i, S.lv[i]);
        public const double CostMul = 4.5;                                   // 💰 09-26 사장님 「아직도 너무 싸」 — 칸 값 전체 배수 (봇으로 맞춤)
        double CostAt(int i, int l) { var n = Nodes[i]; return Math.Ceiling(CostMul * ZoneCostK[Math.Min(ZoneOpen, ZoneCostK.Length - 1)] * EffFirst(i) * Math.Pow(n.mult, l) * (1 - 0.15 * Cr(2)) * (1 - 0.05 * Lv("e_used")) * (n.id == "c_pow" && l < 3 ? PowEarlyK : 1) * (n.branch == "drone" ? DroneCostK : n.branch == "bh" ? BhCostK : 1)); }
        public static double DroneCostK = 2.5, BhCostK = 2.5;                    // 💰 09-27 밤 사장님 「드론이 센 건 드론 칸 값을, 블랙홀도」 — 드론 · 블랙홀 줄 칸 값 배수
        public static double PowEarlyK = 0.3;
        // 🪜 09-27 밤 사장님 「스킬 찍다가 갑자기 싸지는 구간 — 흡입 반경 1」: 뒤에 붙은 칸 첫 값이 붙은 앞 칸 값보다 쌌다 (134칸 중 40칸).
        //    뒤 칸 첫 값 ≥ 붙은 앞 칸(그 칸 단계) 값 × ChildStepK — 트리를 따라가면 늘 오른다. 빔 위력(초반 할인) · 행성 항로 뒤는 뺀다
        public static double ChildStepK = 1.15;
        static double[] firstEff;
        static double EffFirst(int i)
        {
            if (firstEff == null)
            {
                var fe = new double[Nodes.Length];
                for (int k = 0; k < Nodes.Length; k++) fe[k] = Nodes[k].first;
                for (int pass = 0; pass < 16; pass++)
                    for (int k = 0; k < Nodes.Length; k++)
                    {
                        var n = Nodes[k];
                        if (n.id.StartsWith("p_") || !Layout.TryGetValue(n.id, out var pl) || pl.par == "R" || pl.par.StartsWith("p_") || pl.par == "c_pow" || pl.tile <= 0 || !NodeIx.ContainsKey(pl.par)) continue;
                        int pi = NodeIx[pl.par]; var pn = Nodes[pi];
                        int a = pl.tile > 1 ? TileLv(pi, pl.tile - 1) : 0, b = TileLv(pi, pl.tile);
                        double pc = 0; for (int l = a; l < b; l++) pc += fe[pi] * Math.Pow(pn.mult, l);
                        if (fe[k] < pc * ChildStepK) fe[k] = pc * ChildStepK;
                    }
                firstEff = fe;
            }
            return firstEff[i];
        }                                    // 🔰 09-27 사장님 「완전 처음이 어렵다 — 빔 위력 1 · 2 칸을 싸게」 (17 · 68 → 6 · 21)

        // 🔴 칸 = 한 번 사기 (사장님 09-23: "한 칸에 1/3 이런식 말고 무조건 다음칸으로 넘어가지는 방식")
        //    레벨이 여럿인 칸은 많아야 셋으로 나눈다 — 한 칸이 여러 레벨을 한꺼번에 올리고, 가격은 그 레벨들 값을 합친 것
        public static bool Infinite(int i) => false;                     // ∞ 무한 칸은 없앴다 — 5단계 일반 칸 (09-26)
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
            if (KeyNodes.Contains(id)) S.keys--;   // 🔑 파산해도 남는다
            if (id == "e_shop") RollShop();
            int pi = Array.IndexOf(PlanetNode, id); if (pi > 0) PlanetBought(pi);
            if (id == "e_quest" && S.lv[i] == 1 && S.contract < 0) RollContract();
            Emit(SwEv.NodeBought, 0, 0, i, S.lv[i]);
            return true;
        }
        public bool BranchOpen(string br) => true;                        // 가지는 처음부터 다 보인다 — 값으로만 막는다 (09-24)
        // 🪐 행성 구역 (09-24 사장님 6·21번 「지구에선 여기까지 · 다 찍어야 다음 행성」) — 칸마다 구역(= 항로 순위).
        //    구역은 첫 가격으로 나누고 부모보다 앞설 수 없다. 항로 칸은 앞 행성 구역. 핵심 · 무한 · 네 번째 고리는 「다 찍기」에서 뺀다
        public static readonly double[] ZoneCost = { 120, 1200, 12000, 150000, 2000000 };   // 구역 칸 합 ≈ 다음 항로 값 (봇으로 맞춤)   // 지구 · 달 · 화성 · 소행성대 · 목성 · (그 위 토성)
        public static readonly string[] ZoneName = { Loc.T("지구"), Loc.T("달"), Loc.T("화성"), Loc.T("소행성대"), Loc.T("목성"), Loc.T("토성"), Loc.T("천왕성"), Loc.T("해왕성"), Loc.T("카이퍼 벨트"), Loc.T("오르트 구름"), Loc.T("태양권 계면"), Loc.T("성간 공간") };
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
        public static bool ZoneNeed(int i) { var id = Nodes[i].id; return !id.StartsWith("p_") && id != "e_shop" && !KeyNodes.Contains(id) && !Infinite(i) && !Ring4(id) && !(id.StartsWith("w_") && id.EndsWith("_e")) && !id.StartsWith("l_"); }   // 무기 특화 · 복권 칸은 더 파고드는 선택 — 다 찍기에서 뺀다 (09-25 밸런스: 토성 구역 3480만 중 1800만이 레일건 특화)
        public int ZoneLeft(int z) { int c = 0; for (int i = 0; i < Nodes.Length; i++) if (Zone[i] == z && ZoneNeed(i) && S.lv[i] <= 0) c++; return c; }

        public NodeSt State(int i)
        {
            var n = Nodes[i];
            if (Retired(i)) return NodeSt.Locked;
            if (!BranchOpen(n.branch)) return NodeSt.Locked;
            if (S.lv[i] >= n.max) return NodeSt.Max;
            if (Ring4(n.id) && Lv("p_jup") <= 0) return NodeSt.Locked;           // ✦ 외행성 면허 = 목성 항로
            if (Zone[i] > ZoneOpen) return NodeSt.Locked;                          // 🪐 그 행성 항로를 사야 열린다
            if (n.id.StartsWith("p_")) return NodeSt.Locked;                     // 🛰 항로는 관문을 부숴야 열린다 (09-26 — 구역 칸 개수 · 허가증 돈은 없앴다)
            if (CapLocked(i)) return NodeSt.Locked;                             // 🛰 행성 한도 — 이 행성에선 여기까지
            foreach (var p in n.par) if (S.lv[NodeIx[p]] <= 0) return NodeSt.Hidden;
            var pl = Layout[n.id];
            if (pl.par != "R" && S.lv[NodeIx[pl.par]] < TileLv(NodeIx[pl.par], pl.tile)) return NodeSt.Hidden;
            if (KeyNodes.Contains(n.id) && S.keys < 1) return NodeSt.Poor;          // ◆ 열쇠가 없다
            return S.cash >= TileCost(i) ? NodeSt.Can : NodeSt.Poor;
        }
        public bool Buy(int i) => BuyTile(i);

    }
}
