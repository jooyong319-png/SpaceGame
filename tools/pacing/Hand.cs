using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 🖱 사장님 손 (09-29 「내가 클릭하는 횟수 기반으로 시뮬」)
// 게임이 판마다 남기는 playlog.tsv 의 손 기록(초당누름 · 누른% · 관문노림%)을 청구서 단계별로 평균 내서
// 봇이 그대로 따라 한다 — 수동 사격(누르고 있을 때만 빔) · 누를 때마다 한 방 · 관문을 노리는 비율.
// 기록이 모자라면(판 5개 미만) 끈다 → 예전처럼 늘 자동 사격. HAND=0 이면 기록이 있어도 끈다.
static class Hand
{
    public static bool On;
    public static int Rows;
    public static readonly double[] Rate = new double[13], Held = new double[13], GateAim = new double[13];
    public static readonly int[] N = new int[13];

    public static void Load()
    {
        if (Environment.GetEnvironmentVariable("HAND") == "0") return;
        var paths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "playlog.tsv"),                                   // 에디터 기록 (SalvageRun/playlog.tsv)
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "jooco", "Orbit Sweeper", "playlog.tsv"),   // 빌드 기록
        };
        var extra = Environment.GetEnvironmentVariable("HANDLOG"); if (!string.IsNullOrEmpty(extra)) paths.Add(extra);
        double[] sr = new double[13], sh = new double[13], sg = new double[13]; int[] ng = new int[13];
        foreach (var p in paths.Select(Path.GetFullPath).Distinct())
        {
            if (!File.Exists(p)) continue;
            var lines = File.ReadAllLines(p); if (lines.Length < 2) continue;
            var head = lines[0].Split('\t'); int cb = Array.IndexOf(head, "청구서"), cr = Array.IndexOf(head, "초당누름"), ch = Array.IndexOf(head, "누른%"), cg = Array.IndexOf(head, "관문노림%"), cgt = Array.IndexOf(head, "관문초"), ct = Array.IndexOf(head, "판초");
            if (cb < 0 || cr < 0 || ch < 0) continue;
            foreach (var ln in lines.Skip(1))
            {
                var c = ln.Split('\t'); if (c.Length <= Math.Max(cr, ch)) continue;
                if (!int.TryParse(c[cb], out int b) || !D(c[cr], out double r) || !D(c[ch], out double h)) continue;
                if (ct >= 0 && D(c[ct], out double secs) && secs < 5) continue;                   // 바로 끝낸 판은 빼고
                b = Math.Max(0, Math.Min(12, b)); sr[b] += r; sh[b] += h / 100; N[b]++; Rows++;
                if (cg >= 0 && cgt >= 0 && D(c[cgt], out double gt) && gt > 3 && D(c[cg], out double ga)) { sg[b] += ga / 100; ng[b]++; }
            }
        }
        if (Rows < 5) { Rows = 0; Array.Clear(N, 0, 13); return; }
        double ar = 0, ah = 0, ag = 0; int an = 0, agn = 0;
        for (int b = 0; b < 13; b++) { ar += sr[b]; ah += sh[b]; an += N[b]; ag += sg[b]; agn += ng[b]; }
        ar /= an; ah /= an; ag = agn > 0 ? ag / agn : 0.35;
        for (int b = 0; b < 13; b++)
        {   // 판이 셋 넘게 모인 단계는 그 단계 값, 모자라면 전체 평균 쪽으로 섞는다
            double w = Math.Min(1, N[b] / 3.0);
            Rate[b] = w * (N[b] > 0 ? sr[b] / N[b] : ar) + (1 - w) * ar;
            Held[b] = w * (N[b] > 0 ? sh[b] / N[b] : ah) + (1 - w) * ah;
            GateAim[b] = ng[b] > 0 ? sg[b] / ng[b] : ag;
        }
        On = true;
    }
    static bool D(string s, out double v) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);

    public static string Summary()
    {
        if (!On) return "🖱 손 기록 없음 — 늘 자동 사격 (예전 봇)";
        var parts = new List<string>();
        for (int b = 0; b < 13; b++) if (N[b] > 0) parts.Add($"{b}장:{Rate[b]:0.0}/초·{Held[b] * 100:0}%");
        return $"🖱 사장님 손 {Rows}판 기준 — 청구서 갚은 수: 초당 누름 · 누르고 있는 % → " + string.Join("  ", parts);
    }
}
