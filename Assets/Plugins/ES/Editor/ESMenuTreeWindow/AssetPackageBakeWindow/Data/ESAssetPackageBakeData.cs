using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public enum ESAssetPackageAnalysisState
    {
        [InspectorName("未分析")] NotAnalyzed,
        [InspectorName("已分析")] Analyzed,
        [InspectorName("待人工确认")] NeedsReview,
        [InspectorName("已接受")] Accepted,
        [InspectorName("已验证")] Verified,
        [InspectorName("已过期")] Stale,
        [InspectorName("禁止使用")] Rejected
    }

    public enum ESAssetPackageExportLinkState
    {
        Unknown,
        LegacyLink,
        Valid,
        SourceChanged,
        TargetMissing,
        TargetReplaced,
        ConfigChanged,
        Conflict
    }

    public enum ESAssetPackageExportAttemptState
    {
        Idle,
        Resolving,
        NeedsReview,
        Blocked,
        AwaitingConfirmation,
        Stale,
        Staging,
        Verifying,
        Committing,
        Committed,
        CommittedWithWarning,
        Cancelled,
        Failed,
        RollbackPartial
    }

    public enum ESAssetPackageRollbackState
    {
        NotRequired,
        Complete,
        Partial
    }

    public enum ESAssetPackageExportOperation
    {
        Create,
        Update
    }

    public enum ESAssetPackageExportReasonCode
    {
        NewSource,
        SourceChanged,
        DependencyChanged,
        TargetMissing,
        ConfigChanged
    }

    [Flags]
    public enum ESAssetPackagePreviewCapability
    {
        None = 0,
        Static = 1 << 0,
        Scene = 1 << 1,
        Animation = 1 << 2,
        DynamicEffect = 1 << 3,
        Material = 1 << 4,
        Audio = 1 << 5,
        Shader = 1 << 6,
        Video = 1 << 7,
        Detail = 1 << 8
    }

    public readonly struct ESAssetPackageCategoryDescriptor
    {
        public readonly string stableKey;
        public readonly string displayName;
        public readonly string defaultExportFolder;
        public readonly string iconName;
        public readonly ESAssetPackagePreviewCapability previewCapabilities;

        public ESAssetPackageCategoryDescriptor(string stableKey, string displayName, string defaultExportFolder, string iconName, ESAssetPackagePreviewCapability previewCapabilities)
        {
            this.stableKey = stableKey;
            this.displayName = displayName;
            this.defaultExportFolder = defaultExportFolder;
            this.iconName = iconName;
            this.previewCapabilities = previewCapabilities;
        }
    }

    public static class ESAssetPackageCategoryCatalog
    {
        public static ESAssetPackageCategoryDescriptor Get(ESAssetPackageCategory category)
        {
            switch (category)
            {
                case ESAssetPackageCategory.Prefab: return new ESAssetPackageCategoryDescriptor("prefab", "预制体", "Prefabs", "d_Prefab Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.DynamicEffect | ESAssetPackagePreviewCapability.Animation | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Scene: return new ESAssetPackageCategoryDescriptor("scene", "场景", "Scenes", "d_SceneAsset Icon", ESAssetPackagePreviewCapability.Scene | ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Material: return new ESAssetPackageCategoryDescriptor("material", "材质", "Materials", "d_Material Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Material | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Texture: return new ESAssetPackageCategoryDescriptor("texture", "贴图", "Textures", "d_Texture Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Model: return new ESAssetPackageCategoryDescriptor("model", "模型", "Models", "d_Mesh Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Animation | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Audio: return new ESAssetPackageCategoryDescriptor("audio", "音频", "Audio", "d_AudioClip Icon", ESAssetPackagePreviewCapability.Audio | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Animation: return new ESAssetPackageCategoryDescriptor("animation", "动画", "Animations", "d_AnimationClip Icon", ESAssetPackagePreviewCapability.Animation | ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.ScriptableObject: return new ESAssetPackageCategoryDescriptor("scriptable_object", "SO资产", "ScriptableObjects", "d_ScriptableObject Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Shader: return new ESAssetPackageCategoryDescriptor("shader", "Shader", "Shaders", "d_Shader Icon", ESAssetPackagePreviewCapability.Shader | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Font: return new ESAssetPackageCategoryDescriptor("font", "字体", "Fonts", "d_Font Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Detail);
                case ESAssetPackageCategory.Video: return new ESAssetPackageCategoryDescriptor("video", "视频", "Videos", "d_VideoClip Icon", ESAssetPackagePreviewCapability.Video | ESAssetPackagePreviewCapability.Detail);
                default: return new ESAssetPackageCategoryDescriptor("other", "其他", "Others", "d_DefaultAsset Icon", ESAssetPackagePreviewCapability.Static | ESAssetPackagePreviewCapability.Detail);
            }
        }

        public static ESAssetPackagePreviewCapability GetPreviewCapabilities(ESAssetPackageCategory category)
        {
            return Get(category).previewCapabilities;
        }
    }

    [Serializable]
    public sealed class ESAssetPackageResolutionItem
    {
        [LabelText("源 GUID"), ReadOnly] public string sourceGuid;
        [LabelText("源路径"), ReadOnly] public string sourcePath;
        [LabelText("源依赖 Hash"), ReadOnly] public string sourceDependencyHash;
        [LabelText("源文件 Hash"), ReadOnly] public string sourceFileHash;
        [LabelText("目标路径"), ReadOnly] public string targetPath;
        [LabelText("预期目标 GUID"), ReadOnly] public string expectedTargetGuid;
        [LabelText("预期目标 Hash"), ReadOnly] public string expectedTargetFileHash;
        [LabelText("分类"), ReadOnly] public ESAssetPackageCategory category;
        [LabelText("操作"), ReadOnly] public ESAssetPackageExportOperation operation;
        [LabelText("原因"), ReadOnly] public ESAssetPackageExportReasonCode reasonCode;
        [LabelText("直接选择"), ReadOnly] public bool rootSelected;
        [LabelText("依赖项"), ReadOnly] public bool dependency;
    }

    [Serializable]
    public sealed class ESAssetPackageResolutionSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        [LabelText("Schema 版本"), ReadOnly] public int schemaVersion = CurrentSchemaVersion;
        [LabelText("Package ID"), ReadOnly] public string packageId;
        [LabelText("配置指纹"), ReadOnly] public string definitionHash;
        [LabelText("创建时间 UTC"), ReadOnly] public string createdUtc;
        [LabelText("快照 Hash"), ReadOnly] public string snapshotHash;
        [LabelText("解析项"), ReadOnly, ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
        public List<ESAssetPackageResolutionItem> items = new List<ESAssetPackageResolutionItem>();

        public void Seal()
        {
            schemaVersion = CurrentSchemaVersion;
            snapshotHash = ComputeHash();
        }

        public bool HasValidIntegrity()
        {
            return schemaVersion == CurrentSchemaVersion &&
                   !string.IsNullOrWhiteSpace(snapshotHash) &&
                   string.Equals(snapshotHash, ComputeHash(), StringComparison.OrdinalIgnoreCase);
        }

        public string ComputeHash()
        {
            var builder = new StringBuilder();
            AppendCanonical(builder, schemaVersion.ToString());
            AppendCanonical(builder, packageId);
            AppendCanonical(builder, definitionHash);
            AppendCanonical(builder, createdUtc);
            foreach (ESAssetPackageResolutionItem item in items ?? new List<ESAssetPackageResolutionItem>())
            {
                if (item == null)
                {
                    AppendCanonical(builder, "<null>");
                    continue;
                }

                AppendCanonical(builder, item.sourceGuid);
                AppendCanonical(builder, item.sourcePath);
                AppendCanonical(builder, item.sourceDependencyHash);
                AppendCanonical(builder, item.sourceFileHash);
                AppendCanonical(builder, item.targetPath);
                AppendCanonical(builder, item.expectedTargetGuid);
                AppendCanonical(builder, item.expectedTargetFileHash);
                AppendCanonical(builder, item.category.ToString());
                AppendCanonical(builder, item.operation.ToString());
                AppendCanonical(builder, item.reasonCode.ToString());
                AppendCanonical(builder, item.rootSelected.ToString());
                AppendCanonical(builder, item.dependency.ToString());
            }

            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", string.Empty);
        }

        private static void AppendCanonical(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }

    internal static class ESAssetPackagePathSafety
    {
        public static bool TryNormalizeProjectAssetPath(string rawPath, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(rawPath))
                return false;

            string path = rawPath.Trim().Replace('\\', '/');
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("~", StringComparison.Ordinal))
                return false;

            string[] parts = path.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.OrdinalIgnoreCase))
                return false;

            var stack = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0 || part == ".")
                    continue;
                if (part == "..")
                {
                    if (stack.Count <= 1)
                        return false;
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                if (part.IndexOf(':') >= 0 || part.IndexOf('\0') >= 0)
                    return false;
                stack.Add(part);
            }

            if (stack.Count == 0 || !string.Equals(stack[0], "Assets", StringComparison.OrdinalIgnoreCase))
                return false;

            stack[0] = "Assets";
            normalized = string.Join("/", stack);
            return true;
        }

        public static bool IsForbiddenExportFolder(string path)
        {
            if (!TryNormalizeProjectAssetPath(path, out string normalized))
                return true;

            return IsInside(normalized, "Assets/Resources")
                || IsInside(normalized, "Assets/Editor Default Resources")
                || IsInside(normalized, "Assets/Editor")
                || normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase)
                || IsInside(normalized, "Assets/.Recovery")
                || IsInside(normalized, "Assets/.ESBakeTransactions")
                || normalized.IndexOf("/.Recovery/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("/.ESBakeTransactions/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsInside(string path, string root)
        {
            return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(root)
                && (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsAllowedExportOverlap(string sourceRoot, string exportRoot, string categoryFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || string.IsNullOrWhiteSpace(exportRoot) || string.IsNullOrWhiteSpace(categoryFolder))
                return false;

            string source = sourceRoot.Replace('\\', '/').TrimEnd('/');
            return string.Equals(source, "Assets", StringComparison.OrdinalIgnoreCase)
                && IsInside(categoryFolder, exportRoot);
        }

        public static bool HasReparsePointInPath(string assetPath)
        {
            if (!TryNormalizeProjectAssetPath(assetPath, out string normalized)
                || string.IsNullOrEmpty(Application.dataPath))
                return true;

            string relative = normalized.Length > "Assets".Length
                ? normalized.Substring("Assets".Length).TrimStart('/')
                : string.Empty;
            string current = Application.dataPath;
            if (HasReparsePoint(current))
                return true;

            foreach (string part in relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if (Directory.Exists(current) && HasReparsePoint(current))
                    return true;
            }
            return false;
        }

        private static bool HasReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }
    }

    [Serializable]
    public sealed class ESAssetPackageExportPolicy
    {
        [LabelText("要求最新分析")] public bool requireCurrentAnalysis = false;
        [LabelText("允许待人工确认")] public bool allowNeedsReview = false;
        [LabelText("允许自定义脚本")] public bool allowCustomScripts = false;
        [LabelText("允许循环特效")] public bool allowLoopingEffects = true;
        [LabelText("允许材质风险")] public bool allowMaterialRisk = false;
        [LabelText("风险阻断阈值")] public int blockingRiskCount = 1;
    }

    [Serializable]
    public sealed class ESAssetPackageLicenseMetadata
    {
        [LabelText("来源发布者")] public string sourcePublisher;
        [LabelText("来源包名称")] public string sourcePackageName;
        [LabelText("来源 URL")] public string sourceUrl;
        [LabelText("许可证类型")] public string licenseType;
        [LabelText("已确认可商业使用")] public bool commercialUseConfirmed;
        [LabelText("限制说明")] [TextArea(2, 5)] public string licenseNotes;
        [LabelText("第三方依赖")] [TextArea(2, 5)] public string thirdPartyDependencies;
    }

    [Serializable]
    public sealed class ESAssetPackageResolvedFolder
    {
        public ESAssetPackageCategory category;
        public string resolvedPath;
        public bool useFixedPath;
        public bool writable;
        public bool overlapsSource;
        public bool overlapsOtherCategory;
        public string collisionState;
    }

    [Serializable]
    public sealed class ESAssetPackageAnalysisSummary
    {
        [LabelText("总资产数"), ReadOnly] public int totalCount;
        [LabelText("ParticleSystem"), ReadOnly] public int particleSystemCount;
        [LabelText("VFX Graph 候选"), ReadOnly] public int vfxGraphCount;
        [LabelText("可池化候选"), ReadOnly] public int poolCandidateCount;
        [LabelText("循环特效"), ReadOnly] public int loopingCount;
        [LabelText("自定义脚本风险"), ReadOnly] public int customScriptRiskCount;
        [LabelText("材质/Shader 风险"), ReadOnly] public int materialRiskCount;
        [LabelText("待人工确认"), ReadOnly] public int needsReviewCount;
    }

    [Serializable]
    public sealed class ESAssetPackageAnalysisRecord
    {
        [LabelText("GUID"), ReadOnly] public string guid;
        [LabelText("资产路径"), ReadOnly] public string assetPath;
        [LabelText("资产名称"), ReadOnly] public string assetName;
        [LabelText("类型"), ReadOnly] public string typeName;
        [LabelText("分类"), ReadOnly] public ESAssetPackageCategory category;
        [LabelText("文件 Hash"), ReadOnly] public string sourceHash;
        [LabelText("依赖数"), ReadOnly] public int dependencyCount;
        [LabelText("ParticleSystem 数"), ReadOnly] public int particleSystemCount;
        [LabelText("Renderer 数"), ReadOnly] public int rendererCount;
        [LabelText("材质数"), ReadOnly] public int materialCount;
        [LabelText("自定义脚本数"), ReadOnly] public int customScriptCount;
        [LabelText("VFX Graph 候选"), ReadOnly] public bool vfxGraphCandidate;
        [LabelText("循环"), ReadOnly] public bool looping;
        [LabelText("估算时长"), ReadOnly] public float estimatedDuration;
        [LabelText("可池化候选"), ReadOnly] public bool poolCandidate;
        [LabelText("状态")] public ESAssetPackageAnalysisState state;
        [LabelText("推荐用途")] public string recommendedUse;
        [LabelText("标签")] public List<string> tags = new List<string>();
        [LabelText("风险")] public List<string> risks = new List<string>();
        [LabelText("AI 备注")] [TextArea(2, 6)] public string aiNotes;
        [LabelText("置信度"), Range(0f, 1f)] public float confidence;
    }

    [ESOnlyEditorSO("仅保存资产包分析快照，不进入运行时资源、Manifest 或 AssetBundle。")]
    [CreateAssetMenu(fileName = "资产包分析", menuName = MenuItemPathDefine.ASSET_DEV_MANAGEMENT_PATH + "资产包分析")]
    public sealed class ESAssetPackageAnalysisData : ESSO
    {
        [LabelText("所属资产包"), ReadOnly] public string packageGuid;
        [LabelText("资产包路径"), ReadOnly] public string packagePath;
        [LabelText("资产包 Hash"), ReadOnly] public string packageHash;
        [LabelText("分析器版本"), ReadOnly] public string analyzerVersion;
        [LabelText("分析时间"), ReadOnly] public string analyzedAt;
        [LabelText("状态")] public ESAssetPackageAnalysisState state;
        [LabelText("统计")] public ESAssetPackageAnalysisSummary summary = new ESAssetPackageAnalysisSummary();
        [LabelText("资产分析记录"), Searchable]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 20, ListElementLabelName = "assetName")]
        public List<ESAssetPackageAnalysisRecord> records = new List<ESAssetPackageAnalysisRecord>();

        public bool IsCurrent(string currentPackageHash)
        {
            return state != ESAssetPackageAnalysisState.Stale &&
                   !string.IsNullOrEmpty(packageHash) &&
                   string.Equals(packageHash, currentPackageHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum ESAssetPackageCategory
    {
        [InspectorName("预制体")] Prefab,
        [InspectorName("场景")] Scene,
        [InspectorName("材质")] Material,
        [InspectorName("贴图")] Texture,
        [InspectorName("模型")] Model,
        [InspectorName("音频")] Audio,
        [InspectorName("动画")] Animation,
        [InspectorName("SO资产")] ScriptableObject,
        [InspectorName("Shader")] Shader,
        [InspectorName("字体")] Font,
        [InspectorName("视频")] Video,
        [InspectorName("其他")] Other
    }

    [Serializable]
    public sealed class ESAssetPackageBakeRecord
    {
        [LabelText("使用"), HorizontalGroup("Top", Width = 48)]
        public bool selectedForUse;

        [LabelText("分类"), HorizontalGroup("Top", Width = 88), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("名称"), HorizontalGroup("Top"), ReadOnly]
        public string assetName;

        [LabelText("路径"), ReadOnly]
        public string assetPath;

        [LabelText("GUID"), ReadOnly]
        public string guid;

        [LabelText("类型"), ReadOnly]
        public string typeName;

        [LabelText("大小"), ReadOnly]
        public string fileSize;

        [LabelText("导出子目录"), ReadOnly]
        public string exportSubFolder;

#if UNITY_EDITOR
        [HorizontalGroup("Ops"), Button("Ping", ButtonSizes.Small)]
        public void Ping()
        {
            UnityEngine.Object asset = LoadAsset();
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        [HorizontalGroup("Ops"), Button("选中", ButtonSizes.Small)]
        public void SelectAsset()
        {
            UnityEngine.Object asset = LoadAsset();
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        [HorizontalGroup("Ops"), Button("设为使用", ButtonSizes.Small)]
        public void MarkUsed()
        {
            selectedForUse = true;
        }

        [HorizontalGroup("Ops"), Button("取消使用", ButtonSizes.Small)]
        public void UnmarkUsed()
        {
            selectedForUse = false;
        }

        public UnityEngine.Object LoadAsset()
        {
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
                assetPath = AssetDatabase.GUIDToAssetPath(guid);

            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
        }
#endif
    }

    [Serializable]
    public sealed class ESAssetPackageExportLink
    {
        [LabelText("源GUID"), ReadOnly]
        public string sourceGuid;

        [LabelText("源路径"), ReadOnly]
        public string sourceAssetPath;

        [LabelText("导出GUID"), ReadOnly]
        public string targetGuid;

        [LabelText("导出路径"), ReadOnly]
        public string targetAssetPath;

        [LabelText("分类"), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("用户直接选择"), ReadOnly]
        public bool rootSelected;

        [LabelText("最后导出会话"), ReadOnly]
        public string lastExportSessionId;

        [LabelText("最后导出时间"), ReadOnly]
        public string lastExportTime;

        [LabelText("导出次数"), ReadOnly]
        public int exportCount;

        [LabelText("源依赖 Hash"), ReadOnly]
        public string sourceDependencyHash;

        [LabelText("目标文件 Hash"), ReadOnly]
        public string targetFileHash;

        [LabelText("导出配置指纹"), ReadOnly]
        public string exportConfigFingerprint;

        [LabelText("链路状态"), ReadOnly]
        public ESAssetPackageExportLinkState linkState;

        [LabelText("Package ID"), ReadOnly]
        public string packageId;
    }

    [Serializable]
    public sealed class ESAssetPackageExportChain
    {
        [LabelText("源GUID"), ReadOnly]
        public string sourceGuid;

        [LabelText("源路径"), ReadOnly]
        public string sourceAssetPath;

        [LabelText("目标GUID"), ReadOnly]
        public string targetGuid;

        [LabelText("目标路径"), ReadOnly]
        public string targetAssetPath;

        [LabelText("分类"), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("用户直接选择"), ReadOnly]
        public bool rootSelected;

        [LabelText("有效"), ReadOnly]
        public bool targetExists;

        [LabelText("最后导出会话"), ReadOnly]
        public string lastExportSessionId;

        [LabelText("最后导出时间"), ReadOnly]
        public string lastExportTime;

        [LabelText("导出次数"), ReadOnly]
        public int exportCount;

        [LabelText("源依赖 Hash"), ReadOnly]
        public string sourceDependencyHash;

        [LabelText("目标文件 Hash"), ReadOnly]
        public string targetFileHash;

        [LabelText("导出配置指纹"), ReadOnly]
        public string exportConfigFingerprint;

        [LabelText("链路状态"), ReadOnly]
        public ESAssetPackageExportLinkState linkState;

        [LabelText("Package ID"), ReadOnly]
        public string packageId;

        public ESAssetPackageExportLink ToLink()
        {
            return new ESAssetPackageExportLink
            {
                sourceGuid = sourceGuid,
                sourceAssetPath = sourceAssetPath,
                targetGuid = targetGuid,
                targetAssetPath = targetAssetPath,
                category = category,
                rootSelected = rootSelected,
                lastExportSessionId = lastExportSessionId,
                lastExportTime = lastExportTime,
                exportCount = exportCount,
                sourceDependencyHash = sourceDependencyHash,
                targetFileHash = targetFileHash,
                exportConfigFingerprint = exportConfigFingerprint,
                linkState = linkState
                , packageId = packageId
            };
        }

        public void FromLink(ESAssetPackageExportLink link)
        {
            if (link == null)
                return;

            sourceGuid = link.sourceGuid;
            sourceAssetPath = link.sourceAssetPath;
            targetGuid = link.targetGuid;
            targetAssetPath = link.targetAssetPath;
            category = link.category;
            rootSelected = link.rootSelected;
#if UNITY_EDITOR
            targetExists = !string.IsNullOrEmpty(targetAssetPath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetAssetPath) != null;
#else
            targetExists = false;
#endif
            lastExportSessionId = link.lastExportSessionId;
            lastExportTime = link.lastExportTime;
            exportCount = link.exportCount;
            sourceDependencyHash = link.sourceDependencyHash;
            targetFileHash = link.targetFileHash;
            exportConfigFingerprint = link.exportConfigFingerprint;
            linkState = link.linkState;
            packageId = link.packageId;
        }
    }

    [Serializable]
    public sealed class ESAssetPackageExportSession
    {
        [LabelText("会话ID"), ReadOnly]
        public string sessionId;

        [LabelText("配置名称"), ReadOnly]
        public string configName;

        [LabelText("导出时间"), ReadOnly]
        public string exportTime;

        [LabelText("导出根目录"), ReadOnly]
        public string exportRootPath;

        [LabelText("直接选择数"), ReadOnly]
        public int selectedRootCount;

        [LabelText("导出总数"), ReadOnly]
        public int totalAssetCount;

        [LabelText("依赖数"), ReadOnly]
        public int dependencyAssetCount;

        [LabelText("新增数"), ReadOnly]
        public int createdCount;

        [LabelText("更新数"), ReadOnly]
        public int updatedCount;

        [LabelText("重映射文件"), ReadOnly]
        public int remappedFileCount;

        [LabelText("失败数"), ReadOnly]
        public int errorCount;

        [LabelText("事务状态"), ReadOnly]
        public ESAssetPackageExportAttemptState transactionState;

        [LabelText("Package ID"), ReadOnly]
        public string packageId;

        [LabelText("解析快照 Hash"), ReadOnly]
        public string resolutionSnapshotHash;

        [LabelText("事务警告"), ReadOnly]
        public string transactionWarning;

        [LabelText("回退状态"), ReadOnly]
        public ESAssetPackageRollbackState rollbackState;

        [LabelText("重复跳过数"), ReadOnly]
        public int duplicateSkippedCount;

        [LabelText("导出目标路径"), ReadOnly]
        public List<string> targetAssetPaths = new List<string>();

        [LabelText("导出目标GUID"), ReadOnly]
        public List<string> targetAssetGuids = new List<string>();

        [LabelText("导出目标 Hash"), ReadOnly]
        public List<string> targetAssetFileHashes = new List<string>();

        [LabelText("源 GUID"), ReadOnly]
        public List<string> sourceAssetGuids = new List<string>();

        [LabelText("依赖源路径"), ReadOnly]
        public List<string> dependencyAssetPaths = new List<string>();

        [LabelText("重复跳过源路径"), ReadOnly]
        public List<string> duplicateSkippedSourcePaths = new List<string>();

        [LabelText("失败源路径"), ReadOnly]
        public List<string> errorAssetPaths = new List<string>();
    }

    [Serializable]
    public sealed class ESAssetPackageCategoryFolderSetting
    {
        [LabelText("分类"), ReadOnly]
        public ESAssetPackageCategory category;

        [LabelText("文件夹名")]
        public string folderName;

        [LabelText("绑定固定路径")]
        public bool useFixedAssetPath;

        [LabelText("固定 Assets 路径")]
        public string fixedAssetFolderPath;
    }

    [ESOnlyEditorSO("资产包烘焙数据只保存编辑器复制/收集状态，不应进入运行时构建或AB资源包。")]
    [CreateAssetMenu(fileName = "资产包烘焙数据", menuName = MenuItemPathDefine.ASSET_DEV_MANAGEMENT_PATH + "资产包烘焙数据")]
    public class ESAssetPackageBakeData : ESSO
    {
        [DisplayAsString(fontSize: 24, Alignment = TextAlignment.Center), HideLabel]
        [GUIColor(0.45f, 0.82f, 1f)]
        public string title = "资产包烘焙数据";

        [LabelText("显示名称")]
        public string displayName = "新资产包";

        [FoldoutGroup("资产包身份"), LabelText("Package ID")]
        public string packageId;

        [FoldoutGroup("资产包身份"), LabelText("Schema 版本"), ReadOnly]
        public int packageSchemaVersion = 2;

        [FoldoutGroup("资产包身份"), LabelText("内容版本")]
        public string contentVersion = "1.0.0";

        [FoldoutGroup("资产包身份"), LabelText("内容 Hash"), ReadOnly]
        public string contentHash;

        [FoldoutGroup("资产包身份"), LabelText("所有者")]
        public string owner;

        [FoldoutGroup("来源与授权"), LabelText("来源与许可证")]
        public ESAssetPackageLicenseMetadata licenseMetadata = new ESAssetPackageLicenseMetadata();

        [LabelText("目标文件夹路径"), FolderPath(AbsolutePath = false)]
        public string targetFolderPath = "Assets";

        [LabelText("默认导出根目录"), FolderPath(AbsolutePath = false)]
        public string exportRootPath = "Assets/_ESAssetPackageExport";

        [FoldoutGroup("导出配置"), LabelText("配置名称")]
        public string exportConfigName = "默认导出配置";

        [FoldoutGroup("导出配置"), LabelText("预览兜底材质")]
        public Material previewFallbackMaterial;

        [FoldoutGroup("导出配置"), LabelText("动作预览默认模型")]
        public GameObject animationPreviewModel;

        [FoldoutGroup("导出配置"), LabelText("动作预览Avatar")]
        public Avatar animationPreviewAvatar;

        [LabelText("包含子文件夹")]
        public bool includeSubFolders = true;

        [FoldoutGroup("导出配置"), LabelText("导出依赖资源")]
        public bool exportDependencies = true;

        [FoldoutGroup("导出配置"), LabelText("重映射导出内部GUID")]
        public bool remapExportedGuids = true;

        [FoldoutGroup("导出配置"), LabelText("重复导出时覆盖旧目标")]
        public bool overwriteExistingExport = false;

        [FoldoutGroup("导出配置"), LabelText("源资源变更时增量更新")]
        public bool updateChangedExports = true;

        [FoldoutGroup("导出配置"), LabelText("导出前自动修正链路")]
        public bool repairExportLinksOnExport = true;

        [FoldoutGroup("导出配置"), LabelText("变更配置时重新导出")]
        public bool reexportWhenConfigChanges = true;

        [FoldoutGroup("导出配置"), LabelText("导出配置指纹"), ReadOnly]
        public string exportConfigFingerprint;

        [FoldoutGroup("AI 资产分析"), LabelText("导出策略")]
        public ESAssetPackageExportPolicy exportPolicy = new ESAssetPackageExportPolicy();

        [FoldoutGroup("扫描配置"), LabelText("排除目录")]
        public List<string> excludedFolders = new List<string>();

        [FoldoutGroup("导出配置"), LabelText("导出文件名前缀")]
        public string exportFileNamePrefix = "ES选用_";

        [LabelText("最后烘焙时间"), ReadOnly]
        public string lastBakeTime;

        [LabelText("总资产数"), ReadOnly]
        public int totalAssetCount;

        [LabelText("已选使用数"), ReadOnly]
        public int selectedUseCount;

        [LabelText("分类统计"), DictionaryDrawerSettings(KeyLabel = "分类", ValueLabel = "数量")]
        public Dictionary<ESAssetPackageCategory, int> categoryCounts = new Dictionary<ESAssetPackageCategory, int>();

        [LabelText("资产记录"), Searchable]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "assetName", NumberOfItemsPerPage = 20)]
        public List<ESAssetPackageBakeRecord> records = new List<ESAssetPackageBakeRecord>();

        [FoldoutGroup("AI 资产分析"), LabelText("分析快照"), ReadOnly]
        public ESAssetPackageAnalysisData analysisData;

        [FoldoutGroup("AI 资产分析"), LabelText("分析状态"), ReadOnly]
        public ESAssetPackageAnalysisState analysisState;

        [FoldoutGroup("AI 资产分析"), LabelText("分析包 Hash"), ReadOnly]
        public string analysisPackageHash;

        [FoldoutGroup("AI 资产分析"), LabelText("分析记录数"), ReadOnly]
        public int analysisRecordCount;

        public void MarkAnalysisStale()
        {
            if (analysisData == null)
                return;

            Undo.RecordObject(analysisData, "标记资产包分析过期");
            analysisState = ESAssetPackageAnalysisState.Stale;
            analysisData.state = ESAssetPackageAnalysisState.Stale;
            EditorUtility.SetDirty(analysisData);
        }

        [FoldoutGroup("导出链路"), LabelText("最后导出时间"), ReadOnly]
        public string lastExportTime;

        [FoldoutGroup("导出链路"), LabelText("最后导出根目录"), ReadOnly]
        public string lastExportRootPath;

        [FoldoutGroup("导出链路"), LabelText("最后导出总数"), ReadOnly]
        public int lastExportAssetCount;

        [FoldoutGroup("导出链路"), LabelText("最后导出依赖数"), ReadOnly]
        public int lastExportDependencyCount;

        [LabelText("最近导出尝试状态"), ReadOnly]
        public ESAssetPackageExportAttemptState lastExportAttemptState = ESAssetPackageExportAttemptState.Idle;

        [FoldoutGroup("导出解析"), LabelText("当前解析快照"), ReadOnly]
        public ESAssetPackageResolutionSnapshot currentResolutionSnapshot;

        [LabelText("最近导出尝试会话"), ReadOnly]
        public string lastExportAttemptSessionId;

        [LabelText("最近导出尝试时间"), ReadOnly]
        public string lastExportAttemptTime;

        [LabelText("最近导出尝试说明"), ReadOnly]
        public string lastExportAttemptMessage;

        [FoldoutGroup("导出链路"), LabelText("导出链路")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 12)]
        public List<ESAssetPackageExportLink> exportLinks = new List<ESAssetPackageExportLink>();

        [FoldoutGroup("导出链路"), LabelText("导出链路字典(SourceGuid)")]
        [DictionaryDrawerSettings(KeyLabel = "源GUID", ValueLabel = "链路")]
        public Dictionary<string, ESAssetPackageExportChain> exportChainBySourceGuid = new Dictionary<string, ESAssetPackageExportChain>();

        [FoldoutGroup("导出链路"), LabelText("导出会话")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 8)]
        public List<ESAssetPackageExportSession> exportSessions = new List<ESAssetPackageExportSession>();

        [FoldoutGroup("导出配置"), LabelText("分类导出文件夹")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 12)]
        public List<ESAssetPackageCategoryFolderSetting> categoryFolderSettings = new List<ESAssetPackageCategoryFolderSetting>();

        public void EnsureCategoryFolderSettings()
        {
            if (categoryFolderSettings == null)
                categoryFolderSettings = new List<ESAssetPackageCategoryFolderSetting>();

            foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
            {
                ESAssetPackageCategoryFolderSetting setting = categoryFolderSettings.FirstOrDefault(x => x != null && x.category == category);
                if (setting == null)
                {
                    categoryFolderSettings.Add(new ESAssetPackageCategoryFolderSetting
                    {
                        category = category,
                        folderName = GetDefaultExportSubFolder(category),
                        useFixedAssetPath = false,
                        fixedAssetFolderPath = string.Empty
                    });
                }
                else if (string.IsNullOrWhiteSpace(setting.folderName))
                {
                    setting.folderName = GetDefaultExportSubFolder(category);
                }
            }

            categoryFolderSettings.RemoveAll(x => x == null);
            categoryFolderSettings.Sort((a, b) => a.category.CompareTo(b.category));
        }

        public string GetConfiguredExportSubFolder(ESAssetPackageCategory category)
        {
            EnsureCategoryFolderSettings();
            ESAssetPackageCategoryFolderSetting setting = categoryFolderSettings.FirstOrDefault(x => x != null && x.category == category);
            return SanitizeFolderName(string.IsNullOrWhiteSpace(setting?.folderName) ? GetDefaultExportSubFolder(category) : setting.folderName);
        }

        public void EnsureIdentity()
        {
            if (string.IsNullOrWhiteSpace(packageId))
                packageId = Guid.NewGuid().ToString("N");
            packageId = packageId.Trim();
            if (packageSchemaVersion <= 0)
                packageSchemaVersion = 2;
            if (string.IsNullOrWhiteSpace(contentVersion))
                contentVersion = "1.0.0";
            if (licenseMetadata == null)
                licenseMetadata = new ESAssetPackageLicenseMetadata();
            if (exportPolicy == null)
                exportPolicy = new ESAssetPackageExportPolicy();
            if (excludedFolders == null)
                excludedFolders = new List<string>();
        }

#if UNITY_EDITOR
        public bool HasValidIdentity(out string error)
        {
            error = string.Empty;
            EnsureIdentity();
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "资产包必须先保存为项目资产。";
                return false;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ESAssetPackageBakeData"))
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(candidatePath, assetPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                ESAssetPackageBakeData candidate = AssetDatabase.LoadAssetAtPath<ESAssetPackageBakeData>(candidatePath);
                if (candidate != null && string.Equals(candidate.packageId, packageId, StringComparison.Ordinal))
                {
                    error = "Package ID 已被其他资产包占用：" + candidatePath;
                    return false;
                }
            }
            return true;
        }
#endif

        public List<ESAssetPackageResolvedFolder> ResolveExportFolders(string sourceRoot)
        {
            EnsureIdentity();
            EnsureCategoryFolderSettings();
            var result = new List<ESAssetPackageResolvedFolder>();
            string normalizedSource = NormalizeAssetPath(sourceRoot);
            foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
            {
                string path = GetConfiguredExportFolder(exportRootPath, category);
                bool overlaps = !string.Equals(normalizedSource, "Assets", StringComparison.OrdinalIgnoreCase) &&
                                (IsPathInsideRoot(path, normalizedSource) || IsPathInsideRoot(normalizedSource, path));
                bool safePath = ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(path, out string normalizedPath)
                    && !ESAssetPackagePathSafety.IsForbiddenExportFolder(normalizedPath)
                    && !ESAssetPackagePathSafety.HasReparsePointInPath(normalizedPath);
                result.Add(new ESAssetPackageResolvedFolder
                {
                    category = category,
                    resolvedPath = safePath ? normalizedPath : path,
                    useFixedPath = categoryFolderSettings.Any(x => x != null && x.category == category && x.useFixedAssetPath),
                    writable = safePath,
                    overlapsSource = overlaps,
                    collisionState = !safePath ? "UnsafePath" : (overlaps ? "SourceOverlap" : string.Empty)
                });
            }
            var groups = result.GroupBy(x => x.resolvedPath, StringComparer.OrdinalIgnoreCase);
            foreach (var group in groups.Where(x => x.Count() > 1))
                foreach (var item in group) { item.overlapsOtherCategory = true; item.collisionState = "CategoryCollision"; }
            return result;
        }

        public string ComputeExportConfigFingerprint()
        {
            EnsureIdentity();
            EnsureCategoryFolderSettings();
            var sb = new StringBuilder();
            sb.Append("schema=").Append(packageSchemaVersion).Append('|');
            sb.Append("root=").Append(NormalizeAssetPath(exportRootPath)).Append('|');
            sb.Append("prefix=").Append(exportFileNamePrefix ?? string.Empty).Append('|');
            sb.Append("deps=").Append(exportDependencies).Append('|');
            sb.Append("remap=").Append(remapExportedGuids).Append('|');
            sb.Append("update=").Append(updateChangedExports).Append('|');
            sb.Append("overwrite=").Append(overwriteExistingExport).Append('|');
            sb.Append("reexportConfig=").Append(reexportWhenConfigChanges).Append('|');
            sb.Append("sourceRoot=").Append(NormalizeAssetPath(targetFolderPath)).Append('|');
            sb.Append("excluded=");
            foreach (string excluded in (excludedFolders ?? new List<string>()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                sb.Append(NormalizeAssetPath(excluded)).Append(';');
            sb.Append('|');
            foreach (var setting in categoryFolderSettings.OrderBy(x => x.category))
            {
                ESAssetPackageCategoryDescriptor descriptor = ESAssetPackageCategoryCatalog.Get(setting.category);
                sb.Append(descriptor.stableKey).Append('=').Append(setting.folderName).Append('|').Append(setting.useFixedAssetPath).Append('|').Append(NormalizeAssetPath(setting.fixedAssetFolderPath)).Append(';');
            }
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", string.Empty);
        }

        public string GetConfiguredExportFolder(string exportRootPath, ESAssetPackageCategory category)
        {
            EnsureCategoryFolderSettings();
            ESAssetPackageCategoryFolderSetting setting = categoryFolderSettings.FirstOrDefault(x => x != null && x.category == category);
            if (setting != null && setting.useFixedAssetPath)
            {
                if (ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(setting.fixedAssetFolderPath, out string fixedPath))
                    return fixedPath;
                return string.Empty;
            }

            string fallback = NormalizeAssetPath($"{exportRootPath}/{GetConfiguredExportSubFolder(category)}");
            return ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(fallback, out string normalizedFallback)
                ? normalizedFallback
                : fallback;
        }

        public static string GetDefaultExportSubFolder(ESAssetPackageCategory category)
        {
            return ESAssetPackageCategoryCatalog.Get(category).defaultExportFolder;
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Others";

            string result = name.Trim().Replace("\\", "_").Replace("/", "_").Replace(":", "_");
            foreach (char c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return string.IsNullOrWhiteSpace(result) ? "Others" : result;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            path = NormalizeAssetPath(path);
            root = NormalizeAssetPath(root);
            return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(root) &&
                   (string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<ESAssetPackageBakeRecord> GetRecords(ESAssetPackageCategory category)
        {
            if (records == null)
                yield break;

            for (int i = 0; i < records.Count; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                if (record != null && record.category == category)
                    yield return record;
            }
        }

        public void RebuildStats()
        {
            totalAssetCount = records != null ? records.Count : 0;
            selectedUseCount = 0;
            categoryCounts.Clear();

            if (records == null)
                return;

            for (int i = 0; i < records.Count; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                if (record == null)
                    continue;

                if (record.selectedForUse)
                    selectedUseCount++;

                if (!categoryCounts.ContainsKey(record.category))
                    categoryCounts[record.category] = 0;
                categoryCounts[record.category]++;
            }
        }

#if UNITY_EDITOR
        [Button("重新烘焙目标文件夹", ButtonHeight = 32), GUIColor(0.32f, 0.58f, 0.9f)]
        public void BakeNow()
        {
            ESAssetPackageBakeUtility.Bake(this);
        }

        [Button("选中所有已使用资产", ButtonHeight = 24)]
        public void SelectUsedAssets()
        {
            if (records == null)
                return;

            var assets = new List<UnityEngine.Object>();
            for (int i = 0; i < records.Count; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                if (record == null || !record.selectedForUse)
                    continue;

                UnityEngine.Object asset = record.LoadAsset();
                if (asset != null)
                    assets.Add(asset);
            }

            Selection.objects = assets.ToArray();
            if (assets.Count > 0)
                EditorGUIUtility.PingObject(assets[0]);
        }

        [Button("复制勾选资产到分类文件夹", ButtonHeight = 32), GUIColor(0.35f, 0.72f, 0.45f)]
        public void ExportSelectedAssetsByCategory()
        {
            ESAssetPackageBakeUtility.ExportSelectedAssetsByCategory(this);
        }

        [Button("回退最近一次导出", ButtonHeight = 28), GUIColor(0.8f, 0.45f, 0.35f)]
        public void RollbackLastExport()
        {
            ESAssetPackageBakeUtility.RollbackLastExport(this);
        }

        [Button("分析资产可用性", ButtonHeight = 32), GUIColor(0.42f, 0.68f, 0.95f)]
        public void AnalyzeAssetUsability()
        {
            ESAssetPackageAnalysisUtility.Analyze(this);
        }

#endif
    }

#if UNITY_EDITOR
    public static class ESAssetPackageAnalysisUtility
    {
        public static void Analyze(ESAssetPackageBakeData data)
        {
            if (data == null || data.records == null)
                return;

            Undo.RecordObject(data, "分析 ES 资产包");
            ESAssetPackageAnalysisData analysis = data.analysisData;
            if (analysis == null)
            {
                string bakePath = AssetDatabase.GetAssetPath(data);
                if (string.IsNullOrEmpty(bakePath))
                {
                    Debug.LogError("[ES] 资产包分析需要先保存资产包烘焙数据。");
                    return;
                }

                string folder = Path.GetDirectoryName(bakePath)?.Replace("\\", "/") ?? "Assets";
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileNameWithoutExtension(bakePath) + "_Analysis.asset");
                analysis = ScriptableObject.CreateInstance<ESAssetPackageAnalysisData>();
                AssetDatabase.CreateAsset(analysis, path);
                Undo.RegisterCreatedObjectUndo(analysis, "创建 ES 资产包分析数据");
                data.analysisData = analysis;
            }

            Undo.RecordObject(analysis, "分析 ES 资产包");
            analysis.packageGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
            analysis.packagePath = AssetDatabase.GetAssetPath(data);
            analysis.analyzerVersion = "ESAssetPackageAnalysis/1";
            analysis.analyzedAt = DateTime.UtcNow.ToString("O");
            analysis.records.Clear();

            foreach (ESAssetPackageBakeRecord source in data.records)
            {
                if (source == null || string.IsNullOrEmpty(source.assetPath))
                    continue;

                ESAssetPackageAnalysisRecord record = AnalyzeRecord(source);
                analysis.records.Add(record);
            }

            analysis.summary.totalCount = analysis.records.Count;
            analysis.summary.particleSystemCount = analysis.records.Count(x => x.particleSystemCount > 0);
            analysis.summary.vfxGraphCount = analysis.records.Count(x => x.vfxGraphCandidate);
            analysis.summary.poolCandidateCount = analysis.records.Count(x => x.poolCandidate);
            analysis.summary.loopingCount = analysis.records.Count(x => x.looping);
            analysis.summary.customScriptRiskCount = analysis.records.Count(x => x.customScriptCount > 0);
            analysis.summary.materialRiskCount = analysis.records.Count(x => x.risks != null && x.risks.Any(r => r.Contains("材质")));
            analysis.summary.needsReviewCount = analysis.records.Count(x => x.state == ESAssetPackageAnalysisState.NeedsReview);
            analysis.state = analysis.summary.needsReviewCount > 0 ? ESAssetPackageAnalysisState.NeedsReview : ESAssetPackageAnalysisState.Analyzed;

            data.analysisState = analysis.state;
            data.analysisRecordCount = data.records.Count;
            data.analysisPackageHash = ComputePackageHash(analysis.records);
            analysis.packageHash = data.analysisPackageHash;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(analysis);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.SaveAssetIfDirty(analysis);
            Debug.Log($"[ES] 资产包分析完成: {data.name}, 记录 {data.analysisRecordCount}, ParticleSystem {analysis.summary.particleSystemCount}, 待确认 {analysis.summary.needsReviewCount}。");
        }

        private static ESAssetPackageAnalysisRecord AnalyzeRecord(ESAssetPackageBakeRecord source)
        {
            string path = source.assetPath.Replace("\\", "/");
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            var record = new ESAssetPackageAnalysisRecord
            {
                guid = string.IsNullOrEmpty(source.guid) ? AssetDatabase.AssetPathToGUID(path) : source.guid,
                assetPath = path,
                assetName = source.assetName,
                typeName = type != null ? type.FullName : source.typeName,
                category = source.category,
                sourceHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                dependencyCount = Math.Max(0, AssetDatabase.GetDependencies(path, true).Length - 1),
                state = ESAssetPackageAnalysisState.Analyzed,
                confidence = 0.65f,
                vfxGraphCandidate = Path.GetExtension(path).Equals(".vfx", StringComparison.OrdinalIgnoreCase)
            };

            if (asset is GameObject prefab)
            {
                ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                record.particleSystemCount = systems.Length;
                record.rendererCount = renderers.Length;
                record.customScriptCount = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                    .Count(x => x != null && x.GetType().Assembly != typeof(GameObject).Assembly);
                record.looping = systems.Any(x => x != null && x.main.loop);
                record.estimatedDuration = systems.Length == 0 ? 0f : systems.Max(x => x.main.duration + (x.main.startLifetime.mode == ParticleSystemCurveMode.Constant ? x.main.startLifetime.constant : 0f));
                record.poolCandidate = systems.Length > 0 && !record.looping && record.estimatedDuration > 0f && record.estimatedDuration < 30f;
                record.materialCount = renderers.SelectMany(x => x.sharedMaterials ?? Array.Empty<Material>()).Where(x => x != null).Distinct().Count();
                record.recommendedUse = record.looping ? "Aura/持续表现" : "命中/爆炸/短时表现";
                record.tags.Add("particle-system");
                if (record.poolCandidate) record.tags.Add("pool-candidate");
                if (record.looping) record.risks.Add("循环特效需要独立停止策略");
                if (record.customScriptCount > 0) record.risks.Add("包含自定义 MonoBehaviour，需要检查外部运行时依赖");
                if (record.materialCount == 0) record.risks.Add("未识别到材质，可能存在材质或依赖缺失");
            }
            else if (record.vfxGraphCandidate)
            {
                record.state = ESAssetPackageAnalysisState.NeedsReview;
                record.recommendedUse = "VFX Graph 高级表现候选";
                record.tags.Add("vfx-graph");
                record.risks.Add("VFX Graph 参数和生命周期需要人工确认");
                record.confidence = 0.45f;
            }
            else
            {
                record.state = ESAssetPackageAnalysisState.NeedsReview;
                record.recommendedUse = "待分类";
            }

            return record;
        }

        public static string ComputeCurrentPackageHash(ESAssetPackageBakeData data)
        {
            if (data == null || data.records == null)
                return string.Empty;

            var records = new List<ESAssetPackageAnalysisRecord>();
            foreach (ESAssetPackageBakeRecord source in data.records)
            {
                if (source == null)
                    continue;

                string path = NormalizeAssetPath(source.assetPath);
                records.Add(new ESAssetPackageAnalysisRecord
                {
                    guid = string.IsNullOrEmpty(source.guid) ? AssetDatabase.AssetPathToGUID(path) : source.guid,
                    assetPath = path,
                    sourceHash = AssetDatabase.GetAssetDependencyHash(path).ToString()
                });
            }

            return ComputePackageHash(records);
        }

        private static string ComputePackageHash(List<ESAssetPackageAnalysisRecord> records)
        {
            string text = string.Join("\n", records.Select(x => (x.guid ?? string.Empty) + "|" + (x.assetPath ?? string.Empty) + "|" + (x.sourceHash ?? string.Empty)));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }
    }

    public static class ESAssetPackagePreflightUtility
    {
        public static bool ShowExportPreflight(ESAssetPackageBakeData data)
        {
            if (data == null)
                return false;

            List<ESAssetPackageBakeRecord> roots = data.records == null
                ? new List<ESAssetPackageBakeRecord>()
                : data.records.Where(x => x != null && x.selectedForUse).ToList();
            var errors = new List<string>();
            var warnings = new List<string>();
            var sourceGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int dependencyCount = 0;
            int editorOnlyCount = 0;

            if (string.IsNullOrWhiteSpace(data.targetFolderPath) || !AssetDatabase.IsValidFolder(data.targetFolderPath))
                errors.Add("目标资源包文件夹不存在或不是 Assets 内有效目录。");
            if (string.IsNullOrWhiteSpace(data.exportRootPath) || !data.exportRootPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                errors.Add("默认导出根目录必须位于 Assets/ 下。");
            if (roots.Count == 0)
                warnings.Add("没有勾选任何根资产，当前不会产生导出计划。");

            foreach (ESAssetPackageBakeRecord root in roots)
            {
                string path = NormalizeAssetPath(root.assetPath);
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    errors.Add("根资产缺失：" + (string.IsNullOrEmpty(path) ? root.assetName : path));
                    continue;
                }

                string guid = string.IsNullOrEmpty(root.guid) ? AssetDatabase.AssetPathToGUID(path) : root.guid;
                if (string.IsNullOrEmpty(guid))
                    errors.Add("根资产没有有效 GUID：" + path);
                else if (!sourceGuids.Add(guid))
                    errors.Add("根资产 GUID 重复：" + guid);

                if (!data.exportDependencies)
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(path, true);
                dependencyCount += Math.Max(0, dependencies.Length - 1);
                foreach (string dependency in dependencies)
                {
                    if (string.IsNullOrEmpty(dependency) || dependency == path)
                        continue;
                    if (AssetDatabase.LoadMainAssetAtPath(dependency) == null)
                        errors.Add("依赖缺失：" + dependency);
                    Type type = AssetDatabase.GetMainAssetTypeAtPath(dependency);
                    if (type != null && typeof(ScriptableObject).IsAssignableFrom(type) && Attribute.IsDefined(type, typeof(ESOnlyEditorSOAttribute), true))
                        editorOnlyCount++;
                }
            }

            if (editorOnlyCount > 0)
                errors.Add("依赖闭包包含 " + editorOnlyCount + " 个 EditorOnly SO；当前导出策略禁止将不完整依赖闭包提交。");
            foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
            {
                string folder = data.GetConfiguredExportFolder(data.exportRootPath, category);
                if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(folder, out string normalizedFolder))
                    errors.Add("分类 " + category + " 的导出路径不在 Assets/ 下：" + folder);
                else if (ESAssetPackagePathSafety.IsForbiddenExportFolder(normalizedFolder))
                    errors.Add("分类 " + category + " 的导出路径属于 Unity 保留/编辑器目录：" + normalizedFolder);
                else if (ESAssetPackagePathSafety.HasReparsePointInPath(normalizedFolder))
                    errors.Add("分类 " + category + " 的导出路径包含重解析点，已拒绝：" + normalizedFolder);
                else if ((IsPathInsideRoot(normalizedFolder, NormalizeAssetPath(data.targetFolderPath)) || IsPathInsideRoot(NormalizeAssetPath(data.targetFolderPath), normalizedFolder))
                    && !ESAssetPackagePathSafety.IsAllowedExportOverlap(data.targetFolderPath, data.exportRootPath, normalizedFolder))
                    errors.Add("分类 " + category + " 的导出路径与扫描源目录重叠：" + normalizedFolder);
            }
            if (data.overwriteExistingExport)
                warnings.Add("已启用覆盖旧导出链路；提交前仍会执行事务备份和回滚保护。");

            string title = errors.Count == 0 ? "资产包导出预检通过" : "资产包导出预检阻断";
            string body = "根资产：" + roots.Count + "\n依赖资产：" + dependencyCount + "\n";
            if (warnings.Count > 0)
                body += "\n警告：\n- " + string.Join("\n- ", warnings);
            if (errors.Count > 0)
                body += "\n\n错误：\n- " + string.Join("\n- ", errors.Take(20));
            else
                body += "\n\n可以继续进入导出事务。";

            EditorUtility.DisplayDialog(title, body, "确定");
            return errors.Count == 0;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            path = NormalizeAssetPath(path);
            root = NormalizeAssetPath(root);
            return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(root) &&
                   (string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        }
    }

public static class ESAssetPackageBakeUtility
{
        private sealed class ExportPlanItem
        {
            public string sourcePath;
            public string targetPath;
            public ESAssetPackageCategory category;
            public bool rootSelected;
            public bool dependency;
            public bool overwrite;
            public ESAssetPackageExportReasonCode reasonCode;
        }

        private sealed class ExportTransactionItem
        {
            public ExportPlanItem plan;
            public string stagedPath;
            public string backupPath;
            public bool hadExistingTarget;
            public bool committed;
            public string expectedTargetGuid;
            public string expectedTargetFileHash;
            public string expectedStagedFileHash;
            public string committedTargetGuid;
            public string committedTargetFileHash;
        }

        private static ESAssetPackageExportReasonCode DetermineReasonCode(
            string sourcePath,
            string targetPath,
            bool overwrite,
            Dictionary<string, ESAssetPackageExportLink> previousLinks,
            string configFingerprint)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            ESAssetPackageExportLink oldLink = null;
            if (!string.IsNullOrEmpty(sourceGuid))
                previousLinks?.TryGetValue(sourceGuid, out oldLink);
            if (oldLink == null)
                previousLinks?.TryGetValue(NormalizeAssetPath(sourcePath), out oldLink);

            if (oldLink == null)
                return ESAssetPackageExportReasonCode.NewSource;
            if (string.IsNullOrEmpty(oldLink.targetAssetPath) || AssetDatabase.LoadMainAssetAtPath(oldLink.targetAssetPath) == null)
                return ESAssetPackageExportReasonCode.TargetMissing;
            if (!string.Equals(oldLink.exportConfigFingerprint, configFingerprint, StringComparison.OrdinalIgnoreCase))
                return ESAssetPackageExportReasonCode.ConfigChanged;
            if (overwrite && !string.Equals(oldLink.sourceDependencyHash, AssetDatabase.GetAssetDependencyHash(sourcePath).ToString(), StringComparison.OrdinalIgnoreCase))
                return ESAssetPackageExportReasonCode.DependencyChanged;
            return ESAssetPackageExportReasonCode.SourceChanged;
        }

        private static ESAssetPackageResolutionSnapshot BuildResolutionSnapshot(
            ESAssetPackageBakeData data,
            List<ExportPlanItem> plan,
            string configFingerprint)
        {
            var snapshot = new ESAssetPackageResolutionSnapshot
            {
                packageId = data.packageId,
                definitionHash = configFingerprint,
                createdUtc = DateTime.UtcNow.ToString("O")
            };

            foreach (ExportPlanItem planItem in plan)
            {
                bool targetExists = AssetDatabase.LoadMainAssetAtPath(planItem.targetPath) != null;
                snapshot.items.Add(new ESAssetPackageResolutionItem
                {
                    sourceGuid = AssetDatabase.AssetPathToGUID(planItem.sourcePath),
                    sourcePath = NormalizeAssetPath(planItem.sourcePath),
                    sourceDependencyHash = AssetDatabase.GetAssetDependencyHash(planItem.sourcePath).ToString(),
                    sourceFileHash = ComputeAssetFileHash(planItem.sourcePath),
                    targetPath = NormalizeAssetPath(planItem.targetPath),
                    expectedTargetGuid = targetExists ? AssetDatabase.AssetPathToGUID(planItem.targetPath) : string.Empty,
                    expectedTargetFileHash = targetExists ? ComputeAssetFileHash(planItem.targetPath) : string.Empty,
                    category = planItem.category,
                    operation = planItem.overwrite ? ESAssetPackageExportOperation.Update : ESAssetPackageExportOperation.Create,
                    reasonCode = planItem.reasonCode,
                    rootSelected = planItem.rootSelected,
                    dependency = planItem.dependency
                });
            }

            snapshot.Seal();
            return snapshot;
        }

        private static bool TryCreatePlanFromSnapshot(
            ESAssetPackageBakeData data,
            ESAssetPackageResolutionSnapshot snapshot,
            out List<ExportPlanItem> plan,
            out string error)
        {
            plan = new List<ExportPlanItem>();
            error = string.Empty;
            if (snapshot == null || !snapshot.HasValidIntegrity())
            {
                error = "解析快照完整性校验失败。";
                return false;
            }
            if (!string.Equals(snapshot.packageId, data.packageId, StringComparison.Ordinal) ||
                !string.Equals(snapshot.definitionHash, data.ComputeExportConfigFingerprint(), StringComparison.OrdinalIgnoreCase))
            {
                error = "资产包身份或导出配置已变化，解析快照已失效。";
                return false;
            }

            foreach (ESAssetPackageResolutionItem item in snapshot.items)
            {
                if (item == null || !ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(item.sourcePath, out string sourcePath) ||
                    !ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(item.targetPath, out string targetPath))
                {
                    error = "解析快照包含无效路径。";
                    return false;
                }
                if (!string.Equals(AssetDatabase.AssetPathToGUID(sourcePath), item.sourceGuid, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(AssetDatabase.GetAssetDependencyHash(sourcePath).ToString(), item.sourceDependencyHash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ComputeAssetFileHash(sourcePath), item.sourceFileHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "源资产在确认后发生变化，请重新解析：" + sourcePath;
                    return false;
                }

                bool targetExists = AssetDatabase.LoadMainAssetAtPath(targetPath) != null;
                if (item.operation == ESAssetPackageExportOperation.Create && targetExists)
                {
                    error = "目标在确认后出现，拒绝隐式覆盖：" + targetPath;
                    return false;
                }
                if (item.operation == ESAssetPackageExportOperation.Update &&
                    (!targetExists ||
                     !string.Equals(AssetDatabase.AssetPathToGUID(targetPath), item.expectedTargetGuid, StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(ComputeAssetFileHash(targetPath), item.expectedTargetFileHash, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "目标在确认后发生变化，拒绝覆盖：" + targetPath;
                    return false;
                }

                plan.Add(new ExportPlanItem
                {
                    sourcePath = sourcePath,
                    targetPath = targetPath,
                    category = item.category,
                    rootSelected = item.rootSelected,
                    dependency = item.dependency,
                    overwrite = item.operation == ESAssetPackageExportOperation.Update,
                    reasonCode = item.reasonCode
                });
            }
            return true;
        }

        public static void Bake(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            Undo.RecordObject(data, "烘焙资产包记录");
            data.EnsureIdentity();
            data.EnsureCategoryFolderSettings();

            string folder = NormalizeAssetPath(data.targetFolderPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("资产包烘焙", "目标文件夹无效：" + folder, "确定");
                return;
            }

            var oldUseByGuid = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (data.records != null)
            {
                for (int i = 0; i < data.records.Count; i++)
                {
                    ESAssetPackageBakeRecord record = data.records[i];
                    if (record != null && !string.IsNullOrEmpty(record.guid))
                        oldUseByGuid[record.guid] = record.selectedForUse;
                }
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            data.records.Clear();
            var excluded = BuildBakeExcludePaths(data, folder);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;

                if (excluded.Any(root => IsPathInsideRoot(path, root)))
                    continue;

                if (!data.includeSubFolders && !string.Equals(Path.GetDirectoryName(path)?.Replace("\\", "/"), folder, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsOnlyEditorSOAsset(path))
                    continue;

                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                ESAssetPackageCategory category = DetermineCategory(path, type);
                string guid = AssetDatabase.AssetPathToGUID(path);

                data.records.Add(new ESAssetPackageBakeRecord
                {
                    selectedForUse = oldUseByGuid.TryGetValue(guid, out bool selected) && selected,
                    category = category,
                    assetName = Path.GetFileNameWithoutExtension(path),
                    assetPath = path,
                    guid = guid,
                    typeName = type != null ? type.Name : "Unknown",
                    fileSize = FormatFileSize(path),
                    exportSubFolder = data.GetConfiguredExportSubFolder(category)
                });
            }

            data.records.Sort((a, b) =>
            {
                int c = a.category.CompareTo(b.category);
                return c != 0 ? c : string.Compare(a.assetPath, b.assetPath, StringComparison.OrdinalIgnoreCase);
            });

            data.lastBakeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            data.contentHash = ComputeRecordsHash(data.records);
            data.RebuildStats();
            data.MarkAnalysisStale();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        public static void ExportSelectedAssetsByCategory(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            Undo.RecordObject(data, "导出资产包分类内容");
            data.EnsureCategoryFolderSettings();
            data.EnsureIdentity();
            if (!data.HasValidIdentity(out string identityError))
            {
                data.lastExportAttemptState = ESAssetPackageExportAttemptState.Blocked;
                data.lastExportAttemptMessage = identityError;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                EditorUtility.DisplayDialog("资源包导出已阻止", identityError, "确定");
                return;
            }
            if (!ValidateAnalysisPolicy(data))
                return;
            data.exportLinks ??= new List<ESAssetPackageExportLink>();
            data.exportSessions ??= new List<ESAssetPackageExportSession>();
            data.exportChainBySourceGuid ??= new Dictionary<string, ESAssetPackageExportChain>();
            SyncExportChainDictionary(data);

            if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(data.exportRootPath, out string exportRoot))
            {
                EditorUtility.DisplayDialog("资源包导出", "导出根目录必须位于 Assets 下。", "确定");
                return;
            }

            if (!ValidateExportRootForUse(exportRoot))
            {
                return;
            }

            if (HasUnresolvedExportTransactions(exportRoot, out string unresolvedTransactions))
            {
                data.lastExportAttemptState = ESAssetPackageExportAttemptState.Blocked;
                data.lastExportAttemptTime = DateTime.UtcNow.ToString("O");
                data.lastExportAttemptMessage = unresolvedTransactions;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                EditorUtility.DisplayDialog("资源包导出已阻止", unresolvedTransactions, "确定");
                return;
            }

            data.exportRootPath = exportRoot;
            data.exportFileNamePrefix = SanitizeFileNamePrefix(data.exportFileNamePrefix);
            string configFingerprint = data.ComputeExportConfigFingerprint();
            data.exportConfigFingerprint = configFingerprint;

            var resolvedFolders = data.ResolveExportFolders(data.targetFolderPath);
            var folderErrors = resolvedFolders.Where(x => !x.writable || x.overlapsSource || x.overlapsOtherCategory).ToList();
            if (folderErrors.Count > 0)
            {
                EditorUtility.DisplayDialog("资源包导出已阻止", "分类导出路径存在冲突或不可写：\n" + string.Join("\n", folderErrors.Select(x => x.category + " -> " + x.resolvedPath + " [" + x.collisionState + "]")), "确定");
                return;
            }

            if (!data.updateChangedExports && data.overwriteExistingExport)
                data.overwriteExistingExport = false;

            int skipped = 0;
            var errors = new List<string>();
            var exportPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependencyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var copiedPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rootSelectedByPath = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var record in data.records)
            {
                if (record == null || !record.selectedForUse || string.IsNullOrEmpty(record.assetPath))
                {
                    skipped++;
                    continue;
                }

                string rootPath = NormalizeAssetPath(record.assetPath);
                if (IsCodeOrEditorOnlyDependency(rootPath) || IsOnlyEditorSOAsset(rootPath))
                {
                    errors.Add("根资产属于代码或 EditorOnly 资产，禁止导出：" + rootPath);
                    continue;
                }
                if (AddExportPath(rootPath, exportRoot, exportPaths))
                {
                    rootPaths.Add(rootPath);
                    rootSelectedByPath[rootPath] = true;
                }

                if (!data.exportDependencies)
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(rootPath, true);
                for (int i = 0; i < dependencies.Length; i++)
                {
                    string dependency = NormalizeAssetPath(dependencies[i]);
                    if (string.Equals(dependency, rootPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IsCodeOrEditorOnlyDependency(dependency) || IsOnlyEditorSOAsset(dependency))
                    {
                        errors.Add("依赖闭包包含代码或 EditorOnly 资产，禁止提交不完整导出：" + dependency);
                        continue;
                    }

                    if (AddExportPath(dependency, exportRoot, exportPaths))
                        dependencyPaths.Add(dependency);
                }
            }

            if (exportPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("资源包导出", "没有可导出的已使用资源。", "确定");
                return;
            }

            var previousLinks = BuildExportLinkLookup(data.exportLinks);
            if (data.repairExportLinksOnExport)
                RepairExportLinks(data);
            previousLinks = BuildExportLinkLookup(data.exportLinks);
            var usedTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plan = new List<ExportPlanItem>();
            var duplicateSkipped = new List<string>();
            var categoryFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int created = 0;
            int updated = 0;

            foreach (string sourcePath in exportPaths.OrderBy(p => rootPaths.Contains(p) ? 0 : 1).ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                Type type = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
                ESAssetPackageCategory category = DetermineCategory(sourcePath, type);
                string categoryFolder = data.GetConfiguredExportFolder(exportRoot, category);
                if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(categoryFolder, out categoryFolder)
                    || ESAssetPackagePathSafety.IsForbiddenExportFolder(categoryFolder)
                    || ESAssetPackagePathSafety.HasReparsePointInPath(categoryFolder)
                    || ((IsPathInsideRoot(categoryFolder, NormalizeAssetPath(data.targetFolderPath))
                        || IsPathInsideRoot(NormalizeAssetPath(data.targetFolderPath), categoryFolder))
                        && !ESAssetPackagePathSafety.IsAllowedExportOverlap(data.targetFolderPath, exportRoot, categoryFolder)))
                {
                    errors.Add("分类导出路径安全校验失败：" + category + " -> " + categoryFolder);
                    continue;
                }
                categoryFolders.Add(categoryFolder);

                string targetPath = ResolveExportTargetPath(
                    sourcePath,
                    categoryFolder,
                    data.exportFileNamePrefix,
                    data.packageId,
                    previousLinks,
                    usedTargetPaths,
                    data.overwriteExistingExport,
                    data.updateChangedExports,
                    data.reexportWhenConfigChanges,
                    configFingerprint,
                    out bool overwrite,
                    out string skipReason);

                if (string.IsNullOrEmpty(targetPath))
                {
                    duplicateSkipped.Add(string.IsNullOrEmpty(skipReason) ? sourcePath : sourcePath + "  ->  " + skipReason);
                    continue;
                }

                plan.Add(new ExportPlanItem
                {
                    sourcePath = sourcePath,
                    targetPath = targetPath,
                    category = category,
                    rootSelected = rootPaths.Contains(sourcePath),
                    dependency = dependencyPaths.Contains(sourcePath),
                    overwrite = overwrite
                });
            }

            if (errors.Count > 0)
            {
                data.lastExportAttemptState = ESAssetPackageExportAttemptState.Blocked;
                data.lastExportAttemptSessionId = sessionId;
                data.lastExportAttemptTime = exportTime;
                data.lastExportAttemptMessage = string.Join("\n", errors.Take(20));
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                EditorUtility.DisplayDialog("资源包导出已阻止", string.Join("\n", errors.Take(20)), "确定");
                return;
            }

            if (plan.Count == 0)
            {
                string duplicateText = duplicateSkipped.Count > 0 ? "\n\n重复/冲突项:\n" + string.Join("\n", duplicateSkipped.Take(20)) : string.Empty;
                EditorUtility.DisplayDialog("资源包导出", "没有需要新导出的资源。默认不重复导出已有有效链路。" + duplicateText, "确定");
                return;
            }

            ESAssetPackageResolutionSnapshot snapshot = BuildResolutionSnapshot(data, plan, configFingerprint);
            data.currentResolutionSnapshot = snapshot;
            data.lastExportAttemptState = ESAssetPackageExportAttemptState.NeedsReview;
            data.lastExportAttemptMessage = "解析快照已生成，等待人工确认。";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            data.lastExportAttemptState = ESAssetPackageExportAttemptState.AwaitingConfirmation;
            data.lastExportAttemptSessionId = sessionId;
            data.lastExportAttemptTime = exportTime;
            data.lastExportAttemptMessage = "导出计划已生成，等待用户确认。";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            if (!DisplayExportPreflight(data, exportRoot, plan, rootPaths, dependencyPaths, duplicateSkipped))
            {
                data.lastExportAttemptState = ESAssetPackageExportAttemptState.Cancelled;
                data.lastExportAttemptMessage = "用户取消了本次导出，未修改目标资产。";
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                return;
            }

            if (!TryCreatePlanFromSnapshot(data, snapshot, out plan, out string snapshotError))
            {
                data.lastExportAttemptState = ESAssetPackageExportAttemptState.Stale;
                data.lastExportAttemptMessage = snapshotError;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                EditorUtility.DisplayDialog("资产包导出已阻止", snapshotError, "确定");
                return;
            }



            EnsureAssetFolder(exportRoot);
            foreach (string folder in categoryFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                EnsureAssetFolder(folder);

            string transactionRoot = NormalizeAssetPath($"{exportRoot}/.ESBakeTransactions/{sessionId}");
            if (AssetDatabase.IsValidFolder(transactionRoot))
            {
                data.lastExportAttemptState = ESAssetPackageExportAttemptState.Blocked;
                data.lastExportAttemptSessionId = sessionId;
                data.lastExportAttemptTime = exportTime;
                data.lastExportAttemptMessage = "事务目录已存在，拒绝复用以避免覆盖其他导出会话：" + transactionRoot;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                EditorUtility.DisplayDialog("资源包导出已阻止", data.lastExportAttemptMessage, "确定");
                return;
            }
            data.lastExportAttemptState = ESAssetPackageExportAttemptState.Staging;
            data.lastExportAttemptSessionId = sessionId;
            data.lastExportAttemptTime = exportTime;
            data.lastExportAttemptMessage = "事务正在准备暂存、备份和提交。若 Unity 中途重载，下次打开窗口时请优先检查事务状态。";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            if (!TryCommitExportTransaction(
                    transactionRoot,
                    plan,
                    out copiedPathMap,
                    out created,
                    out updated,
                    out string transactionError,
                    out ESAssetPackageRollbackState rollbackState))
            {
                errors.Add("事务导出失败：" + transactionError);
                data.lastExportAttemptState = rollbackState == ESAssetPackageRollbackState.Partial
                    ? ESAssetPackageExportAttemptState.RollbackPartial
                    : ESAssetPackageExportAttemptState.Failed;
                data.lastExportAttemptMessage = transactionError;
                data.exportSessions.Add(new ESAssetPackageExportSession
                {
                    sessionId = sessionId,
                    packageId = data.packageId,
                    resolutionSnapshotHash = data.currentResolutionSnapshot != null ? data.currentResolutionSnapshot.snapshotHash : string.Empty,
                    configName = string.IsNullOrWhiteSpace(data.exportConfigName) ? data.displayName : data.exportConfigName,
                    exportTime = exportTime,
                    exportRootPath = exportRoot,
                    selectedRootCount = rootPaths.Count,
                    totalAssetCount = copiedPathMap.Count,
                    dependencyAssetCount = copiedPathMap.Keys.Count(path => dependencyPaths.Contains(NormalizeAssetPath(path))),
                    createdCount = created,
                    updatedCount = updated,
                    errorCount = errors.Count,
                    transactionState = data.lastExportAttemptState,
                    transactionWarning = transactionError,
                    rollbackState = rollbackState,
                    targetAssetPaths = copiedPathMap.Values.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                    targetAssetGuids = copiedPathMap.Values.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(AssetDatabase.AssetPathToGUID).ToList(),
                    targetAssetFileHashes = copiedPathMap.Values.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(ComputeAssetFileHash).ToList(),
                    sourceAssetGuids = copiedPathMap.OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase).Select(pair => AssetDatabase.AssetPathToGUID(pair.Key)).ToList(),
                    errorAssetPaths = errors.ToList()
                });
                TrimExportSessions(data.exportSessions, 30);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                EditorUtility.DisplayDialog(
                    "资源包导出失败",
                    (rollbackState == ESAssetPackageRollbackState.Partial
                        ? "导出事务未提交，回退不完整；部分目标可能已保留或被外部修改，必须人工复核。\n\n"
                        : "导出事务未提交，已尝试恢复原有目标。\n\n") + transactionError,
                    "确定");
                return;
            }

            int remapped = data.remapExportedGuids ? RemapCopiedAssetGuids(copiedPathMap) : 0;
            AssetDatabase.Refresh();
            UpdateExportLinks(data, copiedPathMap, rootSelectedByPath, sessionId, exportTime);
            SyncExportChainDictionary(data);
            MarkRecordsWithValidExportLinksAsSelected(data);
            int copiedDependencyCount = copiedPathMap.Keys.Count(path => dependencyPaths.Contains(NormalizeAssetPath(path)));

            var session = new ESAssetPackageExportSession
            {
                sessionId = sessionId,
                packageId = data.packageId,
                resolutionSnapshotHash = data.currentResolutionSnapshot != null ? data.currentResolutionSnapshot.snapshotHash : string.Empty,
                configName = string.IsNullOrWhiteSpace(data.exportConfigName) ? data.displayName : data.exportConfigName,
                exportTime = exportTime,
                exportRootPath = exportRoot,
                selectedRootCount = rootPaths.Count,
                totalAssetCount = copiedPathMap.Count,
                dependencyAssetCount = copiedDependencyCount,
                createdCount = created,
                updatedCount = updated,
                remappedFileCount = remapped,
                errorCount = errors.Count,
                transactionState = ESAssetPackageExportAttemptState.Committed,
                transactionWarning = transactionError,
                rollbackState = ESAssetPackageRollbackState.NotRequired,
                duplicateSkippedCount = duplicateSkipped.Count,
                targetAssetPaths = copiedPathMap.Values.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                targetAssetGuids = copiedPathMap.Values
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(AssetDatabase.AssetPathToGUID)
                    .ToList(),
                targetAssetFileHashes = copiedPathMap.Values
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(ComputeAssetFileHash)
                    .ToList(),
                sourceAssetGuids = copiedPathMap
                    .OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => AssetDatabase.AssetPathToGUID(pair.Key))
                    .ToList(),
                dependencyAssetPaths = dependencyPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                duplicateSkippedSourcePaths = duplicateSkipped.ToList(),
                errorAssetPaths = errors.ToList()
            };
            data.exportSessions.Add(session);
            TrimExportSessions(data.exportSessions, 30);

            data.lastExportTime = exportTime;
            data.lastExportRootPath = exportRoot;
            data.lastExportAssetCount = copiedPathMap.Count;
            data.lastExportDependencyCount = session.dependencyAssetCount;
            data.lastExportAttemptState = string.IsNullOrEmpty(transactionError) ? ESAssetPackageExportAttemptState.Committed : ESAssetPackageExportAttemptState.CommittedWithWarning;
            data.lastExportAttemptMessage = string.IsNullOrEmpty(transactionError)
                ? "导出事务已提交，目标和会话记录已刷新。"
                : transactionError;
            data.RebuildStats();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            string message = $"导出完成。\n新增: {created}\n更新: {updated}\n总导出: {copiedPathMap.Count}\n其中依赖: {session.dependencyAssetCount}\n重映射文件: {remapped}\n未选跳过: {skipped}\n重复/冲突跳过: {duplicateSkipped.Count}\n失败: {errors.Count}\n\n导出目录:\n{exportRoot}\n命名前缀:\n{data.exportFileNamePrefix}\n会话:\n{sessionId}";
            if (duplicateSkipped.Count > 0)
                message += "\n\n重复/冲突项:\n" + string.Join("\n", duplicateSkipped.Take(8));
            if (errors.Count > 0)
                message += "\n\n失败项:\n" + string.Join("\n", errors.Take(8));
            if (!string.IsNullOrEmpty(transactionError))
                message += "\n\n事务警告:\n" + transactionError;

            EditorUtility.DisplayDialog("资源包导出", message, "确定");
            UnityEngine.Object folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(exportRoot);
            if (folderAsset != null)
                EditorGUIUtility.PingObject(folderAsset);
        }

        private static bool TryCommitExportTransaction(
            string transactionRoot,
            List<ExportPlanItem> plan,
            out Dictionary<string, string> copiedPathMap,
            out int created,
            out int updated,
            out string transactionError,
            out ESAssetPackageRollbackState rollbackState)
        {
            copiedPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            created = 0;
            updated = 0;
            transactionError = string.Empty;
            rollbackState = ESAssetPackageRollbackState.NotRequired;
            var items = new List<ExportTransactionItem>(plan?.Count ?? 0);
            bool editing = false;

            try
            {
                if (string.IsNullOrWhiteSpace(transactionRoot) || !transactionRoot.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("事务目录必须位于 Assets 下。");
                if (AssetDatabase.IsValidFolder(transactionRoot))
                    throw new IOException("事务目录已存在，拒绝复用旧事务：" + transactionRoot);

                string stagedRoot = NormalizeAssetPath(transactionRoot + "/Staged");
                string backupRoot = NormalizeAssetPath(transactionRoot + "/Backup");
                EnsureAssetFolder(stagedRoot);
                EnsureAssetFolder(backupRoot);

                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int index = 0; index < plan.Count; index++)
                {
                    ExportPlanItem exportPlan = plan[index];
                    if (exportPlan == null || string.IsNullOrEmpty(exportPlan.sourcePath) || string.IsNullOrEmpty(exportPlan.targetPath))
                        throw new InvalidDataException("导出事务包含空的源或目标路径。");
                    if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(exportPlan.sourcePath, out string normalizedSource)
                        || !ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(exportPlan.targetPath, out string normalizedTarget)
                        || ESAssetPackagePathSafety.IsForbiddenExportFolder(Path.GetDirectoryName(normalizedTarget)?.Replace('\\', '/') ?? string.Empty))
                        throw new InvalidDataException("导出事务包含不安全路径：" + exportPlan.targetPath);
                    exportPlan.sourcePath = normalizedSource;
                    exportPlan.targetPath = normalizedTarget;
                    if (AssetDatabase.LoadMainAssetAtPath(exportPlan.sourcePath) == null)
                        throw new FileNotFoundException("导出源资产不存在：" + exportPlan.sourcePath);

                    bool existedBefore = AssetDatabase.LoadMainAssetAtPath(exportPlan.targetPath) != null;
                    if (existedBefore && !exportPlan.overwrite)
                        throw new IOException("目标在事务阶段已存在且不允许覆盖：" + exportPlan.targetPath);

                    var item = new ExportTransactionItem
                    {
                        plan = exportPlan,
                        hadExistingTarget = existedBefore,
                        stagedPath = BuildTransactionAssetPath(stagedRoot, index, exportPlan.targetPath),
                        backupPath = existedBefore ? BuildTransactionAssetPath(backupRoot, index, exportPlan.targetPath) : string.Empty,
                        expectedTargetGuid = existedBefore ? AssetDatabase.AssetPathToGUID(exportPlan.targetPath) : string.Empty,
                        expectedTargetFileHash = existedBefore ? ComputeAssetFileHash(exportPlan.targetPath) : string.Empty
                    };

                    // Register the item before either copy starts. If backup succeeds
                    // but staging fails, the rollback state still knows this item and
                    // can retain the transaction evidence for recovery.
                    items.Add(item);
                    if (existedBefore && !AssetDatabase.CopyAsset(exportPlan.targetPath, item.backupPath))
                        throw new IOException("无法备份已有目标，事务已中止：" + exportPlan.targetPath);
                    if (!AssetDatabase.CopyAsset(exportPlan.sourcePath, item.stagedPath))
                        throw new IOException("无法生成暂存资产，事务已中止：" + exportPlan.sourcePath);
                }

                AssetDatabase.StopAssetEditing();
                editing = false;
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                VerifyTransactionAssets(items);

                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int index = 0; index < items.Count; index++)
                {
                    ExportTransactionItem item = items[index];
                    if (string.IsNullOrEmpty(item.expectedStagedFileHash)
                        || !string.Equals(
                            ComputeAssetFileHash(item.stagedPath),
                            item.expectedStagedFileHash,
                            StringComparison.OrdinalIgnoreCase))
                        throw new IOException("提交阶段发现暂存资产已被外部修改，拒绝覆盖目标：" + item.plan.targetPath);
                    bool targetExistsNow = AssetDatabase.LoadMainAssetAtPath(item.plan.targetPath) != null;
                    if (targetExistsNow && !item.plan.overwrite)
                        throw new IOException("提交阶段发现新的目标冲突：" + item.plan.targetPath);
                    bool externalTargetChanged = item.hadExistingTarget
                        && (!string.Equals(item.expectedTargetGuid, AssetDatabase.AssetPathToGUID(item.plan.targetPath), StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(item.expectedTargetFileHash, ComputeAssetFileHash(item.plan.targetPath), StringComparison.OrdinalIgnoreCase));
                    if (targetExistsNow && item.plan.overwrite && externalTargetChanged)
                        throw new IOException("提交阶段发现目标已被外部替换，拒绝覆盖：" + item.plan.targetPath);
                    if (targetExistsNow && !AssetDatabase.DeleteAsset(item.plan.targetPath))
                        throw new IOException("无法删除待替换目标，事务已中止：" + item.plan.targetPath);

                    string moveError = AssetDatabase.MoveAsset(item.stagedPath, item.plan.targetPath);
                    if (!string.IsNullOrEmpty(moveError))
                        throw new IOException("暂存资产提交失败：" + item.plan.targetPath + "，" + moveError);

                    item.committed = true;
                    item.committedTargetGuid = AssetDatabase.AssetPathToGUID(item.plan.targetPath);
                    item.committedTargetFileHash = ComputeAssetFileHash(item.plan.targetPath);
                    copiedPathMap[item.plan.sourcePath] = item.plan.targetPath;
                    if (item.hadExistingTarget) updated++;
                    else created++;
                }

                AssetDatabase.StopAssetEditing();
                editing = false;
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                if (!AssetDatabase.DeleteAsset(transactionRoot))
                    transactionError = "导出已提交，但事务目录清理失败，已保留供恢复：" + transactionRoot;
                else
                    RemoveEmptyFolders(NormalizeAssetPath(transactionRoot.Substring(0, transactionRoot.LastIndexOf('/'))));
                return true;
            }
            catch (Exception exception)
            {
                if (editing)
                {
                    AssetDatabase.StopAssetEditing();
                    editing = false;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                string rollbackError = RollbackExportTransaction(items, transactionRoot, out rollbackState);
                transactionError = exception.Message + (string.IsNullOrEmpty(rollbackError) ? string.Empty : "\n回滚异常：" + rollbackError);
                return false;
            }
            finally
            {
                if (editing)
                    AssetDatabase.StopAssetEditing();
            }
        }


        private static void VerifyTransactionAssets(List<ExportTransactionItem> items)
        {
            for (int index = 0; index < items.Count; index++)
            {
                ExportTransactionItem item = items[index];
                if (AssetDatabase.LoadMainAssetAtPath(item.stagedPath) == null)
                    throw new IOException("暂存资产刷新后不可见：" + item.stagedPath);
                if (item.hadExistingTarget && AssetDatabase.LoadMainAssetAtPath(item.backupPath) == null)
                    throw new IOException("已有目标备份刷新后不可见：" + item.backupPath);
                item.expectedStagedFileHash = ComputeAssetFileHash(item.stagedPath);
                if (string.IsNullOrEmpty(item.expectedStagedFileHash))
                    throw new IOException("暂存资产没有可验证的文件 Hash：" + item.stagedPath);
            }
        }

        private static string RollbackExportTransaction(
            List<ExportTransactionItem> items,
            string transactionRoot,
            out ESAssetPackageRollbackState rollbackState)
        {
            var rollbackErrors = new List<string>();
            rollbackState = ESAssetPackageRollbackState.Complete;
            bool editing = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int index = items.Count - 1; index >= 0; index--)
                {
                    ExportTransactionItem item = items[index];
                    if (item.committed && AssetDatabase.LoadMainAssetAtPath(item.plan.targetPath) != null)
                    {
                        string currentGuid = AssetDatabase.AssetPathToGUID(item.plan.targetPath);
                        string currentHash = ComputeAssetFileHash(item.plan.targetPath);
                        bool stillOwned = string.Equals(currentGuid, item.committedTargetGuid, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(currentHash, item.committedTargetFileHash, StringComparison.OrdinalIgnoreCase);
                        if (!stillOwned)
                        {
                            rollbackErrors.Add("已跳过被外部修改的提交目标：" + item.plan.targetPath);
                            continue;
                        }
                        if (!AssetDatabase.DeleteAsset(item.plan.targetPath))
                            rollbackErrors.Add("无法删除已提交目标：" + item.plan.targetPath);
                    }

                    if (item.hadExistingTarget && AssetDatabase.LoadMainAssetAtPath(item.backupPath) != null)
                    {
                        if (!string.IsNullOrEmpty(item.expectedTargetFileHash)
                            && !string.Equals(
                                ComputeAssetFileHash(item.backupPath),
                                item.expectedTargetFileHash,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            rollbackErrors.Add("已跳过被修改的原目标备份：" + item.backupPath);
                            continue;
                        }
                        string moveError = AssetDatabase.MoveAsset(item.backupPath, item.plan.targetPath);
                        if (!string.IsNullOrEmpty(moveError))
                            rollbackErrors.Add("无法恢复原目标：" + item.plan.targetPath + "，" + moveError);
                    }
                }
            }
            catch (Exception exception)
            {
                rollbackErrors.Add(exception.Message);
            }
            finally
            {
                if (editing)
                    AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            if (rollbackErrors.Count == 0 && AssetDatabase.IsValidFolder(transactionRoot)
                && !AssetDatabase.DeleteAsset(transactionRoot))
            {
                rollbackErrors.Add("无法清理事务目录：" + transactionRoot);
            }
            if (rollbackErrors.Count > 0)
                rollbackState = ESAssetPackageRollbackState.Partial;
            return string.Join("；", rollbackErrors);
        }

        private static string BuildTransactionAssetPath(string root, int index, string targetPath)
        {
            string name = Path.GetFileNameWithoutExtension(targetPath);
            string extension = Path.GetExtension(targetPath);
            string safeName = SanitizeFileNamePrefix(name).TrimEnd('_');
            if (string.IsNullOrEmpty(safeName)) safeName = "Asset";
            return NormalizeAssetPath($"{root}/item_{index:D5}_{safeName}{extension}");
        }

        public static void RollbackLastExport(ESAssetPackageBakeData data)
        {
            if (data == null || data.exportSessions == null || data.exportSessions.Count == 0)
            {
                EditorUtility.DisplayDialog("资产包导出回退", "没有可回退的导出会话。", "确定");
                return;
            }

            ESAssetPackageExportSession session = data.exportSessions
                .AsEnumerable()
                .Reverse()
                .FirstOrDefault(item => item != null &&
                    (item.transactionState == ESAssetPackageExportAttemptState.Committed ||
                     item.transactionState == ESAssetPackageExportAttemptState.CommittedWithWarning ||
                     item.transactionState == ESAssetPackageExportAttemptState.RollbackPartial));
            if (session == null)
            {
                EditorUtility.DisplayDialog("资产包导出回退", "没有可回退的已提交导出会话。", "确定");
                return;
            }
            string exportRoot = NormalizeAssetPath(session.exportRootPath);
            if (string.IsNullOrEmpty(exportRoot) || !exportRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("资产包导出回退", "最近导出会话的根目录无效，已拒绝回退。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "资产包导出回退",
                    $"将删除最近一次导出的 {session.targetAssetPaths?.Count ?? 0} 个目标资源。\n\n导出根目录:\n{exportRoot}\n会话:\n{session.sessionId}\n\n此操作只处理导出链路记录中的目标路径。",
                    "确认回退",
                    "取消"))
                return;

            int sessionIndex = data.exportSessions.LastIndexOf(session);
            if (sessionIndex < 0)
            {
                EditorUtility.DisplayDialog("资产包导出回退", "导出会话已发生变化，拒绝回退。", "确定");
                return;
            }
            Undo.RecordObject(data, "回退资产包导出");
            int deleted = 0;
            int missing = 0;
            int changed = 0;
            List<string> recordedTargetPaths = session.targetAssetPaths != null
                ? session.targetAssetPaths.Select(NormalizeAssetPath).ToList()
                : new List<string>();
            var recordedGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (session.targetAssetGuids != null)
            {
                for (int index = 0; index < recordedTargetPaths.Count && index < session.targetAssetGuids.Count; index++)
                    recordedGuids[recordedTargetPaths[index]] = session.targetAssetGuids[index];
            }
            var recordedHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (session.targetAssetFileHashes != null)
            {
                for (int index = 0; index < recordedTargetPaths.Count && index < session.targetAssetFileHashes.Count; index++)
                    recordedHashes[recordedTargetPaths[index]] = session.targetAssetFileHashes[index];
            }
            List<string> targets = recordedTargetPaths.OrderByDescending(p => p.Length).ToList();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string rawPath in targets)
                {
                    string targetPath = NormalizeAssetPath(rawPath);
                    if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(targetPath, out targetPath))
                    {
                        changed++;
                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) == null)
                    {
                        missing++;
                        continue;
                    }

                    if (recordedGuids.TryGetValue(targetPath, out string expectedGuid)
                        && !string.IsNullOrEmpty(expectedGuid)
                        && !string.Equals(AssetDatabase.AssetPathToGUID(targetPath), expectedGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        changed++;
                        continue;
                    }

                    if (recordedHashes.TryGetValue(targetPath, out string expectedHash)
                        && !string.IsNullOrEmpty(expectedHash)
                        && !string.Equals(ComputeAssetFileHash(targetPath), expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        changed++;
                        continue;
                    }

                    if (AssetDatabase.DeleteAsset(targetPath))
                        deleted++;
                    else
                        changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            bool partialRollback = changed > 0;
            session.rollbackState = partialRollback
                ? ESAssetPackageRollbackState.Partial
                : ESAssetPackageRollbackState.Complete;
            session.transactionState = partialRollback
                ? ESAssetPackageExportAttemptState.RollbackPartial
                : ESAssetPackageExportAttemptState.Committed;
            if (data.exportLinks != null)
            {
                var targetSet = new HashSet<string>(targets.Select(NormalizeAssetPath), StringComparer.OrdinalIgnoreCase);
                data.exportLinks.RemoveAll(link =>
                    link != null &&
                    !partialRollback &&
                    string.Equals(link.packageId, data.packageId, StringComparison.Ordinal) &&
                    (string.Equals(link.lastExportSessionId, session.sessionId, StringComparison.OrdinalIgnoreCase) ||
                     targetSet.Contains(NormalizeAssetPath(link.targetAssetPath))));
            }

            SyncExportChainDictionary(data);

            if (!partialRollback)
                data.exportSessions.RemoveAt(sessionIndex);
            else
                session.transactionState = ESAssetPackageExportAttemptState.RollbackPartial;
            ESAssetPackageExportSession lastCommittedSession = data.exportSessions
                .AsEnumerable()
                .Reverse()
                .FirstOrDefault(item => item != null &&
                    (item.transactionState == ESAssetPackageExportAttemptState.Committed ||
                     item.transactionState == ESAssetPackageExportAttemptState.CommittedWithWarning ||
                     item.transactionState == ESAssetPackageExportAttemptState.RollbackPartial));
            data.lastExportTime = lastCommittedSession != null ? lastCommittedSession.exportTime : string.Empty;
            data.lastExportRootPath = lastCommittedSession != null ? lastCommittedSession.exportRootPath : string.Empty;
            data.lastExportAssetCount = lastCommittedSession != null ? lastCommittedSession.totalAssetCount : 0;
            data.lastExportDependencyCount = lastCommittedSession != null ? lastCommittedSession.dependencyAssetCount : 0;

            // Only prune the transaction namespace created by ES. Pruning the
            // whole export root could delete unrelated user-created empty folders.
            RemoveEmptyFolders(NormalizeAssetPath(exportRoot + "/.ESBakeTransactions"));
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("资产包导出回退", $"回退完成。\n删除: {deleted}\n已不存在: {missing}\n已被修改而跳过: {changed}\n状态: {(partialRollback ? "部分回退，保留会话与链路供人工处理" : "完整回退")}", "确定");
        }

        private static bool AddExportPath(string path, string exportRoot, HashSet<string> exportPaths)
        {
            path = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.StartsWith(exportRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.IsValidFolder(path) ||
                IsCodeOrEditorOnlyDependency(path))
                return false;

            if (IsOnlyEditorSOAsset(path))
                return false;

            return exportPaths.Add(path);
        }

        private static bool ValidateExportRootForUse(string exportRoot)
        {
            if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(exportRoot, out exportRoot)
                || ESAssetPackagePathSafety.IsForbiddenExportFolder(exportRoot)
                || ESAssetPackagePathSafety.HasReparsePointInPath(exportRoot))
            {
                EditorUtility.DisplayDialog(
                    "资源包导出已阻止",
                    "导出根目录不是安全的 Assets 项目目录，或位于 Unity 保留/编辑器目录、重解析路径下。\n\n" +
                    "当前目录:\n" + exportRoot + "\n\n" +
                    "禁止目录示例:\n" +
                    "- Assets/Resources\n" +
                    "- Assets/Editor Default Resources\n" +
                    "- Assets/Editor 或任意 /Editor/ 子目录",
                    "确定");
                return false;
            }

            if (IsPathInsideRoot(exportRoot, "Assets/StreamingAssets"))
            {
                return EditorUtility.DisplayDialog(
                    "确认导出到资源目录",
                    "当前导出根目录会进入资源管理或构建链路。\n\n" +
                    "当前目录:\n" + exportRoot + "\n\n" +
                    "这适合正式分离选用资源；如果只是临时预览，建议使用默认目录:\n" +
                    "Assets/_ESAssetPackageExport\n\n" +
                    "是否继续复制勾选资产？",
                    "继续导出",
                    "取消");
            }

            return true;
        }

        private static bool HasUnresolvedExportTransactions(string exportRoot, out string message)
        {
            message = string.Empty;
            if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(exportRoot, out string normalizedRoot))
                return false;

            string transactionRoot = NormalizeAssetPath(normalizedRoot + "/.ESBakeTransactions");
            if (!AssetDatabase.IsValidFolder(transactionRoot))
                return false;

            string[] sessions = AssetDatabase.GetSubFolders(transactionRoot) ?? Array.Empty<string>();
            sessions = sessions
                .Select(NormalizeAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (sessions.Length == 0)
                return false;

            message = "发现尚未完成或未复核的资产包导出事务，已拒绝继续导出。\n\n" +
                      "请先检查事务目录中的 Staged/Backup 与最近导出状态，确认恢复或人工清理后再重试。\n\n" +
                      "事务目录:\n" + string.Join("\n", sessions.Take(8));
            if (sessions.Length > 8)
                message += "\n…其余 " + (sessions.Length - 8) + " 个事务目录未展开。";
            return true;
        }

        private static Dictionary<string, ESAssetPackageExportLink> BuildExportLinkLookup(List<ESAssetPackageExportLink> links)
        {
            var result = new Dictionary<string, ESAssetPackageExportLink>(StringComparer.OrdinalIgnoreCase);
            if (links == null)
                return result;

            for (int i = 0; i < links.Count; i++)
            {
                ESAssetPackageExportLink link = links[i];
                if (link == null)
                    continue;

                if (!string.IsNullOrEmpty(link.sourceGuid))
                    result[link.sourceGuid] = link;
                if (!string.IsNullOrEmpty(link.sourceAssetPath))
                    result[NormalizeAssetPath(link.sourceAssetPath)] = link;
            }

            return result;
        }

        private static bool IsTargetIdentityCompatible(
            ESAssetPackageExportLink link,
            string packageId,
            string sourceGuid,
            string targetPath)
        {
            if (link == null || string.IsNullOrEmpty(targetPath))
                return false;
            if (string.IsNullOrWhiteSpace(packageId) ||
                !string.Equals(link.packageId, packageId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(sourceGuid) ||
                !string.Equals(link.sourceGuid, sourceGuid, StringComparison.OrdinalIgnoreCase))
                return false;
            string actualGuid = AssetDatabase.AssetPathToGUID(targetPath);
            bool hasIdentity = !string.IsNullOrEmpty(link.targetGuid) || !string.IsNullOrEmpty(link.targetFileHash);
            if (!hasIdentity)
                return false;
            if (!string.IsNullOrEmpty(link.targetGuid) && !string.Equals(actualGuid, link.targetGuid, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(link.targetFileHash))
            {
                string actualHash = ComputeAssetFileHash(targetPath);
                if (!string.Equals(actualHash, link.targetFileHash, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private static bool IsTargetOwnedByPackage(ESAssetPackageExportLink link, string packageId, string targetPath)
        {
            return link != null
                && string.Equals(link.packageId, packageId, StringComparison.Ordinal)
                && IsTargetIdentityCompatible(link, packageId, link.sourceGuid, targetPath);
        }

        private static bool ValidateAnalysisPolicy(ESAssetPackageBakeData data)
        {
            ESAssetPackageExportPolicy policy = data.exportPolicy ?? new ESAssetPackageExportPolicy();
            if (policy.requireCurrentAnalysis)
            {
                string currentHash = ComputeRecordsHash(data.records);
                if (data.analysisData == null || !data.analysisData.IsCurrent(currentHash))
                {
                    EditorUtility.DisplayDialog("导出已阻止", "导出策略要求最新 AI 资产分析；请先重新分析当前资产包。", "确定");
                    return false;
                }
            }

            if (data.analysisData == null || data.analysisData.records == null)
                return true;

            var blocked = new List<string>();
            foreach (var record in data.analysisData.records)
            {
                if (record == null || !IsRecordSelected(data, record.guid, record.assetPath))
                    continue;
                if (!policy.allowNeedsReview && record.state == ESAssetPackageAnalysisState.NeedsReview)
                    blocked.Add(record.assetPath + "：待人工确认");
                if (!policy.allowCustomScripts && record.customScriptCount > 0)
                    blocked.Add(record.assetPath + "：包含自定义脚本");
                if (!policy.allowLoopingEffects && record.looping)
                    blocked.Add(record.assetPath + "：循环特效未获允许");
                if (!policy.allowMaterialRisk && record.risks != null && record.risks.Any(x => x != null && x.Contains("材质")))
                    blocked.Add(record.assetPath + "：材质风险");
            }
            if (blocked.Count == 0)
                return true;

            EditorUtility.DisplayDialog("导出已阻止", "当前分析策略阻断以下资产：\n" + string.Join("\n", blocked.Take(20)), "确定");
            return false;
        }

        private static bool IsRecordSelected(ESAssetPackageBakeData data, string guid, string path)
        {
            if (data.records == null) return false;
            return data.records.Any(x => x != null && ((string.IsNullOrEmpty(guid) || string.Equals(x.guid, guid, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(path) && string.Equals(NormalizeAssetPath(x.assetPath), NormalizeAssetPath(path), StringComparison.OrdinalIgnoreCase))) && x.selectedForUse);
        }

        private static string ComputeAssetFileHash(string assetPath)
        {
            string fullPath = AssetPathToFullPath(assetPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return string.Empty;
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(fullPath))
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
            catch { return string.Empty; }
        }

        private static string ComputeRecordsHash(List<ESAssetPackageBakeRecord> records)
        {
            string text = string.Join("\n", (records ?? new List<ESAssetPackageBakeRecord>())
                .Where(x => x != null)
                .OrderBy(x => x.guid, StringComparer.OrdinalIgnoreCase)
                .Select(x => (x.guid ?? string.Empty) + "|" + (x.assetPath ?? string.Empty) + "|" + ComputeAssetFileHash(x.assetPath)));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
        }

        private static List<string> BuildBakeExcludePaths(ESAssetPackageBakeData data, string sourceFolder)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NormalizeAssetPath(data.exportRootPath) };
            foreach (var resolved in data.ResolveExportFolders(sourceFolder))
                result.Add(NormalizeAssetPath(resolved.resolvedPath));
            if (data.excludedFolders != null)
                foreach (string path in data.excludedFolders)
                    if (!string.IsNullOrWhiteSpace(path)) result.Add(NormalizeAssetPath(path));
            return result.Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        private static string ResolveExportTargetPath(
            string sourcePath,
            string categoryFolder,
            string fileNamePrefix,
            string packageId,
            Dictionary<string, ESAssetPackageExportLink> previousLinks,
            HashSet<string> usedTargetPaths,
            bool overwriteExistingExport,
            bool updateChangedExports,
            bool reexportWhenConfigChanges,
            string configFingerprint,
            out bool overwrite,
            out string skipReason)
        {
            overwrite = false;
            skipReason = string.Empty;
            sourcePath = NormalizeAssetPath(sourcePath);
            categoryFolder = NormalizeAssetPath(categoryFolder);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string currentSourceHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            ESAssetPackageExportLink oldLink = null;
            if (!string.IsNullOrEmpty(sourceGuid))
                previousLinks?.TryGetValue(sourceGuid, out oldLink);
            if (oldLink == null)
                previousLinks?.TryGetValue(sourcePath, out oldLink);

            string oldTarget = NormalizeAssetPath(oldLink?.targetAssetPath);
            bool oldTargetValid = !string.IsNullOrEmpty(oldTarget) &&
                                  oldTarget.StartsWith("Assets/", StringComparison.Ordinal) &&
                                  IsPathInsideRoot(oldTarget, categoryFolder);
            bool oldTargetExists = oldTargetValid && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(oldTarget) != null;
            bool configChanged = oldLink != null && !string.IsNullOrEmpty(oldLink.exportConfigFingerprint) &&
                                 !string.Equals(oldLink.exportConfigFingerprint, configFingerprint, StringComparison.OrdinalIgnoreCase);
            if (oldTargetExists && !overwriteExistingExport)
            {
                if (configChanged && reexportWhenConfigChanges && IsTargetIdentityCompatible(oldLink, packageId, sourceGuid, oldTarget))
                {
                    usedTargetPaths.Add(oldTarget);
                    overwrite = true;
                    return oldTarget;
                }
                if (updateChangedExports && oldLink != null &&
                    !string.Equals(oldLink.sourceDependencyHash, currentSourceHash, StringComparison.OrdinalIgnoreCase) &&
                    IsTargetIdentityCompatible(oldLink, packageId, sourceGuid, oldTarget))
                {
                    usedTargetPaths.Add(oldTarget);
                    overwrite = true;
                    return oldTarget;
                }
                if (oldLink != null && !IsTargetIdentityCompatible(oldLink, packageId, sourceGuid, oldTarget))
                {
                    skipReason = "目标已被替换，链路冲突: " + oldTarget;
                    return string.Empty;
                }
                skipReason = "已有有效导出链路";
                return string.Empty;
            }

            if (oldTargetExists && overwriteExistingExport && !usedTargetPaths.Contains(oldTarget))
            {
                if (!IsTargetOwnedByPackage(oldLink, packageId, oldTarget))
                {
                    skipReason = "目标不再属于当前链路，禁止接管覆盖: " + oldTarget;
                    return string.Empty;
                }
                usedTargetPaths.Add(oldTarget);
                overwrite = true;
                return oldTarget;
            }

            string desired = BuildPrefixedTargetPath(sourcePath, categoryFolder, fileNamePrefix);
            if (usedTargetPaths.Contains(desired))
            {
                skipReason = "本次导出目标路径重复: " + desired;
                return string.Empty;
            }

            UnityEngine.Object existingTarget = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(desired);
            if (existingTarget != null)
            {
                skipReason = "目标已存在且没有可覆盖链路: " + desired;
                return string.Empty;
            }

            usedTargetPaths.Add(desired);
            return desired;
        }

        private static string BuildPrefixedTargetPath(string sourcePath, string categoryFolder, string fileNamePrefix)
        {
            string extension = Path.GetExtension(sourcePath);
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            fileNamePrefix = SanitizeFileNamePrefix(fileNamePrefix);
            if (!string.IsNullOrEmpty(fileNamePrefix) && !fileName.StartsWith(fileNamePrefix, StringComparison.OrdinalIgnoreCase))
                fileName = fileNamePrefix + fileName;

            return NormalizeAssetPath($"{categoryFolder}/{fileName}{extension}");
        }

        private static bool DisplayExportPreflight(
            ESAssetPackageBakeData data,
            string exportRoot,
            List<ExportPlanItem> plan,
            HashSet<string> rootPaths,
            HashSet<string> dependencyPaths,
            List<string> duplicateSkipped)
        {
            int rootCount = plan.Count(x => x.rootSelected);
            int dependencyCount = plan.Count(x => x.dependency);
            int overwriteCount = plan.Count(x => x.overwrite);
            string message =
                $"导出前确认\n\n" +
                $"配置: {(string.IsNullOrWhiteSpace(data.exportConfigName) ? data.displayName : data.exportConfigName)}\n" +
                $"导出根目录: {exportRoot}\n" +
                $"命名前缀: {data.exportFileNamePrefix}\n" +
                $"按类型分目录: 已启用\n" +
                $"重复导出覆盖: {(data.overwriteExistingExport ? "允许覆盖已有链路目标" : "默认跳过已有有效链路")}\n\n" +
                $"直接选中: {rootPaths.Count}\n" +
                $"计划复制直接资源: {rootCount}\n" +
                $"依赖资源: {dependencyPaths.Count}\n" +
                $"计划复制依赖: {dependencyCount}\n" +
                $"计划覆盖: {overwriteCount}\n" +
                $"重复/冲突跳过: {duplicateSkipped.Count}\n\n";

            message += "事务策略: 先暂存全部源资产并备份已有目标，全部校验通过后才提交；任一阶段失败将尝试自动回滚。\n\n";

            if (dependencyPaths.Count > 0)
                message += "依赖文件预览:\n" + string.Join("\n", dependencyPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Take(18)) + "\n\n";

            if (duplicateSkipped.Count > 0)
                message += "重复/冲突预览:\n" + string.Join("\n", duplicateSkipped.Take(12)) + "\n\n";

            message += "是否继续导出？";
            return EditorUtility.DisplayDialog("资源包导出前通报", message, "继续导出", "取消");
        }

        private static void UpdateExportLinks(
            ESAssetPackageBakeData data,
            Dictionary<string, string> copiedPathMap,
            Dictionary<string, bool> rootSelectedByPath,
            string sessionId,
            string exportTime)
        {
            if (data.exportLinks == null)
                data.exportLinks = new List<ESAssetPackageExportLink>();

            var lookup = BuildExportLinkLookup(data.exportLinks);
            foreach (var pair in copiedPathMap)
            {
                string sourcePath = NormalizeAssetPath(pair.Key);
                string targetPath = NormalizeAssetPath(pair.Value);
                string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                ESAssetPackageExportLink link = null;
                if (!string.IsNullOrEmpty(sourceGuid))
                    lookup.TryGetValue(sourceGuid, out link);
                if (link == null)
                    lookup.TryGetValue(sourcePath, out link);

                if (link == null)
                {
                    link = new ESAssetPackageExportLink();
                    data.exportLinks.Add(link);
                }

                Type type = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
                link.sourceGuid = sourceGuid;
                link.sourceAssetPath = sourcePath;
                link.targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
                link.targetAssetPath = targetPath;
                link.category = DetermineCategory(sourcePath, type);
                link.rootSelected = rootSelectedByPath != null && rootSelectedByPath.TryGetValue(sourcePath, out bool rootSelected) && rootSelected;
                link.lastExportSessionId = sessionId;
                link.lastExportTime = exportTime;
                link.exportCount++;
                link.sourceDependencyHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
                link.targetFileHash = ComputeAssetFileHash(targetPath);
                link.exportConfigFingerprint = data.ComputeExportConfigFingerprint();
                link.linkState = ESAssetPackageExportLinkState.Valid;
                link.packageId = data.packageId;
            }

            data.exportLinks.Sort((a, b) => string.Compare(a?.targetAssetPath, b?.targetAssetPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void SyncExportChainDictionary(ESAssetPackageBakeData data)
        {
            if (data == null)
                return;

            data.exportLinks ??= new List<ESAssetPackageExportLink>();
            data.exportChainBySourceGuid ??= new Dictionary<string, ESAssetPackageExportChain>();
            data.exportChainBySourceGuid.Clear();

            for (int i = 0; i < data.exportLinks.Count; i++)
            {
                ESAssetPackageExportLink link = data.exportLinks[i];
                if (link == null || string.IsNullOrEmpty(link.sourceGuid))
                    continue;

                var chain = new ESAssetPackageExportChain();
                chain.FromLink(link);
                data.exportChainBySourceGuid[link.sourceGuid] = chain;
            }
        }

        public static int RepairExportLinks(ESAssetPackageBakeData data)
        {
            if (data == null || data.exportLinks == null)
                return 0;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int removed = 0;
            data.exportLinks.RemoveAll(link =>
            {
                if (link == null || string.IsNullOrEmpty(link.sourceGuid) || string.IsNullOrEmpty(link.targetAssetPath))
                {
                    removed++;
                    return true;
                }

                string target = NormalizeAssetPath(link.targetAssetPath);
                string resolvedSourcePath = AssetDatabase.GUIDToAssetPath(link.sourceGuid);
                if (!string.IsNullOrEmpty(resolvedSourcePath))
                    link.sourceAssetPath = NormalizeAssetPath(resolvedSourcePath);
                bool targetExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(target) != null;
                string key = link.sourceGuid + "|" + target;
                if (!seen.Add(key))
                {
                    removed++;
                    return true;
                }

                link.targetAssetPath = target;
                string actualTargetGuid = targetExists ? AssetDatabase.AssetPathToGUID(target) : string.Empty;
                if (targetExists && !string.IsNullOrEmpty(link.targetGuid) && !string.Equals(link.targetGuid, actualTargetGuid, StringComparison.OrdinalIgnoreCase))
                    link.linkState = ESAssetPackageExportLinkState.TargetReplaced;
                else if (targetExists && !string.IsNullOrEmpty(link.targetFileHash) && !string.Equals(link.targetFileHash, ComputeAssetFileHash(target), StringComparison.OrdinalIgnoreCase))
                    link.linkState = ESAssetPackageExportLinkState.TargetReplaced;
                else if (!targetExists)
                    link.linkState = string.IsNullOrEmpty(link.targetFileHash) ? ESAssetPackageExportLinkState.LegacyLink : ESAssetPackageExportLinkState.TargetMissing;
                else if (string.IsNullOrEmpty(link.sourceDependencyHash) || string.IsNullOrEmpty(link.exportConfigFingerprint))
                    link.linkState = ESAssetPackageExportLinkState.LegacyLink;
                else
                    link.linkState = ESAssetPackageExportLinkState.Valid;
                if (link.linkState != ESAssetPackageExportLinkState.TargetReplaced)
                {
                    link.targetGuid = actualTargetGuid;
                    link.targetFileHash = targetExists ? ComputeAssetFileHash(target) : link.targetFileHash;
                }
                link.sourceAssetPath = NormalizeAssetPath(link.sourceAssetPath);
                return false;
            });

            SyncExportChainDictionary(data);
            EditorUtility.SetDirty(data);
            return removed;
        }

        private static void MarkRecordsWithValidExportLinksAsSelected(ESAssetPackageBakeData data)
        {
            if (data == null || data.records == null || data.exportLinks == null)
                return;

            var exportedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < data.exportLinks.Count; i++)
            {
                ESAssetPackageExportLink link = data.exportLinks[i];
                if (link == null || string.IsNullOrEmpty(link.targetAssetPath))
                    continue;

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.targetAssetPath) == null)
                    continue;

                if (!string.IsNullOrEmpty(link.sourceGuid))
                    exportedSources.Add(link.sourceGuid);
                if (!string.IsNullOrEmpty(link.sourceAssetPath))
                    exportedSources.Add(NormalizeAssetPath(link.sourceAssetPath));
            }

            for (int i = 0; i < data.records.Count; i++)
            {
                ESAssetPackageBakeRecord record = data.records[i];
                if (record == null)
                    continue;

                if ((!string.IsNullOrEmpty(record.guid) && exportedSources.Contains(record.guid)) ||
                    (!string.IsNullOrEmpty(record.assetPath) && exportedSources.Contains(NormalizeAssetPath(record.assetPath))))
                {
                    record.selectedForUse = true;
                }
            }
        }

        private static void TrimExportSessions(List<ESAssetPackageExportSession> sessions, int keepCount)
        {
            if (sessions == null || keepCount <= 0)
                return;

            while (sessions.Count > keepCount)
                sessions.RemoveAt(0);
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            path = NormalizeAssetPath(path);
            root = NormalizeAssetPath(root);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }


        private static void RemoveEmptyFolders(string root)
        {
            root = NormalizeAssetPath(root);
            if (!AssetDatabase.IsValidFolder(root))
                return;

            string fullRoot = AssetPathToFullPath(root);
            if (!Directory.Exists(fullRoot))
                return;

            foreach (string directory in ESManagedFileIO.EnumerateDirectoriesSafely(fullRoot)
                         .OrderByDescending(d => d.Length))
            {
                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    continue;

                string assetPath = FullPathToAssetPath(directory);
                if (IsPathInsideRoot(assetPath, root) && !string.Equals(assetPath, root, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static bool IsCodeOrEditorOnlyDependency(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".cs" ||
                   ext == ".asmdef" ||
                   ext == ".dll" ||
                   ext == ".pdb" ||
                   ext == ".mdb";
        }

        private static bool IsOnlyEditorSOAsset(string path)
        {
            path = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path))
                return false;

            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type != null &&
                   typeof(ScriptableObject).IsAssignableFrom(type) &&
                   Attribute.IsDefined(type, typeof(ESOnlyEditorSOAttribute), true);
        }

        private static int RemapCopiedAssetGuids(Dictionary<string, string> copiedPathMap)
        {
            if (copiedPathMap == null || copiedPathMap.Count == 0)
                return 0;

            var guidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in copiedPathMap)
            {
                string oldGuid = AssetDatabase.AssetPathToGUID(pair.Key);
                string newGuid = AssetDatabase.AssetPathToGUID(pair.Value);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid) && oldGuid != newGuid)
                    guidMap[oldGuid] = newGuid;
            }

            int changedFiles = 0;
            var originals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string targetPath in copiedPathMap.Values)
                {
                    if (!IsTextSerializedAsset(targetPath))
                        continue;

                    string fullPath = AssetPathToFullPath(targetPath);
                    if (!File.Exists(fullPath))
                        continue;

                    string text = File.ReadAllText(fullPath);
                    string newText = text;
                    foreach (var pair in guidMap)
                        newText = newText.Replace(pair.Key, pair.Value);

                    if (newText == text)
                        continue;

                    originals[fullPath] = text;
                    ESManagedFileIO.WriteTextAtomic(fullPath, newText, new UTF8Encoding(false), Application.dataPath);
                    changedFiles++;
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                foreach (string targetPath in copiedPathMap.Values)
                {
                    if (!IsTextSerializedAsset(targetPath))
                        continue;
                    string fullPath = AssetPathToFullPath(targetPath);
                    if (!File.Exists(fullPath))
                        throw new IOException("GUID 重映射后目标文件丢失：" + targetPath);
                }
                return changedFiles;
            }
            catch
            {
                foreach (var original in originals)
                    ESManagedFileIO.WriteTextAtomic(original.Key, original.Value, new UTF8Encoding(false), Application.dataPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                throw;
            }
        }

        private static bool IsTextSerializedAsset(string assetPath)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".mat" ||
                   ext == ".prefab" ||
                   ext == ".anim" ||
                   ext == ".controller" ||
                   ext == ".overridecontroller" ||
                   ext == ".playable" ||
                   ext == ".asset" ||
                   ext == ".unity";
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return assetPath;

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(fullPath))
                return string.Empty;

            string normalizedFullPath = Path.GetFullPath(fullPath).Replace("\\", "/");
            string normalizedProjectRoot = Path.GetFullPath(projectRoot).Replace("\\", "/").TrimEnd('/');
            if (!normalizedFullPath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return normalizedFullPath.Substring(normalizedProjectRoot.Length + 1);
        }

        public static ESAssetPackageCategory DetermineCategory(string path, Type type)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".prefab") return ESAssetPackageCategory.Prefab;
            if (ext == ".unity") return ESAssetPackageCategory.Scene;
            if (ext == ".mat") return ESAssetPackageCategory.Material;
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd" || ext == ".exr" || ext == ".tif" || ext == ".tiff") return ESAssetPackageCategory.Texture;
            if (ext == ".fbx" || ext == ".obj" || ext == ".blend" || ext == ".dae")
                return IsAnimationModelAsset(path) ? ESAssetPackageCategory.Animation : ESAssetPackageCategory.Model;
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff") return ESAssetPackageCategory.Audio;
            if (ext == ".anim") return ESAssetPackageCategory.Animation;
            if (ext == ".controller" || ext == ".overridecontroller" || ext == ".playable") return ESAssetPackageCategory.Other;
            if (ext == ".shader" || ext == ".shadergraph") return ESAssetPackageCategory.Shader;
            if (ext == ".ttf" || ext == ".otf" || ext == ".fontsettings") return ESAssetPackageCategory.Font;
            if (ext == ".mp4" || ext == ".mov" || ext == ".webm") return ESAssetPackageCategory.Video;

            if (type != null && typeof(ScriptableObject).IsAssignableFrom(type))
                return ESAssetPackageCategory.ScriptableObject;

            return ESAssetPackageCategory.Other;
        }

        private static bool IsAnimationModelAsset(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return false;

            if (importer.animationType == ModelImporterAnimationType.None)
                return false;

            string normalizedPath = NormalizeAssetPath(path);
            string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
            bool animationNamedAsset = normalizedPath.IndexOf("/Animations/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       fileName.IndexOf('@') >= 0;
            if (!animationNamedAsset)
                return false;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips != null && clips.Length > 0)
                return true;

            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
            return defaultClips != null && defaultClips.Length > 0;
        }

        public static string GetExportSubFolder(ESAssetPackageCategory category)
        {
            return ESAssetPackageBakeData.GetDefaultExportSubFolder(category);
        }

        private static string FormatFileSize(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                    return "未知";

                double size = info.Length;
                string[] units = { "B", "KB", "MB", "GB" };
                int unit = 0;
                while (size >= 1024 && unit < units.Length - 1)
                {
                    size /= 1024;
                    unit++;
                }

                return $"{size:F1} {units[unit]}";
            }
            catch
            {
                return "未知";
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        private static string SanitizeFileNamePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return "ES选用_";

            string result = prefix.Trim().Replace("\\", "_").Replace("/", "_").Replace(":", "_");
            foreach (char c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');

            return string.IsNullOrWhiteSpace(result) ? "ES选用_" : result;
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
#endif
}
