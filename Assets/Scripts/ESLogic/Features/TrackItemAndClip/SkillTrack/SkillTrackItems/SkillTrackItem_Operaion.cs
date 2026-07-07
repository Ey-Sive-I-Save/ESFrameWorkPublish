using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateTrackItem(TrackItemType.Skill,"Operation轨道")]
    public class SkillTrackItem_Operation: SkillTrackItem<SkillTrackClip_Operation>
    {
    
    }
    [System.Serializable,ESCreatePath("技能轨道剪辑","操作轨道剪辑")]
    public class SkillTrackClip_Operation : SkillTrackClip
    {
          [LabelText("操作描述")]
          public string OperationDescription;

          [SerializeReference,HideLabel,BoxGroup("操作内容")]
          public ESOutputOp op;

          public override IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track)
          {
#if UNITY_EDITOR
              return CreateEditorSampler(sequence, track, null);
#else
              return base.CreateSampler(sequence, track);
#endif
          }

#if UNITY_EDITOR
          public override IEditorTimeSampler CreateEditorSampler(ITrackSequence sequence, ITrackItem track, object editorTarget)
          {
              return new SkillTrackOperationEditorSampler(this, editorTarget as ESRuntimeTarget);
          }
    }

    public class SkillTrackOperationEditorSampler : EditorTimeSamplerBase
    {
        private readonly SkillTrackClip_Operation _clip;
        private readonly ESRuntimeTarget _target;
        private bool _isInside;

        public SkillTrackOperationEditorSampler(SkillTrackClip_Operation clip, ESRuntimeTarget target)
        {
            _clip = clip;
            _target = target;
        }

        public override void SampleTime(float time)
        {
            if (_clip == null || _clip.op == null || _target == null || _target.IsRecycled)
                return;

            bool inside = time >= _clip.StartTime && time < _clip.StartTime + _clip.DurationTime;
            if (inside == _isInside)
                return;

            _isInside = inside;
            if (_isInside)
                _clip.op._TryStartOp(_target, null);
            else
                _clip.op._TryStopOp(_target, null);
        }

        public override void OnEditorPreviewStop()
        {
            if (_clip != null && _clip.op != null && _isInside)
                _clip.op._TryStopOp(_target, null);

            _isInside = false;
        }
    }
#endif
}
