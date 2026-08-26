using UnityEngine;
using SalvageRun.Core;
using SalvageRun.Data;
using SalvageRun.Meta;

namespace SalvageRun.Run
{
    /// <summary>
    /// 마우스 조작. **누르고 있는 동안만** 커서를 향해 추진하고, 떼면 관성으로 미끄러진다.
    /// Shift(또는 우클릭) = 대시 — 커서 방향으로 순간 가속. (2026-08-19 확정)
    ///
    /// 🔴 버튼을 안 누르면 연료를 쓰지 않는다.
    ///    "가만히 생각하는 시간"에 벌을 주지 않는다는 원칙을 이 조작에서도 지킨다.
    ///
    /// ⚠️ 목표점은 카메라 기준이고 카메라는 배를 따라간다. 그래서 배와 목표점을 **둘 다**
    ///    지역 경계 안에 가두지 않으면, 커서가 화면 가장자리에 있을 때 배가 목표를 영원히
    ///    쫓아가며 무한 가속한다. (2026-08-19 실제로 발생한 버그)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class ShipController : MonoBehaviour
    {
        public RunConfig config;
        public RunStats stats;
        public Camera cam;

        /// <summary>지역 반경. (0,0)이면 제한 없음. RunDirector가 출항 때 넣는다.</summary>
        public Vector2 boundsHalf;

        public float Fuel { get; private set; }
        public float FuelMax => stats != null ? stats.fuelMax : (config != null ? config.fuelMax : 100f);
        public bool OutOfFuel => Fuel <= 0.001f;
        public float ThrottleNow { get; private set; }
        public bool ControlEnabled { get; set; } = true;
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;

        // ---------------------------------------------------------------- 배리어

        /// <summary>
        /// 🔴 **배리어** (2026-08-21 요청: *"우주선에 배리어가 있고, 1대 맞으면 사라지고,
        ///    시간이 지나면 다시 생성되는 방식(5초 정도)"*).
        ///
        ///    피해량을 줄이는 게 아니라 **한 대를 통째로 없앤다.**
        ///    그래서 "몇 대 맞았나"가 아니라 **"지금 배리어가 있나"**를 보게 된다 —
        ///    수치가 아니라 상태라서 화면만 보고 판단할 수 있다.
        ///
        ///    5초는 짧지 않다. 배리어를 깨고 들어갔으면 **그 다음 5초는 진짜 위험**이어야
        ///    붙었다 빠지는 리듬이 생긴다.
        /// </summary>
        /// <summary>드릴이 물고 있을 때의 속도 배율. 0이 아닌 이유는 위 주석 참고.</summary>

        /// <summary>드릴이 무언가를 물고 있는가. `WeaponRig`이 매 프레임 넣어 준다.</summary>

        /// <summary>
        /// 🔴 **부활 무적 시간** (2026-08-23 플레이 피드백:
        ///    *"이게 죽고 나면, 부활도 못하고 계속 죽여서 못 살아나"*).
        ///
        ///    부활 지점은 기지인데, **파손 로봇은 배를 쫓으므로 죽은 자리에 모여 있다.**
        ///    그래서 나오자마자 두 대 맞고 다시 죽고, 5초 뒤 또 같은 자리에 나오고 —
        ///    **빠져나올 수 없는 고리**가 된다. 플레이어가 할 수 있는 게 아무것도 없다.
        ///
        ///    무적 동안 배가 깜빡인다. 안 보이면 무적인 줄 모르고 도망만 친다.
        /// </summary>
        public float InvulnLeft { get; private set; }

        public bool Invulnerable => InvulnLeft > 0f;

        public void GrantInvuln(float seconds) => InvulnLeft = Mathf.Max(InvulnLeft, seconds);

        /// <summary>
        /// ⬜ **2026-08-23부터 깨지지 않는다.** 플레이어가 무적이 되면서
        ///    `AbsorbHit()`을 부르는 곳이 없어졌기 때문이다.
        ///    그래서 배 주위의 고리가 **항상 켜져 있다** — 그게 마침 "무적"으로 읽힌다.
        ///    (지우지 않은 이유: 위협을 되살리면 그대로 다시 동작한다)
        /// </summary>
        public bool BarrierUp { get; private set; } = true;

        /// <summary>남은 재생 시간. HUD가 링으로 그린다.</summary>
        public float BarrierLeft { get; private set; }

        public float BarrierSeconds = 5f;

        /// <summary>배리어가 막았으면 true — 이 경우 피해는 0이다.</summary>
        public bool AbsorbHit()
        {
            if (Invulnerable) return true;      // 무적 중에는 아예 없던 일이 된다
            if (!BarrierUp) return false;

            BarrierUp = false;
            BarrierLeft = BarrierSeconds;
            Juice.Contact();
            return true;
        }

        void UpdateBarrier()
        {
            if (InvulnLeft > 0f) InvulnLeft = Mathf.Max(0f, InvulnLeft - Time.deltaTime);
            if (BarrierUp) return;

            BarrierLeft -= Time.deltaTime;
            if (BarrierLeft > 0f) return;

            BarrierUp = true;
            BarrierLeft = 0f;
            Juice.LevelUp();
            if (Fx.Instance != null)
                Fx.Shockwave(transform.position, 1.4f, new Color(0.5f, 0.9f, 1f, 0.9f), 0.25f);
        }
        public Vector2 AimPoint { get; private set; }

        /// <summary>지역의 연료 소모 배수.</summary>
        public float StageDrain { get; set; } = 1f;

        /// <summary>
        /// 마우스 대신 이 좌표를 목표로 삼는다. 헤드리스 시뮬레이션(봇 조종)에서 쓴다.
        /// null이면 평소대로 커서를 따라간다.
        /// </summary>
        public Vector2? AimOverride { get; set; }

        /// <summary>시뮬레이션이 버튼을 대신 눌러주는 훅. null이면 실제 입력을 본다.</summary>
        public bool? ThrustOverride { get; set; }

        public float DashCooldownLeft { get; private set; }
        public bool DashReady => DashCooldownLeft <= 0f && !OutOfFuel;

        Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        public void ResetShip(Vector2 pos, float fuel)
        {
            transform.position = pos;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                // 🔴 각속도도 지운다. 안 지우면 **앞 런에서 돌던 회전이 남아**
                //    다음 런의 첫 몇 초 조종감이 달라진다 — 결정론이 조용히 깨지는 자리다.
                rb.angularVelocity = 0f;
                rb.rotation = 0f;
            }

            // 🔴 처치 가속(`KillSpeed`)이 런 사이에 남아 있었다.
            //    남으면 다음 런이 **더 빠른 배로 시작**한다.
            killRush = 0f;
            killRushLeft = 0f;
            Fuel = fuel;
            ControlEnabled = true;
            ThrottleNow = 0f;
            AimPoint = pos;
            DashCooldownLeft = 0f;

            BarrierUp = true;
            BarrierLeft = 0f;
            InvulnLeft = 0f;
        }

        /// <summary>
        /// ⚠️ 입력은 Update에서 읽는다. Input System은 기본이 Dynamic Update라
        ///    FixedUpdate에서 읽으면 갱신 타이밍이 어긋날 수 있다.
        /// </summary>
        void Update()
        {
            if (RunDirector.WorldPaused) return;

            // 🔴 **연료는 타이머다** (2026-08-23 사장님: *"연료는 자동으로 닳게 해줘,
            //    타이머 개념인거지"*).
            //
            //    무엇을 하든 같은 속도로 준다 — 밀어도, 대시해도, 가만히 있어도.
            //    행동에 값을 매기던 것(추진 소모·대시 비용)은 전부 뺐다.
            //
            // 🔴 왜 이게 나은가: 값이 붙어 있으면 **아끼는 것이 이득**이 된다.
            //    그러면 최적 플레이가 "덜 움직이기"가 되는데, 이 게임에서
            //    움직이는 것 말고는 할 게 없다 — 즉 **잘하는 법이 안 하는 것**이 된다.
            //    타이머는 반대다. 어차피 가므로 **쓰는 게 이득**이다.
            //
            //    ⚠️ 조종이 잠겼을 때도 간다(`ControlEnabled` 확인보다 위에 있다).
            //       멈추는 건 `WorldPaused`(카드 고르는 중)뿐이다.
            if (ControlEnabled || Fuel > 0f)
                Fuel = Mathf.Max(0f, Fuel - config.idleFuelPerSecond * Tuning.FuelDrainMul
                                          * (stats != null ? stats.fuelDrainMul : 1f)
                                          * Time.deltaTime);

            if (DashCooldownLeft > 0f) DashCooldownLeft -= Time.deltaTime;
            if (killRushLeft > 0f) killRushLeft -= Time.deltaTime;
            if (!ControlEnabled) return;

            // 🔴 화면 흔들림이 조준을 흔들지 않게, 흔들리기 전 카메라 위치를 기준으로 계산한다
            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            // 🔴 봇(`AimOverride`)이 없으면 **배 자신**이 조준점이다 —
            //    키보드 전용이 된 뒤로 마우스 좌표는 아무 데도 안 쓰인다 (2026-08-27).
            //    `WorldMouse`를 계속 부르면 카메라 변환만 낭비하고,
            //    무엇보다 **마우스를 안 쓰는데 마우스를 읽는 코드**가 남아 오해를 부른다.
            Vector2 world = AimOverride ?? (Vector2)transform.position;
            AimPoint = ClampToBounds(world);

            if (InputReader.DashPressed) TryDash();
        }

        /// <summary>커서 방향으로 순간 가속. 대시 자체는 방향만 주고 속도는 관성이 만든다.</summary>
        public bool TryDash()
        {
            if (!ControlEnabled || !DashReady || config == null) return false;

            Vector2 dir = AimPoint - (Vector2)transform.position;
            if (dir.sqrMagnitude < 0.0001f) return false;

            rb.AddForce(dir.normalized * config.dashImpulse, ForceMode2D.Impulse);
            // ⬜ 대시는 공짜다. 연료는 **타이머**이므로 행동에 값을 매기지 않는다
            // 🔴 `dashCooldownMul`을 여기서 읽는다. 2026-08-26까지 노드 두 개(drv3·drv6)가
            //    이 값을 올리고 있었는데 **읽는 곳이 없어서 아무 일도 안 났다.**
            DashCooldownLeft = config.dashCooldown
                             * (stats != null ? stats.dashCooldownMul : 1f);
            return true;
        }

        void FixedUpdate()
        {
            if (config == null) return;

            // 🔴 카드를 고르는 동안은 배도 멈춘다. 관성으로 계속 미끄러지면 고르는 사이에 부딪힌다.
            //    rb.simulated를 끄면 속도는 그대로 보존되므로 재개하면 자연스럽게 이어진다.
            if (RunDirector.WorldPaused)
            {
                if (rb.simulated) rb.simulated = false;
                ThrottleNow = 0f;
                return;
            }
            if (!rb.simulated) rb.simulated = true;

            rb.mass = config.mass;
            rb.linearDamping = stats != null ? stats.damping : config.linearDamping;

            UpdateBarrier();

            if (!ControlEnabled) { ThrottleNow = 0f; return; }

            // 🔴 누르고 있는 동안만 추진한다
            bool held = ThrustOverride ?? InputReader.LeftHeld;

            Vector2 toCursor = AimPoint - (Vector2)transform.position;
            float dist = toCursor.magnitude;

            // 🔴 **키보드 조작** (2026-08-21 요청). 봇이 조종 중일 때는 건드리지 않는다 —
            //    시뮬과 화면 속 봇이 같은 경로를 타야 측정이 유효하다.
            //
            //    마우스는 "여기로 가라"(목적지)이고 키보드는 "이쪽으로 밀어라"(방향)다.
            //    그래서 거리 기반 감속이 없다 — 누르면 최대 추력, 놓으면 관성으로 미끄러진다.
            if (InputReader.UsingKeyboard && ThrustOverride == null && AimOverride == null)
            {
                Vector2 axis = InputReader.MoveAxis;
                held = axis.sqrMagnitude > 0.0001f;

                // 조준(무기 방향)은 미는 쪽을 본다. 안 그러면 배가 엉뚱한 데를 겨눈다
                if (held) toCursor = axis;
                dist = held ? config.fullThrustDistance : 0f;
            }

            // 🔴 2026-08-20 뱀서류 전환: 연료는 **순수 HP**다.
            //    이동에 비용이 없다 — 뱀서에서 도망이 곧 생존인 것과 같다.
            //    연료는 오직 '부딪힐 때' 줄어든다. 그래서 "가만히 있는 게 최적"이 성립할 수 없다.

            if (!held || dist <= config.deadZone || OutOfFuel)
            {
                ThrottleNow = 0f;
            }
            else
            {
                // 멀수록 강하게, 가까울수록 약하게 = 커서 위에서 부드럽게 정지
                ThrottleNow = Mathf.Clamp01(Mathf.InverseLerp(config.deadZone, config.fullThrustDistance, dist));

                // 🔴 **짐이 무거우면 느려진다** (2026-08-26 — 견인을 되살렸다).
                //    욕심의 대가가 조작감에 직접 실려야 *"하나만 더?"*가 진짜 결정이 된다.
                float weight = RunDirector.Instance != null
                             ? RunDirector.Instance.TowWeightMul : 1f;

                float force = (stats != null ? stats.thrustForce * stats.speedMul : config.thrustForce)
                            * ThrottleNow * weight * KillRushMul;
                rb.AddForce(toCursor.normalized * force, ForceMode2D.Force);

            }


            float cap = config.maxSpeed * (stats != null ? stats.speedMul : 1f)
                      * (RunDirector.Instance != null ? RunDirector.Instance.TowWeightMul : 1f)
                      * KillRushMul;
            if (rb.linearVelocity.magnitude > cap)
                rb.linearVelocity = rb.linearVelocity.normalized * cap;

            ClampShipInsideBounds();
        }

        Vector2 ClampToBounds(Vector2 p)
        {
            if (boundsHalf.x <= 0f || boundsHalf.y <= 0f) return p;
            p.x = Mathf.Clamp(p.x, -boundsHalf.x, boundsHalf.x);
            p.y = Mathf.Clamp(p.y, -boundsHalf.y, boundsHalf.y);
            return p;
        }

        /// <summary>경계에 부딪히면 그 축의 속도를 죽인다. 튕기면 조작이 어지럽다.</summary>
        void ClampShipInsideBounds()
        {
            if (boundsHalf.x <= 0f || boundsHalf.y <= 0f) return;

            Vector2 p = transform.position;
            Vector2 v = rb.linearVelocity;

            if (Mathf.Abs(p.x) > boundsHalf.x)
            {
                p.x = Mathf.Sign(p.x) * boundsHalf.x;
                v.x = 0f;
            }
            if (Mathf.Abs(p.y) > boundsHalf.y)
            {
                p.y = Mathf.Sign(p.y) * boundsHalf.y;
                v.y = 0f;
            }

            transform.position = p;
            rb.linearVelocity = v;
        }

        /// <summary>
        /// 보스 반발장 등 외부에서 미는 힘.
        /// 🔴 **연료를 깎지 않는다** — 보스는 때리지 않고 방해만 한다
        ///    (브리프 §10 "공격 패턴 안 함"). 플레이어가 잃는 건 체력이 아니라 자리와 시간이다.
        /// </summary>
        public void AddExternalForce(Vector2 f)
        {
            if (rb != null && rb.simulated) rb.AddForce(f, ForceMode2D.Force);
        }

        /// <summary>추진 외의 소모 — 위험물을 먹었을 때 등.</summary>
        public void ConsumeFuel(float amount) => Fuel = Mathf.Max(0f, Fuel - amount);

        public void Refuel(float amount) => Fuel = Mathf.Min(FuelMax, Fuel + amount);

        // ---------------------------------------------------------------- 처치 가속

        /// <summary>
        /// 🔴 **부수면 잠깐 빨라진다** (테크트리 `KillSpeed`).
        ///    치우는 리듬에 보상을 붙인다 — 잘 부술수록 다음 것으로 빨리 간다.
        ///    ⚠️ 짐이 무거우면 그만큼 덜 체감된다. 그건 의도다 —
        ///       가볍게 다니는 쪽에 더 큰 보상이 가야 "얼마나 실을까"가 계산이 된다.
        /// </summary>
        public void GrantKillRush(float amount)
        {
            killRush = Mathf.Max(killRush, amount);
            killRushLeft = KillRushSeconds;
        }

        public float KillRushMul => killRushLeft > 0f ? 1f + killRush : 1f;

        const float KillRushSeconds = 2f;
        float killRush, killRushLeft;
    }
}
