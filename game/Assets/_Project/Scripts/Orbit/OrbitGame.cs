using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Run;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 **궤도 청소부 (rev15) — 보는 화면 + 저장.** 규칙은 전부 Sim/OrbitSim.cs 에 있다.
    ///
    /// 이 파일은 규칙을 **다시 적지 않는다.** 시뮬이 말하는 D · 드론 · 봉쇄를 그림으로 옮길 뿐이다.
    /// (같은 규칙을 두 곳에 두면 반드시 어긋난다 — Wiki_gm verification.md ⑥)
    ///
    /// 조종은 없다. 누르는 건 파편 · 죽은 위성(화면)과 오른쪽 패널(OrbitHud)뿐.
    /// ⚠️ 흰 네모 단계. 그림은 전부 코드로 찍는다.
    /// </summary>
    public class OrbitGame : MonoBehaviour
    {
        public OrbitSim sim;
        public OrbitHud hud;
        public float timeScale = 1f;            // F2 — 개발용 ×5
        public OrbitFx fx;
        public float cinematic = -1f;           // 3막 여는 장면 경과 초 (-1 = 없음)
        public const float CinematicLen = 6.5f;
        float hitStop, endingFxTimer;
        Vector3 camBase = new Vector3(0f, 0f, -10f);
        double lastCollected, lastEarned, burstBudget, popupEarn;
        float popupTimer;
        public bool seenCollect;                // 첫 파편이 지구에 닿았다 → 「회수」가 생긴다

        const string SaveKey = "orbit.save.v1";
        const float PerDot = 100f;              // 화면의 점 하나 = 파편 100개
        const int MaxDots = 460;                // 띠 하나에 그리는 점 상한 — 넘치면 하얗게 덮는다
        public static readonly float[] BandR = { 2.0f, 3.4f, 4.8f };
        static readonly float[] BandW = { 0.34f, 0.30f, 0.26f };
        public readonly Vector3 earthPos = new Vector3(-2.8f, 0f, 0f);

        class Dot
        {
            public Transform t;
            public SpriteRenderer sr;
            public float angle, rOff, speed, fade;
            public int band;
        }

        class Fx
        {
            public SpriteRenderer sr;
            public Vector3 from;
            public float time, life;
            public int kind;        // 0 끌려 내려감 · 1 섬광 · 2 꺼지는 드론
        }

        readonly List<Dot>[] dots = { new List<Dot>(), new List<Dot>(), new List<Dot>() };
        readonly Stack<Dot> pool = new Stack<Dot>();
        readonly SpriteRenderer[] rings = new SpriteRenderer[3];
        readonly SpriteRenderer[] hazes = new SpriteRenderer[3];
        readonly List<SpriteRenderer>[] droneViews = { new List<SpriteRenderer>(), new List<SpriteRenderer>(), new List<SpriteRenderer>() };
        readonly List<SpriteRenderer> transitViews = new List<SpriteRenderer>();
        readonly List<Fx> fxList = new List<Fx>();
        SpriteRenderer salvageView, salvageHalo;

        Camera cam;
        Sprite disc, thinRing, droneSprite, satSprite;
        Sprite[] debris;
        Dot hover, blinkTarget;
        bool hoverSalvage;
        float t, saveTimer;
        bool restarting;

        // ───────────────────────────────── 부팅

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<OrbitGame>() != null) return;
            new GameObject("== 궤도 청소부 ==").AddComponent<OrbitGame>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Load();
            SetupCamera();
            EnsureLight();

            disc = MakeRing(128, 0f);
            thinRing = MakeRing(96, 0.86f);
            debris = new[] { PixelArt.Debris(10, 1), PixelArt.Debris(10, 2), PixelArt.Debris(10, 3), PixelArt.Debris(10, 4) };
            droneSprite = PixelArt.Cleaner(16);
            satSprite = PixelArt.Satellite(18, 3);

            BuildStars(260);
            fx = gameObject.AddComponent<OrbitFx>();
            fx.Init(this, cam, earthPos, disc, thinRing, droneSprite);

            for (int i = 0; i < 3; i++)
            {
                float outer = BandR[i] + BandW[i];
                var ring = MakeRing(256, (BandR[i] - BandW[i]) / outer);
                rings[i] = MakeSprite(OrbitSim.Names[i] + " 띠", ring, earthPos, outer * 2f, Color.clear, 1);
                rings[i].transform.SetParent(transform);
                hazes[i] = MakeSprite(OrbitSim.Names[i] + " 덮임", ring, earthPos, outer * 2f, Color.clear, 12);
                hazes[i].transform.SetParent(transform);
                SyncDots(i, true);
            }

            salvageView = MakeSprite("죽은 위성", satSprite, earthPos, 0.8f, Color.white, 18);
            salvageHalo = MakeSprite("죽은 위성 둘레", thinRing, earthPos, 1.2f, Color.clear, 17);
            salvageHalo.transform.SetParent(transform);
            salvageView.transform.SetParent(transform);
            salvageView.enabled = false;

            hud = gameObject.AddComponent<OrbitHud>();
            hud.game = this;
            seenCollect = sim.S.manual > 1 || sim.S.bought > 0;
            lastCollected = sim.S.collected;
            lastEarned = sim.S.earned;
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

        // ───────────────────────────────── 저장 — 이어하기만. 오프라인 수익은 없다 (wiki 1회차)

        // ───────────────────────────────── 테스트 속도 (09-22 사장님: "게임 속도좀 늘려줘봐 · 테스트 해보게")

        static readonly float[] Speeds = { 1f, 3f, 10f };
        const string SpeedKey = "orbit.speed";

        public void CycleSpeed()
        {
            int i = System.Array.IndexOf(Speeds, timeScale);
            timeScale = Speeds[(i + 1) % Speeds.Length];
            PlayerPrefs.SetFloat(SpeedKey, timeScale);
            PlayerPrefs.Save();
        }

        void Load()
        {
            timeScale = PlayerPrefs.GetFloat(SpeedKey, 1f);
            if (System.Array.IndexOf(Speeds, timeScale) < 0) timeScale = 1f;
            var s = OrbitSim.NewState();
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                // 🔴 FromJson 이 아니라 FromJsonOverwrite — 없는 필드가 0 이 되지 않게 (Wiki_gm unity.md)
                try { JsonUtility.FromJsonOverwrite(json, s); }
                catch { s = OrbitSim.NewState(); }
            }
            sim = new OrbitSim(s);
        }

        public void Save()
        {
            if (sim == null) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(sim.S));
            PlayerPrefs.Save();
        }

        public void Restart()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            sim = null;
            restarting = true;
            new GameObject("== 궤도 청소부 ==").AddComponent<OrbitGame>();
            Destroy(gameObject);
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }

        // ───────────────────────────────── 매 프레임

        void Update()
        {
            if (sim == null)
            {
                // 에디터에서 Play 중에 스크립트가 다시 읽히면 시뮬이 비어 버린다 —
                // 마지막 저장(5초마다)에서 스스로 다시 연다. (SlimeEscape 5f74234 와 같은 병)
                if (!restarting)
                {
                    restarting = true;
                    new GameObject("== 궤도 청소부 ==").AddComponent<OrbitGame>();
                    Destroy(gameObject);
                }
                return;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f2Key.wasPressedThisFrame) CycleSpeed();
                if (kb.escapeKey.wasPressedThisFrame) hud.ToggleMenu();
            }

            // 3막 여는 장면 · 봉쇄 순간엔 시간이 멈칫한다
            if (hitStop > 0f) hitStop -= Time.deltaTime;
            bool frozen = hud.MenuOpen || cinematic >= 0f || hitStop > 0f;
            float dt = frozen ? 0f : Time.deltaTime * timeScale;
            Cinematic(Time.deltaTime);
            fx.intensity = Intensity();
            EndingFireworks(Time.deltaTime);
            ApplyCamera();
            t += Time.deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.1f));
            for (int k = 0; k < steps; k++) sim.Tick(dt / steps);

            ConsumeEvents();
            AmbientCollisions(dt);

            for (int i = 0; i < 3; i++) { DrawBand(i); SyncDots(i, false); }
            MoveDots(dt);
            DrawDrones();
            DrawTransit();
            DrawSalvage();
            UpdateFx(Time.deltaTime);
            UpdateHoverAndClick();
            JuiceFromSim(dt);
            fx.Tick(Time.deltaTime, dt);

            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer > 5f) { saveTimer = 0f; Save(); }
        }

        void ConsumeEvents()
        {
            while (sim.Events.Count > 0)
            {
                var e = sim.Events.Dequeue();
                switch (e.kind)
                {
                    case SimEventKind.Collision:
                        Flash(e.orbit, Random.Range(0f, Mathf.PI * 2f), 1.4f);
                        break;
                    case SimEventKind.DroneLost:
                        // 🔴 숫자가 아니라 화면에서 점이 하나씩 꺼져야 한다 (wiki 4회차)
                        foreach (var v in droneViews[e.orbit])
                        {
                            if (!v.enabled) continue;
                            var ghost = MakeSprite("꺼지는 드론", droneSprite, v.transform.position, 0.20f, v.color, 21);
                            ghost.transform.SetParent(transform);
                            ghost.transform.rotation = v.transform.rotation;
                            fxList.Add(new Fx { sr = ghost, kind = 2, life = Random.Range(1.2f, 3.5f) });
                        }
                        break;
                    case SimEventKind.Salvaged:
                    {
                        var at = Orbit(e.orbit, (float)sim.S.salvage.angle);
                        fx.BigBurst(at, new Color(1f, 0.85f, 0.5f), 26, 4f, 1.4f);
                        fx.Pop(at, "+" + KNum.Fmt(sim.S.salvage.value), new Color(1f, 0.85f, 0.4f), 22f);
                        fx.Coins(at, 8);
                        lastEarned += sim.S.salvage.value;     // 아래 일반 「+금액」과 겹쳐 두 번 뜨지 않게
                        break;
                    }
                    case SimEventKind.Unlock:
                        if (e.text != null) fx.SyncStructures(true);
                        break;
                    case SimEventKind.Lock:
                        // 봉쇄 — 멈칫, 그리고 띠 전체가 도미노로 한 바퀴 터진다
                        hitStop = 0.35f;
                        fx.shake = Mathf.Max(fx.shake, 0.28f);
                        fx.Domino(e.orbit, Random.Range(0f, 6.28f), 26, 0.25f, 0.05f, 1);
                        fx.CoinShower(25);
                        break;
                    case SimEventKind.StationHit:
                        fx.StationPass();
                        break;
                    case SimEventKind.Act:
                        if (e.orbit == 3) cinematic = 0f;     // 3막 여는 장면
                        break;
                    case SimEventKind.Ending:
                        fx.CoinShower(40);
                        break;
                }
                hud.OnSimEvent(e);
            }
        }

        /// <summary>3막 — 아무도 안 눌렀는데 화면에서 뭔가 깨진다. D 가 높을수록 자주.</summary>
        void AmbientCollisions(float dt)
        {
            if (sim.S.act < 2 || dt <= 0f) return;
            for (int i = 0; i < 3; i++)
            {
                var o = sim.S.orbits[i];
                if (!o.open && sim.S.act < 3) continue;
                float level = Mathf.Clamp01((float)(o.D / OrbitSim.LockAt[i]));
                float rate = sim.S.act >= 3 ? level * 3f : level * 0.6f;
                if (Random.value < rate * dt)
                    fx.ChainBurst(Orbit(i, Random.Range(0f, Mathf.PI * 2f), Random.Range(-0.2f, 0.2f)), sim.S.act >= 3 ? 2 : 0);
            }
        }

        // ───────────────────────────────── 띠와 파편

        void DrawBand(int i)
        {
            var o = sim.S.orbits[i];
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            // ⚠️ 선형 색공간이라 작은 알파가 화면에선 훨씬 밝다 — 0.02 가 트랙처럼 보였다 (09-21 캡처)
            Color ring = new Color(1f, 1f, 1f, o.open ? 0.005f : 0.002f);
            if (o.warned && !o.locked) ring = new Color(0.88f, 0.34f, 0.29f, 0.02f + 0.06f * pulse);   // 붉게 깜빡이기 시작한다
            rings[i].color = ring;

            // 점이 상한을 넘으면 개별 파편이 안 보일 만큼 하얗게 덮인다
            float over = Mathf.Clamp01((float)((o.D - MaxDots * PerDot) / (OrbitSim.LockAt[i] * 0.6)));
            float haze = o.locked ? 0.42f + 0.05f * Mathf.Sin(t * 1.3f + i) : over * 0.32f;
            hazes[i].color = new Color(0.92f, 0.93f, 0.96f, haze);
        }

        void SyncDots(int band, bool instant)
        {
            var o = sim.S.orbits[band];
            int target = Mathf.Min(MaxDots, Mathf.CeilToInt((float)(o.D / PerDot)));
            var list = dots[band];
            while (list.Count < target) list.Add(NewDot(band, instant));
            while (list.Count > target) FreeDot(list[list.Count - 1]);
        }

        Dot NewDot(int band, bool instant)
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
            d.band = band;
            d.sr.sprite = debris[Random.Range(0, debris.Length)];
            d.angle = Random.Range(0f, Mathf.PI * 2f);
            d.rOff = Random.Range(-BandW[band], BandW[band]) * 0.85f;
            d.speed = (float)OrbitSim.Speed[band] * Random.Range(0.8f, 1.2f);
            d.fade = instant ? 1f : 0f;
            d.t.position = DotPos(d);
            return d;
        }

        void FreeDot(Dot d)
        {
            dots[d.band].Remove(d);
            if (d == hover) hover = null;
            if (d == blinkTarget) blinkTarget = null;
            d.t.gameObject.SetActive(false);
            pool.Push(d);
        }

        Vector3 DotPos(Dot d) => Orbit(d.band, d.angle, d.rOff);

        public Vector3 Orbit(int band, float angle, float rOff = 0f)
        {
            float r = BandR[band] + rOff;
            return earthPos + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
        }

        void MoveDots(float dt)
        {
            if (blinkTarget == null && sim.S.manual == 0 && dots[0].Count > 0) blinkTarget = dots[0][dots[0].Count / 3];

            for (int b = 0; b < 3; b++)
            {
                var o = sim.S.orbits[b];
                Color baseCol = o.open ? new Color(0.70f, 0.72f, 0.78f) : new Color(0.45f, 0.47f, 0.52f, 0.5f);
                if (o.claimed) baseCol = new Color(0.80f, 0.74f, 0.62f);
                foreach (var d in dots[b])
                {
                    d.angle += d.speed * dt;
                    d.fade = Mathf.MoveTowards(d.fade, 1f, Time.deltaTime * 1.5f);
                    d.t.position = DotPos(d);
                    Color c = baseCol;
                    float s = 1f;
                    if (d == hover) { c = Color.white; s = 1.5f; }
                    if (d == blinkTarget)
                    {
                        // 첫 과녁 — 색이 아니라 **움직임**으로 가리킨다
                        float k = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                        c = Color.Lerp(c, Color.white, k); s = 1f + 0.6f * k;
                    }
                    c.a *= d.fade;
                    d.sr.color = c;
                    d.t.localScale = Vector3.one * s * (0.16f / Mathf.Max(0.01f, d.sr.sprite.bounds.size.x));
                }
            }
        }

        // ───────────────────────────────── 드론

        void DrawDrones()
        {
            for (int b = 0; b < 3; b++)
            {
                var o = sim.S.orbits[b];
                int n = Mathf.Min(o.drones, 90);   // 커지는 게 보여야 한다 — 함대가 떼로 보이게
                var list = droneViews[b];
                while (list.Count < n)
                {
                    var sr = MakeSprite("드론", droneSprite, earthPos, 0.16f, new Color(0.95f, 0.80f, 0.36f), 20);   // 0.34 는 목걸이처럼 띠를 덮었다 (09-21)
                    sr.transform.SetParent(transform);
                    list.Add(sr);
                }
                for (int i = 0; i < list.Count; i++)
                {
                    bool on = i < n;
                    list[i].enabled = on;
                    if (!on) continue;
                    float jit = Mathf.Sin(i * 12.9898f) * 0.5f;
                    float a = t * (float)OrbitSim.Speed[b] * (1.4f + 0.4f * jit) + i * (Mathf.PI * 2f / n) + jit;
                    float rr = Mathf.Sin(t * 0.9f + i * 1.7f) * BandW[b] * 0.6f;
                    list[i].transform.position = Orbit(b, a, rr);
                    list[i].transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 180f);
                }
            }
        }

        void DrawTransit()
        {
            int need = 0;
            foreach (var tr in sim.S.transit) need += Mathf.Min(tr.count, 10);
            while (transitViews.Count < need)
            {
                var sr = MakeSprite("이동 중", droneSprite, earthPos, 0.18f, new Color(0.95f, 0.80f, 0.36f, 0.7f), 20);
                sr.transform.SetParent(transform);
                transitViews.Add(sr);
            }
            int idx = 0;
            foreach (var tr in sim.S.transit)
            {
                int n = Mathf.Min(tr.count, 10);
                float k = Mathf.Clamp01(1f - (float)(tr.left / tr.total));
                float e = k * k * (3f - 2f * k);
                int from = tr.from < 0 ? tr.to : tr.from;
                for (int i = 0; i < n; i++, idx++)
                {
                    float a = t * 0.25f + i * 0.14f + tr.to;
                    float r = Mathf.Lerp(BandR[from], BandR[tr.to], e);
                    var v = transitViews[idx];
                    v.enabled = true;
                    v.transform.position = earthPos + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                    v.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + (BandR[tr.to] > BandR[from] ? 90f : -90f));
                }
            }
            for (; idx < transitViews.Count; idx++) transitViews[idx].enabled = false;
        }

        void DrawSalvage()
        {
            var v = sim.S.salvage;
            salvageView.enabled = v.active;
            salvageHalo.enabled = v.active;
            if (!v.active) return;
            salvageView.transform.position = Orbit(v.orbit, (float)v.angle);
            salvageView.transform.rotation = Quaternion.Euler(0, 0, t * 25f);
            float blink = v.life < 5 ? (Mathf.Sin(t * 12f) > 0 ? 1f : 0.35f) : 1f;
            float glow = hoverSalvage ? 1f : 0.85f + 0.15f * Mathf.Sin(t * 3f);
            salvageView.color = new Color(1f, 0.95f, 0.85f, blink * glow);
            salvageView.transform.localScale = Vector3.one * (hoverSalvage ? 1.2f : 1f) * (0.8f / Mathf.Max(0.01f, satSprite.bounds.size.x));
            // 누를 수 있는 것은 둘레가 숨 쉰다 — 글 없이 「이건 누른다」를 말한다
            float br = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            salvageHalo.transform.position = salvageView.transform.position;
            salvageHalo.transform.localScale = Vector3.one * (1.0f + 0.25f * br) / Mathf.Max(0.01f, thinRing.bounds.size.x);
            salvageHalo.color = new Color(1f, 0.85f, 0.5f, (hoverSalvage ? 0.9f : 0.35f + 0.3f * br) * blink);
        }

        // ───────────────────────────────── 끝으로 갈수록 커진다

        float Intensity()
        {
            var S = sim.S;
            if (S.ending == 1) return 3.5f;
            if (S.act >= 3) return Mathf.Min(3f, 2f + (float)(S.t - S.act3At) / 300f);
            if (S.act == 2) return 1.3f + 0.5f * (float)sim.Danger;
            return 1f;
        }

        /// <summary>
        /// 🔴 3막 여는 장면 (6.5초). 멈칫 → 첫 충돌 → 도미노가 저궤도를 한 바퀴 → 중궤도 → 정지궤도.
        /// 사장님: *"게임 엔딩으로 가는 길인 만큼 뭔가 극적인 스토리를 보여준다거나"* (09-21)
        /// </summary>
        void Cinematic(float dt)
        {
            if (cinematic < 0f) return;
            float prev = cinematic;
            cinematic += dt;
            float a0 = 0.6f;
            if (prev < 0.5f && cinematic >= 0.5f) { fx.ChainBurst(Orbit(0, a0), 2); fx.shake = 0.35f; }
            if (prev < 0.9f && cinematic >= 0.9f) fx.Domino(0, a0, 26, 0.245f, 0.09f, 1);
            if (prev < 3.0f && cinematic >= 3.0f && sim.S.orbits[1].open) fx.Domino(1, a0 + 1f, 30, 0.21f, 0.07f, 1);
            if (prev < 4.4f && cinematic >= 4.4f && sim.S.orbits[2].open) fx.Domino(2, a0 + 2f, 34, 0.185f, 0.05f, 0);
            if (prev < 5.2f && cinematic >= 5.2f) fx.CoinShower(40);
            if (cinematic >= CinematicLen) cinematic = -1f;
        }

        void ApplyCamera()
        {
            if (cam == null) return;
            float zoom = 0f;
            if (cinematic >= 0f) zoom = 0.7f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(cinematic / CinematicLen));
            cam.orthographicSize = 6f - zoom;
            Vector2 j = fx.shake > 0f ? Random.insideUnitCircle * fx.shake : Vector2.zero;
            cam.transform.position = camBase + (Vector3)j;
        }

        /// <summary>회수 극대화 — 끝까지 빨아먹는 60초. 동전이 폭포처럼 쏟아진다.</summary>
        void EndingFireworks(float dt)
        {
            if (sim.S.ending != 1 || sim.Finished) return;
            endingFxTimer -= dt;
            if (endingFxTimer > 0f) return;
            endingFxTimer = 0.4f;
            fx.CoinShower(12);
            for (int k = 0; k < 3; k++)
            {
                int b = Random.Range(0, 3);
                if (sim.S.orbits[b].open) fx.ChainBurst(Orbit(b, Random.Range(0f, 6.28f)), 1);
            }
        }

        // ───────────────────────────────── 보는 맛 — 시뮬의 숫자를 화면의 사건으로

        /// <summary>
        /// 🔴 줍는 게 보여야 한다. 초당 수거량만큼 드론 옆에서 파편이 터지고 드론으로 빨려 든다 (초당 16번까지).
        /// 들어온 돈은 그 자리에서 「+금액」으로 튀고 동전이 크레딧 숫자로 날아간다.
        /// </summary>
        void JuiceFromSim(float dt)
        {
            var S = sim.S;
            double dCol = S.collected - lastCollected; lastCollected = S.collected;
            double dEarn = S.earned - lastEarned; lastEarned = S.earned;
            if (dt <= 0f) return;

            double rate = 0; for (int i = 0; i < 3; i++) rate += sim.Rate(i);
            double per = System.Math.Max(1.0, rate / 16.0);
            burstBudget += dCol;
            int spawned = 0;
            while (burstBudget >= per && spawned < 6) { burstBudget -= per; CollectVisual(); spawned++; }
            if (burstBudget > per * 4) burstBudget = per * 4;

            popupEarn += dEarn;
            popupTimer += Time.deltaTime;
            if (popupTimer > 0.45f && popupEarn >= 1)
            {
                Vector3 at = RandomDronePos(out _);
                fx.Pop(at, "+" + KNum.Fmt(popupEarn), new Color(1f, 0.85f, 0.4f), popupEarn > sim.IncomeRate * 5 ? 26f : 19f);
                int coins = Mathf.Clamp(1 + (int)(System.Math.Log10(popupEarn + 1) / 2), 1, 5);
                fx.Coins(at, coins);
                popupEarn = 0; popupTimer = 0f;
            }
        }

        Vector3 RandomDronePos(out Transform drone)
        {
            drone = null;
            int total = 0;
            for (int b = 0; b < 3; b++) foreach (var v in droneViews[b]) if (v.enabled) total++;
            if (total == 0) return earthPos + (Vector3)(Random.insideUnitCircle.normalized * 1.3f);
            int pick = Random.Range(0, total);
            for (int b = 0; b < 3; b++)
                foreach (var v in droneViews[b])
                {
                    if (!v.enabled) continue;
                    if (pick-- == 0) { drone = v.transform; return v.transform.position; }
                }
            return earthPos;
        }

        void CollectVisual()
        {
            var at = RandomDronePos(out var drone);
            if (drone == null) return;
            Dot best = null; float bd = 0.9f;
            for (int b = 0; b < 3; b++)
                foreach (var d in dots[b])
                {
                    float dist = Vector2.Distance(d.t.position, at);
                    if (dist < bd) { bd = dist; best = d; }
                }
            Vector3 p = best != null ? best.t.position : at + (Vector3)(Random.insideUnitCircle * 0.3f);
            if (best != null) { best.fade = 0f; best.angle += Random.Range(1f, 5f); }
            fx.CollectBurst(p, drone, sim.Has("shatter"));
        }

        // ───────────────────────────────── 누르기

        void UpdateHoverAndClick()
        {
            hover = null;
            hoverSalvage = false;
            var mouse = Mouse.current;
            if (mouse == null || sim.Finished || hud.BlocksWorld(mouse.position.ReadValue())) return;

            Vector2 sp = mouse.position.ReadValue();
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10f));
            w.z = 0f;

            var v = sim.S.salvage;
            if (v.active && Vector2.Distance(Orbit(v.orbit, (float)v.angle), w) < 0.6f) hoverSalvage = true;
            else
            {
                float best = 0.3f;
                for (int b = 0; b < 3; b++)
                {
                    var o = sim.S.orbits[b];
                    if (!o.open || o.locked) continue;
                    foreach (var d in dots[b])
                    {
                        float dist = Vector2.Distance(d.t.position, w);
                        if (dist < best) { best = dist; hover = d; }
                    }
                }
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (hoverSalvage) { sim.ClaimSalvage(); return; }
            if (hover == null) return;

            // 점은 대표일 뿐이다 — 하나를 주워도 D 는 1 줄 뿐이라 띠는 곧 다시 채워진다.
            // 🔴 그게 「34,000개? 평생 걸리겠는데」다. 드론을 원하게 만든다
            var d0 = hover;
            if (!sim.Pick(d0.band)) return;
            fx.BigBurst(d0.t.position, new Color(0.9f, 0.92f, 1f), 8, 2.2f, 0.5f);
            fx.Pop(d0.t.position, "+1", Color.white, 16f);
            var ghost = MakeSprite("회수", d0.sr.sprite, d0.t.position, 0f, Color.white, 15);
            ghost.transform.localScale = d0.t.localScale;
            ghost.transform.SetParent(transform);
            fxList.Add(new Fx { sr = ghost, from = d0.t.position, kind = 0, life = 0.6f });
            FreeDot(d0);
        }

        // ───────────────────────────────── 연출

        void Flash(int band, float angle, float size)
        {
            var sr = MakeSprite("섬광", thinRing, Orbit(band, angle, Random.Range(-0.15f, 0.15f)), 0.1f, new Color(1f, 0.9f, 0.75f, 1f), 16);
            sr.transform.SetParent(transform);
            fxList.Add(new Fx { sr = sr, kind = 1, life = 0.6f, from = Vector3.one * size });
        }

        void UpdateFx(float dt)
        {
            for (int i = fxList.Count - 1; i >= 0; i--)
            {
                var f = fxList[i];
                f.time += dt;
                float k = f.time / f.life;
                if (f.kind == 0)
                {
                    // 끌려 내려갈수록 빨라진다
                    f.sr.transform.position = Vector3.Lerp(f.from, earthPos, k * k);
                    f.sr.transform.localScale *= 1f - dt * 1.2f;
                    if (k >= 1f) seenCollect = true;
                }
                else if (f.kind == 1)
                {
                    float s = Mathf.Lerp(0.1f, 0.9f * f.from.x, k);
                    f.sr.transform.localScale = Vector3.one * s / Mathf.Max(0.01f, thinRing.bounds.size.x);
                    f.sr.color = new Color(1f, 0.9f, 0.75f, 1f - k);
                }
                else
                {
                    var c = f.sr.color;
                    c.a = k < 0.8f ? (Mathf.Sin(f.time * 18f) > 0 ? 1f : 0.25f) : (1f - k) * 5f;
                    f.sr.color = c;
                }
                if (k >= 1f) { Destroy(f.sr.gameObject); fxList.RemoveAt(i); }
            }
        }

        // ───────────────────────────────── 그림 (코드로 찍는다)

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
                var p = new Vector3((float)(rng.NextDouble() * 22 - 11), (float)(rng.NextDouble() * 12 - 6), 0f);
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
