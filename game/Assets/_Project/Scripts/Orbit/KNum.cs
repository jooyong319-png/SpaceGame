namespace SalvageRun.Orbit
{
    /// <summary>
    /// 숫자를 한국어 단위(만·억·조·경)로 읽는다. 🔴 지수 표기(1.2e9)와 소수점은 안 쓴다.
    /// 막이 끝날 때마다 단위 이름이 바뀌는 게 이 게임의 보상이다 (wiki/rev15-design.md 「경제 곡선」).
    ///
    ///   12400        → 1만 2400
    ///   380000000    → 3억 8000만
    ///   1200000000000 → 1조 2000억
    ///
    /// 🌐 영어면 세 자리 단위 (09-27): 12.4K · 380M · 1.2T · 3.4Qa · 5.6Qi
    /// </summary>
    public static class KNum
    {
        static readonly string[] Units = { "", "만", "억", "조", "경" };

        /// <summary>위 단위 하나만 — 34억 · 1200만 (좁은 칸용)</summary>
        public static string Short(double v)
        {
            if (Sim.Loc.En) return En(v);
            if (v < 10000) return ((long)System.Math.Max(0, v)).ToString();
            string f = Fmt(v); int sp = f.IndexOf(' '); return sp > 0 ? f.Substring(0, sp) : f;
        }
        public static string Fmt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + Fmt(-v);
            if (Sim.Loc.En) return En(v);
            if (v >= 9e18) v = 9e18;
            long n = (long)System.Math.Floor(v);
            if (n < 10000) return n.ToString();

            // 네 자리씩 끊어 위에서 두 덩어리만 읽는다 — 더 읽으면 한눈에 안 들어온다
            var parts = new System.Collections.Generic.List<long>();
            while (n > 0) { parts.Add(n % 10000); n /= 10000; }
            int top = System.Math.Min(parts.Count, Units.Length) - 1;

            string s = parts[top] + Units[top];
            if (top > 0 && parts[top - 1] > 0) s += " " + parts[top - 1] + Units[top - 1];
            return s;
        }
        static string En(double v) => Sim.Loc.Num(v);                  // 🌐 영어 숫자는 Loc.Num (봇 · 시뮬도 같이 쓴다)
    }
}
