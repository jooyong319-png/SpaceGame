using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 쓰레기를 깎으면 나오는 파편. **이것이 실제 수집 대상이다.**
    ///
    /// 쓰레기 본체는 닿으면 손해지만 파편은 안전하다 — 그래서 화면에서 둘이 구분돼 보여야 한다.
    /// 흡수는 배의 기본 능력이라 장비와 무관하다.
    /// </summary>
    public class Fragment : MonoBehaviour
    {
        public int value;
        public bool Alive { get; private set; }

        SpriteRenderer sr;
        Vector2 velocity;
        float life;

        public void Bind(SpriteRenderer sr) => this.sr = sr;

        /// <summary>
        /// 🔴 파편 색은 **쓰레기 색을 물려받지 않는다.**
        ///    물려받았더니 "뭐가 쓰레기고 뭐가 재화인지 구분이 안 간다"는 피드백이 나왔다
        ///    (2026-08-21). 파편만 밝고 선명한 색을 쓰고, 쓰레기는 채도를 낮춰 잔해로 읽히게 한다.
        ///    색을 값에 묶어 두면 **구분과 정보가 동시에** 해결된다 — 보라가 보이면 달려갈 이유가 된다.
        /// </summary>
        public static Color ColorFor(int value)
        {
            if (value >= 45) return new Color(0.88f, 0.60f, 1.00f);   // 보라 — 큰 것
            if (value >= 18) return new Color(1.00f, 0.84f, 0.32f);   // 금색 — 중간
            return new Color(0.55f, 1.00f, 0.98f);                    // 청록 — 기본
        }

        public void Spawn(Vector3 pos, Vector2 velocity, int value)
        {
            this.value = value;
            this.velocity = velocity;
            life = 14f;                 // 너무 오래 떠다니면 화면이 지저분해진다
            Alive = true;
            rushing = false;
            Towed = false;
            TowLead = null;
            TowIndex = 0;
            pickupLock = 0f;
            spinAngle = 0f;
            pulse = 0f;
            baseColor = ColorFor(value);
            baseScale = value >= 45 ? 0.30f : value >= 18 ? 0.26f : 0.22f;

            transform.position = pos;
            transform.localScale = Vector3.one * baseScale;
            if (sr != null) sr.color = baseColor;
            gameObject.SetActive(true);
        }

        Color baseColor;
        float baseScale = 0.22f;
        float pulse;

        public void Despawn()
        {
            Alive = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 🔴 '전체 흡수' 전용 — 배 쪽으로 **강하게 날아간다.**
        ///    자석 반경 밖에 있어도 무조건 들어오도록 표시를 남긴다.
        /// </summary>
        public void RushTo(Vector2 shipPos)
        {
            if (!Alive) return;

            Vector2 d = shipPos - (Vector2)transform.position;
            float dist = d.magnitude;
            if (dist < 0.001f) return;

            // 멀리 있는 것일수록 빠르게 — 전부 비슷한 시각에 도착해야 "쏟아진다"가 된다
            rushSpeed = Mathf.Clamp(dist * 2.2f, 14f, 55f);
            velocity = d / dist * rushSpeed;
            rushing = true;
            life = Mathf.Max(life, 6f);   // 오는 도중에 사라지면 안 된다
        }

        /// <summary>전체 흡수로 날아오는 중인가. RunDirector가 자석 반경을 무시하고 거둔다.</summary>
        public bool rushing;
        float rushSpeed;

        /// <summary>
        /// 🔴 날아오는 동안 **배를 계속 따라간다.**
        ///    처음 한 번만 방향을 잡으면 플레이어가 그 자리에 가만히 있어야만 먹힌다 —
        ///    2026-08-22 피드백: *"그 자리에 내가 가만히 있는 게 아니라서 애매하다"*.
        /// </summary>
        public void RushUpdate(Vector2 shipPos)
        {
            if (!Alive || !rushing) return;

            Vector2 d = shipPos - (Vector2)transform.position;
            if (d.sqrMagnitude < 0.0001f) return;

            velocity = Vector2.Lerp(velocity, d.normalized * rushSpeed, 10f * Time.deltaTime);
        }


        /// <summary>
        /// 🔴 수명을 늘린다. **격침 잔해 전용.**
        ///    부활 5초 + 사고 지점까지 돌아가는 시간이 기본 수명 14초보다 길 수 있다.
        ///    되찾으러 갔더니 이미 사라져 있으면 "떨어뜨렸다"가 아니라 그냥 "잃었다"가 된다.
        /// </summary>
        public void ExtendLife(float seconds) => life = Mathf.Max(life, seconds);
        /// <summary>
        /// 🔴 **빨려든다.** 곧장 끌려오는 게 아니라 **나선을 그리며** 빨려든다 —
        ///    청소기에 들어가는 먼지처럼 보이게 하는 건 이 접선 성분 하나다.
        ///    (2026-08-22 요청: *"청소기로 빨아들이는 느낌이 들었으면"*)
        /// </summary>
        public void Attract(Vector2 shipPos, float pull)
        {
            Vector2 d = shipPos - (Vector2)transform.position;
            float dist = d.magnitude;
            if (dist < 0.001f) return;

            Vector2 inward = d / dist;
            Vector2 swirl = new Vector2(-inward.y, inward.x);

            // 가까울수록 빨라진다
            float near = 1.2f - Mathf.Clamp01(dist / 6f);

            // 🔴 멀리 있을수록 접선 성분이 크다 → 휘어 들어오다 중심에서 곧게 빨린다
            float spin = Mathf.Clamp01(dist / 5f) * 0.85f;

            velocity += (inward + swirl * spin).normalized * (pull * near * Time.deltaTime);

            // 회전도 같이 — 빨려드는 것은 돈다
            spinAngle += (140f + dist * 40f) * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
        }

        float spinAngle;

        // ================================================================ 견인 (rev.11)

        /// <summary>
        /// 🔴 **딸려오는 중인가** (2026-08-23 사장님:
        ///    *"화물칸에 넣는 것보단 끌고 다니는 방식으로, 많이 딸려다니면 점점 무거워지게"*).
        ///
        ///    흡수되어 숫자가 되는 게 아니라 **배 뒤에 실제로 매달린다.**
        ///    꼬리 길이가 곧 적재량이라 **UI를 안 봐도 얼마나 실었는지 안다.**
        /// </summary>
        public bool Towed { get; private set; }

        /// <summary>줄에서 내 앞에 달린 것. 맨 앞이면 null이고 배를 따라간다.</summary>
        public Transform TowLead { get; private set; }

        /// <summary>줄에서 몇 번째인가. 뒤로 갈수록 느슨하게 따라온다.</summary>
        public int TowIndex { get; private set; }

        public void AttachTow(Transform lead, int index)
        {
            Towed = true;
            TowLead = lead;
            TowIndex = index;
            rushing = false;
            life = float.MaxValue;      // 매달린 것은 수명으로 사라지지 않는다
        }

        /// <summary>
        /// 놓는다. 🔴 **그 자리에 그대로 남는다** — 버린 게 사라지면
        /// 그건 결정이 아니라 손실이다. 나중에 다시 오면 있어야 한다.
        /// </summary>
        public void ReleaseTow(Vector2 fling)
        {
            Towed = false;
            TowLead = null;
            velocity = fling;
            life = TowedDropLife;

            // 🔴 **버린 직후에는 다시 안 빨린다.**
            //    안 그러면 놓는 즉시 자석이 도로 물어서 `Q`가 아무 쓸모가 없다 —
            //    2026-08-23 스모크가 정확히 이걸 잡았다 (버렸는데 12개가 그대로 달려 있었다).
            //    쫓길 때 **버리고 도망치는 것**이 이 조작의 존재 이유이므로,
            //    도망칠 시간만큼은 확실히 떨어져 있어야 한다.
            pickupLock = PickupLockSeconds;
        }

        /// <summary>버린 뒤 다시 주울 수 없는 시간.</summary>
        public const float PickupLockSeconds = 3f;

        float pickupLock;

        /// <summary>지금 주울 수 있는가.</summary>
        public bool Collectable => Alive && !Towed && pickupLock <= 0f;

        /// <summary>버려진 파편의 수명. 넉넉해야 "다시 오면 있다"가 성립한다.</summary>
        public const float TowedDropLife = 600f;

        void FollowTow()
        {
            if (TowLead == null) { Towed = false; return; }

            Vector2 me = transform.position;
            Vector2 lead = TowLead.position;
            Vector2 d = lead - me;
            float dist = d.magnitude;

            // 🔴 줄처럼 따라온다 — 일정 간격보다 멀어지면 당겨지고, 가까우면 놔둔다.
            //    전부 배에 딱 붙게 하면 뭉쳐서 한 덩어리로 보인다.
            //    간격이 있어야 **꼬리로 읽히고**, 꼬리로 읽혀야 길이가 정보가 된다.
            if (dist > TowGap)
            {
                float pull = Mathf.Min(1f, (dist - TowGap) * 9f * Time.deltaTime);
                transform.position = Vector2.Lerp(me, lead - d / dist * TowGap, pull);
            }

            spinAngle += 120f * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
        }

        /// <summary>줄에서의 간격.</summary>
        public const float TowGap = 0.42f;

        void Update()
        {
            if (!Alive || RunDirector.WorldPaused) return;

            if (Towed)
            {
                FollowTow();
                Sparkle();
                return;
            }

            if (pickupLock > 0f) pickupLock = Mathf.Max(0f, pickupLock - Time.deltaTime);

            transform.position += (Vector3)(velocity * Time.deltaTime);

            // 전체 흡수로 날아오는 중에는 감속하지 않는다 — 끝까지 도달해야 한다
            if (!rushing) velocity *= 1f - Mathf.Min(0.9f, 1.6f * Time.deltaTime);

            Sparkle();

            life -= Time.deltaTime;
            if (life <= 0f) { Despawn(); return; }

            if (sr != null)
            {
                float a = life < 2f ? Mathf.Clamp01(life / 2f) : 1f;
                float b = 0.82f + 0.18f * Mathf.Sin(pulse);          // 밝기도 같이 뛴다
                sr.color = new Color(baseColor.r * b, baseColor.g * b, baseColor.b * b, a);
            }
        }

        /// <summary>
        /// 🔴 반짝인다. 정지한 잔해 사이에서 **움직임이 다른 것**이 가장 빨리 눈에 띈다.
        ///    (색만으로는 화면에 200개가 떠 있을 때 안 읽힌다)
        /// </summary>
        void Sparkle()
        {
            pulse += Time.deltaTime * 9f;
            float p = 1f + 0.22f * Mathf.Sin(pulse);
            transform.localScale = Vector3.one * (baseScale * p);

            if (Towed && sr != null)
            {
                float b = 0.82f + 0.18f * Mathf.Sin(pulse);
                sr.color = new Color(baseColor.r * b, baseColor.g * b, baseColor.b * b, 1f);
            }
        }
    }
}
