using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SalvageRun.Orbit.Sim;

// 🧪 헤드리스 반복 테스트 (09-25 사장님 「반복 테스트 가능해? 헤드리스」) — 유니티 없이 게임 규칙(Sim)만 돌린다.
//
//   dotnet run -c Release -- test          씨앗 12 · 퍼징 30 · 저장 · 구역 · 무한 궤도
//   dotnet run -c Release -- test 50       씨앗 50 (퍼징도 50)
//
// 통과 = 오류 없음 + 규칙 검사(돈 · 빚 · 칸 레벨 · 가격 · 구역)가 한 번도 안 깨짐. 실패하면 씨앗과 걸음을 찍는다.
static class Tests
{
    static int fails;
    static void Fail(string what) { fails++; if (fails <= 30) Console.WriteLine("  ✗ " + what); }
    static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    public static int RunAll(int n)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"🧪 헤드리스 테스트 — 씨앗 {n}");
        Section("1. 봇이 끝까지 간다", () => Bots(n));
        Section("2. 무작위 손 (퍼징)", () => Fuzz(n));
        Section("3. 밀린 저장 옮기기", Migration);
        Section("4. 행성 구역 규칙", Zones);
        Section("5. 무한 궤도", Endless);
        Console.WriteLine();
        Console.WriteLine(fails == 0 ? $"✅ 모두 통과 ({sw.Elapsed.TotalSeconds:0}초)" : $"❌ 실패 {fails}건 ({sw.Elapsed.TotalSeconds:0}초)");
        return fails == 0 ? 0 : 1;
    }
    static void Section(string name, Action a)
    {
        int f0 = fails; var sw = Stopwatch.StartNew();
        Console.WriteLine(); Console.WriteLine("── " + name);
        try { a(); } catch (Exception e) { Fail("예외: " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace?.Split('\n').FirstOrDefault()); }
        Console.WriteLine($"   {(fails == f0 ? "통과" : "실패 " + (fails - f0))} · {sw.Elapsed.TotalSeconds:0.0}초");
    }

    // 규칙 검사 — 어느 걸음에서든 깨지면 안 되는 것
    static void Check(SweepSim sim, string where)
    {
        var S = sim.S;
        if (!Finite(S.cash) || S.cash < -1e-6) Fail($"{where}: 돈 {S.cash}");
        if (!Finite(S.debt) || S.debt < -1e-6) Fail($"{where}: 빚 {S.debt}");
        if (S.keys < 0) Fail($"{where}: 열쇠 {S.keys}");
        for (int i = 0; i < SweepSim.NodeCount; i++)
        {
            var nd = SweepSim.Nodes[i];
            if (S.lv[i] < 0 || S.lv[i] > nd.max) { Fail($"{where}: {nd.id} 레벨 {S.lv[i]} / {nd.max}"); break; }
            if (sim.State(i) == NodeSt.Can && SweepSim.Zone[i] > sim.ZoneOpen) { Fail($"{where}: {nd.id} 구역 {SweepSim.Zone[i]} > 열린 {sim.ZoneOpen} 인데 살 수 있음"); break; }
            if (sim.State(i) == NodeSt.Can && !(sim.TileCost(i) > 0 && Finite(sim.TileCost(i)))) { Fail($"{where}: {nd.id} 값 {sim.TileCost(i)}"); break; }
        }
        if (!(sim.ValMult > 0 && Finite(sim.ValMult))) Fail($"{where}: 값 배율 {sim.ValMult}");
        if (!(sim.HpMul > 0 && Finite(sim.HpMul))) Fail($"{where}: 체력 배율 {sim.HpMul}");
        if (S.shop != null) for (int k = 0; k < S.shop.Count; k++) if (!(sim.ShelfPrice(k) > 0)) { Fail($"{where}: 가게 {k}번 값 {sim.ShelfPrice(k)}"); break; }
    }

    // 한 판 — 조준은 가까운 쓰레기 · 가끔 수동 사격 · 블랙홀
    static void PlayRun(SweepSim sim, Random rng, bool clicks)
    {
        sim.StartRun(); var R = sim.R; double ax = 480, ay = 300; long ticks = 0;
        while (!R.over)
        {
            if (++ticks > 30000) { Fail($"판이 안 끝난다 (연료 {R.fuel:0.0} · 잔해 {R.junk.Count})"); break; }
            if (ticks % 8 == 0 && R.junk.Count > 0) { var j = R.junk[rng.Next(R.junk.Count)]; if (!j.dead) { ax = j.x; ay = j.y; } }
            bool cast = !R.holding && R.shots > 0 && rng.NextDouble() < 0.01;
            sim.Tick(0.05, ax, ay, true, cast);
            if (clicks && rng.NextDouble() < 0.15) sim.ClickShot(ax, ay);
            while (sim.Events.Count > 0) sim.Events.Dequeue();
        }
    }

    static void Bots(int n)
    {
        var mins = new List<double>(); var banks = new List<int>(); int stuck = 0;
        for (int seed = 1; seed <= n; seed++)
        {
            var r = Program.RunQuiet(seed * 7 + 1);
            if (!r.won) { stuck++; Fail($"씨앗 {seed * 7 + 1}: 끝나지 않음 ({r.minutes:0}분 · 청구서 {r.bill})"); continue; }
            mins.Add(r.minutes); banks.Add(r.bankrupt);
        }
        if (mins.Count == 0) return;
        mins.Sort();
        Console.WriteLine($"   끝 {mins.Count}/{n} · 분 최소 {mins[0]:0} · 가운데 {mins[mins.Count / 2]:0} · 최대 {mins[^1]:0} · 파산 평균 {banks.Average():0.0} (최대 {banks.Max()})");
        if (mins[^1] > 300) Fail($"너무 긴 판 {mins[^1]:0}분");
    }

    static void Fuzz(int n)
    {
        long steps = 0;
        for (int seed = 1; seed <= n; seed++)
        {
            var rng = new Random(seed * 131);
            var sim = new SweepSim(null, null, seed);
            for (int cyc = 0; cyc < 80 && !sim.M.won; cyc++)
            {
                string at = $"씨앗 {seed} 걸음 {cyc}";
                if (sim.M.careerOpen) { for (int i = 0; i < SweepSim.CareerCount; i++) if (rng.NextDouble() < .5) sim.BuyCareer(i); sim.CloseCareer(); }
                // 부자 흉내 — 가끔 돈을 크게 준다 (뒤쪽 칸 · 행성까지 닿게)
                if (rng.NextDouble() < .25) sim.S.cash += Math.Pow(10, rng.Next(2, 10));
                if (rng.NextDouble() < .2) sim.S.keys += rng.Next(0, 3);
                int acts = rng.Next(1, 12);
                for (int a = 0; a < acts; a++)
                {
                    switch (rng.Next(12))
                    {
                        case 0: case 1: case 2: case 3: sim.BuyTile(rng.Next(SweepSim.NodeCount)); break;           // 아무 칸이나 눌러 본다
                        case 4: sim.PayBill(); break;
                        case 5: sim.TakeLoan(sim.LoanCap * rng.NextDouble()); break;
                        case 6: sim.RepayDebt(); break;
                        case 7: sim.ScratchBuy(out _); sim.ScratchClaim(); break;
                        case 8: if (sim.S.shop != null && sim.S.shop.Count > 0) sim.BuyPart(rng.Next(sim.S.shop.Count)); break;
                        case 9: sim.RerollShop(); break;
                        case 10: sim.SetOrbit(rng.Next(SweepSim.Orbits.Length)); break;
                        case 11: if (sim.CanBankrupt && rng.NextDouble() < .3) sim.Bankrupt(); break;
                    }
                    for (int pi = 1; pi < SweepSim.Orbits.Length; pi++) if (rng.NextDouble() < .3) sim.BuyPermit(pi);
                    steps++;
                    Check(sim, at);
                }
                if (sim.M.careerOpen) continue;
                if (sim.S.overdue && sim.S.cash < sim.BillAmount && !sim.LoanAndPay() && sim.CanBankrupt) { sim.Bankrupt(); continue; }
                PlayRun(sim, rng, true);
                Check(sim, at + " 판 뒤");
            }
        }
        Console.WriteLine($"   걸음 {steps}");
    }

    static int Ix(string id) => Array.FindIndex(SweepSim.Nodes, x => x.id == id);

    static void Migration()
    {
        // 09-24 밤 배치(130칸 · 새 칸 12개가 106번에)로 저장된 판을 만든다 → 불러오면 id 기준으로 제자리
        var ids = new[] { "w_laser_e", "w_chain_e", "w_vac_e", "w_mine_e", "w_frz_e", "w_clus_e", "w_mag_e", "w_rail_e", "l_more", "l_luck", "l_free", "l_jack" };
        var old = new[] { "w_rail_a", "x_claw_arm", "x_arm_drone", "x_drone_bh", "x_bh_eco", "x_eco_route", "x_route_claw", "i_claw", "i_drone", "i_bh", "i_eco", "i_route" };
        var want = new Dictionary<string, int>();
        var s = new SweepState(); s.lv = new int[130];
        for (int k = 0; k < 106; k++) s.lv[k] = 0;
        s.lv[Ix("c_pow")] = 5; want["c_pow"] = 5;                     // 앞쪽은 그대로
        int[] eVals = { 3, 2, 1, 3, 0, 2, 0, 1 }, lVals = { 3, 2, 1, 1 }, oVals = { 1, 1, 0, 1, 0, 0, 1, 4, 0, 7, 2, 0 };
        for (int j = 0; j < 8; j++) { s.lv[106 + j] = eVals[j]; want[ids[j]] = eVals[j]; }
        for (int j = 0; j < 4; j++) { s.lv[114 + j] = lVals[j]; want[ids[8 + j]] = lVals[j]; }
        for (int j = 0; j < 12; j++) { s.lv[118 + j] = oVals[j]; want[old[j]] = oVals[j]; }
        var sim = new SweepSim(s, null, 1);
        foreach (var kv in want) if (sim.Lv(kv.Key) != kv.Value) Fail($"옮기기: {kv.Key} = {sim.Lv(kv.Key)} (기대 {kv.Value})");
        if (sim.S.layout != 2) Fail($"옮기기: layout {sim.S.layout}");
        // 한 번 옮긴 판은 다시 옮기지 않는다
        var again = new SweepSim(sim.S, sim.M, 1);
        foreach (var kv in want) if (again.Lv(kv.Key) != kv.Value) Fail($"두 번째 불러오기: {kv.Key} = {again.Lv(kv.Key)}");
        // 옛 118칸 판 — 뒤에 붙기만
        var s118 = new SweepState(); s118.lv = new int[118]; s118.lv[Ix("i_bh")] = 9;
        var sim118 = new SweepSim(s118, null, 1);
        if (sim118.Lv("i_bh") != 9 || sim118.Lv("w_laser_e") != 0) Fail($"118칸 판: i_bh {sim118.Lv("i_bh")} · w_laser_e {sim118.Lv("w_laser_e")}");
        Console.WriteLine("   130칸 · 118칸 · 두 번 불러오기");
    }

    // 사람처럼 — 지금 열린 구역까지에서 살 수 있는 가장 싼 칸 (레벨 상관없이 · 행성 항로는 빼고)
    static bool BuyCheapest(SweepSim sim, int zmax)
    {
        int best = -1; double bc = double.MaxValue;
        for (int i = 0; i < SweepSim.NodeCount; i++)
            if (SweepSim.Zone[i] <= zmax && !SweepSim.Nodes[i].id.StartsWith("p_") && !SweepSim.Infinite(i) && sim.State(i) == NodeSt.Can && sim.TileCost(i) < bc) { bc = sim.TileCost(i); best = i; }
        return best >= 0 && sim.BuyTile(best);
    }
    static string Left(SweepSim sim, int z) => string.Join(" ", Enumerable.Range(0, SweepSim.NodeCount).Where(i => SweepSim.Zone[i] == z && SweepSim.ZoneNeed(i) && sim.S.lv[i] <= 0).Select(i => SweepSim.Nodes[i].id + ":" + sim.State(i)));

    static void Zones()
    {
        var sim = new SweepSim(null, null, 5);
        int moon = Ix("p_moon");
        if (sim.State(moon) == NodeSt.Can) Fail("지구 구역을 안 찍었는데 달 항로를 살 수 있음");
        sim.S.cash = 1e12;
        for (int guard = 0; guard < 2000 && sim.ZoneLeft(0) > 0; guard++)
            if (!BuyCheapest(sim, 0)) { Fail($"지구 구역에 살 수 없는 칸이 남음 ({sim.ZoneLeft(0)}개: {Left(sim, 0)})"); break; }
        if (sim.State(moon) != NodeSt.Can) Fail($"지구 구역을 다 찍었는데 달 항로 {sim.State(moon)}");
        // 모든 구역이 차례로 열리는가 (돈 무한 · 열쇠 무한)
        sim.S.keys = 999;
        for (int z = 0; z + 1 < SweepSim.OrbitOrder.Length; z++)
        {
            for (int guard = 0; guard < 4000 && sim.ZoneLeft(sim.ZoneOpen) > 0; guard++) if (!BuyCheapest(sim, sim.ZoneOpen)) break;
            if (sim.ZoneLeft(sim.ZoneOpen) > 0) { Fail($"{SweepSim.ZoneName[sim.ZoneOpen]} 구역 칸 {sim.ZoneLeft(sim.ZoneOpen)}개를 끝내 못 산다: {Left(sim, sim.ZoneOpen)}"); break; }
            int next = SweepSim.OrbitOrder[z + 1];
            if (!sim.BuyPermit(next)) { Fail($"{SweepSim.Orbits[next].name} 항로를 못 산다 ({sim.State(Ix(SweepSim.PlanetNode[next]))})"); break; }
        }
        Console.WriteLine($"   열린 구역 {SweepSim.ZoneName[sim.ZoneOpen]}");
    }

    static void Endless()
    {
        var sim = new SweepSim(null, null, 9); var rng = new Random(9);
        sim.S.cash = 1e13; sim.S.keys = 999;
        for (int i = 0; i < 3000; i++) sim.BuyTile(i % SweepSim.NodeCount);
        sim.S.bill = SweepSim.Bills.Length; sim.M.won = true;
        sim.EnterEndless();
        if (!sim.M.endless || sim.M.won) Fail("무한 궤도로 못 들어감");
        double hp0 = sim.HpMul; int d0 = sim.M.depth;
        for (int r = 0; r < 6; r++) { PlayRun(sim, rng, false); Check(sim, $"무한 {r}"); }
        if (sim.M.depth != d0 + 6) Fail($"층이 안 늘어남 {d0} → {sim.M.depth}");
        if (!(sim.HpMul > hp0)) Fail("층이 늘어도 체력이 그대로");
        Console.WriteLine($"   {d0}층 → {sim.M.depth}층 · 체력 ×{sim.HpMul / hp0:0.0}");
    }
}
