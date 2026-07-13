using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("数据信息", "完整技能体数据信息")]
    public class SkillDefinitionDataInfo : SoDataInfo
    {
        [Title("基础")]
        [LabelText("技能标签")]
        public List<string> tags = new List<string>();

        [LabelText("绑定轨道过程")]
        public SkillTrackProcessInfo trackProcess;

        [LabelText("基础状态")]
        public StateAniDataInfo baseStateInfo;

        [Title("解锁与强化")]
        [LabelText("默认解锁")]
        public bool unlockedByDefault = true;

        [LabelText("最大强化等级")]
        public int maxEnhanceLevel = 1;

        [LabelText("联动技能")]
        public List<SkillDefinitionDataInfo> linkedSkills = new List<SkillDefinitionDataInfo>();

        [Title("资源与次数")]
        [LabelText("次数模式")]
        public SkillChargeMode chargeMode = SkillChargeMode.None;

        [ShowIf(nameof(UsesCharges))]
        [LabelText("最大次数")]
        public int maxCharges = 1;

        [ShowIf(nameof(UsesCharges))]
        [LabelText("恢复间隔")]
        public float rechargeInterval = 1f;

        [LabelText("共享资源组")]
        public string sharedResourceGroup;

        [Title("释放控制")]
        [LabelText("打断模式")]
        public SkillCastInterruptMode interruptMode = SkillCastInterruptMode.ManualCancelable;

        [LabelText("允许主动取消")]
        public bool canManualCancel = true;

        [LabelText("释放前提")]
        [SerializeReference]
        public ESGetBoolExpression castCondition;

        [LabelText("初始目标表达式")]
        [SerializeReference]
        public ESGetGameObjectExpression initialTargetExpression;

        [Title("数值准备")]
        [LabelText("基础倍率")]
        public float baseMultiplier = 1f;

        [LabelText("动态倍率表达式")]
        [SerializeReference]
        public ESGetFloatExpression dynamicMultiplierExpression;

        [Title("运行上下文预填充")]
        [LabelText("预填充使用者为状态宿主")]
        public bool prefillUserFromStateHost = true;

        [LabelText("初始目标加入目标列表")]
        public bool addInitialTargetToList = true;

        [Title("扩展支持")]
        [LabelText("相机支持")]
        public SkillCameraSupportMode cameraSupport = SkillCameraSupportMode.None;

        [LabelText("持续性技能")]
        public bool isContinuousSkill;

        [LabelText("启用回调挂点")]
        public bool enableCallbacks;

        private bool UsesCharges()
        {
            return chargeMode == SkillChargeMode.FixedCharges || chargeMode == SkillChargeMode.RechargeOverTime;
        }
    }

    [Serializable]
    public sealed class SkillRuntimePreparedValues
    {
        public float multiplier = 1f;
        public bool canCast = true;
    }
}
