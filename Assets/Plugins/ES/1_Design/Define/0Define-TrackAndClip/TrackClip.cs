using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif
using UnityEngine;
namespace ES
{
    public enum TrackClipEditorTargetMode
    {
        InheritTrackTarget,
        OverrideClipTarget
    }

    public interface ITrackClip
    {
        public bool Enabled { get; set; }
        public string DisplayName { get; set; }
        public float StartTime { get; set; }
        public float DurationTime { get; set; }

        IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track);
#if UNITY_EDITOR
        IEditorTimeSampler CreateEditorSampler(ITrackSequence sequence, ITrackItem track, object editorTarget);
#endif

        // public IEnumerable<>
    }

    /// <summary>
    /// 资产作用域内的稳定编辑器身份，不是 GameCore/RuntimeKey，不参与存档、联机或热更身份传输。
    /// </summary>
    public interface IStableTrackClip : ITrackClip
    {
        string ClipId { get; set; }
        int ClipSchema { get; set; }
        bool EnsureStableClipIdentity();
    }

    [Serializable]
    public class TrackClipBase : ITrackClip, IStableTrackClip
    {
        [SerializeField, HideInInspector]
        private string clipId = string.Empty;

        [SerializeField, HideInInspector]
        private int clipSchema = ESTrackIdentity.CurrentClipSchema;

        [PropertyOrder(ESTrackInspectorFieldStandard.OverviewOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.ClipOverview, "片段首先展示启用状态和中文名称，再展示时间、内容、目标与行为参数。")]
        [HorizontalGroup(ESTrackInspectorFieldStandard.ClipOverviewBasic, Width = 0.24f)]
        [LabelText("启用片段")]
        public bool enabled = true;

        [PropertyOrder(ESTrackInspectorFieldStandard.OverviewOrder + 1)]
        [TitleGroup(ESTrackInspectorFieldStandard.ClipOverview)]
        [HorizontalGroup(ESTrackInspectorFieldStandard.ClipOverviewBasic)]
        [LabelText("片段名称")]
        public string name = "轨道片段";

        [PropertyOrder(ESTrackInspectorFieldStandard.TimelineOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.Timeline, "单位：秒。时间轴窗口会根据所有片段结束时间自动扩展最大长度。")]
        [HorizontalGroup(ESTrackInspectorFieldStandard.TimelineValues, 0.5f)]
        [LabelText("开始时间")]
        [MinValue(0f)]
        [SuffixLabel("秒", true)]
        public float startTime = 0;

        [PropertyOrder(ESTrackInspectorFieldStandard.TimelineOrder + 1)]
        [TitleGroup(ESTrackInspectorFieldStandard.Timeline)]
        [HorizontalGroup(ESTrackInspectorFieldStandard.TimelineValues, 0.5f)]
        [LabelText("持续时间")]
        [MinValue(0f)]
        [SuffixLabel("秒", true)]
        public float durationTime = 1;

        [PropertyOrder(ESTrackInspectorFieldStandard.TimelineOrder + 2)]
        [TitleGroup(ESTrackInspectorFieldStandard.Timeline)]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("结束时间")]
        [SuffixLabel("秒", true)]
        public float EndTimePreview => startTime + Mathf.Max(0f, durationTime);

        [PropertyOrder(ESTrackInspectorFieldStandard.TimelineOrder + 3)]
        [TitleGroup(ESTrackInspectorFieldStandard.Timeline)]
        [OnInspectorGUI]
        public void EditorTime()
        {
#if UNITY_EDITOR
            EditorGUILayout.LabelField(
                new GUIContent("时间范围拖拽", "拖动左右控制点调整开始时间和结束时间。"),
                EditorStyles.miniBoldLabel);
            float end = startTime + durationTime;
            DrawESGraphTimeRangeSlider(ref startTime, ref end, 0f, Mathf.Max(10f, end));
            durationTime = Mathf.Max(0, end - startTime);
#endif
        }

#if UNITY_EDITOR
        private static void DrawESGraphTimeRangeSlider(ref float start, ref float end, float min, float max)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 22f, GUILayout.ExpandWidth(true));
            Event evt = Event.current;
            int controlId = GUIUtility.GetControlID("ESTrackClipTimeRange".GetHashCode(), FocusType.Passive, rect);

            float range = Mathf.Max(0.0001f, max - min);
            float startT = Mathf.Clamp01((start - min) / range);
            float endT = Mathf.Clamp01((end - min) / range);
            float left = rect.x + 5f;
            float right = rect.xMax - 5f;
            float width = Mathf.Max(1f, right - left);
            float startX = left + startT * width;
            float endX = left + endT * width;
            Rect track = new Rect(left, rect.y + 8f, width, 6f);

            if (evt.type == EventType.Repaint)
            {
                Color background = EditorGUIUtility.isProSkin
                    ? new Color(0.07f, 0.11f, 0.17f, 1f)
                    : new Color(0.78f, 0.84f, 0.91f, 1f);
                Color fill = EditorGUIUtility.isProSkin
                    ? new Color(0.20f, 0.52f, 0.82f, 1f)
                    : new Color(0.18f, 0.48f, 0.82f, 1f);
                Color handle = EditorGUIUtility.isProSkin
                    ? new Color(0.55f, 0.82f, 1f, 1f)
                    : new Color(0.10f, 0.36f, 0.72f, 1f);

                EditorGUI.DrawRect(track, background);
                EditorGUI.DrawRect(
                    new Rect(Mathf.Min(startX, endX), track.y, Mathf.Max(2f, endX - startX), track.height),
                    fill);
                EditorGUI.DrawRect(new Rect(startX - 2f, rect.y + 3f, 4f, 16f), handle);
                EditorGUI.DrawRect(new Rect(endX - 2f, rect.y + 3f, 4f, 16f), handle);
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                evt.Use();
            }

            if (GUIUtility.hotControl == controlId)
            {
                if (evt.type == EventType.MouseDrag || evt.type == EventType.MouseDown)
                {
                    float normalized = Mathf.Clamp01((evt.mousePosition.x - left) / width);
                    float value = min + normalized * range;
                    if (Mathf.Abs(value - start) <= Mathf.Abs(value - end))
                        start = Mathf.Min(value, end);
                    else
                        end = Mathf.Max(value, start);
                    GUI.changed = true;
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.type == EventType.Ignore)
                {
                    GUIUtility.hotControl = 0;
                    evt.Use();
                }
            }
        }
#endif

        public virtual IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track)
        {
            return new DefaultEditorDebugSampler(sequence.Name, track.DisplayName, this);
        }

#if UNITY_EDITOR
        public virtual IEditorTimeSampler CreateEditorSampler(ITrackSequence sequence, ITrackItem track, object editorTarget)
        {
            return CreateSampler(sequence, track);
        }
#endif

        public string DisplayName { get => name; set => name = value; }
        public bool Enabled { get => enabled; set => enabled = value; }
        public float StartTime { get => startTime; set => startTime = value; }
        public float DurationTime { get => durationTime; set => durationTime = value; }

        public string ClipId { get => clipId; set => clipId = value ?? string.Empty; }
        public int ClipSchema { get => clipSchema; set => clipSchema = value; }

        public virtual bool EnsureStableClipIdentity()
        {
            bool changed = false;
            if (!ESTrackIdentity.IsValidStableId(clipId))
            {
                clipId = ESTrackIdentity.NewClipId();
                changed = true;
            }

            if (clipSchema <= 0)
            {
                clipSchema = ESTrackIdentity.CurrentClipSchema;
                changed = true;
            }

            return changed;
        }
    }

}
