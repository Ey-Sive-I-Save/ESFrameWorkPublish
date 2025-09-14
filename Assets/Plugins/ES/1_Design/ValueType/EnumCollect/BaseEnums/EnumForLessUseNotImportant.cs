using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class EnumCollect
    {
        public enum GameEventMessageType
        {
            [InspectorName("玩家交互")] PlayerInteraction,
            [InspectorName("关卡事件")] LevelEvent,
            [InspectorName("物品收集")] ItemCollect,
        }
    }
}

