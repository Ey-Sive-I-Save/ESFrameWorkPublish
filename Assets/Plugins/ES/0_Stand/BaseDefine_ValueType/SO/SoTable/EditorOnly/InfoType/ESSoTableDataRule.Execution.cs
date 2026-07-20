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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Unity.Profiling.ProfilerMarker ExportTableFilesMarker =
            new Unity.Profiling.ProfilerMarker("【ES】SO表格导出");
        private static readonly Unity.Profiling.ProfilerMarker ImportTableFileMarker =
            new Unity.Profiling.ProfilerMarker("【ES】SO表格导入");
#endif

        #region Batch Execution And Import Export Entry Points
        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("导出表格")]
        public void ExportTableFiles()
        {
            BeginExecutionCache();
            try
            {
                ESTablePlanRiskSummary summary = BuildBatchRiskSummary(GetActiveUseBatch());
                if (!ConfirmPlanRiskBeforeExecute(summary, GetActiveUseBatch()))
                    return;

                TryExportTableFiles();
            }
            finally
            {
                EndExecutionCache();
            }
        }

        private bool TryExportTableFiles()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (ExportTableFilesMarker.Auto())
            {
                return TryExportTableFilesCore();
            }
#else
            return TryExportTableFilesCore();
#endif
        }

        private bool TryExportTableFilesCore()
        {
            try
            {
                List<List<string>> table = BuildTableRows();
                if (table.Count == 0)
                {
                    Debug.LogWarning("No mapped columns available for export.", this);
                    return false;
                }

                ESTableFileKind currentFileKind = GetActiveFileKind();
                string csvPath = GetOutputPath(GetActiveCsvRelativePath(), ".csv");
                string xlsxPath = GetOutputPath(GetActiveXlsxRelativePath(), ".xlsx");
                List<List<string>> csvTable = null;
                List<List<string>> xlsxTable = null;

                if (currentFileKind == ESTableFileKind.Csv || currentFileKind == ESTableFileKind.CsvAndXlsx)
                {
                    csvTable = BuildExportWriteTable(table, csvPath);
                    if (csvTable == null)
                        return false;
                }

                if (currentFileKind == ESTableFileKind.Xlsx || currentFileKind == ESTableFileKind.CsvAndXlsx)
                {
                    xlsxTable = BuildExportWriteTable(table, xlsxPath);
                    if (xlsxTable == null)
                        return false;
                }

                if (csvTable != null)
                    WriteCsv(csvPath, csvTable);
                if (xlsxTable != null)
                    WriteXlsx(xlsxPath, xlsxTable, string.IsNullOrEmpty(GetActiveSheetName()) ? "Sheet1" : GetActiveSheetName(), BuildXlsxDataDropdowns(xlsxTable));

                AssetDatabase.Refresh();
                Debug.Log($"Table export complete: {csvPath} / {xlsxPath}", this);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Table export failed: " + e.Message + "\n" + e, this);
                return false;
            }
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("Auto Import")]
        public void ImportTableFileAuto()
        {
            TryImportTableFileAuto();
        }

        private bool TryImportTableFileAuto()
        {
            string path = ResolveExistingInputPath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("No CSV/XLSX file found for import.", this);
                return false;
            }

            return TryImportTableFile(path);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("Import Selected Table")]
        public void ImportTableFileByPanel()
        {
            string path = EditorUtility.OpenFilePanel("Select CSV or XLSX table", GetOutputFolder(GetActiveCsvRelativePath()), "csv,xlsx");
            if (string.IsNullOrEmpty(path))
                return;

            TryImportTableFile(path);
        }

        private bool TryImportTableFile(string path)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (ImportTableFileMarker.Auto())
            {
                return TryImportTableFileCore(path);
            }
#else
            return TryImportTableFileCore(path);
#endif
        }

        private bool TryImportTableFileCore(string path)
        {
            bool ownsCache = activeExecutionCache == null;
            if (ownsCache)
                BeginExecutionCache();

            try
            {
                int changedCount;
                activeImportTablePath = path;
                try
                {
                    List<List<string>> table = ReadTableFileCached(path);
                    if (table.Count < 5)
                    {
                        Debug.LogWarning("SO Table import stopped: table needs 4 header rows and at least 1 data row.", this);
                        return false;
                    }

                    if (!ConfirmImportRiskBeforeWrite(table, path))
                        return false;

                    changedCount = ApplyTableRowsToExistingObjects(table);
                }
                finally
                {
                    activeImportTablePath = null;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Table import complete: {path}, updated SO count: {changedCount}", this);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Table import failed: " + e.Message + "\n" + e, this);
                return false;
            }
            finally
            {
                if (ownsCache)
                    EndExecutionCache();
            }
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("Generate Empty Example")]
        public void GenerateEmptyTableExample()
        {
            List<List<string>> table = BuildEmptyExampleTableRows();
            if (table.Count == 0)
            {
                Debug.LogWarning("没有可生成案例表的字段映射。请先生成字段映射。", this);
                return;
            }

            string file = FirstConfiguredName();
            if (string.IsNullOrWhiteSpace(file))
                file = "SoTableExample";
            file = file + "_Example";

            string root = Path.Combine(Application.dataPath, "..", "SoTableConfig", "Examples");
            string csvPath = Path.GetFullPath(Path.Combine(root, "csv", file + ".csv"));
            string xlsxPath = Path.GetFullPath(Path.Combine(root, "xlsx", file + ".xlsx"));
            WriteCsv(csvPath, table);
            WriteXlsx(xlsxPath, table, string.IsNullOrEmpty(GetActiveSheetName()) ? "Sheet1" : GetActiveSheetName(), BuildXlsxDataDropdowns(table));
            AssetDatabase.Refresh();
            Debug.Log("空表案例已生成：" + csvPath + " / " + xlsxPath, this);
        }

        private List<List<string>> BuildEmptyExampleTableRows()
        {
            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            var table = new List<List<string>>();
            if (enabledColumns.Count == 0)
                return table;

            table.Add(BuildHeaderRow(header.varMark, enabledColumns, GetActiveTableColumnName));
            table.Add(BuildHeaderRow(header.typeMark, enabledColumns, c => string.IsNullOrEmpty(c.tableType) ? "string" : c.tableType));
            table.Add(BuildHeaderRow(header.groupMark, enabledColumns, c => string.IsNullOrEmpty(header.defaultGroup) ? "client" : header.defaultGroup));
            table.Add(BuildHeaderRow(header.commentMark, enabledColumns, c => string.IsNullOrEmpty(c.comment) ? c.displayName : c.comment));

            table.Add(BuildAssertRow(enabledColumns));
            table.Add(BuildRowDirectiveHelpRow(enabledColumns));

            var demoRow = new List<string> { string.Empty };
            for (int i = 0; i < enabledColumns.Count; i++)
                demoRow.Add(BuildExampleCellValue(enabledColumns[i]));
            table.Add(demoRow);

            return table;
        }

        private List<string> BuildAssertRow(List<ESTableColumnNameMap> enabledColumns)
        {
            var assertRow = new List<string> { "##assert" };
            if (enabledColumns == null)
                return assertRow;

            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap column = enabledColumns[i];
                if (column.isInfoKey || IsObjectKeyColumn(column) || IsSerialChildRowKeyColumn(column))
                    assertRow.Add("required;unique");
                else if (string.Equals(column.tableType, "json", StringComparison.OrdinalIgnoreCase) || column.valueWriteMode == ESTableValueWriteMode.Json)
                    assertRow.Add("json");
                else
                    assertRow.Add(string.Empty);
            }

            return assertRow;
        }

        private static List<string> BuildRowDirectiveHelpRow(List<ESTableColumnNameMap> enabledColumns)
        {
            var directiveRow = new List<string> { "##rowDirective | 空=正常导入；串行List父级列空=继承上一条父级；skip/ignore/disabled=跳过；comment:=备注；required=整行和Key必填；patch=只写非空；replace=强制覆盖；owner=只写SO本体；delete=删除；debug=打印本行追踪；debug:patch/debug:delete=按真实指令执行并打印" };
            int count = enabledColumns != null ? enabledColumns.Count : 0;
            for (int i = 0; i < count; i++)
                directiveRow.Add(string.Empty);
            return directiveRow;
        }

        private Dictionary<int, string> BuildXlsxDataDropdowns(List<List<string>> table)
        {
            var result = new Dictionary<int, string>();
            if (table == null || table.Count == 0)
                return result;

            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            Dictionary<string, ESTableColumnNameMap> columnMap = BuildColumnMap(enabledColumns);
            Dictionary<int, ESTableColumnNameMap> tableColumnMap = BuildTableColumnMap(table, columnMap);
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, tableColumnMap);
            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                if (column == null || column.tableColumnIndex < 0 || column.valueType == null || !column.valueType.IsEnum)
                    continue;

                string dropdown = BuildEnumDropdownList(column.valueType);
                if (!string.IsNullOrWhiteSpace(dropdown))
                    result[column.tableColumnIndex] = dropdown;
            }

            return result;
        }

        private static string BuildEnumDropdownList(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
                return string.Empty;

            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            var values = new List<string>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                string displayName = GetEnumDisplayName(fields[i]);
                string value = IsValidInlineXlsxListValue(displayName) ? displayName : fields[i].Name;
                if (IsValidInlineXlsxListValue(value))
                    values.Add(value);
            }

            string joined = string.Join(",", values);
            return joined.Length <= 240 ? joined : string.Empty;
        }

        private static bool IsValidInlineXlsxListValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(',') < 0
                && value.IndexOf('"') < 0
                && value.IndexOf('\n') < 0
                && value.IndexOf('\r') < 0;
        }

        private string BuildExampleCellValue(ESTableColumnNameMap column)
        {
            if (column == null)
                return string.Empty;
            if (column.isInfoKey || IsObjectKeyColumn(column))
                return "example_001";
            if (IsSerialChildRowKeyColumn(column))
                return rowBinding != null && rowBinding.allowEmptyRowKey ? string.Empty : "row_001";

            string type = column.tableType ?? string.Empty;
            if (type.IndexOf("int", StringComparison.OrdinalIgnoreCase) >= 0)
                return "1";
            if (type.IndexOf("float", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("double", StringComparison.OrdinalIgnoreCase) >= 0)
                return "1.0";
            if (type.IndexOf("bool", StringComparison.OrdinalIgnoreCase) >= 0)
                return "true";
            if (column.valueWriteMode == ESTableValueWriteMode.Json || type.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                return "{}";
            return "example";
        }

        public void AddUseBatch()
        {
            if (useBatches == null)
                useBatches = new List<ESSoTableRuleUseBatch>();

            useBatches.Add(new ESSoTableRuleUseBatch
            {
                batchName = string.IsNullOrEmpty(ruleKey) ? "New Batch" : ruleKey,
                fileName = FirstConfiguredName(),
                sheetName = FirstConfiguredName()
            });

            EditorUtility.SetDirty(this);
        }

        [ContextMenu("SO 表格规则/快速方案/标准 SO 表")]
        public void ApplyPresetStandardSoTable()
        {
            infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;
            groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;
            nameMatchMode = ESTableNameMatchMode.FieldToColumn;
            allowCreateInfoOnImport = true;
            allowCreateGroupOnImport = true;
            refreshPackBeforeExport = true;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.ImportAndExport);
        }

        [ContextMenu("SO 表格规则/快速方案/只导出表格")]
        public void ApplyPresetExportOnly()
        {
            infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;
            groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;
            refreshPackBeforeExport = true;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.Export);
        }

        [ContextMenu("SO 表格规则/快速方案/表格写回 SO")]
        public void ApplyPresetImportBack()
        {
            infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;
            groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;
            allowCreateInfoOnImport = true;
            allowCreateGroupOnImport = true;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.Import);
        }

        [ContextMenu("SO 表格规则/快速方案/普通 SO 简化表")]
        public void ApplyPresetSimpleScriptableObject()
        {
            if (typeBinding != null)
                typeBinding.objectKind = ESSoTableRuleObjectKind.ScriptableObject;

            infoExpandMode = ESTableInfoExpandMode.SerializedFields;
            groupSliceMode = ESTableGroupSliceMode.IgnoreGroup;
            nameMatchMode = ESTableNameMatchMode.FieldToColumn;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.ImportAndExport);
        }

        public void ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection direction)
        {
            if (useBatches == null)
                useBatches = new List<ESSoTableRuleUseBatch>();

            if (useBatches.Count == 0)
                AddUseBatch();

            for (int i = 0; i < useBatches.Count; i++)
            {
                ESSoTableRuleUseBatch batch = useBatches[i];
                if (batch == null)
                    continue;

                batch.direction = direction;
                if (string.IsNullOrWhiteSpace(batch.fileName))
                    batch.fileName = FirstConfiguredName();
                if (string.IsNullOrWhiteSpace(batch.sheetName))
                    batch.sheetName = FirstConfiguredName();
                if (string.IsNullOrWhiteSpace(batch.outputRoot))
                    batch.outputRoot = "SoTableConfig/Tables";
                if (string.IsNullOrWhiteSpace(batch.csvRelativePath))
                    batch.csvRelativePath = "csv";
                if (string.IsNullOrWhiteSpace(batch.xlsxRelativePath))
                    batch.xlsxRelativePath = "xlsx";
            }

            EditorUtility.SetDirty(this);
        }

        private string FirstConfiguredName()
        {
            if (!string.IsNullOrWhiteSpace(ruleKey))
                return ruleKey;
            if (!string.IsNullOrWhiteSpace(tableName))
                return tableName.StartsWith("Tb", StringComparison.Ordinal) && tableName.Length > 2 ? tableName.Substring(2) : tableName;
            if (!string.IsNullOrWhiteSpace(beanName))
                return beanName;
            return name;
        }

        public void ExecuteAllEnabledBatches()
        {
            if (useBatches == null)
                return;

            ESTableBatchExecuteChoice choice = ShowAllBatchesExecuteDialog();
            if (choice == ESTableBatchExecuteChoice.Cancel)
                return;
            if (choice == ESTableBatchExecuteChoice.Plan)
            {
                BeginExecutionCache();
                try
                {
                    LogAllBatchesExecutionPlan();
                }
                finally
                {
                    EndExecutionCache();
                }
                return;
            }

            BeginExecutionCache();
            try
            {
                ESTablePlanRiskSummary summary = BuildAllBatchesRiskSummary();
                if (!ConfirmPlanRiskBeforeExecute(summary, null))
                    return;

                for (int i = 0; i < useBatches.Count; i++)
                {
                    ESSoTableRuleUseBatch batch = useBatches[i];
                    if (batch != null && batch.enabled)
                        ExecuteUseBatchDirect(batch);
                }
            }
            finally
            {
                EndExecutionCache();
            }
        }

        public void ExecuteUseBatch(ESSoTableRuleUseBatch batch)
        {
            if (batch == null)
                return;

            ESTableBatchExecuteChoice choice = ShowBatchExecuteDialog(batch);
            if (choice == ESTableBatchExecuteChoice.Cancel)
                return;
            if (choice == ESTableBatchExecuteChoice.Plan)
            {
                BeginExecutionCache();
                try
                {
                    LogBatchExecutionPlan(batch);
                }
                finally
                {
                    EndExecutionCache();
                }
                return;
            }

            BeginExecutionCache();
            try
            {
                ESTablePlanRiskSummary summary = BuildBatchRiskSummary(batch);
                if (!ConfirmPlanRiskBeforeExecute(summary, batch))
                    return;

                ExecuteUseBatchDirect(batch);
            }
            finally
            {
                EndExecutionCache();
            }
        }

        private void ExecuteUseBatchDirect(ESSoTableRuleUseBatch batch)
        {
            if (batch == null)
                return;

            ESSoTableRuleUseBatch oldBatch = activeUseBatch;
            bool oldSuppressImportRiskConfirmation = suppressImportRiskConfirmation;
            try
            {
                activeUseBatch = batch;
                suppressImportRiskConfirmation = true;
                if (batch.useSuperBatch)
                {
                    ExecuteSuperBatch(batch);
                    return;
                }

                if (batch.direction == ESSoTableRuleUseDirection.Export)
                {
                    TryExportTableFiles();
                    return;
                }

                if (batch.direction == ESSoTableRuleUseDirection.Import)
                {
                    TryImportTableFileAuto();
                    return;
                }

                if (TryImportTableFileAuto())
                    TryExportTableFiles();
            }
            finally
            {
                activeUseBatch = oldBatch;
                suppressImportRiskConfirmation = oldSuppressImportRiskConfirmation;
            }
        }

        private List<List<string>> BuildTableRows()
        {
            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            var table = new List<List<string>>();
            if (enabledColumns.Count == 0)
                return table;

            table.Add(BuildHeaderRow(header.varMark, enabledColumns, GetActiveTableColumnName));
            table.Add(BuildHeaderRow(header.typeMark, enabledColumns, c => string.IsNullOrEmpty(c.tableType) ? "string" : c.tableType));
            table.Add(BuildHeaderRow(header.groupMark, enabledColumns, c => string.IsNullOrEmpty(header.defaultGroup) ? "client" : header.defaultGroup));
            table.Add(BuildHeaderRow(header.commentMark, enabledColumns, c => string.IsNullOrEmpty(c.comment) ? c.displayName : c.comment));
            table.Add(BuildAssertRow(enabledColumns));
            table.Add(BuildRowDirectiveHelpRow(enabledColumns));

            List<ScriptableObject> owners = CollectExportOwners();
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, null);
            for (int i = 0; i < owners.Count; i++)
                AppendOwnerRows(table, owners[i], compiledColumns);

            AppendPassThroughColumnsIfNeeded(table, enabledColumns);
            return table;
        }

        private int ApplyTableRowsToExistingObjects(List<List<string>> table)
        {
            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            Dictionary<string, ESTableColumnNameMap> columnMap = BuildColumnMap(enabledColumns);
            Dictionary<int, ESTableColumnNameMap> tableColumnMap = BuildTableColumnMap(table, columnMap);
            List<ScriptableObject> owners = CollectExportOwners();
            Dictionary<string, ScriptableObject> ownersByKey = BuildOwnersByKey(owners, enabledColumns);
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, tableColumnMap);
            int rowKeyColumnIndex = FindTableColumnIndex(tableColumnMap, rowBinding != null ? rowBinding.rowKeyColumnName : null);

            int changedCount = 0;
            int ownerCursor = 0;
            bool tableHasObjectKey = HasObjectKeyColumn(tableColumnMap);
            bool serialChildRows = rowBinding != null && rowBinding.IsListElementRow;
            bool pruneMissingChildren = serialChildRows && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.PruneMissingByTable;
            bool rebuildTouchedChildren = serialChildRows && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.RebuildTouchedOwners;
            Dictionary<ScriptableObject, HashSet<string>> importedChildKeysByOwner = pruneMissingChildren
                ? new Dictionary<ScriptableObject, HashSet<string>>()
                : null;
            HashSet<ScriptableObject> rebuiltChildOwners = rebuildTouchedChildren
                ? new HashSet<ScriptableObject>()
                : null;
            Dictionary<ScriptableObject, Dictionary<string, object>> childRowsByOwner = serialChildRows
                ? new Dictionary<ScriptableObject, Dictionary<string, object>>()
                : null;
            Dictionary<ScriptableObject, int> keylessChildCursorByOwner = serialChildRows
                ? new Dictionary<ScriptableObject, int>()
                : null;
            Dictionary<ScriptableObject, int> keylessChildCountByOwner = pruneMissingChildren
                ? new Dictionary<ScriptableObject, int>()
                : null;
            Dictionary<string, ISoDataGroup> groupsByKey = BuildGroupsByKey();
            HashSet<ISoDataGroup> mutatedGroupsForPackRefresh = new HashSet<ISoDataGroup>();
            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            ScriptableObject currentSerialOwner = null;
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                try
                {
                List<string> row = table[rowIndex];
                if (row == null || row.Count == 0)
                    continue;
                ESTableRowDirectiveInfo directive = ParseRowDirective(row);
                if (directive.directive == ESTableRowDirective.Skip || directive.directive == ESTableRowDirective.Comment)
                    continue;
                if (directive.directive == ESTableRowDirective.Required && IsDataRowEmpty(row))
                {
                    Debug.LogWarning("第 " + (rowIndex + 1) + " 行标记为 required，但整行为空，已跳过。", this);
                    continue;
                }
                if (!ShouldApplyTableRow(row, applyFilter))
                    continue;

                string groupKey = GetGroupKeyFromTableRow(row, tableColumnMap);
                ISoDataGroup targetGroup = ResolveImportGroup(groupKey, groupsByKey, ownerType, ownerType, row, tableColumnMap);
                string explicitObjectKey = GetObjectKeyFromTableRow(row, tableColumnMap);
                bool inheritedSerialOwner = serialChildRows && tableHasObjectKey && string.IsNullOrWhiteSpace(explicitObjectKey);
                ScriptableObject owner = FindOwnerForTableRow(row, tableColumnMap, ownersByKey, owners, ref ownerCursor, tableHasObjectKey);
                if (owner == null && inheritedSerialOwner)
                    owner = currentSerialOwner;
                if (targetGroup != null && ownerType != null && typeof(ISoDataInfo).IsAssignableFrom(ownerType))
                {
                    string infoKey = GetObjectKeyFromTableRow(row, tableColumnMap);
                    if (directive.directive == ESTableRowDirective.Delete)
                    {
                        if (string.IsNullOrWhiteSpace(infoKey))
                        {
                            if (TryDeleteGroupAsset(targetGroup, out string deleteGroupReason))
                            {
                                ClearExecutionCache();
                                RefreshPackAfterGroupDeletion(targetGroup);
                                changedCount++;
                            }
                            else
                            {
                                Debug.LogWarning("Row " + (rowIndex + 1) + " delete Group failed: " + deleteGroupReason, targetGroup as ScriptableObject);
                            }

                            continue;
                        }

                        if (TryDeleteInfoFromGroup(targetGroup, infoKey, out string deleteReason))
                        {
                            ClearExecutionCache();
                            EditorUtility.SetDirty(targetGroup as ScriptableObject);
                            mutatedGroupsForPackRefresh.Add(targetGroup);
                            changedCount++;
                        }
                        else
                        {
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行删除 Group/Info 失败：" + deleteReason, targetGroup as ScriptableObject);
                        }
                        continue;
                    }

                    owner = ResolveInfoOwnerInGroup(targetGroup, infoKey, out bool createdInfo, out string resolveReason);
                    if (owner == null)
                    {
                        if (!string.IsNullOrEmpty(resolveReason))
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行 Group/Info 解析失败：" + resolveReason, targetGroup as ScriptableObject);
                        continue;
                    }

                    if (createdInfo)
                    {
                        changedCount++;
                        mutatedGroupsForPackRefresh.Add(targetGroup);
                    }
                }

                if (owner == null)
                    owner = TryCreateOwnerForTableRow(row, tableColumnMap, ownerType, owners, ownersByKey);
                if (owner == null)
                    continue;
                if (serialChildRows && !inheritedSerialOwner)
                    currentSerialOwner = owner;

                object rowObject = owner;
                StringBuilder rowDebug = BuildRowDebugHeader(directive, rowIndex, owner);
                if (serialChildRows)
                {
                    string rowKey = rowKeyColumnIndex >= 0 && rowKeyColumnIndex < row.Count ? row[rowKeyColumnIndex] : string.Empty;
                    AppendRowDebugTarget(rowDebug, owner, rowKey);
                    bool emptyRowKeyAllowedByRebuild = string.IsNullOrWhiteSpace(rowKey) && CanImportEmptyChildKeyForRebuild(owner);
                    bool keylessChildByOrder = directive.directive != ESTableRowDirective.Owner
                        && string.IsNullOrWhiteSpace(rowKey)
                        && CanImportEmptyChildKeyByOrder(owner);
                    if (directive.directive != ESTableRowDirective.Owner && string.IsNullOrWhiteSpace(rowKey) && !emptyRowKeyAllowedByRebuild && !keylessChildByOrder)
                    {
                        Debug.LogWarning(BuildTableRowErrorMessage("导入定位子级", rowIndex, owner, "子级 RowKey 为空。", "填写子级 Key；或开启允许空 Key，并使用按宿主重建/顺序匹配模式。"), owner);
                        continue;
                    }

                    if (directive.directive == ESTableRowDirective.Required && string.IsNullOrWhiteSpace(rowKey) && !emptyRowKeyAllowedByRebuild && !keylessChildByOrder)
                    {
                        Debug.LogWarning("第 " + (rowIndex + 1) + " 行标记为 required，但子级 Key 为空，已跳过。", owner);
                        continue;
                    }

                    if (directive.directive == ESTableRowDirective.Delete)
                    {
                        if (rowDebug != null)
                            rowDebug.AppendLine("操作：删除子级");
                        bool deletedChild = keylessChildByOrder
                            ? TryDeleteKeylessChildRowByOrder(owner, keylessChildCursorByOwner, out string deleteReason)
                            : TryDeleteChildRow(owner, rowKey, out deleteReason);
                        if (deletedChild)
                        {
                            RemoveCachedChildRow(childRowsByOwner, owner, rowKey);
                            EditorUtility.SetDirty(owner);
                            if (rowDebug != null)
                                rowDebug.AppendLine("结果：删除子级成功");
                            changedCount++;
                        }
                        else
                        {
                            if (rowDebug != null)
                                rowDebug.AppendLine("结果：删除子级失败，" + deleteReason);
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行删除子级失败：" + deleteReason, owner);
                        }
                        if (rowDebug != null)
                            Debug.Log(rowDebug.ToString(), owner);
                        continue;
                    }

                    bool childRowsClearedNow = rebuildTouchedChildren
                        && directive.directive != ESTableRowDirective.Owner
                        && rebuiltChildOwners != null
                        && !rebuiltChildOwners.Contains(owner);

                    if (rebuildTouchedChildren && directive.directive != ESTableRowDirective.Owner && !EnsureChildRowsClearedForRebuild(owner, rebuiltChildOwners, out string rebuildReason))
                    {
                        Debug.LogWarning("第 " + (rowIndex + 1) + " 行子级重建失败：" + rebuildReason, owner);
                        continue;
                    }

                    if (childRowsClearedNow)
                    {
                        childRowsByOwner?.Remove(owner);
                        keylessChildCursorByOwner?.Remove(owner);
                    }

                    if (directive.directive != ESTableRowDirective.Owner && keylessChildByOrder)
                    {
                        if (!TryGetOrCreateKeylessChildRowByOrder(owner, keylessChildCursorByOwner, out rowObject, out string keylessReason))
                        {
                            Debug.LogWarning(BuildTableRowErrorMessage("导入无 Key 子级", rowIndex, owner, keylessReason, "确认当前批次允许空 Key，且子级容器是可按顺序匹配的 List。"), owner);
                            continue;
                        }

                        AddKeylessChildCount(keylessChildCountByOwner, owner);
                    }
                    else if (directive.directive != ESTableRowDirective.Owner && emptyRowKeyAllowedByRebuild)
                    {
                        if (!TryCreateAppendedChildRow(owner, out rowObject, out string appendReason))
                        {
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行无 Key 子级追加失败：" + appendReason, owner);
                            continue;
                        }

                        AddKeylessChildCount(keylessChildCountByOwner, owner);
                    }
                    else if (directive.directive != ESTableRowDirective.Owner && !TryGetOrCreateChildRowCached(owner, rowKey, childRowsByOwner, out rowObject, out string reason))
                    {
                        Debug.LogWarning(BuildTableRowErrorMessage("导入定位子级", rowIndex, owner, reason, "确认 rowKey、List 字段路径、元素 Key 字段路径和创建缺失元素设置。"), owner);
                        continue;
                    }

                    if (pruneMissingChildren && directive.directive != ESTableRowDirective.Owner && !string.IsNullOrWhiteSpace(rowKey))
                        AddImportedChildKey(importedChildKeysByOwner, owner, rowKey);
                }
                else if (directive.directive == ESTableRowDirective.Delete)
                {
                    if (rowDebug != null)
                        rowDebug.AppendLine("操作：删除 SO 资产");
                    if (TryDeleteOwnerAsset(owner, out string deleteReason))
                    {
                        ClearExecutionCache();
                        if (rowDebug != null)
                            rowDebug.AppendLine("结果：删除 SO 成功");
                        changedCount++;
                    }
                    else
                    {
                        if (rowDebug != null)
                            rowDebug.AppendLine("结果：删除 SO 失败，" + deleteReason);
                        Debug.LogWarning("第 " + (rowIndex + 1) + " 行删除 SO 失败：" + deleteReason, owner);
                    }
                    if (rowDebug != null)
                        Debug.Log(rowDebug.ToString(), owner);
                    continue;
                }

                Undo.RecordObject(owner, "SO 表格导入写回");
                bool forceTableValues = directive.directive == ESTableRowDirective.Replace;
                bool skipEmptyCells = directive.directive == ESTableRowDirective.Patch;
                bool ownerColumnsOnly = directive.directive == ESTableRowDirective.Owner;
                if (rowDebug != null && !serialChildRows)
                    rowDebug.AppendLine("目标对象：SO 本体");
                ApplyTableRowToObject(row, compiledColumns, owner, rowObject, forceTableValues, skipEmptyCells, ownerColumnsOnly, rowDebug, rowIndex, inheritedSerialOwner);
                EditorUtility.SetDirty(owner);
                changedCount++;
                if (rowDebug != null)
                    Debug.Log(rowDebug.ToString(), owner);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(BuildTableRowErrorMessage("导入处理行", rowIndex, this, e.Message, "查看该行 Key、行指令、字段格式和本批次字段映射。"), this);
                }
            }

            if (pruneMissingChildren)
            {
                changedCount += PruneMissingChildRows(importedChildKeysByOwner, keylessChildCountByOwner);
                changedCount += PruneKeylessChildRowsByCount(keylessChildCountByOwner);
            }

            foreach (ISoDataGroup mutatedGroup in mutatedGroupsForPackRefresh)
                RefreshPackAfterGroupMutation(mutatedGroup);

            return changedCount;
        }

        #endregion
    }
}
#endif
