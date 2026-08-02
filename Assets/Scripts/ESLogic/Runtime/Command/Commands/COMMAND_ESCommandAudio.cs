using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.AudioEmitterPlay)]
    public sealed class ESCommand_AudioEmitter_Play : ESCommand
    {
        [Required, LabelText("\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668")]
        [InfoBox("\u6b63\u5f0f\u5185\u5bb9\u53ea\u80fd\u4f7f\u7528 ESVfxAudioEmitter\u3002\u5b83\u4f1a\u7ecf\u8fc7\u97f3\u9891\u5206\u7c7b\u3001\u9884\u7b97\u3001\u6de1\u5165\u6de1\u51fa\u3001\u5bf9\u8c61\u6c60\u56de\u6536\u548c\u8bca\u65ad\uff1b\u4e0d\u8981\u76f4\u63a5\u63a7\u5236 AudioSource\u3002")]
        public ESVfxAudioEmitter target;

        [LabelText("\u4ece\u5934\u64ad\u653e")]
        public bool restart = true;

        public override string CommandName
        {
            get { return "\u64ad\u653e\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668"; }
        }

        public override void Invoke()
        {
            if (target == null)
                return;

            if (restart)
                target.StopConfigured();

            target.PlayConfigured();
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.AudioEmitterStop)]
    public sealed class ESCommand_AudioEmitter_Stop : ESCommand
    {
        [Required, LabelText("\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668")]
        [InfoBox("\u505c\u6b62\u4f1a\u6309\u5f53\u524d Voice Handle \u7cbe\u786e\u6536\u53e3\uff0c\u907f\u514d\u8bef\u505c\u540c\u4e00\u5bf9\u8c61\u4e0a\u7684\u5176\u4ed6\u97f3\u9891\u3002")]
        public ESVfxAudioEmitter target;

        public override string CommandName
        {
            get { return "\u505c\u6b62\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668"; }
        }

        public override void Invoke()
        {
            target?.StopConfigured();
        }
    }

    /// <summary>
    /// Serialization-only bridge for old assets. It deliberately has no registry entry and never
    /// invokes AudioSource directly, so legacy content cannot silently bypass ESAudioModule.
    /// Replace it with <see cref="ESCommand_AudioEmitter_Play"/> before release.
    /// </summary>
    [Serializable, Obsolete("\u5df2\u5e9f\u6b62\uff1a\u8bf7\u6539\u7528 ESCommand_AudioEmitter_Play\u3002\u6b63\u5f0f\u5185\u5bb9\u7981\u6b62\u88f8 AudioSource \u547d\u4ee4\u3002")]
    [InfoBox("\u5df2\u5e9f\u6b62\uff1a\u6b64\u547d\u4ee4\u4e0d\u4f1a\u64ad\u653e AudioSource\u3002\u8bf7\u66ff\u6362\u4e3a\u201c\u64ad\u653e\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668\u201d\uff0c\u5e76\u5728\u5bf9\u8c61\u4e0a\u6dfb\u52a0 ESVfxAudioEmitter\u3002")]
    public sealed class ESCommand_AudioSource_Play : ESCommand
    {
        [LabelText("\u65e7 AudioSource\uff08\u4ec5\u8fc1\u79fb\u5b9a\u4f4d\uff09")]
        public AudioSource target;

        [LabelText("\u65e7\u4ece\u5934\u64ad\u653e\u53c2\u6570\uff08\u4ec5\u8fc1\u79fb\u5b9a\u4f4d\uff09")]
        public bool restart = true;

        public override string CommandName
        {
            get { return "[Legacy/\u5df2\u7981\u7528] \u64ad\u653e\u88f8 AudioSource"; }
        }

        public override void Invoke()
        {
            Debug.LogError("[ESAudio] \u5df2\u963b\u6b62 Legacy \u2018\u64ad\u653e\u88f8 AudioSource\u2019\u547d\u4ee4\u3002\u8bf7\u8fc1\u79fb\u4e3a\u2018\u64ad\u653e\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668\u2019\u5e76\u914d\u7f6e ESVfxAudioEmitter\u3002", target);
        }
    }

    /// <summary>Serialization-only bridge for old assets; see <see cref="ESCommand_AudioSource_Play"/>.</summary>
    [Serializable, Obsolete("\u5df2\u5e9f\u6b62\uff1a\u8bf7\u6539\u7528 ESCommand_AudioEmitter_Stop\u3002\u6b63\u5f0f\u5185\u5bb9\u7981\u6b62\u88f8 AudioSource \u547d\u4ee4\u3002")]
    [InfoBox("\u5df2\u5e9f\u6b62\uff1a\u6b64\u547d\u4ee4\u4e0d\u4f1a\u505c\u6b62 AudioSource\u3002\u8bf7\u66ff\u6362\u4e3a\u201c\u505c\u6b62\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668\u201d\u3002")]
    public sealed class ESCommand_AudioSource_Stop : ESCommand
    {
        [LabelText("\u65e7 AudioSource\uff08\u4ec5\u8fc1\u79fb\u5b9a\u4f4d\uff09")]
        public AudioSource target;

        public override string CommandName
        {
            get { return "[Legacy/\u5df2\u7981\u7528] \u505c\u6b62\u88f8 AudioSource"; }
        }

        public override void Invoke()
        {
            Debug.LogError("[ESAudio] \u5df2\u963b\u6b62 Legacy \u2018\u505c\u6b62\u88f8 AudioSource\u2019\u547d\u4ee4\u3002\u8bf7\u8fc1\u79fb\u4e3a\u2018\u505c\u6b62\u53d7\u7ba1\u97f3\u9891\u53d1\u5c04\u5668\u2019\u5e76\u914d\u7f6e ESVfxAudioEmitter\u3002", target);
        }
    }
}
