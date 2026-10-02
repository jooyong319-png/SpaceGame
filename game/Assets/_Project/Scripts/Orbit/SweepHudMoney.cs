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
        const int ScCols = 8, ScRows = 6, ScCells = 3;                      // 칸 셋 · 칸마다 은박 조각 (09-27 아홉 칸 → 셋)
        static Texture2D[] scIcon; bool scUse;
        static readonly string[] ScIconPath = { "junk/junk_chip_a", "junk/junk_sat", "junk/junk_vault", "shop/key", "shop/part_0", "junk/junk_vault_gold" };
        public void TestLotto(int step)   // 에디터 시험용
        {
            if (step == 0) { lottoOpen = true; lottoTab = 0; scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[ScCells, ScCols * ScRows]; scDone = false; }
            else if (step == 1) { for (int c = 0; c < ScCells; c++) for (int b = 0; b < ScCols * ScRows; b++) if (c == 0 || (b + c) % 3 != 0 && c == 1) scCoat[c, b] = true; }
            else if (step == 2) { lottoTab = 1; lottoPick.Clear(); lottoPick.AddRange(new[] { 3, 7, 11 }); sim.LottoBuy(3, 7, 11); lottoPick.Clear(); lottoPick.AddRange(new[] { 2, 5 }); }
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
            var r = new Rect(vw / 2 - 300, 60, 600, 480);
            GUI.color = new Color(0.07f, 0.03f, 0.09f, 0.96f); GUI.DrawTexture(r, white);                    // 분홍 홀로그램 테 (09-24)
            GUI.color = new Color(1f, 0.66f, 0.94f, 0.05f); for (float yy = r.y + 2; yy < r.yMax; yy += 4) GUI.DrawTexture(new Rect(r.x, yy, r.width, 1), white);
            Frame(r, new Color(1f, 0.66f, 0.94f, 0.85f), 1.5f);
            GUI.color = new Color(1f, 0.66f, 0.94f); foreach (var cn in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax - 14, r.y), new Vector2(r.x, r.yMax - 3), new Vector2(r.xMax - 14, r.yMax - 3) }) GUI.DrawTexture(new Rect(cn.x, cn.y, 14, 3), white);
            GUI.color = Color.white;
            Lbl(new Rect(r.x + 20, r.y + 14, 200, 30), Loc.T("<size=18><b><color=#f3c8ff>즉석 복권</color></b></size>"), label);   // 🎱 궤도 로또는 뺐다 (09-24 사장님 「로또는 의미가 없다」)
            Lbl(new Rect(r.x, r.y + 18, r.width - 20, 24), Loc.T("<size=13><color=#8a9bb3>돈</color> ") + KNum.Fmt(S.cash) + "</size>", cost);
            Scratch(r, ev);
            if (Bt(new Rect(r.xMax - 106, r.yMax - 46, 92, 34), Loc.T("닫기"), btnOffC)) lottoOpen = false;
        }

        void Scratch(Rect r, Event ev)
        {   // 🎟 09-27 칸 셋 · 도트 그림 · 문지르면 한 번에 여러 칸 (시안 TxxPY6mLoewcLnW1vmNVBG)
            var S = sim.S;
            if (scIcon == null) { scIcon = new Texture2D[ScIconPath.Length]; for (int i = 0; i < ScIconPath.Length; i++) scIcon[i] = Resources.Load<Texture2D>(ScIconPath[i]); }
            int leftN = sim.ScratchLeft + (scGrid != null && !scDone ? 1 : 0);          // 긁고 있는 장도 센다 (09-27 「남은 개수가 이상」)
            Lbl(new Rect(r.x + 20, r.y + 52, r.width - 40, 22), Loc.T("<size=13>한 장 <color=#ffdf95>") + KNum.Fmt(sim.ScratchPrice) + Loc.T("</color> · 같은 그림 셋 = 당첨 · 둘 = 표값 돌려받기</size>"), label);
            Lbl(new Rect(r.x + 20, r.y + 52, r.width - 40, 22), Loc.T("<size=13><color=#f3c8ff>남은 표 ") + leftN + Loc.T("장") + (scGrid != null && !scDone ? Loc.T(" (이 장 포함)") : "") + (S.freeTix > 0 ? Loc.T(" · <color=#9ff0bf>공짜 ") + S.freeTix + "</color>" : "") + "</color></size>", cost);
            // 표 — 분홍 바탕에 창 셋
            var card = new Rect(r.center.x - 250, r.y + 88, 500, 230);
            GUI.color = new Color(0.23f, 0.06f, 0.2f); GUI.DrawTexture(new Rect(card.x - 4, card.y - 4, card.width + 8, card.height + 8), white);
            GUI.color = new Color(1f, 0.7f, 0.86f); GUI.DrawTexture(card, white);
            GUI.color = new Color(1f, 0.83f, 0.45f, 0.55f); GUI.DrawTexture(new Rect(card.x, card.yMax - 40, card.width, 40), white); GUI.color = Color.white;
            Lbl(new Rect(card.x + 14, card.y + 8, 300, 26), Loc.T("<size=17><b><color=#3a0f33>궤도 즉석 복권</color></b></size>"), label);
            if (scGrid == null)
            {
                Lbl(new Rect(card.x, card.y + 40, card.width, card.height - 60), Loc.T("<size=18><color=#5a2050>한 장 사서 긁어 보세요</color></size>"), center);
            }
            float gap = 14, cw = (card.width - gap * 4) / 3, ch = card.height - 70;
            int revealed = 0;
            for (int c = 0; c < ScCells && scGrid != null; c++)
            {
                var cr = new Rect(card.x + gap + c * (cw + gap), card.y + 42, cw, ch);
                int sym = scGrid[c];
                bool winCell = scDone && (scWin >= 0 || scWin == -2 && System.Array.IndexOf(scGrid, sym) != System.Array.LastIndexOf(scGrid, sym));
                GUI.color = new Color(0.23f, 0.06f, 0.2f); GUI.DrawTexture(new Rect(cr.x - 3, cr.y - 3, cr.width + 6, cr.height + 6), white);
                GUI.color = winCell ? new Color(1f, 0.95f, 0.62f) : new Color(1f, 0.97f, 0.93f); GUI.DrawTexture(cr, white); GUI.color = Color.white;
                var ic = scIcon[Mathf.Clamp(sym, 0, scIcon.Length - 1)];
                if (ic != null) { float isz = Mathf.Min(cr.width, cr.height) * 0.66f; GUI.DrawTexture(new Rect(cr.center.x - isz / 2, cr.center.y - isz / 2 - 8, isz, isz), ic, ScaleMode.ScaleToFit); }
                Lbl(new Rect(cr.x, cr.yMax - 24, cr.width, 20), "<size=12><color=#5a2050>" + SweepSim.ScratchSym[sym] + "</color></size>", center);
                // 은박 — 누른 채 지나가면 벗겨진다 (칸 사이를 한 번에 문질러도 된다)
                int left = 0; float bw = cr.width / ScCols, bh = cr.height / ScRows;
                for (int b = 0; b < ScCols * ScRows; b++)
                {
                    if (scCoat[c, b]) continue; left++;
                    if (scDone) continue;
                    var br = new Rect(cr.x + (b % ScCols) * bw, cr.y + (b / ScCols) * bh, bw + 0.5f, bh + 0.5f);
                    float shade = (b % ScCols) % 2 == 0 ? 0.74f : 0.8f;
                    GUI.color = new Color(shade, shade, shade + 0.04f); GUI.DrawTexture(br, white); GUI.color = Color.white;
                }
                if (left == ScCols * ScRows && !scDone) Lbl(cr, Loc.T("<size=13><color=#7a5a86>긁기</color></size>"), center);
                if (left <= ScCols * ScRows * 0.45f) revealed++;
                if (!scDone && (ev.type == EventType.MouseDrag || ev.type == EventType.MouseDown) && ev.button == 0 && new Rect(cr.x - 20, cr.y - 20, cr.width + 40, cr.height + 40).Contains(ev.mousePosition))
                {
                    for (int b = 0; b < ScCols * ScRows; b++)
                    {
                        var bc = new Vector2(cr.x + (b % ScCols + 0.5f) * bw, cr.y + (b / ScCols + 0.5f) * bh);
                        if (!scCoat[c, b] && (bc - ev.mousePosition).sqrMagnitude < 26 * 26) { scCoat[c, b] = true; if (Random.value < 0.2f) OrbitSfx.PlayPitch("tick", 0.2f, 1.6f + Random.value * 0.4f); }
                    }
                    scUse = true;
                }
            }
            if (scUse) { scUse = false; ev.Use(); }
            // 당첨표 — 그림 여섯
            string[] pay = { "× 0.2", Loc.En ? "Fire +30%" : Loc.T("화력 +30%"), "× 1", Loc.T("열쇠"), Loc.T("부품"), "× 10" };   // 🌐 칸이 좁아 영어는 짧게
            for (int k = 0; k < 6; k++)
            {
                var pr = new Rect(r.x + 24 + k * 94, r.y + 330, 90, 30);
                if (scIcon[k] != null) GUI.DrawTexture(new Rect(pr.x, pr.y + 2, 26, 26), scIcon[k], ScaleMode.ScaleToFit);
                Lbl(new Rect(pr.x + 28, pr.y + 5, 64, 20), "<color=" + (k == 5 ? "#ffdf95" : "#c9b3d6") + ">" + Fit(pay[k], 11, 62, label) + "</color>", label);   // 🌐 영어 Firepower +30% 는 칸보다 넓다
            }
            Lbl(new Rect(r.x + 24, r.y + 360, r.width - 48, 18), Loc.T("<size=10><color=#8a7a96>× = 판 벌이 · 부품은 가게 진열에서 하나 무료 장착 · 황금은 부품도 하나</color></size>"), label);
            if (scGrid != null && !scDone && revealed == ScCells) { scDone = true; scDoneT = Time.unscaledTime; scGot = sim.ScratchClaim(); if (scWin != -1) { OrbitSfx.Play("buy", 1f); OrbitSfx.Play("clank", 0.7f); game.creditPulse = 1; } else OrbitSfx.PlayPitch("tick", 0.6f, 0.7f); }
            bool can = sim.ScratchLeft > 0 && S.cash >= sim.ScratchCost;
            string buyTxt = can ? "<size=16>" + (scGrid == null ? Loc.T("복권 1장 구매") : Loc.T("한 장 더")) + " · " + (sim.ScratchCost <= 0 ? Loc.T("공짜") : KNum.Fmt(sim.ScratchCost)) + "</size>" : "<size=12>" + (sim.ScratchLeft <= 0 ? Loc.T("오늘 표 소진 — 출동하면 다시 충전") : Loc.T("돈 부족")) + "</size>";
            if (scGrid != null && !scDone)
            {
                Lbl(new Rect(r.x, r.yMax - 96, r.width, 22), Loc.T("<size=13><color=#b89ac6>누른 채 문질러 긁기</color></size>"), center);
                if (Bt(new Rect(r.center.x - 90, r.yMax - 70, 180, 36), Loc.T("<size=14>한 번에 다 긁기</size>"), btn)) for (int c = 0; c < ScCells; c++) for (int b = 0; b < ScCols * ScRows; b++) scCoat[c, b] = true;
                return;
            }
            if (scDone)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - scDoneT) / 0.25f);
                string msg = scWin == -1 ? Loc.T("<size=22><color=#8a93a3>꽝</color></size>") : "<size=" + Mathf.RoundToInt(Mathf.Lerp(30, 20, k)) + "><b><color=#ffdf95>" + sim.ScratchText + (scGot > 0 ? "  +" + KNum.Fmt(scGot) : "") + "</color></b></size>";
                Lbl(new Rect(r.x, r.yMax - 104, r.width, 36), msg, center);
            }
            if (Bt(new Rect(r.center.x - 120, r.yMax - 62, 240, 40), buyTxt, can ? btn : btnOff) && can)
            { scGrid = sim.ScratchBuy(out scWin); scCoat = new bool[ScCells, ScCols * ScRows]; scDone = false; scGot = 0; OrbitSfx.Play("buy", 0.5f); }
        }

        void Lotto(Rect r)
        {
            var S = sim.S;
            Lbl(new Rect(r.x + 20, r.y + 58, r.width - 40, 24), Loc.T("<size=15><b>제 ") + S.lottoRound + Loc.T("회</b> · <color=#ffdf95>출동 다녀오면 바로 추첨</color></size>"), label);
            Lbl(new Rect(r.x + 20, r.y + 82, r.width - 40, 20), Loc.T("<size=11><color=#8a9bb3>1~12 중 셋 · 한 장 ") + KNum.Fmt(sim.LottoPrice) + Loc.T(" · 한 회 3장까지 · 셋 다 ×60 · 둘 ×2 · 하나 ×0.3</color></size>"), label);
            // 번호판
            for (int n = 1; n <= 12; n++)
            {
                var nb = new Rect(r.x + 40 + ((n - 1) % 6) * 58, r.y + 116 + ((n - 1) / 6) * 58, 50, 50);
                bool on = lottoPick.Contains(n);
                GUI.color = on ? new Color(1f, 0.55f, 0.92f, 0.55f) : new Color(1f, 0.66f, 0.94f, 0.06f); GUI.DrawTexture(nb, texDisc);          // 빛 구슬
                GUI.color = new Color(1f, 0.66f, 0.94f, on ? 1f : 0.45f); GUI.DrawTexture(nb, texRing); GUI.color = Color.white;
                Lbl(nb, "<size=18><b>" + (on ? "<color=#ffffff>" : "<color=#b89ac6>") + n + "</color></b></size>", center);
                if (Bt(nb, GUIContent.none, GUIStyle.none)) { if (on) lottoPick.Remove(n); else if (lottoPick.Count < 3) lottoPick.Add(n); OrbitSfx.Play("tick", 0.4f); }
            }
            bool can = lottoPick.Count == 3 && S.lotto.Count < 3 && S.cash >= sim.LottoPrice;
            if (Bt(new Rect(r.x + 400, r.y + 116, 170, 44), can ? Loc.T("<size=14>이 번호로 구매</size>") : "<size=12>" + (S.lotto.Count >= 3 ? Loc.T("이번 회는 3장까지") : lottoPick.Count < 3 ? Loc.T("번호 셋을 고르세요") : Loc.T("돈 부족")) + "</size>", can ? btnC : btnOffC) && can)
            { lottoPick.Sort(); if (sim.LottoBuy(lottoPick[0], lottoPick[1], lottoPick[2])) { OrbitSfx.Play("buy", 0.6f); lottoPick.Clear(); } }
            if (Bt(new Rect(r.x + 400, r.y + 166, 170, 34), Loc.T("<size=12>자동 고르기</size>"), btnOff))
            { lottoPick.Clear(); while (lottoPick.Count < 3) { int n = Random.Range(1, 13); if (!lottoPick.Contains(n)) lottoPick.Add(n); } OrbitSfx.Play("tick", 0.5f); }
            // 내 표
            Lbl(new Rect(r.x + 20, r.y + 244, 200, 22), Loc.T("<size=13>내 표</size>"), label);
            for (int i = 0; i < 3; i++)
            {
                var tr = new Rect(r.x + 20 + i * 180, r.y + 270, 170, 40);
                GUI.color = new Color(0.12f, 0.07f, 0.15f); GUI.DrawTexture(tr, white); GUI.color = Color.white; Frame(tr, new Color(0.4f, 0.25f, 0.45f), 1.5f);
                Lbl(tr, i < S.lotto.Count ? "<size=17><b>" + S.lotto[i].a + " · " + S.lotto[i].b + " · " + S.lotto[i].c + "</b></size>" : Loc.T("<size=12><color=#5f4f66>빈 칸</color></size>"), center);
            }
            // 지난 회
            if (S.lottoLast != null)
            {
                Lbl(new Rect(r.x + 20, r.y + 330, r.width - 40, 22), Loc.T("<size=13>지난 ") + S.lottoLastRound + Loc.T("회 당첨 번호  <b><color=#f3c8ff>") + S.lottoLast[0] + " · " + S.lottoLast[1] + " · " + S.lottoLast[2] + "</color></b></size>", label);
                Lbl(new Rect(r.x + 20, r.y + 354, r.width - 40, 22), "<size=12>" + (S.lottoLastHit < 0 ? Loc.T("<color=#8a9bb3>그 회차엔 표 없음</color>") : S.lottoLastWin > 0 ? "<color=#6fcf97>" + S.lottoLastHit + Loc.T("개 맞음 · +") + KNum.Fmt(S.lottoLastWin) + "</color>" : Loc.T("<color=#ee7766>꽝</color>")) + "</size>", label);
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
