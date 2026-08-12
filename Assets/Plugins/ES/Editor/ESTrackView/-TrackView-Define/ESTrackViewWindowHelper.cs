using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ES;
using UnityEditor;
using UnityEngine;
public enum TrackMoveCommand
{
    StepUp,
    StepDown,
    ToMovableTop,
    ToBottom
}

public class ESTrackViewWindowHelper : EditorInvoker_Level0

{
    private const string AutoOpenFromSelectionPrefKey = "ES.TrackView.AutoOpenFromSelection";
    private const string AutoFollowPreviewEntityPrefKey = "ES.TrackView.AutoFollowPreviewEntity";
    private static readonly Dictionary<TrackItemType, List<(string name, Type type)>> AllTrackItemTypes =
        new Dictionary<TrackItemType, List<(string name, Type type)>>();
    private static IEditorTrackSupport_GetSequence s_PendingSelectionTrackContainer;
    private static bool s_SelectionTrackRefreshScheduled;

    public static bool AutoOpenFromSelection
    {
        get => EditorPrefs.GetBool(AutoOpenFromSelectionPrefKey, false);
        set
        {
            EditorPrefs.SetBool(AutoOpenFromSelectionPrefKey, value);
            if (!value)
                CancelPendingSelectionTrackRefresh();
        }
    }

    public static bool AutoFollowPreviewEntity
    {
        get => EditorPrefs.GetBool(AutoFollowPreviewEntityPrefKey, false);
        set => EditorPrefs.SetBool(AutoFollowPreviewEntityPrefKey, value);
    }

    public override void InitInvoke()
    {
        Selection.selectionChanged -= ForTrackWindowSelection;
        Selection.selectionChanged += ForTrackWindowSelection;
    }

    private static void ForTrackWindowSelection()
    {
        if (AutoOpenFromSelection
            && Selection.activeObject is IEditorTrackSupport_GetSequence supportSequence
            && supportSequence.Sequence != null)
        {
            ScheduleOpenAndRefreshTrackWindow(supportSequence);
        }
    }

    internal static IReadOnlyList<(string name, Type type)> GetTrackItemTypes(TrackItemType itemType)
    {
        return AllTrackItemTypes.TryGetValue(itemType, out List<(string name, Type type)> list)
            ? list
            : Array.Empty<(string name, Type type)>();
    }

    internal static void RegisterTrackItemType(TrackItemType itemType, string menuName, Type type)
    {
        if (!AllTrackItemTypes.TryGetValue(itemType, out List<(string name, Type type)> list))
        {
            list = new List<(string name, Type type)>();
            AllTrackItemTypes.Add(itemType, list);
        }

        if (list.Any(entry => entry.type == type))
            return;

        list.Add((menuName, type));
        list.Sort((left, right) =>
        {
            int menuCompare = string.Compare(left.name, right.name, StringComparison.Ordinal);
            if (menuCompare != 0)
                return menuCompare;
            return string.Compare(left.type?.FullName, right.type?.FullName, StringComparison.Ordinal);
        });
    }

    private static void ScheduleOpenAndRefreshTrackWindow(IEditorTrackSupport_GetSequence supportSequence)
    {
        if (supportSequence == null || supportSequence.Sequence == null)
            return;

        s_PendingSelectionTrackContainer = supportSequence;
        if (s_SelectionTrackRefreshScheduled)
            return;

        s_SelectionTrackRefreshScheduled = true;
        EditorApplication.delayCall += FlushOpenAndRefreshTrackWindow;
    }

    public static void CancelPendingSelectionTrackRefresh()
    {
        if (s_SelectionTrackRefreshScheduled)
            EditorApplication.delayCall -= FlushOpenAndRefreshTrackWindow;

        s_PendingSelectionTrackContainer = null;
        s_SelectionTrackRefreshScheduled = false;
    }

    private static void FlushOpenAndRefreshTrackWindow()
    {
        EditorApplication.delayCall -= FlushOpenAndRefreshTrackWindow;
        s_SelectionTrackRefreshScheduled = false;

        IEditorTrackSupport_GetSequence supportSequence = s_PendingSelectionTrackContainer;
        s_PendingSelectionTrackContainer = null;
        if (supportSequence == null || supportSequence.Sequence == null)
            return;

        ESTrackViewWindow.TryUpdateTrackSequence(supportSequence);
        if (ESTrackViewWindow.window != null)
        {
            ESTrackViewWindow.window.ForceRefreshClipLayoutNow();
            ESTrackViewWindow.window.Repaint();
        }
    }

    public static void AddNewTrackItemToCurrentSequence(Type itemType)
    {
        if (ESTrackViewWindow.Sequence != null && ESTrackViewWindow.window != null && ESTrackViewWindow.window.leftPanel != null)
        {
            if (itemType != null && typeof(ITrackItem).IsAssignableFrom(itemType) && itemType.GetConstructor(Type.EmptyTypes) != null)
            {
                var newItem = Activator.CreateInstance(itemType) as ITrackItem;
                if (newItem != null)
                {
                    if (newItem is IStableTrackItem stableTrack)
                        stableTrack.EnsureStableTrackIdentity();
                    UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
                    if (undoTarget != null)
                        Undo.RecordObject(undoTarget, "添加轨道");

                    if (ESTrackViewWindow.Sequence.TryAddTrackItem(newItem))
                    {
                        var item = new ESEditorTrackItem().InitWithItem(newItem);
                        ESTrackViewWindow.window.leftPanel.Add(item);
                        ESTrackViewWindow.window.Items.Add(item);
                        ESTrackViewWindow.window.UpdateTimelineContentHeight();
                        ESTrackViewWindow.window.ApplyTrackPanelLayout(false);
                        ESTrackViewWindow.window.SelectTrack(item);
                        ESTrackViewWindow.window.ApplyAuthoringChange(
                            null,
                            ESTrackAuthoringChangeFlags.StructuralEdit,
                            "添加轨道");
                    }
                }
            }
        }
    }

    public static bool MoveTrackItemInCurrentSequence(ESEditorTrackItem editorTrack, TrackMoveCommand command)
    {
        if (editorTrack == null || editorTrack.item == null)
            return false;

        if (editorTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可参与排序。");
            return false;
        }

        ITrackSequenceMutableOrder mutableOrder = ESTrackViewWindow.Sequence as ITrackSequenceMutableOrder;
        if (mutableOrder == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前序列不支持轨道排序。");
            return false;
        }

        int oldIndex = mutableOrder.IndexOfTrackItem(editorTrack.item);
        int minIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        if (oldIndex < minIndex)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道区域不可参与排序。");
            return false;
        }

        int newIndex = oldIndex;
        switch (command)
        {
            case TrackMoveCommand.StepUp:
                newIndex = oldIndex - 1;
                break;
            case TrackMoveCommand.StepDown:
                newIndex = oldIndex + 1;
                break;
            case TrackMoveCommand.ToMovableTop:
                newIndex = minIndex;
                break;
            case TrackMoveCommand.ToBottom:
                newIndex = mutableOrder.TrackItemCount - 1;
                break;
        }

        newIndex = ESTrackViewIconUtility.ClampUserTrackInsertIndex(newIndex, mutableOrder.TrackItemCount - 1);
        if (newIndex == oldIndex)
            return false;

        return MoveTrackItemToFinalIndexInCurrentSequence(editorTrack, newIndex);
    }

    public static bool MoveTrackItemToFinalIndexInCurrentSequence(ESEditorTrackItem editorTrack, int targetFinalIndex)
    {
        if (editorTrack == null || editorTrack.item == null)
            return false;

        if (editorTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可参与排序。");
            return false;
        }

        ITrackSequenceMutableOrder mutableOrder = ESTrackViewWindow.Sequence as ITrackSequenceMutableOrder;
        if (mutableOrder == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前序列不支持轨道排序。");
            return false;
        }

        int oldIndex = mutableOrder.IndexOfTrackItem(editorTrack.item);
        int minIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        if (oldIndex < minIndex)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道区域不可参与排序。");
            return false;
        }

        int finalIndex = Mathf.Clamp(targetFinalIndex, minIndex, mutableOrder.TrackItemCount - 1);
        if (finalIndex == oldIndex)
            return false;

        UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        if (undoTarget != null)
            Undo.RecordObject(undoTarget, "调整轨道顺序");

        if (!mutableOrder.TryMoveTrackItem(editorTrack.item, finalIndex))
            return false;

        ESTrackViewWindowHelper.SaveContainerChanges("调整轨道顺序");
        return true;
    }

    public static bool MoveTrackItemToIndexInCurrentSequence(ESEditorTrackItem editorTrack, int targetIndex)
    {
        if (editorTrack == null || editorTrack.item == null)
            return false;

        if (editorTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可参与排序。");
            return false;
        }

        ITrackSequenceMutableOrder mutableOrder = ESTrackViewWindow.Sequence as ITrackSequenceMutableOrder;
        if (mutableOrder == null)
        {
            Debug.LogWarning("[轨道编辑器] 当前序列不支持轨道排序。");
            return false;
        }

        int oldIndex = mutableOrder.IndexOfTrackItem(editorTrack.item);
        int minIndex = ESTrackViewIconUtility.ProtectedBasicTrackCount;
        if (oldIndex < minIndex)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道区域不可参与排序。");
            return false;
        }

        int insertIndex = ESTrackViewIconUtility.ClampUserTrackInsertIndex(targetIndex, mutableOrder.TrackItemCount);
        if (insertIndex > oldIndex)
            insertIndex--;

        insertIndex = Mathf.Clamp(insertIndex, minIndex, mutableOrder.TrackItemCount - 1);
        if (insertIndex == oldIndex)
            return false;

        UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        if (undoTarget != null)
            Undo.RecordObject(undoTarget, "调整轨道顺序");

        if (!mutableOrder.TryMoveTrackItem(editorTrack.item, insertIndex))
            return false;

        ESTrackViewWindowHelper.SaveContainerChanges("调整轨道顺序");
        return true;
    }

    public static void RemoveTrackItemToCurrentSequence(ESEditorTrackItem ediTrack)
    {
        if (ediTrack == null || ediTrack.IsProtectedBasicTrack)
        {
            Debug.LogWarning("[轨道编辑器] 基础轨道不可删除。");
            return;
        }

        if (ESTrackViewWindow.Sequence != null && ESTrackViewWindow.window != null)
        {
            var item = ediTrack.item;
            if (item != null && typeof(ITrackItem).IsAssignableFrom(item.GetType()))
            {


                UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
                if (undoTarget != null)
                    Undo.RecordObject(undoTarget, "删除轨道");

                if (ESTrackViewWindow.Sequence.TryRemoveTrackItem(item))
                {
                    ESTrackViewWindow.window.SetTrackCollapsedState(item, false);
                    if (ESTrackViewWindow.window.leftPanel != null)
                        ESTrackViewWindow.window.leftPanel.Remove(ediTrack);
                    ESTrackViewWindow.window.HandleTrackItemRemoved(ediTrack);
                    ESTrackViewWindow.window.ApplyAuthoringChange(
                        null,
                        ESTrackAuthoringChangeFlags.StructuralEdit,
                        "删除轨道");
                }

            }
        }
    }

    public static void RemoveTrackClipToCurrentSequence(ESEditorTrackClip clip)
    {
        if (clip == null || clip.trackClip == null)
            return;

        if (ESTrackViewWindow.Sequence != null && ESTrackViewWindow.window != null)
        {
            var item = clip.trackClip;
            if (item != null && typeof(ITrackClip).IsAssignableFrom(item.GetType()))
            {

                foreach (var trackItemEditor in ESTrackViewWindow.window.Items)
                {
                    if (trackItemEditor == null || trackItemEditor.item == null)
                        continue;

                    if (!trackItemEditor.TrackClips.Contains(clip))
                        continue;

                    UnityEngine.Object undoTarget = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
                    if (undoTarget != null)
                        Undo.RecordObject(undoTarget, "删除片段");

                    trackItemEditor.RemoveClip(clip);
                    ESTrackViewWindow.window.HandleTrackClipRemoved(clip);
                    ESTrackViewWindow.window.ApplyAuthoringChange(
                        null,
                        ESTrackAuthoringChangeFlags.StructuralEdit,
                        "删除片段");
                    break;
                }
            }
        }
    }

    public static void EditClip(ESEditorTrackClip clip)
    {
        if (clip == null || clip.trackClip == null || ESTrackViewWindow.window == null)
            return;

        ESTrackViewWindow trackWindow = ESTrackViewWindow.window;
        trackWindow.EditClip(clip);
    }

    public static void SaveContainerChanges(string changeSource = null)
    {
        if (ESTrackViewWindow.TrackContainer != null)
        {
            UnityEngine.Object target = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
            if (target == null)
                return;

            ESDesignUtility.SafeEditor.Wrap_SetDirty(target);
            SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
            ESTrackViewWindow.window?.ScheduleAutoSave(string.IsNullOrEmpty(changeSource) ? "时间轴编辑" : changeSource);
        }
    }

    public static void SaveContainerNow()
    {
        if (!(ESTrackViewWindow.TrackContainer is UnityEngine.Object target))
            return;

        if (ESTrackViewWindow.window != null
            && !ESTrackViewWindow.window.ConfirmManualSaveWhenExternalConflict())
            return;

        try
        {
            ESTrackViewWindow.window?.UpdateSaveStatus("保存中", ESTrackViewTheme.StatusModified, "正在立即保存当前时间轴。", "用户点击立即保存");
            ESTrackViewWindow.window?.FlushAutoSaveImmediate();
            ESDesignUtility.SafeEditor.Wrap_SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);

            if (EditorUtility.IsDirty(target))
                ESTrackViewWindow.window?.UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError, "时间轴仍有未保存修改，请查看 Console 后重试。", "用户点击立即保存");
            else
            {
                ESTrackViewWindow.window?.UpdateSaveStatus("已保存", ESTrackViewTheme.StatusReady, "当前时间轴已保存。", "用户点击立即保存");
                ESTrackViewWindow.window?.NotifyTrackAssetSaved();
            }
        }
        catch (Exception e)
        {
            ESTrackViewWindow.window?.UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError, "立即保存发生异常：" + e.Message, "用户点击立即保存");
            Debug.LogException(e, target);
        }
        SkillSequenceRuntimeCache.NotifySequenceChanged(ESTrackViewWindow.Sequence);
    }

    public static void SaveContainerAsNewAsset()
    {
        if (!(ESTrackViewWindow.TrackContainer is ScriptableObject source)
            || ESTrackViewWindow.Sequence == null)
        {
            EditorUtility.DisplayDialog("保存时间轴副本", "当前没有可复制的时间轴资产。", "确定");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string sourceName = string.IsNullOrEmpty(sourcePath) ? source.name : System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        string path = EditorUtility.SaveFilePanelInProject(
            "保存时间轴副本",
            string.IsNullOrEmpty(sourceName) ? "时间轴副本" : sourceName + "_副本",
            "asset",
            "选择项目内路径保存当前时间轴副本。不会覆盖原资产。",
            string.IsNullOrEmpty(sourcePath) ? "Assets" : System.IO.Path.GetDirectoryName(sourcePath));

        if (string.IsNullOrEmpty(path))
            return;

        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
        IEditorTrackSupport_GetSequence sourceSupport = ESTrackViewWindow.TrackContainer;
        string sourceGuid = string.IsNullOrEmpty(sourcePath) ? string.Empty : AssetDatabase.AssetPathToGUID(sourcePath);
        ScriptableObject clone = null;
        bool cloneAssetCreated = false;
        try
        {
            clone = UnityEngine.Object.Instantiate(source);
            clone.name = System.IO.Path.GetFileNameWithoutExtension(uniquePath);
            AssetDatabase.CreateAsset(clone, uniquePath);
            cloneAssetCreated = true;
            // 只提交本次新建的副本，不能因“保存为新资产”顺带落盘其他 Dirty 资产。
            AssetDatabase.SaveAssetIfDirty(clone);
            AssetDatabase.ImportAsset(uniquePath, ImportAssetOptions.ForceUpdate);

            UnityEngine.Object savedAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(uniquePath);
            if (!(savedAsset is IEditorTrackSupport_GetSequence copied)
                || copied.Sequence == null)
            {
                RollbackCreatedContainerAsset(uniquePath, ref clone, cloneAssetCreated);
                EditorUtility.DisplayDialog(
                    "保存时间轴副本失败",
                    "副本已经写入，但重新加载后没有找到有效的时间轴序列。请检查 Console 和资产内容。\n路径：" + uniquePath,
                    "确定");
                return;
            }

            string copyGuid = AssetDatabase.AssetPathToGUID(uniquePath);
            if (savedAsset == source
                || (!string.IsNullOrEmpty(sourceGuid) && string.Equals(sourceGuid, copyGuid, StringComparison.OrdinalIgnoreCase))
                || !AreTrackSequenceRecordsIndependent(sourceSupport?.Sequence, copied.Sequence))
            {
                RollbackCreatedContainerAsset(uniquePath, ref clone, cloneAssetCreated);
                EditorUtility.DisplayDialog(
                    "保存时间轴副本失败",
                    "副本已写入，但独立性复核未通过：原资产与副本可能共享序列对象或稳定身份。请检查资产内容。\n路径：" + uniquePath,
                    "确定");
                return;
            }

            Selection.activeObject = savedAsset;
            EditorGUIUtility.PingObject(savedAsset);
            ESTrackViewWindow.TryUpdateTrackSequence(copied);
            ESTrackViewWindow.window?.UpdateSaveStatus("已保存", ESTrackViewTheme.StatusReady, "已保存为新资产：" + uniquePath);
        }
        catch (Exception e)
        {
            RollbackCreatedContainerAsset(uniquePath, ref clone, cloneAssetCreated);
            Debug.LogException(e);
            EditorUtility.DisplayDialog(
                "保存时间轴副本失败",
                "复制时间轴时发生异常。原资产未切换，请查看 Console。\n目标路径：" + uniquePath,
                "确定");
        }
        finally
        {
            if (clone != null && AssetDatabase.GetAssetPath(clone) != uniquePath)
                UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    private static void RollbackCreatedContainerAsset(string assetPath, ref ScriptableObject clone, bool cloneAssetCreated)
    {
        if (!cloneAssetCreated || string.IsNullOrEmpty(assetPath))
            return;

        try
        {
            // 只回滚本次 CreateAsset 成功创建的路径；GenerateUniqueAssetPath 已经保证不会覆盖原资产。
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null)
                    clone = null;
            }
        }
        catch (Exception rollbackException)
        {
            Debug.LogError("保存时间轴副本失败后的回滚未完成：" + rollbackException.Message);
        }
    }

    private static bool AreTrackSequenceRecordsIndependent(ITrackSequence source, ITrackSequence copy)
    {
        if (source == null || copy == null || ReferenceEquals(source, copy))
            return false;

        List<ITrackItem> sourceTracks = source.Tracks != null ? source.Tracks.ToList() : new List<ITrackItem>();
        List<ITrackItem> copyTracks = copy.Tracks != null ? copy.Tracks.ToList() : new List<ITrackItem>();
        if (sourceTracks.Count != copyTracks.Count)
            return false;

        for (int i = 0; i < sourceTracks.Count; i++)
        {
            ITrackItem sourceTrack = sourceTracks[i];
            ITrackItem copyTrack = copyTracks[i];
            if (sourceTrack == null || copyTrack == null || ReferenceEquals(sourceTrack, copyTrack))
                return false;

            List<ITrackClip> sourceClips = sourceTrack.Clips != null ? sourceTrack.Clips.ToList() : new List<ITrackClip>();
            List<ITrackClip> copyClips = copyTrack.Clips != null ? copyTrack.Clips.ToList() : new List<ITrackClip>();
            if (sourceClips.Count != copyClips.Count)
                return false;

            for (int j = 0; j < sourceClips.Count; j++)
            {
                if (sourceClips[j] == null || copyClips[j] == null || ReferenceEquals(sourceClips[j], copyClips[j]))
                    return false;
            }
        }

        return true;
    }

    internal static string BuildCurrentSequenceSummary()
    {
        ITrackSequence sequence = ESTrackViewWindow.Sequence;
        if (sequence == null)
            return string.Empty;

        UnityEngine.Object asset = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        string assetPath = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
        string assetGuid = !string.IsNullOrEmpty(assetPath) ? AssetDatabase.AssetPathToGUID(assetPath) : string.Empty;
        List<string> warnings = new List<string>(32);
        List<string> infos = new List<string>(16);
        Dictionary<ITrackClip, string> clipWarnings = new Dictionary<ITrackClip, string>();
        ESTrackViewWindow.ValidateSequence(sequence, warnings, infos, clipWarnings);

        StringBuilder builder = new StringBuilder(2048);
        builder.AppendLine("ES-TrackSummary/1");
        builder.Append("资产名称：").AppendLine(asset != null ? asset.name : "<未命名>");
        builder.Append("资产 GUID：").AppendLine(string.IsNullOrEmpty(assetGuid) ? "<非资产对象>" : assetGuid);
        builder.Append("资产路径：").AppendLine(string.IsNullOrEmpty(assetPath) ? "<非资产对象>" : assetPath);
        builder.Append("序列名称：").AppendLine(sequence.Name ?? "<未命名序列>");
        ESTrackIdentity.ValidateSequenceIdentity(
            sequence,
            out int trackIdentityIssues,
            out int clipIdentityIssues,
            out int unsupportedTrackCount,
            out int unsupportedClipCount);
        builder.Append("身份 Schema：Track v").Append(ESTrackIdentity.CurrentTrackSchema)
            .Append(" / Clip v").Append(ESTrackIdentity.CurrentClipSchema)
            .Append("，问题 ").Append((trackIdentityIssues + clipIdentityIssues).ToString(CultureInfo.InvariantCulture))
            .Append("，未接入 Track ").Append(unsupportedTrackCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine("，未接入 Clip " + unsupportedClipCount.ToString(CultureInfo.InvariantCulture));
        ESTrackIdentity.HasFutureSchema(
            sequence,
            out int futureTrackCount,
            out int futureClipCount);
        builder.Append("未来 Schema：Track ").Append(futureTrackCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" / Clip " + futureClipCount.ToString(CultureInfo.InvariantCulture));

        int trackCount = 0;
        int clipCount = 0;
        if (sequence.Tracks != null)
        {
            foreach (ITrackItem track in sequence.Tracks)
            {
                trackCount++;
                if (track == null)
                    continue;

                if (track.Clips != null)
                    clipCount += track.Clips.Count();
            }
        }

        builder.Append("轨道数量：").AppendLine(trackCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("片段数量：").AppendLine(clipCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("校验警告：").AppendLine(warnings.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append("校验提示：").AppendLine(infos.Count.ToString(CultureInfo.InvariantCulture));

        int trackIndex = 0;
        if (sequence.Tracks != null)
        {
            foreach (ITrackItem track in sequence.Tracks)
            {
                trackIndex++;
                if (track == null)
                {
                    builder.Append("\n轨道 ").Append(trackIndex.ToString(CultureInfo.InvariantCulture)).AppendLine("：<空引用>");
                    continue;
                }

                builder.Append("\n轨道 ").Append(trackIndex.ToString(CultureInfo.InvariantCulture)).Append("：")
                    .Append(string.IsNullOrWhiteSpace(track.DisplayName) ? track.GetType()._GetTypeDisplayName() : track.DisplayName)
                    .Append(" | ").Append(track.Enabled ? "启用" : "停用")
                    .Append(" | 类型：").AppendLine(track.GetType().FullName);

                int localClipIndex = 0;
                if (track.Clips == null)
                    continue;

                foreach (ITrackClip clip in track.Clips)
                {
                    localClipIndex++;
                    if (clip == null)
                    {
                        builder.Append("  - 片段 ").Append(localClipIndex.ToString(CultureInfo.InvariantCulture)).AppendLine("：<空引用>");
                        continue;
                    }

                    float start = Mathf.Max(0f, clip.StartTime);
                    float duration = Mathf.Max(0f, clip.DurationTime);
                    builder.Append("  - ").Append(localClipIndex.ToString(CultureInfo.InvariantCulture)).Append(". ")
                        .Append(string.IsNullOrWhiteSpace(clip.DisplayName) ? clip.GetType()._GetTypeDisplayName() : clip.DisplayName)
                        .Append(" | ").Append(clip.Enabled ? "启用" : "停用")
                        .Append(" | 开始 ").Append(start.ToString("0.###", CultureInfo.InvariantCulture))
                        .Append("s | 持续 ").Append(duration.ToString("0.###", CultureInfo.InvariantCulture))
                        .Append("s | 结束 ").Append((start + duration).ToString("0.###", CultureInfo.InvariantCulture))
                        .Append("s | 类型：").AppendLine(clip.GetType().FullName);
                }
            }
        }

        if (warnings.Count > 0)
        {
            builder.AppendLine("\n校验警告明细：");
            for (int i = 0; i < warnings.Count; i++)
                builder.Append("- ").AppendLine(warnings[i]);
        }

        if (infos.Count > 0)
        {
            builder.AppendLine("\n校验提示明细：");
            for (int i = 0; i < infos.Count; i++)
                builder.Append("- ").AppendLine(infos[i]);
        }

        return builder.ToString();
    }

    public static void CopyCurrentSequenceSummary()
    {
        string summary = BuildCurrentSequenceSummary();
        if (string.IsNullOrEmpty(summary))
            return;

        EditorGUIUtility.systemCopyBuffer = summary;
        ESTrackViewWindow.window?.ShowNotification(new GUIContent("已复制时间轴结构摘要（ES-TrackSummary/1）"));
    }

    public static void SaveContainerChangesImmediately(IEditorTrackSupport_GetSequence container)
    {
        if (!(container is UnityEngine.Object target))
            return;

        if (ReferenceEquals(ESTrackViewWindow.TrackContainer, container))
        {
            SaveContainerNow();
            SkillSequenceRuntimeCache.NotifySequenceChanged(container.Sequence);
            return;
        }

        ESDesignUtility.SafeEditor.Wrap_SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(target);
        if (ESTrackViewWindow.window != null && EditorUtility.IsDirty(target))
            ESTrackViewWindow.window.UpdateSaveStatus("保存失败", ESTrackViewTheme.StatusError, "时间轴仍有未保存修改，请查看 Console 后重试。", "Inspector 立即保存");
        SkillSequenceRuntimeCache.NotifySequenceChanged(container.Sequence);
    }

    public static void SaveContainerDisplayChanges(string source = null)
    {
        UnityEngine.Object target = ESTrackViewWindow.TrackContainer as UnityEngine.Object;
        if (target == null)
            return;

        ESDesignUtility.SafeEditor.Wrap_SetDirty(target);
        ESTrackViewWindow.window?.ScheduleAutoSave(string.IsNullOrWhiteSpace(source) ? "显示状态或时序修改" : source);
    }
}



