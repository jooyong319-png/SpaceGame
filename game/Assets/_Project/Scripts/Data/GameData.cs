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
    //  카탈로그 (에셋 = 밸런스 정본)
    // ==================================================================================

    [CreateAssetMenu(fileName = "GameContent", menuName = "SalvageRun/Game Content")]
    public class GameContent : ScriptableObject
    {
        public JunkType[] junk;
        public StageDef[] stages;
        public WeaponDef[] weapons;
        public TechNodeDef[] techTree;

        public bool IsEmpty => junk == null || junk.Length == 0
                            || weapons == null || weapons.Length == 0
                            || techTree == null || techTree.Length == 0;

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
