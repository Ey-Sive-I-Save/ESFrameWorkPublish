#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [ESCreatePath("数据信息", "SO表格规则数据信息")]
    public partial class ESSoTableDataRule : SoDataInfo
    {
        #region Serialized State And Inspector Configuration
        private static readonly ESReflectionRowBridge RowBridge = new ESReflectionRowBridge();
        private static readonly JsonSerializerSettings CellJsonSettings = new JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.None,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };
        [NonSerialized]
        private ESSoTableRuleUseBatch activeUseBatch;
        [NonSerialized]
        private string activeImportTablePath;
        [NonSerialized]
        private bool suppressImportRiskConfirmation;

        [TitleGroup("")]
        [HorizontalGroup("", Width = 80)]
        [LabelText("")]
        public bool enabled = true;

        [HorizontalGroup("")]
        [LabelText("规则 Key")]
        public string ruleKey;

        [TitleGroup("")]
        [LabelText("")]
        [TextArea(2, 5)]
        public string description;

        [LabelText("Debug 行号")]
        [PropertyTooltip("用于行级 Debug 的表格行号，按 Excel 行号填写。表头第 1 行就是 1。")]
        public int debugRowNumber = 5;

        [FoldoutGroup("", Expanded = false)]
        [HideLabel]
        public ESSoTableRuleTypeBinding typeBinding = new ESSoTableRuleTypeBinding();

        [HideLabel]
        [PropertyOrder(8)]
        public ESSoTableRuleBuildStage buildStage = new ESSoTableRuleBuildStage();

        [LabelText("")]
        [PropertyOrder(12)]
        public List<ESSoTableRuleUseBatch> useBatches = new List<ESSoTableRuleUseBatch>();

        [FoldoutGroup("", Expanded = false)]
        [ReadOnly]
        [LabelText("")]
        public string tableName;

        [FoldoutGroup("")]
        [ReadOnly]
        [LabelText("")]
        public string beanName;

        [FoldoutGroup("Pack / Group / Info", Expanded = false)]
        [LabelText("Info 展开方式")]
        public ESTableInfoExpandMode infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;

        [FoldoutGroup("Pack / Group / Info")]
        [LabelText("Group 截取方式")]
        public ESTableGroupSliceMode groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;

        [FoldoutGroup("Pack / Group / Info")]
        [LabelText("")]
        public string packColumnName = "pack";

        [FoldoutGroup("Pack / Group / Info")]
        [LabelText("")]
        public string groupColumnName = "group";

        [FoldoutGroup("Pack / Group / Info")]
        [LabelText("")]
        public string infoKeyColumnName = "key";

        [FoldoutGroup("Pack / Group / Info")]
        [LabelText("")]
        public ESTableNameMatchMode nameMatchMode = ESTableNameMatchMode.Exact;

        [FoldoutGroup("", Expanded = false)]
        [InfoBox("")]
        [HideLabel]
        public ESTableRowBindingRule rowBinding = new ESTableRowBindingRule();

        [FoldoutGroup("", Expanded = false)]
        [InfoBox("")]
        [HideLabel]
        public ESTableNestedFieldRule nestedFieldRule = new ESTableNestedFieldRule();

        [FoldoutGroup("", Expanded = false)]
        [HideLabel]
        public ESTableHeaderLayout header = new ESTableHeaderLayout();

        [TitleGroup("")]
        [LabelText("")]
        [InfoBox("")]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = true, DrawScrollView = true, MinScrollViewHeight = 180)]
        public List<ESTableColumnNameMap> columns = new List<ESTableColumnNameMap>();

        [FoldoutGroup("导入导出策略")]
        [LabelText("导入时允许创建 Info")]
        public bool allowCreateInfoOnImport = true;

        [FoldoutGroup("导入导出策略")]
        [LabelText("导入时允许创建 Group")]
        public bool allowCreateGroupOnImport = true;

        [FoldoutGroup("导入导出策略")]
        [LabelText("导出前刷新 Pack 缓存")]
        public bool refreshPackBeforeExport = true;

        private ESSoTableRuleSourceBinding BuildSourceBinding => buildStage != null ? buildStage.sourceBinding : null;

        #endregion

        #region Type Binding And Column Compilation
        public bool TryGetTargetTypes(out Type packType, out Type groupType, out Type infoType)
        {
            packType = typeBinding.PackType;
            groupType = typeBinding.GroupType;
            infoType = typeBinding.InfoType;
            return packType != null && groupType != null && infoType != null;
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [PropertyOrder(11)]
        [Button("SO绑定生成")]
        private Dictionary<string, ESTableColumnNameMap> BuildColumnMap(List<ESTableColumnNameMap> enabledColumns)
        {
            var map = new Dictionary<string, ESTableColumnNameMap>();
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap column = enabledColumns[i];
                string activeName = GetActiveTableColumnName(column);
                if (!string.IsNullOrEmpty(activeName))
                    map[activeName] = column;
            }

            return map;
        }

        private static Dictionary<int, ESTableColumnNameMap> BuildTableColumnMap(List<List<string>> table, Dictionary<string, ESTableColumnNameMap> columnMap)
        {
            var result = new Dictionary<int, ESTableColumnNameMap>();
            if (table == null || table.Count == 0)
                return result;

            List<string> varRow = table[0];
            if (varRow == null)
                return result;

            for (int i = 1; i < varRow.Count; i++)
            {
                string columnName = varRow[i];
                if (!string.IsNullOrEmpty(columnName) && columnMap.TryGetValue(columnName, out ESTableColumnNameMap column))
                    result[i] = column;
            }

            return result;
        }

        private List<ESTableCompiledColumn> CompileColumns(List<ESTableColumnNameMap> enabledColumns, Type ownerType, Type listElementType, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            var result = new List<ESTableCompiledColumn>(enabledColumns.Count);
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap map = enabledColumns[i];
                int tableColumnIndex = tableColumnMap == null ? i + 1 : FindTableColumnIndex(tableColumnMap, map);
                if (tableColumnMap != null && tableColumnIndex < 0)
                    continue;

                var compiled = new ESTableCompiledColumn
                {
                    tableColumnIndex = tableColumnIndex,
                    map = map,
                    canRead = map.direction != ESSoTableRuleDirection.TableToSoOnly && map.direction != ESSoTableRuleDirection.Ignore,
                    canWrite = map.direction != ESSoTableRuleDirection.SoToTableOnly && map.direction != ESSoTableRuleDirection.Ignore && !map.isInfoKey && !map.isGroupKey && !map.locked
                };

                if (TryGetReflectionPathForColumn(map.soFieldPath, ownerType, listElementType, out Type targetType, out string memberPath))
                {
                    compiled.ownerType = targetType;
                    compiled.memberPath = memberPath;
                    compiled.useRowObject = targetType == listElementType;
                    compiled.valueType = ESRowBindingReflectionUtility.GetMemberType(targetType, memberPath);
                    ESRowBindingReflectionUtility.GetOrCreateMemberPath(targetType, memberPath);
                }

                if (map.isInfoKey)
                    compiled.canRead = true;
                if (compiled.valueType == null && !map.isInfoKey)
                {
                    compiled.canRead = false;
                    compiled.canWrite = false;
                }

                result.Add(compiled);
            }

            return result;
        }

        private static int FindTableColumnIndex(Dictionary<int, ESTableColumnNameMap> tableColumnMap, ESTableColumnNameMap map)
        {
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (ReferenceEquals(pair.Value, map))
                    return pair.Key;
            }

            return -1;
        }

        private int FindTableColumnIndex(Dictionary<int, ESTableColumnNameMap> tableColumnMap, string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return -1;

            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (GetActiveTableColumnName(pair.Value) == columnName)
                    return pair.Key;
            }

            return -1;
        }

        private Type GetConfiguredListElementType(Type ownerType)
        {
            if (ownerType == null || rowBinding == null || !rowBinding.IsListElementRow)
                return null;

            Type listType = ESRowBindingReflectionUtility.GetMemberType(ownerType, rowBinding.listFieldPath);
            Type listElementType = ESRowBindingReflectionUtility.GetListElementType(listType);
            if (listElementType != null)
                return listElementType;

            return ESRowBindingReflectionUtility.GetDictionaryValueType(listType);
        }

        private Dictionary<string, ScriptableObject> BuildOwnersByKey(List<ScriptableObject> owners, List<ESTableColumnNameMap> enabledColumns)
        {
            var result = new Dictionary<string, ScriptableObject>();
            ESTableColumnNameMap objectKeyColumn = FindObjectKeyColumn(enabledColumns);
            ESTableCompiledColumn compiledKeyColumn = null;
            Type ownerType = typeBinding.RowOwnerType;
            if (objectKeyColumn != null)
            {
                List<ESTableCompiledColumn> compiled = CompileColumns(new List<ESTableColumnNameMap> { objectKeyColumn }, ownerType, GetConfiguredListElementType(ownerType), null);
                if (compiled.Count > 0)
                    compiledKeyColumn = compiled[0];
            }

            for (int i = 0; i < owners.Count; i++)
            {
                ScriptableObject owner = owners[i];
                if (owner == null)
                    continue;

                string key = null;
                if (owner is ISoDataInfo info)
                    key = info.GetKey();
                else if (compiledKeyColumn != null)
                    key = ConvertCellValue(GetColumnValue(owner, owner, compiledKeyColumn), compiledKeyColumn.map.valueWriteMode);

                if (!string.IsNullOrEmpty(key))
                    result[key] = owner;
            }

            return result;
        }

        private ESTableColumnNameMap FindObjectKeyColumn(List<ESTableColumnNameMap> enabledColumns)
        {
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap column = enabledColumns[i];
                if (IsObjectKeyColumn(column))
                    return column;
            }

            return null;
        }

        private ScriptableObject FindOwnerForTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap, Dictionary<string, ScriptableObject> ownersByKey, List<ScriptableObject> owners, ref int ownerCursor, bool tableHasObjectKey)
        {
            string key = null;
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                ESTableColumnNameMap column = pair.Value;
                if (!IsObjectKeyColumn(column))
                    continue;

                key = pair.Key < row.Count ? row[pair.Key] : string.Empty;
                if (!string.IsNullOrEmpty(key))
                    break;
            }

            if (!string.IsNullOrEmpty(key) && ownersByKey.TryGetValue(key, out ScriptableObject keyedOwner))
                return keyedOwner;

            if (tableHasObjectKey)
                return null;

            if (ownerCursor < owners.Count)
                return owners[ownerCursor++];

            return null;
        }

        private bool HasObjectKeyColumn(Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (IsObjectKeyColumn(pair.Value))
                    return true;
            }

            return false;
        }

        private ScriptableObject TryCreateOwnerForTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap, Type ownerType, List<ScriptableObject> owners, Dictionary<string, ScriptableObject> ownersByKey)
        {
            if (!CanCreateOwnerInActiveFolder(ownerType, out string folderPath))
                return null;

            string key = GetObjectKeyFromTableRow(row, tableColumnMap);
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("SO Table import skipped row: object key is empty, cannot create owner asset.", this);
                return null;
            }

            string assetName = SanitizeAssetFileName(key);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName + ".asset"));
            var owner = ScriptableObject.CreateInstance(ownerType);
            owner.name = assetName;
            if (owner is ISoDataInfo info)
                info.SetKey(key);
            if (owner is IString stringKey)
                stringKey.SetSTR(key);

            AssetDatabase.CreateAsset(owner, assetPath);
            owners.Add(owner);
            ownersByKey[key] = owner;

            Debug.Log("SO Table import created owner asset: " + assetPath, owner);
            return owner;
        }

        private Dictionary<string, ISoDataGroup> BuildGroupsByKey()
        {
            if (TryGetCachedGroups(out Dictionary<string, ISoDataGroup> cachedGroups))
                return cachedGroups;

            var result = new Dictionary<string, ISoDataGroup>(StringComparer.OrdinalIgnoreCase);
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return result;

            AddGroupCandidates(result, source.soAsset);

            if (source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                    AddGroupCandidates(result, source.folderAssets[i]);
            }

            SetCachedGroups(result);
            return result;
        }

        private bool IsObjectKeyColumn(ESTableColumnNameMap column)
        {
            if (column == null || IsSerialChildRowKeyColumn(column))
                return false;
            if (column.isInfoKey)
                return true;

            return IsLikelyObjectKeyName(column.columnName) || IsLikelyObjectKeyName(column.soFieldPath);
        }

        private bool IsSerialChildRowKeyColumn(ESTableColumnNameMap column)
        {
            if (column == null || rowBinding == null || !rowBinding.IsListElementRow)
                return false;

            string rowKeyPath = BuildListElementPath(rowBinding.elementKeyFieldPath);
            if (!string.IsNullOrWhiteSpace(column.soFieldPath) && string.Equals(column.soFieldPath, rowKeyPath, StringComparison.Ordinal))
                return true;

            return !string.IsNullOrWhiteSpace(column.columnName)
                && string.Equals(column.columnName, rowBinding.rowKeyColumnName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(column.soFieldPath)
                && column.soFieldPath.StartsWith(rowBinding.listFieldPath + "[].", StringComparison.Ordinal);
        }

        private static bool IsLikelyObjectKeyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).Trim();
            return normalized.Equals("id", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("key", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("itemid", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("keyname", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("rulekey", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeAssetFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "ImportedSO" : value.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
                name = name.Replace(invalidChars[i], '_');

            return string.IsNullOrWhiteSpace(name) ? "ImportedSO" : name;
        }

        private static string GetProjectRelativePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalizedFullPath = Path.GetFullPath(fullPath);
            if (normalizedFullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedFullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalizedFullPath;
        }

        #endregion

        #region Active Batch And Path Context
        private string GetOutputFolder(string relativeFolder)
        {
            string root = string.IsNullOrEmpty(GetActiveOutputRoot()) ? "SoTableConfig/Tables" : GetActiveOutputRoot();
            string folder = string.IsNullOrEmpty(relativeFolder) ? string.Empty : relativeFolder;
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", root, folder));
        }

        private string ResolveExistingInputPath()
        {
            if (!string.IsNullOrEmpty(activeImportTablePath) && File.Exists(activeImportTablePath))
                return activeImportTablePath;

            ESTableFileKind currentFileKind = GetActiveFileKind();
            string csvPath = GetOutputPath(GetActiveCsvRelativePath(), ".csv");
            string xlsxPath = GetOutputPath(GetActiveXlsxRelativePath(), ".xlsx");

            bool csvExists = File.Exists(csvPath);
            bool xlsxExists = File.Exists(xlsxPath);

            if (currentFileKind == ESTableFileKind.Csv)
                return csvExists ? csvPath : null;
            if (currentFileKind == ESTableFileKind.Xlsx)
                return xlsxExists ? xlsxPath : null;

            if (csvExists && xlsxExists)
                return File.GetLastWriteTimeUtc(csvPath) >= File.GetLastWriteTimeUtc(xlsxPath) ? csvPath : xlsxPath;
            if (csvExists)
                return csvPath;
            if (xlsxExists)
                return xlsxPath;

            return null;
        }

        private ESSoTableRuleUseBatch GetActiveUseBatch()
        {
            if (activeUseBatch != null)
                return activeUseBatch;

            if (useBatches == null || useBatches.Count == 0)
                return null;

            for (int i = 0; i < useBatches.Count; i++)
            {
                if (useBatches[i] != null && useBatches[i].enabled)
                    return useBatches[i];
            }

            return useBatches[0];
        }

        private ESSoTableRuleSourceBinding GetActiveUseSourceBinding()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.sourceBinding : null;
        }

        private string GetActiveTableColumnName(ESTableColumnNameMap column)
        {
            if (column == null)
                return string.Empty;

            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            if (batch != null && batch.columnNameMode == ESTableColumnNameMode.Chinese)
                return string.IsNullOrWhiteSpace(column.displayName) ? column.columnName : column.displayName;

            return string.IsNullOrWhiteSpace(column.columnName) ? column.displayName : column.columnName;
        }

        private string GetActiveInfoKeyColumnName()
        {
            if (columns != null)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    ESTableColumnNameMap column = columns[i];
                    if (column != null && column.isInfoKey)
                        return GetActiveTableColumnName(column);
                }
            }

            return infoKeyColumnName;
        }

        private ESTableFileKind GetActiveFileKind()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.fileKind : ESTableFileKind.CsvAndXlsx;
        }

        private string GetActiveFileName()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.fileName) ? batch.fileName : FirstConfiguredName();
        }

        private string GetActiveSheetName()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.sheetName) ? batch.sheetName : FirstConfiguredName();
        }

        private string GetActiveOutputRoot()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.outputRoot) ? batch.outputRoot : "SoTableConfig/Tables";
        }

        private string GetActiveCsvRelativePath()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.csvRelativePath) ? batch.csvRelativePath : "csv";
        }

        private string GetActiveXlsxRelativePath()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.xlsxRelativePath) ? batch.xlsxRelativePath : "xlsx";
        }

    }
        #endregion

}
#endif
