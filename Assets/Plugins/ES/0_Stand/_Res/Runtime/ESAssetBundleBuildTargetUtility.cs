#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>新版资源管线的编辑器平台映射，独立于旧资源主控。</summary>
    public static class ESAssetBundleBuildTargetUtility
    {
        public static BuildTarget GetBuildTarget(RuntimePlatform? runtimePlatform = null)
        {
            RuntimePlatform sourcePlatform = runtimePlatform ?? Application.platform;
            string canonicalPlatformName = ESAssetBundleUtility.GetBuildPlatformName(sourcePlatform);
            if (Enum.TryParse(canonicalPlatformName, out BuildTarget target)) return target;
            throw new ArgumentOutOfRangeException(nameof(runtimePlatform), sourcePlatform,
                "无法将统一资源平台名转换为 Unity BuildTarget：" + canonicalPlatformName);
        }
    }
}
#endif
