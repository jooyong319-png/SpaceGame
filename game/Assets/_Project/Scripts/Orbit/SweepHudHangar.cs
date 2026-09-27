using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 🛠 격납고 — 파산 뒤 화면 (09-26 사장님 「파산에 힘을 — 신용으로 배 기본 무기 + 뱀서처럼 패시브」 · 시안 https://claude.ai/artifact/QmnALcrcZVthDjXqeWCY21).
    /// 위 탭 [청소선 | 영구 강화] · 아래 [전부 되돌려 받기] [출발]. 경력 6칸 자리를 바꿨다.
    /// </summary>
    public partial class SweepHud
    {
        int hangarTab;
        int hangarPick = -1;
        static Texture2D[] upTex, shipHullTex;
        static readonly string[] HullName = { "ship/hull", "ship/hull_scatter", "ship/hull_harpoon", "ship/hull_tesla", "ship/hull_missile", "ship/hull_frost", "ship/hull_cluster" };
        static readonly Color[] ShipCol = { new Color(0.91f, 0.53f, 0.23f), new Color(1f, 0.77f, 0.23f), new Color(0.3f, 0.82f, 0.78f), new Color(0.35f, 0.78f, 1f), new Color(1f, 0.42f, 0.4f), new Color(0.56f, 0.85f, 1f), new Color(1f, 0.6f, 0.25f) };
        static readonly int[][] ShipStat = { new[] { 3, 2, 2 }, new[] { 2, 4, 4 }, new[] { 3, 3, 2 }, new[] { 3, 4, 3 }, new[] { 2, 3, 2 }, new[] { 2, 4, 3 }, new[] { 3, 4, 4 } };   // 한 방 · 넓이 · 연쇄 (5칸)

        Texture2D UpTex(int i) { if (upTex == null) upTex = new Texture2D[SweepSim.UpCount]; if (upTex[i] == null) upTex[i] = Resources.Load<Texture2D>("hangar/up_" + i); return upTex[i]; }
        Texture2D HullTex(int i) { if (shipHullTex == null) shipHullTex = new Texture2D[HullName.Length]; if (shipHullTex[i] == null) shipHullTex[i] = Resources.Load<Texture2D>(HullName[i]); return shipHullTex[i]; }

        public void TestHangar(int tab, int pick) { hangarTab = tab; hangarPick = pick; }   // 에디터 캡처용

        void Hangar()
        {
            var M = sim.M;
            if (hangarPick < 0) hangarPick = sim.Ship;
            GUI.color = new Color(0.035f, 0.045f, 0.065f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white);
            GUI.color = new Color(1f, 0.76f, 0.35f, 0.035f); for (float yy = 0; yy < RefH; yy += 4) GUI.DrawTexture(new Rect(0, yy, vw, 1), white);
            GUI.color = Color.white;
            var w = new Rect(ox + 40, 16, 880, 568);
            var ac = ShipCol[hangarPick];
            string ah = ColorUtility.ToHtmlStringRGB(ac);

            // 머리 — 파산 · 신용
            GUI.Label(new Rect(w.x, w.y, 600, 34), "<size=26><b><color=#" + ah + Loc.T(">격납고</color></b></size>   <size=13>") + (M.company > 1 ? "<color=#ff9b8f>" + (M.company - 1) + Loc.T("대 파산</color> <color=#5a6475>→</color> ") : "") + "<color=#ffdf95>" + M.company + Loc.T("대 청소선</color></size>"), label);
            GUI.Label(new Rect(w.x, w.y + 34, 700, 20), Loc.T("<size=11><color=#8a9bb3>잃은 것: 돈 · 트리 · 청구서 · 면허 · 열쇠     남은 것: 신용 · 영구 강화 · 배 · 기사 · 기록</color></size>"), label);
            var cr = new Rect(w.xMax - 170, w.y + 2, 170, 34);
            GUI.color = new Color(0.14f, 0.11f, 0.04f); GUI.DrawTexture(cr, white); Frame(cr, new Color(0.35f, 0.28f, 0.07f), 2); GUI.color = Color.white;
            GUI.Label(cr, Loc.T("<size=13><color=#8a9bb3>신용</color>  <size=20><b><color=#ffdf95>") + M.credit.ToString("0") + "</color></b></size></size>", center);

            // 탭
            string[] tabs = { Loc.T("청소선"), Loc.T("영구 강화") };
            for (int t = 0; t < 2; t++)
            {
                var tr = new Rect(w.x + t * 128, w.y + 62, 120, 30); bool on = hangarTab == t;
                GUI.color = on ? new Color(0.1f, 0.13f, 0.2f) : new Color(0.05f, 0.065f, 0.1f); GUI.DrawTexture(tr, white);
                Frame(tr, on ? ac : new Color(0.17f, 0.22f, 0.34f), 2); GUI.color = Color.white;
                if (GUI.Button(tr, "<size=14>" + (on ? "<color=#e7ecf8>" : "<color=#8d99b8>") + tabs[t] + "</color></size>", center)) { hangarTab = t; OrbitSfx.Play("tick", 0.5f); }
            }
            var body = new Rect(w.x, w.y + 102, w.width, 390);
            GUI.color = new Color(0.05f, 0.065f, 0.1f); GUI.DrawTexture(body, white); Frame(body, new Color(0.17f, 0.22f, 0.34f), 2); GUI.color = Color.white;
            if (hangarTab == 0) HangarShips(body, ac, ah); else HangarUps(body);

            // 아래 — 되돌려 받기 · 출발
            int spent = sim.UpSpent;
            var rb = new Rect(w.x, w.yMax - 60, 240, 44);
            GUI.enabled = spent > 0;
            if (GUI.Button(rb, spent > 0 ? Loc.T("<size=14>강화 전부 되돌려 받기 <color=#ffdf95>+") + spent + "</color></size>" : Loc.T("<size=14><color=#5a6475>되돌려 받을 강화 없음</color></size>"), btnC)) { sim.RefundUps(); OrbitSfx.Play("pick", 0.8f); }
            GUI.enabled = true;
            int goShip = sim.ShipOwned(hangarPick) ? hangarPick : sim.Ship;          // 안 산 배를 보고 있으면 지금 타는 배로 나간다
            var go = new Rect(w.xMax - 330, w.yMax - 64, 330, 52);
            if (GUI.Button(go, "(" + M.company + Loc.T("대) ") + SweepSim.Ships[goShip].name + Ro(SweepSim.Ships[goShip].name) + Loc.T(" 출발 ▸"), bigBtn))
            {
                sim.SelectShip(goShip); sim.CloseCareer(); showResult = false; flow = 2; hangarPick = -1; hangarTab = 0; game.Save();   // 09-26 사장님 「조종실이 첫 화면으로」
            }
        }

        void HangarShips(Rect body, Color ac, string ah)
        {
            var M = sim.M;
            // 왼쪽 — 배 목록
            var L = new Rect(body.x + 12, body.y + 12, 230, body.height - 24);
            for (int i = 0; i < SweepSim.Ships.Length; i++)
            {
                var d = SweepSim.Ships[i]; bool own = sim.ShipOwned(i), on = hangarPick == i;
                float pitch = Mathf.Min(76, (L.height - 4) / SweepSim.Ships.Length), rh = pitch - 6;   // 배가 늘면 칸을 줄인다 (다섯 척부터 넘쳤다)
                var r = new Rect(L.x, L.y + i * pitch, L.width, rh);
                GUI.color = on ? new Color(0.1f, 0.13f, 0.2f) : new Color(0.08f, 0.1f, 0.16f); GUI.DrawTexture(r, white);
                Frame(r, on ? ShipCol[i] : new Color(0.15f, 0.18f, 0.26f), on ? 2 : 1);
                var th = new Rect(r.x + 6, r.y + 4, 90 * (rh - 8) / 56, rh - 8);
                GUI.color = new Color(0.03f, 0.04f, 0.07f); GUI.DrawTexture(th, white);
                var ht = HullTex(i); if (ht != null) { GUI.color = own ? Color.white : new Color(0.35f, 0.35f, 0.38f); GUI.DrawTexture(th, ht, ScaleMode.ScaleToFit); }
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 104, r.y + rh / 2 - 22, r.width - 108, 20), "<size=14><b>" + d.name + "</b></size>", label);
                GUI.Label(new Rect(r.x + 104, r.y + rh / 2, r.width - 108, 20), (Loc.En ? "<size=10>" : "<size=11>") + (Loc.En && !own ? "" : "<color=#8d99b8>" + d.weapon + " · </color>") + (own ? (sim.Ship == i ? Loc.T("<color=#6fe3a0>타는 중</color>") : Loc.T("<color=#8d99b8>보유</color>")) : Loc.T("<color=#ffdf95>신용 ") + d.price + "</color>") + "</size>", label);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { hangarPick = i; OrbitSfx.Play("tick", 0.5f); }
            }
            if (SweepSim.Ships.Length < 5) GUI.Label(new Rect(L.x, L.y + SweepSim.Ships.Length * 76 + 6, L.width, 60), Loc.T("<size=11><color=#5a6475>배는 차례로 더 들어온다</color></size>"), label);

            // 오른쪽 — 고른 배
            var sd = SweepSim.Ships[hangarPick]; bool mine = sim.ShipOwned(hangarPick);
            var st = new Rect(L.xMax + 16, body.y + 12, 340, 240);
            GUI.color = new Color(0.03f, 0.04f, 0.07f); GUI.DrawTexture(st, white); Frame(st, new Color(0.17f, 0.22f, 0.34f), 2);
            GUI.color = new Color(ac.r, ac.g, ac.b, 0.16f); GUI.DrawTexture(new Rect(st.x + 30, st.yMax - 90, st.width - 60, 88), texDisc);
            var hb = HullTex(hangarPick); GUI.color = Color.white;
            if (hb != null) GUI.DrawTexture(new Rect(st.x + 10, st.y + 30, st.width - 20, st.height - 36), hb, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(st.x + 8, st.y + 4, 200, 18), Loc.T("<size=10><color=#5a6475>조종실 미리보기</color></size>"), label);

            var I = new Rect(st.xMax + 18, body.y + 14, body.xMax - st.xMax - 30, body.height - 28);
            GUI.Label(new Rect(I.x, I.y, I.width, 30), "<size=22><b><color=#" + ah + ">" + sd.name + "</color></b></size>", label);
            GUI.Label(new Rect(I.x, I.y + 34, I.width, 20), Loc.T("<size=12><color=#e7ecf8>기본 무기 · ") + sd.weapon + "</color>   <color=#8d99b8>" + sd.trait + "</color></size>", label);
            var dsty = new GUIStyle(label) { wordWrap = true };
            int dfs = Loc.En ? 11 : 12;   // 🌐 영어 설명은 길다 — 한 치수 작게
            GUI.Label(new Rect(I.x, I.y + 62, I.width, 70), "<size=" + dfs + "><color=#c4cce2>" + KWrap(sd.desc, dfs, I.width - 6) + "</color></size>", dsty);
            string[] lab = { Loc.T("한 방"), Loc.T("넓이"), Loc.T("연쇄") };
            for (int k = 0; k < 3; k++)
            {
                float yy = I.y + 134 + k * 20;
                GUI.Label(new Rect(I.x, yy - 3, 50, 18), "<size=11><color=#8d99b8>" + lab[k] + "</color></size>", label);
                for (int p = 0; p < 5; p++) { GUI.color = p < ShipStat[hangarPick][k] ? ac : new Color(0.14f, 0.17f, 0.27f); GUI.DrawTexture(new Rect(I.x + 50 + p * 30, yy, 26, 10), white); }
                GUI.color = Color.white;
            }
            var bb = new Rect(I.x, I.y + 206, 220, 40);
            if (mine) GUI.Label(bb, "<size=13><color=#6fe3a0>" + (hangarPick == sim.Ship ? Loc.T("지금 타는 배") : Loc.T("보유 — 아래 [출발]로 이 배를 탄다")) + "</color></size>", label);
            else
            {
                bool can = M.credit >= sd.price;
                GUI.enabled = can;
                if (GUI.Button(bb, can ? Loc.T("<size=15>들여오기 · 신용 ") + sd.price + "</size>" : Loc.T("<size=13><color=#5a6475>신용 ") + sd.price + Loc.T(" 필요 (지금 ") + M.credit.ToString("0") + ")</color></size>", btnC) && sim.BuyShip(hangarPick)) { OrbitSfx.Play("buy", 1f); OrbitSfx.Play("unit", 0.8f); }
                GUI.enabled = true;
            }
            GUI.Label(new Rect(I.x, I.y + 256, I.width, 130), Loc.T("<size=10><color=#5a6475>트리의 빔 칸 셋은 배의 무기에 맞게 바뀐다.\n무기고의 보조 무기는 어느 배든 그대로.") + (hangarPick == 5 ? Loc.T("\n<color=#8fd8ff>무기고 냉동 빔 줄(강화 · 각성 · 특화 · 3단계)이 이 배의 서리 포도 세게 한다.</color>") : hangarPick == 6 ? Loc.T("\n<color=#ffa040>무기고 분열탄 줄(강화 · 각성 · 특화 · 3단계)이 이 배의 포탄도 세게 한다.</color>") : "") + "</color></size>", dsty);
        }

        void HangarUps(Rect body)
        {
            var M = sim.M;
            GUI.Label(new Rect(body.x + 14, body.y + 8, body.width - 28, 20), Loc.T("<size=11><color=#8d99b8>회사가 바뀌어도 남는다 · 단계마다 값이 오른다 · 언제든 전부 되돌려 받아 다시 짤 수 있다</color></size>"), label);
            float cw = (body.width - 24 - 3 * 8) / 4, ch = 82;
            for (int i = 0; i < SweepSim.UpCount; i++)
            {
                var u = SweepSim.Ups[i]; int lv = sim.Up(i), c = sim.UpCost(i); bool max = c < 0, can = !max && M.credit >= c;
                var r = new Rect(body.x + 12 + (i % 4) * (cw + 8), body.y + 34 + (i / 4) * (ch + 8), cw, ch);
                GUI.color = new Color(0.08f, 0.1f, 0.16f); GUI.DrawTexture(r, white);
                Frame(r, max ? new Color(0.3f, 0.6f, 0.42f) : can ? new Color(0.45f, 0.37f, 0.12f) : new Color(0.15f, 0.18f, 0.26f), can ? 2 : 1);
                GUI.color = Color.white;
                var t = UpTex(i); if (t != null) GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, 52, 52), t);
                GUI.Label(new Rect(r.x + 64, r.y + 4, r.width - 68, 20), "<size=13><b>" + u.name + "</b></size>", label);
                var ws = new GUIStyle(label) { wordWrap = true };
                GUI.Label(new Rect(r.x + 64, r.y + 22, r.width - 68, 30), "<size=10><color=#8d99b8>" + KWrap(u.desc, 10, r.width - 74) + "</color></size>", ws);
                float pw = Mathf.Min(10, (r.width - 72) / u.max - 2);
                for (int k = 0; k < u.max; k++) { GUI.color = k < lv ? new Color(1f, 0.81f, 0.29f) : new Color(0.14f, 0.17f, 0.27f); GUI.DrawTexture(new Rect(r.x + 64 + k * (pw + 2), r.y + 56, pw, 6), white); }
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 64, r.y + 62, r.width - 68, 18), max ? Loc.T("<size=11><color=#6fe3a0>최대</color></size>") : "<size=11><color=" + (can ? "#ffdf95" : "#ff8a7a") + Loc.T(">신용 ") + c + "</color></size>", label);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && can && sim.BuyUp(i)) OrbitSfx.Play("buy", 0.8f);
            }
            GUI.Label(new Rect(body.x + 14, body.yMax - 24, body.width - 28, 20), Loc.T("<size=10><color=#5a6475>신용은 파산할 때 받는다 — 갚은 청구서가 많을수록 많이 (쌓인 신용 × ") + SweepSim.CreditK + ")</color></size>", label);
        }
    }
}
