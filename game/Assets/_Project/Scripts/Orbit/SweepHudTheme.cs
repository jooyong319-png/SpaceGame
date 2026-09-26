using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🎨 배 안쪽 테마 (09-26 사장님 「청소선 안쪽 테마도 바꿔야 해」) — 조종실 벽 · 계기판 · 출동 단추 · 방 바탕 · 주사선을 배마다 한 벌로.
    /// 0 = 낡은 청소선(지금 그대로) · 1 = 산탄선(광산). 배를 더할 때 여기에 한 벌만 붙인다.
    /// </summary>
    public partial class SweepHud
    {
        public class ShipTheme
        {
            public Color32 bg, plate, rim, edge, deskTop, deskBot;          // 조종실 벽 (BuildHull)
            public Color rivet, plateCol, bezel, screen;                     // 리벳 · 계기판
            public Color room, scan, bayTint;                                // 방 바탕 · 주사선 · 정비고 덧칠
            public Color btnFace, btnHover, btnWall, btnGlow; public string btnInk;   // 출동 단추
            public Color accent;                                             // 테두리 · 탭 강조
            public int deco;                                                 // 0 없음 · 1 경고 줄무늬
        }

        static readonly ShipTheme[] Themes =
        {
            new ShipTheme   // 낡은 청소선 — 녹슨 공업 (지금 그대로)
            {
                bg = new Color32(7, 11, 17, 255), plate = new Color32(12, 19, 28, 255), rim = new Color32(42, 59, 82, 255), edge = new Color32(59, 81, 112, 255),
                deskTop = new Color32(18, 28, 41, 255), deskBot = new Color32(10, 16, 25, 255),
                rivet = new Color(0.2f, 0.27f, 0.36f), plateCol = new Color(0.075f, 0.11f, 0.16f), bezel = new Color(0.15f, 0.21f, 0.29f), screen = new Color(0.03f, 0.05f, 0.075f),
                room = new Color(0.035f, 0.045f, 0.065f), scan = new Color(1f, 0.76f, 0.35f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(0.95f, 0.76f, 0.31f), deco = 0,
            },
            new ShipTheme   // 산탄선 — 광산 (노랑 · 검정 경고 줄무늬 · 흙먼지 벽)
            {
                bg = new Color32(16, 13, 8, 255), plate = new Color32(30, 25, 15, 255), rim = new Color32(70, 56, 18, 255), edge = new Color32(150, 118, 30, 255),
                deskTop = new Color32(38, 31, 17, 255), deskBot = new Color32(19, 16, 10, 255),
                rivet = new Color(0.5f, 0.42f, 0.2f), plateCol = new Color(0.11f, 0.09f, 0.055f), bezel = new Color(0.4f, 0.32f, 0.1f), screen = new Color(0.045f, 0.04f, 0.025f),
                room = new Color(0.06f, 0.05f, 0.03f), scan = new Color(1f, 0.82f, 0.2f, 0.05f), bayTint = new Color(0.3f, 0.22f, 0.02f, 0.12f),
                btnFace = new Color(1f, 0.8f, 0.12f), btnHover = new Color(1f, 0.88f, 0.34f), btnWall = new Color(0.45f, 0.34f, 0.04f), btnGlow = new Color(1f, 0.8f, 0.2f), btnInk = "#2a1c02",
                accent = new Color(1f, 0.77f, 0.23f), deco = 1,
            },
        };
        ShipTheme Th => Themes[sim != null ? Mathf.Clamp(sim.Ship, 0, Themes.Length - 1) : 0];
        static int hullShip = -1;

        // ───────── 🎨 도트 조종실 (Resources/cockpit/cock_{배}) · 소품 — 없으면 예전 코드 그림(BuildHull)
        static readonly Texture2D[] cockTex = new Texture2D[8]; static readonly bool[] cockTried = new bool[8];
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> propTex = new System.Collections.Generic.Dictionary<string, Texture2D>();
        static Texture2D CockTex(int s) { if (s < 0 || s >= 8) return null; if (!cockTried[s]) { cockTried[s] = true; cockTex[s] = Resources.Load<Texture2D>("cockpit/cock_" + s); } return cockTex[s]; }
        static Texture2D Prop(string n) { if (!propTex.TryGetValue(n, out var t)) { t = Resources.Load<Texture2D>("cockpit/" + n); propTex[n] = t; } return t; }
        float charmKick; int decoFlow = -1;

        /// <summary>그림을 pivot 둘레로 돌려 그린다 (GUI.matrix 가 배율을 들고 있어서 직접 곱한다)</summary>
        void DrawRot(Rect r, Texture2D t, Vector2 pivot, float deg)
        {
            var m = GUI.matrix;
            GUI.matrix = m * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, deg), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            GUI.DrawTexture(r, t);
            GUI.matrix = m;
        }

        void CockDeco(int s)
        {
            if (s != 1) return;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (decoFlow != flow) { if (decoFlow == 0 || decoFlow == 1) charmKick = 1.4f; decoFlow = flow; }   // 출동에서 돌아오면 덜컹
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && new Rect(ox + 100, 90, 760, 320).Contains(ev.mousePosition)) charmKick = Mathf.Max(charmKick, 0.9f);   // 창을 누르면 흔들린다
            if (ev.type == EventType.Repaint) charmKick = Mathf.Max(0, charmKick - dt * 0.6f);

            // 🔦 작업등 — 계기판 양쪽 둥근 등이 가끔 깜빡
            float fl = (Mathf.Repeat(t, 3.2f) > 1.45f && Mathf.Repeat(t, 3.2f) < 1.55f) || (Mathf.Repeat(t, 5.3f) > 3.9f && Mathf.Repeat(t, 5.3f) < 3.96f) ? 0.25f : 1f;
            GUI.color = new Color(1f, 0.62f, 0.2f, 0.28f * fl); foreach (float lx in new[] { 168f, 792f }) GUI.DrawTexture(new Rect(ox + lx - 46, 446, 92, 92), texDisc);
            // 🌫 먼지 — 느리게 떠다니는 알갱이
            for (int i = 0; i < 22; i++)
            {
                float sx = Mathf.Repeat(i * 173.7f + t * (6 + i % 5 * 2), 960), sy = Mathf.Repeat(i * 97.3f - t * (4 + i % 3 * 3), 600);
                GUI.color = new Color(1f, 0.85f, 0.55f, 0.18f + 0.12f * Mathf.Sin(t * 1.3f + i)); GUI.DrawTexture(new Rect(ox + sx, sy, i % 4 == 0 ? 3 : 2, i % 4 == 0 ? 3 : 2), white);
            }
            GUI.color = Color.white;
            // 🧨 산탄 탄띠 선반 — 왼쪽 아래 벽
            var sh = Prop("shells_1"); if (sh != null) GUI.DrawTexture(new Rect(ox + 6, 506, 132, 66), sh);
            // ⛏ 곡괭이 행운 장식 — 창 위에 매달려 흔들린다
            var ch = Prop("charm_1");
            if (ch != null)
            {
                float ang = Mathf.Sin(t * 2.3f) * 9f + charmKick * Mathf.Sin(t * 8.5f) * 22f;
                var piv = new Vector2(ox + 612, 96);
                DrawRot(new Rect(piv.x - 32, piv.y, 64, 128), ch, piv, ang);
            }
            // 👷 광부 흔들 인형 — 계기판 위
            var bo = Prop("bobble_1");
            if (bo != null)
            {
                float ang = Mathf.Sin(t * 4.6f) * (4f + 10f * charmKick);
                var piv = new Vector2(ox + 578, 414);
                DrawRot(new Rect(piv.x - 27, piv.y - 72, 54, 72), bo, piv, ang);
            }
        }

        /// <summary>경고 줄무늬 띠 — 산탄선 방 테두리 · 조종대 가장자리</summary>
        static Texture2D hazardTex;
        void Hazard(Rect r, float a = 1f)
        {
            if (hazardTex == null)
            {
                hazardTex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[256];
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) px[y * 16 + x] = ((x + y) / 8) % 2 == 0 ? new Color32(255, 204, 31, 255) : new Color32(22, 19, 12, 255);
                hazardTex.SetPixels32(px); hazardTex.Apply();
            }
            GUI.color = new Color(1, 1, 1, a);
            GUI.DrawTextureWithTexCoords(r, hazardTex, new Rect(0, 0, r.width / 16f, r.height / 16f));
            GUI.color = Color.white;
        }
    }
}
