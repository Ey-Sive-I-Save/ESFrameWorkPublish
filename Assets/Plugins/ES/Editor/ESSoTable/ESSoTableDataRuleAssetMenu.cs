using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESSoTableDataRuleAssetMenu
    {
        private const string DefaultFolder = "Assets/ESNormalAssets/Data/GlobalData/SoTable";
        private const string DefaultAssetPath = DefaultFolder + "/ESSoTableDataRule.asset";
        private const string LegacyFolder = "Assets/ESNormalAssets/Data/Legacy/SoTableRuleGroup";

        [MenuItem("【ES】/玩法搭建/SO表格/迁移旧表格规则为独立资产", priority = 1200)]
        public static void MigrateLegacyRuleToStandaloneAsset()
        {
            ESSoTableDataRule source = FindBestRule();
            ESSoTableDataRule target = AssetDatabase.LoadAssetAtPath<ESSoTableDataRule>(DefaultAssetPath);

            if (target != null && source != null && target != source)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "确认覆盖独立 SO 表格规则",
                    "已经存在独立 SO 表格规则：\n" + DefaultAssetPath + "\n\n是否用找到的旧规则覆盖它？",
                    "覆盖",
                    "取消");
                if (!overwrite)
                    return;
            }

            EnsureFolder(DefaultFolder);

            if (target == null)
            {
                target = ScriptableObject.CreateInstance<ESSoTableDataRule>();
                target.name = "ESSoTableDataRule";
                AssetDatabase.CreateAsset(target, DefaultAssetPath);
            }

            if (source != null && source != target)
            {
                EditorUtility.CopySerialized(source, target);
                target.name = "ESSoTableDataRule";
                EditorUtility.SetDirty(target);
                Debug.Log("已提取旧 SO 表格规则为独立资产：" + DefaultAssetPath, target);
            }
            else
            {
                EditorUtility.SetDirty(target);
                Debug.Log("未找到旧 SO 表格规则，已创建空的独立规则资产：" + DefaultAssetPath, target);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        [MenuItem("【ES】/玩法搭建/SO表格/定位独立表格规则", priority = 1201)]
        public static void PingStandaloneRule()
        {
            ESSoTableDataRule rule = AssetDatabase.LoadAssetAtPath<ESSoTableDataRule>(DefaultAssetPath);
            if (rule == null)
            {
                if (EditorUtility.DisplayDialog(
                        "没有找到独立 SO 表格规则",
                        "默认路径还没有 ESSoTableDataRule.asset：\n" + DefaultAssetPath + "\n\n是否现在创建或迁移？",
                        "创建或迁移",
                        "取消"))
                {
                    MigrateLegacyRuleToStandaloneAsset();
                }

                return;
            }

            Selection.activeObject = rule;
            EditorGUIUtility.PingObject(rule);
        }

        private static ESSoTableDataRule FindBestRule()
        {
            var candidates = new List<ESSoTableDataRule>();
            AddTypedAssets(candidates);
            AddSubAssetsFromFolder(candidates, LegacyFolder);
            return PickBest(candidates);
        }

        private static void AddTypedAssets(List<ESSoTableDataRule> candidates)
        {
            List<ESSoTableDataRule> rules = ESEditorSO.SOS.GetNewGroupOfType<ESSoTableDataRule>() ?? new List<ESSoTableDataRule>(0);
            for (int i = 0; i < rules.Count; i++)
            {
                ESSoTableDataRule rule = rules[i];
                string path = AssetDatabase.GetAssetPath(rule);
                if (string.IsNullOrEmpty(path) || path == DefaultAssetPath)
                    continue;

                if (rule != null && !candidates.Contains(rule))
                    candidates.Add(rule);

                AddSubAssetsAtPath(candidates, path);
            }
        }

        private static void AddSubAssetsFromFolder(List<ESSoTableDataRule> candidates, string folder)
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

        private static void AddSubAssetsAtPath(List<ESSoTableDataRule> candidates, string path)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < allAssets.Length; i++)
            {
                if (allAssets[i] is ESSoTableDataRule rule && !candidates.Contains(rule))
                    candidates.Add(rule);
            }
        }

        private static ESSoTableDataRule PickBest(List<ESSoTableDataRule> candidates)
        {
            ESSoTableDataRule best = null;
            int bestScore = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                ESSoTableDataRule rule = candidates[i];
                if (rule == null)
                    continue;

                int score = 0;
                if (!string.IsNullOrEmpty(rule.ruleKey))
                    score += 100;
                if (rule.useBatches != null)
                    score += rule.useBatches.Count * 10;
                if (rule.columns != null)
                    score += rule.columns.Count;

                if (score > bestScore)
                {
                    best = rule;
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
