namespace SalvageRun.Orbit.Sim
{
    /// <summary>
    /// 🔩 청소선 부품 (09-24 설계서 3단계) — 칸 다섯(엔진 · 사출기 · 선체 · 레이더 · 부적)에 하나씩 끼운다.
    /// 트리가 "영구 성장"이라면 부품은 "지금 이 청소선의 조합". 부품 가게 진열은 출동마다 바뀐다. 가끔 열쇠(핵심 칸을 여는 것)도 올라온다.
    /// 효과 키: dmg 화력% · spd 연사% · rad 크기% · fuel 연료초 · crit 치명 · dbl 한 발 더 · drone 드론 몫% · val 값% · vault 금고% · att 부착물% · cut 상환 몫 − · fee0 수수료 0 · div 배당 · combo 연쇄 보너스 상한 · hole 블랙홀 확률
    /// </summary>
    public struct PartDef { public string name, desc; public int slot, rar; public string[] k; public double[] v; }

    public static class Parts
    {
        public const int Key = 100;                                          // 가게에 올라오는 열쇠
        public static readonly string[] SlotName = { "엔진", "사출기", "선체", "레이더", "부적" };
        public static readonly string[] RarName = { "일반", "희귀", "전설" };
        public static readonly double[] RarPrice = { 1, 2.5, 6 };
        static PartDef P(int slot, int rar, string name, string desc, params object[] kv)
        {
            var k = new string[kv.Length / 2]; var v = new double[kv.Length / 2];
            for (int i = 0; i < k.Length; i++) { k[i] = (string)kv[i * 2]; v[i] = System.Convert.ToDouble(kv[i * 2 + 1]); }
            return new PartDef { slot = slot, rar = rar, name = name, desc = desc, k = k, v = v };
        }
        public static readonly PartDef[] Defs =
        {
            P(0, 1, "과급 엔진", "화력 +10% · 연료 −3초", "dmg", 0.10, "fuel", -3),
            P(0, 0, "장거리 탱크", "연료 +5초", "fuel", 5),
            P(0, 1, "플라즈마 점화기", "연사 +12%", "spd", 0.12),
            P(0, 2, "핵융합 엔진", "화력 +20% · 연료 +4초", "dmg", 0.20, "fuel", 4),
            P(1, 1, "쌍발 사출구", "35% 확률로 한 발 더", "dbl", 0.35),
            P(1, 0, "대구경 사출구", "크기 +20% (집게 원 · 레이저 굵기 · 번개 거리)", "rad", 0.20),
            P(1, 0, "고속 사출구", "연사 +8%", "spd", 0.08),
            P(1, 2, "삼연발 포탑", "60% 확률로 한 발 더 · 화력 +10%", "dbl", 0.60, "dmg", 0.10),
            P(1, 1, "치명 조준경", "치명 +8%", "crit", 0.08),
            P(2, 0, "보강 선체", "연료 +3초", "fuel", 3),
            P(2, 1, "흡착 선체", "드론 몫 +25%", "drone", 0.25),
            P(2, 0, "경량 선체", "연사 +6% · 연료 −1초", "spd", 0.06, "fuel", -1),
            P(2, 1, "블랙홀 코일", "블랙홀 확률 +0.6%", "hole", 0.006),
            P(3, 0, "금고 레이더", "금고 위성 +50%", "vault", 0.5),
            P(3, 0, "부착물 레이더", "부착물 +40%", "att", 0.4),
            P(3, 1, "고철 감정기", "모든 값 +12%", "val", 0.12),
            P(3, 2, "심우주 레이더", "모든 값 +20% · 금고 위성 +50%", "val", 0.20, "vault", 0.5),
            P(4, 1, "케슬러의 금니", "빚 상환으로 떼는 몫 −10%p", "cut", 0.10),
            P(4, 0, "행운 동전", "치명 +4% · 연쇄 보너스 상한 +30", "crit", 0.04, "combo", 30),
            P(4, 1, "증권사 배지", "주식 수수료 0 · 배당 +0.05%", "fee0", 1, "div", 0.0005),
            P(4, 1, "연쇄 부적", "연쇄 보너스 상한 +100 (최대 ×2.5)", "combo", 100),
            P(4, 2, "황금 나사", "모든 값 +15% · 화력 +10%", "val", 0.15, "dmg", 0.10),
        };
        public static double Sum(int[] equipped, string key)
        {
            if (equipped == null) return 0;
            double s = 0;
            foreach (var id in equipped)
            {
                if (id < 0 || id >= Defs.Length) continue;
                var d = Defs[id];
                for (int i = 0; i < d.k.Length; i++) if (d.k[i] == key) s += d.v[i];
            }
            return s;
        }
    }
}
