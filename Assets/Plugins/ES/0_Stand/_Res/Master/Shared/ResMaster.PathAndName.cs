using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ESResMaster 的常规路径和名称处理部分，还有一部分为Editor的另见Editor部分的PathAndName。
    /// 提供资源路径构建、AB包名处理、平台映射等工具方法。
    /// 用于确保跨平台兼容性和资源命名规范。
    /// </summary>
    public partial class ESResMaster
    {
        #region 常量定义

        /// <summary>
        /// JSON数据文件名常量类。
        /// 集中定义ESRes相关的JSON文件和文件夹名称，便于管理和维护。
        /// </summary>
        public static class JsonDataFileName
        {
            /// <summary>
            /// ESRes JSON数据父文件夹名称。
            /// 此文件夹专门存放ESRes相关的JSON配置文件，每个Lib名下单独拥有一个子文件夹。
            /// </summary>
            public const string PathParentFolder_ESResJsonData = "ESResData";

            /// <summary>
            /// AB包哈希JSON文件名。
            /// 存储AB包名到哈希值的映射（ABName -> Hash）。
            /// </summary>
            public const string PathJsonFileName_ESABHashs = "ABHashes.json";

            /// <summary>
            /// AB包依赖关系JSON文件名。
            /// 存储AB包之间的依赖关系（ABName -> 依赖的AB包数组）。
            /// </summary>
            public const string PathJsonFileName_ESDependences = "ABDependences.json";

            /// <summary>
            /// 资源键JSON文件名。
            /// 存储所有资源的键信息。
            /// </summary>
            public const string PathFileName_ESAssetkeys = "AssetKeys.json";

            /// <summary>
            /// AB包键JSON文件名。
            /// 存储AB包的键信息。
            /// </summary>
            public const string PathFileName_ESABkeys = "ABKeys.json";

            /// <summary>
            /// AB包元数据JSON文件名。
            /// 存储AB包的哈希、依赖和键的合并信息。
            /// </summary>
            public const string PathJsonFileName_ESABMetadata = "ESABMetadata.json";

        }
        /// <summary>
        /// 平台文件夹名称常量类。
        /// 定义各平台对应的输出文件夹名称，用于资源路径构建和AB包输出。
        /// 集中管理避免硬编码，提高维护性。
        /// </summary>
        public static class PlatformFolderNames
        {
            /// <summary>
            /// Windows平台文件夹名称。
            /// 对应 RuntimePlatform.WindowsPlayer 和 WindowsEditor。
            /// </summary>
            public const string WindowsPlayer = "WindowsPlayer";

            /// <summary>
            /// Android平台文件夹名称。
            /// 对应 RuntimePlatform.Android。
            /// </summary>
            public const string Android = "Android";

            /// <summary>
            /// iOS平台文件夹名称。
            /// 对应 RuntimePlatform.IPhonePlayer 和 tvOS。
            /// </summary>
            public const string iOS = "iOS";

            /// <summary>
            /// WebGL平台文件夹名称。
            /// 对应 RuntimePlatform.WebGLPlayer。
            /// </summary>
            public const string WebGL = "WebGL";

            /// <summary>
            /// Linux平台文件夹名称。
            /// 对应 RuntimePlatform.LinuxPlayer 和 LinuxEditor。
            /// </summary>
            public const string Linux = "Linux";

            /// <summary>
            /// PS5平台文件夹名称。
            /// 对应 RuntimePlatform.PS5。
            /// </summary>
            public const string PS5 = "PS5";

            /// <summary>
            /// Xbox平台文件夹名称。
            /// 对应 RuntimePlatform.XboxOne。
            /// </summary>
            public const string Xbox = "Xbox";
        }
        #endregion

        #region AB包哈希和名称处理
        /// <summary>
        /// 从带哈希的AB包名中提取哈希部分。
        /// 假设格式为 "包名_哈希"，返回哈希字符串。
        /// </summary>
        /// <param name="input">带哈希的完整AB包名。</param>
        /// <returns>哈希字符串；如果无下划线，返回原字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string PathAndNameTool_GetHash(string input)
        {
            int index = input.LastIndexOf("_");
            if (index >= 0) return input.Substring(index + 1, input.Length - index - 1);
            return input;
        }

        /// <summary>
        /// 从带哈希的AB包名中提取前缀包名。
        /// 假设格式为 "包名_哈希"，返回包名。
        /// </summary>
        /// <param name="input">带哈希的完整AB包名。</param>
        /// <returns>包名字符串；如果无下划线，返回原字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string PathAndNameTool_GetPreName(string input)
        {
            int index = input.LastIndexOf("_");
            if (index >= 0) return input.Substring(0, index);
            return input;
        }

        /// <summary>
        /// 根据前缀包名获取完整的AB包名（含哈希）。
        /// 当前实现直接返回输入，未来可扩展为从哈希字典查询。
        /// </summary>
        /// <param name="input">前缀包名。</param>
        /// <returns>完整的AB包名。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string PathAndName_GetABAllNameFromPreName(string input)
        {
            // 注释：未来可从 ESAssetBundlePath.AssetBundleHashes 字典查询带哈希版本
            // if (ESAssetBundlePath.AssetBundleHashes.TryGetValue(input, out var withHash)) return withHash;
            return input;
        }

        /// <summary>
        /// 根据指定符号分割字符串，获取前缀部分。
        /// </summary>
        /// <param name="input">输入字符串。</param>
        /// <param name="sym">分隔符号。</param>
        /// <returns>前缀字符串；如果无符号，返回原字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string PathAndName_GetPreNameFromCompleteNameBySymbol(string input, char sym)
        {
            int index = input.LastIndexOf(sym);
            if (index >= 0) return input.Substring(0, index);
            return input;
        }

        /// <summary>
        /// 根据指定符号分割字符串，获取后缀部分。
        /// </summary>
        /// <param name="input">输入字符串。</param>
        /// <param name="sym">分隔符号。</param>
        /// <returns>后缀字符串；如果无符号，返回原字符串。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string PathAndName_GetPostNameFromCompleteNameBySymbol(string input, char sym)
        {
            int index = input.LastIndexOf(sym);
            if (index >= 0) return input.Substring(index + 1, input.Length - index - 1);
            return input;
        }

        /// <summary>
        /// 将任意字符串转换为安全的AB包名。
        /// 处理非法字符、中文转换、连续分隔符等，确保符合Unity AB包命名规范。
        /// </summary>
        /// <param name="input">原始字符串（可为任意内容）。</param>
        /// <param name="useSlashSeparator">是否使用斜杠（/）作为层级分隔符（默认为下划线_）。</param>
        /// <returns>安全的AB包名；如果转换后为空，返回默认名称。</returns>
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
                if ((c >= '\u4e00' && c <= '\u9fa5'))
                {
                    // 中文字符转换为拼音
                    string py = NPinyin.Pinyin.GetPinyin(c, Encoding.UTF8);
                    safeName.Append(py);
                }
                else if (char.IsLetterOrDigit(c) || c == '_')
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
        /// <summary>
        /// 将任意字符串转换为安全的文件夹路径名。
        /// 以最小修改确保路径安全：只替换路径中的非法字符，其他字符尽量保留。
        /// 处理中文转换、连续分隔符等。
        /// </summary>
        /// <param name="input">原始路径字符串。</param>
        /// <param name="toLower">是否转换为小写（默认为false，保留原大小写）。</param>
        /// <returns>安全的路径名；如果转换后为空，返回默认名称。</returns>
        public static string GetSafeFolderName(string input, bool toLower = false)
        {
            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("输入路径为空，返回默认路径：Default_Path");
                return "Default_Path";
            }

            // 1. 可选转换为小写
            string normalized = toLower ? input.ToLowerInvariant() : input;

            // 2. 获取路径非法字符集合
            char[] invalidChars = System.IO.Path.GetInvalidPathChars();

            // 3. 替换非法字符：只替换路径非法字符为下划线，其他保留
            StringBuilder safeName = new StringBuilder();
            foreach (char c in normalized)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    safeName.Append('_'); // 非法字符替换为下划线
                }
                else if ((c >= '\u4e00' && c <= '\u9fa5'))
                {
                    // 中文字符转换为拼音（可选，如果不需要可移除）
                    string py = NPinyin.Pinyin.GetPinyin(c, Encoding.UTF8);
                    safeName.Append(py);
                }
                else
                {
                    safeName.Append(c); // 保留其他字符
                }
            }

            // 4. 处理连续分隔符（可选，合并多个_或/）
            string result = safeName.ToString();
            result = result.Replace("__", "_").Replace("//", "/");

            // 注意：不去除开头/结尾的_或/，因为路径可能以它们开头或结尾

            // 5. 检查是否为空
            if (string.IsNullOrEmpty(result))
            {
                Debug.LogWarning($"转换后路径名为空，输入：{input}，返回默认名称：Default_Path");
                return "Default_Path";
            }

            return result;
        }

        #endregion
        #region 下载路径
        /// <summary>
        /// 获取带平台的网络下载路径。
        /// 格式：网络基础路径 + "/" + 平台文件夹名。
        /// </summary>
        /// <returns>完整的网络下载路径。</returns>
        public string GetDownloadNetPathWithPlatform()
        {
            return Settings.Path_Net + "/" + GetParentFolderNameByRuntimePlatform(Application.platform);
        }

        /// <summary>
        /// 获取本地下载路径。
        /// 基于Application.persistentDataPath + 子路径。
        /// </summary>
        /// <returns>本地下载路径。</returns>
        public string GetDownloadLocalPath()
        {
            return Application.persistentDataPath + "/" + Settings.Path_Sub_DownloadRelative;
        }
        #endregion

        #region 平台相关(构建时见Editor的PathAndName)

        /// <summary>
        /// 将RuntimePlatform枚举映射为对应的文件夹名称字符串。
        /// 用于构建平台特定的资源路径。
        /// </summary>
        /// <param name="platform">RuntimePlatform枚举值。</param>
        /// <returns>对应的文件夹名称字符串。</returns>
        public static string GetParentFolderNameByRuntimePlatform(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return PlatformFolderNames.WindowsPlayer;
                case RuntimePlatform.Android:
                    return PlatformFolderNames.Android;
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS:
                    return PlatformFolderNames.iOS;
                case RuntimePlatform.WebGLPlayer:
                    return PlatformFolderNames.WebGL;
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return PlatformFolderNames.Linux;
                case RuntimePlatform.PS5:
                    return PlatformFolderNames.PS5;
                case RuntimePlatform.XboxOne:
                    return PlatformFolderNames.Xbox;
                default:
                    return platform.ToString().ToLower();
            }
        }


        /// <summary>
        /// 获取当前运行平台的文件夹名称。
        /// </summary>
        /// <returns>平台文件夹名。</returns>
        public static string GetRuntimePlatformName()
        {
            return GetParentFolderNameByRuntimePlatform(Application.platform);
        }


        #endregion
        

        #region  验证器
        public static bool TrySetResLibFolderName(ResLibrary resLibrary, string preLibFolderName, int attemptCount = 0)
        {
            const int maxAttempts = 10; // 防止无限递归
            if (attemptCount >= maxAttempts)
            {
                string errorMessage = $"无法为库 '{resLibrary.Name}' 生成唯一的文件夹名，已达到最大尝试次数。";
                Debug.LogError(errorMessage);
#if UNITY_EDITOR
                EditorUtility.DisplayDialog("错误", errorMessage, "确定");
#endif
                return false;
            }

            var allLibraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            var validName = GetSafeFolderName(preLibFolderName._ToValidIdentName());

            foreach (var lib in allLibraries)
            {
                if (lib != resLibrary && lib.LibFolderName == validName)
                {
                    // 有重复，加一个"_r"再次判定
                    return TrySetResLibFolderName(resLibrary, validName + "_r", attemptCount + 1);
                }
            }
            resLibrary.LibFolderName = validName;
#if UNITY_EDITOR
            EditorUtility.SetDirty(resLibrary);
#endif
            return true;
        }
        #endregion
    }
}

