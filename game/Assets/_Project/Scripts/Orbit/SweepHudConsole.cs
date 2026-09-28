using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🟦 조종대 위 — 청구서 · 항로 · 복권 홀로그램 · 출동 보고 · 궤도일보 전광판 · 명판 · 파산 덮개 · 막 전환 카드 (09-28 SweepHudRooms.cs 에서 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 🟦 홀로그램 청구서 — 조종대 위 발사기에서 떠오른다 (C안)
        static readonly Color Holo = new Color(0.49f, 0.91f, 1f), HoloRed = new Color(1f, 0.55f, 0.48f);
        void HoloBill(bool due)
        {
            var S = sim.S; var M = sim.M;
            float ex = ox + 175, ey = 506;
            var p = new Rect(ox + 75, 184, 200, 258);
            float t = Time.unscaledTime;
            float fl = reduceMotion ? 1 : Mathf.PerlinNoise(t * 1.7f, 0.3f) > 0.78f ? 0.5f + 0.4f * Mathf.PerlinNoise(t * 40, 1.3f) : 1f;   // 가끔 지직
            // 빛기둥
            const int N = 40; float step = (ey - p.y) / N;
            for (int i = 0; i < N; i++)
            {
                float k = i / (float)(N - 1), y = ey - 2 - step * (i + 1), bwid = Mathf.Lerp(76, p.width + 20, k);
                GUI.color = new Color(Holo.r, Holo.g, Holo.b, (0.09f - 0.06f * k) * fl);
                GUI.DrawTexture(new Rect(ex - bwid / 2, y, bwid, step + 0.5f), white);
            }
            // 발사기 (조종대 위 원반)
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(ex - 76, ey - 2, 152, 34), texDisc);
            GUI.color = new Color(0.17f, 0.23f, 0.31f); GUI.DrawTexture(new Rect(ex - 70, ey - 8, 140, 30), texDisc);
            GUI.color = new Color(0.08f, 0.28f, 0.36f); GUI.DrawTexture(new Rect(ex - 54, ey - 6, 108, 20), texDisc);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.8f * fl); GUI.DrawTexture(new Rect(ex - 36, ey - 3, 72, 12), texDisc);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.22f * fl); GUI.DrawTexture(new Rect(ex - 90, ey - 26, 180, 56), texDisc);

            // 뒤에 한 장 더 — 빚 명세서 (누르면 대출 창구)
            if (S.debt > 0 && !M.cleanReady)
            {
                var g = new Rect(p.x + 14, p.y - 24, p.width, 30);
                bool gh = g.Contains(Event.current.mousePosition);
                GUI.color = new Color(HoloRed.r, HoloRed.g, HoloRed.b, (gh ? 0.22f : 0.1f) * fl); GUI.DrawTexture(g, white);
                Frame(g, new Color(HoloRed.r, HoloRed.g, HoloRed.b, (gh ? 0.9f : 0.55f) * fl), 1);
                GUI.color = new Color(1, 1, 1, fl);
                GUI.Label(new Rect(g.x + 8, g.y + 3, g.width - 16, 22), Loc.T("<size=11><color=#ffc2b8>빚 명세서</color></size>"), label);
                GUI.Label(new Rect(g.x + 8, g.y + 3, g.width - 16, 22), "<size=12><color=#ffc2b8>" + KNum.Fmt(S.debt) + " ▸</color></size>", cost);
                GUI.color = Color.white;
                if (GUI.Button(g, GUIContent.none, GUIStyle.none)) loanOpen = true;
            }

            // 판 — 반투명 · 주사선 · 위아래로 훑는 빛 띠
            GUI.color = new Color(0.02f, 0.035f, 0.05f, 0.6f * fl); GUI.DrawTexture(p, white);          // 어두운 바탕 (09-24 글자 점검)
            GUI.color = new Color(0.1f, 0.45f, 0.58f, 0.2f * fl); GUI.DrawTexture(p, white);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.06f * fl);
            for (float y = p.y + 2; y < p.yMax; y += 4) GUI.DrawTexture(new Rect(p.x, y, p.width, 1), white);
            if (!reduceMotion) { GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.08f * fl); GUI.DrawTexture(new Rect(p.x, p.y + Mathf.Repeat(t * 50, p.height - 8), p.width, 8), white); }
            Frame(p, new Color(Holo.r, Holo.g, Holo.b, 0.8f * fl), 1.5f);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, fl);                // 모서리 꺾쇠
            foreach (var c in new[] { new Vector2(p.x, p.y), new Vector2(p.xMax - 10, p.y), new Vector2(p.x, p.yMax - 3), new Vector2(p.xMax - 10, p.yMax - 3) }) GUI.DrawTexture(new Rect(c.x, c.y, 10, 3), white);

            GUI.color = new Color(1, 1, 1, fl);
            float x = p.x + 12, w = p.width - 24, y0 = p.y + 8;
            GUI.Label(new Rect(x, y0, w, 20), Loc.T("<size=12><b><color=#bff4ff>KESSLER // 청구</color></b></size>"), label);
            if (M.endless)
            {   // ∞ 무한 궤도 — 청구서 자리에 층
                GUI.Label(new Rect(x, y0 + 26, w, 20), Loc.T("<size=12><color=#d8ccff>무한 궤도</color></size>"), label);
                GUI.Label(new Rect(x, y0 + 44, w, 44), "<size=34><b><color=#e6dcff>" + M.depth + Loc.T("층</color></b></size>"), label);
                GUI.Label(new Rect(x, y0 + 92, w, 20), Loc.T("<size=11><color=#bff4ff>최고 ") + M.bestDepth + Loc.T("층 · ★ ") + M.legend + "</color></size>", label);
                GUI.Label(new Rect(x, y0 + 116, w, 60), Loc.T("<size=11><color=#7fcfe0>판이 끝날 때마다 한 층 아래로\n층마다 체력 ×1.25 · 값 ×1.2\n5층마다 열쇠 +1</color></size>"), small);
            }
            else if (M.cleanReady)
            {
                GUI.Label(new Rect(x, y0 + 50, w, 70), Loc.T("<size=22><color=#9ff0bf>빚 청산!</color></size>\n<size=12><color=#bff4ff>청산 출동만 남았다</color></size>"), center);
            }
            else if (S.bill >= SweepSim.Bills.Length)
            {
                GUI.Label(new Rect(x, y0 + 30, w, 20), Loc.T("<size=12><color=#7fcfe0>청구서는 끝</color></size>"), label);
                GUI.Label(new Rect(x, y0 + 52, w, 20), Loc.T("<size=11><color=#7fcfe0>남은 빚</color></size>"), label);
                GUI.Label(new Rect(x, y0 + 68, w, 36), "<size=28><b><color=#ffc2b8>" + KNum.Fmt(S.debt) + "</color></b></size>", label);
                GUI.Label(new Rect(x, y0 + 110, w, 40), Loc.T("<size=11><color=#bff4ff>다 갚으면 청산 출동</color></size>"), label);
                GUI.color = Color.white;
                if (HoloBtn(new Rect(x, y0 + 196, w, 30), Loc.T("대출 창구 ▸"), Holo, true, fl)) loanOpen = true;
            }
            else
            {
                var b = SweepSim.Bills[S.bill];
                GUI.Label(new Rect(x, y0, w, 20), "<size=12><color=#7fcfe0>" + (S.bill + 1) + " / " + SweepSim.Bills.Length + "</color></size>", cost);
                GUI.Label(new Rect(x, y0 + 22, w, 22), "<size=13><color=#dff8ff>" + b.t + "</color></size>", label);
                GUI.Label(new Rect(x, y0 + 48, w, 18), Loc.T("<size=11><color=#7fcfe0>납부 금액</color></size>"), label);
                GUI.Label(new Rect(x, y0 + 62, w, 38), "<size=28><b><color=#e8fbff>" + KNum.Fmt(sim.BillAmount) + "</color></b></size>", label);
                bool blink = Mathf.Repeat(t, 0.8f) < 0.55f;
                GUI.Label(new Rect(x, y0 + 100, w, 20), due ? "<size=13><b><color=" + (blink ? "#ff9b8f" : "#b0564c") + Loc.T(">오늘 납부일</color></b></size>") : Loc.T("<size=12><color=#bff4ff>기한 ▸ ") + S.billDue + Loc.T("판</color></size>"), label);
                float prog = Mathf.Clamp01((float)(S.cash / System.Math.Max(1, sim.BillAmount)));
                GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.15f * fl); GUI.DrawTexture(new Rect(x, y0 + 126, w, 5), white);
                GUI.color = prog >= 1 ? new Color(0.62f, 0.94f, 0.75f, fl) : new Color(Holo.r, Holo.g, Holo.b, 0.9f * fl); GUI.DrawTexture(new Rect(x, y0 + 126, w * prog, 5), white);
                GUI.color = new Color(1, 1, 1, fl);
                GUI.Label(new Rect(x, y0 + 132, w, 18), "<size=10><color=#7fcfe0>" + Mathf.RoundToInt(prog * 100) + Loc.T("% 모였다</color></size>"), label);
                GUI.Label(new Rect(x, y0 + 150, w, 36), "<size=11><color=#7fcfe0>" + Clip(b.perk, 26) + "</color></size>", small);
                GUI.color = Color.white;
                bool can = S.cash >= sim.BillAmount; double need = sim.BillAmount - S.cash;
                bool loanOk = !can && sim.LoanCap > 0 && need <= sim.LoanCap;
                float bw = (w - 6) / 2;
                if (HoloBtn(new Rect(x, y0 + 190, bw, 32), Loc.T("납부"), Holo, can, fl)) sim.PayBill();
                if (HoloBtn(new Rect(x + bw + 6, y0 + 190, bw, 32), Loc.T("대출로"), HoloRed, loanOk, fl)) RequestLoan(need, true);
                GUI.color = new Color(1, 1, 1, fl);
                bool loanOv = new Rect(x + bw + 6, y0 + 190, bw, 32).Contains(Event.current.mousePosition);
                string foot = loanOk ? (loanOv ? "<color=#ffc2b8>" + KNum.Fmt(need) + Loc.T(" 빌려 납부 · 빚 +") + KNum.Fmt(need * SweepSim.LoanMult) + "</color>" : "<color=#7fcfe0>" + KNum.Fmt(need) + Loc.T(" 모자라다</color>")) : can ? Loc.T("<color=#9ff0bf>지금 낼 수 있다</color>") : Loc.T("<color=#7fcfe0>대출 한도 부족</color>");   // 빚 얼마는 [대출로]에 올렸을 때만 (정돈 11)
                GUI.Label(new Rect(x - 6, y0 + 224, w + 12, 18), "<size=10>" + foot + "</size>", center);
                GUI.color = Color.white;
            }
            GUI.color = Color.white;
            if (dueNag > 0) GUI.Label(new Rect(p.x - 40, p.y - 50, p.width + 80, 20), Loc.T("<color=#ff9b8f><size=12>납부일 — 먼저 갚거나 · 대출받거나 · 파산</size></color>"), center);
        }

        bool HoloBtn(Rect r, string s, Color c, bool on, float fl)
        {
            bool ov = on && r.Contains(Event.current.mousePosition);
            GUI.color = new Color(c.r, c.g, c.b, (on ? (ov ? 0.38f : 0.2f) : 0.04f) * fl); GUI.DrawTexture(r, white);
            Frame(r, new Color(c.r, c.g, c.b, (on ? 0.95f : 0.25f) * fl), ov ? 2 : 1);
            GUI.color = new Color(1, 1, 1, (on ? 1 : 0.35f) * fl);
            GUI.Label(r, "<size=14><b>" + s + "</b></size>", center);
            GUI.color = Color.white;
            return GUI.Button(r, GUIContent.none, GUIStyle.none) && on;
        }

        // ───────────────────────────────── 09-24 조종실 물건들 (사장님 BBA + 홀로그램 셋)
        static readonly Color HoloPink = new Color(1f, 0.66f, 0.94f), Amber3 = new Color(1f, 0.67f, 0.24f);
        float Flick(float seed) => reduceMotion ? 1 : Mathf.PerlinNoise(Time.unscaledTime * 1.7f, seed) > 0.8f ? 0.5f + 0.4f * Mathf.PerlinNoise(Time.unscaledTime * 40, seed + 1) : 1f;

        /// <summary>홀로그램 바탕 — 원반 · 빛기둥 · 반투명 판 · 주사선 · 꺾쇠</summary>
        void HoloBase(Rect p, float ex, float ey, float emW, Color hc, float fl, bool hover)
        {
            const int N = 30; float step = (ey - p.y) / N;
            for (int i = 0; i < N; i++)
            {
                float k = i / (float)(N - 1), y = ey - 2 - step * (i + 1), bwid = Mathf.Lerp(emW * 0.55f, p.width + 16, k);
                GUI.color = new Color(hc.r, hc.g, hc.b, (0.09f - 0.06f * k) * fl);
                GUI.DrawTexture(new Rect(ex - bwid / 2, y, bwid, step + 0.5f), white);
            }
            float e = emW / 140f;
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(ex - 76 * e, ey - 2, 152 * e, 34 * e), texDisc);
            GUI.color = new Color(0.17f, 0.23f, 0.31f); GUI.DrawTexture(new Rect(ex - 70 * e, ey - 8, 140 * e, 30 * e), texDisc);
            GUI.color = new Color(hc.r * 0.25f, hc.g * 0.25f, hc.b * 0.3f); GUI.DrawTexture(new Rect(ex - 54 * e, ey - 6, 108 * e, 20 * e), texDisc);
            GUI.color = new Color(hc.r, hc.g, hc.b, 0.8f * fl); GUI.DrawTexture(new Rect(ex - 36 * e, ey - 3, 72 * e, 12 * e), texDisc);
            GUI.color = new Color(hc.r, hc.g, hc.b, 0.22f * fl); GUI.DrawTexture(new Rect(ex - 90 * e, ey - 26 * e, 180 * e, 56 * e), texDisc);
            GUI.color = new Color(0.02f, 0.035f, 0.05f, 0.6f * fl); GUI.DrawTexture(p, white);          // 어두운 바탕 — 뒤가 비쳐 글자와 겹치지 않게 (09-24 글자 점검)
            GUI.color = new Color(hc.r * 0.3f, hc.g * 0.4f, hc.b * 0.5f, (hover ? 0.34f : 0.22f) * fl); GUI.DrawTexture(p, white);
            GUI.color = new Color(hc.r, hc.g, hc.b, 0.06f * fl);
            for (float y = p.y + 2; y < p.yMax; y += 4) GUI.DrawTexture(new Rect(p.x, y, p.width, 1), white);
            if (!reduceMotion) { GUI.color = new Color(hc.r, hc.g, hc.b, 0.08f * fl); GUI.DrawTexture(new Rect(p.x, p.y + Mathf.Repeat(Time.unscaledTime * 50 + p.x, p.height - 8), p.width, 8), white); }
            Frame(p, new Color(hc.r, hc.g, hc.b, (hover ? 1f : 0.8f) * fl), hover ? 2f : 1.5f);
            GUI.color = new Color(hc.r, hc.g, hc.b, fl);
            foreach (var c in new[] { new Vector2(p.x, p.y), new Vector2(p.xMax - 10, p.y), new Vector2(p.x, p.yMax - 3), new Vector2(p.xMax - 10, p.yMax - 3) }) GUI.DrawTexture(new Rect(c.x, c.y, 10, 3), white);
            GUI.color = Color.white;
        }

        // 🟦 항로 홀로그램 — 행성 다섯 빛 구슬 · 열린 곳은 누르면 간다 · 판매 중이면 허가증 (두 번)
        // 🧭 항로 — B 「한 장씩 넘기기」 (09-26 사장님 · 시안 https://claude.ai/artifact/WLid5UFWSgJxQ1LM9chNDH)
        //    행성 하나를 크게 · ◀ ▶ · 휠로 넘기면 그 궤도로 바로 정해진다. 아래 점 · 특성 칩(값 · 체력 · 폭풍 · 중력 · 고리 틈 · 부착물) · 의뢰
        int routeBrowse = -1;                                                    // 보고 있는 칸 (열린 행성 + 다음 하나 안에서) · -1 = 지금 궤도
        void RouteHolo()
        {
            var S = sim.S; var M = sim.M;
            var p = new Rect(ox + 685, 248, 200, 200);
            float fl = Flick(2.1f);
            HoloBase(p, ox + 785, 506, 140, Holo, fl, false);
            GUI.color = new Color(1, 1, 1, fl);
            var vis = new List<int>(); foreach (int oi2 in SweepSim.OrbitOrder) { vis.Add(oi2); if (!sim.Open(oi2)) break; }
            int cur = Mathf.Max(0, vis.IndexOf(S.orbit));
            if (routeBrowse < 0 || routeBrowse >= vis.Count) routeBrowse = cur;
            if (routeBrowse < vis.Count && sim.Open(vis[routeBrowse]) && vis[routeBrowse] != S.orbit) routeBrowse = cur;   // 다른 데서 궤도가 바뀌면 따라간다
            GUI.Label(new Rect(p.x + 10, p.y + 5, p.width - 20, 20), Loc.T("<size=12><b><color=#bff4ff>NAV // 항로</color></b></size>"), label);
            GUI.Label(new Rect(p.x + 10, p.y + 5, p.width - 20, 20), "<size=11><color=#7fcfe0>" + (routeBrowse + 1) + " / " + vis.Count + "</color></size>", cost);
            if (M.cleanReady)
            {
                GUI.Label(new Rect(p.x + 10, p.y + 60, p.width - 20, 40), Loc.T("<size=12><color=#bff4ff>청산 출동 — 항로 고정</color></size>"), center);
                GUI.color = Color.white; return;
            }
            void Browse(int d)
            {
                routeBrowse = (routeBrowse + d + vis.Count) % vis.Count;
                int pi = vis[routeBrowse];
                if (sim.Open(pi) && pi != S.orbit) { sim.SetOrbit(pi); permitArmed = -1; }
                OrbitSfx.Play("tick", 0.5f);
            }
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && p.Contains(ev.mousePosition)) { Browse(ev.delta.y > 0 ? 1 : -1); ev.Use(); }
            int show = vis[routeBrowse]; var so = SweepSim.Orbits[show]; bool open = sim.Open(show);
            // ◀ 큰 행성 ▶
            if (HoloBtn(new Rect(p.x + 8, p.y + 34, 24, 50), "<size=13>◀</size>", Holo, true, fl)) Browse(-1);
            if (HoloBtn(new Rect(p.xMax - 32, p.y + 34, 24, 50), "<size=13>▶</size>", Holo, true, fl)) Browse(1);
            GUI.color = new Color(1, 1, 1, fl);
            var big = new Rect(p.center.x - 30, p.y + 26, 60, 60);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.22f * fl); GUI.DrawTexture(new Rect(big.x - 8, big.y - 8, big.width + 16, big.height + 16), texDisc);
            GUI.color = open ? new Color(1, 1, 1, fl) : new Color(0.25f, 0.28f, 0.32f, 0.9f * fl); GUI.DrawTexture(big, PlanetArt.Get(show).texture);
            if (!open) { GUI.color = new Color(1, 1, 1, fl); GUI.Label(big, "<size=20><b>?</b></size>", center); }
            GUI.color = new Color(1, 1, 1, fl);
            GUI.Label(new Rect(p.x + 6, p.y + 86, p.width - 12, 18), "<size=13><b><color=#ffdf95>" + so.name + "</color></b>" + (open ? "" : Loc.T("  <color=#ffb3a8>다음</color>")) + "</size>", center);
            GUI.Label(new Rect(p.x + 6, p.y + 103, p.width - 12, 16), "<size=9><color=#7fcfe0>" + Clip(so.desc, 20) + "</color></size>", center);
            // 점 — 어디쯤인지
            float dw = 9, dx0 = p.center.x - vis.Count * dw / 2;
            for (int k = 0; k < vis.Count; k++)
            {
                var dr = new Rect(dx0 + k * dw + 1, p.y + 121, 6, 6);
                bool on = k == routeBrowse, lk = !sim.Open(vis[k]);
                if (lk) Frame(dr, new Color(Holo.r, Holo.g, Holo.b, 0.6f * fl), 1);
                else { GUI.color = on ? new Color(1f, 0.87f, 0.58f, fl) : new Color(Holo.r, Holo.g, Holo.b, 0.4f * fl); GUI.DrawTexture(dr, white); }
                GUI.color = Color.white;
                if (GUI.Button(new Rect(dr.x - 1, dr.y - 3, 9, 12), GUIContent.none, GUIStyle.none)) Browse(k - routeBrowse);
            }
            // 특성 칩
            {
                var chips = new List<(string, Color)>();
                int trt = SweepSim.TraitOf(show); if (trt > 0) chips.Add(("★ " + SweepSim.TraitName[trt], new Color(1f, 0.87f, 0.58f)));   // 🪐 행성 특성
                chips.Add((Loc.T("값 ×") + KNum.Fmt(SweepSim.PlanetMulOf(show)), new Color(0.62f, 0.94f, 0.75f)));   // 📈 행성 한 칸 = ×3
                chips.Add((Loc.T("체력 ×") + so.hp, new Color(1f, 0.7f, 0.66f)));
                if (so.storm) chips.Add((Loc.T("폭풍"), new Color(0.62f, 0.94f, 0.75f)));
                if (so.pull > 0) chips.Add((Loc.T("중력"), new Color(0.75f, 0.96f, 1f)));
                if (so.gap > 0) chips.Add((Loc.T("고리 틈"), new Color(0.75f, 0.96f, 1f)));
                chips.Add((Loc.T("부착물 ") + Mathf.RoundToInt((float)so.att * 100) + "%", new Color(0.75f, 0.96f, 1f)));
                float lineW = 0; var widths = new List<float>();
                foreach (var c in chips) { float cwd = label.CalcSize(new GUIContent("<size=9>" + c.Item1 + "</size>")).x + 8; widths.Add(cwd); lineW += cwd + 3; }
                float cx = p.center.x - Mathf.Min(lineW, p.width - 12) / 2, cy = p.y + 132, x0 = cx;
                for (int k = 0; k < chips.Count; k++)
                {
                    if (cx + widths[k] > p.xMax - 6) { cx = p.x + 8; cy += 15; }
                    var cr = new Rect(cx, cy, widths[k], 13); var c = chips[k].Item2;
                    Frame(cr, new Color(c.r, c.g, c.b, 0.6f * fl), 1);
                    GUI.color = new Color(1, 1, 1, fl); GUI.Label(new Rect(cr.x, cr.y - 2, cr.width, 16), "<size=9><color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + chips[k].Item1 + "</color></size>", center);
                    cx += widths[k] + 3;
                }
            }
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.35f * fl);
            for (float xx = p.x + 10; xx < p.xMax - 10; xx += 6) GUI.DrawTexture(new Rect(xx, p.y + 164, 3, 1), white);
            GUI.color = new Color(1, 1, 1, fl);
            // 아래 — 열린 행성이면 의뢰 · 다음 행성이면 항로 사러
            if (!open)
            {
                bool nextUp = sim.HasGate && show == SweepSim.OrbitOrder[sim.Frontier + 1];
                GUI.color = new Color(1, 1, 1, fl);
                GUI.Label(new Rect(p.x + 10, p.y + 170, p.width - 20, 22), nextUp ? "<size=11><color=#ffb36b>🛰 " + (sim.GateReady ? sim.GateName + " " + Mathf.CeilToInt((float)sim.GateLeft * 100) + Loc.T("% — 부수면 열린다") : Loc.T("청구서 ") + sim.GateBill + Loc.T("장을 갚으면 관문 방어막이 풀린다")) + "</color></size>" : Loc.T("<size=11><color=#8a93a3>앞 행성 관문부터</color></size>"), center);   // 🛰 09-26 허가증 대신 관문
            }
            else
            {
                var ct = sim.CurContract;
                if (ct != null)
                {
                    GUI.Label(new Rect(p.x + 10, p.y + 168, p.width - 64, 30), Loc.T("<size=10><color=#7fcfe0>의뢰</color> <color=#dff8ff>") + ct.Value.text + "</color>  <color=#9ff0bf>+" + (25 + 10 * sim.Lv("e_quest")) + "%</color></size>", small);
                    GUI.color = Color.white;
                    if (!S.rerolled && HoloBtn(new Rect(p.xMax - 52, p.y + 170, 44, 20), Loc.T("<size=10>바꾸기</size>"), Holo, true, fl)) sim.Reroll();
                }
                else GUI.Label(new Rect(p.x + 10, p.y + 170, p.width - 20, 24), Loc.T("<size=10><color=#7fcfe0>의뢰는 청구서 2 뒤에 들어온다</color></size>"), small);
            }
            GUI.color = Color.white;
        }

        // 🩷 복권 홀로그램 (출동 버튼 오른쪽) — 누르면 복권 창
        void LottoHolo()
        {
            var p = new Rect(ox + 570, 404, 108, 92);                            // 588 → 570 — 항로 홀로그램 왼쪽 아래를 덮었다 (09-26)
            bool ov = p.Contains(Event.current.mousePosition);
            float fl = Flick(5.3f);
            HoloBase(p, ox + 624, 524, 100, Holo, fl, ov);                                  // 분홍 → 다른 홀로그램과 같은 청록 (09-26 정돈 10)
            GUI.color = new Color(1, 1, 1, fl);
            GUI.Label(new Rect(p.x, p.y + 6, p.width, 18), "<size=11><b><color=#bff3ff>SCRATCH</color></b></size>", center);
            GUI.Label(new Rect(p.x, p.y + 26, p.width, 32), Loc.T("<size=22><b><color=#e8fbff>복권</color></b></size>"), center);
            GUI.Label(new Rect(p.x, p.y + 62, p.width, 18), Loc.T("<size=10><color=#7fcfe0>즉석 복권 ") + sim.ScratchLeft + Loc.T("장</color></size>"), center);
            GUI.color = Color.white;
            if (GUI.Button(p, GUIContent.none, GUIStyle.none)) { lottoOpen = true; OrbitSfx.Play("tick", 0.6f); }
        }

        // 📺 출동 보고 — 초록 브라운관 (B) · 최근 출동 여섯 번 벌이 막대
        void CrtReport(Rect r)
        {
            var S = sim.S;
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = new Color(0.2f, 0.23f, 0.18f); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.3f, 0.34f, 0.26f), 2);
            var sc = new Rect(r.x + 7, r.y + 7, r.width - 14, r.height - 14);
            GUI.color = new Color(0.045f, 0.07f, 0.045f); GUI.DrawTexture(sc, white);
            GUI.color = new Color(0.55f, 1f, 0.6f, 0.05f); GUI.DrawTexture(new Rect(sc.x + 10, sc.y + 8, sc.width - 20, sc.height - 16), texDisc);
            GUI.color = Color.white;
            bool cur = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
            float x = sc.x + 6, w = sc.width - 12;
            GUI.Label(new Rect(x, sc.y + 2, w, 18), Loc.T("<size=11><color=#8dff9a>> 출동 #") + S.runs + Loc.T(" 기록</color></size>"), label);
            if (S.lastBroke < 0) GUI.Label(new Rect(x, sc.y + 24, w, 40), Loc.T("<size=11><color=#8dff9a>기록 없음") + (cur ? "▮" : "") + "</color></size>", label);
            else
            {
                var h = S.runEarn; int n = h.Count; double mx = 1; foreach (var v in h) mx = System.Math.Max(mx, v);
                var bar = new Rect(x, sc.y + 22, w, 38); float bw = (w - 5 * 3) / 6f;
                for (int i = 0; i < 6; i++)
                {
                    int k = n - 6 + i; if (k < 0) continue;
                    float bh = Mathf.Max(2, bar.height * (float)(h[k] / mx));
                    GUI.color = new Color(0.55f, 1f, 0.6f, k == n - 1 ? 0.95f : 0.45f);
                    GUI.DrawTexture(new Rect(bar.x + i * (bw + 3), bar.yMax - bh, bw, bh), white);
                }
                GUI.color = Color.white;
                double lE = S.lastClaw + S.lastDrone + S.lastBlast;
                GUI.Label(new Rect(x, sc.y + 62, w, 18), Loc.T("<size=10><color=#6fd67a>부순것 ") + S.lastBroke + Loc.T(" · 연쇄 ") + S.lastChain + (S.lastContract == 1 ? Loc.T(" · 의뢰 ○") : S.lastContract == 2 ? Loc.T(" · 의뢰 ✕") : "") + "</color></size>", label);
                GUI.Label(new Rect(x, sc.y + 78, w, 22), "<size=14><b><color=#b8ffc0>+" + KNum.Fmt(lE) + "</color></b><color=#8dff9a>" + (cur ? " ▮" : "") + "</color></size>", label);
            }
            GUI.color = new Color(0, 0, 0, 0.28f);
            for (float y = sc.y; y < sc.yMax; y += 3) GUI.DrawTexture(new Rect(sc.x, y, sc.width, 1), white);
            GUI.color = Color.white;
        }

        // 🟧 궤도일보 — LED 전광판 (B) · 글자가 흘러간다 · 누르면 신문
        GUIStyle ledSt, ledR;
        void LedNews(Rect r)
        {
            var S = sim.S; var M = sim.M;
            if (ledSt == null) { ledSt = new GUIStyle(label) { wordWrap = false, clipping = TextClipping.Overflow }; ledR = new GUIStyle(label) { alignment = TextAnchor.UpperRight }; }
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = new Color(0.045f, 0.035f, 0.028f); GUI.DrawTexture(r, white);
            Frame(r, ov ? Amber3 : new Color(0.17f, 0.15f, 0.13f), 3);
            int unread = sim.Unread;
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 18), (Loc.En ? "<size=10><color=#ffab3d>NEWS · Day " + S.runs + "</color></size>" : "<size=10><color=#ffab3d>ORBIT NEWS · " + S.runs + "일째</color></size>"), label);
            if (unread > 0) GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 18), Loc.T("<size=10><color=#ff5a3a>●</color> <color=#ffab3d>새 ") + unread + "</color></size>", ledR);
            string a = M.news.Count > 0 ? (unread > 0 ? Loc.T("속보 ▸ ") : "") + Loc.T(M.news[M.news.Count - 1].head) : Loc.T("오늘은 조용하다");
            string b = M.news.Count > 1 ? Loc.T(M.news[M.news.Count - 2].head) + (M.news.Count > 2 ? "  ▸  " + Loc.T(M.news[M.news.Count - 3].head) : "") : Loc.T("궤도 청소부 영업 중");
            LedStrip(new Rect(r.x + 8, r.y + 26, r.width - 16, 34), a, 15, 38f);
            LedStrip(new Rect(r.x + 8, r.y + 66, r.width - 16, 26), b, 12, 26f);
            if (S.front1 >= 0 && S.front2 >= 0 && sim.Mk != null && !frontOpen)
            {   // ★ 내일 1면 — 창을 띄우지 않고 모니터 아랫줄 단추 하나 (09-26 정돈 9: 조종실에 올 때마다 창문 한가운데를 덮었다)
                var fb = new Rect(r.x + 6, r.yMax - 28, r.width - 12, 22); float pp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5);
                bool fov = fb.Contains(Event.current.mousePosition);
                GUI.color = new Color(0.35f, 0.08f, 0.26f, fov ? 1f : 0.7f + 0.3f * pp); GUI.DrawTexture(fb, white); Frame(fb, new Color(1f, 0.36f, 0.81f), 1); GUI.color = Color.white;
                GUI.Label(fb, Loc.T("<size=11><b><color=#ffd6f2>★ 내일 1면 고르기 ▸</color></b></size>"), center);
                if (GUI.Button(fb, GUIContent.none, GUIStyle.none)) { frontOpen = true; OrbitSfx.Play("tick", 0.6f); }
            }
            else GUI.Label(new Rect(r.x + 10, r.yMax - 26, r.width - 20, 20), "<size=10><color=" + (ov ? "#ffdf95" : "#7a6a55") + Loc.T(">누르면 신문 ▸</color></size>"), label);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { newsOpen = true; newsSel = -1; }
        }
        void LedStrip(Rect s, string text, int size, float speed)
        {
            GUI.color = new Color(0.085f, 0.04f, 0.015f); GUI.DrawTexture(s, white);
            GUI.color = new Color(1f, 0.67f, 0.24f, 0.05f);
            for (float xx = s.x + 1; xx < s.xMax; xx += 3) GUI.DrawTexture(new Rect(xx, s.y, 1, s.height), white);
            GUI.color = Color.white;
            string t = "<size=" + size + "><b><color=#ffab3d>" + text + "</color></b></size>";
            float tw = ledSt.CalcSize(new GUIContent(t)).x;
            float off = reduceMotion ? 4 : s.width - Mathf.Repeat(Time.unscaledTime * speed, s.width + tw + 20);
            GUI.BeginGroup(s);
            GUI.color = new Color(1f, 0.55f, 0.1f, 0.25f); GUI.Label(new Rect(off, (s.height - size - 8) / 2 + 1, tw + 10, size + 10), t, ledSt);
            GUI.color = Color.white; GUI.Label(new Rect(off, (s.height - size - 8) / 2, tw + 10, size + 10), t, ledSt);
            GUI.EndGroup();
        }

        // 🟨 회사 명판 — 황동판 · 파산은 빨간 안전덮개 (A)
        void BrassPlate(Rect r)
        {
            var S = sim.S; var M = sim.M;
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = new Color(0.66f, 0.51f, 0.22f); GUI.DrawTexture(r, white);
            GUI.color = new Color(0.9f, 0.77f, 0.46f, 0.7f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.38f), white);
            GUI.color = new Color(1f, 0.95f, 0.8f, 0.6f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), white);
            GUI.color = new Color(0.38f, 0.28f, 0.1f); GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), white);
            foreach (var sp in new[] { new Vector2(r.x + 4, r.y + 4), new Vector2(r.xMax - 10, r.y + 4), new Vector2(r.x + 4, r.yMax - 10), new Vector2(r.xMax - 10, r.yMax - 10) })
            { GUI.color = new Color(0.4f, 0.3f, 0.12f); GUI.DrawTexture(new Rect(sp.x, sp.y, 6, 6), texDisc); GUI.color = new Color(1, 1, 1, 0.35f); GUI.DrawTexture(new Rect(sp.x + 1, sp.y + 1, 2, 2), texDisc); }
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 24, 22), Loc.T("<size=13><b><color=#3b2a08>궤도 청소부 ") + M.company + Loc.T("대</color></b></size>"), label);
            GUI.Label(new Rect(r.x + 14, r.y + 26, r.width - 24, 20), Loc.T("<size=10><color=#4a360c>쌓인 신용 +") + S.creditPending * SweepSim.CreditK + "</color></size>", label);
            GUI.color = Color.white; return;                                   // 파산은 출동 단추 왼쪽 위 유리 덮개 단추로 옮겼다 (09-24)
#pragma warning disable CS0162
            var cv = new Rect(r.xMax - 48, r.y + 7, 36, r.height - 14);
            bool can = sim.CanBankrupt, ov = can && cv.Contains(Event.current.mousePosition);
            if (!can)
            {
                GUI.color = new Color(0.25f, 0.2f, 0.12f); GUI.DrawTexture(cv, white); Frame(cv, new Color(0.35f, 0.27f, 0.12f), 1);
                GUI.Label(cv, Loc.T("<size=9><color=#8a6a30>잠김</color></size>"), center);
            }
            else if (!bankruptArmed)
            {
                GUI.color = new Color(0.2f, 0.05f, 0.04f); GUI.DrawTexture(cv, white);
                GUI.color = new Color(1f, 0.25f, 0.18f, ov ? 0.7f : 0.5f); GUI.DrawTexture(cv, white);
                GUI.color = new Color(0, 0, 0, 0.22f); for (float yy = cv.y + 3; yy < cv.yMax; yy += 6) GUI.DrawTexture(new Rect(cv.x, yy, cv.width, 2), white);
                Frame(cv, new Color(0.75f, 0.15f, 0.1f), ov ? 2 : 1.5f);
                GUI.Label(cv, Loc.T("<size=10><b><color=#ffe0dc>파산</color></b></size>"), center);
                if (GUI.Button(cv, GUIContent.none, GUIStyle.none)) { bankruptArmed = true; OrbitSfx.Play("tick", 0.7f, 0.2f, 0.02f); }
            }
            else
            {
                GUI.color = new Color(1f, 0.25f, 0.18f, 0.5f); GUI.DrawTexture(new Rect(cv.x, cv.y - 12, cv.width, 8), white);
                GUI.color = new Color(0.12f, 0.03f, 0.03f); GUI.DrawTexture(cv, white);
                float gl = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8);
                GUI.color = new Color(1f, 0.2f, 0.15f, gl); GUI.DrawTexture(new Rect(cv.x + 6, cv.y + 5, cv.width - 12, cv.height - 10), texDisc);
                GUI.color = Color.white;
                GUI.Label(new Rect(cv.x - 30, cv.yMax + 1, cv.width + 36, 16), Loc.T("<size=9><color=#ff9b8f>한 번 더 → 파산</color></size>"), center);
                if (GUI.Button(cv, GUIContent.none, GUIStyle.none)) { sim.Bankrupt(); bankruptArmed = false; showResult = false; flow = 2; }
            }
            GUI.color = Color.white;
        }

        // 🧯 유리 덮개 파산 단추 — 유리를 누르면 젖혀 열리고, 빨간 단추를 누르면 파산 (09-24 사장님). 5초 안 누르면 다시 닫힌다
        float glassK, glassT; bool glassOpen;
        void BankruptGlass(Rect r)
        {
            bool can = sim.CanBankrupt;
            bool stuck = can && sim.S.overdue && sim.S.cash + sim.LoanCap < sim.BillAmount;   // 🧯 대출로도 못 갚는다 — 파산할 때 (09-26 사장님 「파산해야 하는 경우엔 파산 쪽에 표시」)
            if (stuck && glassK < 0.05f)
            {
                float sp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                GUI.color = new Color(1f, 0.2f, 0.15f, 0.22f + 0.3f * sp); GUI.DrawTexture(new Rect(r.center.x - 70, r.y - 22, 140, 110), texDisc);
                GUI.color = Color.white;
                var tag = new Rect(r.center.x - 70, r.y - 44 - 4 * sp, 140, 22);
                GUI.color = new Color(0.45f, 0.05f, 0.04f, 0.95f); GUI.DrawTexture(tag, white); Frame(tag, new Color(1f, 0.35f, 0.25f), 2); GUI.color = Color.white;
                GUI.Label(tag, Loc.T("<size=12><b><color=#ffd2c8>대출로도 못 갚는다 ▼</color></b></size>"), center);
            }
            float dt = Time.unscaledDeltaTime;
            if (glassOpen) { glassT -= dt; if (glassT <= 0 || !can) glassOpen = false; }
            glassK = Mathf.MoveTowards(glassK, glassOpen ? 1 : 0, dt / 0.25f);
            // 🧯 누운 단추 — 출동 단추처럼 비스듬히 내려다본 모양 (09-24 사장님 14번)
            float cx = r.center.x, by = r.y + 30;
            var box = new Rect(r.x, r.y, r.width, 70);
            // 받침 — 노란 경고 테 · 검은 줄 · 어두운 속
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(cx - 46, by + 6, 92, 38), texDisc);
            GUI.color = new Color(0.95f, 0.75f, 0.1f); GUI.DrawTexture(new Rect(cx - 45, by, 90, 36), texDisc);
            GUI.color = new Color(0.08f, 0.08f, 0.08f);
            for (int k = 0; k < 12; k++) { float a = k * Mathf.PI / 6 + 0.2f; GUI.DrawTexture(new Rect(cx + Mathf.Cos(a) * 40 - 3, by + 18 + Mathf.Sin(a) * 15 - 2, 6, 4), white); }
            GUI.color = new Color(0.12f, 0.13f, 0.16f); GUI.DrawTexture(new Rect(cx - 36, by + 4, 72, 28), texDisc);
            // 빨간 단추 — 옆면 + 윗면 (누르면 가라앉음)
            float fw = 44, fh = 18, side = 8;
            var btn = new Rect(cx - fw / 2, by + 2, fw, fh + side);
            bool ovBtn = glassK > 0.95f && btn.Contains(Event.current.mousePosition);
            bool press = ovBtn && Mouse.current != null && Mouse.current.leftButton.isPressed;
            float dip = press ? 5 : 0, fy = by + 2 + dip, sh = side - dip;
            float pulse = glassK > 0.95f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8) : 0;
            Color face = can ? Color.Lerp(new Color(0.85f, 0.1f, 0.08f), new Color(1f, 0.3f, 0.22f), ovBtn ? 1 : pulse * 0.5f) : new Color(0.4f, 0.18f, 0.16f);
            GUI.color = new Color(0.35f, 0.04f, 0.03f); GUI.DrawTexture(new Rect(cx - fw / 2, fy + sh, fw, fh), texDisc); GUI.DrawTexture(new Rect(cx - fw / 2, fy + fh / 2, fw, sh), white);
            GUI.color = face; GUI.DrawTexture(new Rect(cx - fw / 2, fy, fw, fh), texDisc);
            GUI.color = new Color(1, 1, 1, 0.3f); GUI.DrawTexture(new Rect(cx - fw * 0.3f, fy + 3, fw * 0.4f, 5), texDisc);
            if (can && glassK > 0.95f) { GUI.color = new Color(1f, 0.3f, 0.2f, 0.18f + 0.18f * pulse); GUI.DrawTexture(new Rect(cx - 50, by - 12, 100, 56), texDisc); }
            GUI.color = Color.white;
            if (glassK > 0.95f && GUI.Button(btn, GUIContent.none, GUIStyle.none) && can)
            { sim.Bankrupt(); glassOpen = false; glassK = 0; bankruptArmed = false; showResult = false; flow = 2; OrbitSfx.Play("break", 1f); return; }
            // 유리 돔 — 뒤 경첩으로 젖혀 선다 (열릴수록 위로 올라가며 납작한 테만 보임)
            float gy = by - 16 - glassK * 26, gh2 = Mathf.Lerp(40, 10, glassK);
            var glass = new Rect(cx - 33, gy, 66, gh2);
            bool ovGlass = glassK < 0.05f && new Rect(cx - 36, by - 18, 72, 50).Contains(Event.current.mousePosition);
            GUI.color = new Color(0.6f, 0.85f, 1f, 0.2f + (ovGlass ? 0.1f : 0)); GUI.DrawTexture(glass, texDisc);
            GUI.color = new Color(0.8f, 0.92f, 1f, 0.55f); GUI.DrawTexture(glass, texRing);
            GUI.color = new Color(1, 1, 1, 0.55f); GUI.DrawTexture(new Rect(glass.x + 12, glass.y + gh2 * 0.18f, 16, Mathf.Max(2, gh2 * 0.14f)), texDisc);
            GUI.color = new Color(0.55f, 0.58f, 0.62f); GUI.DrawTexture(new Rect(cx - 8, by + 1 - glassK * 2, 16, 3), white);   // 경첩
            GUI.color = Color.white;
            if (!can && glassK < 0.05f) GUI.Label(new Rect(cx - 30, by - 10, 60, 18), Loc.T("<size=10><color=#c8d0dc>잠김</color></size>"), center);
            if (ovGlass && GUI.Button(new Rect(cx - 36, by - 18, 72, 50), GUIContent.none, GUIStyle.none))
            {
                if (can) { glassOpen = true; glassT = 5f; OrbitSfx.Play("clank", 0.8f); } else OrbitSfx.Play("tick", 0.5f, 0.1f, 0.02f);
            }
            // 명판
            var plate = new Rect(r.x - 6, box.yMax + 3, r.width + 12, 18);
            GUI.color = new Color(0.12f, 0.04f, 0.04f, 0.9f); GUI.DrawTexture(plate, white); Frame(plate, new Color(0.7f, 0.18f, 0.12f), 1); GUI.color = Color.white;
            string pl = !can ? Loc.T("3장부터") : glassK > 0.95f ? Loc.T("누르면 파산 · 신용 +") + sim.S.creditPending * SweepSim.CreditK : stuck ? Loc.T("지금 파산") : Loc.T("파산");
            GUI.Label(plate, "<size=10><b><color=#ffb3a8>" + pl + "</color></b></size>", center);
        }

        // 🎬 막 전환 카드 — 화면 가운데 3.5초 (09-24 레벨 설계: 목성 = 2막 · 해왕성 = 3막)
        int actN; float actT;
        string actSub;
        public void ShowAct(int n, string sub = null) { actN = n; actT = 3.5f; actSub = sub; }
        void ActCard()
        {
            if (actT <= 0) return;
            actT -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((3.5f - actT) / 0.35f) * Mathf.Clamp01(actT / 0.6f);      // 들어오고 · 사라지고
            float cy = RefH * 0.42f, h = 150 * k;
            GUI.color = new Color(0, 0, 0, 0.72f * k); GUI.DrawTexture(new Rect(0, cy - h / 2, vw, h), white);
            Color ac = actN == 1 ? new Color(1f, 0.6f, 0.4f) : actN == 2 ? new Color(1f, 0.76f, 0.35f) : new Color(0.55f, 0.75f, 1f);
            GUI.color = new Color(ac.r, ac.g, ac.b, k); GUI.DrawTexture(new Rect(0, cy - h / 2, vw, 2), white); GUI.DrawTexture(new Rect(0, cy + h / 2 - 2, vw, 2), white);
            float sweep = (3.5f - actT) * 900 % (vw + 400) - 200;                                  // 지나가는 빛줄기
            GUI.color = new Color(ac.r, ac.g, ac.b, 0.25f * k); GUI.DrawTexture(new Rect(sweep, cy - h / 2, 120, h), white);
            GUI.color = new Color(1, 1, 1, k);
            string hex = ColorUtility.ToHtmlStringRGB(ac);
            string t1 = actN == 1 ? Loc.T("🛰 관문 붕괴") : actN == 2 ? Loc.T("2막 · 외행성") : Loc.T("3막 · 심우주");
            string t2 = actN == 2 ? Loc.T("외행성 면허 — 정비고 바깥 고리 16칸이 열렸다 · 곱하기 칸 · 무기 3단계") : Loc.T("심우주 — 해왕성 너머 카이퍼 벨트까지 · 마지막 청구서가 기다린다");
            GUI.Label(new Rect(0, cy - 48, vw, 60), "<size=40><b><color=#" + hex + ">" + t1 + "</color></b></size>", center);
            if (actN == 1) t2 = (actSub ?? "") + Loc.T(" · 열쇠 +1 · 정비고 한 칸씩 더");
            GUI.Label(new Rect(0, cy + 16, vw, 26), "<size=15><color=#dfe6ef>" + t2 + "</color></size>", center);
            GUI.color = Color.white;
        }

    }
}
