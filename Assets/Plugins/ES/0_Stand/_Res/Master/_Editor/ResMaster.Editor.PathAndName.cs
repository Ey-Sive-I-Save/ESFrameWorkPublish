using ES;
using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;


namespace ES
{
    /// <summary>
    /// ESResMaster 的路径和名称处理部分,专为Editor环境设计。
    /// </summary>
    public partial class ESResMaster
    {
        #if UNITY_EDITOR
        #region 平台映射
        public static BuildTarget GetValidBuildTargetByRuntimePlatform(UnityEngine.RuntimePlatform? RuntimePlatform = null)
        {
            var tryUse = RuntimePlatform ?? Application.platform;
            switch (tryUse)
            {
                case UnityEngine.RuntimePlatform.WindowsPlayer:
                case UnityEngine.RuntimePlatform.WindowsEditor:
                    return BuildTarget.StandaloneWindows64;
                case UnityEngine.RuntimePlatform.Android:
                    return BuildTarget.Android;
                case UnityEngine.RuntimePlatform.IPhonePlayer:
                case UnityEngine.RuntimePlatform.tvOS:
                    return BuildTarget.iOS;
                case UnityEngine.RuntimePlatform.WebGLPlayer:
                    return BuildTarget.WebGL;
                case UnityEngine.RuntimePlatform.LinuxPlayer:
                case UnityEngine.RuntimePlatform.LinuxEditor:
                    return BuildTarget.StandaloneLinux64;
                case UnityEngine.RuntimePlatform.PS5:
                    return BuildTarget.PS5;
                case UnityEngine.RuntimePlatform.XboxOne:
                    return BuildTarget.XboxOne;
                default:
                    return BuildTarget.StandaloneWindows64; // 默认
            }
        }

        /// <summary>
        /// 将BuildTarget映射为对应的文件夹名称字符串
        /// </summary>
        /// <param name="target">BuildTarget枚举值</param>
        /// <returns>对应的文件夹名称字符串</returns>
        public static string GetFolderNameByBuildTarget(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return PlatformFolderNames.WindowsPlayer;
                case BuildTarget.Android:
                    return PlatformFolderNames.Android;
                case BuildTarget.iOS:
                case BuildTarget.tvOS:
                    return PlatformFolderNames.iOS;
                case BuildTarget.WebGL:
                    return PlatformFolderNames.WebGL;
                case BuildTarget.StandaloneOSX:
                    return "macOS";
                case BuildTarget.StandaloneLinux64:
                    return PlatformFolderNames.Linux;
                case BuildTarget.WSAPlayer: // UWP
                    return "UWP";
                case BuildTarget.PS5:
                    return PlatformFolderNames.PS5;
                case BuildTarget.XboxOne:
                    return PlatformFolderNames.Xbox;
                default:
                    // 未知平台使用枚举名称的小写形式（如"unknown"）
                    return target.ToString().ToLower();
            }
        }
        #endregion

        #endif
    }
}

