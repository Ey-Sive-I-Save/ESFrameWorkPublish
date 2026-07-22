using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESRuntimeDataAssetEditorInjector
    {
        [MenuItem("ES/Runtime Data/Inject Asset Registry")]
        public static void MenuAutoRegisterAllLibraries()
        {
            AutoRegisterAllLibraries(true, true);
            AssetDatabase.SaveAssets();
        }

        public static ESAssetAutoRegisterReport AutoRegisterAllLibraries(bool clearBeforeInject = true, bool logReport = true)
        {
            ESAssetAutoRegisterReport report = ESRuntimeDataAsset.RebuildAssetTableFromLibrariesEditor(true, clearBeforeInject);
            if (logReport)
                Debug.Log(report.ToString());

            return report;
        }
    }
}
