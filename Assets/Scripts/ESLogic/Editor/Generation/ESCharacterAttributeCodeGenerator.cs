using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("ES_Logic.Editor.Generation.Tests")]

namespace ES
{
    /// <summary>
    /// Projects the fixed Character HotSlot structure from the GameCore schema into the small
    /// typed API used by KCC and other hot callers. The projection intentionally excludes
    /// authored values such as base values, ranges and display names, so those edits do not
    /// require a C# generation or compilation pass.
    /// </summary>
    internal static class ESCharacterAttributeCodeGenerator
    {
        internal const string GeneratedAssetPath =
            "Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Attributes/ESCharacterAttributeCatalog.generated.cs";

        // Keep byte value 255 out of the public fixed-slot ABI. It leaves room for an explicit
        // terminal value and prevents a Count member from becoming an accidental valid ID.
        private const int MaxFixedSlotCount = byte.MaxValue - 1;
        private const string StructuralSignaturePrefix = "// Fixed API structural signature: ";
        // Bump this when generated fixed-slot behavior changes without a schema change.
        private const string FixedApiProjectionVersion = "v1";

        public static bool TryGenerate(ESSuperAttributeTable source, out bool changed, out string error)
        {
            changed = false;
            if (!TryBuildSource(source, out string generatedSource, out error))
                return false;

            return ESGeneratedSourceFile.TryEnsureCurrent(
                GeneratedAssetPath,
                generatedSource,
                true,
                out changed,
                out error);
        }

        public static bool IsCurrent(ESSuperAttributeTable source, out string error)
        {
            return TryBuildSource(source, out string generatedSource, out error)
                   && ESGeneratedSourceFile.IsCurrent(GeneratedAssetPath, generatedSource, out error);
        }

        public static bool TryBuildSource(ESSuperAttributeTable source, out string generatedSource, out string error)
        {
            generatedSource = null;
            if (source == null)
            {
                error = "缺少角色属性表。";
                return false;
            }

            if (!string.Equals(source.catalogScope, ESAttributeBakeTable.CharacterScope, StringComparison.Ordinal))
            {
                error = "固定角色属性代码只能从 " + ESAttributeBakeTable.CharacterScope + " 生成。";
                return false;
            }

            if (!source.enabled)
            {
                error = "角色属性表未启用，不能生成固定属性代码。";
                return false;
            }

            if (!source.ValidateDefinitions(out error))
            {
                error = "角色属性表无效：" + error;
                return false;
            }

            if (!TryCollect(source, out List<FloatEntry> floats, out List<PermitEntry> permits, out error))
                return false;

            floats.Sort(Compare);
            permits.Sort(Compare);
            if (!ValidateGeneratedNames(floats, permits, out error))
                return false;

            generatedSource = BuildSource(floats, permits, BuildStructuralSignature(floats, permits));
            error = null;
            return true;
        }

        private static bool TryCollect(
            ESSuperAttributeTable source,
            out List<FloatEntry> floats,
            out List<PermitEntry> permits,
            out string error)
        {
            floats = new List<FloatEntry>();
            permits = new List<PermitEntry>();

            if (source.floatAttributes != null)
            {
                for (int i = 0; i < source.floatAttributes.Count; i++)
                {
                    ESSuperFloatAttributeDefinition definition = source.floatAttributes[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.fixedApiName))
                        continue;

                    string apiName = definition.fixedApiName.Trim();
                    if (!string.Equals(definition.fixedApiName, apiName, StringComparison.Ordinal))
                    {
                        error = "Float definition[" + i + "] 的固定代码访问名不能包含前后空白。";
                        return false;
                    }
                    if (!TryValidateFixedDefinition(apiName, definition.storagePolicy, "Float", i, out error))
                        return false;

                    floats.Add(new FloatEntry(definition, apiName));
                    if (floats.Count > MaxFixedSlotCount)
                    {
                        error = "固定角色 Float 属性最多 " + MaxFixedSlotCount + " 个。";
                        return false;
                    }
                }
            }

            if (source.permitAttributes != null)
            {
                for (int i = 0; i < source.permitAttributes.Count; i++)
                {
                    ESSuperPermitAttributeDefinition definition = source.permitAttributes[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.fixedApiName))
                        continue;

                    string apiName = definition.fixedApiName.Trim();
                    if (!string.Equals(definition.fixedApiName, apiName, StringComparison.Ordinal))
                    {
                        error = "Permit definition[" + i + "] 的固定代码访问名不能包含前后空白。";
                        return false;
                    }
                    if (!TryValidateFixedDefinition(apiName, definition.storagePolicy, "Permit", i, out error))
                        return false;

                    permits.Add(new PermitEntry(definition, apiName));
                    if (permits.Count > MaxFixedSlotCount)
                    {
                        error = "固定角色 Permit 属性最多 " + MaxFixedSlotCount + " 个。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TryValidateFixedDefinition(
            string apiName,
            ESKeyStoragePolicy storagePolicy,
            string kind,
            int index,
            out string error)
        {
            if (storagePolicy != ESKeyStoragePolicy.HotSlot)
            {
                error = kind + " definition[" + index + "] 的固定代码访问名只能使用 HotSlot。"
                        + "普通 HotSlot/Sparse 属性请留空 fixedApiName。";
                return false;
            }

            if (!IsIdentifier(apiName) || IsReservedIdentifier(apiName))
            {
                error = kind + " definition[" + index + "] 的固定代码访问名不是可生成的 C# 标识符：" + apiName;
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateGeneratedNames(
            List<FloatEntry> floats,
            List<PermitEntry> permits,
            out string error)
        {
            var floatNames = new HashSet<string>(StringComparer.Ordinal);
            var permitNames = new HashSet<string>(StringComparer.Ordinal);
            var enumNames = new HashSet<string>(StringComparer.Ordinal);
            var enumValues = new HashSet<ushort>();
            var keyNames = new HashSet<string>(StringComparer.Ordinal);
            var keyValues = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < floats.Count; i++)
            {
                FloatEntry entry = floats[i];
                if (!floatNames.Add(entry.ApiName))
                {
                    error = "重复的固定角色 Float 代码访问名：" + entry.ApiName;
                    return false;
                }

                if (!ValidateSharedNames(entry.ApiName, entry.Definition.enumKey, entry.Definition.StringKey,
                        enumNames, enumValues, keyNames, keyValues, out error))
                    return false;
            }

            for (int i = 0; i < permits.Count; i++)
            {
                PermitEntry entry = permits[i];
                if (!permitNames.Add(entry.ApiName))
                {
                    error = "重复的固定角色 Permit 代码访问名：" + entry.ApiName;
                    return false;
                }

                if (!ValidateSharedNames(entry.ApiName, entry.Definition.enumKey, entry.Definition.StringKey,
                        enumNames, enumValues, keyNames, keyValues, out error))
                    return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateSharedNames(
            string apiName,
            ushort enumKey,
            string stringKey,
            HashSet<string> enumNames,
            HashSet<ushort> enumValues,
            HashSet<string> keyNames,
            HashSet<string> keyValues,
            out string error)
        {
            if (enumKey != 0)
            {
                if (!enumNames.Add(apiName))
                {
                    error = "固定角色属性的 EnumKey 代码访问名重复：" + apiName;
                    return false;
                }

                if (!enumValues.Add(enumKey))
                {
                    error = "固定角色属性的 EnumKey 重复：" + enumKey;
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(stringKey))
            {
                if (!keyNames.Add(apiName))
                {
                    error = "固定角色属性的 StringKey 代码访问名重复：" + apiName;
                    return false;
                }

                if (!keyValues.Add(stringKey))
                {
                    error = "固定角色属性的 StringKey 重复：" + stringKey;
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static int Compare(FloatEntry left, FloatEntry right)
        {
            return Compare(left.Definition.enumKey, left.Definition.StringKey, left.ApiName,
                right.Definition.enumKey, right.Definition.StringKey, right.ApiName);
        }

        private static int Compare(PermitEntry left, PermitEntry right)
        {
            return Compare(left.Definition.enumKey, left.Definition.StringKey, left.ApiName,
                right.Definition.enumKey, right.Definition.StringKey, right.ApiName);
        }

        private static int Compare(ushort leftEnum, string leftKey, string leftName, ushort rightEnum, string rightKey, string rightName)
        {
            int enumComparison = leftEnum.CompareTo(rightEnum);
            if (leftEnum == 0)
                enumComparison = rightEnum == 0 ? 0 : 1;
            else if (rightEnum == 0)
                enumComparison = -1;
            if (enumComparison != 0)
                return enumComparison;

            int keyComparison = string.Compare(leftKey, rightKey, StringComparison.Ordinal);
            return keyComparison != 0 ? keyComparison : string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private static string BuildSource(List<FloatEntry> floats, List<PermitEntry> permits, string structuralSignature)
        {
            var builder = new StringBuilder(8192);
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("// Generated projection of GameCore character fixed attributes. Do not edit by hand.");
            builder.AppendLine("// Regenerate only after changing the fixed API structure: fixedApiName, stable identity, kind, or slot order.");
            builder.Append(StructuralSignaturePrefix).AppendLine(structuralSignature);
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine();
            builder.AppendLine("namespace ES");
            builder.AppendLine("{");
            AppendIdEnum(builder, "ESCharacterFloatAttributeId", floats);
            builder.AppendLine();
            AppendIdEnum(builder, "ESCharacterPermitAttributeId", permits);
            builder.AppendLine();
            AppendStableEnum(builder, floats, permits);
            builder.AppendLine();
            builder.AppendLine("    public enum ESCharacterAttributeValueKind : byte");
            builder.AppendLine("    {");
            builder.AppendLine("        Float = 0,");
            builder.AppendLine("        Permit = 1");
            builder.AppendLine("    }");
            builder.AppendLine();
            AppendKeyConstants(builder, floats, permits);
            builder.AppendLine();
            builder.AppendLine("    public static partial class ESCharacterAttributeCatalog");
            builder.AppendLine("    {");
            builder.AppendLine("        // IDs are compact compiled slots only. EnumKey/StringKey remain the serializable identity.");
            AppendKeyArray(builder, "Float", floats);
            builder.AppendLine();
            AppendKeyArray(builder, "Permit", permits);
            builder.AppendLine();
            AppendEnumKeyArray(builder, "Float", floats);
            builder.AppendLine();
            AppendEnumKeyArray(builder, "Permit", permits);
            builder.AppendLine();
            builder.AppendLine("        public static int FloatCount => (int)ESCharacterFloatAttributeId.Count;");
            builder.AppendLine("        public static int PermitCount => (int)ESCharacterPermitAttributeId.Count;");
            builder.AppendLine();
            AppendDefaultTable(builder, floats, permits);
            builder.AppendLine();
            AppendLookupMethods(builder, floats, permits);
            builder.AppendLine();
            AppendDefaultHelpers(builder);
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildStructuralSignature(List<FloatEntry> floats, List<PermitEntry> permits)
        {
            var builder = new StringBuilder(256);
            builder.Append(FixedApiProjectionVersion).Append("|Float|");
            AppendStructuralSignature(builder, floats);
            builder.Append("|Permit|");
            AppendStructuralSignature(builder, permits);
            return builder.ToString();
        }

        private static void AppendStructuralSignature(StringBuilder builder, List<FloatEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                FloatEntry entry = entries[i];
                AppendSignatureField(builder, entry.ApiName);
                builder.Append(entry.Definition.enumKey).Append('|');
                AppendSignatureField(builder, entry.Definition.StringKey);
            }
        }

        private static void AppendStructuralSignature(StringBuilder builder, List<PermitEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                PermitEntry entry = entries[i];
                AppendSignatureField(builder, entry.ApiName);
                builder.Append(entry.Definition.enumKey).Append('|');
                AppendSignatureField(builder, entry.Definition.StringKey);
            }
        }

        private static void AppendSignatureField(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append(';');
        }

        private static void AppendIdEnum<TEntry>(StringBuilder builder, string typeName, List<TEntry> entries)
            where TEntry : IFixedEntry
        {
            builder.Append("    public enum ").Append(typeName).AppendLine(" : byte");
            builder.AppendLine("    {");
            for (int i = 0; i < entries.Count; i++)
                builder.Append("        ").Append(entries[i].ApiName).Append(" = ").Append(i).AppendLine(",");
            builder.Append("        Count = ").Append(entries.Count).AppendLine();
            builder.AppendLine("    }");
        }

        private static void AppendStableEnum(StringBuilder builder, List<FloatEntry> floats, List<PermitEntry> permits)
        {
            builder.AppendLine("    public enum ESCharacterAttributeEnumKey : ushort");
            builder.AppendLine("    {");
            builder.AppendLine("        None = 0,");
            AppendStableEnumMembers(builder, floats);
            AppendStableEnumMembers(builder, permits);
            builder.AppendLine("    }");
        }

        private static void AppendStableEnumMembers(StringBuilder builder, List<FloatEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Definition.enumKey == 0)
                    continue;
                builder.Append("        ").Append(entries[i].ApiName).Append(" = ")
                    .Append(entries[i].Definition.enumKey).AppendLine(",");
            }
        }

        private static void AppendStableEnumMembers(StringBuilder builder, List<PermitEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Definition.enumKey == 0)
                    continue;
                builder.Append("        ").Append(entries[i].ApiName).Append(" = ")
                    .Append(entries[i].Definition.enumKey).AppendLine(",");
            }
        }

        private static void AppendKeyConstants(StringBuilder builder, List<FloatEntry> floats, List<PermitEntry> permits)
        {
            builder.AppendLine("    public static class ESCharacterSuperAttributeKeys");
            builder.AppendLine("    {");
            bool wrote = false;
            AppendKeyConstants(builder, floats, ref wrote);
            AppendKeyConstants(builder, permits, ref wrote);
            if (!wrote)
                builder.AppendLine("        // No fixed StringKey attributes are configured.");
            builder.AppendLine("    }");
        }

        private static void AppendKeyConstants(StringBuilder builder, List<FloatEntry> entries, ref bool wrote)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                string key = entries[i].Definition.StringKey;
                if (string.IsNullOrEmpty(key))
                    continue;
                builder.Append("        public const string ").Append(entries[i].ApiName).Append(" = ")
                    .Append(ToLiteral(key)).AppendLine(";");
                wrote = true;
            }
        }

        private static void AppendKeyConstants(StringBuilder builder, List<PermitEntry> entries, ref bool wrote)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                string key = entries[i].Definition.StringKey;
                if (string.IsNullOrEmpty(key))
                    continue;
                builder.Append("        public const string ").Append(entries[i].ApiName).Append(" = ")
                    .Append(ToLiteral(key)).AppendLine(";");
                wrote = true;
            }
        }

        private static void AppendKeyArray(StringBuilder builder, string prefix, List<FloatEntry> entries)
        {
            builder.Append("        private static readonly string[] ").Append(prefix).AppendLine("Keys =");
            AppendKeyArrayValues(builder, entries);
        }

        private static void AppendKeyArray(StringBuilder builder, string prefix, List<PermitEntry> entries)
        {
            builder.Append("        private static readonly string[] ").Append(prefix).AppendLine("Keys =");
            AppendKeyArrayValues(builder, entries);
        }

        private static void AppendKeyArrayValues(StringBuilder builder, List<FloatEntry> entries)
        {
            builder.AppendLine("        {");
            for (int i = 0; i < entries.Count; i++)
                builder.Append("            ").Append(KeyReference(entries[i].ApiName, entries[i].Definition.StringKey)).AppendLine(",");
            builder.AppendLine("        };");
        }

        private static void AppendKeyArrayValues(StringBuilder builder, List<PermitEntry> entries)
        {
            builder.AppendLine("        {");
            for (int i = 0; i < entries.Count; i++)
                builder.Append("            ").Append(KeyReference(entries[i].ApiName, entries[i].Definition.StringKey)).AppendLine(",");
            builder.AppendLine("        };");
        }

        private static void AppendEnumKeyArray(StringBuilder builder, string prefix, List<FloatEntry> entries)
        {
            builder.Append("        private static readonly ushort[] ").Append(prefix).AppendLine("EnumKeys =");
            AppendEnumKeyArrayValues(builder, entries);
        }

        private static void AppendEnumKeyArray(StringBuilder builder, string prefix, List<PermitEntry> entries)
        {
            builder.Append("        private static readonly ushort[] ").Append(prefix).AppendLine("EnumKeys =");
            AppendEnumKeyArrayValues(builder, entries);
        }

        private static void AppendEnumKeyArrayValues(StringBuilder builder, List<FloatEntry> entries)
        {
            builder.AppendLine("        {");
            for (int i = 0; i < entries.Count; i++)
                builder.Append("            ").Append(entries[i].Definition.enumKey).AppendLine(",");
            builder.AppendLine("        };");
        }

        private static void AppendEnumKeyArrayValues(StringBuilder builder, List<PermitEntry> entries)
        {
            builder.AppendLine("        {");
            for (int i = 0; i < entries.Count; i++)
                builder.Append("            ").Append(entries[i].Definition.enumKey).AppendLine(",");
            builder.AppendLine("        };");
        }

        private static void AppendDefaultTable(StringBuilder builder, List<FloatEntry> floats, List<PermitEntry> permits)
        {
            builder.AppendLine("        public static ESSuperAttributeTable CreateDefaultSuperAttributeTable()");
            builder.AppendLine("        {");
            builder.AppendLine("            return new ESSuperAttributeTable");
            builder.AppendLine("            {");
            builder.AppendLine("                catalogScope = ESAttributeBakeTable.CharacterScope,");
            builder.AppendLine("                floatAttributes = new List<ESSuperFloatAttributeDefinition>");
            builder.AppendLine("                {");
            for (int i = 0; i < floats.Count; i++)
                AppendDefaultFloat(builder, floats[i]);
            builder.AppendLine("                },");
            builder.AppendLine("                permitAttributes = new List<ESSuperPermitAttributeDefinition>");
            builder.AppendLine("                {");
            for (int i = 0; i < permits.Count; i++)
                AppendDefaultPermit(builder, permits[i]);
            builder.AppendLine("                }");
            builder.AppendLine("            };");
            builder.AppendLine("        }");
        }

        private static void AppendDefaultFloat(StringBuilder builder, FloatEntry entry)
        {
            builder.Append("                    Float(").Append(entry.Definition.enumKey).Append(", ")
                .Append(KeyReference(entry.ApiName, entry.Definition.StringKey)).Append(", ")
                .Append(ToLiteral(entry.ApiName)).AppendLine("),");
        }

        private static void AppendDefaultPermit(StringBuilder builder, PermitEntry entry)
        {
            builder.Append("                    Permit(").Append(entry.Definition.enumKey).Append(", ")
                .Append(KeyReference(entry.ApiName, entry.Definition.StringKey)).Append(", ")
                .Append(ToLiteral(entry.ApiName)).AppendLine("),");
        }

        private static void AppendDefaultHelpers(StringBuilder builder)
        {
            builder.AppendLine("        private static ESSuperFloatAttributeDefinition Float(ushort enumKey, string key, string fixedApiName)");
            builder.AppendLine("        {");
            builder.AppendLine("            return new ESSuperFloatAttributeDefinition");
            builder.AppendLine("            {");
            builder.AppendLine("                enumKey = enumKey,");
            builder.AppendLine("                key = key,");
            builder.AppendLine("                fixedApiName = fixedApiName,");
            builder.AppendLine("                storagePolicy = ESKeyStoragePolicy.HotSlot");
            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static ESSuperPermitAttributeDefinition Permit(ushort enumKey, string key, string fixedApiName)");
            builder.AppendLine("        {");
            builder.AppendLine("            return new ESSuperPermitAttributeDefinition");
            builder.AppendLine("            {");
            builder.AppendLine("                enumKey = enumKey,");
            builder.AppendLine("                key = key,");
            builder.AppendLine("                fixedApiName = fixedApiName,");
            builder.AppendLine("                storagePolicy = ESKeyStoragePolicy.HotSlot");
            builder.AppendLine("            };");
            builder.AppendLine("        }");
        }

        private static void AppendLookupMethods(StringBuilder builder, List<FloatEntry> floats, List<PermitEntry> permits)
        {
            builder.AppendLine("        public static bool IsValid(ESCharacterFloatAttributeId id) => (uint)id < (uint)ESCharacterFloatAttributeId.Count;");
            builder.AppendLine("        public static bool IsValid(ESCharacterPermitAttributeId id) => (uint)id < (uint)ESCharacterPermitAttributeId.Count;");
            builder.AppendLine("        public static string GetKey(ESCharacterFloatAttributeId id) => IsValid(id) ? FloatKeys[(int)id] : null;");
            builder.AppendLine("        public static ushort GetEnumKey(ESCharacterFloatAttributeId id) => IsValid(id) ? FloatEnumKeys[(int)id] : (ushort)0;");
            builder.AppendLine("        public static string GetKey(ESCharacterPermitAttributeId id) => IsValid(id) ? PermitKeys[(int)id] : null;");
            builder.AppendLine("        public static ushort GetEnumKey(ESCharacterPermitAttributeId id) => IsValid(id) ? PermitEnumKeys[(int)id] : (ushort)0;");
            builder.AppendLine();
            AppendEnumLookup(builder, "Float", "ESCharacterFloatAttributeId", floats);
            builder.AppendLine();
            AppendEnumLookup(builder, "Permit", "ESCharacterPermitAttributeId", permits);
            builder.AppendLine();
            AppendStringLookup(builder, "Float", "ESCharacterFloatAttributeId", floats);
            builder.AppendLine();
            AppendStringLookup(builder, "Permit", "ESCharacterPermitAttributeId", permits);
            builder.AppendLine();
            builder.AppendLine("        public static bool TryGetValueKind(string key, out ESCharacterAttributeValueKind kind)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (TryGetFloatId(key, out _)) { kind = ESCharacterAttributeValueKind.Float; return true; }");
            builder.AppendLine("            if (TryGetPermitId(key, out _)) { kind = ESCharacterAttributeValueKind.Permit; return true; }");
            builder.AppendLine("            kind = default;");
            builder.AppendLine("            return false;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public static bool TryGetValueKind(ushort enumKey, out ESCharacterAttributeValueKind kind)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (TryGetFloatId(enumKey, out _)) { kind = ESCharacterAttributeValueKind.Float; return true; }");
            builder.AppendLine("            if (TryGetPermitId(enumKey, out _)) { kind = ESCharacterAttributeValueKind.Permit; return true; }");
            builder.AppendLine("            kind = default;");
            builder.AppendLine("            return false;");
            builder.AppendLine("        }");
        }

        private static void AppendEnumLookup<TEntry>(StringBuilder builder, string prefix, string idType, List<TEntry> entries)
            where TEntry : IFixedEntry
        {
            builder.Append("        public static bool TryGet").Append(prefix).Append("Id(ushort enumKey, out ").Append(idType).AppendLine(" id)");
            builder.AppendLine("        {");
            builder.Append("            for (int i = 0; i < ").Append(prefix).AppendLine("EnumKeys.Length; i++)");
            builder.Append("                if (").Append(prefix).AppendLine("EnumKeys[i] != 0 && " + prefix + "EnumKeys[i] == enumKey)");
            builder.AppendLine("                {");
            builder.Append("                    id = (").Append(idType).AppendLine(")i;");
            builder.AppendLine("                    return true;");
            builder.AppendLine("                }");
            builder.AppendLine("            id = default;");
            builder.AppendLine("            return false;");
            builder.AppendLine("        }");
        }

        private static void AppendStringLookup(StringBuilder builder, string prefix, string idType, List<FloatEntry> entries)
        {
            builder.Append("        public static bool TryGet").Append(prefix).Append("Id(string key, out ").Append(idType).AppendLine(" id)");
            builder.AppendLine("        {");
            builder.AppendLine("            switch (key)");
            builder.AppendLine("            {");
            AppendStringCases(builder, idType, entries);
            builder.AppendLine("                default: id = default; return false;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
        }

        private static void AppendStringLookup(StringBuilder builder, string prefix, string idType, List<PermitEntry> entries)
        {
            builder.Append("        public static bool TryGet").Append(prefix).Append("Id(string key, out ").Append(idType).AppendLine(" id)");
            builder.AppendLine("        {");
            builder.AppendLine("            switch (key)");
            builder.AppendLine("            {");
            AppendStringCases(builder, idType, entries);
            builder.AppendLine("                default: id = default; return false;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
        }

        private static void AppendStringCases(StringBuilder builder, string idType, List<FloatEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrEmpty(entries[i].Definition.StringKey))
                    continue;
                builder.Append("                case ESCharacterSuperAttributeKeys.").Append(entries[i].ApiName)
                    .Append(": id = ").Append(idType).Append('.').Append(entries[i].ApiName).AppendLine("; return true;");
            }
        }

        private static void AppendStringCases(StringBuilder builder, string idType, List<PermitEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrEmpty(entries[i].Definition.StringKey))
                    continue;
                builder.Append("                case ESCharacterSuperAttributeKeys.").Append(entries[i].ApiName)
                    .Append(": id = ").Append(idType).Append('.').Append(entries[i].ApiName).AppendLine("; return true;");
            }
        }

        private static string KeyReference(string apiName, string key)
        {
            return string.IsNullOrEmpty(key) ? "null" : "ESCharacterSuperAttributeKeys." + apiName;
        }

        private static string ToLiteral(string value)
        {
            if (value == null)
                return "null";

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '\"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (char.IsControl(character))
                            builder.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || (!char.IsLetter(value[0]) && value[0] != '_'))
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '_')
                    return false;
            }

            return true;
        }

        private static bool IsReservedIdentifier(string value)
        {
            switch (value)
            {
                case "Count":
                case "None":
                case "abstract": case "as": case "base": case "bool": case "break": case "byte":
                case "case": case "catch": case "char": case "checked": case "class": case "const":
                case "continue": case "decimal": case "default": case "delegate": case "do": case "double":
                case "else": case "enum": case "event": case "explicit": case "extern": case "false":
                case "finally": case "fixed": case "float": case "for": case "foreach": case "goto":
                case "if": case "implicit": case "in": case "int": case "interface": case "internal":
                case "is": case "lock": case "long": case "namespace": case "new": case "null":
                case "object": case "operator": case "out": case "override": case "params": case "private":
                case "protected": case "public": case "readonly": case "ref": case "return": case "sbyte":
                case "sealed": case "short": case "sizeof": case "stackalloc": case "static": case "string":
                case "struct": case "switch": case "this": case "throw": case "true": case "try":
                case "typeof": case "uint": case "ulong": case "unchecked": case "unsafe": case "ushort":
                case "using": case "virtual": case "void": case "volatile": case "while":
                    return true;
                default:
                    return false;
            }
        }

        private interface IFixedEntry
        {
            string ApiName { get; }
        }

        private sealed class FloatEntry : IFixedEntry
        {
            public readonly ESSuperFloatAttributeDefinition Definition;
            public string ApiName { get; }

            public FloatEntry(ESSuperFloatAttributeDefinition definition, string apiName)
            {
                Definition = definition;
                ApiName = apiName;
            }
        }

        private sealed class PermitEntry : IFixedEntry
        {
            public readonly ESSuperPermitAttributeDefinition Definition;
            public string ApiName { get; }

            public PermitEntry(ESSuperPermitAttributeDefinition definition, string apiName)
            {
                Definition = definition;
                ApiName = apiName;
            }
        }
    }
}
