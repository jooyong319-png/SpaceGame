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
        public bool Blocking => sim != null && (sim.R.over || sim.M.careerOpen || sim.M.won || newsOpen);

        // 결산
        bool showResult, bankruptArmed, newsOpen;
        int prevBestChain, prevBestPack, runNewsFrom;
        SweepRun last;
        double shown;
        int selected = 0, newsSel = -1, endStage;
        Vector2 newsScroll;
        // 알림
        string banner; int bannerKind; float bannerT;
        string paidText; float paidT; int paidBill;
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
            showResult = true; bankruptArmed = false; last = sim.R;
            sim.M.flags.Remove("hint_seen_now");
            if (!sim.M.flags.Contains("hint_claw")) sim.M.flags.Add("hint_claw");
            if (sim.DronesOn && !sim.M.flags.Contains("hint_drone")) sim.M.flags.Add("hint_drone");
            if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb")) sim.M.flags.Add("hint_bomb");
        }

        void Go()
        {
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
            bannerT -= dt; paidT -= dt; breakingT -= dt; tickT -= dt;
            for (int i = 0; i < 4; i++) branchFlash[i] = Mathf.Max(0, branchFlash[i] - dt);
            for (int i = 0; i < nodePulse.Length; i++) nodePulse[i] = Mathf.Max(0, nodePulse[i] - dt * 3);
            if (tickT <= 0) { tickT = 8f; tickI++; }
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && sim.R.over && !sim.M.careerOpen && !sim.M.won && !newsOpen && paidT < 2.4f) Go();
            if (kb != null && kb.escapeKey.wasPressedThisFrame) newsOpen = false;
        }

        void OnGUI()
        {
            if (sim == null) return;
            Styles();
            scale = Screen.height / RefH; vw = Screen.width / scale; ox = Mathf.Max(0, (vw - 960) / 2);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            Effects();
            if (!sim.R.over) { Pops(); RunHud(); }
            if (sim.M.won) Ending();
            else if (sim.M.careerOpen) Career();
            else if (sim.R.over) Shop();
            if (newsOpen) News();
            if (!sim.M.won) Ticker();
        }

        // ───────────────────────────────── 연출 (도파민 사다리 §5)

        void Effects()
        {
            if (game.edgeGlow > 0.01f && !reduceMotion) { GUI.color = new Color(1f, 0.76f, 0.3f, game.edgeGlow * 0.35f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette); }
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
            if (bannerT > 0 && banner != null)
            {
                var c = bannerKind == -1 ? SweepGame.Amber2 : new Color(1f, 0.55f, 0.48f, Mathf.Sin(Time.time * 12) > -0.3f ? 1 : 0.55f);
                pop.fontSize = 17; pop.normal.textColor = c;
                GUI.Label(new Rect(vw / 2 - 300, 196, 600, 26), banner, pop);
                if (bannerKind == 0) GUI.Label(new Rect(8, RefH / 2 - 14, 30, 28), "◀", pop);
                if (bannerKind == 1) GUI.Label(new Rect(vw - 36, 110, 30, 28), "▶", pop);
            }
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
            if (!sim.M.flags.Contains("hint_claw") && R.t < 12) hint = "쓰레기를 눌러서 하나씩 줍는다 — 위성은 세 번, 로켓은 다섯 번";
            else if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb") && R.t < 14) hint = "지구에서 폭탄이 올라온다 — 길게 누르고 있으면 빨아들이고, 떼면 모인 만큼 터진다";
            else if (sim.DronesOn && !sim.M.flags.Contains("hint_drone") && R.t < 8) hint = "드론은 알아서 줍는다 — 한 방에 부서지는 것만";
            if (hint != null) GUI.Label(new Rect(vw / 2 - 360, RefH - 70, 720, 20), hint, center);
            if (game.timeScale > 1) GUI.Label(new Rect(vw - 120, RefH - 46, 106, 18), "시험 속도 ×3", cost);
        }

        // ───────────────────────────────── 정비소 — 집

        void Shop()
        {
            var S = sim.S; var M = sim.M;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            // 위 띠 (돈)
            big.fontSize = 22 + Mathf.RoundToInt(game.creditPulse * 6);
            GUI.Label(new Rect(ox + 14, 10, 60, 20), "돈", dim);
            GUI.Label(new Rect(ox + 36, 6, 300, 30), KNum.Fmt(shown), big);
            big.fontSize = 20;
            CreditScreen = new Vector2((ox + 60) * scale, Screen.height - 22 * scale);
            GUI.Label(new Rect(ox + 600, 12, 346, 20), "주식회사 궤도 청소부 (" + M.company + "대) · 출동 " + S.runs, cost);

            BillCard(new Rect(ox + 250, 40, 460, 104));
            ResultPane(new Rect(ox + 14, 40, 226, 460));
            Tree(new Rect(ox + 250, 150, 460, 356));
            Detail(new Rect(ox + 720, 40, 226, 300));

            // 아래 줄
            float y = 512;
            float bx = ox + 250;
            if (!M.cleanReady && sim.MaxOrbit > 0)
            {
                for (int i = 0; i <= 2; i++)
                {
                    var o = SweepSim.Orbits[i];
                    if (i > sim.MaxOrbit) { GUI.Label(new Rect(bx, y + 6, 130, 20), "잠김 · " + o.name, dim); bx += 110; continue; }
                    if (GUI.Button(new Rect(bx, y, 104, 26), o.name + " ×" + o.mult, i == S.orbit ? btn : btnOff)) sim.SetOrbit(i);
                    if (i == S.orbit) { GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(bx, y + 24, 104, 2), white); GUI.color = Color.white; }
                    bx += 110;
                }
            }
            var c = sim.CurContract;
            if (c != null && !M.cleanReady)
            {
                GUI.Label(new Rect(ox + 250, y + 32, 340, 18), "의뢰: <color=#dde3ea>" + c.Value.text + "</color> — 성공하면 판 수입 +25%", dim);
                if (!S.rerolled && GUI.Button(new Rect(ox + 590, y + 30, 70, 22), "바꾸기", btn)) sim.Reroll();
            }
            // 파산 · 신용
            GUI.Label(new Rect(ox + 720, 350, 226, 18), "쌓인 신용 +" + S.creditPending + " (파산할 때 쓴다)", dim);
            if (sim.CanBankrupt)
            {
                if (GUI.Button(new Rect(ox + 720, 372, 226, 30), bankruptArmed ? "<color=#ffb3a8>정말? 한 번 더 누르면 파산</color>" : "<color=#ffb3a8>파산…</color>  <size=11>돈 · 트리 · 청구서를 잃는다</size>", btn))
                {
                    if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; } else bankruptArmed = true;
                }
            }
            else if (!M.cleanReady) GUI.Label(new Rect(ox + 720, 374, 226, 32), "파산은 연체 중이거나 청구서 3장을 갚은 뒤에", small);
            int unread = sim.Unread;
            if (GUI.Button(new Rect(ox + 720, 412, 226, 30), "궤도일보" + (unread > 0 ? "  <color=#ff8a7a>● " + unread + "</color>" : "  · 기사 " + M.news.Count), btn)) { newsOpen = true; newsSel = -1; }
            reduceMotion = GUI.Toggle(new Rect(ox + 720, 448, 226, 20), reduceMotion, " 움직임 줄이기", label);
            if (GUI.Button(new Rect(ox + 720, 470, 226, 62), M.cleanReady ? "청산 출동 ▸" : "출동 ▸", bigBtn) && paidT < 2.4f) Go();
            GUI.Label(new Rect(ox + 720, 534, 226, 16), "Space 로도", small);
        }

        void BillCard(Rect r)
        {
            var S = sim.S;
            if (sim.M.cleanReady)
            {
                GUI.DrawTexture(r, texCard); GUI.DrawTexture(new Rect(r.x, r.y, 5, r.height), texAmber);
                GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 30, 20), "납부 완료 · 청소선 할부 완납", head);
                title.fontSize = 30; GUI.Label(new Rect(r.x, r.y + 30, r.width, 44), "빚 청산", title);
                GUI.Label(new Rect(r.x + 16, r.y + 78, r.width - 30, 18), "남은 건 청산 출동 한 번 — 궤도를 전부 치운다", center);
                return;
            }
            if (S.bill >= SweepSim.Bills.Length) return;
            var b = SweepSim.Bills[S.bill];
            bool can = S.cash >= sim.BillAmount, last1 = !S.overdue && S.billDue <= 1;
            Color edge = S.overdue ? SweepGame.Red : last1 ? SweepGame.Orange : can ? SweepGame.Green : SweepGame.Amber;
            GUI.DrawTexture(r, texCard);
            GUI.color = edge; GUI.DrawTexture(new Rect(r.x, r.y, 5, r.height), white); GUI.color = Color.white;
            if (paidT > 0 && paidBill > 0)
            {
                var pb = SweepSim.Bills[paidBill - 1];
                GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 30, 20), "<color=#ffdf95>납부 완료</color> · " + pb.t, head);
                title.fontSize = 22; GUI.Label(new Rect(r.x, r.y + 34, r.width, 36), pb.perk, title);
                GUI.Label(new Rect(r.x + 16, r.y + 76, r.width - 30, 18), "신용 +" + pb.credit + " 쌓임 · 다음 청구서가 올라온다", center);
                return;
            }
            string pill = S.overdue ? "<color=#ff8a7a>연체 — 추심 " + Mathf.RoundToInt((float)sim.Cut * 100) + "% · 판마다 연체료 +" + (sim.M.career[5] > 0 ? 5 : 10) + "%</color>"
                : last1 ? "<color=#ff9a4d>이번 판이 마지막 — 청구서 " + (S.bill + 1) + " / 8</color>"
                : "청구서 " + (S.bill + 1) + " / 8 · 기한 " + S.billDue + "판 · 케슬러 금융";
            GUI.Label(new Rect(r.x + 16, r.y + 8, r.width - 120, 18), pill, head);
            GUI.Label(new Rect(r.x + 16, r.y + 28, 220, 26), b.t, big);
            title.fontSize = 28; title.alignment = TextAnchor.UpperLeft;
            GUI.Label(new Rect(r.x + 200, r.y + 24, 170, 34), KNum.Fmt(sim.BillAmount), title);
            title.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(r.x + 16, r.y + 60, r.width - 120, 18), "갚으면 → <color=#ffdf95>" + b.perk + "</color>", dim);
            float k = Mathf.Clamp01((float)(S.cash / Mathf.Max(1, (float)sim.BillAmount)));
            GUI.DrawTexture(new Rect(r.x + 16, r.y + 82, r.width - 130, 6), texBar);
            GUI.color = can ? SweepGame.Green : SweepGame.Amber; GUI.DrawTexture(new Rect(r.x + 16, r.y + 82, (r.width - 130) * k, 6), white); GUI.color = Color.white;
            if (S.bill + 1 < SweepSim.Bills.Length) { var nb = SweepSim.Bills[S.bill + 1]; GUI.Label(new Rect(r.x + 16, r.y + 89, r.width - 20, 16), "다음: " + nb.t + " " + KNum.Fmt(nb.m) + " → " + nb.perk, small); }
            var pay = new Rect(r.xMax - 104, r.y + 22, 92, 52);
            if (can) { GUI.color = new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin(Time.time * 5)); }
            if (GUI.Button(pay, can ? "<color=#6fcf97>갚기</color>" : "갚기", can ? btn : btnOff) && can) sim.PayBill();
            GUI.color = Color.white;
        }

        void ResultPane(Rect r)
        {
            GUI.DrawTexture(r, texCard);
            float x = r.x + 10, y = r.y + 8, w = r.width - 20;
            var M = sim.M;
            if (!showResult || last == null)
            {
                GUI.Label(new Rect(x, y, w, 20), "정비소", label);
                GUI.Label(new Rect(x, y + 22, w, 60), "출동 사이엔 늘 여기로 온다. 청구서를 갚으면 새 도구가 열린다.", small);
                y += 90;
                GUI.Label(new Rect(x, y, w, 18), "최대 연쇄 " + M.bestChain + " · 최대 압축 " + M.bestPack, dim); y += 20;
                GUI.Label(new Rect(x, y, w, 18), "파산 " + M.bankrupt + " · 특종 " + M.scoops + " / 6", dim);
                return;
            }
            var R = last;
            GUI.Label(new Rect(x, y, w, 18), "출동 " + sim.S.runs + " — 귀환", head); y += 18;
            GUI.Label(new Rect(x, y, w, 30), "+" + KNum.Fmt(R.Earned + R.bonus), big); y += 32;
            double tot = System.Math.Max(1, R.Earned);
            float a = (float)(R.earnClaw / tot), b = (float)(R.earnDrone / tot);
            GUI.DrawTexture(new Rect(x, y, w, 10), texBar);
            GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(x, y, w * a, 10), white);
            GUI.color = SweepGame.Cyan; GUI.DrawTexture(new Rect(x + w * a, y, w * b, 10), white);
            GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(x + w * (a + b), y, w * (1 - a - b), 10), white);
            GUI.color = Color.white; y += 14;
            GUI.Label(new Rect(x, y, w, 16), "<color=#f2c14e>집게 " + Mathf.RoundToInt(a * 100) + "%</color>  <color=#6fd3e8>드론 " + Mathf.RoundToInt(b * 100) + "%</color>  <color=#b69cff>폭발 " + Mathf.RoundToInt((1 - a - b) * 100) + "%</color>", small); y += 22;
            void Row(string k, string v) { GUI.Label(new Rect(x, y, w, 18), k, dim); GUI.Label(new Rect(x, y, w, 18), v, cost); y += 18; }
            Row("부순 것", R.broke.ToString());
            Row("최대 연쇄", R.chainBest + (R.chainBest > prevBestChain && R.chainBest >= 10 ? "  <color=#ff8a7a>새 기록!</color>" : ""));
            Row("최대 압축", R.packBest + (R.packBest > prevBestPack && R.packBest >= 5 ? "  <color=#ff8a7a>새 기록!</color>" : ""));
            if (R.cut > 0) Row("추심으로 떼인 것", "<color=#ee7766>-" + KNum.Fmt(R.cut) + "</color>");
            if (R.toBill > 0) Row("압류로 갚은 빚", "<color=#ff8a7a>" + KNum.Fmt(R.toBill) + "</color>");
            var c = sim.CurContract;
            if (R.contractOk) Row("의뢰 성공", "<color=#6fcf97>+" + KNum.Fmt(R.bonus) + "</color>");
            y += 6;
            // 청구서 막대 — 얼마 남았나
            if (sim.S.bill < SweepSim.Bills.Length && !sim.M.cleanReady)
            {
                double need = sim.BillAmount - sim.S.cash;
                if (need <= 0) GUI.Label(new Rect(x, y, w, 18), "<color=#6fcf97>청구서를 갚을 수 있다!</color>", label);
                else GUI.Label(new Rect(x, y, w, 18), "청구서까지 " + KNum.Fmt(need) + (R.Earned > 0 ? " · 약 " + Mathf.CeilToInt((float)(need / R.Earned)) + "판" : ""), dim);
                y += 24;
            }
            // 이번 판 새 기사
            int n = 0;
            for (int i = sim.M.news.Count - 1; i >= runNewsFrom && i >= 0 && n < 3; i--, n++)
            {
                var it = sim.M.news[i];
                if (n == 0) { GUI.Label(new Rect(x, y, w, 16), "이번 판 새 기사", head); y += 18; }
                if (GUI.Button(new Rect(x, y, w, 36), "<size=11>" + Clip(it.head, 30) + "</size>", btn)) { newsOpen = true; newsSel = i; it.read = true; }
                y += 40;
            }
        }

        static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ───────────────────────────────── 트리 — 청소선을 가운데 두고 가지 넷 (§8)

        static readonly Vector2[] Dir = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        static readonly Color[] BranchCol = { SweepGame.Amber, SweepGame.Cyan, SweepGame.Violet, SweepGame.Green };

        Vector2 Place(SweepSim.Node n, Vector2 c, float sc)
        {
            int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
            var u = Dir[b].normalized; var p = new Vector2(-u.y, u.x);
            return c + (u * (46 + n.depth * 62) + p * n.lane * 60) * sc;
        }

        void Line(Vector2 a, Vector2 b, Color col, float w)
        {
            var m = GUI.matrix;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = m * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, ang), Vector3.one);
            GUI.color = col; GUI.DrawTexture(new Rect(0, -w / 2, (b - a).magnitude, w), white);
            GUI.matrix = m; GUI.color = Color.white;
        }

        void Tree(Rect area)
        {
            var S = sim.S;
            GUI.DrawTexture(area, texCard);
            var c = area.center + new Vector2(0, 4); float sc = 0.6f;
            var N = SweepSim.Nodes;
            // 선
            for (int i = 0; i < N.Length; i++)
            {
                var st = sim.State(i); var p = Place(N[i], c, sc);
                int b = System.Array.IndexOf(SweepSim.BranchIds, N[i].branch);
                Color col = st == NodeSt.Hidden || st == NodeSt.Locked ? new Color(0.16f, 0.2f, 0.27f) : BranchCol[b] * new Color(1, 1, 1, 0.5f);
                if (N[i].par.Length == 0) Line(c, p, col, 2);
                foreach (var pid in N[i].par) { int j = System.Array.FindIndex(N, q => q.id == pid); Line(Place(N[j], c, sc), p, col, 2); }
            }
            // 가지 이름 · 지난 판 몫
            for (int b = 0; b < 4; b++)
            {
                var at = new Vector2(Dir[b].x < 0 ? area.x + 64 : area.xMax - 64, Dir[b].y < 0 ? area.y + 22 : area.yMax - 44);
                bool open = S.bill >= SweepSim.BranchNeed[b];
                string share = "";
                if (showResult && last != null && last.Earned > 0) { double v = b == 0 ? last.earnClaw : b == 1 ? last.earnDrone : b == 2 ? last.earnBlast : -1; if (v >= 0) share = "\n<size=10>지난 판 " + Mathf.RoundToInt((float)(v / last.Earned * 100)) + "%</size>"; }
                if (branchFlash[b] > 0) { GUI.color = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.Sin(Time.time * 10)); }
                center.normal.textColor = open ? BranchCol[b] : new Color(0.35f, 0.39f, 0.46f);
                GUI.Label(new Rect(at.x - 60, at.y - 12, 120, 34), SweepSim.BranchNames[b] + (open ? "" : "\n<size=10>잠김 · 청구서 " + SweepSim.BranchNeed[b] + "</size>") + (branchFlash[b] > 0 ? "\n<size=10>새로 열림!</size>" : share), center);
                GUI.color = Color.white;
            }
            center.normal.textColor = new Color(0.87f, 0.89f, 0.92f);
            // 청소선
            GUI.color = new Color(0.08f, 0.11f, 0.16f); GUI.DrawTexture(new Rect(c.x - 18, c.y - 18, 36, 36), texDisc);
            GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(c.x - 18, c.y - 18, 36, 36), texRing);
            GUI.color = Color.white; GUI.Label(new Rect(c.x - 30, c.y - 10, 60, 20), "<color=#f2c14e>▲</color>", center);
            // 칸
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            for (int i = 0; i < N.Length; i++)
            {
                var n = N[i]; var st = sim.State(i); var p = Place(n, c, sc);
                int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
                float rad = 13 + nodePulse[i] * 4;
                var rr = new Rect(p.x - rad, p.y - rad, rad * 2, rad * 2);
                Color bc = BranchCol[b];
                if (st == NodeSt.Hidden)
                {
                    GUI.color = new Color(0.1f, 0.12f, 0.17f); GUI.DrawTexture(rr, texDisc);
                    GUI.color = new Color(0.2f, 0.24f, 0.31f); GUI.DrawTexture(rr, texRing);
                    GUI.color = Color.white; GUI.Label(new Rect(p.x - 20, p.y - 9, 40, 18), "<color=#3d4a5e>?</color>", center);
                }
                else
                {
                    bool owned = S.lv[i] > 0;
                    GUI.color = st == NodeSt.Locked ? new Color(0.1f, 0.12f, 0.16f) : owned ? bc * new Color(0.35f, 0.35f, 0.35f, 1) : new Color(0.06f, 0.08f, 0.11f);
                    GUI.DrawTexture(rr, texDisc);
                    float breathe = st == NodeSt.Can ? 0.55f + 0.45f * Mathf.Sin(Time.time * 5.2f) : 1;
                    GUI.color = st == NodeSt.Max ? SweepGame.Amber2 : st == NodeSt.Can ? new Color(bc.r, bc.g, bc.b, breathe) : st == NodeSt.Poor ? new Color(0.25f, 0.29f, 0.36f) : new Color(0.18f, 0.2f, 0.25f);
                    GUI.DrawTexture(new Rect(rr.x - 1, rr.y - 1, rr.width + 2, rr.height + 2), texRing);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(p.x - 20, p.y - 9, 40, 18), st == NodeSt.Max ? "<color=#ffdf95>끝</color>" : (owned ? "<color=#ffffff>" : "<color=#7f8b9d>") + S.lv[i] + "</color>", center);
                    small.alignment = TextAnchor.UpperCenter; small.wordWrap = false;
                    GUI.Label(new Rect(p.x - 40, p.y + 14, 80, 14), st == NodeSt.Locked ? "" : n.name, small);
                    small.alignment = TextAnchor.UpperLeft; small.wordWrap = true;
                }
                if (i == selected) { GUI.color = Color.white; GUI.DrawTexture(new Rect(rr.x - 3, rr.y - 3, rr.width + 6, rr.height + 6), texRing); }
                if (GUI.Button(new Rect(p.x - 16, p.y - 16, 32, 32), GUIContent.none, GUIStyle.none))
                {
                    if (st == NodeSt.Can && (i == selected || shift))
                    {
                        int times = shift ? 50 : 1;
                        while (times-- > 0 && sim.State(i) == NodeSt.Can) sim.Buy(i);
                        nodePulse[i] = 1; OrbitSfx.Play("buy", 0.7f, 0.01f, 0.15f);
                    }
                    selected = i;
                }
            }
            GUI.Label(new Rect(area.x + 8, area.yMax - 18, area.width - 16, 16), "칸을 누르면 오른쪽에 자세히 · 한 번 더 누르면 산다 · Shift+누르기 = 살 수 있는 만큼", small);
        }

        void Detail(Rect r)
        {
            GUI.DrawTexture(r, texCard);
            float x = r.x + 10, y = r.y + 10, w = r.width - 20;
            var n = SweepSim.Nodes[selected]; var st = sim.State(selected); int lv = sim.S.lv[selected];
            int b = System.Array.IndexOf(SweepSim.BranchIds, n.branch);
            head.normal.textColor = BranchCol[b];
            GUI.Label(new Rect(x, y, w, 16), SweepSim.BranchNames[b] + " 가지 · 구간 " + n.seg, head); y += 20;
            head.normal.textColor = new Color(0.51f, 0.56f, 0.64f);
            if (st == NodeSt.Hidden || st == NodeSt.Locked)
            {
                GUI.Label(new Rect(x, y, w, 24), st == NodeSt.Locked ? n.name : "?", big); y += 30;
                string why = st == NodeSt.Locked ? "청구서 " + SweepSim.BranchNeed[b] + "을 갚으면 이 가지가 열린다." : "아직 안 보이는 칸.\n";
                if (st == NodeSt.Hidden)
                {
                    var need = new List<string>();
                    foreach (var pid in n.par) { int j = System.Array.FindIndex(SweepSim.Nodes, q => q.id == pid); if (sim.S.lv[j] <= 0) need.Add("「" + SweepSim.Nodes[j].name + "」"); }
                    if (need.Count > 0) why += string.Join(" · ", need) + "을 한 번 사면";
                    if (n.seg > sim.Seg) why += (need.Count > 0 ? "\n+ " : "") + "청구서 " + (n.seg - 1) + "을 갚으면";
                    why += " 보인다.";
                }
                GUI.Label(new Rect(x, y, w, 80), why, small);
                return;
            }
            GUI.Label(new Rect(x, y, w, 24), n.name, big); y += 28;
            GUI.Label(new Rect(x, y, w, 16), "레벨 " + lv + " / " + n.max + " · " + n.desc, small); y += 30;
            label.fontSize = 16;
            GUI.Label(new Rect(x, y, w, 22), st == NodeSt.Max ? "<color=#ffdf95>" + Val(n.id, lv) + "</color>" : Val(n.id, lv) + "  →  <color=#ffdf95>" + Val(n.id, lv + 1) + "</color>", label);
            label.fontSize = 14; y += 30;
            if (st == NodeSt.Max) { GUI.Label(new Rect(x, y, w, 18), "끝까지 올렸다", dim); return; }
            double c = sim.Cost(selected);
            GUI.Label(new Rect(x, y, w, 18), "가격", dim); GUI.Label(new Rect(x, y, w, 18), (st == NodeSt.Can ? "<color=#ffdf95>" : "<color=#ff9b8f>") + KNum.Fmt(c) + "</color>", cost); y += 20;
            GUI.Label(new Rect(x, y, w, 18), "돈", dim); GUI.Label(new Rect(x, y, w, 18), KNum.Fmt(sim.S.cash), cost); y += 28;
            bool can = st == NodeSt.Can;
            if (GUI.Button(new Rect(x, y, w, 34), can ? "사기 — " + KNum.Fmt(c) : KNum.Fmt(c - sim.S.cash) + " 모자람", can ? btn : btnOff) && can && sim.Buy(selected)) { nodePulse[selected] = 1; OrbitSfx.Play("buy", 0.7f, 0.01f, 0.15f); }
        }

        string Val(string id, int l)
        {
            switch (id)
            {
                case "c_pow": return "한 방 " + (1 + l);
                case "c_auto": return l > 0 ? "대고만 있어도" : "눌러야 친다";
                case "c_rad": return l > 0 ? "반지름 " + (22 + 10 * l) : "하나씩";
                case "c_spd": return Mathf.Max(0.38f, 0.8f - 0.06f * l).ToString("0.00") + "초";
                case "c_fuel": return (30 + 3 * l) + "초";
                case "c_find": return "판마다 " + l + "번";
                case "c_crit": return (5 * l) + "%";
                case "d_n": return (2 + l) + "대";
                case "d_spd": return Mathf.Max(0.4f, 1 - 0.1f * l).ToString("0.0") + "초마다";
                case "d_reach": return "거리 " + (80 + 15 * l);
                case "d_mag": return "+" + (25 * l) + "%";
                case "d_grade": return "한 방 " + (1 + l);
                case "d_pair": return l > 0 ? "둘씩" : "하나씩";
                case "b_n": return "판마다 " + (2 + l) + "발";
                case "b_pr": return "반경 " + (150 + 20 * l);
                case "b_cap": return (18 + 8 * l) + "개";
                case "b_pf": return "×" + (1 + 0.25f * l).ToString("0.00");
                case "b_br": return "+" + (15 * l) + "%";
                case "b_chain": return (25 + 7 * l) + "%";
                case "b_pack": return "개당 +" + (2 + 1.2f * l).ToString("0.0") + "%";
                case "e_val": return "×" + Mathf.Pow(1.25f, l).ToString("0.00");
                case "e_vault": return "+" + (50 * l) + "%";
                case "e_att": return "+" + (40 * l) + "%";
                case "e_talk": return "기한 +" + l + "판";
                case "e_guard": return "추심 " + (l > 0 ? 20 : 30) + "%";
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
            if (GUI.Button(new Rect(x + 150, 400, 300, 56), "(" + M.company + "대) 출발 ▸", bigBtn)) { sim.CloseCareer(); showResult = false; game.Save(); }
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
