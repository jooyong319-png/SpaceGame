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
        public float orbitRadiusMul = 1f;  // 궤도 반경

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
        // 🔴 **기지 포탑.** 0이면 포탑이 없다 — 첫 강화 카드를 먹어야 생긴다.
        //    기지가 처음부터 쏘면 초반이 너무 쉬워지고, "기지를 지킨다"는 긴장이 사라진다.
        //    스스로 싸우는 기지는 **보상**이어야 한다.
        public int baseTurretLevel;
        public float baseTurretPower = 1f;
        public float baseTurretRange = 1f;
        public float baseTurretHaste = 1f;
        public int baseTurretCount = 1;
        public float baseHpBonus;

        public float intakeMul = 1f;
        public float valueMultiplier = 1f;
        public float xpMultiplier = 1f;   // ⬜ 레벨업이 없다 (2026-08-26). 읽는 곳 없음

        /// <summary>🔴 끌 때 덜 무거워지는 정도. `RunDirector.TowWeightMul`이 읽는다.</summary>
        public float towWeightMul = 1f;

        /// <summary>🔴 끌 수 있는 개수 +N. `RunDirector.ShipTow`가 읽는다.</summary>
        public int towCapacityBonus;

        /// <summary>🔴 회수 드론 대수. `RunDirector.SyncDrones`가 읽는다.</summary>
        public int carrierDrones;

        /// <summary>🔴 보스 탄 피해 감소(0~1). `RunDirector.CheckBossShots`가 읽는다.</summary>
        public float bossShotResist;
        public float refinePerCollect;

        // ---- 카드 ----
        public int cardChoices = 3;

        // ---- 영구 강화가 채우는 런 밖 값들 ----
        public float startFuelRatio = 1f;   // 시작 연료 비율
        public int   startLevel;            // 시작 레벨 보정
        public int   startWeaponLevel;      // 시작 무기 레벨 보정
        public int   comboLevelDown;        // 조합 요구 레벨 감소
        public int   revives;               // 부활 횟수
        public float dashCooldownMul = 1f;
        public float itemDropBonus;         // %p
        public float fuelPickupMul = 1f;
        public float hazardResist;

        // ---- 재화 발견율 (1.0 = 기본) ----
        public float scrapFind = 1f, circuitFind = 1f, coreFind = 1f;

        public float FindMul(MatKind m)
            => m == MatKind.Scrap ? scrapFind : m == MatKind.Circuit ? circuitFind : coreFind;

        // ---- 단발성 버프 (2026-08-22 사용자 요청) ----
        //
        // 🔴 카드는 원래 영구 성장인데 이건 **몇 초짜리**다.
        //    그래서 수치를 아주 크게 잡는다 — 작으면 "고르면 손해인 카드"가 되고,
        //    손해인 선택지는 선택지가 아니다.
        //    대신 등급을 전설로 두고 화면에 남은 시간을 크게 띄운다.
        float burstPowerLeft, burstSizeLeft, burstHasteLeft;

        public float BurstPowerMul => burstPowerLeft > 0f ? 6f : 1f;   // 피해 +500%
        public float BurstSizeMul  => burstSizeLeft  > 0f ? 6f : 1f;   // 크기 +500%
        public float BurstHasteMul => burstHasteLeft > 0f ? 0.25f : 1f; // 쿨다운 -75%

        /// <summary>지금 켜져 있는 버프 중 가장 오래 남은 시간. HUD가 읽는다.</summary>
        public float BurstLeft => Mathf.Max(burstPowerLeft, Mathf.Max(burstSizeLeft, burstHasteLeft));

        public string BurstName =>
            burstPowerLeft > 0f ? "과부하 — 피해 500%"
          : burstSizeLeft  > 0f ? "확장 — 범위 500%"
          : burstHasteLeft > 0f ? "가속 — 쿨다운 75% 감소"
          : null;

        public void AddBurst(CardEffect kind, float seconds)
        {
            switch (kind)
            {
                case CardEffect.BurstPower: burstPowerLeft = Mathf.Max(burstPowerLeft, seconds); break;
                case CardEffect.BurstSize:  burstSizeLeft  = Mathf.Max(burstSizeLeft,  seconds); break;
                case CardEffect.BurstHaste: burstHasteLeft = Mathf.Max(burstHasteLeft, seconds); break;
            }
        }

        public void TickBursts(float dt)
        {
            if (burstPowerLeft > 0f) burstPowerLeft -= dt;
            if (burstSizeLeft  > 0f) burstSizeLeft  -= dt;
            if (burstHasteLeft > 0f) burstHasteLeft -= dt;
        }

        // ---- 무기 조합 (히든) ----
        /// <summary>열린 조합. None이면 아직 아니다.</summary>
        public ComboEffect combo = ComboEffect.None;
        public bool HasCombo(ComboEffect e) => combo == e;

        /// <summary>보유한 두 무기의 계열. 하나뿐이면 둘 다 같은 값이 된다.</summary>
        public bool OwnedTags(GameContent content, out WeaponTag a, out WeaponTag b)
        {
            a = WeaponTag.Cut; b = WeaponTag.Cut;
            OwnedPair(out int i, out int j);
            if (i < 0) return false;

            var da = content.Weapon((WeaponKind)i);
            if (da == null) return false;
            a = da.tag;

            if (j < 0) return false;
            var db = content.Weapon((WeaponKind)j);
            if (db == null) return false;
            b = db.tag;
            return true;
        }

        /// <summary>보유한 무기 둘을 순서대로 돌려준다. 아직 하나뿐이면 두 번째는 -1.</summary>
        public void OwnedPair(out int first, out int second)
        {
            first = -1; second = -1;
            for (int i = 0; i < weaponLevel.Length; i++)
            {
                if (weaponLevel[i] <= 0) continue;
                if (first < 0) first = i;
                else if (second < 0) second = i;
            }
        }
    }

    public static class TechSystem
    {
        /// <summary>런 시작 상태. 카드는 런 중에 여기 더해진다.</summary>
        /// <summary>`FillOwnedWeapons`가 채우는 임시 버퍼. 매 런마다 새로 만들 이유가 없다.</summary>
        static readonly List<WeaponKind> ownedBuf = new List<WeaponKind>();

        public static RunStats BuildStats(GameContent content, RunConfig cfg)
        {
            var s = new RunStats
            {
                fuelMax = cfg.fuelMax,
                thrustForce = cfg.thrustForce,
                damping = cfg.linearDamping,
                cardChoices = 3
            };

            // 🔴 영구 강화를 먼저 얹는다. 런 카드는 그 위에 쌓인다 —
            //    순서를 뒤집으면 "테크로 올린 값이 카드 배수를 못 받는" 이상한 상태가 된다.
            ApplyTechTree(s, content);

            // 🔴 그다음 우주선. 테크는 **더하기**, 우주선은 **곱하기**로 준다 —
            //    순서가 반대면 우주선 배수가 테크 보너스를 못 받아
            //    "테크를 올릴수록 배 차이가 줄어드는" 이상한 곡선이 된다.
            // 공짜 노드를 먼저 채워 둔다 — 안 그러면 첫 무기조차 안 열린 것으로 읽힌다
            MetaSave.EnsureFreeNodes(content);

            var ship = MetaSave.CurrentShip(content);
            ApplyShip(s, ship);

            // 🔴 **연 무기가 전부 붙는다** (2026-08-26 사장님 지시:
            //    *"무기는 장착이 아니라 추가다. 개수 제한은 없다"*).
            //
            //    고르는 방식이었을 때는 두 번째 무기를 사는 순간 첫 번째가 창고로 갔다 —
            //    **산 보람이 없다.** 인크리멘탈에서 산 것은 쌓여야 한다.
            //
            //    ⚠️ 하나도 안 열렸으면 배가 주던 무기를 준다. 무기가 없으면 40초를 구경만 한다.
            var fallback = ship != null ? ship.startingWeapon : cfg.startingWeapon;
            MetaSave.FillOwnedWeapons(content, ownedBuf, fallback);

            for (int i = 0; i < ownedBuf.Count; i++)
                s.AddWeapon(ownedBuf[i], 1 + s.startWeaponLevel);
            return s;
        }

        /// <summary>우주선 특성을 얹는다. 전부 배수라 테크 보너스 위에 곱해진다.</summary>
        public static void ApplyShip(RunStats s, ShipDef ship)
        {
            if (ship == null) return;

            s.fuelMax *= ship.fuelMul;
            s.thrustForce *= ship.thrustMul;
            s.damping *= ship.dampingMul;

            s.powerMul *= ship.powerMul;
            s.rangeMul *= ship.rangeMul;
            s.cooldownMul *= ship.cooldownMul;

            s.intakeMul *= ship.intakeMul;
            s.valueMultiplier *= ship.valueMul;

            // 🔴 음수도 허용한다 — 폭파선처럼 "맞으면 더 아픈" 맞바꾸기가 있어야
            //    배마다 성격이 생긴다. 다만 하한을 둬서 즉사로는 안 가게 한다.
            s.contactResist = Mathf.Clamp(s.contactResist + ship.contactResist, -0.5f, 0.7f);
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
                    case TechEffect.BossShotResist: s.bossShotResist = Mathf.Min(0.75f, s.bossShotResist + v); break;
                    case TechEffect.ValueMul:       s.valueMultiplier += v; break;
                    case TechEffect.XpMul:          s.xpMultiplier += v; break;
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
                    case TechEffect.StartLevel:       s.startLevel += Mathf.RoundToInt(v); break;
                    case TechEffect.StartWeaponLevel: s.startWeaponLevel += Mathf.RoundToInt(v); break;
                    case TechEffect.CardChoices:      s.cardChoices += Mathf.RoundToInt(v); break;
                    case TechEffect.ComboLevelDown:   s.comboLevelDown += Mathf.RoundToInt(v); break;
                }
            }
        }

        /// <summary>카드 하나를 런 능력치에 적용한다.</summary>
        public static void ApplyCard(RunStats s, CardDef card)
        // (기지 포탑 카드는 아래 switch에서 처리한다)
        {
            switch (card.effect)
            {
                case CardEffect.Weapon:
                    s.AddWeapon((WeaponKind)card.param, Mathf.Max(1, Mathf.RoundToInt(card.value)));
                    break;

                case CardEffect.ToolRange:       s.rangeMul += card.value; break;
                case CardEffect.ToolPower:       s.powerMul += card.value; break;
                case CardEffect.Cooldown:        s.cooldownMul *= (1f - card.value); break;

                case CardEffect.MoveSpeed:       s.speedMul += card.value; break;
                case CardEffect.ContactResist:   s.contactResist = Mathf.Min(0.7f, s.contactResist + card.value); break;
                case CardEffect.FuelMax:         s.fuelMax += card.value; break;
                case CardEffect.Thrust:          s.thrustForce += card.value; break;

                // ⬜ 무기 패턴별 강화. 카드 뽑기를 없애서(2026-08-23) **부르는 곳이 없다.**
                //    궤도·폭발 계열은 스탯 자체가 사라져 조용히 버린다.
                case CardEffect.OrbitCount:
                case CardEffect.OrbitSpin:
                case CardEffect.BlastCount:      break;
                case CardEffect.OrbitRadius:     s.orbitRadiusMul += card.value; break;
                case CardEffect.ProjectileCount: s.projectileCountBonus += Mathf.RoundToInt(card.value); break;
                case CardEffect.PierceBonus:     s.pierceBonus += Mathf.RoundToInt(card.value); break;
                case CardEffect.ChainTargets:    s.chainTargetBonus += Mathf.RoundToInt(card.value); break;

                // 🔴 단발성 — 즉시 켜진다. 영구값을 안 건드린다
                case CardEffect.BurstPower:
                case CardEffect.BurstSize:
                case CardEffect.BurstHaste:
                    s.AddBurst(card.effect, card.value);
                    break;

                case CardEffect.IntakeRadius:    s.intakeMul += card.value; break;
                case CardEffect.ValueMul:        s.valueMultiplier += card.value; break;
                case CardEffect.XpGain:          s.xpMultiplier += card.value; break;
                case CardEffect.RefineOnCollect: s.refinePerCollect += card.value; break;

                // 🔴 **기지 포탑** (2026-08-21 요청: "기지도 공격 기능 있게, 레벨업 보상으로 강화").
                //    기지가 스스로 싸우면 "지킨다"가 혼자 짊어지는 짐에서
                //    **같이 싸우는 것**으로 바뀐다. 내가 자리를 비워도 기지가 버텨 주므로
                //    멀리 나가는 선택에 값이 붙는다 — 그게 rev.7의 저울에 무게를 더한다.
                case CardEffect.BaseTurretLevel: s.baseTurretLevel += Mathf.Max(1, Mathf.RoundToInt(card.value)); break;
                case CardEffect.BaseTurretPower: s.baseTurretPower += card.value; break;
                case CardEffect.BaseTurretRange: s.baseTurretRange += card.value; break;
                case CardEffect.BaseTurretHaste: s.baseTurretHaste *= (1f - card.value); break;
                case CardEffect.BaseTurretCount: s.baseTurretCount += Mathf.RoundToInt(card.value); break;
                case CardEffect.BaseHpMax:       s.baseHpBonus += card.value; break;
            }
        }
    }
}
