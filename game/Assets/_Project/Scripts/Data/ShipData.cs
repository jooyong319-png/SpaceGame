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
        public WeaponKind startingWeapon = WeaponKind.Blade;

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
    /// 우주선 6척. 계열마다 한 척씩.
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
                    // 🔴 rev.10: 첫 배는 **드릴**을 준다. 이 게임의 기본 동사가 채굴이므로
                    //    첫 판에서 반드시 만나야 한다 — 못 만나면 무슨 게임인지 모른 채 끝난다
                    description = "표준형. 드릴로 캐낸다. 무엇과도 어울린다",
                    startingWeapon = WeaponKind.Drill,
                    color = C(160, 210, 240),
                    nose = 0.16f, tail = 0.92f, wing = 0.10f,
                },

                new ShipDef {
                    id = "harpoon", displayName = "포경선 · 하푼",
                    description = "멀리서 꿰뚫는다. 사거리가 길지만 선체가 얇다",
                    startingWeapon = WeaponKind.Harpoon,
                    rangeMul = 1.25f, powerMul = 1.10f,
                    fuelMul = 0.80f, dampingMul = 0.85f,
                    color = C(255, 210, 130), bodyScale = 0.92f,
                    nose = 0.06f, tail = 0.70f, wing = 0f,        // 송곳형 — 멀리 찌른다
                    costScrap = 900, costCircuit = 12,
                },

                new ShipDef {
                    id = "grinder", displayName = "굴착선 · 그라인더",
                    description = "붙어서 갈아버린다. 튼튼하지만 굼뜨다",
                    startingWeapon = WeaponKind.Vortex,
                    fuelMul = 1.45f, contactResist = 0.18f,
                    thrustMul = 0.85f, rangeMul = 0.90f,
                    color = C(140, 200, 255), bodyScale = 1.18f,
                    nose = 0.42f, tail = 1.10f, wing = 0.05f,     // 뭉툭한 굴착기
                    costScrap = 1200, costCircuit = 18,
                },

                new ShipDef {
                    id = "blaster", displayName = "폭파선 · 블래스트",
                    description = "크게 터뜨린다. 화력은 최고지만 맞으면 아프다",
                    startingWeapon = WeaponKind.Bomb,
                    powerMul = 1.35f,
                    fuelMul = 0.75f, contactResist = -0.12f,
                    color = C(255, 150, 90), bodyScale = 1.05f,
                    nose = 0.30f, tail = 0.85f, wing = 0.40f,     // 양쪽에 탄창을 단 형태
                    costScrap = 1600, costCircuit = 26, costCore = 3,
                },

                new ShipDef {
                    id = "arc", displayName = "전기선 · 아크",
                    description = "쉴 새 없이 흘린다. 쿨다운이 짧지만 한 방이 약하다",
                    startingWeapon = WeaponKind.Arc,
                    cooldownMul = 0.72f, thrustMul = 1.12f,
                    powerMul = 0.85f, fuelMul = 0.90f,
                    color = C(200, 180, 255), bodyScale = 0.95f,
                    nose = 0.10f, tail = 0.66f, wing = 0.28f,     // 날개 달린 화살
                    costScrap = 1800, costCircuit = 30, costCore = 4,
                },

                new ShipDef {
                    id = "titan", displayName = "견인선 · 타이탄",
                    description = "끌어모아 한 번에 처리한다. 잘 벌지만 화력이 낮다",
                    startingWeapon = WeaponKind.Well,
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
