using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("任务与剧情模块")]
    public sealed class ESStoryModule : ESRuntimeModule
    {
        private const string SaveSectionKey = "story.runtime";
        private const int MaxSynchronousSteps = 64;
        private const int MaxDiagnostics = 32;

        [ShowInInspector, ReadOnly, LabelText("活动实例数")]
        private int ActiveInstanceCount => instances.Count;
        [ShowInInspector, ReadOnly, LabelText("任务记录数")]
        private int QuestRecordCount => questRecords.Count;
        [ShowInInspector, ReadOnly, LabelText("前台实例")]
        private string ForegroundInstanceId => foreground?.InstanceId ?? string.Empty;

        [NonSerialized] private readonly Dictionary<string, ESStoryInstance> instances = new Dictionary<string, ESStoryInstance>(StringComparer.Ordinal);
        [NonSerialized] private readonly Dictionary<string, ESQuestRecord> questRecords = new Dictionary<string, ESQuestRecord>(StringComparer.Ordinal);
        [NonSerialized] private readonly Queue<ESStoryInstance> foregroundQueue = new Queue<ESStoryInstance>();
        [NonSerialized] private readonly List<string> diagnostics = new List<string>(MaxDiagnostics);
        [NonSerialized] private IESStoryDialoguePresenter presenter;
        [NonSerialized] private ESStoryInstance foreground;
        [NonSerialized] private int nextSessionGeneration;
        [NonSerialized] private long checkpointRevision;
        [NonSerialized] private bool isDestroying;
        [NonSerialized] private bool checkpointDirty;

        private sealed class ESStoryPreparedSaveApply
        {
            public ESStorySaveSection section;
            public readonly Dictionary<string, ESQuestRecord> previousQuestRecords = new Dictionary<string, ESQuestRecord>(StringComparer.Ordinal);
            public readonly Dictionary<string, ESStoryInstance> previousInstances = new Dictionary<string, ESStoryInstance>(StringComparer.Ordinal);
            public readonly List<ESStoryInstance> previousForegroundQueue = new List<ESStoryInstance>();
            public readonly HashSet<string> previousLeasedInstances = new HashSet<string>(StringComparer.Ordinal);
            public ESStoryInstance previousForeground;
            public long previousCheckpointRevision;
            public bool previousCheckpointDirty;
            public bool committed;
            public bool rolledBack;
            public bool finalized;
        }

        public override void Start()
        {
            base.Start();
            ESGameSave.BeforeSave += FlushCheckpoint;
            ESGameSave.ValidateCandidate += OnValidateSaveCandidate;
            ESGameSave.PrepareCandidate += OnPrepareSaveCandidate;
            ESGameSave.CommitCandidate += OnCommitSaveCandidate;
            ESGameSave.RollbackCandidate += OnRollbackSaveCandidate;
            ESGameSave.FinalizeCandidate += OnFinalizeSaveCandidate;
            ReplayCurrentSaveCandidate();
        }

        protected override void Update()
        {
            if (checkpointDirty) FlushCheckpoint();
        }

        public void BindPresenter(IESStoryDialoguePresenter value) => presenter = value;

        public bool TryStartFromInteraction(ESStoryConfigKey definitionKey, Entity actor, ESInteractionBinding binding, out string instanceId, out string error)
        {
            instanceId = null;
            error = null;
            if (definitionKey == null || string.IsNullOrWhiteSpace(definitionKey.StringKey)
                || actor == null || !binding.IsValid || binding.Owner != actor)
            {
                error = "Story 启动参数无效。";
                return false;
            }
            if (!ESStoryDefinitionCatalog.TryResolve(definitionKey, out ESStoryDefinitionSnapshot snapshot))
            {
                error = "Story 定义尚未完成验证并注入运行时 Catalog：" + definitionKey.StringKey;
                return false;
            }
            if (snapshot.StoryKind == ESStoryKind.Quest && ESStoryRuntimeGuard.HasActiveQuest(instances.Values, snapshot.DefinitionId))
            {
                error = "同一 Quest DefinitionId 已有活动 StoryInstance。";
                return false;
            }

            ESQuestRecord record = null;
            string currentNodeId = snapshot.EntryNodeId;
            if (snapshot.StoryKind == ESStoryKind.Quest)
            {
                if (questRecords.TryGetValue(snapshot.DefinitionId, out record))
                {
                    if (record.contentVersion != snapshot.ContentVersion || !string.Equals(record.contentSignature, snapshot.ContentSignature, StringComparison.Ordinal))
                    {
                        error = "QuestRecord 对应的 Definition 版本或签名不匹配。";
                        return false;
                    }
                    if (record.runState == ESStoryRunState.Completed)
                    {
                        error = "该 Quest 已完成。";
                        return false;
                    }
                    currentNodeId = record.currentNodeId;
                }
                else
                {
                    record = new ESQuestRecord
                    {
                        definitionId = snapshot.DefinitionId,
                        contentVersion = snapshot.ContentVersion,
                        contentSignature = snapshot.ContentSignature,
                        currentNodeId = currentNodeId,
                        runState = ESStoryRunState.Created,
                        recordRevision = 1
                    };
                    questRecords.Add(record.definitionId, record);
                }
            }

            ESStoryInstance instance = new ESStoryInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"), Definition = snapshot, Actor = actor,
                InteractionBinding = binding, CurrentNodeId = currentNodeId, RunState = ESStoryRunState.Created,
                Revision = 1, QuestRecord = record,
                NodeVisitSequence = record?.nodeVisitSequence ?? 0,
                ViewRevision = 0
            };
            instances.Add(instance.InstanceId, instance);
            instanceId = instance.InstanceId;
            RequestForeground(instance);
            MarkCheckpointDirty();
            return true;
        }

        public bool SubmitContinue(ESStoryViewSubmission submission)
        {
            if (!TryValidateSubmission(submission, out ESStoryInstance instance, out ESStoryNodeSnapshot node) || node.NodeKind != ESStoryNodeKind.Dialogue)
                return false;
            instance.Revision++;
            MoveTo(instance, node.NextNodeId);
            instance.RunState = ESStoryRunState.Running;
            Advance(instance);
            return true;
        }

        public bool SubmitOption(ESStoryViewSubmission submission)
        {
            if (!TryValidateSubmission(submission, out ESStoryInstance instance, out ESStoryNodeSnapshot node) || node.NodeKind != ESStoryNodeKind.Choice)
                return false;
            ESStoryOptionSnapshot selected = null;
            for (int i = 0; i < node.Options.Count; i++)
                if (string.Equals(node.Options[i].OptionId, submission.OptionId, StringComparison.Ordinal)) { selected = node.Options[i]; break; }
            if (selected == null) { RecordDiagnostic("拒绝不属于当前节点的 OptionId：" + submission.OptionId); return false; }
            instance.Revision++;
            MoveTo(instance, selected.NextNodeId);
            instance.RunState = ESStoryRunState.Running;
            Advance(instance);
            return true;
        }

        public void NotifyInteractionEnded(ESInteractionBinding binding, ESInteractionEndReason reason)
        {
            ESStoryInstance found = null;
            foreach (ESStoryInstance instance in instances.Values)
                if (instance.InteractionBinding.Token == binding.Token && instance.InteractionBinding.Generation == binding.Generation) { found = instance; break; }
            if (found != null) Finish(found, ESStoryRunState.Aborted, reason, false, false);
        }

        private void ReleaseRuntimeForLoad(bool endInteraction)
        {
            ESStoryInstance[] active = new ESStoryInstance[instances.Count];
            instances.Values.CopyTo(active, 0);
            instances.Clear();
            foregroundQueue.Clear();
            foreground = null;
            for (int i = 0; i < active.Length; i++)
            {
                ESStoryInstance instance = active[i];
                try { presenter?.Close(instance.InstanceId, instance.SessionId, instance.SessionGeneration); }
                catch (Exception exception) { Debug.LogException(exception); }
                instance.RuntimeModeLease?.Dispose();
                instance.RuntimeModeLease = null;
                if (endInteraction)
                    TryEndInteraction(instance, false, ESInteractionEndReason.SceneTransition);
            }
        }

        private ESGameSaveApplyResult OnValidateSaveCandidate(ESGameSaveCandidate candidate)
        {
            if (candidate == null || candidate.Archive == null)
                return ESGameSaveApplyResult.Fail("Story.Candidate.Missing", "Story 收到空候选 Archive。");

            ESGameSaveSectionPacket packet = candidate.FindSection(SaveSectionKey);
            ESStorySaveSection section;
            if (packet == null)
            {
                section = new ESStorySaveSection();
            }
            else
            {
                if (packet.schemaVersion != 1)
                    return ESGameSaveApplyResult.Fail("Story.Packet.SchemaUnsupported", "story.runtime 分区封装版本不受支持：" + packet.schemaVersion);
                if (string.IsNullOrWhiteSpace(packet.json))
                    return ESGameSaveApplyResult.Fail("Story.Json.Empty", "story.runtime JSON 为空。");
                try { section = ESGameSaveJson.Deserialize<ESStorySaveSection>(packet.json); }
                catch (Exception exception) { return ESGameSaveApplyResult.Fail("Story.Json.Invalid", exception.Message); }
            }

            ESGameSaveApplyResult result = ValidateSaveSection(section);
            if (!result.Success)
                return result;
            candidate.SetParticipantData(this, section);
            return ESGameSaveApplyResult.Ok();
        }

        private ESGameSaveApplyResult OnPrepareSaveCandidate(ESGameSaveCandidate candidate)
        {
            if (candidate == null || !candidate.TryGetParticipantData(this, out ESStorySaveSection section))
                return ESGameSaveApplyResult.Fail("Story.Prepare.NotValidated", "Story 候选状态尚未通过 Validate。");

            ESStoryPreparedSaveApply prepared = new ESStoryPreparedSaveApply
            {
                section = section,
                previousForeground = foreground,
                previousCheckpointRevision = checkpointRevision,
                previousCheckpointDirty = checkpointDirty
            };
            foreach (KeyValuePair<string, ESQuestRecord> pair in questRecords)
                prepared.previousQuestRecords.Add(pair.Key, pair.Value);
            foreach (KeyValuePair<string, ESStoryInstance> pair in instances)
            {
                prepared.previousInstances.Add(pair.Key, pair.Value);
                if (pair.Value.RuntimeModeLease != null && pair.Value.RuntimeModeLease.IsValid)
                    prepared.previousLeasedInstances.Add(pair.Key);
            }
            foreach (ESStoryInstance queued in foregroundQueue)
                prepared.previousForegroundQueue.Add(queued);
            candidate.SetParticipantData(this, prepared);
            return ESGameSaveApplyResult.Ok();
        }

        private ESGameSaveApplyResult OnCommitSaveCandidate(ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            if (phase != ESGameSaveApplyPhase.Quest)
                return ESGameSaveApplyResult.Ok();
            if (candidate == null || !candidate.TryGetParticipantData(this, out ESStoryPreparedSaveApply prepared))
                return ESGameSaveApplyResult.Fail("Story.Commit.NotPrepared", "Story 候选状态尚未通过 Prepare。");

            ReleaseRuntimeForLoad(false);
            questRecords.Clear();
            for (int i = 0; i < prepared.section.questRecords.Count; i++)
            {
                ESQuestRecord record = prepared.section.questRecords[i];
                questRecords.Add(record.definitionId, record);
            }
            checkpointRevision = prepared.section.metadata.checkpointRevision;
            checkpointDirty = false;
            prepared.committed = true;
            return ESGameSaveApplyResult.Ok();
        }

        private ESGameSaveApplyResult OnRollbackSaveCandidate(ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            if (phase != ESGameSaveApplyPhase.Quest)
                return ESGameSaveApplyResult.Ok();
            if (candidate == null || !candidate.TryGetParticipantData(this, out ESStoryPreparedSaveApply prepared))
                return ESGameSaveApplyResult.Fail("Story.Rollback.NotPrepared", "Story Rollback 缺少 Prepare 状态。");
            if (!prepared.committed || prepared.rolledBack)
                return ESGameSaveApplyResult.Ok();

            ReleaseRuntimeForLoad(false);
            questRecords.Clear();
            foreach (KeyValuePair<string, ESQuestRecord> pair in prepared.previousQuestRecords)
                questRecords.Add(pair.Key, pair.Value);
            instances.Clear();
            foreach (KeyValuePair<string, ESStoryInstance> pair in prepared.previousInstances)
                instances.Add(pair.Key, pair.Value);
            foregroundQueue.Clear();
            for (int i = 0; i < prepared.previousForegroundQueue.Count; i++)
                foregroundQueue.Enqueue(prepared.previousForegroundQueue[i]);
            foreground = prepared.previousForeground;
            checkpointRevision = prepared.previousCheckpointRevision;
            checkpointDirty = prepared.previousCheckpointDirty;

            foreach (string instanceId in prepared.previousLeasedInstances)
            {
                if (!instances.TryGetValue(instanceId, out ESStoryInstance instance))
                    continue;
                instance.RuntimeModeLease = ESGameManager.RuntimeMode?.AcquireModeLease(ESRuntimeMode.Dialogue, instance);
                if (instance.RuntimeModeLease == null || !instance.RuntimeModeLease.IsValid)
                    return ESGameSaveApplyResult.Fail("Story.Rollback.RuntimeMode", "Story Rollback 无法恢复 RuntimeMode Lease：" + instanceId);
            }
            if (foreground != null && foreground.RunState == ESStoryRunState.WaitingForUI)
            {
                if (!foreground.Definition.TryGetNode(foreground.CurrentNodeId, out ESStoryNodeSnapshot node))
                    return ESGameSaveApplyResult.Fail("Story.Rollback.NodeMissing", "Story Rollback 无法恢复 UI 节点：" + foreground.CurrentNodeId);
                try { presenter?.Show(BuildViewData(foreground, node, node.NodeKind == ESStoryNodeKind.Dialogue)); }
                catch (Exception exception) { return ESGameSaveApplyResult.Fail("Story.Rollback.UI", exception.Message); }
            }
            prepared.rolledBack = true;
            return ESGameSaveApplyResult.Ok();
        }

        private ESGameSaveApplyResult OnFinalizeSaveCandidate(ESGameSaveCandidate candidate, ESGameSaveApplyPhase phase)
        {
            if (phase != ESGameSaveApplyPhase.Quest)
                return ESGameSaveApplyResult.Ok();
            if (candidate == null || !candidate.TryGetParticipantData(this, out ESStoryPreparedSaveApply prepared))
                return ESGameSaveApplyResult.Fail("Story.Finalize.NotPrepared", "Story Finalize 缺少 Prepare 状态。");
            if (prepared.finalized || prepared.rolledBack)
                return ESGameSaveApplyResult.Ok();
            foreach (ESStoryInstance instance in prepared.previousInstances.Values)
                TryEndInteraction(instance, false, ESInteractionEndReason.SceneTransition);
            prepared.finalized = true;
            return ESGameSaveApplyResult.Ok();
        }

        private void ReplayCurrentSaveCandidate()
        {
            if (!ESGameSave.TryGetCurrentCandidate(out ESGameSaveCandidate candidate))
                return;
            ReplaySaveCandidateWithDiagnostics(candidate);
        }

        private ESGameSaveApplyResult ReplaySaveCandidateWithDiagnostics(ESGameSaveCandidate candidate)
        {
            ESGameSaveApplyResult result = ReplaySaveCandidate(candidate);
            if (!result.Success)
                RecordDiagnostic("晚注册 StoryModule 重放失败 [" + result.ErrorCode + "] " + result.Message);
            return result;
        }

        private ESGameSaveApplyResult ReplaySaveCandidate(ESGameSaveCandidate candidate)
        {
            ESGameSaveApplyResult result = OnValidateSaveCandidate(candidate);
            if (result.Success) result = OnPrepareSaveCandidate(candidate);
            if (result.Success) result = OnCommitSaveCandidate(candidate, ESGameSaveApplyPhase.Quest);
            if (result.Success) result = OnFinalizeSaveCandidate(candidate, ESGameSaveApplyPhase.Quest);
            return result;
        }

        private static ESGameSaveApplyResult ValidateSaveSection(ESStorySaveSection section)
        {
            if (section == null)
                return ESGameSaveApplyResult.Fail("Story.Dto.Null", "story.runtime 无法反序列化为有效 DTO。");
            if (section.snapshotSchemaVersion != ESStorySaveSection.CurrentSchemaVersion)
                return ESGameSaveApplyResult.Fail("Story.Schema.Unsupported", "Story SnapshotSchemaVersion 不受支持：" + section.snapshotSchemaVersion);
            if (section.questRecords == null || section.metadata == null)
                return ESGameSaveApplyResult.Fail("Story.Dto.Illegal", "QuestRecords 或 Metadata 缺失。");

            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < section.questRecords.Count; i++)
            {
                ESQuestRecord record = section.questRecords[i];
                if (record == null || string.IsNullOrWhiteSpace(record.definitionId)
                    || record.contentVersion <= 0 || string.IsNullOrWhiteSpace(record.contentSignature)
                    || string.IsNullOrWhiteSpace(record.currentNodeId) || record.recordRevision <= 0
                    || record.nodeVisitSequence < 0 || !Enum.IsDefined(typeof(ESStoryRunState), record.runState))
                    return ESGameSaveApplyResult.Fail("Story.Dto.IllegalQuestRecord", "QuestRecord 包含空身份、非法版本、节点或 Revision。");
                if (!identities.Add(record.definitionId))
                    return ESGameSaveApplyResult.Fail("Story.Dto.DuplicateDefinition", "QuestRecord DefinitionId 重复：" + record.definitionId);
            }
            if (section.metadata.checkpointRevision < 0 || section.metadata.checkpointUtcTicks < 0)
                return ESGameSaveApplyResult.Fail("Story.Dto.IllegalMetadata", "Story Metadata 包含非法负数值。");
            return ESGameSaveApplyResult.Ok();
        }

        private void RequestForeground(ESStoryInstance instance)
        {
            if (foreground != null)
            {
                instance.RunState = ESStoryRunState.WaitingForForeground;
                foregroundQueue.Enqueue(instance);
                return;
            }
            ActivateForeground(instance);
        }

        private void ActivateForeground(ESStoryInstance instance)
        {
            foreground = instance;
            instance.SessionId = Guid.NewGuid().ToString("N");
            nextSessionGeneration = nextSessionGeneration == int.MaxValue ? 1 : nextSessionGeneration + 1;
            instance.SessionGeneration = nextSessionGeneration;
            instance.RuntimeModeLease = ESGameManager.RuntimeMode?.AcquireModeLease(ESRuntimeMode.Dialogue, instance);
            if (instance.RuntimeModeLease == null || !instance.RuntimeModeLease.IsValid)
            {
                Finish(instance, ESStoryRunState.Failed, ESInteractionEndReason.StoryFailed, false);
                return;
            }
            instance.RunState = ESStoryRunState.Running;
            Advance(instance);
        }

        private void Advance(ESStoryInstance instance)
        {
            for (int step = 0; step < MaxSynchronousSteps; step++)
            {
                if (!instances.ContainsKey(instance.InstanceId) || instance.RunState != ESStoryRunState.Running) return;
                if (!instance.Definition.TryGetNode(instance.CurrentNodeId, out ESStoryNodeSnapshot node))
                {
                    Finish(instance, ESStoryRunState.Failed, ESInteractionEndReason.StoryFailed, false); return;
                }
                switch (node.NodeKind)
                {
                    case ESStoryNodeKind.Start: MoveTo(instance, node.NextNodeId); break;
                    case ESStoryNodeKind.Condition:
                        bool matches = instance.Actor != null && instance.Actor.Tags.Matches(node.TagCondition);
                        MoveTo(instance, matches ? node.TrueNodeId : node.FalseNodeId); break;
                    case ESStoryNodeKind.Action:
                        if (!ExecuteSetTag(instance, node)) { Finish(instance, ESStoryRunState.Failed, ESInteractionEndReason.StoryFailed, false); return; }
                        MoveTo(instance, node.NextNodeId); break;
                    case ESStoryNodeKind.Dialogue: PublishView(instance, node, true); return;
                    case ESStoryNodeKind.Choice: PublishView(instance, node, false); return;
                    case ESStoryNodeKind.Complete: Finish(instance, ESStoryRunState.Completed, ESInteractionEndReason.Completed, true); return;
                    case ESStoryNodeKind.Fail: Finish(instance, ESStoryRunState.Failed, ESInteractionEndReason.StoryFailed, false); return;
                }
            }
            RecordDiagnostic("同步推进超过上限，可能存在无进展循环：" + instance.CurrentNodeId);
            Finish(instance, ESStoryRunState.Failed, ESInteractionEndReason.StoryFailed, false);
        }

        private bool ExecuteSetTag(ESStoryInstance instance, ESStoryNodeSnapshot node)
        {
            ESStoryExecutionTicket ticket = new ESStoryExecutionTicket
            {
                executionId = Guid.NewGuid().ToString("N"), storyInstanceId = instance.InstanceId,
                expectedInstanceRevision = instance.Revision, nodeId = node.NodeId,
                nodeVisitSequence = instance.NodeVisitSequence, actionId = node.ActionId,
                executionState = ESStoryExecutionState.Prepared
            };
            instance.CurrentExecution = ticket;
            bool result = instance.Actor != null && instance.Actor.Tags.SetTag(node.SetTag, node.SetTagActive);
            if (!instances.TryGetValue(ticket.storyInstanceId, out ESStoryInstance current)
                || current.Revision != ticket.expectedInstanceRevision || current.CurrentNodeId != ticket.nodeId
                || current.NodeVisitSequence != ticket.nodeVisitSequence || !ReferenceEquals(current.CurrentExecution, ticket))
            {
                ticket.executionState = ESStoryExecutionState.Discarded;
                RecordDiagnostic("迟到或失配的 Action 结果已丢弃：" + ticket.actionId);
                return false;
            }
            ticket.actionResult = result;
            ticket.executionState = result ? ESStoryExecutionState.Succeeded : ESStoryExecutionState.Failed;
            instance.CurrentExecution = null;
            return result;
        }

        private void PublishView(ESStoryInstance instance, ESStoryNodeSnapshot node, bool canContinue)
        {
            instance.RunState = ESStoryRunState.WaitingForUI;
            instance.Revision++;
            instance.ViewRevision++;
            ESDialogueViewData view = BuildViewData(instance, node, canContinue);
            MarkCheckpointDirty();
            try { presenter?.Show(view); }
            catch (Exception exception) { Debug.LogException(exception); Finish(instance, ESStoryRunState.Failed, ESInteractionEndReason.StoryFailed, false); }
        }

        private static ESDialogueViewData BuildViewData(ESStoryInstance instance, ESStoryNodeSnapshot node, bool canContinue)
        {
            ESDialogueViewData view = new ESDialogueViewData
            {
                definitionId = instance.Definition.DefinitionId, storyInstanceId = instance.InstanceId,
                instanceRevision = instance.Revision, sessionId = instance.SessionId,
                sessionGeneration = instance.SessionGeneration, viewRevision = instance.ViewRevision,
                speakerName = node.SpeakerName, text = node.Text, canContinue = canContinue
            };
            if (!canContinue)
                for (int i = 0; i < node.Options.Count; i++) view.options.Add(new ESDialogueOptionViewData { optionId = node.Options[i].OptionId, text = node.Options[i].Text });
            return view;
        }

        private bool TryValidateSubmission(ESStoryViewSubmission submission, out ESStoryInstance instance, out ESStoryNodeSnapshot node)
        {
            node = null;
            if (!instances.TryGetValue(submission.StoryInstanceId ?? string.Empty, out instance)
                || !ESStoryRuntimeGuard.IsCurrentSubmission(instance, foreground, submission)
                || !instance.Definition.TryGetNode(instance.CurrentNodeId, out node))
            {
                RecordDiagnostic("拒绝旧代、迟到或非前台 UI 提交。");
                return false;
            }
            return true;
        }

        private void MoveTo(ESStoryInstance instance, string nextNodeId)
        {
            instance.CurrentNodeId = nextNodeId;
            instance.NodeVisitSequence++;
            instance.Revision++;
            if (instance.QuestRecord != null)
            {
                instance.QuestRecord.currentNodeId = nextNodeId;
                instance.QuestRecord.nodeVisitSequence = instance.NodeVisitSequence;
                instance.QuestRecord.runState = instance.RunState;
                instance.QuestRecord.recordRevision++;
            }
            MarkCheckpointDirty();
        }

        private void Finish(ESStoryInstance instance, ESStoryRunState state, ESInteractionEndReason reason, bool success, bool requestInteractionEnd = true)
        {
            if (instance == null || !instances.ContainsKey(instance.InstanceId)) return;
            instance.RunState = state;
            instance.Revision++;
            if (instance.QuestRecord != null)
            {
                instance.QuestRecord.runState = state;
                instance.QuestRecord.currentNodeId = instance.CurrentNodeId;
                instance.QuestRecord.nodeVisitSequence = instance.NodeVisitSequence;
                instance.QuestRecord.recordRevision++;
            }
            try { presenter?.Close(instance.InstanceId, instance.SessionId, instance.SessionGeneration); }
            catch (Exception exception) { Debug.LogException(exception); }
            instance.RuntimeModeLease?.Dispose();
            instance.RuntimeModeLease = null;
            instances.Remove(instance.InstanceId);
            if (foreground == instance) foreground = null;
            if (!isDestroying) MarkCheckpointDirty();
            if (requestInteractionEnd) TryEndInteraction(instance, success, reason);
            if (!isDestroying) PromoteForeground();
        }

        private static void TryEndInteraction(ESStoryInstance instance, bool success, ESInteractionEndReason reason)
        {
            Entity actor = instance.Actor;
            if (actor?.basicDomain?.MyModules?.ValuesNow == null) return;
            List<EntityBasicModuleBase> modules = actor.basicDomain.MyModules.ValuesNow;
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] is EntityBasicInteractionModule interaction) { interaction.TryEndExternalInteraction(instance.InteractionBinding, success, reason); return; }
        }

        private void PromoteForeground()
        {
            while (foreground == null && foregroundQueue.Count > 0)
            {
                ESStoryInstance next = foregroundQueue.Dequeue();
                if (next != null && instances.ContainsKey(next.InstanceId)) ActivateForeground(next);
            }
        }

        private void MarkCheckpointDirty()
        {
            checkpointDirty = true;
        }

        public void FlushCheckpoint()
        {
            if (!checkpointDirty || isDestroying)
                return;
            ESStorySaveSection section = new ESStorySaveSection();
            foreach (ESQuestRecord record in questRecords.Values) section.questRecords.Add(record);
            section.metadata.checkpointRevision = ++checkpointRevision;
            section.metadata.checkpointUtcTicks = DateTime.UtcNow.Ticks;
            ESGameSave.SetCurrent(SaveSectionKey, section);
            checkpointDirty = false;
        }

        private void RecordDiagnostic(string message)
        {
            if (diagnostics.Count == MaxDiagnostics) diagnostics.RemoveAt(0);
            diagnostics.Add(message != null && message.Length > 256 ? message.Substring(0, 256) : message);
        }

        public override void OnDestroy()
        {
            isDestroying = true;
            ESGameSave.BeforeSave -= FlushCheckpoint;
            ESGameSave.ValidateCandidate -= OnValidateSaveCandidate;
            ESGameSave.PrepareCandidate -= OnPrepareSaveCandidate;
            ESGameSave.CommitCandidate -= OnCommitSaveCandidate;
            ESGameSave.RollbackCandidate -= OnRollbackSaveCandidate;
            ESGameSave.FinalizeCandidate -= OnFinalizeSaveCandidate;
            ESStoryInstance[] copy = new ESStoryInstance[instances.Count];
            instances.Values.CopyTo(copy, 0);
            for (int i = 0; i < copy.Length; i++) Finish(copy[i], ESStoryRunState.Aborted, ESInteractionEndReason.ModuleDisabled, false);
            presenter = null;
            base.OnDestroy();
        }
    }
}
