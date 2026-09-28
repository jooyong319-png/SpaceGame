using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // ⚔ 무기 · 빔 · 맞음 · 부서짐 · 돈 받기 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ───────────────────────── ⚔ 무기 여섯 더 (09-24 설계서 4단계) — 진공 청소기 · 기뢰 · 냉동 빔 · 분열탄 · 자석 펄스 · 레일건
        public static readonly double[] FireRate = { 1, 0.25, 1, 0.25, 2.2, 0.25, 1.6, 3, 3.2 };   // 무기마다 쏘는 간격 (Gap 배수)
        double vacMul = 1;                                                   // 청소기로 부순 것 — 값이 더 붙는다

        // 🌀 진공 청소기 — 청소선에서 부채꼴로 빨아들인다 (작은 것 떼에 강함 · 큰 것은 못 삼킨다)
        // 🌀 청소기 — 조준점에 소용돌이 원. 원 안의 것이 가운데로 빨려 들며 부서진다, 가운데 닿으면 흡수 (09-24 사장님 「원 영역 그리고 그 부분이 흡수되게」)
        int We(string w) => Lv("w_" + w + "_e");                            // ◇ 무기 특화 단계
        public double VacR => (46 + 0.8 * ClawR) * (Lv("w_vac_u") >= 1 ? 1.3 : 1) * (1 + 0.2 * We("vac"));
        void Vac()
        {
            var r = R; int u = Lv("w_vac_u"); bool awk = Lv("w_vac_a") > 0;
            double cx = r.ax, cy = r.ay, rad = VacR, per = Pow * 0.45;
            bool any = false;
            vacMul = (u >= 2 ? 1.6 : 1.3) + 0.2 * We("vac");
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead || Types[d.k].big) continue;
                double dx = cx - d.x, dy = cy - d.y, dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > rad + Types[d.k].r) continue;
                if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                d.capT = 0.5; d.vx += dx * 1.2; d.vy += dy * 1.2;                     // 가운데로 끌려온다
                int dmg = RoundP(per * (dist < 16 ? 4 : 1)); if (dmg <= 0) continue;   // 가운데 닿으면 흡수
                bool was = d.dead; Hit(d, dmg, 0, true); any = true;
                if (!was && d.dead && awk) { r.vacAmmo++; if (r.vacAmmo >= 25) { r.vacAmmo = 0; r.pend.Add(new Blast { wid = 3, x = cx, y = cy, t = 0.25, R = 70, w = true }); Emit(SwEv.Bolt, ShipX, ShipY, 0, 1, null, cx, cy); Emit(SwEv.Pop, cx, cy - 20, 0, 3, Loc.T("압축 고철탄!")); } }
            }
            vacMul = 1;
            Emit(SwEv.Vac, cx, cy, rad, 0);
            if (any) OnHit(0.25);
        }

        // 💣 기뢰 — 조준 자리에 깔아 둔다. 궤도를 돌던 잔해가 지나가면 쾅 (무기 폭발이라 번질 수 있다)
        void MineLay()
        {
            var r = R; int u = Lv("w_mine_u");
            int cap = 3 + (u >= 1 ? 2 : 0) + We("mine");
            if (r.mines.Count >= cap) r.mines.RemoveAt(0);
            r.mines.Add(new Blast { x = r.ax, y = r.ay, t = 0.4, R = (45 + 0.25 * ClawR) * (u >= 2 ? 1.4 : 1) * (1 + 0.15 * We("mine")) });
            Emit(SwEv.Ring, r.ax, r.ay, 18, 3);
        }
        void Mines(double dt)
        {
            var r = R; if (r.mines.Count == 0) return;
            bool awk = Lv("w_mine_a") > 0;
            for (int i = r.mines.Count - 1; i >= 0; i--)
            {
                var m = r.mines[i];
                if (m.t > 0) { m.t -= dt; continue; }
                bool boom = false;
                foreach (var d in r.junk) { if (d.dead || d.fade < 0.35) continue; double dx = d.x - m.x, dy = d.y - m.y; if (dx * dx + dy * dy < 18 * 18) { boom = true; break; } }
                if (!boom) continue;
                r.mines.RemoveAt(i);
                r.pend.Add(new Blast { wid = 4, x = m.x, y = m.y, t = 0.01, R = m.R, w = true });
                OnHit(1);
            }
            // 각성 — 기뢰끼리 레이저 울타리 (지나가는 것을 태운다)
            if (awk && r.mines.Count >= 2)
            {
                r.fenceT -= dt; if (r.fenceT > 0) return; r.fenceT = 0.2;
                for (int i = 0; i + 1 < r.mines.Count; i++)
                {
                    var a = r.mines[i]; var b = r.mines[i + 1];
                    double ux = b.x - a.x, uy = b.y - a.y, L = Math.Sqrt(ux * ux + uy * uy); if (L < 1) continue; ux /= L; uy /= L;
                    for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
                    {
                        var d = r.junk[ji]; if (d.dead) continue;
                        double px = d.x - a.x, py = d.y - a.y, t = px * ux + py * uy; if (t < 0 || t > L) continue;
                        if (Math.Abs(px * uy - py * ux) > 8 + Types[d.k].r) continue;
                        Hit(d, Math.Max(1, RoundP(Pow * 0.5)), 0, true);
                    }
                    Emit(SwEv.Laser, a.x, a.y, 3, 16, null, b.x, b.y);
                }
            }
        }

        // ❄ 냉동 빔 — 맞은 것이 얼어 멈춘다. 언 것은 무엇에 맞든 두 배로 아프다. 부서지면 산산조각
        // ❄ 냉동 빔 — 빔은 조준점까지, 거기서 서리 원이 퍼진다. 원 안이 언다 (09-24 「얼음은 좀 이상해」 — 한 줄 전체가 얼던 것을 원으로)
        public double FrzR => (34 + 0.5 * ClawR) * (Lv("w_frz_u") >= 1 ? 1.3 : 1) * (1 + 0.2 * We("frz"));
        void Freeze()
        {
            var r = R; int u = Lv("w_frz_u");
            double sx = ShipX, sy = ShipY, cx = r.ax, cy = r.ay, rad = FrzR;
            bool any = false;
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead) continue;
                double dx = d.x - cx, dy = d.y - cy; if (dx * dx + dy * dy > (rad + Types[d.k].r) * (rad + Types[d.k].r)) continue;
                bool fresh = d.frz <= 0;
                d.frz = (u >= 1 ? 4 : 2.5) + 0.5 * We("frz"); any = true;
                if (fresh) Emit(SwEv.Shatter, d.x, d.y, 0, 1);
                int dmg = RoundP(Pow * 0.12); if (dmg > 0) Hit(d, dmg, 0, true);
            }
            Emit(SwEv.Laser, sx, sy, rad, 32 | 64, null, cx, cy);
            if (any) OnHit(0.25);
        }
        public double FrzMul => Lv("w_frz_u") >= 2 ? 2.5 : 2;

        // ❄ 냉동선 — 무기고 냉동 빔을 매번 쏜다. 한 방은 약하고, 냉동 빔 줄(강화 · 각성 · 특화 · 3단계)이 그대로 세게 한다 (09-27)
        public static double FrostGap = 0.3, FrostK = 0.42, FrostGateK = 0.9, FrostHold = 1.0;
        public double FrostR => (44 + 6 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2)) * (Lv("w_frz_u") >= 1 ? 1.3 : 1) * (1 + 0.2 * We("frz"));
        void FrostShot()
        {
            var r = R; double cx = r.ax, cy = r.ay, rad = FrostR, hold = FrostHold + (Lv("w_frz_u") >= 1 ? 0.5 : 0) + 0.25 * We("frz");
            bool crit = Rnd() < Crit, any = false; int dmg = Math.Max(1, RoundP(Pow * FrostK * (Lv("w_frz_x") > 0 ? 2 : 1) * (crit ? CritX : 1)));
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead) continue;
                double dx = d.x - cx, dy = d.y - cy; if (dx * dx + dy * dy > (rad + Types[d.k].r) * (rad + Types[d.k].r)) continue;
                any = true; Hit(d, dmg, 0, true);                                   // 치고 나서 얼린다 — 다음 방부터 두 배
                if (!d.dead) { if (d.frz <= 0) Emit(SwEv.Shatter, d.x, d.y, 0, 1); d.frz = Math.Max(d.frz, hold); }
            }
            Emit(SwEv.Laser, ShipX, ShipY, rad, 32 | 64, null, cx, cy);
            Emit(SwEv.Strike, cx, cy, rad, any ? 1 : 0);
            if (any && crit) Emit(SwEv.Crit, cx, cy - rad - 8);
            if (any) OnHit(1);
        }

        // 🎆 분열탄선 — 무기고 분열탄을 매번 쏜다. 포탄이 떨어져 터지고 파편 넷 (분열탄 줄이 그대로 세게 한다 · 09-27)
        public static double ClusGap = 0.45, ClusK = 0.8, ClusFragK = 0.5, ClusGateK = 1.0;
        public int ClusN => 4 + Lv("c_rad") / 3 + (Lv("w_clus_u") >= 1 ? 2 : 0) + We("clus");
        void ClusShot()
        {
            var r = R; int u = Lv("w_clus_u"); bool awk = Lv("w_clus_a") > 0; double xk = Lv("w_clus_x") > 0 ? 2 : 1;
            double ax = r.ax, ay = r.ay, R0 = (26 + 5 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2));
            Emit(SwEv.Shell, ShipX, ShipY, 0, 0, null, ax, ay);
            r.pend.Add(new Blast { x = ax, y = ay, t = 0.35, R = R0, dk = ClusK * xk });
            int n = ClusN;
            var targets = new List<Junk>();
            if (u >= 2) { foreach (var d in r.junk) if (!d.dead) { double dx = d.x - ax, dy = d.y - ay; if (dx * dx + dy * dy < 130 * 130) targets.Add(d); } }
            for (int k = 0; k < n; k++)
            {
                double fx, fy;
                if (u >= 2 && targets.Count > 0) { var t = targets[rng.Next(targets.Count)]; fx = t.x; fy = t.y; }
                else { double a = k * Math.PI * 2 / n + Rnd(-0.3, 0.3), dd = Rnd(40, 85); fx = ax + Math.Cos(a) * dd; fy = ay + Math.Sin(a) * dd * Tilt; }
                double t0 = 0.47 + k * 0.03;
                r.pend.Add(new Blast { x = fx, y = fy, t = t0, R = 16, dk = ClusFragK * xk });
                Emit(SwEv.Shell, ax, ay, t0, 1, null, fx, fy);
                if (awk) for (int j = 0; j < 2; j++) r.pend.Add(new Blast { x = fx + Rnd(-28, 28), y = fy + Rnd(-20, 20), t = t0 + 0.16 + j * 0.03, R = 11, dk = ClusFragK * 0.5 * xk });
            }
            Emit(SwEv.Strike, ax, ay, R0, 1);
            OnHit(0.5);
        }
        void ShellBurst(double x, double y, double Rb, double dk)
        {
            Emit(SwEv.Blast, x, y, Rb, 1);                                      // k 1 = 맞는 범위 그대로 그린다
            bool crit = Rnd() < Crit; int dmg = Math.Max(1, RoundP(Pow * dk * (crit ? CritX : 1)));
            var list = R.junk;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i]; if (d.dead) continue;
                double dx = d.x - x, dy = d.y - y, rr = Rb + Types[d.k].r;
                if (dx * dx + dy * dy > rr * rr) continue;
                Hit(d, dmg, 0, true);
            }
            if (crit) Emit(SwEv.Crit, x, y - Rb - 8);
        }
        void Shatter(Junk d)                                                  // 언 것이 부서질 때 — 옆을 친다 (각성: 옆도 얼린다)
        {
            bool awk = Lv("w_frz_a") > 0;
            for (int ji = 0, jn = R.junk.Count; ji < jn && ji < R.junk.Count; ji++)
            {
                var q = R.junk[ji]; if (q.dead || q == d) continue;
                double dx = q.x - d.x, dy = q.y - d.y; if (dx * dx + dy * dy > 34 * 34) continue;
                if (awk && q.frz <= 0) q.frz = 2;
                if (q.sig == GateSig && !GateReady) continue;
                { int sd = Math.Max(1, RoundP(Pow * 0.6)); if (R != null) { double a = Math.Min(sd, Math.Max(0, q.hp)); R.dmgBy[5] += a; DbgCat[5] += a; } q.hp -= sd; }
                q.hit = 0.12; if (q.hp <= 0) { int sv = dmgCat; dmgCat = 5; Kill(q, 0, 1); dmgCat = sv; }
            }
            Emit(SwEv.Shatter, d.x, d.y, 0, 2);
        }

        // 🎆 분열탄 — 조준점에 떨어져 터지며 파편 여섯 (강화: 아홉 · 가장 가까운 것을 노림 · 각성: 파편이 한 번 더)
        void Cluster()
        {
            var r = R; int u = Lv("w_clus_u"); bool awk = Lv("w_clus_a") > 0;
            double ax = r.ax, ay = r.ay;
            Emit(SwEv.Shell, ShipX, ShipY, 0, 0, null, ax, ay);
            r.pend.Add(new Blast { wid = 6, x = ax, y = ay, t = 0.35, R = 42 + 0.2 * ClawR, w = true });
            int n = 6 + (u >= 1 ? 3 : 0) + 2 * We("clus");
            var targets = new List<Junk>();
            if (u >= 2) { foreach (var d in r.junk) if (!d.dead) { double dx = d.x - ax, dy = d.y - ay; if (dx * dx + dy * dy < 150 * 150) targets.Add(d); } }
            for (int k = 0; k < n; k++)
            {
                double fx, fy;
                if (u >= 2 && targets.Count > 0) { var t = targets[rng.Next(targets.Count)]; fx = t.x; fy = t.y; }
                else { double a = k * Math.PI * 2 / n + Rnd(-0.2, 0.2), d = Rnd(55, 110); fx = ax + Math.Cos(a) * d; fy = ay + Math.Sin(a) * d * Tilt; }
                double t0 = 0.5 + k * 0.04;
                r.pend.Add(new Blast { wid = 6, x = fx, y = fy, t = t0, R = 22, w = true });
                Emit(SwEv.Shell, ax, ay, t0, 1, null, fx, fy);                       // 🚀 자탄 — 화면만 (터진 자리 → 파편 자리, t0에 떨어짐)
                if (awk) for (int j = 0; j < 3; j++) r.pend.Add(new Blast { wid = 6, x = fx + Rnd(-35, 35), y = fy + Rnd(-25, 25), t = t0 + 0.18 + j * 0.03, R = 14, w = true });
            }
            OnHit(1);
        }

        // 🧲 자석 펄스 — 주변을 한 점으로 끌어모은 뒤 쾅 (강화: 넓게 · 모인 만큼 크게 · 각성: 모인 자리에 블랙홀)
        void MagPulse()
        {
            var r = R; int u = Lv("w_mag_u"); bool awk = Lv("w_mag_a") > 0;
            double cx = r.ax, cy = r.ay, Rr = (110 + 0.4 * ClawR) * (u >= 1 ? 1.4 : 1) * (1 + 0.2 * We("mag"));
            int n = 0;
            foreach (var d in r.junk)
            {
                if (d.dead || Types[d.k].big) continue;
                double dx = cx - d.x, dy = cy - d.y; if (dx * dx + dy * dy > Rr * Rr) continue;
                if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                d.vx = dx * 1.9; d.vy = dy * 1.9; d.capT = 1.1; n++;
            }
            Emit(SwEv.Ring, cx, cy, Rr, 2);
            r.pend.Add(new Blast { wid = 7, x = cx, y = cy, t = 0.55, R = 50 + (u >= 2 ? Math.Min(60, n * 1.5) : 0), w = true });
            if (awk && !r.holding) { r.magHole = 0.6; r.magX = cx; r.magY = cy; }
            OnHit(1);
        }

        // ⚡ 레일건 — 모았다가 한 줄로 관통. 엄청 세다 · 장갑도 뚫는다 (강화: 빨리 모음 · 뚫을수록 세짐 · 각성: 튕겨 한 번 더)
        void Rail()
        {
            var r = R; int u = Lv("w_rail_u"); bool awk = Lv("w_rail_a") > 0;
            double sx = ShipX, sy = ShipY, dx0 = r.ax - sx, dy0 = r.ay - sy, L0 = Math.Sqrt(dx0 * dx0 + dy0 * dy0);
            if (L0 < 1) return;
            double ux = dx0 / L0, uy = dy0 / L0, len = L0 + 420 + 120 * We("rail");
            RailLine(sx, sy, ux, uy, len, u >= 2);
            if (awk || We("rail") >= 3) { double ex = sx + ux * (L0 + 40), ey = sy + uy * (L0 + 40), a = Math.Atan2(uy, ux) + Math.PI + Rnd(-0.6, 0.6); RailLine(ex, ey, Math.Cos(a), Math.Sin(a), 360, u >= 2); }
            OnHit(1);
        }
        void RailLine(double sx, double sy, double ux, double uy, double len, bool grow)
        {
            var r = R; double dmg = Pow * 6 * (Rnd() < Crit ? CritX : 1);
            var hit = new List<Junk>();
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)
            {
                var d = r.junk[ji]; if (d.dead) continue;
                double px = d.x - sx, py = d.y - sy, t = px * ux + py * uy; if (t < 0 || t > len) continue;
                if (Math.Abs(px * uy - py * ux) > 10 + Types[d.k].r) continue;
                hit.Add(d);
            }
            hit.Sort((a, b) => ((a.x - sx) * ux + (a.y - sy) * uy).CompareTo((b.x - sx) * ux + (b.y - sy) * uy));
            foreach (var d in hit) { pierce = true; Hit(d, Math.Max(1, (int)Math.Round(dmg)), 0, true); pierce = false; if (grow) dmg *= 1.15; }
            Emit(SwEv.Rail, sx, sy, hit.Count, 0, null, sx + ux * len, sy + uy * len);
        }

        void Claw(double dt)
        {
            var r = R;
            r.next -= dt;
            if (Lv("c_magnet") > 0)
            {
                double mr = Math.Max(ClawR, PickR) + 40 + 20 * Lv("c_magnet");
                foreach (var d in r.junk)
                {
                    if (d.dead || d.k != Chip) continue;
                    double dx = r.ax - d.x, dy = r.ay - d.y, dd = dx * dx + dy * dy;
                    if (dd > mr * mr || dd < 100) continue;
                    if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                    d.capT = 0.5; d.vx += dx * 2.5 * dt; d.vy += dy * 2.5 * dt;
                }
            }
            // 청소선 — 조준 방향 쪽 바깥 궤도로 (무기는 여기서 나간다)
            {
                double want = Math.Atan2((r.ay - EY) / Tilt, r.ax - EX), da = want - r.shipA;
                while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI;
                r.shipA += da * Math.Min(1, dt * 3);
            }
            if (r.clickCd > 0) r.clickCd -= dt;
            for (int cw = 1; cw <= 5; cw += 2)                                  // 이어서 쏘는 확률 효과 (레이저 1 · 청소기 3 · 냉동 5)
                if (r.chan[cw] > 0) { r.chan[cw] -= dt; r.chanNext[cw] -= dt; if (r.chanNext[cw] <= 0) { r.chanNext[cw] = Gap * 0.25; Emit(SwEv.Proc, r.chanX[cw], r.chanY[cw], cw, 2); FireAt(cw, r.chanX[cw], r.chanY[cw]); } }   // kk 2 = 이어 쏘기 (포대만)
            VolleyTick(dt);
            if (r.next > 0) return;
            if (ManualFire && !FireHeld) return;                               // 👆 수동 공격 — 누르고 있는 동안만 (09-26 사장님 「공격을 수동으로 바꿔 봐」)
            if (!TargetNear(r.ax, r.ay)) { r.next = 0.05; return; }         // ⛽ 조준점 근처에 잔해가 없으면 쉰다 — 연료도 안 닳는다
            double over = Lv("c_over") > 0 && r.fuel < 5 ? 0.5 : 1;
            r.next = Gap * over * Rate(0);                                    // 기본 공격 간격
            r.fuel = Math.Max(0, r.fuel - ShotFuel * r.next); r.lastShot = r.t;                 // ⛽ 쏠 때마다 — 간격에 비례해서 연사가 빨라도 초당 몫은 같다
            Fire(0); Procs();
            if (Rnd() < 0.1 * Lv("c_double") + Part("dbl")) { Fire(0); Procs(); }
        }
        // 👆 수동 사격 — 누를 때마다 조준점에 한 방 더 (위력 ×1.5 · 0.2초 간격). 무기 발동 · 전탄 게이지도 굴러간다 (09-24 사장님 37번 「클릭에 요소」)
        public void PressFire() { var r = R; if (r == null || r.over) return; r.tapCombo = r.t - r.lastPress <= 0.3 ? Math.Min(10, r.tapCombo + 1) : 0; r.lastPress = r.t; if (r.t - r.lastShot >= 0.12) r.next = 0; }   // 👆 누르는 순간 바로 쏜다 (너무 빠른 연타는 0.12초로 막음)
        public bool ManualFire, FireHeld = true;                               // 👆 수동 공격 (설정) · 지금 누르고 있나 — 게임이 매 프레임 넣는다. 봇 · 시험은 자동
        public bool ClickShot(double x, double y)
        {
            var r = R; if (r == null || r.over || r.clickCd > 0 || r.fuel <= 0 || ManualFire) return false;   // 수동 공격일 땐 누르는 게 곧 사격 — 따로 한 방은 없다
            r.clickCd = 0.3; double ox = r.ax, oy = r.ay; r.ax = x; r.ay = y;
            r.fuel = Math.Max(0, r.fuel - ClickFuel);                         // ⛽ 손으로 쏘면 한 방에 더
            clickMul = 1.3; Fire(0); clickMul = 1; Procs();
            r.ax = ox; r.ay = oy;
            return true;
        }
        double clickMul = 1;
        void Fire2(double dt)
        {
            var r = R; int w2 = S.weapon2;
            if (Lv("w_slot2") <= 0 || w2 < 0 || w2 == Weapon || !WeaponOwned(w2)) return;
            r.next2 -= dt; if (r.next2 > 0) return;
            r.next2 = Gap * 2 * Rate(w2);                                       // 보조 무기 — 절반 빠르기
            Fire(w2);
        }
        // 🔫 조종실 포구 — 무기마다 모양 · 개수가 다르다 (09-24 사장님 「조종선에서 쏜다」 · 시안 DbsvFEEy1K5ddbZsM61B2y)
        // 시안 화면(1280×720) 좌표로 (x, y, 포신 길이) 셋씩. 화면 아래 가운데 = (640, 720)
        public static readonly double[][] Mounts = {
            new double[] { 640, 654, 52 },                                                   // 집게 빔 — 큰 집게 포 하나
            new double[] { 598, 650, 74, 682, 650, 74 },                                     // 레이저 — 가는 장포신 둘 (선체 안쪽)
            new double[] { 640, 586, 0 },                                                    // 번개 — 테슬라 코일 탑
            new double[] { 640, 660, 50 },                                                   // 청소기 — 넓은 흡입구
            new double[] { 622, 640, 24, 658, 640, 24, 622, 664, 24, 658, 664, 24 },         // 기뢰 — 박격포 넷
            new double[] { 640, 654, 58 },                                                   // 냉동 빔 — 냉각 노즐
            PodMounts(),                                                                     // 분열탄 — 로켓 포드 열
            new double[] { 640, 656, 60 },                                                   // 자석 — 말굽
            new double[] { 640, 660, 108 },                                                  // 레일건 — 긴 레일
        };
        static double[] PodMounts() { var m = new List<double>(); for (int r = 0; r < 2; r++) for (int c = 0; c < 5; c++) { m.Add(596 + c * 22); m.Add(630 + r * 22); m.Add(9); } return m.ToArray(); }
        public double ViewHalf => Math.Min(7, Math.Max(3.8, Bo * 0.0145)) * 1.1;   // SweepGame 카메라 크기와 같은 식 (월드 단위, 화면 절반 높이) — 계기판 몫 10% 물림
        public const double TurS = 1.4;                                        // 포구 크기 (시안보다 1.4배 — 09-24 작아 보였다)
        public double MountK => 100 * ViewHalf / 720 * TurS;                   // 시안 1px → 시뮬 px
        public int MountCount(int w) => Mounts[Math.Min(w, Mounts.Length - 1)].Length / 3;
        public void MountPx(int w, int i, out double bx, out double by, out double tx, out double ty)
        {
            var m = Mounts[Math.Min(w, Mounts.Length - 1)]; i %= m.Length / 3; double k = MountK;
            bx = EX + (m[i * 3] - 640) * k; by = EY + ViewHalf * 1.1 * 50 - (720 - m[i * 3 + 1]) * k;   // 화면 아래 가운데 기준으로 TurS배   // 카메라가 0.1 내려가 있다
            double L = m[i * 3 + 2] * k, dx = (R != null ? R.ax : EX) - bx, dy = (R != null ? R.ay : EY) - by, d = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
            tx = bx + dx / d * L; ty = by + dy / d * L;
        }
        public double ShipX { get { MountPx(Weapon, 0, out _, out _, out var x, out _); return x; } }
        public double ShipY { get { MountPx(Weapon, 0, out _, out _, out _, out var y); return y; } }
        void Fire(int w) { switch (w) { case 1: Laser(); break; case 2: Chain(); break; case 3: Vac(); break; case 4: MineLay(); break; case 5: Freeze(); break; case 6: Cluster(); break; case 7: MagPulse(); break; case 8: Rail(); break; default: Strike(); break; } }
        double Rate(int w) => FireRate[Math.Min(w, FireRate.Length - 1)] * (w == 8 && Lv("w_rail_u") >= 1 ? 0.75 : 1);
        bool pierce;                                                       // 레이저 각성 — 장갑판 한도 무시

        // 🔴 레이저 — 청소선에서 조준점 너머까지. 네 배 자주 · 한 번은 화력의 0.3 (소수는 확률로)
        // 🔴 레이저 — 빔은 조준점에서 멈추고 끝의 작은 원만 태운다 (09-24 사장님: 화면 끝까지 한 줄로 쓸던 것이 말이 안 됐다)
        public double LaserR => (12 + 0.25 * Math.Max(ClawR, PickR * 0.6)) * (Lv("w_laser_u") >= 1 ? 1.4 : 1) * (Lv("w_laser_a") > 0 ? 1.5 : 1) * (1 + 0.2 * We("laser"));
        void Laser()
        {
            var r = R;
            int u = Lv("w_laser_u"); bool awk = Lv("w_laser_a") > 0;
            double sx = ShipX, sy = ShipY, cx = r.ax, cy = r.ay, rad = LaserR;
            bool crit = Rnd() < Crit, any = false;
            double per = Pow * 0.36 * (u >= 2 ? r.heat : 1) * (crit ? CritX : 1) * (1 + 0.15 * We("laser"));
            for (int ji = 0, jn = r.junk.Count; ji < jn && ji < r.junk.Count; ji++)   // 부서지며 조각이 새로 붙어도 괜찮게 (번호로 돈다)
            {
                var d = r.junk[ji];
                if (d.dead) continue;
                double dx = d.x - cx, dy = d.y - cy, rr = rad + Types[d.k].r;
                if (dx * dx + dy * dy > rr * rr) continue;
                int dmg = (int)per + (Rnd() < per - (int)per ? 1 : 0);
                if (dmg <= 0) continue;
                any = true; pierce = awk; Hit(d, dmg, 0, true); pierce = false;
            }
            Emit(SwEv.Laser, sx, sy, rad, (crit ? 1 : 0) + (awk ? 2 : 0) + 64, null, cx, cy);
            r.heat = any ? Math.Min(1.8, r.heat + 0.04) : 1;
            if (any && We("laser") >= 3 && Rnd() < 0.25) r.pend.Add(new Blast { wid = 1, x = cx, y = cy, t = 0.05, R = rad * 1.4, w = true });   // ◇ 3단계 — 태운 자리가 터진다
            if (any) OnHit(0.25);
        }

        // ⚡ 번개 — 조준점 근처 하나를 치고 가까운 것으로 튄다
        void Chain()
        {
            var r = R;
            int u = Lv("w_chain_u"); bool awk = Lv("w_chain_a") > 0;
            double reach0 = 70 + 0.5 * ClawR, hop = 115 + 0.6 * ClawR;
            Junk cur = null; double bd = reach0 * reach0;
            foreach (var d in r.junk) { if (d.dead) continue; double dx = d.x - r.ax, dy = d.y - r.ay, dd = dx * dx + dy * dy; if (dd < bd) { bd = dd; cur = d; } }
            if (cur == null) { Emit(SwEv.Strike, r.ax, r.ay, PickR, 0); return; }
            bool crit = Rnd() < Crit;
            int jumps = 5 + (u >= 1 ? 2 : 0) + 2 * We("chain");
            double dmg = Pow * 1.3 * (crit ? CritX : 1);
            double px = ShipX, py = ShipY;
            var hit = new List<Junk>();
            for (int j = 0; j <= jumps && cur != null; j++)
            {
                hit.Add(cur);
                Emit(SwEv.Bolt, px, py, j, crit ? 1 : 0, null, cur.x, cur.y);
                px = cur.x; py = cur.y;
                Hit(cur, Math.Max(1, (int)Math.Round(dmg)), 0, true);
                if (awk && r.chain < ChainMax) r.pend.Add(new Blast { wid = 2, x = px, y = py, t = 0.05 + 0.03 * j, R = 30, w = true });
                if (u >= 2) dmg *= 1.2;
                Junk nx = null; double nd = hop * hop;
                foreach (var d in r.junk) { if (d.dead || hit.Contains(d)) continue; double dx = d.x - px, dy = d.y - py, dd = dx * dx + dy * dy; if (dd < nd) { nd = dd; nx = d; } }
                cur = nx;
            }
            if (crit) Emit(SwEv.Crit, r.ax, r.ay - 20);
            OnHit(1);
        }

        void Strike()
        {
            var r = R;
            if (Ship == 1) { Scatter(); return; }
            if (Ship == 2) { Harpoon(); return; }
            if (Ship == 3) { Tesla(); return; }
            if (Ship == 4) { MissileFire(); return; }
            if (Ship == 5) { FrostShot(); return; }
            if (Ship == 6) { ClusShot(); return; }
            if (ClawR <= 0)
            {
                // 범위가 없으면 커서 밑의 하나만
                Junk best = null; double bd = double.MaxValue;
                foreach (var d in r.junk)
                {
                    if (d.dead) continue;
                    double dx = d.x - r.ax, dy = d.y - r.ay, dd = dx * dx + dy * dy, lim = PickR + Types[d.k].r;
                    if (dd < lim * lim && dd < bd) { bd = dd; best = d; }
                }
                bool c1 = best != null && Rnd() < Crit;
                if (best != null) { Hit(best, Math.Max(1, RoundP(Pow * (c1 ? CritX : 1))), 0, true); OnHit(1); }
                Emit(SwEv.Strike, r.ax, r.ay, PickR, best != null ? 1 : 0);
                if (c1) Emit(SwEv.Crit, r.ax, r.ay - 20);
                return;
            }
            double R0 = ClawR; bool hit = false, crit = Rnd() < Crit; int dmg = Math.Max(1, RoundP(Pow * (crit ? CritX : 1)));
            var list = r.junk;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.dead) continue;
                double dx = d.x - r.ax, dy = d.y - r.ay;
                if (dx * dx + dy * dy > (R0 + Types[d.k].r) * (R0 + Types[d.k].r)) continue;
                hit = true; Hit(d, dmg, 0, true);
            }
            Emit(SwEv.Strike, r.ax, r.ay, R0, hit ? 1 : 0);
            if (hit && crit) Emit(SwEv.Crit, r.ax, r.ay - R0 - 8);
            if (hit) OnHit(1);
        }
        /// <summary>🚀 산탄선 — 처음부터 넓게, 가까울수록 세다 (09-26)</summary>
        void Scatter()
        {
            var r = R;
            double R0 = ScatterR, dx0 = r.ax - ShipX, dy0 = r.ay - ShipY, near = Math.Max(0.6, Math.Min(1.3, 1.4 - Math.Sqrt(dx0 * dx0 + dy0 * dy0) / 560));
            bool hit = false, crit = Rnd() < Crit; int dmg = Math.Max(1, RoundP(Pow * 0.7 * near * (crit ? CritX : 1)));
            var list = r.junk;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.dead) continue;
                double dx = d.x - r.ax, dy = d.y - r.ay;
                if (dx * dx + dy * dy > (R0 + Types[d.k].r) * (R0 + Types[d.k].r)) continue;
                hit = true; Hit(d, dmg, 0, true);
            }
            Emit(SwEv.Strike, r.ax, r.ay, R0, hit ? 1 : 0);
            if (hit && crit) Emit(SwEv.Crit, r.ax, r.ay - R0 - 8);
            if (hit) OnHit(1);
        }
        /// <summary>🚀 작살선 — 포구에서 조준 방향으로 한 줄 꿰뚫기. 뚫을 때마다 피해 −15% (09-26)</summary>
        public double HarpoonL => (340 + 30 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2));
        public double HarpoonW => 7 + 1.5 * Lv("c_rad");
        public int HarpoonN => 3 + Lv("c_rad");                                    // 한 번에 꿰뚫는 개수 (관문은 세지 않는다)
        public const double HarpoonDecay = 0.8;                                 // 뚫을 때마다 남는 피해 (봇으로 맞춤)
        void Harpoon()
        {
            var r = R;
            double sx = ShipX, sy = ShipY, dx = r.ax - sx, dy = r.ay - sy, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double ux = dx / len, uy = dy / len, L = HarpoonL, W = HarpoonW;
            var hits = new List<(double t, Junk d)>();
            foreach (var d in r.junk)
            {
                if (d.dead) continue;
                double px = d.x - sx, py = d.y - sy, t = px * ux + py * uy;
                if (t < 0 || t > L) continue;
                if (Math.Abs(px * uy - py * ux) > W + Types[d.k].r) continue;
                hits.Add((t, d));
            }
            hits.Sort((a, b) => a.t.CompareTo(b.t));
            bool crit = Rnd() < Crit;
            int n = 0;
            foreach (var h in hits)
            {
                bool gate = h.d.sig == GateSig;
                if (!gate && n >= HarpoonN) continue;
                Hit(h.d, Math.Max(1, RoundP(Pow * (gate ? 1 : Math.Pow(HarpoonDecay, n)) * (crit ? CritX : 1))), 0, true);   // 🛰 관문은 늘 제 위력
                if (!gate) n++;
            }
            Emit(SwEv.Strike, r.ax, r.ay, hits.Count, hits.Count > 0 ? 1 : 0, null, sx + ux * L, sy + uy * L);
            if (hits.Count > 0 && crit) Emit(SwEv.Crit, hits[0].d.x, hits[0].d.y - 20);
            if (hits.Count > 0) OnHit(1);
        }
        /// <summary>🚀 전격선 — 전기 구체를 던진다: 날아가며 번개로 지지고, 닿으면 머물다 터진다 (09-27)</summary>
        public const double TeslaGap = 0.9, OrbSpeed = 520, OrbHover = 0.5, ZapEvery = 0.15, ZapK = 0.2, BurstK = 1.0;
        public double ZapR => (70 + 10 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2));
        public int ZapN => 2 + Lv("c_rad") / 3;
        public double BurstR => (55 + 8 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2));
        void Tesla()
        {
            var r = R;
            double sx = ShipX, sy = ShipY, dx = r.ax - sx, dy = r.ay - sy, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            r.orbs.Add(new Orb { x = sx, y = sy, tx = r.ax, ty = r.ay, vx = dx / len * OrbSpeed, vy = dy / len * OrbSpeed, zap = 0.05 });
            Emit(SwEv.Strike, r.ax, r.ay, 0, 1);
        }
        /// <summary>🚀 미사일선 — 조준점 둘레 잔해를 골라 쫓는 작은 미사일 (09-27)</summary>
        public const double MissileGap = 0.25, MissileK = 0.8, MissileSpeed = 620;
        public double LockR => (140 + 10 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2));
        public int MissileN => 1 + Lv("c_rad") / 3;
        Junk LockOn(double x, double y, double rad)
        {
            var c = new List<(double dd, Junk d)>();
            foreach (var d in R.junk) { if (d.dead) continue; double ex = d.x - x, ey = d.y - y, dd = ex * ex + ey * ey; if (dd < rad * rad) c.Add((dd, d)); }
            if (c.Count == 0) return null;
            c.Sort((a, b) => a.dd.CompareTo(b.dd));
            foreach (var q in c) if (q.d.sig == GateSig && GateReady && q.dd < 90 * 90) return q.d;   // 🛰 조준점 가까이 관문이 있으면 관문부터 (흩어져서 관문이 안 깨졌다)
            return c[rng.Next(Math.Min(4, c.Count))].d;
        }
        public static long DbgMisF, DbgMisH, DbgMisCall;
        void MissileFire()
        {
            var r = R; bool any = false; DbgMisCall++;
            for (int k = 0; k < MissileN; k++)
            {
                var tg = LockOn(r.ax, r.ay, LockR); if (tg == null) break;
                DbgMisF++; r.missiles.Add(new Missile { x = ShipX, y = ShipY, vx = Rnd(-150, 150) + (k - MissileN / 2.0) * 80, vy = -260, tg = tg, life = 3 });
                any = true;
            }
            Emit(SwEv.Strike, r.ax, r.ay, 0, any ? 1 : 0);
        }
        void MissileTick(double dt)
        {
            var r = R;
            for (int i = r.missiles.Count - 1; i >= 0; i--)
            {
                var m = r.missiles[i]; m.life -= dt;
                if (m.tg == null || m.tg.dead) m.tg = LockOn(m.x, m.y, 160);                 // 과녁이 먼저 부서지면 가까운 걸로
                if (m.tg == null || m.life <= 0) { r.missiles.RemoveAt(i); continue; }
                double dx = m.tg.x - m.x, dy = m.tg.y - m.y, L = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
                if (L < 12 + Types[m.tg.k].r * 0.6)
                {
                    bool crit = Rnd() < Crit;
                    DbgMisH++; Hit(m.tg, Math.Max(1, RoundP(Pow * MissileK * (crit ? CritX : 1))), 0, true);
                    Emit(SwEv.Ring, m.x, m.y, 14, 4);
                    if (crit) Emit(SwEv.Crit, m.x, m.y - 16);
                    OnHit(0.25);
                    r.missiles.RemoveAt(i); continue;
                }
                double wx = dx / L * MissileSpeed, wy = dy / L * MissileSpeed;       // 과녁 쪽으로 방향을 부드럽게 튼다 — 가속만 주면 과녁 둘레를 빙빙 돌다 수명이 끝났다 (22%만 맞음)
                if (L < 70) { m.vx = wx; m.vy = wy; }
                else { double k = 1 - Math.Exp(-7 * dt); m.vx += (wx - m.vx) * k; m.vy += (wy - m.vy) * k; }
                m.x += m.vx * dt; m.y += m.vy * dt;
            }
        }
        void OrbTick(double dt)
        {
            var r = R;
            for (int i = r.orbs.Count - 1; i >= 0; i--)
            {
                var o = r.orbs[i];
                if (!o.there)
                {
                    double ddx = o.tx - o.x, ddy = o.ty - o.y, rem = Math.Sqrt(ddx * ddx + ddy * ddy), step = OrbSpeed * dt;
                    if (rem <= step) { o.x = o.tx; o.y = o.ty; o.there = true; } else { o.x += o.vx * dt; o.y += o.vy * dt; }
                }
                else o.t += dt;
                o.zap -= dt;
                if (o.zap <= 0)
                {   // ⚡ 가까운 잔해 몇 개에 번개
                    o.zap = ZapEvery; double zr = ZapR;
                    var near = new List<(double dd, Junk d)>();
                    foreach (var d in r.junk) { if (d.dead) continue; double ex = d.x - o.x, ey = d.y - o.y, dd = ex * ex + ey * ey, lim = zr + Types[d.k].r; if (dd < lim * lim) near.Add((dd, d)); }
                    near.Sort((a, b) => a.dd.CompareTo(b.dd));
                    for (int k = 0; k < Math.Min(ZapN, near.Count); k++)
                    {
                        var d = near[k].d; bool crit = Rnd() < Crit;
                        Emit(SwEv.Bolt, o.x, o.y, 0, crit ? 1 : 0, null, d.x, d.y);
                        Hit(d, Math.Max(1, RoundP(Pow * ZapK * (crit ? CritX : 1))), 0, true);
                    }
                    if (near.Count > 0) OnHit(0.3);
                }
                if (o.there && o.t >= OrbHover)
                {   // 💥 펑
                    double br = BurstR; bool crit = Rnd() < Crit;
                    foreach (var d in r.junk.ToArray()) { if (d.dead) continue; double ex = d.x - o.x, ey = d.y - o.y, lim = br + Types[d.k].r; if (ex * ex + ey * ey < lim * lim) Hit(d, Math.Max(1, RoundP(Pow * BurstK * (crit ? CritX : 1))), 0, true); }
                    Emit(SwEv.Ring, o.x, o.y, br, 3);
                    r.orbs.RemoveAt(i);
                }
            }
        }
        void HoleRoll() { if (Rnd() < HoleChance) OpenHole(); }
        /// <summary>무기가 맞았다 — 블랙홀 · ★ 내부자 거래 (share = 레이저처럼 자주 쏘는 무기는 몫을 나눈다)</summary>
        void OnHit(double share)
        {
            if (Rnd() < share) HoleRoll();
            var r = R;
            if (Lv("q_insider") > 0 && r.insiderN < 15 && Mk != null && StockOpen && Rnd() < 0.005 * share)
            {
                var own = new List<int>(); for (int i = 0; i < Mk.M.st.Count; i++) if (Mk.M.st[i].shares > 0) own.Add(i);
                if (own.Count > 0) { int i = own[rng.Next(own.Count)]; Mk.M.st[i].price *= 1.01; r.insiderN++; Emit(SwEv.Pop, ShipX, ShipY - 16, 0, 4, Market.Defs[i].name + " +1%"); }
            }
        }
        /// <summary>블랙홀이 저절로 열린다 — 조준점에서 3초 빨아들이고 터진다 (하나씩만)</summary>
        void OpenHole()
        {
            var r = R;
            if (r.holding || r.fuel <= 0 || (!BombsOn && !r.clean)) return;
            r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.hx = r.ax; r.hy = r.ay; r.shots++;   // Release 가 하나 뺀다
            Emit(SwEv.SkillReady, r.ax, r.ay, 1);
        }

        public static long DbgHits, DbgOneShot, DbgKills;                      // 📊 봇 진단 (tools/pacing econ) — 맞은 수 · 한 방 · 부서진 수
        void Hit(Junk d, int dmg, int src, bool spread)
        {
            if (d.dead) return;
            if (d.att == Att.Armor && src == 0 && !pierce) dmg = Math.Min(dmg, 1);
            if (d.frz > 0) dmg = (int)Math.Round(dmg * FrzMul);                        // 언 것은 두 배
            if (d.sig == GateSig && !GateReady) { if (Rnd() < 0.08) Emit(SwEv.Pop, d.x, d.y - 30, 0, 4, Loc.T("방어막 — 청구서 ") + GateBill + Loc.T("장 뒤")); return; }   // 🛡 방어막
            if (d.sig == GateSig) { if (src != 0) dmg = Math.Max(1, dmg / 3); if (Up(9) > 0) dmg = Math.Max(1, (int)Math.Round(dmg * (1 + 0.10 * Up(9)))); GateHit(d); }   // 🛰 연쇄 · 드론 · 폭발은 조금만
            if (d.sig != GateSig) R.dmgDone += Math.Min(dmg, Math.Max(0, d.hp));
            Tally(Math.Min(dmg, Math.Max(0, d.hp)), src);
            bool fresh = d.hp >= d.max; d.hp -= dmg; d.hit = 0.12; DbgHits++; if (fresh && d.hp <= 0) DbgOneShot++;   // 📊 봇 진단 — 단단함
            TraitOnHit(d);                                                      // 🪐 광맥 소행성
            if (d.att == Att.Ice && d.hp <= d.max - 2)
            {
                d.att = Att.None;
                for (int i = 0; i < 4; i++) SpawnFree(Chip, d.x, d.y, Rnd(-90, 90), Rnd(-90, 90), 1.2);
                Emit(SwEv.Shatter, d.x, d.y);
            }
            if (spread && d.att == Att.Cable)
                foreach (var l in new[] { d.link1, d.link2 }) if (l != null && !l.dead) Hit(l, Math.Max(1, dmg / 2), src, false);
            if (d.hp <= 0) Kill(d, src, 1);
        }

        void Drones(double dt)
        {
            var r = R; var o = Orbits[S.orbit];
            if (r.rushT > 0) r.rushT -= dt;
            double mid = (o.bi + Bo) / 2, reach = Reach, cd = DroneCd; int grade = DroneDmg, per = Lv("d_pair") > 0 ? 2 : 1;
            foreach (var dr in r.drones)
            {
                dr.a += dt * 0.45;
                double tx = EX + Math.Cos(dr.a) * mid, ty = EY + Math.Sin(dr.a) * mid * Tilt;
                if (r.rushT > 0) { tx = r.rushX + Math.Cos(dr.a * 3) * 30; ty = r.rushY + Math.Sin(dr.a * 3) * 20; }
                if (dr.x == 0 && dr.y == 0) { dr.x = tx; dr.y = ty; }
                double kk = Math.Min(1, dt * (r.rushT > 0 ? 4 : 6)); dr.x += (tx - dr.x) * kk; dr.y += (ty - dr.y) * kk;
                dr.cd -= dt; if (dr.cd > 0) continue;
                dr.cd = r.rushT > 0 ? cd * 0.35 : cd;
                for (int p = 0; p < per; p++)
                {
                    Junk best = null; double bd = reach * reach;
                    foreach (var d in r.junk)
                    {
                        if (d.dead || d.hp > grade || Types[d.k].big || d.k == Fuel || d.k == Tank || d.att == Att.Det || d.att == Att.Armor || d.att == Att.FuelPod) continue;   // 💥 폭탄은 드론이 안 건드린다 (09-27 — 가만히 있어도 터졌다)   // 🔴 한 방에 부술 수 있는 것만 줍는다
                        double dx = d.x - dr.x, dy = d.y - dr.y, dd = dx * dx + dy * dy;
                        if (dd < bd) { bd = dd; best = d; }
                    }
                    if (best == null) break;
                    Emit(SwEv.Beam, dr.x, dr.y, 0, 0, null, best.x, best.y);
                    Hit(best, grade, 1, false);
                    if (Lv("x_arm_drone") > 0) OnHit(0.3);
                }
            }
        }

        void DoBlast(double x, double y, double Rb, bool chainable = true)
        {
            Emit(SwEv.Blast, x, y, Rb, redBlast ? 2 : 0);                    // k 2 = 빨간 폭발 — 그림 하나로
            var list = R.junk;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d.dead || d.fade < 0.35) continue;              // 막 스며든 것은 아직 안 맞는다
                double dx = d.x - x, dy = d.y - y, rr = Rb + Types[d.k].r;
                if (dx * dx + dy * dy > rr * rr) continue;
                if (d.sig == GateSig && !GateReady) continue;                        // 🛡 방어막
                if (Types[d.k].big) { Tally(Math.Min(3, Math.Max(0, d.hp)), 2); d.hp -= 3; d.hit = 0.15; if (d.hp <= 0) Kill(d, 2, 1); }
                else { Tally(Math.Min(BlastDmg, Math.Max(0, d.hp)), 2); d.hp -= BlastDmg; d.hit = 0.15; if (d.hp <= 0) Kill(d, 2, 1); }
            }
        }

        bool blastW, redBlast; int shatterDepth, redGen;                                // redBlast = 폭발 탱크 · 기폭 장치 (휩쓸린 것 값 ×2)
        void Kill(Junk d, int src, double mult)
        {
            if (d.sig == GateSig) { if (!GateReady) { d.hp = d.max; return; } if (d.hp > 0) return; GateBroken(d); }
            if (!d.dead && d.hp > 0) Tally(d.hp, src);                          // 블랙홀 · 행성 특성처럼 체력을 안 깎고 없애는 것   // 🛡 방어막 — 폭발 · 얼음 파편은 Hit 를 안 거쳐서 여기서 막는다   // 🛰 관문은 빨려 들거나 휩쓸려 사라지지 않는다
            DbgKills++;
            if (d.dead) return;
            d.dead = true;
            if (d.frz > 0 && (Lv("w_frz") > 0 || Ship == 5) && shatterDepth < 40) { shatterDepth++; Shatter(d); shatterDepth--; }
            var r = R;
            r.broke++; M.broken++;
            if (r.clean) r.cleanKills++;
            switch (d.k) { case Vault: r.cVault++; break; case Rocket: r.cFuel++; break; case Chip: r.cChip++; break; case Sat: r.cSat++; break; case Tank: r.cTank++; break; case Big: r.cBig++; break; }
            if (d.k == Big) Record("big1", Loc.T("30년 떠다닌 우주선 잔해, 드디어 사라져"), Loc.T("큰 잔해 하나가 궤도에서 사라졌다. 청소업계는 「보험료가 아깝지 않다」고 했다."));
            // 연쇄 = 잇달아 부순 수 (어떤 무기든 · 1.6초 안에 다음 것을 부수면 이어진다)
            {
                r.chain++; r.chainT = 1.6;
                r.chainBest = Math.Max(r.chainBest, r.chain); M.bestChain = Math.Max(M.bestChain, r.chain);
                int tier = r.chain >= 200 ? 4 : r.chain >= 80 ? 3 : r.chain >= 30 ? 2 : r.chain >= 10 ? 1 : 0;
                if (tier > r.tier) { r.tier = tier; Emit(SwEv.Tier, d.x, d.y, r.chain, tier); }
                if (r.chain == 30 || r.chain == 80 || r.chain == 200) Record("chain" + r.chain, Loc.T("민간 청소선, 잔해 ") + r.chain + Loc.T("개 연쇄 파괴") + (r.chain >= 200 ? Loc.T(" — 지상에서도 보였다") : ""), Loc.T("폭발이 폭발을 불렀다. 궤도일보 관측팀은 「케슬러 연쇄를 일부러 일으킨 첫 사례」라고 적었다."));
                if (src == 2 && blastW && r.chain < ChainMax && Rnd() < ChainP) r.pend.Add(new Blast { x = d.x, y = d.y, t = Rnd(0.06, 0.14), R = 40, w = true });   // 무기 폭발만 번진다
            }
            // ★ 운석 호출 — 80개마다
            if (src == 0) r.weaponKills++;
            if (Lv("q_meteor") > 0 && r.meteors < 3 && r.weaponKills >= r.meteorAt) { r.meteorAt += 80; r.meteors++; Meteor(); }   // 무기로 부순 것만 센다 · 한 판 4번 (운석이 운석을 부르지 않게)
            // ★ 관광 명소 — 한 판에 연쇄 100
            if (Lv("q_tour") > 0 && !r.tourDone && r.chain >= 100) { r.tourDone = true; Emit(SwEv.Pop, EX, EY - 120, 0, 4, Loc.T("관광객이 몰려든다!")); Emit(SwEv.Tourist, 0, 0); if (Mk != null) Mk.GameEvent(Loc.T("토성 고리 관광객, 청소선 구경 러시"), Loc.T("궤도 청소부의 연쇄 파괴를 보려는 관광선이 줄을 섰다."), new[] { "sat" }, null, 0.12f); }
            mult *= TraitMult(d);                                               // 🪐 월면 금고 ×3 · 탐사차 ×5 · 혜성 ×4 · 대적점 안 ×2
            double v = Types[d.k].val * ValMult * mult * vacMul;
            if (d.att == Att.Gold) { v *= 3; if (r.goldN < 3) { r.goldN++; S.freeTix++; Emit(SwEv.Pop, d.x, d.y - 14, 0, 4, Loc.T("황금! 공짜 복권 +1")); } }   // 🎟 09-27 사장님 「공짜 복권을 주는 게 낫다」 — 전엔 살 수 있는 장수만 늘었다
            if (d.att == Att.Rock)
            {
                if (Rnd() < 0.4) { S.keys++; Emit(SwEv.Pop, d.x, d.y - 14, 0, 5, Loc.T("열쇠 +1!")); AddNews(null, Loc.T("떠돌이 소행성 속에서 이상한 열쇠가 나왔다"), Loc.T("청소선이 부순 소행성 속에서 반짝이는 금속 조각이 발견됐다. 케슬러 금융은 「우리 것이 아니다」라고 했다.")); }
                else { double cash = ShopBase * 0.4; S.cash += cash; Emit(SwEv.Pop, d.x, d.y - 14, 0, 4, Loc.T("돈 뭉치 +") + Math.Round(cash).ToString("N0")); }
            }
            if (d.att == Att.Pouch) v *= PouchK;                                // 💰 09-27 「다른 것도 의미 있게」 — 돈 주머니 ×2 → ×2.5 · 늘 숫자가 뜬다
            if (src == 1) v *= DroneMag * DroneValK;
            if (src == 2 && redBlast) v *= RedBlastVal;                        // 💥 09-27 사장님 「폭발 잔해가 의미가 없다」 — 빨간 폭발에 휩쓸린 것은 값 ×1.5
            { double cm = Math.Min(r.chain, 200 + Part("combo")) / 200.0;          // 잇달아 부수면 값이 더 붙는다 (최대 ×2 · 어떤 무기든)
              if (!r.clean && cm > 0) { r.chainBank += v * cm * BotEarn; r.chainX = d.x; r.chainY = d.y; }   // 💰 09-27 사장님 「연쇄 끝나면 돈을 파파팍」 — 보너스 합계만 세어 두었다가 연쇄가 끝날 때 보여 준다 (돈은 바로 들어간다 · 경제 그대로)
              v *= 1 + cm; }
            Pay(d, src, v, d.x, d.y, true);
            if (d.att == Att.Pouch && v * BotEarn >= 1) Emit(SwEv.Pop, d.x, d.y - 14, 0, 0, Loc.T("돈 주머니 +") + KFmt(v));
            Emit(SwEv.Broke, d.x, d.y, 0, d.k * 100 + (int)d.att);
            if (d.k == Fuel && AddFuel(3) > 0) Emit(SwEv.Pop, d.x, d.y, 0, 1, Loc.T("연료 +3초"));
            if (d.k == Tank && r.pend.Count < PendCap && (!redBlast || redGen < RedGenMax)) r.pend.Add(new Blast { x = d.x, y = d.y, t = 0.05, R = TankR, red = true, gen = redBlast ? redGen + 1 : 1 });   // 💥 09-27 사장님 「빨간 폭탄 안 터지는 것도 있나?」 — 연쇄 한도(지구 40)를 넘으면 조용히 안 터졌다. 이제 늘 (렉 막는 건 대기 폭발 수로)
            switch (d.att)
            {
                case Att.FuelPod: if (AddFuel(2) > 0) Emit(SwEv.Pop, d.x, d.y - 10, 0, 1, Loc.T("연료 +2초")); break;
                case Att.Det: if (r.pend.Count < PendCap && (!redBlast || redGen < RedGenMax)) r.pend.Add(new Blast { x = d.x, y = d.y, t = 0.07, R = DetR, red = true, gen = redBlast ? redGen + 1 : 1 }); break;
                case Att.Ice: for (int i = 0; i < 4; i++) SpawnFree(Chip, d.x, d.y, Rnd(-90, 90), Rnd(-90, 90), 1.2); break;
                case Att.Magnet:
                    Emit(SwEv.Ring, d.x, d.y, 90, 2);
                    foreach (var q in r.junk) if (!q.dead && q.k == Chip) { double dx = d.x - q.x, dy = d.y - q.y; if (dx * dx + dy * dy < 8100) { q.free = true; q.vx = dx * 2.2; q.vy = dy * 2.2; q.capT = 1; } }
                    break;
                case Att.Beacon: r.rushT = 3 + 2 * Lv("d_sig"); r.rushX = d.x; r.rushY = d.y; Emit(SwEv.Ring, d.x, d.y, 40, 2); break;
                case Att.BBox:
                    M.scoops++; S.creditPending += 1;
                    AddNews("s" + M.scoops);
                    Emit(SwEv.Pop, d.x, d.y - 14, 0, 3, Loc.T("블랙박스 — 특종 제보!"));
                    break;
            }
            TraitOnKill(d);                                                     // 🪐 위성 줄 · 결정 공명 · 금고 · 얼음 · 혜성
        }

        // 연료는 탱크를 넘지 않고, 한 판에 되찾는 양도 탱크의 60%까지 — 판이 끝없이 길어지지 않게
        double AddFuel(double s) { var r = R; double room = Math.Max(0, r.max * 0.25 - r.fuelGot); s = Math.Min(s, Math.Min(room, r.max - r.fuel)); if (s <= 0) return 0; r.fuelGot += s; r.fuel += s; return s; }

        void Pay(Junk d, int src, double v, double x, double y, bool coin)
        {
            var r = R;
            if (v <= 0) return;
            v *= BotEarn;
            if (d.att == Att.Tag && S.bill < Bills.Length)
            {
                double toBill = v * 1.5;
                r.toBill += toBill; r.cTag++;
                S.billAmount = Math.Max(0, BillAmount - toBill);
                Emit(SwEv.Coin, x, y, toBill, 3);
                return;
            }
            double cutAmt = Math.Min(v * Cut, S.debt), got = v - cutAmt;
            S.cash += got; r.cut += cutAmt; S.debt -= cutAmt;
            if (src == 0) r.earnClaw += got; else if (src == 1) r.earnDrone += got; else r.earnBlast += got;
            if (coin) Emit(SwEv.Coin, x, y, got, src + (cutAmt > 0 ? 10 : 0));
        }

        void ChainPay()
        {   // 💰 연쇄가 끝나면 이번 연쇄로 더 붙은 돈을 한꺼번에 보여 준다 (돈은 부술 때 이미 들어갔다)
            var r = R; if (r == null || r.chainBank < 1) { if (r != null) r.chainBank = 0; return; }
            double v = r.chainBank; r.chainBank = 0;
            Emit(SwEv.ChainPay, r.chainX, r.chainY, v, r.chain);
        }
        void EndRun()
        {
            var r = R;
            ChainPay();                                                         // 판이 끝나도 남은 연쇄 보너스는 준다
            r.over = true; M.totalRuns++;
            if (r.clean)
            {
                M.won = true; M.cleanReady = false; M.legend++;                   // ★ 전설 경력
                M.history.Add(new PastCompany { company = M.company, bill = S.bill, runs = S.runs, minutes = (M.playSeconds - S.startedAt) / 60, won = true });
                AddNews("clean");
                Emit(SwEv.Won);
                return;
            }
            if (M.endless)
            {   // ∞ 무한 궤도 — 판이 끝날 때마다 한 층 아래로
                M.depth++; if (M.depth > M.bestDepth) M.bestDepth = M.depth;
                if (M.depth % 5 == 0) { S.keys++; AddNews(null, Loc.T("무한 궤도 ") + M.depth + Loc.T("층 — 심연 보급"), Loc.T("깊은 궤도에서 낡은 열쇠 하나가 떠올랐다. (열쇠 +1)")); }
            }
            var c = CurContract;
            if (c != null) { r.contractText = c.Value.text; r.contractTarget = c.Value.target; r.contractProg = Math.Min(ContractProgress(r), c.Value.target); }
            if (c != null && ContractProgress(r) >= c.Value.target) { r.contractOk = true; r.bonus = Math.Ceiling(r.Earned * (0.25 + 0.1 * Lv("e_quest"))); S.cash += r.bonus; }
            if (Lv("e_save") > 0) { r.interest = Math.Min(r.Earned, Math.Floor(S.cash * 0.02 * Lv("e_save"))); S.cash += r.interest; }
            if (S.bill < Bills.Length)
            {
                if (BillAmount <= 0) FinishBill();              // 압류 딱지로 다 갚았다
                else if (!S.overdue)
                {
                    S.billDue--;
                    if (S.billDue <= 0) { S.overdue = true; S.overRuns = 0; Emit(SwEv.Overdue); }   // 납부일 — 갚거나 · 대출받아 갚거나 · 파산 (연체 이자 · 추심은 없앴다)
                }
                else S.overRuns++;
            }
            if (r.cut > 0) LogLoan(1, r.cut);
            S.lastClaw = r.earnClaw; S.lastDrone = r.earnDrone; S.lastBlast = r.earnBlast; S.runEarn.Add(r.earnClaw + r.earnDrone + r.earnBlast); if (S.runEarn.Count > 6) S.runEarn.RemoveAt(0); S.lastBroke = r.broke; S.lastChain = r.chainBest;
            S.lastContract = r.contractText == null ? 0 : r.contractOk ? 1 : 2;
            LottoDraw();                                                // 🎱 추첨 날이면
            if (!R.clean && R.Earned > 0) S.runAvg = S.runAvg <= 0 ? R.Earned : S.runAvg * 0.6 + R.Earned * 0.4;   // 💰 가게 값 기준
            if (!R.clean && R.dmgDone > 0) S.dmgAvg = S.dmgAvg <= 0 ? R.dmgDone : S.dmgAvg * 0.6 + R.dmgDone * 0.4;
            S.gateFrac = 1;                                                  // 🛰 한 판 안에 못 부수면 다음 판엔 다시 가득 (09-26 사장님)
            if (S.gateNext > 0) { int nx = S.gateNext; S.gateNext = 0; int pn = NodeIx[PlanetNode[nx]]; if (S.lv[pn] <= 0) S.lv[pn] = 1; PlanetBought(nx); }
            S.rolls = 0;
            if (ShopOpen) { RollShop(); S.freeRoll = true; }                  // 🔩 가게 진열이 바뀐다 · 공짜 새로고침 한 번
            if (Lv("q_front") > 0 && Mk != null && StockOpen) { S.front1 = rng.Next(Market.NewsBook.Length); do S.front2 = rng.Next(Market.NewsBook.Length); while (S.front2 == S.front1); }
            CheckClean();                                               // 판 수입에서 떼어 빚을 다 갚았을 수도
            RollContract();
            Emit(SwEv.RunEnd);
        }

        /// <summary>출동 사이 — 조종실 창밖에서 궤도와 드론이 계속 돈다 (규칙은 안 움직인다)</summary>
        /// <summary>조종실 창밖에 보일 궤도 — 출동 전에도 쓰레기와 드론이 떠 있다</summary>
        public void Preview()
        {
            R = new SweepRun { over = true };
            int n = JunkTarget;
            for (int i = 0; i < n; i++) Spawn(-1, -1, -1, null, false).fade = 1;
            for (int i = 0; i < DroneCount; i++) R.drones.Add(new Drone { a = i * Math.PI * 2 / Math.Max(1, DroneCount) });
        }

        public void IdleTick(double dt)
        {
            var o = Orbits[S.orbit];
            foreach (var d in R.junk)
            {
                if (d.dead) continue;
                d.hit = 0; d.fade = 1; d.rot += d.vr * dt;
                if (d.free) Recapture(d, o.bi, Bo);
                d.a += 0.12 * d.ws * dt * o.spin; Place(d);                  // 행성마다 도는 빠르기 (창밖도)
            }
            double mid = (o.bi + Bo) / 2;
            foreach (var dr in R.drones) { dr.a += dt * 0.45; dr.x = EX + Math.Cos(dr.a) * mid; dr.y = EY + Math.Sin(dr.a) * mid * Tilt; }
        }

        public void ReadAll() { foreach (var n in M.news) n.read = true; }
    }
}
