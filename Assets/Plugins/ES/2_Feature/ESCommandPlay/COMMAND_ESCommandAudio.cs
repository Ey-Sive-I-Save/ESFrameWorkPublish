using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.AudioSourcePlay)]
    public sealed class ESCommand_AudioSource_Play : ESCommand
    {
        [LabelText("\u97f3\u9891\u6e90")]
        public AudioSource target;

        [LabelText("\u4ece\u5934\u64ad\u653e")]
        public bool restart = true;

        public override string CommandName
        {
            get { return "\u64ad\u653e\u97f3\u9891\u6e90"; }
        }

        public override void Invoke()
        {
            if (target == null)
                return;

            if (restart)
                target.Stop();

            target.Play();
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.AudioSourceStop)]
    public sealed class ESCommand_AudioSource_Stop : ESCommand
    {
        [LabelText("\u97f3\u9891\u6e90")]
        public AudioSource target;

        public override string CommandName
        {
            get { return "\u505c\u6b62\u97f3\u9891\u6e90"; }
        }

        public override void Invoke()
        {
            if (target != null)
                target.Stop();
        }
    }
}
