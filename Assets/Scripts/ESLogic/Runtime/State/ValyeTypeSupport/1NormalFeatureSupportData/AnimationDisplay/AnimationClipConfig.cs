using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 动画Clip配置 - 包裹Clip及其扩展参数
    /// 用于所有计算器（除OriginalSimple和多向混合外）
    /// </summary>
    [Serializable]
    public class AnimationClipConfig
    {
        [LabelText("动画Clip"), Required]
        [Tooltip("要播放的AnimationClip")]
        public AnimationClip clip;

        [LabelText("播放速度"), Range(0.1f, 3f)]
        [Tooltip("动画播放速度倍率，1为正常速度")]
        public float speed = 1f;

        [LabelText("覆盖键")]
        [Tooltip("运行时覆盖Clip的键，留空则使用默认")]
        public string overrideKey = "";

        [LabelText("事件触发点")]
        [Tooltip("归一化时间点触发事件，0=开始，1=结束")]
        public List<TriggerEventAt> triggerEvents = new List<TriggerEventAt>();

        /// <summary>
        /// 快速创建配置
        /// </summary>
        public static AnimationClipConfig Create(AnimationClip clip, float speed = 1f)
        {
            return new AnimationClipConfig
            {
                clip = clip,
                speed = speed
            };
        }
    }

    /// <summary>
    /// 事件触发点配置
    /// </summary>
    [Serializable]
    public class TriggerEventAt
    {
        [LabelText("触发时间点"), Range(0f, 1f)]
        [Tooltip("归一化时间点，0=开始，0.5=中间，1=结束")]
        public float normalizedTime = 0.5f;

        [LabelText("事件名称")]
        [Tooltip("触发时广播的事件名称")]
        public string eventName = "OnAnimEvent";

        [LabelText("事件参数")]
        [Tooltip("事件携带的字符串参数")]
        public string eventParam = "";

        [LabelText("仅触发一次")]
        [Tooltip("每次播放只触发一次，否则每次经过该时间点都触发")]
        public bool triggerOnce = true;

        // 运行时标记
        [NonSerialized]
        public bool hasTriggered = false;

        /// <summary>
        /// 重置触发标记
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
        }
    }
}
