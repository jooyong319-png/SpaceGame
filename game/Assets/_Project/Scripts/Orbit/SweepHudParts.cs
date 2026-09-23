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
            if (GUI.Button(b, GUIContent.none, GUIStyle.none)) { partsOpen = true; OrbitSfx.Play("tick", 0.6f); }
        }

        void PartsWin()
        {
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var w = new Rect(ox + 60, 50, 840, 500);
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.99f); GUI.DrawTexture(w, white); Frame(w, SweepGame.Amber, 2);
            GUI.color = Color.white;
            title.fontSize = 22; GUI.Label(new Rect(w.x, w.y + 10, w.width, 30), "<color=#ffdf95>부품 가게</color>", title);
            GUI.Label(new Rect(w.x + 20, w.y + 16, 300, 24), "<size=13><color=#8a9bb3>돈</color> <color=#ffdf95>" + KNum.Fmt(S.cash) + "</color>   <color=#d8ccff>열쇠 " + S.keys + "</color></size>", label);
            if (GUI.Button(new Rect(w.xMax - 100, w.y + 12, 84, 30), "닫기", btnOff)) partsOpen = false;

            // 청소선 부품 칸 다섯
            GUI.Label(new Rect(w.x + 20, w.y + 50, 400, 20), "<size=12><color=#8a9bb3>청소선 부품 — 칸마다 하나. 새로 사면 바뀐다</color></size>", label);
            float sw = (w.width - 40 - 4 * 10) / 5;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(w.x + 20 + i * (sw + 10), w.y + 74, sw, 112); slotRects[i] = r;
                int id = S.parts != null && i < S.parts.Length ? S.parts[i] : -1;
                GUI.color = id >= 0 ? new Color(0.1f, 0.12f, 0.15f) : new Color(0.06f, 0.07f, 0.09f); GUI.DrawTexture(r, white);
                Frame(r, id >= 0 ? RarCol[Parts.Defs[id].rar] : new Color(0.2f, 0.22f, 0.26f), id >= 0 ? 2 : 1);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x, r.y + 6, r.width, 18), "<size=11><color=#8a9bb3>" + Parts.SlotName[i] + "</color></size>", center);
                if (id < 0) { GUI.Label(new Rect(r.x, r.y + 40, r.width, 30), "<size=12><color=#3f4652>비어 있음</color></size>", center); continue; }
                var d = Parts.Defs[id];
                GUI.Label(new Rect(r.x + 6, r.y + 28, r.width - 12, 22), "<size=13><b><color=#" + ColorUtility.ToHtmlStringRGB(RarCol[d.rar]) + ">" + d.name + "</color></b></size>", center);
                GUI.Label(new Rect(r.x + 8, r.y + 52, r.width - 16, 56), "<size=11><color=#c8d0dc>" + d.desc + "</color></size>", small);
            }

            // 진열 셋
            GUI.Label(new Rect(w.x + 20, w.y + 200, 500, 20), "<size=12><color=#8a9bb3>오늘의 진열 — 출동을 다녀오면 새로 들어온다</color></size>", label);
            float cw = (w.width - 40 - 2 * 16) / 3;
            for (int k = 0; k < 3; k++)
            {
                var r = new Rect(w.x + 20 + k * (cw + 16), w.y + 224, cw, 190);
                if (S.shop == null || k >= S.shop.Count)
                {
                    GUI.color = new Color(0.05f, 0.05f, 0.07f); GUI.DrawTexture(r, white); Frame(r, new Color(0.14f, 0.15f, 0.18f), 1); GUI.color = Color.white;
                    GUI.Label(r, "<size=13><color=#3f4652>팔렸다</color></size>", center);
                    continue;
                }
                int id = S.shop[k]; bool key = id == Parts.Key;
                Color rc = key ? KeyCol : RarCol[Parts.Defs[id].rar];
                bool ov = r.Contains(Event.current.mousePosition);
                GUI.color = new Color(rc.r * 0.12f, rc.g * 0.12f, rc.b * 0.14f, 1); GUI.DrawTexture(r, white);
                Frame(r, rc, ov ? 3 : 2);
                if (key || Parts.Defs[id].rar == 2) { GUI.color = new Color(rc.r, rc.g, rc.b, 0.1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4)); GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), white); }
                GUI.color = Color.white;
                string tag = key ? "핵심 칸을 여는 것" : Parts.RarName[Parts.Defs[id].rar] + " · " + Parts.SlotName[Parts.Defs[id].slot];
                GUI.Label(new Rect(r.x, r.y + 10, r.width, 18), "<size=11><color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">" + tag + "</color></size>", center);
                GUI.Label(new Rect(r.x, r.y + 32, r.width, 30), "<size=19><b><color=#ffffff>" + (key ? "양자 열쇠" : Parts.Defs[id].name) + "</color></b></size>", center);
                GUI.Label(new Rect(r.x + 14, r.y + 70, r.width - 28, 60), "<size=12><color=#c8d0dc>" + (key ? "◆ 핵심 칸 하나를 연다 (각성 · 과열 사격 · 벌떼 · 쌍둥이 블랙홀 · 큰손 · 궤도 공명 · 두 번째 무기 칸)" : Parts.Defs[id].desc) + "</color></size>", small);
                if (!key && S.parts != null && S.parts[Parts.Defs[id].slot] >= 0) GUI.Label(new Rect(r.x + 14, r.y + 124, r.width - 28, 18), "<size=10><color=#8a7f99>지금 " + Parts.Defs[S.parts[Parts.Defs[id].slot]].name + " → 바뀐다</color></size>", label);
                double price = sim.PartPrice(id); bool can = S.cash >= price;
                var bb = new Rect(r.x + 14, r.yMax - 44, r.width - 28, 32);
                if (GUI.Button(bb, can ? "<size=14>사기 · " + KNum.Fmt(price) + "</size>" : "<size=12><color=#ff9b8f>" + KNum.Fmt(price) + " — 돈이 모자라다</color></size>", can ? btn : btnOff) && can)
                {
                    var to = key ? new Rect(w.x + 20, w.y + 14, 120, 24) : slotRects[Parts.Defs[id].slot];
                    string nm = key ? "열쇠 +1" : Parts.Defs[id].name;
                    if (sim.BuyPart(k))
                    {
                        flyCards.Add(new FlyCard { a = r, b = to, t0 = Time.unscaledTime, c = rc, txt = nm });
                        OrbitSfx.Play(key ? "launch" : "buy", key ? 0.5f : 0.8f);
                    }
                }
            }
            // 새로고침
            {
                var rb = new Rect(w.x + w.width / 2 - 110, w.yMax - 70, 220, 34);
                bool can = S.cash >= sim.RerollPrice;
                if (GUI.Button(rb, "<size=13>진열 새로고침 · " + KNum.Fmt(sim.RerollPrice) + "</size>", can ? btn : btnOff) && can && sim.RerollShop()) OrbitSfx.Play("tick", 0.7f);
                GUI.Label(new Rect(w.x, w.yMax - 30, w.width, 20), "<size=11><color=#5f6878>값은 지금 청구서에 맞춰 오른다 · 전설은 드물다 · 열쇠도 가끔 들어온다</color></size>", center);
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
            BuyFxDraw();
        }
    }
}
