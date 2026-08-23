using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 그레이박스용 최소 피드백. 진행 방향으로 배가 돌고, 출력만큼 화염이 커진다.
    /// 마우스 조작에서는 "지금 추진 중인가"가 눈에 보여야 연료 감각이 생긴다.
    /// </summary>
    public class ShipVisual : MonoBehaviour
    {
        public ShipController ship;
        public Transform bodyRoot;
        public SpriteRenderer body;
        public SpriteRenderer flame;

        /// <summary>우주선 도색. RunDirector가 런 시작마다 넣어 준다.</summary>
        public Color hullColor = Color.white;

        string builtFor;

        public void ApplyShip(SalvageRun.Data.ShipDef def)
        {
            if (def == null) return;

            hullColor = def.color;
            if (bodyRoot != null)
                bodyRoot.localScale = Vector3.one * Mathf.Max(0.5f, def.bodyScale);

            // 🔴 실루엣은 배마다 새로 찍는다. 색만 바꾸면 여섯 척이 사실상 한 척이다.
            //    같은 배로 다시 시작할 땐 다시 안 찍는다 — 런마다 텍스처가 쌓인다
            if (body == null || builtFor == def.id) return;

            builtFor = def.id;
            // 🔴 배마다 흡입구 크기가 다르다 — nose가 작을수록(뾰족할수록) 입도 작다
            body.sprite = PixelArt.Cleaner(24, Mathf.Clamp01(0.25f + def.nose), def.tail, def.wing);
        }

        /// <summary>
        /// 🔴 **배리어가 곧 체력이다** (rev.10). 그래서 숫자가 아니라 **배 주위에** 보여야 한다.
        ///    화면 구석의 막대를 보려고 눈을 떼는 순간 맞는다 —
        ///    "지금 안전한가"는 **배를 보고** 알 수 있어야 한다.
        ///
        ///    · 있을 때: 배를 감싼 푸른 고리
        ///    · 없을 때: 고리가 사라지고 **배가 붉게 맥동한다** (다음 한 대가 끝이다)
        ///    · 재생 직전: 고리가 빠르게 깜빡이며 돌아온다
        /// </summary>
        void DrawBarrier()
        {
            if (barrierRing == null)
            {
                var go = new GameObject("Barrier");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.Ring(48, 0.12f);
                sr.sortingOrder = 14;
                barrierRing = go.transform;
                barrierSr = sr;
            }

            barrierRing.gameObject.SetActive(true);

            // 🔴 **무적이면 무적으로 보여야 한다.** 안 보이면 무적인 줄 모르고 도망만 친다 —
            //    3초를 그냥 버리는 셈이다. 흰색으로 빠르게 깜빡여 배리어와 구분한다.
            if (ship.Invulnerable)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 22f);
                barrierRing.localScale = new Vector3(2.4f, 2.4f, 1f);
                barrierSr.color = new Color(1f, 1f, 1f, 0.35f + 0.45f * blink);

                if (body != null)
                    body.color = Color.Lerp(hullColor, Color.white, 0.35f + 0.35f * blink);
                return;
            }

            // (평상시 색은 아래 LateUpdate 본문에서 hullColor로 다시 칠한다)

            bool up = ship.BarrierUp;

            if (up)
            {
                float pulse = 0.55f + 0.15f * Mathf.Sin(Time.time * 3f);
                barrierRing.localScale = new Vector3(2.05f, 2.05f, 1f);
                barrierSr.color = new Color(0.5f, 0.85f, 1f, pulse * 0.55f);
            }
            else
            {
                // 재생이 가까울수록 빠르게 깜빡인다 — 언제 돌아오는지 세지 않아도 안다
                float left = Mathf.Max(0.01f, ship.BarrierLeft);
                float speed = Mathf.Lerp(16f, 3f, Mathf.Clamp01(left / ship.BarrierSeconds));
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);

                barrierRing.localScale = new Vector3(1.7f, 1.7f, 1f);
                barrierSr.color = new Color(1f, 0.4f, 0.35f, 0.15f + 0.30f * blink);
            }
        }

        Transform barrierRing;
        SpriteRenderer barrierSr;

        /// <summary>흡입 반경 표시. RunDirector가 매 프레임 넣어 준다.</summary>
        public float intakeRadius;

        float suckClock;

        /// <summary>
        /// 🔴 **빨아들이고 있다는 게 보여야 한다.**
        ///    자석 효과는 원래도 있었지만 **화면에 아무 표시가 없어서**
        ///    파편이 그냥 알아서 오는 것처럼 보였다.
        ///    이제 흡입 반경에서 안쪽으로 먼지가 흘러든다 — 청소기처럼.
        /// </summary>
        void DrawSuction()
        {
            if (intakeRadius <= 0.1f || Fx.Instance == null) return;

            suckClock -= Time.deltaTime;
            if (suckClock > 0f) return;
            suckClock = 0.06f;

            for (int i = 0; i < 2; i++)
            {
                suckSeed = (suckSeed * 16807f) % 2147483647f;
                float a = (suckSeed / 2147483647f) * Mathf.PI * 2f;

                Vector2 from = (Vector2)transform.position
                             + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * intakeRadius * 0.95f;

                Fx.Mote(from, transform, new Color(0.65f, 0.9f, 1f, 0.5f), 0.4f);
            }
        }

        float suckSeed = 11.3f;

        void LateUpdate()
        {
            if (ship == null) return;

            DrawSuction();
            DrawBarrier();

            Vector2 v = ship.Velocity;
            if (bodyRoot != null && v.sqrMagnitude > 0.05f)
            {
                float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                bodyRoot.rotation = Quaternion.Lerp(bodyRoot.rotation,
                    Quaternion.Euler(0f, 0f, ang), 1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            // 🔴 배마다 색이 다르다. 우주선 종류가 외형으로 안 보이면
            //    "다른 배로 하는 중"이라는 게 화면 어디에도 안 남는다.
            if (body != null)
                body.color = ship.OutOfFuel ? new Color(0.55f, 0.55f, 0.62f) : hullColor;

            if (flame == null) return;

            float th = ship.ThrottleNow;
            bool on = th > 0.02f;
            flame.enabled = on;
            if (!on) return;

            float len = Mathf.Lerp(0.25f, 1.15f, th);
            flame.transform.localScale = new Vector3(len, 0.34f, 1f);
            flame.transform.localPosition = new Vector3(-0.7f - len * 0.5f, 0f, 0f);
            flame.color = Color.Lerp(new Color(1f, 0.75f, 0.35f), new Color(1f, 0.45f, 0.2f), th);
        }
    }
}
