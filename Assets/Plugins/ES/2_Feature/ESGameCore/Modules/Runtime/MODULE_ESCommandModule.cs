using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u8fd0\u884c\u57df/\u547d\u4ee4\u64ad\u653e\u6a21\u5757")]
    public sealed class ESCommandModule : ESRuntimeModule
    {
        [LabelText("\u9a71\u52a8\u547d\u4ee4\u64ad\u653e\u5668")]
        public bool tickCommandPlayers = true;

        protected override void Update()
        {
            if (!tickCommandPlayers)
                return;

            ESCommandPlayerRunner.TickAll(Time.time, Time.deltaTime);
        }

        public override void OnDestroy()
        {
            ESCommandPlayerRunner.Clear();
            base.OnDestroy();
        }
    }
}
