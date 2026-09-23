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
        static readonly int[] Rooms = { 3, 2, 4 };
        static float RoomX(int f) => f == 3 ? -1 : f == 4 ? 1 : 0;
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
            if (to == flow) return;
            bool strip = (flow == 2 || flow == 3 || flow == 4) && (to == 2 || to == 3 || to == 4);
            slideFrom = strip ? flow : -1; slideAt = Time.unscaledTime; flow = to;
            if (strip) OrbitSfx.Play("tick", 0.5f, 0.3f, 0.02f);
        }

        void NavKeys(Keyboard kb)
        {
            if (kb == null || !sim.R.over || sim.M.careerOpen || sim.M.won || loanOpen || lottoOpen || newsOpen) return;
            if (kb.leftArrowKey.wasPressedThisFrame) { if (flow == 2) GoFlow(3); else if (flow == 4) GoFlow(2); }
            if (kb.rightArrowKey.wasPressedThisFrame) { if (flow == 2) GoFlow(4); else if (flow == 3) GoFlow(2); }
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
                    bool en0 = GUI.enabled; GUI.enabled = en0 && !partsOpen;
                    Bay(); FlowBottom();
                    if (NavTab(true, "조종실로", "", SweepGame.Amber)) GoFlow(2);
                    GUI.enabled = en0;
                    if (partsOpen) PartsWin();
                }
                else StockRoom();
            }
            GUI.matrix = m0;
        }

        /// <summary>화면 가장자리 세로 탭 — ‹ 정비고로 · 증권 하러 가기 ›</summary>
        bool NavTab(bool right, string word, string sub, Color col)
        {
            float w = 42, x = right ? Mathf.Min(vw - w, ox + 960 + 4) : Mathf.Max(0, ox - w - 4);
            var r = new Rect(x, 170, w, 260);
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = new Color(col.r * 0.16f, col.g * 0.16f, col.b * 0.16f, 0.94f); GUI.DrawTexture(r, white);
            Frame(r, ov ? col : new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f), ov ? 2.5f : 1.5f);
            string hex = ColorUtility.ToHtmlStringRGB(col);
            float nd = ov && !reduceMotion ? Mathf.Sin(Time.unscaledTime * 9) * 2.5f : 0;
            GUI.Label(new Rect(r.x + (right ? nd : -nd), r.y + 6, w, 44), "<size=34><b><color=#" + hex + ">" + (right ? "›" : "‹") + "</color></b></size>", center);
            var sb = new System.Text.StringBuilder();
            foreach (char ch in word) sb.Append(ch == ' ' ? "\n" : ch + "\n");
            GUI.Label(new Rect(r.x, r.y + 48, w, 180), "<size=14><b><color=#" + hex + ">" + sb.ToString().TrimEnd('\n') + "</color></b></size>", center);
            if (!string.IsNullOrEmpty(sub)) GUI.Label(new Rect(r.x - 14, r.yMax - 26, w + 28, 22), "<size=10>" + sub + "</size>", center);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // ───────────────────────────────── 🟦 홀로그램 청구서 — 조종대 위 발사기에서 떠오른다 (C안)
        static readonly Color Holo = new Color(0.49f, 0.91f, 1f), HoloRed = new Color(1f, 0.55f, 0.48f);
        void HoloBill(bool due)
        {
            var S = sim.S; var M = sim.M;
            float ex = ox + 175, ey = 506;
            var p = new Rect(ox + 75, 184, 200, 258);
            float t = Time.unscaledTime;
            float fl = reduceMotion ? 1 : Mathf.PerlinNoise(t * 1.7f, 0.3f) > 0.78f ? 0.5f + 0.4f * Mathf.PerlinNoise(t * 40, 1.3f) : 1f;   // 가끔 지직
            // 빛기둥
            const int N = 40; float step = (ey - p.y) / N;
            for (int i = 0; i < N; i++)
            {
                float k = i / (float)(N - 1), y = ey - 2 - step * (i + 1), bwid = Mathf.Lerp(76, p.width + 20, k);
                GUI.color = new Color(Holo.r, Holo.g, Holo.b, (0.09f - 0.06f * k) * fl);
                GUI.DrawTexture(new Rect(ex - bwid / 2, y, bwid, step + 0.5f), white);
            }
            // 발사기 (조종대 위 원반)
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(ex - 76, ey - 2, 152, 34), texDisc);
            GUI.color = new Color(0.17f, 0.23f, 0.31f); GUI.DrawTexture(new Rect(ex - 70, ey - 8, 140, 30), texDisc);
            GUI.color = new Color(0.08f, 0.28f, 0.36f); GUI.DrawTexture(new Rect(ex - 54, ey - 6, 108, 20), texDisc);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.8f * fl); GUI.DrawTexture(new Rect(ex - 36, ey - 3, 72, 12), texDisc);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.22f * fl); GUI.DrawTexture(new Rect(ex - 90, ey - 26, 180, 56), texDisc);

            // 뒤에 한 장 더 — 빚 명세서 (누르면 대출 창구)
            if (S.debt > 0 && !M.cleanReady)
            {
                var g = new Rect(p.x + 14, p.y - 24, p.width, 30);
                bool gh = g.Contains(Event.current.mousePosition);
                GUI.color = new Color(HoloRed.r, HoloRed.g, HoloRed.b, (gh ? 0.22f : 0.1f) * fl); GUI.DrawTexture(g, white);
                Frame(g, new Color(HoloRed.r, HoloRed.g, HoloRed.b, (gh ? 0.9f : 0.55f) * fl), 1);
                GUI.color = new Color(1, 1, 1, fl);
                GUI.Label(new Rect(g.x + 8, g.y + 3, g.width - 16, 22), "<size=11><color=#ffc2b8>빚 명세서</color></size>", label);
                GUI.Label(new Rect(g.x + 8, g.y + 3, g.width - 16, 22), "<size=12><color=#ffc2b8>" + KNum.Fmt(S.debt) + " ▸</color></size>", cost);
                GUI.color = Color.white;
                if (GUI.Button(g, GUIContent.none, GUIStyle.none)) loanOpen = true;
            }

            // 판 — 반투명 · 주사선 · 위아래로 훑는 빛 띠
            GUI.color = new Color(0.1f, 0.45f, 0.58f, 0.2f * fl); GUI.DrawTexture(p, white);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.06f * fl);
            for (float y = p.y + 2; y < p.yMax; y += 4) GUI.DrawTexture(new Rect(p.x, y, p.width, 1), white);
            if (!reduceMotion) { GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.08f * fl); GUI.DrawTexture(new Rect(p.x, p.y + Mathf.Repeat(t * 50, p.height - 8), p.width, 8), white); }
            Frame(p, new Color(Holo.r, Holo.g, Holo.b, 0.8f * fl), 1.5f);
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, fl);                // 모서리 꺾쇠
            foreach (var c in new[] { new Vector2(p.x, p.y), new Vector2(p.xMax - 10, p.y), new Vector2(p.x, p.yMax - 3), new Vector2(p.xMax - 10, p.yMax - 3) }) GUI.DrawTexture(new Rect(c.x, c.y, 10, 3), white);

            GUI.color = new Color(1, 1, 1, fl);
            float x = p.x + 12, w = p.width - 24, y0 = p.y + 8;
            GUI.Label(new Rect(x, y0, w, 20), "<size=12><b><color=#bff4ff>KESSLER // 청구</color></b></size>", label);
            if (M.cleanReady)
            {
                GUI.Label(new Rect(x, y0 + 50, w, 70), "<size=22><color=#9ff0bf>빚 청산!</color></size>\n<size=12><color=#bff4ff>청산 출동만 남았다</color></size>", center);
            }
            else if (S.bill >= SweepSim.Bills.Length)
            {
                GUI.Label(new Rect(x, y0 + 30, w, 20), "<size=12><color=#7fcfe0>청구서는 끝</color></size>", label);
                GUI.Label(new Rect(x, y0 + 52, w, 20), "<size=11><color=#7fcfe0>남은 빚</color></size>", label);
                GUI.Label(new Rect(x, y0 + 68, w, 36), "<size=28><b><color=#ffc2b8>" + KNum.Fmt(S.debt) + "</color></b></size>", label);
                GUI.Label(new Rect(x, y0 + 110, w, 40), "<size=11><color=#bff4ff>다 갚으면 청산 출동</color></size>", label);
                GUI.color = Color.white;
                if (HoloBtn(new Rect(x, y0 + 196, w, 30), "대출 창구 ▸", Holo, true, fl)) loanOpen = true;
            }
            else
            {
                var b = SweepSim.Bills[S.bill];
                GUI.Label(new Rect(x, y0, w, 20), "<size=12><color=#7fcfe0>" + (S.bill + 1) + " / " + SweepSim.Bills.Length + "</color></size>", cost);
                GUI.Label(new Rect(x, y0 + 22, w, 22), "<size=13><color=#dff8ff>" + b.t + "</color></size>", label);
                GUI.Label(new Rect(x, y0 + 48, w, 18), "<size=11><color=#7fcfe0>납부 금액</color></size>", label);
                GUI.Label(new Rect(x, y0 + 62, w, 38), "<size=28><b><color=#e8fbff>" + KNum.Fmt(sim.BillAmount) + "</color></b></size>", label);
                bool blink = Mathf.Repeat(t, 0.8f) < 0.55f;
                GUI.Label(new Rect(x, y0 + 100, w, 20), due ? "<size=13><b><color=" + (blink ? "#ff9b8f" : "#b0564c") + ">오늘 납부일</color></b></size>" : "<size=12><color=#bff4ff>기한 ▸ " + S.billDue + "판</color></size>", label);
                float prog = Mathf.Clamp01((float)(S.cash / System.Math.Max(1, sim.BillAmount)));
                GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.15f * fl); GUI.DrawTexture(new Rect(x, y0 + 126, w, 5), white);
                GUI.color = prog >= 1 ? new Color(0.62f, 0.94f, 0.75f, fl) : new Color(Holo.r, Holo.g, Holo.b, 0.9f * fl); GUI.DrawTexture(new Rect(x, y0 + 126, w * prog, 5), white);
                GUI.color = new Color(1, 1, 1, fl);
                GUI.Label(new Rect(x, y0 + 132, w, 18), "<size=10><color=#7fcfe0>" + Mathf.RoundToInt(prog * 100) + "% 모였다</color></size>", label);
                GUI.Label(new Rect(x, y0 + 150, w, 36), "<size=11><color=#7fcfe0>" + Clip(b.perk, 26) + "</color></size>", small);
                GUI.color = Color.white;
                bool can = S.cash >= sim.BillAmount; double need = sim.BillAmount - S.cash;
                bool loanOk = !can && sim.LoanCap > 0 && need <= sim.LoanCap;
                float bw = (w - 6) / 2;
                if (HoloBtn(new Rect(x, y0 + 190, bw, 32), "납부", Holo, can, fl)) sim.PayBill();
                if (HoloBtn(new Rect(x + bw + 6, y0 + 190, bw, 32), "대출로", HoloRed, loanOk, fl)) RequestLoan(need, true);
                GUI.color = new Color(1, 1, 1, fl);
                string foot = loanOk ? "<color=#ffc2b8>" + KNum.Fmt(need) + " 빌려 납부 · 빚 +" + KNum.Fmt(need * SweepSim.LoanMult) + "</color>" : can ? "<color=#9ff0bf>지금 낼 수 있다</color>" : "<color=#7fcfe0>대출 한도 부족</color>";
                GUI.Label(new Rect(x - 6, y0 + 224, w + 12, 18), "<size=10>" + foot + "</size>", center);
                GUI.color = Color.white;
            }
            GUI.color = Color.white;
            if (dueNag > 0) GUI.Label(new Rect(p.x - 40, p.y - 50, p.width + 80, 20), "<color=#ff9b8f><size=12>납부일 — 먼저 갚거나 · 대출받거나 · 파산</size></color>", center);
        }

        bool HoloBtn(Rect r, string s, Color c, bool on, float fl)
        {
            bool ov = on && r.Contains(Event.current.mousePosition);
            GUI.color = new Color(c.r, c.g, c.b, (on ? (ov ? 0.38f : 0.2f) : 0.04f) * fl); GUI.DrawTexture(r, white);
            Frame(r, new Color(c.r, c.g, c.b, (on ? 0.95f : 0.25f) * fl), ov ? 2 : 1);
            GUI.color = new Color(1, 1, 1, (on ? 1 : 0.35f) * fl);
            GUI.Label(r, "<size=14><b>" + s + "</b></size>", center);
            GUI.color = Color.white;
            return GUI.Button(r, GUIContent.none, GUIStyle.none) && on;
        }

        // ───────────────────────────────── 📈 증권 방
        public const string UpHex = "#ff5c5c", DnHex = "#5494ff";
        static readonly Color UpCol = new Color(1f, 0.36f, 0.36f), DnCol = new Color(0.33f, 0.58f, 1f);
        static string Pct(double v) => (v >= 0 ? "<color=" + UpHex + ">+" : "<color=" + DnHex + ">") + (v * 100).ToString("0.00") + "%</color>";
        static string Tone(double v, string s) => "<color=" + (v > 0 ? UpHex : v < 0 ? DnHex : "#9aa4b4") + ">" + s + "</color>";
        static string Px(double p) => p >= 20 ? p.ToString("#,0.0") : p.ToString("0.00");
        int stTf; int ordSide; float ordFrac = 0.25f;
        static readonly int[] TfN = { 1, 5, 10 };
        static readonly string[] TfName = { "6초", "30초", "1분" };
        static readonly Color PanelC = new Color(0.066f, 0.086f, 0.118f), LineC = new Color(0.13f, 0.16f, 0.21f);
        readonly List<StockCandle> cBuf = new List<StockCandle>(); readonly List<float> vBuf = new List<float>();
        GUIStyle rR;

        void Box(Rect r) { GUI.color = PanelC; GUI.DrawTexture(r, white); Frame(r, LineC, 1); }
        void L(float x, float y, float w, string s) => GUI.Label(new Rect(x, y, w, 22), s, label);
        void Rt(float x, float y, float w, string s) => GUI.Label(new Rect(x, y, w, 22), s, rR);
        static float Hash(float a) => Mathf.Abs(Mathf.Sin(a * 12.9898f) * 43758.5453f) % 1f;
        static float Vol(StockCandle c) => ((c.h - c.l) / Mathf.Max(0.01f, c.c) * 90000f + 400f) * (0.55f + Hash(c.o));   // 가짜 거래량 — 봉이 클수록 많다
        void DotLine(Vector2 a, Vector2 b, Color c, float th)
        {
            GUI.color = c; int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f));
            for (int i = 0; i <= n; i++) { var q = Vector2.Lerp(a, b, i / (float)n); GUI.DrawTexture(new Rect(q.x - th / 2, q.y - th / 2, th, th), white); }
            GUI.color = Color.white;
        }
        double IndexAt(int back)
        {
            double s = 0;
            foreach (var st in sim.Mk.M.st) s += back == 0 || st.hist.Count == 0 ? st.price : st.hist[Mathf.Max(0, st.hist.Count - back)].o;
            return s;
        }

        void StockRoom()
        {
            if (rR == null) rR = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            GUI.color = new Color(0.035f, 0.045f, 0.063f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            if (NavTab(false, "조종실로", "", SweepGame.Amber)) GoFlow(2);
            var mk = sim.Mk; if (mk == null) return;
            var MS = mk.M; var S = sim.S;
            bool open = sim.StockOpen, en = GUI.enabled;
            GUI.enabled = en && open;
            float X0 = ox;
            RowTick(); stockCashPos = new Vector2(X0 + 8 + 780, 28);

            // ── 머리 — 종합지수 · 내 계좌 · 돈 · 다음 봉
            var top = new Rect(X0 + 8, 8, 944, 40); Box(top);
            double idx = IndexAt(0), idx0 = IndexAt(20);
            double tv = mk.TotalValue(), tc = 0; foreach (var st in MS.st) tc += st.cost;
            L(top.x + 12, top.y + 8, 120, "<size=17><b><color=#dde3ea>궤도 증권</color></b></size>");
            L(top.x + 120, top.y + 10, 300, "<size=12><color=#8a9bb3>궤도 종합</color>  <b>" + idx.ToString("#,0.00") + "</b>  " + Tone(idx - idx0, (idx >= idx0 ? "▲ " : "▼ ") + System.Math.Abs(idx - idx0).ToString("0.00")) + " " + Pct(idx / idx0 - 1) + "</size>");
            L(top.x + 420, top.y + 10, 280, "<size=12><color=#8a9bb3>내 주식</color>  <b>" + KNum.Fmt(tv) + "</b>" + (tc > 0 ? "  " + Tone(tv - tc, (tv >= tc ? "+" : "") + KNum.Fmt(tv - tc)) + " " + Pct(tv / tc - 1) : "") + "</size>");
            L(top.x + 720, top.y + 10, 160, "<size=12><color=#8a9bb3>돈</color>  <b><color=#ffdf95>" + KNum.Fmt(S.cash) + "</color></b></size>");
            Rt(top.x, top.y + 10, top.width - 12, "<size=11><color=#8a9bb3>" + (open ? "장중 · 다음 봉 " + Mathf.CeilToInt(Market.CandleSec - MS.candleT) + "초" : "휴장") + "</color></size>");

            // ── 왼쪽 — 종목표 · 속보 · 내부자
            var ls = new Rect(X0 + 8, 54, 214, 538); Box(ls);
            L(ls.x + 8, ls.y + 4, 80, "<size=10><color=#5f6878>종목</color></size>");
            Rt(ls.x, ls.y + 4, ls.width - 62, "<size=10><color=#5f6878>현재가</color></size>");
            Rt(ls.x, ls.y + 4, ls.width - 8, "<size=10><color=#5f6878>등락</color></size>");
            for (int i = 0; i < MS.st.Count; i++)
            {
                var st = MS.st[i]; var d = Market.Defs[i];
                var row = new Rect(ls.x + 4, ls.y + 24 + i * 38, ls.width - 8, 36);
                if (MS.sel == i) { GUI.color = new Color(0.11f, 0.15f, 0.21f); GUI.DrawTexture(row, white); GUI.color = Color.white; }
                else if (row.Contains(Event.current.mousePosition)) { GUI.color = new Color(1, 1, 1, 0.04f); GUI.DrawTexture(row, white); GUI.color = Color.white; }
                RowFx(i, row); if (i < 8) stockRowPos[i] = new Vector2(row.xMax - 40, row.center.y);
                double ch = mk.Change(i, 20);
                string arrow = st.pushLeft > 0 ? (st.push > 0 ? " <color=" + UpHex + ">▲</color>" : " <color=" + DnHex + ">▼</color>") : "";
                L(row.x + 4, row.y + 1, 150, "<size=12>" + (st.shares > 0 ? "<color=#ffdf95>● </color>" : "") + d.name + arrow + "</size>");
                L(row.x + 4, row.y + 18, 150, "<size=9><color=#5f6878>" + d.sector + " · " + d.desc + "</color></size>");
                Rt(row.x, row.y + 1, row.width - 52, "<size=12>" + Tone(ch, Px(st.price)) + "</size>");
                Rt(row.x, row.y + 1, row.width - 4, "<size=11>" + Tone(ch, (ch >= 0 ? "+" : "") + (ch * 100).ToString("0.0") + "%") + "</size>");
                // 작은 선 차트 (최근 20봉)
                float sx = row.x + row.width - 104, sw = 48, sy = row.y + 20, sh = 12;
                int n0 = Mathf.Max(0, st.hist.Count - 20); float lo = float.MaxValue, hi = float.MinValue;
                for (int k = n0; k < st.hist.Count; k++) { lo = Mathf.Min(lo, st.hist[k].c); hi = Mathf.Max(hi, st.hist[k].c); }
                if (st.hist.Count - n0 > 1 && hi > lo)
                    for (int k = n0 + 1; k < st.hist.Count; k++)
                    {
                        float x1 = sx + sw * (k - 1 - n0) / 19f, x2 = sx + sw * (k - n0) / 19f;
                        DotLine(new Vector2(x1, sy + sh - sh * (st.hist[k - 1].c - lo) / (hi - lo)), new Vector2(x2, sy + sh - sh * (st.hist[k].c - lo) / (hi - lo)), ch >= 0 ? new Color(1f, 0.36f, 0.36f, 0.7f) : new Color(0.33f, 0.58f, 1f, 0.7f), 1f);
                    }
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { MS.sel = i; OrbitSfx.Play("tick", 0.4f); }
            }
            float ny = ls.y + 24 + 8 * 38 + 6;
            GUI.color = LineC; GUI.DrawTexture(new Rect(ls.x + 6, ny, ls.width - 12, 1), white); GUI.color = Color.white;
            L(ls.x + 8, ny + 4, 120, "<size=11><b><color=#ff8a7a>속보</color></b></size>");
            for (int k = 0; k < 5 && k < MS.news.Count; k++)
            {
                var nw = MS.news[MS.news.Count - 1 - k];
                float yy = ny + 24 + k * 30;
                string mark = nw.dir > 0 ? "<color=" + UpHex + ">▲</color> " : nw.dir < 0 ? "<color=" + DnHex + ">▼</color> " : "";
                L(ls.x + 8, yy, ls.width - 16, "<size=11>" + mark + (k == 0 ? "<color=#ffdf95>" : "<color=#c8d0dc>") + Clip(nw.head, 15) + "</color></size>");
                L(ls.x + 20, yy + 14, ls.width - 28, "<size=9><color=#5f6878>" + Mathf.Max(0, Mathf.RoundToInt(MS.clock - nw.t)) + "초 전" + (nw.rumor ? " · 소문" : "") + "</color></size>");
            }
            if (MS.news.Count == 0) L(ls.x + 8, ny + 24, ls.width - 16, "<size=11><color=#5f6878>아직 속보 없음</color></size>");
            int il = sim.Lv("a_read");
            if (il > 0 && open)
            {
                var nn = mk.NextNews;
                string tip = "다음 속보 " + Mathf.CeilToInt(mk.NextNewsIn) + "초" + (il >= 2 ? " · " + (nn.up != null ? "<color=" + UpHex + ">오를</color>" : "<color=" + DnHex + ">내릴</color>") + " 쪽: " + Clip(SecName(nn), 10) : "");
                GUI.Label(new Rect(ls.x + 8, ls.yMax - 38, ls.width - 16, 36), "<size=10><color=#e8c77e>내부자</color> " + tip + "</size>", small);
            }

            // ── 가운데 — 봉 차트 · 이동평균 · 거래량
            int si = Mathf.Clamp(MS.sel, 0, MS.st.Count - 1); var ss = MS.st[si]; var sd = Market.Defs[si];
            var cb = new Rect(X0 + 228, 54, 492, 380); Box(cb);
            double sch = mk.Change(si, 20), ref0 = ss.price / (1 + sch);
            L(cb.x + 10, cb.y + 8, 150, "<size=15><b>" + sd.name + "</b></size>");
            L(cb.x + 10, cb.y + 28, 150, "<size=10><color=#5f6878>" + sd.sector + " · " + sd.desc + (sd.div > 0 ? " · 배당" : "") + "</color></size>");
            GUI.Label(new Rect(cb.x + 136, cb.y + 2, 130, 36), "<size=24><b>" + Tone(sch, Px(ss.price)) + "</b></size>", label);
            L(cb.x + 246, cb.y + 12, 130, "<size=11>" + Tone(sch, (sch >= 0 ? "▲ " : "▼ ") + Px(System.Math.Abs(ss.price - ref0))) + " " + Pct(sch) + "</size>");
            for (int k = 0; k < 3; k++)
            {
                var tb = new Rect(cb.xMax - 8 - (3 - k) * 38, cb.y + 8, 36, 22);
                if (stTf == k) { GUI.color = new Color(0.11f, 0.15f, 0.21f); GUI.DrawTexture(tb, white); GUI.color = Color.white; }
                GUI.Label(tb, "<size=11>" + (stTf == k ? "<color=#dde3ea>" : "<color=#5f6878>") + TfName[k] + "</color></size>", center);
                if (GUI.Button(tb, GUIContent.none, GUIStyle.none)) { stTf = k; OrbitSfx.Play("tick", 0.3f); }
            }
            // 봉 묶기 (서고 있는 봉 포함)
            int tf = TfN[stTf];
            cBuf.Clear(); vBuf.Clear();
            int total = ss.hist.Count + 1;
            StockCandle At(int k) => k < ss.hist.Count ? ss.hist[k] : new StockCandle { o = ss.co, h = ss.ch, l = ss.cl, c = (float)ss.price };
            for (int end = total; end > 0 && cBuf.Count < 48; end -= tf)
            {
                int s0 = Mathf.Max(0, end - tf);
                var a = new StockCandle { o = At(s0).o, c = At(end - 1).c, h = float.MinValue, l = float.MaxValue }; float vv = 0;
                for (int k = s0; k < end; k++) { var q = At(k); a.h = Mathf.Max(a.h, q.h); a.l = Mathf.Min(a.l, q.l); vv += Vol(q); }
                cBuf.Insert(0, a); vBuf.Insert(0, vv);
            }
            int n = cBuf.Count;
            float lo2 = float.MaxValue, hi2 = float.MinValue, vmax = 1, vsum = 0; int iHi = 0, iLo = 0;
            for (int k = 0; k < n; k++) { if (cBuf[k].h > hi2) { hi2 = cBuf[k].h; iHi = k; } if (cBuf[k].l < lo2) { lo2 = cBuf[k].l; iLo = k; } vmax = Mathf.Max(vmax, vBuf[k]); vsum += vBuf[k]; }
            L(cb.x + 10, cb.y + 44, 480, "<size=10><color=#8a9bb3>시가 " + Px(cBuf[0].o) + "   고가 <color=" + UpHex + ">" + Px(hi2) + "</color>   저가 <color=" + DnHex + ">" + Px(lo2) + "</color>   거래량 " + KNum.Fmt(vsum) + "     <color=#f2c14e>— 5이평</color>  <color=#b69cff>— 20이평</color>" + (ss.shares > 0 ? "  <color=#ffdf95>- - 내 평균</color>" : "") + "</color></size>");
            float pad = (hi2 - lo2) * 0.08f + 0.01f; float plo = lo2 - pad, phi = hi2 + pad;
            var g = new Rect(cb.x + 8, cb.y + 66, cb.width - 66, 212);
            var vr = new Rect(g.x, g.yMax + 8, g.width, 60);
            float Yp(float v) => g.yMax - g.height * (v - plo) / (phi - plo);
            for (int k = 0; k <= 4; k++)
            {
                float gy = g.y + g.height * k / 4f;
                GUI.color = new Color(1, 1, 1, 0.05f); GUI.DrawTexture(new Rect(g.x, gy, g.width, 1), white); GUI.color = Color.white;
                GUI.Label(new Rect(g.xMax + 4, gy - 9, 54, 18), "<size=10><color=#5f6878>" + Px(phi - (phi - plo) * k / 4f) + "</color></size>", label);
            }
            GUI.color = new Color(1, 1, 1, 0.06f); GUI.DrawTexture(new Rect(vr.x, vr.y - 4, vr.width, 1), white); GUI.color = Color.white;
            float cw = g.width / 48f; int off = 48 - n;
            float Cx(int k) => g.x + (off + k) * cw + cw / 2;
            for (int k = 0; k < n; k++)
            {
                var c = cBuf[k]; float cx = Cx(k); bool up = c.c >= c.o;
                GUI.color = up ? UpCol : DnCol;
                GUI.DrawTexture(new Rect(cx - 0.6f, Yp(c.h), 1.2f, Mathf.Max(1, Yp(c.l) - Yp(c.h))), white);
                float yt = Yp(Mathf.Max(c.o, c.c)), yb = Yp(Mathf.Min(c.o, c.c));
                GUI.DrawTexture(new Rect(cx - cw * 0.36f, yt, cw * 0.72f, Mathf.Max(1.2f, yb - yt)), white);
                GUI.color = up ? new Color(1f, 0.36f, 0.36f, 0.45f) : new Color(0.33f, 0.58f, 1f, 0.45f);
                float vh = vr.height * vBuf[k] / vmax;
                GUI.DrawTexture(new Rect(cx - cw * 0.36f, vr.yMax - vh, cw * 0.72f, vh), white);
            }
            GUI.color = Color.white;
            // 이동평균선
            void MA(int len, Color col)
            {
                Vector2 prev = Vector2.zero; bool has = false;
                for (int k = len - 1; k < n; k++)
                {
                    float s = 0; for (int j = 0; j < len; j++) s += cBuf[k - j].c;
                    var pt = new Vector2(Cx(k), Yp(s / len));
                    if (has) DotLine(prev, pt, col, 1.4f);
                    prev = pt; has = true;
                }
            }
            MA(5, new Color(0.95f, 0.76f, 0.31f, 0.9f)); MA(20, new Color(0.71f, 0.61f, 1f, 0.9f));
            // 최고 · 최저 표시
            if (n > 0)
            {
                GUI.Label(new Rect(Cx(iHi) - 40, Yp(hi2) - 16, 80, 16), "<size=9><color=" + UpHex + ">최고 " + Px(hi2) + "</color></size>", center);
                GUI.Label(new Rect(Cx(iLo) - 40, Yp(lo2) + 1, 80, 16), "<size=9><color=" + DnHex + ">최저 " + Px(lo2) + "</color></size>", center);
            }
            // 내 평균가 점선 · 현재가 꼬리표
            if (ss.shares > 0)
            {
                float ay = Yp((float)(ss.cost / ss.shares));
                if (ay > g.y && ay < g.yMax) { GUI.color = new Color(1f, 0.87f, 0.58f, 0.7f); for (float xx = g.x; xx < g.xMax; xx += 8) GUI.DrawTexture(new Rect(xx, ay, 4, 1), white); GUI.color = Color.white; }
            }
            float py = Yp((float)ss.price);
            GUI.color = sch >= 0 ? UpCol : DnCol; GUI.DrawTexture(new Rect(g.xMax + 2, py - 8, 54, 16), white);
            GUI.color = new Color(1, 1, 1, 0.25f); for (float xx = g.x; xx < g.xMax; xx += 6) GUI.DrawTexture(new Rect(xx, py, 3, 1), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(g.xMax + 2, py - 8, 54, 16), "<size=10><b>" + Px(ss.price) + "</b></size>", center);
            float mins = n * tf * Market.CandleSec / 60f;
            GUI.Label(new Rect(vr.x, vr.yMax + 2, 120, 16), "<size=9><color=#5f6878>" + (mins >= 1 ? Mathf.RoundToInt(mins) + "분 전" : Mathf.RoundToInt(mins * 60) + "초 전") + "</color></size>", label);
            GUI.Label(new Rect(vr.x, vr.yMax + 2, vr.width, 16), "<size=9><color=#5f6878>지금</color></size>", rR);
            GUI.Label(new Rect(vr.xMax + 4, vr.y - 2, 54, 16), "<size=9><color=#5f6878>거래량</color></size>", label);

            // ── 오른쪽 — 호가 · 주문
            var ob = new Rect(X0 + 726, 54, 226, 380); Box(ob);
            L(ob.x + 8, ob.y + 4, 80, "<size=10><color=#5f6878>호가</color></size>");
            Rt(ob.x, ob.y + 4, ob.width - 8, "<size=10><color=#5f6878>잔량</color></size>");
            double tk = ss.price >= 100 ? 0.5 : ss.price >= 20 ? 0.1 : 0.01;
            int seed = ss.hist.Count * 31 + si * 7 + Mathf.FloorToInt(Time.unscaledTime * 1.5f);
            float Q(int k) => 120 + Hash(seed * 0.37f + k * 1.91f) * 1880;
            float qmax = 2000;
            for (int k = 0; k < 5; k++)          // 위 = 팔자 (파랑) — 먼 것부터
            {
                int lv = 5 - k; double pp = System.Math.Ceiling(ss.price / tk) * tk + (lv - 1) * tk;
                var rr = new Rect(ob.x + 4, ob.y + 22 + k * 17, ob.width - 8, 16); float q = Q(lv);
                GUI.color = new Color(0.33f, 0.58f, 1f, 0.16f); GUI.DrawTexture(new Rect(rr.xMax - rr.width * 0.6f * q / qmax, rr.y + 1, rr.width * 0.6f * q / qmax, rr.height - 2), white); GUI.color = Color.white;
                L(rr.x + 6, rr.y - 2, 100, "<size=11>" + Tone(pp - ref0, Px(pp)) + "</size>");
                Rt(rr.x, rr.y - 2, rr.width - 6, "<size=11><color=#c8d0dc>" + Mathf.RoundToInt(q) + "</color></size>");
            }
            var mid = new Rect(ob.x + 4, ob.y + 22 + 5 * 17, ob.width - 8, 20);
            GUI.color = new Color(1, 1, 1, 0.05f); GUI.DrawTexture(mid, white); Frame(mid, sch >= 0 ? UpCol : DnCol, 1);
            GUI.Label(mid, "<size=13><b>" + Tone(sch, Px(ss.price)) + "</b></size>", center);
            for (int k = 0; k < 5; k++)          // 아래 = 사자 (빨강)
            {
                int lv = k + 1; double pp = System.Math.Floor(ss.price / tk) * tk - (lv - 1) * tk;
                var rr = new Rect(ob.x + 4, mid.yMax + 2 + k * 17, ob.width - 8, 16); float q = Q(10 + lv);
                GUI.color = new Color(1f, 0.36f, 0.36f, 0.16f); GUI.DrawTexture(new Rect(rr.x, rr.y + 1, rr.width * 0.6f * q / qmax, rr.height - 2), white); GUI.color = Color.white;
                Rt(rr.x, rr.y - 2, rr.width - 6, "<size=11>" + Tone(pp - ref0, Px(pp)) + "</size>");
                L(rr.x + 6, rr.y - 2, 100, "<size=11><color=#c8d0dc>" + Mathf.RoundToInt(q) + "</color></size>");
            }
            // 주문
            float oy = mid.yMax + 2 + 5 * 17 + 8;
            float hw = (ob.width - 16) / 2;
            for (int k = 0; k < 2; k++)
            {
                var tb = new Rect(ob.x + 8 + k * hw, oy, hw, 26);
                bool on = ordSide == k;
                GUI.color = on ? (k == 0 ? new Color(0.28f, 0.09f, 0.09f) : new Color(0.08f, 0.14f, 0.3f)) : new Color(0.09f, 0.11f, 0.15f); GUI.DrawTexture(tb, white); GUI.color = Color.white;
                GUI.Label(tb, "<size=13><b>" + (on ? (k == 0 ? "<color=#ff8a8a>" : "<color=#8ab4ff>") : "<color=#5f6878>") + (k == 0 ? "매수" : "매도") + "</color></b></size>", center);
                if (GUI.Button(tb, GUIContent.none, GUIStyle.none)) { ordSide = k; OrbitSfx.Play("tick", 0.3f); }
            }
            oy += 32;
            double fee = sim.StockFee;
            double money = System.Math.Floor(S.cash * ordFrac), shBuy = money * (1 - fee) / System.Math.Max(0.01, ss.price);
            double shSell = ss.shares * ordFrac, got = shSell * ss.price * (1 - fee);
            L(ob.x + 10, oy, 80, "<size=11><color=#8a9bb3>" + (ordSide == 0 ? "금액" : "수량") + "</color></size>");
            Rt(ob.x, oy, ob.width - 10, "<size=13><b>" + (ordSide == 0 ? KNum.Fmt(money) : shSell.ToString("0.#") + "주") + "</b></size>");
            oy += 22;
            float fw = (ob.width - 16 - 9) / 4f; float[] fr = { 0.1f, 0.25f, 0.5f, 1f }; string[] fl = { "10%", "25%", "50%", "전부" };
            for (int k = 0; k < 4; k++)
            {
                var fb = new Rect(ob.x + 8 + k * (fw + 3), oy, fw, 24);
                bool on = Mathf.Approximately(ordFrac, fr[k]);
                GUI.color = on ? new Color(0.16f, 0.21f, 0.29f) : new Color(0.09f, 0.11f, 0.15f); GUI.DrawTexture(fb, white); GUI.color = Color.white;
                GUI.Label(fb, "<size=11>" + (on ? "<color=#dde3ea>" : "<color=#8a9bb3>") + fl[k] + "</color></size>", center);
                if (GUI.Button(fb, GUIContent.none, GUIStyle.none)) ordFrac = fr[k];
            }
            oy += 28;
            L(ob.x + 10, oy, ob.width - 20, "<size=10><color=#5f6878>수수료 " + (fee * 100).ToString("0.#") + "%</color></size>");
            Rt(ob.x, oy, ob.width - 10, "<size=10><color=#8a9bb3>" + (ordSide == 0 ? "약 " + shBuy.ToString("#,0.#") + "주" : "받을 돈 " + KNum.Fmt(got)) + "</color></size>");
            oy += 18;
            var go = new Rect(ob.x + 8, oy, ob.width - 16, 34);
            bool can = ordSide == 0 ? money >= 1 : ss.shares > 0;
            bool gov = can && go.Contains(Event.current.mousePosition);
            GUI.color = !can ? new Color(0.12f, 0.13f, 0.16f) : ordSide == 0 ? (gov ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.77f, 0.17f, 0.17f)) : (gov ? new Color(0.25f, 0.5f, 1f) : new Color(0.18f, 0.4f, 0.85f));
            GUI.DrawTexture(go, white); GUI.color = Color.white;
            GUI.Label(go, "<size=15><b>" + (can ? "<color=#ffffff>" : "<color=#5f6878>") + (ordSide == 0 ? "매수" : "매도") + "</color></b></size>", center);
            if (GUI.Button(go, GUIContent.none, GUIStyle.none) && can)
            {
                if (ordSide == 0) TradeBuy(si, ordFrac, go.center);
                else TradeSell(si, ordFrac, go.center);
            }

            // ── 아래 — 내 잔고
            var bb = new Rect(X0 + 228, 440, 724, 152); Box(bb);
            float[] cx2 = { 10, 230, 305, 380, 470, 550, 624 };
            string[] hd = { "내 잔고", "보유", "평균가", "현재가", "평가금액", "손익", "수익률" };
            for (int k = 0; k < hd.Length; k++) { if (k == 0) L(bb.x + cx2[0], bb.y + 4, 120, "<size=10><color=#5f6878>" + hd[0] + "</color></size>"); else Rt(bb.x, bb.y + 4, cx2[k], "<size=10><color=#5f6878>" + hd[k] + "</color></size>"); }
            int rows = 0, more = 0;
            for (int i = 0; i < MS.st.Count; i++)
            {
                var st = MS.st[i]; if (st.shares <= 0) continue;
                if (rows >= 5) { more++; continue; }
                float ry = bb.y + 22 + rows * 20; rows++;
                double val = st.shares * st.price, pl = val - st.cost, avg = st.cost / st.shares;
                var rr = new Rect(bb.x + 4, ry, bb.width - 100, 19);
                if (MS.sel == i) { GUI.color = new Color(0.11f, 0.15f, 0.21f); GUI.DrawTexture(rr, white); GUI.color = Color.white; }
                L(bb.x + cx2[0], ry, 200, "<size=11>" + Market.Defs[i].name + "</size>");
                Rt(bb.x, ry, cx2[1], "<size=11>" + st.shares.ToString("#,0.#") + "주</size>");
                Rt(bb.x, ry, cx2[2], "<size=11>" + Px(avg) + "</size>");
                Rt(bb.x, ry, cx2[3], "<size=11>" + Tone(st.price - avg, Px(st.price)) + "</size>");
                Rt(bb.x, ry, cx2[4], "<size=11>" + KNum.Fmt(val) + "</size>");
                Rt(bb.x, ry, cx2[5], "<size=11>" + Tone(pl, (pl >= 0 ? "+" : "") + KNum.Fmt(pl)) + "</size>");
                Rt(bb.x, ry, cx2[6], "<size=11>" + Pct(val / st.cost - 1) + "</size>");
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) MS.sel = i;
                if (GUI.Button(new Rect(bb.xMax - 90, ry, 82, 18), "<size=10><color=#8ab4ff>전부 팔기</color></size>", btnOff)) TradeSell(i, 1, new Vector2(bb.xMax - 49, ry + 9));
            }
            if (rows == 0) L(bb.x + 10, bb.y + 30, 500, "<size=12><color=#5f6878>보유 종목 없음 — 왼쪽에서 종목을 고르고 오른쪽에서 매수</color></size>");
            if (more > 0) L(bb.x + 10, bb.y + 22 + 5 * 20, 200, "<size=10><color=#5f6878>외 " + more + "종목</color></size>");
            // 🙏 개미의 기도 · 🍀 행운의 부적 (칸을 사야)
            Rt(bb.x, bb.yMax - 24, bb.width - 10, "<size=11>" + LuckLine() + "</size>");

            GUI.enabled = en;
            // 잠김 — 보이긴 하되 흐리게, 여는 길을 알려 준다
            if (!open)
            {
                GUI.color = new Color(0.02f, 0.025f, 0.035f, 0.72f); GUI.DrawTexture(new Rect(X0 + 8, 54, 944, 538), white); GUI.color = Color.white;
                var card = new Rect(X0 + 480 - 180, 230, 360, 150);
                GUI.color = new Color(0.07f, 0.09f, 0.12f); GUI.DrawTexture(card, white); Frame(card, SweepGame.Amber, 2);
                GUI.Label(new Rect(card.x, card.y + 14, card.width, 30), "<size=20><b><color=#ffdf95>잠김 · 증권 계좌 없음</color></b></size>", center);
                GUI.Label(new Rect(card.x, card.y + 48, card.width, 24), "<size=12><color=#c8d0dc>정비고에서 [증권 계좌] 칸을 사면 열린다</color></size>", center);
                if (GUI.Button(new Rect(card.x + 90, card.y + 90, 180, 40), "<size=14>‹ 정비고로</size>", btn)) GoFlow(3);
            }
        }

        // ───────────────────────────────── 09-24 조종실 물건들 (사장님 BBA + 홀로그램 셋)
        static readonly Color HoloPink = new Color(1f, 0.66f, 0.94f), Amber3 = new Color(1f, 0.67f, 0.24f);
        float Flick(float seed) => reduceMotion ? 1 : Mathf.PerlinNoise(Time.unscaledTime * 1.7f, seed) > 0.8f ? 0.5f + 0.4f * Mathf.PerlinNoise(Time.unscaledTime * 40, seed + 1) : 1f;

        /// <summary>홀로그램 바탕 — 원반 · 빛기둥 · 반투명 판 · 주사선 · 꺾쇠</summary>
        void HoloBase(Rect p, float ex, float ey, float emW, Color hc, float fl, bool hover)
        {
            const int N = 30; float step = (ey - p.y) / N;
            for (int i = 0; i < N; i++)
            {
                float k = i / (float)(N - 1), y = ey - 2 - step * (i + 1), bwid = Mathf.Lerp(emW * 0.55f, p.width + 16, k);
                GUI.color = new Color(hc.r, hc.g, hc.b, (0.09f - 0.06f * k) * fl);
                GUI.DrawTexture(new Rect(ex - bwid / 2, y, bwid, step + 0.5f), white);
            }
            float e = emW / 140f;
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(ex - 76 * e, ey - 2, 152 * e, 34 * e), texDisc);
            GUI.color = new Color(0.17f, 0.23f, 0.31f); GUI.DrawTexture(new Rect(ex - 70 * e, ey - 8, 140 * e, 30 * e), texDisc);
            GUI.color = new Color(hc.r * 0.25f, hc.g * 0.25f, hc.b * 0.3f); GUI.DrawTexture(new Rect(ex - 54 * e, ey - 6, 108 * e, 20 * e), texDisc);
            GUI.color = new Color(hc.r, hc.g, hc.b, 0.8f * fl); GUI.DrawTexture(new Rect(ex - 36 * e, ey - 3, 72 * e, 12 * e), texDisc);
            GUI.color = new Color(hc.r, hc.g, hc.b, 0.22f * fl); GUI.DrawTexture(new Rect(ex - 90 * e, ey - 26 * e, 180 * e, 56 * e), texDisc);
            GUI.color = new Color(hc.r * 0.3f, hc.g * 0.4f, hc.b * 0.5f, (hover ? 0.34f : 0.22f) * fl); GUI.DrawTexture(p, white);
            GUI.color = new Color(hc.r, hc.g, hc.b, 0.06f * fl);
            for (float y = p.y + 2; y < p.yMax; y += 4) GUI.DrawTexture(new Rect(p.x, y, p.width, 1), white);
            if (!reduceMotion) { GUI.color = new Color(hc.r, hc.g, hc.b, 0.08f * fl); GUI.DrawTexture(new Rect(p.x, p.y + Mathf.Repeat(Time.unscaledTime * 50 + p.x, p.height - 8), p.width, 8), white); }
            Frame(p, new Color(hc.r, hc.g, hc.b, (hover ? 1f : 0.8f) * fl), hover ? 2f : 1.5f);
            GUI.color = new Color(hc.r, hc.g, hc.b, fl);
            foreach (var c in new[] { new Vector2(p.x, p.y), new Vector2(p.xMax - 10, p.y), new Vector2(p.x, p.yMax - 3), new Vector2(p.xMax - 10, p.yMax - 3) }) GUI.DrawTexture(new Rect(c.x, c.y, 10, 3), white);
            GUI.color = Color.white;
        }

        // 🟦 항로 홀로그램 — 행성 다섯 빛 구슬 · 열린 곳은 누르면 간다 · 판매 중이면 허가증 (두 번)
        void RouteHolo()
        {
            var S = sim.S; var M = sim.M;
            var p = new Rect(ox + 685, 262, 200, 186);
            float fl = Flick(2.1f);
            HoloBase(p, ox + 785, 506, 140, Holo, fl, false);
            GUI.color = new Color(1, 1, 1, fl);
            GUI.Label(new Rect(p.x + 10, p.y + 5, p.width - 20, 20), "<size=12><b><color=#bff4ff>NAV // 항로</color></b></size>", label);
            GUI.Label(new Rect(p.x + 10, p.y + 5, p.width - 20, 20), "<size=11><color=#7fcfe0>지금 " + SweepSim.Orbits[S.orbit].name + "</color></size>", cost);
            int hoverP = -1; float cw = (p.width - 12) / SweepSim.Orbits.Length;
            if (!M.cleanReady)
                for (int i = 0; i < SweepSim.Orbits.Length; i++)
                {
                    var o = SweepSim.Orbits[i];
                    var cell = new Rect(p.x + 6 + i * cw, p.y + 26, cw, 50);
                    bool open = sim.Open(i), sale = sim.OnSale(i);
                    if (cell.Contains(Event.current.mousePosition)) hoverP = i;
                    if (i == S.orbit) { GUI.color = new Color(1f, 0.87f, 0.58f, 0.14f * fl); GUI.DrawTexture(cell, white); Frame(cell, new Color(1f, 0.87f, 0.58f, fl), 1.5f); }
                    var ic = new Rect(cell.center.x - 13, cell.y + 3, 26, 26);
                    GUI.color = new Color(Holo.r, Holo.g, Holo.b, (hoverP == i ? 0.5f : 0.25f) * fl); GUI.DrawTexture(new Rect(ic.x - 5, ic.y - 5, ic.width + 10, ic.height + 10), texDisc);
                    GUI.color = open ? new Color(1, 1, 1, 0.9f * fl) : sale ? new Color(0.6f, 0.65f, 0.7f, 0.8f * fl) : new Color(0.2f, 0.24f, 0.28f, 0.8f * fl);
                    GUI.DrawTexture(ic, PlanetArt.Get(i).texture);
                    if (i == 4) GUI.DrawTexture(new Rect(ic.x - 4, ic.center.y - 1, ic.width + 8, 2), white);
                    GUI.color = new Color(1, 1, 1, fl);
                    string sub = open ? "<color=#dff8ff>" + o.name + "</color>" : sale ? "<color=#ffdf95>" + KNum.Fmt(o.permit) + "</color>" : "<color=#4f7380>잠김</color>";
                    GUI.Label(new Rect(cell.x - 4, cell.y + 30, cell.width + 8, 18), "<size=9>" + sub + "</size>", center);
                    GUI.color = Color.white;
                    if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                    {
                        if (open) { sim.SetOrbit(i); permitArmed = -1; OrbitSfx.Play("tick", 0.5f); }
                        else GoFlow(3);                                          // 항로는 정비고에서 산다 (09-24)
                    }
                }
            GUI.color = new Color(1, 1, 1, fl);
            float x = p.x + 10, w = p.width - 20;
            int show = hoverP >= 0 ? hoverP : S.orbit;
            var so = SweepSim.Orbits[show];
            string st = sim.Open(show) ? "<color=#9ff0bf>열림</color>" : "<color=#ffdf95>정비고 항로 " + KNum.Fmt(so.permit) + " ›</color>";
            if (M.cleanReady) GUI.Label(new Rect(x, p.y + 34, w, 40), "<size=12><color=#bff4ff>청산 출동 — 항로 고정</color></size>", center);
            else
            {
                GUI.Label(new Rect(x, p.y + 80, w, 20), "<size=12><b><color=#ffdf95>" + so.name + " ×" + so.mult + "</color></b>  " + st + "</size>", label);
                GUI.Label(new Rect(x, p.y + 98, w, 30), "<size=10><color=#7fcfe0>" + so.desc + "</color></size>", small);
            }
            GUI.color = new Color(Holo.r, Holo.g, Holo.b, 0.35f * fl);
            for (float xx = x; xx < x + w; xx += 6) GUI.DrawTexture(new Rect(xx, p.y + 130, 3, 1), white);
            GUI.color = new Color(1, 1, 1, fl);
            var ct = sim.CurContract;
            if (ct != null && !M.cleanReady)
            {
                GUI.Label(new Rect(x, p.y + 134, w - (S.rerolled ? 0 : 46), 32), "<size=10><color=#7fcfe0>의뢰</color> <color=#dff8ff>" + ct.Value.text + "</color></size>", small);
                GUI.Label(new Rect(x, p.y + 162, w, 18), "<size=10><color=#7fcfe0>성공하면 판 수입 +" + (25 + 10 * sim.Lv("e_quest")) + "%</color></size>", label);
                GUI.color = Color.white;
                if (!S.rerolled && HoloBtn(new Rect(p.xMax - 52, p.y + 136, 44, 20), "<size=10>바꾸기</size>", Holo, true, fl)) sim.Reroll();
            }
            else GUI.Label(new Rect(x, p.y + 140, w, 36), "<size=10><color=#7fcfe0>의뢰는 청구서 2 뒤에 들어온다</color></size>", small);
            GUI.color = Color.white;
        }

        // 🩷 복권 홀로그램 (출동 버튼 오른쪽) — 누르면 복권 창
        void LottoHolo()
        {
            var p = new Rect(ox + 588, 404, 108, 92);
            bool ov = p.Contains(Event.current.mousePosition);
            float fl = Flick(5.3f);
            HoloBase(p, ox + 642, 524, 100, HoloPink, fl, ov);
            GUI.color = new Color(1, 1, 1, fl);
            GUI.Label(new Rect(p.x, p.y + 6, p.width, 18), "<size=11><b><color=#ffd6f7>SCRATCH</color></b></size>", center);
            GUI.Label(new Rect(p.x, p.y + 26, p.width, 32), "<size=22><b><color=#ffe8fb>복권</color></b></size>", center);
            GUI.Label(new Rect(p.x, p.y + 62, p.width, 18), "<size=10><color=#ffb8ee>즉석 복권 " + sim.ScratchLeft + "장</color></size>", center);
            GUI.color = Color.white;
            if (GUI.Button(p, GUIContent.none, GUIStyle.none)) { lottoOpen = true; OrbitSfx.Play("tick", 0.6f); }
        }

        // 📺 출동 보고 — 초록 브라운관 (B) · 최근 출동 여섯 번 벌이 막대
        void CrtReport(Rect r)
        {
            var S = sim.S;
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = new Color(0.2f, 0.23f, 0.18f); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.3f, 0.34f, 0.26f), 2);
            var sc = new Rect(r.x + 7, r.y + 7, r.width - 14, r.height - 14);
            GUI.color = new Color(0.045f, 0.07f, 0.045f); GUI.DrawTexture(sc, white);
            GUI.color = new Color(0.55f, 1f, 0.6f, 0.05f); GUI.DrawTexture(new Rect(sc.x + 10, sc.y + 8, sc.width - 20, sc.height - 16), texDisc);
            GUI.color = Color.white;
            bool cur = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
            float x = sc.x + 6, w = sc.width - 12;
            GUI.Label(new Rect(x, sc.y + 2, w, 18), "<size=11><color=#8dff9a>> 출동 #" + S.runs + " 기록</color></size>", label);
            if (S.lastBroke < 0) GUI.Label(new Rect(x, sc.y + 24, w, 40), "<size=11><color=#8dff9a>기록 없음" + (cur ? "▮" : "") + "</color></size>", label);
            else
            {
                var h = S.runEarn; int n = h.Count; double mx = 1; foreach (var v in h) mx = System.Math.Max(mx, v);
                var bar = new Rect(x, sc.y + 22, w, 38); float bw = (w - 5 * 3) / 6f;
                for (int i = 0; i < 6; i++)
                {
                    int k = n - 6 + i; if (k < 0) continue;
                    float bh = Mathf.Max(2, bar.height * (float)(h[k] / mx));
                    GUI.color = new Color(0.55f, 1f, 0.6f, k == n - 1 ? 0.95f : 0.45f);
                    GUI.DrawTexture(new Rect(bar.x + i * (bw + 3), bar.yMax - bh, bw, bh), white);
                }
                GUI.color = Color.white;
                double lE = S.lastClaw + S.lastDrone + S.lastBlast;
                GUI.Label(new Rect(x, sc.y + 62, w, 18), "<size=10><color=#6fd67a>부순것 " + S.lastBroke + " · 연쇄 " + S.lastChain + (S.lastContract == 1 ? " · 의뢰 ○" : S.lastContract == 2 ? " · 의뢰 ✕" : "") + "</color></size>", label);
                GUI.Label(new Rect(x, sc.y + 78, w, 22), "<size=14><b><color=#b8ffc0>+" + KNum.Fmt(lE) + "</color></b><color=#8dff9a>" + (cur ? " ▮" : "") + "</color></size>", label);
            }
            GUI.color = new Color(0, 0, 0, 0.28f);
            for (float y = sc.y; y < sc.yMax; y += 3) GUI.DrawTexture(new Rect(sc.x, y, sc.width, 1), white);
            GUI.color = Color.white;
        }

        // 🟧 궤도일보 — LED 전광판 (B) · 글자가 흘러간다 · 누르면 신문
        GUIStyle ledSt, ledR;
        void LedNews(Rect r)
        {
            var S = sim.S; var M = sim.M;
            if (ledSt == null) { ledSt = new GUIStyle(label) { wordWrap = false, clipping = TextClipping.Overflow }; ledR = new GUIStyle(label) { alignment = TextAnchor.UpperRight }; }
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = new Color(0.045f, 0.035f, 0.028f); GUI.DrawTexture(r, white);
            Frame(r, ov ? Amber3 : new Color(0.17f, 0.15f, 0.13f), 3);
            int unread = sim.Unread;
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 18), "<size=10><color=#ffab3d>ORBIT NEWS · " + S.runs + "일째</color></size>", label);
            if (unread > 0) GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 18), "<size=10><color=#ff5a3a>●</color> <color=#ffab3d>새 " + unread + "</color></size>", ledR);
            string a = M.news.Count > 0 ? (unread > 0 ? "속보 ▸ " : "") + M.news[M.news.Count - 1].head : "오늘은 조용하다";
            string b = M.news.Count > 1 ? M.news[M.news.Count - 2].head + (M.news.Count > 2 ? "  ▸  " + M.news[M.news.Count - 3].head : "") : "궤도 청소부 영업 중";
            LedStrip(new Rect(r.x + 8, r.y + 26, r.width - 16, 34), a, 15, 38f);
            LedStrip(new Rect(r.x + 8, r.y + 66, r.width - 16, 26), b, 12, 26f);
            GUI.Label(new Rect(r.x + 10, r.yMax - 26, r.width - 20, 20), "<size=10><color=" + (ov ? "#ffdf95" : "#7a6a55") + ">누르면 신문 ▸</color></size>", label);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { newsOpen = true; newsSel = -1; }
        }
        void LedStrip(Rect s, string text, int size, float speed)
        {
            GUI.color = new Color(0.085f, 0.04f, 0.015f); GUI.DrawTexture(s, white);
            GUI.color = new Color(1f, 0.67f, 0.24f, 0.05f);
            for (float xx = s.x + 1; xx < s.xMax; xx += 3) GUI.DrawTexture(new Rect(xx, s.y, 1, s.height), white);
            GUI.color = Color.white;
            string t = "<size=" + size + "><b><color=#ffab3d>" + text + "</color></b></size>";
            float tw = ledSt.CalcSize(new GUIContent(t)).x;
            float off = reduceMotion ? 4 : s.width - Mathf.Repeat(Time.unscaledTime * speed, s.width + tw + 20);
            GUI.BeginGroup(s);
            GUI.color = new Color(1f, 0.55f, 0.1f, 0.25f); GUI.Label(new Rect(off, (s.height - size - 8) / 2 + 1, tw + 10, size + 10), t, ledSt);
            GUI.color = Color.white; GUI.Label(new Rect(off, (s.height - size - 8) / 2, tw + 10, size + 10), t, ledSt);
            GUI.EndGroup();
        }

        // 🟨 회사 명판 — 황동판 · 파산은 빨간 안전덮개 (A)
        void BrassPlate(Rect r)
        {
            var S = sim.S; var M = sim.M;
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), white);
            GUI.color = new Color(0.66f, 0.51f, 0.22f); GUI.DrawTexture(r, white);
            GUI.color = new Color(0.9f, 0.77f, 0.46f, 0.7f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.38f), white);
            GUI.color = new Color(1f, 0.95f, 0.8f, 0.6f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), white);
            GUI.color = new Color(0.38f, 0.28f, 0.1f); GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), white);
            foreach (var sp in new[] { new Vector2(r.x + 4, r.y + 4), new Vector2(r.xMax - 10, r.y + 4), new Vector2(r.x + 4, r.yMax - 10), new Vector2(r.xMax - 10, r.yMax - 10) })
            { GUI.color = new Color(0.4f, 0.3f, 0.12f); GUI.DrawTexture(new Rect(sp.x, sp.y, 6, 6), texDisc); GUI.color = new Color(1, 1, 1, 0.35f); GUI.DrawTexture(new Rect(sp.x + 1, sp.y + 1, 2, 2), texDisc); }
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 60, 22), "<size=13><b><color=#3b2a08>궤도 청소부 " + M.company + "대</color></b></size>", label);
            GUI.Label(new Rect(r.x + 14, r.y + 26, r.width - 60, 20), "<size=10><color=#4a360c>쌓인 신용 +" + S.creditPending + "</color></size>", label);
            var cv = new Rect(r.xMax - 48, r.y + 7, 36, r.height - 14);
            bool can = sim.CanBankrupt, ov = can && cv.Contains(Event.current.mousePosition);
            if (!can)
            {
                GUI.color = new Color(0.25f, 0.2f, 0.12f); GUI.DrawTexture(cv, white); Frame(cv, new Color(0.35f, 0.27f, 0.12f), 1);
                GUI.Label(cv, "<size=9><color=#8a6a30>잠김</color></size>", center);
            }
            else if (!bankruptArmed)
            {
                GUI.color = new Color(0.2f, 0.05f, 0.04f); GUI.DrawTexture(cv, white);
                GUI.color = new Color(1f, 0.25f, 0.18f, ov ? 0.7f : 0.5f); GUI.DrawTexture(cv, white);
                GUI.color = new Color(0, 0, 0, 0.22f); for (float yy = cv.y + 3; yy < cv.yMax; yy += 6) GUI.DrawTexture(new Rect(cv.x, yy, cv.width, 2), white);
                Frame(cv, new Color(0.75f, 0.15f, 0.1f), ov ? 2 : 1.5f);
                GUI.Label(cv, "<size=10><b><color=#ffe0dc>파산</color></b></size>", center);
                if (GUI.Button(cv, GUIContent.none, GUIStyle.none)) { bankruptArmed = true; OrbitSfx.Play("tick", 0.7f, 0.2f, 0.02f); }
            }
            else
            {
                GUI.color = new Color(1f, 0.25f, 0.18f, 0.5f); GUI.DrawTexture(new Rect(cv.x, cv.y - 12, cv.width, 8), white);
                GUI.color = new Color(0.12f, 0.03f, 0.03f); GUI.DrawTexture(cv, white);
                float gl = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8);
                GUI.color = new Color(1f, 0.2f, 0.15f, gl); GUI.DrawTexture(new Rect(cv.x + 6, cv.y + 5, cv.width - 12, cv.height - 10), texDisc);
                GUI.color = Color.white;
                GUI.Label(new Rect(cv.x - 30, cv.yMax + 1, cv.width + 36, 16), "<size=9><color=#ff9b8f>한 번 더 → 파산</color></size>", center);
                if (GUI.Button(cv, GUIContent.none, GUIStyle.none)) { sim.Bankrupt(); bankruptArmed = false; showResult = false; flow = 2; }
            }
            GUI.color = Color.white;
        }

        // ───────────────────────────────── 💸 빚 갚기 연출 (09-24 사장님 시안 확정)
        // 돈 → 빚 명세서로 동전 줄기 (닿을 때마다 짤랑 · 숫자 도르르) → 「상환」 도장 쾅 → 다 갚으면 「완납」 + 명세서가 부서지고 번쩍 · 큰 「완납!」
        // 판 끝 자동 상환(수입 30%)은 결산 화면에서 작은 명세서로 짧게
        float rpAt = -1; double rpFrom, rpTo, rpOwed; bool rpFull, rpMini, rpStamped, rpBoomed; int rpN, rpHit;
        readonly Vector4[] rpPix = new Vector4[70];
        const float RpGap = 0.07f, RpFly = 0.42f;
        public void Repay()
        {
            double before = sim.S.debt;
            if (!sim.RepayDebt()) return;
            RepayFx(before, sim.S.debt, 0);
        }
        void RepayFx(double from, double to, float delay, bool mini = false)
        {
            rpFrom = from; rpTo = System.Math.Max(0, to); rpFull = to <= 0.5; rpMini = mini && !rpFull;
            rpAt = Time.unscaledTime + delay; rpHit = 0; rpStamped = rpBoomed = false;
            rpN = rpMini ? 6 : rpFull ? 22 : 14;
            rpOwed = 0; if (sim.S.loanLog != null) foreach (var l in sim.S.loanLog) if (l.kind == 0) rpOwed += l.amt * SweepSim.LoanMult;
            rpOwed = System.Math.Max(rpOwed, rpFrom);
            for (int i = 0; i < rpPix.Length; i++) rpPix[i] = new Vector4(Random.value, Random.value, (Random.value - 0.5f) * 420, -Random.value * 320);
        }
        void RepayOverlay()
        {
            if (rpAt < 0) return;
            float t = Time.unscaledTime - rpAt; if (t < 0) return;
            float tLast = 0.25f + (rpN - 1) * RpGap + RpFly, tStamp = tLast + 0.05f, tBoom = tStamp + 0.55f;
            float end = rpMini ? tLast + 1.3f : rpFull ? tBoom + 1.9f : tStamp + 1.3f;
            if (t > end) { rpAt = -1; return; }
            float alpha = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((end - t) / 0.35f);
            var sh = rpMini ? new Rect(vw / 2 - 130, 64, 260, 70) : new Rect(vw / 2 - 160, 170, 320, 170);
            Vector2 from = rpMini ? new Vector2(vw / 2, 330) : new Vector2(vw / 2, 16);
            Vector2 to = new Vector2(sh.center.x, sh.y + (rpMini ? 42 : 72));
            int arrived = 0; float lastHit = -9;
            for (int i = 0; i < rpN; i++) { float ta = 0.25f + i * RpGap + RpFly; if (t >= ta) { arrived++; lastHit = ta; } }
            while (rpHit < arrived) { rpHit++; OrbitSfx.PlayPitch("pick", 0.45f, 0.9f + rpHit * 0.035f); }
            double cur = rpFrom - (rpFrom - rpTo) * arrived / rpN;
            bool gone = rpFull && t > tBoom;
            if (!rpMini) { GUI.color = new Color(0, 0, 0, 0.5f * alpha); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); }
            if (!gone)
            {
                float jolt = t - lastHit < 0.06f ? Random.Range(-3f, 3f) : 0;
                var s = new Rect(sh.x + jolt, sh.y + jolt * 0.5f, sh.width, sh.height);
                GUI.color = new Color(0.35f, 0.07f, 0.06f, 0.75f * alpha); GUI.DrawTexture(s, white);
                GUI.color = new Color(HoloRed.r, HoloRed.g, HoloRed.b, 0.07f * alpha);
                for (float y = s.y + 2; y < s.yMax; y += 4) GUI.DrawTexture(new Rect(s.x, y, s.width, 1), white);
                Frame(s, new Color(HoloRed.r, HoloRed.g, HoloRed.b, 0.9f * alpha), 1.5f);
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(new Rect(s.x + 14, s.y + 8, s.width - 28, 20), "<size=12><b><color=#ffd0c8>" + (rpMini ? "자동 상환 · 판 수입 30%" : "KESSLER // 빚 명세서") + "</color></b></size>", label);
                GUI.Label(new Rect(s.x + 14, s.y + 8, s.width - 28, 20), "<size=11><color=#ffb3a8>−" + KNum.Fmt(rpFrom - rpTo) + "</color></size>", cost);
                if (rpMini) GUI.Label(new Rect(s.x + 14, s.y + 28, s.width - 28, 34), "<size=24><b><color=#ffffff>빚 " + KNum.Fmt(cur) + "</color></b></size>", label);
                else
                {
                    GUI.Label(new Rect(s.x + 14, s.y + 34, s.width - 28, 56), "<size=42><b><color=#ffffff>" + KNum.Fmt(cur) + "</color></b></size>", label);
                    float paid = Mathf.Clamp01((float)(1 - cur / rpOwed));
                    GUI.color = new Color(HoloRed.r, HoloRed.g, HoloRed.b, 0.2f * alpha); GUI.DrawTexture(new Rect(s.x + 14, s.y + 104, s.width - 28, 9), white);
                    GUI.color = new Color(0.62f, 0.94f, 0.75f, alpha); GUI.DrawTexture(new Rect(s.x + 14, s.y + 104, (s.width - 28) * paid, 9), white);
                    GUI.color = new Color(1, 1, 1, alpha);
                    GUI.Label(new Rect(s.x + 14, s.y + 118, s.width - 28, 20), "<size=11><color=#ffd0c8>갚은 비율</color></size>", label);
                    GUI.Label(new Rect(s.x + 14, s.y + 118, s.width - 28, 20), "<size=11><color=#9ff0bf>" + Mathf.RoundToInt(paid * 100) + "%</color></size>", cost);
                }
                // 도장
                if (!rpMini && t > tStamp)
                {
                    if (!rpStamped) { rpStamped = true; OrbitSfx.Play(rpFull ? "buy" : "grab", 0.9f); game.shake = Mathf.Max(game.shake, 0.08f); }
                    float k = Mathf.Clamp01((t - tStamp) / 0.3f); float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);   // 튕기며 내려앉는다
                    float sc = Mathf.Lerp(3f, 1f, e);
                    var c = new Vector2(s.xMax - 64, s.y + 62); var sr = new Rect(c.x - 50 * sc, c.y - 20 * sc, 100 * sc, 40 * sc);
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(-12, c);
                    Frame(sr, new Color(1f, 0.29f, 0.23f, Mathf.Min(1, k * 2) * 0.95f * alpha), 4 * sc);
                    GUI.color = new Color(1, 1, 1, Mathf.Min(1, k * 2) * alpha);
                    GUI.Label(sr, "<size=" + Mathf.RoundToInt(26 * sc) + "><b><color=#ff4a3a>" + (rpFull ? "완납" : "상환") + "</color></b></size>", center);
                    GUI.matrix = m;
                }
            }
            // 동전
            for (int i = 0; i < rpN; i++)
            {
                float tc = t - (0.25f + i * RpGap); if (tc < 0 || tc >= RpFly) continue;
                float k = tc / RpFly, u = 1 - k;
                float sx = from.x + ((i * 37) % 30 - 15), sy = from.y;
                float mx = (sx + to.x) / 2 + ((i * 53) % 120 - 60), my = Mathf.Min(sy, to.y) - 40;
                var p = new Vector2(u * u * sx + 2 * u * k * mx + k * k * to.x, u * u * sy + 2 * u * k * my + k * k * to.y);
                GUI.color = new Color(1f, 0.82f, 0.4f, 0.35f); GUI.DrawTexture(new Rect(p.x - 12, p.y - 12, 24, 24), texDisc);
                GUI.color = new Color(1f, 0.85f, 0.45f); GUI.DrawTexture(new Rect(p.x - 7, p.y - 7, 14, 14), texDisc);
                GUI.color = new Color(1f, 0.95f, 0.75f); GUI.DrawTexture(new Rect(p.x - 4, p.y - 5, 5, 5), texDisc);
            }
            // 완납 — 번쩍 · 명세서가 빨간 픽셀로 흩어짐 · 큰 「완납!」
            if (gone)
            {
                float tt = t - tBoom;
                if (!rpBoomed) { rpBoomed = true; OrbitSfx.Play("launch", 0.7f); game.flash = Mathf.Max(game.flash, 0.6f); game.shake = Mathf.Max(game.shake, 0.15f); }
                for (int i = 0; i < rpPix.Length; i++)
                {
                    var q = rpPix[i]; float a = 1 - tt / 1.2f; if (a <= 0) break;
                    float x = sh.x + q.x * sh.width + q.z * tt, y = sh.y + q.y * sh.height + q.w * tt + 520 * tt * tt;
                    GUI.color = new Color(1f, 0.55f, 0.48f, a); GUI.DrawTexture(new Rect(x, y, 6, 6), white);
                }
                float k = Mathf.Clamp01(tt / 0.25f), sc = tt < 0.25f ? Mathf.Lerp(0.4f, 1.15f, k) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((tt - 0.25f) / 0.2f));
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((end - t) / 0.4f));
                GUI.Label(new Rect(0, RefH * 0.42f - 50 * sc, vw, 100 * sc), "<size=" + Mathf.RoundToInt(64 * sc) + "><b><color=#ffdf95>완납!</color></b></size>", center);
                GUI.Label(new Rect(0, RefH * 0.42f + 48, vw, 24), "<size=14><color=#ffe9b0>케슬러 금융에 진 빚을 다 갚았다</color></size>", center);
            }
            GUI.color = Color.white;
        }

        string LuckLine()
        {
            int p = sim.Lv("a_auto"), c = sim.Lv("a_ins");
            return (p > 0 ? "<color=#ffdf95>개미의 기도</color> <color=" + UpHex + ">내 종목 ↑</color>" : "<color=#3f4652>개미의 기도 · 잠김</color>") + "   " +
                   (c > 0 ? "<color=#9ff0bf>행운의 부적 " + c + "</color> <color=#8a9bb3>나쁜 속보 " + new[] { 0, 60, 70, 80 }[Mathf.Min(3, c)] + "% 피하기</color>" : "<color=#3f4652>행운의 부적 · 잠김</color>");
        }

        // ───────────────────────────────── ✨ 칸을 샀을 때 — 퍼지는 고리 · 불꽃 · 해금 칸이면 「해금!」 (09-24)
        struct BuyBurst { public Vector2 p; public Color c; public float t0; public bool big; public string txt; }
        readonly List<BuyBurst> bursts = new List<BuyBurst>();
        void BuyFx(Vector2 p, Color c, bool big, string txt = "해금!") { bursts.Add(new BuyBurst { p = p, c = c, t0 = Time.unscaledTime, big = big, txt = txt }); if (big) OrbitSfx.Play("launch", 0.35f); }
        void BuyFxDraw()
        {
            float now = Time.unscaledTime;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i]; float t = now - b.t0, L = b.big ? 1.1f : 0.6f;
                if (t > L) { bursts.RemoveAt(i); continue; }
                float k = t / L, a = 1 - k, R = (b.big ? 90 : 46) * Mathf.Sqrt(k);
                GUI.color = new Color(b.c.r, b.c.g, b.c.b, a * 0.8f);
                int seg = 28;
                for (int s = 0; s < seg; s++) { float an = s * Mathf.PI * 2 / seg; GUI.DrawTexture(new Rect(b.p.x + Mathf.Cos(an) * R - 2, b.p.y + Mathf.Sin(an) * R - 2, 4, 4), white); }
                for (int s = 0; s < (b.big ? 14 : 8); s++) { float an = s * 2.39996f, d = R * (0.6f + 0.5f * ((s * 37) % 10) / 10f); GUI.color = new Color(1f, 0.93f, 0.7f, a); GUI.DrawTexture(new Rect(b.p.x + Mathf.Cos(an) * d - 1.5f, b.p.y + Mathf.Sin(an) * d - 1.5f, 3, 3), white); }
                if (b.big) { GUI.color = new Color(1, 1, 1, Mathf.Clamp01(a * 1.5f)); GUI.Label(new Rect(b.p.x - 80, b.p.y - 46 - 30 * k, 160, 30), "<size=18><b><color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(b.c, Color.white, 0.4f)) + ">" + b.txt + "</color></b></size>", center); }
            }
            GUI.color = Color.white;
        }

        // ⚔ 무기 단추 — 정비고 왼쪽 위. 산 무기만 (무기고를 사야 보인다)
        int weaponDrop = -1;                                                  // 펼친 목록 — 0 주 무기 · 1 보조 무기
        /// <summary>⚔ 무기 고르기 — 평소엔 「무기 ▾」 한 칸, 누르면 산 무기 목록이 펼쳐진다 (아홉 개가 트리를 덮지 않게)</summary>
        void WeaponBar(Rect r)
        {
            if (sim.Lv("w_hub") <= 0) { weaponDrop = -1; return; }
            bool two = sim.Lv("w_slot2") > 0;
            for (int row = 0; row < (two ? 2 : 1); row++)
            {
                var b = new Rect(r.x, r.y + row * (r.height + 4), 190, r.height);
                bool ov = b.Contains(Event.current.mousePosition), open = weaponDrop == row;
                Color ac = row == 0 ? new Color(1f, 0.55f, 0.5f) : KeyCol;
                GUI.color = open ? new Color(ac.r * 0.3f, ac.g * 0.2f, ac.b * 0.25f) : new Color(0.1f, 0.08f, 0.09f); GUI.DrawTexture(b, white);
                Frame(b, ov || open ? ac : new Color(ac.r * 0.5f, ac.g * 0.45f, ac.b * 0.5f), ov || open ? 2 : 1);
                int cur = row == 0 ? sim.Weapon : sim.S.weapon2;
                string nm = cur < 0 ? "없음" : SweepSim.WeaponName[cur];
                GUI.Label(new Rect(b.x + 8, b.y + 5, 60, 20), "<size=11><color=#" + ColorUtility.ToHtmlStringRGB(ac) + ">" + (row == 0 ? "무기" : "보조") + "</color></size>", label);
                GUI.Label(new Rect(b.x + 44, b.y + 4, b.width - 60, 22), "<size=13><b><color=#ffe0da>" + nm + "</color></b></size>", label);
                GUI.Label(new Rect(b.xMax - 22, b.y + 4, 18, 22), "<size=12><color=#8a9bb3>" + (open ? "▴" : "▾") + "</color></size>", label);
                if (GUI.Button(b, GUIContent.none, GUIStyle.none)) { weaponDrop = open ? -1 : row; OrbitSfx.Play("tick", 0.4f); }
            }
            if (weaponDrop < 0) return;
            // 펼친 목록 — 산 무기만
            float y = r.y + (two ? 2 : 1) * (r.height + 4) + 2;
            var items = new List<int>();
            if (weaponDrop == 1) items.Add(-1);
            for (int w = 0; w < SweepSim.WeaponName.Length; w++) if (sim.WeaponOwned(w) && !(weaponDrop == 1 && w == sim.Weapon)) items.Add(w);
            var box = new Rect(r.x, y, 190, items.Count * 26 + 6);
            GUI.color = new Color(0.06f, 0.05f, 0.06f, 0.97f); GUI.DrawTexture(box, white); Frame(box, new Color(0.35f, 0.25f, 0.24f), 1); GUI.color = Color.white;
            for (int k = 0; k < items.Count; k++)
            {
                int w = items[k]; var it = new Rect(box.x + 3, box.y + 3 + k * 26, box.width - 6, 24);
                bool on = weaponDrop == 0 ? sim.Weapon == w : sim.S.weapon2 == w, ov = it.Contains(Event.current.mousePosition);
                if (on || ov) { GUI.color = on ? new Color(0.3f, 0.12f, 0.1f) : new Color(1, 1, 1, 0.06f); GUI.DrawTexture(it, white); GUI.color = Color.white; }
                string desc = w < 0 ? "" : w == 0 ? "원형 타격" : SweepSim.Nodes[System.Array.IndexOf(SweepSim.IconOrder, SweepSim.WeaponNode[w]) >= 0 ? NodeIndex(SweepSim.WeaponNode[w]) : 0].desc;
                GUI.Label(new Rect(it.x + 8, it.y + 3, 90, 20), "<size=12>" + (on ? "<b><color=#ffd0c8>" : "<color=#d8c8c0>") + (w < 0 ? "없음" : SweepSim.WeaponName[w]) + (on ? "</color></b>" : "</color>") + "</size>", label);
                if (w >= 0) GUI.Label(new Rect(it.x + 82, it.y + 5, it.width - 88, 18), "<size=9><color=#6f6268>" + Clip(desc, 16) + "</color></size>", label);
                if (!on && GUI.Button(it, GUIContent.none, GUIStyle.none))
                {
                    bool ok = weaponDrop == 0 ? sim.Equip(w) : sim.Equip2(w);
                    if (ok) { OrbitSfx.Play("grab", 0.7f); BuyFx(it.center, weaponDrop == 0 ? new Color(1f, 0.55f, 0.5f) : KeyCol, false); weaponDrop = -1; }
                }
            }
        }
        static int NodeIndex(string id) { for (int i = 0; i < SweepSim.Nodes.Length; i++) if (SweepSim.Nodes[i].id == id) return i; return 0; }

        // ★ 1면 조작 — 조종실 창 위에 신문 두 장. 고른 기사가 증권 속보로 나간다
        void FrontPick()
        {
            var S = sim.S;
            if (S.front1 < 0 || S.front2 < 0 || sim.Mk == null) return;
            var w = new Rect(ox + 250, 52, 460, 170);
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(w.x + 4, w.y + 6, w.width, w.height), white);
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.96f); GUI.DrawTexture(w, white); Frame(w, new Color(1f, 0.36f, 0.81f), 2);
            GUI.color = Color.white;
            GUI.Label(new Rect(w.x, w.y + 6, w.width, 22), "<size=14><b><color=#ffb3ea>★ 내일 궤도일보 1면을 고른다</color></b></size>", center);
            for (int k = 0; k < 2; k++)
            {
                var nd = Market.NewsBook[k == 0 ? S.front1 : S.front2];
                var r = new Rect(w.x + 14 + k * 222, w.y + 34, 210, 122);
                bool ov = r.Contains(Event.current.mousePosition);
                GUI.color = ov ? new Color(0.98f, 0.95f, 0.88f) : new Color(0.93f, 0.9f, 0.83f); GUI.DrawTexture(r, white);
                GUI.color = new Color(0.1f, 0.1f, 0.1f); GUI.DrawTexture(new Rect(r.x + 8, r.y + 24, r.width - 16, 2), white); GUI.color = Color.white;
                paperSmall.fontSize = 10; GUI.Label(new Rect(r.x + 8, r.y + 3, r.width - 16, 20), "궤도일보 1면", paperSmall);
                paperBody.fontSize = 13; GUI.Label(new Rect(r.x + 8, r.y + 30, r.width - 16, 40), "<b>" + nd.head.Replace("[소문] ", "") + "</b>", paperBody);
                string eff = (nd.up != null ? "<color=#b3261e>▲ " + SecName(new NewsDef { up = nd.up }) + "</color>  " : "") + (nd.down != null ? "<color=#1f4fb3>▼ " + SecName(new NewsDef { down = nd.down }) + "</color>" : "");
                paperBody.fontSize = 11; GUI.Label(new Rect(r.x + 8, r.y + 78, r.width - 16, 40), eff, paperBody);
                paperBody.fontSize = 14;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { sim.PickFront(k); OrbitSfx.Play("buy", 0.8f); BuyFx(r.center, new Color(1f, 0.36f, 0.81f), true, "1면 확정!"); }
            }
        }
    }
}
