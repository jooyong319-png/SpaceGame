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
    public partial class SweepGame : MonoBehaviour
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
        SpriteRenderer shipView, shipFlame;
        readonly List<SpriteRenderer> mineViews = new List<SpriteRenderer>();
        SpriteRenderer earth, ringB, ringF, atmo, rim, band, bandGlow, claw, clawRing, clawWind, holeCore, holeGlow, holeRing, moon, sun, sunCore;
        float t, saveTimer, bandInner = -1, earthR = 120, camBase;
        Sprite thinRing; readonly SpriteRenderer[] tracks = new SpriteRenderer[5]; readonly SpriteRenderer[] trackDots = new SpriteRenderer[30]; readonly float[] dotPhase = new float[30];
        public bool aimOn, holdOn;
        bool castPending;
        public Vector2 aimPx;
        public static bool TestAim, TestHold;
        public static double PerfSim, PerfFx, PerfDraw, PerfUfx; public static int PerfFxN; static readonly System.Diagnostics.Stopwatch PerfSw = new System.Diagnostics.Stopwatch();   // ⏱ 렉 재기 (에디터 계측 · 09-26)
        public static Vector2 TestPx;

        // 도파민 사다리 (§5)
        public float hitStop, slowMo, flash, edgeGlow, bandLit, rimLit, shake, creditPulse, kessT, tallyPulse;
        public double runTally; SweepRun tallyRun;                         // 💰 「이번 판」 계산대 (시안 DbsvFEEy1K5ddbZsM61B2y)
        public string kessText;

        class P { public SpriteRenderer sr; public Vector3 v, a, b; public float age, life, size; public Color c; public int kind; }
        readonly List<P> fx = new List<P>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        public class Pop { public Vector2 px; public string text, baseText; public Color c; public float age, size; public double val; public int n = 1; }
        // 📣 알림줄 — 특별한 일(황금 · 열쇠 · 운석 · 붕괴 …)은 맞은 자리가 아니라 왼쪽 가장자리에 한 줄 (09-26 정돈 2)
        public class Notice { public string text; public Color c; public float t; public int n = 1; }
        public readonly List<Notice> notices = new List<Notice>();
        public void Note(string text, Color c)
        {
            foreach (var q in notices) if (q.text == text && q.t < 2f) { q.n++; q.t = 0; return; }
            notices.Add(new Notice { text = text, c = c });
            if (notices.Count > 4) notices.RemoveAt(0);
        }
        public readonly List<Pop> pops = new List<Pop>();

        public static readonly Color Amber = new Color(0.95f, 0.76f, 0.31f), Amber2 = new Color(1f, 0.87f, 0.58f), Cyan = new Color(0.44f, 0.83f, 0.91f),
            Violet = new Color(0.71f, 0.61f, 1f), Red = new Color(0.89f, 0.35f, 0.29f), Green = new Color(0.44f, 0.81f, 0.59f), Ice = new Color(0.62f, 0.85f, 1f),
            Mag = new Color(0.88f, 0.48f, 0.88f), Orange = new Color(1f, 0.6f, 0.3f), Grey = new Color(0.7f, 0.73f, 0.78f);

        // ───────────────────────────────── 부팅

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<SweepGame>() != null) return;
            new GameObject(Loc.T("== 궤도 청소부 rev17 ==")).AddComponent<SweepGame>();
        }

        void Awake()
        {
            TestAim = false; TestHold = false;                                // 시험 스위치는 Play 를 넘어 남는다 (정적) — 켜진 채 남으면 사장님 커서가 안 먹는다
            SweepHud.CastReq = false;
            autoMode = PlayerPrefs.GetInt("orbit.auto", 0) == 1;
            Application.targetFrameRate = 60;
            OrbitMusic.Ensure();                                             // 🎵 배경음 (09-25)
            if (!Application.isEditor)                                                  // 🖥 빌드는 전체 화면으로 시작 (09-24) — 창 모드 · 창 크기는 설정에서 (09-27, Alt+Enter 끔)
            {
                SweepHud.ApplyScreen();                                                 // 🖥 09-27 설정에 저장한 화면 · 창 크기로 (처음엔 전체 화면)
            }
            Load();
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); }
            cam.orthographic = true; cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.012f, 0.02f, 0.04f);
            cam.transform.position = new Vector3(0, 0, -10);
            // 🟦 640×360 도트 격자 — 게임 화면은 작은 그림에 그려 도트 그대로 키운다 (09-24 사장님 「픽셀 개수를 안 정해서 지저분」). 글자(HUD)는 화면 해상도 그대로
            EnsurePixRT();
            var pc = new GameObject("Present Camera").AddComponent<Camera>();
            pc.clearFlags = CameraClearFlags.SolidColor; pc.backgroundColor = Color.black; pc.cullingMask = 0; pc.depth = cam.depth + 1; pc.orthographic = true;
            if (FindFirstObjectByType<Light2D>() == null) { var l = new GameObject("Global Light 2D").AddComponent<Light2D>(); l.lightType = Light2D.LightType.Global; }
            // ✨ 블룸 — 빔 · 맞는 자리 · 블랙홀이 번져 빛난다 (09-24 사장님 참고 그림)
            {
                var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true;
                var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                var bl = prof.Add<Bloom>(true); bl.threshold.Override(0.82f); bl.intensity.Override(1.1f); bl.scatter.Override(0.72f); bl.clamp.Override(8f);
                var vol = new GameObject("Bloom Volume").AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true; vol.priority = 10; vol.sharedProfile = prof;
            }

            disc = Ring(128, 0f); ring = Ring(128, 0.9f); pixel = Ring(8, 0f); glow = Glow(128); square = Square();
            junkArt = OrbitArt.Debris(); deadSat = OrbitArt.DeadSat(); wreck = OrbitArt.BigWreck(); droneArt = OrbitArt.Drone();

            Stars();
            moon = Make(disc, PxToWorld(790, 122), 0.6f, new Color(0.6f, 0.62f, 0.66f), 1);
            sun = Make(glow, PxToWorld(160, 500), 7.5f, new Color(1f, 0.86f, 0.6f, 0.18f), 0);     // 정지궤도 — 멀리 있는 태양
            sunCore = Make(disc, PxToWorld(160, 500), 0.5f, new Color(1f, 0.95f, 0.82f, 0.9f), 1);
            atmo = Make(glow, Vector3.zero, 3.4f, new Color(0.35f, 0.6f, 1f, 0.35f), 4);
            earth = Make(PlanetArt.Get(0), Vector3.zero, 2.4f, Color.white, 5);
            ringB = Make(PlanetArt.RingBack, Vector3.zero, 1f, Color.white, 4);      // 토성 고리 — 뒤 · 앞
            ringF = Make(PlanetArt.RingFront, Vector3.zero, 1f, Color.white, 6);
            rim = Make(ring, Vector3.zero, 2.5f, new Color(1f, 0.87f, 0.58f, 0), 6);
            band = Make(disc, Vector3.zero, 7f, new Color(1, 1, 1, 0.05f), 1);
            bandGlow = Make(ring, Vector3.zero, 7f, new Color(1f, 0.76f, 0.3f, 0), 2);
            // 🛰 궤도선 — 가는 타원 다섯 줄 + 선을 따라 도는 빛점 (09-24 사장님 「좀 더 궤도 같은 느낌」)
            thinRing = Ring(1024, 0.993f); bandGlow.sprite = thinRing;
            for (int k = 0; k < 5; k++) tracks[k] = Make(thinRing, Vector3.zero, 7f, new Color(1, 1, 1, 0), 2);
            for (int i = 0; i < trackDots.Length; i++) { trackDots[i] = Make(glow, Vector3.zero, 0.16f, new Color(1, 1, 1, 0), 3); dotPhase[i] = Random.value * 6.283f; }
            claw = Make(droneArt, Vector3.zero, 0.5f, Color.white, 70);
            shipView = Make(droneArt, Vector3.zero, 0.62f, new Color(1f, 0.9f, 0.7f), 72);            // 🚀 청소선 — 궤도 바깥에서 조준 쪽으로 (09-24)
            shipFlame = Make(glow, Vector3.zero, 0.5f, new Color(1f, 0.6f, 0.25f, 0.6f), 71);
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

        // 📝 09-26 사장님 「내 기준으로 청구서를 맞추자」 — 판이 끝날 때마다 한 줄 (에디터에서만 · SalvageRun/playlog.tsv)
        void PlayLog()
        {
#if UNITY_EDITOR
            try
            {
                var S = sim.S; var R = sim.R; var g = sim.GateJunk;
                double h = 0, m = 0; for (int i = 0; i < SweepSim.NodeCount; i++) { var nd = SweepSim.Nodes[i]; if (nd.id.StartsWith("p_")) continue; h += System.Math.Min(S.lv[i], nd.max); m += nd.max; }
                string path = Application.dataPath + "/../../playlog.tsv";
                if (!System.IO.File.Exists(path)) System.IO.File.AppendAllText(path, Loc.T("시각\t분\t회사\t판\t청구서\t금액\t기한\t연체\t돈\t벌이\t빚\t궤도\t관문수\t관문체력\t트리%\t연쇄") + "\n");
                System.IO.File.AppendAllText(path, System.DateTime.Now.ToString("MM-dd HH:mm") + "\t" + (sim.M.playSeconds / 60).ToString("0.0") + "\t" + sim.M.company + "\t" + S.runs + "\t" + S.bill + "\t" + System.Math.Round(sim.BillAmount) + "\t" + S.billDue + "\t" + (S.overdue ? 1 : 0) + "\t" + System.Math.Round(S.cash) + "\t" + System.Math.Round(R != null ? R.Earned : 0) + "\t" + System.Math.Round(S.debt) + "\t" + SweepSim.Orbits[S.orbit].name + "\t" + sim.ZoneOpen + "\t" + (g != null ? g.hp + "/" + g.max : "-") + "\t" + (m > 0 ? h / m * 100 : 0).ToString("0") + "\t" + (R != null ? R.chainBest : 0) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning("playlog " + e.Message); }
#endif
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
                m.legend = old.legend; m.bestDepth = old.bestDepth;               // ★ 전설 경력 · 무한 궤도 최고 층은 남는다
            }
            sim = new SweepSim(null, m);
            if (keepRecords) sim.S.keys += m.legend;                             // ★ 하나당 처음 열쇠 +1
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
                if (Application.isEditor && kb.f2Key.wasPressedThisFrame) timeScale = timeScale > 1f ? 1f : 3f;   // 🔧 3배속은 시험용 — 출시 빌드에선 안 된다
                if (kb.mKey.wasPressedThisFrame && OrbitSfx.I != null) OrbitSfx.I.ToggleMute();
                if (kb.aKey.wasPressedThisFrame && !sim.R.over) ToggleAuto();                  // 🎯 자동 조준 ON/OFF
                if (kb.sKey.wasPressedThisFrame && hud != null) { if (sim.R.over) { if (hud.flow == 2) hud.GoFlow(4); else if (hud.flow == 4) hud.GoFlow(2); } else if (sim.StockOpen) hud.stockOpen = !hud.stockOpen; }   // 📈 판 중 = 주식 창 · 조종실 = 증권 방
            }
            ReadAim();
            if (hud != null && sim != null)
            {   // 🎵 지금 화면에 맞는 곡 — 로비 · 방 = 조종실, 출동 = 행성 따라 셋, 연체 = 긴장, 엔딩 = 빚 청산
                string mw;
                if (sim.M.won) mw = "end";
                else if (!sim.R.over) mw = sim.M.endless || sim.Rank >= 6 ? "runC" : sim.Rank >= 3 ? "runB" : "runA";
                else mw = !hud.lobby && sim.S.overdue && !sim.M.cleanReady ? "due" : "cockpit";
                OrbitMusic.Want(mw);
            }
            if (sim.R != null && !sim.R.over) sim.MarketTick(dt);          // 📈 시장은 출동 중에만 흐른다 (09-24 사장님 「끝난 상태에선 움직이지 않게」)
            if (!sim.R.over)
            {
                float sdt = dt * timeScale;
                if (hud != null && hud.launchT > 0) sdt = 0;                   // 🚀 출발 연출 중 — 판은 아직
                else if (hitStop > 0) { hitStop -= dt; sdt = 0; }
                else if (slowMo > 0) { slowMo -= dt; sdt *= 0.4f; }
                int steps = Mathf.Max(1, Mathf.CeilToInt(sdt / 0.03f));
                PerfSw.Restart();
                if (sdt > 0) { for (int i = 0; i < steps; i++) sim.Tick(sdt / steps, aimPx.x, aimPx.y, aimOn, holdOn || castPending); castPending = false; }   // 히트스톱 중에 누른 것도 멈춤이 풀리면 열린다
            }
            else { sim.IdleTick(dt); castPending = false; }             // 조종실 창밖 — 궤도는 계속 돈다
            PerfSim = PerfSim * 0.9 + PerfSw.Elapsed.TotalMilliseconds * 0.1; PerfSw.Restart();
            Consume();
            PerfFx = PerfFx * 0.9 + PerfSw.Elapsed.TotalMilliseconds * 0.1; PerfSw.Restart();
            DrawWorld();
            DrawJunk();
            DrawTools();
            DrawOrbs();                                                       // ⚡ 전격선 구체
            DrawMissiles();                                                   // 🚀 미사일선
            PerfDraw = PerfDraw * 0.9 + PerfSw.Elapsed.TotalMilliseconds * 0.1; PerfSw.Restart();
            UpdateFx(dt);
            PerfUfx = PerfUfx * 0.9 + PerfSw.Elapsed.TotalMilliseconds * 0.1; PerfFxN = fx.Count;
            creditPulse = Mathf.MoveTowards(creditPulse, 0, dt * 3f); tallyPulse = Mathf.MoveTowards(tallyPulse, 0, dt * 3f);
            shake = Mathf.MoveTowards(shake, 0, dt * 0.9f);
            flash = Mathf.MoveTowards(flash, 0, dt * 1.5f);
            edgeGlow = Mathf.MoveTowards(edgeGlow, 0, dt * 0.5f);
            bandLit = Mathf.MoveTowards(bandLit, 0, dt * 0.6f);
            rimLit = Mathf.MoveTowards(rimLit, 0, dt * 0.5f);
            kessT = Mathf.MoveTowards(kessT, 0, dt);
            // 조종실에선 카메라가 물러나 지구가 창 가운데 오게 (창 = SweepHud.Win)
            bool cockpit = hud != null && hud.CockpitView;
            // 출동 중엔 궤도 띠가 화면에 차도록 당긴다 (사장님 「좀 더 확대」) — 띠가 넓어지면 그만큼 물러난다
            float wantSize = cockpit ? 9.6f : Mathf.Clamp((float)sim.Bo * 0.0145f, 3.8f, 7f) * 1.1f;   // 아래 계기판 몫만큼 물린다 (sim.ViewHalf 와 같은 식)   // 09-24 「화면에 작게 보인다」 — 더 당긴다 (띠는 30% 넓어짐)
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, wantSize, 1 - Mathf.Exp(-dt * 5f));
            float camY = cockpit ? -0.3f * cam.orthographicSize : -0.1f * cam.orthographicSize;   // 출동 중엔 궤도가 계기판 위로
            camBase = Mathf.Lerp(camBase, camY, 1 - Mathf.Exp(-dt * 5f));
            float camX = hud != null ? -hud.CockpitDx * 2f * cam.orthographicSize / 600f : 0f;   // 옆 방으로 밀리면 창밖도 같이
            cam.transform.position = new Vector3(camX, camBase, -10) + (Vector3)(Random.insideUnitCircle * shake * ShakeMul);
            foreach (var bp in bgParts) bp.sr.transform.position = new Vector3(bp.at.x + camX * bp.par, bp.at.y + camBase * bp.par, 0);   // 멀리 있는 것은 카메라를 거의 따라온다
            if (bgView != null)
            {
                float bh = cam.orthographicSize * 2f, bw = bh * cam.aspect; var bs = bgView.sprite.bounds.size;
                bgView.transform.position = new Vector3(camX * 0.9f, camBase * 0.9f, 0);        // 살짝 느리게 — 멀리 있는 느낌
                bgView.transform.localScale = Vector3.one * Mathf.Max(bw / bs.x, bh / bs.y) * 1.12f;
            }
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
            sim.VolleyManual = !autoMode;                                       // 🚀 전탄 발사 — 사람은 Space · 단추, 자동은 차면 바로 (09-26)
            if (autoMode && sim.VolleyReady) sim.FireVolley();
            if ((SweepHud.VolleyReq || kb != null && kb.spaceKey.wasPressedThisFrame) && hud != null && !hud.Blocking) sim.FireVolley();
            SweepHud.VolleyReq = false;
            sim.ManualFire = hud != null && hud.manualFire; sim.FireHeld = false;   // 👆 수동 공격 — 아래에서 누르고 있으면 켠다
            if (TestAim && hud != null && !hud.Blocking) { aimPx = TestPx; aimOn = true; holdOn = TestHold; sim.FireHeld = true; return; }   // 에디터 시험용 (MCP 자동 플레이)
            if (mouse == null || hud == null || hud.Blocking) return;
            Vector2 sp = mouse.position.ReadValue();
            bool inside = !(sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height);
            if ((sp - lastMouse).sqrMagnitude > 4) { idleT = 0; lastMouse = sp; } else idleT += Time.deltaTime;
            // 🎯 자동 조준 — 사면 늘 스스로 잔해를 찾는다. 왼쪽 단추를 누르고 있을 때만 마우스로 직접 (사장님 「마우스 따라다니는데?」)
            bool manual = inside && !hud.overSkill && !hud.overAuto && !hud.overStock && !hud.overVolley && mouse.leftButton.isPressed;
            if ((manual || !autoMode) && !sim.R.over) sim.R.idleT = 0;          // ★ 게으름 보너스 — 손을 대면 처음부터
            autoAiming = autoMode && !sim.R.over && !manual;
            sim.FireHeld = manual || autoAiming && !hud.manualFire;
            if (manual && mouse.leftButton.wasPressedThisFrame) sim.PressFire();   // 👆 누르는 순간 한 방            // 수동이면 AUTO 칸이 있어도 누르고 있는 동안만 — 자동 · 수동을 그대로 비교하게 (09-26)
            if (autoAiming) { AutoAim(3); return; }
            if (!inside) return;
            Vector3 w = ScreenToWorld(sp);
            aimPx = new Vector2(480 + (w.x - cam.transform.position.x) * PxPerUnit, 310 - (w.y - cam.transform.position.y) * PxPerUnit);
            aimOn = true;
            if (hud.overSkill || hud.overAuto || hud.overStock || hud.overVolley) aimOn = false;          // 스킬 칸 위 — 빔 자리는 그대로 둔다
            else if (mouse.leftButton.wasPressedThisFrame && !sim.R.over && sim.ClickShot(aimPx.x, aimPx.y)) { retKick = Mathf.Max(retKick, 1f); shake = Mathf.Max(shake, 0.04f); OrbitSfx.Play("clank", 0.5f, 0.02f, 0.1f); }   // 👆 수동 사격
        }

        Vector2 lastMouse, autoTarget; float idleT, autoRetarget, autoDwell, autoBanT; int autoHp; Junk autoJunk, autoBan, prevAuto;
        public bool autoAiming, autoMode;
        public static float ShakeMul = 1f, FlashMul = 1f;               // ⚙ 설정 — 흔들림 · 번쩍임
        float brokeWinT, brokeStopT; int brokeWinN;                                   // 💥 최근 0.25초 부서진 수 — 적을 때만 굵은 연출
        public void ToggleAuto() { autoMode = !autoMode; PlayerPrefs.SetInt("orbit.auto", autoMode ? 1 : 0); PlayerPrefs.Save(); OrbitSfx.Play("tick", 0.7f); }
        void AutoAim(int al)
        {
            var R = sim.R;
            autoRetarget -= Time.deltaTime; autoBanT -= Time.deltaTime;
            // 🔴 한 잔해에 붙어 2초 동안 체력이 안 줄면 포기하고 4초 동안 다시 안 고른다 (사장님 09-24 「화성에서 오토가 멈춤」 — 얼음 껍질)
            if (autoJunk != null && !autoJunk.dead && Vector2.Distance(aimPx, new Vector2((float)autoJunk.x, (float)autoJunk.y)) < 24)
            {
                if (autoJunk.hp < autoHp) { autoHp = autoJunk.hp; autoDwell = 0; } else autoDwell += Time.deltaTime;
                if (autoDwell > 2f) { autoBan = autoJunk; autoBanT = 4f; autoJunk = null; autoDwell = 0; }
            }
            if (autoRetarget <= 0 || autoJunk == null || autoJunk.dead)
            {
                autoRetarget = al == 1 ? 0.9f : al == 2 ? 0.5f : 1.2f;
                var keep = autoJunk != null && !autoJunk.dead && autoJunk.fade >= 0.5 ? autoJunk : null;
                autoJunk = null; float best = float.MaxValue;
                if (al < 3)
                {
                    foreach (var d in R.junk) { if (d.dead || d.fade < 0.5) continue; float dd = (float)((d.x - aimPx.x) * (d.x - aimPx.x) + (d.y - aimPx.y) * (d.y - aimPx.y)); if (dd < best) { best = dd; autoJunk = d; } }
                }
                else
                {
                    // 빽빽하고 가까운 곳 — 주변 60 안의 수 ÷ 거리 벌점. 지금 목표보다 확실히(35%) 나아야 옮긴다 (사장님 09-24 「여기저기 널뛰기」)
                    float cur = keep != null ? AutoScore(keep) : -1, bestS = -1;
                    for (int t = 0; t < 40 && R.junk.Count > 0; t++)
                    {
                        var c = R.junk[Random.Range(0, R.junk.Count)]; if (c.dead || c.fade < 0.5 || (autoBanT > 0 && c == autoBan)) continue;
                        float sc = AutoScore(c);
                        if (sc > bestS) { bestS = sc; autoJunk = c; }
                    }
                    if (keep != null && bestS < cur * 1.35f) autoJunk = keep;
                }
            }
            if (autoJunk != prevAuto) { prevAuto = autoJunk; autoDwell = 0; autoHp = autoJunk != null ? autoJunk.hp : 0; }
            if (autoJunk != null) autoTarget = new Vector2((float)autoJunk.x, (float)autoJunk.y);
            float sp = al == 1 ? 170 : al == 2 ? 320 : 380;
            aimPx = Vector2.MoveTowards(aimPx, autoTarget, sp * Time.deltaTime * timeScale);
            aimOn = true;
        }

        float AutoScore(Junk c)
        {
            int n = 0; foreach (var d in sim.R.junk) if (!d.dead && (d.x - c.x) * (d.x - c.x) + (d.y - c.y) * (d.y - c.y) < 3600) n++;
            float dist = Vector2.Distance(aimPx, new Vector2((float)c.x, (float)c.y));
            return n / (1f + dist / 160f);
        }

        // 🟦 도트 격자 — 모니터 높이를 정수로 나눠 약 540줄 (1080p = 960×540 ×2 · 1600 = 853×533 ×3). 띠 없이 도트가 고르다 (09-24 사장님 「픽셀 수를 좀 늘리자」)
        public int PixW = 960, PixH = 540, PixS = 2;
        RenderTexture pixRT;
        void EnsurePixRT()
        {
            int sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            int s = Mathf.Max(1, Mathf.RoundToInt(sh / 540f)), w = Mathf.Max(1, sw / s), h = Mathf.Max(1, sh / s);
            if (pixRT != null && w == PixW && h == PixH && s == PixS) return;
            PixW = w; PixH = h; PixS = s;
            if (pixRT != null) { cam.targetTexture = null; pixRT.Release(); Destroy(pixRT); }
            pixRT = new RenderTexture(PixW, PixH, 24, RenderTextureFormat.ARGBHalf) { filterMode = FilterMode.Point, name = "PixelGrid" };
            cam.targetTexture = pixRT;
        }
        public Rect ViewRect { get { float w = PixW * PixS, h = PixH * PixS; return new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h); } }
        public Vector3 ScreenToWorld(Vector2 sp)                                           // 화면 좌표(아래가 0) → 월드 (도트 격자를 거쳐)
        {
            var v = ViewRect; return cam.ScreenToWorldPoint(new Vector3((sp.x - v.x) / v.width * PixW, (sp.y - v.y) / v.height * PixH, 10));
        }
        public Vector3 WorldToScreen(Vector3 w)                                            // 월드 → 화면 좌표(아래가 0)
        {
            var v = ViewRect; var p = cam.WorldToScreenPoint(w); return new Vector3(v.x + p.x / PixW * v.width, v.y + p.y / PixH * v.height, p.z);
        }
        void OnGUI()                                                                       // 도트 격자 그림을 화면에 — HUD(SweepHud.OnGUI) 보다 뒤에
        {
            GUI.depth = 100;
            if (Event.current.type == EventType.Layout) EnsurePixRT();                     // 창 크기가 바뀌면 격자도 다시
            if (Event.current.type == EventType.Repaint && pixRT != null) GUI.DrawTexture(ViewRect, pixRT, ScaleMode.StretchToFill, false);
        }

        public Vector3 PxToWorld(double x, double y) => new Vector3((float)(x - 480) / PxPerUnit, (float)(310 - y) / PxPerUnit, 0);

    }
}
