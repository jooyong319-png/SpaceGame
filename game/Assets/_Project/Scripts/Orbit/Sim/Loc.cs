using System;
using System.Collections.Generic;

namespace SalvageRun.Orbit.Sim
{
    /// <summary>
    /// 🌐 언어 (09-27 사장님 「언어 영어 해놔봐」 — 스팀 출시 준비).
    /// 코드 안 한국어 글은 그대로 두고 Loc.T("한국어") 로 감싼다 — 영어면 번역표(Resources/loc_en.txt)에서 찾아 바꾸고, 없으면 한국어 그대로.
    /// 언어는 게임을 켤 때 정한다 (정적 표 · 트리 칸 이름이 시작할 때 한 번 만들어지므로 — 바꾸면 다시 켜야 한다). 봇은 늘 한국어.
    /// </summary>
    public static class Loc
    {
        public static bool En;                                              // 켤 때 SweepGame 이 정한다 (PlayerPrefs orbit.lang)
        public static Func<string> Source;                                  // 번역표 읽기 (유니티가 넣는다 — 봇에선 없음)
        static Dictionary<string, string> map;

        public static string T(string ko)
        {
            if (!En || string.IsNullOrEmpty(ko)) return ko;
            if (map == null) Load();
            return map.TryGetValue(ko, out var e) ? e : ko;
        }

        static void Load()
        {
            map = new Dictionary<string, string>();
            var src = Source != null ? Source() : null;
            if (string.IsNullOrEmpty(src)) return;
            foreach (var raw in src.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t'); if (tab <= 0) continue;
                string ko = Unesc(line.Substring(0, tab)), en = Unesc(line.Substring(tab + 1));
                if (en.Length > 0) map[ko] = en;
            }
        }
        static string Unesc(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");
        public static void Reset() { map = null; }
        static readonly string[] EnUnits = { "", "K", "M", "B", "T", "Qa", "Qi" };
        static readonly System.Globalization.CultureInfo IC = System.Globalization.CultureInfo.InvariantCulture;
        /// <summary>영어 숫자 — 1000 아래는 그대로 · 위는 유효 숫자 셋 (12.4K · 380M · 1.2T)</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + Num(-v);
            if (v < 1000) return ((long)Math.Floor(v)).ToString(IC);
            int u = 0; while (v >= 1000 && u < EnUnits.Length - 1) { v /= 1000; u++; }
            string n = v >= 100 ? Math.Floor(v).ToString("0", IC) : v >= 10 ? (Math.Floor(v * 10) / 10).ToString("0.#", IC) : (Math.Floor(v * 100) / 100).ToString("0.##", IC);
            return n + EnUnits[u];
        }
    }
}
