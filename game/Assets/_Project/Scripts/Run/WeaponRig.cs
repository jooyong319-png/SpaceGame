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
        readonly Dictionary<WeaponKind, List<Transform>> orbits = new Dictionary<WeaponKind, List<Transform>>();
        readonly Dictionary<WeaponKind, Transform> auras = new Dictionary<WeaponKind, Transform>();
        readonly Dictionary<WeaponKind, Transform> beams = new Dictionary<WeaponKind, Transform>();

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

        class DroneUnit
        {
            public Transform tr;
            public Vector2 pos;
            public float cd;
            public float phase;
        }
        readonly Dictionary<WeaponKind, List<DroneUnit>> drones = new Dictionary<WeaponKind, List<DroneUnit>>();

        readonly List<Transform> fx = new List<Transform>();
        readonly List<float> fxLife = new List<float>();

        const float RngStart = 3.7f;
        readonly float[] trailCd = new float[Weapons.Count];
        readonly float[] gunCd = new float[Weapons.Count];
        readonly float[] moteCd = new float[Weapons.Count];

        float beamCd;
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
            drillTarget = null;
            drillHeat = 0f;
            drillFx = 0f;
            Drilling = false;

            rngSeed = RngStart;
            for (int i = 0; i < cooldown.Length; i++)
            {
                cooldown[i] = 0f; clock[i] = 0f; subClock[i] = 0f;
                trailCd[i] = 0f; gunCd[i] = 0f; moteCd[i] = 0f;
            }
            beamCd = 0f;

            // 드론의 위상도 되감는다 — 어디에 떠 있느냐가 조준 대상을 바꾼다
            foreach (var kv in drones)
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    kv.Value[i].phase = i * 2.1f;
                    kv.Value[i].cd = 0f;
                    kv.Value[i].pos = Vector2.zero;
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

                if (def.pattern == WeaponPattern.Orbit)
                    SyncOrbit(kind, def, lv > 0 ? CountOf(def, lv) : 0);

                if (def.pattern == WeaponPattern.Companion)
                    SyncDrones(kind, def, lv > 0 ? CountOf(def, lv) : 0);

                if (def.pattern == WeaponPattern.Aura && lv > 0 && !auras.ContainsKey(kind))
                {
                    var a = MakeSprite("Aura_" + kind, Fade(def.color, 0.22f), -2, Vector3.one);
                    if (ringSprite != null) a.GetComponent<SpriteRenderer>().sprite = ringSprite;
                    auras[kind] = a;
                }

                if (def.pattern == WeaponPattern.Beam && lv > 0 && !beams.ContainsKey(kind))
                    beams[kind] = MakeSprite("Beam_" + kind, Fade(def.color, 0.75f), 12, Vector3.one);
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

        int ExtraCountFromMeta(WeaponDef d)
        {
            if (stats == null) return 0;
            switch (d.pattern)
            {
                case WeaponPattern.Orbit:       return stats.orbitCountBonus;
                case WeaponPattern.Projectile:  return stats.projectileCountBonus;
                case WeaponPattern.Boomerang:   return stats.projectileCountBonus;
                case WeaponPattern.PeriodicAoe: return stats.blastCountBonus;
                case WeaponPattern.Mine:        return stats.blastCountBonus;
                case WeaponPattern.Chain:       return stats.chainTargetBonus;
            }
            return 0;
        }

        void SyncOrbit(WeaponKind k, WeaponDef def, int want)
        {
            if (!orbits.TryGetValue(k, out var list)) { list = new List<Transform>(); orbits[k] = list; }

            while (list.Count > want)
            {
                var last = list[list.Count - 1];
                if (last != null) Destroy(last.gameObject);
                list.RemoveAt(list.Count - 1);
            }
            while (list.Count < want)
            {
                var t = MakeSprite("Orbit_" + k, Fade(def.color, 0.95f), 11, Vector3.one * 1.1f);
                if (bladeSprite != null) t.GetComponent<SpriteRenderer>().sprite = bladeSprite;
                list.Add(t);
            }
        }

        void SyncDrones(WeaponKind k, WeaponDef def, int want)
        {
            if (!drones.TryGetValue(k, out var list)) { list = new List<DroneUnit>(); drones[k] = list; }

            while (list.Count > want)
            {
                var last = list[list.Count - 1];
                if (last != null && last.tr != null) Destroy(last.tr.gameObject);
                list.RemoveAt(list.Count - 1);
            }
            while (list.Count < want)
                list.Add(new DroneUnit {
                    tr = MakeSprite("Drone_" + k, Fade(def.color, 0.95f), 11, Vector3.one * 0.42f),
                    phase = list.Count * 2.1f
                });
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
            bool active = director.FieldActive
                       && ship.gameObject.activeSelf
                       && director.RespawnLeft <= 0f;

            UpdateShots(active);
            UpdateZones(active);
            UpdateFx();

            foreach (var kv in orbits) SetActive(kv.Value, active && stats.Has(kv.Key));
            foreach (var kv in auras) kv.Value.gameObject.SetActive(active && stats.Has(kv.Key));
            foreach (var kv in beams) kv.Value.gameObject.SetActive(false);
            foreach (var kv in drones)
                for (int i = 0; i < kv.Value.Count; i++)
                    kv.Value[i].tr.gameObject.SetActive(active && stats.Has(kv.Key));

            if (!active) return;

            Vector2 shipPos = ship.transform.position;

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

        void RunWeapon(WeaponDef d, int lv, Vector2 shipPos)
        {
            switch (d.pattern)
            {
                // 상시형 — 쿨다운 없이 매 프레임 작동한다
                case WeaponPattern.Orbit:       RunOrbit(d, lv, shipPos); return;
                case WeaponPattern.Drill:       RunDrill(d, lv, shipPos); return;
                case WeaponPattern.Aura:        RunAura(d, lv, shipPos); return;
                case WeaponPattern.Beam:        RunBeam(d, lv, shipPos); return;
                case WeaponPattern.Companion:   RunCompanion(d, lv, shipPos); return;
            }

            // 주기형 — 쿨다운이 돌아야 작동한다
            if (!Tick(d, lv)) return;

            switch (d.pattern)
            {
                case WeaponPattern.Projectile:  FireProjectile(d, lv, shipPos, false); break;
                case WeaponPattern.Boomerang:   FireProjectile(d, lv, shipPos, true); break;
                case WeaponPattern.Chain:       RunChain(d, lv, shipPos); break;
                case WeaponPattern.PeriodicAoe: RunPeriodicAoe(d, lv, shipPos); break;
                case WeaponPattern.Nova:        RunNova(d, lv, shipPos); break;
                case WeaponPattern.Mine:        RunMine(d, lv, shipPos); break;
                case WeaponPattern.Well:        RunWell(d, lv, shipPos); break;
            }
        }

        /// <summary>쿨다운. 특성 DoubleTap이면 한 주기에 두 번 나가도록 절반으로 줄인다.</summary>
        bool Tick(WeaponDef d, int lv)
        {
            int i = (int)d.kind;
            cooldown[i] -= Time.deltaTime;
            if (cooldown[i] > 0f) return false;

            float cd = d.cooldown * Mathf.Pow(d.cooldownPerLevel, lv - 1)
                     * stats.cooldownMul * stats.BurstHasteMul;
            if (d.HasTraitAt(WeaponTrait.DoubleTap, lv)) cd *= 0.5f;
            cooldown[i] = Mathf.Max(0.08f, cd);
            return true;
        }

        /// <summary>🔴 단발성 버프(카드)가 여기에 곱해진다 — 한 곳만 지나게 해서 빠뜨릴 일이 없게.</summary>
        float Damage(WeaponDef d, int lv)
            => (d.damage + d.damagePerLevel * (lv - 1)) * stats.powerMul * stats.BurstPowerMul;

        /// <summary>🔴 보스의 EMP가 사거리를 줄이고, 단발성 '확장'이 늘린다.</summary>
        float Range(WeaponDef d, int lv)
            => (d.range + d.rangePerLevel * (lv - 1)) * stats.rangeMul
             * stats.BurstSizeMul * BossBehaviour.RangeChoke;

        // ================================================================ 패턴 11가지

        // ---- Orbit: 배 주위를 도는 것들 (절단날 · 방벽) ----
        void RunOrbit(WeaponDef d, int lv, Vector2 shipPos)
        {
            if (!orbits.TryGetValue(d.kind, out var list) || list.Count == 0) return;

            float radius = Range(d, lv) * stats.orbitRadiusMul;
            float hitR = 0.55f * (1f + d.TraitValue(WeaponTrait.WideArc, lv));

            // ★ 분쇄 장판 — 궤도가 장판 끝까지 넓어진다
            //
            // 🔴 궤도만 넓히면 **안쪽이 빈다.** 붙어 있는 대상은 원 안에 들어가 버려서
            //    날이 영영 안 닿는다 — 2026-08-22 시뮬에서 소용돌이가 낀 조합 3개가
            //    **보스 옆에 붙어 있는데도 못 깼다.** 강화가 약점이 된 상태였다.
            //    그래서 넓어진 만큼 **타격 폭도 같이 넓힌다.** 날이 선이 아니라 띠가 된다.
            if (stats.HasCombo(ComboEffect.CutField))
            {
                float wide = Mathf.Max(radius, WidestAura());
                hitR *= 1f + (wide - radius) * 0.55f;
                radius = wide;
            }
            float dmg = Damage(d, lv);

            int i0 = (int)d.kind;
            clock[i0] += Time.deltaTime * (3.2f + lv * 0.25f) * stats.orbitSpinMul;
            float spin = clock[i0];

            for (int i = 0; i < list.Count; i++)
            {
                float ang = spin + (i / (float)list.Count) * Mathf.PI * 2f;
                Vector2 pos = shipPos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;

                list[i].position = pos;
                list[i].rotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg + 90f);
                list[i].GetComponent<SpriteRenderer>().color = Tint(Fade(d.color, 0.95f));

                HitAround(pos, hitR, dmg * Time.deltaTime * 12f, d, lv);

                // 🔴 잔상 — 지나간 자리에 호가 남는다. 네모가 도는 것과 완전히 달라 보인다
                trailCd[i0] -= Time.deltaTime;
                if (trailCd[i0] <= 0f && i == 0)
                {
                    trailCd[i0] = 0.045f;
                    for (int k = 0; k < list.Count; k++)
                    {
                        float a2 = spin + (k / (float)list.Count) * Mathf.PI * 2f;
                        Vector2 p2 = shipPos + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius;
                        Fx.Streak(p2, a2 * Mathf.Rad2Deg + 90f, radius * 0.55f, Fade(d.color, 0.5f));
                    }
                }

                float kb = d.TraitValue(WeaponTrait.Knockback, lv);
                if (kb > 0f) PushAround(pos, hitR * 1.6f, kb * Time.deltaTime * 6f);
            }

            // 🔴 특성 '포탑 궤도' — 돌면서 바깥으로 쏜다.
            //    사용자 제안 그대로다: *"주위를 도는 무기가 강화하면 돌면서 총 같은 게 나간다"*.
            //    숫자만 오르는 강화와 달리 **화면이 달라져서** 강해진 게 보인다.
            if (!d.HasTraitAt(WeaponTrait.OrbitGun, lv)) return;

            gunCd[i0] -= Time.deltaTime;
            if (gunCd[i0] > 0f) return;
            gunCd[i0] = Mathf.Max(0.08f, 0.55f * stats.cooldownMul);

            float gunDmg = d.TraitValue(WeaponTrait.OrbitGun, lv) * 6f * stats.powerMul;
            for (int i = 0; i < list.Count; i++)
            {
                float ang = spin + (i / (float)list.Count) * Mathf.PI * 2f;
                Vector2 outward = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Fire(d, lv, shipPos + outward * radius, outward * 26f, gunDmg, 2, Range(d, lv) * 2.2f);
            }
            Juice.Chip(0.6f);
        }

        // ---- Aura: 배 주위 지속 장판 (소용돌이) ----
        void RunAura(WeaponDef d, int lv, Vector2 shipPos)
        {
            float r = Range(d, lv);
            if (stats.HasCombo(ComboEffect.FieldField)) r *= 1.6f;      // ★ 영구 장판

            float dps = Damage(d, lv);

            bool charged = stats.HasCombo(ComboEffect.ShockField);      // ★ 대전 장판
            if (charged) dps *= 2.1f;

            if (auras.TryGetValue(d.kind, out var ring))
            {
                ring.position = shipPos;
                ring.localScale = new Vector3(r * 2f, r * 2f, 1f);
                ring.GetComponent<SpriteRenderer>().color = Tint(Fade(d.color, 0.22f));
            }

            HitAround(shipPos, r, dps * Time.deltaTime, d, lv);

            // 🔴 **빨려드는 그림.** 지금까지 고리만 있고 아무것도 안 움직여서
            //    소용돌이가 무슨 일을 하는지 화면에 전혀 안 보였다
            int mi = (int)d.kind;
            moteCd[mi] -= Time.deltaTime;
            if (moteCd[mi] <= 0f)
            {
                moteCd[mi] = 0.05f;
                for (int m = 0; m < 3; m++)
                {
                    float a = Rand() * Mathf.PI * 2f;
                    Vector2 from = shipPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * (0.85f + Rand() * 0.35f);
                    Fx.Mote(from, ship.transform, Fade(d.color, 0.9f), 0.45f);
                }
            }

            float pull = d.TraitValue(WeaponTrait.Pull, lv);
            if (stats.HasCombo(ComboEffect.FieldGravity)) pull += 5f;   // ★ 포획장
            if (pull > 0f) PullAround(shipPos, r, pull);

            int i0 = (int)d.kind;

            if (charged)
            {
                clock[i0] -= Time.deltaTime;
                if (clock[i0] <= 0f)
                {
                    clock[i0] = 0.6f * stats.cooldownMul;
                    ArcFrom(shipPos, 3, r * 2.2f, dps * 0.5f, d, lv);
                }
            }

            // ★ 압축 붕괴 — 장판이 주기적으로 스스로 터진다
            //
            // 🔴 이펙트가 **일반 폭발과 똑같아서 조합이 열린 줄도 몰랐다** (2026-08-22 피드백).
            //    히든 조합은 발견이 보상인데, 발동을 못 알아보면 보상이 통째로 사라진다.
            //    그래서 터지기 전에 **빨아들이고**, 터질 때 **두 겹으로** 터진다.
            if (stats.HasCombo(ComboEffect.BlastField))
            {
                subClock[i0] -= Time.deltaTime;

                // ---- 예고: 0.4초 전부터 장판이 수축하며 안으로 빨아들인다 ----
                if (subClock[i0] <= CollapseTell && subClock[i0] > 0f)
                {
                    float t = 1f - subClock[i0] / CollapseTell;   // 0 → 1

                    if (auras.TryGetValue(d.kind, out var tellRing))
                    {
                        // 고리가 조여들고 하얗게 달아오른다
                        float shrink = r * 2f * (1f - t * 0.30f);
                        tellRing.localScale = new Vector3(shrink, shrink, 1f);
                        tellRing.GetComponent<SpriteRenderer>().color =
                            Color.Lerp(Tint(Fade(d.color, 0.22f)), new Color(1f, 1f, 1f, 0.75f), t);
                    }

                    collapseCd -= Time.deltaTime;
                    if (collapseCd <= 0f)
                    {
                        collapseCd = 0.022f;
                        for (int m = 0; m < 4; m++)
                        {
                            float a = Rand() * Mathf.PI * 2f;
                            Vector2 from = shipPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * (1f + Rand() * 0.4f);
                            Fx.Mote(from, ship.transform, new Color(1f, 0.9f, 0.6f, 0.95f), 0.18f);
                        }
                    }
                }

                if (subClock[i0] <= 0f)
                {
                    subClock[i0] = 2.2f * stats.cooldownMul;

                    // ---- 붕괴: 안으로 한 번, 밖으로 한 번 ----
                    Fx.Shockwave(shipPos, r * 0.35f, new Color(1f, 0.95f, 0.7f, 0.9f), 0.16f);
                    Fx.Shockwave(shipPos, r * 1.35f, new Color(1f, 0.55f, 0.2f, 0.8f), 0.34f);
                    for (int m = 0; m < 8; m++)
                        Fx.Streak(shipPos, m * 45f, r * 1.1f, new Color(1f, 0.8f, 0.4f), 0.20f);

                    Explode(shipPos, r * 1.35f, dps * 3.4f, d, lv);
                    Juice.Break();
                }
            }
        }

        /// <summary>압축 붕괴 예고 시간. 이보다 짧으면 예고가 아니라 그냥 번쩍임이다.</summary>
        const float CollapseTell = 0.40f;
        float collapseCd;

        // ---- Drill: 목표 하나에 붙어 갈아낸다 (채굴 드릴) ----

        /// <summary>
        /// 🔴 **드릴** (rev.10). 이 게임의 새 기본 동사.
        ///
        ///    쏘는 게 아니라 **시간을 들이는** 무기다. 커서 쪽 가장 가까운 쓰레기 하나를 잡고,
        ///    잡고 있는 동안 계속 갈아낸다. 목표가 부서지면 다음 것을 잡는다.
        ///
        /// 🔴 **캐는 동안 배가 묶인다** — 이게 이 패턴의 존재 이유다.
        ///    채굴은 그 자체로는 재미가 없다. 가만히 있는 돌에 버튼을 누르는 일이다.
        ///    재미는 *"지금 이걸 캘까, 로봇 먼저 처리할까, 그냥 뺄까"*에서 나오고,
        ///    그러려면 캐는 시간이 **무방비한 시간**이어야 한다.
        ///
        ///    한 번에 **하나**만 상대하므로 떼로 몰리면 아무것도 못 한다.
        ///    그래서 두 번째 무기(호위용)를 고를 이유가 생긴다 —
        ///    무기가 딱 둘이라 "둘 다 채굴이냐, 하나는 호위냐"가 매 판 갈림길이 된다.
        /// </summary>
        void RunDrill(WeaponDef d, int lv, Vector2 shipPos)
        {
            float reach = Range(d, lv) * (1f + d.TraitValue(WeaponTrait.WideArc, lv));

            // 잡고 있던 게 사라졌거나 멀어졌으면 놓는다
            if (drillTarget != null && (!drillTarget.Alive ||
                ((Vector2)drillTarget.transform.position - shipPos).sqrMagnitude > reach * reach * 1.35f))
                drillTarget = null;

            // 🔴 커서 **쪽**에서 고른다. 가장 가까운 것을 자동으로 잡으면
            //    플레이어가 무엇을 캘지 못 정하고, 그러면 파밍에 결정이 사라진다.
            if (drillTarget == null)
            {
                Vector2 aim = ship.AimPoint;
                JunkPiece best = null;
                float bestScore = float.MaxValue;

                for (int i = 0; i < field.Pieces.Count; i++)
                {
                    var p = field.Pieces[i];
                    if (!p.Alive) continue;

                    Vector2 at = p.transform.position;
                    if ((at - shipPos).sqrMagnitude > reach * reach) continue;

                    // 커서에 가까울수록 우선
                    float score = (at - aim).sqrMagnitude;
                    if (score >= bestScore) continue;
                    bestScore = score; best = p;
                }

                drillTarget = best;
                drillHeat = 0f;
            }

            if (drillTarget == null) { Drilling = false; ship.Drilling = false; return; }

            Drilling = true;
            ship.Drilling = true;

            // 🔴 오래 붙어 있을수록 빨라진다 — 큰 덩어리에 붙어 있을 이유를 만든다
            drillHeat = Mathf.Min(1f, drillHeat + Time.deltaTime * 0.7f);
            float dps = Damage(d, lv) * (1f + drillHeat * 0.8f) * Tuning.DrillPower;

            Vector2 to = (Vector2)drillTarget.transform.position;
            Hit(drillTarget, dps * Time.deltaTime, d, lv, to);

            // ★ 충격 전달 — 갈아내는 대상 주변으로 진동이 퍼진다
            if (d.HasTraitAt(WeaponTrait.Chain, lv))
                HitAround(to, reach * 0.7f, dps * 0.25f * Time.deltaTime, d, lv);

            // ---- 그림: 배에서 목표로 뻗은 굵은 축 + 튀는 파편 ----
            Fx.Line(shipPos, to, Fade(d.color, 0.85f), 0.22f + drillHeat * 0.12f, 0.06f);

            drillFx -= Time.deltaTime;
            if (drillFx <= 0f)
            {
                drillFx = 0.045f;
                float a = Rand() * Mathf.PI * 2f;
                Fx.Streak(to, a * Mathf.Rad2Deg, 0.9f + drillHeat, Fade(d.color, 0.9f), 0.16f);
                Juice.Chip(0.2f + drillHeat * 0.3f);
            }
        }

        /// <summary>지금 드릴이 물고 있는가. `ShipController`가 읽어 배를 묶는다.</summary>
        public bool Drilling { get; private set; }

        JunkPiece drillTarget;
        float drillHeat;
        float drillFx;

        // ---- Beam: 커서 방향 지속 광선 (레이저) ----
        void RunBeam(WeaponDef d, int lv, Vector2 shipPos)
        {
            float len = Range(d, lv);
            float thick = 0.45f * (1f + d.TraitValue(WeaponTrait.WideArc, lv));
            float dps = Damage(d, lv);

            Vector2 dir = ship.AimPoint - (Vector2)ship.transform.position;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;

            int rays = 1 + Mathf.RoundToInt(d.TraitValue(WeaponTrait.ExtraProjectile, lv));
            for (int r = 0; r < rays; r++)
            {
                Vector2 rd = r == 0 ? dir : Rotate(dir, Mathf.PI * 2f * r / rays);
                BeamRay(d, lv, shipPos, rd, len, thick, dps, r == 0);
            }
        }

        void BeamRay(WeaponDef d, int lv, Vector2 from, Vector2 dir, float len, float thick, float dps, bool primary)
        {
            Transform tr;
            if (primary && beams.TryGetValue(d.kind, out var main))
            {
                tr = main;
                tr.gameObject.SetActive(true);
            }
            else
            {
                tr = GetFx();
                tr.GetComponent<SpriteRenderer>().color = Fade(d.color, 0.6f);
            }

            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            tr.position = from + dir * (len * 0.5f);
            tr.rotation = Quaternion.Euler(0f, 0f, ang);
            tr.localScale = new Vector3(len, thick, 1f);

            // 🔴 광선은 **두 겹**이어야 광선으로 보인다 — 넓고 흐린 번짐 위에 밝은 심지.
            //    직사각형 하나면 그냥 막대다.
            beamCd -= Time.deltaTime;
            if (beamCd <= 0f)
            {
                beamCd = 0.05f;
                Fx.Streak(from + dir * (len * 0.5f), ang, len, Tint(Fade(d.color, 0.22f)), 0.12f);
                Fx.Spark(from + dir * len, thick * 2.4f, Tint(Fade(d.color, 0.8f)), 0.10f);
            }

            // 🔴 과충전 — 멀수록 아프다. 붙어서 쓰는 무기들과 성격이 갈리게 하는 장치
            float over = d.TraitValue(WeaponTrait.Overcharge, lv);

            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive) continue;

                Vector2 rel = (Vector2)p.transform.position - from;
                float along = Vector2.Dot(rel, dir);
                if (along < 0f || along > len) continue;

                float perp = Mathf.Abs(rel.x * -dir.y + rel.y * dir.x);
                if (perp > thick * 0.5f + p.transform.localScale.x * 0.5f) continue;

                float mul = 1f + over * (along / Mathf.Max(0.01f, len));
                Hit(p, dps * mul * Time.deltaTime, d, lv, p.transform.position);
            }
        }

        // ---- Projectile / Boomerang ----
        void FireProjectile(WeaponDef d, int lv, Vector2 shipPos, bool boomerang)
        {
            Vector2 dir = ship.AimPoint - shipPos;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;

            int count = CountOf(d, lv);
            float dmg = Damage(d, lv);

            int pierce = d.pierce + Mathf.RoundToInt(d.TraitValue(WeaponTrait.ExtraPierce, lv))
                       + stats.pierceBonus;
            if (d.HasTraitAt(WeaponTrait.Pierceless, lv)) pierce = 999;
            if (stats.HasCombo(ComboEffect.PierceP)) pierce += 6;      // ★ 관통 정렬

            for (int i = 0; i < count; i++)
            {
                float spread = (count == 1) ? 0f : (i - (count - 1) * 0.5f) * 0.16f;
                var dd = Rotate(dir, spread);
                Fire(d, lv, shipPos + dd * 0.8f, dd * d.projectileSpeed, dmg, pierce, Range(d, lv));
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

        // ---- PeriodicAoe: 주기적으로 근처에서 폭발 ----
        void RunPeriodicAoe(WeaponDef d, int lv, Vector2 shipPos)
        {
            float r = Range(d, lv) * (1f + d.TraitValue(WeaponTrait.WideArc, lv));
            float dmg = Damage(d, lv);

            int n = CountOf(d, lv);
            for (int i = 0; i < n; i++)
            {
                // 🔴 **아무 데나 던지지 않는다.**
                //    예전엔 배 주변 2~8유닛 랜덤이었고, 그래서 플레이어가 아무것도 정하지 않는
                //    무기가 됐다 — 2026-08-22 피드백: *"압축 폭탄, 흡입 소용돌이가 좀 애매한데"*.
                //    이제 **쓰레기가 몰린 쪽**에 떨어지고, 없으면 커서 쪽으로 간다.
                Vector2 at = BombTarget(shipPos, r, i, n);

                if (stats.HasCombo(ComboEffect.BlastGravity))       // ★ 중력 폭탄
                    PullAround(at, r * 1.8f, 9f);

                Explode(at, r, dmg, d, lv);
            }
            Juice.Break();
        }

        /// <summary>
        /// 폭탄이 떨어질 자리. 🔴 **가장 붐비는 곳**을 고른다 —
        /// 그래야 "던진 보람"이 보이고, 플레이어가 몰이를 할 이유가 생긴다.
        /// </summary>
        Vector2 BombTarget(Vector2 shipPos, float radius, int index, int count)
        {
            // 🔴 예전엔 **풀에서 무작위로** 후보를 뽑았다. 풀은 224칸인데 대부분이
            //    죽은 조각이라 후보 7개 중 대부분이 **시체 자리**였고,
            //    살아 있는지 확인조차 안 했다.
            //
            //    결과: 폭탄이 사실상 아무 데나 떨어졌고, 그래서 폭탄 계열 조합의
            //    측정 편차가 45~55%였다 (2026-08-22 3회 평균 시뮬).
            //    **운에 좌우되는 무기는 밸런스를 잡을 수가 없다.**
            //
            //    이제 난수를 아예 안 쓴다. 살아 있는 것만 훑어 **가장 붐비는 자리**를 고른다.
            //    🔴 다만 이 훑기는 **살아 있는 수의 제곱**이다.
            //       rev.7에서 동시 생존 상한이 50 → 300으로 올랐다 (웨이브당 +35).
            //       300이면 후보 300 × 채점 300 = 9만 번, 그걸 폭탄 발수만큼 반복한다 —
            //       WebGL에서 프레임이 눈에 띄게 튄다.
            //
            //       그래서 **후보만 솎는다.** 채점은 전수로 하되(정확도가 여기서 나온다),
            //       후보는 최대 48개까지 일정 간격으로 고른다. 간격은 난수가 아니라
            //       개수에서 나오므로 **결정론은 그대로다.**
            Vector2 best = ship.AimPoint;
            int bestScore = -1;

            float reach2 = 14f * 14f;
            float r2 = radius * radius;

            int alive = 0;
            for (int i = 0; i < field.Pieces.Count; i++) if (field.Pieces[i].Alive) alive++;

            int stride = Mathf.Max(1, Mathf.CeilToInt(alive / (float)MaxBombCandidates));
            int seen = 0;

            for (int c = 0; c < field.Pieces.Count; c++)
            {
                var cp = field.Pieces[c];
                if (!cp.Alive) continue;

                // 살아 있는 것 중 stride 간격으로만 후보에 올린다
                if (seen++ % stride != 0) continue;

                Vector2 cand = cp.transform.position;
                if ((cand - shipPos).sqrMagnitude > reach2) continue;

                int score = 0;
                for (int i = 0; i < field.Pieces.Count; i++)
                {
                    var p = field.Pieces[i];
                    if (!p.Alive) continue;
                    if (((Vector2)p.transform.position - cand).sqrMagnitude <= r2) score++;
                }

                if (score > bestScore) { bestScore = score; best = cand; }
            }

            // 사거리 안에 아무것도 없으면 커서 쪽으로 — 플레이어 의도를 따른다
            if (bestScore <= 0) best = ship.AimPoint;

            // 여러 발이면 조금씩 흩뿌린다 — 같은 자리에 겹치면 한 발과 다를 게 없다
            if (count > 1)
            {
                float a = (index / (float)count) * Mathf.PI * 2f;
                best += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.8f;
            }
            return best;
        }

        /// <summary>
        /// 폭탄 낙하 지점 후보 수 상한. 채점은 전수로 하되 **후보만** 솎는다.
        /// 밀집 지점은 여러 조각이 공유하므로, 후보를 솎아도 고르는 자리는 거의 안 변한다.
        /// </summary>
        const int MaxBombCandidates = 48;

        // ---- Nova: 배 중심 원형 파동 ----
        void RunNova(WeaponDef d, int lv, Vector2 shipPos)
        {
            float r = Range(d, lv) * (1f + d.TraitValue(WeaponTrait.WideArc, lv));
            Explode(shipPos, r, Damage(d, lv), d, lv);

            // 파동은 두 겹으로 퍼진다 — 배에서 나갔다는 게 보여야 한다
            Fx.Shockwave(shipPos, r * 0.7f, Tint(Fade(d.color, 0.9f)), 0.26f);
            Fx.Shockwave(shipPos, r * 1.15f, Tint(Fade(d.color, 0.55f)), 0.40f);
            Juice.Break();
        }

        // ---- Mine: 제자리에 두고 간다 ----
        void RunMine(WeaponDef d, int lv, Vector2 shipPos)
        {
            int n = CountOf(d, lv);
            float r = Range(d, lv) * (1f + d.TraitValue(WeaponTrait.WideArc, lv));

            // 🔴 **지나온 자리에 깐다.** 배 바로 밑에 깔면 의미가 없다 —
            //    쓰레기는 어차피 배로 몰려오므로, 뒤에 깔아야 **쫓아오는 것들이 밟는다.**
            //    설명("지나온 자리에 지뢰를 둔다")과 실제가 어긋나 있었다.
            Vector2 back = -ship.Velocity;
            if (back.sqrMagnitude < 0.5f) back = Vector2.down;   // 멈춰 있으면 아무 방향
            back = back.normalized;

            for (int i = 0; i < n; i++)
            {
                // 여러 개면 뒤쪽에 부채꼴로 흩는다 — 한 점에 겹치면 한 개와 같다
                float spread = (n == 1) ? 0f : (i - (n - 1) * 0.5f) * 0.5f;
                Vector2 dir = Rotate(back, spread);
                // 🔴 거리도 난수를 안 쓴다. 지뢰가 어디 깔릴지가 운이면
                //    같은 플레이를 해도 결과가 달라져 밸런스를 잡을 수 없다.
                //    개수에 따라 **일정한 간격**으로 뒤에 깐다.
                float back2 = 2.2f + (i % 3) * 0.6f;
                Vector2 at = shipPos + dir * back2;

                var z = AddZone(at, r, 0f, 9f, d.TraitValue(WeaponTrait.Pull, lv), d, lv, Fade(d.color, 0.35f));
                z.armDelay = 0.35f;
                z.detonateDamage = Damage(d, lv);
            }
        }

        // ---- Well: 한 점으로 끌어모은다 ----
        void RunWell(WeaponDef d, int lv, Vector2 shipPos)
        {
            int n = CountOf(d, lv);
            float r = Range(d, lv);
            float pull = 7f + d.TraitValue(WeaponTrait.Pull, lv);

            if (stats.HasCombo(ComboEffect.GravGrav)) pull *= 2.2f;    // ★ 사건의 지평

            // 🔴 우물을 **커서에 그대로 놓지 않는다.**
            //    쓰레기는 배로 몰려오는데 우물이 커서 자리에 생기면 빈 곳에 놓이기 일쑤였다 —
            //    2026-08-22 시뮬에서 중력 계열이 파편 974개(1위 3285개)로 꼴찌였고,
            //    파편이 적다는 건 **덜 죽였다**는 뜻이다.
            //    이제 배와 커서 **사이**에 놓아 몰려오는 길목을 잡는다.
            Vector2 aim = Vector2.Lerp(shipPos, ship.AimPoint, 0.55f);

            for (int i = 0; i < n; i++)
            {
                Vector2 at = n == 1 ? aim : shipPos + Rotate(aim - shipPos, Mathf.PI * 2f * i / n);

                var z = AddZone(at, r, Damage(d, lv), 2.6f, pull, d, lv, Fade(d.color, 0.20f));
                z.detonateOnEnd = d.HasTraitAt(WeaponTrait.Detonate, lv);
                z.detonateDamage = d.TraitValue(WeaponTrait.Detonate, lv) * stats.powerMul;
            }
        }

        // ---- Companion: 따라다니며 스스로 쏜다 ----
        void RunCompanion(WeaponDef d, int lv, Vector2 shipPos)
        {
            if (!drones.TryGetValue(d.kind, out var list) || list.Count == 0) return;

            float orbit = 2.2f;
            float dmg = Damage(d, lv);
            float range = Range(d, lv);

            for (int i = 0; i < list.Count; i++)
            {
                var dr = list[i];
                dr.phase += Time.deltaTime * 1.6f;

                Vector2 home = shipPos + new Vector2(Mathf.Cos(dr.phase + i * 2.1f),
                                                     Mathf.Sin(dr.phase + i * 2.1f)) * orbit;
                dr.pos = Vector2.Lerp(dr.pos, home, 5f * Time.deltaTime);
                dr.tr.position = dr.pos;

                dr.cd -= Time.deltaTime;
                if (dr.cd > 0f) continue;

                var target = Nearest(dr.pos, range);
                if (target == null) continue;

                dr.cd = Mathf.Max(0.1f, d.cooldown * Mathf.Pow(d.cooldownPerLevel, lv - 1) * stats.cooldownMul);

                Vector2 dir = ((Vector2)target.transform.position - dr.pos).normalized;
                int pierce = d.pierce + Mathf.RoundToInt(d.TraitValue(WeaponTrait.ExtraPierce, lv)) + stats.pierceBonus;
                Fire(d, lv, dr.pos + dir * 0.4f, dir * d.projectileSpeed, dmg, pierce, range);

                // 🔴 조준선 — 드론이 **무엇을 노리는지** 보여야 따라다니는 게 의미가 생긴다
                Fx.Line(dr.pos, target.transform.position, Tint(Fade(d.color, 0.35f)), 0.07f, 0.12f);

                float pull = d.TraitValue(WeaponTrait.Pull, lv);
                if (pull > 0f) target.Tug(shipPos, pull);
            }
        }

        // ================================================================ 장판 · 지뢰 · 우물

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

                // 지뢰: 무언가 들어오면 즉시 터지고 사라진다
                if (def.pattern == WeaponPattern.Mine)
                {
                    // 🔴 **모일 때까지 기다린다.** 처음 다가온 하나에게 터뜨리면
                    //    광역 무기가 단발 무기가 된다 — 지뢰가 파편 수 꼴찌였던 이유다.
                    //    다만 영영 안 터지면 안 되므로, 수명이 얼마 안 남으면 그냥 터진다.
                    // 🔴 기준을 3마리로 잡았더니 **초반에 아예 안 터졌다.**
                    //    웨이브 1~2에는 화면에 몇 마리 없어서 조건이 영영 안 맞고,
                    //    그 사이 경험치가 뒤처져 런 전체가 무너졌다 (Lv.10 vs Lv.14).
                    //
                    //    2마리로 낮추고, **깔린 지 2.5초 지나면 하나만 있어도 터진다.**
                    //    "모아서 터뜨리면 이득"은 유지하되 **멈추지는 않게** 한다.
                    int near = CountNear(z.at, z.radius * 0.6f);
                    bool crowded = near >= 2;
                    bool waited = z.life < 6.5f && near >= 1;

                    if (crowded || waited)
                    {
                        Explode(z.at, z.radius, z.detonateDamage, def, z.level);
                        if (def.HasTraitAt(WeaponTrait.Chain, z.level))
                            ArcFrom(z.at, 3, z.radius * 2f, z.detonateDamage * 0.4f, def, z.level);
                        z.life = 0f;
                        continue;
                    }

                    // 🔴 지뢰는 **깜빡여야** 지뢰로 보인다. 가만히 있는 원은 장식이다
                    z.blink -= Time.deltaTime;
                    if (z.blink <= 0f)
                    {
                        z.blink = 0.28f;
                        Fx.Spark(z.at, z.radius * 0.5f, Fade(def.color, 0.75f), 0.16f);
                    }

                    continue;
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

            if (!p.Chip(dmg)) return;

            // ---- 부순 순간에만 일어나는 것들 ----
            if (d.HasTraitAt(WeaponTrait.Detonate, lv) && d.pattern != WeaponPattern.Well)
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

        float WidestAura()
        {
            float best = 0f;
            for (int i = 0; i < Weapons.Count; i++)
            {
                int lv = stats.LevelOf((WeaponKind)i);
                if (lv <= 0) continue;
                var d = content.Weapon((WeaponKind)i);
                if (d == null || d.pattern != WeaponPattern.Aura) continue;
                best = Mathf.Max(best, Range(d, lv));
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

        void SpawnBeamFx(Vector2 a, Vector2 b, Color c)
        {
            var tr = GetFx();
            tr.GetComponent<SpriteRenderer>().sprite = sprite;   // 선은 각져야 한다
            float len = Vector2.Distance(a, b);
            tr.position = (a + b) * 0.5f;
            tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            tr.localScale = new Vector3(len, 0.12f, 1f);
            tr.GetComponent<SpriteRenderer>().color = Tint(new Color(c.r, c.g, c.b, 0.8f));
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
