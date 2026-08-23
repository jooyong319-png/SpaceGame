using UnityEngine;
using SalvageRun.Data;

namespace SalvageRun.Run
{
    /// <summary>
    /// 봇 조종.
    ///
    /// 🔴 **밸런스 시뮬레이터와 화면 속 봇이 같은 코드다.**
    ///    따로 두면 둘이 조용히 갈라지고, 그러면 봇을 구경해도
    ///    시뮬 결과를 검증하는 게 아니게 된다 — 그냥 다른 봇을 보는 것이다.
    ///    `BalanceSim`은 <see cref="Drive"/>를 호출할 뿐 자기 조종 로직을 갖지 않는다.
    ///
    /// 게임 안에서는 `B`로 켜고 끈다. 시뮬이 왜 그런 값을 냈는지 눈으로 보라고 만든 것이다.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public RunDirector director;
        public ShipController ship;

        public static bool Engaged { get; private set; }

        void Update()
        {
            if (director == null || ship == null) return;

            if (Core.InputReader.ToggleBotPressed) Engaged = !Engaged;

            if (!Engaged || !director.FieldActive)
            {
                if (!Engaged) Release(ship);
                return;
            }

            Drive(director, ship);
        }

        /// <summary>조종을 사람에게 돌려준다.</summary>
        public static void Release(ShipController ship)
        {
            if (ship == null) return;
            ship.AimOverride = null;
            ship.ThrustOverride = null;
        }

        /// <summary>
        /// 한 프레임 조종한다.
        ///
        /// 🔴 뱀서라이크에서 사람이 하는 일은 **"무리에 붙되 닿지 않는 것"**이다.
        ///    가장 가까운 쓰레기로 곧장 가는 봇은 *수집 게임의 봇*이고,
        ///    계속 부딪혀서 실제보다 훨씬 나쁜 값을 낸다.
        ///
        /// 🔴 보스가 뜨면 **붙는다.** 보스는 제자리에 있는데 거리를 벌리면
        ///    근접 무기가 영영 닿지 않는다 — 2026-08-22 측정에서 절단날 계열이
        ///    전부 "보스 못 깸"으로 나온 원인이 이거였다. 밸런스가 아니라 봇이었다.
        /// </summary>
        public static void Drive(RunDirector director, ShipController ship)
        {
            if (director == null || ship == null) return;

            Vector2 me = ship.transform.position;

            // 🔴 **경보가 울리면 보스 자리로 간다.**
            //    보스가 맵 중앙에 고정되면서, 등장 순간 배가 어디 있었느냐에 따라
            //    이동 시간이 크게 달라져 측정 편차가 커졌다 (2026-08-22).
            //    사람도 경보를 보면 그쪽으로 움직인다 — 봇이 가만히 있는 게 비현실적이었다.
            if (director.Phase == FloorPhase.BossIncoming && director.field != null)
            {
                Vector2 toBoss = director.field.BossCenter - me;
                ship.AimOverride = toBoss.sqrMagnitude > 4f
                    ? me + toBoss.normalized * 6f
                    : director.field.BossCenter;
                ship.ThrustOverride = true;
                return;
            }

            bool boss = director.Phase == FloorPhase.BossActive;

            // 🔴 **rev.7: 봇이 귀환과 입금을 알아야 한다.**
            //    rev.6까지 봇은 "가장 가까운 걸 계속 팬다"였다. 그건 이제
            //    *사람이 하지 않는 플레이*다 — 입금을 안 하면 레벨이 안 오르고
            //    기지가 수리되지 않으니, 그 봇으로 잰 값은 이 게임의 값이 아니다.
            if (!boss && ShouldReturn(director))
            {
                Vector2 home = director.field != null ? director.field.BaseCenter : Vector2.zero;
                Vector2 toHome = home - me;

                // 도킹 반경 안에 들어가면 자동 입금된다 — 그 뒤엔 평소 행동으로 돌아간다
                if (toHome.sqrMagnitude > 0.25f)
                {
                    ship.AimOverride = me + toHome.normalized * 6f;
                    ship.ThrustOverride = true;
                    return;
                }
            }

            var target = boss ? NearestBossPart(director, me, out float dist)
                              : BestThreat(director, me, out dist);

            if (target == null)
            {
                ship.AimOverride = Vector2.zero;   // 아무것도 없으면 중앙으로
                ship.ThrustOverride = true;
                return;
            }

            Vector2 to = (Vector2)target.transform.position - me;
            Vector2 dir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.right;

            // 보스전에는 붙고(근접 무기 사거리 안), 평소엔 무기 사거리 안 · 접촉 밖
            float keep = boss ? BossKeepDistance : KeepDistance;
            Vector2 aim = dist < keep ? me - dir * 6f : me + dir * 6f;

            ship.AimOverride = aim;
            ship.ThrustOverride = true;

            // 잡몹에 끼였을 때만 대시로 뺀다. 보스전에는 붙어 있어야 한다
            if (!boss && dist < DashOutDistance) ship.TryDash();
        }

        /// <summary>
        /// 🔴 지금 돌아가야 하는가 — **이 게임의 세 번째 결정**을 봇이 흉내 내는 부분.
        ///
        ///    화물이 차면 돌아간다. rev.8에서 기지 체력이 사라지면서
        ///    "기지가 위험하니 수리하러 간다"는 이유는 없어졌다.
        /// </summary>
        static bool ShouldReturn(RunDirector director)
        {
            if (director.CargoCount <= 0) return false;

            // 만재에 가까우면 간다
            if (director.CargoRatio >= ReturnAtCargoRatio) return true;

            return false;
        }

        /// <summary>
        /// 🔴 무엇부터 치울 것인가.
        ///
        ///    가장 가까운 것을 치는 건 rev.4까지의 답이었다. rev.7에서는 틀렸다 —
        ///    **기지에 가까운 쓰레기가 곧 손해**이기 때문이다.
        ///    그렇다고 기지에 제일 가까운 것만 쫓으면 배가 기지에 붙어 살게 되고,
        ///    그것도 사람이 하는 플레이가 아니다.
        ///
        ///    그래서 **위협(기지까지 거리) + 이동 비용(나까지 거리)**를 함께 본다.
        ///    계수는 찍은 값이다. 봇이 사람처럼 보이는지로만 검증할 수 있다.
        /// </summary>
        public static JunkPiece BestThreat(RunDirector director, Vector2 from, out float dist)
        {
            var field = director.field;
            Vector2 home = field != null ? field.BaseCenter : Vector2.zero;

            JunkPiece best = null;
            float bestScore = float.MaxValue;
            float bestSq = 0f;

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var j = field.Pieces[i];
                if (!j.Alive) continue;
                if (j.type != null && j.type.isHazard) continue;   // 위험물은 노리지 않는다

                Vector2 at = j.transform.position;
                float toMeSq = (at - from).sqrMagnitude;

                float score = (at - home).magnitude + Mathf.Sqrt(toMeSq) * TravelCostWeight;
                if (score >= bestScore) continue;

                bestScore = score; best = j; bestSq = toMeSq;
            }

            dist = best != null ? Mathf.Sqrt(bestSq) : 999f;
            return best;
        }

        /// <summary>화물이 이만큼 차면 돌아간다.</summary>
        public const float ReturnAtCargoRatio = 0.55f;

        /// <summary>위협 점수에서 이동 거리에 붙는 가중치.</summary>
        public const float TravelCostWeight = 0.6f;

        public const float KeepDistance = 3.2f;
        public const float BossKeepDistance = 1.6f;
        public const float DashOutDistance = 1.4f;

        public static JunkPiece NearestJunk(RunDirector director, Vector2 from, out float dist)
        {
            var field = director.field;
            JunkPiece best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var j = field.Pieces[i];
                if (!j.Alive) continue;
                if (j.type != null && j.type.isHazard) continue;   // 위험물은 노리지 않는다

                float sq = ((Vector2)j.transform.position - from).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq; best = j;
            }

            dist = best != null ? Mathf.Sqrt(bestSq) : 999f;
            return best;
        }

        public static JunkPiece NearestBossPart(RunDirector director, Vector2 from, out float dist)
        {
            var field = director.field;
            JunkPiece best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var j = field.Pieces[i];
                if (!j.Alive || !j.IsBossPart) continue;

                float sq = ((Vector2)j.transform.position - from).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq; best = j;
            }

            // 보스 부위가 안 보이면 평소처럼 잡몹을 상대한다
            if (best == null) return NearestJunk(director, from, out dist);

            dist = Mathf.Sqrt(bestSq);
            return best;
        }

        /// <summary>시뮬이 끝날 때 상태가 남지 않게 한다.</summary>
        public static void ResetEngaged() => Engaged = false;
    }
}
