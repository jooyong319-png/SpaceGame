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
        public static readonly string[] RarName = { "일반", "희귀", "영웅", "전설" };   // 09-26 사장님 「영웅 등급은 없나?」 — 희귀와 전설 사이
        public static readonly double[] RarPrice = { 0.3, 0.8, 1, 2 };   // 판 벌이 배수 — 09-27 사장님 「일반은 가게 열 때 살 수 있게 · 전설은 마지막 전~전전 행성쯤」: 등급 차이는 바닥값(열리는 행성)이 맡고, 열린 뒤엔 몇 판치 (예전 0.4 · 2 · 6 · 18 이라 전설이 끝까지 18판치)
        public static readonly double[] RarFloor = { 0, 1e7, 3e9, 8e10 };   // 등급 바닥 = 처음 살 만해지는 행성 (사장님 기록 기준) — 희귀 1천만 천왕성 · 영웅 30억 카이퍼 · 전설 800억 오르트
        public static readonly double[] RarBoost = { 1, 1.5, 2.5, 4.5 };      // 등급 성능 배수 — 부품 효과에 곱한다 (음수 · 켜짐/꺼짐은 그대로)

        static Parts()
        {
            for (int i = 0; i < Defs.Length; i++)
            {
                var d = Defs[i]; var parts = new System.Collections.Generic.List<string>();
                for (int j = 0; j < d.k.Length; j++)
                {
                    if (d.v[j] > 0 && d.k[j] != "fee0") d.v[j] *= RarBoost[d.rar];
                    if (d.k[j] == "dbl") d.v[j] = System.Math.Min(1, d.v[j]);
                    parts.Add(Say(d.k[j], d.v[j]));
                }
                Defs[i].desc = string.Join(" · ", parts);                           // 설명은 실제 값으로 (등급 배수를 곱한 뒤)
            }
        }
        static string Pc(double v) => System.Math.Round(v * 100, v * 100 < 10 ? 1 : 0).ToString();
        static string Say(string k, double v)
        {
            switch (k)
            {
                case "dmg": case "spd": return "화력 +" + Pc(v) + "%";
                case "rad": return "크기 +" + Pc(v) + "%";
                case "fuel": return "연료 " + (v >= 0 ? "+" : "−") + System.Math.Round(System.Math.Abs(v), 1) + "초";
                case "crit": return "치명 +" + Pc(v) + "%";
                case "dbl": return Pc(v) + "% 확률로 한 발 더";
                case "drone": return "드론 몫 +" + Pc(v) + "%";
                case "val": return "모든 값 +" + Pc(v) + "%";
                case "vault": return "금고 위성 +" + Pc(v) + "%";
                case "att": return "부착물 +" + Pc(v) + "%";
                case "hole": return "블랙홀 확률 +" + Pc(v) + "%";
                case "combo": return "연쇄 보너스 상한 +" + System.Math.Round(v);
                case "cut": return "빚 갚는 몫 −" + Pc(v) + "%";
                case "fee0": return "주식 수수료 0";
                case "div": return "배당 +" + System.Math.Round(v * 100, 3) + "%";
            }
            return k + " +" + v;
        }
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
            P(0, 1, "플라즈마 점화기", "화력 +12%", "spd", 0.12),
            P(0, 3, "핵융합 엔진", "화력 +20% · 연료 +4초", "dmg", 0.20, "fuel", 4),
            P(1, 1, "쌍발 사출구", "35% 확률로 한 발 더", "dbl", 0.35),
            P(1, 0, "대구경 사출구", "크기 +20% (집게 원 · 레이저 굵기 · 번개 거리)", "rad", 0.20),
            P(1, 0, "고속 사출구", "화력 +8%", "spd", 0.08),
            P(1, 3, "삼연발 포탑", "60% 확률로 한 발 더 · 화력 +10%", "dbl", 0.60, "dmg", 0.10),
            P(1, 1, "치명 조준경", "치명 +8%", "crit", 0.08),
            P(2, 0, "보강 선체", "연료 +3초", "fuel", 3),
            P(2, 1, "흡착 선체", "드론 몫 +25%", "drone", 0.25),
            P(2, 0, "경량 선체", "화력 +6% · 연료 −1초", "spd", 0.06, "fuel", -1),
            P(2, 1, "블랙홀 코일", "블랙홀 확률 +0.6%", "hole", 0.006),
            P(3, 0, "금고 레이더", "금고 위성 +50%", "vault", 0.5),
            P(3, 0, "부착물 레이더", "부착물 +40%", "att", 0.4),
            P(3, 1, "고철 감정기", "모든 값 +12%", "val", 0.12),
            P(3, 3, "심우주 레이더", "모든 값 +20% · 금고 위성 +50%", "val", 0.20, "vault", 0.5),
            P(4, 1, "케슬러의 금니", "판 벌이에서 빚 갚는 데 떼 가는 몫 −10%", "cut", 0.10),
            P(4, 0, "행운 동전", "치명 +4% · 연쇄 보너스 상한 +30", "crit", 0.04, "combo", 30),
            P(4, 1, "증권사 배지", "주식 수수료 0 · 배당 +0.05%", "fee0", 1, "div", 0.0005),
            P(4, 1, "연쇄 부적", "연쇄 보너스 상한 +100 (최대 ×2.5)", "combo", 100),
            P(4, 3, "황금 나사", "모든 값 +15% · 화력 +10%", "val", 0.15, "dmg", 0.10),
            // 👑 영웅 (09-26) — 효과 둘, 전설 바로 아래. 새 번호는 맨 뒤에만
            P(0, 2, "이온 추진기", "화력 +25%", "dmg", 0.25),
            P(1, 2, "산탄 사출구", "45% 확률로 한 발 더 · 크기 +15%", "dbl", 0.30, "rad", 0.15),
            P(2, 2, "공명 선체", "드론 몫 +40% · 블랙홀 확률 +0.8%", "drone", 0.40, "hole", 0.008),
            P(3, 2, "금맥 탐지기", "모든 값 +15% · 부착물 +50%", "val", 0.15, "att", 0.5),
            P(4, 2, "연쇄 목걸이", "연쇄 보너스 상한 +150 · 치명 +6%", "combo", 150, "crit", 0.06),
        };
        // 📖 효과마다 판에서 무슨 뜻인지 (09-26 사장님 「가게 아이템 설명이 더 자세했으면」) — 카드에 올리면 옆 창에
        public static readonly System.Collections.Generic.Dictionary<string, string> Help = new System.Collections.Generic.Dictionary<string, string>
        {
            { "dmg",   "화력 — 한 방에 깎는 체력. 큰 잔해 · 장갑판을 덜 쳐도 부순다" },
            { "spd",   "화력 — 한 방에 깎는 체력 (연사는 없앴다 — 이 부품도 화력으로 친다)" },
            { "rad",   "크기 — 한 방이 덮는 넓이 (집게 원 · 레이저 굵기 · 번개 거리). 뭉친 잔해를 한 번에" },
            { "fuel",  "연료 — 한 판에 쓸 수 있는 연료. 늘면 판이 길어지고, 줄면 짧아진다" },
            { "crit",  "치명 — 이 확률로 한 방이 몇 배로 들어간다" },
            { "dbl",   "한 발 더 — 쏠 때마다 이 확률로 같은 자리에 한 번 더" },
            { "drone", "드론 몫 — 드론이 부순 잔해 값이 이만큼 더 붙는다" },
            { "val",   "모든 값 — 무엇을 부수든 받는 돈이 이만큼 더" },
            { "vault", "금고 위성 — 노란 금고 위성을 부술 때 받는 돈이 더" },
            { "att",   "부착물 — 잔해에 붙은 금 · 연료통 같은 부착물이 더 자주 · 더 비싸게" },
            { "cut",   "빚 상환 몫 — 빚이 있을 때 판 수입에서 떼 가는 비율이 줄어든다 (빚이 없으면 효과 없음)" },
            { "fee0",  "수수료 0 — 증권에서 사고팔 때 수수료가 안 붙는다" },
            { "div",   "배당 — 가진 주식 값의 이만큼을 판마다 돈으로 받는다" },
            { "combo", "연쇄 보너스 상한 — 연쇄가 길수록 붙는 값 배수의 천장이 올라간다" },
            { "hole",  "블랙홀 — 칠 때마다 이 확률로 블랙홀이 저절로 열려 주변을 빨아들인다" },
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
