using UnityEngine;
using SalvageRun.Data;

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

        // 🔴 **지금 주워질 것**을 알려주는 테두리 (2026-08-26 사장님:
        //    *"space 누르기 전에 어떤 게 먹어지는지 보여줘야 할 듯"*).
        //    수집기는 **가장 가까운 하나**만 문다 — 어느 것인지 안 보이면
        //    "고른다"가 사실상 운이 된다. 누르기 **전에** 보여야 고르는 것이다.
        SpriteRenderer ring;

        public void Bind(SpriteRenderer sr) => this.sr = sr;

        public void BindRing(SpriteRenderer ring)
        {
            this.ring = ring;
            if (ring != null) ring.enabled = false;
        }

        /// <summary>이번 프레임에 이게 주워질 후보인가. `RunDirector`가 매 프레임 정한다.</summary>
        public bool Marked { get; set; }

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

        /// <summary>
        /// 🔴 **이 덩어리가 무슨 재화인가** (2026-08-26 사장님 지시:
        ///    *"자원을 직접 쓰레기를 잡으면 실제로 나오는 것처럼"*).
        ///
        ///    전에는 재화가 **눈에 안 보였다** — 쓰레기를 부수면 확률로 저장에 숫자만 올랐다.
        ///    그러면 *"이걸 가져갈까 버릴까"*가 성립할 수 없다. 물건이 없으니까.
        ///    이제 덩어리로 떨어지고, 주우면 배 뒤에 매달린다.
        /// </summary>
        public MatKind mat;

        /// <summary>이 덩어리가 품은 재화 개수.</summary>
        public int matAmount;

        public void Spawn(Vector3 pos, Vector2 velocity, int value,
                          MatKind mat = MatKind.Scrap, int matAmount = 1)
        {
            this.value = value;
            this.mat = mat;
            this.matAmount = Mathf.Max(1, matAmount);
            this.velocity = velocity;
            // 🔴 **14초 → 45초** (2026-08-26). 자석이 없어져 **직접 가서 주워야** 하는데
            //    14초면 고르는 사이에 사라진다 — 그러면 "고를 수 있다"가 거짓말이 된다.
            //    화면이 지저분해지는 건 감수한다. 널린 재화가 곧 "어디로 갈까"의 지도다.
            life = 45f;
            Alive = true;
            rushing = false;
            Towed = false;
            TowLead = null;
            TowIndex = 0;
            pickupLock = 0f;
            Marked = false;
            if (ring != null) ring.enabled = false;
            spinAngle = 0f;
            pulse = 0f;
            // 🔴 **색은 재화 종류를 말한다.** 값어치가 아니라 **무엇인가**가 먼저다 —
            //    무엇을 가져갈지 고르는 게임이 되면 종류가 값보다 중요해진다.
            baseColor = Mats.ColorOf(mat);
            // 🔴 **크기를 두 배로** (2026-08-26 사장님: *"재화들의 크기를 좀 더 키우고"*).
            //    자석이 없어져 **직접 가서 밟아야** 하는데, 작으면 조준이 신경질적이 된다.
            //    그리고 멀리서 "저기 코어다"가 보여야 **어디로 갈지**를 고를 수 있다.
            // 🔴 크기도 종류가 정한다 (`Mats.ScaleOf`). 값진 것일수록 크다 —
            //    밟아서 줍는 게임이라 작으면 조준이 신경질적이고,
            //    멀리서 "저기 코어다"가 보여야 어디로 갈지 고를 수 있다.
            baseScale = Mats.ScaleOf(mat);

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
            Marked = false;
            if (ring != null) ring.enabled = false;   // 표시가 남은 채 풀로 돌아가면 다음 것이 켜진 채 나온다
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

            UpdateRing();
        }

        /// <summary>
        /// 🔴 **다음에 주워질 것에 테두리를 두른다.**
        ///    수집기를 켜기 **전에** 보여야 고르는 것이 된다 —
        ///    켠 뒤에 알려주면 그건 통보지 선택이 아니다.
        /// </summary>
        void UpdateRing()
        {
            if (ring == null) return;

            if (!Marked || Towed)
            {
                if (ring.enabled) ring.enabled = false;
                return;
            }

            ring.enabled = true;

            // 재화 색을 그대로 쓰되 더 밝게 — 무엇이 걸렸는지 색으로도 읽힌다
            float b = 0.75f + 0.25f * Mathf.Sin(pulse * 0.8f);
            ring.color = new Color(
                Mathf.Min(1f, baseColor.r + 0.35f),
                Mathf.Min(1f, baseColor.g + 0.35f),
                Mathf.Min(1f, baseColor.b + 0.35f), b);

            // 살짝 크게 돌면서 숨 쉰다. 정지한 테두리는 배경으로 묻힌다
            float k = 1.9f + 0.18f * Mathf.Sin(pulse * 0.8f);
            ring.transform.localScale = Vector3.one * k;
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, pulse * 22f);
        }
    }
}
