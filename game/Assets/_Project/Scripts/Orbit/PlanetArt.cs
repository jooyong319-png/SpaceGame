using UnityEngine;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🪐 행성 다섯 그림 — 코드로 한 번 그려 둔다 (지구 · 달 · 화성 · 목성 · 토성). 빛은 왼쪽 위에서.
    /// 토성 고리는 뒤 · 앞 두 장으로 나눠 행성을 감싸게 한다 (뒤 → 행성 → 앞).
    /// </summary>
    public static class PlanetArt
    {
        const int N = 256;
        static readonly Sprite[] cache = new Sprite[5];
        static Sprite ringBack, ringFront;

        // 🔴 ?? 는 쓰지 않는다 — Play 를 멈추면 유니티가 그림을 지우는데 C# 참조는 남는다. 유니티식 == null 로 봐야 다시 그린다
        public static Sprite Get(int kind) { if (cache[kind] == null) cache[kind] = Make(kind); return cache[kind]; }
        public static Sprite RingBack { get { if (ringBack == null) ringBack = Ring(false); return ringBack; } }
        public static Sprite RingFront { get { if (ringFront == null) ringFront = Ring(true); return ringFront; } }

        static float Fbm(float x, float y, int oct)
        {
            float s = 0, a = 0.5f, f = 1;
            for (int i = 0; i < oct; i++) { s += a * Mathf.PerlinNoise(x * f + 17.3f * i, y * f + 5.1f * i); a *= 0.5f; f *= 2.05f; }
            return s / (1 - Mathf.Pow(0.5f, oct));
        }

        static Sprite Make(int kind)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            var L = new Vector3(-0.55f, 0.5f, 0.67f).normalized;
            // 달 크레이터 자리 (고정)
            var rnd = new System.Random(11);
            var craters = new Vector3[26];
            for (int i = 0; i < craters.Length; i++) craters[i] = new Vector3((float)rnd.NextDouble() * 1.8f - 0.9f, (float)rnd.NextDouble() * 1.8f - 0.9f, 0.03f + (float)rnd.NextDouble() * (i < 5 ? 0.13f : 0.06f));
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float nx = (x + 0.5f) / N * 2 - 1, ny = (y + 0.5f) / N * 2 - 1, r2 = nx * nx + ny * ny;
                    if (r2 > 1) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                    float nz = Mathf.Sqrt(1 - r2);
                    float lon = Mathf.Atan2(nx, nz), lat = Mathf.Asin(Mathf.Clamp(ny, -1, 1));
                    float u = lon / Mathf.PI + 1, v = lat / Mathf.PI + 0.5f;
                    Color c;
                    switch (kind)
                    {
                        case 0:   // 지구 — 바다 · 대륙 · 얼음 · 구름
                        {
                            float land = Fbm(u * 3.2f, v * 3.6f, 5);
                            c = Color.Lerp(new Color(0.09f, 0.24f, 0.56f), new Color(0.18f, 0.42f, 0.78f), Fbm(u * 6, v * 6, 3));
                            if (land > 0.56f) c = Color.Lerp(new Color(0.24f, 0.5f, 0.24f), new Color(0.55f, 0.48f, 0.3f), Mathf.InverseLerp(0.56f, 0.8f, land));
                            if (Mathf.Abs(lat) > 1.2f) c = Color.Lerp(c, new Color(0.92f, 0.95f, 1f), Mathf.InverseLerp(1.2f, 1.32f, Mathf.Abs(lat)));
                            float cl = Fbm(u * 5 + 3, v * 9, 4);
                            if (cl > 0.55f) c = Color.Lerp(c, Color.white, Mathf.InverseLerp(0.55f, 0.75f, cl) * 0.85f);
                            break;
                        }
                        case 1:   // 달 — 회색 · 어두운 바다 · 크레이터
                        {
                            float g = 0.58f + 0.12f * (Fbm(u * 5, v * 5, 4) - 0.5f);
                            if (Fbm(u * 1.7f + 9, v * 1.9f, 3) > 0.58f) g -= 0.14f;
                            foreach (var cr in craters)
                            {
                                float dx = nx - cr.x, dy = ny - cr.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                                if (d < cr.z) g -= 0.1f * (1 - d / cr.z);
                                else if (d < cr.z * 1.25f) g += 0.06f;
                            }
                            c = new Color(g, g * 0.98f, g * 0.95f);
                            break;
                        }
                        case 2:   // 화성 — 녹슨 땅 · 어두운 무늬 · 극관
                        {
                            float n1 = Fbm(u * 4, v * 4, 5);
                            c = Color.Lerp(new Color(0.78f, 0.38f, 0.2f), new Color(0.52f, 0.22f, 0.13f), Mathf.InverseLerp(0.45f, 0.7f, n1));
                            c = Color.Lerp(c, new Color(0.88f, 0.55f, 0.32f), Mathf.Clamp01((0.4f - n1) * 3));
                            if (lat > 1.22f) c = Color.Lerp(c, new Color(0.95f, 0.93f, 0.9f), Mathf.InverseLerp(1.22f, 1.3f, lat));
                            break;
                        }
                        case 3:   // 목성 — 줄무늬 · 대적점
                        {
                            float w = Fbm(u * 2.5f, v * 6, 3);
                            float band = Mathf.Sin(lat * 13f + w * 3f);
                            c = band > 0.35f ? new Color(0.93f, 0.86f, 0.74f) : band > -0.3f ? new Color(0.82f, 0.62f, 0.44f) : new Color(0.62f, 0.42f, 0.3f);
                            c = Color.Lerp(c, new Color(0.85f, 0.75f, 0.65f), Fbm(u * 12, v * 30, 2) * 0.25f);
                            float sx = (lon - 0.45f) / 0.32f, sy = (lat + 0.38f) / 0.13f;
                            float sp = sx * sx + sy * sy;
                            if (sp < 1) c = Color.Lerp(new Color(0.78f, 0.34f, 0.24f), c, sp * sp);
                            break;
                        }
                        default:  // 토성 — 옅은 금빛 줄
                        {
                            float band = Mathf.Sin(lat * 10f + Fbm(u * 2, v * 5, 2) * 1.5f);
                            c = Color.Lerp(new Color(0.9f, 0.82f, 0.6f), new Color(0.78f, 0.66f, 0.45f), band * 0.5f + 0.5f);
                            break;
                        }
                    }
                    // 빛 — 왼쪽 위에서 · 가장자리는 어둡게 (대기 있는 행성은 가장자리 살짝 푸르게/밝게)
                    float lam = Mathf.Max(0, nx * L.x + ny * L.y + nz * L.z);
                    float shade = 0.16f + 0.9f * Mathf.Pow(lam, 0.85f);
                    c *= shade;
                    if (kind == 0) c = Color.Lerp(c, new Color(0.45f, 0.7f, 1f), Mathf.Pow(1 - nz, 3) * 0.5f * lam);
                    float edge = Mathf.Clamp01((1 - Mathf.Sqrt(r2)) * N * 0.5f);   // 테두리 부드럽게
                    c.a = edge;
                    px[y * N + x] = c;
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / 2f);   // 지름 2 유닛
        }

        // 토성 고리 — 납작하게 눌러 쓴다. front = 아래 반쪽만 (행성 앞을 지나는 부분)
        static Sprite Ring(bool front)
        {
            const int M = 512;
            var tex = new Texture2D(M, M, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[M * M];
            for (int y = 0; y < M; y++)
                for (int x = 0; x < M; x++)
                {
                    float nx = (x + 0.5f) / M * 2 - 1, ny = (y + 0.5f) / M * 2 - 1, r = Mathf.Sqrt(nx * nx + ny * ny);
                    Color c = new Color(0, 0, 0, 0);
                    if (r > 0.6f && r < 1f && (!front || ny < 0))
                    {
                        float t = Mathf.InverseLerp(0.6f, 1f, r);
                        float a = 0.55f + 0.3f * Mathf.Sin(t * 38f) * Mathf.Sin(t * 7f);
                        if (t > 0.62f && t < 0.68f) a *= 0.15f;                  // 카시니 틈
                        a *= Mathf.Clamp01((1 - r) * 40) * Mathf.Clamp01((r - 0.6f) * 40);
                        c = Color.Lerp(new Color(0.95f, 0.88f, 0.7f), new Color(0.75f, 0.64f, 0.46f), t);
                        c.a = Mathf.Clamp01(a);
                    }
                    px[y * M + x] = c;
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, M, M), new Vector2(0.5f, 0.5f), M / 2f);
        }
    }
}
