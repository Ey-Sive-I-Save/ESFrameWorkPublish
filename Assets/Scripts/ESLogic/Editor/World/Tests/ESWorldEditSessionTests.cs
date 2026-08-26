#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void PrefabPlacementWithoutFormalSceneBindingRemainsValid()
        {
            ESWorldMapPrefabPlacement placement = new ESWorldMapPrefabPlacement
            {
                placementId = "placement-unbound",
                prefabKey = "prefab-key"
            };

            Assert.IsTrue(placement.IsValid(out string error), error);
        }

        [Test]
        public void PrefabPlacementRejectsOneSidedFormalSceneBinding()
        {
            ESWorldMapPrefabPlacement placement = new ESWorldMapPrefabPlacement
            {
                placementId = "placement-partial",
                prefabKey = "prefab-key",
                formalScenePath = "Assets/Scenes/Formal.unity"
            };

            Assert.IsFalse(placement.IsValid(out string error));
            StringAssert.Contains("formalScenePath", error);
        }

        [Test]
        public void PrefabPlacementAcceptsCompleteFormalSceneBinding()
        {
            ESWorldMapPrefabPlacement placement = new ESWorldMapPrefabPlacement
            {
                placementId = "placement-bound",
                prefabKey = "prefab-key",
                formalScenePath = "Assets/Scenes/Formal.unity",
                formalObjectGlobalId = "GlobalObjectId_V1-1-2-3-4"
            };

            Assert.IsTrue(placement.IsValid(out string error), error);
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
        public void LegacyNullAuthoringContainersAreRepairedBeforeSessionUse()
        {
            source.Definition.prefabPlacements = null;
            source.Definition.regions = null;
            source.Definition.pois = null;
            source.Definition.navigation = null;
            source.Definition.streaming = null;
            source.Definition.heightfield = null;

            source.EnsureAuthoringContainers();
            session.ReloadFromSource();

            Assert.IsNotNull(source.Definition.prefabPlacements);
            Assert.IsNotNull(source.Definition.regions);
            Assert.IsNotNull(source.Definition.pois);
            Assert.IsNotNull(source.Definition.navigation);
            Assert.IsNotNull(source.Definition.streaming);
            Assert.IsNotNull(source.Definition.heightfield);
            Assert.IsNotNull(session.Draft.Definition.prefabPlacements);
            Assert.IsNotNull(session.Draft.Definition.navigation);
        }

        [Test]
        public void UntrackedMutationGateIgnoresBindingNoiseAndDetectsRealSerializedChanges()
        {
            Assert.IsFalse(session.HasUntrackedDraftMutation);

            session.Draft.Definition.seed += 1;
            Assert.IsTrue(session.HasUntrackedDraftMutation);

            session.NotifyDraftChanged("definition.seed");
            Assert.IsFalse(session.HasUntrackedDraftMutation);
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
        public void DisposePersistsUntrackedDraftMutationBeforeDestroyingTemporaryObject()
        {
            session.Draft.Definition.mapId = "untracked-recovered-draft";
            Assert.IsTrue(session.HasUntrackedDraftMutation);

            session.Dispose();
            session = ESWorldEditSession.Open(source);

            Assert.AreEqual("untracked-recovered-draft", session.Draft.Definition.mapId);
            Assert.IsTrue(session.IsDirty);
            CollectionAssert.Contains(session.ChangedPaths, "definition.mapId");
            Assert.AreEqual("source-map", source.Definition.mapId);
        }

        [Test]
        public void ExplicitRecoveryClearIsNotReintroducedByDispose()
        {
            session.Draft.Definition.mapId = "discarded-draft";
            session.NotifyDraftChanged("definition.mapId");
            session.ClearRecoveryState();
            session.Dispose();

            session = ESWorldEditSession.Open(source);

            Assert.AreEqual("source-map", session.Draft.Definition.mapId);
            Assert.IsFalse(session.IsDirty);
            Assert.Zero(session.ChangeCount);
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

        [Test]
        public void WindowScopedRecoveryKeepsConcurrentDraftsIsolated()
        {
            session.ClearRecoveryState();
            session.Dispose();
            session = null;
            ESWorldEditSession first = ESWorldEditSession.Open(source, "window-a");
            ESWorldEditSession second = ESWorldEditSession.Open(source, "window-b");
            try
            {
                first.Draft.Definition.mapId = "draft-a";
                first.NotifyDraftChanged("definition.mapId");
                second.Draft.Definition.mapId = "draft-b";
                second.NotifyDraftChanged("definition.mapId");
                first.Dispose();
                second.Dispose();

                first = ESWorldEditSession.Open(source, "window-a");
                second = ESWorldEditSession.Open(source, "window-b");

                Assert.AreEqual("draft-a", first.Draft.Definition.mapId);
                Assert.AreEqual("draft-b", second.Draft.Definition.mapId);
                Assert.AreEqual(2, ESWorldEditSession.GetActiveSessionCount(source));
            }
            finally
            {
                first?.ClearRecoveryState();
                second?.ClearRecoveryState();
                first?.Dispose();
                second?.Dispose();
            }
        }

        [Test]
        public void SuccessfulCommitImmediatelyInvalidatesOtherOpenWindow()
        {
            session.ClearRecoveryState();
            session.Dispose();
            session = null;
            ESWorldEditSession first = ESWorldEditSession.Open(source, "window-a");
            ESWorldEditSession second = ESWorldEditSession.Open(source, "window-b");
            try
            {
                first.Draft.Definition.mapId = "committed-by-a";
                first.NotifyDraftChanged("definition.mapId");

                ESWorldEditCommitResult commit = first.TryCommit();

                Assert.IsTrue(commit.success, commit.message);
                Assert.IsTrue(second.HasExternalConflict);
                Assert.AreEqual("window-a", second.ConflictOwnerSessionId);
                ESWorldEditCommitResult rejected = second.TryCommit();
                Assert.IsFalse(rejected.success);
                Assert.IsTrue(rejected.conflict);
            }
            finally
            {
                first?.ClearRecoveryState();
                second?.ClearRecoveryState();
                first?.Dispose();
                second?.Dispose();
            }
        }

        [Test]
        public void UndoRedoRebuildsChangeSetFromActualSerializedDifference()
        {
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(session.Draft, "修改地图身份");
            session.Draft.Definition.mapId = "draft-map";
            session.NotifyDraftChanged("definition.mapId");
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(session.Draft, "修改地图种子");
            session.Draft.Definition.seed = 99;
            session.NotifyDraftChanged("definition.seed");

            CollectionAssert.Contains(session.ChangedPaths, "definition.mapId");
            CollectionAssert.Contains(session.ChangedPaths, "definition.seed");

            Undo.PerformUndo();
            session.SynchronizeDraftAfterUndoRedo();

            Assert.AreEqual("draft-map", session.Draft.Definition.mapId);
            Assert.Zero(session.Draft.Definition.seed);
            CollectionAssert.Contains(session.ChangedPaths, "definition.mapId");
            CollectionAssert.DoesNotContain(session.ChangedPaths, "definition.seed");
        }

        [Test]
        public void ConsistencySnapshotProvesMemoryPersistenceAndDirtyStateAfterUndoRedo()
        {
            ESWorldEditSessionConsistencySnapshot initial = session.CaptureConsistencySnapshot();
            Assert.IsTrue(initial.Passed, initial.ToDiagnosticText());
            Assert.IsFalse(initial.IsDirty);
            Assert.AreEqual(1, initial.ActiveOwnerSessionIds.Count);

            Undo.RecordObject(session.Draft, "修改世界草稿一致性");
            session.Draft.Definition.mapId = "snapshot-draft";
            session.NotifyDraftChanged("definition.mapId");
            ESWorldEditSessionConsistencySnapshot changed = session.CaptureConsistencySnapshot();

            Assert.IsTrue(changed.Passed, changed.ToDiagnosticText());
            Assert.IsTrue(changed.IsDirty);
            Assert.AreEqual(
                1,
                changed.ChangeCount,
                "Unexpected changed paths:\n" + string.Join("\n", session.ChangedPaths));
            Assert.AreEqual(changed.DraftHash, changed.ActualDraftHash);

            Undo.PerformUndo();
            session.SynchronizeDraftAfterUndoRedo();
            ESWorldEditSessionConsistencySnapshot undone = session.CaptureConsistencySnapshot();

            Assert.IsTrue(undone.Passed, undone.ToDiagnosticText());
            Assert.IsFalse(undone.IsDirty);
            Assert.Zero(undone.ChangeCount);

            session.Dispose();
            session = ESWorldEditSession.Open(source);
            ESWorldEditSessionConsistencySnapshot restored = session.CaptureConsistencySnapshot();
            Assert.IsTrue(restored.Passed, restored.ToDiagnosticText());
            Assert.AreEqual("source-map", session.Draft.Definition.mapId);
        }

        [Test]
        public void ConflictSnapshotIdentifiesOwnersAndKeepsRejectAfterWriteConsistent()
        {
            session.ClearRecoveryState();
            session.Dispose();
            session = null;
            ESWorldEditSession first = ESWorldEditSession.Open(source, "window-a");
            ESWorldEditSession second = ESWorldEditSession.Open(source, "window-b");
            try
            {
                first.Draft.Definition.mapId = "committed-by-a";
                first.NotifyDraftChanged("definition.mapId");
                Assert.IsTrue(first.TryCommit().success);

                ESWorldEditSessionConsistencySnapshot snapshot = second.CaptureConsistencySnapshot();

                Assert.IsTrue(snapshot.Passed, snapshot.ToDiagnosticText());
                Assert.IsTrue(snapshot.HasExternalConflict);
                Assert.AreEqual("window-a", snapshot.ConflictOwnerSessionId);
                CollectionAssert.AreEquivalent(
                    new[] { "window-a", "window-b" }, snapshot.ActiveOwnerSessionIds);
                ESWorldEditCommitResult rejected = second.TryCommit();
                Assert.IsFalse(rejected.success);
                Assert.IsTrue(rejected.conflict);
            }
            finally
            {
                first?.ClearRecoveryState();
                second?.ClearRecoveryState();
                first?.Dispose();
                second?.Dispose();
            }
        }

        [Test]
        public void CommercialAcceptanceStateChecksUseIsolatedSpecimensAndPreserveCurrentSource()
        {
            string sourceMapId = source.Definition.mapId;
            string sourceHash = ESWorldEditSession.ComputeStateHash(source);

            ESWorldWorkbenchAcceptance.CaptureStateEvidenceForTest(
                session.Draft,
                session,
                out ESWorldAcceptanceCheckEvidence current,
                out ESWorldAcceptanceCheckEvidence undoRedo,
                out ESWorldAcceptanceCheckEvidence conflict);

            Assert.IsTrue(current.passed, current.diagnostic);
            Assert.IsTrue(undoRedo.passed, undoRedo.diagnostic);
            Assert.IsTrue(conflict.passed, conflict.diagnostic);
            Assert.IsNotNull(undoRedo.initialState);
            Assert.IsNotNull(undoRedo.changedState);
            Assert.IsNotNull(undoRedo.undoState);
            Assert.IsNotNull(undoRedo.redoState);
            Assert.IsNotNull(undoRedo.reopenedState);
            Assert.AreEqual(undoRedo.initialState.draftHash, undoRedo.undoState.draftHash);
            Assert.AreEqual(undoRedo.changedState.draftHash, undoRedo.redoState.draftHash);
            Assert.AreEqual(undoRedo.changedState.draftHash, undoRedo.reopenedState.draftHash);
            Assert.AreEqual(undoRedo.changedState.changeCount, undoRedo.redoState.changeCount);
            Assert.AreEqual(undoRedo.changedState.changeCount, undoRedo.reopenedState.changeCount);
            Assert.AreEqual(undoRedo.changedState.dirty, undoRedo.redoState.dirty);
            Assert.AreEqual(undoRedo.changedState.dirty, undoRedo.reopenedState.dirty);
            Assert.AreEqual(undoRedo.changedState.prefabGuid, undoRedo.redoState.prefabGuid);
            Assert.AreEqual(undoRedo.changedState.prefabGuid, undoRedo.reopenedState.prefabGuid);
            Assert.AreEqual(conflict.draftHashBeforeReject, conflict.draftHashAfterReject);
            Assert.AreEqual(conflict.sourceHashBeforeReject, conflict.sourceHashAfterReject);
            Assert.IsTrue(conflict.writeRejected);
            Assert.IsTrue(conflict.localDraftPreserved);
            Assert.IsTrue(conflict.sourcePreserved);
            Assert.AreEqual(sourceMapId, source.Definition.mapId);
            Assert.AreEqual(sourceHash, ESWorldEditSession.ComputeStateHash(source));
            Assert.IsTrue(session.CaptureConsistencySnapshot().Passed);
        }

        [Test]
        public void LiveWindowUndoRedoEvidenceRestoresHashChangeSetDirtySessionAndPrefabReference()
        {
            ESWorldMapAsset specimen = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            ESWorldEditSession liveSession = null;
            try
            {
                ESWorldBuilderWorkbenchWindow.PopulateCommercialValidationSample(specimen);
                liveSession = ESWorldEditSession.Open(specimen, "live-undo-window-test");
                string sourceHash = ESWorldEditSession.ComputeStateHash(specimen);
                string originalReference = liveSession.Draft.Definition
                    .prefabPlacements[0].editorPrefabGuid;

                ESWorldAcceptanceCheckEvidence evidence =
                    ESWorldWorkbenchAcceptance.CaptureLiveWindowUndoRedoForTest(liveSession);

                Assert.IsTrue(evidence.passed, evidence.diagnostic);
                Assert.AreEqual(sourceHash, ESWorldEditSession.ComputeStateHash(specimen));
                Assert.AreEqual(originalReference, liveSession.Draft.Definition
                    .prefabPlacements[0].editorPrefabGuid);
                Assert.IsTrue(liveSession.CaptureConsistencySnapshot().Passed);
                Assert.AreEqual(evidence.initialState.draftHash, evidence.undoState.draftHash);
                Assert.AreEqual(evidence.initialState.draftHash, evidence.restoredState.draftHash);
                Assert.AreEqual(evidence.changedState.draftHash, evidence.redoState.draftHash);
                Assert.AreEqual(evidence.initialState.changeCount, evidence.undoState.changeCount);
                Assert.AreEqual(evidence.initialState.changeCount, evidence.restoredState.changeCount);
                Assert.AreEqual(evidence.changedState.changeCount, evidence.redoState.changeCount);
                Assert.AreEqual(evidence.initialState.dirty, evidence.undoState.dirty);
                Assert.AreEqual(evidence.initialState.dirty, evidence.restoredState.dirty);
                Assert.AreEqual(evidence.changedState.dirty, evidence.redoState.dirty);
                Assert.AreEqual(evidence.initialState.prefabGuid, evidence.undoState.prefabGuid);
                Assert.AreEqual(evidence.initialState.prefabGuid, evidence.restoredState.prefabGuid);
                Assert.AreEqual(evidence.changedState.prefabGuid, evidence.redoState.prefabGuid);
                StringAssert.Contains("OriginalPrefabPath=Assets/", evidence.diagnostic);
                StringAssert.Contains("AlternatePrefabPath=Assets/", evidence.diagnostic);
            }
            finally
            {
                liveSession?.ClearRecoveryState();
                liveSession?.Dispose();
                UnityEngine.Object.DestroyImmediate(specimen);
            }
        }

        [UnityTest]
        public IEnumerator PersistentValidationSourceRunsSingleWindowConflictUndoRedoAndPreviewAcceptance()
        {
            RecordAcceptanceStage("01-test-start");
            ESWorldMapAsset validationSource = AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(
                ESWorldBuilderWorkbenchWindow.CommercialValidationAssetPath);
            Assert.IsNotNull(validationSource, "缺少固定 World 商业验收样本。");
            RecordAcceptanceStage("02-source-loaded");
            string sourceBefore = EditorJsonUtility.ToJson(validationSource);
            ESWorldBuilderWorkbenchWindow main = null;
            ESWorldEditSession peerSession = null;
            try
            {
                main = RunAcceptanceStage(
                    "创建 World 验收主窗口",
                    () => ScriptableObject.CreateInstance<ESWorldBuilderWorkbenchWindow>());
                RunAcceptanceStage("设置 World 验收主窗口标题",
                    () => main.titleContent = new GUIContent("ES World 验收主窗口"));
                RunAcceptanceStage("显示 World 验收主窗口", main.ShowUtility);
                RecordAcceptanceStage("03-main-shown");
                yield return null;
                RecordAcceptanceStage("05-first-editor-frame");

                RunAcceptanceStage("主窗口绑定固定验收资产",
                    () => main.BindAssetForTest(validationSource));
                peerSession = RunAcceptanceStage(
                    "创建 World 受管协作会话",
                    () => ESWorldEditSession.Open(validationSource, "world-commercial-acceptance-peer"));
                Assert.IsNotNull(peerSession);
                RecordAcceptanceStage("06-window-and-peer-session-ready");
                yield return null;
                yield return null;
                RecordAcceptanceStage("07-bound-layout-settled");

                ESWorldEditSession mainSession = main.EditSessionForTest;
                Assert.IsNotNull(mainSession);
                Assert.GreaterOrEqual(
                    ESWorldEditSession.GetActiveSessionCount(validationSource), 2);
                RecordAcceptanceStage("08-sessions-ready");

                RunAcceptanceStage("主窗口从 Source 重载草稿", mainSession.ReloadFromSource);
                RunAcceptanceStage("协作会话从 Source 重载草稿", peerSession.ReloadFromSource);
                Assert.IsNotNull(mainSession.Draft, "主窗口重载后草稿为空。");
                Assert.IsNotNull(mainSession.Draft.Definition, "主窗口重载后 World 定义为空。");
                Assert.IsNotNull(mainSession.Draft.Definition.prefabPlacements,
                    "主窗口重载后 Prefab 放置列表为空。");
                ESWorldMapPrefabPlacement localPlacement = RunAcceptanceStage(
                    "查询固定验收 Prefab 放置",
                    () => mainSession.Draft.Definition.prefabPlacements.Find(value => value != null
                        && value.placementId == "placement.commercial-validation"));
                Assert.IsNotNull(localPlacement);
                string alternateGuid = AssetDatabase.AssetPathToGUID(
                    "Assets/ESNormalAssets/Prefabs/蓝色方块.prefab");
                Assert.IsNotEmpty(alternateGuid);
                localPlacement.editorPrefabGuid = alternateGuid;
                localPlacement.prefabKey = "validation.prefab.alternate";
                mainSession.NotifyDraftChanged("definition.prefabPlacements");

                RunAcceptanceStage("协作会话修改并记录草稿", () =>
                {
                    peerSession.Draft.Definition.seed += 1;
                    peerSession.NotifyDraftChanged("definition.seed");
                });
                ESWorldEditCommitResult peerCommit = RunAcceptanceStage(
                    "协作会话提交 Source", peerSession.TryCommit);
                Assert.IsTrue(peerCommit.success, peerCommit.message);
                Assert.IsTrue(mainSession.RefreshExternalConflict());
                RecordAcceptanceStage("09-conflict-ready");

                ESWorldAcceptanceCheckEvidence liveUndoRedo =
                    ESWorldWorkbenchAcceptance.CaptureLiveWindowUndoRedoForTest(mainSession);
                Assert.IsTrue(liveUndoRedo.passed, liveUndoRedo.diagnostic);
                ESWorldAcceptanceCheckEvidence liveConflict =
                    ESWorldWorkbenchAcceptance.CaptureLiveWindowConflictForTest(mainSession);
                Assert.IsTrue(liveConflict.passed, liveConflict.diagnostic);
                Assert.IsTrue(liveConflict.writeRejected);
                Assert.IsTrue(liveConflict.localDraftPreserved);
                Assert.IsTrue(liveConflict.sourcePreserved);
                Assert.AreEqual(
                    liveConflict.draftHashBeforeReject,
                    liveConflict.draftHashAfterReject);
                Assert.AreEqual(
                    liveConflict.sourceHashBeforeReject,
                    liveConflict.sourceHashAfterReject);
                RecordAcceptanceStage("10-live-probes-passed");

                ESWorldWorkbenchAcceptanceResult result = RunAcceptanceStage(
                    "运行 World 专项验收", () => main.RunAcceptanceForTest(24));
                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(result.AutomatedChecksPassed, result.Message);
                Assert.IsFalse(result.Accepted,
                    "没有完整视觉矩阵和 Memory Profiler 快照时不得冒充最终验收通过。");
                Assert.IsTrue(File.Exists(result.ManifestPath), result.ManifestPath);
                ESWorldWorkbenchAcceptanceManifest manifest =
                    JsonUtility.FromJson<ESWorldWorkbenchAcceptanceManifest>(
                        File.ReadAllText(result.ManifestPath, System.Text.Encoding.UTF8));
                Assert.AreEqual(7, manifest.schemaVersion);
                Assert.AreEqual(
                    ESWorldBuilderWorkbenchWindow.CommercialValidationAssetPath,
                    manifest.sourceAssetPath);
                Assert.IsNotEmpty(manifest.sourceAssetGuid);
                Assert.AreEqual(manifest.sourceAssetPath, manifest.currentSession.sourceAssetPath);
                Assert.AreEqual(manifest.sourceAssetGuid, manifest.currentSession.sourceAssetGuid);
                Assert.IsNotEmpty(manifest.currentSession.ownerSessionId);
                Assert.IsNotEmpty(manifest.currentSession.activeOwnerSessionIds);
                Assert.IsNotNull(manifest.liveWindowUndoRedo.initialState);
                Assert.IsNotNull(manifest.liveWindowConflict.rejectedState);
                RecordAcceptanceStage("11-acceptance-complete");
            }
            finally
            {
                if (main != null) main.Close();
                peerSession?.ClearRecoveryState();
                peerSession?.Dispose();
                EditorJsonUtility.FromJsonOverwrite(sourceBefore, validationSource);
                EditorUtility.SetDirty(validationSource);
                AssetDatabase.SaveAssets();
            }
        }

        private static void RecordAcceptanceStage(string stage)
        {
            string directory = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "Library", "ESWorkbench", "Acceptance", "world"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "live-window-stage.txt"),
                System.DateTime.UtcNow.ToString("O") + "\n" + stage,
                new System.Text.UTF8Encoding(false));
        }

        private static void RunAcceptanceStage(string stage, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception exception)
            {
                throw new AssertionException(stage + "失败：\n" + exception);
            }
        }

        private static T RunAcceptanceStage<T>(string stage, System.Func<T> action)
        {
            try
            {
                return action();
            }
            catch (System.Exception exception)
            {
                throw new AssertionException(stage + "失败：\n" + exception);
            }
        }

        [Test]
        public void WorldAcceptanceIdentityRejectsOldUnityAssemblyOrDifferentSource()
        {
            ESWorldWorkbenchAcceptanceManifest CreateCurrent()
            {
                return new ESWorldWorkbenchAcceptanceManifest
                {
                    unityVersion = Application.unityVersion,
                    assemblyModuleVersionId = ESWorldWorkbenchAcceptance.CurrentAssemblyModuleVersionId,
                    sourceAssetPath = "Assets/World/Source.asset",
                    sourceAssetGuid = "source-guid-a"
                };
            }

            ESWorldWorkbenchAcceptanceManifest current = CreateCurrent();
            Assert.IsTrue(ESWorldWorkbenchAcceptance.HasCurrentArtifactIdentity(
                current, "source-guid-a"));

            ESWorldWorkbenchAcceptanceManifest oldSchema = CreateCurrent();
            oldSchema.schemaVersion = 6;
            Assert.IsFalse(ESWorldWorkbenchAcceptance.HasCurrentArtifactIdentity(oldSchema));

            ESWorldWorkbenchAcceptanceManifest oldUnity = CreateCurrent();
            oldUnity.unityVersion = "older-unity";
            Assert.IsFalse(ESWorldWorkbenchAcceptance.HasCurrentArtifactIdentity(oldUnity));

            ESWorldWorkbenchAcceptanceManifest oldAssembly = CreateCurrent();
            oldAssembly.assemblyModuleVersionId = System.Guid.Empty.ToString("D");
            Assert.IsFalse(ESWorldWorkbenchAcceptance.HasCurrentArtifactIdentity(oldAssembly));
            Assert.IsFalse(ESWorldWorkbenchAcceptance.HasCurrentArtifactIdentity(
                current, "source-guid-b"));
        }

        [Test]
        public void LiveWindowConflictEvidenceRequiresTwoActiveSessionsAndPreservesLocalDraft()
        {
            ESWorldEditSession peer = null;
            try
            {
                session.Draft.Definition.seed = 91;
                session.NotifyDraftChanged("definition.seed");
                peer = ESWorldEditSession.Open(source, "live-peer");
                peer.Draft.Definition.mapId = "live-peer-commit";
                peer.NotifyDraftChanged("definition.mapId");
                Assert.IsTrue(peer.TryCommit().success);

                ESWorldAcceptanceCheckEvidence evidence =
                    ESWorldWorkbenchAcceptance.CaptureLiveWindowConflictForTest(session);
                Assert.IsTrue(evidence.passed, evidence.diagnostic);
                Assert.GreaterOrEqual(evidence.activeSameSourceSessionCount, 2);
                Assert.IsTrue(evidence.externalConflictObserved);
                Assert.AreEqual("live-peer", evidence.conflictOwnerSessionId);
                Assert.IsTrue(evidence.writeAttempted);
                Assert.IsTrue(evidence.writeRejected);
                Assert.IsTrue(evidence.localDraftPreserved);
                Assert.IsTrue(evidence.sourcePreserved);
                Assert.AreEqual(evidence.draftHashBeforeReject, evidence.draftHashAfterReject);
                Assert.AreEqual(evidence.sourceHashBeforeReject, evidence.sourceHashAfterReject);
                Assert.IsNotNull(evidence.initialState);
                Assert.IsNotNull(evidence.rejectedState);
                Assert.AreEqual(evidence.initialState.draftHash, evidence.rejectedState.draftHash);
                Assert.AreEqual(evidence.initialState.sourceHash, evidence.rejectedState.sourceHash);
                StringAssert.Contains("其他窗口", evidence.writeResult);
                Assert.AreEqual(91, session.Draft.Definition.seed);
            }
            finally
            {
                peer?.ClearRecoveryState();
                peer?.Dispose();
            }
        }

        [Test]
        public void PreviewAcceptanceRecordsProfilerSnapshotAndReturnsToLifecycleBaseline()
        {
            ESWorldAcceptancePreviewEvidence evidence =
                ESWorldWorkbenchAcceptance.RunPreviewStress(source, 3, false, 0d);

            Assert.IsTrue(evidence.executed);
            Assert.IsTrue(evidence.passed, evidence.summary);
            Assert.AreEqual(3, evidence.completedIterations);
            Assert.Zero(evidence.minimumDurationSeconds);
            Assert.IsTrue(evidence.iterationRequirementPassed);
            Assert.IsTrue(evidence.durationRequirementPassed);
            Assert.IsTrue(evidence.lifecycleTrendStable);
            Assert.Greater(evidence.trendSampleCount, 0);
            Assert.IsTrue(evidence.samples.Any(sample => sample.phase == "Running"));
            Assert.IsTrue(evidence.samples.Any(sample => sample.phase == "AfterDispose"));
            Assert.Zero(evidence.activeScopeDelta);
            Assert.Zero(evidence.activeRenderContextDelta);
            Assert.Zero(evidence.activeResourceScopeDelta);
            Assert.Zero(evidence.activeModelGroupDelta);
            Assert.Zero(evidence.activeTemporaryObjectDelta);
            Assert.Zero(evidence.activeRenderTextureDelta);
            Assert.Zero(evidence.activeRenderTexturePixelDelta);
            Assert.Zero(evidence.estimatedRenderTextureByteDelta);
            Assert.Zero(evidence.cleanupFailureDelta);
            Assert.IsTrue(evidence.previewSceneObserved);
            Assert.IsTrue(evidence.cameraObserved);
            Assert.Greater(evidence.totalScopeRegistrationDelta, 0);
            Assert.AreEqual(evidence.totalScopeRegistrationDelta, evidence.totalScopeReleaseDelta);
            Assert.Greater(evidence.peakActiveRenderContextCount, 0);
            Assert.Greater(evidence.peakActiveTemporaryObjectCount, 0);
            Assert.Greater(evidence.peakActiveRenderTextureCount, 0);
            Assert.Greater(evidence.peakActiveRenderTexturePixels, 0);
            Assert.Greater(evidence.peakEstimatedRenderTextureBytes, 0);
            Assert.IsFalse(evidence.memoryProfilerCaptureAvailable);
            StringAssert.Contains("Memory Profiler", evidence.evidenceBoundary);
        }

        [Test]
        public void MemoryProfilerResolverSupportsCurrentUnitySnapshotApi()
        {
            Assert.IsTrue(
                ESWorldWorkbenchAcceptance.IsMemoryProfilerCaptureSupported(out string message),
                message);
            System.Type profilerType =
                ESWorldWorkbenchAcceptance.ResolveMemoryProfilerTypeForTest();
            Assert.IsNotNull(profilerType);
            StringAssert.Contains("MemoryProfiler", profilerType.FullName);

            System.Reflection.MethodInfo method =
                ESWorldWorkbenchAcceptance.ResolveTakeSnapshotMethodForTest(profilerType);
            Assert.IsNotNull(method);
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            Assert.GreaterOrEqual(parameters.Length, 2);
            Assert.AreEqual(typeof(string), parameters[0].ParameterType);
            Assert.IsTrue(System.Array.Exists(parameters,
                parameter => parameter.ParameterType == typeof(System.Action<string, bool>)));

            System.Action<string, bool> callback = (_, __) => { };
            object[] arguments = ESWorldWorkbenchAcceptance.BuildTakeSnapshotArgumentsForTest(
                method,
                "Temp/ESWorkbenchDiagnostics/world-memory-test.snap",
                callback);
            Assert.AreEqual(parameters.Length, arguments.Length);
            Assert.AreEqual("Temp/ESWorkbenchDiagnostics/world-memory-test.snap", arguments[0]);
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(System.Action<string, bool>))
                    Assert.AreSame(callback, arguments[i]);
                if (arguments[i] != null)
                    Assert.IsTrue(parameters[i].ParameterType.IsInstanceOfType(arguments[i]),
                        parameters[i].Name + " 参数类型不匹配。");
            }
        }

        [Test]
        public void CommercialCollaborationAcceptanceUsesFullPreviewStressCount()
        {
            Assert.AreEqual(
                240,
                ESWorldBuilderWorkbenchWindow.CommercialValidationPreviewIterations);
            Assert.AreEqual(
                30d,
                ESWorldBuilderWorkbenchWindow.CommercialValidationMinimumDurationSeconds);
            Assert.IsTrue(ESWorldWorkbenchAcceptance.MeetsPreviewStressRequirementsForTest(
                240, 240, 30d, 30d));
            Assert.IsFalse(ESWorldWorkbenchAcceptance.MeetsPreviewStressRequirementsForTest(
                240, 239, 30d, 30d));
            Assert.IsFalse(ESWorldWorkbenchAcceptance.MeetsPreviewStressRequirementsForTest(
                240, 240, 30d, 29.999d));
        }

        [Test]
        public void CommercialPreviewStressEnforcesRealMinimumDurationAndStableTrend()
        {
            ESWorldMapAsset validationSource = AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(
                ESWorldBuilderWorkbenchWindow.CommercialValidationAssetPath);
            Assert.IsNotNull(validationSource, "缺少固定 World 商业验收样本。");

            ESWorldAcceptancePreviewEvidence evidence =
                ESWorldWorkbenchAcceptance.RunPreviewStress(
                    validationSource,
                    ESWorldBuilderWorkbenchWindow.CommercialValidationPreviewIterations,
                    false,
                    ESWorldBuilderWorkbenchWindow.CommercialValidationMinimumDurationSeconds);

            Assert.IsTrue(evidence.passed, evidence.summary);
            Assert.IsTrue(evidence.iterationRequirementPassed);
            Assert.IsTrue(evidence.durationRequirementPassed);
            Assert.GreaterOrEqual(
                evidence.completedIterations,
                ESWorldBuilderWorkbenchWindow.CommercialValidationPreviewIterations);
            Assert.GreaterOrEqual(
                evidence.durationSeconds,
                ESWorldBuilderWorkbenchWindow.CommercialValidationMinimumDurationSeconds);
            Assert.IsTrue(evidence.lifecycleTrendStable);
            Assert.GreaterOrEqual(evidence.trendSampleCount, 2);
            Assert.IsTrue(evidence.samples.Any(sample => sample.phase == "AfterDispose"));
            Assert.Zero(evidence.activeScopeDelta);
            Assert.Zero(evidence.activeTemporaryObjectDelta);
            Assert.Zero(evidence.activeRenderTextureDelta);
            Assert.Zero(evidence.cleanupFailureDelta);
        }

        [TestCase("mapId", "地图 ID")]
        [TestCase("sourceMode", "来源模式")]
        [TestCase("worldMin", "世界最小坐标")]
        [TestCase("terrainDataAssetPath", "TerrainData 资产路径")]
        [TestCase("rotationEuler", "旋转角")]
        public void WorldInspectorUsesChineseAuthoringLabels(string propertyName, string expected)
        {
            Assert.AreEqual(
                expected,
                ESWorldBuilderWorkbenchWindow.ResolveWorldInspectorPropertyLabel(propertyName));
        }

        [Test]
        public void VisualMatrixCoversThemeScaleTierAndLongChineseStressCases()
        {
            var policy = new ESWorkbenchResponsiveLayoutPolicy(
                minimumWindowWidth: 760f,
                minimumWindowHeight: 560f,
                wideBreakpoint: 1160f,
                narrowBreakpoint: 820f,
                minimumCenterWidth: 420f);
            var matrix = policy.CreateCommercialVisualMatrix();
            ESWorkbenchResponsiveTier[] tiers =
            {
                ESWorkbenchResponsiveTier.Wide,
                ESWorkbenchResponsiveTier.Compact,
                ESWorkbenchResponsiveTier.Narrow
            };
            ESWorkbenchVisualTheme[] themes =
            {
                ESWorkbenchVisualTheme.Dark,
                ESWorkbenchVisualTheme.Light
            };
            float[] scales = { 1f, 1.25f, 1.5f, 2f };
            var scenarioIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < matrix.Count; i++)
                Assert.IsTrue(scenarioIds.Add(matrix[i].ScenarioId),
                    "视觉矩阵场景 ID 必须唯一：" + matrix[i].ScenarioId);

            Assert.AreEqual(48, matrix.Count);
            for (int tierIndex = 0; tierIndex < tiers.Length; tierIndex++)
            for (int themeIndex = 0; themeIndex < themes.Length; themeIndex++)
            for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
            for (int chineseIndex = 0; chineseIndex < 2; chineseIndex++)
            {
                bool longChinese = chineseIndex == 1;
                Assert.IsTrue(matrix.Any(item =>
                        item.ExpectedTier == tiers[tierIndex]
                        && item.Theme == themes[themeIndex]
                        && Mathf.Abs(item.PixelsPerPoint - scales[scaleIndex]) < 0.01f
                        && item.LongChineseContent == longChinese),
                    tiers[tierIndex] + " / " + themes[themeIndex] + " / "
                    + scales[scaleIndex] + "x / long-cn=" + longChinese);
            }
            ESWorkbenchVisualValidationResult current = policy.EvaluateVisualEnvironment(
                new ESWorkbenchVisualEnvironment(1440f, 900f, 720f, 2f, ESWorkbenchVisualTheme.Dark, true));
            Assert.IsTrue(current.LayoutContractPassed, current.Summary);
            ESWorkbenchVisualValidationScenario currentEnvironment = matrix.Single(item =>
                item.ScenarioId == "wide-dark-200-long-cn");
            ESWorkbenchVisualScenarioMatch currentMatch = policy.EvaluateScenario(
                new ESWorkbenchVisualEnvironment(
                    1416f, 682f, 986f, 2f, ESWorkbenchVisualTheme.Dark, true),
                currentEnvironment);
            Assert.IsTrue(currentMatch.Passed, currentMatch.Summary);
        }

        [Test]
        public void VisualEvidenceScenarioIdRecordsActualTierThemeScaleAndChineseState()
        {
            string scenario = ESWorkbenchVisualEvidenceCapture.BuildScenarioId(
                new ESWorkbenchVisualEnvironment(
                    760f, 560f, 420f, 2f, ESWorkbenchVisualTheme.Light, true),
                ESWorkbenchResponsiveTier.Narrow);

            Assert.AreEqual("narrow-light-200-long-cn", scenario);
        }

        [Test]
        public void PreviewResourceScopeRegistersWithGlobalLifecycleAndRejectsLateRegistration()
        {
            int before = ESEditorPreviewLifecycleHub.ActiveScopeCount;
            var scope = new ESEditorPreviewResourceScope("WorldTests");
            var texture = new Texture2D(4, 4)
            {
                name = "WorldTests Preview Texture",
                hideFlags = ESEditorPreviewUtility.PreviewHideFlags
            };
            try
            {
                Assert.AreEqual(before + 1, ESEditorPreviewLifecycleHub.ActiveScopeCount);
                scope.RegisterTexture(texture);
                Assert.AreEqual(1, scope.RegisteredObjectCount);
            }
            finally
            {
                scope.Dispose();
            }

            Assert.AreEqual(before, ESEditorPreviewLifecycleHub.ActiveScopeCount);
            Assert.IsTrue(scope.IsDisposed);
            Assert.IsTrue(texture == null);
            Assert.Throws<System.ObjectDisposedException>(() => scope.RegisterTexture(null));
        }

        [Test]
        public void PreviewResourceScopeRegistrationIsIdempotentForSameUnityObject()
        {
            var scope = new ESEditorPreviewResourceScope("WorldTests.Idempotent");
            var texture = new Texture2D(4, 4)
            {
                name = "WorldTests Idempotent Preview Texture",
                hideFlags = ESEditorPreviewUtility.PreviewHideFlags
            };
            try
            {
                Assert.AreSame(texture, scope.RegisterTexture(texture));
                Assert.AreSame(texture, scope.RegisterTexture(texture));
                Assert.AreEqual(1, scope.RegisteredObjectCount);
                Assert.AreEqual(0, scope.RegisteredRenderTextureCount);
            }
            finally
            {
                scope.Dispose();
            }

            Assert.IsTrue(texture == null);
        }

        [Test]
        public void PreviewResourceScopeRunsSameDisposeActionOnlyOnce()
        {
            var scope = new ESEditorPreviewResourceScope("WorldTests.DisposeAction");
            int disposeCount = 0;
            System.Action disposeAction = () => disposeCount++;
            try
            {
                scope.RegisterDisposeAction(disposeAction);
                scope.RegisterDisposeAction(disposeAction);
                Assert.AreEqual(1, scope.RegisteredDisposerCount);
            }
            finally
            {
                scope.Dispose();
            }

            Assert.AreEqual(1, disposeCount);
        }

        [Test]
        public void RepeatedWorldPreviewRebuildKeepsBoundedScopeAndReleasesOnDispose()
        {
            int before = ESEditorPreviewLifecycleHub.ActiveScopeCount;
            source.Definition.streaming.enabled = true;
            source.Definition.streaming.chunkRadius = 0;
            source.Definition.regions.Add(new ESWorldMapRegionDefinition
            {
                regionId = "mesh-guide",
                min = new Vector2(12f, 20f),
                max = new Vector2(52f, 68f)
            });
            source.Definition.prefabPlacements.Add(new ESWorldMapPrefabPlacement
            {
                placementId = "far",
                editorPrefabGuid = "missing-guid",
                enabled = true,
                position = new Vector3(1000f, 0f, 1000f)
            });
            var viewport = new ESWorldAuthoringViewport(_ => { }, null, false);
            try
            {
                viewport.Bind(source, true);
                int activeAfterFirstBuild = ESEditorPreviewLifecycleHub.ActiveScopeCount;
                Assert.AreEqual(before + 2, activeAfterFirstBuild);
                Assert.IsNotNull(viewport.PreviewContextForTest);
                Assert.IsTrue(viewport.PreviewContextForTest.IsReady);
                Assert.GreaterOrEqual(viewport.PreviewObjectCountForTest, 1);
                Assert.AreEqual(1, viewport.CulledPlacementCountForTest);
                Assert.AreEqual(1, viewport.RegionGuideCountForTest);
                Assert.IsTrue(viewport.TryGetRegionGuideMeshForTest(
                    "world.region.mesh-guide", out Mesh regionGuide));
                Assert.AreEqual(2, regionGuide.subMeshCount,
                    "区域应由 PreviewScene 内的填充与边界子网格渲染，不能回退为无裁剪 Handles。 ");
                Assert.That(regionGuide.bounds.size.x, Is.EqualTo(40f).Within(0.001f));
                Assert.That(regionGuide.bounds.size.z, Is.EqualTo(48f).Within(0.001f));
                Assert.Greater(regionGuide.vertexCount, 4,
                    "区域必须按地形细分贴地，不能只使用四角平面。 ");

                for (int i = 0; i < 8; i++) viewport.Rebuild(false);

                Assert.AreEqual(activeAfterFirstBuild, ESEditorPreviewLifecycleHub.ActiveScopeCount);
                Assert.AreEqual(1, viewport.CulledPlacementCountForTest);
                Assert.AreEqual(1, viewport.RegionGuideCountForTest);
            }
            finally
            {
                viewport.Dispose();
            }

            Assert.AreEqual(before, ESEditorPreviewLifecycleHub.ActiveScopeCount);
            Assert.IsTrue(viewport.ContentScopeDisposedForTest);
        }

        [Test]
        public void PreviewDiagnosticsTracksContextModelsRenderTexturePeakAndReleaseBaseline()
        {
            ESEditorPreviewDiagnosticsSnapshot before =
                ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            var sourceObject = new GameObject("World Preview Diagnostics Source");
            ESEditorPreviewRenderContext context = null;
            Texture2D frame = null;
            try
            {
                context = new ESEditorPreviewRenderContext(
                    "WorldPreviewDiagnosticsTests",
                    ESEditorPreviewSceneMode.PreviewScene);
                ESEditorPreviewModelHandle handle = context.CreateModelGroup(sourceObject);
                Assert.IsNotNull(handle);
                frame = context.Snapshot(
                    640,
                    360,
                    new ESEditorPreviewCameraPose(context.GroupOrigin, 2f, 35f, 20f, 1f),
                    ESEditorPreviewQuality.Balanced,
                    "World Preview Diagnostics Frame");
                Assert.IsNotNull(frame);

                ESEditorPreviewDiagnosticsSnapshot active =
                    ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
                Assert.AreEqual(before.ActiveScopeCount + 1, active.ActiveScopeCount);
                Assert.AreEqual(before.ActiveRenderContextCount + 1, active.ActiveRenderContextCount);
                Assert.GreaterOrEqual(active.ActiveModelGroupCount, before.ActiveModelGroupCount + 1);
                Assert.GreaterOrEqual(active.ActiveRenderTextureCount, before.ActiveRenderTextureCount + 1);
                Assert.Greater(active.ActiveRenderTexturePixels, before.ActiveRenderTexturePixels);
                Assert.Greater(active.EstimatedRenderTextureBytes, before.EstimatedRenderTextureBytes);
                Assert.GreaterOrEqual(active.PeakRenderTexturePixels, active.ActiveRenderTexturePixels);
            }
            finally
            {
                if (frame != null) Object.DestroyImmediate(frame);
                context?.Dispose();
                Object.DestroyImmediate(sourceObject);
            }

            ESEditorPreviewDiagnosticsSnapshot after =
                ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            Assert.AreEqual(before.ActiveScopeCount, after.ActiveScopeCount);
            Assert.AreEqual(before.ActiveRenderContextCount, after.ActiveRenderContextCount);
            Assert.AreEqual(before.ActiveRenderTextureCount, after.ActiveRenderTextureCount);
            Assert.AreEqual(before.ActiveRenderTexturePixels, after.ActiveRenderTexturePixels);
            Assert.AreEqual(before.EstimatedRenderTextureBytes, after.EstimatedRenderTextureBytes);
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

        [Test]
        public void FormalOutputPathPreflightDerivesStableNavigationAsset()
        {
            bool valid = ESWorldMapTerrainEditorFacade.TryValidateOutputPaths(
                source,
                "Assets/Generated/Test_Terrain.asset",
                "Assets/Generated/Test.unity",
                out string navigationPath,
                out string error);

            Assert.IsTrue(valid, error);
            Assert.AreEqual("Assets/Generated/Test_NavMesh.asset", navigationPath);
        }

        [TestCase("../Outside.asset", "Assets/Generated/Test.unity")]
        [TestCase("Assets/Generated/Test.txt", "Assets/Generated/Test.unity")]
        [TestCase("Assets/Generated/Test.asset", "../Outside.unity")]
        [TestCase("Assets/Generated/Test.asset", "Assets/Generated/Test.prefab")]
        public void FormalOutputPathPreflightRejectsUnsafeOrWrongTypedTargets(
            string terrainPath,
            string scenePath)
        {
            bool valid = ESWorldMapTerrainEditorFacade.TryValidateOutputPaths(
                source, terrainPath, scenePath, out _, out string error);

            Assert.IsFalse(valid);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TerrainBrushPaintsRadiusWithStrongCenterAndProtectedOutsideSamples()
        {
            ESWorldMapDefinition definition = source.Definition;
            definition.terrainMode = ESWorldMapTerrainMode.UnityTerrain;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(32f, 32f);
            definition.heightfield.width = 33;
            definition.heightfield.height = 33;
            definition.heightfield.defaultHeight = 0f;
            definition.heightfield.samples.Clear();
            definition.heightfield.EnsureSamples();

            bool painted = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                definition,
                new Vector2(16f, 16f),
                definition.worldMin,
                definition.worldMax,
                1f,
                4f,
                0.5f,
                0.75f,
                out string error);

            Assert.IsTrue(painted, error);
            Assert.That(definition.heightfield.Get(16, 16), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(definition.heightfield.Get(18, 16), Is.GreaterThan(0f));
            Assert.That(definition.heightfield.Get(18, 16), Is.LessThan(0.5f));
            Assert.That(definition.heightfield.Get(21, 16), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TerrainBrushClipsSafelyAtWorldBoundary()
        {
            ESWorldMapDefinition definition = source.Definition;
            definition.terrainMode = ESWorldMapTerrainMode.Heightfield;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(32f, 32f);
            definition.heightfield.width = 33;
            definition.heightfield.height = 33;
            definition.heightfield.defaultHeight = 0f;
            definition.heightfield.samples.Clear();
            definition.heightfield.EnsureSamples();

            Assert.DoesNotThrow(() =>
            {
                bool painted = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                    definition,
                    Vector2.zero,
                    definition.worldMin,
                    definition.worldMax,
                    0.8f,
                    6f,
                    1f,
                    0f,
                    out string error);
                Assert.IsTrue(painted, error);
            });
            Assert.That(definition.heightfield.Get(0, 0), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(definition.heightfield.Get(32, 32), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TerrainBrushKeepsWorldSpaceRadiusOnNonSquareMaps()
        {
            ESWorldMapDefinition definition = source.Definition;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(64f, 32f);
            definition.heightfield.width = 65;
            definition.heightfield.height = 65;
            definition.heightfield.defaultHeight = 0f;
            definition.heightfield.samples.Clear();
            definition.heightfield.EnsureSamples();

            bool painted = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                definition,
                new Vector2(32f, 16f),
                definition.worldMin,
                definition.worldMax,
                1f,
                4f,
                1f,
                0f,
                out string error);

            Assert.IsTrue(painted, error);
            Assert.That(definition.heightfield.Get(35, 32), Is.GreaterThan(0f), "X 轴三米应位于笔刷内。");
            Assert.That(definition.heightfield.Get(32, 39), Is.GreaterThan(0f), "Z 轴三点五米应位于笔刷内。");
            Assert.That(definition.heightfield.Get(37, 32), Is.EqualTo(0f).Within(0.0001f), "X 轴五米应位于笔刷外。");
            Assert.That(definition.heightfield.Get(32, 41), Is.EqualTo(0f).Within(0.0001f), "Z 轴四点五米应位于笔刷外。");
        }

        [Test]
        public void TerrainBrushRejectsOutOfBoundsPointWithoutPaintingClampedEdge()
        {
            ESWorldMapDefinition definition = source.Definition;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(32f, 32f);
            definition.heightfield.width = 33;
            definition.heightfield.height = 33;
            definition.heightfield.defaultHeight = 0f;
            definition.heightfield.samples.Clear();
            definition.heightfield.EnsureSamples();

            bool painted = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                definition,
                new Vector2(-0.1f, 16f),
                definition.worldMin,
                definition.worldMax,
                1f,
                8f,
                1f,
                0f,
                out string error);

            Assert.IsFalse(painted);
            StringAssert.Contains("地图范围", error);
            Assert.That(definition.heightfield.Get(0, 16), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TerrainBrushModesHaveDeterministicRaiseLowerAndSmoothSemantics()
        {
            ESWorldMapDefinition definition = source.Definition;
            definition.terrainMode = ESWorldMapTerrainMode.Heightfield;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(32f, 32f);
            definition.heightfield.width = 33;
            definition.heightfield.height = 33;
            definition.heightfield.defaultHeight = 0f;
            definition.heightfield.samples.Clear();
            definition.heightfield.EnsureSamples();

            bool raised = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                definition, new Vector2(16f, 16f), definition.worldMin, definition.worldMax,
                0f, 4f, 0.5f, 0.5f, ESWorldTerrainBrushMode.Raise, out string raiseError);
            Assert.IsTrue(raised, raiseError);
            Assert.That(definition.heightfield.Get(16, 16), Is.EqualTo(0.5f).Within(0.0001f));
            float raisedCenter = definition.heightfield.Get(16, 16);

            bool lowered = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                definition, new Vector2(16f, 16f), definition.worldMin, definition.worldMax,
                0f, 4f, 0.25f, 0.5f, ESWorldTerrainBrushMode.Lower, out string lowerError);
            Assert.IsTrue(lowered, lowerError);
            Assert.That(definition.heightfield.Get(16, 16), Is.EqualTo(raisedCenter * 0.75f).Within(0.0001f));

            for (int y = 0; y < definition.heightfield.height; y++)
                for (int x = 0; x < definition.heightfield.width; x++)
                    definition.heightfield.Set(x, y, 0f);
            definition.heightfield.Set(16, 16, 1f);
            bool smoothed = ESWorldMapTerrainEditorFacade.TryPaintHeight(
                definition, new Vector2(16f, 16f), definition.worldMin, definition.worldMax,
                0f, 4f, 1f, 0f, ESWorldTerrainBrushMode.Smooth, out string smoothError);
            Assert.IsTrue(smoothed, smoothError);
            Assert.That(definition.heightfield.Get(16, 16), Is.LessThan(1f));
            Assert.That(definition.heightfield.Get(18, 16), Is.GreaterThan(0f));
            Assert.That(definition.heightfield.Get(15, 16),
                Is.EqualTo(definition.heightfield.Get(17, 16)).Within(0.0001f));
            Assert.That(definition.heightfield.Get(16, 15),
                Is.EqualTo(definition.heightfield.Get(16, 17)).Within(0.0001f));
            Assert.That(definition.heightfield.Get(25, 16), Is.EqualTo(0f).Within(0.0001f));
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
