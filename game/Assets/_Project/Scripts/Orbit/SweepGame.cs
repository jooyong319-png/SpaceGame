using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 rev17 「궤도 청소부」 — 화면 · 입력 · 연출 · 저장. 규칙은 전부 Sim/SweepSim.cs (정본 wiki/rev17-detail.md).
    /// 커서 = 조준점 (빔은 저절로 쏜다). Q 또는 아래 칸 = 블랙홀 스킬 — 3초 빨아들이고 모인 만큼 연쇄 폭발.
    /// 그림은 평평한 도형 + 빛 (§1). 도트 안 해도 된다.
    /// </summary>
    public class SweepGame : MonoBehaviour
    {
        public SweepSim sim;
        public SweepHud hud;
        public Camera cam;
        public float timeScale = 1f;

        const string StateKey = "sweep.state", MetaKey = "sweep.meta";
        public const float PxPerUnit = 50f;

        Sprite disc, ring, pixel, glow, bandSprite, square;
        Sprite[] junkArt;
        Sprite deadSat, wreck, droneArt;
        readonly List<SpriteRenderer> views = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> attViews = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> droneViews = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> cableViews = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> podViews = new List<SpriteRenderer>();
        SpriteRenderer earth, atmo, rim, band, bandGlow, claw, clawRing, clawWind, holeCore, holeGlow, holeRing, moon, sun, sunCore;
        float t, saveTimer, bandInner = -1, earthR = 120, camBase;
        public bool aimOn, holdOn;
        bool castPending;
        public Vector2 aimPx;
        public static bool TestAim, TestHold;
        public static Vector2 TestPx;

        // 도파민 사다리 (§5)
        public float hitStop, slowMo, flash, edgeGlow, bandLit, rimLit, shake, creditPulse, kessT;
        public string kessText;

        class P { public SpriteRenderer sr; public Vector3 v, a, b; public float age, life, size; public Color c; public int kind; }
        readonly List<P> fx = new List<P>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        public class Pop { public Vector2 px; public string text; public Color c; public float age, size; }
        public readonly List<Pop> pops = new List<Pop>();

        public static readonly Color Amber = new Color(0.95f, 0.76f, 0.31f), Amber2 = new Color(1f, 0.87f, 0.58f), Cyan = new Color(0.44f, 0.83f, 0.91f),
            Violet = new Color(0.71f, 0.61f, 1f), Red = new Color(0.89f, 0.35f, 0.29f), Green = new Color(0.44f, 0.81f, 0.59f), Ice = new Color(0.62f, 0.85f, 1f),
            Mag = new Color(0.88f, 0.48f, 0.88f), Orange = new Color(1f, 0.6f, 0.3f), Grey = new Color(0.7f, 0.73f, 0.78f);

        // ───────────────────────────────── 부팅

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<SweepGame>() != null) return;
            new GameObject("== 궤도 청소부 rev17 ==").AddComponent<SweepGame>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Load();
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); }
            cam.orthographic = true; cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.012f, 0.02f, 0.04f);
            cam.transform.position = new Vector3(0, 0, -10);
            if (FindFirstObjectByType<Light2D>() == null) { var l = new GameObject("Global Light 2D").AddComponent<Light2D>(); l.lightType = Light2D.LightType.Global; }

            disc = Ring(128, 0f); ring = Ring(128, 0.9f); pixel = Ring(8, 0f); glow = Glow(128); square = Square();
            junkArt = OrbitArt.Debris(); deadSat = OrbitArt.DeadSat(); wreck = OrbitArt.BigWreck(); droneArt = OrbitArt.Drone();

            Stars();
            moon = Make(disc, PxToWorld(862, 104), 0.6f, new Color(0.6f, 0.62f, 0.66f), 1);
            sun = Make(glow, PxToWorld(96, 528), 7.5f, new Color(1f, 0.86f, 0.6f, 0.18f), 0);     // 정지궤도 — 멀리 있는 태양
            sunCore = Make(disc, PxToWorld(96, 528), 0.5f, new Color(1f, 0.95f, 0.82f, 0.9f), 1);
            atmo = Make(glow, Vector3.zero, 3.4f, new Color(0.35f, 0.6f, 1f, 0.35f), 4);
            earth = Make(disc, Vector3.zero, 2.4f, new Color(0.26f, 0.47f, 0.84f), 5);
            rim = Make(ring, Vector3.zero, 2.5f, new Color(1f, 0.87f, 0.58f, 0), 6);
            band = Make(disc, Vector3.zero, 7f, new Color(1, 1, 1, 0.05f), 1);
            bandGlow = Make(ring, Vector3.zero, 7f, new Color(1f, 0.76f, 0.3f, 0), 2);
            claw = Make(droneArt, Vector3.zero, 0.5f, Color.white, 70);
            clawRing = Make(ring, Vector3.zero, 1.7f, new Color(1f, 0.76f, 0.3f, 0.8f), 69);
            clawWind = Make(disc, Vector3.zero, 0.18f, Amber2, 71);
            holeGlow = Make(glow, Vector3.zero, 1f, Violet, 66);
            holeCore = Make(disc, Vector3.zero, 0.4f, Color.black, 67);
            holeRing = Make(ring, Vector3.zero, 3f, new Color(0.71f, 0.61f, 1f, 0.25f), 65);

            gameObject.AddComponent<OrbitSfx>();
            hud = gameObject.AddComponent<SweepHud>();
            hud.game = this;
        }

        // ───────────────────────────────── 저장

        void Load()
        {
            SweepState s = null; SweepMeta m = null;
            try
            {
                string js = PlayerPrefs.GetString(StateKey, ""), jm = PlayerPrefs.GetString(MetaKey, "");
                if (!string.IsNullOrEmpty(jm)) { m = new SweepMeta(); JsonUtility.FromJsonOverwrite(jm, m); }
                if (!string.IsNullOrEmpty(js)) { s = new SweepState(); JsonUtility.FromJsonOverwrite(js, s); }
            }
            catch { s = null; m = null; }
            sim = new SweepSim(s, m);
        }

        public void Save()
        {
            if (sim == null) return;
            PlayerPrefs.SetString(StateKey, JsonUtility.ToJson(sim.S));
            PlayerPrefs.SetString(MetaKey, JsonUtility.ToJson(sim.M));
            PlayerPrefs.Save();
        }

        /// <summary>엔딩 뒤 새 회사 — 기사 · 기록 · 지난 회사들은 남긴다 (§9-5)</summary>
        public void NewGame(bool keepRecords)
        {
            var old = sim.M;
            var m = new SweepMeta();
            if (keepRecords)
            {
                m.news = old.news; m.history = old.history; m.bestChain = old.bestChain; m.bestPack = old.bestPack; m.scoops = old.scoops;
                foreach (var f in old.flags) if (f.StartsWith("n:")) m.flags.Add(f);
                m.company = old.company + 1;
            }
            sim = new SweepSim(null, m);
            Save();
        }

        public void WipeAll()
        {
            PlayerPrefs.DeleteKey(StateKey); PlayerPrefs.DeleteKey(MetaKey); PlayerPrefs.Save();
            sim = new SweepSim();
        }

        void OnApplicationQuit() { Save(); }

        // ───────────────────────────────── 매 프레임

        void Update()
        {
            if (sim == null) { Load(); return; }
            float dt = Time.deltaTime;
            t += dt;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f2Key.wasPressedThisFrame) timeScale = timeScale > 1f ? 1f : 3f;
                if (kb.mKey.wasPressedThisFrame && OrbitSfx.I != null) OrbitSfx.I.ToggleMute();
            }
            ReadAim();
            if (!sim.R.over)
            {
                float sdt = dt * timeScale;
                if (hitStop > 0) { hitStop -= dt; sdt = 0; }
                else if (slowMo > 0) { slowMo -= dt; sdt *= 0.4f; }
                int steps = Mathf.Max(1, Mathf.CeilToInt(sdt / 0.03f));
                if (sdt > 0) { for (int i = 0; i < steps; i++) sim.Tick(sdt / steps, aimPx.x, aimPx.y, aimOn, holdOn || castPending); castPending = false; }   // 히트스톱 중에 누른 것도 멈춤이 풀리면 열린다
            }
            else { sim.IdleTick(dt); castPending = false; }             // 조종실 창밖 — 궤도는 계속 돈다
            Consume();
            DrawWorld();
            DrawJunk();
            DrawTools();
            UpdateFx(dt);
            creditPulse = Mathf.MoveTowards(creditPulse, 0, dt * 3f);
            shake = Mathf.MoveTowards(shake, 0, dt * 0.9f);
            flash = Mathf.MoveTowards(flash, 0, dt * 1.5f);
            edgeGlow = Mathf.MoveTowards(edgeGlow, 0, dt * 0.5f);
            bandLit = Mathf.MoveTowards(bandLit, 0, dt * 0.6f);
            rimLit = Mathf.MoveTowards(rimLit, 0, dt * 0.5f);
            kessT = Mathf.MoveTowards(kessT, 0, dt);
            // 조종실에선 카메라가 물러나 지구가 창 가운데 오게 (창 = SweepHud.Win)
            bool cockpit = hud != null && hud.CockpitView;
            float wantSize = cockpit ? 9.6f : 6f;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, wantSize, 1 - Mathf.Exp(-dt * 5f));
            float camY = cockpit ? -0.3f * cam.orthographicSize : 0f;
            camBase = Mathf.Lerp(camBase, camY, 1 - Mathf.Exp(-dt * 5f));
            cam.transform.position = new Vector3(0, camBase, -10) + (Vector3)(Random.insideUnitCircle * shake);
            saveTimer += dt;
            if (saveTimer > 5f) { saveTimer = 0; Save(); }
        }

        void ReadAim()
        {
            var mouse = Mouse.current;
            aimOn = false; holdOn = false;
            var kb = Keyboard.current;
            if (SweepHud.CastReq || (kb != null && kb.qKey.wasPressedThisFrame && hud != null && !hud.Blocking)) castPending = true;   // 블랙홀 스킬 (Q · 아래 칸) — 커서가 창 밖이어도
            SweepHud.CastReq = false;
            if (TestAim && hud != null && !hud.Blocking) { aimPx = TestPx; aimOn = true; holdOn = TestHold; return; }   // 에디터 시험용 (MCP 자동 플레이)
            if (mouse == null || hud == null || hud.Blocking) return;
            Vector2 sp = mouse.position.ReadValue();
            if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) return;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10));
            aimPx = new Vector2(480 + (w.x - cam.transform.position.x) * PxPerUnit, 310 - (w.y - cam.transform.position.y) * PxPerUnit);
            aimOn = true;
            if (hud.overSkill) aimOn = false;          // 스킬 칸 위 — 빔 자리는 그대로 둔다
        }

        public Vector3 PxToWorld(double x, double y) => new Vector3((float)(x - 480) / PxPerUnit, (float)(310 - y) / PxPerUnit, 0);

        // ───────────────────────────────── 사건 → 연출 · 소리

        void Consume()
        {
            while (sim.Events.Count > 0)
            {
                var e = sim.Events.Dequeue();
                var at = PxToWorld(e.x, e.y);
                switch (e.kind)
                {
                    case SwEv.SkillReady: OrbitSfx.Play("tick", 0.5f, 1.4f, 0.05f); break;
                    case SwEv.Supply:
                        PopAt(e.x, e.y - 10, e.text, e.k == 1 ? Violet : Green, 15);
                        Add(ring, at, 0.1f, e.k == 1 ? Violet : Green, 5, 0.5f, 0.9f);
                        OrbitSfx.Play("supply", 0.7f, 0.1f);
                        break;
                    case SwEv.SupplyGet:
                        PopAt(e.x, e.y, e.text, e.k == 1 ? Violet : Green, 18);
                        Burst(at, e.k == 1 ? Violet : Green, 14, 3f);
                        OrbitSfx.Play("grab", 0.9f, 0.05f);
                        break;
                    case SwEv.Strike:
                    {
                        bool spot = e.v <= SweepSim.PickR + 0.1;      // 아직 좁은 빔 — 한 점
                        Beam(at, e.k == 1);
                        Add(ring, at, 0.1f, e.k == 1 ? Amber2 : new Color(0.35f, 0.38f, 0.44f), 5, spot ? 0.26f : 0.22f, (float)e.v * 2 / PxPerUnit);
                        if (e.k == 1)
                        {
                            OrbitSfx.Play(spot ? "clank" : "tick", spot ? 0.5f : 0.45f, 0.05f);
                            shake = Mathf.Max(shake, spot ? 0.045f : 0.035f);
                            Burst(at, Amber2, spot ? 3 : 5, 2.2f);
                        }
                        break;
                    }
                    case SwEv.Broke:
                    {
                        int k = e.k / 100; var att = (Att)(e.k % 100);
                        Burst(at, JunkColor(k), k == SweepSim.Big ? 40 : k == SweepSim.Vault ? 18 : k == SweepSim.Chip ? 3 : 7, k == SweepSim.Big ? 6f : 3f);
                        if (att != Att.None && att != Att.Cable) Burst(at, AttColor(att), 8, 3.5f);
                        OrbitSfx.Play(k == SweepSim.Big ? "break" : k == SweepSim.Vault ? "unit" : k == SweepSim.Chip ? "pick" : "clank", k == SweepSim.Chip ? 0.3f : 0.7f, 0.03f);
                        if (k == SweepSim.Big) shake = Mathf.Max(shake, 0.2f);
                        break;
                    }
                    case SwEv.Coin:
                    {
                        int src = e.k % 10; bool cut = e.k >= 10;
                        Color c = src == 3 ? Red : cut ? new Color(0.9f, 0.65f, 0.6f) : Amber2;
                        if (e.v >= 1 && (src == 3 || e.v > sim.ValMult * 20)) PopAt(e.x, e.y - 8, (src == 3 ? "빚 -" : "+") + KNum.Fmt(e.v), c, 14 + Mathf.Min(10, Mathf.Log10((float)e.v + 1) * 2));
                        if (Random.value < 0.6f) Add(disc, at, 0.11f, src == 3 ? Red : Amber, 2, 1.6f).v = (Vector3)(Random.insideUnitCircle * 3f);
                        break;
                    }
                    case SwEv.Pop: PopAt(e.x, e.y, e.text, e.k == 1 ? Green : e.k == 3 ? Orange : Amber2, 16); if (e.k == 3) OrbitSfx.Play("unit", 0.8f); break;
                    case SwEv.Beam: { var p = Add(pixel, at, 0.05f, Cyan, 3, 0.16f); p.a = at; p.b = PxToWorld(e.x2, e.y2); break; }
                    case SwEv.Ring: Add(ring, at, 0.1f, e.k == 1 ? Red : e.k == 2 ? Mag : Orange, 5, 0.45f, (float)e.v * 2 / PxPerUnit); break;
                    case SwEv.Blast:
                        Add(ring, at, 0.1f, Orange, 5, 0.4f, (float)e.v * 2 / PxPerUnit);
                        Add(glow, at, (float)e.v * 2.4f / PxPerUnit, new Color(1f, 0.6f, 0.3f, 0.5f), 7, 0.25f);
                        OrbitSfx.Play("blast", 0.5f, 0.025f, 0.12f);
                        shake = Mathf.Max(shake, 0.05f);
                        break;
                    case SwEv.Tier:
                        OnTier(e.k);
                        break;
                    case SwEv.Crit: PopAt(e.x, e.y, "치명타!", Orange, 20); OrbitSfx.Play("blast", 0.8f, 0.1f); shake = Mathf.Max(shake, 0.1f); break;
                    case SwEv.Collapse: PopAt(e.x, e.y, "붕괴! " + (int)e.v + "개 흩어짐", Red, 22); Burst(at, Red, 40, 7f); shake = 0.25f; OrbitSfx.Play("collide", 1f); break;
                    case SwEv.Release:
                        Burst(at, Violet, 26 + (int)Mathf.Min(60, (float)e.v * 2), 8f);
                        shake = Mathf.Max(shake, Mathf.Min(0.3f, 0.08f + (float)e.v * 0.01f));
                        if (e.text != null) PopAt(e.x, e.y - 40, e.text, Violet, 18 + Mathf.Min(14, (float)e.v / 3));
                        OrbitSfx.Play("break", 1f, 0.05f);
                        break;
                    case SwEv.Shatter: Add(ring, at, 0.1f, Ice, 5, 0.3f, 0.6f); OrbitSfx.Play("pick", 0.7f); break;
                    case SwEv.Warn: hud.Banner(e.text, e.k, 2.2f); OrbitSfx.Play("warn", 0.8f); break;
                    case SwEv.EventGo: hud.Banner(e.text, -1, 1.6f); break;
                    case SwEv.Collector: hud.Banner(e.text, -2, 3f); CollectorShip(); OrbitSfx.Play("warn", 1f); break;
                    case SwEv.RunEnd: Save(); hud.OnRunEnd(); break;
                    case SwEv.Overdue: OrbitSfx.Play("warn", 1f); break;
                    case SwEv.BillPaid: hud.OnBillPaid(e.text, (int)e.v); OrbitSfx.Play("unit", 1f); OrbitSfx.Play("buy", 1f, 0.01f); Save(); break;
                    case SwEv.Bankrupt: OrbitSfx.Play("lock", 1f); Save(); break;
                    case SwEv.News: hud.OnNews(e.text, e.k == 1); break;
                    case SwEv.Won: OrbitSfx.Play("ending", 1f); Save(); hud.OnWon(); break;
                }
            }
        }

        void OnTier(int tier)
        {
            // 🔴 도파민 사다리 — 연쇄 10 · 30 · 80 · 200
            bool calm = hud != null && hud.reduceMotion;
            switch (tier)
            {
                case 1: shake = Mathf.Max(shake, 0.06f); break;
                case 2: if (!calm) hitStop = 0.07f; edgeGlow = Mathf.Max(edgeGlow, 0.7f); OrbitSfx.Play("collide", 0.8f); break;
                case 3: if (!calm) slowMo = 0.8f; edgeGlow = 1f; bandLit = 1f; kessT = 1.4f; kessText = "케슬러!"; OrbitSfx.Play("cine", 1f); break;
                case 4: if (!calm) flash = 1f; bandLit = 1f; rimLit = 1f; kessT = 2.4f; kessText = "케슬러 연쇄"; OrbitSfx.Play("ending", 0.9f); break;
            }
        }

        /// <summary>🔴 빔 — 화면 아래 선체(포구)에서 조준점까지 (사장님 09-23: "집게보단 우주선에서 빔 쏘는 느낌")</summary>
        void Beam(Vector3 at, bool hit)
        {
            var muzzle = new Vector3(0, camBase - 6.4f, 0);
            var core = Add(pixel, at, 0.05f, hit ? new Color(1f, 0.95f, 0.78f, 1f) : new Color(0.6f, 0.65f, 0.75f, 0.5f), 8, 0.22f);
            core.a = muzzle; core.b = at; core.size = hit ? 0.1f : 0.05f;
            var halo = Add(pixel, at, 0.05f, new Color(1f, 0.76f, 0.3f, hit ? 0.55f : 0.22f), 8, 0.3f);
            halo.a = muzzle; halo.b = at; halo.size = hit ? 0.3f : 0.14f;
            if (hit) Add(glow, at, 0.5f, new Color(1f, 0.87f, 0.58f, 0.6f), 7, 0.18f);
        }

        void CollectorShip()
        {
            // 추심선 — 청소선과 같은 배 그림을 빨갛게, 크게. 꼬리에 빨간 불꽃
            var p = Add(droneArt, PxToWorld(-40, 150), 1.1f, new Color(1f, 0.42f, 0.36f), 6, 3.2f);
            p.sr.transform.rotation = Quaternion.Euler(0, 0, -90);
            p.sr.sortingOrder = 85;
            p.v = new Vector3(1040f / PxPerUnit / 3.2f, -0.2f, 0);
            var glowTail = Add(glow, PxToWorld(-70, 152), 0.9f, new Color(1f, 0.35f, 0.3f, 0.5f), 6, 3.2f);
            glowTail.v = p.v;
        }

        public void PopAt(double x, double y, string text, Color c, float size)
        {
            if (pops.Count > 40) pops.RemoveAt(0);
            pops.Add(new Pop { px = new Vector2((float)x + Random.Range(-6f, 6f), (float)y), text = text, c = c, size = size });
        }

        public static Color JunkColor(int k)
        {
            switch (k)
            {
                case SweepSim.Vault: return Amber;
                case SweepSim.Fuel: return Green;
                case SweepSim.Tank: return Red;
                case SweepSim.Sat: return new Color(0.69f, 0.73f, 0.78f);
                case SweepSim.Rocket: return new Color(0.65f, 0.64f, 0.6f);
                case SweepSim.Big: return new Color(0.79f, 0.82f, 0.85f);
                default: return new Color(0.56f, 0.59f, 0.65f);
            }
        }

        public static Color AttColor(Att a)
        {
            switch (a)
            {
                case Att.FuelPod: return Green;
                case Att.Pouch: return Amber;
                case Att.Beacon: case Att.Magnet: return Mag;
                case Att.Det: case Att.Tag: return Red;
                case Att.Ice: return Ice;
                case Att.Armor: return new Color(0.42f, 0.46f, 0.52f);
                case Att.BBox: return Orange;
                default: return new Color(0.56f, 0.63f, 0.72f);
            }
        }

        // ───────────────────────────────── 궤도 · 지구 (궤도마다 카메라가 물러난다 §1-5)

        void DrawWorld()
        {
            var o = SweepSim.Orbits[sim.S.orbit];
            float wantR = sim.S.orbit == 0 ? 120 : sim.S.orbit == 1 ? 80 : 46;
            earthR = Mathf.Lerp(earthR, wantR, 1 - Mathf.Exp(-Time.deltaTime * 2.5f));
            float d = earthR * 2 / PxPerUnit;
            earth.transform.localScale = Vector3.one * d / disc.bounds.size.x;
            atmo.transform.localScale = Vector3.one * d * 1.45f * (1f + 0.02f * Mathf.Sin(t)) / glow.bounds.size.x;
            rim.transform.localScale = Vector3.one * (d + 0.12f) / ring.bounds.size.x;
            rim.color = new Color(1f, 0.87f, 0.58f, rimLit);
            moon.enabled = sim.S.orbit == 1;
            bool geo = sim.S.orbit == 2;
            sun.enabled = sunCore.enabled = geo;
            if (geo)
            {
                float pulse = 1f + 0.03f * Mathf.Sin(t * 0.8f);
                sun.transform.localScale = Vector3.one * 7.5f * pulse / glow.bounds.size.x;
                sunCore.transform.localScale = Vector3.one * 0.5f * pulse / disc.bounds.size.x;
            }
            float inner = (float)(o.bi / sim.Bo);
            if (Mathf.Abs(inner - bandInner) > 0.001f) { bandInner = inner; bandSprite = Ring(256, inner); band.sprite = bandSprite; }
            float outer = (float)sim.Bo * 2 / PxPerUnit;
            band.transform.localScale = new Vector3(outer / bandSprite.bounds.size.x, outer * (float)SweepSim.Tilt / bandSprite.bounds.size.y, 1);
            Color bc = sim.S.orbit == 0 ? new Color(0.43f, 0.55f, 0.78f) : sim.S.orbit == 1 ? new Color(0.55f, 0.45f, 0.82f) : new Color(0.78f, 0.55f, 0.45f);
            bc.a = 0.035f + bandLit * 0.025f; band.color = bc;
            bandGlow.transform.localScale = new Vector3(outer / ring.bounds.size.x, outer * (float)SweepSim.Tilt / ring.bounds.size.y, 1);
            bandGlow.color = new Color(1f, 0.8f, 0.4f, bandLit * 0.4f + edgeGlow * 0.12f);
        }

        void DrawJunk()
        {
            var list = sim.R.junk;
            bool live = !sim.R.over;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.dead) continue;
                while (views.Count <= n) { views.Add(Make(junkArt[0], Vector3.zero, 0.2f, Color.white, 10)); attViews.Add(Make(disc, Vector3.zero, 0.1f, Color.white, 12)); }
                var v = views[n]; var av = attViews[n]; n++;
                v.enabled = true;
                var pos = PxToWorld(d.x, d.y);
                v.transform.position = pos;
                Sprite s; Color c = Color.white;
                switch (d.k)
                {
                    case SweepSim.Sat: s = deadSat; break;
                    case SweepSim.Rocket: s = junkArt[2 % junkArt.Length]; break;
                    case SweepSim.Vault: s = deadSat; c = new Color(1f, 0.82f, 0.4f); c = Color.Lerp(c, Color.white, 0.2f + 0.2f * Mathf.Sin(t * 6)); break;
                    case SweepSim.Fuel: s = square; c = Green; break;
                    case SweepSim.Tank: s = disc; c = Red; break;
                    case SweepSim.Big: s = wreck; break;
                    default: s = junkArt[d.id % junkArt.Length]; break;
                }
                if (v.sprite != s) v.sprite = s;
                float dmg = 1f - (float)d.hp / Mathf.Max(1, d.max);
                if (d.max > 1) c = Color.Lerp(c, new Color(0.3f, 0.26f, 0.24f), dmg * 0.55f);   // 금 간 만큼 어두워진다
                if (d.hit > 0) c = Color.white;
                c.a = (float)d.fade;
                v.color = c;
                float r = (float)SweepSim.Types[d.k].r;
                float size = r * 2.6f / PxPerUnit * (d.hit > 0 ? 1.25f : 1f) * (d.k == SweepSim.Fuel ? 0.6f : 1f);
                v.transform.localScale = new Vector3(size / Mathf.Max(0.01f, s.bounds.size.x), size / Mathf.Max(0.01f, s.bounds.size.x) * (d.k == SweepSim.Fuel ? 1.6f : 1f), 1);
                v.transform.rotation = Quaternion.Euler(0, 0, (float)d.rot * Mathf.Rad2Deg);
                int order = 10 + Mathf.Clamp((int)(d.y / 6), 0, 99);
                bool behind = d.y < SweepSim.EY && (d.x - SweepSim.EX) * (d.x - SweepSim.EX) + (d.y - SweepSim.EY) * (d.y - SweepSim.EY) < earthR * earthR;
                v.sortingOrder = behind ? 3 : order;      // 지구 뒤로 지나가는 것
                // 부착물 — 가장자리에 붙어 함께 돈다 (§3-2)
                if (d.att == Att.None || d.att == Att.Cable) { av.enabled = false; continue; }
                av.enabled = true;
                Color ac = AttColor(d.att); ac.a = (float)d.fade;
                if (d.att == Att.Ice || d.att == Att.Armor)
                {
                    av.sprite = ring;
                    float rs = size * (d.att == Att.Ice ? 1.9f : 1.6f);
                    av.transform.position = pos; av.transform.localScale = Vector3.one * rs / ring.bounds.size.x;
                    if (d.att == Att.Armor) ac = new Color(0.62f, 0.66f, 0.72f, (float)d.fade);
                }
                else
                {
                    av.sprite = d.att == Att.BBox || d.att == Att.Tag || d.att == Att.Det ? square : disc;
                    if ((d.att == Att.Det || d.att == Att.Beacon) && Mathf.Sin(t * 8 + d.id) < 0) ac = Color.Lerp(ac, Color.black, 0.6f);
                    float ang = (float)d.rot + 0.9f;
                    av.transform.position = pos + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * size * 0.62f;
                    float asz = Mathf.Max(0.09f, size * (d.att == Att.Tag ? 0.55f : 0.42f));
                    av.transform.localScale = Vector3.one * asz / Mathf.Max(0.01f, av.sprite.bounds.size.x);
                }
                av.transform.rotation = v.transform.rotation;
                av.color = ac;
                av.sortingOrder = v.sortingOrder + 1;
            }
            for (int i = n; i < views.Count; i++) { views[i].enabled = false; attViews[i].enabled = false; }

            // 케이블 — 이어진 둘 사이 가는 선
            int cn = 0;
            foreach (var d in list)
                {
                    if (d.dead || d.att != Att.Cable && d.link1 == null) continue;
                    foreach (var l in new[] { d.link1 })
                    {
                        if (l == null || l.dead) continue;
                        while (cableViews.Count <= cn) cableViews.Add(Make(pixel, Vector3.zero, 0.05f, new Color(0.56f, 0.63f, 0.72f, 0.7f), 9));
                        var cv = cableViews[cn++]; cv.enabled = true;
                        Vector3 a = PxToWorld(d.x, d.y), b = PxToWorld(l.x, l.y), dir = b - a;
                        cv.transform.position = (a + b) / 2;
                        cv.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        cv.transform.localScale = new Vector3(dir.magnitude / pixel.bounds.size.x, 0.03f / pixel.bounds.size.y, 1);
                    }
                }
            for (int i = cn; i < cableViews.Count; i++) cableViews[i].enabled = false;

            // 지구 보급 — 지구에서 올라와 청소선까지 (폭탄은 보라 · 연료는 초록)
            var pods = sim.R.pods; int pn = 0;
            if (live)
                foreach (var p in pods)
                {
                    if (!p.up || p.got) continue;
                    while (podViews.Count <= pn) podViews.Add(Make(square, Vector3.zero, 0.2f, Color.white, 80));
                    var pv = podViews[pn++]; pv.enabled = true;
                    pv.color = p.kind == 1 ? Violet : Green;
                    var pp = PxToWorld(p.x, p.y);
                    pv.transform.position = pp;
                    pv.transform.localScale = new Vector3(0.16f / square.bounds.size.x, 0.28f / square.bounds.size.y, 1);
                    var to = PxToWorld(aimPx.x, aimPx.y) - pp;
                    pv.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90);
                    if (Random.value < 0.7f) Add(pixel, pp - to.normalized * 0.15f, 0.07f, p.kind == 1 ? Violet : Green, 0, 0.4f);   // 꼬리
                }
            for (int i = pn; i < podViews.Count; i++) podViews[i].enabled = false;

            // 드론
            var drs = sim.R.drones;
            while (droneViews.Count < drs.Count) droneViews.Add(Make(droneArt, Vector3.zero, 0.32f, Cyan, 60));
            for (int i = 0; i < droneViews.Count; i++)
            {
                bool on = i < drs.Count;
                droneViews[i].enabled = on;
                if (!on) continue;
                var dr = drs[i];
                droneViews[i].transform.position = PxToWorld(dr.x, dr.y);
                droneViews[i].transform.rotation = Quaternion.Euler(0, 0, -(float)dr.a * Mathf.Rad2Deg);
            }
        }

        void DrawTools()
        {
            var R = sim.R;
            bool show = aimOn && !R.over;
            bool holding = R.holding && !R.over;
            claw.enabled = clawRing.enabled = show;
            clawWind.enabled = show && R.fuel > 0;
            holeCore.enabled = holeGlow.enabled = holeRing.enabled = holding;
            Cursor.visible = !show;
            if (holding)
            {
                var hp = PxToWorld(R.hx, R.hy);
                int n = R.packed.Count; float k = (float)n / Mathf.Max(1, sim.Cap);
                float core = (9 + n * 0.5f) / PxPerUnit * 2;
                var shakeOff = k > 0.8f ? (Vector3)(Random.insideUnitCircle * 0.04f) : Vector3.zero;
                holeCore.transform.position = hp + shakeOff; holeCore.transform.localScale = Vector3.one * core / disc.bounds.size.x;
                holeGlow.transform.position = hp + shakeOff; holeGlow.transform.localScale = Vector3.one * core * 3.2f / glow.bounds.size.x;
                holeGlow.color = k > 0.8f ? new Color(0.9f, 0.3f, 0.25f, 0.9f) : new Color(0.42f, 0.31f, 0.78f, 0.9f);
                if (k > 0.8f) OrbitSfx.Play("danger", 0.6f, 0.45f, 0.05f);        // 붕괴 직전 — 떼라는 신호
                holeRing.transform.position = hp; holeRing.transform.localScale = Vector3.one * (float)sim.PullR * 2 / PxPerUnit / ring.bounds.size.x;
                // 소용돌이 — 모인 것들이 가운데서 돈다
                if (Random.value < 0.5f && n > 0) { float a = Random.value * 6.28f, rr = core * 0.8f; var p = Add(pixel, hp + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * rr, 0.06f, Grey, 0, 0.3f); p.v = new Vector3(-Mathf.Sin(a), Mathf.Cos(a)) * 2f; }
            }
            if (!show) return;
            var at = PxToWorld(aimPx.x, aimPx.y);
            bool area = sim.ClawR > 0, auto = sim.AutoClaw;
            float r = (float)(area ? sim.ClawR : SweepSim.PickR) * 2 / PxPerUnit;
            bool fuel = R.fuel > 0;
            float wind = auto ? 1f - Mathf.Clamp01((float)(R.next / sim.Gap)) : 1f;
            clawWind.enabled = fuel && auto;
            claw.transform.position = at;
            claw.transform.rotation = Quaternion.Euler(0, 0, t * 40f);        // 조준점이 천천히 돈다
            claw.transform.localScale = Vector3.one * (0.12f + 0.05f * wind) / Mathf.Max(0.01f, droneArt.bounds.size.x);
            clawRing.transform.position = at;
            clawRing.transform.localScale = Vector3.one * r / ring.bounds.size.x;
            clawRing.color = fuel ? new Color(1f, 0.76f, 0.3f, auto ? 0.3f + 0.5f * wind : 0.9f) : new Color(0.5f, 0.54f, 0.6f, 0.4f);
            float ang = wind * Mathf.PI * 2 + Mathf.PI / 2;
            clawWind.transform.position = at + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * r / 2;
        }

        // ───────────────────────────────── 입자

        P Add(Sprite s, Vector3 at, float size, Color c, int kind, float life, float grow = 0)
        {
            var sr = pool.Count > 0 ? pool.Pop() : null;
            if (sr == null) { var go = new GameObject("fx"); go.transform.SetParent(transform); sr = go.AddComponent<SpriteRenderer>(); }
            sr.gameObject.SetActive(true);
            sr.sprite = s; sr.color = c; sr.sortingOrder = kind == 2 ? 90 : kind == 7 ? 38 : 40;
            sr.transform.position = at; sr.transform.rotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, s.bounds.size.x);
            var p = new P { sr = sr, c = c, kind = kind, life = life, size = grow > 0 ? grow : size };
            fx.Add(p);
            return p;
        }

        void Burst(Vector3 at, Color c, int n, float speed)
        {
            for (int i = 0; i < n && fx.Count < 900; i++)
                Add(pixel, at, Random.Range(0.05f, 0.1f), Color.Lerp(c, Color.white, Random.value * 0.3f), 0, Random.Range(0.35f, 0.8f)).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(speed * 0.25f, speed));
        }

        void UpdateFx(float dt)
        {
            Vector3 anchor = hud != null ? cam.ScreenToWorldPoint(new Vector3(hud.CreditScreen.x, hud.CreditScreen.y, 10)) : Vector3.zero;
            anchor.z = 0;
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var p = fx[i];
                p.age += dt;
                float k = p.age / p.life;
                var tr = p.sr.transform;
                bool dead = k >= 1f;
                switch (p.kind)
                {
                    case 0: p.v *= Mathf.Exp(-3f * dt); tr.position += p.v * dt; p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1 - k); break;
                    case 2:   // 금화 — 돈 숫자까지 실제로 날아간다
                        if (p.age > 0.25f) { var to = anchor - tr.position; p.v = Vector3.Lerp(p.v, to.normalized * 18f, 1 - Mathf.Exp(-dt * 7f)); if (to.magnitude < 0.35f) { dead = true; creditPulse = 1f; OrbitSfx.Play("coin", 0.3f, 0.05f, 0.1f); } }
                        else p.v *= Mathf.Exp(-3f * dt);
                        tr.position += p.v * dt;
                        break;
                    case 3:
                        tr.position = (p.a + p.b) / 2;
                        var dir = p.b - p.a;
                        tr.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(dir.magnitude / pixel.bounds.size.x, 0.04f / pixel.bounds.size.y, 1);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1 - k);
                        break;
                    case 5:
                        float s = Mathf.Lerp(0.1f, p.size, 1 - (1 - k) * (1 - k));
                        tr.localScale = Vector3.one * s / ring.bounds.size.x;
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1 - k);
                        break;
                    case 6: tr.position += p.v * dt; break;       // 추심선
                    case 8:      // 빔 — 포구에서 조준점까지, 굵기가 줄며 사라진다
                    {
                        tr.position = (p.a + p.b) / 2;
                        var d8 = p.b - p.a;
                        tr.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d8.y, d8.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(d8.magnitude / pixel.bounds.size.x, p.size * (1 - k * 0.7f) / pixel.bounds.size.y, 1);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, p.c.a * (1 - k));
                        break;
                    }
                    case 7: p.sr.color = new Color(p.c.r, p.c.g, p.c.b, p.c.a * (1 - k)); break;
                }
                if (dead) { p.sr.gameObject.SetActive(false); pool.Push(p.sr); fx.RemoveAt(i); }
            }
            for (int i = pops.Count - 1; i >= 0; i--) { pops[i].age += dt; pops[i].px.y -= 30 * dt; if (pops[i].age > 1f) pops.RemoveAt(i); }
        }

        // ───────────────────────────────── 그림 (단순한 도형)

        SpriteRenderer Make(Sprite s, Vector3 pos, float size, Color c, int order)
        {
            var go = new GameObject("v"); go.transform.SetParent(transform); go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, s.bounds.size.x);
            return sr;
        }

        void Stars()
        {
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
