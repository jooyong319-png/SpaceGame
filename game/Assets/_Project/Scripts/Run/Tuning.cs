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

        /// <summary>드릴이 무는 동안 배 속도 배율. 작을수록 꽉 묶인다.</summary>
        public static float DrillDrag = 0.22f;

        /// <summary>드릴 피해 배수. 캐는 속도가 곧 이 게임의 리듬이다.</summary>
        public static float DrillPower = 1f;

        /// <summary>파손 로봇이 스폰에서 차지하는 비율(0~1). 위협의 총량이다.</summary>
        public static float HunterRatio = 0.22f;

        /// <summary>기지 연료 초당 감소 배수. 🔴 이게 0이면 게임 구조가 무너진다.</summary>
        public static float BaseDrainMul = 1f;

        /// <summary>화물 하나당 기지 회복 배수.</summary>
        public static float FuelPerCargoMul = 1f;

        /// <summary>우주선 최대 연료 배수 — 한 번에 얼마나 멀리 나갈 수 있는가.</summary>
        public static float ShipFuelMul = 1f;

        /// <summary>추진 연료 소모 배수.</summary>
        public static float ThrustFuelMul = 1f;

        /// <summary>
        /// 🔴 쓰레기 크기 배수 (2026-08-22 피드백: *"크기가 너무 작아"*).
        ///    밭이 된 이상 **멀리서도 "저기 캘 게 있다"가 보여야** 찾아갈 마음이 생긴다.
        /// </summary>
        public static float JunkSize = 1.5f;

        /// <summary>
        /// 🔴 쓰레기 밀도 배수 (*"량이 너무 적고"*).
        ///    동시에 떠 있는 개수 상한과 유입 속도에 함께 곱해진다.
        /// </summary>
        public static float JunkDensity = 1f;

        // ---------------------------------------------------------------- 항행 (rev.11)

        /// <summary>항행 한 구간 길이 배수. 방어 국면이 얼마나 긴가.</summary>
        public static float LegSecondsMul = 1f;

        /// <summary>
        /// 🔴 잔해가 기지에 부딪힐 때 연료 손실 배수.
        ///    **"못 막으면 얼마나 손해인가"**를 정한다. 이 값이 항행 난이도의 핵심이다.
        /// </summary>
        public static float IncomingCostMul = 1f;

        /// <summary>항행 중 잔해가 밀려오는 양 배수.</summary>
        public static float IncomingRateMul = 1f;

        /// <summary>기지 포탑 화력 배수 — 항행에서 얼마나 버티는가.</summary>
        public static float TurretPowerMul = 1f;

        /// <summary>끌 때 무거워지는 정도. 작을수록 금방 무거워진다.</summary>
        public static float TowWeightMul = 1f;

        public static void Reset()
        {
            DrillDrag = 0.22f;
            DrillPower = 1f;
            HunterRatio = 0.22f;
            BaseDrainMul = 1f;
            FuelPerCargoMul = 1f;
            ShipFuelMul = 1f;
            ThrustFuelMul = 1f;
            JunkSize = 1.5f;
            JunkDensity = 1f;
            LegSecondsMul = 1f;
            IncomingCostMul = 1f;
            IncomingRateMul = 1f;
            TurretPowerMul = 1f;
            TowWeightMul = 1f;
        }

        /// <summary>사장님이 그대로 읽어서 알려줄 수 있게 한 줄로.</summary>
        public static string Summary =>
            $"드릴묶임 {DrillDrag:0.00} · 드릴피해 {DrillPower:0.00} · 로봇비율 {HunterRatio:0.00} · " +
            $"기지감소 {BaseDrainMul:0.00} · 화물회복 {FuelPerCargoMul:0.00} · " +
            $"배연료 {ShipFuelMul:0.00} · 추진소모 {ThrustFuelMul:0.00} · " +
            $"쓰레기크기 {JunkSize:0.00} · 쓰레기밀도 {JunkDensity:0.00} · " +
            $"항행길이 {LegSecondsMul:0.00} · 충돌손실 {IncomingCostMul:0.00} · " +
            $"잔해량 {IncomingRateMul:0.00} · 포탑화력 {TurretPowerMul:0.00} · 견인무게 {TowWeightMul:0.00}";
    }
}
