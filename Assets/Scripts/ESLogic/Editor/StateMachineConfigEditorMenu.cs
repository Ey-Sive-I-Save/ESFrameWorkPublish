using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class StateMachineConfigEditorMenu
    {
        private const string ConfigFolder = "Assets/NormalResources/Data/GlobalData/StateMachineConfig";

        [MenuItem("ES/State/Open State Machine Config", false, 10)]
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
            string[] guids = AssetDatabase.FindAssets("t:StateMachineConfig");
            StateMachineConfig fallback = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                StateMachineConfig config = AssetDatabase.LoadAssetAtPath<StateMachineConfig>(path);
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
