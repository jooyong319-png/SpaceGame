using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 rev16 「빚 갚는 청소선」 — 화면 · 입력 · 연출 · 저장. 규칙은 전부 Sim/DebtSim.cs.
    /// 커서가 곧 청소선 집게다. 궤도 위에 대고 있으면 1초마다 저절로 내리친다 (Bills Must Be Paid 의 망치).
    /// ⚠️ 그림은 단순한 도형이어도 된다 — 사장님: *"도트도 굳이 안해도 된다"* (09-22)
    /// </summary>
    public class DebtGame : MonoBehaviour
    {
        public DebtSim sim;
        public DebtHud hud;
        public Camera cam;
        public float timeScale = 1f;

        const string StateKey = "debt.state", MetaKey = "debt.meta";
        const float PxPerUnit = 50f;

        Sprite disc, ring, pixel, glow, bandSprite;
        Sprite[] junk;
        Sprite deadSat, wreck, drone;
        readonly List<SpriteRenderer> debrisViews = new List<SpriteRenderer>();
        SpriteRenderer earth, atmo, band, claw, clawRing, clawWind;
        float t, saveTimer;
        public bool aimOn;
        public Vector2 aimPx;              // 시안과 같은 960×600 좌표
        public static bool TestAim;
        public static Vector2 TestPx;

        class P { public SpriteRenderer sr; public Vector3 v; public float age, life, size; public Color c; public int kind; public Vector3 a, b; }
        readonly List<P> fx = new List<P>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        public class Pop { public Vector2 px; public string text; public Color c; public float age, size; }
        public readonly List<Pop> pops = new List<Pop>();
        public float creditPulse, shake;

        // ───────────────────────────────── 부팅

        // rev17 SweepGame 으로 넘어갔다 (2026-09-23) — rev16 은 태그 rev16-final
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<DebtGame>() != null) return;
            new GameObject("== 빚 갚는 청소선 ==").AddComponent<DebtGame>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Load();
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); }
            cam.orthographic = true; cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.016f, 0.024f, 0.043f);
            cam.transform.position = new Vector3(0, 0, -10);
            if (FindFirstObjectByType<Light2D>() == null) { var l = new GameObject("Global Light 2D").AddComponent<Light2D>(); l.lightType = Light2D.LightType.Global; }

            disc = Ring(128, 0f); ring = Ring(128, 0.9f); pixel = Ring(8, 0f); glow = Glow(128);
            junk = OrbitArt.Debris(); deadSat = OrbitArt.DeadSat(); wreck = OrbitArt.BigWreck(); drone = OrbitArt.Drone();

            Stars();
            atmo = Make(glow, PxToWorld(480, 300), 3.4f, new Color(0.35f, 0.6f, 1f, 0.35f), 4);
            var eg = Make(disc, PxToWorld(480, 300), 2.48f, new Color(0.30f, 0.52f, 0.86f), 5); earth = eg;
            bandSprite = Ring(256, 0.55f);
            band = Make(bandSprite, PxToWorld(480, 300), 7f, new Color(1, 1, 1, 0.04f), 1);
            claw = Make(drone, Vector3.zero, 0.55f, Color.white, 30);
            clawRing = Make(ring, Vector3.zero, 1.7f, new Color(1f, 0.76f, 0.3f, 0.8f), 29);
            clawWind = Make(disc, Vector3.zero, 0.2f, new Color(1f, 0.87f, 0.58f, 0.95f), 31);

            gameObject.AddComponent<OrbitSfx>();
            hud = gameObject.AddComponent<DebtHud>();
            hud.game = this;
        }

        // ───────────────────────────────── 저장

        void Load()
        {
            DebtState s = null; DebtMeta m = null;
            try
            {
                string js = PlayerPrefs.GetString(StateKey, ""), jm = PlayerPrefs.GetString(MetaKey, "");
                if (!string.IsNullOrEmpty(js)) { s = new DebtState(); JsonUtility.FromJsonOverwrite(js, s); }
                if (!string.IsNullOrEmpty(jm)) { m = new DebtMeta(); JsonUtility.FromJsonOverwrite(jm, m); }
            }
            catch { s = null; m = null; }
            sim = new DebtSim(s, m);
        }

        public void Save()
        {
            if (sim == null) return;
            PlayerPrefs.SetString(StateKey, JsonUtility.ToJson(sim.S));
            PlayerPrefs.SetString(MetaKey, JsonUtility.ToJson(sim.M));
            PlayerPrefs.Save();
        }

        public void WipeAll()
        {
            PlayerPrefs.DeleteKey(StateKey); PlayerPrefs.DeleteKey(MetaKey); PlayerPrefs.Save();
            sim = new DebtSim();
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
                int steps = Mathf.Max(1, Mathf.CeilToInt(sdt / 0.05f));
                for (int i = 0; i < steps; i++) sim.Tick(sdt / steps, aimPx.x, aimPx.y, aimOn);
            }
            Consume();
            DrawDebris();
            DrawClaw();
            UpdateFx(dt);
            creditPulse = Mathf.MoveTowards(creditPulse, 0, dt * 3f);
            shake = Mathf.MoveTowards(shake, 0, dt * 0.8f);
            cam.transform.position = new Vector3(0, 0, -10) + (Vector3)(Random.insideUnitCircle * shake);
            atmo.transform.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t)) * (3.4f / glow.bounds.size.x);
            saveTimer += dt;
            if (saveTimer > 5f) { saveTimer = 0; Save(); }
        }

        void ReadAim()
        {
            var mouse = Mouse.current;
            aimOn = false;
            if (TestAim && hud != null && !hud.Blocking) { aimPx = TestPx; aimOn = true; return; }   // 에디터 시험용 (MCP 자동 플레이)
            if (mouse == null || hud == null || hud.Blocking) return;
            Vector2 sp = mouse.position.ReadValue();
            if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) return;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10));
            aimPx = new Vector2(480 + w.x * PxPerUnit, 300 - w.y * PxPerUnit);
            aimOn = true;
        }

        public Vector3 PxToWorld(double x, double y) => new Vector3((float)(x - 480) / PxPerUnit, (float)(300 - y) / PxPerUnit, 0);

        // ───────────────────────────────── 사건 → 연출 · 소리

        void Consume()
        {
            while (sim.Events.Count > 0)
            {
                var e = sim.Events.Dequeue();
                var at = PxToWorld(e.x, e.y);
                switch (e.kind)
                {
                    case DebtEv.Slam:
                        Add(ring, at, 0.1f, e.k == 1 ? new Color(1f, 0.87f, 0.58f) : new Color(0.35f, 0.38f, 0.44f), 5, 0.22f, (float)e.v * 2 / PxPerUnit);
                        OrbitSfx.Play(e.k == 1 ? "blast" : "tick", e.k == 1 ? 0.45f : 0.3f, 0.05f);
                        if (e.k == 1) shake = Mathf.Max(shake, 0.04f); else PopAt(e.x, e.y + 30, "헛침", new Color(0.5f, 0.54f, 0.6f), 13);
                        break;
                    case DebtEv.Broke:
                        Burst(at, DebrisColor(e.k), e.k == DebtSim.Wreck ? 40 : e.k == DebtSim.Vault ? 26 : 8 + (int)DebtSim.Types[e.k].r);
                        OrbitSfx.Play(e.k == DebtSim.Wreck ? "break" : e.k == DebtSim.Vault ? "unit" : e.k == DebtSim.Chip ? "pick" : "clank", e.k == DebtSim.Chip ? 0.35f : 0.8f, 0.03f);
                        if (e.k == DebtSim.Wreck) shake = Mathf.Max(shake, 0.2f);
                        break;
                    case DebtEv.Coin:
                        PopAt(e.x, e.y, "+" + KNum.Fmt(e.v), e.k == 1 ? new Color(0.9f, 0.65f, 0.6f) : new Color(1f, 0.87f, 0.58f), 15 + Mathf.Min(10, Mathf.Log10((float)e.v + 1) * 3));
                        for (int i = 0; i < Mathf.Min(5, 1 + (int)Mathf.Log10((float)e.v + 1)); i++) Add(disc, at, 0.12f, new Color(1f, 0.8f, 0.3f), 2, 1.6f).v = (Vector3)(Random.insideUnitCircle * 3f);
                        break;
                    case DebtEv.Pop: PopAt(e.x, e.y, e.text, new Color(0.45f, 0.85f, 0.6f), 16); OrbitSfx.Play("buy", 0.6f); break;
                    case DebtEv.Ring: Add(ring, at, 0.1f, new Color(0.9f, 0.35f, 0.3f), 5, 0.35f, (float)e.v * 2 / PxPerUnit); OrbitSfx.Play("collide", 0.7f, 0.05f); break;
                    case DebtEv.Bolt:
                    {
                        var p = Add(pixel, at, 0.05f, e.k == 2 ? new Color(0.56f, 0.82f, 1f) : e.k == 4 ? new Color(1f, 0.76f, 0.3f) : new Color(1f, 0.7f, 0.44f), 3, 0.18f);
                        p.a = at; p.b = PxToWorld(e.x2, e.y2);
                        if (e.k == 1) OrbitSfx.Play("tick", 0.4f, 0.04f, 0.2f);
                        break;
                    }
                    case DebtEv.Crit: PopAt(e.x, e.y, "치명타!", new Color(1f, 0.55f, 0.36f), 22); OrbitSfx.Play("blast", 0.9f, 0.1f); shake = Mathf.Max(shake, 0.1f); break;
                    case DebtEv.RunEnd: Save(); hud.OnRunEnd(); break;
                    case DebtEv.Overdue: hud.News("연체 — 추심원이 붙었다. 갚을 때까지 수입의 " + Mathf.RoundToInt((float)sim.Cut * 100) + "%를 떼 간다"); OrbitSfx.Play("warn", 0.9f); break;
                    case DebtEv.BillPaid: hud.News(e.text); OrbitSfx.Play("unit", 1f); Save(); break;
                    case DebtEv.Bankrupt: hud.News(e.text + " · 빚은 날아갔고 경력은 남았다"); OrbitSfx.Play("lock", 1f); Save(); break;
                    case DebtEv.Won: OrbitSfx.Play("ending", 1f); Save(); break;
                }
            }
        }

        public void PopAt(double x, double y, string text, Color c, float size)
        {
            if (pops.Count > 40) pops.RemoveAt(0);
            pops.Add(new Pop { px = new Vector2((float)x + Random.Range(-6f, 6f), (float)y), text = text, c = c, size = size });
        }

        static Color DebrisColor(int k)
        {
            switch (k)
            {
                case DebtSim.Vault: return new Color(1f, 0.76f, 0.3f);
                case DebtSim.Fuel: return new Color(0.44f, 0.81f, 0.59f);
                case DebtSim.Bomb: return new Color(0.89f, 0.35f, 0.29f);
                default: return new Color(0.7f, 0.73f, 0.78f);
            }
        }

        // ───────────────────────────────── 궤도 · 잔해

        void DrawDebris()
        {
            var o = DebtSim.Orbits[sim.S.orbit];
            float outer = (float)(o.r1 + 14) * 2 / PxPerUnit;      // 궤도 띠 — 잔해가 도는 자리
            band.transform.localScale = new Vector3(outer / bandSprite.bounds.size.x, outer * (float)DebtSim.Squash / bandSprite.bounds.size.y, 1);
            band.color = sim.S.orbit == 0 ? new Color(0.5f, 0.6f, 0.8f, 0.06f) : sim.S.orbit == 1 ? new Color(0.6f, 0.5f, 0.9f, 0.07f) : new Color(0.9f, 0.5f, 0.4f, 0.07f);

            var list = sim.R.debris;
            while (debrisViews.Count < list.Count) debrisViews.Add(Make(junk[0], Vector3.zero, 0.2f, Color.white, 10));
            for (int i = 0; i < debrisViews.Count; i++)
            {
                var v = debrisViews[i];
                bool on = i < list.Count && !sim.R.over;
                v.enabled = on;
                if (!on) continue;
                var d = list[i];
                DebtSim.Pos(d, out double x, out double y);
                v.transform.position = PxToWorld(x, y);
                Sprite s; Color c = Color.white;
                switch (d.k)
                {
                    case DebtSim.Sat: s = deadSat; break;
                    case DebtSim.Rocket: s = junk[2]; break;
                    case DebtSim.Vault: s = deadSat; c = new Color(1f, 0.82f, 0.4f); break;
                    case DebtSim.Fuel: s = disc; c = new Color(0.44f, 0.81f, 0.59f); break;
                    case DebtSim.Bomb: s = disc; c = new Color(0.89f, 0.35f, 0.29f); break;
                    case DebtSim.Wreck: s = wreck; break;
                    default: s = junk[i % junk.Length]; break;
                }
                if (v.sprite != s) v.sprite = s;
                // 금 대신 — 맞을수록 어두워진다 (몇 대 남았는지)
                float dmg = 1f - (float)d.hp / Mathf.Max(1, d.max);
                c = Color.Lerp(c, new Color(0.35f, 0.3f, 0.28f), dmg * 0.6f);
                if (d.k == DebtSim.Vault) c = Color.Lerp(c, Color.white, 0.25f + 0.25f * Mathf.Sin(t * 6));
                c.a = (float)d.fade;
                v.color = c;
                float size = (float)DebtSim.Types[d.k].r * 2.4f / PxPerUnit * (d.hit > 0 ? 1.2f : 1f);
                v.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, s.bounds.size.x);
                v.transform.rotation = Quaternion.Euler(0, 0, (float)d.a * 120f);
                v.sortingOrder = 10 + (int)(y / 10);      // 아래쪽이 앞
            }
        }

        void DrawClaw()
        {
            bool show = aimOn && !sim.R.over;
            claw.enabled = clawRing.enabled = clawWind.enabled = show;
            if (!show) { Cursor.visible = true; return; }
            Cursor.visible = false;
            var at = PxToWorld(aimPx.x, aimPx.y);
            float r = (float)sim.Rad * 2 / PxPerUnit;
            bool fuel = sim.R.fuel > 0;
            float wind = 1f - Mathf.Clamp01((float)(sim.R.next / sim.Gap));
            claw.transform.position = at + new Vector3(0, 0.1f + (1 - wind) * 0.25f, 0);
            claw.transform.rotation = Quaternion.Euler(0, 0, -90);
            clawRing.transform.position = at;
            clawRing.transform.localScale = Vector3.one * r / ring.bounds.size.x;
            clawRing.color = fuel ? new Color(1f, 0.76f, 0.3f, 0.35f + 0.5f * wind) : new Color(0.5f, 0.54f, 0.6f, 0.4f);
            float a = wind * Mathf.PI * 2 + Mathf.PI / 2;
            clawWind.transform.position = at + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r / 2;
            clawWind.enabled = fuel;
        }

        // ───────────────────────────────── 입자

        P Add(Sprite s, Vector3 at, float size, Color c, int kind, float life, float grow = 0)
        {
            var sr = pool.Count > 0 ? pool.Pop() : null;
            if (sr == null) { var go = new GameObject("fx"); go.transform.SetParent(transform); sr = go.AddComponent<SpriteRenderer>(); }
            sr.gameObject.SetActive(true);
            sr.sprite = s; sr.color = c; sr.sortingOrder = kind == 2 ? 60 : 40;
            sr.transform.position = at; sr.transform.rotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, s.bounds.size.x);
            var p = new P { sr = sr, c = c, kind = kind, life = life, size = grow > 0 ? grow : size };
            fx.Add(p);
            return p;
        }

        void Burst(Vector3 at, Color c, int n)
        {
            for (int i = 0; i < n && fx.Count < 700; i++)
                Add(pixel, at, Random.Range(0.05f, 0.1f), Color.Lerp(c, Color.white, Random.value * 0.3f), 0, Random.Range(0.4f, 0.8f)).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1f, 4f));
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
                    case 2:
                        if (p.age > 0.2f) { var to = anchor - tr.position; p.v = Vector3.Lerp(p.v, to.normalized * 16f, 1 - Mathf.Exp(-dt * 7f)); if (to.magnitude < 0.35f) { dead = true; creditPulse = 1f; OrbitSfx.Play("coin", 0.35f, 0.05f, 0.1f); } }
                        else p.v *= Mathf.Exp(-3f * dt);
                        tr.position += p.v * dt;
                        break;
                    case 3:
                        tr.position = (p.a + p.b) / 2;
                        var dir = p.b - p.a;
                        tr.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(dir.magnitude / pixel.bounds.size.x, 0.05f / pixel.bounds.size.y, 1);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1 - k);
                        break;
                    case 5:
                        float s = Mathf.Lerp(0.1f, p.size, 1 - (1 - k) * (1 - k));
                        tr.localScale = Vector3.one * s / ring.bounds.size.x;
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1 - k);
                        break;
                }
                if (dead) { p.sr.gameObject.SetActive(false); pool.Push(p.sr); fx.RemoveAt(i); }
            }
            for (int i = pops.Count - 1; i >= 0; i--) { pops[i].age += dt; pops[i].px.y -= 30 * dt; if (pops[i].age > 1f) pops.RemoveAt(i); }
        }

        // ───────────────────────────────── 그림 (단순한 도형 — 도트 안 해도 된다)

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
            for (int i = 0; i < 220; i++)
                Make(Ring(8, 0f), new Vector3((float)(r.NextDouble() * 22 - 11), (float)(r.NextDouble() * 12 - 6)), 0.04f + (float)r.NextDouble() * 0.03f, new Color(1, 1, 1, 0.08f + (float)r.NextDouble() * 0.3f), 0);
        }

        static Sprite Ring(int size, float inner)
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
                float gl = r < 0.7f ? 0 : Mathf.Clamp01(1 - (r - 0.72f) / 0.28f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * gl * gl));
            }
            tex.SetPixels32(px); tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
