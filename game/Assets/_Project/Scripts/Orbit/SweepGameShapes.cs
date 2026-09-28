using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // ⬜ 기본 도형 · 별 — 코드로 만드는 스프라이트 (09-28 나눔)
    public partial class SweepGame
    {
        // ───────────────────────────────── 그림 (단순한 도형)

        static float FullW(Sprite s) => s.rect.width / s.pixelsPerUnit;         // 🔴 애니메이션 크기는 판 전체(48×48) 기준 — bounds(첫 장 그림 테두리)로 재면 뒷장이 몇 배로 커졌다 (09-26 「공격할 때 너무 큰 게」)
        SpriteRenderer MakeAnim(Sprite s, Vector3 pos, float size, Color c, int order) { var sr = Make(s, pos, size, c, order); sr.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, FullW(s)); return sr; }
        SpriteRenderer Make(Sprite s, Vector3 pos, float size, Color c, int order)
        {
            var go = new GameObject("v"); go.transform.SetParent(transform); go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, s.bounds.size.x);
            return sr;
        }

        SpriteRenderer bgView;                                                          // 🌌 (옛) 통 배경 — 지금은 안 쓴다
        readonly List<(SpriteRenderer sr, Vector2 at, float par)> bgParts = new List<(SpriteRenderer, Vector2, float)>();   // 🌌 배경 조각 (성운 그림에서 잘라 낸 은하 · 구름)
        void Stars()
        {
            Sprite nb = null;                                                          // 픽셀랩 성운 배경은 09-24 사장님 「아쉽다」 → 되돌림 (ArtUnused/bg)
            // 조각만 골라 붙인다 (09-24 사장님 「필요한 부분만 뽑아서」) — (이름, 자리, 가로 크기, 밝기, 따라오는 정도)
            foreach (var (n, at, w, a, par) in new[] { ("galaxy_a", new Vector2(9.5f, 4.6f), 3.2f, 0.75f, 0.85f), ("galaxy_b", new Vector2(-10.5f, -3.8f), 3.8f, 0.6f, 0.85f), ("wisp_a", new Vector2(-5.5f, 3.2f), 7.5f, 0.32f, 0.9f), ("wisp_b", new Vector2(6.5f, -4.4f), 6.5f, 0.28f, 0.9f) })
            {
                var sp = Resources.Load<Sprite>("bgparts/" + n); if (sp == null) continue;
                var sr = Make(sp, at, w, new Color(1, 1, 1, a), -40); bgParts.Add((sr, at, par));
            }
            if (nb != null) bgView = Make(nb, Vector3.zero, 1f, new Color(0.42f, 0.42f, 0.5f), -50);   // 어둡게 — 쓰레기보다 뒤로
            var r = new System.Random(5);
            var star = Ring(8, 0f);
            for (int i = 0; i < 220; i++)
                Make(star, new Vector3((float)(r.NextDouble() * 24 - 12), (float)(r.NextDouble() * 12 - 6)), 0.04f + (float)r.NextDouble() * 0.03f, new Color(1, 1, 1, 0.08f + (float)r.NextDouble() * 0.3f), 0);
        }

        static Sprite Square()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var px = new Color32[64]; for (int i = 0; i < 64; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8); s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        public static Sprite Ring(int size, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f, R = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / R;
                float a = Mathf.Min(Mathf.Clamp01((1 - r) * size * 0.5f), inner <= 0 ? 1 : Mathf.Clamp01((r - inner) * size * 0.5f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
            tex.SetPixels32(px); tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite Glow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f, R = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / R;
                float gl = Mathf.Clamp01(1 - r); gl = gl * gl;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * gl));
            }
            tex.SetPixels32(px); tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
