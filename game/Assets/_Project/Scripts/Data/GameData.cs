using System;
using UnityEngine;

namespace SalvageRun.Data
{
    // ==================================================================================
    //  쓰레기
    // ==================================================================================

    /// <summary>
    /// 🔴 이동 패턴. 숫자만 다르면 종류가 아무리 많아도 한 종류로 느껴진다 —
    ///    적이 다르게 느껴지는 건 속도·HP가 아니라 **어떻게 움직이는가**다.
    /// </summary>
    /// <summary>
    /// 🔴 **쓰레기가 무엇으로 보이는가** (2026-08-26 사장님 지시:
    ///    *"쓰레기라는 게 이런 거거든? 위성 · 작은 우주선 · 전함 · 거대 우주선 · 외계 우주선"*).
    ///
    ///    전에는 전부 `PixelArt.Debris`(찌그러진 사각형)라
    ///    **무엇을 부수고 있는지 안 읽혔다.** 실루엣이 갈려야 "저건 전함이다"가 보자마자 온다.
    ///
    ///    ⚠️ 크기·HP와 **따로** 둔 이유: 같은 실루엣으로 여러 크기를 쓸 수 있어야
    ///       종류를 늘릴 때 그림을 매번 새로 안 그린다.
    /// </summary>
    public enum JunkShape
    {
        Satellite = 0,   // 위성 — 십자 (몸통 + 태양광 날개)
        Vessel,          // 작은 우주선 — 화살
        Warship,         // 전함 — 긴 상자 + 포탑
        Hulk,            // 거대 우주선 — 덩어리 + 블록
        Debris           // 그 밖(위험물·로봇) — 찌그러진 잔해
    }

    public enum MoveKind
    {
        // ⚠️ 아래 다섯은 **쓰레기**의 흐름이다. 이름이 "쫓는다"처럼 들리지만
        //    2026-08-23부터 **아무것도 안 쫓는다** — `JunkPiece.ApplyMovePattern` 참고.
        //    이름을 안 바꾼 이유: 쓰레기 데이터 20여 줄에 박혀 있어서
        //    바꾸면 그 줄을 전부 건드려야 하는데, 위험만 늘고 얻는 게 없다.
        Chase = 0,   // 아주 천천히 방향이 휜다 (조각마다 다른 위상)
        Drift,       // 직진만
        Zigzag,      // 흘러가며 좌우로 사행
        Charger,     // 굴러가다 가끔 튕기듯 가속
        Orbiter,     // 완만하게 휘어 흐른다 — 궤도에 실린 잔해처럼

        /// <summary>
        /// 🔴 **파손 로봇** (rev.9). 쓰레기 무리 근처에 있다가 **플레이어를 쫓는다.**
        ///    (2026-08-21: *"쓰레기 무리 주변에 파손된 로봇 같은 게 있어서 플레이어를 공격"*)
        ///
        ///    쓰레기가 밭이 되면서 **위협이 통째로 사라졌다.** 로봇이 그 자리를 메운다.
        ///    쓰레기와 위협을 **분리한 것**이 핵심이다 —
        ///    이제 "캐고 싶은 것"과 "무서운 것"이 다른 물건이라,
        ///    좋은 밭일수록 위험하다는 관계를 수치가 아니라 **배치**로 만들 수 있다.
        /// </summary>
        Hunter,

        /// <summary>
        /// 🔴 **저격기.** 거리를 유지하며 **쏜다.** 붙으면 물러난다.
        ///    (2026-08-23 사장님: *"적은 왜 돌진..만 있어?"*)
        ///
        ///    돌진만 있으면 대응이 하나뿐이다 — 피하거나 죽이거나.
        ///    거리를 두는 적이 있으면 **"쫓아갈까, 무시하고 캘까"**가 생긴다.
        /// </summary>
        Sniper,

        /// <summary>
        /// 🔴 **매복기.** 평소엔 **쓰레기인 척 멈춰 있다가**, 가까이 오면 달려든다.
        ///    밭에 들어갈 때마다 *"저게 진짜 쓰레기인가"*를 한 번 보게 만든다.
        /// </summary>
        Ambusher,

        /// <summary>
        /// 🔴 **선회기.** 배 주위를 **맴돌며 조인다.** 정면으로 안 오므로 조준이 까다롭다.
        ///    드릴처럼 한 놈만 무는 무기에 특히 성가시다 — 그래서 호위 무기를 고를 이유가 된다.
        /// </summary>
        Circler
    }

    /// <summary>
    /// 쓰레기 한 종류. 값(value)만 다르면 종류가 아무리 많아도 1종이다.
    /// **이동 패턴 · HP · 크기 · 특수 행동**이 서로 어긋나야 종류가 된다.
    /// </summary>
    [Serializable]
    public class JunkType
    {
        public string displayName = "고철";
        public int tier;
        public int value = 10;
        public float size = 0.6f;

        [Tooltip("이동 속도. 🔴 배의 실제 순항 속도는 약 20(thrustForce/linearDamping)이다. " +
                 "다수를 차지하는 종류는 그 절반(≈10) 아래여야 피하면서 싸울 수 있다")]
        public float driftSpeed = 7f;

        [Tooltip("🔴 배를 쫓는 정도(초당 방향 보정). 0이면 직진만 하고 빗나간다")]
        public float homing = 1.2f;

        [Tooltip("🔴 이동 패턴")]
        public MoveKind move = MoveKind.Chase;

        [Tooltip("패턴 세기 — 사행 진폭 / 돌진 주기 / 선회 속도")]
        public float movePower = 1f;

        [Tooltip("부서지면 이 종류로 N개 분열한다. 비우면 분열 없음")]
        public string splitInto;
        public int splitCount = 2;

        [Tooltip("한 번에 이만큼 무리지어 나온다")]
        public int groupSize = 1;

        [Tooltip("🔴 출현 가중치 = **화면에 뜨는 개체 수의 비율**. " +
                 "무리(groupSize)로 나오는 종류는 한 번 뽑힐 때 여러 마리가 나오므로 " +
                 "스폰 확률은 groupSize로 나눠 보정한다 — 여기 적는 값은 항상 '개체 수 기준'이다")]
        public int spawnWeight = 10;

        [Tooltip("🔴 깎아내야 하는 총량. 0이 되면 파편이 나온다")]
        public float hp = 10f;

        [Tooltip("🔴 배가 닿았을 때 잃는 연료")]
        public float contactDamage = 4f;

        [Tooltip("부서질 때 나오는 파편 수")]
        public int fragments = 2;

        [Tooltip("파편 하나의 가치. 비우면 value/fragments로 자동 계산")]
        public int fragmentValue;

        [Tooltip("절단 레이저 없이는 수집 불가. 대신 값이 크다")]
        public bool requiresCutter;

        [Tooltip("주우면 돌려받는 연료")]
        public float fuelBonus;

        [Tooltip("🔴 먹으면 안 되는 쓰레기. 크레딧이 안 나오고 연료를 깎는다")]
        public bool isHazard;

        /// <summary>
        /// 🔴 **계류 장치** (rev.10 최종 지역). 거대 잔해가 기지에 박은 닻이다.
        ///
        ///    살아 있는 동안 **기지 연료 감소를 가속**하고, 넷을 다 부수면 **승리**한다.
        ///    맵 곳곳에 흩어져 있으므로 마지막 판도 **나가서 캐고 돌아오는** 이 게임의
        ///    본래 리듬 그대로다 — 기지 앞 한 자리에서 끝나는 보스전이 아니다.
        /// </summary>
        public bool isAnchor;

        /// <summary>이 쓰레기가 어떤 실루엣으로 보이는가. `StageField`가 스프라이트를 고른다.</summary>
        public JunkShape shape = JunkShape.Debris;

        /// <summary>
        /// 🔴 **이건 밭이 아니라 적이다.**
        ///
        ///    스폰 풀 분리 · 채도 · 컬링 · 표적 선택이 전부 이 판정을 쓴다.
        ///    예전엔 `move == MoveKind.Hunter`로 일일이 비교했는데,
        ///    2026-08-23에 행동을 넷으로 늘리면서 **비교하던 곳마다 빠뜨릴 뻔했다.**
        ///    한 곳에서 정의하면 행동을 더 늘려도 여기만 고치면 된다.
        /// </summary>
        public bool IsRobot =>
            move == MoveKind.Hunter || move == MoveKind.Sniper ||
            move == MoveKind.Ambusher || move == MoveKind.Circler;

        [Tooltip("위험물을 먹었을 때 깎이는 연료")]
        public float fuelPenalty;

        public Color color = Color.gray;
    }

    // ==================================================================================
    //  지역(스테이지)
    // ==================================================================================

    [Serializable]
    public class StageDef
    {
        public string displayName = "기지 궤도";
        [TextArea] public string description;

        [Tooltip("난이도 표시용 1~5")]
        public int rank = 1;

        // ==============================================================================
        //  🔴 **해금은 재화로 산다** (2026-08-26)
        //
        //     전에는 앞 구역의 **보스를 잡아야** 다음이 열렸다. Space Rock Breaker 쪽으로
        //     방향을 잡으면서 그 구조를 버렸다 — 인크리멘탈은 **이기는 게 아니라 모으는** 게임이다.
        //
        //     그리고 실제로 막혀 있었다: 보스는 300초에 나오는데 연료는 72초라
        //     **2번 구역이 영원히 안 열렸다.** 벽을 재화로 바꾸면 판을 반복하는 것만으로 뚫린다.
        //
        //     ⚠️ 0이면 처음부터 열려 있다 (첫 구역).
        // ==============================================================================
        [Header("해금 — 재화로 산다")]
        public int unlockScrap;
        public int unlockCircuit;
        public int unlockCore;

        public bool FreeFromStart => unlockScrap <= 0 && unlockCircuit <= 0 && unlockCore <= 0;

        // ⬜ **더 이상 안 읽는다** (2026-08-23, rev.12).
        //    맵이 **화면 한 장**이 되면서 크기는 창이 정한다 —
        //    `RunDirector.MapHalf`가 카메라에서 직접 뽑는다.
        //    지우지 않고 남겨 둔 이유: 저장된 에셋에 값이 들어 있고,
        //    넓은 맵으로 되돌릴 가능성이 아직 닫히지 않았다.
        [Tooltip("⬜ 미사용 — 맵 크기는 창 크기가 정한다")]
        public Vector2 mapHalfSize = new Vector2(60f, 40f);

        [Tooltip("화면 주변에 동시에 존재할 수 있는 최대 개수")]
        public int junkCount = 70;

        [Tooltip("시작할 때 화면 안에 미리 깔아두는 개수. 첫 화면이 비면 안 된다")]
        public int initialFill = 26;

        [Tooltip("🔴 초당 바깥에서 흘러들어오는 개수. 이게 이 지역의 처리량 상한이다")]
        public float spawnPerSecond = 4f;

        [Tooltip("들어오는 것 중 위험물 비율(0~1)")]
        public float hazardRatio = 0.12f;
        [Tooltip("이 지역에서 나오는 쓰레기 등급 범위")]
        public int minTier = 0;
        public int maxTier = 0;

        [Tooltip("🔴 기지 연료가 초당 이만큼 닳는다. **맵의 난이도는 여기서 나온다** — " +
                 "깊을수록 빨리 닳으므로 더 자주, 더 많이 가져와야 한다")]
        public float baseDrainPerSecond = 6f;

        [Tooltip("🔴 다음 지역으로 떠날 때 기지 연료를 이만큼 쓴다. **연료가 곧 여비다** — " +
                 "지금 떠나면 적은 연료로 시작하고, 더 캐고 떠나면 여유롭지만 그동안 계속 닳는다")]
        public float travelFuelCost = 320f;

        [Header("맵 진행")]
        [Tooltip("🔴 이 맵의 총 웨이브 수. 다 넘기면 최종 보스가 나온다")]
        public int waveCount = 8;

        [Tooltip("웨이브 하나의 길이(초)")]
        public float waveSeconds = 30f;

        [Tooltip("🔴 보스를 해체해야 다음 층 문이 열린다")]
        public BossDef boss = new BossDef();

        public int unlockCost;
        public Color ambient = new Color(0.043f, 0.047f, 0.07f);
    }

    // ==================================================================================
    //  보스
    // ==================================================================================

    /// <summary>
    /// 보스가 방해하는 방식. 🔴 전투가 아니다 — 공격도 체력도 없고, 플레이어는 그냥 '해체'한다.
    /// 도구가 곧 무기라서 새 시스템이 거의 늘지 않는다. (project-brief.md §8의 '전투 안 함'과 양립)
    /// </summary>
    public enum BossKind
    {
        Inert = 0,      // 아무 방해도 안 함 — 해체를 가르치는 용도
        Repulsor,       // 반발장으로 배를 밀어낸다
        Spewer,         // 깎일 때마다 위험물을 토해낸다
        Emp,            // 주기적으로 도구 반경을 줄인다
        Devourer,       // 주변 쓰레기를 자기가 빨아들여 뺏어간다
        Rift            // 위험물을 계속 뿜는다
    }

    [Serializable]
    public class BossDef
    {
        public string displayName = "버려진 위성";
        public BossKind kind = BossKind.Inert;

        [Tooltip("해체에 필요한 총량. 도구가 셀수록 빨리 깎인다")]
        public float integrity = 120f;

        [Tooltip("해체 성공 시 크레딧")]
        public int reward = 400;

        [Tooltip("해체될 때 터져나오는 조각 수 — 이게 도파민 구간이다")]
        public int fragments = 14;

        public float size = 3.2f;
        public Color color = new Color(0.85f, 0.85f, 0.9f);

        [Tooltip("방해 강도. 종류마다 의미가 다르다(밀어내는 힘/뿜는 주기 등)")]
        public float interferePower = 1f;
    }

    // ==================================================================================
    //  카드 (런 안에서 자라는 빌드)
    // ==================================================================================

    /// <summary>
    /// 🔴 2026-08-20 구조: **쓰레기가 경험치다.** 주울수록 레벨이 오르고, 레벨업마다 카드 3장이 나온다.
    ///    장비는 **딱 하나만** 장착하며, 첫 레벨업의 카드 3장이 그 장비를 정한다.
    ///    이후 카드는 그 장비를 키우거나 상시 효과를 붙인다.
    ///    → 런 안에 선택이 생기고(이전 설계의 가장 큰 구멍), 매 런 빌드가 달라진다.
    /// </summary>
    public enum CardEffect
    {
        /// <summary>param = WeaponKind. 무기를 얻거나 레벨을 올린다.</summary>
        Weapon = 0,

        ArmCount,       // 회전 절단날 +N
        Cooldown,       // 전 무기 쿨다운 -비율
        MoveSpeed,      // 배 속도 +비율
        ContactResist,  // 접촉 피해 -비율
        ToolLevel,      // 장비 레벨 +1 (반경·출력 동시)
        ToolRange,      // value = +비율
        ToolPower,      // value = +비율
        ToolCooldown,   // value = -비율
        IntakeRadius,   // 수집 판정 반경 +비율
        ValueMul,       // 수집 가치 +비율
        XpGain,         // 경험치 획득 +비율
        FuelMax,        // 최대 연료 +절대값
        Thrust,         // 추력 +절대값
        RefineOnCollect,// 수집당 연료 회수 +절대값
        HazardResist,   // 위험물 페널티 -비율
        BossDamage,     // 보스 해체 속도 +비율

        // 🔴 무기 **패턴별** 강화 (2026-08-22 추가).
        //    무기 이름이 아니라 패턴에 붙여야 무기가 늘어도 카드를 다시 안 쓴다.
        OrbitCount,      // 궤도체 +N (절단날 · 방벽)
        OrbitSpin,       // 궤도 회전 속도 +비율
        OrbitRadius,     // 궤도 반경 +비율
        ProjectileCount, // 발사체 +N (작살 · 원반)
        PierceBonus,     // 관통 +N
        BlastCount,      // 폭발물 +N (폭탄 · 지뢰)
        ChainTargets,    // 연쇄 대상 +N (방전)

        // 🔴 단발성 버프 — 고르는 즉시 몇 초 동안만 켜진다 (2026-08-22 요청).
        //    value = 지속 시간(초).
        BurstPower,      // 피해 +500%
        BurstSize,       // 범위 +500%
        BurstHaste,      // 쿨다운 -75%

        // 🔴 **기지 포탑** (2026-08-21 요청).
        //    rev.7에서 지는 조건은 기지 상실인데, 기지가 아무것도 안 하고 맞기만 했다.
        //    스스로 싸우는 기지는 **레벨업 보상**으로만 얻는다 — 처음부터 쏘면
        //    초반이 쉬워지고 "지킨다"는 긴장이 사라진다.
        BaseTurretLevel, // 포탑 레벨 +N (없으면 이 카드로 처음 생긴다)
        BaseTurretPower, // 포탑 피해 +비율
        BaseTurretRange, // 포탑 사거리 +비율
        BaseTurretHaste, // 포탑 쿨다운 -비율
        BaseTurretCount, // 포신 +N (동시에 여러 목표)
        BaseHpMax        // 🔴 rev.8: 기지 **가동 시간 단축**(초). 이름은 남겨 뒀다 — 저장 데이터 호환
    }

    [Serializable]
    public class CardDef
    {
        public string title;
        [TextArea] public string description;
        public CardEffect effect;
        public int param;
        public float value;

        [Tooltip("뽑기 가중치")]
        public int weight = 10;

        [Tooltip("첫 레벨업(장비 선택) 전용 카드")]
        public bool startingCard;

        /// <summary>
        /// 🔴 등급. 같은 효과의 강약을 **색으로** 구분한다.
        ///    "피해 +25%"와 "+45%"가 같은 색이면 무엇이 좋은 건지 매번 글을 읽어야 한다 —
        ///    2026-08-22 피드백: *"둘 다 같은 색이라서 에픽·유니크·레전드 식으로 구별이 있으면 좋겠다"*.
        /// </summary>
        public CardRarity rarity = CardRarity.Common;

        public Color color = new Color(0.8f, 0.85f, 1f);

        /// <summary>등급 색. 카드 테두리와 제목에 쓴다.</summary>
        public Color RarityColor => Cards.ColorOf(rarity);
    }

    /// <summary>
    /// 🔴 흔한 순서대로 흰 → 파랑 → 보라 → 주황.
    ///    게임에서 널리 쓰이는 순서라 설명 없이도 읽힌다.
    /// </summary>
    public enum CardRarity
    {
        Common = 0,   // 일반   흰색
        Rare,         // 희귀   파랑
        Epic,         // 영웅   보라
        Legend        // 전설   주황
    }

    public static class Cards
    {
        /// <summary>
        /// 🔴 **이 카드는 기지를 키우나, 우주선을 키우나** (rev.11).
        ///
        ///    자원은 하나인데 쓸 곳이 둘이고, **둘이 서로 다른 국면을 담당한다**:
        ///    · 우주선 → **정박**에서 더 빨리 더 많이 캔다
        ///    · 기지   → **항행**에서 더 멀리 버틴다
        ///
        ///    한쪽만 파면 반드시 막힌다 — 우주선만 키우면 항행에서 깨지고,
        ///    기지만 키우면 캘 게 모자라 강화가 안 돈다.
        ///    **그래서 매 레벨업마다 양쪽을 하나씩은 보여 준다** (`RunDirector.BuildOffers`).
        /// </summary>
        public static bool IsBase(CardEffect e)
        {
            switch (e)
            {
                case CardEffect.BaseTurretLevel:
                case CardEffect.BaseTurretPower:
                case CardEffect.BaseTurretRange:
                case CardEffect.BaseTurretHaste:
                case CardEffect.BaseTurretCount:
                case CardEffect.BaseHpMax:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>카드에 붙일 라벨. 화면에서 어느 쪽을 키우는지 바로 읽히게 한다.</summary>
        public static string SideName(CardEffect e) => IsBase(e) ? "기지" : "우주선";

        public static Color SideColor(CardEffect e) => IsBase(e)
            ? new Color(1.00f, 0.72f, 0.35f)     // 주황 = 기지
            : new Color(0.55f, 0.90f, 1.00f);    // 청록 = 우주선

        public static Color ColorOf(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Rare:   return new Color(0.40f, 0.72f, 1.00f);
                case CardRarity.Epic:   return new Color(0.72f, 0.45f, 1.00f);
                case CardRarity.Legend: return new Color(1.00f, 0.62f, 0.22f);
            }
            return new Color(0.86f, 0.90f, 0.96f);
        }

        public static string NameOf(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Rare:   return "희귀";
                case CardRarity.Epic:   return "영웅";
                case CardRarity.Legend: return "전설";
            }
            return "일반";
        }
    }

    // ==================================================================================
    //  테크트리
    // ==================================================================================

    // ==================================================================================
    //  카탈로그 (에셋 = 밸런스 정본)
    // ==================================================================================

    [CreateAssetMenu(fileName = "GameContent", menuName = "SalvageRun/Game Content")]
    public class GameContent : ScriptableObject
    {
        public JunkType[] junk;
        public ComboDef[] combos;
        public StageDef[] stages;
            public CardDef[] cards;

        [Header("경험치")]
        [Tooltip("첫 레벨업에 필요한 경험치. 쓰레기 '가치'가 곧 경험치다")]
        public float xpBase = 90f;
        [Tooltip("레벨마다 필요량이 곱해지는 비율")]
        public float xpGrowth = 1.35f;

        public float XpToNext(int level) => xpBase * Mathf.Pow(xpGrowth, Mathf.Max(0, level));

        public bool IsEmpty => junk == null || junk.Length == 0
                            || combos == null || combos.Length == 0
                            || weapons == null || weapons.Length == 0
                            || techTree == null || techTree.Length == 0
                            || ships == null || ships.Length == 0;

        public WeaponDef[] weapons;
        public TechNodeDef[] techTree;
        public ShipDef[] ships;

        public ShipDef Ship(string id)
        {
            if (ships == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < ships.Length; i++)
                if (ships[i].id == id) return ships[i];
            return null;
        }

        /// <summary>고른 배가 없거나 사라졌으면 첫 배로 떨어진다.</summary>
        public ShipDef ShipOrDefault(string id)
        {
            var s = Ship(id);
            if (s != null) return s;
            return (ships != null && ships.Length > 0) ? ships[0] : null;
        }

        /// <summary>두 계열의 조합을 찾는다. 순서는 상관없다.</summary>
        public ComboDef FindCombo(WeaponTag a, WeaponTag b)
        {
            if (combos == null) return null;
            for (int i = 0; i < combos.Length; i++)
                if (combos[i].Matches(a, b)) return combos[i];
            return null;
        }

        public WeaponDef Weapon(WeaponKind k)
        {
            if (weapons == null) return null;
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].kind == k) return weapons[i];
            return null;
        }

        public TechNodeDef Node(string id)
        {
            if (techTree == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < techTree.Length; i++)
                if (techTree[i].id == id) return techTree[i];
            return null;
        }

        public StageDef Stage(int index) =>
            (stages == null || index < 0 || index >= stages.Length) ? null : stages[index];

        public int StageCount => stages == null ? 0 : stages.Length;
    }
}
