using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("播放一次", OperationTypeRegistryNames.AudioOneShot)]
    public sealed class OpAudio_PlayOneShot : ESOutputOp
    {
        public AudioClipExpressionSource clip = new AudioClipExpressionSource();
        public GameObjectExpressionSource audioSourceObject = new GameObjectExpressionSource();
        public FloatExpressionSource volume = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            AudioClip audioClip = clip != null ? clip.Evaluate(target, support) : null;
            if (audioClip == null)
                return;

            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, support) : null;
            AudioSource source = obj != null ? obj.GetComponent<AudioSource>() : null;
            if (source != null)
                source.PlayOneShot(audioClip, volume != null ? volume.Evaluate(target, support) : 1f);
            else if (target != null && target.GetTransform() != null)
                AudioSource.PlayClipAtPoint(audioClip, target.GetTransform().position, volume != null ? volume.Evaluate(target, support) : 1f);
        }
    }

    [Serializable, TypeRegistryItem("设置Source播放", OperationTypeRegistryNames.AudioLoop)]
    public sealed class OpAudio_SetSourcePlaying : ESOutputOp
    {
        public GameObjectExpressionSource audioSourceObject = new GameObjectExpressionSource();
        public AudioClipExpressionSource clip = new AudioClipExpressionSource();
        public bool setClip = true;
        public bool loop = true;
        public FloatExpressionSource volume = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            AudioSource source = GetSource(target, support);
            if (source == null)
                return;

            if (setClip)
                source.clip = clip != null ? clip.Evaluate(target, support) : null;
            source.loop = loop;
            source.volume = volume != null ? volume.Evaluate(target, support) : 1f;
            source.Play();
        }

        protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            AudioSource source = GetSource(target, RuntimeSupport(scopeSupport, hostSupport));
            if (source != null)
                source.Stop();
        }

        private AudioSource GetSource(ESRuntimeTargetPack target, ESOpSupport support)
        {
            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, support) : null;
            return obj != null ? obj.GetComponent<AudioSource>() : null;
        }
    }
}
