using UnityEngine;
using SalvageRun.Data;

namespace SalvageRun.Run
{
    /// <summary>
    /// 떠다니는 쓰레기 한 조각.
    ///
    /// 🔴 이 게임의 동사는 "깎다"이다 (2026-08-20 확정).
    ///    · 쓰레기는 HP를 가진다. 팔이 원거리에서 깎는다
    ///    · HP가 0이 되면 **파편**이 터져나온다 — 그게 실제 수집 대상이다
    ///    · **배가 본체에 닿으면 연료를 잃는다** → 붙으면 빨리 깎이지만 위험하다
    ///
    /// 보스도 같은 것이다. 부위 = HP가 큰 JunkPiece 몇 개를 한 덩어리로 배치한 것뿐이다.
    /// </summary>
    public class JunkPiece : MonoBehaviour
    {
        public JunkType type;
        public bool Alive { get; private set; }

        /// <summary>보스 부위인가. 부서지면 RunDirector가 센다.</summary>
        public bool IsBossPart { get; private set; }

        public float Hp { get; private set; }
        public float HpMax { get; private set; }
        public float HpRatio => HpMax <= 0f ? 0f : Mathf.Clamp01(Hp / HpMax);

        /// <summary>이번 프레임에 무기가 건드리고 있는가 — 시각 표시용.</summary>
        public bool BeingChipped { get; set; }

        /// <summary>🔴 돌진 직전 겨누는 중. 색으로 예고한다 — 신호 없는 돌진은 기습이다.</summary>
        public bool Winding { get; private set; }

        /// <summary>방전이 이번 발사에서 이미 때렸는지 — 같은 대상을 두 번 안 때리게.</summary>
        public bool ArcMark { get; set; }

        /// <summary>기지 포탑이 한 번의 사격에서 같은 목표를 두 번 겨누지 않도록 하는 표식.</summary>
        public bool TurretMark { get; set; }

        SpriteRenderer body;
        Transform highlight;
        SpriteRenderer hlSr;
        StageField field;
        Vector2 drift;
        float contactCooldown;
        float moveClock;      // 패턴용 자체 시계 (절대 시각을 쓰지 않는다 — 시뮬 재현성)
        float chargeTimer;

        public void Bind(SpriteRenderer body, Transform highlight)
        {
            this.body = body;
            this.highlight = highlight;
        }

        public void Spawn(StageField field, JunkType t, Vector3 pos, Vector2 drift, float hpScale = 1f, bool bossPart = false)
        {
            this.field = field;
            type = t;
            this.drift = drift;
            IsBossPart = bossPart;

            HpMax = Mathf.Max(1f, t.hp * hpScale);
            Hp = HpMax;
            Alive = true;
            contactCooldown = 0f;
            BeingChipped = false;
            slowFactor = 0f;

            // 🔴 새 행동들의 상태도 반드시 되감는다 — 풀에서 재사용되므로
            //    안 지우면 **깨어난 채로 스폰되는 매복기**가 생긴다
            shotClock = 0.6f;
            awake = false;
            slowLeft = 0f;
            fleeing = false;

            moveClock = 0f;
            chargeTimer = 0f;
            transform.position = pos;
            // 🔴 밭이 된 이상 **멀리서도 보여야** 찾아갈 마음이 생긴다 (2026-08-22 피드백)
            transform.localScale = Vector3.one * (t.size * Tuning.JunkSize * (bossPart ? 1.9f : 1f));
            if (body != null) body.color = t.color;

            if (highlight != null)
            {
                hlSr = highlight.GetComponent<SpriteRenderer>();
                highlight.gameObject.SetActive(true);
            }
            gameObject.SetActive(true);
        }

        public void Despawn()
        {
            Alive = false;
            BeingChipped = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 🔴 조합 능력이 쓰는 밀기/당기기. 이동 패턴을 덮어쓰지 않고 **속도에 더하기만** 한다 —
        ///    덮어쓰면 다음 프레임에 패턴이 되돌려서 아무 일도 안 일어난 것처럼 보인다.
        /// </summary>
        public void Tug(Vector2 toward, float power)
        {
            if (!Alive) return;

            // 🔴 끌어당기는 **목표점**이 NaN이면 drift가 통째로 오염된다.
            //    무기 쪽 좌표(폭발 중심 · 조준점)가 한 번만 망가져도 여기로 새어 든다 —
            //    입구에서 막는 게 원인을 쫓는 것보다 확실하다.
            if (!IsFinite(toward) || float.IsNaN(power) || float.IsInfinity(power)) return;

            Vector2 d = toward - (Vector2)transform.position;
            if (d.sqrMagnitude < 0.0001f) return;

            drift += d.normalized * power;

            // 🔴 **속도 상한.** 이게 없으면 끌어당기기가 매 프레임 속도를 더하기만 해서
            //    쓰레기가 보이지 않을 만큼 빨라진다. 특히 Drift 패턴은 방향 보정이 없어
            //    되돌아오지도 않는다 — 2026-08-22 플레이 피드백:
            //    "웨이브가 진행될수록 쓰레기 속도가 너무 빨라지는데 보이지도 않을 정도야"
            float max = MaxSpeed;
            if (drift.sqrMagnitude > max * max) drift = drift.normalized * max;
        }

        /// <summary>
        /// 🔴 보스 등장 연출 — 바깥으로 도망친다. 이동 패턴을 완전히 끈다.
        ///    안 끄면 다음 프레임에 다시 배 쪽으로 돌아와서 화면이 안 비워진다.
        /// </summary>
        public void Flee(Vector2 from, float speed)
        {
            if (!Alive) return;
            if (!IsFinite(from) || float.IsNaN(speed) || float.IsInfinity(speed)) return;

            Vector2 d = (Vector2)transform.position - from;
            if (d.sqrMagnitude < 0.0001f) d = Vector2.up;

            drift = d.normalized * speed;
            fleeing = true;
        }

        bool fleeing;

        /// <summary>저격기 사격 타이머 · 매복기 각성 여부. 스폰마다 초기화된다.</summary>
        float shotClock;
        bool awake;

        /// <summary>저격기가 유지하려는 거리.</summary>
        const float SniperKeep = 11f;

        /// <summary>매복기가 깨어나는 거리.</summary>
        const float AmbushRange = 7.5f;

        /// <summary>선회기가 돌려는 반경.</summary>
        const float CirclerRing = 6.5f;

        /// <summary>제 속도의 2.6배까지만. 돌진(Charger)이 2.4배라 그보다 조금 위에 둔다.</summary>
        float MaxSpeed => Mathf.Max(1f, type != null ? type.driftSpeed * 2.6f : 12f);

        static bool IsFinite(Vector2 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

        static bool nanReported;

        /// <summary>
        /// 무기가 깎는다. 다 깎이면 파편이 나온다.
        /// 🔴 **부쉈는지를 돌려준다** — "부순 순간에만" 터지는 특성들이 이걸 본다.
        /// </summary>
        public bool Chip(float amount)
        {
            if (!Alive || amount <= 0f) return false;

            Hp -= amount;
            BeingChipped = true;

            if (Hp > 0f) return false;
            field.BreakJunk(this);
            return true;
        }

        /// <summary>
        /// 🔴 감속. 겹쳐 걸리면 **더 센 쪽만** 남긴다 — 더하면 여러 무기가 붙는 순간
        ///    전부 정지해서 게임이 사라진다.
        /// </summary>
        public void Slow(float factor, float seconds)
        {
            if (!Alive || factor <= 0f) return;
            slowFactor = Mathf.Max(slowFactor, Mathf.Clamp01(factor));
            slowLeft = Mathf.Max(slowLeft, seconds);
        }

        float slowFactor, slowLeft;
        public float SpeedScale => slowLeft > 0f ? 1f - slowFactor : 1f;

        void Update()
        {
            if (!Alive || RunDirector.WorldPaused) return;

            moveClock += Time.deltaTime;
            if (slowLeft > 0f)
            {
                slowLeft -= Time.deltaTime;
                if (slowLeft <= 0f) slowFactor = 0f;
            }

            if (!fleeing && !IsBossPart && field != null) ApplyMovePattern();

            // 🔴 **NaN 방벽.** 2026-08-21 시뮬에서 실제로 터졌다:
            //    "transform.position assign attempt for 'Junk' is not valid. Input position is { NaN, NaN }".
            //
            //    drift를 쓰는 곳은 전부 상한이 걸려 있어서 코드만 읽어서는 원인이 안 보였다.
            //    추측으로 더 파는 대신 **막고, 어디서 났는지 이름을 남긴다.**
            //    막지 않으면 한 조각의 NaN이 유니티 에러 폭포가 되어 판 전체가 멎는다.
            if (!IsFinite(drift))
            {
                if (!nanReported)
                {
                    nanReported = true;
                    Debug.LogWarning($"[JunkPiece] drift가 NaN/무한이 됐다 — type={(type != null ? type.displayName : "null")} " +
                                     $"move={(type != null ? type.move.ToString() : "?")} pos={transform.position}");
                }
                drift = Vector2.zero;
                field?.BreakJunkSilently(this);
                return;
            }

            transform.position += (Vector3)(drift * SpeedScale * Time.deltaTime);
            if (contactCooldown > 0f) contactCooldown -= Time.deltaTime;

            UpdateHpBorder();

            if (body != null)
            {
                // 🔴 쓰레기는 **채도를 낮춰 잔해로 읽히게** 한다. 밝고 선명한 건 파편(재화)뿐이다.
                //    2026-08-21 피드백: "쓰레기인지 재화인지 구분이 안 간다".
                //    위험물은 예외 — 피해야 하는 것이니 선명하게 둔다.
                var c = type.color;

                // 🔴 **로봇은 채도를 안 낮춘다.** rev.10에서 화면에는 세 부류가 있다:
                //    캐는 것(쓰레기) · 죽이는 것(로봇) · 피하는 것(위험물).
                //    쓰레기만 흐리게 깔아서 **선명한 것 = 나를 해치는 것**이 되게 한다.
                //    그래야 눈이 위험을 먼저 잡는다.
                bool threat = type.isHazard || type.IsRobot;
                if (!threat)
                {
                    float grey = (c.r + c.g + c.b) / 3f;
                    c = Color.Lerp(new Color(grey, grey, grey), c, 0.45f);
                }
                float t = 0.42f + 0.58f * HpRatio;   // 깎일수록 어두워진다

                // 로봇은 맥동한다 — 정지한 밭 사이에서 **움직이는 것**이 먼저 눈에 띄어야 한다
                if (type.IsRobot)
                    t *= 0.85f + 0.15f * Mathf.Sin(moveClock * 6f);

                body.color = new Color(c.r * t, c.g * t, c.b * t);
            }

            BeingChipped = false;   // 팔이 매 프레임 다시 켠다
        }

        /// <summary>
        /// 🔴 **빨간 테두리가 곧 HP 바다.** 두꺼우면 멀쩡하고, 얇아지면 곧 터진다.
        ///    화면에 200개가 떠 있는 게임에서 개체마다 막대를 띄우면 화면이 UI로 덮인다 —
        ///    **윤곽선 자체를 게이지로 쓰면** 자리를 안 먹으면서 읽힌다.
        ///
        ///    빨강은 "이건 쓰레기다(닿으면 아프다)"라는 정체성도 같이 맡는다.
        ///    반짝이는 청록/금색/보라는 파편(재화)이고, 빨간 테두리가 있으면 쓰레기다.
        ///    무기가 건드리는 중이면 잠깐 하얗게 뜬다.
        /// </summary>
        void UpdateHpBorder()
        {
            if (hlSr == null || highlight == null) return;

            float r = HpRatio;

            // 테두리 두께 ∝ 남은 HP. 죽기 직전엔 본체에 달라붙는다.
            float pad = Mathf.Lerp(0.06f, 0.30f, r);
            highlight.localScale = Vector3.one * (1f + pad);

            if (BeingChipped)
            {
                hlSr.color = new Color(1f, 1f, 1f, 0.85f);      // 맞는 순간은 하얗게
                return;
            }

            // 🔴 돌진 예고 — 겨누는 동안 노랗게 번쩍인다. "온다"를 알려주는 유일한 신호다
            if (Winding)
            {
                float f = 0.55f + 0.45f * Mathf.Sin(moveClock * 26f);
                hlSr.color = new Color(1f, 0.9f, 0.25f, 0.55f + 0.45f * f);
                highlight.localScale = Vector3.one * 1.45f;
                return;
            }

            // 깎일수록 진해진다 — 다 깎인 걸 먼저 처리하게 유도한다
            float a = Mathf.Lerp(0.75f, 0.34f, r);
            hlSr.color = new Color(1f, 0.25f, 0.22f, a);
        }

        /// <summary>
        /// 🔴 **rev.7: 쓰레기는 쫓아오지 않는다. 떠다닌다.**
        ///
        ///    사용자 결정 (2026-08-22):
        ///    *"이제는 그냥 공중에 떠다니는 거야."*
        ///
        ///    **쫓아오는 건 몬스터고, 떠다니는 게 쓰레기다.**
        ///    이 게임이 뱀서로 읽힌 큰 이유 하나가 "적이 나를 향해 몰려온다"였다.
        ///
        ///    목표를 쫓는 조종을 전부 뺐다. 스폰될 때 받은 속도로 **관성으로만** 흘러간다.
        ///    다만 완전 무작위면 기지에 아무것도 안 닿아 위협이 사라지므로,
        ///    바깥에서 기지 쪽으로 흘러드는 **조류**로 뿌린다 (`StageField.SpawnFromEdge`).
        ///
        ///    `MoveKind`는 이제 "무엇을 쫓는가"가 아니라 **"어떻게 흘러가는가"**다.
        /// </summary>
        void ApplyMovePattern()
        {
            float speed = Mathf.Max(0.1f, type.driftSpeed);

            // 밭이 떠 있는 속도. 0이면 죽은 그림이라 아주 조금만 움직인다
            float idleSpeed = speed * 0.12f;

            switch (type.move)
            {
                case MoveKind.Drift:
                    // 밀리거나 끌린 뒤에는 다시 느린 표류로 가라앉는다
                    if (drift.sqrMagnitude > idleSpeed * idleSpeed * 1.02f)
                        drift = Vector2.Lerp(drift, drift.normalized * idleSpeed, 1.5f * Time.deltaTime);
                    break;

                case MoveKind.Zigzag:
                {
                    // 흘러가면서 좌우로 흔들린다 — 맞히기 까다롭다
                    Vector2 fwd = drift.sqrMagnitude > 0.01f ? drift.normalized : Vector2.right;
                    Vector2 perp = new Vector2(-fwd.y, fwd.x);
                    drift += perp * Mathf.Sin(moveClock * 5f) * type.movePower * speed * 1.6f * Time.deltaTime;
                    break;
                }

                case MoveKind.Charger:
                {
                    // 굴러가다 가끔 튕기듯 가속한다. 직전에 색으로 예고한다
                    chargeTimer -= Time.deltaTime;
                    if (chargeTimer <= 0f) chargeTimer = Mathf.Max(1.2f, 3.2f / Mathf.Max(0.1f, type.movePower));

                    bool burst = chargeTimer < 0.5f;
                    Winding = !burst && chargeTimer < 1.0f;

                    if (burst && drift.sqrMagnitude > 0.01f)
                        drift += drift.normalized * speed * 1.2f * Time.deltaTime;
                    break;
                }

                case MoveKind.Hunter:
                {
                    // 🔴 **파손 로봇.** 이것만 플레이어를 쫓는다 — rev.9에서 유일한 능동 위협이다.
                    //    쓰레기는 밭이고 이것이 지키는 개다.
                    if (field != null && field.target != null)
                    {
                        Vector2 toShip = (Vector2)field.target.position - (Vector2)transform.position;
                        if (toShip.sqrMagnitude > 0.01f)
                            drift = Vector2.Lerp(drift, toShip.normalized * speed,
                                                 Mathf.Max(0.4f, type.homing) * Time.deltaTime);
                    }
                    break;
                }

                case MoveKind.Sniper:
                {
                    // 🔴 **거리를 유지한다.** 멀면 다가오고, 붙으면 물러난다.
                    //    사거리 밖에서 쏘므로 플레이어는 **쫓아갈지 무시할지** 정해야 한다.
                    if (field != null && field.target != null)
                    {
                        Vector2 to = (Vector2)field.target.position - (Vector2)transform.position;
                        float d = to.magnitude;
                        float keep = SniperKeep;

                        if (d > 0.01f)
                        {
                            // 사거리 안쪽이면 후퇴, 바깥이면 접근. 딱 맞으면 옆으로 흐른다
                            float sign = d < keep * 0.8f ? -1f : (d > keep * 1.25f ? 1f : 0f);
                            Vector2 want = to.normalized * (speed * sign);

                            if (Mathf.Approximately(sign, 0f))
                            {
                                var perp = new Vector2(-to.y, to.x).normalized;
                                want = perp * speed * 0.5f;
                            }
                            drift = Vector2.Lerp(drift, want, 1.6f * Time.deltaTime);
                        }

                        // 쏜다 — 사거리 안이고 시야가 트였을 때만
                        shotClock -= Time.deltaTime;
                        if (d <= keep * 1.6f && shotClock <= 0f)
                        {
                            shotClock = Mathf.Max(0.9f, 2.4f / Mathf.Max(0.3f, type.movePower));
                            field.FireEnemyShot(this, (Vector2)transform.position, to.normalized);
                        }
                    }
                    break;
                }

                case MoveKind.Ambusher:
                {
                    // 🔴 **깨어나기 전에는 쓰레기처럼 멈춰 있다.** 그래서 밭에 들어갈 때마다
                    //    *"저게 진짜 쓰레기인가"*를 한 번 보게 된다.
                    if (field != null && field.target != null)
                    {
                        Vector2 to = (Vector2)field.target.position - (Vector2)transform.position;

                        if (!awake)
                        {
                            drift = Vector2.Lerp(drift, Vector2.zero, 3f * Time.deltaTime);
                            if (to.sqrMagnitude <= AmbushRange * AmbushRange)
                            {
                                awake = true;
                                Fx.Shockwave(transform.position, 1.6f, new Color(1f, 0.4f, 0.35f, 0.9f), 0.3f);
                                Juice.Chip(0.5f);
                            }
                            break;
                        }

                        // 깨어난 뒤에는 아주 빠르게 달려든다 — 매복의 값은 이 순간에 있다
                        if (to.sqrMagnitude > 0.01f)
                            drift = Vector2.Lerp(drift, to.normalized * speed * 1.5f,
                                                 2.2f * Time.deltaTime);
                    }
                    break;
                }

                case MoveKind.Circler:
                {
                    // 🔴 **정면으로 오지 않는다.** 맴돌며 서서히 조인다 —
                    //    한 놈만 무는 드릴에 특히 성가시다. 호위 무기를 고를 이유가 된다.
                    if (field != null && field.target != null)
                    {
                        Vector2 to = (Vector2)field.target.position - (Vector2)transform.position;
                        float d = to.magnitude;

                        if (d > 0.01f)
                        {
                            Vector2 inward = to / d;
                            Vector2 perp = new Vector2(-inward.y, inward.x);

                            // 멀면 파고들고, 가까우면 도는 성분이 커진다
                            float pull = Mathf.Clamp01((d - CirclerRing) / 8f);
                            Vector2 want = (inward * pull + perp * (1f - Mathf.Abs(pull) * 0.5f)).normalized * speed;
                            drift = Vector2.Lerp(drift, want, 1.8f * Time.deltaTime);
                        }
                    }
                    break;
                }

                case MoveKind.Orbiter:
                    // 완만하게 휘어 흐른다 — 궤도에 실린 잔해처럼
                    drift = Rotate(drift, type.movePower * 0.5f * Time.deltaTime);
                    break;

                case MoveKind.Chase:
                default:
                    // 🔴 **rev.9: 아무 데도 향하지 않는다.**
                    //    여기가 *"쓰레기가 아직도 기지로 모인다"*의 진범이었다 —
                    //    스폰 방향을 고쳐도 이 보정이 매 프레임 다시 기지로 끌어당겼다.
                    //
                    //    이제 쓰레기는 **밭**이다. 제자리에서 아주 느리게 표류하기만 한다.
                    //    (쫓는 것은 `MoveKind.Hunter`뿐이다)
                    if (drift.sqrMagnitude > idleSpeed * idleSpeed)
                        drift = Vector2.Lerp(drift, drift.normalized * idleSpeed, 1.2f * Time.deltaTime);
                    break;
            }

            float max = MaxSpeed;
            if (drift.sqrMagnitude > max * max) drift = drift.normalized * max;
        }

        static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }


        /// <summary>🔴 배가 닿았다. 연료를 깎는다 — 붙는 것의 대가.</summary>
        public bool TryContact()
        {
            if (!Alive || contactCooldown > 0f) return false;
            contactCooldown = 0.45f;   // 한 번 닿으면 잠시 무적 — 안 그러면 프레임마다 터진다
            return true;
        }
    }
}
