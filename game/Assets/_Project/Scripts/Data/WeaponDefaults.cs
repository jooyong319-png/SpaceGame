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

        // ==============================================================================
        //  조합표 (태그 6종 → 같은 계열 6 + 다른 계열 15 = 21줄)
        //
        //  ⚠️ **2026-08-23부터 실제로 뜨는 건 6가지다.**
        //     무기가 셋(원반=절삭 · 작살=관통 · 방전=전기)으로 줄면서
        //     쓰는 태그도 셋이 됐다 — 같은 계열 3 + 다른 계열 3 = **6줄**만 살아 있다.
        //     장판·중력·폭발이 낀 15줄은 **짝지을 무기가 없어 영원히 안 뜬다.**
        //
        //     표를 지우지 않은 이유: 태그 기준이라 **무기를 다시 늘리면 그대로 살아난다.**
        //     (그게 애초에 무기 쌍이 아니라 태그 쌍으로 짠 이유다)
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
