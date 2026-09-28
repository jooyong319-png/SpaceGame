using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    // 📋 정해진 표 — 잔해 종류 · 행성 · 트리 칸 · 트리 자리 · 청구서 · 경력 · 영구 강화 · 배 · 의뢰 (09-28 나눔)
    public sealed partial class SweepSim
    {
        // ───────────────────────── 잔해 일곱 (§3-1)
        public struct DType { public string name; public int hp; public double val, r; public bool heavy, big; }
        public static readonly DType[] Types =
        {
            new DType { name = Loc.T("조각"),      hp = 2,  val = 1,   r = 4 },
            new DType { name = Loc.T("죽은 위성"), hp = 4,  val = 6,   r = 7 },
            new DType { name = Loc.T("로켓 동체"), hp = 7,  val = 14,  r = 9,  heavy = true },
            new DType { name = Loc.T("금고 위성"), hp = 6,  val = 40,  r = 8 },
            new DType { name = Loc.T("연료통"),    hp = 1,  val = 0,   r = 6 },
            new DType { name = Loc.T("폭발 탱크"), hp = 1,  val = 2,   r = 6 },
            new DType { name = Loc.T("큰 잔해"),   hp = 30, val = 180, r = 17, big = true },
        };
        public const int Chip = 0, Sat = 1, Rocket = 2, Vault = 3, Fuel = 4, Tank = 5, Big = 6;
        static bool IsHost(int k) => k == Sat || k == Rocket || k == Vault || k == Big;

        // ───────────────────────── 궤도 셋 (§4-1)
        // 🪐 행성 다섯 — 궤도 자리 (09-23 사장님 「여러 행성을 청소해 주는 느낌」). 청구서를 갚으면 허가증이 팔리고, 돈 내고 연다
        // spin 궤도 도는 빠르기 · hp 잔해 체력 배율 · pull 안쪽으로 끌리는 힘(목성) · gap 띠 가운데 빈 틈(토성 두 겹 고리) · storm 모래 폭풍(화성, 화면만)
        public struct OrbitDef { public string name, desc; public double bi, bo, mult, att, spin, hp, pull, gap, permit; public int sell; public bool storm; public double[] mix; public int nMin, nMax; public int[] forms, events; }
        public static readonly OrbitDef[] Orbits =
        {
            new OrbitDef { name = Loc.T("지구"), desc = Loc.T("기본 궤도"),                         bi = 150, bo = 296, mult = 1, att = 0.10, spin = 1,    hp = 1,   sell = 0, permit = 0,       mix = new double[] { 60, 22, 4, 2, 5, 4, 1.2 },  nMin = 127, nMax = 212, forms = new[] { 0, 0 },       events = new[] { 0, 1 } },
            new OrbitDef { name = Loc.T("달"),   desc = Loc.T("느린 궤도 · 금고 위성이 많다"),       bi = 118, bo = 277, mult = 1.5, att = 0.15, spin = 0.6,  hp = 1.1, sell = 2, permit = 800,     mix = new double[] { 48, 22, 6, 9, 5, 4, 1.2 },  nMin = 144, nMax = 244, forms = new[] { 0, 1 },       events = new[] { 0, 3 } },
            new OrbitDef { name = Loc.T("화성"), desc = Loc.T("모래 폭풍 · 얼음 껍질"),             bi = 132, bo = 314, mult = 2.2, att = 0.25, spin = 1.1,  hp = 1.3, sell = 3, permit = 15000,    storm = true, mix = new double[] { 48, 22, 12, 3, 5, 7, 1.5 }, nMin = 172, nMax = 316, forms = new[] { 1, 2, 0 }, events = new[] { 2, 1 } },
            new OrbitDef { name = Loc.T("목성"), desc = Loc.T("중력이 잔해를 안쪽에 모은다 · 장갑판"), bi = 188, bo = 357, mult = 4.5, att = 0.30, spin = 1.3,  hp = 1.6, sell = 5, permit = 2000000,  pull = 9, mix = new double[] { 40, 18, 10, 6, 5, 8, 2.5 }, nMin = 238, nMax = 392, forms = new[] { 3, 4, 1 }, events = new[] { 3, 4 } },
            new OrbitDef { name = Loc.T("토성"), desc = Loc.T("두 겹 고리 · 케이블 망"),            bi = 150, bo = 345, mult = 6.5, att = 0.30, spin = 0.9,  hp = 2.0, sell = 6, permit = 6000000, gap = 0.28, mix = new double[] { 38, 18, 8, 8, 5, 6, 3 }, nMin = 257, nMax = 415, forms = new[] { 2, 2, 3, 4 }, events = new[] { 3, 4 } },
            new OrbitDef { name = Loc.T("소행성대"), desc = Loc.T("암석 무리 · 단단한 잔해 · 광석"),     bi = 140, bo = 330, mult = 3.2, att = 0.28, spin = 1.2,  hp = 1.45, sell = 4, permit = 200000,   mix = new double[] { 44, 20, 12, 4, 5, 7, 2 },   nMin = 205, nMax = 350, forms = new[] { 1, 3, 0 }, events = new[] { 2, 3 } },
            new OrbitDef { name = Loc.T("천왕성"), desc = Loc.T("옆으로 누운 궤도 · 얼음 결정"),       bi = 180, bo = 380, mult = 10, att = 0.35, spin = 0.8,  hp = 2.8, sell = 8, permit = 15000000, pull = 6, mix = new double[] { 34, 18, 10, 9, 5, 8, 4 }, nMin = 280, nMax = 440, forms = new[] { 2, 3, 4 }, events = new[] { 3, 4 } },
            new OrbitDef { name = Loc.T("해왕성"), desc = Loc.T("초속 폭풍 · 무거운 잔해"),           bi = 175, bo = 390, mult = 15, att = 0.38, spin = 1.4,  hp = 3.8, sell = 10, permit = 30000000, storm = true, mix = new double[] { 32, 16, 10, 10, 5, 9, 5 }, nMin = 300, nMax = 460, forms = new[] { 3, 4, 2 }, events = new[] { 4, 3 } },
            new OrbitDef { name = Loc.T("카이퍼 벨트"), desc = Loc.T("태양계 끝 · 고대 탐사선 · 혜성"),   bi = 150, bo = 400, mult = 24, att = 0.40, spin = 0.6,  hp = 5.2, sell = 14, permit = 60000000, gap = 0.2, mix = new double[] { 30, 16, 10, 12, 5, 9, 6 }, nMin = 320, nMax = 480, forms = new[] { 4, 3, 2, 1 }, events = new[] { 3, 4 } },
            new OrbitDef { name = Loc.T("오르트 구름"), desc = Loc.T("얼음 혜성 떼 · 단단하고 느리다"),   bi = 155, bo = 410, mult = 24, att = 0.42, spin = 0.5,  hp = 7.2, sell = 18, permit = 60000000, mix = new double[] { 30, 16, 10, 12, 5, 9, 6 }, nMin = 320, nMax = 480, forms = new[] { 4, 3, 2, 1 }, events = new[] { 3, 4 } },
            new OrbitDef { name = Loc.T("태양권 계면"), desc = Loc.T("태양풍이 부딪히는 경계 · 잔해가 출렁인다"),   bi = 160, bo = 420, mult = 24, att = 0.44, spin = 1.6,  hp = 9.6, sell = 22, permit = 60000000, storm = true, mix = new double[] { 30, 16, 10, 12, 5, 9, 6 }, nMin = 320, nMax = 480, forms = new[] { 4, 3, 2, 1 }, events = new[] { 3, 4 } },
            new OrbitDef { name = Loc.T("성간 공간"), desc = Loc.T("태양 빛이 닿지 않는 곳 · 드문드문하지만 값이 가장 크다"),   bi = 165, bo = 430, mult = 24, att = 0.46, spin = 0.4,  hp = 13, sell = 28, permit = 60000000, mix = new double[] { 30, 16, 10, 12, 5, 9, 6 }, nMin = 320, nMax = 480, forms = new[] { 4, 3, 2, 1 }, events = new[] { 3, 4 } },
        };
        public static readonly string[] FormNames = { Loc.T("무리"), Loc.T("탱크 사슬"), Loc.T("케이블 망"), Loc.T("호송대"), Loc.T("난파 구역") };
        public static readonly string[] EventNames = { Loc.T("연료 보급선"), Loc.T("충돌 사고"), Loc.T("파편 폭풍"), Loc.T("금고 호송대"), Loc.T("대충돌") };
        public static readonly string[] EventHint = { Loc.T("왼쪽에서 연료통 하나 · 부수면 연료가 찬다"), Loc.T("오른쪽 위에 파편 서른 · 뭉쳤을 때 쓸면 연쇄"), Loc.T("왼쪽에서 고철이 쏟아진다 · 길목을 막아라"), Loc.T("금고 위성 줄이 지나간다 · 놓치기 전에"), Loc.T("오른쪽 위에 파편 예순 · 폭탄 위성도 섞였다") };   // 09-26 사장님 「뭐가 되는 건데?」
        public const double FuelIdle = 0.5, ShotFuel = 0.5, ClickFuel = 0.4, VolleyFuel = 2;   // ⛽ 연료 — 가만히 · 쏠 때(간격 비례) · 클릭 한 방 · 전탄
        bool TargetNear(double x, double y)
        {   // 조준점 근처에 부술 잔해가 있나 — 없으면 주 무기가 쉰다
            double lim = Math.Max(Math.Max(ClawR, PickR), 30) + 24;
            foreach (var d in R.junk) { if (d.dead || d.fade < 0.3) continue; double dx = d.x - x, dy = d.y - y; if (dx * dx + dy * dy < lim * lim) return true; }
            return false;
        }

        // ───────────────────────── 트리 스물네 칸 (§8-2)
        public struct Node { public string id, branch, name, desc; public string[] par; public int seg, max; public double first, mult; public int depth, lane; }
        // 🔴 정비고 네 칸 × 아홉 = 36칸 (사장님 09-23: "강화하는 것도 더 넓히고 더 많게"). depth = 행(0~4) · lane = 열(0~4)
        public static readonly Node[] Nodes =
        {
            N("c_pow", "claw", Loc.T("빔 위력"), Loc.T("한 방 피해 +1 (단계마다) — 단단한 잔해를 덜 쳐도 부순다"), new string[0], 1, 3, 1.6, 12, 0, 2),
            N("c_rad", "claw", Loc.T("빔 범위"), Loc.T("빔이 한 점이 아니라 원으로 — 반지름 25에서 단계마다 +5 · 원 안이 한꺼번에 맞는다"), new[] { "c_pow" }, 1, 9, 1.7, 8, 1, 3),
            N("c_spd", "claw", Loc.T("빔 증폭"), Loc.T("빔 화력 +8% (단계마다)"), new[] { "c_pow" }, 2, 40, 1.6, 7, 1, 1),
            N("c_fuel", "claw", Loc.T("연료 탱크"), Loc.T("연료 용량 +3% (단계마다) — 쏠 수 있는 양 · 판 길이가 늘어난다"), new[] { "c_rad" }, 1, 20, 1.6, 10, 2, 4),
            N("c_crit", "claw", Loc.T("치명타"), Loc.T("치명 확률 +5% (단계마다) — 치명이면 피해 ×3"), new[] { "c_spd" }, 3, 600, 1.6, 6, 3, 0),
            N("c_double", "claw", Loc.T("한 발 더"), Loc.T("쏠 때 10% 확률로 같은 자리에 한 발 더 (단계마다 +10%) · 연료는 한 발 값만"), new[] { "c_spd" }, 3, 900, 1.7, 5, 3, 2),
            N("c_magnet", "claw", Loc.T("견인 빔"), Loc.T("조준점 둘레(단계마다 +20)의 작은 조각을 끌어당긴다"), new[] { "c_rad", "c_fuel" }, 3, 1200, 1.8, 3, 3, 4),
            N("c_over", "claw", Loc.T("과부하"), Loc.T("연료가 5초 남으면 두 배 빠르게 쏜다"), new[] { "c_crit", "c_double" }, 4, 3000, 1, 1, 4, 1),
            N("d_n", "drone", Loc.T("드론 격납고"), Loc.T("드론이 생긴다 — 첫 칸 두 대, 그 뒤 한 대씩 더 · 한 방에 부서지는 잔해를 알아서 부순다"), new string[0], 1, 60, 2.2, 8, 0, 2),
            N("d_spd", "drone", Loc.T("드론 속도"), Loc.T("드론이 한 번 줍고 다시 가는 간격 −10% (단계마다)"), new[] { "d_n" }, 2, 300, 1.6, 6, 1, 1),
            N("d_reach", "drone", Loc.T("드론 거리"), Loc.T("드론이 잔해를 찾는 거리 80에서 +15 (단계마다)"), new[] { "d_n" }, 3, 660, 1.6, 5, 1, 3),
            N("d_mag", "drone", Loc.T("드론 수거"), Loc.T("드론이 부순 잔해 값 +25% (단계마다)"), new[] { "d_spd" }, 3, 900, 1.6, 5, 2, 0),
            N("d_sig", "drone", Loc.T("신호 증폭"), Loc.T("깜빡이는 신호기가 붙은 잔해를 부수면 드론이 그리로 몰려든다 — 3초에서 +2초 (단계마다)"), new[] { "d_spd", "d_reach" }, 3, 700, 1.7, 3, 2, 2),
            N("d_grade", "drone", Loc.T("드론 등급"), Loc.T("드론이 더 큰 잔해도 한 방에 줍는다 — 조각 → 죽은 위성 → 금고 위성 → 로켓 동체"), new[] { "d_reach" }, 5, 4500, 2.0, 3, 2, 4),
            N("d_fix", "drone", Loc.T("급유 드론"), Loc.T("드론이 연료를 채워 준다 — 연료 +1초 (단계마다)"), new[] { "d_mag" }, 4, 2000, 1.8, 3, 3, 1),
            N("d_pair", "drone", Loc.T("편대"), Loc.T("드론이 한 번에 두 개씩 친다"), new[] { "d_mag", "d_grade" }, 6, 18000, 1, 1, 3, 3),
            N("d_fact", "drone", Loc.T("드론 공장"), Loc.T("드론 +1대 (단계마다)"), new[] { "d_pair" }, 6, 30000, 2.5, 2, 4, 2),
            N("b_n", "bh", Loc.T("블랙홀"), Loc.T("쏠 때 가끔 블랙홀이 저절로 열려 둘레 잔해를 빨아들였다가 터진다 — 단계마다 더 자주"), new string[0], 1, 400, 2.6, 4, 0, 1),
            N("c_find", "bh", Loc.T("연료 보급"), Loc.T("판마다 연료 보급선이 궤도를 돌며 나간다 (단계마다 +1) — 조준점을 대면 연료 +4초, 놓치면 사라진다"), new string[0], 3, 240, 1.8, 5, 0, 3),
            N("s_speed", "bh", Loc.T("블랙홀 빈도"), Loc.T("블랙홀이 열리는 확률 +0.25% (단계마다)"), new[] { "b_n" }, 3, 500, 1.6, 4, 1, 2),
            N("b_pr", "bh", Loc.T("흡입 반경"), Loc.T("블랙홀이 빨아들이는 범위 260에서 +30 (단계마다) — 큰 잔해 · 장갑판도 빨려 든다"), new[] { "b_n" }, 3, 600, 1.6, 6, 1, 0),
            N("b_cap", "bh", Loc.T("블랙홀 용량"), Loc.T("블랙홀이 터지기 전까지 삼킬 수 있는 잔해 40개 → 단계마다 +15개"), new[] { "b_n" }, 3, 660, 1.6, 6, 2, 1),
            N("b_pf", "bh", Loc.T("흡입 세기"), Loc.T("빨아들이는 힘 +25% (단계마다) — 멀리 있는 잔해도 빨리 끌려온다"), new[] { "b_pr" }, 4, 1800, 1.6, 5, 2, 0),
            N("b_br", "bh", Loc.T("폭발 반경"), Loc.T("모든 폭발(블랙홀 · 무기 · 폭탄) 범위 +15% (단계마다)"), new[] { "b_cap" }, 4, 1800, 1.6, 6, 3, 1),
            N("b_chain", "bh", Loc.T("폭발 번짐"), Loc.T("무기 폭발(기뢰 · 분열탄 · 번개 각성 …)이 옆으로 또 터질 확률 +7% · 번지는 한도 +15 (단계마다)"), new[] { "b_br" }, 4, 2700, 1.6, 8, 3, 3),
            N("b_pack", "bh", Loc.T("압축 보너스"), Loc.T("블랙홀은 많이 삼킬수록 터질 때 값이 붙는다 — 하나마다 +2%, 단계마다 +1.2% 더"), new[] { "b_chain", "b_pf" }, 5, 7500, 1.7, 5, 4, 2),
            N("o_wide", "eco", Loc.T("궤도 확장"), Loc.T("궤도 폭 +10% (단계마다) — 잔해도 그만큼 는다"), new string[0], 1, 20, 1.9, 6, 0, 4),
            N("e_val", "eco", Loc.T("고철 시세"), Loc.T("부순 것 값 ×1.25 (단계마다 곱한다) — 지구에선 한 칸, 관문을 부술 때마다 한 칸 더"), new string[0], 1, 120, 1.9, 10, 0, 2),
            N("e_vault", "eco", Loc.T("금고 감별"), Loc.T("금고 위성이 나올 확률 +50% (단계마다)"), new[] { "e_val" }, 2, 360, 1.6, 6, 1, 0),
            N("e_att", "eco", Loc.T("부착물 감별"), Loc.T("잔해에 돈 주머니 · 기폭 장치 같은 부착물이 붙을 확률 +40% (단계마다)"), new[] { "e_val" }, 3, 1200, 1.7, 5, 1, 2),
            N("e_quest", "eco", Loc.T("의뢰 게시판"), Loc.T("출동마다 의뢰 카드 — 해내면 판 수입이 오른다 · 단계마다 보상 +10%"), new[] { "e_val" }, 1, 250, 2.4, 4, 1, 4),
            N("e_talk", "eco", Loc.T("청구서 협상"), Loc.T("청구서 기한 +1판 (단계마다)"), new[] { "e_vault" }, 4, 3600, 3.0, 2, 2, 0),
            N("e_tip", "eco", Loc.T("제보망"), Loc.T("블랙박스(특종) 나올 확률 +10% (단계마다)"), new[] { "e_att" }, 3, 1500, 1.8, 3, 2, 2),
            N("e_save", "eco", Loc.T("적금"), Loc.T("판이 끝날 때 가진 돈에 이자 2% (단계마다) — 한 판 벌이만큼까지"), new[] { "e_val" }, 4, 2500, 1.9, 5, 2, 4),
            N("e_guard", "eco", Loc.T("상환 조절"), Loc.T("판 벌이에서 빚 갚는 데 떼 가는 몫 30% → 20%"), new[] { "e_talk", "e_tip" }, 5, 9000, 1, 1, 3, 1),
            N("e_used", "eco", Loc.T("중고 거래"), Loc.T("정비고 모든 칸 값 −5%"), new[] { "e_save" }, 4, 6000, 2.0, 3, 3, 3),
            // 📈 증권 줄기 (09-23 사장님 「진짜 주식처럼 · 정비소에서 이점을」) — 맨 끝에 붙여 옛 저장의 칸 순서를 안 흔든다 (경매 칸 자리를 그대로 썼다)
            N("a_open", "eco", Loc.T("증권 계좌"), Loc.T("궤도 증권이 열린다 — 조종실에서 사고팔고, 출동 중엔 S 로 시세만 본다"), new[] { "e_val" }, 2, 500, 1, 1, 1, 1),
            N("a_auto", "eco", Loc.T("개미의 기도"), Loc.T("내가 산 종목이 조금씩 더 오르는 쪽으로 움직인다"), new[] { "a_open" }, 2, 800, 1, 1, 1, 1),
            N("a_read", "eco", Loc.T("내부자 정보"), Loc.T("다음 속보를 미리 안다 — 시간 · 업종 · 제목"), new[] { "a_auto" }, 2, 1500, 2.5, 3, 1, 1),
            N("a_ins", "eco", Loc.T("행운의 부적"), Loc.T("내 종목에 걸릴 나쁜 속보를 피해 간다 (60 · 70 · 80%)"), new[] { "a_read" }, 3, 4000, 2.5, 3, 1, 1),
            N("a_big", "eco", Loc.T("큰손 계좌"), Loc.T("수수료가 줄고 배당이 붙는다"), new[] { "a_ins" }, 4, 20000, 3, 3, 1, 1),
            // 🪐 항로 (09-24 사장님 「청구서로 늘리는 방식 말고 정비고에서 통일」) — 옛 허가증. 값은 허가증 값 그대로
            N("p_moon", "route", Loc.T("달 항로"), Loc.T("달 궤도에 갈 수 있다 — 값 ×3 · 느린 궤도 · 금고 위성이 많다"), new string[0], 1, 800, 1, 1, 0, 0),
            N("p_mars", "route", Loc.T("화성 항로"), Loc.T("화성 궤도 — 값 ×9 · 모래 폭풍 · 얼음 껍질 · 큰 잔해"), new[] { "p_moon" }, 1, 15000, 1, 1, 0, 0),
            N("p_jup", "route", Loc.T("목성 항로"), Loc.T("목성 궤도 — 값 ×81 · 중력이 안쪽으로 모은다 · 장갑판"), new[] { "p_belt" }, 1, 2000000, 1, 1, 0, 0),
            N("p_sat", "route", Loc.T("토성 항로"), Loc.T("토성 궤도 — 값 ×243 · 두 겹 고리 · 케이블 망"), new[] { "p_jup" }, 1, 6000000, 1, 1, 0, 0),
            N("p_belt", "route", Loc.T("소행성대 항로"), Loc.T("화성과 목성 사이 소행성대 — 값 ×27 · 단단한 암석 · 광석"), new[] { "p_mars" }, 1, 200000, 1, 1, 0, 0),
            N("p_ura", "route", Loc.T("천왕성 항로"), Loc.T("천왕성 궤도 — 값 ×729 · 옆으로 누운 궤도 · 얼음 결정"), new[] { "p_sat" }, 1, 15000000, 1, 1, 0, 0),
            N("p_nep", "route", Loc.T("해왕성 항로"), Loc.T("해왕성 궤도 — 값 ×2187 · 초속 폭풍 · 무거운 잔해"), new[] { "p_ura" }, 1, 30000000, 1, 1, 0, 0),
            N("p_kui", "route", Loc.T("카이퍼 벨트 항로"), Loc.T("태양계 끝 카이퍼 벨트 — 값 ×6561 · 고대 탐사선 · 혜성"), new[] { "p_nep" }, 1, 60000000, 1, 1, 0, 0),
            // ✦ 네 번째 고리 — 곱하기 · 무기 3단계. 목성 항로를 사야 열린다 (09-24 레벨 설계 2막 「외행성 면허」)
            N("m_claw", "claw", Loc.T("✦ 과충전 포신"), Loc.T("화력 ×1.5 (단계마다 곱한다) (목성 항로 뒤)"), new[] { "k_claw" }, 1, 10000000, 6, 3, 0, 0),
            N("m_crit", "claw", Loc.T("✦ 정밀 조준"), Loc.T("치명타 피해 ×3 → ×4 (목성 항로 뒤)"), new[] { "k_claw" }, 1, 16000000, 6, 2, 0, 0),
            N("m_fuel", "claw", Loc.T("✦ 연료 탱크 증설"), Loc.T("연료 +25% (목성 항로 뒤)"), new[] { "k_claw" }, 1, 8000000, 5, 2, 0, 0),
            N("m_drone", "drone", Loc.T("✦ 드론 공장 확장"), Loc.T("드론이 부순 잔해 값 ×1.5 (목성 항로 뒤)"), new[] { "k_drone" }, 1, 10000000, 6, 3, 0, 0),
            N("m_dcount", "drone", Loc.T("✦ 드론 증원"), Loc.T("드론 +2대 (목성 항로 뒤)"), new[] { "k_drone" }, 1, 12000000, 6, 2, 0, 0),
            N("m_bh", "bh", Loc.T("✦ 사건의 지평선"), Loc.T("블랙홀 발동 +30% · 범위 +15% (목성 항로 뒤)"), new[] { "k_bh" }, 1, 10000000, 6, 3, 0, 0),
            N("m_val", "eco", Loc.T("✦ 시세 조작"), Loc.T("모든 값 ×1.5 (곱한다) (목성 항로 뒤)"), new[] { "k_eco" }, 1, 12000000, 6, 3, 0, 0),
            N("m_route", "route", Loc.T("✦ 심우주 항법"), Loc.T("행성마다 붙는 값 배수(×3 · ×9 …)가 ×1.25 (목성 항로 뒤)"), new[] { "k_route" }, 1, 16000000, 6, 3, 0, 0),
            N("w_laser_x", "arm", Loc.T("✦ 레이저 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_laser_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_chain_x", "arm", Loc.T("✦ 번개 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_chain_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_vac_x", "arm", Loc.T("✦ 진공 청소기 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_vac_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_mine_x", "arm", Loc.T("✦ 기뢰 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_mine_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_frz_x", "arm", Loc.T("✦ 냉동 빔 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_frz_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_clus_x", "arm", Loc.T("✦ 분열탄 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_clus_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_mag_x", "arm", Loc.T("✦ 자석 펄스 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_mag_e" }, 1, 8000000, 1, 1, 0, 0),
            N("w_rail_x", "arm", Loc.T("✦ 레일건 3단계"), Loc.T("발동 확률 ×2 · 위력 ×2 (목성 항로 뒤)"), new[] { "w_rail_e" }, 1, 8000000, 1, 1, 0, 0),
            // ⚔ 무기고 (09-24 설계서 2단계) — 지금 빔 칸(화력 · 크기 · 연사 · 치명 · 두 번)은 모든 무기 공통
            N("w_hub", "arm", Loc.T("무기고"), Loc.T("처음부터 열려 있다 — 여기서 뻗은 무기를 사면 쏠 때 확률로 함께 터진다"), new string[0], 1, 300, 1, 1, 0, 0),
            N("w_laser", "arm", Loc.T("레이저"), Loc.T("공격 때 12% — 조준점을 0.7초 동안 태운다"), new[] { "w_hub" }, 1, 2500, 1, 1, 0, 0),
            N("w_laser_u", "arm", Loc.T("레이저 강화"), Loc.T("첫 칸: 확률 ×1.5 · 태우는 원 +40% / 둘째 칸: 계속 쬘수록 뜨거워진다 (최대 +80%)"), new[] { "w_laser" }, 1, 6000, 4, 2, 0, 0),
            N("w_laser_a", "arm", Loc.T("레이저 각성"), Loc.T("태우는 원 +50% · 장갑판도 녹인다"), new[] { "w_laser_u" }, 1, 60000, 1, 1, 0, 0),
            N("w_chain", "arm", Loc.T("번개"), Loc.T("공격 때 10% — 조준점에서 옆으로 다섯 번 튄다"), new[] { "w_laser" }, 1, 5000, 1, 1, 0, 0),
            N("w_chain_u", "arm", Loc.T("번개 강화"), Loc.T("첫 칸: 확률 ×1.5 · 두 번 더 튄다 / 둘째 칸: 튈수록 세진다"), new[] { "w_chain" }, 1, 9000, 4, 2, 0, 0),
            N("w_chain_a", "arm", Loc.T("번개 각성"), Loc.T("튄 자리마다 작은 폭발 — 연쇄로 이어진다"), new[] { "w_chain_u" }, 1, 90000, 1, 1, 0, 0),
            // 🔩 부품 거래 · ◆ 핵심 칸 (09-24 설계서 3단계)
            N("e_shop", "eco", Loc.T("부품 거래"), Loc.T("부품 가게가 열린다 — 청소선 부품 칸 다섯에 사서 끼운다 · 진열은 출동마다 바뀐다"), new[] { "e_save" }, 1, 1500, 1, 1, 0, 0),
            N("k_claw", "claw", Loc.T("◆ 과열 사격"), Loc.T("모든 무기 화력 +40% — 대신 연료 −15%"), new[] { "c_over" }, 1, 30000, 1, 1, 0, 0),
            N("k_drone", "drone", Loc.T("◆ 벌떼"), Loc.T("드론 +4대 — 대신 드론이 부순 값 −25%"), new[] { "d_fact" }, 1, 60000, 1, 1, 0, 0),
            N("k_bh", "bh", Loc.T("◆ 쌍둥이 블랙홀"), Loc.T("블랙홀이 터지면 그 자리에 한 번 더 열린다 — 대신 여는 확률 −30%"), new[] { "s_speed" }, 1, 40000, 1, 1, 0, 0),
            N("k_eco", "eco", Loc.T("◆ 큰손"), Loc.T("모든 값 +25% — 대신 청구서 +10%"), new[] { "e_used" }, 1, 80000, 1, 1, 0, 0),
            N("k_route", "route", Loc.T("◆ 궤도 공명"), Loc.T("행성 값 배수 +15% — 대신 잔해 체력 +20%"), new[] { "p_sat" }, 1, 150000, 1, 1, 0, 0),
            N("w_slot2", "arm", Loc.T("◆ 무기 공명"), Loc.T("모든 무기의 발동 확률 ×1.5"), new[] { "w_chain" }, 1, 50000, 1, 1, 0, 0),
            // ★ 신기한 칸 (09-24 설계서 4단계) — 판 밖(주식 · 뉴스 · 행성)과 판을 잇는다
            N("q_insider", "eco", Loc.T("★ 내부자 거래"), Loc.T("공격이 맞을 때 가끔(0.5%) 내가 산 종목 하나가 +1% — 「누군가 청소선을 보고 샀다」"), new[] { "a_read" }, 1, 30000, 1, 1, 0, 0),
            N("q_rage", "eco", Loc.T("★ 물린 개미의 분노"), Loc.T("내 주식이 손해일수록 화력이 오른다 (손해 % 만큼 · 최대 +50%)"), new[] { "a_big" }, 1, 120000, 1, 1, 0, 0),
            N("q_front", "eco", Loc.T("★ 1면 조작"), Loc.T("출동이 끝나면 궤도일보 1면을 둘 중에서 고른다 — 고른 기사가 주가를 움직인다"), new[] { "e_tip" }, 1, 50000, 1, 1, 0, 0),
            N("q_debt", "eco", Loc.T("★ 빚쟁이의 근성"), Loc.T("빚이 많을수록 화력이 오른다 (최대 +15%)"), new[] { "e_guard" }, 1, 60000, 1, 1, 0, 0),
            N("q_meteor", "bh", Loc.T("★ 운석 호출"), Loc.T("잔해를 80개 부술 때마다 운석이 떨어져 크게 터진다 — 궤도일보 1면 · 연료공사 주가 ↓"), new[] { "b_chain" }, 1, 150000, 1, 1, 0, 0),
            N("q_sling", "bh", Loc.T("★ 중력 새총"), Loc.T("블랙홀이 터질 때 빨아들인 잔해를 사방으로 쏘아 보낸다"), new[] { "b_pack" }, 1, 250000, 1, 1, 0, 0),
            N("q_tour", "route", Loc.T("★ 관광 명소"), Loc.T("한 판에 연쇄 100을 넘기면 관광객이 몰린다 — 토성 고리 관광 주가 ↑"), new[] { "p_sat" }, 1, 3000000, 1, 1, 0, 0),
            N("q_rock", "route", Loc.T("★ 떠돌이 소행성"), Loc.T("가끔 소행성이 궤도에 끼어든다 — 부수면 열쇠(40%) 또는 돈 뭉치"), new[] { "p_jup" }, 1, 500000, 1, 1, 0, 0),
            N("q_gold", "hull", Loc.T("★ 황금 잔해"), Loc.T("가끔 금빛 잔해가 섞인다 — 값 ×3 · 부수면 공짜 즉석 복권 (한 판 3장까지)"), new[] { "o_wide" }, 1, 40000, 1, 1, 0, 0),
            N("q_lazy", "drone", Loc.T("★ 연타 장인"), Loc.T("0.3초 안에 다시 누를 때마다 화력 +3% (최대 +30%) · 1초 손을 떼면 식는다"), new[] { "d_fix" }, 1, 80000, 1, 1, 0, 0),
            // ⚔ 무기 여섯 더 (09-24 설계서 4단계)
            N("w_vac", "arm", Loc.T("진공 청소기"), Loc.T("공격 때 8% — 조준점에 소용돌이, 0.7초 동안 빨아들인다 · 삼킨 것은 값 +30%"), new[] { "w_chain" }, 1, 12000, 1, 1, 0, 0),
            N("w_vac_u", "arm", Loc.T("진공 청소기 강화"), Loc.T("첫 칸: 확률 ×1.5 · 소용돌이 +30% / 둘째 칸: 삼킨 것 값 +60%"), new[] { "w_vac" }, 1, 24000, 4, 2, 0, 0),
            N("w_vac_a", "arm", Loc.T("진공 청소기 각성"), Loc.T("가득 차면(25개) 압축 고철탄을 조준점에 쏜다"), new[] { "w_vac_u" }, 1, 120000, 1, 1, 0, 0),
            N("w_mine", "arm", Loc.T("기뢰"), Loc.T("공격 때 8% — 조준 자리에 기뢰를 깐다 · 잔해가 지나가면 쾅 (3개까지)"), new[] { "w_vac" }, 1, 30000, 1, 1, 0, 0),
            N("w_mine_u", "arm", Loc.T("기뢰 강화"), Loc.T("첫 칸: 확률 ×1.5 · 기뢰 +2 / 둘째 칸: 폭발 반경 +40%"), new[] { "w_mine" }, 1, 60000, 4, 2, 0, 0),
            N("w_mine_a", "arm", Loc.T("기뢰 각성"), Loc.T("기뢰끼리 레이저 울타리로 이어진다"), new[] { "w_mine_u" }, 1, 300000, 1, 1, 0, 0),
            N("w_frz", "arm", Loc.T("냉동 빔"), Loc.T("공격 때 8% — 조준점에 서리 원, 0.7초 동안 얼린다 · 언 것은 두 배 · 부서지면 산산조각"), new[] { "w_mine" }, 1, 80000, 1, 1, 0, 0),
            N("w_frz_u", "arm", Loc.T("냉동 빔 강화"), Loc.T("첫 칸: 확률 ×1.5 · 더 오래 언다 / 둘째 칸: 언 것 ×2.5"), new[] { "w_frz" }, 1, 160000, 4, 2, 0, 0),
            N("w_frz_a", "arm", Loc.T("냉동 빔 각성"), Loc.T("산산조각 파편도 옆을 얼린다 (끝없는 연쇄)"), new[] { "w_frz_u" }, 1, 800000, 1, 1, 0, 0),
            N("w_clus", "arm", Loc.T("분열탄"), Loc.T("공격 때 6% — 조준점에 떨어져 파편 여섯으로 흩어진다"), new[] { "w_frz" }, 1, 250000, 1, 1, 0, 0),
            N("w_clus_u", "arm", Loc.T("분열탄 강화"), Loc.T("첫 칸: 확률 ×1.5 · 파편 +3 / 둘째 칸: 파편이 가까운 잔해를 노린다"), new[] { "w_clus" }, 1, 500000, 4, 2, 0, 0),
            N("w_clus_a", "arm", Loc.T("분열탄 각성"), Loc.T("파편이 한 번 더 셋으로 갈라진다"), new[] { "w_clus_u" }, 1, 2500000, 1, 1, 0, 0),
            N("w_mag", "arm", Loc.T("자석 펄스"), Loc.T("공격 때 5% — 주변 잔해를 한 점으로 끌어모은 뒤 쾅"), new[] { "w_clus" }, 1, 800000, 1, 1, 0, 0),
            N("w_mag_u", "arm", Loc.T("자석 펄스 강화"), Loc.T("첫 칸: 확률 ×1.5 · 끌림 반경 +40% / 둘째 칸: 모인 만큼 크게 터진다"), new[] { "w_mag" }, 1, 1600000, 4, 2, 0, 0),
            N("w_mag_a", "arm", Loc.T("자석 펄스 각성"), Loc.T("모인 자리에 블랙홀이 열린다"), new[] { "w_mag_u" }, 1, 8000000, 1, 1, 0, 0),
            N("w_rail", "arm", Loc.T("레일건"), Loc.T("공격 때 4% — 한 줄로 관통, 엄청 세고 장갑도 뚫는다"), new[] { "w_mag" }, 1, 3000000, 1, 1, 0, 0),
            N("w_rail_u", "arm", Loc.T("레일건 강화"), Loc.T("첫 칸: 확률 ×1.5 / 둘째 칸: 뚫을수록 +15%"), new[] { "w_rail" }, 1, 6000000, 4, 2, 0, 0),
            N("w_rail_a", "arm", Loc.T("레일건 각성"), Loc.T("띠 끝에서 튕겨 한 번 더 쏜다"), new[] { "w_rail_u" }, 1, 30000000, 1, 1, 0, 0),
            // ◆ 교차 핵심 (두 방향을 다 키워야 닿는다 · 열쇠) · ∞ 무한 칸 (3막의 돈이 계속 쓸 곳)
            N("x_claw_arm", "claw", Loc.T("◆ 교차: 사격 통제"), Loc.T("모든 무기 치명 +10% · 치명타는 ×4 (청소선 × 무기고)"), new[] { "c_magnet", "w_hub" }, 1, 400000, 1, 1, 0, 0),
            N("x_arm_drone", "drone", Loc.T("◆ 교차: 드론 사수"), Loc.T("드론이 쏠 때도 블랙홀이 열리고 내부자 거래가 터진다 · 드론 피해 ×2 (무기고 × 드론)"), new[] { "d_grade", "w_hub" }, 1, 600000, 1, 1, 0, 0),
            N("x_drone_bh", "drone", Loc.T("◆ 교차: 블랙홀 견인"), Loc.T("블랙홀이 열려 있는 동안 드론이 두 배 빠르다 (드론 × 블랙홀)"), new[] { "d_fix", "b_n" }, 1, 500000, 1, 1, 0, 0),
            N("x_bh_eco", "bh", Loc.T("◆ 교차: 파산 보험"), Loc.T("파산하면 돈의 10% 와 제일 좋은 부품 하나를 다음 대로 가져간다 (블랙홀 × 경영)"), new[] { "k_bh", "e_save" }, 1, 800000, 1, 1, 0, 0),
            N("x_eco_route", "eco", Loc.T("◆ 교차: 행성 투자"), Loc.T("지금 궤도 행성의 종목(달 · 화성 · 목성 · 토성)을 들고 있으면 그 판 값 +20% (경영 × 항로)"), new[] { "e_tip", "p_mars" }, 1, 700000, 1, 1, 0, 0),
            N("x_route_claw", "claw", Loc.T("◆ 교차: 궤도 폭격"), Loc.T("행성이 멀수록 화력이 오른다 (달 +5% · 화성 +10% · 목성 +20% · 토성부터 +35%) (항로 × 청소선)"), new[] { "c_double", "p_moon" }, 1, 900000, 1, 1, 0, 0),
            N("i_claw", "claw", Loc.T("✦ 화력 증폭"), Loc.T("모든 무기 화력 +10% (단계마다 · 5단계)"), new[] { "k_claw" }, 1, 2000000, 2.0, 5, 0, 0),
            N("i_drone", "drone", Loc.T("✦ 드론 증폭"), Loc.T("드론이 부순 잔해 값 +10% (단계마다 · 5단계)"), new[] { "k_drone" }, 1, 2000000, 2.0, 5, 0, 0),
            N("i_bh", "bh", Loc.T("✦ 블랙홀 증폭"), Loc.T("블랙홀 확률 +0.2% (단계마다 · 5단계)"), new[] { "k_bh" }, 1, 2000000, 2.0, 5, 0, 0),
            N("i_eco", "eco", Loc.T("✦ 시세 증폭"), Loc.T("모든 값 +8% (단계마다 · 5단계)"), new[] { "k_eco" }, 1, 2000000, 2.0, 5, 0, 0),
            N("i_route", "route", Loc.T("✦ 궤도 증폭"), Loc.T("행성 값 배수 +5% (단계마다 · 5단계)"), new[] { "k_route" }, 1, 2000000, 2.0, 5, 0, 0),
            // ⚠️ 새 칸은 늘 맨 뒤에 — 저장은 칸 번호로 레벨을 들고 있다 (09-25 중간에 끼웠다가 옛 저장이 밀린 일)
            // ◇ 무기 특화 — 단계마다 효과가 커진다 (09-24 사장님 38번 「강화가 너무 적다 · 효과가 추가」)
            N("w_laser_e", "arm", Loc.T("레이저 특화"), Loc.T("태우는 점 +20% · 위력 +15% — 3단계: 태운 자리가 가끔 터진다"), new[] { "w_laser_a" }, 1, 18000, 4, 3, 0, 0),
            N("w_chain_e", "arm", Loc.T("번개 특화"), Loc.T("튀는 수 +2"), new[] { "w_chain_a" }, 1, 27000, 4, 3, 0, 0),
            N("w_vac_e", "arm", Loc.T("진공 청소기 특화"), Loc.T("흡입 원 +20% · 삼킨 것 값 +20%"), new[] { "w_vac_a" }, 1, 72000, 4, 3, 0, 0),
            N("w_mine_e", "arm", Loc.T("기뢰 특화"), Loc.T("기뢰 +1개 · 폭발 +15%"), new[] { "w_mine_a" }, 1, 180000, 4, 3, 0, 0),
            N("w_frz_e", "arm", Loc.T("냉동 빔 특화"), Loc.T("서리 원 +20% · 어는 시간 +0.5초"), new[] { "w_frz_a" }, 1, 480000, 4, 3, 0, 0),
            N("w_clus_e", "arm", Loc.T("분열탄 특화"), Loc.T("파편 +2"), new[] { "w_clus_a" }, 1, 1500000, 4, 3, 0, 0),
            N("w_mag_e", "arm", Loc.T("자석 펄스 특화"), Loc.T("끄는 범위 +20%"), new[] { "w_mag_a" }, 1, 4800000, 4, 3, 0, 0),
            N("w_rail_e", "arm", Loc.T("레일건 특화"), Loc.T("사거리 +120 — 3단계: 한 줄 더"), new[] { "w_rail_a" }, 1, 18000000, 4, 3, 0, 0),
            // 🎟 복권 — 스킬로 (09-24 사장님 33번)
            N("l_more", "eco", Loc.T("복권 단골"), Loc.T("판마다 즉석 복권 +2장 (단계마다)"), new[] { "e_val" }, 1, 300, 3, 3, 0, 0),
            N("l_luck", "eco", Loc.T("행운의 긁개"), Loc.T("즉석 복권 당첨 확률 +50% (단계마다)"), new[] { "l_more" }, 1, 2000, 3, 3, 0, 0),
            N("l_free", "eco", Loc.T("첫 장은 공짜"), Loc.T("판마다 즉석 복권 두 장이 공짜"), new[] { "l_luck" }, 1, 1500, 1, 1, 0, 0),
            N("l_jack", "eco", Loc.T("잭팟"), Loc.T("즉석 복권 돈 당첨 ×3 · 둘이 같으면 표값 두 배"), new[] { "l_luck" }, 1, 30000, 1, 1, 0, 0),
            N("v_volley", "arm", Loc.T("🚀 전탄 발사"), Loc.T("출동 중 게이지가 차면 Space · 계기판 단추 — 산 무기가 모두 한꺼번에 쏜다. 단계마다 더 오래 · 더 촘촘히 · 게이지가 빨리 찬다 (무기 둘부터)"), new[] { "w_hub" }, 1, 4000, 4, 3, 0, 0),   // 09-26 사장님 「전탄 발사도 트리에 · 기본은 안 좋게 · 지금이 최종」
            // 🪐 09-27 행성 셋 더 — 맨 뒤에만 (저장 규칙)
            N("p_oort", "route", Loc.T("오르트 구름 항로"), Loc.T("태양계 바깥 얼음 혜성 떼 — 값 ×19683 · 단단하고 느리다"), new[] { "p_kui" }, 1, 120000000, 1, 1, 0, 0),
            N("p_helio", "route", Loc.T("태양권 계면 항로"), Loc.T("태양풍이 부딪히는 경계 — 값 ×59049 · 잔해가 출렁인다"), new[] { "p_oort" }, 1, 240000000, 1, 1, 0, 0),
            N("p_inter", "route", Loc.T("성간 공간 항로"), Loc.T("태양 빛이 닿지 않는 곳 — 값 ×177147 · 드문드문 · 가장 비싸다"), new[] { "p_helio" }, 1, 480000000, 1, 1, 0, 0),
        };
        public const int NodeCount = 134;
        /// <summary>◆ 핵심 칸 — 돈 + 열쇠 하나 (부품 가게에서 산다). 각성도 여기</summary>
        public static readonly HashSet<string> KeyNodes = new HashSet<string> { "w_laser_a", "w_chain_a", "k_claw", "k_drone", "k_bh", "k_eco", "k_route", "w_slot2", "w_vac_a", "w_mine_a", "w_frz_a", "w_clus_a", "w_mag_a", "w_rail_a", "x_claw_arm", "x_arm_drone", "x_drone_bh", "x_bh_eco", "x_eco_route", "x_route_claw" };
        public static readonly string[] WeaponName = { Loc.T("집게 빔"), Loc.T("레이저"), Loc.T("번개"), Loc.T("청소기"), Loc.T("기뢰"), Loc.T("냉동 빔"), Loc.T("분열탄"), Loc.T("자석"), Loc.T("레일건") };
        public static readonly string[] WeaponNode = { null, "w_laser", "w_chain", "w_vac", "w_mine", "w_frz", "w_clus", "w_mag", "w_rail" };
        public static readonly string[] PlanetNode = { null, "p_moon", "p_mars", "p_jup", "p_sat", "p_belt", "p_ura", "p_nep", "p_kui", "p_oort", "p_helio", "p_inter" };   // 09-27 행성 셋 더 (사장님 「끝까지 가려면 행성이 더 있어야」)
        public static readonly Dictionary<string, string> IconAlias = new Dictionary<string, string> { { "v_volley", "w_hub" }, { "l_more", "e_val" }, { "l_luck", "e_val" }, { "l_free", "e_val" }, { "l_jack", "e_val" }, { "w_laser_e", "w_laser_u" }, { "w_chain_e", "w_chain_u" }, { "w_vac_e", "w_vac_u" }, { "w_mine_e", "w_mine_u" }, { "w_frz_e", "w_frz_u" }, { "w_clus_e", "w_clus_u" }, { "w_mag_e", "w_mag_u" }, { "w_rail_e", "w_rail_u" }, { "m_claw", "c_pow" }, { "m_crit", "c_crit" }, { "m_fuel", "c_fuel" }, { "m_drone", "d_fact" }, { "m_dcount", "d_n" }, { "m_bh", "b_n" }, { "m_val", "e_val" }, { "m_route", "o_wide" }, { "w_laser_x", "w_laser_u" }, { "w_chain_x", "w_chain_u" }, { "w_vac_x", "w_vac_u" }, { "w_mine_x", "w_mine_u" }, { "w_frz_x", "w_frz_u" }, { "w_clus_x", "w_clus_u" }, { "w_mag_x", "w_mag_u" }, { "w_rail_x", "w_rail_u" } };
        public static bool Ring4(string id) => id.StartsWith("m_") || (id.StartsWith("w_") && id.EndsWith("_x"));
        public static readonly int[] OrbitOrder = { 0, 1, 2, 5, 3, 4, 6, 7, 8, 9, 10, 11 };          // 가까운 → 먼 (소행성대는 번호 5지만 화성과 목성 사이)
        // ───────────────────────── 정비소 트리 자리 (손으로 격자에 놓았다 · 시안 https://claude.ai/artifact/NLZseBQWXKMGfuAmFDFJcR)
        //    par = 이어지는 앞 칸의 능력 (R = 가운데 청소선) · tile = 그 능력의 몇 번째 칸 뒤 · (x, y) 첫 칸 자리 · (dx, dy) 뻗는 방향
        //    🔴 여는 조건도 이것 — 앞 칸을 사야 이 능력의 첫 칸이 열린다 (게임 · 봇 같은 규칙)
        public struct TreeSpot { public string par; public int tile, x, y, dx, dy; }
        public static readonly Dictionary<string, TreeSpot> Layout = new Dictionary<string, TreeSpot>
        {
            { "c_pow", new TreeSpot { par = "R", tile = 0, x = 0, y = -1, dx = 0, dy = -1 } },
            { "c_rad", new TreeSpot { par = "c_pow", tile = 2, x = 1, y = -3, dx = 0, dy = -1 } },
            { "c_spd", new TreeSpot { par = "c_pow", tile = 2, x = -1, y = -3, dx = 0, dy = -1 } },
            { "c_fuel", new TreeSpot { par = "R", tile = 0, x = -1, y = 0, dx = -1, dy = 0 } },
            { "c_crit", new TreeSpot { par = "c_spd", tile = 2, x = -2, y = -5, dx = 0, dy = -1 } },
            { "c_double", new TreeSpot { par = "c_spd", tile = 4, x = -3, y = -7, dx = 0, dy = -1 } },
            { "c_magnet", new TreeSpot { par = "c_rad", tile = 3, x = 2, y = -6, dx = 0, dy = -1 } },
            { "c_over", new TreeSpot { par = "c_crit", tile = 5, x = -2, y = -10, dx = 0, dy = 0 } },
            { "d_n", new TreeSpot { par = "R", tile = 0, x = 1, y = 0, dx = 1, dy = 0 } },
            { "d_spd", new TreeSpot { par = "d_n", tile = 2, x = 3, y = 1, dx = 1, dy = 0 } },
            { "d_reach", new TreeSpot { par = "d_n", tile = 4, x = 5, y = -1, dx = 1, dy = 0 } },
            { "d_mag", new TreeSpot { par = "d_spd", tile = 3, x = 5, y = 2, dx = 1, dy = 0 } },
            { "d_sig", new TreeSpot { par = "d_spd", tile = 5, x = 8, y = 1, dx = 1, dy = 0 } },
            { "d_grade", new TreeSpot { par = "d_reach", tile = 3, x = 10, y = -1, dx = 1, dy = 0 } },
            { "d_fix", new TreeSpot { par = "d_mag", tile = 2, x = 6, y = 3, dx = 1, dy = 0 } },
            { "d_pair", new TreeSpot { par = "d_sig", tile = 3, x = 11, y = 1, dx = 0, dy = 0 } },
            { "d_fact", new TreeSpot { par = "d_pair", tile = 1, x = 12, y = 1, dx = 1, dy = 0 } },
            { "b_n", new TreeSpot { par = "R", tile = 0, x = 0, y = 1, dx = 0, dy = 1 } },
            { "c_find", new TreeSpot { par = "c_fuel", tile = 5, x = -6, y = 0, dx = -1, dy = 0 } },
            { "s_speed", new TreeSpot { par = "b_n", tile = 4, x = 0, y = 6, dx = 0, dy = 1 } },
            { "b_pr", new TreeSpot { par = "b_n", tile = 2, x = 1, y = 4, dx = 1, dy = 0 } },
            { "b_cap", new TreeSpot { par = "b_n", tile = 4, x = 1, y = 5, dx = 1, dy = 0 } },
            { "b_pf", new TreeSpot { par = "b_pr", tile = 5, x = 6, y = 5, dx = 1, dy = 0 } },
            { "b_br", new TreeSpot { par = "b_cap", tile = 3, x = 3, y = 6, dx = 0, dy = 1 } },
            { "b_chain", new TreeSpot { par = "b_br", tile = 2, x = 4, y = 7, dx = 1, dy = 0 } },
            { "b_pack", new TreeSpot { par = "b_chain", tile = 5, x = 9, y = 8, dx = 0, dy = 1 } },
            { "o_wide", new TreeSpot { par = "c_fuel", tile = 1, x = -2, y = -1, dx = -1, dy = 0 } },
            { "e_val", new TreeSpot { par = "R", tile = 0, x = -1, y = 1, dx = 0, dy = 1 } },
            { "e_vault", new TreeSpot { par = "e_val", tile = 4, x = -2, y = 4, dx = -1, dy = 0 } },
            { "e_att", new TreeSpot { par = "e_val", tile = 5, x = -2, y = 5, dx = -1, dy = 0 } },
            { "e_quest", new TreeSpot { par = "e_val", tile = 2, x = -2, y = 2, dx = -1, dy = 0 } },
            { "e_talk", new TreeSpot { par = "e_vault", tile = 5, x = -7, y = 4, dx = -1, dy = 0 } },
            { "e_tip", new TreeSpot { par = "e_att", tile = 5, x = -7, y = 5, dx = -1, dy = 0 } },
            { "e_save", new TreeSpot { par = "e_val", tile = 5, x = -1, y = 6, dx = 0, dy = 1 } },
            { "e_guard", new TreeSpot { par = "e_talk", tile = 2, x = -9, y = 4, dx = 0, dy = 0 } },
            { "e_used", new TreeSpot { par = "e_save", tile = 3, x = -2, y = 8, dx = -1, dy = 0 } },
            { "a_open", new TreeSpot { par = "e_val", tile = 3, x = -2, y = 3, dx = 0, dy = 0 } },
            { "a_auto", new TreeSpot { par = "a_open", tile = 1, x = -3, y = 3, dx = 0, dy = 0 } },
            { "a_read", new TreeSpot { par = "a_auto", tile = 1, x = -4, y = 3, dx = -1, dy = 0 } },
            { "a_ins", new TreeSpot { par = "a_read", tile = 3, x = -7, y = 3, dx = -1, dy = 0 } },
            { "a_big", new TreeSpot { par = "a_ins", tile = 3, x = -10, y = 3, dx = -1, dy = 0 } },
            { "p_moon", new TreeSpot { par = "R", tile = 1, x = -2, y = -2, dx = 0, dy = 0 } },
            { "p_mars", new TreeSpot { par = "p_moon", tile = 1, x = -4, y = -3, dx = 0, dy = 0 } },
            { "p_jup", new TreeSpot { par = "p_mars", tile = 1, x = -6, y = -4, dx = 0, dy = 0 } },
            { "p_sat", new TreeSpot { par = "p_jup", tile = 1, x = -8, y = -5, dx = 0, dy = 0 } },
            { "p_belt", new TreeSpot { par = "p_mars", tile = 1, x = -5, y = -5, dx = 0, dy = 0 } },
            { "p_ura", new TreeSpot { par = "p_sat", tile = 1, x = -10, y = -7, dx = 0, dy = 0 } },
            { "p_nep", new TreeSpot { par = "p_ura", tile = 1, x = -12, y = -8, dx = 0, dy = 0 } },
            { "p_kui", new TreeSpot { par = "p_nep", tile = 1, x = -14, y = -9, dx = 0, dy = 0 } },
            { "p_oort", new TreeSpot { par = "p_kui", tile = 1, x = -16, y = -10, dx = 0, dy = 0 } },
            { "p_helio", new TreeSpot { par = "p_oort", tile = 1, x = -18, y = -11, dx = 0, dy = 0 } },
            { "p_inter", new TreeSpot { par = "p_helio", tile = 1, x = -20, y = -12, dx = 0, dy = 0 } },
            { "m_claw", new TreeSpot { par = "k_claw", tile = 1, x = -1, y = -12, dx = 1, dy = 0 } },
            { "m_crit", new TreeSpot { par = "k_claw", tile = 1, x = -3, y = -12, dx = -1, dy = 0 } },
            { "m_fuel", new TreeSpot { par = "k_claw", tile = 1, x = -1, y = -11, dx = 1, dy = 0 } },
            { "m_drone", new TreeSpot { par = "k_drone", tile = 1, x = 14, y = 2, dx = 0, dy = 1 } },
            { "m_dcount", new TreeSpot { par = "k_drone", tile = 1, x = 14, y = 0, dx = 0, dy = -1 } },
            { "m_bh", new TreeSpot { par = "k_bh", tile = 1, x = 1, y = 11, dx = 1, dy = 0 } },
            { "m_val", new TreeSpot { par = "k_eco", tile = 1, x = -5, y = 9, dx = 0, dy = 1 } },
            { "m_route", new TreeSpot { par = "k_route", tile = 1, x = -9, y = -4, dx = -1, dy = 0 } },
            { "w_laser_x", new TreeSpot { par = "w_laser_e", tile = 3, x = 11, y = -2, dx = 0, dy = 0 } },
            { "w_chain_x", new TreeSpot { par = "w_chain_e", tile = 3, x = 11, y = -3, dx = 0, dy = 0 } },
            { "w_vac_x", new TreeSpot { par = "w_vac_e", tile = 3, x = 11, y = -4, dx = 0, dy = 0 } },
            { "w_mine_x", new TreeSpot { par = "w_mine_e", tile = 3, x = 11, y = -5, dx = 0, dy = 0 } },
            { "w_frz_x", new TreeSpot { par = "w_frz_e", tile = 3, x = 11, y = -6, dx = 0, dy = 0 } },
            { "w_clus_x", new TreeSpot { par = "w_clus_e", tile = 3, x = 11, y = -7, dx = 0, dy = 0 } },
            { "w_mag_x", new TreeSpot { par = "w_mag_e", tile = 3, x = 11, y = -8, dx = 0, dy = 0 } },
            { "w_rail_x", new TreeSpot { par = "w_rail_e", tile = 3, x = 11, y = -9, dx = 0, dy = 0 } },
            { "w_hub", new TreeSpot { par = "R", tile = 0, x = 1, y = -1, dx = 0, dy = 0 } },
            { "v_volley", new TreeSpot { par = "w_hub", tile = 1, x = 2, y = -1, dx = 1, dy = 0 } },
            { "w_laser", new TreeSpot { par = "w_hub", tile = 1, x = 4, y = -2, dx = 0, dy = 0 } },
            { "w_laser_u", new TreeSpot { par = "w_laser", tile = 1, x = 5, y = -2, dx = 1, dy = 0 } },
            { "w_laser_a", new TreeSpot { par = "w_laser_u", tile = 2, x = 7, y = -2, dx = 0, dy = 0 } },
            { "w_chain", new TreeSpot { par = "w_laser", tile = 1, x = 4, y = -3, dx = 0, dy = 0 } },
            { "w_chain_u", new TreeSpot { par = "w_chain", tile = 1, x = 5, y = -3, dx = 1, dy = 0 } },
            { "w_chain_a", new TreeSpot { par = "w_chain_u", tile = 2, x = 7, y = -3, dx = 0, dy = 0 } },
            { "e_shop", new TreeSpot { par = "e_save", tile = 2, x = -2, y = 7, dx = 0, dy = 0 } },
            { "k_claw", new TreeSpot { par = "c_over", tile = 1, x = -2, y = -11, dx = 0, dy = 0 } },
            { "k_drone", new TreeSpot { par = "d_fact", tile = 2, x = 14, y = 1, dx = 0, dy = 0 } },
            { "k_bh", new TreeSpot { par = "s_speed", tile = 4, x = 0, y = 10, dx = 0, dy = 0 } },
            { "k_eco", new TreeSpot { par = "e_used", tile = 3, x = -5, y = 8, dx = 0, dy = 0 } },
            { "k_route", new TreeSpot { par = "p_sat", tile = 1, x = -9, y = -3, dx = 0, dy = 0 } },
            { "w_slot2", new TreeSpot { par = "w_chain", tile = 1, x = 3, y = -4, dx = 0, dy = 0 } },
            { "q_insider", new TreeSpot { par = "a_read", tile = 3, x = -7, y = 2, dx = 0, dy = 0 } },
            { "q_rage", new TreeSpot { par = "a_big", tile = 3, x = -13, y = 3, dx = 0, dy = 0 } },
            { "q_front", new TreeSpot { par = "e_tip", tile = 3, x = -10, y = 5, dx = 0, dy = 0 } },
            { "q_debt", new TreeSpot { par = "e_guard", tile = 1, x = -10, y = 4, dx = 0, dy = 0 } },
            { "q_meteor", new TreeSpot { par = "b_chain", tile = 5, x = 9, y = 6, dx = 0, dy = 0 } },
            { "q_sling", new TreeSpot { par = "b_pack", tile = 5, x = 9, y = 13, dx = 0, dy = 0 } },
            { "q_tour", new TreeSpot { par = "p_sat", tile = 1, x = -10, y = -6, dx = 0, dy = 0 } },
            { "q_rock", new TreeSpot { par = "p_jup", tile = 1, x = -6, y = -6, dx = 0, dy = 0 } },
            { "q_gold", new TreeSpot { par = "o_wide", tile = 5, x = -7, y = -1, dx = 0, dy = 0 } },
            { "q_lazy", new TreeSpot { par = "d_fix", tile = 3, x = 9, y = 3, dx = 0, dy = 0 } },
            { "w_vac", new TreeSpot { par = "w_chain", tile = 1, x = 4, y = -4, dx = 0, dy = 0 } },
            { "w_vac_u", new TreeSpot { par = "w_vac", tile = 1, x = 5, y = -4, dx = 1, dy = 0 } },
            { "w_vac_a", new TreeSpot { par = "w_vac_u", tile = 2, x = 7, y = -4, dx = 0, dy = 0 } },
            { "w_mine", new TreeSpot { par = "w_vac", tile = 1, x = 4, y = -5, dx = 0, dy = 0 } },
            { "w_mine_u", new TreeSpot { par = "w_mine", tile = 1, x = 5, y = -5, dx = 1, dy = 0 } },
            { "w_mine_a", new TreeSpot { par = "w_mine_u", tile = 2, x = 7, y = -5, dx = 0, dy = 0 } },
            { "w_frz", new TreeSpot { par = "w_mine", tile = 1, x = 4, y = -6, dx = 0, dy = 0 } },
            { "w_frz_u", new TreeSpot { par = "w_frz", tile = 1, x = 5, y = -6, dx = 1, dy = 0 } },
            { "w_frz_a", new TreeSpot { par = "w_frz_u", tile = 2, x = 7, y = -6, dx = 0, dy = 0 } },
            { "w_clus", new TreeSpot { par = "w_frz", tile = 1, x = 4, y = -7, dx = 0, dy = 0 } },
            { "w_clus_u", new TreeSpot { par = "w_clus", tile = 1, x = 5, y = -7, dx = 1, dy = 0 } },
            { "w_clus_a", new TreeSpot { par = "w_clus_u", tile = 2, x = 7, y = -7, dx = 0, dy = 0 } },
            { "w_mag", new TreeSpot { par = "w_clus", tile = 1, x = 4, y = -8, dx = 0, dy = 0 } },
            { "w_mag_u", new TreeSpot { par = "w_mag", tile = 1, x = 5, y = -8, dx = 1, dy = 0 } },
            { "w_mag_a", new TreeSpot { par = "w_mag_u", tile = 2, x = 7, y = -8, dx = 0, dy = 0 } },
            { "w_rail", new TreeSpot { par = "w_mag", tile = 1, x = 4, y = -9, dx = 0, dy = 0 } },
            { "w_rail_u", new TreeSpot { par = "w_rail", tile = 1, x = 5, y = -9, dx = 1, dy = 0 } },
            { "w_laser_e", new TreeSpot { par = "w_laser_a", tile = 1, x = 8, y = -2, dx = 1, dy = 0 } },
            { "w_chain_e", new TreeSpot { par = "w_chain_a", tile = 1, x = 8, y = -3, dx = 1, dy = 0 } },
            { "w_vac_e", new TreeSpot { par = "w_vac_a", tile = 1, x = 8, y = -4, dx = 1, dy = 0 } },
            { "w_mine_e", new TreeSpot { par = "w_mine_a", tile = 1, x = 8, y = -5, dx = 1, dy = 0 } },
            { "w_frz_e", new TreeSpot { par = "w_frz_a", tile = 1, x = 8, y = -6, dx = 1, dy = 0 } },
            { "w_clus_e", new TreeSpot { par = "w_clus_a", tile = 1, x = 8, y = -7, dx = 1, dy = 0 } },
            { "w_mag_e", new TreeSpot { par = "w_mag_a", tile = 1, x = 8, y = -8, dx = 1, dy = 0 } },
            { "w_rail_e", new TreeSpot { par = "w_rail_a", tile = 1, x = 8, y = -9, dx = 1, dy = 0 } },
            { "l_more", new TreeSpot { par = "e_val", tile = 1, x = -2, y = 1, dx = -1, dy = 0 } },
            { "l_luck", new TreeSpot { par = "l_more", tile = 3, x = -5, y = 1, dx = -1, dy = 0 } },
            { "l_free", new TreeSpot { par = "l_luck", tile = 2, x = -6, y = 2, dx = 0, dy = 0 } },
            { "l_jack", new TreeSpot { par = "l_luck", tile = 3, x = -8, y = 1, dx = 0, dy = 0 } },
            { "w_rail_a", new TreeSpot { par = "w_rail_u", tile = 2, x = 7, y = -9, dx = 0, dy = 0 } },
            { "x_claw_arm", new TreeSpot { par = "c_magnet", tile = 3, x = 2, y = -10, dx = 0, dy = 0 } },
            { "x_arm_drone", new TreeSpot { par = "d_grade", tile = 3, x = 13, y = -2, dx = 0, dy = 0 } },
            { "x_drone_bh", new TreeSpot { par = "d_fix", tile = 3, x = 10, y = 4, dx = 0, dy = 0 } },
            { "x_bh_eco", new TreeSpot { par = "k_bh", tile = 1, x = -1, y = 12, dx = 0, dy = 0 } },
            { "x_eco_route", new TreeSpot { par = "e_tip", tile = 3, x = -10, y = 6, dx = 0, dy = 0 } },
            { "x_route_claw", new TreeSpot { par = "c_double", tile = 5, x = -4, y = -11, dx = 0, dy = 0 } },
            { "i_claw", new TreeSpot { par = "k_claw", tile = 1, x = -2, y = -12, dx = 0, dy = -1 } },
            { "i_drone", new TreeSpot { par = "k_drone", tile = 1, x = 15, y = 1, dx = 1, dy = 0 } },
            { "i_bh", new TreeSpot { par = "k_bh", tile = 1, x = 0, y = 11, dx = 0, dy = 1 } },
            { "i_eco", new TreeSpot { par = "k_eco", tile = 1, x = -6, y = 8, dx = -1, dy = 0 } },
            { "i_route", new TreeSpot { par = "k_route", tile = 1, x = -10, y = -3, dx = -1, dy = 0 } },
        };
        public static readonly string[] IconOrder = { "c_pow", "c_rad", "c_spd", "c_fuel", "c_crit", "c_double", "c_magnet", "c_over", "o_wide", "c_find", "d_n", "d_spd", "d_reach", "d_mag", "d_sig", "d_grade", "d_fix", "d_pair", "d_fact", "b_n", "s_speed", "b_pr", "b_cap", "b_pf", "b_br", "b_chain", "b_pack", "e_val", "e_vault", "e_att", "e_quest", "e_talk", "e_tip", "e_save", "e_guard", "e_used", "R", "a_open", "a_auto", "a_read", "a_ins", "a_big", "p_moon", "p_mars", "p_jup", "p_sat", "w_hub", "w_laser", "w_laser_u", "w_laser_a", "w_chain", "w_chain_u", "w_chain_a", "e_shop", "k_claw", "k_drone", "k_bh", "k_eco", "k_route", "w_slot2", "q_insider", "q_rage", "q_front", "q_debt", "q_meteor", "q_sling", "q_tour", "q_rock", "q_gold", "q_lazy", "w_vac", "w_vac_u", "w_vac_a", "w_mine", "w_mine_u", "w_mine_a", "w_frz", "w_frz_u", "w_frz_a", "w_clus", "w_clus_u", "w_clus_a", "w_mag", "w_mag_u", "w_mag_a", "w_rail", "w_rail_u", "w_rail_a", "x_claw_arm", "x_arm_drone", "x_drone_bh", "x_bh_eco", "x_eco_route", "x_route_claw", "i_claw", "i_drone", "i_bh", "i_eco", "i_route", "p_belt", "p_ura", "p_nep", "p_kui", "w_laser_e", "w_chain_e", "w_vac_e", "w_mine_e", "w_frz_e", "w_clus_e", "w_mag_e", "w_rail_e", "l_more", "l_luck", "l_free", "l_jack", "m_claw", "m_crit", "m_fuel", "m_drone", "m_dcount", "m_bh", "m_val", "m_route", "w_laser_x", "w_chain_x", "w_vac_x", "w_mine_x", "w_frz_x", "w_clus_x", "w_mag_x", "w_rail_x" };
        public static string VisBranch(string id) => id.StartsWith("w_") ? "arm" : id.StartsWith("x_") ? "cross" : id == "c_fuel" || id == "o_wide" || id == "c_find" ? "hull" : id == "s_speed" ? "bh" : Nodes[NodeIx[id]].branch;

        static Node N(string id, string br, string name, string desc, string[] par, int seg, double first, double mult, int max, int depth, int lane)
            => new Node { id = id, branch = br, name = name, desc = desc, par = par, seg = seg, first = first, mult = mult, max = max, depth = depth, lane = lane };
        static readonly Dictionary<string, int> NodeIx = new Dictionary<string, int>();
        public static readonly string[] BranchIds = { "claw", "drone", "bh", "eco" };
        public static readonly string[] BranchNames = { Loc.T("빔 · 선체"), Loc.T("드론 격납고"), Loc.T("블랙홀 · 보급"), Loc.T("사무실") };
        public static readonly int[] BranchNeed = { 0, 1, 2, 0 };

        // ───────────────────────── 청구서 여덟 (§7-3)
        public struct Bill { public string t, perk; public double m, credit; public int due; }
        public static readonly Bill[] Bills =                                   // 09-24 8장 → 12장 (2시간 · 약 ×3~4씩)
        {
            new Bill { t = Loc.T("연료비"),           m = 45,         due = 5, credit = 2,  perk = Loc.T("케슬러 금융이 청소선 한 대를 믿어 주었다") },
            new Bill { t = Loc.T("청소선 할부 1회"),  m = 450,        due = 4, credit = 3,  perk = Loc.T("창구 직원이 이름을 외웠다") },
            new Bill { t = Loc.T("궤도 사용료"),      m = 1800,       due = 4, credit = 5,  perk = Loc.T("궤도청에 청소선이 정식 등록됐다") },
            new Bill { t = Loc.T("정비비"),           m = 12000,      due = 4, credit = 6,  perk = Loc.T("정비소 단골이 됐다") },
            new Bill { t = Loc.T("보험료"),           m = 80000,     due = 5, credit = 8,  perk = Loc.T("궤도 보험이 청소선을 받아 주었다") },
            new Bill { t = Loc.T("청소선 할부 2회"),  m = 300000,     due = 4, credit = 10, perk = Loc.T("청소선 절반은 이제 내 것") },
            new Bill { t = Loc.T("법인세"),           m = 1200000,    due = 5, credit = 13, perk = Loc.T("세금을 내는 어엿한 회사가 됐다") },
            new Bill { t = Loc.T("청소선 할부 3회"),  m = 5000000,   due = 5, credit = 16, perk = Loc.T("케슬러 금융이 먼저 인사를 한다") },
            new Bill { t = Loc.T("외행성 면허세"),    m = 15000000,   due = 5, credit = 20, perk = Loc.T("외행성 면허가 나왔다") },
            new Bill { t = Loc.T("청소선 할부 4회"),  m = 28000000,   due = 5, credit = 24, perk = Loc.T("청소선 네 조각 중 셋이 내 것") },
            new Bill { t = Loc.T("심우주 보험"),      m = 42000000,  due = 6, credit = 28, perk = Loc.T("심우주에서도 보험이 된다") },
            new Bill { t = Loc.T("청소선 할부 완납"), m = 90000000,  due = 10, credit = 0,  perk = Loc.T("빚 청산 → 청산 출동") },
        };

        // ───────────────────────── 경력 (§9-4) — 파산할 때만 산다
        public struct Career { public string id, name, desc; public int[] cost; }
        public static readonly Career[] Careers =
        {
            new Career { id = "pilot",  name = Loc.T("베테랑 조종사"), desc = Loc.T("연료 +20%"),            cost = new[] { 3, 6, 10 } },
            new Career { id = "seed",   name = Loc.T("단골 고객"),     desc = Loc.T("모든 값 ×1.3"),         cost = new[] { 2, 4, 7 } },
            new Career { id = "wrench", name = Loc.T("정비 요령"),     desc = Loc.T("트리 -15%"),            cost = new[] { 4, 7, 11 } },
            new Career { id = "dronef", name = Loc.T("드론 공장"),     desc = Loc.T("드론 +1 (해금 뒤)"),    cost = new[] { 4, 8 } },
            new Career { id = "bhole",  name = Loc.T("블랙홀 연구"),   desc = Loc.T("폭탄 +1 (해금 뒤)"),    cost = new[] { 5, 10 } },
            new Career { id = "friend", name = Loc.T("추심원과 친구"), desc = Loc.T("추심 20% · 연체료 절반"), cost = new[] { 6 } },
        };
        public const int CareerCount = 6;

        // ───────────────────────── 🛠 격납고 (09-26 사장님 「파산에 힘을 — 신용으로 배 기본 무기 + 뱀서처럼 패시브」 · 시안 QmnALcrcZVthDjXqeWCY21)
        public struct Upg { public string id, name, desc; public int max; public double cost; }
        public static readonly Upg[] Ups =
        {
            new Upg { id = "dmg",   name = Loc.T("화력"),      desc = Loc.T("모든 무기 피해 +8%"),            max = 8, cost = 2 },
            new Upg { id = "fuel",  name = Loc.T("연료"),      desc = Loc.T("연료 용량 +5%"),                 max = 6, cost = 2 },
            new Upg { id = "rad",   name = Loc.T("범위"),      desc = Loc.T("주 무기 범위 +4%"),              max = 6, cost = 2 },
            new Upg { id = "cash",  name = Loc.T("시작 돈"),   desc = Loc.T("새 회사 시작 돈 +60"),   max = 3, cost = 3 },
            new Upg { id = "luck",  name = Loc.T("행운"),      desc = Loc.T("치명타 확률 +3%"),               max = 6, cost = 3 },
            new Upg { id = "val",   name = Loc.T("시세"),      desc = Loc.T("모든 값 +6%"),                   max = 8, cost = 3 },
            new Upg { id = "due",   name = Loc.T("청구 기한"), desc = Loc.T("첫 청구서 기한 +1판"),           max = 2, cost = 6 },
            new Upg { id = "loan",  name = Loc.T("대출 한도"), desc = Loc.T("대출 한도 +20%"),                max = 3, cost = 3 },
            new Upg { id = "shop",  name = Loc.T("가게 할인"), desc = Loc.T("가게 값 −5%"),                   max = 3, cost = 3 },
            new Upg { id = "gate",  name = Loc.T("관문 파쇄"), desc = Loc.T("관문에 주는 피해 +10%"),         max = 6, cost = 3 },
            new Upg { id = "chain", name = Loc.T("연쇄 반경"), desc = Loc.T("폭발 · 연쇄 반경 +6%"),          max = 6, cost = 3 },
            new Upg { id = "key",   name = Loc.T("예비 열쇠"), desc = Loc.T("새 회사 시작 열쇠 +1"),  max = 2, cost = 8 },
        };
        public const int UpCount = 12;
        public const double UpGrow = 1.5, CreditK = 2;                          // 단계마다 값 ×1.5 · 파산 신용 = 쌓인 신용 × 2 (봇으로 맞춤)
        public int Up(int i) => M.up != null && i < M.up.Length ? M.up[i] : 0;
        public static int UpCostAt(int i, int l) => (int)Math.Round(Ups[i].cost * Math.Pow(UpGrow, l));
        public int UpCost(int i) => Up(i) < Ups[i].max ? UpCostAt(i, Up(i)) : -1;
        public bool BuyUp(int i) { int c = UpCost(i); if (c < 0 || M.credit < c) return false; M.credit -= c; M.up[i]++; return true; }
        public int UpSpent { get { int t = 0; for (int i = 0; i < UpCount; i++) for (int l = 0; l < Up(i); l++) t += UpCostAt(i, l); return t; } }
        public void RefundUps() { M.credit += UpSpent; for (int i = 0; i < UpCount; i++) M.up[i] = 0; }   // 언제든 전부 돌려받아 다시 짠다

        public struct ShipDef { public string id, name, weapon, trait, desc; public int price; }
        public static readonly ShipDef[] Ships =
        {
            new ShipDef { id = "old",     name = Loc.T("낡은 청소선"), weapon = Loc.T("빔"),   trait = Loc.T("무난"), price = 0, desc = Loc.T("처음부터 있는 배. 한 점을 겨누는 빔 — 무엇 하나 튀지 않지만 무엇도 모자라지 않다") },
            new ShipDef { id = "scatter", name = Loc.T("산탄선"),      weapon = Loc.T("산탄"), trait = Loc.T("연쇄"), price = 10, desc = Loc.T("넓게 퍼지는 산탄. 가까울수록 세고 멀수록 약하다 — 몰린 잔해를 한 번에 터뜨려 연쇄를 연다") },
            new ShipDef { id = "harpoon", name = Loc.T("작살선"),      weapon = Loc.T("작살"), trait = Loc.T("꿰뚫기"), price = 16, desc = Loc.T("작살이 조준 방향으로 한 줄을 꿰뚫는다 — 줄 위의 잔해를 모두 맞히고, 뚫을 때마다 약해진다. 줄지어 선 잔해에 강하다") },
            new ShipDef { id = "tesla",   name = Loc.T("전격선"),      weapon = Loc.T("전기 구체"), trait = Loc.T("지지기"), price = 24, desc = Loc.T("조준점으로 전기 구체를 던진다 — 날아가며 둘레 잔해에 번개를 튀기고, 닿으면 잠깐 머물다 펑 터진다") },   // 09-27 사장님 「레일건 말고 전기로 무언가 — 전기 구체를 범위로 던진다든가」
            new ShipDef { id = "missile", name = Loc.T("미사일선"),    weapon = Loc.T("유도 미사일"), trait = Loc.T("쫓기"), price = 20, desc = Loc.T("누르고 있으면 작은 미사일이 줄줄이 나가 조준점 둘레의 잔해를 스스로 골라 쫓는다 — 빗나가지 않지만 한 발은 작다") },   // 09-27 사장님 「유도 미사일이 그나마 낫네」 (시안 X4byMmYEwkfh7u9rat8pML)
            new ShipDef { id = "frost",   name = Loc.T("냉동선"),      weapon = Loc.T("서리 포"), trait = Loc.T("얼려 깨기"), price = 22, desc = Loc.T("조준점에 서리 원이 터진다 — 언 잔해는 무엇에 맞든 두 배로 아프고, 언 채 부서지면 산산조각 나 옆을 친다") },   // 09-27 사장님 「냉동이랑 분열탄선은 할만할듯」 (시안 MTRmPv5Fe9bgvb1Xcq4Tii)
            new ShipDef { id = "cluster", name = Loc.T("분열탄선"),    weapon = Loc.T("분열 포탄"), trait = Loc.T("흩뿌리기"), price = 24, desc = Loc.T("포탄이 조준점에 떨어져 터지고 파편 넷으로 흩어진다 — 몰린 곳에 강하다. 떨어지는 사이 잔해가 움직인다") },
        };
        public int Ship => M.ship >= 0 && M.ship < Ships.Length && ShipOwned(M.ship) ? M.ship : 0;
        public bool ShipOwned(int i) => i == 0 || (M.shipsOwned & (1 << i)) != 0;
        public bool BuyShip(int i) { if (i <= 0 || i >= Ships.Length || ShipOwned(i) || M.credit < Ships[i].price) return false; M.credit -= Ships[i].price; M.shipsOwned |= 1 << i; M.ship = i; return true; }
        public void SelectShip(int i) { if (i >= 0 && i < Ships.Length && ShipOwned(i)) M.ship = i; }
        // 🎨 트리 칸 이름 · 설명 — 배마다 (산탄선이면 빔 칸이 산탄 칸으로 읽힌다 · 09-26)
        static readonly System.Collections.Generic.Dictionary<string, string[]> ScatterNode = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "c_pow", new[] { Loc.T("산탄 위력"), Loc.T("알 하나 피해 +1 (단계마다) — 가까이 쏠수록 더 세다") } },
            { "c_rad", new[] { Loc.T("산탄 퍼짐"), Loc.T("퍼지는 원 반지름 +6 (단계마다) — 처음부터 38") } },
            { "c_spd", new[] { Loc.T("산탄 증폭"), Loc.T("산탄 화력 +8% (단계마다)") } },
        };
        static readonly System.Collections.Generic.Dictionary<string, string[]> HarpoonNode = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "c_pow", new[] { Loc.T("작살 위력"), Loc.T("작살 피해 +1 (단계마다) — 뚫을 때마다 20%씩 약해진다") } },
            { "c_rad", new[] { Loc.T("작살 관통"), Loc.T("한 번에 꿰뚫는 수 +1 · 30 더 멀리 (단계마다) — 처음 3개") } },
            { "c_spd", new[] { Loc.T("작살 증폭"), Loc.T("작살 화력 +8% (단계마다)") } },
        };
        static readonly System.Collections.Generic.Dictionary<string, string[]> MissileNode = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "c_pow", new[] { Loc.T("미사일 위력"), Loc.T("미사일 한 발 피해 +1 (단계마다)") } },
            { "c_rad", new[] { Loc.T("탐지 범위"), Loc.T("조준점 둘레 고르는 범위 +10 (단계마다) — 세 단계마다 한 번에 한 발 더") } },
            { "c_spd", new[] { Loc.T("미사일 증폭"), Loc.T("미사일 화력 +8% (단계마다)") } },
        };
        static readonly System.Collections.Generic.Dictionary<string, string[]> FrostNode = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "c_pow", new[] { Loc.T("서리 위력"), Loc.T("서리 피해 +1 (단계마다) — 언 잔해는 두 배로 아프다") } },
            { "c_rad", new[] { Loc.T("서리 원"), Loc.T("서리 원 반지름 +6 (단계마다) — 처음 44") } },
            { "c_spd", new[] { Loc.T("냉기 증폭"), Loc.T("서리 화력 +8% (단계마다)") } },
        };
        static readonly System.Collections.Generic.Dictionary<string, string[]> ClusNode = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "c_pow", new[] { Loc.T("포탄 위력"), Loc.T("포탄 · 파편 피해 +1 (단계마다)") } },
            { "c_rad", new[] { Loc.T("파편 수"), Loc.T("포탄 폭발 +5 (단계마다) — 세 단계마다 파편 하나 더 · 처음 넷") } },
            { "c_spd", new[] { Loc.T("분열 증폭"), Loc.T("포탄 화력 +8% (단계마다)") } },
        };
        static readonly System.Collections.Generic.Dictionary<string, string[]> TeslaNode = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "c_pow", new[] { Loc.T("전기 위력"), Loc.T("번개 · 터지는 피해 +1 (단계마다)") } },
            { "c_rad", new[] { Loc.T("구체 크기"), Loc.T("번개가 닿는 범위 +10 · 터지는 범위 +8 (단계마다) — 두 단계마다 번개 한 줄 더") } },
            { "c_spd", new[] { Loc.T("전기 증폭"), Loc.T("전기 화력 +8% (단계마다)") } },
        };
        System.Collections.Generic.Dictionary<string, string[]> ShipNode => Ship == 1 ? ScatterNode : Ship == 2 ? HarpoonNode : Ship == 3 ? TeslaNode : Ship == 4 ? MissileNode : Ship == 5 ? FrostNode : Ship == 6 ? ClusNode : null;
        public string NodeName(int i) => ShipNode != null && ShipNode.TryGetValue(Nodes[i].id, out var a) ? a[0] : Nodes[i].name;
        public string NodeDesc(int i) => ShipNode != null && ShipNode.TryGetValue(Nodes[i].id, out var a) ? a[1] : Nodes[i].desc;
        public double ShipGateK => Ship == 1 ? 0.6 : Ship == 2 ? 1.0 : Ship == 3 ? 2.0 : Ship == 4 ? 0.4 : Ship == 5 ? FrostGateK : Ship == 6 ? ClusGateK : 1;   // 미사일은 날아가는 사이 흩어져 관문엔 덜 박힌다 · 전격 한 발 ≈ 빔 × 2 (번개 여럿 + 펑) · 미사일 한 발 = 빔 × MissileK         // 🛰 관문 체력은 배 무기가 한 방에 주는 만큼으로 (산탄 한 알은 약하다)
        public double ScatterR => (38 + 6 * Lv("c_rad")) * (1 + Part("rad")) * (1 + 0.04 * Up(2));   // 산탄 — 처음부터 넓다

        // ───────────────────────── 의뢰 (§4-4) — kind: 0 금고 1 연료통 2 조각 3 위성 4 연쇄 5 탱크 6 압축 7 큰 잔해 8 압류
        public struct Contract { public int orbit, kind, target; public string text; }
        public static readonly Contract[] Contracts =
        {
            new Contract { orbit = 0, kind = 0, target = 2,   text = Loc.T("금고 위성 2개") },
            new Contract { orbit = 0, kind = 1, target = 6,   text = Loc.T("로켓 동체 6개") },
            new Contract { orbit = 0, kind = 2, target = 150, text = Loc.T("조각 150개") },
            new Contract { orbit = 0, kind = 3, target = 20,  text = Loc.T("죽은 위성 20개") },
            new Contract { orbit = 1, kind = 0, target = 5,   text = Loc.T("금고 위성 5개") },
            new Contract { orbit = 1, kind = 2, target = 250, text = Loc.T("조각 250개") },
            new Contract { orbit = 1, kind = 3, target = 30,  text = Loc.T("죽은 위성 30개") },
            new Contract { orbit = 2, kind = 4, target = 40,  text = Loc.T("연쇄 40") },
            new Contract { orbit = 2, kind = 5, target = 15,  text = Loc.T("폭발 탱크 15개") },
            new Contract { orbit = 2, kind = 6, target = 25,  text = Loc.T("한 번에 25개 압축") },
            new Contract { orbit = 3, kind = 7, target = 3,   text = Loc.T("큰 잔해 3개") },
            new Contract { orbit = 3, kind = 4, target = 150, text = Loc.T("연쇄 150") },
            new Contract { orbit = 3, kind = 6, target = 60,  text = Loc.T("한 번에 60개 압축") },
            new Contract { orbit = 4, kind = 0, target = 12,  text = Loc.T("금고 위성 12개") },
            new Contract { orbit = 4, kind = 4, target = 200, text = Loc.T("연쇄 200") },
            new Contract { orbit = 4, kind = 7, target = 4,   text = Loc.T("큰 잔해 4개") },
            new Contract { orbit = 5, kind = 2, target = 400, text = Loc.T("조각 400개") },
            new Contract { orbit = 5, kind = 5, target = 20,  text = Loc.T("폭발 탱크 20개") },
            new Contract { orbit = 5, kind = 4, target = 100, text = Loc.T("연쇄 100") },
            new Contract { orbit = 6, kind = 0, target = 14,  text = Loc.T("금고 위성 14개") },
            new Contract { orbit = 6, kind = 4, target = 250, text = Loc.T("연쇄 250") },
            new Contract { orbit = 7, kind = 3, target = 60,  text = Loc.T("죽은 위성 60개") },
            new Contract { orbit = 7, kind = 7, target = 5,   text = Loc.T("큰 잔해 5개") },
            new Contract { orbit = 8, kind = 0, target = 18,  text = Loc.T("금고 위성 18개") },
            new Contract { orbit = 8, kind = 4, target = 300, text = Loc.T("연쇄 300") },
            new Contract { orbit = -1, kind = 8, target = 5,  text = Loc.T("압류 딱지 5개") },
        };

    }
}
