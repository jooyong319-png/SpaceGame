using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Core;
using SalvageRun.Data;
using SalvageRun.Meta;

namespace SalvageRun.Run
{
    /// <summary>
    /// 🔴 `Title`은 게임을 켰을 때 딱 한 번 보는 화면이다.
    ///    itch에서는 로딩이 끝나고 **뭘 봐야 할지 모르는 몇 초**가 이탈 구간이라,
    ///    이름과 "시작" 하나만 있는 화면이 그 몇 초를 잡아 준다.
    /// </summary>
    public enum GameState { Title, Ready, Field, Drafting, Result }
    /// <summary>
    /// 🔴 `BossIncoming`은 **보스 전용 웨이브**다.
    ///    2026-08-22 피드백: *"보스 웨이브가 따로 있으면 좋겠다. 보스인 줄도 몰랐대."*
    ///    웨이브가 끝나자마자 조용히 덩어리 4개가 생기니 아무도 보스인 줄 몰랐다.
    ///    이 구간에서 **유입을 끊고, 화면을 비우고, 등장을 보여준다.**
    /// </summary>
    public enum FloorPhase { Collecting, BossIncoming, BossActive }

    /// <summary>
    /// 🔴 **rev.11의 두 국면** (2026-08-23).
    ///
    ///    이야기: 지구가 기지를 특정 좌표까지 보내는 임무. 연료 수단이 전부 파괴됐고
    ///    남은 방법은 쓰레기를 연료로 바꾸는 것뿐이다.
    ///
    ///    · <see cref="Docked"/> — 기지는 멈춰 있고, 우주선을 몰고 나가 **캔다**
    ///    · <see cref="Travelling"/> — 우주선은 격납되고, 기지가 나아가며
    ///      **플레이어가 기지 무기로 막는다**
    ///
    /// 🔴 국면을 나눈 이유: 지금까지는 위협이 **항상 조금씩** 있어서
    ///    조용한 때도 위험한 때도 없었다. **일정한 위협은 위협이 아니다.**
    ///    조용한 구간이 있어야 시끄러운 구간이 무섭다.
    ///
    /// 🔴 조작도 갈린다 — 정박은 **몰고**, 항행은 **조준한다.**
    ///    조작이 다르면 "국면이 다르다"가 손으로 느껴진다.
    /// </summary>
    public enum Voyage { Docked, Travelling }

    public struct Popup
    {
        public Vector3 worldPos;
        public string text;
        public Color color;
        public float life;
    }

    /// <summary>
    /// 🔴 수직 슬라이스 (2026-08-20). `docs/vertical-slice.md`가 정본이다.
    ///
    ///   Ready → 출항 → 1층 (팔이 깎는다 → 파편 흡수 → 레벨업마다 카드 3장)
    ///         → 할당량 도달 → 보스 부위 3개 등장 → 전부 부수면 종료
    ///         → 연료 0이어도 종료 → Result → 다시
    ///
    /// 확인할 것은 딱 하나다: **깎는 게 손맛이 있나.**
    /// 하강·테크트리·환생·특수 장비·위험물은 전부 뺐다. 손맛이 확인된 뒤에 붙인다.
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        public static RunDirector Instance { get; private set; }

        [Header("주입")]
        public GameContent content;
        public RunConfig config;

        /// <summary>다른 컴포넌트가 설정을 읽을 때 쓴다.</summary>
        public RunConfig Config => config;
        public ShipController ship;
        public StageField field;
        public WeaponRig arms;
        public BossBehaviour boss;
        public HomeBase homeBase;
        ShipVisual shipVisual;
        public Transform stageBounds;
        public Camera cam;

        public GameState State { get; private set; } = GameState.Ready;
        public FloorPhase Phase { get; private set; }

        /// <summary>지금 정박인가 항행인가.</summary>
        public Voyage Leg { get; private set; } = Voyage.Docked;

        public bool Docked => Leg == Voyage.Docked;
        public bool Travelling => Leg == Voyage.Travelling;

        /// <summary>이번 구간에서 남은 거리(0~1). 항행 중에만 뜻이 있다.</summary>
        public float LegProgress { get; private set; }

        /// <summary>이번 구간 총 소요 시간(초). 연료·강화에 따라 달라진다.</summary>
        public float LegSeconds { get; private set; } = 40f;
        public RunStats Stats { get; private set; }

        /// <summary>🔴 지금 플레이 중인 맵. 맵 하나가 완결된 한 판이다(뱀서 구조).</summary>
        public int MapIndex { get; private set; }
        public StageDef Stage => content.Stage(MapIndex);
        public bool IsFinalWave => Wave >= (Stage != null ? Stage.waveCount : 8);

        // 경험치
        public int Level { get; private set; }
        public float Xp { get; private set; }
        public float XpNeed { get; private set; }
        public float XpRatio => XpNeed <= 0f ? 0f : Mathf.Clamp01(Xp / XpNeed);

        // 🔴 웨이브 — 시간이 갈수록 거세진다 (뱀서 골격)
        public int Wave { get; private set; }
        public float NextBossIn { get; private set; }
        public int BossPartsLeft { get; private set; }
        public int FloorCollected { get; private set; }

        float WaveSeconds => Stage != null ? Stage.waveSeconds : 30f;

        // 집계
        public int RunValue { get; private set; }
        public int RunCollected { get; private set; }
        public int ContactHits { get; private set; }
        public float ContactFuelLost { get; private set; }
        public float RunTime { get; private set; }
        public string LastMessage { get; private set; } = "";

        public bool FieldActive => State == GameState.Field;

        /// <summary>
        /// 🔴 세계가 멈춰 있는가. **카드를 고르는 동안 쓰레기가 계속 오면 고를 수가 없다.**
        ///    (2026-08-21 피드백: "카드 선택 때 몬스터들이 안 멈춤")
        ///
        ///    `Time.timeScale`을 건드리지 않는 이유: 헤드리스 시뮬이 `Time.captureDeltaTime`으로
        ///    시간을 굴리는데 timeScale을 0으로 두면 게임 시간이 안 흘러 멈춰 버린다.
        ///    대신 움직이는 것들이 각자 이 플래그를 본다.
        /// </summary>
        public static bool WorldPaused { get; private set; }

        public readonly List<Popup> Popups = new List<Popup>();
        public readonly List<CardDef> Offers = new List<CardDef>();
        public readonly List<CardDef> Taken = new List<CardDef>();

        Vector2 lastArena;
        int draftSeed;

        /// <summary>
        /// 🔴 켜면 카드 뽑기가 매번 같아진다. **밸런스 시뮬 전용.**
        ///    실제 플레이에서 켜면 모든 런이 같은 카드 순서가 된다.
        /// </summary>
        public static bool DeterministicDraft;

        void Awake() => Instance = this;

        void Start()
        {
            RebuildStats();
            State = GameState.Title;      // 🔴 켜면 타이틀부터
            ship.ControlEnabled = false;
        }

        public void RebuildStats()
        {
            Stats = TechSystem.BuildStats(content, config);
            if (ship != null) ship.stats = Stats;
            if (arms != null) { arms.stats = Stats; arms.Rebuild(); }
        }

        // ---------------------------------------------------------------- 런

        public void StartRun(int mapIndex = 0)
        {
            MapIndex = Mathf.Clamp(mapIndex, 0, Mathf.Max(0, content.StageCount - 1));
            RebuildStats();

            // 🔴 영구 강화 '사전 조율'이 있으면 레벨을 올린 채로 시작한다
            Level = Mathf.Max(0, Stats.startLevel);
            Xp = 0f;
            XpNeed = content.XpToNext(Level);
            Taken.Clear();
            Offers.Clear();
            // 🔴 카드 뽑기 시드.
            //
            //    고정값(12345)이었다. 시뮬 재현성 때문이었는데, 그러면
            //    **모든 런에서 카드가 똑같은 순서로 나온다** —
            //    2026-08-22 피드백: *"처음 레벨업 했을 때 나오는 무기가 맨날 같은 것만 나오는 것 같은데?"*
            //    맞다. 항상 같았다.
            //
            //    시뮬(`DeterministicDraft`)만 고정하고, 실제 플레이는 매번 다르게 한다.
            draftSeed = DeterministicDraft
                ? 12345
                : (int)(System.DateTime.Now.Ticks & 0x7fffffff) ^ (MapIndex * 7919);

            // 🔴 무기 난수도 런마다 되감는다. 안 그러면 앞 런의 길이가 이번 런을 바꾼다
            if (arms != null) arms.ResetRandom();

            RunValue = 0;
            RunCollected = 0;
            CargoCount = 0;
            CargoValue = 0;
            CargoXp = 0f;
            DepositedTotal = 0;
            LostCargo = 0;
            AtBase = false;
            RespawnLeft = 0f;
            WreckCount = 0;
            DepositStreak = 0;
            Depositing = false;
            DepositBonus = 1f;
            fullLoadFlash = 0f;
            pendingValue = 0f; pendingXp = 0f;
            DraftIndex = 0; DraftTotal = 0;
            DockedValue = 0; dockFlash = 0f;
            anchorFlash = 0f; finalIntro = 0f;
            IntroLeft = 0f; lastArraysLost = 0;
            Leg = Voyage.Docked; LegProgress = 0f; legIntro = 0f; legSpawn = 0f;

            if (homeBase != null)
            {
                // 🔴 rev.9: 승리는 **가동 게이지**, 패배는 **기지 연료 고갈**.
                //    강화 카드는 가동 시간을 줄인다 (baseHpBonus를 그 용도로 재사용).
                homeBase.Begin(
                    config.baseFuelMax,
                    (Stage != null ? Stage.baseDrainPerSecond : 6f) * Tuning.BaseDrainMul);
                homeBase.director = this;
                homeBase.field = field;
            }
            if (field != null) field.BaseCenter = Vector2.zero;
            ContactHits = 0;
            ContactFuelLost = 0f;
            RunTime = 0f;
            Cleared = false;
            FloorCollected = 0;
            BossPartsLeft = 0;
            Wave = 1;
            NextBossIn = 0f;
            Phase = FloorPhase.Collecting;
            if (boss != null) boss.End();
            Popups.Clear();
            LastMessage = "";

            field.director = this;
            field.target = ship.transform;
            field.spawnRateMul = 1f;
            field.aliveCapOverride = 25;
            field.MapHalf = MapHalf;
            field.spawnRadius = ScreenHalf.magnitude * 1.15f;   // 화면 바로 밖
            field.ResetDockClock();
            field.itemDropChance = config.itemDropChance + Stats.itemDropBonus;
            field.scrapFind = Stats.scrapFind;
            field.circuitFind = Stats.circuitFind;
            field.coreFind = Stats.coreFind;
            for (int i = 0; i < field.MatsThisRun.Length; i++) field.MatsThisRun[i] = 0;
            field.cullRadius = ScreenHalf.magnitude * 2.0f;
            field.Build(Stage, MapHalf);
            UpdateStageBounds(MapHalf);

            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (follow != null) { follow.target = ship.transform; follow.mapHalf = MapHalf; }

            ship.boundsHalf = MapHalf;
            // 🔴 고른 배를 화면에 반영한다. 색과 크기가 안 바뀌면 배를 고른 의미가 안 보인다
            CurrentShip = MetaSave.CurrentShip(content);
            ship.GetComponentInChildren<ShipVisual>()?.ApplyShip(CurrentShip);

            // 🔴 **격침 상태로 판이 끝났으면 배가 꺼진 채 남는다.**
            //    `Wreck()`이 `SetActive(false)`로 끄고 `Respawn()`이 켜는데,
            //    부활을 기다리는 5초 사이에 기지가 무너지면 켜 줄 사람이 없다.
            //    그러면 **다음 판이 꺼진 배로 시작한다** — 움직이지도, 줍지도 못한다.
            //
            //    2026-08-21 시뮬에서 결정론 91.8% 차이로 잡혔다.
            //    1회차 Lv.14 / 파편 2462 → 2회차 Lv.0 / 파편 201.
            //    밸런스 표 21줄이 통째로 못 쓰게 된 원인이 이 한 줄이었다.
            if (!ship.gameObject.activeSelf) ship.gameObject.SetActive(true);

            ship.ResetShip(Vector2.zero, Stats.fuelMax * Tuning.ShipFuelMul * Mathf.Clamp(Stats.startFuelRatio, 0.1f, 1f));
            revivesLeft = Stats.revives;
            ship.ControlEnabled = true;

            State = GameState.Field;
            WorldPaused = false;
            ActiveCombo = null;
        }

        void Update()
        {
            HandleDebugKeys();
            KeepArenaInSync();
            if (comboFlashLeft > 0f) comboFlashLeft -= Time.deltaTime;
            UpdatePopups();

            if (State != GameState.Field) return;

            // 🔴 도입 연출 동안에는 아무것도 안 돈다 — 보는 시간이다
            if (InIntro)
            {
                UpdateIntro();
                return;
            }

            RunTime += Time.deltaTime;

            // 🔴 항행 중에는 밭도 화물도 없다. 오직 막는 것뿐이다
            if (Travelling)
            {
                UpdateLeg();
                UpdatePopups();
                return;
            }

            UpdateWave();
            AttractFragments();
            CollectPickups();

            // 입고 중에는 접촉을 보지 않는다. 쓰레기가 멈춰도 **겹쳐 있으면** 판정이 난다
            // 🔴 **격침 중에는 접촉을 보지 않는다** (2026-08-23 피드백:
            //    *"부활도 못하고 계속 죽은 상태에서 맞는다"*).
            //
            //    배가 꺼져 있어도 `transform.position`은 그 자리에 남는다.
            //    그래서 잔해 자리에 쓰레기가 있으면 판정이 계속 나고,
            //    `Wreck()`이 다시 불려 **부활 타이머가 5초로 되돌아간다.**
            //    로봇은 배를 쫓으므로 죽은 자리에 모여 있다 → **영원히 못 나온다.**
            if (!Depositing && RespawnLeft <= 0f) { CheckContact(); CheckEnemyShots(); }

            TidyTow();
            if (Core.InputReader.JettisonPressed && !Depositing) JettisonTow();

            Stats.TickBursts(Time.deltaTime);
            UpdateCargo();

            if (Phase == FloorPhase.BossIncoming) UpdateBossIntro();

            // 🔴 **rev.7: 우주선이 부서져도 게임은 안 끝난다.**
            //    일정 시간 뒤 기지에서 다시 나온다. 우주선은 소모품이고 기지가 목적이다.
            //    🔴 rev.8: 기지에 체력이 없다. 이기는 건 **가동 게이지를 다 채우는 것**이고
            //       (`TravelToNext`), 격침은 시간 손실이다 — 그동안 기지 연료는 계속 닳는다.
            if (RespawnLeft > 0f)
            {
                RespawnLeft -= Time.deltaTime;
                if (RespawnLeft <= 0f) Respawn();
            }
            else if (ship.OutOfFuel)
            {
                Wreck();
            }
        }

        /// <summary>
        /// 🔴 시간이 갈수록 유입이 거세지고 화면이 어질러진다.
        ///    "우주를 청소하는 느낌"이 성립하려면 **어질러지는 속도**가 있어야 한다 —
        ///    치울 것이 계속 쌓여야 치우는 게 보상이 된다.
        /// </summary>
        void UpdateWave()
        {
            if (Phase != FloorPhase.Collecting) return;

            int total = Stage.waveCount;
            int w = Mathf.Min(total, 1 + Mathf.FloorToInt(RunTime / WaveSeconds));

            if (w != Wave)
            {
                Wave = w;
                AddPopup(ship.transform.position, $"웨이브 {Wave} / {total}", new Color(1f, 0.8f, 0.5f));
                Juice.LevelUp();
            }

            // 🔴 곡선은 완만하게 시작해 후반에 급격히 오른다 (뱀서 실측 참고).
            //    초반은 한산해야 한다 — "파바바박"은 무기가 쌓인 뒤에 오는 보상이지 시작 상태가 아니다.
            //    뱀서는 웨이브가 1분마다이고, 동시 300마리를 넘으면 스폰을 멈춘다.
            field.spawnRateMul = Mathf.Pow(1.45f, Wave - 1);
            field.aliveCapOverride = Mathf.Min(300, 25 + (Wave - 1) * 35);

            // 🔴 쓰레기도 같이 단단해진다. 무기만 세지면 후반엔 뭐든 한 방에 죽어
            //    부수는 손맛이 사라진다 (2026-08-22 플레이 피드백).
            field.hpMul = 1f + (Wave - 1) * 0.55f;

            // 🔴 마지막 웨이브를 다 채우면 최종 보스 — 잡으면 맵 클리어
            NextBossIn = Mathf.Max(0f, total * WaveSeconds - RunTime);
            if (Wave >= total && NextBossIn <= 0f) BeginBossIntro();
        }

        // ---------------------------------------------------------------- 파편 · 접촉

        void AttractFragments()
        {
            Vector2 shipPos = ship.transform.position;
            float r = config.magnetRadius * (Stats != null ? Stats.intakeMul : 1f);

            // 🔴 흡입 반경을 시각에 알려준다 — 빨아들이는 게 보여야 청소기다.
            //    입금 중에는 0으로 꺼서 **비우는 중**이라는 게 눈으로도 읽히게 한다.
            if (shipVisual == null) shipVisual = ship.GetComponentInChildren<ShipVisual>();
            if (shipVisual != null) shipVisual.intakeRadius = Depositing ? 0f : r;
            float r2 = r * r;
            float take2 = config.intakeRadius * config.intakeRadius;

            // 🔴 **입금 중에는 빨아들이지 않는다.**
            //
            //    2026-08-21 시뮬에서 교착이 잡혔다: 배가 기지 정중앙에 속도 0으로 굳고
            //    화물이 200/200에서 120초 동안 1도 안 줄었다.
            //
            //    입금이 화물을 199로 내리면 **다음 프레임에 자석이 주변 파편을 빨아들여
            //    다시 200으로 채웠다.** 기지 주변에 파편이 176개 쌓여 있었으니 무한 반복.
            //    화물이 0이 되어야 입금이 끝나는데 0이 될 수가 없다 → 영원히 입금 중.
            //    봇은 "화물 가득하니 기지로"라고 판단하는데 이미 기지라 붙박이가 된다.
            //
            //    🔴 이건 시뮬만의 문제가 아니다. 사람이 화물 가득 싣고 기지에 갔을 때
            //       주변에 파편이 흩어져 있으면 **똑같이 멈춘다.**
            //       입금을 3초에 걸쳐 빼도록 바꾸면서 생긴 구멍이다 —
            //       예전처럼 한 번에 정산했으면 없었을 문제다.
            //
            //    규칙으로도 자연스럽다: 청소기가 먼지통을 비우는 중에 동시에 빨아들이지는 않는다.
            if (Depositing) return;

            for (int i = 0; i < field.Fragments.Count; i++)
            {
                var f = field.Fragments[i];
                if (!f.Collectable) continue;      // 이미 끌고 있거나, 방금 버린 것은 건너뛴다

                float sq = ((Vector2)f.transform.position - shipPos).sqrMagnitude;

                // 전체 흡수로 날아오는 중이면 자석 반경을 무시하고 **배를 쫓아온다**
                if (f.rushing) f.RushUpdate(shipPos);
                else if (sq > r2) continue;

                if (sq <= take2)
                {
                    // 🔴 가득 차면 더 못 줍는다. 돌아가라는 신호다
                    if (CargoCount < CargoMax) Absorb(f);
                    continue;
                }
                if (!f.rushing) f.Attract(shipPos, config.magnetPull);
            }
        }

        /// <summary>
        /// 🔴 아이템은 **끌려오지 않는다. 직접 가서 먹어야 한다.**
        ///    자동으로 빨려오면 아이템이 그냥 또 하나의 파편이 되고,
        ///    "저기 떴다 → 가지러 간다"는 **선택**이 사라진다.
        ///    가지러 가는 길에 위험을 감수하는 것이 아이템의 값어치다.
        ///    (2026-08-22 플레이 피드백: "아이템들은 흡수되지 않고 직접 그 자리 가서 먹게")
        /// </summary>
        void CollectPickups()
        {
            Vector2 shipPos = ship.transform.position;
            float take = config.intakeRadius + 0.9f;   // 파편보다 판정은 조금 넉넉하게
            float take2 = take * take;

            for (int i = 0; i < field.Pickups.Count; i++)
            {
                var it = field.Pickups[i];
                if (!it.Alive) continue;

                if (((Vector2)it.transform.position - shipPos).sqrMagnitude <= take2) Take(it);
            }
        }

        void Take(PickupItem it)
        {
            switch (it.kind)
            {
                case PickupKind.Fuel:
                {
                    float before = ship.Fuel;
                    ship.Refuel(config.fuelPickupAmount * Stats.fuelPickupMul);
                    AddPopup(ship.transform.position,
                        $"연료 +{Mathf.RoundToInt(ship.Fuel - before)}", PickupItem.ColorFor(it.kind));
                    break;
                }

                case PickupKind.Vacuum:
                {
                    // 🔴 **즉시 삼키지 않는다. 맵 전체가 빨려오는 걸 보여준다.**
                    //    한 프레임에 없애 버리면 숫자만 오르고 아무 그림도 안 남는다 —
                    //    이 게임에서 가장 시원해야 할 순간인데 그게 사라진다.
                    //    (2026-08-22 플레이 피드백: "맵에 있는 모든 고철이 흡수되는 걸 보여주는 게 낫다")
                    int rushed = field.RushAllFragments();
                    AddPopup(ship.transform.position, $"전체 흡수 {rushed}", PickupItem.ColorFor(it.kind));
                    break;
                }
            }

            it.Despawn();
            Juice.LevelUp();     // 🔴 파편 줍는 소리와 달라야 한다. 사건이니까
        }

        /// <summary>
        /// 🔴 **rev.11: 흡수하지 않고 매단다.**
        ///
        ///    (2026-08-23 사장님: *"화물칸에 넣는 것보단 끌고 다니는 방식으로,
        ///    많이 딸려다니면 점점 무거워지게"*)
        ///
        ///    파편이 숫자가 되어 사라지는 대신 **배 뒤에 줄줄이 매달린다.**
        ///    · 꼬리 길이가 곧 적재량이라 **UI를 안 봐도 얼마나 실었는지 안다**
        ///    · 한계가 딱딱하지 않다 — 더 달 수 있지만 **느려진다.**
        ///      그래서 *"하나만 더?"*가 **매 순간** 돌아온다.
        ///      화물칸 방식은 "찼나 안 찼나" 한 번뿐이었다
        /// </summary>
        void Absorb(Fragment f)
        {
            int gained = Mathf.RoundToInt(f.value * Stats.valueMultiplier);
            RunCollected++;
            FloorCollected++;

            CargoCount++;
            CargoValue += gained;
            CargoXp += gained * Stats.xpMultiplier;

            // 줄의 맨 뒤에 붙인다. 앞에 아무것도 없으면 배를 따라간다
            Transform lead = towed.Count > 0 ? towed[towed.Count - 1].transform : ship.transform;
            f.AttachTow(lead, towed.Count);
            towed.Add(f);

            Fx.Spark(f.transform.position, 0.22f, new Color(0.7f, 0.95f, 1f), 0.12f);
            Juice.Pickup();
        }

        /// <summary>지금 끌고 있는 파편들. 순서가 곧 줄이다.</summary>
        readonly System.Collections.Generic.List<Fragment> towed =
            new System.Collections.Generic.List<Fragment>();

        public int TowedCount => towed.Count;

        /// <summary>
        /// 🔴 **끌던 것을 놓는다** (`Q`).
        ///
        ///    로봇에 쫓길 때 **버리고 도망칠 수 있다** — 목숨과 벌이를 맞바꾸는 선택이다.
        ///    무겁다 = 느리다 = **도망을 못 친다**이므로, 욕심이 곧 위험이라는 게
        ///    조작으로 직결된다.
        ///
        ///    버린 것은 **그 자리에 남는다.** 사라지면 그건 결정이 아니라 손실이다.
        /// </summary>
        public void JettisonTow()
        {
            if (towed.Count == 0) return;

            int n = towed.Count;
            Vector2 at = ship.transform.position;

            for (int i = 0; i < towed.Count; i++)
            {
                var f = towed[i];
                if (f == null || !f.Alive) continue;

                // 사방으로 조금씩 튕긴다 — 한 점에 뭉치면 다시 주울 때 한 번에 다 붙는다
                float ang = (i / (float)n) * Mathf.PI * 2f;
                f.ReleaseTow(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 3.5f);
            }

            towed.Clear();
            CargoCount = 0;
            CargoValue = 0;
            CargoXp = 0f;

            AddPopup(at, $"화물 {n}개 투기", new Color(1f, 0.7f, 0.4f));
            Fx.Shockwave(at, 2.2f, new Color(1f, 0.8f, 0.5f, 0.8f), 0.3f);
            Juice.Break();
        }

        /// <summary>줄에서 빠진 것을 정리하고 순서를 다시 잇는다.</summary>
        void TidyTow()
        {
            bool dirty = false;
            for (int i = towed.Count - 1; i >= 0; i--)
            {
                var f = towed[i];
                if (f == null || !f.Alive || !f.Towed) { towed.RemoveAt(i); dirty = true; }
            }
            if (!dirty) return;

            // 앞사람이 사라졌으면 줄을 다시 잇는다 — 안 그러면 뒤가 허공을 따라간다
            for (int i = 0; i < towed.Count; i++)
            {
                Transform lead = i == 0 ? ship.transform : towed[i - 1].transform;
                towed[i].AttachTow(lead, i);
            }
        }

        /// <summary>기지에 넘긴다 — 줄이 통째로 빨려 들어간다.</summary>
        void ConsumeTow(int count)
        {
            for (int i = 0; i < count && towed.Count > 0; i++)
            {
                var f = towed[0];
                towed.RemoveAt(0);
                if (f != null && f.Alive) f.Despawn();
            }

            for (int i = 0; i < towed.Count; i++)
            {
                Transform lead = i == 0 ? ship.transform : towed[i - 1].transform;
                towed[i].AttachTow(lead, i);
            }
        }

        // ================================================================ 화물 · 입금

        /// <summary>지금 싣고 있는 파편 수 · 가치 · 경험치.</summary>
        public int CargoCount { get; private set; }
        public int CargoValue { get; private set; }
        public float CargoXp { get; private set; }

        /// <summary>적재 한계. 넘으면 더 못 줍는다.</summary>
        public int CargoMax => config != null ? config.cargoMax : 200;

        public float CargoRatio => CargoMax <= 0 ? 0f : Mathf.Clamp01(CargoCount / (float)CargoMax);

        /// <summary>
        /// 🔴 무게 = 느려짐. **욕심의 대가**다.
        ///    가득 실으면 이동 속도가 절반 아래로 떨어져 회피가 어려워진다.
        /// </summary>
        /// <summary>
        /// 🔴 **rev.11: 상한이 없다. 계속 무거워진다.**
        ///
        ///    화물칸이 있던 시절엔 "가득 참"에서 딱 끊겼고, 결정은 그 한 번뿐이었다.
        ///    이제는 하나 달 때마다 조금씩 느려져서 **"하나만 더?"가 매 순간** 돌아온다.
        ///
        ///    점근선(0.30)을 두는 이유: 0으로 수렴하면 어느 순간 **아예 못 움직여서**
        ///    조작 불능이 된다. 아주 느리더라도 갈 수는 있어야 플레이어가 스스로 판단한다.
        /// </summary>
        public float CargoWeightMul
        {
            get
            {
                float n = towed.Count;
                float half = Mathf.Max(1f, config.towWeightHalf * Tuning.TowWeightMul);
                return Mathf.Lerp(1f, 0.30f, n / (n + half));
            }
        }

        /// <summary>모선에 닿았는가. HUD와 입금 판정이 같이 쓴다.</summary>
        public bool AtBase { get; private set; }

        /// <summary>
        /// 🔴 **메뉴 없이 지나가면 입금된다.**
        ///    창을 띄우면 뱀서의 끊김 없는 흐름이 깨진다 — rev.2에서 화물 시스템을
        ///    잘라냈던 이유가 그것이었다. 이번엔 흐름을 지키면서 결정만 가져온다.
        /// </summary>
        // ---------------------------------------------------------------- 입금 연출

        /// <summary>입금 중인가. 이 동안 카운터가 내려가고 화물이 기지로 빨려 들어간다.</summary>
        public bool Depositing { get; private set; }

        /// <summary>이번 입금의 시작 물량 — HUD가 진행률을 그린다.</summary>
        public int DepositTotal { get; private set; }

        /// <summary>이번 입금이 만재였는가. 연출이 달라진다.</summary>
        public bool DepositWasFull { get; private set; }

        /// <summary>이번 입금에 적용되는 총 배수 (만재 보너스 × 연쇄).</summary>
        public float DepositBonus { get; private set; } = 1f;

        /// <summary>
        /// 🔴 **연쇄 입금.** 죽지 않고 만재 입금을 이어 가면 쌓인다.
        ///    진짜 재미는 배수가 아니라 **"지금 죽으면 끊긴다"는 공포**다 —
        ///    3연쇄째에 화물 가득 싣고 돌아가는 길이 이 게임에서 가장 긴장되는 30초가 된다.
        /// </summary>
        public int DepositStreak { get; private set; }

        /// <summary>연쇄 배수. 4연쇄에서 ×2.0으로 멈춘다.</summary>
        public float StreakMul => 1f + Mathf.Min(DepositStreak, 4) * 0.25f;

        /// <summary>만재 배너를 띄우는 시간.</summary>
        public float fullLoadFlash;

        /// <summary>이번 도킹에서 실어 온 크레딧 — 정산 배너가 읽는다.</summary>
        public int DockedValue { get; private set; }

        /// <summary>도킹 정산 배너를 띄우는 시간.</summary>
        public float dockFlash;

        float depFrac;          // 소수점 누적 — 프레임률과 무관하게 같은 속도로 흐른다
        float depRate;          // 초당 옮기는 개수
        float depTickClock;
        float depPerValue, depPerXp;
        float depositClock;

        /// <summary>입금이 이보다 오래 걸리면 무언가 잘못된 것이다. 화물 200개도 2.4초면 빠진다.</summary>
        const float DepositTimeout = 8f;
        float pendingValue, pendingXp;

        /// <summary>
        /// 🔴 **입금은 순간이 아니라 장면이다.**
        ///
        ///    바꾸기 전에는 숫자 팝업 하나가 뜨고 끝이었다. 이 게임에서 가장 중요한
        ///    순간인데 가장 밋밋했다.
        ///
        ///    보상이 즉시 끝나면 도파민이 안 나온다 — 슬롯머신이 결과를 바로 안 보여주고
        ///    뱀서의 상자가 느리게 열리는 것과 같은 이유다. 같은 보상이라도
        ///    **올라가는 음과 함께 카운터가 가속하며 떨어지는 3초**가 붙으면 체감이 다르다.
        /// </summary>
        void UpdateCargo()
        {
            float r = config.baseDockRadius;
            AtBase = ((Vector2)ship.transform.position).sqrMagnitude <= r * r;

            if (fullLoadFlash > 0f) fullLoadFlash = Mathf.Max(0f, fullLoadFlash - Time.deltaTime);
            if (dockFlash > 0f) dockFlash = Mathf.Max(0f, dockFlash - Time.deltaTime);
            if (anchorFlash > 0f) anchorFlash = Mathf.Max(0f, anchorFlash - Time.deltaTime);

            if (!Depositing)
            {
                if (AtBase && CargoCount > 0) BeginDeposit();
                return;
            }

            // 🔴 기지를 벗어나면 **멈춘다.** 남은 화물은 그대로 싣고 간다.
            //    다 넣으려면 서 있어야 한다는 게 작지만 진짜 선택이다 —
            //    그 3초 동안 쓰레기는 계속 흘러든다.
            UpdateDocking();
            DrainDeposit();
        }

        /// <summary>
        /// 🔴 **도킹.** 입금하는 동안 배를 기지 중심으로 끌어당기고 조종을 잠근다
        ///    (2026-08-22 요청: *"기지로 쏙 들어가지는 연출... 멈추면서
        ///    재화 얼마나 모았고 레벨업이 파파파파파박"*).
        ///
        ///    전에는 기지 근처를 **지나가면 조용히 정산**됐다. 그래서 아무 사건도 아니었다 —
        ///    이 게임에서 가장 큰 보상의 순간인데 화면에서 아무 일도 안 일어났다.
        ///
        ///    이제 빨려 들어가서 **멈춘다.** 멈추는 것이 핵심이다:
        ///    움직이면서 받는 보상은 배경음이고, **멈춰서 받는 보상은 사건**이다.
        /// </summary>
        void UpdateDocking()
        {
            if (!Depositing || homeBase == null) return;

            Vector2 to = (Vector2)homeBase.transform.position - (Vector2)ship.transform.position;

            // 빨려 들어간다 — 가까울수록 부드럽게 멎는다
            ship.transform.position = Vector2.Lerp(
                ship.transform.position, homeBase.transform.position, 6f * Time.deltaTime);

            if (to.sqrMagnitude > 0.04f && Fx.Instance != null)
            {
                dockFx -= Time.deltaTime;
                if (dockFx <= 0f)
                {
                    dockFx = 0.07f;
                    Fx.Mote(ship.transform.position, homeBase.transform,
                            new Color(0.6f, 1f, 0.9f, 0.8f), 0.25f);
                }
            }
        }

        float dockFx;

        void BeginDeposit()
        {
            Depositing = true;

            // 🔴 조종을 잠근다. 안 잠그면 빨려 들어가는 중에 밖으로 빠져나가
            //    입금이 끊기고 연출이 반쪽이 된다
            ship.ControlEnabled = false;
            ship.AimOverride = null;
            ship.ThrustOverride = null;

            // 🔴 **입고하는 동안 세상이 멈춘다** (2026-08-22 사장님 판단:
            //    *"기지로 들어가면 쓰레기들도 멈추는 게 나아 보이는데?"*).
            //
            //    조종만 잠그면 **묶인 채로 맞아 죽는다** — 만재면 2.4초인데
            //    그 사이 로봇 둘이면 격침이고, 그건 긴장이 아니라 억울함이다.
            //
            //    무적으로 막을 수도 있었지만 멈추는 쪽이 낫다:
            //    · 카드 고를 때 이미 세상이 멈춘다 — **규칙이 일관된다**
            //    · 보상을 받는 동안은 **화면에 다른 일이 없어야** 보상이 보인다
            //    · 기지 연료 감소도 같이 멈춘다 — 입고가 **안전한 주머니**가 된다
            WorldPaused = true;

            DepositTotal = CargoCount;
            DepositWasFull = CargoRatio >= 0.9f;
            DockedValue = CargoValue;

            // 🔴 많이 실을수록 입금 보너스 — 아슬아슬하게 버티다 가는 게 이득이어야
            //    "그냥 자주 왕복하기"가 최적 전략이 되지 않는다.
            DepositBonus = (1f + CargoRatio * config.fullLoadBonus) * StreakMul;

            depPerValue = CargoValue / (float)DepositTotal;
            depPerXp    = CargoXp    / (float)DepositTotal;
            pendingValue = 0f; pendingXp = 0f;
            depFrac = 0f; depTickClock = 0f; depositClock = 0f;

            // 물량이 많을수록 길게, 하지만 가속해서 흐르므로 지루하지 않다
            float seconds = Mathf.Lerp(0.55f, 2.4f, CargoRatio);
            depRate = DepositTotal / Mathf.Max(0.1f, seconds);
        }

        void DrainDeposit()
        {
            // 🔴 **안전장치.** 위 교착(입금하며 동시에 흡입)으로 120초를 굳은 적이 있다.
            //    원인은 고쳤지만, 입금이 안 끝나면 판이 통째로 멎으므로 상한을 둔다.
            //    화물 200개를 빼는 데 2.4초면 되니 8초는 "무언가 잘못됐다"는 뜻이다.
            depositClock += Time.deltaTime;
            if (depositClock > DepositTimeout)
            {
                Debug.LogWarning($"[RunDirector] 입금이 {DepositTimeout}초를 넘겼다 — 강제 정산 " +
                                 $"(남은 화물 {CargoCount}). 교착이 다시 생긴 것이다.");
                CargoCount = 0;
                FinishDeposit();
                return;
            }

            float t01 = DepositTotal <= 0 ? 1f : 1f - CargoCount / (float)DepositTotal;

            // 🔴 뒤로 갈수록 빨라진다. 등속이면 그냥 기다림이고, 가속이면 고조다
            depFrac += depRate * (0.6f + t01 * 1.2f) * Time.deltaTime;

            int move = Mathf.FloorToInt(depFrac);
            if (move > 0)
            {
                depFrac -= move;
                move = Mathf.Min(move, CargoCount);

                CargoCount -= move;
                ConsumeTow(move);
                CargoValue = Mathf.Max(0, CargoValue - Mathf.RoundToInt(depPerValue * move));
                CargoXp    = Mathf.Max(0f, CargoXp - depPerXp * move);

                pendingValue += depPerValue * move * DepositBonus;
                pendingXp    += depPerXp    * move * DepositBonus;

                // 🔴 **입금이 기지를 고친다.** 회복 수단을 따로 두지 않고 입금에 묶은 이유:
                //    그래야 "지금 돌아갈까"에 이유가 하나 더 붙는다. 기지가 위험하면
                //    욕심을 줄이고 자주 실어 나르게 되고, 여유가 있으면 만재를 노리게 된다.
                // 🔴 **입금이 기지를 살린다** (rev.9).
                //    지금까지 "왜 굳이 주워야 하나"의 답이 레벨업뿐이었고, 레벨업은 안 해도 그만이라
                //    결국 안 주워도 되는 게임이었다. 이제 **안 주우면 진다.**
                if (homeBase != null && !homeBase.Destroyed)
                    homeBase.Refuel(move * config.fuelPerCargo * Tuning.FuelPerCargoMul);

                // 화물이 배에서 기지로 빨려 들어가는 줄기
                if (Fx.Instance != null && homeBase != null)
                    Fx.Mote(ship.transform.position, homeBase.transform,
                            new Color(0.6f, 1f, 0.85f, 0.85f), 0.30f);
            }

            // 소리는 개수가 아니라 **시간**으로 낸다. 200개면 200번 울려서 못 듣는다
            depTickClock -= Time.deltaTime;
            if (depTickClock <= 0f)
            {
                depTickClock = Mathf.Lerp(0.075f, 0.030f, t01);
                Juice.DepositTick(t01);
            }

            if (CargoCount <= 0) FinishDeposit();
        }

        /// <summary>기지를 벗어났다 — 넣은 만큼만 정산하고 조용히 끝낸다.</summary>
        void InterruptDeposit()
        {
            Flush();
            Depositing = false;
            ship.ControlEnabled = true;
            WorldPaused = false;
        }

        void FinishDeposit()
        {
            int moved = DepositTotal;
            bool full = DepositWasFull;
            float bonus = DepositBonus;

            Flush();
            Depositing = false;
            ship.ControlEnabled = true;

            // 🔴 여기서 먼저 푼다. 아래 LevelUp()이 카드 화면을 위해 다시 멈추므로
            //    순서가 뒤바뀌면 카드가 뜬 채로 세상이 돌아간다
            WorldPaused = false;

            // 🔴 연쇄는 **만재로 다 넣었을 때만** 오른다. 조금씩 자주 나르는 플레이로는
            //    쌓이지 않아야 "꽉 채워서 살아 돌아오기"에 값이 붙는다.
            if (full)
            {
                DepositStreak++;
                fullLoadFlash = 1.6f;
            }

            DepositedTotal += moved;
            dockFlash = 2.2f;

            AddPopup(ship.transform.position,
                     $"입금 {moved}개" + (bonus > 1.05f ? $"  ×{bonus:0.00}" : ""),
                     new Color(0.5f, 1f, 0.8f));

            Juice.DepositDone(full);

            // 🔴 레벨업은 여기서 **몰아서** 터진다. rev.7은 입금 때만 XP가 들어오므로
            //    한 번에 2~3레벨이 동시에 오른다 — 구조가 이미 그렇게 생겼다
            if (Xp >= XpNeed && State != GameState.Drafting) LevelUp();
        }

        /// <summary>쌓아 둔 크레딧·경험치를 실제로 넣는다.</summary>
        void Flush()
        {
            RunValue += Mathf.RoundToInt(pendingValue);
            Xp += pendingXp;
            pendingValue = 0f; pendingXp = 0f;
        }

        /// <summary>
        /// 🔴 지금 입금하면 몇 레벨이 오르는가. HUD가 "돌아갈 값어치"를 보여줄 때 쓴다.
        /// </summary>
        public int PendingLevels
        {
            get
            {
                if (content == null) return 0;
                float xp = Xp + CargoXp * (1f + CargoRatio * config.fullLoadBonus) * StreakMul;
                float need = XpNeed;
                int lv = Level, n = 0;
                while (xp >= need && n < 20) { xp -= need; lv++; n++; need = content.XpToNext(lv); }
                return n;
            }
        }

        /// <summary>이번 런에 실제로 입금한 총량. 결과 화면이 읽는다.</summary>
        public int DepositedTotal { get; private set; }

        /// <summary>격침 때 흘린 화물. 결과 화면이 읽는다.</summary>
        public int LostCargo { get; private set; }

        /// <summary>우주선이 다시 나오기까지 남은 시간. 0이면 살아 있다.</summary>
        public float RespawnLeft { get; private set; }

        public int WreckCount { get; private set; }

        /// <summary>
        /// 🔴 격침. **런은 계속된다.** 그동안 기지는 무방비다 —
        ///    죽는 것의 대가가 "게임 오버"가 아니라 **"기지가 맞는 시간"**이 된다.
        ///    이게 컨셉과 규칙을 일치시킨다: 우주선은 소모품, 기지가 목적.
        /// </summary>
        void Wreck()
        {
            // 🔴 **이미 격침 상태면 다시 죽지 않는다.** 위의 CheckContact 가드와 이중으로 막는다 —
            //    한 곳만 막으면 다른 경로(연료 고갈 등)로 또 리셋될 수 있다.
            if (RespawnLeft > 0f) return;

            WreckCount++;
            RespawnLeft = config.respawnSeconds;

            // 🔴 **연쇄가 끊긴다.** 배수를 잃는 것 자체보다 "쌓아 둔 걸 날렸다"가 아프다 —
            //    그 아픔이 곧 돌아갈 이유이고, 입금 보상을 키운 만큼의 반대편 추다.
            if (DepositStreak > 0)
            {
                AddPopup(ship.transform.position, $"연쇄 {DepositStreak} 끊김", new Color(1f, 0.6f, 0.3f));
                DepositStreak = 0;
            }

            // 입금 중에 죽으면 넣던 것은 넣은 것으로 친다
            if (Depositing) InterruptDeposit();

            if (CargoCount > 0)
            {
                // 🔴 **잃는 게 아니라 떨어뜨린다.** 전부 잃으면 "많이 싣는다"가 선택지에서 빠지고,
                //    그러면 무게 저울질이라는 이 게임의 세 번째 결정이 통째로 죽는다.
                //    되찾으러 가려면 격침당한 그 자리로 돌아가야 한다 — 위험했던 곳으로.
                // 🔴 rev.11: 끌던 것을 **그대로 놓는다.** 새로 만들어 뿌리지 않는다 —
                //    내가 끌고 있던 바로 그것들이 그 자리에 남아야 "되찾으러 간다"가 성립한다.
                //    (`wreckSpillRatio`로 일부만 남기던 방식은 없앴다.
                //     끌던 게 눈앞에서 절반 증발하면 그건 규칙이 아니라 버그로 읽힌다)
                int spilled = towed.Count;
                JettisonTow();

                AddPopup(ship.transform.position, $"화물 {spilled}개를 그 자리에 놓쳤다",
                         new Color(1f, 0.5f, 0.4f));
            }

            AddPopup(ship.transform.position, "격침 — 재출항 준비", new Color(1f, 0.45f, 0.4f));
            Juice.Break();

            ship.ControlEnabled = false;
            ship.gameObject.SetActive(false);
        }

        void Respawn()
        {
            ship.gameObject.SetActive(true);
            ship.ResetShip(field != null ? field.BaseCenter : Vector2.zero, Stats.fuelMax * Tuning.ShipFuelMul);
            ship.ControlEnabled = true;

            // 🔴 **나오자마자 죽는 고리를 끊는다** (2026-08-23 피드백).
            //    부활 지점은 기지인데 로봇은 배를 쫓으므로 **죽은 자리에 모여 있다.**
            //    무적이 없으면 나오자마자 두 대 맞고 또 죽는다 — 플레이어가 할 수 있는 게 없다.
            ship.GrantInvuln(config.respawnInvulnSeconds);

            // 그리고 기지 주변을 **밀어낸다.** 무적만 주면 무적이 끝나는 순간 같은 일이 난다
            if (field != null)
            {
                Vector2 at = ship.transform.position;
                float r = config.respawnClearRadius;

                for (int i = 0; i < field.Pieces.Count; i++)
                {
                    var p = field.Pieces[i];
                    if (!p.Alive || p.IsBossPart) continue;
                    if (p.type != null && p.type.isAnchor) continue;   // 계류 장치는 안 밀린다

                    Vector2 d = (Vector2)p.transform.position - at;
                    if (d.sqrMagnitude > r * r) continue;

                    p.Flee(at, 16f);
                }

                Fx.Shockwave(at, r, new Color(0.6f, 1f, 0.9f, 0.9f), 0.4f);
            }

            AddPopup(ship.transform.position, "재출항 — 잠시 무적", new Color(0.6f, 1f, 0.8f));
            Juice.LevelUp();
        }

        // ---------------------------------------------------------------- 지역 이동

        /// <summary>
        /// 🔴 **다음 지역으로 떠날 수 있는가** (rev.10).
        ///    기지 안에 있고, 여비(`travelFuelCost`)를 내고도 연료가 남아야 한다.
        /// </summary>
        public bool CanTravel =>
            State == GameState.Field && Docked && AtBase && !Depositing &&
            homeBase != null && !homeBase.Destroyed &&
            Stage != null && MapIndex < content.StageCount - 1 &&
            homeBase.Fuel > Stage.travelFuelCost + 1f &&
            !AnchorsBlocking;

        /// <summary>
        /// 🔴 **계류 장치가 마지막 도약을 막는다** (rev.11 재배치).
        ///
        ///    rev.10에서는 최종 지역에 도착한 뒤 상시로 연료를 빨아먹는 장치였다.
        ///    rev.11에서는 **떠나지 못하게 하는 자물쇠**가 된다 —
        ///    이야기와 맞다: *가야 하는데 붙잡혀 있다.*
        ///
        ///    그래서 마지막 구간은 **"연료를 모아 떠난다"가 아니라
        ///    "붙잡은 것을 끊어내고 떠난다"**가 된다. 목표가 하나 더 얹히는 게 아니라
        ///    **같은 목표(떠나기)에 장애물이 놓이는 것**이라 새로 배울 게 없다.
        /// </summary>
        public bool AnchorsBlocking =>
            field != null && field.AnchorsTotal > 0 && field.AnchorsAlive > 0;

        public float TravelCost => Stage != null ? Stage.travelFuelCost : 0f;

        /// <summary>🔴 마지막 지역인가.</summary>
        public bool AtLastRegion => content != null && MapIndex >= content.StageCount - 1;

        /// <summary>
        /// 🔴 **떠난다.** 이 게임에서 이기는 유일한 길이다.
        ///
        ///    출발을 자동으로 만들지 않은 이유: 그러면 결정이 사라진다.
        ///    여비를 내야 하므로 **"지금 갈까, 더 캐고 갈까"**가 매 지역마다 돌아온다 —
        ///    지금 가면 적은 연료로 시작하고, 더 캐고 가면 여유롭지만 그동안 계속 닳는다.
        ///
        /// 🔴 화물·레벨·무기는 **유지한다.** 지역마다 초기화하면
        ///    이동이 이득이 아니라 손해가 되어 아무도 안 간다.
        /// </summary>
        /// <summary>
        /// 🔴 **rev.11: 즉시 이동이 아니라 항행 국면으로 들어간다.**
        ///    출발만 여기서 하고, 도착은 `FinishLeg()`가 한다.
        /// </summary>
        public void TravelToNext()
        {
            if (!CanTravel) return;

            homeBase.Spend(TravelCost);
            BeginLeg();
        }

        /// <summary>
        /// 🔴 **항행 시작.** 우주선을 격납하고 기지가 나아가기 시작한다.
        ///    이 동안 플레이어는 **기지 무기를 조준**해 정면에서 오는 잔해를 막는다.
        /// </summary>
        void BeginLeg()
        {
            Leg = Voyage.Travelling;
            LegProgress = 0f;
            LegSeconds = config.legSeconds * Tuning.LegSecondsMul;

            // 🔴 우주선을 격납한다 — 조작이 "몰기"에서 "조준"으로 바뀌는 지점이다
            JettisonTow();                    // 끌던 것은 기지가 이미 먹었거나 놓는다
            ship.ControlEnabled = false;
            ship.gameObject.SetActive(false);

            // 항행 중에는 밭이 없다. 정면에서 잔해가 밀려올 뿐이다
            field.ClearAllJunk();
            field.Spawning = false;

            // 🔴 카메라를 기지로 옮긴다 — 배가 꺼졌으므로 따라갈 대상이 없다
            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (follow != null && homeBase != null) follow.target = homeBase.transform;

            legIntro = 3.5f;
            AddPopup(Vector2.zero, "항행 시작 — 기지를 지켜라", new Color(1f, 0.8f, 0.45f));
            Juice.LevelUp();
        }

        /// <summary>항행 시작 안내를 띄우는 시간.</summary>
        public float legIntro;

        /// <summary>
        /// 🔴 **항행 진행.** 시간이 곧 거리다. 잔해가 정면에서 밀려온다.
        /// </summary>
        void UpdateLeg()
        {
            if (legIntro > 0f) legIntro = Mathf.Max(0f, legIntro - Time.deltaTime);

            LegProgress = Mathf.Clamp01(LegProgress + Time.deltaTime / Mathf.Max(1f, LegSeconds));

            // 정면에서 밀려오는 잔해 — rev.7의 조류를 여기서 되살린다
            legSpawn -= Time.deltaTime;
            if (legSpawn <= 0f)
            {
                legSpawn = Mathf.Lerp(0.55f, 0.22f, LegProgress) / Mathf.Max(0.2f, Tuning.IncomingRateMul);
                field.SpawnIncoming();
            }

            if (LegProgress >= 1f) FinishLeg();
        }

        float legSpawn;

        /// <summary>🔴 도착. 다음 지역에 정박한다.</summary>
        void FinishLeg()
        {
            Leg = Voyage.Docked;
            LegProgress = 0f;

            MapIndex++;
            Wave = 1;
            NextBossIn = 0f;
            Phase = FloorPhase.Collecting;
            FloorCollected = 0;
            if (boss != null) boss.End();

            // 새 지역의 감소율로 갈아탄다 — **난이도는 여기서 올라간다**
            homeBase.Retune(Stage.baseDrainPerSecond * Tuning.BaseDrainMul);

            // MapHalf는 Stage에서 파생되는 읽기 전용이다 — MapIndex를 올린 순간 이미 새 값이다
            ship.boundsHalf = MapHalf;
            field.MapHalf = MapHalf;
            field.Build(Stage, MapHalf);
            field.BaseCenter = Vector2.zero;
            UpdateStageBounds(MapHalf);

            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.mapHalf = MapHalf;

            // 🔴 **마지막 구간 직전에 계류 장치가 붙는다.**
            //    도착한 지역이 최종 바로 앞이면, 여기서 떠나려 할 때 붙잡혀 있다.
            //    (rev.10에서는 최종 지역에서 상시로 연료를 빨았다 —
            //     이제는 **떠나지 못하게 하는 자물쇠**다)
            if (MapIndex == content.StageCount - 2)
            {
                field.PlantAnchors();
                finalIntro = 6f;
            }

            // 우주선을 다시 꺼낸다 — 조작이 "조준"에서 "몰기"로 돌아온다
            ship.gameObject.SetActive(true);
            ship.ResetShip(Vector2.zero, Stats.fuelMax * Tuning.ShipFuelMul);
            ship.ControlEnabled = true;
            field.Spawning = true;
            field.ResetDockClock();          // 새 지역이니 썩은 정도도 초기화

            // 카메라를 배로 되돌린다
            var back = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (back != null) back.target = ship.transform;

            AddPopup(Vector2.zero, $"{Stage.displayName} 도착", new Color(0.6f, 1f, 0.85f));
            Juice.DepositDone(true);

            MetaSave.UnlockNextMap(MapIndex - 1);

            // 🔴 **최종 좌표에 닿았다 — 임무 완수.**
            if (AtLastRegion)
            {
                Cleared = true;
                Finish("임무 완수 — 좌표 도달");
            }
        }

        /// <summary>
        /// 🔴 계류 장치를 하나 부쉈다. 남은 게 없으면 **승리.**
        /// </summary>
        public void OnAnchorBroken(int alive, int total)
        {
            if (State != GameState.Field) return;

            anchorFlash = 2.6f;

            if (alive > 0)
            {
                AddPopup(ship.transform.position,
                         $"계류 장치 파괴  {total - alive}/{total}  —  {alive}개 남았다",
                         new Color(0.6f, 1f, 0.85f));
                Juice.DepositDone(true);
                return;
            }

            // 🔴 다 끊었다 — 이제 **떠날 수 있다.** 여기서 판이 끝나는 게 아니다
            AddPopup(ship.transform.position, "계류 해제 — 이제 떠날 수 있다",
                     new Color(0.6f, 1f, 0.85f));
            Juice.DepositDone(true);
        }

        /// <summary>계류 장치를 부순 직후 크게 알리는 시간.</summary>
        public float anchorFlash;

        /// <summary>
        /// 최종 지역 도착 안내를 띄우는 시간.
        /// 🔴 **뭘 해야 하는지 모르면 아무것도 안 한다.** 한 번은 크게 말해 줘야 한다.
        /// </summary>
        public float finalIntro;

        /// <summary>🔴 기지 연료가 바닥났다 — **패배.**</summary>
        public void OnBaseDrained()
        {
            if (State != GameState.Field) return;
            Finish("기지 연료 고갈");
        }

        /// <summary>
        /// 🔴 **적 탄 판정.** 쓰레기 충돌과 **같은 규칙**이다 — 배리어 한 대, 그다음은 격침.
        ///    규칙을 하나로 두면 플레이어가 배울 게 늘지 않는다.
        /// </summary>
        void CheckEnemyShots()
        {
            Vector2 shipPos = ship.transform.position;
            const float hit = 0.9f;

            for (int i = 0; i < field.Shots.Count; i++)
            {
                var sh = field.Shots[i];
                if (!sh.Alive) continue;
                if (((Vector2)sh.transform.position - shipPos).sqrMagnitude > hit * hit) continue;

                sh.Despawn();
                ContactHits++;

                if (ship.AbsorbHit())
                {
                    AddPopup(shipPos, "배리어 파괴", new Color(0.55f, 0.9f, 1f));
                    continue;
                }

                AddPopup(shipPos, "피격 — 격침", new Color(1f, 0.35f, 0.3f));
                Wreck();
                return;
            }
        }

        /// <summary>🔴 쓰레기 본체에 닿으면 연료를 잃는다 — 붙는 것의 대가.</summary>
        void CheckContact()
        {
            Vector2 shipPos = ship.transform.position;

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var j = field.Pieces[i];
                if (!j.Alive) continue;

                float touch = 0.55f + j.transform.localScale.x * 0.5f;
                if (((Vector2)j.transform.position - shipPos).sqrMagnitude > touch * touch) continue;
                if (!j.TryContact()) continue;

                // 🔴 웨이브가 오를수록 접촉이 아파진다.
                //    HP는 웨이브마다 올렸는데 **접촉 피해는 고정**이라,
                //    후반에 쓰레기가 단단해지기만 하고 **위협은 그대로**였다.
                //    2026-08-22 피드백: *"쓰레기가 약해서 잘 안 죽는데, 난이도가 좀 쉬운가봐"*
                //    시뮬에서도 27런 격침 0회였다 — 봇이 잘해서가 아니라 실제로 안 아팠다.
                // 🔴 **rev.10: 우주선은 두 대에 부서진다.**
                //    (2026-08-21: *"배리어가 없는 상태에서 한 대 더 맞으면 우주선 파괴"*)
                //
                //    연료를 야금야금 깎던 방식을 버렸다. 그때는 "지금 얼마나 위험한가"가
                //    **숫자**라서 화면을 안 보면 알 수 없었다.
                //    이제 상태는 둘뿐이다 — **배리어가 있다 / 없다.**
                //    없으면 다음 한 대가 끝이다. 보기만 해도 안다.
                //
                //    🔴 연료는 이제 **오직 이동 비용**이다. 맞아서 줄지 않는다.
                //       역할이 하나면 플레이어가 헷갈릴 일이 없다.
                ContactHits++;

                if (ship.AbsorbHit())
                {
                    AddPopup(ship.transform.position, "배리어 파괴", new Color(0.55f, 0.9f, 1f));
                    continue;
                }

                AddPopup(ship.transform.position, "격침", new Color(1f, 0.35f, 0.3f));
                Wreck();
                return;
            }
        }

        // ---------------------------------------------------------------- 레벨업 · 카드

        void LevelUp()
        {
            Xp -= XpNeed;
            Level++;
            XpNeed = content.XpToNext(Level);

            // 🔴 몰아서 오를수록 음이 높아진다 — "3레벨이 한꺼번에 올랐다"가 귀로 들려야 한다
            Juice.LevelUp();
            if (Xp >= XpNeed) Juice.Fanfare(0.5f, 1.35f);

            BuildOffers();
            if (Offers.Count == 0) return;

            // 이번 정산에서 몇 장을 고르게 되는지 미리 센다 — HUD가 "2 / 5"로 보여준다.
            //    몇 장 남았는지 알면 기다림이 **기대**가 된다. 모르면 그냥 반복이다
            if (DraftTotal <= 0)
            {
                DraftIndex = 0;
                DraftTotal = 1 + ExtraLevelsQueued();
            }

            State = GameState.Drafting;
            ship.ControlEnabled = false;
            WorldPaused = true;
        }

        /// <summary>지금 XP로 이번 정산에서 **추가로** 오를 레벨 수.</summary>
        int ExtraLevelsQueued()
        {
            if (content == null) return 0;

            float xp = Xp;
            int lv = Level, n = 0;
            float need = XpNeed;

            while (xp >= need && n < 20) { xp -= need; lv++; n++; need = content.XpToNext(lv); }
            return n;
        }

        /// <summary>이번 정산에서 지금 몇 번째 장을 고르는가 (0부터).</summary>
        public int DraftIndex { get; private set; }

        /// <summary>이번 정산에서 고르게 될 총 장수.</summary>
        public int DraftTotal { get; private set; }

        /// <summary>
        /// 🔴 무기는 **딱 둘**이다 (우주선이 준 것 + 얻은 것 하나).
        ///    그래서 두 번째 무기를 고르는 순간이 이 게임의 첫 갈림길이고,
        ///    그 판이 어떤 판이 될지가 거기서 정해진다 — 조합 능력까지 같이 결정되니까.
        ///
        ///    · 아직 하나뿐이면 → **무기만** 보여준다. 강화 카드를 섞으면 갈림길이 흐려진다
        ///    · 둘이 되면 → 안 가진 무기 카드는 **영영 안 나온다**
        /// </summary>
        public int MaxWeapons => config != null ? config.maxWeapons : 2;
        public int ComboLevel => config != null ? config.comboLevel : 5;

        public bool PickingSecondWeapon => Stats != null && config != null
                                        && Stats.OwnedWeaponCount < config.maxWeapons;

        void BuildOffers()
        {
            Offers.Clear();

            // 🔴 무기 카드는 **데이터로 두지 않는다.** `content.weapons`에서 그때그때 만든다 —
            //    무기를 추가할 때마다 카드도 같이 써야 하면 반드시 어긋난다.
            if (PickingSecondWeapon) { BuildWeaponOffers(); return; }

            var pool = new List<CardDef>();

            // 보유한 무기의 강화 카드 — 무기가 둘뿐이라 항상 후보에 올린다
            for (int i = 0; i < Weapons.Count; i++)
            {
                var kind = (WeaponKind)i;
                if (!Stats.Has(kind)) continue;

                var def = content.Weapon(kind);
                if (def == null) continue;
                pool.Add(WeaponCard(def, Stats.LevelOf(kind)));
            }

            if (content.cards != null)
                for (int i = 0; i < content.cards.Length; i++)
                {
                    // 남아 있는 구 무기 카드는 무시한다 (에셋이 오래됐을 수 있다)
                    if (content.cards[i].effect == CardEffect.Weapon) continue;
                    pool.Add(content.cards[i]);
                }

            if (pool.Count == 0) return;

            // 🔴 **무기 카드 한 장은 보장한다.**
            //    패시브가 28장이라 무기 강화가 잘 안 뜬다는 피드백이 있었다 (2026-08-22).
            //    무기를 둘만 갖는 게 이 게임의 구조인데, 그 둘을 키울 기회가
            //    운에 맡겨져 있으면 구조가 성립하지 않는다.
            var weaponCards = new List<CardDef>();
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].effect == CardEffect.Weapon) weaponCards.Add(pool[i]);

            if (weaponCards.Count > 0)
            {
                var pick = weaponCards[NextRandom(weaponCards.Count)];
                Offers.Add(pick);
                pool.Remove(pick);
            }

            // 🔴 **기지 카드도 한 장 보장한다** (rev.11 — 이 게임의 전략 축).
            //
            //    자원은 하나인데 쓸 곳이 둘이다: 우주선(정박에서 캔다) / 기지(항행에서 버틴다).
            //    한쪽만 파면 반드시 막히므로 **매번 양쪽을 다 보여 줘야** 저울질이 성립한다.
            //    기지 카드가 9장뿐이라 운에 맡기면 몇 판 내내 안 뜬다 —
            //    그러면 플레이어는 "기지를 키울 수 있다"는 걸 모른 채 항행에서 깨진다.
            var baseCards = new List<CardDef>();
            for (int i = 0; i < pool.Count; i++)
                if (Cards.IsBase(pool[i].effect)) baseCards.Add(pool[i]);

            if (baseCards.Count > 0 && Offers.Count < Stats.cardChoices)
            {
                var pick = baseCards[NextRandom(baseCards.Count)];
                Offers.Add(pick);
                pool.Remove(pick);
            }

            DrawFrom(pool, Stats.cardChoices - Offers.Count);
        }

        /// <summary>
        /// 🔴 두 번째 무기를 고르는 판. **무기만** 보여준다.
        ///    패시브를 섞으면 이 게임의 첫 갈림길이 흐려진다 —
        ///    여기서 고른 것이 조합까지 결정하기 때문에 다른 판보다 중요하다.
        /// </summary>
        void BuildWeaponOffers()
        {
            var pool = new List<CardDef>();
            if (content.weapons == null) return;

            for (int i = 0; i < content.weapons.Length; i++)
            {
                var def = content.weapons[i];
                if (def == null || Stats.Has(def.kind)) continue;
                pool.Add(WeaponCard(def, 0));
            }
            DrawFrom(pool, Stats.cardChoices);
        }

        /// <summary>
        /// 무기 하나를 카드 모양으로 포장한다.
        /// 🔴 **무엇이 얼마나 오르는지 숫자로 보여준다.** "무기 강화"라고만 쓰면
        ///    뭘 고른 건지 알 수가 없다 — 2026-08-22 플레이 피드백: *"무기 강화가 너무 애매하다"*.
        /// </summary>
        CardDef WeaponCard(WeaponDef def, int currentLevel)
        {
            int next = currentLevel + 1;
            string desc;

            if (currentLevel <= 0)
            {
                desc = def.description;
            }
            else
            {
                float dmgNow = def.damage + def.damagePerLevel * (currentLevel - 1);
                float dmgNext = def.damage + def.damagePerLevel * (next - 1);
                desc = $"피해 {dmgNow:0} → {dmgNext:0}";

                if (def.rangePerLevel > 0.001f)
                {
                    float rNow = def.range + def.rangePerLevel * (currentLevel - 1);
                    float rNext = def.range + def.rangePerLevel * (next - 1);
                    desc += $"\n사거리 {rNow:0.0} → {rNext:0.0}";
                }

                if (def.cooldown > 0.001f && def.cooldownPerLevel < 0.999f)
                {
                    float cNow = def.cooldown * Mathf.Pow(def.cooldownPerLevel, currentLevel - 1);
                    float cNext = def.cooldown * Mathf.Pow(def.cooldownPerLevel, next - 1);
                    desc += $"\n쿨다운 {cNow:0.00}초 → {cNext:0.00}초";
                }

                // 개수가 느는 레벨이면 그게 제일 눈에 띄는 변화다
                if (def.countEveryLevels > 0 && (next - 1) % def.countEveryLevels == 0)
                    desc += "\n개수 +1";
            }

            var trait = def.TraitUnlockedAt(next);
            if (trait != null) desc += "\n\n★ " + trait.title + " — " + trait.description;

            return new CardDef
            {
                title = currentLevel <= 0 ? def.displayName : $"{def.displayName}  Lv.{next}",
                description = desc,
                effect = CardEffect.Weapon,
                param = (int)def.kind,
                value = 1f,
                // 🔴 특성이 붙는 레벨은 더 자주 뜨게 한다. 그게 진짜 성장 구간이다
                weight = trait != null ? 34 : 20,
                // 특성이 붙는 레벨은 등급을 올려 **눈에 띄게** 한다
                rarity = trait != null ? CardRarity.Epic : CardRarity.Rare,
                color = def.color
            };
        }

        void DrawFrom(List<CardDef> pool, int want)
        {
            want = Mathf.Min(want, pool.Count);
            for (int n = 0; n < want; n++)
            {
                int total = 0;
                for (int i = 0; i < pool.Count; i++) total += Mathf.Max(1, pool[i].weight);

                int roll = NextRandom(total);
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= Mathf.Max(1, pool[i].weight);
                    if (roll >= 0) continue;
                    Offers.Add(pool[i]);
                    pool.RemoveAt(i);
                    break;
                }
            }
        }

        public void ChooseCard(int index)
        {
            if (State != GameState.Drafting) return;
            if (index < 0 || index >= Offers.Count) return;

            var card = Offers[index];
            TechSystem.ApplyCard(Stats, card);

            // 🔴 기지 체력 카드는 스탯만 올려서는 안 된다 — 지금 서 있는 기지에 바로 반영해야
            //    카드를 먹은 순간 실드 고리가 커지는 게 보인다
            // (rev.8: 기지 체력 카드는 '가동 단축'으로 바뀌었다 — Begin에서 반영된다)
            Taken.Add(card);
            Offers.Clear();

            arms.Rebuild();
            ship.stats = Stats;
            CheckCombo();

            // 🔴 특성이 붙었으면 그걸 알린다. 붙었는데 아무 말이 없으면
            //    "레벨만 올랐네"로 읽혀서 레벨업의 의미가 사라진다.
            if (card.effect == CardEffect.Weapon)
            {
                var wdef = content.Weapon((WeaponKind)card.param);
                var got = wdef?.TraitUnlockedAt(Stats.LevelOf((WeaponKind)card.param));
                if (got != null)
                {
                    AddPopup(ship.transform.position, $"★ {got.title}", wdef.color);
                    LastMessage = $"{wdef.displayName} — {got.title}: {got.description}";
                }
            }

            string tag = card.effect == CardEffect.Weapon
                ? (Stats.LevelOf((WeaponKind)card.param) <= 1 ? "NEW  " : $"Lv.{Stats.LevelOf((WeaponKind)card.param)}  ")
                : "";
            AddPopup(ship.transform.position, tag + card.title, card.color);

            // 🔴 **밀려 있는 보상은 화면을 새로 열지 않고 이어서 고른다** (rev.10).
            //    (2026-08-21 요청: *"여러 번 레벨업 하면 그에 따른 여러 번 보상 지급,
            //    도파민스러운 연출까지"*)
            //
            //    rev.9까지는 카드를 고를 때마다 `State`를 Field로 돌렸다가 다시 Drafting으로
            //    올렸다. 그러면 화면이 **다섯 번 새로 열린다** — 같은 보상인데
            //    보상이 아니라 **절차**로 느껴진다.
            //
            //    입금 한 번에 3~5레벨이 오르는 게 rev.9 이후의 정상이므로,
            //    여기가 이 게임에서 가장 자주 보는 화면이다. 끊기면 안 된다.
            DraftIndex++;

            if (Xp >= XpNeed)
            {
                LevelUp();                       // 화면을 유지한 채 다음 장으로
                if (State == GameState.Drafting) return;
            }

            DraftIndex = 0;
            DraftTotal = 0;

            State = GameState.Field;
            ship.ControlEnabled = true;
            WorldPaused = false;
        }

        /// <summary>지금 판에서 열린 조합. 아직이면 null.</summary>
        public ComboDef ActiveCombo { get; private set; }

        /// <summary>이번 런에 탄 배.</summary>
        public ShipDef CurrentShip { get; private set; }

        /// <summary>조합이 막 열렸을 때 HUD가 크게 알리는 시간.</summary>
        public float comboFlashLeft;

        /// <summary>
        /// 🔴 히든 조합. 두 무기가 **모두** 기준 레벨에 닿으면 저절로 열린다.
        ///    미리 알려주지 않는다 — 발견이 보상이기 때문이다.
        ///    한쪽만 키우면 안 열리므로, "둘 다 키운다"가 자연스럽게 목표가 된다.
        /// </summary>
        void CheckCombo()
        {
            if (ActiveCombo != null) return;
            if (Stats.OwnedWeaponCount < 2) return;

            Stats.OwnedPair(out int a, out int b);
            if (a < 0 || b < 0) return;

            int need = Mathf.Max(1, config.comboLevel - Stats.comboLevelDown);
            if (Stats.weaponLevel[a] < need || Stats.weaponLevel[b] < need) return;

            if (!Stats.OwnedTags(content, out var ta, out var tb)) return;

            var combo = content.FindCombo(ta, tb);
            if (combo == null) return;

            ActiveCombo = combo;
            Stats.combo = combo.effect;

            AddPopup(ship.transform.position, $"★ {combo.title}", combo.color);
            LastMessage = $"조합 발동 — {combo.title}: {combo.description}";
            Juice.LevelUp();

            // 🔴 조합은 이 게임의 차별점이다. 열리는 순간이 **화면에서 사건이어야** 한다.
            //    팝업 한 줄로 지나가면 플레이어는 뭐가 달라졌는지 모른 채 계속 한다.
            Vector2 at = ship.transform.position;
            for (int i = 0; i < 3; i++)
                Fx.Shockwave(at, 6f + i * 3.5f, new Color(combo.color.r, combo.color.g, combo.color.b, 0.9f), 0.5f + i * 0.15f);
            Fx.Spark(at, 5f, combo.color, 0.45f);

            comboFlashLeft = 2.2f;
        }

        /// <summary>시드 고정 의사난수 — 시뮬 재현성을 위해 UnityEngine.Random을 쓰지 않는다.</summary>
        int NextRandom(int maxExclusive)
        {
            draftSeed = draftSeed * 1103515245 + 12345;
            int v = (draftSeed >> 16) & 0x7fff;
            return maxExclusive <= 0 ? 0 : v % maxExclusive;
        }

        // ---------------------------------------------------------------- 보스 = 부위 3개

        /// <summary>보스 등장 연출이 남은 시간. HUD가 읽는다.</summary>
        public float BossIntroLeft { get; private set; }

        /// <summary>
        /// 🔴 보스 등장 구간. 3초 동안 **유입을 끊고 화면을 비운다.**
        ///    쓰레기가 계속 쏟아지는 와중에 덩어리가 생기면 그게 보스인지 알 수 없다 —
        ///    조용해지는 것 자체가 "무언가 온다"는 신호다.
        /// </summary>
        void BeginBossIntro()
        {
            Phase = FloorPhase.BossIncoming;
            introPulse = 0f;

            field.Spawning = false;          // 유입 정지
            field.PushAllOut(24f);           // 남아 있던 것들을 바깥으로 밀어낸다

            // 🔴 보스는 맵 한가운데에 나타난다. 배가 가장자리에 있으면 화면 밖이므로
            //    등장 연출 동안 **어디로 가야 하는지**를 화살표(HUD)가 가리킨다.
            BossIntroLeft = 4f;              // 이동할 시간을 조금 더 준다

            AddPopup(ship.transform.position, "경보 — 대형 반응", new Color(1f, 0.45f, 0.35f));
            Juice.LevelUp();
        }

        void UpdateBossIntro()
        {
            BossIntroLeft -= Time.deltaTime;

            // 배 주위로 경보 고리가 좁혀 들어온다
            introPulse -= Time.deltaTime;
            if (introPulse <= 0f)
            {
                introPulse = 0.45f;
                // 경보 고리는 **보스 자리**에서 퍼진다 — 그쪽을 보게 만든다
                float t = Mathf.Clamp01(1f - BossIntroLeft / 4f);
                Fx.Shockwave(field.BossCenter, Mathf.Lerp(30f, 10f, t),
                             new Color(1f, 0.45f, 0.35f, 0.85f), 0.55f);
            }

            if (BossIntroLeft <= 0f) SpawnBoss();
        }

        float introPulse;

        void SpawnBoss()
        {
            Phase = FloorPhase.BossActive;
            field.Spawning = false;                 // 보스전 동안 일반 유입 정지

            // 🔴 HP를 크게 올렸다. 2026-08-22 시뮬에서 **보스가 0.3~3.2초 만에 죽었다** —
            //    관문이 아니라 통과 의례였다. 맵 등급에 따라 가파르게 오른다.
            float hpScale = 55f + Stage.rank * 45f;
            BossPartsLeft = field.SpawnBossParts(4, hpScale);

            if (BossPartsLeft <= 0) { Clear(); return; }

            // 🔴 팝업을 **배 위에** 띄운다. 원점에 띄우면 화면 밖이라 아무도 못 본다
            AddPopup(ship.transform.position,
                $"{Stage.boss.displayName} — 전부 해체하라", new Color(1f, 0.7f, 0.5f));
            Juice.LevelUp();

            // 🔴 보스가 방해를 시작한다. 지금까지 `BossKind`가 데이터에만 있고
            //    아무도 안 읽어서, 보스가 HP 큰 덩어리일 뿐이었다.
            if (boss != null) boss.Begin(Stage.boss);
        }

        public void OnBossPartBroken()
        {
            BossPartsLeft--;
            if (BossPartsLeft > 0)
            {
                AddPopup(ship.transform.position, $"부위 {BossPartsLeft} 남음", new Color(1f, 0.85f, 0.4f));
                return;
            }
            Clear();
        }

        /// <summary>🔴 맵 클리어 — 다음 맵이 열린다.</summary>
        void Clear()
        {
            if (boss != null) boss.End();

            MetaSave.UnlockNextMap(MapIndex);
            Cleared = true;
            Finish($"{Stage.displayName} 클리어");
        }

        public bool Cleared { get; private set; }

        int revivesLeft;

        void Finish(string why)
        {
            // 🔴 영구 강화 '비상 전개 장치' — 격침될 때 한 번 살아난다.
            //    잃는 것 없이 되살아나면 긴장이 사라지므로 **연료 절반**으로만 돌아온다.
            if (revivesLeft > 0 && why != "강제 종료" && why != "귀환" && !Cleared)
            {
                revivesLeft--;
                ship.ResetShip(ship.transform.position, Stats.fuelMax * 0.5f);
                AddPopup(ship.transform.position, "비상 전개", new Color(1f, 0.6f, 0.4f));
                Juice.LevelUp();
                return;
            }

            // 🔴 **죽으면 화물을 그 자리에 흘린다.** 통째로 잃으면 다시 안 한다 —
            //    5분 모은 걸 날리는 게임은 두 번째 판이 없다.
            //    (지금은 표시만 한다. 회수 구현은 이 루프가 재미있다고 판단된 뒤에)
            if (CargoCount > 0 && why != "귀환")
            {
                AddPopup(ship.transform.position, $"화물 {CargoCount}개 유실", new Color(1f, 0.5f, 0.4f));
                LostCargo = CargoCount;
                CargoCount = 0;
                CargoValue = 0;
                CargoXp = 0f;
            }

            State = GameState.Result;
            WorldPaused = false;
            if (boss != null) boss.End();
            field.Spawning = false;
            ship.ControlEnabled = false;
            MetaSave.RecordRun(RunValue, RunCollected, 1);

            LastMessage = $"{why} · Lv.{Level} · 파편 {RunCollected}개 · {RunValue} 크레딧";
        }

        public void ReturnNow() { if (State == GameState.Field) Finish("귀환"); }

        /// <summary>결과 화면에서 맵 선택으로 돌아간다. 🔴 타이틀이 아니라 **홈**이다 —
        /// 한 판 끝날 때마다 타이틀을 다시 보면 "한 판 더"가 끊긴다.</summary>
        public void BackToReady()
        {
            State = GameState.Ready;
            WorldPaused = false;
            field.Spawning = false;
            ship.ControlEnabled = false;
            Popups.Clear();
            RebuildStats();
        }

        // ---------------------------------------------------------------- 팝업 · 아레나

        /// <summary>타이틀 → 홈. 게임을 켠 뒤 한 번만 지난다.</summary>
        // ================================================================ 도입부 (rev.12)

        /// <summary>
        /// 🔴 **도입 연출** — 태양광 어레이가 뜯겨 나가는 장면.
        ///
        ///    (2026-08-23 사장님: *"새로 하는 경우 기지의 부품들이 파괴되는 모습을
        ///    보여주면서 시작"*)
        ///
        ///    *"연료 수단이 파괴됐다"*를 글로 읽히는 것과 **눈앞에서 뜯겨 나가는 걸 보는 것**은
        ///    완전히 다르다. 그리고 마지막에 남은 게 드릴 하나뿐이라는 것도 그림으로 설명된다.
        ///
        /// 🔴 **컷신으로 분리하지 않는다.** 게임 안에서 벌어지고 그대로 조작이 넘어와야
        ///    "남의 이야기"가 아니라 "내 상황"이 된다.
        /// </summary>
        public float IntroLeft { get; private set; }

        public bool InIntro => IntroLeft > 0f;

        /// <summary>도입부 총 길이.</summary>
        public const float IntroSeconds = 7f;

        /// <summary>어레이 3개가 차례로 뜯긴다. 진행률 0~1.</summary>
        public float IntroProgress => 1f - Mathf.Clamp01(IntroLeft / IntroSeconds);

        /// <summary>지금까지 몇 개가 뜯겨 나갔나 (0~3).</summary>
        public int ArraysLost
        {
            get
            {
                float t = IntroProgress;
                if (t >= 0.72f) return 3;
                if (t >= 0.50f) return 2;
                if (t >= 0.28f) return 1;
                return 0;
            }
        }

        int lastArraysLost;

        /// <summary>새 임무 — 도입 연출부터 시작한다.</summary>
        public void StartNewMission()
        {
            StartRun(0);
            IntroLeft = IntroSeconds;
            lastArraysLost = 0;

            // 연출 동안은 조작이 없다. 보는 시간이다
            ship.ControlEnabled = false;
            if (field != null) field.Spawning = false;
        }

        /// <summary>연출을 건너뛴다.</summary>
        public void SkipIntro()
        {
            if (!InIntro) return;
            IntroLeft = 0f;
            EndIntro();
        }

        void UpdateIntro()
        {
            IntroLeft = Mathf.Max(0f, IntroLeft - Time.deltaTime);

            // 어레이가 하나씩 뜯길 때마다 충격
            int lost = ArraysLost;
            if (lost != lastArraysLost)
            {
                lastArraysLost = lost;

                Vector2 at = homeBase != null ? (Vector2)homeBase.transform.position : Vector2.zero;
                float side = (lost % 2 == 0) ? 1f : -1f;
                Vector2 hit = at + new Vector2(side * 5f, 1.5f);

                Fx.Shockwave(hit, 3.2f, new Color(1f, 0.6f, 0.3f, 0.9f), 0.45f);
                for (int i = 0; i < 10; i++)
                    Fx.Streak(hit, i * 36f, 2.4f, new Color(1f, 0.75f, 0.4f), 0.35f);

                // 마지막 하나는 더 크게 — 여기서 끝이라는 게 느껴져야 한다
                Juice.Break();
                if (lost >= 3)
                {
                    Fx.Shockwave(at, 9f, new Color(1f, 0.4f, 0.25f, 0.95f), 0.8f);
                    Juice.DepositDone(true);
                }
            }

            if (IntroLeft <= 0f) EndIntro();
        }

        void EndIntro()
        {
            IntroLeft = 0f;
            ship.ControlEnabled = true;
            if (field != null)
            {
                field.Spawning = true;
                field.ResetDockClock();
            }
            AddPopup(ship.transform.position, "드릴 가동", new Color(0.6f, 1f, 0.85f));
        }

        public void LeaveTitle()
        {
            if (State == GameState.Title) State = GameState.Ready;
        }

        public void AddPopup(Vector3 pos, string text, Color color)
        {
            if (Popups.Count > 40) Popups.RemoveAt(0);
            Popups.Add(new Popup { worldPos = pos, text = text, color = color, life = 0.9f });
        }

        void UpdatePopups()
        {
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                var p = Popups[i];
                p.life -= Time.deltaTime;
                p.worldPos += Vector3.up * (Time.deltaTime * 1.4f);
                if (p.life <= 0f) Popups.RemoveAt(i);
                else Popups[i] = p;
            }
        }

        /// <summary>맵 반경. 🔴 화면보다 훨씬 크다 — 카메라가 배를 따라간다.</summary>
        public Vector2 MapHalf => Stage != null ? Stage.mapHalfSize : new Vector2(52f, 34f);

        /// <summary>화면 반경 — 스폰/컬링 반경을 여기서 뽑는다.</summary>
        public Vector2 ScreenHalf
        {
            get
            {
                var c = cam != null ? cam : Camera.main;
                if (c == null) return new Vector2(19f, 11f);
                float halfH = c.orthographicSize;
                return new Vector2(halfH * c.aspect, halfH);
            }
        }

        void KeepArenaInSync()
        {
            if (State != GameState.Field) return;

            var screen = ScreenHalf;
            if ((screen - lastArena).sqrMagnitude < 0.01f) return;

            lastArena = screen;
            field.spawnRadius = screen.magnitude * 1.15f;
            field.cullRadius = screen.magnitude * 2.0f;
        }

        void UpdateStageBounds(Vector2 arena)
        {
            if (stageBounds == null) return;
            stageBounds.localScale = new Vector3(arena.x * 2f, arena.y * 2f, 1f);
            stageBounds.position = Vector3.zero;
        }

        void HandleDebugKeys()
        {
            if (InputReader.ForceReturnPressed && State == GameState.Field) Finish("강제 종료");
            if (InputReader.ToggleShakePressed) Juice.ShakeScale = Juice.ShakeScale > 0f ? 0f : 1f;
        }
    }
}
