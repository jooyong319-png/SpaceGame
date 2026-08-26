using System;
using UnityEngine;

namespace SalvageRun.Data
{
    // ==================================================================================
    //  영구 재화
    // ==================================================================================

    /// <summary>
    /// 🔴 재화를 셋으로 나눈 이유.
    ///    하나면 "많이 모으면 다 산다"가 되어 **순서가 사라진다.**
    ///    희귀한 것이 게이트를 맡으면 흔한 것은 잔돈이 되고,
    ///    흔한 것만 있으면 깊은 노드가 그냥 '시간'이 된다.
    ///
    ///    고철  = 양으로 사는 것 (얕은 노드, 랭크 반복)
    ///    회로  = 갈림길을 여는 것 (중간 노드)
    ///    코어  = 판을 바꾸는 것 (끝 노드) — 큰 쓰레기와 보스에서만 나온다
    /// </summary>
    /// <summary>
    /// 🔴 **깊이 갈수록 새 재화가 나온다** (2026-08-26 · Space Rock Breaker 방향).
    ///    스토어 문구가 *"깊이 들어갈수록 새롭고 값진 광석"*이고, 그게 그 게임 진행의 축이다.
    ///    구역을 여는 이유가 "더 빨리 번다"가 아니라 **"여기서만 나오는 게 있다"**여야 한다.
    ///
    /// ⚠️ **기존 셋의 정수값을 절대 바꾸지 말 것.** 저장이 인덱스로 물려 있다
    ///    (`MetaData.selectedWeapon`처럼 순서가 곧 뜻인 자리가 있다). 새 것은 **뒤에만** 붙인다.
    /// </summary>
    public enum MatKind
    {
        Scrap = 0,   // 고철   — 어디서나
        Circuit,     // 회로   — 2구역부터
        Core,        // 코어   — 3구역부터
        Alloy,       // 초합금 — 4구역부터
        Crystal,     // 냉각결정 — 5구역부터
        Isotope      // 동위원소 — 6구역
    }

    public static class Mats
    {
        // 🔴 enum에서 뽑는다. 손으로 센 상수는 enum이 늘 때 조용히 어긋나고,
        //    컴파일은 통과한 채 **런타임에만** 터진다 (2026-08-21 드릴 사고).
        public static readonly int Count = System.Enum.GetValues(typeof(MatKind)).Length;

        public static string Name(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return "고철";
                case MatKind.Circuit: return "회로";
                case MatKind.Core:    return "코어";
                case MatKind.Alloy:   return "초합금";
                case MatKind.Crystal: return "냉각결정";
                case MatKind.Isotope: return "동위원소";
            }
            return "?";
        }

        /// <summary>
        /// 🔴 한 덩어리의 값어치. **종류가 곧 값**이다.
        ///    깊은 구역 재화일수록 한 덩어리가 크게 값나가야
        ///    *"칸 하나를 무엇에 쓸까"*가 진짜 계산이 된다.
        /// </summary>
        public static int WorthOf(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return 8;
                case MatKind.Circuit: return 24;
                case MatKind.Core:    return 60;
                case MatKind.Alloy:   return 140;
                case MatKind.Crystal: return 320;
                case MatKind.Isotope: return 700;
            }
            return 1;
        }

        /// <summary>덩어리 크기. 값진 것일수록 크게 — 멀리서 "저기 있다"가 보여야 한다.</summary>
        /// <summary>
        /// 🔴 **덩어리 하나에 담기는 양** (2026-08-27 · 사장님이 고르신 *"덩어리 값어치를 키운다"*).
        ///
        ///    한 판 수입이 **견인 칸(6개) × 덩어리당 양**으로 정해진다 —
        ///    판이 길어져도, 많이 부숴도 수입이 안 는다. **칸이 딱딱한 천장**이기 때문이다.
        ///    그래서 수입을 올리는 길은 *칸을 늘리거나 덩어리를 키우거나* 둘뿐이고,
        ///    칸을 늘리면 사장님이 넣으신 *"가져갈까 버릴까"*가 무뎌진다. **덩어리를 키운다.**
        ///
        ///    ⚠️ 실측: 덩어리당 1~2였을 때 한 판 고철 **6개**. 1구역 전부 사는 데 **190판**이었다.
        ///
        ///    🔴 깊은 재화일수록 **덩어리는 작다.** 값이 비싸서가 아니라
        ///       **노드 비용의 자릿수가 다르기 때문**이다 (고철 1600 vs 코어 12).
        ///       그래서 코어 두 개가 고철 수백 개만큼 무겁다 —
        ///       6칸에 무엇을 실을지가 여기서 갈린다.
        /// </summary>
        public static int LumpOf(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return 6;
                case MatKind.Circuit: return 3;
                case MatKind.Core:    return 2;
                case MatKind.Alloy:   return 1;
                case MatKind.Crystal: return 1;
                case MatKind.Isotope: return 1;
            }
            return 1;
        }

        public static float ScaleOf(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return 0.68f;
                case MatKind.Circuit: return 0.84f;
                case MatKind.Core:    return 1.05f;
                case MatKind.Alloy:   return 1.20f;
                case MatKind.Crystal: return 1.35f;
                case MatKind.Isotope: return 1.55f;
            }
            return 0.7f;
        }

        /// <summary>이 재화가 처음 나오는 구역 등급(`StageDef.rank`).</summary>
        public static int FirstRank(MatKind m) => (int)m + 1;

        public static Color ColorOf(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return new Color(0.78f, 0.80f, 0.86f);   // 흐린 회색
                case MatKind.Circuit: return new Color(0.45f, 0.95f, 0.80f);   // 청록
                case MatKind.Core:    return new Color(1.00f, 0.55f, 0.95f);   // 분홍
                case MatKind.Alloy:   return new Color(1.00f, 0.78f, 0.35f);   // 금색
                case MatKind.Crystal: return new Color(0.55f, 0.75f, 1.00f);   // 얼음빛
                case MatKind.Isotope: return new Color(0.70f, 1.00f, 0.35f);   // 형광 연두
            }
            return Color.white;
        }
    }

    // ==================================================================================
    //  테크트리
    // ==================================================================================

    /// <summary>
    /// 영구 강화 노드가 하는 일.
    ///
    /// 🔴 여기 있는 것은 전부 **런을 시작하기 전에** 결정된다.
    ///    런 안의 성장(카드)과 역할이 겹치면 둘 다 밋밋해지므로,
    ///    카드가 "이번 판을 어떻게 풀까"라면 테크는 "어떤 배로 출발할까"다.
    /// </summary>
    public enum TechEffect
    {
        None = 0,

        // ---- 선체 ----
        FuelMax,            // 최대 연료 +N
        ContactResist,      // 충돌 피해 -N%
        StartFuel,          // 시작 연료를 최대치의 +N% 더
        Revive,             // 격침 시 1회 부활

        // ---- 기동 ----
        MoveSpeed,          // 이동 속도 +N%
        Thrust,             // 추진력 +N
        Handling,           // 감쇠 +N (더 잘 멈춘다)
        DashCooldown,       // 대시 쿨다운 -N%

        // ---- 화력 (전 무기 공통) ----
        WeaponPower,        // 피해 +N%
        WeaponRange,        // 사거리 +N%
        WeaponCooldown,     // 쿨다운 -N%
        BossDamage,         // 보스에게 주는 피해 +N%

        // ---- 🔴 무기별 (2026-08-23) ----
        //
        // 🔴 사장님 지시: *"공용이 있고, 무기별로 따로 있는 방식."*
        //    위의 `WeaponPower`·`WeaponRange`·`WeaponCooldown`이 **공용**이고,
        //    아래는 **어느 무기 하나**에만 붙는다 — 어느 무기인지는 `TechNodeDef.weapon`.
        //
        //    ⚠️ 예전에는 이 자리에 `HarpoonCount` 같은 **패턴 단위** 효과가 있었다.
        //       그건 같은 패턴을 쓰는 무기를 **같이** 올려서(작살 노드가 원반까지 키웠다)
        //       "무기마다 다른 길"이 성립하지 않았다.
        WeaponPowerOne,     // 이 무기 피해 +N%
        WeaponRangeOne,     // 이 무기 사거리 +N%
        WeaponCooldownOne,  // 이 무기 쿨다운 -N%
        WeaponCountOne,     // 이 무기 발사 수 · 연쇄 대상 +N
        WeaponPierceOne,    // 이 무기 관통 +N

        // ---- 수집 · 경제 ----
        IntakeRadius,       // 흡수 반경 +N%
        ValueMul,           // 크레딧 +N%
        XpMul,              // 경험치 +N%
        RefineOnCollect,    // 파편마다 연료 +N
        ItemDropChance,     // 아이템 드랍률 +N%p
        FuelPickupBonus,    // 연료 아이템 회복량 +N%

        // ---- 재화 발견 ----
        ScrapFind,          // 고철 드랍률 +N%
        CircuitFind,        // 회로 드랍률 +N%
        CoreFind,           // 코어 드랍률 +N%
        MatFindAll,         // 전 재화 드랍률 +N%

        /// <summary>
        /// 🔴 **무기를 연다** (2026-08-23 사장님: *"테크트리 하나 안에 무기 종류를 넣어라"*).
        ///    어느 무기인지는 `TechNodeDef.weapon`이 정한다.
        ///    이미 연 노드를 다시 누르면 **그 무기를 골라 든다** (`MetaSave.SelectWeapon`).
        ///
        ///    ⚠️ 스탯을 안 바꾼다. 다른 효과와 달리 `BuildStats`에서 값을 더하지 않고,
        ///       "무엇을 들고 시작하는가"를 정할 뿐이다.
        /// </summary>
        UnlockWeapon,

        /// <summary>
        /// 🔴 **끌 때 덜 무겁다** (2026-08-26). 값이 클수록 같은 개수를 끌어도 덜 느려진다.
        ///    지금 판에서 제일 큰 결정이 *"얼마나 싣고 갈까"*라 여기가 그 손잡이다.
        /// </summary>
        TowWeight,

        /// <summary>🔴 **끌 수 있는 개수 +N.** 무게 감소와 달리 **벽 자체를 밀어낸다.</summary>
        TowCapacity,

        /// <summary>🔴 **보스 탄 피해 -N%.** 위협이 보스에만 있으므로 방어도 여기만 있다.</summary>
        BossShotResist,

        /// <summary>보스에서 나오는 재화 +N%.</summary>
        BossMatBonus,

        // ---- 🔴 발동형 (2026-08-26 사장님: *"독특한 것이 많으면 많을수록 좋아"*) ----
        //
        //    숫자만 오르는 노드는 이미 충분하다. **가끔 터지는 것**이 있어야
        //    같은 판이 판마다 다르게 느껴진다 — 인크리멘탈의 도파민은 여기서 나온다.
        //    ⚠️ 전부 `RunStats`에 실리고 `WeaponRig`/`StageField`가 **실제로 읽는다.**
        //       정의만 하고 아무도 안 읽는 효과를 만들면 돈만 먹는 노드가 된다.

        /// <summary>맞힐 때 N% 확률로 그 자리가 터진다.</summary>
        ProcExplode,

        /// <summary>부술 때 N% 확률로 번개가 옮겨붙는다.</summary>
        ProcChain,

        /// <summary>N% 확률로 한 번 더 쏜다.</summary>
        ProcDoubleShot,

        /// <summary>부수면 잠깐 빨라진다 (+N%, 2초).</summary>
        KillSpeed,

        // ---- 🔴 드롭형 ----

        /// <summary>재화가 **두 배로** 나올 확률 +N%.</summary>
        MatDoubleChance,

        /// <summary>희귀 재화(회로 이상)가 나올 확률 배수 +N.</summary>
        RareMatChance,

        /// <summary>가져온 재화의 값어치 +N%.</summary>
        MatValue,

        /// <summary>밀려나거나 떨어진 덩어리가 **더 오래 남는다** (+N초).</summary>
        LumpLife,

        /// <summary>
        /// 🔴 **연료가 천천히 준다** (-N%). 연료가 곧 한 판의 길이이므로
        ///    이건 "판이 길어진다"와 같은 말이다 — 최대치를 올리는 것과 다른 축이다.
        /// </summary>
        FuelDrain,

        /// <summary>
        /// 🔴 **부순 순간** N% 확률로 주변이 터진다. `ProcExplode`(맞힐 때)와 짝이지만
        ///    뜻이 다르다 — 이건 **빽빽한 곳에서 연쇄**가 되고, 저건 큰 것에 꽂힌다.
        /// </summary>
        KillBlast,

        /// <summary>🔴 보스 부위를 부술 때마다 연료 +N. 보스전이 연료 싸움이라 이게 숨통이다.</summary>
        BossFuel,

        /// <summary>🔴 투사체가 빨라진다 (+N%). 사거리와 달리 **닿는 데 걸리는 시간**을 줄인다.</summary>
        ShotSpeed,

        /// <summary>
        /// 🔴 **칸이 비어 있을 때만** Space 없이 줍는다.
        ///    ⚠️ 꽉 찼을 때는 절대 자동으로 안 줍는다 — 밀어내기 판단이 이 게임의 핵심이라
        ///       거기까지 자동이 되면 "이것만 가져갈까"가 사라진다.
        /// </summary>
        TowAuto,

        /// <summary>
        /// 🔴 **회수 드론 +N대.** 한 대가 배 옆에 떠서 제 줄을 끈다 (`RunDirector.DroneCarry`칸).
        ///    칸 노드와 달리 **화면에 보인다** — 산 것이 눈에 보여야 강해진 게 남는다.
        /// </summary>
        CarrierDrone,

        /// <summary>
        /// 🔴 **직송 드론 — 인벤토리를 거치지 않고 바로 집으로 보낸다** (2026-08-27 사장님 지시:
        ///    *"인벤토리에 포함되지 않고 그냥 자동으로 집으로 가져가는 드론"*).
        ///
        ///    실측이 이 문제를 정확히 짚어 줬다: 2구역에서 **주움 116 · 가져옴 11** —
        ///    부순 것의 90%를 버린다. 한 판 수입이 **칸 수 × 덩어리**로 고정이기 때문이다.
        ///    칸을 늘리면 *"가져갈까 버릴까"*가 무뎌지고, 덩어리는 이미 키웠다.
        ///    **드론은 그 상한 자체를 우회한다** — 견인 줄에 안 들어가니
        ///    결정은 그대로 두고 수입만 는다.
        ///
        ///    🔴 **등급으로 나눈다** (사장님: *"강화하면서 점점 높은 것도 가져갈 수 있게"*).
        ///    랭크 1이면 고철만, 2면 회로까지… 6이면 전부.
        ///    그래서 **값진 것은 한동안 직접 실어야 한다** — 견인 결정이 살아남는다.
        ///    드론이 처음부터 다 가져가면 이 게임의 유일한 결정이 사라진다.
        /// </summary>
        HaulerGrade,

        // ---- 런 시작 상태 ----
        StartLevel,         // ⬜ 레벨업이 없다 (2026-08-26). 읽는 곳 없음
        StartWeaponLevel,   // 시작 무기 레벨 +N
        CardChoices,        // 카드 선택지 +N
        ComboLevelDown      // 조합 발동 요구 레벨 -N
    }

    /// <summary>표시 계열. 색과 묶음에만 쓴다.</summary>
    public enum TechBranch
    {
        Core = 0,   // 중앙 — 뿌리
        Hull,       // 선체
        Drive,      // 기동
        Power,      // 화력
        Salvage,    // 수집
        Weapon,     // 무기 특색
        Special     // 특수 — 조합 · 시작 상태
    }

    /// <summary>
    /// 테크 노드 하나.
    ///
    /// 🔴 격자 좌표(<see cref="cell"/>)와 선행 노드(<see cref="requires"/>)가 그림을 만든다.
    ///    선은 요구 관계에서 자동으로 그려진다 — 선을 따로 데이터로 두면 반드시 어긋난다.
    /// </summary>
    [Serializable]
    public class TechNodeDef
    {
        public string id;
        public string title;
        [TextArea] public string description;

        public TechBranch branch;

        [Tooltip("격자 위치. (0,0)이 뿌리")]
        public Vector2Int cell;

        [Tooltip("선행 노드 id. 하나라도 안 찍혀 있으면 잠긴다. 비우면 항상 열림")]
        public string[] requires;

        [Tooltip("몇 번까지 찍을 수 있는가. 1이면 한 번뿐")]
        public int maxRank = 1;

        [Tooltip("1랭크 비용")]
        public int costScrap;
        public int costCircuit;
        public int costCore;

        /// <summary>
        /// 🔴 **깊은 구역 재화도 값으로 쓴다** (2026-08-27).
        ///
        ///    8/26에 재화를 3종 → 6종으로 늘리면서 **버는 쪽만 만들고 쓰는 쪽을 안 만들었다.**
        ///    초합금·냉각결정·동위원소는 떨어지기만 하고 쓸 데가 없었다 —
        ///    깊이 갈 이유가 *"여기서만 나오는 게 있다"*인데 그게 아무 데도 안 쓰이면
        ///    **깊이 가는 이유 자체가 없어진다.**
        ///
        ///    ⚠️ 기존 108개 노드는 이 값을 안 쓴다(0). `Deep(...)`으로 붙인 것만 쓴다 —
        ///       그래야 호출 108곳을 안 건드린다.
        /// </summary>
        public int costAlloy;
        public int costCrystal;
        public int costIsotope;

        /// <summary>이 재화의 1랭크 기본 비용. **여섯 종류가 한 곳에서 답한다.**</summary>
        public int BaseCost(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return costScrap;
                case MatKind.Circuit: return costCircuit;
                case MatKind.Core:    return costCore;
                case MatKind.Alloy:   return costAlloy;
                case MatKind.Crystal: return costCrystal;
                case MatKind.Isotope: return costIsotope;
            }
            return 0;
        }

        [Tooltip("🔴 랭크마다 비용이 이 배수로 는다. 1.0이면 균일 — " +
                 "균일하면 후반에 잔돈이 남아 아무 의미 없이 다 찍게 된다")]
        public float costGrowth = 1.55f;

        public TechEffect effect;

        [Tooltip("랭크 1당 효과량")]
        public float value;

        [Tooltip("무기 전용 노드에서만 쓴다")]
        public WeaponKind weapon;

        /// <summary>랭크 n(1부터)을 찍는 데 드는 비용.</summary>
        public int CostAt(MatKind m, int nextRank)
        {
            int b = BaseCost(m);
            if (b <= 0) return 0;
            float mul = Mathf.Pow(Mathf.Max(1f, costGrowth), Mathf.Max(0, nextRank - 1));
            return Mathf.Max(1, Mathf.RoundToInt(b * mul));
        }

        /// <summary>⚠️ **여섯 종류를 다 본다.** 셋만 보면 깊은 재화만 드는 노드가 공짜가 된다.</summary>
        public bool IsFree
        {
            get
            {
                for (int i = 0; i < Mats.Count; i++)
                    if (BaseCost((MatKind)i) > 0) return false;
                return true;
            }
        }
    }
}
