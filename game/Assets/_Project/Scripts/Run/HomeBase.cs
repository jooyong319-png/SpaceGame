using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 모선(기지).
    ///
    /// 🔴 **rev.8 — 이기는 방법이 바뀌었다.**
    ///
    ///    사용자 결정 (2026-08-21):
    ///    *"기지의 체력을 없애고, 기지에 들어가서 스페이스바를 누르면 게이지를 채워서
    ///      다 채우면 게임이 끝나게 해줘."*
    ///
    ///    rev.7은 *"기지가 안 부서지게 지켜라"*였다. 지금은 *"기지를 가동시켜라"*다.
    ///
    /// 🔴 무엇이 좋아지나:
    ///
    ///    · **수동 방어에서 능동 목표로.** 버티는 게 아니라 **해내는 것**이 목표가 된다.
    ///      버티기는 아무것도 안 해도 시간이 흐르지만, 가동은 내가 직접 붙어 있어야 찬다
    ///    · **가장 위험한 순간을 내가 고른다.** 채우는 동안 배는 그 자리에 묶인다 —
    ///      언제 붙을지가 결정이 되고, 그 결정을 매번 다시 한다
    ///    · **끝이 보인다.** 남은 초가 화면에 뜨므로 "얼마나 더 해야 하나"가 항상 읽힌다
    ///
    /// ⚠️ 대신 **지는 조건이 없어졌다.** 기지가 안 부서지고 배는 부활하므로,
    ///    지금은 실패할 방법이 없다. 이건 사장님 판단이 필요한 지점이다 —
    ///    `wiki/worklog-auto.md` #17에 적어 뒀다.
    /// </summary>
    public class HomeBase : MonoBehaviour
    {
        public RunDirector director;
        public StageField field;

        public SpriteRenderer body;
        public Transform shieldRing;

        /// <summary>
        /// 🔴 **태양광 어레이 3개** (rev.12 도입 연출).
        ///    처음엔 붙어 있다가 도입부에서 하나씩 뜯겨 나간다.
        ///    이게 이 게임의 전제를 **글이 아니라 그림으로** 설명한다 —
        ///    *"연료 수단이 전멸했다"*를 눈으로 보고 시작하는 것과 읽고 시작하는 건 다르다.
        /// </summary>
        public Transform[] arrays = new Transform[0];

        void UpdateArrays()
        {
            if (arrays == null || arrays.Length == 0) return;

            int lost = director != null ? director.ArraysLost : 3;

            for (int i = 0; i < arrays.Length; i++)
            {
                if (arrays[i] == null) continue;
                bool alive = i >= lost;
                if (arrays[i].gameObject.activeSelf != alive)
                    arrays[i].gameObject.SetActive(alive);
            }
        }

        /// <summary>기지 반경. 이 안에서 스페이스바를 누르면 가동이 진행된다.</summary>
        public float Radius = 4.5f;

        // ---------------------------------------------------------------- 기지 연료 (= HP)

        /// <summary>
        /// 🔴 **기지 연료.** rev.9에서 이 게임의 심장이 됐다
        ///    (2026-08-21: *"기지의 연료(HP)가 쓰레기인 거야"*).
        ///
        ///    · **계속 닳는다.** 아무것도 안 해도 줄어든다 — 맵마다 속도가 다르다
        ///    · **쓰레기를 먹여야 찬다.** 주워 온 화물을 입금하면 그만큼 회복된다
        ///    · **0이 되면 패배**
        ///
        /// 🔴 이 하나가 앞선 판본들의 가장 큰 구멍을 메운다:
        ///    지금까지 "왜 굳이 주워야 하나"에 대한 답이 **레벨업뿐**이었다.
        ///    레벨업은 안 해도 그만이라 결국 안 주워도 되는 게임이었다.
        ///    이제 **안 주우면 진다.** 수집이 곧 생존이다.
        /// </summary>
        public float Fuel { get; private set; }
        public float FuelMax { get; private set; } = 1000f;
        public float FuelRatio => FuelMax <= 0f ? 0f : Mathf.Clamp01(Fuel / FuelMax);
        public bool Destroyed => Fuel <= 0.001f;

        /// <summary>초당 감소량. 맵이 정한다.</summary>
        public float DrainPerSecond { get; private set; } = 6f;

        /// <summary>런 시작 · 새 지역 도착 시 연료를 채우고 감소율을 정한다.</summary>
        public void Begin(float fuelMax, float drainPerSecond)
        {
            FuelMax = Mathf.Max(1f, fuelMax);
            Fuel = FuelMax;
            DrainPerSecond = Mathf.Max(0f, drainPerSecond);
            warnClock = 0f;
            hitFlash = 0f;
        }

        /// <summary>쓰레기를 먹인다. 입금할 때 불린다.</summary>
        public void Refuel(float amount)
        {
            if (Destroyed || amount <= 0f) return;
            Fuel = Mathf.Min(FuelMax, Fuel + amount);
        }

        /// <summary>여비를 낸다 — 다음 지역으로 떠날 때.</summary>
        public void Spend(float amount)
        {
            if (amount <= 0f) return;
            Fuel = Mathf.Max(0f, Fuel - amount);
        }

        /// <summary>새 지역의 감소율로 갈아탄다. 난이도는 여기서 오른다.</summary>
        public void Retune(float drainPerSecond)
        {
            DrainPerSecond = Mathf.Max(0f, drainPerSecond);
            warnClock = 0f;
        }

        /// <summary>계류 장치 때문에 감소가 몇 배가 됐나. HUD가 그대로 보여준다.</summary>
        public float DrainMul =>
            field != null && field.AnchorsAlive > 0 ? 1f + field.AnchorsAlive * 0.4f : 1f;

        void UpdateDrain()
        {
            if (Destroyed) return;

            // 🔴 **계류 장치가 기지를 빨아먹는다.** 하나당 기본의 +40%.
            //    닻을 하나 부술 때마다 **숨통이 트인다** — 지는 중에도 나아지는 게 보여야
            //    끝까지 해볼 마음이 생긴다.
            // 🔴 **rev.11: 정박 중에는 거의 안 닳는다.**
            //
            //    이야기가 그렇게 말한다 — 연료는 **가는 데** 쓰는 것이다.
            //    가만히 서 있는데 연료가 새는 건 이야기가 설명하지 못하고,
            //    설명 안 되는 압박은 플레이어에게 **부당하게** 느껴진다.
            //
            //    정박 중 감소는 생명유지분(10%)만 남긴다. 진짜 소모는 항행에서 일어난다:
            //    출발 여비(`travelFuelCost`)와 **못 막은 잔해**가 연료를 먹는다.
            // 🔴 **항행 중에는 시간당 감소가 없다.**
            //
            //    한 구간의 연료값은 **출발 여비(`travelFuelCost`)**가 이미 받고 있다.
            //    거기에 시간당 감소까지 걸면 **같은 것(= 거리)에 두 번 청구**하는 셈이다.
            //
            //    2026-08-23 진단이 이걸 잡았다: 심연 구간은 여비 520 + 감소 427 = 947인데
            //    탱크가 1000이라 **포탑 10레벨이어도 잔해 몇 대면 표류**했다.
            //    실력과 무관하게 불가능한 구간이 있으면 그건 난이도가 아니라 **결함**이다.
            //
            //    이제 항행 중 손실은 **못 막은 잔해**에서만 나온다 —
            //    즉 잃는 만큼이 곧 **내가 못 막은 양**이다. 그게 이 국면의 실력이다.
            float rate = director.Travelling ? 0f : DrainPerSecond * DockedDrainRatio;
            Fuel = Mathf.Max(0f, Fuel - rate * DrainMul * Time.deltaTime);

            // 낮아지면 경고. 30% 아래에서만 울려서 **경고가 배경이 되지 않게** 한다
            if (FuelRatio < 0.3f)
            {
                warnClock -= Time.deltaTime;
                if (warnClock <= 0f)
                {
                    warnClock = Mathf.Lerp(0.6f, 2.0f, FuelRatio / 0.3f);
                    Juice.BaseAlarm();
                }
            }

            if (Destroyed) director.OnBaseDrained();
        }

        float warnClock;

        /// <summary>정박 중 감소 비율. 생명유지만 돌린다.</summary>
        const float DockedDrainRatio = 0.10f;

        float hitFlash;

        // ---------------------------------------------------------------- 포탑

        /// <summary>
        /// 🔴 **기지가 스스로 싸운다** (2026-08-21 요청:
        ///    *"기지도 공격 기능 있게 만들어주고 레벨업 보상으로 기지 무기 강화"*).
        ///
        ///    rev.7에서 지는 조건은 기지 상실인데, 정작 기지는 **아무것도 안 하고 맞기만 했다.**
        ///    스스로 싸우면 "지킨다"가 혼자 짊어지는 짐에서 **같이 싸우는 것**으로 바뀐다.
        ///    내가 자리를 비워도 기지가 얼마간 버텨 주므로 **멀리 나가는 선택에 값이 붙는다** —
        ///    그게 rev.7의 저울(더 모을까 / 지금 돌아갈까)에 무게를 더한다.
        ///
        ///    🔴 다만 **처음부터 쏘지는 않는다.** 레벨업 카드로 얻어야 생긴다.
        ///       처음부터 있으면 초반이 쉬워지고 긴장이 사라진다 — 보상이어야 한다.
        /// </summary>
        void FireTurret()
        {
            var st = director.Stats;
            if (st == null) return;

            // 🔴 항행 중에는 포탑이 없어도 **최소 1레벨**로 쏜다.
            //    아무것도 못 쏘면 플레이어가 할 수 있는 게 없고,
            //    그건 난이도가 아니라 **조작 불능**이다. 강화는 그 위에 얹는 것이어야 한다.
            int baseLv = st.baseTurretLevel;
            if (director.Travelling) baseLv = Mathf.Max(1, baseLv);
            if (baseLv <= 0) return;

            turretCd -= Time.deltaTime;
            if (turretCd > 0f) return;

            int lv = baseLv;
            float cd = Mathf.Max(0.12f, (0.85f - lv * 0.045f) * st.baseTurretHaste);
            turretCd = cd;

            float range = (9f + lv * 1.1f) * st.baseTurretRange;
            float dmg   = (14f + lv * 9f) * st.baseTurretPower * Tuning.TurretPowerMul;
            int barrels = Mathf.Max(1, st.baseTurretCount);

            float r2 = range * range;
            int fired = 0;

            // 🔴 **항행 중에는 플레이어가 조준한다** (rev.11).
            //
            //    정박 중엔 기지가 알아서 쏘지만(플레이어는 밖에 나가 있다),
            //    항행 중엔 **커서 쪽을 우선해서** 쏜다.
            //    그래야 "내가 지키고 있다"가 되고, 강화한 무기가 내 손에 들린 것이 된다.
            //    ⚠️ 항행 중에는 **우주선이 꺼져 있어** `ship.AimPoint`가 갱신되지 않는다.
            //       그래서 기지가 커서를 **직접** 읽는다.
            bool aimed = director.Travelling;
            Vector2 aimAt = aimed ? AimFromCursor() : (Vector2)transform.position;

            for (int shot = 0; shot < barrels; shot++)
            {
                JunkPiece best = null;
                float bestSq = float.MaxValue;

                for (int i = 0; i < field.Pieces.Count; i++)
                {
                    var p = field.Pieces[i];
                    if (!p.Alive || p.IsBossPart || p.TurretMark) continue;

                    Vector2 spot = p.transform.position;
                    if ((spot - (Vector2)transform.position).sqrMagnitude > r2) continue;

                    // 조준 중이면 커서에 가까운 것, 아니면 기지에 가까운 것
                    float sq = aimed
                        ? (spot - aimAt).sqrMagnitude
                        : (spot - (Vector2)transform.position).sqrMagnitude;

                    if (sq >= bestSq) continue;
                    bestSq = sq; best = p;
                }

                if (best == null) break;

                best.TurretMark = true;          // 같은 발사에서 두 번 겨누지 않는다
                Vector2 at = best.transform.position;

                Fx.Line(transform.position, at, new Color(0.55f, 0.95f, 1f), 0.10f, 0.12f);
                Fx.Spark(at, 0.35f, new Color(0.7f, 1f, 1f), 0.14f);

                // Chip()이 다 깎이면 스스로 BreakJunk까지 한다 — 여기서 또 부르면 두 번 터진다
                best.Chip(dmg);
                fired++;
            }

            for (int i = 0; i < field.Pieces.Count; i++) field.Pieces[i].TurretMark = false;

            if (fired > 0) Juice.Chip(0.35f);
        }

        float turretCd;

        /// <summary>항행 중 조준점. 커서의 월드 좌표.</summary>
        public Vector2 AimFromCursor()
        {
            var cam = director != null ? director.cam : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return transform.position;

            var follow = cam.GetComponent<CameraFollow>();
            return Core.InputReader.WorldMouse(cam, transform.position.z,
                                               follow != null ? (Vector3?)follow.BasePosition : null);
        }

        void Update()
        {
            if (director == null || field == null) return;
            if (!director.FieldActive || RunDirector.WorldPaused) return;

            // 🔴 도입 연출 중에는 연료도 안 닳고 포탑도 안 쏜다 — 보는 시간이다.
            //    (어레이 표시만 `UpdateVisual`에서 계속 갱신된다)
            if (director.InIntro) { UpdateVisual(); return; }

            UpdateDrain();
            if (Destroyed) return;

            FireTurret();
            if (director.Travelling) TakeIncomingHits();

            if (hitFlash > 0f) hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 2.5f);
            UpdateVisual();
        }

        /// <summary>
        /// 🔴 **항행 중 잔해가 기지에 부딪히면 연료를 잃는다 = 거리를 잃는다.**
        ///
        ///    지표를 연료 하나로 둔 이유: 연료 바 하나만 보면
        ///    *"이대로 가면 도착하나"*가 읽힌다. 기지 HP를 따로 두면
        ///    **둘을 봐야 하고, 그러면 둘 다 안 보게 된다.**
        /// </summary>
        void TakeIncomingHits()
        {
            float r2 = Radius * Radius;
            float total = 0f;

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive || p.IsBossPart) continue;
                if (p.type != null && p.type.isAnchor) continue;
                if (((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude > r2) continue;

                total += (p.type != null ? p.type.contactDamage * director.Config.incomingFuelCost : 8f)
                       * Tuning.IncomingCostMul;
                field.BreakJunkSilently(p);
            }

            if (total <= 0f) return;

            Fuel = Mathf.Max(0f, Fuel - total);
            hitFlash = 1f;

            hitClock -= Time.deltaTime;
            if (hitClock <= 0f)
            {
                hitClock = 0.4f;
                director.AddPopup(transform.position, $"충돌 -{total:0}", new Color(1f, 0.45f, 0.35f));
                Juice.BaseAlarm();
            }

            if (Destroyed) director.OnBaseDrained();
        }

        float hitClock;

        void UpdateVisual()
        {
            UpdateArrays();

            if (body != null)
            {
                // 🔴 밝기는 **연료**다. 꺼져 가는 기지가 눈에 보여야 한다
                float t = 0.25f + 0.75f * FuelRatio;
                var c = new Color(0.55f * t, 0.75f * t, 0.95f * t);
                body.color = Color.Lerp(c, Color.white, hitFlash * 0.7f);
            }

            if (shieldRing != null)
            {
                // 🔴 고리가 **줄어든다.** 남은 연료다 — 멀리서도 기지 상태가 읽힌다
                float scale = Radius * 2f * (0.55f + 0.45f * FuelRatio);
                shieldRing.localScale = new Vector3(scale, scale, 1f);

                var sr = shieldRing.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    // 연료가 낮으면 붉게 맥동한다 — 위험이 화면에서 먼저 읽혀야 한다
                    sr.color = FuelRatio < 0.3f
                        ? new Color(1f, 0.4f, 0.35f, 0.28f + 0.22f * Mathf.Sin(Time.time * 9f))
                        : new Color(0.45f, 0.85f, 1f, 0.14f + 0.14f * FuelRatio);
                }
            }
        }
    }
}
