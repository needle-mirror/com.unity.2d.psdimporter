using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    internal class PSDImportPostProcessor : AssetPostprocessor
    {
        [NoAutoStaticsCleanup]
        private static string s_CurrentApplyAssetPath = null;

        public static string currentApplyAssetPath
        {
            set { s_CurrentApplyAssetPath = value; }
        }
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
        {
            if (!string.IsNullOrEmpty(s_CurrentApplyAssetPath))
            {
                foreach (string asset in importedAssets)
                {
                    if (asset == s_CurrentApplyAssetPath)
                    {
                        Object obj = AssetDatabase.LoadMainAssetAtPath(asset);
                        Selection.activeObject = obj;
                        Unsupported.SceneTrackerFlushDirty();
                        s_CurrentApplyAssetPath = null;
                        break;
                    }
                }
            }
        }
    }
}
