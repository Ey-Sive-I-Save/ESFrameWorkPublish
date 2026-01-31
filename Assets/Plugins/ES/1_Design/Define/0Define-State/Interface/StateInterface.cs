using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngineInternal;

namespace ES
{

    //状态自主生命周期--微型数据开始才有
    public enum EnumStateRunningStatus
    {
        [InspectorName("从未启动")] Never,
        [InspectorName("运行时")] StateUpdate,  //OnStateEnter=>触发
        [InspectorName("已退出")] StateExit //OnStateExit=>触发
    }
}
