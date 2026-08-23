using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 배가 향하는 목표점을 화면에 그린다.
    /// 조작 피드백이기도 하지만, 목표점이 어디로 계산되는지 눈으로 확인하는 수단이기도 하다 —
    /// 2026-08-19에 배가 화면 밖으로 날아가던 버그를 여기서 잡았다.
    /// </summary>
    public class AimCursor : MonoBehaviour
    {
        public ShipController ship;
        public RunDirector director;
        public SpriteRenderer[] parts;

        void LateUpdate()
        {
            bool show = ship != null && director != null && director.FieldActive;

            for (int i = 0; i < parts.Length; i++)
                if (parts[i].enabled != show) parts[i].enabled = show;

            if (!show) return;

            transform.position = ship.AimPoint;

            // 출력이 셀수록 진해진다
            float a = Mathf.Lerp(0.20f, 0.75f, ship.ThrottleNow);
            for (int i = 0; i < parts.Length; i++)
            {
                var c = parts[i].color;
                parts[i].color = new Color(c.r, c.g, c.b, a);
            }
        }
    }
}
