using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 💸 빚 청산 연출 — 동전 줄기 · 명세서 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 💸 빚 갚기 연출 (09-24 사장님 시안 확정)
        // 돈 → 빚 명세서로 동전 줄기 (닿을 때마다 짤랑 · 숫자 도르르) → 「상환」 도장 쾅 → 다 갚으면 「완납」 + 명세서가 부서지고 번쩍 · 큰 「완납!」
        // 판 끝 자동 상환(수입 30%)은 결산 화면에서 작은 명세서로 짧게
        float rpAt = -1; double rpFrom, rpTo, rpOwed; bool rpFull, rpMini, rpStamped, rpBoomed; int rpN, rpHit;
        readonly Vector4[] rpPix = new Vector4[70];
        const float RpGap = 0.07f, RpFly = 0.42f;
        public void Repay()
        {
            double before = sim.S.debt;
            if (!sim.RepayDebt()) return;
            RepayFx(before, sim.S.debt, 0);
        }
        void RepayFx(double from, double to, float delay, bool mini = false)
        {
            rpFrom = from; rpTo = System.Math.Max(0, to); rpFull = to <= 0.5; rpMini = mini && !rpFull;
            rpAt = Time.unscaledTime + delay; rpHit = 0; rpStamped = rpBoomed = false;
            rpN = rpMini ? 6 : rpFull ? 22 : 14;
            rpOwed = 0; if (sim.S.loanLog != null) foreach (var l in sim.S.loanLog) if (l.kind == 0) rpOwed += l.amt * SweepSim.LoanMult;
            rpOwed = System.Math.Max(rpOwed, rpFrom);
            for (int i = 0; i < rpPix.Length; i++) rpPix[i] = new Vector4(Random.value, Random.value, (Random.value - 0.5f) * 420, -Random.value * 320);
        }
        void RepayOverlay()
        {
            if (rpAt < 0) return;
            float t = Time.unscaledTime - rpAt; if (t < 0) return;
            float tLast = 0.25f + (rpN - 1) * RpGap + RpFly, tStamp = tLast + 0.05f, tBoom = tStamp + 0.55f;
            float end = rpMini ? tLast + 1.3f : rpFull ? tBoom + 1.9f : tStamp + 1.3f;
            if (t > end) { rpAt = -1; return; }
            float alpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((end - t) / 0.35f);
            var sh = rpMini ? new Rect(vw / 2 - 130, 64, 260, 70) : new Rect(vw / 2 - 160, 170, 320, 170);
            Vector2 from = rpMini ? new Vector2(vw / 2, 330) : new Vector2(vw / 2, 16);
            Vector2 to = new Vector2(sh.center.x, sh.y + (rpMini ? 42 : 72));
            int arrived = 0; float lastHit = -9;
            for (int i = 0; i < rpN; i++) { float ta = 0.25f + i * RpGap + RpFly; if (t >= ta) { arrived++; lastHit = ta; } }
            while (rpHit < arrived) { rpHit++; OrbitSfx.PlayPitch("pick", 0.45f, 0.9f + rpHit * 0.035f); }
            double cur = rpFrom - (rpFrom - rpTo) * arrived / rpN;
            bool gone = rpFull && t > tBoom;
            if (!rpMini) { GUI.color = new Color(0, 0, 0, 0.5f * alpha); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); }
            if (!gone)
            {
                float jolt = t - lastHit < 0.06f ? Random.Range(-3f, 3f) : 0;
                var s = new Rect(sh.x + jolt, sh.y + jolt * 0.5f, sh.width, sh.height);
                GUI.color = new Color(0.35f, 0.07f, 0.06f, 0.75f * alpha); GUI.DrawTexture(s, white);
                GUI.color = new Color(HoloRed.r, HoloRed.g, HoloRed.b, 0.07f * alpha);
                for (float y = s.y + 2; y < s.yMax; y += 4) GUI.DrawTexture(new Rect(s.x, y, s.width, 1), white);
                Frame(s, new Color(HoloRed.r, HoloRed.g, HoloRed.b, 0.9f * alpha), 1.5f);
                GUI.color = new Color(1, 1, 1, alpha);
                Lbl(new Rect(s.x + 14, s.y + 8, s.width - 28, 20), "<size=12><b><color=#ffd0c8>" + (rpMini ? Loc.T("자동 상환 · 판 수입 30%") : Loc.T("KESSLER // 빚 명세서")) + "</color></b></size>", label);
                Lbl(new Rect(s.x + 14, s.y + 8, s.width - 28, 20), "<size=11><color=#ffb3a8>−" + KNum.Fmt(rpFrom - rpTo) + "</color></size>", cost);
                if (rpMini) Lbl(new Rect(s.x + 14, s.y + 28, s.width - 28, 34), Loc.T("<size=24><b><color=#ffffff>빚 ") + KNum.Fmt(cur) + "</color></b></size>", label);
                else
                {
                    Lbl(new Rect(s.x + 14, s.y + 34, s.width - 28, 56), "<size=42><b><color=#ffffff>" + KNum.Fmt(cur) + "</color></b></size>", label);
                    float paid = Mathf.Clamp01((float)(1 - cur / rpOwed));
                    GUI.color = new Color(HoloRed.r, HoloRed.g, HoloRed.b, 0.2f * alpha); GUI.DrawTexture(new Rect(s.x + 14, s.y + 104, s.width - 28, 9), white);
                    GUI.color = new Color(0.62f, 0.94f, 0.75f, alpha); GUI.DrawTexture(new Rect(s.x + 14, s.y + 104, (s.width - 28) * paid, 9), white);
                    GUI.color = new Color(1, 1, 1, alpha);
                    Lbl(new Rect(s.x + 14, s.y + 118, s.width - 28, 20), Loc.T("<size=11><color=#ffd0c8>갚은 비율</color></size>"), label);
                    Lbl(new Rect(s.x + 14, s.y + 118, s.width - 28, 20), "<size=11><color=#9ff0bf>" + Mathf.RoundToInt(paid * 100) + "%</color></size>", cost);
                }
                // 도장
                if (!rpMini && t > tStamp)
                {
                    if (!rpStamped) { rpStamped = true; OrbitSfx.Play(rpFull ? "buy" : "grab", 0.9f); game.shake = Mathf.Max(game.shake, 0.08f); }
                    float k = Mathf.Clamp01((t - tStamp) / 0.3f); float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);   // 튕기며 내려앉는다
                    float sc = Mathf.Lerp(3f, 1f, e);
                    var c = new Vector2(s.xMax - 64, s.y + 62); var sr = new Rect(c.x - 50 * sc, c.y - 20 * sc, 100 * sc, 40 * sc);
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(-12, c);
                    Frame(sr, new Color(1f, 0.29f, 0.23f, Mathf.Min(1, k * 2) * 0.95f * alpha), 4 * sc);
                    GUI.color = new Color(1, 1, 1, Mathf.Min(1, k * 2) * alpha);
                    Lbl(sr, "<size=" + Mathf.RoundToInt(26 * sc) + "><b><color=#ff4a3a>" + (rpFull ? Loc.T("완납") : Loc.T("상환")) + "</color></b></size>", center);
                    GUI.matrix = m;
                }
            }
            // 동전
            for (int i = 0; i < rpN; i++)
            {
                float tc = t - (0.25f + i * RpGap); if (tc < 0 || tc >= RpFly) continue;
                float k = tc / RpFly, u = 1 - k;
                float sx = from.x + ((i * 37) % 30 - 15), sy = from.y;
                float mx = (sx + to.x) / 2 + ((i * 53) % 120 - 60), my = Mathf.Min(sy, to.y) - 40;
                var p = new Vector2(u * u * sx + 2 * u * k * mx + k * k * to.x, u * u * sy + 2 * u * k * my + k * k * to.y);
                GUI.color = new Color(1f, 0.82f, 0.4f, 0.35f); GUI.DrawTexture(new Rect(p.x - 12, p.y - 12, 24, 24), texDisc);
                GUI.color = new Color(1f, 0.85f, 0.45f); GUI.DrawTexture(new Rect(p.x - 7, p.y - 7, 14, 14), texDisc);
                GUI.color = new Color(1f, 0.95f, 0.75f); GUI.DrawTexture(new Rect(p.x - 4, p.y - 5, 5, 5), texDisc);
            }
            // 완납 — 번쩍 · 명세서가 빨간 픽셀로 흩어짐 · 큰 「완납!」
            if (gone)
            {
                float tt = t - tBoom;
                if (!rpBoomed) { rpBoomed = true; OrbitSfx.Play("launch", 0.7f); game.flash = Mathf.Max(game.flash, 0.6f); game.shake = Mathf.Max(game.shake, 0.15f); }
                for (int i = 0; i < rpPix.Length; i++)
                {
                    var q = rpPix[i]; float a = 1 - tt / 1.2f; if (a <= 0) break;
                    float x = sh.x + q.x * sh.width + q.z * tt, y = sh.y + q.y * sh.height + q.w * tt + 520 * tt * tt;
                    GUI.color = new Color(1f, 0.55f, 0.48f, a); GUI.DrawTexture(new Rect(x, y, 6, 6), white);
                }
                float k = Mathf.Clamp01(tt / 0.25f), sc = tt < 0.25f ? Mathf.Lerp(0.4f, 1.15f, k) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((tt - 0.25f) / 0.2f));
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((end - t) / 0.4f));
                Lbl(new Rect(0, RefH * 0.42f - 50 * sc, vw, 100 * sc), "<size=" + Mathf.RoundToInt(64 * sc) + Loc.T("><b><color=#ffdf95>완납!</color></b></size>"), center);
                Lbl(new Rect(0, RefH * 0.42f + 48, vw, 24), Loc.T("<size=14><color=#ffe9b0>케슬러 금융에 진 빚을 다 갚았다</color></size>"), center);
            }
            GUI.color = Color.white;
        }

        string LuckLine()
        {
            int p = sim.Lv("a_auto"), c = sim.Lv("a_ins");
            return (p > 0 ? Loc.T("<color=#ffdf95>개미의 기도</color> <color=") + UpHex + Loc.T(">내 종목 ↑</color>") : Loc.T("<color=#3f4652>개미의 기도 · 잠김</color>")) + "   " +
                   (c > 0 ? Loc.T("<color=#9ff0bf>행운의 부적 ") + c + Loc.T("</color> <color=#8a9bb3>나쁜 속보 ") + new[] { 0, 60, 70, 80 }[Mathf.Min(3, c)] + Loc.T("% 피하기</color>") : Loc.T("<color=#3f4652>행운의 부적 · 잠김</color>"));
        }

    }
}
