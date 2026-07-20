#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Row Cell Access Debug And Value Conversion
        private static string GetCell(List<string> row, int columnIndex)
        {
            return row != null && columnIndex >= 0 && columnIndex < row.Count ? row[columnIndex] : string.Empty;
        }

        private static int FindRawTableColumnIndex(List<List<string>> table, string columnName)
        {
            if (table == null || table.Count == 0 || string.IsNullOrWhiteSpace(columnName))
                return -1;

            List<string> varRow = table[0];
            if (varRow == null)
                return -1;

            for (int i = 1; i < varRow.Count; i++)
            {
                if (string.Equals(varRow[i], columnName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private StringBuilder BuildRowDebugHeader(ESTableRowDirectiveInfo directive, int rowIndex, ScriptableObject owner)
        {
            if (!directive.debug)
                return null;

            var builder = new StringBuilder();
            builder.AppendLine("SO 表格行 Debug");
            builder.AppendLine("表格行号：" + (rowIndex + 1));
            builder.AppendLine("原始行指令：" + (string.IsNullOrEmpty(directive.rawText) ? "(空)" : directive.rawText));
            builder.AppendLine("实际行指令：" + directive.directive);
            builder.AppendLine("批次：" + (GetActiveUseBatch() != null ? GetActiveUseBatch().batchName : "(默认批次)"));
            builder.AppendLine("导入表格：" + (string.IsNullOrEmpty(activeImportTablePath) ? "(未记录)" : activeImportTablePath));
            builder.AppendLine("目标 SO：" + (owner != null ? owner.name : "(空)"));
            builder.AppendLine("SO 类型：" + (owner != null ? owner.GetType().FullName : "(空)"));
            builder.AppendLine("SO 路径：" + (owner != null ? AssetDatabase.GetAssetPath(owner) : "(空)"));
            return builder;
        }

        private void AppendRowDebugTarget(StringBuilder debugLog, ScriptableObject owner, string rowKey)
        {
            if (debugLog == null)
                return;

            debugLog.AppendLine("目标对象：List 子级");
            debugLog.AppendLine("子级 Key：" + (string.IsNullOrWhiteSpace(rowKey) ? "(空)" : rowKey));
            debugLog.AppendLine("容器字段：" + (rowBinding != null ? rowBinding.listFieldPath : string.Empty));
            debugLog.AppendLine("宿主路径：" + (owner != null ? AssetDatabase.GetAssetPath(owner) : "(空)"));
        }

        private static void AppendFieldDebug(StringBuilder debugLog, ESTableCompiledColumn column, string message)
        {
            if (debugLog == null)
                return;

            string columnName = column != null && column.map != null ? column.map.columnName : "(未知列)";
            string fieldPath = column != null ? column.memberPath : "(未知字段)";
            string target = column != null && column.useRowObject ? "子级" : "SO";
            debugLog.AppendLine("字段[" + columnName + "] -> " + target + "." + fieldPath + "：" + message);
        }

        private static string FormatDebugValue(object value)
        {
            if (value == null)
                return "(null)";
            if (value is UnityEngine.Object unityObject)
                return unityObject == null ? "(null)" : unityObject.name + " <" + AssetDatabase.GetAssetPath(unityObject) + ">";
            return value.ToString();
        }

        private void ApplyTableRowToObject(List<string> row, List<ESTableCompiledColumn> compiledColumns, ScriptableObject owner, object rowObject, bool forceTableValues, bool skipEmptyCells, bool ownerColumnsOnly, StringBuilder debugLog = null, int rowIndex = -1, bool skipOwnerColumns = false)
        {
            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                if (!column.canWrite || column.tableColumnIndex < 0 || row == null || column.tableColumnIndex >= row.Count)
                {
                    AppendFieldDebug(debugLog, column, "跳过：列不可写或表格索引无效");
                    continue;
                }
                if (ownerColumnsOnly && column.useRowObject)
                {
                    AppendFieldDebug(debugLog, column, "跳过：owner 行只写 SO 本体字段");
                    continue;
                }
                if (skipOwnerColumns && !column.useRowObject)
                {
                    AppendFieldDebug(debugLog, column, "跳过：继承父级行不写 SO 本体字段");
                    continue;
                }
                if (skipEmptyCells && string.IsNullOrWhiteSpace(row[column.tableColumnIndex]))
                {
                    AppendFieldDebug(debugLog, column, "跳过：patch 行空单元格不写回");
                    continue;
                }

                object target = column.useRowObject ? rowObject : owner;
                if (target == null)
                {
                    AppendFieldDebug(debugLog, column, "Skip: target object is null");
                    continue;
                }

                try
                {
                if (!forceTableValues && column.map.authority == ESTableColumnAuthority.SoAuthority)
                {
                    object currentValue = ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
                    if (!IsEmptyAuthorityValue(currentValue))
                    {
                        AppendFieldDebug(debugLog, column, "跳过：导入策略为保留现有值，当前值非空，当前值=" + FormatDebugValue(currentValue));
                        continue;
                    }
                }

                object oldValue = ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
                object value = ConvertStringToValue(row[column.tableColumnIndex], column.valueType, column.map.valueWriteMode);
                ESRowBindingReflectionUtility.SetMemberValue(target, column.memberPath, value);
                AppendFieldDebug(debugLog, column, "写入：" + FormatDebugValue(oldValue) + " -> " + FormatDebugValue(value));
                }
                catch (Exception e)
                {
                    string suggestion = BuildImportCellFixSuggestion(column.valueType, column.map != null ? column.map.valueWriteMode : ESTableValueWriteMode.PlainValue);
                    Debug.LogWarning(BuildTableErrorMessage("导入写回字段", rowIndex, column.tableColumnIndex, column, owner, e.Message, suggestion), owner);
                    AppendFieldDebug(debugLog, column, "Write failed and skipped: " + e.Message);
                }
            }
        }

        private static bool IsEmptyAuthorityValue(object value)
        {
            if (value == null)
                return true;
            if (value is string text)
                return string.IsNullOrEmpty(text);
            if (value is UnityEngine.Object unityObject)
                return unityObject == null;
            if (value is ICollection collection)
                return collection.Count == 0;

            Type type = value.GetType();
            if (type.IsValueType)
                return value.Equals(Activator.CreateInstance(type));

            return false;
        }

        private static object ConvertStringToValue(string value, Type targetType, ESTableValueWriteMode writeMode)
        {
            string raw = value ?? string.Empty;
            string trimmed = raw.Trim();

            if (targetType == null)
                return raw;
            if (targetType == typeof(string))
                return raw;
            if (string.IsNullOrEmpty(trimmed))
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                    return null;

                object emptyInstance = ESRowBindingReflectionUtility.CreateObjectInstance(targetType);
                return emptyInstance ?? (targetType.IsValueType ? Activator.CreateInstance(targetType) : null);
            }
            if (targetType == typeof(bool))
                return trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) || trimmed == "1";
            if (targetType.IsEnum)
                return ConvertStringToEnum(trimmed, targetType);
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                string path = writeMode == ESTableValueWriteMode.UnityObjectPath ? trimmed : AssetDatabase.GUIDToAssetPath(trimmed);
                if (string.IsNullOrEmpty(path))
                    path = trimmed;
                return AssetDatabase.LoadAssetAtPath(path, targetType);
            }

            if (ShouldUseJsonCell(targetType))
                return JsonConvert.DeserializeObject(trimmed, targetType, CellJsonSettings);

            return Convert.ChangeType(trimmed, targetType, CultureInfo.InvariantCulture);
        }

        private static object ConvertStringToEnum(string value, Type enumType)
        {
            string normalized = NormalizeEnumCell(value);
            if (Enum.TryParse(enumType, normalized, true, out object enumValue))
                return enumValue;

            object displayNameValue = TryConvertEnumDisplayName(value, enumType);
            if (displayNameValue != null)
                return displayNameValue;

            object numericValue = TryConvertEnumNumber(value, enumType);
            if (numericValue != null)
                return numericValue;

            throw new FormatException("枚举导入失败：值 \"" + value + "\" 不能转换为 " + enumType.Name + "。请使用枚举名、数字值，或字段上的中文显示名。");
        }

        private static string NormalizeEnumCell(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace("，", ",")
                .Replace("|", ",")
                .Replace("+", ",")
                .Replace("/", ",");
        }

        private static object TryConvertEnumDisplayName(string value, Type enumType)
        {
            string normalized = NormalizeEnumCell(value);
            string[] parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            long combined = 0L;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                FieldInfo matchedField = FindEnumFieldByDisplayName(enumType, part);
                if (matchedField == null)
                    return null;

                object raw = matchedField.GetValue(null);
                combined |= Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            }

            return Enum.ToObject(enumType, combined);
        }

        private static FieldInfo FindEnumFieldByDisplayName(Type enumType, string displayName)
        {
            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (string.Equals(field.Name, displayName, StringComparison.OrdinalIgnoreCase))
                    return field;

                string attributeName = GetEnumDisplayName(field);
                if (!string.IsNullOrEmpty(attributeName) && string.Equals(attributeName, displayName, StringComparison.OrdinalIgnoreCase))
                    return field;
            }

            return null;
        }

        private static string GetEnumDisplayName(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                object attribute = attributes[i];
                string typeName = attribute.GetType().Name;
                if (typeName != "LabelTextAttribute" && typeName != "InspectorNameAttribute")
                    continue;

                string text = TryReadStringProperty(attribute, "Text");
                if (!string.IsNullOrEmpty(text))
                    return text;

                text = TryReadStringProperty(attribute, "Name");
                if (!string.IsNullOrEmpty(text))
                    return text;

                text = TryReadStringProperty(attribute, "displayName");
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }

        private static string TryReadStringProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(string))
                return null;

            return property.GetValue(target, null) as string;
        }

        private static object TryConvertEnumNumber(string value, Type enumType)
        {
            string trimmed = (value ?? string.Empty).Trim();
            try
            {
                Type underlyingType = Enum.GetUnderlyingType(enumType);
                object number = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ChangeType(Convert.ToInt64(trimmed.Substring(2), 16), underlyingType, CultureInfo.InvariantCulture)
                    : Convert.ChangeType(trimmed, underlyingType, CultureInfo.InvariantCulture);
                return Enum.ToObject(enumType, number);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
#endif
