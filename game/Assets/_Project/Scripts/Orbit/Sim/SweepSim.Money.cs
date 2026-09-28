using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 💰 돈 — 대출 · 무한 궤도 · 증권 · 즉석 복권 · 로또 · 1면 · 부품 가게 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ── 대출 (연체 대신) — 언제든 받을 수 있다. 받은 돈 × 배수를 판 수입에서 조금씩 갚는다
        public static double LoanMult = 3;
        public void EnterEndless()
        {
            M.won = false; M.endless = true; if (M.depth < 1) M.depth = 1; if (M.bestDepth < M.depth) M.bestDepth = M.depth;
            S.orbit = MaxOrbit; RollContract(); Preview();
            AddNews(null, Loc.T("무한 궤도 개장 — 청소선, 끝없는 궤도로"), Loc.T("빚은 끝났다. 이제 누가 더 깊이 내려가는지만 남았다."));
        }
        void CheckClean() { if (!M.endless && S.bill >= Bills.Length && S.debt <= 0.5) {   // ∞ 무한 궤도엔 청산이 없다 (09-25 헤드리스 테스트가 잡음 — 다음 판이 청산 출동이 되어 엔딩이 또 떴다)
            S.debt = 0; M.cleanReady = true; } }   // 청구서도 빚도 다 갚아야 청산 출동
        public double LoanCap => M.cleanReady || S.bill >= Bills.Length - 1 ? 0 :   // 마지막 할부(완납)엔 대출이 안 된다 — 빚으로 빚을 끝내면 끝없이 갚기만 한다
             Math.Max(0, Math.Floor(BillAmount * (1 + 0.2 * Up(7)) - S.debt / LoanMult));   // 한도 = 지금 청구서 금액 − 남은 원금
        public bool TakeLoan(double amt)
        {
            amt = Math.Min(Math.Ceiling(amt), LoanCap);
            if (amt <= 0) return false;
            S.cash += amt; S.debt += amt * LoanMult; M.loans++; LogLoan(0, amt);
            return true;
        }
        void LogLoan(int kind, double amt)
        {
            if (S.loanLog == null) S.loanLog = new List<LoanRec>();
            S.loanLog.Add(new LoanRec { kind = kind, amt = amt, run = S.runs });
            if (S.loanLog.Count > 30) S.loanLog.RemoveAt(0);
        }
        // 📈 궤도 증권 — 규칙은 Market.cs. 여기선 돈 · 칸과 잇는다
        public Market Mk;
        public bool StockOpen => Lv("a_open") > 0;
        public double StockFee => Part("fee0") > 0 ? 0 : new[] { 0.01, 0.006, 0.003, 0 }[Math.Min(3, Lv("a_big"))];
        public double StockDiv => 0.0003 * Lv("a_big") + Part("div");
        public double LuckAt => new[] { 0, 0.6, 0.7, 0.8 }[Math.Min(3, Lv("a_ins"))];   // 🍀 행운의 부적 — 내 종목 나쁜 속보를 좋은 속보로 (09-24 사장님 「자동 매도 말고 오를 확률」)
        void MakeMarket()
        {
            if (S.market == null) S.market = new MarketState { seed = 1 + (int)(M.playSeconds * 7) % 100000 + M.company * 131 };
            Mk = new Market(S.market);
        }
        /// <summary>시장 시간 — 판 중이든 조종실이든 (계좌를 열었을 때만). 배당 · 자동 매도 돈은 바로 들어온다</summary>
        public void MarketTick(double dt)
        {
            if (!StockOpen || Mk == null) return;
            S.cash += Mk.Update(dt, Lv("a_auto") > 0 ? 0.0008 : 0, LuckAt, StockFee, StockDiv);
        }
        public void StockBuy(int i, double frac) { if (!StockOpen) return; double money = Math.Floor(S.cash * frac); if (money < 1) return; S.cash -= Mk.Buy(i, money, StockFee); }
        public void StockSell(int i, double frac) { if (!StockOpen) return; S.cash += Math.Floor(Mk.Sell(i, frac, StockFee)); }

        // 🎟 즉석 복권 · 🎱 궤도 로또 (09-23 사장님 「복권 두 가지 다」) — 게임 안 돈만. 평균 기대값 0.7 안팎 (복권답게 손해)
        readonly Random luck = new Random();                              // 게임 난수와 따로 — 봇 영향 없음
        // 🎟 09-27 사장님 「복권이 진짜 의미가 없다 · 디자인도 불편」 (시안 TxxPY6mLoewcLnW1vmNVBG) — 칸 셋 · 그림 여섯 · 판 벌이 기준 · 돈 말고도 부품 · 열쇠 · 강화
        public static readonly string[] ScratchSym = { Loc.T("고철"), Loc.T("위성"), Loc.T("금고"), Loc.T("열쇠"), Loc.T("부품"), Loc.T("황금") };
        static readonly double[] ScratchOdds = { 0.12, 0.10, 0.06, 0.04, 0.03, 0.005 };   // 셋이 같을 확률 (드문 것부터 뽑는다)
        public const double ScratchPairP = 0.25;                                   // 둘만 같으면 표값 돌려받기
        public double ScratchPrice => Math.Max(10, Math.Round(ShopBase * 0.15 / 10) * 10);   // 한 장 = 판 벌이의 15%
        public int ScratchMax => 3 + 2 * Lv("l_more");                              // 🎟 복권 단골 — 단계마다 +2 (09-27 +1 에서)
        public int ScratchLeft => (S.scratchRun == S.runs ? Math.Max(0, ScratchMax - S.scratchN) : ScratchMax) + S.freeTix;   // 공짜 표는 따로 더한다
        public double ScratchCost => S.freeTix > 0 || Lv("l_free") > 0 && (S.scratchRun != S.runs || S.scratchN < 2) ? 0 : ScratchPrice;   // 공짜 표 · 판마다 두 장 공짜 (09-27 한 장에서)
        int scratchWin = -1; double scratchPaid;                                 // 긁어서 다 보이면 받는다
        public string ScratchText;                                               // 받은 것 한 줄 (화면에 띄운다)
        /// <summary>한 장 산다 — 돌려주는 값 = 칸 셋의 그림 (null = 못 삼). win = 셋이 같은 그림 (-1 꽝 · -2 둘만 같음)</summary>
        public int[] ScratchBuy(out int win)
        {
            win = -1;
            if (ScratchLeft <= 0 || S.cash < ScratchCost) return null;
            double cost = ScratchCost;
            if (S.scratchRun != S.runs) { S.scratchRun = S.runs; S.scratchN = 0; }
            if (S.freeTix > 0) S.freeTix--; else S.scratchN++;                // 공짜 표부터 쓴다 — 판마다 장수는 안 깎인다
            S.cash -= cost; scratchPaid = cost > 0 ? cost : ScratchPrice;
            double u = luck.NextDouble() / (1 + 0.5 * Lv("l_luck")), acc = 0;    // 행운의 긁개 — 단계마다 +50% (09-27 +25% 에서)
            for (int k = ScratchOdds.Length - 1; k >= 0; k--) { acc += ScratchOdds[k]; if (u < acc) { win = k; break; } }
            int n = ScratchSym.Length;
            if (win >= 0) { scratchWin = win; return new[] { win, win, win }; }
            if (u < acc + ScratchPairP)
            {
                win = -2; scratchWin = -2;
                int a = luck.Next(n), b; do b = luck.Next(n); while (b == a);
                var g = new[] { a, a, b }; int j = luck.Next(3); (g[j], g[2]) = (g[2], g[j]); return g;
            }
            scratchWin = -1;
            var l = new List<int>(); while (l.Count < 3) { int c = luck.Next(n); if (!l.Contains(c)) l.Add(c); }
            return l.ToArray();
        }
        /// <summary>다 긁으면 받는다 — 돌려주는 값 = 받은 돈 (돈 아닌 것은 ScratchText 로)</summary>
        public double ScratchClaim()
        {
            int w = scratchWin; scratchWin = -1; ScratchText = null;
            double R0 = ShopBase, jack = Lv("l_jack") > 0 ? 3 : 1, got = 0;       // 잭팟 ×3 (09-27 ×2 에서)
            switch (w)
            {
                case -2: got = scratchPaid * (Lv("l_jack") > 0 ? 2 : 1); ScratchText = Lv("l_jack") > 0 ? Loc.T("둘이 같다 — 잭팟! 표값 두 배") : Loc.T("둘이 같다 — 표값 돌려받기"); break;
                case 0: got = R0 * 0.2 * jack; ScratchText = Loc.T("고철 셋 — 판 벌이 × 0.2"); break;
                case 1: S.nDmg += 30; ScratchText = Loc.T("위성 셋 — 다음 판 화력 +30%"); break;
                case 2: got = R0 * 1 * jack; ScratchText = Loc.T("금고 셋 — 판 벌이 × 1"); break;
                case 3: S.keys++; ScratchText = Loc.T("열쇠 셋 — 열쇠 1개"); break;
                case 4: ScratchText = Loc.T("부품 셋 — ") + FreePart() + Loc.T(" 공짜"); break;
                case 5: got = R0 * 10 * jack; ScratchText = Loc.T("황금 셋! 판 벌이 × 10 + ") + FreePart(); break;
                default: return 0;
            }
            got = Math.Round(got); S.cash += got;
            return got;
        }
        string FreePart()
        {   // 가게 진열대 부품 하나 (없으면 일반 · 희귀 중 하나) — 제 칸에 끼운다
            var pick = new List<int>();
            if (S.shop != null) foreach (var id in S.shop) if (id >= 0 && id < Parts.Defs.Length) pick.Add(id);
            bool shelf = pick.Count > 0;
            if (!shelf) for (int i = 0; i < Parts.Defs.Length; i++) if (Parts.Defs[i].rar <= 1 && (S.parts == null || Array.IndexOf(S.parts, i) < 0)) pick.Add(i);
            if (pick.Count == 0) return Loc.T("부품");
            int got = pick[luck.Next(pick.Count)];
            if (shelf) { int k = S.shop.IndexOf(got); S.shop.RemoveAt(k); if (k == S.shopSale) S.shopSale = -1; else if (k < S.shopSale) S.shopSale--; }
            if (S.parts == null || S.parts.Length != 5) S.parts = new[] { -1, -1, -1, -1, -1 };
            S.parts[Parts.Defs[got].slot] = got;
            return Parts.Defs[got].name;
        }

        public double LottoPrice => Math.Max(20, Math.Round(BillAmount * 0.03));
        public int LottoMine => S.lotto.Count;
        public bool LottoBuy(int a, int b, int c)
        {
            if (S.lotto.Count >= 3 || S.cash < LottoPrice || a == b || b == c || a == c) return false;
            S.cash -= LottoPrice;
            S.lotto.Add(new LottoTicket { a = a, b = b, c = c, round = S.lottoRound, price = LottoPrice });
            return true;
        }
        /// <summary>출동이 끝날 때 — 추첨 날이면 번호를 뽑고 당첨금을 준다</summary>
        void LottoDraw()
        {
            if (S.lotto.Count == 0) return;
            // 🎱 궤도 로또는 뺐다 (09-24) — 옛 저장에 남은 표는 값을 돌려준다
            foreach (var t in S.lotto) S.cash += t.price;
            S.lotto.Clear();
            return;
#pragma warning disable CS0162
            var pool = new List<int>(); for (int i = 1; i <= 12; i++) pool.Add(i);
            var d = new int[3]; for (int k = 0; k < 3; k++) { int j = luck.Next(pool.Count); d[k] = pool[j]; pool.RemoveAt(j); }
            Array.Sort(d);
            int best = 0; double win = 0;
            foreach (var t in S.lotto)
            {
                int hit = 0; foreach (var x in new[] { t.a, t.b, t.c }) if (x == d[0] || x == d[1] || x == d[2]) hit++;
                best = Math.Max(best, hit);
                win += t.price * (hit == 3 ? 60 : hit == 2 ? 2 : hit == 1 ? 0.3 : 0);
            }
            win = Math.Floor(win); S.cash += win;
            S.lottoLast = d; S.lottoLastRound = S.lottoRound; S.lottoLastHit = S.lotto.Count > 0 ? best : -1; S.lottoLastWin = win;
            AddNews(null, Loc.T("궤도 로또 ") + S.lottoRound + Loc.T("회 — ") + d[0] + " · " + d[1] + " · " + d[2], best == 3 ? Loc.T("세 개를 다 맞힌 사람이 나왔다! 주식회사 궤도 청소부라는 소문이다.") : Loc.T("이번 회 당첨 번호는 ") + d[0] + ", " + d[1] + ", " + d[2] + ".");
            S.lotto.Clear(); S.lottoRound++; S.lottoDrawAt = S.runs + 3;
        }

        public bool LoanAndPay()
        {
            if (S.cash >= BillAmount) return PayBill();
            double need = BillAmount - S.cash;
            if (need > LoanCap) return false;
            TakeLoan(need);
            return PayBill();
        }
        public bool RepayDebt()
        {
            double p = Math.Min(S.cash, S.debt);
            if (p <= 0) return false;
            S.cash -= p; S.debt -= p; LogLoan(2, p);
            CheckClean();
            return true;
        }
        // 🔩 부품 가게 — 진열 셋, 출동이 끝날 때마다 새로. 값은 지금 청구서에 맞춰 오른다
        public bool ShopOpen => Lv("e_shop") > 0;
        /// <summary>★ 1면 조작 — 고른 기사를 증권 속보로 낸다</summary>
        public void PickFront(int k)
        {
            int id = k == 0 ? S.front1 : S.front2; S.front1 = S.front2 = -1;
            if (id < 0 || Mk == null) return;
            S.frontPick = id;                                                 // 📰 내일 1면 확정 — 다음 출동을 시작할 때 발행 (시장은 출동 중에만 흐른다)
        }
        void PublishFront()
        {
            int id = S.frontPick; S.frontPick = -1;
            if (id < 0 || id >= Market.NewsBook.Length || Mk == null) return;
            var nd = Market.NewsBook[id]; string h = nd.head.Replace(Loc.T("[소문] "), "");
            Mk.Publish(Loc.T("오늘 1면 — ") + h, nd.body, nd.up, nd.down, nd.size * 1.5f, false);   // 조작한 1면은 세게 (09-24 사장님 7번 「안 되는 것 같다」)
            AddNews(null, Loc.T("오늘 1면 — ") + h, Loc.T("궤도일보 1면. (편집장은 청소선에서 온 제보라고만 했다)"));
        }
        // 🔩 가게 v2 (09-24 사장님 24번 「너무 비싸기만 하다 · 판마다 바뀌고 · 돈으로 바꾸고 · 가격 다양하게」)
        public double ShopBase => Math.Max(80, Math.Round((S.runAvg > 0 ? S.runAvg : BillAmount * 0.15) / 10) * 10);   // 💰 09-26 사장님 「전설이 너무 싸」 — 청구서가 아니라 한 판 벌이에 묶는다
        public const int Cons0 = 200;
        public static readonly string[] ConsName = { Loc.T("연료 캔"), Loc.T("복권 묶음"), Loc.T("과부하 탄창"), Loc.T("감정 할인권") };
        public static readonly string[] ConsDesc = { Loc.T("다음 판 연료 +10초"), Loc.T("공짜 즉석 복권 +3장"), Loc.T("다음 판 화력 +20%"), Loc.T("다음 판 모든 값 +15%") };
        static readonly double[] ConsPrice = { 0.12, 0.1, 0.22, 0.25 };   // 09-26 밤 절반으로
        public static readonly string[] ConsHelp = { Loc.T("다음 출동 한 판만 연료가 10초 늘어난다. 끝나면 사라진다"), Loc.T("공짜 즉석 복권 세 장을 받는다. 조종실 복권기에서 긁는다 — 표값이 안 든다"), Loc.T("다음 출동 한 판 동안 모든 무기 화력이 20% 세진다"), Loc.T("다음 출동 한 판 동안 부순 것 값이 전부 15% 더 붙는다") };   // 📖 가게 카드 자세히
        public static bool IsCons(int id) => id >= Cons0 && id < Cons0 + ConsName.Length;
        public double PartPrice(int id)
        {
            double b = ShopBase, p = id == Parts.Key ? b * 2.5 : IsCons(id) ? b * ConsPrice[id - Cons0] : b * Parts.RarPrice[Parts.Defs[id].rar];
            if (!IsCons(id) && id != Parts.Key) p = Math.Max(Parts.RarFloor[Parts.Defs[id].rar], p);   // 👑 등급 바닥 — 희귀 천왕성 · 영웅 카이퍼 · 전설 오르트쯤 열린다 (Parts.RarFloor)
            double jit = 1 + 0.15 * Math.Sin(id * 12.9898 + S.runs * 78.233);          // 판마다 조금씩 다른 값
            return Math.Max(10, Math.Round(p * jit * (1 - 0.05 * Up(8)) / 10) * 10);
        }
        public double ShelfPrice(int k) => S.shop == null || k < 0 || k >= S.shop.Count ? 0 : Math.Max(10, Math.Round(PartPrice(S.shop[k]) * (k == S.shopSale ? 0.5 : 1) / 10) * 10);
        public double RerollPrice => S.freeRoll ? 0 : Math.Round(ShopBase * 0.15 * Math.Pow(2, S.rolls) / 10) * 10;   // 누를수록 두 배 · 출동하면 처음부터 (09-26)
        public void RollShop()
        {
            if (S.shop == null) S.shop = new List<int>();
            S.shop.Clear();
            var used = new HashSet<int>(S.parts ?? new int[0]);
            bool key = false;
            for (int c = 0; c < 2; c++) { int ci; do ci = Cons0 + rng.Next(ConsName.Length); while (S.shop.Contains(ci)); S.shop.Add(ci); }   // 🧃 소모품 둘 (싸다)
            for (int n = 0; n < 4; n++)
            {
                if (!key && Rnd() < 0.18) { S.shop.Add(Parts.Key); key = true; continue; }
                for (int t = 0; t < 30; t++)
                {
                    double u = Rnd(); int rar = u < 0.62 ? 0 : u < 0.87 ? 1 : u < 0.96 ? 2 : 3;   // 일반 · 희귀 · 영웅 · 전설 — 처음부터 다 나온다, 막는 건 값 (09-26 사장님 「진열에는 나와도 됨」)
                    int id = rng.Next(Parts.Defs.Length);
                    if (Parts.Defs[id].rar != rar || used.Contains(id) || S.shop.Contains(id)) continue;
                    S.shop.Add(id); break;
                }
            }
            S.shopSale = S.shop.Count > 0 ? rng.Next(S.shop.Count) : -1;         // 오늘의 반값 한 칸
        }
        public bool BuyPart(int k)
        {
            if (!R.over || !ShopOpen || S.shop == null || k < 0 || k >= S.shop.Count) return false;
            int id = S.shop[k]; double p = ShelfPrice(k);
            if (S.cash < p) return false;
            S.cash -= p; S.shop.RemoveAt(k);
            if (k == S.shopSale) S.shopSale = -1; else if (k < S.shopSale) S.shopSale--;
            if (id == Parts.Key) { S.keys++; return true; }
            if (IsCons(id))
            {
                switch (id - Cons0)
                {
                    case 0: S.nFuel += 10; break;
                    case 1: S.freeTix += 3; break;                                           // 🎟 공짜 표 셋 (09-27 — 전엔 살 수 있는 장수만 늘었다)
                    case 2: S.nDmg += 20; break;
                    case 3: S.nVal += 15; break;
                }
                return true;
            }
            if (S.parts == null || S.parts.Length != 5) S.parts = new[] { -1, -1, -1, -1, -1 };
            S.parts[Parts.Defs[id].slot] = id;
            return true;
        }
        public bool RerollShop() { if (!R.over || !ShopOpen || S.cash < RerollPrice) return false; S.cash -= RerollPrice; if (!S.freeRoll) S.rolls++; S.freeRoll = false; RollShop(); return true; }
        public int TotalLv { get { int n = 0; foreach (var l in S.lv) n += l; return n; } }
        public double Widen => 1 + 0.1 * Lv("o_wide");      // 🔴 정비소에서 산다 (사장님 09-23: "맵 크기도 여기서 늘리게")
        public double Bo => Orbits[S.orbit].bi + (Orbits[S.orbit].bo - Orbits[S.orbit].bi) * Widen;
        public double BillAmount => S.bill < Bills.Length ? (S.billAmount >= 0 ? S.billAmount : Math.Round(Bills[S.bill].m * BillMul * BillK[S.bill])) * (Lv("k_eco") > 0 ? 1.1 : 1) : 0;
        public static double[] BillK = { 0.8, 0.45, 2, 1, 1.2, 1.3, 1.4, 5, 150, 900, 4000, 30000 };   // 📈 09-26 사장님 「모든 청구서가 봇에게 간신히」 — 청구서마다 더 곱하는 수 (봇 「남긴 판」이 0~1 이 되게 맞춤)
        public static double BotEarn = 1;                                    // 🤖 봇 전용 — 사장님만큼 연쇄를 못 터뜨리니 벌이를 곱해 준다 (게임에선 늘 1)
        public const double BillMul = 5.1;                                   // 🧾 09-26 사장님 기록(첫 청구서 5판에 400 · 판당 150) 기준 — 봇의 약 3배로 올림                                   // 🧾 09-26 사장님 「청구서 아직 쉬움」 — 청구서 전체 배수 (봇으로 맞춤)
        void ApplyPerm() { foreach (var kid in M.perm) if (NodeIx.TryGetValue(kid, out int ki) && S.lv[ki] < Nodes[ki].max) S.lv[ki] = Nodes[ki].max; if (S.lv[NodeIx["w_hub"]] < 1) S.lv[NodeIx["w_hub"]] = 1; }   // ⚔ 무기고는 사지 않는다 — 처음부터 열려 있다 (09-26 사장님 「왜 필요한지 모르겠음」)
        public int BankruptKeys => 2 + S.bill / 2;                                // 청구서 7장째 = 열쇠 5
        public bool CanBankrupt => !M.cleanReady && S.bill < Bills.Length && (S.bill >= 3 || S.overdue && S.bill >= 1);
        public int CareerCost(int i) => M.career[i] < Careers[i].cost.Length ? Careers[i].cost[M.career[i]] : -1;
        public int Unread { get { int n = 0; foreach (var it in M.news) if (!it.read) n++; return n; } }
        public Contract? CurContract => ContractsOn && S.contract >= 0 && S.contract < Contracts.Length ? Contracts[S.contract] : (Contract?)null;
        public int JunkTarget
        {
            get
            {
                var o = Orbits[S.orbit];
                int n = new[] { 90, 110, 130, 150, 175, 200, 230, 260, 260 }[Math.Min(8, S.bill)];
                double area = (Bo * Bo - o.bi * o.bi) / (o.bo * o.bo - o.bi * o.bi);       // 넓어진 만큼 더 많이
                return (int)(Math.Max(o.nMin, Math.Min(o.nMax, n)) * area);
            }
        }

    }
}
