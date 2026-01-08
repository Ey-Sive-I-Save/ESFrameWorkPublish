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
    /// <summary>
    /// ES框架 - 字符串扩展方法类
    /// 
    /// 【核心功能】
    /// 提供丰富的字符串操作扩展方法，涵盖字符串截取、特征检测、类型转换、格式处理等功能
    /// 
    /// 【设计理念】
    /// • 链式调用：所有扩展方法支持fluent API风格
    /// • 性能优化：提供字符和字符串双版本，优化常用场景性能
    /// • 安全处理：内置空值检查和异常处理，提供默认值保护
    /// • 命名统一：使用_前缀标识扩展方法，语义清晰直观
    /// 
    /// 【功能分类】
    /// • 截取系列：按分隔符、长度、位置进行字符串截取和保留
    /// • 特征查询：邮箱、URL、数字、中文等格式验证和特征检测  
    /// • 操作处理：字符串清理、转换、格式化等操作
    /// • 类型转换：安全的类型转换，支持默认值和异常处理
    /// • 加密哈希：MD5、SHA1、SHA256等哈希算法支持
    /// 
    /// 【使用示例】
    /// <code>
    /// string email = "user@example.com";
    /// bool isValid = email._IsValidEmail();  // 验证邮箱格式
    /// 
    /// string path = "/folder/file.txt";
    /// string filename = path._KeepAfterByLastChar('/');  // 获取文件名
    /// 
    /// string number = "123.45";
    /// float value = number._AsFloat(0f);  // 安全转换为浮点数
    /// </code>
    /// </summary>
    public static class ExtForString_Main
    {
        #region 截取系列
        // ==================================================================================
        // 字符串截取和保留操作
        // 
        // 【功能概述】
        // 提供基于分隔符、位置、长度的字符串截取功能，支持灵活的保留策略
        // 
        // 【核心方法】
        // • KeepBefore系列：保留分隔符之前的内容
        // • KeepAfter系列：保留分隔符之后的内容  
        // • KeepBetween：保留两个分隔符之间的内容
        // • 字符优化版本：使用char参数提升性能
        // 
        // 【参数说明】
        // • includeSeparator：是否在结果中包含分隔符
        // • comparison：字符串比较规则（大小写、文化等）
        // • First vs Last：使用第一个还是最后一个分隔符
        // ==================================================================================

        // ================== 基础截取方法 ==================

        /// <summary>
        /// 按第一个分隔符保留之前的字符串
        /// 
        /// 【功能描述】
        /// 在源字符串中查找第一个匹配的分隔符，返回该分隔符之前的所有内容
        /// 如果未找到分隔符，则返回原始字符串
        /// 
        /// 【使用场景】
        /// • 文件路径处理：从完整路径中提取目录部分
        /// • URL解析：从URL中提取协议或域名部分
        /// • 数据解析：从格式化字符串中提取前缀部分
        /// 
        /// 【示例】
        /// <code>
        /// "hello-world-test"._KeepBeforeByFirst("-")        // 返回: "hello"
        /// "hello-world-test"._KeepBeforeByFirst("-", true)  // 返回: "hello-" 
        /// "noSeparator"._KeepBeforeByFirst("-")            // 返回: "noSeparator"
        /// </code>
        /// </summary>
        /// <param name="source">源字符串</param>
        /// <param name="separator">分隔符字符串</param>
        /// <param name="includeSeparator">是否在结果中包含分隔符，默认false</param>
        /// <param name="comparison">字符串比较规则，默认为序数比较</param>
        /// <returns>分隔符之前的字符串，如果未找到分隔符则返回原字符串</returns>
        public static string _KeepBeforeByFirst(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
        public static string _KeepBeforeByLast(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
        public static string _KeepAfterByFirst(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
        public static string _KeepAfterByLast(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
        public static string _KeepBeforeCutFlag(this string source, string separator, StringComparison comparison = StringComparison.Ordinal)
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
        public static string _KeepAfterCutFlag(this string source, string separator, StringComparison comparison = StringComparison.Ordinal)
        {
            return source._KeepAfterByLast(separator, false, comparison);
        }
        /// <summary>
        /// 保留两个分隔符之间的内容
        /// 
        /// 【功能描述】
        /// 查找起始分隔符和结束分隔符，返回它们之间的内容
        /// 支持选择是否在结果中包含分隔符本身
        /// 
        /// 【查找策略】
        /// • 从字符串开头查找起始分隔符
        /// • 从起始分隔符之后查找结束分隔符
        /// • 任一分隔符未找到时返回空字符串
        /// 
        /// 【使用场景】
        /// • XML/HTML标签内容提取
        /// • 配置文件值提取：key=value中的value
        /// • 括号内容提取：func(params)中的params
        /// • JSON字段值提取
        /// 
        /// 【示例】
        /// <code>
        /// "[Hello World]"._KeepBetween("[", "]")           // 返回: "Hello World"
        /// "[Hello World]"._KeepBetween("[", "]", true)     // 返回: "[Hello World]"
        /// "func(param1,param2)"._KeepBetween("(", ")")     // 返回: "param1,param2"
        /// "noEndTag[content"._KeepBetween("[", "]")        // 返回: ""
        /// </code>
        /// </summary>
        /// <param name="source">源字符串</param>
        /// <param name="startSeparator">起始分隔符</param>
        /// <param name="endSeparator">结束分隔符</param>
        /// <param name="includeSeparators">是否在结果中包含两个分隔符，默认false</param>
        /// <param name="comparison">字符串比较规则，默认为序数比较</param>
        /// <returns>两个分隔符之间的内容，如果任一分隔符未找到则返回空字符串</returns>
        public static string _KeepBetween(this string source, string startSeparator, string endSeparator, bool includeSeparators = false, StringComparison comparison = StringComparison.Ordinal)
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

        // ================== 字符版本（性能优化） ==================
        // 
        // 【性能优势】
        // 当分隔符为单个字符时，使用char参数比string参数性能更好：
        // • 避免字符串比较的开销
        // • IndexOf(char)比IndexOf(string)更快
        // • 减少内存分配和垃圾回收压力
        // 
        // 【使用建议】
        // • 分隔符为单字符时优先使用字符版本
        // • 高频调用场景建议使用字符版本
        // • 路径处理、CSV解析等场景的首选方案
        // ==================

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
        // ==================================================================================
        // 字符串特征检测和格式验证
        // 
        // 【功能概述】
        // 提供各种字符串格式验证和特征检测功能，涵盖常用的数据格式和字符特征
        // 
        // 【验证类型】
        // • 格式验证：邮箱、URL、数字等标准格式
        // • 字符特征：中文字符、空格、符号等字符类型检测  
        // • 编程相关：C#关键字、标识符有效性等
        // • 文件路径：路径分隔符统计等
        // 
        // 【设计原则】
        // • 返回bool值，便于条件判断
        // • 内置异常处理，避免验证过程中的错误
        // • 基于正则表达式和.NET内置验证
        // ==================================================================================
        /// <summary>
        /// 验证字符串是否为有效的邮箱地址格式
        /// 
        /// 【验证方法】
        /// 使用.NET内置的MailAddress类进行验证，比正则表达式更准确可靠
        /// 
        /// 【验证规则】
        /// • 符合RFC 2822邮箱地址标准
        /// • 包含有效的用户名和域名部分
        /// • 域名部分符合DNS规范
        /// • 支持国际化域名
        /// 
        /// 【使用场景】
        /// • 用户注册时的邮箱格式验证
        /// • 邮件发送前的地址检查
        /// • 配置文件中邮箱设置的有效性验证
        /// • 数据导入时的邮箱字段清洗
        /// 
        /// 【示例】
        /// <code>
        /// "user@example.com"._IsValidEmail()     // 返回: true
        /// "invalid.email"._IsValidEmail()        // 返回: false
        /// "user@sub.domain.com"._IsValidEmail()  // 返回: true
        /// ""._IsValidEmail()                     // 返回: false
        /// </code>
        /// </summary>
        /// <param name="str">待验证的字符串</param>
        /// <returns>如果是有效邮箱格式返回true，否则返回false</returns>
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
        /// 检测字符串是否包含中文汉字
        /// 
        /// 【检测范围】
        /// 使用Unicode范围 \u4e00-\u9fa5 检测中文汉字字符
        /// • \u4e00: 中文汉字起始码位（一）
        /// • \u9fa5: 中文汉字结束码位（龥）
        /// • 涵盖简体和繁体中文汉字
        /// • 不包括中文标点符号和特殊符号
        /// 
        /// 【使用场景】
        /// • 用户输入验证：检查是否包含中文
        /// • 国际化处理：区分中英文内容
        /// • 文本分析：统计中文字符占比
        /// • 字体选择：根据是否含中文选择合适字体
        /// • 编码检测：判断文本编码需求
        /// 
        /// 【示例】
        /// <code>
        /// "Hello世界"._ContainsChineseCharacter()    // 返回: true
        /// "Hello World"._ContainsChineseCharacter()  // 返回: false  
        /// "中文测试"._ContainsChineseCharacter()      // 返回: true
        /// "123"._ContainsChineseCharacter()         // 返回: false
        /// "hello，world"._ContainsChineseCharacter() // 返回: false (仅标点，无汉字)
        /// </code>
        /// </summary>
        /// <param name="input">待检测的字符串</param>
        /// <returns>如果包含中文汉字返回true，否则返回false</returns>
        private static readonly Regex ChineseCharacterRegex = new Regex(@"[\u4e00-\u9fa5]", RegexOptions.Compiled);
        public static bool _ContainsChineseCharacter(this string input)
        {
            return ChineseCharacterRegex.IsMatch(input); // 使用预编译版本
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
        /// 验证字符串是否为有效的C#标识符名称
        /// 
        /// 【验证规则】
        /// 1. 不能为空或null
        /// 2. 只能包含字母、数字、下划线字符
        /// 3. 不能以数字开头
        /// 4. 不能包含连续的下划线（2个或更多）
        /// 5. 不能是C#保留关键字
        /// 
        /// 【验证步骤】
        /// • 步骤1：检查是否包含非法字符（使用\W正则表达式）
        /// • 步骤2：检查是否以数字开头
        /// • 步骤3：检查是否存在连续下划线
        /// • 步骤4：检查是否为C#关键字
        /// 
        /// 【使用场景】
        /// • 代码生成器：验证生成的变量名、方法名
        /// • 配置系统：验证配置项名称的合法性
        /// • 动态编译：确保动态生成的标识符符合C#规范
        /// • 工具开发：IDE、编辑器的标识符验证
        /// 
        /// 【示例】
        /// <code>
        /// "validName"._IsValidIdentName()        // 返回: true
        /// "123invalid"._IsValidIdentName()       // 返回: false (以数字开头)
        /// "class"._IsValidIdentName()            // 返回: false (C#关键字)
        /// "valid_name"._IsValidIdentName()       // 返回: true
        /// "invalid__name"._IsValidIdentName()    // 返回: false (连续下划线)
        /// </code>
        /// </summary>
        /// <param name="input">待验证的字符串</param>
        /// <returns>如果是有效的C#标识符名称返回true，否则返回false</returns>
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
        // ==================================================================================
        // 字符串操作和处理方法
        // 
        // 【功能概述】
        // 提供字符串的各种操作和处理功能，包括清理、转换、格式化等
        // 
        // 【操作类型】
        // • 清理操作：移除多余空格、移除子字符串等
        // • 连接操作：字符串数组连接、前后缀添加等
        // • 转换操作：大小写转换、标识符转换等
        // • 提取操作：移除扩展名、提取特定部分等
        // 
        // 【设计特点】
        // • 支持链式调用，提高代码可读性
        // • 内置空值检查，避免空引用异常
        // • 提供文化参数支持，适应国际化需求
        // ==================================================================================
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
        /// 将字符串的首个非空白字符转为大写（智能首字母大写）
        /// 
        /// 【处理策略】
        /// • 自动跳过开头的空白字符（空格、制表符、换行符等）
        /// • 找到第一个有效字符后将其转为大写
        /// • 保持其他字符和空白格式不变
        /// • 如果字符串全为空白字符，返回原字符串
        /// 
        /// 【与_FirstUpper的区别】
        /// • _FirstUpper：只处理第一个字符，不跳过空白
        /// • _FirstCharToUpperCapitalize：智能跳过空白，处理首个有效字符
        /// 
        /// 【使用场景】
        /// • 文本格式化：处理可能有前导空格的用户输入
        /// • 标题处理：智能首字母大写，保持格式
        /// • 数据清洗：标准化带空白的文本数据
        /// • 模板系统：处理格式不规范的文本
        /// 
        /// 【示例】
        /// <code>
        /// "  hello world"._FirstCharToUpperCapitalize()  // 返回: "  Hello world"
        /// "\thello"._FirstCharToUpperCapitalize()        // 返回: "\tHello"
        /// "   "._FirstCharToUpperCapitalize()            // 返回: "   "
        /// "Hello"._FirstCharToUpperCapitalize()          // 返回: "Hello"
        /// </code>
        /// </summary>
        /// <param name="input">待处理的字符串</param>
        /// <returns>首个非空白字符大写后的字符串</returns>
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

        // 添加预编译的正则表达式（在类级别）
        private static readonly Regex NewlineRegex = new Regex(@"\r?\n", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(@"[ \t]+", RegexOptions.Compiled);

        /// <summary>
        /// 转化成代码格式（性能优化版本）
        /// 
        /// 【格式化规则】
        /// • 统一换行符为\n
        /// • 合并多余的空格和制表符
        /// • 根据大括号层级自动缩进（4个空格/层）
        /// • 添加ES标识注释
        /// 
        /// 【性能优化】
        /// • 预编译正则表达式，避免重复编译开销
        /// • 预分配StringBuilder容量，减少扩容次数
        /// • 缓存空格字符串，避免重复创建
        /// • 减少字符串拼接和临时对象创建
        /// 
        /// 【使用场景】
        /// • 代码生成工具：格式化生成的代码
        /// • 模板系统：标准化代码模板输出
        /// • 代码美化：自动格式化不规范的代码
        /// • 配置文件：格式化JSON、XML等结构化文本
        /// </summary>
        /// <param name="code">待格式化的代码字符串</param>
        /// <returns>格式化后的代码字符串，末尾带ES标识注释</returns>
        public static string _ToCodePro(this string code)
        {
            if (string.IsNullOrEmpty(code)) return code + "\n//ES已修正";

            int indentLevel = 0;

            // 性能优化1: 使用预编译的正则表达式
            string cleanedCode = NewlineRegex.Replace(code, "\n");
            cleanedCode = WhitespaceRegex.Replace(cleanedCode, " ");

            // 性能优化2: 预分配StringBuilder容量（估算为原长度的1.3倍）
            StringBuilder sb = new StringBuilder((int)(cleanedCode.Length * 1.3f));

            // 性能优化3: 预创建常用缩进字符串缓存（最大支持20层嵌套）
            string[] indentCache = new string[20];
            for (int i = 0; i < indentCache.Length; i++)
            {
                indentCache[i] = new string(' ', i * 4);
            }

            foreach (char c in cleanedCode)
            {
                if (c == '{')
                {
                    indentLevel++;
                }
                else if (c == '}')
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }

                sb.Append(c);

                if (c == '\n')
                {
                    // 性能优化4: 使用缓存的缩进字符串，避免重复创建
                    if (indentLevel < indentCache.Length)
                    {
                        sb.Append(indentCache[indentLevel]);
                    }
                    else
                    {
                        // 超出缓存范围时才创建新字符串
                        sb.Append(' ', indentLevel * 4);
                    }
                }
            }

            return sb.ToString() + "\n//ES已修正";
        }
        #endregion

        #region Builder专属
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
        /// 在Builder开头加入内容
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
        /// 将字符串转换为MD5哈希值
        /// 
        /// 【算法特性】
        /// • 使用MD5算法生成128位（16字节）哈希值
        /// • 输出格式：32位十六进制小写字符串
        /// • 相同输入始终产生相同输出
        /// • 不可逆，无法从哈希值还原原始字符串
        /// 
        /// 【安全说明】
        /// MD5已被证明存在碰撞漏洞，不建议用于安全敏感场景
        /// 推荐用途：数据完整性校验、非安全场景的唯一标识
        /// 
        /// 【编码处理】
        /// 使用UTF-8编码将字符串转换为字节数组进行哈希计算
        /// 确保中文等Unicode字符的正确处理
        /// 
        /// 【使用场景】
        /// • 文件完整性校验：比较文件是否被修改
        /// • 数据去重：生成唯一标识符
        /// • 缓存键值：为复杂对象生成简短的缓存键
        /// • 非敏感密码存储（不推荐，建议使用更安全的算法）
        /// 
        /// 【示例】
        /// <code>
        /// "Hello World"._ToMD5Hash()  // 返回: "b10a8db164e0754105b7a99be72e3fe5"
        /// ""._ToMD5Hash()             // 返回: "d41d8cd98f00b204e9800998ecf8427e"
        /// "中文测试"._ToMD5Hash()      // 返回: 对应的MD5哈希值
        /// </code>
        /// </summary>
        /// <param name="str">待哈希的字符串</param>
        /// <returns>32位十六进制小写的MD5哈希字符串</returns>
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