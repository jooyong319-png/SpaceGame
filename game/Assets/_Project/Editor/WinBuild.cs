#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SalvageRun.EditorTools
{
    /// <summary>
    /// 윈도우 64비트 빌드 — Builds/OrbitSweeper_win64/OrbitSweeper.exe (Builds/ 는 git 무시).
    /// 결과는 Builds/build_result.txt 한 줄로 남긴다 (MCP 로 부를 때 끝났는지 알 수 있게).
    /// </summary>
    public static class WinBuild
    {
        static bool building;
        [MenuItem("SalvageRun/윈도우 64비트 빌드")]
        public static void Build()
        {
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));   // .../SalvageRun
            string outDir = Path.Combine(root, "Builds", "OrbitSweeper_win64");
            string res = Path.Combine(root, "Builds", "build_result.txt");
            if (File.Exists(res)) File.Delete(res);
            if (building) return;
            building = true;
            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                var scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
                var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(outDir, "OrbitSweeper.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                File.WriteAllText(res, rep.summary.result + " errors=" + rep.summary.totalErrors + " size=" + (rep.summary.totalSize / 1048576f).ToString("0.0") + "MB time=" + rep.summary.totalTime);
                Debug.Log("[SalvageRun] 윈도우 빌드 " + rep.summary.result + " → " + outDir);
            }
            finally { building = false; }
        }
    }
}
#endif
