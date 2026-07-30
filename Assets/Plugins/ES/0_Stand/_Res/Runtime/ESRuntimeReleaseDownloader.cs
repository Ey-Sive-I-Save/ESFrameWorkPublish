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
    [Serializable] public sealed class ESRuntimeConsumerLibraryReference
    {
        public string libraryName, libraryFolder, libraryIdentityUrl, libraryIdentitySha256, embeddedIdentityRelativePath;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
        public bool requiredAtBoot;
    }
    [Serializable] public sealed class ESRuntimeConsumerCodePackageReference { public string packageKey, kind, fileName, url, sha256, notes; public long size; public bool requiredAtBoot; public int loadOrder; }
    [Serializable] public sealed class ESRuntimeConsumerGameCoreReference { public string guid; public long localFileId; public List<ESRuntimeConsumerGameCoreDependencyReference> dependencies = new List<ESRuntimeConsumerGameCoreDependencyReference>(); public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeConsumerGameCoreDependencyReference { public string guid; public long localFileId; public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeConsumerResidentAssetReference { public string guid; public long localFileId; public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeConsumerManifest
    {
        public int formatVersion;
        public string consumerId, name, description, maintainer, releaseNotes, version, platform, channel, publishedUtc;
        public bool isTotalConsumer;
        public List<string> tags = new List<string>();
        public List<ESRuntimeConsumerReference> requiredConsumers = new List<ESRuntimeConsumerReference>();
        public List<ESRuntimeConsumerLibraryReference> libraries = new List<ESRuntimeConsumerLibraryReference>();
        public List<ESRuntimeConsumerGameCoreReference> gameCoreAssets = new List<ESRuntimeConsumerGameCoreReference>();
        public List<ESRuntimeConsumerResidentAssetReference> residentAssets = new List<ESRuntimeConsumerResidentAssetReference>();
        public List<ESRuntimeConsumerCodePackageReference> codePackages = new List<ESRuntimeConsumerCodePackageReference>();
    }
    [Serializable] public sealed class ESRuntimeLibraryIdentity
    {
        public int formatVersion;
        public string libraryName, libraryFolder, libraryBundleCode, platform, version, channel, catalogUrl, assetBundleManifestUrl, catalogSha256, assetBundleManifestSha256;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
    }
    [Serializable] public sealed class ESRuntimeCatalogIdentity { public string guid; public long localFileId; public bool IsValid => !string.IsNullOrEmpty(guid) && localFileId >= 0; }
    [Serializable] public sealed class ESRuntimeCatalogEntry
    {
        public ESRuntimeCatalogIdentity identity = new ESRuntimeCatalogIdentity();
        public string assetTypeName, kind, stringKey, libraryName, libraryFolder, libraryBundleCode, pageName, subAssetName;
        public int enumKey;
        public bool isBusinessAsset;
    }
    [Serializable] public sealed class ESRuntimeCatalog { public int formatVersion; public string libraryName, libraryFolder, libraryBundleCode, libraryAssetGuid; public List<ESRuntimeCatalogEntry> assets = new List<ESRuntimeCatalogEntry>(); }
    [Serializable] public sealed class ESRuntimeBundleRecord { public string assetBundleKey, fileName, unityHash, sha256, localRelativePath; public uint crc; public long size; public List<string> dependencies = new List<string>(); }
    [Serializable] public sealed class ESRuntimeReleaseMainAssetRecord { public string guid, assetBundleKey, internalName, typeName; }
    [Serializable] public sealed class ESRuntimeReleaseSubAssetRecord { public string guid, assetBundleKey, internalName, subAssetName, typeName; public long localFileId; }
    [Serializable] public sealed class ESRuntimeBundleManifest { public int formatVersion; public string platform, libraryName; public List<ESRuntimeBundleRecord> assetBundles = new List<ESRuntimeBundleRecord>(); public List<ESRuntimeReleaseMainAssetRecord> mainAssetsByGuid = new List<ESRuntimeReleaseMainAssetRecord>(); public List<ESRuntimeReleaseSubAssetRecord> subAssetsById = new List<ESRuntimeReleaseSubAssetRecord>(); }
    [Serializable] public sealed class ESRuntimeReleaseBundleRecord
    {
        public string libraryFolder, assetBundleKey, fileUrl, sha256, localRelativePath, embeddedRelativePath;
        public ESAssetDeliveryMode deliveryMode = ESAssetDeliveryMode.Updateable;
        public uint crc;
        public long size;
        public List<string> dependencies = new List<string>();
    }
    [Serializable] public sealed class ESRuntimeReleaseBundleIndex { public int formatVersion; public string platform, releaseVersion; public List<ESRuntimeReleaseBundleRecord> assetBundles = new List<ESRuntimeReleaseBundleRecord>(); }
    [Serializable] internal sealed class ESRuntimeVerifiedFile { public string relativePath, sha256; public long size; }
    [Serializable] internal sealed class ESRuntimeVerifiedFileIndex { public string releaseVersion; public List<ESRuntimeVerifiedFile> files = new List<ESRuntimeVerifiedFile>(); }

    public enum ESRuntimeReleaseDownloadStage { ReadingRelease, ReadingConsumer, ReadingLibraryIdentity, ReadingCatalog, ReadingAssetBundleManifest, PreparingTransfer, DownloadingFile, VerifyingAssetBundle, InitializingRuntime, Completed }
    public enum ESRuntimeReleaseTransferState { Discovering, Downloading, Verifying, Initializing, Completed }
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

    /// <summary>一次发布事务的真实传输快照。字节进度只统计需要落地到缓存的物理文件。</summary>
    public readonly struct ESRuntimeReleaseDownloadSnapshot
    {
        public readonly ESRuntimeReleaseTransferState State;
        public readonly string Subject;
        public readonly long TotalBytes;
        public readonly long CompletedBytes;
        public readonly long CurrentFileBytes;
        public readonly long CurrentFileSize;
        public readonly int CompletedFileCount;
        public readonly int TotalFileCount;
        public readonly int RetryAttempt;
        public readonly float SpeedBytesPerSecond;
        public readonly int EstimatedRemainingSeconds;

        public ESRuntimeReleaseDownloadSnapshot(ESRuntimeReleaseTransferState state, string subject, long totalBytes, long completedBytes,
            long currentFileBytes, long currentFileSize, int completedFileCount, int totalFileCount, int retryAttempt,
            float speedBytesPerSecond, int estimatedRemainingSeconds)
        {
            State = state;
            Subject = subject ?? string.Empty;
            TotalBytes = Math.Max(0, totalBytes);
            CompletedBytes = Math.Max(0, completedBytes);
            CurrentFileBytes = Math.Max(0, currentFileBytes);
            CurrentFileSize = Math.Max(0, currentFileSize);
            CompletedFileCount = Math.Max(0, completedFileCount);
            TotalFileCount = Math.Max(0, totalFileCount);
            RetryAttempt = Math.Max(0, retryAttempt);
            SpeedBytesPerSecond = Math.Max(0f, speedBytesPerSecond);
            EstimatedRemainingSeconds = Math.Max(0, estimatedRemainingSeconds);
        }

        public float Progress01 => TotalBytes <= 0 ? (State == ESRuntimeReleaseTransferState.Completed ? 1f : 0f) : Mathf.Clamp01((float)CompletedBytes / TotalBytes);
    }

    public sealed class ESRuntimeReleaseDownloadResult
    {
        public ESGlobalAssetRuntimeMap RuntimeMap { get; internal set; }
        public string ReleaseVersion { get; internal set; }
        public IReadOnlyList<string> DownloadedLibraries { get; internal set; }
        public IReadOnlyList<ESRuntimeCatalog> Catalogs { get; internal set; }
        public IReadOnlyList<ESRuntimeDownloadedCodePackage> DownloadedCodePackages { get; internal set; }
        public IReadOnlyList<ESRuntimeConsumerGameCoreReference> GameCoreAssets { get; internal set; }
        public IReadOnlyList<ESRuntimeConsumerResidentAssetReference> ResidentAssets { get; internal set; }

        /// <summary>Combines a boot result and an on-demand Consumer/Library result from the
        /// same release. It is intentionally the only supported way to activate a partial
        /// download; callers never replace the active RuntimeMap with a fragment.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static ESRuntimeReleaseDownloadResult Merge(ESRuntimeReleaseDownloadResult current, ESRuntimeReleaseDownloadResult addition)
        {
            if (addition == null) throw new ArgumentNullException(nameof(addition));
            if (current == null) return addition;
            if (!string.Equals(current.ReleaseVersion, addition.ReleaseVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("不能合并不同发布版本的运行时内容：" + current.ReleaseVersion + " / " + addition.ReleaseVersion);

            return new ESRuntimeReleaseDownloadResult
            {
                RuntimeMap = ESGlobalAssetRuntimeMap.Merge(current.RuntimeMap, addition.RuntimeMap),
                ReleaseVersion = current.ReleaseVersion,
                DownloadedLibraries = MergeStrings(current.DownloadedLibraries, addition.DownloadedLibraries),
                Catalogs = MergeCatalogs(current.Catalogs, addition.Catalogs),
                DownloadedCodePackages = MergeCodePackages(current.DownloadedCodePackages, addition.DownloadedCodePackages),
                GameCoreAssets = MergeIdentities(current.GameCoreAssets, addition.GameCoreAssets),
                ResidentAssets = MergeIdentities(current.ResidentAssets, addition.ResidentAssets)
            };
        }

        private static IReadOnlyList<string> MergeStrings(IReadOnlyList<string> left, IReadOnlyList<string> right)
            => (left ?? Array.Empty<string>()).Concat(right ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();

        private static IReadOnlyList<ESRuntimeCatalog> MergeCatalogs(IReadOnlyList<ESRuntimeCatalog> left, IReadOnlyList<ESRuntimeCatalog> right)
        {
            var result = new Dictionary<string, ESRuntimeCatalog>(StringComparer.Ordinal);
            foreach (ESRuntimeCatalog catalog in (left ?? Array.Empty<ESRuntimeCatalog>()).Concat(right ?? Array.Empty<ESRuntimeCatalog>()))
            {
                if (catalog == null || string.IsNullOrWhiteSpace(catalog.libraryFolder)) continue;
                if (result.TryGetValue(catalog.libraryFolder, out ESRuntimeCatalog existing) && !ReferenceEquals(existing, catalog))
                    continue; // Same release/library was reached by two Consumer dependency paths.
                result[catalog.libraryFolder] = catalog;
            }
            return result.Values.OrderBy(item => item.libraryFolder, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<ESRuntimeDownloadedCodePackage> MergeCodePackages(IReadOnlyList<ESRuntimeDownloadedCodePackage> left, IReadOnlyList<ESRuntimeDownloadedCodePackage> right)
        {
            var result = new Dictionary<string, ESRuntimeDownloadedCodePackage>(StringComparer.Ordinal);
            foreach (ESRuntimeDownloadedCodePackage package in (left ?? Array.Empty<ESRuntimeDownloadedCodePackage>()).Concat(right ?? Array.Empty<ESRuntimeDownloadedCodePackage>()))
            {
                if (package == null || string.IsNullOrWhiteSpace(package.PackageKey)) continue;
                if (result.TryGetValue(package.PackageKey, out ESRuntimeDownloadedCodePackage existing)
                    && !string.Equals(existing.Sha256, package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("同一发布版本存在冲突的代码包：" + package.PackageKey);
                result[package.PackageKey] = package;
            }
            return result.Values.OrderBy(item => item.LoadOrder).ThenBy(item => item.PackageKey, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<T> MergeIdentities<T>(IReadOnlyList<T> left, IReadOnlyList<T> right) where T : class
        {
            var result = new Dictionary<ESAssetIdentity, T>();
            foreach (T item in (left ?? Array.Empty<T>()).Concat(right ?? Array.Empty<T>()))
            {
                switch (item)
                {
                    case ESRuntimeConsumerGameCoreReference gameCore when gameCore.IsValid:
                        result[new ESAssetIdentity(gameCore.guid, gameCore.localFileId)] = item;
                        break;
                    case ESRuntimeConsumerResidentAssetReference resident when resident.IsValid:
                        result[new ESAssetIdentity(resident.guid, resident.localFileId)] = item;
                        break;
                }
            }
            return result.OrderBy(item => item.Key.Guid, StringComparer.Ordinal).ThenBy(item => item.Key.LocalFileId).Select(item => item.Value).ToArray();
        }
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
    public sealed partial class ESRuntimeReleaseDownloader
    {
        private sealed class TransferPlanFile
        {
            public string RelativePath;
            public string LocalPath;
            public string Hash;
            public long Size;
            public long InitialBytes;
            public bool Completed;
        }
        private sealed class ReleaseContext
        {
            public ESRuntimeReleaseManifest Root;
            public Dictionary<string, ESRuntimeReleaseBundleRecord> BundlesByKey;
        }
        private const int ReleaseProtocolFormatVersion = 5;
        private const int MaxAttempts = 3;
        private readonly ESGlobalResSetting settings;
        private readonly string platform;
        private readonly string cacheRoot;
        private readonly ESAssetRunMode runMode;
        private readonly bool useLocalReleaseSource;
        private readonly string localReleaseRoot;
        private readonly SemaphoreSlim releaseOperationGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, ESRuntimeVerifiedFile> verified = new Dictionary<string, ESRuntimeVerifiedFile>(StringComparer.Ordinal);
        // A release can request the same Consumer/Library through multiple public entry points.
        // Materialization for one cache file must remain single-writer: both .part append and
        // the JSON .tmp replacement protocol are otherwise unsafe under concurrent requests.
        private readonly object cacheFileGateSync = new object();
        private readonly Dictionary<string, CacheFileGate> cacheFileGates = new Dictionary<string, CacheFileGate>(StringComparer.Ordinal);
        private readonly Dictionary<string, TransferPlanFile> transferPlanFiles = new Dictionary<string, TransferPlanFile>(StringComparer.Ordinal);
        private long transferTotalBytes;
        private long transferCompletedBytes;
        private long transferCurrentFileBytes;
        private long transferCurrentFileSize;
        private long transferCurrentInitialBytes;
        private int transferCompletedFileCount;
        private int transferRetryAttempt;
        private string transferCurrentSubject = string.Empty;
        private ESRuntimeReleaseTransferState transferState = ESRuntimeReleaseTransferState.Discovering;
        private float transferSampleTime;
        private long transferSampleBytes;
        private float transferSpeedBytesPerSecond;
        private float transferLastSnapshotTime;
        private string verifiedReleaseVersion;
        // Root is intentionally stable so it can be revalidated. Everything it points at is
        // release-versioned locally as well; an interrupted new release therefore cannot
        // overwrite the last known-good release's manifests or bundles.
        private string activeReleaseCacheRoot;
        public event Action<ESRuntimeReleaseDownloadProgress> ProgressChanged;
        public event Action<ESRuntimeReleaseDownloadSnapshot> DownloadSnapshotChanged;

        private sealed class CacheFileGate
        {
            public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
            public int UserCount;
        }

        private sealed class TransferRequestProgress : IProgress<float>
        {
            private readonly ESRuntimeReleaseDownloader owner;
            private readonly UnityWebRequest request;
            private readonly string relativePath;
            private readonly long offset;

            public TransferRequestProgress(ESRuntimeReleaseDownloader owner, UnityWebRequest request, string relativePath, long offset)
            {
                this.owner = owner;
                this.request = request;
                this.relativePath = relativePath;
                this.offset = offset;
            }

            public void Report(float value)
            {
                owner.ReportTransferFileBytes(relativePath, offset + (long)request.downloadedBytes);
            }
        }

        public ESRuntimeReleaseDownloader(ESGlobalResSetting globalSettings, ESAssetRunMode lockedRunMode)
        {
            settings = globalSettings ? globalSettings : throw new ArgumentNullException(nameof(globalSettings));
            if (lockedRunMode != ESAssetRunMode.LocalBuild && lockedRunMode != ESAssetRunMode.HotUpdate)
                throw new ArgumentOutOfRangeException(nameof(lockedRunMode), lockedRunMode, "Release downloader only supports LocalBuild and HotUpdate.");

            runMode = lockedRunMode;
            platform = ESAssetBundleUtility.GetRuntimeResourcePlatformName(settings.applyPlatform);
            cacheRoot = Path.Combine(Application.persistentDataPath, settings.Path_Sub_DownloadRelative_, "ReleaseV2", platform);
            useLocalReleaseSource = runMode == ESAssetRunMode.LocalBuild;
            localReleaseRoot = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + ESGlobalResSetting.ResParentFolderName;
        }

        /// <summary>Marks the already initialized release as bootable without network access.
        /// Call only after Provider, resident assets and GameCore injection all succeeded.</summary>
        public static bool TryCommitLastKnownGood(ESGlobalResSetting settings, string releaseVersion, out string error)
        {
            error = string.Empty;
            if (settings == null || string.IsNullOrWhiteSpace(releaseVersion))
            {
                error = "缺少发布设置或版本号。";
                return false;
            }
            try
            {
                string platform = ESAssetBundleUtility.GetRuntimeResourcePlatformName(settings.applyPlatform);
                string root = Path.Combine(Application.persistentDataPath, settings.Path_Sub_DownloadRelative_, "ReleaseV2", platform);
                string source = Path.Combine(root, "ESAssetReleaseManifest.json");
                var manifest = JsonConvert.DeserializeObject<ESRuntimeReleaseManifest>(File.ReadAllText(source));
                if (manifest == null || manifest.formatVersion != ReleaseProtocolFormatVersion
                    || !string.Equals(manifest.releaseVersion, releaseVersion, StringComparison.Ordinal)
                    || !string.Equals(manifest.platform, platform, StringComparison.Ordinal))
                    throw new InvalidDataException("当前 Root 清单与待提交版本不一致。");
                WriteTextAtomically(Path.Combine(root, "LastKnownGood", "ESAssetReleaseManifest.json"), File.ReadAllText(source));
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public UniTask<ESRuntimeReleaseDownloadResult> DownloadBootAsync(CancellationToken cancellationToken = default)
        {
            return ExecuteReleaseOperationAsync(() => DownloadBootCoreAsync(false, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// 从 Root -> TotalConsumer 的受签名链中定位并下载一个 Consumer。
        /// 这是业务侧的首选入口；不会接受调用方拼出的 URL 或 Hash。
        /// </summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> DownloadConsumerAsync(string consumerId, CancellationToken cancellationToken = default)
        {
            return await ExecuteReleaseOperationAsync(() => DownloadConsumerCoreAsync(consumerId, cancellationToken), cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadConsumerCoreAsync(string consumerId, CancellationToken cancellationToken)
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
            return await ExecuteReleaseOperationAsync(() => DownloadConsumerCoreAsync(consumerReference, cancellationToken), cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadConsumerCoreAsync(ESRuntimeConsumerReference consumerReference, CancellationToken cancellationToken)
        {
            if (consumerReference == null || string.IsNullOrWhiteSpace(consumerReference.consumerUrl) || string.IsNullOrWhiteSpace(consumerReference.consumerSha256))
                throw new ArgumentException("Consumer 引用缺少 URL 或 SHA-256。", nameof(consumerReference));
            ReleaseContext context = await LoadReleaseContextAsync(cancellationToken);
            Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, consumerReference.consumerId);
            var consumer = await DownloadJsonAsync<ESRuntimeConsumerManifest>(consumerReference.consumerUrl,
                ReleaseCachePath("Consumers", SafePathSegment(consumerReference.consumerId, "ConsumerId") + ".json"), consumerReference.consumerSha256, cancellationToken);
            if (consumer == null || !string.Equals(consumer.consumerId, consumerReference.consumerId, StringComparison.Ordinal))
                throw new InvalidDataException("Consumer manifest identity does not match its signed reference: " + consumerReference.consumerId);
            return await DownloadConsumerContentAsync(consumer, context, false, cancellationToken);
        }

        /// <summary>从指定 Consumer（包括其必需 Consumer）中按 LibraryFolder 精确下载并验证一个 Library。</summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> DownloadLibraryAsync(string consumerId, string libraryFolder, CancellationToken cancellationToken = default)
        {
            return await ExecuteReleaseOperationAsync(() => DownloadLibraryCoreAsync(consumerId, libraryFolder, cancellationToken), cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadLibraryCoreAsync(string consumerId, string libraryFolder, CancellationToken cancellationToken)
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
            return await ExecuteReleaseOperationAsync(() => DownloadLibraryCoreAsync(libraryReference, cancellationToken), cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadLibraryCoreAsync(ESRuntimeConsumerLibraryReference libraryReference, CancellationToken cancellationToken)
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
            ValidateCatalogsAgainstGlobalAssetRecords(catalogs, mainAssets, subAssets);
            BeginTransferPlan(Array.Empty<CollectedCodePackage>(), roots, context.BundlesByKey);
            await DownloadAssetBundleClosureAsync(roots, context.BundlesByKey, records, cancellationToken);
            if (!useLocalReleaseSource) SaveVerifiedIndex();
            CompleteTransferPlan();
            return CreateResult(context.Root.releaseVersion, new[] { libraryReference.libraryFolder }, records, mainAssets, subAssets, catalogs,
                Array.Empty<ESRuntimeDownloadedCodePackage>(), Array.Empty<ESRuntimeConsumerGameCoreReference>(), Array.Empty<ESRuntimeConsumerResidentAssetReference>());
        }

        internal UniTask<ESRuntimeReleaseDownloadResult> DownloadBootAndInitializeCodeAsync(CancellationToken cancellationToken = default)
        {
            return ExecuteReleaseOperationAsync(() => DownloadBootCoreAsync(true, cancellationToken), cancellationToken);
        }

        private async UniTask<ESRuntimeReleaseDownloadResult> DownloadBootCoreAsync(bool initializeCodePackages, CancellationToken cancellationToken)
        {
            string rootUrl = useLocalReleaseSource
                ? CombineLocalReleasePath(platform + "/ESAssetReleaseManifest.json")
                : CombineUrl(settings.Path_Net, platform + "/ESAssetReleaseManifest.json");
            var root = await DownloadRootOrLastKnownGoodAsync(rootUrl, cancellationToken);
            ValidateFormat(root?.formatVersion, "RootReleaseManifest");
            if (root == null || string.IsNullOrWhiteSpace(root.releaseVersion)) throw new InvalidDataException("RootReleaseManifest 缺少发布版本。");
            if (!string.Equals(root.platform, platform, StringComparison.Ordinal)) throw new InvalidDataException("RootReleaseManifest 平台不匹配：" + root.platform + " / " + platform);
            PrepareVerifiedIndex(root.releaseVersion);
            if (root == null || string.IsNullOrEmpty(root.bundleIndexUrl) || string.IsNullOrEmpty(root.bundleIndexSha256)) throw new InvalidDataException("RootReleaseManifest 缺少全局 Bundle 索引定位或 Hash。");
            var bundleIndex = await DownloadJsonAsync<ESRuntimeReleaseBundleIndex>(root.bundleIndexUrl, ReleaseCachePath("ESAssetReleaseBundleIndex.json"), root.bundleIndexSha256, cancellationToken);
            ValidateFormat(bundleIndex?.formatVersion, "GlobalAssetBundleIndex");
            var bundlesByKey = ValidateGlobalBundleIndex(root, bundleIndex);
            if (root == null || string.IsNullOrEmpty(root.totalConsumerUrl) || string.IsNullOrEmpty(root.totalConsumerSha256)) throw new InvalidDataException("RootReleaseManifest 缺少 TotalConsumer 定位或 Hash。");
            var total = await DownloadTotalConsumerManifestAsync(root, cancellationToken);
            var libraries = new Dictionary<string, ESRuntimeConsumerLibraryReference>(StringComparer.Ordinal);
            var codePackages = new Dictionary<string, CollectedCodePackage>(StringComparer.Ordinal);
            var gameCoreAssets = new Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference>();
            var residentAssets = new Dictionary<ESAssetIdentity, ESRuntimeConsumerResidentAssetReference>();
            await CollectConsumerContentAsync(total, libraries, codePackages, gameCoreAssets, residentAssets, new HashSet<string>(StringComparer.Ordinal), true, cancellationToken);
            var assetBundleRecords = new List<ESRuntimeAssetBundleRecord>();
            var mainAssets = new List<ESRuntimeAssetRecord>();
            var subAssets = new List<ESRuntimeSubAssetRecord>();
            var catalogs = new List<ESRuntimeCatalog>();
            var requiredAssetBundleKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var library in libraries.Values.OrderBy(item => item.libraryFolder, StringComparer.Ordinal))
                await DownloadLibraryAsync(library, root.releaseVersion, bundlesByKey, requiredAssetBundleKeys, mainAssets, subAssets, catalogs, cancellationToken);
            ValidateCatalogsAgainstGlobalAssetRecords(catalogs, mainAssets, subAssets);
            BeginTransferPlan(codePackages.Values, requiredAssetBundleKeys, bundlesByKey);
            var downloadedCodePackages = new List<ESRuntimeDownloadedCodePackage>();
            foreach (CollectedCodePackage codePackage in codePackages.Values.OrderBy(item => item.Reference.loadOrder).ThenBy(item => item.Reference.packageKey, StringComparer.Ordinal))
                downloadedCodePackages.Add(await DownloadCodePackageAsync(codePackage, cancellationToken));
            if (initializeCodePackages)
            {
                ReportRuntimeInitialization("CodePackages");
                await ESRuntimeCodePackageBootstrap.LoadAsync(downloadedCodePackages, cancellationToken);
            }
            await DownloadAssetBundleClosureAsync(requiredAssetBundleKeys, bundlesByKey, assetBundleRecords, cancellationToken);
            if (!useLocalReleaseSource)
                SaveVerifiedIndex();
            CompleteTransferPlan();
            var runtimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            runtimeMap.SetRecords(assetBundleRecords.ToArray(), mainAssets.ToArray(), subAssets.ToArray());
            return new ESRuntimeReleaseDownloadResult
            {
                RuntimeMap = runtimeMap,
                ReleaseVersion = root.releaseVersion,
                DownloadedLibraries = libraries.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                Catalogs = catalogs,
                DownloadedCodePackages = downloadedCodePackages,
                GameCoreAssets = gameCoreAssets.Values.ToArray(),
                ResidentAssets = residentAssets.Values.ToArray()
            };
        }

        private async UniTask<ReleaseContext> LoadReleaseContextAsync(CancellationToken token)
        {
            Report(ESRuntimeReleaseDownloadStage.ReadingRelease, "RootReleaseManifest");
            string rootUrl = useLocalReleaseSource ? CombineLocalReleasePath(platform + "/ESAssetReleaseManifest.json") : CombineUrl(settings.Path_Net, platform + "/ESAssetReleaseManifest.json");
            var root = await DownloadRootOrLastKnownGoodAsync(rootUrl, token);
            ValidateFormat(root?.formatVersion, "RootReleaseManifest");
            if (root == null || string.IsNullOrWhiteSpace(root.releaseVersion) || !string.Equals(root.platform, platform, StringComparison.Ordinal))
                throw new InvalidDataException("RootReleaseManifest 无效或平台不匹配。");
            PrepareVerifiedIndex(root.releaseVersion);
            if (string.IsNullOrEmpty(root.bundleIndexUrl) || string.IsNullOrEmpty(root.bundleIndexSha256))
                throw new InvalidDataException("RootReleaseManifest 缺少全局 Bundle 索引。");
            var index = await DownloadJsonAsync<ESRuntimeReleaseBundleIndex>(root.bundleIndexUrl, ReleaseCachePath("ESAssetReleaseBundleIndex.json"), root.bundleIndexSha256, token);
            ValidateFormat(index?.formatVersion, "GlobalAssetBundleIndex");
            return new ReleaseContext { Root = root, BundlesByKey = ValidateGlobalBundleIndex(root, index) };
        }

        private async UniTask<ESRuntimeReleaseManifest> DownloadRootOrLastKnownGoodAsync(string rootUrl, CancellationToken token)
        {
            try
            {
                return await DownloadJsonAsync<ESRuntimeReleaseManifest>(rootUrl, Path.Combine(cacheRoot, "ESAssetReleaseManifest.json"), null, token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception remoteException) when (!useLocalReleaseSource)
            {
                string fallbackPath = Path.Combine(cacheRoot, "LastKnownGood", "ESAssetReleaseManifest.json");
                if (!File.Exists(fallbackPath))
                    throw new IOException("远端 Root 清单不可用，且不存在已成功启动过的离线发布版本。", remoteException);
                try
                {
                    var fallback = JsonConvert.DeserializeObject<ESRuntimeReleaseManifest>(File.ReadAllText(fallbackPath));
                    ValidateFormat(fallback?.formatVersion, "LastKnownGoodRootReleaseManifest");
                    if (fallback == null || string.IsNullOrWhiteSpace(fallback.releaseVersion)
                        || !string.Equals(fallback.platform, platform, StringComparison.Ordinal))
                        throw new InvalidDataException("离线发布 Root 清单无效或平台不匹配。");
                    Debug.LogWarning("[ESRes][Release] 远端 Root 不可用，回退到最近一次成功启动版本：" + fallback.releaseVersion);
                    return fallback;
                }
                catch (Exception fallbackException)
                {
                    throw new IOException("远端 Root 清单不可用，且离线发布版本无法验证。", new AggregateException(remoteException, fallbackException));
                }
            }
        }

        private string ReleaseCachePath(params string[] segments)
        {
            if (string.IsNullOrWhiteSpace(activeReleaseCacheRoot))
                throw new InvalidOperationException("Release 缓存目录尚未初始化；必须先验证 Root Release Manifest。");
            string path = activeReleaseCacheRoot;
            foreach (string segment in segments)
                path = Path.Combine(path, segment);
            return path;
        }

        private async UniTask<ESRuntimeConsumerManifest> DownloadTotalConsumerManifestAsync(ESRuntimeReleaseManifest root, CancellationToken token)
        {
            if (root == null || string.IsNullOrEmpty(root.totalConsumerUrl) || string.IsNullOrEmpty(root.totalConsumerSha256))
                throw new InvalidDataException("RootReleaseManifest is missing TotalConsumer location or hash.");
            Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, "TotalConsumer");
            var total = await DownloadJsonAsync<ESRuntimeConsumerManifest>(root.totalConsumerUrl, ReleaseCachePath("Consumers", "total.json"), root.totalConsumerSha256, token);
            ValidateFormat(total?.formatVersion, "TotalConsumerManifest");
            if (total == null || !total.isTotalConsumer || string.IsNullOrWhiteSpace(total.consumerId))
                throw new InvalidDataException("TotalConsumerManifest is invalid.");
            return total;
        }

        private async UniTask<ESRuntimeConsumerManifest> FindConsumerAsync(ESRuntimeConsumerManifest current, string consumerId, HashSet<string> visited, CancellationToken token)
        {
            ValidateFormat(current?.formatVersion, "ConsumerManifest");
            if (current == null || !visited.Add(current.consumerId)) return null;
            if (string.Equals(current.consumerId, consumerId, StringComparison.Ordinal)) return current;
            foreach (ESRuntimeConsumerReference reference in current.requiredConsumers ?? new List<ESRuntimeConsumerReference>())
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.consumerUrl) || string.IsNullOrWhiteSpace(reference.consumerSha256))
                    throw new InvalidDataException("Consumer dependency reference is incomplete: " + current.consumerId);
                string childId = SafePathSegment(reference.consumerId, "ConsumerId");
                Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, childId);
                var child = await DownloadJsonAsync<ESRuntimeConsumerManifest>(reference.consumerUrl, ReleaseCachePath("Consumers", childId + ".json"), reference.consumerSha256, token);
                ValidateFormat(child?.formatVersion, "ConsumerManifest:" + childId);
                if (child == null || !string.Equals(child.consumerId, childId, StringComparison.Ordinal))
                    throw new InvalidDataException("Consumer dependency manifest identity does not match its signed reference: " + childId);
                ESRuntimeConsumerManifest found = await FindConsumerAsync(child, consumerId, visited, token);
                if (found != null) return found;
            }
            return null;
        }

        private async UniTask<ESRuntimeConsumerLibraryReference> FindLibraryAsync(ESRuntimeConsumerManifest current, string libraryFolder, HashSet<string> visited, CancellationToken token)
        {
            ValidateFormat(current?.formatVersion, "ConsumerManifest");
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
                var child = await DownloadJsonAsync<ESRuntimeConsumerManifest>(reference.consumerUrl, ReleaseCachePath("Consumers", childId + ".json"), reference.consumerSha256, token);
                ValidateFormat(child?.formatVersion, "ConsumerManifest:" + childId);
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
            var residentAssets = new Dictionary<ESAssetIdentity, ESRuntimeConsumerResidentAssetReference>();
            await CollectConsumerContentAsync(consumer, libraries, codePackages, gameCoreAssets, residentAssets, new HashSet<string>(StringComparer.Ordinal), requiredAtBootOnly, token);
            var records = new List<ESRuntimeAssetBundleRecord>();
            var mainAssets = new List<ESRuntimeAssetRecord>();
            var subAssets = new List<ESRuntimeSubAssetRecord>();
            var catalogs = new List<ESRuntimeCatalog>();
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESRuntimeConsumerLibraryReference library in libraries.Values.OrderBy(item => item.libraryFolder, StringComparer.Ordinal))
                await DownloadLibraryAsync(library, context.Root.releaseVersion, context.BundlesByKey, roots, mainAssets, subAssets, catalogs, token);
            ValidateCatalogsAgainstGlobalAssetRecords(catalogs, mainAssets, subAssets);
            BeginTransferPlan(codePackages.Values, roots, context.BundlesByKey);
            await DownloadAssetBundleClosureAsync(roots, context.BundlesByKey, records, token);
            var downloadedCode = new List<ESRuntimeDownloadedCodePackage>();
            foreach (CollectedCodePackage code in codePackages.Values.OrderBy(item => item.Reference.loadOrder).ThenBy(item => item.Reference.packageKey, StringComparer.Ordinal))
                downloadedCode.Add(await DownloadCodePackageAsync(code, token));
            if (!useLocalReleaseSource) SaveVerifiedIndex();
            CompleteTransferPlan();
            return CreateResult(context.Root.releaseVersion, libraries.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray(), records, mainAssets, subAssets, catalogs, downloadedCode, gameCoreAssets.Values.ToArray(), residentAssets.Values.ToArray());
        }

        private static ESRuntimeReleaseDownloadResult CreateResult(string version, IReadOnlyList<string> libraries, List<ESRuntimeAssetBundleRecord> bundles, List<ESRuntimeAssetRecord> mainAssets, List<ESRuntimeSubAssetRecord> subAssets, List<ESRuntimeCatalog> catalogs, IReadOnlyList<ESRuntimeDownloadedCodePackage> codePackages, IReadOnlyList<ESRuntimeConsumerGameCoreReference> gameCoreAssets, IReadOnlyList<ESRuntimeConsumerResidentAssetReference> residentAssets)
        {
            var runtimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            runtimeMap.SetRecords(bundles.ToArray(), mainAssets.ToArray(), subAssets.ToArray());
            return new ESRuntimeReleaseDownloadResult { RuntimeMap = runtimeMap, ReleaseVersion = version, DownloadedLibraries = libraries, Catalogs = catalogs, DownloadedCodePackages = codePackages, GameCoreAssets = gameCoreAssets, ResidentAssets = residentAssets };
        }

        private async UniTask CollectConsumerContentAsync(ESRuntimeConsumerManifest consumer, Dictionary<string, ESRuntimeConsumerLibraryReference> libraries,
            Dictionary<string, CollectedCodePackage> codePackages, Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference> gameCoreAssets,
            Dictionary<ESAssetIdentity, ESRuntimeConsumerResidentAssetReference> residentAssets, HashSet<string> visitedConsumers, bool requiredAtBootOnly, CancellationToken token)
        {
            ValidateFormat(consumer?.formatVersion, "ConsumerManifest");
            if (consumer == null || string.IsNullOrWhiteSpace(consumer.consumerId)) throw new InvalidDataException("Consumer Manifest 缺少稳定 ID。");
            if (!visitedConsumers.Add(consumer.consumerId)) return;
            foreach (var dependency in consumer.requiredConsumers ?? new List<ESRuntimeConsumerReference>())
            {
                if (string.IsNullOrEmpty(dependency.consumerUrl) || string.IsNullOrEmpty(dependency.consumerSha256)) throw new InvalidDataException("Consumer 依赖缺少 URL 或 Hash。");
                string childId = SafePathSegment(dependency.consumerId, "ConsumerId");
                Report(ESRuntimeReleaseDownloadStage.ReadingConsumer, childId);
                var child = await DownloadJsonAsync<ESRuntimeConsumerManifest>(dependency.consumerUrl, ReleaseCachePath("Consumers", childId + ".json"), dependency.consumerSha256, token);
                ValidateFormat(child?.formatVersion, "ConsumerManifest:" + childId);
                if (child == null || !string.Equals(child.consumerId, dependency.consumerId, StringComparison.Ordinal))
                    throw new InvalidDataException("Consumer dependency manifest identity does not match its signed reference: " + dependency.consumerId);
                await CollectConsumerContentAsync(child, libraries, codePackages, gameCoreAssets, residentAssets, visitedConsumers, requiredAtBootOnly, token);
            }
            foreach (var library in consumer.libraries ?? new List<ESRuntimeConsumerLibraryReference>())
            {
                if (library == null) throw new InvalidDataException("Consumer contains a null library reference: " + consumer.consumerId);
                string libraryFolder = SafePathSegment(library.libraryFolder, "Library folder");
                if (requiredAtBootOnly && !library.requiredAtBoot) continue;
                if (libraries.TryGetValue(libraryFolder, out ESRuntimeConsumerLibraryReference existing)
                    && (!string.Equals(existing.libraryIdentityUrl, library.libraryIdentityUrl, StringComparison.Ordinal)
                        || !string.Equals(existing.libraryIdentitySha256, library.libraryIdentitySha256, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(existing.embeddedIdentityRelativePath, library.embeddedIdentityRelativePath, StringComparison.Ordinal)
                        || existing.deliveryMode != library.deliveryMode))
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
            foreach (ESRuntimeConsumerResidentAssetReference asset in consumer.residentAssets ?? new List<ESRuntimeConsumerResidentAssetReference>())
                if (asset != null && asset.IsValid)
                {
                    var id = new ESAssetIdentity(asset.guid, asset.localFileId);
                    if (!residentAssets.ContainsKey(id))
                        residentAssets.Add(id, asset);
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
            string localPath = ReleaseCachePath("Code", ownerConsumerId, fileName);
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

        private void ValidateLibraryReference(ESRuntimeConsumerLibraryReference library, string libraryFolder)
        {
            if (!IsValidDeliveryMode(library.deliveryMode) || !IsSha256(library.libraryIdentitySha256))
                throw new InvalidDataException("Library 分发方式或 Identity Hash 无效：" + libraryFolder);

            bool hasEmbedded = !string.IsNullOrWhiteSpace(library.embeddedIdentityRelativePath);
            bool hasRemote = !string.IsNullOrWhiteSpace(library.libraryIdentityUrl);
            if (hasEmbedded != (library.deliveryMode != ESAssetDeliveryMode.Remote)
                || hasRemote != (library.deliveryMode != ESAssetDeliveryMode.BuiltIn))
                throw new InvalidDataException("Library Identity 来源与分发方式不匹配：" + libraryFolder);

            if (hasEmbedded)
            {
                string expectedPath = platform + "/Embedded/Libraries/" + libraryFolder + "/ESAssetLibraryIdentity.json";
                string normalizedPath = library.embeddedIdentityRelativePath.Replace('\\', '/');
                if (!string.Equals(normalizedPath, expectedPath, StringComparison.Ordinal))
                    throw new InvalidDataException("Library Embedded Identity 路径无效：" + libraryFolder);
            }
        }

        private bool ShouldUseEmbedded(ESAssetDeliveryMode mode)
        {
            return mode == ESAssetDeliveryMode.BuiltIn
                || (mode == ESAssetDeliveryMode.Updateable && useLocalReleaseSource);
        }

        private static bool IsValidDeliveryMode(ESAssetDeliveryMode mode)
        {
            return mode == ESAssetDeliveryMode.BuiltIn
                || mode == ESAssetDeliveryMode.Updateable
                || mode == ESAssetDeliveryMode.Remote;
        }

        private async UniTask DownloadLibraryAsync(ESRuntimeConsumerLibraryReference library, string releaseVersion, Dictionary<string, ESRuntimeReleaseBundleRecord> bundlesByKey, HashSet<string> requiredAssetBundleKeys, List<ESRuntimeAssetRecord> mainAssets, List<ESRuntimeSubAssetRecord> subAssets, List<ESRuntimeCatalog> catalogs, CancellationToken token)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            string libraryFolder = SafePathSegment(library.libraryFolder, "Library folder");
            ValidateLibraryReference(library, libraryFolder);
            bool useEmbedded = ShouldUseEmbedded(library.deliveryMode);
            string identitySource = useEmbedded
                ? CombineLocalReleasePath(library.embeddedIdentityRelativePath)
                : library.libraryIdentityUrl;
            string libraryRoot = ReleaseCachePath("Libraries", libraryFolder);
            Report(ESRuntimeReleaseDownloadStage.ReadingLibraryIdentity, libraryFolder);
            var identity = await DownloadJsonAsync<ESRuntimeLibraryIdentity>(identitySource, Path.Combine(libraryRoot, "ESAssetLibraryIdentity.json"), library.libraryIdentitySha256, token, useEmbedded);
            ValidateFormat(identity?.formatVersion, "LibraryIdentity");
            string expectedIdentityVersion = library.deliveryMode == ESAssetDeliveryMode.BuiltIn ? "embedded" : releaseVersion;
            if (identity == null || !string.Equals(identity.libraryFolder, libraryFolder, StringComparison.Ordinal)
                || identity.deliveryMode != library.deliveryMode
                || !string.Equals(identity.platform, platform, StringComparison.Ordinal) || !string.Equals(identity.version, expectedIdentityVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Library Identity 与当前发布不匹配：" + library.libraryFolder);
            if (string.IsNullOrWhiteSpace(identity.catalogSha256) || string.IsNullOrWhiteSpace(identity.assetBundleManifestSha256)
                || (!useEmbedded && (string.IsNullOrWhiteSpace(identity.catalogUrl) || string.IsNullOrWhiteSpace(identity.assetBundleManifestUrl))))
                throw new InvalidDataException("Library Identity 缺少索引定位或完整性信息：" + library.libraryFolder);
            string embeddedBase = platform + "/Embedded/Libraries/" + libraryFolder + "/";
            string catalogSource = useEmbedded ? CombineLocalReleasePath(embeddedBase + "ESAssetLibraryCatalog.json") : identity.catalogUrl;
            string manifestSource = useEmbedded ? CombineLocalReleasePath(embeddedBase + "ESAssetBundleManifest.json") : identity.assetBundleManifestUrl;
            Report(ESRuntimeReleaseDownloadStage.ReadingCatalog, libraryFolder);
            var catalog = await DownloadJsonAsync<ESRuntimeCatalog>(catalogSource, Path.Combine(libraryRoot, "ESAssetLibraryCatalog.json"), identity.catalogSha256, token, useEmbedded);
            if (catalog == null || catalog.assets == null) throw new InvalidDataException("Catalog 解析失败：" + library.libraryFolder);
            if (catalog.formatVersion != 3) throw new InvalidDataException("Catalog 命名协议版本不匹配：" + library.libraryFolder);
            if (string.IsNullOrWhiteSpace(identity.libraryBundleCode)
                || !string.Equals(identity.libraryBundleCode, catalog.libraryBundleCode, StringComparison.Ordinal))
                throw new InvalidDataException("Catalog 与 LibraryIdentity 的 AB 短码不一致：" + library.libraryFolder);
            if (!string.IsNullOrEmpty(catalog.libraryFolder) && !string.Equals(catalog.libraryFolder, libraryFolder, StringComparison.Ordinal))
            {
                // 兼容旧发布物：早期 GameCore 构建曾把规范目录 gamecore_x 写成 __gamecore_x。
                // 只允许这一种已知历史差异，普通 Library 仍必须严格匹配身份。
                string legacyGameCoreFolder = "__" + libraryFolder;
                bool legacyGameCoreName = libraryFolder.StartsWith("gamecore_", StringComparison.Ordinal)
                    && string.Equals(catalog.libraryFolder, legacyGameCoreFolder, StringComparison.Ordinal);
                if (!legacyGameCoreName)
                    throw new InvalidDataException("Catalog Library 身份不匹配：" + library.libraryFolder);
                Debug.LogWarning("[ESRes][Catalog] 兼容旧 GameCore Catalog 目录名：" + catalog.libraryFolder + " -> " + libraryFolder);
            }
            catalogs.Add(catalog);
            Report(ESRuntimeReleaseDownloadStage.ReadingAssetBundleManifest, libraryFolder);
            var manifest = await DownloadJsonAsync<ESRuntimeBundleManifest>(manifestSource, Path.Combine(libraryRoot, "ESAssetBundleManifest.json"), identity.assetBundleManifestSha256, token, useEmbedded);
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
                if (!string.Equals(indexed.libraryFolder, libraryFolder, StringComparison.Ordinal) || indexed.deliveryMode != library.deliveryMode
                    || indexed.size != bundle.size || indexed.crc != bundle.crc
                    || !string.Equals(NormalizeAssetBundleRelativePath(indexed.localRelativePath), NormalizeAssetBundleRelativePath(bundle.localRelativePath), StringComparison.Ordinal)
                    || !string.Equals(indexed.sha256, bundle.sha256, StringComparison.OrdinalIgnoreCase)
                    || !(indexed.dependencies ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal).SequenceEqual((bundle.dependencies ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Global Bundle index differs from Library Manifest: " + bundle.assetBundleKey);
                requiredAssetBundleKeys.Add(bundle.assetBundleKey);
            }
            // Catalog 的业务资产可以由同一 Consumer 的另一个物理 Library（例如
            // gamecore_<consumerId>）承载。此处只校验本 Manifest 自身的文件索引；
            // 所有 Library 收集完成后，再使用全局 GUID 索引校验 Catalog。
            ValidateManifestAssetRecords(manifest, availableAssetBundleKeys);
            foreach (var asset in manifest.mainAssetsByGuid ?? new List<ESRuntimeReleaseMainAssetRecord>())
            {
                requiredAssetBundleKeys.Add(asset.assetBundleKey);
                AddOrValidateMainAssetRecord(mainAssets, new ESRuntimeAssetRecord(asset.guid, asset.assetBundleKey, asset.internalName, asset.typeName));
            }
            foreach (var asset in manifest.subAssetsById ?? new List<ESRuntimeReleaseSubAssetRecord>())
            {
                requiredAssetBundleKeys.Add(asset.assetBundleKey);
                AddOrValidateSubAssetRecord(subAssets, new ESRuntimeSubAssetRecord(asset.guid, asset.localFileId, asset.assetBundleKey, asset.internalName, asset.subAssetName, asset.typeName));
            }
        }

        private static void AddOrValidateMainAssetRecord(List<ESRuntimeAssetRecord> destination, ESRuntimeAssetRecord candidate)
        {
            ESRuntimeAssetRecord existing = destination.FirstOrDefault(item => string.Equals(item.Guid, candidate.Guid, StringComparison.Ordinal));
            if (existing == null) { destination.Add(candidate); return; }
            if (!string.Equals(existing.AssetBundleKey, candidate.AssetBundleKey, StringComparison.Ordinal)
                || !string.Equals(existing.InternalName, candidate.InternalName, StringComparison.Ordinal)
                || !string.Equals(existing.TypeName, candidate.TypeName, StringComparison.Ordinal))
                throw new InvalidDataException("[ESRes][Catalog] 同一 GUID 指向不同物理资源：GUID=" + candidate.Guid + ", A=" + existing.AssetBundleKey + "/" + existing.InternalName + ", B=" + candidate.AssetBundleKey + "/" + candidate.InternalName);
        }

        private static void AddOrValidateSubAssetRecord(List<ESRuntimeSubAssetRecord> destination, ESRuntimeSubAssetRecord candidate)
        {
            ESRuntimeSubAssetRecord existing = destination.FirstOrDefault(item => string.Equals(item.Guid, candidate.Guid, StringComparison.Ordinal) && item.LocalFileId == candidate.LocalFileId);
            if (existing == null) { destination.Add(candidate); return; }
            if (!string.Equals(existing.AssetBundleKey, candidate.AssetBundleKey, StringComparison.Ordinal)
                || !string.Equals(existing.InternalName, candidate.InternalName, StringComparison.Ordinal)
                || !string.Equals(existing.Selector, candidate.Selector, StringComparison.Ordinal)
                || !string.Equals(existing.TypeName, candidate.TypeName, StringComparison.Ordinal))
                throw new InvalidDataException("[ESRes][SubAsset] 同一子资产身份指向不同物理资源：GUID=" + candidate.Guid + ", LocalFileId=" + candidate.LocalFileId + ", A=" + existing.AssetBundleKey + "/" + existing.InternalName + ", B=" + candidate.AssetBundleKey + "/" + candidate.InternalName);
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
                string libraryFolder = SafePathSegment(bundle.libraryFolder, "Bundle library folder");
                if (!IsValidDeliveryMode(bundle.deliveryMode) || !IsSha256(bundle.sha256) || bundle.size <= 0)
                    throw new InvalidDataException("全局 Bundle 索引记录不完整：" + bundle.assetBundleKey);
                string normalizedPath = NormalizeAssetBundleRelativePath(bundle.localRelativePath);
                if (!string.Equals(normalizedPath, bundle.localRelativePath, StringComparison.Ordinal))
                    throw new InvalidDataException("全局 Bundle 索引路径未规范化：" + bundle.assetBundleKey);
                bool hasEmbedded = !string.IsNullOrWhiteSpace(bundle.embeddedRelativePath);
                bool hasRemote = !string.IsNullOrWhiteSpace(bundle.fileUrl);
                if (hasEmbedded != (bundle.deliveryMode != ESAssetDeliveryMode.Remote)
                    || hasRemote != (bundle.deliveryMode != ESAssetDeliveryMode.BuiltIn))
                    throw new InvalidDataException("全局 Bundle 来源与分发方式不匹配：" + bundle.assetBundleKey);
                if (hasEmbedded)
                {
                    string expectedPath = platform + "/Embedded/Libraries/" + libraryFolder + "/" + normalizedPath;
                    if (!string.Equals(bundle.embeddedRelativePath.Replace('\\', '/'), expectedPath, StringComparison.Ordinal))
                        throw new InvalidDataException("全局 Bundle Embedded 路径无效：" + bundle.assetBundleKey);
                }
                var dependencySet = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependency in bundle.dependencies ?? new List<string>())
                    if (string.IsNullOrWhiteSpace(dependency) || string.Equals(dependency, bundle.assetBundleKey, StringComparison.Ordinal) || !dependencySet.Add(dependency))
                        throw new InvalidDataException("全局 Bundle 索引依赖无效：" + bundle.assetBundleKey + " -> " + dependency);
            }

            foreach (ESRuntimeReleaseBundleRecord bundle in result.Values)
                foreach (string dependency in bundle.dependencies ?? new List<string>())
                {
                    if (!result.TryGetValue(dependency, out ESRuntimeReleaseBundleRecord dependencyBundle))
                        throw new InvalidDataException("全局 Bundle 索引依赖缺失：" + bundle.assetBundleKey + " -> " + dependency);
                    if (bundle.deliveryMode != ESAssetDeliveryMode.Remote && dependencyBundle.deliveryMode == ESAssetDeliveryMode.Remote)
                        throw new InvalidDataException("随包或更新资源不能依赖纯远端资源：" + bundle.assetBundleKey + " -> " + dependency);
                }
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

        private static void ValidateManifestAssetRecords(ESRuntimeBundleManifest manifest, HashSet<string> ownedBundleKeys)
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

        }

        private static void ValidateCatalogsAgainstGlobalAssetRecords(IReadOnlyList<ESRuntimeCatalog> catalogs, IReadOnlyList<ESRuntimeAssetRecord> mainAssets, IReadOnlyList<ESRuntimeSubAssetRecord> subAssets)
        {
            var mainByGuid = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESRuntimeAssetRecord asset in mainAssets ?? Array.Empty<ESRuntimeAssetRecord>())
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.Guid) || !mainByGuid.Add(asset.Guid))
                    throw new InvalidDataException("[ESRes][Catalog] 全局主资源文件索引无效或身份重复：GUID=" + (asset?.Guid ?? "<null>"));
            }
            var subById = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESRuntimeSubAssetRecord asset in subAssets ?? Array.Empty<ESRuntimeSubAssetRecord>())
            {
                string id = asset == null ? string.Empty : asset.Guid + ":" + asset.LocalFileId;
                if (asset == null || string.IsNullOrWhiteSpace(asset.Guid) || asset.LocalFileId == 0 || !subById.Add(id))
                    throw new InvalidDataException("[ESRes][SubAsset] 全局子资产文件索引无效或身份重复：" + id);
            }

            foreach (ESRuntimeCatalog catalog in catalogs ?? Array.Empty<ESRuntimeCatalog>())
            foreach (ESRuntimeCatalogEntry entry in catalog.assets ?? new List<ESRuntimeCatalogEntry>())
            {
                if (entry == null || !entry.isBusinessAsset) continue;
                if (entry.identity == null || !entry.identity.IsValid) throw new InvalidDataException("[ESRes][Catalog] Catalog 业务资源身份无效：Library=" + catalog.libraryFolder + ", Page=" + entry.pageName + ", GUID=" + (entry.identity?.guid ?? "<null>") + ", LocalFileId=" + (entry.identity?.localFileId.ToString() ?? "<null>"));
                bool indexed = entry.identity.localFileId == 0
                    ? mainByGuid.Contains(entry.identity.guid)
                    : subById.Contains(entry.identity.guid + ":" + entry.identity.localFileId);
                if (!indexed)
                {
                    string tag = entry.identity.localFileId == 0 ? "[ESRes][Catalog]" : "[ESRes][SubAsset]";
                    throw new InvalidDataException(tag + " Catalog 业务资源未进入全局文件索引：Library=" + catalog.libraryFolder + ", Page=" + entry.pageName + ", GUID=" + entry.identity.guid + ", LocalFileId=" + entry.identity.localFileId + ", Kind=" + entry.kind + ", Type=" + entry.assetTypeName);
                }
            }
        }

        private static bool IsSha256(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(Uri.IsHexDigit);
        }

        private void BeginTransferPlan(IEnumerable<CollectedCodePackage> codePackages, IEnumerable<string> roots, Dictionary<string, ESRuntimeReleaseBundleRecord> bundlesByKey)
        {
            transferPlanFiles.Clear();
            transferTotalBytes = 0;
            transferCompletedBytes = 0;
            transferCurrentFileBytes = 0;
            transferCurrentFileSize = 0;
            transferCurrentInitialBytes = 0;
            transferCompletedFileCount = 0;
            transferRetryAttempt = 0;
            transferCurrentSubject = string.Empty;
            transferState = ESRuntimeReleaseTransferState.Discovering;

            foreach (CollectedCodePackage collected in codePackages ?? Array.Empty<CollectedCodePackage>())
            {
                ESRuntimeConsumerCodePackageReference package = collected?.Reference;
                if (package == null) continue;
                string owner = SafePathSegment(collected.OwnerConsumerId, "ConsumerId");
                string fileName = SafePathSegment(package.fileName, "Code package file name");
                string relativePath = Path.Combine("Code", owner, fileName).Replace('\\', '/');
                string localPath = ReleaseCachePath("Code", owner, fileName);
                bool requiresCacheFile = !useLocalReleaseSource || !IsFilePath(ResolveLocalReleasePath(package.url));
                if (requiresCacheFile)
                    AddTransferPlanFile(relativePath, localPath, package.size, package.sha256);
            }

            var resolved = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key)
            {
                if (!resolved.Add(key)) return;
                if (!bundlesByKey.TryGetValue(key, out ESRuntimeReleaseBundleRecord bundle))
                    throw new InvalidDataException("Global Bundle dependency is missing: " + key);
                foreach (string dependency in bundle.dependencies ?? new List<string>()) Visit(dependency);
                if (ShouldUseEmbedded(bundle.deliveryMode)) return;

                string libraryFolder = SafePathSegment(bundle.libraryFolder, "Bundle library folder");
                string relativeBundlePath = NormalizeAssetBundleRelativePath(bundle.localRelativePath);
                string relativePath = Path.Combine("Libraries", libraryFolder, relativeBundlePath).Replace('\\', '/');
                string localPath = ReleaseCachePath("Libraries", libraryFolder, relativeBundlePath.Replace('/', Path.DirectorySeparatorChar));
                AddTransferPlanFile(relativePath, localPath, bundle.size, bundle.sha256);
            }
            foreach (string root in roots ?? Array.Empty<string>()) Visit(root);

            transferState = ESRuntimeReleaseTransferState.Downloading;
            transferSampleTime = Time.realtimeSinceStartup;
            transferSampleBytes = transferCompletedBytes;
            transferSpeedBytesPerSecond = 0f;
            Report(ESRuntimeReleaseDownloadStage.PreparingTransfer, "TransferPlan", transferCompletedFileCount, transferPlanFiles.Count);
            PublishTransferSnapshot(true);
        }

        private void AddTransferPlanFile(string relativePath, string localPath, long size, string hash)
        {
            if (size < 0 || string.IsNullOrWhiteSpace(hash))
                throw new InvalidDataException("Transfer plan contains an invalid file: " + relativePath);
            if (transferPlanFiles.ContainsKey(relativePath)) return;

            long initialBytes = 0;
            if (IsVerified(relativePath, localPath, size, hash))
            {
                initialBytes = size;
            }
            else
            {
                string partPath = localPath + ".part";
                if (File.Exists(partPath))
                {
                    long partLength = new FileInfo(partPath).Length;
                    if (partLength >= 0 && partLength < size) initialBytes = partLength;
                }
            }

            var file = new TransferPlanFile { RelativePath = relativePath, LocalPath = localPath, Hash = hash, Size = size, InitialBytes = initialBytes, Completed = initialBytes == size };
            transferPlanFiles.Add(relativePath, file);
            transferTotalBytes += size;
            transferCompletedBytes += initialBytes;
            if (file.Completed) transferCompletedFileCount++;
        }

        private void BeginTransferFile(string relativePath, long size, int retryAttempt)
        {
            if (!transferPlanFiles.TryGetValue(relativePath, out TransferPlanFile file)) return;
            transferState = ESRuntimeReleaseTransferState.Downloading;
            transferCurrentSubject = relativePath;
            transferCurrentFileSize = size;
            transferCurrentInitialBytes = file.InitialBytes;
            transferCurrentFileBytes = file.InitialBytes;
            transferRetryAttempt = retryAttempt;
            PublishTransferSnapshot(true);
        }

        private void ReportTransferFileBytes(string relativePath, long bytes)
        {
            if (!transferPlanFiles.TryGetValue(relativePath, out TransferPlanFile file)) return;
            transferState = ESRuntimeReleaseTransferState.Downloading;
            transferCurrentSubject = relativePath;
            transferCurrentFileSize = file.Size;
            transferCurrentInitialBytes = file.InitialBytes;
            transferCurrentFileBytes = Math.Min(file.Size, Math.Max(0, bytes));
            PublishTransferSnapshot();
        }

        private void CompleteTransferFile(string relativePath)
        {
            if (!transferPlanFiles.TryGetValue(relativePath, out TransferPlanFile file) || file.Completed) return;
            file.Completed = true;
            transferCompletedBytes += file.Size - file.InitialBytes;
            transferCompletedFileCount++;
            transferCurrentSubject = relativePath;
            transferCurrentFileSize = file.Size;
            transferCurrentFileBytes = file.Size;
            transferCurrentInitialBytes = file.InitialBytes;
            transferRetryAttempt = 0;
            PublishTransferSnapshot(true);
        }

        private void ReportTransferVerification(string relativePath, long size)
        {
            if (!transferPlanFiles.TryGetValue(relativePath, out TransferPlanFile file)) return;
            transferState = ESRuntimeReleaseTransferState.Verifying;
            transferCurrentSubject = relativePath;
            transferCurrentFileSize = size;
            transferCurrentInitialBytes = file.InitialBytes;
            transferCurrentFileBytes = Math.Min(size, Math.Max(file.InitialBytes, new FileInfo(file.LocalPath + ".part").Length));
            PublishTransferSnapshot(true);
        }

        private void PublishTransferSnapshot(bool force = false)
        {
            long visibleBytes = transferCompletedBytes + Math.Max(0, transferCurrentFileBytes - transferCurrentInitialBytes);
            visibleBytes = Math.Min(transferTotalBytes, visibleBytes);
            float now = Time.realtimeSinceStartup;
            if (!force && now - transferLastSnapshotTime < .1f)
                return;
            float elapsed = now - transferSampleTime;
            if (elapsed >= .25f)
            {
                long delta = visibleBytes - transferSampleBytes;
                transferSpeedBytesPerSecond = delta > 0 ? delta / elapsed : 0f;
                transferSampleTime = now;
                transferSampleBytes = visibleBytes;
            }
            int eta = transferSpeedBytesPerSecond > 0f
                ? Mathf.CeilToInt(Math.Max(0, transferTotalBytes - visibleBytes) / transferSpeedBytesPerSecond)
                : 0;
            var snapshot = new ESRuntimeReleaseDownloadSnapshot(transferState, transferCurrentSubject, transferTotalBytes,
                visibleBytes, transferCurrentFileBytes, transferCurrentFileSize, transferCompletedFileCount, transferPlanFiles.Count,
                transferRetryAttempt, transferSpeedBytesPerSecond, eta);
            RecordDiagnosticSnapshot(snapshot);
            transferLastSnapshotTime = now;
            DownloadSnapshotChanged?.Invoke(snapshot);
        }

        private void ReportRuntimeInitialization(string subject)
        {
            transferState = ESRuntimeReleaseTransferState.Initializing;
            transferCurrentSubject = subject ?? string.Empty;
            transferRetryAttempt = 0;
            Report(ESRuntimeReleaseDownloadStage.InitializingRuntime, transferCurrentSubject, transferCompletedFileCount, transferPlanFiles.Count);
            PublishTransferSnapshot(true);
        }

        private void CompleteTransferPlan()
        {
            transferState = ESRuntimeReleaseTransferState.Completed;
            transferCurrentSubject = string.Empty;
            transferCurrentFileBytes = 0;
            transferCurrentFileSize = 0;
            transferCurrentInitialBytes = 0;
            transferRetryAttempt = 0;
            Report(ESRuntimeReleaseDownloadStage.Completed, "TransferComplete", transferCompletedFileCount, transferPlanFiles.Count);
            PublishTransferSnapshot(true);
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
                    bool useEmbedded = ShouldUseEmbedded(bundle.deliveryMode);
                    if (useEmbedded)
                    {
                        string streamingSource = CombineLocalReleasePath(bundle.embeddedRelativePath);
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
                        string localPath = ReleaseCachePath("Libraries", libraryFolder, assetBundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
                        await EnsureFileAsync(bundle.fileUrl, localPath, relativePath, bundle.size, bundle.sha256, token, false);
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

        private async UniTask<T> DownloadJsonAsync<T>(string url, string localPath, string expectedHash, CancellationToken token, bool? localSourceOverride = null) where T : class
        {
            string text = await DownloadTextAsync(url, localPath, expectedHash, token, localSourceOverride);
            try { return JsonConvert.DeserializeObject<T>(text); }
            catch (Exception exception) { throw new InvalidDataException("JSON 解析失败：" + url, exception); }
        }

        private async UniTask<string> DownloadTextAsync(string url, string localPath, string expectedHash, CancellationToken token, bool? localSourceOverride = null)
        {
            return await ExecuteForCacheFileAsync(localPath,
                () => DownloadTextCoreAsync(url, localPath, expectedHash, token, localSourceOverride), token);
        }

        private async UniTask<string> DownloadTextCoreAsync(string url, string localPath, string expectedHash, CancellationToken token, bool? localSourceOverride)
        {
            bool localSource = localSourceOverride ?? useLocalReleaseSource;
            if (localSource)
            {
                string localText = await RequestTextAsync(url, token, true);
                if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(ESResManifestIntegrity.ComputeFileSha256FromText(localText), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("StreamingAssets 清单 Hash 不匹配：" + url);
                return localText;
            }

            if (!string.IsNullOrEmpty(expectedHash) && File.Exists(localPath) && ESResManifestIntegrity.VerifyFileSha256(localPath, expectedHash))
            {
                VerboseLog("复用已校验清单缓存 | " + url);
                return File.ReadAllText(localPath);
            }
            VerboseLog("请求远端清单 | " + url);
            string text = await RequestTextAsync(url, token, false);
            if (!string.IsNullOrEmpty(expectedHash) && !string.Equals(ESResManifestIntegrity.ComputeFileSha256FromText(text), expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("下载文件 Hash 不匹配：" + url);
            WriteTextAtomically(localPath, text);
            VerboseLog("清单校验完成并写入缓存 | " + url);
            return text;
        }

        private async UniTask EnsureFileAsync(string url, string localPath, string relativePath, long expectedSize, string expectedHash, CancellationToken token, bool? localSourceOverride = null)
        {
            await ExecuteForCacheFileAsync(localPath,
                () => EnsureFileCoreAsync(url, localPath, relativePath, expectedSize, expectedHash, token, localSourceOverride), token);
        }

        private async UniTask EnsureFileCoreAsync(string url, string localPath, string relativePath, long expectedSize, string expectedHash, CancellationToken token, bool? localSourceOverride)
        {
            if (expectedSize < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedSize), expectedSize, "Expected file size cannot be negative.");
            if (string.IsNullOrWhiteSpace(expectedHash))
                throw new ArgumentException("Expected SHA-256 is required for release files.", nameof(expectedHash));
            if (IsVerified(relativePath, localPath, expectedSize, expectedHash))
            {
                CompleteTransferFile(relativePath);
                VerboseLog("复用已校验文件缓存 | " + relativePath + " | " + expectedSize + " bytes");
                return;
            }
            bool localSource = localSourceOverride ?? useLocalReleaseSource;
            if (localSource)
            {
                string sourcePath = ResolveLocalReleasePath(url);
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                BeginTransferFile(relativePath, expectedSize, 1);
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
                        request.downloadHandler = CreateFileDownloadHandler(localSourcePartPath, false);
                        await request.SendWebRequest().ToUniTask(progress: new TransferRequestProgress(this, request, relativePath, 0), cancellationToken: token);
                        if (request.result != UnityWebRequest.Result.Success) throw new IOException("Initial AssetBundle read failed: " + sourcePath + " / " + request.error);
                        PersistBufferedFileDownload(request, localSourcePartPath, false);
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
                CompleteTransferFile(relativePath);
                return;
            }
            string partPath = localPath + ".part";
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
            if (TryPromoteVerifiedPart(partPath, localPath, relativePath, expectedSize, expectedHash))
                return;

            if (File.Exists(partPath) && new FileInfo(partPath).Length >= expectedSize)
            {
                VerboseLog("丢弃无效完整残片 | " + relativePath);
                File.Delete(partPath);
            }

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                long length = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                BeginTransferFile(relativePath, expectedSize, attempt);
                ReportTransferFileBytes(relativePath, length);
                VerboseLog("下载资源文件 | Attempt=" + attempt + "/" + MaxAttempts + " | Resume=" + length + " | Size=" + expectedSize + " | " + url);
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 30;
                    if (length > 0) request.SetRequestHeader("Range", "bytes=" + length + "-");
                    request.downloadHandler = CreateFileDownloadHandler(partPath, length > 0);
                    await request.SendWebRequest().ToUniTask(progress: new TransferRequestProgress(this, request, relativePath, length), cancellationToken: token);
                    bool rangeIgnored = length > 0 && request.responseCode == 200;
                    bool rangeRejected = length > 0 && request.responseCode == 416;
                    bool invalidContentRange = length > 0 && request.responseCode == 206
                        && !HasExpectedContentRange(request, length);
                    if (request.result == UnityWebRequest.Result.Success && !rangeIgnored && !rangeRejected && !invalidContentRange)
                        PersistBufferedFileDownload(request, partPath, length > 0);
                    if (rangeRejected)
                    {
                        if (TryPromoteVerifiedPart(partPath, localPath, relativePath, expectedSize, expectedHash))
                            return;

                        VerboseLog("Range 416 且残片无效，改为完整下载 | " + relativePath);
                        DeleteFileIfExists(partPath);
                    }
                    else if (request.result != UnityWebRequest.Result.Success || rangeIgnored || invalidContentRange)
                    {
                        VerboseLog("资源下载失败，准备重试 | HTTP=" + request.responseCode + " | " + request.error + " | " + url);
                        if (rangeIgnored || invalidContentRange)
                            DeleteFileIfExists(partPath);
                    }
                }
                if (TryPromoteVerifiedPart(partPath, localPath, relativePath, expectedSize, expectedHash))
                {
                    return;
                }

                // Network interruption may still have produced a valid prefix. Keep only a
                // short prefix for the next Range request; a complete/oversized invalid part
                // can never recover by appending and must be discarded.
                if (File.Exists(partPath) && new FileInfo(partPath).Length >= expectedSize)
                    DeleteFileIfExists(partPath);
                if (attempt == MaxAttempts)
                    throw new IOException("资源文件下载失败或完整性校验失败：" + url);
                await UniTask.Delay(TimeSpan.FromSeconds(attempt), cancellationToken: token);
            }
            throw new IOException("资源文件完整性校验失败：" + url);
        }

        private bool TryPromoteVerifiedPart(string partPath, string localPath, string relativePath, long expectedSize, string expectedHash)
        {
            if (!File.Exists(partPath) || new FileInfo(partPath).Length != expectedSize)
                return false;
            ReportTransferVerification(relativePath, expectedSize);
            if (!ESResManifestIntegrity.VerifyFileSha256(partPath, expectedHash))
                return false;

            DeleteFileIfExists(localPath);
            File.Move(partPath, localPath);
            verified[relativePath] = new ESRuntimeVerifiedFile { relativePath = relativePath, size = expectedSize, sha256 = expectedHash };
            CompleteTransferFile(relativePath);
            VerboseLog("资源校验完成 | " + relativePath + " | " + expectedSize + " bytes");
            return true;
        }

        private static bool HasExpectedContentRange(UnityWebRequest request, long expectedStart)
        {
            string contentRange = request.GetResponseHeader("Content-Range");
            return !string.IsNullOrEmpty(contentRange)
                && contentRange.StartsWith("bytes " + expectedStart + "-", StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static DownloadHandler CreateFileDownloadHandler(string path, bool append)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // DownloadHandlerFile is not available on every WebGL runtime. The buffer fallback
            // trades per-file memory for a deterministic write into persistentDataPath.
            return new DownloadHandlerBuffer();
#else
            return new DownloadHandlerFile(path, append);
#endif
        }

        private static void PersistBufferedFileDownload(UnityWebRequest request, string path, bool append)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            byte[] bytes = request.downloadHandler.data;
            if (bytes == null)
                throw new IOException("WebGL download returned no file data: " + request.url);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
                stream.Write(bytes, 0, bytes.Length);
#endif
        }

        private async UniTask<T> ExecuteReleaseOperationAsync<T>(Func<UniTask<T>> operation, CancellationToken token)
        {
            await releaseOperationGate.WaitAsync(token);
            try
            {
                return await operation();
            }
            finally
            {
                releaseOperationGate.Release();
            }
        }

        private async UniTask ExecuteForCacheFileAsync(string localPath, Func<UniTask> operation, CancellationToken token)
        {
            CacheFileGate gate = AcquireCacheFileGate(localPath);
            bool entered = false;
            try
            {
                await gate.Semaphore.WaitAsync(token);
                entered = true;
                await operation();
            }
            finally
            {
                if (entered)
                    gate.Semaphore.Release();
                ReleaseCacheFileGate(localPath, gate);
            }
        }

        private async UniTask<T> ExecuteForCacheFileAsync<T>(string localPath, Func<UniTask<T>> operation, CancellationToken token)
        {
            CacheFileGate gate = AcquireCacheFileGate(localPath);
            bool entered = false;
            try
            {
                await gate.Semaphore.WaitAsync(token);
                entered = true;
                return await operation();
            }
            finally
            {
                if (entered)
                    gate.Semaphore.Release();
                ReleaseCacheFileGate(localPath, gate);
            }
        }

        private CacheFileGate AcquireCacheFileGate(string localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath))
                throw new ArgumentException("A cache file path is required.", nameof(localPath));

            lock (cacheFileGateSync)
            {
                if (!cacheFileGates.TryGetValue(localPath, out CacheFileGate gate))
                {
                    gate = new CacheFileGate();
                    cacheFileGates.Add(localPath, gate);
                }
                gate.UserCount++;
                return gate;
            }
        }

        private void ReleaseCacheFileGate(string localPath, CacheFileGate gate)
        {
            lock (cacheFileGateSync)
            {
                gate.UserCount--;
                if (gate.UserCount != 0)
                    return;

                if (cacheFileGates.TryGetValue(localPath, out CacheFileGate current) && ReferenceEquals(current, gate))
                    cacheFileGates.Remove(localPath);
                gate.Semaphore.Dispose();
            }
        }

        private bool IsVerified(string relativePath, string localPath, long size, string hash)
        {
            if (!File.Exists(localPath) || new FileInfo(localPath).Length != size) return false;
            if (verified.TryGetValue(relativePath, out var record) && record.size == size && string.Equals(record.sha256, hash, StringComparison.OrdinalIgnoreCase)) return true;
            if (!ESResManifestIntegrity.VerifyFileSha256(localPath, hash)) return false;
            verified[relativePath] = new ESRuntimeVerifiedFile { relativePath = relativePath, size = size, sha256 = hash };
            return true;
        }

        private async UniTask<string> RequestTextAsync(string url, CancellationToken token, bool? localSourceOverride = null)
        {
            bool localSource = localSourceOverride ?? useLocalReleaseSource;
            if (localSource)
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
                if (IsRootReleaseUrl(url))
                {
                    // Versioned release files are immutable; only this stable pointer must
                    // revalidate with the CDN on every HotUpdate bootstrap.
                    request.SetRequestHeader("Cache-Control", "no-cache");
                    request.SetRequestHeader("Pragma", "no-cache");
                }
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
                if (request.result == UnityWebRequest.Result.Success) return request.downloadHandler.text;
                if (attempt == MaxAttempts) throw new IOException("清单下载失败：" + url + "，" + request.error);
                await UniTask.Delay(TimeSpan.FromSeconds(attempt), cancellationToken: token);
            }
            throw new InvalidOperationException();
        }

        private bool IsRootReleaseUrl(string url)
        {
            string expected = useLocalReleaseSource
                ? CombineLocalReleasePath(platform + "/ESAssetReleaseManifest.json")
                : CombineUrl(settings.Path_Net, platform + "/ESAssetReleaseManifest.json");
            return string.Equals(url, expected, StringComparison.OrdinalIgnoreCase);
        }

        private void Report(ESRuntimeReleaseDownloadStage stage, string subject, int completedCount = 0, int totalCount = 0)
        {
            VerboseLog("阶段=" + stage + " | 目标=" + (subject ?? string.Empty) + " | " + completedCount + "/" + totalCount);
            var progress = new ESRuntimeReleaseDownloadProgress(stage, subject, completedCount, totalCount);
            RecordDiagnosticProgress(progress);
            ProgressChanged?.Invoke(progress);
        }

        private void VerboseLog(string message)
        {
            if (settings != null && settings.EnableResVerboseLog)
                Debug.Log("[ESRes][Release] " + message);
        }

        private void PrepareVerifiedIndex(string releaseVersion)
        {
            verified.Clear();
            verifiedReleaseVersion = releaseVersion;
            activeReleaseCacheRoot = Path.Combine(cacheRoot, "Releases", SafePathSegment(releaseVersion, "ReleaseVersion"));
            string path = ReleaseCachePath("ESVerifiedFileIndex.json");
            if (!File.Exists(path)) return;
            try
            {
                ESRuntimeVerifiedFileIndex index = JsonConvert.DeserializeObject<ESRuntimeVerifiedFileIndex>(File.ReadAllText(path));
                if (index == null || !string.Equals(index.releaseVersion, releaseVersion, StringComparison.Ordinal)) return;
                foreach (var file in index.files ?? new List<ESRuntimeVerifiedFile>()) verified[file.relativePath] = file;
            }
            catch { verified.Clear(); }
        }
        private void SaveVerifiedIndex() => WriteTextAtomically(ReleaseCachePath("ESVerifiedFileIndex.json"), JsonConvert.SerializeObject(new ESRuntimeVerifiedFileIndex { releaseVersion = verifiedReleaseVersion, files = verified.Values.OrderBy(item => item.relativePath, StringComparer.Ordinal).ToList() }, Formatting.Indented));
        private static void WriteTextAtomically(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL's virtual filesystem does not provide a durable File.Replace contract.
            // The verified index is an optimization only: a crash here falls back to SHA-256.
            DeleteFileIfExists(path);
            File.Move(temp, path);
#else
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
#endif
        }
        private string ResolveLocalReleasePath(string url)
        {
            if (!string.IsNullOrEmpty(url) && url.StartsWith(localReleaseRoot, StringComparison.OrdinalIgnoreCase))
                return url;
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
