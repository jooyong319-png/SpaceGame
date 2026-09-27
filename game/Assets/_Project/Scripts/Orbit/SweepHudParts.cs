using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🔩 부품 가게 · 🔑 열쇠 · ◆ 핵심 칸 (09-24 설계서 3단계).
    /// 정비고 왼쪽 위 [부품 가게] → 창: 위에 청소선 부품 칸 다섯, 아래 진열 셋 (출동마다 바뀜) · 새로고침. 산 부품은 제 칸으로 날아가 끼워진다.
    /// </summary>
    public partial class SweepHud
    {
        public bool partsOpen;
        static readonly Color[] RarCol = { new Color(0.72f, 0.76f, 0.82f), new Color(0.44f, 0.83f, 0.91f), new Color(0.8f, 0.45f, 1f), new Color(1f, 0.8f, 0.35f) };   // 일반 · 희귀 · 영웅(진보라 — 열쇠의 옅은 보라와 다르게) · 전설
        static readonly Color KeyCol = new Color(0.71f, 0.61f, 1f);
        struct FlyCard { public Rect a, b; public float t0; public Color c; public string txt; public int id, slot; }
        // 🎨 부품 칸 색 · 모양 — 카드 띠 · 청소선 점 · 목록이 같은 색 (09-25)
        static readonly Color[] SlotCol = { new Color(1f, 0.6f, 0.29f), new Color(1f, 0.44f, 0.49f), new Color(0.56f, 0.69f, 0.85f), new Color(0.49f, 0.88f, 0.54f), new Color(1f, 0.56f, 0.82f) };
        static readonly string[] SlotMark = { "▼", "▲", "■", "◆", "●" };
        static readonly Vector2[] SlotAt = { new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.07f), new Vector2(0.26f, 0.81f), new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.66f) };   // hull.png 위 자리 — 꼬리 · 코 · 날개 · 창 · 해치
        static readonly Vector2[] SlotTag = { new Vector2(0.66f, 0.95f), new Vector2(0.66f, 0.05f), new Vector2(0.1f, 0.94f), new Vector2(0.72f, 0.33f), new Vector2(0.71f, 0.62f) };
        static readonly string[] RarStar = { "◇", "◆", "◆◆", "◆◆◆" };
        static Texture2D shipTex; int shopHot = -1; readonly float[] slotFlash = { -9, -9, -9, -9, -9 }; Rect consRect;
        public int testShopHot = -1;                                         // 에디터 시험용 — 깜빡일 칸 강제로
        readonly List<FlyCard> flyCards = new List<FlyCard>();
        readonly Rect[] slotRects = new Rect[5];
        static readonly System.Collections.Generic.Dictionary<int, Texture2D> itemTex = new System.Collections.Generic.Dictionary<int, Texture2D>();
        static Texture2D ItemTex(int id)                                     // 🖼 가게 물건 도트 (픽셀랩) — shop/part_N · cons_N · key
        {
            if (itemTex.TryGetValue(id, out var t)) return t;
            string n = id == Parts.Key ? "shop/key" : SweepSim.IsCons(id) ? "shop/cons_" + (id - SweepSim.Cons0) : "shop/part_" + id;
            t = Resources.Load<Texture2D>(n); itemTex[id] = t; return t;
        }

        /// <summary>정비고 왼쪽 위 — [부품 가게] · 열쇠 수</summary>
        void PartsButton(Rect r)
        {
            var S = sim.S;
            if (S.keys > 0 || sim.ShopOpen)
            {
                var kr = new Rect(r.x, r.y, 70, r.height);
                GUI.color = new Color(0.16f, 0.12f, 0.24f); GUI.DrawTexture(kr, white); Frame(kr, KeyCol, 1);
                GUI.Label(kr, Loc.T("<size=12><color=#d8ccff>열쇠 <b>") + S.keys + "</b></color></size>", center);
                GUI.color = Color.white;
            }
            if (!sim.ShopOpen) return;
            var b = new Rect(r.x + 76, r.y, 150, r.height);
            bool ov = b.Contains(Event.current.mousePosition);
            GUI.color = ov ? new Color(0.26f, 0.2f, 0.1f) : new Color(0.18f, 0.14f, 0.08f); GUI.DrawTexture(b, white); Frame(b, SweepGame.Amber, ov ? 2 : 1);
            int filled = 0; foreach (var p in S.parts) if (p >= 0) filled++;
            GUI.Label(b, Loc.T("<size=12><color=#ffdf95>부품 가게 ▸</color>  <color=#8a9bb3>") + filled + "/5</color></size>", center);
            GUI.color = Color.white;
            if (GUI.Button(b, GUIContent.none, GUIStyle.none)) { GoFlow(5); OrbitSfx.Play("tick", 0.6f); }
        }

        // 🏪 부품 가게 방 — 정비고 오른쪽 (09-24 사장님 24번 · 시안 https://claude.ai/artifact/CthkM2c8KDnFcaxG8m5zJt)
        // 🛒 구입 확인 — 「다음부터 묻지 않고 바로 구매」 체크박스 (09-27). 설정에서 되돌린다
        int buyAsk = -1; Rect buyAskR; bool buyAskChk;
        public void TestBuyAsk(int k) { buyAsk = k; buyAskChk = true; }   // 에디터 캡처용
        void ShopBuy(int k, Rect r, Rect w)
        {
            var S = sim.S; if (S.shop == null || k < 0 || k >= S.shop.Count) return;
            int id = S.shop[k]; bool key = id == Parts.Key, cn = SweepSim.IsCons(id);
            int slot = key || cn ? -1 : Parts.Defs[id].slot;
            Color rc = key ? KeyCol : cn ? new Color(0.44f, 0.81f, 0.59f) : RarCol[Parts.Defs[id].rar];
            string nm = key ? Loc.T("양자 열쇠") : cn ? SweepSim.ConsName[id - SweepSim.Cons0] : Parts.Defs[id].name;
            var to = key ? new Rect(w.xMax - 120, w.y + 6, 110, 24) : cn ? consRect : slotRects[slot];
            string fl = key ? Loc.T("열쇠 +1") : nm;
            if (sim.BuyPart(k))
            {
                flyCards.Add(new FlyCard { a = new Rect(r.x + 6, r.y + 44, 52, 52), b = to, t0 = Time.unscaledTime, c = slot >= 0 ? SlotCol[slot] : rc, txt = fl, id = id, slot = slot });
                OrbitSfx.Play(key ? "launch" : "buy", key ? 0.5f : 0.8f);
            }
        }
        void BuyAskWin(Rect w)
        {
            var S = sim.S;
            if (S.shop == null || buyAsk >= S.shop.Count) { buyAsk = -1; return; }
            int id = S.shop[buyAsk]; bool key = id == Parts.Key, cn = SweepSim.IsCons(id);
            string nm = key ? Loc.T("양자 열쇠") : cn ? SweepSim.ConsName[id - SweepSim.Cons0] : Parts.Defs[id].name;
            double price = sim.ShelfPrice(buyAsk); bool can = S.cash >= price;
            GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            var m = new Rect(w.center.x - 200, w.center.y - 90, 400, 180);
            GUI.color = new Color(0.043f, 0.063f, 0.09f, 0.98f); GUI.DrawTexture(m, white); Frame(m, SweepGame.Amber, 2); GUI.color = Color.white;
            GUI.Label(new Rect(m.x, m.y + 18, m.width, 26), "<size=17><b>" + nm + "</b></size>", center);
            GUI.Label(new Rect(m.x, m.y + 46, m.width, 22), Loc.T("<size=14>구입하시겠습니까? · <color=#ffdf95>") + KNum.Fmt(price) + "</color></size>", center);
            var cb = new Rect(m.x + 70, m.y + 84, 18, 18);
            GUI.color = new Color(0.1f, 0.13f, 0.18f); GUI.DrawTexture(cb, white); Frame(cb, SweepGame.Amber, 1); GUI.color = Color.white;
            if (buyAskChk) { GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(cb.x + 4, cb.y + 4, 10, 10), white); GUI.color = Color.white; }
            GUI.Label(new Rect(cb.xMax + 8, cb.y - 2, 260, 22), Loc.T("<size=13>다음부터 묻지 않고 바로 구매</size>"), label);
            if (GUI.Button(new Rect(cb.x - 4, cb.y - 4, 290, 26), GUIContent.none, GUIStyle.none)) { buyAskChk = !buyAskChk; OrbitSfx.Play("tick", 0.5f); }
            if (GUI.Button(new Rect(m.x + 60, m.yMax - 52, 130, 36), can ? Loc.T("<size=15>구입</size>") : Loc.T("<size=12>돈 모자람</size>"), can ? btnC : btnOffC) && can)
            {
                if (buyAskChk) { PlayerPrefs.SetInt("orbit.shopQuick", 1); PlayerPrefs.Save(); }
                int k = buyAsk; buyAsk = -1; ShopBuy(k, buyAskR, w);
            }
            if (GUI.Button(new Rect(m.xMax - 190, m.yMax - 52, 130, 36), Loc.T("<size=15>취소</size>"), btnOffC)) { buyAsk = -1; OrbitSfx.Play("tick", 0.4f); }
        }
        void ShopRoom()
        {
            var S = sim.S;
            bool askOpen = buyAsk >= 0; if (askOpen) GUI.enabled = false;                // 확인 창이 떠 있으면 뒤 단추는 막는다
            GUI.color = Th.room; GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);   // 🎨 배 테마
            GUI.color = Th.scan; for (float yy = 0; yy < RefH; yy += 4) GUI.DrawTexture(new Rect(0, yy, vw, 1), white);
            GUI.color = Color.white;
            var w = new Rect(ox + 50, 14, 860, 572);
            GUI.Label(new Rect(w.x, w.y, 900, 34), Loc.T("<size=24><b><color=#ffdf95>부품 가게</color></b></size>  <size=12><color=#8a9bb3>진열은 출동하고 오면 바뀐다 · 카드에 올리면 끼울 자리가 깜빡인다</color></size>"), label);
            GUI.Label(new Rect(w.x, w.y + 6, w.width, 24), Loc.T("<size=14><color=#8a9bb3>돈</color> <color=#ffdf95>") + KNum.Fmt(S.cash) + Loc.T("</color>   <color=#d8ccff>열쇠 ") + S.keys + "</color></size>", cost);
            // 🚀 내 청소선 — 그림 위에 부품 칸 다섯 (09-25 사장님 「어디에 들어가는지 이해가 어렵다」 · 시안 https://claude.ai/artifact/6bRbq2eUNE6dXqsSgqvHND)
            if (shipTex == null) shipTex = Resources.Load<Texture2D>("ship/hull");
            var L = new Rect(w.x, w.y + 60, 250, 162);
            GUI.Label(new Rect(L.x, L.y - 22, L.width, 20), Loc.T("<size=11><color=#8a9bb3>내 청소선 · 부품 칸 다섯</color></size>"), label);
            GUI.color = new Color(0.04f, 0.05f, 0.075f); GUI.DrawTexture(L, white); Frame(L, new Color(0.15f, 0.18f, 0.23f), 1);
            var hr = new Rect(L.x + 5, L.y + 4, 240, 150);
            if (shipTex != null) { GUI.color = new Color(1, 1, 1, 0.92f); GUI.DrawTexture(hr, shipTex); }
            int hotNow = testShopHot >= 0 ? testShopHot : shopHot; shopHot = -1;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8);
            for (int i = 0; i < 5; i++)
            {
                bool has = S.parts != null && i < S.parts.Length && S.parts[i] >= 0, hot = hotNow == i; var c = SlotCol[i];
                for (int q = 0; q < (i == 2 ? 2 : 1); q++)
                {
                    var sp = new Vector2(hr.x + hr.width * (i == 2 ? (q == 0 ? 0.26f : 0.74f) : SlotAt[i].x), hr.y + hr.height * SlotAt[i].y);
                    float s = hot ? 30 + 6 * pulse : 24; var sr = new Rect(sp.x - s / 2, sp.y - s / 2, s, s);
                    if (q == 0) slotRects[i] = sr;
                    if (hot) { GUI.color = new Color(c.r, c.g, c.b, 0.25f + 0.2f * pulse); GUI.DrawTexture(new Rect(sr.x - 8, sr.y - 8, sr.width + 16, sr.height + 16), texDisc); }
                    GUI.color = new Color(c.r * 0.2f, c.g * 0.2f, c.b * 0.2f, has ? 0.85f : 0.45f); GUI.DrawTexture(sr, white);
                    if (has || hot) Frame(sr, c, hot ? 2.5f : 2); else DashFrame(sr, c);
                    GUI.color = Color.white; GUI.Label(sr, "<size=" + (hot ? 15 : 12) + "><b><color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + SlotMark[i] + "</color></b></size>", center);
                }
                var tp = new Vector2(hr.x + hr.width * SlotTag[i].x, hr.y + hr.height * SlotTag[i].y);
                GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.82f); GUI.DrawTexture(new Rect(tp.x - Parts.SlotName[i].Length * 6 - 5, tp.y - 8, Parts.SlotName[i].Length * 12 + 10, 16), white); GUI.color = Color.white;   // 배 그림 위에서도 읽히게
                GUI.Label(new Rect(tp.x - 40, tp.y - 8, 80, 16), "<size=10><b><color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + Parts.SlotName[i] + "</color></b></size>", center);
            }
            // 칸 목록 — 색 띠 · 모양 · 지금 끼운 것
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(L.x, L.yMax + 8 + i * 43, L.width, 38); var c = SlotCol[i];
                int id = S.parts != null && i < S.parts.Length ? S.parts[i] : -1; bool hot = hotNow == i;
                float fl = Mathf.Clamp01(1 - (Time.unscaledTime - slotFlash[i]) / 0.6f);
                GUI.color = Color.Lerp(hot ? new Color(c.r * 0.12f + 0.06f, c.g * 0.12f + 0.07f, c.b * 0.12f + 0.09f) : new Color(0.08f, 0.1f, 0.14f), new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f), fl); GUI.DrawTexture(r, white);
                Frame(r, hot ? c : new Color(0.15f, 0.18f, 0.23f), hot ? 2 : 1);
                GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), white); GUI.color = Color.white;
                var ic = new Rect(r.x + 8, r.y + 3, 32, 32);
                if (id >= 0 && ItemTex(id) != null) GUI.DrawTexture(ic, ItemTex(id)); else DashFrame(ic, new Color(0.25f, 0.28f, 0.33f));
                string chx = ColorUtility.ToHtmlStringRGB(c);
                string slotTx = "<size=10><b><color=#" + chx + ">" + SlotMark[i] + " " + Parts.SlotName[i] + "</color></b></size>";
                float slotW = label.CalcSize(new GUIContent(slotTx)).x;   // 🌐 칸 이름 폭만큼 부품 이름을 민다 (영어 Launcher 는 길다)
                GUI.Label(new Rect(r.x + 46, r.y - 1, slotW + 4, 20), slotTx, label);
                if (id < 0) { GUI.Label(new Rect(r.x + 46, r.y + 17, r.width - 50, 18), Loc.T("<size=12><color=#3f4652>비어 있음</color></size>"), label); continue; }
                var d = Parts.Defs[id];
                float nmX = Mathf.Max(100, 46 + slotW + 8);
                GUI.Label(new Rect(r.x + nmX, r.y - 1, r.width - nmX - 4, 20), "<size=12><b><color=#" + ColorUtility.ToHtmlStringRGB(RarCol[d.rar]) + ">" + d.name + "</color></b></size>", label);
                GUI.Label(new Rect(r.x + 46, r.y + 17, r.width - 50, 20), "<size=10><color=#aab4c3>" + d.desc + "</color></size>", label);
            }
            // 🧃 다음 판 소모품
            string cons = (S.nFuel > 0 ? Loc.T("연료 +") + S.nFuel + Loc.T("초  ") : "") + (S.nDmg > 0 ? Loc.T("화력 +") + S.nDmg + "%  " : "") + (S.nVal > 0 ? Loc.T("값 +") + S.nVal + "%" : "");
            var tray = new Rect(L.x, L.yMax + 8 + 5 * 43 + 2, L.width, 26); consRect = tray;
            DashFrame(tray, new Color(0.2f, 0.24f, 0.3f));
            GUI.Label(new Rect(tray.x + 8, tray.y + 4, tray.width - 12, 18), Loc.T("<size=11><color=#8a9bb3>다음 출동 때 쓰일 것</color>  ") + (cons.Length > 0 ? "<color=#9ff0bf><b>" + cons + "</b></color>" : Loc.T("<color=#3f4652>없음</color>")) + "</size>", label);

            // 진열 여섯 — 3 × 2, 오른쪽
            var RR = new Rect(w.x + 266, w.y + 60, w.width - 266, 0);
            GUI.Label(new Rect(RR.x, RR.y - 22, RR.width, 20), Loc.T("<size=11><color=#8a9bb3>오늘의 진열</color></size>"), label);   // 설명은 제목 줄로 — 「오늘의 반값」 띠에 가려졌다
            float cw = (RR.width - 2 * 12) / 3, ch2 = 222;
            for (int k = 0; k < 6; k++)
            {
                var r = new Rect(RR.x + (k % 3) * (cw + 12), RR.y + (k / 3) * (ch2 + 12), cw, ch2);
                if (S.shop == null || k >= S.shop.Count)
                {
                    GUI.color = new Color(0.045f, 0.05f, 0.065f); GUI.DrawTexture(r, white); Frame(r, new Color(0.13f, 0.14f, 0.17f), 1); GUI.color = Color.white;
                    GUI.Label(r, Loc.T("<size=13><color=#3f4652>팔렸다</color></size>"), center);
                    continue;
                }
                int id = S.shop[k]; bool key = id == Parts.Key, cn = SweepSim.IsCons(id), sale = k == S.shopSale;
                int slot = key || cn ? -1 : Parts.Defs[id].slot;
                Color rc = key ? KeyCol : cn ? new Color(0.44f, 0.81f, 0.59f) : RarCol[Parts.Defs[id].rar];
                bool ov = r.Contains(Event.current.mousePosition);
                if (ov && slot >= 0) shopHot = slot;
                if (ov || k == testShopTip) { tipK = k; tipR = r; }
                GUI.color = new Color(rc.r * 0.07f + 0.02f, rc.g * 0.07f + 0.025f, rc.b * 0.08f + 0.035f, 1); GUI.DrawTexture(r, white);
                Frame(r, sale ? new Color(1f, 0.36f, 0.3f) : rc, ov ? 3 : 2);
                if (key || !cn && Parts.Defs[id].rar >= 2) { GUI.color = new Color(rc.r, rc.g, rc.b, 0.06f + 0.05f * Mathf.Sin(Time.unscaledTime * 4)); GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), white); }
                // 어디로 가나 — 칸 색 띠
                Color dc = slot >= 0 ? SlotCol[slot] : rc;
                string dt = key ? Loc.T("열쇠 +1 · 핵심 칸 하나") : cn ? Loc.T("다음 출동 한 번용") : SlotMark[slot] + " " + Parts.SlotName[slot] + Loc.T("에 끼움");
                var db = new Rect(r.x + 8, r.y + 8, 0, 20); db.width = Mathf.Min(r.width - 16, label.CalcSize(new GUIContent("<size=12><b>" + dt + "</b></size>")).x + 14);
                GUI.color = new Color(dc.r, dc.g, dc.b, 0.2f); GUI.DrawTexture(db, white); Frame(db, dc, 1);
                GUI.color = Color.white; GUI.Label(new Rect(db.x + 7, db.y + 1, db.width, 18), "<size=12><b><color=#" + ColorUtility.ToHtmlStringRGB(dc) + ">" + dt + "</color></b></size>", label);
                if (!key && !cn) GUI.Label(new Rect(r.x, r.y + 28, r.width - 10, 18), "<size=10><color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">" + RarStar[Parts.Defs[id].rar] + " " + Parts.RarName[Parts.Defs[id].rar] + "</color></size>", cost);
                if (sale) { float sw = Mathf.Max(78, label.CalcSize(new GUIContent(Loc.T("<size=11><b>오늘의 반값</b></size>"))).x + 10); var sr = new Rect(r.xMax - sw - 6, r.y - 9, sw, 17); GUI.color = new Color(0.8f, 0.18f, 0.14f); GUI.DrawTexture(sr, white); GUI.color = Color.white; GUI.Label(sr, Loc.T("<size=11><b>오늘의 반값</b></size>"), center); }
                string nm = key ? Loc.T("양자 열쇠") : cn ? SweepSim.ConsName[id - SweepSim.Cons0] : Parts.Defs[id].name;
                var itx = ItemTex(id);
                if (itx != null) { GUI.color = new Color(1, 1, 1, 0.08f); GUI.DrawTexture(new Rect(r.x + 8, r.y + 46, 48, 48), texDisc); GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + 6, r.y + 44, 52, 52), itx); }
                int nmFs = 15; while (nmFs > 11 && label.CalcSize(new GUIContent("<size=" + nmFs + "><b>" + nm + "</b></size>")).x > r.width - 68) nmFs--;   // 🌐 긴 이름은 글자를 줄인다
                GUI.Label(new Rect(r.x + 62, r.y + 44, r.width - 68, 22), "<size=" + nmFs + "><b><color=#ffffff>" + nm + "</color></b></size>", label);
                string desc = key ? Loc.T("◆ 보라 테두리 핵심 칸 하나를 연다") : cn ? SweepSim.ConsDesc[id - SweepSim.Cons0] : Parts.Defs[id].desc;
                GUI.Label(new Rect(r.x + 62, r.y + 66, r.width - 68, 42), "<size=11><color=#c8d0dc>" + desc + "</color></size>", small);
                // 지금 것 → 바뀌는 것
                GUI.color = new Color(0.2f, 0.23f, 0.29f); GUI.DrawTexture(new Rect(r.x + 10, r.y + 112, r.width - 20, 1), white); GUI.color = Color.white;
                string cmp = key ? Loc.T("<color=#8a9bb3>정비고 ◆ 칸에 쓴다 · 가진 열쇠 ") + S.keys + "</color>" : cn ? Loc.T("<color=#8a9bb3>사 두면 <color=#9ff0bf>다음 출동 때 저절로</color> 쓰인다 · 한 번 쓰면 끝</color>") : PartCompare(id);
                GUI.Label(new Rect(r.x + 10, r.y + 116, r.width - 20, 58), "<size=11>" + cmp + "</size>", small);
                double price = sim.ShelfPrice(k); bool can = S.cash >= price;
                var bb = new Rect(r.x + 10, r.yMax - 40, r.width - 20, 30);
                string ptxt = KNum.Short(price) + (sale ? Loc.T(" <color=#ffb0a0>(반값)</color>") : "");   // 원래 값은 카드 위 「오늘의 반값」 띠가 말해 준다 — 단추가 좁다
                bool clickBtn = GUI.Button(bb, can ? "<size=" + (sale ? 13 : 14) + Loc.T(">구입 · ") + ptxt + "</size>" : "<size=12><color=#ff9b8f>" + ptxt + Loc.T(" — 돈 모자람</color></size>"), can ? btn : btnOff);
                bool clickCard = GUI.Button(r, GUIContent.none, GUIStyle.none);          // 🛒 09-27 사장님 「물건 칸 전체를 클릭하면 구입하시겠습니까?」
                if ((clickBtn || clickCard) && buyAsk < 0)
                {
                    if (!can) OrbitSfx.Play("clank", 0.35f, 0.05f, 0f);
                    else if (PlayerPrefs.GetInt("orbit.shopQuick", 0) == 1) ShopBuy(k, r, w);   // 「다음부터 바로 구매」
                    else { buyAsk = k; buyAskR = r; buyAskChk = false; OrbitSfx.Play("tick", 0.6f); }
                }
            }
            if (tipK >= 0 && S.shop != null && tipK < S.shop.Count && buyAsk < 0) ShopTip(S.shop[tipK], tipR);
            tipK = -1;
            if (askOpen) { GUI.enabled = true; BuyAskWin(w); GUI.enabled = false; }
            // 새로고침 — 판마다 한 번 공짜
            {
                var rb = new Rect(RR.x + RR.width / 2 - 120, w.yMax - 36, 240, 32);
                bool free = S.freeRoll, can = free || S.cash >= sim.RerollPrice;
                if (GUI.Button(rb, Loc.T("<size=13>진열 새로고침 · ") + (free ? Loc.T("<color=#9ff0bf>공짜</color>") : KNum.Fmt(sim.RerollPrice)) + "</size>", can ? btn : btnOff) && can && sim.RerollShop()) OrbitSfx.Play("tick", 0.7f);
            }
            // 날아가는 카드
            float now = Time.unscaledTime;
            for (int i = flyCards.Count - 1; i >= 0; i--)
            {
                var f = flyCards[i]; float k = (now - f.t0) / 0.5f;
                if (k >= 1) { flyCards.RemoveAt(i); if (f.slot >= 0) slotFlash[f.slot] = now; BuyFx(f.b.center, f.c, true, f.slot >= 0 ? Parts.SlotName[f.slot] + Loc.T(" 장착!") : Loc.T("장착!")); continue; }
                float e = k * k * (3 - 2 * k);
                var rr = new Rect(Mathf.Lerp(f.a.x, f.b.x, e), Mathf.Lerp(f.a.y, f.b.y, e) - Mathf.Sin(k * Mathf.PI) * 60, Mathf.Lerp(f.a.width, f.b.width, e), Mathf.Lerp(f.a.height, f.b.height, e));
                var ftx = ItemTex(f.id);
                if (ftx != null) { float sz = Mathf.Lerp(52, 30, e); GUI.color = new Color(f.c.r, f.c.g, f.c.b, 0.35f); GUI.DrawTexture(new Rect(rr.center.x - sz * 0.75f, rr.center.y - sz * 0.75f, sz * 1.5f, sz * 1.5f), texDisc); GUI.color = Color.white; GUI.DrawTexture(new Rect(rr.center.x - sz / 2, rr.center.y - sz / 2, sz, sz), ftx); }   // 부품 그림이 제 자리로 날아간다
                else { GUI.color = new Color(f.c.r, f.c.g, f.c.b, 0.3f); GUI.DrawTexture(rr, white); Frame(rr, f.c, 2); GUI.color = Color.white; GUI.Label(rr, "<size=14><b>" + f.txt + "</b></size>", center); }
            }
        }
            void DashFrame(Rect r, Color c)                                      // 점선 테두리 — 빈 칸
        {
            GUI.color = new Color(c.r, c.g, c.b, 0.7f);
            for (float x = r.x; x < r.xMax; x += 6) { float l = Mathf.Min(3, r.xMax - x); GUI.DrawTexture(new Rect(x, r.y, l, 1), white); GUI.DrawTexture(new Rect(x, r.yMax - 1, l, 1), white); }
            for (float y = r.y; y < r.yMax; y += 6) { float l = Mathf.Min(3, r.yMax - y); GUI.DrawTexture(new Rect(r.x, y, 1, l), white); GUI.DrawTexture(new Rect(r.xMax - 1, y, 1, l), white); }
            GUI.color = Color.white;
        }

        static readonly Dictionary<string, string[]> FxName = new Dictionary<string, string[]>
        {
            { "dmg", new[] { Loc.T("화력"), "%" } }, { "spd", new[] { Loc.T("연사"), "%" } }, { "rad", new[] { Loc.T("크기"), "%" } }, { "fuel", new[] { Loc.T("연료"), Loc.T("초") } }, { "crit", new[] { Loc.T("치명"), "%" } },
            { "dbl", new[] { Loc.T("한 발 더"), "%" } }, { "drone", new[] { Loc.T("드론 몫"), "%" } }, { "val", new[] { Loc.T("모든 값"), "%" } }, { "vault", new[] { Loc.T("금고 위성"), "%" } }, { "att", new[] { Loc.T("부착물"), "%" } },
            { "cut", new[] { Loc.T("빚 갚는 몫"), "%" } }, { "fee0", new[] { Loc.T("수수료 0"), "" } }, { "div", new[] { Loc.T("배당"), "%" } }, { "combo", new[] { Loc.T("연쇄 상한"), "" } }, { "hole", new[] { Loc.T("블랙홀"), "%" } },
        };
        /// <summary>진열 부품을 끼우면 — 지금 부품이 빠지고 무엇이 오르고 내리나</summary>
        string PartCompare(int id)
        {
            var np = Parts.Defs[id]; int cur = sim.S.parts != null ? sim.S.parts[np.slot] : -1;
            var sum = new Dictionary<string, double>(); var order = new List<string>();
            for (int i = 0; i < np.k.Length; i++) { if (!sum.ContainsKey(np.k[i])) { sum[np.k[i]] = 0; order.Add(np.k[i]); } sum[np.k[i]] += np.v[i]; }
            if (cur >= 0) { var cp = Parts.Defs[cur]; for (int i = 0; i < cp.k.Length; i++) { if (!sum.ContainsKey(cp.k[i])) { sum[cp.k[i]] = 0; order.Add(cp.k[i]); } sum[cp.k[i]] -= cp.v[i]; } }
            var outp = new List<string>();
            foreach (var k in order)
            {
                double d = sum[k]; if (System.Math.Abs(d) < 1e-9) continue;
                var fn = FxName.TryGetValue(k, out var f) ? f : new[] { k, "" };
                double shown = k == "fuel" || k == "combo" ? d : d * 100;
                outp.Add("<color=" + (d > 0 ? "#9ff0bf" : "#ff9b8f") + ">" + fn[0] + (k == "fee0" ? (d > 0 ? Loc.T(" 켜짐") : Loc.T(" 꺼짐")) : " " + (d > 0 ? "+" : "−") + System.Math.Abs(shown).ToString("0.##") + fn[1]) + "</color>");
            }
            if (cur == id) return Loc.T("<color=#8a9bb3>지금 끼운 것과 같다</color>");
            string head = cur >= 0 ? Loc.T("<color=#8a9bb3>지금 <color=#8a7f99>") + Parts.Defs[cur].name + Loc.T("</color> 빠짐</color>\n") : Loc.T("<color=#8a9bb3>빈 칸에 끼움</color>\n");
            return head + (outp.Count > 0 ? string.Join(" · ", outp) : Loc.T("<color=#8a9bb3>달라지는 것 없음</color>"));
        }
    
        // 📖 가게 카드 자세히 — 올리면 옆에 (09-26 사장님 「설명이 더 자세했으면」)
        int tipK = -1; Rect tipR; public int testShopTip = -1;          // 에디터 시험용
        void ShopTip(int id, Rect card)
        {
            var sb = new System.Text.StringBuilder();
            if (id == Parts.Key) sb.Append(Loc.T("<color=#d8ccff>양자 열쇠</color>") + "\n\n" + Loc.T("정비고에서 보라 테두리 ◆ 핵심 칸을 하나 연다. 핵심 칸은 돈만으로는 못 산다.") + "\n" + Loc.T("가진 열쇠 ") + sim.S.keys + Loc.T("개"));
            else if (SweepSim.IsCons(id)) sb.Append("<color=#9ff0bf>" + SweepSim.ConsName[id - SweepSim.Cons0] + Loc.T("</color>  <color=#8a93a3>다음 출동 한 번용</color>") + "\n\n" + SweepSim.ConsHelp[id - SweepSim.Cons0] + "\n\n" + Loc.T("<color=#8a93a3>칸에 끼우지 않는다. 사 두면 다음 출동을 시작할 때 저절로 쓰이고 사라진다.</color>"));
            else
            {
                var d = Parts.Defs[id];
                sb.Append("<color=#" + ColorUtility.ToHtmlStringRGB(RarCol[d.rar]) + ">" + d.name + "</color>  <color=#8a93a3>" + Parts.RarName[d.rar] + " · " + Parts.SlotName[d.slot] + Loc.T(" 칸</color>") + "\n\n");
                for (int i = 0; i < d.k.Length; i++)
                {
                    string v = d.k[i] == "fuel" ? (d.v[i] > 0 ? "+" : "") + d.v[i] + Loc.T("초") : d.k[i] == "fee0" ? "" : d.k[i] == "combo" ? "+" + d.v[i] : (d.v[i] > 0 ? "+" : "") + (d.v[i] * 100).ToString(d.v[i] < 0.01 ? "0.0" : "0") + "%";
                    string h = Parts.Help.TryGetValue(d.k[i], out var hh) ? hh : d.k[i];
                    if (d.k[i] == "crit") h += Loc.T(" (지금 ×") + sim.CritX + ")";
                    sb.Append("<color=#ffdf95>" + v + "</color>  " + h + "\n");
                }
                sb.Append("\n" + "<color=#8a93a3>" + Parts.SlotName[d.slot] + Loc.T(" 칸에 하나만 끼운다. 이미 끼운 게 있으면 바뀐다 — 빠지는 것은 사라진다.</color>"));
            }
            if (tipStyle == null) tipStyle = new GUIStyle(small) { wordWrap = true, richText = true, fontSize = 12 };
            float tw = 300, th = tipStyle.CalcHeight(new GUIContent(sb.ToString()), tw - 24) + 22;
            var tr = new Rect(card.xMax + 8, card.y, tw, th);
            if (tr.xMax > vw - 8) tr.x = card.x - tw - 8;
            tr.y = Mathf.Min(tr.y, RefH - th - 8);
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.97f); GUI.DrawTexture(tr, white); Frame(tr, new Color(0.45f, 0.5f, 0.6f), 1); GUI.color = Color.white;
            GUI.Label(new Rect(tr.x + 12, tr.y + 11, tw - 24, th - 16), sb.ToString(), tipStyle);
        }
        GUIStyle tipStyle;
    }
}
