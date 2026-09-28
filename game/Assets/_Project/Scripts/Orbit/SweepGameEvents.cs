using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 💥 사건 받기 · 효과 — 규칙이 낸 사건(SwEv)을 소리 · 빛 · 글자로 (09-28 SweepGame.cs 에서 나눔)
    public partial class SweepGame
    {
        // ───────────────────────────────── 사건 → 연출 · 소리

        void Consume()
        {
            while (sim.Events.Count > 0)
            {
                var e = sim.Events.Dequeue();
                if ((e.kind == SwEv.Laser || e.kind == SwEv.Vac || e.kind == SwEv.Shell && e.k == 0 || e.kind == SwEv.Rail || e.kind == SwEv.Bolt) && sim.R != null && !sim.R.over
                    && (e.kind != SwEv.Bolt || e.v < 0.5)                                     // 번개는 첫 줄기만 — 튀는 줄기까지 포구에서 뻗으면 화면을 가르는 선이 됐다 (09-25)
                    && (curW > 0 || System.Math.Abs(e.x - sim.ShipX) < 2 && System.Math.Abs(e.y - sim.ShipY) < 2))   // 무기 포대가 쏘는 중이면 늘 그 포구에서
                {
                    var mw = PxToWorld(e.x, e.y); var nt = ShotFrom(); e.x = 480 + nt.x * PxPerUnit; e.y = 310 - nt.y * PxPerUnit;
                    if ((e.kind == SwEv.Laser && (e.k & 64) == 0) || e.kind == SwEv.Rail) { e.x2 += e.x - (480 + mw.x * PxPerUnit); e.y2 += e.y - (310 - mw.y * PxPerUnit); }   // 64 = 조준점에서 멈추는 빔
                }
                var at = PxToWorld(e.x, e.y);
                switch (e.kind)
                {
                    case SwEv.SkillReady: OrbitSfx.Play("tick", 0.5f, 1.4f, 0.05f); if (e.k == 1) Note(Loc.T("전탄 발사 준비 — Space"), Amber); break;
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
                        if (sim.Ship == 1) { ScatterFx(at, (float)e.v, e.k == 1); break; }   // 🚀 산탄선 (09-26)
                        if (sim.Ship == 2) { HarpoonFx(PxToWorld(e.x2, e.y2), (int)e.v); break; }   // 🚀 작살선
                        if (sim.Ship == 3) { TeslaFx(); break; }                                   // 🚀 전격선
                        if (sim.Ship == 4) { if (e.k == 1) MissileLaunchFx(); break; }             // 🚀 미사일선
                        if (sim.Ship == 5) { if (e.k == 1) OrbitSfx.Play("tick", 0.3f, 0.06f); break; }   // ❄ 냉동선 — 그림은 냉동 빔(Laser) 것을 쓴다
                        if (sim.Ship == 6) break;                                                  // 🎆 분열탄선 — 그림은 분열탄(Shell) 것을 쓴다
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
                            Add(glow, at, 0.6f * cam.orthographicSize / 6f, new Color(1f, 0.93f, 0.75f, 0.9f), 7, 0.09f).sr.sortingOrder = 120;
                            Burst(at, Color.Lerp(JunkColor(k), Color.white, 0.3f), 6, 4f);
                            if (brokeWinN == 1 && Time.time - brokeStopT > 0.5f && (sim.R == null || sim.R.volleyT <= 0)) { brokeStopT = Time.time; hitStop = Mathf.Max(hitStop, 0.015f); }   // 🐢 09-26 사장님 「해왕성 · 전탄 발사 랙」 — 멈칫이 연달아 걸려 끊겨 보였다 → 0.5초에 한 번 · 절반 · 전탄 중엔 없음
                            shake = Mathf.Max(shake, 0.07f);
                            OrbitSfx.Play("clank", 0.55f, 0.08f, 0.02f);
                        }
                        Burst(at, JunkColor(k), k == SweepSim.Big ? 30 : k == SweepSim.Vault ? 10 : k == SweepSim.Chip ? 1 : 3, k == SweepSim.Big ? 6f : 3f);
                        if (att != Att.None && att != Att.Cable) Burst(at, AttColor(att), 3, 3.5f);
                        OrbitSfx.Play(k == SweepSim.Big ? "break" : k == SweepSim.Vault ? "unit" : k == SweepSim.Chip ? "pick" : "clank", k == SweepSim.Chip ? 0.3f : 0.7f, 0.03f);
                        if (k == SweepSim.Big) shake = Mathf.Max(shake, 0.2f);
                        break;
                    }
                    case SwEv.ChainPay:
                    {   // 💰 09-27 연쇄가 끝나면 모은 보너스 — 큰 글씨 + 금화가 계산대로 파파팍
                        if (e.v < 1) break;                                                  // 돈은 부술 때 이미 들어갔다 — 여기선 보여 주기만
                        if (e.k >= 5)
                        {
                            PopAt(e.x, e.y - 34, Loc.T("연쇄 ") + e.k + Loc.T(" 보너스 +") + KNum.Fmt(e.v), new Color(1f, 0.87f, 0.4f), 18 + Mathf.Min(10, e.k / 20f));
                            int coins = Mathf.Clamp(6 + e.k / 6, 6, 36);
                            for (int i = 0; i < coins && fx.Count < 880; i++) Add(disc, at + (Vector3)(Random.insideUnitCircle * 0.4f), 0.13f, Amber, 2, 1.4f + i * 0.02f).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(2f, 5f));
                            OrbitSfx.Play("buy", 0.9f); OrbitSfx.Play("coin", 0.8f, 0.05f);
                            creditPulse = 1; shake = Mathf.Max(shake, 0.05f + Mathf.Min(0.1f, e.k / 1000f));
                        }
                        break;
                    }
                    case SwEv.Coin:
                    {
                        int src = e.k % 10; bool cut = e.k >= 10;
                        Color c = src == 3 ? Green : cut ? new Color(0.9f, 0.65f, 0.6f) : Amber2;
                        if (sim.R != tallyRun) { tallyRun = sim.R; runTally = 0; }
                        if (src != 3 && e.v > 0) runTally += e.v;                                   // 값은 계산대에 모은다 — 쓰레기 위 숫자는 아주 큰 것만
                        if (e.v >= 1 && (src == 3 || e.v > sim.ValMult * 400 || brokeWinN <= 2)) CoinPop(e.x, e.y - 8, src == 3 ? Loc.T("청구서 -") : "+", e.v, c);   // 🏷 딱지 = 청구서를 깎는다 (09-27 「빚 -」라 떠서 돈이 빠지는 줄 알았다)
                        if (Random.value < 0.25f) Add(disc, at, 0.11f, src == 3 ? Green : Amber, 2, 1.6f).v = (Vector3)(Random.insideUnitCircle * 3f);
                        break;
                    }
                    case SwEv.Pop: if (e.k >= 3) Note(e.text, e.k == 3 ? Orange : e.k == 4 ? new Color(1f, 0.5f, 0.85f) : Violet); else PopAt(e.x, e.y, e.text, e.k == 1 ? Green : Amber2, 15); if (e.k == 3) OrbitSfx.Play("unit", 0.8f); if (e.k >= 4) OrbitSfx.Play("buy", 0.6f); break;
                    case SwEv.Meteor:
                    {
                        var to = at; var from = to + new Vector3(-6f, 7f, 0);
                        if (Prop(ref meteorSpr, "meteor") != null) meteors.Add(new Meteor { sr = MakeAnim(meteorSpr, from, 0.55f, Color.white, 62), a = from, b = to });
                        else { var tail = Add(pixel, from, 0.05f, new Color(1f, 0.6f, 0.2f, 0.9f), 8, 0.4f); tail.a = from; tail.b = to; tail.size = 0.25f; }
                        OrbitSfx.Play("launch", 0.7f);                                              // ☄ 빛 · 흔들림은 떨어질 때 (UpdateMissiles)
                        Note(Loc.T("운석!"), Orange);
                        break;
                    }
                    case SwEv.Tourist:
                    {
                        var ts = Prop(ref touristSpr, "tourist");
                        var p = Add(ts != null ? ts : droneArt, PxToWorld(-40, 120), ts != null ? 1.3f : 0.9f, ts != null ? Color.white : new Color(0.6f, 0.85f, 1f), 6, 4f);
                        p.sr.transform.rotation = Quaternion.Euler(0, 0, ts != null ? 0 : -90); p.sr.sortingOrder = 85;   // 🛳 관광 셔틀 (픽셀랩) p.v = new Vector3(1040f / PxPerUnit / 4f, -0.1f, 0);
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
                        if (!(spot && ice))
                        {
                            var halo = Add(pixel, s0, 0.05f, hc, 8, 0.13f); halo.a = s0; halo.b = s1; halo.size = w;
                            var core = Add(pixel, s0, 0.05f, cc, 8, 0.1f); core.a = s0; core.b = s1; core.size = Mathf.Max(0.04f, w * 0.22f);
                        }
                        if (!ice && !fence && Time.time - lastLaserCharge > 0.3f)
                        {   // 🔴 레이저 — 포구에 충전 빛이 모였다가 · 끝에서 불똥 (0.3초에 한 번 — 쬐는 동안 매번이면 시끄럽다)
                            lastLaserCharge = Time.time;
                            for (int q = 0; q < 6; q++) { var cp = s0 + (Vector3)(Random.insideUnitCircle.normalized * 0.35f); Add(pixel, cp, 0.06f, new Color(1f, 0.7f, 0.45f), 0, 0.08f).v = (s0 - cp) / 0.08f; }
                            var wide = Add(pixel, s0, 0.05f, new Color(hc.r, hc.g, hc.b, hc.a * 0.6f), 8, 0.22f); wide.a = s0; wide.b = s1; wide.size = w * 1.8f;   // 굵어졌다가 가늘어진다
                            for (int q = 0; q < 3; q++) Add(pixel, s1, 0.06f, new Color(1f, 0.82f, 0.55f), 0, 0.25f).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f));
                        }
                        if (spot && ice && Time.time - lastIceShot > 0.15f)
                        {   // ❄ 냉동 — 얼음 탄 세 발이 0.03초 간격으로 꽂힌다 (0.08초 안에 닿음 · 규칙은 즉시)
                            lastIceShot = Time.time;
                            for (int q = 0; q < 3; q++) Shot(s0, s1 + new Vector3((q - 1) * spotR * 0.35f, (q - 1) * spotR * 0.2f), new Color(0.9f, 0.97f, 1f), 0.09f, q * 0.03f, 0.08f);
                        }
                        if (spot && !ice)
                        {
                            if (!fence && Random.value < 0.5f) for (int q = 0; q < 6; q++) Add(pixel, s1 + (Vector3)(Random.insideUnitCircle.normalized * spotR), 0.06f, new Color(1f, 0.6f, 0.35f, 0.75f), 0, 0.2f);   // 🔴 09-27 태우는 원 경계 — 불똥 알갱이   // 🔴 태우는 점 — 픽셀랩 끓는 점이 조준점에서 반복 (없으면 빛 번짐)
                            LoadAnims();
                            if (animBurn != null)
                            {
                                if (burnView == null) burnView = Make(animBurn[0], s1, 1f, Color.white, 59);
                                burnView.transform.position = s1; burnView.transform.localScale = Vector3.one * spotR * 1.1f / FullW(animBurn[0]); burnView.color = new Color(1, 1, 1, 0.8f);   // 🔴 태우는 점이 레이저 범위의 2배로 커서 조준점에 큰 주황 공 — 절반 · 조금 투명 (09-26 「너무 큰 게」) burnT = 0.18f;
                            }
                            else { Add(glow, s1, spotR * 2.6f, new Color(1f, 0.35f, 0.25f, 0.55f), 7, 0.12f); Add(glow, s1, spotR * 1.1f, new Color(1f, 0.95f, 0.85f, 0.9f), 7, 0.09f); }
                            if (Random.value < 0.5f) Add(pixel, s1, 0.07f, new Color(1f, 0.7f, 0.4f), 0, 0.3f).v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f));
                        }
                        if (spot && ice)
                        {   // ❄ 서리 원 — 픽셀랩 얼음 폭발 (0.25초마다 한 번 · 없으면 원 + 서리)
                            LoadAnims();
                            { if (Time.time - lastFrost > 0.25f) { lastFrost = Time.time; FrostFlakes(s1, spotR); } }   // ❄ 코드로 그린 얼음 — 고리 + 조각 넷 (09-26 · 픽셀랩 눈꽃은 과했다)
                            for (int q = 0; q < 3; q++) { var fp = s1 + (Vector3)(Random.insideUnitCircle * spotR); Add(pixel, fp, 0.08f, new Color(0.9f, 0.98f, 1f), 0, 0.5f).v = (Vector3)(Random.insideUnitCircle * 0.6f); }
                            for (int q = 0; q < 10; q++) { var ep = s1 + (Vector3)(Random.insideUnitCircle.normalized * spotR); Add(pixel, ep, 0.06f, new Color(0.8f, 0.93f, 1f, 0.8f), 0, 0.3f); }   // ❄ 09-27 사장님 「범위가 표시에 비해 너무 크다」 — 눈꽃 가지 끝(그림 폭의 37.5%)이 실제 범위에 닿게 키우고, 경계에 서리 알갱이
                        }
                        if (cr && !fence) Star(s1, hc, 0.5f, 7, 0.16f);                             // 치명타 — 끝점에서 빛살
                        if (!fence && Random.value < 0.25f) OrbitSfx.PlayPitch("tick", 0.12f, ice ? 2.8f : 2.2f + Random.value * 0.3f);
                        break;
                    }
                    case SwEv.Proc:
                    {   // 🔫 확률 효과 발동 — 무기 이름이 조준점 위에 잠깐 · 포대가 그 색으로 번쩍
                        int pw = (int)e.v; var pc = WeaponCol(pw);
                        curW = pw; if (pw > 0 && pw < 9) { wTgt[pw] = PxToWorld(e.x, e.y + (e.k == 0 ? 26 : 20)); wRec[pw] = 1; }
                        if (e.k == 0) { Add(glow, ShotFrom(), 0.3f * cam.orthographicSize / 6f, pc, 7, 0.18f).sr.sortingOrder = 150; }
                        break;
                    }
                    case SwEv.TraitFx:
                        if (e.k == 2) { Burst(at, new Color(1f, 0.85f, 0.35f), 22, 6f); RingFx(at, new Color(1f, 0.85f, 0.35f), 0.35f, (float)e.v * 2 / PxPerUnit); OrbitSfx.Play("coin", 0.8f); shake = Mathf.Max(shake, 0.12f); }   // 🌕 월면 금고 — 금화
                        else if (e.k == 6) { var ff = OrbitFxArt.Frost; var fr = Make(ff[0], at, (float)e.v * 2 / PxPerUnit, Color.white, 58); frameFx.Add(new FrameFx { sr = fr, f = ff, fps = 18 }); OrbitSfx.PlayPitch("tick", 0.4f, 2.6f); }   // 🧊 고리 얼음 — 둘레가 언다
                        else if (e.k == 8) OrbitSfx.PlayPitch("launch", 0.35f, 0.6f);        // 🌬 돌풍
                        break;
                    case SwEv.Volley:
                    {   // 🚀 전탄 발사 — 멈칫 · 번쩍 · 흔들림 · 큰 글자
                        hitStop = Mathf.Max(hitStop, 0.08f); flash = Mathf.Max(flash, 0.55f); shake = Mathf.Max(shake, 0.35f);   // 멈춤 0.22 → 0.08 (09-26 「전탄 발사 랙」)
                        if (hud != null) hud.Big(Loc.T("전탄 발사!"), 1f, 34, new Color(1f, 0.87f, 0.58f));
                        OrbitSfx.Play("launch", 1f); OrbitSfx.Play("blast", 0.8f, 0.1f);
                        break;
                    }
                    case SwEv.Vac:
                    {   // 🌀 소용돌이 — 조준점 원 테두리 · 안으로 휘어 드는 알갱이 · 포구로 흘러가는 줄기 (09-24)
                        float R = (float)e.v / PxPerUnit; var mz = ShotFrom();
                        LoadAnims();
                        if (false && animVortex != null)                                          // 픽셀랩 소용돌이는 크게 늘면 네모 덩어리 — 점선 흡입으로 (09-26)
                        {
                            if (vacView == null) vacView = Make(animVortex[0], at, 1f, Color.white, 57);
                            vacView.transform.position = at; vacView.transform.localScale = Vector3.one * R * 1.6f / animVortex[0].bounds.size.x; vacView.color = new Color(1, 1, 1, 0.7f); vacT = 0.3f;   // 2.3 → 1.6 · 반투명 (09-26 정돈 3: 보라 덩어리가 숫자를 덮었다)
                        }
                        Add(glow, at, R * 1.2f, new Color(0.3f, 0.8f, 0.75f, 0.12f), 7, 0.12f);
                        if (Time.time - lastVacLine > 0.08f)
                        {   // 🌀 청소기 — 포구 ↔ 조준점 점선 흡입 줄기(짧게 · 흘러가는 점) · 조준점 점선 고리 · 조각이 포구로 빨려 온다
                            lastVacLine = Time.time;
                            var dv = mz - at; float L = dv.magnitude; int nd = Mathf.Min(40, Mathf.FloorToInt(L / 0.25f));
                            float ph = Mathf.Repeat(Time.time * 4f, 1f);
                            for (int q = 0; q < nd; q++) { float u = (q + ph) / Mathf.Max(1, nd); Add(pixel, at + dv * u, 0.05f, new Color(0.47f, 0.9f, 0.84f, 0.55f), 0, 0.09f); }
                            for (int q = 0; q < 10; q++) { float aa = q * 0.628f + Time.time * 5f; Add(pixel, at + new Vector3(Mathf.Cos(aa), Mathf.Sin(aa)) * R, 0.05f, new Color(0.47f, 0.9f, 0.84f, 0.6f), 0, 0.09f); }
                            for (int q = 0; q < 2; q++) { var pp = at + (Vector3)(Random.insideUnitCircle * R * 0.7f); Add(pixel, pp, 0.08f, new Color(0.85f, 0.6f, 0.29f, 0.95f), 0, 0.3f).v = (mz - pp) / 0.3f; }
                        }
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
                    {   // 🚀 분열탄 = 진짜 미사일 (09-26 사장님 「미사일처럼」 · 시안 https://claude.ai/artifact/PiacR6DKdyPgWiYXsSZsAo)
                        var s0 = PxToWorld(e.x, e.y); var s1 = PxToWorld(e.x2, e.y2);
                        if (e.k == 1)
                        {   // 자탄 — 터진 자리에서 파편 자리로 짧은 포물선
                            var bl = Make(pixel, s0, 0.09f, new Color(1f, 0.82f, 0.54f), 62); bl.enabled = false;
                            missiles.Add(new Missile { sr = bl, p0 = s0, p1 = (s0 + s1) / 2 + new Vector3(0, 0.3f, 0), p2 = s1, delay = 0.35f, dur = Mathf.Max(0.08f, (float)e.v - 0.35f), small = true });
                            break;
                        }
                        float bend = (Random.value < 0.5f ? -1 : 1) * Random.Range(0.8f, 1.8f);
                        var c1 = (s0 + s1) / 2 + new Vector3(bend, 0, 0); c1.y = Mathf.Max(s0.y, s1.y) + 1.2f;   // 위로 튀어 나갔다가 휘어 떨어진다
                        var mf = OrbitFxArt.Missile;
                        missiles.Add(new Missile { sr = Make(mf[0], s0, 0.66f, Color.white, 62), p0 = s0, p1 = c1, p2 = s1, dur = 0.35f });
                        OrbitSfx.PlayPitch("launch", 0.3f, 2.2f);
                        break;
                    }
                    case SwEv.Rail:
                    {
                        var s0 = PxToWorld(e.x, e.y); var s1 = PxToWorld(e.x2, e.y2);
                        var h = Add(pixel, s0, 0.05f, new Color(0.7f, 0.85f, 1f, 0.55f), 8, 0.35f); h.a = s0; h.b = s1; h.size = 0.32f;   // 레일건 빔 굵기 절반 (09-26)
                        var c = Add(pixel, s0, 0.05f, Color.white, 8, 0.28f); c.a = s0; c.b = s1; c.size = 0.16f;
                        Add(glow, s0, 0.6f, new Color(0.8f, 0.9f, 1f, 0.8f), 7, 0.3f);
                        Zap(s0, s1, new Color(0.6f, 0.8f, 1f), 0.22f, 0.35f, 12); Zap(s0, s1, new Color(0.6f, 0.8f, 1f), 0.18f, 0.25f, 12);
                        for (int q = 0; q < 8; q++) { var cp = s0 + (Vector3)(Random.insideUnitCircle.normalized * 0.5f); Add(pixel, cp, 0.06f, new Color(0.75f, 0.88f, 1f), 0, 0.08f).v = (s0 - cp) / 0.08f; }   // ⚡ 전기가 포구로 모인다
                        { var dl = s1 - s0; var nl = new Vector3(-dl.y, dl.x).normalized; for (int q = 0; q < 16; q++) Add(pixel, s0 + dl * (q / 16f) + nl * Random.Range(-0.08f, 0.08f), 0.05f, new Color(0.75f, 0.88f, 1f, 0.7f), 0, 0.5f); }   // 줄 따라 잔상
                        shake = Mathf.Max(shake, 0.2f); flash = Mathf.Max(flash, 0.12f);
                        OrbitSfx.PlayPitch("launch", 0.55f, 1.6f);
                        if (e.v >= 5) PopAt(e.x2 * 0.3 + e.x * 0.7, e.y2 * 0.3 + e.y * 0.7 - 20, (int)e.v + Loc.T("개 관통!"), Cyan, 18);
                        break;
                    }
                    case SwEv.Bolt:
                    {
                        var c = e.k == 1 ? new Color(1f, 1f, 0.75f) : new Color(0.7f, 0.85f, 1f);
                        var lb = new LateBolt { p0 = PxToWorld(e.x, e.y), p1 = PxToWorld(e.x2, e.y2), c = c, hop = (int)e.v, delay = (float)e.v * 0.03f };   // ⚡ 한 칸씩 차례로 튄다
                        if (lb.delay <= 0) DrawBolt(lb); else lateBolts.Add(lb);
                        break;
                    }
                    case SwEv.Beam: { var p = Add(pixel, at, 0.05f, Cyan, 3, 0.16f); p.a = at; p.b = PxToWorld(e.x2, e.y2); break; }
                    case SwEv.Ring:
                        if (e.k == 4) { Add(glow, at, 0.5f, new Color(1f, 0.6f, 0.3f, 0.85f), 7, 0.12f); Burst(at, new Color(1f, 0.7f, 0.4f), 5, 3f); OrbitSfx.Play("tick", 0.35f, 0.08f); break; }   // 🚀 미사일이 닿았다
                        if (e.k == 3)
                        {   // ⚡ 전격선 구체가 터진다 — 파란 고리 · 불똥 · 번개 가시
                            var cy = new Color(0.55f, 0.88f, 1f); float rw = (float)e.v / PxPerUnit;
                            RingFx(at, cy, 0.3f, rw * 2); Add(glow, at, rw * 1.6f, new Color(0.6f, 0.9f, 1f, 0.7f), 7, 0.16f); Burst(at, cy, 10, 4f);
                            for (int zi = 0; zi < 5; zi++) Zap(at, at + (Vector3)(Random.insideUnitCircle.normalized * rw), cy, 0.12f, 0.15f, 5);
                            OrbitSfx.Play("blast", 0.45f, 0.08f); shake = Mathf.Max(shake, 0.06f);
                            break;
                        }
                        LoadAnims();
                        if (e.k == 2 && sim.WeaponOwned(7)) Shot(MuzzleOf(7), at, new Color(0.78f, 0.66f, 1f), 0.16f, 0, 0.08f);   // 🧲 보라 구슬이 쏜살같이
                        if (e.k == 2) { var mf = OrbitFxArt.Magnet; var mg = Make(mf[0], at, (float)e.v * 2 / 0.75f / PxPerUnit, Color.white, 58); frameFx.Add(new FrameFx { sr = mg, f = mf, fps = 22 }); }   // 🧲 코드로 그린 자석 — 조여드는 고리 둘 (09-26)   // 🧲 픽셀랩 자석 — 조여든다
                        else RingFx(at, e.k == 1 ? Red : e.k == 2 ? Mag : Orange, 0.45f, (float)e.v * 2 / PxPerUnit);
                        break;
                    case SwEv.Blast:
                        RingFx(at, Orange, 0.4f, (float)e.v * 2 / PxPerUnit);
                        if (e.k == 2) Fireball(at, (float)e.v / PxPerUnit * 1.6f, 99f);   // 💥 빨간 폭발 — 하나만 (09-27 여러 개로 채우니 너무 요란했다)
                        else if (e.k == 1) FireFill(at, (float)e.v / PxPerUnit);   // 🎆 분열탄선 포탄 · 파편 — 맞는 범위만큼 (09-27 「범위가 표시보다 크다」)
                        else FireFill(at, (float)e.v / PxPerUnit);                             // 💥 09-27 폭발 그림 = 맞는 범위 (전엔 상한에 막혀 1/3) · 크면 여러 개
                        OrbitSfx.Play("blast", 0.5f, 0.025f, 0.12f);
                        shake = Mathf.Max(shake, 0.05f);
                        break;
                    case SwEv.Tier:
                        OnTier(e.k);
                        break;
                    case SwEv.Crit: PopAt(e.x, e.y, Loc.T("치명타!"), Orange, 16); OrbitSfx.Play("blast", 0.8f, 0.1f); shake = Mathf.Max(shake, 0.1f); break;
                    case SwEv.Collapse: Note(Loc.T("붕괴! ") + (int)e.v + Loc.T("개 흩어짐"), Red); Burst(at, Red, 40, 7f); shake = 0.25f; OrbitSfx.Play("collide", 1f); break;
                    case SwEv.Release:
                        Burst(at, Violet, 12 + (int)Mathf.Min(24, (float)e.v), 6f);
                        shake = Mathf.Max(shake, Mathf.Min(0.3f, 0.08f + (float)e.v * 0.01f));
                        if (e.text != null) Note(e.text, Violet);
                        OrbitSfx.Play("break", 1f, 0.05f);
                        break;
                    case SwEv.Shatter: RingFx(at, Ice, 0.3f, 0.6f); OrbitSfx.Play("pick", 0.7f); break;
                    case SwEv.Warn: hud.Banner(e.text, e.k, 2.2f); OrbitSfx.Play("warn", 0.8f); break;
                    case SwEv.EventGo: hud.Banner(e.text, -1, 1.6f); hud.SawEvent(e.k); break;
                    case SwEv.Collector: hud.Banner(e.text, -2, 3f); CollectorShip(); OrbitSfx.Play("warn", 1f); break;
                    case SwEv.RunEnd: Save(); hud.OnRunEnd(); PlayLog(); break;
                    case SwEv.Overdue: OrbitSfx.Play("warn", 1f); hud.RadioOverdue(sim.S.bill); break;
                    case SwEv.Act: hud.ShowAct((int)e.v, e.text); flash = Mathf.Max(flash, 0.8f); shake = Mathf.Max(shake, 0.25f); OrbitSfx.Play("ending", 1f); OrbitSfx.Play("launch", 0.8f); Save(); break;
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
        // ❄ 서리 원 눈꽃 — 범위가 커지면 하나를 키우지 않고 여러 개로 채운다 (09-27 사장님 「커질수록 1개로 커지지 말고 여러 개」)
        void FrostFlakes(Vector3 c, float R)
        {
            var ff = OrbitFxArt.Frost; const float Art = 96f / 36f;                    // 눈꽃 가지 끝 = 그림 폭의 37.5%
            float rb = Mathf.Min(R, 0.5f);                                            // 눈꽃 하나가 덮는 반지름 (월드)
            void Flake(Vector3 at, float r) { var fr = Make(ff[0], at, r * 2 * Art, Color.white, 58); fr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 60f)); frameFx.Add(new FrameFx { sr = fr, f = ff, fps = 22 }); }
            Flake(c, rb);
            if (R <= rb * 1.3f) return;
            float ring = R - rb * 0.8f, sr = rb * 0.8f;
            int n = Mathf.Clamp(Mathf.RoundToInt(2 * Mathf.PI * ring / (sr * 1.8f)), 3, 8);
            float a0 = Random.Range(0f, Mathf.PI * 2);
            for (int k = 0; k < n; k++) { float an = a0 + k * Mathf.PI * 2 / n; Flake(c + new Vector3(Mathf.Cos(an) * ring, Mathf.Sin(an) * ring, 0), sr); }
        }
        // 💥 폭발 — 하나를 키우지 않고 범위를 여러 개로 채운다 (서리 눈꽃과 같은 방식 · 09-27 사장님 「커질수록 여러 개」)
        void FireFill(Vector3 at, float R)
        {
            const float Fill = 0.95f;                                                  // 폭발 그림이 가장 클 때 판 폭의 95%
            float rb = Mathf.Min(R, 0.45f);
            Fireball(at, rb * 2 / Fill / 1.2f, 99f);
            if (R <= rb * 1.3f) return;
            float ring = R - rb * 0.8f, sr = rb * 0.75f;
            int n = Mathf.Clamp(Mathf.RoundToInt(2 * Mathf.PI * ring / (sr * 1.8f)), 3, 7);
            float a0 = Random.Range(0f, Mathf.PI * 2);
            for (int k = 0; k < n && fireballsThisFrame < 3; k++) { float an = a0 + k * Mathf.PI * 2 / n; Fireball(at + new Vector3(Mathf.Cos(an) * ring, Mathf.Sin(an) * ring, 0), sr * 2 / Fill / 1.2f, 99f); }
        }
        void Fireball(Vector3 at, float R, float cap = 0.7f)
        {
            if (fireballsThisFrame >= 3 || fx.Count > 760) { Add(glow, at, R * 1.0f, new Color(1f, 0.6f, 0.3f, 0.22f), 7, 0.2f); return; }
            fireballsThisFrame++;
            LoadAnims();
            if (animExplode != null)
            {   // 💥 픽셀랩 폭발 9장 — 한 번 재생
                var sr = MakeAnim(animExplode[0], at, Mathf.Min(R * 1.2f, cap), new Color(1, 1, 1, 0.9f), 58);   // 판 전체 기준 · 폭발 반지름에 맞춤
                sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 4) * 90);
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

        static readonly Dictionary<int, Sprite> gatePx = new Dictionary<int, Sprite>();
        static Sprite GatePx(int n)
        {   // 🛰 관문 잔해 — 픽셀랩 Resources/gate/gate_0~7 (항로 순위)
            if (gatePx.TryGetValue(n, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("gate/gate_" + Mathf.Clamp(n, 0, 10)); gatePx[n] = s; return s;
        }
        void OnTier(int tier)
        {
            // 🔴 도파민 사다리 — 연쇄 10 · 30 · 80 · 200
            bool calm = hud != null && hud.reduceMotion;
            switch (tier)
            {
                case 1: shake = Mathf.Max(shake, 0.06f); break;
                case 2: hud?.SawChain(); if (!calm) hitStop = 0.07f; edgeGlow = Mathf.Max(edgeGlow, 0.7f); OrbitSfx.Play("collide", 0.8f); break;
                case 3: if (!calm) slowMo = 0.8f; edgeGlow = 1f; bandLit = 1f; kessT = 1.4f; kessText = Loc.T("케슬러!"); OrbitSfx.Play("cine", 1f); break;
                case 4: if (!calm) flash = 1f; bandLit = 1f; rimLit = 1f; kessT = 2.4f; kessText = Loc.T("케슬러 연쇄"); OrbitSfx.Play("ending", 0.9f); break;
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

        // 🚀 산탄선 — 포구 불꽃 · 굵은 알 여덟이 날아가 조준 원 안에 흩어진다 · 닿는 자리 불똥 · 탄피 (09-26 사장님 「기본 무기 외형 바꾸자」)
        void ScatterFx(Vector3 at, float radiusPx, bool hit)
        {
            var muzzle = sim.R != null && !sim.R.over ? ShotFrom() : new Vector3(0, camBase - cam.orthographicSize - 0.4f, 0);
            float rw = radiusPx / PxPerUnit; var col = new Color(1f, 0.8f, 0.25f); const float fly = 0.11f;
            Add(glow, muzzle, 1.1f, new Color(1f, 0.85f, 0.4f, 0.95f), 7, 0.1f);                    // 포구 불꽃
            Star(muzzle, new Color(1f, 0.9f, 0.55f), 0.7f, 8, 0.09f);
            for (int i = 0; i < 8; i++)
            {
                var p = at + (Vector3)(Random.insideUnitCircle * rw);
                var tr = Add(pixel, muzzle, 0.05f, new Color(1f, 0.95f, 0.7f, 1f), 9, fly + 0.05f); tr.a = muzzle; tr.b = p; tr.size = 0.12f;   // 날아가는 알
                var hitFx = Add(glow, p, hit ? 0.45f : 0.26f, new Color(col.r, col.g, col.b, hit ? 0.75f : 0.35f), 7, 0.16f); hitFx.age = -fly;   // 닿는 자리
                if (hit) for (int k = 0; k < 2; k++) { var sp = Add(pixel, p, 0.05f, new Color(1f, 0.85f, 0.45f), 0, 0.3f); sp.v = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f)); sp.age = -fly; }
            }
            RingFx(at, col, 0.14f, rw * 2);
            for (int k = 0; k < 2; k++)                                                              // 탄피 — 옆으로 튀어 떨어진다
            {
                float side = Random.value < 0.5f ? -1 : 1;
                var cs = Add(pixel, muzzle + new Vector3(side * 0.25f, -0.35f, 0), 0.06f, new Color(0.85f, 0.62f, 0.2f), 10, 0.8f);
                cs.v = new Vector3(side * Random.Range(1.6f, 2.6f), Random.Range(2.2f, 3.4f), 0);
            }
            if (hit) { OrbitSfx.Play("blast", 0.35f, 0.08f); shake = Mathf.Max(shake, 0.06f); }
            else OrbitSfx.Play("tick", 0.4f, 0.05f);
        }

        // 🚀 작살선 — 작살 머리가 한 줄로 날아가고 밧줄이 따라가다 감긴다 · 꿰뚫은 수만큼 탁탁 (09-26)
        void HarpoonFx(Vector3 end, int hits)
        {
            var muzzle = sim.R != null && !sim.R.over ? ShotFrom() : new Vector3(0, camBase - cam.orthographicSize - 0.4f, 0);
            var teal = new Color(0.45f, 0.95f, 0.9f);
            Add(glow, muzzle, 0.8f, new Color(0.6f, 1f, 0.95f, 0.8f), 7, 0.09f);                              // 포구 불빛
            var head = Add(pixel, muzzle, 0.05f, new Color(0.9f, 1f, 1f, 1f), 9, 0.2f); head.a = muzzle; head.b = end; head.size = 0.13f;   // 작살 머리
            var rope = Add(pixel, (muzzle + end) / 2, 0.05f, new Color(0.84f, 0.7f, 0.45f, 0.85f), 8, 0.32f); rope.a = muzzle; rope.b = end; rope.size = 0.045f;   // 밧줄
            var dir = (end - muzzle).normalized;
            for (int i = 0; i < Mathf.Min(hits, 8); i++)                                                    // 꿰뚫은 자리 — 앞에서부터 차례로
            {
                var p = muzzle + dir * ((end - muzzle).magnitude * (0.35f + 0.08f * i));
                var h = Add(glow, p, 0.34f, new Color(teal.r, teal.g, teal.b, 0.8f), 7, 0.15f); h.age = -0.04f - 0.025f * i;
            }
            if (hits > 0) { OrbitSfx.Play("clank", 0.7f, 0.05f); shake = Mathf.Max(shake, 0.04f + 0.01f * Mathf.Min(hits, 5)); }
            else OrbitSfx.Play("tick", 0.4f, 0.05f);
        }

        // 🚀 전격선 — 쏠 때 포구 번쩍 (구체는 DrawOrbs 가 매 판 그린다 · 09-27)
        void TeslaFx()
        {
            var muzzle = sim.R != null && !sim.R.over ? ShotFrom() : new Vector3(0, camBase - cam.orthographicSize - 0.4f, 0);
            Add(glow, muzzle, 0.9f, new Color(0.6f, 0.9f, 1f, 0.9f), 7, 0.12f);
            Star(muzzle, new Color(0.75f, 0.95f, 1f), 0.5f, 6, 0.1f);
            OrbitSfx.PlayPitch("launch", 0.3f, 1.8f);
        }
        // 🚀 미사일선 — 발사 연기 · 날아가는 미사일(빨간 머리 + 꼬리 연기) · 닿으면 작은 폭발 (09-27)
        void MissileLaunchFx()
        {
            var muzzle = sim.R != null && !sim.R.over ? ShotFrom() : new Vector3(0, camBase - cam.orthographicSize - 0.4f, 0);
            Add(glow, muzzle, 0.55f, new Color(1f, 0.7f, 0.45f, 0.8f), 7, 0.08f);
            for (int i = 0; i < 3; i++) { var sm = Add(glow, muzzle, 0.3f, new Color(0.7f, 0.7f, 0.75f, 0.5f), 0, 0.5f); sm.v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.6f, 0.2f), 0); }
            OrbitSfx.PlayPitch("launch", 0.18f, 2.2f);
        }
        void DrawMissiles()
        {
            if (sim.R == null) return;
            foreach (var m in sim.R.missiles)
            {
                var p = PxToWorld(m.x, m.y);
                Add(glow, p, 0.2f, new Color(1f, 0.35f, 0.3f, 0.95f), 7, 0.03f);
                Add(glow, p, 0.42f, new Color(1f, 0.55f, 0.35f, 0.35f), 7, 0.03f);
                if (Random.value < 0.6f) Add(glow, p, 0.16f, new Color(0.85f, 0.85f, 0.9f, 0.45f), 0, 0.35f);   // 꼬리 연기
            }
        }
        void DrawOrbs()
        {
            if (sim.R == null) return;
            float t = Time.time;
            foreach (var o in sim.R.orbs)
            {
                var p = PxToWorld(o.x, o.y); float pul = 0.5f + 0.5f * Mathf.Sin(t * 30 + (float)o.x);
                Add(glow, p, 0.62f + 0.12f * pul, new Color(0.45f, 0.8f, 1f, 0.55f), 7, 0.035f);
                Add(glow, p, 0.26f, new Color(0.9f, 0.98f, 1f, 0.95f), 7, 0.035f);
                if (Random.value < 0.35f) { var q = p + (Vector3)(Random.insideUnitCircle * 0.35f); Zap(p, q, new Color(0.7f, 0.9f, 1f), 0.05f, 0.08f, 3); }
            }
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
                if (q.val <= 0 || q.age > 0.45f || q.c != c || (q.px - at).sqrMagnitude > 80 * 80) continue;   // 45 → 80 · 0.35 → 0.45초 — 돈 숫자가 겹쳐 쌓였다 (09-26 밤)
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

    }
}
