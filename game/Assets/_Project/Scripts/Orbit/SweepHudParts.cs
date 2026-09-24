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
        struct FlyCard { public Rect a, b; public float t0; public Color c; public string txt; }
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
            GUI.Label(new Rect(w.x, w.y, 400, 34), "<size=24><b><color=#ffdf95>부품 가게</color></b></size>  <size=12><color=#8a9bb3>진열은 출동하고 오면 바뀐다</color></size>", label);
            GUI.Label(new Rect(w.x, w.y + 6, w.width, 24), "<size=14><color=#8a9bb3>돈</color> <color=#ffdf95>" + KNum.Fmt(S.cash) + "</color>   <color=#d8ccff>열쇠 " + S.keys + "</color></size>", cost);
            // 끼운 부품 다섯
            float sw = (w.width - 4 * 10) / 5;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(w.x + i * (sw + 10), w.y + 46, sw, 84); slotRects[i] = r;
                int id = S.parts != null && i < S.parts.Length ? S.parts[i] : -1;
                GUI.color = id >= 0 ? new Color(0.09f, 0.11f, 0.14f) : new Color(0.05f, 0.06f, 0.08f); GUI.DrawTexture(r, white);
                Frame(r, id >= 0 ? RarCol[Parts.Defs[id].rar] : new Color(0.2f, 0.22f, 0.26f), id >= 0 ? 2 : 1);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x, r.y + 2, r.width, 20), "<size=10><color=#8a9bb3>" + Parts.SlotName[i] + "</color></size>", center);
                if (id < 0) { GUI.Label(new Rect(r.x, r.y + 30, r.width, 24), "<size=11><color=#3f4652>비어 있음</color></size>", center); continue; }
                var d = Parts.Defs[id]; var stx = ItemTex(id); float sx0 = stx != null ? 50 : 8;
                if (stx != null) GUI.DrawTexture(new Rect(r.x + 4, r.y + 24, 44, 44), stx);
                GUI.Label(new Rect(r.x + sx0, r.y + 22, r.width - sx0 - 6, 20), "<size=13><b><color=#" + ColorUtility.ToHtmlStringRGB(RarCol[d.rar]) + ">" + d.name + "</color></b></size>", label);
                GUI.Label(new Rect(r.x + sx0, r.y + 44, r.width - sx0 - 6, 38), "<size=10><color=#c8d0dc>" + d.desc + "</color></size>", small);
            }
            // 🧃 다음 판 소모품
            string cons = (S.nFuel > 0 ? "연료 +" + S.nFuel + "초  " : "") + (S.nDmg > 0 ? "화력 +" + S.nDmg + "%  " : "") + (S.nVal > 0 ? "값 +" + S.nVal + "%" : "");
            GUI.Label(new Rect(w.x, w.y + 136, w.width, 18), "<size=12><color=#8a9bb3>오늘의 진열</color>" + (cons.Length > 0 ? "   <color=#9ff0bf>다음 판: " + cons + "</color>" : "") + "</size>", label);
            // 진열 여섯 — 3 × 2
            float cw = (w.width - 2 * 14) / 3, ch = 176;
            for (int k = 0; k < 6; k++)
            {
                var r = new Rect(w.x + (k % 3) * (cw + 14), w.y + 160 + (k / 3) * (ch + 12), cw, ch);
                if (S.shop == null || k >= S.shop.Count)
                {
                    GUI.color = new Color(0.045f, 0.05f, 0.065f); GUI.DrawTexture(r, white); Frame(r, new Color(0.13f, 0.14f, 0.17f), 1); GUI.color = Color.white;
                    GUI.Label(r, "<size=13><color=#3f4652>팔렸다</color></size>", center);
                    continue;
                }
                int id = S.shop[k]; bool key = id == Parts.Key, cn = SweepSim.IsCons(id), sale = k == S.shopSale;
                Color rc = key ? KeyCol : cn ? new Color(0.44f, 0.81f, 0.59f) : RarCol[Parts.Defs[id].rar];
                bool ov = r.Contains(Event.current.mousePosition);
                GUI.color = new Color(rc.r * 0.1f, rc.g * 0.1f, rc.b * 0.12f, 1); GUI.DrawTexture(r, white);
                Frame(r, sale ? new Color(1f, 0.36f, 0.3f) : rc, ov ? 3 : 2);
                if (key || !cn && Parts.Defs[id].rar == 2) { GUI.color = new Color(rc.r, rc.g, rc.b, 0.08f + 0.06f * Mathf.Sin(Time.unscaledTime * 4)); GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), white); }
                GUI.color = Color.white;
                if (sale) { var sr = new Rect(r.xMax - 92, r.y - 8, 84, 18); GUI.color = new Color(0.8f, 0.18f, 0.14f); GUI.DrawTexture(sr, white); GUI.color = Color.white; GUI.Label(sr, "<size=11><b>오늘의 반값</b></size>", center); }
                string tag = key ? "핵심 칸을 여는 것" : cn ? "소모품 · 한 판" : Parts.RarName[Parts.Defs[id].rar] + " · " + Parts.SlotName[Parts.Defs[id].slot];
                GUI.Label(new Rect(r.x + 12, r.y + 7, r.width - 24, 20), "<size=11><color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">" + tag + "</color></size>", label);
                string nm = key ? "양자 열쇠" : cn ? SweepSim.ConsName[id - SweepSim.Cons0] : Parts.Defs[id].name;
                var itx = ItemTex(id); float tx0 = itx != null ? 72 : 12;
                if (itx != null) { GUI.color = new Color(1, 1, 1, 0.08f); GUI.DrawTexture(new Rect(r.x + 10, r.y + 28, 56, 56), texDisc); GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + 8, r.y + 26, 60, 60), itx); }
                GUI.Label(new Rect(r.x + tx0, r.y + 28, r.width - tx0 - 12, 26), "<size=18><b><color=#ffffff>" + nm + "</color></b></size>", label);
                string desc = key ? "◆ 보라 테두리 핵심 칸 하나를 연다" : cn ? SweepSim.ConsDesc[id - SweepSim.Cons0] : Parts.Defs[id].desc;
                GUI.Label(new Rect(r.x + tx0, r.y + 58, r.width - tx0 - 12, 44), "<size=12><color=#c8d0dc>" + desc + "</color></size>", small);
                if (!key && !cn && S.parts != null && S.parts[Parts.Defs[id].slot] >= 0) GUI.Label(new Rect(r.x + 12, r.y + 100, r.width - 24, 20), "<size=10><color=#8a7f99>지금 " + Parts.Defs[S.parts[Parts.Defs[id].slot]].name + " → 바뀐다</color></size>", label);
                double price = sim.ShelfPrice(k), full = sim.PartPrice(id); bool can = S.cash >= price;
                var bb = new Rect(r.x + 12, r.yMax - 42, r.width - 24, 30);
                string ptxt = (sale ? "<color=#8a93a3>" + KNum.Fmt(full) + "</color> → " : "") + KNum.Fmt(price);
                if (GUI.Button(bb, can ? "<size=14>사기 · " + ptxt + "</size>" : "<size=12><color=#ff9b8f>" + ptxt + " — 돈이 모자라다</color></size>", can ? btn : btnOff) && can)
                {
                    var to = key ? new Rect(w.xMax - 120, w.y + 6, 110, 24) : cn ? new Rect(w.x, w.y + 136, 200, 18) : slotRects[Parts.Defs[id].slot];
                    string fl = key ? "열쇠 +1" : nm;
                    if (sim.BuyPart(k))
                    {
                        flyCards.Add(new FlyCard { a = r, b = to, t0 = Time.unscaledTime, c = rc, txt = fl });
                        OrbitSfx.Play(key ? "launch" : "buy", key ? 0.5f : 0.8f);
                    }
                }
            }
            // 새로고침 — 판마다 한 번 공짜
            {
                var rb = new Rect(w.x + w.width / 2 - 120, w.yMax - 36, 240, 32);
                bool free = S.freeRoll, can = free || S.cash >= sim.RerollPrice;
                if (GUI.Button(rb, "<size=13>진열 새로고침 · " + (free ? "<color=#9ff0bf>공짜</color>" : KNum.Fmt(sim.RerollPrice)) + "</size>", can ? btn : btnOff) && can && sim.RerollShop()) OrbitSfx.Play("tick", 0.7f);
            }
            // 날아가는 카드
            float now = Time.unscaledTime;
            for (int i = flyCards.Count - 1; i >= 0; i--)
            {
                var f = flyCards[i]; float k = (now - f.t0) / 0.5f;
                if (k >= 1) { flyCards.RemoveAt(i); BuyFx(f.b.center, f.c, true, "장착!"); continue; }
                float e = k * k * (3 - 2 * k);
                var rr = new Rect(Mathf.Lerp(f.a.x, f.b.x, e), Mathf.Lerp(f.a.y, f.b.y, e) - Mathf.Sin(k * Mathf.PI) * 60, Mathf.Lerp(f.a.width, f.b.width, e), Mathf.Lerp(f.a.height, f.b.height, e));
                GUI.color = new Color(f.c.r, f.c.g, f.c.b, 0.3f); GUI.DrawTexture(rr, white); Frame(rr, f.c, 2);
                GUI.color = Color.white; GUI.Label(rr, "<size=14><b>" + f.txt + "</b></size>", center);
            }
        }
    }
}
