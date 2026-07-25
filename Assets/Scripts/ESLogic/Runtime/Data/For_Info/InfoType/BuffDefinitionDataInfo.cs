using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("数据信息", "Buff定义数据")]
    public class BuffDefinitionDataInfo : SoDataInfo, ISharedAndVariable<BuffSharedData, BuffVariableData>, IGameCoreSO
    {
        [TitleGroup("Buff定义/共享数据", "共享数据", Alignment = TitleAlignments.Left)]
        [HideLabel, InlineProperty]
        public BuffSharedData sharedData = new BuffSharedData();

        [TitleGroup("Buff定义/默认可变数据", "默认可变数据", Alignment = TitleAlignments.Left)]
        [HideLabel, InlineProperty]
        public BuffVariableData variableData = new BuffVariableData();

        public BuffSharedData SharedData
        {
            get => sharedData;
            set => sharedData = value;
        }

        public BuffVariableData VariableData
        {
            get => variableData;
            set => variableData = value;
        }

        private void OnValidate() { }

        public void InjectGameCoreTables()
        {
            ESRuntimeDataModule.InjectGameCoreRoot(this);
        }
    }

    [Serializable]
    public sealed class BuffSharedData
    {
        [Title("Key")]
        [HideLabel, InlineProperty]
        public ESBuffConfigKey key = new ESBuffConfigKey();

        [Title("基础")]
        [LabelText("标签")]
        public List<ESGameTag> tags = new List<ESGameTag>();

        [LabelText("默认持续时间")]
        public float duration = 5f;

        [Title("叠层 / 互斥 / 来源")]
        [LabelText("Buff Group")]
        public string buffGroup;

        [LabelText("强度")]
        public int strength = 0;

        [LabelText("来源隔离")]
        public ESBuffSourceIsolationMode sourceIsolationMode = ESBuffSourceIsolationMode.IgnoreSource;

        [LabelText("叠层模式")]
        public ESBuffStackMode stackMode = ESBuffStackMode.StackSameBuff;

        [LabelText("时间刷新")]
        public ESBuffTimeRefreshMode timeRefreshMode = ESBuffTimeRefreshMode.ResetDuration;

        [LabelText("Group Conflict")]
        public ESBuffGroupConflictMode groupConflictMode = ESBuffGroupConflictMode.None;

        [LabelText("Max Stack")]
        [Min(1)]
        public int maxStack = 1;

        [Title("Tick")]
        [LabelText("Tick模式")]
        public ESBuffTickMode tickMode = ESBuffTickMode.None;

        [LabelText("Tick间隔")]
        [Min(0f)]
        public float tickInterval = 1f;

        [Title("Op")]
        [LabelText("On Apply Op")]
        [SerializeReference]
        public ESOutputOp onApplyOp;

        [SerializeReference]
        public ESOutputOp onRefreshOp;

        [SerializeReference]
        public ESOutputOp onTickOp;

        [SerializeReference]
        public ESOutputOp onRemoveOp;

        [Title("ValueChange")]
        [LabelText("Float Changes")]
        [SerializeReference]
        public List<ESBuffFloatValueChangeBinding> floatChanges = new List<ESBuffFloatValueChangeBinding>();

        [LabelText("权限变化")]
        [SerializeReference]
        public List<ESBuffPermitValueChangeBinding> permitChanges = new List<ESBuffPermitValueChangeBinding>();

    }

    [Serializable]
    public sealed class BuffVariableData : IDeepClone<BuffVariableData>
    {
        [LabelText("层数")]
        [Min(1)]
        public int stackCount = 1;

        [LabelText("剩余时间")]
        public float remainingTime;

        [LabelText("Elapsed Time")]
        public float elapsedTime;

        [LabelText("Tick累计")]
        public float tickAccumulator;

        [LabelText("来源Key")]
        public int sourceKey;

        public void DeepCloneFrom(BuffVariableData t)
        {
            if (t == null)
                return;

            stackCount = t.stackCount;
            remainingTime = t.remainingTime;
            elapsedTime = t.elapsedTime;
            tickAccumulator = t.tickAccumulator;
            sourceKey = t.sourceKey;
        }
    }

    [Serializable]
    public sealed class ESBuffFloatValueChangeBinding
    {
        [LabelText("Stat Key")]
        public string statKey;

        [LabelText("变化")]
        public ESFloatValueChangeExpressionBinding change = new ESFloatValueChangeExpressionBinding();
    }

    [Serializable]
    public sealed class ESBuffPermitValueChangeBinding
    {
        [LabelText("Permit Key")]
        public string permitKey;

        [LabelText("变化")]
        public ESPermitValueChangeExpressionBinding change = new ESPermitValueChangeExpressionBinding();
    }
}
