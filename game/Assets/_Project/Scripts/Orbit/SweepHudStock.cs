using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 📈 증권 방 — 종합지수 · 종목표 · 봉 차트 · 호가 · 주문 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 📈 증권 방
        public const string UpHex = "#ff5c5c", DnHex = "#5494ff";
        static readonly Color UpCol = new Color(1f, 0.36f, 0.36f), DnCol = new Color(0.33f, 0.58f, 1f);
        static string Pct(double v) => (v >= 0 ? "<color=" + UpHex + ">+" : "<color=" + DnHex + ">") + (v * 100).ToString("0.00") + "%</color>";
        static string Tone(double v, string s) => "<color=" + (v > 0 ? UpHex : v < 0 ? DnHex : "#9aa4b4") + ">" + s + "</color>";
        static string Px(double p) => p >= 20 ? p.ToString("#,0.0") : p.ToString("0.00");
        int stTf; int ordSide; float ordFrac = 0.25f;
        static readonly int[] TfN = { 1, 5, 10 };
        static readonly string[] TfName = { Loc.T("6초"), Loc.T("30초"), Loc.T("1분") };
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

        int holdScroll;
        void StockRoom()
        {
            if (rR == null) rR = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            GUI.color = Th.room; GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;   // 🎨 배 테마
            if (NavTab(false, Loc.T("조종실로"), "", SweepGame.Amber)) GoFlow(2);
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
            string exT = Loc.T("<size=17><b><color=#dde3ea>궤도 증권</color></b></size>"); float exW = label.CalcSize(new GUIContent(exT)).x;   // 🌐 제목 폭만큼 민다
            L(top.x + 12, top.y + 8, exW + 4, exT);
            L(top.x + Mathf.Max(120, 12 + exW + 14), top.y + 10, 300, Loc.T("<size=12><color=#8a9bb3>궤도 종합</color>  <b>") + idx.ToString("#,0.00") + "</b>  " + Tone(idx - idx0, (idx >= idx0 ? "▲ " : "▼ ") + System.Math.Abs(idx - idx0).ToString("0.00")) + " " + Pct(idx / idx0 - 1) + "</size>");
            L(top.x + 420, top.y + 10, 280, Loc.T("<size=12><color=#8a9bb3>내 주식</color>  <b>") + KNum.Fmt(tv) + "</b>" + (tc > 0 ? "  " + Tone(tv - tc, (tv >= tc ? "+" : "") + KNum.Fmt(tv - tc)) + " " + Pct(tv / tc - 1) : "") + "</size>");
            L(top.x + 720, top.y + 10, 160, Loc.T("<size=12><color=#8a9bb3>돈</color>  <b><color=#ffdf95>") + KNum.Fmt(S.cash) + "</color></b></size>");
            Rt(top.x, top.y + 10, top.width - 12, "<size=12>" + (open ? Loc.T("<color=#ffcf6e><b>■ 시세 멈춤</b></color>") : Loc.T("<color=#8a9bb3>휴장</color>")) + "</size>");   // 조종실에선 시장이 안 흐른다 — 「장중 · 다음 봉」은 틀린 말이었다 (09-25 사장님 「정지돼 있는 걸 보여 줘」)

            // ── 왼쪽 — 종목표 · 속보 · 내부자
            var ls = new Rect(X0 + 8, 54, 214, 538); Box(ls);
            L(ls.x + 8, ls.y + 4, 80, Loc.T("<size=10><color=#5f6878>종목</color></size>"));
            Rt(ls.x, ls.y + 4, ls.width - 62, Loc.T("<size=10><color=#5f6878>현재가</color></size>"));
            Rt(ls.x, ls.y + 4, ls.width - 8, Loc.T("<size=10><color=#5f6878>등락</color></size>"));
            for (int i = 0; i < MS.st.Count; i++)
            {
                var st = MS.st[i]; var d = Market.Defs[i];
                var row = new Rect(ls.x + 4, ls.y + 24 + i * 38, ls.width - 8, 36);
                if (MS.sel == i) { GUI.color = new Color(0.11f, 0.15f, 0.21f); GUI.DrawTexture(row, white); GUI.color = Color.white; }
                else if (row.Contains(Event.current.mousePosition)) { GUI.color = new Color(1, 1, 1, 0.04f); GUI.DrawTexture(row, white); GUI.color = Color.white; }
                RowFx(i, row); if (i < 8) stockRowPos[i] = new Vector2(row.xMax - 40, row.center.y);
                double ch = mk.Change(i, 20);
                string arrow = st.pushLeft > 0 ? (st.push > 0 ? " <color=" + UpHex + ">▲</color>" : " <color=" + DnHex + ">▼</color>") : "";
                L(row.x + 4, row.y + 1, 150, (Loc.En ? "<size=10>" : "<size=12>") + (st.shares > 0 ? "<color=#ffdf95>● </color>" : "") + d.name + arrow + "</size>");
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
            L(ls.x + 8, ny + 4, 120, Loc.T("<size=11><b><color=#ff8a7a>속보</color></b></size>"));
            int nShow = sim.Lv("a_read") > 0 && open ? 4 : 5;                    // 내부자 줄이 있으면 넷만 — 다섯째가 겹쳤다
            for (int k = 0; k < nShow && k < MS.news.Count; k++)
            {
                var nw = MS.news[MS.news.Count - 1 - k];
                float yy = ny + 24 + k * 30;
                string mark = nw.dir > 0 ? "<color=" + UpHex + ">▲</color> " : nw.dir < 0 ? "<color=" + DnHex + ">▼</color> " : "";
                L(ls.x + 8, yy, ls.width - 16, "<size=11>" + mark + (k == 0 ? "<color=#ffdf95>" : "<color=#c8d0dc>") + Clip(Loc.T(nw.head), 15) + "</color></size>");
                L(ls.x + 20, yy + 14, ls.width - 28, "<size=9><color=#5f6878>" + Mathf.Max(0, Mathf.RoundToInt(MS.clock - nw.t)) + Loc.T("초 전") + (nw.rumor ? Loc.T(" · 소문") : "") + "</color></size>");
            }
            if (MS.news.Count == 0) L(ls.x + 8, ny + 24, ls.width - 16, Loc.T("<size=11><color=#5f6878>아직 속보 없음</color></size>"));
            int il = sim.Lv("a_read");
            if (il > 0 && open)
            {
                var nn = mk.NextNews;
                string tip = Loc.T("다음 출동 중 속보") + (il >= 2 ? " · " + (nn.up != null ? "<color=" + UpHex + Loc.T(">오를</color>") : "<color=" + DnHex + Loc.T(">내릴</color>")) + Loc.T(" 쪽: ") + Clip(SecName(nn), 10) : "");
                GUI.Label(new Rect(ls.x + 8, ls.yMax - 38, ls.width - 16, 36), Loc.T("<size=10><color=#e8c77e>내부자</color> ") + tip + "</size>", small);
            }

            // ── 가운데 — 봉 차트 · 이동평균 · 거래량
            int si = Mathf.Clamp(MS.sel, 0, MS.st.Count - 1); var ss = MS.st[si]; var sd = Market.Defs[si];
            var cb = new Rect(X0 + 228, 54, 492, 380); Box(cb);
            double sch = mk.Change(si, 20), ref0 = ss.price / (1 + sch);
            string cnT = "<size=15><b>" + sd.name + "</b></size>"; float cnW = label.CalcSize(new GUIContent(cnT)).x, pxX = Mathf.Max(136, 10 + cnW + 12);   // 🌐 종목 이름 폭만큼 값을 민다
            L(cb.x + 10, cb.y + 8, cnW + 4, cnT);
            L(cb.x + 10, cb.y + 28, 150, "<size=10><color=#5f6878>" + sd.sector + " · " + sd.desc + (sd.div > 0 ? Loc.T(" · 배당") : "") + "</color></size>");
            GUI.Label(new Rect(cb.x + pxX, cb.y + 2, 130, 36), "<size=24><b>" + Tone(sch, Px(ss.price)) + "</b></size>", label);
            L(cb.x + pxX + 110, cb.y + 12, 130, "<size=11>" + Tone(sch, (sch >= 0 ? "▲ " : "▼ ") + Px(System.Math.Abs(ss.price - ref0))) + " " + Pct(sch) + "</size>");
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
            L(cb.x + 10, cb.y + 44, 480, Loc.T("<size=10><color=#8a9bb3>시가 ") + Px(cBuf[0].o) + Loc.T("   고가 <color=") + UpHex + ">" + Px(hi2) + Loc.T("</color>   저가 <color=") + DnHex + ">" + Px(lo2) + Loc.T("</color>   거래량 ") + KNum.Fmt(vsum) + Loc.T("     <color=#f2c14e>— 5이평</color>  <color=#b69cff>— 20이평</color>") + (ss.shares > 0 ? Loc.T("  <color=#ffdf95>- - 내 평균</color>") : "") + "</color></size>");
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
                GUI.Label(new Rect(Mathf.Clamp(Cx(iHi) - 40, g.x, g.xMax - 80), Yp(hi2) - 16, 80, 16), "<size=10><color=" + UpHex + Loc.T(">최고 ") + Px(hi2) + "</color></size>", center);
                GUI.Label(new Rect(Mathf.Clamp(Cx(iLo) - 40, g.x, g.xMax - 80), Yp(lo2) + 1, 80, 16), "<size=10><color=" + DnHex + Loc.T(">최저 ") + Px(lo2) + "</color></size>", center);
            }
            // 내 평균가 점선 · 현재가 꼬리표
            if (ss.shares > 0)
            {
                float ay = Yp((float)(ss.cost / ss.shares));
                if (ay > g.y && ay < g.yMax) { GUI.color = new Color(1f, 0.87f, 0.58f, 0.7f); for (float xx = g.x; xx < g.xMax; xx += 8) GUI.DrawTexture(new Rect(xx, ay, 4, 1), white); GUI.color = Color.white; }
            }
            float py = Mathf.Clamp(Yp((float)ss.price), g.y + 2, g.yMax - 2);          // 차트 밖(목록 위)으로 선이 나가지 않게 (09-24 사장님 11번)
            GUI.color = sch >= 0 ? UpCol : DnCol; GUI.DrawTexture(new Rect(g.xMax + 2, py - 8, 54, 16), white);
            GUI.color = new Color(1, 1, 1, 0.25f); for (float xx = g.x; xx < g.xMax; xx += 6) GUI.DrawTexture(new Rect(xx, py, 3, 1), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(g.xMax + 2, py - 8, 54, 16), "<size=10><b>" + Px(ss.price) + "</b></size>", center);
            float mins = n * tf * Market.CandleSec / 60f;
            GUI.Label(new Rect(vr.x, vr.yMax, 120, 22), "<size=10><color=#7a8494>" + (mins >= 1 ? Mathf.RoundToInt(mins) + Loc.T("분 전") : Mathf.RoundToInt(mins * 60) + Loc.T("초 전")) + "</color></size>", label);
            GUI.Label(new Rect(vr.x, vr.yMax, vr.width, 22), Loc.T("<size=10><color=#7a8494>지금</color></size>"), rR);
            GUI.Label(new Rect(vr.xMax + 4, vr.y + 6, 54, 22), Loc.T("<size=10><color=#7a8494>거래량</color></size>"), label);

            // ── 오른쪽 — 호가 · 주문
            var ob = new Rect(X0 + 726, 54, 226, 380); Box(ob);
            L(ob.x + 8, ob.y + 4, 80, Loc.T("<size=10><color=#5f6878>호가</color></size>"));
            Rt(ob.x, ob.y + 4, ob.width - 8, Loc.T("<size=10><color=#5f6878>잔량</color></size>"));
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
                GUI.Label(tb, "<size=13><b>" + (on ? (k == 0 ? "<color=#ff8a8a>" : "<color=#8ab4ff>") : "<color=#5f6878>") + (k == 0 ? Loc.T("매수") : Loc.T("매도")) + "</color></b></size>", center);
                if (GUI.Button(tb, GUIContent.none, GUIStyle.none)) { ordSide = k; OrbitSfx.Play("tick", 0.3f); }
            }
            oy += 32;
            double fee = sim.StockFee;
            double money = System.Math.Floor(S.cash * ordFrac), shBuy = money * (1 - fee) / System.Math.Max(0.01, ss.price);
            double shSell = ss.shares * ordFrac, got = shSell * ss.price * (1 - fee);
            L(ob.x + 10, oy, 80, "<size=11><color=#8a9bb3>" + (ordSide == 0 ? Loc.T("금액") : Loc.T("수량")) + "</color></size>");
            Rt(ob.x, oy, ob.width - 10, "<size=13><b>" + (ordSide == 0 ? KNum.Fmt(money) : shSell.ToString("0.#") + Loc.T("주")) + "</b></size>");
            oy += 22;
            float fw = (ob.width - 16 - 9) / 4f; float[] fr = { 0.1f, 0.25f, 0.5f, 1f }; string[] fl = { "10%", "25%", "50%", Loc.T("전부") };
            for (int k = 0; k < 4; k++)
            {
                var fb = new Rect(ob.x + 8 + k * (fw + 3), oy, fw, 24);
                bool on = Mathf.Approximately(ordFrac, fr[k]);
                GUI.color = on ? new Color(0.16f, 0.21f, 0.29f) : new Color(0.09f, 0.11f, 0.15f); GUI.DrawTexture(fb, white); GUI.color = Color.white;
                GUI.Label(fb, "<size=11>" + (on ? "<color=#dde3ea>" : "<color=#8a9bb3>") + fl[k] + "</color></size>", center);
                if (GUI.Button(fb, GUIContent.none, GUIStyle.none)) ordFrac = fr[k];
            }
            oy += 28;
            L(ob.x + 10, oy, ob.width - 20, Loc.T("<size=10><color=#5f6878>수수료 ") + (fee * 100).ToString("0.#") + "%</color></size>");
            Rt(ob.x, oy, ob.width - 10, "<size=10><color=#8a9bb3>" + (ordSide == 0 ? Loc.T("약 ") + (shBuy >= 10000 ? KNum.Short(shBuy) : shBuy.ToString("#,0.#")) + Loc.T("주") : Loc.T("받을 돈 ") + KNum.Fmt(got)) + "</color></size>");
            oy += 18;
            var go = new Rect(ob.x + 8, oy, ob.width - 16, 34);
            bool can = ordSide == 0 ? money >= 1 : ss.shares > 0;
            bool gov = can && go.Contains(Event.current.mousePosition);
            GUI.color = !can ? new Color(0.12f, 0.13f, 0.16f) : ordSide == 0 ? (gov ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.77f, 0.17f, 0.17f)) : (gov ? new Color(0.25f, 0.5f, 1f) : new Color(0.18f, 0.4f, 0.85f));
            GUI.DrawTexture(go, white); GUI.color = Color.white;
            GUI.Label(go, "<size=15><b>" + (can ? "<color=#ffffff>" : "<color=#5f6878>") + (ordSide == 0 ? Loc.T("매수") : Loc.T("매도")) + "</color></b></size>", center);
            if (GUI.Button(go, GUIContent.none, GUIStyle.none) && can)
            {
                if (ordSide == 0) TradeBuy(si, ordFrac, go.center);
                else TradeSell(si, ordFrac, go.center);
            }

            if (open) GUI.Label(new Rect(ob.x + 4, go.yMax + 3, ob.width - 8, 18), Loc.T("<size=10><color=#9ff0bf>●</color> <color=#c8d0dc>매매는 조종실에서만 · 출동 중엔 잠김</color></size>"), center);
            // ⏸ 차트 위 — 지금은 멈춰 있다
            if (open)
            {
                var pz = new Rect(g.x + g.width / 2 - 150, g.y + 6, 300, 40);
                GUI.color = new Color(0.1f, 0.08f, 0.03f, 0.82f); GUI.DrawTexture(pz, white); Frame(pz, new Color(1f, 0.81f, 0.43f, 0.8f), 1); GUI.color = Color.white;
                GUI.Label(new Rect(pz.x, pz.y + 3, pz.width, 18), Loc.T("<size=13><b><color=#ffcf6e>■ 시세 멈춤</color></b></size>"), center);
                GUI.Label(new Rect(pz.x, pz.y + 20, pz.width, 16), Loc.T("<size=10><color=#c8b88a>출동하면 다시 움직인다 · 지금 사 두면 출동 중에 오르내린다</color></size>"), center);
            }

            // ── 아래 — 내 잔고
            var bb = new Rect(X0 + 228, 440, 724, 152); Box(bb);
            float[] cx2 = { 10, 230, 305, 380, 470, 550, 624 };
            string[] hd = { Loc.T("내 잔고"), Loc.T("보유"), Loc.T("평균가"), Loc.T("현재가"), Loc.T("평가금액"), Loc.T("손익"), Loc.T("수익률") };
            for (int k = 0; k < hd.Length; k++) { if (k == 0) L(bb.x + cx2[0], bb.y + 4, 120, "<size=10><color=#5f6878>" + hd[0] + "</color></size>"); else Rt(bb.x, bb.y + 4, cx2[k], "<size=10><color=#5f6878>" + hd[k] + "</color></size>"); }
            int rows = 0, more = 0, held = 0, skip;
            for (int i = 0; i < MS.st.Count; i++) if (MS.st[i].shares > 0) held++;
            // 📜 5개 넘으면 휠 · ▲▼ 로 넘긴다 (09-24 사장님 「5개까지만 보이고 더 안 보여」)
            if (Event.current.type == EventType.ScrollWheel && bb.Contains(Event.current.mousePosition)) { holdScroll += Event.current.delta.y > 0 ? 1 : -1; Event.current.Use(); }
            holdScroll = Mathf.Clamp(holdScroll, 0, Mathf.Max(0, held - 5)); skip = holdScroll;
            for (int i = 0; i < MS.st.Count; i++)
            {
                var st = MS.st[i]; if (st.shares <= 0) continue;
                if (skip > 0) { skip--; continue; }
                if (rows >= 5) { more++; continue; }
                float ry = bb.y + 22 + rows * 20; rows++;
                double val = st.shares * st.price, pl = val - st.cost, avg = st.cost / st.shares;
                var rr = new Rect(bb.x + 4, ry, bb.width - 100, 19);
                if (MS.sel == i) { GUI.color = new Color(0.11f, 0.15f, 0.21f); GUI.DrawTexture(rr, white); GUI.color = Color.white; }
                L(bb.x + cx2[0], ry, 200, "<size=11>" + Market.Defs[i].name + "</size>");
                Rt(bb.x, ry, cx2[1], "<size=11>" + st.shares.ToString("#,0.#") + Loc.T("주</size>"));
                Rt(bb.x, ry, cx2[2], "<size=11>" + Px(avg) + "</size>");
                Rt(bb.x, ry, cx2[3], "<size=11>" + Tone(st.price - avg, Px(st.price)) + "</size>");
                Rt(bb.x, ry, cx2[4], "<size=11>" + KNum.Fmt(val) + "</size>");
                Rt(bb.x, ry, cx2[5], "<size=11>" + Tone(pl, (pl >= 0 ? "+" : "") + KNum.Fmt(pl)) + "</size>");
                Rt(bb.x, ry, cx2[6], "<size=11>" + Pct(val / st.cost - 1) + "</size>");
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) MS.sel = i;
                if (GUI.Button(new Rect(bb.xMax - 90, ry, 82, 18), Loc.T("<size=10><color=#8ab4ff>전부 팔기</color></size>"), btnOff)) TradeSell(i, 1, new Vector2(bb.xMax - 49, ry + 9));
            }
            if (rows == 0) L(bb.x + 10, bb.y + 30, 500, Loc.T("<size=12><color=#5f6878>보유 종목 없음 — 왼쪽에서 종목을 고르고 오른쪽에서 매수</color></size>"));
            if (held > 5)
            {
                L(bb.x + 10, bb.y + 22 + 5 * 20, 260, "<size=10><color=#8a9bb3>" + (holdScroll + 1) + "–" + (holdScroll + rows) + " / " + held + Loc.T("종목 · 휠로 넘기기</color></size>"));
                if (holdScroll > 0 && GUI.Button(new Rect(bb.x + 200, bb.y + 20 + 5 * 20, 26, 18), "<size=10>▲</size>", btnOff)) holdScroll--;
                if (more > 0 && GUI.Button(new Rect(bb.x + 230, bb.y + 20 + 5 * 20, 26, 18), "<size=10>▼</size>", btnOff)) holdScroll++;
            }
            // 🙏 개미의 기도 · 🍀 행운의 부적 (칸을 사야)
            Rt(bb.x, bb.yMax - 24, bb.width - 10, "<size=11>" + LuckLine() + "</size>");

            GUI.enabled = en;
            // 잠김 — 보이긴 하되 흐리게, 여는 길을 알려 준다
            if (!open)
            {
                GUI.color = new Color(0.02f, 0.025f, 0.035f, 0.72f); GUI.DrawTexture(new Rect(X0 + 8, 54, 944, 538), white); GUI.color = Color.white;
                var card = new Rect(X0 + 480 - 180, 230, 360, 150);
                GUI.color = new Color(0.07f, 0.09f, 0.12f); GUI.DrawTexture(card, white); Frame(card, SweepGame.Amber, 2);
                GUI.Label(new Rect(card.x, card.y + 14, card.width, 30), Loc.T("<size=20><b><color=#ffdf95>잠김 · 증권 계좌 없음</color></b></size>"), center);
                GUI.Label(new Rect(card.x, card.y + 48, card.width, 24), Loc.T("<size=12><color=#c8d0dc>정비고에서 [증권 계좌] 칸을 사면 열린다</color></size>"), center);
                if (GUI.Button(new Rect(card.x + 90, card.y + 90, 180, 40), Loc.T("<size=14>‹ 정비고로</size>"), btn)) GoFlow(3);
            }
        }

    }
}
