using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class ESPublishDownloadClosedLoopTests
    {
        [UnityTest]
        public IEnumerator MinimalV5FileFixture_DownloadBoot_SelfTest()
        {
            // This is a v5 file fixture/download self-test. It does NOT call
            // ESAssetBundlePublisher.Publish() and does NOT prove a real publisher loop.
            const string platform = "StandaloneWindows64";
            const string releaseVersion = "1.0.0.selftest";
            const string libraryFolder = "self_lib";
            const string bundleKey = "self.bundle";
            const string bundleFile = "AssetBundles/self.bundle";

            string tempRoot = Path.Combine(Application.temporaryCachePath, "ESPublishDownloadClosedLoopTests", Guid.NewGuid().ToString("N"));
            string releaseRoot = Path.Combine(tempRoot, "release");
            string platformRoot = Path.Combine(releaseRoot, platform);
            string versionRoot = Path.Combine(platformRoot, releaseVersion);
            string libraryRoot = Path.Combine(versionRoot, "Libraries", libraryFolder);
            string bundleRoot = Path.Combine(libraryRoot, "AssetBundles");
            string testId = Guid.NewGuid().ToString("N");
            string cacheRoot = Path.Combine(Application.persistentDataPath, "SelfTest", testId);
            byte[] dummyBundle = Encoding.UTF8.GetBytes("dummy-asset-bundle-for-self-test");
            ESGlobalResSetting settings = null;

            try
            {
                Directory.CreateDirectory(bundleRoot);
                string bundlePath = Path.Combine(bundleRoot, "self.bundle");
                File.WriteAllBytes(bundlePath, dummyBundle);
                string bundleSha = ESResManifestIntegrity.ComputeFileSha256(bundlePath);
                long bundleSize = new FileInfo(bundlePath).Length;

                string identityUrl = ToFileUri(Path.Combine(libraryRoot, "ESAssetLibraryIdentity.json"));
                string catalogUrl = ToFileUri(Path.Combine(libraryRoot, "ESAssetLibraryCatalog.json"));
                string manifestUrl = ToFileUri(Path.Combine(libraryRoot, "ESAssetBundleManifest.json"));
                string bundleUrl = ToFileUri(bundlePath);

                var identity = new ESRuntimeLibraryIdentity
                {
                    formatVersion = 5,
                    libraryName = "Self Test Library",
                    libraryFolder = libraryFolder,
                    libraryBundleCode = "self",
                    platform = platform,
                    version = releaseVersion,
                    channel = "default",
                    catalogUrl = catalogUrl,
                    assetBundleManifestUrl = manifestUrl,
                    catalogSha256 = string.Empty,
                    assetBundleManifestSha256 = string.Empty,
                    deliveryMode = ESAssetDeliveryMode.Remote
                };
                var catalog = new ESRuntimeCatalog
                {
                    formatVersion = ESRuntimeCatalog.CurrentFormatVersion,
                    libraryName = "Self Test Library",
                    libraryFolder = libraryFolder,
                    libraryBundleCode = "self",
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    assets = new List<ESRuntimeCatalogEntry>()
                };
                WriteJson(Path.Combine(libraryRoot, "ESAssetLibraryCatalog.json"), catalog);

                var bundleManifest = new ESRuntimeBundleManifest
                {
                    formatVersion = 5,
                    platform = platform,
                    libraryName = "Self Test Library",
                    assetBundles = new List<ESRuntimeBundleRecord>
                    {
                        new ESRuntimeBundleRecord
                        {
                            assetBundleKey = bundleKey,
                            fileName = "self.bundle",
                            sha256 = bundleSha,
                            localRelativePath = bundleFile,
                            size = bundleSize,
                            dependencies = new List<string>()
                        }
                    },
                    mainAssetsByGuid = new List<ESRuntimeReleaseMainAssetRecord>(),
                    subAssetsById = new List<ESRuntimeReleaseSubAssetRecord>()
                };
                WriteJson(Path.Combine(libraryRoot, "ESAssetBundleManifest.json"), bundleManifest);

                identity.catalogSha256 = ESResManifestIntegrity.ComputeFileSha256(Path.Combine(libraryRoot, "ESAssetLibraryCatalog.json"));
                identity.assetBundleManifestSha256 = ESResManifestIntegrity.ComputeFileSha256(Path.Combine(libraryRoot, "ESAssetBundleManifest.json"));
                WriteJson(Path.Combine(libraryRoot, "ESAssetLibraryIdentity.json"), identity);
                string identityJson = File.ReadAllText(Path.Combine(libraryRoot, "ESAssetLibraryIdentity.json"), Encoding.UTF8);
                Assert.That(identityJson, Does.Contain("\"catalogSha256\""));
                Assert.That(identityJson, Does.Contain("\"assetBundleManifestSha256\""));
                Assert.That(identityJson, Does.Contain("\"catalogUrl\""));
                Assert.That(identityJson, Does.Contain("\"assetBundleManifestUrl\""));

                string totalConsumerPath = Path.Combine(versionRoot, "Consumers", "total.json");
                Directory.CreateDirectory(Path.GetDirectoryName(totalConsumerPath));
                var consumer = new ESRuntimeConsumerManifest
                {
                    formatVersion = 5,
                    consumerId = "self.total",
                    name = "Self Total",
                    version = releaseVersion,
                    platform = platform,
                    channel = "default",
                    publishedUtc = DateTime.UtcNow.ToString("O"),
                    isTotalConsumer = true,
                    tags = new List<string>(),
                    requiredConsumers = new List<ESRuntimeConsumerReference>(),
                    libraries = new List<ESRuntimeConsumerLibraryReference>
                    {
                        new ESRuntimeConsumerLibraryReference
                        {
                            libraryName = "Self Test Library",
                            libraryFolder = libraryFolder,
                            libraryIdentityUrl = identityUrl,
                            libraryIdentitySha256 = string.Empty,
                            deliveryMode = ESAssetDeliveryMode.Remote,
                            requiredAtBoot = true
                        }
                    },
                    gameCoreAssets = new List<ESRuntimeConsumerGameCoreReference>(),
                    residentAssets = new List<ESRuntimeConsumerResidentAssetReference>(),
                    codePackages = new List<ESRuntimeConsumerCodePackageReference>()
                };
                WriteJson(totalConsumerPath, consumer);
                consumer.libraries[0].libraryIdentitySha256 = ESResManifestIntegrity.ComputeFileSha256(Path.Combine(libraryRoot, "ESAssetLibraryIdentity.json"));
                WriteJson(totalConsumerPath, consumer);
                string consumerSha = ESResManifestIntegrity.ComputeFileSha256(totalConsumerPath);

                string bundleIndexPath = Path.Combine(versionRoot, "ESAssetReleaseBundleIndex.json");
                var bundleIndex = new ESRuntimeReleaseBundleIndex
                {
                    formatVersion = 5,
                    platform = platform,
                    releaseVersion = releaseVersion,
                    assetBundles = new List<ESRuntimeReleaseBundleRecord>
                    {
                        new ESRuntimeReleaseBundleRecord
                        {
                            libraryFolder = libraryFolder,
                            assetBundleKey = bundleKey,
                            fileUrl = bundleUrl,
                            sha256 = bundleSha,
                            localRelativePath = bundleFile,
                            deliveryMode = ESAssetDeliveryMode.Remote,
                            size = bundleSize,
                            dependencies = new List<string>()
                        }
                    }
                };
                WriteJson(bundleIndexPath, bundleIndex);

                string rootPath = Path.Combine(platformRoot, "ESAssetReleaseManifest.json");
                var root = new ESRuntimeReleaseManifest
                {
                    formatVersion = 5,
                    platform = platform,
                    releaseVersion = releaseVersion,
                    channel = "default",
                    publishedUtc = DateTime.UtcNow.ToString("O"),
                    totalConsumerUrl = ToFileUri(totalConsumerPath),
                    totalConsumerSha256 = consumerSha,
                    bundleIndexUrl = ToFileUri(bundleIndexPath),
                    bundleIndexSha256 = ESResManifestIntegrity.ComputeFileSha256(bundleIndexPath)
                };
                WriteJson(rootPath, root);

                settings = ScriptableObject.CreateInstance<ESGlobalResSetting>();
                settings.Path_Net = ToFileUri(releaseRoot);
                settings.applyPlatform = RuntimePlatform.WindowsPlayer;
                settings.Path_Sub_DownloadRelative_ = "SelfTest/" + testId;
                Assert.That(settings.TryGetRuntimeDownloadCachePath(out string authoritativeCacheRoot, out string cachePathError), Is.True, cachePathError);
                cacheRoot = authoritativeCacheRoot;

                var downloader = new ESRuntimeReleaseDownloader(settings, ESAssetRunMode.HotUpdate);
                UniTask<ESRuntimeReleaseDownloadResult> task = downloader.DownloadBootAsync();
                while (!task.GetAwaiter().IsCompleted)
                    yield return null;
                ESRuntimeReleaseDownloadResult result = task.GetAwaiter().GetResult();

                Assert.That(result.ReleaseVersion, Is.EqualTo(releaseVersion));
                Assert.That(result.DownloadedLibraries, Contains.Item(libraryFolder));
                Assert.That(result.RuntimeMap, Is.Not.Null);
                Assert.That(File.Exists(Path.Combine(cacheRoot, "ReleaseV2", platform, "Releases", releaseVersion, bundleFile)), Is.True);
            }
            finally
            {
                if (settings != null)
                    UnityEngine.Object.DestroyImmediate(settings);
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, true);
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        private static string ToFileUri(string path)
        {
            return "file:///" + Path.GetFullPath(path).Replace('\\', '/');
        }

        private static void WriteJson<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        }
    }
}
