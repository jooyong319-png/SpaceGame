using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// ✨ 코드로 그리는 공격 효과 (09-26 사장님 「공격 도트가 과하다 · 얼음이랑 자석만 바꿔봐」 · 시안 https://claude.ai/artifact/1hpRaMr1i8S6ABeTV4XSEP)
    /// 가시 · 눈꽃 대신 얇은 고리 + 작은 조각. 무기 색 한 가지 + 흰 심지, 검은 테두리 없음. 48×48 도트 · Point 필터라 크게 그려도 번지지 않는다.
    /// </summary>
    public static class OrbitFxArt
    {
        const int N = 48, Frames = 9;
        static Sprite[] frost, magnet;
        static readonly Color32 White = new Color32(255, 255, 255, 255);

        public static Sprite[] Frost => frost ?? (frost = Make(DrawFrost));
        public static Sprite[] Magnet => magnet ?? (magnet = Make(DrawMagnet));

        static Sprite[] Make(System.Action<Color32[], float> draw)
        {
            var a = new Sprite[Frames];
            for (int f = 0; f < Frames; f++)
            {
                var px = new Color32[N * N];
                draw(px, f / (float)(Frames - 1));
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(px); tex.Apply();
                a[f] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
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
            var c = new Color32(150, 215, 255, 255); float cx = N / 2f, cy = N / 2f, t = k * 0.4f;
            Ring(px, 6 + k * 10, c, 0.7f * (1 - k), 2);
            for (int i = 0; i < 4; i++)
            {
                float an = i * 1.57f + 0.6f, d = 8 + k * 3, x = cx + Mathf.Cos(an) * d, y = cy + Mathf.Sin(an) * d;
                float a = (1 - k) * (0.6f + 0.4f * Mathf.Sin(t * 40 + i));
                Px(px, x, y - 1, c, a); Px(px, x - 1, y, c, a); Px(px, x + 1, y, c, a); Px(px, x, y + 1, c, a); Px(px, x, y, White, a);
            }
        }

        // 🧲 자석 — 얇은 보라 고리 둘이 안쪽으로 조여들고, 끝에 가운데 한 점
        static void DrawMagnet(Color32[] px, float k)
        {
            var c = new Color32(190, 160, 255, 255); float cx = N / 2f, cy = N / 2f;
            Ring(px, 18 - k * 14, c, 1 - k * 0.6f, 1.5f);
            Ring(px, 12 - k * 10, c, 0.6f * (1 - k), 2);
            Px(px, cx, cy, White, k);
        }
    }
}
