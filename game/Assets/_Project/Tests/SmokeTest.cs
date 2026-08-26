using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SalvageRun.Data;
using SalvageRun.Meta;
using SalvageRun.Run;

namespace SalvageRun.Tests
{
    /// <summary>
    /// 🔴 **스모크 테스트 — "터지나"만 본다.**
    ///
    ///    2026-08-21 사고에서 나왔다: <c>Weapons.Count</c>를 12로 박아 둔 채
    ///    13번째 무기(드릴)를 추가했고, **드릴을 고르는 순간 판이 죽었다.**
    ///    컴파일 검사는 열 번 넘게 통과했다 — 컴파일은 *"문법이 맞나"*만 보기 때문이다.
    ///
    ///    ⚠️ 이건 밸런스를 재지 않는다. **재는 건 오직 "예외 없이 굴러가나"다.**
    ///       그래서 빠르다 — 무기마다 4초씩, 전부 1분 안쪽.
    ///       밸런스 시뮬(<c>BalanceSim</c>)은 7분이 걸리므로 성격이 완전히 다르다.
    ///
    ///    🔴 실행:  tools\unity-test.ps1 -Only SmokeTest
    ///
    ///    유니티 테스트 프레임워크는 **처리되지 않은 Error 로그가 하나라도 뜨면 실패**시킨다.
    ///    그러니 여기서 단언을 많이 쓸 필요가 없다 — 굴리기만 하면 잡힌다.
    /// </summary>
    public class SmokeTest
    {
        const float StepSeconds = 1f / 30f;

        /// <summary>무기 하나당 굴리는 시간. 짧아도 초기화·첫 발사·첫 격파는 다 지난다.</summary>
        const int FramesPerWeapon = 120;   // 4초

        RunDirector director;
        GameObject bootGo;
        float originalTimeScale, originalCapture, originalFixed;
        float originalVolume;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            RunDirector.DeterministicDraft = true;
            MetaSave.DisableWrites = true;
            MetaSave.ReplaceInMemory(new MetaData());

            // 🔴 **소리를 끈다** (2026-08-23 사장님: *"시뮬할 때 소리 끄고 하라니까"*).
            //    PlayMode 테스트는 게임을 실제로 돌리므로 `Juice`가 만든 효과음이
            //    **스피커로 그대로 나온다.** 수십 판을 돌리면 몇 분간 계속 울린다.
            //    검사에 소리는 아무 값어치가 없다 — 끄는 게 맞다.
            originalVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            AudioListener.pause = true;

            originalTimeScale = Time.timeScale;
            originalCapture = Time.captureDeltaTime;
            originalFixed = Time.fixedDeltaTime;

            bootGo = new GameObject("== SMOKE BOOTSTRAP ==");
            bootGo.AddComponent<GreyboxBootstrap>();

            yield return null;
            yield return null;

            director = RunDirector.Instance;
            Assert.IsNotNull(director, "RunDirector가 만들어지지 않았다");

            Time.timeScale = 1f;
            Time.captureDeltaTime = StepSeconds;
            Time.fixedDeltaTime = StepSeconds;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioListener.volume = originalVolume;
            AudioListener.pause = false;

            Time.timeScale = originalTimeScale;
            Time.captureDeltaTime = originalCapture;
            if (originalFixed > 0f) Time.fixedDeltaTime = originalFixed;
            if (bootGo != null) Object.Destroy(bootGo);
            MetaSave.ReplaceInMemory(new MetaData());
            MetaSave.DisableWrites = false;
            RunDirector.DeterministicDraft = false;
            AutoPilot.ResetEngaged();
            if (director != null) director.CollectOverride = null;
            yield return null;
        }

        // ==============================================================================
        //  1. 무기 전 종류 — 고르면 죽지 않는가
        // ==============================================================================

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryWeaponRuns()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 무기 전 종류 ===========");

            var kinds = (WeaponKind[])System.Enum.GetValues(typeof(WeaponKind));
            t.AppendLine($"무기 {kinds.Length}종 · Weapons.Count = {Weapons.Count}");

            // 🔴 이 둘이 어긋나면 배열이 범위를 벗어난다 — 드릴 사고의 원인이 정확히 이것이었다
            Assert.AreEqual(kinds.Length, Weapons.Count,
                "🔴 WeaponKind 개수와 Weapons.Count가 다르다. 배열이 범위를 벗어난다");

            foreach (var k in kinds)
            {
                var def = director.content.Weapon(k);
                Assert.IsNotNull(def, $"🔴 {Weapons.Name(k)}({k})의 WeaponDef가 없다");

                yield return RunWithWeapon(k, 1);
                t.AppendLine($"  Lv.1  {Pad(Weapons.Name(k), 14)} OK");

                yield return RunWithWeapon(k, 10);
                t.AppendLine($"  Lv.10 {Pad(Weapons.Name(k), 14)} OK");
            }

            t.AppendLine("전부 예외 없이 굴렀다.");
            Debug.Log("[SMOKE]" + t);
            Assert.Pass();
        }

        // ==============================================================================
        //  2. 카드 전 종류 — 적용하면 죽지 않는가
        // ==============================================================================

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryCardApplies()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 카드 전 종류 ===========");

            director.StartRun(0);
            yield return null;

            var cards = director.content.cards;
            Assert.IsNotNull(cards, "카드 목록이 비어 있다");

            for (int i = 0; i < cards.Length; i++)
            {
                TechSystem.ApplyCard(director.Stats, cards[i]);
                director.arms.stats = director.Stats;
                director.arms.Rebuild();
                yield return null;
            }

            t.AppendLine($"카드 {cards.Length}장 전부 적용 — 예외 없음");

            // 🔴 개별로는 멀쩡한데 **다 겹쳤을 때** 터지는 경우가 있다
            //    (0 나눗셈, 무한대 반경 — 2026-08-21 연쇄 폭발이 정확히 그랬다)
            for (int f = 0; f < 90; f++) yield return null;

            t.AppendLine("전부 적용한 상태로 3초 — 예외 없음");
            director.ReturnNow();
            yield return null;

            Debug.Log("[SMOKE]" + t);
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **주우면 곧바로 경험치가 되고, 쌓이면 레벨이 오른다** (rev.12).
        ///
        ///    rev.6~11에서는 화물을 기지에 입금해야 레벨이 올랐다. 그 구조를 걷어냈으니
        ///    **이 한 줄이 게임의 심장**이다 — 여기가 끊기면 판이 아무 데도 안 간다.
        ///    (rev.4~5로 되돌리면서 `Absorb()`를 통째로 갈아 끼웠다. 갈아 끼운 곳은
        ///     컴파일이 통과해도 굴려 보기 전엔 아무것도 보장되지 않는다)
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TowingBringsMaterialsHome()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 주움 → 매달림 → 귀환 정산 ===========");

            director.StartRun(0);
            yield return null;

            var ship = director.ship;
            var field = director.field;

            int before = MetaSave.Data.scrap;

            // 🔴 **수집기를 켠다.** 2026-08-26부터 Space를 누른 동안만 줍는다 —
            //    검사는 키보드를 못 누르므로 스위치로 대신 켠다.
            //    (이걸 빼면 "하나도 안 매달렸다"로 실패하는데, 그건 게임이 아니라 검사 탓이다)
            director.CollectOverride = true;

            // 🔴 배 **바로 위**에 뿌린다. 자석이 없어졌으므로(2026-08-26)
            //    떨어져 있으면 영영 안 붙는다 — 닿아야 붙는다.
            Vector2 at = ship.transform.position;
            field.SpillCargo(at, 12, 8);
            yield return null;
            for (int i = 0; i < 60 && director.TowedCount < 3; i++) yield return null;

            t.AppendLine($"  매달린 것 {director.TowedCount}개 · 속도 배수 {director.TowWeightMul:0.00}");

            Assert.Greater(director.TowedCount, 0,
                "🔴 배 위에 뿌렸는데 하나도 안 매달렸다 — 줍는 경로가 끊겼다");
            Assert.Less(director.TowWeightMul, 1f,
                "🔴 짐을 실었는데 안 무거워졌다 — 무게가 조작에 안 실린다");

            // 🔴 **끌고 온 것만 내 것이 된다.** 귀환해야 저장에 들어간다
            int towed = director.TowedCount;
            director.ReturnNow();
            yield return null;

            int gained = MetaSave.Data.scrap - before;
            t.AppendLine($"  귀환 정산 — 고철 +{gained} (가져옴 {director.BankedCount}개)");

            Assert.AreEqual(towed, director.BankedCount,
                "🔴 매달고 있던 개수와 가져온 개수가 다르다");
            Assert.Greater(gained, 0, "🔴 끌고 왔는데 재화가 안 들어왔다");

            Debug.Log("[SMOKE]" + t);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **보스가 진행의 유일한 관문이다** (2026-08-26).
        ///
        ///    보스를 부숴야 다음 구역이 열린다. 그런데 보스는 **연료가 다 되기 전에
        ///    닿을 수 있어야** 한다 — 못 닿으면 게임이 통째로 막힌다.
        ///    실제로 그랬던 적이 있다: 보스가 300초에 나오는데 연료가 40초였다.
        ///
        ///    ⚠️ 이 검사가 없으면 **깰 수 없는 게임**이 조용히 나간다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BossArrivesWithinFuel()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 보스가 연료 안에 오는가 ===========");

            director.StartRun(0);
            yield return null;

            var st = director.Stage;
            float bossAt = st.waveCount * st.waveSeconds;
            float budget = director.ship.FuelMax / director.Config.idleFuelPerSecond;

            // 🔴 **여유를 같이 찍는다.** "42 vs 48"만 보면 통과라서 넘어가는데,
            //    실제로 보스전에 쓸 수 있는 시간은 **6초**다.
            //    이 줄이 없어서 *"보스에 닿는다"*를 *"보스를 깬다"*로 오래 착각했다.
            t.AppendLine($"  보스 등장 {bossAt:0}초 · 판 길이 {budget:0}초 " +
                         $"→ 보스전 여유 **{budget - bossAt:0.0}초**");

            Assert.Less(bossAt, budget,
                $"🔴 보스가 {bossAt:0}초에 나오는데 연료는 {budget:0}초뿐이다 — 깰 수 없는 게임이다");

            Debug.Log("[SMOKE]" + t);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **보스 탄에 맞으면 연료가 닳는다** (2026-08-26 사장님 지시).
        ///    위협이 이것 하나뿐이라 여기가 끊기면 **게임에 긴장이 0**이 된다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BossShotsBurnFuel()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 보스 탄 = 연료 손실 ===========");

            director.StartRun(0);
            yield return null;

            var ship = director.ship;
            var field = director.field;

            // 🔴 보스가 나올 때까지 기다리지 않는다 — 부위를 직접 세우고 탄을 쏜다.
            //    기다리면 42초가 걸리고, 그 사이 다른 것이 섞여 무엇을 잰 건지 흐려진다.
            ship.ControlEnabled = false;
            field.Spawning = false;

            int parts = field.SpawnBossParts(2, 40f);
            Assert.Greater(parts, 0, "🔴 보스 부위가 하나도 안 생겼다");
            yield return null;

            JunkPiece shooter = null;
            for (int i = 0; i < field.Pieces.Count; i++)
                if (field.Pieces[i].Alive && field.Pieces[i].IsBossPart) { shooter = field.Pieces[i]; break; }
            Assert.IsNotNull(shooter, "🔴 살아 있는 보스 부위를 못 찾았다");

            // 🔴 판정은 보스 국면에서만 돈다 — 부위만 세워 두면 아무 일도 안 일어난다
            director.ForceBossPhaseForTest();

            Vector2 from = shooter.transform.position;
            Vector2 dir = ((Vector2)ship.transform.position - from).normalized;

            float before = ship.Fuel;
            int hitsBefore = director.BossHits;

            field.FireEnemyShot(shooter, from, dir);

            for (int i = 0; i < 240 && director.BossHits == hitsBefore; i++) yield return null;

            float drop = before - ship.Fuel;
            t.AppendLine($"  피격 {director.BossHits - hitsBefore}회 · 연료 {before:0.0} → {ship.Fuel:0.0}");

            Assert.Greater(director.BossHits, hitsBefore,
                "🔴 보스 탄이 배에 닿았는데 맞은 것으로 안 친다 — 위협이 통째로 없다");
            Assert.Greater(drop, director.Config.bossShotFuelCost * 0.5f,
                "🔴 맞았는데 연료가 그만큼 안 줄었다");

            Debug.Log("[SMOKE]" + t);
            ship.ControlEnabled = true;
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **플레이어는 무적이다** (2026-08-23 사장님:
        ///    *"플레이어를 공격하는 것도 없애고, 플레이어는 무적이야"*).
        ///
        ///    쓰레기를 배 위에 얹어 놓고 굴려도 **연료가 접촉으로 줄지 않아야** 한다.
        ///    ⚠️ 추진·생명유지로는 계속 줄기 때문에 "안 준다"로는 못 잰다 —
        ///       배를 세워 두고 **가만히 있을 때의 감소량과 같은가**로 잰다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator ContactDoesNotHurt()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 무적 ===========");

            director.StartRun(0);
            yield return null;

            var ship = director.ship;
            var field = director.field;

            // 🔴 **무기를 끈다.** 배 위에 얹어 둔 쓰레기를 무기가 부숴 버리면
            //    접촉이 일어나기 전에 표적이 사라진다 (조준이 자동이라 반드시 쏜다)
            for (int i = 0; i < director.Stats.weaponLevel.Length; i++)
                director.Stats.weaponLevel[i] = 0;
            director.arms.Rebuild();

            // 조종을 끊어 추진 소모를 없앤다. 남는 건 생명유지뿐이다
            ship.ControlEnabled = false;
            ship.AimOverride = null;
            ship.ThrustOverride = null;

            // 1) 아무것도 안 닿는 상태에서 30프레임 — 기준선
            field.Spawning = false;
            for (int i = 0; i < field.Pieces.Count; i++)
                if (field.Pieces[i].Alive) field.BreakJunkSilently(field.Pieces[i]);
            yield return null;

            float a0 = ship.Fuel;
            for (int i = 0; i < 30; i++) yield return null;
            float idleDrop = a0 - ship.Fuel;

            // 🔴 기준선이 0이면 아래 비교가 **아무것도 안 재는 검사**가 된다.
            //    (회복 지점 안에서 재던 시절 실제로 그랬다)
            Assert.Greater(idleDrop, 0.01f,
                "🔴 기준선이 0이다 — 타이머가 안 간다. 비교가 무의미해진다");

            // 2) 쓰레기를 배 위에 얹고 같은 시간 — 더 줄면 접촉 피해가 살아 있는 것이다
            //
            // 🔴 **한 프레임 기다려서는 안 나온다.** 유입은 초당 2.5개라
            //    한 프레임(1/30초)에 0.08개꼴이고, 나오자마자 무기가 부수기도 한다.
            //    "쓰레기가 하나도 없다"로 실패한 적이 있는데 **스폰이 끊긴 게 아니라
            //    검사가 너무 급했던 것**이었다 (2026-08-23).
            field.Spawning = true;
            JunkPiece junk = null;
            for (int i = 0; i < 120 && junk == null; i++)
            {
                yield return null;
                junk = NearestFarmJunk(field);
            }
            Assert.IsNotNull(junk, "🔴 4초를 기다려도 쓰레기가 안 나온다 — 스폰이 끊겼다");

            float b0 = ship.Fuel;
            for (int i = 0; i < 30; i++)
            {
                junk.transform.position = ship.transform.position;   // 매 프레임 겹쳐 둔다
                yield return null;
            }
            float touchDrop = b0 - ship.Fuel;

            t.AppendLine($"  가만히 {idleDrop:0.00} · 겹친 채 {touchDrop:0.00}");

            Assert.LessOrEqual(touchDrop, idleDrop + 0.01f,
                "🔴 쓰레기에 닿았더니 연료가 더 줄었다 — 무적이 아니다");

            Debug.Log("[SMOKE]" + t);
            ship.ControlEnabled = true;
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **연료는 타이머다 — 아무것도 안 해도 준다** (2026-08-23 사장님:
        ///    *"연료는 자동으로 닳게 해줘, 타이머 개념인거지"*).
        ///
        ///    ⚠️ 이 검사가 없으면 **끝나지 않는 게임**이 조용히 나갈 수 있다.
        ///       위협을 전부 뺐으므로 이 타이머가 멈추면 패배 조건 자체가 사라진다.
        ///       실제로 그 직전까지 갔었다 — 추진할 때만 닳던 판본에서는
        ///       가만히 서서 자동 무기로 캐는 것이 **죽지 않는 최적 플레이**였다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator FuelDrainsOnItsOwn()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 연료 타이머 ===========");

            director.StartRun(0);
            yield return null;

            var ship = director.ship;

            // ⬜ 예전에는 여기서 배를 모선 밖으로 옮겼다. 초당 12씩 채워져서
            //    **연료가 아예 안 줄었기 때문**이다 (검사가 그걸로 한 번 실패했고, 맞는 실패였다).
            //    2026-08-23에 모선을 통째로 없애면서 그럴 필요가 없어졌다.

            // 🔴 **아무것도 안 한다.** 조종도 끄고, 스폰도 끄고, 판을 비운다 —
            //    캐서 버는 연료가 섞이면 "저절로 주는가"를 못 잰다.
            ship.ControlEnabled = false;
            ship.AimOverride = null;
            ship.ThrustOverride = null;
            director.field.Spawning = false;
            for (int i = 0; i < director.field.Pieces.Count; i++)
                if (director.field.Pieces[i].Alive)
                    director.field.BreakJunkSilently(director.field.Pieces[i]);
            yield return null;

            float before = ship.Fuel;
            const int frames = 60;                       // 2초 (captureDeltaTime = 1/30)
            for (int i = 0; i < frames; i++) yield return null;

            float drop = before - ship.Fuel;
            float want = director.Config.idleFuelPerSecond * (frames * StepSeconds);

            t.AppendLine($"  {frames * StepSeconds:0.0}초 동안 {drop:0.00} 감소 (기대 {want:0.00})");

            Assert.Greater(drop, 0.01f,
                "🔴 가만히 있는데 연료가 안 준다 — 타이머가 안 간다 = 판이 안 끝난다");
            Assert.AreEqual(want, drop, want * 0.35f,
                "🔴 감소량이 설정값과 다르다 — 어딘가에서 또 깎거나 채우고 있다");

            Debug.Log("[SMOKE]" + t);
            ship.ControlEnabled = true;
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **연료가 0이면 판이 끝난다 — 그리고 그것 말고는 끝나는 길이 없다.**
        ///
        ///    2026-08-23에 위협을 전부 뺐으므로 **이게 유일한 패배 조건**이다.
        ///    이 한 줄이 끊기면 게임이 **끝나지 않는다** — 그건 지루한 게 아니라 고장이다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator RunEndsWhenFuelRunsOut()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 연료 0 = 판 종료 ===========");

            director.StartRun(0);
            yield return null;

            var ship = director.ship;

            // 타이머가 다 갈 때까지 3분을 기다릴 수는 없으니 바닥 직전까지 태운다
            ship.ConsumeFuel(ship.Fuel - 3f);
            t.AppendLine($"  남은 연료 {ship.Fuel:0.0}");

            Assert.AreEqual(GameState.Field, director.State, "🔴 아직 연료가 남았는데 판이 끝났다");

            ship.ConsumeFuel(ship.Fuel);
            yield return null;
            yield return null;

            t.AppendLine($"  연료 0 → 상태 {director.State} · {director.LastMessage}");
            Assert.AreEqual(GameState.Result, director.State,
                            "🔴 연료가 0인데 판이 안 끝났다 — 지는 방법이 없는 게임이다");

            Debug.Log("[SMOKE]" + t);
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **무기가 실제로 뭔가를 부수는가.**
        ///
        ///    `EveryWeaponRuns`는 *"터지지 않는다"*까지만 본다.
        ///    그런데 피해 0·사거리 0·특성 오설정인 무기는 **예외를 안 내고 조용히 죽어 있다** —
        ///    화면에서는 "이 무기 약하네"로 보여서 밸런스 문제로 오인하기 딱 좋다.
        ///
        ///    각 무기를 밭 한가운데 세워 두고 8초 굴려 **한 개라도 부수는지** 본다.
        ///    ⚠️ 이건 세기를 재는 게 아니다 — **0인가 아닌가**만 본다.
        ///       숫자 비교는 밸런스 시뮬의 몫이다.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator EveryWeaponActuallyBreaksSomething()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 무기가 실제로 부수는가 ===========");

            var kinds = (WeaponKind[])System.Enum.GetValues(typeof(WeaponKind));
            var dead = new System.Collections.Generic.List<string>();

            foreach (var k in kinds)
            {
                director.StartRun(0);

                var st = director.Stats;
                for (int i = 0; i < st.weaponLevel.Length; i++) st.weaponLevel[i] = 0;
                st.AddWeapon(k, 10);
                director.arms.stats = st;
                director.arms.Rebuild();
                yield return null;

                var field = director.field;
                var ship = director.ship;

                int before = field.BrokenTotal;

                for (int f = 0; f < 240; f++)      // 8초
                {
                    if (director.State == GameState.Result) break;

                    // 🔴 **매 프레임 밭을 찾아가 옆에 붙는다.**
                    //
                    //    처음엔 배를 원점(기지)에 세웠는데, `BaseClearRadius = 18`이라
                    //    **기지 반경 안에는 밭이 아예 안 생긴다.** 그래서 조준형 무기
                    //    (회수 원반·절단 레이저)가 빈 공간에 쏘고 0을 기록했다 —
                    //    무기가 죽은 게 아니라 **표적이 없었다.**
                    //
                    //    🔴 2026-08-22에 이 유형으로 두 번 헛다리를 짚었다.
                    //       테스트가 실패하면 **게임보다 테스트를 먼저 의심할 것.**
                    var mark = NearestFarmJunk(field);
                    if (mark != null)
                    {
                        Vector2 at = mark.transform.position;
                        ship.transform.position = at + Vector2.left * 2.5f;
                        ship.AimOverride = at;
                    }
                    yield return null;
                }

                ship.AimOverride = null;
                int broke = field.BrokenTotal - before;

                t.AppendLine($"  {Pad(Weapons.Name(k), 14)} {broke,4}개");
                if (broke <= 0) dead.Add(Weapons.Name(k));

                director.ReturnNow();
                yield return null;
                director.BackToReady();
                yield return null;
            }

            if (dead.Count > 0)
                t.AppendLine($"🔴 아무것도 못 부순 무기: {string.Join(", ", dead)}");
            else
                t.AppendLine("전부 최소 한 개는 부쉈다.");

            Debug.Log("[SMOKE]" + t);

            Assert.IsEmpty(dead,
                "🔴 예외는 안 나지만 **아무것도 못 부수는 무기**가 있다 — " +
                "화면에서는 '약한 무기'로 보여 밸런스 문제로 오인하게 된다");
            Assert.Pass();
        }

        // ==============================================================================

        IEnumerator RunWithWeapon(WeaponKind k, int level)
        {
            director.StartRun(0);

            var s = director.Stats;
            for (int i = 0; i < s.weaponLevel.Length; i++) s.weaponLevel[i] = 0;
            s.AddWeapon(k, level);

            director.arms.stats = s;
            director.arms.Rebuild();

            var ship = director.ship;

            for (int f = 0; f < FramesPerWeapon; f++)
            {
                if (director.State == GameState.Result) break;

                AutoPilot.Drive(director, ship);
                yield return null;
            }

            AutoPilot.Release(ship);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
        }

        /// <summary>
        /// 🔴 **테크 노드 하나하나가 실제로 능력치를 바꾸는가.**
        ///
        ///    이 프로젝트에서 여러 번 난 사고다: `TechEffect`에 값을 넣고,
        ///    `TechNodeDef`에서 그걸 가리키고, **아무도 그 효과를 읽지 않는다.**
        ///    컴파일도 통과하고 스모크도 통과한다 — 사장님이 재화를 쓰고 아무 일도 안 일어난다.
        ///
        ///    그래서 노드마다 **혼자만 찍은 `RunStats`를 기본값과 대조한다.**
        ///    한 글자도 안 달라지면 그 노드는 죽은 노드다.
        ///
        ///    ⚠️ 공짜 노드는 기본값에도 이미 들어가 있으므로 판정에서 뺀다
        ///       (`EnsureFreeNodes`가 랭크 1로 채워 두기 때문이다).
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator EveryTechNodeChangesSomething()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 테크 노드가 실제로 먹히는가 ===========");

            var tree = director.content.techTree;
            Assert.IsNotNull(tree, "테크트리가 비어 있다");
            Assert.Greater(tree.Length, 0, "테크트리에 노드가 없다");

            var meta = MetaSave.Data;
            var saved = new List<NodeRank>(meta.nodes);

            meta.nodes.Clear();
            string baseline = Snapshot(TechSystem.BuildStats(director.content, director.config));

            // 공짜 노드는 기본값에 이미 섞여 있다 — 대조 대상에서 빼기 위해 먼저 걷어 둔다
            var freeSet = new HashSet<string>();
            for (int i = 0; i < meta.nodes.Count; i++) freeSet.Add(meta.nodes[i].id);

            var dead = new List<string>();
            int checkedCount = 0;

            for (int i = 0; i < tree.Length; i++)
            {
                var n = tree[i];
                if (n == null || string.IsNullOrEmpty(n.id)) continue;

                meta.nodes.Clear();
                meta.SetRank(n.id, Mathf.Max(1, n.maxRank));
                string one = Snapshot(TechSystem.BuildStats(director.content, director.config));

                if (freeSet.Contains(n.id) && n.maxRank <= 1) continue;   // 기본값과 같을 수밖에 없다
                checkedCount++;
                if (one == baseline) dead.Add($"{n.id}({n.effect})");
            }

            meta.nodes.Clear();
            meta.nodes.AddRange(saved);
            yield return null;

            t.AppendLine($"  노드 {tree.Length}개 · 대조 {checkedCount}개");
            t.AppendLine(dead.Count == 0
                ? "  전부 능력치를 바꾼다 — 죽은 노드 없음"
                : $"  🔴 아무것도 안 바꾸는 노드 {dead.Count}개: {string.Join(", ", dead)}");

            Debug.Log("[SMOKE]" + t);
            Assert.IsEmpty(dead, "효과가 아무 데도 안 읽히는 테크 노드가 있다 — " + string.Join(", ", dead));
        }

        /// <summary>`RunStats`의 public 필드를 통째로 문자열로 만든다. 배열도 편다.</summary>
        static string Snapshot(RunStats s)
        {
            var sb = new StringBuilder();
            var fields = typeof(RunStats).GetFields(BindingFlags.Public | BindingFlags.Instance);
            System.Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            for (int i = 0; i < fields.Length; i++)
            {
                object v = fields[i].GetValue(s);
                sb.Append(fields[i].Name).Append('=');
                if (v is System.Array arr)
                    for (int k = 0; k < arr.Length; k++) sb.Append(arr.GetValue(k)).Append(',');
                else sb.Append(v);
                sb.Append('|');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 🔴 **보스를 부수는 데 몇 초가 필요한가.**
        ///
        ///    2026-08-26 밸런스 시뮬에서 **보스를 3판 중 0판 클리어**했다.
        ///    닿기는 하는데 도착할 때 연료가 이미 0이라 부술 시간이 없었다.
        ///    그런데 *"몇 초가 있어야 부술 수 있는가"*를 재는 곳이 어디에도 없었다 —
        ///    그 숫자가 없으면 **웨이브를 줄일지 HP를 낮출지 고를 수가 없다.**
        ///
        ///    ⚠️ 여기서 연료를 계속 채워 준다. 연료가 떨어져 판이 끝나면
        ///       *"보스가 얼마나 단단한가"*가 아니라 *"연료가 얼마나 짧은가"*를 재게 된다.
        ///       두 개를 한 번에 재면 어느 쪽을 고쳐야 할지 알 수 없다.
        ///
        ///    ⚠️ 무기는 **중반 상태**(3종 · Lv.5)로 맞춘다. 초반 무기로 재면
        ///       "안 부서진다"만 나오고, 실제로 보스를 만나는 시점은 초반이 아니다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BossTakesHowLongToKill()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 보스를 부수는 데 몇 초 ===========");

            director.StartRun(0);
            yield return null;

            var ship = director.ship;
            var field = director.field;
            var stats = director.Stats;

            // 🔴 **두 가지 상태로 잰다** (2026-08-27 사장님: *"강화해서 잡게 해야지
            //    왜 시간을 늘리려고 하냐"*). 맞는 말이라 질문을 바꿨다 —
            //    *"보스가 6초에 맞나"*가 아니라 **"플레이어가 자라서 보스에 닿나"**다.
            //
            //    · `1구역 천장` = 1구역에서 살 수 있는 것을 **전부 최대로** 찍은 상태.
            //      작살 1종 + `pow1` 8랭크(+32%)가 끝이다 — 다른 무기는 회로가 들어 못 산다
            //    · `중반`     = 무기 3종 Lv.5 (2구역 이후를 가정)
            //
            //    1구역 천장에서 얼마나 모자라는지가 **"강화로 닿을 수 있는가"의 답**이다.
            //    · `트리 완주`  = **108노드 전부 최대 랭크.** 더 자랄 데가 없는 상태다.
            //      여기서도 못 닿으면 *"강화해서 잡는다"*가 구조적으로 불가능하다는 뜻이다
            // 🔴 **트리 완주는 6구역에서 재야 뜻이 있다** (2026-08-27).
            //    셋 다 1구역에서 재고 있었다 — 다 자란 플레이어가 첫 보스를 뭉개는 건
            //    **정상**이지 문제가 아니다. *"보스가 너무 쉽다"*는 말이 겨누는 곳은
            //    **끝까지 자란 뒤 만나는 마지막 보스**다.
            yield return MeasureBoss(t, "1구역 천장", stage1: true);
            //    ⚠️ `무기만3종`은 **노드를 하나도 안 찍은** 인위적 상태다 (무기만 손으로 Lv.5).
            //       연료 노드가 없어 판이 48초뿐이라 🔴가 뜨는데, **그건 당연한 것이고
            //       밸런스 신호가 아니다.** 화력만 따로 보기 위한 대조군이다 —
            //       실제 플레이어가 무기 3종을 가질 무렵이면 연료 노드도 찍혀 있다.
            yield return MeasureBoss(t, "무기만3종·노드0", mid: true);
            yield return MeasureBoss(t, "트리 완주·1구역", fullTree: true);
            yield return MeasureBoss(t, "트리 완주·6구역", fullTree: true, map: 5);

            Debug.Log("[SMOKE]" + t);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>보스를 한 상태로 때려 보고 몇 초 걸리는지 적는다.</summary>
        IEnumerator MeasureBoss(StringBuilder t, string label,
                                bool stage1 = false, bool mid = false, bool fullTree = false,
                                int map = 0)
        {
            // 🔴 **노드를 실제로 찍어서 만든다.** 처음엔 `powerBonus: 0.32f`처럼
            //    손으로 흉내 냈는데, 그러면 **연료 노드가 빠진다** —
            //    그리고 연료가 곧 판 길이라 *"보스전에 몇 초 쓸 수 있나"*가 통째로 틀린다.
            //    (1구역 천장을 48초로 재고 있었는데 실제로는 연료 노드까지 찍으면 훨씬 길다)
            //
            //    ⚠️ **StartRun 전에** 찍어야 한다 — `RebuildStats`가 StartRun 안에서 돈다.
            var savedNodes = new List<NodeRank>(MetaSave.Data.nodes);
            var all = director.content.techTree;

            if (stage1)
            {
                // 1구역에서 살 수 있는 것 = 회로·코어가 안 드는 것 (선행조건까지 따라간다)
                var ok = new HashSet<string>();
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var n = all[i];
                        if (ok.Contains(n.id)) continue;
                        if (!ScrapOnly(n)) continue;
                        bool reqOk = true;
                        if (n.requires != null)
                            for (int r = 0; r < n.requires.Length; r++)
                                if (!ok.Contains(n.requires[r])) { reqOk = false; break; }
                        if (reqOk) { ok.Add(n.id); changed = true; }
                    }
                }
                for (int i = 0; i < all.Length; i++)
                    if (ok.Contains(all[i].id))
                        MetaSave.Data.SetRank(all[i].id, Mathf.Max(1, all[i].maxRank));
            }
            else if (fullTree)
            {
                for (int i = 0; i < all.Length; i++)
                    MetaSave.Data.SetRank(all[i].id, Mathf.Max(1, all[i].maxRank));
            }

            director.StartRun(map);
            yield return null;

            var ship = director.ship;
            var field = director.field;
            var stats = director.Stats;

            // `중반`만 손으로 맞춘다 (2구역 이후를 가정한 가상의 상태라 노드로 표현이 안 된다)
            if (mid)
                for (int i = 0; i < stats.weaponLevel.Length; i++) stats.weaponLevel[i] = 5;

            int wCount = 0, wLvSum = 0;
            for (int i = 0; i < stats.weaponLevel.Length; i++)
                if (stats.weaponLevel[i] > 0) { wCount++; wLvSum += stats.weaponLevel[i]; }

            director.arms.stats = stats;
            director.arms.Rebuild();

            field.Spawning = false;                    // 잡것이 섞이면 무엇을 잰 건지 흐려진다

            // 🔴 **실전과 같은 값으로 세운다.** RunDirector가 쓰는 식 그대로:
            //    ⚠️ 식을 **복사하지 않는다.** 예전엔 여기 `55 + rank * 45`를 적어 뒀는데
            //       게임 쪽 값을 고쳐도 검사는 옛 값으로 재서 **표가 조용히 거짓말했다.**
            float hpScale = RunDirector.BossPartHp(director.Stage.rank);
            int parts = field.SpawnBossParts(4, hpScale);
            Assert.Greater(parts, 0, "🔴 보스 부위가 하나도 안 생겼다");

            // 🔴 **부위 수를 같이 넘긴다.** 안 넘기면 첫 부위를 부수는 순간
            //    `Clear()`가 돌아 배 조종이 꺼진다 — 세게 만들수록 빨리 얼어붙는다.
            director.ForceBossPhaseForTest(parts);
            yield return null;

            int total = 0;
            for (int i = 0; i < field.Pieces.Count; i++)
                if (field.Pieces[i].Alive && field.Pieces[i].IsBossPart) total++;

            int frames = 0, alive = total;
            float clearedAt = -1f;
            // 🔴 40초면 초당 깎는 양을 재는 데 충분하다. 어차피 **비율로 늘려 쓰므로**
            //    120초를 봐도 답이 더 정확해지지 않는다 — 검사만 세 배 느려진다
            //    (120초 × 세 상태로 뒀더니 스모크 한 바퀴가 748초 걸렸다)
            const int Cap = 30 * 40;

            // 🔴 **왜 느린지도 같이 잰다.** 초당 깎는 양만 보면
            //    *"화력이 약한가 / 못 붙는가 / 짐이 무거운가"*를 못 가른다.
            //    트리를 다 찍었는데 초당이 절반이라 원인을 좁혀야 한다.
            float distSum = 0f, speedSum = 0f, throttleSum = 0f, aimSum = 0f;
            int towMax = 0, inRange = 0, atWall = 0;

            while (frames < Cap && alive > 0)
            {
                AutoPilot.Drive(director, ship);
                ship.Refuel(999f);                     // 연료 축은 여기서 재지 않는다
                frames++;
                yield return null;

                AutoPilot.NearestBossPart(director, ship.transform.position, out float bd);
                if (bd < 900f)
                {
                    distSum += bd;
                    if (bd <= 6f) inRange++;           // 대략 무기 사거리 안
                }
                speedSum += ship.Velocity.magnitude;
                towMax = Mathf.Max(towMax, director.TowedCount);

                // 🔴 **밀고는 있는가.** 순항 165짜리 배가 0.3으로 움직이면
                //    힘이 약한 게 아니라 **아예 안 밀고 있는** 것이다.
                //    `AimPoint`는 맵 경계로 잘린다(`ClampToBounds`) — 배가 가장자리에 있으면
                //    조준점이 배 자신한테로 잘려 거리가 0이 되고 스로틀이 0이 된다.
                throttleSum += ship.ThrottleNow;
                aimSum += ((Vector2)ship.AimPoint - (Vector2)ship.transform.position).magnitude;
                Vector2 sp = ship.transform.position;
                if (Mathf.Abs(Mathf.Abs(sp.x) - director.MapHalf.x) < 0.5f ||
                    Mathf.Abs(Mathf.Abs(sp.y) - director.MapHalf.y) < 0.5f) atWall++;

                alive = 0;
                for (int i = 0; i < field.Pieces.Count; i++)
                    if (field.Pieces[i].Alive && field.Pieces[i].IsBossPart) alive++;

                if (alive == 0 && clearedAt < 0f) clearedAt = frames * StepSeconds;
            }
            AutoPilot.Release(ship);

            float spent = frames * StepSeconds;
            t.AppendLine($"  [{label}] 무기 {wCount}종(합 Lv.{wLvSum}) · 피해배수 {stats.powerMul:0.00} · " +
                         $"부순 것 {total - alive}/{total}");

            // 🔴 **못 부숴도 얼마나 깎았는지는 봐야 한다.**
            //    부순 *개수*로만 재면 하나도 못 부순 경우가 전부 "영원히"로 뭉개진다 —
            //    5% 남은 것과 95% 남은 것이 같아 보여서 **얼마나 모자라는지를 알 수 없다.**
            //    그래서 **남은 HP 비율**로 잰다.
            float leftRatio = 0f;
            for (int i = 0; i < field.Pieces.Count; i++)
                if (field.Pieces[i].Alive && field.Pieces[i].IsBossPart)
                    leftRatio += field.Pieces[i].HpRatio;

            float doneRatio = total - leftRatio;                 // 부위 단위로 몇 개어치 깎았나
            float dps = spent > 0.01f ? doneRatio * hpScale / spent : 0f;
            float need = dps > 0.01f ? total * hpScale / dps : -1f;
            t.AppendLine($"       {spent:0}초 동안 {doneRatio:0.00}부위어치 깎음 · 초당 {dps:0.0} → " +
                         "4부위 전부에 " + (need > 0f ? $"약 {need:0}초" : "영원히") + " 필요");
            t.AppendLine($"       평균거리 {distSum / Mathf.Max(1, frames):0.0} · " +
                         $"사거리 안 {inRange * 100f / Mathf.Max(1, frames):0}% · " +
                         $"평균속도 {speedSum / Mathf.Max(1, frames):0.0} · 최대 짐 {towMax}개");

            // 🔴 **움직임의 재료를 그대로 찍는다.** 순항 속도 = 힘 ÷ 감쇠 인데
            //    계산상으로는 트리 완주가 더 빨라야 한다 — 그런데 실측이 0.3이다.
            //    어느 값이 예상과 다른지는 **값을 봐야** 안다. 또 추측하면 또 헛짚는다.
            t.AppendLine($"       힘 {stats.thrustForce:0} · 감쇠 {stats.damping:0.00} · " +
                         $"속도배수 {stats.speedMul:0.00} → 순항 " +
                         $"{stats.thrustForce * stats.speedMul / Mathf.Max(0.01f, stats.damping):0.0} · " +
                         $"쿨배수 {stats.cooldownMul:0.00} · 사거리배수 {stats.rangeMul:0.00}");
            t.AppendLine($"       평균스로틀 {throttleSum / Mathf.Max(1, frames):0.00} · " +
                         $"조준점까지 {aimSum / Mathf.Max(1, frames):0.0} · " +
                         $"경계에 붙어있던 시간 {atWall * 100f / Mathf.Max(1, frames):0}%");

            // 🔴 **연료 예산과 나란히 놓는다.** 이 숫자 하나만 보면 판단이 안 선다 —
            //    "보스가 몇 초에 나오고, 그때부터 몇 초가 남는가"와 붙어야 뜻이 생긴다
            //    ⚠️ **연료는 초가 아니다.** 처음에 `fuelMax`를 그대로 초로 적었다가
            //       *"78초 남는다"*고 찍혔는데 실제로는 6초였다 —
            //       연료 120이 48초다(초당 2.5씩 준다). 나눠야 초가 된다.
            float bossAt = director.Stage.waveCount * director.Stage.waveSeconds;
            float runSeconds = ship.FuelMax / director.Config.idleFuelPerSecond;
            float budget = runSeconds - bossAt;
            t.AppendLine($"  보스 등장 {bossAt:0.0}초 · 판 길이 {runSeconds:0.0}초 " +
                         $"→ 보스전에 쓸 수 있는 시간 {budget:0.0}초");
            // 🔴 **판정은 "재는 창(40초) 안에 끝냈나"가 아니라 "연료 안에 끝나나"다.**
            //    창을 못 넘겼다는 이유로 🔴를 찍으면, 계산상 되는 것도 안 되는 것처럼 보인다 —
            //    실제로 1구역 천장이 *"86초면 되는데"* 🔴로 찍혀 있었다. 검사가 거짓말한 것이다.
            float takes = clearedAt >= 0f ? clearedAt : need;
            t.AppendLine(takes > 0f && takes <= budget
                ? $"  ✅ 연료로 {budget:0.0}초가 남고 부수는 데 {takes:0.0}초" +
                  (clearedAt >= 0f ? "" : " (재는 창 밖이라 추정)") + " — **깰 수 있다**"
                : $"  🔴 **연료로 {budget:0.0}초가 남는데 부수는 데 {takes:0}초가 든다**");

            director.ReturnNow();
            yield return null;
            director.BackToReady();

            MetaSave.Data.nodes.Clear();
            MetaSave.Data.nodes.AddRange(savedNodes);   // 다음 검사에 새지 않게 되돌린다
            yield return null;
        }

        /// <summary>
        /// 🔴 **각 구역에서 실제로 살 수 있는 노드가 몇 개인가.**
        ///
        ///    재화는 구역 등급으로 잠겨 있다 (`Mats.FirstRank`: 고철 1 · 회로 2 · 코어 3 …).
        ///    그래서 **1구역에서는 고철만 나온다.** 회로가 드는 노드는 전부 못 산다.
        ///
        ///    이건 선행조건 검사로는 절대 안 잡힌다 — `requires`는 전부 이어져 있고
        ///    노드도 108개 다 도달 가능하다. **막는 것은 지갑이지 그래프가 아니다.**
        ///
        ///    🔴 여기가 너무 적으면 **첫 구역이 벽이 된다.** 살 게 없는데
        ///       다음 구역은 보스로 잠겨 있으면 플레이어가 할 수 있는 일이 없어진다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator EachStageHasSomethingToBuy()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 구역마다 살 게 있는가 ===========");

            var tree = director.content.techTree;
            Assert.IsNotNull(tree, "테크트리가 비어 있다");

            int worst = int.MaxValue;
            string worstName = "";

            for (int rank = 1; rank <= Mats.Count; rank++)
            {
                // 이 등급에서 나오는 재화만으로 살 수 있는 노드를 **선행조건까지 따라가며** 센다
                var ok = new HashSet<string>();
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < tree.Length; i++)
                    {
                        var n = tree[i];
                        if (n == null || ok.Contains(n.id)) continue;
                        if (!Affordable(n, rank)) continue;

                        bool reqOk = true;
                        if (n.requires != null)
                            for (int r = 0; r < n.requires.Length; r++)
                                if (!ok.Contains(n.requires[r])) { reqOk = false; break; }

                        if (reqOk) { ok.Add(n.id); changed = true; }
                    }
                }

                // 그 노드들을 1랭크씩 사는 데 드는 고철 (= 그 구역에서 살 수 있는 성장의 크기)
                int scrap = 0;
                for (int i = 0; i < tree.Length; i++)
                    if (ok.Contains(tree[i].id)) scrap += tree[i].costScrap;

                t.AppendLine($"  {rank}구역 (등급 {rank} 재화까지)   {ok.Count,3}/{tree.Length} 노드" +
                             $" · 1랭크 합계 고철 {scrap,6}");

                if (ok.Count < worst) { worst = ok.Count; worstName = $"{rank}구역"; }
            }

            // 🔴 **떨어지기만 하고 쓸 곳이 없는 재화가 있는가.**
            //    재화를 6종으로 늘리면서 **버는 쪽만 만들고 쓰는 쪽을 안 만들면**
            //    깊은 구역의 재화가 영원히 쌓이기만 한다 — 주워도 아무 일이 안 난다.
            //    (8/26~27에 실제로 그랬다: 초합금·냉각결정·동위원소가 쓸 데가 없었다)
            var sink = new HashSet<MatKind>();
            for (int i = 0; i < tree.Length; i++)
                for (int m = 0; m < Mats.Count; m++)
                    if (tree[i].BaseCost((MatKind)m) > 0) sink.Add((MatKind)m);
            var dead = new List<string>();
            for (int m = 0; m < Mats.Count; m++)
                if (!sink.Contains((MatKind)m)) dead.Add(Mats.Name((MatKind)m));

            t.AppendLine();
            t.AppendLine($"  쓸 곳이 있는 재화 {sink.Count}/{Mats.Count}종");
            if (dead.Count > 0)
                t.AppendLine($"  🔴 **떨어지기만 하고 쓸 데가 없는 재화: {string.Join(" · ", dead)}**");

            t.AppendLine();
            t.AppendLine($"  → 제일 좁은 곳: {worstName} · {worst}개");
            t.AppendLine("  ⚠️ 1구역이 좁은 건 의도일 수 있다 — 다만 **다음 구역이 보스로 잠겨 있으므로**");
            t.AppendLine("     보스를 못 깨면 플레이어는 여기서 영원히 멈춘다.");

            Debug.Log("[SMOKE]" + t);

            // 🔴 단언은 **0이 아닌가**만. 몇 개가 적당한지는 밸런스라 여기서 정하지 않는다
            Assert.Greater(worst, 0,
                $"🔴 {worstName}에서 살 수 있는 노드가 하나도 없다 — 그 구역은 벽이다");

            yield return null;
            Assert.Pass();
        }

        /// <summary>이 구역 등급에서 나오는 재화만으로 이 노드를 살 수 있는가.</summary>
        static bool Affordable(TechNodeDef n, int rank)
        {
            // 🔴 **여섯 종류를 다 본다** (2026-08-27). 셋만 보면
            //    초합금 이상이 드는 노드가 **1구역에서도 살 수 있는 것처럼** 세어진다.
            for (int i = 0; i < Mats.Count; i++)
            {
                var m = (MatKind)i;
                if (n.BaseCost(m) > 0 && rank < Mats.FirstRank(m)) return false;
            }
            return true;
        }

        /// <summary>고철 말고는 아무것도 안 드는가 (= 1구역에서 살 수 있는가).</summary>
        static bool ScrapOnly(TechNodeDef n)
        {
            for (int i = 1; i < Mats.Count; i++)
                if (n.BaseCost((MatKind)i) > 0) return false;
            return true;
        }

        /// <summary>밭(로봇·위험물 제외) 중 아무거나 하나. 없으면 null.</summary>
        static JunkPiece NearestFarmJunk(StageField field)
        {
            for (int i = 0; i < field.Pieces.Count; i++)
            {
                var p = field.Pieces[i];
                if (!p.Alive || p.type == null) continue;
                if (p.type.isHazard || p.type.IsRobot || p.type.isAnchor) continue;
                return p;
            }
            return null;
        }

        static string Pad(string v, int n)
        {
            if (string.IsNullOrEmpty(v)) v = "";
            return v.Length >= n ? v.Substring(0, n) : v + new string(' ', n - v.Length);
        }
    }
}
