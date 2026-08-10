using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESAssetBundleBuilder
    {
        /// <summary>
        /// Project-side release gates run here before the resource pipeline mutates generated output.
        /// Exceptions are deliberately allowed to stop the current build/batch process.
        /// </summary>
        public static event Action BeforeBuildValidation;

        public static void Build()
        {
            BeforeBuildValidation?.Invoke();
            ESAssetPipelineIO.EnsureAssetBundleReleaseMode();
            string platform = ESAssetPipelineIO.PlatformName;
            string planFolder = ESAssetPipelineIO.PlanRoot(platform);
            var plan = ESAssetPipelineIO.ReadJson<ESAssetBundleBuildPlan>(Path.Combine(planFolder, ESAssetPipelineIO.PlanFileName));
            var assetList = ESAssetPipelineIO.ReadJson<ESAssetBundleAssetList>(Path.Combine(planFolder, ESAssetPipelineIO.AssetListFileName));
            if (plan.errors.Count > 0) throw new InvalidOperationException("[ESRes][Build] BuildPlan 包含错误，拒绝构建。");
            ValidatePlanAndAssetList(plan, assetList);
            ValidateLabels(plan);
            string sourceFingerprint = ComputeSourceFingerprint(plan);
            string planFingerprint = ESResManifestIntegrity.ComputeFileSha256(
                Path.Combine(planFolder, ESAssetPipelineIO.PlanFileName));
            string assetListFingerprint = ESResManifestIntegrity.ComputeFileSha256(
                Path.Combine(planFolder, ESAssetPipelineIO.AssetListFileName));

            string buildId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            string stagingRoot = ESAssetPipelineIO.StagingRoot(platform);
            CleanStagingRoot(stagingRoot);
            // 固定保留 Unity 原始 Manifest 与 AB 输出，供下一次构建复用。
            // 它不属于 Staging，因而不会上传、下载或混入发布目录。
            string buildRoot = ESAssetPipelineIO.BuildCacheRoot(platform);
            ESAssetPipelineIO.EnsureGeneratedDirectory(buildRoot);
            var builds = plan.assignments.GroupBy(item => item.assetBundleKey, StringComparer.Ordinal).Select(group => new AssetBundleBuild
            {
                assetBundleName = group.Key,
                assetNames = group.Select(item => item.assetPath).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray()
            }).ToArray();
            BuildTarget target = ESAssetBundleBuildTargetUtility.GetBuildTarget(ESGlobalResSetting.Instance.applyPlatform);
            AssetBundleManifest unityManifest = BuildPipeline.BuildAssetBundles(buildRoot, builds, BuildAssetBundleOptions.ChunkBasedCompression, target);
            if (unityManifest == null) throw new InvalidOperationException("[ESRes][Build] BuildPipeline.BuildAssetBundles 返回 null。");
            ValidateBuiltBundleGraph(plan, unityManifest);
            ValidateBuiltAssetContent(plan, assetList);
            PruneBuildCache(buildRoot, builds.Select(item => item.assetBundleName));

            var ownerByBundle = plan.assignments.GroupBy(item => item.assetBundleKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(item => item.ownerLibrary).OrderBy(item => item, StringComparer.Ordinal).First(), StringComparer.Ordinal);
            var owners = ownerByBundle.Values.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            foreach (string owner in owners) BuildLibraryStage(platform, owner, ownerByBundle.Where(item => string.Equals(item.Value, owner, StringComparison.Ordinal)).Select(item => item.Key).ToList(), buildRoot, unityManifest, assetList);
            RemoveStaleLibraryStages(platform, owners);
            ESAssetPipelineIO.WriteJson(Path.Combine(stagingRoot, ESAssetPipelineIO.BuildSetFileName), new ESAssetBuildSet
            {
                platform = platform,
                buildId = buildId,
                builtUtc = DateTime.UtcNow.ToString("O"),
                libraryFolders = owners.Select(ESAssetPipelineIO.SafeSegment).ToList(),
                sourceFingerprint = sourceFingerprint,
                planFingerprint = planFingerprint,
                assetListFingerprint = assetListFingerprint
            }, true);
            Debug.Log($"[ESAssetBundleBuilder] 构建完成，共 {builds.Length} 个 AB。输出：{stagingRoot}");
        }

        private static void ValidatePlanAndAssetList(ESAssetBundleBuildPlan plan, ESAssetBundleAssetList assetList)
        {
            if (plan == null || assetList == null)
                throw new InvalidOperationException("[ESRes][Build] BuildPlan/AssetList cannot be null.");
            if (plan.formatVersion != 2 || assetList.formatVersion != 2)
                throw new InvalidOperationException("[ESRes][Build] BuildPlan/AssetList 命名协议已过期，请重新规划。");
            if (!string.Equals(plan.platform, assetList.platform, StringComparison.Ordinal))
                throw new InvalidOperationException($"[ESRes][Build] BuildPlan and AssetList platform mismatch: Plan={plan.platform}, AssetList={assetList.platform}");

            var assignmentByPath = new Dictionary<string, ESAssetBundleAssignment>(StringComparer.Ordinal);
            foreach (ESAssetBundleAssignment assignment in plan.assignments ?? new List<ESAssetBundleAssignment>())
            {
                if (assignment == null || string.IsNullOrWhiteSpace(assignment.assetPath) || string.IsNullOrWhiteSpace(assignment.assetBundleKey))
                    throw new InvalidOperationException("[ESRes][Build] BuildPlan contains an invalid assignment.");
                ESAssetBundleUtility.RequireValidAssetBundleKey(assignment.assetBundleKey);
                string physicalFileName = assignment.assetBundleKey + ".bundle";
                if (physicalFileName.Length > ESAssetBundleUtility.MaxAssetBundleFileNameLength)
                    throw new InvalidOperationException("[ESRes][Build] AssetBundle 文件名超长：" + physicalFileName);
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

        private static void ValidateBuiltBundleGraph(ESAssetBundleBuildPlan plan, AssetBundleManifest unityManifest)
        {
            var planned = new HashSet<string>((plan.assignments ?? new List<ESAssetBundleAssignment>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.assetBundleKey))
                .Select(item => item.assetBundleKey), StringComparer.Ordinal);
            var built = new HashSet<string>(unityManifest.GetAllAssetBundles() ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (!planned.SetEquals(built))
                throw new InvalidOperationException("[ESRes][Build] Unity 实际 AB 集合与 BuildPlan 不一致。Missing="
                    + string.Join(",", planned.Except(built)) + " Extra=" + string.Join(",", built.Except(planned)));

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key)
            {
                if (visited.Contains(key)) return;
                if (!visiting.Add(key))
                    throw new InvalidOperationException("[ESRes][Build] Unity 生成了循环 AB 依赖：" + key);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependency in unityManifest.GetDirectDependencies(key) ?? Array.Empty<string>())
                {
                    if (string.Equals(key, dependency, StringComparison.Ordinal) || !unique.Add(dependency) || !built.Contains(dependency))
                        throw new InvalidOperationException("[ESRes][Build] Unity 生成了无效 AB 直接依赖：" + key + " -> " + dependency);
                    Visit(dependency);
                }
                visiting.Remove(key);
                visited.Add(key);
            }
            foreach (string key in built) Visit(key);
        }

        private static void ValidateBuiltAssetContent(ESAssetBundleBuildPlan plan, ESAssetBundleAssetList assetList)
        {
            var expectedPathsByBundle = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (ESAssetBundleAssignment assignment in plan.assignments ?? new List<ESAssetBundleAssignment>())
            {
                if (assignment == null || string.IsNullOrWhiteSpace(assignment.assetPath)
                    || string.IsNullOrWhiteSpace(assignment.assetBundleKey))
                    continue;

                if (!expectedPathsByBundle.TryGetValue(
                        assignment.assetBundleKey,
                        out HashSet<string> expectedPaths))
                    expectedPathsByBundle[assignment.assetBundleKey] = expectedPaths =
                        new HashSet<string>(StringComparer.Ordinal);
                expectedPaths.Add(assignment.assetPath);
            }

            foreach (KeyValuePair<string, HashSet<string>> pair in expectedPathsByBundle)
            {
                string[] actualPaths = AssetDatabase.GetAssetPathsFromAssetBundle(pair.Key)
                    ?? Array.Empty<string>();
                var actual = new HashSet<string>(actualPaths, StringComparer.Ordinal);
                foreach (string expectedPath in pair.Value)
                    if (!actual.Contains(expectedPath))
                        throw new InvalidOperationException(
                            "[ESRes][Build] 计划资产未进入实际 AB：BundleKey=" + pair.Key
                            + ", Path=" + expectedPath);
            }

            foreach (ESAssetBundleAssetEntry asset in assetList.assets ?? new List<ESAssetBundleAssetEntry>())
            {
                if (asset == null || asset.identity == null || !asset.identity.IsValid
                    || string.IsNullOrWhiteSpace(asset.internalName))
                    continue;
                if (!IdentityExistsAtPath(asset))
                    throw new InvalidOperationException(
                        "[ESRes][Build] AssetList 中的资产身份未在目标资产文件内找到："
                        + asset.identity.Key + ", Path=" + asset.internalName);
            }
        }

        private static bool IdentityExistsAtPath(ESAssetBundleAssetEntry asset)
        {
            if (asset == null || asset.identity == null || string.IsNullOrWhiteSpace(asset.internalName))
                return false;

            if (!asset.identity.IsSubAsset)
                return string.Equals(
                    AssetDatabase.AssetPathToGUID(asset.internalName),
                    asset.identity.guid,
                    StringComparison.Ordinal);

            foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(asset.internalName))
            {
                if (loaded == null
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        loaded,
                        out string guid,
                        out long localFileId))
                    continue;
                if (string.Equals(guid, asset.identity.guid, StringComparison.Ordinal)
                    && localFileId == asset.identity.localFileId)
                    return true;
            }
            return false;
        }

        internal static string ComputeSourceFingerprint(ESAssetBundleBuildPlan plan)
        {
            var builder = new StringBuilder();
            foreach (ESAssetBundleAssignment assignment in (plan?.assignments ?? new List<ESAssetBundleAssignment>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.assetPath))
                .OrderBy(item => item.assetPath, StringComparer.Ordinal))
            {
                string guid = AssetDatabase.AssetPathToGUID(assignment.assetPath) ?? string.Empty;
                string dependencyHash = AssetDatabase.GetAssetDependencyHash(assignment.assetPath).ToString();
                builder.Append(assignment.assetPath).Append('|');
                builder.Append(guid).Append('|');
                builder.Append(dependencyHash).Append('\n');
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void BuildLibraryStage(string platform, string owner, List<string> bundleKeys, string buildRoot, AssetBundleManifest unityManifest, ESAssetBundleAssetList assetList)
        {
            string stageFolder = ESAssetPipelineIO.StagingLibraryFolder(platform, owner);
            RecreateGeneratedDirectory(stageFolder);
            string assetBundlesFolder = Path.Combine(stageFolder, ESAssetPipelineIO.AssetBundlesFolderName);
            ESAssetPipelineIO.EnsureGeneratedDirectory(assetBundlesFolder);
            var manifest = new ESAssetBundleManifest { platform = platform, libraryName = owner };
            foreach (string key in bundleKeys.OrderBy(item => item, StringComparer.Ordinal))
            {
                string source = Path.Combine(buildRoot, key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source)) throw new FileNotFoundException("[ESRes][Build] Unity 未产出计划中的 AB：BundleKey=" + key, source);
                string fileName = ESAssetBundleUtility.ToSafeAssetBundleFileName(key + ".bundle");
                string localRelativePath = ESAssetPipelineIO.AssetBundleRelativePath(fileName);
                string destination = Path.Combine(stageFolder, localRelativePath.Replace('/', Path.DirectorySeparatorChar));
                // CRC 必须从 Unity BuildPipeline 的原始产物读取。复制并重命名后的 Staging 文件
                // 在部分 Unity 版本中不会被 GetCRCForAssetBundle 识别，即使文件内容完全有效。
                if (!BuildPipeline.GetCRCForAssetBundle(source, out uint crc))
                    throw new InvalidDataException("[ESRes][Build] 无法读取 Unity 原始 AssetBundle CRC：" + source);
                // 发布候选从 BuildCache 复制；缓存必须保留其原始 Manifest 与 AB。
                ESAssetPipelineIO.CopyGeneratedFileAtomic(source, destination);
                manifest.assetBundles.Add(new ESAssetBundleRecord { assetBundleKey = key, fileName = fileName, unityHash = unityManifest.GetAssetBundleHash(key).ToString(), sha256 = ESResManifestIntegrity.ComputeFileSha256(destination),
                    crc = crc, size = new FileInfo(destination).Length, localRelativePath = localRelativePath, dependencies = unityManifest.GetDirectDependencies(key).OrderBy(item => item, StringComparer.Ordinal).ToList() });
            }
            var businessAssets = assetList.assets.Where(item => item.isBusinessAsset && string.Equals(item.ownerLibrary, owner, StringComparison.Ordinal));
            foreach (var asset in businessAssets)
            {
                AddAssetRecord(manifest, asset);
            }
            string catalogSource = Path.Combine(ESAssetPipelineIO.LibraryBakeFolder(owner), ESAssetPipelineIO.CatalogFileName);
            string catalogDestination = Path.Combine(stageFolder, ESAssetPipelineIO.CatalogFileName);
            ESAssetLibraryCatalog catalog;
            if (File.Exists(catalogSource)) { ESAssetPipelineIO.CopyGeneratedFileAtomic(catalogSource, catalogDestination); catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(catalogSource); }
            else { catalog = new ESAssetLibraryCatalog { libraryName = owner, libraryFolder = owner,
                libraryBundleCode = ESAssetBundleUtility.NormalizeLibraryCode(owner), libraryAssetGuid = ESAssetBundleUtility.StableHash(owner, 32) };
                ESAssetPipelineIO.WriteJson(catalogDestination, catalog); }

            // 同一 GUID 可以出现在多个业务 Catalog 中，但物理资源只打进一个权威 AB。
            // 每份 Catalog 所属 Manifest 都写入 GUID -> 全局 BundleKey 的别名记录，
            // 使 Consumer 只引用该 Library 时也能自动拉取真实物理 Bundle，而不会重复打包。
            var globalBusinessAssets = (assetList.assets ?? new List<ESAssetBundleAssetEntry>())
                .Where(item => item != null && item.isBusinessAsset && item.identity != null && item.identity.IsValid)
                .ToDictionary(item => item.identity.Key, item => item, StringComparer.Ordinal);
            var indexedIdentities = new HashSet<string>(manifest.mainAssetsByGuid.Select(item => item.guid), StringComparer.Ordinal);
            indexedIdentities.UnionWith(manifest.subAssetsById.Select(item => item.guid + ":" + item.localFileId));
            foreach (ESAssetCatalogEntry entry in catalog.assets ?? new List<ESAssetCatalogEntry>())
            {
                if (entry == null || !entry.isBusinessAsset || entry.identity == null || !entry.identity.IsValid || indexedIdentities.Contains(entry.identity.Key))
                    continue;
                if (!globalBusinessAssets.TryGetValue(entry.identity.Key, out ESAssetBundleAssetEntry physicalAsset))
                    throw new InvalidDataException("[ESRes][Build] Catalog 业务资源没有物理 AB 归属：Library=" + owner + ", Page=" + entry.pageName + ", Identity=" + entry.identity.Key);
                AddAssetRecord(manifest, physicalAsset);
                indexedIdentities.Add(entry.identity.Key);
            }

            string manifestPath = Path.Combine(stageFolder, ESAssetPipelineIO.BundleManifestFileName);
            ESAssetPipelineIO.WriteJson(manifestPath, manifest);
            var identity = new ESAssetLibraryIdentity { libraryName = catalog.libraryName, libraryFolder = owner, libraryBundleCode = catalog.libraryBundleCode,
                platform = platform, version = ESGlobalResSetting.Instance.Version,
                deliveryMode = ESAssetDeliveryModeEditorUtility.ResolveLibrary(owner),
                channel = "staging", catalogSha256 = ESResManifestIntegrity.ComputeFileSha256(catalogDestination), assetBundleManifestSha256 = ESResManifestIntegrity.ComputeFileSha256(manifestPath),
                assetBundles = manifest.assetBundles.Select(item => new ESAssetBundleIdentityHash { assetBundleKey = item.assetBundleKey, sha256 = item.sha256, size = item.size }).ToList() };
            ESAssetPipelineIO.WriteJson(Path.Combine(stageFolder, ESAssetPipelineIO.LibraryIdentityFileName), identity);
        }

        private static void AddAssetRecord(ESAssetBundleManifest manifest, ESAssetBundleAssetEntry asset)
        {
            if (asset.identity.IsSubAsset) manifest.subAssetsById.Add(new ESRuntimeSubAssetManifestRecord { guid = asset.identity.guid, localFileId = asset.identity.localFileId, assetBundleKey = asset.assetBundleKey,
                internalName = asset.internalName, subAssetName = asset.subAssetName, typeName = asset.typeName });
            else manifest.mainAssetsByGuid.Add(new ESRuntimeMainAssetManifestRecord { guid = asset.identity.guid, assetBundleKey = asset.assetBundleKey, internalName = asset.internalName, typeName = asset.typeName });
        }

        private static void RemoveStaleLibraryStages(string platform, IEnumerable<string> owners)
        {
            string librariesRoot = ESAssetPipelineIO.StagingLibrariesRoot(platform);
            if (!Directory.Exists(librariesRoot))
                return;
            ESAssetPipelineIO.EnsureGeneratedDirectory(librariesRoot);

            var active = new HashSet<string>(owners.Select(ESAssetPipelineIO.SafeSegment), StringComparer.OrdinalIgnoreCase);
            foreach (string folder in Directory.EnumerateDirectories(librariesRoot))
            {
                if (!active.Contains(Path.GetFileName(folder)))
                    ESAssetPipelineIO.DeleteGeneratedDirectory(folder);
            }
        }

        private static void CleanStagingRoot(string stagingRoot)
        {
            if (!Directory.Exists(stagingRoot))
                return;
            ESAssetPipelineIO.EnsureGeneratedDirectory(stagingRoot);

            foreach (string folder in Directory.EnumerateDirectories(stagingRoot))
            {
                if (!string.Equals(Path.GetFileName(folder), ESAssetPipelineIO.LibrariesFolderName, StringComparison.OrdinalIgnoreCase))
                    ESAssetPipelineIO.DeleteGeneratedDirectory(folder);
            }

            // BuildSet 只在全部 Library 成功构建后写入；旧指针不可保留。
            foreach (string file in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.TopDirectoryOnly))
                ESAssetPipelineIO.DeleteGeneratedFile(file);
        }

        private static void PruneBuildCache(string buildRoot, IEnumerable<string> activeBundleKeys)
        {
            if (!Directory.Exists(buildRoot)) return;
            ESAssetPipelineIO.EnsureGeneratedDirectory(buildRoot);
            string rootManifestName = Path.GetFileName(buildRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var retainedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                rootManifestName,
                rootManifestName + ".manifest"
            };
            foreach (string key in activeBundleKeys ?? Enumerable.Empty<string>())
            {
                retainedFiles.Add(key);
                retainedFiles.Add(key + ".manifest");
            }

            int removed = 0;
            foreach (string file in Directory.EnumerateFiles(buildRoot, "*", SearchOption.TopDirectoryOnly))
            {
                if (retainedFiles.Contains(Path.GetFileName(file))) continue;
                ESAssetPipelineIO.DeleteGeneratedFile(file);
                removed++;
            }
            // 新命名规范禁止斜杠，当前有效 Bundle 不会占用子目录；这里清除旧路径式命名产物。
            foreach (string folder in Directory.EnumerateDirectories(buildRoot))
            {
                ESAssetPipelineIO.DeleteGeneratedDirectory(folder);
                removed++;
            }
            if (removed > 0)
                Debug.Log($"[ESRes][Cleanup] BuildCache 已移除 {removed} 个不在当前 BuildPlan 中的旧产物。");
        }

        private static void RecreateGeneratedDirectory(string path)
        {
            if (Directory.Exists(path))
                ESAssetPipelineIO.DeleteGeneratedDirectory(path);
            ESAssetPipelineIO.EnsureGeneratedDirectory(path);
        }
    }
}
