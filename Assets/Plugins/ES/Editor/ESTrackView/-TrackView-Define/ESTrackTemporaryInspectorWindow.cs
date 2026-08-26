using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ES.EditorInternal;

namespace ES
{
    internal static class ESTrackInspectorTargetResolver
    {
        internal static bool TryResolveTrack(
            UnityEngine.Object sourceAsset,
            string stableTargetKey,
            out ITrackItem resolvedTrack)
        {
            resolvedTrack = null;
            if (!(sourceAsset is IEditorTrackSupport_GetSequence source)
                || source.Sequence?.Tracks == null
                || !ESTrackIdentity.IsValidStableId(stableTargetKey))
            {
                return false;
            }

            foreach (ITrackItem track in source.Sequence.Tracks)
            {
                if (track is IStableTrackItem stableTrack
                    && string.Equals(stableTrack.TrackId, stableTargetKey, StringComparison.Ordinal))
                {
                    if (resolvedTrack != null)
                    {
                        resolvedTrack = null;
                        return false;
                    }

                    resolvedTrack = track;
                }
            }

            return resolvedTrack != null;
        }

        internal static bool TryResolveClip(
            UnityEngine.Object sourceAsset,
            string stableTargetKey,
            out ITrackClip resolvedClip)
        {
            resolvedClip = null;
            if (!(sourceAsset is IEditorTrackSupport_GetSequence source)
                || source.Sequence?.Tracks == null
                || !ESTrackIdentity.IsValidStableId(stableTargetKey))
            {
                return false;
            }

            foreach (ITrackItem track in source.Sequence.Tracks)
            {
                if (track?.Clips == null)
                    continue;

                foreach (ITrackClip clip in track.Clips)
                {
                    if (clip is IStableTrackClip stableClip
                        && string.Equals(stableClip.ClipId, stableTargetKey, StringComparison.Ordinal))
                    {
                        if (resolvedClip != null)
                        {
                            resolvedClip = null;
                            return false;
                        }

                        resolvedClip = clip;
                    }
                }
            }

            return resolvedClip != null;
        }
    }

    public abstract class ESTrackTemporaryInspectorWindow<TWindow> : ESIndependentInspectorWindow<TWindow>
        where TWindow : ESTrackTemporaryInspectorWindow<TWindow>
    {
        protected override ESWindowSleepLinkMode ESWindow_SleepLinkMode
            => ESWindowSleepLinkMode.FollowOwner;

        protected override EditorWindow ESWindow_SleepOwner
            => ESTrackViewWindow.window;

        protected override string ESWindow_SleepOwnerKey => ESTrackViewWindow.SleepOwnerKey;

        protected override string InspectorSubtitle => "Odin 业务字段桥接 · Track 独立编辑器";

        protected override void DrawIndependentInspectorSummary(UnityEngine.Object target)
        {
            ESTrackInspectorVisuals.DrawSummary(target);
        }

        protected override IDisposable BeginIndependentInspectorBody()
        {
            return ESTrackInspectorVisuals.BeginBody();
        }

        protected override void OnIndependentInspectorChanged(UnityEngine.Object resolvedSourceAsset, object data)
        {
            if (resolvedSourceAsset is IEditorTrackSupport_GetSequence sourceContainer)
            {
                if (ESTrackViewWindow.window != null && ReferenceEquals(ESTrackViewWindow.TrackContainer, sourceContainer))
                {
                    ESTrackViewWindow.window.ApplyIndependentInspectorChanges(data);
                    return;
                }

                ESDesignUtility.SafeEditor.Wrap_SetDirty(resolvedSourceAsset);
                SkillSequenceRuntimeCache.NotifySequenceChanged(sourceContainer.Sequence);
                return;
            }

            EditorUtility.SetDirty(resolvedSourceAsset);
        }

        protected override void OnIndependentInspectorClosed(
            UnityEngine.Object resolvedSourceAsset,
            object data,
            bool targetLost)
        {
            if (resolvedSourceAsset is IEditorTrackSupport_GetSequence sourceContainer)
            {
                ESTrackViewWindowHelper.SaveContainerChangesImmediately(sourceContainer);
            }
            else if (resolvedSourceAsset != null)
            {
                EditorUtility.SetDirty(resolvedSourceAsset);
                AssetDatabase.SaveAssetIfDirty(resolvedSourceAsset);
            }

            ESTrackViewWindow.window?.NotifyIndependentInspectorClosed(this);
        }

        protected override void OnIndependentInspectorBound(bool restoredAfterReload)
        {
            ESTrackViewWindow.window?.NotifyIndependentInspectorBound(this);
        }
    }

    [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Inspector)]
    public sealed class ESTrackItemTemporaryInspectorWindow : ESTrackTemporaryInspectorWindow<ESTrackItemTemporaryInspectorWindow>
    {
        public static ESTrackItemTemporaryInspectorWindow OpenFor(
            ITrackItem track,
            UnityEngine.Object sourceAsset,
            string title,
            string page,
            ESTrackViewWindow owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            string stableId = EnsureStableTrackId(track, sourceAsset);
            return OpenIndependent(track, sourceAsset, stableId, title, page, owner);
        }

        private static string EnsureStableTrackId(ITrackItem track, UnityEngine.Object sourceAsset)
        {
            if (!(track is IStableTrackItem stableTrack))
                return string.Empty;

            bool changed = false;
            if (stableTrack.TrackSchema <= ESTrackIdentity.CurrentTrackSchema
                && (!ESTrackIdentity.IsValidStableId(stableTrack.TrackId) || stableTrack.TrackSchema <= 0))
            {
                if (sourceAsset != null)
                    Undo.RecordObject(sourceAsset, "迁移 Track 稳定身份");
                changed = stableTrack.EnsureStableTrackIdentity();
            }
            if (changed && sourceAsset != null)
                EditorUtility.SetDirty(sourceAsset);

            return ESTrackIdentity.IsValidStableId(stableTrack.TrackId)
                ? stableTrack.TrackId
                : string.Empty;
        }

        protected override bool TryResolveManagedInspectorData(
            UnityEngine.Object resolvedSourceAsset,
            string stableTargetKey,
            out object data)
        {
            bool resolved = ESTrackInspectorTargetResolver.TryResolveTrack(
                resolvedSourceAsset,
                stableTargetKey,
                out ITrackItem track);
            data = track;
            return resolved;
        }
    }

    [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Inspector)]
    public sealed class ESTrackClipTemporaryInspectorWindow : ESTrackTemporaryInspectorWindow<ESTrackClipTemporaryInspectorWindow>
    {
        public static ESTrackClipTemporaryInspectorWindow OpenFor(
            ITrackClip clip,
            UnityEngine.Object sourceAsset,
            string title,
            string page,
            ESTrackViewWindow owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            string stableId = EnsureStableClipId(clip, sourceAsset);
            return OpenIndependent(clip, sourceAsset, stableId, title, page, owner);
        }

        private static string EnsureStableClipId(ITrackClip clip, UnityEngine.Object sourceAsset)
        {
            if (!(clip is IStableTrackClip stableClip))
                return string.Empty;

            bool changed = false;
            if (stableClip.ClipSchema <= ESTrackIdentity.CurrentClipSchema
                && (!ESTrackIdentity.IsValidStableId(stableClip.ClipId) || stableClip.ClipSchema <= 0))
            {
                if (sourceAsset != null)
                    Undo.RecordObject(sourceAsset, "迁移 Clip 稳定身份");
                changed = stableClip.EnsureStableClipIdentity();
            }
            if (changed && sourceAsset != null)
                EditorUtility.SetDirty(sourceAsset);

            return ESTrackIdentity.IsValidStableId(stableClip.ClipId)
                ? stableClip.ClipId
                : string.Empty;
        }

        protected override bool TryResolveManagedInspectorData(
            UnityEngine.Object resolvedSourceAsset,
            string stableTargetKey,
            out object data)
        {
            bool resolved = ESTrackInspectorTargetResolver.TryResolveClip(
                resolvedSourceAsset,
                stableTargetKey,
                out ITrackClip clip);
            data = clip;
            return resolved;
        }
    }

    [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Inspector)]
    public sealed class ESTrackSkillDataTemporaryInspectorWindow : ESTrackTemporaryInspectorWindow<ESTrackSkillDataTemporaryInspectorWindow>
    {
        public static ESTrackSkillDataTemporaryInspectorWindow OpenFor(
            UnityEngine.Object skillData,
            string title,
            string page,
            ESTrackViewWindow owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            return OpenIndependent(skillData, skillData, string.Empty, title, page, owner);
        }

        protected override bool TryResolveManagedInspectorData(
            UnityEngine.Object resolvedSourceAsset,
            string stableTargetKey,
            out object data)
        {
            data = null;
            return false;
        }
    }

    /// <summary>
    /// Track Inspector 的 IMGUI 视觉适配层。
    /// GraphView 使用 UI Toolkit，但两者必须共享同一组 ES presentation token，
    /// 且不能通过全局 GUI 颜色污染其他 Unity 窗口。
    /// </summary>
    internal static class ESTrackInspectorVisuals
    {
        private static Color InspectorTextColor
        {
            get { return EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.88f, 0.91f, 0.96f, 1f)
                : EditorInternal.ESEditorPresentation.SectionTextColor; }
        }

        private static Color InspectorMutedTextColor
        {
            get { return EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.66f, 0.71f, 0.79f, 1f)
                : EditorInternal.ESEditorPresentation.SectionMutedTextColor; }
        }

        private static Color InspectorSelectedTextColor
        {
            get { return EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.44f, 0.74f, 1f, 1f)
                : EditorInternal.ESEditorPresentation.SectionSelectedTextColor; }
        }

        internal sealed class BodyScope : IDisposable
        {
            private readonly GUISkin previousSkin;
            private readonly float previousLabelWidth;
            private readonly float previousFieldWidth;
            private readonly bool previousWideMode;
            private readonly int previousIndentLevel;
            private readonly Color previousGuiColor;
            private readonly Color previousContentColor;
            private readonly Color previousBackgroundColor;
            private readonly GUISkin localSkin;
            private bool disposed;

            private static GUISkin cachedSkin;
            private static int cachedSkinGeneration = -1;
            private static readonly List<Texture2D> cachedTextures = new List<Texture2D>(8);

            internal BodyScope()
            {
                previousSkin = GUI.skin;
                previousLabelWidth = EditorGUIUtility.labelWidth;
                previousFieldWidth = EditorGUIUtility.fieldWidth;
                previousWideMode = EditorGUIUtility.wideMode;
                previousIndentLevel = EditorGUI.indentLevel;
                previousGuiColor = GUI.color;
                previousContentColor = GUI.contentColor;
                previousBackgroundColor = GUI.backgroundColor;

                EditorGUIUtility.wideMode = false;
                EditorGUIUtility.labelWidth = Mathf.Clamp(
                    EditorGUIUtility.currentViewWidth * 0.22f, 78f, 102f);
                EditorGUIUtility.fieldWidth = 64f;
                EditorGUI.indentLevel = 0;
                GUI.color = Color.white;
                GUI.contentColor = InspectorTextColor;
                localSkin = GetCachedGraphSkin(previousSkin);
                GUI.skin = localSkin;
                EditorGUILayout.BeginVertical(EditorInternal.ESEditorPresentation.SurfaceStyle);
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                EditorGUILayout.EndVertical();
                GUI.skin = previousSkin;
                GUI.color = previousGuiColor;
                GUI.contentColor = previousContentColor;
                GUI.backgroundColor = previousBackgroundColor;
                EditorGUI.indentLevel = previousIndentLevel;
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUIUtility.fieldWidth = previousFieldWidth;
                EditorGUIUtility.wideMode = previousWideMode;
            }

            private static GUISkin GetCachedGraphSkin(GUISkin source)
            {
                int generation = EditorInternal.ESEditorPresentation.SkinGeneration;
                if (cachedSkin != null && cachedSkinGeneration == generation)
                    return cachedSkin;

                DestroyCachedGraphSkin();
                cachedSkin = BuildGraphSkin(source);
                cachedSkinGeneration = generation;
                return cachedSkin;
            }

            private static GUISkin BuildGraphSkin(GUISkin source)
            {
                GUISkin skin = ScriptableObject.CreateInstance<GUISkin>();
                skin.hideFlags = HideFlags.HideAndDontSave;

                Color text = InspectorTextColor;
                Color muted = InspectorMutedTextColor;
                Color selected = InspectorSelectedTextColor;
                if (EditorInternal.ESEditorPresentation.IsProSkin)
                {
                    // Graph 的深色面板需要“深底 + 高对比字”；原 token 的正文灰度对 Odin 字段过低。
                    text = new Color(0.88f, 0.91f, 0.96f, 1f);
                    muted = new Color(0.66f, 0.71f, 0.79f, 1f);
                    selected = new Color(0.44f, 0.74f, 1f, 1f);
                }
                Color surface = Darken(EditorInternal.ESEditorPresentation.GetDepthBackground(2), 0.18f);
                Color input = Darken(EditorInternal.ESEditorPresentation.GetDepthBackground(3), 0.12f);
                Color accent = EditorInternal.ESEditorPresentation.GetDepthAccent(0);

                skin.label = MakeStyle(source != null ? source.label : null, text, Color.clear);
                skin.box = MakeStyle(source != null ? source.box : null, text, surface);
                skin.button = MakeStyle(source != null ? source.button : null, selected, input);
                skin.textField = MakeStyle(source != null ? source.textField : null, text, input);
                skin.textArea = MakeStyle(source != null ? source.textArea : null, text, input);
                skin.toggle = MakeStyle(source != null ? source.toggle : null, text, Color.clear);
                skin.horizontalSlider = MakeStyle(source != null ? source.horizontalSlider : null, muted, input);
                skin.horizontalSliderThumb = MakeStyle(source != null ? source.horizontalSliderThumb : null, selected, accent);
                skin.verticalSlider = MakeStyle(source != null ? source.verticalSlider : null, muted, input);
                skin.verticalSliderThumb = MakeStyle(source != null ? source.verticalSliderThumb : null, selected, accent);

                skin.customStyles = new[]
                {
                    MakeNamedStyle("Label", source != null ? source.GetStyle("Label") : null, text, Color.clear),
                    MakeNamedStyle("BoldLabel", source != null ? source.GetStyle("BoldLabel") : null, selected, Color.clear),
                    MakeNamedStyle("MiniLabel", source != null ? source.GetStyle("MiniLabel") : null, muted, Color.clear),
                    MakeNamedStyle("Foldout", source != null ? source.GetStyle("Foldout") : null, selected, Color.clear),
                    MakeStyle(source != null ? source.GetStyle("Toolbar") : null, text, surface),
                    MakeStyle(source != null ? source.GetStyle("ToolbarButton") : null, selected, input),
                    MakeStyle(source != null ? source.GetStyle("HelpBox") : null, muted, surface),
                    MakeStyle(source != null ? source.GetStyle("ObjectField") : null, text, input),
                };
                return skin;
            }

            private static Color Darken(Color color, float amount)
            {
                if (!EditorInternal.ESEditorPresentation.IsProSkin)
                    return color;
                float factor = Mathf.Clamp01(1f - amount);
                return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
            }

            private static GUIStyle MakeNamedStyle(string name, GUIStyle source, Color textColor, Color backgroundColor)
            {
                GUIStyle style = MakeStyle(source, textColor, backgroundColor);
                style.name = name;
                return style;
            }

            private static GUIStyle MakeStyle(GUIStyle source, Color textColor, Color backgroundColor)
            {
                GUIStyle style = source != null ? new GUIStyle(source) : new GUIStyle();
                style.normal.textColor = textColor;
                style.onNormal.textColor = textColor;
                style.hover.textColor = textColor;
                style.onHover.textColor = textColor;
                style.active.textColor = Color.white;
                style.onActive.textColor = Color.white;
                style.focused.textColor = textColor;
                style.onFocused.textColor = textColor;
                if (backgroundColor.a > 0f)
                {
                    Texture2D texture = MakeTexture(backgroundColor);
                    style.normal.background = texture;
                    style.hover.background = texture;
                    style.active.background = MakeTexture(Color.Lerp(backgroundColor, EditorInternal.ESEditorPresentation.GetDepthAccent(0), 0.22f));
                    style.focused.background = texture;
                    style.onNormal.background = texture;
                    style.onHover.background = texture;
                    style.onActive.background = style.active.background;
                    style.onFocused.background = texture;
                }
                return style;
            }

            private static Texture2D MakeTexture(Color color)
            {
                Texture2D texture = new Texture2D(1, 1)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "ESTrackInspectorLocalSkin"
                };
                try
                {
                    texture.SetPixel(0, 0, color);
                    texture.Apply(false, true);
                    cachedTextures.Add(texture);
                    return texture;
                }
                catch
                {
                    if (texture != null)
                        UnityEngine.Object.DestroyImmediate(texture);
                    throw;
                }
            }

            private static void DestroyCachedGraphSkin()
            {
                for (int i = 0; i < cachedTextures.Count; i++)
                {
                    if (cachedTextures[i] != null)
                        UnityEngine.Object.DestroyImmediate(cachedTextures[i]);
                }
                cachedTextures.Clear();
                if (cachedSkin != null)
                    UnityEngine.Object.DestroyImmediate(cachedSkin);
                cachedSkin = null;
                cachedSkinGeneration = -1;
            }
        }

        public static BodyScope BeginBody()
        {
            return new BodyScope();
        }

        public static void DrawSummary(UnityEngine.Object target)
        {
            VisualGUIDrawerSO drawer = target as VisualGUIDrawerSO;
            object data = drawer != null ? drawer.drawerData : target;
            string typeName = data != null
                ? data.GetType()._GetTypeDisplayName()
                : "未绑定业务对象";
            bool isClip = data is ITrackClip;
            bool isTrack = data is ITrackItem;
            string context = isClip ? "片段属性" : isTrack ? "轨道属性" : "资产属性";
            string displayName = data is ITrackItem trackItem
                ? trackItem.DisplayName
                : data is ITrackClip trackClip
                    ? trackClip.DisplayName
                    : typeName;

            Color previousContentColor = GUI.contentColor;
            Color previousBackgroundColor = GUI.backgroundColor;
            Rect cardRect;
            using (new EditorGUILayout.VerticalScope(EditorInternal.ESEditorPresentation.SurfaceStyle))
            {
                EditorGUILayout.LabelField(context, EditorInternal.ESEditorPresentation.MetaStyle);
                GUI.contentColor = InspectorSelectedTextColor;
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(displayName) ? "未命名" : displayName,
                    EditorInternal.ESEditorPresentation.HeaderStyle);
                GUI.contentColor = InspectorMutedTextColor;
                EditorGUILayout.LabelField(typeName, EditorInternal.ESEditorPresentation.SubtitleStyle);
                GUI.contentColor = InspectorTextColor;
                if (data is ITrackItem summaryTrack)
                {
                    EditorGUILayout.LabelField("运行状态", summaryTrack.Enabled ? "已启用" : "已停用");
                    EditorGUILayout.LabelField("片段数量", CountTrackClips(summaryTrack).ToString());
                }
                else if (data is ITrackClip summaryClip)
                {
                    float endTime = summaryClip.StartTime + Mathf.Max(0f, summaryClip.DurationTime);
                    EditorGUILayout.LabelField("运行状态", summaryClip.Enabled ? "已启用" : "已停用");
                    EditorGUILayout.LabelField(
                        "时间范围",
                        summaryClip.StartTime.ToString("0.###") + " 秒 → " + endTime.ToString("0.###") + " 秒");
                    EditorGUILayout.LabelField("持续时间", Mathf.Max(0f, summaryClip.DurationTime).ToString("0.###") + " 秒");
                }
                else
                {
                    EditorGUILayout.LabelField("状态", "资产编辑中");
                }

                EditorGUILayout.LabelField("同步方式", "修改将同步回当前时间轴资产");
            }

            cardRect = GUILayoutUtility.GetLastRect();
            GUI.contentColor = previousContentColor;
            GUI.backgroundColor = previousBackgroundColor;
            DrawCardFrame(cardRect, EditorInternal.ESEditorPresentation.GetDepthAccent(0));
            EditorGUILayout.Space(4f);
        }

        private static int CountTrackClips(ITrackItem track)
        {
            IEnumerable<ITrackClip> clips = track?.Clips;
            if (clips == null)
                return 0;
            if (clips is System.Collections.ICollection collection)
                return collection.Count;

            int count = 0;
            foreach (ITrackClip clip in clips)
                if (clip != null)
                    count++;
            return count;
        }

        public static void DrawEmptyState(string title, string description, string notice)
        {
            Color previousContentColor = GUI.contentColor;
            Color previousBackgroundColor = GUI.backgroundColor;
            Rect cardRect;
            using (new EditorGUILayout.VerticalScope(EditorInternal.ESEditorPresentation.SurfaceStyle))
            {
                EditorGUILayout.LabelField("轨道属性", EditorInternal.ESEditorPresentation.MetaStyle);
                GUI.contentColor = InspectorSelectedTextColor;
                EditorGUILayout.LabelField(title, EditorInternal.ESEditorPresentation.HeaderStyle);
                GUI.contentColor = InspectorMutedTextColor;
                EditorGUILayout.LabelField(description, EditorInternal.ESEditorPresentation.SubtitleStyle);
                EditorGUILayout.Space(5f);
                Color accent = EditorInternal.ESEditorPresentation.GetDepthAccent(0);
                Color noticeBackground = Color.Lerp(
                    EditorInternal.ESEditorPresentation.GetDepthBackground(2), accent,
                    EditorInternal.ESEditorPresentation.IsProSkin ? 0.13f : 0.08f);
                Color oldBackground = GUI.backgroundColor;
                GUI.backgroundColor = noticeBackground;
                EditorGUILayout.HelpBox(notice, MessageType.Info);
                GUI.backgroundColor = oldBackground;
            }

            cardRect = GUILayoutUtility.GetLastRect();
            GUI.contentColor = previousContentColor;
            GUI.backgroundColor = previousBackgroundColor;
            DrawCardFrame(cardRect, EditorInternal.ESEditorPresentation.GetDepthAccent(0));
        }

        private static void DrawCardFrame(Rect rect, Color accent)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;
            EditorInternal.ESEditorPresentation.DrawFrame(
                rect, EditorInternal.ESEditorPresentation.DividerColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
        }
    }
}
