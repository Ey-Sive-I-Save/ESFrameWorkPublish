using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 无条件规则配置 - 作为左值（承接新状态时）
    /// </summary>
    [Serializable]
    public class NormalMergeRule
    {
        [Header("最高层 :  无条件规则 ")]
        [LabelText("启用无条件规则")]
        public bool enableUnconditionalRule = false;
        [LabelText("无条件规则列表"), ShowIf("enableUnconditionalRule")]
        public List<UnconditionalMatchRule> unconditionalRule = new List<UnconditionalMatchRule>();

        [LabelText("第二层 ： 打断判据 : 层级")]
        public StateHitByLayerOption hitByLayerOption = StateHitByLayerOption.SameLevelTest;

        [Header("第三层 : 打断判据 : 状态优先级 ")]
        [LabelText("生效优先级(打断者＞(=)承接者可打断)")]
        public byte EffectialPripority = 0;
        [LabelText("相等是否生效")]
        public bool EqualIsEffectial_ = true;

    }
    //无条件单情况条目
    [Serializable]
    public class UnconditionalMatchRule
    {
        public StateMergeResult matchBackType = StateMergeResult.MergeComplete;
        [LabelText("状态名匹配")]
        public string stateName = "状态名";
        [LabelText("状态ID匹配(仅初始定义有效)")]
        public int stateID = -1;




    }
    [Serializable, TypeRegistryItem("冲突时操作机制")]
    public class StateMergeData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [LabelText("状态占据通道")]
        public StateChannelMask stateChannelMask = StateChannelMask.AllBodyActive;
        public StateStayLevel stayLevel = StateStayLevel.Low;
        //最高级别 - 分为左值和右值两组

        [HorizontalGroup("对称条件")]
        [BoxGroup("对称条件/作为承接者")]
        [HideLabel, InlineProperty]
        public NormalMergeRule asLeftRule;

        [BoxGroup("对称条件/作为尝试加入者")]
        [HideLabel, InlineProperty]
        public NormalMergeRule asRightRule;

        /// <summary>
        /// 运行时初始化
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            // StateMergeData目前无预计算需求，但保留接口以便未来扩展
            _isRuntimeInitialized = true;
        }
    }

}
