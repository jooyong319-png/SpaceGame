using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🧬 10-10 사장님 「빌드 — 이번 판엔 얼음쪽으로 쭉 파볼까?」
    // 계열 여섯. 정비고 칸(단계, 칸마다 3까지) · 장착한 부품(1) · 타는 배(2)를 세어 3 / 6 / 9 점마다 계열 보너스가 켜진다.
    // 회사마다 처음부터라(트리 · 부품이 파산하면 비워진다) 회사마다 다른 계열을 시험해 볼 수 있다.
    public sealed partial class SweepSim
    {
        public const int FamFrost = 0, FamShock = 1, FamBlast = 2, FamHole = 3, FamDrone = 4, FamMoney = 5, FamCount = 6;
        public static readonly string[] FamName = { Loc.T("얼음"), Loc.T("전기"), Loc.T("폭발"), Loc.T("블랙홀"), Loc.T("드론"), Loc.T("돈") };
        public static readonly int[] FamStep = { 3, 6, 9 };
        public static readonly string[][] FamBonus =
        {
            new[] { Loc.T("언 잔해 피해 ×2 → ×2.5"), Loc.T("냉동 빔 발동 ×1.5 · 서리 원 +20%"), Loc.T("냉동 빔 각성 — 산산조각이 옆을 얼림") },
            new[] { Loc.T("레이저 · 번개 · 레일건 발동 ×1.5"), Loc.T("번개 튀는 수 +3 · 전격선 번개 +1줄"), Loc.T("번개 각성 — 튄 자리마다 폭발") },
            new[] { Loc.T("모든 폭발 범위 +20%"), Loc.T("폭발 번짐 +15% · 번지는 한도 +40"), Loc.T("분열탄 각성 — 파편이 한 번 더 분열") },
            new[] { Loc.T("블랙홀 확률 ×1.3"), Loc.T("흡입 반경 +30% · 용량 +20"), Loc.T("쌍둥이 블랙홀 — 터진 자리에 한 번 더") },
            new[] { Loc.T("드론 +2대"), Loc.T("드론 속도 +30%"), Loc.T("드론이 부순 값 ×1.5") },
            new[] { Loc.T("모든 값 +10%"), Loc.T("적금 이자 +2% · 상인 물건 −15%"), Loc.T("모든 값 ×1.2") },
        };
        static readonly Dictionary<string, int> FamOfNode = BuildFamOfNode();
        static Dictionary<string, int> BuildFamOfNode()
        {
            var m = new Dictionary<string, int>();
            void Add(int f, params string[] ids) { foreach (var id in ids) m[id] = f; }
            foreach (var w in new[] { "frz" }) Add(FamFrost, "w_" + w, "w_" + w + "_u", "w_" + w + "_a", "w_" + w + "_e", "w_" + w + "_x");
            foreach (var w in new[] { "laser", "chain", "rail" }) Add(FamShock, "w_" + w, "w_" + w + "_u", "w_" + w + "_a", "w_" + w + "_e", "w_" + w + "_x");
            foreach (var w in new[] { "mine", "clus" }) Add(FamBlast, "w_" + w, "w_" + w + "_u", "w_" + w + "_a", "w_" + w + "_e", "w_" + w + "_x");
            Add(FamBlast, "b_br", "b_chain", "q_meteor");
            foreach (var w in new[] { "vac", "mag" }) Add(FamHole, "w_" + w, "w_" + w + "_u", "w_" + w + "_a", "w_" + w + "_e", "w_" + w + "_x");
            Add(FamHole, "b_n", "s_speed", "b_pr", "b_cap", "b_pf", "b_pack", "q_sling", "m_bh", "i_bh", "k_bh");
            Add(FamDrone, "d_n", "d_spd", "d_reach", "d_mag", "d_sig", "d_grade", "d_fix", "d_pair", "d_fact", "m_drone", "m_dcount", "i_drone", "k_drone", "x_arm_drone", "x_drone_bh");
            Add(FamMoney, "e_val", "e_vault", "e_att", "e_quest", "e_talk", "e_tip", "e_save", "e_guard", "e_used", "e_shop", "a_open", "a_auto", "a_read", "a_ins", "a_big",
                "q_insider", "q_rage", "q_front", "q_debt", "q_tour", "q_gold", "l_more", "l_luck", "l_free", "l_jack", "l_wild", "l_again", "m_val", "i_eco", "k_eco", "x_eco_route", "x_bh_eco");
            return m;
        }
        public static int NodeFam(string id) => id != null && FamOfNode.TryGetValue(id, out int f) ? f : -1;
        public static readonly int[] ShipFam = { -1, FamBlast, FamShock, FamShock, FamBlast, FamFrost, FamBlast };   // 낡은 · 산탄 · 작살 · 전격 · 미사일 · 냉동 · 분열탄
        public static int PartFam(int id)
        {
            if (id < 0 || id >= Parts.Defs.Length) return -1;
            switch (Parts.Defs[id].k[0])
            {
                case "fuel": case "frost": case "frz": case "frzp": return FamFrost;   // 냉각 · 연료
                case "crit": case "dbl": case "shockp": case "zap": return FamShock;
                case "rad": case "combo": case "blast": case "spread": return FamBlast;
                case "hole": return FamHole;
                case "drone": return FamDrone;
                case "val": case "vault": case "att": case "cut": case "fee0": case "div": return FamMoney;
            }
            return -1;
        }

        /// <summary>계열 점수 — 칸 단계(칸마다 3까지) + 장착 부품 1 + 배 2</summary>
        public int[] FamPoints()
        {
            var p = new int[FamCount];
            for (int i = 0; i < Nodes.Length && i < S.lv.Length; i++) { int f = NodeFam(Nodes[i].id); if (f >= 0 && S.lv[i] > 0) p[f] += Math.Min(3, S.lv[i]); }
            if (S.parts != null) foreach (var id in S.parts) { int f = PartFam(id); if (f >= 0) p[f]++; }
            int sf = Ship >= 0 && Ship < ShipFam.Length ? ShipFam[Ship] : -1; if (sf >= 0) p[sf] += 2;
            return p;
        }
        public static int TierOf(int pts) { int t = 0; foreach (var s in FamStep) if (pts >= s) t++; return t; }
        // 판 도중엔 틱마다 한 번만 센다 (블랙홀 확률 · 드론 · 발동 확률이 맞힐 때마다 묻는다)
        readonly int[] famT = new int[FamCount]; double famAt = -1; SweepRun famRun;
        public int FamT(int f)
        {
            if (R == null || R.over || famRun != R || famAt != R.t) { var p = FamPoints(); for (int i = 0; i < FamCount; i++) famT[i] = TierOf(p[i]); if (R != null && !R.over) { famRun = R; famAt = R.t; } else famRun = null; }
            return famT[f];
        }
        double FamProcK(int w) => w == 5 ? (FamT(FamFrost) >= 2 ? 1.5 : 1) : (w == 1 || w == 2 || w == 8) ? (FamT(FamShock) >= 1 ? 1.5 : 1) : 1;
        double PartProc(int w) => w == 5 ? Part("frzp") : (w == 1 || w == 2 || w == 8) ? Part("shockp") : 0;   // 계열 부품 — 그 계열 무기 발동 +%
        bool Awk(string w, int fam) => Lv("w_" + w + "_a") > 0 || FamT(fam) >= 3;   // 3단계 보너스 = 그 무기 각성
    }
}
