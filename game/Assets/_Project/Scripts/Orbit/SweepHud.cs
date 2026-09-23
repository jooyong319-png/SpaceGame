using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// rev17 — 위 띠 · 결산 · 정비소(청구서 카드 · 노드 그래프 트리 · 상세) · 경력 · 궤도일보 · 엔딩. 전부 OnGUI (960×600 기준).
    /// 🔴 청구서 카드가 맨 위 한가운데 (rev16: 못 봤다) · [출동 ▸] 이 가장 큰 단추 · 이야기는 뉴스로만 (§11).
    /// </summary>
    public class SweepHud : MonoBehaviour
    {
        public SweepGame game;
        SweepSim sim => game.sim;

        const float RefH = 600f;
        float scale = 1f, vw = 960f, ox;
        public Vector2 CreditScreen = new Vector2(120, 580);
        public bool reduceMotion;
        public bool Blocking => sim != null && (sim.R.over || sim.M.careerOpen || sim.M.won || newsOpen || bayOpen);

        // 결산
        bool showResult, bankruptArmed; public bool newsOpen;
        int prevBestChain, prevBestPack, runNewsFrom;
        SweepRun last;
        double shown;
        int selected = 0, newsSel = -1; public int endStage;
        Vector2 newsScroll;
        // 알림
        string banner; int bannerKind; float bannerT;
        string paidText; float paidT; int paidBill; float resultT;
        float[] branchFlash = new float[4];
        float breakingT, tickT; int tickI; string breakingHead;
        readonly float[] nodePulse = new float[SweepSim.NodeCount];

        GUIStyle big, label, dim, small, cost, head, title, btn, btnOff, bigBtn, pop, paperHead, paperBody, paperSmall, center, chainSt;
        Texture2D texInk, white, texDim, texCard, texCard2, texRed, texAmber, texBar, texPaper, texDisc, texRing, texVignette;

        public void Banner(string s, int kind, float time) { banner = s.Replace("⚠", "!!"); bannerKind = kind; bannerT = time; }
        public void OnNews(string head, bool scoop) { breakingT = 6f; breakingHead = (scoop ? "특종 — " : "") + head; }
        public void OnBillPaid(string text, int billNo) { paidText = text; paidT = 3f; paidBill = billNo; if (billNo == 1) branchFlash[1] = 3f; if (billNo == 2) branchFlash[2] = 3f; }
        public void OnWon() { endStage = 0; newsOpen = false; }

        public void OnRunEnd()
        {
            showResult = true; bankruptArmed = false; last = sim.R; resultT = 2.6f; flow = 1;
            resultAt = Time.time; endCash = sim.S.cash; gained = last.Earned + last.bonus + last.interest; flyers.Clear(); flyT = 0;
            bannerT = 0; banner = null;          // 판 중 예고가 조종실까지 남지 않게
            sim.M.flags.Remove("hint_seen_now");
            if (!sim.M.flags.Contains("hint_claw")) sim.M.flags.Add("hint_claw");
            if (sim.DronesOn && !sim.M.flags.Contains("hint_drone")) sim.M.flags.Add("hint_drone");
            if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb")) sim.M.flags.Add("hint_bomb");
        }

        void Go()
        {
            bayOpen = false; flow = 0;
            prevBestChain = sim.M.bestChain; prevBestPack = sim.M.bestPack; runNewsFrom = sim.M.news.Count;
            showResult = false; bankruptArmed = false;
            sim.StartRun();
            OrbitSfx.Play("buy", 0.8f);
        }

        static Texture2D Tex(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }

        void Styles()
        {
            if (big != null && white != null) return;
            var font = Resources.Load<Font>("Galmuri11");
            GUIStyle S(int size, Color c, TextAnchor a = TextAnchor.UpperLeft)
            { var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = a, richText = true, wordWrap = false }; s.normal.textColor = c; return s; }
            var text = new Color(0.87f, 0.89f, 0.92f); var gray = new Color(0.51f, 0.56f, 0.64f);
            big = S(20, Color.white); label = S(14, text); dim = S(12, gray); small = S(11, gray); small.wordWrap = true;
            cost = S(13, SweepGame.Amber, TextAnchor.UpperRight); head = S(12, gray); title = S(34, SweepGame.Amber2, TextAnchor.MiddleCenter);
            pop = S(16, SweepGame.Amber, TextAnchor.MiddleCenter); center = S(13, text, TextAnchor.MiddleCenter); chainSt = S(30, SweepGame.Amber2, TextAnchor.MiddleCenter);
            var ink = new Color(0.11f, 0.1f, 0.09f);
            paperHead = S(24, ink); paperHead.wordWrap = true; paperBody = S(14, ink); paperBody.wordWrap = true; paperSmall = S(11, new Color(0.35f, 0.33f, 0.28f));
            white = Texture2D.whiteTexture;
            texDim = Tex(new Color(0.02f, 0.027f, 0.047f, 0.94f)); texCard = Tex(new Color(0.05f, 0.07f, 0.1f)); texCard2 = Tex(new Color(0.08f, 0.1f, 0.15f));
            texRed = Tex(SweepGame.Red); texAmber = Tex(SweepGame.Amber); texBar = Tex(new Color(0.08f, 0.11f, 0.16f)); texPaper = Tex(new Color(0.945f, 0.93f, 0.894f)); texInk = Tex(new Color(0.11f, 0.1f, 0.09f));
            texDisc = SweepGame.Ring(64, 0f).texture; texRing = SweepGame.Ring(64, 0.82f).texture;
            texVignette = new Texture2D(64, 64) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float dx = (x - 31.5f) / 32f, dy = (y - 31.5f) / 32f; float r = Mathf.Sqrt(dx * dx + dy * dy); texVignette.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((r - 0.55f) / 0.5f))); }
            texVignette.Apply();
            btn = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4), richText = true };
            btn.normal.background = Tex(new Color(0.08f, 0.11f, 0.16f)); btn.hover.background = Tex(new Color(0.12f, 0.16f, 0.22f)); btn.active.background = Tex(new Color(0.16f, 0.21f, 0.29f));
            btn.normal.textColor = btn.hover.textColor = btn.active.textColor = text;
            btnOff = new GUIStyle(btn); btnOff.hover.background = btnOff.active.background = btnOff.normal.background;
            btnOff.normal.textColor = btnOff.hover.textColor = btnOff.active.textColor = gray;
            bigBtn = new GUIStyle(btn) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            bigBtn.normal.background = Tex(new Color(0.24f, 0.18f, 0.05f)); bigBtn.hover.background = Tex(new Color(0.34f, 0.25f, 0.07f));
            bigBtn.normal.textColor = bigBtn.hover.textColor = SweepGame.Amber2;
        }

        void Update()
        {
            if (sim == null) return;
            double a = sim.S.cash;
            shown = a < shown ? a : shown + (a - shown) * (1 - Mathf.Exp(-Time.deltaTime * 7f));
            if (a - shown < 1) shown = a;
            float dt = Time.deltaTime;
            bannerT -= dt; paidT -= dt; breakingT -= dt; tickT -= dt; resultT -= dt;
            for (int i = 0; i < 4; i++) branchFlash[i] = Mathf.Max(0, branchFlash[i] - dt);
            for (int i = 0; i < nodePulse.Length; i++) nodePulse[i] = Mathf.Max(0, nodePulse[i] - dt * 3);
            if (tickT <= 0) { tickT = 8f; tickI++; }
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && sim.R.over && !sim.M.careerOpen && !sim.M.won && !newsOpen && paidT < 2.4f)
            {
                Go();          // 결산에서도 정비고에서도 Space = 계속
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { if (newsOpen) newsOpen = false; else bayOpen = false; }
        }

        void OnGUI()
        {
            if (sim == null) return;
            Styles();
            scale = Screen.height / RefH; vw = Screen.width / scale; ox = Mathf.Max(0, (vw - 960) / 2);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            Effects();
            if (!sim.R.over) { WindowEdge(); Pops(); RunHud(); }
            if (sim.M.won) Ending();
            else if (sim.M.careerOpen) Career();
            else if (sim.R.over)
            {
                if (flow == 0) flow = 3;
                if (flow == 2) flow = 3;
                if (flow == 1) FlowResult(); else { Bay(); FlowBottom(); }
            }
            if (newsOpen) News();
            if (!sim.M.won) Ticker();
        }

        // ───────────────────────────────── 연출 (도파민 사다리 §5)

        void Effects()
        {
            if (game.edgeGlow > 0.01f && !reduceMotion) { GUI.color = new Color(1f, 0.76f, 0.3f, game.edgeGlow * 0.22f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette); }
            if (game.flash > 0.01f) { GUI.color = new Color(1f, 0.97f, 0.9f, game.flash * 0.7f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); }
            GUI.color = Color.white;
            var R = sim.R;
            if (!R.over && R.chain >= 10 && R.chainT > 0)
            {
                int tier = R.chain >= 200 ? 4 : R.chain >= 80 ? 3 : R.chain >= 30 ? 2 : 1;
                chainSt.fontSize = new[] { 0, 28, 40, 54, 72 }[tier];
                chainSt.normal.textColor = tier >= 3 ? new Color(1f, 0.96f, 0.84f, Mathf.Min(1, (float)R.chainT * 2)) : new Color(1f, 0.87f, 0.58f, Mathf.Min(1, (float)R.chainT * 2));
                GUI.Label(new Rect(vw / 2 - 300, 70, 600, 80), "연쇄 ×" + R.chain, chainSt);
            }
            if (game.kessT > 0)
            {
                chainSt.fontSize = game.kessText == "케슬러!" ? 30 : 44; chainSt.normal.textColor = new Color(1f, 0.6f, 0.3f, Mathf.Min(1, game.kessT * 1.5f));
                GUI.Label(new Rect(vw / 2 - 300, 142, 600, 60), game.kessText, chainSt);
            }
            if (bannerT > 0 && banner != null && !sim.R.over)
            {
                var c = bannerKind == -1 ? SweepGame.Amber2 : new Color(1f, 0.55f, 0.48f, Mathf.Sin(Time.time * 12) > -0.3f ? 1 : 0.55f);
                pop.fontSize = 17; pop.normal.textColor = c;
                GUI.Label(new Rect(vw / 2 - 300, 196, 600, 26), banner, pop);
                if (bannerKind == 0) GUI.Label(new Rect(8, RefH / 2 - 14, 30, 28), "◀", pop);
                if (bannerKind == 1) GUI.Label(new Rect(vw - 36, 110, 30, 28), "▶", pop);
            }
        }

        /// <summary>출동 중에도 「창으로 내다본다」 — 가장자리에 옅은 선체와 창틀 모서리 (사장님 09-23)</summary>
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

        void Pops()
        {
            foreach (var p in game.pops)
            {
                Vector3 sp = game.cam.WorldToScreenPoint(game.PxToWorld(p.px.x, p.px.y));
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
            big.fontSize = 22 + Mathf.RoundToInt(game.creditPulse * 6);
            float x0 = x;
            Item("돈", KNum.Fmt(shown));
            big.fontSize = 20;
            CreditScreen = new Vector2((x0 + 50) * scale, Screen.height - 22 * scale);
            if (R.clean)
            {
                Item("궤도 청소율", Mathf.Min(100, Mathf.FloorToInt(100f * R.cleanKills / R.cleanGoal)) + "%", big);
            }
            else if (S.bill < SweepSim.Bills.Length)
                Item("청구서", KNum.Fmt(sim.BillAmount) + (S.overdue ? " <color=#ee7766>연체 · 추심 " + Mathf.RoundToInt((float)sim.Cut * 100) + "%</color>" : " · " + S.billDue + "판"), label);
            GUI.Label(new Rect(x, 14, 40, 20), "연료", dim);
            GUI.DrawTexture(new Rect(x + 34, 19, 160, 9), texBar);
            float fk = Mathf.Clamp01((float)(R.fuel / R.max));
            GUI.DrawTexture(new Rect(x + 34, 19, 160 * fk, 9), R.fuel < 6 ? texRed : texAmber);
            x += 210;
            if (R.maxShots > 0)
            {
                GUI.Label(new Rect(x, 14, 40, 20), "폭탄", dim);
                for (int i = 0; i < R.maxShots; i++) { GUI.color = i < R.shots ? SweepGame.Violet : new Color(0.17f, 0.18f, 0.24f); GUI.DrawTexture(new Rect(x + 34 + i * 15, 18, 11, 11), texDisc); }
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(vw - 330, 14, 316, 20), "주식회사 궤도 청소부 (" + sim.M.company + "대) · " + SweepSim.Orbits[S.orbit].name, cost);
            if (R.holding)
            {
                float k = Mathf.Clamp01((float)R.packed.Count / Mathf.Max(1, sim.Cap));
                GUI.DrawTexture(new Rect(vw - 200, 42, 186, 8), texBar);
                GUI.color = k > 0.8f ? SweepGame.Red : SweepGame.Violet; GUI.DrawTexture(new Rect(vw - 200, 42, 186 * k, 8), white); GUI.color = Color.white;
                GUI.Label(new Rect(vw - 330, 52, 316, 18), "압축 " + R.packed.Count + " / 붕괴 " + sim.Cap, cost);
            }
            var c = sim.CurContract;
            if (c != null && !R.clean)
            {
                int pr = sim.ContractProgress(R); bool ok = pr >= c.Value.target;
                GUI.Label(new Rect(14, RefH - 46, 400, 18), "의뢰: " + c.Value.text + "  " + (ok ? "<color=#6fcf97>성공!</color>" : Mathf.Min(pr, c.Value.target) + " / " + c.Value.target), dim);
            }
            // 첫 5분 — 새 장난감마다 한 줄씩만 (§10)
            string hint = null;
            if (R.clean) hint = null; else
            if (!sim.M.flags.Contains("hint_claw") && R.t < 12) hint = "궤도 위에 커서를 대면 청소선이 빔을 쏜다 — 처음엔 한 점씩";
            else if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb") && R.t < 14) hint = "지구에서 폭탄이 올라온다 — 누르고 있으면 빨아들이고, 떼면 모인 만큼 터진다 (모으는 동안 빔은 쉰다)";
            else if (sim.DronesOn && !sim.M.flags.Contains("hint_drone") && R.t < 8) hint = "드론은 알아서 줍는다 — 한 방에 부서지는 것만";
            if (hint != null) GUI.Label(new Rect(vw / 2 - 360, RefH - 70, 720, 20), hint, center);
            if (game.timeScale > 1) GUI.Label(new Rect(vw - 120, RefH - 46, 106, 18), "시험 속도 ×3", cost);
        }

        // ───────────────────────────────── 조종실 — 첫 화면 (사장님 09-23: "첫 화면 자체를 우주선 화면 컨셉으로 · 유저 친화적으로")
        // 가운데 창 = 지금 내 궤도 (사면 바로 창밖에 보인다) · 계기판마다 할 일 하나 · 강화는 정비고(네 칸 · 36칸)

        public bool bayOpen; int bayTab;
        public bool CockpitView => false;       // 조종실 화면은 뺐다 — 결산 → 청구서 → 정비고 한 줄 흐름으로 (09-23)
        public int flow;                         // 0 출동 중 · 1 결산 · 2 청구서 · 3 정비고
        static readonly Color[] BranchCol = { SweepGame.Amber, SweepGame.Cyan, SweepGame.Violet, SweepGame.Green };
        static readonly string[] BayDesc = { "조준점 하나 → 넓은 착탄 → 한 번에 여럿", "알아서 줍는다 — 한 방에 부서지는 것만", "지구에서 올려 보낸다 — 폭탄과 연료", "돈 · 청구서 · 추심 · 기사" };
        public static readonly Rect Win = new Rect(200, 44, 560, 344);

        static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        void Frame(Rect r, Color c, float w)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), white); GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), white);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), white); GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), white);
            GUI.color = Color.white;
        }

        bool Panel(Rect r, string label, string right, Color edge)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            GUI.DrawTexture(r, texCard2);
            Frame(r, hover ? edge : new Color(0.14f, 0.2f, 0.28f), hover ? 2 : 1.5f);
            GUI.Label(new Rect(r.x + 9, r.y + 6, r.width - 18, 16), label, head);
            if (right != null) GUI.Label(new Rect(r.x + 9, r.y + 5, r.width - 18, 16), right, cost);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void Hull()
        {
            float L = ox + Win.x, Rr = ox + Win.xMax;
            GUI.color = new Color(0.043f, 0.067f, 0.1f);
            GUI.DrawTexture(new Rect(0, 0, vw, Win.y), white);
            GUI.DrawTexture(new Rect(0, Win.y, L, RefH - Win.y), white);
            GUI.DrawTexture(new Rect(Rr, Win.y, vw - Rr, RefH - Win.y), white);
            GUI.color = new Color(0.06f, 0.09f, 0.135f);
            GUI.DrawTexture(new Rect(L, Win.yMax, Rr - L, RefH - Win.yMax), white);
            Frame(new Rect(L - 7, Win.y - 7, Win.width + 14, Win.height + 14), new Color(0.16f, 0.23f, 0.32f), 7);
            Frame(new Rect(L - 1, Win.y - 1, Win.width + 2, Win.height + 2), new Color(0.25f, 0.33f, 0.45f), 1);
            GUI.color = new Color(0.2f, 0.27f, 0.36f);
            for (int i = 0; i <= 16; i++) { float x = L + Win.width * i / 16f; GUI.DrawTexture(new Rect(x - 2, Win.y - 16, 4, 4), texDisc); GUI.DrawTexture(new Rect(x - 2, Win.yMax + 12, 4, 4), texDisc); }
            GUI.color = Color.white;
        }

        void Cockpit()
        {
            var S = sim.S; var M = sim.M;
            Hull();
            // 위 띠 — 돈 · 회사
            big.fontSize = 22 + Mathf.RoundToInt(game.creditPulse * 6);
            GUI.Label(new Rect(ox + 14, 12, 40, 20), "돈", dim);
            GUI.Label(new Rect(ox + 36, 7, 240, 30), KNum.Fmt(shown), big);
            big.fontSize = 20;
            CreditScreen = new Vector2((ox + 60) * scale, Screen.height - 22 * scale);
            GUI.Label(new Rect(ox + 560, 13, 386, 20), "주식회사 궤도 청소부 (" + M.company + "대) · 출동 " + S.runs + " · " + SweepSim.Orbits[S.orbit].name, cost);

            // 창 — 지금 내 궤도
            if (paidT > 0 && paidBill > 0)
            {
                var pb = SweepSim.Bills[paidBill - 1];
                title.fontSize = 26; GUI.Label(new Rect(ox + Win.x, Win.y + 18, Win.width, 36), "<color=#ffdf95>납부 완료</color> · " + pb.t, title);
                title.fontSize = 18; GUI.Label(new Rect(ox + Win.x, Win.y + 54, Win.width, 26), pb.perk, title);
            }
            if (resultT > 0 && last != null) ReturnCard();
            else if (CanPayNow && paidT <= 0) PayPrompt();
            GUI.Label(new Rect(ox + Win.x, Win.yMax - 22, Win.width, 18), "창밖 = 지금 내 궤도 · 칸을 살수록 궤도가 넓어진다 (" + Mathf.RoundToInt((float)(sim.Widen - 1) * 100) + "%)", center);

            BillTerminal(new Rect(ox + 12, 44, 180, 176));
            Report(new Rect(ox + 12, 228, 180, 160));
            NewsMonitor(new Rect(ox + 768, 44, 180, 132));
            Plate(new Rect(ox + 768, 184, 180, 124));
            reduceMotion = GUI.Toggle(new Rect(ox + 770, 316, 178, 20), reduceMotion, " 움직임 줄이기", small);

            // 아래 조종대 — 정비고 해치 · 출동 · 궤도 다이얼
            Hatch(new Rect(ox + 12, 400, 300, 150));
            string goText = M.cleanReady ? "청산 출동 ▸" : CanPayNow ? "갚고 출동 ▸" : "출동 ▸";
            if (GUI.Button(new Rect(ox + 330, 408, 300, 104), goText, bigBtn) && paidT < 2.4f) { if (CanPayNow) sim.PayBill(); Go(); }
            GUI.Label(new Rect(ox + 330, 516, 300, 16), "Space 로도 · 한 판 " + Mathf.RoundToInt((float)sim.FuelMax) + "초", center);
            Dial(new Rect(ox + 648, 400, 300, 150));
        }

        // ───────────────────────────────── ① 결산 · ② 청구서 · ③ 정비고 아래 줄

        Rect CardRect(float w, float h) => new Rect(vw / 2 - w / 2, RefH / 2 - h / 2 - 10, w, h);

        // ── 결산 화면 — Bills Must Be Paid 결산 틀 (사장님이 보여 주신 화면): 제목 · 왼쪽 성적과 합계 · 오른쪽 부순 것 · 다음 해금 · 아래 버튼 셋
        float resultAt, flyT; double endCash, gained;
        class Flyer { public Vector2 a, b; public float t; public string txt; }
        readonly List<Flyer> flyers = new List<Flyer>();

        void Panel2(Rect r, string head2)
        {
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.16f, 0.2f, 0.27f), 1.5f);
            if (head2 != null) GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, 18), head2, head);
        }

        void FlowResult()
        {
            var S = sim.S; var R = last;
            GUI.color = new Color(0, 0, 0, 0.72f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            float t = Time.time - resultAt, tally = Mathf.Clamp01((t - 0.35f) / 1.3f);
            float cx = vw / 2;

            // 돈 칸 — 오른쪽 위. 합계가 여기로 날아와 더해진다
            var money = new Rect(vw - 230, 14, 210, 44);
            GUI.color = new Color(0.14f, 0.12f, 0.08f, 0.95f); GUI.DrawTexture(money, white); GUI.color = Color.white;
            Frame(money, SweepGame.Amber * new Color(1, 1, 1, 0.6f), 1.5f);
            big.fontSize = 26 + Mathf.RoundToInt(game.creditPulse * 6);
            GUI.Label(new Rect(money.x + 12, money.y + 6, money.width - 24, 34), "<color=#ffdf95>" + KNum.Fmt(endCash - gained * (1 - tally)) + "</color>", big);
            big.fontSize = 20;

            // 제목
            title.fontSize = 40;
            GUI.Label(new Rect(cx - 300, 70, 600, 52), "연료 바닥!", title);

            // 왼쪽 — 이번 출동: 부순 것 종류별 · 합계
            var L = new Rect(cx - 420, 140, 410, 250);
            Panel2(L, null);
            float y = L.y + 14;
            void Line2(string k, string v) { GUI.Label(new Rect(L.x + 18, y, L.width - 36, 26), "<size=18>" + k + "</size>", label); GUI.Label(new Rect(L.x + 18, y, L.width - 36, 26), "<size=18>" + v + "</size>", cost); y += 34; }
            Line2("부순 것", Mathf.RoundToInt(R.broke * tally).ToString());
            // 종류별 아이콘 줄
            int[] counts = { R.cChip, R.cSat, R.cFuel, R.cVault, R.cTank, R.cBig };
            int[] kinds = { SweepSim.Chip, SweepSim.Sat, SweepSim.Rocket, SweepSim.Vault, SweepSim.Tank, SweepSim.Big };
            float ix = L.x + 22;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0) continue;
                GUI.color = SweepGame.JunkColor(kinds[i]); GUI.DrawTexture(new Rect(ix, y + 4, 16, 16), kinds[i] == SweepSim.Chip || kinds[i] == SweepSim.Rocket ? white : texDisc); GUI.color = Color.white;
                GUI.Label(new Rect(ix + 20, y + 2, 60, 20), Mathf.RoundToInt(counts[i] * tally).ToString(), label);
                ix += 70;
            }
            y += 32;
            Line2("최대 연쇄", R.chainBest + (R.chainBest > prevBestChain && R.chainBest >= 10 ? " <color=#ff8a7a>새 기록!</color>" : ""));
            y += 6;
            GUI.color = new Color(0.16f, 0.2f, 0.27f); GUI.DrawTexture(new Rect(L.x + 18, y, L.width - 36, 1.5f), white); GUI.color = Color.white;
            y += 12;
            var totalPos = new Vector2(L.xMax - 60, y + 14);
            GUI.Label(new Rect(L.x + 18, y, L.width - 36, 32), "<size=24>합계</size>", label);
            GUI.Label(new Rect(L.x + 18, y, L.width - 36, 32), "<size=26><color=#6fcf97>+" + KNum.Fmt(gained * tally) + "</color></size>", cost);

            // 합계 → 돈 칸으로 날아가는 「+」
            if (tally < 1)
            {
                flyT -= Time.deltaTime;
                if (flyT <= 0 && gained > 0) { flyT = 0.045f; flyers.Add(new Flyer { a = totalPos + new Vector2(Random.Range(-20f, 20f), 0), b = new Vector2(money.x + 40, money.center.y), txt = "+" + KNum.Fmt(System.Math.Max(1, System.Math.Round(gained / 30))) }); OrbitSfx.Play("coin", 0.25f, 0.04f, 0.15f); }
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i]; f.t += Time.deltaTime * 1.6f;
                if (f.t >= 1) { flyers.RemoveAt(i); game.creditPulse = 1; continue; }
                float e = f.t * f.t * (3 - 2 * f.t);
                var p = Vector2.Lerp(f.a, f.b, e) + new Vector2(0, -Mathf.Sin(e * Mathf.PI) * 60);
                pop.fontSize = 14; pop.normal.textColor = new Color(1f, 0.93f, 0.7f, 1 - f.t * 0.3f);
                GUI.Label(new Rect(p.x - 40, p.y - 10, 80, 20), f.txt, pop);
            }

            // 오른쪽 위 — 어디서 벌었나 · 의뢰
            var RT = new Rect(cx + 10, 140, 410, 120);
            Panel2(RT, "어디서 벌었나");
            double tot = System.Math.Max(1, R.Earned);
            float a = (float)(R.earnClaw / tot), b = (float)(R.earnDrone / tot);
            float bw = RT.width - 36, bx = RT.x + 18, by = RT.y + 38;
            GUI.DrawTexture(new Rect(bx, by, bw, 12), texBar);
            GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(bx, by, bw * a * tally, 12), white);
            GUI.color = SweepGame.Cyan; GUI.DrawTexture(new Rect(bx + bw * a, by, bw * b * tally, 12), white);
            GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(bx + bw * (a + b), by, bw * (1 - a - b) * tally, 12), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by + 16, bw, 18), "<color=#f2c14e>빔 " + Mathf.RoundToInt(a * 100) + "%</color>   " + (sim.DronesOn ? "<color=#6fd3e8>드론 " + Mathf.RoundToInt(b * 100) + "%</color>   " : "") + (sim.BombsOn ? "<color=#b69cff>폭발 " + Mathf.RoundToInt((1 - a - b) * 100) + "%</color>" : ""), label);
            if (R.contractText != null) GUI.Label(new Rect(bx, by + 44, bw, 20), "의뢰 · " + R.contractText + "  " + (R.contractOk ? "<color=#6fcf97>성공 +" + KNum.Fmt(R.bonus) + "</color>" : "<color=#ee7766>실패 " + R.contractProg + "/" + R.contractTarget + "</color>"), label);
            else if (R.cut > 0) GUI.Label(new Rect(bx, by + 44, bw, 20), "<color=#ee7766>추심으로 떼인 것 -" + KNum.Fmt(R.cut) + "</color>", label);

            // 오른쪽 아래 — 다음 해금 (청구서를 갚으면 열리는 것)
            var RB = new Rect(cx + 10, 270, 410, 120);
            Panel2(RB, sim.S.bill < SweepSim.Bills.Length ? "청구서를 갚으면 열린다" : null);
            if (S.bill < SweepSim.Bills.Length)
            {
                float prog = Mathf.Clamp01((float)(S.cash / System.Math.Max(1, sim.BillAmount)));
                var sil = new Rect(RB.x + 18, RB.y + 34, 72, 72);
                GUI.color = new Color(0.1f, 0.12f, 0.16f); GUI.DrawTexture(sil, texDisc);
                GUI.color = prog >= 1 ? SweepGame.Green : new Color(0.3f, 0.34f, 0.42f); GUI.DrawTexture(sil, texRing); GUI.color = Color.white;
                title.fontSize = 22; GUI.Label(sil, prog >= 1 ? "<color=#6fcf97>!</color>" : "?", title);
                GUI.Label(new Rect(sil.xMax + 14, RB.y + 38, RB.width - 120, 40), "<size=13>" + SweepSim.Bills[S.bill].perk + "</size>", label);
                GUI.DrawTexture(new Rect(sil.xMax + 14, RB.y + 84, RB.width - 124, 8), texBar);
                GUI.color = prog >= 1 ? SweepGame.Green : SweepGame.Amber; GUI.DrawTexture(new Rect(sil.xMax + 14, RB.y + 84, (RB.width - 124) * prog, 8), white); GUI.color = Color.white;
                GUI.Label(new Rect(sil.xMax + 14, RB.y + 94, RB.width - 124, 16), "<size=11>" + Mathf.RoundToInt(prog * 100) + "%</size>", small);
            }

            // 아래 버튼 셋 — [업그레이드] [청구서] [계속]
            float w = 250, gap = 16, x0 = cx - (w * 3 + gap * 2) / 2, yb = 420;
            if (GUI.Button(new Rect(x0, yb, w, 66), "업그레이드", bigBtn)) flow = 3;
            BillButton(new Rect(x0 + w + gap, yb, w, 66));
            if (GUI.Button(new Rect(x0 + (w + gap) * 2, yb, w, 66), "계속 ▸", bigBtn)) Go();
            GUI.Label(new Rect(x0 + (w + gap) * 2, yb + 68, w, 16), "<size=10>Space</size>", center);
        }

        /// <summary>빨간 청구서 버튼 — 금액 · 기한. 돈이 되면 누르는 즉시 납부</summary>
        void BillButton(Rect r)
        {
            var S = sim.S;
            if (paidT > 0 && paidBill > 0)
            {
                GUI.color = new Color(0.1f, 0.3f, 0.18f); GUI.DrawTexture(r, white); GUI.color = Color.white; Frame(r, SweepGame.Green, 2);
                GUI.Label(new Rect(r.x, r.y + 8, r.width, 26), "<size=20><color=#6fcf97>납부 완료!</color></size>", center);
                GUI.Label(new Rect(r.x + 6, r.y + 36, r.width - 12, 22), "<size=11>" + SweepSim.Bills[paidBill - 1].perk + "</size>", center);
                return;
            }
            if (sim.M.cleanReady || S.bill >= SweepSim.Bills.Length)
            {
                GUI.color = new Color(0.1f, 0.3f, 0.18f); GUI.DrawTexture(r, white); GUI.color = Color.white;
                GUI.Label(r, "<size=20><color=#6fcf97>빚 청산</color></size>", center);
                return;
            }
            bool can = S.cash >= sim.BillAmount;
            float pulse = can ? 0.5f + 0.5f * Mathf.Sin(Time.time * 5) : 0;
            GUI.color = can ? Color.Lerp(new Color(0.42f, 0.1f, 0.1f), new Color(0.6f, 0.16f, 0.14f), pulse) : new Color(0.3f, 0.08f, 0.09f);
            GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, can ? Color.Lerp(SweepGame.Red, Color.white, pulse * 0.5f) : new Color(0.5f, 0.2f, 0.18f), 2);
            GUI.Label(new Rect(r.x, r.y + 6, r.width, 30), "<size=24><color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color></size>", center);
            string sub = S.overdue ? "<color=#ffb3a8>연체 중</color>" : S.billDue + "판 남음";
            if (can) sub += " · <color=#ffffff>눌러서 갚기</color>";
            GUI.Label(new Rect(r.x, r.y + 38, r.width, 22), "<size=13>" + sub + "</size>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && can) sim.PayBill();
        }

        void FlowBottom()
        {
            var S = sim.S; var M = sim.M;
            float y = 512;
            // 궤도
            float bx = ox + 16;
            if (!M.cleanReady)
                for (int i = 0; i <= 2; i++)
                {
                    var o = SweepSim.Orbits[i]; var br = new Rect(bx, y, 112, 34);
                    if (i > sim.MaxOrbit) { GUI.DrawTexture(br, texCard); GUI.Label(br, "<size=11>" + o.name + " · 잠김</size>", center); }
                    else
                    {
                        if (GUI.Button(br, "<size=12>" + o.name + " ×" + o.mult + "</size>", i == S.orbit ? btn : btnOff)) sim.SetOrbit(i);
                        if (i == S.orbit) Frame(br, SweepGame.Amber, 2);
                    }
                    bx += 118;
                }
            // 의뢰
            var c = sim.CurContract;
            if (c != null && !M.cleanReady)
            {
                GUI.Label(new Rect(ox + 16, y + 40, 420, 18), "이번 의뢰: <color=#dde3ea>" + c.Value.text + "</color> — 성공하면 수입 +" + (25 + 10 * sim.Lv("e_quest")) + "%", dim);
                if (!S.rerolled && GUI.Button(new Rect(ox + 380, y + 38, 60, 22), "<size=11>바꾸기</size>", btnOff)) sim.Reroll();
            }
            // 파산 — 구석에 작게, 두 번 눌러야
            if (sim.CanBankrupt && GUI.Button(new Rect(ox + 470, y, 210, 30), bankruptArmed ? "<color=#ffb3a8><size=12>정말? 한 번 더 누르면 파산</size></color>" : "<color=#ffb3a8><size=12>파산하기… (신용 +" + S.creditPending + ")</size></color>", btnOff))
            {
                if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; } else bankruptArmed = true;
            }
            // 출동
            if (GUI.Button(new Rect(ox + 700, y, 246, 62), M.cleanReady ? "청산 출동 ▸" : "출동 ▸", bigBtn) && paidT < 2.4f) Go();
            GUI.Label(new Rect(ox + 700, y + 64, 246, 14), "<size=10>Space · 한 판 " + Mathf.RoundToInt((float)sim.FuelMax) + "초</size>", center);
        }

        bool CanPayNow => !sim.M.cleanReady && sim.S.bill < SweepSim.Bills.Length && sim.S.cash >= sim.BillAmount;

        /// <summary>🔴 갚을 수 있으면 창 가운데에 크게 — 사장님이 돈 357 을 들고 30짜리 첫 청구서를 안 갚으셨다 (09-23 첫 플레이)</summary>
        void PayPrompt()
        {
            var b = SweepSim.Bills[sim.S.bill];
            var r = new Rect(ox + Win.x + 70, Win.yMax - 132, Win.width - 140, 96);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
            GUI.color = new Color(1, 1, 1, 0.93f); GUI.DrawTexture(r, texCard2); GUI.color = Color.white;
            Frame(r, Color.Lerp(SweepGame.Green, Color.white, pulse * 0.4f), 2);
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 20), "<color=#6fcf97>청구서를 갚을 수 있다</color> · " + b.t + " " + KNum.Fmt(sim.BillAmount) + (sim.S.overdue ? "  <color=#ff8a7a>(연체 중 — 추심 30%)</color>" : ""), center);
            GUI.Label(new Rect(r.x, r.y + 28, r.width, 20), "갚으면 → <color=#ffdf95>" + b.perk + "</color>", center);
            if (GUI.Button(new Rect(r.center.x - 90, r.y + 52, 180, 36), "<color=#6fcf97>갚기</color>", bigBtn)) sim.PayBill();
        }

        /// <summary>귀환 직후 2.6초 — 창 가운데에 크게 (§9-1)</summary>
        void ReturnCard()
        {
            var R = last;
            float k = Mathf.Clamp01(resultT / 0.35f), grow = Mathf.Clamp01((2.6f - resultT) / 0.25f);
            var r = new Rect(ox + Win.x + 90, Win.y + 70, Win.width - 180, 170);
            GUI.color = new Color(1, 1, 1, k * 0.94f);
            GUI.DrawTexture(r, texCard2);
            GUI.color = new Color(1, 1, 1, k);
            Frame(r, SweepGame.Amber, 2);
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 20), "출동 " + sim.S.runs + " — 귀환", center);
            title.fontSize = 34;
            double earned = (R.Earned + R.bonus + R.interest) * grow;
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 44), "<color=#6fcf97>+" + KNum.Fmt(earned) + "</color>", title);
            double tot = System.Math.Max(1, R.Earned);
            float a = (float)(R.earnClaw / tot), b = (float)(R.earnDrone / tot);
            float bw = r.width - 80, bx = r.x + 40, by = r.y + 84;
            GUI.DrawTexture(new Rect(bx, by, bw, 10), texBar);
            GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(bx, by, bw * a * grow, 10), white);
            GUI.color = SweepGame.Cyan; GUI.DrawTexture(new Rect(bx + bw * a, by, bw * b * grow, 10), white);
            GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(bx + bw * (a + b), by, bw * (1 - a - b) * grow, 10), white);
            GUI.color = new Color(1, 1, 1, k);
            GUI.Label(new Rect(r.x, by + 14, r.width, 18), "<color=#f2c14e>빔 " + Mathf.RoundToInt(a * 100) + "</color> · <color=#6fd3e8>드론 " + Mathf.RoundToInt(b * 100) + "</color> · <color=#b69cff>폭발 " + Mathf.RoundToInt((1 - a - b) * 100) + "</color> %  ·  부순 것 " + R.broke, center);
            string rec = "최대 연쇄 " + R.chainBest + (R.chainBest > prevBestChain && R.chainBest >= 10 ? " <color=#ff8a7a>새 기록!</color>" : "") + "   최대 압축 " + R.packBest + (R.packBest > prevBestPack && R.packBest >= 5 ? " <color=#ff8a7a>새 기록!</color>" : "");
            GUI.Label(new Rect(r.x, by + 34, r.width, 18), rec, center);
            if (R.contractText != null) GUI.Label(new Rect(r.x, by + 52, r.width, 18), R.contractOk ? "<color=#6fcf97>의뢰 · " + R.contractText + " 성공 +" + KNum.Fmt(R.bonus) + "</color>" : "<color=#ee7766>의뢰 · " + R.contractText + " 실패 (" + R.contractProg + "/" + R.contractTarget + ")</color>", center);
            else if (R.cut > 0) GUI.Label(new Rect(r.x, by + 52, r.width, 18), "<color=#ee7766>추심으로 떼인 것 -" + KNum.Fmt(R.cut) + "</color>", center);
            GUI.color = Color.white;
        }

        void BillTerminal(Rect r)
        {
            var S = sim.S;
            if (sim.M.cleanReady)
            {
                Panel(r, "청구서 단말", "완납", SweepGame.Amber);
                title.fontSize = 26; GUI.Label(new Rect(r.x, r.y + 40, r.width, 40), "빚 청산", title);
                GUI.Label(new Rect(r.x + 8, r.y + 90, r.width - 16, 60), "남은 건 청산 출동 한 번 — 궤도를 전부 치운다", small);
                return;
            }
            if (S.bill >= SweepSim.Bills.Length) return;
            var b = SweepSim.Bills[S.bill];
            bool can = S.cash >= sim.BillAmount, last1 = !S.overdue && S.billDue <= 1;
            Color edge = S.overdue ? SweepGame.Red : last1 ? SweepGame.Orange : can ? SweepGame.Green : SweepGame.Amber;
            string right = S.overdue ? "<color=#ff8a7a>● 연체</color>" : last1 ? "<color=#ff9a4d>● 마지막 판</color>" : "기한 " + S.billDue + "판";
            Panel(r, "청구서 단말 · " + (S.bill + 1) + "/8", right, edge);
            Frame(new Rect(r.x, r.y, 3, r.height), edge, 3);
            GUI.Label(new Rect(r.x + 10, r.y + 24, r.width - 20, 20), b.t, label);
            title.fontSize = 26; title.alignment = TextAnchor.UpperLeft;
            GUI.Label(new Rect(r.x + 10, r.y + 42, r.width - 20, 32), KNum.Fmt(sim.BillAmount), title);
            title.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(r.x + 10, r.y + 76, r.width - 20, 32), "갚으면 → <color=#ffdf95>" + b.perk + "</color>", small);
            float k = Mathf.Clamp01((float)(S.cash / Mathf.Max(1, (float)sim.BillAmount)));
            GUI.DrawTexture(new Rect(r.x + 10, r.y + 112, r.width - 20, 6), texBar);
            GUI.color = can ? SweepGame.Green : SweepGame.Amber; GUI.DrawTexture(new Rect(r.x + 10, r.y + 112, (r.width - 20) * k, 6), white); GUI.color = Color.white;
            if (S.overdue) GUI.Label(new Rect(r.x + 10, r.y + 120, r.width - 20, 16), "추심 " + Mathf.RoundToInt((float)sim.Cut * 100) + "% · 판마다 연체료", small);
            else if (last != null && !can && last.Earned > 0) GUI.Label(new Rect(r.x + 10, r.y + 120, r.width - 20, 16), "앞으로 약 " + Mathf.CeilToInt((float)((sim.BillAmount - S.cash) / last.Earned)) + "판", small);
            if (can) GUI.color = new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin(Time.time * 5));
            if (GUI.Button(new Rect(r.x + 10, r.y + 140, r.width - 20, 28), can ? "<color=#6fcf97>갚기</color>" : "<size=11>갚기 · " + KNum.Fmt(sim.BillAmount - S.cash) + " 모자람</size>", can ? btn : btnOff) && can) sim.PayBill();
            GUI.color = Color.white;
        }

        void Report(Rect r)
        {
            Panel(r, "출동 보고" + (showResult && last != null ? " · " + sim.S.runs : ""), showResult && last != null ? "<color=#6fcf97>+" + KNum.Fmt(last.Earned + last.bonus + last.interest) + "</color>" : null, SweepGame.Amber);
            float x = r.x + 10, y = r.y + 26, w = r.width - 20;
            if (!showResult || last == null)
            {
                GUI.Label(new Rect(x, y, w, 60), "출동 사이엔 늘 여기로 돌아온다.\n최대 연쇄 " + sim.M.bestChain + " · 최대 압축 " + sim.M.bestPack + "\n파산 " + sim.M.bankrupt + " · 특종 " + sim.M.scoops + " / 6", small);
                return;
            }
            var R = last; double tot = System.Math.Max(1, R.Earned);
            float a = (float)(R.earnClaw / tot), b = (float)(R.earnDrone / tot);
            GUI.DrawTexture(new Rect(x, y, w, 8), texBar);
            GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(x, y, w * a, 8), white);
            GUI.color = SweepGame.Cyan; GUI.DrawTexture(new Rect(x + w * a, y, w * b, 8), white);
            GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(x + w * (a + b), y, w * (1 - a - b), 8), white);
            GUI.color = Color.white; y += 11;
            GUI.Label(new Rect(x, y, w, 14), "<color=#f2c14e>빔 " + Mathf.RoundToInt(a * 100) + "</color> · <color=#6fd3e8>드론 " + Mathf.RoundToInt(b * 100) + "</color> · <color=#b69cff>폭발 " + Mathf.RoundToInt((1 - a - b) * 100) + "</color> %", small); y += 18;
            void Row(string k, string v) { GUI.Label(new Rect(x, y, w, 16), k, small); GUI.Label(new Rect(x, y, w, 16), v, cost); y += 16; }
            Row("부순 것", R.broke.ToString());
            Row("최대 연쇄", R.chainBest + (R.chainBest > prevBestChain && R.chainBest >= 10 ? " <color=#ff8a7a>새 기록</color>" : ""));
            Row("최대 압축", R.packBest + (R.packBest > prevBestPack && R.packBest >= 5 ? " <color=#ff8a7a>새 기록</color>" : ""));
            if (R.contractText != null) Row("의뢰", R.contractOk ? "<color=#6fcf97>성공 +" + KNum.Fmt(R.bonus) + "</color>" : "<color=#ee7766>실패 " + R.contractProg + "/" + R.contractTarget + "</color>");
            if (R.interest > 0) Row("적금 이자", "<color=#6fcf97>+" + KNum.Fmt(R.interest) + "</color>");
            if (R.cut > 0) Row("추심", "<color=#ee7766>-" + KNum.Fmt(R.cut) + "</color>");
            if (R.toBill > 0) Row("압류로 갚은 빚", "<color=#ff8a7a>" + KNum.Fmt(R.toBill) + "</color>");
        }

        void NewsMonitor(Rect r)
        {
            int unread = sim.Unread;
            if (Panel(r, "궤도일보", unread > 0 ? "<color=#ff8a7a>● 새 기사 " + unread + "</color>" : "기사 " + sim.M.news.Count, SweepGame.Amber)) { newsOpen = true; newsSel = -1; }
            var pr = new Rect(r.x + 8, r.y + 24, r.width - 16, r.height - 32);
            GUI.DrawTexture(pr, texPaper);
            GUI.DrawTexture(new Rect(pr.x + 6, pr.y + 20, pr.width - 12, 2), texInk);
            paperSmall.fontSize = 10; GUI.Label(new Rect(pr.x + 6, pr.y + 3, pr.width - 12, 16), "궤도일보 · 출동 " + sim.S.runs + "일째", paperSmall); paperSmall.fontSize = 11;
            if (sim.M.news.Count > 0)
            {
                var it = sim.M.news[sim.M.news.Count - 1];
                paperBody.fontSize = 12; GUI.Label(new Rect(pr.x + 6, pr.y + 26, pr.width - 12, 36), "<b>" + Clip(it.head, 26) + "</b>", paperBody);
                paperBody.fontSize = 10; GUI.Label(new Rect(pr.x + 6, pr.y + 60, pr.width - 12, 36), Clip(it.body, 44), paperBody); paperBody.fontSize = 14;
            }
        }

        void Plate(Rect r)
        {
            var S = sim.S; var M = sim.M;
            Panel(r, "회사 명판", M.company + "대", SweepGame.Amber);
            GUI.Label(new Rect(r.x + 10, r.y + 24, r.width - 20, 20), "<color=#ffdf95>주식회사 궤도 청소부</color>", label);
            GUI.Label(new Rect(r.x + 10, r.y + 46, r.width - 20, 16), "쌓인 신용 +" + S.creditPending + " (파산할 때 쓴다)", small);
            if (sim.CanBankrupt)
            {
                if (GUI.Button(new Rect(r.x + 10, r.y + 70, r.width - 20, 44), bankruptArmed ? "<color=#ffb3a8>정말? 한 번 더 누르면 파산</color>" : "<color=#ffb3a8>파산 스위치</color>\n<size=10>돈 · 트리 · 청구서를 잃는다</size>", btn))
                {
                    if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; } else bankruptArmed = true;
                }
            }
            else if (!M.cleanReady) GUI.Label(new Rect(r.x + 10, r.y + 72, r.width - 20, 40), "파산 스위치 — 덮개 잠김\n(연체 중이거나 청구서 3장 뒤)", small);
        }

        void Hatch(Rect r)
        {
            int total = 0;
            for (int b = 0; b < 4; b++) total += CanCount(b);
            if (Panel(r, "정비고 해치", total > 0 ? "<color=#ffdf95>● 살 수 있는 칸 " + total + "</color>" : null, SweepGame.Amber)) { bayOpen = true; }
            if (total > 0) Frame(r, new Color(1f, 0.76f, 0.3f, 0.5f + 0.5f * Mathf.Sin(Time.time * 4)), 2);
            float cw = (r.width - 20) / 4;
            for (int b = 0; b < 4; b++)
            {
                var cr = new Rect(r.x + 10 + b * cw, r.y + 28, cw - 6, 70);
                GUI.DrawTexture(cr, texCard);
                bool open = sim.S.bill >= SweepSim.BranchNeed[b];
                center.normal.textColor = open ? BranchCol[b] : new Color(0.35f, 0.39f, 0.46f);
                GUI.Label(new Rect(cr.x, cr.y + 6, cr.width, 30), SweepSim.BranchNames[b].Replace(" ", "\n").Replace("\n·\n", " · "), center);
                center.normal.textColor = new Color(0.87f, 0.89f, 0.92f);
                GUI.Label(new Rect(cr.x, cr.y + 40, cr.width, 16), open ? Owned(b) + " / " + Total(b) : "잠김", center);
                if (open && CanCount(b) > 0) GUI.Label(new Rect(cr.x, cr.y + 54, cr.width, 14), "<size=10><color=#ffdf95>+" + CanCount(b) + "</color></size>", center);
            }
            title.fontSize = 18; GUI.Label(new Rect(r.x, r.y + 108, r.width, 30), "정비고로 ▾", title);
        }

        void Dial(Rect r)
        {
            var S = sim.S;
            Panel(r, "궤도 다이얼 · 의뢰", null, SweepGame.Amber);
            float bw = (r.width - 28) / 3;
            for (int i = 0; i <= 2; i++)
            {
                var o = SweepSim.Orbits[i]; var br = new Rect(r.x + 10 + i * (bw + 4), r.y + 28, bw, 40);
                if (i > sim.MaxOrbit || sim.M.cleanReady) { GUI.DrawTexture(br, texCard); GUI.Label(br, "<size=10>" + o.name + "\n잠김 · 청구서 " + (i == 1 ? 3 : 6) + "</size>", center); continue; }
                if (GUI.Button(br, "<size=12>" + o.name + "</size>\n<size=11>값 ×" + o.mult + "</size>", i == S.orbit ? btn : btnOff)) sim.SetOrbit(i);
                if (i == S.orbit) Frame(br, SweepGame.Amber, 2);
            }
            var c = sim.CurContract;
            if (c != null && !sim.M.cleanReady)
            {
                GUI.Label(new Rect(r.x + 10, r.y + 78, r.width - 20, 36), "의뢰: <color=#dde3ea>" + c.Value.text + "</color>\n성공하면 판 수입 +" + (25 + 10 * sim.Lv("e_quest")) + "%", small);
                if (!S.rerolled && GUI.Button(new Rect(r.xMax - 80, r.y + 116, 70, 24), "바꾸기", btn)) sim.Reroll();
            }
            else if (!sim.M.cleanReady) GUI.Label(new Rect(r.x + 10, r.y + 80, r.width - 20, 36), "의뢰는 청구서 2 뒤부터", small);
        }

        int CanCount(int b) { int n = 0; for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Nodes[i].branch == SweepSim.BranchIds[b] && sim.State(i) == NodeSt.Can) n++; return n; }
        int Total(int b) { int n = 0; for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Nodes[i].branch == SweepSim.BranchIds[b]) n++; return n; }
        int Owned(int b) { int n = 0; for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Nodes[i].branch == SweepSim.BranchIds[b] && sim.S.lv[i] > 0) n++; return n; }

        // ───────────────────────────────── 정비고 — 네 칸 · 칸마다 카드 (설명 창 없이 카드에 다)

        // ───────────────────────────────── 정비소 — 가운데 한 칸에서 격자로 뻗는 트리 (사장님이 보여 주신 Bills Must Be Paid 트리)
        // 🔴 칸 = 한 번 사기. 레벨이 여럿인 능력은 칸 여러 개가 한 줄로 이어진다 (Ⅰ Ⅱ Ⅲ …)
        // 산 칸 = 밝게 · 다음 칸 = 보인다(살 수 있으면 빛난다) · 그 너머 = 어두운 실루엣 · 더 먼 곳 = 안 보인다
        // 화면은 보이는 칸에 맞춰 저절로 당겨지고 물러난다. 끌어서 옮길 수도 있다.

        class GTile { public int stat, j, lpar = -1; public List<int> xpar = new List<int>(); public Vector2Int cell; public Vector2Int inDir; }
        static List<GTile> gtiles;
        static readonly Vector2Int[] Dirs8 = { new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(-1, -1) };
        Vector2 camC; float camZ = 1f; Vector2 pan; bool dragging; Vector2 dragFrom;

        static Vector2Int Rot(Vector2Int d, int k) { int i = System.Array.IndexOf(Dirs8, d); return Dirs8[((i + k) % 8 + 8) % 8]; }

        static void BuildGraph()
        {
            gtiles = new List<GTile>();
            var N = SweepSim.Nodes;
            var first = new Dictionary<string, int>();
            // 칸 만들기
            for (int i = 0; i < N.Length; i++)
                for (int j = 1; j <= SweepSim.Tiles(i); j++)
                {
                    var t = new GTile { stat = i, j = j };
                    if (j == 1) first[N[i].id] = gtiles.Count;
                    gtiles.Add(t);
                }
            // 부모 — 같은 능력의 앞 칸, 첫 칸이면 앞 능력의 첫 칸 (가지 뿌리는 가운데 「빔 위력」에)
            for (int k = 0; k < gtiles.Count; k++)
            {
                var t = gtiles[k]; var n = N[t.stat];
                if (t.j > 1) { t.lpar = k - 1; continue; }
                var ps = n.par.Length > 0 ? n.par : n.id == "c_pow" ? new string[0] : new[] { "c_pow" };
                for (int q = 0; q < ps.Length; q++) { int pk = first[ps[q]]; if (q == 0) t.lpar = pk; else t.xpar.Add(pk); }
            }
            // 자리 — 가운데에서 너비 우선으로, 가지마다 선호 방향. 빈 이웃 칸을 찾는다
            var used = new HashSet<Vector2Int>();
            var branchDir = new Dictionary<string, Vector2Int> { { "claw", new Vector2Int(-1, -1) }, { "drone", new Vector2Int(1, -1) }, { "bh", new Vector2Int(1, 1) }, { "eco", new Vector2Int(-1, 1) } };
            int root = first["c_pow"];
            gtiles[root].cell = Vector2Int.zero; gtiles[root].inDir = new Vector2Int(0, -1); used.Add(Vector2Int.zero);
            var q2 = new Queue<int>(); q2.Enqueue(root);
            var placed = new HashSet<int> { root };
            while (q2.Count > 0)
            {
                int pk = q2.Dequeue(); var p = gtiles[pk];
                for (int k = 0; k < gtiles.Count; k++)
                {
                    var t = gtiles[k]; if (t.lpar != pk || placed.Contains(k)) continue;
                    var n = N[t.stat];
                    Vector2Int pref;
                    if (t.j > 1) pref = p.inDir;                                          // 같은 능력 — 곧게 이어 간다
                    else if (pk == root) pref = n.branch == "claw" ? (n.id == "c_rad" ? new Vector2Int(-1, -1) : new Vector2Int(-1, 0)) : branchDir[n.branch];
                    else pref = Rot(p.inDir, (k % 2 == 0) ? 2 : -2);                      // 새 능력 — 옆으로 꺾는다
                    int[] order = { 0, 1, -1, 2, -2, 3, -3, 4 };
                    Vector2Int cell = p.cell, dir = pref; bool ok = false;
                    for (int dist = 1; dist <= 3 && !ok; dist++)
                        foreach (int o in order)
                        {
                            var d = Rot(pref, o); var c = p.cell + d * dist;
                            if (used.Contains(c)) continue;
                            cell = c; dir = d; ok = true; break;
                        }
                    t.cell = cell; t.inDir = dir; used.Add(cell); placed.Add(k); q2.Enqueue(k);
                }
            }
        }

        int GTileState(int k)   // 0 안 보임 · 1 실루엣 · 2 다음 칸 · 3 산 것
        {
            var t = gtiles[k];
            if (sim.S.lv[t.stat] >= SweepSim.TileLv(t.stat, t.j)) return 3;
            bool parOwned = t.lpar < 0 || GTileState(t.lpar) == 3;
            foreach (var x in t.xpar) if (GTileState(x) != 3) parOwned = false;
            if (parOwned) return 2;
            if (t.lpar >= 0 && GTileState(t.lpar) == 2) return 1;
            return 0;
        }

        static readonly string[] Roman = { "", "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ" };

        void Bay()
        {
            if (gtiles == null) BuildGraph();
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            // 머리 — 돈 · 청구서 · 궤도일보
            big.fontSize = 24 + Mathf.RoundToInt(game.creditPulse * 6);
            GUI.Label(new Rect(ox + 16, 12, 40, 20), "돈", dim);
            GUI.Label(new Rect(ox + 38, 6, 240, 32), "<color=#ffdf95>" + KNum.Fmt(shown) + "</color>", big);
            big.fontSize = 20;
            CreditScreen = new Vector2((ox + 60) * scale, Screen.height - 22 * scale);
            if (S.bill < SweepSim.Bills.Length && !sim.M.cleanReady)
            {
                string bl = "청구서 · " + SweepSim.Bills[S.bill].t + " <color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color>" + (S.overdue ? "  <color=#ff8a7a>연체 중</color>" : " · " + S.billDue + "판 남음") + (S.cash >= sim.BillAmount ? "  <color=#6fcf97>▶ 눌러서 갚기</color>" : "");
                if (GUI.Button(new Rect(ox + 240, 8, 500, 30), bl, S.cash >= sim.BillAmount ? btn : btnOff) && S.cash >= sim.BillAmount) sim.PayBill();
            }
            int unreadN = sim.Unread;
            if (GUI.Button(new Rect(ox + 780, 8, 166, 30), "궤도일보" + (unreadN > 0 ? "  <color=#ff8a7a>● " + unreadN + "</color>" : ""), btn)) { newsOpen = true; newsSel = -1; }

            var area = new Rect(0, 46, vw, 456);
            int nT = gtiles.Count;
            int[] st = new int[nT];
            Vector2 lo = new Vector2(1e9f, 1e9f), hi = new Vector2(-1e9f, -1e9f);
            for (int k = 0; k < nT; k++)
            {
                st[k] = GTileState(k);
                if (st[k] == 0) continue;
                lo = Vector2.Min(lo, gtiles[k].cell); hi = Vector2.Max(hi, gtiles[k].cell);
            }
            // 화면 맞추기 — 보이는 칸이 다 들어오게 (끌면 옮겨진다)
            float cellPx = 64f;
            var size = (hi - lo + Vector2.one * 2f) * cellPx;
            float wantZ = Mathf.Clamp(Mathf.Min(area.width / size.x, area.height / size.y), 0.45f, 1.15f);
            var wantC = (lo + hi) / 2f;
            camZ = Mathf.Lerp(camZ, wantZ, 1 - Mathf.Exp(-Time.deltaTime * 4));
            camC = Vector2.Lerp(camC, wantC, 1 - Mathf.Exp(-Time.deltaTime * 4));
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 1 && area.Contains(ev.mousePosition)) { dragging = true; dragFrom = ev.mousePosition; }
            if (ev.type == EventType.MouseDrag && dragging) { pan += ev.mousePosition - dragFrom; dragFrom = ev.mousePosition; }
            if (ev.type == EventType.MouseUp && ev.button == 1) dragging = false;
            Vector2 ToScr(Vector2Int c) => area.center + pan + ((Vector2)c - camC) * cellPx * camZ;
            float tile = 44f * camZ;

            // 선 — 둘 다 산 것이면 금색
            for (int k = 0; k < nT; k++)
            {
                if (st[k] == 0) continue;
                var t = gtiles[k];
                var pars = new List<int>(t.xpar); if (t.lpar >= 0) pars.Add(t.lpar);
                foreach (var pk in pars)
                {
                    if (st[pk] == 0) continue;
                    bool gold = st[k] == 3 && st[pk] == 3;
                    var a = ToScr(gtiles[pk].cell); var b = ToScr(t.cell);
                    if (gold) Line(a, b, new Color(1f, 0.72f, 0.2f, 0.3f), 8 * camZ);
                    Line(a, b, gold ? new Color(1f, 0.74f, 0.2f) : new Color(0.32f, 0.3f, 0.28f, st[k] == 1 ? 0.5f : 0.9f), (gold ? 3.2f : 2f) * camZ);
                }
            }
            // 칸
            int hover = -1;
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            var m0 = GUI.matrix;
            for (int k = 0; k < nT; k++)
            {
                if (st[k] == 0) continue;
                var t = gtiles[k]; var n = SweepSim.Nodes[t.stat];
                int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
                bool next = st[k] == 2, owned = st[k] == 3;
                var ns = sim.State(t.stat);
                bool can = next && ns == NodeSt.Can;
                bool diamond = n.max == 1;
                var pc = ToScr(t.cell);
                float grow = nodePulse[t.stat] * 8 * camZ;
                float sz = (diamond ? tile * 0.92f : tile) + grow;
                var r = new Rect(pc.x - sz / 2, pc.y - sz / 2, sz, sz);
                if (can) { GUI.color = new Color(1f, 0.78f, 0.3f, 0.28f + 0.18f * Mathf.Sin(Time.time * 5 + k)); GUI.DrawTexture(new Rect(r.x - 9 * camZ, r.y - 9 * camZ, r.width + 18 * camZ, r.height + 18 * camZ), texDisc); }
                if (diamond) GUI.matrix = m0 * Matrix4x4.TRS(new Vector3(pc.x, pc.y, 0), Quaternion.Euler(0, 0, 45), Vector3.one) * Matrix4x4.TRS(new Vector3(-pc.x, -pc.y, 0), Quaternion.identity, Vector3.one);
                Color bg = owned ? BranchCol[b] * new Color(0.38f, 0.3f, 0.26f, 1) : next ? new Color(0.09f, 0.09f, 0.1f) : new Color(0.06f, 0.06f, 0.07f);
                GUI.color = bg; GUI.DrawTexture(r, white);
                Color edge = can ? new Color(1f, 0.8f, 0.35f) : owned ? BranchCol[b] * new Color(0.9f, 0.8f, 0.7f, 1) : new Color(0.22f, 0.21f, 0.2f);
                Frame(r, edge, can ? 2.5f : 1.5f);
                GUI.matrix = m0;
                string glyph = st[k] == 1 ? "" : ns == NodeSt.Locked && !owned ? "잠" : n.name.Substring(0, 1);
                center.normal.textColor = owned ? Color.white : next ? new Color(0.72f, 0.72f, 0.74f) : new Color(0.3f, 0.3f, 0.32f);
                center.fontSize = Mathf.RoundToInt(17 * camZ); GUI.Label(r, glyph, center); center.fontSize = 13;
                center.normal.textColor = new Color(0.87f, 0.89f, 0.92f);
                if (r.Contains(ev.mousePosition)) hover = k;
                if (next && GUI.Button(r, GUIContent.none, GUIStyle.none) && ns == NodeSt.Can)
                {
                    int times = shift ? 5 : 1;
                    while (times-- > 0 && sim.State(t.stat) == NodeSt.Can) sim.BuyTile(t.stat);
                    nodePulse[t.stat] = 1; OrbitSfx.Play("buy", 0.7f, 0.01f, 0.15f);
                }
            }
            GUI.color = Color.white;
            if (hover >= 0) Tip(hover, ToScr(gtiles[hover].cell), st[hover], tile);
            else GUI.Label(new Rect(0, area.yMax - 18, vw, 16), "<size=11>칸에 마우스를 올리면 무엇인지 보인다 · 빛나는 칸을 누르면 산다 · 오른쪽 단추로 끌면 옮겨 본다</size>", center);
        }

        void Tip(int k, Vector2 at, int vis, float tile)
        {
            var t = gtiles[k]; var n = SweepSim.Nodes[t.stat]; var ns = sim.State(t.stat); int lv = sim.S.lv[t.stat];
            int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
            var r = new Rect(at.x + tile / 2 + 14, at.y - 70, 300, 150);
            if (r.xMax > vw - 8) r.x = at.x - tile / 2 - 14 - r.width;
            r.y = Mathf.Clamp(r.y, 50, 500 - r.height);
            GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.97f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.6f, 0.5f, 0.35f), 2);
            GUI.color = new Color(0.12f, 0.12f, 0.14f); GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, 34), white); GUI.color = Color.white;
            title.fontSize = 18;
            if (vis == 1)
            {
                GUI.Label(new Rect(r.x, r.y + 4, r.width, 28), "<color=#b89a6a>?</color>", title);
                GUI.Label(new Rect(r.x, r.y + 58, r.width, 20), "앞 칸을 사면 무엇인지 보인다", center);
                return;
            }
            string nm = n.name + (SweepSim.Tiles(t.stat) > 1 ? " " + Roman[t.j] : "");
            GUI.Label(new Rect(r.x, r.y + 4, r.width, 28), "<color=#d9b98a>" + nm + "</color>", title);
            GUI.Label(new Rect(r.x + 10, r.y + 42, r.width - 20, 22), n.desc, center);
            GUI.color = new Color(0.3f, 0.28f, 0.24f); GUI.DrawTexture(new Rect(r.x + 24, r.y + 70, r.width - 48, 1), white); GUI.DrawTexture(new Rect(r.x + 24, r.y + 98, r.width - 48, 1), white); GUI.color = Color.white;
            int from = t.j == 1 ? 0 : SweepSim.TileLv(t.stat, t.j - 1), to = SweepSim.TileLv(t.stat, t.j);
            GUI.Label(new Rect(r.x + 10, r.y + 74, r.width - 20, 20), Val(n.id, from) + "  <color=#d9b98a>▸</color>  <color=#ffdf95>" + Val(n.id, to) + "</color>", center);
            string foot;
            if (vis == 3) foot = "<color=#6fcf97>샀다</color>";
            else if (ns == NodeSt.Locked) foot = "<color=#ff9b8f>청구서 " + SweepSim.BranchNeed[b] + "을 갚으면 열린다</color>";
            else if (ns == NodeSt.Hidden) foot = n.seg > sim.Seg ? "<color=#ff9b8f>청구서 " + (n.seg - 1) + "을 갚으면 열린다</color>" : "<color=#ff9b8f>앞 칸을 먼저 사야 한다</color>";
            else foot = (ns == NodeSt.Can ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + "</color>";
            center.fontSize = 20; GUI.Label(new Rect(r.x, r.y + 106, r.width, 32), foot, center); center.fontSize = 13;
        }

        void Line(Vector2 a, Vector2 b, Color col, float w)
        {
            var m = GUI.matrix;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = m * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, ang), Vector3.one);
            GUI.color = col; GUI.DrawTexture(new Rect(0, -w / 2, (b - a).magnitude, w), white);
            GUI.matrix = m; GUI.color = Color.white;
        }

        string Val(string id, int l)
        {
            switch (id)
            {
                case "c_pow": return "한 방 " + (1 + l);
                case "c_rad": return l > 0 ? "반지름 " + (22 + 10 * l) : "한 점";
                case "c_spd": return Mathf.Max(0.3f, 0.6f - 0.045f * l).ToString("0.00") + "초";
                case "c_fuel": return (30 + 3 * l) + "초";
                case "c_crit": return (5 * l) + "%";
                case "c_double": return (10 * l) + "%";
                case "c_magnet": return l > 0 ? "반경 +" + (40 + 20 * l) : "없음";
                case "c_over": return l > 0 ? "마지막 5초 ×2" : "없음";
                case "d_n": return (2 + l) + "대";
                case "d_spd": return Mathf.Max(0.4f, 1 - 0.1f * l).ToString("0.0") + "초마다";
                case "d_reach": return "거리 " + (80 + 15 * l);
                case "d_mag": return "+" + (25 * l) + "%";
                case "d_sig": return (3 + 2 * l) + "초";
                case "d_grade": return "한 방 " + (1 + l);
                case "d_fix": return "+" + (2 * l) + "초";
                case "d_pair": return l > 0 ? "둘씩" : "하나씩";
                case "d_fact": return "+" + l + "대";
                case "b_n": return "판마다 " + (2 + l) + "발";
                case "c_find": return "판마다 " + l + "번";
                case "s_speed": return "속도 " + (240 + 60 * l);
                case "b_pr": return "반경 " + (150 + 20 * l);
                case "b_cap": return (18 + 8 * l) + "개";
                case "b_pf": return "×" + (1 + 0.25f * l).ToString("0.00");
                case "b_br": return "+" + (15 * l) + "%";
                case "b_chain": return (25 + 7 * l) + "%";
                case "b_pack": return "개당 +" + (2 + 1.2f * l).ToString("0.0") + "%";
                case "o_wide": return "궤도 폭 +" + (10 * l) + "%";
                case "e_val": return "×" + Mathf.Pow(1.25f, l).ToString("0.00");
                case "e_vault": return "+" + (50 * l) + "%";
                case "e_att": return "+" + (40 * l) + "%";
                case "e_quest": return "+" + (25 + 10 * l) + "%";
                case "e_talk": return "기한 +" + l + "판";
                case "e_tip": return (25 + 10 * l) + "%";
                case "e_save": return "이자 " + (2 * l) + "%";
                case "e_guard": return "추심 " + (l > 0 ? 20 : 30) + "%";
                case "e_used": return "-" + (5 * l) + "%";
            }
            return l.ToString();
        }

        // ───────────────────────────────── 파산 뒤 — 경력 (신용으로 산다)

        void Career()
        {
            var M = sim.M;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            float x = ox + 180, w = 600;
            title.fontSize = 30;
            GUI.Label(new Rect(x, 50, w, 40), "<color=#ff8a7a>파산</color>", title);
            GUI.Label(new Rect(x, 94, w, 22), "<color=#5a6475>주식회사 궤도 청소부 (" + (M.company - 1) + "대)</color>  →  <color=#ffdf95>주식회사 궤도 청소부 (" + M.company + "대)</color>", center);
            GUI.Label(new Rect(x, 124, w, 20), "잃은 것: 돈 · 트리 · 청구서 · 면허     남은 것: 신용 · 경력 · 읽은 기사 · 기록", center);
            GUI.Label(new Rect(x, 158, w, 26), "신용 <color=#ffdf95>" + M.credit + "</color> — 경력은 파산할 때만 산다", label);
            for (int i = 0; i < SweepSim.CareerCount; i++)
            {
                var c = SweepSim.Careers[i]; int lv = M.career[i], cc = sim.CareerCost(i);
                bool can = cc > 0 && M.credit >= cc;
                var rr = new Rect(x + (i % 2) * 304, 192 + (i / 2) * 64, 296, 56);
                if (GUI.Button(rr, GUIContent.none, can ? btn : btnOff) && can && sim.BuyCareer(i)) OrbitSfx.Play("buy", 0.8f);
                GUI.Label(new Rect(rr.x + 10, rr.y + 6, rr.width - 20, 20), c.name + "  " + lv + " / " + c.cost.Length, can ? label : dim);
                GUI.Label(new Rect(rr.x + 10, rr.y + 6, rr.width - 20, 20), cc < 0 ? "<color=#6fcf97>끝</color>" : "신용 " + cc, cost);
                GUI.Label(new Rect(rr.x + 10, rr.y + 30, rr.width - 20, 18), c.desc, small);
            }
            if (GUI.Button(new Rect(x + 150, 400, 300, 56), "(" + M.company + "대) 출발 ▸", bigBtn)) { sim.CloseCareer(); showResult = false; flow = 3; game.Save(); }
        }

        // ───────────────────────────────── 궤도일보 (§11)

        void Ticker()
        {
            string line;
            bool breaking = breakingT > 0 && breakingHead != null;
            if (breaking) line = breakingHead;
            else
            {
                var heads = new List<string>();
                for (int i = sim.M.news.Count - 1; i >= 0 && heads.Count < 3; i--) heads.Add(sim.M.news[i].head);
                int k = tickI % (heads.Count + 2);
                line = k < heads.Count ? heads[k] : SweepSim.World[(tickI * 7) % SweepSim.World.Length];
            }
            float y = RefH - 22;
            float x = sim.R.over ? ox + 14 : 14;
            if (breaking) { GUI.DrawTexture(new Rect(x, y + 2, 34, 15), texRed); GUI.Label(new Rect(x, y, 34, 18), "<color=#ffffff>속보</color>", center); x += 40; }
            GUI.Label(new Rect(x, y, 700, 18), "궤도일보 · " + line, dim);
        }

        void News()
        {
            var M = sim.M;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var box = new Rect(ox + 40, 30, 880, 520);
            GUI.DrawTexture(box, texCard);
            // 목록
            var lr = new Rect(box.x + 10, box.y + 34, 300, box.height - 70);
            GUI.Label(new Rect(box.x + 12, box.y + 8, 300, 20), "궤도일보 — 기사 목록", label);
            int total = M.news.Count;
            newsScroll = GUI.BeginScrollView(lr, newsScroll, new Rect(0, 0, 280, total * 40));
            for (int j = 0; j < total; j++)
            {
                int i = total - 1 - j; var it = M.news[i];
                string kind = it.kind == "scoop" ? "<color=#ff6b5a>특종</color>" : it.kind == "world" ? "<color=#8a93a3>세상</color>" : it.kind == "extra" ? "<color=#ffdf95>호외</color>" : "<color=#f2c14e>우리</color>";
                if (GUI.Button(new Rect(0, j * 40, 280, 36), (it.read ? "   " : "<color=#ff6b5a>●</color> ") + kind + "  <size=12>" + Clip(it.head, 20) + "</size>", i == newsSel ? btn : btnOff)) { newsSel = i; it.read = true; }
            }
            GUI.EndScrollView();
            int scoops = 0; foreach (var it in M.news) if (it.kind == "scoop") scoops++;
            GUI.Label(new Rect(box.x + 12, box.yMax - 30, 300, 18), "모은 기사 " + total + " · 특종 " + scoops + " / 6 · 파산해도 남는다", small);
            // 전문
            if (newsSel < 0 && total > 0) { newsSel = total - 1; M.news[newsSel].read = true; }
            var pr = new Rect(box.x + 322, box.y + 10, box.width - 332, box.height - 20);
            GUI.DrawTexture(pr, texPaper);
            if (newsSel >= 0 && newsSel < total)
            {
                var it = M.news[newsSel];
                float x = pr.x + 22, w = pr.width - 44, y = pr.y + 16;
                paperHead.fontSize = 26; GUI.Label(new Rect(x, y, 200, 30), "궤도일보", paperHead);
                paperSmall.alignment = TextAnchor.UpperRight; GUI.Label(new Rect(x, y + 8, w, 18), "출동 " + it.run + "일째 · (" + it.company + "대) 시절", paperSmall); paperSmall.alignment = TextAnchor.UpperLeft;
                y += 36; GUI.DrawTexture(new Rect(x, y, w, 3), texInk); y += 12;
                string kn = it.kind == "scoop" ? "특종" : it.kind == "world" ? "세상 소식" : it.kind == "extra" ? "호외" : "우리 소식";
                GUI.color = it.kind == "scoop" ? new Color(0.75f, 0.22f, 0.17f) : it.kind == "world" ? new Color(0.42f, 0.39f, 0.34f) : new Color(0.72f, 0.53f, 0.04f);
                GUI.DrawTexture(new Rect(x, y, 70, 18), white); GUI.color = Color.white;
                GUI.Label(new Rect(x, y, 70, 18), "<color=#ffffff>" + kn + "</color>", center); y += 26;
                paperHead.fontSize = 22; float hh = paperHead.CalcHeight(new GUIContent(it.head), w);
                GUI.Label(new Rect(x, y, w, hh), it.head, paperHead); y += hh + 14;
                float bh = paperBody.CalcHeight(new GUIContent(it.body), w);
                GUI.Label(new Rect(x, y, w, bh), it.body, paperBody); y += bh + 16;
                GUI.Label(new Rect(x, y, w, 16), "— 궤도일보 궤도부", paperSmall);
            }
            if (GUI.Button(new Rect(box.xMax - 110, box.y + 6, 100, 24), "닫기 (Esc)", btn)) newsOpen = false;
        }

        // ───────────────────────────────── 엔딩 — 마지막 호외 → 결과판 (§9-5)

        void Ending()
        {
            var M = sim.M;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            if (endStage == 0)
            {
                var pr = new Rect(ox + 130, 40, 700, 500);
                GUI.DrawTexture(pr, texPaper);
                float x = pr.x + 30, w = pr.width - 60, y = pr.y + 20;
                paperHead.fontSize = 40; GUI.Label(new Rect(x, y, w, 48), "궤도일보 — 호외", paperHead);
                y += 56; GUI.DrawTexture(new Rect(x, y, w, 4), texInk); y += 20;
                paperHead.fontSize = 46; GUI.Label(new Rect(x, y, w, 56), "궤도 청소율 100%", paperHead); y += 70;
                GUI.Label(new Rect(x, y, w, 90), "지구 둘레에 쓰레기가 하나도 없다. 주식회사 궤도 청소부 (" + M.company + "대)가 청소선 할부를 끝까지 갚고, 마지막 출동에서 궤도를 전부 치웠다. 케슬러 발사는 이번 분기 발사 계획이 없다고 밝혔다.", paperBody); y += 100;
                if (M.scoops >= 6) { paperHead.fontSize = 24; GUI.Label(new Rect(x, y, w, 60), "케슬러 그룹, 궤도 사업 전면 철수", paperHead); y += 50; }
                else GUI.Label(new Rect(x, y, w, 20), "(특종 " + M.scoops + " / 6 — 블랙박스를 더 모으면 한 줄이 더 붙는다)", paperSmall);
                if (GUI.Button(new Rect(pr.center.x - 100, pr.yMax - 70, 200, 44), "결과 보기", bigBtn)) endStage = 1;
                return;
            }
            float cx = ox + 180, cw = 600;
            title.fontSize = 44; GUI.Label(new Rect(cx, 40, cw, 60), "빚 청산", title);
            GUI.Label(new Rect(cx, 102, cw, 22), "주식회사 궤도 청소부 (" + M.company + "대) — 청소선은 이제 조종사의 것이다", center);
            double minutes = 0; foreach (var h in M.history) minutes += h.minutes;
            string[] rows =
            {
                "걸린 시간   " + Mathf.RoundToInt((float)minutes) + "분",
                "출동 · 파산   " + M.totalRuns + " · " + M.bankrupt,
                "부순 잔해   " + KNum.Fmt(M.broken),
                "최대 연쇄 · 최대 압축   " + M.bestChain + " · " + M.bestPack,
                "기사 · 특종   " + M.news.Count + " · " + M.scoops + " / 6",
            };
            for (int i = 0; i < rows.Length; i++) GUI.Label(new Rect(cx, 140 + i * 24, cw, 22), rows[i], center);
            GUI.Label(new Rect(cx, 272, cw, 18), "지난 회사들", head);
            for (int i = 0; i < M.history.Count && i < 8; i++)
            {
                var h = M.history[i];
                GUI.Label(new Rect(cx, 292 + i * 20, cw, 20), "(" + h.company + "대)  " + (h.won ? "<color=#ffdf95>빚 청산</color>" : SweepSim.Bills[Mathf.Min(h.bill, 7)].t + "에서 파산") + " · 출동 " + h.runs + " · " + Mathf.RoundToInt((float)h.minutes) + "분", label);
            }
            GUI.Label(new Rect(cx, 470, cw, 20), "<color=#f2c14e>오늘도 궤도는 깨끗합니다.</color>", center);
            if (GUI.Button(new Rect(cx + 90, 500, 200, 44), "새 회사로", bigBtn)) { game.NewGame(true); showResult = false; }
            if (GUI.Button(new Rect(cx + 310, 506, 200, 32), "기록까지 모두 지우기", btn)) { game.WipeAll(); showResult = false; }
        }
    }
}
