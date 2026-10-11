using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🧬 10-10 계열(빌드) 화면 — 정비고 오른쪽 판(칸을 안 골랐을 때) · 칸 설명 · 보상 카드의 계열 표시 · 단계가 오르면 알림.
    /// 규칙은 SweepSim.Builds.cs (칸 단계 · 부품 · 배를 세어 3 / 6 / 9 점마다 보너스).
    /// </summary>
    public partial class SweepHud
    {
        public static readonly Color[] FamCol =
        {
            new Color(0.56f, 0.85f, 1f), new Color(1f, 0.91f, 0.4f), new Color(1f, 0.55f, 0.25f),
            new Color(0.7f, 0.48f, 1f), new Color(0.5f, 0.94f, 0.82f), new Color(0.56f, 0.88f, 0.48f),
        };
        static Texture2D[] famTex;
        public static Texture2D FamIcon(int f)
        {
            if (famTex == null) { famTex = new Texture2D[SweepSim.FamCount]; for (int i = 0; i < famTex.Length; i++) famTex[i] = Resources.Load<Texture2D>("fam/fam_" + i); }
            return f >= 0 && f < famTex.Length ? famTex[f] : null;
        }
        static string FamHex(int f) => ColorUtility.ToHtmlStringRGB(FamCol[f]);
        static string Pts(int n) => Loc.En ? n + (n == 1 ? " pt" : " pts") : n + "점";       // 10-10 「단계」는 다른 곳에서 빈칸으로 번역돼 있어 따로
        static string TierTxt(int t) => Loc.En ? "Tier " + t : t + "단계";

        /// <summary>계열 하나 — 아이콘 · 이름 · 점수 막대(9칸, 3 · 6 · 9 마디) · 지금 보너스 또는 다음 보너스</summary>
        float FamRow(float x, float y, float w, int f, int pts)
        {
            int tier = SweepSim.TierOf(pts); var c = FamCol[f];
            var ic = FamIcon(f);
            GUI.color = tier > 0 ? Color.white : new Color(1, 1, 1, 0.55f);
            if (ic != null) GUI.DrawTexture(new Rect(x, y, 24, 24), ic, ScaleMode.ScaleToFit);
            else { GUI.color = c; GUI.DrawTexture(new Rect(x + 4, y + 4, 16, 16), texDisc); }
            GUI.color = Color.white;
            Lbl(new Rect(x + 30, y, 90, 20), "<size=13><color=#" + FamHex(f) + ">" + SweepSim.FamName[f] + "</color></size>", label);
            Lbl(new Rect(x + 30, y, w - 30, 20), "<size=12><color=" + (tier > 0 ? "#ffdf95" : "#6f7a8c") + ">" + Pts(pts) + (tier > 0 ? " · " + TierTxt(tier) : "") + "</color></size>", cost);
            float bx = x + 30, bw = w - 30, seg = bw / 9f;
            for (int i = 0; i < 9; i++)
            {
                bool on = i < pts;
                GUI.color = on ? c : new Color(0.12f, 0.16f, 0.22f); GUI.DrawTexture(new Rect(bx + i * seg + 1, y + 21, seg - 2, 5), white);
                if (i == 2 || i == 5 || i == 8) { GUI.color = on ? Color.white : new Color(0.3f, 0.36f, 0.45f); GUI.DrawTexture(new Rect(bx + i * seg + seg - 2, y + 19, 2, 9), white); }
            }
            GUI.color = Color.white;
            string line = tier >= 3 ? "<color=#" + FamHex(f) + ">" + SweepSim.FamBonus[f][2] + "</color>"
                        : "<color=#8a96aa>" + Loc.T("다음") + " " + Pts(SweepSim.FamStep[tier]) + " · </color><color=#c4cce2>" + SweepSim.FamBonus[f][tier] + "</color>";
            var lw = new GUIStyle(label) { wordWrap = true, fontSize = 10 };   // 줄 높이를 글자 크기로 재게 (<size> 로만 주면 큰 글자로 쟀다)
            Lbl(new Rect(bx, y + 27, bw, 30), KWrapRich(line.Replace(" → ", "→"), 10, bw - 4), lw);   // 낱말 단위 (「서 / 리 원」으로 끊겼다) · 화살표는 앞뒤 숫자와 붙여 「×2 → / ×2.5」로 안 끊기게 (10-11)   // 영어 보너스는 두 줄
            return y + 58;
        }

        void FamBoard(float x, float y, float w)
        {
            var p = sim.FamPoints();
            Lbl(new Rect(x, y, w, 22), "<size=16><color=#e8f1fb>" + Loc.T("빌드 — 계열") + "</color></size>", label); y += 24;
            var ws = new GUIStyle(label) { wordWrap = true, fontSize = 10 };   // 10-11 줄 높이를 글자 크기로
            Lbl(new Rect(x, y, w, 40), "<size=10><color=#8a96aa>" + string.Join("\n", System.Array.ConvertAll(Loc.T("점수 = 칸 단계 · 끼운 부품 · 타는 배\n3 · 6 · 9점마다 보너스").Split('\n'), ln => KWrap(ln, 10, w))) + "</color></size>", ws); y += 42;
            // 점수 높은 계열부터
            var order = new int[SweepSim.FamCount]; for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => p[b] != p[a] ? p[b].CompareTo(p[a]) : a.CompareTo(b));
            foreach (int f in order) y = FamRow(x, y, w, f, p[f]);
        }

        /// <summary>칸 · 부품 · 배에 붙는 작은 계열 딱지</summary>
        void FamChip(Rect r, int f, string tail = null)
        {
            if (f < 0) return;
            var ic = FamIcon(f);
            if (ic != null) GUI.DrawTexture(new Rect(r.x, r.y + (r.height - 16) / 2, 16, 16), ic, ScaleMode.ScaleToFit);
            Lbl(new Rect(r.x + 19, r.y, r.width - 19, r.height), Fit("<color=#" + FamHex(f) + ">" + SweepSim.FamName[f] + "</color>" + (tail != null ? "<color=#8a96aa> " + tail + "</color>" : ""), 11, r.width - 21, label, 8), label);
        }

        // 단계가 오르면 위쪽 띠로 알린다 (칸을 사거나 부품을 끼운 그 순간)
        int[] famSeen;
        void FamWatch()
        {
            if (sim == null || !sim.R.over) return;
            var p = sim.FamPoints();
            if (famSeen == null || famSeen.Length != p.Length) { famSeen = new int[p.Length]; for (int i = 0; i < p.Length; i++) famSeen[i] = SweepSim.TierOf(p[i]); return; }
            for (int f = 0; f < p.Length; f++)
            {
                int t = SweepSim.TierOf(p[f]);
                if (t > famSeen[f]) { Guide("<color=#" + FamHex(f) + ">" + SweepSim.FamName[f] + " " + TierTxt(t) + "</color>   " + SweepSim.FamBonus[f][t - 1]); OrbitSfx.Play("buy", 0.8f); }   // 10-11 이름표는 색으로 · 줄표 겹침(「— 냉동 빔 각성 — …」) 없게
                famSeen[f] = t;
            }
        }
    }
}
