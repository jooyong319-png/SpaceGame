using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 궤도 청소부 — 위 띠 · 오른쪽 패널 · 뉴스 띠 · 끝 화면. 전부 OnGUI (임시).
    ///
    /// 🔴 UI 는 처음부터 있지 않다 — 개념을 만날 때 생긴다 (wiki 6회차).
    ///    0:00 에는 아무것도 없다. 회수 → 판매 → 드론 → 궤도 파편 순서로 나타난다.
    /// 🔴 유혹 버튼은 다른 해금과 **똑같이 생겼다** (wiki 3회차). 따로 칠하지 않는다.
    /// </summary>
    public class OrbitHud : MonoBehaviour
    {
        public OrbitGame game;
        OrbitSim sim => game != null ? game.sim : null;

        const float RefH = 600f;
        const float PanelW = 300f;
        float scale = 1f, vw = 960f;

        public bool MenuOpen { get; private set; }
        bool confirmRestart;
        Vector2 scroll;
        readonly HashSet<string> seenUnlock = new HashSet<string>();
        readonly Dictionary<string, float> newSince = new Dictionary<string, float>();
        float bannerUntil;
        string banner = "";

        Font font;
        GUIStyle big, label, labelDim, small, cost, costDim, head, newsStyle, pinStyle, warnStyle, titleStyle, center;
        GUIStyle btn, btnOff, mini;
        Texture2D texPanel, texLine, texCard, texBar, texBarBg, texDim, texWarn;

        public bool PanelVisible => sim != null && sim.SellVisible;
        public Vector2 CreditAnchorScreen = new Vector2(120, 580);   // 동전이 날아가 꽂히는 자리 (화면 좌표)

        double shownCredits;
        int lastUnit = -1;
        float unitAt = -99f;
        string unitName = "";
        GUIStyle popStyle, unitStyle, unitSub;
        static readonly string[] UnitNames = { "", "만", "억", "조", "경" };

        /// <summary>
        /// 🔴 숫자 연출 — 크레딧은 굴러 올라가고, 만·억·조로 넘어가는 순간 크게 한 번 터진다.
        /// 「이제 억 단위구나」가 이 장르의 보상이다 (wiki 2회차).
        /// </summary>
        void Update()
        {
            if (sim == null) return;
            double a = sim.S.credits;
            if (a < shownCredits || double.IsNaN(shownCredits)) shownCredits = a;
            else shownCredits += (a - shownCredits) * (1 - Mathf.Exp(-Time.deltaTime * 7f));
            if (a - shownCredits < 1) shownCredits = a;

            double peak = sim.S.peak;
            int unit = peak >= 10000 ? Mathf.Min(4, (int)(System.Math.Log10(peak) / 4)) : 0;
            if (lastUnit < 0) lastUnit = unit;
            else if (unit > lastUnit)
            {
                lastUnit = unit;
                unitAt = Time.time;
                unitName = UnitNames[unit];
                if (game.fx != null) game.fx.CoinShower(30);
            }
        }

        public void ToggleMenu() { MenuOpen = !MenuOpen; confirmRestart = false; }

        /// <summary>화면 좌표(Input System, 왼쪽 아래 원점)가 UI 위인가 — 그러면 파편을 줍지 않는다.</summary>
        public bool BlocksWorld(Vector2 screen)
        {
            if (MenuOpen || (sim != null && sim.Finished)) return true;
            if (PanelVisible && screen.x > Screen.width - PanelW * scale) return true;
            return false;
        }

        public void OnSimEvent(SimEvent e)
        {
            if (e.kind == SimEventKind.Warn) Banner("⚠ " + OrbitSim.Names[e.orbit] + " 봉쇄 임박 — 드론을 옮기면 산다", 12f);
            else if (e.kind == SimEventKind.Lock) Banner(OrbitSim.Names[e.orbit] + " 봉쇄 — 다시는 못 들어간다", 8f);
            else if (e.kind == SimEventKind.ContractDone) Banner("계약 완료", 3f);
            else if (e.kind == SimEventKind.Unlock && e.text == null && sim.S.endingsOpen) Banner("마지막 기술 셋이 열렸다 — 하나만 살 수 있다", 10f);
        }

        void Banner(string s, float secs) { banner = s; bannerUntil = Time.time + secs; }

        // ───────────────────────────────── 스타일

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (big != null && texPanel != null) return;
            font = Resources.Load<Font>("Galmuri11");
            texPanel = Tex(new Color(0.055f, 0.075f, 0.11f, 0.97f));
            texLine = Tex(new Color(0.14f, 0.19f, 0.25f));
            texCard = Tex(new Color(0.08f, 0.11f, 0.16f));
            texBar = Tex(new Color(0.95f, 0.78f, 0.36f));
            texBarBg = Tex(new Color(0.12f, 0.16f, 0.22f));
            texDim = Tex(new Color(0.02f, 0.025f, 0.04f, 0.88f));
            texWarn = Tex(new Color(0.55f, 0.16f, 0.13f, 0.9f));

            GUIStyle S(int size, Color c, TextAnchor a = TextAnchor.UpperLeft)
            {
                var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = a, richText = true, wordWrap = false };
                s.normal.textColor = c;
                return s;
            }
            var text = new Color(0.86f, 0.89f, 0.93f);
            var dim = new Color(0.50f, 0.55f, 0.62f);
            var amber = new Color(0.95f, 0.80f, 0.40f);
            big = S(20, Color.white);
            label = S(14, text);
            labelDim = S(14, dim);
            small = S(11, dim); small.wordWrap = true;
            cost = S(13, amber, TextAnchor.UpperRight);
            costDim = S(13, dim, TextAnchor.UpperRight);
            head = S(11, dim);
            newsStyle = S(14, text, TextAnchor.MiddleLeft);
            pinStyle = S(13, new Color(0.95f, 0.55f, 0.50f), TextAnchor.MiddleLeft);
            warnStyle = S(15, Color.white, TextAnchor.MiddleCenter);
            titleStyle = S(34, Color.white, TextAnchor.MiddleCenter);
            center = S(15, text, TextAnchor.MiddleCenter); center.wordWrap = true;
            popStyle = S(15, amber, TextAnchor.MiddleCenter);
            unitStyle = S(96, Color.white, TextAnchor.MiddleCenter);
            unitSub = S(16, amber, TextAnchor.MiddleCenter);

            btn = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13, border = new RectOffset(0, 0, 0, 0) };
            btn.normal.background = Tex(new Color(0.08f, 0.105f, 0.15f));
            btn.hover.background = Tex(new Color(0.11f, 0.15f, 0.21f));
            btn.active.background = Tex(new Color(0.15f, 0.2f, 0.28f));
            btn.normal.textColor = btn.hover.textColor = btn.active.textColor = text;
            btnOff = new GUIStyle(btn);
            btnOff.hover.background = btnOff.active.background = btnOff.normal.background;
            btnOff.normal.textColor = btnOff.hover.textColor = btnOff.active.textColor = dim;
            mini = new GUIStyle(btn) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        }

        // ───────────────────────────────── 그리기

        void OnGUI()
        {
            if (sim == null) return;
            EnsureStyles();
            scale = Screen.height / RefH;
            vw = Screen.width / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float left = PanelVisible ? vw - PanelW : vw;
            Popups();
            UnitBreak(left);
            TopBar(left);
            DangerMeter(left);
            CinematicTitle(left);
            News(left);
            BannerDraw(left);
            if (PanelVisible) Panel();
            if (game.timeScale > 1f) GUI.Label(new Rect(12, RefH - 64, 240, 20), "<color=#f0c070>테스트 속도 ×" + game.timeScale + "</color>  (F2)", small);
            if (sim.Finished) EndScreen();
            else if (MenuOpen) Menu();
        }

        void TopBar(float left)
        {
            var S = sim.S;
            float x = 16f;
            void Item(string k, string v, GUIStyle vs = null)
            {
                var kc = new GUIContent(k);
                float kw = labelDim.CalcSize(kc).x;
                GUI.Label(new Rect(x, 14, kw + 4, 24), kc, labelDim);
                var vc = new GUIContent(v);
                var st = vs ?? big;
                float w = st.CalcSize(vc).x;
                GUI.Label(new Rect(x + kw + 6, 9, w + 4, 30), vc, st);
                x += kw + 6 + w + 22;
            }

            // 🔴 표시 자체가 없다가 생긴다
            if (game.seenCollect && !sim.Has("autosell")) Item("회수", KNum.Fmt(S.held));
            if (PanelVisible)
            {
                float x0 = x;
                big.fontSize = 20 + Mathf.RoundToInt(game.fx != null ? game.fx.creditPulse * 6f : 0f);
                Item("크레딧", KNum.Fmt(shownCredits));
                big.fontSize = 20;
                CreditAnchorScreen = new Vector2((x0 + 90f) * scale, Screen.height - 24f * scale);
            }
            if (S.bought > 0)
            {
                double inc = sim.IncomeRate > 0 ? sim.IncomeRate : sim.CollectIncome;
                if (inc > 0) Item("", "+" + KNum.Fmt(inc) + "/초", label);
                Item("궤도 파편", KNum.Fmt(sim.TotalD) + "개");
            }
            // 시가총액은 한 줄 아래 — 위 띠에 넣으면 시간과 겹쳤다 (09-21 캡처)
            if (sim.Has("cap")) GUI.Label(new Rect(16, 38, 400, 20), "시가총액  <color=#dfe4ea>" + KNum.Fmt(sim.CompanyValue) + "</color>", labelDim);

            if (PanelVisible && x < left - 80)
            {
                int m = (int)(S.t / 60), s = (int)(S.t % 60);
                GUI.Label(new Rect(left - 70, 14, 60, 20), $"{m:00}:{s:00}", costDim);
            }
        }

        void Popups()
        {
            var fx = game.fx;
            var cam = Camera.main;
            if (fx == null || cam == null) return;
            foreach (var p in fx.popups)
            {
                Vector3 sp = cam.WorldToScreenPoint(p.world);
                float k = p.age / p.life;
                float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale - k * 30f;
                popStyle.fontSize = Mathf.RoundToInt(p.size * (k < 0.12f ? 1f + (0.12f - k) * 4f : 1f));
                var c = p.c; c.a = 1f - k * k;
                popStyle.normal.textColor = c;
                GUI.Label(new Rect(gx - 100, gy - 14, 200, 28), p.text, popStyle);
            }
        }

        /// <summary>🔴 2막 후반 — 궤도 위험도. 「뭔가 온다」가 화면에 있어야 한다 (09-21: 끝난 줄 알았다).</summary>
        void DangerMeter(float left)
        {
            if (sim.S.act != 2) return;
            double d = sim.Danger;
            if (d < 0.15) return;
            string word = d < 0.35 ? "안전" : d < 0.6 ? "주의" : d < 0.8 ? "경고" : "임계";
            Color col = d < 0.35 ? new Color(0.5f, 0.85f, 0.6f) : d < 0.6 ? new Color(0.95f, 0.8f, 0.4f) : new Color(0.95f, 0.4f, 0.35f);
            bool blink = d >= 0.8f && Mathf.Sin(Time.time * 8f) > 0f;
            float w = 200f, x = left / 2f - w / 2f, y = 88f;   // 60 은 튀는 숫자 · 시가총액과 겹쳤다
            GUI.Label(new Rect(x, y - 20, 120, 18), "궤도 위험도", head);
            var ws = new GUIStyle(head) { alignment = TextAnchor.UpperRight };
            ws.normal.textColor = col;
            GUI.Label(new Rect(x, y - 20, w, 18), word, ws);
            GUI.DrawTexture(new Rect(x, y, w, 6), texBarBg);
            var c = GUI.color;
            GUI.color = blink ? Color.white : col;
            GUI.DrawTexture(new Rect(x, y, w * (float)d, 6), Texture2D.whiteTexture);
            GUI.color = c;
        }

        void CinematicTitle(float left)
        {
            float c = game.cinematic;
            if (c < 0.8f) return;
            float alpha = c < 1.3f ? (c - 0.8f) * 2f : c > OrbitGame.CinematicLen - 0.8f ? (OrbitGame.CinematicLen - c) / 0.8f : 1f;
            float punch = c < 1.1f ? 1f + (1.1f - c) * 1.5f : 1f;
            var col = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * 0.75f);
            GUI.DrawTexture(new Rect(0, 185, left, 140), texDim);          // 폭발에 글자가 묻혔다 — 뒤에 띠를 깐다
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            unitStyle.fontSize = Mathf.RoundToInt(64 * punch);
            unitStyle.normal.textColor = new Color(1f, 0.55f, 0.45f);
            GUI.Label(new Rect(left / 2 - 260, 190, 520, 100), "연쇄 충돌", unitStyle);
            unitStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(left / 2 - 260, 285, 520, 30), "파편이 파편을 낳는다 · 멈출 수 없다", unitSub);
            GUI.color = col;
        }

        void UnitBreak(float left)
        {
            float age = Time.time - unitAt;
            if (age > 2.8f || string.IsNullOrEmpty(unitName)) return;
            float punch = age < 0.3f ? 1f + (0.3f - age) * 2f : 1f;
            float alpha = age < 2f ? 1f : 1f - (age - 2f) / 0.8f;
            var col = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            unitStyle.fontSize = Mathf.RoundToInt(96 * punch);
            GUI.Label(new Rect(left / 2 - 200, 170, 400, 130), unitName, unitStyle);
            GUI.Label(new Rect(left / 2 - 200, 290, 400, 30), "크레딧 " + unitName + " 단위", unitSub);
            GUI.color = col;
        }

        void News(float left)
        {
            var S = sim.S;
            if (string.IsNullOrEmpty(S.newsLine)) return;
            float y = RefH - 34f;
            GUI.DrawTexture(new Rect(0, y, left, 34), texPanel);
            GUI.DrawTexture(new Rect(0, y, left, 1), texLine);
            float fade = Mathf.Clamp01((float)(S.t - S.newsAt) * 2f);
            var c = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Max(0.2f, fade));
            GUI.Label(new Rect(14, y, 40, 34), "뉴스", head);
            GUI.Label(new Rect(52, y, left - 60, 34), S.newsLine, newsStyle);
            GUI.color = c;

            // 🔴 저궤도가 막힌 줄은 남은 3막 내내 화면에 그대로 있다. 치우지 않는다 (wiki 4회차)
            if (!string.IsNullOrEmpty(S.pinned) && S.pinned != S.newsLine)
            {
                GUI.DrawTexture(new Rect(0, y - 26, left, 26), texPanel);
                GUI.Label(new Rect(52, y - 26, left - 60, 26), S.pinned, pinStyle);
            }
        }

        void BannerDraw(float left)
        {
            if (Time.time > bannerUntil || string.IsNullOrEmpty(banner)) return;
            bool warn = banner.StartsWith("⚠") || banner.Contains("봉쇄");
            var r = new Rect(left / 2 - 200, 52, 400, 34);
            GUI.DrawTexture(r, warn ? texWarn : texCard);
            GUI.Label(r, banner, warnStyle);
        }

        void Panel()
        {
            var S = sim.S;
            var area = new Rect(vw - PanelW, 0, PanelW, RefH);
            GUI.DrawTexture(area, texPanel);
            GUI.DrawTexture(new Rect(area.x, 0, 1, RefH), texLine);

            GUILayout.BeginArea(new Rect(area.x + 12, 48, PanelW - 18, RefH - 56));
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            float w = PanelW - 36;

            // ── 판매 · 드론
            if (!sim.Has("autosell"))
            {
                string r = S.held > 0 ? KNum.Fmt(S.held) + "개 → " + KNum.Fmt(S.heldValue) : "없음";
                if (Row(w, "판매", null, r, S.held > 0)) sim.Sell();
            }
            if (S.earned > 0 || S.bought > 0)
            {
                string name = S.bought > 0 ? "수거 드론  ×" + sim.FleetTotal : "수거 드론";
                if (Row(w, name, S.bought == 0 ? "파편을 알아서 줍는다" : null, KNum.Fmt(sim.DronePrice), S.credits >= sim.DronePrice && S.ending == 0))
                    sim.BuyDrone();
            }
            if (sim.Has("manager"))
            {
                if (Row(w, "관리자 — 드론 자동 구매", null, S.autoBuy ? "켜짐" : "꺼짐", true)) S.autoBuy = !S.autoBuy;
            }

            // ── 함대 배치 (2막부터)
            if (sim.Has("meo")) Fleet(w);

            // ── 계약
            if (S.contract.active) ContractCard(w);

            // ── 해금
            var list = OrbitSim.Unlocks.Where(u => sim.Visible(u)).OrderBy(u => u.ending ? 1 : 0).ThenBy(u => u.cost(sim)).ToList();
            if (list.Count > 0)
            {
                bool endings = list.Any(u => u.ending);
                GUILayout.Space(6);
                GUILayout.Label(endings ? "마지막 기술 — 하나만 살 수 있다" : "해금", head);
                foreach (var u in list)
                {
                    if (seenUnlock.Add(u.id)) newSince[u.id] = Time.time;
                    string price = u.id == "end_net" ? "전 재산" : KNum.Fmt(u.cost(sim));
                    if (Row(w, u.name, u.desc, price, sim.CanBuy(u), newSince.TryGetValue(u.id, out var ns) ? Time.time - ns : 99f))
                        sim.Buy(u.id);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>한 줄짜리 단추. 이름 · 오른쪽 값 · 아래 설명. 새로 생긴 건 잠깐 밝다.</summary>
        bool Row(float w, string name, string desc, string right, bool enabled, float age = 99f)
        {
            float descH = string.IsNullOrEmpty(desc) ? 0f : small.CalcHeight(new GUIContent(desc), w - 24);
            float h = 34f + (descH > 0 ? descH + 2f : 0f);
            Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            bool click = GUI.Button(r, GUIContent.none, enabled ? btn : btnOff) && enabled;
            if (age < 1.2f)
            {
                var c = GUI.color; GUI.color = new Color(1f, 0.85f, 0.45f, (1.2f - age) / 1.2f * 0.12f);
                GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = c;
            }
            GUI.Label(new Rect(r.x + 12, r.y + 9, w - 110, 20), name, enabled ? label : labelDim);
            if (right != null) GUI.Label(new Rect(r.x + 12, r.y + 10, w - 24, 20), right, enabled ? cost : costDim);
            if (descH > 0) GUI.Label(new Rect(r.x + 12, r.y + 30, w - 24, descH), desc, small);
            GUILayout.Space(6);
            return click;
        }

        void Fleet(float w)
        {
            var S = sim.S;
            GUILayout.Space(4);
            GUILayout.Label("함대 배치", head);
            for (int i = 0; i < 3; i++)
            {
                var o = S.orbits[i];
                if (!o.open) continue;
                Rect r = GUILayoutUtility.GetRect(w, 34, GUILayout.Width(w), GUILayout.Height(34));
                GUI.DrawTexture(r, texCard);
                string name = (o.warned && !o.locked ? "<color=#f08070>⚠ </color>" : "") + OrbitSim.Names[i];
                GUI.Label(new Rect(r.x + 10, r.y + 9, 130, 20), name, label);
                if (o.locked)
                {
                    GUI.Label(new Rect(r.x + 10, r.y + 10, w - 20, 20), "<color=#e0564a>봉쇄</color>", cost);
                }
                else
                {
                    GUI.Label(new Rect(r.x + 74, r.y + 11, 50, 20), o.claimed ? "<color=#d9c9a0>채굴권</color>" : DensityWord(i), small);
                    GUI.Label(new Rect(r.x + 100, r.y + 9, w - 212, 20), o.drones + "대", cost);
                    if (GUI.Button(new Rect(r.x + w - 102, r.y + 5, 46, 24), "전부", mini)) S_Move(i, false);
                    if (GUI.Button(new Rect(r.x + w - 52, r.y + 5, 46, 24), "절반", mini)) S_Move(i, true);
                }
                GUILayout.Space(4);
            }
            int moving = sim.InTransit;
            if (moving > 0) GUILayout.Label($"이동 중 {moving}대 — 도착까지 줍지 못한다", small);
            GUILayout.Space(4);
        }

        void S_Move(int i, bool half) => sim.Move(i, half);

        string DensityWord(int i)
        {
            double d = sim.S.orbits[i].D / sim.S.orbits[i].D0;
            if (d < 0.35) return "휑함";
            if (d < 0.9) return "보통";
            if (d < 2) return "빽빽";
            return "<color=#f0c070>포화</color>";
        }

        void ContractCard(float w)
        {
            var c = sim.S.contract;
            GUILayout.Space(4);
            float h = c.accepted ? 72 : 96;
            Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            GUI.DrawTexture(r, texCard);
            float pulse = c.accepted ? 0.3f : 0.4f + 0.4f * Mathf.Sin(Time.time * 3f);
            var col = GUI.color; GUI.color = new Color(0.95f, 0.78f, 0.36f, pulse);
            GUI.DrawTexture(new Rect(r.x, r.y, 2, h), Texture2D.whiteTexture); GUI.color = col;

            GUI.Label(new Rect(r.x + 12, r.y + 6, w, 16), "계약", head);
            string what = c.kind == 1
                ? "발사 대행 — " + OrbitSim.Names[c.orbit]
                : OrbitSim.Names[c.orbit] + " 잔해 " + KNum.Fmt(c.target) + "개 · " + (int)(c.accepted ? c.timeLeft : 150) + "초 안에";
            GUI.Label(new Rect(r.x + 12, r.y + 22, w - 20, 20), what, label);
            string pay = "보상 " + KNum.Fmt(c.reward) + (c.kind == 1 ? " · 파편 +" + KNum.Fmt(c.debris) : "");
            GUI.Label(new Rect(r.x + 12, r.y + 42, w - 20, 20), pay, label);

            if (c.accepted)
            {
                float k = Mathf.Clamp01((float)(c.progress / Mathf.Max(1f, (float)c.target)));
                GUI.DrawTexture(new Rect(r.x + 12, r.y + 62, w - 24, 4), texBarBg);
                GUI.DrawTexture(new Rect(r.x + 12, r.y + 62, (w - 24) * k, 4), texBar);
            }
            else
            {
                if (GUI.Button(new Rect(r.x + 12, r.y + 66, 70, 24), "수락", mini)) sim.AcceptContract();
                if (GUI.Button(new Rect(r.x + 88, r.y + 66, 70, 24), "거절", mini)) sim.DeclineContract();
                GUI.Label(new Rect(r.x + 12, r.y + 70, w - 24, 20), (int)c.offerLeft + "초", costDim);
            }
            GUILayout.Space(6);
        }

        void EndScreen()
        {
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            float cx = vw / 2f;
            GUI.Label(new Rect(cx - 300, 70, 600, 50), OrbitSim.EndingTitle(S.ending), titleStyle);
            var lines = OrbitSim.EndingLines(S.ending);
            GUI.Label(new Rect(cx - 300, 130, 600, 24), lines[0], center);
            GUI.Label(new Rect(cx - 300, 156, 600, 24), lines[1], center);

            // 🔴 글로 나무라지 않는다. 숫자 두 줄이면 된다 (wiki 3회차)
            int m = (int)(S.t / 60), s = (int)(S.t % 60);
            var rows = new List<(string, string)>
            {
                ("조업 시간", $"{m}분 {s}초"),
                ("최고 크레딧", KNum.Fmt(S.peak)),
                ("잃은 드론", S.dronesLost + "대"),
                ("당신이 만든 파편", KNum.Fmt(S.made) + "개"),
                ("그중 안 만들어도 됐던 것", KNum.Fmt(S.avoidable) + "개"),
            };
            float y = 220;
            foreach (var (k, v) in rows)
            {
                GUI.Label(new Rect(cx - 200, y, 220, 26), k, labelDim);
                GUI.Label(new Rect(cx - 200, y, 400, 26), v, cost);
                y += 30;
            }
            if (GUI.Button(new Rect(cx - 80, y + 30, 160, 40), "처음부터", mini)) game.Restart();
        }

        void Menu()
        {
            float cx = vw / 2f;
            var r = new Rect(cx - 150, 170, 300, 210);
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            GUI.DrawTexture(r, texCard);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 24), "멈춤", center);
            if (GUI.Button(new Rect(r.x + 50, r.y + 56, 200, 34), "계속", mini)) ToggleMenu();
            if (GUI.Button(new Rect(r.x + 50, r.y + 98, 200, 34), confirmRestart ? "정말? 한 번 더 누르면 지워진다" : "처음부터 다시", mini))
            {
                if (confirmRestart) game.Restart(); else confirmRestart = true;
            }
            if (GUI.Button(new Rect(r.x + 50, r.y + 140, 200, 30), "속도 ×" + game.timeScale + "  (F2)", mini)) game.CycleSpeed();
            GUI.Label(new Rect(r.x, r.y + 176, r.width, 20), "진행은 5초마다 저장된다 · Esc", small);
        }
    }
}
