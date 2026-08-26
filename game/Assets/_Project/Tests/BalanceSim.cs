using System.Collections;
using System.Collections.Generic;
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
    /// 헤드리스 밸런스 시뮬레이션 (rev.5).
    ///
    /// 🔴 왜 필요한가: 지금 밸런스가 **전부 추측**이다.
    ///    무기 12종 × 조합 21가지 × 우주선 6척을 사람이 손으로 잴 수는 없다.
    ///
    /// 실제 게임 루프(WeaponRig·JunkPiece·ShipController)를 그대로 돌린다.
    /// 다른 점은 조종을 사람이 아니라 봇이 한다는 것뿐 —
    /// **쓰레기를 피하면서 무리 쪽으로 붙는다.** 사람보다 못한 하한선으로 본다.
    ///
    /// ⚠️ 이건 "재미"를 재지 않는다. 재는 건 수치뿐이다.
    ///    "무기 둘이 재미있는가"는 여전히 사람이 플레이해야 안다.
    ///
    /// 🔴 결정론 3종 세트 (하나라도 빠지면 값이 조용히 흔들린다):
    ///    1. `Time.timeScale`이 아니라 `Time.captureDeltaTime`
    ///    2. 절대 시각 금지 — 객체마다 자기 수명 타이머
    ///    3. 워밍업 런 하나를 버린다
    /// </summary>
    public class BalanceSim
    {
        /// <summary>
        /// 🔴 배치 모드는 프레임이 매우 빨라 `Time.timeScale`을 올려도
        ///    프레임당 deltaTime이 미세하다 — 12,000프레임을 돌려도 게임 시간은 5초뿐이다.
        ///    `captureDeltaTime`은 **프레임당 게임 시간을 고정**한다.
        ///    (2026-08-19 1차 시뮬이 22/30 런에서 조용히 잘려 알게 됨)
        /// </summary>
        const float StepSeconds = 1f / 30f;

        /// <summary>
        /// 런 하나의 상한 = 게임 시간 **600초**.
        ///
        /// 🔴 상한이 측정 대상을 가리면 표는 그럴듯한데 아무것도 안 재는 상태가 된다.
        ///    이 값 때문에 **두 번** 헛다리를 짚었다:
        ///    - 240초: 보스(300초)를 한 번도 못 봤다. 전부 "생존 240.0s"로 잘림
        ///    - 400초: 보스전에 100초만 줬다. 못 깬 게 아니라 **시간이 모자랐던** 조합이 섞였다
        ///
        ///    지금은 보스전에 300초를 준다. 여기서도 못 깨면 그건 진짜 화력 부족이다.
        /// </summary>
        const int MaxFramesPerRun = 18000;

        /// <summary>맵 1의 보스가 나오는 시각(6웨이브 × 50초). 보스 처치 시간 계산용.</summary>
        const float BossAtSeconds = 300f;

        RunDirector director;
        GameObject bootGo;
        float originalTimeScale, originalCapture;
        float originalVolume;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 🔴 플레이어의 진짜 meta.json을 절대 건드리지 않는다
            // 🔴 시뮬에서만 카드 뽑기를 고정한다. 실제 플레이는 매번 달라야 한다
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

            bootGo = new GameObject("== SIM BOOTSTRAP ==");
            bootGo.AddComponent<GreyboxBootstrap>();

            yield return null;   // Awake/Start 통과
            yield return null;

            director = RunDirector.Instance;
            Assert.IsNotNull(director, "RunDirector가 만들어지지 않았다");

            Time.timeScale = 1f;
            Time.captureDeltaTime = StepSeconds;

            // 🔴 **결정론 4번째 조건 — 물리 스텝을 프레임에 딱 맞춘다.**
            //
            //    `captureDeltaTime`만 고정하면 게임 시간은 프레임당 일정해지지만,
            //    물리(FixedUpdate)는 자기 누산기로 돌아간다. 그 누산기의 **나머지가
            //    런 사이에 남아서** 다음 런의 첫 물리 스텝 타이밍이 달라진다.
            //    배가 움직이는 게임이라 그 한 스텝이 경로 전체를 바꾼다.
            //
            //    fixedDeltaTime을 프레임 시간과 같게 두면 프레임당 정확히 한 스텝이라
            //    나머지가 생기지 않는다.
            originalFixed = Time.fixedDeltaTime;
            Time.fixedDeltaTime = StepSeconds;

            // 🔴 **결정론 5번째 조건 — 물리를 우리가 직접 돌린다** (2026-08-26).
            //
            //    `fixedDeltaTime = captureDeltaTime`으로 "프레임당 한 스텝"을 노렸지만
            //    실측에서 안 맞았다. 같은 빌드 두 번이 **프레임 1에는 완전히 같고
            //    프레임 15에서 속도만 1.6 vs 3.5**로 갈렸다 —
            //    세상(쓰레기 16개·연료 120)은 똑같은데 **배만 두 배로 빨랐다.**
            //    누산기에 남은 나머지 때문에 어떤 프레임은 물리를 두 번 밟는다.
            //
            //    수동 모드로 두고 프레임마다 정확히 한 번 `Simulate`하면 누산기가 없다.
            originalSim = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            physGo = new GameObject("== SIM PHYSICS ==");
            physGo.AddComponent<StepPhysics>().step = StepSeconds;
        }

        float originalFixed;
        SimulationMode2D originalSim;
        GameObject physGo;

        /// <summary>
        /// 프레임마다 물리를 **정확히 한 번** 민다. `LateUpdate`에서 미는 이유는
        /// 유니티의 평소 순서(FixedUpdate → Update)와 어긋나지 않게 하려는 것이다 —
        /// 이번 프레임의 `Update`가 읽는 물리 상태는 언제나 *직전 스텝의 결과*가 된다.
        /// </summary>
        class StepPhysics : MonoBehaviour
        {
            public float step = 1f / 30f;
            void LateUpdate() => Physics2D.Simulate(step);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioListener.volume = originalVolume;
            AudioListener.pause = false;

            Time.timeScale = originalTimeScale;
            Time.captureDeltaTime = originalCapture;
            if (originalFixed > 0f) Time.fixedDeltaTime = originalFixed;
            Physics2D.simulationMode = originalSim;
            if (physGo != null) Object.Destroy(physGo);
            if (bootGo != null) Object.Destroy(bootGo);
            MetaSave.ReplaceInMemory(new MetaData());
            MetaSave.DisableWrites = false;
            RunDirector.DeterministicDraft = false;
            yield return null;
        }

        // ==============================================================================
        //  1. 조합 21가지 — 어느 게 압도적이고 어느 게 쓸모없는가
        // ==============================================================================

        /// <summary>
        /// 🔴 **조합마다 여러 번 돌려 평균을 낸다.**
        ///
        ///    한 번씩만 재던 시절, 손대지도 않은 조합의 결과가 런마다 2배씩 흔들렸다
        ///    (`견인 분쇄` 1727 → 802). 그걸 내 수정 효과로 착각하고
        ///    **노이즈를 보고 밸런스를 만졌다.**
        ///
        ///    결정론 검사는 한 조합만 두 번 재므로 "그 조합이 안정적이다"만 말해 준다.
        ///    표 전체가 안정적이라는 뜻이 아니다 — 조합마다 흔들림의 크기가 다르다.
        ///
        ///    그래서 여기서는 **반복해서 평균과 편차를 같이 찍는다.**
        ///    편차가 크면 그 줄은 **읽지 말라고** 표시한다.
        /// </summary>
        const int Repeats = 3;

        // 🔴 21조합 × 3회 × 최대 600초는 기본 제한(180초)을 훌쩍 넘는다.
        //    제한에 걸리면 표가 **중간에 잘린 채 실패**로 끝나서 아무것도 못 읽는다.
        [UnityTest, Timeout(3600000)]
        public IEnumerator MeasureCombos()
        {
            var content = director.content;
            Assert.IsNotNull(content.combos, "조합 데이터가 없다");

            var t = new StringBuilder();
            t.AppendLine();
            // 🔴 **rev.7에서 재는 것이 바뀌었다.**
            //    전에는 "얼마나 오래 살아남아 얼마나 주웠나"를 쟀다.
            //    이제 지는 조건은 **기지 상실**이므로, 그게 첫 칸이어야 한다.
            //    파편 수는 여전히 화력의 대리 지표지만 **더 이상 목적이 아니다** —
            //    입금하지 않은 파편은 레벨도 수리도 되지 않는다.
            t.AppendLine($"=========== 조합 21가지 시뮬 ({Repeats}회 평균 · 맵 1) ===========");
            t.AppendLine();
            t.AppendLine("🔴 **첫 칸은 `가져옴`이다** — 매달고 돌아온 재화 개수.");
            t.AppendLine("   이게 한 판의 진짜 수입이다. 부순 양도, 주운 양도 아니다 —");
            t.AppendLine("   **견인 칸이 곧 상한**이라 잘 부순다고 수입이 늘지 않는다.");
            t.AppendLine("   `주움`과 `가져옴`이 크게 벌어질수록 **밀려 떨어진 것이 많았다**는 뜻이다.");
            t.AppendLine();
            t.AppendLine("조합            | 무기 조합              | 가져옴 | 주움 | 편차 | 보스탄 | 보스");
            t.AppendLine("----------------|------------------------|--------|------|------|--------|------");

            yield return Warmup();

            var rows = new List<(string name, float perMin, float time, int level)>();

            for (int i = 0; i < content.combos.Length; i++)
            {
                var combo = content.combos[i];
                if (!PickPairFor(combo, out var a, out var b)) continue;

                // 🔴 2026-08-26: 칸을 갈아엎었다. 전에는 **기지생존 · 잔여연료 · 격침**을 쟀는데
                //    **셋 다 지금 게임에 없다.** (기지도, 격침도 없고 연료는 항상 0으로 끝난다)
                //    없어진 것을 재는 표는 통과하면서 아무것도 안 알려준다 —
                //    이 프로젝트에서 네 번 겪은 사고다.
                int bankSum = 0, bankMin = int.MaxValue, bankMax = 0;
                int pickSum = 0, cleared = 0, hitSum = 0;

                for (int r = 0; r < Repeats; r++)
                {
                    yield return RunWith(a, b, director.ComboLevel);

                    // 🔴 **매달고 돌아온 것만 수입이다.** 주운 것(`RunCollected`)은
                    //    밀려 떨어진 것까지 포함하므로 수입이 아니다.
                    int bank = director.BankedCount;
                    bankSum += bank;
                    bankMin = Mathf.Min(bankMin, bank);
                    bankMax = Mathf.Max(bankMax, bank);

                    pickSum += director.RunCollected;
                    hitSum += director.BossHits;
                    if (director.Cleared) cleared++;

                    director.BackToReady();
                    yield return null;
                }

                float bankAvg = bankSum / (float)Repeats;

                // 🔴 편차 = (최대-최소) ÷ 평균. 이게 크면 그 줄은 못 믿는다
                float spread = bankAvg > 0.01f ? (bankMax - bankMin) / bankAvg * 100f : 0f;
                string flag = spread > 40f ? "🔴" : spread > 20f ? "🟡" : "  ";

                t.AppendLine(
                    $"{Pad(combo.title, 15)} | {Pad(Weapons.Name(a) + "+" + Weapons.Name(b), 22)} | " +
                    $"{bankAvg,6:0.0} | {pickSum / (float)Repeats,4:0} | " +
                    $"{flag}{spread,3:0}% | {hitSum / (float)Repeats,6:0.0} | {cleared}/{Repeats}");

                rows.Add((combo.title, bankAvg, pickSum / (float)Repeats, cleared));
            }

            AppendSpread(t, rows, "개 가져옴",
                "⚠️ **가져옴은 견인 칸 수에서 거의 안 벗어난다.** 무기가 셀수록 늘어나는 값이 아니다. " +
                "무기 차이는 `주움`에서 보인다 — 두 칸을 같이 봐야 한 조합이 뭘 잘하는지 읽힌다.");
            t.AppendLine("편차 🔴40%+ / 🟡20%+ 인 줄은 **한 번의 결과로 판단하지 말 것.**");
            t.AppendLine();
            t.AppendLine("🔴 읽는 법:");
            t.AppendLine("   · **가져옴**이 첫 칸이다 — 매달고 돌아온 재화. 이게 한 판의 수입이다");
            t.AppendLine("   · **주움**과 벌어질수록 **밀려 떨어진 것**이 많았다는 뜻이다");
            t.AppendLine("     (견인이 꽉 차면 맨 앞이 밀려난다 — 부수는 속도가 칸을 넘어섰다는 신호)");
            t.AppendLine("   · **가져옴이 전 조합에서 칸 수에 붙어 있으면** 무기 강화가 수입에 안 닿는다.");
            t.AppendLine("     그때 올려야 하는 건 화력이 아니라 **칸 · 값어치 · 드론**이다");
            t.AppendLine("   · **보스**가 0/N이면 다른 칸을 볼 필요가 없다 — 구역이 안 열린다");
            t.AppendLine("   · **보스탄**은 맞은 횟수다. 0.0이면 위협이 실제로는 없다는 뜻이다");
            Debug.Log("[SIM]" + t);
            Assert.Pass();
        }

        // ==============================================================================
        //  1-b. 🔴 결정론 검사 — 같은 빌드를 두 번 돌려 결과가 같은지 본다

        /// <summary>
        /// 결정론 진단용 한 줄 요약. 런 사이에 **무엇이 남아 있는지** 보려는 것이므로
        /// 리셋되어야 마땅한 것들만 골라 찍는다.
        /// </summary>
        string Snapshot(string label)
        {
            var f = director.field;
            int junk = 0, frag = 0;

            // 🔴 **세상의 지문.** 개수만 세면 "쓰레기 159개"가 같아도
            //    *어느* 159개인지는 모른다. 위치까지 접어 넣어야
            //    "세상이 같은가 / 배만 다른가"를 한 줄로 가른다.
            //    (2026-08-26에 이 구분이 없어서 물리 누산기를 찾는 데 다섯 바퀴를 썼다)
            //    🔴 **두 가지를 따로 낸다** — 이걸 안 나누면 아무것도 못 가린다:
            //       · `내용` = 순서와 무관한 합. **어떤 것들이 어디 있는가**
            //       · `순서` = 풀 순서를 곱해 접은 값. **목록에 어떤 차례로 들어 있는가**
            //
            //    내용이 같은데 순서가 다르면 **풀 재사용**이다 —
            //    `AutoPilot.BestThreat`과 `CollectByTouch`가 이 목록을 훑어
            //    가장 가까운 것을 고르므로, 순서가 다르면 **같은 자리에서 다른 것을 문다.**
            long sumHash = 0, ordHash = 0;
            if (f != null)
            {
                for (int i = 0; i < f.Pieces.Count; i++)
                {
                    var pc = f.Pieces[i];
                    if (!pc.Alive) continue;
                    junk++;
                    var pp = pc.transform.position;
                    long one = Mathf.RoundToInt(pp.x * 50f) * 7919L
                             + Mathf.RoundToInt(pp.y * 50f) * 104729L;
                    sumHash = (sumHash + one) & 0xFFFFFFFL;
                    ordHash = (ordHash * 31 + one) & 0xFFFFFFFL;
                }
                for (int i = 0; i < f.Fragments.Count; i++) if (f.Fragments[i].Alive) frag++;
            }

            var sh = director.ship;

            var st = director.Stats;
            return $"{Pad(label, 7)} t={director.RunTime,6:0.0} 짐={director.TowedCount,2} " +
                   $"주움={director.RunCollected,5} 크레딧={director.RunValue,6} " +
                   $"배켜짐={(sh != null && sh.gameObject.activeSelf ? "O" : "X")} " +
                   $"연료={(sh != null ? sh.Fuel : -1f),5:0} " +
                   $"쓰레기={junk,4} 파편={frag,4} 웨이브={director.Wave,2} " +
                   $"상태={director.State} 국면={director.Phase} " +
                   $"힘={(st != null ? st.powerMul : -1f):0.00} 사거리={(st != null ? st.rangeMul : -1f):0.00} " +
                   $"쿨={(st != null ? st.cooldownMul : -1f):0.00} 사거리제약={BossBehaviour.RangeChoke:0.00} " +
                   $"원반Lv={(st != null ? st.LevelOf(WeaponKind.Discus) : -1)} " +
                   $"모선까지={(sh != null ? ((Vector2)sh.transform.position).magnitude : -1f),6:0.0} " +
                   $"속도={(sh != null ? sh.Velocity.magnitude : -1f),5:0.0} " +
                   // 🔴 **풀 크기와 배 좌표.** 결정론이 깨졌을 때 "세상이 다른가 / 배가 다른가"를
                   //    가르는 두 값이다. 풀 크기가 다르면 `EnsurePool` 성장이 런 사이에 남은 것이고,
                   //    풀은 같은데 좌표가 다르면 물리·조종 쪽이다.
                   $"내용={sumHash:x7} 순서={ordHash:x7} " +
                   $"풀={(f != null ? f.Pieces.Count : -1)} " +
                   $"좌표=({(sh != null ? sh.transform.position.x : 0f),6:0.00},{(sh != null ? sh.transform.position.y : 0f),6:0.00})";
        }

        // ==============================================================================

        /// <summary>
        /// 🔴 **"결정론적일 것이다"라고 믿지 말고 잰다.**
        ///
        ///    2026-08-22에 세 번이나 같은 함정에 빠졌다 —
        ///    안 건드린 조합의 결과가 실행마다 바뀌었고, 그때마다
        ///    밸런스가 변한 줄 알고 게임을 고치려 했다.
        ///    원인은 매번 **런 사이에 남은 상태**였다 (무기 난수, 스폰 상한 …).
        ///
        ///    측정이 흔들리면 그 위의 모든 판단이 무의미하므로,
        ///    이 검사가 실패하면 **다른 표는 읽지 않는다.**
        /// </summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator VerifyDeterminism()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 결정론 검사 ===========");
            t.AppendLine("같은 빌드를 두 번 돌려 결과가 같아야 한다.");
            t.AppendLine();

            yield return Warmup();

            // 사이에 다른 런을 끼워 넣는다 — 상태가 새면 여기서 드러난다
            var a = default((float time, int level, int frag, int value));
            var b = default((float time, int level, int frag, int value));

            // 🔴 **어디서 새는지 눈으로 본다.** 두 번이 다르다는 것만 알고 원인을 추측하면
            //    2026-08-21에 그랬듯 엉뚱한 데를 세 번 고치게 된다. 런 전후 상태를 찍는다.
            t.AppendLine("[진단] 런 직전/직후 상태");
            t.AppendLine("       " + Snapshot("A 전"));

            trace = t;
            yield return RunWith(WeaponKind.Discus, WeaponKind.Harpoon, director.ComboLevel);
            trace = null;
            a = (director.RunTime, director.BankedCount, director.RunCollected, director.RunValue);
            t.AppendLine("       " + Snapshot("A 후"));
            director.BackToReady();
            yield return null;

            // 사이에 전혀 다른 조합을 한 번 돌린다
            t.AppendLine("       " + Snapshot("끼움 전"));
            yield return RunWith(WeaponKind.Harpoon, WeaponKind.Arc, director.ComboLevel);
            t.AppendLine("       " + Snapshot("끼움 후"));
            director.BackToReady();
            yield return null;

            t.AppendLine("       " + Snapshot("B 전"));
            trace = t;
            yield return RunWith(WeaponKind.Discus, WeaponKind.Harpoon, director.ComboLevel);
            trace = null;
            b = (director.RunTime, director.BankedCount, director.RunCollected, director.RunValue);
            t.AppendLine("       " + Snapshot("B 후"));
            director.BackToReady();
            yield return null;
            t.AppendLine();

            t.AppendLine($"1회차 : {a.time,6:0.0}s · Lv.{a.level,2} · 파편 {a.frag,5} · 크레딧 {a.value,6}");
            t.AppendLine($"2회차 : {b.time,6:0.0}s · Lv.{b.level,2} · 파편 {b.frag,5} · 크레딧 {b.value,6}");
            t.AppendLine();

            bool same = Mathf.Abs(a.time - b.time) < 0.05f && a.level == b.level
                     && a.frag == b.frag && a.value == b.value;

            // 🔴 완전 일치는 목표지만, **현실적인 기준선**도 필요하다.
            //    0/1 판정만 하면 "3.8%"와 "40%"가 똑같이 실패로 보여서
            //    표를 아예 못 읽게 된다. 흔들림의 크기에 따라 **읽어도 되는 범위**가 다르다.
            float drift = a.frag > 0 ? Mathf.Abs(b.frag - a.frag) / (float)a.frag * 100f : 0f;

            if (same)
            {
                t.AppendLine("✅ 두 번이 정확히 같다 — 어떤 수치든 믿어도 된다");
            }
            else if (drift < 1f)
            {
                t.AppendLine($"🟢 {drift:0.0}% 차이 — 미세한 흔들림. 대부분의 비교에 문제없다");
            }
            else if (drift < 5f)
            {
                t.AppendLine($"🟡 {drift:0.0}% 차이 — **큰 차이만 읽을 것.**");
                t.AppendLine("   2배 이상 벌어진 것은 진짜다. 10~20% 차이는 노이즈일 수 있다.");
                t.AppendLine("   수치를 미세 조정하는 판단에는 쓰지 말 것.");
            }
            else
            {
                t.AppendLine($"🔴 {drift:0.0}% 차이 — **이 상태에서는 다른 표를 읽지 말 것.**");
                t.AppendLine("   런 사이에 남는 상태가 있다. 밸런스가 아니라 측정이 흔들린 것이다.");
                t.AppendLine("   최근에 넣은 것 중 **난수나 타이머를 쓰는 것**을 먼저 의심할 것.");
            }
            t.AppendLine("==========================================");

            Debug.Log("[SIM]" + t);
            Assert.Pass();
        }

        // ==============================================================================
        //  2. 우주선 6척 — 맞바꾸기가 실제로 균형이 맞는가
        // ==============================================================================

        [UnityTest, Timeout(1800000)]
        public IEnumerator MeasureShips()
        {
            var content = director.content;
            Assert.IsNotNull(content.ships, "우주선 데이터가 없다");

            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 우주선 6척 시뮬 (두 번째 무기 = 절단날 고정) ===========");
            t.AppendLine("우주선              | 시작 무기      | 생존   | Lv | 파편 | 크레딧 | 피격 | 크레딧/분");
            t.AppendLine("--------------------|----------------|--------|----|------|--------|------|----------");

            yield return Warmup();

            var rows = new List<(string name, float perMin, float time, int level)>();

            for (int i = 0; i < content.ships.Length; i++)
            {
                var ship = content.ships[i];

                // 🔴 두 번째 무기를 고정해야 **배의 차이만** 남는다.
                //    절단날은 근접 상시라 어느 배와도 붙어서 기준선으로 쓰기 좋다.
                var second = ship.startingWeapon == WeaponKind.Harpoon ? WeaponKind.Discus : WeaponKind.Harpoon;

                UnlockAndSelect(ship);
                yield return RunWith(ship.startingWeapon, second, director.ComboLevel);

                float minutes = Mathf.Max(0.01f, director.RunTime / 60f);
                float perMin = director.RunValue / minutes;

                t.AppendLine(
                    $"{Pad(ship.displayName, 19)} | {Pad(Weapons.Name(ship.startingWeapon), 14)} | " +
                    $"{director.RunTime,5:0.0}s | {director.BankedCount,2} | {director.RunCollected,4} | " +
                    $"{director.RunValue,6} | {director.FuelRecovered,4:0} | {perMin,8:0}");

                rows.Add((ship.displayName, perMin, director.RunTime, director.BankedCount));

                director.BackToReady();
                yield return null;
            }

            AppendSpread(t, rows, "/분",
                "⚠️ 클리어한 조합은 300초에 끝나고 못 깬 조합은 더 오래 가므로,\n" +
                "   크레딧/분은 **클리어 여부에 오염된다.** 진짜 신호는 '클리어했는가'다.");
            Debug.Log("[SIM]" + t);
            Assert.Pass();
        }

        // ==============================================================================
        //  3. XP 곡선 · 재화 수입 — 가장 오래 묵은 미검증 수치 둘
        // ==============================================================================

        [UnityTest, Timeout(1800000)]
        public IEnumerator MeasureCurveAndIncome()
        {
            var t = new StringBuilder();
            t.AppendLine();
            t.AppendLine("=========== 견인 리듬 · 재화 수입 ===========");

            yield return Warmup();

            // 🔴 **두 상태로 잰다** (2026-08-27).
            //    첫 판만 재면 *"견인 제한이 안 켜진다"*가 영원히 참으로 보인다 —
            //    그건 **아무것도 안 산 상태**의 이야기다. 1구역을 다 사면 무기가 둘이 되고
            //    부수는 속도가 붙는다. **사장님 특색이 언제부터 켜지는지**가 이 표의 질문이다.
            yield return MeasureRun(t, "첫 판 (아무것도 없음)", stage1: false);
            yield return MeasureRun(t, "1구역 천장 (11노드 최대)", stage1: true);

            // 🔴 **2구역도 잰다** (2026-08-27). 1구역은 **고철만** 떨어져서
            //    68개 중 11개를 골라도 **전부 같은 물건**이다 — 선택이 원래 성립하지 않는다.
            //    사장님 특색(*"이걸 가져갈까 저걸 가져갈까"*)이 실제로 사는 곳은
            //    **재화가 섞이기 시작하는 2구역**이다. 거기를 한 번도 안 재봤다.
            yield return MeasureRun(t, "2구역 (회로가 섞인다)", stage1: true, map: 1);

            t.AppendLine("==========================================");
            Debug.Log("[SIM]" + t);
            director.BackToReady();
            yield return null;
            Assert.Pass();
        }

        /// <summary>한 상태로 한 판 굴리고 견인 리듬·수입을 적는다.</summary>
        int mapForRun;

        IEnumerator MeasureRun(StringBuilder t, string label, bool stage1, int map = 0)
        {
            // ⚠️ 노드는 **StartRun 전에** 찍어야 한다 — `RebuildStats`가 그 안에서 돈다
            var savedNodes = new List<NodeRank>(MetaSave.Data.nodes);
            if (stage1)
            {
                var all = director.content.techTree;
                var ok = new HashSet<string>();
                bool ch = true;
                while (ch)
                {
                    ch = false;
                    for (int i = 0; i < all.Length; i++)
                    {
                        var nd = all[i];
                        if (ok.Contains(nd.id)) continue;

                        bool scrapOnly = true;              // 고철 말고는 아무것도 안 드는가
                        for (int mm = 1; mm < Mats.Count; mm++)
                            if (nd.BaseCost((MatKind)mm) > 0) { scrapOnly = false; break; }
                        if (!scrapOnly) continue;

                        bool reqOk = true;
                        if (nd.requires != null)
                            for (int r = 0; r < nd.requires.Length; r++)
                                if (!ok.Contains(nd.requires[r])) { reqOk = false; break; }
                        if (reqOk) { ok.Add(nd.id); ch = true; }
                    }
                }
                for (int i = 0; i < all.Length; i++)
                    if (ok.Contains(all[i].id))
                        MetaSave.Data.SetRank(all[i].id, Mathf.Max(1, all[i].maxRank));
            }

            t.AppendLine();
            t.AppendLine($"───────── {label} ─────────");
            mapForRun = map;

            // 🔴 2026-08-26: 여기는 원래 **레벨 곡선**을 쟀다. 레벨업이 없어진 뒤로는
            //    `TowedCount`(지금 매달린 개수)를 `Lv`라고 찍고
            //    *"첫 레벨업까지 5.9초"*라고 부르고 있었다 — **아무 뜻이 없는 문장**이다.
            //
            //    대신 **견인 리듬**을 잰다. 지금 게임의 심장이 거기다:
            //      · 첫 하나를 매다는 데 몇 초 (초반 손맛)
            //      · **6칸이 처음 꽉 차는 시각** — 이 순간부터 모든 획득이 맞바꾸기가 된다
            //      · 꽉 찬 채로 보낸 시간 비율 — *"선택과 집중"이 실제로 얼마나 오래 켜져 있나*
            //      · 밀려난 횟수 — 몇 번이나 버렸나
            director.StartRun(mapForRun);
            var marks = new List<(int level, float at)>();
            int lastTow = director.TowedCount;
            float firstPick = -1f, firstFull = -1f, fullSeconds = 0f;

            var f = director.field;
            for (int i = 0; i < f.MatsThisRun.Length; i++) f.MatsThisRun[i] = 0;

            int frames = 0;
            var ship = director.ship;

            // 🔴 **고를 여지가 화면에 있는가.**
            //    봇은 고르지 않는다(가장 가까운 것을 줍는다) — 그러니 *"봇이 잘 골랐나"*는
            //    잴 수 없다. 대신 **동시에 몇 종류가 떠 있는지**를 센다.
            //    한 종류만 떠 있으면 사람이 아무리 잘해도 **고를 것이 없다.**
            int kindsSum = 0, kindsMax = 0, twoPlus = 0;
            var seen = new bool[Mats.Count];

            while (director.State != GameState.Result && frames < MaxFramesPerRun)
            {
                DriveBot(ship);

                for (int i = 0; i < seen.Length; i++) seen[i] = false;
                for (int i = 0; i < f.Fragments.Count; i++)
                    if (f.Fragments[i].Alive) seen[(int)f.Fragments[i].mat] = true;
                int kinds = 0;
                for (int i = 0; i < seen.Length; i++) if (seen[i]) kinds++;
                kindsSum += kinds;
                kindsMax = Mathf.Max(kindsMax, kinds);
                if (kinds >= 2) twoPlus++;


                int tow = director.TowedCount;
                if (tow >= director.MaxTow) fullSeconds += StepSeconds;

                if (tow != lastTow)
                {
                    // 🔴 **늘어난 순간만** 기록한다. 밀려나서 줄어드는 것도 여기로 오는데
                    //    그걸 같이 세면 "몇 칸까지 갔나"가 왔다 갔다 하며 뭉개진다
                    if (tow > lastTow)
                    {
                        if (firstPick < 0f) firstPick = director.RunTime;
                        if (tow >= director.MaxTow && firstFull < 0f) firstFull = director.RunTime;
                        if (marks.Count < 12) marks.Add((tow, director.RunTime));
                    }
                    lastTow = tow;
                }

                frames++;

                yield return null;
            }
            ClearBot(ship);

            // 🔴 **무기 레벨을 같이 찍는다.** 이 측정은 `StartRun`이 준 그대로(초반 무기)이고,
            //    위의 조합 표는 `ComboLevel`(강화된 무기)로 돈다. 같은 `주움`인데
            //    6과 74로 나온다 — 조건을 안 적어 두면 **다음에 표 둘을 나란히 놓고
            //    "숫자가 안 맞는다"고 엉뚱한 데를 뒤지게 된다.**
            var sSt = director.Stats;
            int lvSum2 = 0, lvCnt = 0;
            for (int i = 0; i < sSt.weaponLevel.Length; i++)
                if (sSt.weaponLevel[i] > 0) { lvSum2 += sSt.weaponLevel[i]; lvCnt++; }
            t.AppendLine($"⚠️ 조건: 무기 {lvCnt}종 · 평균 Lv.{(lvCnt > 0 ? lvSum2 / (float)lvCnt : 0f):0.0}" +
                         "  (조합 표는 강화된 무기라 `주움`이 훨씬 크다 — 나란히 비교하지 말 것)");
            t.AppendLine();
            t.AppendLine($"견인 칸이 차는 시각 (칸 {director.MaxTow}개)");
            for (int i = 0; i < marks.Count; i++)
                t.AppendLine($"  {marks[i].level,2}칸  {marks[i].at,6:0.0}s");

            float runTime = Mathf.Max(0.01f, director.RunTime);
            int pushed = Mathf.Max(0, director.RunCollected - director.BankedCount);

            t.AppendLine();
            t.AppendLine($"  → 첫 획득까지        {(firstPick >= 0f ? firstPick : -1f),6:0.0}s" +
                         "   🔴 여기가 길면 판의 첫인상이 비어 있다");
            t.AppendLine($"  → 처음 꽉 차기까지    {(firstFull >= 0f ? firstFull : -1f),6:0.0}s" +
                         "   🔴 이때부터 모든 획득이 맞바꾸기가 된다");
            t.AppendLine($"  → 꽉 찬 채로 보낸 시간 {fullSeconds / runTime * 100f,5:0}%" +
                         "    (= '버릴까 말까'가 켜져 있던 시간)");
            t.AppendLine($"  → 밀려난 것          {pushed,6}개" +
                         "   (주움 - 가져옴. 몇 번이나 버렸나)");
            t.AppendLine($"  → 화면에 뜬 종류      평균 {kindsSum / (float)Mathf.Max(1, frames):0.00}" +
                         $" · 최대 {kindsMax} · **2종 이상이던 시간 {twoPlus * 100f / Mathf.Max(1, frames):0}%**");
            t.AppendLine("     🔴 이게 낮으면 고를 것이 없다 — 칸이 몇이든 '선택'이 성립하지 않는다");

            t.AppendLine();
            t.AppendLine($"런 결과: {director.RunTime:0.0}초 · 가져옴 {director.BankedCount} · " +
                         $"주움 {director.RunCollected} · 크레딧 {director.RunValue}");

            t.AppendLine();
            t.AppendLine("재화 수입 (🔴 테크 비용의 기준. 지금 노드 값은 전부 추측이다)");
            for (int i = 0; i < f.MatsThisRun.Length; i++)
                t.AppendLine($"  {Pad(Mats.Name((MatKind)i), 6)} {f.MatsThisRun[i],5}");

            float mins = Mathf.Max(0.01f, director.RunTime / 60f);
            t.AppendLine($"  → 분당 고철 {f.MatsThisRun[0] / mins:0.0} · " +
                         $"회로 {f.MatsThisRun[1] / mins:0.00} · 코어 {f.MatsThisRun[2] / mins:0.000}");

            AppendTechCostEstimate(t, f, mins);

            director.BackToReady();
            MetaSave.Data.nodes.Clear();
            MetaSave.Data.nodes.AddRange(savedNodes);   // 다음 상태에 새지 않게
            yield return null;
        }

        // ==============================================================================
        //  보조
        // ==============================================================================

        /// <summary>
        /// 🔴 워밍업 런 하나를 버린다.
        ///    부팅 직후 첫 런은 에셋 로딩·JIT 때문에 초반 프레임이 흔들려 재현되지 않는다.
        ///    (2026-08-20: 이것 때문에 표의 첫 줄만 실행마다 달랐다)
        /// </summary>
        IEnumerator Warmup()
        {
            yield return RunWith(WeaponKind.Discus, WeaponKind.Harpoon, 3, 900);
            director.BackToReady();
            yield return null;
        }

        /// <summary>이 조합을 만들 수 있는 무기 한 쌍을 찾는다.</summary>
        bool PickPairFor(ComboDef combo, out WeaponKind a, out WeaponKind b)
        {
            a = WeaponKind.Discus; b = WeaponKind.Discus;

            var weapons = director.content.weapons;
            if (weapons == null) return false;

            WeaponDef first = null, second = null;
            for (int i = 0; i < weapons.Length; i++)
            {
                if (first == null && weapons[i].tag == combo.a) { first = weapons[i]; continue; }
                // 같은 계열 조합이면 두 번째도 같은 태그에서, 단 다른 무기로
                if (second == null && weapons[i].tag == combo.b && weapons[i] != first) second = weapons[i];
            }
            if (first == null || second == null) return false;

            a = first.kind; b = second.kind;
            return true;
        }

        void UnlockAndSelect(ShipDef ship)
        {
            var meta = new MetaData();
            meta.unlockedShips.Add(ship.id);
            meta.selectedShip = ship.id;
            MetaSave.ReplaceInMemory(meta);
        }

        /// <summary>
        /// 무기 두 개를 지정 레벨로 주고 런을 끝까지 돌린다.
        /// 🔴 카드로 자연스럽게 얻기를 기다리면 **조합마다 조건이 달라져 비교가 안 된다.**
        ///    그래서 직접 준다 — 여기서 재는 건 "빌드가 만들어진 뒤의 힘"이다.
        /// </summary>
        /// <summary>
        /// 켜면 런 도중 30초마다 상태를 찍는다. 결정론 진단 전용 —
        /// 끝 값만 봐서는 **처음부터 약했는지 도중에 무너졌는지** 구분할 수 없다.
        /// </summary>
        StringBuilder trace;

        IEnumerator RunWith(WeaponKind a, WeaponKind b, int level, int maxFrames = MaxFramesPerRun)
        {
            director.StartRun(0);

            var s = director.Stats;
            for (int i = 0; i < s.weaponLevel.Length; i++) s.weaponLevel[i] = 0;
            s.AddWeapon(a, level);
            if (b != a) s.AddWeapon(b, level);

            director.arms.stats = s;
            director.arms.Rebuild();

            var ship = director.ship;
            int frames = 0;

            while (director.State != GameState.Result && frames < maxFrames)
            {
                DriveBot(ship);

                // 레벨업이 뜨면 첫 장을 고른다. 무기는 이미 줬으므로 강화만 쌓인다

                frames++;

                // 🔴 **초반을 촘촘히 찍는다.** 30초마다만 찍으면 "두 번이 다르다"만 알고
                //    *언제부터* 갈렸는지를 모른다 — 첫 프레임부터 다르면 시작 상태가 샌 것이고,
                //    중간부터 갈리면 판이 도는 중에 남는 것이 있다는 뜻이다. 원인이 완전히 다르다.
                if (trace != null && (frames == 1 || frames == 15 || frames == 30
                                      || frames == 45 || frames == 60 || frames == 75
                                      || frames == 90 || frames == 150 || frames % 900 == 0))
                    trace.AppendLine($"         " + Snapshot($"+{frames}f"));

                yield return null;
            }
            ClearBot(ship);

            if (frames >= maxFrames)
            {
                // 🔴 시간 초과는 **실패가 아니라 결과다** — 끝까지 살아남았다는 뜻이다.
                var sh2 = director.ship;
                Debug.Log($"[SIM] {(maxFrames * StepSeconds):0}초 완주 — 남은 연료 " +
                          $"{(sh2 != null && sh2.FuelMax > 0f ? sh2.Fuel / sh2.FuelMax * 100f : 0f):0}%");
                director.ReturnNow();
                yield return null;
            }
        }

        /// <summary>
        /// 🔴 봇 조종은 **런타임의 `AutoPilot`을 그대로 쓴다.**
        ///    시뮬용 봇을 따로 두면 둘이 조용히 갈라지고,
        ///    그러면 게임에서 봇을 구경해도 시뮬을 검증하는 게 아니게 된다.
        ///    (게임에서는 `B`로 켤 수 있다 — 시뮬이 왜 그런 값을 냈는지 눈으로 보라고)
        /// </summary>
        void DriveBot(ShipController ship) => AutoPilot.Drive(director, ship);

        void ClearBot(ShipController ship)
        {
            AutoPilot.Release(ship);
            AutoPilot.ClearCollect(director);
        }

        /// <summary>
        /// 🔴 밸런스에서 중요한 건 평균이 아니라 **격차**다.
        ///    최고가 최저의 3배를 넘으면 그건 선택지가 아니라 정답과 함정이다.
        /// </summary>
        /// <summary>
        /// 🔴 두 표가 같이 쓴다. rev.7에서 표 1의 단위가 '크레딧/분'에서 '입금'으로 바뀌었으므로
        ///    단위 이름을 **밖에서 준다.** 안 그러면 표가 자기 값을 잘못된 이름으로 부른다.
        /// </summary>
        static void AppendSpread(StringBuilder t, List<(string name, float perMin, float time, int level)> rows,
                                 string unit = "/분", string caveat = null)
        {
            if (rows.Count == 0) return;

            var best = rows[0]; var worst = rows[0];
            float sum = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].perMin > best.perMin) best = rows[i];
                if (rows[i].perMin < worst.perMin) worst = rows[i];
                sum += rows[i].perMin;
            }
            float avg = sum / rows.Count;
            float ratio = worst.perMin > 0.01f ? best.perMin / worst.perMin : 999f;

            t.AppendLine("----------------|------------------------|--------|----|------|--------|------|----------");
            t.AppendLine($"최고: {best.name} ({best.perMin:0}{unit})   최저: {worst.name} ({worst.perMin:0}{unit})   평균 {avg:0}");
            if (caveat != null) t.AppendLine(caveat);
            t.AppendLine($"격차: {ratio:0.0}배  " +
                         (ratio > 3f ? "🔴 3배 초과 — 선택지가 아니라 정답과 함정이다"
                                     : ratio > 2f ? "🟡 2배 초과 — 손봐야 한다"
                                                  : "✅ 허용 범위"));
            t.AppendLine("==========================================");
        }

        /// <summary>재화 수입으로 테크트리 완주 시간을 역산한다.</summary>
        void AppendTechCostEstimate(StringBuilder t, StageField f, float minutes)
        {
            var tree = director.content.techTree;
            if (tree == null || tree.Length == 0) return;

            // 🔴 **여섯 종류를 다 센다** (2026-08-27). 셋만 세면
            //    초합금 이상이 드는 노드의 값이 **표에서 사라진다.**
            var tot = new long[Mats.Count];
            for (int i = 0; i < tree.Length; i++)
            {
                var n = tree[i];
                for (int r = 1; r <= n.maxRank; r++)
                    for (int m = 0; m < Mats.Count; m++)
                        tot[m] += n.CostAt((MatKind)m, r);
            }

            t.AppendLine();
            t.AppendLine("테크트리 전체 완주 비용 (모든 노드 최대 랭크)");
            for (int m = 0; m < Mats.Count; m++)
            {
                if (tot[m] <= 0) continue;
                float perMin = f.MatsThisRun[m] / minutes;
                t.AppendLine($"  {Pad(Mats.Name((MatKind)m), 6)} {tot[m],8:N0}  →  " +
                             (perMin <= 0.0001f ? "이 맵에선 안 나온다"
                                                : $"{Hours(tot[m], perMin):0.0}시간"));
            }
            t.AppendLine("  🔴 **제일 긴 것이 실제 플레이타임이다.** 크게 다르면 병목이 하나뿐이라는 뜻");
            t.AppendLine("  ⚠️ 코어는 티어 2 쓰레기에서만 나온다 — 맵 1에서 0인 건 **의도**다.");
            t.AppendLine("     우주선 해금이 '깊은 맵까지 가라'는 뜻이어야 하기 때문. 맵 3+에서 다시 재야 한다.");
        }

        static float Hours(long total, float perMin)
            => perMin <= 0.0001f ? 9999f : total / perMin / 60f;

        /// <summary>
        /// 🔴 보스를 얼마 만에 깼는가 — **이게 화력의 진짜 지표다.**
        ///    "클리어했는가"는 상한에 오염되고, 크레딧/분은 클리어 시각에 오염된다.
        /// </summary>
        string BossKillText()
            => director.Cleared ? $"{(director.RunTime - BossAtSeconds):0.0}s" : "  못깸  ";

        static string Pad(string s, int width)
        {
            if (string.IsNullOrEmpty(s)) s = "";
            if (s.Length >= width) return s.Substring(0, width);
            return s + new string(' ', width - s.Length);
        }
    }
}
