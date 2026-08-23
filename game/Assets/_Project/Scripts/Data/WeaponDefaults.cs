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
                // ---------------------------------------------------------- 채굴 (rev.10)
                //  🔴 **이 게임의 새 기본 동사.** 다른 무기는 '쏜다'지만 이건 '캔다'.
                //     커서 쪽 쓰레기 하나에 붙어 갈아내고, **캐는 동안 배가 묶인다.**
                //
                //     피해가 세고 쿨다운이 없다 — 대신 **한 번에 하나**만 상대한다.
                //     떼로 몰리면 아무것도 못 한다. 그래서 두 번째 무기가 필요해진다.
                new WeaponDef {
                    kind = WeaponKind.Drill, displayName = "채굴 드릴", tag = WeaponTag.Cut,
                    pattern = WeaponPattern.Drill,
                    description = "커서 쪽 쓰레기에 붙어 갈아낸다. 캐는 동안 배가 묶인다",
                    damage = 34f, cooldown = 0f, range = 3.0f, count = 1,
                    damagePerLevel = 11f, rangePerLevel = 0.16f, countEveryLevels = 0,
                    color = C(255,200,110),
                    traits = new[] {
                        T(3,  WeaponTrait.Shred,     "경화 비트", "내구가 많이 남은 것일수록 더 깎는다", 0.45f),
                        T(5,  WeaponTrait.WideArc,   "확장 헤드", "드릴이 닿는 범위가 넓어진다", 0.40f),
                        T(7,  WeaponTrait.Chain,     "충격 전달", "갈아내는 대상 주변으로 진동이 퍼진다", 1f),
                        T(10, WeaponTrait.Detonate,  "발파 모드", "다 캔 것이 터지며 주변을 부순다", 26f),
                    }
                },

                // ---------------------------------------------------------- 절삭 (Cut)
                new WeaponDef {
                    kind = WeaponKind.Blade, displayName = "회전 절단날", tag = WeaponTag.Cut,
                    pattern = WeaponPattern.Orbit,
                    description = "우주선 주위를 도는 날. 닿는 족족 간다",
                    damage = 7f, cooldown = 0f, range = 3.4f, count = 3,
                    damagePerLevel = 1.6f, rangePerLevel = 0.22f, countEveryLevels = 2,
                    color = C(150,230,255),
                    traits = new[] {
                        T(3,  WeaponTrait.WideArc,   "확장 궤도", "날의 타격 범위가 넓어진다", 0.35f),
                        T(5,  WeaponTrait.Shred,     "톱니",      "내구가 많이 남은 것일수록 더 깎는다", 0.30f),
                        T(7,  WeaponTrait.OrbitGun,  "포탑 궤도", "날이 돌면서 바깥으로 사격한다", 1f),
                        T(10, WeaponTrait.Detonate,  "과열 절단", "이 무기로 부순 것이 작게 터진다", 12f),
                    }
                },

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

                new WeaponDef {
                    kind = WeaponKind.Laser, displayName = "절단 레이저", tag = WeaponTag.Pierce,
                    pattern = WeaponPattern.Beam,
                    description = "커서 방향으로 뻗는 지속 광선. 선 위의 모든 것을 태운다",
                    damage = 22f, cooldown = 0f, range = 11f, count = 1,
                    damagePerLevel = 9f, rangePerLevel = 0.7f, countEveryLevels = 0,
                    color = C(255,120,140),
                    traits = new[] {
                        T(3,  WeaponTrait.WideArc,         "확산 렌즈", "광선이 두꺼워진다", 0.5f),
                        T(5,  WeaponTrait.Slow,            "냉각 절단", "맞는 동안 대상이 느려진다", 0.35f),
                        T(7,  WeaponTrait.Overcharge,      "과충전",    "사거리 끝일수록 피해가 크다", 0.6f),
                        T(10, WeaponTrait.ExtraProjectile, "분광",      "반대 방향으로도 쏜다"),
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

                new WeaponDef {
                    kind = WeaponKind.Nova, displayName = "충격파", tag = WeaponTag.Shock,
                    pattern = WeaponPattern.Nova,
                    description = "우주선을 중심으로 파동이 퍼진다. 붙은 것들을 떼어낸다",
                    damage = 18f, cooldown = 2.4f, range = 5.5f, count = 1,
                    damagePerLevel = 7f, rangePerLevel = 0.45f, cooldownPerLevel = 0.92f, countEveryLevels = 6,
                    color = C(150,220,255),
                    traits = new[] {
                        T(3,  WeaponTrait.Knockback, "반발",      "맞은 것이 밀려난다", 7f),
                        T(5,  WeaponTrait.WideArc,   "광역 파동", "파동이 훨씬 넓어진다", 0.45f),
                        T(7,  WeaponTrait.DoubleTap, "이중 파동", "파동이 두 번 퍼진다"),
                        T(10, WeaponTrait.Chain,     "전이 파동", "맞은 것에서 방전이 튄다"),
                    }
                },

                // ---------------------------------------------------------- 폭발 (Blast)
                new WeaponDef {
                    kind = WeaponKind.Bomb, displayName = "압축 폭탄", tag = WeaponTag.Blast,
                    pattern = WeaponPattern.PeriodicAoe,
                    description = "주기적으로 근처에 광역 폭발을 던진다",
                    // 🔴 폭탄은 **즉발**이라 쿨다운 동안 아무 일도 안 일어난다.
                    //    장판·궤도는 상시로 훑는데 폭탄만 비어 있어서, 폭탄 계열 조합이
                    //    파편 수 꼴찌였다 (2026-08-22 시뮬: 865 vs 2795).
                    //    피해를 올리는 대신 **더 자주 터지게** 한다 — 부족한 건 화력이 아니라 상시성이었다.
                    damage = 14f, cooldown = 1.5f, range = 3.2f, count = 1,
                    damagePerLevel = 6f, rangePerLevel = 0.35f, cooldownPerLevel = 0.93f, countEveryLevels = 3,
                    color = C(255,170,110),
                    traits = new[] {
                        T(3,  WeaponTrait.WideArc,         "고폭",      "폭발 반경이 커진다", 0.35f),
                        T(5,  WeaponTrait.ExtraProjectile, "다탄두",    "폭탄을 하나 더 던진다"),
                        T(7,  WeaponTrait.Knockback,       "충격",      "폭발이 밀어낸다", 6f),
                        T(10, WeaponTrait.Detonate,        "연쇄 기폭", "폭발로 부서진 것이 또 터진다", 18f),
                    }
                },

                new WeaponDef {
                    kind = WeaponKind.Mine, displayName = "자기 지뢰", tag = WeaponTag.Blast,
                    pattern = WeaponPattern.Mine,
                    description = "지나온 자리에 지뢰를 둔다. 다가온 것이 밟는다",
                    // 🔴 지뢰는 깔아 두는 무기라 **개수가 곧 상시성**이다. 더 자주, 더 많이.
                    damage = 24f, cooldown = 1.1f, range = 2.8f, count = 1,
                    damagePerLevel = 9f, rangePerLevel = 0.25f, cooldownPerLevel = 0.94f, countEveryLevels = 3,
                    color = C(255,140,90),
                    traits = new[] {
                        T(3,  WeaponTrait.Pull,            "자기 유인", "지뢰가 주변을 끌어당긴다", 3f),
                        T(5,  WeaponTrait.ExtraProjectile, "이중 부설", "지뢰를 하나 더 둔다"),
                        T(7,  WeaponTrait.WideArc,         "광역 기폭", "폭발 반경이 커진다", 0.5f),
                        T(10, WeaponTrait.Chain,           "감응 기폭", "터질 때 방전이 퍼진다"),
                    }
                },

                // ---------------------------------------------------------- 장판 (Field)
                new WeaponDef {
                    kind = WeaponKind.Vortex, displayName = "흡입 소용돌이", tag = WeaponTag.Field,
                    pattern = WeaponPattern.Aura,
                    description = "우주선 주위 지속 장판. 가까운 것을 계속 갈아낸다",
                    damage = 7f, cooldown = 0f, range = 2.8f, count = 1,
                    damagePerLevel = 3.5f, rangePerLevel = 0.45f, countEveryLevels = 0,
                    color = C(140,200,255),
                    traits = new[] {
                        T(3,  WeaponTrait.Pull,      "흡인",          "장판이 대상을 끌어당긴다", 3f),
                        T(5,  WeaponTrait.Slow,      "점성",          "장판 안의 것이 느려진다", 0.35f),
                        T(7,  WeaponTrait.Shred,     "연마",          "내구가 많이 남은 것일수록 더 깎인다", 0.5f),
                        T(10, WeaponTrait.LifeSteal, "정제 소용돌이", "장판으로 부술 때 연료를 회수한다", 1.5f),
                    }
                },

                new WeaponDef {
                    kind = WeaponKind.Barrier, displayName = "플라즈마 방벽", tag = WeaponTag.Field,
                    pattern = WeaponPattern.Orbit,
                    description = "우주선을 감싸는 플라즈마 고리. 닿는 것을 태우고 밀어낸다",
                    damage = 11f, cooldown = 0f, range = 2.2f, count = 4,
                    damagePerLevel = 4f, rangePerLevel = 0.14f, countEveryLevels = 3,
                    color = C(120,255,230),
                    traits = new[] {
                        T(3,  WeaponTrait.Knockback, "반발장",    "닿은 것을 밀어낸다", 5f),
                        T(5,  WeaponTrait.WideArc,   "확장 방벽", "고리가 두꺼워진다", 0.5f),
                        T(7,  WeaponTrait.Slow,      "감속장",    "닿은 것이 느려진다", 0.4f),
                        T(10, WeaponTrait.Chain,     "방전 방벽", "닿은 것에서 방전이 튄다"),
                    }
                },

                // ---------------------------------------------------------- 중력 (Gravity)
                new WeaponDef {
                    kind = WeaponKind.Well, displayName = "중력 우물", tag = WeaponTag.Gravity,
                    pattern = WeaponPattern.Well,
                    description = "한 점으로 빨아들인다. 모인 것은 함께 갈린다",
                    // 🔴 쿨다운 3.4초에 지속 2.6초라 **24%는 꺼져 있었다.**
                    //    지속시간에 맞춰 끊기지 않게 하고 피해도 올렸다 (2026-08-22 시뮬: 벌이 꼴찌)
                    damage = 14f, cooldown = 2.5f, range = 6.5f, count = 1,
                    damagePerLevel = 6f, rangePerLevel = 0.5f, cooldownPerLevel = 0.93f, countEveryLevels = 6,
                    color = C(200,140,255),
                    traits = new[] {
                        T(3,  WeaponTrait.Pull,            "심화",      "끌어당기는 힘이 세진다", 6f),
                        T(5,  WeaponTrait.Slow,            "시간 지연", "우물 안이 느려진다", 0.45f),
                        T(7,  WeaponTrait.ExtraProjectile, "쌍성",      "우물을 하나 더 만든다"),
                        T(10, WeaponTrait.Detonate,        "붕괴",      "우물이 사라질 때 터진다", 40f),
                    }
                },

                new WeaponDef {
                    kind = WeaponKind.Drone, displayName = "견인 드론", tag = WeaponTag.Gravity,
                    pattern = WeaponPattern.Companion,
                    description = "따라다니며 스스로 일한다. 가까운 것을 물어 끌어온다",
                    // 🔴 사격이 느리고 약해서 중력 계열 전체가 벌이 꼴찌였다 (2026-08-22 시뮬)
                    damage = 15f, cooldown = 0.85f, range = 8f, count = 1,
                    projectileSpeed = 30f, pierce = 2,
                    damagePerLevel = 6f, rangePerLevel = 0.4f, cooldownPerLevel = 0.94f, countEveryLevels = 3,
                    color = C(160,220,200),
                    traits = new[] {
                        T(3,  WeaponTrait.Pull,            "견인 빔",   "드론이 대상을 끌어온다", 4f),
                        T(5,  WeaponTrait.ExtraProjectile, "편대",      "드론이 하나 더 붙는다"),
                        T(7,  WeaponTrait.Magnetize,       "회수 드론", "부순 자리의 파편을 즉시 회수한다"),
                        T(10, WeaponTrait.Homing,          "정밀 조준", "드론의 사격이 목표를 따라간다", 5f),
                    }
                },
            };
        }

        // ==============================================================================
        //  조합 21가지 (태그 6종 → 같은 계열 6 + 다른 계열 15)
        // ==============================================================================

        static ComboDef K(WeaponTag a, WeaponTag b, ComboEffect e, string title, string desc, Color col)
            => new ComboDef { a = a, b = b, effect = e, title = title, description = desc, color = col };

        public static void FillCombos(GameContent c)
        {
            var cut = WeaponTag.Cut;   var pie = WeaponTag.Pierce; var sho = WeaponTag.Shock;
            var bla = WeaponTag.Blast; var fie = WeaponTag.Field;  var gra = WeaponTag.Gravity;

            c.combos = new[]
            {
                // ---- 같은 계열 = 특화. 🔴 "같은 걸 두 개 골랐다"가 실수로 느껴지면 안 된다 ----
                K(cut, cut, ComboEffect.CutCut,       "난도질",
                  "절삭 피해가 크게 오르고, 같은 대상을 벨수록 더 깊게 들어간다", C(150,230,255)),
                K(pie, pie, ComboEffect.PierceP,      "관통 정렬",
                  "관통이 대폭 늘고 일직선상의 모든 것을 꿰뚫는다", C(255,220,140)),
                K(sho, sho, ComboEffect.ShockShock,   "과부하",
                  "연쇄가 두 배로 뻗고 감전이 오래간다", C(200,180,255)),
                K(bla, bla, ComboEffect.BlastBlast,   "연쇄 폭발",
                  "모든 폭발이 한 박자 뒤에 한 번 더 터진다", C(255,170,110)),
                K(fie, fie, ComboEffect.FieldField,   "영구 장판",
                  "장판이 훨씬 넓어지고 지나간 자리에 잔류한다", C(140,200,255)),
                K(gra, gra, ComboEffect.GravGrav,     "사건의 지평",
                  "끌어당김이 압도적으로 강해지고 중심에서 계속 갈린다", C(200,140,255)),

                // ---- 다른 계열 ----
                K(cut, pie, ComboEffect.CutPierce,    "절개",
                  "관통이 지나간 자리에 절삭 흔적이 남아 계속 깎는다", C(190,240,200)),
                K(cut, sho, ComboEffect.CutShock,     "전도 날",
                  "절삭이 닿은 곳에서 방전이 튄다", C(190,200,255)),
                K(cut, bla, ComboEffect.CutBlast,     "파편 폭풍",
                  "절삭이 지나간 자리가 잇따라 터진다", C(255,190,140)),
                K(cut, fie, ComboEffect.CutField,     "분쇄 장판",
                  "절삭 궤도가 장판 끝까지 넓어진다", C(150,220,255)),
                K(cut, gra, ComboEffect.CutGravity,   "견인 분쇄",
                  "끌려온 것이 절삭의 밥이 된다", C(200,180,255)),

                K(pie, sho, ComboEffect.PierceShock,  "번개 관통",
                  "관통한 대상마다 방전이 튄다", C(220,200,255)),
                K(pie, bla, ComboEffect.PierceBlast,  "작렬 관통",
                  "관통이 멈춘 자리에서 폭발한다", C(255,200,120)),
                K(pie, fie, ComboEffect.PierceField,  "회수 관통",
                  "관통이 멈춘 자리에 장판이 남는다", C(140,230,230)),
                K(pie, gra, ComboEffect.PierceGravity,"견인 관통",
                  "관통이 맞힌 것을 우주선 쪽으로 끌어당긴다", C(220,190,255)),

                K(sho, bla, ComboEffect.ShockBlast,   "감전 폭탄",
                  "폭발한 자리에서 방전이 퍼진다", C(255,160,200)),
                K(sho, fie, ComboEffect.ShockField,   "대전 장판",
                  "장판 피해가 크게 오르고 밖으로 방전한다", C(180,220,255)),
                K(sho, gra, ComboEffect.ShockGravity, "자기 폭풍",
                  "모여 있는 것들이 서로 감전된다", C(210,170,255)),

                K(bla, fie, ComboEffect.BlastField,   "압축 붕괴",
                  "장판이 주기적으로 스스로 터진다", C(255,170,150)),
                K(bla, gra, ComboEffect.BlastGravity, "중력 폭탄",
                  "터지기 직전에 빨아들인 뒤 터진다", C(255,150,220)),

                K(fie, gra, ComboEffect.FieldGravity, "포획장",
                  "장판이 대상을 붙잡아 둔다", C(170,200,255)),
            };
        }
    }
}
