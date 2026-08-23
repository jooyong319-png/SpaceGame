#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SalvageRun.EditorTools
{
    /// <summary>
    /// itch.io에 올릴 WebGL 빌드를 한 번에 만든다.
    ///
    /// 🔴 손으로 설정하면 반드시 하나를 빠뜨린다. 특히 압축 설정은
    ///    빠뜨려도 **에디터에서는 멀쩡하고 itch에서만 하얀 화면**이 되므로 찾기 어렵다.
    /// </summary>
    public static class WebGLBuild
    {
        const string OutDir = "build/webgl";

        [MenuItem("SalvageRun/WebGL 빌드 (itch.io용)")]
        public static void Build()
        {
            ApplySettings();

            string root = Path.GetDirectoryName(Application.dataPath);      // .../game
            string outPath = Path.Combine(Path.GetDirectoryName(root), OutDir);
            Directory.CreateDirectory(outPath);

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("[SalvageRun] Build Settings에 씬이 없다. " +
                               "File > Build Settings에서 현재 씬을 추가할 것.");
                return;
            }

            var opts = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(scenes, s => s.path),
                locationPathName = outPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[SalvageRun] WebGL 빌드 시작 → {outPath}");
            var report = BuildPipeline.BuildPlayer(opts);

            if (report.summary.result == BuildResult.Succeeded)
            {
                float mb = report.summary.totalSize / 1024f / 1024f;
                Debug.Log($"[SalvageRun] 빌드 성공 · {mb:0.0} MB · {outPath}\n" +
                          "itch.io 업로드 방법:\n" +
                          $"  1. {OutDir} 폴더를 통째로 zip으로 압축한다 (폴더가 아니라 **내용물**이 zip 루트에 오게)\n" +
                          "  2. itch.io > Upload > 'This file will be played in the browser' 체크\n" +
                          "  3. Embed 크기는 960 x 600 정도, 'Click to launch in fullscreen' 켜기\n" +
                          "  4. 공개 전이면 Visibility를 Draft 또는 Restricted로");
                EditorUtility.RevealInFinder(outPath);
            }
            else
            {
                Debug.LogError($"[SalvageRun] 빌드 실패: {report.summary.result} " +
                               $"(에러 {report.summary.totalErrors}건)");
            }
        }

        [MenuItem("SalvageRun/WebGL 설정만 적용 (빌드 안 함)")]
        public static void ApplySettings()
        {
            // 🔴 itch.io는 파일에 Content-Encoding 헤더를 붙여주지 않는다.
            //    그래서 Brotli/Gzip을 쓰려면 **압축 해제 폴백**이 반드시 켜져 있어야 한다.
            //    안 켜면 itch에서 하얀 화면만 뜨고 콘솔에만 에러가 난다 — 원인 찾기가 고약하다.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // 예외를 전부 켜면 코드가 크게 불어난다. 명시적으로 던진 것만 잡는다
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // 두 번째 방문부터 로딩이 빨라진다 — 친구들이 여러 판 하려면 중요하다
            PlayerSettings.WebGL.dataCaching = true;

            // 🔴 로딩 화면. 유니티 기본 템플릿은 회색 배경에 진행 바 하나뿐이라
            //    itch에서 그걸 본 사람은 "덜 만든 것"으로 읽고 닫는다.
            //    `Assets/WebGLTemplates/SalvageRun/index.html`
            PlayerSettings.WebGL.template = "PROJECT:SalvageRun";

            PlayerSettings.stripEngineCode = true;
            PlayerSettings.runInBackground = false;

            // 🔴 이 게임은 화면에 200개가 뜬다. WebGL에서 프레임이 깎이는 걸 조금이라도 막는다
            QualitySettings.vSyncCount = 1;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
            });

            PlayerSettings.SetIl2CppCompilerConfiguration(
                NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);

            Debug.Log("[SalvageRun] WebGL 설정 적용 완료 " +
                      "(Gzip + 폴백 · 예외 최소 · 데이터 캐싱 · 스트립 · 커스텀 로딩 화면)");
        }
    }
}
#endif
