using System;
using System.Collections.Generic;
using System.Linq;
using SalvageRun.Orbit.Sim;

// 궤도 청소부 rev17 — 2시간 흐름을 봇으로 잰다 (wiki/rev17-detail.md §6 · §7).
//
// 🔴 재는 것은 「끝까지 가는가, 몇 분에 무엇이 오는가, 수입이 어디서 나는가」뿐이다.
//    재미·난이도는 안 잰다 — 그건 사장님이 해보고 판단하신다.
//
// 사용법:  dotnet run -c Release                 보통 사람 셋 (씨앗 셋)
//          dotnet run -c Release -- 7 verbose    씨앗 7 하나, 판마다 한 줄
static class Program
{
    const double ShopSec = 22;       // 정비소에서 보내는 시간 (판마다)
    const double Dt = 0.05;

    static void Main(string[] args)
    {
        SweepSim.BotEarn = double.TryParse(Environment.GetEnvironmentVariable("EARN"), out double be) ? be : 3;   // 🤖 사장님은 연쇄로 봇의 약 3배를 번다 (09-26 첫 청구서 기록)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (double.TryParse(Environment.GetEnvironmentVariable("LOANMULT"), out double lm)) SweepSim.LoanMult = lm;
        if (args.Length > 0 && args[0] == "zones") { for (int z = 0; z < 6; z++) { double sum = 0; var ids = new List<string>(); for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Zone[i] == z && SweepSim.ZoneNeed(i)) { sum += SweepSim.Nodes[i].first; ids.Add(SweepSim.Nodes[i].id + ":" + SweepSim.Nodes[i].first); } Console.WriteLine($"구역 {z} {SweepSim.ZoneName[z]} 칸 {ids.Count} 합 {sum:0}"); Console.WriteLine("   " + string.Join(" ", ids)); } return; }
        if (args.Length > 0 && args[0] == "econ") { Econ(args.Length > 1 && int.TryParse(args[1], out int en) ? en : 10); return; }   // 💰 경제 진단 — 돈이 남는지 · 칸이 싼지 · 판 길이
        if (args.Length > 0 && args[0] == "bal") { Balance(args.Length > 1 && int.TryParse(args[1], out int bn) ? bn : 20); return; }   // 📊 밸런스 보고서
        if (args.Length > 0 && args[0] == "test") { Environment.ExitCode = Tests.RunAll(args.Length > 1 && int.TryParse(args[1], out int tn) ? tn : 12); return; }   // 🧪 헤드리스 테스트
        var seeds = args.Length > 0 && int.TryParse(args[0], out int one) ? new[] { one } : new[] { 3, 7, 11 };
        bool verbose = args.Contains("verbose");
        foreach (var s in seeds) Run(s, verbose);
    }

    public class Result { public bool won; public double minutes; public int bankrupt, bill; public double[] billAt = new double[13], planetAt = new double[9]; public List<double> bankAt = new List<double>(); public List<int> bankBill = new List<int>(); public double earn50, tree; public List<int>[] slack = Enumerable.Range(0, 13).Select(_ => new List<int>()).ToArray(), win = Enumerable.Range(0, 13).Select(_ => new List<int>()).ToArray(); }
    public static double TreePct(SweepSim sim) { double h = 0, m = 0; for (int i = 0; i < SweepSim.NodeCount; i++) { var nd = SweepSim.Nodes[i]; if (nd.id.StartsWith("p_") || nd.id == "e_quest") continue; h += Math.Min(sim.S.lv[i], nd.max); m += nd.max; } return m > 0 ? h / m * 100 : 0; }
    static bool quiet;
    public static Result RunQuiet(int seed) { quiet = true; try { return Run(seed, false); } finally { quiet = false; } }

    static Result Run(int seed, bool verbose)
    {
        var sim = new SweepSim(null, null, seed);
        if (int.TryParse(Environment.GetEnvironmentVariable("SHIP"), out int forceShip)) { sim.M.shipsOwned = 7; sim.M.ship = forceShip; }   // 🚀 배 비교용 (SHIP=0 빔 · 1 산탄 · 2 작살)
        var rng = new Random(seed * 31 + 1);
        double ax = 600, ay = 360, tx = 600, ty = 360, retarget = 0, shopClock = 0;
        bool hold = false;
        var log = new List<string>();
        var segEarn = new double[9]; var segRuns = new int[9]; var segSplit = new double[9, 3];
        double Min() => (sim.M.playSeconds + shopClock) / 60;

        var rec = new Result(); for (int k = 0; k < 13; k++) rec.billAt[k] = -1; for (int k = 0; k < 9; k++) rec.planetAt[k] = -1;
        void Mark() { if (rec.billAt[sim.S.bill] < 0) for (int k = 0; k <= sim.S.bill; k++) if (rec.billAt[k] < 0) rec.billAt[k] = Min(); for (int pi = 0; pi < 9; pi++) if (rec.planetAt[pi] < 0 && sim.Open(pi)) rec.planetAt[pi] = Min(); }
        for (int guard = 0; guard < 400 && !sim.M.won; guard++)
        {
            Mark();
            // ── 정비소
            if (sim.M.careerOpen)
            {
                bool bought = true;
                while (bought) { bought = false; int best = -1, bc = 999; for (int i = 0; i < SweepSim.UpCount; i++) { int c = sim.UpCost(i); if (c > 0 && c <= sim.M.credit && c < bc) { bc = c; best = i; } } if (best >= 0) { sim.BuyUp(best); bought = true; } }   // 🛠 격납고 — 싼 영구 강화부터
                sim.CloseCareer();
            }
            if (!sim.M.cleanReady)
            {
                int dueWas = sim.S.billDue, billWas = sim.S.bill; bool overWas = sim.S.overdue;
                if (sim.PayBill()) { rec.slack[billWas].Add(overWas ? -1 : dueWas); rec.win[billWas].Add(overWas ? -1 : dueWas); }
                if (sim.S.bill != billWas) log.Add($"{Min(),6:0.0}분  {sim.M.company}대  청구서 {sim.S.bill} 갚음  (출동 {sim.S.runs})");
                // 납부일 — 모자라면 대출받아 갚는다. 한도가 모자라면 파산
                if (sim.S.overdue && sim.S.cash < sim.BillAmount)
                {
                    double need = sim.BillAmount - sim.S.cash;
                    int lb = sim.S.bill;
                    if (sim.LoanAndPay()) { rec.slack[lb].Add(-2); rec.win[lb].Add(-2); }
                    if (sim.S.bill != lb) log.Add($"{Min(),6:0.0}분  {sim.M.company}대  🏦 대출 {need:0} 받아 청구서 {sim.S.bill} 갚음 · 빚 {sim.S.debt:0}");
                }
                if (sim.CanBankrupt && sim.S.overdue)
                {
                    log.Add($"{Min(),6:0.0}분  {sim.M.company}대  💥 파산 (청구서 {sim.S.bill} · 출동 {sim.S.runs} · 신용 +{sim.S.creditPending})");
                    rec.bankAt.Add(Min()); rec.bankBill.Add(sim.S.bill); rec.slack[sim.S.bill].Add(-9); foreach (var w in rec.win) w.Clear();
                    sim.Bankrupt();
                    continue;
                }
                if (sim.S.bill >= SweepSim.Bills.Length && sim.RepayDebt()) log.Add($"{Min(),6:0.0}분  {sim.M.company}대  🏦 빚 갚는 중 · 남은 빚 {sim.S.debt:0}");
                // 행성 허가증 — 기한이 3판 넘게 남았거나, 사고도 청구서 몫이 남으면 산다
                for (int pi = 1; pi < SweepSim.Orbits.Length; pi++)
                    if (sim.OnSale(pi) && !sim.S.overdue && sim.S.cash >= SweepSim.PermitCost(pi) && (sim.S.billDue >= 2 || sim.S.cash - SweepSim.PermitCost(pi) >= sim.BillAmount) && sim.BuyPermit(pi))
                        log.Add($"{Min(),6:0.0}분  {sim.M.company}대  🪐 {SweepSim.Orbits[pi].name} 허가증 ({SweepSim.PermitCost(pi):0})");
                sim.SetOrbit(sim.MaxOrbit);
                // 사기 — 청구서 몫은 남겨 두고 싼 것부터
                // 기한이 한 판 남았거나 연체 중이면 모은다 (사람도 그렇게 한다)
                double reserve = sim.S.overdue || sim.S.billDue <= 1 ? double.MaxValue : Math.Min(sim.BillAmount, sim.S.cash * 0.35);   // 첫 청구서 전엔 아끼지 않는다 (자동 집게부터)
                // 🪐 사람처럼 — 「지구 구역 3/6 — 다 찍으면 달 항로」 안내를 따라 구역 칸부터 채운다 (09-25 밸런스: 가장 싼 것만 사면 빔 위력만 올리다 달이 22분)
                for (int loop = 0; loop < 30 && sim.ZoneLeft(sim.ZoneOpen) > 0; loop++)
                {
                    int zb = -1; double zc = double.MaxValue;
                    for (int i = 0; i < SweepSim.NodeCount; i++) if (SweepSim.Zone[i] == sim.ZoneOpen && SweepSim.ZoneNeed(i) && sim.S.lv[i] == 0 && sim.State(i) == NodeSt.Can && sim.TileCost(i) < zc) { zc = sim.TileCost(i); zb = i; }
                    if (zb < 0 || reserve == double.MaxValue || sim.S.cash - zc < reserve) break;
                    sim.Buy(zb);
                }
                for (int loop = 0; loop < 60; loop++)
                {
                    int best = -1; double bc = double.MaxValue;
                    for (int i = 0; i < SweepSim.NodeCount; i++) if (!SweepSim.Nodes[i].id.StartsWith("p_") && SweepSim.Nodes[i].id != "e_shop" && sim.State(i) == NodeSt.Can && sim.TileCost(i) < bc) { bc = sim.TileCost(i); best = i; }
                    if (best < 0 || reserve == double.MaxValue || sim.S.cash - bc < reserve) break;
                    sim.Buy(best);
                }
            }
            if (econOn)
            {   // 💰 가게를 나설 때 — 열린 칸(항로 제외)이 얼마나 남았고 얼마인가
                int cn = 0; double cmin = double.MaxValue, csum = 0, own = 0;
                for (int i = 0; i < SweepSim.NodeCount; i++)
                {
                    if (sim.S.lv[i] > 0) own++;
                    if (SweepSim.Nodes[i].id.StartsWith("p_") || SweepSim.Nodes[i].id == "e_shop" || sim.State(i) != NodeSt.Can) continue;
                    double c = sim.TileCost(i); cn++; csum += c; if (c < cmin) cmin = c;
                }
                eb = Math.Min(11, sim.S.bill); eCanMin = cn > 0 ? cmin : -1; eCanSum = csum;
                E[eb, 0] += 1; E[eb, 1] += cn == 0 ? 1 : 0; E[eb, 2] += own / SweepSim.NodeCount;
            }
            shopClock += ShopSec;
            if (Environment.GetEnvironmentVariable("DBG") == "2" && Min() > 90) Console.WriteLine($"  {Min(),5:0.0}분 돈 {sim.S.cash,12:0} 청구서 {sim.S.bill}({sim.BillAmount:0}) 기한 {sim.S.billDue}{(sim.S.overdue ? " 연체" : "")} 구역 {sim.ZoneOpen} 남음 {sim.ZoneLeft(sim.ZoneOpen)} 해왕성 {sim.State(Array.FindIndex(SweepSim.Nodes, x => x.id == "p_nep"))} 빚 {sim.S.debt:0} 궤도 {SweepSim.Orbits[sim.S.orbit].name}");

            // ── 출동
            int seg = Math.Min(8, sim.S.bill + 1);
            sim.StartRun();
            var R = sim.R;
            bool gateRun = sim.GateJunk != null && !sim.S.overdue && sim.S.billDue >= 3 && rng.NextDouble() < 0.35;   // 🛰 청구서 기한이 넉넉할 때 가끔 관문만 노린다   // 🛰 사람처럼 — 트리에서 한도까지 다 샀고 청구서가 급하지 않으면 관문만 노린다
            long h0 = SweepSim.DbgHits, o0 = SweepSim.DbgOneShot, k0 = SweepSim.DbgKills;
            long ticks = 0;
            while (!R.over)
            {
                if (++ticks > 20000) { if (!quiet) Console.WriteLine($"  ⚠ 판이 안 끝난다: 연료 {R.fuel:0.0} 붙잡음 {R.holding} 연쇄대기 {R.pend.Count} 잔해 {R.junk.Count}"); break; }
                retarget -= Dt;
                var gj = gateRun ? sim.GateJunk : null;
                if (gj != null) { tx = gj.x; ty = gj.y; double kg = Math.Min(1, Dt * 8); ax += (tx - ax) * kg; ay += (ty - ay) * kg; }
                else if (!hold && sim.ClawR <= 0)
                {
                    // 범위가 없을 땐 가까운 것 하나에 커서를 올려 둔다 (집게는 저절로 친다)
                    if (retarget <= 0) { retarget = 0.4; Nearest(sim, ax, ay, ref tx, ref ty); }
                    double kk = Math.Min(1, Dt * 12); ax += (tx - ax) * kk; ay += (ty - ay) * kk;
                }
                else
                {
                    if (!hold && retarget <= 0) { retarget = 1.5; Densest(sim, rng, ref tx, ref ty); }
                    double k = Math.Min(1, Dt * (hold ? 1.2 : 3)); ax += (tx - ax) * k; ay += (ty - ay) * k;
                }
                // 블랙홀 스킬 — 칸이 있으면 가끔 빽빽한 곳에 연다 (3초 뒤 저절로 터진다)
                bool cast = false;
                if (!R.holding && R.shots > 0 && R.t > 3 && R.fuel > 4 && rng.NextDouble() < Dt / 2.5) { Densest(sim, rng, ref tx, ref ty); ax = tx; ay = ty; cast = true; }
                sim.Tick(Dt, ax, ay, true, cast);
                while (sim.Events.Count > 0) sim.Events.Dequeue();
            }
            hold = false;
            if (Environment.GetEnvironmentVariable("DBG") == "gate" && sim.S.bill < 4) Console.WriteLine($"  {Min(),5:0.0}분 청구서 {sim.S.bill} 관문판 {gateRun} 관문 {(sim.GateJunk != null ? sim.GateJunk.hp + "/" + sim.GateJunk.max : "-")} max {sim.S.gateMax:0} 돈 {sim.S.cash:0} 청구 {sim.BillAmount:0} 살칸없음 {CappedOut(sim)} 화력 {sim.Pow:0.0}");
            if (R.clean) { log.Add($"{Min(),6:0.0}분  ✨ 청산 출동 끝 — 빚 청산"); break; }
            segEarn[seg] += R.Earned; segRuns[seg]++;
            if (econOn && R.Earned > 0)
            {
                E[eb, 3] += 1; E[eb, 4] += R.t; E[eb, 5] += R.Earned;
                H[eb, 0] += SweepSim.DbgHits - h0; H[eb, 1] += SweepSim.DbgOneShot - o0; H[eb, 2] += SweepSim.DbgKills - k0;
                if (eCanMin > 0) { E[eb, 6] += eCanMin / R.Earned; E[eb, 7] += 1; }
                E[eb, 8] += eCanSum / R.Earned;
            }
            segSplit[seg, 0] += R.earnClaw; segSplit[seg, 1] += R.earnDrone; segSplit[seg, 2] += R.earnBlast;
            if (!sim.M.won) rec.tree = TreePct(sim);
            if (Environment.GetEnvironmentVariable("DBG") == "gmax") Console.WriteLine($"  {Min(),5:0.0}분 청구서 {sim.S.bill} 관문 {sim.ZoneOpen} max {sim.S.gateMax:0} 판피해 {sim.GateShotDmg * sim.FuelMax / sim.Gap:0} 트리 {TreePct(sim):0}% 판수입 {sim.S.runAvg:0}");
            if (verbose) Console.WriteLine($"{Min(),6:0.0}분    출동 {sim.S.runs,2}  구간 {seg}  {SweepSim.Orbits[sim.S.orbit].name}  +{R.Earned,8:0}  연쇄 {R.chainBest,3}  압축 {R.packBest,3}  돈 {sim.S.cash,8:0}  청구서 {sim.BillAmount,7:0}{(sim.S.overdue ? " 연체" : " 기한 " + sim.S.billDue)}");
        }

        Mark();
        var res = rec; res.won = sim.M.won; res.minutes = Min(); res.bankrupt = sim.M.bankrupt; res.bill = sim.S.bill;
        if (quiet) return res;
        Console.WriteLine($"── 씨앗 {seed} ── 끝 {Min():0}분 · 출동 {sim.M.totalRuns} · 파산 {sim.M.bankrupt} · 최대 연쇄 {sim.M.bestChain} · 최대 압축 {sim.M.bestPack} · 특종 {sim.M.scoops}");
        foreach (var l in log) Console.WriteLine(l);
        Console.WriteLine("   구간   판   판당 수입    집게/드론/폭발");
        for (int s = 1; s <= 8; s++)
        {
            if (segRuns[s] == 0) continue;
            double tot = segEarn[s];
            Console.WriteLine($"   {s}     {segRuns[s],3}  {tot / segRuns[s],10:0}    {Pct(segSplit[s, 0], tot)}/{Pct(segSplit[s, 1], tot)}/{Pct(segSplit[s, 2], tot)}");
        }
        Console.WriteLine();
        return res;
    }

    static bool CappedOut(SweepSim sim)
    {   // 한도 안에서 더 살 칸이 없다 (항로 · 가게 · 열쇠 칸 제외)
        for (int i = 0; i < SweepSim.NodeCount; i++) { var id = SweepSim.Nodes[i].id; if (id.StartsWith("p_") || id == "e_shop") continue; var st = sim.State(i); if (st == NodeSt.Can) return false; }   // 지금 살 수 있는 칸이 없다
        return true;
    }
    // 💰 경제 진단 (09-26 사장님 「돈이 전혀 안 모자라 · 스킬이 너무 싸 · 한 판이 너무 길어」)
    static bool econOn; static int eb; static double eCanMin, eCanSum;
    static double[,] E = new double[12, 9], H = new double[12, 3];   // H: 맞은 수 · 한 방 · 부서진 수   // 0 가게 수 · 1 살 게 없음 · 2 트리 보유율 · 3 판 수 · 4 판 길이 합 · 5 수입 합 · 6 싼 칸/판 수입 합 · 7 그 수 · 8 열린 칸 전부/판 수입 합
    static void Econ(int n)
    {
        econOn = true; E = new double[12, 9]; H = new double[12, 3];
        for (int i = 1; i <= n; i++) RunQuiet(i * 7 + 1);
        econOn = false;
        Console.WriteLine($"💰 경제 — 씨앗 {n} · 청구서 구간별 평균 (판 길이 = 시뮬 초)");
        Console.WriteLine("  구간 청구서             판수  판길이   판당수입   싼칸=판  열린칸전부=판  살게없음  트리보유  한방비율  부서짐당맞음");
        for (int b = 0; b < 12; b++)
        {
            if (E[b, 3] == 0) continue;
            double shops = Math.Max(1, E[b, 0]), runs = E[b, 3];
            Console.WriteLine($"  {b + 1,2}  {SweepSim.Bills[b].t,-12} {runs / n,5:0.0}  {E[b, 4] / runs,5:0}초  {E[b, 5] / runs,10:0}  {(E[b, 7] > 0 ? E[b, 6] / E[b, 7] : -1),6:0.00}  {E[b, 8] / runs,10:0.0}  {E[b, 1] / shops * 100,6:0}%  {E[b, 2] / shops * 100,6:0}%  {(H[b, 0] > 0 ? H[b, 1] / Math.Max(1, H[b, 2]) * 100 : 0),6:0}%  {(H[b, 2] > 0 ? H[b, 0] / H[b, 2] : 0),8:0.00}");
        }
    }

    // 📊 씨앗 n개 — 청구서 k장 첫 도달 · 행성 첫 도달 · 파산 시각 · 끝, 가운데값(과 10% · 90%)
    static void Balance(int n)
    {
        var rs = new List<Result>(); for (int i = 1; i <= n; i++) rs.Add(RunQuiet(i * 7 + 1));
        string Q(IEnumerable<double> xs) { var a = xs.Where(x => x >= 0).OrderBy(x => x).ToList(); if (a.Count == 0) return "   -"; return $"{a[a.Count / 2],5:0}분 ({a[a.Count / 10],3:0}~{a[a.Count * 9 / 10],3:0}) {(a.Count < n ? a.Count + "/" + n : "")}"; }
        Console.WriteLine($"📊 밸런스 — 씨앗 {n}   (가운데값 · 10%~90%)");
        { double tw = SweepSim.DbgWDmg.Sum(); string[] wn = { "주무기·드론·폭발", "레이저", "번개", "진공", "기뢰", "냉동", "분열탄", "자석", "레일건" }; Console.WriteLine("  🔫 피해 몫  " + string.Join("  ", Enumerable.Range(0, 9).Select(i => wn[i] + " " + (SweepSim.DbgWDmg[i] / Math.Max(1, tw) * 100).ToString("0.0") + "%"))); }
        Console.WriteLine("  끝(빚 청산)      " + Q(rs.Select(r => r.won ? r.minutes : -1)));
        Console.WriteLine("  끝낼 때 트리 %     " + Q(rs.Where(r => r.won).Select(r => r.tree)).Replace("분", "%"));
        Console.WriteLine($"  파산 수 평균     {rs.Average(r => r.bankrupt):0.0}  · 첫 파산 " + Q(rs.Select(r => r.bankAt.Count > 0 ? r.bankAt[0] : -1)) + " · 둘째 " + Q(rs.Select(r => r.bankAt.Count > 1 ? r.bankAt[1] : -1)));
        for (int c = 0; c < 3; c++) { var bb = rs.Where(r => r.bankBill.Count > c).Select(r => r.bankBill[c]).OrderBy(x => x).ToList(); if (bb.Count > 0) Console.WriteLine($"  {c + 1}대 파산 — 갚은 청구서 가운데 {bb[bb.Count / 2]}장 ({bb[0]}~{bb[bb.Count - 1]}) · {bb.Count}/{n}판"); }
        for (int k = 0; k < 12; k++) { var sl = rs.SelectMany(r => r.slack[k]).ToList(); int tot = sl.Count; if (tot == 0) continue; var paid = sl.Where(x => x >= 0).OrderBy(x => x).ToList(); Console.WriteLine($"  🧾 {k + 1,2}장 {SweepSim.Bills[k].t,-10} 기한 {SweepSim.Bills[k].due}  남긴 판(가운데) {(paid.Count > 0 ? paid[paid.Count / 2].ToString() : "-"),2}  제때 {paid.Count,3}  연체후 {sl.Count(x => x == -1),3}  대출 {sl.Count(x => x == -2),3}  파산 {sl.Count(x => x == -9),3}   ║ 완납한 회사: 남긴 판 {string.Join(",", rs.Where(r => r.won).Select(r => r.win[k].Count > 0 ? r.win[k].Max() : -3).OrderBy(x => x).Select(x => x == -2 ? "빚" : x == -1 ? "늦" : x == -3 ? "·" : x.ToString()))}"); }
        for (int k = 1; k <= 12; k++) Console.WriteLine($"  청구서 {k,2}장 갚음 {Q(rs.Select(r => r.billAt[k]))}   {SweepSim.Bills[k - 1].t} {Math.Round(SweepSim.Bills[k - 1].m * SweepSim.BillMul * SweepSim.BillK[k - 1]):0}");
        foreach (int pi in SweepSim.OrbitOrder) if (pi > 0) Console.WriteLine($"  🪐 {SweepSim.Orbits[pi].name,-6} {Q(rs.Select(r => r.planetAt[pi]))}   관문 {SweepSim.GateNames[Math.Max(0, Array.IndexOf(SweepSim.OrbitOrder, pi) - 1)]}");
    }

    static string Pct(double a, double t) => t <= 0 ? "-" : Math.Round(a / t * 100).ToString();

    static void Nearest(SweepSim sim, double ax, double ay, ref double tx, ref double ty)
    {
        double bd = double.MaxValue;
        foreach (var o in sim.R.junk)
        {
            if (o.dead || o.fade < 0.5 || o.sig == SweepSim.GateSig) continue;
            double d = (o.x - ax) * (o.x - ax) + (o.y - ay) * (o.y - ay) - SweepSim.Types[o.k].val * 400;
            if (d < bd) { bd = d; tx = o.x; ty = o.y; }
        }
    }

    static void Densest(SweepSim sim, Random rng, ref double tx, ref double ty)
    {
        var list = sim.R.junk; if (list.Count == 0) return;
        double bs = -1;
        for (int i = 0; i < 30; i++)
        {
            var c = list[rng.Next(list.Count)]; if (c.dead || c.sig == SweepSim.GateSig) continue;
            double s = 0;
            foreach (var o in list) if (!o.dead && Math.Abs(o.x - c.x) < 60 && Math.Abs(o.y - c.y) < 60) s += 1 + SweepSim.Types[o.k].val / 8 + (o.k == SweepSim.Tank ? 2 : 0);
            if (s > bs) { bs = s; tx = c.x; ty = c.y; }
        }
    }
}
