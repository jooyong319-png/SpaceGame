using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    /// <summary>
    /// 📈 증권 연출 (09-24 사장님 시안 확정) — 매수 체결 도장 · 수익 동전 · 대박/초대박 · 손절 도장 · 봉마다 줄 번쩍 ·
    /// 속보 종목 불/얼음 테두리 · 속보 앵커 (입 뻐끔). 연출만 — 규칙은 그대로.
    /// </summary>
    public partial class SweepHud
    {
        struct FxCoin { public Vector2 a, b; public float t0; }
        struct FxText { public string txt; public Vector2 p; public float t0; public Color c; public bool stamp, played; }
        readonly List<FxCoin> fxCoins = new List<FxCoin>();
        readonly List<FxText> fxTexts = new List<FxText>();
        readonly Vector2[] stockRowPos = new Vector2[8];
        readonly float[] rowFlash = new float[8]; readonly bool[] rowFlashUp = new bool[8]; readonly int[] rowHist = new int[8];
        Vector2 stockCashPos = new Vector2(480, 16);
        float bigAt = -9; bool bigPlayed; string bigTxt, bigSub; readonly Vector4[] rain = new Vector4[40];
        const float CoinFly = 0.45f;

        void Coins(int n, Vector2 a, Vector2 b, float gap = 0.05f)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < n; i++) fxCoins.Add(new FxCoin { a = a + new Vector2(Random.Range(-18f, 18f), Random.Range(-6f, 6f)), b = b, t0 = now + i * gap });
        }
        void FxSay(string txt, Vector2 p, Color c, bool stamp, float delay = 0) => fxTexts.Add(new FxText { txt = txt, p = p, c = c, stamp = stamp, t0 = Time.unscaledTime + delay });

        public void TradeBuy(int i, float frac, Vector2 from)
        {
            if (sim.R != null && !sim.R.over) return;                     // 🔒 출동 중엔 못 산다
            double before = sim.S.cash;
            sim.StockBuy(i, frac);
            double spent = before - sim.S.cash; if (spent <= 0) return;
            OrbitSfx.Play("buy", 0.5f);
            Coins(8, from, stockRowPos[i]);
            FxSay("체결", stockRowPos[i] + new Vector2(10, -4), UpCol, true, 8 * 0.05f + CoinFly);
            rowFlash[i] = 0.6f; rowFlashUp[i] = true;
        }
        public void TradeSell(int i, double frac, Vector2 from)
        {
            var st = sim.Mk.M.st[i];
            double c0 = st.cost, before = sim.S.cash;
            sim.StockSell(i, frac);
            double got = sim.S.cash - before; if (got <= 0) return;
            double sold = c0 - st.cost, pl = got - sold, ratio = sold > 0 ? got / sold - 1 : 0;
            if (pl >= 0)
            {
                OrbitSfx.Play("grab", 0.6f);
                int n = ratio >= 0.3 ? 26 : 12;
                Coins(n, stockRowPos[i], stockCashPos, 0.045f);
                FxSay("+" + KNum.Fmt(pl), stockCashPos + new Vector2(0, 26), UpCol, false, n * 0.045f + CoinFly * 0.6f);
                if (ratio >= 0.3)
                {
                    bigAt = Time.unscaledTime + 0.3f; bigPlayed = false; bigTxt = ratio >= 0.5 ? "초대박!" : "대박!";
                    bigSub = Market.Defs[i].name + " +" + (ratio * 100).ToString("0.0") + "%";
                    for (int k = 0; k < rain.Length; k++) rain[k] = new Vector4(Random.value * vw, -Random.value * 200, 120 + Random.value * 160, Random.value);
                }
            }
            else
            {
                OrbitSfx.PlayPitch("tick", 0.7f, 0.55f);
                FxSay("손절", stockRowPos[i] + new Vector2(10, -4), DnCol, true);
                FxSay("−" + KNum.Fmt(-pl), stockCashPos + new Vector2(0, 26), DnCol, false, 0.1f);
            }
        }

        /// <summary>봉이 닫힐 때마다 종목 줄이 한 번 번쩍 (오르면 빨강 · 내리면 파랑) — 줄을 그리기 전에 부른다</summary>
        void RowTick()
        {
            var ms = sim.Mk.M.st;
            for (int i = 0; i < ms.Count && i < 8; i++)
            {
                int n = ms[i].hist.Count;
                if (rowHist[i] != n) { if (rowHist[i] != 0 && n > 0) { var c = ms[i].hist[n - 1]; rowFlash[i] = 0.45f; rowFlashUp[i] = c.c >= c.o; } rowHist[i] = n; }
                rowFlash[i] = Mathf.Max(0, rowFlash[i] - Time.unscaledDeltaTime * 0.5f);
            }
        }
        /// <summary>종목 줄 바탕 — 봉 번쩍 + 속보 여파가 남아 있으면 불(급등) · 얼음(급락) 테두리</summary>
        void RowFx(int i, Rect row)
        {
            if (i >= 8) return;
            if (rowFlash[i] > 0) { var c = rowFlashUp[i] ? UpCol : DnCol; GUI.color = new Color(c.r, c.g, c.b, rowFlash[i] * 0.5f); GUI.DrawTexture(row, white); GUI.color = Color.white; }
            var st = sim.Mk.M.st[i];
            if (st.pushLeft > 0)
            {
                float w = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (st.push > 0 ? 14 : 5));
                var c = st.push > 0 ? Color.Lerp(UpCol, new Color(1f, 0.7f, 0.3f), w) : Color.Lerp(DnCol, new Color(0.75f, 0.9f, 1f), w * 0.6f);
                GUI.color = new Color(c.r, c.g, c.b, 0.18f); GUI.DrawTexture(new Rect(row.x - 3, row.y - 3, row.width + 6, row.height + 6), white);
                Frame(row, c, 1.5f + w);
            }
        }

        void StockFxOverlay()
        {
            float now = Time.unscaledTime;
            for (int i = fxCoins.Count - 1; i >= 0; i--)
            {
                var c = fxCoins[i]; float k = (now - c.t0) / CoinFly;
                if (k < 0) continue;
                if (k >= 1) { fxCoins.RemoveAt(i); OrbitSfx.PlayPitch("pick", 0.3f, 1f + Random.value * 0.3f); continue; }
                float u = 1 - k; var m = new Vector2((c.a.x + c.b.x) / 2 + (i * 53 % 140 - 70), Mathf.Min(c.a.y, c.b.y) - 50);
                var p = u * u * c.a + 2 * u * k * m + k * k * c.b;
                GUI.color = new Color(1f, 0.82f, 0.4f, 0.35f); GUI.DrawTexture(new Rect(p.x - 11, p.y - 11, 22, 22), texDisc);
                GUI.color = new Color(1f, 0.85f, 0.45f); GUI.DrawTexture(new Rect(p.x - 6, p.y - 6, 12, 12), texDisc);
                GUI.color = new Color(1f, 0.96f, 0.78f); GUI.DrawTexture(new Rect(p.x - 3, p.y - 4, 4, 4), texDisc);
            }
            for (int i = fxTexts.Count - 1; i >= 0; i--)
            {
                var f = fxTexts[i]; float t = now - f.t0;
                if (t < 0) continue;
                if (t > 1.5f) { fxTexts.RemoveAt(i); continue; }
                float a = Mathf.Clamp01((1.5f - t) / 0.4f);
                string hex = ColorUtility.ToHtmlStringRGB(f.c);
                if (f.stamp)
                {
                    if (!f.played) { f.played = true; fxTexts[i] = f; OrbitSfx.Play("grab", 0.7f); }
                    float k = Mathf.Clamp01(t / 0.25f); float e = 1 + 2.70158f * Mathf.Pow(k - 1, 3) + 1.70158f * Mathf.Pow(k - 1, 2);
                    float sc = Mathf.Lerp(2.6f, 1f, e);
                    var r = new Rect(f.p.x - 34 * sc, f.p.y - 14 * sc, 68 * sc, 28 * sc);
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(-10, f.p);
                    Frame(r, new Color(f.c.r, f.c.g, f.c.b, Mathf.Min(1, k * 2) * a), 3 * sc);
                    GUI.color = new Color(1, 1, 1, Mathf.Min(1, k * 2) * a);
                    GUI.Label(r, "<size=" + Mathf.RoundToInt(17 * sc) + "><b><color=#" + hex + ">" + f.txt + "</color></b></size>", center);
                    GUI.matrix = m;
                }
                else
                {
                    GUI.color = new Color(1, 1, 1, a);
                    GUI.Label(new Rect(f.p.x - 120, f.p.y - 40 * t, 240, 30), "<size=20><b><color=#" + hex + ">" + f.txt + "</color></b></size>", center);
                }
            }
            GUI.color = Color.white;
            // 대박 / 초대박 — 가운데 큰 금빛 글씨 + 동전 비
            float bt = now - bigAt;
            if (bt >= 0 && bt < 2f)
            {
                if (!bigPlayed) { bigPlayed = true; OrbitSfx.Play("launch", 0.6f); game.flash = Mathf.Max(game.flash, 0.35f); }
                for (int k = 0; k < rain.Length; k++)
                {
                    var q = rain[k]; float y = q.y + q.z * bt + 180 * bt * bt;
                    GUI.color = new Color(1f, 0.82f, 0.35f, Mathf.Clamp01((2f - bt) / 0.5f)); GUI.DrawTexture(new Rect(q.x, y, 9, 9), texDisc);
                }
                float sc = bt < 0.25f ? Mathf.Lerp(0.4f, 1.15f, bt / 0.25f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((bt - 0.25f) / 0.2f));
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((2f - bt) / 0.4f));
                GUI.Label(new Rect(0, RefH * 0.4f - 46 * sc, vw, 92 * sc), "<size=" + Mathf.RoundToInt(60 * sc) + "><b><color=#ffdf95>" + bigTxt + "</color></b></size>", center);
                GUI.Label(new Rect(0, RefH * 0.4f + 44, vw, 28), "<size=20><b><color=" + UpHex + ">" + bigSub + "</color></b></size>", center);
                GUI.color = Color.white;
            }
        }

        // 🎙 속보 앵커 — 속보가 뜨면 왼쪽 아래에 작은 창, 입을 뻐끔거리며 제목을 읽는다
        float anchorAt = -9; string anchorHead; int anchorWho;
        static Texture2D[] anchorTex;                                   // 픽셀랩 앵커 둘 — a 남 · b 여, 0 입 닫음 · 1 입 벌림 (09-24 사장님 「기자도 도트로」)
        public void Anchor(string head) { anchorAt = Time.unscaledTime; anchorHead = head; anchorWho = 1 - anchorWho; }   // 속보마다 번갈아
        void AnchorBox()
        {
            float t = Time.unscaledTime - anchorAt;
            if (t < 0 || t > 3.4f || anchorHead == null) return;
            float a = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((3.4f - t) / 0.4f);
            float slide = (1 - Mathf.Clamp01(t / 0.25f)) * -260;
            var r = new Rect(12 + slide, RefH - 136, 250, 112);
            GUI.color = new Color(0.07f, 0.09f, 0.13f, 0.95f * a); GUI.DrawTexture(r, white);
            Frame(r, new Color(0.7f, 0.09f, 0.06f, a), 2);
            var face = new Rect(r.x + 2, r.y + 2, 74, r.height - 4);
            GUI.color = new Color(0.06f, 0.09f, 0.14f, a); GUI.DrawTexture(face, white);
            float cx = face.center.x, hy = r.y + 44;
            bool open = t < 2.9f && Mathf.Repeat(t, 0.28f) < 0.14f;
            if (anchorTex == null) { anchorTex = new Texture2D[4]; for (int i = 0; i < 4; i++) anchorTex[i] = Resources.Load<Texture2D>("news/anchor_" + (i < 2 ? "a" : "b") + (i % 2)); }
            var atx = anchorTex[anchorWho * 2 + (open ? 1 : 0)] ?? anchorTex[anchorWho * 2];
            if (atx != null)
            {   // 🎙 도트 앵커 — 스튜디오 뒤판 위에 가슴까지
                GUI.color = new Color(0.1f, 0.16f, 0.26f, a); GUI.DrawTexture(new Rect(face.x, face.y, face.width, face.height * 0.55f), white);
                GUI.color = new Color(0.7f, 0.09f, 0.06f, 0.5f * a); GUI.DrawTexture(new Rect(face.x, face.y + face.height * 0.55f, face.width, 2), white);
                GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(cx - 36, face.yMax - 72, 72, 72), atx);
            }
            else
            {
            GUI.color = new Color(0.16f, 0.23f, 0.35f, a); GUI.DrawTexture(new Rect(cx - 26, r.y + 70, 52, 42), white);        // 양복
            GUI.color = new Color(0.9f, 0.9f, 0.92f, a); GUI.DrawTexture(new Rect(cx - 6, r.y + 70, 12, 22), white);             // 셔츠
            GUI.color = new Color(0.75f, 0.12f, 0.1f, a); GUI.DrawTexture(new Rect(cx - 2.5f, r.y + 72, 5, 18), white);          // 넥타이
            GUI.color = new Color(0.89f, 0.71f, 0.56f, a); GUI.DrawTexture(new Rect(cx - 19, hy - 22, 38, 42), texDisc);         // 얼굴
            GUI.color = new Color(0.17f, 0.1f, 0.06f, a); GUI.DrawTexture(new Rect(cx - 20, hy - 25, 40, 16), texDisc);          // 머리
            GUI.color = new Color(0, 0, 0, a); GUI.DrawTexture(new Rect(cx - 9, hy - 3, 4, 4), texDisc); GUI.DrawTexture(new Rect(cx + 5, hy - 3, 4, 4), texDisc);
            GUI.color = new Color(0.48f, 0.16f, 0.12f, a); GUI.DrawTexture(new Rect(cx - 5, hy + 8, 10, open ? 8 : 2.5f), texDisc);  // 입 뻐끔
            }
            GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(r.x + 84, r.y + 8, r.width - 92, 20), "<size=12><b><color=#ff5c5c>● 속보입니다</color></b></size>", label);
            GUI.Label(new Rect(r.x + 84, r.y + 30, r.width - 92, 76), "<size=12><color=#e8edf3>" + anchorHead + "</color></size>", small);
            GUI.color = Color.white;
        }

        // ───────────────────────────────── 📈 내 주식 미리보기 (09-24 사장님 시안 A · A — 수익률 높은 게 맨 위)
        readonly List<int> myIdx = new List<int>();
        double MyPct(int i) { var s = sim.Mk.M.st[i]; return s.shares > 0 && s.cost > 0 ? s.shares * s.price / s.cost - 1 : 0; }
        void MyStocks()
        {
            myIdx.Clear();
            var ms = sim.Mk.M.st;
            for (int i = 0; i < ms.Count; i++) if (ms[i].shares > 0) myIdx.Add(i);
            myIdx.Sort((a, b) => MyPct(b).CompareTo(MyPct(a)));
        }
        double MyTotal() { double v = 0, c = 0; foreach (var s in sim.Mk.M.st) if (s.shares > 0) { v += s.shares * s.price; c += s.cost; } return c > 0 ? v / c - 1 : 0; }
        static string PctTxt(double v) => "<color=" + (v >= 0 ? UpHex : DnHex) + ">" + (v >= 0 ? "+" : "−") + System.Math.Abs(v * 100).ToString("0.0") + "%</color>";
        bool Hot(int i) { var s = sim.Mk.M.st[i]; return s.pushLeft > 0 && s.push > 0; }

        /// <summary>조종실 — 창 오른쪽 위 주황 LED 시세판 (증권 탭 쪽). 누르면 증권 방으로</summary>
        void MyStockBoard()
        {
            if (!sim.StockOpen || sim.Mk == null) return;
            MyStocks();
            int n = Mathf.Min(3, myIdx.Count);
            var r = new Rect(ox + 598, 50, 156, 30 + Mathf.Max(1, n) * 17 + 22);
            bool ov = r.Contains(Event.current.mousePosition);
            GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(r.x + 3, r.y + 4, r.width, r.height), white);
            GUI.color = new Color(0.045f, 0.035f, 0.028f, 0.96f); GUI.DrawTexture(r, white);
            Frame(r, ov ? Amber3 : new Color(0.17f, 0.15f, 0.13f), 3);
            GUI.Label(new Rect(r.x + 9, r.y + 6, r.width - 18, 18), "<size=10><color=#ffab3d>MY STOCK</color></size>", label);
            if (n > 0) GUI.Label(new Rect(r.x + 9, r.y + 5, r.width - 18, 18), "<size=12><b>" + PctTxt(MyTotal()) + "</b></size>", ledR ?? cost);
            for (int k = 0; k < n; k++)
            {
                int i = myIdx[k]; float y = r.y + 26 + k * 17;
                if (Hot(i)) { GUI.color = new Color(1f, 0.36f, 0.36f, 0.14f + 0.1f * Mathf.Sin(Time.unscaledTime * 10)); GUI.DrawTexture(new Rect(r.x + 4, y, r.width - 8, 16), white); GUI.color = Color.white; }
                GUI.Label(new Rect(r.x + 9, y - 1, r.width - 18, 18), "<size=11><b><color=#e8d8c0>" + Clip(Market.Defs[i].name, 7) + "</color></b></size>", label);
                GUI.Label(new Rect(r.x + 9, y - 1, r.width - 18, 18), "<size=11><b>" + PctTxt(MyPct(i)) + "</b></size>", ledR ?? cost);
            }
            if (n == 0) GUI.Label(new Rect(r.x + 9, r.y + 26, r.width - 18, 18), "<size=11><color=#7a6a55>보유 종목 없음</color></size>", label);
            if (myIdx.Count > 3) GUI.Label(new Rect(r.x + 9, r.yMax - 21, r.width - 18, 16), "<size=9><color=#7a6a55>외 " + (myIdx.Count - 3) + "</color></size>", label);
            GUI.Label(new Rect(r.x + 9, r.yMax - 21, r.width - 18, 16), "<size=9><color=" + (ov ? "#ffdf95" : "#7a6a55") + ">증권 ›</color></size>", ledR ?? cost);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) GoFlow(4);
        }

        /// <summary>출동 중 — 오른쪽 위 작은 칩 (전체 + 종목 셋, 수익률 높은 순 · 급등 중이면 빨갛게 빛남)</summary>
        void MyStockChips()
        {
            float y = 76, right = vw - 14;
            if (sim.Grit > 0.005) { var gr = new Rect(right - 130, y, 130, 22); GUI.color = new Color(0.2f, 0.05f, 0.05f, 0.85f); GUI.DrawTexture(gr, white); Frame(gr, new Color(1f, 0.4f, 0.3f), 1); GUI.color = Color.white; GUI.Label(new Rect(gr.x + 8, gr.y + 1, gr.width, 20), "<size=12><color=#ffb3a8>근성 화력 <b>+" + Mathf.RoundToInt((float)sim.Grit * 100) + "%</b></color></size>", label); y += 25; }
            if (!sim.StockOpen || sim.Mk == null) return;
            MyStocks();
            if (myIdx.Count == 0) return;
            void Chip(string txt, bool hot)
            {
                float w = label.CalcSize(new GUIContent(txt)).x * 0.86f + 16;
                var r = new Rect(right - w, y, w, 22);
                GUI.color = new Color(0.04f, 0.055f, 0.08f, 0.85f); GUI.DrawTexture(r, white);
                if (hot) { float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10); GUI.color = new Color(1f, 0.36f, 0.36f, 0.12f + 0.12f * p); GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), white); Frame(r, UpCol, 1 + p); }
                else Frame(r, new Color(0.16f, 0.2f, 0.26f), 1);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 8, r.y + 1, r.width, 20), txt, label);
                y += 25;
            }
            Chip("<size=12>내 주식 <b>" + PctTxt(MyTotal()) + "</b></size>", false);
            if (sim.PlanetStockBonus > 1) Chip("<size=12><color=#9fe8ff>행성 투자 값 <b>+20%</b></color></size>", false);
            if (sim.Rage > 0.005) Chip("<size=12><color=#ff8a7a>분노 화력 <b>+" + Mathf.RoundToInt((float)sim.Rage * 100) + "%</b></color></size>", true);
            for (int k = 0; k < Mathf.Min(3, myIdx.Count); k++) { int i = myIdx[k]; Chip("<size=12>" + Clip(Market.Defs[i].name, 7) + " <b>" + PctTxt(MyPct(i)) + (Hot(i) ? " ▲" : "") + "</b></size>", Hot(i)); }
        }
    }
}
