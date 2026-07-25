#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>新版资源管线的编辑器平台映射，独立于旧资源主控。</summary>
    public static class ESAssetBundleBuildTargetUtility
    {
        public static BuildTarget GetBuildTarget(RuntimePlatform? runtimePlatform = null)
        {
            switch (runtimePlatform ?? Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor: return BuildTarget.StandaloneWindows64;
                case RuntimePlatform.Android: return BuildTarget.Android;
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS: return BuildTarget.iOS;
                case RuntimePlatform.WebGLPlayer: return BuildTarget.WebGL;
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor: return BuildTarget.StandaloneLinux64;
                case RuntimePlatform.PS5: return BuildTarget.PS5;
                case RuntimePlatform.XboxOne: return BuildTarget.XboxOne;
                default: return BuildTarget.StandaloneWindows64;
            }
        }
    }
}
#endif
