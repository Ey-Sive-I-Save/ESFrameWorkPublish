using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class GameCoreGlobalDataMenu
    {
        private const string AssetFolder = "Assets/ESNormalAssets/Data/GlobalData/GameCore";
        private const string AssetPath = AssetFolder + "/GameCoreGlobalData.asset";
        [MenuItem("ES/GameCore/打开或创建GameCore全局数据", priority = 20)]
        public static void OpenOrCreateGameCoreGlobalData()
        {
            GameCoreGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreGlobalData>(AssetPath);
            if (data == null)
                data = CreateGameCoreGlobalData();

            if (data == null)
                return;

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        [MenuItem("ES/GameCore/重置GameCore推荐规则", priority = 21)]
        public static void ResetGameCoreDefaultRules()
        {
            GameCoreGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreGlobalData>(AssetPath);
            if (data == null)
                data = CreateGameCoreGlobalData();

            if (data == null)
                return;

            data.ResetDefaultRules();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        private static GameCoreGlobalData CreateGameCoreGlobalData()
        {
            EnsureFolder("Assets/ESNormalAssets", "Data");
            EnsureFolder("Assets/ESNormalAssets/Data", "GlobalData");
            EnsureFolder("Assets/ESNormalAssets/Data/GlobalData", "GameCore");

            GameCoreGlobalData data = ScriptableObject.CreateInstance<GameCoreGlobalData>();
            data.ResetDefaultRules();
            AssetDatabase.CreateAsset(data, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
