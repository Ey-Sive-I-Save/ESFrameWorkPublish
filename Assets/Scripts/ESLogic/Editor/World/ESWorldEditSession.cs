#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ES_Logic.Editor.World.Tests")]

namespace ES
{
    internal enum ESWorldAuthoringTool
    {
        Select,
        Terrain,
        Region,
        Poi,
        Prefab
    }

    internal enum ESWorldAuthoringSelectionKind
    {
        Map,
        Region,
        Poi,
        PrefabPlacement
    }

    internal readonly struct ESWorldEditCommitResult
    {
        public readonly bool success;
        public readonly bool conflict;
        public readonly string message;

        public ESWorldEditCommitResult(bool success, bool conflict, string message)
        {
            this.success = success;
            this.conflict = conflict;
            this.message = message ?? string.Empty;
        }
    }

    internal readonly struct ESWorldEditSessionConsistencySnapshot
    {
        public ESWorldEditSessionConsistencySnapshot(
            string ownerSessionId,
            string conflictOwnerSessionId,
            string[] activeOwnerSessionIds,
            string baselineHash,
            string draftHash,
            string actualDraftHash,
            string currentSourceHash,
            int changeCount,
            bool isDirty,
            bool hasExternalConflict,
            bool memoryDraftHashMatches,
            bool dirtyMatchesHashes,
            bool changeSetMatchesDirty,
            bool persistedBaselineMatches,
            bool persistedDraftMatches,
            bool persistedChangeSetMatches,
            bool conflictMatchesSource)
        {
            OwnerSessionId = ownerSessionId ?? string.Empty;
            ConflictOwnerSessionId = conflictOwnerSessionId ?? string.Empty;
            ActiveOwnerSessionIds = activeOwnerSessionIds ?? Array.Empty<string>();
            BaselineHash = baselineHash ?? string.Empty;
            DraftHash = draftHash ?? string.Empty;
            ActualDraftHash = actualDraftHash ?? string.Empty;
            CurrentSourceHash = currentSourceHash ?? string.Empty;
            ChangeCount = changeCount;
            IsDirty = isDirty;
            HasExternalConflict = hasExternalConflict;
            MemoryDraftHashMatches = memoryDraftHashMatches;
            DirtyMatchesHashes = dirtyMatchesHashes;
            ChangeSetMatchesDirty = changeSetMatchesDirty;
            PersistedBaselineMatches = persistedBaselineMatches;
            PersistedDraftMatches = persistedDraftMatches;
            PersistedChangeSetMatches = persistedChangeSetMatches;
            ConflictMatchesSource = conflictMatchesSource;
        }

        public string OwnerSessionId { get; }
        public string ConflictOwnerSessionId { get; }
        public IReadOnlyList<string> ActiveOwnerSessionIds { get; }
        public string BaselineHash { get; }
        public string DraftHash { get; }
        public string ActualDraftHash { get; }
        public string CurrentSourceHash { get; }
        public int ChangeCount { get; }
        public bool IsDirty { get; }
        public bool HasExternalConflict { get; }
        public bool MemoryDraftHashMatches { get; }
        public bool DirtyMatchesHashes { get; }
        public bool ChangeSetMatchesDirty { get; }
        public bool PersistedBaselineMatches { get; }
        public bool PersistedDraftMatches { get; }
        public bool PersistedChangeSetMatches { get; }
        public bool ConflictMatchesSource { get; }
        public bool Passed => MemoryDraftHashMatches
            && DirtyMatchesHashes
            && ChangeSetMatchesDirty
            && PersistedBaselineMatches
            && PersistedDraftMatches
            && PersistedChangeSetMatches
            && ConflictMatchesSource;

        public string Summary => Passed
            ? "Draft、ChangeSet、Dirty、SessionState 与 Source 冲突状态一致"
            : "会话状态存在不一致，提交前必须重新载入或修复";

        public string ToDiagnosticText()
        {
            var builder = new StringBuilder(512);
            builder.AppendLine("ES World 会话一致性诊断");
            builder.AppendLine("Owner=" + OwnerSessionId);
            builder.AppendLine("ActiveOwners=" + string.Join(",", ActiveOwnerSessionIds));
            builder.AppendLine("ConflictOwner=" + ConflictOwnerSessionId);
            builder.AppendLine("BaselineHash=" + BaselineHash);
            builder.AppendLine("DraftHash=" + DraftHash);
            builder.AppendLine("ActualDraftHash=" + ActualDraftHash);
            builder.AppendLine("CurrentSourceHash=" + CurrentSourceHash);
            builder.AppendLine("ChangeCount=" + ChangeCount);
            builder.AppendLine("Dirty=" + IsDirty);
            builder.AppendLine("ExternalConflict=" + HasExternalConflict);
            builder.AppendLine("MemoryDraftHashMatches=" + MemoryDraftHashMatches);
            builder.AppendLine("DirtyMatchesHashes=" + DirtyMatchesHashes);
            builder.AppendLine("ChangeSetMatchesDirty=" + ChangeSetMatchesDirty);
            builder.AppendLine("PersistedBaselineMatches=" + PersistedBaselineMatches);
            builder.AppendLine("PersistedDraftMatches=" + PersistedDraftMatches);
            builder.AppendLine("PersistedChangeSetMatches=" + PersistedChangeSetMatches);
            builder.AppendLine("ConflictMatchesSource=" + ConflictMatchesSource);
            builder.Append("Passed=" + Passed);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Owns one isolated authoring draft. UI and preview code must edit Draft only; Source is
    /// mutated exclusively by TryCommit after the baseline drift guard succeeds.
    /// </summary>
    internal sealed class ESWorldEditSession : IDisposable
    {
        private const string SessionPrefix = "ES.WorldEditSession.";
        private const string BaselineSuffix = ".Baseline";
        private const string BaselineHashSuffix = ".BaselineHash";
        private const string DraftSuffix = ".Draft";
        private const string ChangeSetSuffix = ".ChangeSet";

        private static readonly Dictionary<string, HashSet<ESWorldEditSession>> ActiveSessions =
            new Dictionary<string, HashSet<ESWorldEditSession>>(StringComparer.Ordinal);

        private readonly string sourceIdentity;
        private readonly string identity;
        private readonly string ownerSessionId;
        private string baselineJson;
        private string baselineHash;
        private string draftHash;
        private bool externalConflict;
        private string conflictOwnerSessionId = string.Empty;
        private bool disposed;
        private bool recoveryStateCleared;
        private readonly HashSet<string> changedPaths = new HashSet<string>(StringComparer.Ordinal);
        private ESWorldMapAsset baselineSnapshot;
        private SerializedObject serializedBaseline;

        public ESWorldMapAsset Source { get; }
        public ESWorldMapAsset Draft { get; private set; }
        public SerializedObject SerializedDraft { get; private set; }
        public bool HasExternalConflict => externalConflict;
        public bool IsDirty => !string.Equals(baselineHash, draftHash, StringComparison.Ordinal);
        public string BaselineHash => baselineHash;
        public string DraftHash => draftHash;
        public string CurrentSourceHash => Source == null ? string.Empty : ComputeStateHash(Source);
        public bool HasUntrackedDraftMutation => Draft != null
            && !string.Equals(ComputeStateHash(Draft), draftHash, StringComparison.Ordinal);
        public string OwnerSessionId => ownerSessionId;
        public string ConflictOwnerSessionId => conflictOwnerSessionId;
        public IReadOnlyCollection<string> ChangedPaths => changedPaths;
        public int ChangeCount => changedPaths.Count;

        private ESWorldEditSession(ESWorldMapAsset source, string ownerSessionId)
        {
            Source = source != null ? source : throw new ArgumentNullException(nameof(source));
            Source.EnsureAuthoringContainers();
            sourceIdentity = ResolveIdentity(source);
            this.ownerSessionId = NormalizeOwnerSessionId(ownerSessionId);
            identity = string.IsNullOrEmpty(this.ownerSessionId)
                ? sourceIdentity
                : sourceIdentity + ".Owner." + ComputeHash(this.ownerSessionId).Substring(0, 16);
            string currentJson = Serialize(source);
            string currentHash = ComputeHash(currentJson);
            string storedBaseline = SessionState.GetString(SessionPrefix + identity + BaselineSuffix, string.Empty);
            string storedHash = SessionState.GetString(SessionPrefix + identity + BaselineHashSuffix, string.Empty);
            string storedDraft = SessionState.GetString(SessionPrefix + identity + DraftSuffix, string.Empty);

            if (!string.IsNullOrEmpty(storedBaseline) && !string.IsNullOrEmpty(storedHash) && !string.IsNullOrEmpty(storedDraft))
            {
                baselineJson = storedBaseline;
                baselineHash = storedHash;
                Draft = CreateDraft(source, storedDraft);
                string storedChangeSet = SessionState.GetString(SessionPrefix + identity + ChangeSetSuffix, string.Empty);
                if (!string.IsNullOrEmpty(storedChangeSet))
                    foreach (string path in storedChangeSet.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        changedPaths.Add(path);
            }
            else
            {
                baselineJson = currentJson;
                baselineHash = currentHash;
                Draft = CreateDraft(source, currentJson);
                Persist();
            }

            draftHash = ComputeStateHash(Draft);
            SerializedDraft = new SerializedObject(Draft);
            CreateOrUpdateBaselineSnapshot();
            RebuildChangedPaths("definition");
            externalConflict = !string.Equals(baselineHash, currentHash, StringComparison.Ordinal);
            RegisterActiveSession();
            Persist();
        }

        public static ESWorldEditSession Open(ESWorldMapAsset source)
        {
            return Open(source, null);
        }

        public static ESWorldEditSession Open(ESWorldMapAsset source, string ownerSessionId)
        {
            return source == null ? null : new ESWorldEditSession(source, ownerSessionId);
        }

        public void NotifyDraftChanged(string changePath = "definition")
        {
            ThrowIfDisposed();
            SerializedDraft.ApplyModifiedProperties();
            EditorUtility.SetDirty(Draft);
            string draftJson = Serialize(Draft);
            draftHash = ComputeHash(draftJson);
            RebuildChangedPaths(changePath);
            Persist(draftJson);
        }

        public void SynchronizeDraftAfterUndoRedo()
        {
            ThrowIfDisposed();
            SerializedDraft.UpdateIfRequiredOrScript();
            Draft.EnsureAuthoringContainers();
            string draftJson = Serialize(Draft);
            draftHash = ComputeHash(draftJson);
            RebuildChangedPaths("definition");
            Persist(draftJson);
        }

        public void RevertDraft()
        {
            ThrowIfDisposed();
            Undo.RecordObject(Draft, "回退世界草稿");
            EditorJsonUtility.FromJsonOverwrite(baselineJson, Draft);
            Draft.EnsureAuthoringContainers();
            SerializedDraft.Update();
            draftHash = baselineHash;
            changedPaths.Clear();
            Persist(baselineJson);
        }

        public void ReloadFromSource()
        {
            ThrowIfDisposed();
            Source.EnsureAuthoringContainers();
            baselineJson = Serialize(Source);
            baselineHash = ComputeHash(baselineJson);
            externalConflict = false;
            conflictOwnerSessionId = string.Empty;
            Undo.RecordObject(Draft, "重新载入世界资产");
            EditorJsonUtility.FromJsonOverwrite(baselineJson, Draft);
            Draft.EnsureAuthoringContainers();
            SerializedDraft.Update();
            CreateOrUpdateBaselineSnapshot();
            draftHash = baselineHash;
            changedPaths.Clear();
            Persist(baselineJson);
        }

        public ESWorldEditCommitResult TryCommit()
        {
            ThrowIfDisposed();
            SerializedDraft.ApplyModifiedProperties();
            if (RefreshExternalConflict())
                return new ESWorldEditCommitResult(false, true, "正式地图已被其他窗口或工具修改。请先检查差异，再重新载入基线。");
            if (Draft == null)
                return new ESWorldEditCommitResult(false, false, "草稿不存在，无法提交。");
            if (!Draft.Validate(out string validationError))
                return new ESWorldEditCommitResult(false, false, "草稿验证失败：" + validationError);

            string draftJson = Serialize(Draft);
            if (string.Equals(baselineHash, ComputeHash(draftJson), StringComparison.Ordinal))
                return new ESWorldEditCommitResult(true, false, "没有需要提交的草稿变更。");

            string sourceBeforeCommit = Serialize(Source);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("提交 ES 世界草稿");
            Undo.RecordObject(Source, "提交 ES 世界草稿");
            EditorJsonUtility.FromJsonOverwrite(draftJson, Source);
            ESWorldMapSaveResult save = ESWorldMapAuthoringUtility.Save(Source);
            if (!save.success)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                EditorJsonUtility.FromJsonOverwrite(sourceBeforeCommit, Source);
                EditorUtility.SetDirty(Source);
                AssetDatabase.SaveAssetIfDirty(Source);
                return new ESWorldEditCommitResult(false, false, "提交写入失败，正式地图已恢复到提交前状态：" + save.error);
            }
            Undo.CollapseUndoOperations(undoGroup);

            baselineJson = Serialize(Source);
            baselineHash = ComputeHash(baselineJson);
            externalConflict = false;
            conflictOwnerSessionId = string.Empty;
            EditorJsonUtility.FromJsonOverwrite(baselineJson, Draft);
            SerializedDraft.Update();
            CreateOrUpdateBaselineSnapshot();
            draftHash = baselineHash;
            changedPaths.Clear();
            Persist(baselineJson);
            NotifyOtherSessionsSourceCommitted();
            return new ESWorldEditCommitResult(true, false,
                save.contentChanged ? "草稿已提交，地图内容签名已更新。" : "草稿已提交。");
        }

        public void Dispose()
        {
            if (disposed) return;

            // OnDisable/Domain Reload may arrive between a direct Draft mutation and
            // NotifyDraftChanged/SynchronizeDraftAfterUndoRedo. Capture that latest
            // in-memory state before destroying the temporary object. This is recovery
            // persistence only; it never commits Source or changes the formal asset.
            try
            {
                if (!recoveryStateCleared && Draft != null)
                {
                    SerializedDraft?.ApplyModifiedProperties();
                    string draftJson = Serialize(Draft);
                    draftHash = ComputeHash(draftJson);
                    RebuildChangedPaths("definition");
                    Persist(draftJson);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESWorldEditSession] 关闭前无法持久化最新草稿恢复快照：" + exception.Message);
            }

            disposed = true;
            UnregisterActiveSession();
            SerializedDraft?.Dispose();
            SerializedDraft = null;
            if (Draft != null) UnityEngine.Object.DestroyImmediate(Draft);
            Draft = null;
            serializedBaseline?.Dispose();
            serializedBaseline = null;
            if (baselineSnapshot != null) UnityEngine.Object.DestroyImmediate(baselineSnapshot);
            baselineSnapshot = null;
        }

        internal void ClearRecoveryState()
        {
            recoveryStateCleared = true;
            SessionState.EraseString(SessionPrefix + identity + BaselineSuffix);
            SessionState.EraseString(SessionPrefix + identity + BaselineHashSuffix);
            SessionState.EraseString(SessionPrefix + identity + DraftSuffix);
            SessionState.EraseString(SessionPrefix + identity + ChangeSetSuffix);
        }

        internal static string ComputeStateHash(ESWorldMapAsset asset)
        {
            return asset == null ? string.Empty : ComputeHash(Serialize(asset));
        }

        internal bool RefreshExternalConflict()
        {
            ThrowIfDisposed();
            externalConflict = !string.Equals(baselineHash, ComputeStateHash(Source), StringComparison.Ordinal);
            if (!externalConflict) conflictOwnerSessionId = string.Empty;
            return externalConflict;
        }

        internal static int GetActiveSessionCount(ESWorldMapAsset source)
        {
            if (source == null) return 0;
            string key = ResolveIdentity(source);
            return ActiveSessions.TryGetValue(key, out HashSet<ESWorldEditSession> sessions)
                ? sessions.Count : 0;
        }

        internal ESWorldEditSessionConsistencySnapshot CaptureConsistencySnapshot()
        {
            ThrowIfDisposed();
            string actualDraftJson = Serialize(Draft);
            string actualDraftHash = ComputeHash(actualDraftJson);
            string sourceHash = ComputeStateHash(Source);
            bool actualDirty = !string.Equals(baselineHash, actualDraftHash, StringComparison.Ordinal);
            bool actualConflict = !string.Equals(baselineHash, sourceHash, StringComparison.Ordinal);
            string persistedBaseline = SessionState.GetString(
                SessionPrefix + identity + BaselineSuffix, string.Empty);
            string persistedBaselineHash = SessionState.GetString(
                SessionPrefix + identity + BaselineHashSuffix, string.Empty);
            string persistedDraft = SessionState.GetString(
                SessionPrefix + identity + DraftSuffix, string.Empty);
            string persistedChangeSet = SessionState.GetString(
                SessionPrefix + identity + ChangeSetSuffix, string.Empty);
            string[] persistedPaths = persistedChangeSet
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] memoryPaths = changedPaths
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] activeOwners = GetActiveOwnerSessionIds();

            return new ESWorldEditSessionConsistencySnapshot(
                ownerSessionId,
                conflictOwnerSessionId,
                activeOwners,
                baselineHash,
                draftHash,
                actualDraftHash,
                sourceHash,
                changedPaths.Count,
                IsDirty,
                externalConflict,
                string.Equals(draftHash, actualDraftHash, StringComparison.Ordinal),
                IsDirty == actualDirty,
                actualDirty ? changedPaths.Count > 0 : changedPaths.Count == 0,
                string.Equals(persistedBaseline, baselineJson, StringComparison.Ordinal)
                    && string.Equals(persistedBaselineHash, baselineHash, StringComparison.Ordinal)
                    && string.Equals(ComputeHash(persistedBaseline), persistedBaselineHash, StringComparison.Ordinal),
                string.Equals(ComputeHash(persistedDraft), actualDraftHash, StringComparison.Ordinal),
                persistedPaths.SequenceEqual(memoryPaths, StringComparer.Ordinal),
                externalConflict == actualConflict);
        }

        private void Persist(string draftJson = null)
        {
            recoveryStateCleared = false;
            SessionState.SetString(SessionPrefix + identity + BaselineSuffix, baselineJson ?? string.Empty);
            SessionState.SetString(SessionPrefix + identity + BaselineHashSuffix, baselineHash ?? string.Empty);
            SessionState.SetString(SessionPrefix + identity + DraftSuffix, draftJson ?? Serialize(Draft));
            SessionState.SetString(
                SessionPrefix + identity + ChangeSetSuffix,
                string.Join("\n", changedPaths.OrderBy(value => value, StringComparer.Ordinal)));
        }

        private void CreateOrUpdateBaselineSnapshot()
        {
            if (baselineSnapshot == null)
            {
                baselineSnapshot = CreateDraft(Source, baselineJson);
                baselineSnapshot.name = Source.name + " (Baseline Snapshot)";
                serializedBaseline = new SerializedObject(baselineSnapshot);
                return;
            }

            EditorJsonUtility.FromJsonOverwrite(baselineJson, baselineSnapshot);
            serializedBaseline.UpdateIfRequiredOrScript();
        }

        private void RebuildChangedPaths(string fallbackPath)
        {
            changedPaths.Clear();
            if (string.Equals(baselineHash, draftHash, StringComparison.Ordinal)) return;
            if (SerializedDraft == null || serializedBaseline == null)
            {
                AddFallbackPath(fallbackPath);
                return;
            }

            SerializedDraft.UpdateIfRequiredOrScript();
            serializedBaseline.UpdateIfRequiredOrScript();
            SerializedProperty property = SerializedDraft.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (IsUnityObjectMetadata(property)) continue;
                if (property.propertyType == SerializedPropertyType.Generic
                    && property.hasVisibleChildren)
                {
                    enterChildren = true;
                    continue;
                }
                SerializedProperty baselineProperty = serializedBaseline.FindProperty(property.propertyPath);
                if (baselineProperty == null || !SerializedProperty.DataEquals(property, baselineProperty))
                    changedPaths.Add(property.propertyPath);
            }

            if (changedPaths.Count == 0) AddFallbackPath(fallbackPath);
        }

        private static bool IsUnityObjectMetadata(SerializedProperty property)
        {
            if (property == null || property.depth != 0) return false;
            switch (property.propertyPath)
            {
                case "m_ObjectHideFlags":
                case "m_CorrespondingSourceObject":
                case "m_PrefabInstance":
                case "m_PrefabAsset":
                case "m_GameObject":
                case "m_Enabled":
                case "m_EditorHideFlags":
                case "m_Script":
                case "m_Name":
                case "m_EditorClassIdentifier":
                    return true;
                default:
                    return false;
            }
        }

        private void AddFallbackPath(string fallbackPath)
        {
            changedPaths.Add(string.IsNullOrWhiteSpace(fallbackPath) ? "definition" : fallbackPath.Trim());
        }

        private void RegisterActiveSession()
        {
            if (!ActiveSessions.TryGetValue(sourceIdentity, out HashSet<ESWorldEditSession> sessions))
            {
                sessions = new HashSet<ESWorldEditSession>();
                ActiveSessions.Add(sourceIdentity, sessions);
            }
            sessions.Add(this);
        }

        private string[] GetActiveOwnerSessionIds()
        {
            if (!ActiveSessions.TryGetValue(sourceIdentity, out HashSet<ESWorldEditSession> sessions))
                return Array.Empty<string>();
            return sessions
                .Where(session => session != null && !session.disposed)
                .Select(session => session.ownerSessionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private void UnregisterActiveSession()
        {
            if (!ActiveSessions.TryGetValue(sourceIdentity, out HashSet<ESWorldEditSession> sessions)) return;
            sessions.Remove(this);
            if (sessions.Count == 0) ActiveSessions.Remove(sourceIdentity);
        }

        private void NotifyOtherSessionsSourceCommitted()
        {
            if (!ActiveSessions.TryGetValue(sourceIdentity, out HashSet<ESWorldEditSession> sessions)) return;
            foreach (ESWorldEditSession session in sessions)
            {
                if (session == null || ReferenceEquals(session, this) || session.disposed) continue;
                session.externalConflict = true;
                session.conflictOwnerSessionId = ownerSessionId;
            }
        }

        private static ESWorldMapAsset CreateDraft(ESWorldMapAsset source, string json)
        {
            ESWorldMapAsset draft = UnityEngine.Object.Instantiate(source);
            draft.name = source.name + " (Draft)";
            draft.hideFlags = HideFlags.HideAndDontSave;
            if (!string.IsNullOrEmpty(json)) EditorJsonUtility.FromJsonOverwrite(json, draft);
            draft.EnsureAuthoringContainers();
            return draft;
        }

        private static string ResolveIdentity(ESWorldMapAsset source)
        {
            string path = AssetDatabase.GetAssetPath(source);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? "Instance." + source.GetInstanceID() : "Guid." + guid;
        }

        private static string NormalizeOwnerSessionId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string Serialize(ESWorldMapAsset asset)
        {
            return asset == null ? string.Empty : EditorJsonUtility.ToJson(asset, false);
        }

        private static string ComputeHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ESWorldEditSession));
        }
    }
}
#endif
