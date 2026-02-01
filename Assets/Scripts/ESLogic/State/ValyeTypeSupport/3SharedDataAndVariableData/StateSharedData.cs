using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace ES
{

    [Serializable, TypeRegistryItem("标准状态共享数据")]
    public class StateSharedData
    {
        [TabGroup("核心", "基础配置")]
        [InlineProperty, HideLabel]
        public StateBasicConfig basicConfig = new StateBasicConfig();
        
        [TabGroup("核心", "动画配置")]
        [LabelText("动画配置")]
        public StateAnimationConfigData animationConfig = new StateAnimationConfigData();

        [TabGroup("切换", "冲突合并配置")]
        [InlineProperty, HideLabel]
        public StateMergeData mergeData = new StateMergeData();

        [TabGroup("切换", "消耗配置")]
        [InlineProperty, HideLabel]
        public StateCostData costData = new StateCostData();


    }

    [Serializable, TypeRegistryItem("标准状态运行状态")]
    //目前还没有特殊的玩意
    public class StateVariableData : IDeepClone<StateVariableData>
    {
        [LabelText("状态开始时间")] public float hasEnterTime;
        public void DeepCloneFrom(StateVariableData t)
        {
            if (t is StateVariableData svd)
            {
                hasEnterTime = svd.hasEnterTime;
            }
        }
    }

}
