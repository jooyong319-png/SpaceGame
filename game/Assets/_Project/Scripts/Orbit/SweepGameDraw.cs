using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🪐 궤도 그리기 — 행성 · 잔해 · 도구 (09-28 나눔)
    public partial class SweepGame
    {
        // ───────────────────────────────── 궤도 · 지구 (궤도마다 카메라가 물러난다 §1-5)

        // 🪐 행성마다 크기 · 대기 빛 · 띠 색 (지구 · 달 · 화성 · 목성 · 토성)
        static readonly float[] PlanetR = { 118, 80, 96, 150, 104, 64, 130, 125, 58, 70, 110, 96 };   // 9 오르트 · 10 태양권 계면 · 11 성간 (09-27)   // 5 소행성대(세레스) · 6 천왕성 · 7 해왕성 · 8 카이퍼(명왕성)
        static readonly Color[] AtmoCol = { new Color(0.35f, 0.6f, 1f, 0.35f), new Color(0.8f, 0.8f, 0.85f, 0.06f), new Color(1f, 0.5f, 0.35f, 0.18f), new Color(1f, 0.8f, 0.55f, 0.2f), new Color(1f, 0.9f, 0.6f, 0.16f) , new Color(0.7f, 0.65f, 0.6f, 0.05f), new Color(0.55f, 0.9f, 0.95f, 0.3f), new Color(0.3f, 0.45f, 1f, 0.35f), new Color(0.85f, 0.8f, 0.75f, 0.05f), new Color(0.75f, 0.9f, 1f, 0.12f), new Color(1f, 0.45f, 0.8f, 0.35f), new Color(0.55f, 0.4f, 1f, 0.2f) };
        static readonly Color[] BandCol = { new Color(0.43f, 0.55f, 0.78f), new Color(0.6f, 0.6f, 0.66f), new Color(0.8f, 0.45f, 0.35f), new Color(0.8f, 0.62f, 0.42f), new Color(0.85f, 0.75f, 0.5f) , new Color(0.62f, 0.55f, 0.48f), new Color(0.5f, 0.78f, 0.82f), new Color(0.38f, 0.5f, 0.9f), new Color(0.7f, 0.66f, 0.62f), new Color(0.72f, 0.85f, 0.95f), new Color(0.9f, 0.45f, 0.7f), new Color(0.45f, 0.38f, 0.75f) };
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
                var px = d.sig == SweepSim.GateSig ? GatePx(sim.Frontier) : d.sp >= 0 ? SpeciesPx(d.sp) : null; if (px == null) px = JunkPx(d.k, d.id);   // 🛰 관문은 전용 그림   // 종 그림 먼저                                                // 🛰 픽셀랩 쓰레기 그림이 있으면 그걸로
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
                float size = r * 2.6f / PxPerUnit * (d.hit > 0 ? 1.25f : 1f) * (px != null ? 1.3f : d.k == SweepSim.Fuel ? 0.6f : 1f) * (d.sig == 2 ? 1.9f : d.sig == 3 ? 1.5f : d.sig == 6 ? 1.5f : d.sig == 9 ? 0.7f : d.sig == SweepSim.GateSig ? 1.7f : 1f);   // 🪐 월면 금고 · 탐사차 · 얼음 덩이는 크게, 혜성 머리는 작게 · 🛰 관문은 큰 잔해의 세 배 남짓   // 그림은 둘레가 비어 있어 1.3배
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
                    var pspr = p.kind == 1 ? Prop(ref podRareSpr, "pod_rare") : Prop(ref podFuelSpr, "pod_fuel");   // 📦 보급 캡슐 (픽셀랩) — 초록 연료 · 보라 특수
                    var pp = PxToWorld(p.x, p.y);
                    pv.transform.position = pp;
                    var to = PxToWorld(aimPx.x, aimPx.y) - pp;
                    if (pspr != null)
                    {
                        pv.sprite = pspr; pv.color = Color.white;
                        pv.transform.localScale = Vector3.one * 0.75f / FullW(pspr);   // 09-27 0.5 → 0.75 (잔해 사이에서 안 보였다)
                        pv.transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 3f + pn) * 12f);   // 살짝 흔들 — 방향대로 돌리면 눕거나 뒤집혔다
                    }
                    else
                    {
                        pv.color = p.kind == 1 ? Violet : Green;
                        pv.transform.localScale = new Vector3(0.16f / square.bounds.size.x, 0.28f / square.bounds.size.y, 1);
                        pv.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90);
                    }
                    if (Random.value < 0.7f) Add(pixel, pp - to.normalized * 0.15f, 0.07f, p.kind == 1 ? Violet : Green, 0, 0.4f);   // 꼬리
                    { float pul = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f); var pc = p.kind == 1 ? Violet : Green;   // 📦 09-27 보급 — 숨 쉬는 빛 + 퍼지는 고리 (잔해 사이에서 보이게)
                      Add(glow, pp, 1.1f + 0.3f * pul, new Color(pc.r, pc.g, pc.b, 0.28f + 0.2f * pul), 7, 0.03f);
                      if (Time.time - podRingT > 0.35f) { podRingT = Time.time; Add(ring, pp, 0.3f, new Color(pc.r, pc.g, pc.b, 0.7f), 5, 0.45f, 1.4f); } }
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
                if (i >= mineViews.Count) mineViews.Add(Make(OrbitFxArt.Mine[0], Vector3.zero, 0.34f, Color.white, 64));
                var mv = mineViews[i]; bool on = !R.over && i < R.mines.Count; mv.enabled = on; if (!on) continue;
                var m = R.mines[i]; var mp = PxToWorld(m.x, m.y);
                if (!mineFrom.ContainsKey(m)) { if (mineFrom.Count > 64) mineFrom.Clear(); mineFrom[m] = sim.WeaponOwned(4) ? MuzzleOf(4) : mp; }
                float age = (float)(0.4 - m.t);                                            // 💣 깔린 뒤 0.3초 동안 포구에서 포물선으로 날아간다 (켜지기 0.4초 전)
                if (m.t > 0 && age < 0.3f) { float u = age / 0.3f; var fr = mineFrom[m]; mp = Vector3.Lerp(fr, mp, u) + new Vector3(0, Mathf.Sin(u * Mathf.PI) * 1.1f, 0); }
                mv.transform.position = mp;
                var mf = OrbitFxArt.Mine; mv.sprite = mf[m.t > 0 || Mathf.Sin(Time.time * 10 + i) < 0 ? 1 : 0];   // 💣 켜지면 빨간 불이 깜빡
                mv.color = Color.white; mv.transform.localScale = Vector3.one * 0.34f / FullW(mv.sprite);
            }
            TraitView(R);                                                           // 🪐 행성 특성 — 대적점 · 돌풍 · 혜성 꼬리
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
                    holeAnim.transform.localScale = Vector3.one * holeCore.transform.lossyScale.x * disc.bounds.size.x * 2.6f / FullW(animHole[0]); holeAnim.color = new Color(1, 1, 1, 0.8f);   // 3.4 → 2.5 (정돈 3)
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
                holeRing.transform.position = hp; holeRing.transform.localScale = Vector3.one * ((float)sim.PullR + 10f * Mathf.Sin(Time.time * 5f)) * 2 / PxPerUnit / ring.bounds.size.x;   // 🌀 화면 전체를 빨아들인다 — 원은 크게 (09-26 사장님 「커져도 돼」)
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

    }
}
