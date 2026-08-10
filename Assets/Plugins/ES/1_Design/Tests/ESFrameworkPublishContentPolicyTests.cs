using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using UnityEditor;

namespace ES.Tests
{
    public sealed class ESFrameworkPublishContentPolicyTests
    {
        [Test]
        public void Classify_UsesExplicitContentBoundary()
        {
            Assert.That(
                ESFrameworkPublishContentPolicy.Classify("Assets/ESNormalAssets/Camera/ESCameraViewDefinitionCatalog.asset"),
                Is.EqualTo(ESFrameworkPublishPathDisposition.RequiredPackage));
            Assert.That(
                ESFrameworkPublishContentPolicy.Classify("Assets/ESNormalAssets/Data/GlobalData/EditorConfi/全局编辑器流程基本配置.asset"),
                Is.EqualTo(ESFrameworkPublishPathDisposition.RequiredPackage));
            Assert.That(
                ESFrameworkPublishContentPolicy.Classify("Assets/ESNormalAssets/Data/GlobalData/CmdAgent/ESCmdAgent.asset"),
                Is.EqualTo(ESFrameworkPublishPathDisposition.GeneratedOnDemand));
            Assert.That(
                ESFrameworkPublishContentPolicy.Classify("Assets/ESNormalAssets/Data/GlobalData/SceneManage/ESSceneGlobalData.asset"),
                Is.EqualTo(ESFrameworkPublishPathDisposition.ProjectOwnedState));
            Assert.That(
                ESFrameworkPublishContentPolicy.Classify("Assets/ESNormalAssets/CharacterTemplates/ES基础角色模板.prefab"),
                Is.EqualTo(ESFrameworkPublishPathDisposition.OptionalHeavyContent));
            Assert.That(
                ESFrameworkPublishContentPolicy.Classify("Assets/ESNormalAssets/Unclassified/New.asset"),
                Is.EqualTo(ESFrameworkPublishPathDisposition.Unknown));
        }

        [Test]
        public void CurrentProjectConfiguration_CoversEveryBuiltInRequiredPath()
        {
            ESGlobalEditorDefaultConfi config = AssetDatabase.LoadAssetAtPath<ESGlobalEditorDefaultConfi>(
                "Assets/ESNormalAssets/Data/GlobalData/EditorConfi/全局编辑器流程基本配置.asset");
            Assert.That(config, Is.Not.Null);

            bool valid = ESFrameworkPublishContentPolicy.TryValidateConfiguration(
                config.PackagePublishAssetPaths,
                config.PackagePublishRequiredAssetPaths,
                config.PackagePublishExcludePaths,
                out string error);

            Assert.That(valid, Is.True, error);
        }

        [Test]
        public void RemovingRequiredCameraRoot_IsRejected()
        {
            var roots = ESGlobalEditorDefaultConfi.CreateDefaultPackagePublishAssetPaths();
            roots.Remove("Assets/ESNormalAssets/Camera");

            bool valid = ESFrameworkPublishContentPolicy.TryValidateConfiguration(
                roots,
                ESGlobalEditorDefaultConfi.CreateDefaultPackagePublishRequiredAssetPaths(),
                new List<string>(),
                out string error);

            Assert.That(valid, Is.False);
            StringAssert.Contains("Assets/ESNormalAssets/Camera", error);
        }

        [Test]
        public void HardcodedESNormalAssetsPaths_HaveExplicitDisposition()
        {
            bool valid = ESFrameworkPublishContentPolicy.TryAuditHardcodedAssetPaths(
                new[] { "Assets/Plugins/ES", "Assets/Scripts/ESLogic" },
                out ESFrameworkPublishHardcodedPathAudit audit,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(audit.unknownPaths, Is.Empty);
            Assert.That(audit.requiredCount, Is.GreaterThan(0));
        }

        [Test]
        public void ExportedUnityPackage_ExactPlanPasses()
        {
            string packagePath = CreateUnityPackageFixture(
                "Assets/Plugins/ES/TestA.cs",
                "Assets/Scripts/ESLogic/TestB.cs");
            try
            {
                bool valid = ESFrameworkPublishContentPolicy.TryValidateExportedUnityPackage(
                    packagePath,
                    new[] { "Assets/Plugins/ES/TestA.cs", "Assets/Scripts/ESLogic/TestB.cs" },
                    out int packagedPathCount,
                    out string error);

                Assert.That(valid, Is.True, error);
                Assert.That(packagedPathCount, Is.EqualTo(2));
            }
            finally
            {
                File.Delete(packagePath);
            }
        }

        [Test]
        public void ExportedUnityPackage_MissingPlanAssetIsRejected()
        {
            string packagePath = CreateUnityPackageFixture("Assets/Plugins/ES/TestA.cs");
            try
            {
                bool valid = ESFrameworkPublishContentPolicy.TryValidateExportedUnityPackage(
                    packagePath,
                    new[] { "Assets/Plugins/ES/TestA.cs", "Assets/Scripts/ESLogic/TestB.cs" },
                    out _,
                    out string error);

                Assert.That(valid, Is.False);
                StringAssert.Contains("缺少", error);
            }
            finally
            {
                File.Delete(packagePath);
            }
        }

        [Test]
        public void ExportedUnityPackage_UnexpectedAssetIsRejected()
        {
            string packagePath = CreateUnityPackageFixture(
                "Assets/Plugins/ES/TestA.cs",
                "Assets/Unrelated/Unexpected.asset");
            try
            {
                bool valid = ESFrameworkPublishContentPolicy.TryValidateExportedUnityPackage(
                    packagePath,
                    new[] { "Assets/Plugins/ES/TestA.cs" },
                    out _,
                    out string error);

                Assert.That(valid, Is.False);
                StringAssert.Contains("计划外", error);
            }
            finally
            {
                File.Delete(packagePath);
            }
        }

        private static string CreateUnityPackageFixture(params string[] assetPaths)
        {
            string packagePath = Path.Combine(
                Path.GetTempPath(),
                "ESFrameworkPublishContentPolicy-" + Guid.NewGuid().ToString("N") + ".unitypackage");
            using (var file = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var gzip = new GZipStream(file, CompressionMode.Compress, false))
            {
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string guid = (i + 1).ToString("x32");
                    WriteTarEntry(gzip, guid + "/pathname", Encoding.UTF8.GetBytes(assetPaths[i]));
                }

                gzip.Write(new byte[1024], 0, 1024);
            }
            return packagePath;
        }

        private static void WriteTarEntry(Stream stream, string entryName, byte[] payload)
        {
            var header = new byte[512];
            WriteAscii(header, 0, 100, entryName);
            WriteAscii(header, 100, 8, "0000644");
            WriteAscii(header, 108, 8, "0000000");
            WriteAscii(header, 116, 8, "0000000");
            WriteAscii(header, 124, 12, Convert.ToString(payload.Length, 8).PadLeft(11, '0'));
            header[156] = (byte)'0';
            stream.Write(header, 0, header.Length);
            stream.Write(payload, 0, payload.Length);

            int padding = (512 - payload.Length % 512) % 512;
            if (padding > 0)
                stream.Write(new byte[padding], 0, padding);
        }

        private static void WriteAscii(byte[] buffer, int offset, int length, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, Math.Min(length, bytes.Length));
        }
    }
}
