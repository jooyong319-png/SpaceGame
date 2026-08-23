using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Data;

namespace SalvageRun.Run
{
    /// <summary>
    /// 지역 하나를 채운다.
    ///
    /// 🔴 2026-08-19 (A안): 쓰레기는 **아레나 바깥에서 흘러들어온다.** 유한한 필드를 청소하는 게
    ///    아니라, 계속 떠내려오는 것을 처리하는 구조다. 이렇게 바꾼 이유:
    ///    1차 시뮬레이션에서 "도구를 늘려도 수익이 10%밖에 안 오른다"가 나왔는데,
    ///    원인이 **모든 조합이 어차피 필드를 다 청소해서**였다. ([[balance-log]] 2026-08-19)
    ///    유입식으로 바꾸면 도구가 셀수록 시간당 처리량이 올라 도구 = 수익이 직결된다.
    ///
    /// 아레나는 항상 화면 하나 크기이고, 밖으로 흘러나간 것은 사라진다.
    /// </summary>
    public class StageField : MonoBehaviour
    {
        // (파편·보스 부위는 아래 '파편' 절 참고)
        public GameContent content;
        public Sprite sprite;
        public int seed = 20260819;

        public StageDef Stage { get; private set; }
        public readonly List<JunkPiece> Pieces = new List<JunkPiece>();

        public int AliveCount { get; private set; }
        public int SpawnedTotal { get; private set; }
        public int EscapedTotal { get; private set; }

        /// <summary>
        /// 이번 판에 **실제로 부순** 쓰레기 수.
        /// 🔴 스모크가 *"무기가 안 터진다"*를 넘어 *"무기가 실제로 뭔가 부순다"*까지
        ///    확인할 수 있게 하려고 둔다 — 피해 0인 무기는 예외를 안 내고 조용히 죽어 있다.
        /// </summary>
        public int BrokenTotal { get; private set; }

        /// <summary>런이 진행 중일 때만 유입된다. RunDirector가 켜고 끈다.</summary>
        public bool Spawning { get; set; }

        /// <summary>🔴 쓰레기가 향하는 표적 = 배. 뱀서의 '적이 몰려온다'에 해당한다.</summary>
        public Transform target;

        /// <summary>웨이브 배수 — 시간이 갈수록 RunDirector가 올린다.</summary>
        public float spawnRateMul = 1f;
        public int aliveCapOverride;

        /// <summary>🔴 배 주변 이 반경(화면 밖)에서 생성된다. 뱀서식.</summary>
        public float spawnRadius = 22f;
        /// <summary>이 반경을 벗어나면 사라진다. 화면에서 한참 먼 것을 굴릴 이유가 없다.</summary>
        public float cullRadius = 36f;
        /// <summary>맵 경계 — 쓰레기도 이 밖으로는 안 나간다.</summary>
        public Vector2 MapHalf { get; set; } = new Vector2(52f, 34f);

        /// <summary>
        /// 동시 생존 상한. 🔴 **풀 크기를 넘지 않는다** —
        /// 넘으면 스폰이 조용히 실패해서 손잡이가 먹히지 않는 것처럼 보인다.
        /// </summary>
        int AliveCap
        {
            get
            {
                int want = Mathf.RoundToInt(
                    (aliveCapOverride > 0 ? aliveCapOverride : Stage.junkCount) * Tuning.JunkDensity);
                return Pieces.Count > 0 ? Mathf.Min(want, Pieces.Count) : want;
            }
        }

        System.Random rng;
        float spawnAccum;
        readonly List<JunkType> normalPool = new List<JunkType>();
        readonly List<JunkType> hazardPool = new List<JunkType>();

        /// <summary>바깥 여백 — 여기까지 나가면 사라진다.</summary>
        const float CullMargin = 5f;

        public void Build(StageDef stage, Vector2 arenaHalf)
        {
            Stage = stage;
            MapHalf = arenaHalf;
            rng = new System.Random(seed + stage.rank * 7919);
            spawnAccum = 0f;

            // 🔴 **런 사이에 남으면 안 되는 것들.**
            //    `aliveCapOverride`가 안 지워져서 다음 런의 풀 크기가
            //    **앞 런이 몇 웨이브까지 갔는지에 따라 달라졌다** —
            //    그러면 같은 빌드가 실행마다 다른 결과를 낸다.
            //    2026-08-22 시뮬에서 안 건드린 조합의 결과가 바뀌어 발견했다.
            aliveCapOverride = 0;
            spawnRateMul = 1f;
            hpMul = 1f;

            Fx.ClearAll();

            AliveCount = 0;
            SpawnedTotal = 0;
            Anchors.Clear();
            AnchorsAlive = 0;
            AnchorsTotal = 0;
            ClearShots();
            EscapedTotal = 0;
            BrokenTotal = 0;

            BuildPools(stage);

            // 🔴 풀 크기는 **이 맵이 실제로 도달할 최대치**로 잡는다.
            //
            //    예전엔 `aliveCapOverride`(직전 프레임의 상한)를 참고했는데,
            //    그 값은 런 사이에 남아서 **앞 런이 몇 웨이브까지 갔는지가
            //    다음 런의 풀 크기를 바꿨다.** 그래서 그걸 지웠더니
            //    이번엔 풀이 항상 50개로 작아져서 **웨이브가 올라도 50마리에서 멈췄다.**
            //    (2026-08-22: 한 번의 수정이 다른 버그를 만든 전형적인 경우)
            //
            //    상한은 RunDirector.UpdateWave와 같은 식이어야 한다:
            //      min(300, 25 + (웨이브-1) * 35)
            int maxAlive = Mathf.Min(300, 25 + Mathf.Max(0, stage.waveCount - 1) * 35);

            // 🔴 **밀도 손잡이(`Tuning.JunkDensity`)까지 감안해 풀을 잡는다.**
            //    안 그러면 손잡이를 올려도 풀이 모자라 `FreePiece()`가 null을 돌려주고,
            //    **스폰이 조용히 실패한다** — 사장님은 "올렸는데 안 늘어나네"만 보게 된다.
            //    조용히 안 되는 것이 제일 나쁘다.
            //
            //    ⚠️ 판이 도는 중에 손잡이를 올리면 풀은 안 커진다 (여기서만 잡으므로).
            //       그래서 `AliveCap`이 풀 크기를 넘지 않도록 아래에서 한 번 더 조인다.
            int want = Mathf.RoundToInt(Mathf.Max(stage.junkCount, maxAlive) * Mathf.Max(1f, Tuning.JunkDensity));
            EnsurePool(Mathf.Min(700, want) + 24);

            for (int i = 0; i < Pieces.Count; i++) Pieces[i].Despawn();
            for (int i = 0; i < Fragments.Count; i++) Fragments[i].Despawn();
            for (int i = 0; i < Pickups.Count; i++) Pickups[i].Despawn();

            // 시작 화면이 비면 안 된다 — 미리 깔아둔다
            for (int i = 0; i < stage.initialFill; i++) SpawnInside();

            Spawning = true;
        }

        // ---------------------------------------------------------------- 계류 장치

        /// <summary>지금 살아 있는 계류 장치 수. HUD와 기지가 읽는다.</summary>
        public int AnchorsAlive { get; private set; }

        /// <summary>이 지역에 심은 총 개수 (0이면 계류 지역이 아니다).</summary>
        public int AnchorsTotal { get; private set; }

        public readonly List<JunkPiece> Anchors = new List<JunkPiece>();

        /// <summary>
        /// 🔴 **최종 지역의 닻을 심는다.**
        ///
        ///    기지 앞이 아니라 **맵 곳곳에** 흩어 놓는다 —
        ///    그래야 마지막 판도 *나가서 캐고 돌아오는* 이 게임의 리듬 그대로다.
        ///    기지 앞 한 자리에서 끝나는 보스전은 20분 동안 가르친 것을 하나도 안 쓴다.
        ///
        ///    🔴 **가까운 것부터 약하게** 놓는다. 첫 닻이 쉽게 부서져야
        ///       *"할 만하다"*를 먼저 배우고, 그 뒤에 어려워지는 게 느껴진다.
        /// </summary>
        public void PlantAnchors()
        {
            Anchors.Clear();
            AnchorsAlive = 0;
            AnchorsTotal = 0;
            if (content.junk == null) return;

            var kinds = new List<JunkType>();
            for (int i = 0; i < content.junk.Length; i++)
                if (content.junk[i].isAnchor) kinds.Add(content.junk[i]);
            if (kinds.Count == 0) return;

            EnsurePool(Pieces.Count + kinds.Count + 4);

            for (int i = 0; i < kinds.Count; i++)
            {
                var p = FreePiece();
                if (p == null) break;

                // 네 방향으로 하나씩. 가까운 것부터 약한 순서다
                float ang = (i / (float)kinds.Count) * Mathf.PI * 2f + 0.4f;
                float dist = Mathf.Lerp(MapHalf.magnitude * 0.34f, MapHalf.magnitude * 0.78f,
                                        i / Mathf.Max(1f, kinds.Count - 1f));

                var pos = BaseCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                pos.x = Mathf.Clamp(pos.x, -MapHalf.x + 4f, MapHalf.x - 4f);
                pos.y = Mathf.Clamp(pos.y, -MapHalf.y + 4f, MapHalf.y - 4f);

                p.Spawn(this, kinds[i], (Vector3)pos, Vector2.zero, 1f);
                Anchors.Add(p);
                AliveCount++;
                SpawnedTotal++;
            }

            AnchorsTotal = Anchors.Count;
            AnchorsAlive = AnchorsTotal;
        }

        /// <summary>부서진 닻을 센다. `BreakJunk`가 부른다.</summary>
        void CountAnchor(JunkPiece j)
        {
            if (j.type == null || !j.type.isAnchor || AnchorsTotal <= 0) return;

            // 🔴 **`Alive`만 보면 안 된다.** `Anchors`는 풀의 조각을 가리키는데,
            //    계류 장치가 부서지면 그 슬롯이 **다른 쓰레기로 재사용**된다.
            //    그러면 "살아 있는 다른 쓰레기"를 계류 장치로 세어
            //    **다 끊었는데도 영영 안 풀린다** (2026-08-23 스모크가 잡았다).
            //    지금도 계류 장치인지 **종류까지** 확인해야 한다.
            AnchorsAlive = 0;
            for (int i = 0; i < Anchors.Count; i++)
            {
                var a = Anchors[i];
                if (a != null && a.Alive && a.type != null && a.type.isAnchor) AnchorsAlive++;
            }

            if (director != null) director.OnAnchorBroken(AnchorsAlive, AnchorsTotal);
        }

        /// <summary>
        /// 🔴 **지역이 썩은 정도.** 정박 시간에 따라 1 → 최대 3배까지 오른다.
        ///    로봇 비율에 곱해진다.
        /// </summary>
        public float RotRatio
        {
            get
            {
                if (director == null || !director.Docked) return 1f;
                float mins = DockedSeconds / 60f;
                return Mathf.Clamp(1f + mins * 0.9f, 1f, 3f);
            }
        }

        /// <summary>이번 지역에 정박한 지 몇 초 됐나.</summary>
        public float DockedSeconds { get; private set; }

        public void ResetDockClock() => DockedSeconds = 0f;

        /// <summary>가중치로 하나 고른다. 풀이 비면 null.</summary>
        JunkType PickFrom(System.Collections.Generic.List<JunkType> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++) total += PickChance(pool[i]);
            if (total <= 0f) return pool[rng.Next(pool.Count)];

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= PickChance(pool[i]);
                if (roll <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        readonly System.Collections.Generic.List<JunkType> hunterPool =
            new System.Collections.Generic.List<JunkType>();

        void BuildPools(StageDef stage)
        {
            normalPool.Clear();
            hazardPool.Clear();
            hunterPool.Clear();
            if (content.junk == null) return;

            for (int i = 0; i < content.junk.Length; i++)
            {
                var t = content.junk[i];
                if (t.isHazard)
                {
                    // 위험물은 지역 등급 이하면 전부 섞인다 — 초보 지역에도 하나는 있어야 배운다
                    if (t.tier <= stage.maxTier) hazardPool.Add(t);
                    continue;
                }
                if (t.tier < stage.minTier || t.tier > stage.maxTier) continue;

                // 🔴 로봇은 **별도 풀**이다. 밭과 섞으면 비율을 따로 조절할 수 없고,
                //    그러면 "좋은 밭일수록 위험하다"를 배치로 만들 수 없다
                if (t.isAnchor) continue;                                  // 최종 지역에서 직접 심는다
                if (t.IsRobot) { hunterPool.Add(t); continue; }

                normalPool.Add(t);
            }

            // 상위 지역에도 흔한 것이 조금 섞여야 화면이 비지 않는다
            if (stage.minTier > 0)
            {
                for (int i = 0; i < content.junk.Length; i++)
                {
                    var t = content.junk[i];
                    if (!t.isHazard && !t.IsRobot
                        && t.tier == stage.minTier - 1 && i % 2 == 0) normalPool.Add(t);
                }
            }
        }

        void Update()
        {
            if (Stage == null || RunDirector.WorldPaused) return;

            if (director != null && director.Docked) DockedSeconds += Time.deltaTime;

            CullEscaped();

            if (!Spawning || AliveCount >= AliveCap) return;

            spawnAccum += Stage.spawnPerSecond * spawnRateMul * Tuning.JunkDensity * Time.deltaTime;
            while (spawnAccum >= 1f && AliveCount < AliveCap)
            {
                spawnAccum -= 1f;
                SpawnFromEdge();
            }
        }

        // ---------------------------------------------------------------- 스폰

        /// <summary>
        /// 🔴 종류를 **가중치로** 고른다. 균등 추첨이던 시절엔 무리로 나오는 종류가
        ///    화면을 뒤덮었다 — 뽑힐 확률은 1/8인데 한 번에 5마리씩 나오니
        ///    실제 개체 수는 다른 것의 다섯 배였다. 2026-08-21 피드백:
        ///    *"출현하는 몬스터의 비율이 잘못된 느낌"* — 느낌이 아니라 실제로 그랬다.
        ///
        ///    그래서 `spawnWeight`는 **개체 수 기준**으로 적고, 여기서 groupSize로 나눠
        ///    스폰 확률로 환산한다. 에셋에 적은 비율 = 화면에 보이는 비율.
        /// </summary>
        JunkType PickType()
        {
            // 🔴 **로봇 비율을 먼저 굴린다** (rev.10). 위협의 총량이 여기서 정해진다 —
            //    많으면 캘 틈이 없고, 적으면 밭이 그냥 정물이 된다.
            //    `Tuning.HunterRatio`로 플레이 중에 돌릴 수 있다.
            // 🔴 **정박이 길어질수록 로봇이 늘어난다** (rev.11 — 출발 압박).
            //
            //    타이머를 박는 대신 **지역이 썩게** 만든다.
            //    마감이 밖에서 주어지는 게 아니라 **내가 만든 상황**이 되고,
            //    *"지금이 뜰 때다"*를 스스로 읽게 된다.
            //
            //    그리고 이게 견인·왕복과 맞물린다 — 두고 온 걸 주우러 돌아가면
            //    그만큼 시간이 가고, 시간이 가면 로봇이 는다.
            if (rng.NextDouble() < Tuning.HunterRatio * RotRatio)
            {
                var h = PickFrom(hunterPool);
                if (h != null) return h;
            }

            bool hazard = hazardPool.Count > 0 && rng.NextDouble() < Stage.hazardRatio;
            var pool = hazard ? hazardPool : normalPool;
            if (pool.Count == 0) pool = normalPool;
            if (pool.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Count; i++) total += PickChance(pool[i]);
            if (total <= 0f) return pool[rng.Next(pool.Count)];

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= PickChance(pool[i]);
                if (roll <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        static float PickChance(JunkType t)
            => Mathf.Max(1, t.spawnWeight) / (float)Mathf.Max(1, t.groupSize);

        JunkPiece FreePiece()
        {
            for (int i = 0; i < Pieces.Count; i++)
                if (!Pieces[i].Alive) return Pieces[i];
            return null;
        }

        /// <summary>시작용 — 아레나 안에 바로 놓는다.</summary>
        void SpawnInside()
        {
            var t = PickType();
            var p = FreePiece();
            if (t == null || p == null) return;

            // 🔴 시작할 때도 **밭으로** 깐다. 배 주변 링에 깔면 첫인상이
            //    "몰려온다"가 되어 버린다 — 첫 화면이 장르를 말한다
            p.Despawn();
            SpawnCluster(t, PickFieldSpot());
        }

        /// <summary>
        /// 🔴 **rev.9: 쓰레기는 밭이다.** 몰려오지 않는다 — **여러 곳에 흩어져 있고
        ///    플레이어가 찾아가서 캔다.**
        ///
        ///    (2026-08-21: *"그냥 쓰레기들이 여러 곳에 있는 걸 플레이어가 파밍해서
        ///    기지에 넣는 방식이야."*)
        ///
        /// 🔴 앞선 판본의 흔적을 **세 군데** 지웠다. 하나만 지웠을 때
        ///    사장님이 *"아직도 기지로 모인다"*고 하신 이유가 이것이다:
        ///
        ///    1. 스폰 위치가 **배 주변 링**이었다 → 맵 전체의 무작위 지점으로
        ///    2. 스폰 방향이 **배를 향했다** → 방향 자체를 없앴다 (거의 정지)
        ///    3. `MoveKind.Chase`가 **기지 쪽으로 표류 보정**을 했다 → `JunkPiece`에서 제거
        ///
        ///    ⚠️ 이동을 바꿀 때는 **스폰 위치 · 스폰 속도 · 이동 패턴 셋을 다 봐야 한다.**
        ///       하나만 고치면 나머지 둘이 옛 동작을 그대로 되살린다.
        /// </summary>
        void SpawnFromEdge()
        {
            var t = PickType();
            if (t == null) return;

            // 🔴 로봇은 밭이 아니다. **밭 근처에 붙어** 생긴다 (아래 SpawnHunter)
            if (t.IsRobot) { SpawnHunter(t); return; }

            Vector2 spot = PickFieldSpot();
            SpawnCluster(t, spot);
        }

        /// <summary>
        /// 밭이 생길 자리. **기지에서 멀고, 배 코앞이 아닌 곳**을 고른다.
        /// 배 위에 그냥 튀어나오면 캐러 간다는 감각이 사라지고 그냥 몰려오는 것으로 보인다.
        /// </summary>
        Vector2 PickFieldSpot()
        {
            Vector2 ship = target != null ? (Vector2)target.position : Vector2.zero;

            for (int tries = 0; tries < 12; tries++)
            {
                var pos = new Vector2(RandRange(-MapHalf.x, MapHalf.x),
                                      RandRange(-MapHalf.y, MapHalf.y));

                // 기지 안마당에는 밭이 안 생긴다 — 앉아서 캐면 루프가 사라진다
                if ((pos - BaseCenter).sqrMagnitude < BaseClearRadius * BaseClearRadius) continue;

                // 배 바로 옆에서 솟지 않는다
                if ((pos - ship).sqrMagnitude < 14f * 14f) continue;

                return pos;
            }

            // 자리를 못 찾으면 기지 반대편 어딘가
            float ang = RandRange(0f, Mathf.PI * 2f);
            return BaseCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (BaseClearRadius + 8f);
        }

        /// <summary>기지 주변 이 반경 안에는 밭이 생기지 않는다.</summary>
        public float BaseClearRadius = 18f;

        /// <summary>
        /// 밭 하나. 한 자리에 여러 개가 **뭉쳐서** 뜬다 —
        /// 흩어져 하나씩 있으면 "캐러 간다"가 아니라 "주우며 지나간다"가 된다.
        /// </summary>
        void SpawnCluster(JunkType t, Vector2 spot)
        {
            int n = Mathf.Max(1, t.groupSize) * RandInt(2, 5);
            float spread = 2.2f + n * 0.35f;

            for (int i = 0; i < n && AliveCount < AliveCap; i++)
            {
                var p = FreePiece();
                if (p == null) break;

                var off = new Vector2(RandRange(-spread, spread), RandRange(-spread, spread));
                var pos = spot + off;
                pos.x = Mathf.Clamp(pos.x, -MapHalf.x, MapHalf.x);
                pos.y = Mathf.Clamp(pos.y, -MapHalf.y, MapHalf.y);

                // 🔴 **거의 정지.** 방향도 무작위다 — 어디로도 향하지 않는다
                float ang = RandRange(0f, Mathf.PI * 2f);
                var drift = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * t.driftSpeed * 0.12f;

                p.Spawn(this, t, (Vector3)pos, drift, hpMul);
                AliveCount++;
                SpawnedTotal++;
            }
        }

        /// <summary>
        /// 🔴 **파손 로봇은 밭을 지킨다.** 살아 있는 쓰레기 근처에 붙여 생성한다 —
        ///    좋은 밭일수록 위험하다는 관계를 **수치가 아니라 배치로** 만드는 부분이다.
        ///    밭이 하나도 없으면 배 쪽에서 다가온다 (그때는 로봇이 유일한 할 일이므로).
        /// </summary>
        void SpawnHunter(JunkType t)
        {
            var p = FreePiece();
            if (p == null) return;

            Vector2 spot;
            var host = AnyAliveJunk();

            if (host != null)
            {
                float ang = RandRange(0f, Mathf.PI * 2f);
                spot = (Vector2)host.transform.position
                     + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * RandRange(3f, 9f);
            }
            else
            {
                Vector2 ship = target != null ? (Vector2)target.position : Vector2.zero;
                float ang = RandRange(0f, Mathf.PI * 2f);
                spot = ship + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spawnRadius;
            }

            spot.x = Mathf.Clamp(spot.x, -MapHalf.x, MapHalf.x);
            spot.y = Mathf.Clamp(spot.y, -MapHalf.y, MapHalf.y);

            p.Spawn(this, t, (Vector3)spot, Vector2.zero, hpMul);
            AliveCount++;
            SpawnedTotal++;

            for (int g = 1; g < t.groupSize && AliveCount < AliveCap; g++)
            {
                var extra = FreePiece();
                if (extra == null) break;
                var off = new Vector2(RandRange(-2.5f, 2.5f), RandRange(-2.5f, 2.5f));
                extra.Spawn(this, t, (Vector3)(spot + off), Vector2.zero, hpMul);
                AliveCount++;
                SpawnedTotal++;
            }
        }

        JunkPiece AnyAliveJunk()
        {
            // 결정론을 위해 난수 대신 **순서대로** 훑어 몇 번째 것을 고른다
            int alive = 0;
            for (int i = 0; i < Pieces.Count; i++)
                if (Pieces[i].Alive && Pieces[i].type != null && !Pieces[i].type.IsRobot) alive++;
            if (alive == 0) return null;

            int pick = RandInt(0, alive);
            for (int i = 0; i < Pieces.Count; i++)
            {
                var p = Pieces[i];
                if (!p.Alive || p.type == null || p.type.IsRobot) continue;
                if (pick-- <= 0) return p;
            }
            return null;
        }

        int RandInt(int a, int b) => a + rng.Next(Mathf.Max(1, b - a));

        float RandRange(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------- 소멸

        /// <summary>배에서 한참 멀어진 것은 사라진다. 맵 밖으로도 안 나간다.</summary>
        void CullEscaped()
        {
            Vector2 center = target != null ? (Vector2)target.position : Vector2.zero;
            float cull2 = cullRadius * cullRadius;

            for (int i = 0; i < Pieces.Count; i++)
            {
                var p = Pieces[i];
                if (!p.Alive) continue;

                var pos = (Vector2)p.transform.position;

                // 맵 경계에 가둔다
                pos.x = Mathf.Clamp(pos.x, -MapHalf.x, MapHalf.x);
                pos.y = Mathf.Clamp(pos.y, -MapHalf.y, MapHalf.y);
                p.transform.position = pos;

                // 🔴 **rev.9: 밭은 사라지지 않는다.**
                //
                //    예전엔 배에서 36유닛만 멀어져도 지웠다 (뱀서식 — 화면 밖은 없는 셈).
                //    그런데 쓰레기가 **찾아가서 캐는 밭**이 된 지금 그건 치명적이다:
                //    밭을 떠나면 증발하고 배 근처에 다시 생겨서,
                //    결국 **"쓰레기가 나를 따라다닌다"**로 보인다.
                //    맵을 넓게 만든 이유 자체가 사라진다.
                //
                //    이제 맵 안에 있는 한 그대로 둔다. 개수는 `AliveCap`이 잡는다.
                //    쫓아오는 로봇만, 너무 멀어지면 포기한 것으로 보고 지운다.
                bool isHunter = p.type != null && p.type.IsRobot;
                if (!isHunter) continue;
                if ((pos - center).sqrMagnitude <= cull2 * 4f) continue;

                p.Despawn();
                AliveCount--;
                EscapedTotal++;
            }
        }

        public void NotifyCollected() => AliveCount--;

        // ---------------------------------------------------------------- 파편

        public readonly List<Fragment> Fragments = new List<Fragment>();
        public readonly List<PickupItem> Pickups = new List<PickupItem>();

        /// <summary>🔴 아이템 드랍 확률(부순 것 하나당). RunDirector가 설정에서 넣어 준다.</summary>
        public float itemDropChance = 0.02f;

        /// <summary>재화 드랍 확률 배수 (테크트리의 '발견' 노드).</summary>
        public float scrapFind = 1f, circuitFind = 1f, coreFind = 1f;

        /// <summary>
        /// 🔴 웨이브가 오를수록 쓰레기가 단단해진다.
        ///    무기는 레벨업으로 계속 세지는데 쓰레기 HP는 고정이라
        ///    후반엔 **뭐든 한 방에 죽어서** 부수는 손맛이 사라진다.
        ///    (2026-08-22 플레이 피드백: *"쓰레기들이 뭐 다 한방이면 죽어"*)
        ///
        ///    단, 잡몹(HP 1)은 배수를 받아도 여전히 잘 터진다 —
        ///    "파바바박"의 주력이라 그건 유지되어야 한다.
        /// </summary>
        public float hpMul = 1f;

        /// <summary>이번 런에서 주운 재화 — 결과 화면에서 보여주고 저장한다.</summary>
        public readonly int[] MatsThisRun = new int[Mats.Count];

        /// <summary>
        /// 🔴 **격침 잔해.** 싣고 있던 화물을 그 자리에 흩뿌린다.
        ///
        ///    설계 문서(rev.6 §위험과 대응)에서 정한 대응이다 —
        ///    죽을 때 전부 잃으면 "많이 싣는다"는 선택지가 죽고,
        ///    그러면 무게 저울질이라는 이 게임의 세 번째 결정 자체가 사라진다.
        ///    잃는 게 아니라 **떨어뜨리는** 것이어야 되찾으러 가는 판단이 생긴다.
        ///
        ///    되찾으려면 격침당한 자리로 다시 가야 한다 — 위험했던 곳으로.
        /// </summary>
        /// <returns>실제로 뿌린 개수 (풀이 모자라면 더 적을 수 있다)</returns>
        public int SpillCargo(Vector2 at, int count, int perValue)
        {
            count = Mathf.Clamp(count, 0, 120);
            if (count <= 0) return 0;

            EnsureFragments(count);
            int made = 0;

            for (int i = 0; i < count; i++)
            {
                var f = FreeFragment();
                if (f == null) break;

                float ang = (i / (float)count) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

                // 넓게 흩뿌린다 — 한 점에 뭉치면 되찾기가 그냥 한 번 지나가기가 된다
                f.Spawn(at + dir * (0.6f + (i % 5) * 0.35f), dir * (3.5f + (i % 4)), Mathf.Max(1, perValue));

                // 부활 5초 + 사고 지점까지 돌아가는 시간을 버텨야 한다
                f.ExtendLife(40f);
                made++;
            }

            return made;
        }

        /// <summary>🔴 다 깎인 쓰레기가 파편으로 흩어진다. 파편만이 실제 수집 대상이다.</summary>
        public void BreakJunk(JunkPiece j)
        {
            BrokenTotal++;
            bool wasAnchor = j.type != null && j.type.isAnchor;
            var t = j.type;
            int count = Mathf.Max(1, t.fragments);
            int per = t.fragmentValue > 0 ? t.fragmentValue : Mathf.Max(1, Mathf.RoundToInt(t.value / (float)count));

            EnsureFragments(count);
            Vector3 center = j.transform.position;

            for (int i = 0; i < count; i++)
            {
                var f = FreeFragment();
                if (f == null) break;

                float ang = (i / (float)count) * Mathf.PI * 2f + (SpawnedTotal * 0.13f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                // 세게 터뜨린다 — 흩어졌다가 빨려오는 그림이 있어야 '부쉈다'가 느껴진다
                f.Spawn(center + (Vector3)(dir * 0.3f), dir * (6.5f + (i % 3)), per);
            }

            Juice.Break();

            // 🔴 아이템 — 파편이 '흐름'이라면 아이템은 '사건'이다. 드물어야 사건이 된다.
            //    위험물은 아무것도 안 남긴다 (닿으면 손해인 것에 보상을 붙이면 신호가 섞인다)
            if (!t.isHazard && rng.NextDouble() < itemDropChance) DropItem(center);
            if (!t.isHazard) RollMaterials(t, center);

            // 🔴 분열 — 부서지면 새끼가 튀어나온다. 연쇄로 터지는 맛이 여기서 나온다
            if (!string.IsNullOrEmpty(t.splitInto))
            {
                var child = FindType(t.splitInto);
                if (child != null)
                {
                    for (int i = 0; i < t.splitCount; i++)
                    {
                        var cp = FreePiece();
                        if (cp == null) break;

                        float a2 = (i / (float)Mathf.Max(1, t.splitCount)) * Mathf.PI * 2f;
                        var d2 = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2));
                        cp.Spawn(this, child, center + (Vector3)(d2 * 0.6f), d2 * child.driftSpeed, hpMul);
                        AliveCount++;
                        SpawnedTotal++;
                    }
                }
            }

            bool wasBossPart = j.IsBossPart;
            j.Despawn();
            AliveCount--;

            if (wasBossPart && director != null) director.OnBossPartBroken();
            if (wasAnchor) CountAnchor(j);
        }

        public RunDirector director;

        // ---- 도트 (PixelArt가 만들어 준다. 진짜 아트가 오면 여기만 갈아끼운다) ----
        public Sprite[] debrisSprites;
        public Sprite shardSprite;
        public Sprite crystalSprite;
        public Sprite ringSprite;

        Sprite DebrisFor(int index)
        {
            if (debrisSprites == null || debrisSprites.Length == 0) return sprite;
            return debrisSprites[Mathf.Abs(index) % debrisSprites.Length];
        }

        JunkType FindType(string name)
        {
            if (content.junk == null) return null;
            for (int i = 0; i < content.junk.Length; i++)
                if (content.junk[i].displayName == name) return content.junk[i];
            return null;
        }

        Fragment FreeFragment()
        {
            for (int i = 0; i < Fragments.Count; i++)
                if (!Fragments[i].Alive) return Fragments[i];
            return null;
        }

        void EnsureFragments(int need)
        {
            int free = 0;
            for (int i = 0; i < Fragments.Count; i++) if (!Fragments[i].Alive) free++;

            while (free < need)
            {
                var go = new GameObject("Fragment");
                go.transform.SetParent(transform);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = shardSprite != null ? shardSprite : sprite;
                sr.sortingOrder = 8;

                var f = go.AddComponent<Fragment>();
                f.Bind(sr);
                f.Despawn();
                Fragments.Add(f);
                free++;
            }
        }

        // ---------------------------------------------------------------- 보스 = HP 큰 쓰레기 여러 개

        /// <summary>보스를 부위 N개로 낸다. 별도 시스템이 아니라 HP 큰 JunkPiece 묶음이다.</summary>
        /// <summary>
        /// 보스 부위를 놓는다.
        ///
        /// 🔴 **배 주변에 놓는다.** 예전엔 월드 원점(0,0)에 놓았는데,
        ///    맵이 반경 52×34라 배가 멀리 있으면 **보스가 화면 밖에 생기고**
        ///    그 순간 일반 유입도 멈춰서 화면이 텅 빈다.
        ///    2026-08-22 플레이 피드백: *"보스가 안 나왔어. 진짜 그냥 안 생겼어."*
        ///
        /// 🔴 부위는 **가장 큰 쓰레기 종류**로 만든다. 잡몹 모양이면 보스로 안 보인다.
        /// </summary>
        /// <summary>
        /// 🔴 보스가 위험물을 토해낼 때 쓴다. 일반 스폰과 달리 **지정한 자리**에 놓는다.
        ///    위험물 종류가 없는 맵(1맵)이면 아무것도 안 한다 — 조용히 실패하는 게 맞다.
        /// </summary>
        // ---------------------------------------------------------------- 적 탄

        public readonly List<EnemyShot> Shots = new List<EnemyShot>();

        /// <summary>
        /// 🔴 저격기가 쏜다. 탄은 **배리어를 깎는다** — 쓰레기 충돌과 같은 규칙이라
        ///    플레이어가 새로 배울 게 없다.
        /// </summary>
        public void FireEnemyShot(JunkPiece from, Vector2 pos, Vector2 dir)
        {
            var shot = FreeShot();
            if (shot == null) return;

            var col = from != null && from.type != null
                ? from.type.color
                : new Color(1f, 0.5f, 0.4f);

            shot.Spawn(pos + dir * 0.8f, dir * EnemyShotSpeed, col);
            Juice.Chip(0.25f);
        }

        public const float EnemyShotSpeed = 13f;

        EnemyShot FreeShot()
        {
            for (int i = 0; i < Shots.Count; i++)
                if (!Shots[i].Alive) return Shots[i];

            if (Shots.Count >= 120) return null;      // 상한 — 화면이 탄으로 덮이면 안 된다

            var go = new GameObject("EnemyShot");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Glow(16);
            sr.sortingOrder = 8;
            go.transform.localScale = Vector3.one * 0.5f;

            var shot = go.AddComponent<EnemyShot>();
            shot.body = sr;
            shot.Despawn();
            Shots.Add(shot);
            return shot;
        }

        /// <summary>모든 탄을 지운다. 판이 새로 시작될 때.</summary>
        public void ClearShots()
        {
            for (int i = 0; i < Shots.Count; i++)
                if (Shots[i].Alive) Shots[i].Despawn();
        }

        // ---------------------------------------------------------------- 항행 (rev.11)

        /// <summary>판을 비운다 — 항행으로 들어갈 때. 밭은 두고 온다.</summary>
        public void ClearAllJunk()
        {
            for (int i = 0; i < Pieces.Count; i++)
                if (Pieces[i].Alive) { Pieces[i].Despawn(); }
            AliveCount = 0;
            ClearShots();
        }

        /// <summary>
        /// 🔴 **정면에서 밀려오는 잔해** (항행 국면).
        ///
        ///    기지가 우주를 가로지르니 앞에서 부딪혀 온다 — rev.7의 조류를 여기서 되살린다.
        ///    다만 그때와 달리 **상시가 아니라 항행 중에만** 온다.
        ///    조용한 구간(정박)이 있어야 이 구간이 무섭다.
        /// </summary>
        public void SpawnIncoming()
        {
            var t = PickIncomingType();
            var p = FreePiece();
            if (t == null || p == null) return;

            // 화면 위쪽(진행 방향)에서 기지 쪽으로 내려온다
            float x = RandRange(-MapHalf.x * 0.85f, MapHalf.x * 0.85f);
            var pos = new Vector2(x, MapHalf.y * 0.95f);

            Vector2 aim = BaseCenter + new Vector2(RandRange(-6f, 6f), 0f);
            Vector2 dir = (aim - pos).normalized;
            float speed = Mathf.Max(4f, t.driftSpeed) * RandRange(0.9f, 1.35f);

            p.Spawn(this, t, (Vector3)pos, dir * speed, hpMul);
            AliveCount++;
            SpawnedTotal++;
        }

        /// <summary>항행에 나오는 것 — 밭(캐는 것)이 아니라 **부딪혀 오는 것**이다.</summary>
        JunkType PickIncomingType()
        {
            var pick = PickFrom(normalPool);
            return pick;
        }

        public void SpawnHazardAt(Vector2 pos)
        {
            if (hazardPool.Count == 0) return;

            var p = FreePiece();
            if (p == null) return;

            var t = hazardPool[rng.Next(hazardPool.Count)];
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            var drift = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * t.driftSpeed;

            p.Spawn(this, t, pos, drift, hpMul);
            AliveCount++;
            SpawnedTotal++;
        }

        /// <summary>
        /// 🔴 보스 등장 전에 화면을 비운다. 남아 있던 쓰레기를 바깥으로 밀어낸다.
        ///    지우지 않고 **밀어내는** 이유: 갑자기 사라지면 버그처럼 보인다.
        /// </summary>
        /// <summary>보스가 자리 잡은 중심. HUD 화살표가 읽는다.</summary>
        public Vector2 BossCenter { get; private set; }

        /// <summary>🔴 기지 위치. 쓰레기가 여기로 몰려온다 (rev.7).</summary>
        public Vector2 BaseCenter = Vector2.zero;

        static Vector2 Rotate2(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// 기지에 닿아 사라지는 경우. **파편도 아이템도 안 남긴다** —
        /// 통과시킨 건 손해여야지 보상이 되면 안 된다.
        /// </summary>
        public void BreakJunkSilently(JunkPiece j)
        {
            if (j == null || !j.Alive) return;
            j.Despawn();
            AliveCount--;
        }

        public void PushAllOut(float speed)
        {
            if (target == null) return;
            Vector2 c = target.position;

            for (int i = 0; i < Pieces.Count; i++)
            {
                var p = Pieces[i];
                if (!p.Alive || p.IsBossPart) continue;

                Vector2 d = (Vector2)p.transform.position - c;
                if (d.sqrMagnitude < 0.0001f) d = Vector2.up;

                p.Flee(c, speed);
            }
        }

        public int SpawnBossParts(int parts, float hpScale)
        {
            if (normalPool.Count == 0) return 0;

            // 가장 무거운 종류를 고른다 — 보스는 커야 한다
            var t = normalPool[0];
            for (int i = 1; i < normalPool.Count; i++)
                if (normalPool[i].hp > t.hp) t = normalPool[i];

            // 🔴 보스는 **맵 한가운데**에 나타난다.
            //
            //    배 주변에 띄운 적이 있는데, 그건 "안 보인다"는 문제를 위치로 때운 것이었다.
            //    보스가 플레이어를 따라다니면 **장소로서의 무게가 없다** —
            //    보스는 찾아가는 것이지 따라오는 게 아니다.
            //    안 보이는 문제는 화면 밖 화살표로 따로 푼다 (`GameHud.DrawBossArrow`).
            //    (2026-08-22 피드백: *"가운데나 특정 위치에서 나오는 게 좋겠는데?"*)
            Vector2 c = Vector2.zero;
            BossCenter = c;

            float ring = 6f;

            int made = 0;
            for (int i = 0; i < parts; i++)
            {
                var p = FreePiece();
                if (p == null) break;

                float ang = (i / (float)parts) * Mathf.PI * 2f;
                var pos = (Vector3)(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ring);

                p.Spawn(this, t, pos, Vector2.zero, hpScale, true);
                AliveCount++;
                SpawnedTotal++;
                made++;
            }
            return made;
        }

        // ---------------------------------------------------------------- 보스가 쓰는 것들

        // (구 BossEntity용 헬퍼 BurstFragments/SpawnHazardAt/DevourAround 는 제거됨 —
        //  보스가 'HP 큰 JunkPiece 묶음'이 되면서 필요 없어졌다. 2026-08-20)

        /// <summary>
        /// 🔴 영구 재화. **티어가 높은 쓰레기일수록 좋은 게 나온다** —
        ///    "큰 걸 부수면 좋은 게 나온다"가 성립해야 위험한 것에 다가갈 이유가 생긴다.
        ///
        ///    확률은 아주 낮게 잡는다. 흔해지면 테크트리가 '시간'이 되고,
        ///    그러면 노드를 70개 만든 의미가 사라진다.
        /// </summary>
        void RollMaterials(JunkType t, Vector3 at)
        {
            // 티어 0 / 1 / 2 기준 확률
            // 🔴 2026-08-22 실측으로 올렸다. 맵 1에서 분당 고철 18.5개였는데
            //    테크트리 전체 완주에 216,000개가 필요해 **195시간**이 나왔다.
            //    수입과 비용이 두 자릿수 배로 어긋나 있었다.
            float pScrap   = (0.14f + t.tier * 0.10f) * scrapFind;
            float pCircuit = (t.tier >= 1 ? 0.030f + (t.tier - 1) * 0.030f : 0.005f) * circuitFind;

            // 🔴 코어는 맵 1(티어 0)에서 **한 개도 안 나온다.** 그건 의도다 —
            //    우주선 해금이 "깊은 맵까지 가라"는 뜻이어야 한다.
            //    다만 아예 0이면 벽이라, 티어 1에서도 아주 낮은 확률로 나오게 뒀다.
            float pCore    = (t.tier >= 2 ? 0.010f : t.tier >= 1 ? 0.0015f : 0f) * coreFind;

            TryMat(MatKind.Scrap,   pScrap,   1 + t.tier, at);
            TryMat(MatKind.Circuit, pCircuit, 1, at);
            TryMat(MatKind.Core,    pCore,    1, at);
        }

        void TryMat(MatKind m, float chance, int amount, Vector3 at)
        {
            if (chance <= 0f || rng.NextDouble() >= chance) return;

            MatsThisRun[(int)m] += amount;
            Meta.MetaSave.AddMaterial(m, amount);

            if (director != null)
                director.AddPopup(at, $"{Mats.Name(m)} +{amount}", Mats.ColorOf(m));
        }

        void DropItem(Vector3 center)
        {
            EnsurePickups(1);
            var it = FreePickup();
            if (it == null) return;

            // 연료가 더 자주 나온다 — 흡입은 판을 흔드는 것이라 희소해야 값어치가 있다
            var kind = rng.NextDouble() < 0.65 ? PickupKind.Fuel : PickupKind.Vacuum;

            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            it.Spawn(center, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f, kind);
        }

        /// <summary>
        /// 🔴 특성 '자기 날/회수 드론' — 부순 자리의 파편을 즉시 배 쪽으로 던진다.
        ///    파편을 바로 흡수해 버리면 "터졌다"는 그림이 사라지므로 **속도만 준다.**
        /// </summary>
        public void RushFragmentsNear(Vector2 at, float radius)
        {
            if (target == null) return;
            Vector2 shipPos = target.position;
            float r2 = radius * radius;

            for (int i = 0; i < Fragments.Count; i++)
            {
                var f = Fragments[i];
                if (!f.Alive) continue;
                if (((Vector2)f.transform.position - at).sqrMagnitude > r2) continue;
                f.Attract(shipPos, 90f);
            }
        }

        /// <summary>
        /// 🔴 '전체 흡수' — 맵의 모든 파편을 배 쪽으로 **날려 보낸다.**
        ///    즉시 흡수하지 않는다. 몇 초에 걸쳐 사방에서 쏟아져 들어오는 그림이
        ///    이 아이템의 값어치이기 때문이다.
        /// </summary>
        public int RushAllFragments()
        {
            if (target == null) return 0;
            Vector2 shipPos = target.position;

            int n = 0;
            for (int i = 0; i < Fragments.Count; i++)
            {
                var f = Fragments[i];
                if (!f.Alive) continue;

                f.RushTo(shipPos);
                n++;
            }
            return n;
        }

        PickupItem FreePickup()
        {
            for (int i = 0; i < Pickups.Count; i++)
                if (!Pickups[i].Alive) return Pickups[i];
            return null;
        }

        void EnsurePickups(int need)
        {
            int free = 0;
            for (int i = 0; i < Pickups.Count; i++) if (!Pickups[i].Alive) free++;

            while (free < need)
            {
                var go = new GameObject("Pickup");
                go.transform.SetParent(transform);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = crystalSprite != null ? crystalSprite : sprite;
                sr.sortingOrder = 10;   // 🔴 파편(8)보다 위. 절대 가려지면 안 된다

                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(go.transform, false);
                var ring = ringGo.AddComponent<SpriteRenderer>();
                ring.sprite = ringSprite != null ? ringSprite : sprite;
                ring.color = new Color(1f, 1f, 1f, 0.35f);
                ring.sortingOrder = 9;

                var it = go.AddComponent<PickupItem>();
                it.Bind(sr, ringGo.transform);
                it.Despawn();
                Pickups.Add(it);
                free++;
            }
        }

        void EnsurePool(int count)
        {
            while (Pieces.Count < count)
            {
                var go = new GameObject("Junk");
                go.transform.SetParent(transform);

                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(go.transform, false);
                var body = bodyGo.AddComponent<SpriteRenderer>();
                body.sprite = DebrisFor(Pieces.Count);
                body.sortingOrder = 5;

                var hlGo = new GameObject("Highlight");
                hlGo.transform.SetParent(go.transform, false);
                hlGo.transform.localScale = Vector3.one * 1.3f;
                var hl = hlGo.AddComponent<SpriteRenderer>();
                hl.sprite = DebrisFor(Pieces.Count);
                hl.color = new Color(1f, 0.25f, 0.22f, 0.5f);   // 🔴 빨간 테두리 = HP 바
                hl.sortingOrder = 4;

                var piece = go.AddComponent<JunkPiece>();
                piece.Bind(body, hlGo.transform);
                piece.Despawn();
                Pieces.Add(piece);
            }
        }
    }
}
