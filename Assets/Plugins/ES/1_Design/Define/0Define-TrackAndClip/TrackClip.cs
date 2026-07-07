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
        [LabelText("名称")]
        public string name = "轨道片段";

        [BoxGroup("时间", showLabel: false)]
        [LabelText("开始时间"), HorizontalGroup("时间/时间信息H", 0.5f)]
        public float startTime = 0;
        [LabelText("持续时间"), HorizontalGroup("时间/时间信息H", 0.5f)]
        public float durationTime = 1;

        [BoxGroup("时间")]
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
        public float StartTime { get => startTime; set => startTime = value; }
        public float DurationTime { get => durationTime; set => durationTime = value; }
    }

}
