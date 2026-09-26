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
        static readonly Color[] RarCol = { new Color(0.72f, 0.76f, 0.82f), new Color(0.44f, 0.83f, 0.91f), new Color(1f, 0.8f, 0.35f) };
        static readonly Color KeyCol = new Color(0.71f, 0.61f, 1f);
        struct FlyCard { public Rect a, b; public float t0; public Color c; public string txt; public int id, slot; }
        // 🎨 부품 칸 색 · 모양 — 카드 띠 · 청소선 점 · 목록이 같은 색 (09-25)
        static readonly Color[] SlotCol = { new Color(1f, 0.6f, 0.29f), new Color(1f, 0.44f, 0.49f), new Color(0.56f, 0.69f, 0.85f), new Color(0.49f, 0.88f, 0.54f), new Color(1f, 0.56f, 0.82f) };
        static readonly string[] SlotMark = { "▼", "▲", "■", "◆", "●" };
        static readonly Vector2[] SlotAt = { new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.07f), new Vector2(0.26f, 0.81f), new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.66f) };   // hull.png 위 자리 — 꼬리 · 코 · 날개 · 창 · 해치
        static readonly Vector2[] SlotTag = { new Vector2(0.66f, 0.95f), new Vector2(0.66f, 0.05f), new Vector2(0.1f, 0.94f), new Vector2(0.72f, 0.33f), new Vector2(0.71f, 0.62f) };
        static readonly string[] RarStar = { "◇", "◆", "◆◆" };
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
                GUI.Label(kr, "<size=12><color=#d8ccff>열쇠 <b>" + S.keys + "</b></color></size>", center);
                GUI.color = Color.white;
            }
            if (!sim.ShopOpen) return;
            var b = new Rect(r.x + 76, r.y, 150, r.height);
            bool ov = b.Contains(Event.current.mousePosition);
            GUI.color = ov ? new Color(0.26f, 0.2f, 0.1f) : new Color(0.18f, 0.14f, 0.08f); GUI.DrawTexture(b, white); Frame(b, SweepGame.Amber, ov ? 2 : 1);
            int filled = 0; foreach (var p in S.parts) if (p >= 0) filled++;
            GUI.Label(b, "<size=12><color=#ffdf95>부품 가게 ▸</color>  <color=#8a9bb3>" + filled + "/5</color></size>", center);
            GUI.color = Color.white;
            if (GUI.Button(b, GUIContent.none, GUIStyle.none)) { GoFlow(5); OrbitSfx.Play("tick", 0.6f); }
        }

        // 🏪 부품 가게 방 — 정비고 오른쪽 (09-24 사장님 24번 · 시안 https://claude.ai/artifact/CthkM2c8KDnFcaxG8m5zJt)
        void ShopRoom()
        {
            var S = sim.S;
            GUI.color = new Color(0.035f, 0.045f, 0.065f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            GUI.color = new Color(1f, 0.76f, 0.35f, 0.04f); for (float yy = 0; yy < RefH; yy += 4) GUI.DrawTexture(new Rect(0, yy, vw, 1), white);
            GUI.color = Color.white;
            var w = new Rect(ox + 50, 14, 860, 572);
            GUI.Label(new Rect(w.x, w.y, 900, 34), "<size=24><b><color=#ffdf95>부품 가게</color></b></size>  <size=12><color=#8a9bb3>진열은 출동하고 오면 바뀐다 · 카드에 올리면 끼울 자리가 깜빡인다</color></size>", label);
            GUI.Label(new Rect(w.x, w.y + 6, w.width, 24), "<size=14><color=#8a9bb3>돈</color> <color=#ffdf95>" + KNum.Fmt(S.cash) + "</color>   <color=#d8ccff>열쇠 " + S.keys + "</color></size>", cost);
            // 🚀 내 청소선 — 그림 위에 부품 칸 다섯 (09-25 사장님 「어디에 들어가는지 이해가 어렵다」 · 시안 https://claude.ai/artifact/6bRbq2eUNE6dXqsSgqvHND)
            if (shipTex == null) shipTex = Resources.Load<Texture2D>("ship/hull");
            var L = new Rect(w.x, w.y + 60, 250, 162);
            GUI.Label(new Rect(L.x, L.y - 22, L.width, 20), "<size=11><color=#8a9bb3>내 청소선 · 부품 칸 다섯</color></size>", label);
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
                GUI.Label(new Rect(r.x + 46, r.y - 1, 80, 20), "<size=10><b><color=#" + chx + ">" + SlotMark[i] + " " + Parts.SlotName[i] + "</color></b></size>", label);
                if (id < 0) { GUI.Label(new Rect(r.x + 46, r.y + 17, r.width - 50, 18), "<size=12><color=#3f4652>비어 있음</color></size>", label); continue; }
                var d = Parts.Defs[id];
                GUI.Label(new Rect(r.x + 100, r.y - 1, r.width - 104, 20), "<size=12><b><color=#" + ColorUtility.ToHtmlStringRGB(RarCol[d.rar]) + ">" + d.name + "</color></b></size>", label);
                GUI.Label(new Rect(r.x + 46, r.y + 17, r.width - 50, 20), "<size=10><color=#aab4c3>" + d.desc + "</color></size>", label);
            }
            // 🧃 다음 판 소모품
            string cons = (S.nFuel > 0 ? "연료 +" + S.nFuel + "초  " : "") + (S.nDmg > 0 ? "화력 +" + S.nDmg + "%  " : "") + (S.nVal > 0 ? "값 +" + S.nVal + "%" : "");
            var tray = new Rect(L.x, L.yMax + 8 + 5 * 43 + 2, L.width, 26); consRect = tray;
            DashFrame(tray, new Color(0.2f, 0.24f, 0.3f));
            GUI.Label(new Rect(tray.x + 8, tray.y + 4, tray.width - 12, 18), "<size=11><color=#8a9bb3>다음 판에 쓰는 것</color>  " + (cons.Length > 0 ? "<color=#9ff0bf><b>" + cons + "</b></color>" : "<color=#3f4652>없음</color>") + "</size>", label);

            // 진열 여섯 — 3 × 2, 오른쪽
            var RR = new Rect(w.x + 266, w.y + 60, w.width - 266, 0);
            GUI.Label(new Rect(RR.x, RR.y - 22, RR.width, 20), "<size=11><color=#8a9bb3>오늘의 진열</color></size>", label);   // 설명은 제목 줄로 — 「오늘의 반값」 띠에 가려졌다
            float cw = (RR.width - 2 * 12) / 3, ch2 = 222;
            for (int k = 0; k < 6; k++)
            {
                var r = new Rect(RR.x + (k % 3) * (cw + 12), RR.y + (k / 3) * (ch2 + 12), cw, ch2);
                if (S.shop == null || k >= S.shop.Count)
                {
                    GUI.color = new Color(0.045f, 0.05f, 0.065f); GUI.DrawTexture(r, white); Frame(r, new Color(0.13f, 0.14f, 0.17f), 1); GUI.color = Color.white;
                    GUI.Label(r, "<size=13><color=#3f4652>팔렸다</color></size>", center);
                    continue;
                }
                int id = S.shop[k]; bool key = id == Parts.Key, cn = SweepSim.IsCons(id), sale = k == S.shopSale;
                int slot = key || cn ? -1 : Parts.Defs[id].slot;
                Color rc = key ? KeyCol : cn ? new Color(0.44f, 0.81f, 0.59f) : RarCol[Parts.Defs[id].rar];
                bool ov = r.Contains(Event.current.mousePosition);
                if (ov && slot >= 0) shopHot = slot;
                GUI.color = new Color(rc.r * 0.07f + 0.02f, rc.g * 0.07f + 0.025f, rc.b * 0.08f + 0.035f, 1); GUI.DrawTexture(r, white);
                Frame(r, sale ? new Color(1f, 0.36f, 0.3f) : rc, ov ? 3 : 2);
                if (key || !cn && Parts.Defs[id].rar == 2) { GUI.color = new Color(rc.r, rc.g, rc.b, 0.06f + 0.05f * Mathf.Sin(Time.unscaledTime * 4)); GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), white); }
                // 어디로 가나 — 칸 색 띠
                Color dc = slot >= 0 ? SlotCol[slot] : rc;
                string dt = key ? "열쇠 +1 · 핵심 칸 하나" : cn ? "다음 판에 한 번" : SlotMark[slot] + " " + Parts.SlotName[slot] + "에 끼움";
                var db = new Rect(r.x + 8, r.y + 8, 0, 20); db.width = Mathf.Min(r.width - 16, label.CalcSize(new GUIContent("<size=12><b>" + dt + "</b></size>")).x + 14);
                GUI.color = new Color(dc.r, dc.g, dc.b, 0.2f); GUI.DrawTexture(db, white); Frame(db, dc, 1);
                GUI.color = Color.white; GUI.Label(new Rect(db.x + 7, db.y + 1, db.width, 18), "<size=12><b><color=#" + ColorUtility.ToHtmlStringRGB(dc) + ">" + dt + "</color></b></size>", label);
                if (!key && !cn) GUI.Label(new Rect(r.x, r.y + 28, r.width - 10, 18), "<size=10><color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">" + RarStar[Parts.Defs[id].rar] + " " + Parts.RarName[Parts.Defs[id].rar] + "</color></size>", cost);
                if (sale) { var sr = new Rect(r.xMax - 84, r.y - 9, 78, 17); GUI.color = new Color(0.8f, 0.18f, 0.14f); GUI.DrawTexture(sr, white); GUI.color = Color.white; GUI.Label(sr, "<size=11><b>오늘의 반값</b></size>", center); }
                string nm = key ? "양자 열쇠" : cn ? SweepSim.ConsName[id - SweepSim.Cons0] : Parts.Defs[id].name;
                var itx = ItemTex(id);
                if (itx != null) { GUI.color = new Color(1, 1, 1, 0.08f); GUI.DrawTexture(new Rect(r.x + 8, r.y + 46, 48, 48), texDisc); GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + 6, r.y + 44, 52, 52), itx); }
                GUI.Label(new Rect(r.x + 62, r.y + 44, r.width - 68, 22), "<size=15><b><color=#ffffff>" + nm + "</color></b></size>", label);
                string desc = key ? "◆ 보라 테두리 핵심 칸 하나를 연다" : cn ? SweepSim.ConsDesc[id - SweepSim.Cons0] : Parts.Defs[id].desc;
                GUI.Label(new Rect(r.x + 62, r.y + 66, r.width - 68, 42), "<size=11><color=#c8d0dc>" + desc + "</color></size>", small);
                // 지금 것 → 바뀌는 것
                GUI.color = new Color(0.2f, 0.23f, 0.29f); GUI.DrawTexture(new Rect(r.x + 10, r.y + 112, r.width - 20, 1), white); GUI.color = Color.white;
                string cmp = key ? "<color=#8a9bb3>정비고 ◆ 칸에 쓴다 · 가진 열쇠 " + S.keys + "</color>" : cn ? "<color=#8a9bb3>끼우지 않는다 — 다음 출동에만</color>" : PartCompare(id);
                GUI.Label(new Rect(r.x + 10, r.y + 116, r.width - 20, 58), "<size=11>" + cmp + "</size>", small);
                double price = sim.ShelfPrice(k); bool can = S.cash >= price;
                var bb = new Rect(r.x + 10, r.yMax - 40, r.width - 20, 30);
                string ptxt = KNum.Fmt(price) + (sale ? " <color=#ffb0a0>(반값)</color>" : "");   // 원래 값은 카드 위 「오늘의 반값」 띠가 말해 준다 — 단추가 좁다
                if (GUI.Button(bb, can ? "<size=" + (sale ? 13 : 14) + ">사기 · " + ptxt + "</size>" : "<size=12><color=#ff9b8f>" + ptxt + " — 돈 모자람</color></size>", can ? btn : btnOff) && can)
                {
                    var to = key ? new Rect(w.xMax - 120, w.y + 6, 110, 24) : cn ? consRect : slotRects[slot];
                    string fl = key ? "열쇠 +1" : nm;
                    if (sim.BuyPart(k))
                    {
                        flyCards.Add(new FlyCard { a = new Rect(r.x + 6, r.y + 44, 52, 52), b = to, t0 = Time.unscaledTime, c = slot >= 0 ? SlotCol[slot] : rc, txt = fl, id = id, slot = slot });
                        OrbitSfx.Play(key ? "launch" : "buy", key ? 0.5f : 0.8f);
                    }
                }
            }
            // 새로고침 — 판마다 한 번 공짜
            {
                var rb = new Rect(RR.x + RR.width / 2 - 120, w.yMax - 36, 240, 32);
                bool free = S.freeRoll, can = free || S.cash >= sim.RerollPrice;
                if (GUI.Button(rb, "<size=13>진열 새로고침 · " + (free ? "<color=#9ff0bf>공짜</color>" : KNum.Fmt(sim.RerollPrice)) + "</size>", can ? btn : btnOff) && can && sim.RerollShop()) OrbitSfx.Play("tick", 0.7f);
            }
            // 날아가는 카드
            float now = Time.unscaledTime;
            for (int i = flyCards.Count - 1; i >= 0; i--)
            {
                var f = flyCards[i]; float k = (now - f.t0) / 0.5f;
                if (k >= 1) { flyCards.RemoveAt(i); if (f.slot >= 0) slotFlash[f.slot] = now; BuyFx(f.b.center, f.c, true, f.slot >= 0 ? Parts.SlotName[f.slot] + " 장착!" : "장착!"); continue; }
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
            { "dmg", new[] { "화력", "%" } }, { "spd", new[] { "연사", "%" } }, { "rad", new[] { "크기", "%" } }, { "fuel", new[] { "연료", "초" } }, { "crit", new[] { "치명", "%" } },
            { "dbl", new[] { "한 발 더", "%" } }, { "drone", new[] { "드론 몫", "%" } }, { "val", new[] { "모든 값", "%" } }, { "vault", new[] { "금고 위성", "%" } }, { "att", new[] { "부착물", "%" } },
            { "cut", new[] { "상환 몫", "%p" } }, { "fee0", new[] { "수수료 0", "" } }, { "div", new[] { "배당", "%" } }, { "combo", new[] { "연쇄 상한", "" } }, { "hole", new[] { "블랙홀", "%" } },
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
                outp.Add("<color=" + (d > 0 ? "#9ff0bf" : "#ff9b8f") + ">" + fn[0] + (k == "fee0" ? (d > 0 ? " 켜짐" : " 꺼짐") : " " + (d > 0 ? "+" : "−") + System.Math.Abs(shown).ToString("0.##") + fn[1]) + "</color>");
            }
            if (cur == id) return "<color=#8a9bb3>지금 끼운 것과 같다</color>";
            string head = cur >= 0 ? "<color=#8a9bb3>지금 <color=#8a7f99>" + Parts.Defs[cur].name + "</color> 빠짐</color>\n" : "<color=#8a9bb3>빈 칸에 끼움</color>\n";
            return head + (outp.Count > 0 ? string.Join(" · ", outp) : "<color=#8a9bb3>달라지는 것 없음</color>");
        }
    }
}
