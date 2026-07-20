#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        private string BuildTableErrorMessage(string stage, int rowIndex, int columnIndex, ESTableCompiledColumn column, UnityEngine.Object targetObject, string reason, string suggestion)
        {
            var builder = new StringBuilder(256);
            builder.AppendLine("【ES表格错误】");
            builder.AppendLine("阶段：" + FirstNonEmpty(stage, "未知"));
            builder.AppendLine("批次：" + GetCurrentBatchDisplayName());
            builder.AppendLine("表格：" + FirstNonEmpty(GetCurrentTableDisplayPath(), "未找到当前表格路径"));
            if (rowIndex >= 0)
                builder.AppendLine("行：" + (rowIndex + 1));
            if (columnIndex >= 0)
                builder.AppendLine("列：" + (columnIndex + 1));
            if (column != null && column.map != null)
            {
                builder.AppendLine("列名：" + FirstNonEmpty(GetActiveTableColumnName(column.map), column.map.columnName, column.map.displayName, "(未知列)"));
                builder.AppendLine("字段：" + FirstNonEmpty(column.map.soFieldPath, "(未绑定字段)"));
                builder.AppendLine("目标类型：" + GetTypeName(column.valueType));
            }
            if (targetObject != null)
                builder.AppendLine("目标：" + targetObject.name + " <" + AssetDatabase.GetAssetPath(targetObject) + ">");
            builder.AppendLine("原因：" + FirstNonEmpty(reason, "未知错误"));
            builder.AppendLine("建议：" + FirstNonEmpty(suggestion, "查看本批次计划报告，确认表头、Key、字段映射和单元格格式。"));
            return builder.ToString();
        }

        private string BuildTableRowErrorMessage(string stage, int rowIndex, UnityEngine.Object targetObject, string reason, string suggestion)
        {
            return BuildTableErrorMessage(stage, rowIndex, -1, null, targetObject, reason, suggestion);
        }

        private string GetCurrentBatchDisplayName()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return FirstNonEmpty(batch != null ? batch.batchName : null, ruleKey, tableName, name, "(未命名批次)");
        }

        private string GetCurrentTableDisplayPath()
        {
            string path = ResolveExistingInputPath();
            if (!string.IsNullOrWhiteSpace(path))
                return path;

            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            if (batch == null)
                return string.Empty;

            return FirstNonEmpty(batch.superBatchTablePath, batch.fileName);
        }

        private static string FirstNonEmpty(params string[] values)
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
    }
}
#endif
