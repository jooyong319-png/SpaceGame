using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace SalvageRun.EditorTools
{
    /// <summary>
    /// 📄 빌드가 끝나면 글꼴 라이선스(갈무리 · SIL OFL)를 실행 파일 옆에 둔다 (09-27 스팀 출시 준비).
    /// OFL 은 글꼴을 함께 배포할 때 라이선스 글을 같이 넣으라고 한다.
    /// </summary>
    public static class CopyLicenses
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string exePath)
        {
            string src = "Assets/_Project/Resources/OFL.txt";
            if (!File.Exists(src)) return;
            string dir = Directory.Exists(exePath) ? exePath : Path.GetDirectoryName(exePath);
            File.Copy(src, Path.Combine(dir, "Galmuri-OFL.txt"), true);
        }
    }
}
