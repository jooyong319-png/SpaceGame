using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 쓰레기를 부술 때 드물게 나오는 **아이템**.
    ///
    /// 🔴 파편(<see cref="Fragment"/>)과 역할이 다르다.
    ///    파편은 **항상** 나오고 자원이 된다 — 흐름이다.
    ///    아이템은 **가끔** 나오고 판을 한 번 흔든다 — 사건이다.
    ///    둘 다 "주우면 좋은 것"이라 **한눈에 구분되지 않으면 아이템의 사건성이 죽는다.**
    ///    그래서 아이템은 훨씬 크고, 흰 테두리가 돌고, 색이 고정이다.
    /// </summary>
    public enum PickupKind
    {
        Fuel = 0,   // 연료 회복
        Vacuum      // 맵 전체의 파편을 한 번에 빨아들인다
    }

    public class PickupItem : MonoBehaviour
    {
        public PickupKind kind;
        public bool Alive { get; private set; }

        SpriteRenderer sr;
        Transform ring;
        SpriteRenderer ringSr;

        Vector2 velocity;
        float life;
        float clock;

        public void Bind(SpriteRenderer sr, Transform ring)
        {
            this.sr = sr;
            this.ring = ring;
            ringSr = ring != null ? ring.GetComponent<SpriteRenderer>() : null;
        }

        /// <summary>🔴 아이템 색은 고정이다. 매번 같은 색이어야 "저건 연료다"가 학습된다.</summary>
        public static Color ColorFor(PickupKind k)
            => k == PickupKind.Fuel
                ? new Color(0.45f, 1.00f, 0.62f)    // 초록 — 연료
                : new Color(1.00f, 0.95f, 0.45f);   // 노랑 — 흡입

        public void Spawn(Vector3 pos, Vector2 velocity, PickupKind kind)
        {
            this.kind = kind;
            this.velocity = velocity;
            life = 22f;            // 파편보다 오래 남는다 — 놓치면 아까우니까
            clock = 0f;
            Alive = true;

            transform.position = pos;
            if (sr != null) sr.color = ColorFor(kind);
            gameObject.SetActive(true);
        }

        public void Despawn()
        {
            Alive = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Alive || RunDirector.WorldPaused) return;

            transform.position += (Vector3)(velocity * Time.deltaTime);
            velocity *= 1f - Mathf.Min(0.9f, 1.1f * Time.deltaTime);

            // 🔴 크게 맥동한다. 파편의 반짝임보다 느리고 크게 — 리듬이 달라야 구분된다
            clock += Time.deltaTime * 3.4f;
            float p = 1f + 0.20f * Mathf.Sin(clock);
            transform.localScale = Vector3.one * (0.62f * p);

            if (ring != null)
            {
                ring.localScale = Vector3.one * (1.6f + 0.35f * Mathf.Sin(clock * 1.3f));
                if (ringSr != null)
                    ringSr.color = new Color(1f, 1f, 1f, 0.30f + 0.20f * Mathf.Sin(clock * 1.3f));
            }

            life -= Time.deltaTime;
            if (life <= 0f) { Despawn(); return; }

            if (sr != null && life < 3f)
            {
                var c = ColorFor(kind);
                sr.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(life / 3f));
            }
        }

        /// <summary>배 쪽으로 끌려온다. 파편보다 자석 반경이 넓게 잡혀 있다.</summary>
        public void Attract(Vector2 shipPos, float pull)
        {
            Vector2 d = shipPos - (Vector2)transform.position;
            float dist = d.magnitude;
            if (dist < 0.001f) return;
            velocity += d / dist * (pull * 0.7f * Time.deltaTime);
        }
    }
}
