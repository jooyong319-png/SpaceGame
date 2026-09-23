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
        float dueNag; public bool loanOpen; public float launchT; const float LaunchLen = 1.3f; int permitArmed = -1;
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
            showResult = true; bankruptArmed = false; last = sim.R; resultT = 2.6f; flow = 1; aucUsed = false; aucState = 0;
            resultAt = Time.time; endCash = sim.S.cash; gained = last.Earned + last.bonus + last.interest; flyers.Clear(); flyT = 0;
            bannerT = 0; banner = null;          // 판 중 예고가 조종실까지 남지 않게
            sim.M.flags.Remove("hint_seen_now");
            if (!sim.M.flags.Contains("hint_claw")) sim.M.flags.Add("hint_claw");
            if (sim.DronesOn && !sim.M.flags.Contains("hint_drone")) sim.M.flags.Add("hint_drone");
            if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb")) sim.M.flags.Add("hint_bomb");
            if (sim.S.orbit > 0 && !sim.M.flags.Contains("hint_p" + sim.S.orbit)) sim.M.flags.Add("hint_p" + sim.S.orbit);
        }

        public void Go()
        {
            if (sim.S.overdue && !sim.M.cleanReady) { dueNag = 1.6f; OrbitSfx.Play("tick", 0.6f, 0.6f, 0.05f); return; }   // 납부일 — 갚기 · 대출 · 파산 중 하나를 먼저
            bayOpen = false; flow = 0;
            prevBestChain = sim.M.bestChain; prevBestPack = sim.M.bestPack; runNewsFrom = sim.M.news.Count;
            showResult = false; bankruptArmed = false;
            sim.StartRun();
            launchT = LaunchLen; OrbitSfx.Play("launch", 1f); game.shake = 0.18f;   // 🚀 출발 — 창을 뚫고 나간다
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
            dueNag = Mathf.Max(0, dueNag - dt);
            if (launchT > 0) { launchT -= dt; if (launchT <= 0) OrbitSfx.Play("tick", 0.7f); }
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && sim.R.over && !sim.M.careerOpen && !sim.M.won && !newsOpen && paidT < 2.4f)
            {
                if (loanOpen || aucState > 0) { } else if (flow == 2) Go(); else flow = 2;   // Space — 결과 · 정비소 → 조종실, 조종실 → 출동
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { if (loanOpen) { loanOpen = false; pendLoan = 0; } else if (newsOpen) newsOpen = false; else bayOpen = false; }
        }

        void OnGUI()
        {
            if (sim == null) return;
            Styles();
            scale = Screen.height / RefH; vw = Screen.width / scale; ox = Mathf.Max(0, (vw - 960) / 2);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            Effects();
            if (!sim.R.over) { Storm(); WindowEdge(); Pops(); RunHud(); if (launchT > 0) Launch(); }
            if (sim.M.won) Ending();
            else if (sim.M.careerOpen) Career();
            else if (sim.R.over)
            {
                if (flow == 0) flow = 2;                                // 켜자마자 · 파산 뒤 = 조종실
                GUI.enabled = !loanOpen && aucState == 0;
                if (flow == 1) FlowResult(); else if (flow == 3) { Bay(); FlowBottom(); } else Cockpit();
                GUI.enabled = true;
                if (aucState > 0) Auction();
                if (loanOpen) LoanWin();
            }
            if (newsOpen) News();
            if (!sim.M.won && !CockpitView) Ticker();          // 조종실엔 궤도일보 모니터가 있다 — 아래 한 줄과 겹친다
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
        // 🔴 화성 모래 폭풍 — 22초마다 5초쯤 붉은 먼지가 화면을 덮는다 (화면만, 규칙은 그대로)
        void Storm()
        {
            var o = SweepSim.Orbits[sim.S.orbit];
            if (!o.storm || sim.R.clean) return;
            float ph = (float)(sim.R.t % 22.0);
            float a = ph < 14 ? 0 : ph < 15.5f ? (ph - 14) / 1.5f : ph < 19.5f ? 1 : ph < 21 ? 1 - (ph - 19.5f) / 1.5f : 0;
            if (a <= 0) return;
            GUI.color = new Color(0.72f, 0.36f, 0.2f, 0.42f * a); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            GUI.color = new Color(0.9f, 0.55f, 0.35f, 0.18f * a);
            for (int i = 0; i < 6; i++) { float x = Mathf.Repeat(Time.time * (60 + i * 25) + i * 170, vw + 400) - 200; GUI.DrawTexture(new Rect(x, 80 + i * 80, 380, 60), texDisc); }
            GUI.color = Color.white;
            if (ph > 14 && ph < 15.2f) GUI.Label(new Rect(0, 90, vw, 26), "<size=18><color=#ffb080>모래 폭풍!</color></size>", center);
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
                GUI.color = new Color(1, 1, 1, ta);
                GUI.Label(new Rect(0, 150 + dy, vw, 50), "<color=#ffdf95>" + (sim.R.clean ? "청산 출동" : o.name + " 궤도") + "</color>", title);
                var c = sim.CurContract;
                title.fontSize = 16;
                GUI.Label(new Rect(0, 200 + dy, vw, 26), "출동 " + sim.S.runs + " · 연료 " + Mathf.RoundToInt((float)sim.R.max) + "초" + (c != null && !sim.R.clean ? " · 의뢰: " + c.Value.text : ""), title);
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
                Item("청구서", KNum.Fmt(sim.BillAmount) + " · " + S.billDue + "판" + (S.debt > 0 ? " <color=#ee7766>빚 상환 " + Mathf.RoundToInt((float)sim.Cut * 100) + "%</color>" : ""), label);
            GUI.Label(new Rect(x, 14, 40, 20), "연료", dim);
            GUI.DrawTexture(new Rect(x + 34, 19, 160, 9), texBar);
            float fk = Mathf.Clamp01((float)(R.fuel / R.max));
            GUI.DrawTexture(new Rect(x + 34, 19, 160 * fk, 9), R.fuel < 6 ? texRed : texAmber);
            x += 210;
            if (R.maxShots > 0)
            {
                GUI.Label(new Rect(x, 14, 40, 20), "블랙홀", dim);
                for (int i = 0; i < R.maxShots; i++) { GUI.color = i < R.shots ? SweepGame.Violet : new Color(0.17f, 0.18f, 0.24f); GUI.DrawTexture(new Rect(x + 50 + i * 15, 18, 11, 11), texDisc); }
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
            if (R.maxShots > 0 && !R.over) SkillSlot(R);
            AutoSwitch();
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
            else if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb") && R.t < 14) hint = "블랙홀 스킬이 열렸다 — Q(또는 아래 칸)를 누르면 커서 자리에 3초 열려 빨아들이고 터진다";
            else if (sim.DronesOn && !sim.M.flags.Contains("hint_drone") && R.t < 8) hint = "드론은 알아서 줍는다 — 한 방에 부서지는 것만";
            else if (sim.S.orbit > 0 && !sim.M.flags.Contains("hint_p" + sim.S.orbit) && R.t < 8) hint = PlanetHint[sim.S.orbit];   // 새 행성 첫 판
            if (hint != null) GUI.Label(new Rect(vw / 2 - 360, RefH - 70, 720, 20), hint, center);
            if (game.timeScale > 1) GUI.Label(new Rect(vw - 120, RefH - 46, 106, 18), "시험 속도 ×3", cost);
        }

        // ───────────────────────────────── 조종실 — 첫 화면 (사장님 09-23: "첫 화면 자체를 우주선 화면 컨셉으로 · 유저 친화적으로")
        // 가운데 창 = 지금 내 궤도 (사면 바로 창밖에 보인다) · 계기판마다 할 일 하나 · 강화는 정비고(네 칸 · 36칸)

        public bool bayOpen; int bayTab;
        public bool CockpitView => flow == 2 && sim != null && sim.R.over && !sim.M.careerOpen && !sim.M.won;   // 조종실 — 카메라가 물러나 지구가 창 가운데 (09-23 부활)
        public int flow;                         // 0 출동 중 · 1 결산 · 2 청구서 · 3 정비고
        static readonly Color[] BranchCol = { SweepGame.Amber, SweepGame.Cyan, SweepGame.Violet, SweepGame.Green };
        static readonly string[] BayDesc = { "조준점 하나 → 넓은 착탄 → 한 번에 여럿", "알아서 줍는다 — 한 방에 부서지는 것만", "블랙홀 스킬 · 지구에서 연료 보급", "돈 · 청구서 · 대출 · 기사" };
        public static readonly Rect Win = new Rect(200, 44, 560, 344);

        static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        void Frame(Rect r, Color c, float w)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), white); GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), white);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), white); GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), white);
            GUI.color = Color.white;
        }

        bool Panel(Rect r, string label, string right, Color edge, bool clickable = true)   // 안에 단추가 있는 칸은 clickable = false (칸 전체 단추가 안쪽 클릭을 가로챈다)
        {
            bool hover = clickable && r.Contains(Event.current.mousePosition);
            GUI.DrawTexture(r, texCard2);
            Frame(r, hover ? edge : new Color(0.14f, 0.2f, 0.28f), hover ? 2 : 1.5f);
            GUI.Label(new Rect(r.x + 9, r.y + 6, r.width - 18, 16), label, head);
            if (right != null) GUI.Label(new Rect(r.x + 9, r.y + 5, r.width - 18, 16), right, cost);
            return clickable && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }



        // ───────────────────────────────── ① 결산 · ② 청구서 · ③ 정비고 아래 줄


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
            else if (R.cut > 0) GUI.Label(new Rect(bx, by + 44, bw, 20), "<color=#ee7766>빚 상환으로 떼인 것 -" + KNum.Fmt(R.cut) + "</color>", label);

            // 🔨 고철 경매 — 왼쪽 칸 아래 (경매장을 샀고, 이번 판 수입이 있으면 한 번)
            if (sim.AucOpen && !aucUsed && R != null && !R.clean && R.Earned >= 1 && aucState == 0 && S.cash >= 1)
            {
                var ab = new Rect(L.x + 16, L.yMax - 48, L.width - 32, 38);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4);
                GUI.color = Color.Lerp(new Color(0.3f, 0.2f, 0.05f), new Color(0.45f, 0.3f, 0.07f), pulse); GUI.DrawTexture(ab, white); GUI.color = Color.white;
                Frame(ab, SweepGame.Amber, 2);
                GUI.Label(ab, "<size=16><color=#ffdf95><b>고철 경매</b></color></size>  <size=13><color=#e8c77e>이번 판 +" + KNum.Fmt(System.Math.Min(R.Earned, S.cash)) + " 걸기 ▸</color></size>", center);
                if (GUI.Button(ab, GUIContent.none, GUIStyle.none)) AucPrep();
            }

            // 오른쪽 아래 — 다음 해금 (청구서를 갚으면 열리는 것)
            var RB = new Rect(cx + 10, 270, 410, 120);
            // 연체가 이어져 사실상 못 갚는 벽 — 다음 해금 대신 파산 안내 (설계상 첫 파산 자리. 구석 단추만으로는 모른다)
            bool stuck = sim.CanBankrupt && S.overdue && S.cash + sim.LoanCap < sim.BillAmount;   // 대출 한도로도 모자라다
            if (stuck)
            {
                Panel2(RB, "<color=#ff9b8f>대출 한도로도 못 갚는다</color>");
                GUI.Label(new Rect(RB.x + 16, RB.y + 30, RB.width - 32, 20), "<size=13>파산하면 빚이 사라지고 <color=#ffdf95>신용 +" + S.creditPending + "</color></size>", label);
                GUI.Label(new Rect(RB.x + 16, RB.y + 50, RB.width - 32, 20), "<size=13>신용으로 경력을 사면 다음 회사는 처음부터 더 세다</size>", label);
                var bb = new Rect(RB.x + 16, RB.y + 76, RB.width - 32, 34);
                if (GUI.Button(bb, bankruptArmed ? "<color=#ffb3a8>정말? 한 번 더 누르면 파산</color>" : "<color=#ffb3a8>파산하고 새 회사로 ▸</color>", btn))
                {
                    if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; flow = 2; } else bankruptArmed = true;
                }
            }
            else Panel2(RB, sim.S.bill < SweepSim.Bills.Length ? "청구서를 갚으면 열린다" : null);
            if (!stuck && S.bill < SweepSim.Bills.Length)
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
            if (GUI.Button(new Rect(x0 + (w + gap) * 2, yb, w, 66), "조종실로 ▸", bigBtn)) flow = 2;
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
                if (sim.M.cleanReady) { GUI.Label(r, "<size=20><color=#6fcf97>빚 청산</color></size>", center); return; }
                // 청구서는 끝 — 남은 빚을 갚아야 청산 출동
                GUI.Label(new Rect(r.x, r.y + 6, r.width, 30), "<size=22><color=#ffdf95>빚 " + KNum.Fmt(S.debt) + "</color></size>", center);
                GUI.Label(new Rect(r.x, r.y + 38, r.width, 22), "<size=13>" + (S.cash > 0 ? "눌러서 갚기 — 다 갚으면 청산 출동" : "다 갚으면 청산 출동") + "</size>", center);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) sim.RepayDebt();
                return;
            }
            bool can = S.cash >= sim.BillAmount;
            double need = sim.BillAmount - S.cash;
            bool loanPay = !can && S.overdue && need <= sim.LoanCap;
            float pulse = can ? 0.5f + 0.5f * Mathf.Sin(Time.time * 5) : 0;
            GUI.color = can ? Color.Lerp(new Color(0.42f, 0.1f, 0.1f), new Color(0.6f, 0.16f, 0.14f), pulse) : new Color(0.3f, 0.08f, 0.09f);
            GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, can ? Color.Lerp(SweepGame.Red, Color.white, pulse * 0.5f) : new Color(0.5f, 0.2f, 0.18f), 2);
            GUI.Label(new Rect(r.x, r.y + 6, r.width, 30), "<size=24><color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color></size>", center);
            string sub = S.overdue ? "<color=#ffb3a8>오늘 납부일</color>" : S.billDue + "판 남음";
            if (can) sub += " · <color=#ffffff>눌러서 갚기</color>";
            else if (loanPay) sub += " · <color=#ffffff>대출 " + KNum.Fmt(need) + " 받아 갚기</color>";
            GUI.Label(new Rect(r.x, r.y + 38, r.width, 22), "<size=13>" + sub + "</size>", center);
            if (loanPay) GUI.Label(new Rect(r.x, r.yMax + 2, r.width, 16), "<size=11>빚 +" + KNum.Fmt(need * SweepSim.LoanMult) + " (판 수입 30%씩 상환)</size>", center);
            if (dueNag > 0) GUI.Label(new Rect(r.x - 40, r.y - 22, r.width + 80, 18), "<color=#ff9b8f><size=12>납부일 — 먼저 갚거나 · 대출받거나 · 파산</size></color>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { if (can) sim.PayBill(); else if (loanPay) RequestLoan(sim.BillAmount - S.cash, true); }
        }

        void FlowBottom()
        {
            float y = 512;
            if (GUI.Button(new Rect(ox + 700, y, 246, 62), "조종실로 ▸", bigBtn)) flow = 2;
            GUI.Label(new Rect(ox + 700, y + 64, 246, 14), "<size=10>Space</size>", center);
        }

        // ───────────────────────────────── 조종실 — 판과 판 사이의 집. 출동은 여기서만
        // 시안(cockpit.html 1600×1000)을 960×600 으로 — 사다리꼴 창 · 양옆 선체 판 · 아래 조종대
        static readonly Vector2[] WinPoly = { new Vector2(198, 42), new Vector2(762, 42), new Vector2(834, 336), new Vector2(126, 336) };
        static Texture2D hullTex;
        static bool InQuad(Vector2[] q, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = q.Length - 1; i < q.Length; j = i++)
                if ((q[i].y > y) != (q[j].y > y) && x < (q[j].x - q[i].x) * (y - q[i].y) / (q[j].y - q[i].y) + q[i].x) inside = !inside;
            return inside;
        }
        static Vector2[] Grow(Vector2[] q, float d) => new[] { q[0] + new Vector2(-d * 1.1f, -d), q[1] + new Vector2(d * 1.1f, -d), q[2] + new Vector2(d * 1.1f, d), q[3] + new Vector2(-d * 1.1f, d) };
        static void BuildHull()
        {
            const int W = 960, H = 600;
            hullTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Color32 bg = new Color32(7, 11, 17, 255), plate = new Color32(12, 19, 28, 255), rim = new Color32(42, 59, 82, 255), edge = new Color32(59, 81, 112, 255);
            Vector2[] outer = Grow(WinPoly, 8), inner = Grow(WinPoly, 1.4f);
            var leftPlate = new[] { new Vector2(0, 0), new Vector2(198, 42), new Vector2(126, 336), new Vector2(0, 384) };
            var rightPlate = new[] { new Vector2(W, 0), new Vector2(762, 42), new Vector2(834, 336), new Vector2(W, 384) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    Color32 c;
                    if (InQuad(WinPoly, fx, fy)) c = new Color32(0, 0, 0, 0);
                    else if (InQuad(inner, fx, fy)) c = edge;
                    else if (InQuad(outer, fx, fy)) c = rim;
                    else if (fy > 336 && (fy > 384 || InQuad(new[] { new Vector2(0, 384), new Vector2(126, 336), new Vector2(834, 336), new Vector2(W, 384) }, fx, fy) || fy >= 384))
                    {
                        float k = Mathf.InverseLerp(336, H, fy);   // 조종대 — 위가 밝고 아래로 어두워진다
                        c = Color32.Lerp(new Color32(18, 28, 41, 255), new Color32(10, 16, 25, 255), k);
                    }
                    else if (InQuad(leftPlate, fx, fy) || InQuad(rightPlate, fx, fy)) c = plate;
                    else c = bg;
                    px[(H - 1 - y) * W + x] = c;
                }
            hullTex.SetPixels32(px); hullTex.Apply();
        }

        // 계기판 — 테두리(베젤) · 제목줄 · 안쪽 어두운 화면
        static readonly Color PlateCol = new Color(0.075f, 0.11f, 0.16f), Bezel = new Color(0.15f, 0.21f, 0.29f), ScreenCol = new Color(0.03f, 0.05f, 0.075f);
        bool Plate(Rect r, string cap, string right, Color hot, bool clickable)
        {
            bool hover = clickable && r.Contains(Event.current.mousePosition);
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = PlateCol; GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, hover ? hot : Bezel, hover ? 2.5f : 2f);
            GUI.Label(new Rect(r.x + 10, r.y + 6 - 3, r.width - 20, 22), "<size=11><color=#8a9bb3>" + cap + "</color></size>", label);
            if (!string.IsNullOrEmpty(right)) GUI.Label(new Rect(r.x + 10, r.y + 6 - 3, r.width - 20, 22), "<size=11>" + right + "</size>", cost);
            return clickable && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
        Rect Scr(Rect r, float top, float h) { var sr = new Rect(r.x + 8, r.y + top, r.width - 16, h); GUI.color = ScreenCol; GUI.DrawTexture(sr, white); GUI.color = Color.white; return sr; }
        string Led(Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">●</color> ";

        void Cockpit()
        {
            var S = sim.S; var M = sim.M;
            if (hullTex == null) BuildHull();
            // 선체 — 가운데 960 밖(넓은 화면)은 바탕색
            GUI.color = new Color(7 / 255f, 11 / 255f, 17 / 255f); GUI.DrawTexture(new Rect(0, 0, ox + 1, RefH), white); GUI.DrawTexture(new Rect(ox + 959, 0, vw - ox - 959, RefH), white); GUI.color = Color.white;
            GUI.DrawTexture(new Rect(ox, 0, 960, 600), hullTex);
            GUI.color = new Color(0.2f, 0.27f, 0.36f);                       // 리벳
            for (int i = 0; i <= 12; i++) GUI.DrawTexture(new Rect(ox + 198 + 564 * i / 12f - 2, 30, 4, 4), texDisc);
            for (int i = 0; i <= 18; i++) { float x = 36 + i * 49; GUI.DrawTexture(new Rect(ox + x - 2, 365 + (x < 126 || x > 834 ? 12 : 0), 4, 4), texDisc); }
            GUI.color = Color.white;

            // 위 — 돈 (창 위 가운데)
            big.fontSize = 22 + Mathf.RoundToInt(game.creditPulse * 6);
            GUI.Label(new Rect(ox + 330, 2, 300, 28), "<size=13><color=#8a9bb3>돈</color></size>  " + KNum.Fmt(shown), new GUIStyle(big) { alignment = TextAnchor.MiddleCenter });
            big.fontSize = 20;
            CreditScreen = new Vector2((ox + 480) * scale, 16 * scale);
            // 창 안 — 납부 완료 알림
            if (paidT > 0 && paidBill > 0)
            {
                var pb = SweepSim.Bills[paidBill - 1];
                title.fontSize = 24; GUI.Label(new Rect(ox + 200, 60, 560, 32), "<color=#ffdf95>납부 완료</color> · " + pb.t, title);
                title.fontSize = 16; GUI.Label(new Rect(ox + 200, 92, 560, 24), pb.perk, title);
            }

            // ① 청구서 단말 (왼쪽 위)
            var bt = new Rect(ox + 15, 36, 176, 196);
            bool due = S.overdue && !M.cleanReady;
            string dueTxt = M.cleanReady ? Led(SweepGame.Green) + "빚 청산" : S.bill >= SweepSim.Bills.Length ? Led(SweepGame.Red) + "빚만 남음" : due ? "<color=#ff8a7a>" + Led(SweepGame.Red) + "오늘 납부일</color>" : Led(SweepGame.Amber) + "기한 " + S.billDue + "판";
            Plate(bt, "청구서 단말", dueTxt, SweepGame.Amber, false);
            var sr = Scr(bt, 26, 104);
            if (M.cleanReady) GUI.Label(new Rect(sr.x + 8, sr.y + 8, sr.width - 16, 60), "<size=15><color=#6fcf97>빚 청산!</color></size>\n<size=12>청산 출동만 남았다</size>", label);
            else if (S.bill >= SweepSim.Bills.Length) GUI.Label(new Rect(sr.x + 8, sr.y + 8, sr.width - 16, 80), "<size=12>청구서는 끝</size>\n<size=18><color=#ffb3a8>빚 " + KNum.Fmt(S.debt) + "</color></size>\n<size=11>다 갚으면 청산 출동</size>", label);
            else
            {
                var b = SweepSim.Bills[S.bill];
                GUI.Label(new Rect(sr.x + 8, sr.y + 5 - 3, sr.width - 16, 22), "<size=11><color=#8a9bb3>케슬러 금융 · 청구서 " + (S.bill + 1) + " / " + SweepSim.Bills.Length + "</color></size>", label);
                GUI.Label(new Rect(sr.x + 8, sr.y + 22 - 3, sr.width - 16, 24), "<size=13>" + b.t + "</size>", label);
                GUI.Label(new Rect(sr.x + 8, sr.y + 38, sr.width - 16, 28), "<size=22><color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color></size>", label);
                float prog = Mathf.Clamp01((float)(S.cash / System.Math.Max(1, sim.BillAmount)));
                GUI.color = new Color(0.1f, 0.15f, 0.2f); GUI.DrawTexture(new Rect(sr.x + 8, sr.y + 70, sr.width - 16, 6), white);
                GUI.color = prog >= 1 ? SweepGame.Green : SweepGame.Amber; GUI.DrawTexture(new Rect(sr.x + 8, sr.y + 70, (sr.width - 16) * prog, 6), white); GUI.color = Color.white;
                GUI.Label(new Rect(sr.x + 8, sr.y + 80 - 3, sr.width - 16, 26), "<size=11><color=#8a9bb3>갚으면 →</color> <color=#ffdf95>" + Clip(b.perk, 11) + "</color></size>", label);
            }
            // 단말 아래 단추 — 갚기 · 대출받아 갚기 · 대출 창구
            float by = bt.y + 136;
            if (!M.cleanReady && S.bill < SweepSim.Bills.Length)
            {
                bool can = S.cash >= sim.BillAmount; double need = sim.BillAmount - S.cash; bool loanPay = !can && need <= sim.LoanCap;
                if (can) { if (GUI.Button(new Rect(bt.x + 8, by, bt.width - 16, 24), "<size=13>갚기</size>", btn)) sim.PayBill(); }
                else if (loanPay && due) { if (GUI.Button(new Rect(bt.x + 8, by, bt.width - 16, 24), "<size=12>대출 " + KNum.Fmt(need) + " 받아 갚기</size>", btn)) RequestLoan(need, true); }
                else GUI.Label(new Rect(bt.x + 8, by, bt.width - 16, 24), "<size=12><color=#8a9bb3>" + Mathf.RoundToInt((float)(S.cash / System.Math.Max(1, sim.BillAmount)) * 100) + "% 모였다</color></size>", center);
            }
            // 대출 창구 — 잘 보이게 (사장님: 「너무 안 보여」). 호박색 테두리 · 밝은 글씨 · 빚이 있으면 옆에 빨갛게
            if (!M.cleanReady)
            {
                var lb = new Rect(bt.x + 8, by + 27, bt.width - 16, 30);
                bool lh = lb.Contains(Event.current.mousePosition);
                GUI.color = lh ? new Color(0.36f, 0.26f, 0.08f) : new Color(0.25f, 0.18f, 0.06f); GUI.DrawTexture(lb, white); GUI.color = Color.white;
                Frame(lb, lh ? SweepGame.Amber2 : SweepGame.Amber, 2);
                GUI.Label(lb, "<size=13><color=#ffdf95>대출 창구 ▸</color></size>" + (S.debt > 0 ? "  <size=11><color=#ffb3a8>빚 " + KNum.Fmt(S.debt) + "</color></size>" : ""), center);
                if (GUI.Button(lb, GUIContent.none, GUIStyle.none)) loanOpen = true;
            }

            // ② 출동 보고 (왼쪽 가운데)
            var rp = new Rect(ox + 15, 236, 140, 118);
            double lE = S.lastClaw + S.lastDrone + S.lastBlast;
            Plate(rp, "출동 보고 · " + S.runs, S.lastBroke >= 0 ? "<color=#6fcf97>+" + KNum.Fmt(lE) + "</color>" : "", SweepGame.Amber, false);
            var rs = Scr(rp, 26, 84);
            if (S.lastBroke < 0) GUI.Label(new Rect(rs.x + 8, rs.y + 8, rs.width - 16, 40), "<size=12><color=#8a9bb3>아직 출동 전</color></size>", label);
            else
            {
                double tot = System.Math.Max(1, lE);
                float bx = rs.x + 8, bw = rs.width - 16;
                float w0 = bw * (float)(S.lastClaw / tot), w1 = bw * (float)(S.lastDrone / tot);
                GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(bx, rs.y + 8, w0, 6), white);
                GUI.color = SweepGame.Cyan; GUI.DrawTexture(new Rect(bx + w0, rs.y + 8, w1, 6), white);
                GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(bx + w0 + w1, rs.y + 8, bw - w0 - w1, 6), white); GUI.color = Color.white;
                GUI.Label(new Rect(bx, rs.y + 20 - 3, bw, 24), "<size=12><color=#8a9bb3>부순 것</color></size>", label); GUI.Label(new Rect(bx, rs.y + 20 - 3, bw, 24), "<size=12>" + S.lastBroke + "</size>", cost);
                GUI.Label(new Rect(bx, rs.y + 38 - 3, bw, 24), "<size=12><color=#8a9bb3>최대 연쇄</color></size>", label); GUI.Label(new Rect(bx, rs.y + 38 - 3, bw, 24), "<size=12>" + S.lastChain + "</size>", cost);
                GUI.Label(new Rect(bx, rs.y + 56 - 3, bw, 24), "<size=12><color=#8a9bb3>의뢰</color></size>", label);
                GUI.Label(new Rect(bx, rs.y + 56 - 3, bw, 24), S.lastContract == 0 ? "<size=12>—</size>" : S.lastContract == 1 ? "<size=12><color=#6fcf97>성공</color></size>" : "<size=12><color=#ee7766>실패</color></size>", cost);
            }

            // ③ 궤도일보 (오른쪽 위) — 누르면 신문
            var nr = new Rect(ox + 769, 36, 176, 150);
            int unread = sim.Unread;
            if (Plate(nr, "궤도일보", unread > 0 ? "<color=#ff8a7a>새 기사 " + unread + "</color>" : "기사 " + M.news.Count, SweepGame.Amber, true)) { newsOpen = true; newsSel = -1; }
            var pr = new Rect(nr.x + 8, nr.y + 26, nr.width - 16, nr.height - 34);
            GUI.DrawTexture(pr, texPaper);
            GUI.DrawTexture(new Rect(pr.x + 6, pr.y + 20, pr.width - 12, 2), texInk);
            paperSmall.fontSize = 11; GUI.Label(new Rect(pr.x + 6, pr.y + 3 - 3, pr.width - 12, 22), "궤도일보 · 출동 " + S.runs + "일째", paperSmall);
            if (M.news.Count > 0)
            {
                var it = M.news[M.news.Count - 1];
                paperBody.fontSize = 13; GUI.Label(new Rect(pr.x + 6, pr.y + 26, pr.width - 12, 40), "<b>" + Clip(it.head, 28) + "</b>", paperBody);
                paperBody.fontSize = 11; GUI.Label(new Rect(pr.x + 6, pr.y + 66, pr.width - 12, 44), Clip(it.body, 40), paperBody);
            }
            paperBody.fontSize = 14;

            // ④ 회사 명판 (오른쪽 가운데) — 신용 · 파산 스위치
            var cp = new Rect(ox + 805, 196, 140, 118);
            Plate(cp, "회사 명판", M.company + "대", SweepGame.Amber, false);
            var cs = Scr(cp, 26, 84);
            GUI.Label(new Rect(cs.x + 8, cs.y + 6 - 3, cs.width - 16, 24), "<size=12><color=#ffdf95>궤도 청소부 (" + M.company + "대)</color></size>", label);
            GUI.Label(new Rect(cs.x + 8, cs.y + 26 - 3, cs.width - 16, 24), "<size=12><color=#8a9bb3>쌓인 신용</color></size>", label); GUI.Label(new Rect(cs.x + 8, cs.y + 26 - 3, cs.width - 16, 24), "<size=12>+" + S.creditPending + "</size>", cost);
            if (sim.CanBankrupt)
            {
                if (GUI.Button(new Rect(cs.x + 6, cs.y + 50, cs.width - 12, 26), bankruptArmed ? "<color=#ffb3a8><size=11>한 번 더 → 파산</size></color>" : "<color=#ffb3a8><size=11>파산 스위치 (덮개)</size></color>", btnOff))
                { if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; flow = 2; } else bankruptArmed = true; }
            }
            else GUI.Label(new Rect(cs.x + 6, cs.y + 50, cs.width - 12, 26), "<size=11><color=#5a2a24>파산 스위치 (잠김)</color></size>", center);

            // ⑤ 정비고 해치 (아래 왼쪽) — 누르면 정비소로
            var ht = new Rect(ox + 48, 410, 250, 116);
            int canN = 0; for (int b = 0; b < 4; b++) canN += CanCount(b);
            Frame(new Rect(ht.x - 1, ht.y - 1, ht.width + 2, ht.height + 2), SweepGame.Amber, 1.5f);
            if (Plate(ht, "정비고 해치", canN > 0 ? "<color=#f2c14e>살 수 있는 칸 " + canN + "</color>" : "", SweepGame.Amber, true)) flow = 3;
            var hs = Scr(ht, 26, 52);
            float cw4 = hs.width / 4;
            for (int b = 0; b < 4; b++)
            {
                var col = b == 0 ? SweepGame.Amber : b == 1 ? SweepGame.Cyan : b == 2 ? SweepGame.Violet : SweepGame.Green;
                GUI.Label(new Rect(hs.x + b * cw4, hs.y + 6 - 3, cw4, 24), "<size=11><color=#" + ColorUtility.ToHtmlStringRGB(col) + ">" + SweepSim.BranchNames[b].Split(' ')[0] + "</color></size>", center);
                GUI.Label(new Rect(hs.x + b * cw4, hs.y + 26 - 3, cw4, 24), "<size=11>" + Owned(b) + " / " + Total(b) + "</size>", center);
            }
            GUI.Label(new Rect(ht.x, ht.yMax - 32, ht.width, 26), "<size=17><color=#ffdf95>정비고로 ▾</color></size>", center);

            SweepSim.Contract? c;
            // ⑥ 항로 다이얼 · 의뢰 카드 (아래 오른쪽) — 행성 다섯. 열린 곳은 누르면 간다, 판매 중이면 허가증을 산다 (두 번)
            var dl = new Rect(ox + 662, 398, 250, 132);
            Plate(dl, "항로 다이얼", "의뢰 카드", SweepGame.Amber, false);
            int hoverP = -1;
            if (!M.cleanReady)
                for (int i = 0; i < SweepSim.Orbits.Length; i++)
                {
                    var o = SweepSim.Orbits[i];
                    var cell = new Rect(dl.x + 8 + i * 47, dl.y + 24, 45, 58);
                    var ic = new Rect(cell.x + 6, cell.y + 2, 33, 33);
                    bool open = sim.Open(i), sale = sim.OnSale(i);
                    if (cell.Contains(Event.current.mousePosition)) hoverP = i;
                    if (i == S.orbit) { GUI.color = new Color(0.95f, 0.76f, 0.31f, 0.18f); GUI.DrawTexture(cell, white); GUI.color = Color.white; Frame(cell, SweepGame.Amber, 2); }
                    GUI.color = open ? Color.white : sale ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.13f, 0.14f, 0.17f);
                    GUI.DrawTexture(ic, PlanetArt.Get(i).texture);
                    if (i == 4) { GUI.color = open ? new Color(0.9f, 0.82f, 0.6f, 0.8f) : GUI.color; GUI.DrawTexture(new Rect(ic.x - 5, ic.center.y - 2, ic.width + 10, 3), white); }
                    GUI.color = Color.white;
                    string sub = open ? "<color=#dde3ea>" + o.name + "</color>" : sale ? (permitArmed == i ? "<color=#ffdf95>한 번 더</color>" : "<color=#ffdf95>" + KNum.Fmt(o.permit) + "</color>") : "<color=#5a6a80>청구서 " + o.sell + "</color>";
                    GUI.Label(new Rect(cell.x - 4, cell.y + 36, cell.width + 8, 22), "<size=10>" + sub + "</size>", center);
                    if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    {
                        if (open) { sim.SetOrbit(i); permitArmed = -1; }
                        else if (sale && S.cash >= o.permit) { if (permitArmed == i) { sim.BuyPermit(i); permitArmed = -1; OrbitSfx.Play("buy", 0.9f); } else permitArmed = i; }
                        else OrbitSfx.Play("tick", 0.5f, 0.6f, 0.05f);
                    }
                }
            var ds = Scr(dl, 86, 38);
            if (hoverP >= 0)
            {
                var o = SweepSim.Orbits[hoverP];
                // 첫 줄 = 이름 · 배수 · 상태 (짧게), 둘째 줄 = 특징 — 칸 폭 230 에 맞춘다
                string st = sim.Open(hoverP) ? "<color=#6fcf97>열림</color>" : sim.OnSale(hoverP) ? (S.cash >= o.permit ? "<color=#ffdf95>허가증 " + KNum.Fmt(o.permit) + " · 두 번</color>" : "<color=#ee7766>허가증 " + KNum.Fmt(o.permit) + "</color>") : "<color=#8a9bb3>청구서 " + o.sell + " 뒤 판매</color>";
                GUI.Label(new Rect(ds.x + 6, ds.y - 2, ds.width - 12, 22), "<size=12><color=#ffdf95>" + o.name + " ×" + o.mult + "</color>  " + st + "</size>", label);
                GUI.Label(new Rect(ds.x + 6, ds.y + 16, ds.width - 12, 22), "<size=11><color=#8a9bb3>" + o.desc + "</color></size>", label);
                c = null;
            }
            else c = sim.CurContract;
            if (hoverP >= 0) { }
            else if (c != null && !M.cleanReady)
            {
                GUI.Label(new Rect(ds.x + 8, ds.y + 4 - 3, ds.width - 70, 26), "<size=12><color=#8a9bb3>의뢰</color> " + c.Value.text + "</size>", label);
                GUI.Label(new Rect(ds.x + 8, ds.y + 22 - 3, ds.width - 70, 24), "<size=11><color=#8a9bb3>성공하면 판 수입 +" + (25 + 10 * sim.Lv("e_quest")) + "%</color></size>", label);
                if (!S.rerolled && GUI.Button(new Rect(ds.xMax - 58, ds.y + 10, 52, 24), "<size=11>바꾸기</size>", btnOff)) sim.Reroll();
            }
            else GUI.Label(new Rect(ds.x + 8, ds.y + 12 - 3, ds.width - 16, 26), "<size=12><color=#8a9bb3>의뢰는 청구서 2 뒤에 들어온다</color></size>", label);

            // ⑦ 출동 버튼 (가운데 아래) — 받침 위에 둥근 누름 버튼, 윗면에 「출동」
            {
                float cxm = ox + 480, fy0 = 458, fw = 150, fh = 62, side = 16;
                var hit = new Rect(cxm - fw / 2, fy0, fw, fh + side);
                bool hover = !due && hit.Contains(Event.current.mousePosition);
                bool press = hover && Mouse.current != null && Mouse.current.leftButton.isPressed;
                float dip = press ? 10 : 0;                                   // 누르면 몸통이 받침 속으로
                float glow = due ? 0 : 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
                Color face = due ? new Color(0.32f, 0.3f, 0.28f) : hover ? new Color(1f, 0.8f, 0.36f) : new Color(0.95f, 0.72f, 0.26f);
                Color wall = due ? new Color(0.18f, 0.17f, 0.16f) : new Color(0.55f, 0.36f, 0.08f);
                // 빛 번짐
                GUI.color = new Color(0.95f, 0.76f, 0.31f, due ? 0 : 0.10f + 0.10f * glow + (hover ? 0.08f : 0)); GUI.DrawTexture(new Rect(cxm - 120, fy0 - 26, 240, 150), texDisc);
                // 받침 (어두운 테 + 그림자)
                GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(cxm - 92, fy0 + 24, 184, 76), texDisc);
                GUI.color = new Color(0.1f, 0.13f, 0.18f); GUI.DrawTexture(new Rect(cxm - 88, fy0 + 16, 176, 76), texDisc);
                GUI.color = new Color(0.17f, 0.22f, 0.3f); GUI.DrawTexture(new Rect(cxm - 84, fy0 + 14, 168, 72), texDisc);
                GUI.color = new Color(0.05f, 0.07f, 0.1f); GUI.DrawTexture(new Rect(cxm - fw / 2 - 4, fy0 + side + 2, fw + 8, fh + 4), texDisc);
                // 몸통 옆면
                float fy = fy0 + dip, sh = side - dip * 0.8f;
                GUI.color = wall; GUI.DrawTexture(new Rect(cxm - fw / 2, fy + sh, fw, fh), texDisc);
                GUI.DrawTexture(new Rect(cxm - fw / 2, fy + fh / 2, fw, sh), white);
                // 윗면 + 반사
                GUI.color = face; GUI.DrawTexture(new Rect(cxm - fw / 2, fy, fw, fh), texDisc);
                GUI.color = new Color(1, 1, 1, due ? 0.05f : 0.22f); GUI.DrawTexture(new Rect(cxm - fw * 0.32f, fy + 6, fw * 0.5f, fh * 0.32f), texDisc);
                GUI.color = Color.white;
                // 윗면 글자
                string word = due ? "납부일" : M.cleanReady ? "청산" : "출동";
                GUI.Label(new Rect(cxm - fw / 2, fy + 2, fw, fh - 4), "<size=" + (due ? 20 : 28) + "><b><color=" + (due ? "#6a655e" : "#3a2306") + ">" + word + "</color></b></size>", center);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none) && paidT < 2.4f) Go();
            }

            // 아래 한 줄
            string tip = due ? "<color=" + (dueNag > 0 ? "#ff9b8f" : "#b8a89a") + ">납부일 — 왼쪽 청구서 단말: 갚기 · 대출 · 또는 파산</color>" : "Space = 출동 · 창밖 = 지금 내 궤도 · 궤도 넓히기 " + Mathf.RoundToInt((float)(sim.Widen - 1) * 100) + "% · 한 판 " + Mathf.RoundToInt((float)sim.FuelMax) + "초";
            GUI.Label(new Rect(ox + 200, 570 - 3, 560, 26), "<size=12>" + tip + "</size>", center);
        }

        // ───────────────────────────────── 대출 창구 (모달) — 내역 · 받기 · 갚기
        // 🔨 고철 경매 — 주식 봉 차트. 봉 하나가 서면 「파시겠습니까?」 (사장님 09-23)
        // aucState 1 준비 · 2 봉이 서는 중 · 5 고르기 · 3 낙찰 · 4 유찰
        int aucState; bool aucUsed, aucRecord; float aucT, aucEndT, aucNextTick, aucTarget = 2f;
        double aucGot, aucStakeShown, aucOpenP, aucCloseP, aucHi, aucLo;
        struct Candle { public float o, c, h, l; }
        readonly List<Candle> aucCandles = new List<Candle>();
        readonly List<Vector3> aucBubbles = new List<Vector3>();   // x = 가격, y = 뜬 시각, z = 이름 번호
        static readonly string[] Bidders = { "케슬러 금융", "달 기지 조합", "화성 제련소", "고철왕 박 씨", "목성 선박", "익명 수집가" };
        static readonly float[] AutoTargets = { 1.5f, 2f, 3f, 5f, 8f };
        const float CandleSec = 0.95f;
        public void TestAuc(int step) { if (step == 0) AucPrep(); else if (step == 1) AucBegin(); else if (step == 2 && aucState == 5) AucSell(); else if (step == 3 && aucState == 5) AucNextCandle(); }   // 에디터 시험용

        // 걸기 전 — 시세가 실제 차트처럼 계속 움직인다 (보기만, 앞날과는 상관없다). [배팅!] 누른 순간 가격에 산다
        readonly List<Candle> aucHist = new List<Candle>();
        float prepT; float prepO, prepC, prepH, prepL;
        void AucPrep()
        {
            aucState = 1; OrbitSfx.Play("tick", 0.7f);
            aucHist.Clear(); aucCandles.Clear(); aucBubbles.Clear();
            float p = 1f; var tmp = new List<Candle>();
            for (int i = 0; i < 10; i++)
            {
                float f = Random.value < 0.52f ? Random.Range(1.02f, 1.12f) : Random.Range(0.9f, 0.98f);
                float o = p / f;
                tmp.Add(new Candle { o = o, c = p, h = Mathf.Max(o, p) * Random.Range(1.01f, 1.05f), l = Mathf.Min(o, p) * Random.Range(0.95f, 0.99f) });
                p = o;
            }
            for (int i = tmp.Count - 1; i >= 0; i--) aucHist.Add(tmp[i]);
            PrepCandle();
        }
        void PrepCandle()
        {
            prepT = 0; prepO = aucHist.Count > 0 ? aucHist[aucHist.Count - 1].c : 1f;
            float f = Random.value < 0.5f ? Random.Range(1.01f, 1.1f) : Random.Range(0.91f, 0.99f);
            prepC = prepO * f; prepH = Mathf.Max(prepO, prepC) * Random.Range(1.005f, 1.04f); prepL = Mathf.Min(prepO, prepC) * Random.Range(0.96f, 0.995f);
        }
        float PrepNow() => Mathf.Lerp(prepO, prepC, Mathf.Clamp01(prepT / CandleSec)) + (1 - Mathf.Clamp01(prepT / CandleSec)) * Mathf.Sin(prepT * 37) * 0.012f * prepO;

        void AucBegin()
        {
            double stake = System.Math.Min(last.Earned, sim.S.cash);
            if (!sim.AuctionStart(stake)) { aucState = 0; return; }
            aucStakeShown = sim.AucStake; endCash = sim.S.cash; gained = 0;
            float now = PrepNow();
            aucHist.Add(new Candle { o = prepO, c = now, h = Mathf.Max(prepO, now, Mathf.Lerp(prepO, prepH, prepT / CandleSec)), l = Mathf.Min(prepO, now, Mathf.Lerp(prepO, prepL, prepT / CandleSec)) });
            float k = (float)sim.AucStartMult / Mathf.Max(0.01f, now);
            for (int i = 0; i < aucHist.Count; i++) { var c = aucHist[i]; aucHist[i] = new Candle { o = c.o * k, c = c.c * k, h = c.h * k, l = c.l * k }; }
            aucCandles.Clear(); aucBubbles.Clear();
            OrbitSfx.Play("launch", 0.4f); game.shake = 0.12f;
            AucNextCandle();
        }
        void AucNextCandle()
        {
            aucOpenP = sim.AucPrice;
            bool crash = sim.AuctionStep();
            aucCloseP = crash ? aucOpenP * 0.08 : sim.AucPrice;
            aucHi = System.Math.Max(aucOpenP, aucCloseP) * (1 + Random.Range(0.01f, 0.06f));
            aucLo = System.Math.Min(aucOpenP, aucCloseP) * (1 - Random.Range(0.01f, 0.05f));
            aucState = 2; aucT = 0; aucNextTick = 0;
            aucCrashNow = crash;
        }
        bool aucCrashNow;
        void AucSell()
        {
            double m = sim.AucPrice;
            aucRecord = m > sim.M.bestAuc + 1e-6;
            aucGot = sim.AuctionSell();
            aucState = 3; aucEndT = 0; endCash = sim.S.cash;
            OrbitSfx.Play("clank", 1f); OrbitSfx.Play("buy", 1f); game.creditPulse = 1; game.shake = 0.25f;
        }
        void AucBust()
        {
            aucGot = sim.AuctionCrash();
            aucState = 4; aucEndT = 0; endCash = sim.S.cash;
            OrbitSfx.Play("crash", 1f); game.shake = 0.45f;
        }

        // 봉 차트 — 지난 봉(흐리게) + 내 봉 · 「매수」 점선 · 오른쪽 가격 눈금. 걸기 전엔 서는 봉이 계속 움직인다
        Rect chG; float chLo, chHi;
        float ChartY(float v) => chG.yMax - 10 - (chG.height - 20) * (Mathf.Log(Mathf.Max(0.05f, v)) - Mathf.Log(chLo)) / (Mathf.Log(chHi) - Mathf.Log(chLo));
        void Chart(Rect g, float grow)
        {
            chG = g;
            GUI.color = new Color(0.02f, 0.025f, 0.04f); GUI.DrawTexture(g, white); GUI.color = Color.white; Frame(g, new Color(0.15f, 0.2f, 0.27f), 1.5f);
            var all = new List<Candle>(aucHist); int histN = all.Count; all.AddRange(aucCandles);
            bool prep = aucState == 1, forming = aucState == 2 || prep;
            float fo = prep ? prepO : (float)aucOpenP, fc = prep ? prepC : (float)aucCloseP, fh = prep ? prepH : (float)aucHi, fl = prep ? prepL : (float)aucLo;
            float cw = 26, gap = 14, x0 = g.x + 12;
            int maxN = Mathf.Max(6, (int)((g.width - 60) / (cw + gap)));
            int shown = all.Count + (forming ? 1 : 0), first = Mathf.Max(0, shown - maxN);
            float lo = float.MaxValue, hi = 0;
            for (int i = first; i < all.Count; i++) { lo = Mathf.Min(lo, all[i].l); hi = Mathf.Max(hi, all[i].h); }
            if (forming) { hi = Mathf.Max(hi, fh); lo = Mathf.Min(lo, fl); }
            if (hi <= 0) { lo = 0.8f; hi = 1.25f; }
            if (hi / lo < 1.3f) { float mid = Mathf.Sqrt(hi * lo); lo = mid / 1.14f; hi = mid * 1.14f; }
            chLo = Mathf.Max(0.03f, lo * 0.94f); chHi = hi * 1.06f;
            foreach (var k in new[] { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f, 5f, 8f, 13f, 20f, 35f, 60f })
            {
                if (k < chLo || k > chHi) continue;
                float yy = ChartY(k);
                GUI.color = new Color(1, 1, 1, 0.06f); GUI.DrawTexture(new Rect(g.x, yy, g.width, 1), white); GUI.color = Color.white;
                GUI.Label(new Rect(g.xMax + 4, yy - 9, 60, 18), "<size=10><color=#5f6878>×" + k + "</color></size>", small);
            }
            void DrawCandle(int slot, float o, float c, float h, float l, float k, float alpha)
            {
                float cx = x0 + slot * (cw + gap) + cw / 2;
                float cNow = Mathf.Lerp(o, c, k), hNow = Mathf.Lerp(Mathf.Max(o, cNow), h, k), lNow = Mathf.Lerp(Mathf.Min(o, cNow), l, k);
                GUI.color = cNow >= o ? new Color(0.35f, 0.85f, 0.5f, alpha) : new Color(0.95f, 0.3f, 0.25f, alpha);
                GUI.DrawTexture(new Rect(cx - 1, ChartY(hNow), 2, Mathf.Max(1, ChartY(lNow) - ChartY(hNow))), white);
                float yTop = ChartY(Mathf.Max(o, cNow)), yBot = ChartY(Mathf.Min(o, cNow));
                GUI.DrawTexture(new Rect(cx - cw / 2, yTop, cw, Mathf.Max(2, yBot - yTop)), white);
                GUI.color = Color.white;
            }
            for (int i = first; i < all.Count; i++) DrawCandle(i - first, all[i].o, all[i].c, all[i].h, all[i].l, 1, (!prep && i < histN) ? 0.4f : 1f);
            float now = fc;
            if (forming)
            {
                float wob = (1 - grow) * Mathf.Sin((prep ? prepT : aucT) * 40) * 0.035f * fo;
                now = Mathf.Lerp(fo, fc, grow) + wob;
                DrawCandle(all.Count - first, fo, now, fh, fl, grow, 1);
            }
            else now = (float)sim.AucPrice;
            // 매수 점선 — 여기서부터 내 돈
            if (!prep && histN - first >= 0)
            {
                float mx = x0 + (histN - first) * (cw + gap) - gap / 2;
                GUI.color = new Color(1f, 0.87f, 0.58f, 0.6f);
                for (float yy = g.y + 4; yy < g.yMax - 4; yy += 10) GUI.DrawTexture(new Rect(mx, yy, 1.5f, 5), white);
                GUI.color = Color.white;
                GUI.Label(new Rect(mx + 4, g.yMax - 22, 60, 18), "<size=11><color=#ffdf95>매수</color></size>", small);
            }
            // 지금 가격 줄 + 오른쪽 꼬리표
            float cy = ChartY(now);
            GUI.color = new Color(1f, 0.87f, 0.58f, 0.45f); GUI.DrawTexture(new Rect(g.x, cy, g.width, 1), white);
            GUI.color = new Color(1f, 0.8f, 0.4f, 0.95f); GUI.DrawTexture(new Rect(g.xMax - 54, cy - 9, 54, 18), white); GUI.color = Color.white;
            GUI.Label(new Rect(g.xMax - 54, cy - 9, 54, 18), "<size=11><b><color=#2a1a05>" + now.ToString("0.00") + "</color></b></size>", center);
        }

        void Auction()
        {
            var S = sim.S; var ev = Event.current;
            bool tick = ev.type == EventType.Repaint;
            float dt = tick ? Time.unscaledDeltaTime : 0;
            double price = sim.AucPrice;
            GUI.color = new Color(0, 0, 0, 0.8f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            if ((aucState == 2 || aucState == 5) && price > 3) { float a = 0.22f * Mathf.Clamp01((float)(price - 3) / 5) * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8)); Frame(new Rect(0, 0, vw, RefH), new Color(0.9f, 0.2f, 0.15f, a), 14); }
            var r = new Rect(vw / 2 - 320, 44, 640, 512);
            GUI.color = new Color(0.035f, 0.04f, 0.06f, 0.98f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.62f, 0.5f, 0.3f), 2);
            title.fontSize = 24; GUI.Label(new Rect(r.x, r.y + 10, r.width, 32), "<color=#ffdf95>고철 경매</color>  <size=13><color=#8a9bb3>케슬러 고철 거래소</color></size>", title); title.fontSize = 18;

            if (aucState == 1)
            {
                double stake = System.Math.Min(last.Earned, S.cash);
                GUI.Label(new Rect(r.x, r.y + 68, r.width, 30), "<size=18>이번 판 수입 <color=#ffdf95>+" + KNum.Fmt(stake) + "</color> 을 건다</size>", center);
                GUI.Label(new Rect(r.x, r.y + 96, r.width, 24), "<size=13><color=#8a9bb3>시세는 계속 움직인다 — 원하는 순간 [배팅!]. 그 뒤로 봉이 설 때마다 팔지 정한다</color></size>", center);
                if (tick) { prepT += dt; if (prepT >= CandleSec) { aucHist.Add(new Candle { o = prepO, c = prepC, h = prepH, l = prepL }); if (aucHist.Count > 30) aucHist.RemoveAt(0); PrepCandle(); OrbitSfx.PlayPitch("tick", 0.25f, 0.7f); } }
                Chart(new Rect(r.x + 50, r.y + 126, r.width - 140, 160), Mathf.Clamp01(prepT / CandleSec));
                string[] info = {
                    "시작 가격  ×" + sim.AucStartMult.ToString("0.00") + (sim.Lv("a_big") > 0 ? "  <color=#6fcf97>(큰손 입찰)</color>" : ""),
                    "폭락 보험  " + (sim.AucInsure > 0 ? "<color=#6fcf97>" + Mathf.RoundToInt((float)sim.AucInsure * 100) + "% 돌려받기</color>" : "<color=#5f6878>없음</color>"),
                    "시세 예측  " + (sim.Lv("a_read") > 0 ? "<color=#6fcf97>폭락 봉 앞에서 「⚠ 수상하다」 (" + new[] { "", "가끔 · 헛경보도", "자주", "늘" }[Mathf.Min(3, sim.Lv("a_read"))] + ")</color>" : "<color=#5f6878>없음</color>"),
                };
                for (int i = 0; i < info.Length; i++) GUI.Label(new Rect(r.x + 150, r.y + 296 + i * 24, r.width - 200, 26), "<size=13>" + info[i] + "</size>", label);
                if (sim.Lv("a_auto") > 0)
                {
                    GUI.Label(new Rect(r.x + 150, r.y + 368, 200, 30), "<size=13>자동 낙찰  <color=#ffdf95>×" + aucTarget.ToString("0.0") + "</color></size>", label);
                    int ti = System.Array.IndexOf(AutoTargets, aucTarget); if (ti < 0) ti = 1;
                    if (GUI.Button(new Rect(r.x + 330, r.y + 368, 34, 26), "◀", btnOff)) aucTarget = AutoTargets[Mathf.Max(0, ti - 1)];
                    if (GUI.Button(new Rect(r.x + 368, r.y + 368, 34, 26), "▶", btnOff)) aucTarget = AutoTargets[Mathf.Min(AutoTargets.Length - 1, ti + 1)];
                }
                var bb = new Rect(r.center.x - 240, r.yMax - 84, 240, 64);
                float bp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6);
                GUI.color = Color.Lerp(new Color(0.55f, 0.12f, 0.1f), new Color(0.8f, 0.2f, 0.15f), bp); GUI.DrawTexture(bb, white); GUI.color = Color.white;
                Frame(bb, new Color(1f, 0.6f, 0.5f), 3);
                GUI.Label(bb, "<size=30><b><color=#fff0e8>배팅!</color></b></size>", center);
                if (GUI.Button(bb, GUIContent.none, GUIStyle.none)) AucBegin();
                if (GUI.Button(new Rect(r.center.x + 20, r.yMax - 84, 200, 64), "<size=16>그만두기</size>", btnOff)) aucState = 0;
                return;
            }

            // ── 봉이 서는 중 — 꿈틀거리며 자란다
            float grow = 1;
            if (aucState == 2)
            {
                if (tick) aucT += dt;
                grow = Mathf.Clamp01(aucT / CandleSec);
                aucNextTick -= dt;
                if (aucNextTick <= 0 && tick) { aucNextTick = 0.11f; OrbitSfx.PlayPitch("tick", 0.45f, 0.8f + 0.25f * Mathf.Log((float)System.Math.Max(1, aucOpenP), 2) + grow * 0.3f); }
                if (aucOpenP > 3 && tick && Mathf.Repeat(aucT, 0.5f) < dt) OrbitSfx.Play("heart", 0.8f, 0.1f, 0);
                if (aucT >= CandleSec)
                {
                    aucCandles.Add(new Candle { o = (float)aucOpenP, c = (float)aucCloseP, h = (float)aucHi, l = (float)aucLo });
                    if (aucCrashNow) AucBust();
                    else
                    {
                        aucState = 5;
                        if (aucCloseP > aucOpenP) { OrbitSfx.Play("grab", 0.6f); if (Random.value < 0.6f) aucBubbles.Add(new Vector3((float)aucCloseP, Time.unscaledTime, Random.Range(0, Bidders.Length))); }
                        else OrbitSfx.PlayPitch("tick", 0.6f, 0.5f);
                        if (sim.Lv("a_auto") > 0 && sim.AucPrice >= aucTarget) AucSell();
                    }
                }
            }
            if ((aucState == 3 || aucState == 4) && tick) aucEndT += dt;

            // ── 차트
            var g = new Rect(r.x + 24, r.y + 52, r.width - 110, 290);
            Chart(g, grow);
            // 입찰 말풍선
            foreach (var b in aucBubbles)
            {
                float age = Time.unscaledTime - b.y; if (age > 2f) continue;
                float bx = g.x + 30 + (b.z * 71 % (g.width - 200)), by = ChartY(b.x) - 34 - age * 12;
                GUI.color = new Color(0.95f, 0.9f, 0.8f, 0.9f * Mathf.Clamp01(2f - age)); GUI.DrawTexture(new Rect(bx, by, 150, 22), white); GUI.color = Color.white;
                GUI.Label(new Rect(bx, by + 1, 150, 20), "<size=11><color=#2a1a05>" + Bidders[(int)b.z] + " ×" + b.x.ToString("0.00") + " 입찰!</color></size>", center);
            }
            // 오른쪽 위 — 지금 가격 크게
            double shownP = aucState == 2 ? Mathf.Lerp((float)aucOpenP, (float)aucCloseP, grow) : price;
            if (aucState == 4) shownP = aucCloseP;
            string pc = aucState == 4 ? "#ff5a4a" : shownP < 1 ? "#ff8a7a" : shownP < 2 ? "#ffdf95" : shownP < 4 ? "#ffb070" : "#ff7a6a";
            float fs = 34 + Mathf.Min(28f, 8f * Mathf.Log((float)System.Math.Max(1, shownP), 2));
            GUI.Label(new Rect(g.x, g.y + 2, g.width, 60), "<size=" + Mathf.RoundToInt(fs) + "><b><color=" + pc + ">×" + shownP.ToString("0.00") + "</color></b></size>", new GUIStyle(center) { alignment = TextAnchor.UpperCenter });
            GUI.Label(new Rect(g.x + 10, g.y + 8, 200, 20), "<size=12><color=#8a9bb3>봉 " + aucCandles.Count + "</color></size>", label);

            // 아래 — 건 돈 · 지금 팔면
            GUI.Label(new Rect(r.x, g.yMax + 8, r.width, 24), "<size=14><color=#8a9bb3>건 돈</color> " + KNum.Fmt(aucStakeShown) + "   <color=#8a9bb3>지금 팔면</color> <color=#ffdf95>+" + KNum.Fmt(System.Math.Floor(aucStakeShown * System.Math.Max(0, shownP))) + "</color>" + (sim.Lv("a_auto") > 0 ? "   <color=#8a9bb3>자동 ×" + aucTarget.ToString("0.0") + "</color>" : "") + "</size>", center);

            if (aucState == 2)
            {
                GUI.Label(new Rect(r.x, r.yMax - 110, r.width, 30), "<size=16><color=#8a9bb3>봉이 서는 중…</color></size>", center);
                return;
            }
            if (aucState == 5)
            {
                GUI.Label(new Rect(r.x, r.yMax - 140, r.width, 30), "<size=22><b>파시겠습니까?</b></size>", center);
                if (sim.AucWarn) GUI.Label(new Rect(g.x, g.y + 34, g.width, 24), Mathf.Repeat(Time.unscaledTime * 3, 1) < 0.7f ? "<size=15><b><color=#ff4a3a>⚠ 다음 봉이 수상하다</color></b></size>" : "", center);
                var sb = new Rect(r.center.x - 250, r.yMax - 110, 240, 70);
                var nb = new Rect(r.center.x + 10, r.yMax - 110, 240, 70);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6);
                GUI.color = Color.Lerp(new Color(0.45f, 0.3f, 0.06f), new Color(0.7f, 0.48f, 0.1f), pulse); GUI.DrawTexture(sb, white); GUI.color = Color.white;
                Frame(sb, SweepGame.Amber2, 3);
                GUI.Label(new Rect(sb.x, sb.y + 6, sb.width, 32), "<size=24><b><color=#fff3d6>팔기</color></b></size>", center);
                GUI.Label(new Rect(sb.x, sb.y + 38, sb.width, 24), "<size=13><color=#fff3d6>+" + KNum.Fmt(System.Math.Floor(aucStakeShown * price)) + "  (×" + price.ToString("0.00") + ")</color></size>", center);
                if (GUI.Button(sb, GUIContent.none, GUIStyle.none)) AucSell();
                GUI.color = new Color(0.1f, 0.14f, 0.2f); GUI.DrawTexture(nb, white); GUI.color = Color.white;
                Frame(nb, new Color(0.4f, 0.55f, 0.75f), 2);
                GUI.Label(new Rect(nb.x, nb.y + 6, nb.width, 32), "<size=22><b><color=#cfe0ff>한 봉 더 ▸</color></b></size>", center);
                GUI.Label(new Rect(nb.x, nb.y + 38, nb.width, 24), "<size=12><color=#8a9bb3>오를까, 떨어질까, 폭락할까</color></size>", center);
                if (GUI.Button(nb, GUIContent.none, GUIStyle.none)) AucNextCandle();
                return;
            }

            // ── 끝 — 낙찰 / 유찰 도장
            float k2 = Mathf.Clamp01(aucEndT / 0.2f), sc = Mathf.Lerp(2.4f, 1f, k2);
            var cc = new Vector2(r.center.x - 40, r.y + 190);
            var m0 = GUI.matrix;
            GUIUtility.RotateAroundPivot(aucState == 3 ? -8 : 10, cc); GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), cc);
            Color stc = aucState == 3 ? SweepGame.Amber : new Color(0.85f, 0.15f, 0.12f);
            GUI.color = new Color(stc.r, stc.g, stc.b, 0.92f * k2); GUI.DrawTexture(new Rect(cc.x - 110, cc.y - 44, 220, 88), white);
            GUI.color = new Color(0.035f, 0.04f, 0.06f, 0.95f * k2); GUI.DrawTexture(new Rect(cc.x - 104, cc.y - 38, 208, 76), white);
            GUI.color = new Color(1, 1, 1, k2);
            GUI.Label(new Rect(cc.x - 110, cc.y - 30, 220, 60), aucState == 3 ? "<size=40><b><color=#ffdf95>낙찰!</color></b></size>" : "<size=40><b><color=#ff5a4a>유찰</color></b></size>", center);
            GUI.matrix = m0; GUI.color = Color.white;
            if (aucState == 3)
            {
                GUI.Label(new Rect(r.x, r.yMax - 150, r.width, 44), "<size=32><b><color=#6fcf97>+" + KNum.Fmt(aucGot) + "</color></b></size>  <size=15><color=#ffdf95>(봉 " + aucCandles.Count + " · ×" + (aucGot / System.Math.Max(1, aucStakeShown)).ToString("0.00") + ")</color></size>", center);
                if (aucRecord && aucEndT > 0.5f) GUI.Label(new Rect(r.x, r.yMax - 108, r.width, 24), "<size=15><b><color=#ff8a7a>신기록!</color></b></size>", center);
                var dst = new Vector2(vw - 125, 36);
                for (int i = 0; i < 26; i++)
                {
                    float st = i * 0.035f, u = Mathf.Clamp01((aucEndT - st) / 0.7f);
                    if (u <= 0 || u >= 1) continue;
                    var from = new Vector2(r.center.x + (i * 37 % 120 - 60), r.yMax - 130);
                    var pos = Vector2.Lerp(from, dst, u * u) + new Vector2(0, -Mathf.Sin(u * Mathf.PI) * 80);
                    GUI.color = SweepGame.Amber2; GUI.DrawTexture(new Rect(pos.x - 6, pos.y - 6, 12, 12), texDisc); GUI.color = Color.white;
                }
            }
            else
            {
                GUI.Label(new Rect(r.x, r.yMax - 150, r.width, 36), "<size=20><color=#ff8a7a>봉 " + aucCandles.Count + " 에서 폭락</color></size>", center);
                GUI.Label(new Rect(r.x, r.yMax - 116, r.width, 24), aucGot > 0 ? "<size=15><color=#6fcf97>경매 보험 +" + KNum.Fmt(aucGot) + " 돌려받음</color></size>" : "<size=15><color=#8a9bb3>건 돈 " + KNum.Fmt(aucStakeShown) + " 을 잃었다</color></size>", center);
            }
            if (aucEndT > 0.9f && GUI.Button(new Rect(r.center.x - 90, r.yMax - 76, 180, 50), "<size=18>확인</size>", btn)) { aucState = 0; aucUsed = true; }
        }

        // ✍ 대출 계약서 — 누르면 바로가 아니라, 계약서를 펼치고 서명란에 직접 그어 서명 → 「승인」 도장 → 돈 (사장님 09-23)
        double pendLoan; bool pendPay; float signT = -1; readonly List<Vector2> signPts = new List<Vector2>(); float signLen; bool signing;
        public void TestSign() { signPts.Clear(); for (int i = 0; i < 30; i++) signPts.Add(new Vector2(vw / 2 - 150 + i * 9, 360 + Mathf.Sin(i * 0.9f) * 18)); signLen = 300; signT = Time.unscaledTime; }   // 에디터 시험용
        public void RequestLoan(double amt, bool payBill)
        {
            if (amt <= 0) return;
            pendLoan = System.Math.Ceiling(amt); pendPay = payBill; signT = -1; signPts.Clear(); signLen = 0; signing = false;
            loanOpen = true; OrbitSfx.Play("tick", 0.6f, 0.05f, 0.02f);
        }
        void Contract()
        {
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 220, 56, 440, 470);
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, r.width, r.height), white); GUI.color = Color.white;
            GUI.DrawTexture(r, texPaper);
            GUI.DrawTexture(new Rect(r.x + 20, r.y + 58, r.width - 40, 2), texInk);
            paperHead.fontSize = 24; GUI.Label(new Rect(r.x, r.y + 16, r.width, 36), "대출 계약서", new GUIStyle(paperHead) { alignment = TextAnchor.UpperCenter });
            paperSmall.fontSize = 11; GUI.Label(new Rect(r.x + 20, r.y + 62, r.width - 40, 18), "채권자 케슬러 금융 · 채무자 주식회사 궤도 청소부 (" + sim.M.company + "대)", paperSmall);
            string[] k = { "빌리는 돈", "갚을 돈", "갚는 법", "쓰는 곳" };
            string[] v = { KNum.Fmt(pendLoan), KNum.Fmt(pendLoan * SweepSim.LoanMult) + "  (" + SweepSim.LoanMult + "배)", "판 수입의 " + (sim.Lv("e_guard") > 0 ? 20 : 30) + "%가 자동으로", pendPay ? "청구서를 바로 갚는다" : "돈으로 들어온다" };
            for (int i = 0; i < 4; i++)
            {
                float y = r.y + 92 + i * 34;
                paperBody.fontSize = 14; GUI.Label(new Rect(r.x + 30, y, 120, 24), k[i], paperBody);
                paperBody.fontSize = i < 2 ? 18 : 14; GUI.Label(new Rect(r.x + 150, y - (i < 2 ? 3 : 0), r.width - 180, 28), i < 2 ? "<b>" + v[i] + "</b>" : v[i], paperBody);
                GUI.color = new Color(0, 0, 0, 0.12f); GUI.DrawTexture(new Rect(r.x + 30, y + 27, r.width - 60, 1), white); GUI.color = Color.white;
            }
            paperBody.fontSize = 14;
            // 서명란 — 마우스로 그어 서명
            var sa = new Rect(r.x + 40, r.y + 250, r.width - 80, 100);
            GUI.color = new Color(0, 0, 0, 0.04f); GUI.DrawTexture(sa, white); GUI.color = Color.white;
            GUI.DrawTexture(new Rect(sa.x, sa.yMax - 22, sa.width, 2), texInk);
            paperSmall.fontSize = 11; GUI.Label(new Rect(sa.x, sa.yMax - 18, 200, 16), "서명", paperSmall);
            var ev = Event.current;
            bool done = signT >= 0;
            if (!done)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && sa.Contains(ev.mousePosition)) { signing = true; signPts.Add(new Vector2(-1, -1)); signPts.Add(ev.mousePosition); ev.Use(); }
                else if (ev.type == EventType.MouseDrag && signing)
                {
                    var q = new Vector2(Mathf.Clamp(ev.mousePosition.x, sa.x, sa.xMax), Mathf.Clamp(ev.mousePosition.y, sa.y, sa.yMax));
                    var last = signPts[signPts.Count - 1];
                    if ((q - last).sqrMagnitude > 4) { signLen += (q - last).magnitude; signPts.Add(q); if (signPts.Count % 6 == 0) OrbitSfx.Play("tick", 0.15f, 0.04f, 0.3f); }
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp && signing)
                {
                    signing = false; ev.Use();
                    if (signLen > 140) { signT = Time.unscaledTime; OrbitSfx.Play("clank", 0.9f); }   // 쾅 — 도장
                }
                if (signPts.Count == 0) GUI.Label(new Rect(sa.x, sa.y + 22, sa.width, 24), "<color=#8a7f6a>여기를 마우스로 그어 서명하세요</color>", paperBody);
            }
            for (int i = 1; i < signPts.Count; i++)
                if (signPts[i - 1].x >= 0 && signPts[i].x >= 0) Line(signPts[i - 1], signPts[i], new Color(0.1f, 0.14f, 0.35f), 2.6f);
            // 도장 — 쾅 찍히고 조금 뒤 돈이 들어온다
            if (done)
            {
                float k2 = Mathf.Clamp01((Time.unscaledTime - signT) / 0.18f);
                float sc = Mathf.Lerp(2.2f, 1f, k2);
                var c = new Vector2(r.xMax - 110, r.y + 300);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(-14, c); GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), c);
                GUI.color = new Color(0.78f, 0.14f, 0.12f, 0.85f * k2);
                GUI.DrawTexture(new Rect(c.x - 52, c.y - 52, 104, 104), texRing);
                GUI.DrawTexture(new Rect(c.x - 44, c.y - 44, 88, 88), texRing);
                GUI.Label(new Rect(c.x - 60, c.y - 20, 120, 40), "<size=26><b><color=#c42420>승인</color></b></size>", center);
                GUI.Label(new Rect(c.x - 60, c.y + 14, 120, 20), "<size=10><color=#c42420>케슬러 금융</color></size>", center);
                GUI.matrix = m; GUI.color = Color.white;
                if (Time.unscaledTime - signT > 0.9f)
                {
                    bool ok = pendPay ? sim.LoanAndPay() : sim.TakeLoan(pendLoan);
                    if (ok) { OrbitSfx.Play("buy", 0.9f); game.creditPulse = 1; }
                    pendLoan = 0; signT = -1;
                    if (pendPay) loanOpen = false;
                }
            }
            else if (GUI.Button(new Rect(r.xMax - 120, r.yMax - 52, 100, 34), "취소", btnOff)) { pendLoan = 0; if (pendPay) loanOpen = false; }
            if (!done) GUI.Label(new Rect(r.x + 20, r.yMax - 50, 260, 30), "<size=11><color=#8a7f6a>서명하면 도장이 찍히고 돈이 들어온다</color></size>", paperBody);
        }

        void LoanWin()
        {
            var S = sim.S;
            if (pendLoan > 0) { Contract(); return; }
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 280, 70, 560, 440);
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.99f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.62f, 0.5f, 0.3f), 2);
            title.fontSize = 22; GUI.Label(new Rect(r.x, r.y + 12, r.width, 30), "<color=#ffdf95>케슬러 금융 — 대출 창구</color>", title);
            GUI.Label(new Rect(r.x, r.y + 44, r.width, 18), "<size=12>받은 돈의 " + SweepSim.LoanMult + "배를 갚는다 · 빚이 있으면 판 수입의 " + Mathf.RoundToInt((float)(sim.Lv("e_guard") > 0 ? 20 : 30)) + "%가 자동으로 빠져나간다</size>", center);
            // 숫자 셋
            float cx = r.x + 24, cw = (r.width - 48) / 3;
            string[] k = { "지금 빚", "대출 한도", "가진 돈" };
            string[] v = { KNum.Fmt(S.debt), KNum.Fmt(sim.LoanCap), KNum.Fmt(S.cash) };
            for (int i = 0; i < 3; i++)
            {
                var cr = new Rect(cx + i * cw + 4, r.y + 72, cw - 8, 60);
                GUI.DrawTexture(cr, texCard);
                GUI.Label(new Rect(cr.x, cr.y + 6, cr.width, 16), "<size=11>" + k[i] + "</size>", center);
                GUI.Label(new Rect(cr.x, cr.y + 24, cr.width, 30), "<size=20><color=" + (i == 0 && S.debt > 0 ? "#ffb3a8" : "#ffdf95") + ">" + v[i] + "</color></size>", center);
            }
            // 받기 · 갚기
            double quarter = System.Math.Min(sim.LoanCap, System.Math.Ceiling(sim.BillAmount * 0.25));
            float by = r.y + 146;
            GUI.enabled = quarter > 0;
            if (GUI.Button(new Rect(cx + 4, by, 160, 36), "<size=12>대출 +" + KNum.Fmt(quarter) + "</size>", btn)) RequestLoan(quarter, false);
            GUI.enabled = sim.LoanCap > 0;
            if (GUI.Button(new Rect(cx + 172, by, 160, 36), "<size=12>한도까지 +" + KNum.Fmt(sim.LoanCap) + "</size>", btn)) RequestLoan(sim.LoanCap, false);
            GUI.enabled = S.debt > 0 && S.cash > 0;
            if (GUI.Button(new Rect(cx + 340, by, 168, 36), "<size=12>빚 갚기 −" + KNum.Fmt(System.Math.Min(S.cash, S.debt)) + "</size>", btn)) sim.RepayDebt();
            GUI.enabled = true;
            GUI.Label(new Rect(cx + 4, by + 38, 500, 20), S.bill >= SweepSim.Bills.Length - 1
                ? "<size=11><color=#ff9b8f>마지막 할부(완납)엔 대출이 안 된다 — 제힘으로 갚거나, 못 갚으면 파산</color></size>"
                : "<size=11>받으면 빚 +" + KNum.Fmt(quarter * SweepSim.LoanMult) + " · 한도 = 지금 청구서 − 남은 원금</size>", small);
            // 내역
            float hy = by + 64;
            GUI.Label(new Rect(cx + 4, hy, 300, 18), "내역", label);
            GUI.DrawTexture(new Rect(cx + 4, hy + 20, r.width - 56, 1), texBar);
            var log = S.loanLog;
            if (log == null || log.Count == 0) GUI.Label(new Rect(cx + 4, hy + 28, 400, 18), "<size=12>아직 없다</size>", small);
            else
                for (int i = 0; i < 8 && i < log.Count; i++)
                {
                    var e = log[log.Count - 1 - i];
                    string what = e.kind == 0 ? "<color=#ffdf95>대출 +" + KNum.Fmt(e.amt) + "</color>  → 빚 +" + KNum.Fmt(e.amt * SweepSim.LoanMult)
                                : e.kind == 1 ? "<color=#6fcf97>판 수입에서 상환 −" + KNum.Fmt(e.amt) + "</color>"
                                : "<color=#6fcf97>직접 상환 −" + KNum.Fmt(e.amt) + "</color>";
                    GUI.Label(new Rect(cx + 4, hy + 26 + i * 20, 90, 18), "<size=11>출동 " + e.run + "</size>", small);
                    GUI.Label(new Rect(cx + 96, hy + 26 + i * 20, 420, 18), "<size=12>" + what + "</size>", label);
                }
            if (GUI.Button(new Rect(r.xMax - 110, r.yMax - 44, 96, 32), "닫기", btn)) loanOpen = false;
        }

        bool CanPayNow => !sim.M.cleanReady && sim.S.bill < SweepSim.Bills.Length && sim.S.cash >= sim.BillAmount;

        /// <summary>🔴 갚을 수 있으면 창 가운데에 크게 — 사장님이 돈 357 을 들고 30짜리 첫 청구서를 안 갚으셨다 (09-23 첫 플레이)</summary>

        /// <summary>귀환 직후 2.6초 — 창 가운데에 크게 (§9-1)</summary>







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
            gtiles.Add(new GTile { stat = -1, j = 1, cell = Vector2Int.zero });          // 가운데 — 청소선
            first["R"] = 0;
            for (int i = 0; i < N.Length; i++)
            {
                var pl = SweepSim.Layout[N[i].id];
                for (int j = 1; j <= SweepSim.Tiles(i); j++)
                {
                    if (j == 1) first[N[i].id] = gtiles.Count;
                    gtiles.Add(new GTile { stat = i, j = j, cell = new Vector2Int(pl.x + pl.dx * (j - 1), pl.y + pl.dy * (j - 1)) });
                }
            }
            for (int k = 1; k < gtiles.Count; k++)
            {
                var t = gtiles[k]; var n = N[t.stat];
                if (t.j > 1) { t.lpar = k - 1; continue; }
                var pl = SweepSim.Layout[n.id];
                t.lpar = pl.par == "R" ? 0 : first[pl.par] + pl.tile - 1;
                foreach (var p in n.par) if (p != pl.par) t.xpar.Add(first[p]);
            }
        }

        int[] gmemo;
        int GTileState(int k)   // 0 안 보임 · 1 실루엣 · 2 다음 칸 · 3 산 것 (한 번 그릴 때 한 번만 계산 — 부모를 거슬러 가는 재귀가 겹치면 기하급수로 느려진다)
        {
            if (gmemo[k] >= 0) return gmemo[k];
            return gmemo[k] = GTileCalc(k);
        }
        int GTileCalc(int k)
        {
            var t = gtiles[k];
            if (t.stat < 0) return 3;                                   // 청소선 — 늘 있다
            if (sim.S.lv[t.stat] >= SweepSim.TileLv(t.stat, t.j)) return 3;
            bool parOwned = t.lpar < 0 || GTileState(t.lpar) == 3;
            foreach (var x in t.xpar) if (GTileState(x) != 3) parOwned = false;
            if (parOwned) return 2;
            if (t.lpar >= 0 && GTileState(t.lpar) == 2) return 1;
            return 0;
        }

        // 🎯 AUTO — 판 화면 오른쪽 아래 단추 · 키보드 A. 켜지면 화면 가운데에 「AUTO...」 (사장님 09-23)
        public bool overAuto;
        void AutoSwitch()
        {
            bool on = game.autoMode;
            // 가운데 — 은은하게, 점이 늘었다 줄었다
            if (on && !sim.R.over)
            {
                int dots = (int)(Time.unscaledTime * 2.2f) % 4;
                float a = 0.22f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f);
                GUI.color = new Color(1f, 0.87f, 0.58f, a);
                GUI.Label(new Rect(vw / 2 - 150, 250, 300, 60), "<size=44><b>AUTO" + new string('.', dots) + "</b></size>", center);
                GUI.color = Color.white;
            }
            // 오른쪽 아래 단추
            var r = new Rect(vw - 142, RefH - 70, 124, 46);
            overAuto = r.Contains(Event.current.mousePosition);
            if (on) { GUI.color = new Color(0.95f, 0.76f, 0.31f, 0.18f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f)); GUI.DrawTexture(new Rect(r.x - 10, r.y - 10, r.width + 20, r.height + 20), texDisc); }
            GUI.color = on ? new Color(0.3f, 0.21f, 0.06f, 0.95f) : new Color(0.05f, 0.05f, 0.08f, 0.9f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, on ? SweepGame.Amber : new Color(0.3f, 0.3f, 0.36f), overAuto ? 3 : 2);
            GUI.Label(new Rect(r.x, r.y + 2, r.width, 28), on ? "<size=20><b><color=#ffdf95>AUTO</color></b></size>" : "<size=20><b><color=#5f6878>AUTO</color></b></size>", center);
            GUI.Label(new Rect(r.x, r.y + 26, r.width, 18), "<size=10><color=" + (on ? "#e8c77e" : "#5f6878") + ">" + (on ? "켜짐" : "꺼짐") + " · A</color></size>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) game.ToggleAuto();
        }

        public static bool CastReq;
        public bool overSkill;
        void SkillSlot(SweepRun R)
        {
            var r = new Rect(vw / 2 - 34, RefH - 92, 68, 68);
            overSkill = r.Contains(Event.current.mousePosition);
            bool ready = R.shots > 0 && !R.holding;
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.9f); GUI.DrawTexture(r, white);
            if (R.shots < R.maxShots && !R.clean)
            {
                float k = Mathf.Clamp01((float)(R.holeCd / sim.HoleCd));
                GUI.color = new Color(0.42f, 0.31f, 0.78f, 0.35f); GUI.DrawTexture(new Rect(r.x, r.yMax - r.height * k, r.width, r.height * k), white);
            }
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

        static readonly string[] PlanetHint = { "", "달 — 궤도가 느리다 · 금고 위성이 많으니 노려 보자", "화성 — 22초마다 모래 폭풍이 온다 · 얼음 껍질은 먼저 깨 두자", "목성 — 중력이 잔해를 안쪽으로 모은다 · 안쪽 가장자리에 블랙홀을", "토성 — 고리가 두 겹 · 가운데 틈은 비어 있다" };
        static Texture2D iconTex;
        void DrawIcon(Rect r, string id)
        {
            if (iconTex == null) iconTex = Resources.Load<Texture2D>("tree_icons");
            int idx = System.Array.IndexOf(SweepSim.IconOrder, id);
            if (iconTex == null || idx < 0) return;
            int cols = 8, rows = Mathf.Max(1, iconTex.height / 96);
            float cw = 1f / cols, ch = 1f / rows;
            GUI.DrawTextureWithTexCoords(r, iconTex, new Rect((idx % cols) * cw, 1f - (idx / cols + 1) * ch, cw, ch));
        }

        static Color VisCol(string id)
        {
            switch (SweepSim.VisBranch(id))
            {
                case "claw": return SweepGame.Amber;
                case "hull": return new Color(0.56f, 0.72f, 0.9f);
                case "drone": return SweepGame.Cyan;
                case "bh": return SweepGame.Violet;
                default: return SweepGame.Green;
            }
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
                string bl = "청구서 · " + SweepSim.Bills[S.bill].t + " <color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color>" + (S.overdue ? "  <color=#ff8a7a>오늘 납부일</color>" : " · " + S.billDue + "판 남음") + (S.debt > 0 ? "  <color=#ffb3a8>빚 " + KNum.Fmt(S.debt) + "</color>" : "") + (S.cash >= sim.BillAmount ? "  <color=#6fcf97>▶ 눌러서 갚기</color>" : "");
                bool loanPay = S.overdue && S.cash < sim.BillAmount && sim.BillAmount - S.cash <= sim.LoanCap;
                if (loanPay) bl += "  <color=#6fcf97>▶ 대출 " + KNum.Fmt(sim.BillAmount - S.cash) + " 받아 갚기</color>";
                if (GUI.Button(new Rect(ox + 240, 8, 500, 30), bl, S.cash >= sim.BillAmount || loanPay ? btn : btnOff)) { if (S.cash >= sim.BillAmount) sim.PayBill(); else if (loanPay) RequestLoan(sim.BillAmount - S.cash, true); }
            }
            int unreadN = sim.Unread;
            if (GUI.Button(new Rect(ox + 780, 8, 166, 30), "궤도일보" + (unreadN > 0 ? "  <color=#ff8a7a>● " + unreadN + "</color>" : ""), btn)) { newsOpen = true; newsSel = -1; }

            var area = new Rect(0, 46, vw, 456);
            int nT = gtiles.Count;
            int[] st = new int[nT];
            Vector2 lo = new Vector2(1e9f, 1e9f), hi = new Vector2(-1e9f, -1e9f);
            for (int k = 0; k < nT; k++)
            {
                if (k == 0) { if (gmemo == null || gmemo.Length != nT) gmemo = new int[nT]; System.Array.Fill(gmemo, -1); }
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
                var t = gtiles[k];
                bool isRoot = t.stat < 0;
                var n = isRoot ? SweepSim.Nodes[0] : SweepSim.Nodes[t.stat];
                Color bcol = isRoot ? SweepGame.Amber : VisCol(n.id);
                bool next = st[k] == 2, owned = st[k] == 3;
                var ns = isRoot ? NodeSt.Max : sim.State(t.stat);
                bool can = next && ns == NodeSt.Can;
                bool diamond = !isRoot && n.max == 1;
                var pc = ToScr(t.cell);
                float grow = isRoot ? 0 : nodePulse[t.stat] * 8 * camZ;
                float sz = (isRoot ? tile * 1.25f : diamond ? tile * 0.92f : tile) + grow;
                var r = new Rect(pc.x - sz / 2, pc.y - sz / 2, sz, sz);
                if (can) { GUI.color = new Color(1f, 0.78f, 0.3f, 0.28f + 0.18f * Mathf.Sin(Time.time * 5 + k)); GUI.DrawTexture(new Rect(r.x - 9 * camZ, r.y - 9 * camZ, r.width + 18 * camZ, r.height + 18 * camZ), texDisc); }
                if (diamond) GUI.matrix = m0 * Matrix4x4.TRS(new Vector3(pc.x, pc.y, 0), Quaternion.Euler(0, 0, 45), Vector3.one) * Matrix4x4.TRS(new Vector3(-pc.x, -pc.y, 0), Quaternion.identity, Vector3.one);
                Color bg = owned ? Color.Lerp(bcol, new Color(0.1f, 0.08f, 0.06f), 0.62f) : next ? new Color(0.09f, 0.09f, 0.1f) : new Color(0.06f, 0.06f, 0.07f);
                GUI.color = bg; GUI.DrawTexture(r, white);
                Color edge = can ? new Color(1f, 0.8f, 0.35f) : owned ? Color.Lerp(bcol, Color.black, 0.25f) : new Color(0.22f, 0.21f, 0.2f);
                Frame(r, edge, can ? 2.5f : 1.5f);
                GUI.matrix = m0;
                // 아이콘 — 칸 안에 글자 없이 (시안에서 구운 tree_icons.png)
                bool lockedTile = !owned && ns == NodeSt.Locked;
                GUI.color = owned ? new Color(1f, 0.96f, 0.86f) : next ? (lockedTile ? new Color(0.3f, 0.31f, 0.34f) : new Color(0.72f, 0.72f, 0.74f)) : new Color(0.17f, 0.17f, 0.19f);
                float isz = sz * 0.62f;
                DrawIcon(new Rect(pc.x - isz / 2, pc.y - isz / 2, isz, isz), isRoot ? "R" : n.id);
                if (lockedTile && next) { GUI.color = new Color(1f, 0.6f, 0.55f); GUI.Label(new Rect(r.xMax - 14, r.y - 4, 18, 18), "<size=11>잠</size>", center); }
                GUI.color = Color.white;
                if (r.Contains(ev.mousePosition)) hover = k;
                if (!isRoot && next && GUI.Button(r, GUIContent.none, GUIStyle.none) && ns == NodeSt.Can)
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
            var t = gtiles[k];
            if (t.stat < 0)
            {
                var r0 = new Rect(at.x + tile / 2 + 14, at.y - 40, 260, 76);
                GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.97f); GUI.DrawTexture(r0, white); GUI.color = Color.white; Frame(r0, new Color(0.6f, 0.5f, 0.35f), 2);
                title.fontSize = 18; GUI.Label(new Rect(r0.x, r0.y + 6, r0.width, 26), "<color=#d9b98a>청소선</color>", title);
                GUI.Label(new Rect(r0.x + 10, r0.y + 38, r0.width - 20, 22), "여기서 다섯 갈래로 뻗는다", center);
                return;
            }
            var n = SweepSim.Nodes[t.stat]; var ns = sim.State(t.stat); int lv = sim.S.lv[t.stat];
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
            else if (ns == NodeSt.Hidden && vis != 2) foot = "<color=#ff9b8f>앞 칸을 먼저 사야 한다</color>";
            else if (ns == NodeSt.Hidden) foot = n.seg > sim.Seg ? "<color=#ff9b8f>청구서 " + (n.seg - 1) + "을 갚으면 열린다</color>" : "<color=#ff9b8f>이어진 다른 칸도 사야 한다</color>";
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
                case "a_open": return l > 0 ? "열림" : "잠김";
                case "a_auto": return l > 0 ? "목표 배수에서 자동" : "없음";
                case "a_read": return new[] { "없음", "경고 60%", "경고 80% · 일찍", "경고 늘 · 더 일찍" }[Mathf.Min(3, l)];
                case "a_ins": return "돌려받기 " + new[] { 0, 15, 25, 35 }[Mathf.Min(3, l)] + "%";
                case "a_big": return "시작 ×" + (1 + 0.07f * l).ToString("0.00");
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
                case "b_n": return "최대 " + (2 + l) + "칸";
                case "c_find": return "판마다 " + l + "번";
                case "s_speed": return (16 - 1.5 * l).ToString("0.#") + "초마다";
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
                case "e_guard": return "상환 " + (l > 0 ? 20 : 30) + "%";
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
