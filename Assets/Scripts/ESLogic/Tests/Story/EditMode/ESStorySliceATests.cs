using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class ESStorySliceATests
    {
        private sealed class CapturingPresenter : IESStoryDialoguePresenter
        {
            public ESDialogueViewData LastView;
            public int CloseCount;
            public int ShowCount;

            public void Show(ESDialogueViewData view) { LastView = view; ShowCount++; }
            public void Close(string storyInstanceId, string sessionId, int sessionGeneration) { CloseCount++; }
        }

        private sealed class StorySaveHooks
        {
            public ESGameSaveValidateCandidateHandler validate;
            public ESGameSavePrepareCandidateHandler prepare;
            public ESGameSaveCommitCandidateHandler commit;
            public ESGameSaveRollbackCandidateHandler rollback;
            public ESGameSaveFinalizeCandidateHandler finalize;
        }

        private sealed class ReentrantInteractable : ESInteractable
        {
            public EntityBasicInteractionModule module;
            public int endedCount;
            public bool reentrantEndAccepted;

            public override void OnInteractEnded(Entity entity, bool success, ESInteractionEndReason reason)
            {
                endedCount++;
                reentrantEndAccepted = module.TryEndExternalInteraction(default, false, reason);
            }
        }

        [SetUp]
        public void ResetStoryCatalogBeforeTest()
        {
            ESStoryDefinitionCatalog.AbortBuild();
            ESStoryDefinitionCatalog.Clear();
        }

        [TearDown]
        public void ResetStoryCatalogAfterTest()
        {
            ESStoryDefinitionCatalog.AbortBuild();
            ESStoryDefinitionCatalog.Clear();
        }

        [Test]
        public void RuntimeModeLease_OldGenerationCannotReleaseNewRequest()
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            object oldOwner = new object();
            ESRuntimeModeLease oldLease = service.AcquireModeLease(ESRuntimeMode.Dialogue, oldOwner);
            service.Clear();
            ESRuntimeModeLease currentLease = service.AcquireModeLease(ESRuntimeMode.PauseMenu, new object());

            Assert.That(oldLease.Release(), Is.False);
            Assert.That(service.ModeCount, Is.EqualTo(1));
            Assert.That(service.CurrentMode, Is.EqualTo(ESRuntimeMode.PauseMenu));
            Assert.That(currentLease.Release(), Is.True);
            Assert.That(service.ModeCount, Is.Zero);
        }

        [Test]
        public void RuntimeMode_LeaseOwnedEntriesRejectLegacyRemovalPaths()
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            object owner = new object();
            ESRuntimeModeLease lease = service.AcquireModeLease(ESRuntimeMode.Dialogue, owner);

            Assert.That(service.GetModeEntryAt(0).ownershipKind, Is.EqualTo(ESRuntimeModeOwnershipKind.LeaseOwned));
            Assert.That(service.RemoveMostRecentMode(ESRuntimeMode.Dialogue), Is.False);
            LogAssert.Expect(LogType.Warning, "RuntimeMode 拒绝不安全操作 PopTopMode，目标所有权为 LeaseOwned。");
            Assert.That(service.PopTopMode(), Is.False);
            Assert.That(service.ReleaseModesByOwner(owner), Is.Zero);
            Assert.That(service.ModeCount, Is.EqualTo(1));
            Assert.That(lease.Release(), Is.True);
        }

        [Test]
        public void RuntimeMode_LegacyCommandCannotDeleteStoryLease()
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            ESRuntimeModeLease lease = service.AcquireModeLease(ESRuntimeMode.Dialogue, new object());
            ESCommandServices.SetRuntimeMode(service);
            LogAssert.Expect(LogType.Warning, "RuntimeMode ESCommand 已冻结，拒绝执行无实例所有权的命令：移除运行模式");
            new ESCommand_RuntimeMode_RemoveMode { mode = ESRuntimeMode.Dialogue }.Invoke();
            Assert.That(service.ModeCount, Is.EqualTo(1));
            Assert.That(lease.Release(), Is.True);
        }

        [Test]
        public void RuntimeMode_TagHandleRequiresHostGenerationOwnerAndOwnership()
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            object owner = new object();
            ESRuntimeModeTagHandle authorized = service.AddTag(ESRuntimeModeTag.Aiming, owner);
            ESRuntimeModeTagHandle forged = new ESRuntimeModeTagHandle { id = authorized.id };
            LogAssert.Expect(LogType.Warning, "RuntimeMode 拒绝无授权或旧代 Tag Handle 删除。");
            Assert.That(service.RemoveTag(forged), Is.False);
            LogAssert.Expect(LogType.Warning, "RuntimeMode 拒绝无授权或旧代 Tag Handle 删除。");
            Assert.That(new ESRuntimeModeService().RemoveTag(authorized), Is.False);
            Assert.That(service.ContainsTag(ESRuntimeModeTag.Aiming), Is.True);
            Assert.That(service.RemoveTag(authorized), Is.True);

            ESRuntimeModeTagHandle unowned = service.AddTag(ESRuntimeModeTag.NetworkBusy);
            LogAssert.Expect(LogType.Warning, "RuntimeMode 拒绝无授权或旧代 Tag Handle 删除。");
            Assert.That(service.RemoveTag(unowned), Is.False);
            Assert.That(service.RemoveMostRecentTag(ESRuntimeModeTag.NetworkBusy), Is.True);

            ESRuntimeModeTagHandle oldGeneration = service.AddTag(ESRuntimeModeTag.Combat, owner);
            service.Clear();
            LogAssert.Expect(LogType.Warning, "RuntimeMode 拒绝无授权或旧代 Tag Handle 删除。");
            Assert.That(service.RemoveTag(oldGeneration), Is.False);
        }

        [Test]
        public void RuntimeMode_ActiveSetBatchCommitsOnce()
        {
            ESRuntimeModeService service = new ESRuntimeModeService();
            int before = service.CommitVersion;
            service.BeginActiveSetUpdate();
            service.PushMode(ESRuntimeMode.Dialogue);
            service.AddTag(ESRuntimeModeTag.NetworkBusy);
            Assert.That(service.IsPolicyDirty, Is.True);
            Assert.That(service.CommitVersion, Is.EqualTo(before));
            service.EndActiveSetUpdate();
            Assert.That(service.IsPolicyDirty, Is.False);
            Assert.That(service.CommitVersion, Is.EqualTo(before + 1));
        }

        [Test]
        public void Interaction_EndCallbackCannotReenterSameCleanup()
        {
            GameObject target = new GameObject("reentrant-interactable");
            ReentrantInteractable interactable = target.AddComponent<ReentrantInteractable>();
            EntityBasicInteractionModule module = new EntityBasicInteractionModule
            {
                isInteracting = true,
                activeInteractable = interactable
            };
            interactable.module = module;
            MethodInfo end = typeof(EntityBasicInteractionModule).GetMethod("EndInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(end, Is.Not.Null);
            end.Invoke(module, new object[] { false, ESInteractionEndReason.UserCancelled });
            Assert.That(interactable.endedCount, Is.EqualTo(1));
            Assert.That(interactable.reentrantEndAccepted, Is.False);
            Assert.That(module.isInteracting, Is.False);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void Snapshot_IsDetachedFromAuthoringLists()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            Assert.That(ESStoryDefinitionSnapshot.TryBake(definition, out ESStoryDefinitionSnapshot snapshot, out string error), Is.True, error);
            definition.nodes[0].nextNodeId = "changed";
            definition.nodes[1].text = "changed";

            Assert.That(snapshot.TryGetNode("start", out ESStoryNodeSnapshot start), Is.True);
            Assert.That(start.NextNodeId, Is.EqualTo("line"));
            Assert.That(snapshot.TryGetNode("line", out ESStoryNodeSnapshot line), Is.True);
            Assert.That(line.Text, Is.EqualTo("hello"));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Validator_AllowsWaitingLoopButRejectsUnreachableNode()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            definition.nodes[1].nextNodeId = "line";
            definition.nodes.Add(new ESStoryNodeDefinition { nodeId = "orphan", nodeKind = ESStoryNodeKind.Complete });
            List<ESStoryValidationIssue> issues = ESStoryDefinitionValidator.Validate(definition);

            Assert.That(issues.Exists(x => x.code == "Graph.LoopAllowed"), Is.True);
            Assert.That(issues.Exists(x => x.code == "Graph.Unreachable" && x.nodeId == "orphan"), Is.True);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void ContentSignature_DoesNotDependOnNodeListOrder()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            string before = definition.ContentSignature;
            definition.nodes.Reverse();
            string after = definition.ContentSignature;
            Assert.That(after, Is.EqualTo(before));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void ContentSignature_ChangesWhenOptionDisplayOrderChanges()
        {
            ESStoryDefinitionDataInfo definition = CreateChoiceDefinition();
            string before = definition.ContentSignature;
            definition.nodes[1].options.Reverse();
            string after = definition.ContentSignature;

            Assert.That(after, Is.Not.EqualTo(before));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void ContentSignature_UsesUnambiguousFieldEncoding()
        {
            ESStoryDefinitionDataInfo first = CreateDialogueDefinition();
            ESStoryDefinitionDataInfo second = CreateDialogueDefinition();
            first.nodes[1].speakerName = "a|b";
            first.nodes[1].text = "c:d";
            second.nodes[1].speakerName = "a";
            second.nodes[1].text = "b|c:d";
            Assert.That(first.ContentSignature, Is.Not.EqualTo(second.ContentSignature));
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void LegacyLocalizationSignature_IgnoresUnusedStableTextReferences()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            string before = definition.ContentSignature;
            definition.nodes[1].bodyText = new ESLocalizedTextRef("story.unused", fallbackLiteral: "未使用");
            Assert.That(definition.ContentSignature, Is.EqualTo(before));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void StableLocalization_RequiresKeysAndSignsOnlyActiveLocalizedContent()
        {
            ESStoryDefinitionDataInfo definition = CreateStableDialogueDefinition();
            Assert.That(ESStoryDefinitionValidator.Validate(definition).Exists(x => x.severity == ESStoryValidationSeverity.Error), Is.False);
            string before = definition.ContentSignature;

            definition.nodes[1].text = "旧字段不参与稳定合同";
            Assert.That(definition.ContentSignature, Is.EqualTo(before));
            definition.nodes[1].bodyText = new ESLocalizedTextRef("story.line.changed", fallbackLiteral: "你好");
            Assert.That(definition.ContentSignature, Is.Not.EqualTo(before));

            definition.nodes[1].bodyText = default;
            Assert.That(ESStoryDefinitionValidator.Validate(definition)
                .Exists(x => x.code == "Localization.BodyKey" && x.severity == ESStoryValidationSeverity.Error), Is.True);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void LocaleChange_RefreshesForegroundWithoutInvalidatingSubmissionGeneration()
        {
            IESLocalizationProvider originalProvider = ESLocalizationRuntime.Provider;
            EnumCollect.Envir_LanguageType originalLanguage = ESLocalizationRuntime.CurrentLanguage;
            if (originalProvider != null) Assert.That(ESLocalizationRuntime.UnregisterProvider(originalProvider), Is.True);

            var provider = new ESInMemoryLocalizationProvider();
            Assert.That(provider.Set("story.speaker", EnumCollect.Envir_LanguageType.ChineseSimplified, "向导"), Is.True);
            Assert.That(provider.Set("story.speaker", EnumCollect.Envir_LanguageType.English, "Guide"), Is.True);
            Assert.That(provider.Set("story.line", EnumCollect.Envir_LanguageType.ChineseSimplified, "你好"), Is.True);
            Assert.That(provider.Set("story.line", EnumCollect.Envir_LanguageType.English, "Hello"), Is.True);
            Assert.That(ESLocalizationRuntime.RegisterProvider(provider), Is.True);
            ESLocalizationRuntime.SetCurrentLanguageOrThrow(EnumCollect.Envir_LanguageType.ChineseSimplified);

            ESStoryDefinitionDataInfo definition = CreateStableDialogueDefinition();
            ESStoryDefinitionCatalog.Inject(definition);
            ESStoryModule story = new ESStoryModule();
            CapturingPresenter presenter = new CapturingPresenter();
            story.BindPresenter(presenter);
            InvokeStoryLocalizationSubscription(story);
            GameObject actorObject = new GameObject("localized-story-actor");
            Entity actor = actorObject.AddComponent<Entity>();
            GameObject targetObject = new GameObject("localized-story-target");
            ESInteractable target = targetObject.AddComponent<ESInteractable>();
            ESInteractionBinding binding = new ESInteractionBinding(31, 7, actor, target);

            try
            {
                Assert.That(story.TryStartFromInteraction(definition.definitionId, actor, binding, out _, out string error), Is.True, error);
                ESDialogueViewData chinese = presenter.LastView;
                Assert.That(chinese.text, Is.EqualTo("你好"));
                Assert.That(chinese.speakerName, Is.EqualTo("向导"));
                Assert.That(chinese.bodyResolveStatus, Is.EqualTo(ESLocalizationResolveStatus.Resolved));

                ESLocalizationRuntime.SetCurrentLanguageOrThrow(EnumCollect.Envir_LanguageType.English);
                ESDialogueViewData english = presenter.LastView;
                Assert.That(presenter.ShowCount, Is.EqualTo(2));
                Assert.That(english.text, Is.EqualTo("Hello"));
                Assert.That(english.speakerName, Is.EqualTo("Guide"));
                Assert.That(english.instanceRevision, Is.EqualTo(chinese.instanceRevision));
                Assert.That(english.viewRevision, Is.EqualTo(chinese.viewRevision));
                Assert.That(story.SubmitContinue(new ESStoryViewSubmission(chinese.storyInstanceId,
                    chinese.instanceRevision, chinese.sessionId, chinese.sessionGeneration, chinese.viewRevision)), Is.True);
            }
            finally
            {
                story.OnDestroy();
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(definition);
                if (ReferenceEquals(ESLocalizationRuntime.Provider, provider))
                    ESLocalizationRuntime.UnregisterProvider(provider);
                ESLocalizationRuntime.SetCurrentLanguageOrThrow(originalLanguage);
                if (originalProvider != null)
                    Assert.That(ESLocalizationRuntime.RegisterProvider(originalProvider), Is.True);
            }
        }

        [Test]
        public void StoryCatalog_FailedReplacementKeepsPublishedGeneration()
        {
            ESStoryDefinitionDataInfo original = CreateDialogueDefinition();
            original.storyKind = ESStoryKind.Dialogue;
            ESStoryDefinitionCatalog.Inject(original);
            Assert.That(ESStoryDefinitionCatalog.TryResolve(original.definitionId, out ESStoryDefinitionSnapshot published), Is.True);

            ESStoryDefinitionDataInfo invalidReplacement = CreateDialogueDefinition();
            invalidReplacement.storyKind = ESStoryKind.Dialogue;
            invalidReplacement.contentVersion = 2;
            invalidReplacement.nodes[0].nextNodeId = "missing";
            ESStoryDefinitionCatalog.BeginBuild(true);
            Assert.Throws<System.InvalidOperationException>(() => ESStoryDefinitionCatalog.Inject(invalidReplacement));
            ESStoryDefinitionCatalog.AbortBuild();

            Assert.That(ESStoryDefinitionCatalog.TryResolve(original.definitionId, out ESStoryDefinitionSnapshot afterFailure), Is.True);
            Assert.That(afterFailure, Is.SameAs(published));
            Object.DestroyImmediate(invalidReplacement);
            Object.DestroyImmediate(original);
        }

        [Test]
        public void StoryCatalog_PublishesReplacementOnlyWhenBuildCommits()
        {
            ESStoryDefinitionDataInfo original = CreateDialogueDefinition();
            original.storyKind = ESStoryKind.Dialogue;
            ESStoryDefinitionCatalog.Inject(original);
            Assert.That(ESStoryDefinitionCatalog.TryResolve(original.definitionId, out ESStoryDefinitionSnapshot first), Is.True);

            ESStoryDefinitionDataInfo replacement = CreateDialogueDefinition();
            replacement.storyKind = ESStoryKind.Dialogue;
            replacement.contentVersion = 2;
            replacement.nodes[1].text = "replacement";
            ESStoryDefinitionCatalog.BeginBuild(true);
            ESStoryDefinitionCatalog.Inject(replacement);
            Assert.That(ESStoryDefinitionCatalog.TryResolve(original.definitionId, out ESStoryDefinitionSnapshot duringBuild), Is.True);
            Assert.That(duringBuild, Is.SameAs(first));
            ESStoryDefinitionCatalog.EndBuild();

            Assert.That(ESStoryDefinitionCatalog.TryResolve(replacement.definitionId, out ESStoryDefinitionSnapshot committed), Is.True);
            Assert.That(committed.ContentVersion, Is.EqualTo(2));
            Assert.That(committed.TryGetNode("line", out ESStoryNodeSnapshot line), Is.True);
            Assert.That(line.Text, Is.EqualTo("replacement"));
            Object.DestroyImmediate(replacement);
            Object.DestroyImmediate(original);
        }

        [Test]
        public void StoryCatalog_RepeatedEquivalentInjectionIsIdempotent()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            definition.storyKind = ESStoryKind.Dialogue;
            ESStoryDefinitionCatalog.Inject(definition);
            Assert.That(ESStoryDefinitionCatalog.TryResolve(definition.definitionId, out ESStoryDefinitionSnapshot first), Is.True);

            ESStoryDefinitionCatalog.Inject(definition);

            Assert.That(ESStoryDefinitionCatalog.IsBuilding, Is.False);
            Assert.That(ESStoryDefinitionCatalog.Count, Is.EqualTo(1));
            Assert.That(ESStoryDefinitionCatalog.TryResolve(definition.definitionId, out ESStoryDefinitionSnapshot second), Is.True);
            Assert.That(second, Is.SameAs(first));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void StoryStart_RequiresPublishedCatalogAndNeverRebakesAuthoringData()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            definition.storyKind = ESStoryKind.Dialogue;
            ESStoryModule story = new ESStoryModule();
            CapturingPresenter presenter = new CapturingPresenter();
            story.BindPresenter(presenter);
            GameObject actorObject = new GameObject("story-catalog-actor");
            Entity actor = actorObject.AddComponent<Entity>();
            GameObject targetObject = new GameObject("story-catalog-target");
            ESInteractable target = targetObject.AddComponent<ESInteractable>();
            ESInteractionBinding binding = new ESInteractionBinding(23, 4, actor, target);

            Assert.That(story.TryStartFromInteraction(definition.definitionId, actor, binding, out _, out string missingError), Is.False);
            Assert.That(missingError, Does.Contain("尚未完成验证并注入"));

            ESStoryDefinitionCatalog.Inject(definition);
            definition.nodes[0].nextNodeId = "missing-after-publish";
            definition.nodes[1].text = "mutable-authoring-change";
            Assert.That(story.TryStartFromInteraction(definition.definitionId, actor, binding, out string instanceId, out string error), Is.True, error);
            Assert.That(instanceId, Is.Not.Empty);
            Assert.That(presenter.LastView, Is.Not.Null);
            Assert.That(presenter.LastView.text, Is.EqualTo("hello"));

            story.OnDestroy();
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void RuntimeGuard_RejectsDuplicateQuestAndLateSessionGeneration()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            definition.storyKind = ESStoryKind.Quest;
            Assert.That(ESStoryDefinitionSnapshot.TryBake(definition, out ESStoryDefinitionSnapshot snapshot, out string error), Is.True, error);
            ESStoryInstance instance = new ESStoryInstance();
            SetInstanceProperty(instance, "Definition", snapshot);
            SetInstanceProperty(instance, "RunState", ESStoryRunState.WaitingForUI);
            SetInstanceProperty(instance, "Revision", 8L);
            SetInstanceProperty(instance, "SessionId", "session");
            SetInstanceProperty(instance, "SessionGeneration", 3);
            SetInstanceProperty(instance, "ViewRevision", 5L);
            Assert.That(ESStoryRuntimeGuard.HasActiveQuest(new[] { instance }, snapshot.DefinitionId), Is.True);
            ESStoryViewSubmission late = new ESStoryViewSubmission(null, 8, "session", 2, 5);
            Assert.That(ESStoryRuntimeGuard.IsCurrentSubmission(instance, instance, late), Is.False);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void SaveSection_RoundTripsWithoutClrTypeMetadata()
        {
            ESStorySaveSection source = new ESStorySaveSection();
            source.questRecords.Add(new ESQuestRecord
            {
                definitionId = "quest.test", contentVersion = 2, contentSignature = "sig",
                currentNodeId = "choice", recordRevision = 7, nodeVisitSequence = 3
            });
            string json = ESGameSaveJson.Serialize(source);
            ESStorySaveSection restored = ESGameSaveJson.Deserialize<ESStorySaveSection>(json);
            Assert.That(json, Does.Not.Contain("$type"));
            Assert.That(json, Does.Not.Contain("activeInstances"));
            Assert.That(restored.questRecords[0].currentNodeId, Is.EqualTo("choice"));
        }

        [Test]
        public void SaveApply_SecondPrepareFailurePreventsEveryCommit()
        {
            int committed = 0;
            ESGameSavePrepareCandidateHandler first = _ => ESGameSaveApplyResult.Ok();
            ESGameSavePrepareCandidateHandler failing = _ => ESGameSaveApplyResult.Fail("test.prepare.reject", "reject");
            ESGameSaveCommitCandidateHandler commitA = (_, __) => { committed++; return ESGameSaveApplyResult.Ok(); };
            ESGameSaveCommitCandidateHandler commitB = (_, __) => { committed++; return ESGameSaveApplyResult.Ok(); };
            ESGameSaveRollbackCandidateHandler rollback = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSaveFinalizeCandidateHandler finalize = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSave.PrepareCandidate += first;
            ESGameSave.PrepareCandidate += failing;
            ESGameSave.CommitCandidate += commitA;
            ESGameSave.CommitCandidate += commitB;
            ESGameSave.RollbackCandidate += rollback;
            ESGameSave.RollbackCandidate += rollback;
            ESGameSave.FinalizeCandidate += finalize;
            ESGameSave.FinalizeCandidate += finalize;
            try
            {
                Assert.That(ApplyCandidate(new ESGameSaveModule(), "slot", new ESGameSaveArchive()), Is.False);
                Assert.That(committed, Is.Zero);
            }
            finally
            {
                ESGameSave.PrepareCandidate -= first;
                ESGameSave.PrepareCandidate -= failing;
                ESGameSave.CommitCandidate -= commitA;
                ESGameSave.CommitCandidate -= commitB;
                ESGameSave.RollbackCandidate -= rollback;
                ESGameSave.RollbackCandidate -= rollback;
                ESGameSave.FinalizeCandidate -= finalize;
                ESGameSave.FinalizeCandidate -= finalize;
            }
        }

        [Test]
        public void StoryLoad_MissingSectionClearsPreviousQuestRecords()
        {
            ESStoryModule story = new ESStoryModule();
            ESGameSaveModule save = new ESGameSaveModule();
            StorySaveHooks hooks = AttachStorySaveParticipant(story);
            try
            {
                Assert.That(ApplyCandidate(save, "A", CreateArchive(CreateStorySection("quest.a", ESStorySaveSection.CurrentSchemaVersion))), Is.True);
                Assert.That(GetPrivateDictionaryCount(story, "questRecords"), Is.EqualTo(1));
                Assert.That(ApplyCandidate(save, "B", new ESGameSaveArchive()), Is.True);
                Assert.That(save.CurrentSlotId, Is.EqualTo("B"));
                Assert.That(GetPrivateDictionaryCount(story, "questRecords"), Is.Zero);
                Assert.That(GetPrivateDictionaryCount(story, "instances"), Is.Zero);
            }
            finally { DetachStorySaveParticipant(hooks); }
        }

        [Test]
        public void StoryLoad_InvalidSchemaPreservesPreviousSlotAndState()
        {
            ESStoryModule story = new ESStoryModule();
            ESGameSaveModule save = new ESGameSaveModule();
            StorySaveHooks hooks = AttachStorySaveParticipant(story);
            try
            {
                Assert.That(ApplyCandidate(save, "A", CreateArchive(CreateStorySection("quest.a", ESStorySaveSection.CurrentSchemaVersion))), Is.True);
                Assert.That(ApplyCandidate(save, "B", CreateArchive(CreateStorySection("quest.b", 999))), Is.False);
                Assert.That(save.CurrentSlotId, Is.EqualTo("A"));
                Assert.That(GetPrivateDictionaryCount(story, "questRecords"), Is.EqualTo(1));
            }
            finally { DetachStorySaveParticipant(hooks); }
        }

        [Test]
        public void StoryValidation_RejectsDuplicateQuestDefinitionIdentity()
        {
            ESStoryModule story = new ESStoryModule();
            ESStorySaveSection section = CreateStorySection("quest.duplicate", ESStorySaveSection.CurrentSchemaVersion);
            section.questRecords.Add(new ESQuestRecord
            {
                definitionId = "quest.duplicate", contentVersion = 1, contentSignature = "sig",
                currentNodeId = "line", runState = ESStoryRunState.Created, recordRevision = 2
            });
            ESGameSaveCandidate candidate = CreateCandidate("slot", CreateArchive(section));
            ESGameSaveApplyResult result = InvokeStoryValidation(story, candidate);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("Story.Dto.DuplicateDefinition"));
            Assert.That(GetPrivateDictionaryCount(story, "questRecords"), Is.Zero);
        }

        [Test]
        public void SaveLoad_AnyParticipantValidationFailurePreventsPartialApply()
        {
            ESGameSaveModule save = new ESGameSaveModule();
            object participant = new object();
            string appliedSlot = null;
            ESGameSaveValidateCandidateHandler validate = candidate =>
            {
                candidate.SetParticipantData(participant, candidate.SlotId);
                return ESGameSaveApplyResult.Ok();
            };
            ESGameSavePrepareCandidateHandler prepare = _ => ESGameSaveApplyResult.Ok();
            ESGameSaveCommitCandidateHandler commit = (candidate, phase) =>
            {
                if (phase == ESGameSaveApplyPhase.Config && candidate.TryGetParticipantData(participant, out string prepared))
                    appliedSlot = prepared;
                return ESGameSaveApplyResult.Ok();
            };
            ESGameSaveValidateCandidateHandler rejectB = candidate => candidate.SlotId == "B"
                ? ESGameSaveApplyResult.Fail("test.reject", "B rejected")
                : ESGameSaveApplyResult.Ok();
            ESGameSaveRollbackCandidateHandler rollback = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSaveFinalizeCandidateHandler finalize = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSave.ValidateCandidate += validate;
            ESGameSave.PrepareCandidate += prepare;
            ESGameSave.CommitCandidate += commit;
            ESGameSave.RollbackCandidate += rollback;
            ESGameSave.FinalizeCandidate += finalize;
            ESGameSave.ValidateCandidate += rejectB;
            try
            {
                Assert.That(ApplyCandidate(save, "A", new ESGameSaveArchive()), Is.True);
                Assert.That(appliedSlot, Is.EqualTo("A"));
                Assert.That(ApplyCandidate(save, "B", new ESGameSaveArchive()), Is.False);
                Assert.That(save.CurrentSlotId, Is.EqualTo("A"));
                Assert.That(appliedSlot, Is.EqualTo("A"));
            }
            finally
            {
                ESGameSave.ValidateCandidate -= validate;
                ESGameSave.PrepareCandidate -= prepare;
                ESGameSave.CommitCandidate -= commit;
                ESGameSave.RollbackCandidate -= rollback;
                ESGameSave.FinalizeCandidate -= finalize;
                ESGameSave.ValidateCandidate -= rejectB;
            }
        }

        [Test]
        public void SaveApply_CommitFailureRollsBackCommittedParticipantsInReverseOrder()
        {
            List<string> trace = new List<string>();
            ESGameSavePrepareCandidateHandler prepare = _ => ESGameSaveApplyResult.Ok();
            ESGameSaveCommitCandidateHandler commitA = (_, phase) =>
            {
                if (phase == ESGameSaveApplyPhase.Config) trace.Add("commit-A");
                return ESGameSaveApplyResult.Ok();
            };
            ESGameSaveCommitCandidateHandler commitB = (_, phase) =>
            {
                if (phase == ESGameSaveApplyPhase.Config) trace.Add("commit-B");
                return ESGameSaveApplyResult.Ok();
            };
            ESGameSaveCommitCandidateHandler commitC = (_, phase) => phase == ESGameSaveApplyPhase.Config
                ? ESGameSaveApplyResult.Fail("test.commit", "C failed")
                : ESGameSaveApplyResult.Ok();
            ESGameSaveRollbackCandidateHandler rollbackA = (_, phase) => { if (phase == ESGameSaveApplyPhase.Config) trace.Add("rollback-A"); return ESGameSaveApplyResult.Ok(); };
            ESGameSaveRollbackCandidateHandler rollbackB = (_, phase) => { if (phase == ESGameSaveApplyPhase.Config) trace.Add("rollback-B"); return ESGameSaveApplyResult.Ok(); };
            ESGameSaveRollbackCandidateHandler rollbackC = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSaveFinalizeCandidateHandler finalize = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSave.PrepareCandidate += prepare;
            ESGameSave.CommitCandidate += commitA; ESGameSave.CommitCandidate += commitB; ESGameSave.CommitCandidate += commitC;
            ESGameSave.RollbackCandidate += rollbackA; ESGameSave.RollbackCandidate += rollbackB; ESGameSave.RollbackCandidate += rollbackC;
            ESGameSave.FinalizeCandidate += finalize; ESGameSave.FinalizeCandidate += finalize; ESGameSave.FinalizeCandidate += finalize;
            try
            {
                Assert.That(ApplyCandidate(new ESGameSaveModule(), "slot", new ESGameSaveArchive()), Is.False);
                Assert.That(trace, Is.EqualTo(new[] { "commit-A", "commit-B", "rollback-B", "rollback-A" }));
            }
            finally
            {
                ESGameSave.PrepareCandidate -= prepare;
                ESGameSave.CommitCandidate -= commitA; ESGameSave.CommitCandidate -= commitB; ESGameSave.CommitCandidate -= commitC;
                ESGameSave.RollbackCandidate -= rollbackA; ESGameSave.RollbackCandidate -= rollbackB; ESGameSave.RollbackCandidate -= rollbackC;
                ESGameSave.FinalizeCandidate -= finalize; ESGameSave.FinalizeCandidate -= finalize; ESGameSave.FinalizeCandidate -= finalize;
            }
        }

        [Test]
        public void SaveApply_RollbackFailureIsReportedExplicitly()
        {
            ESGameSavePrepareCandidateHandler prepare = _ => ESGameSaveApplyResult.Ok();
            ESGameSaveCommitCandidateHandler commitA = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSaveCommitCandidateHandler commitB = (_, phase) => phase == ESGameSaveApplyPhase.Config
                ? ESGameSaveApplyResult.Fail("test.commit", "commit failed") : ESGameSaveApplyResult.Ok();
            ESGameSaveRollbackCandidateHandler rollbackA = (_, __) => ESGameSaveApplyResult.Fail("test.rollback", "rollback failed");
            ESGameSaveRollbackCandidateHandler rollbackB = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSaveFinalizeCandidateHandler finalize = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSave.PrepareCandidate += prepare;
            ESGameSave.CommitCandidate += commitA; ESGameSave.CommitCandidate += commitB;
            ESGameSave.RollbackCandidate += rollbackA; ESGameSave.RollbackCandidate += rollbackB;
            ESGameSave.FinalizeCandidate += finalize; ESGameSave.FinalizeCandidate += finalize;
            ESGameSaveModule save = new ESGameSaveModule();
            try
            {
                Assert.That(ApplyCandidate(save, "slot", new ESGameSaveArchive()), Is.False);
                Assert.That(save.LastReport.message, Does.Contain("Save.Rollback.Failed"));
                Assert.That(save.LastReport.message, Does.Contain("rollback failed"));
            }
            finally
            {
                ESGameSave.PrepareCandidate -= prepare;
                ESGameSave.CommitCandidate -= commitA; ESGameSave.CommitCandidate -= commitB;
                ESGameSave.RollbackCandidate -= rollbackA; ESGameSave.RollbackCandidate -= rollbackB;
                ESGameSave.FinalizeCandidate -= finalize; ESGameSave.FinalizeCandidate -= finalize;
            }
        }

        [Test]
        public void StoryCommitFailure_RollbackRestoresRecordUiLeaseSessionAndBinding()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            definition.storyKind = ESStoryKind.Quest;
            Assert.That(ESStoryDefinitionSnapshot.TryBake(definition, out ESStoryDefinitionSnapshot snapshot, out string error), Is.True, error);
            ESStoryModule story = new ESStoryModule();
            CapturingPresenter presenter = new CapturingPresenter();
            story.BindPresenter(presenter);
            ESQuestRecord record = new ESQuestRecord
            {
                definitionId = snapshot.DefinitionId, contentVersion = snapshot.ContentVersion,
                contentSignature = snapshot.ContentSignature, currentNodeId = "line",
                runState = ESStoryRunState.WaitingForUI, recordRevision = 4
            };
            GetPrivateDictionary<ESQuestRecord>(story, "questRecords").Add(record.definitionId, record);
            GameObject actorObject = new GameObject("rollback-story-actor");
            Entity actor = actorObject.AddComponent<Entity>();
            GameObject targetObject = new GameObject("rollback-story-target");
            ESInteractable target = targetObject.AddComponent<ESInteractable>();
            ESInteractionBinding binding = new ESInteractionBinding(41, 6, actor, target);
            ESStoryInstance instance = new ESStoryInstance();
            SetInstanceProperty(instance, "InstanceId", "rollback-instance");
            SetInstanceProperty(instance, "Definition", snapshot);
            SetInstanceProperty(instance, "Actor", actor);
            SetInstanceProperty(instance, "InteractionBinding", binding);
            SetInstanceProperty(instance, "CurrentNodeId", "line");
            SetInstanceProperty(instance, "RunState", ESStoryRunState.WaitingForUI);
            SetInstanceProperty(instance, "Revision", 9L);
            SetInstanceProperty(instance, "SessionId", "rollback-session");
            SetInstanceProperty(instance, "SessionGeneration", 5);
            SetInstanceProperty(instance, "ViewRevision", 3L);
            SetInstanceProperty(instance, "QuestRecord", record);
            ESRuntimeModeLease originalLease = ESGameManager.RuntimeMode.AcquireModeLease(ESRuntimeMode.Dialogue, instance);
            SetInstanceProperty(instance, "RuntimeModeLease", originalLease);
            GetPrivateDictionary<ESStoryInstance>(story, "instances").Add(instance.InstanceId, instance);
            SetPrivateField(story, "foreground", instance);

            StorySaveHooks hooks = AttachStorySaveParticipant(story);
            ESGameSavePrepareCandidateHandler laterPrepare = _ => ESGameSaveApplyResult.Ok();
            ESGameSaveCommitCandidateHandler laterCommit = (_, phase) => phase == ESGameSaveApplyPhase.Quest
                ? ESGameSaveApplyResult.Fail("test.later", "later participant failed") : ESGameSaveApplyResult.Ok();
            ESGameSaveRollbackCandidateHandler laterRollback = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSaveFinalizeCandidateHandler laterFinalize = (_, __) => ESGameSaveApplyResult.Ok();
            ESGameSave.PrepareCandidate += laterPrepare;
            ESGameSave.CommitCandidate += laterCommit;
            ESGameSave.RollbackCandidate += laterRollback;
            ESGameSave.FinalizeCandidate += laterFinalize;
            try
            {
                Assert.That(ApplyCandidate(new ESGameSaveModule(), "new-slot", new ESGameSaveArchive()), Is.False);
                Assert.That(originalLease.IsReleased, Is.True);
                Assert.That(GetPrivateDictionaryCount(story, "questRecords"), Is.EqualTo(1));
                Assert.That(GetPrivateDictionaryCount(story, "instances"), Is.EqualTo(1));
                Assert.That(instance.RuntimeModeLease, Is.Not.Null);
                Assert.That(instance.RuntimeModeLease.IsValid, Is.True);
                Assert.That(instance.SessionId, Is.EqualTo("rollback-session"));
                Assert.That(instance.SessionGeneration, Is.EqualTo(5));
                Assert.That(instance.InteractionBinding.Token, Is.EqualTo(binding.Token));
                Assert.That(presenter.LastView, Is.Not.Null);
                Assert.That(presenter.LastView.sessionId, Is.EqualTo("rollback-session"));
            }
            finally
            {
                ESGameSave.PrepareCandidate -= laterPrepare;
                ESGameSave.CommitCandidate -= laterCommit;
                ESGameSave.RollbackCandidate -= laterRollback;
                ESGameSave.FinalizeCandidate -= laterFinalize;
                DetachStorySaveParticipant(hooks);
                instance.RuntimeModeLease?.Dispose();
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void StoryLoad_ClearsRuntimeAndRejectsOldSubmission()
        {
            ESStoryModule story = new ESStoryModule();
            CapturingPresenter presenter = new CapturingPresenter();
            story.BindPresenter(presenter);
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            Assert.That(ESStoryDefinitionSnapshot.TryBake(definition, out ESStoryDefinitionSnapshot snapshot, out string error), Is.True, error);
            ESStoryInstance old = new ESStoryInstance();
            SetInstanceProperty(old, "InstanceId", "old-instance");
            SetInstanceProperty(old, "Definition", snapshot);
            SetInstanceProperty(old, "CurrentNodeId", "line");
            SetInstanceProperty(old, "RunState", ESStoryRunState.WaitingForUI);
            SetInstanceProperty(old, "Revision", 7L);
            SetInstanceProperty(old, "SessionId", "old-session");
            SetInstanceProperty(old, "SessionGeneration", 4);
            SetInstanceProperty(old, "ViewRevision", 2L);
            GameObject actorObject = new GameObject("old-story-actor");
            Entity actor = actorObject.AddComponent<Entity>();
            GameObject targetObject = new GameObject("old-story-target");
            ESInteractable target = targetObject.AddComponent<ESInteractable>();
            ESInteractionBinding oldBinding = new ESInteractionBinding(9, 2, actor, target);
            SetInstanceProperty(old, "Actor", actor);
            SetInstanceProperty(old, "InteractionBinding", oldBinding);
            ESRuntimeModeLease lease = ESGameManager.RuntimeMode.AcquireModeLease(ESRuntimeMode.Dialogue, old);
            SetInstanceProperty(old, "RuntimeModeLease", lease);
            GetPrivateDictionary<ESStoryInstance>(story, "instances").Add(old.InstanceId, old);
            SetPrivateField(story, "foreground", old);

            ESGameSaveCandidate candidate = CreateCandidate("B", new ESGameSaveArchive());
            Assert.That(InvokeStoryValidation(story, candidate).Success, Is.True);
            Assert.That(InvokeStoryPrepare(story, candidate).Success, Is.True);
            Assert.That(InvokeStoryCommit(story, candidate, ESGameSaveApplyPhase.Quest).Success, Is.True);
            Assert.That(InvokeStoryFinalize(story, candidate, ESGameSaveApplyPhase.Quest).Success, Is.True);
            Assert.That(GetPrivateDictionaryCount(story, "instances"), Is.Zero);
            Assert.That(lease.IsReleased, Is.True);
            Assert.That(presenter.CloseCount, Is.EqualTo(1));
            Assert.That(story.SubmitContinue(new ESStoryViewSubmission("old-instance", 7, "old-session", 4, 2)), Is.False);
            story.NotifyInteractionEnded(oldBinding, ESInteractionEndReason.UserCancelled);
            Assert.That(GetPrivateDictionaryCount(story, "instances"), Is.Zero);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void QuestLoad_DelayedHydrationCreatesNewIdentityFromRecordNode()
        {
            ESStoryDefinitionDataInfo definition = CreateDialogueDefinition();
            definition.storyKind = ESStoryKind.Quest;
            Assert.That(ESStoryDefinitionSnapshot.TryBake(definition, out ESStoryDefinitionSnapshot snapshot, out string error), Is.True, error);
            ESStoryDefinitionCatalog.Inject(definition);
            ESStorySaveSection section = CreateStorySection(snapshot.DefinitionId, ESStorySaveSection.CurrentSchemaVersion);
            section.questRecords[0].contentVersion = snapshot.ContentVersion;
            section.questRecords[0].contentSignature = snapshot.ContentSignature;
            section.questRecords[0].currentNodeId = "line";
            ESStoryModule story = new ESStoryModule();
            CapturingPresenter presenter = new CapturingPresenter();
            story.BindPresenter(presenter);
            ESGameSaveCandidate candidate = CreateCandidate("slot", CreateArchive(section));
            Assert.That(InvokeStoryValidation(story, candidate).Success, Is.True);
            Assert.That(InvokeStoryPrepare(story, candidate).Success, Is.True);
            Assert.That(InvokeStoryCommit(story, candidate, ESGameSaveApplyPhase.Quest).Success, Is.True);
            Assert.That(InvokeStoryFinalize(story, candidate, ESGameSaveApplyPhase.Quest).Success, Is.True);
            Assert.That(GetPrivateDictionaryCount(story, "instances"), Is.Zero);

            GameObject actorObject = new GameObject("story-test-actor");
            Entity actor = actorObject.AddComponent<Entity>();
            GameObject targetObject = new GameObject("story-test-target");
            ESInteractable target = targetObject.AddComponent<ESInteractable>();
            ESInteractionBinding binding = new ESInteractionBinding(11, 3, actor, target);
            Assert.That(story.TryStartFromInteraction(definition.definitionId, actor, binding, out string instanceId, out error), Is.True, error);
            Assert.That(instanceId, Is.Not.Empty.And.Not.EqualTo("old-instance"));
            Assert.That(presenter.LastView, Is.Not.Null);
            Assert.That(presenter.LastView.text, Is.EqualTo("hello"));
            Assert.That(presenter.LastView.sessionId, Is.Not.Empty);
            Assert.That(presenter.LastView.sessionGeneration, Is.GreaterThan(0));
            story.OnDestroy();
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void StoryModule_LateRegistrationReplaysQuestRecordsOnly()
        {
            ESGameSaveModule save = new ESGameSaveModule();
            ESStorySaveSection section = CreateStorySection("quest.late", ESStorySaveSection.CurrentSchemaVersion);
            Assert.That(ApplyCandidate(save, "late-slot", CreateArchive(section)), Is.True);
            ESGameSaveCandidate retained = GetCurrentCandidate(save);
            ESStoryModule story = new ESStoryModule();
            CapturingPresenter presenter = new CapturingPresenter();
            story.BindPresenter(presenter);
            int modesBefore = ESGameManager.RuntimeMode.ModeCount;

            ESGameSaveApplyResult replay = InvokeStoryReplay(story, retained);
            Assert.That(replay.Success, Is.True, replay.Message);
            Assert.That(GetPrivateDictionaryCount(story, "questRecords"), Is.EqualTo(1));
            Assert.That(GetPrivateDictionaryCount(story, "instances"), Is.Zero);
            Assert.That(presenter.LastView, Is.Null);
            Assert.That(ESGameManager.RuntimeMode.ModeCount, Is.EqualTo(modesBefore));
        }

        [Test]
        public void StoryModule_LateRegistrationFailureRecordsDiagnostic()
        {
            ESGameSaveModule save = new ESGameSaveModule();
            Assert.That(ApplyCandidate(save, "bad-late-slot", CreateArchive(CreateStorySection("quest.bad", 999))), Is.True);
            ESStoryModule story = new ESStoryModule();
            ESGameSaveApplyResult replay = InvokeStoryReplay(story, GetCurrentCandidate(save));
            Assert.That(replay.Success, Is.False);
            ESRingBuffer<string> diagnostics = GetPrivateField<ESRingBuffer<string>>(story, "diagnostics");
            Assert.That(diagnostics.Count, Is.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("晚注册 StoryModule 重放失败"));
        }

        [Test]
        public void StoryModule_DiagnosticsKeepOnlyNewestEntriesInOrder()
        {
            var story = new ESStoryModule();
            for (int i = 0; i < 35; i++)
                InvokeStoryDiagnostic(story, "diagnostic-" + i);

            ESRingBuffer<string> diagnostics = GetPrivateField<ESRingBuffer<string>>(story, "diagnostics");
            Assert.That(diagnostics.Count, Is.EqualTo(32));
            Assert.That(diagnostics[0], Is.EqualTo("diagnostic-3"));
            Assert.That(diagnostics[31], Is.EqualTo("diagnostic-34"));
        }

        private static ESStoryDefinitionDataInfo CreateDialogueDefinition()
        {
            ESStoryDefinitionDataInfo definition = ScriptableObject.CreateInstance<ESStoryDefinitionDataInfo>();
            definition.definitionId.stringKey = "dialogue.test";
            definition.localizationMode = ESStoryLocalizationMode.LegacyLiteral;
            definition.entryNodeId = "start";
            definition.nodes = new List<ESStoryNodeDefinition>
            {
                new ESStoryNodeDefinition { nodeId = "start", nodeKind = ESStoryNodeKind.Start, nextNodeId = "line" },
                new ESStoryNodeDefinition { nodeId = "line", nodeKind = ESStoryNodeKind.Dialogue, text = "hello", nextNodeId = "done" },
                new ESStoryNodeDefinition { nodeId = "done", nodeKind = ESStoryNodeKind.Complete }
            };
            return definition;
        }

        private static ESStoryDefinitionDataInfo CreateChoiceDefinition()
        {
            ESStoryDefinitionDataInfo definition = ScriptableObject.CreateInstance<ESStoryDefinitionDataInfo>();
            definition.definitionId.stringKey = "dialogue.choice-order";
            definition.storyKind = ESStoryKind.Dialogue;
            definition.localizationMode = ESStoryLocalizationMode.LegacyLiteral;
            definition.entryNodeId = "start";
            definition.nodes = new List<ESStoryNodeDefinition>
            {
                new ESStoryNodeDefinition { nodeId = "start", nodeKind = ESStoryNodeKind.Start, nextNodeId = "choice" },
                new ESStoryNodeDefinition
                {
                    nodeId = "choice",
                    nodeKind = ESStoryNodeKind.Choice,
                    options = new List<ESStoryOptionDefinition>
                    {
                        new ESStoryOptionDefinition { optionId = "first", text = "First", nextNodeId = "first-end" },
                        new ESStoryOptionDefinition { optionId = "second", text = "Second", nextNodeId = "second-end" }
                    }
                },
                new ESStoryNodeDefinition { nodeId = "first-end", nodeKind = ESStoryNodeKind.Complete },
                new ESStoryNodeDefinition { nodeId = "second-end", nodeKind = ESStoryNodeKind.Complete }
            };
            return definition;
        }

        private static ESStoryDefinitionDataInfo CreateStableDialogueDefinition()
        {
            ESStoryDefinitionDataInfo definition = ScriptableObject.CreateInstance<ESStoryDefinitionDataInfo>();
            definition.definitionId.stringKey = "dialogue.localized";
            definition.storyKind = ESStoryKind.Dialogue;
            definition.localizationMode = ESStoryLocalizationMode.StableTextKey;
            definition.entryNodeId = "start";
            definition.nodes = new List<ESStoryNodeDefinition>
            {
                new ESStoryNodeDefinition { nodeId = "start", nodeKind = ESStoryNodeKind.Start, nextNodeId = "line" },
                new ESStoryNodeDefinition
                {
                    nodeId = "line",
                    nodeKind = ESStoryNodeKind.Dialogue,
                    speakerText = new ESLocalizedTextRef("story.speaker", fallbackLiteral: "向导"),
                    bodyText = new ESLocalizedTextRef("story.line", fallbackLiteral: "你好"),
                    nextNodeId = "done"
                },
                new ESStoryNodeDefinition { nodeId = "done", nodeKind = ESStoryNodeKind.Complete }
            };
            return definition;
        }

        private static ESStorySaveSection CreateStorySection(string definitionId, int schemaVersion)
        {
            ESStorySaveSection section = new ESStorySaveSection { snapshotSchemaVersion = schemaVersion };
            section.questRecords.Add(new ESQuestRecord
            {
                definitionId = definitionId, contentVersion = 1, contentSignature = "sig",
                currentNodeId = "line", runState = ESStoryRunState.Created, recordRevision = 1
            });
            return section;
        }

        private static ESGameSaveArchive CreateArchive(ESStorySaveSection section)
        {
            ESGameSaveArchive archive = new ESGameSaveArchive();
            archive.UpsertSection(ESGameSaveSectionPacket.FromData("story.runtime", section));
            return archive;
        }

        private static ESGameSaveCandidate CreateCandidate(string slotId, ESGameSaveArchive archive)
        {
            return (ESGameSaveCandidate)System.Activator.CreateInstance(typeof(ESGameSaveCandidate),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { slotId, archive }, null);
        }

        private static bool ApplyCandidate(ESGameSaveModule module, string slotId, ESGameSaveArchive archive)
        {
            MethodInfo method = typeof(ESGameSaveModule).GetMethod("TryApplyCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(module, new object[] { slotId, archive });
        }

        private static ESGameSaveApplyResult InvokeSaveValidation(ESGameSaveCandidate candidate)
        {
            MethodInfo method = typeof(ESGameSave).GetMethod("NotifyValidateCandidate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(null, new object[] { candidate });
        }

        private static ESGameSaveApplyResult InvokeStoryValidation(ESStoryModule story, ESGameSaveCandidate candidate)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("OnValidateSaveCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(story, new object[] { candidate });
        }

        private static ESGameSaveApplyResult InvokeStoryCommit(ESStoryModule story, ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("OnCommitSaveCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(story, new object[] { candidate, phase });
        }

        private static ESGameSaveApplyResult InvokeStoryPrepare(ESStoryModule story, ESGameSaveCandidate candidate)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("OnPrepareSaveCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(story, new object[] { candidate });
        }

        private static ESGameSaveApplyResult InvokeStoryRollback(ESStoryModule story, ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("OnRollbackSaveCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(story, new object[] { candidate, phase });
        }

        private static ESGameSaveApplyResult InvokeStoryFinalize(ESStoryModule story, ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("OnFinalizeSaveCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(story, new object[] { candidate, phase });
        }

        private static ESGameSaveApplyResult InvokeStoryReplay(ESStoryModule story, ESGameSaveCandidate candidate)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("ReplaySaveCandidateWithDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ESGameSaveApplyResult)method.Invoke(story, new object[] { candidate });
        }

        private static void InvokeStoryDiagnostic(ESStoryModule story, string message)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("RecordDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(story, new object[] { message });
        }

        private static void InvokeStoryLocalizationSubscription(ESStoryModule story)
        {
            MethodInfo method = typeof(ESStoryModule).GetMethod("SubscribeLocalizationEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(story, null);
        }

        private static StorySaveHooks AttachStorySaveParticipant(ESStoryModule story)
        {
            StorySaveHooks hooks = new StorySaveHooks
            {
                validate = candidate => InvokeStoryValidation(story, candidate),
                prepare = candidate => InvokeStoryPrepare(story, candidate),
                commit = (candidate, phase) => InvokeStoryCommit(story, candidate, phase),
                rollback = (candidate, phase) => InvokeStoryRollback(story, candidate, phase),
                finalize = (candidate, phase) => InvokeStoryFinalize(story, candidate, phase)
            };
            ESGameSave.ValidateCandidate += hooks.validate;
            ESGameSave.PrepareCandidate += hooks.prepare;
            ESGameSave.CommitCandidate += hooks.commit;
            ESGameSave.RollbackCandidate += hooks.rollback;
            ESGameSave.FinalizeCandidate += hooks.finalize;
            return hooks;
        }

        private static void DetachStorySaveParticipant(StorySaveHooks hooks)
        {
            ESGameSave.ValidateCandidate -= hooks.validate;
            ESGameSave.PrepareCandidate -= hooks.prepare;
            ESGameSave.CommitCandidate -= hooks.commit;
            ESGameSave.RollbackCandidate -= hooks.rollback;
            ESGameSave.FinalizeCandidate -= hooks.finalize;
        }

        private static ESGameSaveCandidate GetCurrentCandidate(ESGameSaveModule save)
        {
            MethodInfo method = typeof(ESGameSaveModule).GetMethod("TryGetCurrentCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { null };
            Assert.That((bool)method.Invoke(save, arguments), Is.True);
            return (ESGameSaveCandidate)arguments[0];
        }

        private static Dictionary<string, T> GetPrivateDictionary<T>(ESStoryModule story, string fieldName)
        {
            FieldInfo field = typeof(ESStoryModule).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (Dictionary<string, T>)field.GetValue(story);
        }

        private static int GetPrivateDictionaryCount(ESStoryModule story, string fieldName)
        {
            object dictionary = typeof(ESStoryModule).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(story);
            Assert.That(dictionary, Is.Not.Null);
            return (int)dictionary.GetType().GetProperty("Count").GetValue(dictionary);
        }

        private static T GetPrivateField<T>(ESStoryModule story, string fieldName)
        {
            FieldInfo field = typeof(ESStoryModule).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(story);
        }

        private static void SetPrivateField(ESStoryModule story, string fieldName, object value)
        {
            FieldInfo field = typeof(ESStoryModule).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(story, value);
        }

        private static void SetInstanceProperty(ESStoryInstance instance, string propertyName, object value)
        {
            PropertyInfo property = typeof(ESStoryInstance).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            property.SetValue(instance, value);
        }
    }
}
