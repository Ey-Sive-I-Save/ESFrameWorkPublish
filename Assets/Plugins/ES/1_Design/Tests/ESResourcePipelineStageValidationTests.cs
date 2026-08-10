using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESResourcePipelineStageValidationTests
    {
        private readonly List<ESAssetLibrary> createdLibraries = new List<ESAssetLibrary>();
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(
                Application.temporaryCachePath,
                nameof(ESResourcePipelineStageValidationTests),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
            createdObjects.Clear();

            for (int i = createdLibraries.Count - 1; i >= 0; i--)
            {
                if (createdLibraries[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdLibraries[i]);
            }
            createdLibraries.Clear();

            if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void CatalogStage_RequiresEveryBuildLibraryAndRejectsAnyCatalogError()
        {
            ESAssetLibrary baked = CreateLibrary("baked", true);
            ESAssetLibrary failed = CreateLibrary("failed", true);
            ESAssetLibrary excluded = CreateLibrary("excluded", false);

            WriteCatalogPair(baked, "generation-1");
            WriteCatalogPair(failed, "generation-1", new List<string> { "catalog-failure" });

            Assert.That(
                ESResourcePipelineStageValidators.HasCatalogStage(
                    new[] { baked, failed, excluded },
                    CatalogPath,
                    GraphPath),
                Is.False);

            WriteCatalogPair(failed, "generation-1");
            Assert.That(
                ESResourcePipelineStageValidators.HasCatalogStage(
                    new[] { baked, failed, excluded },
                    CatalogPath,
                    GraphPath),
                Is.True);

            File.Delete(GraphPath(baked));
            Assert.That(
                ESResourcePipelineStageValidators.HasCatalogStage(
                    new[] { baked, failed, excluded },
                    CatalogPath,
                    GraphPath),
                Is.False);

            WriteCatalogPair(baked, "generation-1");
            var mismatchedGraph = new ESAssetReferenceGraph
            {
                libraryName = baked.Name,
                libraryFolder = baked.LibFolderName,
                generatedUtc = "generation-2"
            };
            WriteJson(GraphPath(baked), mismatchedGraph);
            Assert.That(
                ESResourcePipelineStageValidators.HasCatalogStage(
                    new[] { baked, failed, excluded },
                    CatalogPath,
                    GraphPath),
                Is.False);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void PlanStage_RejectsPlatformMismatchAndMissingBusinessAssetListEntry()
        {
            string planPath = Path.Combine(tempRoot, "plan.json");
            string assetListPath = Path.Combine(tempRoot, "asset-list.json");
            ESAssetBundleAssignment assignment = CreateAssignment("Assets/Business.prefab", true, "library_business_asset_01");
            var plan = new ESAssetBundleBuildPlan
            {
                platform = "StandaloneWindows64",
                assignments = new List<ESAssetBundleAssignment> { assignment }
            };
            var assetList = new ESAssetBundleAssetList
            {
                platform = "Android",
                assets = new List<ESAssetBundleAssetEntry>
                {
                    CreateAssetListEntry(assignment)
                }
            };
            WriteJson(planPath, plan);
            WriteJson(assetListPath, assetList);

            Assert.That(ESResourcePipelineStageValidators.HasPlanStage(planPath, assetListPath), Is.False);

            assetList.platform = plan.platform;
            assetList.assets.Clear();
            WriteJson(assetListPath, assetList);
            Assert.That(ESResourcePipelineStageValidators.HasPlanStage(planPath, assetListPath), Is.False);

            assetList.assets.Add(CreateAssetListEntry(assignment));
            WriteJson(assetListPath, assetList);
            Assert.That(ESResourcePipelineStageValidators.HasPlanStage(planPath, assetListPath), Is.True);

            assetList.assets[0].identity.guid = "different-guid";
            WriteJson(assetListPath, assetList);
            Assert.That(ESResourcePipelineStageValidators.HasPlanStage(planPath, assetListPath), Is.False);

            assetList.assets[0].identity.guid = assignment.identity.guid;
            plan.errors.Add("plan-failure");
            WriteJson(assetListPath, assetList);
            WriteJson(planPath, plan);
            Assert.That(ESResourcePipelineStageValidators.HasPlanStage(planPath, assetListPath), Is.False);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void PublishStage_RejectsV5RootWhenIndexOrTotalConsumerIsMissing()
        {
            string releaseFolder = Path.Combine(tempRoot, "1.0.0");
            string consumerPath = Path.Combine(releaseFolder, "Consumers", "total.json");
            string rootPath = Path.Combine(tempRoot, "ESAssetReleaseManifest.json");
            string bundleIndexPath = Path.Combine(releaseFolder, ESAssetPipelineIO.ReleaseBundleIndexFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(consumerPath));

            var release = new ESAssetReleaseManifest
            {
                platform = "StandaloneWindows64",
                releaseVersion = "1.0.0",
                bundleIndexUrl = "StandaloneWindows64/1.0.0/ESAssetReleaseBundleIndex.json",
                totalConsumerUrl = "StandaloneWindows64/1.0.0/Consumers/total.json"
            };
            WriteJson(rootPath, release);
            Assert.That(
                ESResourcePipelineStageValidators.HasPublishStage(rootPath, releaseFolder, consumerPath),
                Is.False);

            var bundleIndex = new ESAssetReleaseBundleIndex
            {
                platform = "StandaloneWindows64",
                releaseVersion = release.releaseVersion,
                assetBundles = new List<ESAssetReleaseBundleRecord>
                {
                    new ESAssetReleaseBundleRecord
                    {
                        libraryFolder = "library_business",
                        assetBundleKey = "library_business_asset_01",
                        fileUrl = "StandaloneWindows64/1.0.0/AssetBundles/library_business_asset_01",
                        localRelativePath = "AssetBundles/library_business_asset_01",
                        dependencies = new List<string>()
                    }
                }
            };
            string bundlePath = Path.Combine(releaseFolder, "AssetBundles", "library_business_asset_01");
            Directory.CreateDirectory(Path.GetDirectoryName(bundlePath));
            File.WriteAllBytes(bundlePath, new byte[] { 0x42 });
            bundleIndex.assetBundles[0].size = new FileInfo(bundlePath).Length;
            bundleIndex.assetBundles[0].sha256 = ESResManifestIntegrity.ComputeFileSha256(bundlePath);
            WriteJson(bundleIndexPath, bundleIndex);
            WriteJson(consumerPath, new ESAssetConsumerManifest
            {
                consumerId = "total",
                platform = release.platform,
                isTotalConsumer = true
            });
            release.bundleIndexSha256 = ESResManifestIntegrity.ComputeFileSha256(bundleIndexPath);
            release.totalConsumerSha256 = ESResManifestIntegrity.ComputeFileSha256(consumerPath);
            WriteJson(rootPath, release);

            Assert.That(
                ESResourcePipelineStageValidators.HasPublishStage(rootPath, releaseFolder, consumerPath),
                Is.True);

            File.WriteAllBytes(bundlePath, new byte[] { 0x43 });
            Assert.That(
                ESResourcePipelineStageValidators.HasPublishStage(rootPath, releaseFolder, consumerPath),
                Is.False);

            File.WriteAllBytes(bundlePath, new byte[] { 0x42 });
            File.Delete(consumerPath);
            Assert.That(
                ESResourcePipelineStageValidators.HasPublishStage(rootPath, releaseFolder, consumerPath),
                Is.False);

            File.WriteAllText(consumerPath, "consumer", new UTF8Encoding(false));
            File.Delete(bundleIndexPath);
            Assert.That(
                ESResourcePipelineStageValidators.HasPublishStage(rootPath, releaseFolder, consumerPath),
                Is.False);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void RemotePlanCache_InvalidatesWhenPlanFingerprintChangesOrDisappears()
        {
            string planPath = Path.Combine(tempRoot, "upload-plan.json");
            File.WriteAllText(planPath, "one", new UTF8Encoding(false));
            string firstFingerprint = ESResourcePipelineStageValidators.GetUploadPlanFingerprint(planPath);
            Assert.That(firstFingerprint, Is.Not.Empty);

            File.WriteAllText(planPath, "two-two", new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(planPath, DateTime.UtcNow.AddSeconds(2));
            string changedFingerprint = ESResourcePipelineStageValidators.GetUploadPlanFingerprint(planPath);
            Assert.That(changedFingerprint, Is.Not.EqualTo(firstFingerprint));
            Assert.That(
                ESResourcePipelineStageValidators.ShouldInvalidateRemotePlanCache(
                    "Ready",
                    firstFingerprint,
                    changedFingerprint),
                Is.True);

            File.Delete(planPath);
            Assert.That(
                ESResourcePipelineStageValidators.ShouldInvalidateRemotePlanCache(
                    "Ready",
                    changedFingerprint,
                    ESResourcePipelineStageValidators.GetUploadPlanFingerprint(planPath)),
                Is.True);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void LibraryConsumerRelation_ReassignsWithoutDualOwnershipOrDuplicates()
        {
            ESAssetLibrary library = CreateLibrary("relation", true);
            var consumer = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
            createdObjects.Add(consumer);
            consumer.ConsumerLibFolders.Add(library);
            consumer.ConsumerLibFolders.Add(library);
            consumer.OptionalLibFolders.Add(library);
            consumer.OptionalLibFolders.Add(library);

            ESAssetLibraryConsumerRelationRules.SetLibraryRelation(consumer, library, true);
            Assert.That(consumer.ConsumerLibFolders, Has.Count.EqualTo(1));
            Assert.That(consumer.OptionalLibFolders, Has.No.Member(library));

            ESAssetLibraryConsumerRelationRules.SetLibraryRelation(consumer, library, false);
            Assert.That(consumer.ConsumerLibFolders, Has.No.Member(library));
            Assert.That(consumer.OptionalLibFolders, Has.Count.EqualTo(1));

            ESAssetLibraryConsumerRelationRules.RemoveLibraryRelation(consumer, library);
            Assert.That(consumer.ConsumerLibFolders, Has.No.Member(library));
            Assert.That(consumer.OptionalLibFolders, Has.No.Member(library));
        }

        private ESAssetLibrary CreateLibrary(string folder, bool containsBuild)
        {
            var library = ScriptableObject.CreateInstance<ESAssetLibrary>();
            library.Name = folder + " Library";
            library.LibFolderName = folder;
            library.ContainsBuild = containsBuild;
            createdLibraries.Add(library);
            return library;
        }

        private string CatalogPath(ESAssetLibrary library)
            => Path.Combine(tempRoot, library.LibFolderName + ".catalog.json");

        private string GraphPath(ESAssetLibrary library)
            => Path.Combine(tempRoot, library.LibFolderName + ".graph.json");

        private void WriteCatalogPair(
            ESAssetLibrary library,
            string generation,
            List<string> catalogErrors = null)
        {
            WriteJson(CatalogPath(library), new ESAssetLibraryCatalog
            {
                libraryName = library.Name,
                libraryFolder = library.LibFolderName,
                libraryAssetGuid = "guid-" + library.LibFolderName,
                generatedUtc = generation,
                errors = catalogErrors ?? new List<string>()
            });
            WriteJson(GraphPath(library), new ESAssetReferenceGraph
            {
                libraryName = library.Name,
                libraryFolder = library.LibFolderName,
                generatedUtc = generation
            });
        }

        private static ESAssetBundleAssignment CreateAssignment(
            string assetPath,
            bool isBusinessAsset,
            string bundleKey)
        {
            return new ESAssetBundleAssignment
            {
                assetPath = assetPath,
                assetBundleKey = bundleKey,
                isBusinessAsset = isBusinessAsset,
                identity = new ESPipelineAssetIdentity
                {
                    guid = "guid-" + bundleKey,
                    localFileId = 0
                }
            };
        }

        private static ESAssetBundleAssetEntry CreateAssetListEntry(ESAssetBundleAssignment assignment)
        {
            return new ESAssetBundleAssetEntry
            {
                internalName = assignment.assetPath,
                assetBundleKey = assignment.assetBundleKey,
                isBusinessAsset = assignment.isBusinessAsset,
                identity = new ESPipelineAssetIdentity
                {
                    guid = assignment.identity.guid,
                    localFileId = assignment.identity.localFileId
                }
            };
        }

        private static void WriteJson<T>(string path, T value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        }
    }
}
