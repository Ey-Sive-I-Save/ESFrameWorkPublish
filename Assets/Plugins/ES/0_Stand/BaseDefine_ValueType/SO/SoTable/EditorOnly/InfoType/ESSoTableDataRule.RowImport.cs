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
        #region Row Import Directives And Value Conversion
        private ESTableBatchApplyFilter BuildApplyFilter(List<List<string>> table, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            var filter = new ESTableBatchApplyFilter
            {
                mode = batch != null ? batch.applyRangeMode : ESTableBatchApplyRangeMode.All,
                startValue = batch != null ? batch.sliceStartValue : string.Empty,
                endValue = batch != null ? batch.sliceEndValue : string.Empty,
                includeStart = batch == null || batch.includeSliceStart,
                includeEnd = batch == null || batch.includeSliceEnd,
                targetGroupKey = batch != null ? batch.targetGroupKey : string.Empty,
                targetInfoKey = batch != null ? batch.targetInfoKey : string.Empty
            };

            if (filter.mode == ESTableBatchApplyRangeMode.All)
                return filter;

            string sliceColumnName = batch != null ? batch.sliceColumnName : string.Empty;
            filter.sliceColumnIndex = FindRawTableColumnIndex(table, string.IsNullOrWhiteSpace(sliceColumnName) ? GetActiveInfoKeyColumnName() : sliceColumnName);
            filter.groupColumnIndex = FindRawTableColumnIndex(table, groupColumnName);
            filter.infoColumnIndex = FindRawTableColumnIndex(table, GetActiveInfoKeyColumnName());

            if (filter.sliceColumnIndex < 0)
                filter.sliceColumnIndex = FindTableColumnIndex(tableColumnMap, GetActiveInfoKeyColumnName());
            if (filter.infoColumnIndex < 0)
                filter.infoColumnIndex = FindTableColumnIndex(tableColumnMap, GetActiveInfoKeyColumnName());

            return filter;
        }

        private bool ShouldApplyTableRow(List<string> row, ESTableBatchApplyFilter filter)
        {
            if (filter == null || filter.mode == ESTableBatchApplyRangeMode.All)
                return true;

            if (filter.mode == ESTableBatchApplyRangeMode.SingleGroupInfo)
            {
                bool groupMatched = string.IsNullOrWhiteSpace(filter.targetGroupKey) || GetCell(row, filter.groupColumnIndex) == filter.targetGroupKey;
                bool infoMatched = string.IsNullOrWhiteSpace(filter.targetInfoKey) || GetCell(row, filter.infoColumnIndex) == filter.targetInfoKey;
                return groupMatched && infoMatched;
            }

            if (filter.mode != ESTableBatchApplyRangeMode.Slice)
                return true;

            string value = GetCell(row, filter.sliceColumnIndex);
            bool isStart = !string.IsNullOrWhiteSpace(filter.startValue) && value == filter.startValue;
            bool isEnd = !string.IsNullOrWhiteSpace(filter.endValue) && value == filter.endValue;

            if (filter.sliceFinished)
                return false;

            if (!filter.sliceStarted)
            {
                if (string.IsNullOrWhiteSpace(filter.startValue))
                    filter.sliceStarted = true;
                else if (isStart)
                    filter.sliceStarted = true;
                else
                    return false;

                if (isStart && !filter.includeStart)
                    return false;
            }

            if (isEnd)
            {
                filter.sliceFinished = true;
                return filter.includeEnd;
            }

            return filter.sliceStarted;
        }

        private bool ConfirmImportRiskBeforeWrite(List<List<string>> table, string path)
        {
            if (suppressImportRiskConfirmation)
                return true;

            ESTablePlanRiskSummary assertionSummary = BuildImportAssertionSummary(table);
            if (assertionSummary.HasErrors)
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("表格断言预检失败，已阻止写入 SO。");
                errorBuilder.AppendLine("表格：" + path);
                for (int i = 0; i < assertionSummary.errorLines.Count; i++)
                    errorBuilder.AppendLine(assertionSummary.errorLines[i]);
                Debug.LogWarning(errorBuilder.ToString(), this);
                EditorUtility.DisplayDialog("SO 表格断言失败", "表格断言预检失败，已阻止写入 SO。请查看 Console 里的行列错误。", "知道了");
                return false;
            }

            ESTablePlanRiskSummary fullSummary = BuildBatchRiskSummary(GetActiveUseBatch());
            return ConfirmPlanRiskBeforeExecute(fullSummary, GetActiveUseBatch());
        }
        private ESTablePlanRiskSummary BuildImportAssertionSummary(List<List<string>> table)
        {
            var summary = new ESTablePlanRiskSummary();
            if (table == null || FindAssertRowIndex(table) < 0)
                return summary;

            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            Dictionary<string, ESTableColumnNameMap> columnMap = BuildColumnMap(enabledColumns);
            Dictionary<int, ESTableColumnNameMap> tableColumnMap = BuildTableColumnMap(table, columnMap);
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, tableColumnMap);
            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            ValidateColumnAssertions(table, compiledColumns, applyFilter, summary);
            return summary;
        }

        private static int CountDirectiveRows(List<List<string>> table, ESTableRowDirective directive)
        {
            if (table == null)
                return 0;

            int count = 0;
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                if (ParseRowDirective(table[rowIndex]).directive == directive)
                    count++;
            }

            return count;
        }

        private static ESTableRowDirectiveInfo ParseRowDirective(List<string> row)
        {
            string raw = GetCell(row, 0).Trim();
            var result = new ESTableRowDirectiveInfo
            {
                rawText = raw,
                directive = ESTableRowDirective.Normal
            };

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string normalized = raw.Trim().ToLowerInvariant();
            if (normalized == "debug")
            {
                result.debug = true;
                result.effectiveText = string.Empty;
                return result;
            }

            if (normalized.StartsWith("debug:", StringComparison.Ordinal))
            {
                result.debug = true;
                normalized = normalized.Substring("debug:".Length).Trim();
                result.effectiveText = normalized;
            }

            int chineseStart = normalized.IndexOf('(');
            if (chineseStart >= 0)
                normalized = normalized.Substring(0, chineseStart).Trim();

            if (normalized.StartsWith("comment", StringComparison.Ordinal))
                result.directive = ESTableRowDirective.Comment;
            else if (normalized == "skip" || normalized == "ignore" || normalized == "disabled")
                result.directive = ESTableRowDirective.Skip;
            else if (normalized == "required")
                result.directive = ESTableRowDirective.Required;
            else if (normalized == "patch")
                result.directive = ESTableRowDirective.Patch;
            else if (normalized == "replace")
                result.directive = ESTableRowDirective.Replace;
            else if (normalized == "owner")
                result.directive = ESTableRowDirective.Owner;
            else if (normalized == "delete")
                result.directive = ESTableRowDirective.Delete;

            if (string.IsNullOrEmpty(result.effectiveText))
                result.effectiveText = normalized;

            return result;
        }

        private static bool IsDataRowEmpty(List<string> row)
        {
            if (row == null)
                return true;

            for (int i = 1; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                    return false;
            }

            return true;
        }

        private static int GetDataStartRowIndex(List<List<string>> table)
        {
            int index = 4;
            if (table == null)
                return index;

            while (index < table.Count)
            {
                string marker = GetCell(table[index], 0).Trim();
                if (!marker.StartsWith("##", StringComparison.Ordinal))
                    break;
                index++;
            }

            return index;
        }

        private static bool TryDeleteOwnerAsset(ScriptableObject owner, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "目标 SO 为空。";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(owner);
            if (string.IsNullOrWhiteSpace(path))
            {
                reason = "目标不是可删除的普通资产。";
                return false;
            }

            return AssetDatabase.DeleteAsset(path);
        }

        #endregion
    }
}
#endif
