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
            ESAssetPipelineIO.EnsureAssetBundleReleaseMode();
            string platform = ESAssetPipelineIO.PlatformName;
            string stagingRoot = ESAssetPipelineIO.StagingRoot(platform);
            if (!Directory.Exists(stagingRoot)) throw new DirectoryNotFoundException("资源包暂存目录不存在，请先执行“构建资源包”：" + stagingRoot);
            var buildSet = ESAssetPipelineIO.ReadJson<ESAssetBuildSet>(Path.Combine(stagingRoot, ESAssetPipelineIO.BuildSetFileName));
            var stageFolders = buildSet.libraryFolders.Select(folder => ESAssetPipelineIO.StagingLibraryFolder(platform, folder)).ToList();
            if (stageFolders.Count == 0) throw new InvalidOperationException("资源包暂存目录中没有可发布的资源库。");
            ValidateAll(stageFolders);
            var consumers = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibraryConsumer>()
                .Where(item => item != null)
                .OrderBy(item => AssetDatabase.GetAssetPath(item), StringComparer.Ordinal)
                .ToList();
            if (consumers.Count == 0) throw new InvalidOperationException("未找到资源使用者（Consumer）。请先创建至少一个 Consumer。\n");
            ESCodeModuleEditorIntegration.GenerateAndSyncAll(consumers);
            ESAssetConsumerBuildRevision.IncrementAllForBuild();

            DateTime publishLocalTime = DateTime.Now;
            string releaseVersion = ESGlobalResSetting.Instance.Version + "." + publishLocalTime.ToString("yyyyMMddHHmmssfff");
            string initialRoot = ToAbsolutePath(ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_);
            string localTestRoot = Path.Combine(ESAssetPipelineIO.ProjectRoot, "ES", "Published", "LocalTest", platform);
            string cdnRoot = Path.Combine(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath, platform);
            var release = new ESAssetReleaseManifest { platform = platform, releaseVersion = releaseVersion, channel = "default", publishedUtc = DateTime.UtcNow.ToString("O") };
            var publishedLibraries = new Dictionary<string, PublishedLibrary>(StringComparer.Ordinal);
            var bundleIndex = new ESAssetReleaseBundleIndex { platform = platform, releaseVersion = releaseVersion };

            foreach (string stageFolder in stageFolders)
            {
                var identity = ESAssetPipelineIO.ReadJson<ESAssetLibraryIdentity>(Path.Combine(stageFolder, ESAssetPipelineIO.LibraryIdentityFileName));
                identity.version = releaseVersion;
                identity.channel = release.channel;
                string libraryFolder = Path.GetFileName(stageFolder);
                // Staging 的实际规范化目录名是发布与运行时共同使用的 LibraryFolder 权威值。
                // 同时兼容由旧 Planner 生成、Identity 中仍带前导下划线的 GameCore 暂存产物。
                identity.libraryFolder = libraryFolder;
                var manifest = ESAssetPipelineIO.ReadJson<ESAssetBundleManifest>(Path.Combine(stageFolder, ESAssetPipelineIO.BundleManifestFileName));
                string relativeBase = ESAssetPipelineIO.ReleaseLibraryRelativeBase(platform, releaseVersion, libraryFolder);
                identity.catalogUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativeBase + ESAssetPipelineIO.CatalogFileName);
                identity.assetBundleManifestUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativeBase + ESAssetPipelineIO.BundleManifestFileName);
                CopyStage(stageFolder, ESAssetPipelineIO.ReleaseLibraryFolder(initialRoot, platform, releaseVersion, libraryFolder), manifest, identity);
                CopyStage(stageFolder, ESAssetPipelineIO.ReleaseLibraryFolder(localTestRoot, string.Empty, releaseVersion, libraryFolder), manifest, identity);
                CopyStage(stageFolder, ESAssetPipelineIO.ReleaseLibraryFolder(cdnRoot, string.Empty, releaseVersion, libraryFolder), manifest, identity);
                release.libraries.Add(new ESAssetReleaseLibrary
                {
                    libraryName = identity.libraryName,
                    version = releaseVersion,
                    catalogUrl = identity.catalogUrl,
                    catalogSha256 = identity.catalogSha256,
                    assetBundleManifestUrl = identity.assetBundleManifestUrl,
                    assetBundleManifestSha256 = identity.assetBundleManifestSha256
                });
                string identityRelativePath = relativeBase + ESAssetPipelineIO.LibraryIdentityFileName;
                string publishedIdentityPath = Path.Combine(ESAssetPipelineIO.ReleaseLibraryFolder(cdnRoot, string.Empty, releaseVersion, libraryFolder), ESAssetPipelineIO.LibraryIdentityFileName);
                publishedLibraries.Add(libraryFolder, new PublishedLibrary
                {
                    identity = identity,
                    manifest = manifest,
                    identityUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, identityRelativePath),
                    identitySha256 = ESResManifestIntegrity.ComputeFileSha256(publishedIdentityPath)
                });
                foreach (var assetBundle in manifest.assetBundles)
                    bundleIndex.assetBundles.Add(new ESAssetReleaseBundleRecord
                    {
                        libraryFolder = libraryFolder,
                        assetBundleKey = assetBundle.assetBundleKey,
                        fileUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, relativeBase + assetBundle.localRelativePath),
                        localRelativePath = assetBundle.localRelativePath,
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
            string initialBundleIndexPath = Path.Combine(initialRoot, platform, releaseVersion, ESAssetPipelineIO.ReleaseBundleIndexFileName);
            string localBundleIndexPath = Path.Combine(localTestRoot, releaseVersion, ESAssetPipelineIO.ReleaseBundleIndexFileName);
            string cdnBundleIndexPath = Path.Combine(cdnRoot, releaseVersion, ESAssetPipelineIO.ReleaseBundleIndexFileName);
            ESAssetPipelineIO.WriteJson(initialBundleIndexPath, bundleIndex, true);
            ESAssetPipelineIO.WriteJson(localBundleIndexPath, bundleIndex, true);
            ESAssetPipelineIO.WriteJson(cdnBundleIndexPath, bundleIndex, true);
            release.bundleIndexUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, bundleIndexRelativePath);
            release.bundleIndexSha256 = ESResManifestIntegrity.ComputeFileSha256(cdnBundleIndexPath);
            ValidatePublishedBundleIndex(cdnRoot, releaseVersion, bundleIndex);

            var totalConsumers = consumers.Where(item => item.IsTotalConsumer).ToList();
            if (totalConsumers.Count == 0)
            {
                ESAssetLibraryConsumer selected = consumers[0];
                Undo.RecordObject(selected, "自动指定总资源使用者");
                selected.IsTotalConsumer = true;
                selected.EnsureStableIdentity();
                EditorUtility.SetDirty(selected);
                AssetDatabase.SaveAssets();
                totalConsumers.Add(selected);
                Debug.LogWarning("[ES资源发布] 未指定总资源使用者，已自动使用第一个资源使用者：" + selected.Name);
            }
            if (totalConsumers.Count != 1) throw new InvalidOperationException("资源发布只能有一个总资源使用者。请取消多余 Consumer 的“总 Consumer（启动入口）”勾选。");
            var consumerPublications = new Dictionary<string, ESAssetConsumerReference>(StringComparer.Ordinal);
            var publishStack = new HashSet<string>(StringComparer.Ordinal);
            ESAssetConsumerReference totalConsumer = PublishConsumer(totalConsumers[0], consumers, publishedLibraries, consumerPublications, publishStack, platform, releaseVersion, initialRoot, localTestRoot, cdnRoot);
            release.totalConsumerUrl = totalConsumer.consumerUrl;
            release.totalConsumerSha256 = totalConsumer.consumerSha256;

            ESAssetPipelineIO.WriteJson(Path.Combine(initialRoot, platform, ESAssetPipelineIO.ReleaseManifestFileName), release, true);
            ESAssetPipelineIO.WriteJson(Path.Combine(localTestRoot, ESAssetPipelineIO.ReleaseManifestFileName), release, true);
            ESAssetPipelineIO.WriteJson(Path.Combine(cdnRoot, ESAssetPipelineIO.ReleaseManifestFileName), release, true);
            string uploadPlanPath = WriteManualUploadPlan(cdnRoot, platform, releaseVersion);
            AssetDatabase.Refresh();
            Debug.Log($"[ESAssetBundlePublisher] 发布完成：{releaseVersion}，资源库数量 {release.libraries.Count}。根清单已最后原子写入。\n手动 OSS 上传计划：{uploadPlanPath}");
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
                    string file = Path.Combine(folder, assetBundle.localRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(file) || new FileInfo(file).Length != assetBundle.size || !ESResManifestIntegrity.VerifyFileSha256(file, assetBundle.sha256)) throw new InvalidDataException("AB 文件完整性失败：" + file);
                    if (packages.ContainsKey(assetBundle.assetBundleKey)) throw new InvalidDataException("重复 AssetBundleKey：" + assetBundle.assetBundleKey);
                    packages.Add(assetBundle.assetBundleKey, assetBundle);
                }
            }
            foreach (var package in packages.Values) foreach (string dependency in package.dependencies)
                if (!packages.ContainsKey(dependency)) throw new InvalidDataException($"AB 依赖缺失：{package.assetBundleKey} -> {dependency}");
        }

        private static string WriteManualUploadPlan(string cdnRoot, string platform, string releaseVersion)
        {
            string releaseFolder = Path.Combine(cdnRoot, releaseVersion);
            string rootManifestPath = Path.Combine(cdnRoot, ESAssetPipelineIO.ReleaseManifestFileName);
            if (!Directory.Exists(releaseFolder) || !File.Exists(rootManifestPath))
                throw new InvalidOperationException("无法生成手动上传计划：发布目录不完整。");

            var plan = new ESAssetReleaseUploadPlan
            {
                platform = platform,
                releaseVersion = releaseVersion,
                sourceRoot = cdnRoot,
                publicBaseUrl = CombineUrl(ESGlobalResSetting.Instance.Path_Net, platform + "/"),
                generatedUtc = DateTime.UtcNow.ToString("O")
            };
            int order = 0;
            foreach (string sourcePath in Directory.GetFiles(releaseFolder, "*", SearchOption.AllDirectories).OrderBy(item => item, StringComparer.Ordinal))
            {
                string relativePath = releaseVersion + "/" + sourcePath.Substring(releaseFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                plan.files.Add(CreateUploadPlanFile(sourcePath, relativePath, ++order, false));
            }
            plan.files.Add(CreateUploadPlanFile(rootManifestPath, ESAssetPipelineIO.ReleaseManifestFileName, ++order, true));

            string planFolder = Path.Combine(ESAssetPipelineIO.ProjectRoot, "ES", "Published", "ManualUploadPlans", platform);
            string planPath = Path.Combine(planFolder, releaseVersion + ".json");
            ESAssetPipelineIO.WriteJson(planPath, plan, true);
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
                uploadLast = uploadLast
            };
        }

        private static void CopyStage(string sourceFolder, string destinationFolder, ESAssetBundleManifest manifest, ESAssetLibraryIdentity identity)
        {
            RecreateGeneratedDirectory(destinationFolder);
            var relativePaths = manifest.assetBundles.Select(item => item.localRelativePath)
                .Concat(new[] { ESAssetPipelineIO.BundleManifestFileName, ESAssetPipelineIO.CatalogFileName })
                .Distinct(StringComparer.Ordinal);
            foreach (string relativePath in relativePaths)
            {
                string sourcePath = Path.Combine(sourceFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
                string destinationPath = Path.Combine(destinationFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                File.Copy(sourcePath, destinationPath, true);
            }
            ESAssetPipelineIO.WriteJson(Path.Combine(destinationFolder, ESAssetPipelineIO.LibraryIdentityFileName), identity);
        }

        private static void RecreateGeneratedDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
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
                AddResidentAssets(manifest, consumer, libraries);
                PublishCodePackages(manifest.codePackages, consumer, platform, releaseVersion, initialRoot, localTestRoot, cdnRoot);

                string relativePath = platform + "/" + releaseVersion + "/Consumers/" + consumer.ConsumerId + ".json";
                string initialPath = Path.Combine(initialRoot, platform, releaseVersion, "Consumers", consumer.ConsumerId + ".json");
                string localPath = Path.Combine(localTestRoot, releaseVersion, "Consumers", consumer.ConsumerId + ".json");
                string cdnPath = Path.Combine(cdnRoot, releaseVersion, "Consumers", consumer.ConsumerId + ".json");
                ESAssetPipelineIO.WriteJson(initialPath, manifest, true);
                ESAssetPipelineIO.WriteJson(localPath, manifest, true);
                ESAssetPipelineIO.WriteJson(cdnPath, manifest, true);
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
                destination.Add(new ESAssetConsumerLibraryReference { libraryName = published.identity.libraryName, libraryFolder = folder, libraryIdentityUrl = published.identityUrl, libraryIdentitySha256 = published.identitySha256, requiredAtBoot = requiredAtBoot });
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
                libraryIdentityUrl = published.identityUrl,
                libraryIdentitySha256 = published.identitySha256,
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
                    var dependency = AssetDatabase.LoadMainAssetAtPath(dependencyPath) as ScriptableObject;
                    if (dependency == null || ESScriptableObjectClassification.GetClass(dependency) != ESScriptableObjectClass.GameCore)
                        continue;
                    ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(dependency);
                    var runtimeIdentity = new ESAssetIdentity(identity.guid, identity.localFileId);
                    if (!byIdentity.ContainsKey(runtimeIdentity))
                        throw new InvalidOperationException("GameCore 依赖未归属当前 Consumer：" + rootPath + " -> " + dependencyPath);
                    if (!runtimeIdentity.Equals(new ESAssetIdentity(root.guid, root.localFileId)))
                        root.dependencies.Add(new ESAssetConsumerGameCoreDependencyReference { guid = identity.guid, localFileId = identity.localFileId });
                }
                root.dependencies = root.dependencies.OrderBy(item => item.guid, StringComparer.Ordinal).ThenBy(item => item.localFileId).ToList();
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
                        libraryIdentityUrl = owner.Value.identityUrl,
                        libraryIdentitySha256 = owner.Value.identitySha256,
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

        private static void ValidatePublishedBundleIndex(string cdnRoot, string releaseVersion, ESAssetReleaseBundleIndex bundleIndex)
        {
            var bundlesByKey = (bundleIndex.assetBundles ?? new List<ESAssetReleaseBundleRecord>()).ToDictionary(item => item.assetBundleKey, StringComparer.Ordinal);
            if (bundlesByKey.Count != (bundleIndex.assetBundles ?? new List<ESAssetReleaseBundleRecord>()).Count) throw new InvalidDataException("发布 Bundle 索引包含重复 AssetBundleKey。");
            foreach (var bundle in bundlesByKey.Values)
            {
                if (string.IsNullOrWhiteSpace(bundle.libraryFolder) || string.IsNullOrWhiteSpace(bundle.fileUrl)
                    || string.IsNullOrWhiteSpace(bundle.localRelativePath) || string.IsNullOrWhiteSpace(bundle.sha256)
                    || bundle.sha256.Length != 64 || !bundle.sha256.All(Uri.IsHexDigit) || bundle.size <= 0)
                    throw new InvalidDataException("发布 Bundle 索引记录不完整：" + bundle.assetBundleKey);
                string normalizedPath = bundle.localRelativePath.Replace('\\', '/');
                if (!normalizedPath.StartsWith(ESAssetPipelineIO.AssetBundlesFolderName + "/", StringComparison.Ordinal)
                    || !string.Equals(Path.GetFileName(normalizedPath), normalizedPath.Substring(ESAssetPipelineIO.AssetBundlesFolderName.Length + 1), StringComparison.Ordinal))
                    throw new InvalidDataException("发布 Bundle 索引路径无效：" + bundle.assetBundleKey);
                string filePath = Path.Combine(ESAssetPipelineIO.ReleaseLibraryFolder(cdnRoot, string.Empty, releaseVersion, bundle.libraryFolder), bundle.localRelativePath);
                if (!File.Exists(filePath) || new FileInfo(filePath).Length != bundle.size || !ESResManifestIntegrity.VerifyFileSha256(filePath, bundle.sha256)) throw new InvalidDataException("发布 Bundle 文件校验失败：" + bundle.assetBundleKey);
                var dependencySet = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependency in bundle.dependencies ?? new List<string>())
                    if (string.IsNullOrWhiteSpace(dependency) || string.Equals(dependency, bundle.assetBundleKey, StringComparison.Ordinal)
                        || !dependencySet.Add(dependency) || !bundlesByKey.ContainsKey(dependency))
                        throw new InvalidDataException("发布 Bundle 索引依赖无效：" + bundle.assetBundleKey + " -> " + dependency);
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
                string initialPath = Path.Combine(initialRoot, platform, releaseVersion, "Code", consumer.ConsumerId, fileName);
                string localPath = Path.Combine(localTestRoot, releaseVersion, "Code", consumer.ConsumerId, fileName);
                string cdnPath = Path.Combine(cdnRoot, releaseVersion, "Code", consumer.ConsumerId, fileName);
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
            return Path.IsPathRooted(sourcePath)
                ? Path.GetFullPath(sourcePath)
                : Path.GetFullPath(Path.Combine(ESAssetPipelineIO.ProjectRoot, sourcePath));
        }

        private static void CopyFile(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, true);
        }

        private sealed class PublishedLibrary
        {
            public ESAssetLibraryIdentity identity;
            public ESAssetBundleManifest manifest;
            public string identityUrl;
            public string identitySha256;
        }

        private static string ToAbsolutePath(string path) => Path.IsPathRooted(path) ? path : Path.Combine(ESAssetPipelineIO.ProjectRoot, path);
        private static string CombineUrl(string root, string relative) => (root ?? string.Empty).TrimEnd('/', '\\') + "/" + relative.Replace('\\', '/');
    }
}
