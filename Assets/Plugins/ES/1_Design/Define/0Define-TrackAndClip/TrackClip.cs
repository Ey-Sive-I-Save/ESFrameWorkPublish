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

    public class TrackClipBase : ITrackClip 
    {
        public static float defaultEndTime = 10f;

        [TitleGroup("片段基础", "片段名称会显示在轨道时间轴中。")]
        [HorizontalGroup("片段基础/基础", Width = 70)]
        [LabelText("启用")]
        public bool enabled = true;

        [HorizontalGroup("片段基础/基础")]
        [LabelText("名称")]
        public string name = "轨道片段";

        [TitleGroup("时间范围", "单位：秒。时间轴窗口会根据所有片段结束时间自动扩展最大长度。")]
        [HorizontalGroup("时间范围/时间信息", 0.5f)]
        [LabelText("开始")]
        [MinValue(0f)]
        [SuffixLabel("秒", true)]
        public float startTime = 0;

        [HorizontalGroup("时间范围/时间信息", 0.5f)]
        [LabelText("持续")]
        [MinValue(0f)]
        [SuffixLabel("秒", true)]
        public float durationTime = 1;

        [TitleGroup("时间范围")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("结束时间")]
        [SuffixLabel("秒", true)]
        public float EndTimePreview => startTime + Mathf.Max(0f, durationTime);

        [TitleGroup("时间范围")]
        [OnInspectorGUI]
        public void EditorTime()
        {
#if UNITY_EDITOR
            float end = startTime + durationTime;
            EditorGUILayout.MinMaxSlider(ref startTime, ref end, 0, defaultEndTime);
            durationTime = Mathf.Max(0, end - startTime);
#endif
        }

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
    }

}
