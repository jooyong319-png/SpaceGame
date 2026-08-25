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
        ShipVisual shipVisual;
        public Transform stageBounds;
        public Camera cam;

        public GameState State { get; private set; } = GameState.Ready;
        public FloorPhase Phase { get; private set; }

        public RunStats Stats { get; private set; }

        /// <summary>🔴 지금 플레이 중인 맵. 맵 하나가 완결된 한 판이다(뱀서 구조).</summary>
        public int MapIndex { get; private set; }
        public StageDef Stage => content.Stage(MapIndex);
        public bool IsFinalWave => Wave >= (Stage != null ? Stage.waveCount : 8);

        // 경험치
        // ⬜ **레벨업을 없앴다** (2026-08-26 사장님 지시: *"레벨업은 없애고"*).
        //    카드 뽑기가 사라진 뒤로 레벨은 **숫자만 오르고 아무것도 안 주고** 있었다.
        //    성장은 전부 정비소(테크트리)로 갔으므로 판 안에 층을 하나 더 둘 이유가 없다.

        // 🔴 웨이브 — 시간이 갈수록 거세진다 (뱀서 골격)
        public int Wave { get; private set; }
        public float NextBossIn { get; private set; }
        public int BossPartsLeft { get; private set; }
        public int FloorCollected { get; private set; }

        float WaveSeconds => Stage != null ? Stage.waveSeconds : 30f;

        // 집계
        public int RunValue { get; private set; }
        public int RunCollected { get; private set; }
        /// <summary>
        /// 이번 런에 **주워서** 되찾은 연료. 2026-08-23부터 출처는 연료 아이템 하나뿐이다
        /// (모선도, 파편 변환도 없앴다). HUD와 결과 화면이 읽는다.
        /// </summary>
        public float FuelRecovered { get; private set; }

        /// <summary>
        /// ⬜ 파편 가치 1당 돌아오던 연료. **2026-08-23에 0이 됐다** (사장님 지시).
        ///    되살리려면 `Absorb()`에서 다시 곱하면 된다.
        /// </summary>
        public const float FuelPerValue = 0f;
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

        Vector2 lastArena;

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
            // 🔴 무기 난수도 런마다 되감는다. 안 그러면 앞 런의 길이가 이번 런을 바꾼다
            if (arms != null) arms.ResetRandom();

            RunValue = 0;
            RunCollected = 0;
            BankedCount = 0;
            towed.Clear();
            SyncDrones();
            WreckCount = 0;

            if (field != null) field.BaseCenter = Vector2.zero;
            FuelRecovered = 0f;
            RunTime = 0f;
            Cleared = false;
            FloorCollected = 0;
            BossPartsLeft = 0;
            BossHits = 0;
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
            field.ResetDockClock();
            field.itemDropChance = config.itemDropChance + Stats.itemDropBonus;
            field.scrapFind = Stats.scrapFind;
            field.circuitFind = Stats.circuitFind;
            field.coreFind = Stats.coreFind;
            for (int i = 0; i < field.MatsThisRun.Length; i++) field.MatsThisRun[i] = 0;
            // 🔴 **배를 먼저 제자리로 돌린 다음에 밭을 짓는다** (2026-08-27).
            //
            //    `StageField.SpawnInside`는 **배 코앞(8유닛)을 피해서** 자리를 뽑는다 —
            //    가까우면 최대 8번까지 다시 뽑는다. 즉 **난수를 몇 번 쓰는지가
            //    배가 어디 있느냐에 달려 있다.**
            //
            //    그런데 `Build`가 `ResetShip`보다 먼저 불리고 있었다.
            //    그래서 밭을 짓는 순간 배는 **앞 런이 끝난 자리**에 서 있었고,
            //    앞 런이 어디서 끝났느냐가 **이번 판의 쓰레기 배치를 통째로 바꿨다.**
            //
            //    실측: 같은 빌드 두 번이 **프레임 1에 이미** 쓰레기 16개의 자리가 달랐다
            //    (개수는 같고 내용 해시가 달랐다). 결정론 3.2%의 마지막 원인이다.
            //
            //    ⚠️ 이건 검사만의 문제가 아니다. 실제 플레이에서도
            //       **앞판을 어디서 끝냈느냐가 다음 판 첫 화면을 바꾼다** — 뜻 없는 연결이다.
            if (!ship.gameObject.activeSelf) ship.gameObject.SetActive(true);
            ship.ResetShip(Vector2.zero,
                Stats.fuelMax * Tuning.ShipFuelMul * Mathf.Clamp(Stats.startFuelRatio, 0.1f, 1f));

            field.Build(Stage, MapHalf);
            UpdateStageBounds(MapHalf);

            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (follow != null) { follow.target = ship.transform; follow.mapHalf = MapHalf; }

            ship.boundsHalf = MapHalf;
            // 🔴 고른 배를 화면에 반영한다. 색과 크기가 안 바뀌면 배를 고른 의미가 안 보인다
            CurrentShip = MetaSave.CurrentShip(content);
            var vis = ship.GetComponentInChildren<ShipVisual>();
            if (vis != null)
            {
                vis.ApplyShip(CurrentShip);
                // 🔴 연 무기만큼 배에 부품이 붙는다 — 산 것이 배에 보여야 "늘었다"가 남는다
                vis.SyncWeaponParts(Stats, content);
            }

            // 🔴 **격침 상태로 판이 끝났으면 배가 꺼진 채 남는다.**
            //    `Wreck()`이 `SetActive(false)`로 끄기 때문에, 다음 판을 그대로 시작하면
            //    **꺼진 배로 시작한다** — 움직이지도, 줍지도 못한다.
            //
            //    2026-08-21 시뮬에서 결정론 91.8% 차이로 잡혔다.
            //    1회차 Lv.14 / 파편 2462 → 2회차 Lv.0 / 파편 201.
            //    밸런스 표 21줄이 통째로 못 쓰게 된 원인이 이 한 줄이었다.
            // 배 되살리기·되돌리기는 위에서 이미 했다 (밭을 짓기 전에 해야 하므로)
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

            RunTime += Time.deltaTime;
            UpdateWave();
            CollectByTouch();
            CollectPickups();

            TidyTow();
            UpdateDrones();
            CheckBossShots();

            Stats.TickBursts(Time.deltaTime);

            if (Phase == FloorPhase.BossIncoming) UpdateBossIntro();

            // 🔴 **판이 끝나는 길은 이것 하나뿐이다.** 맞아 죽는 것도, 격침도 없다
            if (ship.OutOfFuel) Finish("연료 소진 — 자동 귀환");
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

        /// <summary>
        /// 🔴 **자석을 없앴다** (2026-08-26 사장님 지시: *"자석 아예 없애고"*).
        ///
        ///    자석이 있으면 지나가기만 해도 다 빨려 온다 —
        ///    그러면 *"이건 가져갈까, 저건 버릴까"*가 **성립할 수가 없다.**
        ///    이제 **배가 직접 닿아야** 붙는다. 무엇을 실을지가 곧 **어디로 갈지**가 된다.
        ///
        /// 🔴 그리고 **여러 개가 겹쳐 있으면 하나만 문다** — 제일 가까운 것 하나.
        ///    한 번에 다 쓸어 담으면 종류를 고를 수 없고,
        ///    그러면 자석을 없앤 의미가 사라진다.
        ///
        ///    ⚠️ 대신 **덩어리가 오래 남아야** 한다. 금방 사라지면 고를 새가 없다
        ///       (`Fragment`의 수명은 `StageField`가 길게 준다).
        /// </summary>
        void CollectByTouch()
        {
            Vector2 shipPos = ship.transform.position;

            // 닿는 거리. 자석이 아니라 **배 크기**에 가까운 값이다
            float reach = config.intakeRadius * (Stats != null ? Stats.intakeMul : 1f);
            float reach2 = reach * reach;

            // 🔴 **한 번 누르면 하나만.** 홀드였을 때는 매 프레임 하나씩 물어서
            //    뭉쳐 있으면 통째로 빨려 들어갔다 (2026-08-26 피드백).
            bool pressed = CollectOverride ?? Core.InputReader.CollectPressed;

            if (shipVisual == null) shipVisual = ship.GetComponentInChildren<ShipVisual>();
            if (shipVisual != null) shipVisual.intakeRadius = reach;

            // 🔴 **후보는 수집기를 끈 채로도 계산한다** (2026-08-26).
            //    누르기 **전에** 무엇이 걸리는지 보여야 고르는 것이 된다 —
            //    켠 뒤에 알려주면 그건 통보지 선택이 아니다.
            Fragment best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < field.Fragments.Count; i++)
            {
                var f = field.Fragments[i];
                f.Marked = false;                  // 지난 프레임 표시를 지운다

                if (!f.Collectable) continue;      // 이미 끌고 있거나, 방금 버린 것은 건너뛴다

                float sq = ((Vector2)f.transform.position - shipPos).sqrMagnitude;
                if (sq > reach2 || sq >= bestSq) continue;

                bestSq = sq; best = f;
            }

            PickTarget = best;
            if (best == null) return;

            best.Marked = true;

            // 🔴 **자동 회수는 칸이 남을 때만** (테크트리 `TowAuto`).
            //
            //    ⚠️ 꽉 찼을 때는 절대 자동으로 안 줍는다. 밀어내기(`PushOutOldest`)는
            //       **사장님이 손으로 내려야 하는 판단**이다 — *"이 자원을 가져갈까? 버릴까?"*
            //       거기까지 자동이 되면 이 게임의 특색이 통째로 사라진다.
            //    그래서 이 노드가 지우는 건 "빈 칸인데도 Space를 눌러야 하는 번거로움"뿐이다.
            if (Stats != null && Stats.towAuto && towed.Count < MaxTow) pressed = true;

            if (pressed) { Absorb(best); PickTarget = null; }
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
                    FuelRecovered += ship.Fuel - before;
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
        /// <summary>
        /// 🔴 **줍는 게 아니라 매단다** (2026-08-26 사장님 지시:
        ///    *"이걸 먹으면 뒤에 1개씩 줄처럼 매달린다"* · Dome Keeper 방식).
        ///
        ///    재화가 숫자로 사라지는 대신 **배 뒤에 줄줄이 달린다.**
        ///    · 꼬리 길이가 곧 적재량이라 **UI를 안 봐도 얼마나 실었는지 안다**
        ///    · 한계가 딱딱하지 않다 — 더 달 수 있지만 **느려진다**
        ///      그래서 *"하나만 더?"*가 **매 순간** 돌아온다
        ///    · 끌고 **돌아와야** 내 것이 된다. 도중에 버리면 그 자리에 남는다
        ///
        /// 🔴 이게 사장님이 원하신 "선택과 집중"이다 —
        ///    *이걸 가져갈까 · 버릴까 · 이것만 가져갈까.*
        /// </summary>
        void Absorb(Fragment f)
        {
            RunCollected++;
            FloorCollected++;

            // 🔴 **꽉 찼으면 맨 앞이 밀려 떨어진다** (2026-08-26 · Dome Keeper 방식).
            //
            //    버리기 버튼을 두지 않는 이유: Dome Keeper에는 그런 버튼이 **없다.**
            //    거기서 "선택과 집중"은 버리는 조작이 아니라 **애초에 뭘 밟느냐**에서 나온다.
            //    자석을 없앤 지금 우리도 똑같다 — 안 주우려면 그 위로 안 가면 된다.
            //
            //    그리고 이게 더 좋은 이유: **코어를 주우면 고철이 하나 밀려 나간다.**
            //    "이것만 가져갈까"가 버튼이 아니라 **줍는 행위 자체**로 표현된다.
            while (towed.Count >= MaxTow) PushOutOldest();

            towed.Add(f);
            f.AttachTow(LeadFor(towed.Count - 1), towed.Count - 1);

            // 🔴 **주우면 연료가 조금 돈다** (`RefineOnCollect`).
            //    2026-08-26까지 노드 하나(sal_refine)가 이 값을 올리는데
            //    **읽는 곳이 없었다** — 사장님이 재화를 쓰고 아무 일도 안 일어났다.
            //
            //    연료가 곧 시간이므로 이건 "많이 주울수록 판이 길어진다"가 된다.
            //    견인 6칸 제한과 정확히 반대 방향으로 당기는 축이라 재밌다:
            //    **줍고 밀어내기를 반복하면 시간이 벌린다.**
            if (Stats != null && Stats.refinePerCollect > 0f && ship != null)
                ship.Refuel(Stats.refinePerCollect);

            Fx.Spark(f.transform.position, 0.22f, Mats.ColorOf(f.mat), 0.12f);
            Juice.Pickup();
        }

        /// <summary>지금 끌고 있는 것들. 순서가 곧 줄이다.</summary>
        readonly List<Fragment> towed = new List<Fragment>();

        public int TowedCount => towed.Count;

        /// <summary>지금 Space를 누르면 주워질 것. HUD가 이름과 색을 읽는다.</summary>
        public Fragment PickTarget { get; private set; }

        /// <summary>
        /// 🔴 **봇·검사가 대신 누르는 스위치.** 봇은 키보드를 못 누른다 —
        ///    `true`면 후보가 있을 때마다 계속 줍는다 (봇에게는 고르는 판단이 없으므로 그게 맞다).
        ///    이게 없으면 시뮬이 **아무것도 안 줍는 배**를 재게 되고,
        ///    그 숫자로 밸런스를 판단하면 통째로 틀린다.
        ///    (`ShipController.AimOverride`와 같은 이유의 같은 장치다)
        /// </summary>
        public bool? CollectOverride { get; set; }

        /// <summary>
        /// 🔴 **무게는 삼각수로 붙는다** (2026-08-26 · Dome Keeper 방식).
        ///
        ///    Dome Keeper는 N개를 끌면 무게가 **N(N+1)/2**다 —
        ///    1·3·6·10·15… 개수가 늘수록 **가속도로** 무거워진다.
        ///
        ///    처음엔 점근선(계속 달아도 0.35까지만)으로 했는데 그건 틀렸다.
        ///    점근선은 *"많이 달아도 어쨌든 갈 수는 있다"*라서 **결정이 안 생긴다** —
        ///    귀찮을 뿐 못 할 이유가 없으니 결국 전부 줍게 된다.
        ///
        ///    삼각수는 **일곱 개쯤에서 확실히 못 견디게** 만든다.
        ///    그 벽이 있어야 *"이건 두고 갈까"*가 진짜 질문이 된다.
        ///
        ///    ⚠️ 0으로 수렴하면 조작 불능이 되므로 하한을 둔다.
        /// </summary>
        public float TowWeightMul
        {
            get
            {
                int n = towed.Count;
                if (n <= 0) return 1f;

                float carry = n * (n + 1) * 0.5f;                       // 삼각수
                float perUnit = SlowPerCarry
                              / Mathf.Max(0.05f, Tuning.TowWeightMul
                                               * (Stats != null ? Stats.towWeightMul : 1f));

                return Mathf.Clamp(1f - carry * perUnit, MinTowSpeed, 1f);
            }
        }

        /// <summary>캐리 1당 깎이는 속도 비율. 삼각수에 곱해진다.</summary>
        const float SlowPerCarry = 0.012f;

        /// <summary>아무리 무거워도 이보다 느려지지 않는다. 0이면 조작 불능이다.</summary>
        const float MinTowSpeed = 0.25f;

        /// <summary>
        /// 🔴 **끌 수 있는 최대 개수.** Dome Keeper의 *"줄이 6블록을 넘으면 끊긴다"*를 옮긴 것.
        ///    넘으면 **맨 앞(제일 먼저 주운 것)이 밀려 떨어진다.**
        /// </summary>
        /// <summary>배 자체가 끄는 칸.</summary>
        public int ShipTow => Mathf.Max(1, config.towCapacity + (Stats != null ? Stats.towCapacityBonus : 0));

        /// <summary>드론 한 대가 더 끌어 주는 칸.</summary>
        public const int DroneCarry = 2;

        /// <summary>배 + 드론을 합쳐 끌 수 있는 총 칸.</summary>
        public int MaxTow => ShipTow + drones.Count * DroneCarry;

        // ---------------------------------------------------------------- 회수 드론

        /// <summary>
        /// 🔴 **회수 드론** (2026-08-26 사장님 제안:
        ///    *"드론같은게 붙어서 몇 개 더 가져갈 수 있게"*).
        ///
        ///    칸을 늘리는 노드는 이미 있었지만 그건 **숫자만 늘어난다** —
        ///    산 게 화면에 안 보이면 "강해졌다"가 안 남는다.
        ///    드론은 배 옆에 **실제로 떠서 제 줄을 끈다.** 사면 보인다.
        ///
        ///    ⚠️ 장식이 아니다. 줄이 **드론 뒤로 갈라져** 붙는다 —
        ///       배 뒤 6칸이 차면 그다음 2칸은 1번 드론이, 그다음은 2번 드론이 끈다.
        /// </summary>
        readonly List<Transform> drones = new List<Transform>();

        void SyncDrones()
        {
            int want = Stats != null ? Mathf.Max(0, Stats.carrierDrones) : 0;

            while (drones.Count > want)
            {
                var last = drones[drones.Count - 1];
                drones.RemoveAt(drones.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }

            while (drones.Count < want)
            {
                var go = new GameObject("TowDrone" + drones.Count);
                var sr = go.AddComponent<SpriteRenderer>();
                // 드론도 같은 계열로 — 작은 예인선이다. 배와 다른 그림이면 남의 물건처럼 보인다
                sr.sprite = PixelArt.Tug(16, 0.30f, 0.75f, 0.15f);
                sr.color = new Color(0.75f, 0.9f, 1f);
                sr.sortingOrder = 9;
                go.transform.localScale = Vector3.one * 0.55f;
                go.transform.position = ship != null ? ship.transform.position : Vector3.zero;
                drones.Add(go.transform);
            }

            // 🔴 **런마다 드론을 배 옆 제자리로 되돌린다.**
            //    안 그러면 앞 런에서 드론이 어디 떠 있었느냐가 남고,
            //    드론은 견인 줄의 시작점이라 **매달린 것들의 위치가 달라진다.**
            //    (2026-08-22 밸런스 로그에 "드론 위상"으로 이미 한 번 적혀 있는 누수다)
            Vector2 home = ship != null ? (Vector2)ship.transform.position : Vector2.zero;
            for (int i = 0; i < drones.Count; i++)
            {
                if (drones[i] == null) continue;
                drones[i].position = home;
                drones[i].rotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 드론은 배 **옆**에 붙어 따라온다. 좌우로 번갈아 서서 줄이 겹치지 않게 한다.
        /// </summary>
        void UpdateDrones()
        {
            if (drones.Count == 0 || ship == null) return;

            Vector2 fwd = ship.Velocity.sqrMagnitude > 0.04f
                ? ship.Velocity.normalized : Vector2.right;
            Vector2 side = new Vector2(-fwd.y, fwd.x);

            for (int i = 0; i < drones.Count; i++)
            {
                if (drones[i] == null) continue;

                float lane = (i % 2 == 0 ? 1f : -1f) * (1.15f + (i / 2) * 0.75f);
                Vector2 want = (Vector2)ship.transform.position + side * lane - fwd * 0.5f;

                drones[i].position = Vector2.Lerp(drones[i].position, want,
                                                  1f - Mathf.Exp(-9f * Time.deltaTime));

                float ang = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
                drones[i].rotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        /// <summary>
        /// 🔴 **i번째 짐이 무엇을 따라가는가.**
        ///    배 뒤 `ShipTow`칸까지는 배가 끌고, 그다음부터는 드론이 나눠 끈다.
        /// </summary>
        Transform LeadFor(int i)
        {
            int shipCap = ShipTow;

            if (i < shipCap)
                return i == 0 ? ship.transform : towed[i - 1].transform;

            int d = (i - shipCap) / DroneCarry;
            int k = (i - shipCap) % DroneCarry;

            if (d >= drones.Count || drones[d] == null)
                return towed[Mathf.Max(0, i - 1)].transform;      // 드론이 사라졌으면 줄에 잇는다

            return k == 0 ? drones[d] : towed[i - 1].transform;
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
            for (int i = 0; i < towed.Count; i++) towed[i].AttachTow(LeadFor(i), i);
        }

        /// <summary>
        /// 🔴 **맨 앞(제일 먼저 주운 것)을 밀어낸다.** 줄이 꽉 찼을 때 새것이 들어오면서 부른다.
        ///
        ///    맨 뒤가 아니라 맨 앞인 이유: 맨 뒤는 **방금 주운 것**이다.
        ///    방금 집은 걸 도로 뱉으면 "주웠다"가 취소된 것으로 읽혀 조작이 배신처럼 느껴진다.
        ///    앞에서 밀려 나가야 **컨베이어**로 읽힌다.
        ///
        ///    ⚠️ 밀려난 것은 그 자리에 남는다 (`ReleaseTow`가 3초간 다시 안 붙게 잠근다) —
        ///       사라지면 그건 결정이 아니라 손실이다. 되찾으러 갈 수 있어야 한다.
        /// </summary>
        void PushOutOldest()
        {
            if (towed.Count == 0) return;

            var f = towed[0];
            towed.RemoveAt(0);

            // 남은 줄을 다시 잇는다 — 안 그러면 뒤가 허공을 따라간다
            for (int i = 0; i < towed.Count; i++) towed[i].AttachTow(LeadFor(i), i);

            if (f == null || !f.Alive) return;

            Vector2 back = ship.Velocity.sqrMagnitude > 0.01f
                ? -ship.Velocity.normalized : Vector2.down;
            f.ReleaseTow(back * 2.2f);

            AddPopup(f.transform.position, $"{Mats.Name(f.mat)} 밀려남", new Color(1f, 0.75f, 0.45f));
        }

        /// <summary>
        /// 🔴 **끌고 온 것만 내 것이 된다.** 귀환 정산에서 부른다.
        ///    이게 "가져갈까 버릴까"에 값을 매긴다 — 안 그러면 아무거나 다 주우면 된다.
        /// </summary>
        void BankTow()
        {
            for (int i = 0; i < towed.Count; i++)
            {
                var f = towed[i];
                if (f == null || !f.Alive) continue;

                field.MatsThisRun[(int)f.mat] += f.matAmount;
                MetaSave.AddMaterial(f.mat, f.matAmount);
                // 🔴 가져온 재화의 값어치 (테크트리 `MatValue`)
                RunValue += Mathf.RoundToInt(f.value * Stats.valueMultiplier * Stats.matValue);
                BankedCount++;

                f.Despawn();
            }
            towed.Clear();
        }

        public int WreckCount { get; private set; }

        /// <summary>이번 귀환에 실제로 가져온 덩어리 수. 결과 화면이 읽는다.</summary>
        public int BankedCount { get; private set; }

        /// <summary>
        /// 🔴 **닿아도 아프지 않다 — 플레이어는 무적이다** (2026-08-23 사장님:
        ///    *"플레이어를 공격하는 것도 없애고, 플레이어는 무적이야.
        ///      죽는 건 연료가 다 닳아서 죽는 것 말곤 없음"*).
        ///
        ///    그래서 접촉 판정(`CheckContact`)과 적 탄 판정(`CheckEnemyShots`)을 통째로 뺐다.
        ///    배리어·격침·부활도 같이 의미를 잃었다.
        ///
        /// 🔴 **그러면 긴장은 어디서 오나 — 연료다.**
        ///    이제 시계가 하나뿐이다: 나가면 닳고, 안 나가면 못 캔다.
        ///    맞아 죽는 게 없어진 만큼 **연료가 진짜 압박이어야** 판이 성립한다.
        ///    그래서 추진 소모를 되돌렸고(rev.12 초안에서 1/4로 눌러 뒀었다),
        ///    쓰레기의 `fuelBonus`를 처음으로 실제로 물렸다.
        ///
        ///    되찾는 길은 둘이다 — **모선에 들어가 채우거나, 연료가 나오는 쓰레기를 캐거나.**
        ///
        ///    ⚠️ 되살리려면 `rev11-voyage` 브랜치나 이 커밋 직전을 보면 된다.

        // ---------------------------------------------------------------- 레벨업 · 카드

        // ⬜ **무기 상한을 없앴다** (2026-08-26 사장님: *"개수 제한은 없다"*).
        //    연 무기가 전부 배에 붙으므로 셀 상한이 없다.
        public int ComboLevel => config != null ? config.comboLevel : 5;

        /// <summary>지금 판에서 열린 조합. 아직이면 null.</summary>
        public ComboDef ActiveCombo { get; private set; }

        /// <summary>이번 런에 탄 배.</summary>
        public ShipDef CurrentShip { get; private set; }

        /// <summary>조합이 막 열렸을 때 HUD가 크게 알리는 시간.</summary>
        public float comboFlashLeft;

        /// <summary>
        /// ⬜ **조합을 껐다** (2026-08-26).
        ///
        ///    조합은 *"한 판에 무기를 딱 둘만 갖는다"*는 전제 위에 있었다 —
        ///    그 둘을 무엇으로 고르느냐가 그 판의 성격이었고, 태그 쌍이 그 답이었다.
        ///
        ///    사장님 지시로 **연 무기가 전부 붙게** 되면서 그 전제가 사라졌다.
        ///    무기를 다 가지면 조합도 전부 성립하므로 **고른 보람이 없다** —
        ///    남겨 두면 "열렸다"는 팝업만 뜨고 아무 결정도 안 만든다.
        ///
        ///    🔴 그 자리를 대신하는 것이 **테크트리의 발동형 노드**다
        ///       (*"공격 시 N% 폭발"* 같은 것). 사장님이 요청하신 방향이기도 하다.
        ///
        ///    ⚠️ `ComboDef` 표와 `WeaponRig`의 조합 처리는 **그대로 뒀다.**
        ///       무기를 다시 제한하는 날 이 함수만 되살리면 된다.
        /// </summary>
        void CheckCombo() { }

        /// <summary>
        /// 🔴 **보스가 던지는 것에 맞으면 연료가 닳는다** (2026-08-26 사장님 지시:
        ///    *"보스가 투사체를 던지는거야. 그걸 맞으면 플레이어의 연료가 닳고"*).
        ///
        ///    2026-08-23에 위협을 전부 없앴는데(플레이어 무적), 사장님이 **보스에만** 되살리셨다.
        ///
        /// 🔴 **평소에는 여전히 안전하다.** 긴장이 판 전체에 얇게 퍼져 있으면
        ///    *"일정한 위협은 위협이 아니다"*가 된다 — 조용한 구간이 있어야 시끄러운 구간이 무섭다.
        ///    캐는 동안은 마음 놓고 캐고, **보스 앞에서만** 조심하면 된다.
        ///
        ///    ⚠️ 맞아도 죽지 않는다. **연료가 닳을 뿐**이다 — 그건 곧 "이번 판이 짧아진다"이고,
        ///       인크리멘탈에서 그게 딱 맞는 벌이다. 죽음은 여전히 없다.
        /// </summary>
        void CheckBossShots()
        {
            if (Phase != FloorPhase.BossActive) return;

            Vector2 shipPos = ship.transform.position;
            const float hit = 1.0f;

            for (int i = 0; i < field.Shots.Count; i++)
            {
                var sh = field.Shots[i];
                if (!sh.Alive) continue;
                if (((Vector2)sh.transform.position - shipPos).sqrMagnitude > hit * hit) continue;

                sh.Despawn();

                float cost = config.bossShotFuelCost * Tuning.IncomingCostMul
                           * (1f - Mathf.Clamp01(Stats != null ? Stats.bossShotResist : 0f));

                ship.ConsumeFuel(cost);
                BossHits++;

                Fx.Spark(shipPos, 0.6f, new Color(1f, 0.5f, 0.35f), 0.18f);
                AddPopup(shipPos, $"피격 -{cost:0}", new Color(1f, 0.55f, 0.45f));
                Juice.Contact();

                if (ship.OutOfFuel) { Finish("연료 소진 — 자동 귀환"); return; }
            }
        }

        /// <summary>이번 판에 보스 탄에 맞은 횟수. 결과 화면이 읽는다.</summary>
        public int BossHits { get; private set; }

        /// <summary>
        /// 🔴 **검사 전용.** 보스 국면으로 바로 넘긴다.
        ///    스모크가 보스 탄 판정을 재려면 42초를 기다려야 하는데,
        ///    그 사이 다른 것이 섞여 **무엇을 잰 건지 흐려진다.**
        /// </summary>
        public void ForceBossPhaseForTest() => Phase = FloorPhase.BossActive;

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
                Fx.Shockwave(field.BossCenter, Mathf.Lerp(MapHalf.y * 1.4f, MapHalf.y * 0.45f, t),
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

            // 🔴 **부위를 부수면 연료가 돈다** (테크트리 `BossFuel`).
            //    보스전은 연료가 줄줄 새는 구간이다(투사체 피격 + 그냥 흐르는 시간).
            //    여기에 보상을 걸어야 "부수는 게 곧 시간 버는 것"이 된다.
            if (Stats != null && Stats.bossFuel > 0f && ship != null) ship.Refuel(Stats.bossFuel);

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

            // 🔴 **보스를 부숴야 다음 구역이 열린다** (2026-08-26 사장님 지시:
            //    *"이 맵의 다음을 넘어가기 위해선 외계 우주선을 다 부숴야 나갈 수 있는 거지"*).
            //
            //    낮에 잠깐 **재화 구매**로 바꿨었다. 보스가 300초에 나오는데 연료가 짧아
            //    도달이 불가능했기 때문이다. 이제 웨이브를 7초로 줄여 **40초 안에 닿으므로**
            //    원래 자리로 돌려놓는다 — 벽이 둘이면 어느 쪽도 안 급하다.
            MetaSave.UnlockStage(content, MapIndex + 1, true);
            Cleared = true;
            Finish($"{Stage.displayName} 클리어");
        }

        public bool Cleared { get; private set; }


        /// <summary>
        /// 🔴 **한 바퀴가 끝났다 — 지는 게 아니다** (2026-08-26 사장님:
        ///    *"연료를 다 쓰면 자동으로 복귀되는 방식으로 하자"*).
        ///
        ///    Space Rock Breaker 쪽으로 가기로 하면서 **패배라는 개념을 뺐다.**
        ///    인크리멘탈에는 실패가 없다 — 연료가 떨어지면 **자동 귀환**이고,
        ///    가지고 온 것을 정산해서 강화에 쓴다. 그게 한 바퀴다.
        ///
        ///    ⚠️ 그래서 여기서 잃는 것은 아무것도 없다. `revivesLeft`(비상 전개)도
        ///       되살릴 대상이 없어져 뺐다 — 죽지 않는데 부활이 있을 수 없다.
        /// </summary>
        void Finish(string why)
        {
            // 🔴 **끌고 온 것만 내 것이 된다.** 여기가 "가져갈까 버릴까"에 값을 매기는 자리다
            BankTow();

            State = GameState.Result;
            WorldPaused = false;
            if (boss != null) boss.End();
            field.Spawning = false;
            ship.ControlEnabled = false;
            MetaSave.RecordRun(RunValue, RunCollected, 1);

            LastMessage = $"{why} · 파편 {RunCollected}개 · {RunValue} 크레딧";
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

        /// <summary>
        /// 🔴 **타이틀에서 바로 시작한다** (2026-08-23 사장님:
        ///    *"배는 선택할 필요가 없어, 바로 게임 시작이 되어야지"*).
        ///
        ///    준비 화면(`Ready`)은 정비소로 남겨 둔다 — 결과 화면에서 돌아오는 자리다.
        /// </summary>
        public void StartNewMission()
        {
            StartRun(Mathf.Max(0, MetaSave.Data.unlockedMaps - 1));
        }

        /// <summary>타이틀 → 홈. 정비소를 거쳐 가고 싶을 때 쓴다.</summary>
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

        /// <summary>
        /// 🔴 **맵 = 화면 한 장** (2026-08-23 사장님: *"딱 화면 전체가 맵인 걸로 하자"*).
        ///
        ///    2026-08-20에 *"화면 한 장에 갇히면 도망이 성립하지 않는다"*며 넓은 맵 +
        ///    추적 카메라로 바꿨었다. 그 판단을 되돌린다 — 사장님이 rev.4~5로
        ///    돌아가기로 하셨고, 그때는 화면 한 장이었다.
        ///
        /// 🔴 화면 한 장이 주는 것: **모든 위협이 항상 보인다.**
        ///    화면 밖에서 무슨 일이 벌어지는지 몰라서 지는 일이 없어지고,
        ///    "어디에 서 있을지"가 **화면 전체를 보고 내리는 판단**이 된다.
        ///    카메라도 안 움직이니 화면이 조용해서 파바박이 더 잘 읽힌다.
        ///
        ///    ⚠️ `StageDef.mapHalfSize`는 더 이상 안 쓴다. 맵 크기는 창 크기가 정한다.
        /// </summary>
        public Vector2 MapHalf => ScreenHalf;

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

            // 🔴 창 크기가 바뀌면 **맵 크기 자체가 바뀐다** (맵 = 화면이므로).
            //    배 경계·카메라 한계·경계선까지 전부 같이 따라가야 한다 —
            //    하나라도 빠지면 창을 줄인 순간 배가 화면 밖에 갇힌다.
            lastArena = screen;
            field.MapHalf = screen;
            ship.boundsHalf = screen;
            UpdateStageBounds(screen);

            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.mapHalf = screen;
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
