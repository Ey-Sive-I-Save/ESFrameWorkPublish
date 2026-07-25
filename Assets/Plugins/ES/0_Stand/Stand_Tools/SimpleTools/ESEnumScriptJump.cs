using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public sealed class ESEnumScriptAttribute : Attribute
    {
        public readonly string assetPath;
        public readonly string note;

        public ESEnumScriptAttribute(string assetPath = null, string note = null)
        {
            this.assetPath = assetPath;
            this.note = note;
        }
    }

    [Serializable]
    public struct ESEnumScriptJumpResult
    {
        public string enumTypeName;
        public string assetPath;
        public int enumLine;
        public int openBraceLine;
        public int closeBraceLine;
        public int insertLine;
        public int memberLine;
        public bool singleLineEnum;
        public bool found;

        public bool HasInsertLine => insertLine > 0;
        public bool HasMemberLine => memberLine > 0;
        public string EditPositionText
        {
            get
            {
                if (!found)
                    return "enum script not registered or not found";

                return singleLineEnum
                    ? "single-line enum: edit this line or expand it before appending"
                    : "append before the enum closing brace";
            }
        }
    }

    public static class ESEnumScriptJumpTemplate
    {
        public const string DefaultTitle = "ES enum append request";

        public static string ToEnumMemberName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NewKey";

            StringBuilder builder = new StringBuilder(value.Length);
            bool upperNext = true;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                }
                else
                {
                    upperNext = true;
                }
            }

            if (builder.Length == 0)
                return "NewKey";

            if (char.IsDigit(builder[0]))
                builder.Insert(0, "Key");

            return builder.ToString();
        }

        public static string BuildAppendRequest(Type enumType, string desiredStringKey, string currentEnumValue, ESEnumScriptJumpResult jump)
        {
            string enumTypeName = enumType != null ? enumType.Name : "UnknownEnum";
            string memberName = ToEnumMemberName(desiredStringKey);

            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine(DefaultTitle);
            builder.AppendLine();
            builder.AppendLine("Goal: add one enum member, or decide to keep stringKey if this is dynamic data.");
            builder.AppendLine();
            builder.AppendLine("Rules:");
            builder.AppendLine("1. Only edit the enum declaration file.");
            builder.AppendLine("2. Keep None = 0. Do not reorder or reuse existing values.");
            builder.AppendLine("3. Append at the end of the enum and check semantic duplication first.");
            builder.AppendLine("4. If the data should stay external/dynamic, keep stringKey and do not add enum.");
            builder.AppendLine();
            builder.AppendLine("enumType: " + enumTypeName);
            builder.AppendLine("suggestedMember: " + memberName);
            builder.AppendLine("sourceStringKey: " + (desiredStringKey ?? string.Empty));
            builder.AppendLine("currentEnumValue: " + (currentEnumValue ?? string.Empty));
            builder.AppendLine("scriptPath: " + (jump.assetPath ?? string.Empty));
            builder.AppendLine("enumLine: " + jump.enumLine);
            builder.AppendLine("openBraceLine: " + jump.openBraceLine);
            builder.AppendLine("closeBraceLine: " + jump.closeBraceLine);
            builder.AppendLine("suggestedInsertLine: " + jump.insertLine);
            builder.AppendLine("singleLineEnum: " + jump.singleLineEnum);
            builder.AppendLine("editPosition: " + jump.EditPositionText);
            builder.AppendLine();
            builder.AppendLine("suggestedCode:");
            builder.AppendLine("    " + memberName + " = <next_value>,");
            return builder.ToString();
        }
    }

    public static class ESEnumScriptJump
    {
        private static readonly Dictionary<Type, ESEnumScriptJumpResult> ResultByEnumType = new Dictionary<Type, ESEnumScriptJumpResult>(128);

        public static void ClearRegistry()
        {
            ResultByEnumType.Clear();
        }

        public static bool RegisterEnum(Type enumType, ESEnumScriptAttribute attribute)
        {
            if (enumType == null || !enumType.IsEnum)
                return false;

            ESEnumScriptJumpResult result = new ESEnumScriptJumpResult
            {
                enumTypeName = enumType.Name
            };

#if UNITY_EDITOR
            if (attribute != null && !string.IsNullOrEmpty(attribute.assetPath))
            {
                string fullPath = AssetPathToFullPath(attribute.assetPath);
                if (TryReadEnumFile(enumType.Name, null, fullPath, ref result))
                {
                    ResultByEnumType[enumType] = result;
                    return true;
                }
            }
#endif

            result.found = false;
            ResultByEnumType[enumType] = result;
            return false;
        }

        public static bool IsRegistered(Type enumType)
        {
            return enumType != null && ResultByEnumType.ContainsKey(enumType);
        }

        public static bool TryFindEnum(Type enumType, out ESEnumScriptJumpResult result)
        {
            return TryFindEnum(enumType, null, out result);
        }

        public static bool TryFindEnumMember(Type enumType, string memberName, out ESEnumScriptJumpResult result)
        {
            return TryFindEnum(enumType, memberName, out result);
        }

        public static bool OpenEnum(Type enumType, bool openInsertLine = false)
        {
#if UNITY_EDITOR
            if (!TryFindEnum(enumType, out ESEnumScriptJumpResult result))
            {
                EditorUtility.DisplayDialog("ES Enum Jump", "Enum script is not registered or not found: " + enumType?.Name, "OK");
                return false;
            }

            int line = openInsertLine && result.HasInsertLine ? result.insertLine : result.enumLine;
            return ESStandUtility.SafeEditor.OpenCodeAtLine(result.assetPath, line);
#else
            return false;
#endif
        }

        public static bool OpenEnumAppendPosition(Type enumType)
        {
            return OpenEnum(enumType, true);
        }

        public static bool OpenEnumMember(Type enumType, string memberName)
        {
#if UNITY_EDITOR
            if (!TryFindEnumMember(enumType, memberName, out ESEnumScriptJumpResult result))
                return OpenEnum(enumType, false);

            int line = result.HasMemberLine ? result.memberLine : result.enumLine;
            return ESStandUtility.SafeEditor.OpenCodeAtLine(result.assetPath, line);
#else
            return false;
#endif
        }

        public static string BuildAppendRequest(Type enumType, string desiredStringKey, string currentEnumValue = null)
        {
            TryFindEnum(enumType, out ESEnumScriptJumpResult result);
            return ESEnumScriptJumpTemplate.BuildAppendRequest(enumType, desiredStringKey, currentEnumValue, result);
        }

        public static void CopyAppendRequest(Type enumType, string desiredStringKey, string currentEnumValue = null)
        {
            GUIUtility.systemCopyBuffer = BuildAppendRequest(enumType, desiredStringKey, currentEnumValue);
        }

        public static bool CopyAppendRequestAndOpenAppendPosition(Type enumType, string desiredStringKey, string currentEnumValue = null)
        {
            CopyAppendRequest(enumType, desiredStringKey, currentEnumValue);
            return OpenEnumAppendPosition(enumType);
        }

        private static bool TryFindEnum(Type enumType, string memberName, out ESEnumScriptJumpResult result)
        {
            result = default;
            if (enumType == null)
                return false;

            if (!ResultByEnumType.TryGetValue(enumType, out result))
            {
                ESEnumScriptAttribute attribute = Attribute.GetCustomAttribute(enumType, typeof(ESEnumScriptAttribute)) as ESEnumScriptAttribute;
                if (attribute == null)
                {
                    result = new ESEnumScriptJumpResult
                    {
                        enumTypeName = enumType.Name,
                        found = false
                    };
                    return false;
                }

                RegisterEnum(enumType, attribute);
                ResultByEnumType.TryGetValue(enumType, out result);
            }

            if (!result.found || string.IsNullOrEmpty(memberName))
                return result.found;

            ESEnumScriptJumpResult memberResult = result;
            string fullPath = AssetPathToFullPath(result.assetPath);
            if (TryReadEnumFile(enumType.Name, memberName, fullPath, ref memberResult))
            {
                result = memberResult;
                return true;
            }

            return result.found;
        }

        private static bool TryReadEnumFile(string enumName, string memberName, string fullPath, ref ESEnumScriptJumpResult result)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return false;

            string text = File.ReadAllText(fullPath);
            int enumIndex = FindEnumKeyword(text, enumName);
            if (enumIndex < 0)
                return false;

            result.assetPath = FullPathToAssetPath(fullPath);
            result.enumLine = CountLine(text, enumIndex);
            FillBraceAndInsertLines(text, enumIndex, ref result);
            result.memberLine = string.IsNullOrEmpty(memberName) ? 0 : FindMemberLine(text, enumIndex, memberName, result.closeBraceLine);
            result.found = true;
            return true;
        }

        private static int FindEnumKeyword(string text, string enumName)
        {
            string pattern = "enum " + enumName;
            int index = text.IndexOf(pattern, StringComparison.Ordinal);
            if (index >= 0)
                return index;

            pattern = "enum\t" + enumName;
            return text.IndexOf(pattern, StringComparison.Ordinal);
        }

        private static void FillBraceAndInsertLines(string text, int enumIndex, ref ESEnumScriptJumpResult result)
        {
            int openBrace = text.IndexOf('{', enumIndex);
            if (openBrace < 0)
            {
                result.openBraceLine = 0;
                result.closeBraceLine = 0;
                result.insertLine = result.enumLine;
                result.singleLineEnum = false;
                return;
            }

            int depth = 0;
            for (int i = openBrace; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        result.openBraceLine = CountLine(text, openBrace);
                        result.closeBraceLine = CountLine(text, i);
                        result.insertLine = result.closeBraceLine;
                        result.singleLineEnum = result.openBraceLine == result.closeBraceLine;
                        return;
                    }
                }
            }

            result.openBraceLine = CountLine(text, openBrace);
            result.closeBraceLine = 0;
            result.insertLine = result.openBraceLine;
            result.singleLineEnum = false;
        }

        private static int FindMemberLine(string text, int enumIndex, string memberName, int closeBraceLine)
        {
            int openBrace = text.IndexOf('{', enumIndex);
            if (openBrace < 0 || closeBraceLine <= 0)
                return 0;

            int closeIndex = FindIndexAtLine(text, closeBraceLine);
            if (closeIndex <= openBrace)
                closeIndex = text.Length;

            int index = text.IndexOf(memberName, openBrace, closeIndex - openBrace, StringComparison.Ordinal);
            return index >= 0 ? CountLine(text, index) : 0;
        }

        private static int CountLine(string text, int index)
        {
            int line = 1;
            int max = Mathf.Clamp(index, 0, text.Length);
            for (int i = 0; i < max; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static int FindIndexAtLine(string text, int targetLine)
        {
            if (targetLine <= 1)
                return 0;

            int line = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n')
                    continue;

                line++;
                if (line == targetLine)
                    return i + 1;
            }

            return text.Length;
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + normalized.Substring(dataPath.Length);

            return normalized;
        }

#if UNITY_EDITOR
        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath)).Replace('\\', '/');
        }
#endif
    }

    public sealed class ESAS_Register_ESEnumScriptJump : EditorRegister_FOR_ClassAttribute<ESEnumScriptAttribute>
    {
        public override int Order => EditorRegisterOrder.Level0.GetHashCode();

        public override void Handle(ESEnumScriptAttribute attribute, Type type)
        {
            ESEnumScriptJump.RegisterEnum(type, attribute);
        }
    }

    public sealed class ESEnumScriptJumpClearOnAssemblyStream : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESEnumScriptJump.ClearRegistry();
        }
    }
}
