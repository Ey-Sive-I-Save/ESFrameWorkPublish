using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("数据信息", "完整技能体数据信息")]
    public class SkillDefinitionDataInfo : SoDataInfo, IGameCoreSO
    {
        [Title("基础")]
        [LabelText("Skill Tags")]
        public List<string> tags = new List<string>();

        [Title("Runtime Key")]
        [HideLabel, InlineProperty]
        public ESSkillConfigKey skillKey = new ESSkillConfigKey();

        [LabelText("绑定轨道过程")]
        public SkillTrackProcessInfo trackProcess;

        [LabelText("Base State")]
        public StateAniDataInfo baseStateInfo;

        [Title("Unlock And Upgrade")]
        [LabelText("默认解锁")]
        public bool unlockedByDefault = true;

        [LabelText("Max Enhance Level")]
        public int maxEnhanceLevel = 1;

        [LabelText("Linked Skills")]
        public List<SkillDefinitionDataInfo> linkedSkills = new List<SkillDefinitionDataInfo>();

        [Title("Resource And Charges")]
        [LabelText("次数模式")]
        public SkillChargeMode chargeMode = SkillChargeMode.None;

        [ShowIf(nameof(UsesCharges))]
        [LabelText("Max Charges")]
        public int maxCharges = 1;

        [ShowIf(nameof(UsesCharges))]
        [LabelText("恢复间隔")]
        public float rechargeInterval = 1f;

        [LabelText("Shared Resource Group")]
        public string sharedResourceGroup;

        [Title("释放控制")]
        [LabelText("打断模式")]
        public SkillCastInterruptMode interruptMode = SkillCastInterruptMode.ManualCancelable;

        [LabelText("允许主动取消")]
        public bool canManualCancel = true;

        [LabelText("释放前提")]
        [SerializeReference]
        public ESGetBoolExpression castCondition;

        [LabelText("Initial Target Expression")]
        [SerializeReference]
        public ESGetGameObjectExpression initialTargetExpression;

        [Title("Value Prepare")]
        [LabelText("基础倍率")]
        public float baseMultiplier = 1f;

        [LabelText("Dynamic Multiplier Expression")]
        [SerializeReference]
        public ESGetFloatExpression dynamicMultiplierExpression;

        [Title("运行上下文预填充")]
        [LabelText("Prefill User From State Host")]
        public bool prefillUserFromStateHost = true;

        [LabelText("初始目标加入目标列表")]
        public bool addInitialTargetToList = true;

        [Title("扩展支持")]
        [LabelText("相机支持")]
        public SkillCameraSupportMode cameraSupport = SkillCameraSupportMode.None;

        [LabelText("Continuous Skill")]
        public bool isContinuousSkill;

        [LabelText("启用回调挂点")]
        public bool enableCallbacks;

        private bool UsesCharges()
        {
            return chargeMode == SkillChargeMode.FixedCharges || chargeMode == SkillChargeMode.RechargeOverTime;
        }

        public void InjectGameCoreTables()
        {
            ESSkillGameCoreTable.Inject(this);
        }
    }

    /// <summary>Skill 领域强类型注册入口；根 SO 直接注入，不经过中央类别分发。</summary>
    public static class ESSkillGameCoreTable
    {
        public static ESConfigKeyTable<ESSkillRuntimeData> Table => ESRuntimeDataGameCore.Skills;

        public static void Inject(SkillDefinitionDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.skillKey ??= new ESSkillConfigKey();
                if (Table.TryGet(info.skillKey, out ESSkillRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info)) return;
                    throw new InvalidOperationException("Skill GameCore Key 重复：" + info.KeyName);
                }

                var data = new ESSkillRuntimeData
                {
                    keyName = info.KeyName,
                    displayName = info.KeyName,
                    sourcePackage = info.name,
                    soSource = info,
                    trackProcess = info.trackProcess,
                    baseStateInfo = info.baseStateInfo
                };
                data.runtimeKey = Table.Bake(info.skillKey, info.KeyName);
                if (!Table.Upsert(info.skillKey, data, info.KeyName))
                    throw new InvalidOperationException("Skill GameCore 注入失败：" + info.KeyName);
            }
            finally
            {
                if (ownsBuild) Table.EndBuild();
            }
        }
    }

    [Serializable]
    public sealed class SkillRuntimePreparedValues
    {
        public float multiplier = 1f;
        public bool canCast = true;
    }
}
