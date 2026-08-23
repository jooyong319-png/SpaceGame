using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 🔴 **적이 쏘는 탄** (rev.10).
    ///
    ///    2026-08-23 사장님: *"적은 왜 돌진..만 있어?"*
    ///
    ///    돌진만 있으면 플레이어의 대응이 하나뿐이다 — 피하거나 죽이거나.
    ///    **거리를 두고 쏘는 적**이 생기면 *"쫓아갈까, 무시하고 캘까"*가 생기고,
    ///    드릴로 캐는 동안 배가 묶이는 것과 정면으로 부딪힌다 —
    ///    **캐는 중에 날아오는 탄**이 이 게임에서 가장 어려운 순간이 된다.
    ///
    /// ⚠️ 탄은 **배리어를 깎는다.** 즉 두 대 맞으면 격침이다 —
    ///    쓰레기에 부딪히는 것과 같은 규칙이라 새로 배울 게 없다.
    /// </summary>
    public class EnemyShot : MonoBehaviour
    {
        public SpriteRenderer body;

        public bool Alive { get; private set; }

        Vector2 vel;
        float life;

        public void Spawn(Vector3 pos, Vector2 velocity, Color color, float seconds = 3.2f)
        {
            transform.position = pos;
            vel = velocity;
            life = seconds;
            Alive = true;

            gameObject.SetActive(true);
            if (body != null) body.color = color;
        }

        public void Despawn()
        {
            Alive = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Alive || RunDirector.WorldPaused) return;

            life -= Time.deltaTime;
            if (life <= 0f) { Despawn(); return; }

            transform.position += (Vector3)(vel * Time.deltaTime);

            // 🔴 꼬리를 남긴다. 작은 점 하나는 **날아오는 게 안 보인다** —
            //    안 보이는 위협은 난이도가 아니라 불공평함이다
            if (Fx.Instance != null)
            {
                trailCd -= Time.deltaTime;
                if (trailCd <= 0f)
                {
                    trailCd = 0.04f;
                    Fx.Trail(transform.position, -vel * 0.25f,
                             body != null ? body.color : Color.red, 0.16f);
                }
            }
        }

        float trailCd;
    }
}
