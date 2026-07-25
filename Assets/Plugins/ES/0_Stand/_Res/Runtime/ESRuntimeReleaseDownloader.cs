using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ES
{
    [Serializable] public sealed class ESRuntimeReleaseManifest { public int formatVersion; public string platform, releaseVersion, channel, publishedUtc, totalConsumerUrl, totalConsumerSha256, bundleIndexUrl, bundleIndexSha256; }
    [Serializable] public sealed class ESRuntimeConsumerReference { public string consumerId, consumerUrl, consumerSha256; }
    [Serializable] public sealed class ESRuntimeConsumerLibraryReference { public string libraryName, libraryFolder, libraryIdentityUrl, libraryIdentitySha256; public bool requiredAtBoot; }
    [Serializable] public sealed class ESRuntimeConsumerCodePackageReference { public string packageKey, kind, fileName, url, sha256, notes; public long size; public bool requiredAtBoot; public int loadOrder; }
    [Serializable] public sealed class ESRuntimeConsumerGameCoreReference { public string guid; public long localFileId; public List<ESRuntimeConsumerGameCoreDependencyReference> dependencies = new List<ESRuntimeConsumerGameCoreDependencyReference>(); public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeConsumerGameCoreDependencyReference { public string guid; public long localFileId; public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeConsumerManifest
    {
        public string consumerId, name, description, maintainer, releaseNotes, version, platform, channel, publishedUtc;
        public bool isTotalConsumer;
        public List<string> tags = new List<string>();
        public List<ESRuntimeConsumerReference> requiredConsumers = new List<ESRuntimeConsumerReference>();
        public List<ESRuntimeConsumerLibraryReference> libraries = new List<ESRuntimeConsumerLibraryReference>();
        public List<ESRuntimeConsumerGameCoreReference> gameCoreAssets = new List<ESRuntimeConsumerGameCoreReference>();
        public List<ESRuntimeConsumerCodePackageReference> codePackages = new List<ESRuntimeConsumerCodePackageReference>();
    }
    [Serializable] public sealed class ESRuntimeLibraryIdentity { public int formatVersion; public string libraryName, libraryFolder, platform, version, channel, catalogUrl, assetBundleManifestUrl, catalogSha256, assetBundleManifestSha256; }
    [Serializable] public sealed class ESRuntimeCatalogIdentity { public string guid; public long localFileId; public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeCatalogEntry
    {
        public ESRuntimeCatalogIdentity identity = new ESRuntimeCatalogIdentity();
        public string assetTypeName, kind, stringKey, libraryName, libraryFolder, pageName, subAssetName;
        public int enumKey;
        public bool isBusinessAsset;
    }
    [Serializable] public sealed class ESRuntimeCatalog { public string libraryName, libraryFolder; public List<ESRuntimeCatalogEntry> assets = new List<ESRuntimeCatalogEntry>(); }
    [Serializable] public sealed class ESRuntimeBundleRecord { public string assetBundleKey, fileName, unityHash, sha256, localRelativePath; public uint crc; public long size; public List<string> dependencies = new List<string>(); }
    [Serializable] public sealed class ESRuntimeReleaseMainAssetRecord { public string guid, assetBundleKey, internalName, typeName; }
    [Serializable] public sealed class ESRuntimeReleaseSubAssetRecord { public string guid, assetBundleKey, internalName, subAssetName, typeName; public long localFileId; }
    [Serializable] public sealed class ESRuntimeBundleManifest { public int formatVersion; public string platform, libraryName; public List<ESRuntimeBundleRecord> assetBundles = new List<ESRuntimeBundleRecord>(); public List<ESRuntimeReleaseMainAssetRecord> mainAssetsByGuid = new List<ESRuntimeReleaseMainAssetRecord>(); public List<ESRuntimeReleaseSubAssetRecord> subAssetsById = new List<ESRuntimeReleaseSubAssetRecord>(); }
    [Serializable] public sealed class ESRuntimeReleaseBundleRecord { public string libraryFolder, assetBundleKey, fileUrl, sha256, localRelativePath; public uint crc; public long size; public List<string> dependencies = new List<string>(); }
    [Serializable] public sealed class ESRuntimeReleaseBundleIndex { public int formatVersion; public string platform, releaseVersion; public List<ESRuntimeReleaseBundleRecord> assetBundles = new List<ESRuntimeReleaseBundleRecord>(); }
    [Serializable] internal sealed class ESRuntimeVerifiedFile { public string relativePath, sha256; public long size; }
    [Serializable] internal sealed class ESRuntimeVerifiedFileIndex { public string releaseVersion; public List<ESRuntimeVerifiedFile> files = new List<ESRuntimeVerifiedFile>(); }

    public enum ESRuntimeReleaseDownloadStage { ReadingRelease, ReadingConsumer, ReadingLibraryIdentity, ReadingCatalog, ReadingAssetBundleManifest, VerifyingAssetBundle, Completed }
    public readonly struct ESRuntimeReleaseDownloadProgress
    {
        public readonly ESRuntimeReleaseDownloadStage Stage;
        public readonly string Subject;
        public readonly int CompletedCount;
        public readonly int TotalCount;
        public ESRuntimeReleaseDownloadProgress(ESRuntimeReleaseDownloadStage stage, string subject, int completedCount = 0, int totalCount = 0)
        {
            Stage = stage; Subject = subject ?? string.Empty; CompletedCount = completedCount; TotalCount = totalCount;
        }
    }

    public sealed class ESRuntimeReleaseDownloadResult
    {
        public ESGlobalAssetRuntimeMap RuntimeMap { get; internal set; }
        public string ReleaseVersion { get; internal set; }
        public IReadOnlyList<string> DownloadedLibraries { get; internal set; }
        public IReadOnlyList<ESRuntimeCatalog> Catalogs { get; internal set; }
        public IReadOnlyList<ESRuntimeDownloadedCodePackage> DownloadedCodePackages { get; internal set; }
        public IReadOnlyList<ESRuntimeConsumerGameCoreReference> GameCoreAssets { get; internal set; }
    }

    public sealed class ESRuntimeDownloadedCodePackage
    {
        public string OwnerConsumerId { get; internal set; }
        public string PackageKey { get; internal set; }
        public string Kind { get; internal set; }
        public string LocalPath { get; internal set; }
        public string Sha256 { get; internal set; }
        public string Notes { get; internal set; }
        public long Size { get; internal set; }
        public int LoadOrder { get; internal set; }
    }

    /// <summary>新版 Root → Consumer → Library → Manifest → AB 下载链；与旧 GameIdentity 管线完全独立。</summary>
    public sealed class ESRuntimeReleaseDownloader
    {
        private sealed class ReleaseContext
        {
            public ESRuntimeReleaseManifest Root;
            public Dictionary<string, ESRuntimeReleaseBundleRecord> BundlesByKey;
        }
        private const int ReleaseProtocolFormatVersion = 2;
        private const int MaxAttempts = 3;
        private readonly ESGlobalResSetting settings;
        private readonly string platform;
        private readonly string cacheRoot;
        private readonly ESAssetRunMode runMode;
        private readonly bool useLocalReleaseSource;
        private readonly string localReleaseRoot;
        private readonly Dictionary<string, ESRuntimeVerifiedFile> verified = new Dictionary<string, ESRuntimeVerifiedFile>(StringComparer.Ordinal);
        private string verifiedReleaseVersion;
        public event Action<ESRuntimeReleaseDownloadProgress> ProgressChanged;

        public ESRuntimeReleaseDownloader(ESGlobalResSetting globalSettings, ESAssetRunMode lockedRunMode)
        {
            settings = globalSettings ? globalSettings : throw new ArgumentNullException(nameof(globalSettings));
            if (lockedRunMode != ESAssetRunMode.LocalBuild && lockedRunMode != ESAssetRunMode.HotUpdate)
                throw new ArgumentOutOfRangeException(nameof(lockedRunMode), lockedRunMode, "Release downloader only supports LocalBuild and HotUpdate.");

            runMode = lockedRunMode;
            platform = ESAssetBundleUtility.GetBuildPlatformName(settings.applyPlatform);
            cacheRoot = Path.Combine(Application.persistentDataPath, settings.Path_Sub_DownloadRelative_, "ReleaseV2", platform);
            useLocalReleaseSource = runMode == ESAssetRunMode.LocalBuild;
            localReleaseRoot = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + ESGlobalResSetting.ResParentFolderName;
        }

        public UniTask<ESRuntimeReleaseDownloadResult> DownloadBootAsync(CancellationToken cancellationToken = default)
        {
            return DownloadBootCoreAsync(false, cancellationToken);
        }

        /// <summary>
        /// 从 Root -> TotalConsumer 的受签名链中定位并下载一个 Consumer。
        /// 这是业务侧的首选入口；不会接受调用方拼出的 URL 或 Hash。
        /// </summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> DownloadConsumerAsync(string consumerId, CancellationToken cancellationToken = default)
        {
            string requestedId = SafePathSegment(consumerId, "ConsumerId");
            ReleaseContext context = await LoadReleaseContextAsync(cancellationToken);
            ESRuntimeConsumerManifest total = await DownloadTotalConsumerManifestAsync(context.Root, cancellationToken);
            ESRuntimeConsumerManifest consumer = await FindConsumerAsync(total, requestedId, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
            if (consumer == null) throw new KeyNotFoundException("Consumer is not declared by TotalConsumer: " + requestedId);
            return await DownloadConsumerContentAsync(consumer, context, false, cancellationToken);
        }

        /// <summary>独立下载并验证一个 Consumer，以及它声明的必需 Consumer、Library、GameCore 与 AB 依赖闭包。</summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> DownloadConsumerAsync(ESRuntimeConsumerReference consumerReference, CancellationToken cancellationToken = default)
        {
            if (consumerReference == null || string.IsNullOrWhiteSpace(consumerReference.consumerUrl) || string.IsNullOrWhiteSpace(consumerReference.consumerSha256))
                throw new ArgumentException("Consumer 引用缺少 URL 或 SHA-256。", nameof(consumerReference));
            ReleaseContext context = await LoadReleaseContextAsync(cancellationToken);
            Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, consumerReference.consumerId);
            var consumer = await DownloadJsonAsync<ESRuntimeConsumerManifest>(consumerReference.consumerUrl,
                Path.Combine(cacheRoot, "Consumers", SafePathSegment(consumerReference.consumerId, "ConsumerId") + ".json"), consumerReference.consumerSha256, cancellationToken);
            if (consumer == null || !string.Equals(consumer.consumerId, consumerReference.consumerId, StringComparison.Ordinal))
                throw new InvalidDataException("Consumer manifest identity does not match its signed reference: " + consumerReference.consumerId);
            return await DownloadConsumerContentAsync(consumer, context, false, cancellationToken);
        }

        /// <summary>从指定 Consumer（包括其必需 Consumer）中按 LibraryFolder 精确下载并验证一个 Library。</summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> DownloadLibraryAsync(string consumerId, string libraryFolder, CancellationToken cancellationToken = default)
        {
            string requestedConsumerId = SafePathSegment(consumerId, "ConsumerId");
            string requestedLibraryFolder = SafePathSegment(libraryFolder, "Library folder");
            ReleaseContext context = await LoadReleaseContextAsync(cancellationToken);
            ESRuntimeConsumerManifest total = await DownloadTotalConsumerManifestAsync(context.Root, cancellationToken);
            ESRuntimeConsumerManifest consumer = await FindConsumerAsync(total, requestedConsumerId, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
            if (consumer == null) throw new KeyNotFoundException("Consumer is not declared by TotalConsumer: " + requestedConsumerId);
            ESRuntimeConsumerLibraryReference library = await FindLibraryAsync(consumer, requestedLibraryFolder, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
            if (library == null) throw new KeyNotFoundException("Library is not declared by Consumer: " + requestedLibraryFolder);
            return await DownloadLibraryAsync(library, context, cancellationToken);
        }

        /// <summary>独立下载并验证一个 Library，以及其 AssetBundle 依赖闭包。</summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> DownloadLibraryAsync(ESRuntimeConsumerLibraryReference libraryReference, CancellationToken cancellationToken = default)
        {
            if (libraryReference == null) throw new ArgumentNullException(nameof(libraryReference));
            ReleaseContext context = await LoadReleaseContextAsync(cancellationToken);
            return await DownloadLibraryAsync(libraryReference, context, cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadLibraryAsync(ESRuntimeConsumerLibraryReference libraryReference, ReleaseContext context, CancellationToken cancellationToken)
        {
            var records = new List<ESRuntimeAssetBundleRecord>();
            var mainAssets = new List<ESRuntimeAssetRecord>();
            var subAssets = new List<ESRuntimeSubAssetRecord>();
            var catalogs = new List<ESRuntimeCatalog>();
            var roots = new HashSet<string>(StringComparer.Ordinal);
            await DownloadLibraryAsync(libraryReference, context.Root.releaseVersion, context.BundlesByKey, roots, mainAssets, subAssets, catalogs, cancellationToken);
            await DownloadAssetBundleClosureAsync(roots, context.BundlesByKey, records, cancellationToken);
            if (!useLocalReleaseSource) SaveVerifiedIndex();
            return CreateResult(context.Root.releaseVersion, new[] { libraryReference.libraryFolder }, records, mainAssets, subAssets, catalogs, Array.Empty<ESRuntimeDownloadedCodePackage>(), Array.Empty<ESRuntimeConsumerGameCoreReference>());
        }

        internal UniTask<ESRuntimeReleaseDownloadResult> DownloadBootAndInitializeCodeAsync(CancellationToken cancellationToken = default)
        {
            return DownloadBootCoreAsync(true, cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadBootCoreAsync(bool initializeCodePackages, CancellationToken cancellationToken)
        {
            string rootUrl = useLocalReleaseSource
                ? CombineLocalReleasePath(platform + "/ESAssetReleaseManifest.json")
                : CombineUrl(settings.Path_Net, platform + "/ESAssetReleaseManifest.json");
            var root = await DownloadJsonAsync<ESRuntimeReleaseManifest>(rootUrl, Path.Combine(cacheRoot, "ESAssetReleaseManifest.json"), null, cancellationToken);
            ValidateFormat(root?.formatVersion, "RootReleaseManifest");
            if (root == null || string.IsNullOrWhiteSpace(root.releaseVersion)) throw new InvalidDataException("RootReleaseManifest 缺少发布版本。");
            if (!string.Equals(root.platform, platform, StringComparison.Ordinal)) throw new InvalidDataException("RootReleaseManifest 平台不匹配：" + root.platform + " / " + platform);
            if (!useLocalReleaseSource) PrepareVerifiedIndex(root.releaseVersion);
            if (root == null || string.IsNullOrEmpty(root.bundleIndexUrl) || string.IsNullOrEmpty(root.bundleIndexSha256)) throw new InvalidDataException("RootReleaseManifest 缺少全局 Bundle 索引定位或 Hash。");
            var bundleIndex = await DownloadJsonAsync<ESRuntimeReleaseBundleIndex>(root.bundleIndexUrl, Path.Combine(cacheRoot, "ESAssetReleaseBundleIndex.json"), root.bundleIndexSha256, cancellationToken);
            ValidateFormat(bundleIndex?.formatVersion, "GlobalAssetBundleIndex");
            var bundlesByKey = ValidateGlobalBundleIndex(root, bundleIndex);
            if (root == null || string.IsNullOrEmpty(root.totalConsumerUrl) || string.IsNullOrEmpty(root.totalConsumerSha256)) throw new InvalidDataException("RootReleaseManifest 缺少 TotalConsumer 定位或 Hash。");
            var total = await DownloadTotalConsumerManifestAsync(root, cancellationToken);
            var libraries = new Dictionary<string, ESRuntimeConsumerLibraryReference>(StringComparer.Ordinal);
            var codePackages = new Dictionary<string, CollectedCodePackage>(StringComparer.Ordinal);
            var gameCoreAssets = new Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference>();
            await CollectConsumerContentAsync(total, libraries, codePackages, gameCoreAssets, new HashSet<string>(StringComparer.Ordinal), true, cancellationToken);
            var assetBundleRecords = new List<ESRuntimeAssetBundleRecord>();
            var mainAssets = new List<ESRuntimeAssetRecord>();
            var subAssets = new List<ESRuntimeSubAssetRecord>();
            var catalogs = new List<ESRuntimeCatalog>();
            var requiredAssetBundleKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var library in libraries.Values.OrderBy(item => item.libraryFolder, StringComparer.Ordinal))
                await DownloadLibraryAsync(library, root.releaseVersion, bundlesByKey, requiredAssetBundleKeys, mainAssets, subAssets, catalogs, cancellationToken);
            var downloadedCodePackages = new List<ESRuntimeDownloadedCodePackage>();
            foreach (CollectedCodePackage codePackage in codePackages.Values.OrderBy(item => item.Reference.loadOrder).ThenBy(item => item.Reference.packageKey, StringComparer.Ordinal))
                downloadedCodePackages.Add(await DownloadCodePackageAsync(codePackage, cancellationToken));
            if (initializeCodePackages)
                await ESRuntimeCodePackageBootstrap.LoadAsync(downloadedCodePackages, cancellationToken);
            await DownloadAssetBundleClosureAsync(requiredAssetBundleKeys, bundlesByKey, assetBundleRecords, cancellationToken);
            if (!useLocalReleaseSource)
                SaveVerifiedIndex();
            var runtimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            runtimeMap.SetRecords(assetBundleRecords.ToArray(), mainAssets.ToArray(), subAssets.ToArray());
            return new ESRuntimeReleaseDownloadResult
            {
                RuntimeMap = runtimeMap,
                ReleaseVersion = root.releaseVersion,
                DownloadedLibraries = libraries.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                Catalogs = catalogs,
                DownloadedCodePackages = downloadedCodePackages,
                GameCoreAssets = gameCoreAssets.Values.ToArray()
            };
        }

        private async UniTask<ReleaseContext> LoadReleaseContextAsync(CancellationToken token)
        {
            Report(ESRuntimeReleaseDownloadStage.ReadingRelease, "RootReleaseManifest");
            string rootUrl = useLocalReleaseSource ? CombineLocalReleasePath(platform + "/ESAssetReleaseManifest.json") : CombineUrl(settings.Path_Net, platform + "/ESAssetReleaseManifest.json");
            var root = await DownloadJsonAsync<ESRuntimeReleaseManifest>(rootUrl, Path.Combine(cacheRoot, "ESAssetReleaseManifest.json"), null, token);
            ValidateFormat(root?.formatVersion, "RootReleaseManifest");
            if (root == null || string.IsNullOrWhiteSpace(root.releaseVersion) || !string.Equals(root.platform, platform, StringComparison.Ordinal))
                throw new InvalidDataException("RootReleaseManifest 无效或平台不匹配。");
            if (!useLocalReleaseSource) PrepareVerifiedIndex(root.releaseVersion);
            if (string.IsNullOrEmpty(root.bundleIndexUrl) || string.IsNullOrEmpty(root.bundleIndexSha256))
                throw new InvalidDataException("RootReleaseManifest 缺少全局 Bundle 索引。");
            var index = await DownloadJsonAsync<ESRuntimeReleaseBundleIndex>(root.bundleIndexUrl, Path.Combine(cacheRoot, "ESAssetReleaseBundleIndex.json"), root.bundleIndexSha256, token);
            ValidateFormat(index?.formatVersion, "GlobalAssetBundleIndex");
            return new ReleaseContext { Root = root, BundlesByKey = ValidateGlobalBundleIndex(root, index) };
        }

        private async UniTask<ESRuntimeConsumerManifest> DownloadTotalConsumerManifestAsync(ESRuntimeReleaseManifest root, CancellationToken token)
        {
            if (root == null || string.IsNullOrEmpty(root.totalConsumerUrl) || string.IsNullOrEmpty(root.totalConsumerSha256))
                throw new InvalidDataException("RootReleaseManifest is missing TotalConsumer location or hash.");
            Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, "TotalConsumer");
            var total = await DownloadJsonAsync<ESRuntimeConsumerManifest>(root.totalConsumerUrl, Path.Combine(cacheRoot, "Consumers", "total.json"), root.totalConsumerSha256, token);
            if (total == null || !total.isTotalConsumer || string.IsNullOrWhiteSpace(total.consumerId))
                throw new InvalidDataException("TotalConsumerManifest is invalid.");
            return total;
        }

        private async UniTask<ESRuntimeConsumerManifest> FindConsumerAsync(ESRuntimeConsumerManifest current, string consumerId, HashSet<string> visited, CancellationToken token)
        {
            if (current == null || !visited.Add(current.consumerId)) return null;
            if (string.Equals(current.consumerId, consumerId, StringComparison.Ordinal)) return current;
            foreach (ESRuntimeConsumerReference reference in current.requiredConsumers ?? new List<ESRuntimeConsumerReference>())
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.consumerUrl) || string.IsNullOrWhiteSpace(reference.consumerSha256))
                    throw new InvalidDataException("Consumer dependency reference is incomplete: " + current.consumerId);
                string childId = SafePathSegment(reference.consumerId, "ConsumerId");
                Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, childId);
                var child = await DownloadJsonAsync<ESRuntimeConsumerManifest>(reference.consumerUrl, Path.Combine(cacheRoot, "Consumers", childId + ".json"), reference.consumerSha256, token);
                if (child == null || !string.Equals(child.consumerId, childId, StringComparison.Ordinal))
                    throw new InvalidDataException("Consumer dependency manifest identity does not match its signed reference: " + childId);
                ESRuntimeConsumerManifest found = await FindConsumerAsync(child, consumerId, visited, token);
                if (found != null) return found;
            }
            return null;
        }

        private async UniTask<ESRuntimeConsumerLibraryReference> FindLibraryAsync(ESRuntimeConsumerManifest current, string libraryFolder, HashSet<string> visited, CancellationToken token)
        {
            if (current == null || !visited.Add(current.consumerId)) return null;
            foreach (ESRuntimeConsumerLibraryReference library in current.libraries ?? new List<ESRuntimeConsumerLibraryReference>())
                if (library != null && string.Equals(SafePathSegment(library.libraryFolder, "Library folder"), libraryFolder, StringComparison.Ordinal))
                    return library;
            foreach (ESRuntimeConsumerReference reference in current.requiredConsumers ?? new List<ESRuntimeConsumerReference>())
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.consumerUrl) || string.IsNullOrWhiteSpace(reference.consumerSha256))
                    throw new InvalidDataException("Consumer dependency reference is incomplete: " + current.consumerId);
                string childId = SafePathSegment(reference.consumerId, "ConsumerId");
                Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, childId);
                var child = await DownloadJsonAsync<ESRuntimeConsumerManifest>(reference.consumerUrl, Path.Combine(cacheRoot, "Consumers", childId + ".json"), reference.consumerSha256, token);
                if (child == null || !string.Equals(child.consumerId, childId, StringComparison.Ordinal))
                    throw new InvalidDataException("Consumer dependency manifest identity does not match its signed reference: " + childId);
                ESRuntimeConsumerLibraryReference found = await FindLibraryAsync(child, libraryFolder, visited, token);
                if (found != null) return found;
            }
            return null;
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadConsumerContentAsync(ESRuntimeConsumerManifest consumer, ReleaseContext context, bool requiredAtBootOnly, CancellationToken token)
        {
            var libraries = new Dictionary<string, ESRuntimeConsumerLibraryReference>(StringComparer.Ordinal);
            var codePackages = new Dictionary<string, CollectedCodePackage>(StringComparer.Ordinal);
            var gameCoreAssets = new Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference>();
            await CollectConsumerContentAsync(consumer, libraries, codePackages, gameCoreAssets, new HashSet<string>(StringComparer.Ordinal), requiredAtBootOnly, token);
            var records = new List<ESRuntimeAssetBundleRecord>();
            var mainAssets = new List<ESRuntimeAssetRecord>();
            var subAssets = new List<ESRuntimeSubAssetRecord>();
            var catalogs = new List<ESRuntimeCatalog>();
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESRuntimeConsumerLibraryReference library in libraries.Values.OrderBy(item => item.libraryFolder, StringComparer.Ordinal))
                await DownloadLibraryAsync(library, context.Root.releaseVersion, context.BundlesByKey, roots, mainAssets, subAssets, catalogs, token);
            await DownloadAssetBundleClosureAsync(roots, context.BundlesByKey, records, token);
            var downloadedCode = new List<ESRuntimeDownloadedCodePackage>();
            foreach (CollectedCodePackage code in codePackages.Values.OrderBy(item => item.Reference.loadOrder).ThenBy(item => item.Reference.packageKey, StringComparer.Ordinal))
                downloadedCode.Add(await DownloadCodePackageAsync(code, token));
            if (!useLocalReleaseSource) SaveVerifiedIndex();
            return CreateResult(context.Root.releaseVersion, libraries.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray(), records, mainAssets, subAssets, catalogs, downloadedCode, gameCoreAssets.Values.ToArray());
        }

        private static ESRuntimeReleaseDownloadResult CreateResult(string version, IReadOnlyList<string> libraries, List<ESRuntimeAssetBundleRecord> bundles, List<ESRuntimeAssetRecord> mainAssets, List<ESRuntimeSubAssetRecord> subAssets, List<ESRuntimeCatalog> catalogs, IReadOnlyList<ESRuntimeDownloadedCodePackage> codePackages, IReadOnlyList<ESRuntimeConsumerGameCoreReference> gameCoreAssets)
        {
            var runtimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            runtimeMap.SetRecords(bundles.ToArray(), mainAssets.ToArray(), subAssets.ToArray());
            return new ESRuntimeReleaseDownloadResult { RuntimeMap = runtimeMap, ReleaseVersion = version, DownloadedLibraries = libraries, Catalogs = catalogs, DownloadedCodePackages = codePackages, GameCoreAssets = gameCoreAssets };
        }

        private async UniTask CollectConsumerContentAsync(ESRuntimeConsumerManifest consumer, Dictionary<string, ESRuntimeConsumerLibraryReference> libraries,
            Dictionary<string, CollectedCodePackage> codePackages, Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference> gameCoreAssets, HashSet<string> visitedConsumers, bool requiredAtBootOnly, CancellationToken token)
        {
            if (consumer == null || string.IsNullOrWhiteSpace(consumer.consumerId)) throw new InvalidDataException("Consumer Manifest 缺少稳定 ID。");
            if (!visitedConsumers.Add(consumer.consumerId)) return;
            foreach (var dependency in consumer.requiredConsumers ?? new List<ESRuntimeConsumerReference>())
            {
                if (string.IsNullOrEmpty(dependency.consumerUrl) || string.IsNullOrEmpty(dependency.consumerSha256)) throw new InvalidDataException("Consumer 依赖缺少 URL 或 Hash。");
                string childId = SafePathSegment(dependency.consumerId, "ConsumerId");
                Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, childId);
                var child = await DownloadJsonAsync<ESRuntimeConsumerManifest>(dependency.consumerUrl, Path.Combine(cacheRoot, "Consumers", childId + ".json"), dependency.consumerSha256, token);
                if (child == null || !string.Equals(child.consumerId, dependency.consumerId, StringComparison.Ordinal))
                    throw new InvalidDataException("Consumer dependency manifest identity does not match its signed reference: " + dependency.consumerId);
                await CollectConsumerContentAsync(child, libraries, codePackages, gameCoreAssets, visitedConsumers, requiredAtBootOnly, token);
            }
            foreach (var library in consumer.libraries ?? new List<ESRuntimeConsumerLibraryReference>())
            {
                if (library == null) throw new InvalidDataException("Consumer contains a null library reference: " + consumer.consumerId);
                string libraryFolder = SafePathSegment(library.libraryFolder, "Library folder");
                if (requiredAtBootOnly && !library.requiredAtBoot) continue;
                if (libraries.TryGetValue(libraryFolder, out ESRuntimeConsumerLibraryReference existing)
                    && (!string.Equals(existing.libraryIdentityUrl, library.libraryIdentityUrl, StringComparison.Ordinal)
                        || !string.Equals(existing.libraryIdentitySha256, library.libraryIdentitySha256, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Consumer graph has conflicting signed Library references: " + libraryFolder);
                libraries[libraryFolder] = library;
            }
            foreach (ESRuntimeConsumerGameCoreReference asset in consumer.gameCoreAssets ?? new List<ESRuntimeConsumerGameCoreReference>())
                if (asset != null && asset.IsValid)
                {
                    var id = new ESAssetIdentity(asset.guid, asset.localFileId);
                    if (!gameCoreAssets.TryGetValue(id, out ESRuntimeConsumerGameCoreReference existing))
                    {
                        gameCoreAssets.Add(id, asset);
                    }
                    else
                    {
                        existing.dependencies ??= new List<ESRuntimeConsumerGameCoreDependencyReference>();
                        foreach (ESRuntimeConsumerGameCoreDependencyReference dependency in asset.dependencies ?? new List<ESRuntimeConsumerGameCoreDependencyReference>())
                            if (dependency != null && dependency.IsValid
                                && !existing.dependencies.Any(item => item != null && item.guid == dependency.guid && item.localFileId == dependency.localFileId))
                                existing.dependencies.Add(dependency);
                    }
                }
            foreach (ESRuntimeConsumerCodePackageReference codePackage in consumer.codePackages ?? new List<ESRuntimeConsumerCodePackageReference>())
            {
                if (codePackage == null || (requiredAtBootOnly && !codePackage.requiredAtBoot)) continue;
                ValidateCodePackage(codePackage, consumer.consumerId);
                if (codePackages.TryGetValue(codePackage.packageKey, out CollectedCodePackage existing))
                {
                    if (!IsSameCodePackage(existing.Reference, codePackage))
                        throw new InvalidDataException("启动文件名称冲突：" + codePackage.packageKey + "，请检查 Consumer 配置。");
                    continue;
                }
                codePackages.Add(codePackage.packageKey, new CollectedCodePackage { OwnerConsumerId = consumer.consumerId, Reference = codePackage });
            }
        }

        private async UniTask<ESRuntimeDownloadedCodePackage> DownloadCodePackageAsync(CollectedCodePackage collected, CancellationToken token)
        {
            ESRuntimeConsumerCodePackageReference package = collected.Reference;
            string ownerConsumerId = SafePathSegment(collected.OwnerConsumerId, "ConsumerId");
            string fileName = SafePathSegment(package.fileName, "启动文件名");
            string relativePath = Path.Combine("Code", ownerConsumerId, fileName).Replace('\\', '/');
            string localPath = Path.Combine(cacheRoot, "Code", ownerConsumerId, fileName);
            if (useLocalReleaseSource)
            {
                string streamingSource = ResolveLocalReleasePath(package.url);
                if (IsFilePath(streamingSource))
                {
                    if (!File.Exists(streamingSource) || new FileInfo(streamingSource).Length != package.size || !ESResManifestIntegrity.VerifyFileSha256(streamingSource, package.sha256))
                        throw new InvalidDataException("StreamingAssets code package is invalid: " + streamingSource);
                    localPath = streamingSource;
                }
                else
                {
                    // 非文件型 StreamingAssets 无法直接作为程序集路径；仅代码包需要落地到缓存。
                    await EnsureFileAsync(package.url, localPath, relativePath, package.size, package.sha256, token);
                }
            }
            else
            {
                await EnsureFileAsync(package.url, localPath, relativePath, package.size, package.sha256, token);
            }
            return new ESRuntimeDownloadedCodePackage
            {
                OwnerConsumerId = collected.OwnerConsumerId,
                PackageKey = package.packageKey,
                Kind = package.kind,
                LocalPath = localPath,
                Sha256 = package.sha256,
                Notes = package.notes,
                Size = package.size,
                LoadOrder = package.loadOrder
            };
        }

        private static void ValidateCodePackage(ESRuntimeConsumerCodePackageReference package, string consumerId)
        {
            if (string.IsNullOrWhiteSpace(package.packageKey)) throw new InvalidDataException("Consumer“" + consumerId + "”存在未命名的启动文件。");
            if (string.IsNullOrWhiteSpace(package.kind)) throw new InvalidDataException("启动文件缺少类型：" + package.packageKey);
            if (string.IsNullOrWhiteSpace(package.fileName)) throw new InvalidDataException("启动文件缺少文件名：" + package.packageKey);
            if (string.IsNullOrWhiteSpace(package.url)) throw new InvalidDataException("启动文件缺少下载地址：" + package.packageKey);
            if (string.IsNullOrWhiteSpace(package.sha256)) throw new InvalidDataException("启动文件缺少完整性信息：" + package.packageKey);
            if (package.size < 0) throw new InvalidDataException("启动文件大小无效：" + package.packageKey);
        }

        private static bool IsSameCodePackage(ESRuntimeConsumerCodePackageReference left, ESRuntimeConsumerCodePackageReference right)
        {
            return left.size == right.size
                && string.Equals(left.kind, right.kind, StringComparison.Ordinal)
                && string.Equals(left.url, right.url, StringComparison.Ordinal)
                && string.Equals(left.sha256, right.sha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string SafePathSegment(string value, string fieldName)
        {
            string segment = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(segment) || !string.Equals(segment, Path.GetFileName(segment), StringComparison.Ordinal)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment == "." || segment == "..")
                throw new InvalidDataException(fieldName + " 不是合法路径片段：" + value);
            return segment;
        }

        private static string NormalizeAssetBundleRelativePath(string value)
        {
            const string assetBundlesPrefix = "AssetBundles/";
            string normalized = (value ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith(assetBundlesPrefix, StringComparison.Ordinal))
                throw new InvalidDataException("AB 文件必须位于 AssetBundles/ 目录：" + value);

            string fileName = SafePathSegment(normalized.Substring(assetBundlesPrefix.Length), "AssetBundle file name");
            return assetBundlesPrefix + fileName;
        }

        private async UniTask DownloadLibraryAsync(ESRuntimeConsumerLibraryReference library, string releaseVersion, Dictionary<string, ESRuntimeReleaseBundleRecord> bundlesByKey, HashSet<string> requiredAssetBundleKeys, List<ESRuntimeAssetRecord> mainAssets, List<ESRuntimeSubAssetRecord> subAssets, List<ESRuntimeCatalog> catalogs, CancellationToken token)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            string libraryFolder = SafePathSegment(library.libraryFolder, "Library folder");
            if (string.IsNullOrEmpty(library.libraryIdentityUrl) || string.IsNullOrEmpty(library.libraryIdentitySha256)) throw new InvalidDataException("Library 缺少 Identity URL 或 Hash：" + library.libraryFolder);
            string libraryRoot = Path.Combine(cacheRoot, "Libraries", libraryFolder);
            Report(ESRuntimeReleaseDownloadStage.ReadingLibraryIdentity, libraryFolder);
            var identity = await DownloadJsonAsync<ESRuntimeLibraryIdentity>(library.libraryIdentityUrl, Path.Combine(libraryRoot, "ESAssetLibraryIdentity.json"), library.libraryIdentitySha256, token);
            ValidateFormat(identity?.formatVersion, "LibraryIdentity");
            if (identity == null || !string.Equals(identity.libraryFolder, libraryFolder, StringComparison.Ordinal)
                || !string.Equals(identity.platform, platform, StringComparison.Ordinal) || !string.Equals(identity.version, releaseVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Library Identity 与当前发布不匹配：" + library.libraryFolder);
            if (string.IsNullOrWhiteSpace(identity.catalogUrl) || string.IsNullOrWhiteSpace(identity.catalogSha256)
                || string.IsNullOrWhiteSpace(identity.assetBundleManifestUrl) || string.IsNullOrWhiteSpace(identity.assetBundleManifestSha256))
                throw new InvalidDataException("Library Identity 缺少索引定位或完整性信息：" + library.libraryFolder);
            Report(ESRuntimeReleaseDownloadStage.ReadingCatalog, libraryFolder);
            var catalog = await DownloadJsonAsync<ESRuntimeCatalog>(identity.catalogUrl, Path.Combine(libraryRoot, "ESAssetLibraryCatalog.json"), identity.catalogSha256, token);
            if (catalog == null || catalog.assets == null) throw new InvalidDataException("Catalog 解析失败：" + library.libraryFolder);
            if (!string.IsNullOrEmpty(catalog.libraryFolder) && !string.Equals(catalog.libraryFolder, libraryFolder, StringComparison.Ordinal)) throw new InvalidDataException("Catalog Library 身份不匹配：" + library.libraryFolder);
            catalogs.Add(catalog);
            Report(ESRuntimeReleaseDownloadStage.ReadingAssetBundleManifest, libraryFolder);
            var manifest = await DownloadJsonAsync<ESRuntimeBundleManifest>(identity.assetBundleManifestUrl, Path.Combine(libraryRoot, "ESAssetBundleManifest.json"), identity.assetBundleManifestSha256, token);
            ValidateFormat(manifest?.formatVersion, "LibraryAssetBundleManifest");
            if (manifest == null) throw new InvalidDataException("Library ABManifest 解析失败：" + library.libraryFolder);
            if (!string.Equals(manifest.platform, platform, StringComparison.Ordinal)) throw new InvalidDataException("Library ABManifest 平台不匹配：" + library.libraryFolder);
            var ownedAssetBundleKeys = new HashSet<string>((manifest.assetBundles ?? new List<ESRuntimeBundleRecord>()).Select(item => item.assetBundleKey), StringComparer.Ordinal);
            if (ownedAssetBundleKeys.Count != (manifest.assetBundles ?? new List<ESRuntimeBundleRecord>()).Count) throw new InvalidDataException("ABManifest AssetBundleKey 重复：" + library.libraryFolder);
            var availableAssetBundleKeys = new HashSet<string>(ownedAssetBundleKeys, StringComparer.Ordinal);
            availableAssetBundleKeys.UnionWith(bundlesByKey.Keys);
            foreach (var bundle in manifest.assetBundles ?? new List<ESRuntimeBundleRecord>())
                foreach (var dependency in bundle.dependencies ?? new List<string>())
                    if (!availableAssetBundleKeys.Contains(dependency)) throw new InvalidDataException("ABManifest 依赖缺失：" + bundle.assetBundleKey + " -> " + dependency);
            foreach (var bundle in manifest.assetBundles ?? new List<ESRuntimeBundleRecord>())
            {
                if (!bundlesByKey.TryGetValue(bundle.assetBundleKey, out ESRuntimeReleaseBundleRecord indexed)) throw new InvalidDataException("Global Bundle index is missing package: " + bundle.assetBundleKey);
                if (!string.Equals(indexed.libraryFolder, libraryFolder, StringComparison.Ordinal) || indexed.size != bundle.size || indexed.crc != bundle.crc
                    || !string.Equals(NormalizeAssetBundleRelativePath(indexed.localRelativePath), NormalizeAssetBundleRelativePath(bundle.localRelativePath), StringComparison.Ordinal)
                    || !string.Equals(indexed.sha256, bundle.sha256, StringComparison.OrdinalIgnoreCase)
                    || !(indexed.dependencies ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal).SequenceEqual((bundle.dependencies ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Global Bundle index differs from Library Manifest: " + bundle.assetBundleKey);
                requiredAssetBundleKeys.Add(bundle.assetBundleKey);
            }
            ValidateCatalogAndAssetRecords(catalog, manifest, ownedAssetBundleKeys);
            foreach (var asset in manifest.mainAssetsByGuid ?? new List<ESRuntimeReleaseMainAssetRecord>()) mainAssets.Add(new ESRuntimeAssetRecord(asset.guid, asset.assetBundleKey, asset.internalName, asset.typeName));
            foreach (var asset in manifest.subAssetsById ?? new List<ESRuntimeReleaseSubAssetRecord>()) subAssets.Add(new ESRuntimeSubAssetRecord(asset.guid, asset.localFileId, asset.assetBundleKey, asset.internalName, asset.subAssetName, asset.typeName));
        }

        private Dictionary<string, ESRuntimeReleaseBundleRecord> ValidateGlobalBundleIndex(ESRuntimeReleaseManifest root, ESRuntimeReleaseBundleIndex index)
        {
            if (index == null || !string.Equals(index.platform, platform, StringComparison.Ordinal)
                || !string.Equals(index.releaseVersion, root.releaseVersion, StringComparison.Ordinal))
                throw new InvalidDataException("全局 Bundle 索引与当前发布不匹配。");

            var result = new Dictionary<string, ESRuntimeReleaseBundleRecord>(StringComparer.Ordinal);
            foreach (ESRuntimeReleaseBundleRecord bundle in index.assetBundles ?? new List<ESRuntimeReleaseBundleRecord>())
            {
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.assetBundleKey) || !result.TryAdd(bundle.assetBundleKey, bundle))
                    throw new InvalidDataException("全局 Bundle 索引包含无效或重复 BundleKey。");
                SafePathSegment(bundle.libraryFolder, "Bundle library folder");
                if (string.IsNullOrWhiteSpace(bundle.fileUrl) || !IsSha256(bundle.sha256) || bundle.size <= 0)
                    throw new InvalidDataException("全局 Bundle 索引记录不完整：" + bundle.assetBundleKey);
                string normalizedPath = NormalizeAssetBundleRelativePath(bundle.localRelativePath);
                if (!string.Equals(normalizedPath, bundle.localRelativePath, StringComparison.Ordinal))
                    throw new InvalidDataException("全局 Bundle 索引路径未规范化：" + bundle.assetBundleKey);
                var dependencySet = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependency in bundle.dependencies ?? new List<string>())
                    if (string.IsNullOrWhiteSpace(dependency) || string.Equals(dependency, bundle.assetBundleKey, StringComparison.Ordinal) || !dependencySet.Add(dependency))
                        throw new InvalidDataException("全局 Bundle 索引依赖无效：" + bundle.assetBundleKey + " -> " + dependency);
            }

            foreach (ESRuntimeReleaseBundleRecord bundle in result.Values)
                foreach (string dependency in bundle.dependencies ?? new List<string>())
                    if (!result.ContainsKey(dependency)) throw new InvalidDataException("全局 Bundle 索引依赖缺失：" + bundle.assetBundleKey + " -> " + dependency);
            ValidateBundleGraph(result);
            return result;
        }

        private static void ValidateBundleGraph(Dictionary<string, ESRuntimeReleaseBundleRecord> bundlesByKey)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key)
            {
                if (visited.Contains(key)) return;
                if (!visiting.Add(key)) throw new InvalidDataException("全局 Bundle 索引存在依赖循环：" + key);
                foreach (string dependency in bundlesByKey[key].dependencies ?? new List<string>()) Visit(dependency);
                visiting.Remove(key);
                visited.Add(key);
            }
            foreach (string key in bundlesByKey.Keys) Visit(key);
        }

        private static void ValidateCatalogAndAssetRecords(ESRuntimeCatalog catalog, ESRuntimeBundleManifest manifest, HashSet<string> ownedBundleKeys)
        {
            var mainByGuid = new Dictionary<string, ESRuntimeReleaseMainAssetRecord>(StringComparer.Ordinal);
            foreach (ESRuntimeReleaseMainAssetRecord asset in manifest.mainAssetsByGuid ?? new List<ESRuntimeReleaseMainAssetRecord>())
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.guid) || string.IsNullOrWhiteSpace(asset.internalName)
                    || !ownedBundleKeys.Contains(asset.assetBundleKey) || !mainByGuid.TryAdd(asset.guid, asset))
                    throw new InvalidDataException("[ESRes][Catalog] Library 主资产文件索引无效：Library=" + manifest.libraryName + ", GUID=" + (asset?.guid ?? "<null>") + ", BundleKey=" + (asset?.assetBundleKey ?? "<null>") + ", InternalName=" + (asset?.internalName ?? "<null>") + ", Type=" + (asset?.typeName ?? "<null>"));
            }

            var subById = new Dictionary<string, ESRuntimeReleaseSubAssetRecord>(StringComparer.Ordinal);
            foreach (ESRuntimeReleaseSubAssetRecord asset in manifest.subAssetsById ?? new List<ESRuntimeReleaseSubAssetRecord>())
            {
                string id = asset == null ? string.Empty : asset.guid + ":" + asset.localFileId;
                if (asset == null || string.IsNullOrWhiteSpace(asset.guid) || asset.localFileId == 0 || string.IsNullOrWhiteSpace(asset.internalName)
                    || string.IsNullOrWhiteSpace(asset.subAssetName) || string.IsNullOrWhiteSpace(asset.typeName)
                    || !ownedBundleKeys.Contains(asset.assetBundleKey) || !subById.TryAdd(id, asset))
                    throw new InvalidDataException("[ESRes][SubAsset] Library 子资产文件索引无效或身份重复：Library=" + manifest.libraryName + ", GUID=" + (asset?.guid ?? "<null>") + ", LocalFileId=" + (asset?.localFileId.ToString() ?? "<null>") + ", BundleKey=" + (asset?.assetBundleKey ?? "<null>") + ", InternalName=" + (asset?.internalName ?? "<null>") + ", Selector=" + (asset?.subAssetName ?? "<null>") + ", Type=" + (asset?.typeName ?? "<null>"));
            }

            foreach (ESRuntimeCatalogEntry entry in catalog.assets ?? new List<ESRuntimeCatalogEntry>())
            {
                if (entry == null || !entry.isBusinessAsset) continue;
                if (entry.identity == null || !entry.identity.IsValid) throw new InvalidDataException("[ESRes][Catalog] Catalog 业务资源身份无效：Library=" + catalog.libraryFolder + ", Page=" + entry.pageName + ", GUID=" + (entry.identity?.guid ?? "<null>") + ", LocalFileId=" + (entry.identity?.localFileId.ToString() ?? "<null>"));
                bool indexed = entry.identity.localFileId == 0
                    ? mainByGuid.ContainsKey(entry.identity.guid)
                    : subById.ContainsKey(entry.identity.guid + ":" + entry.identity.localFileId);
                if (!indexed)
                {
                    string tag = entry.identity.localFileId == 0 ? "[ESRes][Catalog]" : "[ESRes][SubAsset]";
                    throw new InvalidDataException(tag + " Catalog 业务资源未进入文件索引：Library=" + catalog.libraryFolder + ", Page=" + entry.pageName + ", GUID=" + entry.identity.guid + ", LocalFileId=" + entry.identity.localFileId + ", Kind=" + entry.kind + ", Type=" + entry.assetTypeName);
                }
            }
        }

        private static bool IsSha256(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(Uri.IsHexDigit);
        }

        private async UniTask DownloadAssetBundleClosureAsync(IEnumerable<string> roots, Dictionary<string, ESRuntimeReleaseBundleRecord> bundlesByKey, List<ESRuntimeAssetBundleRecord> assetBundles, CancellationToken token)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            async UniTask VisitAsync(string packageKey)
            {
                if (visited.Contains(packageKey)) return;
                if (!visiting.Add(packageKey)) throw new InvalidDataException("Global Bundle dependency cycle: " + packageKey);
                try
                {
                    if (!bundlesByKey.TryGetValue(packageKey, out ESRuntimeReleaseBundleRecord bundle)) throw new InvalidDataException("Global Bundle dependency is missing: " + packageKey);
                    Report(ESRuntimeReleaseDownloadStage.VerifyingAssetBundle, packageKey, visited.Count, roots.Count());
                    foreach (string dependency in bundle.dependencies ?? new List<string>()) await VisitAsync(dependency);
                    string libraryFolder = SafePathSegment(bundle.libraryFolder, "Bundle library folder");
                    string assetBundleRelativePath = NormalizeAssetBundleRelativePath(bundle.localRelativePath);
                    if (useLocalReleaseSource)
                    {
                        string streamingSource = ResolveLocalReleasePath(bundle.fileUrl);
                        if (IsFilePath(streamingSource))
                        {
                            if (!File.Exists(streamingSource) || new FileInfo(streamingSource).Length != bundle.size || !ESResManifestIntegrity.VerifyFileSha256(streamingSource, bundle.sha256))
                                throw new InvalidDataException("StreamingAssets AssetBundle is invalid: " + streamingSource);

                            assetBundles.Add(new ESRuntimeAssetBundleRecord(bundle.assetBundleKey, streamingSource, null, null, Hash128.Compute(bundle.sha256 ?? string.Empty).ToString(), bundle.crc, bundle.size, (bundle.dependencies ?? new List<string>()).ToArray()));
                        }
                        else
                        {
                            // Android/WebGL 等 StreamingAssets 不是普通文件：Provider 用 UnityWebRequest 直接读取，不复制到 persistentDataPath。
                            assetBundles.Add(new ESRuntimeAssetBundleRecord(bundle.assetBundleKey, null, streamingSource, null, Hash128.Compute(bundle.sha256 ?? string.Empty).ToString(), bundle.crc, bundle.size, (bundle.dependencies ?? new List<string>()).ToArray()));
                        }
                    }
                    else
                    {
                        string relativePath = Path.Combine("Libraries", libraryFolder, assetBundleRelativePath).Replace('\\', '/');
                        string localPath = Path.Combine(cacheRoot, "Libraries", libraryFolder, assetBundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
                        await EnsureFileAsync(bundle.fileUrl, localPath, relativePath, bundle.size, bundle.sha256, token);
                        assetBundles.Add(new ESRuntimeAssetBundleRecord(bundle.assetBundleKey, localPath, null, bundle.fileUrl, Hash128.Compute(bundle.sha256 ?? string.Empty).ToString(), bundle.crc, bundle.size, (bundle.dependencies ?? new List<string>()).ToArray()));
                    }
                    visited.Add(packageKey);
                }
                finally
                {
                    visiting.Remove(packageKey);
                }
            }

            foreach (string root in roots.OrderBy(item => item, StringComparer.Ordinal)) await VisitAsync(root);
        }

        private async UniTask<T> DownloadJsonAsync<T>(string url, string localPath, string expectedHash, CancellationToken token) where T : class
        {
            string text = await DownloadTextAsync(url, localPath, expectedHash, token);
            try { return JsonConvert.DeserializeObject<T>(text); }
            catch (Exception exception) { throw new InvalidDataException("JSON 解析失败：" + url, exception); }
        }

        private async UniTask<string> DownloadTextAsync(string url, string localPath, string expectedHash, CancellationToken token)
        {
            if (useLocalReleaseSource)
            {
                string localText = await RequestTextAsync(url, token);
                if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(ESResManifestIntegrity.ComputeFileSha256FromText(localText), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("StreamingAssets 清单 Hash 不匹配：" + url);
                return localText;
            }

            if (!string.IsNullOrEmpty(expectedHash) && File.Exists(localPath) && ESResManifestIntegrity.VerifyFileSha256(localPath, expectedHash)) return File.ReadAllText(localPath);
            string text = await RequestTextAsync(url, token);
            if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(ESResManifestIntegrity.ComputeFileSha256FromText(text), expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("下载文件 Hash 不匹配：" + url);
            WriteTextAtomically(localPath, text);
            return text;
        }

        private async UniTask EnsureFileAsync(string url, string localPath, string relativePath, long expectedSize, string expectedHash, CancellationToken token)
        {
            if (IsVerified(relativePath, localPath, expectedSize, expectedHash)) return;
            if (useLocalReleaseSource)
            {
                string sourcePath = ResolveLocalReleasePath(url);
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                if (IsFilePath(sourcePath))
                {
                    if (!File.Exists(sourcePath) || new FileInfo(sourcePath).Length != expectedSize || !ESResManifestIntegrity.VerifyFileSha256(sourcePath, expectedHash))
                        throw new InvalidDataException("Initial AssetBundle is invalid: " + sourcePath);
                    File.Copy(sourcePath, localPath, true);
                }
                else
                {
                    string localSourcePartPath = localPath + ".part";
                    using (var request = UnityWebRequest.Get(sourcePath))
                    {
                        request.downloadHandler = new DownloadHandlerFile(localSourcePartPath);
                        await request.SendWebRequest().ToUniTask(cancellationToken: token);
                        if (request.result != UnityWebRequest.Result.Success) throw new IOException("Initial AssetBundle read failed: " + sourcePath + " / " + request.error);
                    }
                    if (new FileInfo(localSourcePartPath).Length != expectedSize || !ESResManifestIntegrity.VerifyFileSha256(localSourcePartPath, expectedHash))
                    {
                        if (File.Exists(localSourcePartPath)) File.Delete(localSourcePartPath);
                        throw new InvalidDataException("Initial AssetBundle is invalid: " + sourcePath);
                    }
                    if (File.Exists(localPath)) File.Delete(localPath);
                    File.Move(localSourcePartPath, localPath);
                }
                verified[relativePath] = new ESRuntimeVerifiedFile { relativePath = relativePath, size = expectedSize, sha256 = expectedHash };
                return;
            }
            string partPath = localPath + ".part";
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                long length = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 30;
                    if (length > 0) request.SetRequestHeader("Range", "bytes=" + length + "-");
                    request.downloadHandler = new DownloadHandlerFile(partPath, length > 0);
                    await request.SendWebRequest().ToUniTask(cancellationToken: token);
                    if (request.result != UnityWebRequest.Result.Success || (length > 0 && request.responseCode == 200))
                    {
                        if (length > 0 && request.responseCode == 200 && File.Exists(partPath)) File.Delete(partPath);
                        if (attempt == MaxAttempts) throw new IOException("资源文件下载失败：" + url + "，" + request.error);
                        await UniTask.Delay(TimeSpan.FromSeconds(attempt), cancellationToken: token);
                        continue;
                    }
                }
                if (new FileInfo(partPath).Length == expectedSize && ESResManifestIntegrity.VerifyFileSha256(partPath, expectedHash))
                {
                    if (File.Exists(localPath)) File.Delete(localPath);
                    File.Move(partPath, localPath);
                    verified[relativePath] = new ESRuntimeVerifiedFile { relativePath = relativePath, size = expectedSize, sha256 = expectedHash };
                    return;
                }
                if (File.Exists(partPath)) File.Delete(partPath);
            }
            throw new IOException("资源文件完整性校验失败：" + url);
        }

        private bool IsVerified(string relativePath, string localPath, long size, string hash)
        {
            if (!File.Exists(localPath) || new FileInfo(localPath).Length != size) return false;
            if (verified.TryGetValue(relativePath, out var record) && record.size == size && string.Equals(record.sha256, hash, StringComparison.OrdinalIgnoreCase)) return true;
            if (!ESResManifestIntegrity.VerifyFileSha256(localPath, hash)) return false;
            verified[relativePath] = new ESRuntimeVerifiedFile { relativePath = relativePath, size = size, sha256 = hash };
            return true;
        }

        private async UniTask<string> RequestTextAsync(string url, CancellationToken token)
        {
            if (useLocalReleaseSource)
            {
                token.ThrowIfCancellationRequested();
                string sourcePath = ResolveLocalReleasePath(url);
                if (IsFilePath(sourcePath))
                {
                    if (!File.Exists(sourcePath)) throw new FileNotFoundException("Initial release manifest is missing.", sourcePath);
                    return File.ReadAllText(sourcePath);
                }
                using (var request = UnityWebRequest.Get(sourcePath))
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: token);
                    if (request.result != UnityWebRequest.Result.Success) throw new IOException("Initial release manifest read failed: " + sourcePath + " / " + request.error);
                    return request.downloadHandler.text;
                }
            }
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 30;
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
                if (request.result == UnityWebRequest.Result.Success) return request.downloadHandler.text;
                if (attempt == MaxAttempts) throw new IOException("清单下载失败：" + url + "，" + request.error);
                await UniTask.Delay(TimeSpan.FromSeconds(attempt), cancellationToken: token);
            }
            throw new InvalidOperationException();
        }

        private void Report(ESRuntimeReleaseDownloadStage stage, string subject, int completedCount = 0, int totalCount = 0)
        {
            ProgressChanged?.Invoke(new ESRuntimeReleaseDownloadProgress(stage, subject, completedCount, totalCount));
        }

        private void PrepareVerifiedIndex(string releaseVersion)
        {
            verified.Clear();
            verifiedReleaseVersion = releaseVersion;
            string path = Path.Combine(cacheRoot, "ESVerifiedFileIndex.json");
            if (!File.Exists(path)) return;
            try
            {
                ESRuntimeVerifiedFileIndex index = JsonConvert.DeserializeObject<ESRuntimeVerifiedFileIndex>(File.ReadAllText(path));
                if (index == null || !string.Equals(index.releaseVersion, releaseVersion, StringComparison.Ordinal)) return;
                foreach (var file in index.files ?? new List<ESRuntimeVerifiedFile>()) verified[file.relativePath] = file;
            }
            catch { verified.Clear(); }
        }
        private void SaveVerifiedIndex() => WriteTextAtomically(Path.Combine(cacheRoot, "ESVerifiedFileIndex.json"), JsonConvert.SerializeObject(new ESRuntimeVerifiedFileIndex { releaseVersion = verifiedReleaseVersion, files = verified.Values.OrderBy(item => item.relativePath, StringComparer.Ordinal).ToList() }, Formatting.Indented));
        private static void WriteTextAtomically(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + ".tmp"; File.WriteAllText(temp, text); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
        private string ResolveLocalReleasePath(string url)
        {
            if (Path.IsPathRooted(url)) return url;
            string remoteRoot = (settings.Path_Net ?? string.Empty).TrimEnd('/', '\\');
            string relative = url ?? string.Empty;
            if (!string.IsNullOrEmpty(remoteRoot) && relative.StartsWith(remoteRoot, StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring(remoteRoot.Length).TrimStart('/', '\\');
            return CombineLocalReleasePath(relative);
        }
        private string CombineLocalReleasePath(string relativePath)
        {
            if (!IsFilePath(localReleaseRoot)) return localReleaseRoot.TrimEnd('/') + "/" + (relativePath ?? string.Empty).TrimStart('/', '\\').Replace('\\', '/');
            return Path.Combine(localReleaseRoot, (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }
        private static bool IsFilePath(string path) => !path.Contains("://") && !path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        private static void ValidateFormat(int? formatVersion, string manifestName)
        {
            if (!formatVersion.HasValue || formatVersion.Value != ReleaseProtocolFormatVersion)
                throw new InvalidDataException(manifestName + " protocol version is unsupported: " + (formatVersion?.ToString() ?? "missing"));
        }
        private static string CombineUrl(string root, string relative) => (root ?? string.Empty).TrimEnd('/', '\\') + "/" + (relative ?? string.Empty).TrimStart('/', '\\');
        private static string GetUrlDirectory(string url) { int index = (url ?? string.Empty).LastIndexOf('/'); return index < 0 ? string.Empty : url.Substring(0, index + 1); }

        private sealed class CollectedCodePackage
        {
            public string OwnerConsumerId;
            public ESRuntimeConsumerCodePackageReference Reference;
        }
    }

    /// <summary>
    /// 发布资源层只负责取得并校验代码包，不依赖 HybridCLR 等具体运行时。
    /// 具体代码运行时必须在启动下载前注册加载器。
    /// </summary>
    internal static class ESRuntimeCodePackageBootstrap
    {
        private static Func<IEnumerable<ESRuntimeDownloadedCodePackage>, CancellationToken, UniTask> loader;

        internal static void Register(Func<IEnumerable<ESRuntimeDownloadedCodePackage>, CancellationToken, UniTask> codePackageLoader)
        {
            loader = codePackageLoader ?? throw new ArgumentNullException(nameof(codePackageLoader));
        }

        internal static UniTask LoadAsync(IEnumerable<ESRuntimeDownloadedCodePackage> packages, CancellationToken cancellationToken)
        {
            if (loader == null)
                throw new InvalidOperationException("代码包加载器尚未注册。请通过对应的代码运行时启动器初始化发布链路。");
            return loader(packages, cancellationToken);
        }
    }
}
