#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Super Batch
        public void GenerateSuperBatchTemplate()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, "SoTableConfig", "Examples", "super_batch");
            Directory.CreateDirectory(folder);

            string baseName = string.IsNullOrWhiteSpace(ruleKey) ? "SoTableSuperBatch" : SanitizeAssetFileName(ruleKey) + "_SuperBatch";
            string csvPath = Path.Combine(folder, baseName + ".csv");
            string xlsxPath = Path.Combine(folder, baseName + ".xlsx");

            var table = new List<List<string>>
            {
                new List<string>
                {
                    "##var",
                    "enabled",
                    "batchName",
                    "direction",
                    "soAsset",
                    "soFolder",
                    "includeSubFolders",
                    "tableFile",
                    "fileKind",
                    "columnNameMode",
                    "exportWriteMode",
                    "serialChildWriteMode",
                    "serialChildImportSyncMode",
                    "applyRange",
                    "sliceColumn",
                    "start",
                    "end",
                    "targetGroup",
                    "targetInfo",
                    "activeFields",
                    "excludedFields"
                },
                new List<string>
                {
                    "##type",
                    "bool",
                    "string",
                    "enum",
                    "assetPath",
                    "folderPath",
                    "bool",
                    "tablePath",
                    "enum",
                    "enum",
                    "enum",
                    "enum",
                    "enum",
                    "enum",
                    "string",
                    "string",
                    "string",
                    "string",
                    "string",
                    "string",
                    "string"
                },
                new List<string>
                {
                    "##group",
                    "control",
                    "control",
                    "control",
                    "source",
                    "source",
                    "source",
                    "table",
                    "table",
                    "table",
                    "table",
                    "serialChild",
                    "serialChild",
                    "range",
                    "range",
                    "range",
                    "range",
                    "range",
                    "range",
                    "fields",
                    "fields"
                },
                new List<string>
                {
                    "##",
                    "是否执行本行",
                    "派生批次名",
                    "Export / Import / ImportAndExport",
                    "单个 SO 资产路径，和 soFolder 二选一",
                    "SO 文件夹路径，和 soAsset 二选一",
                    "是否包含子文件夹",
                    "目标表格路径，支持 csv/xlsx",
                    "Csv / Xlsx / CsvAndXlsx",
                    "English / Chinese",
                    "Rebuild / MergeByKey / UpdateExistingOnly",
                    "RebuildByOwner / MergeByKey / UpdateExistingOnly",
                    "KeepMissing / RebuildTouchedOwners / PruneMissingByTable / DeleteDirectiveOnly",
                    "All / Slice / SingleGroupInfo",
                    "片段截取列名",
                    "片段起点",
                    "片段终点",
                    "目标 Group",
                    "目标 Info",
                    "仅生效字段，逗号或分号分隔",
                    "排除字段，逗号或分号分隔"
                },
                new List<string>
                {
                    "true",
                    "示例批次",
                    "Export",
                    "",
                    "Assets/Plugins/ES/3_Examples/3_Data/Example_SOTableRule/02_Assets",
                    "true",
                    "SoTableConfig/Examples/super_batch/example.xlsx",
                    "Xlsx",
                    "English",
                    "MergeByKey",
                    "RebuildByOwner",
                    "KeepMissing",
                    "All",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                }
            };

            WriteCsv(csvPath, table);
            WriteXlsx(xlsxPath, table, "SuperBatch");

            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            if (batch != null)
            {
                batch.useSuperBatch = true;
                batch.superBatchTablePath = GetProjectRelativePath(xlsxPath);
                batch.superBatchSkipInvalidRows = true;
                EditorUtility.SetDirty(this);
            }

            AssetDatabase.Refresh();
            Debug.Log("超级批关系表模板已生成：" + csvPath + " / " + xlsxPath, this);
        }

        private void ExecuteSuperBatch(ESSoTableRuleUseBatch superBatch)
        {
            string path = ResolveProjectFullPath(superBatch != null ? superBatch.superBatchTablePath : null);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning("超级批关系表不存在：" + (superBatch != null ? superBatch.superBatchTablePath : string.Empty), this);
                return;
            }

            List<List<string>> table = ReadTableFileCached(path);
            if (!TryBuildSuperBatchHeader(table, out Dictionary<string, int> columns, out int dataStartRow, out string headerError))
            {
                Debug.LogWarning("超级批关系表表头无效：" + headerError, this);
                return;
            }

            int executed = 0;
            int skipped = 0;
            for (int rowIndex = dataStartRow; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                if (row == null || IsDataRowEmpty(row))
                {
                    skipped++;
                    continue;
                }

                ESSoTableRuleUseBatch derived = CloneUseBatch(superBatch);
                derived.useSuperBatch = false;
                if (!ApplySuperBatchRow(derived, row, columns, rowIndex + 1, out string reason))
                {
                    skipped++;
                    string message = "超级批第 " + (rowIndex + 1) + " 行跳过：" + reason;
                    if (superBatch != null && superBatch.superBatchSkipInvalidRows)
                    {
                        Debug.LogWarning(message, this);
                        continue;
                    }

                    Debug.LogError(message, this);
                    return;
                }

                ExecuteUseBatchDirect(derived);
                executed++;
            }

            Debug.Log("超级批执行完成：" + path + "，执行 " + executed + " 行，跳过 " + skipped + " 行。", this);
        }

        private string BuildSuperBatchPlanReport(ESSoTableRuleUseBatch superBatch, ESTablePlanRiskSummary summary)
        {
            var builder = new StringBuilder();
            string path = ResolveProjectFullPath(superBatch != null ? superBatch.superBatchTablePath : null);
            builder.AppendLine("========== 超级批计划 ==========");
            builder.AppendLine("关系表：" + FirstNotEmptyLocal(path, "(未找到)"));
            builder.AppendLine("跳过无效行：" + (superBatch != null && superBatch.superBatchSkipInvalidRows));

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                AddPlanError(summary, "超级批关系表不存在：" + (superBatch != null ? superBatch.superBatchTablePath : string.Empty));
                return builder.ToString();
            }

            List<List<string>> table = ReadTableFileCached(path);
            if (!TryBuildSuperBatchHeader(table, out Dictionary<string, int> columns, out int dataStartRow, out string headerError))
            {
                AddPlanError(summary, "超级批表头无效：" + headerError);
                return builder.ToString();
            }

            builder.AppendLine("数据行数：" + Math.Max(0, table.Count - dataStartRow));
            int executed = 0;
            int skipped = 0;
            for (int rowIndex = dataStartRow; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                if (row == null || IsDataRowEmpty(row))
                {
                    skipped++;
                    continue;
                }

                ESSoTableRuleUseBatch derived = CloneUseBatch(superBatch);
                derived.useSuperBatch = false;
                if (!ApplySuperBatchRow(derived, row, columns, rowIndex + 1, out string reason))
                {
                    skipped++;
                    string message = "超级批第 " + (rowIndex + 1) + " 行跳过：" + reason;
                    if (superBatch != null && superBatch.superBatchSkipInvalidRows)
                    {
                        builder.AppendLine(message);
                        continue;
                    }

                    AddPlanError(summary, message);
                    builder.AppendLine(message);
                    continue;
                }

                var rowSummary = new ESTablePlanRiskSummary();
                builder.AppendLine();
                builder.AppendLine("######## 超级批第 " + (rowIndex + 1) + " 行 ########");
                ESSoTableRuleUseBatch oldBatch = activeUseBatch;
                try
                {
                    activeUseBatch = derived;
                    builder.Append(BuildStandardBatchPlanReport(derived, rowSummary));
                }
                finally
                {
                    activeUseBatch = oldBatch;
                }
                MergePlanSummary(summary, rowSummary);
                executed++;
            }

            builder.AppendLine();
            builder.AppendLine("执行行数：" + executed);
            builder.AppendLine("跳过行数：" + skipped);
            return builder.ToString();
        }
        private static ESSoTableRuleUseBatch CloneUseBatch(ESSoTableRuleUseBatch source)
        {
            if (source == null)
                return new ESSoTableRuleUseBatch();

            return new ESSoTableRuleUseBatch
            {
                enabled = source.enabled,
                batchName = source.batchName,
                direction = source.direction,
                useSuperBatch = source.useSuperBatch,
                superBatchTablePath = source.superBatchTablePath,
                superBatchSkipInvalidRows = source.superBatchSkipInvalidRows,
                sourceBinding = CloneSourceBinding(source.sourceBinding),
                fileKind = source.fileKind,
                columnNameMode = source.columnNameMode,
                fileName = source.fileName,
                sheetName = source.sheetName,
                outputRoot = source.outputRoot,
                csvRelativePath = source.csvRelativePath,
                xlsxRelativePath = source.xlsxRelativePath,
                importConflictPolicy = source.importConflictPolicy,
                exportConflictPolicy = source.exportConflictPolicy,
                exportWriteMode = source.exportWriteMode,
                serialChildWriteMode = source.serialChildWriteMode,
                serialChildImportSyncMode = source.serialChildImportSyncMode,
                activeFields = source.activeFields,
                excludedFields = source.excludedFields,
                applyRangeMode = source.applyRangeMode,
                sliceColumnName = source.sliceColumnName,
                sliceStartValue = source.sliceStartValue,
                sliceEndValue = source.sliceEndValue,
                includeSliceStart = source.includeSliceStart,
                includeSliceEnd = source.includeSliceEnd,
                targetGroupKey = source.targetGroupKey,
                targetInfoKey = source.targetInfoKey
            };
        }

        private static ESSoTableRuleSourceBinding CloneSourceBinding(ESSoTableRuleSourceBinding source)
        {
            var result = new ESSoTableRuleSourceBinding();
            if (source == null)
                return result;

            result.sourceKind = source.sourceKind;
            result.soAsset = source.soAsset;
            result.soFolder = source.soFolder;
            result.includeSubFolders = source.includeSubFolders;
            result.monoScript = source.monoScript;
            result.folderAssets = source.folderAssets != null ? new List<ScriptableObject>(source.folderAssets) : new List<ScriptableObject>();
            result.folderSyncMode = source.folderSyncMode;
            result.createMissingAssetsInFolder = source.createMissingAssetsInFolder;
            result.updateExistingAssetsInFolder = source.updateExistingAssetsInFolder;
            result.sourceGuid = source.sourceGuid;
            result.sourcePath = source.sourcePath;
            result.sourceTypeName = source.sourceTypeName;
            return result;
        }

        private static bool TryBuildSuperBatchHeader(List<List<string>> table, out Dictionary<string, int> columns, out int dataStartRow, out string error)
        {
            columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            dataStartRow = 1;
            error = string.Empty;

            if (table == null || table.Count == 0)
            {
                error = "关系表为空。";
                return false;
            }

            List<string> headerRow = table[0] ?? new List<string>();
            int startColumn = 0;
            if (headerRow.Count > 0 && IsHeaderMark(headerRow[0]))
            {
                startColumn = 1;
                dataStartRow = 4;
            }

            for (int i = startColumn; i < headerRow.Count; i++)
            {
                string name = NormalizeSuperBatchColumnName(headerRow[i]);
                if (!string.IsNullOrEmpty(name) && !columns.ContainsKey(name))
                    columns[name] = i;
            }

            if (columns.Count == 0)
            {
                error = "第一行没有可用列名。";
                return false;
            }

            return true;
        }

        private bool ApplySuperBatchRow(ESSoTableRuleUseBatch target, List<string> row, Dictionary<string, int> columns, int rowNumber, out string reason)
        {
            reason = string.Empty;
            if (TryGetSuperBatchValue(row, columns, "enabled", out string enabled) && !ParseFlexibleBool(enabled, true))
            {
                reason = "enabled=false";
                return false;
            }

            ApplyStringOverride(row, columns, "batchName", value => target.batchName = value);
            ApplyStringOverride(row, columns, "fileName", value => target.fileName = value);
            ApplyStringOverride(row, columns, "sheetName", value => target.sheetName = value);
            ApplyStringOverride(row, columns, "outputRoot", value => target.outputRoot = value);
            ApplyStringOverride(row, columns, "csvRelativePath", value => target.csvRelativePath = value);
            ApplyStringOverride(row, columns, "xlsxRelativePath", value => target.xlsxRelativePath = value);
            ApplyStringOverride(row, columns, "activeFields", value => target.activeFields = value);
            ApplyStringOverride(row, columns, "excludedFields", value => target.excludedFields = value);

            if (TryGetSuperBatchValue(row, columns, "direction", out string direction) && Enum.TryParse(direction, true, out ESSoTableRuleUseDirection parsedDirection))
                target.direction = parsedDirection;
            if (TryGetSuperBatchValue(row, columns, "fileKind", out string fileKind) && Enum.TryParse(fileKind, true, out ESTableFileKind parsedKind))
                target.fileKind = parsedKind;
            if (TryGetSuperBatchValue(row, columns, "columnNameMode", out string columnNameMode) && Enum.TryParse(columnNameMode, true, out ESTableColumnNameMode parsedNameMode))
                target.columnNameMode = parsedNameMode;
            if (TryGetSuperBatchValue(row, columns, "exportWriteMode", out string exportWriteMode) && Enum.TryParse(exportWriteMode, true, out ESTableExportWriteMode parsedExportMode))
                target.exportWriteMode = parsedExportMode;
            if (TryGetSuperBatchValue(row, columns, "serialChildWriteMode", out string childWriteMode) && Enum.TryParse(childWriteMode, true, out ESTableSerialChildWriteMode parsedChildWriteMode))
                target.serialChildWriteMode = parsedChildWriteMode;
            if (TryGetSuperBatchValue(row, columns, "serialChildImportSyncMode", out string childImportMode) && Enum.TryParse(childImportMode, true, out ESTableSerialChildImportSyncMode parsedChildImportMode))
                target.serialChildImportSyncMode = parsedChildImportMode;

            if (TryGetSuperBatchValue(row, columns, "tableFile", out string tableFile))
                ApplyTableFileToBatch(target, tableFile);

            if (!ApplySuperBatchSource(target.sourceBinding, row, columns, out reason))
                return false;

            ApplySuperBatchRange(target, row, columns);

            if (string.IsNullOrWhiteSpace(target.fileName) && string.IsNullOrWhiteSpace(target.superBatchTablePath))
                target.fileName = FirstConfiguredName();
            if (string.IsNullOrWhiteSpace(target.sheetName))
                target.sheetName = target.fileName;

            return true;
        }

        private static void ApplySuperBatchRange(ESSoTableRuleUseBatch target, List<string> row, Dictionary<string, int> columns)
        {
            bool hasSlice = TryGetSuperBatchValue(row, columns, "sliceColumn", out string sliceColumn)
                | TryGetSuperBatchValue(row, columns, "start", out string start)
                | TryGetSuperBatchValue(row, columns, "end", out string end);
            bool hasGroupInfo = TryGetSuperBatchValue(row, columns, "targetGroup", out string targetGroup)
                | TryGetSuperBatchValue(row, columns, "targetInfo", out string targetInfo);

            if (TryGetSuperBatchValue(row, columns, "applyRange", out string applyRange) && Enum.TryParse(applyRange, true, out ESTableBatchApplyRangeMode parsedRange))
                target.applyRangeMode = parsedRange;
            else if (hasSlice)
                target.applyRangeMode = ESTableBatchApplyRangeMode.Slice;
            else if (hasGroupInfo)
                target.applyRangeMode = ESTableBatchApplyRangeMode.SingleGroupInfo;

            if (!string.IsNullOrWhiteSpace(sliceColumn))
                target.sliceColumnName = sliceColumn;
            if (!string.IsNullOrWhiteSpace(start))
                target.sliceStartValue = start;
            if (!string.IsNullOrWhiteSpace(end))
                target.sliceEndValue = end;
            if (!string.IsNullOrWhiteSpace(targetGroup))
                target.targetGroupKey = targetGroup;
            if (!string.IsNullOrWhiteSpace(targetInfo))
                target.targetInfoKey = targetInfo;
            if (TryGetSuperBatchValue(row, columns, "includeStart", out string includeStart))
                target.includeSliceStart = ParseFlexibleBool(includeStart, true);
            if (TryGetSuperBatchValue(row, columns, "includeEnd", out string includeEnd))
                target.includeSliceEnd = ParseFlexibleBool(includeEnd, true);
        }

        private bool ApplySuperBatchSource(ESSoTableRuleSourceBinding source, List<string> row, Dictionary<string, int> columns, out string reason)
        {
            reason = string.Empty;
            if (source == null)
                return true;

            if (TryGetSuperBatchValue(row, columns, "includeSubFolders", out string includeSubFolders))
                source.includeSubFolders = ParseFlexibleBool(includeSubFolders, true);

            if (TryGetSuperBatchValue(row, columns, "soAsset", out string soAssetPath))
            {
                ScriptableObject asset = LoadProjectAsset<ScriptableObject>(soAssetPath);
                if (asset == null)
                {
                    reason = "SO 文件不存在：" + soAssetPath;
                    return false;
                }

                source.soAsset = asset;
                source.soFolder = null;
                source.folderAssets.Clear();
                source.Capture(ESSoTableRuleBindSourceKind.SoAsset, asset, asset.GetType());
            }

            if (TryGetSuperBatchValue(row, columns, "soFolder", out string soFolderPath))
            {
                DefaultAsset folder = LoadProjectAsset<DefaultAsset>(soFolderPath);
                string assetPath = folder != null ? AssetDatabase.GetAssetPath(folder) : ResolveAssetRelativePath(soFolderPath);
                if (folder == null || string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
                {
                    reason = "SO 文件夹不存在：" + soFolderPath;
                    return false;
                }

                source.soAsset = null;
                source.soFolder = folder;
                source.folderAssets = CollectScriptableObjectsFromFolder(folder, source.includeSubFolders);
                source.Capture(ESSoTableRuleBindSourceKind.SoFolder, folder, null);
            }

            return true;
        }

        private void ApplyTableFileToBatch(ESSoTableRuleUseBatch batch, string tableFile)
        {
            string fullPath = ResolveProjectFullPath(tableFile);
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            string folder = Path.GetDirectoryName(fullPath);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relativeFolder = folder != null && folder.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? folder.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : folder;

            batch.fileName = fileName;
            if (extension == ".csv")
            {
                batch.fileKind = ESTableFileKind.Csv;
                batch.csvRelativePath = relativeFolder;
            }
            else if (extension == ".xlsx")
            {
                batch.fileKind = ESTableFileKind.Xlsx;
                batch.xlsxRelativePath = relativeFolder;
            }
        }

        private static bool TryGetSuperBatchValue(List<string> row, Dictionary<string, int> columns, string name, out string value)
        {
            value = string.Empty;
            if (columns == null || !columns.TryGetValue(NormalizeSuperBatchColumnName(name), out int columnIndex))
                return false;

            value = GetCell(row, columnIndex).Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static void ApplyStringOverride(List<string> row, Dictionary<string, int> columns, string name, Action<string> apply)
        {
            if (TryGetSuperBatchValue(row, columns, name, out string value))
                apply(value);
        }

        private static string NormalizeSuperBatchColumnName(string value)
        {
            string name = (value ?? string.Empty).Trim();
            int paren = name.IndexOf('(');
            if (paren >= 0)
                name = name.Substring(0, paren).Trim();
            return name.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        }

        private static bool ParseFlexibleBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            string text = value.Trim();
            return text.Equals("true", StringComparison.OrdinalIgnoreCase)
                || text.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || text.Equals("y", StringComparison.OrdinalIgnoreCase)
                || text.Equals("1", StringComparison.OrdinalIgnoreCase)
                || text.Equals("是", StringComparison.OrdinalIgnoreCase)
                || text.Equals("启用", StringComparison.OrdinalIgnoreCase);
        }

        private static T LoadProjectAsset<T>(string path) where T : UnityEngine.Object
        {
            string assetPath = ResolveAssetRelativePath(path);
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        private static string ResolveAssetRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            string normalized = path.Replace('\\', '/');
            int assetsIndex = normalized.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
                return normalized.Substring(assetsIndex);
            return normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? normalized : string.Empty;
        }

        private static string ResolveProjectFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }
        #endregion
    }
}
#endif
