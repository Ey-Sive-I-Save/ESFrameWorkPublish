using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable] public sealed class ESPipelineAssetIdentity : IEquatable<ESPipelineAssetIdentity>
    {
        public string guid = string.Empty;
        public long localFileId;
        public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0;
        public bool IsSubAsset => localFileId != 0;
        public string Key => IsSubAsset ? guid + ":" + localFileId : guid;
        public bool Equals(ESPipelineAssetIdentity other) => other != null && localFileId == other.localFileId && string.Equals(guid, other.guid, StringComparison.Ordinal);
        public override bool Equals(object obj) => Equals(obj as ESPipelineAssetIdentity);
        public override int GetHashCode() => (StringComparer.Ordinal.GetHashCode(guid ?? string.Empty) * 397) ^ localFileId.GetHashCode();
    }

    [Serializable] public sealed class ESAssetCatalogEntry
    {
        public ESPipelineAssetIdentity identity = new ESPipelineAssetIdentity();
        public string assetPath = string.Empty, assetTypeName = string.Empty, kind = string.Empty, stringKey = string.Empty;
        public string libraryName = string.Empty, libraryFolder = string.Empty, libraryBundleCode = string.Empty, pageName = string.Empty, namedOption = string.Empty, subAssetName = string.Empty;
        public string parentFolderPath = string.Empty, parentFolderGuid = string.Empty, topLevelFolderPath = string.Empty, topLevelFolderGuid = string.Empty;
        public int enumKey;
        public bool isBusinessAsset = true;
    }
    [Serializable] public sealed class ESAssetLibraryCatalog
    {
        public int formatVersion = 3;
        public string libraryName = string.Empty, libraryFolder = string.Empty, libraryBundleCode = string.Empty, libraryAssetGuid = string.Empty, generatedUtc = string.Empty;
        public List<ESAssetCatalogEntry> assets = new List<ESAssetCatalogEntry>();
        public List<string> excludedEditorOnlyPaths = new List<string>();
        public List<string> errors = new List<string>(), warnings = new List<string>();
    }
    [Serializable] public sealed class ESAssetCatalogBakeOutput
    {
        public string relativePath = string.Empty;
        public string libraryName = string.Empty, libraryFolder = string.Empty, outputKind = string.Empty;
        public int protocolVersion;
        public long commitGeneration;
        public long size;
        public string sha256 = string.Empty;
        public bool isCatalog;
    }
    [Serializable] public sealed class ESAssetCatalogBakeCommit
    {
        public int formatVersion = 2;
        public string transactionId = string.Empty, generatedUtc = string.Empty;
        public long commitGeneration;
        public List<ESAssetCatalogBakeOutput> outputs = new List<ESAssetCatalogBakeOutput>();
    }
    [Serializable] public sealed class ESAssetReferenceRoot
    {
        public ESPipelineAssetIdentity identity = new ESPipelineAssetIdentity();
        public string assetPath = string.Empty;
    }
    [Serializable] public sealed class ESAssetReferenceNode
    {
        public ESPipelineAssetIdentity identity = new ESPipelineAssetIdentity();
        public string assetPath = string.Empty, assetTypeName = string.Empty, dependencyHash = string.Empty;
        public bool editorOnly, markable;
        public List<string> ownerLibraryFolders = new List<string>();
        public List<string> directDependencies = new List<string>();
    }
    [Serializable] public sealed class ESAssetReferenceGraph
    {
        public int formatVersion = ESAssetPipelineIO.ReferenceGraphFormatVersion;
        public string libraryName = string.Empty, libraryFolder = string.Empty, generatedUtc = string.Empty;
        public List<ESAssetReferenceRoot> roots = new List<ESAssetReferenceRoot>();
        public List<ESAssetReferenceNode> nodes = new List<ESAssetReferenceNode>();
        public List<string> errors = new List<string>(), warnings = new List<string>();
    }
    [Serializable] public sealed class ESAssetBundleAssignment
    {
        public string assetPath = string.Empty, assetBundleKey = string.Empty, ownerLibrary = string.Empty;
        public ESPipelineAssetIdentity identity = new ESPipelineAssetIdentity();
        public bool isBusinessAsset;
    }
    [Serializable] public sealed class ESAssetBundleBuildPlan
    {
        public int formatVersion = 2;
        public string platform = string.Empty, generatedUtc = string.Empty;
        public List<ESAssetBundleAssignment> assignments = new List<ESAssetBundleAssignment>();
        public List<string> errors = new List<string>(), warnings = new List<string>();
    }
    [Serializable] public sealed class ESAssetBundleAssetEntry
    {
        public ESPipelineAssetIdentity identity = new ESPipelineAssetIdentity();
        public string assetBundleKey = string.Empty, internalName = string.Empty, kind = string.Empty, typeName = string.Empty, ownerLibrary = string.Empty, subAssetName = string.Empty;
        public bool isBusinessAsset;
    }
    [Serializable] public sealed class ESAssetBundleAssetList
    {
        public int formatVersion = 2;
        public string platform = string.Empty;
        public List<ESAssetBundleAssetEntry> assets = new List<ESAssetBundleAssetEntry>();
    }
    [Serializable] public sealed class ESAssetBundleRecord
    {
        public string assetBundleKey = string.Empty, fileName = string.Empty, unityHash = string.Empty, sha256 = string.Empty, localRelativePath = string.Empty;
        public uint crc;
        public long size;
        public List<string> dependencies = new List<string>();
    }
    [Serializable] public sealed class ESRuntimeMainAssetManifestRecord
    {
        public string guid = string.Empty, assetBundleKey = string.Empty, internalName = string.Empty, typeName = string.Empty;
    }
    [Serializable] public sealed class ESRuntimeSubAssetManifestRecord
    {
        public string guid = string.Empty, assetBundleKey = string.Empty, internalName = string.Empty, subAssetName = string.Empty, typeName = string.Empty;
        public long localFileId;
    }
    [Serializable] public sealed class ESAssetBundleManifest
    {
        public int formatVersion = ESAssetPipelineIO.RuntimeProtocolFormatVersion;
        public string platform = string.Empty, libraryName = string.Empty;
        public List<ESAssetBundleRecord> assetBundles = new List<ESAssetBundleRecord>();
        public List<ESRuntimeMainAssetManifestRecord> mainAssetsByGuid = new List<ESRuntimeMainAssetManifestRecord>();
        public List<ESRuntimeSubAssetManifestRecord> subAssetsById = new List<ESRuntimeSubAssetManifestRecord>();
    }
    [Serializable] public sealed class ESAssetLibraryIdentity
    {
        public int formatVersion = ESAssetPipelineIO.RuntimeProtocolFormatVersion;
        public string libraryName = string.Empty, libraryFolder = string.Empty, libraryBundleCode = string.Empty, platform = string.Empty, version = string.Empty, channel = string.Empty, catalogUrl = string.Empty, assetBundleManifestUrl = string.Empty, catalogSha256 = string.Empty, assetBundleManifestSha256 = string.Empty;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
        public List<ESAssetBundleIdentityHash> assetBundles = new List<ESAssetBundleIdentityHash>();
    }
    [Serializable] public sealed class ESAssetBundleIdentityHash
    {
        public string assetBundleKey = string.Empty, sha256 = string.Empty;
        public long size;
    }
    [Serializable] public sealed class ESAssetBuildSet
    {
        public int formatVersion = 1;
        public string platform = string.Empty, buildId = string.Empty, builtUtc = string.Empty;
        public List<string> libraryFolders = new List<string>();
    }
    [Serializable] public sealed class ESAssetReleaseLibrary
    {
        public string libraryName = string.Empty, version = string.Empty, catalogUrl = string.Empty, catalogSha256 = string.Empty, assetBundleManifestUrl = string.Empty, assetBundleManifestSha256 = string.Empty;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
    }
    [Serializable] public sealed class ESAssetReleaseManifest
    {
        public int formatVersion = ESAssetPipelineIO.RuntimeProtocolFormatVersion;
        public string platform = string.Empty, releaseVersion = string.Empty, channel = string.Empty, publishedUtc = string.Empty;
        public List<ESAssetReleaseLibrary> libraries = new List<ESAssetReleaseLibrary>();
        public string totalConsumerUrl = string.Empty, totalConsumerSha256 = string.Empty, bundleIndexUrl = string.Empty, bundleIndexSha256 = string.Empty;
    }
    /// <summary>
    /// 仅供人工上传或未来 OSS 上传器消费的发布计划；它绝不是运行时协议的一部分。
    /// 根发布清单条目始终排在最后，避免任何上传工具误把半成品发布给客户端。
    /// </summary>
    [Serializable] public sealed class ESAssetReleaseUploadPlan
    {
        public int formatVersion = 1;
        public string platform = string.Empty, releaseVersion = string.Empty, sourceRoot = string.Empty, publicBaseUrl = string.Empty, generatedUtc = string.Empty;
        public string instruction = "按 uploadOrder 升序上传；ESAssetReleaseManifest.json 必须最后上传并设置 Cache-Control: no-cache, max-age=0, must-revalidate；其余版本化文件使用 immutable 长缓存。";
        public List<ESAssetReleaseUploadPlanFile> files = new List<ESAssetReleaseUploadPlanFile>();
    }
    [Serializable] public sealed class ESAssetReleaseUploadPlanFile
    {
        public string sourcePath = string.Empty, relativePath = string.Empty, publicUrl = string.Empty, sha256 = string.Empty;
        /// <summary>上传 Provider 必须原样应用到远端对象；它是 Root 可见性的发布契约。</summary>
        public string cacheControl = string.Empty;
        public long size;
        public int uploadOrder;
        public bool uploadLast;
    }
    [Serializable] public sealed class ESAssetReleaseBundleRecord
    {
        public string libraryFolder = string.Empty, assetBundleKey = string.Empty, fileUrl = string.Empty, sha256 = string.Empty, localRelativePath = string.Empty, embeddedRelativePath = string.Empty;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
        public uint crc;
        public long size;
        public List<string> dependencies = new List<string>();
    }
    [Serializable] public sealed class ESAssetReleaseBundleIndex
    {
        public int formatVersion = ESAssetPipelineIO.RuntimeProtocolFormatVersion;
        public string platform = string.Empty, releaseVersion = string.Empty;
        public List<ESAssetReleaseBundleRecord> assetBundles = new List<ESAssetReleaseBundleRecord>();
    }
    [Serializable] public sealed class ESAssetConsumerLibraryReference
    {
        public string libraryName = string.Empty, libraryFolder = string.Empty, libraryIdentityUrl = string.Empty, libraryIdentitySha256 = string.Empty, embeddedIdentityRelativePath = string.Empty;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
        public bool requiredAtBoot;
    }
    [Serializable] public sealed class ESAssetConsumerReference
    {
        public string consumerId = string.Empty, consumerUrl = string.Empty, consumerSha256 = string.Empty;
    }
    [Serializable] public sealed class ESAssetConsumerCodePackageReference
    {
        public string packageKey = string.Empty, kind = string.Empty, fileName = string.Empty, url = string.Empty, sha256 = string.Empty, notes = string.Empty;
        public long size;
        public bool requiredAtBoot;
        public int loadOrder;
    }
    [Serializable] public sealed class ESAssetConsumerGameCoreReference
    {
        public string guid = string.Empty;
        public long localFileId;
        public List<ESAssetConsumerGameCoreDependencyReference> dependencies = new List<ESAssetConsumerGameCoreDependencyReference>();
    }
    [Serializable] public sealed class ESAssetConsumerGameCoreDependencyReference { public string guid = string.Empty; public long localFileId; }
    [Serializable] public sealed class ESAssetConsumerResidentAssetReference
    {
        public string guid = string.Empty;
        public long localFileId;
    }
    [Serializable] public sealed class ESAssetConsumerManifest
    {
        public int formatVersion = ESAssetPipelineIO.RuntimeProtocolFormatVersion;
        public string consumerId = string.Empty, name = string.Empty, description = string.Empty, maintainer = string.Empty, releaseNotes = string.Empty;
        public string version = string.Empty, platform = string.Empty, channel = string.Empty, publishedUtc = string.Empty;
        public bool isTotalConsumer;
        public List<string> tags = new List<string>();
        public List<ESAssetConsumerReference> requiredConsumers = new List<ESAssetConsumerReference>();
        public List<ESAssetConsumerLibraryReference> libraries = new List<ESAssetConsumerLibraryReference>();
        public List<ESAssetConsumerGameCoreReference> gameCoreAssets = new List<ESAssetConsumerGameCoreReference>();
        public List<ESAssetConsumerResidentAssetReference> residentAssets = new List<ESAssetConsumerResidentAssetReference>();
        public List<ESAssetConsumerCodePackageReference> codePackages = new List<ESAssetConsumerCodePackageReference>();
    }

    internal static class ESAssetPipelineIO
    {
        public const int ReferenceGraphFormatVersion = 1;
        public const int CatalogFormatVersion = 3;
        public const int RuntimeProtocolFormatVersion = 5;
        public const int CatalogBakeCommitFormatVersion = 2;
        public const string CatalogOutputKind = "Catalog", ReferenceGraphOutputKind = "ReferenceGraph";
        public const string CatalogFileName = "ESAssetLibraryCatalog.json", ReferenceGraphFileName = "ESAssetReferenceGraph.json", CatalogBakeCommitFileName = "ESAssetCatalogBakeCommit.json", PlanFileName = "ESAssetBundleBuildPlan.json", AssetListFileName = "ESAssetBundleAssetList.json";
        public const string BundleManifestFileName = "ESAssetBundleManifest.json", LibraryIdentityFileName = "ESAssetLibraryIdentity.json", BuildSetFileName = "ESAssetBuildSet.json", ReleaseManifestFileName = "ESAssetReleaseManifest.json", ConsumerManifestFileName = "ESAssetConsumerManifest.json", ReleaseBundleIndexFileName = "ESAssetReleaseBundleIndex.json";
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string PipelineRoot => Path.Combine(ProjectRoot, "ES", "ResourcePipeline");
        // Unity 原始 AB 与 AssetBundleManifest 的专用构建缓存，永不参与发布或下载。
        public static string BuildCacheRoot(string platform) => Path.Combine(PipelineRoot, "BuildCache", platform, "UnityAssetBundles");
        public static string BakeRoot => Path.Combine(PipelineRoot, "Baked");
        public static string PlanRoot(string platform) => Path.Combine(PipelineRoot, "Planned", platform);
        public const string LibrariesFolderName = "Libraries";
        public const string AssetBundlesFolderName = "AssetBundles";

        public static string StagingRoot(string platform) => Path.Combine(PipelineRoot, "BuildStaging", platform);
        public static string InitialTargetRoot => Path.Combine(PipelineRoot, "InitialTarget");
        public static string PublishedRoot => Path.Combine(PipelineRoot, "Published");
        public static string LocalTestRoot(string platform) => Path.Combine(PublishedRoot, "LocalTest", platform);
        public static string ManualUploadPlansRoot(string platform) => Path.Combine(PublishedRoot, "ManualUploadPlans", platform);
        public static string ReleasesRoot => Path.Combine(PipelineRoot, "Releases");
        public static string StagingLibrariesRoot(string platform) => Path.Combine(StagingRoot(platform), LibrariesFolderName);
        public static string StagingLibraryFolder(string platform, string libraryFolder) => Path.Combine(StagingLibrariesRoot(platform), SafeSegment(libraryFolder));
        public static string AssetBundleRelativePath(string fileName) => AssetBundlesFolderName + "/" + ESAssetBundleUtility.ToSafeAssetBundleFileName(fileName);
        public static string ReleaseLibraryFolder(string releaseRoot, string platform, string releaseVersion, string libraryFolder)
        {
            string safeReleaseVersion = RequirePathSegment(releaseVersion, "releaseVersion");
            string safePlatform = string.IsNullOrEmpty(platform) ? string.Empty : RequirePathSegment(platform, "platform");
            return string.IsNullOrEmpty(safePlatform)
                ? Path.Combine(releaseRoot, safeReleaseVersion, LibrariesFolderName, SafeSegment(libraryFolder))
                : Path.Combine(releaseRoot, safePlatform, safeReleaseVersion, LibrariesFolderName, SafeSegment(libraryFolder));
        }
        public static string ReleaseLibraryRelativeBase(string platform, string releaseVersion, string libraryFolder)
            => RequirePathSegment(platform, "platform") + "/" + RequirePathSegment(releaseVersion, "releaseVersion") + "/" + LibrariesFolderName + "/" + SafeSegment(libraryFolder) + "/";
        public static string EmbeddedLibraryRelativeBase(string platform, string libraryFolder)
            => RequirePathSegment(platform, "platform") + "/Embedded/" + LibrariesFolderName + "/" + SafeSegment(libraryFolder) + "/";
        public static string EmbeddedLibraryFolder(string releaseRoot, string platform, string libraryFolder)
            => Path.Combine(releaseRoot, EmbeddedLibraryRelativeBase(platform, libraryFolder).Replace('/', Path.DirectorySeparatorChar));

        public static void EnsureAssetBundleReleaseMode()
        {
            ESAssetRunMode mode = ESGlobalResSetting.Instance.AssetRunMode;
            if (mode != ESAssetRunMode.LocalBuild && mode != ESAssetRunMode.HotUpdate)
                throw new InvalidOperationException($"AB 构建/发布只支持 LocalBuild 或 HotUpdate，当前模式为 {mode}。");
        }

        [MenuItem(MenuItemPathDefine.RESOURCE_DELIVERY_PATH + "资源管理/清理旧协议生成物", false, 30)]
        private static void PurgeLegacyGeneratedArtifactsMenu()
        {
            if (ESGlobalResSetting.Instance == null)
                throw new InvalidOperationException("未找到 ESGlobalResSetting，无法确定旧协议产物的平台目录。");

            string platform = PlatformName;
            if (!HasLegacyGeneratedArtifacts(platform))
            {
                EditorUtility.DisplayDialog("ES 资源管线", "当前平台没有检测到旧协议生成物。", "确定");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "清理旧协议生成物",
                "将清理当前平台的旧协议生成目录，包括 Baked、Planned、BuildCache、BuildStaging、Published、Releases，以及 StreamingAssets 中的 ES 生成资源。\n\n不会删除 Assets 中的业务源资产。确认继续吗？",
                "清理",
                "取消");
            if (confirmed)
                PurgeLegacyGeneratedArtifactsExplicitly();
        }

        /// <summary>
        /// One-time destructive boundary for the v5 release protocol. Generated v1/v3 files
        /// cannot participate in the new pipeline, so keeping them is both misleading and a
        /// source of accidental manual publication. This is an explicit cleanup action and
        /// must not be coupled to EditorDirect catalog baking.
        /// </summary>
        public static void PurgeLegacyGeneratedArtifactsExplicitly()
        {
            string platform = PlatformName;
            if (!HasLegacyGeneratedArtifacts(platform))
                return;

            DeleteGeneratedDirectory(BakeRoot);
            DeleteGeneratedDirectory(PlanRoot(platform));
            DeleteGeneratedDirectory(Path.Combine(PipelineRoot, "BuildCache", platform));
            DeleteGeneratedDirectory(StagingRoot(platform));
            DeleteGeneratedDirectory(LocalTestRoot(platform));
            DeleteGeneratedDirectory(ManualUploadPlansRoot(platform));
            DeleteGeneratedDirectory(Path.Combine(ReleasesRoot, platform));
            DeleteGeneratedDirectory(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName, platform));

            // Pre-v5 Master output used WindowsPlayer rather than the unified BuildTarget name.
            DeleteGeneratedDirectory(Path.Combine(InitialTargetRoot, "WindowsPlayer"));
            DeleteGeneratedDirectory(Path.Combine(ReleasesRoot, "WindowsPlayer"));
            AssetDatabase.Refresh();
            Debug.Log("[ESRes][Pipeline] 已清理旧协议生成物；请从烘焙开始完整执行 v5 四步发布流程。");
        }

        private static bool HasLegacyGeneratedArtifacts(string platform)
        {
            if (Directory.Exists(Path.Combine(InitialTargetRoot, "WindowsPlayer"))
                || Directory.Exists(Path.Combine(ReleasesRoot, "WindowsPlayer")))
                return true;

            return HasUnexpectedFormat<ESAssetBundleBuildPlan>(Path.Combine(PlanRoot(platform), PlanFileName), 2, value => value.formatVersion)
                || HasUnexpectedFormat<ESAssetBundleAssetList>(Path.Combine(PlanRoot(platform), AssetListFileName), 2, value => value.formatVersion)
                || HasUnexpectedFormat<ESAssetReleaseManifest>(Path.Combine(LocalTestRoot(platform), ReleaseManifestFileName), RuntimeProtocolFormatVersion, value => value.formatVersion)
                || HasUnexpectedFormat<ESAssetReleaseManifest>(Path.Combine(ReleasesRoot, platform, ReleaseManifestFileName), RuntimeProtocolFormatVersion, value => value.formatVersion);
        }

        private static bool HasUnexpectedFormat<T>(string path, int expected, Func<T, int> getFormat) where T : class
        {
            if (!File.Exists(path)) return false;
            try
            {
                T value = ReadJson<T>(path);
                return value == null || getFormat(value) != expected;
            }
            catch { return true; }
        }

        internal static void DeleteGeneratedDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("生成物清理路径不能为空。");

            string candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string pipelineRoot = Path.GetFullPath(PipelineRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string streamingRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!IsWithinRoot(candidate, pipelineRoot) && !IsWithinRoot(candidate, streamingRoot))
                throw new UnauthorizedAccessException("生成物清理路径越出 ES 发布根目录：" + path);
            if (ContainsExistingReparsePoint(candidate))
                throw new UnauthorizedAccessException("生成物清理路径不能穿过 junction/symlink：" + path);

            if (string.Equals(candidate, pipelineRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate, streamingRoot, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("禁止删除 ES 资源管线根目录：" + path);
            if (Directory.Exists(candidate))
            {
                string managedRoot = IsWithinRoot(candidate, pipelineRoot) ? pipelineRoot : streamingRoot;
                ESManagedFileIO.DeleteDirectory(candidate, managedRoot);
            }
        }

        private static bool IsWithinRoot(string candidate, string root)
        {
            return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        internal static void DeleteGeneratedFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("生成物文件清理路径不能为空。");
            string candidate = Path.GetFullPath(path);
            string pipelineRoot = Path.GetFullPath(PipelineRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string streamingRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!IsWithinRoot(candidate, pipelineRoot) && !IsWithinRoot(candidate, streamingRoot))
                throw new UnauthorizedAccessException("生成物文件清理路径越出 ES 受管目录：" + path);
            if (ContainsExistingReparsePoint(candidate))
                throw new UnauthorizedAccessException("生成物文件不能穿过 junction/symlink：" + path);
            if (File.Exists(candidate)) File.Delete(candidate);
        }

        internal static void EnsureGeneratedDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("生成物目录路径不能为空。");
            string candidate = Path.GetFullPath(path);
            string pipelineRoot = Path.GetFullPath(PipelineRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string streamingRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!IsWithinRoot(candidate, pipelineRoot) && !IsWithinRoot(candidate, streamingRoot))
                throw new UnauthorizedAccessException("生成物目录不在 ES 受管目录内：" + path);
            if (ContainsExistingReparsePoint(candidate))
                throw new UnauthorizedAccessException("生成物目录不能穿过 junction/symlink：" + path);
            Directory.CreateDirectory(candidate);
        }

        internal static void CopyGeneratedFileAtomic(string sourcePath, string destinationPath)
        {
            string source = Path.GetFullPath(sourcePath ?? string.Empty);
            string destination = Path.GetFullPath(destinationPath ?? string.Empty);
            string pipelineRoot = Path.GetFullPath(PipelineRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string streamingRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!File.Exists(source)) throw new FileNotFoundException("资源管线源文件不存在。", source);
            if (!IsWithinRoot(source, pipelineRoot) && !IsWithinRoot(source, streamingRoot))
                throw new UnauthorizedAccessException("资源管线源文件不在受管目录内：" + sourcePath);
            if (!IsWithinRoot(destination, pipelineRoot) && !IsWithinRoot(destination, streamingRoot))
                throw new UnauthorizedAccessException("资源管线目标文件不在受管目录内：" + destinationPath);
            if (ContainsExistingReparsePoint(source) || ContainsExistingReparsePoint(Path.GetDirectoryName(destination) ?? destination)
                || (File.Exists(destination) && ContainsExistingReparsePoint(destination)))
                throw new UnauthorizedAccessException("资源管线复制不能穿过 junction/symlink。");
            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(directory)) throw new InvalidDataException("资源管线目标目录无效：" + destinationPath);
            Directory.CreateDirectory(directory);
            ESManagedFileIO.CopyFileAtomic(source, destination, pipelineRoot, streamingRoot);
        }

        private static bool ContainsExistingReparsePoint(string path)
        {
            string root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return false;
            string current = root;
            string relative = path.Substring(root.Length);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }

        private static void EnsureNoNestedReparsePoints(string directory)
        {
            ESManagedFileIO.EnsureNoNestedReparsePoints(directory);
        }
        public static string PlatformName => ESAssetBundleBuildTargetUtility.GetBuildTarget(ESGlobalResSetting.Instance.applyPlatform).ToString();
        public static string LibraryBakeFolder(string folder) => Path.Combine(BakeRoot, SafeSegment(folder));
        public static string RecoveryBakeRoot(string transactionId)
            => Path.Combine(BakeRoot, ".Recovery", RequirePathSegment(transactionId, "transactionId"));
        public static string RecoveryBakeCommitPath(string transactionId)
            => Path.Combine(RecoveryBakeRoot(transactionId), CatalogBakeCommitFileName);
        // LibraryFolder 本身就是物理目录和运行时清单共同使用的权威值，禁止先生成
        // "__gamecore_x" 再由 SafeSegment 隐式改成 "gamecore_x"，否则构建与发布查找不一致。
        public static string GameCoreLibraryFolder(string consumerId) => SafeSegment("gamecore_" + SafeSegment(consumerId));
        public static string SafeSegment(string value) => string.IsNullOrWhiteSpace(value) ? "DefaultLibrary" : ESAssetBundleUtility.ToSafeAssetBundleKey(value).Replace('/', '_').Replace('\\', '_');

        public static string ResolveGeneratedRelativePath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("生成物相对路径不能为空。");

            string normalized = relativePath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
                throw new InvalidDataException("生成物相对路径不能是绝对路径：" + relativePath);
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".." || segment.IndexOf(':') >= 0)
                    throw new InvalidDataException("生成物相对路径包含非法片段：" + relativePath);
            }

            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinRoot(candidate, normalizedRoot))
                throw new UnauthorizedAccessException("生成物相对路径越出根目录：" + relativePath);
            return candidate;
        }

        private static string RequirePathSegment(string value, string fieldName)
        {
            string segment = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(segment)
                || segment == "."
                || segment == ".."
                || !string.Equals(Path.GetFileName(segment), segment, StringComparison.Ordinal)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException(fieldName + " 不是合法路径片段：" + value);
            return segment;
        }
        public static ESPipelineAssetIdentity GetIdentity(UnityEngine.Object asset)
        {
            var result = new ESPipelineAssetIdentity();
            if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)) return result;
            result.guid = guid;
            result.localFileId = AssetDatabase.IsSubAsset(asset) ? localFileId : 0;
            return result;
        }
        public static ESPipelineAssetIdentity GetMainIdentity(string path) => new ESPipelineAssetIdentity { guid = AssetDatabase.AssetPathToGUID(path), localFileId = 0 };
        public static bool IsEditorOnly(string path, UnityEngine.Object asset = null)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            string extension = Path.GetExtension(normalized);
            if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase)
                || normalized.IndexOf("/Editor Default Resources/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.StartsWith("Assets/Editor Default Resources/", StringComparison.OrdinalIgnoreCase)
                || normalized.IndexOf("/Gizmos/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.StartsWith("Assets/Gizmos/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase)) return true;

            Type assetType = asset != null ? asset.GetType() : AssetDatabase.GetMainAssetTypeAtPath(normalized);
            return assetType != null
                && typeof(ScriptableObject).IsAssignableFrom(assetType)
                && Attribute.IsDefined(assetType, typeof(ESOnlyEditorSOAttribute), true);
        }
        public static void WriteJson<T>(string path, T value, bool atomic = true)
        {
            WriteJsonCore(path, value, false);
        }

        public static void WriteJsonCreateNew<T>(string path, T value)
        {
            WriteJsonCore(path, value, true);
        }

        private static void WriteJsonCore<T>(string path, T value, bool createNew)
        {
            string normalizedPath = Path.GetFullPath(path);
            string pipelineRoot = Path.GetFullPath(PipelineRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string streamingRoot = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!IsWithinRoot(normalizedPath, pipelineRoot) && !IsWithinRoot(normalizedPath, streamingRoot))
                throw new UnauthorizedAccessException("资源管线 JSON 只能写入 ES/ResourcePipeline 或 StreamingAssets/Res：" + path);
            if (ContainsExistingReparsePoint(normalizedPath))
                throw new UnauthorizedAccessException("资源管线 JSON 路径不能穿过 junction/symlink：" + path);

            string json = JsonConvert.SerializeObject(value, Formatting.Indented);
            // 发布清单、目录、计划和索引统一原子提升；保留 atomic 参数仅兼容旧调用方。
            if (createNew)
                ESManagedFileIO.WriteTextAtomicCreateNew(
                    normalizedPath,
                    json,
                    new UTF8Encoding(false),
                    pipelineRoot,
                    streamingRoot);
            else
                ESManagedFileIO.WriteTextAtomic(
                    normalizedPath,
                    json,
                    new UTF8Encoding(false),
                    pipelineRoot,
                    streamingRoot);
        }
        public static T ReadJson<T>(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("资源管线输入不存在，请先执行前一阶段。", path);
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
    }
}
