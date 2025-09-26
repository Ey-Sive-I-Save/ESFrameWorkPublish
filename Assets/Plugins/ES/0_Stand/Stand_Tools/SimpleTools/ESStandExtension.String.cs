using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal static class STANDExtensionForString
{
    public static string _KeepBeforeByFirst(this string source)
    {
        return "";
    }
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
    internal static string _KeepBeforeByLast(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
    internal static string _KeepAfterByFirst(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
    internal static string _KeepAfterByLast(this string source, string separator, bool includeSeparator = false, StringComparison comparison = StringComparison.Ordinal)
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
    internal static string _KeepBeforeCutFlag(this string source, string separator, StringComparison comparison = StringComparison.Ordinal)
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
    internal static string _KeepAfterCutFlag(this string source, string separator, StringComparison comparison = StringComparison.Ordinal)
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
    internal static string _KeepBetween(this string source, string startSeparator, string endSeparator, bool includeSeparators = false, StringComparison comparison = StringComparison.Ordinal)
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
    internal static string _KeepBeforeByFirstChar(this string source, char separator)
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
    internal static string _KeepBeforeByLastChar(this string source, char separator)
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
    internal static string _KeepAfterByFirstChar(this string source, char separator)
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
    internal static string _KeepAfterByLastChar(this string source, char separator)
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
    internal static string _STAND_KeepBeforeByMaxLength(this string str, int maxLength, string end = "...")
    {
        if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
            return str;

        return str.Substring(0, maxLength) + end;
    }
}
        #endregion
