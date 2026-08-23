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
    public enum MatKind
    {
        Scrap = 0,   // 고철
        Circuit,     // 회로
        Core         // 코어
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
            }
            return "?";
        }

        public static Color ColorOf(MatKind m)
        {
            switch (m)
            {
                case MatKind.Scrap:   return new Color(0.78f, 0.80f, 0.86f);
                case MatKind.Circuit: return new Color(0.45f, 0.95f, 0.80f);
                case MatKind.Core:    return new Color(1.00f, 0.55f, 0.95f);
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

        // ---- 무기 전용 (특색) ----
        BladeCount,         // 절단날 +N개
        BladeSpin,          // 절단날 회전 +N%
        HarpoonCount,       // 작살 발사 수 +N
        HarpoonPierce,      // 작살 관통 +N
        VortexRadius,       // 소용돌이 반경 +N%
        VortexDamage,       // 소용돌이 피해 +N%
        BombCount,          // 폭탄 +N개
        BombRadius,         // 폭발 반경 +N%
        ArcTargets,         // 방전 대상 +N
        ArcRange,           // 방전 사거리 +N%

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

        // ---- 런 시작 상태 ----
        StartLevel,         // 시작 레벨 +N
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
            int b = m == MatKind.Scrap ? costScrap : m == MatKind.Circuit ? costCircuit : costCore;
            if (b <= 0) return 0;
            float mul = Mathf.Pow(Mathf.Max(1f, costGrowth), Mathf.Max(0, nextRank - 1));
            return Mathf.Max(1, Mathf.RoundToInt(b * mul));
        }

        public bool IsFree => costScrap <= 0 && costCircuit <= 0 && costCore <= 0;
    }
}
