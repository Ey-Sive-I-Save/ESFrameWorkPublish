using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
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

        [TitleGroup("音频片段", "在片段开始时间播放指定 Cue。")]
        [LabelText("Cue"), InlineProperty]
        public ESAudioCueKey cue = new ESAudioCueKey();

        [FormerlySerializedAs("audioClip")]
        [SerializeField, LabelText("直接 Clip（可选）")]
        private AudioClip legacyAudioClip;

        public AudioClip audioClip
        {
            get => legacyAudioClip;
            set => legacyAudioClip = value;
        }

        public AudioClip LegacyAudioClip => legacyAudioClip;

        [TitleGroup("音频片段")]
        [LabelText("音量"), OnValueChanged(nameof(ClampVolume))]
        [SuffixLabel("0–1", true)]
        public float volume = 1f;

        private void ClampVolume()
        {
            volume = Mathf.Clamp01(volume);
        }

        [TitleGroup("音频片段")]
        [HideInInspector]
        public bool addAudioSourceIfMissing = true;

        [TitleGroup("音频片段")]
        [LabelText("离开片段时停止音效")]
        [Tooltip("关闭时音效自然播放完毕；开启时片段退出或技能退出会立即停止该音效。")]
        public bool stopOnClipExit = false;

        public ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context)
        {
            return new AudioClipRuntimePlayer(this);
        }

        public override IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track)
        {
#if UNITY_EDITOR
            return new AudioEditorSampler(legacyAudioClip,startTime);
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
            if (clip == null)
                return;

            GameObject target = state != null && state.SkillRuntimeTarget != null
                ? state.SkillRuntimeTarget.GetGameObject()
                : null;
            if (clip.cue != null && clip.cue.IsConfigured && ESGameManager.Audio != null)
            {
                ESAudioPlayRequest request = new ESAudioPlayRequest { volumeScale = clip.volume };
                clipState.UserData = target != null
                    ? ESGameManager.Audio.PlayAttached(clip.cue, target.transform, request)
                    : ESGameManager.Audio.PlayOneShot(clip.cue, request);
                return;
            }

            if (clip.LegacyAudioClip == null || ESGameManager.Audio == null)
                return;

            ESAudioPlayRequest directRequest = new ESAudioPlayRequest { volumeScale = clip.volume };
            clipState.UserData = target != null
                ? ESGameManager.Audio.PlayAttached(clip.LegacyAudioClip, target.transform, directRequest)
                : ESGameManager.Audio.PlayOneShot(clip.LegacyAudioClip, directRequest);
        }

        public void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime)
        {
        }

        public void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clip != null && clip.stopOnClipExit)
            {
                if (clipState.UserData is ESAudioVoiceHandle handle)
                    ESGameManager.Audio?.Stop(handle);
            }

            clipState.UserData = null;
        }
    }
}
