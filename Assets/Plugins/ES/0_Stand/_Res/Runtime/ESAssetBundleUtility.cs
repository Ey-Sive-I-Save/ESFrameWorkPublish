using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ES
{
    /// <summary>新版资源链的无状态平台和 AssetBundle 命名工具；不依赖旧 ESResMaster。</summary>
    public static class ESAssetBundleUtility
    {
        // Unity/Mono 在部分编辑器与旧播放器环境仍受 MAX_PATH 约束；物理 AB 名必须留出发布目录空间。
        public const int MaxAssetBundleKeyLength = 56;
        public const int MaxAssetBundleFileNameLength = 63;
        public const int MinLibraryCodeLength = 2;
        public const int MaxLibraryCodeLength = 12;
        private const int StableHashLength = 24;

        public static string ToReadableSlug(string value, int maxLength, string fallback = "asset")
        {
            string source = value ?? string.Empty;
            var transliterated = new StringBuilder(source.Length * 2);
            foreach (char c in source)
            {
                if (c >= '\u4e00' && c <= '\u9fa5')
                    transliterated.Append(NPinyin.Pinyin.GetPinyin(c, Encoding.UTF8));
                else
                    transliterated.Append(c);
            }

            string slug = ToSafeAssetBundleKey(transliterated.ToString());
            if (string.Equals(slug, "default_assetbundle", StringComparison.Ordinal)) slug = fallback;
            if (maxLength > 0 && slug.Length > maxLength) slug = slug.Substring(0, maxLength).Trim('_');
            return string.IsNullOrEmpty(slug) ? fallback : slug;
        }

        public static string StableHash(string value, int length)
        {
            if (length < 1 || length > 64) throw new ArgumentOutOfRangeException(nameof(length));
            using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                    .Replace("-", string.Empty).ToLowerInvariant();
                return hash.Substring(0, length);
            }
        }

        public static bool IsValidLibraryCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < MinLibraryCodeLength || value.Length > MaxLibraryCodeLength) return false;
            foreach (char c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
            return value[0] != '_' && value[value.Length - 1] != '_';
        }

        public static string NormalizeLibraryCode(string value)
        {
            string code = ToReadableSlug(value, MaxLibraryCodeLength, "lib").Trim('_');
            return code.Length < MinLibraryCodeLength ? "lib" : code;
        }

        public static string CreateAutomaticLibraryCode(string libraryName, string libraryAssetGuid)
        {
            string hint = ToReadableSlug(libraryName, 5, "lib");
            string result = hint + "_" + StableHash(libraryAssetGuid, 6);
            return result.Length <= MaxLibraryCodeLength ? result : result.Substring(0, MaxLibraryCodeLength).TrimEnd('_');
        }

        public static string GetTypeCode(string kind)
        {
            switch (kind ?? string.Empty)
            {
                case "Prefab": return "pfb";
                case "Material": return "mat";
                case "Texture":
                case "Texture2D": return "tex";
                case "Sprite": return "spr";
                case "AudioClip": return "aud";
                case "Scene": return "scn";
                case "ScriptableObject": return "so";
                case "AnimationClip": return "anim";
                case "Mesh": return "mesh";
                case "SpriteAtlas": return "atlas";
                default: return "asset";
            }
        }

        public static string CreateAssetBundleKey(string libraryCode, string libraryAssetGuid, string folderPath,
            string kind, string assetHint, string assetGuid, long localFileId)
        {
            string folderHint = ToReadableSlug(System.IO.Path.GetFileName(folderPath?.Replace('\\', '/')), 10, "root");
            string folderHash = StableHash((folderPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant(), 4);
            string identityHash = StableHash(libraryAssetGuid + "|asset|" + assetGuid + ":" + localFileId, 12);
            string key = NormalizeLibraryCode(libraryCode) + "_" + folderHint + "_" + folderHash + "_"
                + GetTypeCode(kind) + "_" + ToReadableSlug(assetHint, 12) + "_" + identityHash;
            return RequireValidAssetBundleKey(key);
        }

        public static string CreateGroupBundleKey(string libraryCode, string libraryAssetGuid, string groupPath,
            string namedOption, string folderAssetGuid)
        {
            string folderHint = ToReadableSlug(System.IO.Path.GetFileName(groupPath?.Replace('\\', '/')), 10, "root");
            string folderHash = StableHash((groupPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant(), 6);
            string groupHash = StableHash(libraryAssetGuid + "|group|" + namedOption + "|" + folderAssetGuid, 12);
            string key = NormalizeLibraryCode(libraryCode) + "_" + folderHint + "_" + folderHash + "_grp_" + groupHash;
            return RequireValidAssetBundleKey(key);
        }

        public static string CreateSpecialBundleKey(string scopeCode, string hint, string stableIdentity)
        {
            string key = NormalizeLibraryCode(scopeCode) + "_" + ToReadableSlug(hint, 12) + "_asset_"
                + StableHash(stableIdentity, 12);
            return RequireValidAssetBundleKey(key);
        }

        public static string RequireValidAssetBundleKey(string key)
        {
            string safe = ToSafeAssetBundleKey(key);
            if (!string.Equals(key, safe, StringComparison.Ordinal))
                throw new ArgumentException("AssetBundleKey 包含非法字符：" + key, nameof(key));
            if (key.Length > MaxAssetBundleKeyLength)
                throw new ArgumentException($"AssetBundleKey 超过 {MaxAssetBundleKeyLength} 字符：{key}", nameof(key));
            return key;
        }

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

        /// <summary>生成跨机器、跨进程稳定且有界的 AB 标识。保留可读前缀，超长部分使用 SHA-256 后缀。</summary>
        public static string ToBoundedAssetBundleKey(string value, int maxLength = MaxAssetBundleKeyLength, bool preserveSlash = false)
        {
            string safe = ToSafeAssetBundleKey(value, preserveSlash);
            if (maxLength < 24) maxLength = 24;
            if (safe.Length <= maxLength) return safe;

            string hash = StableHash(value, StableHashLength);
            int prefixLength = Math.Max(1, maxLength - hash.Length - 1);
            string prefix = safe.Substring(0, Math.Min(prefixLength, safe.Length)).Trim('_', '/');
            string result = prefix + "_" + hash;
            return result.Length <= maxLength ? result : result.Substring(0, maxLength);
        }

        /// <summary>物理文件名保留扩展名，避免 AssetBundles/foo_bundle 与 foo.bundle 不一致。</summary>
        public static string ToSafeAssetBundleFileName(string fileName)
        {
            string value = (fileName ?? string.Empty).Trim();
            string extension = System.IO.Path.GetExtension(value);
            string stem = string.IsNullOrEmpty(extension) ? value : value.Substring(0, value.Length - extension.Length);
            string safeStem = ToBoundedAssetBundleKey(stem, MaxAssetBundleKeyLength);
            string safeExtension = string.IsNullOrEmpty(extension) ? string.Empty : "." + extension.TrimStart('.').ToLowerInvariant();
            string result = safeStem + safeExtension;
            if (result.Length > MaxAssetBundleFileNameLength)
                throw new ArgumentException($"AssetBundle 文件名超过 {MaxAssetBundleFileNameLength} 字符：{result}", nameof(fileName));
            return result;
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
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(platform), platform,
                        "新版资源发布链尚未定义该平台的 BuildTarget/发布目录映射。");
            }
        }

        /// <summary>
        /// 新发布链的唯一平台来源。
        /// 编辑器内可按全局设置模拟目标平台；Player 永远信任自身实际平台，禁止被遗留配置误导。
        /// </summary>
        public static RuntimePlatform GetRuntimeResourcePlatform(RuntimePlatform configuredEditorPlatform)
        {
#if UNITY_EDITOR
            return configuredEditorPlatform;
#else
            return Application.platform;
#endif
        }

        public static string GetRuntimeResourcePlatformName(RuntimePlatform configuredEditorPlatform)
        {
            return GetBuildPlatformName(GetRuntimeResourcePlatform(configuredEditorPlatform));
        }
    }
}
