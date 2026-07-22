using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESInputConfigAssetMenu
    {
        private const string DefaultFolder = "Assets/ESNormalAssets/Data/GlobalData/Input";
        private const string DefaultAssetPath = DefaultFolder + "/ESInputConfig.asset";
        private const string LegacyFolder = "Assets/ESNormalAssets/Data/Legacy/InputConfigGroup";

        [MenuItem("【ES】/运行时/输入/迁移旧输入配置为独立资产", priority = 1100)]
        public static void MigrateLegacyInputConfigToStandaloneAsset()
        {
            ESInputConfig source = FindBestInputConfig();
            ESInputConfig target = AssetDatabase.LoadAssetAtPath<ESInputConfig>(DefaultAssetPath);

            if (target != null && source != null && target != source)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "确认覆盖独立输入配置",
                    "已经存在独立输入配置：\n" + DefaultAssetPath + "\n\n是否用找到的旧配置覆盖它？",
                    "覆盖",
                    "取消");
                if (!overwrite)
                    return;
            }

            EnsureFolder(DefaultFolder);

            if (target == null)
            {
                target = ScriptableObject.CreateInstance<ESInputConfig>();
                target.name = "ESInputConfig";
                AssetDatabase.CreateAsset(target, DefaultAssetPath);
            }

            if (source != null && source != target)
            {
                EditorUtility.CopySerialized(source, target);
                target.name = "ESInputConfig";
                EditorUtility.SetDirty(target);
                Debug.Log("已提取旧输入配置为独立资产：" + DefaultAssetPath, target);
            }
            else
            {
                target.ApplyDefaultGameplayConfig();
                EditorUtility.SetDirty(target);
                Debug.Log("未找到旧输入配置，已创建默认独立输入配置：" + DefaultAssetPath, target);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        [MenuItem("【ES】/运行时/输入/定位独立输入配置", priority = 1101)]
        public static void PingStandaloneInputConfig()
        {
            ESInputConfig config = AssetDatabase.LoadAssetAtPath<ESInputConfig>(DefaultAssetPath);
            if (config == null)
            {
                if (EditorUtility.DisplayDialog(
                        "没有找到独立输入配置",
                        "默认路径还没有 ESInputConfig.asset：\n" + DefaultAssetPath + "\n\n是否现在创建？",
                        "创建",
                        "取消"))
                {
                    MigrateLegacyInputConfigToStandaloneAsset();
                }

                return;
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private static ESInputConfig FindBestInputConfig()
        {
            var candidates = new List<ESInputConfig>();
            AddTypedAssets(candidates);
            AddSubAssetsFromFolder(candidates, LegacyFolder);
            return PickBest(candidates);
        }

        private static void AddTypedAssets(List<ESInputConfig> candidates)
        {
            List<ESInputConfig> configs = ESEditorSO.SOS.GetNewGroupOfType<ESInputConfig>() ?? new List<ESInputConfig>(0);
            for (int i = 0; i < configs.Count; i++)
            {
                ESInputConfig config = configs[i];
                string path = AssetDatabase.GetAssetPath(config);
                if (string.IsNullOrEmpty(path) || path == DefaultAssetPath)
                    continue;

                if (config != null && !candidates.Contains(config))
                    candidates.Add(config);

                AddSubAssetsAtPath(candidates, path);
            }
        }

        private static void AddSubAssetsFromFolder(List<ESInputConfig> candidates, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Object", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    AddSubAssetsAtPath(candidates, path);
            }
        }

        private static void AddSubAssetsAtPath(List<ESInputConfig> candidates, string path)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < allAssets.Length; i++)
            {
                if (allAssets[i] is ESInputConfig config && !candidates.Contains(config))
                    candidates.Add(config);
            }
        }

        private static ESInputConfig PickBest(List<ESInputConfig> candidates)
        {
            ESInputConfig best = null;
            int bestScore = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                ESInputConfig config = candidates[i];
                if (config == null)
                    continue;

                int score = config.ActionCount;
                if (config.schemes != null)
                    score += config.schemes.Count * 10;

                if (score > bestScore)
                {
                    best = config;
                    bestScore = score;
                }
            }

            return best;
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            string[] parts = folder.Split('/');
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
