using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🎟 복권 · 의뢰 · 대출 창 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── 대출 창구 (모달) — 내역 · 받기 · 갚기
        // 🎟 복권 창 — [즉석 복권] 은박을 마우스로 긁는다 · [궤도 로또] 번호 셋을 고른다 (사장님 09-23 「복권 두 가지 다」)
        public bool lottoOpen; int lottoTab, lottoSeen = -1; float lottoToast;
        int[] scGrid; int scWin = -1; bool[,] scCoat; bool scDone; float scDoneT; double scGot;
        readonly List<int> lottoPick = new List<int>();
        const int ScCols = 7, ScRows = 4, ScCells = SweepSim.ScratchCells;  // 🎟 10-06 창 9칸(3×3) · 칸마다 은박 조각 7×4
        static Texture2D[] scIcon; bool scUse;
        static readonly string[] ScIconPath = { "junk/junk_vault_gold", "junk/junk_vault", "shop/part_0", "shop/key", "junk/junk_rocket", "junk/junk_sat", "junk/junk_fuel", "junk/junk_chip_a" };   // 1등 → 8등
        public void TestLotto(int step)   // 에디터 시험용
        {
            if (step == 0) { lottoOpen = true; lottoTab = 0; scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[ScCells, ScCols * ScRows]; scDone = false; }
            else if (step == 1) { for (int c = 0; c < ScCells; c++) for (int b = 0; b < ScCols * ScRows; b++) if (c < 5 || (b + c) % 3 != 0 && c == 5) scCoat[c, b] = true; }
            else if (step == 2) { lottoTab = 1; lottoPick.Clear(); lottoPick.AddRange(new[] { 3, 7, 11 }); sim.LottoBuy(3, 7, 11); lottoPick.Clear(); lottoPick.AddRange(new[] { 2, 5 }); }
        }
        /// <summary>에디터 시험용 — rank 등 당첨 표를 펼친다 (reveal = 몇 칸까지 긁어 둘지 · 9 = 다)</summary>
        public void TestLottoWin(int rank, int reveal)
        {
            lottoOpen = true; lottoTab = 0;
            var g = new int[ScCells]; var cnt = new int[SweepSim.ScratchSym.Length];
            for (int i = 0; i < ScCells; i++)
            {
                if (rank >= 0 && (i == 0 || i == 4 || i == 8)) { g[i] = rank; continue; }   // 대각선에 셋 — 긁는 순서 보기 좋게
                int c; do c = Random.Range(0, cnt.Length); while (c == rank || cnt[c] >= 2); cnt[c]++; g[i] = c;
            }
            scGrid = g; scWin = rank; sim.DebugScratchWin(rank); scCoat = new bool[ScCells, ScCols * ScRows]; scDone = false; scRank = -1; scBannerTxt = null;
            for (int c = 0; c < Mathf.Min(reveal, ScCells); c++) for (int b = 0; b < ScCols * ScRows; b++) scCoat[c, b] = true;
        }
        void LottoToast()
        {
            var S = sim.S;
            if (lottoSeen < 0) lottoSeen = S.lottoLast != null ? S.lottoLastRound : 0;                         // 켜자마자 옛 결과는 건너뛴다
            if (S.lottoLast != null && S.lottoLastRound > lottoSeen) { lottoToast = 7f; OrbitSfx.Play(S.lottoLastWin > 0 ? "buy" : "tick", 0.8f); lottoSeen = S.lottoLastRound; }
            if (lottoToast <= 0) return;
            lottoToast -= Time.unscaledDeltaTime;
            var tr = new Rect(vw / 2 - 250, RefH - 92, 500, 60);            // 결과 화면 단추 아래
            GUI.color = new Color(0.25f, 0.08f, 0.3f, 0.95f * Mathf.Clamp01(lottoToast)); GUI.DrawTexture(tr, white); GUI.color = new Color(1, 1, 1, Mathf.Clamp01(lottoToast));
            Frame(tr, SweepGame.Mag, 2);
            Lbl(new Rect(tr.x, tr.y + 5, tr.width, 24), Loc.T("<size=16><b>궤도 로또 ") + S.lottoLastRound + Loc.T("회 당첨 번호  ") + S.lottoLast[0] + " · " + S.lottoLast[1] + " · " + S.lottoLast[2] + "</b></size>", center);
            Lbl(new Rect(tr.x, tr.y + 31, tr.width, 22), "<size=14>" + (S.lottoLastWin > 0 ? "<color=#6fcf97>" + S.lottoLastHit + Loc.T("개 맞음 · +") + KNum.Fmt(S.lottoLastWin) + "</color>" : Loc.T("<color=#ee7766>꽝 — 맞힌 번호 없음</color>")) + "</size>", center);
            GUI.color = Color.white;
        }

        void LottoWin()
        {
            var S = sim.S; var ev = Event.current;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 450, 44, 900, 512);
            GUI.color = new Color(0.07f, 0.03f, 0.09f, 0.96f); GUI.DrawTexture(r, white);                    // 분홍 홀로그램 테 (09-24)
            GUI.color = new Color(1f, 0.66f, 0.94f, 0.05f); for (float yy = r.y + 2; yy < r.yMax; yy += 4) GUI.DrawTexture(new Rect(r.x, yy, r.width, 1), white);
            Frame(r, new Color(1f, 0.66f, 0.94f, 0.85f), 1.5f);
            GUI.color = Color.white;
            Lbl(new Rect(r.x + 20, r.y + 12, 200, 30), Loc.T("<size=18><b><color=#f3c8ff>즉석 복권</color></b></size>"), label);
            Lbl(new Rect(r.x, r.y + 16, r.width - 20, 24), Loc.T("<size=13><color=#8a9bb3>돈</color> ") + KNum.Fmt(S.cash) + "</size>", cost);
            Scratch(r, ev);
            if (Bt(new Rect(r.xMax - 106, r.yMax - 50, 92, 36), Loc.T("닫기"), btnOffC)) lottoOpen = false;
            ScratchFx();
        }

        // 🎟 10-06 개편 — 창 9칸 · 같은 그림 3개 · 1~8등 · 둘 보이면 「하나 더!」 · 등수별 연출 (시안 Kd11dtfKUykpWiS1P8TLdz)
        readonly List<(Vector2 p, Vector2 v, float l, Color c, float s, float g)> scParts = new List<(Vector2, Vector2, float, Color, float, float)>();
        float scShakeT = -9, scFlashT = -9, scBannerT = -9; int scRank = -1;
        void ScBurst(Vector2 at, int n, float sp, float grav, params Color[] cols)
        {
            if (reduceMotion) n = Mathf.Min(n, 8);
            for (int i = 0; i < n; i++) { float a = Random.value * Mathf.PI * 2, v = sp * (0.4f + Random.value); scParts.Add((at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v - new Vector2(0, sp * 0.3f), 1f, cols[i % cols.Length], 2 + Random.value * 3, grav)); }
        }
        void ScratchFx()
        {   // 동전 · 은박 가루 · 번쩍 · 1등 배너 — 창 위에
            float dt = Time.unscaledDeltaTime;
            if (Event.current.type == EventType.Repaint)
                for (int i = scParts.Count - 1; i >= 0; i--) { var q = scParts[i]; q.v.y += q.g * dt; q.p += q.v * dt; q.l -= dt; if (q.l <= 0 || q.p.y > RefH + 20) scParts.RemoveAt(i); else scParts[i] = q; }
            foreach (var q in scParts) { GUI.color = new Color(q.c.r, q.c.g, q.c.b, Mathf.Clamp01(q.l * 2)); GUI.DrawTexture(new Rect(q.p.x, q.p.y, q.s, q.s), white); }
            float fe = Time.unscaledTime - scFlashT; if (fe < 0.5f && !reduceMotion) { GUI.color = new Color(1, 1, 1, 0.8f * (1 - fe / 0.5f)); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); }
            float be = Time.unscaledTime - scBannerT;
            if (be < 2.6f && scRank >= 0)
            {
                float a = Mathf.Clamp01(be / 0.2f) * Mathf.Clamp01((2.6f - be) / 0.5f); var c = new Vector2(vw / 2, RefH / 2);
                for (int i = 0; i < 18; i++) { float ang = i * 20 + be * 30; var d = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)); Line(c + d * 60, c + d * 420, new Color(1f, 0.87f, 0.58f, 0.16f * a), 26); }
                float sc = reduceMotion ? 1 : Mathf.Lerp(2.4f, 1f, Mathf.Clamp01(be / 0.35f));
                GUI.color = new Color(0.1f, 0.03f, 0.1f, 0.8f * a); GUI.DrawTexture(new Rect(c.x - 260, c.y - 80, 520, 150), white); Frame(new Rect(c.x - 260, c.y - 80, 520, 150), new Color(1f, 0.87f, 0.58f, a), 2);   // 글자가 빛줄기 · 표에 묻히지 않게
                GUI.color = new Color(1, 1, 1, a);
                Lbl(new Rect(c.x - 300, c.y - 70, 600, 100), "<size=" + Mathf.RoundToInt(88 * sc) + "><b><color=#ffdf95>" + SweepSim.RankName(scRank + 1) + "!</color></b></size>", center);
                if (sim.ScratchText != null || scBannerTxt != null) Lbl(new Rect(c.x - 300, c.y + 30, 600, 30), "<size=20>" + scBannerTxt + "</size>", center);
            }
            GUI.color = Color.white;
        }
        string scBannerTxt;
        void Scratch(Rect r, Event ev)
        {
            var S = sim.S;
            if (scIcon == null) { scIcon = new Texture2D[ScIconPath.Length]; for (int i = 0; i < ScIconPath.Length; i++) scIcon[i] = Resources.Load<Texture2D>(ScIconPath[i]); }
            // 표 — 분홍 바탕에 창 아홉 (흔들림은 2~5등)
            float se = Time.unscaledTime - scShakeT, sh = se < 0.45f && !reduceMotion ? Mathf.Sin(se * 60) * 5 * (1 - se / 0.45f) : 0;
            var card = new Rect(r.x + 20 + sh, r.y + 52, 500, 372);
            bool gold = scDone && scRank >= 0 && scRank <= 1;
            GUI.color = gold ? new Color(1f, 0.87f, 0.58f, 0.5f + 0.3f * Mathf.Sin(Time.unscaledTime * 6)) : new Color(0.23f, 0.06f, 0.2f); GUI.DrawTexture(new Rect(card.x - 4, card.y - 4, card.width + 8, card.height + 8), white);
            GUI.color = new Color(1f, 0.7f, 0.86f); GUI.DrawTexture(card, white);
            GUI.color = new Color(1f, 0.83f, 0.45f, 0.55f); GUI.DrawTexture(new Rect(card.x, card.yMax - 34, card.width, 34), white); GUI.color = Color.white;
            Lbl(new Rect(card.x + 14, card.y + 6, 300, 26), Loc.T("<size=17><b><color=#3a0f33>궤도 즉석 복권</color></b></size>"), label);
            Lbl(new Rect(card.x, card.y + 9, card.width - 14, 20), Loc.T("<size=11><color=#6a2a5c>같은 그림 3개 = 당첨</color></size>"), cost);
            string band = scGrid == null ? Loc.T("한 장 사서 긁어 보세요") : scDone ? (scRank >= 0 ? sim.ScratchText ?? scBannerTxt : Loc.T("꽝 — 다음 장은 출동 전까지 값이 오름")) : Loc.T("누른 채 문질러 긁기");
            Lbl(new Rect(card.x + 8, card.yMax - 30, card.width - 16, 26), Fit("<color=#5a2050>" + (scDone && scRank >= 0 ? scBannerTxt : band) + "</color>", 12, card.width - 16, center, 9), center);
            float gap = 10, gx = card.x + 18, gy = card.y + 38, cw = (464 - gap * 2) / 3, ch = (290 - gap * 2) / 3;
            int nRev = 0; var revCnt = new int[SweepSim.ScratchSym.Length]; var rev = new bool[ScCells];
            for (int c = 0; c < ScCells && scGrid != null; c++)
            {
                int left = 0; for (int b = 0; b < ScCols * ScRows; b++) if (!scCoat[c, b]) left++;
                rev[c] = scDone || left <= ScCols * ScRows * 0.45f; if (rev[c]) { nRev++; revCnt[scGrid[c]]++; }
            }
            int reachSym = -1; if (!scDone && scGrid != null) for (int k = 0; k < revCnt.Length; k++) if (revCnt[k] == 2) reachSym = k;
            for (int c = 0; c < ScCells && scGrid != null; c++)
            {
                var cr = new Rect(gx + (c % 3) * (cw + gap), gy + (c / 3) * (ch + gap), cw, ch);
                int sym = scGrid[c];
                bool hit = scDone && scRank >= 0 && sym == scRank, reach = rev[c] && sym == reachSym;
                float pop = hit && !reduceMotion ? 1 + 0.12f * Mathf.Exp(-6 * (Time.unscaledTime - scDoneT)) * Mathf.Sin((Time.unscaledTime - scDoneT) * 18) : 1;
                if (pop != 1) cr = new Rect(cr.center.x - cr.width * pop / 2, cr.center.y - cr.height * pop / 2, cr.width * pop, cr.height * pop);
                GUI.color = new Color(0.23f, 0.06f, 0.2f); GUI.DrawTexture(new Rect(cr.x - 3, cr.y - 3, cr.width + 6, cr.height + 6), white);
                if (reach) { GUI.color = new Color(1f, 0.87f, 0.58f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 8)); GUI.DrawTexture(new Rect(cr.x - 6, cr.y - 6, cr.width + 12, cr.height + 12), white); }
                GUI.color = hit ? new Color(1f, 0.95f, 0.62f) : new Color(1f, 0.97f, 0.93f); GUI.DrawTexture(cr, white); GUI.color = Color.white;
                var ic = scIcon[Mathf.Clamp(sym, 0, scIcon.Length - 1)];
                if (ic != null) { float isz = Mathf.Min(cr.width, cr.height) * 0.62f; GUI.DrawTexture(new Rect(cr.center.x - isz / 2, cr.center.y - isz / 2 - 7, isz, isz), ic, ScaleMode.ScaleToFit); }
                Lbl(new Rect(cr.x, cr.yMax - 19, cr.width, 18), "<size=10><color=#5a2050>" + SweepSim.ScratchSym[sym] + "</color></size>", center);
                if (reach) { var tg = new Rect(cr.xMax - 58, cr.y + 2, 56, 15); GUI.color = new Color(1f, 0.87f, 0.58f); GUI.DrawTexture(tg, white); GUI.color = Color.white; Lbl(tg, Loc.T("<size=10><color=#5a2050>하나 더!</color></size>"), center); }
                // 은박 — 누른 채 지나가면 벗겨진다 (칸 사이를 한 번에 문질러도 된다)
                int leftN = 0; float bw = cr.width / ScCols, bh = cr.height / ScRows;
                for (int b = 0; b < ScCols * ScRows; b++)
                {
                    if (scCoat[c, b]) continue; leftN++;
                    if (scDone || rev[c]) continue;
                    var br = new Rect(cr.x + (b % ScCols) * bw, cr.y + (b / ScCols) * bh, bw + 0.5f, bh + 0.5f);
                    float shade = ((b % ScCols) + (b / ScCols)) % 2 == 0 ? 0.74f : 0.8f;
                    GUI.color = new Color(shade, shade, shade + 0.04f); GUI.DrawTexture(br, white); GUI.color = Color.white;
                }
                if (leftN == ScCols * ScRows && !scDone) Lbl(cr, Loc.T("<size=12><color=#7a5a86>긁기</color></size>"), center);
                if (!scDone && !rev[c] && (ev.type == EventType.MouseDrag || ev.type == EventType.MouseDown) && ev.button == 0 && new Rect(cr.x - 10, cr.y - 10, cr.width + 20, cr.height + 20).Contains(ev.mousePosition))
                {
                    for (int b = 0; b < ScCols * ScRows; b++)
                    {
                        var bc = new Vector2(cr.x + (b % ScCols + 0.5f) * bw, cr.y + (b / ScCols + 0.5f) * bh);
                        if (!scCoat[c, b] && (bc - ev.mousePosition).sqrMagnitude < 22 * 22) { scCoat[c, b] = true; if (Random.value < 0.35f) ScBurst(bc, 2, 60, 500, new Color(0.85f, 0.85f, 0.88f), new Color(0.72f, 0.72f, 0.77f)); if (Random.value < 0.2f) OrbitSfx.PlayPitch("tick", 0.2f, 1.6f + Random.value * 0.4f); }
                    }
                    scUse = true;
                }
            }
            if (scUse) { scUse = false; ev.Use(); }
            // 당첨표 — 1~8등 · 실제 금액
            var pt = new Rect(r.xMax - 360, r.y + 52, 340, 8 * 36);
            Lbl(new Rect(pt.x, pt.y - 2, pt.width, 18), Loc.T("<size=12><color=#f3c8ff>당첨표 — 같은 그림 3개</color></size>"), label);
            for (int k = 0; k < 8; k++)
            {
                var row = new Rect(pt.x, pt.y + 20 + k * 36, pt.width, 34);
                if (scDone && scRank == k) { GUI.color = new Color(1f, 0.87f, 0.58f, 0.22f); GUI.DrawTexture(row, white); }
                GUI.color = new Color(1, 1, 1, 0.08f); GUI.DrawTexture(new Rect(row.x, row.yMax, row.width, 1), white); GUI.color = Color.white;
                string rc = k == 0 ? "#ffdf95" : "#c9b3d6";
                Lbl(new Rect(row.x + 4, row.y + 8, 36, 18), "<size=12><color=" + rc + ">" + SweepSim.RankName(k + 1) + "</color></size>", label);
                if (scIcon[k] != null) GUI.DrawTexture(new Rect(row.x + 42, row.y + 4, 26, 26), scIcon[k], ScaleMode.ScaleToFit);
                Lbl(new Rect(row.x + 74, row.y + 1, 200, 18), "<size=12><color=" + (k == 0 ? "#ffdf95" : "#efe6f3") + ">" + SweepSim.ScratchSym[k] + "</color></size>", label);
                Lbl(new Rect(row.x + 74, row.y + 17, row.width - 120, 16), Fit("<color=#9a8ca6>" + ScPrize(k) + "</color>", 10, row.width - 120, label, 8), label);
                Lbl(new Rect(row.x, row.y + 8, row.width - 6, 18), "<size=10><color=#9a8ca6>" + (SweepSim.ScratchOdds[k] * 100 * (1 + 0.5f * sim.Lv("l_luck"))).ToString(SweepSim.ScratchOdds[k] < 0.01 ? "0.0" : "0") + "%</color></size>", cost);
            }
            Lbl(new Rect(pt.x, pt.y + 20 + 8 * 36 + 4, pt.width, 30), Loc.T("<size=10><color=#8a7a96>판 벌이 = 지금 평균 판 수입 · 화력 · 연료는 다음 판 한 번 · 겹치지 않음</color></size>"), small);
            // 다 보이면 받는다 — 당첨 그림 셋이 보이면 바로
            if (scGrid != null && !scDone)
            {
                int won = -1; for (int k = 0; k < revCnt.Length; k++) if (revCnt[k] >= 3) won = k;
                if (won >= 0 || nRev == ScCells)
                {
                    scDone = true; scDoneT = Time.unscaledTime; scRank = scWin; scGot = sim.ScratchClaim(); scBannerTxt = sim.ScratchText;
                    var cc = new Vector2(card.center.x, card.center.y);
                    if (scRank >= 0)
                    {
                        OrbitSfx.Play("buy", 1f); OrbitSfx.Play("clank", 0.7f); game.creditPulse = 1;
                        if (scRank <= 4) { ScBurst(cc, scRank <= 1 ? 120 : 50, scRank <= 1 ? 420 : 260, 300, new Color(1f, 0.87f, 0.58f), SweepGame.Amber, new Color(1f, 0.96f, 0.78f)); scShakeT = Time.unscaledTime; }
                        if (scRank <= 1)
                        {
                            scFlashT = Time.unscaledTime; scBannerT = Time.unscaledTime; OrbitSfx.Play("launch", 0.6f);
                            for (int i = 0; i < (scRank == 0 ? 160 : 80); i++) scParts.Add((new Vector2(Random.value * vw, -10 - Random.value * 300), new Vector2((Random.value - 0.5f) * 40, 120 + Random.value * 120), 3.5f, i % 2 == 0 ? new Color(1f, 0.87f, 0.58f) : SweepGame.Amber, 4 + Random.value * 3, 120));
                        }
                    }
                    else OrbitSfx.PlayPitch("tick", 0.6f, 0.7f);
                }
            }
            // 아래 — 사기 · 다 긁기 · 결과 · 표값
            bool can = S.cash >= sim.ScratchCost;
            string costTxt = sim.ScratchCost <= 0 ? Loc.T("공짜") : KNum.Fmt(sim.ScratchCost);
            string buyTxt = "<size=15>" + (scGrid == null ? Loc.T("복권 1장 구매") : Loc.T("한 장 더")) + " · " + costTxt + "</size>";
            var bb = new Rect(r.x + 20, r.yMax - 50, 220, 36);
            if (scGrid != null && !scDone)
            {
                if (Bt(new Rect(bb.x, bb.y, 180, 36), Loc.T("<size=14>한 번에 다 긁기</size>"), btn)) for (int c = 0; c < ScCells; c++) for (int b = 0; b < ScCols * ScRows; b++) scCoat[c, b] = true;
            }
            else if (Bt(bb, buyTxt, can ? btn : btnOff) && can)
            { scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[ScCells, ScCols * ScRows]; scDone = false; scGot = 0; scRank = -1; scBannerTxt = null; OrbitSfx.Play("buy", 0.5f); }
            if (scDone)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - scDoneT) / 0.25f);
                string msg = scRank < 0 ? Loc.T("<color=#8a93a3>꽝</color>") : "<color=#ffdf95>" + scBannerTxt + "</color>";
                Lbl(new Rect(r.x + 250, r.yMax - 52, 280, 36), Fit(msg, scRank < 0 ? 20 : Mathf.RoundToInt(Mathf.Lerp(24, 16, k)), 280, center, 9), center);   // 오른쪽 표값 안내와 안 겹치게
            }
            string pr = sim.ScratchLeft > 0 ? Loc.T("공짜 표 ") + sim.ScratchLeft + Loc.T("장") : Loc.T("이번 장 ") + KNum.Fmt(sim.ScratchPrice) + Loc.T(" · 다음 장 ") + KNum.Fmt(sim.ScratchNextPrice);
            Lbl(new Rect(r.xMax - 360, r.yMax - 46, 240, 30), "<size=11><color=#9a8ca6>" + pr + Loc.T("\n출동하면 표값 원래대로") + "</color></size>", small);
        }
        string ScPrize(int k)
        {
            double m = sim.ScratchMoney(k);
            switch (k)
            {
                case 0: return Loc.T("판 벌이 × ") + SweepSim.ScratchK[k] * (sim.Lv("l_jack") > 0 ? 3 : 1) + " = " + KNum.Fmt(m) + Loc.T(" + 부품");
                case 2: return Loc.T("가게 부품 1 무료");
                case 3: return Loc.T("열쇠 1");
                case 5: return Loc.T("다음 판 화력 +30%");
                case 6: return Loc.T("다음 판 연료 +10초");
                default: return Loc.T("판 벌이 × ") + SweepSim.ScratchK[k] * (sim.Lv("l_jack") > 0 ? 3 : 1) + " = " + KNum.Fmt(m);
            }
        }

        // ✍ 대출 계약서 — 누르면 바로가 아니라, 계약서를 펼치고 서명란에 직접 그어 서명 → 「승인」 도장 → 돈 (사장님 09-23)
        double pendLoan; bool pendPay; float signT = -1; readonly List<Vector2> signPts = new List<Vector2>(); float signLen; bool signing;
        public void TestSign() { signPts.Clear(); for (int i = 0; i < 30; i++) signPts.Add(new Vector2(vw / 2 - 150 + i * 9, 360 + Mathf.Sin(i * 0.9f) * 18)); signLen = 300; signT = Time.unscaledTime; }   // 에디터 시험용
        public void RequestLoan(double amt, bool payBill)
        {
            if (amt <= 0) return;
            pendLoan = System.Math.Ceiling(amt); pendPay = payBill; signT = -1; signPts.Clear(); signLen = 0; signing = false;
            loanOpen = true; OrbitSfx.Play("tick", 0.6f, 0.05f, 0.02f);
        }
        void Contract()
        {
            var S = sim.S;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 220, 56, 440, 470);
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, r.width, r.height), white); GUI.color = Color.white;
            GUI.DrawTexture(r, texPaper);
            GUI.DrawTexture(new Rect(r.x + 20, r.y + 58, r.width - 40, 2), texInk);
            paperHead.fontSize = 24; Lbl(new Rect(r.x, r.y + 16, r.width, 36), Loc.T("대출 계약서"), new GUIStyle(paperHead) { alignment = TextAnchor.UpperCenter });
            paperSmall.fontSize = 11; Lbl(new Rect(r.x + 20, r.y + 62, r.width - 40, 18), Loc.T("채권자 케슬러 금융 · 채무자 주식회사 궤도 청소부 (") + sim.M.company + Loc.T("대)"), paperSmall);
            string[] k = { Loc.T("빌리는 돈"), Loc.T("갚을 돈"), Loc.T("갚는 법"), Loc.T("쓰는 곳") };
            string[] v = { KNum.Fmt(pendLoan), KNum.Fmt(pendLoan * SweepSim.LoanMult) + "  (" + SweepSim.LoanMult + Loc.T("배)"), Loc.T("판 수입의 ") + (sim.Lv("e_guard") > 0 ? 20 : 30) + Loc.T("%가 자동으로"), pendPay ? Loc.T("청구서 즉시 납부") : Loc.T("돈으로 입금") };
            for (int i = 0; i < 4; i++)
            {
                float y = r.y + 92 + i * 34;
                paperBody.fontSize = 14; Lbl(new Rect(r.x + 30, y, 120, 24), k[i], paperBody);
                paperBody.fontSize = i < 2 ? 18 : 14; Lbl(new Rect(r.x + 150, y - (i < 2 ? 3 : 0), r.width - 180, 28), i < 2 ? "<b>" + v[i] + "</b>" : v[i], paperBody);
                GUI.color = new Color(0, 0, 0, 0.12f); GUI.DrawTexture(new Rect(r.x + 30, y + 27, r.width - 60, 1), white); GUI.color = Color.white;
            }
            paperBody.fontSize = 14;
            // 서명란 — 마우스로 그어 서명
            var sa = new Rect(r.x + 40, r.y + 250, r.width - 80, 100);
            GUI.color = new Color(0, 0, 0, 0.04f); GUI.DrawTexture(sa, white); GUI.color = Color.white;
            GUI.DrawTexture(new Rect(sa.x, sa.yMax - 22, sa.width, 2), texInk);
            paperSmall.fontSize = 11; Lbl(new Rect(sa.x, sa.yMax - 18, 200, 16), Loc.T("서명"), paperSmall);
            var ev = Event.current;
            bool done = signT >= 0;
            if (!done)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && sa.Contains(ev.mousePosition)) { signing = true; signPts.Add(new Vector2(-1, -1)); signPts.Add(ev.mousePosition); ev.Use(); }
                else if (ev.type == EventType.MouseDrag && signing)
                {
                    var q = new Vector2(Mathf.Clamp(ev.mousePosition.x, sa.x, sa.xMax), Mathf.Clamp(ev.mousePosition.y, sa.y, sa.yMax));
                    var last = signPts[signPts.Count - 1];
                    if ((q - last).sqrMagnitude > 4) { signLen += (q - last).magnitude; signPts.Add(q); if (signPts.Count % 6 == 0) OrbitSfx.Play("tick", 0.15f, 0.04f, 0.3f); }
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp && signing)
                {
                    signing = false; ev.Use();
                    if (signLen > 140) { signT = Time.unscaledTime; OrbitSfx.Play("clank", 0.9f); }   // 쾅 — 도장
                }
                if (signPts.Count == 0) Lbl(new Rect(sa.x, sa.y + 22, sa.width, 24), Loc.T("<color=#8a7f6a>여기를 마우스로 그어 서명하세요</color>"), paperBody);
            }
            for (int i = 1; i < signPts.Count; i++)
                if (signPts[i - 1].x >= 0 && signPts[i].x >= 0) Line(signPts[i - 1], signPts[i], new Color(0.1f, 0.14f, 0.35f), 2.6f);
            // 도장 — 쾅 찍히고 조금 뒤 돈이 들어온다
            if (done)
            {
                float k2 = Mathf.Clamp01((Time.unscaledTime - signT) / 0.18f);
                float sc = Mathf.Lerp(2.2f, 1f, k2);
                var c = new Vector2(r.xMax - 110, r.y + 300);
                var m = GUI.matrix;
                GUI.matrix = m * Matrix4x4.TRS(c, Quaternion.Euler(0, 0, -14), new Vector3(sc, sc, 1)) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);   // 화면 배율(GUI.matrix) 위에서 돌린다 — RotateAroundPivot 은 배율이 걸리면 자리가 어긋났다 (10-02)
                GUI.color = new Color(0.78f, 0.14f, 0.12f, 0.85f * k2);
                GUI.DrawTexture(new Rect(c.x - 52, c.y - 52, 104, 104), texRing);
                GUI.DrawTexture(new Rect(c.x - 44, c.y - 44, 88, 88), texRing);
                Lbl(new Rect(c.x - 60, c.y - 20, 120, 40), Loc.T("<size=26><b><color=#c42420>승인</color></b></size>"), center);
                Lbl(new Rect(c.x - 60, c.y + 14, 120, 20), Loc.T("<size=10><color=#c42420>케슬러 금융</color></size>"), center);
                GUI.matrix = m; GUI.color = Color.white;
                if (Time.unscaledTime - signT > 0.9f)
                {
                    bool ok = pendPay ? sim.LoanAndPay() : sim.TakeLoan(pendLoan);
                    if (ok) { OrbitSfx.Play("buy", 0.9f); game.creditPulse = 1; }
                    pendLoan = 0; signT = -1;
                    if (pendPay) loanOpen = false;
                }
            }
            else if (Bt(new Rect(r.xMax - 120, r.yMax - 52, 100, 34), Loc.T("취소"), btnOff)) { pendLoan = 0; if (pendPay) loanOpen = false; }
            if (!done) Lbl(new Rect(r.x + 20, r.yMax - 50, 260, 30), Loc.T("<size=11><color=#8a7f6a>서명하면 도장과 함께 입금</color></size>"), paperBody);
        }

        void LoanWin()
        {
            var S = sim.S;
            if (pendLoan > 0) { Contract(); return; }
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 280, 70, 560, 440);
            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.99f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.62f, 0.5f, 0.3f), 2);
            title.fontSize = 22; Lbl(new Rect(r.x, r.y + 12, r.width, 30), Loc.T("<color=#ffdf95>케슬러 금융 — 대출 창구</color>"), title);
            Lbl(new Rect(r.x, r.y + 44, r.width, 18), Loc.T("<size=12>받은 돈의 ") + SweepSim.LoanMult + Loc.T("배 상환 · 빚이 있으면 판 수입의 ") + Mathf.RoundToInt((float)(sim.Lv("e_guard") > 0 ? 20 : 30)) + Loc.T("% 자동 차감</size>"), center);
            // 숫자 셋
            float cx = r.x + 24, cw = (r.width - 48) / 3;
            string[] k = { Loc.T("지금 빚"), Loc.T("대출 한도"), Loc.T("가진 돈") };
            string[] v = { KNum.Fmt(S.debt), KNum.Fmt(sim.LoanCap), KNum.Fmt(S.cash) };
            for (int i = 0; i < 3; i++)
            {
                var cr = new Rect(cx + i * cw + 4, r.y + 72, cw - 8, 60);
                GUI.DrawTexture(cr, texCard);
                Lbl(new Rect(cr.x, cr.y + 6, cr.width, 16), "<size=11>" + k[i] + "</size>", center);
                Lbl(new Rect(cr.x, cr.y + 24, cr.width, 30), "<size=20><color=" + (i == 0 && S.debt > 0 ? "#ffb3a8" : "#ffdf95") + ">" + v[i] + "</color></size>", center);
            }
            // 받기 · 갚기
            double quarter = System.Math.Min(sim.LoanCap, System.Math.Ceiling(sim.BillAmount * 0.25));
            float by = r.y + 146;
            GUI.enabled = quarter > 0;
            if (Bt(new Rect(cx + 4, by, 160, 36), Loc.T("<size=12>대출 +") + KNum.Fmt(quarter) + "</size>", btnC)) RequestLoan(quarter, false);
            GUI.enabled = sim.LoanCap > 0;
            if (Bt(new Rect(cx + 172, by, 160, 36), Loc.T("<size=12>한도까지 +") + KNum.Fmt(sim.LoanCap) + "</size>", btnC)) RequestLoan(sim.LoanCap, false);
            GUI.enabled = S.debt > 0 && S.cash > 0;
            if (Bt(new Rect(cx + 340, by, 168, 36), (S.debt > 0 ? Loc.T("<size=12>빚 갚기 −") + KNum.Fmt(System.Math.Min(S.cash, S.debt)) + "</size>" : Loc.T("<size=12>갚을 빚 없음</size>")), btnC)) Repay();
            GUI.enabled = true;
            Lbl(new Rect(cx + 4, by + 38, 500, 20), S.bill >= SweepSim.Bills.Length - 1
                ? Loc.T("<size=11><color=#ff9b8f>마지막 할부(완납)엔 대출 불가 — 직접 갚거나, 못 갚으면 파산</color></size>")
                : Loc.T("<size=11>받으면 빚 +") + KNum.Fmt(quarter * SweepSim.LoanMult) + Loc.T(" · 한도 = 지금 청구서 − 남은 원금</size>"), small);
            // 내역
            float hy = by + 64;
            Lbl(new Rect(cx + 4, hy - 2, 300, 22), Loc.T("내역"), label);
            GUI.DrawTexture(new Rect(cx + 4, hy + 20, r.width - 56, 1), texBar);
            var log = S.loanLog;
            if (log == null || log.Count == 0) Lbl(new Rect(cx + 4, hy + 28, 400, 18), Loc.T("<size=12>아직 없음</size>"), small);
            else
                for (int i = 0; i < 8 && i < log.Count; i++)
                {
                    var e = log[log.Count - 1 - i];
                    string what = e.kind == 0 ? Loc.T("<color=#ffdf95>대출 +") + KNum.Fmt(e.amt) + Loc.T("</color>  → 빚 +") + KNum.Fmt(e.amt * SweepSim.LoanMult)
                                : e.kind == 1 ? Loc.T("<color=#6fcf97>판 수입에서 상환 −") + KNum.Fmt(e.amt) + "</color>"
                                : Loc.T("<color=#6fcf97>직접 상환 −") + KNum.Fmt(e.amt) + "</color>";
                    Lbl(new Rect(cx + 4, hy + 26 + i * 20, 90, 18), Loc.T("<size=11>출동 ") + e.run + "</size>", small);
                    Lbl(new Rect(cx + 96, hy + 26 + i * 20, 420, 18), "<size=12>" + what + "</size>", label);
                }
            if (Bt(new Rect(r.xMax - 110, r.yMax - 44, 96, 32), Loc.T("닫기"), btnC)) loanOpen = false;
        }

        bool CanPayNow => !sim.M.cleanReady && sim.S.bill < SweepSim.Bills.Length && sim.S.cash >= sim.BillAmount;

        /// <summary>🔴 갚을 수 있으면 창 가운데에 크게 — 사장님이 돈 357 을 들고 30짜리 첫 청구서를 안 갚으셨다 (09-23 첫 플레이)</summary>

        /// <summary>귀환 직후 2.6초 — 창 가운데에 크게 (§9-1)</summary>







    
        // ✍ 청소선 할부 계약서 — 새 회사마다 (10-02 사장님 「사은품 주식」 · 시안 XbhugR76DmgJ5myAa7iz41)
        // 처음 회사: 윤 대리 첫 인사 끝 → 계약서 (마우스로 그어 서명) → pro2. 파산 뒤: re1 → 계약서 ([서명] 단추 한 번) → re2.
        // 종이 · 도장 = 대출 계약서와 같다. 서명하면 사은품 주식이 오른쪽 [증권] 탭으로 날아간다.
        bool hireOpen; float hireSignT = -1, hireFlyT = -1; int hireCo = -1, newStockBadge;
        public void TestHire(bool stamped = false) { sim.S.hired = false; hireOpen = true; hireSignT = -1; signPts.Clear(); signLen = 0; signing = false; if (stamped) { for (int i = 0; i < 30; i++) signPts.Add(new Vector2(vw / 2 - 150 + i * 9, 375 + Mathf.Sin(i * 0.9f) * 18)); hireSignT = Time.unscaledTime; } }   // 에디터 시험용
        bool HireTrigger()
        {
            var S = sim.S;
            if (S.hired) return false;
            if (S.runs > 0 || S.bill > 0) { S.hired = true; return false; }   // 옛 저장 — 이미 일하던 회사는 건너뛴다
            if (flow != 2) return false;
            if (hireOpen) return true;
            if (!sim.M.flags.Contains("g:yoon0")) return false;                 // 첫 인사(pro)가 먼저 — RadioBox 가 띄운다
            if (sim.M.company > 1 && hireCo != sim.M.company) { hireCo = sim.M.company; DlgStart("re1", true); return true; }
            hireOpen = true; hireSignT = -1; signPts.Clear(); signLen = 0; signing = false; OrbitSfx.Play("tick", 0.6f, 0.05f, 0.02f);
            return true;
        }
        void HireWin()
        {
            bool quick = sim.M.company > 1;
            GUI.DrawTexture(new Rect(0, 0, vw, RefH), texDim);
            var r = new Rect(vw / 2 - 220, 26, 440, 520);
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, r.width, r.height), white); GUI.color = Color.white;
            GUI.DrawTexture(r, texPaper);
            GUI.DrawTexture(new Rect(r.x + 20, r.y + 58, r.width - 40, 2), texInk);
            paperHead.fontSize = 24; Lbl(new Rect(r.x, r.y + 16, r.width, 36), Loc.T("청소선 할부 계약서"), new GUIStyle(paperHead) { alignment = TextAnchor.UpperCenter });
            paperSmall.fontSize = 11; Lbl(new Rect(r.x + 20, r.y + 62, r.width - 40, 18), Loc.T("채권자 케슬러 금융 · 채무자 주식회사 궤도 청소부 (") + sim.M.company + Loc.T("대)"), paperSmall);
            string[] k = { Loc.T("물건"), Loc.T("청구서"), Loc.T("완납 시") };
            string[] v = { SweepSim.Ships[sim.Ship].name + Loc.T(" 1척"), Loc.T("12장 · 첫 장 ") + SweepSim.Bills[0].t + " " + KNum.Fmt(sim.BillAmount), Loc.T("청소선 소유권 이전") };
            for (int i = 0; i < 3; i++)
            {
                float y = r.y + 90 + i * 32;
                paperBody.fontSize = 14; Lbl(new Rect(r.x + 30, y, 110, 24), k[i], paperBody);
                Lbl(new Rect(r.x + 140, y, r.width - 170, 24), Fit(v[i], 15, r.width - 170, paperBody, 11), paperBody);
                GUI.color = new Color(0, 0, 0, 0.12f); GUI.DrawTexture(new Rect(r.x + 30, y + 26, r.width - 60, 1), white); GUI.color = Color.white;
            }
            // 사은품 — 빨간 테
            var gb = new Rect(r.x + 30, r.y + 196, r.width - 60, 30 + 22 * SweepSim.HireGift.Length);
            GUI.color = new Color(0.72f, 0.26f, 0.18f); Frame(gb, GUI.color, 2); GUI.color = Color.white;
            paperSmall.fontSize = 12; Lbl(new Rect(gb.x + 10, gb.y + 6, gb.width - 20, 18), Loc.T("<color=#b8432f>계약 사은품 — 케슬러 그룹 주식</color>"), paperSmall);
            for (int i = 0; i < SweepSim.HireGift.Length; i++)
            {
                int si = System.Array.FindIndex(Market.Defs, x => x.id == SweepSim.HireGift[i].id);
                paperBody.fontSize = 14; Lbl(new Rect(gb.x + 10, gb.y + 26 + i * 22, 200, 20), si >= 0 ? Market.Defs[si].name : "", paperBody);
                Lbl(new Rect(gb.x + 10, gb.y + 26 + i * 22, gb.width - 20, 20), Loc.T("평가 ") + KNum.Fmt(SweepSim.HireGift[i].amt), new GUIStyle(paperBody) { alignment = TextAnchor.UpperRight });
            }
            hireGiftPos = new Vector2(gb.center.x, gb.center.y);
            // 서명
            var sa = new Rect(r.x + 40, r.y + 316, r.width - 80, 100);
            var ev = Event.current; bool done = hireSignT >= 0;
            if (!quick)
            {
                GUI.color = new Color(0, 0, 0, 0.04f); GUI.DrawTexture(sa, white); GUI.color = Color.white;
                GUI.DrawTexture(new Rect(sa.x, sa.yMax - 22, sa.width, 2), texInk);
                paperSmall.fontSize = 11; Lbl(new Rect(sa.x, sa.yMax - 18, 200, 16), Loc.T("서명"), paperSmall);
                if (!done)
                {
                    if (ev.type == EventType.MouseDown && ev.button == 0 && sa.Contains(ev.mousePosition)) { signing = true; signPts.Add(new Vector2(-1, -1)); signPts.Add(ev.mousePosition); ev.Use(); }
                    else if (ev.type == EventType.MouseDrag && signing)
                    {
                        var q = new Vector2(Mathf.Clamp(ev.mousePosition.x, sa.x, sa.xMax), Mathf.Clamp(ev.mousePosition.y, sa.y, sa.yMax));
                        var last = signPts[signPts.Count - 1];
                        if ((q - last).sqrMagnitude > 4) { signLen += (q - last).magnitude; signPts.Add(q); if (signPts.Count % 6 == 0) OrbitSfx.Play("tick", 0.15f, 0.04f, 0.3f); }
                        ev.Use();
                    }
                    else if (ev.type == EventType.MouseUp && signing) { signing = false; ev.Use(); if (signLen > 140) { hireSignT = Time.unscaledTime; OrbitSfx.Play("clank", 0.9f); } }
                    if (signPts.Count == 0) Lbl(new Rect(sa.x, sa.y + 22, sa.width, 24), Loc.T("<color=#8a7f6a>여기를 마우스로 그어 서명하세요</color>"), new GUIStyle(paperBody) { alignment = TextAnchor.UpperCenter });
                }
                for (int i = 1; i < signPts.Count; i++)
                    if (signPts[i - 1].x >= 0 && signPts[i].x >= 0) Line(signPts[i - 1], signPts[i], new Color(0.1f, 0.14f, 0.35f), 2.6f);
            }
            else if (!done && Bt(new Rect(r.center.x - 70, sa.y + 30, 140, 40), Loc.T("<size=16>서명</size>"), btn)) { hireSignT = Time.unscaledTime; OrbitSfx.Play("clank", 0.9f); }
            if (!done) Lbl(new Rect(r.x + 20, r.yMax - 40, r.width - 40, 30), Loc.T("<size=11><color=#8a7f6a>서명하면 도장과 함께 사은품 주식 지급</color></size>"), paperBody);
            if (done)
            {
                float k2 = Mathf.Clamp01((Time.unscaledTime - hireSignT) / 0.18f), sc = Mathf.Lerp(2.2f, 1f, k2);
                var c = new Vector2(r.xMax - 110, r.y + 360);
                var m = GUI.matrix;
                GUI.matrix = m * Matrix4x4.TRS(c, Quaternion.Euler(0, 0, -14), new Vector3(sc, sc, 1)) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);   // 화면 배율(GUI.matrix) 위에서 돌린다 — RotateAroundPivot 은 배율이 걸리면 자리가 어긋났다 (10-02)
                GUI.color = new Color(0.78f, 0.14f, 0.12f, 0.85f * k2);
                GUI.DrawTexture(new Rect(c.x - 52, c.y - 52, 104, 104), texRing);
                GUI.DrawTexture(new Rect(c.x - 44, c.y - 44, 88, 88), texRing);
                Lbl(new Rect(c.x - 60, c.y - 20, 120, 40), Loc.T("<size=26><b><color=#c42420>승인</color></b></size>"), center);
                Lbl(new Rect(c.x - 60, c.y + 14, 120, 20), Loc.T("<size=10><color=#c42420>케슬러 금융</color></size>"), center);
                GUI.matrix = m; GUI.color = Color.white;
                if (Time.unscaledTime - hireSignT > 1.0f)
                {
                    sim.SignHire(); OrbitSfx.Play("buy", 0.8f);
                    hireOpen = false; hireSignT = -1; signPts.Clear(); hireFlyT = Time.unscaledTime; newStockBadge = SweepSim.HireGift.Length;
                    DlgStart(quick ? "re2" : "pro2", quick); game.Save();
                }
            }
        }
        Vector2 hireGiftPos;
        /// <summary>사은품 주식 칩이 계약서에서 오른쪽 [증권] 탭으로 날아간다 (0.8초 · 둘이 조금씩 어긋나게)</summary>
        void HireFly()
        {
            if (hireFlyT < 0) return;
            float e = Time.unscaledTime - hireFlyT; if (e > 1.3f) { hireFlyT = -1; return; }
            var to = new Vector2(Mathf.Min(vw - 21, ox + 964 + 21), 300);
            for (int i = 0; i < SweepSim.HireGift.Length; i++)
            {
                float t = Mathf.Clamp01((e - i * 0.16f) / 0.8f); if (t <= 0 || t >= 1) continue;
                float u = reduceMotion ? 1 : t * t * (3 - 2 * t);
                var p = Vector2.Lerp(hireGiftPos + new Vector2(0, i * 22), to, u) - new Vector2(0, Mathf.Sin(u * Mathf.PI) * 40);
                int si = System.Array.FindIndex(Market.Defs, x => x.id == SweepSim.HireGift[i].id);
                string tx = "<size=12><color=#bff4d8>" + (si >= 0 ? Market.Defs[si].name : "") + " +" + KNum.Fmt(SweepSim.HireGift[i].amt) + "</color></size>";
                float w = center.CalcSize(new GUIContent(tx)).x + 16; var rr = new Rect(p.x - w / 2, p.y - 12, w, 24);
                GUI.color = new Color(0.04f, 0.1f, 0.08f, 0.95f); GUI.DrawTexture(rr, white); Frame(rr, new Color(0.44f, 0.81f, 0.59f), 1.5f); GUI.color = Color.white;
                Lbl(rr, tx, center);
            }
        }
}
}
