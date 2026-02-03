using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 动画配置基类
    /// 用于高级Clip选择和配置,支持多种模式
    /// 所有Calculator实现已移至AnimationMixerCalculators.cs
    /// </summary>
    [Serializable]
    public class StateAnimationConfigData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [SerializeReference,LabelText("动画混合计算器")]
        public StateAnimationMixCalculator calculator = new StateAnimationMixCalculatorForSimpleClip();
        /// <summary>
        /// 获取Clip和起始时间
        /// </summary>
        /// <param name="context">状态上下文</param>
        /// <returns>返回选定的Clip和起始归一化时间</returns>
        public virtual (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            return (null, 0f);
        }

        /// <summary>
        /// 运行时初始化
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            // StateAnimationConfigData目前无预计算需求，但保留接口以便未来扩展
            // 未来可能需要初始化calculator
            _isRuntimeInitialized = true;
        }
    }
}
