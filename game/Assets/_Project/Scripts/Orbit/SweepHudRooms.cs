using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 09-24 사장님 시안 확정 — 조종실 양옆이 방이다: 정비고(왼쪽) ← 조종실 → 증권(오른쪽), 화살표를 누르면 화면이 슥 밀린다.
    /// 청구서는 조종대 위 홀로그램 (C안) · [납부] 한 번 · 모자라면 [대출로] → 계약서.
    /// 증권 방 = 진짜 증권 앱처럼: 종합지수 · 종목표 · 봉 차트(6초/30초/1분) + 이동평균 · 거래량 · 호가 · 주문 · 잔고 · 속보. 한국식 색 (오름 빨강 · 내림 파랑).
    /// </summary>
    public partial class SweepHud
    {
        // ───────────────────────────────── 좌우로 밀리는 세 방
        int slideFrom = -1; float slideAt = -9;
        const float SlideLen = 0.45f;
        static readonly int[] Rooms = { 3, 5, 2, 4 };                         // 정비고 · 🏪 가게 · 조종실 · 증권 (가게는 사야 생긴다)
        float RoomX(int f) => f == 3 ? (sim.ShopOpen ? -2 : -1) : f == 5 ? -1 : f == 4 ? 1 : 0;
        float ViewX
        {
            get
            {
                float k = slideFrom < 0 ? 1 : Mathf.Clamp01((Time.unscaledTime - slideAt) / SlideLen);
                if (k >= 1 || reduceMotion) return RoomX(flow);
                k = k * k * (3 - 2 * k);
                return Mathf.Lerp(RoomX(slideFrom), RoomX(flow), k);
            }
        }
        /// <summary>조종실이 화면에서 밀려난 만큼 (기준 px) — 카메라도 같이 밀어 창밖 행성이 따라간다</summary>
        public float CockpitDx => CockpitView ? -ViewX * vw : 0;

        public void GoFlow(int to)
        {
            lobby = false;
            if (to == flow) return;
            bool strip = (flow >= 2 && flow <= 5) && (to >= 2 && to <= 5);
            slideFrom = strip ? flow : -1; slideAt = Time.unscaledTime; flow = to;
            if (strip) OrbitSfx.Play("tick", 0.5f, 0.3f, 0.02f);
        }

        void NavKeys(Keyboard kb)
        {
            if (kb == null || !sim.R.over || sim.M.careerOpen || sim.M.won || loanOpen || lottoOpen || newsOpen) return;
            if (kb.leftArrowKey.wasPressedThisFrame) { if (flow == 2) GoFlow(sim.ShopOpen ? 5 : 3); else if (flow == 5) GoFlow(3); else if (flow == 4) GoFlow(2); }
            if (kb.rightArrowKey.wasPressedThisFrame) { if (flow == 2) GoFlow(4); else if (flow == 3) GoFlow(sim.ShopOpen ? 5 : 2); else if (flow == 5) GoFlow(2); }
        }

        void Strip()
        {
            var m0 = GUI.matrix; float v = ViewX;
            foreach (int f in Rooms)
            {
                float d = RoomX(f) - v;
                if (Mathf.Abs(d) >= 0.999f) continue;
                GUI.matrix = m0 * Matrix4x4.Translate(new Vector3(d * vw, 0, 0));
                if (f == 2) Cockpit();
                else if (f == 3)
                {
                    GUI.color = new Color(0.02f, 0.027f, 0.04f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
                    bool fr = RoomFrameBegin(); Bay(); if (fr) RoomFrameEnd(BpBg);   // 🖥 10-07 모니터 테 안으로
                    // 큰 「조종실로」 버튼 뺌 — 옆 탭 하나로 (09-24 25번)
                    if (sim.ShopOpen) { if (NavTab(true, Loc.T("부품 가게"), "", SweepGame.Amber)) GoFlow(5); }
                    else if (NavTab(true, Loc.T("조종실로"), "", SweepGame.Amber)) GoFlow(2);
                }
                else if (f == 5)
                {
                    if (!sim.ShopOpen) continue;
                    ShopRoom(); GUI.enabled = true;                              // 🛒 구입 확인 창이 막아 둔 것을 풀어 준다
                    // 가게 안내 줄은 뺐다 — 카드 머리를 덮었고, 고 영감 대화(dlg:shop)가 같은 말을 한다 (09-26)
                    if (NavTab(false, Loc.T("정비고"), "", SweepGame.Amber)) GoFlow(3);
                    if (NavTab(true, Loc.T("조종실로"), "", SweepGame.Amber)) GoFlow(2);
                }
                else { StockRoom(); if (flow == 4 && sim.StockOpen) GuideOnce("stock", Loc.T("시세는 출동 중에만 변동 · 여기서 매수하고, 출동 중엔 S로 보며 매도")); }
            }
            GUI.matrix = m0;
        }

        // 🖥 10-07 증권 · 정비고 = 모니터 화면 — 내용을 0.94로 줄여 남색 강철 테(PixelLab) 안에. 테 밖은 방 바탕색으로 가린다 · 그림 없으면 예전처럼 꽉 차게
        static Texture2D monTex; Matrix4x4 roomM0; bool roomFramed;
        const float RoomK = 0.94f, MonB = 34, MonBd = 20;                   // 내용 배율 · 테 그림 가장자리(그림 칸) · 그릴 굵기
        Vector2 RoomC => new Vector2(ox + 480, 300);
        Vector2 RoomPt(Vector2 p) => roomFramed ? RoomC + (p - RoomC) * RoomK : p;   // 테 안 좌표 → 바깥 좌표 (동전 날리기 등)
        bool RoomFrameBegin()
        {
            if (monTex == null) monTex = Resources.Load<Texture2D>("cockpit/monitor_frame");
            if (monTex == null) return false;
            roomM0 = GUI.matrix; var c = RoomC;
            GUI.matrix = roomM0 * Matrix4x4.TRS(c, Quaternion.identity, new Vector3(RoomK, RoomK, 1)) * Matrix4x4.Translate(-c);
            return roomFramed = true;
        }
        void RoomFrameEnd(Color bg)
        {
            GUI.matrix = roomM0; roomFramed = false;
            var fr = new Rect(ox + 4, 4, 952, 592);
            GUI.color = bg;                                                   // 테 밖 — 넓은 화면에서 삐져나온 내용 가리기
            GUI.DrawTexture(new Rect(0, 0, fr.x, RefH), white); GUI.DrawTexture(new Rect(fr.xMax, 0, vw - fr.xMax, RefH), white);
            GUI.DrawTexture(new Rect(fr.x, 0, fr.width, fr.y), white); GUI.DrawTexture(new Rect(fr.x, fr.yMax, fr.width, RefH - fr.yMax), white);
            GUI.color = Color.white;
            float tw = monTex.width, th = monTex.height, u = MonB / tw, v = MonB / th, b = MonBd;
            float[] xs = { fr.x, fr.x + b, fr.xMax - b, fr.xMax }, ys = { fr.y, fr.y + b, fr.yMax - b, fr.yMax };
            float[] us = { 0, u, 1 - u, 1 }, vs = { 1, 1 - v, v, 0 };       // 그림 v 는 아래가 0
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    if (i == 1 && j == 1) continue;                           // 가운데(화면)는 비워 둔다
                    GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xs[i], ys[j], xs[i + 1], ys[j + 1]), monTex, Rect.MinMaxRect(us[i], vs[j + 1], us[i + 1], vs[j]));
                }
        }

        static readonly Texture2D[] tabTex = new Texture2D[2]; static GUIStyle tabStyle;
        /// <summary>화면 가장자리 세로 탭 — ‹ 정비고로 · 증권 하러 가기 ›</summary>
        bool NavTab(bool right, string word, string sub, Color col)
        {
            float w = 42, x = right ? Mathf.Min(vw - w, ox + 960 + 4) : Mathf.Max(0, ox - w - 4);
            var r = new Rect(x, 170, w, 260);
            bool ov = r.Contains(Event.current.mousePosition);
            bool green = col.g > col.r * 1.2f;                                  // 🖼 10-07 픽셀랩 금속 판 — 주황(정비고 · 가게 · 조종실) · 초록(증권)
            if (tabTex[green ? 1 : 0] == null) tabTex[green ? 1 : 0] = Resources.Load<Texture2D>(green ? "cockpit/tab_green" : "cockpit/tab_amber");
            var tt = tabTex[green ? 1 : 0];
            if (tt != null)
            {
                if (tabStyle == null) tabStyle = new GUIStyle { border = new RectOffset(0, 0, 48, 26) };   // 위 · 아래 장식은 그대로, 가운데만 늘린다
                tabStyle.normal.background = tt;
                GUI.color = ov ? new Color(1.15f, 1.15f, 1.15f) : Color.white;
                if (Event.current.type == EventType.Repaint) tabStyle.Draw(r, false, false, false, false);
                if (ov) Frame(r, col, 1.5f);
            }
            else
            {
                GUI.color = new Color(col.r * 0.16f, col.g * 0.16f, col.b * 0.16f, 0.94f); GUI.DrawTexture(r, white);
                Frame(r, ov ? col : new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f), ov ? 2.5f : 1.5f);
            }
            GUI.color = Color.white;
            string hex = ColorUtility.ToHtmlStringRGB(col);
            float nd = ov && !reduceMotion ? Mathf.Sin(Time.unscaledTime * 9) * 2.5f : 0;
            Lbl(new Rect(r.x + (right ? nd : -nd), r.y + 6, w, 44), "<size=34><b><color=#" + hex + ">" + (right ? "›" : "‹") + "</color></b></size>", center);
            if (Loc.En)
            {   // 🌐 영어는 한 글자씩 세우면 못 읽는다 — 책등처럼 옆으로 눕혀 쓴다
                var m0 = GUI.matrix; var c = new Vector2(r.center.x, r.y + 122);
                GUI.matrix = m0 * Matrix4x4.TRS(c, Quaternion.Euler(0, 0, 90), Vector3.one) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);   // 화면 배율(GUI.matrix) 위에서 돌린다
                Lbl(new Rect(c.x - 90, c.y - w / 2, 180, w), "<size=15><b><color=#" + hex + ">" + word + "</color></b></size>", center);
                GUI.matrix = m0;
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                foreach (char ch in word) sb.Append(ch == ' ' ? "\n" : ch + "\n");
                Lbl(new Rect(r.x, r.y + 48, w, 180), "<size=14><b><color=#" + hex + ">" + sb.ToString().TrimEnd('\n') + "</color></b></size>", center);
            }
            if (!string.IsNullOrEmpty(sub)) Lbl(new Rect(r.x - 4, r.yMax - (tt == null ? 50 : Loc.En ? 60 : 74), w + 8, 46), "<size=10>" + sub.Replace(" ", "\n") + "</size>", center);   // 탭 안에 두 줄 — 밖으로 삐져나오지 않게
            return Bt(r, GUIContent.none, GUIStyle.none);
        }

    }
}
