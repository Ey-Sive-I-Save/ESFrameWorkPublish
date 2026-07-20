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
            GenerateSuperBatchTemplate(GetActiveUseBatch());
        }

        public void GenerateSuperBatchTemplate(ESSoTableRuleUseBatch targetBatch)
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
                    "是否执行本行，true/false。false 会跳过本行。",
                    "派生批次名称，只用于计划报告和日志识别。",
                    "执行方向：Export=SO 写表，Import=表写 SO，ImportAndExport=先导入再导出。",
                    "单个 SO 资产路径。和 soFolder 二选一；两者都空时沿用超级批批次面板里的来源。",
                    "SO 文件夹路径。和 soAsset 二选一；适合一行处理一个文件夹。",
                    "是否包含子文件夹，true/false。",
                    "目标表格路径，支持 .csv 或 .xlsx。会自动拆成文件名、输出目录和格式。",
                    "文件格式：Csv / Xlsx / CsvAndXlsx。tableFile 带扩展名时会自动修正。",
                    "表头模式：English=字段列名，Chinese=中文显示名。",
                    "导出写表模式：Rebuild=重建，MergeByKey=按 Key 合并，UpdateExistingOnly=只更新已有行。",
                    "子级串行导出模式：RebuildByOwner=按父级重建子行，MergeByKey=按子 Key 合并，UpdateExistingOnly=只更新已有子行。",
                    "子级串行导入同步：KeepMissing=保留缺失，RebuildTouchedOwners=重建触达父级，PruneMissingByTable=按表裁剪，DeleteDirectiveOnly=只响应 delete 行。",
                    "生效范围：All=全部，Slice=按片段截取，SingleGroupInfo=指定 Group/Info。",
                    "片段截取列名，留空时通常使用规则里的唯一 Key。",
                    "片段起点值。Slice 模式下使用。",
                    "片段终点值。Slice 模式下使用。",
                    "目标 Group Key。只处理指定 Group，留空不限制。",
                    "目标 Info Key。只处理指定 Info，留空不限制。",
                    "仅生效字段，逗号或分号分隔。用于分部分排表；留空表示沿用批次设置。",
                    "排除字段，逗号或分号分隔。优先级高于 activeFields。"
                },
                new List<string>
                {
                    "##assert",
                    "",
                    "",
                    "",
                    "asset",
                    "asset",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                },
                new List<string>
                {
                    "##rowDirective | 空=正常执行；skip/ignore/disabled=跳过；comment:=备注；debug=打印本行解析摘要",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                },
                new List<string>
                {
                    "",
                    "true",
                    "整表导出示例",
                    "Export",
                    "",
                    "Assets/Plugins/ES/3_Examples/3_Data/Example_SOTableRule/02_Assets",
                    "true",
                    "SoTableConfig/Examples/super_batch/full_export.xlsx",
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
                },
                new List<string>
                {
                    "",
                    "false",
                    "片段截取示例",
                    "ImportAndExport",
                    "",
                    "Assets/Plugins/ES/3_Examples/3_Data/Example_SOTableRule/02_Assets",
                    "true",
                    "SoTableConfig/Examples/super_batch/slice_001.xlsx",
                    "Xlsx",
                    "English",
                    "MergeByKey",
                    "MergeByKey",
                    "KeepMissing",
                    "Slice",
                    "rowKey",
                    "reward_001",
                    "reward_099",
                    "",
                    "",
                    "",
                    ""
                },
                new List<string>
                {
                    "comment: 这行是备注，不会执行。把 enabled 改成 true 后，才会执行对应关系行。",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
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
            WriteXlsx(xlsxPath, table, "SuperBatch", BuildSuperBatchTemplateDropdowns());

            if (targetBatch != null)
            {
                targetBatch.useSuperBatch = true;
                targetBatch.superBatchTablePath = GetProjectRelativePath(xlsxPath);
                targetBatch.superBatchSkipInvalidRows = true;
                if (targetBatch.superBatchLocateRowNumber <= 0)
                    targetBatch.superBatchLocateRowNumber = 7;
                EditorUtility.SetDirty(this);
            }

            AssetDatabase.Refresh();
            Debug.Log("超级批关系表模板已生成：" + csvPath + " / " + xlsxPath, this);
        }

        public string BuildSuperBatchRelationRowReport(int rowNumber)
        {
            ESSoTableRuleUseBatch superBatch = GetActiveUseBatch();
            return BuildSuperBatchRelationRowReport(superBatch != null ? superBatch.superBatchTablePath : null, rowNumber);
        }

        public string BuildSuperBatchRelationRowReport(string tablePath, int rowNumber)
        {
            string path = ResolveProjectFullPath(tablePath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return "超级批关系表不存在：" + (tablePath ?? string.Empty);

            List<List<string>> table = ReadTableFileCached(path);
            if (!TryBuildSuperBatchHeader(table, out Dictionary<string, int> columns, out int dataStartRow, out string headerError))
                return "超级批关系表表头无效：" + headerError;

            int rowIndex = Math.Max(1, rowNumber) - 1;
            if (rowIndex < dataStartRow || rowIndex >= table.Count)
                return "关系表第 " + rowNumber + " 行不是可执行数据行。数据起始行：" + (dataStartRow + 1) + "，总行数：" + table.Count + "。";

            List<string> row = table[rowIndex];
            var builder = new StringBuilder();
            builder.AppendLine("超级批关系表行定位");
            builder.AppendLine("表格：" + path);
            builder.AppendLine("行号：" + rowNumber);
            builder.AppendLine("行指令：" + FirstNotEmptyLocal(GetCell(row, 0), "(空，正常执行)"));

            AppendSuperBatchReportValue(builder, row, columns, "batchName", "批次名");
            AppendSuperBatchReportValue(builder, row, columns, "direction", "方向");
            AppendSuperBatchReportValue(builder, row, columns, "soAsset", "SO 文件");
            AppendSuperBatchReportValue(builder, row, columns, "soFolder", "SO 文件夹");
            AppendSuperBatchReportValue(builder, row, columns, "tableFile", "表格");
            AppendSuperBatchReportValue(builder, row, columns, "applyRange", "范围");
            AppendSuperBatchReportValue(builder, row, columns, "sliceColumn", "截取列");
            AppendSuperBatchReportValue(builder, row, columns, "start", "起点");
            AppendSuperBatchReportValue(builder, row, columns, "end", "终点");
            AppendSuperBatchReportValue(builder, row, columns, "targetGroup", "Group");
            AppendSuperBatchReportValue(builder, row, columns, "targetInfo", "Info");
            AppendSuperBatchReportValue(builder, row, columns, "activeFields", "生效字段");
            AppendSuperBatchReportValue(builder, row, columns, "excludedFields", "排除字段");
            return builder.ToString();
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
                superBatchLocateRowNumber = source.superBatchLocateRowNumber,
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
            dataStartRow = GetDataStartRowIndex(table);
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
            string rowDirective = GetCell(row, 0).Trim();
            if (IsSkipSuperBatchDirective(rowDirective))
            {
                reason = string.IsNullOrWhiteSpace(rowDirective) ? "行指令跳过" : "行指令：" + rowDirective;
                return false;
            }

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

        private static Dictionary<int, string> BuildSuperBatchTemplateDropdowns()
        {
            return new Dictionary<int, string>
            {
                { 1, "true,false" },
                { 3, "Export,Import,ImportAndExport" },
                { 6, "true,false" },
                { 8, "Csv,Xlsx,CsvAndXlsx" },
                { 9, "English,Chinese" },
                { 10, "Rebuild,MergeByKey,UpdateExistingOnly" },
                { 11, "RebuildByOwner,MergeByKey,UpdateExistingOnly" },
                { 12, "KeepMissing,RebuildTouchedOwners,PruneMissingByTable,DeleteDirectiveOnly" },
                { 13, "All,Slice,SingleGroupInfo" }
            };
        }

        private static bool IsSkipSuperBatchDirective(string directive)
        {
            if (string.IsNullOrWhiteSpace(directive))
                return false;

            string text = directive.Trim();
            return text.Equals("skip", StringComparison.OrdinalIgnoreCase)
                || text.Equals("ignore", StringComparison.OrdinalIgnoreCase)
                || text.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("comment", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendSuperBatchReportValue(StringBuilder builder, List<string> row, Dictionary<string, int> columns, string name, string label)
        {
            if (TryGetSuperBatchValue(row, columns, name, out string value))
                builder.AppendLine(label + "：" + value);
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
