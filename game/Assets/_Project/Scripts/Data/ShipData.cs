using System;
using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 우주선 한 척.
    ///
    /// 🔴 **rev.5에서 우주선은 단순한 스탯 묶음이 아니다.**
    ///    무기를 한 판에 둘만 갖는 구조라, 우주선이 정하는 시작 무기가
    ///    **조합의 절반을 미리 결정한다.** 즉 우주선을 고르는 것이
    ///    "어떤 계열로 갈까"를 고르는 것과 같다.
    ///
    ///    그래서 배마다 **시작 무기의 계열이 전부 다르다** — 여섯 계열에 하나씩.
    ///    같은 계열 배가 둘 있으면 그 둘은 사실상 같은 배다.
    ///
    /// 🔴 스탯은 **더하기가 아니라 맞바꾸기**로 준다.
    ///    전부 플러스면 나중에 해금한 배가 항상 좋아지고, 그러면 선택이 사라진다.
    ///    강한 곳이 있으면 약한 곳이 있어야 "이 판엔 이 배"가 성립한다.
    /// </summary>
    [Serializable]
    public class ShipDef
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Tooltip("🔴 이 배가 주는 시작 무기. 조합의 절반이 여기서 정해진다")]
        public WeaponKind startingWeapon = WeaponKind.Discus;

        [Header("스탯 — 맞바꾸기. 전부 플러스면 선택이 사라진다")]
        [Tooltip("최대 연료 배수")]
        public float fuelMul = 1f;
        [Tooltip("추진력 배수")]
        public float thrustMul = 1f;
        [Tooltip("감쇠 배수. 크면 잘 멈추고 작으면 미끄러진다")]
        public float dampingMul = 1f;
        [Tooltip("전 무기 피해 배수")]
        public float powerMul = 1f;
        [Tooltip("전 무기 사거리 배수")]
        public float rangeMul = 1f;
        [Tooltip("전 무기 쿨다운 배수. 작을수록 빠르다")]
        public float cooldownMul = 1f;
        [Tooltip("파편 흡수 반경 배수")]
        public float intakeMul = 1f;
        [Tooltip("크레딧 배수")]
        public float valueMul = 1f;
        [Tooltip("충돌 피해 감소(0~0.5). 더해진다")]
        public float contactResist;

        [Header("해금")]
        [Tooltip("0이면 처음부터 열려 있다")]
        public int costScrap;
        public int costCircuit;
        public int costCore;

        [Header("외형")]
        public Color color = Color.white;
        [Tooltip("몸체 크기 배수 — 큰 배는 눈에 띄지만 더 잘 부딪힌다")]
        public float bodyScale = 1f;

        // 🔴 실루엣. 색만 다르면 여섯 척이 사실상 한 척이다.
        //    형태가 성격을 말해야 한다 — 뾰족하면 빠르고, 넓으면 튼튼해 보인다.
        [Tooltip("앞부분이 얼마나 뾰족한가 (0.05 송곳 ~ 0.55 뭉툭)")]
        [Range(0.05f, 0.55f)] public float nose = 0.14f;
        [Tooltip("뒤가 얼마나 넓은가 (0.6 날렵 ~ 1.1 육중)")]
        [Range(0.55f, 1.15f)] public float tail = 0.95f;
        [Tooltip("중간이 얼마나 부푸는가 (0 없음 ~ 0.5 큰 날개)")]
        [Range(0f, 0.5f)] public float wing;

        public bool FreeFromStart => costScrap <= 0 && costCircuit <= 0 && costCore <= 0;
    }

    /// <summary>
    /// 우주선 3척. 배 하나가 무기 하나를 맡는다 (2026-08-23).
    /// 수치 정본은 에셋이고 여기는 씨앗이다.
    /// </summary>
    public static class ShipDefaults
    {
        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        public static void Fill(GameContent c)
        {
            c.ships = new[]
            {
                // 🔴 첫 배는 반드시 무료여야 한다. 아무것도 없는 상태에서 시작하므로
                new ShipDef {
                    id = "handy", displayName = "정비선 · 핸디",
                    // 🔴 **첫 배는 견인 작살을 준다** (2026-08-23 사장님 지시).
                    //    조준한 쪽으로 곧게 날아가 꽂히는 것이라 **인과가 제일 단순하다** —
                    //    첫 3초에 "내가 뭘 했는지"가 이해되는 무기가 첫 배에 맞다.
                    //    (드릴 → 원반을 거쳐 여기로 왔다)
                    description = "표준형. 곧게 쏴서 꿰뚫는다. 무엇과도 어울린다",
                    startingWeapon = WeaponKind.Harpoon,
                    color = C(160, 210, 240),
                    nose = 0.16f, tail = 0.92f, wing = 0.10f,
                },

                // ⬜ 2026-08-23: **포경선 하푼을 뺐다.**
                //    첫 배(핸디)가 작살을 가져가면서 **같은 무기를 주는 배가 둘**이 됐다.
                //    무기가 셋인데 배가 넷이면 반드시 하나가 겹치는데,
                //    겹치는 쪽이 하푼이었다 — 정체성이 *"작살을 조금 더 잘 쓴다"*뿐이라
                //    **사는 이유가 배가 아니라 숫자**였다.
                //
                //    🔴 배 하나가 무기 하나를 맡는 편이 낫다. 그래야 배를 고르는 것이
                //       곧 **무기를 고르는 것**이 되고, 정비소에서 살 이유가 분명해진다.
                //    (하푼의 사거리·화력 배수가 아까우면 다른 배에 옮겨 붙이면 된다)

                // ⬜ 2026-08-23: **그라인더 · 블래스트를 뺐다** (사장님 지시).
                //    둘 다 `contactResist`(충돌 저항)가 정체성의 절반이었는데,
                //    같은 날 플레이어가 무적이 되면서 **그 절반이 아무 의미도 없어졌다.**
                //    남은 절반(연료 배수·화력 배수)만으로는 다른 배와 구분이 안 된다.
                //    지우기 전 상태는 `rev11-voyage` 브랜치와 이 커밋 직전에 있다.

                new ShipDef {
                    id = "arc", displayName = "전기선 · 아크",
                    description = "쉴 새 없이 흘린다. 쿨다운이 짧지만 한 방이 약하다",
                    startingWeapon = WeaponKind.Arc,
                    cooldownMul = 0.72f, thrustMul = 1.12f,
                    powerMul = 0.85f, fuelMul = 0.90f,
                    color = C(200, 180, 255), bodyScale = 0.95f,
                    nose = 0.10f, tail = 0.66f, wing = 0.28f,     // 날개 달린 화살
                    // 🔴 값을 내렸다 (1800·30·코어4 → 1000·14·코어0).
                    //    하푼(900·12)이 빠지면서 **첫 구매 자리가 비었다** —
                    //    그대로 두면 처음 살 수 있는 배가 두 배로 비싸져서
                    //    "몇 판을 해도 살 게 없다"가 된다. 하푼이 있던 자리를 이어받는다.
                    costScrap = 1000, costCircuit = 14,
                },

                new ShipDef {
                    id = "titan", displayName = "견인선 · 타이탄",
                    // 🔴 겹치는 자리를 **여기로 옮겼다** (2026-08-23).
                    //    첫 배가 작살을 가져가면서 하푼과 겹쳤는데, 그 둘은 **같은 무기를
                    //    다르게 쓰는 배**(표준 / 사거리 특화)라 겹쳐도 뜻이 통한다.
                    //    대신 타이탄이 원반을 맡아 **무기 셋이 전부 어느 배엔가 있게** 했다.
                    //    원반(오가며 두 번 벤다)은 넓게 훑는 견인선과도 맞는다.
                    description = "끌어모아 잘 번다. 흡입이 넓지만 화력이 낮다",
                    startingWeapon = WeaponKind.Discus,
                    intakeMul = 1.50f, valueMul = 1.30f, fuelMul = 1.20f,
                    powerMul = 0.80f, thrustMul = 0.88f,
                    color = C(210, 150, 255), bodyScale = 1.25f,
                    nose = 0.52f, tail = 1.12f, wing = 0.22f,     // 거대한 예인선
                    costScrap = 2400, costCircuit = 40, costCore = 8,
                },
            };
        }
    }
}
