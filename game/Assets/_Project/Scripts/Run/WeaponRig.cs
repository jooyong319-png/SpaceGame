using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Data;
using SalvageRun.Meta;

namespace SalvageRun.Run
{
    /// <summary>
    /// 무기를 굴린다. 🔴 한 런에 **둘만** 갖는다 (우주선이 준 것 + 얻은 것 하나).
    ///
    /// 🔴 **무기마다 전용 코드를 쓰지 않는다.**
    ///    무기 12종을 각각 손으로 짜면 20종이 될 때 이 파일이 손댈 수 없게 된다.
    ///    무기는 `WeaponDef`(데이터)이고, 여기는 **패턴 11가지**만 구현한다.
    ///    새 무기를 넣는 일 = 데이터 한 줄 추가.
    ///
    /// 피해는 전부 <see cref="Hit"/>를 지난다. 특성(traits)과 조합(combo)이
    /// 거기 한 곳에서 붙으므로, 새 특성을 넣어도 패턴 코드는 안 건드린다.
    /// </summary>
    public class WeaponRig : MonoBehaviour
    {
        public RunConfig config;
        public RunStats stats;
        public ShipController ship;
        public StageField field;
        public RunDirector director;
        public GameContent content;
        public Sprite sprite;

        // ---- 도트 (PixelArt). 없으면 흰 사각형으로 떨어진다 ----
        public Sprite bladeSprite;
        public Sprite glowSprite;
        public Sprite ringSprite;

        // ---------------------------------------------------------------- 상태

        readonly float[] cooldown = new float[Weapons.Count];
        readonly float[] clock = new float[Weapons.Count];
        readonly float[] subClock = new float[Weapons.Count];

        /// <summary>궤도체(절단날·방벽) — 무기별로 따로 돈다.</summary>

        class Shot
        {
            public Transform tr;
            public Vector2 vel;
            public float life, dmg;
            public int pierce;
            public WeaponKind owner;
            public int level;
            public bool resolved;
            public bool returning;      // 부메랑
            public float travel, maxTravel;
            public float trailCd;
            public float spin;
            public int generation;      // 0 = 원본, 1 = 갈라져 나온 것 (무한 분열 방지)
        }
        readonly List<Shot> shots = new List<Shot>();

        /// <summary>지뢰 · 중력우물 · 남은 장판을 하나로 다룬다 — 셋 다 "자리를 차지하는 것"이다.</summary>
        class Zone
        {
            public Transform tr;
            public Vector2 at;
            public float life, radius, dps, pull;
            public float armDelay;      // 지뢰: 이 시간 뒤부터 터진다
            public float blink;         // 깜빡임 · 입자 타이머
            public bool detonateOnEnd;
            public float detonateDamage;

            /// <summary>
            /// 🔴 이 구역이 **연쇄 폭발이 낳은 것**인가.
            ///    메아리가 또 메아리를 낳으면 반경이 매 번 1.2배로 불어나 무한대가 된다.
            ///    메아리는 한 번까지다.
            /// </summary>
            public bool isEcho;
            public WeaponKind owner;
            public int level;
        }
        readonly List<Zone> zones = new List<Zone>();

        /// <summary>구역 반경 상한. 화면보다 큰 장판은 조작이 아니라 배경이다.</summary>
        const float MaxZoneRadius = 40f;
        readonly List<Transform> fx = new List<Transform>();
        readonly List<float> fxLife = new List<float>();

        const float RngStart = 3.7f;
        readonly float[] trailCd = new float[Weapons.Count];
        readonly float[] gunCd = new float[Weapons.Count];
        readonly float[] moteCd = new float[Weapons.Count];

        float rngSeed = RngStart;
        bool inEchoExplosion;

        /// <summary>
        /// 🔴 런이 시작될 때마다 난수를 되감는다.
        ///
        ///    이게 없으면 **앞 런이 얼마나 길었는지가 다음 런의 결과를 바꾼다.**
        ///    `StageField`와 `RunDirector`는 런마다 시드를 다시 잡는데 무기만 안 잡고 있었다.
        ///    2026-08-22 시뮬에서 같은 빌드가 실행마다 "클리어"와 "못 깸"을 오갔고,
        ///    원인이 이것이었다 — **밸런스가 아니라 측정이 흔들린 것**이다.
        /// </summary>
        public void ResetRandom()
        {

            rngSeed = RngStart;
            for (int i = 0; i < cooldown.Length; i++)
            {
                cooldown[i] = 0f; clock[i] = 0f; subClock[i] = 0f;
                trailCd[i] = 0f; gunCd[i] = 0f; moteCd[i] = 0f;
            }

            for (int i = 0; i < shots.Count; i++) { shots[i].life = 0f; shots[i].tr.gameObject.SetActive(false); }
            for (int i = 0; i < zones.Count; i++) { zones[i].life = 0f; zones[i].tr.gameObject.SetActive(false); }
            for (int i = 0; i < fxLife.Count; i++) { fxLife[i] = 0f; fx[i].gameObject.SetActive(false); }

            inEchoExplosion = false;
        }

        // ---------------------------------------------------------------- 준비

        /// <summary>레벨이 바뀔 때마다 불린다. 궤도체 수처럼 '개수'가 변하는 것만 여기서 맞춘다.</summary>
        public void Rebuild()
        {
            if (content == null || stats == null || config == null) return;

            for (int i = 0; i < Weapons.Count; i++)
            {
                var kind = (WeaponKind)i;
                var def = content.Weapon(kind);
                if (def == null) continue;

                int lv = stats.LevelOf(kind);

            }
        }

        /// <summary>레벨에 따른 개수. 특성 ExtraProjectile와 영구 강화가 더 얹힌다.</summary>
        int CountOf(WeaponDef d, int lv)
        {
            int n = d.count;
            if (d.countEveryLevels > 0) n += (lv - 1) / d.countEveryLevels;
            n += Mathf.RoundToInt(d.TraitValue(WeaponTrait.ExtraProjectile, lv));
            n += ExtraCountFromMeta(d);
            return Mathf.Max(1, n);
        }

        /// <summary>
        /// 🔴 **무기별 칸이 먼저다** (2026-08-23). 패턴 보너스는 남아 있는 카드용이라
        ///    지금은 거의 0이지만, 있으면 같이 더한다 — 공용과 무기별이 겹쳐도 되게.
        /// </summary>
        int ExtraCountFromMeta(WeaponDef d)
        {
            if (stats == null) return 0;

            int n = stats.wCount[(int)d.kind];
            switch (d.pattern)
            {
                case WeaponPattern.Projectile:
                case WeaponPattern.Boomerang: n += stats.projectileCountBonus; break;
                case WeaponPattern.Chain:     n += stats.chainTargetBonus; break;
            }
            return n;
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>
        /// 🔴 조합이 열렸으면 **색이 바뀐다.**
        ///    조합은 이 게임의 차별점인데, 열려도 화면이 똑같으면 열린 줄을 모른다.
        ///    이펙트를 따로 만드는 대신 색을 조합 색 쪽으로 섞어 **한눈에 달라 보이게** 한다.
        ///    (임시 처방이다. 조합마다 고유 이펙트가 최종 목표 — 🟡 [[weapons]])
        /// </summary>
        Color Tint(Color c)
        {
            if (director == null || director.ActiveCombo == null) return c;
            var cc = director.ActiveCombo.color;
            return new Color(
                Mathf.Lerp(c.r, cc.r, 0.45f),
                Mathf.Lerp(c.g, cc.g, 0.45f),
                Mathf.Lerp(c.b, cc.b, 0.45f), c.a);
        }

        Transform MakeSprite(string name, Color c, int order, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            go.transform.localScale = scale;
            return go.transform;
        }

        // ---------------------------------------------------------------- 매 프레임

        void Update()
        {
            if (config == null || field == null || ship == null || director == null
                || stats == null || content == null) return;

            // 🔴 **격침 중에는 무기도 죽는다** (2026-08-22 플레이 피드백:
            //    *"플레이어가 죽으면 파괴된 거가 공격이 됨"*).
            //
            //    `WeaponRig`은 배와 **별개 오브젝트**라, `Wreck()`이 배를 꺼도
            //    이쪽은 그대로 돌면서 **배의 마지막 좌표에서 계속 쐈다.**
            //    부서진 배가 유령처럼 싸우는 그림이 된다 —
            //    격침의 대가(그동안 아무것도 못 한다)가 통째로 사라진다.
            bool active = director.FieldActive && ship.gameObject.activeSelf;

            UpdateShots(active);
            UpdateZones(active);
            UpdateFx();


            if (!active) return;

            Vector2 shipPos = ship.transform.position;

            // 🔴 **조준은 프레임당 한 번만 계산한다.** 아래 무기 루프가 각자 찾으면
            //    후반(동시 300개)에 무기 수만큼 전수 순회가 돈다
            UpdateAutoAim(shipPos);

            for (int i = 0; i < Weapons.Count; i++)
            {
                var kind = (WeaponKind)i;
                int lv = stats.LevelOf(kind);
                if (lv <= 0) continue;

                var def = content.Weapon(kind);
                if (def == null) continue;

                RunWeapon(def, lv, shipPos);
            }
        }

        // ---------------------------------------------------------------- 자동 조준

        /// <summary>
        /// 🔴 **무조건 가장 가까운 것을 친다** (2026-08-26 사장님 지시:
        ///    *"이거 그냥 무조건 가까이 있는 것만 때리게 해줘"*).
        ///
        ///    앞서 두 가지를 얹어 뒀었는데 둘 다 뺐다:
        ///
        ///    · **표적 붙잡기** — 죽거나 16유닛을 벗어날 때까지 물고 있었다.
        ///      떨림을 막으려던 것인데, 실제로는 **코앞의 것을 두고 멀리 있는 걸 계속 쏘는**
        ///      그림이 나온다. 플레이어 눈에는 그게 고장으로 보인다
        ///    · **위험물 후순위** — 지금 위험물은 닿아도 아프지 않다(무적).
        ///      아프지 않은 것을 피할 이유가 없다
        ///
        ///    규칙이 하나면 플레이어가 배울 게 없다: **가까운 것부터.**
        ///
        ///    ⚠️ 거리가 비슷한 둘 사이에서 표적이 오갈 수 있다.
        ///       그건 실제로 둘 다 사거리 안이라는 뜻이므로 어느 쪽을 쏘든 손해가 아니다.
        /// </summary>
        void UpdateAutoAim(Vector2 shipPos)
        {
            aimTarget = null;

            float bestSq = float.MaxValue;

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;

                float sq = ((Vector2)p.transform.position - shipPos).sqrMagnitude;
                if (sq >= bestSq) continue;

                bestSq = sq; aimTarget = p;
            }

            if (aimTarget != null) aimPoint = aimTarget.transform.position;
            else aimPoint = shipPos + lastAimDir * 6f;    // 아무것도 없으면 보던 쪽으로

            RememberAimDir(shipPos);
        }

        void RememberAimDir(Vector2 shipPos)
        {
            Vector2 d = aimPoint - shipPos;
            if (d.sqrMagnitude > 0.0001f) lastAimDir = d.normalized;
        }

        /// <summary>지금 겨누고 있는 자리. 표적이 없으면 배 앞쪽 한 점.</summary>
        public Vector2 AimPoint => aimPoint;

        /// <summary>지금 겨누는 방향(단위 벡터). 표적이 없어도 항상 값이 있다.</summary>
        public Vector2 AimDir => lastAimDir;

        JunkPiece aimTarget;
        Vector2 aimPoint;
        Vector2 lastAimDir = Vector2.right;

        void RunWeapon(WeaponDef d, int lv, Vector2 shipPos)
        {
            // 🔴 **남은 셋은 전부 주기형이다.** 쿨다운이 돌아야 나간다.
            //    (2026-08-23까지는 여기 위에 "상시형" 갈래가 하나 더 있었다 —
            //     궤도·장판·레이저처럼 쿨다운 없이 매 프레임 도는 것들이었고,
            //     그 무기들이 전부 빠지면서 갈래째 없어졌다)
            if (!Tick(d, lv)) return;

            switch (d.pattern)
            {
                case WeaponPattern.Projectile:  FireProjectile(d, lv, shipPos, false); break;
                case WeaponPattern.Boomerang:   FireProjectile(d, lv, shipPos, true); break;
                case WeaponPattern.Chain:       RunChain(d, lv, shipPos); break;
            }
        }

        /// <summary>쿨다운. 특성 DoubleTap이면 한 주기에 두 번 나가도록 절반으로 줄인다.</summary>
        bool Tick(WeaponDef d, int lv)
        {
            int i = (int)d.kind;
            cooldown[i] -= Time.deltaTime;
            if (cooldown[i] > 0f) return false;

            float cd = d.cooldown * Mathf.Pow(d.cooldownPerLevel, lv - 1)
                     * stats.CooldownOf(d.kind) * stats.BurstHasteMul;
            if (d.HasTraitAt(WeaponTrait.DoubleTap, lv)) cd *= 0.5f;

            // 🔴 **N% 확률로 한 번 더** (테크트리 `ProcDoubleShot`).
            //    쿨다운을 아주 짧게 만들어 다음 프레임에 또 나가게 한다 —
            //    별도 경로를 만들면 특성·조합이 그 경로를 안 타서 조용히 어긋난다
            if (stats.procDoubleShot > 0f && Rand() < stats.procDoubleShot) cd = 0.02f;
            cooldown[i] = Mathf.Max(0.08f, cd);
            return true;
        }

        /// <summary>🔴 단발성 버프(카드)가 여기에 곱해진다 — 한 곳만 지나게 해서 빠뜨릴 일이 없게.</summary>
        // 🔴 **공용 × 무기별.** 화력 가지는 어느 무기든 올리고,
        //    무기 가지는 그 무기만 올린다 (2026-08-23). `PowerOf`가 둘을 곱한다.
        float Damage(WeaponDef d, int lv)
            => (d.damage + d.damagePerLevel * (lv - 1)) * stats.PowerOf(d.kind) * stats.BurstPowerMul;

        /// <summary>🔴 보스의 EMP가 사거리를 줄이고, 단발성 '확장'이 늘린다.</summary>
        float Range(WeaponDef d, int lv)
            => (d.range + d.rangePerLevel * (lv - 1)) * stats.RangeOf(d.kind)
             * stats.BurstSizeMul * BossBehaviour.RangeChoke;

        /// <summary>압축 붕괴 예고 시간. 이보다 짧으면 예고가 아니라 그냥 번쩍임이다.</summary>
        const float CollapseTell = 0.40f;
        float collapseCd;

        // ---- Projectile / Boomerang ----
        void FireProjectile(WeaponDef d, int lv, Vector2 shipPos, bool boomerang)
        {
            // 🔴 커서가 아니라 **물고 있는 표적** 쪽 (2026-08-23)
            Vector2 dir = lastAimDir;

            int count = CountOf(d, lv);
            float dmg = Damage(d, lv);

            int pierce = d.pierce + Mathf.RoundToInt(d.TraitValue(WeaponTrait.ExtraPierce, lv))
                       + stats.pierceBonus + stats.wPierce[(int)d.kind];
            if (d.HasTraitAt(WeaponTrait.Pierceless, lv)) pierce = 999;
            if (stats.HasCombo(ComboEffect.PierceP)) pierce += 6;      // ★ 관통 정렬

            for (int i = 0; i < count; i++)
            {
                float spread = (count == 1) ? 0f : (i - (count - 1) * 0.5f) * 0.16f;
                var dd = Rotate(dir, spread);
                Fire(d, lv, shipPos + dd * 0.8f, dd * d.projectileSpeed * stats.shotSpeedMul,
                     dmg, pierce, Range(d, lv));
            }
            Juice.Chip(0.8f);
        }

        Shot Fire(WeaponDef d, int lv, Vector2 pos, Vector2 vel, float dmg, int pierce, float maxTravel)
        {
            Shot s = null;
            for (int i = 0; i < shots.Count; i++) if (shots[i].life <= 0f) { s = shots[i]; break; }
            if (s == null)
            {
                s = new Shot { tr = MakeSprite("Shot", d.color, 12, new Vector3(0.7f, 0.16f, 1f)) };
                shots.Add(s);
            }

            s.tr.gameObject.SetActive(true);
            s.tr.GetComponent<SpriteRenderer>().color = d.color;
            s.tr.position = pos;
            s.tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            s.vel = vel;
            s.life = d.pattern == WeaponPattern.Boomerang ? 4f : 1.4f;
            s.pierce = pierce;
            s.dmg = dmg;
            s.owner = d.kind;
            s.level = lv;
            s.resolved = false;
            s.returning = false;
            s.travel = 0f;
            s.maxTravel = maxTravel;
            s.trailCd = 0f;
            s.spin = 0f;
            s.generation = 0;
            return s;
        }

        void UpdateShots(bool active)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                var s = shots[i];
                if (s.life <= 0f) { if (s.tr.gameObject.activeSelf) s.tr.gameObject.SetActive(false); continue; }
                if (!active) { s.life = 0f; continue; }

                var def = content.Weapon(s.owner);
                if (def == null) { s.life = 0f; continue; }

                s.life -= Time.deltaTime;

                if (def.pattern == WeaponPattern.Boomerang)
                {
                    s.travel += s.vel.magnitude * Time.deltaTime;
                    if (!s.returning && s.travel >= s.maxTravel)
                    {
                        s.returning = true;
                        // 도탄 — 돌아오는 길에 한 번 더 벤다
                        if (def.HasTraitAt(WeaponTrait.Ricochet, s.level)) s.pierce += 3;
                    }
                    if (s.returning)
                    {
                        Vector2 back = (Vector2)ship.transform.position - (Vector2)s.tr.position;
                        if (back.sqrMagnitude < 0.6f) { s.life = 0f; continue; }
                        s.vel = Vector2.Lerp(s.vel, back.normalized * def.projectileSpeed, 6f * Time.deltaTime);
                    }
                }
                else if (def.HasTraitAt(WeaponTrait.Homing, s.level))
                {
                    var target = Nearest((Vector2)s.tr.position, 9f);
                    if (target != null)
                    {
                        Vector2 want = ((Vector2)target.transform.position - (Vector2)s.tr.position).normalized
                                     * s.vel.magnitude;
                        float turn = def.TraitValue(WeaponTrait.Homing, s.level);
                        s.vel = Vector2.Lerp(s.vel, want, turn * Time.deltaTime);
                    }
                }

                s.tr.position += (Vector3)(s.vel * Time.deltaTime);

                // 🔴 부메랑은 **회전한다.** 날아가는 방향으로 누워 있으면 작살과 구분이 안 된다
                if (def.pattern == WeaponPattern.Boomerang)
                {
                    s.spin += 900f * Time.deltaTime;
                    s.tr.rotation = Quaternion.Euler(0f, 0f, s.spin);
                }
                else
                {
                    s.tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.vel.y, s.vel.x) * Mathf.Rad2Deg);
                }

                // 🔴 꼬리 — 막대 하나가 날아가는 것과 완전히 다르게 보인다
                s.trailCd -= Time.deltaTime;
                if (s.trailCd <= 0f)
                {
                    s.trailCd = 0.04f;
                    Fx.Trail(s.tr.position, s.vel, Tint(Fade(def.color, 0.55f)));
                }

                for (int j = 0; j < field.Pieces.Count && s.pierce > 0; j++)
                {
                    var p = field.Pieces[j];
                    if (!p.Alive) continue;

                    float touch = 0.5f + p.transform.localScale.x * 0.5f;
                    if (((Vector2)p.transform.position - (Vector2)s.tr.position).sqrMagnitude > touch * touch) continue;

                    Hit(p, s.dmg, def, s.level, s.tr.position);
                    s.pierce--;

                    Fx.Spark(s.tr.position, 0.8f, Tint(Fade(def.color, 0.9f)), 0.13f);

                    // ★ 분열 원반 — 명중할 때마다 작은 것이 갈라져 나간다
                    if (def.HasTraitAt(WeaponTrait.Split, s.level) && s.generation == 0)
                    {
                        int shards = Mathf.Max(1, Mathf.RoundToInt(def.TraitValue(WeaponTrait.Split, s.level)));
                        for (int k = 0; k < shards; k++)
                        {
                            float a = (k / (float)shards) * Mathf.PI * 2f + 0.6f;
                            var dd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            var child = Fire(def, s.level, s.tr.position, dd * def.projectileSpeed * 0.8f,
                                             s.dmg * 0.5f, 1, Range(def, s.level) * 0.5f);
                            if (child != null) child.generation = 1;
                        }
                    }

                    if (stats.HasCombo(ComboEffect.PierceShock))        // ★ 번개 관통
                        ArcFrom(p.transform.position, 2, 5f * stats.rangeMul, s.dmg * 0.5f, def, s.level);

                    if (stats.HasCombo(ComboEffect.PierceGravity))      // ★ 견인 관통
                        p.Tug(ship.transform.position, 5.5f);
                }

                bool spent = s.pierce <= 0 || s.life <= 0f;
                if (spent && !s.resolved)
                {
                    s.resolved = true;
                    ResolveShotEnd(s.tr.position, s.dmg, def, s.level);
                }
                if (s.pierce <= 0) s.life = 0f;
            }
        }

        /// <summary>
        /// 발사체가 멈춘 자리에서 일어나는 일. 🔴 **조합에 따라 다르다** —
        /// 같은 무기인데 무엇과 짝지었느냐로 성격이 바뀌는 게 이 구조의 핵심이다.
        /// </summary>
        void ResolveShotEnd(Vector2 at, float dmg, WeaponDef d, int lv)
        {
            if (stats.HasCombo(ComboEffect.PierceBlast))            // ★ 작렬 관통
                Explode(at, 2.6f * stats.rangeMul, dmg * 1.3f, d, lv);

            if (stats.HasCombo(ComboEffect.PierceField))            // ★ 회수 관통
                AddZone(at, 2.4f * stats.rangeMul, dmg * 1.1f, 1.6f, 0f, d, lv, Fade(d.color, 0.28f));

            if (stats.HasCombo(ComboEffect.CutPierce))              // ★ 절개
                AddZone(at, 1.6f * stats.rangeMul, dmg * 0.8f, 2.4f, 0f, d, lv, Fade(d.color, 0.22f));
        }

        // ---- Chain: 연쇄 방전 ----
        void RunChain(WeaponDef d, int lv, Vector2 shipPos)
        {
            int targets = CountOf(d, lv);
            if (stats.HasCombo(ComboEffect.ShockShock)) targets *= 2;   // ★ 과부하

            ArcFrom(shipPos, targets, Range(d, lv), Damage(d, lv), d, lv);
            Juice.Chip(1f);
        }

        void ArcFrom(Vector2 origin, int targets, float range, float dmg, WeaponDef d, int lv)
        {
            float r2 = range * range;
            Vector2 from = origin;

            for (int t = 0; t < targets; t++)
            {
                JunkPiece best = null;
                float bestSq = r2;

                for (int i = 0; i < field.Pieces.Count; i++)
                {
                    var p = field.Pieces[i];
                    if (!p.Alive || p.ArcMark) continue;

                    float sq = ((Vector2)p.transform.position - from).sqrMagnitude;
                    if (sq >= bestSq) continue;
                    bestSq = sq; best = p;
                }
                if (best == null) break;

                Vector2 to = best.transform.position;
                best.ArcMark = true;
                Fx.Line(from, to, Tint(d.color), 0.16f, 0.18f);
                Fx.Spark(to, 0.9f, Tint(Fade(d.color, 0.9f)), 0.14f);
                Hit(best, dmg, d, lv, to);
                from = to;
            }

            for (int i = 0; i < field.Pieces.Count; i++) field.Pieces[i].ArcMark = false;
        }

        // ================================================================ 구역(장판)

        Zone AddZone(Vector2 at, float radius, float dps, float life, float pull,
                     WeaponDef d, int lv, Color color)
        {
            // 🔴 **마지막 방벽.** 반경이 무한/NaN이면 localScale 대입이 유니티 에러가 되고,
            //    그 뒤로 쓰레기 좌표까지 NaN으로 오염된다 (2026-08-21 시뮬에서 실제로 그랬다).
            //    원인은 따로 고치되, 여기서도 막는다 — 한 군데 실수가 판 전체를 멈추면 안 된다.
            if (float.IsNaN(radius) || float.IsInfinity(radius)) radius = 1f;
            radius = Mathf.Clamp(radius, 0.1f, MaxZoneRadius);

            Zone z = null;
            for (int i = 0; i < zones.Count; i++) if (zones[i].life <= 0f) { z = zones[i]; break; }
            if (z == null)
            {
                z = new Zone { tr = MakeSprite("Zone", color, -1, Vector3.one) };
                zones.Add(z);
            }

            z.at = at; z.radius = radius; z.dps = dps; z.life = life; z.pull = pull;
            z.armDelay = 0f; z.detonateOnEnd = false; z.detonateDamage = 0f;
            z.isEcho = false;
            z.owner = d.kind; z.level = lv;

            z.tr.gameObject.SetActive(true);
            z.tr.position = at;
            z.tr.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            // (반경은 AddZone 진입에서 이미 유한값으로 걸러진다)
            z.tr.GetComponent<SpriteRenderer>().color = color;
            return z;
        }

        void UpdateZones(bool active)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.life <= 0f) { if (z.tr.gameObject.activeSelf) z.tr.gameObject.SetActive(false); continue; }
                if (!active) { z.life = 0f; continue; }

                z.life -= Time.deltaTime;

                if (z.armDelay > 0f) { z.armDelay -= Time.deltaTime; continue; }

                var def = content.Weapon(z.owner);
                if (def == null) { z.life = 0f; continue; }

                if (z.pull > 0f)
                {
                    PullAround(z.at, z.radius * 1.6f, z.pull);

                    // 🔴 중력 우물도 빨아들이는 게 보여야 한다 — 나선으로 들어오는 입자
                    z.blink -= Time.deltaTime;
                    if (z.blink <= 0f)
                    {
                        z.blink = 0.05f;
                        for (int m = 0; m < 2; m++)
                        {
                            float a = Rand() * Mathf.PI * 2f;
                            Vector2 from2 = z.at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * z.radius * 1.3f;
                            Fx.Mote(from2, z.tr, Fade(def.color, 0.9f), 0.4f);
                        }
                    }
                }

                if (z.dps > 0f) HitAround(z.at, z.radius, z.dps * Time.deltaTime, def, z.level);

                if (z.life <= 0f && z.detonateOnEnd)
                {
                    // 🔴 **메아리는 메아리를 낳지 않는다.**
                    //    이게 없으면 연쇄 폭발(★ BlastBlast)이 매 번 반경 1.2배짜리 구역을
                    //    새로 낳고, 그게 또 낳아서 **반경이 지수적으로 불어나 무한대**가 된다.
                    //    `inEchoExplosion` 가드는 *같은 호출 안*만 막는다 —
                    //    메아리는 **다음 프레임에** 터지므로 그 가드로는 안 잡힌다.
                    Explode(z.at, z.radius * 1.2f, z.detonateDamage, def, z.level, !z.isEcho);
                }
            }
        }

        // ================================================================ 피해 한 곳

        void Explode(Vector2 at, float r, float dmg, WeaponDef d, int lv, bool allowEcho = true)
        {
            if (dmg <= 0f) return;
            if (float.IsNaN(r) || float.IsInfinity(r)) return;
            r = Mathf.Clamp(r, 0.1f, MaxZoneRadius);

            HitAround(at, r, dmg, d, lv);

            // 🔴 원이 커지는 게 아니라 **고리가 퍼진다.** 폭발이라는 게 읽혀야 한다
            Fx.Shockwave(at, r, Fade(d.color, 0.85f));
            Fx.Spark(at, r * 0.8f, Fade(d.color, 0.7f));

            if (stats.HasCombo(ComboEffect.ShockBlast))                 // ★ 감전 폭탄
                ArcFrom(at, 3, r * 2.2f, dmg * 0.45f, d, lv);

            float kb = d.TraitValue(WeaponTrait.Knockback, lv);
            if (kb > 0f) PushAround(at, r, kb);

            // ★ 연쇄 폭발 — 한 박자 뒤에 한 번 더. 재진입을 막지 않으면 무한히 갈라진다
            if (allowEcho && stats.HasCombo(ComboEffect.BlastBlast) && !inEchoExplosion)
            {
                inEchoExplosion = true;
                var z = AddZone(at, r, 0f, 0.35f, 0f, d, lv, Fade(d.color, 0.18f));
                z.detonateOnEnd = true;
                z.detonateDamage = dmg * 0.7f;
                z.isEcho = true;
                inEchoExplosion = false;
            }
        }

        void HitAround(Vector2 pos, float radius, float dmg, WeaponDef d, int lv)
        {
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;

                float touch = radius + p.transform.localScale.x * 0.5f;
                if (((Vector2)p.transform.position - pos).sqrMagnitude > touch * touch) continue;

                Hit(p, dmg, d, lv, p.transform.position);
            }
        }

        /// <summary>
        /// 🔴 **모든 피해가 이 함수를 지난다.**
        ///    특성과 조합이 여기 한 곳에서 붙기 때문에, 새 특성을 넣어도
        ///    패턴 코드(11가지)는 손대지 않는다. 무기가 20종이 돼도 마찬가지다.
        /// </summary>
        void Hit(JunkPiece p, float dmg, WeaponDef d, int lv, Vector2 at)
        {
            if (p == null || !p.Alive || dmg <= 0f) return;

            // 톱니/연마 — 내구가 많이 남은 것일수록 더 깎는다 (큰 것을 빨리 무르게 만든다)
            float shred = d.TraitValue(WeaponTrait.Shred, lv);
            if (shred > 0f) dmg *= 1f + shred * p.HpRatio;

            // ★ 난도질 — 같은 대상을 벨수록 깊게 들어간다
            if (stats.HasCombo(ComboEffect.CutCut) && d.tag == WeaponTag.Cut)
                dmg *= 1f + 0.55f * (1f - p.HpRatio);

            if (p.IsBossPart) dmg *= 1f + stats.bossDamageMul;

            float slow = d.TraitValue(WeaponTrait.Slow, lv);
            if (stats.HasCombo(ComboEffect.FieldGravity)) slow = Mathf.Max(slow, 0.5f);
            if (slow > 0f) p.Slow(slow, 0.6f);

            float pull = d.TraitValue(WeaponTrait.Pull, lv);
            if (pull > 0f && d.pattern == WeaponPattern.Projectile) p.Tug(ship.transform.position, pull);

            // 🔴 **맞힐 때 터진다** (테크트리 `ProcExplode`).
            //    부술 때가 아니라 **맞힐 때**인 이유: 큰 것에 붙어 있으면 계속 터져서
            //    "이 무기가 세졌다"가 매 순간 보인다. 부술 때만 터지면 잔몹에서만 보인다.
            if (stats.procExplode > 0f && Rand() < stats.procExplode)
                Explode(at, 2.0f * stats.rangeMul, dmg * 0.9f, d, lv);

            if (!p.Chip(dmg)) return;

            // ---- 부순 순간에만 일어나는 것들 ----

            // 🔴 **부순 자리가 터진다** (테크트리 `KillBlast`).
            //    `ProcExplode`가 큰 것에 꽂히는 값이라면 이건 **잔해가 몰린 곳**에서 산다 —
            //    하나가 터져 옆을 부수고 그게 또 터진다.
            if (stats.killBlast > 0f && Rand() < stats.killBlast)
                Explode(at, 2.6f * stats.rangeMul, dmg * 1.15f, d, lv);

            // 🔴 **부술 때 번개가 옮겨붙는다** (테크트리 `ProcChain`)
            if (stats.procChain > 0f && Rand() < stats.procChain)
                ArcFrom(at, 3, 6f * stats.rangeMul, dmg * 0.6f, d, lv);

            // 🔴 **부수면 잠깐 빨라진다** (테크트리 `KillSpeed`).
            //    치우는 리듬에 보상을 붙인다 — 잘 부술수록 다음 것으로 빨리 간다
            if (stats.killSpeed > 0f && ship != null) ship.GrantKillRush(stats.killSpeed);
            if (d.HasTraitAt(WeaponTrait.Detonate, lv))
                Explode(at, 1.8f * stats.rangeMul, d.TraitValue(WeaponTrait.Detonate, lv) * stats.powerMul, d, lv);

            if (d.HasTraitAt(WeaponTrait.Chain, lv))
                ArcFrom(at, 2, 5f * stats.rangeMul, dmg * 0.5f, d, lv);

            if (d.HasTraitAt(WeaponTrait.LifeSteal, lv))
                ship.Refuel(d.TraitValue(WeaponTrait.LifeSteal, lv));

            if (d.HasTraitAt(WeaponTrait.Magnetize, lv))
                field.RushFragmentsNear(at, 4.5f);

            // ★ 절삭 계열이 부순 자리에서 일어나는 것
            if (d.tag == WeaponTag.Cut)
            {
                if (stats.HasCombo(ComboEffect.CutShock))
                    ArcFrom(at, 2, 5.5f * stats.rangeMul, dmg * 0.5f, d, lv);
                if (stats.HasCombo(ComboEffect.CutBlast))
                    Explode(at, 1.6f * stats.rangeMul, dmg * 1.1f, d, lv);
            }

            // ★ 자기 폭풍 — 모여 있는 것들이 서로 감전된다
            if (stats.HasCombo(ComboEffect.ShockGravity))
                ArcFrom(at, 3, 4.5f * stats.rangeMul, dmg * 0.4f, d, lv);
        }

        // ================================================================ 공용

        void PullAround(Vector2 at, float r, float power)
        {
            float r2 = r * r;
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;
                if (((Vector2)p.transform.position - at).sqrMagnitude > r2) continue;
                p.Tug(at, power * Time.deltaTime * 8f);
            }
        }

        void PushAround(Vector2 at, float r, float power)
        {
            float r2 = r * r;
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;

                Vector2 d = (Vector2)p.transform.position - at;
                if (d.sqrMagnitude > r2) continue;
                if (d.sqrMagnitude < 0.0001f) d = Vector2.up;

                p.Tug(at + d.normalized * (r * 2.5f), power);
            }
        }

        /// <summary>반경 안에 살아 있는 쓰레기가 몇인가. 지뢰가 "모였는지" 판단할 때 쓴다.</summary>
        int CountNear(Vector2 at, float range)
        {
            int n = 0;
            float r2 = range * range;
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;
                if (((Vector2)p.transform.position - at).sqrMagnitude <= r2) n++;
            }
            return n;
        }

        JunkPiece Nearest(Vector2 at, float range)
        {
            JunkPiece best = null;
            float bestSq = range * range;
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;
                float sq = ((Vector2)p.transform.position - at).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq; best = p;
            }
            return best;
        }

        void SpawnFx(Vector2 at, float size, Color c)
        {
            var tr = GetFx();
            tr.position = at;
            tr.rotation = Quaternion.identity;
            tr.localScale = new Vector3(size, size, 1f);

            var sr = tr.GetComponent<SpriteRenderer>();
            if (glowSprite != null) sr.sprite = glowSprite;   // 폭발은 둥글게 번져야 한다
            sr.color = Tint(c);
        }

        Transform GetFx()
        {
            for (int i = 0; i < fx.Count; i++)
                if (fxLife[i] <= 0f) { fxLife[i] = 0.18f; fx[i].gameObject.SetActive(true); return fx[i]; }

            var tr = MakeSprite("Fx", Color.white, 13, Vector3.one);
            fx.Add(tr);
            fxLife.Add(0.18f);
            return tr;
        }

        void UpdateFx()
        {
            for (int i = 0; i < fx.Count; i++)
            {
                if (fxLife[i] <= 0f) continue;
                fxLife[i] -= Time.deltaTime;

                var sr = fx[i].GetComponent<SpriteRenderer>();
                var c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, c.a - Time.deltaTime * 4f));

                if (fxLife[i] <= 0f) fx[i].gameObject.SetActive(false);
            }
        }

        static void SetActive(List<Transform> list, bool on)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].gameObject.activeSelf != on) list[i].gameObject.SetActive(on);
        }

        static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>시드 고정 난수 — UnityEngine.Random을 쓰면 시뮬 재현성이 깨진다.</summary>
        float Rand()
        {
            rngSeed = (rngSeed * 16807f) % 2147483647f;
            return rngSeed / 2147483647f;
        }
    }
}
