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
    public partial class SweepHud : MonoBehaviour
    {
        public SweepGame game;
        SweepSim sim => game.sim;

        const float RefH = 600f;
        float scale = 1f, vw = 960f, ox;
        public Vector2 CreditScreen = new Vector2(120, 580), TallyScreen = new Vector2(600, 60);
        public bool reduceMotion;
        public bool Blocking => sim != null && (sim.R.over || sim.M.careerOpen || sim.M.won || newsOpen || bayOpen);

        // 결산
        bool showResult, bankruptArmed; public bool newsOpen, manualFire = true;
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

        GUIStyle big, label, dim, small, cost, head, title, btn, btnOff, btnC, btnOffC, bigBtn, pop, paperHead, paperBody, paperSmall, center, chainSt;
        Texture2D texInk, white, texDim, texCard, texCard2, texRed, texAmber, texBar, texPaper, texDisc, texRing, texVignette;

        public void Banner(string s, int kind, float time) { banner = s.Replace("⚠", "!!"); bannerKind = kind; bannerT = time; }
        public void OnNews(string head, bool scoop) { breakingT = 6f; breakingHead = (scoop ? Loc.T("특종 — ") : "") + head; }
        public void OnBillPaid(string text, int billNo) { paidText = text; paidT = 3f; paidBill = billNo; if (billNo == 1) branchFlash[1] = 3f; if (billNo == 2) branchFlash[2] = 3f; }
        public void OnWon() { endStage = 0; newsOpen = false; }

        public void OnRunEnd()
        {
            showResult = true; bankruptArmed = false; last = sim.R; resultT = 2.6f; flow = 1;
            if (last.cut > 0) RepayFx(sim.S.debt + last.cut, sim.S.debt, 1.4f, true);   // 판 끝 자동 상환 — 작은 명세서로
            resultAt = Time.time; endCash = sim.S.cash; gained = last.Earned + last.bonus + last.interest; flyers.Clear(); flyT = 0;
            bannerT = 0; banner = null;          // 판 중 예고가 조종실까지 남지 않게
            sim.M.flags.Remove("hint_seen_now");
            if (!sim.M.flags.Contains("hint_claw")) sim.M.flags.Add("hint_claw");
            if (sim.DronesOn && !sim.M.flags.Contains("hint_drone")) sim.M.flags.Add("hint_drone");
            if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb")) sim.M.flags.Add("hint_bomb");
            if (sim.S.orbit > 0 && !sim.M.flags.Contains("hint_p" + sim.S.orbit)) sim.M.flags.Add("hint_p" + sim.S.orbit);
        }

        double[] runStockSh, runStockPx;
        public void Go()
        {
            if (sim.S.overdue && !sim.M.cleanReady) { dueNag = 1.6f; OrbitSfx.Play("tick", 0.6f, 0.6f, 0.05f); return; }   // 납부일 — 갚기 · 대출 · 파산 중 하나를 먼저
            bayOpen = false; flow = 0; lobby = false; charmKick = 1.2f;
            prevBestChain = sim.M.bestChain; prevBestPack = sim.M.bestPack; runNewsFrom = sim.M.news.Count;
            showResult = false; bankruptArmed = false;
            if (sim.Mk != null) { var ms = sim.Mk.M.st; runStockSh = new double[ms.Count]; runStockPx = new double[ms.Count]; for (int i = 0; i < ms.Count; i++) { runStockSh[i] = ms[i].shares; runStockPx[i] = ms[i].price; } }   // 📈 이번 판 주식 통계용
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
            btnC = new GUIStyle(btn) { alignment = TextAnchor.MiddleCenter }; btnOffC = new GUIStyle(btnOff) { alignment = TextAnchor.MiddleCenter };   // 창 단추 — 글자 가운데
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
            RoomGoTick();                                                      // 🧭 새 방을 사면 그 방으로
            DlgTriggers();                                                     // 💬 대화창 — 떠 있으면 입력을 다 가져간다
            if (DlgUpdate(kb)) return;
            if (lobby && sim.R.over && !sim.M.won)
            {
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { settingsOpen = false; wipeAsk = false; }
                else if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame && !kb.altKey.isPressed) && !settingsOpen && !wipeAsk) LobbyContinue();   // 09-27 Alt+Enter 가 출발로 먹었다
                return;
            }
            if (kb != null && kb.spaceKey.wasPressedThisFrame && sim.R.over && !sim.M.careerOpen && !sim.M.won && !newsOpen && paidT < 2.4f)
            {
                if (loanOpen || lottoOpen) { } else if (flow == 2) Go(); else if (flow >= 3 && flow <= 5) GoFlow(2); else flow = 2;   // Space — 결과 · 정비소 → 조종실, 조종실 → 출동
            }
            if (sim.R.over && paidT < 2.4f) NavKeys(kb);                 // ← → 조종실 양옆 방
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { if (settingsOpen) settingsOpen = false; else if (buyAsk >= 0) buyAsk = -1; else if (lottoOpen) lottoOpen = false; else if (loanOpen) { loanOpen = false; pendLoan = 0; } else if (newsOpen) newsOpen = false; else bayOpen = false; }
        }

        void OnGUI()
        {
            if (sim == null) return;
            Styles();
            scale = Screen.height / RefH; vw = Screen.width / scale; ox = Mathf.Max(0, (vw - 960) / 2);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            if (dlgId != null && (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)) Event.current.Use();   // 대화 중엔 뒤 단추가 눌리지 않게
            Effects();
            if (lobby && sim.R.over && !sim.M.won)
            {   // 🚪 로비 — 켜면 여기부터
                if (flow == 0 || flow == 1) flow = 2;
                GUI.enabled = !settingsOpen; Lobby(); GUI.enabled = true;
                VolumeButton();
                return;
            }
            if (!sim.R.over) { Storm(); WindowEdge(); if (launchT > 0) Launch(); Pops(); Notices(); RunHud(); VolleyGauge(); MyStockChips(); }   // 09-27 출발 연출(날아가는 선체)이 위쪽 돈 · 청구서 줄을 덮던 것 — 먼저 그린다
            if (sim.M.won) Ending();
            else if (sim.M.careerOpen) Hangar();
            else if (sim.R.over)
            {
                if (flow == 0) flow = 2;                                // 켜자마자 · 파산 뒤 = 조종실
                GUI.enabled = !loanOpen && !lottoOpen && !newsOpen && !settingsOpen;             // 모달 뒤 버튼 막음 — 뉴스 닫기가 뒤 전광판에 먹혀 다시 열렸다 (09-24 27번)
                if (flow == 1) FlowResult(); else Strip();                    // 정비고 ← 조종실 → 증권 (좌우로 밀린다)
                GUI.enabled = true;
                if (loanOpen) LoanWin();
                if (lottoOpen) LottoWin();
            }
            if (!sim.M.won) NewsBanner();
            if (newsOpen) News();
            RepayOverlay();
            if (sim.Mk != null) { StockFxOverlay(); if (sim.StockOpen) AnchorBox(); }
            BuyFxDraw();                                               // ✨ 칸 · 부품 · 1면 연출 (어느 화면이든)   // 📈 증권 연출 · 🎙 속보 앵커                                            // 💸 빚 갚기 연출
            if (!sim.M.won && sim.R.over && !(flow == 2 || flow == 4)) Ticker();          // 출동 중엔 계기판 위라 안 그림 — 속보는 앵커가 읽는다          // 조종실엔 궤도일보 모니터가 있다 — 아래 한 줄과 겹친다
            ActCard();
            GuideBar();
            if (sim.R.over && !sim.M.won) RadioBox();                      // 📻 윤 대리
            VolumeButton();
            DlgDraw();                                                         // 💬 맨 위
        }

        SweepRun cardRun; bool cardOk; float cardFlash;
        // 🧭 안내 띠 — 새 방이 생기거나 처음 들어갈 때 한 번 (09-24 사장님 32번 「사면 자연스럽게 이용하게」)
        string guideMsg; float guideT;
        public void Guide(string msg) { guideMsg = msg; guideT = 6f; OrbitSfx.Play("supply", 0.6f); }
        void GuideOnce(string flag, string msg) { if (sim.M.flags.Contains("g:" + flag)) return; sim.M.flags.Add("g:" + flag); Guide(msg); }
        void GuideBar()
        {
            if (guideT <= 0 || guideMsg == null) return;
            guideT -= Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(guideT / 0.5f) * Mathf.Clamp01((6f - guideT) / 0.25f);
            var r = new Rect(vw / 2 - 300, 64, 600, 36);
            GUI.color = new Color(0.06f, 0.08f, 0.05f, 0.94f * a); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.44f, 0.81f, 0.59f, a * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6))), 2);
            GUI.color = new Color(1, 1, 1, a); GUI.Label(r, "<size=14><color=#bff4d0>" + guideMsg + "</color></size>", center);
            GUI.color = Color.white;
        }

        // 🚀 전탄 게이지 — 화면 아래 가운데 (무기 둘부터). 차오르면 빨개지고, 퍼붓는 동안은 빛난다
        public static bool VolleyReq; public bool overVolley;
        // 🚀 전탄 발사 단추 — 계기판 가운데 아래. 차면 빛나며 기다리고, 누르거나 Space (09-26 사장님 「스킬처럼 수동으로」 · 시안 https://claude.ai/artifact/1hpRaMr1i8S6ABeTV4XSEP)
        void VolleyGauge()
        {
            overVolley = false;
            if (!sim.VolleyOn) return;
            var R = sim.R; bool firing = R.volleyT > 0, ready = sim.VolleyReady;
            float k = firing ? (float)(R.volleyT / 1.1) : Mathf.Clamp01((float)R.volley);
            var r = new Rect(vw / 2 - 90, RefH - 36, 180, 30);
            overVolley = r.Contains(Event.current.mousePosition);
            float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8);
            if (ready) { GUI.color = new Color(1f, 0.6f, 0.25f, 0.25f + 0.25f * p); GUI.DrawTexture(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12), texDisc); }
            GUI.color = ready ? new Color(0.95f, 0.45f + 0.15f * p, 0.18f) : firing ? new Color(0.35f, 0.28f, 0.14f) : new Color(0.1f, 0.085f, 0.06f, 0.92f); GUI.DrawTexture(r, white);
            Frame(r, ready ? new Color(1f, 0.87f, 0.58f) : new Color(0.35f, 0.29f, 0.17f), ready ? 2 : 1);
            GUI.color = new Color(0.23f, 0.17f, 0.06f); GUI.DrawTexture(new Rect(r.x + 6, r.yMax - 7, r.width - 12, 4), white);
            GUI.color = firing ? Color.white : ready ? new Color(1f, 0.96f, 0.84f) : SweepGame.Amber; GUI.DrawTexture(new Rect(r.x + 6, r.yMax - 7, (r.width - 12) * k, 4), white);
            GUI.color = Color.white;
            string t = firing ? Loc.T("<color=#ffffff>발사 중!</color>") : ready ? Loc.T("<color=#2a1400>전탄 발사 · Space</color>") : Loc.T("<color=#8a7a5a>전탄 ") + Mathf.FloorToInt(k * 100) + "%</color>";
            GUI.Label(new Rect(r.x, r.y + 2, r.width, 20), "<size=13><b>" + t + "</b></size>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && ready) VolleyReq = true;
        }

    }
}
