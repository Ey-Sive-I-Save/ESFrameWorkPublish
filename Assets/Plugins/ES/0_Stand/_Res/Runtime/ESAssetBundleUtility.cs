using System.Text;
using UnityEngine;

namespace ES
{
    /// <summary>新版资源链的无状态平台和 AssetBundle 命名工具；不依赖旧 ESResMaster。</summary>
    public static class ESAssetBundleUtility
    {
        public static string ToSafeAssetBundleKey(string value, bool preserveSlash = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return "default_assetbundle";

            var builder = new StringBuilder(value.Length);
            foreach (char raw in value.ToLowerInvariant())
            {
                if ((raw >= 'a' && raw <= 'z') || (raw >= '0' && raw <= '9') || raw == '_') builder.Append(raw);
                else if (raw == '/' && preserveSlash) builder.Append(raw);
                else builder.Append('_');
            }

            string result = builder.ToString();
            while (result.IndexOf("__", System.StringComparison.Ordinal) >= 0) result = result.Replace("__", "_");
            while (preserveSlash && result.IndexOf("//", System.StringComparison.Ordinal) >= 0) result = result.Replace("//", "/");
            result = result.Trim('_', '/');
            return string.IsNullOrEmpty(result) ? "default_assetbundle" : result;
        }

        public static string GetPlatformFolderName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor: return "WindowsPlayer";
                case RuntimePlatform.Android: return "Android";
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS: return "iOS";
                case RuntimePlatform.WebGLPlayer: return "WebGL";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor: return "Linux";
                case RuntimePlatform.PS5: return "PS5";
                case RuntimePlatform.XboxOne: return "Xbox";
                default: return platform.ToString();
            }
        }

        // This matches UnityEditor.BuildTarget.ToString() used by the build pipeline,
        // while remaining available in player builds where UnityEditor is absent.
        public static string GetBuildPlatformName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor: return "StandaloneWindows64";
                case RuntimePlatform.Android: return "Android";
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS: return "iOS";
                case RuntimePlatform.WebGLPlayer: return "WebGL";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor: return "StandaloneLinux64";
                case RuntimePlatform.PS5: return "PS5";
                case RuntimePlatform.XboxOne: return "XboxOne";
                default: return "StandaloneWindows64";
            }
        }
    }
}
