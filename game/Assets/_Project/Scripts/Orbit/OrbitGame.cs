using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SalvageRun.Run;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 **궤도 청소부 (rev15) — 1단계 그레이박스: 보는 화면 + 첫 60초.**
    ///
    /// 설계 정본은 wiki/rev15-design.md. 이 파일은 그중 두 가지만 세운다:
    ///   · 화면 — 지구 + 궤도 띠 셋 + 파편이 실제로 돈다. 🔴 숫자표가 되면 진다
    ///   · 첫 60초 — 줍기 → 판매 → 드론. 🔴 UI 는 처음부터 있지 않다, 개념을 만날 때 생긴다
    ///
    /// 조종은 없다. 누르는 건 파편(화면)과 오른쪽 패널뿐.
    /// 입력은 OnGUI 이벤트로 받는다 — 프로젝트가 새 Input System 전용(activeInputHandler 1)이라
    /// 레거시 Input 을 부르면 예외가 난다.
    ///
    /// ⚠️ 흰 네모 단계. 수치는 전부 「모양만」이고 손으로 돌려서 맞춘다.
    /// </summary>
    public class OrbitGame : MonoBehaviour
    {
        // ───────────────────────────────── 수치 (모양만)
        const double PerDot = 100;          // 화면의 점 하나 = 파편 100개
        const double SellPrice = 4;         // 회수 1개 = 크레딧 4
        const double DroneBase = 30;        // 첫 드론 값 (0:35에 회색으로 보이고 1:00에 산다)
        const double DroneGrowth = 1.15;
        const double DroneRate = 0.5;       // 드론 1대가 D=처음값일 때 초당 줍는 개수
        const int SellUnlockAt = 5;         // 회수 5개면 「판매」가 생긴다

        // ───────────────────────────────── 궤도
        class Band
        {
            public string name;
            public float radius, width, speed;
            public double D, D0;
            public bool open;
            public SpriteRenderer ring;
            public readonly List<Dot> dots = new List<Dot>();
        }

        class Dot
        {
            public Transform t;
            public SpriteRenderer sr;
            public float angle, rOff, speed, fade;
            public Band band;
        }

        class Falling
        {
            public Transform t;
            public Vector3 from;
            public float time;
        }

        readonly List<Band> bands = new List<Band>();
        readonly List<Falling> falling = new List<Falling>();
        readonly List<Transform> droneViews = new List<Transform>();
        readonly Stack<Dot> pool = new Stack<Dot>();

        readonly Vector3 earthPos = new Vector3(-2.8f, 0f, 0f);
        Camera cam;
        Sprite disc;
        Sprite[] debris;
        Sprite droneSprite;

        // ───────────────────────────────── 상태
        double held;        // 회수했지만 아직 안 판 것
        double collected;   // 누적 회수 (해금 판정용)
        double credits;
        int drones;
        double droneCarry;
        float droneEatTimer;

        bool seenCollect, seenSell, seenDroneOffer, seenD;
        Dot blinkTarget;
        Dot hover;
        Vector2 mouseGui;
        float t;

        GUIStyle label, big, button, buttonOff;
        Font font;

        const float PanelW = 280f;

        // ───────────────────────────────── 부팅

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<OrbitGame>() != null) return;
            new GameObject("== 궤도 청소부 ==").AddComponent<OrbitGame>();
        }

        void Awake()
        {
            SetupCamera();
            EnsureLight();

            disc = MakeRing(128, 0f);
            debris = new[] { PixelArt.Debris(10, 1), PixelArt.Debris(10, 2),
                             PixelArt.Debris(10, 3), PixelArt.Debris(10, 4) };
            droneSprite = PixelArt.Cleaner(16);

            BuildStars(260);

            var earth = MakeSprite("지구", disc, earthPos, 2.2f, new Color(0.28f, 0.52f, 0.86f), 5);
            earth.transform.SetParent(transform);

            // 시작 D 합계 34,000 — 🔴 지금 지구 궤도에서 실제로 추적되는 10cm 이상 파편 수와 비슷하다
            AddBand("저궤도", 2.0f, 0.34f, 0.35f, 20000, true);
            AddBand("중궤도", 3.4f, 0.30f, 0.20f, 9000, false);
            AddBand("정지궤도", 4.8f, 0.26f, 0.12f, 5000, false);

            // 첫 과녁: 저궤도에서 하나가 혼자 깜빡인다 — 색이 아니라 **움직임**으로 가리킨다
            blinkTarget = bands[0].dots[bands[0].dots.Count / 3];
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.04f, 0.06f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        void EnsureLight()
        {
            if (FindFirstObjectByType<Light2D>() != null) return;
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        void AddBand(string name, float r, float w, float speed, double d, bool open)
        {
            var b = new Band { name = name, radius = r, width = w, speed = speed, D = d, D0 = d, open = open };
            float outer = r + w;
            b.ring = MakeSprite(name + " 띠", MakeRing(256, (r - w) / outer), earthPos, outer * 2f,
                                new Color(1f, 1f, 1f, open ? 0.025f : 0.012f), 1);
            // ⚠️ 0.07/0.03 은 화면에서 트랙처럼 진했다 (09-21 첫 캡처). 띠는 파편이 그려야지 바탕이 그리면 안 된다
            b.ring.transform.SetParent(transform);
            bands.Add(b);
            SyncDots(b, true);
        }

        // ───────────────────────────────── 매 프레임

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;

            // 드론 — 저궤도에서 줍는다. D 가 줄면 덜 줍는다 (1막이 끝날 때 수입이 뚝 끊기는 뿌리)
            var leo = bands[0];
            if (drones > 0 && leo.D > 0)
            {
                droneCarry += drones * DroneRate * (leo.D / leo.D0) * dt;
                while (droneCarry >= 1 && leo.D > 0)
                {
                    droneCarry -= 1;
                    leo.D -= 1; held += 1; collected += 1;
                }
                droneEatTimer -= dt;
                if (droneEatTimer <= 0f) { droneEatTimer = 0.5f / Mathf.Max(1, drones); DroneEatVisual(); }
            }

            foreach (var b in bands)
            {
                SyncDots(b, false);
                foreach (var d in b.dots)
                {
                    d.angle += d.speed * dt;
                    d.fade = Mathf.MoveTowards(d.fade, 1f, dt * 1.5f);
                    d.t.position = DotPos(d);
                    Color c = b.open ? new Color(0.70f, 0.72f, 0.78f) : new Color(0.45f, 0.47f, 0.52f, 0.55f);
                    float s = 1f;
                    if (d == hover && b.open) { c = Color.white; s = 1.5f; }
                    if (d == blinkTarget && !seenCollect)
                    {
                        float k = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                        c = Color.Lerp(c, Color.white, k); s = 1f + 0.5f * k;
                    }
                    c.a *= d.fade;
                    d.sr.color = c;
                    d.t.localScale = Vector3.one * s * DotScale(d.sr.sprite);
                }
            }

            UpdateFalling(dt);
            UpdateDroneViews();
            UpdateHover();
        }

        Vector3 DotPos(Dot d)
        {
            float r = d.band.radius + d.rOff;
            return earthPos + new Vector3(Mathf.Cos(d.angle) * r, Mathf.Sin(d.angle) * r, 0f);
        }

        static float DotScale(Sprite s) => 0.16f / Mathf.Max(0.01f, s.bounds.size.x);

        /// <summary>화면의 점 개수를 D 에 맞춘다. 🔴 D 가 늘면 화면이 진짜로 지저분해진다.</summary>
        void SyncDots(Band b, bool instant)
        {
            int target = Mathf.CeilToInt((float)(b.D / PerDot));
            while (b.dots.Count < target) b.dots.Add(NewDot(b, instant));
            while (b.dots.Count > target) FreeDot(b.dots[b.dots.Count - 1]);
        }

        Dot NewDot(Band b, bool instant)
        {
            var d = pool.Count > 0 ? pool.Pop() : null;
            if (d == null)
            {
                var go = new GameObject("파편");
                go.transform.SetParent(transform);
                d = new Dot { t = go.transform, sr = go.AddComponent<SpriteRenderer>() };
                d.sr.sortingOrder = 10;
            }
            d.t.gameObject.SetActive(true);
            d.band = b;
            d.sr.sprite = debris[Random.Range(0, debris.Length)];
            d.angle = Random.Range(0f, Mathf.PI * 2f);
            d.rOff = Random.Range(-b.width, b.width) * 0.85f;
            d.speed = b.speed * Random.Range(0.8f, 1.2f);
            d.fade = instant ? 1f : 0f;
            d.t.position = DotPos(d);
            return d;
        }

        void FreeDot(Dot d)
        {
            d.band.dots.Remove(d);
            if (d == hover) hover = null;
            if (d == blinkTarget) blinkTarget = null;
            d.t.gameObject.SetActive(false);
            pool.Push(d);
        }

        // ───────────────────────────────── 줍기

        bool OverPanel(Vector2 gui) => seenSell && gui.x > Screen.width - PanelW;

        void UpdateHover()
        {
            hover = null;
            if (OverPanel(mouseGui)) return;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(mouseGui.x, Screen.height - mouseGui.y, 10f));
            w.z = 0f;
            float best = 0.35f;
            foreach (var d in bands[0].dots)
            {
                float dist = Vector2.Distance(d.t.position, w);
                if (dist < best) { best = dist; hover = d; }
            }
        }

        void ClickPick()
        {
            if (hover == null) return;
            var d = hover;
            var b = d.band;

            // 점은 대표일 뿐이다 — 하나를 주워도 D 는 1 줄 뿐이라 띠는 곧 다시 채워진다.
            // 🔴 그게 「34,000개? 평생 걸리겠는데」다. 드론을 원하게 만든다
            var ghost = Instantiate(d.t.gameObject, transform);
            ghost.GetComponent<SpriteRenderer>().sortingOrder = 15;
            falling.Add(new Falling { t = ghost.transform, from = d.t.position, time = 0f });
            FreeDot(d);
            b.D -= 1;
        }

        void UpdateFalling(float dt)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var f = falling[i];
                f.time += dt / 0.6f;
                float e = f.time * f.time;          // 끌려 내려갈수록 빨라진다
                f.t.position = Vector3.Lerp(f.from, earthPos, e);
                f.t.localScale *= 1f - dt * 1.2f;
                if (f.time >= 1f)
                {
                    Destroy(f.t.gameObject);
                    falling.RemoveAt(i);
                    held += 1; collected += 1;
                    seenCollect = true;
                }
            }
        }

        void DroneEatVisual()
        {
            // 드론 근처 점 하나가 사라졌다 딴 데서 다시 떠오른다 — 줍는 게 화면에서 보여야 한다
            var leo = bands[0];
            foreach (var v in droneViews)
            {
                Dot best = null; float bd = 0.9f;
                foreach (var d in leo.dots)
                {
                    float dist = Vector2.Distance(d.t.position, v.position);
                    if (dist < bd) { bd = dist; best = d; }
                }
                if (best != null) { best.fade = 0f; best.angle += Random.Range(1f, 5f); }
            }
        }

        void UpdateDroneViews()
        {
            while (droneViews.Count < drones)
            {
                var sr = MakeSprite("드론", droneSprite, earthPos, 0.34f, new Color(0.95f, 0.85f, 0.45f), 20);
                sr.transform.SetParent(transform);
                droneViews.Add(sr.transform);
            }
            var leo = bands[0];
            for (int i = 0; i < droneViews.Count; i++)
            {
                float a = t * leo.speed * 1.6f + i * (Mathf.PI * 2f / droneViews.Count);
                float r = leo.radius + Mathf.Sin(t * 0.9f + i) * leo.width * 0.6f;
                droneViews[i].position = earthPos + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                droneViews[i].rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 180f);
            }
        }

        // ───────────────────────────────── 패널 (OnGUI — 임시)

        double DronePrice => System.Math.Ceiling(DroneBase * System.Math.Pow(DroneGrowth, drones));

        double TotalD
        {
            get { double s = 0; foreach (var b in bands) s += b.D; return s; }
        }

        void EnsureStyles()
        {
            if (label != null) return;
            font = Resources.Load<Font>("Galmuri11");
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16 };
            label.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            big = new GUIStyle(label) { fontSize = 22 };
            big.normal.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button)
            {
                font = font, fontSize = 15, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 6, 6)
            };
            buttonOff = new GUIStyle(button);
            var grey = new Color(0.5f, 0.52f, 0.56f);
            buttonOff.normal.textColor = grey; buttonOff.hover.textColor = grey; buttonOff.active.textColor = grey;
        }

        void OnGUI()
        {
            EnsureStyles();
            var e = Event.current;
            mouseGui = e.mousePosition;
            if (e.type == EventType.MouseDown && e.button == 0 && !OverPanel(e.mousePosition)) ClickPick();

            // 위쪽 띠 — 🔴 표시 자체가 없다가 생긴다. 0:00 에는 아무것도 없다
            float x = 16f;
            if (seenCollect) { GUI.Label(new Rect(x, 12, 220, 30), "회수  " + KNum.Fmt(held), big); x += 190f; }
            if (seenSell) { GUI.Label(new Rect(x, 12, 260, 30), "크레딧  " + KNum.Fmt(credits), big); x += 220f; }
            if (seenD) GUI.Label(new Rect(x, 12, 320, 30), "궤도 파편  " + KNum.Fmt(TotalD) + "개", big);

            if (!seenSell && collected >= SellUnlockAt) seenSell = true;
            if (!seenSell) return;

            // 오른쪽 패널
            var pr = new Rect(Screen.width - PanelW, 0, PanelW, Screen.height);
            GUI.Box(pr, GUIContent.none);
            float y = 60f;
            float bx = pr.x + 14f, bw = PanelW - 28f;

            string sell = "판매   " + KNum.Fmt(held) + "개 → " + KNum.Fmt(held * SellPrice) + " 크레딧";
            if (GUI.Button(new Rect(bx, y, bw, 44), sell, held > 0 ? button : buttonOff) && held > 0)
            {
                credits += held * SellPrice; held = 0;
                seenDroneOffer = true;
            }
            y += 56f;

            // 🔴 못 사는 걸 먼저 보여준다 — 그게 목표가 된다
            if (seenDroneOffer || drones > 0)
            {
                bool can = credits >= DronePrice;
                string txt = "수거 드론   " + KNum.Fmt(DronePrice) + " 크레딧";
                if (GUI.Button(new Rect(bx, y, bw, 44), txt, can ? button : buttonOff) && can)
                {
                    credits -= DronePrice; drones++;
                    seenD = true;   // 🔴 D 는 드론을 산 뒤에 처음 보여준다 — 「압도하는 크기」로 등장한다
                }
                y += 50f;
                if (drones > 0)
                {
                    double rate = drones * DroneRate * (bands[0].D / bands[0].D0);
                    GUI.Label(new Rect(bx + 4, y, bw, 24), "드론 " + drones + "대 · 초당 " + rate.ToString("0.0") + "개", label);
                }
            }
        }

        // ───────────────────────────────── 그림 (코드로 찍는다 — 흰 네모 단계)

        static SpriteRenderer MakeSprite(string name, Sprite s, Vector3 pos, float size, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            if (size > 0f) go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, s.bounds.size.x));
            return sr;
        }

        void BuildStars(int n)
        {
            var root = new GameObject("별").transform;
            root.SetParent(transform);
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 20 - 10), (float)(rng.NextDouble() * 12 - 6), 0f);
                float a = 0.08f + (float)rng.NextDouble() * 0.3f;
                var sr = MakeSprite("별", disc, p, 0.03f + (float)rng.NextDouble() * 0.04f, new Color(1, 1, 1, a), 0);
                sr.transform.SetParent(root);
            }
        }

        // 🔴 코드가 만든 텍스처는 HideAndDontSave — 안 붙이면 재컴파일 때 지워져 예외가 쏟아진다 (Wiki_gm unity.md)
        static Sprite MakeRing(int size, float innerFrac)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) * 0.5f, R = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / R;
                    float outer = Mathf.Clamp01((1f - r) * size * 0.5f);
                    float inner = innerFrac <= 0f ? 1f : Mathf.Clamp01((r - innerFrac) * size * 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Min(outer, inner)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
