using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    /// <summary>
    /// 🪐 행성마다 한 가지 (09-26 사장님 「전부 해봐」 · 시안 https://claude.ai/artifact/C3vxMVRQugPERTdYJdriBe)
    /// 그 행성에서만 나오는 것 하나 + 작은 규칙 하나. 쓰레기의 <c>sig</c> 가 특성 번호, <c>grp</c> 가 무리 번호.
    /// 1 지구 위성 줄 · 2 달 월면 금고 · 3 화성 폭풍 속 탐사차 · 4 소행성대 광맥 소행성 · 5 목성 대적점
    /// 6 토성 고리 얼음 덩이 · 7 천왕성 결정 공명 · 8 해왕성 돌풍 · 9 카이퍼 혜성 통과
    /// </summary>
    public sealed partial class SweepSim
    {
        public static readonly string[] TraitName = { "", "위성 줄", "월면 금고", "폭풍 속 탐사차", "광맥 소행성", "대적점", "고리 얼음 덩이", "결정 공명", "돌풍", "혜성 통과" };
        public static readonly string[] TraitDesc =
        {
            "",
            "줄지어 도는 위성 — 하나를 부수면 줄 전체가 차례로 터진다",
            "느린 큰 금고 — 값 ×3, 부서지며 둘레 조각까지 부순다",
            "폭풍 동안만 나타나는 탐사차 — 값 ×5, 폭풍이 끝나면 묻힌다",
            "큰 광맥 소행성 — 맞을 때마다 광석을 흘린다",
            "붉은 소용돌이 — 안의 잔해 값 ×2, 대신 천천히 빨려 든다",
            "고리 얼음 덩이 — 부수면 둘레를 얼린다(한 방에 깨짐)",
            "결정 공명 — 결정 하나를 부수면 같은 결정이 모두 깨진다",
            "돌풍 — 가끔 잔해가 한쪽으로 쏠린다",
            "혜성 통과 — 꼬리에 닿은 잔해가 부서지고, 머리를 맞히면 열쇠",
        };
        public static int TraitOf(int orbit) { switch (orbit) { case 0: return 1; case 1: return 2; case 2: return 3; case 5: return 4; case 3: return 5; case 4: return 6; case 6: return 7; case 7: return 8; case 8: return 9; default: return 0; } }
        public int Trait => R == null || R.clean ? 0 : TraitOf(S.orbit);

        // 🔴 목성 대적점 — 화면이 읽는다
        public const double SpotR = 70;
        double SpotRr => (Orbits[S.orbit].bi + Bo) / 2;
        public bool SpotOn => R != null && !R.over && Trait == 5;
        public double SpotX => EX + Math.Cos(R.spotA) * SpotRr;
        public double SpotY => EY + Math.Sin(R.spotA) * SpotRr * Tilt;
        bool InSpot(Junk d) { if (!SpotOn) return false; double dx = d.x - SpotX, dy = (d.y - SpotY) / Tilt; return dx * dx + dy * dy < SpotR * SpotR; }
        // 🌬 해왕성 돌풍
        public bool GustOn => R != null && !R.over && Trait == 8 && R.gustLeft > 0;
        public double GustA => R.gustA;
        // ☄ 카이퍼 혜성
        public Junk Comet => R != null && R.comet != null && !R.comet.dead ? R.comet : null;

        static int SpcIx(string art) { for (int i = 0; i < Spc.Length; i++) if (Spc[i].art == art) return i; return -1; }

        void TraitStart()
        {
            var r = R; r.sigT = 7; r.spotA = Rnd(0, Math.PI * 2); r.gustT = 12;
        }

        void TraitTick(double dt)
        {
            var r = R; int tr = Trait; if (tr == 0) return;
            // 차례로 터지기 (위성 줄 · 결정 공명)
            for (int i = r.sigKills.Count - 1; i >= 0; i--)
            {
                var sk = r.sigKills[i]; sk.t -= dt; if (sk.t > 0) continue;
                r.sigKills.RemoveAt(i);
                if (sk.j.dead) continue;
                if (sk.from != null) Emit(SwEv.Bolt, sk.from.x, sk.from.y, 1, 1, null, sk.j.x, sk.j.y);   // v 1 — 포구로 옮기지 않는다
                Kill(sk.j, 2, 1);
            }
            // 나타나기
            r.sigT -= dt;
            if (r.sigT <= 0 && tr != 3 && tr != 5 && tr != 8) { r.sigT = Rnd(17, 23); SpawnTrait(tr); }
            switch (tr)
            {
                case 3:   // 화성 — 폭풍 동안만
                    if (StormOn && !r.roverUp) { r.roverUp = true; SpawnTrait(3); }
                    if (!StormOn && r.roverUp)
                    {
                        r.roverUp = false;
                        foreach (var d in r.junk) if (!d.dead && d.sig == 3) { d.dead = true; Emit(SwEv.Pop, d.x, d.y, 0, 3, "탐사차가 모래에 묻혔다"); }
                    }
                    break;
                case 5:   // 목성 — 대적점이 궤도를 따라 돌고, 안의 잔해를 끌어들인다
                {
                    r.spotA += 0.12 * 0.9 * Orbits[S.orbit].spin * dt;
                    double sx = SpotX, sy = SpotY, srr = SpotRr;
                    foreach (var d in r.junk)
                    {
                        if (d.dead || d.free || Types[d.k].big) continue;
                        double dx = d.x - sx, dy = (d.y - sy) / Tilt, dd = dx * dx + dy * dy;
                        if (dd > SpotR * SpotR) continue;
                        double da = Math.Atan2(Math.Sin(r.spotA - d.a), Math.Cos(r.spotA - d.a));
                        d.a += Math.Sign(da) * Math.Min(Math.Abs(da), 0.22 * dt);
                        d.rr += (srr - d.rr) * Math.Min(1, 0.9 * dt);
                        if (dd < 11 * 11) { d.dead = true; r.spotEaten++; }                     // 빨려 들어가 사라진다 (돈 없음)
                    }
                    break;
                }
                case 8:   // 해왕성 — 돌풍
                    if (r.gustLeft > 0)
                    {
                        r.gustLeft -= dt;
                        foreach (var d in r.junk)
                        {
                            if (d.dead || d.free || d.frz > 0) continue;
                            double da = Math.Atan2(Math.Sin(r.gustA - d.a), Math.Cos(r.gustA - d.a));
                            d.a += Math.Sign(da) * Math.Min(Math.Abs(da), 0.55 * dt * (Types[d.k].heavy ? 0.5 : 1));
                        }
                    }
                    else if ((r.gustT -= dt) <= 0)
                    {
                        r.gustT = Rnd(16, 22); r.gustLeft = 3.5; r.gustA = Rnd(0, Math.PI * 2);
                        Emit(SwEv.Pop, EX, EY, 0, 3, "★ 돌풍 — 잔해가 한쪽으로 쏠린다");
                        Emit(SwEv.TraitFx, EX + Math.Cos(r.gustA) * SpotRr, EY + Math.Sin(r.gustA) * SpotRr * Tilt, r.gustA, 8);
                    }
                    break;
                case 9:   // 카이퍼 — 혜성이 지나가며 꼬리에 닿은 것을 부순다
                {
                    var c = Comet;
                    if (c != null)
                    {
                        if (c.x < -80 || c.x > 1040 || c.y < -80 || c.y > 700) { c.dead = true; r.comet = null; break; }
                        var hit = new List<Junk>();                                      // 모은 뒤 부순다 — Kill 이 파편을 만들면 목록이 바뀐다
                        foreach (var d in r.junk)
                        {
                            if (d.dead || d == c) continue;
                            double dx = d.x - c.x, dy = d.y - c.y;
                            if (dx * dx + dy * dy < 30 * 30) hit.Add(d);
                        }
                        foreach (var d in hit) Kill(d, 2, 1);
                    }
                    break;
                }
            }
        }

        void SpawnTrait(int tr)
        {
            var r = R; var o = Orbits[S.orbit]; double mid = (o.bi + Bo) / 2, a0 = Rnd(0, Math.PI * 2);
            switch (tr)
            {
                case 1: { int g = ++r.grpId; int sp = SpcIx("junk_sat_comm"); for (int i = 0; i < 6; i++) { var d = Spawn(Sat, a0 + i * 0.07, mid, Att.None, false, 1); d.sig = 1; d.grp = g; if (sp >= 0) d.sp = sp; } break; }
                case 2: { var d = Spawn(Vault, a0, mid, Att.None, false, 0.5); d.sig = 2; d.hp = d.max = d.max * 3; d.sp = SpcIx("junk_sig_moonvault"); break; }
                case 3: { var d = Spawn(Sat, a0, mid, Att.None, false, 1.5); d.sig = 3; d.sp = SpcIx("junk_sig_rover"); break; }
                case 4: { var d = Spawn(Big, a0, mid, Att.None, false, 0.6); d.sig = 4; d.hp = d.max = d.max * 3; d.sp = SpcIx("junk_sig_ore"); break; }
                case 6: { int sp = SpcIx("junk_sig_ice"); for (int i = 0; i < 2; i++) { var d = Spawn(Rocket, a0 + i * Math.PI, mid, Att.None, false, 1); d.sig = 6; d.sp = sp; } break; }
                case 7: { int g = ++r.grpId, sp = SpcIx("junk_crystal"); for (int i = 0; i < 6; i++) { var d = Spawn(Chip, Rnd(0, Math.PI * 2), -1, Att.None, false); d.sig = 7; d.grp = g; d.hp = d.max = Math.Max(2, d.max * 2); if (sp >= 0) d.sp = sp; } break; }
                case 9:
                {
                    bool left = Rnd() < 0.5; double y0 = EY + Rnd(-110, 110);
                    var d = SpawnFree(Big, left ? -40 : 1000, y0, (left ? 1 : -1) * Rnd(300, 360), Rnd(-35, 35), 999);
                    d.sig = 9; d.sp = SpcIx("junk_comet"); d.hp = d.max = Math.Max(3, d.max / 2); r.comet = d;
                    break;
                }
            }
            Emit(SwEv.Pop, EX, EY, 0, 3, "★ " + TraitName[tr]);                        // 알림줄
        }

        // 값 배율 — 부술 때 (Kill 이 부른다)
        double TraitMult(Junk d)
        {
            double m = d.sig == 2 ? 3 : d.sig == 3 ? 5 : d.sig == 9 ? 4 : 1;
            if (InSpot(d)) m *= 2;
            return m;
        }

        // 부순 뒤 (Kill 이 부른다)
        void TraitOnKill(Junk d)
        {
            var r = R;
            switch (d.sig)
            {
                case 1:   // 위성 줄 — 가까운 순서로 차례로
                {
                    var list = new List<Junk>(); foreach (var q in r.junk) if (!q.dead && q.sig == 1 && q.grp == d.grp) list.Add(q);
                    list.Sort((p, q) => Math.Abs(p.a - d.a).CompareTo(Math.Abs(q.a - d.a)));
                    var prev = d; for (int i = 0; i < list.Count; i++) { list[i].sig = 10; r.sigKills.Add(new SigKill { j = list[i], from = prev, t = 0.08 * (i + 1) }); prev = list[i]; }
                    break;
                }
                case 7:   // 결정 공명 — 같은 무리 결정이 모두
                {
                    int i = 0; foreach (var q in r.junk) if (!q.dead && q.sig == 7 && q.grp == d.grp) { q.sig = 10; r.sigKills.Add(new SigKill { j = q, from = d, t = 0.05 * (++i) }); }
                    if (i > 0) Emit(SwEv.Pop, d.x, d.y - 14, 0, 5, "결정 공명 ×" + (i + 1));
                    break;
                }
                case 2:   // 월면 금고 — 금화가 튀며 둘레 조각까지
                    r.pend.Add(new Blast { x = d.x, y = d.y, t = 0.05, R = 80, w = true });
                    Emit(SwEv.TraitFx, d.x, d.y, 80, 2);
                    break;
                case 6:   // 고리 얼음 덩이 — 둘레를 얼린다 · 언 것은 한 방에
                    foreach (var q in r.junk)
                    {
                        if (q.dead || q == d) continue;
                        double dx = q.x - d.x, dy = q.y - d.y; if (dx * dx + dy * dy > 95 * 95) continue;
                        q.frz = Math.Max(q.frz, 3); if (!Types[q.k].big) q.hp = Math.Min(q.hp, 1);
                    }
                    Emit(SwEv.TraitFx, d.x, d.y, 95, 6);
                    break;
                case 9:   // 혜성 머리 — 열쇠 (가끔 돈 뭉치)
                    r.comet = null;
                    if (Rnd() < 0.35) { S.keys++; Emit(SwEv.Pop, d.x, d.y - 14, 0, 5, "혜성에서 열쇠 +1!"); }
                    else { double cash = ShopBase * 0.3; S.cash += cash; Emit(SwEv.Pop, d.x, d.y - 14, 0, 4, "혜성 조각 +" + Math.Round(cash).ToString("N0")); }
                    break;
            }
        }

        // 맞을 때 (Hit 이 부른다) — 광맥 소행성은 맞을 때마다 광석을 흘린다
        void TraitOnHit(Junk d)
        {
            if (d.sig != 4 || d.dead || d.sigCd > 0) return;
            d.sigCd = 0.18;
            int sp = SpcIx("junk_ore");
            var q = SpawnFree(Chip, d.x, d.y, Rnd(-90, 90), Rnd(-90, 90), 1.6); if (sp >= 0) q.sp = sp;
        }
    }

    public class SigKill { public Junk j, from; public double t; }
}
