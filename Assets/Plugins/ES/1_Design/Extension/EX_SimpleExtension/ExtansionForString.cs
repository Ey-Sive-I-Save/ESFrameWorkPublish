#define USE_ES

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Windows;

namespace ES
{
    public static class ExtensionForString
    {
        #region 截取系列
        // ================== 基础截取方法 ==================

        /// <summary>
        /// 按第一个分隔符保留之前的字符串
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="includeSeparator">保留分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)​​</param>
        /// <returns></returns>
        public static string _KeepBeforeByFirst(this string source, string separator,bool includeSeparator = false,StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.IndexOf(separator, comparison);
            if (index < 0) return source; // 未找到分隔符返回原字符串

            return includeSeparator ?
                source.Substring(0, index + separator.Length) :
                source.Substring(0, index);
        }
        /// <summary>
        /// 按最后一个分隔符保留之前的字符串
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="includeSeparator">保留分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)</param>
        /// <returns></returns>
        public static string _KeepBeforeByLast(this string source, string separator,bool includeSeparator = false,StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.LastIndexOf(separator, comparison);
            if (index < 0) return source; // 未找到分隔符返回原字符串

            return includeSeparator ?
                source.Substring(0, index + separator.Length) :
                source.Substring(0, index);
        }
        /// <summary>
        /// 按第一个分隔符保留之后的字符串
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="includeSeparator">保留分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)​​</param>
        /// <returns></returns>
        public static string _KeepAfterByFirst(this string source, string separator,bool includeSeparator = false,StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.IndexOf(separator, comparison);
            if (index < 0) return source; // 未找到分隔符返回原字符串

            return includeSeparator ?
                source.Substring(index) :
                source.Substring(index + separator.Length);
        }
        /// <summary>
        /// 按最后一个分隔符保留之后的字符串
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="includeSeparator">保留分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)​​</param>
        /// <returns></returns>
        public static string _KeepAfterByLast(this string source, string separator,bool includeSeparator = false,StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.LastIndexOf(separator, comparison);
            if (index < 0) return source; // 未找到分隔符返回原字符串

            return includeSeparator ?
                source.Substring(index) :
                source.Substring(index + separator.Length);
        }


        // ================== 保留操作 ==================

        /// <summary>
        /// 按第一个分隔符切断保留前面的(不包含分隔符)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)​​</param>
        /// <returns></returns>
        public static string _KeepBeforeCutFlag(this string source, string separator,StringComparison comparison = StringComparison.Ordinal)
        {
            return source._KeepBeforeByFirst(separator, false, comparison);
        }
        /// <summary>
        /// 按最后一个分隔符切断，保留最后的(不包含分隔符)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)​​</param>
        /// <returns></returns>
        public static string _KeepAfterCutFlag(this string source, string separator,StringComparison comparison = StringComparison.Ordinal)
        {
            return source._KeepAfterByLast(separator, false, comparison);
        }
        /// <summary>
        /// 保留两个分隔符之间的内容
        /// </summary>
        /// <param name="source"></param>
        /// <param name="startSeparator">前面的分隔符</param>
        /// <param name="endSeparator">后面的分隔符</param>
        /// <param name="comparison">比较规则​​(自己查)​​</param>
        /// <returns></returns>
        public static string _KeepBetween(this string source, string startSeparator, string endSeparator,bool includeSeparators = false,StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int startIndex = source.IndexOf(startSeparator, comparison);
            if (startIndex < 0) return string.Empty;

            int endIndex = source.IndexOf(endSeparator, startIndex + startSeparator.Length, comparison);
            if (endIndex < 0) return string.Empty;

            if (includeSeparators)
            {
                return source.Substring(startIndex, endIndex - startIndex + endSeparator.Length);
            }

            return source.Substring(
                startIndex + startSeparator.Length,
                endIndex - startIndex - startSeparator.Length
            );
        }

        // ================== 字符版本（优化性能） ==================

        /// <summary>
        /// 按第一个分隔符保留之前的字符串(字符优化版)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <returns></returns>
        public static string _KeepBeforeByFirstChar(this string source, char separator)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.IndexOf(separator);
            return index < 0 ? source : source.Substring(0, index);
        }
        /// <summary>
        /// 按最后一个分隔符保留之前的字符串(字符优化版)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <returns></returns>
        public static string _KeepBeforeByLastChar(this string source, char separator)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.LastIndexOf(separator);
            return index < 0 ? source : source.Substring(0, index);
        }
        /// <summary>
        /// 按第一个分隔符保留之前的字符串(字符优化版)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <returns></returns>
        public static string _KeepAfterByFirstChar(this string source, char separator)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.IndexOf(separator);
            return index < 0 ? source : source.Substring(index + 1);
        }
        /// <summary>
        /// 按最后一个分隔符保留之后的字符串(字符优化版)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="separator">分隔符</param>
        /// <returns></returns>
        public static string _KeepAfterByLastChar(this string source, char separator)
        {
            if (string.IsNullOrEmpty(source)) return source;

            int index = source.LastIndexOf(separator);
            return index < 0 ? source : source.Substring(index + 1);
        }

        /// <summary>
        /// 限制最长有效字符串数量(加后缀)
        /// </summary>
        /// <param name="maxLength">有效字符数</param>
        /// <param name="end">结尾</param>
        /// <returns></returns>
        public static string _KeepBeforeByMaxLength(this string str, int maxLength, string end = "...")
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
                return str;

            return str.Substring(0, maxLength) + end;
        }
        #endregion

        #region 特征查询部分
        /// <summary>
        /// 有效的邮箱
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool _IsValidEmail(this string str)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(str);
                return addr.Address == str;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 有效的URL网址
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool _IsValidUrl(this string str)
        {
            Uri uriResult;
            return Uri.TryCreate(str, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
        /// <summary>
        /// 有效的数字
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool _IsNumeric(this string str)
        {
            return double.TryParse(str, out _);
        }
        /// <summary>
        /// 含有空格
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool _HasSpace(this string input)
        {
            return input.Contains(" ");
        }
        /// <summary>
        /// 包含中文汉字
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool _ContainsChineseCharacterOnly(this string input)
        {
            return Regex.IsMatch(input, @"[\u4e00-\u9fa5]");
        }
        /// <summary>
        /// 包含中文汉字或者符号
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool _ContainChineseCharacterOrChineseSymbol(this string input)
        {
            return Regex.IsMatch(input, @"[\u4e00-\u9fa5]") || Regex.IsMatch(input, @"[。？！，、；：“”‘’（）《》——……·【】·^ ∧ ¶ =  # / ∞ △ ※ ●＋ － × ÷ ± ≌ ∽ ≤ ≥ ≠ ≡ ∫ ∮ ∑ ∏]");
        }
        /// <summary>
        /// 包含中文汉字或者任意符号
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool _ContainChineseCharacterOrNormalSymbol(this string input)
        {
            return Regex.IsMatch(input, @"[\u4e00-\u9fa5]")
                || Regex.IsMatch(input, @"[。？！，、；：“”‘’（）《》——……·【】·^ ∧ ¶ =  # / ∞ △ ※ ●＋ － × ÷ ± ≌ ∽ ≤ ≥ ≠ ≡ ∫ ∮ ∑ ∏]")
                || Regex.IsMatch(input, @"[. , ? ! ' "" : ; ... — –  ( )   { } + − × ÷ = ≠ ≈ ± ≤ ≥ % ° °C °F π ∫ ∑ ∏ √ ∞ / \ | # & * ~ @ $ £ € ¥ ¢ ^]");

        }
        /// <summary>
        /// 是C# 关键字
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool _IsCSharpKeyword(this string value)
        {
            return Array.Exists(keywords, k => k.Equals(value, StringComparison.Ordinal));
        }
        public static string[] keywords = {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while"
        };

        /// <summary>
        /// 是有效的标识符名字
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool _IsValidIdentName(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // 步骤1：查询是否包含所有非字母、数字、下划线的字符[6,7](@ref)
            if (Regex.IsMatch(input, @"\W"))
                return false;


            // 步骤2：查询是否以数字开头
            if (char.IsDigit(input[0]))
                return false;

            // 步骤3：查询连续下划线
            if (Regex.IsMatch(input, @"_{2,}"))
                return false;

            // 步骤4：防止C#关键字冲突
            if (input._IsCSharpKeyword())
                return false;

            return true;
        }

        /// <summary>
        /// 获得斜线数量
        /// </summary>
        /// <param name="selfStr"></param>
        /// <returns></returns>
        public static int _GetSlashCount(this string selfStr)
        {
            string nor = selfStr.Replace("\\", "/");
            return nor.Count((n) => n == '/');

        }
        #endregion

        #region 操作部分
        /// <summary>
        /// 移除多余的空格
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string _RemoveExtraSpaces(this string str)
        {
            return Regex.Replace(str, @"\s+", " ").Trim();
        }
        /// <summary>
        /// 把一系列字符串,以分隔符加入连接
        /// </summary>
        /// <param name="self"></param>
        /// <param name="separator">分隔</param>
        /// <returns></returns>
        public static string _StringJoin(this IEnumerable<string> self, string separator)
        {
            return string.Join(separator, self);
        }
        /// <summary>
        /// 移除各种字符串
        /// </summary>
        /// <param name="str"></param>
        /// <param name="targets">各种字符串</param>
        /// <returns></returns>
        public static string _RemoveSubStrings(this string str, params string[] targets)
        {
            return targets.Aggregate(str, (current, t) => current.Replace(t, string.Empty));
        }
        /// <summary>
        /// 移除最后的扩展名
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string _RemoveExtension(this string str)
        {
            int thePoint = str.LastIndexOf('.');
            if (thePoint >= 0) return str.Substring(0, thePoint);
            return str;
        }
        /// <summary>
        /// 在前后分别添加
        /// </summary>
        /// <param name="str"></param>
        /// <param name="pre">前</param>
        /// <param name="last">后</param>
        /// <returns></returns>
        public static string _AddPreAndLast(this string str, string pre, string last)
        {
            return pre + str + last;
        }
        /// <summary>
        /// 首字母大写(排除空字符)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string _FirstCharToUpperCapitalize(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // 查找第一个非空白字符的索引
            int firstCharIndex = 0;
            while (firstCharIndex < input.Length && char.IsWhiteSpace(input[firstCharIndex]))
            {
                firstCharIndex++;
            }

            if (firstCharIndex >= input.Length)
                return input;

            // 大写化第一个有效字符
            return input.Substring(0, firstCharIndex) +
                   char.ToUpper(input[firstCharIndex]) +
                   input.Substring(firstCharIndex + 1);
        }
        /// <summary>
        /// 首字母大写(简单)
        /// </summary>
        /// <param name="input"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public static string _FirstUpper(this string input, CultureInfo culture)
        {
            // 处理单字符情况
            if (input.Length == 1)
                return char.ToUpper(input[0], culture).ToString();

            // 处理已大写的情况（避免重复操作）
            if (char.IsUpper(input[0]))
                return input;

            // 核心处理：首字母大写 + 剩余部分保持不变
            return char.ToUpper(input[0], culture) + input.Substring(1);
        }

        /// <summary>
        /// 将字符串首字母小写(简单)
        /// </summary>
        public static string _FirstLower(this string input, CultureInfo culture)
        {
            // 处理单字符情况
            if (input.Length == 1)
                return char.ToLower(input[0], culture).ToString();

            // 处理已小写的情况（避免重复操作）
            if (char.IsLower(input[0]))
                return input;

            // 核心处理：首字母小写 + 剩余部分保持不变
            return char.ToLower(input[0], culture) + input.Substring(1);
        }
        /// <summary>
        /// 转换为有效的标识符
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string _ToValidIdentName(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return "_";

            // 步骤1：替换所有非字母、数字、下划线的字符为下划线[6,7](@ref)
            string normalized = Regex.Replace(input, @"[\W]", "_");

            // 步骤2：处理开头数字（添加下划线前缀）[3,4](@ref)
            if (char.IsDigit(normalized[0]))
                normalized = "_" + normalized;

            // 步骤3：合并连续下划线[6](@ref)
            normalized = Regex.Replace(normalized, @"_{2,}", "_");

            // 步骤4：处理C#关键字冲突（添加@前缀）[3,10](@ref)
            if (normalized._IsCSharpKeyword())
                return "@" + normalized;

            return normalized;
        }
        /// <summary>
        /// 转化成代码格式
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string _ToCode(this string code)
        {
            int indentLevel = 0;
            string cleanedCode = Regex.Replace(code, @"\r?\n", "\n"); // 统一换行符
            string pattern = @"[ \t]+";  // \s 匹配使用的空白符（空格、制表符等）
            cleanedCode = Regex.Replace(cleanedCode, pattern, " "); // 最多保留两个连续空行和空格

            StringBuilder sb = new StringBuilder();
            foreach (char c in cleanedCode)
            {
                if (c == '{') indentLevel++;
                else if (c == '}')
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }

                sb.Append(c);
                if (c == '\n')
                {
                    sb.Append(new string(' ', indentLevel * 4));
                }
            }

            return sb.ToString() + "\n//ES已修正";
        }
        #endregion

        #region Buffer专属
        /// <summary>
        /// 获得一个StringBuilder
        /// </summary>
        /// <param name="selfStr"></param>
        /// <returns></returns>
        public static StringBuilder _AsBuilder(this string selfStr)
        {
            return new StringBuilder(selfStr);
        }

        /// <summary>
        /// 在开头加入
        /// </summary>
        /// <param name="self"></param>
        /// <param name="prefixString"></param>
        /// <returns></returns>
        public static StringBuilder _AddPre(this StringBuilder self, string prefixString)
        {
            self.Insert(0, prefixString);
            return self;
        }

        #endregion

        #region 转化部分
        /// <summary>
        /// 转化为"字符串"
        /// </summary>
        /// <param name="selfStr"></param>
        /// <returns></returns>
        public static string _AsStringValue(this string selfStr)
        {
            return $"\"{selfStr}\"";
        }
        /// <summary>
        /// 转化为 整数
        /// </summary>
        /// <param name="selfStr"></param>
        /// <param name="defaulValue">转化失败的结果</param>
        /// <returns></returns>
        public static int _AsInt(this string selfStr, int defaulValue = 0)
        {
            return int.TryParse(selfStr, out int retValue) ? retValue : defaulValue;
        }
        /// <summary>
        /// 转化为 long整数
        /// </summary>
        /// <param name="self"></param>
        /// <param name="defaultValue">转化失败的结果</param>
        /// <returns></returns>
        public static long _AsLong(this string self, long defaultValue = 0)
        {
            var retValue = defaultValue;
            return long.TryParse(self, out retValue) ? retValue : defaultValue;
        }

        /// <summary>
        /// 转化为 DateTime
        /// </summary>
        /// <param name="selfStr"></param>
        /// <param name="defaultValue">转化失败的结果</param>
        /// <returns></returns>
        public static DateTime _AsDateTime(this string selfStr, DateTime defaultValue = default(DateTime))
        {
            return DateTime.TryParse(selfStr, out var retValue) ? retValue : defaultValue;
        }
        /// <summary>
        /// 转化为 Float浮点数
        /// </summary>
        /// <param name="selfStr"></param>
        /// <param name="defaultValue">转化失败的结果</param>
        /// <returns></returns>
        public static float _AsFloat(this string selfStr, float defaultValue = 0)
        {
            return float.TryParse(selfStr, out var retValue) ? retValue : defaultValue;
        }
        /// <summary>
        /// 转化为Hash值<MD5算法>
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string _ToMD5Hash(this string str)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(str));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
        /// <summary>
        /// 转化为Hash值<SHA1算法>
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string _ToSHA1Hash(this string str)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(str));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
        /// <summary>
        /// 转化为Hash值<Sha256算法>
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string _ToSha256Hash(this string str)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        #endregion
    }

}