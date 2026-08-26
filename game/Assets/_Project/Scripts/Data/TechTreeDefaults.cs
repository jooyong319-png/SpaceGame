using System.Collections.Generic;
using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 영구 강화 테크트리의 초기값.
    ///
    /// 🔴 격자에 놓고 선행 관계로 잇는다. 선은 데이터로 두지 않는다 —
    ///    `requires`에서 자동으로 그리므로 그림과 규칙이 어긋날 수 없다.
    ///
    /// 🔴 배치 규칙: 중앙 (0,0)이 뿌리. 네 방향으로 계열이 뻗고,
    ///    무기 계열은 아래쪽 바깥, 특수(조합·시작 상태)는 위쪽 바깥.
    ///
    ///    (-x, +y) 기동 ┃ 화력 (+x, +y)
    ///    ───────── 뿌리 ─────────
    ///    (-x, -y) 선체 ┃ 수집 (+x, -y)
    ///
    /// 🔴 비용은 2026-08-22 실측으로 내렸다 (고철 ×0.40 · 회로 ×0.55 · 코어 ×0.70).
    ///    그전엔 전체 완주에 195시간이 나왔다 — 수입과 비용이 두 자릿수 배로 어긋나 있었다.
    ///
    /// 🔴 무기 노드는 **무기 종류가 아니라 패턴**에 붙인다.
    ///    "절단날 +1"이 아니라 "궤도체 +1" — 그래야 무기를 20종으로 늘려도
    ///    노드를 다시 쓰지 않는다.
    /// </summary>
    public static class TechTreeDefaults
    {
        static readonly List<TechNodeDef> buf = new List<TechNodeDef>();

        static TechNodeDef N(string id, string title, string desc, TechBranch branch,
                             int x, int y, TechEffect effect, float value,
                             int scrap, int circuit = 0, int core = 0,
                             int maxRank = 1, float growth = 1.55f, params string[] requires)
        {
            var n = new TechNodeDef
            {
                id = id, title = title, description = desc, branch = branch,
                cell = new Vector2Int(x, y), effect = effect, value = value,
                costScrap = scrap, costCircuit = circuit, costCore = core,
                maxRank = maxRank, costGrowth = growth,
                requires = requires
            };
            buf.Add(n);
            return n;
        }

        /// <summary>
        /// 무기를 여는 노드. 효과는 스탯이 아니라 **해금**이라 값이 없다.
        /// </summary>
        static TechNodeDef Wep(string id, string title, WeaponKind k, string desc,
                               int x, int y, int scrap, int circuit = 0, int core = 0,
                               params string[] requires)
        {
            var n = N(id, title, desc, TechBranch.Weapon, x, y,
                      TechEffect.UnlockWeapon, 0f, scrap, circuit, core, 1, 1f, requires);
            n.weapon = k;
            return n;
        }

        /// <summary>무기 **하나에만** 붙는 노드. 어느 무기인지는 `weapon`이 정한다.</summary>
        static TechNodeDef Won(string id, string title, string desc, WeaponKind k,
                               int x, int y, TechEffect effect, float value,
                               int scrap, int circuit = 0, int core = 0,
                               int maxRank = 1, float growth = 1.55f, params string[] requires)
        {
            var n = N(id, title, desc, TechBranch.Weapon, x, y,
                      effect, value, scrap, circuit, core, maxRank, growth, requires);
            n.weapon = k;
            return n;
        }

        public static void Fill(GameContent c)
        {
            buf.Clear();

            // ==========================================================================
            //  뿌리
            // ==========================================================================
            N("root", "예비 동력로", "모든 것이 여기서 갈라진다. 최대 연료 +20",
              TechBranch.Core, 0, 0, TechEffect.FuelMax, 20f, 0);

            // ==========================================================================
            //  무기 — 무엇을 들고 나가는가 (뿌리 바로 아래)
            //
            //  🔴 **무기를 테크트리 안에 넣었다** (2026-08-23 사장님 지시:
            //     *"테크 트리 하나 안에 무기 종류를 넣어주면 됨"*).
            //
            //     전에는 준비 화면에서 배를 골랐고, 배가 무기를 정했다.
            //     그러면 무기가 **영구 강화 바깥**에 있어서 "무엇을 살까"의 저울에 안 올랐다.
            //     이제 노드 하나가 무기 하나다 — 강화를 살지 무기를 열지가 한 저울이 된다.
            //
            //  🔴 **첫 무기는 공짜다.** 무기가 하나도 없으면 판에서 아무것도 못 한다 —
            //     테크트리를 한 번도 안 열어 본 사람도 반드시 하나는 들고 있어야 한다.
            //     (`MetaSave.CurrentWeapon`이 열린 것 중 첫 번째로 되돌린다)
            //
            //  ⚠️ 연 무기를 **다시 누르면 그것을 골라 든다.** 여는 것과 고르는 것이
            //     같은 노드에서 일어난다 — 화면을 하나 더 만들지 않으려는 것이다.
            // ==========================================================================
            Wep("wep_harpoon", "견인 작살", WeaponKind.Harpoon,
                "곧게 쏴서 꿰뚫는다. 관통이 오르면 한 발이 여러 개를 지난다",
                0, -3, 0);

            // 🔴 **고철만으로 산다** (2026-08-27). 전에는 회로 14가 들어
            //    **1구역에서는 절대 못 샀다** — 회로는 2구역부터 나오는데
            //    2구역은 보스를 깨야 열리고, 보스는 무기 하나로는 못 깬다. 갇힌다.
            //
            //    실측이 이유를 딱 짚어 줬다: 무기 **1종 → 3종이 초당 0.4 → 13.1(32배)**.
            //    피해배수는 1구역 천장 쪽이 오히려 높은데도 그렇다 —
            //    **무기 개수가 곱으로 들어간다.** 화력 노드 열 개보다 무기 하나가 크다.
            //    사장님 지시(*"무기는 추가다, 개수 제한 없다"*)가 곧 주 성장축이라는 뜻이다.
            Wep("wep_arc", "정전기 방출", WeaponKind.Arc,
                "가까운 것들에게 옮겨붙는다. 한 방은 약하지만 쉴 새 없다",
                -2, -3, 420, 0, 0, "wep_harpoon");

            Wep("wep_discus", "회수 원반", WeaponKind.Discus,
                "던지면 돌아온다. 오가며 두 번 벤다",
                2, -3, 1600, 24, 2, "wep_harpoon");

            // ==========================================================================
            //  선체 — **조업 시간**을 늘리는 쪽 (좌하)
            //
            //  🔴 2026-08-26: 원래 "버티는 쪽"이었다. 플레이어가 무적이 되면서
            //     충돌 저항 노드가 **돈만 먹는 노드**가 됐고, 가지 전체가 죽어 있었다.
            //
            //  🔴 Space Rock Breaker에서 **연료는 성장을 체감시키는 장치**다 —
            //     처음엔 30초라 답답하고, 강화할수록 길어지다가 결국 제약이 아니게 된다.
            //     *"한 시간 안에 수백 개를 몇 초 만에 부수게 된다"*가 거기서 나온다.
            //     그래서 이 가지를 통째로 **연료 = 조업 시간**으로 바꿨다.
            // ==========================================================================
            N("hull1", "보강 판재", "최대 연료 +25", TechBranch.Hull, -1, -1,
              TechEffect.FuelMax, 25f, 16, 0, 0, 5, 1.5f, "root");

            N("hull2", "예비 연료통", "최대 연료 +40", TechBranch.Hull, -2, -1,
              TechEffect.FuelMax, 40f, 28, 0, 0, 5, 1.6f, "hull1");

            N("hull3", "예열 연료", "시작 연료 +8%p", TechBranch.Hull, -2, -2,
              TechEffect.StartFuel, 0.08f, 36, 1, 0, 3, 1.7f, "hull1");

            N("hull4", "이중 격벽", "최대 연료 +60", TechBranch.Hull, -3, -1,
              TechEffect.FuelMax, 60f, 64, 1, 0, 4, 1.6f, "hull2");

            N("hull5", "정제 회로", "최대 연료 +90", TechBranch.Hull, -3, -2,
              TechEffect.FuelMax, 90f, 88, 2, 0, 5, 1.7f, "hull2", "hull3");

            // 🔴 죽지 않는 게임에 부활이 있을 수 없다 — "연료통 회수량"으로 갈아끼웠다
            N("hull6", "회수 자석", "떨어진 연료통 회복량 +25%",
              TechBranch.Hull, -4, -2, TechEffect.FuelPickupBonus, 0.25f, 240, 7, 2, 4, 1.7f, "hull5");

            N("hull7", "정제 회수기", "파편마다 연료 +0.15", TechBranch.Hull, -4, -1,
              TechEffect.RefineOnCollect, 0.15f, 104, 2, 0, 4, 1.6f, "hull4");

            // ==========================================================================
            //  기동 — 움직이는 쪽 (좌상)
            // ==========================================================================
            N("drv1", "보조 추진기", "이동 속도 +3%", TechBranch.Drive, -1, 1,
              TechEffect.MoveSpeed, 0.03f, 16, 0, 0, 6, 1.5f, "root");

            N("drv2", "출력 증대", "추진력 +2", TechBranch.Drive, -2, 1,
              TechEffect.Thrust, 2f, 32, 1, 0, 5, 1.55f, "drv1");

            N("drv3", "자세 제어", "감쇠 +0.12 (더 잘 멈춘다)", TechBranch.Drive, -2, 2,
              TechEffect.Handling, 0.12f, 36, 1, 0, 4, 1.6f, "drv1");

            N("drv4", "냉각 개선", "대시 쿨다운 -6%", TechBranch.Drive, -3, 1,
              TechEffect.DashCooldown, 0.06f, 60, 1, 0, 5, 1.6f, "drv2");

            N("drv5", "관성 제어기", "이동 속도 +6%", TechBranch.Drive, -3, 2,
              TechEffect.MoveSpeed, 0.06f, 96, 2, 0, 4, 1.7f, "drv2", "drv3");

            N("drv6", "초전도 코일", "대시 쿨다운 -12%", TechBranch.Drive, -4, 1,
              TechEffect.DashCooldown, 0.12f, 168, 4, 1, 3, 1.7f, "drv4");

            N("drv7", "영점 조정", "추진력 +6", TechBranch.Drive, -4, 2,
              TechEffect.Thrust, 6f, 160, 4, 1, 3, 1.7f, "drv5");

            // ==========================================================================
            //  화력 — 때리는 쪽 (우상)
            // ==========================================================================
            N("pow1", "출력 증폭", "전 무기 피해 +4%", TechBranch.Power, 1, 1,
              TechEffect.WeaponPower, 0.04f, 18, 0, 0, 8, 1.5f, "root");

            N("pow2", "확장 코일", "전 무기 사거리 +4%", TechBranch.Power, 2, 1,
              TechEffect.WeaponRange, 0.04f, 32, 1, 0, 6, 1.55f, "pow1");

            N("pow3", "냉각 회로", "전 무기 쿨다운 -3%", TechBranch.Power, 2, 2,
              TechEffect.WeaponCooldown, 0.03f, 44, 1, 0, 6, 1.6f, "pow1");

            N("pow4", "고출력 회로", "전 무기 피해 +9%", TechBranch.Power, 3, 1,
              TechEffect.WeaponPower, 0.09f, 104, 2, 0, 5, 1.65f, "pow2");

            N("pow5", "초전도 냉각", "전 무기 쿨다운 -7%", TechBranch.Power, 3, 2,
              TechEffect.WeaponCooldown, 0.07f, 120, 3, 0, 4, 1.7f, "pow3");

            N("pow6", "해체 프로토콜", "보스에게 주는 피해 +12%", TechBranch.Power, 4, 1,
              TechEffect.BossDamage, 0.12f, 200, 5, 1, 4, 1.65f, "pow4");

            N("pow7", "광역 증폭기", "전 무기 사거리 +10%", TechBranch.Power, 4, 2,
              TechEffect.WeaponRange, 0.10f, 184, 4, 1, 4, 1.7f, "pow2", "pow5");

            N("pow8", "임계 출력", "전 무기 피해 +18%", TechBranch.Power, 5, 1,
              TechEffect.WeaponPower, 0.18f, 360, 9, 3, 3, 1.8f, "pow4", "pow6");

            // ==========================================================================
            //  수집 — 버는 쪽 (우하)
            // ==========================================================================
            N("sal1", "자기 수집기", "파편 흡수 반경 +6%", TechBranch.Salvage, 1, -1,
              TechEffect.IntakeRadius, 0.06f, 16, 0, 0, 6, 1.5f, "root");

            N("sal2", "자동 분류기", "크레딧 +5%", TechBranch.Salvage, 2, -1,
              TechEffect.ValueMul, 0.05f, 30, 1, 0, 6, 1.55f, "sal1");

            // 🔴 레벨업이 없어져(2026-08-26) 경험치 노드가 죽었다 — 견인 쪽으로 갈아끼웠다
            N("sal3", "경량 견인줄", "짐이 덜 무겁다 (+8%)", TechBranch.Salvage, 2, -2,
              TechEffect.TowWeight, 0.08f, 34, 1, 0, 6, 1.55f, "sal1");

            // 🔴 **끌 수 있는 칸 +1.** 지금 판에서 제일 크게 체감되는 강화다 —
            //    무게 감소는 "덜 느려진다"지만 이건 **벽 자체를 민다.**
            N("sal4", "확장 견인대", "끌 수 있는 칸 +1", TechBranch.Salvage, 3, -1,
              TechEffect.TowCapacity, 1f, 88, 2, 0, 5, 2.0f, "sal2");

            N("sal5", "감정 회로", "크레딧 +12%", TechBranch.Salvage, 3, -2,
              TechEffect.ValueMul, 0.12f, 112, 3, 0, 5, 1.7f, "sal2", "sal3");

            N("sal6", "관성 상쇄기", "짐이 덜 무겁다 (+18%)", TechBranch.Salvage, 4, -2,
              TechEffect.TowWeight, 0.18f, 128, 3, 1, 4, 1.7f, "sal3");

            N("sal7", "보급 신호", "아이템 드랍률 +0.6%p", TechBranch.Salvage, 4, -1,
              TechEffect.ItemDropChance, 0.006f, 152, 4, 1, 5, 1.7f, "sal4");

            // 🔴 **회수 드론** — 배 옆에 떠서 제 줄을 끈다 (한 대당 2칸).
            //    칸 노드는 숫자만 늘지만 이건 **화면에 보인다.**
            //    수집 가지의 끝에 두는 이유: 이 가지에서 제일 크게 체감되는 보상이어야 한다.
            N("sal_drone", "회수 드론", "드론이 따라붙어 2칸을 더 끈다",
              TechBranch.Salvage, 5, -3,
              TechEffect.CarrierDrone, 1f, 420, 12, 3, 3, 2.2f, "sal4", "sal7");

            N("sal8", "연료 농축", "연료 아이템 회복량 +20%", TechBranch.Salvage, 5, -1,
              TechEffect.FuelPickupBonus, 0.20f, 168, 4, 1, 3, 1.7f, "sal7");

            // ---- 재화 발견 (수집 계열의 끝) ----
            N("mat1", "선별 자석", "고철 드랍률 +12%", TechBranch.Salvage, 5, -2,
              TechEffect.ScrapFind, 0.12f, 120, 2, 0, 5, 1.6f, "sal5");

            N("mat2", "회로 감지기", "회로 드랍률 +12%", TechBranch.Salvage, 6, -2,
              TechEffect.CircuitFind, 0.12f, 240, 6, 1, 5, 1.7f, "mat1");

            N("mat3", "코어 공명기", "코어 드랍률 +12%", TechBranch.Salvage, 6, -1,
              TechEffect.CoreFind, 0.12f, 360, 11, 2, 5, 1.8f, "mat2");

            N("mat4", "전방위 정제", "모든 재화 드랍률 +10%", TechBranch.Salvage, 7, -2,
              TechEffect.MatFindAll, 0.10f, 640, 18, 4, 4, 1.85f, "mat3");

            // ==========================================================================
            //  무기별 가지 — 무기 노드마다 **제 길**이 아래로 뻗는다
            //
            //  🔴 사장님 지시 (2026-08-23): *"무기는 따로따로 테크트리 타지게 하자."*
            //     *"공용이 있고, 무기별로 따로 있는 방식."*
            //
            //     · **공용**은 화력 가지(`pow*`)다 — 어느 무기를 들든 같이 오른다
            //     · **무기별**은 여기다 — 그 무기를 들었을 때만 뜻이 있다
            //
            //  ⚠️ 예전에는 이 자리에 **패턴 단위** 노드가 있었다("발사체 +1").
            //     그건 같은 패턴을 쓰는 무기를 **같이** 올려서 — 작살 노드가 원반까지 키웠다 —
            //     "무기마다 다른 길"이 성립하지 않았다. 이제 `TechNodeDef.weapon`으로 갈린다.
            //
            //  🔴 세 가지가 **성격이 다르게** 뻗는다. 다 같은 모양이면 무기를 바꿀 이유가 없다:
            //     · 작살 = 한 발을 **뚫는다** (관통 → 발사 수)
            //     · 방전 = **퍼진다** (연쇄 대상 → 사거리)
            //     · 원반 = **오래 돈다** (쿨다운 → 피해)
            // ==========================================================================

            // ---- 견인 작살 (0,-3에서 아래로) ----
            Won("hp1", "강화 미늘", "작살 관통 +1", WeaponKind.Harpoon, 0, -4,
                // 🔴 첫 무기의 가지는 **1구역의 일**이다 — 회로를 빼서 고철만으로 키운다
                TechEffect.WeaponPierceOne, 1f, 120, 0, 0, 4, 1.75f, "wep_harpoon");

            Won("hp2", "연장 탄창", "작살 발사 수 +1", WeaponKind.Harpoon, 0, -5,
                TechEffect.WeaponCountOne, 1f, 300, 0, 0, 3, 1.95f, "hp1");

            Won("hp3", "관통 장약", "작살 피해 +12%", WeaponKind.Harpoon, 1, -5,
                TechEffect.WeaponPowerOne, 0.12f, 180, 0, 0, 5, 1.7f, "hp1");

            // ---- 정전기 방출 (-2,-3에서 아래로) ----
            Won("ac1", "분기 회로", "방전 대상 +1", WeaponKind.Arc, -2, -4,
                TechEffect.WeaponCountOne, 1f, 152, 4, 1, 4, 1.8f, "wep_arc");

            Won("ac2", "전도 확장", "방전 사거리 +12%", WeaponKind.Arc, -3, -5,
                TechEffect.WeaponRangeOne, 0.12f, 120, 3, 0, 4, 1.65f, "ac1");

            Won("ac3", "축전 개선", "방전 쿨다운 -7%", WeaponKind.Arc, -2, -5,
                TechEffect.WeaponCooldownOne, 0.07f, 190, 5, 0, 4, 1.7f, "ac1");

            // ---- 회수 원반 (2,-3에서 아래로) ----
            Won("ds1", "경량 날", "원반 쿨다운 -7%", WeaponKind.Discus, 2, -4,
                TechEffect.WeaponCooldownOne, 0.07f, 150, 4, 0, 4, 1.7f, "wep_discus");

            Won("ds2", "쌍원반", "원반 발사 수 +1", WeaponKind.Discus, 3, -5,
                TechEffect.WeaponCountOne, 1f, 300, 8, 1, 3, 1.95f, "ds1");

            Won("ds3", "중량 테두리", "원반 피해 +14%", WeaponKind.Discus, 2, -5,
                TechEffect.WeaponPowerOne, 0.14f, 200, 5, 0, 5, 1.7f, "ds1");

            // ==========================================================================
            //  🔴 발동형 — **가끔 터지는 것** (2026-08-26)
            //
            //     사장님: *"독특한 것이 많으면 많을수록 좋아.
            //     쓰레기 처치 시 재화 드롭 확률 증가, 무기 공격 시 몇 %로 폭발 이런거"*
            //
            //     숫자만 오르는 노드는 이미 충분하다. **확률로 터지는 것**이 있어야
            //     같은 판이 판마다 다르게 느껴진다 — 인크리멘탈의 도파민이 여기서 나온다.
            //
            //  🔴 오른쪽 위(+x, +y)로 뻗는다. 화력 가지 옆이라 "공격이 화려해진다"로 읽힌다.
            // ==========================================================================
            N("pr1", "불안정 탄두", "맞힐 때 6% 확률로 터진다", TechBranch.Power, 2, 4,
              TechEffect.ProcExplode, 0.06f, 180, 5, 0, 5, 1.8f, "pow2");

            N("pr2", "고폭 신관", "맞힐 때 터질 확률 +8%p", TechBranch.Power, 3, 4,
              TechEffect.ProcExplode, 0.08f, 420, 12, 2, 4, 1.9f, "pr1");

            N("pr3", "유도 방전", "부술 때 10% 확률로 번개가 옮겨붙는다", TechBranch.Power, 4, 4,
              TechEffect.ProcChain, 0.10f, 220, 6, 1, 5, 1.8f, "pr1");

            N("pr4", "과충전 코일", "번개가 옮겨붙을 확률 +12%p", TechBranch.Power, 4, 3,
              TechEffect.ProcChain, 0.12f, 480, 14, 2, 4, 1.9f, "pr3");

            N("pr5", "예비 격발", "8% 확률로 한 번 더 쏜다", TechBranch.Power, 5, 2,
              TechEffect.ProcDoubleShot, 0.08f, 340, 9, 1, 5, 1.95f, "pr2");

            N("pr6", "연사 회로", "한 번 더 쏠 확률 +10%p", TechBranch.Power, 6, 2,
              TechEffect.ProcDoubleShot, 0.10f, 760, 20, 4, 3, 2.1f, "pr5");

            N("pr7", "추진 여열", "부수면 2초 동안 +15% 빨라진다", TechBranch.Drive, 5, 3,
              TechEffect.KillSpeed, 0.15f, 260, 7, 1, 4, 1.85f, "drv2");

            // ==========================================================================
            //  🔴 드롭형 — **무엇이 얼마나 떨어지는가**
            //     인크리멘탈에서 제일 크게 체감되는 가지다. 수집 계열(우하) 아래로 뻗는다.
            // ==========================================================================
            N("dr1", "정밀 절단", "재화가 두 배로 나올 확률 +6%", TechBranch.Salvage, 4, -5,
              TechEffect.MatDoubleChance, 0.06f, 200, 5, 0, 5, 1.85f, "sal2");

            N("dr2", "이중 회수", "두 배로 나올 확률 +9%p", TechBranch.Salvage, 5, -5,
              TechEffect.MatDoubleChance, 0.09f, 520, 14, 2, 4, 1.95f, "dr1");

            N("dr3", "광물 감식", "희귀 재화가 나올 확률 +25%", TechBranch.Salvage, 5, -4,
              TechEffect.RareMatChance, 0.25f, 280, 8, 1, 5, 1.9f, "dr1");

            N("dr4", "심층 탐사", "희귀 재화 확률 +40%p", TechBranch.Salvage, 6, -4,
              TechEffect.RareMatChance, 0.40f, 640, 18, 3, 4, 2.0f, "dr3");

            N("dr5", "감정 등급", "가져온 재화 값어치 +10%", TechBranch.Salvage, 7, -3,
              TechEffect.MatValue, 0.10f, 300, 8, 1, 6, 1.85f, "dr2", "dr3");

            N("dr6", "표류 안정기", "떨어진 덩어리가 +20초 더 남는다", TechBranch.Salvage, 4, -4,
              TechEffect.LumpLife, 20f, 150, 4, 0, 4, 1.7f, "sal3");

            // ==========================================================================
            //  🔴 보스 — 위협이 보스에만 있으므로 대비도 여기만 있다
            // ==========================================================================
            N("bs1", "충격 완충재", "보스 탄 피해 -12%", TechBranch.Hull, -5, -1,
              TechEffect.BossShotResist, 0.12f, 240, 7, 1, 4, 1.85f, "hull4");

            N("bs2", "관통 탄심", "보스에게 주는 피해 +18%", TechBranch.Power, 6, 1,
              TechEffect.BossDamage, 0.18f, 320, 9, 2, 5, 1.9f, "pow3");

            N("bs3", "해체 전문", "보스에서 나오는 재화 +30%", TechBranch.Salvage, 7, -1,
              TechEffect.BossMatBonus, 0.30f, 560, 16, 3, 4, 2.0f, "dr5");

            // ==========================================================================
            //  🔴 연료 — 조업 시간이 곧 한 판의 길이다
            // ==========================================================================
            N("fu1", "연료 절약", "연료 감소 -6%", TechBranch.Hull, -5, -3,
              TechEffect.FuelDrain, 0.06f, 260, 7, 1, 5, 1.9f, "hull5");

            N("fu2", "회수 정제", "연료통 회복량 +20%", TechBranch.Hull, -6, -2,
              TechEffect.FuelPickupBonus, 0.20f, 200, 6, 0, 4, 1.8f, "hull6");

            N("fu3", "대형 탱크", "최대 연료 +140", TechBranch.Hull, -6, -3,
              TechEffect.FuelMax, 140f, 420, 12, 2, 4, 1.9f, "fu1");

            // ==========================================================================
            //  🔴 견인 — 이 게임의 핵심 결정("얼마나 싣고 갈까")을 직접 민다
            // ==========================================================================
            N("tw1", "보강 견인대", "끌 수 있는 칸 +1", TechBranch.Salvage, 3, -3,
              TechEffect.TowCapacity, 1f, 300, 8, 1, 4, 2.1f, "sal4");

            N("tw2", "관성 제어", "짐이 덜 무겁다 (+12%)", TechBranch.Drive, 4, -3,
              TechEffect.TowWeight, 0.12f, 240, 7, 1, 5, 1.85f, "drv3");

            N("tw3", "예비 드론", "드론이 한 대 더 붙는다", TechBranch.Salvage, 6, -3,
              TechEffect.CarrierDrone, 1f, 880, 24, 6, 2, 2.4f, "sal_drone");

            // ==========================================================================
            //  특수 — 런을 시작하는 조건 자체를 바꾼다 (위쪽 바깥)
            //  🔴 여기가 제일 비싸다. **판이 시작되는 모양**을 바꾸는 것이라
            //     숫자 노드 수십 개보다 체감이 크다.
            // ==========================================================================
            // 🔴 카드·레벨이 없어져(2026-08-26) 죽은 두 노드를 **끌고 다니는 쪽**으로 갈아끼웠다.
            //    지금 판에서 제일 큰 결정이 "얼마나 싣고 갈까"이므로 특수 노드도 거기 붙는다.
            N("sp_card1", "화물 정렬기", "짐이 덜 무겁다 (+25%)", TechBranch.Special, 0, 3,
              TechEffect.TowWeight, 0.25f, 280, 8, 1, 2, 2.2f, "pow1", "drv1");

            N("sp_lv1", "화물 증설", "끌 수 있는 칸 +2", TechBranch.Special, -1, 3,
              TechEffect.TowCapacity, 2f, 320, 9, 1, 3, 2.1f, "sp_card1");

            N("sp_wlv1", "숙련 정비", "시작 무기 레벨 +1", TechBranch.Special, 1, 3,
              TechEffect.StartWeaponLevel, 1f, 360, 10, 2, 3, 2.1f, "sp_card1");

            N("sp_combo1", "정제 압축", "가져온 재화 +15%", TechBranch.Special, 0, 4,
              TechEffect.MatFindAll, 0.15f, 560, 15, 4, 3, 2.3f, "sp_lv1", "sp_wlv1");

            // ---- 계열 끝 노드 (코어를 크게 요구한다) ----
            N("sp_power", "과부하 정비", "전 무기 피해 +25%", TechBranch.Special, 2, 3,
              TechEffect.WeaponPower, 0.25f, 800, 22, 6, 2, 2.4f, "pow8");

            N("sp_hull", "불침 설계", "최대 연료 +150", TechBranch.Special, -2, 3,
              TechEffect.FuelMax, 150f, 720, 20, 5, 2, 2.4f, "hull6");

            N("sp_value", "암거래망", "크레딧 +30%", TechBranch.Special, 3, 3,
              TechEffect.ValueMul, 0.30f, 880, 24, 6, 2, 2.4f, "mat4");

            N("sp_speed", "곡예 기동", "이동 속도 +15%", TechBranch.Special, -3, 3,
              TechEffect.MoveSpeed, 0.15f, 760, 21, 5, 2, 2.4f, "drv7");


            // ==========================================================================
            //  🔴 2차 확장 (2026-08-26 밤) — 사장님: *"테크트리를 최대한 많이 늘려라.
            //     독특한 것이 많을수록 좋다"*
            //
            //     기준을 하나 세웠다: **숫자만 오르는 노드는 더 안 만든다.**
            //     이미 충분하고, 그건 살 때 아무 생각이 안 든다.
            //     여기 있는 것들은 전부 *판이 굴러가는 모양*을 바꾼다 —
            //     터지고, 옮겨붙고, 자동으로 줍고, 보스에서 연료가 나온다.
            // ==========================================================================

            // ---- 발동형 2차 (오른쪽 맨 위 줄) ----
            N("pr8", "충격 기폭", "부술 때 8% 확률로 그 자리가 터진다", TechBranch.Power, 2, 5,
              TechEffect.KillBlast, 0.08f, 300, 8, 1, 5, 1.9f, "pr2");

            N("pr9", "연쇄 기폭", "부술 때 터질 확률 +11%p", TechBranch.Power, 3, 5,
              TechEffect.KillBlast, 0.11f, 700, 19, 4, 4, 2.05f, "pr8");

            N("pr10", "고속 사출", "투사체 속도 +18%", TechBranch.Power, 4, 5,
              TechEffect.ShotSpeed, 0.18f, 190, 5, 0, 5, 1.8f, "pow2");

            N("pr11", "자기 가속로", "투사체 속도 +25%", TechBranch.Power, 5, 5,
              TechEffect.ShotSpeed, 0.25f, 520, 14, 3, 3, 2.0f, "pr10");

            N("pr12", "여열 순환", "부수면 빨라지는 폭 +20%p", TechBranch.Drive, 6, 5,
              TechEffect.KillSpeed, 0.20f, 620, 17, 3, 3, 2.0f, "pr7");

            N("pr13", "폭발 확산", "폭발 반경 +12% (사거리)", TechBranch.Power, 6, 3,
              TechEffect.WeaponRange, 0.12f, 460, 13, 2, 4, 1.95f, "pr2");

            N("pr14", "관통 탄자", "전 무기 관통 +1", TechBranch.Power, 7, 3,
              TechEffect.WeaponPierceOne, 1f, 540, 15, 3, 3, 2.2f, "pr13");

            // ---- 드롭형 2차 (오른쪽 맨 아래 줄) ----
            N("dr7", "잔해 선별", "재화 드랍률 +12%", TechBranch.Salvage, 3, -6,
              TechEffect.MatFindAll, 0.12f, 240, 6, 0, 5, 1.85f, "sal3");

            N("dr8", "정련 회수", "주울 때마다 연료 +0.2", TechBranch.Salvage, 4, -6,
              TechEffect.RefineOnCollect, 0.2f, 340, 9, 1, 4, 1.95f, "dr7");

            N("dr9", "덩어리 안정", "떨어진 덩어리가 +25초 더 남는다", TechBranch.Salvage, 5, -6,
              TechEffect.LumpLife, 25f, 400, 11, 2, 3, 1.9f, "dr6");

            N("dr10", "삼중 회수", "두 배로 나올 확률 +12%p", TechBranch.Salvage, 6, -6,
              TechEffect.MatDoubleChance, 0.12f, 940, 26, 6, 3, 2.2f, "dr2");

            N("dr11", "감정 숙련", "가져온 재화 값어치 +14%", TechBranch.Salvage, 7, -6,
              TechEffect.MatValue, 0.14f, 700, 19, 4, 4, 2.0f, "dr5");

            N("dr12", "희귀광 감별", "희귀 재화 확률 +55%p", TechBranch.Salvage, 8, -3,
              TechEffect.RareMatChance, 0.55f, 1200, 32, 8, 3, 2.3f, "dr4");

            // ---- 견인형 2차 (아래 가운데) ----
            N("tw4", "화물 격벽", "끌 수 있는 칸 +1", TechBranch.Salvage, 2, -6,
              TechEffect.TowCapacity, 1f, 620, 17, 3, 4, 2.2f, "tw1");

            N("tw5", "중력 상쇄", "짐이 덜 무겁다 (+16%)", TechBranch.Drive, 1, -6,
              TechEffect.TowWeight, 0.16f, 540, 15, 3, 4, 1.95f, "tw2");

            // 🔴 자동 회수. **빈 칸일 때만** 줍는다 — 밀어내기는 끝까지 손으로 한다.
            //    그 판단이 이 게임의 특색이라 자동화하면 게임이 없어진다.
            N("tw6", "자동 견인 팔", "빈 칸이 있으면 알아서 줍는다", TechBranch.Salvage, 2, -7,
              TechEffect.TowAuto, 1f, 1100, 30, 8, 1, 1f, "tw4");

            N("tw7", "예비 드론 II", "드론이 한 대 더 붙는다", TechBranch.Salvage, 3, -7,
              TechEffect.CarrierDrone, 1f, 1600, 44, 12, 2, 2.6f, "tw3");

            // ---- 보스형 2차 (오른쪽 바깥 기둥) ----
            N("bs4", "탄막 예측", "보스 탄 피해 -16%p", TechBranch.Hull, 8, 1,
              TechEffect.BossShotResist, 0.16f, 580, 16, 3, 4, 2.0f, "bs1");

            N("bs5", "약점 조준", "보스에게 주는 피해 +22%p", TechBranch.Power, 8, 2,
              TechEffect.BossDamage, 0.22f, 820, 22, 6, 4, 2.1f, "bs2");

            // 🔴 보스전은 연료가 제일 빨리 새는 구간이다. 여기에 숨통을 하나 둔다 —
            //    없으면 "보스에 닿았는데 연료가 없어 못 끝낸다"가 반복된다.
            N("bs6", "격파 회수로", "보스 부위를 부술 때마다 연료 +6", TechBranch.Hull, 8, 0,
              TechEffect.BossFuel, 6f, 660, 18, 4, 4, 2.05f, "bs1");

            N("bs7", "전리품 분류", "보스에서 나오는 재화 +45%p", TechBranch.Salvage, 8, -1,
              TechEffect.BossMatBonus, 0.45f, 1250, 34, 9, 3, 2.3f, "bs3");

            // ---- 연료형 2차 (왼쪽 바깥 기둥) ----
            N("fu4", "저온 순환", "연료 감소 -8%p", TechBranch.Hull, -7, -3,
              TechEffect.FuelDrain, 0.08f, 640, 18, 4, 4, 2.05f, "fu1");

            N("fu5", "예열 출항", "시작 연료가 최대치의 +12% 더", TechBranch.Hull, -7, -2,
              TechEffect.StartFuel, 0.12f, 300, 8, 1, 4, 1.9f, "fu2");

            N("fu6", "정제 탱크", "연료통 회복량 +28%p", TechBranch.Hull, -8, -2,
              TechEffect.FuelPickupBonus, 0.28f, 560, 15, 3, 3, 2.0f, "fu2");

            N("fu7", "장기 조업", "최대 연료 +200", TechBranch.Hull, -7, -1,
              TechEffect.FuelMax, 200f, 900, 25, 6, 3, 2.2f, "fu3");

            // ---- 기동 2차 (왼쪽 아래) ----
            N("mv1", "긴급 분사", "대시 쿨다운 -9%", TechBranch.Drive, -8, -3,
              TechEffect.DashCooldown, 0.09f, 280, 8, 1, 4, 1.9f, "drv6");

            N("mv2", "자세 제어", "감쇠 +0.35 (더 잘 멈춘다)", TechBranch.Drive, -6, -1,
              TechEffect.Handling, 0.35f, 220, 6, 0, 4, 1.85f, "drv4");

            N("mv3", "보조 추진", "추진력 +90", TechBranch.Drive, -7, 0,
              TechEffect.Thrust, 90f, 380, 10, 2, 4, 1.95f, "drv5");

            // ---- 수집 2차 ----
            N("iv1", "광역 흡입", "흡수 반경 +14%", TechBranch.Salvage, 9, -2,
              TechEffect.IntakeRadius, 0.14f, 260, 7, 1, 5, 1.85f, "sal4");

            N("iv2", "아이템 감지", "아이템 드랍률 +6%p", TechBranch.Salvage, 9, -3,
              TechEffect.ItemDropChance, 0.06f, 420, 12, 2, 4, 2.0f, "iv1");

            c.techTree = buf.ToArray();
            buf.Clear();
        }
    }
}
