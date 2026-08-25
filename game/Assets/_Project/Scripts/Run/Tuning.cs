using UnityEngine;

namespace SalvageRun.Run
{
    /// <summary>
    /// 🔴 **플레이 중에 돌리는 손잡이** (rev.10).
    ///
    ///    2026-08-21까지 rev.7 → rev.10을 지나오면서 **한 판도 안 해보고** 수치를 정했다.
    ///    기지 감소율, 드릴이 묶는 정도, 로봇 비율 — 전부 내가 찍은 값이고,
    ///    전부 "너무 빡빡하거나 너무 헐거울" 것이다.
    ///
    /// 🔴 그래서 **내가 값을 더 찍는 대신, 사장님이 플레이하면서 직접 돌리게** 만든다.
    ///    추측을 판단으로 바꾸는 게 지금 가장 값싼 진전이다 —
    ///    한 판 하면서 손잡이를 돌려 보고 "이 값이 맞다"고 말해 주는 쪽이,
    ///    내가 열 번 추측하는 것보다 빠르고 정확하다.
    ///
    ///    · `K`로 연다 (⚠️ F-키는 브라우저가 가로챈다)
    ///    · 전부 **배수**다 — 맵 데이터가 여전히 기준이고 여기서 곱해진다.
    ///      그래야 "2번 맵이 1번보다 빡세다" 같은 관계가 유지된 채로 전체만 움직인다
    ///    · 값은 저장하지 않는다. 판단용이지 설정이 아니다
    /// </summary>
    public static class Tuning
    {
        public static bool PanelOpen;

        /// <summary>
        /// 🔴 파손 로봇이 스폰에서 차지하는 비율(0~1).
        ///    **기본값 0** — 2026-08-23부터 플레이어를 공격하는 것이 없다.
        ///    올리면 그 자리에서 로봇이 다시 나오지만 **아프지는 않다** (접촉 피해가 없다).
        /// </summary>
        public static float HunterRatio = 0f;

        /// <summary>우주선 최대 연료 배수 — 연료가 곧 체력이므로 이게 곧 맷집이다.</summary>
        public static float ShipFuelMul = 1f;

        /// <summary>
        /// 🔴 **연료 감소 배수 = 판이 얼마나 빨리 끝나는가.**
        ///    연료가 타이머가 되면서(2026-08-23) 이 값이 곧 제한 시간이다.
        ///    0으로 두면 **안 끝난다** — 연습용으로 쓸 수 있다.
        /// </summary>
        public static float FuelDrainMul = 1f;

        /// <summary>
        /// 🔴 쓰레기 크기 배수 (2026-08-22 피드백: *"크기가 너무 작아"*).
        /// </summary>
        public static float JunkSize = 1.5f;

        /// <summary>
        /// 🔴 쓰레기 밀도 배수 (*"량이 너무 적고"*).
        ///    동시에 떠 있는 개수 상한과 유입 속도에 함께 곱해진다.
        /// </summary>
        public static float JunkDensity = 1f;

        /// <summary>
        /// 🔴 **쓰레기가 흘러가는 속도 배수** (2026-08-23).
        ///    쫓아오지 않고 저 혼자 떠다니게 바꾸면서 생긴 손잡이다.
        ///    너무 느리면 화면이 정지 화면처럼 보이고,
        ///    너무 빠르면 잔해가 아니라 총알로 보인다. **로봇에는 안 걸린다.**
        /// </summary>
        public static float JunkSpeedMul = 1f;

        /// <summary>
        /// 🔴 쓰레기에 닿을 때 연료 손실 배수 — **"한 대가 얼마나 아픈가"**다.
        ///    연료가 곧 체력이므로 이 값이 곧 난이도다.
        /// </summary>
        public static float IncomingCostMul = 1f;

        /// <summary>
        /// 🔴 끌 때 무거워지는 정도. **작을수록 금방 무거워진다.**
        ///    이 값이 "몇 개까지 욕심낼 수 있나"를 정한다 — 선택과 집중의 손잡이다.
        /// </summary>
        public static float TowWeightMul = 1f;

        /// <summary>기지 포탑 화력 배수.</summary>
        public static float TurretPowerMul = 1f;

        public static void Reset()
        {
            HunterRatio = 0f;
            ShipFuelMul = 1f;
            FuelDrainMul = 1f;
            JunkSize = 1.5f;
            JunkDensity = 1f;
            JunkSpeedMul = 1f;
            IncomingCostMul = 1f;
            TowWeightMul = 1f;
            TurretPowerMul = 1f;
        }

        /// <summary>사장님이 그대로 읽어서 알려줄 수 있게 한 줄로.</summary>
        public static string Summary =>
            $"로봇비율 {HunterRatio:0.00} · " +
            $"배연료 {ShipFuelMul:0.00} · 연료감소 {FuelDrainMul:0.00} · " +
            $"쓰레기크기 {JunkSize:0.00} · 쓰레기밀도 {JunkDensity:0.00} · " +
            $"쓰레기속도 {JunkSpeedMul:0.00} · " +
            $"충돌손실 {IncomingCostMul:0.00} · 견인무게 {TowWeightMul:0.00}";
    }
}
