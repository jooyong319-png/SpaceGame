using UnityEngine;
using SalvageRun.Data;

namespace SalvageRun.Run
{
    /// <summary>
    /// 보스의 방해 행동.
    ///
    /// 🔴 **보스가 지금까지 HP 큰 덩어리일 뿐이었다.** `BossKind` 6종이 데이터에만 있고
    ///    코드 어디에서도 안 읽혔다 — 맵을 클리어해도 "오래 때렸다" 말고는 남는 게 없었다.
    ///    브리프 §8의 차별점 중 하나가 **"끝이 있다"**인데, 그 끝이 밋밋하면 차별점이 아니다.
    ///
    /// 🔴 **제약: 쓰레기는 공격하지 않는다** (브리프 §10 안 할 것 목록).
    ///    투사체를 쏘거나 스킬을 쓰면 이 게임은 슈팅이 되고, "청소"라는 표현이 무너진다.
    ///    그래서 보스는 **때리지 않고 방해만 한다** — 밀어내고, 어지럽히고, 뺏어간다.
    ///    플레이어가 잃는 것은 체력이 아니라 **시간과 자리**다.
    /// </summary>
    public class BossBehaviour : MonoBehaviour
    {
        public RunDirector director;
        public StageField field;
        public ShipController ship;

        BossDef def;
        bool active;

        float tick;          // 주기형 행동 타이머
        float windup;        // 예고 시간 — 신호 없는 방해는 기습이다

        /// <summary>EMP가 무기 사거리를 줄이는 중인가. `WeaponRig`이 읽는다.</summary>
        public static float RangeChoke { get; private set; } = 1f;

        public void Begin(BossDef boss)
        {
            def = boss;
            active = boss != null;
            tick = 2.5f;
            windup = 0f;
            empLeft = 0f;
            RangeChoke = 1f;

            // 🔴 난수를 되감는다. 안 하면 **앞 런의 보스전 길이가 다음 런을 바꾼다** —
            //    2026-08-22 결정론 검사가 0.6% → 6.8%로 나빠진 원인이 이것이었다.
            //    새 기능을 넣을 때마다 "런 사이에 남는 게 없나"를 같이 봐야 한다.
            seed = SeedStart;
        }

        /// <summary>
        /// 보스전 종료. 🔴 **런이 시작될 때도 불린다** — 보스를 못 보고 끝난 런도
        ///    난수를 되감아야 다음 런이 앞 런에 영향받지 않는다.
        /// </summary>
        public void End()
        {
            active = false;
            def = null;
            empLeft = 0f;
            RangeChoke = 1f;
            seed = SeedStart;
        }

        void Update()
        {
            if (!active || def == null || director == null || ship == null) return;
            if (!director.FieldActive || RunDirector.WorldPaused) return;

            switch (def.kind)
            {
                case BossKind.Inert:     break;                    // 첫 보스는 해체만 가르친다
                case BossKind.Repulsor:  Repulsor(); break;
                case BossKind.Spewer:    Periodic(Spew, 3.2f); break;
                case BossKind.Emp:       Periodic(Emp, 5.0f); break;
                case BossKind.Devourer:  Devour(); break;
                case BossKind.Rift:      Periodic(RiftPulse, 2.4f); break;
            }
        }

        /// <summary>
        /// 🔴 주기형 행동은 **반드시 예고한다.**
        ///    예고 없는 방해는 회피 기회가 아니라 기습이고,
        ///    플레이어는 "왜 이렇게 됐는지" 모른 채 당하기만 한다.
        /// </summary>
        void Periodic(System.Action act, float interval)
        {
            tick -= Time.deltaTime;

            const float Warn = 0.9f;
            if (tick <= Warn && tick > 0f)
            {
                // 예고 — 보스 부위들이 밝게 맥동한다
                windup += Time.deltaTime;
                if (windup >= 0.12f)
                {
                    windup = 0f;
                    foreach (var p in BossParts())
                        Fx.Spark(p.transform.position, p.transform.localScale.x * 1.4f,
                                 new Color(1f, 0.75f, 0.35f, 0.7f), 0.18f);
                }
            }

            if (tick > 0f) return;
            tick = interval;
            windup = 0f;
            act();
        }

        System.Collections.Generic.IEnumerable<JunkPiece> BossParts()
        {
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (p.Alive && p.IsBossPart) yield return p;
            }
        }

        Vector2 Center()
        {
            Vector2 sum = Vector2.zero;
            int n = 0;
            foreach (var p in BossParts()) { sum += (Vector2)p.transform.position; n++; }
            return n > 0 ? sum / n : (Vector2)ship.transform.position;
        }

        // ==============================================================================
        //  6가지 방해
        // ==============================================================================

        /// <summary>반발장 — 배를 계속 밀어낸다. 붙어서 때리는 빌드가 불리해진다.</summary>
        void Repulsor()
        {
            Vector2 c = Center();
            Vector2 d = (Vector2)ship.transform.position - c;

            float dist = d.magnitude;
            if (dist < 0.01f || dist > 11f) return;

            // 가까울수록 세게 민다 — "밀려나는 벽"이 있는 느낌
            float push = def.interferePower * (1f - dist / 11f);
            ship.AddExternalForce(d.normalized * push);

            if (Random01() < 0.08f)
                Fx.Line(c, ship.transform.position, new Color(0.5f, 0.85f, 1f, 0.35f), 0.10f, 0.12f);
        }

        /// <summary>토해내기 — 위험물을 뱉는다. 자리가 좁아진다.</summary>
        void Spew()
        {
            Vector2 c = Center();
            int n = Mathf.Clamp(Mathf.RoundToInt(def.interferePower), 1, 6);

            for (int i = 0; i < n; i++)
            {
                float a = Random01() * Mathf.PI * 2f;
                field.SpawnHazardAt(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f);
            }

            Fx.Shockwave(c, 3.5f, new Color(0.6f, 1f, 0.5f, 0.8f), 0.35f);
        }

        /// <summary>EMP — 무기 사거리를 잠깐 줄인다. 거리를 두던 빌드가 붙어야 한다.</summary>
        void Emp()
        {
            RangeChoke = Mathf.Clamp(1f - def.interferePower * 0.06f, 0.45f, 0.95f);
            empLeft = 2.6f;

            Fx.Shockwave(Center(), 9f, new Color(0.6f, 0.7f, 1f, 0.9f), 0.5f);
            director.AddPopup(ship.transform.position, "출력 저하", new Color(0.7f, 0.8f, 1f));
        }

        float empLeft;

        /// <summary>포식 — 주변 파편을 자기가 빨아들여 뺏어간다. 벌이가 줄어든다.</summary>
        void Devour()
        {
            Vector2 c = Center();
            float r = 7f + def.interferePower;
            float r2 = r * r;

            for (int i = 0; i < field.Fragments.Count; i++)
            {
                var f = field.Fragments[i];
                if (!f.Alive || f.rushing) continue;
                if (((Vector2)f.transform.position - c).sqrMagnitude > r2) continue;

                f.Attract(c, def.interferePower * 4f);
            }

            if (Random01() < 0.12f)
            {
                float a = Random01() * Mathf.PI * 2f;
                Fx.Mote(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, transform,
                        new Color(1f, 0.5f, 0.8f, 0.8f), 0.4f);
            }
        }

        /// <summary>균열 — 위험물을 계속 뿜고 배를 끌어당긴다. 마지막 맵의 보스.</summary>
        void RiftPulse()
        {
            Spew();

            Vector2 c = Center();
            Vector2 d = c - (Vector2)ship.transform.position;
            if (d.sqrMagnitude > 0.01f) ship.AddExternalForce(d.normalized * def.interferePower * 2.5f);
        }

        void LateUpdate()
        {
            if (empLeft <= 0f) return;

            empLeft -= Time.deltaTime;
            if (empLeft <= 0f) RangeChoke = 1f;
        }

        /// <summary>시드 고정 난수 — 시뮬 재현성을 위해 UnityEngine.Random을 쓰지 않는다.</summary>
        const float SeedStart = 7.3f;
        float seed = SeedStart;
        float Random01()
        {
            seed = (seed * 16807f) % 2147483647f;
            return seed / 2147483647f;
        }
    }
}
