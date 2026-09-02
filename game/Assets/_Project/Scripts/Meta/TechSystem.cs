using System.Collections.Generic;
using UnityEngine;
using SalvageRun.Data;

namespace SalvageRun.Meta
{
    /// <summary>
    /// 이번 런의 능력치. 시작값(RunConfig) 위에 **레벨업 카드**가 쌓인다.
    ///
    /// 🔴 2026-08-20 뱀서 구조: 무기는 여러 개가 동시에 돌아간다.
    ///    같은 무기 카드를 또 뽑으면 레벨이 오르고, 새 무기 카드를 뽑으면 하나 더 늘어난다.
    ///    무기가 늘어 화면이 덮이는 것이 "내가 강해졌다"의 유일한 증거다.
    /// </summary>
    public class RunStats
    {
        // ---- 무기 ----
        /// <summary>인덱스 = WeaponKind. 0이면 미보유.</summary>
        public readonly int[] weaponLevel = new int[Weapons.Count];

        public int LevelOf(WeaponKind k) => weaponLevel[(int)k];
        public bool Has(WeaponKind k) => weaponLevel[(int)k] > 0;
        public void AddWeapon(WeaponKind k, int amount = 1) => weaponLevel[(int)k] += amount;

        public int OwnedWeaponCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < weaponLevel.Length; i++) if (weaponLevel[i] > 0) n++;
                return n;
            }
        }

        public float rangeMul = 1f;       // 전 무기 사거리
        public float powerMul = 1f;       // 전 무기 피해
        public float cooldownMul = 1f;    // 전 무기 쿨다운
        public float bossDamageMul;       // 보스에게 주는 추가 피해

        // ---- 영구 강화(테크트리)가 채우는 무기 보너스 ----
        // ⬜ **패턴 단위** 보너스. 무기가 셋으로 줄고 무기마다 제 가지를 타게 되면서
        //    거의 안 쓴다. 남은 것은 아래 무기별 배열이 대신한다.
        public int projectileCountBonus;   // 발사체 +N (작살 · 원반 공통)
        public int chainTargetBonus;       // 연쇄 대상 +N (방전)
        public int pierceBonus;            // 관통 +N

        // ---- 🔴 무기별 보너스 (2026-08-23) ----
        //
        // 🔴 사장님 지시: *"무기는 따로따로 테크트리 타지게 하자."*
        //
        //    전에는 보너스가 **패턴 단위**였다. 그래서 "작살 발사 수 +1"이
        //    같은 패턴을 쓰는 **원반까지 같이** 올렸다 — 무기마다 다른 길을 타게 하려면
        //    그 구조로는 안 된다. 무기 하나를 키우면 다른 무기도 같이 커지므로
        //    **가지를 나눈 의미가 없어진다.**
        //
        //    그래서 무기 종류마다 칸을 따로 둔다. `TechNodeDef.weapon`이 어느 칸인지 정한다.
        public readonly float[] wPower    = Filled(1f);   // 무기별 피해 배수
        public readonly float[] wRange    = Filled(1f);   // 무기별 사거리 배수
        public readonly float[] wCooldown = Filled(1f);   // 무기별 쿨다운 배수 (작을수록 빠름)
        public readonly int[]   wCount    = new int[Weapons.Count];   // 무기별 발사 수 · 연쇄 대상 +N
        public readonly int[]   wPierce   = new int[Weapons.Count];   // 무기별 관통 +N

        static float[] Filled(float v)
        {
            var a = new float[Weapons.Count];
            for (int i = 0; i < a.Length; i++) a[i] = v;
            return a;
        }

        public float PowerOf(WeaponKind k)    => powerMul * wPower[(int)k];
        public float RangeOf(WeaponKind k)    => rangeMul * wRange[(int)k];
        public float CooldownOf(WeaponKind k) => cooldownMul * wCooldown[(int)k];

        // ---- 배 ----
        public float fuelMax;
        public float thrustForce;
        public float damping;
        public float speedMul = 1f;
        public float contactResist;       // 0~1

        // ---- 수집 ----
        public float intakeMul = 1f;
        public float valueMultiplier = 1f;

        /// <summary>🔴 끌 때 덜 무거워지는 정도. `RunDirector.TowWeightMul`이 읽는다.</summary>
        public float towWeightMul = 1f;

        /// <summary>🔴 끌 수 있는 개수 +N. `RunDirector.ShipTow`가 읽는다.</summary>
        public int towCapacityBonus;

        /// <summary>🔴 회수 드론 대수. `RunDirector.SyncDrones`가 읽는다.</summary>
        public int carrierDrones;

        /// <summary>
        /// 🔴 직송 드론이 **집으로 보낼 수 있는 최고 등급**. `-1`이면 드론이 없다.
        ///    0 = 고철만 · 1 = 회로까지 · … · 5 = 전부.
        /// </summary>
        public int haulerGrade = -1;

        /// <summary>🔴 보스 탄 피해 감소(0~1). `RunDirector.CheckBossShots`가 읽는다.</summary>
        public float bossShotResist;

        /// <summary>보스 재화 배수. `StageField.RollMaterials`가 읽는다.</summary>
        public float bossMatBonus;

        // ---- 🔴 발동형 (전부 확률 0~1) ----
        public float procExplode;      // WeaponRig.Hit — 맞힐 때 터진다
        public float procChain;        // WeaponRig.Hit — 부술 때 번개
        public float procDoubleShot;   // WeaponRig.Tick — 한 번 더 쏜다
        public float killSpeed;        // RunDirector — 부수면 잠깐 빨라진다

        // ---- 🔴 드롭형 ----
        public float matDoubleChance;  // StageField.DropMat — 두 배로 나온다
        public float rareMatChance = 1f; // StageField.RollMaterials — 희귀 확률 배수
        public float matValue = 1f;    // RunDirector.BankTow — 값어치 배수
        public float lumpLife;         // StageField.DropMat — 덩어리 수명 +N초

        /// <summary>🔴 연료 감소 배수(작을수록 오래 간다). `ShipController`가 읽는다.</summary>
        public float fuelDrainMul = 1f;

        public float killBlast;        // WeaponRig.Hit — 부술 때 터질 확률
        public float bossFuel;         // RunDirector.OnBossPartBroken — 부위당 연료
        public float shotSpeedMul = 1f;// WeaponRig.Fire — 투사체 속도 배수
        public bool  towAuto;          // RunDirector.CollectByTouch — 빈 칸이면 자동
        public float refinePerCollect;

        // ---- 카드 ----

        // ---- 영구 강화가 채우는 런 밖 값들 ----
        public float startFuelRatio = 1f;   // 시작 연료 비율
        public int   startWeaponLevel;      // 시작 무기 레벨 보정
        public int   revives;               // 부활 횟수
        public float dashCooldownMul = 1f;
        public float itemDropBonus;         // %p
        public float fuelPickupMul = 1f;
        public float hazardResist;

        // ---- 재화 발견율 (1.0 = 기본) ----
        public float scrapFind = 1f, circuitFind = 1f, coreFind = 1f;

        public float FindMul(MatKind m)
            => m == MatKind.Scrap ? scrapFind : m == MatKind.Circuit ? circuitFind : coreFind;

    }

    public static class TechSystem
    {
        /// <summary>`FillOwnedWeapons`가 채우는 임시 버퍼. 매 런마다 새로 만들 이유가 없다.</summary>
        static readonly List<WeaponKind> ownedBuf = new List<WeaponKind>();

        public static RunStats BuildStats(GameContent content, RunConfig cfg)
        {
            var s = new RunStats
            {
                fuelMax = cfg.fuelMax,
                thrustForce = cfg.thrustForce,
                damping = cfg.linearDamping,
            };

            // 성장은 테크트리 한 층뿐이다 (레벨업·카드·우주선을 전부 뺐다)
            ApplyTechTree(s, content);

            // 공짜 노드를 먼저 채워 둔다 — 안 그러면 첫 무기조차 안 열린 것으로 읽힌다
            MetaSave.EnsureFreeNodes(content);

            // 🔴 **연 무기가 전부 붙는다** (2026-08-26 사장님 지시:
            //    *"무기는 장착이 아니라 추가다. 개수 제한은 없다"*).
            //
            //    고르는 방식이었을 때는 두 번째 무기를 사는 순간 첫 번째가 창고로 갔다 —
            //    **산 보람이 없다.** 인크리멘탈에서 산 것은 쌓여야 한다.
            //
            //    ⚠️ 하나도 안 열렸으면 대비책 무기를 준다. 무기가 없으면 40초를 구경만 한다.
            MetaSave.FillOwnedWeapons(content, ownedBuf, cfg.startingWeapon);

            for (int i = 0; i < ownedBuf.Count; i++)
                s.AddWeapon(ownedBuf[i], 1 + s.startWeaponLevel);
            return s;
        }

        /// <summary>
        /// 영구 강화(테크트리)를 런 능력치에 얹는다.
        /// 🔴 랭크만큼 곱해서 더한다 — 노드마다 "몇 랭크까지"가 다르므로
        ///    여기서 랭크를 세지 않으면 5랭크짜리 노드가 1랭크처럼 작동한다.
        /// </summary>
        public static void ApplyTechTree(RunStats s, GameContent content)
        {
            if (content == null || content.techTree == null) return;

            var meta = MetaSave.Data;
            for (int i = 0; i < content.techTree.Length; i++)
            {
                var n = content.techTree[i];
                int rank = meta.RankOf(n.id);
                if (rank <= 0) continue;

                float v = n.value * rank;
                switch (n.effect)
                {
                    // 선체
                    case TechEffect.FuelMax:        s.fuelMax += v; break;
                    case TechEffect.ContactResist:  s.contactResist = Mathf.Min(0.7f, s.contactResist + v); break;
                    case TechEffect.StartFuel:      s.startFuelRatio += v; break;
                    case TechEffect.Revive:         s.revives += rank; break;

                    // 기동
                    case TechEffect.MoveSpeed:      s.speedMul += v; break;
                    case TechEffect.Thrust:         s.thrustForce += v; break;
                    case TechEffect.Handling:       s.damping += v; break;
                    case TechEffect.DashCooldown:   s.dashCooldownMul *= Mathf.Pow(1f - n.value, rank); break;

                    // 화력
                    case TechEffect.WeaponPower:    s.powerMul += v; break;
                    case TechEffect.WeaponRange:    s.rangeMul += v; break;
                    case TechEffect.WeaponCooldown: s.cooldownMul *= Mathf.Pow(1f - n.value, rank); break;
                    case TechEffect.BossDamage:     s.bossDamageMul += v; break;

                    // 🔴 무기별 — `n.weapon`이 가리키는 한 무기에만 붙는다
                    case TechEffect.WeaponPowerOne:    s.wPower[(int)n.weapon] += v; break;
                    case TechEffect.WeaponRangeOne:    s.wRange[(int)n.weapon] += v; break;
                    case TechEffect.WeaponCooldownOne:
                        s.wCooldown[(int)n.weapon] *= Mathf.Pow(1f - n.value, rank); break;
                    case TechEffect.WeaponCountOne:    s.wCount[(int)n.weapon] += Mathf.RoundToInt(v); break;
                    case TechEffect.WeaponPierceOne:   s.wPierce[(int)n.weapon] += Mathf.RoundToInt(v); break;

                    // ⬜ 무기를 여는 노드는 스탯을 안 바꾼다 (`MetaSave.FillOwnedWeapons`가 읽는다)
                    case TechEffect.UnlockWeapon:      break;

                    // 수집 · 경제
                    case TechEffect.IntakeRadius:   s.intakeMul += v; break;
                    case TechEffect.TowWeight:      s.towWeightMul += v; break;
                    case TechEffect.TowCapacity:    s.towCapacityBonus += Mathf.RoundToInt(v); break;
                    case TechEffect.CarrierDrone:   s.carrierDrones += Mathf.RoundToInt(v); break;
                    // 랭크 1 → 등급 0(고철). 랭크가 오를수록 더 값진 것까지 보낸다
                    case TechEffect.HaulerGrade:
                        s.haulerGrade = Mathf.Max(s.haulerGrade, rank - 1); break;
                    case TechEffect.BossShotResist: s.bossShotResist = Mathf.Min(0.75f, s.bossShotResist + v); break;
                    case TechEffect.BossMatBonus:   s.bossMatBonus += v; break;

                    // 🔴 발동형 — 확률이라 1을 넘으면 뜻이 없다. 상한을 둔다
                    case TechEffect.ProcExplode:    s.procExplode = Mathf.Min(0.85f, s.procExplode + v); break;
                    case TechEffect.ProcChain:      s.procChain = Mathf.Min(0.85f, s.procChain + v); break;
                    case TechEffect.ProcDoubleShot: s.procDoubleShot = Mathf.Min(0.80f, s.procDoubleShot + v); break;
                    case TechEffect.KillSpeed:      s.killSpeed = Mathf.Min(1.5f, s.killSpeed + v); break;

                    case TechEffect.MatDoubleChance:s.matDoubleChance = Mathf.Min(0.90f, s.matDoubleChance + v); break;
                    case TechEffect.RareMatChance:  s.rareMatChance += v; break;
                    case TechEffect.MatValue:       s.matValue += v; break;
                    case TechEffect.LumpLife:       s.lumpLife += v; break;
                    case TechEffect.FuelDrain:
                        s.fuelDrainMul *= Mathf.Pow(1f - n.value, rank); break;
                    case TechEffect.KillBlast:      s.killBlast += v; break;
                    case TechEffect.BossFuel:       s.bossFuel += v; break;
                    case TechEffect.ShotSpeed:      s.shotSpeedMul += v; break;
                    case TechEffect.TowAuto:        s.towAuto = true; break;
                    case TechEffect.ValueMul:       s.valueMultiplier += v; break;
                    case TechEffect.RefineOnCollect:s.refinePerCollect += v; break;
                    case TechEffect.ItemDropChance: s.itemDropBonus += v; break;
                    case TechEffect.FuelPickupBonus:s.fuelPickupMul += v; break;

                    // 재화 발견
                    case TechEffect.ScrapFind:      s.scrapFind += v; break;
                    case TechEffect.CircuitFind:    s.circuitFind += v; break;
                    case TechEffect.CoreFind:       s.coreFind += v; break;
                    case TechEffect.MatFindAll:
                        s.scrapFind += v; s.circuitFind += v; s.coreFind += v; break;

                    // 시작 상태
                    case TechEffect.StartWeaponLevel: s.startWeaponLevel += Mathf.RoundToInt(v); break;
                }
            }
        }

    }
}
