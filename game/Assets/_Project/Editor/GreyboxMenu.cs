#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using SalvageRun.Data;
using SalvageRun.Run;

namespace SalvageRun.EditorTools
{
    public static class GreyboxMenu
    {
        const string DataDir = "Assets/_Project/Data";

        [MenuItem("SalvageRun/그레이박스 준비 (현재 씬에 부트스트랩 추가)")]
        public static void AddBootstrap()
        {
            var existing = Object.FindFirstObjectByType<GreyboxBootstrap>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                Debug.Log("[SalvageRun] 이미 있다. Play 누르면 된다.");
                return;
            }

            var go = new GameObject("== GREYBOX BOOTSTRAP ==");
            go.AddComponent<GreyboxBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Add Greybox Bootstrap");
            Selection.activeObject = go;
            Debug.Log("[SalvageRun] 부트스트랩 추가 완료. Play를 누르면 씬이 코드로 조립된다.");
        }

        /// <summary>밸런스 정본을 에셋으로 뽑는다. 이후 수치는 인스펙터에서 만진다.</summary>
        [MenuItem("SalvageRun/데이터 에셋 생성 (밸런스 정본 만들기)")]
        public static void CreateDataAssets()
        {
            Directory.CreateDirectory(DataDir);

            var run = LoadOrCreate<RunConfig>($"{DataDir}/RunConfig.asset");
            var gc = LoadOrCreate<GameContent>($"{DataDir}/GameContent.asset");
            if (gc.IsEmpty) { ContentDefaults.Fill(gc); EditorUtility.SetDirty(gc); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var boot = Object.FindFirstObjectByType<GreyboxBootstrap>();
            if (boot != null)
            {
                Undo.RecordObject(boot, "Assign Data Assets");
                boot.configAsset = run;
                boot.contentAsset = gc;
                EditorUtility.SetDirty(boot);
                Debug.Log("[SalvageRun] 데이터 에셋 생성 + 부트스트랩에 연결 완료.");
            }
            else
            {
                Debug.Log($"[SalvageRun] 데이터 에셋 생성 완료 → {DataDir}");
            }

            Selection.activeObject = gc;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>
        /// 🔴 `데이터 에셋 생성`은 **비어 있을 때만** 채운다. 에셋이 한 번 만들어진 뒤로는
        ///    `ContentDefaults.cs`를 아무리 고쳐도 게임에 반영되지 않는다 —
        ///    "고쳤는데 왜 그대로지?"의 전형적인 원인이다. 이 메뉴가 그걸 덮어쓴다.
        ///    ⚠️ 인스펙터에서 손으로 만진 값도 같이 날아간다.
        /// </summary>
        [MenuItem("SalvageRun/데이터 에셋 다시 채우기 (코드 기본값으로 덮어쓰기)")]
        public static void RefillDataAssets()
        {
            var gc = AssetDatabase.LoadAssetAtPath<GameContent>($"{DataDir}/GameContent.asset");
            if (gc == null)
            {
                Debug.LogWarning("[SalvageRun] GameContent.asset이 없다. 먼저 `데이터 에셋 생성`을 실행할 것.");
                return;
            }

            ContentDefaults.Fill(gc);
            EditorUtility.SetDirty(gc);
            AssetDatabase.SaveAssets();
            Debug.Log("[SalvageRun] GameContent를 코드 기본값으로 덮어썼다.");
        }

        [MenuItem("SalvageRun/영구 저장 초기화 (크레딧·테크트리 삭제)")]
        public static void WipeSave()
        {
            SalvageRun.Meta.MetaSave.WipeForTesting();
            Debug.Log("[SalvageRun] meta.json 초기화됨.");
        }
    }
}
#endif
