using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>📊 출동 결과 — 곳별 피해 도넛 · 목록 (09-27 · 시안 https://claude.ai/artifact/YKcUqfg3fbM4Ewzdksm6qd). 1% 안 되는 건 「기타」로.</summary>
    public partial class SweepHud
    {
        static readonly Color[] DmgCol =
        {
            new Color(1f, 0.76f, 0.35f),                                           // 주 무기 — 배마다 덧칠
            new Color(1f, 0.3f, 0.37f), new Color(0.62f, 0.85f, 1f), new Color(0.37f, 0.9f, 0.78f), new Color(1f, 0.6f, 0.24f),
            new Color(0.75f, 0.94f, 1f), new Color(1f, 0.82f, 0.4f), new Color(0.77f, 0.61f, 1f), new Color(0.92f, 0.95f, 1f),
            new Color(0.5f, 0.88f, 0.54f), new Color(1f, 0.48f, 0.27f), new Color(0.54f, 0.36f, 1f), new Color(0.88f, 0.63f, 1f),
        };
        static readonly Color DmgEtc = new Color(0.29f, 0.33f, 0.43f);
        Texture2D donutTex; float donutK = -1; SweepRun donutRun;
        readonly System.Collections.Generic.List<(string n, double v, Color c)> dmgItems = new System.Collections.Generic.List<(string, double, Color)>();

        void DmgChart(Rect RT, SweepRun R, float tally)
        {
            // 조각 — 많은 순, 1% 안 되는 건 기타
            if (donutRun != R) { donutRun = R; donutK = -1; }
            dmgItems.Clear();
            double tot = 0; for (int i = 0; i < SweepSim.DmgCatN; i++) tot += R.dmgBy[i];
            if (tot <= 0) { GUI.Label(new Rect(RT.x + 18, RT.y + 44, RT.width - 36, 20), Loc.T("<size=12><color=#5a6475>이번 판엔 피해를 못 넣었다</color></size>"), label); return; }
            double etc = 0;
            for (int i = 0; i < SweepSim.DmgCatN; i++)
            {
                double v = R.dmgBy[i]; if (v <= 0) continue;
                if (v / tot < 0.01) { etc += v; continue; }
                string nm = i == 0 ? SweepSim.Ships[sim.Ship].weapon : SweepSim.DmgCatName[i];
                Color c = i == 0 ? ShipCol[Mathf.Clamp(sim.Ship, 0, ShipCol.Length - 1)] : DmgCol[i];
                dmgItems.Add((nm, v, c));
            }
            dmgItems.Sort((a, b) => b.v.CompareTo(a.v));
            if (etc > 0) dmgItems.Add((Loc.T("기타"), etc, DmgEtc));

            // 도넛 — 결과가 열리며 한 바퀴 차오른다
            var dr = new Rect(RT.x + 14, RT.y + 28, 86, 86);
            float k = Mathf.Clamp01(tally);
            if (Event.current.type == EventType.Repaint && (donutTex == null || Mathf.Abs(k - donutK) > 0.01f)) { donutK = k; BuildDonut(tot, k); }
            if (donutTex != null) GUI.DrawTexture(dr, donutTex);
            GUI.Label(new Rect(dr.x, dr.center.y - 18, dr.width, 18), Loc.T("<size=11><color=#8a93a3>총 피해</color></size>"), center);
            GUI.Label(new Rect(dr.x - 6, dr.center.y - 2, dr.width + 12, 20), "<size=11><color=#ffdf95>" + KNum.Short(tot * k) + "</color></size>", center);

            // 목록 — 두 줄로 (칸마다 이름 · 피해 · %)
            float lx = dr.xMax + 12, ly = RT.y + 28, cw = (RT.xMax - 12 - lx) / 2, rh = 17f;
            int rows = Mathf.CeilToInt(dmgItems.Count / 2f);
            if (rows > 5) rh = 86f / rows;
            for (int i = 0; i < dmgItems.Count; i++)
            {
                var it = dmgItems[i]; int col = i < rows ? 0 : 1, row = i < rows ? i : i - rows;
                float x = lx + col * cw, y = ly + row * rh;
                GUI.color = it.c; GUI.DrawTexture(new Rect(x, y + 5, 9, 9), white); GUI.color = Color.white;
                string pc = (it.v / tot * 100).ToString(it.v / tot < 0.1 ? "0.0" : "0") + "%";
                GUI.Label(new Rect(x + 13, y - 1, cw - 13, 20), "<size=11>" + (i == 0 ? "<color=#ffdf95>" + it.n + " ★</color>" : "<color=#c8d0dc>" + it.n + "</color>") + "</size>", label);
                GUI.Label(new Rect(x + 13, y - 1, cw - 16, 20), "<size=11><color=#6f7a8c>" + KNum.Short(it.v) + "</color> <color=#e7ecf8>" + pc + "</color></size>", cost);
            }
        }

        void BuildDonut(double tot, float k)
        {
            const int N = 96;
            if (donutTex == null) donutTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float r1 = N * 0.5f - 1, r0 = r1 * 0.6f;
            // 조각 끝 각도 (위에서 시계 방향)
            var ends = new float[dmgItems.Count]; double acc = 0;
            for (int i = 0; i < dmgItems.Count; i++) { acc += dmgItems[i].v; ends[i] = (float)(acc / tot); }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - N / 2f, dy = y + 0.5f - N / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r1 || d < r0) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                    float a = Mathf.Atan2(dx, dy) / (Mathf.PI * 2); if (a < 0) a += 1;      // 위 = 0, 시계 방향
                    if (a > k) { px[y * N + x] = new Color32(26, 34, 54, 255); continue; }
                    int s = 0; while (s < ends.Length - 1 && a > ends[s]) s++;
                    bool edge = s > 0 && a - ends[s - 1] < 0.006f;                   // 조각 사이 가는 틈
                    px[y * N + x] = edge ? new Color32(8, 10, 16, 255) : (Color32)dmgItems[s].c;
                }
            donutTex.SetPixels32(px); donutTex.Apply();
        }
    }
}
