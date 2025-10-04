using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using NPinyin;
namespace ES
{
    /// <summary>
    /// ESRes 总工具
    /// </summary>
    public partial class ESResMaster 
    {
        #region ABHash获取，包名和其他处理流程
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public static string PathAndNameTool_GetHash(string input)
        {
            int index = input.LastIndexOf("_");
            if (index >= 0) return input.Substring(index + 1, input.Length - index - 1);
            return input;
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public static string PathAndNameTool_GetPreName(string input)
        {
            int index = input.LastIndexOf("_");
            if (index >= 0) return input.Substring(0, index);
            return input;
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public string PathAndName_GetABAllNameFromPreName(string input)
        {
            /* if( ESAssetBundlePath.AssetBundleHashes.TryGetValue(input,out var withHash))
             {
                 return withHash;
             }*/
            return input;
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public string PathAndName_GetPreNameFromCompleteNameBySymbol(string input, char sym)
        {
            int index = input.LastIndexOf(sym);
            if (index >= 0) return input.Substring(0, index);
            return input;
        }
        [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
        public string PathAndName_GetPostNameFromCompleteNameBySymbol(string input, char sym)
        {
            int index = input.LastIndexOf(sym);
            if (index >= 0) return input.Substring(index + 1, input.Length - index - 1);
            return input;
        }

        /// <summary>
        /// 将任意字符串转换为安全的AB包名
        /// </summary>
        /// <param name="input">原始字符串（可为任意内容）</param>
        /// <param name="useSlashSeparator">是否使用斜杠（/）作为层级分隔符（默认为下划线_）</param>
        /// <returns>安全的AB包名（符合Unity规范）</returns>
        public static string ToSafeABName(string input, bool useSlashSeparator = false)
        {
            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("输入字符串为空，返回默认AB包名：Default_AB");
                return "Default_AB";
            }

            // 1. 转换为小写（可选，根据项目规范调整）
            string normalized = input.ToLowerInvariant();

            // 2. 替换非法字符：仅保留字母、数字、下划线、斜杠（根据useSlashSeparator调整）
            StringBuilder safeName = new StringBuilder();
            foreach (char c in normalized)
            {
                if ((c >= '\u4e00' && c <= '\u9fa5')) {
                    //中文
                    string py= NPinyin.Pinyin.GetPinyin(c, Encoding.UTF8);
                    safeName.Append(py);
                }
                else if ((char.IsLetterOrDigit(c) || c == '_'))
                {
                    safeName.Append(c);
                }
                else if (c == '/' && useSlashSeparator)
                {
                    safeName.Append('/');
                }
                else
                {
                    safeName.Append('_'); // 其他字符替换为下划线
                }
            }

            // 3. 处理连续分隔符（如多个_或/合并为一个）
            string result = safeName.ToString();
            result = result.Replace("__", "_").Replace("//", "/");
            if (result.StartsWith("_")) result = result.Substring(1); // 去除开头下划线
            if (result.EndsWith("_")) result = result.Substring(0, result.Length - 1); // 去除结尾下划线

            // 4. 检查是否为空（极端情况：全非法字符）
            if (string.IsNullOrEmpty(result))
            {
                Debug.LogWarning($"转换后AB包名为空，输入：{input}，返回默认名称：Default_AB");
                return "Default_AB";
            }

            return result;
        }
        #endregion


        public string GetDownloadNetPathWithPlatform()
        {
           return Settings.Path_Net + "/" + RunTimePlatformFolderName(Application.platform);
        }

        public string GetDownloadLocalPath()
        {
            return Application.persistentDataPath + "/" +Settings.Path_Sub_DownloadRelative;
        }

        #region 平台
        /// <summary>
        /// 将RuntimePlatform映射为对应的文件夹名称字符串
        /// </summary>
        /// <param name="platfoem">BuildTarget枚举值</param>
        /// <returns>对应的文件夹名称字符串</returns>
        /// 

        public static string RunTimePlatformFolderName(RuntimePlatform platfoem)
        {
            switch (platfoem)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "WindowsPlayer";
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "Linux";
                case RuntimePlatform.PS5:
                    return "PS5";
                case RuntimePlatform.XboxOne:
                    return "Xbox";
                default:
                    // 未知平台使用枚举名称的小写形式（如"unknown"）
                    return platfoem.ToString().ToLower();
            }
        }
        public static string GetPlatformName()
        {
            return RunTimePlatformFolderName(Application.platform);
        }
        #endregion
    }
}

