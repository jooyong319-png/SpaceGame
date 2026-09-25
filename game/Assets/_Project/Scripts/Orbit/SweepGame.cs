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
        SpriteRenderer shipView, shipFlame;
        readonly List<SpriteRenderer> mineViews = new List<SpriteRenderer>();
        SpriteRenderer earth, ringB, ringF, atmo, rim, band, bandGlow, claw, clawRing, clawWind, holeCore, holeGlow, holeRing, moon, sun, sunCore;
        float t, saveTimer, bandInner = -1, earthR = 120, camBase;
        Sprite thinRing; readonly SpriteRenderer[] tracks = new SpriteRenderer[5]; readonly SpriteRenderer[] trackDots = new SpriteRenderer[30]; readonly float[] dotPhase = new float[30];
        public bool aimOn, holdOn;
        bool castPending;
        public Vector2 aimPx;
        public static bool TestAim, TestHold;
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
            new GameObject("== 궤도 청소부 rev17 ==").AddComponent<SweepGame>();
        }

        void Awake()
        {
            TestAim = false; TestHold = false;                                // 시험 스위치는 Play 를 넘어 남는다 (정적) — 켜진 채 남으면 사장님 커서가 안 먹는다
            SweepHud.CastReq = false;
            autoMode = PlayerPrefs.GetInt("orbit.auto", 0) == 1;
            Application.targetFrameRate = 60;
            OrbitMusic.Ensure();                                             // 🎵 배경음 (09-25)
            if (!Application.isEditor)                                                  // 🖥 빌드는 늘 모니터 전체 화면으로 시작 (09-24 사장님 「전체 화면으로」) — Alt+Enter 로 창 모드
            {
                var d = Screen.currentResolution; Screen.SetResolution(d.width, d.height, FullScreenMode.FullScreenWindow);
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
                if (kb.f2Key.wasPressedThisFrame) timeScale = timeScale > 1f ? 1f : 3f;
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
                if (sdt > 0) { for (int i = 0; i < steps; i++) sim.Tick(sdt / steps, aimPx.x, aimPx.y, aimOn, holdOn || castPending); castPending = false; }   // 히트스톱 중에 누른 것도 멈춤이 풀리면 열린다
            }
            else { sim.IdleTick(dt); castPending = false; }             // 조종실 창밖 — 궤도는 계속 돈다
            Consume();
            DrawWorld();
            DrawJunk();
            DrawTools();
            UpdateFx(dt);
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
            if (TestAim && hud != null && !hud.Blocking) { aimPx = TestPx; aimOn = true; holdOn = TestHold; return; }   // 에디터 시험용 (MCP 자동 플레이)
            if (mouse == null || hud == null || hud.Blocking) return;
            Vector2 sp = mouse.position.ReadValue();
            bool inside = !(sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height);
            if ((sp - lastMouse).sqrMagnitude > 4) { idleT = 0; lastMouse = sp; } else idleT += Time.deltaTime;
            // 🎯 자동 조준 — 사면 늘 스스로 잔해를 찾는다. 왼쪽 단추를 누르고 있을 때만 마우스로 직접 (사장님 「마우스 따라다니는데?」)
            bool manual = inside && !hud.overSkill && !hud.overAuto && !hud.overStock && mouse.leftButton.isPressed;
            if ((manual || !autoMode) && !sim.R.over) sim.R.idleT = 0;          // ★ 게으름 보너스 — 손을 대면 처음부터
            autoAiming = autoMode && !sim.R.over && !manual;
            if (autoAiming) { AutoAim(3); return; }
            if (!inside) return;
            Vector3 w = ScreenToWorld(sp);
            aimPx = new Vector2(480 + (w.x - cam.transform.position.x) * PxPerUnit, 310 - (w.y - cam.transform.position.y) * PxPerUnit);
            aimOn = true;
            if (hud.overSkill || hud.overAuto || hud.overStock) aimOn = false;          // 스킬 칸 위 — 빔 자리는 그대로 둔다
            else if (mouse.leftButton.wasPressedThisFrame && !sim.R.over && sim.ClickShot(aimPx.x, aimPx.y)) { retKick = Mathf.Max(retKick, 1f); shake = Mathf.Max(shake, 0.04f); OrbitSfx.Play("clank", 0.5f, 0.02f, 0.1f); }   // 👆 수동 사격
        }

        Vector2 lastMouse, autoTarget; float idleT, autoRetarget, autoDwell, autoBanT; int autoHp; Junk autoJunk, autoBan, prevAuto;
        public bool autoAiming, autoMode;
        public static float ShakeMul = 1f, FlashMul = 1f;               // ⚙ 설정 — 흔들림 · 번쩍임
        float brokeWinT; int brokeWinN;                                   // 💥 최근 0.25초 부서진 수 — 적을 때만 굵은 연출
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

        // ───────────────────────────────── 사건 → 연출 · 소리

        void Consume()
        {
            while (sim.Events.Count > 0)
            {
                var e = sim.Events.Dequeue();
                if ((e.kind == SwEv.Laser || e.kind == SwEv.Vac || e.kind == SwEv.Shell || e.kind == SwEv.Rail || e.kind == SwEv.Bolt) && sim.R != null && !sim.R.over
                    && (e.kind != SwEv.Bolt || e.v < 0.5)                                     // 번개는 첫 줄기만 — 튀는 줄기까지 포구에서 뻗으면 화면을 가르는 선이 됐다 (09-25)
                    && (curW > 0 || System.Math.Abs(e.x - sim.ShipX) < 2 && System.Math.Abs(e.y - sim.ShipY) < 2))   // 무기 포대가 쏘는 중이면 늘 그 포구에서
                {
                    var mw = PxToWorld(e.x, e.y); var nt = ShotFrom(); e.x = 480 + nt.x * PxPerUnit; e.y = 310 - nt.y * PxPerUnit;
                    if ((e.kind == SwEv.Laser && (e.k & 64) == 0) || e.kind == SwEv.Rail) { e.x2 += e.x - (480 + mw.x * PxPerUnit); e.y2 += e.y - (310 - mw.y * PxPerUnit); }   // 64 = 조준점에서 멈추는 빔
                }
                var at = PxToWorld(e.x, e.y);
                switch (e.kind)
                {
                    case SwEv.SkillReady: OrbitSfx.Play("tick", 0.5f, 1.4f, 0.05f); break;
                    case SwEv.Supply:
                        PopAt(e.x, e.y - 10, e.text, e.k == 1 ? Violet : Green, 15);
                        OrbitSfx.Play("supply", 0.7f, 0.1f);
                        break;
                    case SwEv.SupplyGet:
                        PopAt(e.x, e.y, e.text, e.k == 1 ? Violet : Green, 18);
                        Burst(at, e.k == 1 ? Violet : Green, 14, 3f);
                        OrbitSfx.Play("grab", 0.9f, 0.05f);
                        break;
                    case SwEv.Strike:
                    {
                        curW = 0;
                        bool spot = e.v <= SweepSim.PickR + 0.1;      // 아직 좁은 빔 — 한 점
                        Beam(at, e.k == 1);
                        if (e.k == 1) RingFx(at, Amber2, spot ? 0.26f : 0.22f, (float)e.v * 2 / PxPerUnit);
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
                        // 💥 드물게 부서질 때(초반)만 굵게 — 번쩍 · 파편 · 경직 · 흔들림 (09-24 사장님 15번 「극 초반 타격감」). 후반 수백 개/초엔 가벼운 그대로
                        if (Time.time - brokeWinT > 0.25f) { brokeWinT = Time.time; brokeWinN = 0; }
                        if (++brokeWinN <= 4)
                        {
                            Add(glow, at, 1.1f * cam.orthographicSize / 6f, new Color(1f, 0.93f, 0.75f, 0.9f), 7, 0.09f).sr.sortingOrder = 120;
                            Burst(at, Color.Lerp(JunkColor(k), Color.white, 0.3f), 6, 4f);
                            hitStop = Mathf.Max(hitStop, 0.03f); shake = Mathf.Max(shake, 0.07f);
                            OrbitSfx.Play("clank", 0.55f, 0.08f, 0.02f);
                        }
                        Burst(at, JunkColor(k), k == SweepSim.Big ? 30 : k == SweepSim.Vault ? 10 : k == SweepSim.Chip ? 1 : 3, k == SweepSim.Big ? 6f : 3f);
                        if (att != Att.None && att != Att.Cable) Burst(at, AttColor(att), 3, 3.5f);
                        OrbitSfx.Play(k == SweepSim.Big ? "break" : k == SweepSim.Vault ? "unit" : k == SweepSim.Chip ? "pick" : "clank", k == SweepSim.Chip ? 0.3f : 0.7f, 0.03f);
                        if (k == SweepSim.Big) shake = Mathf.Max(shake, 0.2f);
                        break;
                    }
                    case SwEv.Coin:
                    {
                        int src = e.k % 10; bool cut = e.k >= 10;
                        Color c = src == 3 ? Red : cut ? new Color(0.9f, 0.65f, 0.6f) : Amber2;
                        if (sim.R != tallyRun) { tallyRun = sim.R; runTally = 0; }
                        if (src != 3 && e.v > 0) runTally += e.v;                                   // 값은 계산대에 모은다 — 쓰레기 위 숫자는 아주 큰 것만
                        if (e.v >= 1 && (src == 3 || e.v > sim.ValMult * 400 || brokeWinN <= 2)) CoinPop(e.x, e.y - 8, src == 3 ? "빚 -" : "+", e.v, c);
                        if (Random.value < 0.25f) Add(disc, at, 0.11f, src == 3 ? Red : Amber, 2, 1.6f).v = (Vector3)(Random.insideUnitCircle * 3f);
                        break;
                    }
                    case SwEv.Pop: if (e.k >= 3) Note(e.text, e.k == 3 ? Orange : e.k == 4 ? new Color(1f, 0.5f, 0.85f) : Violet); else PopAt(e.x, e.y, e.text, e.k == 1 ? Green : Amber2, 15); if (e.k == 3) OrbitSfx.Play("unit", 0.8f); if (e.k >= 4) OrbitSfx.Play("buy", 0.6f); break;
                    case SwEv.Meteor:
                    {
                        var to = at; var from = to + new Vector3(-6f, 7f, 0);
                        var tail = Add(pixel, from, 0.05f, new Color(1f, 0.6f, 0.2f, 0.9f), 8, 0.55f); tail.a = from; tail.b = to; tail.size = 0.5f;
                        var core = Add(pixel, from, 0.05f, new Color(1f, 0.95f, 0.8f, 1f), 8, 0.5f); core.a = from; core.b = to; core.size = 0.15f;
                        Add(glow, to, 2.2f, new Color(1f, 0.55f, 0.2f, 0.7f), 7, 0.9f);
                        OrbitSfx.Play("launch", 0.7f); shake = Mathf.Max(shake, 0.25f); flash = Mathf.Max(flash, 0.25f);
                        Note("운석!", Orange);
                        break;
                    }
                    case SwEv.Tourist:
                    {
                        var p = Add(droneArt, PxToWorld(-40, 120), 0.9f, new Color(0.6f, 0.85f, 1f), 6, 4f);
                        p.sr.transform.rotation = Quaternion.Euler(0, 0, -90); p.sr.sortingOrder = 85; p.v = new Vector3(1040f / PxPerUnit / 4f, -0.1f, 0);
                        break;
                    }
                    case SwEv.Laser:
                    {
                        var s0 = PxToWorld(e.x, e.y); var s1 = PxToWorld(e.x2, e.y2); bool awk = (e.k & 2) != 0, cr = (e.k & 1) != 0;
                        float w = (float)e.v * 2 / PxPerUnit;
                        bool fence = (e.k & 16) != 0, ice = (e.k & 32) != 0, sling = (e.k & 4) != 0;
                        Color hc = ice ? new Color(0.55f, 0.85f, 1f, 0.4f) : fence ? new Color(1f, 0.4f, 0.45f, 0.5f) : sling ? new Color(1f, 0.6f, 0.2f, 0.45f) : awk ? new Color(1f, 0.45f, 0.9f, 0.35f) : new Color(1f, 0.3f, 0.25f, 0.35f);
                        Color cc = ice ? new Color(0.9f, 0.98f, 1f, 1f) : fence ? new Color(1f, 0.75f, 0.75f, 1f) : cr ? new Color(1f, 1f, 0.8f, 1f) : new Color(1f, 0.85f, 0.8f, 0.95f);
                        bool spot = (e.k & 64) != 0; float spotR = spot ? (float)e.v / PxPerUnit : 0;       // 64 = 조준점 원 (레이저 · 냉동)
                        if (spot) { w = ice ? 0.08f : 0.07f; hc.a *= 0.6f; }                                      // 굵으면 도트 격자에서 계단 띠가 됐다 (09-25 점검)
                        var halo = Add(pixel, s0, 0.05f, hc, 8, 0.13f); halo.a = s0; halo.b = s1; halo.size = w;
                        var core = Add(pixel, s0, 0.05f, cc, 8, 0.1f); core.a = s0; core.b = s1; core.size = Mathf.Max(0.04f, w * 0.22f);
                        if (spot && !ice)
                        {   // 🔴 태우는 점 — 픽셀랩 끓는 점이 조준점에서 반복 (없으면 빛 번짐)
                            LoadAnims();
                            if (animBurn != null)
                            {
                                if (burnView == null) burnView = Make(animBurn[0], s1, 1f, Color.white, 59);
                                burnView.transform.position = s1; burnView.transform.localScale = Vector3.one * spotR * 2.4f / animBurn[0].bounds.size.x; burnT = 0.18f;
                            }
                            else { Add(glow, s1, spotR * 2.6f, new Color(1f, 0.35f, 0.25f, 0.55f), 7, 0.12f); Add(glow, s1, spotR * 1.1f, new Color(1f, 0.95f, 0.85f, 0.9f), 7, 0.09f); }
                            if (Random.value < 0.5f) Add(pixel, s1, 0.07f, new Color(1f, 0.7f, 0.4f), 0, 0.3f).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f));
                        }
                        if (spot && ice)
                        {   // ❄ 서리 원 — 픽셀랩 얼음 폭발 (0.25초마다 한 번 · 없으면 원 + 서리)
                            LoadAnims();
                            if (animFrost != null) { if (Time.time - lastFrost > 0.25f) { lastFrost = Time.time; var fr = Make(animFrost[0], s1, spotR * 2.8f, Color.white, 58); frameFx.Add(new FrameFx { sr = fr, f = animFrost, fps = 18 }); } }
                            else { Add(ring, s1, 0.1f, new Color(0.8f, 0.95f, 1f, 0.5f), 5, 0.3f, spotR * 2f); Add(glow, s1, spotR * 2.4f, new Color(0.6f, 0.88f, 1f, 0.28f), 7, 0.22f); }
                            for (int q = 0; q < 3; q++) { var fp = s1 + (Vector3)(Random.insideUnitCircle * spotR); Add(pixel, fp, 0.08f, new Color(0.9f, 0.98f, 1f), 0, 0.5f).v = (Vector3)(Random.insideUnitCircle * 0.6f); }
                        }
                        if (cr && !fence) Star(s1, hc, 0.5f, 7, 0.16f);                             // 치명타 — 끝점에서 빛살
                        if (!fence && Random.value < 0.25f) OrbitSfx.PlayPitch("tick", 0.12f, ice ? 2.8f : 2.2f + Random.value * 0.3f);
                        break;
                    }
                    case SwEv.Proc:
                    {   // 🔫 확률 효과 발동 — 무기 이름이 조준점 위에 잠깐 · 포대가 그 색으로 번쩍
                        int pw = (int)e.v; var pc = WeaponCol(pw);
                        curW = pw; if (pw > 0 && pw < 9) { wTgt[pw] = PxToWorld(e.x, e.y + (e.k == 0 ? 26 : 20)); wRec[pw] = 1; }
                        if (e.k == 0) { Add(glow, ShotFrom(), 1.1f * cam.orthographicSize / 6f, pc, 7, 0.18f).sr.sortingOrder = 150; }
                        break;
                    }
                    case SwEv.Volley:
                    {   // 🚀 전탄 발사 — 멈칫 · 번쩍 · 흔들림 · 큰 글자
                        hitStop = Mathf.Max(hitStop, 0.22f); flash = Mathf.Max(flash, 0.55f); shake = Mathf.Max(shake, 0.35f);
                        if (hud != null) hud.Big("전탄 발사!", 1f, 34, new Color(1f, 0.87f, 0.58f));
                        OrbitSfx.Play("launch", 1f); OrbitSfx.Play("blast", 0.8f, 0.1f);
                        break;
                    }
                    case SwEv.Vac:
                    {   // 🌀 소용돌이 — 조준점 원 테두리 · 안으로 휘어 드는 알갱이 · 포구로 흘러가는 줄기 (09-24)
                        float R = (float)e.v / PxPerUnit; var mz = ShotFrom();
                        LoadAnims();
                        if (animVortex != null)
                        {
                            if (vacView == null) vacView = Make(animVortex[0], at, 1f, Color.white, 57);
                            vacView.transform.position = at; vacView.transform.localScale = Vector3.one * R * 1.6f / animVortex[0].bounds.size.x; vacView.color = new Color(1, 1, 1, 0.7f); vacT = 0.3f;   // 2.3 → 1.6 · 반투명 (09-26 정돈 3: 보라 덩어리가 숫자를 덮었다)
                        }
                        Add(glow, at, R * 1.2f, new Color(0.3f, 0.8f, 0.75f, 0.18f), 7, 0.12f);
                        for (int q = 0; q < 3; q++)
                        {
                            float aa = Random.value * 6.283f; var pp = at + new Vector3(Mathf.Cos(aa), Mathf.Sin(aa)) * R;
                            var inward = (at - pp) * 3.2f + new Vector3(-Mathf.Sin(aa), Mathf.Cos(aa)) * R * 1.1f;   // 안으로 + 조금 옆으로 — 옆 힘이 크면 화면을 가로지르는 호가 됐다 (09-24)
                            Add(pixel, pp, 0.08f, new Color(0.7f, 1f, 0.95f, 0.9f), 0, 0.28f).v = inward;
                        }
                        // 포구로 흘러가는 줄기는 뺐다 — 포대가 옆으로 가면서 화면을 가로지르는 계단 호가 됐다 (09-25 점검)
                        break;
                    }
                    case SwEv.Shell:
                    {
                        var s0 = PxToWorld(e.x, e.y); var s1 = PxToWorld(e.x2, e.y2);
                        var tr = Add(pixel, s0, 0.05f, new Color(1f, 0.7f, 0.3f, 0.8f), 8, 0.35f); tr.a = s0; tr.b = s1; tr.size = 0.12f;
                        OrbitSfx.PlayPitch("tick", 0.4f, 0.7f);
                        break;
                    }
                    case SwEv.Rail:
                    {
                        var s0 = PxToWorld(e.x, e.y); var s1 = PxToWorld(e.x2, e.y2);
                        var h = Add(pixel, s0, 0.05f, new Color(0.7f, 0.85f, 1f, 0.55f), 8, 0.35f); h.a = s0; h.b = s1; h.size = 0.7f;
                        var c = Add(pixel, s0, 0.05f, Color.white, 8, 0.28f); c.a = s0; c.b = s1; c.size = 0.16f;
                        Add(glow, s0, 1.2f, new Color(0.8f, 0.9f, 1f, 0.8f), 7, 0.3f);
                        Zap(s0, s1, new Color(0.6f, 0.8f, 1f), 0.22f, 0.35f, 12); Zap(s0, s1, new Color(0.6f, 0.8f, 1f), 0.18f, 0.25f, 12);
                        shake = Mathf.Max(shake, 0.2f); flash = Mathf.Max(flash, 0.12f);
                        OrbitSfx.PlayPitch("launch", 0.55f, 1.6f);
                        if (e.v >= 5) PopAt(e.x2 * 0.3 + e.x * 0.7, e.y2 * 0.3 + e.y * 0.7 - 20, (int)e.v + "개 관통!", Cyan, 18);
                        break;
                    }
                    case SwEv.Bolt:
                    {
                        var p0 = PxToWorld(e.x, e.y); var p1 = PxToWorld(e.x2, e.y2); var mid = (p0 + p1) / 2 + (Vector3)(Random.insideUnitCircle * 0.25f);
                        var c = e.k == 1 ? new Color(1f, 1f, 0.75f) : new Color(0.7f, 0.85f, 1f);
                        foreach (var seg in new[] { (p0, mid), (mid, p1) })
                        {
                            var gl = Add(pixel, seg.Item1, 0.05f, new Color(0.45f, 0.65f, 1f, 0.45f), 8, 0.22f); gl.a = seg.Item1; gl.b = seg.Item2; gl.size = 0.22f;
                            var co = Add(pixel, seg.Item1, 0.05f, c, 8, 0.18f); co.a = seg.Item1; co.b = seg.Item2; co.size = 0.06f;
                        }
                        Add(glow, p1, 0.6f, new Color(0.6f, 0.8f, 1f, 0.7f), 7, 0.2f);
                        OrbitSfx.PlayPitch("tick", 0.35f, 1.6f + (float)e.v * 0.12f);
                        break;
                    }
                    case SwEv.Beam: { var p = Add(pixel, at, 0.05f, Cyan, 3, 0.16f); p.a = at; p.b = PxToWorld(e.x2, e.y2); break; }
                    case SwEv.Ring:
                        LoadAnims();
                        if (e.k == 2 && animMag != null) { var mg = Make(animMag[0], at, (float)e.v * 2.3f / PxPerUnit, Color.white, 58); frameFx.Add(new FrameFx { sr = mg, f = animMag, fps = 14 }); }   // 🧲 픽셀랩 자석 — 조여든다
                        else RingFx(at, e.k == 1 ? Red : e.k == 2 ? Mag : Orange, 0.45f, (float)e.v * 2 / PxPerUnit);
                        break;
                    case SwEv.Blast:
                        RingFx(at, Orange, 0.4f, (float)e.v * 2 / PxPerUnit);
                        Fireball(at, Mathf.Clamp((float)e.v / PxPerUnit * 0.6f, 0.25f, 1.1f));
                        OrbitSfx.Play("blast", 0.5f, 0.025f, 0.12f);
                        shake = Mathf.Max(shake, 0.05f);
                        break;
                    case SwEv.Tier:
                        OnTier(e.k);
                        break;
                    case SwEv.Crit: PopAt(e.x, e.y, "치명타!", Orange, 16); OrbitSfx.Play("blast", 0.8f, 0.1f); shake = Mathf.Max(shake, 0.1f); break;
                    case SwEv.Collapse: Note("붕괴! " + (int)e.v + "개 흩어짐", Red); Burst(at, Red, 40, 7f); shake = 0.25f; OrbitSfx.Play("collide", 1f); break;
                    case SwEv.Release:
                        Burst(at, Violet, 12 + (int)Mathf.Min(24, (float)e.v), 6f);
                        shake = Mathf.Max(shake, Mathf.Min(0.3f, 0.08f + (float)e.v * 0.01f));
                        if (e.text != null) Note(e.text, Violet);
                        OrbitSfx.Play("break", 1f, 0.05f);
                        break;
                    case SwEv.Shatter: RingFx(at, Ice, 0.3f, 0.6f); OrbitSfx.Play("pick", 0.7f); break;
                    case SwEv.Warn: hud.Banner(e.text, e.k, 2.2f); OrbitSfx.Play("warn", 0.8f); break;
                    case SwEv.EventGo: hud.Banner(e.text, -1, 1.6f); break;
                    case SwEv.Collector: hud.Banner(e.text, -2, 3f); CollectorShip(); OrbitSfx.Play("warn", 1f); break;
                    case SwEv.RunEnd: Save(); hud.OnRunEnd(); break;
                    case SwEv.Overdue: OrbitSfx.Play("warn", 1f); hud.RadioOverdue(sim.S.bill); break;
                    case SwEv.Act: hud.ShowAct((int)e.v); flash = Mathf.Max(flash, 0.8f); shake = Mathf.Max(shake, 0.25f); OrbitSfx.Play("ending", 1f); OrbitSfx.Play("launch", 0.8f); Save(); break;
                    case SwEv.BillPaid: hud.OnBillPaid(e.text, (int)e.v); hud.RadioPaid((int)e.v); OrbitSfx.Play("unit", 1f); OrbitSfx.Play("buy", 1f, 0.01f); Save(); break;
                    case SwEv.Bankrupt: OrbitSfx.Play("lock", 1f); hud.RadioBankrupt(); Save(); break;
                    case SwEv.News: hud.OnNews(e.text, e.k == 1); break;
                    case SwEv.Won: OrbitSfx.Play("ending", 1f); Save(); hud.OnWon(); break;
                }
            }
        }

        // ⚡ 지그재그 번개 — a→b 를 여러 마디로 (빔 둘레를 감거나 튄다). 하얀 심지 + 색 테두리
        void Zap(Vector3 a, Vector3 b, Color c, float life, float jit, int seg = 7)
        {
            if (fx.Count > 820) return;
            var prev = a; var d = b - a; var nrm = new Vector3(-d.y, d.x).normalized;
            for (int i = 1; i <= seg; i++)
            {
                float u = (float)i / seg;
                var q = i == seg ? b : a + d * u + nrm * Random.Range(-jit, jit);
                var h = Add(pixel, prev, 0.05f, new Color(c.r, c.g, c.b, 0.45f), 8, life); h.a = prev; h.b = q; h.size = 0.12f;
                var k = Add(pixel, prev, 0.05f, new Color(1f, 1f, 1f, 0.95f), 8, life * 0.8f); k.a = prev; k.b = q; k.size = 0.035f;
                prev = q;
            }
        }
        // ✦ 별빛살 — 맞는 자리에서 사방으로 가는 선
        void Star(Vector3 at, Color c, float r, int n, float life = 0.18f)
        {
            if (fx.Count > 820) return;
            float a0 = Random.value * 6.283f;
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * 6.283f / n + Random.Range(-0.15f, 0.15f), L = r * Random.Range(0.55f, 1f);
                var e = at + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * L;
                var ln = Add(pixel, at, 0.05f, new Color(c.r, c.g, c.b, 0.9f), 8, life); ln.a = at; ln.b = e; ln.size = 0.05f;
            }
            Add(glow, at, r * 0.9f, new Color(1f, 0.97f, 0.9f, 0.85f), 7, life * 0.8f);
        }
        // 💥 불덩이 — 한 프레임 3개까지 (연쇄 폭발이 쏟아져도 화면이 안 덮이게)
        int fireballsThisFrame;
        void Fireball(Vector3 at, float R)
        {
            if (fireballsThisFrame >= 3 || fx.Count > 760) { Add(glow, at, R * 1.6f, new Color(1f, 0.6f, 0.3f, 0.22f), 7, 0.2f); return; }
            fireballsThisFrame++;
            LoadAnims();
            if (animExplode != null)
            {   // 💥 픽셀랩 폭발 9장 — 한 번 재생
                var sr = Make(animExplode[0], at, R * 2.3f, Color.white, 58); sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 4) * 90);
                frameFx.Add(new FrameFx { sr = sr, f = animExplode, fps = 20 });
            }
            else
            {
                Add(glow, at, R * 2.6f, new Color(1f, 0.42f, 0.12f, 0.55f), 7, 0.42f);
                Add(glow, at, R * 1.5f, new Color(1f, 0.78f, 0.35f, 0.85f), 7, 0.28f);
                Add(glow, at, R * 0.7f, new Color(1f, 1f, 0.95f, 1f), 7, 0.16f);
            }
            Star(at, new Color(1f, 0.8f, 0.4f), R * 1.4f, 9, 0.24f);
            for (int i = 0; i < 6; i++) Add(pixel, at, Random.Range(0.08f, 0.14f), new Color(1f, Random.Range(0.5f, 0.85f), 0.25f), 0, Random.Range(0.4f, 0.7f)).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(2f, 5f) * R);
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
            var muzzle = sim.R != null && !sim.R.over ? ShotFrom() : new Vector3(0, camBase - cam.orthographicSize - 0.4f, 0);   // 청소선에서 나간다 (09-24)
            var core = Add(pixel, at, 0.05f, hit ? new Color(1f, 0.95f, 0.78f, 1f) : new Color(0.6f, 0.65f, 0.75f, 0.5f), 8, 0.22f);
            core.a = muzzle; core.b = at; core.size = hit ? 0.1f : 0.05f;
            var halo = Add(pixel, at, 0.05f, new Color(1f, 0.76f, 0.3f, hit ? 0.55f : 0.22f), 8, 0.3f);
            halo.a = muzzle; halo.b = at; halo.size = hit ? 0.3f : 0.14f;
            if (hit) { Add(glow, at, 0.5f, new Color(1f, 0.87f, 0.58f, 0.6f), 7, 0.18f); Zap(muzzle, at, new Color(1f, 0.8f, 0.4f), 0.14f, 0.16f, 9); Star(at, new Color(1f, 0.85f, 0.5f), 0.45f, 6, 0.14f); }   // ⚡ 빔 둘레 번개 · ✦ 맞는 자리 빛살
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

        // 💰 값 숫자 — 가까이(45px) · 막(0.35초) 뜬 같은 색 숫자가 있으면 거기에 더한다 (09-24: +257 수십 개가 뭉쳐 글자 덩어리가 됐다)
        void CoinPop(double x, double y, string pre, double v, Color c)
        {
            var at = new Vector2((float)x, (float)y);
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var q = pops[i];
                if (q.val <= 0 || q.age > 0.35f || q.c != c || (q.px - at).sqrMagnitude > 45 * 45) continue;
                q.val += v; q.text = pre + KNum.Fmt(q.val); q.size = 14 + Mathf.Min(12, Mathf.Log10((float)q.val + 1) * 2); q.age = Mathf.Min(q.age, 0.2f);
                return;
            }
            PopAt(x, y, pre + KNum.Fmt(v), c, 14 + Mathf.Min(10, Mathf.Log10((float)v + 1) * 2));
            pops[pops.Count - 1].val = v;
        }

        public void PopAt(double x, double y, string text, Color c, float size)
        {
            var at = new Vector2((float)x + Random.Range(-6f, 6f), (float)y);
            for (int i = pops.Count - 1; i >= 0; i--)
            {   // 같은 말이 막 떠 있으면 합친다 — 「치명타! ×3」
                var q = pops[i]; if (q.val > 0 || q.baseText != text || q.age > 0.6f || (q.px - at).sqrMagnitude > 140 * 140) continue;
                q.n++; q.text = text + " ×" + q.n; q.age = Mathf.Min(q.age, 0.15f); return;
            }
            while (pops.Count >= 6) pops.RemoveAt(0);                          // 한 번에 여섯까지 (정돈 2)
            pops.Add(new Pop { px = at, text = text, baseText = text, c = c, size = size });
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
                case Att.Gold: return new Color(1f, 0.84f, 0.2f);
                case Att.Rock: return new Color(0.8f, 0.55f, 1f);
                default: return new Color(0.56f, 0.63f, 0.72f);
            }
        }

        // ───────────────────────────────── 궤도 · 지구 (궤도마다 카메라가 물러난다 §1-5)

        // 🪐 행성마다 크기 · 대기 빛 · 띠 색 (지구 · 달 · 화성 · 목성 · 토성)
        static readonly float[] PlanetR = { 118, 80, 96, 150, 104, 64, 130, 125, 58 };   // 5 소행성대(세레스) · 6 천왕성 · 7 해왕성 · 8 카이퍼(명왕성)
        static readonly Color[] AtmoCol = { new Color(0.35f, 0.6f, 1f, 0.35f), new Color(0.8f, 0.8f, 0.85f, 0.06f), new Color(1f, 0.5f, 0.35f, 0.18f), new Color(1f, 0.8f, 0.55f, 0.2f), new Color(1f, 0.9f, 0.6f, 0.16f) , new Color(0.7f, 0.65f, 0.6f, 0.05f), new Color(0.55f, 0.9f, 0.95f, 0.3f), new Color(0.3f, 0.45f, 1f, 0.35f), new Color(0.85f, 0.8f, 0.75f, 0.05f) };
        static readonly Color[] BandCol = { new Color(0.43f, 0.55f, 0.78f), new Color(0.6f, 0.6f, 0.66f), new Color(0.8f, 0.45f, 0.35f), new Color(0.8f, 0.62f, 0.42f), new Color(0.85f, 0.75f, 0.5f) , new Color(0.62f, 0.55f, 0.48f), new Color(0.5f, 0.78f, 0.82f), new Color(0.38f, 0.5f, 0.9f), new Color(0.7f, 0.66f, 0.62f) };
        int shownPlanet = -1;

        void DrawWorld()
        {
            int pi = sim.S.orbit;
            var o = SweepSim.Orbits[pi];
            if (pi != shownPlanet) { shownPlanet = pi; earth.sprite = PlanetArt.Get(pi); }
            var pf = PlanetFrames(pi);                                                   // 🪐 픽셀랩 회전 행성 (09-24) — 없으면 코드 그림
            if (pf != null) earth.sprite = pf[(int)(Time.time * 3f) % pf.Length];
            earthR = Mathf.Lerp(earthR, PlanetR[pi], 1 - Mathf.Exp(-Time.deltaTime * 2.5f));
            float d = earthR * 2 / PxPerUnit;
            earth.transform.localScale = Vector3.one * d / earth.sprite.bounds.size.x;
            earth.transform.rotation = Quaternion.Euler(0, 0, pi == 3 || pi == 4 ? 0 : -8f);
            atmo.color = AtmoCol[pi];
            atmo.transform.localScale = Vector3.one * d * 1.45f * (1f + 0.02f * Mathf.Sin(t)) / glow.bounds.size.x;
            rim.transform.localScale = Vector3.one * (d + 0.12f) / ring.bounds.size.x;
            rim.color = new Color(1f, 0.87f, 0.58f, 0);                                  // 행성 둘레 노란 고리 — 없앰 (09-24)
            // 토성 — 고리가 행성을 감싼다 (띠 안쪽까지만)
            bool saturn = pi == 4;
            ringB.enabled = ringF.enabled = saturn;
            if (saturn)
            {
                float rw = (float)o.bi * 0.95f * 2 / PxPerUnit;
                var sc = new Vector3(rw / ringB.sprite.bounds.size.x, rw * 0.3f / ringB.sprite.bounds.size.y, 1);
                ringB.transform.localScale = ringF.transform.localScale = sc;
                ringB.transform.rotation = ringF.transform.rotation = Quaternion.Euler(0, 0, -6f);
            }
            // 멀리 — 지구에선 달이, 화성 너머로는 작은 해가 보인다
            moon.enabled = pi == 0;
            if (pi == 0 && moon.sprite != PlanetArt.Get(1)) { moon.sprite = PlanetArt.Get(1); moon.color = Color.white; moon.transform.localScale = Vector3.one * 0.6f / moon.sprite.bounds.size.x; }
            bool geo = pi >= 2;
            sun.enabled = sunCore.enabled = geo;
            if (geo)
            {
                float far = pi == 2 ? 1f : pi == 5 ? 0.8f : pi == 3 ? 0.6f : pi == 4 ? 0.42f : pi == 6 ? 0.3f : pi == 7 ? 0.22f : 0.15f;   // 멀수록 해가 작다
                float pulse = (1f + 0.03f * Mathf.Sin(t * 0.8f)) * far;
                sun.transform.localScale = Vector3.one * 7.5f * pulse / glow.bounds.size.x;
                sunCore.transform.localScale = Vector3.one * 0.5f * pulse / disc.bounds.size.x;
            }
            float inner = (float)(o.bi / sim.Bo);
            if (Mathf.Abs(inner - bandInner) > 0.001f) { bandInner = inner; bandSprite = Ring(256, inner); band.sprite = bandSprite; }
            float outer = (float)sim.Bo * 2 / PxPerUnit;
            band.transform.localScale = new Vector3(outer / bandSprite.bounds.size.x, outer * (float)SweepSim.Tilt / bandSprite.bounds.size.y, 1);
            Color bc = BandCol[pi];
            bc.a = 0.018f + bandLit * 0.02f; band.color = bc;                               // 띠 채움은 아주 옅게 — 궤도선이 주인공
            bandGlow.transform.localScale = new Vector3(outer * 1.004f / thinRing.bounds.size.x, outer * 1.004f * (float)SweepSim.Tilt / thinRing.bounds.size.y, 1);
            bandGlow.color = new Color(1f, 0.85f, 0.5f, bandLit * 0.8f + edgeGlow * 0.3f);   // 케슬러 — 바깥 궤도선이 빛난다
            float tilt = (float)SweepSim.Tilt;
            for (int k = 0; k < 5; k++)
            {
                float rk = Mathf.Lerp(inner, 1f, k / 4f), w = outer * rk; bool edgeK = k == 0 || k == 4;
                tracks[k].transform.localScale = new Vector3(w / thinRing.bounds.size.x, w * tilt / thinRing.bounds.size.y, 1);
                var tc = Color.Lerp(bc, Color.white, 0.45f); tc.a = (edgeK ? 0.34f : 0.12f) + bandLit * 0.15f; tracks[k].color = tc;
            }
            // 빛점 — 안쪽 궤도일수록 빨리 돈다 (케플러 느낌). 뒤쪽 반은 행성 뒤로
            for (int i = 0; i < trackDots.Length; i++)
            {
                int k = i % 5; float rk = Mathf.Lerp(inner, 1f, k / 4f), R = outer * 0.5f * rk;
                float a = dotPhase[i] + t * 0.22f / Mathf.Pow(rk, 1.5f) * (float)o.spin;
                var dv = trackDots[i]; dv.transform.position = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R * tilt, 0);
                dv.sortingOrder = Mathf.Sin(a) < 0 ? 3 : 6;
                var dc = Color.Lerp(bc, Color.white, 0.7f); dc.a = 0.55f + 0.25f * Mathf.Sin(t * 3 + i); dv.color = dc;
                dv.transform.localScale = Vector3.one * (k == 0 || k == 4 ? 0.2f : 0.14f) / glow.bounds.size.x;
            }
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
                var px = d.sp >= 0 ? SpeciesPx(d.sp) : null; if (px == null) px = JunkPx(d.k, d.id);   // 종 그림 먼저                                                // 🛰 픽셀랩 쓰레기 그림이 있으면 그걸로
                if (px != null) { s = px; if (d.k == SweepSim.Vault) c = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.75f), 0.5f + 0.5f * Mathf.Sin(t * 6)); }
                else switch (d.k)
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
                if (d.frz > 0) c = Color.Lerp(c, Ice, 0.75f);                      // 냉동 빔 — 언 것
                if (d.hit > 0) c = Color.white;
                c.a = (float)d.fade;
                v.color = c;
                float r = (float)SweepSim.Types[d.k].r;
                float size = r * 2.6f / PxPerUnit * (d.hit > 0 ? 1.25f : 1f) * (px != null ? 1.3f : d.k == SweepSim.Fuel ? 0.6f : 1f);   // 그림은 둘레가 비어 있어 1.3배
                v.transform.localScale = new Vector3(size / Mathf.Max(0.01f, s.bounds.size.x), size / Mathf.Max(0.01f, s.bounds.size.x) * (px == null && d.k == SweepSim.Fuel ? 1.6f : 1f), 1);
                v.transform.rotation = Quaternion.Euler(0, 0, (float)d.rot * Mathf.Rad2Deg);
                int order = 10 + Mathf.Clamp((int)(d.y / 6), 0, 99);
                bool behind = d.y < SweepSim.EY && (d.x - SweepSim.EX) * (d.x - SweepSim.EX) + (d.y - SweepSim.EY) * (d.y - SweepSim.EY) < earthR * earthR;
                v.sortingOrder = behind ? 3 : order;      // 지구 뒤로 지나가는 것
                // 부착물 — 가장자리에 붙어 함께 돈다 (§3-2)
                if (d.att == Att.None || d.att == Att.Cable) { av.enabled = false; continue; }
                av.enabled = true;
                Color ac = AttColor(d.att); ac.a = (float)d.fade;
                if (d.att == Att.Ice || d.att == Att.Armor)
                {   // 원 대신 쓰레기 색 — 얼음 = 하늘빛 · 장갑 = 쇳빛 (09-24)
                    av.enabled = false;
                    var vc = v.color; v.color = Color.Lerp(vc, d.att == Att.Ice ? new Color(0.62f, 0.88f, 1f, vc.a) : new Color(0.55f, 0.6f, 0.7f, vc.a), 0.55f);
                    continue;
                }
                else
                {
                    var ap = AttPx(d.att);                                                   // 🧷 픽셀랩 부착물 그림
                    av.sprite = ap != null ? ap : d.att == Att.BBox || d.att == Att.Tag || d.att == Att.Det ? square : disc;
                    if (ap != null) ac = new Color(1f, 1f, 1f, (float)d.fade);
                    if ((d.att == Att.Det || d.att == Att.Beacon) && Mathf.Sin(t * 8 + d.id) < 0) ac = Color.Lerp(ac, new Color(0.35f, 0.35f, 0.4f, ac.a), 0.6f);
                    float ang = (float)d.rot + 0.9f;
                    av.transform.position = pos + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * size * 0.62f;
                    float asz = Mathf.Max(0.09f, size * (ap != null ? 0.62f : d.att == Att.Tag ? 0.55f : 0.42f));
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
            if (dronePx == null) dronePx = Resources.Load<Sprite>("ship/drone");
            while (droneViews.Count < drs.Count) droneViews.Add(dronePx != null ? Make(dronePx, Vector3.zero, 0.42f, Color.white, 60) : Make(droneArt, Vector3.zero, 0.32f, Cyan, 60));   // 🤖 픽셀랩 드론
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
            claw.enabled = show; clawRing.enabled = show && sim.Weapon == 0;       // 원 = 집게 빔의 범위 (다른 무기엔 없다)
            shipView.enabled = shipFlame.enabled = false;                 // 작은 배 없앰 — 조종실 포구에서 쏜다 (09-24)
            for (int i = 0; i < mineViews.Count || i < R.mines.Count; i++)
            {
                if (i >= mineViews.Count) mineViews.Add(Make(disc, Vector3.zero, 0.22f, Red, 64));
                var mv = mineViews[i]; bool on = !R.over && i < R.mines.Count; mv.enabled = on; if (!on) continue;
                var m = R.mines[i]; mv.transform.position = PxToWorld(m.x, m.y);
                float bl = m.t > 0 ? 0.35f : 0.6f + 0.4f * Mathf.Sin(Time.time * 10 + i);
                mv.color = new Color(1f, 0.3f, 0.25f, bl); mv.transform.localScale = Vector3.one * 0.22f / disc.bounds.size.x;
            }
            DrawTurret(!R.over);
            clawWind.enabled = show && R.fuel > 0;
            holeCore.enabled = holeGlow.enabled = holding; holeRing.enabled = false;         // 범위 고리 없앰 — 도트 블랙홀이 보여 준다
            LoadAnims();
            if (animHole != null)
            {   // 🕳 픽셀랩 블랙홀 — 열려 있는 동안 돈다
                if (holeAnim == null) holeAnim = Make(animHole[0], Vector3.zero, 1f, Color.white, 66);
                holeAnim.enabled = holding;
                if (holding)
                {
                    holeAnim.sprite = animHole[(int)(Time.time * 10) % animHole.Length];
                    holeAnim.transform.position = holeCore.transform.position;
                    holeAnim.transform.localScale = Vector3.one * holeCore.transform.lossyScale.x * disc.bounds.size.x * 2.5f / animHole[0].bounds.size.x; holeAnim.color = new Color(1, 1, 1, 0.8f);   // 3.4 → 2.5 (정돈 3)
                }
            }
            Cursor.visible = true;                                         // 마우스는 늘 보인다 — 판 중 · AUTO 여도 (사장님 09-24)
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
            clawRing.enabled = false; clawWind.enabled = false;                       // ⭕ 조준 원 → 🎯 도트 조준경 (09-24 사장님 「조준한다는 느낌 · 범위 표시도 어울리게」)
            if (retSpr == null) retSpr = Resources.Load<Sprite>("ship/reticle");
            if (reticleView == null && retSpr != null) reticleView = Make(retSpr, at, 1f, Color.white, 130);
            if (reticleView != null)
            {
                reticleView.enabled = claw.enabled;
                if (wind < lastWind - 0.3f) retKick = 1f;                                  // 방금 쐈다 — 꺾쇠가 튕겨 나간다
                lastWind = wind; retKick = Mathf.MoveTowards(retKick, 0, Time.deltaTime * 6f);
                float sc = r * 0.9f * (1f - 0.16f * wind + 0.14f * retKick);                   // 1.2 → 0.9 (09-26 정돈 4: 꺾쇠가 잔해 여러 개를 덮었다)                   // 장전될수록 조여들고 · 쏘면 벌어진다
                reticleView.transform.position = at; reticleView.transform.rotation = Quaternion.identity;
                reticleView.transform.localScale = Vector3.one * sc / retSpr.bounds.size.x;
                reticleView.color = fuel ? new Color(1f, 1f, 1f, auto ? 0.45f + 0.4f * wind : 0.85f) : new Color(0.45f, 0.48f, 0.55f, 0.6f);
                claw.enabled = false;                                                      // 가운데 도는 표시는 조준경 가운데 점이 대신한다
            }
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

        // 🧹 고리 예산 — 화면에 6개까지, 옅게 · 짧게 (09-24 사장님: 「무기들이 너무 다 지저분해」)
        int ringsAlive;
        void RingFx(Vector3 at, Color c, float life, float grow)
        {
            return;                                                                    // ⭕ 고리 연출 없앰 (09-24) — 폭발 · 서리 · 자석은 도트 애니가 맡는다
#pragma warning disable CS0162
            if (ringsAlive >= 6) return;
            ringsAlive++; c.a *= 0.55f;
            Add(ring, at, 0.1f, c, 5, life * 0.7f, grow);
        }

        // ───────────────────────────────── 🔫 조종실 포구 (시안 좌표 1280×720 → 월드)
        readonly List<SpriteRenderer> turPool = new List<SpriteRenderer>(); int turN, turFire;
        readonly float[] recoil = new float[12]; Vector3 turAim; int turW = -1;
        float TK => 2f * cam.orthographicSize / 720f * (float)SweepSim.TurS;          // 시안 1px → 월드 (포구 크기 배율 포함)
        Vector3 TW(float mx, float my) => new Vector3(cam.transform.position.x + (mx - 640) * TK, camBase - cam.orthographicSize + (720 - my) * TK, 0);
        SpriteRenderer TPiece(Sprite s, Color c, int order)
        {
            if (turN >= turPool.Count) { var go = new GameObject("tur"); go.transform.SetParent(transform); turPool.Add(go.AddComponent<SpriteRenderer>()); }
            var sr = turPool[turN++]; sr.enabled = true; sr.sprite = s; sr.color = c; sr.sortingOrder = 140 + order; return sr;   // 쓰레기(~110)보다 앞
        }
        void TBox(Vector3 c, float w, float h, float ang, Color col, int order = 0)     // w · h = 시안 px
        {
            var sr = TPiece(square, col, order); sr.transform.position = c; sr.transform.rotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(w * TK / square.bounds.size.x, h * TK / square.bounds.size.y, 1);
        }
        static Sprite hullSpr, turClawSpr, dronePx;
        static readonly Sprite[][] planetFrames = new Sprite[9][]; static readonly bool[] planetTried = new bool[9];
        static Sprite[] PlanetFrames(int pi)
        {
            if (pi < 0 || pi >= 9) return null;
            if (!planetTried[pi]) { planetTried[pi] = true; var f = new System.Collections.Generic.List<Sprite>(); for (int i = 0; i < 8; i++) { var sp = Resources.Load<Sprite>("planet_anim/p" + pi + "_" + i); if (sp != null) f.Add(sp); } planetFrames[pi] = f.Count > 0 ? f.ToArray() : null; }
            return planetFrames[pi];
        }
        // 🎞 픽셀랩 애니메이션 (09-24) — 폭발 9장 · 소용돌이 9장 · 블랙홀 6장(튀는 3장 뺌)
        static Sprite[] animExplode, animVortex, animHole, animFrost, animBurn, animMag;   // 서리 · 태우는 점 · 자석 (09-24 「공격 원 모양 다듬기」)
        static Sprite[] LoadAnim(string n, int[] idx) { var a = new Sprite[idx.Length]; for (int i = 0; i < idx.Length; i++) a[i] = Resources.Load<Sprite>("anim/" + n + "_" + idx[i]); return a[0] != null ? a : null; }
        void LoadAnims()
        {
            if (animExplode != null && animExplode[0] != null) return;
            animExplode = LoadAnim("explode", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
            animVortex = LoadAnim("vortex", new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            animHole = LoadAnim("blackhole", new[] { 0, 1, 2, 6, 7, 8 });
            animFrost = LoadAnim("frost", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
            animBurn = LoadAnim("burn", new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            animMag = LoadAnim("magnet", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        }
        class FrameFx { public SpriteRenderer sr; public Sprite[] f; public float t, fps; }
        readonly List<FrameFx> frameFx = new List<FrameFx>();
        SpriteRenderer reticleView; static Sprite retSpr; float lastWind, retKick;
        SpriteRenderer vacView, holeAnim, burnView; float vacT, burnT, lastFrost;
        static readonly Sprite[] attPx = new Sprite[13]; static bool attTried;
        static Sprite AttPx(Att a)
        {
            if (!attTried)
            {
                attTried = true;
                foreach (var (k, n) in new[] { (Att.FuelPod, "fuelpod"), (Att.Pouch, "pouch"), (Att.Beacon, "beacon"), (Att.Magnet, "magnet"), (Att.Det, "det"), (Att.BBox, "bbox"), (Att.Tag, "tag"), (Att.Gold, "gold"), (Att.Rock, "rock") })
                    attPx[(int)k] = Resources.Load<Sprite>("att/att_" + n);
            }
            int i = (int)a; return i >= 0 && i < attPx.Length ? attPx[i] : null;
        }
        static Sprite[] junkPx; static Sprite[] chipPx;
        static readonly Sprite[] spcPx = new Sprite[SweepSim.Spc.Length]; static readonly bool[] spcTried = new bool[SweepSim.Spc.Length];
        static Sprite SpeciesPx(int sp)
        {
            if (sp < 0 || sp >= spcPx.Length) return null;
            if (!spcTried[sp]) { spcTried[sp] = true; spcPx[sp] = Resources.Load<Sprite>("junk/" + SweepSim.Spc[sp].art); }
            return spcPx[sp];
        }
        static Sprite JunkPx(int k, int id)
        {
            if (junkPx == null)
            {
                string[] n = { null, "sat", "rocket", "vault", "fuel", "tank", "big" };
                junkPx = new Sprite[n.Length]; for (int i = 1; i < n.Length; i++) junkPx[i] = Resources.Load<Sprite>("junk/junk_" + n[i]);
                chipPx = new[] { Resources.Load<Sprite>("junk/junk_chip_a"), Resources.Load<Sprite>("junk/junk_chip_b"), Resources.Load<Sprite>("junk/junk_chip_c") };
            }
            if (k == SweepSim.Chip) { var c = chipPx[(id & 0x7fffffff) % 3]; return c; }
            return k > 0 && k < junkPx.Length ? junkPx[k] : null;
        }
        static readonly string[] TurName = { "claw", "laser", "bolt", "vac", "mine", "frz", "clus", "mag", "rail" };
        static readonly float[] TurW = { 44, 56, 56, 40, 22, 48, 90, 50, 60 };     // 시안 px 가로 — 포신 끝이 sim 포구 길이에 오도록
        static readonly Sprite[] turSpr = new Sprite[9]; static readonly bool[] turTried = new bool[9];
        static Sprite TurSprite(int w) { if (w < 0 || w > 8) return null; if (!turTried[w]) { turTried[w] = true; turSpr[w] = Resources.Load<Sprite>("ship/turret_" + TurName[w]); } return turSpr[w]; }
        void TSprite(Sprite s, Vector3 c, float wMock, float ang, int order)            // 그림 조각 — 가로 wMock(시안 px), ang 라디안
        {
            var sr = TPiece(s, Color.white, order); sr.transform.position = c; sr.transform.rotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
            sr.transform.localScale = Vector3.one * wMock * TK / s.bounds.size.x;
        }
        void TDisc(Vector3 c, float r, Color col, int order = 0, Sprite s = null, float sy = 1)
        {
            s = s ?? disc; var sr = TPiece(s, col, order); sr.transform.position = c; sr.transform.rotation = Quaternion.identity;
            sr.transform.localScale = new Vector3(r * 2 * TK / s.bounds.size.x, r * 2 * TK * sy / s.bounds.size.y, 1);
        }
        // 포신 — 받침(base)에서 조준 쪽으로 len, 굵기 w. along = 받침에서 거리(시안 px)
        Vector3 TAlong(Vector3 b, float ang, float along, float side = 0) => b + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * along * TK + new Vector3(-Mathf.Sin(ang), Mathf.Cos(ang)) * side * TK;
        void TBarrel(Vector3 b, float ang, float len, float w, Color tip, float rc, int order = 1)
        {
            float L = len - rc * 7;
            TBox(TAlong(b, ang, L / 2), L, w, ang, Metal, order); TBox(TAlong(b, ang, L / 2), L, 1.5f, ang, Metal2, order + 1);
            TBox(TAlong(b, ang, L - 2), 4, w, ang, tip, order + 2);
        }
        static readonly Color Metal = new Color(0.165f, 0.2f, 0.25f), Metal2 = new Color(0.23f, 0.27f, 0.34f), Dark = new Color(0.086f, 0.11f, 0.145f);
        Vector3 ShotFrom()                                                             // 다음 포구 끝 (월드) · 반동
        {
            if (curW > 0 && sim.WeaponOwned(curW))
            {   // 무기 포대 포구
                var wb = WTurretPos(curW); float wa = WTurretAng(curW, wb); wRec[curW] = 1;
                var wp = TAlong(wb, wa, TurW[curW] * 0.8f); Add(glow, wp, 0.9f * cam.orthographicSize / 6f, WeaponCol(curW), 7, 0.1f).sr.sortingOrder = 150; return wp;
            }
            int w = sim.Weapon, n = sim.MountCount(w); turFire = (turFire + 1) % n; recoil[turFire] = 1;
            sim.MountPx(w, turFire, out _, out _, out var tx, out var ty);
            var p = PxToWorld(tx, ty); Add(glow, p, 0.9f * cam.orthographicSize / 6f, WeaponCol(w), 7, 0.1f).sr.sortingOrder = 150; return p;
        }
        public static Color WeaponCol(int w) => w switch { 1 => new Color(1f, 0.3f, 0.37f), 2 => new Color(0.62f, 0.85f, 1f), 3 => new Color(0.37f, 0.9f, 0.78f), 4 => new Color(1f, 0.6f, 0.24f), 5 => new Color(0.75f, 0.94f, 1f), 6 => new Color(1f, 0.82f, 0.4f), 7 => new Color(0.77f, 0.61f, 1f), 8 => Color.white, _ => new Color(1f, 0.76f, 0.35f) };
        void DrawTurret(bool on)
        {
            turN = 0;
            if (on)
            {
                int w = sim.Weapon; if (w != turW) { turW = w; turFire = 0; }
                float dt = Time.deltaTime; for (int i = 0; i < recoil.Length; i++) recoil[i] = Mathf.Max(0, recoil[i] - dt * 6);
                var aimW = PxToWorld(sim.R.ax, sim.R.ay); turAim = Vector3.Lerp(turAim, aimW, 1 - Mathf.Exp(-dt * 10));
                var M = SweepSim.Mounts[w]; int n = M.Length / 3; Color c = WeaponCol(w); float t = Time.time, pul = 0.5f + 0.5f * Mathf.Sin(t * 6);
                Vector3 B(int i) => TW((float)M[i * 3], (float)M[i * 3 + 1]);
                float A(int i) { var d = turAim - B(i); return Mathf.Atan2(d.y, d.x); }
                var plateC = Dark; var edge = new Color(0.17f, 0.2f, 0.26f);
                // 조종실 계기판 — 화면 아래 (가운데가 살짝 솟은 곡선)
                var panel = new Color(0.047f, 0.063f, 0.086f);
                const float q = 1f / (float)SweepSim.TurS;                                   // 계기판은 포구 배율과 상관없이 같은 크기
                // 🚀 창밖 — 청소선 선체 (픽셀랩 그림). 포대 받침이 포구 자리에 오게
                // 선체는 뺐다 (09-24 사장님 「화면에서 쏘는 걸 보는 거지 우주선이 있을 필요가 없다」) — 포대는 창턱 위에
                TBox(TW(640, 720 - 34 * q), 1800 * q, 68 * q, 0, panel, -12); TBox(TW(640, 720 - 68 * q), 1800 * q, 2 * q, 0, edge, -11);
                if (consoleSpr == null && !consoleTried) { consoleTried = true; consoleSpr = Resources.Load<Sprite>("ship/console"); mountSpr = Resources.Load<Sprite>("ship/mount"); }
                if (consoleSpr != null)
                {   // 🛠 도트 계기판 — 창턱을 따라 이어 붙인다 (09-24 사장님 10 · 28번 「우주선에서 공격하는 느낌」)
                    float tw = 68 * q * consoleSpr.bounds.size.x / consoleSpr.bounds.size.y;
                    for (int k = -4; k <= 4; k++) TSprite(consoleSpr, TW(640 + k * tw, 720 - 34 * q), tw, 0, -11);
                }
                if (mountSpr != null) TSprite(mountSpr, TW((float)M[0], (float)M[1]) + new Vector3(0, -12 * TK, 0), 64, 0, -1);
                var ts = TurSprite(w);
                if (ts != null)                                                            // 🔫 픽셀랩 포대 그림 (위를 보는 그림 → -90°)
                {
                    const float up = Mathf.PI / 2;
                    if (w == 2) { TSprite(ts, B(0), TurW[2], 0, 2); TDisc(B(0), 26 + 6 * pul, new Color(c.r, c.g, c.b, 0.55f), 4, glow); }
                    else if (w == 6)
                    {
                        var pc = TW(651, 641); var d6 = turAim - pc; TSprite(ts, pc, TurW[6], Mathf.Atan2(d6.y, d6.x) - up, 2);
                        for (int i = 0; i < n; i++) if (recoil[i] > 0) TDisc(B(i), 14, new Color(c.r, c.g, c.b, recoil[i]), 4, glow);
                    }
                    else for (int i = 0; i < n; i++) TSprite(ts, TAlong(B(i), A(i), -recoil[i] * 6), TurW[w], A(i) - up, 2);
                    if (w == 8) { float ch = Mathf.Clamp01(1f - (float)sim.R.next / 1.2f); if (ch > 0.5f) TDisc(TAlong(B(0), A(0), 108), 10 + 24 * ch, new Color(0.75f, 0.9f, 1f, ch * 0.8f), 4, glow); }
                }
                else switch (w)
                {
                    case 0: { var b = B(0); float a = A(0);
                        if (turClawSpr == null) turClawSpr = Resources.Load<Sprite>("ship/turret_claw");
                        if (turClawSpr != null) { TSprite(turClawSpr, TAlong(b, a, -recoil[0] * 6), 44, a - Mathf.PI / 2, 2); break; }   // 🔫 픽셀랩 포대 — 위를 보는 그림이라 -90°
                        TDisc(b, 28, new Color(0.106f, 0.133f, 0.176f), 0); TBarrel(b, a, 52, 16, c, recoil[0]); break; }
                    case 1: for (int i = 0; i < n; i++) { var b = B(i); TBox(TW((float)M[i * 3], 666), 56, 32, 0, edge, -2); TBox(TW((float)M[i * 3], 666), 52, 28, 0, plateC, -1); TDisc(b, 16, new Color(0.106f, 0.133f, 0.176f), 0);
                            float a = A(i); TBarrel(b, a, 74, 5, c, 0); TDisc(TAlong(b, a, 74), 9, new Color(c.r, c.g, c.b, 0.55f), 5, glow); } break;
                    case 2: { var b = B(0); TBox(TW(640, 668), 82, 28, 0, edge, -2); TBox(TW(640, 668), 78, 24, 0, plateC, -1); TBox(TW(640, 624), 18, 76, 0, Metal, 0);
                        for (int k = 0; k < 3; k++) { bool lit = (k + t * 3) % 3 < 1; TDisc(TW(640, 604 + k * 18), 22 - k * 2, new Color(c.r, c.g, c.b, lit ? 0.85f : 0.35f), 1, ring, 0.28f); }
                        TDisc(b, 11, new Color(0.81f, 0.91f, 1f), 3); TDisc(b, 28 + 6 * pul, new Color(c.r, c.g, c.b, 0.6f), 4, glow); break; }
                    case 3: { var b = B(0); TBox(TW(640, 670), 112, 32, 0, edge, -2); TBox(TW(640, 670), 108, 28, 0, plateC, -1); float a = A(0);
                        for (int k = 0; k < 5; k++) { float u = (k + 0.5f) / 5f; TBox(TAlong(b, a, 50 * u), 10.5f, 24 + 36 * u, a, k % 2 == 0 ? Metal : Metal2, 1); }
                        TBox(TAlong(b, a, 50), 3, 60, a, new Color(c.r, c.g, c.b, 0.6f + 0.4f * pul), 3); break; }
                    case 4: { TBox(TW(640, 652), 98, 58, 0, edge, -2); TBox(TW(640, 652), 94, 54, 0, plateC, -1);
                        for (int i = 0; i < n; i++) { var b = B(i); TBarrel(b, A(i), 24, 13, c, recoil[i]); TDisc(b, 8, new Color(0.106f, 0.133f, 0.176f), 3); } break; }
                    case 5: { var b = B(0); TBox(TW(640, 666), 152, 36, 0, edge, -2); TBox(TW(640, 666), 148, 32, 0, plateC, -1);
                        foreach (float dx in new[] { -52f, 52f }) { TBox(TW(640 + dx, 658), 28, 40, 0, new Color(0.114f, 0.165f, 0.2f), 0); TBox(TW(640 + dx, 648), 20, 4, 0, new Color(c.r, c.g, c.b, 0.35f + 0.3f * pul), 1); TBox(TW(640 + dx, 658), 20, 4, 0, new Color(c.r, c.g, c.b, 0.35f + 0.3f * pul), 1); }
                        TDisc(b, 24, new Color(0.106f, 0.133f, 0.176f), 0); float a = A(0); TBarrel(b, a, 58, 20, c, 0);
                        foreach (float f in new[] { 0.35f, 0.55f, 0.75f }) TBox(TAlong(b, a, 58 * f), 2, 28, a, new Color(c.r, c.g, c.b, 0.8f), 4); break; }
                    case 6: { TBox(TW(651, 645), 140, 64, 0, edge, -2); TBox(TW(651, 645), 136, 60, 0, plateC, -1); TBox(TW(651, 645), 126, 50, 0, Metal, 0);
                        for (int i = 0; i < n; i++) { var b = B(i); TDisc(b, 8, new Color(0.35f, 0.29f, 0.16f), 1); TDisc(b, 6, new Color(0.05f, 0.067f, 0.09f), 2); if (recoil[i] > 0) TDisc(b, 16, new Color(c.r, c.g, c.b, recoil[i]), 3, glow); } break; }
                    case 7: { var b = B(0); TBox(TW(640, 668), 102, 34, 0, edge, -2); TBox(TW(640, 668), 98, 30, 0, plateC, -1); float a = A(0);
                        TDisc(TAlong(b, a, 12), 20, Metal, 0); TDisc(TAlong(b, a, 12), 8, plateC, 1);
                        for (int s = -1; s <= 1; s += 2) { TBox(TAlong(b, a, 36, s * 14), 48, 12, a, Metal, 1); TBox(TAlong(b, a, 56, s * 14), 8, 12, a, s < 0 ? new Color(0.44f, 0.66f, 1f) : new Color(1f, 0.42f, 0.48f), 2); }
                        TDisc(TAlong(b, a, 60), 14 + 8 * pul, new Color(c.r, c.g, c.b, 0.5f), 3, glow); break; }
                    case 8: { var b = B(0); TBox(TW(640, 668), 122, 32, 0, edge, -2); TBox(TW(640, 668), 118, 28, 0, plateC, -1); float a = A(0), L = 108 - recoil[0] * 10;
                        TBox(TAlong(b, a, L / 2, -9.5f), L, 5, a, Metal, 1); TBox(TAlong(b, a, L / 2, 9.5f), L, 5, a, Metal, 1); TBox(TAlong(b, a, 3), 26, 28, a, Metal2, 2);
                        float ch = Mathf.Clamp01(1f - (float)sim.R.next / 1.2f);
                        for (int k = 0; k < 6; k++) TBox(TAlong(b, a, 22 + k * 14), 6, 12, a, new Color(0.62f, 0.82f, 1f, 0.2f + 0.8f * (k < ch * 6 ? ch : 0.15f)), 3);
                        if (ch > 0.5f) TDisc(TAlong(b, a, L), 10 + 24 * ch, new Color(0.75f, 0.9f, 1f, ch * 0.8f), 4, glow); break; }
                }
                WeaponTurrets(dt);
            }
            for (int i = turN; i < turPool.Count; i++) turPool[i].enabled = false;
        }

        // 🔫 산 무기마다 창턱에 포대 하나 — 가운데 기본 빔 둘레로 좌우 번갈아 (09-24 사장님 전탄 B안 「여러 곳에서 쏘는 느낌」)
        static Sprite consoleSpr, mountSpr; static bool consoleTried;
        int curW;                                                                     // 지금 쏘는 무기 — 발동 신호가 먼저 와서 효과가 그 포대에서 나간다
        readonly Vector3[] wTgt = new Vector3[9]; readonly float[] wRec = new float[9];
        static readonly float[] WSlot = { -96, 96, -192, 192, -288, -384, -480, -576 };
        Vector3 WTurretPos(int w)
        {
            int k = 0; for (int i = 1; i < w; i++) if (sim.WeaponOwned(i)) k++;
            return TW(640 + WSlot[Mathf.Min(k, WSlot.Length - 1)], 668);          // 오른쪽은 두 칸까지 — 그 너머는 이번 판 · 주식 액정 자리
        }
        float WTurretAng(int w, Vector3 b) { var t = wTgt[w] == Vector3.zero ? turAim : wTgt[w]; var d = t - b; return Mathf.Atan2(d.y, d.x); }
        void WeaponTurrets(float dt)
        {
            const float up = Mathf.PI / 2;
            var edge = new Color(0.17f, 0.2f, 0.26f);
            for (int w = 1; w < 9; w++)
            {
                wRec[w] = Mathf.Max(0, wRec[w] - dt * 6);
                if (!sim.WeaponOwned(w)) continue;
                var b = WTurretPos(w); float a = WTurretAng(w, b); var ts = TurSprite(w);
                if (mountSpr != null) TSprite(mountSpr, b + new Vector3(0, -12 * TK, 0), 64, 0, -1);                         // 도트 받침
                else { TBox(b + new Vector3(0, -10 * TK, 0), 58, 14, 0, edge, -2); TBox(b + new Vector3(0, -10 * TK, 0), 54, 10, 0, Dark, -1); }
                if (ts != null) TSprite(ts, TAlong(b, a, -wRec[w] * 7), TurW[w] * 0.85f, a - up, 2);
                else { TDisc(b, 14, Dark, 0); TBarrel(b, a, 50, 10, WeaponCol(w), wRec[w]); }
                if (wRec[w] > 0) TDisc(TAlong(b, a, TurW[w] * 0.8f), 10 + 14 * wRec[w], new Color(WeaponCol(w).r, WeaponCol(w).g, WeaponCol(w).b, wRec[w] * 0.8f), 4, glow);
            }
        }

        void Burst(Vector3 at, Color c, int n, float speed)
        {
            for (int i = 0; i < n && fx.Count < 900; i++)
                Add(pixel, at, Random.Range(0.05f, 0.1f), Color.Lerp(c, Color.white, Random.value * 0.3f), 0, Random.Range(0.35f, 0.8f)).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(speed * 0.25f, speed));
        }

        void UpdateFx(float dt)
        {
            var aS = hud == null ? Vector2.zero : sim.R != null && !sim.R.over ? hud.TallyScreen : hud.CreditScreen;   // 출동 중엔 금화가 계산대로
            Vector3 anchor = hud != null ? ScreenToWorld(aS) : Vector3.zero;
            anchor.z = 0;
            ringsAlive = 0; fireballsThisFrame = 0; foreach (var q in fx) if (q.kind == 5) ringsAlive++;
            for (int i = frameFx.Count - 1; i >= 0; i--)
            {
                var ff = frameFx[i]; ff.t += dt; int fi = (int)(ff.t * ff.fps);
                if (fi >= ff.f.Length) { Destroy(ff.sr.gameObject); frameFx.RemoveAt(i); continue; }
                ff.sr.sprite = ff.f[fi];
            }
            if (burnView != null)
            {
                burnT -= dt; burnView.enabled = burnT > 0 && sim.R != null && !sim.R.over;
                if (burnView.enabled) burnView.sprite = animBurn[(int)(Time.time * 14) % animBurn.Length];
            }
            if (vacView != null)
            {
                vacT -= dt; vacView.enabled = vacT > 0 && sim.R != null && !sim.R.over;
                if (vacView.enabled) { vacView.sprite = animVortex[(int)(Time.time * 14) % animVortex.Length]; vacView.color = new Color(1, 1, 1, 0.7f * Mathf.Clamp01(vacT / 0.12f)); }
            }
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
                        if (p.age > 0.25f) { var to = anchor - tr.position; p.v = Vector3.Lerp(p.v, to.normalized * 18f, 1 - Mathf.Exp(-dt * 7f)); if (to.magnitude < 0.35f) { dead = true; creditPulse = 1f; tallyPulse = 1f; OrbitSfx.Play("coin", 0.3f, 0.05f, 0.1f); } }
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
            for (int i = notices.Count - 1; i >= 0; i--) { notices[i].t += Time.unscaledDeltaTime; if (notices[i].t > 2.8f) notices.RemoveAt(i); }
        }

        // ───────────────────────────────── 그림 (단순한 도형)

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
