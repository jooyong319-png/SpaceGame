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
            bayOpen = false; flow = 0; lobby = false;
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
            if (lobby && sim.R.over && !sim.M.won)
            {
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { settingsOpen = false; wipeAsk = false; }
                else if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && !settingsOpen && !wipeAsk) LobbyContinue();
                return;
            }
            if (kb != null && kb.spaceKey.wasPressedThisFrame && sim.R.over && !sim.M.careerOpen && !sim.M.won && !newsOpen && paidT < 2.4f)
            {
                if (loanOpen || lottoOpen) { } else if (flow == 2) Go(); else if (flow >= 3 && flow <= 5) GoFlow(2); else flow = 2;   // Space — 결과 · 정비소 → 조종실, 조종실 → 출동
            }
            if (sim.R.over && paidT < 2.4f) NavKeys(kb);                 // ← → 조종실 양옆 방
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { if (settingsOpen) settingsOpen = false; else if (lottoOpen) lottoOpen = false; else if (loanOpen) { loanOpen = false; pendLoan = 0; } else if (newsOpen) newsOpen = false; else bayOpen = false; }
        }

        void OnGUI()
        {
            if (sim == null) return;
            Styles();
            scale = Screen.height / RefH; vw = Screen.width / scale; ox = Mathf.Max(0, (vw - 960) / 2);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            Effects();
            if (lobby && sim.R.over && !sim.M.won)
            {   // 🚪 로비 — 켜면 여기부터
                if (flow == 0 || flow == 1) flow = 2;
                GUI.enabled = !settingsOpen; Lobby(); GUI.enabled = true;
                VolumeButton();
                return;
            }
            if (!sim.R.over) { Storm(); WindowEdge(); Pops(); RunHud(); VolleyGauge(); MyStockChips(); if (launchT > 0) Launch(); }
            if (sim.M.won) Ending();
            else if (sim.M.careerOpen) Career();
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
        void VolleyGauge()
        {
            if (!sim.VolleyOn) return;
            var R = sim.R; float k = R.volleyT > 0 ? 1 : Mathf.Clamp01((float)R.volley);
            var r = new Rect(vw / 2 - 110, RefH - 16, 220, 7);
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.9f); GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), white);
            Color c = R.volleyT > 0 ? Color.Lerp(new Color(1f, 0.9f, 0.6f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 30)) : k > 0.8f ? new Color(1f, 0.36f, 0.3f) : SweepGame.Amber;
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * k, r.height), white);
            GUI.color = new Color(1, 1, 1, 0.8f); GUI.Label(new Rect(r.x - 60, r.y - 6, 56, 18), "<size=10><color=#8a93a3>전탄</color></size>", cost);
            GUI.color = Color.white;
        }

        // ⚙ 설정 — 늘 오른쪽 위 (09-24 사장님 18번 「소리 설정 버튼 · 항상 오른쪽 위」). 시안 https://claude.ai/artifact/CthkM2c8KDnFcaxG8m5zJt
        public bool settingsOpen;
        float vol = -1, sfxVol = 1; int shakeLv, flashLv;
        static readonly float[] ShakeLvMul = { 1f, 0.4f, 0f }, FlashLvMul = { 1f, 0.35f };
        void LoadSettings()
        {
            vol = PlayerPrefs.GetFloat("orbit.vol", 0.7f); sfxVol = PlayerPrefs.GetFloat("orbit.sfx", 1f);
            shakeLv = PlayerPrefs.GetInt("orbit.shake", 0); flashLv = PlayerPrefs.GetInt("orbit.flash", 0);
            ApplySettings();
        }
        void ApplySettings()
        {
            AudioListener.volume = vol; OrbitSfx.SfxVol = sfxVol;
            SweepGame.ShakeMul = ShakeLvMul[Mathf.Clamp(shakeLv, 0, 2)]; SweepGame.FlashMul = FlashLvMul[Mathf.Clamp(flashLv, 0, 1)];
            reduceMotion = shakeLv == 2;
        }
        void SaveSettings()
        {
            PlayerPrefs.SetFloat("orbit.vol", vol); PlayerPrefs.SetFloat("orbit.sfx", sfxVol); PlayerPrefs.SetFloat("orbit.bgm", OrbitMusic.Vol);
            PlayerPrefs.SetInt("orbit.shake", shakeLv); PlayerPrefs.SetInt("orbit.flash", flashLv); PlayerPrefs.Save();
            ApplySettings();
        }
        void VolumeButton()                                                     // 이름은 그대로 — 이제 ⚙ 설정 단추
        {
            if (vol < 0) LoadSettings();
            var r = new Rect(vw - 58, 8, 50, 22);
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = settingsOpen || ov ? new Color(0.24f, 0.19f, 0.08f, 0.95f) : new Color(0.08f, 0.1f, 0.13f, 0.85f); GUI.DrawTexture(r, white);
            Frame(r, settingsOpen ? SweepGame.Amber : new Color(0.3f, 0.34f, 0.42f), 1); GUI.color = Color.white;
            GUI.Label(r, "<size=12><color=" + (settingsOpen ? "#ffdf95" : "#c8d0dc") + ">설정</color></size>", center);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { settingsOpen = !settingsOpen; OrbitSfx.Play("tick", 0.8f); }
            if (settingsOpen) SettingsWin();
        }
        void SettingsWin()
        {
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            var w = new Rect(vw / 2 - 250, 100, 500, 370);
            GUI.color = new Color(0.043f, 0.063f, 0.09f, 0.98f); GUI.DrawTexture(w, white); Frame(w, SweepGame.Amber, 2); GUI.color = Color.white;
            GUI.Label(new Rect(w.x + 22, w.y + 14, 200, 30), "<size=20><b><color=#ffdf95>설정</color></b></size>", label);
            if (GUI.Button(new Rect(w.xMax - 104, w.y + 14, 88, 26), "<size=12>닫기 Esc</size>", btn)) settingsOpen = false;
            float y = w.y + 62;
            bool changed = false;
            void Slider(string name, ref float v)
            {
                GUI.Label(new Rect(w.x + 24, y, 140, 24), "<size=14>" + name + "</size>", label);
                var sr = new Rect(w.x + 170, y + 9, 230, 8);
                GUI.DrawTexture(sr, texBar); GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(sr.x, sr.y, sr.width * v, sr.height), white);
                GUI.color = new Color(1f, 0.87f, 0.58f); GUI.DrawTexture(new Rect(sr.x + sr.width * v - 6, sr.y - 4, 12, 16), white); GUI.color = Color.white;
                GUI.Label(new Rect(sr.xMax + 10, y, 60, 24), "<size=13>" + Mathf.RoundToInt(v * 100) + "%</size>", label);
                var hit = new Rect(sr.x - 8, y, sr.width + 16, 26); var e = Event.current;
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && hit.Contains(e.mousePosition)) { v = Mathf.Clamp01((e.mousePosition.x - sr.x) / sr.width); v = Mathf.Round(v * 20) / 20f; changed = true; e.Use(); }
                y += 40;
            }
            int Pick(string name, int cur, string[] opts)
            {
                GUI.Label(new Rect(w.x + 24, y, 140, 24), "<size=14>" + name + "</size>", label);
                for (int k = 0; k < opts.Length; k++)
                    if (GUI.Button(new Rect(w.x + 170 + k * 84, y, 80, 26), "<size=12>" + (k == cur ? "<color=#ffdf95>" + opts[k] + "</color>" : opts[k]) + "</size>", k == cur ? btn : btnOff)) { cur = k; changed = true; OrbitSfx.Play("tick", 0.6f); }
                y += 40; return cur;
            }
            Slider("전체 소리", ref vol);
            Slider("효과음", ref sfxVol);
            Slider("배경음", ref OrbitMusic.Vol);
            shakeLv = Pick("화면 흔들림", shakeLv, new[] { "켬", "줄임", "끔" });
            flashLv = Pick("번쩍임", flashLv, new[] { "켬", "줄임" });
            int fs = Screen.fullScreenMode == FullScreenMode.Windowed ? 1 : 0;
            int nf = Pick("화면", fs, new[] { "전체 화면", "창" });
            if (nf != fs && !Application.isEditor)
            {
                if (nf == 0) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                else Screen.SetResolution(Mathf.RoundToInt(Display.main.systemWidth * 0.75f), Mathf.RoundToInt(Display.main.systemHeight * 0.75f), FullScreenMode.Windowed);
            }
            GUI.Label(new Rect(w.x + 24, w.yMax - 30, w.width - 48, 20), "<size=11><color=#8a93a3>저장은 자동 · M = 소리 끄기 · Esc = 닫기</color></size>", label);
            if (!lobby && sim.R.over && GUI.Button(new Rect(w.xMax - 134, w.yMax - 36, 118, 26), "<size=12>로비로 나가기</size>", btnOff)) { game.Save(); settingsOpen = false; lobby = true; lobbyT = 0; }
            if (changed) SaveSettings();
        }

        // ───────────────────────────────── 연출 (도파민 사다리 §5)

        int chainHund, chainTierSeen; float chainPopT;
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
                int ms = 0; for (long m = 200; m <= R.chain; m = m.ToString()[0] == '2' ? m * 5 / 2 : m * 2) ms++;   // 1-2-5 눈금
                if (ms > chainHund) { chainHund = ms; chainPopT = 1.5f; }
                if (tier > chainTierSeen) { chainTierSeen = tier; chainPopT = Mathf.Max(chainPopT, 1.2f); }
                chainPopT -= Time.unscaledDeltaTime;
                if (chainPopT > 0)
                {   // 가운데 큰 글자 — 단계가 오를 때 · 고비를 넘을 때만 (09-24 사장님 9번 · 시안: 평소엔 오른쪽 위 계기)
                    chainSt.fontSize = new[] { 0, 28, 40, 54, 72 }[tier];
                    chainSt.normal.textColor = tier >= 3 ? new Color(1f, 0.96f, 0.84f, Mathf.Min(1, chainPopT * 2)) : new Color(1f, 0.87f, 0.58f, Mathf.Min(1, chainPopT * 2));
                    GUI.Label(new Rect(vw / 2 - 300, 70, 600, 80), "연쇄 ×" + R.chain, chainSt);
                }
                var cg = new Rect(vw - 150, 38, 138, 50);
                GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.85f * Mathf.Min(1, (float)R.chainT * 3)); GUI.DrawTexture(cg, white);
                Frame(cg, new Color(1f, 0.76f, 0.35f, (0.25f + 0.5f * Mathf.Clamp01(chainPopT)) * Mathf.Min(1, (float)R.chainT * 3)), 1);
                GUI.color = new Color(1, 1, 1, Mathf.Min(1, (float)R.chainT * 3));
                GUI.Label(new Rect(cg.x + 8, cg.y + 4, 60, 18), "<size=11><color=#8a93a3>연쇄</color></size>", label);
                GUI.Label(new Rect(cg.x + 8, cg.y + 1, cg.width - 16, 26), "<size=" + (17 + tier) + "><b><color=#ffdf95>" + R.chain + "</color></b></size>", cost);
                GUI.Label(new Rect(cg.x + 8, cg.y + 31, cg.width - 16, 18), "<size=10><color=#8a93a3>최고 " + Mathf.Max(R.chainBest, sim.M.bestChain) + "</color></size>", cost);
                GUI.color = Color.white;
            }
            else { chainHund = 0; chainTierSeen = 0; }
            if (game.kessT > 0)
            {
                chainSt.fontSize = game.kessText == "케슬러!" ? 30 : 44; chainSt.normal.textColor = new Color(1f, 0.6f, 0.3f, Mathf.Min(1, game.kessT * 1.5f));
                GUI.Label(new Rect(vw / 2 - 300, 156, 600, 60), game.kessText, chainSt);   // 큰 연쇄 글자(70~150) 아래로
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
            // 옅은 붉은 먼지 + 가장자리 짙게 + 빠르게 스치는 모래 줄기 (도트 줄) — 전체 덮기(0.42)는 너무 탁했다
            GUI.color = new Color(0.72f, 0.36f, 0.2f, 0.14f * a); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            GUI.color = new Color(0.75f, 0.35f, 0.15f, 0.55f * a); GUI.DrawTexture(new Rect(0, 0, vw, RefH), texVignette);
            for (int i = 0; i < 70; i++)
            {
                float sp = 500 + (i * 53 % 7) * 90, y = (i * 97 % 540) + 30, x = Mathf.Repeat(Time.time * sp + i * 211, vw + 300) - 150, len = 30 + (i * 31 % 5) * 22;
                GUI.color = new Color(1f, 0.72f - (i % 3) * 0.08f, 0.45f, (0.25f + (i % 4) * 0.1f) * a); GUI.DrawTexture(new Rect(x, y, len, i % 5 == 0 ? 3 : 2), white);
            }
            GUI.color = Color.white;
            if (ph > 14 && ph < 19.5f) GUI.Label(new Rect(0, 40, vw, 26), "<size=18><color=#ffb080><b>모래 폭풍</b></color></size>  <size=13><color=#ffd0b0>값 ×1.5 · 잔해가 몰려온다</color></size>", center);
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
            // 💰 이번 판 계산대 — 계기판 위, 포구 오른쪽. 금화가 여기로 날아와 한 숫자로 (시안 DbsvFEEy1K5ddbZsM61B2y)
            {
                var tr = new Rect(vw - 312, RefH - 50, 170, 38);                      // 🟩 계기판 속 액정 (09-24 10 · 28번)
                TallyScreen = new Vector2(tr.center.x * scale, Screen.height - tr.center.y * scale);
                float pu = game.tallyPulse;
                Lcd(tr, pu);
                GUI.Label(new Rect(tr.x + 8, tr.y + 2, tr.width, 14), "<size=10><color=#5fa37d>이번 판</color></size>", label);
                GUI.Label(new Rect(tr.x, tr.y + 14, tr.width - 8, 24), "<size=" + (15 + Mathf.RoundToInt(pu * 3)) + "><b><color=#ffc35a>" + (game.runTally > 0 ? "+" + KNum.Fmt(game.runTally) : "—") + "</color></b></size>", cost);
            }
            GUI.Label(new Rect(x, 14, 40, 20), "연료", dim);
            GUI.DrawTexture(new Rect(x + 34, 19, 160, 9), texBar);
            float fk = Mathf.Clamp01((float)(R.fuel / R.max));
            GUI.DrawTexture(new Rect(x + 34, 19, 160 * fk, 9), R.fuel < 6 ? texRed : texAmber);
            x += 210;
            if (false && R.maxShots > 0 && sim.HoleChance > 0)                     // 위 줄 덜기 — 블랙홀 %는 칸 툴팁으로 (09-24 9번)
            {
                GUI.Label(new Rect(x, 14, 200, 20), "블랙홀 <color=#b69cff>자동 " + (sim.HoleChance * 100).ToString("0.#") + "%</color>" + (R.holding ? "  <color=#b69cff>● 열림</color>" : ""), dim);
            }
            GUI.Label(new Rect(vw - 264, 14, 196, 20), "<color=#8a93a3>" + SweepSim.Orbits[S.orbit].name + "</color>", cost);   // 회사 이름은 조종실에만 — 행성 이름만 작게
            if (R.holding)
            {
                float k = Mathf.Clamp01((float)R.packed.Count / Mathf.Max(1, sim.Cap));
                GUI.DrawTexture(new Rect(vw - 200, 90, 186, 8), texBar);
                GUI.color = k > 0.8f ? SweepGame.Red : SweepGame.Violet; GUI.DrawTexture(new Rect(vw - 200, 90, 186 * k, 8), white); GUI.color = Color.white;
                GUI.Label(new Rect(vw - 330, 100, 316, 18), "압축 " + R.packed.Count + " / 붕괴 " + sim.Cap, cost);
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
                GUI.Label(new Rect(cr.x + 8, cr.y + 3, cr.width - 16, 18), "<size=" + (12 + Mathf.RoundToInt(3 * big2)) + "><color=#ffdf95><b>의뢰</b></color>  " + c.Value.text + "</size>", label);
                GUI.Label(new Rect(cr.x + 8, cr.y + 3, cr.width - 16, 18), "<size=11>" + (ok ? "<color=#6fcf97><b>성공! 판 수입 +" + pct + "%</b></color>" : "<color=#8a93a3>성공하면 +" + pct + "%</color>") + "</size>", cost);
                var pb = new Rect(cr.x + 62, cr.yMax - 14, cr.width - 70, 6);
                GUI.DrawTexture(pb, texBar); GUI.color = ok ? new Color(0.44f, 0.81f, 0.59f) : SweepGame.Amber;
                GUI.DrawTexture(new Rect(pb.x, pb.y, pb.width * Mathf.Clamp01((float)pr / Mathf.Max(1, c.Value.target)), pb.height), white); GUI.color = Color.white;
                GUI.Label(new Rect(cr.x + 8, pb.y - 9, 52, 16), "<size=10><color=#c8d0dc>" + Mathf.Min(pr, c.Value.target) + " / " + c.Value.target + "</color></size>", label);
            }
            // 첫 5분 — 새 장난감마다 한 줄씩만 (§10)
            string hint = null;
            if (R.clean) hint = null; else
            if (!sim.M.flags.Contains("hint_claw") && R.t < 12) hint = "궤도 위에 커서를 대면 청소선이 빔을 쏜다 — 처음엔 한 점씩";
            else if (sim.BombsOn && !sim.M.flags.Contains("hint_bomb") && R.t < 14) hint = "블랙홀이 열렸다 — 집게로 칠 때 가끔 저절로 열려 빨아들인다";
            else if (sim.DronesOn && !sim.M.flags.Contains("hint_drone") && R.t < 8) hint = "드론은 알아서 줍는다 — 한 방에 부서지는 것만";
            else if (sim.S.orbit > 0 && !sim.M.flags.Contains("hint_p" + sim.S.orbit) && R.t < 8) hint = PlanetHint[sim.S.orbit];   // 새 행성 첫 판
            if (hint != null) GUI.Label(new Rect(vw / 2 - 360, RefH - 70, 720, 20), hint, center);
            if (game.timeScale > 1) GUI.Label(new Rect(vw - 120, RefH - 46, 106, 18), "시험 속도 ×3", cost);
        }

        // ───────────────────────────────── 조종실 — 첫 화면 (사장님 09-23: "첫 화면 자체를 우주선 화면 컨셉으로 · 유저 친화적으로")
        // 가운데 창 = 지금 내 궤도 (사면 바로 창밖에 보인다) · 계기판마다 할 일 하나 · 강화는 정비고(네 칸 · 36칸)

        public bool bayOpen; int bayTab;
        public bool CockpitView => (flow >= 2 && flow <= 5) && sim != null && sim.R.over && !sim.M.careerOpen && !sim.M.won;   // 조종실 — 카메라가 물러나 지구가 창 가운데 (09-23 부활)
        public int flow;                         // 0 출동 중 · 1 결산 · 2 조종실 · 3 정비고(왼쪽 끝) · 5 부품 가게(정비고 오른쪽) · 4 증권(오른쪽 방)
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
            var money = new Rect(vw - 300, 14, 210, 44);                        // 오른쪽 끝은 소리 버튼 자리
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
            // 칸 폭을 글자에 맞춘다 — 다섯 자리면 70px 고정 칸을 넘었다 (09-24 사장님 사진 2)
            string CntTxt(int c, bool shortF) => shortF && c >= 10000 ? (c / 10000f).ToString("0.#") + "만" : c.ToString();
            float need = 0; for (int i = 0; i < counts.Length; i++) if (counts[i] > 0) need += 34 + label.CalcSize(new GUIContent(counts[i].ToString())).x;
            bool shortC = need > L.width - 40;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0) continue;
                string ct = CntTxt(Mathf.RoundToInt(counts[i] * tally), shortC);
                float tw = label.CalcSize(new GUIContent(CntTxt(counts[i], shortC))).x;
                GUI.color = SweepGame.JunkColor(kinds[i]); GUI.DrawTexture(new Rect(ix, y + 4, 16, 16), kinds[i] == SweepSim.Chip || kinds[i] == SweepSim.Rocket ? white : texDisc); GUI.color = Color.white;
                GUI.Label(new Rect(ix + 20, y + 2, tw + 4, 20), ct, label);
                ix += 34 + tw;
            }
            y += 32;
            Line2("최대 연쇄", R.chainBest + (R.chainBest > prevBestChain && R.chainBest >= 10 ? " <color=#ff8a7a>새 기록!</color>" : ""));
            y += 6;
            GUI.color = new Color(0.16f, 0.2f, 0.27f); GUI.DrawTexture(new Rect(L.x + 18, y, L.width - 36, 1.5f), white); GUI.color = Color.white;
            y += 12;
            var totalPos = new Vector2(L.xMax - 60, y + 14);
            GUI.Label(new Rect(L.x + 18, y, L.width - 36, 32), "<size=24>합계</size>", label);
            GUI.Label(new Rect(L.x + 18, y, L.width - 36, 32), "<size=26><color=#6fcf97>+" + KNum.Fmt(gained * tally) + "</color></size>", cost);
            if (sim.Mk != null && runStockSh != null && runStockSh.Length == sim.Mk.M.st.Count)
            {   // 📈 내 주식 이번 판 — 출발 때 들고 있던 주식이 얼마나 움직였나 (09-24 사장님 4번)
                double v0 = 0, v1 = 0; for (int i = 0; i < runStockSh.Length; i++) { v0 += runStockSh[i] * runStockPx[i]; v1 += runStockSh[i] * sim.Mk.M.st[i].price; }
                if (v0 > 0)
                {
                    double d = v1 - v0; string hx = d >= 0 ? "#ff5c5c" : "#5494ff";
                    GUI.Label(new Rect(L.x + 18, y + 42, L.width - 36, 24), "<size=16>내 주식 이번 판</size>", label);
                    GUI.Label(new Rect(L.x + 18, y + 42, L.width - 36, 24), "<size=16><color=" + hx + ">" + (d >= 0 ? "+" : "") + KNum.Fmt(d) + "  (" + (d >= 0 ? "+" : "") + (d / v0 * 100).ToString("0.0") + "%)</color></size>", cost);
                }
            }

            // 합계 → 돈 칸으로 날아가는 「+」
            if (tally < 1)
            {
                flyT -= Time.deltaTime;
                if (flyT <= 0 && gained > 0) { flyT = 0.1f; flyers.Add(new Flyer { a = totalPos + new Vector2(Random.Range(-20f, 20f), 0), b = new Vector2(money.x + 40, money.center.y), txt = "+" + KNum.Fmt(System.Math.Max(1, System.Math.Round(gained / 30))) }); OrbitSfx.Play("coin", 0.25f, 0.04f, 0.15f); }
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i]; f.t += Time.deltaTime * 1.6f;
                if (f.t >= 1) { flyers.RemoveAt(i); game.creditPulse = 1; continue; }
                float e = f.t * f.t * (3 - 2 * f.t);
                var p = Vector2.Lerp(f.a, f.b, e) + new Vector2(0, -Mathf.Sin(e * Mathf.PI) * 60);
                pop.fontSize = 13; pop.normal.textColor = new Color(1f, 0.93f, 0.7f, 0.85f * (1 - f.t * 0.7f));
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

            // 오른쪽 아래 — 다음 해금 (청구서를 갚으면 열리는 것)
            var RB = new Rect(cx + 10, 270, 410, 120);
            // 연체가 이어져 사실상 못 갚는 벽 — 다음 해금 대신 파산 안내 (설계상 첫 파산 자리. 구석 단추만으로는 모른다)
            bool stuck = sim.CanBankrupt && S.overdue && S.cash + sim.LoanCap < sim.BillAmount;   // 대출 한도로도 모자라다
            if (stuck)
            {
                Panel2(RB, "<color=#ff9b8f>대출 한도로도 못 갚는다</color>");
                GUI.Label(new Rect(RB.x + 16, RB.y + 30, RB.width - 32, 20), "<size=13>파산하면 빚이 사라지고 <color=#ffdf95>신용 +" + S.creditPending + "</color> · <color=#d8ccff>열쇠 +" + sim.BankruptKeys + "</color></size>", label);
                GUI.Label(new Rect(RB.x + 16, RB.y + 50, RB.width - 32, 20), "<size=13>신용으로 경력을 사면 다음 회사는 처음부터 더 세다</size>", label);
                var bb = new Rect(RB.x + 16, RB.y + 76, RB.width - 32, 34);
                if (GUI.Button(bb, bankruptArmed ? "<color=#ffb3a8>정말? 한 번 더 누르면 파산</color>" : "<color=#ffb3a8>파산하고 새 회사로 ▸</color>", btn))
                {
                    if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; flow = 2; } else bankruptArmed = true;
                }
            }
            else Panel2(RB, sim.S.bill < SweepSim.Bills.Length ? "다음 청구서" : null);
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
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Repay();
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
            if (GUI.Button(new Rect(ox + 700, y, 246, 62), "조종실로 ▸", bigBtn)) GoFlow(2);
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

            bool due = S.overdue && !M.cleanReady;
            // ① 청구서 — 조종대 위 홀로그램 (09-24 C안)
            HoloBill(due);

            // ② 출동 보고 — 초록 브라운관 · ③ 궤도일보 — LED 전광판 · ④ 회사 명판 — 황동판 (09-24 사장님 BBA)
            CrtReport(new Rect(ox + 12, 34, 158, 120));
            LedNews(new Rect(ox + 769, 36, 176, 150));
            BrassPlate(new Rect(ox + 769, 192, 176, 52));
            MyStockBoard();                                             // 📈 내 주식 시세판
            FrontPick();                                                // ★ 1면 조작

            // ⑤ ‹ 정비고로 · 증권 하러 가기 › — 화면 양옆 탭 (누르면 옆 방으로 슥)
            int canN = 0; for (int b = 0; b < 4; b++) canN += CanCount(b);
            if (sim.ShopOpen) { if (NavTab(false, "부품 가게", canN > 0 ? "<color=#f2c14e>정비 " + canN + "칸</color>" : "", SweepGame.Amber)) GoFlow(5); }   // 한 칸씩 — 정비고 ‹ 가게 ‹ 조종실 › 증권 (09-25 사장님: 곧장 정비고로 가니 이상하다)
            else if (NavTab(false, "정비고로", canN > 0 ? "<color=#f2c14e>살 칸 " + canN + "</color>" : "", SweepGame.Amber)) GoFlow(3);
            if (NavTab(true, "증권 하러 가기", sim.StockOpen ? "" : "<color=#5f6878>잠김</color>", new Color(0.62f, 0.94f, 0.75f))) GoFlow(4);

            // ⑥ 항로 — 조종대 오른쪽 홀로그램 (행성 다섯 · 의뢰)
            RouteHolo();

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

            // 🧯 파산 단추 — 출동 단추 왼쪽 위, 유리 덮개 속 (09-24 사장님)
            BankruptGlass(new Rect(ox + 480 - 75 - 96, 404, 70, 78));
            // 🎟 복권 — 분홍 홀로그램 (출동 버튼 오른쪽)
            LottoHolo();
            // 아래 한 줄
            string tip = due ? "<color=" + (dueNag > 0 ? "#ff9b8f" : "#b8a89a") + ">납부일 — 왼쪽 홀로그램 청구서: 납부 · 대출 · 또는 파산</color>" : "Space = 출동 · 창밖 = 지금 내 궤도 · 궤도 넓히기 " + Mathf.RoundToInt((float)(sim.Widen - 1) * 100) + "% · 한 판 " + Mathf.RoundToInt((float)sim.FuelMax) + "초";
            GUI.Label(new Rect(ox + 200, 570 - 3, 560, 26), "<size=12>" + tip + "</size>", center);
        }

        // ───────────────────────────────── 대출 창구 (모달) — 내역 · 받기 · 갚기
        // 🎟 복권 창 — [즉석 복권] 은박을 마우스로 긁는다 · [궤도 로또] 번호 셋을 고른다 (사장님 09-23 「복권 두 가지 다」)
        public bool lottoOpen; int lottoTab, lottoSeen = -1; float lottoToast;
        int[] scGrid; int scWin = -1; bool[,] scCoat; bool scDone; float scDoneT; double scGot;
        readonly List<int> lottoPick = new List<int>();
        const int ScCols = 6, ScRows = 4;                                  // 칸마다 은박 조각
        public void TestLotto(int step)   // 에디터 시험용
        {
            if (step == 0) { lottoOpen = true; lottoTab = 0; scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[9, ScCols * ScRows]; scDone = false; }
            else if (step == 1) { for (int c = 0; c < 6; c++) for (int b = 0; b < ScCols * ScRows; b++) if ((b + c) % 3 != 0 || c < 4) scCoat[c, b] = true; }
            else if (step == 2) { lottoTab = 1; lottoPick.Clear(); lottoPick.AddRange(new[] { 3, 7, 11 }); sim.LottoBuy(3, 7, 11); lottoPick.Clear(); lottoPick.AddRange(new[] { 2, 5 }); }
        }
        void LottoToast()
        {
            var S = sim.S;
            if (lottoSeen < 0) lottoSeen = S.lottoLast != null ? S.lottoLastRound : 0;                         // 켜자마자 옛 결과는 건너뛴다
            if (S.lottoLast != null && S.lottoLastRound > lottoSeen) { lottoToast = 7f; OrbitSfx.Play(S.lottoLastWin > 0 ? "buy" : "tick", 0.8f); lottoSeen = S.lottoLastRound; }
            if (lottoToast <= 0) return;
            lottoToast -= Time.unscaledDeltaTime;
            var tr = new Rect(vw / 2 - 250, RefH - 92, 500, 60);            // 결과 화면 단추 아래
            GUI.color = new Color(0.25f, 0.08f, 0.3f, 0.95f * Mathf.Clamp01(lottoToast)); GUI.DrawTexture(tr, white); GUI.color = new Color(1, 1, 1, Mathf.Clamp01(lottoToast));
            Frame(tr, SweepGame.Mag, 2);
            GUI.Label(new Rect(tr.x, tr.y + 5, tr.width, 24), "<size=16><b>궤도 로또 " + S.lottoLastRound + "회 당첨 번호  " + S.lottoLast[0] + " · " + S.lottoLast[1] + " · " + S.lottoLast[2] + "</b></size>", center);
            GUI.Label(new Rect(tr.x, tr.y + 31, tr.width, 22), "<size=14>" + (S.lottoLastWin > 0 ? "<color=#6fcf97>" + S.lottoLastHit + "개 맞음 · +" + KNum.Fmt(S.lottoLastWin) + "</color>" : "<color=#ee7766>꽝 — 하나도 못 맞혔다</color>") + "</size>", center);
            GUI.color = Color.white;
        }

        void LottoWin()
        {
            var S = sim.S; var ev = Event.current;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 300, 60, 600, 480);
            GUI.color = new Color(0.07f, 0.03f, 0.09f, 0.96f); GUI.DrawTexture(r, white);                    // 분홍 홀로그램 테 (09-24)
            GUI.color = new Color(1f, 0.66f, 0.94f, 0.05f); for (float yy = r.y + 2; yy < r.yMax; yy += 4) GUI.DrawTexture(new Rect(r.x, yy, r.width, 1), white);
            Frame(r, new Color(1f, 0.66f, 0.94f, 0.85f), 1.5f);
            GUI.color = new Color(1f, 0.66f, 0.94f); foreach (var cn in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax - 14, r.y), new Vector2(r.x, r.yMax - 3), new Vector2(r.xMax - 14, r.yMax - 3) }) GUI.DrawTexture(new Rect(cn.x, cn.y, 14, 3), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 20, r.y + 14, 200, 30), "<size=18><b><color=#f3c8ff>즉석 복권</color></b></size>", label);   // 🎱 궤도 로또는 뺐다 (09-24 사장님 「로또는 의미가 없다」)
            GUI.Label(new Rect(r.x, r.y + 18, r.width - 20, 24), "<size=13><color=#8a9bb3>돈</color> " + KNum.Fmt(S.cash) + "</size>", cost);
            Scratch(r, ev);
            if (GUI.Button(new Rect(r.xMax - 106, r.yMax - 46, 92, 34), "닫기", btnOff)) lottoOpen = false;
        }

        void Scratch(Rect r, Event ev)
        {
            var S = sim.S;
            GUI.Label(new Rect(r.x + 20, r.y + 56, r.width - 40, 22), "<size=13>한 장 <color=#ffdf95>" + KNum.Fmt(sim.ScratchPrice) + "</color> · 출동마다 " + sim.ScratchMax + "장 (남은 장 " + sim.ScratchLeft + ") · 같은 그림 셋이면 당첨</size>", label);
            GUI.Label(new Rect(r.x + 20, r.y + 78, r.width - 40, 20), "<size=11><color=#8a9bb3>고철 ×1 · 위성 ×2 · 금고 ×5 · 행성 ×20 · 황금 ×100</color></size>", label);
            var card = new Rect(r.center.x - 200, r.y + 108, 400, 260);
            GUI.color = new Color(0.93f, 0.9f, 0.84f); GUI.DrawTexture(card, white); GUI.color = Color.white;
            Frame(card, new Color(0.7f, 0.5f, 0.2f), 3);
            if (scGrid == null)
            {
                GUI.Label(card, "<size=18><color=#6a5a40>한 장 사서 긁어 보세요</color></size>", center);
                bool can = sim.ScratchLeft > 0 && S.cash >= sim.ScratchCost;
                if (GUI.Button(new Rect(r.center.x - 110, r.yMax - 92, 220, 48), can ? "<size=17>한 장 사기 · " + (sim.ScratchCost <= 0 ? "공짜" : KNum.Fmt(sim.ScratchCost)) + "</size>" : "<size=13>" + (sim.ScratchLeft <= 0 ? "오늘은 다 긁었다 — 출동하고 오자" : "돈이 모자라다") + "</size>", can ? btn : btnOff) && can)
                {
                    scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[9, ScCols * ScRows]; scDone = false; scGot = 0; OrbitSfx.Play("buy", 0.5f);
                }
                return;
            }
            float cw = card.width / 3, ch = card.height / 3;
            int revealed = 0;
            for (int c = 0; c < 9; c++)
            {
                var cr = new Rect(card.x + (c % 3) * cw + 6, card.y + (c / 3) * ch + 6, cw - 12, ch - 12);
                int sym = scGrid[c];
                Color sc = sym == 4 ? new Color(0.85f, 0.65f, 0.1f) : sym == 3 ? new Color(0.35f, 0.5f, 0.9f) : sym == 2 ? new Color(0.2f, 0.6f, 0.35f) : sym == 1 ? new Color(0.45f, 0.45f, 0.55f) : new Color(0.55f, 0.45f, 0.35f);
                bool winCell = scDone && sym == scWin;
                GUI.color = winCell ? new Color(1f, 0.95f, 0.6f) : new Color(0.98f, 0.96f, 0.92f); GUI.DrawTexture(cr, white); GUI.color = Color.white;
                GUI.Label(cr, "<size=22><b><color=#" + ColorUtility.ToHtmlStringRGB(sc) + ">" + SweepSim.ScratchSym[sym] + "</color></b></size>", center);
                // 은박
                int left = 0; float bw = cr.width / ScCols, bh = cr.height / ScRows;
                for (int b = 0; b < ScCols * ScRows; b++)
                {
                    if (scCoat[c, b]) continue; left++;
                    if (scDone) continue;
                    var br = new Rect(cr.x + (b % ScCols) * bw, cr.y + (b / ScCols) * bh, bw + 0.5f, bh + 0.5f);
                    float shade = 0.72f + 0.06f * ((b * 7 + c) % 3);
                    GUI.color = new Color(shade, shade, shade + 0.03f); GUI.DrawTexture(br, white); GUI.color = Color.white;
                }
                if (left <= ScCols * ScRows * 0.45f) revealed++;
                // 긁기 — 누른 채 지나가면 은박이 벗겨진다
                if (!scDone && (ev.type == EventType.MouseDrag || ev.type == EventType.MouseDown) && ev.button == 0 && cr.Contains(ev.mousePosition))
                {
                    for (int b = 0; b < ScCols * ScRows; b++)
                    {
                        var bc = new Vector2(cr.x + (b % ScCols + 0.5f) * bw, cr.y + (b / ScCols + 0.5f) * bh);
                        if (!scCoat[c, b] && (bc - ev.mousePosition).sqrMagnitude < 16 * 16) { scCoat[c, b] = true; if (Random.value < 0.25f) OrbitSfx.PlayPitch("tick", 0.2f, 1.6f + Random.value * 0.4f); }
                    }
                    ev.Use();
                }
            }
            if (!scDone && revealed == 9) { scDone = true; scDoneT = Time.unscaledTime; scGot = sim.ScratchClaim(); if (scGot > 0) { OrbitSfx.Play("buy", 1f); OrbitSfx.Play("clank", 0.7f); game.creditPulse = 1; } else OrbitSfx.PlayPitch("tick", 0.6f, 0.5f); }
            if (!scDone)
            {
                GUI.Label(new Rect(r.x, r.yMax - 88, r.width, 22), "<size=13><color=#b89ac6>누른 채 문질러 긁기</color></size>", center);
                if (GUI.Button(new Rect(r.center.x - 70, r.yMax - 62, 140, 32), "<size=12>한 번에 다 긁기</size>", btnOff)) for (int c = 0; c < 9; c++) for (int b = 0; b < ScCols * ScRows; b++) scCoat[c, b] = true;
            }
            else
            {
                float k = Mathf.Clamp01((Time.unscaledTime - scDoneT) / 0.25f);
                GUI.Label(new Rect(r.x, r.yMax - 100, r.width, 40), scGot > 0 ? "<size=" + Mathf.RoundToInt(Mathf.Lerp(40, 26, k)) + "><b><color=#ffdf95>당첨! " + SweepSim.ScratchSym[scWin] + " ×" + SweepSim.ScratchMult[scWin] + "  +" + KNum.Fmt(scGot) + "</color></b></size>" : "<size=22><color=#b89ac6>꽝 — 다음 장에</color></size>", center);
                bool can = sim.ScratchLeft > 0 && S.cash >= sim.ScratchCost;
                if (GUI.Button(new Rect(r.center.x - 110, r.yMax - 56, 220, 40), can ? "<size=15>한 장 더 · " + (sim.ScratchCost <= 0 ? "공짜" : KNum.Fmt(sim.ScratchCost)) + "</size>" : "<size=12>오늘은 끝</size>", can ? btn : btnOff) && can)
                { scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[9, ScCols * ScRows]; scDone = false; scGot = 0; OrbitSfx.Play("buy", 0.5f); }
            }
        }

        void Lotto(Rect r)
        {
            var S = sim.S;
            GUI.Label(new Rect(r.x + 20, r.y + 58, r.width - 40, 24), "<size=15><b>제 " + S.lottoRound + "회</b> · <color=#ffdf95>출동 다녀오면 바로 추첨</color></size>", label);
            GUI.Label(new Rect(r.x + 20, r.y + 82, r.width - 40, 20), "<size=11><color=#8a9bb3>1~12 중 셋 · 한 장 " + KNum.Fmt(sim.LottoPrice) + " · 한 회 3장까지 · 셋 다 ×60 · 둘 ×2 · 하나 ×0.3</color></size>", label);
            // 번호판
            for (int n = 1; n <= 12; n++)
            {
                var nb = new Rect(r.x + 40 + ((n - 1) % 6) * 58, r.y + 116 + ((n - 1) / 6) * 58, 50, 50);
                bool on = lottoPick.Contains(n);
                GUI.color = on ? new Color(1f, 0.55f, 0.92f, 0.55f) : new Color(1f, 0.66f, 0.94f, 0.06f); GUI.DrawTexture(nb, texDisc);          // 빛 구슬
                GUI.color = new Color(1f, 0.66f, 0.94f, on ? 1f : 0.45f); GUI.DrawTexture(nb, texRing); GUI.color = Color.white;
                GUI.Label(nb, "<size=18><b>" + (on ? "<color=#ffffff>" : "<color=#b89ac6>") + n + "</color></b></size>", center);
                if (GUI.Button(nb, GUIContent.none, GUIStyle.none)) { if (on) lottoPick.Remove(n); else if (lottoPick.Count < 3) lottoPick.Add(n); OrbitSfx.Play("tick", 0.4f); }
            }
            bool can = lottoPick.Count == 3 && S.lotto.Count < 3 && S.cash >= sim.LottoPrice;
            if (GUI.Button(new Rect(r.x + 400, r.y + 116, 170, 44), can ? "<size=14>이 번호로 사기</size>" : "<size=12>" + (S.lotto.Count >= 3 ? "이번 회는 3장까지" : lottoPick.Count < 3 ? "번호 셋을 고르세요" : "돈이 모자라다") + "</size>", can ? btn : btnOff) && can)
            { lottoPick.Sort(); if (sim.LottoBuy(lottoPick[0], lottoPick[1], lottoPick[2])) { OrbitSfx.Play("buy", 0.6f); lottoPick.Clear(); } }
            if (GUI.Button(new Rect(r.x + 400, r.y + 166, 170, 34), "<size=12>자동 고르기</size>", btnOff))
            { lottoPick.Clear(); while (lottoPick.Count < 3) { int n = Random.Range(1, 13); if (!lottoPick.Contains(n)) lottoPick.Add(n); } OrbitSfx.Play("tick", 0.5f); }
            // 내 표
            GUI.Label(new Rect(r.x + 20, r.y + 244, 200, 22), "<size=13>내 표</size>", label);
            for (int i = 0; i < 3; i++)
            {
                var tr = new Rect(r.x + 20 + i * 180, r.y + 270, 170, 40);
                GUI.color = new Color(0.12f, 0.07f, 0.15f); GUI.DrawTexture(tr, white); GUI.color = Color.white; Frame(tr, new Color(0.4f, 0.25f, 0.45f), 1.5f);
                GUI.Label(tr, i < S.lotto.Count ? "<size=17><b>" + S.lotto[i].a + " · " + S.lotto[i].b + " · " + S.lotto[i].c + "</b></size>" : "<size=12><color=#5f4f66>빈 칸</color></size>", center);
            }
            // 지난 회
            if (S.lottoLast != null)
            {
                GUI.Label(new Rect(r.x + 20, r.y + 330, r.width - 40, 22), "<size=13>지난 " + S.lottoLastRound + "회 당첨 번호  <b><color=#f3c8ff>" + S.lottoLast[0] + " · " + S.lottoLast[1] + " · " + S.lottoLast[2] + "</color></b></size>", label);
                GUI.Label(new Rect(r.x + 20, r.y + 354, r.width - 40, 22), "<size=12>" + (S.lottoLastHit < 0 ? "<color=#8a9bb3>그 회엔 표가 없었다</color>" : S.lottoLastWin > 0 ? "<color=#6fcf97>" + S.lottoLastHit + "개 맞음 · +" + KNum.Fmt(S.lottoLastWin) + "</color>" : "<color=#ee7766>꽝</color>") + "</size>", label);
            }
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
            if (GUI.Button(new Rect(cx + 340, by, 168, 36), "<size=12>빚 갚기 −" + KNum.Fmt(System.Math.Min(S.cash, S.debt)) + "</size>", btn)) Repay();
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
        string lastBuyBranch = "";
        Vector2 camC; float camZ = 1f, userZ = 1f; Vector2 pan; bool dragging, dragMoved; int dragBtn; Vector2 dragFrom, dragStart;

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
            overAuto = r.Contains(Event.current.mousePosition);
            GUI.color = on ? new Color(0.3f, 0.21f, 0.06f, 0.85f) : new Color(0.05f, 0.05f, 0.08f, 0.6f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            GUI.Label(r, "<size=9><color=" + (on ? "#ffdf95" : "#4a5260") + ">시험 · 자동 " + (on ? "켬" : "끔") + "</color></size>", center);
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
            GUI.Label(new Rect(r.x, r.y, r.width, 24), on ? "<size=16><b><color=#9ff0bf>주식</color></b></size>" : "<size=16><b><color=#5fa37d>주식</color></b></size>", center);
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
            GUI.Label(new Rect(r.x + 12, r.y + 6, 200, 24), "<size=16><b><color=#9ff0bf>궤도 증권</color></b></size>", label);
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 24), "<size=12><color=#8a9bb3>평가</color> " + KNum.Fmt(tv) + (tc > 0 ? "  " + (tv >= tc ? "<color=#ff5c5c>+" : "<color=#5494ff>") + ((tv / tc - 1) * 100).ToString("0.0") + "%</color>" : "") + "</size>", cost);
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
            string hold = ss.shares > 0 ? "보유 " + KNum.Fmt(ss.shares * ss.price) + "  " + (ss.shares * ss.price >= ss.cost ? "<color=#ff5c5c>+" : "<color=#5494ff>") + ((ss.shares * ss.price / ss.cost - 1) * 100).ToString("0.0") + "%</color>" : "<color=#5f6878>보유 없음</color>";
            GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), "<size=12>" + hold + "</size>", label);
            GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), "<size=11><color=#8a9bb3>돈 " + KNum.Fmt(S.cash) + " · 수수료 " + (sim.StockFee * 100).ToString("0.#") + "%</color></size>", cost);
            y += 22;
            float bw = (r.width - 24 - 12) / 6f;
            string[] bl = { "10%", "25%", "50%", "전부" }; float[] bf = { 0.1f, 0.25f, 0.5f, 1f };
            if (!sim.R.over) GUI.Label(new Rect(r.x + 12, y, r.width - 24, 28), "<size=12><color=#8a9bb3>출동 중엔 시세만 본다 — 사고팔기는 조종실 증권에서</color></size>", center);   // 🔒 출동 중 사고팔기 금지 (09-24 사장님 「팔기도 막아」)
            else
            {
                for (int k = 0; k < 4; k++) if (GUI.Button(new Rect(r.x + 12 + k * (bw + 2), y, bw, 28), "<size=12><color=#9ff0bf>사기 " + bl[k] + "</color></size>", btn)) TradeBuy(si, bf[k], new Vector2(r.x + 12 + k * (bw + 2) + bw / 2, y + 14));
                GUI.enabled = ss.shares > 0 && GUI.enabled;
                if (GUI.Button(new Rect(r.x + 12 + 4 * (bw + 2) + 6, y, bw, 28), "<size=12><color=#ffb3a8>절반 팔기</color></size>", btn)) TradeSell(si, 0.5, new Vector2(r.x + 12 + 4 * (bw + 2) + 6 + bw / 2, y + 14));
                if (GUI.Button(new Rect(r.x + 12 + 5 * (bw + 2) + 6, y, bw, 28), "<size=12><color=#ffb3a8>전부 팔기</color></size>", btn)) TradeSell(si, 1, new Vector2(r.x + 12 + 5 * (bw + 2) + 6 + bw / 2, y + 14));
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
                string tip = "다음 속보까지 " + Mathf.CeilToInt(mk.NextNewsIn) + "초" + (il >= 2 ? " · " + (nn.up != null ? "<color=#ff5c5c>오를</color>" : "<color=#5494ff>내릴</color>") + " 쪽: " + SecName(nn) : "") + (il >= 3 ? " · 「" + Clip(nn.head, 16) + "」" : "");
                GUI.Label(new Rect(r.x + 12, y, r.width - 24, 20), "<size=11><color=#e8c77e>내부자</color> " + tip + "</size>", label);
                y += 20;
            }
            // 최근 속보
            GUI.color = new Color(1, 1, 1, 0.08f); GUI.DrawTexture(new Rect(r.x + 10, y + 2, r.width - 20, 1), white); GUI.color = Color.white;
            for (int k = 0; k < 3 && k < MS.news.Count; k++)
            {
                var nw = MS.news[MS.news.Count - 1 - k];
                GUI.Label(new Rect(r.x + 12, y + 5 + k * 18, r.width - 24, 18), "<size=11>" + (k == 0 ? "<color=#ffdf95>" : "<color=#8a9bb3>") + Clip(nw.head, 30) + "</color></size>", label);
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
                bannerNews = ns[ns.Count - 1]; newsBanner = 5.5f; OrbitSfx.Play("supply", 0.5f); Anchor(bannerNews.head);
                lastNewsSeen = ns[ns.Count - 1].t;
            }
            if (newsBanner <= 0 || bannerNews == null) return;
            newsBanner -= Time.unscaledDeltaTime;
            if (!sim.R.over) return;                                         // 출동 중엔 앵커가 읽는다 — 위 띠가 의뢰 카드를 가렸다 (09-25 점검)
            float a = Mathf.Clamp01(newsBanner / 0.5f) * Mathf.Clamp01((5.5f - newsBanner) / 0.25f);
            var b = new Rect(vw / 2 - 300, 50, 600, 46);
            GUI.color = new Color(0.55f, 0.08f, 0.06f, 0.92f * a); GUI.DrawTexture(new Rect(b.x, b.y, 74, b.height), white);
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.92f * a); GUI.DrawTexture(new Rect(b.x + 74, b.y, b.width - 74, b.height), white);
            GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(b.x, b.y, 74, b.height), "<size=15><b>속보</b></size>", center);
            GUI.Label(new Rect(b.x + 84, b.y + 3, b.width - 94, 22), "<size=14><b>" + Clip(bannerNews.head, 34) + "</b></size>", label);
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
            if (R.shots < R.maxShots && !R.clean) GUI.Label(new Rect(r.x - 20, r.yMax + 1, r.width + 40, 16), "<size=9><color=#8a7fb0>공격 " + (sim.HoleChance * 100).ToString("0.#") + "%</color></size>", center);   // 확률로 찬다
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

        static readonly string[] PlanetHint = { "", "달 — 궤도가 느리다 · 금고 위성이 많으니 노려 보자", "화성 — 22초마다 모래 폭풍이 온다 · 얼음 껍질은 먼저 깨 두자", "목성 — 중력이 잔해를 안쪽으로 모은다 · 안쪽 가장자리에 블랙홀을", "토성 — 고리가 두 겹 · 가운데 틈은 비어 있다" , "소행성대 — 단단한 암석과 광석이 많다", "천왕성 — 옆으로 누운 궤도 · 얼음 결정", "해왕성 — 초속 폭풍이 잔해를 흩는다", "카이퍼 벨트 — 태양계 끝 · 고대 탐사선과 혜성" };
        static Texture2D iconTex;
        void DrawIcon(Rect r, string id)
        {
            if (iconTex == null) iconTex = Resources.Load<Texture2D>("tree_icons");
            int idx = System.Array.IndexOf(SweepSim.IconOrder, SweepSim.IconAlias.TryGetValue(id, out var al) ? al : id);   // ✦ 새 칸은 비슷한 아이콘을 빌린다
            if (iconTex == null || idx < 0) return;
            int cols = 8, rows = Mathf.Max(1, iconTex.height / 96);
            float cw = 1f / cols, ch = 1f / rows;
            GUI.DrawTextureWithTexCoords(r, iconTex, new Rect((idx % cols) * cw, 1f - (idx / cols + 1) * ch, cw, ch));
        }

        static Color VisCol(string id)
        {
            if (id.StartsWith("q_")) return new Color(1f, 0.36f, 0.81f);
            switch (SweepSim.VisBranch(id))
            {
                case "claw": return SweepGame.Amber;
                case "hull": return new Color(0.56f, 0.72f, 0.9f);
                case "route": return new Color(0.6f, 0.72f, 1f);
                case "cross": return new Color(1f, 0.87f, 0.58f);
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
            void BayHead()
            {
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
            }

            var area = new Rect(0, 46, vw, 500);
            bool inArea = area.Contains(Event.current.mousePosition);
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
            // 🔍 확대 (09-24 사장님 「PC 게임이니 확대」) — 휠 = 마우스 자리를 중심으로 0.6~3배 · 끌기(왼쪽/오른쪽) = 이동 · [+][−][맞춤]
            var zb = new Rect(ox + 16, area.y + 8, 118, 30);
            void ZoomAt(Vector2 at, float nz)
            {
                nz = Mathf.Clamp(nz, 0.6f, 3f);
                float z0 = camZ * userZ, z1 = camZ * nz;
                var wc = (at - area.center - pan) / (cellPx * z0);      // 마우스 아래 칸 좌표 (camC 기준)
                pan = at - area.center - wc * cellPx * z1; userZ = nz;
            }
            if (ev.type == EventType.ScrollWheel && area.Contains(ev.mousePosition)) { ZoomAt(ev.mousePosition, userZ * (ev.delta.y > 0 ? 1 / 1.15f : 1.15f)); ev.Use(); }
            if (ev.type == EventType.MouseDown && (ev.button == 1 || ev.button == 0) && area.Contains(ev.mousePosition) && !zb.Contains(ev.mousePosition)) { dragging = true; dragMoved = false; dragBtn = ev.button; dragFrom = dragStart = ev.mousePosition; }
            if (ev.type == EventType.MouseDrag && dragging)
            {
                if (!dragMoved && (ev.mousePosition - dragStart).sqrMagnitude > 36) dragMoved = true;   // 6px 넘게 움직여야 끌기 — 칸 누르기와 안 헷갈리게
                if (dragMoved) { pan += ev.mousePosition - dragFrom; dragFrom = ev.mousePosition; ev.Use(); }
            }
            if (ev.type == EventType.MouseUp && dragging && ev.button == dragBtn) { dragging = false; if (dragMoved) ev.Use(); }
            float zz = camZ * userZ;
            Vector2 ToScr(Vector2Int c) => area.center + pan + ((Vector2)c - camC) * cellPx * zz;
            float tile = 44f * zz;

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
                    if (gold) Line(a, b, new Color(1f, 0.72f, 0.2f, 0.3f), 8 * zz);
                    Line(a, b, gold ? new Color(1f, 0.74f, 0.2f) : new Color(0.32f, 0.3f, 0.28f, st[k] == 1 ? 0.5f : 0.9f), (gold ? 3.2f : 2f) * zz);
                }
            }
            // ⭐ 추천 한 칸 — 모르면 이것만 사도 된다 (09-24 설계서 「무거워도 자연스럽게」)
            int recK = -1; { double best = double.MaxValue; for (int k = 0; k < nT; k++) { var t = gtiles[k]; if (t.stat < 0 || st[k] != 2 || sim.State(t.stat) != NodeSt.Can) continue; double c = sim.TileCost(t.stat) * (SweepSim.Nodes[t.stat].branch == lastBuyBranch ? 0.6 : 1); if (c < best) { best = c; recK = k; } } }
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
                bool inf = !isRoot && SweepSim.Infinite(t.stat);
                bool can = (next || inf && owned) && ns == NodeSt.Can;
                bool circle = !isRoot && n.id.StartsWith("q_");
                bool diamond = !isRoot && !circle && n.max == 1;
                var pc = ToScr(t.cell);
                float grow = isRoot ? 0 : nodePulse[t.stat] * 8 * zz;
                float sz = (isRoot ? tile * 1.25f : diamond ? tile * 0.92f : tile) + grow;
                var r = new Rect(pc.x - sz / 2, pc.y - sz / 2, sz, sz);
                if (can) { GUI.color = new Color(1f, 0.78f, 0.3f, 0.28f + 0.18f * Mathf.Sin(Time.time * 5 + k)); GUI.DrawTexture(new Rect(r.x - 9 * zz, r.y - 9 * zz, r.width + 18 * zz, r.height + 18 * zz), texDisc); }
                if (diamond) GUI.matrix = m0 * Matrix4x4.TRS(new Vector3(pc.x, pc.y, 0), Quaternion.Euler(0, 0, 45), Vector3.one) * Matrix4x4.TRS(new Vector3(-pc.x, -pc.y, 0), Quaternion.identity, Vector3.one);
                Color bg = owned ? Color.Lerp(bcol, new Color(0.1f, 0.08f, 0.06f), 0.62f) : next ? new Color(0.09f, 0.09f, 0.1f) : new Color(0.06f, 0.06f, 0.07f);
                GUI.color = bg; GUI.DrawTexture(r, circle ? texDisc : white);
                Color edge = can ? new Color(1f, 0.8f, 0.35f) : owned ? Color.Lerp(bcol, Color.black, 0.25f) : new Color(0.22f, 0.21f, 0.2f);
                if (circle) { GUI.color = edge; GUI.DrawTexture(r, texRing); GUI.color = Color.white; } else Frame(r, edge, can ? 2.5f : 1.5f);
                GUI.matrix = m0;
                // 아이콘 — 칸 안에 글자 없이 (시안에서 구운 tree_icons.png)
                bool lockedTile = !owned && ns == NodeSt.Locked;
                GUI.color = owned ? new Color(1f, 0.96f, 0.86f) : next ? (lockedTile ? new Color(0.3f, 0.31f, 0.34f) : new Color(0.72f, 0.72f, 0.74f)) : new Color(0.17f, 0.17f, 0.19f);
                float isz = sz * 0.62f;
                int planetI = isRoot ? -1 : System.Array.IndexOf(SweepSim.PlanetNode, n.id);
                if (planetI > 0) { var pr = new Rect(pc.x - isz * 0.62f, pc.y - isz * 0.62f, isz * 1.24f, isz * 1.24f); GUI.color = owned ? Color.white : next ? new Color(0.6f, 0.62f, 0.66f) : new Color(0.2f, 0.2f, 0.22f); GUI.DrawTexture(pr, PlanetArt.Get(planetI).texture); }
                else DrawIcon(new Rect(pc.x - isz / 2, pc.y - isz / 2, isz, isz), isRoot ? "R" : n.id);
                if (!isRoot && SweepSim.KeyNodes.Contains(n.id)) { float kp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3 + k); GUI.color = new Color(0.71f, 0.61f, 1f, owned ? 0.9f : 0.35f + 0.3f * kp); Frame(new Rect(r.x - 3 * zz, r.y - 3 * zz, r.width + 6 * zz, r.height + 6 * zz), GUI.color, 2f); GUI.color = Color.white; if (!owned && next) GUI.Label(new Rect(r.xMax - 6, r.y - 12, 30, 16), "<size=10><color=#d8ccff>열쇠</color></size>", label); }
                if (!isRoot && n.max == 1 && System.Array.IndexOf(SweepSim.WeaponNode, n.id) == sim.Weapon && owned) { GUI.color = new Color(1f, 0.55f, 0.5f); GUI.Label(new Rect(pc.x - 40, r.yMax + 2 * zz, 80, 16), "<size=10><b>장착 중</b></size>", center); GUI.color = Color.white; }
                if (k == recK) { float rp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4); GUI.color = new Color(1f, 0.87f, 0.4f, 0.55f + 0.45f * rp); Frame(new Rect(r.x - 5 * zz, r.y - 5 * zz, r.width + 10 * zz, r.height + 10 * zz), GUI.color, 2.5f); GUI.color = Color.white; GUI.Label(new Rect(pc.x - 40, r.yMax + 2 * zz, 80, 16), "<size=10><b><color=#ffdf95>추천</color></b></size>", center); }
                if (lockedTile && next) { GUI.color = new Color(1f, 0.6f, 0.55f); GUI.Label(new Rect(r.xMax - 14, r.y - 4, 18, 18), "<size=11>잠</size>", center); }
                GUI.color = Color.white;
                if (inArea && r.Contains(ev.mousePosition)) hover = k;
                if (inf && owned) GUI.Label(new Rect(r.x - 10, r.yMax, r.width + 20, 16), "<size=10><color=#ffdf95>∞ " + sim.S.lv[t.stat] + "</color></size>", center);
                bool clicked = !isRoot && (next || inf && owned) && inArea && GUI.Button(r, GUIContent.none, GUIStyle.none);
                if (clicked && ns == NodeSt.Can)
                {
                    int times = shift ? 5 : 1;
                    while (times-- > 0 && sim.State(t.stat) == NodeSt.Can) sim.BuyTile(t.stat);
                    if (n.id == "e_shop") Guide("부품 가게가 생겼다 — 오른쪽 탭 「부품 가게」 ▸");                       // 🧭 새 방 안내 (09-24 사장님 32번)
                    else if (n.id == "a_open") Guide("증권이 열렸다 — 조종실 오른쪽 「증권 하러 가기」 ▸ · 출동 중엔 S");
                    nodePulse[t.stat] = 1; OrbitSfx.Play("buy", 0.7f, 0.01f, 0.15f); lastBuyBranch = n.branch; BuyFx(pc, SweepSim.KeyNodes.Contains(n.id) ? new Color(0.71f, 0.61f, 1f) : bcol, n.max == 1, SweepSim.KeyNodes.Contains(n.id) ? "핵심 해금!" : "해금!");
                }
                else if (clicked && ns != NodeSt.Max)
                { OrbitSfx.Play("clank", 0.35f, 0.05f, 0f); Deny(pc, WhyNot(t.stat)); }   // 🚫 안 눌리는 칸 — 왜 안 되는지 그 자리에 (09-25 사장님 「안 눌리는 게 있던데」)
            }
            // 영역 밖 띠 — 넘어간 칸을 덮고 머리 · 안내를 다시 그린다
            GUI.color = new Color(0.02f, 0.027f, 0.04f); GUI.DrawTexture(new Rect(0, 0, vw, area.y), white); GUI.DrawTexture(new Rect(0, area.yMax, vw, RefH - area.yMax), white);
            GUI.color = new Color(1f, 0.78f, 0.3f, 0.18f); GUI.DrawTexture(new Rect(0, area.y - 1, vw, 1), white); GUI.DrawTexture(new Rect(0, area.yMax, vw, 1), white);
            GUI.color = Color.white;
            BayHead();
            {
                float bw3 = 36;
                if (GUI.Button(new Rect(zb.x, zb.y, bw3, zb.height), "<size=16>−</size>", btnOff)) ZoomAt(area.center, userZ / 1.25f);
                if (GUI.Button(new Rect(zb.x + bw3 + 3, zb.y, bw3, zb.height), "<size=16>+</size>", btnOff)) ZoomAt(area.center, userZ * 1.25f);
                if (GUI.Button(new Rect(zb.x + (bw3 + 3) * 2, zb.y, 40, zb.height), "<size=11>맞춤</size>", btnOff)) { userZ = 1; pan = Vector2.zero; }
                var zr = new Rect(zb.xMax + 4, zb.y + 5, 44, 20);
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.9f); GUI.DrawTexture(zr, white); GUI.color = Color.white;
                GUI.Label(zr, "<size=11><color=#8a93a3>" + Mathf.RoundToInt(userZ * 100) + "%</color></size>", center);
            }
            PartsButton(new Rect(zb.x, zb.yMax + 8, 230, 30));             // 무기 효과판 뺌 (09-24 23번)
            {   // 🪐 구역 진행 — 지금 구역 칸을 다 찍으면 다음 항로 (09-24 6·21번)
                int zo = sim.ZoneOpen, zl = sim.ZoneLeft(zo), zt = 0; for (int i = 0; i < SweepSim.Nodes.Length; i++) if (SweepSim.Zone[i] == zo && SweepSim.ZoneNeed(i)) zt++;
                bool last = zo + 1 >= SweepSim.OrbitOrder.Length;
                var zr = new Rect(zb.xMax + 58, zb.y + 3, 320, 24);                  // 확대 단추 줄 옆 — 트리 칸과 안 겹치게
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.9f); GUI.DrawTexture(zr, white); GUI.color = Color.white;
                string nx = last ? "" : SweepSim.Orbits[SweepSim.OrbitOrder[zo + 1]].name;
                GUI.Label(new Rect(zr.x + 8, zr.y + 3, zr.width - 16, 18), "<size=12><color=#ffdf95>" + SweepSim.ZoneName[zo] + "</color> 구역 " + (zt - zl) + "/" + zt + (last ? "" : zl > 0 ? " <color=#8a93a3>— 다 찍으면 " + nx + " 항로</color>" : " <color=#6fcf97>— " + nx + " 항로를 살 수 있다</color>") + "</size>", label);
            }
            if (testTip >= 0) { for (int k = 0; k < nT; k++) if (gtiles[k].stat >= 0 && SweepSim.Nodes[gtiles[k].stat].id == testTipId) hover = k; }   // 에디터 시험용
            if (hover >= 0) Tip(hover, ToScr(gtiles[hover].cell), st[hover], tile);
            else GUI.Label(new Rect(ox, area.yMax + 2, 750, 16), "<size=11>칸에 마우스를 올리면 무엇인지 보인다 · 빛나는 칸을 누르면 산다 · 휠 = 확대 · 끌기 = 이동</size>", center);
        }

        GUIStyle tipWrap;
        public int testTip = -1; public string testTipId;                  // 에디터 시험용 — 툴팁 강제로 띄우기
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
            // 설명 길이에 맞춰 키가 자란다 (09-24 글자 잘림 점검 — 긴 설명이 한 줄 칸에서 잘렸다)
            if (tipWrap == null) tipWrap = new GUIStyle(center) { wordWrap = true, fontSize = 13 };
            const float TipW = 340;
            float dh = vis == 1 ? 22 : Mathf.Max(22, tipWrap.CalcHeight(new GUIContent(n.desc), TipW - 24));
            bool keyNote = vis != 3 && vis != 1 && SweepSim.KeyNodes.Contains(n.id) && sim.State(t.stat) != NodeSt.Locked && sim.State(t.stat) != NodeSt.Hidden;
            var r = new Rect(at.x + tile / 2 + 14, at.y - 70, TipW, vis == 1 ? 96 : 138 + dh + (keyNote ? 18 : 0));
            if (r.xMax > vw - 8) r.x = at.x - tile / 2 - 14 - r.width;
            r.x = Mathf.Max(8, r.x);
            r.y = Mathf.Clamp(r.y, 50, 500 - r.height);
            GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.97f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.6f, 0.5f, 0.35f), 2);
            GUI.color = new Color(0.12f, 0.12f, 0.14f); GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, 34), white); GUI.color = Color.white;
            title.fontSize = 18;
            if (vis == 1)
            {
                GUI.Label(new Rect(r.x, r.y + 4, r.width, 28), "<color=#b89a6a>?</color>", title);
                GUI.Label(new Rect(r.x, r.y + 52, r.width, 20), "앞 칸을 사면 무엇인지 보인다", center);
                return;
            }
            string nm = n.name + (SweepSim.Tiles(t.stat) > 1 ? " " + Roman[t.j] : "");
            GUI.Label(new Rect(r.x, r.y + 4, r.width, 28), "<color=#d9b98a>" + nm + "</color>", title);
            GUI.Label(new Rect(r.x + 12, r.y + 42, r.width - 24, dh), n.desc, tipWrap);
            float oy = dh - 22;                                          // 설명이 길어진 만큼 아래 줄을 내린다
            GUI.color = new Color(0.3f, 0.28f, 0.24f); GUI.DrawTexture(new Rect(r.x + 24, r.y + 70 + oy, r.width - 48, 1), white); GUI.DrawTexture(new Rect(r.x + 24, r.y + 98 + oy, r.width - 48, 1), white); GUI.color = Color.white;
            int from = t.j == 1 ? 0 : SweepSim.TileLv(t.stat, t.j - 1), to = SweepSim.TileLv(t.stat, t.j);
            GUI.Label(new Rect(r.x + 10, r.y + 74 + oy, r.width - 20, 20), Val(n.id, from) + "  <color=#d9b98a>▸</color>  <color=#ffdf95>" + Val(n.id, to) + "</color>", center);
            string foot;
            if (vis == 3 && SweepSim.Infinite(t.stat)) foot = (ns == NodeSt.Can ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + "</color>  <color=#ffdf95>∞ " + sim.S.lv[t.stat] + "번 삼 · 계속 살 수 있다</color>";   // 누적 칸 — 다음 가격 (09-24 친구들 「가격이 안 보인다」)
            else if (vis == 3) foot = "<color=#6fcf97>샀다</color>";
            else if (ns == NodeSt.Locked && n.id.StartsWith("p_") && sim.ZoneLeft(SweepSim.Zone[t.stat]) > 0) foot = "<color=#ff9b8f>" + SweepSim.ZoneName[SweepSim.Zone[t.stat]] + " 칸 " + sim.ZoneLeft(SweepSim.Zone[t.stat]) + "개 더 찍으면 열린다</color>";   // 🪐 구역
            else if (ns == NodeSt.Locked && SweepSim.Zone[t.stat] > sim.ZoneOpen) foot = "<color=#ff9b8f>" + SweepSim.ZoneName[SweepSim.Zone[t.stat]] + " 항로를 열면 열린다</color>";
            else if (ns == NodeSt.Locked) foot = SweepSim.Ring4(n.id) ? "<color=#ff9b8f>목성 항로를 열면 — 외행성 면허</color>" : "<color=#ff9b8f>청구서 " + SweepSim.BranchNeed[b] + "을 갚으면 열린다</color>";
            else if (ns == NodeSt.Hidden && vis != 2) foot = "<color=#ff9b8f>앞 칸을 먼저 사야 한다</color>";
            else if (ns == NodeSt.Hidden) foot = "<color=#ff9b8f>이어진 다른 칸도 사야 한다</color>";
            else if (SweepSim.KeyNodes.Contains(n.id) && sim.S.keys < 1) foot = (sim.S.cash >= sim.TileCost(t.stat) ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + "</color>  <color=#ff9b8f>+ 열쇠 1 (없음)</color>";   // 돈은 되는데 열쇠가 없다 — 값만 빨개서 이유를 몰랐다
            else foot = (ns == NodeSt.Can ? "<color=#ffffff>" : "<color=#ff9b8f>") + KNum.Fmt(sim.TileCost(t.stat)) + "</color>" + (SweepSim.KeyNodes.Contains(n.id) ? "  <color=#d8ccff>+ 열쇠 1</color>" : "");
            if (keyNote) GUI.Label(new Rect(r.x, r.y + 136 + oy, r.width, 16), "<size=11><color=#b9a9ee>" + (sim.S.keys < 1 ? "열쇠 0 — 청구서를 갚거나 파산하면 +1" : "가진 열쇠 " + sim.S.keys + " · ◆ 핵심 칸은 파산해도 남는다") + "</color></size>", center);
            center.fontSize = 20; if (center.CalcSize(new GUIContent(foot)).x > r.width - 16) center.fontSize = 14;   // 핵심 칸 · 누적 칸은 줄이 길다
            GUI.Label(new Rect(r.x, r.y + 106 + oy, r.width, 32), foot, center); center.fontSize = 13;
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
                case "w_hub": return l > 0 ? "무기 효과 열림" : "잠김";
                case "w_laser": case "w_chain": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_laser_e": case "w_chain_e": case "w_vac_e": case "w_mine_e": case "w_frz_e": case "w_clus_e": case "w_mag_e": case "w_rail_e": return l > 0 ? "특화 " + l + "단계" : "없음";   // ◇ 무기 특화
                case "w_laser_u": return new[] { "없음", "굵기 +50%", "굵기 +50% · 열 축적" }[Mathf.Min(2, l)];
                case "w_chain_u": return new[] { "없음", "7번 튄다", "7번 · 튈수록 ×1.2" }[Mathf.Min(2, l)];
                case "w_laser_a": case "w_chain_a": return l > 0 ? "각성!" : "잠김";
                case "e_shop": return l > 0 ? "부품 가게 열림" : "잠김";
                case "x_claw_arm": return l > 0 ? "켜짐" : "꺼짐";
                case "x_arm_drone": return l > 0 ? "켜짐" : "꺼짐";
                case "x_drone_bh": return l > 0 ? "켜짐" : "꺼짐";
                case "x_bh_eco": return l > 0 ? "켜짐" : "꺼짐";
                case "x_eco_route": return l > 0 ? "켜짐" : "꺼짐";
                case "x_route_claw": return l > 0 ? "켜짐" : "꺼짐";
                case "i_claw": return "화력 +" + 5 * l + "%";
                case "i_drone": return "드론 +" + 5 * l + "%";
                case "i_bh": return "블랙홀 확률 +" + (0.1 * l).ToString("0.#") + "%";
                case "i_eco": return "값 +" + 4 * l + "%";
                case "i_route": return "행성 배수 +" + (0.05 * l).ToString("0.00");
                case "w_vac": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_vac_u": return new[] { "없음", "1단계", "2단계" }[Mathf.Min(2, l)];
                case "w_vac_a": return l > 0 ? "각성!" : "잠김";
                case "w_mine": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_mine_u": return new[] { "없음", "1단계", "2단계" }[Mathf.Min(2, l)];
                case "w_mine_a": return l > 0 ? "각성!" : "잠김";
                case "w_frz": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_frz_u": return new[] { "없음", "1단계", "2단계" }[Mathf.Min(2, l)];
                case "w_frz_a": return l > 0 ? "각성!" : "잠김";
                case "w_clus": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_clus_u": return new[] { "없음", "1단계", "2단계" }[Mathf.Min(2, l)];
                case "w_clus_a": return l > 0 ? "각성!" : "잠김";
                case "w_mag": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_mag_u": return new[] { "없음", "1단계", "2단계" }[Mathf.Min(2, l)];
                case "w_mag_a": return l > 0 ? "각성!" : "잠김";
                case "w_rail": return l > 0 ? "발동 " + Mathf.RoundToInt((float)sim.ProcChance(System.Array.IndexOf(SweepSim.WeaponNode, id)) * 100) + "%" : "잠김";
                case "w_rail_u": return new[] { "없음", "1단계", "2단계" }[Mathf.Min(2, l)];
                case "w_rail_a": return l > 0 ? "각성!" : "잠김";
                case "k_claw": case "k_drone": case "k_bh": case "k_eco": case "k_route": return l > 0 ? "켜짐" : "꺼짐";
                case "w_slot2": return l > 0 ? "발동률 ×1.5" : "없음";
                case "q_insider": case "q_rage": case "q_front": case "q_debt": case "q_meteor": case "q_sling": case "q_tour": case "q_rock": case "q_gold": case "q_lazy": return l > 0 ? "켜짐" : "꺼짐";
                case "p_moon": case "p_mars": case "p_jup": case "p_sat": return l > 0 ? "열림 — 항로 다이얼에서 고른다" : "잠김";
                case "a_auto": return l > 0 ? "내 종목 봉마다 +0.08% 쪽으로" : "없음";
                case "a_read": return new[] { "없음", "다음 속보까지 시간", "+ 업종", "+ 제목까지" }[Mathf.Min(3, l)];
                case "a_ins": return l == 0 ? "없음" : "나쁜 속보 피하기 " + new[] { 0, 60, 70, 80 }[Mathf.Min(3, l)] + "%";
                case "a_big": return "수수료 " + new[] { "1", "0.6", "0.3", "0" }[Mathf.Min(3, l)] + "% · 배당 +" + (0.03f * l).ToString("0.00") + "%";
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
                case "b_n": return "공격마다 +" + (0.2f * l).ToString("0.#") + "%";
                case "c_find": return "판마다 " + l + "번";
                case "s_speed": return "공격마다 " + (1.2f + 0.25f * l).ToString("0.##") + "%";
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

        // 🎬 엔딩 크레딧 — 지구 둘레 쓰레기 0, 천천히 올라가는 글 (09-24 사장님 20번 · 시안). 누르거나 Space = 빨리
        float creditT;
        public void TestEnd(int st) { endStage = st; creditT = st == 2 ? 3 : 0; }   // 에디터 시험용
        void Credits()
        {
            var M = sim.M;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool fast = (kb != null && kb.spaceKey.isPressed) || (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed);
            creditT += Time.unscaledDeltaTime * (fast ? 6 : 1);
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            double minutes = 0; foreach (var h in M.history) minutes += h.minutes;
            string[,] rows =
            {
                { "", "<size=34><b><color=#ffdf95>궤도 청소부</color></b></size>" },
                { "", "<color=#c8d0dc>오늘도 궤도는 깨끗합니다</color>" },
                { "만든 사람", "사장님" },
                { "함께 만든", "Claude" },
                { "도트", "PixelLab" },
                { "글꼴", "갈무리 (Galmuri)" },
                { "엔진", "Unity" },
                { "먼저 해 본 친구들", "고마워" },
                { "기록", Mathf.RoundToInt((float)minutes) + "분 · 회사 " + M.company + "대 · 최고 연쇄 " + M.bestChain },
                { "", "<color=#d8ccff>★ 전설 경력 " + M.legend + "</color>" },
            };
            float y = RefH + 20 - creditT * 42;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                if (rows[i, 0].Length > 0) { GUI.Label(new Rect(0, y, vw, 18), "<size=11><color=#8a93a3>" + rows[i, 0] + "</color></size>", center); y += 20; }
                GUI.Label(new Rect(0, y, vw, 40), "<size=20><b>" + rows[i, 1] + "</b></size>", center); y += 70;
            }
            GUI.Label(new Rect(vw - 320, RefH - 30, 222, 20), "<size=11><color=#5f6878>누르고 있으면 빨리</color></size>", cost);
            if (GUI.Button(new Rect(vw - 90, RefH - 34, 80, 24), "<size=11>넘기기</size>", btnOff) || y < -40) endStage = 1;
        }

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
                if (GUI.Button(new Rect(pr.center.x - 100, pr.yMax - 70, 200, 44), "다음", bigBtn)) { endStage = 2; creditT = 0; }
                return;
            }
            if (endStage == 2) { Credits(); return; }
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
                GUI.Label(new Rect(cx, 292 + i * 20, cw, 20), "(" + h.company + "대)  " + (h.won ? "<color=#ffdf95>빚 청산</color>" : SweepSim.Bills[Mathf.Min(h.bill, SweepSim.Bills.Length - 1)].t + "에서 파산") + " · 출동 " + h.runs + " · " + Mathf.RoundToInt((float)h.minutes) + "분", label);
            }
            GUI.Label(new Rect(cx, 470, cw, 20), "<color=#f2c14e>오늘도 궤도는 깨끗합니다.</color>", center);
            // ∞ 무한 궤도 · ★ 새 회사 (09-24 사장님 12 · 26번)
            if (GUI.Button(new Rect(cx - 10, 500, 220, 44), "<color=#d8ccff>무한 궤도로 ▸</color>", bigBtn)) { sim.EnterEndless(); showResult = false; flow = 2; OrbitSfx.Play("launch", 0.8f); }
            if (GUI.Button(new Rect(cx + 220, 500, 200, 44), "새 회사로 · ★" + M.legend, bigBtn)) { game.NewGame(true); showResult = false; }
            if (GUI.Button(new Rect(cx + 430, 506, 180, 32), "<size=12>기록까지 모두 지우기</size>", btn)) { game.WipeAll(); showResult = false; }
            GUI.Label(new Rect(cx, 552, cw, 18), "<size=11><color=#8a93a3>★ 전설 경력 " + M.legend + " — 다음 회사부터 모든 값 +" + (M.legend * 10) + "% · 처음 열쇠 +" + M.legend + "</color></size>", center);
        }
    }
}
