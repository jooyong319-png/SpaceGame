using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 🔴 **코드로 찍는 도트.** 아트 담당이 정해지기 전까지 흰 사각형을 대신한다.
    ///
    /// 왜 만드는가: 흰 사각형만으로는 "쓰레기가 22종"이라는 게 화면에서 안 읽힌다.
    /// 실루엣이 다르면 종류가 다르다는 게 즉시 보이고, 그게 이 장르에서 가장 중요한 정보다.
    /// (2026-08-21 사용자 요청: "외형같은 경우에도 너가 혹시 할 수 있으면 해봐")
    ///
    /// ⚠️ **진짜 도트가 아니다.** 규칙으로 찍은 것이라 개성이 없다.
    ///    사람이 그린 스프라이트로 교체하는 것이 최종 목표이고, 이건 그때까지의 대역이다.
    ///    교체할 때 이 파일만 지우면 되도록 **여기 밖으로 새어나가지 않게** 짰다.
    ///
    /// 결정론: `System.Random`을 시드와 함께 쓴다. 같은 시드 = 같은 그림이라
    /// 실행할 때마다 쓰레기 모양이 바뀌지 않는다.
    /// </summary>
    public static class PixelArt
    {
        public const int PPU = 32;   // Pixels Per Unit — 🟡 아트 확정 전 임시값

        /// <summary>1×1 흰 사각형. 이펙트·선처럼 모양이 필요 없는 것에 쓴다.</summary>
        public static Sprite Square()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        // ==============================================================================
        //  쓰레기 — 종류마다 실루엣이 달라야 한다
        // ==============================================================================

        /// <summary>
        /// 🔴 **쓰레기는 돌이 아니라 부서진 기계여야 한다.**
        ///    울퉁불퉁한 덩어리로 찍었더니 전부 돌처럼 보였다 —
        ///    2026-08-22 플레이 피드백: *"쓰레기처럼 안 생기고 그냥 돌처럼 생겼던데"*.
        ///
        ///    그래서 **직각**을 쓴다. 자연물은 둥글고 인공물은 각지다.
        ///    · 몸통을 사각형으로 잡고
        ///    · 모서리를 뜯어내고 (부서진 자국)
        ///    · 표면에 패널 선과 리벳을 넣는다
        /// </summary>
        public static Sprite Debris(int size, int seed, float jag = 0.32f)
        {
            var rng = new System.Random(seed);
            var tex = NewTex(size);

            // 몸통 — 정사각형이 아니라 살짝 찌그러진 직사각형
            float mw = size * (0.72f + 0.20f * (float)rng.NextDouble());
            float mh = size * (0.72f + 0.20f * (float)rng.NextDouble());
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            float x0 = cx - mw * 0.5f, x1 = cx + mw * 0.5f;
            float y0 = cy - mh * 0.5f, y1 = cy + mh * 0.5f;

            // 모서리 네 곳 중 두세 곳을 뜯어낸다 — 부서진 티
            int bites = 2 + rng.Next(2);
            var biteX = new float[bites];
            var biteY = new float[bites];
            var biteR = new float[bites];
            for (int i = 0; i < bites; i++)
            {
                biteX[i] = rng.NextDouble() < 0.5 ? x0 : x1;
                biteY[i] = rng.NextDouble() < 0.5 ? y0 : y1;
                biteR[i] = size * (0.14f + jag * 0.45f * (float)rng.NextDouble());
            }

            // 패널 선 — 몸통을 가로지르는 홈
            int panelAt = (int)Mathf.Lerp(y0 + 2f, y1 - 2f, (float)rng.NextDouble());
            bool panelVertical = rng.NextDouble() < 0.5;
            int panelAtX = (int)Mathf.Lerp(x0 + 2f, x1 - 2f, (float)rng.NextDouble());

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (x < x0 || x > x1 || y < y0 || y > y1) { tex.SetPixel(x, y, Clear); continue; }

                bool bitten = false;
                for (int i = 0; i < bites; i++)
                {
                    float dx = x - biteX[i], dy = y - biteY[i];
                    if (dx * dx + dy * dy < biteR[i] * biteR[i]) { bitten = true; break; }
                }
                if (bitten) { tex.SetPixel(x, y, Clear); continue; }

                // 🔴 위가 밝고 아래가 어둡다 — 한 방향 광원이 있어야 덩어리로 보인다
                float shade = 0.66f + 0.34f * ((y - y0) / Mathf.Max(1f, mh));

                // 외곽 한 겹은 진하게 (도트의 외곽선)
                if (x < x0 + 1.05f || x > x1 - 1.05f || y < y0 + 1.05f || y > y1 - 1.05f)
                    shade *= 0.5f;

                // 패널 홈
                if (panelVertical ? Mathf.Abs(x - panelAtX) < 0.6f : Mathf.Abs(y - panelAt) < 0.6f)
                    shade *= 0.62f;

                // 리벳 — 밝은 점 몇 개
                if (rng.NextDouble() < 0.035) shade = Mathf.Min(1f, shade * 1.45f);

                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>작은 파편 — 마름모. 잔해와 실루엣이 확실히 갈려야 한다.</summary>
        public static Sprite Shard(int size)
        {
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                if (d > c) { tex.SetPixel(x, y, Clear); continue; }

                float shade = d > c - 1.1f ? 0.72f : 1f;   // 테두리만 살짝 어둡게
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>
        /// 🔴 **청소선.** 앞이 뾰족한 전투기가 아니라, 앞이 **벌어진 흡입구**인 형태.
        ///
        ///    2026-08-22 사용자 요청: *"우주선 모양도 약간 로봇청소기 같게 생기면 괜찮을듯."*
        ///    맞는 방향이다 — 형태가 곧 설명이다. 앞이 벌어져 있으면
        ///    **빨아들이는 물건**으로 읽히고, 뾰족하면 **쏘는 물건**으로 읽힌다.
        ///
        ///    `mouth`가 클수록 입이 벌어진다. 0이면 기존 삼각형과 같다.
        /// </summary>
        public static Sprite Cleaner(int size, float mouth = 0.55f, float tail = 0.9f, float wing = 0.15f)
        {
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float ny = y / (float)(size - 1);          // 0 = 뒤, 1 = 앞(흡입구)
                float dx = Mathf.Abs(x - c);

                // 🔴 앞으로 갈수록 **넓어진다** — 나팔처럼 벌어진 흡입구
                float halfWidth = Mathf.Lerp(c * tail, c * (0.35f + mouth * 0.6f), ny);

                if (wing > 0.001f)
                {
                    float w = 1f - Mathf.Abs(ny - 0.30f) / 0.30f;
                    if (w > 0f) halfWidth += c * wing * w * w;
                }

                if (dx > halfWidth || ny < 0.08f) { tex.SetPixel(x, y, Clear); continue; }

                // 흡입구 안쪽은 비운다 — 입이 벌어져 있다는 게 실루엣으로 읽혀야 한다
                float innerAt = 0.72f;
                if (ny > innerAt)
                {
                    float innerHalf = halfWidth * (1f - (ny - innerAt) / (1f - innerAt) * 0.75f);
                    if (dx < innerHalf) { tex.SetPixel(x, y, Clear); continue; }
                }

                float shade;
                if (halfWidth - dx < 1.1f) shade = 0.55f;              // 외곽선
                else if (dx < halfWidth * 0.30f) shade = 1f;           // 가운데 밝게
                else shade = 0.82f;

                if (ny < 0.24f) shade *= 1.15f;                        // 엔진부

                shade = Mathf.Min(1f, shade);
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>
        /// 🔴 선체 실루엣. 배마다 **형태가 달라야** 한다 —
        ///    색만 다르면 여섯 척이 사실상 한 척이고, 배를 고른 의미가 화면에 안 남는다.
        ///
        ///    `nose`  앞부분이 얼마나 뾰족한가 (0.05 송곳 ~ 0.55 뭉툭)
        ///    `tail`  뒤가 얼마나 넓은가 (0.6 날렵 ~ 1.1 육중)
        ///    `wing`  중간이 얼마나 부푸는가 (0 없음 ~ 0.5 큰 날개)
        /// </summary>
        public static Sprite Ship(int size, float nose = 0.14f, float tail = 0.95f, float wing = 0f)
        {
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float ny = y / (float)(size - 1);          // 0 = 뒤, 1 = 앞
                float halfWidth = Mathf.Lerp(c * tail, c * nose, ny);

                // 날개 — 중간(ny≈0.35)에서 가장 넓어진다
                if (wing > 0.001f)
                {
                    float w = 1f - Mathf.Abs(ny - 0.35f) / 0.35f;
                    if (w > 0f) halfWidth += c * wing * w * w;
                }

                float dx = Mathf.Abs(x - c);
                if (dx > halfWidth || ny < 0.10f) { tex.SetPixel(x, y, Clear); continue; }

                float shade;
                if (halfWidth - dx < 1.1f) shade = 0.55f;              // 외곽선
                else if (dx < halfWidth * 0.32f) shade = 1f;           // 가운데 밝게
                else shade = 0.82f;

                if (ny < 0.26f) shade *= 1.15f;                        // 엔진부는 밝게

                shade = Mathf.Min(1f, shade);
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>아이템/재화용 육각 결정.</summary>
        public static Sprite Crystal(int size)
        {
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c) / c;
                float dy = Mathf.Abs(y - c) / c;

                // 육각형 근사
                bool inside = dy <= 0.92f && (dx * 0.86f + dy * 0.5f) <= 0.92f && dx <= 0.80f;
                if (!inside) { tex.SetPixel(x, y, Clear); continue; }

                float edge = Mathf.Max(dx * 0.86f + dy * 0.5f, dy);
                float shade = edge > 0.78f ? 0.55f : (x < c ? 1f : 0.78f);   // 왼쪽 면만 밝게
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>
        /// 🔴 **모선(기지).** rev.10 요청: *"구분이 될 정도면 됨.
        ///    이게 기지다, 이게 쓰레기다, 이게 쓰레기를 수거하는 우주선이다."*
        ///
        ///    그래서 셋의 **실루엣을 다르게** 만든다 — 색이 아니라 형태로 갈라야
        ///    화면이 복잡해져도 안 헷갈린다:
        ///
        ///    · 기지 = **크고 각진 육각형** + 창문 불빛. 인공물이고 움직이지 않는다
        ///    · 쓰레기 = **울퉁불퉁한 파편** (`Debris`). 규칙이 없다
        ///    · 우주선 = **앞이 벌어진 흡입구** (`Cleaner`). 방향이 있다
        /// </summary>
        public static Sprite Station(int size, int seed = 3)
        {
            int n = Mathf.Max(16, size);
            var tex = NewTex(n);
            float c = (n - 1) * 0.5f;

            var hull   = new Color(0.42f, 0.52f, 0.66f);
            var plate  = new Color(0.30f, 0.38f, 0.50f);
            var edge   = new Color(0.62f, 0.75f, 0.92f);
            var window = new Color(1.00f, 0.88f, 0.45f);

            float rOuter = c * 0.94f;
            float rInner = c * 0.34f;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 🔴 육각형. 원이면 쓰레기(둥근 파편)와 섞이고, 사각이면 밋밋하다
                float ang = Mathf.Atan2(dy, dx);
                float hex = Mathf.Cos(Mathf.Repeat(ang, Mathf.PI / 3f) - Mathf.PI / 6f);
                float limit = rOuter * (0.866f / Mathf.Max(0.5f, hex));

                if (dist > limit) continue;

                Color col;
                if (dist > limit - 1.6f)         col = edge;              // 테두리 밝게
                else if (dist < rInner)          col = plate;             // 중앙 코어
                else
                {
                    // 방사형 패널 분할 — 인공물로 읽히게
                    int seg = Mathf.FloorToInt(Mathf.Repeat(ang / (Mathf.PI * 2f) * 12f + 12f, 12f));
                    col = (seg % 2 == 0) ? hull : plate;

                    // 창문 — 규칙적으로 박힌 밝은 점. **생명체가 사는 곳**으로 읽힌다
                    float band = dist / Mathf.Max(1f, limit);
                    if (band > 0.52f && band < 0.72f && seg % 3 == 0) col = window;
                }

                tex.SetPixel(x, y, col);
            }

            return ToSprite(tex);
        }

        /// <summary>속이 빈 고리 — 장판·소용돌이·아이템 링에 쓴다.</summary>
        public static Sprite Ring(int size, float thickness = 0.18f)
        {
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > 1f || d < 1f - thickness) { tex.SetPixel(x, y, Clear); continue; }
                tex.SetPixel(x, y, Color.white);
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>가운데가 밝고 가장자리로 갈수록 사라지는 원 — 폭발·글로우.</summary>
        public static Sprite Glow(int size)
        {
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>날 모양 — 가로로 긴 마름모. 궤도체(절단날)에 쓴다.</summary>
        public static Sprite Blade(int w, int h)
        {
            var tex = NewTex(w, h);
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x - cx) / cx;
                float ny = Mathf.Abs(y - cy) / Mathf.Max(0.5f, cy);

                if (ny > 1f - nx * 0.92f) { tex.SetPixel(x, y, Clear); continue; }

                float shade = y > cy ? 1f : 0.66f;   // 윗면만 번쩍이게
                tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
            }

            tex.Apply();
            return ToSprite(tex);
        }

        // ==============================================================================
        //  내부
        // ==============================================================================

        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        static Texture2D NewTex(int size) => NewTex(size, size);

        static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,      // 🔴 도트는 절대 흐려지면 안 된다
                wrapMode = TextureWrapMode.Clamp
            };
            return tex;
        }

        /// <summary>
        /// 🔴 PPU를 **텍스처 가로폭으로** 잡아 스프라이트가 항상 가로 1유닛이 되게 한다.
        ///    이렇게 해야 기존 `localScale` 계산(1유닛 기준)이 그대로 맞는다 —
        ///    고정 PPU를 쓰면 도트를 넣는 순간 모든 크기가 어긋난다.
        ///    🟡 진짜 아트로 갈 때는 PPU를 하나로 고정하고 크기를 다시 잡아야 한다.
        /// </summary>
        static Sprite ToSprite(Texture2D tex)
            => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
    }
}
