using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 무기 12종과 조합 21가지의 초기값.
    /// 수치 정본은 에셋이고 여기는 씨앗이다 — 의도는 주석으로만 남긴다.
    /// </summary>
    public static class WeaponDefaults
    {
        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        static WeaponTraitDef T(int atLevel, WeaponTrait trait, string title, string desc, float value = 1f)
            => new WeaponTraitDef { atLevel = atLevel, trait = trait, title = title, description = desc, value = value };

        // ==============================================================================
        //  무기 12종
        // ==============================================================================
        //  🔴 태그마다 정확히 2종씩 둔다. 한 태그에 몰리면 조합 21가지 중 절반이
        //     실제로는 안 나오는 표가 된다.
        //
        //  🔴 특성(traits)은 3·5·7·10레벨에 붙는다.
        //     무기를 둘만 갖는 구조라 한 무기를 10레벨까지 끌고 가는 판이 흔하다 —
        //     그때까지 "피해 +10%"만 나오면 키우는 맛이 없다.

        public static void FillWeapons(GameContent c)
        {
            c.weapons = new[]
            {

                new WeaponDef {
                    kind = WeaponKind.Discus, displayName = "회수 원반", tag = WeaponTag.Cut,
                    pattern = WeaponPattern.Boomerang,
                    description = "커서 방향으로 던져 돌아온다. 오가며 두 번 벤다",
                    damage = 14f, cooldown = 1.5f, range = 9f, count = 1,
                    projectileSpeed = 22f, pierce = 3,
                    damagePerLevel = 5f, rangePerLevel = 0.4f, cooldownPerLevel = 0.93f, countEveryLevels = 4,
                    color = C(180,255,220),
                    traits = new[] {
                        T(3,  WeaponTrait.ExtraPierce,     "얇은 날",   "관통이 늘어난다", 2f),
                        T(5,  WeaponTrait.Ricochet,        "도탄",      "돌아오는 길에 한 번 더 튄다"),
                        T(7,  WeaponTrait.ExtraProjectile, "쌍원반",    "원반을 하나 더 던진다"),
                        T(10, WeaponTrait.Split,           "분열 원반", "명중할 때마다 작은 원반이 갈라져 나간다", 2f),
                    }
                },

                // ---------------------------------------------------------- 폭발 (Blast)
                // 🔴 **느리지만 반드시 맞고, 터진다.** 빠른 무기가 빗나가는 자리를 메운다.
                //    쿨다운을 길게 잡은 이유: 한 발이 **사건**이어야 한다 —
                //    쉴 새 없이 나가면 그냥 느린 작살이다.
                new WeaponDef {
                    kind = WeaponKind.Missile, displayName = "유도 미사일", tag = WeaponTag.Blast,
                    pattern = WeaponPattern.Missile,
                    description = "느리게 날아가 목표를 따라간다. 닿으면 터진다",
                    damage = 22f, cooldown = 2.2f, range = 14f, count = 1,
                    projectileSpeed = 11f, pierce = 1,
                    damagePerLevel = 9f, rangePerLevel = 0.5f, cooldownPerLevel = 0.92f,
                    countEveryLevels = 4,
                    color = C(255,170,120),
                    traits = new[] {
                        T(3,  WeaponTrait.Homing,          "정밀 유도",  "더 급하게 꺾는다", 3f),
                        T(5,  WeaponTrait.Detonate,        "고폭탄두",   "터질 때 더 아프다", 12f),
                        T(7,  WeaponTrait.ExtraProjectile, "연장 발사관", "미사일을 하나 더 쏜다"),
                        T(10, WeaponTrait.Knockback,       "충격파",     "터질 때 주변을 밀어낸다", 6f),
                    }
                },

                // ---------------------------------------------------------- 관통 (Pierce)
                new WeaponDef {
                    kind = WeaponKind.Harpoon, displayName = "견인 작살", tag = WeaponTag.Pierce,
                    pattern = WeaponPattern.Projectile,
                    description = "커서 방향으로 관통 작살을 쏜다",
                    damage = 9f, cooldown = 0.85f, range = 12f, count = 1,
                    projectileSpeed = 34f, pierce = 2,
                    damagePerLevel = 4f, cooldownPerLevel = 0.92f, countEveryLevels = 3,
                    color = C(255,220,140),
                    traits = new[] {
                        T(3,  WeaponTrait.ExtraPierce,     "미늘",      "관통이 늘어난다", 2f),
                        T(5,  WeaponTrait.Pull,            "견인",      "맞힌 것을 우주선 쪽으로 끌어당긴다", 4f),
                        T(7,  WeaponTrait.ExtraProjectile, "삼연발",    "작살을 하나 더 쏜다"),
                        T(10, WeaponTrait.Homing,          "추적 작살", "작살이 목표를 따라간다", 4f),
                    }
                },

                // ---------------------------------------------------------- 전기 (Shock)
                new WeaponDef {
                    kind = WeaponKind.Arc, displayName = "정전기 방출", tag = WeaponTag.Shock,
                    pattern = WeaponPattern.Chain,
                    description = "가까운 것들에게 연쇄 방전을 흘린다",
                    damage = 8f, cooldown = 1.15f, range = 8f, count = 3,
                    damagePerLevel = 3.5f, rangePerLevel = 0.6f, cooldownPerLevel = 0.93f, countEveryLevels = 1,
                    color = C(200,180,255),
                    traits = new[] {
                        T(3,  WeaponTrait.ExtraProjectile, "분기",      "연쇄 대상이 더 늘어난다", 2f),
                        T(5,  WeaponTrait.Slow,            "마비",      "감전된 것이 느려진다", 0.30f),
                        T(7,  WeaponTrait.DoubleTap,       "이중 방전", "한 주기에 두 번 흐른다"),
                        T(10, WeaponTrait.Detonate,        "과전류",    "감전으로 부서진 것이 터진다", 14f),
                    }
                },
            };
        }

    }
}
