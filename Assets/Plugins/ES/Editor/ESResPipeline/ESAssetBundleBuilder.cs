using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESAssetBundleBuilder
    {
        public static void Build()
        {
            ESAssetPipelineIO.EnsureAssetBundleReleaseMode();
            string platform = ESAssetPipelineIO.PlatformName;
            string planFolder = ESAssetPipelineIO.PlanRoot(platform);
            var plan = ESAssetPipelineIO.ReadJson<ESAssetBundleBuildPlan>(Path.Combine(planFolder, ESAssetPipelineIO.PlanFileName));
            var assetList = ESAssetPipelineIO.ReadJson<ESAssetBundleAssetList>(Path.Combine(planFolder, ESAssetPipelineIO.AssetListFileName));
            if (plan.errors.Count > 0) throw new InvalidOperationException("[ESRes][Build] BuildPlan 包含错误，拒绝构建。");
            ValidatePlanAndAssetList(plan, assetList);
            ValidateLabels(plan);

            string buildId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            string stagingRoot = ESAssetPipelineIO.StagingRoot(platform);
            CleanStagingRoot(stagingRoot);
            // 固定保留 Unity 原始 Manifest 与 AB 输出，供下一次构建复用。
            // 它不属于 Staging，因而不会上传、下载或混入发布目录。
            string buildRoot = ESAssetPipelineIO.BuildCacheRoot(platform);
            Directory.CreateDirectory(buildRoot);
            var builds = plan.assignments.GroupBy(item => item.assetBundleKey, StringComparer.Ordinal).Select(group => new AssetBundleBuild
            {
                assetBundleName = group.Key,
                assetNames = group.Select(item => item.assetPath).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray()
            }).ToArray();
            BuildTarget target = ESAssetBundleBuildTargetUtility.GetBuildTarget(ESGlobalResSetting.Instance.applyPlatform);
            AssetBundleManifest unityManifest = BuildPipeline.BuildAssetBundles(buildRoot, builds, BuildAssetBundleOptions.ChunkBasedCompression, target);
            if (unityManifest == null) throw new InvalidOperationException("[ESRes][Build] BuildPipeline.BuildAssetBundles 返回 null。");

            var ownerByBundle = plan.assignments.GroupBy(item => item.assetBundleKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(item => item.ownerLibrary).OrderBy(item => item, StringComparer.Ordinal).First(), StringComparer.Ordinal);
            var owners = ownerByBundle.Values.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            foreach (string owner in owners) BuildLibraryStage(platform, owner, ownerByBundle.Where(item => string.Equals(item.Value, owner, StringComparison.Ordinal)).Select(item => item.Key).ToList(), buildRoot, unityManifest, assetList);
            RemoveStaleLibraryStages(platform, owners);
            ESAssetPipelineIO.WriteJson(Path.Combine(stagingRoot, ESAssetPipelineIO.BuildSetFileName), new ESAssetBuildSet
            {
                platform = platform,
                buildId = buildId,
                builtUtc = DateTime.UtcNow.ToString("O"),
                libraryFolders = owners.Select(ESAssetPipelineIO.SafeSegment).ToList()
            }, true);
            Debug.Log($"[ESAssetBundleBuilder] 构建完成，共 {builds.Length} 个 AB。输出：{stagingRoot}");
        }

        private static void ValidatePlanAndAssetList(ESAssetBundleBuildPlan plan, ESAssetBundleAssetList assetList)
        {
            if (plan == null || assetList == null)
                throw new InvalidOperationException("[ESRes][Build] BuildPlan/AssetList cannot be null.");
            if (!string.Equals(plan.platform, assetList.platform, StringComparison.Ordinal))
                throw new InvalidOperationException($"[ESRes][Build] BuildPlan and AssetList platform mismatch: Plan={plan.platform}, AssetList={assetList.platform}");

            var assignmentByPath = new Dictionary<string, ESAssetBundleAssignment>(StringComparer.Ordinal);
            foreach (ESAssetBundleAssignment assignment in plan.assignments ?? new List<ESAssetBundleAssignment>())
            {
                if (assignment == null || string.IsNullOrWhiteSpace(assignment.assetPath) || string.IsNullOrWhiteSpace(assignment.assetBundleKey))
                    throw new InvalidOperationException("[ESRes][Build] BuildPlan contains an invalid assignment.");
                if (!assignmentByPath.TryAdd(assignment.assetPath, assignment))
                    throw new InvalidOperationException("[ESRes][Build] Duplicate assignment path: " + assignment.assetPath);
            }

            List<ESAssetBundleAssetEntry> assets = assetList.assets ?? new List<ESAssetBundleAssetEntry>();
            var businessIdentityOwners = new Dictionary<string, ESAssetBundleAssetEntry>(StringComparer.Ordinal);
            foreach (ESAssetBundleAssetEntry asset in assets)
            {
                if (asset == null || asset.identity == null || !asset.identity.IsValid
                    || string.IsNullOrWhiteSpace(asset.internalName) || string.IsNullOrWhiteSpace(asset.assetBundleKey))
                    throw new InvalidOperationException("[ESRes][Build] AssetList contains an invalid asset record.");
                if (!assignmentByPath.TryGetValue(asset.internalName, out ESAssetBundleAssignment assignment)
                    || !string.Equals(assignment.assetBundleKey, asset.assetBundleKey, StringComparison.Ordinal))
                    throw new InvalidOperationException($"[ESRes][Build] AssetList does not match BuildPlan: Path={asset.internalName}, BundleKey={asset.assetBundleKey}");
                if (asset.isBusinessAsset && !businessIdentityOwners.TryAdd(asset.identity.Key, asset))
                    throw new InvalidOperationException("[ESRes][Build] Duplicate business asset identity: " + asset.identity.Key);
            }

            foreach (ESAssetBundleAssignment assignment in assignmentByPath.Values)
            {
                if (!assignment.isBusinessAsset) continue;
                bool found = assets.Any(asset => asset != null && asset.isBusinessAsset
                    && string.Equals(asset.internalName, assignment.assetPath, StringComparison.Ordinal)
                    && string.Equals(asset.assetBundleKey, assignment.assetBundleKey, StringComparison.Ordinal));
                if (!found)
                    throw new InvalidOperationException("[ESRes][Build] Business assignment missing from AssetList: " + assignment.assetPath);
            }
        }

        private static void ValidateLabels(ESAssetBundleBuildPlan plan)
        {
            foreach (var assignment in plan.assignments)
            {
                var importer = AssetImporter.GetAtPath(assignment.assetPath);
                if (importer == null || !string.Equals(importer.assetBundleName, assignment.assetBundleKey, StringComparison.Ordinal))
                    throw new InvalidOperationException($"[ESRes][Build] 当前 AB 标签与 BuildPlan 不一致：Path={assignment.assetPath}, ExpectedBundleKey={assignment.assetBundleKey}");
            }
        }

        private static void BuildLibraryStage(string platform, string owner, List<string> bundleKeys, string buildRoot, AssetBundleManifest unityManifest, ESAssetBundleAssetList assetList)
        {
            string stageFolder = ESAssetPipelineIO.StagingLibraryFolder(platform, owner);
            RecreateGeneratedDirectory(stageFolder);
            string assetBundlesFolder = Path.Combine(stageFolder, ESAssetPipelineIO.AssetBundlesFolderName);
            Directory.CreateDirectory(assetBundlesFolder);
            var manifest = new ESAssetBundleManifest { platform = platform, libraryName = owner };
            foreach (string key in bundleKeys.OrderBy(item => item, StringComparer.Ordinal))
            {
                string source = Path.Combine(buildRoot, key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source)) throw new FileNotFoundException("[ESRes][Build] Unity 未产出计划中的 AB：BundleKey=" + key, source);
                string fileName = ESAssetPipelineIO.SafeSegment(key) + ".bundle";
                string localRelativePath = ESAssetPipelineIO.AssetBundleRelativePath(fileName);
                string destination = Path.Combine(stageFolder, localRelativePath.Replace('/', Path.DirectorySeparatorChar));
                // 发布候选从 BuildCache 复制；缓存必须保留其原始 Manifest 与 AB。
                File.Copy(source, destination, true);
                BuildPipeline.GetCRCForAssetBundle(destination, out uint crc);
                manifest.assetBundles.Add(new ESAssetBundleRecord { assetBundleKey = key, fileName = fileName, unityHash = unityManifest.GetAssetBundleHash(key).ToString(), sha256 = ESResManifestIntegrity.ComputeFileSha256(destination),
                    crc = crc, size = new FileInfo(destination).Length, localRelativePath = localRelativePath, dependencies = unityManifest.GetAllDependencies(key).OrderBy(item => item, StringComparer.Ordinal).ToList() });
            }
            var businessAssets = assetList.assets.Where(item => item.isBusinessAsset && string.Equals(item.ownerLibrary, owner, StringComparison.Ordinal));
            foreach (var asset in businessAssets)
            {
                if (asset.identity.IsSubAsset) manifest.subAssetsById.Add(new ESRuntimeSubAssetManifestRecord { guid = asset.identity.guid, localFileId = asset.identity.localFileId, assetBundleKey = asset.assetBundleKey,
                    internalName = asset.internalName, subAssetName = asset.subAssetName, typeName = asset.typeName });
                else manifest.mainAssetsByGuid.Add(new ESRuntimeMainAssetManifestRecord { guid = asset.identity.guid, assetBundleKey = asset.assetBundleKey, internalName = asset.internalName, typeName = asset.typeName });
            }
            string manifestPath = Path.Combine(stageFolder, ESAssetPipelineIO.BundleManifestFileName);
            ESAssetPipelineIO.WriteJson(manifestPath, manifest);
            string catalogSource = Path.Combine(ESAssetPipelineIO.LibraryBakeFolder(owner), ESAssetPipelineIO.CatalogFileName);
            string catalogDestination = Path.Combine(stageFolder, ESAssetPipelineIO.CatalogFileName);
            ESAssetLibraryCatalog catalog;
            if (File.Exists(catalogSource)) { File.Copy(catalogSource, catalogDestination, true); catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(catalogSource); }
            else { catalog = new ESAssetLibraryCatalog { libraryName = owner, libraryFolder = owner }; ESAssetPipelineIO.WriteJson(catalogDestination, catalog); }
            var identity = new ESAssetLibraryIdentity { libraryName = catalog.libraryName, libraryFolder = owner, platform = platform, version = ESGlobalResSetting.Instance.Version,
                channel = "staging", catalogSha256 = ESResManifestIntegrity.ComputeFileSha256(catalogDestination), assetBundleManifestSha256 = ESResManifestIntegrity.ComputeFileSha256(manifestPath),
                assetBundles = manifest.assetBundles.Select(item => new ESAssetBundleIdentityHash { assetBundleKey = item.assetBundleKey, sha256 = item.sha256, size = item.size }).ToList() };
            ESAssetPipelineIO.WriteJson(Path.Combine(stageFolder, ESAssetPipelineIO.LibraryIdentityFileName), identity);
        }

        private static void RemoveStaleLibraryStages(string platform, IEnumerable<string> owners)
        {
            string librariesRoot = ESAssetPipelineIO.StagingLibrariesRoot(platform);
            if (!Directory.Exists(librariesRoot))
                return;

            var active = new HashSet<string>(owners.Select(ESAssetPipelineIO.SafeSegment), StringComparer.OrdinalIgnoreCase);
            foreach (string folder in Directory.EnumerateDirectories(librariesRoot))
            {
                if (!active.Contains(Path.GetFileName(folder)))
                    Directory.Delete(folder, true);
            }
        }

        private static void CleanStagingRoot(string stagingRoot)
        {
            if (!Directory.Exists(stagingRoot))
                return;

            foreach (string folder in Directory.EnumerateDirectories(stagingRoot))
            {
                if (!string.Equals(Path.GetFileName(folder), ESAssetPipelineIO.LibrariesFolderName, StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(folder, true);
            }

            // BuildSet 只在全部 Library 成功构建后写入；旧指针不可保留。
            foreach (string file in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.TopDirectoryOnly))
                File.Delete(file);
        }

        private static void RecreateGeneratedDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }
    }
}
