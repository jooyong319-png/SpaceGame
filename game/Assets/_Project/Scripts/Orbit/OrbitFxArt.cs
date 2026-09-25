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

        // ❄ 얼음 — 옅은 하늘색 고리가 퍼지고, 얼음 조각(◆) 넷이 제자리에서 반짝
        static void DrawFrost(Color32[] px, float k)
        {
            var c = new Color32(150, 215, 255, 255); float cx = N / 2f, cy = N / 2f, t = k * 0.4f, sc = N / 48f;   // 크게 퍼지니 96 — 점은 1도트 그대로
            Ring(px, (6 + k * 10) * sc, c, 0.7f * (1 - k), 2);
            for (int i = 0; i < 4; i++)
            {
                float an = i * 1.57f + 0.6f, d = (8 + k * 3) * sc, x = cx + Mathf.Cos(an) * d, y = cy + Mathf.Sin(an) * d;
                float a = (1 - k) * (0.6f + 0.4f * Mathf.Sin(t * 40 + i));
                Px(px, x, y - 1, c, a); Px(px, x - 1, y, c, a); Px(px, x + 1, y, c, a); Px(px, x, y + 1, c, a); Px(px, x, y, White, a);
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
