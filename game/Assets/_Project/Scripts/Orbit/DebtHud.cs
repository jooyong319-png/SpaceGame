using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// rev16 — 위 띠 · 정비소(트리 · 청구서 · 파산 · 경력) · 결산 · 빚 청산. 전부 OnGUI (임시).
    /// 정비소가 곧 집이다 — 출동 사이에 늘 여기로 온다. 🔴 [출동 ▸] 이 제일 커야 한다 (「한 판 더」의 문턱).
    /// </summary>
    public class DebtHud : MonoBehaviour
    {
        public DebtGame game;
        DebtSim sim => game.sim;

        const float RefH = 600f;
        float scale = 1f, vw = 960f;
        public Vector2 CreditScreen = new Vector2(120, 580);
        public bool Blocking => sim != null && (sim.R.over || sim.S.won);

        string news = "폐업 직전 청소업체를 물려받았다. 남은 건 청소선 한 척과 할부금뿐.";
        bool showResult, bankruptArmed;
        int rBroke, rVault, rChain; double rEarned, rCut;
        double shown;
        Vector2 scroll;

        GUIStyle big, label, dim, small, cost, head, title, btn, btnOff, bigBtn, red, pop;
        Texture2D texDim, texCard, texRed, texAmber, texBar;

        public void News(string s) { news = s; }

        public void OnRunEnd()
        {
            var R = sim.R;
            showResult = true; bankruptArmed = false;
            rBroke = R.broke; rVault = R.vault; rChain = R.chainBest; rEarned = R.earned; rCut = R.cut;
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }

        void Styles()
        {
            if (big != null && texDim != null) return;
            var font = Resources.Load<Font>("Galmuri11");
            GUIStyle S(int size, Color c, TextAnchor a = TextAnchor.UpperLeft)
            { var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = a, richText = true, wordWrap = false }; s.normal.textColor = c; return s; }
            var text = new Color(0.86f, 0.89f, 0.93f); var gray = new Color(0.49f, 0.54f, 0.6f); var amber = new Color(0.95f, 0.76f, 0.31f);
            big = S(20, Color.white); label = S(14, text); dim = S(13, gray); small = S(11, gray); small.wordWrap = true;
            cost = S(13, amber, TextAnchor.UpperRight); head = S(12, gray); title = S(30, Color.white, TextAnchor.MiddleCenter);
            red = S(13, new Color(0.95f, 0.45f, 0.4f)); pop = S(16, amber, TextAnchor.MiddleCenter);
            texDim = Tex(new Color(0.02f, 0.027f, 0.047f, 0.93f)); texCard = Tex(new Color(0.06f, 0.08f, 0.11f));
            texRed = Tex(new Color(0.89f, 0.35f, 0.29f)); texAmber = Tex(amber); texBar = Tex(new Color(0.08f, 0.11f, 0.16f));
            btn = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6) };
            btn.normal.background = Tex(new Color(0.08f, 0.11f, 0.16f)); btn.hover.background = Tex(new Color(0.12f, 0.16f, 0.22f)); btn.active.background = Tex(new Color(0.16f, 0.21f, 0.29f));
            btn.normal.textColor = btn.hover.textColor = btn.active.textColor = text;
            btnOff = new GUIStyle(btn); btnOff.hover.background = btnOff.active.background = btnOff.normal.background;
            btnOff.normal.textColor = btnOff.hover.textColor = btnOff.active.textColor = gray;
            bigBtn = new GUIStyle(btn) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            bigBtn.normal.background = Tex(new Color(0.24f, 0.18f, 0.05f)); bigBtn.hover.background = Tex(new Color(0.34f, 0.25f, 0.07f));
            bigBtn.normal.textColor = bigBtn.hover.textColor = new Color(1f, 0.87f, 0.58f);
        }

        void Update()
        {
            if (sim == null) return;
            double a = sim.S.cash;
            shown = a < shown ? a : shown + (a - shown) * (1 - Mathf.Exp(-Time.deltaTime * 7f));
            if (a - shown < 1) shown = a;
        }

        void OnGUI()
        {
            if (sim == null) return;
            Styles();
            scale = Screen.height / RefH; vw = Screen.width / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            Pops();
            TopBar();
            if (sim.S.won) Ending();
            else if (sim.R.over) Shop();
            GUI.Label(new Rect(12, RefH - 22, 600, 18), news, dim);
        }

        // ───────────────────────────────── 위 띠

        void TopBar()
        {
            var S = sim.S; var R = sim.R;
            float x = 14;
            void Item(string k, string v, GUIStyle st = null)
            {
                GUI.Label(new Rect(x, 14, 80, 20), k, dim); float kw = dim.CalcSize(new GUIContent(k)).x;
                var s = st ?? big; float w = s.CalcSize(new GUIContent(v)).x;
                GUI.Label(new Rect(x + kw + 6, 9, w + 4, 28), v, s); x += kw + w + 28;
            }
            big.fontSize = 20 + Mathf.RoundToInt(game.creditPulse * 6);
            float x0 = x;
            Item("돈", KNum.Fmt(shown));
            big.fontSize = 20;
            CreditScreen = new Vector2((x0 + 50) * scale, Screen.height - 22 * scale);
            // 연료
            bool live = !R.over;
            int f = live ? R.fuel : sim.FuelMax, fm = live ? R.max : sim.FuelMax;
            GUI.Label(new Rect(x, 14, 40, 20), "연료", dim);
            GUI.DrawTexture(new Rect(x + 34, 19, 140, 8), texBar);
            GUI.DrawTexture(new Rect(x + 34, 19, 140 * Mathf.Clamp01((float)f / Mathf.Max(1, fm)), 8), texAmber);
            GUI.Label(new Rect(x + 180, 12, 50, 22), f.ToString(), label); x += 232;
            Item("출동", S.runs.ToString(), label);
            Item("궤도", DebtSim.Orbits[S.orbit].name, label);
            if (!S.won && S.bill < DebtSim.Bills.Length)
                Item("청구서", DebtSim.Bills[S.bill].t + " " + KNum.Fmt(sim.BillAmount) + (S.overdue ? " <color=#ee7766>연체 · 추심 " + Mathf.RoundToInt((float)sim.Cut * 100) + "%</color>" : " · " + S.billDue + "판"), label);
            GUI.Label(new Rect(vw - 90, 14, 80, 20), "(" + sim.M.company + "대)", dim);
        }

        void Pops()
        {
            foreach (var p in game.pops)
            {
                Vector3 sp = game.cam.WorldToScreenPoint(game.PxToWorld(p.px.x, p.px.y));
                var c = p.c; c.a = 1 - p.age * p.age;
                pop.normal.textColor = c; pop.fontSize = Mathf.RoundToInt(p.size);
                GUI.Label(new Rect(sp.x / scale - 100, (Screen.height - sp.y) / scale - 12, 200, 24), p.text, pop);
            }
        }

        // ───────────────────────────────── 정비소 — 집

        void Shop()
        {
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 44, vw, RefH - 70), texDim);
            float y = 54;
            if (showResult)
            {
                GUI.Label(new Rect(20, y, 600, 26), "출동 " + S.runs + " — 귀환", big);
                GUI.Label(new Rect(20, y + 28, 900, 20), "부순 것 " + rBroke + " · 금고 " + rVault + " · 최대 연쇄 " + rChain + " · 벌어들인 것 +" + KNum.Fmt(rEarned) + (rCut > 0 ? "  <color=#ee7766>(추심 -" + KNum.Fmt(rCut) + ")</color>" : ""), label);
            }
            else GUI.Label(new Rect(20, y, 600, 26), "정비소", big);
            y += 56;

            // 🔴 출동 — 제일 크게
            if (GUI.Button(new Rect(20, y, 180, 50), "출동 ▸", bigBtn)) { showResult = false; sim.StartRun(); }
            float ox = 212;
            if (S.maxOrbit > 0)
                for (int i = 0; i <= S.maxOrbit; i++)
                {
                    var o = DebtSim.Orbits[i];
                    if (GUI.Button(new Rect(ox, y + 10, 118, 32), (i == S.orbit ? "▶ " : "") + o.name + " ×" + o.mult, i == S.orbit ? btn : btnOff)) sim.SetOrbit(i);
                    ox += 124;
                }

            // 청구서
            if (S.bill < DebtSim.Bills.Length)
            {
                var b = DebtSim.Bills[S.bill];
                var r = new Rect(vw - 360, y - 8, 340, 108);
                GUI.DrawTexture(r, texCard);
                GUI.DrawTexture(new Rect(r.x, r.y, 3, r.height), S.overdue ? texRed : texAmber);
                GUI.Label(new Rect(r.x + 12, r.y + 6, 320, 18), S.overdue ? "<color=#ee7766>연체 중 — 추심 " + Mathf.RoundToInt((float)sim.Cut * 100) + "%</color>" : "청구서 · 기한 " + S.billDue + "판", head);
                GUI.Label(new Rect(r.x + 12, r.y + 24, 200, 24), b.t, label);
                GUI.Label(new Rect(r.x + 12, r.y + 24, 316, 24), KNum.Fmt(sim.BillAmount), cost);
                GUI.Label(new Rect(r.x + 12, r.y + 46, 316, 18), "갚으면 → " + b.perk + " · 신용 +" + sim.CreditFor(b.m), small);
                bool can = S.cash >= sim.BillAmount;
                if (GUI.Button(new Rect(r.x + 12, r.y + 70, 90, 28), "갚기", can ? btn : btnOff) && can) sim.PayBill();
                if (sim.CanBankrupt && GUI.Button(new Rect(r.x + 110, r.y + 70, 218, 28), bankruptArmed ? "<color=#ffb3a8>한 번 더 → 파산</color>" : "<color=#ffb3a8>파산 선언 (신용 " + sim.M.credit + ")</color>", btn))
                {
                    if (bankruptArmed) { sim.Bankrupt(); showResult = false; bankruptArmed = false; } else bankruptArmed = true;
                }
            }
            y += 116;

            // 트리 — 가지 다섯
            float colW = (vw - 40) / 5f;
            for (int bi = 0; bi < DebtSim.Branches.Length; bi++)
            {
                string br = DebtSim.Branches[bi];
                float cx = 20 + bi * colW, cy = y;
                GUI.Label(new Rect(cx, cy, colW, 18), br + (br == "장비" ? " · 장착 " + S.equip.Count + "/" + S.slots : ""), head);
                cy += 20;
                for (int i = 0; i < DebtSim.Tree.Length; i++)
                {
                    var n = DebtSim.Tree[i];
                    if (n.branch != br) continue;
                    var rr = new Rect(cx, cy, colW - 8, 58);   // 50 은 두 줄 설명이 잘렸다
                    int lv = S.lv[i];
                    if (br == "장비" && lv > 0)
                    {
                        bool on = S.equip.Contains(i);
                        if (GUI.Button(rr, GUIContent.none, btn)) sim.ToggleEquip(i);
                        GUI.Label(new Rect(rr.x + 8, rr.y + 5, rr.width - 16, 20), n.name, label);
                        GUI.Label(new Rect(rr.x + 8, rr.y + 5, rr.width - 16, 20), on ? "<color=#6fcf97>장착</color>" : "빼둠", cost);
                    }
                    else
                    {
                        bool maxed = lv >= n.max; double c = sim.Cost(i); bool can = !maxed && S.cash >= c;
                        if (GUI.Button(rr, GUIContent.none, can ? btn : btnOff) && can) sim.Buy(i);
                        GUI.Label(new Rect(rr.x + 8, rr.y + 5, rr.width - 16, 20), n.name + (n.max > 1 ? " " + lv + "/" + n.max : ""), can ? label : dim);
                        GUI.Label(new Rect(rr.x + 8, rr.y + 5, rr.width - 16, 20), maxed ? "<color=#6fcf97>끝</color>" : KNum.Fmt(c), cost);
                    }
                    GUI.Label(new Rect(rr.x + 8, rr.y + 24, rr.width - 16, 32), n.desc, small);
                    cy += 62;
                }
            }
            y += 20 + 4 * 62 + 4;

            // 조종사 경력 — 파산해도 남는다
            if (sim.M.bankrupt > 0 || sim.M.credit > 0)
            {
                GUI.Label(new Rect(20, y, 600, 18), "조종사 경력 — 파산해도 남는다 · 신용 " + sim.M.credit, head);
                y += 20;
                float cw = (vw - 40) / 6f;
                for (int i = 0; i < DebtSim.Careers.Length; i++)
                {
                    var c = DebtSim.Careers[i]; int lv = sim.M.career[i];
                    bool can = lv < c.max && sim.M.credit >= c.cost;
                    var rr = new Rect(20 + i * cw, y, cw - 6, 46);
                    if (GUI.Button(rr, GUIContent.none, can ? btn : btnOff) && can) sim.BuyCareer(i);
                    GUI.Label(new Rect(rr.x + 6, rr.y + 4, rr.width - 12, 18), c.name + " " + lv + "/" + c.max, can ? label : dim);
                    GUI.Label(new Rect(rr.x + 6, rr.y + 24, rr.width - 12, 18), c.desc + (lv < c.max ? " · 신용 " + c.cost : ""), small);
                }
            }
        }

        // ───────────────────────────────── 빚 청산

        void Ending()
        {
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var M = sim.M;
            GUI.Label(new Rect(vw / 2 - 300, 130, 600, 50), "빚 청산", title);
            GUI.Label(new Rect(vw / 2 - 300, 190, 600, 24), "주식회사 궤도 청소부 (" + M.company + "대) — 청소선 할부를 다 갚았다.", pop);
            int min = Mathf.RoundToInt((float)M.playSeconds / 60f);
            GUI.Label(new Rect(vw / 2 - 300, 240, 600, 24), "걸린 시간 " + min + "분 · 출동 " + M.totalRuns + " · 파산 " + M.bankrupt + " · 부순 잔해 " + KNum.Fmt(M.broken) + " · 최대 연쇄 " + M.bestChain, pop);
            GUI.Label(new Rect(vw / 2 - 300, 290, 600, 24), "오늘도 궤도는 깨끗합니다.", pop);
            if (GUI.Button(new Rect(vw / 2 - 90, 350, 180, 44), "처음부터", bigBtn)) { game.WipeAll(); showResult = false; }
        }
    }
}
