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
        // 🔴 무기 '종류'가 아니라 **패턴**에 붙인다. 무기를 20종으로 늘려도
        //    노드를 다시 쓰지 않아도 되기 때문이다.
        public int orbitCountBonus;        // 궤도체 +N (절단날 · 방벽)
        public int projectileCountBonus;   // 발사체 +N (작살 · 원반)
        public int blastCountBonus;        // 폭발물 +N (폭탄 · 지뢰)
        public int chainTargetBonus;       // 연쇄 대상 +N (방전)
        public int pierceBonus;            // 관통 +N
        public float orbitSpinMul = 1f;    // 궤도 회전 속도
        public float orbitRadiusMul = 1f;  // 궤도 반경

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
        public float xpMultiplier = 1f;
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
            var ship = MetaSave.CurrentShip(content);
            ApplyShip(s, ship);

            // 🔴 시작 무기는 **우주선이 정한다.** 무기를 둘만 갖는 구조라
            //    이 한 줄이 그 판 조합의 절반을 결정한다.
            //    두 번째 무기는 런 중에 카드로 **얻어야** 한다 — 그게 이 게임의 첫 갈림길이다.
            var start = ship != null ? ship.startingWeapon : cfg.startingWeapon;
            s.AddWeapon(start, 1 + s.startWeaponLevel);
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

                    // 무기 패턴
                    case TechEffect.BladeCount:     s.orbitCountBonus += Mathf.RoundToInt(v); break;
                    case TechEffect.BladeSpin:      s.orbitSpinMul += v; break;
                    case TechEffect.HarpoonCount:   s.projectileCountBonus += Mathf.RoundToInt(v); break;
                    case TechEffect.HarpoonPierce:  s.pierceBonus += Mathf.RoundToInt(v); break;
                    case TechEffect.BombCount:      s.blastCountBonus += Mathf.RoundToInt(v); break;
                    case TechEffect.ArcTargets:     s.chainTargetBonus += Mathf.RoundToInt(v); break;
                    case TechEffect.BombRadius:
                    case TechEffect.VortexRadius:
                    case TechEffect.ArcRange:       s.rangeMul += v * 0.5f; break;
                    case TechEffect.VortexDamage:   s.powerMul += v * 0.5f; break;

                    // 수집 · 경제
                    case TechEffect.IntakeRadius:   s.intakeMul += v; break;
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

                // 🔴 무기 패턴별 강화 — 영구 강화(테크트리)와 같은 값을 쓴다
                case CardEffect.OrbitCount:      s.orbitCountBonus += Mathf.RoundToInt(card.value); break;
                case CardEffect.OrbitSpin:       s.orbitSpinMul += card.value; break;
                case CardEffect.OrbitRadius:     s.orbitRadiusMul += card.value; break;
                case CardEffect.ProjectileCount: s.projectileCountBonus += Mathf.RoundToInt(card.value); break;
                case CardEffect.PierceBonus:     s.pierceBonus += Mathf.RoundToInt(card.value); break;
                case CardEffect.BlastCount:      s.blastCountBonus += Mathf.RoundToInt(card.value); break;
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
