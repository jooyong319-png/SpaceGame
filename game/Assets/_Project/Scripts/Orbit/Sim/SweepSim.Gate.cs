using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🛰 행성 관문 · 행성 허가 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ───────────────────────── 🛰 행성 관문 (09-26 사장님 「엄청 안 부서지는 무언가를 두고 그걸 레벨 디자인으로」 · 시안 HTSncyfVCbLX9kZiSBaGwo)
        public static readonly string[] GateNames = { Loc.T("폐우주정거장"), Loc.T("달 착륙선 잔해"), Loc.T("궤도 엘리베이터"), Loc.T("소행성 채굴기"), Loc.T("두 동강 난 화물선"), Loc.T("얼음 요새"), Loc.T("탐사 모선"), Loc.T("폭풍 관측소"), Loc.T("명왕성 탐사선"), Loc.T("혜성 채굴 기지"), Loc.T("보이저 탐사선") };   // 항로 순위 0~7 (카이퍼는 끝)
        public const int GateSig = 100;                                          // 관문 잔해 표시 (Junk.sig)
        public const double GateK = 10;                                          // 관문 체력 = 큰 잔해 × 60 (봇으로 맞춤)
        public int Frontier => ZoneOpen;                                         // 가장 먼 열린 행성의 순위
        public bool HasGate => Frontier + 1 < OrbitOrder.Length && !M.endless;  // 카이퍼 · 무한 궤도는 관문 없음
        public int GateBill => Frontier + 1;                                     // 🛰 09-27 관문은 그 차례 청구서를 갚아야 나타난다 (행성 하나 ≈ 청구서 한 장 · 뒤쪽 관문이 줄줄이 깨지던 것)
        public bool GateReady => HasGate && (S.bill >= GateBill || M.cleanReady);
        public string GateName => HasGate ? GateNames[Frontier] : "";
        public string NextName => HasGate ? Orbits[OrbitOrder[Frontier + 1]].name : "";
        public int GateMax { get { double want = GateHpNow; if (S.gateMax <= 0 || Frontier == 0 && S.gateMax > want) S.gateMax = want; return (int)Math.Min(2e9, Math.Round(S.gateMax)); } }   // 첫 관문은 옛 저장에 큰 값이 남아 있어도 새 값으로 줄인다
        double GateHpNow => Frontier == 0 ? Math.Max(Types[Big].hp * HpMul * GateK0, GateShotDmg * FuelMax / Gap * GateRuns0) : Math.Max(Types[Big].hp * HpMul * GateK, GateShotDmg * FuelMax / Gap * GateRuns);
        // 🛰 09-28 사장님 「초반 절대 못 깬다 — 위력 3만 찍어도 지구→달 관문을 부수게」: 지구 관문은 큰 잔해 × 7.5 (≈300) · 한 판 빔 피해의 120%.
        //    전엔 × 10 (≈405) 이라 한 방 4로 한 판(빔 60방 = 240) 안에 못 깼다 — 못 깨면 다음 판에 다시 가득 찬다.
        public const double GateK0 = 7.5, GateRuns0 = 1.2;   // 09-28 사장님 「아슬아슬하게 — 300까지」: ≈100 → ≈300 (위력 3 = 한 방 4, 자동 빔만으론 한 판 240 — 눌러 줘야 깬다)
        public double GateShotDmg => ShipGateK * Pow * (1 + Crit * (CritX - 1)) * (1 + 0.1 * Lv("c_double") + Part("dbl"));   // 🛰 처음 뜰 때 「지금 한 판 피해 × 6」으로 정한다 — 화력이 불어나도 늘 몇 판 공들여야
        public const double GateRuns = 1.6;
        public Junk GateJunk { get { if (R == null) return null; foreach (var d in R.junk) if (d.sig == GateSig && !d.dead) return d; return null; } }
        public double GateLeft { get { var g = GateJunk; return g != null ? Math.Max(0, (double)g.hp / Math.Max(1, g.max)) : S.gateFrac; } }
        public int TileCap => 3 + ZoneOpen;                                      // 🛰 행성 한도 — 지구 한 줄 세 칸, 관문 하나마다 한 칸 더
        public bool CapLocked(int i)
        {
            var n = Nodes[i]; if (n.id.StartsWith("p_") || M.endless) return false;
            if (Infinite(i)) return S.lv[i] >= 3 * (1 + ZoneOpen);
            if (n.id == "e_val") return NextTile(i) > 1 + ZoneOpen;          // 💰 고철 시세는 따로 묶는다 — 지구 한 칸 · 관문마다 한 칸 (09-26 「이거 때문에 너무 쉬워짐」)
            return Tiles(i) > 1 && NextTile(i) > TileCap;
        }
        void SpawnGate()
        {
            if (!HasGate || R.clean || S.orbit != OrbitOrder[Frontier]) return;
            var o = Orbits[S.orbit];
            var g = Spawn(Big, Rnd(0, Math.PI * 2), (o.bi + Bo) / 2, Att.None, false, 0.35);
            g.sig = GateSig; g.fade = 1;
            if (!GateReady) { g.max = g.hp = 1; return; }                          // 🛡 09-27 사장님 「보스가 없는데?」 — 차례 청구서 전엔 방어막 (보이기만 · 안 깨진다 · 체력은 풀릴 때 정한다)
            g.max = Math.Max(1, GateMax); g.hp = Math.Max(1, (int)Math.Round(g.max * Math.Max(0.02, S.gateFrac)));
        }
        public void DebugHit(Junk d, int dmg) => Hit(d, dmg, 0, true);             // 시험용
        public void DebugBlast(double x, double y) { for (int i = 0; i < 40; i++) DoBlast(x, y, 80); }   // 시험용
        public void DebugKill(Junk d) => Kill(d, 0, 1);                                   // 시험용
        public bool DebugBreakGate() { var g = GateJunk; if (g == null) return false; g.hp = 0; Kill(g, 0, 1); return true; }   // 시험용
        void GateHit(Junk d)
        {   // 칠 때마다 가끔 비싼 파편이 떨어진다 — 관문을 치는 판도 손해만은 아니게
            if (Rnd() < 0.25) SpawnFree(Rnd() < 0.75 ? Chip : Rnd() < 0.7 ? Sat : Vault, d.x, d.y, Rnd(-120, 120), Rnd(-90, 90), 2.4);
        }
        void GateBroken(Junk d)
        {
            int next = OrbitOrder[Frontier + 1];
            S.gateNext = next; S.gateFrac = 1; S.gateMax = 0; S.keys++;
            double bonus = Math.Max(S.runAvg * 3, BillAmount * 0.3); S.cash += bonus;
            Emit(SwEv.Pop, d.x, d.y - 30, 0, 3, Loc.T("🛰 관문 붕괴! ") + Orbits[next].name + Loc.T(" 항로 · 열쇠 +1 · +") + Math.Round(bonus));
            Emit(SwEv.Act, 0, 0, 1, 0, GateName + Loc.T(" 붕괴 — ") + Orbits[next].name + Loc.T(" 항로"));
            AddNews(null, GateName + Loc.T(" 붕괴 — 민간 청소선이 해냈다"), Orbits[S.orbit].name + Loc.T(" 궤도를 막고 있던 ") + GateName + Loc.T("이(가) 부서졌다. ") + Orbits[next].name + Loc.T(" 항로가 열렸다."));
        }
        public static double PermitCost(int orbit) => orbit <= 0 ? 0 : Nodes[NodeIx[PlanetNode[orbit]]].first;   // 항로 값은 트리 칸 값 하나 — Orbits.permit 은 표시에 섞여 실제 값과 달랐다 (09-25 밸런스)
        public bool BuyPermit(int i)
        {
            if (!R.over || !OnSale(i) || S.cash < PermitCost(i)) return false;
            return BuyTile(NodeIx[PlanetNode[i]]);
        }
        void PlanetBought(int i)
        {
            S.planets |= 1 << i;
            // 🎬 막 전환 (09-24 레벨 설계) — 목성 = 2막 외행성 · 해왕성 = 3막 심우주
            if (i == 3) { Emit(SwEv.Act, 0, 0, 2, 0, Loc.T("2막 · 외행성")); AddNews(null, Loc.T("외행성 면허 발급 — 청소선, 목성 너머로"), Loc.T("궤도청이 외행성 청소 면허를 내줬다. 정비고 바깥 고리가 열렸다는 소문이다.")); }
            if (i == 7) { Emit(SwEv.Act, 0, 0, 3, 0, Loc.T("3막 · 심우주")); AddNews(null, Loc.T("심우주 진입 — 해왕성 궤도에 민간 청소선"), Loc.T("태양이 점처럼 보이는 곳까지 왔다. 마지막 청구서가 기다린다.")); }
            AddNews(null, Orbits[i].name + Loc.T(" 청소 허가 — 민간 청소선 첫 진입"), Loc.T("케슬러 금융이 ") + Orbits[i].name + Loc.T(" 궤도 청소 허가증을 내줬다. ") + Orbits[i].desc + Loc.T(". 값은 지구의 ") + PlanetMulOf(i).ToString("N0") + Loc.T("배라고 한다."));
            S.orbit = i; RollContract(); Preview();
            if (Mk != null && StockOpen) { string[] sec = { "", Loc.T("달"), Loc.T("화성"), Loc.T("목성"), Loc.T("관광"), Loc.T("화성"), Loc.T("목성"), Loc.T("관광"), Loc.T("관광"), Loc.T("관광"), Loc.T("관광"), Loc.T("관광") }; Mk.GameEvent(Loc.T("민간 청소선 ") + Orbits[i].name + Loc.T(" 진출"), Loc.T("궤도 청소부가 ") + Orbits[i].name + Loc.T(" 청소 허가를 땄다. 관련 업계가 들썩인다."), new[] { sec[i], "ship" }, null, 0.14f); }
        }
    }
}
