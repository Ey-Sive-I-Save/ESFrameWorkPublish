using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESRuntimeDataAssetEditorInjector
    {
        [MenuItem("【ES】/资源与发布/索引与注册/注入运行时资产注册表")]
        public static void MenuAutoRegisterAllLibraries()
        {
            AutoRegisterAllLibraries(true, true);
            AssetDatabase.SaveAssets();
        }

        public static ESAssetAutoRegisterReport AutoRegisterAllLibraries(bool clearBeforeInject = true, bool logReport = true)
        {
            ESAssetAutoRegisterReport report = ESRuntimeDataAsset.RebuildEditorConfigQueryTableFromLibraries(true, clearBeforeInject);
            if (logReport)
                Debug.Log(report.ToString());

            return report;
        }
    }
}
