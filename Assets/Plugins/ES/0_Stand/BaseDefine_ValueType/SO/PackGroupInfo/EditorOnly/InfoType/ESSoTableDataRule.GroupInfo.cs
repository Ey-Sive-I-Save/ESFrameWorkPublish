#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Group And Info Asset Resolution
        private static void AddGroupCandidates(Dictionary<string, ISoDataGroup> result, ScriptableObject asset)
        {
            if (result == null || asset == null)
                return;

            if (asset is ISoDataGroup group)
            {
                string key = GetGroupAssetKey(group);
                if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                    result[key] = group;
            }

            if (asset is ISoDataPack pack)
            {
                if (pack.CachingGroups != null)
                {
                    for (int i = 0; i < pack.CachingGroups.Count; i++)
                    {
                        ISoDataGroup cachedGroup = pack.CachingGroups[i];
                        if (cachedGroup == null)
                            continue;

                        string key = GetGroupAssetKey(cachedGroup);
                        if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                            result[key] = cachedGroup;
                    }
                }
            }
        }

        private string GetGroupKeyFromTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            if (tableColumnMap != null)
            {
                foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
                {
                    ESTableColumnNameMap column = pair.Value;
                    if (column == null)
                        continue;

                    if (!column.isGroupKey && !string.Equals(GetActiveTableColumnName(column), groupColumnName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return pair.Key < row.Count ? row[pair.Key] : string.Empty;
                }
            }

            return string.Empty;
        }

        private ISoDataGroup ResolveImportGroup(string groupKey, Dictionary<string, ISoDataGroup> groupsByKey, Type ownerType, Type groupOwnerType, List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            if (groupOwnerType == null || !typeof(ISoDataInfo).IsAssignableFrom(ownerType))
                return null;

            string effectiveKey = string.IsNullOrWhiteSpace(groupKey) ? GetActiveBatchTargetGroupKey() : groupKey;
            if (!string.IsNullOrWhiteSpace(effectiveKey) && groupsByKey != null && groupsByKey.TryGetValue(effectiveKey, out ISoDataGroup existingGroup))
                return existingGroup;

            if (groupsByKey != null && groupsByKey.Count == 1 && string.IsNullOrWhiteSpace(effectiveKey))
            {
                foreach (ISoDataGroup single in groupsByKey.Values)
                    return single;
            }

            if (!allowCreateGroupOnImport)
                return null;

            if (string.IsNullOrWhiteSpace(effectiveKey))
                return null;

            if (typeBinding == null || typeBinding.GroupType == null)
                return null;

            if (!CanCreateGroupInActiveFolder(out string folderPath))
                return null;

            ScriptableObject group = ScriptableObject.CreateInstance(typeBinding.GroupType);
            if (group == null)
                return null;

            string assetName = SanitizeAssetFileName(effectiveKey);
            group.name = assetName;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName + ".asset"));
            AssetDatabase.CreateAsset(group, assetPath);
            EditorUtility.SetDirty(group);

            ISoDataGroup createdGroup = group as ISoDataGroup;
            if (createdGroup != null)
            {
                if (groupsByKey != null)
                    groupsByKey[effectiveKey] = createdGroup;

                Debug.Log("SO Table import created group asset: " + assetPath, group);
                return createdGroup;
            }

            AssetDatabase.DeleteAsset(assetPath);
            return null;
        }

        private ScriptableObject ResolveInfoOwnerInGroup(ISoDataGroup targetGroup, string infoKey, out bool createdInfo, out string reason)
        {
            createdInfo = false;
            reason = string.Empty;

            if (targetGroup == null)
            {
                reason = "Group 目标为空。";
                return null;
            }

            if (string.IsNullOrWhiteSpace(infoKey))
            {
                reason = "Info Key 为空，无法在 Group 中定位或创建。";
                return null;
            }

            ISoDataInfo existing = targetGroup.GetInfoByKey(infoKey);
            if (existing is ScriptableObject existingObject)
                return existingObject;

            if (!allowCreateInfoOnImport)
            {
                reason = "当前规则不允许在导入时创建 Info。";
                return null;
            }

            Type infoType = targetGroup.GetSOInfoType();
            if (infoType == null || !typeof(ScriptableObject).IsAssignableFrom(infoType))
            {
                reason = "Group 未提供可创建的 Info 类型。";
                return null;
            }

            if (!CanWriteSubAssetToGroup(targetGroup, out string groupAssetPath))
            {
                reason = "无法写入 Group 子资产。";
                return null;
            }

            ScriptableObject infoAsset = ScriptableObject.CreateInstance(infoType);
            if (infoAsset == null)
            {
                reason = "创建 Info 实例失败。";
                return null;
            }

            infoAsset.name = SanitizeAssetFileName(infoKey);
            if (infoAsset is ISoDataInfo info)
                info.SetKey(infoKey);
            if (infoAsset is IString stringKey)
                stringKey.SetSTR(infoKey);

            Undo.RecordObject(targetGroup as ScriptableObject, "SO Table Create Group Info");
            AssetDatabase.AddObjectToAsset(infoAsset, groupAssetPath);
            targetGroup._TryAddInfoToDic(infoKey, infoAsset);
            EditorUtility.SetDirty(targetGroup as ScriptableObject);
            AssetDatabase.ImportAsset(groupAssetPath, ImportAssetOptions.ForceUpdate);
            createdInfo = true;
            return infoAsset;
        }

        private bool TryDeleteInfoFromGroup(ISoDataGroup targetGroup, string infoKey, out string reason)
        {
            reason = string.Empty;
            if (targetGroup == null)
            {
                reason = "Group 为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(infoKey))
            {
                reason = "Info Key 为空，无法删除。";
                return false;
            }

            ISoDataInfo info = targetGroup.GetInfoByKey(infoKey);
            ScriptableObject infoAsset = info as ScriptableObject;
            if (infoAsset == null)
            {
                reason = "未找到可删除的 Info 资产。";
                return false;
            }

            string groupAssetPath = AssetDatabase.GetAssetPath(targetGroup as ScriptableObject);
            if (!string.IsNullOrEmpty(groupAssetPath))
                Undo.RecordObject(targetGroup as ScriptableObject, "SO Table Delete Group Info");

            targetGroup._RemoveInfoFromDic(infoKey);
            Undo.DestroyObjectImmediate(infoAsset);
            if (!string.IsNullOrEmpty(groupAssetPath))
                AssetDatabase.ImportAsset(groupAssetPath, ImportAssetOptions.ForceUpdate);

            return true;
        }

        private bool TryDeleteGroupAsset(ISoDataGroup targetGroup, out string reason)
        {
            reason = string.Empty;
            if (targetGroup == null)
            {
                reason = "Group 为空。";
                return false;
            }

            ScriptableObject groupAsset = targetGroup as ScriptableObject;
            if (groupAsset == null)
            {
                reason = "Group 不是可删除的 ScriptableObject。";
                return false;
            }

            string groupAssetPath = AssetDatabase.GetAssetPath(groupAsset);
            if (string.IsNullOrWhiteSpace(groupAssetPath))
            {
                reason = "Group 没有有效资源路径。";
                return false;
            }

            return AssetDatabase.DeleteAsset(groupAssetPath);
        }

        private void RefreshPackAfterGroupMutation(ISoDataGroup targetGroup)
        {
            if (targetGroup == null)
                return;

            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return;

            HashSet<ISoDataPack> packs = new HashSet<ISoDataPack>();
            CollectPacksForRefresh(source.soAsset, packs);
            if (source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                    CollectPacksForRefresh(source.folderAssets[i], packs);
            }

            foreach (ISoDataPack pack in packs)
            {
                if (pack == null)
                    continue;

                if (pack is ScriptableObject packAsset)
                    Undo.RecordObject(packAsset, "SO Table Refresh Pack");

                pack._AddInfosFromGroup(targetGroup);
                if (pack is ScriptableObject dirtyPack)
                    EditorUtility.SetDirty(dirtyPack);
            }
        }

        private void RefreshPackAfterGroupDeletion(ISoDataGroup deletedGroup)
        {
            if (deletedGroup == null)
                return;

            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return;

            HashSet<ISoDataPack> packs = new HashSet<ISoDataPack>();
            CollectPacksForRefresh(source.soAsset, packs);
            if (source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                    CollectPacksForRefresh(source.folderAssets[i], packs);
            }

            foreach (ISoDataPack pack in packs)
            {
                if (pack == null)
                    continue;

                List<ISoDataGroup> cachedGroups = pack.CachingGroups;
                if (cachedGroups != null)
                    cachedGroups.RemoveAll(g => g == null || ReferenceEquals(g, deletedGroup));

                pack.Check();
                if (pack is ScriptableObject dirtyPack)
                    EditorUtility.SetDirty(dirtyPack);
            }
        }

        private static void CollectPacksForRefresh(UnityEngine.Object asset, HashSet<ISoDataPack> result)
        {
            if (asset == null || result == null)
                return;

            if (asset is ISoDataPack pack)
                result.Add(pack);
        }

        private bool CanCreateGroupInActiveFolder(out string folderPath)
        {
            folderPath = null;
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return false;

            if (source.soFolder != null)
                folderPath = AssetDatabase.GetAssetPath(source.soFolder);
            else if (source.soAsset != null)
                folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.soAsset));

            return !string.IsNullOrWhiteSpace(folderPath) && AssetDatabase.IsValidFolder(folderPath);
        }

        private string GetActiveBatchTargetGroupKey()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.targetGroupKey : string.Empty;
        }

        private static string GetGroupAssetKey(ISoDataGroup group)
        {
            if (group == null)
                return string.Empty;

            if (group is ScriptableObject so)
                return so.name;

            return group.FileName;
        }

        private static bool CanWriteSubAssetToGroup(ISoDataGroup group, out string groupAssetPath)
        {
            groupAssetPath = null;
            if (group == null)
                return false;

            groupAssetPath = AssetDatabase.GetAssetPath(group as ScriptableObject);
            return !string.IsNullOrWhiteSpace(groupAssetPath);
        }

        private bool CanCreateOwnerInActiveFolder(Type ownerType, out string folderPath)
        {
            folderPath = null;
            if (ownerType == null || ownerType.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(ownerType))
                return false;

            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null || source.soFolder == null || !source.createMissingAssetsInFolder)
                return false;

            folderPath = AssetDatabase.GetAssetPath(source.soFolder);
            return AssetDatabase.IsValidFolder(folderPath);
        }

        private string GetObjectKeyFromTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (!IsObjectKeyColumn(pair.Value))
                    continue;

                return pair.Key < row.Count ? row[pair.Key] : string.Empty;
            }

            return null;
        }

        #endregion
    }
}
#endif
