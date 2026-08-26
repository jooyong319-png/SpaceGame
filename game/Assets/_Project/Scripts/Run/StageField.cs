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

        /// <summary>
        /// 🔴 **맵 테두리 바깥 이만큼에서 생성된다** (rev.12 — 맵 = 화면 한 장).
        ///
        ///    예전에는 **배를 중심으로 한 원**에 뿌렸다. 맵이 화면보다 훨씬 넓을 때는
        ///    그게 맞았지만, 맵이 화면 한 장이 된 지금은 **틀린다** —
        ///    배가 구석에 있으면 원의 반대편이 **화면 안쪽에 떨어져서**
        ///    쓰레기가 눈앞에 뿅 하고 나타난다.
        ///    이제는 화면 테두리를 따라 뿌리므로 항상 **밖에서 들어온다.**
        /// </summary>
        public float spawnMargin = 3.5f;

        /// <summary>맵 테두리에서 이만큼 밖으로 나가면 사라진다.</summary>
        public float cullMargin = 14f;

        /// <summary>맵 경계 = 화면 한 장. RunDirector가 카메라에서 뽑아 넣는다.</summary>
        public Vector2 MapHalf { get; set; } = new Vector2(19f, 11f);

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

            // 🔴 **파편 풀도 여기서 한 번에 잡는다** (2026-08-26 · 결정론 44.6% 누수의 원인).
            //
            //    쓰레기 풀(`EnsurePool`)만 여기서 잡고 파편은 필요할 때마다 늘렸다.
            //    그래서 **첫 런은 풀 65개로 시작하고 두 번째 런은 199개로 시작했다.**
            //
            //    풀이 판 중간에 자라면 새 파편이 **목록 맨 뒤에** 붙는다.
            //    이미 큰 풀에서는 **중간 자리를 재사용**한다 — 즉 `Fragments`의 순서가 달라진다.
            //    `CollectByTouch`는 이 목록을 훑어 가장 가까운 것을 고르므로
            //    순서가 달라지면 **같은 자리에서 다른 것을 줍는다.**
            //    → 배가 다른 곳으로 가고, 결과가 통째로 갈린다.
            //
            //    ⚠️ 실측: 같은 빌드 두 번이 파편 56 vs 81 (44.6% 차이).
            //       세상은 완전히 같았다(쓰레기 159 동일) — **목록 순서 하나가 원인이었다.**
            for (int i = 0; i < Pieces.Count; i++) Pieces[i].Despawn();
            for (int i = 0; i < Fragments.Count; i++) Fragments[i].Despawn();
            for (int i = 0; i < Pickups.Count; i++) Pickups[i].Despawn();

            // ⚠️ **반드시 Despawn 뒤에 부른다.** `EnsureFragments`는 *비어 있는 것*을 세므로
            //    살아 있는 것이 남은 채로 부르면 그만큼 **또 만든다** — 풀이 런마다 계속 자란다.
            //    (처음에 Despawn 앞에 뒀다가 정확히 그 꼴이 났다: 264 → 320 → 449)
            //
            //    크기는 실측 최고치(약 320) 위로 넉넉히 잡는다. 여기서 한 번에 잡아 두면
            //    판이 도는 중에는 절대 안 자라고, **목록 순서가 런마다 똑같아진다.**
            fragCap = Mathf.Min(700, want * 2 + 64);
            EnsureFragments(fragCap);

            // 시작 화면이 비면 안 된다 — 미리 깔아둔다
            for (int i = 0; i < stage.initialFill; i++) SpawnInside();

            Spawning = true;
        }

        // ---------------------------------------------------------------- 계류 장치

        public void ResetDockClock() { }

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
            // 🔴 **로봇은 기본적으로 안 나온다** (2026-08-23 사장님:
            //    *"플레이어를 공격하는 것도 없애고, 플레이어는 무적이야"*).
            //
            //    `Tuning.HunterRatio` 기본값이 0이라 이 가지는 안 탄다.
            //    코드를 지우지 않은 이유: 로봇 4종은 만들어 둔 내용물이고,
            //    `K` 패널에서 비율을 올리면 **그 자리에서 되살아난다** —
            //    "위협이 있는 편이 나은가"를 다시 판단할 때 빌드를 다시 안 해도 된다.
            //    ⚠️ 지금 되살려도 **아프지는 않다.** 접촉 피해가 통째로 없다.
            if (Tuning.HunterRatio > 0.0001f && rng.NextDouble() < Tuning.HunterRatio)
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

            // 🔴 화면 안 아무 곳. 단 **배 코앞은 피한다** — 시작하자마자 맞으면 억울하다
            Vector2 c = target != null ? (Vector2)target.position : Vector2.zero;
            Vector2 pos2 = c;
            for (int tries = 0; tries < 8; tries++)
            {
                pos2 = new Vector2(RandRange(-MapHalf.x, MapHalf.x), RandRange(-MapHalf.y, MapHalf.y));
                if ((pos2 - c).sqrMagnitude > 8f * 8f) break;
            }
            var pos = (Vector3)pos2;

            float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
            var drift = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * JunkPiece.SpeedOf(t);

            p.Spawn(this, t, pos, drift, hpMul);
            AliveCount++;
            SpawnedTotal++;
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
        /// <summary>
        /// 🔴 **rev.12: 배 주변 화면 밖에서 배를 향해 밀려온다** (뱀서식).
        ///    rev.9~11의 "밭" 방식(`PickFieldSpot`/`SpawnCluster`)은 지웠다.
        ///    필요하면 `rev11-voyage` 브랜치에 그대로 있다.
        /// </summary>
        /// <summary>
        /// 🔴 **화면 테두리 바로 바깥의 한 점.** 네 변 중 하나를 고르고 그 위 아무 데나.
        ///    변을 먼저 고르므로 위·아래·좌·우가 **고르게** 나온다 —
        ///    각도로 고르면 화면이 가로로 길 때 좌우에서만 몰려온다.
        /// </summary>
        Vector2 EdgeSpot()
        {
            float mx = MapHalf.x + spawnMargin;
            float my = MapHalf.y + spawnMargin;

            switch (rng.Next(4))
            {
                case 0:  return new Vector2(RandRange(-mx, mx), my);    // 위
                case 1:  return new Vector2(RandRange(-mx, mx), -my);   // 아래
                case 2:  return new Vector2(-mx, RandRange(-my, my));   // 왼쪽
                default: return new Vector2(mx, RandRange(-my, my));    // 오른쪽
            }
        }

        void SpawnFromEdge()
        {
            var t = PickType();
            var p = FreePiece();
            if (t == null || p == null) return;

            Vector2 pos = EdgeSpot();

            // 🔴 **배를 겨누지 않는다** (2026-08-23 사장님:
            //    *"쓰레기가 플레이어를 따라다니지 않게"*).
            //
            //    쫓지 않을 거면 **던지는 것도 겨누면 안 된다.** 겨눠서 뿌리면
            //    쫓는 것과 그림이 똑같고, 다만 조준이 한 번뿐일 뿐이다.
            //    대신 **맵 반대편 어딘가**를 향해 던진다 — 화면을 가로질러 흘러간다.
            //
            //    ⚠️ 완전 무작위 방향이면 절반이 화면 밖으로 나가 버려서
            //       화면이 텅 빈다. 그래서 안쪽을 향하되 **넓게 흩는다.**
            Vector2 inward = (-pos).normalized;
            if (inward.sqrMagnitude < 0.01f) inward = Vector2.up;

            float spread = RandRange(-0.85f, 0.85f);        // ±약 49°
            float cs = Mathf.Cos(spread), sn = Mathf.Sin(spread);
            Vector2 dir = new Vector2(inward.x * cs - inward.y * sn,
                                      inward.x * sn + inward.y * cs);

            // 🔴 웨이브 배수(`spawnRateMul`)를 속도에 곱하지 않는다.
            //    후반에 40배가 되므로 그대로 곱하면 쓰레기가 **총알처럼 날아간다.**
            //    거세지는 것은 **양**이지 속도가 아니다 (양은 `spawnPerSecond`가 맡는다).
            float speed = JunkPiece.SpeedOf(t) * RandRange(0.75f, 1.35f);

            p.Spawn(this, t, (Vector3)pos, dir * speed, hpMul);
            AliveCount++;
            SpawnedTotal++;

            // 무리로 나오는 종류 — 뭉쳐서 같이 흘러간다
            for (int g = 1; g < t.groupSize && AliveCount < AliveCap; g++)
            {
                var extra = FreePiece();
                if (extra == null) break;

                var off = new Vector2(RandRange(-2.5f, 2.5f), RandRange(-2.5f, 2.5f));
                extra.Spawn(this, t, (Vector3)(pos + off), dir * speed * RandRange(0.9f, 1.1f), hpMul);
                AliveCount++;
                SpawnedTotal++;
            }
        }

        /// <summary>모선 주변 이 반경. 지금은 스모크 테스트가 "배를 어디 세울까"에 쓴다.</summary>
        public float BaseClearRadius = 8f;

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
            float cx = MapHalf.x + cullMargin;
            float cy = MapHalf.y + cullMargin;

            for (int i = 0; i < Pieces.Count; i++)
            {
                var p = Pieces[i];
                if (!p.Alive) continue;

                var pos = (Vector2)p.transform.position;

                // 🔴 **쓰레기는 맵 밖에서 들어온다.** 그래서 맵 경계로 가두면 안 된다 —
                //    가두면 테두리에 딱 붙어서 생기고, 밖에서 들어오는 그림이 사라진다.
                //    바깥 여유까지만 가두고, 그보다 멀면 지운다.
                if (pos.x < -cx || pos.x > cx || pos.y < -cy || pos.y > cy)
                {
                    p.Despawn();
                    AliveCount--;
                    EscapedTotal++;
                    continue;
                }
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
            var t = j.type;
            int count = Mathf.Max(1, t.fragments);
            int per = t.fragmentValue > 0 ? t.fragmentValue : Mathf.Max(1, Mathf.RoundToInt(t.value / (float)count));

            EnsureFragments(count + 2);
            Vector3 center = j.transform.position;

            // ⬜ 예전에는 여기서 **값어치만 있는 파편**을 뿌렸다 (흡수하면 크레딧·경험치).
            //    2026-08-26부터 쓰레기가 내놓는 것은 **재화 덩어리**뿐이다 —
            //    무엇이 나왔는지가 색으로 보이고, 그걸 끌고 돌아와야 내 것이 된다.
            //    (`RollMaterials`가 아래에서 뿌린다)

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
                        cp.Spawn(this, child, center + (Vector3)(d2 * 0.6f), d2 * JunkPiece.SpeedOf(child), hpMul);
                        AliveCount++;
                        SpawnedTotal++;
                    }
                }
            }

            bool wasBossPart = j.IsBossPart;
            j.Despawn();
            AliveCount--;

            if (wasBossPart && director != null) director.OnBossPartBroken();
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

        // ---------------------------------------------------------------- 실루엣

        /// <summary>
        /// 🔴 **종류마다 실루엣이 다르다** (2026-08-26 사장님 지시).
        ///    전에는 전부 `Debris`(찌그러진 사각형)라 **무엇을 부수는지 안 읽혔다.**
        ///
        ///    ⚠️ 스프라이트는 **스폰할 때** 정한다. 풀을 만들 때 정하면
        ///       그 슬롯이 다른 종류로 재사용될 때 **전함이 위성 그림으로 나온다.**
        ///
        ///    한 범주에 세 벌씩 만들어 돌려 쓴다 — 전부 똑같으면 화면이 복사·붙여넣기로 보이고,
        ///    전부 다르면 텍스처가 종류 수만큼 쌓인다.
        /// </summary>
        Sprite ShapeSprite(JunkShape shape, int variant)
        {
            if (shapeSets == null) shapeSets = new Sprite[5][];

            int si = (int)shape;
            if (si < 0 || si >= shapeSets.Length) si = (int)JunkShape.Debris;

            if (shapeSets[si] == null)
            {
                var set = new Sprite[3];
                for (int i = 0; i < set.Length; i++)
                {
                    int seed = 2200 + si * 91 + i * 17;
                    switch ((JunkShape)si)
                    {
                        case JunkShape.Satellite: set[i] = PixelArt.Satellite(18, seed); break;
                        case JunkShape.Vessel:    set[i] = PixelArt.Vessel(20, seed);    break;
                        case JunkShape.Warship:   set[i] = PixelArt.Warship(24, seed);   break;
                        case JunkShape.Hulk:      set[i] = PixelArt.Hulk(28, seed);      break;
                        default:                  set[i] = PixelArt.Debris(16, seed, 0.3f); break;
                    }
                }
                shapeSets[si] = set;
            }

            var arr = shapeSets[si];
            return arr[Mathf.Abs(variant) % arr.Length];
        }

        Sprite[][] shapeSets;

        /// <summary>이 종류가 지금 쓸 스프라이트. `JunkPiece.Spawn`이 부른다.</summary>
        public Sprite SpriteFor(JunkType t, int variant)
        {
            if (t == null) return sprite;
            return ShapeSprite(t.shape, variant);
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

        /// <summary>`Build`에서 한 번에 잡아 두는 파편 풀 크기. 판이 도는 중에는 여길 못 넘는다.</summary>
        int fragCap = 700;

        void EnsureFragments(int need)
        {
            int free = 0;
            for (int i = 0; i < Fragments.Count; i++) if (!Fragments[i].Alive) free++;

            // 🔴 **판이 도는 중에는 풀을 안 늘린다.**
            //    늘리면 새 파편이 목록 **맨 뒤**에 붙어 순서가 달라지고,
            //    `CollectByTouch`가 다른 것을 줍는다 — 결정론 44.6% 누수의 원인이었다.
            //    모자라면 그 파편은 안 나온다. 풀을 `Build`에서 실측 최고치의 두 배로
            //    잡아 두므로 실제로 모자랄 일은 없다.
            while (free < need && Fragments.Count < fragCap)
            {
                var go = new GameObject("Fragment");
                go.transform.SetParent(transform);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = shardSprite != null ? shardSprite : sprite;
                sr.sortingOrder = 8;

                // 🔴 **수집 후보 테두리.** 어느 것이 주워질지 미리 보여준다 (2026-08-26)
                var ringGo = new GameObject("Mark");
                ringGo.transform.SetParent(go.transform, false);
                var ringSr = ringGo.AddComponent<SpriteRenderer>();
                ringSr.sprite = ringSprite != null ? ringSprite : sprite;
                ringSr.sortingOrder = 7;          // 덩어리보다 뒤 — 가리면 무엇인지 안 보인다

                var f = go.AddComponent<Fragment>();
                f.Bind(sr);
                f.BindRing(ringSr);
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

        public void SpawnHazardAt(Vector2 pos)
        {
            if (hazardPool.Count == 0) return;

            var p = FreePiece();
            if (p == null) return;

            var t = hazardPool[rng.Next(hazardPool.Count)];
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            var drift = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * JunkPiece.SpeedOf(t);

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

            // 🔴 맵이 화면 한 장이 되면서 **고정값 6은 창 크기에 따라 어색해진다.**
            //    짧은 쪽의 절반쯤에 두면 어느 창 크기에서도 화면을 채우는 그림이 된다.
            //    모선(반경 4.5)을 **둘러싸는** 배치가 되어 그림도 맞는다.
            float ring = Mathf.Max(5.5f, Mathf.Min(MapHalf.x, MapHalf.y) * 0.55f);

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
            // 🔴 **고철은 반드시 나온다** (2026-08-26).
            //    확률로만 뿌리면 부수고도 아무것도 안 떨어지는 일이 잦고,
            //    그러면 "부쉈다"와 "벌었다"가 따로 논다 — 인크리멘탈에서 그건 치명적이다.
            //    큰 쓰레기일수록 여러 덩어리로 나온다.
            //    🔴 **덩어리 하나에 담기는 양은 `Mats.LumpOf`가 정한다** (2026-08-27).
            //       전에는 `(1 + tier)`라 1~2였고, 6칸을 꽉 채워도 한 판 수입이 6이었다.
            //       큰 쓰레기일수록 덩어리도 굵다 — 전함 하나가 위성 하나보다 값져야 한다.
            int lumps = Mathf.Clamp(1 + t.tier + (t.fragments >= 4 ? 1 : 0), 1, 3);
            int per = Mathf.Max(1, Mathf.RoundToInt(
                          Mats.LumpOf(MatKind.Scrap) * (1 + t.tier * 0.6f) * scrapFind));
            for (int i = 0; i < lumps; i++)
                DropMat(MatKind.Scrap, per, at);

            // 🔴 **구역마다 새 재화가 하나씩 열린다** (2026-08-26 · Space Rock Breaker 방향).
            //
            //    2구역=회로 · 3구역=코어 · 4구역=초합금 · 5구역=냉각결정 · 6구역=동위원소.
            //    구역을 여는 이유가 *"더 빨리 번다"*가 아니라
            //    **"여기서만 나오는 게 있다"**여야 한다 — 그래야 깊이가 곧 진행이 된다.
            //
            //    🔴 **바로 앞 구역 것이 제일 흔하다.** 그 구역의 대표 재화가 잘 나와야
            //       "여기 온 보람"이 있고, 더 깊은 것은 아직 안 나오므로 다음 구역이 궁금해진다.
            int rank = Stage != null ? Stage.rank : 1;

            for (int i = 1; i < Mats.Count; i++)
            {
                var m = (MatKind)i;
                int need = Mats.FirstRank(m);          // 이 재화가 처음 나오는 구역 등급
                if (rank < need) continue;             // 아직 이 구역엔 없다

                // 이 구역에서 얼마나 익었나 — 갓 열린 것은 드물고, 지나온 것은 흔해진다
                int depth = rank - need;
                float chance = (0.012f + depth * 0.022f) * (1f + t.tier * 0.6f);

                // 🔴 **희귀 재화가 더 자주** (테크트리 `RareMatChance`) — 회로 이상에만 붙는다
                var st2 = director != null ? director.Stats : null;
                if (st2 != null) chance *= Mathf.Max(0.1f, st2.rareMatChance);

                // 🔴 보스 부위에서 나오는 것은 더 많다 (테크트리 `BossMatBonus`)
                if (t.isAnchor && st2 != null) chance *= 1f + st2.bossMatBonus;

                chance *= m == MatKind.Circuit ? circuitFind
                        : m == MatKind.Core    ? coreFind
                                               : 1f;

                // 깊은 재화도 덩어리로 나온다 — 다만 자릿수가 달라 개수는 훨씬 적다
                TryMat(m, chance, Mathf.Max(1, Mathf.RoundToInt(
                           Mats.LumpOf(m) * (1 + t.tier * 0.4f))), at);
            }
        }

        /// <summary>
        /// 🔴 **재화는 이제 떨어진다** (2026-08-26).
        ///    예전에는 여기서 저장에 숫자를 바로 더했다 — 눈에 안 보이고, 고를 수도 없었다.
        ///    이제 덩어리를 하나 뿌린다. 주워서 **끌고 돌아와야** 내 것이 된다.
        /// </summary>
        void TryMat(MatKind m, float chance, int amount, Vector3 at)
        {
            if (chance <= 0f || rng.NextDouble() >= chance) return;
            DropMat(m, amount, at);
        }

        /// <summary>재화 덩어리 하나를 그 자리에 떨어뜨린다.</summary>
        void DropMat(MatKind m, int amount, Vector3 at)
        {
            var st = director != null ? director.Stats : null;

            // 🔴 **두 배로 나올 확률** (테크트리 `MatDoubleChance`).
            //    개수를 그냥 올리는 것과 다른 점: **가끔** 두 배가 되므로
            //    부술 때마다 "이번엔?"이 생긴다. 평균만 같으면 아무 느낌이 없다
            if (st != null && st.matDoubleChance > 0f && rng.NextDouble() < st.matDoubleChance)
                amount *= 2;

            var f = FreeFragment();
            if (f == null) return;

            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

            // 값어치는 종류가 정한다 (`Mats.WorthOf`) — 한 곳에서만 답하게 모아 뒀다
            f.Spawn(at + (Vector3)(dir * 0.4f), dir * (4.5f + (float)rng.NextDouble() * 2f),
                    Mats.WorthOf(m), m, amount);

            // 🔴 **덩어리가 더 오래 남는다** (테크트리 `LumpLife`).
            //    자석이 없어 직접 가서 밟아야 하므로, 수명이 곧 "고를 여유"다
            if (st != null && st.lumpLife > 0f) f.ExtendLife(45f + st.lumpLife);

            // ⬜ 팝업은 안 띄운다. 떨어진 덩어리 자체가 눈에 보이므로 글씨는 소음이다
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
