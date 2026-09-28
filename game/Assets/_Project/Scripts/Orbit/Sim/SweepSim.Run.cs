using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 🚀 출동 — 판 시작 · 잔해 뿌리기 · 한 걸음 · 이벤트 · 움직임 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ───────────────────────── 출동
        public void StartRun()
        {
            if (!R.over || S.bill >= Bills.Length && !M.cleanReady && S.debt <= 0 && !M.endless) return;   // 청구서를 다 갚아도 빚이 남았으면 갚으러 출동한다
            bool clean = M.cleanReady;
            if (Mk != null) Mk.ArmNews(Rnd(5, 14));                                 // 📰 이번 출동 중 속보 하나
            if (clean) S.orbit = MaxOrbit;
            S.runs++; S.rerolled = false;
            var r = new SweepRun { clean = clean };
            r.max = r.fuel = clean ? 45 : FuelMax + S.nFuel;
            r.consDmg = S.nDmg; r.consVal = S.nVal; S.nFuel = S.nDmg = S.nVal = 0;   // 🧃 가게 소모품 — 이번 판
            r.maxShots = clean ? 6 : Bombs; r.shots = 0;   // 블랙홀은 스킬 — 한 칸 들고 나가서 시간 따라 찬다
            int nfuel = clean ? 0 : Lv("c_find");
            for (int i = 0; i < nfuel; i++) r.pods.Add(new Pod { kind = 0, t = 5 + i * 5 });
            R = r; TraitStart();
            var o = Orbits[S.orbit];
            var forms = o.forms;
            int nf = Math.Min(forms.Length, 2 + (Rnd() < 0.5 ? 1 : 0));
            for (int i = 0; i < nf; i++) Formation(forms[i], i * 2.1 + Rnd(0, 1.5));
            if (S.runs == 1 && M.company == 1) { Formation(0, 0.5); Formation(0, 1.0); }   // 첫 판 — 커서 가까이 무리 둘 (§10)
            int target = clean ? 400 : JunkTarget;
            while (Alive() < target) Spawn(-1, -1, -1, null, false);
            foreach (var d in r.junk) d.fade = 1;
            for (int i = 0; i < DroneCount; i++) r.drones.Add(new Drone { a = i * Math.PI * 2 / Math.Max(1, DroneCount), cd = Rnd() });
            if (!clean && !M.endless) SpawnGate();                            // 🛰 관문 — 가장 먼 행성에서만
            if (Lv("q_rock") > 0 && !clean && Rnd() < 0.35) r.rockT = Rnd(5, 12);
            // 사건 — 10~14초, 22~26초 (연료 40 넘을 때만)
            var ev = o.events;
            if (S.bill >= 1 || clean)                                     // 판 중 사건 — 첫 청구서를 갚은 뒤부터 (09-26 조작부터 익히게)
            {
                r.ev1 = ev[rng.Next(ev.Length)]; r.ev1T = Rnd(6, 9);
                if (r.max >= 24) { r.ev2 = ev[rng.Next(ev.Length)]; r.ev2T = Rnd(13, 16); }
            }
            r.collector = false;                                        // 추심선은 대출로 바뀌며 쉰다
            // 블랙박스 — 청구서 2 뒤 · 판마다 25%
            if ((S.runs >= 2 || clean) && M.scoops < 6 && (clean || Rnd() < 0.25 + 0.1 * Lv("e_tip")))
            {
                var hosts = r.junk.FindAll(d => IsHost(d.k) && d.att == Att.None);
                if (hosts.Count > 0) hosts[rng.Next(hosts.Count)].att = Att.BBox;
            }
            if (clean) r.cleanGoal = 8000;
            if (S.runs == 6) AddNews("run6");
            PublishFront();                                                  // 📰 조작한 1면 — 출동과 함께 발행
        }

        int Alive() { int n = 0; foreach (var d in R.junk) if (!d.dead) n++; return n; }

        // 🛰 쓰레기 종 — 행동은 종류(k)가 정하고, 모습 · 등급은 종이 정한다 (09-24 사장님 「쓰레기를 더 다양하게 · 하위는 없어지게」)
        // 등급 = 행성 순위(지구 0 … 카이퍼 8 · 오르트 9 · 태양권 계면 10 · 성간 11). 지금 순위 ±1 이 주로 나오고, 두 단계 아래는 드물게, 그보다 아래는 안 나온다
        public struct Species { public string name, art; public int kind, tier; }
        public static readonly Species[] Spc =
        {
            new Species { name = Loc.T("고철 조각"),       art = "junk_chip_a",     kind = Chip,   tier = 0 },
            new Species { name = Loc.T("휜 판"),           art = "junk_chip_b",     kind = Chip,   tier = 1 },
            new Species { name = Loc.T("태양판 조각"),     art = "junk_chip_c",     kind = Chip,   tier = 1 },
            new Species { name = Loc.T("광석 덩어리"),     art = "junk_ore",        kind = Chip,   tier = 3 },
            new Species { name = Loc.T("고리 얼음"),       art = "junk_ringice",    kind = Chip,   tier = 5 },
            new Species { name = Loc.T("결정체"),          art = "junk_crystal",    kind = Chip,   tier = 6 },
            new Species { name = Loc.T("혜성 조각"),       art = "junk_comet",      kind = Chip,   tier = 8 },
            new Species { name = Loc.T("죽은 위성"),       art = "junk_sat",        kind = Sat,    tier = 1 },
            new Species { name = Loc.T("채굴 드론 잔해"),  art = "junk_minedrone",  kind = Sat,    tier = 3 },
            new Species { name = Loc.T("관광선 잔해"),     art = "junk_tourwreck",  kind = Sat,    tier = 5 },
            new Species { name = Loc.T("폭풍 탐사선"),     art = "junk_stormprobe", kind = Sat,    tier = 7 },
            new Species { name = Loc.T("고대 탐사선"),     art = "junk_ancient",    kind = Sat,    tier = 8 },
            new Species { name = Loc.T("로켓 잔해"),       art = "junk_rocket",     kind = Rocket, tier = 2 },
            new Species { name = Loc.T("가스 채굴선 잔해"), art = "junk_gashulk",   kind = Rocket, tier = 4 },
            // 🛰 09-26 사장님 「쓰레기 추가 · 행성별로」 — 한 가지 그림뿐이던 금고 · 큰 잔해와, 비어 있던 로켓 · 위성 · 조각 칸 (픽셀랩)
            new Species { name = Loc.T("구형 발사체 1단"),  art = "junk_rocket_old",   kind = Rocket, tier = 0 },
            new Species { name = Loc.T("고리 수송선"),      art = "junk_rocket_ring",  kind = Rocket, tier = 5 },
            new Species { name = Loc.T("얼음 운반 로켓"),   art = "junk_rocket_ice",   kind = Rocket, tier = 7 },
            new Species { name = Loc.T("성간 탐사 로켓"),   art = "junk_rocket_probe", kind = Rocket, tier = 8 },
            new Species { name = Loc.T("통신 위성"),        art = "junk_sat_comm",     kind = Sat,    tier = 0 },
            new Species { name = Loc.T("달 착륙선 잔해"),   art = "junk_sat_lander",   kind = Sat,    tier = 1 },
            new Species { name = Loc.T("가스 결정"),        art = "junk_chip_gas",     kind = Chip,   tier = 4 },
            new Species { name = Loc.T("은행 위성"),        art = "junk_vault_bank",   kind = Vault,  tier = 0 },
            new Species { name = Loc.T("금고 위성"),        art = "junk_vault",        kind = Vault,  tier = 2 },
            new Species { name = Loc.T("금괴 캡슐"),        art = "junk_vault_gold",   kind = Vault,  tier = 4 },
            new Species { name = Loc.T("고대 금고"),        art = "junk_vault_ancient", kind = Vault, tier = 7 },
            new Species { name = Loc.T("정거장 모듈"),      art = "junk_big_station",  kind = Big,    tier = 0 },
            new Species { name = Loc.T("채굴 플랫폼"),      art = "junk_big_mine",     kind = Big,    tier = 2 },
            new Species { name = Loc.T("원반 정거장"),      art = "junk_big",          kind = Big,    tier = 3 },
            new Species { name = Loc.T("가스 수확기"),      art = "junk_big_gas",      kind = Big,    tier = 4 },
            new Species { name = Loc.T("고리 정거장"),      art = "junk_big_ring",     kind = Big,    tier = 5 },
            new Species { name = Loc.T("얼음 화물선"),      art = "junk_big_ice",      kind = Big,    tier = 7 },
            new Species { name = Loc.T("외계 유물"),        art = "junk_big_relic",    kind = Big,    tier = 8 },
            new Species { name = Loc.T("폭풍 속 탐사차"),   art = "junk_sig_rover",    kind = Sat,    tier = 99 },   // 🪐 행성 특성 전용 (99 = 무작위로 안 나옴)
            new Species { name = Loc.T("월면 금고"),        art = "junk_sig_moonvault", kind = Vault, tier = 99 },
            new Species { name = Loc.T("광맥 소행성"),      art = "junk_sig_ore",      kind = Big,    tier = 99 },
            new Species { name = Loc.T("고리 얼음 덩이"),   art = "junk_sig_ice",      kind = Rocket, tier = 99 },
            // 🪐 09-27 사장님 「어느 순간부터 쓰레기 종류가 고정」 — 등급이 카이퍼(8)까지뿐이라 새 행성 셋에선 카이퍼 잔해만 나왔다 (픽셀랩)
            new Species { name = Loc.T("혜성 얼음 조각"),   art = "junk_oort_chip",    kind = Chip,   tier = 9 },
            new Species { name = Loc.T("얼어붙은 관측 위성"), art = "junk_oort_sat",   kind = Sat,    tier = 9 },
            new Species { name = Loc.T("혜성 포획 로켓"),   art = "junk_oort_rocket",  kind = Rocket, tier = 9 },
            new Species { name = Loc.T("얼음 속 금고"),     art = "junk_oort_vault",   kind = Vault,  tier = 9 },
            new Species { name = Loc.T("혜성 채굴선"),      art = "junk_oort_big",     kind = Big,    tier = 9 },
            new Species { name = Loc.T("플라스마 결정"),    art = "junk_helio_chip",   kind = Chip,   tier = 10 },
            new Species { name = Loc.T("태양풍 측정기"),    art = "junk_helio_sat",    kind = Sat,    tier = 10 },
            new Species { name = Loc.T("빛돛 로켓"),        art = "junk_helio_rocket", kind = Rocket, tier = 10 },
            new Species { name = Loc.T("자기장 금고"),      art = "junk_helio_vault",  kind = Vault,  tier = 10 },
            new Species { name = Loc.T("태양 돛 잔해"),     art = "junk_helio_big",    kind = Big,    tier = 10 },
            new Species { name = Loc.T("암흑 물질 조각"),   art = "junk_inter_chip",   kind = Chip,   tier = 11 },
            new Species { name = Loc.T("성간 신호탑"),      art = "junk_inter_sat",    kind = Sat,    tier = 11 },
            new Species { name = Loc.T("세대선 엔진"),      art = "junk_inter_rocket", kind = Rocket, tier = 11 },
            new Species { name = Loc.T("외계 보물 상자"),   art = "junk_inter_vault",  kind = Vault,  tier = 11 },
            new Species { name = Loc.T("외계 모선 파편"),   art = "junk_inter_big",    kind = Big,    tier = 11 },
        };
        public int Rank => Math.Max(0, Array.IndexOf(OrbitOrder, S.orbit));      // 가까운 → 먼 순위
        int PickSpecies(int k)
        {
            int rk = R != null && R.clean ? OrbitOrder.Length - 1 : Rank, best = -1; double sum = 0; var w = new double[Spc.Length];
            for (int i = 0; i < Spc.Length; i++)
            {
                if (Spc[i].kind != k) continue;
                int dt = Spc[i].tier - rk;
                w[i] = dt == 0 ? 2 : Math.Abs(dt) == 1 ? 1 : dt == -2 ? 0.25 : 0; sum += w[i];
                if (Spc[i].tier <= rk + 1 && (best < 0 || Spc[i].tier > Spc[best].tier)) best = i;
            }
            if (sum <= 0) return best;                                             // 창 안에 없으면 가장 높은 것
            double v = Rnd() * sum;
            for (int i = 0; i < w.Length; i++) { v -= w[i]; if (w[i] > 0 && v <= 0) return i; }
            return best;
        }

        int PickType()
        {
            var o = Orbits[S.orbit];
            double[] w = (double[])o.mix.Clone();
            w[Vault] *= (1 + 0.5 * Lv("e_vault")) * (1 + Part("vault"));
            w[Fuel] = 0;                                                  // 🔴 연료는 궤도에 안 떠 있다 — 지구에서 보급한다
            if (!R.clean && S.bill < 1) w[Vault] = 0;                     // 금고는 청구서 1 뒤
            if (!R.clean && S.bill < 2 && S.orbit == 0) w[Tank] = 0;      // 폭발 탱크는 폭탄이 온 뒤
            w[Tank] *= 0.5;                                               // 💥 09-27 밤 「너무 셈」 — 폭발 탱크 절반
            if (!BigsOn && !R.clean) w[Big] = 0;
            double sum = 0; foreach (var x in w) sum += x;
            double v = Rnd() * sum;
            for (int i = 0; i < w.Length; i++) { v -= w[i]; if (v <= 0) return i; }
            return Chip;
        }

        Att PickAtt()
        {
            var list = new List<Att> { Att.Pouch, Att.Pouch };
            if (DronesOn) list.Add(Att.Beacon);                                 // 자석 부착물은 없앴다 (09-26) · 신호기는 드론이 있을 때만 (09-27 — 없으면 아무 일도 없었다)
            if (S.orbit >= 2 || R.clean) { list.Add(Att.Det); list.Add(Att.Ice); }   // 기폭 장치 둘 → 하나 (09-27 밤)
            if (S.orbit == 2) { list.Add(Att.Ice); list.Add(Att.Ice); }             // 화성 — 얼음 껍질
            if (Rank >= 4 || R.clean) { list.Add(Att.Armor); list.Add(Att.Armor); }          // 목성부터 (순위)
            if (S.orbit == 3) list.Add(Att.Armor);                                   // 목성 — 장갑판
            return list[rng.Next(list.Count)];
        }

        Junk Spawn(int k, double a, double rr, Att? att, bool edge, double ws = -1)
        {
            var o = Orbits[S.orbit];
            if (k < 0) k = PickType();
            if (a < 0) a = Rnd(0, Math.PI * 2);
            if (rr < 0) { double u = Rnd(); if (o.gap > 0) u = u < 0.5 ? u * (1 - o.gap) : 0.5 * (1 - o.gap) + o.gap + (u - 0.5) * (1 - o.gap); rr = o.bi + u * (Bo - o.bi); }   // 토성 — 가운데 틈을 비운다                  // 띠 안 아무 곳에서 서서히 나타난다 (가장자리에서만 들어오면 바깥에 쏠린다)
            Att at = att ?? Att.None;
            if (att == null && Lv("q_gold") > 0 && IsHost(k) && Rnd() < 0.012) at = Att.Gold;
            if (att == null && at == Att.None && IsHost(k) && Rnd() < (R.clean ? 0.35 : o.att * (1 + 0.4 * Lv("e_att")) * (1 + Part("att")))) at = PickAtt();
            int hp = (int)Math.Round(Types[k].hp * (k == Fuel || k == Tank ? 1 : HpMul)) + (at == Att.Ice ? 2 : 0);   // 청구서를 갚을수록 단단해진다
            var d = new Junk { id = ++R.idc, k = k, sp = PickSpecies(k), hp = hp, max = hp, att = at, a = a, rr = rr, ws = ws > 0 ? ws : Rnd(0.92, 1.08), rot = Rnd(0, 6), vr = Rnd(-1, 1) };
            Place(d);
            R.junk.Add(d);
            return d;
        }

        static void Place(Junk d) { if (!d.free) { d.x = EX + Math.Cos(d.a) * d.rr; d.y = EY + Math.Sin(d.a) * d.rr * Tilt; } }

        Junk SpawnFree(int k, double x, double y, double vx, double vy, double capT, Att att = Att.None)
        {
            var d = Spawn(k, 0, 200, att, false);
            d.free = true; d.x = x; d.y = y; d.vx = vx; d.vy = vy; d.capT = capT; d.fade = 1;
            return d;
        }

        void Formation(int kind, double a0)
        {
            var o = Orbits[S.orbit]; double mid = (o.bi + Bo) / 2;
            switch (kind)
            {
                case 0: Spawn(Sat, a0, mid, null, false, 1); for (int i = 0; i < 13; i++) Spawn(Chip, a0 + Rnd(-0.11, 0.11), mid + Rnd(-22, 22), Att.None, false, 1); break;
                case 1: for (int i = 0; i < 8; i++) Spawn(Tank, a0 + i * 0.1, mid + 12, Att.None, false, 1); break;
                case 2:
                {
                    var ring = new Junk[6];
                    for (int i = 0; i < 6; i++) { double an = i / 6.0 * Math.PI * 2; ring[i] = Spawn(Sat, a0 + Math.Cos(an) * 0.15, mid + Math.Sin(an) * 38, Att.Cable, false, 1); }
                    for (int i = 0; i < 6; i++) { ring[i].link1 = ring[(i + 1) % 6]; ring[i].link2 = ring[(i + 5) % 6]; }
                    break;
                }
                case 3:
                {   // 🚚 09-26 사장님 「궤도 안 타고 쭉 지나가서 못 먹을 수도」 — 화면 위쪽을 한쪽에서 다른 쪽으로, 놓치면 빠져나간다
                    bool ltr = Rnd() < 0.5; double y0 = EY - Bo * Tilt * Rnd(0.55, 0.85), vx = (ltr ? 1 : -1) * 190, x0 = ltr ? -30 : 990;
                    for (int i = 0; i < 5; i++) { var v = SpawnFree(Vault, x0 - (ltr ? 1 : -1) * i * 30, y0 + Rnd(-8, 8), vx, 0, 99); v.convoy = true; }
                    var e1 = SpawnFree(Sat, x0 + (ltr ? 1 : -1) * 30, y0, vx, 0, 99, Att.Armor); e1.convoy = true;
                    var e2 = SpawnFree(Sat, x0 - (ltr ? 1 : -1) * 170, y0, vx, 0, 99, Att.Armor); e2.convoy = true;
                }
                    break;
                case 4:
                    Spawn(BigsOn || R.clean ? Big : Rocket, a0, mid, Att.Ice, false, 1);
                    Spawn(BigsOn || R.clean ? Big : Rocket, a0 + 0.2, mid + 18, Att.Det, false, 1);
                    for (int i = 0; i < 10; i++) Spawn(Rnd() < 0.5 ? Sat : Rocket, a0 + Rnd(-0.2, 0.4), mid + Rnd(-40, 40), Rnd() < 0.3 ? Att.Armor : Rnd() < 0.3 ? Att.Det : Att.None, false, 1);
                    break;
            }
        }

        // ───────────────────────── 한 걸음
        public void Tick(double dt, double ax, double ay, bool aim, bool hold)
        {
            var r = R; if (r.over) return;
            M.playSeconds += dt; r.t += dt;
            // 손 안 댄 시간 (idleT — 게임이 손을 대면 0으로 · 예전 게으름 보너스가 썼다)
            r.idleT += dt;
            if (r.tapCombo > 0 && r.t - r.lastPress > 1) r.tapCombo = 0;          // ★ 연타 장인 — 1초 손을 떼면 식는다 (예전 게으름 보너스 자리)
            // ★ 떠돌이 소행성
            if (r.rockT > 0 && r.t >= r.rockT) { r.rockT = -1; var o = Orbits[S.orbit]; var rk = Spawn(Big, Rnd(0, 6.28), (o.bi + Bo) / 2, Att.Rock, false, 0.7); rk.hp = rk.max = (int)Math.Round(rk.max * 2.5); Emit(SwEv.Warn, 0, 0, 0, 1, Loc.T("떠돌이 소행성이 궤도에 끼어들었다!")); }
            if (aim) { r.ax = ax; r.ay = ay; }
            if (r.fuel > 0) r.fuel -= dt * FuelIdle;                       // ⛽ 가만히 있어도 조금씩 — 쏠 때 더 닳는다 (09-26 사장님 「공격에 연료를 닳게」)

            // 🌀 블랙홀 스킬 — 누르면 그 자리에 열려 3초 빨아들이고 저절로 터진다. 칸은 시간 따라 찬다
            r.holeCd = 0;                                               // 시간으로는 안 찬다 — Strike 에서 확률로
            if (r.holding && (r.holdT >= HoleDur || r.fuel <= 0)) Release();

            Schedule(dt);
            TraitTick(dt);                                                   // 🪐 행성 특성
            Supply(dt);
            Motion(dt);
            if (r.holding) Pull(dt);
            if (aim && r.fuel > 0) Claw(dt);                                   // 보조 무기 칸은 없앴다 (09-24 확률 효과로)
            Mines(dt);
            if (r.magHole > 0) { r.magHole -= dt; if (r.magHole <= 0 && !r.holding && r.fuel > 0) { r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.hx = r.magX; r.hy = r.magY; r.shots++; Emit(SwEv.SkillReady, r.hx, r.hy, 1); } }   // 자석 각성 — 모인 자리에 블랙홀                           // 블랙홀이 열려 있어도 빔은 계속
            Drones(dt);
            if (r.orbs.Count > 0) OrbTick(dt);                                  // ⚡ 전격선
            if (r.missiles.Count > 0) MissileTick(dt);                          // 🚀 미사일선

            // 💥 연쇄
            for (int i = 0; i < r.pend.Count; i++) r.pend[i].t -= dt;
            for (int i = r.pend.Count - 1; i >= 0; i--)
            {
                var p = r.pend[i];
                if (p.t > 0) continue;
                r.pend.RemoveAt(i);
                if (p.dk > 0) { ShellBurst(p.x, p.y, p.R, p.dk); continue; }        // 🎆 분열탄선 포탄 · 파편
                blastW = p.w; redBlast = p.red; redGen = p.gen; dmgCat = p.wid; DoBlast(p.x, p.y, p.R * BlastK); dmgCat = -1; blastW = false; redBlast = false; redGen = 0;
            }
            r.chainT -= dt;
            if (r.chainT <= 0 && r.pend.Count == 0 && !r.holding) { ChainPay(); r.chain = 0; r.tier = 0; }

            // 치우기 · 다시 채우기 (띠 안 아무 곳에서 스며든다)
            r.junk.RemoveAll(d => d.dead && !r.packed.Contains(d));
            int target = r.clean ? 400 : JunkTarget, alive = Alive();
            r.refill = Math.Min(6, r.refill + dt * target * RefillRate);
            while (alive < target && r.refill >= 1) { Spawn(-1, -1, -1, null, true); alive++; r.refill -= 1; }
            r.formT -= dt;
            if (r.formT <= 0) { r.formT = 9; if (alive < target + 40) { var f = Orbits[S.orbit].forms; Formation(f[rng.Next(f.Length)], Rnd(0, Math.PI * 2)); } }

            if (r.fuel <= 0 && !r.holding && r.pend.Count == 0)
            {
                if (r.clean) { r.cleanKills = Math.Max(r.cleanKills, r.cleanGoal); DoBlast(EX, EY, 420); EndRun(); return; }   // 청산 출동은 연료를 다 쓰면 끝 — 늘 성공
                r.fuel = 0; r.endT += dt;
                if (r.endT > 1.3) EndRun();
            }
        }

        void Schedule(double dt)
        {
            var r = R;
            void Check(ref double t, ref bool warned, int ev)
            {
                if (t < 0 || ev < 0) return;
                if (!warned && r.t >= t - 2) { warned = true; Emit(SwEv.Warn, 0, 0, ev, ev == 2 ? 0 : 1, "⚠ " + EventNames[ev] + " — " + EventHint[ev]); }
                if (r.t >= t) { t = -1; FireEvent(ev); }
            }
            Check(ref r.ev1T, ref r.ev1Warn, r.ev1);
            Check(ref r.ev2T, ref r.ev2Warn, r.ev2);
            if (r.collector && r.t > 6) { r.collector = false; Collector(); }
            if (StormOn && r.stormLeft <= 0) { r.stormLeft = 1; r.stormT = 0.15; }       // 폭풍 동안 고철 줄기
            if (r.stormLeft > 0)
            {
                r.stormT -= dt;
                while (r.stormT <= 0 && r.stormLeft > 0)
                {
                    r.stormT += 0.12; r.stormLeft--;
                    var o = Orbits[S.orbit];
                    SpawnFree(Chip, -10, EY + Rnd(-Bo * Tilt, Bo * Tilt), Rnd(170, 240), Rnd(-20, 20), 2.6);
                }
            }
        }

        void FireEvent(int ev)
        {
            var r = R; var o = Orbits[S.orbit]; double mid = (o.bi + Bo) / 2;
            Emit(SwEv.EventGo, 0, 0, ev, 0, EventNames[ev]);
            switch (ev)
            {
                case 0: SpawnFree(Fuel, -10, EY + 40, 150, 0, 3.2); break;            // ⛽ 09-27 사장님 「연료통이 한 번에 많이 날아오지 말고 하나만」 — 다섯이 와도 한 판 되찾는 한도(25%)에 막혀 한두 개만 찼다
                case 1:
                case 4:
                {
                    double a = -0.5, x = EX + Math.Cos(a) * mid, y = EY + Math.Sin(a) * mid * Tilt;
                    int n = ev == 1 ? 30 : 60;
                    Emit(SwEv.Ring, x, y, ev == 1 ? 70 : 120, 1);
                    for (int i = 0; i < n; i++)
                    {
                        int k = Rnd() < 0.7 ? Chip : Rnd() < 0.5 ? Sat : Tank;
                        SpawnFree(k, x, y, Rnd(-220, 220), Rnd(-160, 160), Rnd(1, 1.8), ev == 4 && k == Sat && Rnd() < 0.5 ? Att.Det : Att.None);
                    }
                    break;
                }
                case 2: r.stormLeft = 40; r.stormT = 0; break;
                case 3: Formation(3, Rnd(0, Math.PI * 2)); break;
            }
        }

        void Supply(double dt)
        {
            var r = R;
            foreach (var p in r.pods)
            {
                if (p.got) continue;
                if (!p.up)
                {
                    if (r.t < p.t) continue;
                    p.up = true;
                    p.a = Rnd(0, Math.PI * 2); p.rr = Orbits[S.orbit].bi * 0.8;
                    p.x = EX + Math.Cos(p.a) * p.rr; p.y = EY + Math.Sin(p.a) * p.rr * Tilt;
                    Emit(SwEv.Supply, p.x, p.y, 0, p.kind, p.kind == 1 ? Loc.T("지구 보급 — 폭탄") : Loc.T("지구 보급 — 연료"));
                    continue;
                }
                // ⛽ 09-26 사장님 「궤도 따라 쭉 움직이면서 밖으로 — 안 먹으면 없어진다 · 빠르게」 — 조준점을 대야 먹는다
                p.a += (1.4 + 0.3 * Lv("s_speed")) * dt; p.rr += (Bo + 90 - Orbits[S.orbit].bi * 0.8) / 5.0 * dt;   // 09-27 사장님 「연료 보급 가시성이 안 좋다」 — 2.2 · 3.6초 → 1.4 · 5초
                p.x = EX + Math.Cos(p.a) * p.rr; p.y = EY + Math.Sin(p.a) * p.rr * Tilt;
                if (p.rr > Bo + 90) { p.got = true; Emit(SwEv.Pop, p.x, p.y, 0, 1, Loc.T("보급을 놓쳤다")); continue; }
                double dx = r.ax - p.x, dy = r.ay - p.y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= 40)
                {
                    p.got = true;
                    if (p.kind == 1) { Emit(SwEv.SupplyGet, r.ax, r.ay - 18, 0, 1, Loc.T("블랙홀!")); OpenHole(); }
                    else { double s2 = Math.Min(4, r.max - r.fuel); if (s2 > 0) r.fuel += s2; Emit(SwEv.SupplyGet, r.ax, r.ay - 18, 0, 0, Loc.T("연료 +4초")); }
                    continue;
                }
            }
        }

        void Collector()
        {
            var hosts = R.junk.FindAll(d => !d.dead && IsHost(d.k) && d.att == Att.None);
            hosts.Sort((p, q) => Types[q.k].val.CompareTo(Types[p.k].val));
            for (int i = 0; i < Math.Min(6, hosts.Count); i++) { hosts[i].att = Att.Tag; Emit(SwEv.Ring, hosts[i].x, hosts[i].y, 18, 1); }
            Emit(SwEv.Collector, 0, 0, 0, 0, Loc.T("추심선이 압류 딱지를 붙이고 간다 — 부수면 빚이 준다"));
        }

        void Motion(double dt)
        {
            var o = Orbits[S.orbit];
            foreach (var d in R.junk)
            {
                if (d.dead) continue;
                if (d.sig == GateSig && d.free) { d.free = false; d.vx = d.vy = 0; d.capT = 0; }   // 🛰 관문은 늘 궤도에 — 자석 · 청소기 · 돌풍에 안 끌려 나온다
                d.fade = Math.Min(1, d.fade + dt * 1.4); d.hit = Math.Max(0, d.hit - dt); d.rot += d.vr * dt; if (d.sigCd > 0) d.sigCd -= dt;
                if (d.frz > 0) d.frz -= dt;
                if (d.free)
                {
                    d.x += d.vx * dt; d.y += d.vy * dt; if (d.sig != 9 && !d.convoy) { d.vx *= 1 - 0.9 * dt; d.vy *= 1 - 0.9 * dt; }   // 혜성 · 호송대는 줄지 않고 가로지른다
                    if (d.convoy && (d.x < -60 || d.x > 1020)) { d.dead = true; continue; }   // 🚚 놓친 호송대는 빠져나간다
                    if (d.capT > 0) { d.capT -= dt; if (d.capT <= 0) Recapture(d, o.bi, Bo); }
                }
                else
                {
                    if (d.frz <= 0) d.a += 0.12 * d.ws * dt * o.spin;           // 언 것은 궤도에 멈춰 선다
                    if (o.pull > 0 && d.tr <= 0 && d.rr > o.bi + 6) d.rr = Math.Max(o.bi + 6, d.rr - o.pull * dt * (Types[d.k].heavy ? 0.5 : 1));   // 목성 — 안쪽으로 끌린다
                    if (d.tr > 0) { double step = (25 + 20 * (d.ws - 0.92) / 0.16) * dt; if (Math.Abs(d.tr - d.rr) <= step) { d.rr = d.tr; d.tr = 0; } else d.rr += Math.Sign(d.tr - d.rr) * step; }
                    Place(d);
                }
            }
        }

        void Recapture(Junk d, double bi, double bo)
        {
            d.free = false; d.capT = 0; d.vx = d.vy = 0; d.tr = 0;
            d.a = Math.Atan2((d.y - EY) / Tilt, d.x - EX);
            double rad = Math.Sqrt((d.x - EX) * (d.x - EX) + (d.y - EY) / Tilt * (d.y - EY) / Tilt);
            // 띠 밖에서 붙잡히면 끝에 들러붙지 않고 띠 안 아무 자리로 천천히 내려간다 (바깥 테두리에만 쌓이던 것)
            if (rad > bo || rad < bi) { d.rr = Math.Max(bi * 0.8, Math.Min(bo + 60, rad)); d.tr = bi + Rnd() * (bo - bi); }
            else d.rr = rad;
        }

        void Pull(double dt)
        {
            var r = R; r.holdT += dt;
            double pr = PullR, pf = PullF;
            foreach (var d in r.junk)
            {
                if (d.dead) continue;                                               // 큰 잔해 · 장갑판도 끌려 든다 (09-26 밤)
                double dx = r.hx - d.x, dy = r.hy - d.y, dist = Math.Sqrt(dx * dx + dy * dy) + 1;
                if (dist > pr || d.sig == GateSig) continue;               // 🛰 관문은 안 끌려온다
                if (!d.free) { d.free = true; d.vx = d.vy = 0; }
                d.capT = 0;
                double f = (26000 * pf / (dist + 40) + PullBase * pf) / (Types[d.k].heavy || Types[d.k].big ? 2.5 : 1);
                d.vx += (dx / dist * f - dy / dist * f * 0.35) * dt;
                d.vy += (dy / dist * f + dx / dist * f * 0.35) * dt;
                d.vx *= 1 - 1.6 * dt; d.vy *= 1 - 1.6 * dt;              // 끌려 드는 동안 감겨 들어간다 (빙빙 돌기만 하지 않게)
                if (dist < 22)
                {
                    d.dead = true; r.packed.Add(d);
                    foreach (var l in new[] { d.link1, d.link2 })
                        if (l != null && !l.dead && !l.free) { l.free = true; l.vx = (r.hx - l.x) * 1.5; l.vy = (r.hy - l.y) * 1.5; }
                }
            }
            if (r.packed.Count > Cap)
            {
                // 붕괴 — 절반은 절반 값으로 흩어지고, 나머지는 그 자리에서 터진다
                int lost = r.packed.Count / 2;
                for (int i = 0; i < lost; i++) { var d = r.packed[0]; r.packed.RemoveAt(0); Pay(d, 2, Types[d.k].val * ValMult * 0.5, r.hx, r.hy, false); }
                Emit(SwEv.Collapse, r.hx, r.hy, lost);
                Release();
            }
        }

        void Release()
        {
            var r = R;
            r.holding = false; r.shots--;
            int n = r.packed.Count;
            r.packBest = Math.Max(r.packBest, n); M.bestPack = Math.Max(M.bestPack, n);
            if (n >= 50) Record("pack50", Loc.T("중력 폭탄 하나에 잔해 ") + n + Loc.T("개 — 제조사 「그렇게 쓰라고 만든 게 아닌데」"), Loc.T("민간 청소선이 중력 폭탄 하나로 잔해 ") + n + Loc.T("개를 한데 모아 터뜨렸다."));
            if (n >= 100) Record("pack100", Loc.T("잔해 ") + n + Loc.T("개를 한 점에 — 「작은 블랙홀을 봤다」"), Loc.T("지상 관측소에서도 한 점으로 빨려 드는 빛이 보였다고 한다."));
            double mult = 1 + n * PackK;
            dmgCat = 11;
            foreach (var d in r.packed)
            {
                d.x = r.hx + Rnd(-8, 8); d.y = r.hy + Rnd(-8, 8); d.dead = false;
                Kill(d, 2, mult);
            }
            dmgCat = -1;
            if (Lv("q_sling") > 0 && n >= 3) Sling(r.hx, r.hy, Math.Min(12, 3 + n / 4));
            r.packed.Clear();
            Emit(SwEv.Release, r.hx, r.hy, n, 0, n >= 6 ? n + Loc.T("개 압축 · ×") + mult.ToString("0.00") : null);
            dmgCat = 11; DoBlast(r.hx, r.hy, (60 + n * 2.5) * BlastK, false); dmgCat = -1;
            if (Lv("k_bh") > 0 && !r.twin && r.fuel > 0) { r.twin = true; r.holding = true; r.holdT = 0; r.chain = 0; r.tier = 0; r.shots++; Emit(SwEv.SkillReady, r.hx, r.hy, 2); }   // ◆ 쌍둥이 — 그 자리에 한 번 더
            else r.twin = false;
            // 모이다 만 것들은 궤도로 돌아간다
            foreach (var d in r.junk) if (d.free && !d.dead && d.capT <= 0) d.capT = 0.6;
        }

        // ★ 중력 새총 — 블랙홀이 모은 것을 사방으로 쏜다 (줄마다 맞은 것 피해)
        void Sling(double x, double y, int rays)
        {
            for (int k = 0; k < rays; k++)
            {
                double ang = k * Math.PI * 2 / rays + Rnd(-0.1, 0.1), ux = Math.Cos(ang), uy = Math.Sin(ang) * Tilt;
                double L = Bo * 0.9;
                for (int ji = 0, jn = R.junk.Count; ji < jn && ji < R.junk.Count; ji++) { var d = R.junk[ji]; if (d.dead) continue; double px = d.x - x, py = d.y - y, t = (px * ux + py * uy) / (ux * ux + uy * uy); if (t < 0 || t > L) continue; double qx = px - ux * t, qy = py - uy * t; if (qx * qx + qy * qy > 196) continue; Hit(d, Math.Max(1, RoundP(Pow * 2)), 2, false); }
                Emit(SwEv.Laser, x, y, 5, 4, null, x + ux * L, y + uy * L);
            }
        }
        // ★ 운석 호출 — 빽빽한 곳에 떨어져 크게 터진다
        void Meteor()
        {
            var r = R; Junk c = null; int bn = -1;
            for (int t = 0; t < 16 && r.junk.Count > 0; t++) { var q = r.junk[rng.Next(r.junk.Count)]; if (q.dead) continue; int n = 0; foreach (var d in r.junk) if (!d.dead && (d.x - q.x) * (d.x - q.x) + (d.y - q.y) * (d.y - q.y) < 6400) n++; if (n > bn) { bn = n; c = q; } }
            if (c == null) return;
            Emit(SwEv.Meteor, c.x, c.y);
            r.pend.Add(new Blast { x = c.x, y = c.y, t = 0.55, R = 80 });   // 한 방만 크게 (번지지 않는다 — 번지면 옛 연쇄처럼 판을 다 먹는다)
            if (M.flags == null || !M.flags.Contains("meteor1")) { M.flags.Add("meteor1"); AddNews(null, Loc.T("청소선이 부른 운석, 궤도를 쓸고 지나가"), Loc.T("궤도 청소부가 작은 운석을 끌어와 잔해 더미에 떨어뜨렸다. 지구 연료공사는 「보험 청구가 늘 것」이라며 울상.")); }
            if (Mk != null) Mk.GameEvent(Loc.T("운석 낙하 — 궤도 연료 수송로 마비"), Loc.T("청소선이 부른 운석 여파로 연료 수송이 늦어진다."), null, new[] { "fuel" }, 0.06f);
        }


    }
}
