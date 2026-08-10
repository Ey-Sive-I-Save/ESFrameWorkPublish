using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESAssetBundlePublisher
    {
        public static void Publish()
        {
            try
            {
                SetPublishProgress("准备发布环境", 0.01f);
                PublishCore();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void PublishCore()
        {
            ESAssetPipelineIO.EnsureAssetBundleReleaseMode();
            string platform = ESAssetPipelineIO.PlatformName;
            string stagingRoot = ESAssetPipelineIO.StagingRoot(platform);
            if (!Directory.Exists(stagingRoot)) throw new DirectoryNotFoundException("资源包暂存目录不存在，请先执行“构建资源包”：" + stagingRoot);
            var buildSet = ESAssetPipelineIO.ReadJson<ESAssetBuildSet>(Path.Combine(stagingRoot, ESAssetPipelineIO.BuildSetFileName));
            var stageFolders = buildSet.libraryFolders.Select(folder => ESAssetPipelineIO.StagingLibraryFolder(platform, folder)).ToList();
            if (stageFolders.Count == 0) throw new InvalidOperationException("资源包暂存目录中没有可发布的资源库。");
            ValidateBuildSetFingerprints(buildSet, platform);
            SetPublishProgress("校验暂存资源、Manifest 与 Hash", 0.05f);
            ValidateAll(stageFolders);
            var consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                .Where(item => item != null)
                .OrderBy(item => AssetDatabase.GetAssetPath(item), StringComparer.Ordinal)
                .ToList();
            if (consumers.Count == 0) throw new InvalidOperationException("未找到资源使用者（Consumer）。请先创建至少一个 Consumer。\n");
            ESCodeModuleEditorIntegration.ValidateConsumerReleasePrepared(consumers, platform);
            ValidateConsumerIdentities(consumers);
            ValidatePathSegment(ESGlobalResSetting.Instance.Version, "资源发布版本");

            DateTime publishLocalTime = DateTime.Now;
            string releaseVersion = ESGlobalResSetting.Instance.Version + "." + publishLocalTime.ToString("yyyyMMddHHmmssfff");
            bool includeInitialPackage = ESGlobalResSetting.Instance.AssetRunMode == ESAssetRunMode.LocalBuild;
            string streamingReleaseRoot = ToAbsolutePath(ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_);
            string initialRoot = includeInitialPackage ? streamingReleaseRoot : null;
            if (!includeInitialPackage)
                RemoveGeneratedStreamingAssets(platform);
            string localTestRoot = ESAssetPipelineIO.LocalTestRoot(platform);
            string cdnRoot = Path.Combine(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath, platform);
            var release = new ESAssetReleaseManifest { platform = platform, releaseVersion = releaseVersion, channel = "default", publishedUtc = DateTime.UtcNow.ToString("O") };
            var publishedLibraries = new Dictionary<string, PublishedLibrary>(StringComparer.Ordinal);
            var bundleIndex = new ESAssetReleaseBundleIndex { platform = platform, releaseVersion = releaseVersion };

            SetPublishProgress("复制并校验 Library 发布产物", 0.55f);
            foreach (string stageFolder in stageFolders)
            {
                var identity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(Path.Combine(stageFolder, ESAssetPipelineIO.LibraryIdentityFileName));
                string libraryFolder = Path.GetFileName(stageFolder);
                // Staging 的实际规范化目录名是发布与运行时共同使用的 LibraryFolder 权威值。
                // 同时兼容由旧 Planner 生成、Identity 中仍带前导下划线的 GameCore 暂存产物。
                identity.libraryFolder = libraryFolder;
                if (!ESAssetDeliveryModeEditorUtility.IsValid(identity.deliveryMode))
                    throw new InvalidDataException("Library 分发方式无效：" + libraryFolder);
                bool hasEmbeddedCopy = identity.deliveryMode != ESAssetDeliveryMode.Remote;
                bool hasRemoteCopy = identity.deliveryMode != ESAssetDeliveryMode.BuiltIn;
                identity.version = identity.deliveryMode == ESAssetDeliveryMode.BuiltIn ? "embedded" : releaseVersion;
                identity.channel = identity.deliveryMode == ESAssetDeliveryMode.BuiltIn ? "embedded" : release.channel;
                var manifest = ESAssetPipelineIO.ReadJson<ESAssetBundleManifest>(Path.Combine(stageFolder, ESAssetPipelineIO.BundleManifestFileName));
                string relativeBase = ESAssetPipelineIO.ReleaseLibraryRelativeBase(platform, releaseVersion, libraryFolder);
                identity.catalogUrl = hasRemoteCopy
                    ? CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativeBase + ESAssetPipelineIO.CatalogFileName)
                    : string.Empty;
                identity.assetBundleManifestUrl = hasRemoteCopy
                    ? CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativeBase + ESAssetPipelineIO.BundleManifestFileName)
                    : string.Empty;
                if (includeInitialPackage && hasEmbeddedCopy)
                    CopyStage(stageFolder, ESAssetPipelineIO.EmbeddedLibraryFolder(initialRoot, platform, libraryFolder), manifest, identity, true);
                string localTestLibraryFolder = ESAssetPipelineIO.ReleaseLibraryFolder(localTestRoot, string.Empty, releaseVersion, libraryFolder);
                CopyStage(stageFolder, localTestLibraryFolder, manifest, identity);
                if (hasRemoteCopy)
                    CopyStage(stageFolder, ESAssetPipelineIO.ReleaseLibraryFolder(cdnRoot, string.Empty, releaseVersion, libraryFolder), manifest, identity);
                release.libraries.Add(new ESAssetReleaseLibrary
                {
                    libraryName = identity.libraryName,
                    version = identity.version,
                    deliveryMode = identity.deliveryMode,
                    catalogUrl = identity.catalogUrl,
                    catalogSha256 = identity.catalogSha256,
                    assetBundleManifestUrl = identity.assetBundleManifestUrl,
                    assetBundleManifestSha256 = identity.assetBundleManifestSha256
                });
                string identityRelativePath = relativeBase + ESAssetPipelineIO.LibraryIdentityFileName;
                string embeddedRelativeBase = ESAssetPipelineIO.EmbeddedLibraryRelativeBase(platform, libraryFolder);
                string publishedIdentityPath = Path.Combine(localTestLibraryFolder, ESAssetPipelineIO.LibraryIdentityFileName);
                string publishedIdentitySha256 = ESResManifestIntegrity.ComputeFileSha256(publishedIdentityPath);
                if (!includeInitialPackage && identity.deliveryMode == ESAssetDeliveryMode.BuiltIn)
                    ValidateExistingEmbeddedLibrary(ESAssetPipelineIO.EmbeddedLibraryFolder(streamingReleaseRoot, platform, libraryFolder), manifest, publishedIdentitySha256);
                publishedLibraries.Add(libraryFolder, new PublishedLibrary
                {
                    identity = identity,
                    manifest = manifest,
                    identityUrl = hasRemoteCopy ? CombineUrl(ESGlobalResSetting.Instance.Path_Net, identityRelativePath) : string.Empty,
                    embeddedIdentityRelativePath = hasEmbeddedCopy ? embeddedRelativeBase + ESAssetPipelineIO.LibraryIdentityFileName : string.Empty,
                    identitySha256 = publishedIdentitySha256
                });
                foreach (var assetBundle in manifest.assetBundles)
                    bundleIndex.assetBundles.Add(new ESAssetReleaseBundleRecord
                    {
                        libraryFolder = libraryFolder,
                        assetBundleKey = assetBundle.assetBundleKey,
                        deliveryMode = identity.deliveryMode,
                        fileUrl = hasRemoteCopy ? CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativeBase + assetBundle.localRelativePath) : string.Empty,
                        localRelativePath = assetBundle.localRelativePath,
                        embeddedRelativePath = hasEmbeddedCopy ? embeddedRelativeBase + assetBundle.localRelativePath : string.Empty,
                        sha256 = assetBundle.sha256,
                        crc = assetBundle.crc,
                        size = assetBundle.size,
                        dependencies = assetBundle.dependencies.OrderBy(item => item, StringComparer.Ordinal).ToList()
                    });
            }

            if (bundleIndex.assetBundles.GroupBy(item => item.assetBundleKey, StringComparer.Ordinal).Any(group => group.Count() != 1))
                throw new InvalidDataException("全局 Bundle 索引包含重复 AssetBundleKey。");
            bundleIndex.assetBundles = bundleIndex.assetBundles.OrderBy(item => item.assetBundleKey, StringComparer.Ordinal).ToList();
            string bundleIndexRelativePath = platform + "/" + releaseVersion + "/" + ESAssetPipelineIO.ReleaseBundleIndexFileName;
            string initialBundleIndexPath = includeInitialPackage
                ? Path.Combine(initialRoot, platform, releaseVersion, ESAssetPipelineIO.ReleaseBundleIndexFileName)
                : null;
            string localBundleIndexPath = Path.Combine(localTestRoot, releaseVersion, ESAssetPipelineIO.ReleaseBundleIndexFileName);
            string cdnBundleIndexPath = Path.Combine(cdnRoot, releaseVersion, ESAssetPipelineIO.ReleaseBundleIndexFileName);
            if (includeInitialPackage)
                ESAssetPipelineIO.WriteJsonCreateNew(initialBundleIndexPath, bundleIndex);
            ESAssetPipelineIO.WriteJsonCreateNew(localBundleIndexPath, bundleIndex);
            ESAssetPipelineIO.WriteJsonCreateNew(cdnBundleIndexPath, bundleIndex);
            release.bundleIndexUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, bundleIndexRelativePath);
            release.bundleIndexSha256 = ESResManifestIntegrity.ComputeFileSha256(cdnBundleIndexPath);
            SetPublishProgress("校验全局 Bundle 索引与依赖闭包", 0.72f);
            ValidatePublishedBundleIndex(cdnRoot, streamingReleaseRoot, includeInitialPackage, releaseVersion, bundleIndex);

            var totalConsumers = consumers.Where(item => item.IsTotalConsumer).ToList();
            if (totalConsumers.Count == 0)
                throw new InvalidOperationException("资源发布缺少总 Consumer（启动入口）。请在唯一正确的 Consumer 上勾选“总 Consumer（启动入口）”；发布器不会猜测或自动修改配置。");
            if (totalConsumers.Count != 1) throw new InvalidOperationException("资源发布只能有一个总资源使用者。请取消多余 Consumer 的“总 Consumer（启动入口）”勾选。");
            var consumerPublications = new Dictionary<string, ESAssetConsumerReference>(StringComparer.Ordinal);
            var publishStack = new HashSet<string>(StringComparer.Ordinal);
            SetPublishProgress("生成 Consumer、GameCore 与代码包发布清单", 0.82f);
            ESAssetConsumerReference totalConsumer = PublishConsumer(totalConsumers[0], consumers, publishedLibraries, consumerPublications, publishStack, platform, releaseVersion, initialRoot, localTestRoot, cdnRoot);
            release.totalConsumerUrl = totalConsumer.consumerUrl;
            release.totalConsumerSha256 = totalConsumer.consumerSha256;

            SetPublishProgress("原子写入发布根清单并生成上传计划", 0.94f);
            if (includeInitialPackage)
                ESAssetPipelineIO.WriteJson(Path.Combine(initialRoot, platform, ESAssetPipelineIO.ReleaseManifestFileName), release, true);
            ESAssetPipelineIO.WriteJson(Path.Combine(localTestRoot, ESAssetPipelineIO.ReleaseManifestFileName), release, true);
            ESAssetPipelineIO.WriteJson(Path.Combine(cdnRoot, ESAssetPipelineIO.ReleaseManifestFileName), release, true);
            string uploadPlanPath = WriteManualUploadPlan(cdnRoot, platform, releaseVersion);
            PruneManualUploadPlans(Path.GetDirectoryName(uploadPlanPath), 10);
            // 只有新的本机 Root 与第五步上传计划都已完整写入，才回收旧版本。
            // 因此清理发生异常时，Root 已经指向完整的新版本，而不会指向被删除的旧版本。
            PruneGeneratedReleaseVersions(localTestRoot, releaseVersion);
            if (includeInitialPackage)
                PruneGeneratedReleaseVersions(Path.Combine(initialRoot, platform), releaseVersion);
            AssetDatabase.Refresh();
            SetPublishProgress("发布完成", 1f);
            Debug.Log($"[ESAssetBundlePublisher] 发布完成：{releaseVersion}，资源库数量 {release.libraries.Count}。根清单已最后原子写入。\n手动 OSS 上传计划：{uploadPlanPath}");
        }

        private static void SetPublishProgress(string message, float progress)
        {
            EditorUtility.DisplayProgressBar("ES 资源发布", message, Mathf.Clamp01(progress));
        }

        /// <summary>
        /// ConsumerId 同时是发布文件名、URL 路径段和运行时索引键。必须在生成任何发布副本前一次性收口，
        /// 不能等递归发布时才让同名 Consumer 静默复用同一份清单。
        /// </summary>
        private static void ValidateConsumerIdentities(IReadOnlyList<ESAssetLibraryConsumer> consumers)
        {
            var owners = new Dictionary<string, ESAssetLibraryConsumer>(StringComparer.Ordinal);
            foreach (ESAssetLibraryConsumer consumer in consumers)
            {
                string id = consumer.ConsumerId?.Trim() ?? string.Empty;
                ValidatePathSegment(id, "Consumer 稳定 ID：" + consumer.Name);
                if (owners.TryGetValue(id, out ESAssetLibraryConsumer existing))
                    throw new InvalidOperationException("Consumer 稳定 ID 重复：" + id + " / " + existing.Name + " 与 " + consumer.Name);
                owners.Add(id, consumer);

                var required = consumer.RequiredConsumers ?? new List<ESAssetLibraryConsumer>();
                var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (ESAssetLibraryConsumer dependency in required)
                {
                    if (dependency == null)
                        throw new InvalidOperationException("Consumer 依赖列表包含空引用：" + consumer.Name);
                    string dependencyId = dependency.ConsumerId?.Trim() ?? string.Empty;
                    if (!dependencyIds.Add(dependencyId))
                        throw new InvalidOperationException("Consumer 依赖重复：" + consumer.Name + " -> " + dependencyId);
                }
            }
        }

        private static void ValidatePathSegment(string value, string fieldName)
        {
            string segment = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(segment)
                || !string.Equals(segment, Path.GetFileName(segment), StringComparison.Ordinal)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || segment == "." || segment == "..")
                throw new InvalidOperationException(fieldName + " 不是合法路径片段：" + value);
        }

        private static void ValidateAll(List<string> stageFolders)
        {
            var packages = new Dictionary<string, ESAssetBundleRecord>(StringComparer.Ordinal);
            foreach (string folder in stageFolders)
            {
                string manifestPath = Path.Combine(folder, ESAssetPipelineIO.BundleManifestFileName);
                string identityPath = Path.Combine(folder, ESAssetPipelineIO.LibraryIdentityFileName);
                var manifest = ESAssetPipelineIO.ReadJson<ESAssetBundleManifest>(manifestPath);
                var identity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(identityPath);
                if (manifest == null || manifest.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion)
                    throw new InvalidDataException($"[ESRes][Publish] AB Manifest 协议版本过旧，请重新执行 AB 构建：{manifestPath}");
                if (identity == null || identity.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion)
                    throw new InvalidDataException($"[ESRes][Publish] Library Identity 协议版本过旧，请重新执行 AB 构建：{identityPath}");
                if (!ESResManifestIntegrity.VerifyFileSha256(manifestPath, identity.assetBundleManifestSha256)) throw new InvalidDataException("AB Manifest Hash 不匹配：" + manifestPath);
                string catalogPath = Path.Combine(folder, ESAssetPipelineIO.CatalogFileName);
                if (!string.IsNullOrEmpty(identity.catalogSha256) && !ESResManifestIntegrity.VerifyFileSha256(catalogPath, identity.catalogSha256)) throw new InvalidDataException("Catalog Hash 不匹配：" + catalogPath);
                foreach (var assetBundle in manifest.assetBundles)
                {
                    string file = ESAssetPipelineIO.ResolveGeneratedRelativePath(folder, assetBundle.localRelativePath);
                    if (!File.Exists(file) || new FileInfo(file).Length != assetBundle.size || !ESResManifestIntegrity.VerifyFileSha256(file, assetBundle.sha256)) throw new InvalidDataException("AB 文件完整性失败：" + file);
                    if (packages.ContainsKey(assetBundle.assetBundleKey)) throw new InvalidDataException("重复 AssetBundleKey：" + assetBundle.assetBundleKey);
                    packages.Add(assetBundle.assetBundleKey, assetBundle);
                }
            }
            foreach (var package in packages.Values) foreach (string dependency in package.dependencies)
                if (!packages.ContainsKey(dependency)) throw new InvalidDataException($"AB 依赖缺失：{package.assetBundleKey} -> {dependency}");
        }

        private static void ValidateBuildSetFingerprints(ESAssetBuildSet buildSet, string platform)
        {
            if (buildSet == null)
                throw new InvalidDataException("BuildSet 缺失，请重新执行 AB 构建。");
            string planFolder = ESAssetPipelineIO.PlanRoot(platform);
            string planPath = Path.Combine(planFolder, ESAssetPipelineIO.PlanFileName);
            string assetListPath = Path.Combine(planFolder, ESAssetPipelineIO.AssetListFileName);
            if (!File.Exists(planPath) || !File.Exists(assetListPath))
                throw new FileNotFoundException("BuildPlan/AssetList 缺失，请重新执行规划与构建。", planPath);

            string currentPlanFingerprint = ESResManifestIntegrity.ComputeFileSha256(planPath);
            string currentAssetListFingerprint = ESResManifestIntegrity.ComputeFileSha256(assetListPath);
            if (string.IsNullOrWhiteSpace(buildSet.planFingerprint)
                || !string.Equals(buildSet.planFingerprint, currentPlanFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("BuildPlan 已变化，拒绝发布。请重新执行规划与构建。");
            if (string.IsNullOrWhiteSpace(buildSet.assetListFingerprint)
                || !string.Equals(buildSet.assetListFingerprint, currentAssetListFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("AssetList 已变化，拒绝发布。请重新执行规划与构建。");

            ESAssetBundleBuildPlan plan = ESAssetPipelineIO.ReadJson<ESAssetBundleBuildPlan>(planPath);
            string currentSourceFingerprint = ESAssetBundleBuilder.ComputeSourceFingerprint(plan);
            if (string.IsNullOrWhiteSpace(buildSet.sourceFingerprint)
                || !string.Equals(buildSet.sourceFingerprint, currentSourceFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("构建源指纹已变化，拒绝发布。请重新执行构建。");
        }

        private static string WriteManualUploadPlan(string cdnRoot, string platform, string releaseVersion)
        {
            string releaseFolder = Path.Combine(cdnRoot, releaseVersion);
            string rootManifestPath = Path.Combine(cdnRoot, ESAssetPipelineIO.ReleaseManifestFileName);
            if (!Directory.Exists(releaseFolder) || !File.Exists(rootManifestPath))
                throw new InvalidOperationException("无法生成手动上传计划：发布目录不完整。");

            string validatedCdnRoot = Path.GetFullPath(cdnRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (ContainsExistingReparsePoint(validatedCdnRoot, validatedCdnRoot))
                throw new UnauthorizedAccessException("上传计划源根不能穿过 junction/symlink：" + cdnRoot);
            var plan = new ESAssetReleaseUploadPlan
            {
                platform = platform,
                releaseVersion = releaseVersion,
                sourceRoot = cdnRoot,
                publicBaseUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, platform + "/"),
                generatedUtc = DateTime.UtcNow.ToString("O")
            };
            int order = 0;
            foreach (string sourcePath in ESManagedFileIO.EnumerateFilesSafely(releaseFolder, "*").OrderBy(item => item, StringComparer.Ordinal))
            {
                if (ContainsExistingReparsePoint(validatedCdnRoot, sourcePath))
                    throw new UnauthorizedAccessException("上传计划不能收集 junction/symlink 文件：" + sourcePath);
                string relativePath = releaseVersion + "/" + sourcePath.Substring(releaseFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                plan.files.Add(CreateUploadPlanFile(sourcePath, relativePath, ++order, false));
            }
            if (ContainsExistingReparsePoint(validatedCdnRoot, rootManifestPath))
                throw new UnauthorizedAccessException("上传计划根清单不能指向重解析文件：" + rootManifestPath);
            plan.files.Add(CreateUploadPlanFile(rootManifestPath, ESAssetPipelineIO.ReleaseManifestFileName, ++order, true));

            string planFolder = ESAssetPipelineIO.ManualUploadPlansRoot(platform);
            string planPath = Path.Combine(planFolder, releaseVersion + ".json");
            ESAssetPipelineIO.WriteJsonCreateNew(planPath, plan);
            return planPath;
        }

        private static ESAssetReleaseUploadPlanFile CreateUploadPlanFile(string sourcePath, string relativePath, int uploadOrder, bool uploadLast)
        {
            return new ESAssetReleaseUploadPlanFile
            {
                sourcePath = sourcePath,
                relativePath = relativePath,
                publicUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, ESAssetPipelineIO.PlatformName + "/" + relativePath),
                sha256 = ESResManifestIntegrity.ComputeFileSha256(sourcePath),
                size = new FileInfo(sourcePath).Length,
                uploadOrder = uploadOrder,
                uploadLast = uploadLast,
                cacheControl = uploadLast
                    ? "no-cache, max-age=0, must-revalidate"
                    : "public, max-age=31536000, immutable"
            };
        }

        private static void CopyStage(string sourceFolder, string destinationFolder, ESAssetBundleManifest manifest, ESAssetLibraryIdentity identity,
            bool replaceManagedDestination = false)
        {
            string destination = Path.GetFullPath(destinationFolder ?? string.Empty);
            string destinationParent = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(destinationParent))
                throw new InvalidDataException("发布 Library 目标目录无效：" + destinationFolder);

            EnsurePublishDirectory(destinationParent);
            string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                EnsurePublishDirectory(staging);
                var relativePaths = manifest.assetBundles.Select(item => item.localRelativePath)
                    .Concat(new[] { ESAssetPipelineIO.BundleManifestFileName, ESAssetPipelineIO.CatalogFileName })
                    .Distinct(StringComparer.Ordinal);
                foreach (string relativePath in relativePaths)
                {
                    string sourcePath = ESAssetPipelineIO.ResolveGeneratedRelativePath(sourceFolder, relativePath);
                    string stagingPath = ESAssetPipelineIO.ResolveGeneratedRelativePath(staging, relativePath);
                    EnsurePublishDirectory(Path.GetDirectoryName(stagingPath));
                    CopyFile(sourcePath, stagingPath);
                }

                ESAssetPipelineIO.WriteJsonCreateNew(Path.Combine(staging, ESAssetPipelineIO.LibraryIdentityFileName), identity);
                VerifyPublishedLibrary(staging, manifest, identity);
                CommitStagedLibraryDirectory(staging, destination, replaceManagedDestination, manifest, identity);
                staging = null;
                VerifyPublishedLibrary(destination, manifest, identity);
            }
            finally
            {
                if (!string.IsNullOrEmpty(staging) && Directory.Exists(staging))
                    DeletePublishDirectory(staging);
            }
        }

        private static void ValidateExistingEmbeddedLibrary(string embeddedFolder, ESAssetBundleManifest manifest, string expectedIdentitySha256)
        {
            string identityPath = Path.Combine(embeddedFolder, ESAssetPipelineIO.LibraryIdentityFileName);
            string catalogPath = Path.Combine(embeddedFolder, ESAssetPipelineIO.CatalogFileName);
            string manifestPath = Path.Combine(embeddedFolder, ESAssetPipelineIO.BundleManifestFileName);
            if (!ESResManifestIntegrity.VerifyFileSha256(identityPath, expectedIdentitySha256)
                || !File.Exists(catalogPath) || !File.Exists(manifestPath))
                throw new InvalidOperationException("随包 Library 与当前构建不一致，不能仅发布热更新；请切换 LocalBuild 重新生成应用首包：" + embeddedFolder);
            foreach (ESAssetBundleRecord bundle in manifest.assetBundles ?? new List<ESAssetBundleRecord>())
            {
                string path = ESAssetPipelineIO.ResolveGeneratedRelativePath(embeddedFolder, bundle.localRelativePath);
                if (!File.Exists(path) || new FileInfo(path).Length != bundle.size || !ESResManifestIntegrity.VerifyFileSha256(path, bundle.sha256))
                    throw new InvalidOperationException("随包 Library 的 Bundle 已变化，不能仅发布热更新；请重新生成应用首包：" + bundle.assetBundleKey);
            }
        }

        private static ESAssetConsumerReference PublishConsumer(ESAssetLibraryConsumer consumer, List<ESAssetLibraryConsumer> allConsumers, Dictionary<string, PublishedLibrary> libraries,
            Dictionary<string, ESAssetConsumerReference> publications, HashSet<string> stack, string platform, string releaseVersion, string initialRoot, string localTestRoot, string cdnRoot)
        {
            if (consumer == null) throw new InvalidOperationException("Consumer 引用为空。");
            if (string.IsNullOrEmpty(consumer.ConsumerId)) throw new InvalidOperationException("Consumer 缺少稳定 ID：" + consumer.Name);
            if (publications.TryGetValue(consumer.ConsumerId, out var existing)) return existing;
            if (!stack.Add(consumer.ConsumerId)) throw new InvalidOperationException("Consumer 依赖存在循环：" + consumer.Name);
            try
            {
                var manifest = new ESAssetConsumerManifest
                {
                    consumerId = consumer.ConsumerId,
                    name = consumer.Name,
                    description = consumer.Desc,
                    maintainer = consumer.Maintainer,
                    releaseNotes = consumer.ReleaseNotes,
                    version = consumer.RuntimeVersion,
                    platform = platform,
                    channel = consumer.Channel,
                    isTotalConsumer = consumer.IsTotalConsumer,
                    publishedUtc = DateTime.UtcNow.ToString("O")
                };
                manifest.tags.AddRange((consumer.Tags ?? new List<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal));
                foreach (var dependency in consumer.RequiredConsumers.Where(item => item != null).OrderBy(item => item.ConsumerId, StringComparer.Ordinal))
                    manifest.requiredConsumers.Add(PublishConsumer(dependency, allConsumers, libraries, publications, stack, platform, releaseVersion, initialRoot, localTestRoot, cdnRoot));
                AddLibraries(manifest.libraries, consumer.ConsumerLibFolders, true, libraries);
                AddLibraries(manifest.libraries, consumer.OptionalLibFolders, false, libraries);
                AddGameCoreLibrary(manifest.libraries, consumer, libraries);
                AddGameCoreAssets(manifest.gameCoreAssets, consumer.GameCoreAssets);
                ValidateExtensionAssetsForConsumer(consumer, manifest);
                AddResidentAssets(manifest, consumer, libraries);
                PublishCodePackages(manifest.codePackages, consumer, platform, releaseVersion, initialRoot, localTestRoot, cdnRoot);

                string relativePath = platform + "/" + releaseVersion + "/Consumers/" + consumer.ConsumerId + ".json";
                string initialPath = string.IsNullOrEmpty(initialRoot) ? null
                    : Path.Combine(initialRoot, platform, releaseVersion, "Consumers", consumer.ConsumerId + ".json");
                string localPath = Path.Combine(localTestRoot, releaseVersion, "Consumers", consumer.ConsumerId + ".json");
                string cdnPath = Path.Combine(cdnRoot, releaseVersion, "Consumers", consumer.ConsumerId + ".json");
                if (!string.IsNullOrEmpty(initialRoot))
                    ESAssetPipelineIO.WriteJsonCreateNew(initialPath, manifest);
                ESAssetPipelineIO.WriteJsonCreateNew(localPath, manifest);
                ESAssetPipelineIO.WriteJsonCreateNew(cdnPath, manifest);
                var result = new ESAssetConsumerReference { consumerId = consumer.ConsumerId, consumerUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativePath), consumerSha256 = ESResManifestIntegrity.ComputeFileSha256(cdnPath) };
                publications.Add(consumer.ConsumerId, result);
                return result;
            }
            finally { stack.Remove(consumer.ConsumerId); }
        }

        private static void AddLibraries(List<ESAssetConsumerLibraryReference> destination, IEnumerable<ESAssetLibrary> source, bool requiredAtBoot, Dictionary<string, PublishedLibrary> libraries)
        {
            foreach (var library in (source ?? Enumerable.Empty<ESAssetLibrary>()).Where(item => item != null).OrderBy(item => item.LibFolderName, StringComparer.Ordinal))
            {
                string folder = ESAssetPipelineIO.SafeSegment(library.LibFolderName);
                if (!libraries.TryGetValue(folder, out var published)) throw new InvalidOperationException("Consumer 引用了未构建的 Library：" + library.LibFolderName);
                var existing = destination.FirstOrDefault(item => string.Equals(item.libraryFolder, folder, StringComparison.Ordinal));
                if (existing != null) { existing.requiredAtBoot |= requiredAtBoot; continue; }
                destination.Add(new ESAssetConsumerLibraryReference
                {
                    libraryName = published.identity.libraryName,
                    libraryFolder = folder,
                    deliveryMode = published.identity.deliveryMode,
                    libraryIdentityUrl = published.identityUrl,
                    libraryIdentitySha256 = published.identitySha256,
                    embeddedIdentityRelativePath = published.embeddedIdentityRelativePath,
                    requiredAtBoot = requiredAtBoot
                });
            }
        }

        private static void AddGameCoreLibrary(List<ESAssetConsumerLibraryReference> destination, ESAssetLibraryConsumer consumer, Dictionary<string, PublishedLibrary> libraries)
        {
            if (consumer.GameCoreAssets == null || consumer.GameCoreAssets.Count == 0)
                return;

            string folder = ESAssetPipelineIO.GameCoreLibraryFolder(consumer.ConsumerId);
            if (!libraries.TryGetValue(folder, out PublishedLibrary published))
                throw new InvalidOperationException("Consumer GameCore 启动包未构建：" + consumer.Name);
            destination.Add(new ESAssetConsumerLibraryReference
            {
                libraryName = consumer.Name + " GameCore",
                libraryFolder = folder,
                deliveryMode = published.identity.deliveryMode,
                libraryIdentityUrl = published.identityUrl,
                libraryIdentitySha256 = published.identitySha256,
                embeddedIdentityRelativePath = published.embeddedIdentityRelativePath,
                requiredAtBoot = true
            });
        }

        private static void AddGameCoreAssets(List<ESAssetConsumerGameCoreReference> destination, IEnumerable<ESAssetReferBase> assets)
        {
            var identities = new HashSet<ESAssetIdentity>();
            foreach (ESAssetReferBase asset in assets ?? Enumerable.Empty<ESAssetReferBase>())
                if (asset != null && asset.IsValid && identities.Add(asset.AssetIdentity))
                    destination.Add(new ESAssetConsumerGameCoreReference { guid = asset.GUID, localFileId = asset.LocalFileId });

            var byIdentity = destination.ToDictionary(item => new ESAssetIdentity(item.guid, item.localFileId));
            foreach (ESAssetConsumerGameCoreReference root in destination)
            {
                string rootPath = AssetDatabase.GUIDToAssetPath(root.guid);
                if (string.IsNullOrWhiteSpace(rootPath))
                    throw new InvalidOperationException("GameCore 资产路径无效：" + root.guid);
                foreach (string dependencyPath in AssetDatabase.GetDependencies(rootPath, true))
                {
                    if (string.Equals(dependencyPath, rootPath, StringComparison.Ordinal))
                        continue;
                    foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(dependencyPath))
                    {
                        var dependency = loaded as ScriptableObject;
                        if (dependency == null || ESScriptableObjectClassification.GetClass(dependency) != ESScriptableObjectClass.GameCore)
                            continue;
                        ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(dependency);
                        var runtimeIdentity = new ESAssetIdentity(identity.guid, identity.localFileId);
                        if (!byIdentity.ContainsKey(runtimeIdentity))
                            throw new InvalidOperationException("GameCore 依赖未归属当前 Consumer：" + rootPath + " -> " + dependencyPath + " (" + identity.Key + ")");
                        if (!runtimeIdentity.Equals(new ESAssetIdentity(root.guid, root.localFileId)))
                            root.dependencies.Add(new ESAssetConsumerGameCoreDependencyReference { guid = identity.guid, localFileId = identity.localFileId });
                    }
                }
                root.dependencies = root.dependencies
                    .GroupBy(item => item.guid + ":" + item.localFileId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(item => item.guid, StringComparer.Ordinal)
                    .ThenBy(item => item.localFileId)
                    .ToList();
            }
        }

        private static void ValidateExtensionAssetsForConsumer(ESAssetLibraryConsumer consumer, ESAssetConsumerManifest manifest)
        {
            foreach (ESAssetReferBase refer in consumer.GameCoreAssets ?? Enumerable.Empty<ESAssetReferBase>())
            {
                if (refer == null || !refer.IsValid) continue;
                string path = AssetDatabase.GUIDToAssetPath(refer.GUID);
                ESResourcePlanInfo plan = refer.LocalFileId == 0
                    ? AssetDatabase.LoadAssetAtPath<ESResourcePlanInfo>(path)
                    : FindSubAsset(path, refer.GUID, refer.LocalFileId) as ESResourcePlanInfo;
                if (plan == null) continue;
                foreach (ESResourcePlanBakedExtensionEntry extension in plan.BakedExtensions ?? Array.Empty<ESResourcePlanBakedExtensionEntry>())
                foreach (ESResourcePlanBakedAssetEntry asset in extension?.assets ?? new List<ESResourcePlanBakedAssetEntry>())
                {
                    ESAssetPage page = null;
                    bool found = false;
                    if (asset != null)
                    {
                        found = asset.enumKey != 0
                            ? ESAssetRegistry.TryGetByEnum(asset.kind, asset.enumKey, out page)
                            : ESAssetRegistry.TryGetByString(asset.kind, asset.stringKey, out page);
                    }
                    if (!found || page == null) throw new InvalidOperationException("扩展资源未注册到 Catalog：" + plan.name);
                    string libraryFolder = ESAssetPipelineIO.SafeSegment(page.SourceBook);
                    if (!manifest.libraries.Any(item => string.Equals(item.libraryFolder, libraryFolder, StringComparison.Ordinal)))
                        throw new InvalidOperationException("Consumer 未声明扩展资源所属 Library：Consumer=" + consumer.Name + ", Plan=" + plan.name + ", Library=" + libraryFolder);
                }
            }
        }

        private static void AddResidentAssets(ESAssetConsumerManifest manifest, ESAssetLibraryConsumer consumer, Dictionary<string, PublishedLibrary> libraries)
        {
            var identities = new HashSet<ESAssetIdentity>();
            foreach (ESAssetReferBase refer in consumer.ResidentAssets ?? Enumerable.Empty<ESAssetReferBase>())
            {
                if (refer == null || !refer.IsValid)
                    throw new InvalidOperationException("Consumer 启动常驻资产包含无效引用：" + consumer.Name);

                var id = refer.AssetIdentity;
                if (!identities.Add(id))
                    continue;

                string path = AssetDatabase.GUIDToAssetPath(id.Guid);
                UnityEngine.Object asset = id.LocalFileId == 0
                    ? AssetDatabase.LoadMainAssetAtPath(path)
                    : FindSubAsset(path, id.Guid, id.LocalFileId);
                if (asset == null || ESAssetPipelineIO.IsEditorOnly(path, asset) || asset is SceneAsset)
                    throw new InvalidOperationException("Consumer 启动常驻资产无效、属于 EditorOnly 或为 Scene：" + path);
                if (ESAssetPipelineIO.IsExcludedFolderPath(path))
                    throw new InvalidOperationException("Consumer 启动常驻资产位于全局排除目录：" + path);
                if (asset is ScriptableObject scriptableObject
                    && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
                    throw new InvalidOperationException("GameCore 资产不能重复进入 ResidentAssets：" + path);

                var owners = libraries.Where(pair => ContainsIdentity(pair.Value.manifest, id)).ToList();
                if (owners.Count == 0)
                    throw new InvalidOperationException("启动常驻资产未出现在任何已构建 AssetLibrary 中：" + path);
                if (owners.Count != 1)
                    throw new InvalidOperationException("启动常驻资产在多个 Library 构建清单中重复：" + path);

                KeyValuePair<string, PublishedLibrary> owner = owners[0];
                ESAssetConsumerLibraryReference libraryReference = manifest.libraries.FirstOrDefault(item => string.Equals(item.libraryFolder, owner.Key, StringComparison.Ordinal));
                if (libraryReference == null)
                {
                    libraryReference = new ESAssetConsumerLibraryReference
                    {
                        libraryName = owner.Value.identity.libraryName,
                        libraryFolder = owner.Key,
                        deliveryMode = owner.Value.identity.deliveryMode,
                        libraryIdentityUrl = owner.Value.identityUrl,
                        libraryIdentitySha256 = owner.Value.identitySha256,
                        embeddedIdentityRelativePath = owner.Value.embeddedIdentityRelativePath,
                        requiredAtBoot = true
                    };
                    manifest.libraries.Add(libraryReference);
                }
                else
                {
                    libraryReference.requiredAtBoot = true;
                }

                manifest.residentAssets.Add(new ESAssetConsumerResidentAssetReference { guid = id.Guid, localFileId = id.LocalFileId });
            }

            manifest.residentAssets = manifest.residentAssets
                .OrderBy(item => item.guid, StringComparer.Ordinal)
                .ThenBy(item => item.localFileId)
                .ToList();
        }

        private static bool ContainsIdentity(ESAssetBundleManifest manifest, ESAssetIdentity id)
        {
            if (manifest == null) return false;
            if (id.IsSubAsset)
                return manifest.subAssetsById.Any(item => item != null
                    && string.Equals(item.guid, id.Guid, StringComparison.Ordinal)
                    && item.localFileId == id.LocalFileId);
            return manifest.mainAssetsByGuid.Any(item => item != null && string.Equals(item.guid, id.Guid, StringComparison.Ordinal));
        }

        private static UnityEngine.Object FindSubAsset(string path, string guid, long localFileId)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string candidateGuid, out long candidateLocalFileId)
                    && string.Equals(candidateGuid, guid, StringComparison.Ordinal)
                    && candidateLocalFileId == localFileId)
                    return asset;
            return null;
        }

        private static void ValidatePublishedBundleIndex(string cdnRoot, string streamingReleaseRoot, bool includeInitialPackage, string releaseVersion, ESAssetReleaseBundleIndex bundleIndex)
        {
            var bundlesByKey = (bundleIndex.assetBundles ?? new List<ESAssetReleaseBundleRecord>()).ToDictionary(item => item.assetBundleKey, StringComparer.Ordinal);
            if (bundlesByKey.Count != (bundleIndex.assetBundles ?? new List<ESAssetReleaseBundleRecord>()).Count) throw new InvalidDataException("发布 Bundle 索引包含重复 AssetBundleKey。");
            foreach (var bundle in bundlesByKey.Values)
            {
                if (string.IsNullOrWhiteSpace(bundle.libraryFolder) || !ESAssetDeliveryModeEditorUtility.IsValid(bundle.deliveryMode)
                    || string.IsNullOrWhiteSpace(bundle.localRelativePath) || string.IsNullOrWhiteSpace(bundle.sha256)
                    || bundle.sha256.Length != 64 || !bundle.sha256.All(Uri.IsHexDigit) || bundle.size <= 0)
                    throw new InvalidDataException("发布 Bundle 索引记录不完整：" + bundle.assetBundleKey);
                bool requiresEmbedded = bundle.deliveryMode != ESAssetDeliveryMode.Remote;
                bool requiresRemote = bundle.deliveryMode != ESAssetDeliveryMode.BuiltIn;
                if (requiresEmbedded != !string.IsNullOrWhiteSpace(bundle.embeddedRelativePath)
                    || requiresRemote != !string.IsNullOrWhiteSpace(bundle.fileUrl))
                    throw new InvalidDataException("发布 Bundle 来源与分发方式不匹配：" + bundle.assetBundleKey);
                string normalizedPath = bundle.localRelativePath.Replace('\\', '/');
                if (!normalizedPath.StartsWith(ESAssetPipelineIO.AssetBundlesFolderName + "/", StringComparison.Ordinal)
                    || !string.Equals(Path.GetFileName(normalizedPath), normalizedPath.Substring(ESAssetPipelineIO.AssetBundlesFolderName.Length + 1), StringComparison.Ordinal))
                    throw new InvalidDataException("发布 Bundle 索引路径无效：" + bundle.assetBundleKey);
                if (requiresRemote)
                {
                    string filePath = ESAssetPipelineIO.ResolveGeneratedRelativePath(
                        ESAssetPipelineIO.ReleaseLibraryFolder(cdnRoot, string.Empty, releaseVersion, bundle.libraryFolder),
                        bundle.localRelativePath);
                    if (!File.Exists(filePath) || new FileInfo(filePath).Length != bundle.size || !ESResManifestIntegrity.VerifyFileSha256(filePath, bundle.sha256))
                        throw new InvalidDataException("发布 Bundle 远端文件校验失败：" + bundle.assetBundleKey);
                }
                if (bundle.deliveryMode == ESAssetDeliveryMode.BuiltIn
                    || (includeInitialPackage && bundle.deliveryMode == ESAssetDeliveryMode.Updateable))
                {
                    string embeddedPath = ESAssetPipelineIO.ResolveGeneratedRelativePath(streamingReleaseRoot, bundle.embeddedRelativePath);
                    if (!File.Exists(embeddedPath) || new FileInfo(embeddedPath).Length != bundle.size || !ESResManifestIntegrity.VerifyFileSha256(embeddedPath, bundle.sha256))
                        throw new InvalidDataException("发布 Bundle 随包文件校验失败：" + bundle.assetBundleKey);
                }
                var dependencySet = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependency in bundle.dependencies ?? new List<string>())
                    if (string.IsNullOrWhiteSpace(dependency) || string.Equals(dependency, bundle.assetBundleKey, StringComparison.Ordinal)
                        || !dependencySet.Add(dependency) || !bundlesByKey.ContainsKey(dependency))
                        throw new InvalidDataException("发布 Bundle 索引依赖无效：" + bundle.assetBundleKey + " -> " + dependency);
            }

            foreach (ESAssetReleaseBundleRecord bundle in bundlesByKey.Values)
            foreach (string dependencyKey in bundle.dependencies ?? new List<string>())
            {
                ESAssetReleaseBundleRecord dependency = bundlesByKey[dependencyKey];
                if (bundle.deliveryMode != ESAssetDeliveryMode.Remote && dependency.deliveryMode == ESAssetDeliveryMode.Remote)
                    throw new InvalidDataException("随包或更新资源不能依赖纯远端资源：" + bundle.assetBundleKey + " -> " + dependencyKey);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key)
            {
                if (visited.Contains(key)) return;
                if (!visiting.Add(key)) throw new InvalidDataException("发布 Bundle 索引存在依赖循环：" + key);
                foreach (string dependency in bundlesByKey[key].dependencies ?? new List<string>()) Visit(dependency);
                visiting.Remove(key);
                visited.Add(key);
            }
            foreach (string key in bundlesByKey.Keys) Visit(key);
        }

        private static void PublishCodePackages(List<ESAssetConsumerCodePackageReference> destination, ESAssetLibraryConsumer consumer,
            string platform, string releaseVersion, string initialRoot, string localTestRoot, string cdnRoot)
        {
            var packageKeys = new HashSet<string>(StringComparer.Ordinal);
            var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<ESConsumerCodePackageConfig> configs = (consumer.CodePackages ?? new List<ESConsumerCodePackageConfig>())
                .Where(item => item != null && item.Enabled)
                .OrderBy(item => item.LoadOrder)
                .ThenBy(item => item.PackageKey, StringComparer.Ordinal);

            foreach (ESConsumerCodePackageConfig config in configs)
            {
                string packageKey = (config.PackageKey ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(packageKey))
                    throw new InvalidOperationException("Consumer“" + consumer.Name + "”存在未命名的附加文件。");
                if (!packageKeys.Add(packageKey))
                    throw new InvalidOperationException($"Consumer“{consumer.Name}”的附加文件名称重复：{packageKey}");

                string sourcePath = ResolveCodePackageSource(config.SourcePath);
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException($"Consumer“{consumer.Name}”的附加文件不存在：{packageKey}", sourcePath);

                string extension = Path.GetExtension(sourcePath);
                string fileName = ESAssetPipelineIO.SafeSegment(packageKey) + (string.IsNullOrEmpty(extension) ? ".bytes" : extension.ToLowerInvariant());
                if (!fileNames.Add(fileName))
                    throw new InvalidOperationException($"Consumer“{consumer.Name}”的附加文件名称冲突：{fileName}");

                string relativePath = platform + "/" + releaseVersion + "/Code/" + consumer.ConsumerId + "/" + fileName;
                string initialPath = string.IsNullOrEmpty(initialRoot) ? null
                    : Path.Combine(initialRoot, platform, releaseVersion, "Code", consumer.ConsumerId, fileName);
                string localPath = Path.Combine(localTestRoot, releaseVersion, "Code", consumer.ConsumerId, fileName);
                string cdnPath = Path.Combine(cdnRoot, releaseVersion, "Code", consumer.ConsumerId, fileName);
                if (!string.IsNullOrEmpty(initialRoot))
                    CopyFile(sourcePath, initialPath);
                CopyFile(sourcePath, localPath);
                CopyFile(sourcePath, cdnPath);

                destination.Add(new ESAssetConsumerCodePackageReference
                {
                    packageKey = packageKey,
                    kind = config.Kind.ToString(),
                    fileName = fileName,
                    url = CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativePath),
                    sha256 = ESResManifestIntegrity.ComputeFileSha256(cdnPath),
                    size = new FileInfo(cdnPath).Length,
                    requiredAtBoot = config.RequiredAtBoot,
                    loadOrder = config.LoadOrder,
                    notes = config.Notes ?? string.Empty
                });
            }
        }

        private static string ResolveCodePackageSource(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return string.Empty;
            string resolved = Path.IsPathRooted(sourcePath)
                ? Path.GetFullPath(sourcePath)
                : Path.GetFullPath(Path.Combine(ESAssetPipelineIO.ProjectRoot, sourcePath));

            string projectRoot = Path.GetFullPath(ESAssetPipelineIO.ProjectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(resolved, projectRoot, StringComparison.OrdinalIgnoreCase)
                && !resolved.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Consumer 代码包源文件必须位于当前工程根目录内：" + sourcePath);

            if (ContainsExistingReparsePoint(projectRoot, resolved))
                throw new UnauthorizedAccessException("Consumer 代码包源文件不能穿过 junction/symlink：" + sourcePath);
            return resolved;
        }

        private static bool ContainsExistingReparsePoint(string root, string candidate)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if ((Directory.Exists(rootFull) || File.Exists(rootFull))
                && (File.GetAttributes(rootFull) & FileAttributes.ReparsePoint) != 0)
                return true;
            string current = rootFull;
            string relative = candidate.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 发布目录先在同一输出根内完成暂存与完整性验证，再以目录移动提交。
        /// 新版本目录拒绝覆盖；只有 StreamingAssets 的既有受管 Embedded Library 可以进入可恢复替换。
        /// </summary>
        private static void CommitStagedLibraryDirectory(
            string stagingPath,
            string destinationPath,
            bool replaceManagedDestination,
            ESAssetBundleManifest expectedManifest,
            ESAssetLibraryIdentity expectedIdentity)
        {
            string staging = Path.GetFullPath(stagingPath ?? string.Empty);
            string destination = Path.GetFullPath(destinationPath ?? string.Empty);
            string outputRoot = GetPublishOutputRoot(destination);
            if (outputRoot == null || !Directory.Exists(staging))
                throw new InvalidOperationException("发布暂存目录不在受管输出根内或已经丢失：" + stagingPath);
            if (ContainsExistingReparsePoint(outputRoot, staging)
                || ContainsExistingReparsePoint(outputRoot, destination))
                throw new UnauthorizedAccessException("发布目录提交不能穿过 junction/symlink。");

            if (!Directory.Exists(destination))
            {
                Directory.Move(staging, destination);
                VerifyPublishedLibrary(destination, expectedManifest, expectedIdentity);
                return;
            }

            if (!replaceManagedDestination)
                throw new IOException("发布版本目录已存在，拒绝覆盖。请使用新的发布版本：" + destination);

            ManagedLibraryFingerprint oldFingerprint = CaptureManagedLibraryFingerprint(destination);
            string backup = destination + ".backup-" + Guid.NewGuid().ToString("N");
            Directory.Move(destination, backup);
            EnsureManagedLibraryFingerprint(backup, oldFingerprint, "旧 Embedded Library 备份后身份不一致");
            bool committed = false;
            try
            {
                Directory.Move(staging, destination);
                if (!Directory.Exists(destination) || ContainsExistingReparsePoint(outputRoot, destination))
                    throw new IOException("发布目录提交后不可用：" + destination);
                VerifyPublishedLibrary(destination, expectedManifest, expectedIdentity);
                committed = true;
            }
            catch (Exception commitException)
            {
                try
                {
                    if (Directory.Exists(destination))
                        throw new IOException("提交失败后目标目录已被保留，拒绝删除可能的外部修改：" + destination);
                    if (!Directory.Exists(backup))
                        throw new DirectoryNotFoundException("提交失败后旧 Embedded Library 备份丢失：" + backup);
                    EnsureManagedLibraryFingerprint(backup, oldFingerprint, "恢复前旧 Embedded Library 备份已被修改");
                    Directory.Move(backup, destination);
                    EnsureManagedLibraryFingerprint(destination, oldFingerprint, "恢复后的旧 Embedded Library 身份不一致");
                }
                catch (Exception restoreException)
                {
                    throw new AggregateException("发布目录提交失败且旧目录恢复失败。", commitException, restoreException);
                }

                throw;
            }
            finally
            {
                if (committed && Directory.Exists(backup))
                {
                    try
                    {
                        DeletePublishDirectory(backup);
                    }
                    catch (Exception cleanupException)
                    {
                        throw new IOException("新 Embedded Library 已提交，但旧目录备份清理失败；已保留现场：" + backup, cleanupException);
                    }
                }
            }
        }

        private static ManagedLibraryFingerprint CaptureManagedLibraryFingerprint(string destination)
        {
            string identityPath = Path.Combine(destination, ESAssetPipelineIO.LibraryIdentityFileName);
            string manifestPath = Path.Combine(destination, ESAssetPipelineIO.BundleManifestFileName);
            if (!File.Exists(identityPath) || !File.Exists(manifestPath))
                throw new UnauthorizedAccessException("拒绝替换缺少 ES 发布标识的既有目录：" + destination);
            ESAssetLibraryIdentity identity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(identityPath);
            ESAssetBundleManifest manifest = ESAssetPipelineIO.ReadJson<ESAssetBundleManifest>(manifestPath);
            if (identity == null || manifest == null)
                throw new InvalidDataException("拒绝替换无法解析的既有 ES 发布目录：" + destination);
            VerifyPublishedLibrary(destination, manifest, identity);
            return new ManagedLibraryFingerprint
            {
                identitySha256 = ESResManifestIntegrity.ComputeFileSha256(identityPath),
                manifestSha256 = ESResManifestIntegrity.ComputeFileSha256(manifestPath)
            };
        }

        private static void EnsureManagedLibraryFingerprint(string destination, ManagedLibraryFingerprint expected, string context)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            ManagedLibraryFingerprint actual = CaptureManagedLibraryFingerprint(destination);
            if (!string.Equals(actual.identitySha256, expected.identitySha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actual.manifestSha256, expected.manifestSha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException(context + "：" + destination);
        }

        private static void VerifyPublishedLibrary(string folder, ESAssetBundleManifest manifest, ESAssetLibraryIdentity expectedIdentity)
        {
            if (manifest == null || expectedIdentity == null)
                throw new ArgumentNullException(manifest == null ? nameof(manifest) : nameof(expectedIdentity));

            string outputRoot = GetPublishOutputRoot(folder);
            if (outputRoot == null || ContainsExistingReparsePoint(outputRoot, folder))
                throw new UnauthorizedAccessException("发布 Library 校验目录不在受管输出根内或穿过 junction/symlink：" + folder);
            ESManagedFileIO.EnsureNoNestedReparsePoints(folder);

            string catalogPath = Path.Combine(folder, ESAssetPipelineIO.CatalogFileName);
            string manifestPath = Path.Combine(folder, ESAssetPipelineIO.BundleManifestFileName);
            string identityPath = Path.Combine(folder, ESAssetPipelineIO.LibraryIdentityFileName);
            if (string.IsNullOrWhiteSpace(expectedIdentity.catalogSha256)
                || !ESResManifestIntegrity.VerifyFileSha256(catalogPath, expectedIdentity.catalogSha256))
                throw new InvalidDataException("发布 Library Catalog Hash 校验失败：" + catalogPath);
            if (string.IsNullOrWhiteSpace(expectedIdentity.assetBundleManifestSha256)
                || !ESResManifestIntegrity.VerifyFileSha256(manifestPath, expectedIdentity.assetBundleManifestSha256))
                throw new InvalidDataException("发布 Library Manifest Hash 校验失败：" + manifestPath);

            ESAssetLibraryIdentity actualIdentity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(identityPath);
            if (actualIdentity == null
                || !string.Equals(actualIdentity.libraryName, expectedIdentity.libraryName, StringComparison.Ordinal)
                || !string.Equals(actualIdentity.libraryFolder, expectedIdentity.libraryFolder, StringComparison.Ordinal)
                || !string.Equals(actualIdentity.catalogSha256, expectedIdentity.catalogSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actualIdentity.assetBundleManifestSha256, expectedIdentity.assetBundleManifestSha256, StringComparison.OrdinalIgnoreCase)
                || actualIdentity.deliveryMode != expectedIdentity.deliveryMode
                || !string.Equals(actualIdentity.version, expectedIdentity.version, StringComparison.Ordinal)
                || !string.Equals(actualIdentity.channel, expectedIdentity.channel, StringComparison.Ordinal)
                || !string.Equals(actualIdentity.catalogUrl, expectedIdentity.catalogUrl, StringComparison.Ordinal)
                || !string.Equals(actualIdentity.assetBundleManifestUrl, expectedIdentity.assetBundleManifestUrl, StringComparison.Ordinal))
                throw new InvalidDataException("发布 Library Identity 与暂存验证结果不一致：" + identityPath);

            foreach (ESAssetBundleRecord bundle in manifest.assetBundles ?? Enumerable.Empty<ESAssetBundleRecord>())
            {
                string bundlePath = ESAssetPipelineIO.ResolveGeneratedRelativePath(folder, bundle.localRelativePath);
                if (!File.Exists(bundlePath)
                    || new FileInfo(bundlePath).Length != bundle.size
                    || !ESResManifestIntegrity.VerifyFileSha256(bundlePath, bundle.sha256))
                    throw new InvalidDataException("发布 Bundle 完整性校验失败：" + bundle.assetBundleKey);
            }
        }

        private static void CopyFile(string sourcePath, string destinationPath)
        {
            string source = Path.GetFullPath(sourcePath ?? string.Empty);
            string destination = Path.GetFullPath(destinationPath ?? string.Empty);
            string sourceRoot = Path.GetFullPath(ESAssetPipelineIO.ProjectRoot);
            if (!File.Exists(source) || ContainsExistingReparsePoint(sourceRoot, source))
                throw new UnauthorizedAccessException("发布源文件不存在或位于 junction/symlink：" + sourcePath);
            EnsurePublishDirectory(Path.GetDirectoryName(destination));
            string destinationRoot = GetPublishOutputRoot(destination);
            if (destinationRoot == null || ContainsExistingReparsePoint(destinationRoot, destination)
                || (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0))
                throw new UnauthorizedAccessException("发布目标文件不在受管输出目录或位于 junction/symlink：" + destinationPath);
            if (File.Exists(destination))
                throw new IOException("发布产物已存在，拒绝覆盖。请使用新的发布版本：" + destination);

            long expectedSize = new FileInfo(source).Length;
            string expectedSha256 = ESResManifestIntegrity.ComputeFileSha256(source);
            string temporaryPath = Path.Combine(Path.GetDirectoryName(destination), "." + Path.GetFileName(destination) + ".stage-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.Copy(source, temporaryPath, false);
                if (new FileInfo(temporaryPath).Length != expectedSize
                    || !ESResManifestIntegrity.VerifyFileSha256(temporaryPath, expectedSha256))
                    throw new InvalidDataException("发布暂存文件与源文件 Hash 不一致：" + sourcePath);
                if (new FileInfo(source).Length != expectedSize
                    || !ESResManifestIntegrity.VerifyFileSha256(source, expectedSha256))
                    throw new IOException("发布源文件在复制期间发生变化，拒绝提交：" + sourcePath);
                if (File.Exists(destination) || ContainsExistingReparsePoint(destinationRoot, destination))
                    throw new IOException("发布目标在提交前已存在或路径发生变化，拒绝覆盖：" + destinationPath);

                File.Move(temporaryPath, destination);
                if (!File.Exists(destination)
                    || new FileInfo(destination).Length != expectedSize
                    || !ESResManifestIntegrity.VerifyFileSha256(destination, expectedSha256))
                    throw new InvalidDataException("发布提交后文件完整性校验失败：" + destinationPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static void DeletePublishDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            string candidate = Path.GetFullPath(path);
            string outputRoot = GetPublishOutputRoot(candidate);
            if (outputRoot == null || string.Equals(candidate, outputRoot, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("发布目录清理目标越出受管输出根：" + path);
            ESManagedFileIO.DeleteDirectory(candidate, outputRoot);
        }

        private static void EnsurePublishDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("发布输出目录不能为空。");
            string candidate = Path.GetFullPath(path);
            string root = GetPublishOutputRoot(candidate);
            if (root == null || string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
                || ContainsExistingReparsePoint(root, candidate))
                throw new UnauthorizedAccessException("发布输出目录不在受管范围或位于 junction/symlink：" + path);
            Directory.CreateDirectory(candidate);
        }

        private static string GetPublishOutputRoot(string path)
        {
            string candidate = Path.GetFullPath(path);
            string[] roots =
            {
                Path.GetFullPath(ESAssetPipelineIO.PipelineRoot),
                Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, ESGlobalResSetting.ResParentFolderName)),
                ToAbsolutePath(ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_),
                ToAbsolutePath(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath)
            };
            return roots.Where(root => !string.IsNullOrWhiteSpace(root))
                .Select(Path.GetFullPath)
                .Where(root => string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(root => root.Length)
                .FirstOrDefault();
        }

        /// <summary>
        /// HotUpdate 发布不应把资源、Consumer 或代码包写入 StreamingAssets。
        /// 这里只清理由本管线管理的目标平台目录，不触碰 StreamingAssets 下的其它业务文件。
        /// </summary>
        internal static void RemoveGeneratedStreamingAssets(string platform)
        {
            string resRoot = Path.GetFullPath(Path.Combine(ESAssetPipelineIO.ProjectRoot, "Assets", "StreamingAssets", ESGlobalResSetting.ResParentFolderName));
            string platformRoot = Path.GetFullPath(Path.Combine(resRoot, platform));
            string validatedRoot = resRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!platformRoot.StartsWith(validatedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("StreamingAssets 清理目标越界：" + platformRoot);
            if (ContainsExistingReparsePoint(resRoot, platformRoot))
                throw new UnauthorizedAccessException("StreamingAssets 清理目标不能穿过 junction/symlink：" + platformRoot);
            if (!Directory.Exists(platformRoot)) return;

            int removed = 0;
            string embeddedFolderName = "Embedded";
            foreach (string directory in Directory.EnumerateDirectories(platformRoot).ToArray())
            {
                if (string.Equals(Path.GetFileName(directory), embeddedFolderName, StringComparison.OrdinalIgnoreCase))
                    continue;
                string target = Path.GetFullPath(directory);
                string validatedPlatformRoot = platformRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!target.StartsWith(validatedPlatformRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("StreamingAssets 清理目标越界：" + target);
                if (ContainsExistingReparsePoint(platformRoot, target))
                    throw new UnauthorizedAccessException("StreamingAssets 清理目标不能穿过 junction/symlink：" + target);
                ESAssetPipelineIO.DeleteGeneratedDirectory(target);
                string metaPath = target + ".meta";
                if (File.Exists(metaPath)) ESAssetPipelineIO.DeleteGeneratedFile(metaPath);
                removed++;
            }
            foreach (string file in Directory.EnumerateFiles(platformRoot).ToArray())
            {
                string fileName = Path.GetFileName(file);
                if (string.Equals(fileName, embeddedFolderName + ".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ContainsExistingReparsePoint(platformRoot, file))
                    throw new UnauthorizedAccessException("StreamingAssets 清理文件不能穿过 junction/symlink：" + file);
                ESAssetPipelineIO.DeleteGeneratedFile(file);
                removed++;
            }
            if (removed == 0) return;
            AssetDatabase.Refresh();
            Debug.Log($"[ESRes][Cleanup] HotUpdate 模式：已清理 {removed} 项版本发布文件，并保留 Embedded 随包资源：{platformRoot}");
        }

        private static void PruneGeneratedReleaseVersions(string generatedRoot, string currentReleaseVersion)
        {
            if (string.IsNullOrWhiteSpace(generatedRoot) || !Directory.Exists(generatedRoot)) return;
            string root = Path.GetFullPath(generatedRoot);
            if (ContainsExistingReparsePoint(root, root))
                throw new UnauthorizedAccessException("本地发布版本根目录不能穿过 junction/symlink：" + generatedRoot);
            int removed = 0;
            foreach (string folder in Directory.EnumerateDirectories(root).ToArray())
            {
                string name = Path.GetFileName(folder);
                if (string.Equals(name, currentReleaseVersion, StringComparison.Ordinal)) continue;
                // 只删除本发布器生成的版本目录，避免误删根目录中的其它工具数据。
                bool generatedRelease = Directory.Exists(Path.Combine(folder, ESAssetPipelineIO.LibrariesFolderName))
                    || Directory.Exists(Path.Combine(folder, "Consumers"))
                    || Directory.Exists(Path.Combine(folder, "Code"))
                    || File.Exists(Path.Combine(folder, ESAssetPipelineIO.ReleaseBundleIndexFileName));
                if (!generatedRelease) continue;
                string target = Path.GetFullPath(folder);
                string validatedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!target.StartsWith(validatedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("本地发布版本清理目标越界：" + target);
                if (ContainsExistingReparsePoint(root, target))
                    throw new UnauthorizedAccessException("本地发布版本清理目标不能穿过 junction/symlink：" + target);
                ESAssetPipelineIO.DeleteGeneratedDirectory(target);
                removed++;
            }
            if (removed > 0)
                Debug.Log($"[ESRes][Cleanup] {root} 已删除 {removed} 个旧本机发布版本，仅保留 {currentReleaseVersion}。");
        }

        private static void PruneManualUploadPlans(string planFolder, int keepCount)
        {
            if (string.IsNullOrWhiteSpace(planFolder) || !Directory.Exists(planFolder)) return;
            if (ContainsExistingReparsePoint(ESAssetPipelineIO.PipelineRoot, planFolder))
                throw new UnauthorizedAccessException("手工上传计划目录不能穿过 junction/symlink：" + planFolder);
            string[] obsolete = Directory.EnumerateFiles(planFolder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .Skip(Math.Max(1, keepCount))
                .ToArray();
            foreach (string path in obsolete)
            {
                if (ContainsExistingReparsePoint(planFolder, path))
                    throw new UnauthorizedAccessException("手工上传计划清理目标不能穿过 junction/symlink：" + path);
                ESAssetPipelineIO.DeleteGeneratedFile(path);
            }
            if (obsolete.Length > 0)
                Debug.Log($"[ESRes][Cleanup] 手工上传计划已删除 {obsolete.Length} 份旧记录，保留最新 {Math.Max(1, keepCount)} 份。");
        }

        private sealed class PublishedLibrary
        {
            public ESAssetLibraryIdentity identity;
            public ESAssetBundleManifest manifest;
            public string identityUrl;
            public string embeddedIdentityRelativePath;
            public string identitySha256;
        }

        private sealed class ManagedLibraryFingerprint
        {
            public string identitySha256;
            public string manifestSha256;
        }

        private static string ToAbsolutePath(string path) => Path.IsPathRooted(path) ? path : Path.Combine(ESAssetPipelineIO.ProjectRoot, path);
        private static string CombineUrl(string root, string relative) => (root ?? string.Empty).TrimEnd('/', '\\') + "/" + relative.Replace('\\', '/');
    }
}
