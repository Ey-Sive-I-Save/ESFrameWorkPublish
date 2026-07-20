#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using ES.Internal;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Binding And Column Build Entry Points
        public void BindAndGenerateFromSoAsset()
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null || buildSource.soAsset == null)
            {
                Debug.LogWarning("构建阶段未指定单个 SO 样本。", this);
                return;
            }

            BindAndGenerate(buildSource.soAsset.GetType(), ESSoTableRuleBindSourceKind.SoAsset, buildSource.soAsset);
        }

        [PropertyOrder(11)]
        public void BindAndGenerateFromMonoScript()
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null || buildSource.monoScript == null)
            {
                Debug.LogWarning("构建阶段未指定脚本类型。", this);
                return;
            }

            Type scriptType = buildSource.monoScript.GetClass();
            if (scriptType == null)
            {
                Debug.LogWarning("脚本没有可绑定的类型。", this);
                return;
            }

            BindAndGenerate(scriptType, ESSoTableRuleBindSourceKind.MonoScript, buildSource.monoScript);
        }

        [PropertyOrder(11)]
        public void BindAndGenerateFromFolder()
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null || buildSource.soFolder == null)
            {
                Debug.LogWarning("构建阶段未指定文件夹。当前界面建议把文件夹放到使用阶段批次。", this);
                return;
            }

            List<ScriptableObject> assets = CollectScriptableObjectsFromFolder(buildSource.soFolder, buildSource.includeSubFolders);
            buildSource.folderAssets.Clear();
            buildSource.folderAssets.AddRange(assets);

            if (assets.Count == 0)
            {
                Debug.LogWarning("文件夹里没有可用的 ScriptableObject。", this);
                EditorUtility.SetDirty(this);
                return;
            }

            Type sourceType = buildSource.monoScript != null ? buildSource.monoScript.GetClass() : assets[0].GetType();
            if (sourceType == null)
            {
                Debug.LogWarning("无法解析绑定类型。", this);
                return;
            }

            int matchedCount = CountAssignableAssets(assets, sourceType);
            if (matchedCount != assets.Count)
                Debug.LogWarning("文件夹内存在类型不匹配的 SO，构建规则时只会按匹配类型处理。", this);

            BindAndGenerate(sourceType, ESSoTableRuleBindSourceKind.SoFolder, buildSource.soFolder);
        }

        [PropertyOrder(11)]
        public void BindAndGenerateFromSelection()
        {
            UnityEngine.Object active = Selection.activeObject;
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null)
                return;

            if (active is MonoScript script)
            {
                buildSource.monoScript = script;
                BindAndGenerateFromMonoScript();
                return;
            }

            if (active is ScriptableObject so)
            {
                buildSource.soAsset = so;
                BindAndGenerateFromSoAsset();
                return;
            }

            Debug.LogWarning("\u89c4\u5219\u6784\u5efa\u9636\u6bb5\u53ea\u63a5\u53d7\u5355\u4e2a SO \u6216 MonoScript\u3002\u6587\u4ef6\u5939\u8bf7\u653e\u5230\u4f7f\u7528\u9636\u6bb5\u7684\u6279\u6b21\u91cc\u3002", this);
        }
        public bool IsValidForSoData(out string reason)
        {
            reason = string.Empty;

            if (!TryGetTargetTypes(out Type packType, out Type groupType, out Type infoType))
            {
                reason = "Invalid rule.";
                return false;
            }

            if (!typeof(ISoDataPack).IsAssignableFrom(packType))
            {
                reason = "Invalid rule.";
                return false;
            }

            if (!typeof(ISoDataGroup).IsAssignableFrom(groupType))
            {
                reason = "Invalid rule.";
                return false;
            }

            if (!typeof(ISoDataInfo).IsAssignableFrom(infoType))
            {
                reason = "Invalid rule.";
                return false;
            }

            return true;
        }

        private void BindAndGenerate(Type sourceType, ESSoTableRuleBindSourceKind sourceKind, UnityEngine.Object sourceObject)
        {
            if (sourceType == null)
                return;

            Undo.RecordObject(this, "绑定 SO 表格 Rule");

            if (ESSoTableRuleTypeUtility.TryResolveSoDataTypes(sourceType, out Type packType, out Type groupType, out Type infoType, out string reason))
            {
                CaptureBuildSource(sourceKind, sourceObject, sourceType);
                typeBinding.objectKind = ESSoTableRuleObjectKind.SoData;
                typeBinding.objectTypeName = string.Empty;
                typeBinding.packTypeName = packType.FullName;
                typeBinding.groupTypeName = groupType.FullName;
                typeBinding.infoTypeName = infoType.FullName;

                FillDefaultNames(infoType);
                RebuildColumnsFromInfoFields();

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log("SO 表格规则 类型解析完成：Pack=" + packType.Name + "，Group=" + groupType.Name + "，Info=" + infoType.Name, this);
                return;
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(sourceType))
            {
                Debug.LogWarning(reason, this);
                return;
            }

            CaptureBuildSource(sourceKind, sourceObject, sourceType);
            typeBinding.objectKind = ESSoTableRuleObjectKind.ScriptableObject;
            typeBinding.objectTypeName = sourceType.FullName;
            typeBinding.packTypeName = string.Empty;
            typeBinding.groupTypeName = string.Empty;
            typeBinding.infoTypeName = string.Empty;

            FillDefaultNames(sourceType);
            RebuildColumnsFromInfoFields();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log("SO 表格规则 普通 ScriptableObject 类型解析完成：" + sourceType.Name, this);
        }

        private void CaptureBuildSource(ESSoTableRuleBindSourceKind sourceKind, UnityEngine.Object sourceObject, Type sourceType)
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null)
                return;

            buildSource.Capture(sourceKind, sourceObject, sourceType);
        }

        private static List<ScriptableObject> CollectScriptableObjectsFromFolder(DefaultAsset folder, bool includeSubFolders)
        {
            var assets = new List<ScriptableObject>();
            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return assets;

            string[] folders = includeSubFolders ? new[] { folderPath } : GetDirectFolderOnly(folderPath);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!includeSubFolders && System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') != folderPath)
                    continue;

                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets;
        }

        private static string[] GetDirectFolderOnly(string folderPath)
        {
            return new[] { folderPath };
        }

        private static int CountAssignableAssets(List<ScriptableObject> assets, Type sourceType)
        {
            int count = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null && sourceType.IsAssignableFrom(assets[i].GetType()))
                    count++;
            }

            return count;
        }

        private void FillDefaultNames(Type infoType)
        {
            if (infoType == null)
                return;

            string shortName = infoType.Name;
            if (string.IsNullOrEmpty(ruleKey))
                ruleKey = shortName;
            if (string.IsNullOrEmpty(tableName))
                tableName = "Tb" + shortName;
            if (string.IsNullOrEmpty(beanName))
                beanName = shortName;
        }

        public void RebuildColumnsFromInfoFields()
        {
            Type rowOwnerType = typeBinding.RowOwnerType;
            if (rowOwnerType == null)
                return;

            List<ESTableColumnNameMap> lockedColumns = CollectLockedColumns();
            columns.Clear();
            if (typeof(ISoDataInfo).IsAssignableFrom(rowOwnerType))
            {
                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = nameof(SoDataInfo.KeyName),
                    columnName = infoKeyColumnName,
                    displayName = "Info Key",
                    tableType = "string",
                    isInfoKey = true
                });
            }

            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                Type elementType = GetConfiguredListElementType(rowOwnerType);
                if (elementType == null)
                    return;

                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = BuildListElementPath(rowBinding.elementKeyFieldPath),
                    columnName = rowBinding.rowKeyColumnName,
                    displayName = "行 Key",
                    tableType = "string"
                });

                RebuildColumnsFromTypeFields(elementType, true);
                RestoreLockedColumns(lockedColumns);
                return;
            }

            RebuildColumnsFromTypeFields(rowOwnerType, false);
            RestoreLockedColumns(lockedColumns);
        }

        [FoldoutGroup("", Expanded = false)]
        public void PrewarmReflectionCache()
        {
            Type rowOwnerType = typeBinding.RowOwnerType;
            if (rowOwnerType == null || columns == null)
                return;

            Type listElementType = null;
            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                listElementType = GetConfiguredListElementType(rowOwnerType);
                ESRowBindingReflectionUtility.GetOrCreateMemberPath(rowOwnerType, rowBinding.listFieldPath);
            }

            for (int i = 0; i < columns.Count; i++)
            {
                ESTableColumnNameMap column = columns[i];
                if (column == null || !column.IsUsable || string.IsNullOrWhiteSpace(column.soFieldPath))
                    continue;

                if (TryGetReflectionPathForColumn(column.soFieldPath, rowOwnerType, listElementType, out Type ownerType, out string memberPath))
                    ESRowBindingReflectionUtility.GetOrCreateMemberPath(ownerType, memberPath);
            }
        }

        public void RebuildColumnsFromBuildTable()
        {
            if (buildStage == null || string.IsNullOrWhiteSpace(buildStage.tableFilePath))
            {
                Debug.LogWarning("构建阶段未指定表格样本路径。", this);
                return;
            }

            string path = buildStage.tableFilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("构建表格不存在：" + path, this);
                return;
            }

            List<List<string>> table = ReadTableFileAuto(path);
            if (table.Count == 0)
            {
                Debug.LogWarning("构建表格没有可读取的表头。", this);
                return;
            }

            Undo.RecordObject(this, "从表格表头构建 SO 表格规则");
            List<ESTableColumnNameMap> lockedColumns = CollectLockedColumns();
            columns.Clear();

            List<string> varRow = table[0];
            List<string> typeRow = table.Count > 1 ? table[1] : null;
            List<string> commentRow = table.Count > 3 ? table[3] : null;
            int start = varRow.Count > 0 && IsHeaderMark(varRow[0]) ? 1 : 0;
            for (int i = start; i < varRow.Count; i++)
            {
                string columnName = varRow[i];
                if (string.IsNullOrWhiteSpace(columnName))
                    continue;

                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = columnName,
                    columnName = columnName,
                    displayName = columnName,
                    tableType = typeRow != null && i < typeRow.Count && !string.IsNullOrWhiteSpace(typeRow[i]) ? typeRow[i] : "string",
                    comment = commentRow != null && i < commentRow.Count ? commentRow[i] : string.Empty
                });
            }
            RestoreLockedColumns(lockedColumns);

            if (string.IsNullOrEmpty(ruleKey))
                ruleKey = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(tableName))
                tableName = "Tb" + Path.GetFileNameWithoutExtension(path);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private static bool IsHeaderMark(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("##", StringComparison.Ordinal);
        }

        #endregion
    }
}
#endif
