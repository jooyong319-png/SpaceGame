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
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (double.TryParse(Environment.GetEnvironmentVariable("LOANMULT"), out double lm)) SweepSim.LoanMult = lm;
        if (args.Length > 0 && args[0] == "test") { Environment.ExitCode = Tests.RunAll(args.Length > 1 && int.TryParse(args[1], out int tn) ? tn : 12); return; }   // 🧪 헤드리스 테스트
        var seeds = args.Length > 0 && int.TryParse(args[0], out int one) ? new[] { one } : new[] { 3, 7, 11 };
        bool verbose = args.Contains("verbose");
        foreach (var s in seeds) Run(s, verbose);
    }

    public class Result { public bool won; public double minutes; public int bankrupt, bill; }
    static bool quiet;
    public static Result RunQuiet(int seed) { quiet = true; try { return Run(seed, false); } finally { quiet = false; } }

    static Result Run(int seed, bool verbose)
    {
        var sim = new SweepSim(null, null, seed);
        var rng = new Random(seed * 31 + 1);
        double ax = 600, ay = 360, tx = 600, ty = 360, retarget = 0, shopClock = 0;
        bool hold = false;
        var log = new List<string>();
        var segEarn = new double[9]; var segRuns = new int[9]; var segSplit = new double[9, 3];
        double Min() => (sim.M.playSeconds + shopClock) / 60;

        for (int guard = 0; guard < 400 && !sim.M.won; guard++)
        {
            // ── 정비소
            if (sim.M.careerOpen)
            {
                bool bought = true;
                while (bought) { bought = false; int best = -1, bc = 999; for (int i = 0; i < SweepSim.CareerCount; i++) { int c = sim.CareerCost(i); if (c > 0 && c <= sim.M.credit && c < bc) { bc = c; best = i; } } if (best >= 0) { sim.BuyCareer(best); bought = true; } }
                sim.CloseCareer();
            }
            if (!sim.M.cleanReady)
            {
                if (sim.PayBill()) log.Add($"{Min(),6:0.0}분  {sim.M.company}대  청구서 {sim.S.bill} 갚음  (출동 {sim.S.runs})");
                // 납부일 — 모자라면 대출받아 갚는다. 한도가 모자라면 파산
                if (sim.S.overdue && sim.S.cash < sim.BillAmount)
                {
                    double need = sim.BillAmount - sim.S.cash;
                    if (sim.LoanAndPay()) log.Add($"{Min(),6:0.0}분  {sim.M.company}대  🏦 대출 {need:0} 받아 청구서 {sim.S.bill} 갚음 · 빚 {sim.S.debt:0}");
                }
                if (sim.CanBankrupt && sim.S.overdue)
                {
                    log.Add($"{Min(),6:0.0}분  {sim.M.company}대  💥 파산 (청구서 {sim.S.bill} · 출동 {sim.S.runs} · 신용 +{sim.S.creditPending})");
                    sim.Bankrupt();
                    continue;
                }
                if (sim.S.bill >= SweepSim.Bills.Length && sim.RepayDebt()) log.Add($"{Min(),6:0.0}분  {sim.M.company}대  🏦 빚 갚는 중 · 남은 빚 {sim.S.debt:0}");
                // 행성 허가증 — 기한이 3판 넘게 남았거나, 사고도 청구서 몫이 남으면 산다
                for (int pi = 1; pi < SweepSim.Orbits.Length; pi++)
                    if (sim.OnSale(pi) && !sim.S.overdue && sim.S.cash >= SweepSim.Orbits[pi].permit && (sim.S.billDue >= 3 || sim.S.cash - SweepSim.Orbits[pi].permit >= sim.BillAmount) && sim.BuyPermit(pi))
                        log.Add($"{Min(),6:0.0}분  {sim.M.company}대  🪐 {SweepSim.Orbits[pi].name} 허가증 ({SweepSim.Orbits[pi].permit:0})");
                sim.SetOrbit(sim.MaxOrbit);
                // 사기 — 청구서 몫은 남겨 두고 싼 것부터
                // 기한이 한 판 남았거나 연체 중이면 모은다 (사람도 그렇게 한다)
                double reserve = sim.S.overdue || sim.S.billDue <= 1 ? double.MaxValue : Math.Min(sim.BillAmount, sim.S.cash * 0.35);   // 첫 청구서 전엔 아끼지 않는다 (자동 집게부터)
                for (int loop = 0; loop < 60; loop++)
                {
                    int best = -1; double bc = double.MaxValue;
                    for (int i = 0; i < SweepSim.NodeCount; i++) if (!SweepSim.Nodes[i].id.StartsWith("p_") && SweepSim.Nodes[i].id != "e_shop" && sim.State(i) == NodeSt.Can && sim.TileCost(i) < bc) { bc = sim.TileCost(i); best = i; }
                    if (best < 0 || reserve == double.MaxValue || sim.S.cash - bc < reserve) break;
                    sim.Buy(best);
                }
            }
            shopClock += ShopSec;

            // ── 출동
            int seg = Math.Min(8, sim.S.bill + 1);
            sim.StartRun();
            var R = sim.R;
            long ticks = 0;
            while (!R.over)
            {
                if (++ticks > 20000) { if (!quiet) Console.WriteLine($"  ⚠ 판이 안 끝난다: 연료 {R.fuel:0.0} 붙잡음 {R.holding} 연쇄대기 {R.pend.Count} 잔해 {R.junk.Count}"); break; }
                retarget -= Dt;
                if (!hold && sim.ClawR <= 0)
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
            if (R.clean) { log.Add($"{Min(),6:0.0}분  ✨ 청산 출동 끝 — 빚 청산"); break; }
            segEarn[seg] += R.Earned; segRuns[seg]++;
            segSplit[seg, 0] += R.earnClaw; segSplit[seg, 1] += R.earnDrone; segSplit[seg, 2] += R.earnBlast;
            if (verbose) Console.WriteLine($"{Min(),6:0.0}분    출동 {sim.S.runs,2}  구간 {seg}  {SweepSim.Orbits[sim.S.orbit].name}  +{R.Earned,8:0}  연쇄 {R.chainBest,3}  압축 {R.packBest,3}  돈 {sim.S.cash,8:0}  청구서 {sim.BillAmount,7:0}{(sim.S.overdue ? " 연체" : " 기한 " + sim.S.billDue)}");
        }

        var res = new Result { won = sim.M.won, minutes = Min(), bankrupt = sim.M.bankrupt, bill = sim.S.bill };
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

    static string Pct(double a, double t) => t <= 0 ? "-" : Math.Round(a / t * 100).ToString();

    static void Nearest(SweepSim sim, double ax, double ay, ref double tx, ref double ty)
    {
        double bd = double.MaxValue;
        foreach (var o in sim.R.junk)
        {
            if (o.dead || o.fade < 0.5) continue;
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
            var c = list[rng.Next(list.Count)]; if (c.dead) continue;
            double s = 0;
            foreach (var o in list) if (!o.dead && Math.Abs(o.x - c.x) < 60 && Math.Abs(o.y - c.y) < 60) s += 1 + SweepSim.Types[o.k].val / 8 + (o.k == SweepSim.Tank ? 2 : 0);
            if (s > bs) { bs = s; tx = c.x; ty = c.y; }
        }
    }
}
