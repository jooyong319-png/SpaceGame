using UnityEngine;

namespace SalvageRun.Data
{
    /// <summary>
    /// 🔴 배와 연료의 기본 수치. 테크트리로 얻는 보너스는 여기에 더해진 사본(RunStats)에 들어간다.
    /// 다른 스크립트에 숫자를 직접 쓰지 않는다.
    ///
    /// ⚠️ 런타임에 이 에셋을 직접 고치면 에디터에서는 그 변경이 파일에 저장된다.
    ///    Bootstrap이 Instantiate로 복사본을 만들어 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "SalvageRun/Run Config")]
    public class RunConfig : ScriptableObject
    {
        [Header("조작 — 누르고 있는 동안만 이동 (2026-08-19 확정)")]
        [Tooltip("커서를 향해 내는 힘. 🔴 실제 순항 속도 = thrustForce ÷ linearDamping " +
                 "(maxSpeed에는 도달하지 않는다). 쓰레기 속도는 이 값을 기준으로 잡혀 있다")]
        public float thrustForce = 42f;
        [Tooltip("커서와 이 거리 안이면 추진하지 않는다")]
        public float deadZone = 0.3f;
        [Tooltip("이 거리부터 최대 출력. 가까우면 부드럽게 감속한다")]
        public float fullThrustDistance = 4.0f;
        [Tooltip("클수록 잘 멈춘다")]
        public float linearDamping = 2.6f;
        public float maxSpeed = 26f;
        public float mass = 1f;

        [Header("대시 (Shift)")]
        [Tooltip("커서 방향으로 순간적으로 밀어내는 힘")]
        public float dashImpulse = 34f;
        public float dashCooldown = 0.6f;
        public float dashFuelCost = 0f;   // 연료는 HP다 — 대시에 비용을 물리지 않는다

        [Header("연료")]
        // 🔴 **180 → 100** (2026-08-26 · Space Rock Breaker 방향).
        //    100 ÷ 2.5 = **40초.** 처음엔 답답한 게 맞다 —
        //    선체 가지를 타면 늘어나고, 그 늘어나는 게 곧 성장의 체감이다.
        public float fuelMax = 100f;

        [Tooltip("🔴 가만히 있어도 나가는 초당 소모(생명유지). 이게 0이면 정지가 최적 전략이 된다")]
        public float idleBurnPerSecond = 1.4f;

        [Tooltip("추진 중 추가로 나가는 초당 소모. 출력에 비례한다")]
        public float thrustBurnPerSecond = 3.2f;

        [Header("무기 — 🔴 뱀서와 다른 지점")]
        [Tooltip("⚠️ 대비책일 뿐이다. 실제 시작 무기는 **우주선**이 정한다 (ShipDef.startingWeapon). " +
                 "우주선 데이터가 없을 때만 이 값이 쓰인다")]
        public WeaponKind startingWeapon = WeaponKind.Harpoon;

        [Tooltip("🔴 한 런에 가질 수 있는 무기 수. **2다.** " +
                 "뱀서는 6개를 넓게 모으지만 이 게임은 둘을 깊게 판다 — " +
                 "그래야 '무엇을 고를까'가 판마다 달라지고 조합이 정체성이 된다")]
        public int maxWeapons = 2;

        [Tooltip("🔴 두 무기가 모두 이 레벨이 되면 히든 조합 능력이 열린다")]
        public int comboLevel = 5;

        [Header("화물 · 모선 (rev.6)")]
        [Tooltip("🔴 적재 한계. 가득 차면 더 못 줍는다 — 돌아가라는 신호")]
        /// <summary>
        /// 🔴 rev.11: 화물칸 상한은 **사실상 없다** (견인 방식).
        ///    무게가 유일한 한계다. 이 값은 UI 비율 표시에만 쓰는 참고치다.
        /// </summary>
        public int cargoMax = 40;

        /// <summary>
        /// 🔴 **무게 반감점.** 이만큼 끌면 속도 저하가 절반쯤 진행된다.
        ///    작을수록 금방 무거워진다 — 이 값 하나가 "얼마나 욕심낼 수 있나"를 정한다.
        /// </summary>
        // ⬜ 점근선 방식에서 쓰던 값. 2026-08-26에 무게를 **삼각수**로 바꾸면서 안 읽는다.
        public float towWeightHalf = 14f;

        /// <summary>
        /// 🔴 **끌 수 있는 개수.** Dome Keeper의 "줄이 6블록 넘으면 끊긴다"를 옮긴 것이다.
        ///    넘으면 맨 앞이 밀려 떨어진다 — 그래서 **무엇을 밟느냐가 곧 무엇을 버리느냐**다.
        ///    정비소에서 늘릴 수 있다 (`TechEffect.TowCapacity`).
        /// </summary>
        public int towCapacity = 6;

        /// <summary>
        /// 🔴 항행 한 구간에 걸리는 시간(초). **이 시간이 곧 거리다.**
        ///    짧으면 방어 국면이 사건이 안 되고, 길면 지루해진다.
        /// </summary>
        public float legSeconds = 45f;

        /// <summary>
        /// 🔴 항행 중 잔해가 기지에 부딪힐 때 연료 손실 배수.
        ///    이 값이 **"못 막으면 얼마나 손해인가"**를 정한다.
        ///    크면 한 번 새는 것도 치명적이고, 작으면 막을 이유가 없어진다.
        /// </summary>
        public float incomingFuelCost = 2.2f;

        [Tooltip("모선 반경 안에 들어오면 자동 입금. 메뉴를 띄우지 않는다")]
        public float baseDockRadius = 3.2f;

        [Tooltip("🔴 가득 실었을 때 붙는 추가 배수. " +
                 "이게 없으면 '조금씩 자주 왕복'이 최적이 되어 저울질이 사라진다")]
        public float fullLoadBonus = 0.6f;

        [Tooltip("🔴 기지 최대 HP. 이게 0이 되면 패배한다")]
        /// <summary>
        /// 🔴 rev.8: 기지 **가동에 걸리는 총 시간**(초). 체력을 대체했다.
        ///    스페이스바를 붙잡고 있어야 차므로, 이 값이 곧 "얼마나 오래 무방비로 서 있어야 하는가"다.
        /// </summary>

        /// <summary>기지 연료 최대치 (= 기지 HP). 0이 되면 패배.</summary>
        /// <summary>
        /// ⬜ **더 이상 안 읽는다** (2026-08-23). 연료가 타이머가 되면서
        ///    행동에 값을 매기는 것을 전부 뺐다 — `idleFuelPerSecond` 하나만 남았다.
        ///    지우지 않은 이유: 되돌릴 여지가 아직 닫히지 않았다.
        /// </summary>
        public float thrustFuelPerSecond = 2.0f;

        /// <summary>
        /// 🔴 **초당 연료 감소 = 이 게임의 타이머.**
        ///
        ///    (2026-08-23 사장님: *"연료는 자동으로 닳게 해줘, 타이머 개념인거지"*)
        ///
        ///    🔴 **1.0 → 2.5로 올렸다** (2026-08-26 사장님: *"연료의 효율을 확 낮춰"*).
        ///
        ///    카드 뽑기가 없어지면서 **한 판이 하는 일이 "재화 벌어 오기" 하나**가 됐다.
        ///    판 안에서는 아무것도 안 변하므로 **길어봐야 같은 30초의 반복**이다 —
        ///    재밌는 것은 전부 정비소에 있고, 판은 거기로 돌아가는 통로다.
        ///    그러면 통로는 **짧고 자주**여야 한다.
        ///
        ///    한 판 기본 길이 = `fuelMax`(180) ÷ 2.5 = **72초.**
        ///    늘리는 방법은 **떨어진 연료통(+55)** 하나뿐이다.
        ///
        ///    ⚠️ 이제 **연료 숫자 = 남은 초가 아니다** (2.5초어치가 1로 표시된다).
        ///       그래서 HUD가 바 옆에 남은 시간을 따로 계산해 쓴다 — 거기만 보면 된다.
        /// </summary>
        public float idleFuelPerSecond = 2.5f;

        public float baseFuelMax = 1000f;

        /// <summary>
        /// 🔴 화물 1개당 기지 연료 회복량.
        ///    맵의 초당 감소량과 함께 **"몇 초마다 몇 개를 가져와야 하는가"**를 정한다 —
        ///    이 두 값이 이 게임의 리듬 전체를 좌우한다.
        /// </summary>
        public float fuelPerCargo = 3.5f;

        // (rev.8에서 지운 repairPerCargo의 Tooltip이 주인 없이 남아 다음 필드에 붙었다 →
        //  CS0579 Duplicate 'Tooltip'. rev.9의 회복은 fuelPerCargo가 맡는다)

        [Tooltip("우주선이 격침된 뒤 다시 나오기까지의 시간(초). " +
                 "🔴 게임이 끝나지 않는다 — 우주선은 소모품이고 기지가 목적이다")]
        public float respawnSeconds = 5f;

        /// <summary>
        /// 🔴 부활 직후 무적 시간. **나오자마자 죽는 고리**를 끊는 값이다.
        ///    짧으면 고리가 다시 생기고, 길면 기지에서 버티는 꼼수가 된다.
        /// </summary>
        public float respawnInvulnSeconds = 3f;

        /// <summary>부활 시 이 반경 안의 쓰레기·로봇을 바깥으로 밀어낸다.</summary>
        public float respawnClearRadius = 12f;

        [Header("아이템 드랍")]
        [Tooltip("🔴 부순 것 하나당 아이템이 나올 확률. **드물어야 사건이 된다** — " +
                 "흔해지면 파편과 구분이 없어지고 그냥 또 하나의 자원이 된다. " +
                 "2026-08-22 플레이 피드백('아이템이 너무 많이 나옴')으로 2% → 0.6%")]
        public float itemDropChance = 0.006f;

        [Tooltip("연료 아이템 회복량")]
        public float fuelPickupAmount = 55f;

        [Header("파편 흡수")]
        [Tooltip("이 반경 안의 파편이 배로 끌려온다. 넓어야 '우수수 빨려온다'가 된다")]
        /// <summary>
        /// 🔴 격침 시 화물 중 **되찾을 수 있는 비율.** 나머지는 진짜로 사라진다.
        ///    1.0이면 죽어도 손해가 없어 무게 저울질이 무의미해지고,
        ///    0이면 많이 싣는 선택 자체가 사라진다. 그 사이 어딘가여야 한다.
        /// </summary>
        public float wreckSpillRatio = 0.6f;

        // ⬜ **자석을 없앴다** (2026-08-26). 읽는 곳이 없다 —
        //    `RunDirector.CollectByTouch`가 `intakeRadius`만 쓴다.
        public float magnetRadius = 2.6f;
        public float magnetPull = 26f;
        [Tooltip("배 중심에서 이 거리 안에 들어오면 흡수된다")]
        // 🔴 **닿는 거리.** 자석이 없어진 뒤로 이 값이 곧 "줍는 반경"이다.
        //    배 반경보다 조금 크게 둔다 — 정확히 겹쳐야만 주워지면 조작이 신경질적이 된다.
        public float intakeRadius = 1.35f;

        [Header("회전 절단날 (기본 무기)")]
        [Tooltip("날 개수. 카드로 늘어난다")]
        public int baseArms = 3;

        [Tooltip("궤도 반경")]
        public float armReach = 3.4f;

        [Tooltip("초당 회전 라디안 — 클수록 빠르게 돈다")]
        public float bladeSpinSpeed = 4.2f;

        [Tooltip("날 하나의 판정 반경")]
        public float bladeRadius = 0.75f;

        [Tooltip("🔴 한 번 닿을 때 주는 피해. 작은 쓰레기는 한 방에 터져야 한다")]
        public float bladeDamage = 6f;

        [Tooltip("(구) 팔 채굴 속도 — 절단날로 바뀌며 안 쓴다")]
        public float armChipPerSecond = 7f;

        // 🔴 채굴 효율 곡선은 '종 모양'이다 (2026-08-20 개정).
        //    가까울수록 무조건 좋게 두면 **벽에 붙는 것이 정답**이 되고,
        //    접촉 피해로 그걸 막으려면 벌금이 비현실적으로 커야 한다.
        //    최적점을 중간에 두면 "적정 간격 유지"가 실제 조작이 된다.
        [Tooltip("최대 효율이 나오는 거리 (팔 사거리 대비 비율)")]
        [Range(0.2f, 0.9f)] public float chipSweetSpot = 0.6f;

        [Tooltip("적정 거리에서의 채굴 배수 (최대)")]
        public float chipPeakMultiplier = 4.5f;

        [Tooltip("딱 붙었을 때의 배수 — 팔이 접혀 제대로 못 쓴다")]
        public float chipContactMultiplier = 0.5f;

        [Tooltip("팔 끝에서의 배수")]
        public float chipFarMultiplier = 0.3f;

        [Tooltip("거리 보정을 끄면 쓰는 고정 배수 (F3로 전환, A/B 비교용)")]
        public float chipFlatMultiplier = 2.0f;

        [Header("커서 조준")]
        [Tooltip("🔴 팔은 커서 방향 이 각도(도) 안에 있는 것만 잡는다. 좁을수록 조준이 선명하다")]
        [Range(20f, 180f)] public float aimConeDegrees = 75f;

        [Tooltip("콘 안에서 거리와 각도 중 무엇을 우선할지 (0=거리만, 1=각도만)")]
        [Range(0f, 1f)] public float aimBias = 0.5f;
    }
}
