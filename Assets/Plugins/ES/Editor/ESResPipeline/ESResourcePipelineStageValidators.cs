using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ES_Design.ConfigKey.Tests")]

namespace ES
{
    /// <summary>
    /// 资源管理窗口与 EditMode 测试共用的阶段产物校验权威。
    /// 这里只验证阶段产物，不执行 Bake、Build、Publish 或远端网络操作。
    /// </summary>
    internal static class ESResourcePipelineStageValidators
    {
        internal static bool HasCatalogStage(
            IEnumerable<ESAssetLibrary> libraries,
            Func<ESAssetLibrary, string> catalogPathResolver,
            Func<ESAssetLibrary, string> referenceGraphPathResolver)
        {
            List<ESAssetLibrary> buildLibraries = (libraries ?? Enumerable.Empty<ESAssetLibrary>())
                .Where(library => library != null && library.ContainsBuild)
                .ToList();
            if (buildLibraries.Count == 0
                || catalogPathResolver == null
                || referenceGraphPathResolver == null)
                return false;

            foreach (ESAssetLibrary library in buildLibraries)
            {
                string catalogPath = catalogPathResolver(library);
                string referenceGraphPath = referenceGraphPathResolver(library);
                if (!TryReadJson(catalogPath, out ESAssetLibraryCatalog catalog)
                    || !IsCatalogValid(catalog)
                    || !TryReadJson(referenceGraphPath, out ESAssetReferenceGraph graph)
                    || !IsReferenceGraphValid(graph))
                    return false;
                if (!string.Equals(catalog.libraryName, library.Name, StringComparison.Ordinal)
                    || !string.Equals(catalog.libraryFolder, library.LibFolderName, StringComparison.Ordinal)
                    || !string.Equals(graph.libraryName, catalog.libraryName, StringComparison.Ordinal)
                    || !string.Equals(graph.libraryFolder, catalog.libraryFolder, StringComparison.Ordinal)
                    || !string.Equals(graph.generatedUtc, catalog.generatedUtc, StringComparison.Ordinal))
                    return false;

                var catalogIdentities = new HashSet<string>(
                    (catalog.assets ?? new List<ESAssetCatalogEntry>())
                        .Select(asset => asset?.identity?.Key)
                        .Where(key => !string.IsNullOrWhiteSpace(key)),
                    StringComparer.Ordinal);
                var graphRootIdentities = new HashSet<string>(
                    (graph.roots ?? new List<ESAssetReferenceRoot>())
                        .Select(root => root?.identity?.Key)
                        .Where(key => !string.IsNullOrWhiteSpace(key)),
                    StringComparer.Ordinal);
                if (!catalogIdentities.SetEquals(graphRootIdentities))
                    return false;
            }

            return true;
        }

        internal static bool IsCatalogValid(ESAssetLibraryCatalog catalog)
        {
            if (catalog == null)
                return false;

            if (catalog.formatVersion != ESAssetPipelineIO.CatalogFormatVersion
                || string.IsNullOrWhiteSpace(catalog.libraryName)
                || string.IsNullOrWhiteSpace(catalog.libraryFolder)
                || string.IsNullOrWhiteSpace(catalog.libraryAssetGuid)
                || string.IsNullOrWhiteSpace(catalog.generatedUtc)
                || catalog.errors == null
                || catalog.errors.Count > 0)
                return false;

            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAssetCatalogEntry asset in catalog.assets ?? new List<ESAssetCatalogEntry>())
            {
                if (asset == null
                    || asset.identity == null
                    || !asset.identity.IsValid
                    || string.IsNullOrWhiteSpace(asset.assetPath)
                    || !identities.Add(asset.identity.Key))
                    return false;
            }

            return true;
        }

        internal static bool IsReferenceGraphValid(ESAssetReferenceGraph graph)
        {
            if (graph == null
                || graph.formatVersion != ESAssetPipelineIO.ReferenceGraphFormatVersion
                || string.IsNullOrWhiteSpace(graph.libraryName)
                || string.IsNullOrWhiteSpace(graph.libraryFolder)
                || string.IsNullOrWhiteSpace(graph.generatedUtc)
                || graph.errors == null
                || graph.errors.Count > 0)
                return false;

            var rootIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAssetReferenceRoot root in graph.roots ?? new List<ESAssetReferenceRoot>())
            {
                if (root == null
                    || root.identity == null
                    || !root.identity.IsValid
                    || string.IsNullOrWhiteSpace(root.assetPath)
                    || !rootIdentities.Add(root.identity.Key))
                    return false;
            }

            var nodePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAssetReferenceNode node in graph.nodes ?? new List<ESAssetReferenceNode>())
            {
                if (node == null
                    || node.identity == null
                    || !node.identity.IsValid
                    || string.IsNullOrWhiteSpace(node.assetPath)
                    || !nodePaths.Add(node.assetPath))
                    return false;
            }

            return true;
        }

        internal static bool HasPlanStage(string planPath, string assetListPath)
        {
            if (!TryReadJson(planPath, out ESAssetBundleBuildPlan plan)
                || plan.formatVersion != 2
                || !TryReadJson(assetListPath, out ESAssetBundleAssetList assetList)
                || assetList.formatVersion != 2
                || string.IsNullOrWhiteSpace(plan.platform)
                || !string.Equals(plan.platform, assetList.platform, StringComparison.OrdinalIgnoreCase)
                || plan.errors == null
                || plan.errors.Count > 0
                || plan.assignments == null
                || plan.assignments.Count == 0)
                return false;

            var assignmentByPath = new Dictionary<string, ESAssetBundleAssignment>(StringComparer.Ordinal);
            var bundleKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAssetBundleAssignment assignment in plan.assignments)
            {
                if (assignment == null
                    || string.IsNullOrWhiteSpace(assignment.assetPath)
                    || string.IsNullOrWhiteSpace(assignment.assetBundleKey)
                    || assignment.identity == null
                    || !assignment.identity.IsValid
                    || !assignmentByPath.TryAdd(assignment.assetPath, assignment))
                    return false;

                try
                {
                    ESAssetBundleUtility.RequireValidAssetBundleKey(assignment.assetBundleKey);
                }
                catch
                {
                    return false;
                }

                if (!bundleKeys.Add(assignment.assetBundleKey))
                    return false;
            }

            var assetPaths = new HashSet<string>(StringComparer.Ordinal);
            var assetIdentities = new HashSet<string>(StringComparer.Ordinal);
            List<ESAssetBundleAssignment> businessAssignments = new List<ESAssetBundleAssignment>(
                plan.assignments.Where(assignment => assignment.isBusinessAsset));
            foreach (ESAssetBundleAssetEntry asset in assetList.assets ?? new List<ESAssetBundleAssetEntry>())
            {
                if (asset == null
                    || asset.identity == null
                    || !asset.identity.IsValid
                    || string.IsNullOrWhiteSpace(asset.internalName)
                    || string.IsNullOrWhiteSpace(asset.assetBundleKey)
                    || !assetPaths.Add(asset.internalName)
                    || !assetIdentities.Add(asset.identity.Key)
                    || !assignmentByPath.TryGetValue(asset.internalName, out ESAssetBundleAssignment assignment)
                    || !string.Equals(assignment.assetBundleKey, asset.assetBundleKey, StringComparison.Ordinal)
                    || assignment.identity == null
                    || !assignment.identity.Equals(asset.identity)
                    || assignment.isBusinessAsset != asset.isBusinessAsset)
                    return false;
            }

            foreach (ESAssetBundleAssignment assignment in businessAssignments)
            {
                bool found = assetList.assets != null
                    && assetList.assets.Any(asset => asset != null
                        && asset.isBusinessAsset
                        && string.Equals(asset.internalName, assignment.assetPath, StringComparison.Ordinal)
                        && string.Equals(asset.assetBundleKey, assignment.assetBundleKey, StringComparison.Ordinal));
                if (!found)
                    return false;
            }

            return true;
        }

        internal static bool HasPublishStage(
            string rootManifestPath,
            string releaseFolder,
            string consumerPath)
        {
            if (!TryReadJson(rootManifestPath, out ESAssetReleaseManifest release)
                || release.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                || string.IsNullOrWhiteSpace(release.platform)
                || string.IsNullOrWhiteSpace(release.releaseVersion)
                || string.IsNullOrWhiteSpace(release.bundleIndexUrl)
                || string.IsNullOrWhiteSpace(release.bundleIndexSha256)
                || string.IsNullOrWhiteSpace(release.totalConsumerUrl)
                || string.IsNullOrWhiteSpace(release.totalConsumerSha256))
                return false;

            return IsPublishArtifactsValid(release, releaseFolder, consumerPath);
        }

        internal static bool IsPublishArtifactsValid(
            ESAssetReleaseManifest release,
            string releaseFolder,
            string consumerPath)
        {
            if (release == null
                || release.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                || string.IsNullOrWhiteSpace(release.platform)
                || string.IsNullOrWhiteSpace(release.releaseVersion)
                || string.IsNullOrWhiteSpace(release.bundleIndexSha256)
                || string.IsNullOrWhiteSpace(release.totalConsumerSha256)
                || string.IsNullOrWhiteSpace(releaseFolder)
                || string.IsNullOrWhiteSpace(consumerPath))
                return false;

            string bundleIndexFileName = Path.GetFileName(
                (release.bundleIndexUrl ?? string.Empty).Replace('\\', '/'));
            string consumerFileName = Path.GetFileName(
                (release.totalConsumerUrl ?? string.Empty).Replace('\\', '/'));
            if (!string.Equals(bundleIndexFileName, ESAssetPipelineIO.ReleaseBundleIndexFileName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(consumerFileName)
                || !string.Equals(
                    Path.GetFullPath(consumerPath),
                    Path.GetFullPath(Path.Combine(releaseFolder, "Consumers", consumerFileName)),
                    StringComparison.OrdinalIgnoreCase))
                return false;

            string bundleIndexPath = Path.Combine(
                releaseFolder,
                ESAssetPipelineIO.ReleaseBundleIndexFileName);
            if (!TryReadJson(bundleIndexPath, out ESAssetReleaseBundleIndex bundleIndex)
                || bundleIndex.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                || !string.Equals(bundleIndex.platform, release.platform, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(bundleIndex.releaseVersion, release.releaseVersion, StringComparison.Ordinal)
                || bundleIndex.assetBundles == null
                || bundleIndex.assetBundles.Count == 0
                || !ESResManifestIntegrity.VerifyFileSha256(bundleIndexPath, release.bundleIndexSha256))
                return false;

            var bundlesByKey = new Dictionary<string, ESAssetReleaseBundleRecord>(StringComparer.Ordinal);
            foreach (ESAssetReleaseBundleRecord bundle in bundleIndex.assetBundles)
            {
                if (bundle == null
                    || string.IsNullOrWhiteSpace(bundle.assetBundleKey)
                    || string.IsNullOrWhiteSpace(bundle.libraryFolder)
                    || (bundle.deliveryMode != ESAssetDeliveryMode.BuiltIn
                        && string.IsNullOrWhiteSpace(bundle.fileUrl))
                    || string.IsNullOrWhiteSpace(bundle.localRelativePath)
                    || string.IsNullOrWhiteSpace(bundle.sha256)
                    || bundle.size <= 0
                    || !bundlesByKey.TryAdd(bundle.assetBundleKey, bundle))
                    return false;

                string bundlePath;
                try
                {
                    bundlePath = ESAssetPipelineIO.ResolveGeneratedRelativePath(
                        releaseFolder,
                        bundle.localRelativePath);
                }
                catch
                {
                    return false;
                }
                if (!File.Exists(bundlePath)
                    || new FileInfo(bundlePath).Length != bundle.size
                    || !ESResManifestIntegrity.VerifyFileSha256(bundlePath, bundle.sha256))
                    return false;
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            bool cycle = false;
            void Visit(string key)
            {
                if (cycle || visited.Contains(key))
                    return;
                if (!visiting.Add(key))
                {
                    cycle = true;
                    return;
                }

                var dependencies = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependency in bundlesByKey[key].dependencies ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(dependency)
                        || !bundlesByKey.ContainsKey(dependency)
                        || string.Equals(dependency, key, StringComparison.Ordinal)
                        || !dependencies.Add(dependency))
                    {
                        cycle = true;
                        return;
                    }
                    Visit(dependency);
                }

                visiting.Remove(key);
                visited.Add(key);
            }

            foreach (string key in bundlesByKey.Keys)
                Visit(key);
            if (cycle)
                return false;

            if (!File.Exists(consumerPath)
                || !ESResManifestIntegrity.VerifyFileSha256(consumerPath, release.totalConsumerSha256)
                || !TryReadJson(consumerPath, out ESAssetConsumerManifest consumer)
                || consumer.formatVersion != ESAssetPipelineIO.RuntimeProtocolFormatVersion
                || !consumer.isTotalConsumer
                || string.IsNullOrWhiteSpace(consumer.consumerId)
                || !string.Equals(consumer.platform, release.platform, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        internal static string GetUploadPlanFingerprint(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path) + "|"
                    + File.GetLastWriteTimeUtc(path).Ticks + "|"
                    + new FileInfo(path).Length;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool ShouldInvalidateRemotePlanCache(
            string cachedStatus,
            string cachedFingerprint,
            string currentFingerprint)
        {
            if (string.Equals(cachedFingerprint, currentFingerprint, StringComparison.Ordinal))
                return false;
            return !string.IsNullOrEmpty(cachedStatus)
                && !string.Equals(cachedStatus, "未检查", StringComparison.Ordinal);
        }

        private static bool TryReadJson<T>(string path, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            try
            {
                value = ESAssetPipelineIO.ReadJson<T>(path);
                return value != null;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class ESAssetLibraryConsumerRelationRules
    {
        internal static void SetLibraryRelation(
            ESAssetLibraryConsumer consumer,
            ESAssetLibrary library,
            bool required)
        {
            if (consumer == null || library == null)
                return;

            consumer.ConsumerLibFolders ??= new List<ESAssetLibrary>();
            consumer.OptionalLibFolders ??= new List<ESAssetLibrary>();
            RemoveAllRelations(consumer.ConsumerLibFolders, library);
            RemoveAllRelations(consumer.OptionalLibFolders, library);
            if (required)
                consumer.ConsumerLibFolders.Add(library);
            else
                consumer.OptionalLibFolders.Add(library);
        }

        internal static void RemoveLibraryRelation(
            ESAssetLibraryConsumer consumer,
            ESAssetLibrary library)
        {
            if (consumer == null || library == null)
                return;

            RemoveAllRelations(consumer.ConsumerLibFolders, library);
            RemoveAllRelations(consumer.OptionalLibFolders, library);
        }

        private static void RemoveAllRelations(
            List<ESAssetLibrary> relations,
            ESAssetLibrary library)
        {
            relations?.RemoveAll(item => ReferenceEquals(item, library) || item == library);
        }
    }
}
