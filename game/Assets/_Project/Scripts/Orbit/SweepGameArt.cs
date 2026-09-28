using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🎨 그림 불러오기 · 포대 · 미사일 · 효과 굴리기 (09-28 나눔)
    public partial class SweepGame
    {
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
        static int turClawShip = -1;
        static readonly Sprite[] shipTur = new Sprite[8]; static readonly bool[] shipTurTried = new bool[8];
        static readonly string[] ShipTurName = { "claw", "scatter", "harpoon", "tesla", "missile", "frz", "clus" };
        static Sprite ShipTur(int s) { if (s < 0 || s >= ShipTurName.Length) return null; if (!shipTurTried[s]) { shipTurTried[s] = true; shipTur[s] = Resources.Load<Sprite>("ship/turret_" + ShipTurName[s]); } return shipTur[s] ?? TurSprite(0); }                                             // 🚀 포탑 그림을 불러온 배 (09-26)
        static readonly Sprite[][] planetFrames = new Sprite[12][]; static readonly bool[] planetTried = new bool[12];
        static Sprite[] PlanetFrames(int pi)
        {
            if (pi < 0 || pi >= 12) return null;
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
        // 🪐 행성 특성 화면 (09-26) — 대적점(붉은 점선 소용돌이) · 돌풍(바람 줄기) · 혜성 꼬리
        SpriteRenderer spotView; float gustFx;
        void TraitView(SweepRun R)
        {
            bool spot = sim.SpotOn;
            if (spotView == null) spotView = Make(ring, Vector3.zero, 1f, new Color(1f, 0.4f, 0.28f, 0f), 6);
            spotView.enabled = spot;
            if (spot)
            {
                var c = PxToWorld(sim.SpotX, sim.SpotY); float w = (float)SweepSim.SpotR * 2 / PxPerUnit;
                spotView.transform.position = c; spotView.transform.localScale = new Vector3(w / ring.bounds.size.x, w * (float)SweepSim.Tilt / ring.bounds.size.y, 1);
                spotView.color = new Color(1f, 0.42f, 0.3f, 0.35f + 0.1f * Mathf.Sin(Time.time * 2));
                if (Random.value < 0.6f)
                {   // 소용돌이 — 가장자리에서 안으로 휘어 드는 점
                    float a = Random.value * 6.283f, rr = w * 0.5f;
                    var p = c + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * (float)SweepSim.Tilt, 0);
                    Add(pixel, p, 0.06f, new Color(1f, 0.5f, 0.35f, 0.7f), 0, 0.7f).v = (c - p) * 1.4f + new Vector3(-Mathf.Sin(a), Mathf.Cos(a) * (float)SweepSim.Tilt, 0) * rr * 1.2f;
                }
            }
            if (sim.GustOn && Random.value < 0.8f)
            {   // 돌풍 — 쏠리는 쪽으로 흐르는 옅은 줄기
                float ga = (float)sim.GustA, rr = (float)(sim.Bo / PxPerUnit) * Random.Range(0.6f, 1.05f), a = ga + Random.Range(-1.6f, -0.3f);
                var p = PxToWorld(SweepSim.EX + Mathf.Cos(a) * rr * PxPerUnit, SweepSim.EY + Mathf.Sin(a) * rr * PxPerUnit * (float)SweepSim.Tilt);
                var tgt = PxToWorld(SweepSim.EX + Mathf.Cos(ga) * rr * PxPerUnit, SweepSim.EY + Mathf.Sin(ga) * rr * PxPerUnit * (float)SweepSim.Tilt);
                var st = Add(pixel, p, 0.05f, new Color(0.7f, 0.85f, 1f, 0.45f), 0, 0.5f); st.v = (tgt - p).normalized * 3.2f;
            }
            var cm = sim.Comet;
            if (cm != null)
            {   // 혜성 꼬리 — 지나온 쪽으로 청백 알갱이
                var p = PxToWorld(cm.x, cm.y); var back = -new Vector3((float)cm.vx, -(float)cm.vy, 0).normalized;
                for (int q = 0; q < 3; q++) Add(pixel, p + back * Random.Range(0f, 0.25f) + (Vector3)(Random.insideUnitCircle * 0.08f), Random.Range(0.06f, 0.11f), Color.Lerp(new Color(0.75f, 0.92f, 1f, 0.9f), Color.white, Random.value), 0, 0.6f).v = back * Random.Range(1.5f, 3f);
                if (Random.value < 0.3f) Add(glow, p, 0.5f, new Color(0.6f, 0.85f, 1f, 0.4f), 7, 0.2f);
            }
        }
        class Missile { public SpriteRenderer sr; public Vector3 p0, p1, p2; public float t, delay, dur, smokeT; public bool small, noSmoke; }
        // ⚡ 번개 차례로 튀기 · 🔫 포구 위치 (09-26 무기별 발사 방식 · 시안 https://claude.ai/artifact/Y679kvU53W1S3pprANSqgg)
        class LateBolt { public Vector3 p0, p1; public Color c; public float delay; public int hop; }
        readonly List<LateBolt> lateBolts = new List<LateBolt>();
        // 🛰 소품 도트 (09-26 사장님 「도트 필요한 부분 ㄱㄱ」) — 보급 캡슐 둘 · 관광 셔틀 · 운석 돌 (픽셀랩), 기뢰는 코드 그림
        static Sprite podFuelSpr, podRareSpr, touristSpr, meteorSpr; float podRingT;
        static Sprite Prop(ref Sprite s, string n) { if (s == null) s = Resources.Load<Sprite>("ship/" + n); return s; }
        class Meteor { public SpriteRenderer sr; public Vector3 a, b; public float t; }
        readonly List<Meteor> meteors = new List<Meteor>();
        readonly Dictionary<object, Vector3> mineFrom = new Dictionary<object, Vector3>();
        float lastLaserCharge, lastIceShot, lastVacLine;
        Vector3 MuzzleOf(int w) { var wb = WTurretPos(w); return TAlong(wb, WTurretAng(w, wb), TurW[w] * 0.8f); }
        void Shot(Vector3 a, Vector3 b, Color c, float size, float delay, float dur) { var sr = Make(pixel, a, size, c, 63); sr.enabled = false; missiles.Add(new Missile { sr = sr, p0 = a, p1 = (a + b) / 2, p2 = b, delay = delay, dur = dur, small = true, noSmoke = true }); }
        void DrawBolt(LateBolt b)
        {
            var mid = (b.p0 + b.p1) / 2 + (Vector3)(Random.insideUnitCircle * 0.25f);
            foreach (var seg in new[] { (b.p0, mid), (mid, b.p1) })
            {
                var gl = Add(pixel, seg.Item1, 0.05f, new Color(0.45f, 0.65f, 1f, 0.45f), 8, 0.22f); gl.a = seg.Item1; gl.b = seg.Item2; gl.size = 0.22f;
                var co = Add(pixel, seg.Item1, 0.05f, b.c, 8, 0.18f); co.a = seg.Item1; co.b = seg.Item2; co.size = 0.06f;
            }
            Add(glow, b.p1, 0.6f, new Color(0.6f, 0.8f, 1f, 0.7f), 7, 0.2f);
            Add(pixel, b.p1, 0.1f, Color.white, 0, 0.14f);                                   // 맞은 자리 흰 점
            OrbitSfx.PlayPitch("tick", 0.35f, 1.6f + b.hop * 0.12f);
        }
        readonly List<Missile> missiles = new List<Missile>();
        public string TestFxSizes() { var sb = new System.Text.StringBuilder("ortho=" + cam.orthographicSize + " frameFx=" + frameFx.Count + " :"); foreach (var f in frameFx) if (f.sr != null) sb.Append(" " + f.sr.sprite.name + "@" + f.sr.bounds.size.x.ToString("0.00")); return sb.ToString(); }   // 에디터 시험용
        public Vector3 TestMissileScreen() { foreach (var m in missiles) if (!m.small && m.sr != null && m.sr.enabled) return cam.WorldToScreenPoint(m.sr.transform.position); return new Vector3(-1, -1, 0); }   // 에디터 시험용
        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, float u) => (1 - u) * (1 - u) * a + 2 * (1 - u) * u * b + u * u * c;
        void UpdateMissiles(float dt)
        {
            for (int i = meteors.Count - 1; i >= 0; i--)
            {   // ☄ 운석 — 빙글 돌며 불꽃 꼬리를 달고 날아와 떨어지는 순간 터진다
                var m = meteors[i]; if (m.sr == null) { meteors.RemoveAt(i); continue; }
                m.t += dt; float u = m.t / 0.28f;
                if (u >= 1)
                {
                    Destroy(m.sr.gameObject); meteors.RemoveAt(i);
                    Add(glow, m.b, 0.9f, new Color(1f, 0.55f, 0.2f, 0.6f), 7, 0.5f); Burst(m.b, Orange, 10, 4f);
                    shake = Mathf.Max(shake, 0.25f); flash = Mathf.Max(flash, 0.2f);
                    continue;
                }
                var pos = Vector3.Lerp(m.a, m.b, u * u); m.sr.transform.position = pos; m.sr.transform.Rotate(0, 0, 540f * dt);
                Add(pixel, pos, 0.1f, new Color(1f, 0.55f + 0.3f * Random.value, 0.2f, 0.9f), 0, 0.3f).v = (m.a - m.b).normalized * 0.8f + (Vector3)(Random.insideUnitCircle * 0.3f);
            }
            for (int i = lateBolts.Count - 1; i >= 0; i--) { var b = lateBolts[i]; b.delay -= dt; if (b.delay <= 0) { DrawBolt(b); lateBolts.RemoveAt(i); } }
            for (int i = missiles.Count - 1; i >= 0; i--)
            {
                var m = missiles[i];
                if (m.sr == null) { missiles.RemoveAt(i); continue; }
                m.t += dt; float lt = m.t - m.delay;
                if (lt < 0) continue;
                float u = Mathf.Clamp01(lt / m.dur);
                if (u >= 1) { Destroy(m.sr.gameObject); missiles.RemoveAt(i); continue; }
                m.sr.enabled = true;
                float ev = m.small ? u : u * u * (3 - 2 * u) * 0.4f + u * 0.6f;              // 처음엔 튀어 나가고 끝에 빨라진다
                Vector3 p = Bez(m.p0, m.p1, m.p2, ev), q = Bez(m.p0, m.p1, m.p2, Mathf.Min(1, ev + 0.02f));
                m.sr.transform.position = p;
                m.smokeT -= dt;
                if (!m.small)
                {
                    var d = q - p; m.sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);   // 머리가 날아가는 쪽
                    m.sr.sprite = OrbitFxArt.Missile[(int)(m.t * 30) % 2];
                    if (m.smokeT <= 0) { m.smokeT = 0.018f; Add(pixel, p - d.normalized * 0.3f, 0.09f, new Color(0.7f, 0.66f, 0.62f, 0.7f), 0, 0.45f).v = new Vector3(0, 0.12f, 0); }   // 연기
                }
                else if (!m.noSmoke && m.smokeT <= 0) { m.smokeT = 0.04f; Add(pixel, p, 0.035f, new Color(0.63f, 0.59f, 0.55f, 0.45f), 0, 0.22f); }
            }
        }
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
                var wp = TAlong(wb, wa, TurW[curW] * 0.8f); Add(glow, wp, 0.25f * cam.orthographicSize / 6f, WeaponCol(curW), 7, 0.1f).sr.sortingOrder = 150; return wp;
            }
            int w = sim.Weapon, n = sim.MountCount(w); turFire = (turFire + 1) % n; recoil[turFire] = 1;
            sim.MountPx(w, turFire, out _, out _, out var tx, out var ty);
            var p = PxToWorld(tx, ty); Add(glow, p, 0.25f * cam.orthographicSize / 6f, WeaponCol(w), 7, 0.1f).sr.sortingOrder = 150; return p;
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
                var ts = w == 0 && sim.Ship > 0 ? ShipTur(sim.Ship) : TurSprite(w);     // 🚀 배마다 포탑 (09-26 — 빔 포탑이 먼저 불려 와 안 바뀌던 것)
                if (ts != null)                                                            // 🔫 픽셀랩 포대 그림 (위를 보는 그림 → -90°)
                {
                    const float up = Mathf.PI / 2;
                    if (w == 2) { TSprite(ts, B(0), TurW[2], 0, 2); TDisc(B(0), 26 + 6 * pul, new Color(c.r, c.g, c.b, 0.55f), 4, glow); }
                    else if (w == 6)
                    {
                        var pc = TW(651, 641); var d6 = turAim - pc; TSprite(ts, pc, TurW[6], Mathf.Atan2(d6.y, d6.x) - up, 2);
                        for (int i = 0; i < n; i++) if (recoil[i] > 0) TDisc(B(i), 14, new Color(c.r, c.g, c.b, recoil[i]), 4, glow);
                    }
                    else for (int i = 0; i < n; i++) TSprite(ts, TAlong(B(i), A(i), -recoil[i] * 6), w == 0 && sim.Ship > 0 ? 54 : TurW[w], A(i) - up, 2);
                    if (w == 8) { float ch = Mathf.Clamp01(1f - (float)sim.R.next / 1.2f); if (ch > 0.5f) TDisc(TAlong(B(0), A(0), 108), 10 + 24 * ch, new Color(0.75f, 0.9f, 1f, ch * 0.8f), 4, glow); }
                }
                else switch (w)
                {
                    case 0: { var b = B(0); float a = A(0);
                        if (turClawSpr == null || turClawShip != sim.Ship) { turClawShip = sim.Ship; turClawSpr = Resources.Load<Sprite>(sim.Ship == 1 ? "ship/turret_scatter" : "ship/turret_claw") ?? Resources.Load<Sprite>("ship/turret_claw"); }   // 🚀 배마다 포탑
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
        static readonly float[] WSlot = { -70, 70, -140, 140, -210, -280, -350, -420 };   // 96 간격이면 무기 8개 중 셋이 화면 왼쪽 밖 · 오른쪽 액정 뒤였다 (09-26 사장님 「무기들이 다 안 보여」) — 왼쪽 여섯 · 오른쪽 둘, 액정 앞까지
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
            UpdateMissiles(dt);
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
                p.sr.enabled = p.age >= 0;                                          // 늦게 나타나는 것 (산탄이 닿는 자리) — 나이가 음수인 동안 숨긴다
                float k = Mathf.Max(0, p.age) / p.life;
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
                    case 9:      // 산탄 알 — 포구에서 닿는 자리까지 짧은 굵은 선이 날아간다
                    {
                        float u = Mathf.Clamp01(p.age / Mathf.Max(0.01f, p.life - 0.05f)), h = Mathf.Min(1, u), tl = Mathf.Clamp01(u - 0.4f);
                        Vector3 ha = Vector3.Lerp(p.a, p.b, h), ta = Vector3.Lerp(p.a, p.b, tl), d9 = ha - ta;
                        tr.position = (ha + ta) / 2;
                        tr.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d9.y, d9.x) * Mathf.Rad2Deg);
                        tr.localScale = new Vector3(Mathf.Max(0.02f, d9.magnitude) / pixel.bounds.size.x, p.size / pixel.bounds.size.y, 1);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, u >= 1 ? 1 - Mathf.Clamp01((p.age - (p.life - 0.05f)) / 0.05f) : 1);
                        break;
                    }
                    case 10:     // 탄피 — 튀어 올랐다 떨어지며 돈다
                        p.v += new Vector3(0, -14f, 0) * dt; tr.position += p.v * dt; tr.rotation = Quaternion.Euler(0, 0, p.age * 900f);
                        tr.localScale = new Vector3(0.05f / pixel.bounds.size.x, 0.11f / pixel.bounds.size.y, 1);
                        p.sr.color = new Color(p.c.r, p.c.g, p.c.b, 1 - k * k);
                        break;
                }
                if (dead) { p.sr.gameObject.SetActive(false); pool.Push(p.sr); fx.RemoveAt(i); }
            }
            for (int i = pops.Count - 1; i >= 0; i--) { pops[i].age += dt; pops[i].px.y -= 30 * dt; if (pops[i].age > 1f) pops.RemoveAt(i); }
            for (int i = notices.Count - 1; i >= 0; i--) { notices[i].t += Time.unscaledDeltaTime; if (notices[i].t > 2.8f) notices.RemoveAt(i); }
        }

    }
}
