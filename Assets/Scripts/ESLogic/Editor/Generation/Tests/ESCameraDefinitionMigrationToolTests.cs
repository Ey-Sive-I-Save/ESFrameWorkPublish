using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCameraDefinitionMigrationToolTests
    {
        private const string TestDirectory = "Assets/Plugins/ES/1_Design/Tests/GeneratedCameraMigration";
        private static readonly Dictionary<string, ESCameraDefinitionReference> References =
            new Dictionary<string, ESCameraDefinitionReference>(StringComparer.Ordinal)
            {
                {
                    "player.third_person",
                    new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.PlayerThirdPerson, "player.third_person")
                },
            };

        private readonly List<string> generatedRoots = new List<string>();

        [TearDown]
        public void TearDown()
        {
            ESCameraDefinitionMigrationTool.MigrationBackupSession.TestBeforeRestoreCopy = null;
            ESCameraDefinitionMigrationTool.MigrationBackupSession.TestRestoreRootOverride = null;

            for (int i = 0; i < generatedRoots.Count; i++)
            {
                string path = generatedRoots[i];
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }

            generatedRoots.Clear();

            string absoluteTestDirectory = Path.Combine(GetProjectRoot(), TestDirectory);
            if (Directory.Exists(absoluteTestDirectory))
                Directory.Delete(absoluteTestDirectory, true);

            AssetDatabase.Refresh();
        }

        [Test]
        public void Migrate_EmptyReference_WritesDualKeyAndClearsLegacy()
        {
            string path = CreateConsumerAsset("EmptyReference", default, "player.third_person");
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();

            bool changed = ESCameraDefinitionMigrationTool.ScanObject(
                asset,
                path,
                References,
                migrate: true,
                session,
                report);

            Assert.That(changed, Is.True);
            Assert.That(report.migrated, Is.EqualTo(1));
            Assert.That(asset.Definition, Is.EqualTo(References["player.third_person"]));
            Assert.That(asset.LegacyDefinitionKey, Is.Empty);

            AssetDatabase.SaveAssetIfDirty(asset);
            session.MarkMigrated(path);
            Assert.That(session.TryComplete(out string error), Is.True, error);
        }

        [Test]
        public void Migrate_SameDualKey_ClearsLegacyOnly()
        {
            string path = CreateConsumerAsset(
                "SameDualKey",
                References["player.third_person"],
                "player.third_person");
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();

            bool changed = ESCameraDefinitionMigrationTool.ScanObject(
                asset,
                path,
                References,
                migrate: true,
                session,
                report);

            Assert.That(changed, Is.True);
            Assert.That(report.legacyCleared, Is.EqualTo(1));
            Assert.That(asset.Definition, Is.EqualTo(References["player.third_person"]));
            Assert.That(asset.LegacyDefinitionKey, Is.Empty);

            AssetDatabase.SaveAssetIfDirty(asset);
            session.MarkMigrated(path);
            Assert.That(session.TryComplete(out string error), Is.True, error);
        }

        [Test]
        public void Migrate_ConflictingDualKey_RejectsAndKeepsLegacy()
        {
            var conflict = new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.None, "other.camera");
            string path = CreateConsumerAsset("ConflictingDualKey", conflict, "player.third_person");
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();

            bool changed = ESCameraDefinitionMigrationTool.ScanObject(
                asset,
                path,
                References,
                migrate: true,
                session,
                report);

            Assert.That(changed, Is.False);
            Assert.That(report.conflicts, Is.EqualTo(1));
            Assert.That(asset.Definition, Is.EqualTo(conflict));
            Assert.That(asset.LegacyDefinitionKey, Is.EqualTo("player.third_person"));
            Assert.That(session.entries, Is.Empty);
        }

        [Test]
        public void TryCapture_MissingMeta_IsHardFailure()
        {
            string path = CreateConsumerAsset("MissingMeta", default, "player.third_person");
            File.Delete(path + ".meta");
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);

            Assert.That(session.TryCapture(path, out string error), Is.False);
            Assert.That(error, Does.Contain(".meta"));
            Assert.That(session.entries, Is.Empty);
        }

        [Test]
        public void TryComplete_ManifestPublishFailure_RollsBack()
        {
            string path = CreateConsumerAsset("ManifestPublishFailure", default, "player.third_person");
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();

            ESCameraDefinitionMigrationTool.ScanObject(asset, path, References, true, session, report);
            AssetDatabase.SaveAssetIfDirty(asset);
            session.MarkMigrated(path);

            string beforeHash = session.entries[0].assetHashBefore;
            string manifestPath = Path.Combine(session.SnapshotPath, "manifest.json");
            File.Delete(manifestPath);
            Directory.CreateDirectory(manifestPath);

            Assert.That(session.TryComplete(out string error), Is.False);
            Assert.That(error, Does.Contain("回滚"));
            Assert.That(ComputeFileHash(path), Is.EqualTo(beforeHash));
        }

        [Test]
        public void TryComplete_MissingAfterHash_RollsBack()
        {
            string path = CreateConsumerAsset("MissingAfterHash", default, "player.third_person");
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();

            ESCameraDefinitionMigrationTool.ScanObject(asset, path, References, true, session, report);
            AssetDatabase.SaveAssetIfDirty(asset);
            string beforeHash = session.entries[0].assetHashBefore;

            Assert.That(session.TryComplete(out string error), Is.False);
            Assert.That(error, Does.Contain("哈希未完整记录"));
            Assert.That(ComputeFileHash(path), Is.EqualTo(beforeHash));
        }

        [Test]
        public void SelectRestoreCandidate_RejectsDrift()
        {
            string path = CreateConsumerAsset("Drift", default, "player.third_person");
            CompleteMigration(path, out string snapshotPath);
            RegisterRoot(snapshotPath);
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("marker").stringValue = "drifted";
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Assert.That(
                ESCameraDefinitionMigrationTool.MigrationBackupSession.TrySelectRestorableManifest(
                    out _,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("漂移"));

            if (Directory.Exists(snapshotPath))
                Directory.Delete(snapshotPath, true);
        }

        [Test]
        public void SelectRestoreCandidate_SkipsBrokenNewestAndFallsBackToOlder()
        {
            string restoreRoot = Path.Combine(GetProjectRoot(), "Library", "CameraDefinitionFallbackTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(restoreRoot);
            RegisterRoot(restoreRoot);

            string olderPath = Path.Combine(restoreRoot, "20000101T000000000Z-a-older");
            string newerPath = Path.Combine(restoreRoot, "20000101T000000000Z-z-newer");
            string olderAsset = CreateConsumerAsset("FallbackOlder", default, "player.third_person");
            string newerAsset = CreateConsumerAsset("FallbackNewer", default, "player.third_person");

            CompleteMigrationTo(olderAsset, olderPath);
            var newerSession = CompleteMigrationTo(newerAsset, newerPath);

            string brokenBackupFile = Path.Combine(newerSession.SnapshotPath, newerSession.entries[0].backupFile);
            File.Delete(brokenBackupFile);

            ESCameraDefinitionMigrationTool.MigrationBackupSession.TestRestoreRootOverride = restoreRoot;
            try
            {
                bool selected = ESCameraDefinitionMigrationTool.MigrationBackupSession.TrySelectRestorableManifest(
                    out ESCameraDefinitionMigrationTool.BackupManifest manifest,
                    out string selectedDirectory,
                    out string error);

                Assert.That(selected, Is.True, error);
                Assert.That(selectedDirectory, Is.EqualTo(olderPath));
                Assert.That(manifest.entries, Has.Count.EqualTo(1));
                Assert.That(manifest.entries[0].assetPath, Is.EqualTo(olderAsset));
            }
            finally
            {
                ESCameraDefinitionMigrationTool.MigrationBackupSession.TestRestoreRootOverride = null;
            }
        }

        [Test]
        public void RestoreTransaction_ApplyFailure_RollsBackReverse()
        {
            string firstPath = CreateConsumerAsset("RestoreFirst", default, "player.third_person");
            string secondPath = CreateConsumerAsset("RestoreSecond", default, "player.third_person");
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            RegisterRoot(session.SnapshotPath);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();

            TestCameraMigrationConsumer first = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(firstPath);
            TestCameraMigrationConsumer second = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(secondPath);
            ESCameraDefinitionMigrationTool.ScanObject(first, firstPath, References, true, session, report);
            ESCameraDefinitionMigrationTool.ScanObject(second, secondPath, References, true, session, report);
            AssetDatabase.SaveAssetIfDirty(first);
            AssetDatabase.SaveAssetIfDirty(second);
            session.MarkMigrated(firstPath);
            session.MarkMigrated(secondPath);

            string firstAfterHash = session.entries[0].assetHashAfterMigration;
            string secondAfterHash = session.entries[1].assetHashAfterMigration;
            ESCameraDefinitionMigrationTool.MigrationBackupSession.TestBeforeRestoreCopy = entry =>
            {
                if (ReferenceEquals(entry, session.entries[1]))
                    File.Delete(entry.resolvedBackupFile);
            };

            try
            {
                bool restored = ESCameraDefinitionMigrationTool.MigrationBackupSession.TryRestoreTransaction(
                    session.entries,
                    session.SnapshotPath,
                    out string error);

                Assert.That(restored, Is.False);
                Assert.That(error, Does.Contain("已回滚"));
                Assert.That(ComputeFileHash(firstPath), Is.EqualTo(firstAfterHash));
                Assert.That(ComputeFileHash(secondPath), Is.EqualTo(secondAfterHash));
            }
            finally
            {
                ESCameraDefinitionMigrationTool.MigrationBackupSession.TestBeforeRestoreCopy = null;
            }
        }

        [Test]
        public void Drawer_ReadWriteMultiObject_UsesSerializedMixedState()
        {
            string firstPath = CreateConsumerAsset(
                "DrawerFirst",
                new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.PlayerThirdPerson, "player.third_person"),
                string.Empty);
            string secondPath = CreateConsumerAsset(
                "DrawerSecond",
                new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.VehicleChase, "vehicle.chase"),
                string.Empty);
            TestCameraMigrationConsumer first = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(firstPath);
            TestCameraMigrationConsumer second = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(secondPath);

            var serialized = new SerializedObject(first, second);
            SerializedProperty definition = serialized.FindProperty("definition");
            Assert.That(definition.hasMultipleDifferentValues, Is.True);

            ESCameraDefinitionReference target = References["player.third_person"];
            ESCameraDefinitionReferenceDrawer.Write(serialized, "definition", target);

            SerializedProperty after = serialized.FindProperty("definition");
            Assert.That(after.hasMultipleDifferentValues, Is.False);
            Assert.That(ESCameraDefinitionReferenceDrawer.Read(after), Is.EqualTo(target));
            Assert.That(first.Definition, Is.EqualTo(target));
            Assert.That(second.Definition, Is.EqualTo(target));
        }

        private static void CompleteMigration(string path, out string snapshotPath)
        {
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession();
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();
            ESCameraDefinitionMigrationTool.ScanObject(asset, path, References, true, session, report);
            AssetDatabase.SaveAssetIfDirty(asset);
            session.MarkMigrated(path);
            Assert.That(session.TryComplete(out string error), Is.True, error);
            snapshotPath = session.SnapshotPath;
        }

        private static ESCameraDefinitionMigrationTool.MigrationBackupSession CompleteMigrationTo(string path, string snapshotPath)
        {
            var session = new ESCameraDefinitionMigrationTool.MigrationBackupSession(snapshotPath);
            TestCameraMigrationConsumer asset = AssetDatabase.LoadAssetAtPath<TestCameraMigrationConsumer>(path);
            var report = new ESCameraDefinitionMigrationTool.MigrationReport();
            ESCameraDefinitionMigrationTool.ScanObject(asset, path, References, true, session, report);
            AssetDatabase.SaveAssetIfDirty(asset);
            session.MarkMigrated(path);
            Assert.That(session.TryComplete(out string error), Is.True, error);
            return session;
        }

        private static string CreateConsumerAsset(
            string fileName,
            ESCameraDefinitionReference definition,
            string legacyKey)
        {
            EnsureTestDirectory();
            TestCameraMigrationConsumer asset = ScriptableObject.CreateInstance<TestCameraMigrationConsumer>();
            string path = TestDirectory + "/" + fileName + ".asset";
            AssetDatabase.CreateAsset(asset, path);

            var serialized = new SerializedObject(asset);
            SerializedProperty definitionProperty = serialized.FindProperty("definition");
            definitionProperty.FindPropertyRelative("enumKey").intValue = (int)definition.enumKey;
            definitionProperty.FindPropertyRelative("stringKey").stringValue = definition.stringKey ?? string.Empty;
            serialized.FindProperty("legacyDefinitionKey").stringValue = legacyKey ?? string.Empty;
            serialized.FindProperty("marker").stringValue = string.Empty;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return path;
        }

        private static void EnsureTestDirectory()
        {
            if (AssetDatabase.IsValidFolder(TestDirectory))
                return;

            const string parent = "Assets/Plugins/ES/1_Design/Tests";
            if (!AssetDatabase.IsValidFolder(parent))
                throw new InvalidOperationException("Missing test asset parent: " + parent);

            AssetDatabase.CreateFolder(parent, "GeneratedCameraMigration");
        }

        private void RegisterRoot(string path)
        {
            if (!string.IsNullOrEmpty(path) && !generatedRoots.Contains(path))
                generatedRoots.Add(path);
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string ComputeFileHash(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }

        [Serializable]
        public sealed class TestCameraMigrationConsumer : ScriptableObject
        {
            public ESCameraDefinitionReference definition;
            public string marker;

            [SerializeField, HideInInspector]
            private string legacyDefinitionKey;

            public ESCameraDefinitionReference Definition => definition;
            public string LegacyDefinitionKey => legacyDefinitionKey;
        }
    }
}
