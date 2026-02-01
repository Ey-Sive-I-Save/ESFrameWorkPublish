using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ES
{
    /// <summary>
    /// 动画配置基类
    /// 用于高级Clip选择和配置,支持多种模式
    /// 所有Calculator实现已移至AnimationMixerCalculators.cs
    /// </summary>
    [Serializable]
    public abstract class StateAnimationConfigData
    {
        /// <summary>
        /// 获取Clip和起始时间
        /// </summary>
        /// <param name="context">状态上下文</param>
        /// <returns>返回选定的Clip和起始归一化时间</returns>
        public abstract (AnimationClip clip, float normalizedTime) GetClipAndTime(StateContext context);
    }
}
