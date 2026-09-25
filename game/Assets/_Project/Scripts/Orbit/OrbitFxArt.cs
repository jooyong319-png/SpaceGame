using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// ✨ 코드로 그리는 공격 효과 (09-26 사장님 「공격 도트가 과하다 · 얼음이랑 자석만 바꿔봐」 · 시안 https://claude.ai/artifact/1hpRaMr1i8S6ABeTV4XSEP)
    /// 가시 · 눈꽃 대신 얇은 고리 + 작은 조각. 무기 색 한 가지 + 흰 심지, 검은 테두리 없음. 48×48 도트 · Point 필터라 크게 그려도 번지지 않는다.
    /// </summary>
    public static class OrbitFxArt
    {
        const int Frames = 9;
        static int N = 48;                                                    // 그리는 판 크기 — 자석은 크게 터지니 128 (48을 늘리면 도트가 네모 덩어리가 됐다)
        static Sprite[] frost, magnet;
        static readonly Color32 White = new Color32(255, 255, 255, 255);

        // 🔴 플레이를 멈추면 유니티가 만든 그림을 지운다 — 도메인 재로드 없이 다시 플레이하면 static 배열만 남아 「지워진 Sprite」 오류가
        //    자석이 터질 때마다 났다(에디터 로그 11만 번 · 사장님 「출발에서 랙이 엄청」 09-26). 지워졌으면(== null) 다시 그린다 + 안 지워지게 표시.
        public static Sprite[] Missile => Alive(missile) ? missile : (missile = MakeMissile());
        static Sprite[] missile;
        // 🚀 분열탄 미사일 — 13×5 도트, 오른쪽이 머리. 두 장 = 꼬리 불꽃 깜빡 (09-26 시안 https://claude.ai/artifact/PiacR6DKdyPgWiYXsSZsAo)
        static Sprite[] MakeMissile()
        {
            const int w = 13, h = 5;
            var a = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var px = new Color32[w * h];
                void Put(int x, int y, int len, int hh, Color32 c) { for (int yy = y; yy < y + hh; yy++) for (int xx = x; xx < x + len; xx++) px[(h - 1 - yy) * w + xx] = c; }   // 위에서부터 세는 줄 → 텍스처는 아래부터
                bool fl = f == 0;
                Put(fl ? 1 : 0, 1, 2, 2, new Color32(255, 122, 48, 255));                  // 불꽃 끝
                Put(2, 1, 2, 2, fl ? new Color32(255, 242, 192, 255) : new Color32(255, 179, 71, 255));   // 불꽃 심지
                Put(4, 1, 7, 3, new Color32(185, 192, 204, 255));                          // 몸통
                Put(4, 1, 7, 1, new Color32(226, 230, 236, 255));                          // 윗줄 빛
                Put(11, 1, 2, 3, new Color32(226, 85, 61, 255));                           // 빨간 머리
                Put(4, 0, 2, 1, new Color32(110, 118, 134, 255)); Put(4, 4, 2, 1, new Color32(110, 118, 134, 255));   // 지느러미
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(px); tex.Apply();
                a[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w); a[f].hideFlags = HideFlags.HideAndDontSave;
            }
            return a;
        }

        // 💣 기뢰 — 짧은 가시가 난 어두운 공 · 가운데 빨간 불 (0 켜짐 · 1 꺼짐)
        public static Sprite[] Mine => Alive(mine) ? mine : (mine = MakeMine());
        static Sprite[] mine;
        static Sprite[] MakeMine()
        {
            const int w = 15;
            var a = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var px = new Color32[w * w]; float c = 7;
                var body = new Color32(74, 70, 78, 255); var rim = new Color32(40, 36, 44, 255); var hl = new Color32(120, 116, 128, 255); var spike = new Color32(150, 146, 156, 255);
                for (int y = 0; y < w; y++) for (int x = 0; x < w; x++)
                {
                    float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r <= 4.6f) px[y * w + x] = r > 3.8f ? rim : (dx < 0 && dy > 0 && r < 3.2f && r > 1.6f ? hl : body);
                }
                foreach (var (sx, sy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
                    for (int q = 5; q <= 7; q++) px[(int)(c + sy * q) * w + (int)(c + sx * q)] = spike;               // 가시 넷
                foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                    px[(int)(c + sy * 4) * w + (int)(c + sx * 4)] = spike;                                           // 짧은 가시 넷
                var light = f == 0 ? new Color32(255, 90, 74, 255) : new Color32(110, 30, 26, 255);
                for (int y = 6; y <= 8; y++) for (int x = 6; x <= 8; x++) px[y * w + x] = light;
                if (f == 0) px[8 * w + 7] = new Color32(255, 214, 200, 255);                                      // 불 심지
                var tex = new Texture2D(w, w, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(px); tex.Apply();
                a[f] = Sprite.Create(tex, new Rect(0, 0, w, w), new Vector2(0.5f, 0.5f), w); a[f].hideFlags = HideFlags.HideAndDontSave;
            }
            return a;
        }

        public static Sprite[] Frost => Alive(frost) ? frost : (frost = Make(DrawFrost, 96));
        public static Sprite[] Magnet => Alive(magnet) ? magnet : (magnet = Make(DrawMagnet, 128));
        static bool Alive(Sprite[] a) { if (a == null) return false; foreach (var s in a) if (s == null) return false; return true; }

        static Sprite[] Make(System.Action<Color32[], float> draw, int n)
        {
            N = n;
            var a = new Sprite[Frames];
            for (int f = 0; f < Frames; f++)
            {
                var px = new Color32[N * N];
                draw(px, f / (float)(Frames - 1));
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(px); tex.Apply();
                a[f] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N); a[f].hideFlags = HideFlags.HideAndDontSave;
            }
            return a;
        }

        static void Px(Color32[] px, float x, float y, Color32 c, float a)
        {
            if (a <= 0) return;
            int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
            if (ix < 0 || iy < 0 || ix >= N || iy >= N) return;
            byte al = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255);
            int i = iy * N + ix;
            if (al >= px[i].a) px[i] = new Color32(c.r, c.g, c.b, al);           // 겹치면 진한 쪽
        }
        static void Ring(Color32[] px, float r, Color32 c, float a, float step = 1)
        {
            float cx = N / 2f, cy = N / 2f;
            int n = Mathf.Max(12, Mathf.FloorToInt(r * 6.3f / step));
            for (int i = 0; i < n; i++) { float an = i / (float)n * Mathf.PI * 2; Px(px, cx + Mathf.Cos(an) * r, cy + Mathf.Sin(an) * r, c, a); }
        }

        // ❄ 얼음 (09-26 새로 — 고리는 자석과 똑같아 보였다 · 사장님 「얼음이 자석이랑 너무 같다」)
        //    조준점 둘레에 각진 얼음 덩어리 넷이 톡 얼어붙고(하늘 속 · 흰 윗모서리 · 짙은 아랫모서리), 끝에 잘게 깨져 흩어진다
        static void DrawFrost(Color32[] px, float k)
        {
            float cx = N / 2f, cy = N / 2f, sc = N / 48f;
            var fill = new Color32(170, 222, 255, 255); var hi = White; var lo = new Color32(96, 160, 214, 255);
            for (int i = 0; i < 4; i++)
            {
                float an = i * 1.57f + 0.5f + (i % 2) * 0.35f, d = (7 + (i % 2) * 4) * sc;
                float bx = cx + Mathf.Cos(an) * d, by = cy + Mathf.Sin(an) * d;
                if (k < 0.72f)
                {   // 얼어붙기 — 0 → 제 크기로 톡 (살짝 넘쳤다 돌아옴)
                    float g = Mathf.Clamp01((k - i * 0.05f) / 0.3f); if (g <= 0) continue;
                    float pop = g < 1 ? g * 1.15f : 1f;
                    int h = Mathf.Max(1, Mathf.RoundToInt((3 + (i % 2)) * sc * pop));
                    int x0 = Mathf.RoundToInt(bx - h), y0 = Mathf.RoundToInt(by - h);
                    for (int yy = 0; yy <= h * 2; yy++) for (int xx = 0; xx <= h * 2; xx++)
                    {
                        bool top = yy == h * 2, left = xx == 0, bot = yy == 0, right = xx == h * 2;
                        var c = top || left ? hi : bot || right ? lo : fill;
                        Px(px, x0 + xx, y0 + yy, c, top || left || bot || right ? 1f : 0.8f);
                    }
                    for (int q = 1; q < h; q++) Px(px, x0 + q + 1, y0 + h * 2 - q - 1, hi, 0.9f);   // 얼음 빛줄기 — 왼쪽 위에서 비스듬히 (네모가 밋밋했다)
                }
                else
                {   // 깨짐 — 조각 넷이 바깥으로 튀며 흐려진다
                    float u = (k - 0.72f) / 0.28f;
                    for (int q = 0; q < 4; q++)
                    {
                        float a2 = an + (q - 1.5f) * 0.9f, dd = u * 7 * sc;
                        float sx = bx + Mathf.Cos(a2) * dd, sy = by + Mathf.Sin(a2) * dd;
                        var c = q % 2 == 0 ? hi : fill; float a = 1 - u;
                        Px(px, sx, sy, c, a); Px(px, sx + 1, sy, c, a); if (sc > 1) { Px(px, sx, sy + 1, c, a); Px(px, sx + 1, sy + 1, c, a); }
                    }
                }
            }
        }

        // 🧲 자석 — 얇은 보라 고리 둘이 안쪽으로 조여들고, 끝에 가운데 한 점
        static void DrawMagnet(Color32[] px, float k)
        {
            var c = new Color32(190, 160, 255, 255); float cx = N / 2f, cy = N / 2f, sc = N / 48f;   // 반지름은 판 크기에 맞춰 · 점은 1도트 그대로
            Ring(px, (18 - k * 14) * sc, c, 1 - k * 0.6f, 1.5f);
            Ring(px, (12 - k * 10) * sc, c, 0.6f * (1 - k), 2);
            Px(px, cx, cy, White, k);
        }
    }
}
