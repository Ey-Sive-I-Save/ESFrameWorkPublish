using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class StateMachineConfigEditorMenu
    {
        private const string ConfigFolder = "Assets/ESNormalAssets/Data/GlobalData/StateMachineConfig";

        [MenuItem(MenuItemPathDefine.GAMEPLAY_BUILDING_PATH + "状态/打开状态机配置", false, 10)]
        public static void OpenStateMachineConfig()
        {
            StateMachineConfig config = FindStateMachineConfig();
            if (config == null)
                config = CreateStateMachineConfig();

            if (config == null)
            {
                EditorUtility.DisplayDialog("State Machine Config", "Could not find or create StateMachineConfig asset.", "OK");
                return;
            }

            config.HasConfirm = true;
            StateMachineConfig.Instance = config;
            EditorUtility.SetDirty(config);

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            AssetDatabase.OpenAsset(config);
        }

        private static StateMachineConfig FindStateMachineConfig()
        {
            List<StateMachineConfig> configs = ESEditorSO.SOS.GetNewGroupOfType<StateMachineConfig>() ?? new List<StateMachineConfig>(0);
            StateMachineConfig fallback = null;

            for (int i = 0; i < configs.Count; i++)
            {
                StateMachineConfig config = configs[i];
                if (config == null)
                    continue;

                if (config.HasConfirm)
                    return config;

                fallback ??= config;
            }

            return fallback;
        }

        private static StateMachineConfig CreateStateMachineConfig()
        {
            EnsureFolder(ConfigFolder);

            StateMachineConfig config = ScriptableObject.CreateInstance<StateMachineConfig>();
            config.HasConfirm = true;

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(ConfigFolder, "StateMachineConfig.asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return config;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
