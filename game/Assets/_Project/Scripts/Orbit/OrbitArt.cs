using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 궤도 청소부 전용 도트 (2026-09-22). 사장님: *"함선들이나 쓰레기 생김새도 좀 바꿔도 될것같기도해 너무 안 어울린달까"*
    /// 그전 그림은 옛 게임(액션 파밍)의 PixelArt 를 빌려 쓰고 있었다.
    ///
    /// 색은 셋으로 가른다 — 한눈에 누구 것인지 읽히게 (Wiki_gm game-feel 「색은 정보다」):
    ///   · 우리 것 (드론 · 시설)  밝은 선체 + 🟠 호박색 불빛
    ///   · 쓰레기                  차가운 회색 · 부서진 태양판 · 녹슨 조각. 불빛이 없다
    ///   · 남의 위성               옅은 파랑
    /// 전부 코드로 찍는다. 텍스처는 HideAndDontSave (Wiki_gm unity.md).
    /// </summary>
    public static class OrbitArt
    {
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Line = new Color32(28, 32, 42, 255);
        static readonly Color32 HullL = new Color32(214, 220, 228, 255);
        static readonly Color32 HullM = new Color32(150, 158, 172, 255);
        static readonly Color32 HullD = new Color32(96, 104, 118, 255);
        static readonly Color32 Amber = new Color32(255, 196, 80, 255);
        static readonly Color32 AmberHi = new Color32(255, 236, 170, 255);
        static readonly Color32 Solar = new Color32(58, 98, 170, 255);
        static readonly Color32 SolarHi = new Color32(104, 150, 220, 255);
        static readonly Color32 JunkL = new Color32(150, 154, 160, 255);
        static readonly Color32 JunkM = new Color32(112, 116, 124, 255);
        static readonly Color32 JunkD = new Color32(76, 80, 88, 255);
        static readonly Color32 Rust = new Color32(132, 100, 80, 255);
        static readonly Color32 Foil = new Color32(170, 150, 96, 255);
        static readonly Color32 DeadPanel = new Color32(52, 70, 104, 255);

        // ───────────────────────────────── 캔버스

        sealed class C
        {
            public readonly int w, h;
            public readonly Color32[] px;
            public C(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }
            public void P(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }
            public void R(int x, int y, int rw, int rh, Color32 c) { for (int j = 0; j < rh; j++) for (int i = 0; i < rw; i++) P(x + i, y + j, c); }
            public void Disc(float cx, float cy, float r, Color32 c)
            {
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) P(x, y, c);
            }
            public void L(int x0, int y0, int x1, int y1, Color32 c)
            {
                int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, e = dx + dy;
                while (true) { P(x0, y0, c); if (x0 == x1 && y0 == y1) break; int e2 = 2 * e; if (e2 >= dy) { e += dy; x0 += sx; } if (e2 <= dx) { e += dx; y0 += sy; } }
            }
            /// <summary>솔라 패널 — 격자 줄이 있어야 태양판으로 읽힌다.</summary>
            public void Panel(int x, int y, int pw, int ph, Color32 a, Color32 b)
            {
                R(x, y, pw, ph, a);
                for (int i = x + 2; i < x + pw; i += 3) for (int j = y; j < y + ph; j++) P(i, j, b);
            }
            /// <summary>1픽셀 어두운 테두리 — 작은 그림이 배경에 안 묻히게.</summary>
            public C Outline()
            {
                var src = (Color32[])px.Clone();
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x].a != 0) continue;
                    bool near = false;
                    for (int k = 0; k < 4 && !near; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && src[ny * w + nx].a != 0) near = true;
                    }
                    if (near) px[y * w + x] = Line;
                }
                return this;
            }
            public Sprite Sprite()
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                t.SetPixels32(px); t.Apply();
                var s = UnityEngine.Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
                s.hideFlags = HideFlags.HideAndDontSave;
                return s;
            }
        }

        // ───────────────────────────────── 우리 것 — 호박색 불빛

        /// <summary>청소 드론 — 둥근 몸통 · 앞으로 벌린 집게 두 개 · 호박색 눈. 오른쪽을 본다.</summary>
        public static Sprite Drone()
        {
            var c = new C(13, 13);
            c.Disc(5.5f, 6.5f, 3.6f, HullL);
            c.Disc(5.5f, 6.5f, 2.2f, HullM);
            c.R(5, 6, 2, 2, Amber); c.P(5, 7, AmberHi);
            // 집게
            c.L(8, 9, 11, 10, HullM); c.L(11, 10, 12, 9, HullD);
            c.L(8, 4, 11, 3, HullM); c.L(11, 3, 12, 4, HullD);
            // 뒤 추진구
            c.R(1, 6, 1, 2, Amber);
            return c.Outline().Sprite();
        }

        public static Sprite Structure(string id)
        {
            C c;
            switch (id)
            {
                case "yard":        // 해체장 — 네모 틀 안에 붙잡힌 잔해 · 네 모서리 팔
                    c = new C(22, 22);
                    c.R(4, 4, 14, 14, HullD); c.R(6, 6, 10, 10, Clear);
                    c.R(8, 8, 6, 5, JunkM); c.L(8, 8, 13, 12, JunkD);
                    c.L(1, 1, 4, 4, HullM); c.L(20, 1, 17, 4, HullM); c.L(1, 20, 4, 17, HullM); c.L(20, 20, 17, 17, HullM);
                    c.P(4, 4, Amber); c.P(17, 4, Amber); c.P(4, 17, Amber); c.P(17, 17, Amber);
                    break;
                case "scanner":     // 궤도 스캐너 — 접시 안테나
                    c = new C(16, 16);
                    c.R(6, 2, 4, 5, HullM);
                    for (int x = 1; x < 15; x++) { int y = 8 + (int)(Mathf.Abs(x - 7.5f) * 0.55f); c.P(x, y, HullL); c.P(x, y + 1, HullM); }
                    c.L(8, 9, 8, 14, HullD); c.P(8, 14, Amber);
                    c.P(7, 4, Amber);
                    break;
                case "board":       // 계약 게시판 — 중계 위성 · 긴 안테나
                    c = new C(18, 14);
                    c.R(7, 4, 5, 6, HullL); c.R(8, 5, 3, 1, Amber);
                    c.Panel(0, 5, 6, 4, Solar, SolarHi); c.Panel(13, 5, 5, 4, Solar, SolarHi);
                    c.L(9, 10, 9, 13, HullD); c.P(9, 13, Amber);
                    break;
                case "salvager":    // 대형 인양선 — 앞에 큰 집게가 달린 예인선
                    c = new C(28, 14);
                    c.R(3, 4, 16, 7, HullL); c.R(3, 4, 16, 2, HullM);
                    c.R(6, 7, 2, 2, Amber); c.R(10, 7, 2, 2, Amber); c.R(14, 7, 2, 2, AmberHi);
                    c.L(19, 5, 25, 2, HullM); c.L(25, 2, 27, 4, HullD);
                    c.L(19, 9, 25, 12, HullM); c.L(25, 12, 27, 10, HullD);
                    c.R(0, 6, 3, 3, HullD); c.P(0, 7, Amber);
                    break;
                case "recycle":     // 재활용 공장 — 굴뚝 달린 상자 · 옆구리 태양판
                    c = new C(24, 20);
                    c.R(5, 3, 14, 11, HullL); c.R(5, 3, 14, 3, HullM);
                    c.R(8, 14, 2, 4, HullD); c.R(13, 14, 2, 5, HullD); c.P(9, 18, Amber); c.P(14, 19, Amber);
                    c.R(7, 8, 3, 2, Amber); c.R(12, 8, 3, 2, Amber);
                    c.Panel(0, 6, 5, 5, Solar, SolarHi); c.Panel(19, 6, 5, 5, Solar, SolarHi);
                    break;
                case "grade2":      // 드론 등급 2 — 드론 모함 · 뱃속에 격납고 불빛
                    c = new C(26, 14);
                    c.R(2, 3, 22, 8, HullL); c.R(2, 3, 22, 2, HullM);
                    for (int i = 5; i < 22; i += 4) c.R(i, 7, 2, 2, Amber);
                    c.L(24, 5, 25, 7, HullD); c.L(24, 9, 25, 7, HullD);
                    break;
                case "swarm":       // 함대 자동 증식 — 벌집 공장
                    c = new C(22, 22);
                    c.Disc(11, 11, 9.5f, HullD); c.Disc(11, 11, 8f, HullM);
                    for (int y = 5; y < 18; y += 4) for (int x = 5 + (y / 4 % 2) * 2; x < 18; x += 4) c.R(x, y, 2, 2, Amber);
                    break;
                case "refinery":    // 궤도 정련소 — 큰 고리 · 가운데 녹는 핵
                    c = new C(30, 30);
                    c.Disc(15, 15, 13.5f, HullM); c.Disc(15, 15, 11f, Clear);
                    c.Disc(15, 15, 6f, HullD); c.Disc(15, 15, 4f, Amber); c.Disc(14, 16, 2f, AmberHi);
                    c.L(15, 2, 15, 9, HullL); c.L(15, 21, 15, 28, HullL); c.L(2, 15, 9, 15, HullL); c.L(21, 15, 28, 15, HullL);
                    break;
                default:            // 시가총액 공시 — 회사 광고판
                    c = new C(20, 16);
                    c.R(1, 5, 18, 9, HullD); c.R(2, 6, 16, 7, Amber); c.R(3, 7, 14, 1, AmberHi);
                    c.R(4, 9, 3, 2, HullD); c.R(9, 9, 3, 2, HullD); c.R(14, 9, 2, 2, HullD);
                    c.L(10, 5, 10, 0, HullM);
                    break;
            }
            return c.Outline().Sprite();
        }

        // ───────────────────────────────── 쓰레기 — 불빛이 없다

        /// <summary>쓰레기 조각 여섯 가지. 전부 차갑고 불빛이 없다.</summary>
        public static Sprite[] Debris()
        {
            var list = new Sprite[6];
            C c;
            c = new C(8, 8); c.R(1, 2, 5, 3, JunkL); c.R(3, 5, 3, 2, JunkM); c.P(5, 2, JunkD); list[0] = c.Outline().Sprite();           // 휜 판
            c = new C(8, 8); c.Panel(1, 1, 5, 4, DeadPanel, Solar); c.P(5, 5, Clear); c.P(1, 4, Clear); list[1] = c.Outline().Sprite();   // 태양판 조각
            c = new C(9, 7); c.R(1, 1, 6, 4, JunkM); c.R(3, 1, 1, 4, JunkD); c.R(7, 2, 1, 2, JunkD); list[2] = c.Outline().Sprite();      // 로켓 동체 조각
            c = new C(8, 8); c.L(1, 1, 6, 3, Rust); c.L(1, 2, 5, 5, Rust); c.P(2, 5, JunkM); list[3] = c.Outline().Sprite();               // 녹슨 틀
            c = new C(7, 7); c.R(1, 1, 4, 3, Foil); c.P(4, 4, Foil); c.P(2, 4, JunkM); list[4] = c.Outline().Sprite();                     // 찢어진 단열재
            c = new C(7, 7); c.R(1, 1, 3, 3, JunkL); c.L(4, 4, 5, 5, JunkD); list[5] = c.Outline().Sprite();                               // 작은 상자 · 부러진 안테나
            return list;
        }

        /// <summary>죽은 위성 — 불 꺼진 몸통 · 한쪽 태양판은 부러졌다.</summary>
        public static Sprite DeadSat()
        {
            var c = new C(20, 14);
            c.R(8, 4, 5, 6, JunkM); c.R(8, 4, 5, 2, JunkL);
            c.Panel(1, 5, 7, 4, DeadPanel, Solar);
            c.Panel(13, 6, 3, 4, DeadPanel, Solar); c.L(16, 9, 18, 12, DeadPanel);
            c.L(10, 10, 12, 13, JunkD);
            return c.Outline().Sprite();
        }

        /// <summary>큰 우주선 잔해 — 몸통이 찢겨 있다. 여러 번 두드려야 깨진다.</summary>
        public static Sprite BigWreck()
        {
            var c = new C(32, 16);
            c.R(2, 4, 22, 8, JunkM); c.R(2, 4, 22, 2, JunkL); c.R(2, 10, 22, 2, JunkD);
            for (int x = 5; x < 20; x += 4) c.R(x, 7, 2, 2, Line);
            // 찢어진 뒤꽁무니
            for (int y = 4; y < 12; y++) c.R(24, y, (y * 7) % 5 + 1, 1, JunkM);
            c.L(28, 4, 31, 1, JunkD); c.L(27, 12, 30, 15, Rust);
            c.R(0, 6, 2, 4, JunkD);
            c.Panel(8, 12, 8, 3, DeadPanel, Solar);
            return c.Outline().Sprite();
        }

        // ───────────────────────────────── 남의 위성 — 옅은 파랑

        /// <summary>민간 위성 — 흰 몸통 양옆에 태양판. 작지만 위성으로 읽힌다.</summary>
        public static Sprite FriendlySat()
        {
            var c = new C(9, 5);
            c.R(3, 1, 3, 3, HullL);
            c.R(0, 2, 3, 1, SolarHi); c.R(6, 2, 3, 1, SolarHi);
            return c.Sprite();
        }

        /// <summary>우주정거장 — 긴 뼈대 · 모듈 · 큰 태양판. 2막 후반에 지나가다 맞는다.</summary>
        public static Sprite Station()
        {
            var c = new C(40, 24);
            c.R(4, 11, 32, 2, HullM);
            c.R(14, 8, 12, 8, HullL); c.R(14, 8, 12, 2, HullM);
            c.R(17, 11, 2, 2, SolarHi); c.R(21, 11, 2, 2, SolarHi);
            c.Panel(0, 2, 8, 8, Solar, SolarHi); c.Panel(0, 14, 8, 8, Solar, SolarHi);
            c.Panel(32, 2, 8, 8, Solar, SolarHi); c.Panel(32, 14, 8, 8, Solar, SolarHi);
            c.L(20, 16, 20, 21, HullD);
            return c.Outline().Sprite();
        }
    }
}
