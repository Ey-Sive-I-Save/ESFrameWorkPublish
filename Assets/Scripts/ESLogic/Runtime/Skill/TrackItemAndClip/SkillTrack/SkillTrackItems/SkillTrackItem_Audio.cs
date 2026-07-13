using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [CreateTrackItem(TrackItemType.Skill,"音频轨道")]
    public class SkillTrackItem_Audio : SkillTrackItem<SkillTrackClip_Audio>
    {
        public override Color ItemBGColor => new Color(0.7f, 0.48f, 0.18f, 0.42f);

        public SkillTrackItem_Audio()
        {
            displayName = "音频轨道";
        }
    }

    [System.Serializable,ESCreatePath("技能轨道剪辑","音频轨道剪辑")]
    public class SkillTrackClip_Audio : SkillTrackClip, ISkillRuntimeClipCompiler
    {
        public SkillTrackClip_Audio()
        {
            name = "音频片段";
        }

        [TitleGroup("音频片段", "在片段开始时间播放指定 AudioClip。")]
        [LabelText("音频剪辑")]
        [Required("音频片段需要指定 AudioClip。")]
        public AudioClip audioClip;

        [TitleGroup("音频片段")]
        [LabelText("音量")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [TitleGroup("音频片段")]
        [LabelText("没有 AudioSource 时自动添加")]
        public bool addAudioSourceIfMissing = true;

        [TitleGroup("音频片段")]
        [LabelText("离开片段时停止音效")]
        [Tooltip("关闭时音效按 AudioSource 自然播放完毕；开启时片段退出或技能退出会立即停止该音效。")]
        public bool stopOnClipExit = false;

        public ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context)
        {
            return new AudioClipRuntimePlayer(this);
        }

        public override IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track)
        {
#if UNITY_EDITOR
            return new AudioEditorSampler(audioClip,startTime);
#else
            return base.CreateSampler(sequence, track);
#endif
        }
    }

    public sealed class AudioClipRuntimePlayer : ISkillRuntimeClipPlayer
    {
        private readonly SkillTrackClip_Audio clip;

        public AudioClipRuntimePlayer(SkillTrackClip_Audio clip)
        {
            this.clip = clip;
        }

        public void OnClipEnter(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clip == null || clip.audioClip == null)
                return;

            GameObject target = state != null && state.SkillRuntimeTarget != null
                ? state.SkillRuntimeTarget.GetGameObject()
                : null;
            if (target == null)
                return;

            AudioSource source = target.GetComponent<AudioSource>();
            if (source == null && clip.addAudioSourceIfMissing)
                source = target.AddComponent<AudioSource>();

            if (source == null)
                return;

            source.clip = clip.audioClip;
            source.volume = clip.volume;
            source.time = 0f;
            source.Play();
            clipState.UserData = source;
        }

        public void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime)
        {
        }

        public void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clip != null && clip.stopOnClipExit && clipState.UserData is AudioSource source && source != null && source.clip == clip.audioClip)
                source.Stop();

            clipState.UserData = null;
        }
    }
}
