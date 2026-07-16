using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("命令播放模块")]
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
