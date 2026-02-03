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
    public class StateSharedData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;
        [TabGroup("核心", "基础配置")]
        [InlineProperty, HideLabel]
        public StateBasicConfig basicConfig = new StateBasicConfig();
        
        [TabGroup("核心", "动画配置")]
        [LabelText("动画配置")]
        public StateAnimationConfigData animationConfig = new StateAnimationConfigData();

        [TabGroup("核心", "动画配置")]
        [LabelText("是否有动画")]
        [Tooltip("如果为false，则不会被加入Playable图")]
        public bool hasAnimation = false;

        [TabGroup("切换", "冲突合并配置")]
        [InlineProperty, HideLabel]
        public StateMergeData mergeData = new StateMergeData();

        [TabGroup("切换", "代价配置")]
        [InlineProperty, HideLabel]
        public StateCostData costData = new StateCostData();

        /// <summary>
        /// 运行时初始化 - 递归初始化所有子成员
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            // 递归初始化所有成员
            basicConfig?.InitializeRuntime();
            animationConfig?.InitializeRuntime();
            mergeData?.InitializeRuntime();
            costData?.InitializeRuntime();
            
            // 预备主状态判据值（依赖costData）
            basicConfig?.PrepareMainCriterionValue(costData);
            
            _isRuntimeInitialized = true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器手动初始化接口
        /// </summary>
        [Button("初始化预备数据"), PropertyOrder(-1)]
        private void EditorInitialize()
        {
            _isRuntimeInitialized = false; // 重置标记以允许重新初始化
            InitializeRuntime();
            Debug.Log($"[StateSharedData] 预备主状态判据值: {basicConfig.preparedMainCriterionValue}");
        }
#endif


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
