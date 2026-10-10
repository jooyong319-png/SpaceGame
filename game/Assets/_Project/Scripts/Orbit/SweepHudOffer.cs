using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🎁 10-10 스테이지 보상 · 🧳 떠돌이 상인 (사장님 「스테이지 클리어 보상 · 가게는 가끔 오는 느낌」 · 시안 6JmkYLEm5bQUdzWostYE4N)
    /// 청구서 한 장을 갚으면 부품 셋(서로 다른 칸 · 등급은 카드마다 따로) 중 하나를 고른다. 관문을 깬 스테이지는 금빛 · 한 구간 위 확률.
    /// 가게는 늘 열림(10-10 밤) — 청구서 두 장마다 떠돌이 상인이 들러 그날만 희귀 이상 한 칸 · 공짜 새로고침, 다음 출동 전까지 머문다.
    /// </summary>
    public partial class SweepHud
    {
        float offerSeen = -1; int offerHover = -1;
        bool OfferUp => sim.S.offer != null && sim.S.offer.Count > 0 && sim.R.over && flow >= 2 && !sim.M.careerOpen && !sim.M.won && !hireOpen && !loanOpen;
        public void TestOffer(bool gold) { sim.S.gateStage = gold; sim.DebugRollOffer(); offerSeen = Time.unscaledTime - 1; }   // 에디터 시험용

        void OfferWin()
        {
            if (!OfferUp) { offerSeen = -1; return; }
            if (offerSeen < 0) offerSeen = Time.unscaledTime;
            float e = Time.unscaledTime - offerSeen; if (e < 0.6f) return;           // 납부 도장이 먼저 보이게
            float k = reduceMotion ? 1 : Mathf.Clamp01((e - 0.6f) / 0.3f); k = 1 - (1 - k) * (1 - k);
            bool gold = sim.OfferGold;
            GUI.color = new Color(0.016f, 0.024f, 0.04f, 0.86f * k); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            if (gold && !reduceMotion)
            {   // 관문 스테이지 — 은은한 금빛 빛줄기
                var c = new Vector2(vw / 2, 270);
                for (int i = 0; i < 18; i++) { float a = i * 20 + Time.unscaledTime * 6; var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)); Line(c + d * 40, c + d * 700, new Color(1f, 0.84f, 0.47f, 0.05f * k), 22); }
            }
            GUI.color = new Color(1, 1, 1, k);
            var tc = gold ? "#ffdf95" : "#bff4ff";
            string head = Loc.T("스테이지") + " " + sim.S.bill + " " + Loc.T("클리어 — 보급품 하나 고르기");
            var hs = new GUIStyle(center) { fontSize = 26 };
            Lbl(new Rect(vw / 2 - 400, 26, 800, 40), Fit("<color=" + tc + ">" + head + "</color>", 26, 790, hs, 16), hs);
            var odds = SweepSim.OfferOdds[Mathf.Clamp(sim.OfferTierShown, 0, SweepSim.OfferOdds.Length - 1)];
            string sub = (gold ? Loc.T("관문을 깬 스테이지 · 한 구간 위 확률") : Loc.T("카드마다 등급을 따로 굴림")) + "  <color=#5f6878>(" + Parts.RarName[0] + " " + Pc(odds[0]) + " · " + Parts.RarName[1] + " " + Pc(odds[1]) + " · " + Parts.RarName[2] + " " + Pc(odds[2]) + " · " + Parts.RarName[3] + " " + Pc(odds[3]) + ")</color>";
            Lbl(new Rect(vw / 2 - 400, 64, 800, 22), Fit("<color=#8a96aa>" + sub + "</color>", 12, 790, center, 9), center);

            int n = sim.S.offer.Count; float cw = 212, ch = 304, gap = 22, x0 = vw / 2 - (cw * n + gap * (n - 1)) / 2, y0 = 100 + (1 - k) * 30;
            offerHover = -1;
            for (int i = 0; i < n; i++)
            {
                int id = sim.S.offer[i]; var d = Parts.Defs[id]; var rc = RarCol[d.rar];
                var r = new Rect(x0 + i * (cw + gap), y0, cw, ch);
                bool ov = r.Contains(Event.current.mousePosition); if (ov) offerHover = i;
                GUI.color = new Color(0, 0, 0, 0.5f * k); GUI.DrawTexture(new Rect(r.x + 4, r.y + 5, r.width, r.height), white);
                GUI.color = new Color(0.047f, 0.063f, 0.094f, k); GUI.DrawTexture(r, white);
                GUI.color = new Color(rc.r * 0.28f, rc.g * 0.28f, rc.b * 0.28f, k); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 24), white);
                Frame(r, new Color(rc.r, rc.g, rc.b, k), ov ? 3 : 2);
                GUI.color = new Color(1, 1, 1, k);
                Lbl(new Rect(r.x + 9, r.y + 3, r.width - 18, 20), "<size=12><color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">◆ " + Parts.RarName[d.rar] + "</color></size>", label);
                Lbl(new Rect(r.x + 9, r.y + 3, r.width - 18, 20), "<size=11><color=#8a96aa>" + Parts.SlotName[d.slot] + " " + Loc.T("칸") + "</color></size>", cost);
                var ic = ItemTex(id);
                GUI.color = new Color(rc.r, rc.g, rc.b, 0.16f * k); GUI.DrawTexture(new Rect(r.center.x - 50, r.y + 34, 100, 100), texDisc);
                GUI.color = new Color(1, 1, 1, k);
                if (ic != null) GUI.DrawTexture(new Rect(r.center.x - 37, r.y + 46 - (ov && !reduceMotion ? 3 : 0), 74, 74), ic, ScaleMode.ScaleToFit);
                { int pf = SweepSim.PartFam(id); if (pf >= 0) FamChip(new Rect(r.x + 10, r.y + 28, 90, 18), pf); }   // 🧬 계열
                var ns = new GUIStyle(center) { fontSize = 17 };
                Lbl(new Rect(r.x + 6, r.y + 140, r.width - 12, 24), Fit("<b>" + d.name + "</b>", 17, r.width - 14, ns, 11), ns);
                var ds = new GUIStyle(center) { wordWrap = true, alignment = TextAnchor.UpperCenter };
                Lbl(new Rect(r.x + 12, r.y + 170, r.width - 24, 60), "<size=13><color=#9ff0bf>" + d.desc.Replace(" · ", "\n") + "</color></size>", ds);
                GUI.color = new Color(0.16f, 0.2f, 0.27f, k); GUI.DrawTexture(new Rect(r.x + 12, r.yMax - 64, r.width - 24, 1), white); GUI.color = new Color(1, 1, 1, k);
                int old = sim.S.parts != null && sim.S.parts.Length == 5 ? sim.S.parts[d.slot] : -1;
                string now = old < 0 ? Loc.T("빈 칸 → 바로 장착") : Loc.T("교체") + " · " + Parts.Defs[old].name + " · " + Loc.T("고철") + " +" + KNum.Short(sim.ScrapValue(old));
                if (old < 0 || center.CalcSize(new GUIContent("<size=9>" + now + "</size>")).x <= r.width - 18)
                    Lbl(new Rect(r.x + 8, r.yMax - 60, r.width - 16, 18), Fit("<color=#8a96aa>" + now + "</color>", 11, r.width - 18, center, 8), center);
                else
                {   // 10-10 영어 「Replaces · High-Explosive Chamber · Scrap +10」이 카드보다 넓어 앞뒤가 잘림 — 이름 / 고철 두 줄
                    Lbl(new Rect(r.x + 8, r.yMax - 65, r.width - 16, 16), Fit("<color=#8a96aa>" + Loc.T("교체") + " · " + Parts.Defs[old].name + "</color>", 10, r.width - 18, center, 8), center);
                    Lbl(new Rect(r.x + 8, r.yMax - 52, r.width - 16, 16), "<size=10><color=#8a96aa>" + Loc.T("고철") + " +" + KNum.Short(sim.ScrapValue(old)) + "</color></size>", center);
                }
                var b = new Rect(r.x + 14, r.yMax - 36, r.width - 28, 26);
                GUI.color = ov ? new Color(0.24f, 0.47f, 0.31f, k) : new Color(0.12f, 0.16f, 0.22f, k); GUI.DrawTexture(b, white);
                Frame(b, ov ? new Color(0.63f, 0.94f, 0.75f, k) : new Color(0.16f, 0.2f, 0.27f, k), 1); GUI.color = new Color(1, 1, 1, k);
                Lbl(b, "<size=13>" + (ov ? Loc.T("▸ 이걸로 장착") : Loc.T("고르기")) + "</size>", center);
                if (k >= 1 && Bt(r, GUIContent.none, GUIStyle.none)) { sim.PickOffer(i); OrbitSfx.Play("buy", 0.9f); game.shake = Mathf.Max(game.shake, 0.06f); offerSeen = -1; return; }
            }
            Lbl(new Rect(vw / 2 - 400, y0 + ch + 12, 800, 20), Fit("<color=#8a96aa>" + Loc.T("칸이 차 있으면 지금 부품과 바꿔 끼움 — 빠진 부품은 고철로 팔림") + "</color>", 12, 790, center, 9), center);
            double skip = double.MaxValue; foreach (var id in sim.S.offer) skip = System.Math.Min(skip, sim.ScrapValue(id));
            var sb = new Rect(vw / 2 - 120, y0 + ch + 40, 240, 30);
            GUI.color = new Color(0.086f, 0.11f, 0.157f, k); GUI.DrawTexture(sb, white); Frame(sb, new Color(0.16f, 0.2f, 0.27f, k), 1); GUI.color = new Color(1, 1, 1, k);
            if (Bt(sb, Fit("<color=#aab4c3>" + Loc.T("건너뛰기") + " · " + Loc.T("고철") + " +" + KNum.Short(skip) + "</color>", 13, sb.width - 10, center, 9), center) && k >= 1) { sim.PickOffer(-1); OrbitSfx.Play("tick", 0.7f); offerSeen = -1; }
            GUI.color = Color.white;
        }
        static string Pc(double v) => (v * 100).ToString(v < 0.01 ? "0.0" : "0") + "%";

        // 🧳 떠돌이 상인 — 조종실 위 띠 · 창에 붙은 상인 배 · 상인에게 가기
        static Texture2D tugTex;
        void MerchantCockpit()
        {
            if (!sim.ShopOpen || !sim.S.merchant || flow != 2 || OfferUp) return;   // 상인 오는 날만
            if (tugTex == null) tugTex = Resources.Load<Texture2D>("event/ev_tug");
            float t = Time.unscaledTime, bob = reduceMotion ? 0 : Mathf.Sin(t * 1.6f) * 3;
            if (tugTex != null) GUI.DrawTexture(new Rect(ox + 560, 178 + bob, 112, 112 * tugTex.height / (float)tugTex.width), tugTex, ScaleMode.ScaleToFit);
            var band = new Rect(ox + 190, 50, 270, 40);   // 출동 기록 화면 · 키링(ox+490) 사이
            GUI.color = new Color(0.16f, 0.09f, 0.04f, 0.94f); GUI.DrawTexture(band, white); Frame(band, new Color(1f, 0.75f, 0.35f), 2); GUI.color = Color.white;
            Lbl(new Rect(band.x, band.y, band.width, 24), Fit("<color=#ffd68c>" + Loc.T("떠돌이 상인 도착") + "</color>", 15, band.width - 10, center, 10), center);
            Lbl(new Rect(band.x, band.y + 20, band.width, 18), Fit("<color=#e6c8a0>" + Loc.T("다음 출동 전까지만 머묾") + "</color>", 11, band.width - 10, center, 8), center);
            var b = new Rect(ox + 360, 292, 170, 34); float p = 0.5f + 0.5f * Mathf.Sin(t * 4);
            GUI.color = new Color(0.27f, 0.16f, 0.05f, 0.95f); GUI.DrawTexture(b, white); Frame(b, new Color(1f, 0.78f, 0.4f, 0.6f + 0.4f * p), 2); GUI.color = Color.white;
            if (Bt(b, Fit("<color=#ffe6aa>" + Loc.T("상인에게 가기 ▸") + "</color>", 14, b.width - 10, center, 9), center)) { GoFlow(5); OrbitSfx.Play("tick", 0.6f); }
        }
    }
}
