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
        #region Export Merge And Owner Row Projection
        private List<ESTableColumnNameMap> GetEnabledColumns()
        {
            var enabledColumns = new List<ESTableColumnNameMap>();
            if (columns == null)
                return enabledColumns;

            for (int i = 0; i < columns.Count; i++)
            {
                ESTableColumnNameMap column = columns[i];
                if (column != null && column.IsUsable && !string.IsNullOrWhiteSpace(column.columnName) && IsColumnAllowedByActiveBatch(column))
                    enabledColumns.Add(column);
            }

            return enabledColumns;
        }

        private bool IsColumnAllowedByActiveBatch(ESTableColumnNameMap column)
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            if (batch == null || column == null)
                return true;

            HashSet<string> excluded = BuildFieldNameSet(batch.excludedFields);
            if (MatchesFieldNameSet(column, excluded))
                return false;

            HashSet<string> active = BuildFieldNameSet(batch.activeFields);
            return active.Count == 0 || MatchesFieldNameSet(column, active);
        }

        private static HashSet<string> BuildFieldNameSet(string text)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] parts = text.Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (!string.IsNullOrEmpty(item))
                    result.Add(item);
            }

            return result;
        }

        private static bool MatchesFieldNameSet(ESTableColumnNameMap column, HashSet<string> names)
        {
            if (column == null || names == null || names.Count == 0)
                return false;

            return (!string.IsNullOrWhiteSpace(column.columnName) && names.Contains(column.columnName))
                || (!string.IsNullOrWhiteSpace(column.soFieldPath) && names.Contains(column.soFieldPath))
                || (!string.IsNullOrWhiteSpace(column.displayName) && names.Contains(column.displayName));
        }

        private List<ESTableColumnNameMap> CollectLockedColumns()
        {
            var result = new List<ESTableColumnNameMap>();
            if (columns == null)
                return result;

            for (int i = 0; i < columns.Count; i++)
            {
                ESTableColumnNameMap column = columns[i];
                if (column != null && column.locked)
                    result.Add(column);
            }

            return result;
        }

        private void RestoreLockedColumns(List<ESTableColumnNameMap> lockedColumns)
        {
            if (lockedColumns == null || lockedColumns.Count == 0)
                return;
            if (columns == null)
                columns = new List<ESTableColumnNameMap>();

            for (int i = 0; i < lockedColumns.Count; i++)
            {
                ESTableColumnNameMap lockedColumn = lockedColumns[i];
                if (lockedColumn != null && !ContainsEquivalentColumn(columns, lockedColumn))
                    columns.Add(lockedColumn);
            }
        }

        private static bool ContainsEquivalentColumn(List<ESTableColumnNameMap> list, ESTableColumnNameMap column)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ESTableColumnNameMap item = list[i];
                if (item == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(column.soFieldPath) && item.soFieldPath == column.soFieldPath)
                    return true;
                if (!string.IsNullOrWhiteSpace(column.columnName) && item.columnName == column.columnName)
                    return true;
            }

            return false;
        }

        private static List<string> BuildHeaderRow(string mark, List<ESTableColumnNameMap> enabledColumns, Func<ESTableColumnNameMap, string> valueGetter)
        {
            var row = new List<string>(enabledColumns.Count + 1) { mark };
            for (int i = 0; i < enabledColumns.Count; i++)
                row.Add(valueGetter(enabledColumns[i]) ?? string.Empty);

            return row;
        }

        private void AppendPassThroughColumnsIfNeeded(List<List<string>> table, List<ESTableColumnNameMap> enabledColumns)
        {
            if (!ShouldPassThroughUnmappedColumns(enabledColumns))
                return;

            string existingPath = ResolveExistingInputPath();
            if (string.IsNullOrEmpty(existingPath) || !File.Exists(existingPath))
                return;

            List<List<string>> oldTable = ReadTableFileAuto(existingPath);
            if (oldTable.Count == 0 || oldTable[0].Count == 0)
                return;

            HashSet<string> mappedColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(enabledColumns[i].columnName))
                    mappedColumnNames.Add(enabledColumns[i].columnName);
            }

            int oldStart = oldTable[0].Count > 0 && IsHeaderMark(oldTable[0][0]) ? 1 : 0;
            var passThroughIndices = new List<int>();
            for (int i = oldStart; i < oldTable[0].Count; i++)
            {
                string oldColumnName = oldTable[0][i];
                if (!string.IsNullOrWhiteSpace(oldColumnName) && !mappedColumnNames.Contains(oldColumnName))
                    passThroughIndices.Add(i);
            }

            if (passThroughIndices.Count == 0)
                return;

            for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                List<string> oldRow = rowIndex < oldTable.Count ? oldTable[rowIndex] : null;
                for (int i = 0; i < passThroughIndices.Count; i++)
                {
                    int oldColumnIndex = passThroughIndices[i];
                    row.Add(oldRow != null && oldColumnIndex < oldRow.Count ? oldRow[oldColumnIndex] : string.Empty);
                }
            }
        }

        private static bool ShouldPassThroughUnmappedColumns(List<ESTableColumnNameMap> enabledColumns)
        {
            if (enabledColumns == null)
                return false;

            for (int i = 0; i < enabledColumns.Count; i++)
            {
                if (enabledColumns[i] != null && enabledColumns[i].allowPassThrough)
                    return true;
            }

            return false;
        }

        private List<List<string>> BuildExportWriteTable(List<List<string>> newTable, string path)
        {
            if (newTable == null)
                return null;

            ESTableExportWriteMode mode = GetActiveExportWriteMode();
            ESTableSerialChildWriteMode childMode = GetActiveSerialChildWriteMode();
            bool serialChildRows = rowBinding != null && rowBinding.IsListElementRow;
            if (mode == ESTableExportWriteMode.Rebuild || string.IsNullOrEmpty(path) || !File.Exists(path))
                return CloneTable(newTable);

            List<List<string>> oldTable = ReadTableFileAuto(path);
            if (oldTable == null || oldTable.Count == 0)
                return CloneTable(newTable);

            if (serialChildRows && childMode == ESTableSerialChildWriteMode.RebuildByOwner && rowBinding.allowEmptyRowKey)
                return BuildExportWriteTableRebuildOwnersAllowEmptyKeys(newTable, oldTable);

            int oldKeyIndex = FindExportObjectKeyColumnIndex(oldTable);
            int newKeyIndex = FindExportObjectKeyColumnIndex(newTable);
            if (oldKeyIndex < 0 || newKeyIndex < 0)
            {
                Debug.LogWarning(BuildTableRowErrorMessage("导出合并", -1, this, "缺少对象唯一 Key 列。", "在字段映射中标记 Info Key/对象 Key，或恢复表格里的唯一 Key 列。"), this);
                return null;
            }

            int oldRowKeyIndex = FindExportRowKeyColumnIndex(oldTable);
            int newRowKeyIndex = FindExportRowKeyColumnIndex(newTable);
            if (rowBinding != null && rowBinding.IsListElementRow && (oldRowKeyIndex < 0 || newRowKeyIndex < 0))
            {
                Debug.LogWarning(BuildTableRowErrorMessage("导出合并", -1, this, "List 串行行缺少子级 RowKey 列。", "在行绑定里选择行 Key 列名，并确保表格没有删除该列。"), this);
                return null;
            }

            List<List<string>> result = CloneTable(oldTable);
            MergeExportHeaders(result, newTable);

            Dictionary<string, int> oldRowsByKey = BuildExportRowsByKey(result, oldKeyIndex, oldRowKeyIndex, out string oldKeyError);
            if (!string.IsNullOrEmpty(oldKeyError))
            {
                Debug.LogWarning(BuildTableRowErrorMessage("导出合并", -1, this, "已有表格 Key 数据无效：" + oldKeyError, "检查旧表唯一 Key/RowKey 是否为空或重复。"), this);
                return null;
            }

            Dictionary<string, int> resultColumnMap = BuildRawHeaderIndexMap(result);
            Dictionary<string, int> newColumnMap = BuildRawHeaderIndexMap(newTable);
            HashSet<string> newKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> touchedOwnerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentNewObjectKey = null;
            for (int rowIndex = GetDataStartRowIndex(newTable); rowIndex < newTable.Count; rowIndex++)
            {
                List<string> newRow = newTable[rowIndex];
                string explicitObjectKey = BuildExportObjectKey(newRow, newKeyIndex);
                if (!string.IsNullOrWhiteSpace(explicitObjectKey))
                    currentNewObjectKey = explicitObjectKey;

                string key = BuildExportRowKey(newRow, newKeyIndex, newRowKeyIndex, currentNewObjectKey);
                if (string.IsNullOrWhiteSpace(key))
                {
                    Debug.LogWarning(BuildTableRowErrorMessage("导出合并", rowIndex, this, "新导出数据 Key 为空。", "检查 SO 的唯一 Key 和子级 RowKey 字段。"), this);
                    return null;
                }
                if (!newKeys.Add(key))
                {
                    Debug.LogWarning(BuildTableRowErrorMessage("导出合并", rowIndex, this, "新导出数据 Key 重复：" + key, "检查 SO/子级里是否存在重复 Key。"), this);
                    return null;
                }
                if (serialChildRows)
                {
                    string ownerKey = BuildExportObjectKey(newRow, newKeyIndex);
                    if (string.IsNullOrWhiteSpace(ownerKey))
                        ownerKey = currentNewObjectKey;
                    if (!string.IsNullOrWhiteSpace(ownerKey))
                        touchedOwnerKeys.Add(ownerKey);
                }

                if (oldRowsByKey.TryGetValue(key, out int targetRowIndex))
                {
                    MergeExportDataRow(result[targetRowIndex], newRow, newColumnMap, resultColumnMap);
                    continue;
                }

                if (ShouldAppendExportRow(serialChildRows, mode, childMode))
                {
                    List<string> appended = new List<string>();
                    EnsureCellCount(appended, result[0].Count);
                    MergeExportDataRow(appended, newRow, newColumnMap, resultColumnMap);
                    result.Add(appended);
                    oldRowsByKey[key] = result.Count - 1;
                }
            }

            if (serialChildRows && childMode == ESTableSerialChildWriteMode.RebuildByOwner)
                RemoveMissingSerialChildRowsForOwners(result, oldKeyIndex, oldRowKeyIndex, touchedOwnerKeys, newKeys);

            return result;
        }

        private ESTableExportWriteMode GetActiveExportWriteMode()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.exportWriteMode : ESTableExportWriteMode.MergeByKey;
        }

        private ESTableConflictPolicy GetActiveImportConflictPolicy()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.importConflictPolicy : ESTableConflictPolicy.Overwrite;
        }

        private ESTableSerialChildWriteMode GetActiveSerialChildWriteMode()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.serialChildWriteMode : ESTableSerialChildWriteMode.RebuildByOwner;
        }

        private ESTableSerialChildImportSyncMode GetActiveSerialChildImportSyncMode()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.serialChildImportSyncMode : ESTableSerialChildImportSyncMode.KeepMissing;
        }

        private static bool ShouldAppendExportRow(bool serialChildRows, ESTableExportWriteMode tableMode, ESTableSerialChildWriteMode childMode)
        {
            if (!serialChildRows)
                return tableMode == ESTableExportWriteMode.MergeByKey;

            return childMode == ESTableSerialChildWriteMode.MergeByKey
                || childMode == ESTableSerialChildWriteMode.RebuildByOwner;
        }

        private void RemoveMissingSerialChildRowsForOwners(List<List<string>> table, int objectKeyIndex, int rowKeyIndex, HashSet<string> touchedOwnerKeys, HashSet<string> newKeys)
        {
            if (table == null || touchedOwnerKeys == null || touchedOwnerKeys.Count == 0 || newKeys == null)
                return;

            var effectiveOwnerKeysByRow = new Dictionary<int, string>();
            string currentOwnerKey = null;
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                string explicitOwnerKey = BuildExportObjectKey(table[rowIndex], objectKeyIndex);
                if (!string.IsNullOrWhiteSpace(explicitOwnerKey))
                    currentOwnerKey = explicitOwnerKey;
                if (!string.IsNullOrWhiteSpace(currentOwnerKey))
                    effectiveOwnerKeysByRow[rowIndex] = currentOwnerKey;
            }

            for (int rowIndex = table.Count - 1; rowIndex >= GetDataStartRowIndex(table); rowIndex--)
            {
                List<string> row = table[rowIndex];
                effectiveOwnerKeysByRow.TryGetValue(rowIndex, out string ownerKey);
                if (string.IsNullOrWhiteSpace(ownerKey) || !touchedOwnerKeys.Contains(ownerKey))
                    continue;

                string fullKey = BuildExportRowKey(row, objectKeyIndex, rowKeyIndex, ownerKey);
                if (!newKeys.Contains(fullKey))
                    table.RemoveAt(rowIndex);
            }
        }

        private List<List<string>> BuildExportWriteTableRebuildOwnersAllowEmptyKeys(List<List<string>> newTable, List<List<string>> oldTable)
        {
            int oldObjectKeyIndex = FindExportObjectKeyColumnIndex(oldTable);
            int newObjectKeyIndex = FindExportObjectKeyColumnIndex(newTable);
            if (oldObjectKeyIndex < 0 || newObjectKeyIndex < 0)
            {
                Debug.LogWarning("Export stopped: missing object Key column. Empty child Key rebuild still requires an owner/object Key column.", this);
                return null;
            }

            List<List<string>> result = CloneTable(oldTable);
            MergeExportHeaders(result, newTable);

            Dictionary<string, int> resultColumnMap = BuildRawHeaderIndexMap(result);
            Dictionary<string, int> newColumnMap = BuildRawHeaderIndexMap(newTable);
            HashSet<string> touchedOwnerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string currentNewOwnerKey = null;
            for (int rowIndex = GetDataStartRowIndex(newTable); rowIndex < newTable.Count; rowIndex++)
            {
                string ownerKey = BuildExportObjectKey(newTable[rowIndex], newObjectKeyIndex);
                if (!string.IsNullOrWhiteSpace(ownerKey))
                    currentNewOwnerKey = ownerKey;
                else
                    ownerKey = currentNewOwnerKey;

                if (string.IsNullOrWhiteSpace(ownerKey))
                {
                    Debug.LogWarning("Export stopped: new table contains an empty owner Key at row " + (rowIndex + 1) + ".", this);
                    return null;
                }

                touchedOwnerKeys.Add(ownerKey);
            }

            var effectiveOldOwnerKeysByRow = new Dictionary<int, string>();
            string currentOldOwnerKey = null;
            for (int rowIndex = GetDataStartRowIndex(result); rowIndex < result.Count; rowIndex++)
            {
                string ownerKey = BuildExportObjectKey(result[rowIndex], oldObjectKeyIndex);
                if (!string.IsNullOrWhiteSpace(ownerKey))
                    currentOldOwnerKey = ownerKey;
                if (!string.IsNullOrWhiteSpace(currentOldOwnerKey))
                    effectiveOldOwnerKeysByRow[rowIndex] = currentOldOwnerKey;
            }

            for (int rowIndex = result.Count - 1; rowIndex >= GetDataStartRowIndex(result); rowIndex--)
            {
                effectiveOldOwnerKeysByRow.TryGetValue(rowIndex, out string ownerKey);
                if (!string.IsNullOrWhiteSpace(ownerKey) && touchedOwnerKeys.Contains(ownerKey))
                    result.RemoveAt(rowIndex);
            }

            for (int rowIndex = GetDataStartRowIndex(newTable); rowIndex < newTable.Count; rowIndex++)
            {
                List<string> appended = new List<string>();
                EnsureCellCount(appended, result[0].Count);
                MergeExportDataRow(appended, newTable[rowIndex], newColumnMap, resultColumnMap);
                result.Add(appended);
            }

            return result;
        }

        private int FindExportObjectKeyColumnIndex(List<List<string>> table)
        {
            int index = FindRawTableColumnIndex(table, GetActiveInfoKeyColumnName());
            if (index >= 0)
                return index;

            string[] names = { "itemId", "id", "key", "KeyName", "name" };
            for (int i = 0; i < names.Length; i++)
            {
                index = FindRawTableColumnIndex(table, names[i]);
                if (index >= 0)
                    return index;
            }

            return -1;
        }

        private int FindExportRowKeyColumnIndex(List<List<string>> table)
        {
            return rowBinding != null && rowBinding.IsListElementRow
                ? FindRawTableColumnIndex(table, rowBinding.rowKeyColumnName)
                : -1;
        }

        private string BuildExportRowKey(List<string> row, int objectKeyIndex, int rowKeyIndex, string inheritedObjectKey = null)
        {
            string objectKey = BuildExportObjectKey(row, objectKeyIndex);
            if (string.IsNullOrWhiteSpace(objectKey))
                objectKey = inheritedObjectKey;
            if (string.IsNullOrWhiteSpace(objectKey))
                return string.Empty;

            if (rowBinding == null || !rowBinding.IsListElementRow)
                return objectKey;

            string rowKey = GetCell(row, rowKeyIndex).Trim();
            if (string.IsNullOrWhiteSpace(rowKey))
                return string.Empty;

            return objectKey + "\u001f" + rowKey;
        }

        private static string BuildExportObjectKey(List<string> row, int objectKeyIndex)
        {
            return GetCell(row, objectKeyIndex).Trim();
        }

        private Dictionary<string, int> BuildExportRowsByKey(List<List<string>> table, int objectKeyIndex, int rowKeyIndex, out string error)
        {
            error = string.Empty;
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string currentObjectKey = null;
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                string explicitObjectKey = BuildExportObjectKey(table[rowIndex], objectKeyIndex);
                if (!string.IsNullOrWhiteSpace(explicitObjectKey))
                    currentObjectKey = explicitObjectKey;

                string key = BuildExportRowKey(table[rowIndex], objectKeyIndex, rowKeyIndex, currentObjectKey);
                if (string.IsNullOrWhiteSpace(key))
                {
                    error = "Empty Key at row " + (rowIndex + 1) + ".";
                    return result;
                }
                if (result.ContainsKey(key))
                {
                    error = "Duplicate Key " + key + " at row " + (rowIndex + 1) + ".";
                    return result;
                }

                result[key] = rowIndex;
            }

            return result;
        }

        private static Dictionary<string, int> BuildRawHeaderIndexMap(List<List<string>> table)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (table == null || table.Count == 0)
                return result;

            List<string> headerRow = table[0];
            for (int i = 1; i < headerRow.Count; i++)
            {
                string name = headerRow[i];
                if (!string.IsNullOrWhiteSpace(name) && !result.ContainsKey(name))
                    result[name] = i;
            }

            return result;
        }

        private static void MergeExportHeaders(List<List<string>> result, List<List<string>> newTable)
        {
            EnsureExportHeaderRows(result, newTable);

            int newHeaderRowCount = Math.Min(GetDataStartRowIndex(newTable), newTable.Count);
            Dictionary<string, int> resultColumnMap = BuildRawHeaderIndexMap(result);
            for (int columnIndex = 1; newTable.Count > 0 && columnIndex < newTable[0].Count; columnIndex++)
            {
                string columnName = newTable[0][columnIndex];
                if (string.IsNullOrWhiteSpace(columnName))
                    continue;
                if (TryFindCompatibleExportColumnIndex(result, newTable, columnIndex, resultColumnMap, out int existingIndex))
                {
                    for (int headerRow = 0; headerRow < newHeaderRowCount; headerRow++)
                    {
                        int targetHeaderRow = FindMatchingExportHeaderRowIndex(result, newTable[headerRow], headerRow);
                        if (targetHeaderRow < 0)
                            continue;

                        EnsureCellCount(result[targetHeaderRow], existingIndex + 1);
                        result[targetHeaderRow][existingIndex] = GetCell(newTable[headerRow], columnIndex);
                    }

                    resultColumnMap[columnName] = existingIndex;
                    continue;
                }

                int addedIndex = result[0].Count;
                for (int rowIndex = 0; rowIndex < result.Count; rowIndex++)
                    EnsureCellCount(result[rowIndex], addedIndex + 1);
                for (int headerRow = 0; headerRow < newHeaderRowCount; headerRow++)
                {
                    int targetHeaderRow = FindMatchingExportHeaderRowIndex(result, newTable[headerRow], headerRow);
                    if (targetHeaderRow < 0)
                        continue;

                    EnsureCellCount(result[targetHeaderRow], addedIndex + 1);
                    result[targetHeaderRow][addedIndex] = GetCell(newTable[headerRow], columnIndex);
                }

                resultColumnMap[columnName] = addedIndex;
            }
        }

        private static bool TryFindCompatibleExportColumnIndex(List<List<string>> result, List<List<string>> newTable, int newColumnIndex, Dictionary<string, int> resultColumnMap, out int resultColumnIndex)
        {
            resultColumnIndex = -1;
            if (result == null || newTable == null || newTable.Count == 0 || resultColumnMap == null)
                return false;

            string newName = GetCell(newTable[0], newColumnIndex);
            if (!string.IsNullOrWhiteSpace(newName) && resultColumnMap.TryGetValue(newName, out resultColumnIndex))
                return true;

            int newCommentRow = newTable.Count > 3 ? 3 : -1;
            string newComment = newCommentRow >= 0 ? GetCell(newTable[newCommentRow], newColumnIndex) : string.Empty;
            if (string.IsNullOrWhiteSpace(newComment))
                return false;

            int resultCommentRow = FindMatchingExportHeaderRowIndex(result, newTable[newCommentRow], newCommentRow);
            if (resultCommentRow < 0 || result.Count == 0)
                return false;

            for (int i = 1; i < result[0].Count; i++)
            {
                string resultName = GetCell(result[0], i);
                string resultComment = GetCell(result[resultCommentRow], i);
                if (string.Equals(resultName, newComment, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(resultComment, newName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(resultComment, newComment, StringComparison.OrdinalIgnoreCase))
                {
                    resultColumnIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static void EnsureExportHeaderRows(List<List<string>> result, List<List<string>> newTable)
        {
            if (result == null || newTable == null || newTable.Count == 0)
                return;

            int newHeaderRowCount = Math.Min(GetDataStartRowIndex(newTable), newTable.Count);
            EnsureRowCount(result, Math.Min(4, newHeaderRowCount));

            int insertIndex = GetDataStartRowIndex(result);
            for (int rowIndex = 4; rowIndex < newHeaderRowCount; rowIndex++)
            {
                List<string> newHeaderRow = newTable[rowIndex];
                string identity = GetExportHeaderIdentity(newHeaderRow);
                if (string.IsNullOrEmpty(identity) || FindExportHeaderRowByIdentity(result, identity) >= 0)
                    continue;

                List<string> inserted = BuildHeaderRowAlignedToResult(newHeaderRow, newTable, result);
                result.Insert(insertIndex, inserted);
                insertIndex++;
            }
        }

        private static List<string> BuildHeaderRowAlignedToResult(List<string> newHeaderRow, List<List<string>> newTable, List<List<string>> result)
        {
            int targetCount = result != null && result.Count > 0 ? result[0].Count : 0;
            var row = new List<string>();
            EnsureCellCount(row, targetCount);
            if (targetCount == 0)
                return row;

            row[0] = GetCell(newHeaderRow, 0);
            Dictionary<string, int> newColumnMap = BuildRawHeaderIndexMap(newTable);
            for (int columnIndex = 1; columnIndex < targetCount; columnIndex++)
            {
                string columnName = GetCell(result[0], columnIndex);
                if (!string.IsNullOrWhiteSpace(columnName) && newColumnMap.TryGetValue(columnName, out int newColumnIndex))
                    row[columnIndex] = GetCell(newHeaderRow, newColumnIndex);
            }

            return row;
        }

        private static int FindMatchingExportHeaderRowIndex(List<List<string>> table, List<string> newHeaderRow, int fallbackIndex)
        {
            string identity = GetExportHeaderIdentity(newHeaderRow);
            int matched = FindExportHeaderRowByIdentity(table, identity);
            if (matched >= 0)
                return matched;

            return fallbackIndex >= 0 && table != null && fallbackIndex < table.Count ? fallbackIndex : -1;
        }

        private static int FindExportHeaderRowByIdentity(List<List<string>> table, string identity)
        {
            if (table == null || string.IsNullOrEmpty(identity))
                return -1;

            int headerRowCount = Math.Min(GetDataStartRowIndex(table), table.Count);
            for (int rowIndex = 0; rowIndex < headerRowCount; rowIndex++)
            {
                if (GetExportHeaderIdentity(table[rowIndex]) == identity)
                    return rowIndex;
            }

            return -1;
        }

        private static string GetExportHeaderIdentity(List<string> row)
        {
            string marker = GetCell(row, 0).Trim();
            if (string.IsNullOrEmpty(marker))
                return string.Empty;
            if (marker.StartsWith("##assert", StringComparison.OrdinalIgnoreCase))
                return "##assert";
            if (marker.StartsWith("##rowDirective", StringComparison.OrdinalIgnoreCase))
                return "##rowDirective";

            return marker;
        }

        private static void MergeExportDataRow(List<string> targetRow, List<string> sourceRow, Dictionary<string, int> sourceColumnMap, Dictionary<string, int> targetColumnMap)
        {
            EnsureCellCount(targetRow, targetColumnMap.Count + 1);
            foreach (KeyValuePair<string, int> pair in sourceColumnMap)
            {
                if (!targetColumnMap.TryGetValue(pair.Key, out int targetIndex))
                    continue;

                EnsureCellCount(targetRow, targetIndex + 1);
                targetRow[targetIndex] = GetCell(sourceRow, pair.Value);
            }
        }

        private static List<List<string>> CloneTable(List<List<string>> table)
        {
            var result = new List<List<string>>();
            if (table == null)
                return result;

            for (int i = 0; i < table.Count; i++)
                result.Add(table[i] == null ? new List<string>() : new List<string>(table[i]));
            return result;
        }

        private static void EnsureRowCount(List<List<string>> table, int count)
        {
            while (table.Count < count)
                table.Add(new List<string>());
        }

        private static void EnsureCellCount(List<string> row, int count)
        {
            while (row.Count < count)
                row.Add(string.Empty);
        }

        private List<ScriptableObject> CollectExportOwners()
        {
            if (TryGetCachedOwners(out List<ScriptableObject> cachedOwners))
                return cachedOwners;

            var owners = new List<ScriptableObject>();
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();

            if (source != null && source.soFolder != null)
            {
                if (source.folderAssets == null)
                    source.folderAssets = new List<ScriptableObject>();

                if (source.folderAssets.Count == 0)
                {
                    source.folderAssets = CollectScriptableObjectsFromFolder(source.soFolder, source.includeSubFolders);
                    EditorUtility.SetDirty(this);
                }

                AddMatchingOwners(owners, source.folderAssets);
                SetCachedOwners(owners);
                return owners;
            }

            if (source != null && source.soAsset != null)
            {
                AddOwnerOrSoDataChildren(owners, source.soAsset);
                SetCachedOwners(owners);
                return owners;
            }

            AddMatchingOwners(owners, source != null ? source.folderAssets : null);
            SetCachedOwners(owners);
            return owners;
        }

        private void AddMatchingOwners(List<ScriptableObject> owners, List<ScriptableObject> assets)
        {
            if (assets == null)
                return;

            Type ownerType = typeBinding.RowOwnerType;
            for (int i = 0; i < assets.Count; i++)
            {
                ScriptableObject asset = assets[i];
                if (asset == null)
                    continue;

                if (ownerType == null || ownerType.IsAssignableFrom(asset.GetType()) || asset is ISoDataPack || asset is ISoDataGroup)
                    AddOwnerOrSoDataChildren(owners, asset);
            }
        }

        private void AddOwnerOrSoDataChildren(List<ScriptableObject> owners, ScriptableObject asset)
        {
            if (asset == null)
                return;

            Type ownerType = typeBinding.RowOwnerType;
            if (asset is ISoDataPack pack)
            {
                foreach (object value in pack.AllInfos.Values)
                {
                    if (value is ScriptableObject so && (ownerType == null || ownerType.IsAssignableFrom(so.GetType())))
                        owners.Add(so);
                }
                return;
            }

            if (asset is ISoDataGroup group)
            {
                foreach (ISoDataInfo info in group.AllInfos)
                {
                    if (info is ScriptableObject so && (ownerType == null || ownerType.IsAssignableFrom(so.GetType())))
                        owners.Add(so);
                }
                return;
            }

            if (ownerType == null || ownerType.IsAssignableFrom(asset.GetType()))
                owners.Add(asset);
        }

        private void AppendOwnerRows(List<List<string>> table, ScriptableObject owner, List<ESTableCompiledColumn> compiledColumns)
        {
            if (owner == null)
                return;

            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
                if (dictionary != null)
                {
                    var stringKeys = new List<string>();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (entry.Key is string key)
                            stringKeys.Add(key);
                    }

                    stringKeys.Sort(StringComparer.Ordinal);
                    for (int i = 0; i < stringKeys.Count; i++)
                    {
                        object rowObject = dictionary[stringKeys[i]];
                        if (rowObject != null)
                            table.Add(BuildDataRow(owner, rowObject, compiledColumns, stringKeys[i], i == 0));
                    }

                    return;
                }

                bool wroteOwnerColumns = false;
                foreach (object rowObject in RowBridge.EnumerateRows(owner, rowBinding))
                {
                    if (rowObject != null && !ReferenceEquals(rowObject, owner))
                    {
                        table.Add(BuildDataRow(owner, rowObject, compiledColumns, null, !wroteOwnerColumns));
                        wroteOwnerColumns = true;
                    }
                }

                return;
            }

            table.Add(BuildDataRow(owner, owner, compiledColumns));
        }

        private List<string> BuildDataRow(ScriptableObject owner, object rowObject, List<ESTableCompiledColumn> compiledColumns, string rowKeyOverride = null, bool writeOwnerColumns = true)
        {
            var row = new List<string>(compiledColumns.Count + 1) { string.Empty };

            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                if (rowBinding != null && rowBinding.IsListElementRow && !writeOwnerColumns && !column.useRowObject)
                {
                    row.Add(string.Empty);
                    continue;
                }

                object value = rowKeyOverride != null && IsSerialChildRowKeyColumn(column.map)
                    ? rowKeyOverride
                    : GetColumnValue(owner, rowObject, column);
                row.Add(ConvertCellValue(value, column.map.valueWriteMode));
            }

            return row;
        }

        private object GetColumnValue(ScriptableObject owner, object rowObject, ESTableCompiledColumn column)
        {
            if (column.map.isInfoKey && owner is ISoDataInfo info)
                return info.GetKey();

            if (!column.canRead)
                return null;

            object target = column.useRowObject ? rowObject : owner;
            return ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
        }

        private static string ConvertCellValue(object value, ESTableValueWriteMode writeMode)
        {
            if (value == null)
                return string.Empty;

            if (value is UnityEngine.Object unityObject)
            {
                string path = AssetDatabase.GetAssetPath(unityObject);
                if (writeMode == ESTableValueWriteMode.UnityObjectPath)
                    return path;

                string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                return string.IsNullOrEmpty(guid) ? unityObject.name : guid;
            }

            if (value is bool boolValue)
                return boolValue ? "true" : "false";
            if (value is float floatValue)
                return floatValue.ToString(CultureInfo.InvariantCulture);
            if (value is double doubleValue)
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            if (value is decimal decimalValue)
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            if (ShouldUseJsonCell(value.GetType()))
                return JsonConvert.SerializeObject(value, CellJsonSettings);
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        private static bool ShouldUseJsonCell(Type type)
        {
            if (type == null)
                return false;
            if (type.IsPrimitive || type.IsEnum)
                return false;
            if (type == typeof(string) || type == typeof(decimal))
                return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            if (typeof(IDictionary).IsAssignableFrom(type))
                return true;
            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return true;

            return type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
        }

        #endregion
    }
}
#endif
