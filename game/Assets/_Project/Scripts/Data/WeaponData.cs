using System;
using UnityEngine;

namespace SalvageRun.Data
{
    // ==================================================================================
    //  무기
    // ==================================================================================

    /// <summary>
    /// 🔴 무기의 **작동 방식**. 무기 12종은 이 10가지 패턴에 숫자를 다르게 넣은 것이다.
    ///    패턴을 데이터로 빼두면 무기를 20종으로 늘려도 `WeaponRig`는 안 커진다 —
    ///    무기마다 전용 코드를 쓰기 시작하면 5종에서 이미 손을 못 대게 된다.
    /// </summary>
    public enum WeaponPattern
    {
        Orbit = 0,      // 배 주위를 도는 것들 (절단날 · 방벽)
        Boomerang,      // 던져서 돌아온다 (원반)
        Projectile,     // 커서 방향 발사 (작살)
        Beam,           // 커서 방향 지속 광선 (레이저)
        Chain,          // 가까운 것들에게 연쇄 (방전)
        PeriodicAoe,    // 주기적으로 근처에서 폭발 (폭탄)
        Nova,           // 배를 중심으로 원형 파동 (충격파)
        Mine,           // 제자리에 두고 간다 (지뢰)
        Aura,           // 배 주위 지속 장판 (소용돌이)
        Well,           // 한 점으로 끌어모은다 (중력 우물)
        Companion,      // 따라다니며 스스로 일한다 (드론)

        /// <summary>
        /// 🔴 **드릴** (rev.10). 목표 하나에 붙어 **지속적으로** 갈아낸다.
        ///    다른 패턴과 결정적으로 다른 점: **캐는 동안 배가 묶인다.**
        ///
        ///    채굴은 그 자체로는 재미가 없다 — 가만히 있는 돌에 버튼을 누르는 일이다.
        ///    재미는 **"지금 이걸 캘까, 로봇 먼저 처리할까, 그냥 뺄까"**에서 나온다.
        ///    그러려면 캐는 시간이 **무방비한 시간**이어야 한다. 그게 이 패턴의 전부다.
        /// </summary>
        Drill
    }

    /// <summary>
    /// 🔴 무기 계열. **조합은 무기 쌍이 아니라 태그 쌍으로 정의한다.**
    ///    무기 12종이면 쌍이 66개다 — 손으로 다 쓸 수도 없고 대부분 아무도 안 본다.
    ///    태그 6종이면 21가지로 끝나고, 무기를 20종으로 늘려도 조합표는 그대로다.
    ///    같은 태그끼리 짝지으면 **특화** 조합이 된다.
    /// </summary>
    public enum WeaponTag
    {
        Cut = 0,    // 절삭 — 붙어서 간다
        Pierce,     // 관통 — 선으로 뚫는다
        Shock,      // 전기 — 옮겨붙는다
        Blast,      // 폭발 — 면으로 터진다
        Field,      // 장판 — 자리를 차지한다
        Gravity     // 중력 — 모으고 끈다
    }

    public enum WeaponKind
    {
        Blade = 0,      // 회전 절단날   Orbit      Cut
        Discus,         // 회수 원반     Boomerang  Cut
        Harpoon,        // 견인 작살     Projectile Pierce
        Laser,          // 절단 레이저   Beam       Pierce
        Arc,            // 정전기 방출   Chain      Shock
        Nova,           // 충격파        Nova       Shock
        Bomb,           // 압축 폭탄     PeriodicAoe Blast
        Mine,           // 자기 지뢰     Mine       Blast
        Vortex,         // 흡입 소용돌이 Aura       Field
        Barrier,        // 플라즈마 방벽 Orbit      Field
        Well,           // 중력 우물     Well       Gravity
        Drone,          // 견인 드론     Companion  Gravity

        /// <summary>
        /// 🔴 **채굴 드릴** (rev.10). 이 게임의 새 기본 동사.
        ///    커서 방향 가까운 쓰레기 하나에 **붙어서 갈아낸다** —
        ///    쏘는 게 아니라 **시간을 들이는** 무기다.
        /// </summary>
        Drill
    }

    /// <summary>
    /// 무기 레벨이 오를 때 **숫자 말고 붙는 것**.
    ///
    /// 🔴 레벨업이 "피해 +10%"뿐이면 7레벨이나 3레벨이나 같은 무기다.
    ///    특정 레벨에서 **행동이 하나 늘어야** 키우는 맛이 난다 —
    ///    이 게임은 무기를 둘만 갖기 때문에 특히 그렇다.
    /// </summary>
    public enum WeaponTrait
    {
        None = 0,

        ExtraProjectile,   // 발사체/개체 +1
        ExtraPierce,       // 관통 +1
        Ricochet,          // 튕겨서 한 번 더
        Homing,            // 유도
        Shred,             // 남은 HP 비례 추가 피해
        Detonate,          // 이 무기로 부순 것이 작게 터진다
        Chain,             // 맞은 대상에서 방전이 튄다
        Slow,              // 맞은 대상이 느려진다
        Pull,              // 맞은 대상을 끌어당긴다
        Knockback,         // 맞은 대상을 밀어낸다
        Pierceless,        // 관통 무한 (끝 특성)
        WideArc,           // 범위/각도 확장
        DoubleTap,         // 한 주기에 두 번
        Overcharge,        // 쿨다운이 짧아질수록 피해가 오른다
        LifeSteal,         // 부술 때 연료 회복
        Magnetize,         // 부순 자리의 파편이 즉시 끌려온다

        // 🔴 아래는 **행동이 눈에 띄게 바뀌는** 특성이다.
        //    숫자만 오르는 강화는 강해진 게 안 보인다 —
        //    2026-08-22 사용자 제안: *"주위를 도는 무기가 강화하면 돌면서 총이 나간다던가"*
        OrbitGun,          // 궤도체가 돌면서 바깥으로 쏜다 ✅
        Split,             // 발사체가 명중하면 갈라진다 ✅

        // 🔴 아래 둘은 **아직 구현이 없다.** `WeaponRig`이 이 값을 읽지 않는다.
        //    구현하기 전에는 어떤 무기에도 붙이지 말 것 —
        //    붙이면 카드에는 뜨는데 아무 일도 안 일어나서, 플레이어가 헛것을 고르게 된다.
        //    (2026-08-22에 '이중 방벽'을 그렇게 내보낼 뻔했다)
        Vent,              // 🚧 장판이 빨아들인 것을 주기적으로 뱉어낸다
        Twin               // 🚧 궤도가 두 겹이 된다
    }

    [Serializable]
    public class WeaponTraitDef
    {
        [Tooltip("이 레벨이 되면 붙는다")]
        public int atLevel = 3;
        public WeaponTrait trait;
        public string title;
        [TextArea] public string description;
        [Tooltip("특성마다 의미가 다르다 — 추가 발사 수, 감속 비율 등")]
        public float value = 1f;
    }

    [Serializable]
    public class WeaponDef
    {
        public WeaponKind kind;
        public string displayName;
        [TextArea] public string description;

        public WeaponPattern pattern;
        public WeaponTag tag;

        [Header("기본 수치 (레벨 1)")]
        public float damage = 10f;
        [Tooltip("초당 몇 번 작동하는가. Orbit·Aura·Beam처럼 상시인 것은 0")]
        public float cooldown = 1f;
        public float range = 4f;
        [Tooltip("개수 — 날 수 / 발사 수 / 지뢰 수")]
        public int count = 1;
        public float projectileSpeed = 30f;
        public int pierce = 1;

        [Header("레벨당 증가")]
        public float damagePerLevel = 3f;
        public float rangePerLevel = 0.15f;
        [Tooltip("레벨마다 쿨다운에 곱해지는 값 (0.94 = 레벨당 6% 단축)")]
        public float cooldownPerLevel = 0.94f;
        [Tooltip("몇 레벨마다 개수가 하나 느는가. 0이면 안 는다")]
        public int countEveryLevels = 4;

        public Color color = Color.white;

        [Header("레벨 특성")]
        public WeaponTraitDef[] traits;

        public bool HasTraitAt(WeaponTrait t, int level)
        {
            if (traits == null) return false;
            for (int i = 0; i < traits.Length; i++)
                if (traits[i].trait == t && level >= traits[i].atLevel) return true;
            return false;
        }

        public float TraitValue(WeaponTrait t, int level)
        {
            if (traits == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < traits.Length; i++)
                if (traits[i].trait == t && level >= traits[i].atLevel) sum += traits[i].value;
            return sum;
        }

        /// <summary>이번 레벨에 새로 붙은 특성(HUD 알림용). 없으면 null.</summary>
        public WeaponTraitDef TraitUnlockedAt(int level)
        {
            if (traits == null) return null;
            for (int i = 0; i < traits.Length; i++)
                if (traits[i].atLevel == level) return traits[i];
            return null;
        }
    }

    public static class Weapons
    {
        /// <summary>
        /// 🔴 **무기 종류 수. `WeaponKind`를 늘리면 여기도 반드시 늘려야 한다.**
        ///
        ///    2026-08-21에 `Drill`을 13번째로 추가하고 이 값을 12로 둔 채 빌드했다.
        ///    `RunStats.weaponLevel`이 `new int[Count]`라서 드릴을 고르는 순간
        ///    **IndexOutOfRange로 판이 죽었다.** 컴파일은 통과한다 — 런타임에만 터진다.
        ///
        ///    그래서 상수를 손으로 세지 않고 **enum에서 뽑는다.** 다시는 안 어긋난다.
        /// </summary>
        public static readonly int Count = System.Enum.GetValues(typeof(WeaponKind)).Length;
        public static readonly int TagCount = System.Enum.GetValues(typeof(WeaponTag)).Length;

        public static string Name(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Blade:   return "회전 절단날";
                case WeaponKind.Discus:  return "회수 원반";
                case WeaponKind.Harpoon: return "견인 작살";
                case WeaponKind.Laser:   return "절단 레이저";
                case WeaponKind.Arc:     return "정전기 방출";
                case WeaponKind.Nova:    return "충격파";
                case WeaponKind.Bomb:    return "압축 폭탄";
                case WeaponKind.Mine:    return "자기 지뢰";
                case WeaponKind.Drill:   return "채굴 드릴";
                case WeaponKind.Vortex:  return "흡입 소용돌이";
                case WeaponKind.Barrier: return "플라즈마 방벽";
                case WeaponKind.Well:    return "중력 우물";
                case WeaponKind.Drone:   return "견인 드론";
            }
            return "?";
        }

        public static string TagName(WeaponTag t)
        {
            switch (t)
            {
                case WeaponTag.Cut:     return "절삭";
                case WeaponTag.Pierce:  return "관통";
                case WeaponTag.Shock:   return "전기";
                case WeaponTag.Blast:   return "폭발";
                case WeaponTag.Field:   return "장판";
                case WeaponTag.Gravity: return "중력";
            }
            return "?";
        }
    }
}
