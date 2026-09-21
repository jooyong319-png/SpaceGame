using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Run;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔴 **보는 맛** (2026-09-21 첫 판 뒤). 사장님: *"이런류의 게임은 유저가 하는 요소가 별로 없어서
    /// 보는 맛이 좀 있어야하는데 전혀 없었어."* — 넷 다 고르셨다: 파바바박 부서짐 · 숫자 연출 · 커지는 게 보임 · 세상이 변함.
    ///
    /// 🔴 규칙은 하나도 안 건드린다. 시뮬의 숫자를 **화면에서 일어나는 일**로 옮길 뿐이다.
    ///    (한 번에 한 가지만 바꾼다 — 이번 회차는 「보는 맛」 하나)
    /// </summary>
    public class OrbitFx : MonoBehaviour
    {
        OrbitGame game;
        Camera cam;
        Vector3 earthPos;
        Sprite dot, ring, droneSprite, crossSprite, earthSprite;

        // ───────────────────────────────── 입자

        class P
        {
            public SpriteRenderer sr;
            public Vector3 v;
            public float age, life, size, drag, delay;
            public Color c;
            public int kind;           // 0 파편 · 1 드론으로 빨려감 · 2 동전 · 3 유성 · 4 로켓 · 5 섬광 고리
            public Transform home;
            public Vector3 homePos;
            public int payload;        // 로켓: 1 이면 도착해 위성이 된다
            public float radius;
        }

        readonly List<P> live = new List<P>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        const int MaxParticles = 900;

        // ───────────────────────────────── 숫자 튀기

        public class Popup
        {
            public Vector3 world;
            public string text;
            public float age, life = 1.2f, size = 19f;
            public Color c;
        }
        public readonly List<Popup> popups = new List<Popup>();
        public float creditPulse;          // 동전이 꽂힐 때 크레딧 숫자가 한 번 튄다

        /// <summary>
        /// 🔴 연출의 세기 — 끝으로 갈수록 커진다. 1막 1 → 2막 1.3~1.8 → 3막 2~3 → 회수 극대화 3.5.
        /// 사장님: *"마지막엔 진짜 도파민 돌아야해"* (09-21). 숫자가 커지는 만큼 화면도 커진다.
        /// </summary>
        public float intensity = 1f;
        public float shake;                // 카메라 흔들림 — 봉쇄 · 3막 시작 · 정거장 피격에서만

        SpriteRenderer station;
        float stationT = -1f, stationAngle;

        // ───────────────────────────────── 세상

        class Sat
        {
            public SpriteRenderer sr;
            public float angle, radius, speed, blink;
        }
        readonly List<Sat> sats = new List<Sat>();

        class Structure
        {
            public string id;
            public SpriteRenderer sr;
            public float angle, radius, speed, spin;
        }
        readonly List<Structure> structures = new List<Structure>();

        SpriteRenderer earth, shade, atmo;
        Texture2D earthTex;
        Color32[] earthPx;
        float earthScroll, earthTimer, rocketTimer, meteorTimer;
        float t;

        public void Init(OrbitGame g, Camera c, Vector3 earthAt, Sprite disc, Sprite thinRing, Sprite drone)
        {
            game = g; cam = c; earthPos = earthAt;
            dot = disc; ring = thinRing; droneSprite = drone;
            crossSprite = MakeCross();

            // 🔴 지구 — 파란 원이 아니라 대륙이 도는 행성. 세상이 변하는 걸 보여줄 바탕이다
            atmo = Make("대기", MakeGlow(128), earthPos, 3.0f, new Color(0.35f, 0.6f, 1f, 0.35f), 4);
            earthSprite = MakeEarth(128, out earthTex, out earthPx);
            earth = Make("지구", earthSprite, earthPos, 2.2f, Color.white, 5);
            shade = Make("밤", MakeShade(128), earthPos, 2.22f, Color.white, 6);
            SyncStructures(false);
        }

        // ───────────────────────────────── 매 프레임

        public void Tick(float dt, float simDt)
        {
            if (game == null || game.sim == null) return;
            t += dt;
            var S = game.sim.S;

            UpdateEarth(dt);
            UpdateSats(dt, simDt);
            UpdateStructures(dt);
            Rockets(simDt);
            Meteors(simDt);
            UpdateParticles(dt);
            UpdateStation(dt);
            shake = Mathf.MoveTowards(shake, 0f, dt * 0.8f);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].age += dt;
                if (popups[i].age >= popups[i].life) popups.RemoveAt(i);
            }
            creditPulse = Mathf.MoveTowards(creditPulse, 0f, dt * 3f);
        }

        // ───────────────────────────────── 파바바박

        /// <summary>드론이 주운 자리 — 파편이 터져 흩어졌다가 드론으로 빨려 들어간다.</summary>
        public void CollectBurst(Vector3 at, Transform drone, bool shatter)
        {
            int n = Mathf.Min(24, Mathf.RoundToInt((shatter ? 10 : 8) * intensity));
            Color c = shatter ? new Color(1f, 0.55f, 0.25f) : new Color(0.85f, 0.87f, 0.92f);
            for (int i = 0; i < n; i++)
            {
                var p = Spawn(at, 0.09f, c, 1, Random.Range(0.35f, 0.6f));
                if (p == null) return;
                p.v = Random.insideUnitCircle.normalized * Random.Range(1.6f, 3.6f);
                p.drag = 6f;
                p.home = drone;
                p.delay = 0.12f;
            }
            if (shatter)
            {
                // 파쇄 — 못 잡은 조각이 튀어 나가 새 파편이 된다 (「파편 +35%」가 눈에 보인다)
                for (int i = 0; i < 2; i++)
                {
                    var p = Spawn(at, 0.06f, new Color(1f, 0.45f, 0.2f), 0, 0.9f);
                    if (p == null) return;
                    p.v = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
                    p.drag = 1.5f;
                }
            }
            Ring(at, 0.5f * Mathf.Sqrt(intensity), shatter ? new Color(1f, 0.6f, 0.3f) : new Color(1f, 0.95f, 0.8f), 0.25f);
        }

        /// <summary>손으로 누른 것 · 죽은 위성 · 충돌 — 크게 한 번 터진다.</summary>
        public void BigBurst(Vector3 at, Color c, int n, float speed, float ringSize)
        {
            for (int i = 0; i < n; i++)
            {
                var p = Spawn(at, Random.Range(0.05f, 0.11f), Color.Lerp(c, Color.white, Random.value * 0.4f), 0, Random.Range(0.4f, 0.9f));
                if (p == null) break;
                p.v = Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed);
                p.drag = 3f;
            }
            Ring(at, ringSize, c, 0.45f);
        }

        /// <summary>3막 충돌 — 한 번 터지면 옆에서 또 터진다. 연쇄가 눈에 보여야 한다.</summary>
        public void ChainBurst(Vector3 at, int depth)
        {
            BigBurst(at, new Color(1f, 0.75f, 0.45f), Mathf.RoundToInt(10 * intensity), 3f * Mathf.Sqrt(intensity), 0.8f * Mathf.Sqrt(intensity));
            if (depth <= 0) return;
            int kids = Random.Range(1, 3);
            for (int i = 0; i < kids; i++)
            {
                var p = Spawn(at + (Vector3)(Random.insideUnitCircle * 0.5f), 0.01f, Color.clear, 6, Random.Range(0.12f, 0.3f));
                if (p == null) return;
                p.payload = depth - 1;
            }
        }

        /// <summary>도미노 — 궤도를 따라 차례로 터진다. 연쇄 충돌이 「번지는」 게 보여야 한다.</summary>
        public void Domino(int band, float startAngle, int count, float step, float gap, int depth)
        {
            for (int k = 0; k < count; k++)
            {
                var at = Pos(startAngle + k * step, OrbitGame.BandR[band] + Random.Range(-0.15f, 0.15f));
                var p = Spawn(at, 0.01f, Color.clear, 6, 0.02f + k * gap);
                if (p == null) return;
                p.payload = depth;
            }
        }

        /// <summary>2막 후반 — 큰 정거장이 지나가다 파편에 맞는다. 「뭔가 온다」.</summary>
        public void StationPass()
        {
            if (station == null)
                station = Make("우주정거장", PixelArt.Satellite(40, 21), earthPos, 1.5f, new Color(0.9f, 0.93f, 1f), 30);
            station.enabled = true;
            stationAngle = Random.Range(0f, 6.28f);
            stationT = 0f;
        }

        void UpdateStation(float dt)
        {
            if (station == null || stationT < 0f) return;
            stationT += dt;
            stationAngle += 0.25f * dt;
            var at = Pos(stationAngle, 2.7f);
            station.transform.position = at;
            station.transform.rotation = Quaternion.Euler(0, 0, stationT * 12f);
            if (stationT < 2.6f)
            {
                // 날아드는 파편이 보인다
                if (Random.value < dt * 10f)
                {
                    var from = at + (Vector3)(Random.insideUnitCircle.normalized * 2.5f);
                    var p = Spawn(from, 0.07f, new Color(0.8f, 0.8f, 0.85f), 3, 0.5f);
                    if (p != null) p.v = (at - from) / 0.5f;
                }
                return;
            }
            // 맞았다
            BigBurst(at, new Color(1f, 0.9f, 0.75f), 70, 5f, 3f);
            Domino(1, stationAngle, 8, 0.18f, 0.07f, 1);
            shake = Mathf.Max(shake, 0.18f);
            station.enabled = false;
            stationT = -1f;
        }

        void Ring(Vector3 at, float size, Color c, float life)
        {
            var p = Spawn(at, 0.05f, c, 5, life);
            if (p == null) return;
            p.size = size;
            p.sr.sprite = ring;
        }

        // ───────────────────────────────── 숫자

        public void Pop(Vector3 world, string text, Color c, float size = 19f)
        {
            if (popups.Count > 30) popups.RemoveAt(0);
            popups.Add(new Popup { world = world + (Vector3)(Random.insideUnitCircle * 0.15f), text = text, c = c, size = size * (1f + (intensity - 1f) * 0.3f) });
        }

        /// <summary>동전이 크레딧 숫자로 날아가 꽂힌다.</summary>
        public void Coins(Vector3 from, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var p = Spawn(from, 0.14f, new Color(1f, 0.82f, 0.3f), 2, 2f);
                if (p == null) return;
                p.v = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
                p.delay = 0.15f + i * 0.04f;
                p.drag = 3f;
            }
        }

        public void CoinShower(int n)
        {
            var anchor = CreditWorld();
            for (int i = 0; i < n; i++)
            {
                var from = earthPos + (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1.2f, 5f));
                var p = Spawn(from, 0.12f, new Color(1f, 0.85f, 0.35f), 2, 2.5f);
                if (p == null) return;
                p.v = (from - anchor).normalized * Random.Range(1f, 3f);
                p.delay = Random.Range(0.05f, 0.6f);
                p.drag = 2f;
            }
        }

        Vector3 CreditWorld()
        {
            var sp = game.hud != null ? game.hud.CreditAnchorScreen : new Vector2(120, Screen.height - 24);
            var w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10f));
            w.z = 0f;
            return w;
        }

        // ───────────────────────────────── 커지는 게 보인다 — 산 시설이 궤도에 떠 있다

        struct StructDef
        {
            public string id; public int kind, px, seed; public float radius, speed, size; public Color c;
        }

        static readonly StructDef[] Defs =
        {
            new StructDef { id = "yard",     kind = 0, px = 20, seed = 3, radius = 1.45f, speed = 0.18f, size = 0.55f, c = new Color(0.8f, 0.82f, 0.9f) },
            new StructDef { id = "scanner",  kind = 1, px = 12, seed = 7, radius = 1.55f, speed = -0.25f, size = 0.35f, c = new Color(0.7f, 0.9f, 1f) },
            new StructDef { id = "board",    kind = 1, px = 14, seed = 9, radius = 2.7f, speed = 0.1f, size = 0.4f, c = new Color(0.9f, 0.9f, 0.7f) },
            new StructDef { id = "salvager", kind = 2, px = 22, seed = 2, radius = 2.0f, speed = 0.22f, size = 0.7f, c = new Color(0.95f, 0.85f, 0.6f) },
            new StructDef { id = "recycle",  kind = 0, px = 26, seed = 5, radius = 2.75f, speed = -0.08f, size = 0.75f, c = new Color(0.75f, 0.9f, 0.75f) },
            new StructDef { id = "grade2",   kind = 2, px = 20, seed = 6, radius = 3.4f, speed = 0.14f, size = 0.6f, c = new Color(0.8f, 0.9f, 1f) },
            new StructDef { id = "swarm",    kind = 0, px = 22, seed = 11, radius = 4.1f, speed = 0.06f, size = 0.7f, c = new Color(0.95f, 0.8f, 0.5f) },
            new StructDef { id = "refinery", kind = 3, px = 30, seed = 4, radius = 4.15f, speed = -0.05f, size = 0.95f, c = new Color(1f, 0.75f, 0.45f) },
            new StructDef { id = "cap",      kind = 0, px = 18, seed = 13, radius = 5.35f, speed = 0.04f, size = 0.5f, c = new Color(1f, 0.95f, 0.8f) },
        };

        public void SyncStructures(bool announce)
        {
            if (game == null || game.sim == null) return;
            foreach (var d in Defs)
            {
                if (!game.sim.Has(d.id) || structures.Exists(s => s.id == d.id)) continue;
                Sprite sp = d.kind == 0 ? PixelArt.Satellite(d.px + 6, d.seed)   // Station 은 작으면 색 네모로 뭉개졌다 (09-22 캡처)
                          : d.kind == 1 ? PixelArt.Satellite(d.px, d.seed)
                          : d.kind == 2 ? PixelArt.Vessel(d.px, d.seed)
                          : PixelArt.Hulk(d.px, d.seed);
                var st = new Structure { id = d.id, angle = Random.Range(0f, 6.28f), radius = d.radius, speed = d.speed, spin = Random.Range(-20f, 20f) };
                st.sr = Make("시설 " + d.id, sp, earthPos, d.size, d.c, 19);
                structures.Add(st);
                if (announce)
                {
                    var at = Pos(st.angle, st.radius);
                    BigBurst(at, new Color(1f, 0.9f, 0.6f), 16, 2.5f, 1.2f);
                }
            }
        }

        void UpdateStructures(float dt)
        {
            foreach (var s in structures)
            {
                s.angle += s.speed * dt;
                s.sr.transform.position = Pos(s.angle, s.radius);
                s.sr.transform.rotation = Quaternion.Euler(0, 0, t * s.spin);
            }
        }

        // ───────────────────────────────── 세상이 변한다

        /// <summary>
        /// 위성 — 궤도를 치우면 세상이 쏘아 올린다 (1막 「민간 위성 발사 성공 — 궤도 정리 덕분」).
        /// 🔴 3막에는 그 위성들이 깨진다. 내가 치워서 늘어난 것이 내가 더럽힌 궤도에서 죽는다.
        /// </summary>
        int SatTarget()
        {
            var S = game.sim.S;
            var leo = S.orbits[0];
            if (S.act == 1) return Mathf.RoundToInt((float)(1 - leo.D / leo.D0) * 34f);
            double since2 = S.t - S.act2At;
            int grown = 30 + Mathf.Min(70, (int)(since2 / 10));
            if (S.act == 2) return grown;
            float k = Mathf.Clamp01((float)(S.t - S.act3At) / 480f);
            return Mathf.RoundToInt(grown * (1f - k));
        }

        void UpdateSats(float dt, float simDt)
        {
            int target = SatTarget();
            // 늘 때 — 지구에서 로켓이 올라가 위성이 된다 (한 번에 너무 많이는 안 쏜다)
            if (sats.Count + PendingRockets() < target && Random.value < 4f * simDt)
                Launch(true);
            // 줄 때 — 부딪혀 깨진다
            if (sats.Count > target && Random.value < 3f * simDt && sats.Count > 0)
            {
                var s = sats[Random.Range(0, sats.Count)];
                ChainBurst(s.sr.transform.position, 1);
                Recycle(s.sr);
                sats.Remove(s);
            }
            foreach (var s in sats)
            {
                s.angle += s.speed * dt;
                s.sr.transform.position = Pos(s.angle, s.radius);
                s.blink += dt;
                float a = 0.55f + 0.45f * Mathf.Max(0f, Mathf.Sin(s.blink * 3f));
                s.sr.color = new Color(0.75f, 0.9f, 1f, a);
            }
        }

        int PendingRockets()
        {
            int n = 0;
            foreach (var p in live) if (p.kind == 4 && p.payload == 1) n++;
            return n;
        }

        void Launch(bool becomesSat)
        {
            float a = Random.Range(0f, 6.28f);
            var p = Spawn(Pos(a, 1.1f), 0.09f, new Color(1f, 0.95f, 0.85f), 4, 1.6f);
            if (p == null) return;
            p.payload = becomesSat ? 1 : 0;
            p.radius = becomesSat ? Random.Range(1.5f, 5.3f) : OrbitGame.BandR[Random.Range(0, 3)];
            p.v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }

        /// <summary>2막 발사 대행 — 로켓이 올라가 터지며 파편을 남긴다. 파편이 어디서 오는지가 보인다.</summary>
        void Rockets(float simDt)
        {
            var S = game.sim.S;
            if (S.act != 2 || !game.sim.Has("launch")) return;
            rocketTimer -= simDt;
            if (rocketTimer > 0) return;
            rocketTimer = Random.Range(1.2f, 2.5f);
            Launch(false);
        }

        /// <summary>3막 — 재진입하는 파편. 뉴스는 이걸 「유성우」라고 부른다.</summary>
        void Meteors(float simDt)
        {
            var S = game.sim.S;
            if (S.act < 3 || !S.orbits[0].locked) return;
            meteorTimer -= simDt;
            if (meteorTimer > 0) return;
            meteorTimer = Random.Range(0.08f, 0.25f);
            float a = Random.Range(0f, 6.28f);
            var from = Pos(a, Random.Range(2.3f, 3.2f));
            var p = Spawn(from, 0.06f, new Color(1f, 0.6f, 0.3f), 3, 1.2f);
            if (p == null) return;
            var to = Pos(a + Random.Range(-0.6f, 0.6f), 1.05f);
            p.v = (to - from) / 1.1f;
        }

        void UpdateEarth(float dt)
        {
            // 대륙이 천천히 돈다
            earthTimer -= dt;
            if (earthTimer <= 0f)
            {
                earthTimer = 0.12f;
                earthScroll += 0.35f;
                PaintEarth(earthTex, earthPx, earthScroll);
            }

            // 🔴 하늘 — 치우면 맑고, 더러워질수록 탁해진다. 3막엔 붉게 깜빡인다
            var S = game.sim.S;
            float dirt = Mathf.Clamp01((float)((game.sim.TotalD - 34000) / 160000));
            Color clean = new Color(0.35f, 0.62f, 1f, 0.38f);
            Color dirty = new Color(0.62f, 0.56f, 0.46f, 0.55f);
            Color c = Color.Lerp(clean, dirty, dirt);
            if (S.act >= 3) c = Color.Lerp(c, new Color(0.9f, 0.45f, 0.35f, 0.6f), 0.3f + 0.2f * Mathf.Sin(t * 2f));
            atmo.color = c;
            atmo.transform.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t * 0.8f)) * (3.0f / Mathf.Max(0.01f, atmo.sprite.bounds.size.x));
            earth.color = Color.Lerp(Color.white, new Color(0.8f, 0.78f, 0.72f), dirt);
        }

        // ───────────────────────────────── 입자 굴리기

        P Spawn(Vector3 at, float size, Color c, int kind, float life)
        {
            if (live.Count >= MaxParticles) return null;
            var sr = pool.Count > 0 ? pool.Pop() : null;
            if (sr == null)
            {
                var go = new GameObject("fx");
                go.transform.SetParent(transform);
                sr = go.AddComponent<SpriteRenderer>();
            }
            sr.gameObject.SetActive(true);
            sr.sprite = kind == 2 || kind == 4 || kind == 3 ? dot : dot;
            sr.color = c;
            sr.sortingOrder = kind == 2 ? 40 : 22;
            sr.transform.position = at;
            sr.transform.rotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, dot.bounds.size.x));
            var p = new P { sr = sr, c = c, kind = kind, life = life, size = size };
            live.Add(p);
            return p;
        }

        void UpdateParticles(float dt)
        {
            Vector3 coinTarget = CreditWorld();
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.age += dt;
                float k = p.age / p.life;
                var tr = p.sr.transform;
                bool dead = k >= 1f;

                switch (p.kind)
                {
                    case 0:     // 파편 — 흩어지며 사라진다
                        p.v *= Mathf.Exp(-p.drag * dt);
                        tr.position += p.v * dt;
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, p.c.a * (1f - k));
                        break;

                    case 1:     // 드론으로 빨려 간다
                    case 2:     // 동전 — 크레딧 숫자로
                        if (p.age < p.delay)
                        {
                            p.v *= Mathf.Exp(-p.drag * dt);
                            tr.position += p.v * dt;
                            break;
                        }
                        Vector3 target = p.kind == 2 ? coinTarget : (p.home != null ? p.home.position : tr.position);
                        Vector3 to = target - tr.position;
                        float speed = p.kind == 2 ? 14f : 9f;
                        p.v = Vector3.Lerp(p.v, to.normalized * speed, 1f - Mathf.Exp(-dt * 7f));
                        tr.position += p.v * dt;
                        if (to.magnitude < (p.kind == 2 ? 0.35f : 0.12f))
                        {
                            dead = true;
                            if (p.kind == 2) creditPulse = 1f;
                        }
                        if (p.kind == 2) p.sr.color = new Color(p.c.r, p.c.g, p.c.b, Mathf.Min(1f, p.age * 4f));
                        break;

                    case 3:     // 유성 — 길게 늘어져 떨어진다
                        tr.position += p.v * dt;
                        tr.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.v.y, p.v.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(p.size * 5f, p.size, 1f) / Mathf.Max(0.01f, dot.bounds.size.x);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, Mathf.Sin(Mathf.PI * Mathf.Min(1f, k)));
                        if ((tr.position - earthPos).magnitude < 1.12f) dead = true;
                        break;

                    case 4:     // 로켓 — 지구에서 올라간다
                    {
                        float r = (tr.position - earthPos).magnitude;
                        tr.position += p.v * dt * 2.2f;
                        tr.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.v.y, p.v.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(p.size * 2.5f, p.size, 1f) / Mathf.Max(0.01f, dot.bounds.size.x);
                        if (Random.value < 0.6f)
                        {
                            var tail = Spawn(tr.position, 0.05f, new Color(1f, 0.7f, 0.35f, 0.8f), 0, 0.35f);
                            if (tail != null) { tail.v = -p.v * 0.3f; tail.drag = 2f; }
                        }
                        if (r >= p.radius)
                        {
                            dead = true;
                            if (p.payload == 1) AddSat(tr.position);
                            else
                            {
                                // 발사가 남긴 파편
                                BigBurst(tr.position, new Color(0.85f, 0.85f, 0.9f), 6, 1.5f, 0.4f);
                            }
                        }
                        break;
                    }

                    case 5:     // 섬광 고리
                    {
                        float s = Mathf.Lerp(0.1f, p.size, 1f - (1f - k) * (1f - k));
                        tr.localScale = Vector3.one * s / Mathf.Max(0.01f, ring.bounds.size.x);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1f - k);
                        break;
                    }

                    case 6:     // 연쇄 — 잠깐 뒤 옆에서 또 터진다
                        if (dead) ChainBurst(tr.position, p.payload);
                        break;
                }

                if (dead) { Recycle(p.sr); live.RemoveAt(i); }
            }
        }

        void AddSat(Vector3 at)
        {
            float r = (at - earthPos).magnitude;
            float a = Mathf.Atan2(at.y - earthPos.y, at.x - earthPos.x);
            var sr = Make("위성", crossSprite, at, 0.14f, new Color(0.75f, 0.9f, 1f), 9);
            sats.Add(new Sat { sr = sr, angle = a, radius = r, speed = 0.4f / Mathf.Max(1f, r * 0.6f), blink = Random.value * 6f });
            Ring(at, 0.3f, new Color(0.7f, 0.9f, 1f), 0.35f);
        }

        void Recycle(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }

        Vector3 Pos(float angle, float r) => earthPos + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);

        SpriteRenderer Make(string name, Sprite s, Vector3 pos, float size, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, s.bounds.size.x));
            return sr;
        }

        // ───────────────────────────────── 그림 (코드로 찍는다 — HideAndDontSave)

        static Texture2D NewTex(int w, int h, FilterMode f)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = f, wrapMode = TextureWrapMode.Clamp };
        }

        static Sprite ToSprite(Texture2D tex, int ppu)
        {
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite MakeEarth(int size, out Texture2D tex, out Color32[] px)
        {
            tex = NewTex(size, size, FilterMode.Point);
            px = new Color32[size * size];
            PaintEarth(tex, px, 0f);
            return ToSprite(tex, size);
        }

        static void PaintEarth(Texture2D tex, Color32[] px, float scroll)
        {
            int size = tex.width;
            float c = (size - 1) * 0.5f, R = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / R, dy = (y - c) / R;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 1f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    // 구 위의 경도로 대륙을 민다 — 가장자리는 눌려 보인다
                    float lon = Mathf.Asin(Mathf.Clamp(dx / Mathf.Sqrt(Mathf.Max(0.0001f, 1f - dy * dy)), -1f, 1f));
                    float u = lon * 22f + scroll, v = dy * 22f;
                    float n = Mathf.PerlinNoise(u * 0.18f + 11f, v * 0.18f + 3f) * 0.7f + Mathf.PerlinNoise(u * 0.45f, v * 0.45f + 9f) * 0.3f;
                    Color col;
                    if (Mathf.Abs(dy) > 0.86f) col = new Color(0.9f, 0.94f, 0.98f);                          // 극지방
                    else if (n > 0.56f) col = Color.Lerp(new Color(0.28f, 0.55f, 0.3f), new Color(0.58f, 0.52f, 0.34f), (n - 0.56f) * 4f);
                    else col = Color.Lerp(new Color(0.13f, 0.34f, 0.7f), new Color(0.2f, 0.45f, 0.82f), n * 1.4f);
                    // 구름 몇 점
                    float cl = Mathf.PerlinNoise(u * 0.3f + scroll * 0.3f + 40f, v * 0.3f + 20f);
                    if (cl > 0.68f) col = Color.Lerp(col, Color.white, 0.6f);
                    float edge = Mathf.Clamp01((1f - r) * R * 0.5f);
                    px[y * size + x] = new Color32((byte)(col.r * 255), (byte)(col.g * 255), (byte)(col.b * 255), (byte)(255 * edge));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
        }

        static Sprite MakeShade(int size)
        {
            var tex = NewTex(size, size, FilterMode.Bilinear);
            float c = (size - 1) * 0.5f, R = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / R, dy = (y - c) / R;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01((1f - r) * R * 0.5f);
                    // 오른쪽 아래가 밤이다
                    float night = Mathf.SmoothStep(0f, 1f, (dx * 0.8f - dy * 0.5f + 0.1f) * 1.6f);
                    float rim = Mathf.Pow(r, 6f) * 0.35f;
                    byte a = (byte)(255 * edge * Mathf.Clamp01(night * 0.72f + rim));
                    px[y * size + x] = new Color32(5, 8, 20, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return ToSprite(tex, size);
        }

        static Sprite MakeGlow(int size)
        {
            var tex = NewTex(size, size, FilterMode.Bilinear);
            float c = (size - 1) * 0.5f, R = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / R;   // 0..1, 지구 가장자리는 0.73
                    float g = r < 0.7f ? 0f : Mathf.Clamp01(1f - (r - 0.72f) / 0.28f);
                    g = g * g;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * g));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return ToSprite(tex, size);
        }

        static Sprite MakeCross()
        {
            var tex = NewTex(5, 5, FilterMode.Point);
            var px = new Color32[25];
            for (int i = 0; i < 25; i++)
            {
                int x = i % 5, y = i / 5;
                bool on = x == 2 || (y == 2 && (x == 0 || x == 4 || x == 1 || x == 3));
                px[i] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return ToSprite(tex, 5);
        }
    }
}
