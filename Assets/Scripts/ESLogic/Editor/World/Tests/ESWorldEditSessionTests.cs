#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests.Editor.World
{
    public sealed class ESWorldEditSessionTests
    {
        private ESWorldMapAsset source;
        private ESWorldEditSession session;

        [SetUp]
        public void SetUp()
        {
            source = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            InitializeValid(source, "source-map");
            session = ESWorldEditSession.Open(source);
        }

        [TearDown]
        public void TearDown()
        {
            session?.ClearRecoveryState();
            session?.Dispose();
            if (source != null) Object.DestroyImmediate(source);
        }

        [Test]
        public void DraftMutationDoesNotPolluteFormalAsset()
        {
            session.Draft.Definition.mapId = "draft-map";
            session.NotifyDraftChanged();

            Assert.AreEqual("source-map", source.Definition.mapId);
            Assert.AreEqual("draft-map", session.Draft.Definition.mapId);
            Assert.IsTrue(session.IsDirty);
        }

        [Test]
        public void RevertRestoresAcceptedBaseline()
        {
            session.Draft.Definition.mapId = "draft-map";
            session.NotifyDraftChanged();

            session.RevertDraft();

            Assert.AreEqual("source-map", session.Draft.Definition.mapId);
            Assert.IsFalse(session.IsDirty);
        }

        [Test]
        public void ExternalBaselineDriftRejectsCommit()
        {
            session.Draft.Definition.mapId = "draft-map";
            session.NotifyDraftChanged();
            source.Definition.mapId = "external-map";

            ESWorldEditCommitResult result = session.TryCommit();

            Assert.IsFalse(result.success);
            Assert.IsTrue(result.conflict);
            Assert.AreEqual("external-map", source.Definition.mapId);
        }

        [Test]
        public void CommitUsesOneUndoableFormalAssetBoundary()
        {
            session.Draft.Definition.mapId = "committed-map";
            session.NotifyDraftChanged();

            ESWorldEditCommitResult result = session.TryCommit();

            Assert.IsTrue(result.success, result.message);
            Assert.AreEqual("committed-map", source.Definition.mapId);
            Undo.PerformUndo();
            Assert.AreEqual("source-map", source.Definition.mapId);
        }

        [Test]
        public void DomainReloadRecoveryContractRestoresDraftByStableSessionIdentity()
        {
            session.Draft.Definition.mapId = "recovered-draft";
            session.NotifyDraftChanged("definition.mapId");
            session.Dispose();

            session = ESWorldEditSession.Open(source);

            Assert.AreEqual("recovered-draft", session.Draft.Definition.mapId);
            Assert.IsTrue(session.IsDirty);
            CollectionAssert.Contains(session.ChangedPaths, "definition.mapId");
        }

        [Test]
        public void UndoRedoSynchronizationPersistsTheCurrentDraftState()
        {
            Undo.RecordObject(session.Draft, "修改世界草稿");
            session.Draft.Definition.mapId = "draft-before-undo";
            session.NotifyDraftChanged("definition.mapId");

            Undo.PerformUndo();
            session.SynchronizeDraftAfterUndoRedo();
            session.Dispose();
            session = ESWorldEditSession.Open(source);

            Assert.AreEqual("source-map", session.Draft.Definition.mapId);
            Assert.IsFalse(session.IsDirty);
            Assert.AreEqual(0, session.ChangeCount);
        }

        [Test]
        public void ReloadFromSourceAcceptsExternalBaselineAndClearsConflict()
        {
            session.Draft.Definition.mapId = "local-draft";
            session.NotifyDraftChanged("definition.mapId");
            source.Definition.mapId = "external-map";

            Assert.IsTrue(session.RefreshExternalConflict());
            session.ReloadFromSource();

            Assert.AreEqual("external-map", session.Draft.Definition.mapId);
            Assert.IsFalse(session.HasExternalConflict);
            Assert.IsFalse(session.IsDirty);
            Assert.AreEqual(0, session.ChangeCount);
        }

        [TestCase("Queued")]
        [TestCase("Running")]
        [TestCase("Pending")]
        public void ActiveBakeStateIsNotReportedAsFailed(string status)
        {
            var result = new ESContentRegistrationResult { status = status, success = false };

            Assert.AreEqual(ESWorldBuilderWorkbenchWindow.BuildStage.Pending,
                ESWorldBuilderWorkbenchWindow.ResolveBuildStage(result));
        }

        private static void InitializeValid(ESWorldMapAsset asset, string mapId)
        {
            ESWorldMapDefinition definition = asset.Definition;
            definition.mapId = mapId;
            definition.contentVersion = 1;
            definition.contentHash = "baseline";
            definition.generatorKey = "es.tests.world";
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(128f, 128f);
            definition.heightfield.EnsureSamples();
        }
    }
}
#endif
