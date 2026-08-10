using System;

namespace ES
{
    /// <summary>
    /// Deterministic script-origin categories for the Inspector script filter.
    /// Assembly names are diagnostic metadata only and must not override asset paths.
    /// </summary>
    public enum ESInspectorScriptOriginKind
    {
        Missing,
        ES,
        ThirdParty,
        Project,
        Unknown,
        UnityNative
    }

    public static class ESInspectorScriptOriginClassifier
    {
        private const string EsAssetRoot = "Assets/Plugins/ES/";
        private const string PackageRoot = "Packages/";
        private const string AssetsRoot = "Assets/";

        public static ESInspectorScriptOriginKind Classify(
            string assetPath,
            string assemblyName,
            bool isMissing)
        {
            if (isMissing)
                return ESInspectorScriptOriginKind.Missing;

            string normalizedPath = NormalizeAssetPath(assetPath);
            if (normalizedPath.StartsWith(EsAssetRoot, StringComparison.OrdinalIgnoreCase))
                return ESInspectorScriptOriginKind.ES;

            if (normalizedPath.StartsWith(PackageRoot, StringComparison.OrdinalIgnoreCase))
                return ESInspectorScriptOriginKind.ThirdParty;

            if (normalizedPath.StartsWith(AssetsRoot, StringComparison.OrdinalIgnoreCase))
                return ESInspectorScriptOriginKind.Project;

            return ESInspectorScriptOriginKind.Unknown;
        }

        public static string GetDisplayName(ESInspectorScriptOriginKind kind)
        {
            switch (kind)
            {
                case ESInspectorScriptOriginKind.ES:
                    return "ES";
                case ESInspectorScriptOriginKind.Project:
                    return "项目";
                case ESInspectorScriptOriginKind.ThirdParty:
                    return "包";
                case ESInspectorScriptOriginKind.Unknown:
                    return "未知";
                case ESInspectorScriptOriginKind.UnityNative:
                    return "原生";
                default:
                    return "丢失";
            }
        }

        public static string GetTooltip(
            ESInspectorScriptOriginKind kind,
            string assetPath,
            string assemblyName)
        {
            switch (kind)
            {
                case ESInspectorScriptOriginKind.ES:
                    return "ES 资产脚本\n" + FormatPath(assetPath, assemblyName);
                case ESInspectorScriptOriginKind.Project:
                    return "项目资产脚本\n" + FormatPath(assetPath, assemblyName);
                case ESInspectorScriptOriginKind.ThirdParty:
                    return "第三方包脚本\n" + FormatPath(assetPath, assemblyName);
                case ESInspectorScriptOriginKind.Unknown:
                    return "未知来源脚本\n" + FormatPath(assetPath, assemblyName);
                case ESInspectorScriptOriginKind.UnityNative:
                    return "Unity 原生组件\n" + FormatPath(assetPath, assemblyName);
                default:
                    return "组件引用已丢失";
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;

            return assetPath.Replace('\\', '/').TrimStart('/');
        }

        private static string FormatPath(string assetPath, string assemblyName)
        {
            string path = string.IsNullOrWhiteSpace(assetPath) ? "<unknown path>" : assetPath;
            if (string.IsNullOrWhiteSpace(assemblyName))
                return path;

            return path + "\n" + assemblyName;
        }
    }
}
