using System.Collections;
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

        // ==============================================================================
        //  3. 지역 이동 — 끝까지 넘어가지나
        // ==============================================================================

        [UnityTest, Timeout(600000)]
        public IEnumerator TravelThroughAllRegions()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 지역 이동 ===========");

            director.StartRun(0);
            yield return null;

            int guard = 0;
            while (director.MapIndex < director.content.StageCount - 1 && guard++ < 20)
            {
                // 기지 한가운데로 옮기고 연료를 채워 출발 조건을 만든다
                director.ship.transform.position = Vector3.zero;
                director.homeBase.Refuel(999999f);
                yield return null;

                // 🔴 **계류 장치에 붙잡혔으면 끊고 간다** (rev.11).
                //    마지막 구간 직전에 심어지므로, 안 끊으면 여기서 영영 못 떠난다.
                if (director.AnchorsBlocking)
                {
                    var fld = director.field;
                    t.AppendLine($"  계류 {fld.AnchorsAlive}개 — 끊는다");

                    for (int a = 0; a < fld.Anchors.Count; a++)
                    {
                        var anc = fld.Anchors[a];
                        if (anc != null && anc.Alive) anc.Chip(999999f);
                        yield return null;
                    }

                    Assert.IsFalse(director.AnchorsBlocking, "🔴 계류를 다 끊었는데 아직 잡혀 있다");
                }

                // 🔴 **여기서 그냥 break 하면 안 된다.**
                //    2026-08-23에 경고만 남기고 통과시켰더니 4/5에서 멈춘 걸
                //    "통과"로 보고했다 — 진짜로 못 가게 되는 버그도 똑같이 조용히 넘어간다.
                Assert.IsTrue(director.CanTravel,
                    $"🔴 지역 {director.MapIndex}에서 출발할 수 없다 " +
                    $"(연료 {director.homeBase.Fuel:0} / 여비 {director.TravelCost:0} · " +
                    $"계류 {(director.field != null ? director.field.AnchorsAlive : 0)})");

                int before = director.MapIndex;
                director.TravelToNext();
                yield return null;

                // 🔴 **rev.11: 이동이 즉시가 아니라 항행 국면이 됐다.**
                //    출발만 하고 도착은 `LegProgress`가 1이 될 때다 —
                //    검사도 그 시간을 기다려야 한다.
                Assert.IsTrue(director.Travelling, "🔴 출발했는데 항행 국면이 아니다");
                Assert.IsFalse(director.ship.gameObject.activeSelf,
                    "🔴 항행 중인데 우주선이 격납되지 않았다");

                // 🔴 **연료를 계속 채워 준다.** 이 검사는 *이동 구조가 도는가*를 보는 것이지
                //    *연료로 버틸 수 있는가*를 보는 게 아니다.
                //    안 채우면 항행 중 연료가 말라 판이 끝나고,
                //    그러면 "항행이 안 끝났다"로 잘못 보고된다 (2026-08-23에 그랬다).
                //    밸런스는 사장님이 플레이로 판단하실 몫이다.
                int guardLeg = 0;
                while (director.Travelling && director.State == GameState.Field
                       && guardLeg++ < 90 * 30)
                {
                    director.homeBase.Refuel(9999f);
                    yield return null;
                }

                Assert.IsFalse(director.Travelling, "🔴 항행이 끝나지 않았다");
                Assert.IsTrue(director.ship.gameObject.activeSelf,
                    "🔴 도착했는데 우주선이 안 나왔다");

                t.AppendLine($"  {before} -> {director.MapIndex}  {director.Stage.displayName} OK " +
                             $"(항행 {guardLeg / 30f:0}초)");

                for (int f = 0; f < 30; f++) yield return null;   // 새 지역에서 1초 굴려 본다
            }

            t.AppendLine($"최종 지역 도달: {director.MapIndex} / {director.content.StageCount - 1}");

            // 🔴 끝까지 갔는지 **단언한다.** 중간에 멈춘 걸 통과로 넘기지 않는다
            Assert.AreEqual(director.content.StageCount - 1, director.MapIndex,
                "🔴 최종 지역까지 못 갔다 — 이기는 길이 막혀 있다는 뜻이다");
            director.ReturnNow();
            yield return null;

            Debug.Log("[SMOKE]" + t);
            Assert.Pass();
        }

        // ==============================================================================
        //  4. rev.10의 규칙들 — 실제로 그렇게 동작하나
        // ==============================================================================

        /// <summary>
        /// 🔴 **기지 연료가 마르면 진다.** rev.10의 유일한 패배 조건인데
        ///    지금까지 한 번도 확인된 적이 없다 — 코드만 읽고 "그럴 것"이라고 믿고 있었다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BaseDrainsAndLoses()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 기지 연료 고갈 = 패배 ===========");

            float keep = Tuning.BaseDrainMul;
            Tuning.BaseDrainMul = 200f;      // 몇 초 만에 마르게 한다

            director.StartRun(0);
            yield return null;

            float first = director.homeBase.Fuel;
            t.AppendLine($"  시작 연료 {first:0}");

            int f = 0;
            while (director.State == GameState.Field && f++ < 3000) yield return null;

            t.AppendLine($"  {f}프레임 뒤 · 연료 {director.homeBase.Fuel:0} · 상태 {director.State}");

            Tuning.BaseDrainMul = keep;

            Assert.Less(director.homeBase.Fuel, first, "🔴 기지 연료가 줄지 않았다 — 감소 자체가 안 돈다");
            Assert.IsTrue(director.homeBase.Destroyed, "🔴 연료가 0이 됐는데 Destroyed가 아니다");
            Assert.AreEqual(GameState.Result, director.State,
                "🔴 기지 연료가 말랐는데 판이 안 끝났다 — **패배 조건이 없는 게임이 된다**");

            t.AppendLine("  연료 고갈 → 패배 OK");
            Debug.Log("[SMOKE]" + t);
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **배리어는 한 대를 막고, 없으면 다음 한 대가 끝이다.**
        ///    (2026-08-21 요청) 화면에서 상태로 읽히는 게 목적이므로
        ///    "정확히 한 대"가 아니면 설계가 통째로 무너진다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator BarrierTakesOneHitThenRegenerates()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 배리어 ===========");

            director.StartRun(0);
            yield return null;

            // 🔴 **판을 비운다.** 배리어 재생을 재는 동안 쓰레기가 배를 때리면
            //    배리어가 다시 깨지거나 배가 격침돼(= 꺼져서) 재생 자체가 안 돈다.
            //    2026-08-22에 쓰레기 밀도를 올리자 이 검사가 그렇게 깨졌다 —
            //    **게임이 아니라 검사 환경이 바뀐 것이었다.**
            var field0 = director.field;
            field0.Spawning = false;
            for (int i = 0; i < field0.Pieces.Count; i++)
                if (field0.Pieces[i].Alive) field0.BreakJunkSilently(field0.Pieces[i]);
            yield return null;

            var ship = director.ship;
            Assert.IsTrue(ship.BarrierUp, "🔴 판 시작인데 배리어가 없다");

            Assert.IsTrue(ship.AbsorbHit(), "🔴 첫 대를 배리어가 못 막았다");
            Assert.IsFalse(ship.BarrierUp, "🔴 막은 뒤에도 배리어가 남아 있다");
            t.AppendLine($"  1대째 막음 · 재생까지 {ship.BarrierLeft:0.0}초");

            Assert.IsFalse(ship.AbsorbHit(), "🔴 배리어가 없는데 두 번째도 막았다 — 무적이 된다");
            t.AppendLine("  2대째는 못 막음 (= 격침) OK");

            // 재생을 기다린다. captureDeltaTime이 1/30이므로 프레임으로 센다
            int need = Mathf.CeilToInt(ship.BarrierSeconds / StepSeconds) + 10;
            for (int i = 0; i < need && !ship.BarrierUp; i++) yield return null;

            Assert.IsTrue(ship.BarrierUp, $"🔴 {ship.BarrierSeconds}초가 지나도 배리어가 안 돌아왔다");
            t.AppendLine($"  {ship.BarrierSeconds:0}초 뒤 재생 OK");

            Debug.Log("[SMOKE]" + t);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **격침당해도 화물은 되찾을 수 있다.**
        ///    전부 잃으면 "많이 싣는다"가 선택지에서 빠지고,
        ///    그러면 무게 저울질이라는 이 게임의 세 번째 결정이 죽는다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator WreckSpillIsRecoverable()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 격침 화물 산개 ===========");

            director.StartRun(0);
            yield return null;

            var field = director.field;

            int before = 0;
            for (int i = 0; i < field.Fragments.Count; i++) if (field.Fragments[i].Alive) before++;

            int made = field.SpillCargo(new Vector2(20f, 0f), 30, 5);
            yield return null;

            int after = 0;
            for (int i = 0; i < field.Fragments.Count; i++) if (field.Fragments[i].Alive) after++;

            t.AppendLine($"  산개 요청 30개 · 실제 {made}개 · 살아있는 파편 {before} → {after}");

            Assert.Greater(made, 0, "🔴 격침 화물이 하나도 안 떨어졌다 — 되찾을 수 없다");
            Assert.Greater(after, before, "🔴 파편이 실제로 늘지 않았다");

            // 🔴 수명이 충분한가 — 부활(5초) + 돌아가는 시간을 버텨야 한다
            for (int i = 0; i < 300; i++) yield return null;   // 10초

            int stillAlive = 0;
            for (int i = 0; i < field.Fragments.Count; i++) if (field.Fragments[i].Alive) stillAlive++;

            t.AppendLine($"  10초 뒤에도 살아있는 파편 {stillAlive}");
            Assert.Greater(stillAlive, 0,
                "🔴 10초 만에 다 사라졌다 — 부활하고 가면 이미 없다는 뜻이다");

            Debug.Log("[SMOKE]" + t);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>
        /// 🔴 **이 게임의 심장 — 주워서 → 가져와서 → 기지가 산다.**
        ///
        ///    rev.9에서 *"안 주우면 진다"*를 만든 게 이 게임을 뱀서에서 떼어낸 지점인데,
        ///    그 경로가 **한 번도 검증된 적이 없다.** 중간 어디가 끊겨도
        ///    화면상으로는 "그냥 어렵네"로 보여서 알아채기 어렵다.
        ///
        ///    기지 감소를 끄고 잰다 — 회복이 실제로 들어오는지만 보려는 것이므로
        ///    감소가 섞이면 순증가인지 아닌지 구분이 안 된다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator DepositRefuelsTheBase()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 주움 → 입금 → 기지 회복 ===========");

            float keepDrain = Tuning.BaseDrainMul;
            Tuning.BaseDrainMul = 0f;

            director.StartRun(0);
            yield return null;

            var ship = director.ship;
            var field = director.field;

            // 🔴 **기지를 미리 깎아 둔다.** 안 그러면 이미 만땅(1000/1000)이라
            //    회복이 들어와도 상한에 막혀 값이 안 변한다 —
            //    2026-08-22 첫 실행이 정확히 그렇게 실패했다.
            //    **게임 버그가 아니라 테스트 설계 실수였다.**
            director.homeBase.Spend(director.homeBase.FuelMax * 0.5f);
            yield return null;

            // 1) 기지에서 멀리 나가 파편 위에 선다
            var spot = new Vector2(30f, 0f);
            ship.transform.position = spot;
            yield return null;

            int made = field.SpillCargo(spot, 40, 6);
            t.AppendLine($"  파편 {made}개 뿌림");

            // 2) 자석이 빨아들일 시간을 준다
            for (int i = 0; i < 150 && director.CargoCount < 5; i++) yield return null;
            t.AppendLine($"  주운 화물 {director.CargoCount}개");

            Assert.Greater(director.CargoCount, 0,
                "🔴 파편 위에 서 있는데 하나도 안 주웠다 — 자석/흡입이 끊겼다");

            // 3) 기지로 돌아간다
            int cargoBefore = director.CargoCount;
            float fuelBefore = director.homeBase.Fuel;

            ship.transform.position = Vector2.zero;
            yield return null;

            Assert.IsTrue(director.AtBase, "🔴 기지 한가운데인데 도킹으로 안 잡힌다");

            // 4) 입금이 끝날 때까지
            for (int i = 0; i < 600 && director.CargoCount > 0; i++) yield return null;

            float fuelAfter = director.homeBase.Fuel;
            t.AppendLine($"  입금 {director.DepositedTotal}개 · 기지 연료 {fuelBefore:0} → {fuelAfter:0}");

            Tuning.BaseDrainMul = keepDrain;

            Assert.AreEqual(0, director.CargoCount,
                "🔴 기지에 있는데 화물이 안 빠진다 — 입금 교착이 다시 생겼다");
            Assert.Greater(director.DepositedTotal, 0, "🔴 입금이 기록되지 않았다");
            Assert.Greater(fuelAfter, fuelBefore,
                "🔴 입금했는데 기지 연료가 안 늘었다 — **안 주워도 되는 게임이 된다**");

            t.AppendLine("  주움 → 입금 → 회복 OK");
            Debug.Log("[SMOKE]" + t);

            director.ReturnNow();
            yield return null;
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
                    if (director.State == GameState.Drafting) director.ChooseCard(0);

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

        /// <summary>
        /// 🔴 **견인** (rev.11) — 파편이 배 뒤에 붙고, 버리면 그 자리에 남는가.
        ///
        ///    화물칸(숫자)에서 견인(물건)으로 바뀌면서 **줄 관리**가 생겼다.
        ///    중간 것이 사라지면 뒤가 허공을 따라가는데, 그건 화면을 봐야만 보인다 —
        ///    그래서 개수로라도 확인한다.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TowingPicksUpAndDrops()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 스모크: 견인 ===========");

            float keepDrain = Tuning.BaseDrainMul;
            Tuning.BaseDrainMul = 0f;

            director.StartRun(0);
            yield return null;

            var ship = director.ship;
            var field = director.field;

            var spot = new Vector2(30f, 0f);
            ship.transform.position = spot;
            yield return null;

            field.SpillCargo(spot, 30, 6);
            for (int i = 0; i < 150 && director.TowedCount < 5; i++) yield return null;

            t.AppendLine($"  끌고 있는 것 {director.TowedCount}개 · 무게 배수 {director.CargoWeightMul:0.00}");
            Assert.Greater(director.TowedCount, 0, "🔴 파편 위에 서 있는데 하나도 안 달렸다");
            Assert.Less(director.CargoWeightMul, 1f, "🔴 끌고 있는데 무거워지지 않았다");

            int had = director.TowedCount;
            director.JettisonTow();
            yield return null;

            Assert.AreEqual(0, director.TowedCount, "🔴 버렸는데 아직 달려 있다");

            int stillThere = 0;
            for (int i = 0; i < field.Fragments.Count; i++)
                if (field.Fragments[i].Alive) stillThere++;

            t.AppendLine($"  {had}개 버림 → 그 자리에 남은 파편 {stillThere}개");
            Assert.Greater(stillThere, 0,
                "🔴 버린 것이 사라졌다 — 그러면 결정이 아니라 손실이다");

            Tuning.BaseDrainMul = keepDrain;

            Debug.Log("[SMOKE]" + t);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        // ==============================================================================
        //  5. 진단 — 밸런스를 **재서 알려준다** (판단은 사장님이)
        // ==============================================================================

        /// <summary>
        /// 🔴 **항행 한 구간의 연료 수지를 잰다.**
        ///
        ///    2026-08-23에 *"첫 항행을 연료가 못 버틴다"*고 보고했지만 **숫자가 없었다.**
        ///    숫자 없는 보고는 사장님이 판단할 근거가 못 된다 —
        ///    그렇다고 내가 수치를 고치면 그건 또 추측이다.
        ///
        ///    그래서 **재기만 한다.** 이 검사는 밸런스를 단언하지 않는다.
        ///    "무방비로 한 구간 가면 얼마 잃는가"와
        ///    "그걸 메우려면 몇 개를 캐야 하는가"를 표로 뽑아 준다.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator VoyageFuelBudget()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 진단: 항행 연료 수지 ===========");
            t.AppendLine("무방비(포탑 1레벨·조준 없음)로 한 구간을 갈 때 얼마를 잃는가");
            t.AppendLine();
            t.AppendLine("지역             포탑Lv   여비   항행손실   합계   필요 파편수");
            t.AppendLine("---------------- ------ ----- -------- ------ ----------");

            var cfg = director.config;
            int stages = director.content.StageCount;

            // 🔴 **포탑 레벨을 바꿔 가며 잰다.**
            //    무방비 수치만 보면 "못 가는 게임"으로 읽히지만,
            //    이 게임은 **강화해서 버티는 게 설계**다.
            //    그러니 *"강화하면 실제로 버텨지는가"*까지 재야 판단이 된다.
            int[] levels = { 1, 5, 10 };

            for (int m = 0; m < stages - 1; m++)
            foreach (int turret in levels)
            {
                director.StartRun(m);
                yield return null;

                director.Stats.baseTurretLevel = turret;

                var hb = director.homeBase;
                var stage = director.Stage;

                // 출발 조건을 만든다 (연료 가득 · 기지 안 · 계류 없음)
                hb.Refuel(999999f);
                director.ship.transform.position = Vector3.zero;
                yield return null;

                float before = hb.Fuel;
                float toll = director.TravelCost;

                if (!director.CanTravel)
                {
                    t.AppendLine($"{Pad(stage.displayName, 16)} {turret,6} — 출발 불가 (계류 등)");
                    director.ReturnNow(); yield return null;
                    director.BackToReady(); yield return null;
                    continue;
                }

                director.TravelToNext();
                yield return null;

                // 🔴 **아무것도 안 하고** 한 구간을 간다 — 이게 최악의 경우다
                int guard = 0;
                while (director.Travelling && director.State == GameState.Field && guard++ < 90 * 30)
                    yield return null;

                float after = hb.Fuel;
                float lost = before - after;
                float travelLoss = Mathf.Max(0f, lost - toll);

                // 파편 하나가 주는 연료 (만재 보너스 없이)
                float perFrag = cfg.fuelPerCargo * Tuning.FuelPerCargoMul;
                int needed = perFrag > 0.01f ? Mathf.CeilToInt(lost / perFrag) : -1;

                bool died = hb.Fuel <= 0.5f;
                t.AppendLine($"{Pad(stage.displayName, 16)} {turret,6} {toll,5:0} {travelLoss,8:0} " +
                             $"{lost,6:0} {needed,10}" + (died ? "   ← 표류" : ""));

                director.ReturnNow(); yield return null;
                director.BackToReady(); yield return null;
            }

            t.AppendLine();
            t.AppendLine($"파편 1개 = 연료 {cfg.fuelPerCargo:0.0}  ·  기지 연료 최대 {cfg.baseFuelMax:0}");
            t.AppendLine("🔴 '← 표류'는 그 설정으로는 **한 구간도 못 간다**는 뜻이다.");
            t.AppendLine("🔴 포탑 레벨을 올렸을 때 손실이 확 줄면 → **강화가 답인 설계**로 정상.");
            t.AppendLine("   레벨을 올려도 안 줄면 → 포탑 화력이나 충돌 손실을 손봐야 한다.");
            t.AppendLine("   조절은 K 패널: 포탑 화력 · 충돌 손실 · 항행 길이 · 잔해 양");

            Debug.Log("[진단]" + t);
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
                if (director.State == GameState.Drafting) director.ChooseCard(0);

                AutoPilot.Drive(director, ship);
                yield return null;
            }

            AutoPilot.Release(ship);
            director.ReturnNow();
            yield return null;
            director.BackToReady();
            yield return null;
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
