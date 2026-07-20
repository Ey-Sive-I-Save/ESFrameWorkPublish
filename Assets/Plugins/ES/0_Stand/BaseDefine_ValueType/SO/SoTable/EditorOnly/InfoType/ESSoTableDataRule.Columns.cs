#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ES.Internal;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Column Reflection Discovery
        private bool TryGetReflectionPathForColumn(string soFieldPath, Type rowOwnerType, Type listElementType, out Type ownerType, out string memberPath)
        {
            ownerType = rowOwnerType;
            memberPath = soFieldPath;

            if (rowBinding == null || !rowBinding.IsListElementRow || listElementType == null)
                return ownerType != null && !string.IsNullOrWhiteSpace(memberPath);

            string listPrefix = rowBinding.listFieldPath + "[].";
            if (soFieldPath.StartsWith(listPrefix, StringComparison.Ordinal))
            {
                ownerType = listElementType;
                memberPath = soFieldPath.Substring(listPrefix.Length);
                return !string.IsNullOrWhiteSpace(memberPath);
            }

            if (soFieldPath == rowBinding.listFieldPath || soFieldPath == rowBinding.listFieldPath + "[]")
                return false;

            return ownerType != null && !string.IsNullOrWhiteSpace(memberPath);
        }

        private void RebuildColumnsFromTypeFields(Type dataType, bool listElementField)
        {
            RebuildColumnsFromTypeFields(dataType, listElementField, string.Empty, string.Empty, 0);
        }

        private void RebuildColumnsFromTypeFields(Type dataType, bool listElementField, string fieldPrefix, string columnPrefix, int depth)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = dataType.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!ShouldExportField(field))
                    continue;

                if (field.DeclaringType == typeof(SoDataInfo) && field.Name == nameof(SoDataInfo.KeyName))
                    continue;
                if (listElementField && rowBinding != null && field.Name == rowBinding.elementKeyFieldPath)
                    continue;

                string fieldPath = CombineFieldPath(fieldPrefix, field.Name);
                string columnName = CombineColumnName(columnPrefix, field.Name);
                if (ShouldExpandNestedField(field.FieldType, depth))
                {
                    RebuildColumnsFromTypeFields(field.FieldType, listElementField, fieldPath, columnName, depth + 1);
                    continue;
                }

                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = listElementField ? BuildListElementPath(fieldPath) : fieldPath,
                    columnName = columnName,
                    displayName = fieldPath,
                    tableType = ESSoTableRuleTypeUtility.GuessTableType(field.FieldType),
                    valueWriteMode = ESSoTableRuleTypeUtility.GuessValueWriteMode(field.FieldType)
                });
            }
        }

        private bool ShouldExpandNestedField(Type fieldType, int depth)
        {
            if (nestedFieldRule == null || !nestedFieldRule.expandNestedFields)
                return false;
            if (depth >= nestedFieldRule.maxDepth)
                return false;

            return ESSoTableRuleTypeUtility.CanExpandAsNestedObject(fieldType);
        }

        private static string CombineFieldPath(string prefix, string fieldName)
        {
            return string.IsNullOrEmpty(prefix) ? fieldName : prefix + "." + fieldName;
        }

        private string CombineColumnName(string prefix, string fieldName)
        {
            if (string.IsNullOrEmpty(prefix))
                return fieldName;

            string separator = nestedFieldRule != null && !string.IsNullOrEmpty(nestedFieldRule.columnSeparator)
                ? nestedFieldRule.columnSeparator
                : "_";
            return prefix + separator + fieldName;
        }

        private string BuildListElementPath(string elementFieldPath)
        {
            if (rowBinding == null || string.IsNullOrEmpty(rowBinding.listFieldPath))
                return elementFieldPath;

            if (string.IsNullOrEmpty(elementFieldPath))
                return rowBinding.listFieldPath;

            return rowBinding.listFieldPath + "[]." + elementFieldPath;
        }

        private static bool ShouldExportField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized)
                return false;

            if (Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
                return false;

            if (HasOdinSerializeAttribute(field))
                return true;

            bool unitySerializedByVisibility = field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
            return unitySerializedByVisibility && IsUnitySerializableFieldType(field.FieldType);
        }

        private static bool HasOdinSerializeAttribute(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].GetType().FullName == "Sirenix.Serialization.OdinSerializeAttribute")
                    return true;
            }

            return false;
        }

        private static bool IsUnitySerializableFieldType(Type type)
        {
            if (type == null || type.IsPointer)
                return false;

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                return true;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;

            if (type.IsArray)
                return type.GetArrayRank() == 1 && IsUnitySerializableFieldType(type.GetElementType());

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return IsUnitySerializableFieldType(type.GetGenericArguments()[0]);

            if (type.IsClass || type.IsValueType)
                return Attribute.IsDefined(type, typeof(SerializableAttribute));

            return false;
        }
        #endregion
    }
}
#endif
