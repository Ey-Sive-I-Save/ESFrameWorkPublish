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

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            AudioClip audioClip = clip != null ? clip.Evaluate(target, logic) : null;
            if (audioClip == null)
                return;

            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, logic) : null;
            AudioSource source = obj != null ? obj.GetComponent<AudioSource>() : null;
            if (source != null)
                source.PlayOneShot(audioClip, volume != null ? volume.Evaluate(target, logic) : 1f);
            else if (target != null && target.GetTransform() != null)
                AudioSource.PlayClipAtPoint(audioClip, target.GetTransform().position, volume != null ? volume.Evaluate(target, logic) : 1f);
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

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            AudioSource source = GetSource(target, logic);
            if (source == null)
                return;

            if (setClip)
                source.clip = clip != null ? clip.Evaluate(target, logic) : null;
            source.loop = loop;
            source.volume = volume != null ? volume.Evaluate(target, logic) : 1f;
            source.Play();
        }

        protected override void StopOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            AudioSource source = GetSource(target, logic);
            if (source != null)
                source.Stop();
        }

        private AudioSource GetSource(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            GameObject obj = audioSourceObject != null ? audioSourceObject.Evaluate(target, logic) : null;
            return obj != null ? obj.GetComponent<AudioSource>() : null;
        }
    }
}
