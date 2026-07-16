#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        [Button("Debug 指定表格行")]
        public void DebugConfiguredTableRow()
        {
            string inputPath = ResolveExistingInputPath();
            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                Debug.LogWarning("行级 Debug 失败：没有找到当前批次可读取的表格。", this);
                return;
            }

            List<List<string>> table = ReadTableFileAuto(inputPath);
            int rowIndex = Math.Max(1, debugRowNumber) - 1;
            if (rowIndex < 4 || rowIndex >= table.Count)
            {
                Debug.LogWarning("行级 Debug 失败：行号超出数据区。当前表格行数=" + table.Count + "，Debug 行号=" + debugRowNumber, this);
                return;
            }

            var summary = new ESTablePlanRiskSummary();
            var builder = new StringBuilder();
            builder.AppendLine("SO 表格行级 Debug（只读，不写入）");
            builder.AppendLine("表格：" + inputPath);
            builder.AppendLine("行号：" + debugRowNumber);

            try
            {
                AppendSingleImportRowDebug(builder, table, rowIndex, summary);
            }
            catch (Exception e)
            {
                AddPlanError(summary, "行级 Debug 失败：" + e.Message);
                builder.AppendLine("异常：" + e);
            }

            string report = BuildPlanReportWithOverview("SO 表格行级 Debug 报告", summary, builder.ToString());
            string path = WriteBatchPlanReport(GetActiveUseBatch(), report);
            Debug.Log("SO 表格行级 Debug 报告已生成：\n" + path, this);
            EditorUtility.OpenWithDefaultApp(path);
        }

        private void AppendSingleImportRowDebug(StringBuilder builder, List<List<string>> table, int rowIndex, ESTablePlanRiskSummary summary)
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
            bool tableHasObjectKey = HasObjectKeyColumn(tableColumnMap);
            int ownerCursor = 0;

            List<string> row = table[rowIndex];
            ESTableRowDirectiveInfo directive = ParseRowDirective(row);
            builder.AppendLine("指令：" + FirstNotEmptyLocal(directive.rawText, "正常"));
            builder.AppendLine("整行空：" + IsDataRowEmpty(row));

            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            bool inRange = ShouldApplyTableRow(row, applyFilter);
            builder.AppendLine("是否在当前批次范围内：" + inRange);
            if (!inRange)
            {
                summary.skippedRows++;
                return;
            }

            ScriptableObject owner = FindOwnerForTableRow(row, tableColumnMap, ownersByKey, owners, ref ownerCursor, tableHasObjectKey);
            string createFolder = string.Empty;
            bool canCreateOwner = owner == null && CanCreateOwnerInActiveFolder(ownerType, out createFolder);
            builder.AppendLine("目标 SO：" + (owner != null ? DescribePlanObject(owner) : "(未找到)"));
            builder.AppendLine("可创建缺失 SO：" + canCreateOwner + (canCreateOwner ? "，目录=" + createFolder : string.Empty));

            object rowObject = owner;
            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                string rowKey = GetCell(row, rowKeyColumnIndex);
                builder.AppendLine("子级 Key 列索引：" + rowKeyColumnIndex);
                builder.AppendLine("子级 Key：" + FirstNotEmptyLocal(rowKey, "(空)"));
                builder.AppendLine("允许空 Key 重建导入：" + CanImportEmptyChildKeyForRebuild(owner));
                if (owner != null && directive.directive != ESTableRowDirective.Owner)
                    rowObject = FindExistingChildRowReadOnly(owner, rowKey);
                builder.AppendLine("目标子级：" + (rowObject != null && !ReferenceEquals(rowObject, owner) ? rowObject.GetType().Name : "(未找到/将新建/owner行)"));
            }

            AppendFieldChangePlan(builder, row, compiledColumns, owner, rowObject, directive, summary, rowIndex);
        }

        private ESTableBatchExecuteChoice ShowBatchExecuteDialog(ESSoTableRuleUseBatch batch)
        {
            string batchName = FirstNotEmptyLocal(batch != null ? batch.batchName : null, "(未命名批次)");
            int result = EditorUtility.DisplayDialogComplex(
                "SO 表格批次执行确认",
                "即将执行批次：" + batchName + "\n\n获取计划：只读取表格和 SO，输出完整计划报告，不写 SO、不写表格、不保存资产。\n直接执行：执行前仍会做错误预检和高风险二次确认。",
                "直接执行",
                "取消",
                "获取计划");

            if (result == 0)
                return ESTableBatchExecuteChoice.Execute;
            if (result == 2)
                return ESTableBatchExecuteChoice.Plan;
            return ESTableBatchExecuteChoice.Cancel;
        }

        private ESTableBatchExecuteChoice ShowAllBatchesExecuteDialog()
        {
            int enabledCount = 0;
            if (useBatches != null)
            {
                for (int i = 0; i < useBatches.Count; i++)
                {
                    if (useBatches[i] != null && useBatches[i].enabled)
                        enabledCount++;
                }
            }

            int result = EditorUtility.DisplayDialogComplex(
                "SO 表格全部批次执行确认",
                "即将执行全部启用批次。\n\n启用批次数：" + enabledCount + "\n\n获取计划：只读预检所有批次，不写 SO、不写表格、不保存资产。",
                "直接执行",
                "取消",
                "获取计划");

            if (result == 0)
                return ESTableBatchExecuteChoice.Execute;
            if (result == 2)
                return ESTableBatchExecuteChoice.Plan;
            return ESTableBatchExecuteChoice.Cancel;
        }

        private string BuildBatchExecutionPlanBody(ESSoTableRuleUseBatch batch, ESTablePlanRiskSummary summary)
        {
            if (batch != null && batch.useSuperBatch)
                return BuildSuperBatchPlanReport(batch, summary);

            return BuildStandardBatchPlanReport(batch, summary);
        }
        private ESTablePlanRiskSummary BuildBatchRiskSummary(ESSoTableRuleUseBatch batch)
        {
            var summary = new ESTablePlanRiskSummary();
            ESSoTableRuleUseBatch oldBatch = activeUseBatch;
            try
            {
                activeUseBatch = batch;
                BuildBatchExecutionPlanBody(batch, summary);
            }
            catch (Exception e)
            {
                AddPlanError(summary, "批次预检失败：" + e.Message);
            }
            finally
            {
                activeUseBatch = oldBatch;
            }

            return summary;
        }

        private ESTablePlanRiskSummary BuildAllBatchesRiskSummary()
        {
            var all = new ESTablePlanRiskSummary();
            if (useBatches == null)
                return all;

            for (int i = 0; i < useBatches.Count; i++)
            {
                ESSoTableRuleUseBatch batch = useBatches[i];
                if (batch == null || !batch.enabled)
                    continue;

                MergePlanSummary(all, BuildBatchRiskSummary(batch));
            }

            return all;
        }

        private bool ConfirmPlanRiskBeforeExecute(ESTablePlanRiskSummary summary, ESSoTableRuleUseBatch batch)
        {
            if (summary == null)
                return true;

            if (summary.HasErrors)
            {
                string report = BuildPlanReportWithOverview("执行已阻止：计划预检发现错误。", summary, string.Empty);
                string path = WriteBatchPlanReport(batch, report);
                Debug.LogError("SO 表格执行已阻止：计划预检发现错误，报告已生成：\n" + path, this);
                EditorUtility.OpenWithDefaultApp(path);
                return false;
            }

            if (!summary.HasHighRisk)
                return true;

            string message = "计划发现高风险操作，是否继续执行？\n\n"
                + "删除 SO：" + summary.deleteOwners + "\n"
                + "删除子级：" + summary.deleteChildren + "\n"
                + "清空字段：" + summary.clearFields + "\n"
                + "覆盖非空字段：" + summary.overwriteNonEmptyFields + "\n"
                + "重建表格：" + summary.rebuildTables + "\n\n"
                + "建议先选择“获取计划”查看完整明细。";

            return EditorUtility.DisplayDialog("SO 表格高风险确认", message, "继续执行", "取消");
        }

        private void LogBatchExecutionPlan(ESSoTableRuleUseBatch batch)
        {
            var summary = new ESTablePlanRiskSummary();
            ESSoTableRuleUseBatch oldBatch = activeUseBatch;
            try
            {
                activeUseBatch = batch;
                string body = BuildBatchExecutionPlanBody(batch, summary);
                string report = BuildPlanReportWithOverview("SO 表格批次完整计划（只读，不写入）", summary, body);
                string path = WriteBatchPlanReport(batch, report);
                Debug.Log("SO 表格批次完整计划已生成：\n" + path, this);
                EditorUtility.OpenWithDefaultApp(path);
            }
            catch (Exception e)
            {
                Debug.LogError("获取批次计划失败：" + e.Message + "\n" + e, this);
            }
            finally
            {
                activeUseBatch = oldBatch;
            }
        }

        private void LogAllBatchesExecutionPlan()
        {
            var summary = new ESTablePlanRiskSummary();
            var body = new StringBuilder();
            body.AppendLine("全部启用批次明细");

            if (useBatches != null)
            {
                for (int i = 0; i < useBatches.Count; i++)
                {
                    ESSoTableRuleUseBatch batch = useBatches[i];
                    if (batch == null || !batch.enabled)
                        continue;

                    var batchSummary = new ESTablePlanRiskSummary();
                    ESSoTableRuleUseBatch oldBatch = activeUseBatch;
                    try
                    {
                        activeUseBatch = batch;
                        body.AppendLine();
                        body.AppendLine("################ 批次 " + (i + 1) + "：" + FirstNotEmptyLocal(batch.batchName, "(未命名批次)") + " ################");
                        body.Append(BuildBatchExecutionPlanBody(batch, batchSummary));
                    }
                    catch (Exception e)
                    {
                        AddPlanError(batchSummary, "批次 " + (i + 1) + " 计划失败：" + e.Message);
                    }
                    finally
                    {
                        activeUseBatch = oldBatch;
                    }

                    MergePlanSummary(summary, batchSummary);
                }
            }

            string report = BuildPlanReportWithOverview("SO 表格全部批次完整计划（只读，不写入）", summary, body.ToString());
            string path = WriteBatchPlanReport(null, report);
            Debug.Log("SO 表格全部批次完整计划已生成：\n" + path, this);
            EditorUtility.OpenWithDefaultApp(path);
        }

        private string BuildStandardBatchPlanReport(ESSoTableRuleUseBatch batch, ESTablePlanRiskSummary summary)
        {
            var builder = new StringBuilder();
            builder.AppendLine("批次：" + FirstNotEmptyLocal(batch != null ? batch.batchName : null, "(未命名批次)"));
            builder.AppendLine("方向：" + (batch != null ? batch.direction.ToString() : "(无批次)"));
            builder.AppendLine("CSV：" + GetOutputPath(GetActiveCsvRelativePath(), ".csv"));
            builder.AppendLine("XLSX：" + GetOutputPath(GetActiveXlsxRelativePath(), ".xlsx"));

            ESSoTableRuleUseDirection direction = batch != null ? batch.direction : ESSoTableRuleUseDirection.Export;
            if (direction == ESSoTableRuleUseDirection.Import || direction == ESSoTableRuleUseDirection.ImportAndExport)
                AppendFullImportPlan(builder, summary);
            if (direction == ESSoTableRuleUseDirection.Export || direction == ESSoTableRuleUseDirection.ImportAndExport)
                AppendFullExportPlan(builder, summary);

            return builder.ToString();
        }

        private void AppendFullExportPlan(StringBuilder builder, ESTablePlanRiskSummary summary)
        {
            builder.AppendLine();
            builder.AppendLine("========== 导出计划 ==========");

            if (GetActiveExportWriteMode() == ESTableExportWriteMode.Rebuild)
            {
                summary.rebuildTables++;
                AddPlanRisk(summary, "导出模式为整表重建，会覆盖目标表格内容。");
            }

            List<List<string>> table = BuildTableRows();
            builder.AppendLine("将生成表格行数：" + table.Count);
            builder.AppendLine("导出写入模式：" + GetActiveExportWriteMode());
            builder.AppendLine("子级导出模式：" + GetActiveSerialChildWriteMode());

            ValidateExportPlanKeys(table, summary);

            List<ScriptableObject> owners = CollectExportOwners();
            builder.AppendLine("将读取 SO 数：" + owners.Count);
            for (int i = 0; i < owners.Count && i < 200; i++)
                builder.AppendLine("  - " + DescribePlanObject(owners[i]));
            if (owners.Count > 200)
                builder.AppendLine("  ... 其余 " + (owners.Count - 200) + " 个 SO 已省略。");
        }

        private void AppendFullImportPlan(StringBuilder builder, ESTablePlanRiskSummary summary)
        {
            builder.AppendLine();
            builder.AppendLine("========== 导入计划 ==========");
            string inputPath = ResolveExistingInputPath();
            builder.AppendLine("输入表格：" + FirstNotEmptyLocal(inputPath, "(未找到)"));
            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                AddPlanError(summary, "没有找到可导入表格。");
                return;
            }

            List<List<string>> table = ReadTableFileAuto(inputPath);
            if (table.Count < 5)
            {
                AddPlanError(summary, "表格行数不足：至少需要 4 行表头和 1 行数据。");
                return;
            }

            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            Dictionary<string, ESTableColumnNameMap> columnMap = BuildColumnMap(enabledColumns);
            Dictionary<int, ESTableColumnNameMap> tableColumnMap = BuildTableColumnMap(table, columnMap);
            List<ScriptableObject> owners = CollectExportOwners();
            Dictionary<string, ScriptableObject> ownersByKey = BuildOwnersByKey(owners, enabledColumns);
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, tableColumnMap);
            int rowKeyColumnIndex = FindTableColumnIndex(tableColumnMap, rowBinding != null ? rowBinding.rowKeyColumnName : null);
            bool tableHasObjectKey = HasObjectKeyColumn(tableColumnMap);
            bool serialChildRows = rowBinding != null && rowBinding.IsListElementRow;
            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            ValidateColumnAssertions(table, compiledColumns, applyFilter, summary);
            int ownerCursor = 0;

            int dataStartRow = GetDataStartRowIndex(table);
            builder.AppendLine("数据行数：" + Math.Max(0, table.Count - dataStartRow));
            builder.AppendLine("已映射列数：" + tableColumnMap.Count);
            if (serialChildRows && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.RebuildTouchedOwners)
            {
                summary.deleteChildren++;
                AddPlanRisk(summary, "导入同步模式为重建触达宿主子级：每个被表格触达的宿主 SO 会先清空子级容器，再按表格顺序重建。");
            }

            for (int rowIndex = dataStartRow; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                ESTableRowDirectiveInfo directive = ParseRowDirective(row);
                builder.AppendLine();
                builder.AppendLine("第 " + (rowIndex + 1) + " 行 | 指令=" + FirstNotEmptyLocal(directive.rawText, "正常"));

                if (directive.directive == ESTableRowDirective.Skip || directive.directive == ESTableRowDirective.Comment)
                {
                    summary.skippedRows++;
                    builder.AppendLine("动作：跳过整行。");
                    continue;
                }

                if (IsDataRowEmpty(row))
                {
                    summary.skippedRows++;
                    builder.AppendLine("动作：跳过空行。");
                    if (directive.directive == ESTableRowDirective.Required)
                        AddPlanError(summary, "第 " + (rowIndex + 1) + " 行标记 required，但整行为空。");
                    continue;
                }

                if (!ShouldApplyTableRow(row, applyFilter))
                {
                    summary.skippedRows++;
                    builder.AppendLine("动作：跳过。原因：不在当前批次应用范围内。");
                    continue;
                }

                ScriptableObject owner = FindOwnerForTableRow(row, tableColumnMap, ownersByKey, owners, ref ownerCursor, tableHasObjectKey);
                bool canCreateOwner = owner == null && CanCreateOwnerInActiveFolder(ownerType, out _);
                if (owner == null && canCreateOwner)
                    summary.addedOwners++;

                if (owner == null && !canCreateOwner && directive.directive != ESTableRowDirective.Delete)
                {
                    AddPlanError(summary, "第 " + (rowIndex + 1) + " 行找不到目标 SO，且当前批次未允许在文件夹中创建缺失 SO。");
                    builder.AppendLine("目标 SO：未找到。");
                    continue;
                }

                builder.AppendLine("目标 SO：" + (owner != null ? DescribePlanObject(owner) : "将新增 SO"));

                if (directive.directive == ESTableRowDirective.Delete)
                {
                    if (serialChildRows)
                    {
                        string rowKey = GetCell(row, rowKeyColumnIndex);
                        summary.deleteChildren++;
                        AddPlanRisk(summary, "第 " + (rowIndex + 1) + " 行将删除子级，SO=" + DescribePlanObject(owner) + "，RowKey=" + rowKey);
                        builder.AppendLine("动作：删除子级 RowKey=" + rowKey);
                    }
                    else
                    {
                        summary.deleteOwners++;
                        AddPlanRisk(summary, "第 " + (rowIndex + 1) + " 行将删除 SO：" + DescribePlanObject(owner));
                        builder.AppendLine("动作：删除 SO。");
                    }
                    continue;
                }

                object rowObject = owner;
                if (serialChildRows)
                {
                    string rowKey = GetCell(row, rowKeyColumnIndex);
                    if (string.IsNullOrWhiteSpace(rowKey) && !CanImportEmptyChildKeyForRebuild(owner))
                    {
                        AddPlanError(summary, "第 " + (rowIndex + 1) + " 行子级 Key 为空。只有启用允许空 Key 且使用按宿主重建模式时才允许。");
                        builder.AppendLine("目标子级：Key 为空，当前策略不允许。");
                        continue;
                    }

                    rowObject = directive.directive == ESTableRowDirective.Owner ? owner : FindExistingChildRowReadOnly(owner, rowKey);
                    if (directive.directive != ESTableRowDirective.Owner && rowObject == null)
                        summary.addedChildren++;
                    builder.AppendLine("目标子级：" + (directive.directive == ESTableRowDirective.Owner ? "owner 行，只写 SO 本体字段" : FirstNotEmptyLocal(rowKey, "(空 Key 重建行)")));
                }

                AppendFieldChangePlan(builder, row, compiledColumns, owner, rowObject, directive, summary, rowIndex);
            }

            if (serialChildRows && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.PruneMissingByTable)
            {
                summary.deleteChildren++;
                AddPlanRisk(summary, "导入同步模式为按表裁剪子级，表格缺失的已存在子级可能被删除。");
            }
        }

        private void ValidateColumnAssertions(List<List<string>> table, List<ESTableCompiledColumn> compiledColumns, ESTableBatchApplyFilter applyFilter, ESTablePlanRiskSummary summary)
        {
            int assertRowIndex = FindAssertRowIndex(table);
            if (assertRowIndex < 0 || table == null || compiledColumns == null)
                return;

            var firstSeenByColumn = new Dictionary<int, Dictionary<string, int>>();
            int dataStartRow = GetDataStartRowIndex(table);
            for (int rowIndex = dataStartRow; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                ESTableRowDirectiveInfo directive = ParseRowDirective(row);
                if (directive.directive == ESTableRowDirective.Skip || directive.directive == ESTableRowDirective.Comment || IsDataRowEmpty(row))
                    continue;
                if (!ShouldApplyTableRow(row, applyFilter))
                    continue;

                for (int i = 0; i < compiledColumns.Count; i++)
                {
                    ESTableCompiledColumn column = compiledColumns[i];
                    if (column == null || column.tableColumnIndex < 0)
                        continue;

                    string assertText = GetCell(table[assertRowIndex], column.tableColumnIndex).Trim();
                    if (string.IsNullOrWhiteSpace(assertText))
                        continue;

                    string rawValue = GetCell(row, column.tableColumnIndex);
                    string[] tokens = assertText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                    {
                        string token = tokens[tokenIndex].Trim();
                        if (string.IsNullOrEmpty(token))
                            continue;

                        ValidateColumnAssertionToken(summary, firstSeenByColumn, column, rawValue, token, rowIndex);
                    }
                }
            }
        }

        private void ValidateColumnAssertionToken(ESTablePlanRiskSummary summary, Dictionary<int, Dictionary<string, int>> firstSeenByColumn, ESTableCompiledColumn column, string rawValue, string token, int rowIndex)
        {
            string normalized = token.Trim();
            string lower = normalized.ToLowerInvariant();
            string columnName = column.map != null ? column.map.columnName : "(未知列)";
            int excelRow = rowIndex + 1;
            int excelColumn = column.tableColumnIndex + 1;

            if (lower == "required")
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 要求 required，但单元格为空。");
                return;
            }

            if (lower == "unique")
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    return;

                if (!firstSeenByColumn.TryGetValue(column.tableColumnIndex, out Dictionary<string, int> seen))
                {
                    seen = new Dictionary<string, int>(StringComparer.Ordinal);
                    firstSeenByColumn[column.tableColumnIndex] = seen;
                }

                if (seen.TryGetValue(rawValue, out int firstRow))
                    AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 要求 unique，但值 `" + rawValue + "` 已在第 " + (firstRow + 1) + " 行出现。");
                else
                    seen[rawValue] = rowIndex;
                return;
            }

            if (lower == "json")
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    return;
                try
                {
                    JsonConvert.DeserializeObject(rawValue);
                }
                catch (Exception e)
                {
                    AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 要求 json，但解析失败：" + e.Message);
                }
                return;
            }

            if (lower == "asset")
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    return;
                string path = rawValue;
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    path = AssetDatabase.GUIDToAssetPath(rawValue);
                if (string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                    AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 要求 asset，但找不到资源：" + rawValue);
                return;
            }

            if (lower.StartsWith("range:", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    return;
                string range = normalized.Substring("range:".Length);
                string[] parts = range.Split(new[] { ".." }, StringSplitOptions.None);
                if (parts.Length != 2 || !double.TryParse(parts[0], out double min) || !double.TryParse(parts[1], out double max) || !double.TryParse(rawValue, out double number) || number < min || number > max)
                    AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 要求 " + normalized + "，当前值=" + rawValue + "。");
                return;
            }

            if (lower.StartsWith("regex:", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(rawValue))
                    return;
                string pattern = normalized.Substring("regex:".Length);
                try
                {
                    if (!Regex.IsMatch(rawValue, pattern))
                        AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 不匹配 " + normalized + "，当前值=" + rawValue + "。");
                }
                catch (Exception e)
                {
                    AddPlanError(summary, "断言失败：第 " + excelRow + " 行，第 " + excelColumn + " 列，列名=" + columnName + " 的 regex 无效：" + e.Message);
                }
            }
        }

        private void AppendFieldChangePlan(StringBuilder builder, List<string> row, List<ESTableCompiledColumn> compiledColumns, ScriptableObject owner, object rowObject, ESTableRowDirectiveInfo directive, ESTablePlanRiskSummary summary, int rowIndex)
        {
            bool forceTableValues = directive.directive == ESTableRowDirective.Replace;
            bool skipEmptyCells = directive.directive == ESTableRowDirective.Patch;
            bool ownerColumnsOnly = directive.directive == ESTableRowDirective.Owner;

            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                if (!column.canWrite || column.tableColumnIndex < 0 || column.tableColumnIndex >= row.Count)
                    continue;
                if (ownerColumnsOnly && column.useRowObject)
                    continue;

                string rawValue = row[column.tableColumnIndex];
                if (skipEmptyCells && string.IsNullOrWhiteSpace(rawValue))
                    continue;

                object target = column.useRowObject ? rowObject : owner;
                string oldValue = string.Empty;
                if (target != null)
                {
                    object currentValue = ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
                    oldValue = ConvertCellValue(currentValue, column.map.valueWriteMode);
                    if (!forceTableValues && column.map.authority == ESTableColumnAuthority.SoAuthority && !IsEmptyAuthorityValue(currentValue))
                    {
                        builder.AppendLine("  - 跳过 SO 权威字段：" + column.map.columnName + "，当前值=" + oldValue);
                        continue;
                    }
                }

                try
                {
                    object converted = ConvertStringToValue(rawValue, column.valueType, column.map.valueWriteMode);
                    string newValue = ConvertCellValue(converted, column.map.valueWriteMode);
                    if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    {
                        summary.modifiedFields++;
                        if (!string.IsNullOrEmpty(oldValue) && string.IsNullOrEmpty(newValue) && !IsIdentityPlanColumn(column.map))
                        {
                            summary.clearFields++;
                            AddPlanRisk(summary, "第 " + (rowIndex + 1) + " 行将清空字段：" + column.map.columnName + "，旧值=" + oldValue);
                        }
                        else if (!string.IsNullOrEmpty(oldValue) && !IsIdentityPlanColumn(column.map))
                        {
                            summary.overwriteNonEmptyFields++;
                            AddPlanRisk(summary, "第 " + (rowIndex + 1) + " 行将覆盖非空字段：" + column.map.columnName + "，" + oldValue + " -> " + newValue);
                        }
                    }

                    builder.AppendLine("  - " + (column.useRowObject ? "子级" : "SO") + "." + column.memberPath + " | " + oldValue + " -> " + newValue + " | 原始=" + rawValue);
                }
                catch (Exception e)
                {
                    AddPlanError(summary, "第 " + (rowIndex + 1) + " 行，第 " + (column.tableColumnIndex + 1) + " 列，列名=" + column.map.columnName + "，字段=" + column.map.soFieldPath + "，目标类型=" + GetTypeName(column.valueType) + "，原始值=" + rawValue + "，错误=" + e.Message + "，建议=" + BuildImportCellFixSuggestion(column.valueType, column.map.valueWriteMode));
                    builder.AppendLine("  - 错误：" + column.map.columnName + " | " + e.GetType().Name + " | 建议=" + BuildImportCellFixSuggestion(column.valueType, column.map.valueWriteMode));
                }
            }
        }

        private void ValidateExportPlanKeys(List<List<string>> table, ESTablePlanRiskSummary summary)
        {
            int objectKeyIndex = FindExportObjectKeyColumnIndex(table);
            int rowKeyIndex = FindExportRowKeyColumnIndex(table);
            if (objectKeyIndex < 0)
            {
                AddPlanError(summary, "导出表缺少对象唯一 Key 列。");
                return;
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                string key = BuildExportRowKey(table[rowIndex], objectKeyIndex, rowKeyIndex);
                if (string.IsNullOrWhiteSpace(key))
                {
                    if (CanExportEmptyChildKeyForRebuild())
                        continue;
                    AddPlanError(summary, "导出第 " + (rowIndex + 1) + " 行 Key 为空。");
                    continue;
                }

                if (!keys.Add(key))
                    AddPlanError(summary, "导出第 " + (rowIndex + 1) + " 行 Key 重复：" + key);
            }
        }

        private object FindExistingChildRowReadOnly(ScriptableObject owner, string rowKey)
        {
            if (owner == null || rowBinding == null || !rowBinding.IsListElementRow || string.IsNullOrWhiteSpace(rowKey))
                return null;

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
                return dictionary.Contains(rowKey) ? dictionary[rowKey] : null;

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                string key = ESRowBindingReflectionUtility.GetMemberValue(element, rowBinding.elementKeyFieldPath)?.ToString();
                if (string.Equals(key, rowKey, StringComparison.Ordinal))
                    return element;
            }

            return null;
        }

        private bool CanExportEmptyChildKeyForRebuild()
        {
            return rowBinding != null
                && rowBinding.IsListElementRow
                && rowBinding.allowEmptyRowKey
                && GetActiveSerialChildWriteMode() == ESTableSerialChildWriteMode.RebuildByOwner;
        }

        private string BuildPlanReportWithOverview(string title, ESTablePlanRiskSummary summary, string body)
        {
            var builder = new StringBuilder();
            builder.AppendLine(title);
            builder.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();
            AppendPlanOverview(builder, summary);
            builder.AppendLine();
            builder.Append(body ?? string.Empty);
            return builder.ToString();
        }

        private void AppendPlanOverview(StringBuilder builder, ESTablePlanRiskSummary summary)
        {
            summary ??= new ESTablePlanRiskSummary();
            builder.AppendLine("========== 汇总 ==========");
            builder.AppendLine("新增 SO：" + summary.addedOwners);
            builder.AppendLine("新增子级：" + summary.addedChildren);
            builder.AppendLine("字段变更：" + summary.modifiedFields);
            builder.AppendLine("跳过行：" + summary.skippedRows);
            builder.AppendLine("错误：" + summary.errors);
            builder.AppendLine("高风险：删除SO=" + summary.deleteOwners
                + "，删除子级=" + summary.deleteChildren
                + "，清空字段=" + summary.clearFields
                + "，覆盖非空=" + summary.overwriteNonEmptyFields
                + "，重建表格=" + summary.rebuildTables);

            builder.AppendLine();
            builder.AppendLine("========== 错误区（置顶） ==========");
            AppendLimitedLines(builder, summary.errorLines);
            builder.AppendLine();
            builder.AppendLine("========== 高风险区（置顶） ==========");
            AppendLimitedLines(builder, summary.riskLines);
        }

        private static void AppendLimitedLines(StringBuilder builder, List<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                builder.AppendLine("无");
                return;
            }

            for (int i = 0; i < lines.Count; i++)
                builder.AppendLine((i + 1) + ". " + lines[i]);
        }

        private string WriteBatchPlanReport(ESSoTableRuleUseBatch batch, string report)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SoTableConfig", "Plans"));
            Directory.CreateDirectory(root);
            string reportName = SanitizeAssetFileName(FirstNotEmptyLocal(batch != null ? batch.batchName : null, ruleKey, this.name, "SoTablePlan"));
            string path = Path.Combine(root, reportName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(path, report ?? string.Empty, new UTF8Encoding(true));
            return path;
        }

        private static void AddPlanError(ESTablePlanRiskSummary summary, string message)
        {
            if (summary == null)
                return;

            summary.errors++;
            if (summary.errorLines.Count < 500)
                summary.errorLines.Add(message);
        }

        private static void AddPlanRisk(ESTablePlanRiskSummary summary, string message)
        {
            if (summary == null)
                return;

            if (summary.riskLines.Count < 500)
                summary.riskLines.Add(message);
        }

        private static void MergePlanSummary(ESTablePlanRiskSummary target, ESTablePlanRiskSummary source)
        {
            if (target == null || source == null)
                return;

            target.addedOwners += source.addedOwners;
            target.addedChildren += source.addedChildren;
            target.modifiedFields += source.modifiedFields;
            target.skippedRows += source.skippedRows;
            target.errors += source.errors;
            target.deleteOwners += source.deleteOwners;
            target.deleteChildren += source.deleteChildren;
            target.clearFields += source.clearFields;
            target.overwriteNonEmptyFields += source.overwriteNonEmptyFields;
            target.rebuildTables += source.rebuildTables;
            target.errorLines.AddRange(source.errorLines);
            target.riskLines.AddRange(source.riskLines);
        }

        private static string FirstNotEmptyLocal(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static string DescribePlanObject(UnityEngine.Object target)
        {
            if (target == null)
                return "(空)";

            string path = AssetDatabase.GetAssetPath(target);
            return string.IsNullOrEmpty(path) ? target.name : target.name + " | " + path;
        }

        private static bool IsIdentityPlanColumn(ESTableColumnNameMap column)
        {
            return column != null && (column.isInfoKey || column.isGroupKey);
        }

        private static string GetTypeName(Type type)
        {
            return type != null ? type.Name : "(未知类型)";
        }

        private static string BuildImportCellFixSuggestion(Type targetType, ESTableValueWriteMode writeMode)
        {
            if (targetType == null)
                return "检查字段映射是否存在。";
            if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short))
                return "填写整数，例如 1。";
            if (targetType == typeof(float) || targetType == typeof(double) || targetType == typeof(decimal))
                return "填写数字，例如 1 或 1.5。";
            if (targetType == typeof(bool))
                return "填写 true/false 或 1/0。";
            if (targetType.IsEnum)
                return "填写枚举名、数字值或字段中文显示名。";
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                return writeMode == ESTableValueWriteMode.UnityObjectPath ? "填写 Unity 资源路径。" : "填写 Unity 资源 GUID 或资源路径。";
            if (ShouldUseJsonCell(targetType))
                return "填写合法 JSON，例如对象用 {\"field\":1}，数组用 [1,2]。";
            return "检查单元格格式和目标字段类型。";
        }
    }
}
#endif
