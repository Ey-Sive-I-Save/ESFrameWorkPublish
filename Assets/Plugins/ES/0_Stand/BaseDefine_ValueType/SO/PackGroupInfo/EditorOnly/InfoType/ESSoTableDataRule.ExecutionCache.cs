#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Batch Execution Cache
        [NonSerialized]
        private ESTableExecutionCache activeExecutionCache;

        private sealed class ESTableExecutionCache
        {
            public readonly Dictionary<string, List<List<string>>> tablesByPath = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<ScriptableObject>> ownersBySource = new Dictionary<string, List<ScriptableObject>>(StringComparer.Ordinal);
            public readonly Dictionary<string, Dictionary<string, ISoDataGroup>> groupsBySource = new Dictionary<string, Dictionary<string, ISoDataGroup>>(StringComparer.Ordinal);
        }

        private void BeginExecutionCache()
        {
            if (activeExecutionCache == null)
                activeExecutionCache = new ESTableExecutionCache();
        }

        private void EndExecutionCache()
        {
            activeExecutionCache = null;
        }

        private void ClearExecutionCache()
        {
            activeExecutionCache = null;
        }

        private List<List<string>> ReadTableFileCached(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ReadTableFileAuto(path);

            string fullPath = Path.GetFullPath(path);
            if (activeExecutionCache == null)
                return ReadTableFileAuto(fullPath);

            if (!activeExecutionCache.tablesByPath.TryGetValue(fullPath, out List<List<string>> table))
            {
                table = ReadTableFileAuto(fullPath);
                activeExecutionCache.tablesByPath[fullPath] = table;
            }

            return table;
        }

        private bool TryGetCachedOwners(out List<ScriptableObject> owners)
        {
            owners = null;
            if (activeExecutionCache == null)
                return false;

            string key = BuildActiveSourceCacheKey("owners");
            return activeExecutionCache.ownersBySource.TryGetValue(key, out owners);
        }

        private void SetCachedOwners(List<ScriptableObject> owners)
        {
            if (activeExecutionCache == null || owners == null)
                return;

            activeExecutionCache.ownersBySource[BuildActiveSourceCacheKey("owners")] = owners;
        }

        private bool TryGetCachedGroups(out Dictionary<string, ISoDataGroup> groups)
        {
            groups = null;
            if (activeExecutionCache == null)
                return false;

            string key = BuildActiveSourceCacheKey("groups");
            return activeExecutionCache.groupsBySource.TryGetValue(key, out groups);
        }

        private void SetCachedGroups(Dictionary<string, ISoDataGroup> groups)
        {
            if (activeExecutionCache == null || groups == null)
                return;

            activeExecutionCache.groupsBySource[BuildActiveSourceCacheKey("groups")] = groups;
        }

        private string BuildActiveSourceCacheKey(string kind)
        {
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            string ownerType = typeBinding != null && typeBinding.RowOwnerType != null ? typeBinding.RowOwnerType.AssemblyQualifiedName : string.Empty;
            if (source == null)
                return kind + "|null|" + ownerType;

            var builder = new System.Text.StringBuilder(256);
            builder.Append(kind).Append('|')
                .Append(source.sourceKind).Append('|')
                .Append(source.includeSubFolders).Append('|')
                .Append(ownerType).Append('|');

            AppendAssetCacheKey(builder, source.soAsset);
            builder.Append('|');
            AppendAssetCacheKey(builder, source.soFolder);
            builder.Append('|');
            if (source.soFolder == null && source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                {
                    if (i > 0)
                        builder.Append(';');
                    AppendAssetCacheKey(builder, source.folderAssets[i]);
                }
            }

            return builder.ToString();
        }

        private static void AppendAssetCacheKey(System.Text.StringBuilder builder, UnityEngine.Object asset)
        {
            if (builder == null || asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            builder.Append(guid).Append('@').Append(path);
        }
        #endregion
    }
}
#endif
