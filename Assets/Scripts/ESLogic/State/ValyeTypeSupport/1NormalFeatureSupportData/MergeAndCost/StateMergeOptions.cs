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
        [Tooltip("无条件规则=优先于通道/代价/优先级的直接裁决；命中后按 matchBackType 立即决定合并/打断/拒绝。")]
        public bool enableUnconditionalRule = false;
        [LabelText("无条件规则列表"), ShowIf("enableUnconditionalRule")]
        [Tooltip("配置与某个状态名/ID匹配时的直接结果，注意这是承接者/尝试加入者各自的规则集合。")]
        public List<UnconditionalMatchRule> unconditionalRule = new List<UnconditionalMatchRule>();

        [LabelText("第二层 ： 打断判据 : 层级")]
        [Tooltip("打断判据(层级)作用于通道重叠且代价不允许合并时；作为承接者时决定是否允许被打断，作为尝试加入者时决定是否允许打断他人。")]
        public StateHitByLayerOption hitByLayerOption = StateHitByLayerOption.SameLevelTest;

        [Header("第三层 : 打断判据 : 状态优先级 ")]
        [LabelText("生效优先级(打断者＞(=)承接者可打断)")]
        [Tooltip("优先级是打断阶段的比较值；作为尝试加入者时你的优先级更高才可能打断对方，作为承接者时优先级更低会更容易被打断。")]
        public byte EffectialPripority = 0;
        [LabelText("相等是否生效")]
        [Tooltip("优先级相等时是否允许打断；作为尝试加入者为 true 表示你可以打断相等优先级的承接者，作为承接者为 true 表示你会被相等优先级的加入者打断。")]
        public bool EqualIsEffectial_ = true;

    }
    //无条件单情况条目
    [Serializable]
    public class UnconditionalMatchRule
    {
        public StateMergeResult matchBackType = StateMergeResult.MergeComplete;
        [LabelText("状态名匹配")]
        [Tooltip("匹配命中后直接返回 matchBackType，忽略代价与通道。")]
        public string stateName = "状态名";
        [LabelText("状态ID匹配(仅初始定义有效)")]
        [Tooltip("匹配命中后直接返回 matchBackType，忽略代价与通道。")]
        public int stateID = -1;




    }
    [Serializable, TypeRegistryItem("冲突时操作机制")]
    public class StateMergeData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [LabelText("状态占据通道")]
        [Tooltip("通道表示身体/意图占用范围；通道重叠时才进入代价与打断规则，通道不重叠则默认可并行。")]
        public StateChannelMask stateChannelMask = StateChannelMask.AllBodyActive;
        [Tooltip("停留等级用于通道冲突时的稳定性判断；作为承接者时停留等级更高更不易被打断，作为尝试加入者时停留等级更高更容易打断对方。")]
        public StateStayLevel stayLevel = StateStayLevel.Low;
        //最高级别 - 分为左值和右值两组

        [HorizontalGroup("对称条件")]
        [BoxGroup("对称条件/作为承接者")]
        [Tooltip("作为承接者：当已有状态在运行时，本规则用于决定是否接纳新状态或被打断。")]
        [HideLabel, InlineProperty]
        public NormalMergeRule asLeftRule;

        [BoxGroup("对称条件/作为尝试加入者")]
        [Tooltip("作为尝试加入者：当新状态想加入时，本规则用于决定是否能打断已有状态或被拒绝。")]
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
