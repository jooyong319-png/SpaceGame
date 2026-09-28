using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🚀 출동 화면 — 효과 · 폭풍 · 출발 · 알림 · 위 줄 · 자동 조준 · 주식 창 · 속보 띠 · 스킬 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 연출 (도파민 사다리 §5)

        int chainHund, chainTierSeen; float chainPopT;
        string cBigTxt; float cBigT; int cBigSize; Color cBigC;
        /// <summary>가운데 큰 글자 한 줄 — 떠 있는 게 더 크거나 같으면 새 것은 버린다 (막 사라지는 중이면 바꾼다)</summary>
        public void Big(string t, float dur, int size, Color c) { if (cBigT > 0.35f && size < cBigSize) return; cBigTxt = t; cBigT = dur; cBigSize = size; cBigC = c; }
        void Effects()
        {
            if (game.edgeGlow > 0.01f && !reduceMotion) { GUI.color = new Color(1f, 0.76f, 0.3f, game.edgeGlow * 0.22f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette); }
            if (game.flash > 0.01f) { GUI.color = new Color(1f, 0.97f, 0.9f, game.flash * 0.3f * SweepGame.FlashMul); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); }   // 흰 번쩍임 0.7 → 0.3 (블룸이 있어 화면이 하얗게 날아갔다)
            GUI.color = Color.white;
            var R = sim.R;
            if (!R.over && R.chain >= 10 && R.chainT > 0)
            {
                int tier = R.chain >= 200 ? 4 : R.chain >= 80 ? 3 : R.chain >= 30 ? 2 : 1;
                // 200 넘으면 큰 글자는 200 · 500 · 1000 · 2000 · 5000 … 넘을 때만 1.5초 — 그 사이엔 위쪽 작은 계수기 (09-24: 콤보가 판 내내 가운데를 덮었다)
                int ms = 0; for (long m = 100; m <= R.chain; m = m.ToString()[0] == '1' ? m * 5 / 2 : m * 2) ms++;   // 100 · 250 · 500 · 1000 · 2500 …
                if (ms > chainHund) { chainHund = ms; chainPopT = 1.1f; Big(Loc.T("연쇄 ") + R.chain + "!", 1.1f, 38, new Color(1f, 0.87f, 0.58f)); }   // 가운데는 고비를 넘을 때만 — 평소엔 오른쪽 위 계기 (09-26 정돈 1)
                if (tier > chainTierSeen) { chainTierSeen = tier; chainPopT = Mathf.Max(chainPopT, 0.6f); }
                chainPopT -= Time.unscaledDeltaTime;
                var cg = new Rect(vw - 150, 38, 138, 50);
                GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.85f * Mathf.Min(1, (float)R.chainT * 3)); GUI.DrawTexture(cg, white);
                Frame(cg, new Color(1f, 0.76f, 0.35f, (0.25f + 0.5f * Mathf.Clamp01(chainPopT)) * Mathf.Min(1, (float)R.chainT * 3)), 1);
                GUI.color = new Color(1, 1, 1, Mathf.Min(1, (float)R.chainT * 3));
                GUI.Label(new Rect(cg.x + 8, cg.y + 4, 60, 18), Loc.T("<size=11><color=#8a93a3>연쇄</color></size>"), label);
                GUI.Label(new Rect(cg.x + 8, cg.y + 1, cg.width - 16, 26), "<size=" + (17 + tier) + "><b><color=#ffdf95>" + R.chain + "</color></b></size>", cost);
                GUI.Label(new Rect(cg.x + 8, cg.y + 31, cg.width - 16, 18), Loc.T("<size=10><color=#8a93a3>최고 ") + Mathf.Max(R.chainBest, sim.M.bestChain) + "</color></size>", cost);
                GUI.color = Color.white;
            }
            else { chainHund = 0; chainTierSeen = 0; }
            if (game.kessText != null) { Big(game.kessText + " ×" + R.chain, game.kessT, game.kessText == Loc.T("케슬러!") ? 30 : 36, new Color(1f, 0.6f, 0.3f)); game.kessText = null; }   // 케슬러 단계도 같은 통로 (정돈 6)
            if (cBigT > 0 && !R.over)
            {   // 🔠 가운데 큰 글자 — 한 번에 하나 (09-26 정돈 규칙 ①)
                cBigT -= Time.unscaledDeltaTime;
                chainSt.fontSize = cBigSize; chainSt.normal.textColor = new Color(cBigC.r, cBigC.g, cBigC.b, Mathf.Min(1, cBigT * 2.5f));
                GUI.Label(new Rect(vw / 2 - 300, 74, 600, 56), cBigTxt, chainSt);
            }
            if (bannerT > 0 && banner != null && !sim.R.over)
            {
                var c = bannerKind == -1 ? SweepGame.Amber2 : new Color(1f, 0.55f, 0.48f, Mathf.Sin(Time.time * 12) > -0.3f ? 1 : 0.55f);
                pop.fontSize = 17; pop.normal.textColor = c;
                float bw = pop.CalcSize(new GUIContent(banner)).x;              // 🌐 영어 안내는 길다 — 넘치면 글자를 줄여 한 줄에
                if (bw > 760) pop.fontSize = Mathf.Max(12, (int)(17 * 760 / bw));
                GUI.Label(new Rect(vw / 2 - 390, 196, 780, 26), banner, pop);
                pop.fontSize = 17;
                if (bannerKind == 0) GUI.Label(new Rect(8, RefH / 2 - 14, 30, 28), "◀", pop);
                if (bannerKind == 1) GUI.Label(new Rect(vw - 36, 110, 30, 28), "▶", pop);
            }
        }

        /// <summary>출동 중에도 「창으로 내다본다」 — 가장자리에 옅은 선체와 창틀 모서리 (사장님 09-23)</summary>
        // 🔴 화성 모래 폭풍 — 22초마다 5초쯤 붉은 먼지가 화면을 덮는다 (화면만, 규칙은 그대로)
        void Storm()
        {
            var o = SweepSim.Orbits[sim.S.orbit];
            if (!o.storm || sim.R.clean) return;
            float ph = (float)(sim.R.t % 22.0);
            float a = ph < 14 ? 0 : ph < 15.5f ? (ph - 14) / 1.5f : ph < 19.5f ? 1 : ph < 21 ? 1 - (ph - 19.5f) / 1.5f : 0;
            if (a <= 0) return;
            // 옅은 붉은 먼지 + 가장자리 짙게 + 빠르게 스치는 모래 줄기 (도트 줄) — 전체 덮기(0.42)는 너무 탁했다
            GUI.color = new Color(0.72f, 0.36f, 0.2f, 0.14f * a); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            GUI.color = new Color(0.75f, 0.35f, 0.15f, 0.55f * a); GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette);
            for (int i = 0; i < 70; i++)
            {
                float sp = 500 + (i * 53 % 7) * 90, y = (i * 97 % 540) + 30, x = Mathf.Repeat(Time.time * sp + i * 211, vw + 300) - 150, len = 30 + (i * 31 % 5) * 22;
                GUI.color = new Color(1f, 0.72f - (i % 3) * 0.08f, 0.45f, (0.25f + (i % 4) * 0.1f) * a); GUI.DrawTexture(new Rect(x, y, len, i % 5 == 0 ? 3 : 2), white);
            }
            GUI.color = Color.white;
            if (ph > 14 && ph < 19.5f) GUI.Label(new Rect(0, 40, vw, 26), Loc.T("<size=18><color=#ffb080><b>모래 폭풍</b></color></size>  <size=13><color=#ffd0b0>값 ×1.5 · 잔해가 몰려온다</color></size>"), center);
        }

        // 🚀 출발 1.3초 — 조종실 선체가 커지며 밖으로 날아가고, 별 줄기가 쏟아지고, 행성 이름이 뜬다 (사장님 「출발 후가 2% 빠진 느낌」)
        void Launch()
        {
            float k = 1 - Mathf.Clamp01(launchT / LaunchLen);                 // 0 → 1
            var ctr = new Vector2(vw / 2, 300);
            // 별 줄기 — 가운데서 바깥으로
            for (int i = 0; i < 56; i++)
            {
                float ang = i * 2.39996f, spd = 0.55f + (i * 37 % 11) * 0.06f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float r0 = 30 + k * spd * 760, r1 = r0 + 20 + k * 220 * spd;
                Line(ctr + dir * r0, ctr + dir * r1, new Color(0.85f, 0.9f, 1f, 0.75f * (1 - k)), 1.6f + (i % 3) * 0.6f);
            }
            // 조종실 선체가 커지며 날아간다 (처음 0.55초)
            if (hullTex != null && k < 0.55f)
            {
                float a = 1 - k / 0.55f, sc = 1 + k * 3.2f;
                var m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), new Vector2(ox + 480, 190));
                GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(ox, 0, 960, 600), hullTex); GUI.color = Color.white;
                GUI.matrix = m;
            }
            // 도착 제목
            float ta = Mathf.Clamp01((k - 0.25f) / 0.2f) * Mathf.Clamp01((1 - k) / 0.2f + 0.35f);
            if (ta > 0)
            {
                var o = SweepSim.Orbits[sim.S.orbit];
                float dy = (1 - Mathf.Clamp01((k - 0.25f) / 0.25f)) * 14;
                title.fontSize = 38;
                for (int i = 0; i < 16; i++)
                {   // 제목 뒤 옅은 띠 — 가운데 진하고 양끝 옅게 (궤도 잔해 위에서 글자가 묻혔다)
                    float kk = Mathf.Abs(i - 7.5f) / 7.5f;
                    GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.55f * ta * (1 - kk * kk));
                    GUI.DrawTexture(new Rect(vw / 2 - 360 + i * 45, 146 + dy, 46, 86), white);
                }
                GUI.color = new Color(1, 1, 1, ta);
                GUI.Label(new Rect(0, 150 + dy, vw, 50), "<color=#ffdf95>" + (sim.R.clean ? Loc.T("청산 출동") : o.name + Loc.T(" 궤도")) + "</color>", title);
                var c = sim.CurContract;
                title.fontSize = 16;
                GUI.Label(new Rect(0, 200 + dy, vw, 26), Loc.T("출동 ") + sim.S.runs + Loc.T(" · 연료 ") + Mathf.RoundToInt((float)sim.R.max) + Loc.T("초") + (c != null && !sim.R.clean ? Loc.T(" · 의뢰: ") + c.Value.text : ""), title);
                GUI.color = Color.white; title.fontSize = 18;
            }
        }

        void WindowEdge()
        {
            GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.32f);
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette);
            GUI.color = new Color(0.16f, 0.23f, 0.32f, 0.5f);
            float t2 = 3, L = 44;
            GUI.DrawTexture(new Rect(0, 0, vw, t2), white); GUI.DrawTexture(new Rect(0, RefH - t2, vw, t2), white);
            GUI.DrawTexture(new Rect(0, 0, t2, RefH), white); GUI.DrawTexture(new Rect(vw - t2, 0, t2, RefH), white);
            GUI.color = new Color(0.25f, 0.34f, 0.46f, 0.8f);
            foreach (var c in new[] { new Vector2(0, 0), new Vector2(vw - L, 0), new Vector2(0, RefH - 6), new Vector2(vw - L, RefH - 6) })
            { GUI.DrawTexture(new Rect(c.x, c.y, L, 6), white); }
            foreach (var c in new[] { new Vector2(0, 0), new Vector2(vw - 6, 0), new Vector2(0, RefH - L), new Vector2(vw - 6, RefH - L) })
            { GUI.DrawTexture(new Rect(c.x, c.y, 6, L), white); }
            GUI.color = Color.white;
        }

        // 📣 알림줄 — 왼쪽 위 의뢰 카드 아래, 한 줄씩 (정돈 2)
        void Notices()
        {
            float y = AnchorOn ? 164 : 102;                                    // 속보 앵커가 떠 있으면 그 아래
            foreach (var q in game.notices)
            {
                float a = Mathf.Clamp01(q.t / 0.12f) * Mathf.Clamp01((2.8f - q.t) / 0.5f), sx = (1 - Mathf.Clamp01(q.t / 0.15f)) * -30;
                string t = q.text + (q.n > 1 ? "  ×" + q.n : "");
                float w = label.CalcSize(new GUIContent("<size=13><b>" + t + "</b></size>")).x + 22;
                var r = new Rect(12 + sx, y, w, 22);
                GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.8f * a); GUI.DrawTexture(r, white);
                GUI.color = new Color(q.c.r, q.c.g, q.c.b, a); GUI.DrawTexture(new Rect(r.x, r.y, 3, r.height), white);
                GUI.color = new Color(1, 1, 1, a); GUI.Label(new Rect(r.x + 10, r.y + 1, w, 20), "<size=13><b><color=#" + ColorUtility.ToHtmlStringRGB(q.c) + ">" + t + "</color></b></size>", label);
                y += 25;
            }
            GUI.color = Color.white;
        }

        void Pops()
        {
            foreach (var p in game.pops)
            {
                Vector3 sp = game.WorldToScreen(game.PxToWorld(p.px.x, p.px.y));
                var c = p.c; c.a = 1 - p.age * p.age;
                pop.normal.textColor = c; pop.fontSize = Mathf.RoundToInt(p.size);
                GUI.Label(new Rect(sp.x / scale - 120, (Screen.height - sp.y) / scale - 12, 240, 24), p.text, pop);
            }
        }

        // ───────────────────────────────── 출동 중 위 띠

        void RunHud()
        {
            var S = sim.S; var R = sim.R;
            float x = 14;
            void Item(string k, string v, GUIStyle st = null)
            {
                GUI.Label(new Rect(x, 14, 80, 20), k, dim); float kw = dim.CalcSize(new GUIContent(k)).x;
                var s = st ?? big; float w = s.CalcSize(new GUIContent(v)).x;
                GUI.Label(new Rect(x + kw + 6, 9, w + 4, 28), v, s); x += kw + w + 24;
            }
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.55f); GUI.DrawTexture(new Rect(0, 0, vw, 40), white); GUI.color = Color.white;   // 위 줄 뒤 옅은 띠 (09-26 정돈 5)
            big.fontSize = 22;                                                         // 도트 글꼴은 11의 배수에서만 또렷하다 — 크기 대신 색으로 번쩍 (정돈 5)
            big.normal.textColor = Color.Lerp(Color.white, new Color(1f, 0.87f, 0.58f), Mathf.Clamp01(game.creditPulse * 2));
            float x0 = x;
            Item(Loc.T("돈"), KNum.Fmt(shown));
            big.normal.textColor = Color.white; big.fontSize = 20;
            CreditScreen = new Vector2((x0 + 50) * scale, Screen.height - 22 * scale);
            if (R.clean)
            {
                Item(Loc.T("궤도 청소율"), Mathf.Min(100, Mathf.FloorToInt(100f * R.cleanKills / R.cleanGoal)) + "%", big);
            }
            else if (sim.M.endless)
                Item(Loc.T("무한 궤도"), "<color=#d8ccff>" + sim.M.depth + Loc.T("층</color>  <color=#8a93a3>최고 ") + sim.M.bestDepth + Loc.T("층</color>"), label);   // 빚은 끝났다 — 청구서 대신 층
            else if (S.bill < SweepSim.Bills.Length)
                Item(Loc.T("청구서"), KNum.Fmt(sim.BillAmount) + " · " + S.billDue + Loc.T("판") + (S.debt > 0 ? Loc.T(" <color=#ee7766>빚 상환 ") + Mathf.RoundToInt((float)sim.Cut * 100) + "%</color>" : ""), label);
            // 🛰 관문 체력 — 위 가운데 (09-26)
            {
                var gj = sim.GateJunk;
                if (gj != null)
                {
                    Vector3 gsp = game.WorldToScreen(game.PxToWorld(gj.x, gj.y));    // 🛰 관문 바로 위에 따라다닌다 (09-26 — 위 가운데 막대가 화면을 가렸다)
                    var gr = new Rect(Mathf.Clamp(gsp.x / scale - 110, 8, vw - 228), Mathf.Clamp((Screen.height - gsp.y) / scale - 78, 60, RefH - 120), 220, 30);
                    GUI.color = new Color(0.05f, 0.04f, 0.04f, 0.85f); GUI.DrawTexture(gr, white); Frame(gr, new Color(1f, 0.6f, 0.4f, 0.8f), 1);
                    if (!sim.GateReady)
                    {   // 🛡 방어막 — 관문 둘레에 푸른 막 · 막대 대신 자물쇠 (09-27)
                        float sp = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f), gx = gsp.x / scale, gy = (Screen.height - gsp.y) / scale;
                        GUI.color = new Color(0.45f, 0.8f, 1f, 0.12f + 0.08f * sp); GUI.DrawTexture(new Rect(gx - 78, gy - 78, 156, 156), texDisc);
                        GUI.color = new Color(0.6f, 0.9f, 1f, 0.55f + 0.25f * sp); GUI.DrawTexture(new Rect(gx - 80, gy - 80, 160, 160), texRing);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(gr.x + 8, gr.y + 1, gr.width - 16, 18), "<size=11><color=#ffb36b>🛰 " + sim.GateName + Loc.T("</color> <color=#9fdcff>· 방어막</color></size>"), label);
                        GUI.Label(new Rect(gr.x + 8, gr.y + 14, gr.width - 16, 16), Loc.T("<size=10><color=#9fdcff>청구서 ") + sim.GateBill + Loc.T("장을 갚으면 풀린다</color></size>"), label);
                        goto gateDone;
                    }
                    float gk = Mathf.Clamp01((float)gj.hp / Mathf.Max(1, gj.max));
                    GUI.color = new Color(0.2f, 0.12f, 0.1f); GUI.DrawTexture(new Rect(gr.x + 6, gr.yMax - 9, gr.width - 12, 5), white);
                    GUI.color = new Color(1f, 0.55f, 0.35f); GUI.DrawTexture(new Rect(gr.x + 6, gr.yMax - 9, (gr.width - 12) * gk, 5), white); GUI.color = Color.white;
                    GUI.Label(new Rect(gr.x + 8, gr.y + 1, gr.width - 16, 18), "<size=11><color=#ffb36b>🛰 " + sim.GateName + "</color> <color=#8a93a3>→ " + sim.NextName + "</color></size>", label);
                    GUI.Label(new Rect(gr.x + 8, gr.y + 1, gr.width - 16, 18), "<size=11>" + Mathf.CeilToInt(gk * 100) + "%</size>", cost);
                    gateDone:;
                }
            }
            // 💰 이번 판 계산대 — 계기판 위, 포구 오른쪽. 금화가 여기로 날아와 한 숫자로 (시안 DbsvFEEy1K5ddbZsM61B2y)
            {
                var tr = new Rect(vw - 312, RefH - 50, 170, 38);                      // 🟩 계기판 속 액정 (09-24 10 · 28번)
                TallyScreen = new Vector2(tr.center.x * scale, Screen.height - tr.center.y * scale);
                float pu = game.tallyPulse;
                Lcd(tr, pu);
                GUI.Label(new Rect(tr.x + 8, tr.y, tr.width, 18), Loc.T("<size=10><color=#5fa37d>이번 판</color></size>"), label);
                GUI.Label(new Rect(tr.x, tr.y + 14, tr.width - 8, 24), "<size=" + (15 + Mathf.RoundToInt(pu * 3)) + "><b><color=#ffc35a>" + (game.runTally > 0 ? "+" + KNum.Fmt(game.runTally) : "—") + "</color></b></size>", cost);
            }
            GUI.Label(new Rect(x, 14, 40, 20), Loc.T("연료"), dim);
            GUI.DrawTexture(new Rect(x + 34, 19, 160, 9), texBar);
            float fk = Mathf.Clamp01((float)(R.fuel / R.max));
            GUI.DrawTexture(new Rect(x + 34, 19, 160 * fk, 9), R.fuel < 6 ? texRed : texAmber);
            x += 210;
            if (false && R.maxShots > 0 && sim.HoleChance > 0)                     // 위 줄 덜기 — 블랙홀 %는 칸 툴팁으로 (09-24 9번)
            {
                GUI.Label(new Rect(x, 14, 200, 20), Loc.T("블랙홀 <color=#b69cff>자동 ") + (sim.HoleChance * 100).ToString("0.#") + "%</color>" + (R.holding ? Loc.T("  <color=#b69cff>● 열림</color>") : ""), dim);
            }
            GUI.Label(new Rect(vw - 264, 14, 196, 20), "<color=#8a93a3>" + SweepSim.Orbits[S.orbit].name + "</color>", cost);   // 회사 이름은 조종실에만 — 행성 이름만 작게
            if (R.holding)
            {
                float k = Mathf.Clamp01((float)R.packed.Count / Mathf.Max(1, sim.Cap));
                GUI.DrawTexture(new Rect(vw - 200, 90, 186, 8), texBar);
                GUI.color = k > 0.8f ? SweepGame.Red : SweepGame.Violet; GUI.DrawTexture(new Rect(vw - 200, 90, 186 * k, 8), white); GUI.color = Color.white;
                GUI.Label(new Rect(vw - 330, 100, 316, 18), Loc.T("압축 ") + R.packed.Count + Loc.T(" / 붕괴 ") + sim.Cap, cost);
            }
            AutoSwitch();
            if (sim.StockOpen) StockSwitch();
            var c = sim.CurContract;
            if (c != null && !R.clean)
            {   // 📋 의뢰 카드 — 왼쪽 위 돈 아래, 막대 · 성공하면 초록 번쩍 (09-24 사장님 8번 「있는 줄도 몰랐다」)
                int pr = sim.ContractProgress(R); bool ok = pr >= c.Value.target;
                if (R != cardRun) { cardRun = R; cardOk = false; }
                if (ok && !cardOk) { cardOk = true; cardFlash = 1.6f; OrbitSfx.Play("buy", 0.9f); OrbitSfx.Play("coin", 0.6f); }
                cardFlash = Mathf.Max(0, cardFlash - Time.deltaTime);
                float big2 = Mathf.Clamp01(1 - ((float)R.t - 3f) / 0.6f);                     // 출발 3초는 크게
                var cr = new Rect(12, 44, 262 + 60 * big2, 44 + 6 * big2);
                GUI.color = ok ? new Color(0.06f, 0.16f, 0.1f, 0.92f) : new Color(0.05f, 0.06f, 0.09f, 0.88f); GUI.DrawTexture(cr, white);
                Frame(cr, ok ? new Color(0.44f, 0.81f, 0.59f, 0.6f + 0.4f * cardFlash) : new Color(0.95f, 0.76f, 0.31f, 0.35f + 0.5f * big2), 1 + cardFlash * 2);
                GUI.color = Color.white;
                int pct = 25 + 10 * sim.Lv("e_quest");
                GUI.Label(new Rect(cr.x + 8, cr.y + 3, cr.width - 16, 18), "<size=" + (12 + Mathf.RoundToInt(3 * big2)) + Loc.T("><color=#ffdf95><b>의뢰</b></color>  ") + c.Value.text + "</size>", label);
                GUI.Label(new Rect(cr.x + 8, cr.y + 3, cr.width - 16, 18), "<size=11>" + (ok ? Loc.T("<color=#6fcf97><b>성공! 판 수입 +") + pct + "%</b></color>" : Loc.T("<color=#8a93a3>성공하면 +") + pct + "%</color>") + "</size>", cost);
                var pb = new Rect(cr.x + 62, cr.yMax - 14, cr.width - 70, 6);
                GUI.DrawTexture(pb, texBar); GUI.color = ok ? new Color(0.44f, 0.81f, 0.59f) : SweepGame.Amber;
                GUI.DrawTexture(new Rect(pb.x, pb.y, pb.width * Mathf.Clamp01((float)pr / Mathf.Max(1, c.Value.target)), pb.height), white); GUI.color = Color.white;
                GUI.Label(new Rect(cr.x + 8, pb.y - 11, 52, 20), "<size=10><color=#c8d0dc>" + Mathf.Min(pr, c.Value.target) + " / " + c.Value.target + "</color></size>", label);
            }
            // 첫 5분 — 새 장난감마다 한 줄씩만 (§10)
            string hint = null;
            if (R.clean) hint = null; else
            if (CoachBox()) hint = null; else
            if (!sim.M.flags.Contains("hint_claw") && R.t < 12) hint = manualFire ? Loc.T("왼쪽 단추를 누르고 있는 동안 청소선이 쏜다 — 잔해 위를 겨누자") : Loc.T("궤도 위에 커서를 대면 청소선이 빔을 쏜다 — 처음엔 한 점씩");
            else if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb") && R.t < 14) hint = Loc.T("블랙홀이 열렸다 — 쏠 때 가끔 저절로 열려 빨아들인다");
            else if (sim.DronesOn && !sim.M.flags.Contains("hint_drone") && R.t < 8) hint = Loc.T("드론은 알아서 줍는다 — 한 방에 부서지는 것만");
            else if (sim.S.orbit > 0 && !sim.M.flags.Contains("hint_p" + sim.S.orbit) && R.t < 8) hint = PlanetHint[sim.S.orbit];   // 새 행성 첫 판
            if (hint != null)
            {   // 궤도 · 포대 위에서도 읽히게 — 글자 폭만큼 어두운 띠 (09-26 밤 둘러보기)
                float hw = center.CalcSize(new GUIContent(hint)).x + 28;
                GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.72f); GUI.DrawTexture(new Rect(vw / 2 - hw / 2, RefH - 72, hw, 24), white); GUI.color = Color.white;
                GUI.Label(new Rect(vw / 2 - 360, RefH - 70, 720, 20), hint, center);
            }
            if (game.timeScale > 1) GUI.Label(new Rect(vw - 120, RefH - 46, 106, 18), Loc.T("시험 속도 ×3"), cost);
        }

        // 🎯 AUTO — 판 화면 오른쪽 아래 단추 · 키보드 A. 켜지면 화면 가운데에 「AUTO...」 (사장님 09-23)
        public bool overAuto;
        void AutoSwitch()
        {
            bool on = game.autoMode;
            // 가운데 — 은은하게, 점이 늘었다 줄었다
            if (false && on && !sim.R.over)                                  // 가운데 「AUTO...」 뺌 — 시험 단추만 (09-24 17번)
            {
                int dots = (int)(Time.unscaledTime * 2.2f) % 4;
                float a = 0.22f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f);
                GUI.color = new Color(1f, 0.87f, 0.58f, a);
                GUI.Label(new Rect(vw / 2 - 150, 250, 300, 60), "<size=44><b>AUTO" + new string('.', dots) + "</b></size>", center);
                GUI.color = Color.white;
            }
            // 🧪 시험용 작은 단추 — 오른쪽 아래 구석 (09-24 사장님 「오토 제거, 테스트할 겸 버튼만」) · 키보드 A 그대로
            var r = new Rect(vw - 134, RefH - 68, 92, 14);
            if (!on) { overAuto = false; return; }                             // 꺼져 있으면 안 그린다 — A 키로 켠다 (09-26 정돈 8)
            overAuto = r.Contains(Event.current.mousePosition);
            GUI.color = on ? new Color(0.3f, 0.21f, 0.06f, 0.85f) : new Color(0.05f, 0.05f, 0.08f, 0.6f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            GUI.Label(r, "<size=9><color=" + (on ? "#ffdf95" : "#4a5260") + Loc.T(">시험 · 자동 ") + (on ? Loc.T("켬") : Loc.T("끔")) + "</color></size>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) game.ToggleAuto();
        }

        // 📈 주식 — 판 화면 오른쪽 아래 [주식] (키보드 S), 조종실 [증권]. 켜면 오른쪽에 주식 창 (사장님 「오토 도는 동안 주식창 On/Off」)
        public bool stockOpen, overStock;
        public Rect StockRect => new Rect(vw - 478, 54, 466, 470);
        void Lcd(Rect r, float glow)                                            // 🟩 계기판 액정 — 어두운 초록 · 안쪽 그림자
        {
            GUI.color = new Color(0.02f, 0.03f, 0.03f, 0.95f); GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), white);
            GUI.color = new Color(0.035f, 0.09f, 0.06f, 0.97f); GUI.DrawTexture(r, white);
            GUI.color = new Color(0.44f, 0.81f, 0.59f, 0.06f); for (float yy = r.y + 2; yy < r.yMax; yy += 3) GUI.DrawTexture(new Rect(r.x, yy, r.width, 1), white);
            Frame(r, Color.Lerp(new Color(0.16f, 0.26f, 0.2f), new Color(1f, 0.76f, 0.35f), glow), 1); GUI.color = Color.white;
        }
        void StockSwitch()
        {
            var r = new Rect(vw - 134, RefH - 50, 92, 38);
            bool on = stockOpen, ov = r.Contains(Event.current.mousePosition);
            Lcd(r, ov ? 0.6f : on ? 0.35f : 0);
            GUI.Label(new Rect(r.x, r.y, r.width, 24), on ? Loc.T("<size=16><b><color=#9ff0bf>주식</color></b></size>") : Loc.T("<size=16><b><color=#5fa37d>주식</color></b></size>"), center);
            double pl = 0; for (int i = 0; i < sim.Mk.M.st.Count; i++) { var st = sim.Mk.M.st[i]; pl += st.shares * st.price - st.cost; }
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 18), "<size=10>" + (sim.Mk.TotalValue() > 0 ? (pl >= 0 ? "<color=#ff5c5c>+" : "<color=#5494ff>") + KNum.Fmt(pl) + "</color>" : "<color=#5f6878>S</color>") + "</size>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { stockOpen = !stockOpen; OrbitSfx.Play("tick", 0.6f); }
            overStock = ov || (stockOpen && StockRect.Contains(Event.current.mousePosition));
            if (stockOpen) StockWin(StockRect);
        }

        void StockWin(Rect r)
        {
            var mk = sim.Mk; var MS = mk.M; var S = sim.S;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.94f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.3f, 0.55f, 0.4f), 2);
            double tv = mk.TotalValue(), tc = 0; foreach (var st in MS.st) tc += st.cost;
            RowTick(); stockCashPos = new Vector2(r.xMax - 60, r.y + 18);
            GUI.Label(new Rect(r.x + 12, r.y + 6, 200, 24), Loc.T("<size=16><b><color=#9ff0bf>궤도 증권</color></b></size>"), label);
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 24), Loc.T("<size=12><color=#8a9bb3>평가</color> ") + KNum.Fmt(tv) + (tc > 0 ? "  " + (tv >= tc ? "<color=#ff5c5c>+" : "<color=#5494ff>") + ((tv / tc - 1) * 100).ToString("0.0") + "%</color>" : "") + "</size>", cost);
            // 종목 여덟 — 이름 · 가격 · 최근 20봉 등락 · 보유 표시
            for (int i = 0; i < MS.st.Count; i++)
            {
                var st = MS.st[i]; var d = Market.Defs[i];
                var row = new Rect(r.x + 8, r.y + 34 + i * 23, r.width - 16, 22);
                bool sel = MS.sel == i;
                if (sel) { GUI.color = new Color(0.2f, 0.35f, 0.26f, 0.6f); GUI.DrawTexture(row, white); GUI.color = Color.white; }
                RowFx(i, row); if (i < 8) stockRowPos[i] = new Vector2(row.xMax - 50, row.center.y);
                double ch = mk.Change(i, 20);
                string arrow = st.pushLeft > 0 ? (st.push > 0 ? " <color=#ff5c5c>▲</color>" : " <color=#5494ff>▼</color>") : "";
                GUI.Label(new Rect(row.x + 6, row.y + 1, 170, 20), "<size=13>" + (st.shares > 0 ? "<color=#ffdf95>● </color>" : "") + d.name + "</size>" + arrow, label);
                GUI.Label(new Rect(row.x + 170, row.y + 1, 80, 20), "<size=10><color=#5f6878>" + d.sector + "</color></size>", label);
                GUI.Label(new Rect(row.x, row.y + 1, row.width - 80, 20), "<size=13>" + st.price.ToString("0.0") + "</size>", cost);
                GUI.Label(new Rect(row.x, row.y + 1, row.width - 6, 20), "<size=13>" + (ch >= 0 ? "<color=#ff5c5c>+" : "<color=#5494ff>") + (ch * 100).ToString("0.0") + "%</color></size>", cost);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { MS.sel = i; OrbitSfx.Play("tick", 0.4f); }
            }
            // 고른 종목 차트
            int si = Mathf.Clamp(MS.sel, 0, MS.st.Count - 1); var ss = MS.st[si];
            var g = new Rect(r.x + 10, r.y + 222, r.width - 70, 120);
            GUI.color = new Color(0.015f, 0.02f, 0.035f); GUI.DrawTexture(g, white); GUI.color = Color.white; Frame(g, new Color(0.15f, 0.2f, 0.27f), 1);
            int n = Mathf.Min(ss.hist.Count, 34); float lo = ss.cl, hi = ss.ch;
            for (int k = ss.hist.Count - n; k < ss.hist.Count; k++) { lo = Mathf.Min(lo, ss.hist[k].l); hi = Mathf.Max(hi, ss.hist[k].h); }
            lo *= 0.98f; hi *= 1.02f; if (hi - lo < 0.01f) hi = lo + 0.01f;
            float Yp(float v) => g.yMax - 6 - (g.height - 12) * (v - lo) / (hi - lo);
            float cw = (g.width - 8) / 35f;
            void Cd(int slot, float o, float h, float l, float c)
            {
                float cx = g.x + 4 + slot * cw + cw / 2;
                GUI.color = c >= o ? UpCol : DnCol;
                GUI.DrawTexture(new Rect(cx - 0.75f, Yp(h), 1.5f, Mathf.Max(1, Yp(l) - Yp(h))), white);
                float yt = Yp(Mathf.Max(o, c)), yb = Yp(Mathf.Min(o, c));
                GUI.DrawTexture(new Rect(cx - cw * 0.35f, yt, cw * 0.7f, Mathf.Max(1.5f, yb - yt)), white);
                GUI.color = Color.white;
            }
            for (int k = 0; k < n; k++) { var c = ss.hist[ss.hist.Count - n + k]; Cd(k, c.o, c.h, c.l, c.c); }
            Cd(n, ss.co, ss.ch, ss.cl, (float)ss.price);
            if (ss.shares > 0 && Yp((float)(ss.cost / ss.shares)) > g.y && Yp((float)(ss.cost / ss.shares)) < g.yMax) { float ay = Yp((float)(ss.cost / ss.shares)); GUI.color = new Color(1f, 0.87f, 0.58f, 0.6f); for (float x = g.x; x < g.xMax; x += 8) GUI.DrawTexture(new Rect(x, ay, 4, 1), white); GUI.color = Color.white; }
            float py = Mathf.Clamp(Yp((float)ss.price), g.y + 2, g.yMax - 2);
            GUI.color = new Color(1f, 0.87f, 0.58f, 0.95f); GUI.DrawTexture(new Rect(g.xMax + 2, py - 8, 50, 16), white); GUI.color = Color.white;
            GUI.Label(new Rect(g.xMax + 2, py - 8, 50, 16), "<size=10><b><color=#2a1a05>" + ss.price.ToString("0.0") + "</color></b></size>", center);
            GUI.Label(new Rect(g.x + 4, g.y + 2, 200, 16), "<size=11><color=#8a9bb3>" + Market.Defs[si].name + " · " + Market.Defs[si].desc + "</color></size>", small);
            // 보유 · 사고팔기
            float y = g.yMax + 6;
            string hold = ss.shares > 0 ? Loc.T("보유 ") + KNum.Fmt(ss.shares * ss.price) + "  " + (ss.shares * ss.price >= ss.cost ? "<color=#ff5c5c>+" : "<color=#5494ff>") + ((ss.shares * ss.price / ss.cost - 1) * 100).ToString("0.0") + "%</color>" : Loc.T("<color=#5f6878>보유 없음</color>");
            GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), "<size=12>" + hold + "</size>", label);
            GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), Loc.T("<size=11><color=#8a9bb3>돈 ") + KNum.Fmt(S.cash) + Loc.T(" · 수수료 ") + (sim.StockFee * 100).ToString("0.#") + "%</color></size>", cost);
            y += 22;
            float bw = (r.width - 24 - 12) / 6f;
            string[] bl = { "10%", "25%", "50%", Loc.T("전부") }; float[] bf = { 0.1f, 0.25f, 0.5f, 1f };
            if (!sim.R.over) GUI.Label(new Rect(r.x + 12, y, r.width - 24, 28), Loc.T("<size=12><color=#8a9bb3>출동 중엔 시세만 본다 — 사고팔기는 조종실 증권에서</color></size>"), center);   // 🔒 출동 중 사고팔기 금지 (09-24 사장님 「팔기도 막아」)
            else
            {
                for (int k = 0; k < 4; k++) if (GUI.Button(new Rect(r.x + 12 + k * (bw + 2), y, bw, 28), Loc.T("<size=12><color=#9ff0bf>사기 ") + bl[k] + "</color></size>", btn)) TradeBuy(si, bf[k], new Vector2(r.x + 12 + k * (bw + 2) + bw / 2, y + 14));
                GUI.enabled = ss.shares > 0 && GUI.enabled;
                if (GUI.Button(new Rect(r.x + 12 + 4 * (bw + 2) + 6, y, bw, 28), Loc.T("<size=12><color=#ffb3a8>절반 팔기</color></size>"), btn)) TradeSell(si, 0.5, new Vector2(r.x + 12 + 4 * (bw + 2) + 6 + bw / 2, y + 14));
                if (GUI.Button(new Rect(r.x + 12 + 5 * (bw + 2) + 6, y, bw, 28), Loc.T("<size=12><color=#ffb3a8>전부 팔기</color></size>"), btn)) TradeSell(si, 1, new Vector2(r.x + 12 + 5 * (bw + 2) + 6 + bw / 2, y + 14));
            }
            GUI.enabled = !loanOpen;
            y += 32;
            // 🙏 개미의 기도 · 🍀 행운의 부적 (칸을 사야)
            GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), "<size=11>" + LuckLine() + "</size>", label);
            y += 22;
            // 내부자 정보
            int il = sim.Lv("a_read");
            if (il > 0)
            {
                var nn = mk.NextNews; string who = nn.up != null ? string.Join(" ", nn.up) : string.Join(" ", nn.down);
                string tip = Loc.T("다음 출동 중 속보") + (il >= 2 ? " · " + (nn.up != null ? Loc.T("<color=#ff5c5c>오를</color>") : Loc.T("<color=#5494ff>내릴</color>")) + Loc.T(" 쪽: ") + SecName(nn) : "") + (il >= 3 ? " · 「" + Clip(nn.head, 16) + "」" : "");
                GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), Loc.T("<size=11><color=#e8c77e>내부자</color> ") + tip + "</size>", label);
                y += 20;
            }
            // 최근 속보
            GUI.color = new Color(1, 1, 1, 0.08f); GUI.DrawTexture(new Rect(r.x + 10, y + 2, r.width - 20, 1), white); GUI.color = Color.white;
            for (int k = 0; k < 3 && k < MS.news.Count; k++)
            {
                var nw = MS.news[MS.news.Count - 1 - k];
                GUI.Label(new Rect(r.x + 12, y + 5 + k * 18, r.width - 24, 18), "<size=11>" + (k == 0 ? "<color=#ffdf95>" : "<color=#8a9bb3>") + Clip(Loc.T(nw.head), 30) + "</color></size>", label);
            }
        }
        static string SecName(NewsDef nn)
        {
            var keys = nn.up ?? nn.down; var names = new List<string>();
            foreach (var k in keys) { bool found = false; foreach (var d in Market.Defs) if (d.id == k) { names.Add(d.name); found = true; break; } if (!found) names.Add(k); }
            return string.Join(", ", names);
        }

        // 🗞 속보 띠 — 새 증권 속보가 뜨면 화면 위 가운데에 5초 (창을 닫아 둬도)
        float lastNewsSeen = -1, newsBanner;
        MarketNews bannerNews;
        void NewsBanner()
        {
            if (!sim.StockOpen || sim.Mk == null) return;
            var ns = sim.Mk.M.news;
            if (lastNewsSeen < 0) lastNewsSeen = ns.Count > 0 ? ns[ns.Count - 1].t : sim.Mk.M.clock - 0.01f;   // 켜자마자 옛 속보는 건너뛴다
            if (ns.Count > 0 && ns[ns.Count - 1].t > lastNewsSeen)
            {
                bannerNews = ns[ns.Count - 1]; newsBanner = 5.5f; OrbitSfx.Play("supply", 0.5f); Anchor(Loc.T(bannerNews.head));
                lastNewsSeen = ns[ns.Count - 1].t;
            }
            if (newsBanner <= 0 || bannerNews == null) return;
            newsBanner -= Time.unscaledDeltaTime;
            if (!sim.R.over || flow == 1) return;                         // 결산 화면도 앵커가 읽는다 — 띠가 「연료 바닥!」 제목을 덮었다 · 출동 중엔 앵커가 읽는다 — 위 띠가 의뢰 카드를 가렸다 (09-25 점검)
            float a = Mathf.Clamp01(newsBanner / 0.5f) * Mathf.Clamp01((5.5f - newsBanner) / 0.25f);
            var b = new Rect(vw / 2 - 300, 50, 600, 46);
            GUI.color = new Color(0.55f, 0.08f, 0.06f, 0.92f * a); GUI.DrawTexture(new Rect(b.x, b.y, 74, b.height), white);
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.92f * a); GUI.DrawTexture(new Rect(b.x + 74, b.y, b.width - 74, b.height), white);
            GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(b.x, b.y, 74, b.height), Loc.T("<size=15><b>속보</b></size>"), center);
            GUI.Label(new Rect(b.x + 84, b.y + 3, b.width - 94, 22), "<size=14><b>" + Clip(Loc.T(bannerNews.head), 34) + "</b></size>", label);
            string moves = "";
            for (int i = 0; i < sim.Mk.M.st.Count; i++) { var st = sim.Mk.M.st[i]; if (st.pushLeft <= 0) continue; moves += Market.Defs[i].name + (st.push > 0 ? " <color=#ff5c5c>▲</color>  " : " <color=#5494ff>▼</color>  "); }
            GUI.Label(new Rect(b.x + 84, b.y + 23, b.width - 94, 20), "<size=11>" + moves + "</size>", label);
            GUI.color = Color.white;
        }

        public static bool CastReq;
        public bool overSkill;
        void SkillSlot(SweepRun R)
        {
            var r = new Rect(vw / 2 - 34, RefH - 92, 68, 68);
            overSkill = r.Contains(Event.current.mousePosition);
            bool ready = R.shots > 0 && !R.holding;
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.9f); GUI.DrawTexture(r, white);
            if (R.shots < R.maxShots && !R.clean) GUI.Label(new Rect(r.x - 20, r.yMax + 1, r.width + 40, 16), Loc.T("<size=9><color=#8a7fb0>공격 ") + (sim.HoleChance * 100).ToString("0.#") + "%</color></size>", center);   // 확률로 찬다
            if (R.holding)
            {
                float k = 1 - Mathf.Clamp01((float)(R.holdT / SweepSim.HoleDur));
                GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(r.x, r.yMax - 4, r.width * k, 4), white);
            }
            GUI.color = ready ? new Color(0.85f, 0.78f, 1f) : new Color(0.35f, 0.33f, 0.42f);
            DrawIcon(new Rect(r.x + 12, r.y + 10, 44, 44), "b_n");
            GUI.color = Color.white;
            Frame(r, ready ? SweepGame.Violet : new Color(0.22f, 0.21f, 0.26f), ready && overSkill ? 3 : 2);
            GUI.Label(new Rect(r.x + 4, r.y + 2, 30, 18), "<size=11>Q</size>", dim);
            GUI.Label(new Rect(r.xMax - 34, r.yMax - 20, 30, 18), "<size=12>" + R.shots + "</size>", cost);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && ready) CastReq = true;
        }

    }
}
