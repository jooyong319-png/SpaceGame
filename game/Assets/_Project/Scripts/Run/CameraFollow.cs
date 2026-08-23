using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 배를 따라가는 카메라. 맵 경계 안에 가둔다.
    ///
    /// 🔴 2026-08-20: "아레나 = 화면 하나, 카메라 고정"을 뒤집었다.
    ///    뱀서의 핵심 동사가 "도망치며 싸우기"인데, 화면 한 장에 갇히면 도망이 성립하지 않는다.
    ///
    /// ⚠️ 화면 흔들림은 여기서 **마지막에** 더한다.
    ///    Juice가 카메라 위치를 직접 건드리면 추적과 싸우게 되고,
    ///    조준(월드 커서)도 흔들려서 배가 튀는 피드백 루프가 생긴다 — 실제로 겪었다.
    ///    조준은 <see cref="BasePosition"/>(흔들리기 전)을 기준으로 계산한다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smooth = 8f;

        /// <summary>맵 반경. RunDirector가 넣는다.</summary>
        public Vector2 mapHalf = new Vector2(52f, 34f);

        /// <summary>흔들림을 빼기 전의 카메라 위치. 조준은 이걸 기준으로 한다.</summary>
        public Vector3 BasePosition { get; private set; }

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            BasePosition = transform.position;
        }

        void LateUpdate()
        {
            if (target == null || cam == null) return;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            // 맵 밖의 빈 공간이 보이지 않게 가둔다
            float limX = Mathf.Max(0f, mapHalf.x - halfW);
            float limY = Mathf.Max(0f, mapHalf.y - halfH);

            var want = new Vector3(
                Mathf.Clamp(target.position.x, -limX, limX),
                Mathf.Clamp(target.position.y, -limY, limY),
                BasePosition.z);

            BasePosition = Vector3.Lerp(BasePosition, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));

            // 흔들림은 항상 마지막에 더한다
            Vector3 shake = Juice.Instance != null ? Juice.Instance.ShakeOffset : Vector3.zero;
            transform.position = BasePosition + shake;
        }
    }
}
