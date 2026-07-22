using System;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    [Serializable]
    public struct ESEnumScriptJumpResult
    {
        public string enumTypeName;
        public string assetPath;
        public int enumLine;
        public int insertLine;
        public int memberLine;
        public bool found;

        public bool HasInsertLine => insertLine > 0;
        public bool HasMemberLine => memberLine > 0;
    }

    public static class ESEnumScriptJumpTemplate
    {
        public const string DefaultTitle = "ES 枚举补充请求";

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

            StringBuilder builder = new StringBuilder(768);
            builder.AppendLine(DefaultTitle);
            builder.AppendLine();
            builder.AppendLine("目标：给指定枚举追加一个新成员，或判断应继续使用 stringKey。");
            builder.AppendLine();
            builder.AppendLine("规则：");
            builder.AppendLine("1. 只修改枚举定义，不改运行时表、不改加载逻辑。");
            builder.AppendLine("2. 保留 None = 0，不重排、不复用已有数值。");
            builder.AppendLine("3. 新枚举值追加到枚举末尾，先检查语义是否重复。");
            builder.AppendLine("4. 如果这是临时、动态、外部配置驱动的数据，应说明继续使用 stringKey。");
            builder.AppendLine();
            builder.AppendLine("枚举类型：" + enumTypeName);
            builder.AppendLine("建议成员：" + memberName);
            builder.AppendLine("来源 stringKey：" + (desiredStringKey ?? string.Empty));
            builder.AppendLine("当前枚举值：" + (currentEnumValue ?? string.Empty));
            builder.AppendLine("脚本路径：" + (jump.assetPath ?? string.Empty));
            builder.AppendLine("枚举行：" + jump.enumLine);
            builder.AppendLine("建议插入行：" + jump.insertLine);
            return builder.ToString();
        }
    }

    public static class ESEnumScriptJump
    {
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
                EditorUtility.DisplayDialog("ES 枚举跳转", "没有找到枚举脚本：" + enumType?.Name, "确定");
                return false;
            }

            int line = openInsertLine && result.HasInsertLine ? result.insertLine : result.enumLine;
            return ESStandUtility.SafeEditor.OpenCodeAtLine(result.assetPath, line);
#else
            return false;
#endif
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

        private static bool TryFindEnum(Type enumType, string memberName, out ESEnumScriptJumpResult result)
        {
            result = default;
            if (enumType == null)
                return false;

            result.enumTypeName = enumType.Name;

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets(enumType.Name + " t:MonoScript");
            if (TryFindEnumInGuids(enumType.Name, memberName, guids, ref result))
                return true;
#endif

            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath))
                return false;

            string[] files = Directory.GetFiles(dataPath, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (TryReadEnumFile(enumType.Name, memberName, files[i], ref result))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private static bool TryFindEnumInGuids(string enumName, string memberName, string[] guids, ref ESEnumScriptJumpResult result)
        {
            if (guids == null)
                return false;

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                string fullPath = AssetPathToFullPath(assetPath);
                if (TryReadEnumFile(enumName, memberName, fullPath, ref result))
                    return true;
            }

            return false;
        }
#endif

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
            result.insertLine = FindInsertLine(text, enumIndex);
            result.memberLine = string.IsNullOrEmpty(memberName) ? 0 : FindMemberLine(text, enumIndex, memberName);
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

        private static int FindInsertLine(string text, int enumIndex)
        {
            int openBrace = text.IndexOf('{', enumIndex);
            if (openBrace < 0)
                return CountLine(text, enumIndex);

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
                        return CountLine(text, i);
                }
            }

            return CountLine(text, openBrace);
        }

        private static int FindMemberLine(string text, int enumIndex, string memberName)
        {
            int openBrace = text.IndexOf('{', enumIndex);
            int closeBraceLine = FindInsertLine(text, enumIndex);
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
}
