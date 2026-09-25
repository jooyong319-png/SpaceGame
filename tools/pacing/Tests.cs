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
        Section("6. 파산만 거듭하기 · 다시 불러오기", () => Bankrupts(Math.Max(3, n / 4)));
        Section("7. 무한 궤도 60층", DeepEndless);
        Section("8. 가게 소모품", Consumables);
        Section("9. 복권 칸", Lotto);
        Section("10. 수동 사격", () => Clicks(Math.Max(4, n / 2)));
        Section("11. 트리 배치 — 같은 자리에 칸 둘 금지", TreeLayout);
        Section("12. 행성 특성 — 행성마다 나오나", Traits);
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

    // 6 — 회사 여러 대를 연달아 파산 · 파산 뒤 다시 불러와도 칸이 그대로인가
    static void Bankrupts(int n)
    {
        for (int seed = 1; seed <= n; seed++)
        {
            var sim = new SweepSim(null, null, seed * 17); var rng = new Random(seed);
            for (int c = 0; c < 10; c++)
            {
                if (sim.M.careerOpen) { for (int i = 0; i < SweepSim.CareerCount; i++) sim.BuyCareer(i); sim.CloseCareer(); }
                sim.S.cash += 1e7; sim.S.keys += 2;
                for (int k = 0; k < 200; k++) BuyCheapest(sim, sim.ZoneOpen);
                for (int pi = 1; pi < SweepSim.Orbits.Length; pi++) sim.BuyPermit(pi);
                for (int k = 0; k < 100; k++) BuyCheapest(sim, sim.ZoneOpen);
                while (sim.S.bill < 3 && sim.PayBill()) { }
                PlayRun(sim, rng, false);
                var perm = sim.M.perm.ToList();
                int keysBefore = sim.S.keys, bk = sim.BankruptKeys;
                if (!sim.CanBankrupt) { sim.S.bill = Math.Max(sim.S.bill, 3); }
                if (!sim.Bankrupt()) { Fail($"씨앗 {seed} 회사 {c}: 파산 안 됨"); break; }
                if (sim.S.keys != keysBefore + bk) Fail($"씨앗 {seed} 회사 {c}: 열쇠 {sim.S.keys} (기대 {keysBefore + bk})");
                foreach (var id in perm) if (sim.Lv(id) <= 0) Fail($"씨앗 {seed} 회사 {c}: 핵심 칸 {id} 가 파산 뒤 꺼짐");
                if (sim.S.layout != 2) Fail($"씨앗 {seed} 회사 {c}: 파산 뒤 layout {sim.S.layout}");
                // 새 회사에서 좀 산 뒤 저장 → 다시 불러오기 (칸 번호가 그대로인가)
                if (sim.M.careerOpen) { sim.CloseCareer(); }
                sim.S.cash += 1e9; for (int k = 0; k < 300; k++) BuyCheapest(sim, 9);
                var snap = (int[])sim.S.lv.Clone();
                var re = new SweepSim(sim.S, sim.M, 1);
                for (int i = 0; i < SweepSim.NodeCount; i++) if (re.S.lv[i] != snap[i]) { Fail($"씨앗 {seed} 회사 {c}: 다시 불러오니 {SweepSim.Nodes[i].id} {snap[i]} → {re.S.lv[i]}"); break; }
                sim = re;
                Check(sim, $"씨앗 {seed} 회사 {c}");
            }
        }
        Console.WriteLine($"   {n}씨앗 × 회사 10대");
    }

    // 7 — 무한 궤도를 60층까지: 숫자가 터지지 않고 판이 끝나는가
    static void DeepEndless()
    {
        var sim = new SweepSim(null, null, 21); var rng = new Random(21);
        sim.S.cash = 1e13; sim.S.keys = 999;
        for (int i = 0; i < 3000; i++) sim.BuyTile(i % SweepSim.NodeCount);
        sim.S.bill = SweepSim.Bills.Length; sim.M.won = true; sim.EnterEndless();
        for (int r = 0; r < 60; r++) { PlayRun(sim, rng, false); Check(sim, $"무한 {sim.M.depth}층"); if (fails > 0 && fails > 20) break; }
        Console.WriteLine($"   {sim.M.depth}층 · 체력 ×{sim.HpMul:0} · 값 ×{sim.ValMult:0.0e0} · 돈 {sim.S.cash:0.0e0}");
    }

    // 8 — 소모품은 다음 판에만 먹고 사라진다
    static void Consumables()
    {
        var sim = new SweepSim(null, null, 33); var rng = new Random(33);
        sim.S.cash = 1e9; sim.S.keys = 9;
        sim.BuyTile(Ix("e_shop"));
        for (int k = 0; k < 400 && !sim.ShopOpen; k++) BuyCheapest(sim, 9);
        if (!sim.ShopOpen) { sim.S.lv[Ix("e_shop")] = 1; }
        double fuel0 = sim.FuelMax;
        // 진열을 소모품으로 채워 산다
        sim.S.shop.Clear(); for (int i = 0; i < 4; i++) sim.S.shop.Add(SweepSim.Cons0 + i); sim.S.shopSale = -1;
        int sc0 = sim.ScratchLeft;
        for (int i = 0; i < 4; i++) if (!sim.BuyPart(0)) Fail($"소모품 {i} 못 삼");
        if (sim.S.nFuel != 10 || sim.S.nDmg != 20 || sim.S.nVal != 15) Fail($"소모품 쌓임: 연료 {sim.S.nFuel} 화력 {sim.S.nDmg} 값 {sim.S.nVal}");
        if (sim.ScratchLeft != sc0 + 3) Fail($"복권 묶음: {sc0} → {sim.ScratchLeft}");
        double dmgOff = sim.DmgMul, valOff = sim.ValMult;
        sim.StartRun();
        if (Math.Abs(sim.R.max - (fuel0 + 10)) > 1e-6) Fail($"연료 캔: 판 연료 {sim.R.max} (기대 {fuel0 + 10})");
        if (!(sim.DmgMul > dmgOff * 1.19)) Fail($"과부하 탄창: 화력 {dmgOff} → {sim.DmgMul}");
        if (!(sim.ValMult > valOff * 1.14)) Fail($"감정 할인권: 값 {valOff} → {sim.ValMult}");
        while (!sim.R.over) { sim.Tick(0.05, 480, 300, true, false); sim.Events.Clear(); }
        if (sim.S.nFuel != 0 || sim.S.nDmg != 0 || sim.S.nVal != 0) Fail("소모품이 판 뒤에도 남음");
        sim.StartRun();
        if (Math.Abs(sim.R.max - fuel0) > 1e-6) Fail($"다음 판에도 연료 캔이 먹음 {sim.R.max}");
        Console.WriteLine("   연료 캔 · 복권 묶음 · 과부하 탄창 · 감정 할인권");
    }

    // 9 — 복권 칸
    // 🌳 트리 배치 — 한 자리에 칸이 둘이면 「같은 곳을 두 번 눌러야」 한다 (09-25 사장님 · 26곳이 겹쳐 있었다)
    static void TreeLayout()
    {
        var seen = new Dictionary<(int, int), string>(); int tiles = 0;
        for (int i = 0; i < SweepSim.NodeCount; i++)
        {
            var id = SweepSim.Nodes[i].id;
            if (!SweepSim.Layout.TryGetValue(id, out var pl)) { Fail($"{id} 자리가 없다"); continue; }
            if (pl.par != "R")
            {
                int pix = -1; for (int q = 0; q < SweepSim.NodeCount; q++) if (SweepSim.Nodes[q].id == pl.par) pix = q;
                if (pix < 0) { Fail($"{id} 부모 {pl.par} 없음"); continue; }
                int T = SweepSim.Tiles(pix);
                if (pl.tile < 1 || pl.tile > T) Fail($"{id} 부모 {pl.par} 칸 {pl.tile} / {T}");
            }
            int n = SweepSim.Tiles(i);
            if (n > 1 && pl.dx == 0 && pl.dy == 0) Fail($"{id} 칸 {n}개가 한 자리 (dx · dy 0)");
            for (int j = 1; j <= n; j++)
            {
                var c = (pl.x + pl.dx * (j - 1), pl.y + pl.dy * (j - 1)); tiles++;
                if (seen.TryGetValue(c, out var other)) Fail($"{c} 에 {other} 와 {id} #{j} 가 겹침");
                else seen[c] = id + " #" + j;
            }
        }
        // 선이 남의 칸을 가로지르면 그 칸 뒤에 이어진 것처럼 보인다 — 부모 칸 → 자식 칸 직선이 다른 칸 한가운데를 지나는 곳을 센다
        var cellOf = new Dictionary<(string, int), (int, int)>();
        foreach (var kv in seen) { var sp = kv.Value.Split(" #"); cellOf[(sp[0], int.Parse(sp[1]))] = kv.Key; }
        int cross = 0;
        for (int i = 0; i < SweepSim.NodeCount; i++)
        {
            var nd = SweepSim.Nodes[i]; var pl = SweepSim.Layout[nd.id];
            var ends = new List<(int, int)>();
            if (pl.par != "R") ends.Add(cellOf[(pl.par, pl.tile)]);
            foreach (var p in nd.par) if (p != pl.par && cellOf.ContainsKey((p, 1))) ends.Add(cellOf[(p, 1)]);
            var me = cellOf[(nd.id, 1)];
            foreach (var a0 in ends)
                foreach (var kv in seen)
                {
                    var c = kv.Key; if (c == a0 || c == me) continue;
                    double ax = a0.Item1, ay = a0.Item2, bx = me.Item1, by = me.Item2, L2 = (bx - ax) * (bx - ax) + (by - ay) * (by - ay);
                    double t = ((c.Item1 - ax) * (bx - ax) + (c.Item2 - ay) * (by - ay)) / L2; if (t <= 0 || t >= 1) continue;
                    double dx = ax + t * (bx - ax) - c.Item1, dy = ay + t * (by - ay) - c.Item2;
                    if (dx * dx + dy * dy < 0.3 * 0.3) cross++;                                   // 모양 문제 — 세기만 (✕ 연결 칸의 긴 선이 대부분)
                }
        }
        Console.WriteLine($"   칸 {tiles}개 · 자리 {seen.Count}곳 · ⚠ 선이 남의 칸 위를 지나는 곳 {cross} (실패 아님)");
    }

    static void Lotto()
    {
        var sim = new SweepSim(null, null, 44);
        sim.S.cash = 1e9;
        int b0 = sim.ScratchLeft; double c0 = sim.ScratchCost;
        if (b0 != 3) Fail($"처음 복권 {b0}장");
        sim.S.lv[Ix("l_more")] = 2; sim.S.lv[Ix("l_free")] = 1;
        if (sim.ScratchLeft != 5) Fail($"복권 단골 2: {sim.ScratchLeft}장");
        if (sim.ScratchCost != 0) Fail($"첫 장 공짜: {sim.ScratchCost}");
        double cash = sim.S.cash; sim.ScratchBuy(out _); sim.ScratchClaim();
        if (sim.ScratchCost <= 0) Fail("둘째 장도 공짜");
        int wins = 0; var s2 = new SweepSim(null, null, 45); s2.S.cash = 1e12; s2.S.lv[Ix("l_luck")] = 3;
        for (int i = 0; i < 4000; i++) { s2.S.runs = i; s2.ScratchBuy(out int w); s2.ScratchClaim(); if (w >= 0) wins++; }
        var s3 = new SweepSim(null, null, 46); s3.S.cash = 1e12; int wins0 = 0;
        for (int i = 0; i < 4000; i++) { s3.S.runs = i; s3.ScratchBuy(out int w); s3.ScratchClaim(); if (w >= 0) wins0++; }
        if (!(wins > wins0 * 1.3)) Fail($"행운의 긁개 3: 당첨 {wins} vs 없음 {wins0}");
        Console.WriteLine($"   단골 · 공짜 · 긁개(당첨 {wins0} → {wins} / 4000)");
    }

    // 10 — 클릭한 판이 더 부순다
    static void Clicks(int n)
    {
        long a = 0, b = 0;
        for (int seed = 1; seed <= n; seed++)
        {
            var s1 = new SweepSim(null, null, seed); var s2 = new SweepSim(null, null, seed);
            PlayRun(s1, new Random(seed), false); PlayRun(s2, new Random(seed), true);
            a += s1.R.broke; b += s2.R.broke;
        }
        if (!(b > a)) Fail($"클릭해도 더 안 부숨 ({a} vs {b})");
        Console.WriteLine($"   첫 판 부순 수 — 클릭 없음 {a} · 클릭 {b} ({(a > 0 ? (double)b / a : 0):0.00}배)");
    }

    // 🪐 행성마다 한 가지 (09-26) — 행성마다 30초 돌려 특성이 실제로 나오고 오류가 없는지
    static void Traits()
    {
        var line = new List<string>();
        for (int orbit = 0; orbit < SweepSim.Orbits.Length; orbit++)
        {
            var sim = new SweepSim(null, null, 100 + orbit);
            sim.S.planets = 511; sim.S.orbit = orbit; sim.S.cash = 1e6;
            sim.StartRun(); sim.R.fuel = sim.R.max = 60;
            int tr = SweepSim.TraitOf(orbit); bool seen = false, fired = false; int notes = 0; var rng = new Random(orbit);
            double ax = 480, ay = 300;
            for (int t = 0; t < 600 && !sim.R.over; t++)
            {
                if (t % 6 == 0 && sim.R.junk.Count > 0) { var j = sim.R.junk[rng.Next(sim.R.junk.Count)]; if (!j.dead) { ax = j.x; ay = j.y; } }
                sim.Tick(0.05, ax, ay, true, false);
                foreach (var d in sim.R.junk) { if (d.sig == tr && tr != 0) seen = true; if (d.sig == 10) fired = true; }
                if (tr == 5 && sim.SpotOn) seen = true;
                if (tr == 8 && sim.GustOn) seen = true;
                if (tr == 9 && sim.Comet != null) seen = true;
                while (sim.Events.Count > 0) { var e = sim.Events.Dequeue(); if (e.kind == SwEv.Pop && e.text != null && e.text.StartsWith("★")) notes++; }
            }
            if (tr > 0 && !seen) Fail($"{SweepSim.Orbits[orbit].name}: 「{SweepSim.TraitName[tr]}」이 30초 안에 안 나옴");
            line.Add($"{SweepSim.Orbits[orbit].name} {(seen ? "○" : "✕")}{(fired ? "연쇄" : "")}·알림{notes}");
        }
        Console.WriteLine("   " + string.Join(" · ", line));
    }
}
