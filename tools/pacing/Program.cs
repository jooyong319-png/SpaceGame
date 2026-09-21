using System;
using System.Collections.Generic;
using System.Linq;
using SalvageRun.Orbit.Sim;

// 궤도 청소부 — 45분 흐름을 봇으로 잰다.
//
// 🔴 재는 것은 「끝까지 가는가, 대략 몇 분에 무엇이 오는가」뿐이다.
//    재미·난이도는 안 잰다 — 그건 사장님이 해보고 판단하신다 (Wiki_gm playtesting.md).
//
// 사용법:  dotnet run -c Release            세 가지 성향을 다 돈다
//          dotnet run -c Release -- greedy  하나만
static class Program
{
    static void Main(string[] args)
    {
        var who = args.Length > 0 ? args : new[] { "greedy", "good", "lazy" };
        foreach (var w in who) Run(w);
    }

    // greedy = 유혹 셋 다 누른다 · good = 하나도 안 누른다 · lazy = 함대를 안 옮긴다
    static void Run(string style)
    {
        var sim = new OrbitSim();
        var S = sim.S;
        var rng = new Random(11);
        const double dt = 0.25;
        double pickClock = 0, rotateClock = 0, buyClock = 0;
        var log = new List<string>();
        double nextSnap = 0;
        var tempt = new HashSet<string> { "shatter", "insurance", "claim", "fleetshatter", "autolaunch", "finalcontract" };

        void L(string s) => log.Add($"{Fmt(S.t)}  {s}");

        while (!sim.Finished && S.t < 90 * 60)
        {
            // ── 손: 드론 전에는 1분에 8번쯤 줍는다
            if (S.bought == 0)
            {
                pickClock += dt;
                if (pickClock >= 7.5) { pickClock = 0; sim.Pick(0); }
            }
            if (!sim.Has("autosell") && S.held >= 5) sim.Sell();

            // ── 직접 파쇄: 충전이 다 차면 제일 빽빽한 궤도에 (greedy 만 — 사람이 부지런한 경우)
            if (style == "greedy" && S.bought > 0 && S.blastCharge >= sim.BlastMax - 0.01)
            {
                int bb = -1; double bd = -1;
                for (int i = 0; i < 3; i++) if (S.orbits[i].open && !S.orbits[i].locked && S.orbits[i].D > bd) { bd = S.orbits[i].D; bb = i; }
                if (bb >= 0) sim.Blast(bb);
            }

            // ── 인양 표적: 사람은 늘 보진 못한다
            if (S.salvage.active && S.salvage.life < 18 && rng.NextDouble() < 0.02) sim.ClaimSalvage();

            // ── 계약
            var c = S.contract;
            if (c.active && !c.accepted && c.offerLeft < 25)
            {
                if (c.kind == 1 && style == "good") sim.DeclineContract();
                else
                {
                    int o = c.orbit, k = c.kind;
                    sim.AcceptContract();
                    if (k == 0 && style != "lazy") sim.Move(o, false);
                }
            }

            // ── 배치: 60초마다 제일 살진 궤도로 몬다 (8회차 「로테이션」)
            rotateClock += dt;
            if (style != "lazy" && rotateClock >= 60 && !(c.active && c.accepted && c.kind == 0))
            {
                rotateClock = 0;
                int best = -1; double bv = -1;
                for (int i = 0; i < 3; i++)
                {
                    var o = S.orbits[i];
                    if (!o.open || o.locked) continue;
                    double v = sim.Density(i) * sim.Price(i);
                    if (v > bv) { bv = v; best = i; }
                }
                if (best >= 0 && best != S.home) sim.Move(best, false);
            }
            // 3막 대피: 경고가 뜬 궤도에서 뺀다 (greedy 는 끝까지 짜낸다)
            if (style == "good")
                for (int i = 0; i < 3; i++)
                    if (S.orbits[i].warned && !S.orbits[i].locked && S.orbits[i].drones > 0)
                        for (int j = i + 1; j < 3; j++) if (S.orbits[j].open && !S.orbits[j].locked) { sim.Move(j, false); break; }

            // ── 사기: 1초에 한 번 제일 싼 것
            buyClock += dt;
            if (buyClock >= 1)
            {
                buyClock = 0;
                var can = OrbitSim.Unlocks.Where(u => sim.CanBuy(u) && !u.ending)
                                          .Where(u => style != "good" || !tempt.Contains(u.id))
                                          .OrderBy(u => u.cost(sim)).ToList();
                if (can.Count > 0) sim.Buy(can[0].id);
                else if (!sim.Has("manager") && sim.DronePrice <= S.credits) sim.BuyDrone();

                if (S.endingsOpen && S.ending == 0 && S.orbits[1].warned)   // 사람은 한참 구경하다 산다
                {
                    string pick = style == "good" ? "end_net" : "end_max";
                    sim.Buy(pick);
                }
            }

            sim.Tick(dt);
            if (double.IsNaN(S.credits) || double.IsNaN(sim.TotalD) || double.IsNaN(sim.IncomeRate))
            {
                Console.WriteLine($"NaN at {Fmt(S.t)} act={S.act} credits={S.credits} D=[{S.orbits[0].D},{S.orbits[1].D},{S.orbits[2].D}] ramp=[{S.orbits[0].rampFrom},{S.orbits[1].rampFrom},{S.orbits[2].rampFrom}] ins={sim.InsuranceRate} col={sim.CollectIncome}");
                return;
            }

            while (sim.Events.Count > 0)
            {
                var e = sim.Events.Dequeue();
                switch (e.kind)
                {
                    case SimEventKind.Unlock: if (e.text != null) L("해금 " + e.text); else L("🔔 마지막 기술 셋 열림"); break;
                    case SimEventKind.Act: L($"━━ {S.act}막"); break;
                    case SimEventKind.Warn: L($"⚠ {OrbitSim.Names[e.orbit]} 경고"); break;
                    case SimEventKind.Lock: L($"■ {OrbitSim.Names[e.orbit]} 봉쇄"); break;
                    case SimEventKind.Ending: L($"★ 엔딩 {e.orbit} {OrbitSim.EndingTitle(e.orbit)}"); break;
                    case SimEventKind.News: L("  뉴스: " + e.text); break;
                }
            }
            if (S.bought == 1 && !log.Any(x => x.Contains("첫 드론"))) L("첫 드론");

            if (S.t >= nextSnap)
            {
                nextSnap += 120;
                string dens = string.Join(" ", Enumerable.Range(0, 3).Select(i => S.orbits[i].open ? (S.orbits[i].locked ? "■" : (S.orbits[i].D / S.orbits[i].D0).ToString("0.00")) : "-"));
                L($"· 크레딧 {K(S.credits)} · 초당 {K(sim.IncomeRate)} · 파편 {K(sim.TotalD)} · 함대 {sim.FleetTotal} · 밀도 [{dens}] 함대위치 {S.home}");
            }
        }

        Console.WriteLine($"\n================ {style} ================");
        foreach (var s in log) Console.WriteLine(s);
        Console.WriteLine($"끝: {Fmt(S.t)} · 엔딩 {OrbitSim.EndingTitle(S.ending)} · 최고 {K(S.peak)} · 만든 파편 {K(S.made)} (안 만들어도 됐던 것 {K(S.avoidable)}) · 잃은 드론 {S.dronesLost}");
    }

    static string Fmt(double t) => $"{(int)(t / 60):00}:{(int)(t % 60):00}";

    static string K(double v)
    {
        if (v < 10000) return ((long)v).ToString();
        string[] U = { "", "만", "억", "조", "경", "해" };
        var p = new List<long>(); long n = (long)Math.Min(v, 9e18);
        while (n > 0) { p.Add(n % 10000); n /= 10000; }
        int top = Math.Min(p.Count, U.Length) - 1;
        return p[top] + U[top];
    }
}
