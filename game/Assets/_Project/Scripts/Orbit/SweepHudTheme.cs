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
            public int deco;                                                 // 창틀 무늬 — 0 없음 · 1 경고 줄무늬 · 2 밧줄 · 3 군용 (남색에 하늘색 선) · 4 붉은 화살 · 5 성에 · 6 주황 띠
            public float rimW = 8;                                           // 창틀 두께
            public string charm;                                             // 창 위에 매달린 장식 (Resources/cockpit/…)
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
            new ShipTheme   // 산탄선 — 창틀만 광산 (노랑 · 검정 경고 줄무늬) · 창 위에 곡괭이 장식 (09-26 사장님 「지저분 — 창문으로만 테마를 보여 주자」)
            {
                bg = new Color32(7, 11, 17, 255), plate = new Color32(12, 19, 28, 255), rim = new Color32(22, 19, 12, 255), edge = new Color32(120, 94, 26, 255),
                deskTop = new Color32(18, 28, 41, 255), deskBot = new Color32(10, 16, 25, 255),
                rivet = new Color(0.2f, 0.27f, 0.36f), plateCol = new Color(0.075f, 0.11f, 0.16f), bezel = new Color(0.15f, 0.21f, 0.29f), screen = new Color(0.03f, 0.05f, 0.075f),
                room = new Color(0.035f, 0.045f, 0.065f), scan = new Color(1f, 0.76f, 0.35f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(0.95f, 0.76f, 0.31f), deco = 1, rimW = 13, charm = "charm_1",
            },
            new ShipTheme   // 작살선 — 밧줄 감은 창틀 · 고래 이빨 장식
            {
                bg = new Color32(7, 11, 17, 255), plate = new Color32(12, 19, 28, 255), rim = new Color32(96, 70, 40, 255), edge = new Color32(60, 150, 140, 255),
                deskTop = new Color32(18, 28, 41, 255), deskBot = new Color32(10, 16, 25, 255),
                rivet = new Color(0.2f, 0.27f, 0.36f), plateCol = new Color(0.075f, 0.11f, 0.16f), bezel = new Color(0.15f, 0.21f, 0.29f), screen = new Color(0.03f, 0.05f, 0.075f),
                room = new Color(0.035f, 0.045f, 0.065f), scan = new Color(1f, 0.76f, 0.35f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(0.3f, 0.82f, 0.78f), deco = 2, rimW = 12, charm = "charm_2",
            },
            new ShipTheme   // 레일건선 — 남색 창틀에 하늘색 선 · 군번줄
            {
                bg = new Color32(7, 11, 17, 255), plate = new Color32(12, 19, 28, 255), rim = new Color32(22, 36, 74, 255), edge = new Color32(90, 200, 255, 255),
                deskTop = new Color32(18, 28, 41, 255), deskBot = new Color32(10, 16, 25, 255),
                rivet = new Color(0.2f, 0.27f, 0.36f), plateCol = new Color(0.075f, 0.11f, 0.16f), bezel = new Color(0.15f, 0.21f, 0.29f), screen = new Color(0.03f, 0.05f, 0.075f),
                room = new Color(0.035f, 0.045f, 0.065f), scan = new Color(1f, 0.76f, 0.35f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(0.35f, 0.78f, 1f), deco = 3, rimW = 12, charm = "charm_3",
            },
            new ShipTheme   // 미사일선 — 회색 창틀에 붉은 화살 무늬 · 장난감 로켓 키링
            {
                bg = new Color32(7, 11, 17, 255), plate = new Color32(12, 19, 28, 255), rim = new Color32(52, 56, 64, 255), edge = new Color32(220, 70, 60, 255),
                deskTop = new Color32(18, 28, 41, 255), deskBot = new Color32(10, 16, 25, 255),
                rivet = new Color(0.2f, 0.27f, 0.36f), plateCol = new Color(0.075f, 0.11f, 0.16f), bezel = new Color(0.15f, 0.21f, 0.29f), screen = new Color(0.03f, 0.05f, 0.075f),
                room = new Color(0.035f, 0.045f, 0.065f), scan = new Color(1f, 0.76f, 0.35f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(1f, 0.42f, 0.4f), deco = 4, rimW = 12, charm = "charm_4",
            },
            new ShipTheme   // 냉동선 — 성에 낀 하늘색 창틀 · 눈송이 키링 (09-27)
            {
                bg = new Color32(7, 12, 19, 255), plate = new Color32(12, 21, 31, 255), rim = new Color32(78, 120, 150, 255), edge = new Color32(190, 235, 255, 255),
                deskTop = new Color32(18, 30, 44, 255), deskBot = new Color32(10, 17, 27, 255),
                rivet = new Color(0.25f, 0.35f, 0.45f), plateCol = new Color(0.075f, 0.12f, 0.17f), bezel = new Color(0.17f, 0.25f, 0.33f), screen = new Color(0.03f, 0.055f, 0.08f),
                room = new Color(0.035f, 0.05f, 0.07f), scan = new Color(0.7f, 0.9f, 1f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(0.56f, 0.85f, 1f), deco = 5, rimW = 12, charm = "charm_5",
            },
            new ShipTheme   // 분열탄선 — 올리브 창틀에 주황 띠 · 꼬마 폭탄 키링 (09-27)
            {
                bg = new Color32(9, 11, 10, 255), plate = new Color32(18, 22, 16, 255), rim = new Color32(72, 80, 44, 255), edge = new Color32(240, 130, 50, 255),
                deskTop = new Color32(26, 31, 22, 255), deskBot = new Color32(15, 18, 13, 255),
                rivet = new Color(0.3f, 0.33f, 0.22f), plateCol = new Color(0.1f, 0.12f, 0.08f), bezel = new Color(0.22f, 0.25f, 0.16f), screen = new Color(0.04f, 0.05f, 0.035f),
                room = new Color(0.045f, 0.05f, 0.04f), scan = new Color(1f, 0.7f, 0.35f, 0.04f), bayTint = new Color(0, 0, 0, 0),
                btnFace = new Color(0.95f, 0.72f, 0.26f), btnHover = new Color(1f, 0.8f, 0.36f), btnWall = new Color(0.55f, 0.36f, 0.08f), btnGlow = new Color(0.95f, 0.76f, 0.31f), btnInk = "#3a2306",
                accent = new Color(1f, 0.6f, 0.25f), deco = 6, rimW = 13, charm = "charm_6",
            },
        };
        ShipTheme Th => Themes[sim != null ? Mathf.Clamp(sim.Ship, 0, Themes.Length - 1) : 0];
        static int hullShip = -1;

        // ───────── 🎨 창 위 장식 (Resources/cockpit/…)
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> propTex = new System.Collections.Generic.Dictionary<string, Texture2D>();
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

        void CockDeco()
        {
            var ch = Th.charm != null ? Prop(Th.charm) : null;
            if (ch == null) return;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (decoFlow != flow) { if (decoFlow == 0 || decoFlow == 1) charmKick = 1.4f; decoFlow = flow; }   // 출동에서 돌아오면 덜컹
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && new Rect(ox + 130, 42, 700, 290).Contains(ev.mousePosition)) charmKick = Mathf.Max(charmKick, 0.9f);   // 창을 누르면 흔들린다
            if (ev.type == EventType.Repaint) charmKick = Mathf.Max(0, charmKick - dt * 0.6f);
            float ang = Mathf.Sin(t * 2.3f) * 9f + charmKick * Mathf.Sin(t * 8.5f) * 22f;   // ⛏ 자동차 백미러 장식처럼
            var piv = new Vector2(ox + 612, 44);
            DrawRot(new Rect(piv.x - 28, piv.y, 56, 112), ch, piv, ang);
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
