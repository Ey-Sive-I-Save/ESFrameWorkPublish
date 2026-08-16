#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

        private readonly string identity;
        private string baselineJson;
        private string baselineHash;
        private string draftHash;
        private bool externalConflict;
        private bool disposed;
        private readonly HashSet<string> changedPaths = new HashSet<string>(StringComparer.Ordinal);

        public ESWorldMapAsset Source { get; }
        public ESWorldMapAsset Draft { get; private set; }
        public SerializedObject SerializedDraft { get; private set; }
        public bool HasExternalConflict => externalConflict;
        public bool IsDirty => !string.Equals(baselineHash, draftHash, StringComparison.Ordinal);
        public string BaselineHash => baselineHash;
        public IReadOnlyCollection<string> ChangedPaths => changedPaths;
        public int ChangeCount => changedPaths.Count;

        private ESWorldEditSession(ESWorldMapAsset source)
        {
            Source = source != null ? source : throw new ArgumentNullException(nameof(source));
            identity = ResolveIdentity(source);
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
            if (!string.Equals(baselineHash, draftHash, StringComparison.Ordinal) && changedPaths.Count == 0)
                changedPaths.Add("definition");
            externalConflict = !string.Equals(baselineHash, currentHash, StringComparison.Ordinal);
            SerializedDraft = new SerializedObject(Draft);
        }

        public static ESWorldEditSession Open(ESWorldMapAsset source)
        {
            return source == null ? null : new ESWorldEditSession(source);
        }

        public void NotifyDraftChanged(string changePath = "definition")
        {
            ThrowIfDisposed();
            SerializedDraft.ApplyModifiedProperties();
            EditorUtility.SetDirty(Draft);
            string draftJson = Serialize(Draft);
            draftHash = ComputeHash(draftJson);
            if (!string.IsNullOrWhiteSpace(changePath)) changedPaths.Add(changePath.Trim());
            Persist(draftJson);
        }

        public void SynchronizeDraftAfterUndoRedo()
        {
            ThrowIfDisposed();
            SerializedDraft.UpdateIfRequiredOrScript();
            string draftJson = Serialize(Draft);
            draftHash = ComputeHash(draftJson);
            if (string.Equals(baselineHash, draftHash, StringComparison.Ordinal))
                changedPaths.Clear();
            else if (changedPaths.Count == 0)
                changedPaths.Add("definition");
            Persist(draftJson);
        }

        public void RevertDraft()
        {
            ThrowIfDisposed();
            Undo.RecordObject(Draft, "回退世界草稿");
            EditorJsonUtility.FromJsonOverwrite(baselineJson, Draft);
            SerializedDraft.Update();
            draftHash = baselineHash;
            changedPaths.Clear();
            Persist(baselineJson);
        }

        public void ReloadFromSource()
        {
            ThrowIfDisposed();
            baselineJson = Serialize(Source);
            baselineHash = ComputeHash(baselineJson);
            externalConflict = false;
            Undo.RecordObject(Draft, "重新载入世界资产");
            EditorJsonUtility.FromJsonOverwrite(baselineJson, Draft);
            SerializedDraft.Update();
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
            EditorJsonUtility.FromJsonOverwrite(baselineJson, Draft);
            SerializedDraft.Update();
            draftHash = baselineHash;
            changedPaths.Clear();
            Persist(baselineJson);
            return new ESWorldEditCommitResult(true, false,
                save.contentChanged ? "草稿已提交，地图内容签名已更新。" : "草稿已提交。");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SerializedDraft?.Dispose();
            SerializedDraft = null;
            if (Draft != null) UnityEngine.Object.DestroyImmediate(Draft);
            Draft = null;
        }

        internal void ClearRecoveryState()
        {
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
            return externalConflict;
        }

        private void Persist(string draftJson = null)
        {
            SessionState.SetString(SessionPrefix + identity + BaselineSuffix, baselineJson ?? string.Empty);
            SessionState.SetString(SessionPrefix + identity + BaselineHashSuffix, baselineHash ?? string.Empty);
            SessionState.SetString(SessionPrefix + identity + DraftSuffix, draftJson ?? Serialize(Draft));
            SessionState.SetString(SessionPrefix + identity + ChangeSetSuffix, string.Join("\n", changedPaths));
        }

        private static ESWorldMapAsset CreateDraft(ESWorldMapAsset source, string json)
        {
            ESWorldMapAsset draft = UnityEngine.Object.Instantiate(source);
            draft.name = source.name + " (Draft)";
            draft.hideFlags = HideFlags.HideAndDontSave;
            if (!string.IsNullOrEmpty(json)) EditorJsonUtility.FromJsonOverwrite(json, draft);
            return draft;
        }

        private static string ResolveIdentity(ESWorldMapAsset source)
        {
            string path = AssetDatabase.GetAssetPath(source);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? "Instance." + source.GetInstanceID() : "Guid." + guid;
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
