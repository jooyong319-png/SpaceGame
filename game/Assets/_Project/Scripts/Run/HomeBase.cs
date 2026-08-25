using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 모선(정비소).
    ///
    /// 🔴 **rev.12 — 기지는 목적이 아니라 거점으로 돌아왔다.**
    ///
    ///    rev.7~11에서 기지는 연료가 닳고, 입금을 받고, 지역을 옮겨 다니는
    ///    **게임의 목적** 그 자체였다. 2026-08-23 사장님 결정으로 rev.4~5의
    ///    뱀서 구조로 되돌아오면서, 기지가 하던 일 대부분이 사라졌다.
    ///
    ///    지금 기지가 하는 일은 둘뿐이다:
    ///    · **스스로 쏜다** — 레벨업 카드로 얻는 포탑. 있으면 근처가 안전해진다
    ///    · **연료를 채워 준다** — 반경 안에 있으면 천천히 회복된다.
    ///      연료가 곧 체력이므로 이게 이 게임의 유일한 회복 지점이다
    ///
    /// 🔴 회복을 '기지 근처'에 묶은 이유: 그래야 지도에 **의미 있는 자리**가 하나 생긴다.
    ///    아무 데서나 회복되면 위치가 결정이 아니게 되고, 뱀서에서 위치는 유일한 결정이다.
    /// </summary>
    public class HomeBase : MonoBehaviour
    {
        public RunDirector director;
        public StageField field;

        public SpriteRenderer body;
        public Transform shieldRing;

        /// <summary>기지 반경. 이 안에 있으면 연료가 찬다.</summary>
        public float Radius = 4.5f;

        /// <summary>초당 회복량. 붙어 있으면 꽤 빠르게 찬다 — 대신 그동안 못 캔다.</summary>
        public float RepairPerSecond = 12f;

        /// <summary>지금 배가 기지 안에 있는가. HUD가 읽는다.</summary>
        public bool ShipInside { get; private set; }

        public void Begin()
        {
            turretCd = 0f;
            repairFx = 0f;
            ShipInside = false;
        }

        void Update()
        {
            if (director == null || field == null) return;
            if (!director.FieldActive || RunDirector.WorldPaused) return;

            UpdateRepair();
            FireTurret();
            UpdateVisual();
        }

        // ---------------------------------------------------------------- 수리

        void UpdateRepair()
        {
            var ship = director.ship;
            if (ship == null || !ship.gameObject.activeSelf) { ShipInside = false; return; }

            float r = Radius;
            ShipInside = ((Vector2)ship.transform.position - (Vector2)transform.position).sqrMagnitude
                         <= r * r;

            if (!ShipInside || ship.Fuel >= ship.FuelMax) return;

            ship.Refuel(RepairPerSecond * Time.deltaTime);

            // 채워지는 게 보여야 "여기가 안전한 자리"라는 걸 배운다
            repairFx -= Time.deltaTime;
            if (repairFx <= 0f)
            {
                repairFx = 0.12f;
                Fx.Mote(transform.position, ship.transform, new Color(0.6f, 1f, 0.85f, 0.8f), 0.25f);
            }
        }

        float repairFx;

        // ---------------------------------------------------------------- 포탑

        /// <summary>
        /// 🔴 **기지가 스스로 싸운다** (2026-08-21 요청:
        ///    *"기지도 공격 기능 있게 만들어주고 레벨업 보상으로 기지 무기 강화"*).
        ///
        ///    🔴 다만 **처음부터 쏘지는 않는다.** 레벨업 카드로 얻어야 생긴다.
        ///       처음부터 있으면 초반이 쉬워지고 긴장이 사라진다 — 보상이어야 한다.
        /// </summary>
        void FireTurret()
        {
            var st = director.Stats;
            if (st == null || st.baseTurretLevel <= 0) return;

            turretCd -= Time.deltaTime;
            if (turretCd > 0f) return;

            int lv = st.baseTurretLevel;
            turretCd = Mathf.Max(0.12f, (0.85f - lv * 0.045f) * st.baseTurretHaste);

            float range = (9f + lv * 1.1f) * st.baseTurretRange;
            float dmg   = (14f + lv * 9f) * st.baseTurretPower * Tuning.TurretPowerMul;
            int barrels = Mathf.Max(1, st.baseTurretCount);

            float r2 = range * range;
            int fired = 0;

            for (int shot = 0; shot < barrels; shot++)
            {
                JunkPiece best = null;
                float bestSq = float.MaxValue;

                for (int i = 0; i < field.Pieces.Count; i++)
                {
                    var p = field.Pieces[i];
                    if (!p.Alive || p.IsBossPart || p.TurretMark) continue;

                    float sq = ((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude;
                    if (sq > r2 || sq >= bestSq) continue;

                    bestSq = sq; best = p;
                }

                if (best == null) break;

                best.TurretMark = true;          // 같은 발사에서 두 번 겨누지 않는다
                Vector2 at = best.transform.position;

                Fx.Line(transform.position, at, new Color(0.55f, 0.95f, 1f), 0.10f, 0.12f);
                Fx.Spark(at, 0.35f, new Color(0.7f, 1f, 1f), 0.14f);

                // Chip()이 다 깎이면 스스로 BreakJunk까지 한다 — 여기서 또 부르면 두 번 터진다
                best.Chip(dmg);
                fired++;
            }

            for (int i = 0; i < field.Pieces.Count; i++) field.Pieces[i].TurretMark = false;

            if (fired > 0) Juice.Chip(0.35f);
        }

        float turretCd;

        // ---------------------------------------------------------------- 표시

        void UpdateVisual()
        {
            if (body != null)
            {
                float pulse = ShipInside ? 0.25f + 0.15f * Mathf.Sin(Time.time * 7f) : 0f;
                var c = new Color(0.55f, 0.75f, 0.95f);
                body.color = Color.Lerp(c, Color.white, pulse);
            }

            if (shieldRing == null) return;

            float scale = Radius * 2f;
            shieldRing.localScale = new Vector3(scale, scale, 1f);

            var sr = shieldRing.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            // 배가 들어와 있으면 고리가 밝아진다 — "지금 채워지는 중"이 눈으로 읽힌다
            sr.color = ShipInside
                ? new Color(0.5f, 1f, 0.85f, 0.22f + 0.12f * Mathf.Sin(Time.time * 6f))
                : new Color(0.45f, 0.85f, 1f, 0.14f);
        }
    }
}
