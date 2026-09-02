using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 🔴 배와 연료의 기본 수치. 테크트리로 얻는 보너스는 여기에 더해진 사본(RunStats)에 들어간다.
    /// 다른 스크립트에 숫자를 직접 쓰지 않는다.
    ///
    /// ⚠️ 런타임에 이 에셋을 직접 고치면 에디터에서는 그 변경이 파일에 저장된다.
    ///    Bootstrap이 Instantiate로 복사본을 만들어 쓴다.
    ///
    /// ⬜ **2026-09-02에 죽은 필드 27개를 뺐다** — 모선·부활·자석·회전날·커서조준 등
    ///    rev.6~9의 잔재로, 전부 **읽는 곳이 하나도 없었다.**
    ///    (지운 것들은 git 이력에 있다. 옛 주석에 적혀 있던 CS0579 사고 —
    ///     주인 잃은 `[Tooltip]`이 다음 필드에 붙는 것 — 도 여기서 같이 정리했다)
    /// </summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "SalvageRun/Run Config")]
    public class RunConfig : ScriptableObject
    {
        [Header("조작 — 키보드 전용 (2026-08-27)")]
        [Tooltip("미는 힘. 🔴 실제 순항 속도 = thrustForce ÷ linearDamping " +
                 "(maxSpeed에는 도달하지 않는다). 쓰레기 속도는 이 값을 기준으로 잡혀 있다")]
        public float thrustForce = 42f;
        [Tooltip("이 거리 안이면 추진하지 않는다")]
        public float deadZone = 0.3f;
        [Tooltip("이 거리부터 최대 출력")]
        public float fullThrustDistance = 4.0f;
        [Tooltip("클수록 잘 멈춘다")]
        public float linearDamping = 2.6f;
        public float maxSpeed = 26f;
        public float mass = 1f;

        [Header("대시 (Shift)")]
        [Tooltip("순간적으로 밀어내는 힘")]
        public float dashImpulse = 34f;
        public float dashCooldown = 0.6f;

        [Header("연료 — 이 게임의 타이머")]
        // 🔴 **180 → 100** (2026-08-26 · Space Rock Breaker 방향).
        //    100 ÷ 2.5 = **40초.** 처음엔 답답한 게 맞다 —
        //    선체 가지를 타면 늘어나고, 그 늘어나는 게 곧 성장의 체감이다.
        public float fuelMax = 100f;

        /// <summary>
        /// 🔴 **초당 연료 감소 = 이 게임의 타이머.**
        ///    (2026-08-23 사장님: *"연료는 자동으로 닳게 해줘, 타이머 개념인거지"*)
        ///
        ///    🔴 **1.0 → 2.5로 올렸다** (2026-08-26 사장님: *"연료의 효율을 확 낮춰"*).
        ///    판 안에서는 아무것도 안 변하므로 **길어봐야 같은 30초의 반복**이다 —
        ///    재밌는 것은 전부 정비소에 있고, 판은 거기로 돌아가는 통로다.
        ///    그러면 통로는 **짧고 자주**여야 한다.
        ///
        ///    ⚠️ **연료 숫자 = 남은 초가 아니다** (2.5초어치가 1로 표시된다).
        ///       HUD가 바 옆에 남은 시간을 따로 계산해 쓴다 — 거기만 보면 된다.
        /// </summary>
        public float idleFuelPerSecond = 2.5f;

        /// <summary>
        /// 🔴 **보스 탄 한 대에 닳는 연료** (2026-08-26).
        ///    죽지는 않는다 — 맞으면 **이번 판이 짧아질** 뿐이다.
        ///    그게 인크리멘탈에 맞는 벌이다: 손해는 시간이지 진행이 아니다.
        /// </summary>
        public float bossShotFuelCost = 12f;

        [Header("무기")]
        [Tooltip("아무 무기도 안 열렸을 때 주는 대비책. 무기는 전부 테크트리가 정한다")]
        public WeaponKind startingWeapon = WeaponKind.Harpoon;

        [Header("견인")]
        /// <summary>
        /// 🔴 **끌 수 있는 개수.** Dome Keeper의 "줄이 6블록 넘으면 끊긴다"를 옮긴 것이다.
        ///    넘으면 맨 앞이 밀려 떨어진다 — 그래서 **무엇을 밟느냐가 곧 무엇을 버리느냐**다.
        ///    정비소에서 늘릴 수 있다 (`TechEffect.TowCapacity`).
        /// </summary>
        public int towCapacity = 6;

        [Header("줍기")]
        [Tooltip("🔴 닿는 거리. 자석이 없으므로 이 값이 곧 '줍는 반경'이다. " +
                 "배 반경보다 조금 크게 둔다 — 정확히 겹쳐야만 주워지면 조작이 신경질적이 된다")]
        public float intakeRadius = 1.35f;

        [Header("아이템 드랍")]
        [Tooltip("🔴 부순 것 하나당 아이템이 나올 확률. **드물어야 사건이 된다** — " +
                 "흔해지면 파편과 구분이 없어지고 그냥 또 하나의 자원이 된다. " +
                 "2026-08-22 플레이 피드백('아이템이 너무 많이 나옴')으로 2% → 0.6%")]
        public float itemDropChance = 0.006f;

        [Tooltip("연료 아이템 회복량")]
        public float fuelPickupAmount = 55f;
    }
}
