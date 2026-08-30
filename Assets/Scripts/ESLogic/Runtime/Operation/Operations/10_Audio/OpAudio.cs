using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    [Serializable, TypeRegistryItem("播放一次", OperationTypeRegistryNames.AudioOneShot)]
    public sealed class OpAudio_PlayOneShot : ESOutputOp
    {
        [LabelText("Cue"), InlineProperty]
        public ESAudioCueKey cue = new ESAudioCueKey();

        [FormerlySerializedAs("clip")]
        [SerializeField, LabelText("直接 Clip（可选）"), InlineProperty]
        private AudioClipExpressionSource legacyClip = new AudioClipExpressionSource();

        public AudioClipExpressionSource clip
        {
            get => legacyClip;
            set => legacyClip = value;
        }

        public AudioClipExpressionSource LegacyClip
        {
            get => legacyClip;
            set => legacyClip = value;
        }

        public GameObjectExpressionSource audioSourceObject = new GameObjectExpressionSource();
        public FloatExpressionSource volume = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, support) : null;
            float resolvedVolume = volume != null ? volume.Evaluate(target, support) : 1f;
            Transform emitter = obj != null
                ? obj.transform
                : target != null ? target.GetTransform() : null;
            if (cue != null && cue.IsConfigured && ESGameManager.Audio != null)
            {
                var cueRequest = new ESAudioPlayRequest { volumeScale = resolvedVolume };
                if (emitter != null)
                    ESGameManager.Audio.PlayAttached(cue, emitter, cueRequest);
                else
                    ESGameManager.Audio.PlayOneShot(cue, cueRequest);
                return;
            }

            AudioClip audioClip = legacyClip != null ? legacyClip.Evaluate(target, support) : null;
            if (audioClip == null || ESGameManager.Audio == null)
                return;

            var directRequest = new ESAudioPlayRequest { volumeScale = resolvedVolume };
            if (emitter != null)
                ESGameManager.Audio.PlayAttached(audioClip, emitter, directRequest);
            else
                ESGameManager.Audio.PlayOneShot(audioClip, directRequest);
        }
    }

    [Serializable, TypeRegistryItem("设置Source播放", OperationTypeRegistryNames.AudioLoop)]
    public sealed class OpAudio_SetSourcePlaying : ESOutputOp
    {
        public override bool NeedsStop => true;

        [LabelText("Cue"), InlineProperty]
        public ESAudioCueKey cue = new ESAudioCueKey();

        public GameObjectExpressionSource audioSourceObject = new GameObjectExpressionSource();

        [FormerlySerializedAs("clip")]
        [SerializeField, LabelText("直接 Clip（可选）"), InlineProperty]
        private AudioClipExpressionSource legacyClip = new AudioClipExpressionSource();

        public AudioClipExpressionSource clip
        {
            get => legacyClip;
            set => legacyClip = value;
        }

        public AudioClipExpressionSource LegacyClip
        {
            get => legacyClip;
            set => legacyClip = value;
        }

        public bool setClip = true;
        public bool loop = true;
        public FloatExpressionSource volume = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, support) : null;
            Transform emitter = obj != null
                ? obj.transform
                : target != null ? target.GetTransform() : null;
            if (cue != null && cue.IsConfigured && emitter != null && ESGameManager.Audio != null)
            {
                StopOwnedVoice(support);
                ESAudioPlayRequest cueRequest = new ESAudioPlayRequest
                {
                    volumeScale = volume != null ? volume.Evaluate(target, support) : 1f
                };
                ESAudioVoiceHandle handle = loop
                    ? ESGameManager.Audio.PlayLoop(cue, emitter, cueRequest)
                    : ESGameManager.Audio.PlayAttached(cue, emitter, cueRequest);
                if (handle.IsValid)
                    support?.AddAudioVoiceHandle(this, handle);
                return;
            }

            if (!setClip || emitter == null || ESGameManager.Audio == null)
                return;

            AudioClip audioClip = legacyClip != null ? legacyClip.Evaluate(target, support) : null;
            if (audioClip == null)
                return;

            StopOwnedVoice(support);
            var directRequest = new ESAudioPlayRequest
            {
                volumeScale = volume != null ? volume.Evaluate(target, support) : 1f
            };
            ESAudioVoiceHandle directHandle = loop
                ? ESGameManager.Audio.PlayLoop(audioClip, emitter, directRequest)
                : ESGameManager.Audio.PlayAttached(audioClip, emitter, directRequest);
            if (directHandle.IsValid)
                support?.SetAudioVoiceHandle(this, directHandle);
        }

        protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, support) : null;
            Transform emitter = obj != null
                ? obj.transform
                : target != null ? target.GetTransform() : null;
            if (ESGameManager.Audio != null && support != null
                && support.StopAudioVoices(this) > 0)
            {
                return;
            }

            if (cue != null && cue.IsConfigured && emitter != null && ESGameManager.Audio != null)
                ESGameManager.Audio.StopAttachedCue(cue, emitter);
        }

        private void StopOwnedVoice(ESOpSupport support)
        {
            support?.StopAudioVoices(this);
        }
    }
}
