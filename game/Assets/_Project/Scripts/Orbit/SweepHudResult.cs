using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SalvageRun.Orbit.Sim;

namespace SalvageRun.Orbit
{
    // 🧾 결산 화면 (09-28 나눔)
    public partial class SweepHud
    {
        // ───────────────────────────────── ① 결산 · ② 청구서 · ③ 정비고 아래 줄


        // ── 결산 화면 — Bills Must Be Paid 결산 틀 (사장님이 보여 주신 화면): 제목 · 왼쪽 성적과 합계 · 오른쪽 부순 것 · 다음 해금 · 아래 버튼 셋
        float resultAt, flyT; double endCash, gained;
        class Flyer { public Vector2 a, b; public float t; public string txt; }
        readonly List<Flyer> flyers = new List<Flyer>();

        void Panel2(Rect r, string head2)
        {
            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, new Color(0.16f, 0.2f, 0.27f), 1.5f);
            if (head2 != null) Lbl(new Rect(r.x + 14, r.y + 10, r.width - 28, 18), head2, head);
        }

        void FlowResult()
        {
            var S = sim.S; var R = last;
            GUI.color = new Color(0, 0, 0, 0.72f); GUI.DrawTexture(new Rect(0, 0, vw, RefH), white); GUI.color = Color.white;
            float t = Time.time - resultAt, tally = Mathf.Clamp01((t - 0.35f) / 1.3f);
            float cx = vw / 2;

            // 돈 칸 — 오른쪽 위. 합계가 여기로 날아와 더해진다
            var money = new Rect(vw - 300, 14, 210, 44);                        // 오른쪽 끝은 소리 버튼 자리
            GUI.color = new Color(0.14f, 0.12f, 0.08f, 0.95f); GUI.DrawTexture(money, white); GUI.color = Color.white;
            Frame(money, SweepGame.Amber * new Color(1, 1, 1, 0.6f), 1.5f);
            big.fontSize = 22;   // 도트 글꼴 11의 배수 (정돈 5)
            Lbl(new Rect(money.x + 12, money.y + 6, money.width - 24, 34), "<color=#ffdf95>" + KNum.Fmt(endCash - gained * (1 - tally)) + "</color>", big);
            big.fontSize = 20;

            // 제목
            title.fontSize = 40;
            Lbl(new Rect(cx - 300, 70, 600, 52), Loc.T("연료 바닥!"), title);

            // 왼쪽 — 이번 출동: 부순 것 종류별 · 합계
            var L = new Rect(cx - 420, 140, 410, 250);
            Panel2(L, null);
            float y = L.y + 14;
            void Line2(string k, string v) { Lbl(new Rect(L.x + 18, y, L.width - 36, 26), "<size=18>" + k + "</size>", label); Lbl(new Rect(L.x + 18, y, L.width - 36, 26), "<size=18>" + v + "</size>", cost); y += 34; }
            Line2(Loc.T("부순 것"), Mathf.RoundToInt(R.broke * tally).ToString());
            // 종류별 아이콘 줄
            int[] counts = { R.cChip, R.cSat, R.cFuel, R.cVault, R.cTank, R.cBig };
            int[] kinds = { SweepSim.Chip, SweepSim.Sat, SweepSim.Rocket, SweepSim.Vault, SweepSim.Tank, SweepSim.Big };
            float ix = L.x + 22;
            // 칸 폭을 글자에 맞춘다 — 다섯 자리면 70px 고정 칸을 넘었다 (09-24 사장님 사진 2)
            string CntTxt(int c, bool shortF) => shortF && c >= 10000 ? (Loc.En ? KNum.Short(c) : (c / 10000f).ToString("0.#") + "만") : c.ToString();
            float need = 0; for (int i = 0; i < counts.Length; i++) if (counts[i] > 0) need += 34 + label.CalcSize(new GUIContent(counts[i].ToString())).x;
            bool shortC = need > L.width - 40;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0) continue;
                string ct = CntTxt(Mathf.RoundToInt(counts[i] * tally), shortC);
                float tw = label.CalcSize(new GUIContent(CntTxt(counts[i], shortC))).x;
                GUI.color = SweepGame.JunkColor(kinds[i]); GUI.DrawTexture(new Rect(ix, y + 4, 16, 16), kinds[i] == SweepSim.Chip || kinds[i] == SweepSim.Rocket ? white : texDisc); GUI.color = Color.white;
                Lbl(new Rect(ix + 20, y + 2, tw + 4, 20), ct, label);
                ix += 34 + tw;
            }
            y += 32;
            Line2(Loc.T("최대 연쇄"), R.chainBest + (R.chainBest > prevBestChain && R.chainBest >= 10 ? Loc.T(" <color=#ff8a7a>새 기록!</color>") : ""));
            y += 6;
            GUI.color = new Color(0.16f, 0.2f, 0.27f); GUI.DrawTexture(new Rect(L.x + 18, y, L.width - 36, 1.5f), white); GUI.color = Color.white;
            y += 12;
            var totalPos = new Vector2(L.xMax - 60, y + 14);
            Lbl(new Rect(L.x + 18, y, L.width - 36, 32), Loc.T("<size=24>합계</size>"), label);
            Lbl(new Rect(L.x + 18, y - 2, L.width - 36, 38), "<size=26><color=#6fcf97>+" + KNum.Fmt(gained * tally) + "</color></size>", cost);
            if (sim.Mk != null && runStockSh != null && runStockSh.Length == sim.Mk.M.st.Count)
            {   // 📈 내 주식 이번 판 — 출발 때 들고 있던 주식이 얼마나 움직였나 (09-24 사장님 4번)
                double v0 = 0, v1 = 0; for (int i = 0; i < runStockSh.Length; i++) { v0 += runStockSh[i] * runStockPx[i]; v1 += runStockSh[i] * sim.Mk.M.st[i].price; }
                if (v0 > 0)
                {
                    double d = v1 - v0; string hx = d >= 0 ? "#ff5c5c" : "#5494ff";
                    Lbl(new Rect(L.x + 18, y + 42, L.width - 36, 24), Loc.T("<size=16>내 주식 이번 판</size>"), label);
                    Lbl(new Rect(L.x + 18, y + 42, L.width - 36, 24), "<size=16><color=" + hx + ">" + (d >= 0 ? "+" : "") + KNum.Fmt(d) + "  (" + (d >= 0 ? "+" : "") + (d / v0 * 100).ToString("0.0") + "%)</color></size>", cost);
                }
            }

            // 합계 → 돈 칸으로 날아가는 「+」
            if (tally < 1)
            {
                flyT -= Time.deltaTime;
                if (flyT <= 0 && gained > 0) { flyT = 0.1f; flyers.Add(new Flyer { a = totalPos + new Vector2(Random.Range(-20f, 20f), 0), b = new Vector2(money.x + 40, money.center.y), txt = "+" + KNum.Fmt(System.Math.Max(1, System.Math.Round(gained / 30))) }); OrbitSfx.Play("coin", 0.25f, 0.04f, 0.15f); }
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i]; f.t += Time.deltaTime * 1.6f;
                if (f.t >= 1) { flyers.RemoveAt(i); game.creditPulse = 1; continue; }
                float e = f.t * f.t * (3 - 2 * f.t);
                var p = Vector2.Lerp(f.a, f.b, e) + new Vector2(0, -Mathf.Sin(e * Mathf.PI) * 60);
                pop.fontSize = 13; pop.normal.textColor = new Color(1f, 0.93f, 0.7f, 0.85f * (1 - f.t * 0.7f));
                Lbl(new Rect(p.x - 40, p.y - 10, 80, 20), f.txt, pop);
            }

            // 오른쪽 위 — 📊 무기별 피해 원그래프 (09-27 사장님 「출동 결과에서 어떤 무기들이 데미지 넣었는지 원그래프 · 블랙홀 기타 다 포함 · 잔잔바리는 기타로」)
            var RT = new Rect(cx + 10, 140, 410, 120);
            Panel2(RT, Loc.T("이번 판 피해 — 곳별"));
            DmgChart(RT, R, tally);
            if (R.cut > 0) Lbl(new Rect(RT.x + 16, RT.yMax - 2, RT.width - 32, 18), Loc.T("<size=11><color=#ee7766>빚 상환으로 떼인 것 -") + KNum.Fmt(R.cut) + "</color></size>", label);
            if (false) {
            double tot = System.Math.Max(1, R.Earned);
            float a = (float)(R.earnClaw / tot), b = (float)(R.earnDrone / tot);
            float bw = RT.width - 36, bx = RT.x + 18, by = RT.y + 38;
            GUI.DrawTexture(new Rect(bx, by, bw, 12), texBar);
            GUI.color = SweepGame.Amber; GUI.DrawTexture(new Rect(bx, by, bw * a * tally, 12), white);
            GUI.color = SweepGame.Cyan; GUI.DrawTexture(new Rect(bx + bw * a, by, bw * b * tally, 12), white);
            GUI.color = SweepGame.Violet; GUI.DrawTexture(new Rect(bx + bw * (a + b), by, bw * (1 - a - b) * tally, 12), white);
            GUI.color = Color.white;
            Lbl(new Rect(bx, by + 16, bw, 18), Loc.T("<color=#f2c14e>빔 ") + Mathf.RoundToInt(a * 100) + "%</color>   " + (sim.DronesOn ? Loc.T("<color=#6fd3e8>드론 ") + Mathf.RoundToInt(b * 100) + "%</color>   " : "") + (sim.BombsOn ? Loc.T("<color=#b69cff>폭발 ") + Mathf.RoundToInt((1 - a - b) * 100) + "%</color>" : ""), label);
            if (R.contractText != null) Lbl(new Rect(bx, by + 44, bw, 20), Loc.T("의뢰 · ") + R.contractText + "  " + (R.contractOk ? Loc.T("<color=#6fcf97>성공 +") + KNum.Fmt(R.bonus) + "</color>" : Loc.T("<color=#ee7766>실패 ") + R.contractProg + "/" + R.contractTarget + "</color>"), label);
            else if (R.cut > 0) Lbl(new Rect(bx, by + 44, bw, 20), Loc.T("<color=#ee7766>빚 상환으로 떼인 것 -") + KNum.Fmt(R.cut) + "</color>", label);
            }

            // 오른쪽 아래 — 다음 해금 (청구서를 갚으면 열리는 것)
            var RB = new Rect(cx + 10, 270, 410, 120);
            // 연체가 이어져 사실상 못 갚는 벽 — 다음 해금 대신 파산 안내 (설계상 첫 파산 자리. 구석 단추만으로는 모른다)
            bool stuck = sim.CanBankrupt && S.overdue && S.cash + sim.LoanCap < sim.BillAmount;   // 대출 한도로도 모자라다
            if (stuck)
            {
                Panel2(RB, Loc.T("<color=#ff9b8f>대출 한도로도 못 갚는다</color>"));
                Lbl(new Rect(RB.x + 16, RB.y + 30, RB.width - 32, 20), Loc.T("<size=13>파산하면 빚이 사라지고 <color=#ffdf95>신용 +") + S.creditPending * SweepSim.CreditK + "</color></size>", label);
                Lbl(new Rect(RB.x + 16, RB.y + 50, RB.width - 32, 20), Loc.T("<size=13>격납고에서 신용으로 새 배 · 영구 강화를 산다</size>"), label);
                var bb = new Rect(RB.x + 16, RB.y + 76, RB.width - 32, 34);
                if (Bt(bb, bankruptArmed ? Loc.T("<color=#ffb3a8>정말? 한 번 더 누르면 파산</color>") : Loc.T("<color=#ffb3a8>파산하고 새 회사로 ▸</color>"), btn))
                {
                    if (bankruptArmed) { sim.Bankrupt(); bankruptArmed = false; showResult = false; flow = 2; } else bankruptArmed = true;
                }
            }
            else Panel2(RB, sim.S.bill < SweepSim.Bills.Length ? Loc.T("다음 청구서") : null);
            if (!stuck && S.bill < SweepSim.Bills.Length)
            {
                float prog = Mathf.Clamp01((float)(S.cash / System.Math.Max(1, sim.BillAmount)));
                var sil = new Rect(RB.x + 18, RB.y + 34, 72, 72);
                GUI.color = new Color(0.1f, 0.12f, 0.16f); GUI.DrawTexture(sil, texDisc);
                GUI.color = prog >= 1 ? SweepGame.Green : new Color(0.3f, 0.34f, 0.42f); GUI.DrawTexture(sil, texRing); GUI.color = Color.white;
                title.fontSize = 22; Lbl(sil, prog >= 1 ? "<color=#6fcf97>!</color>" : "?", title);
                Lbl(new Rect(sil.xMax + 14, RB.y + 38, RB.width - 120, 40), Fit(SweepSim.Bills[S.bill].perk, 13, RB.width - 122, label), label);   // 🌐 영어는 길다
                GUI.DrawTexture(new Rect(sil.xMax + 14, RB.y + 84, RB.width - 124, 8), texBar);
                GUI.color = prog >= 1 ? SweepGame.Green : SweepGame.Amber; GUI.DrawTexture(new Rect(sil.xMax + 14, RB.y + 84, (RB.width - 124) * prog, 8), white); GUI.color = Color.white;
                Lbl(new Rect(sil.xMax + 14, RB.y + 94, RB.width - 124, 16), "<size=11>" + Mathf.RoundToInt(prog * 100) + "%</size>", small);
            }

            // 아래 버튼 셋 — [업그레이드] [청구서] [계속]
            float w = 250, gap = 16, x0 = cx - (w * 3 + gap * 2) / 2, yb = 420;
            if (Bt(new Rect(x0, yb, w, 66), Loc.T("업그레이드"), bigBtn)) flow = 3;
            BillButton(new Rect(x0 + w + gap, yb, w, 66));
            if (Bt(new Rect(x0 + (w + gap) * 2, yb, w, 66), Loc.T("조종실로 ▸"), bigBtn)) flow = 2;
            Lbl(new Rect(x0 + (w + gap) * 2, yb + 68, w, 16), "<size=10>Space</size>", center);
        }

        /// <summary>빨간 청구서 버튼 — 금액 · 기한. 돈이 되면 누르는 즉시 납부</summary>
        void BillButton(Rect r)
        {
            var S = sim.S;
            if (paidT > 0 && paidBill > 0)
            {
                GUI.color = new Color(0.1f, 0.3f, 0.18f); GUI.DrawTexture(r, white); GUI.color = Color.white; Frame(r, SweepGame.Green, 2);
                Lbl(new Rect(r.x, r.y + 8, r.width, 26), Loc.T("<size=20><color=#6fcf97>납부 완료!</color></size>"), center);
                Lbl(new Rect(r.x + 6, r.y + 36, r.width - 12, 22), "<size=11>" + SweepSim.Bills[paidBill - 1].perk + "</size>", center);
                return;
            }
            if (sim.M.cleanReady || S.bill >= SweepSim.Bills.Length)
            {
                GUI.color = new Color(0.1f, 0.3f, 0.18f); GUI.DrawTexture(r, white); GUI.color = Color.white;
                if (sim.M.cleanReady) { Lbl(r, Loc.T("<size=20><color=#6fcf97>빚 청산</color></size>"), center); return; }
                // 청구서는 끝 — 남은 빚을 갚아야 청산 출동
                Lbl(new Rect(r.x, r.y + 6, r.width, 30), Loc.T("<size=22><color=#ffdf95>빚 ") + KNum.Fmt(S.debt) + "</color></size>", center);
                Lbl(new Rect(r.x, r.y + 38, r.width, 22), "<size=13>" + (S.cash > 0 ? Loc.T("눌러서 갚기 — 다 갚으면 청산 출동") : Loc.T("다 갚으면 청산 출동")) + "</size>", center);
                if (Bt(r, GUIContent.none, GUIStyle.none)) Repay();
                return;
            }
            bool can = S.cash >= sim.BillAmount;
            double need = sim.BillAmount - S.cash;
            bool loanPay = !can && S.overdue && need <= sim.LoanCap;
            float pulse = can ? 0.5f + 0.5f * Mathf.Sin(Time.time * 5) : 0;
            GUI.color = can ? Color.Lerp(new Color(0.42f, 0.1f, 0.1f), new Color(0.6f, 0.16f, 0.14f), pulse) : new Color(0.3f, 0.08f, 0.09f);
            GUI.DrawTexture(r, white); GUI.color = Color.white;
            Frame(r, can ? Color.Lerp(SweepGame.Red, Color.white, pulse * 0.5f) : new Color(0.5f, 0.2f, 0.18f), 2);
            Lbl(new Rect(r.x, r.y + 4, r.width, 34), "<size=24><color=#ffdf95>" + KNum.Fmt(sim.BillAmount) + "</color></size>", center);
            string sub = S.overdue ? Loc.T("<color=#ffb3a8>오늘 납부일</color>") : S.billDue + Loc.T("판 남음");
            if (can) sub += Loc.T(" · <color=#ffffff>눌러서 갚기</color>");
            else if (loanPay) sub += Loc.T(" · <color=#ffffff>대출 ") + KNum.Fmt(need) + Loc.T(" 받아 갚기</color>");
            Lbl(new Rect(r.x, r.y + 38, r.width, 22), "<size=13>" + sub + "</size>", center);
            if (loanPay) Lbl(new Rect(r.x, r.yMax + 2, r.width, 16), Loc.T("<size=11>빚 +") + KNum.Fmt(need * SweepSim.LoanMult) + Loc.T(" (판 수입 30%씩 상환)</size>"), center);
            if (dueNag > 0) Lbl(new Rect(r.x - 40, r.y - 22, r.width + 80, 18), Loc.T("<color=#ff9b8f><size=12>납부일 — 먼저 갚거나 · 대출받거나 · 파산</size></color>"), center);
            if (Bt(r, GUIContent.none, GUIStyle.none)) { if (can) sim.PayBill(); else if (loanPay) RequestLoan(sim.BillAmount - S.cash, true); }
        }

        void FlowBottom()
        {
            float y = 512;
            if (Bt(new Rect(ox + 700, y, 246, 62), Loc.T("조종실로 ▸"), bigBtn)) GoFlow(2);
            Lbl(new Rect(ox + 700, y + 64, 246, 14), "<size=10>Space</size>", center);
        }

    }
}
