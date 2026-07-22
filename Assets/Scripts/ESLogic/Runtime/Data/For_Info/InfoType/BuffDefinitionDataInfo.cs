using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("鏁版嵁淇℃伅", "Buff瀹氫箟鏁版嵁")]
    public class BuffDefinitionDataInfo : SoDataInfo, ISharedAndVariable<BuffSharedData, BuffVariableData>
    {
        [TitleGroup("Buff瀹氫箟/鍏变韩鏁版嵁", "鍏变韩鏁版嵁", Alignment = TitleAlignments.Left)]
        [HideLabel, InlineProperty]
        public BuffSharedData sharedData = new BuffSharedData();

        [TitleGroup("Buff瀹氫箟/榛樿鍙彉鏁版嵁", "榛樿鍙彉鏁版嵁", Alignment = TitleAlignments.Left)]
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
    }

    [Serializable]
    public sealed class BuffSharedData
    {
        [Title("Key")]
        [HideLabel, InlineProperty]
        public ESBuffConfigKey key = new ESBuffConfigKey();

        [Title("鍩虹")]
        [LabelText("鏍囩")]
        public List<ESGameTag> tags = new List<ESGameTag>();

        [LabelText("榛樿鎸佺画鏃堕棿")]
        public float duration = 5f;

        [Title("鍙犲眰 / 浜掓枼 / 鏉ユ簮")]
        [LabelText("Buff Group")]
        public string buffGroup;

        [LabelText("寮哄害")]
        public int strength = 0;

        [LabelText("鏉ユ簮闅旂")]
        public ESBuffSourceIsolationMode sourceIsolationMode = ESBuffSourceIsolationMode.IgnoreSource;

        [LabelText("鍙犲眰妯″紡")]
        public ESBuffStackMode stackMode = ESBuffStackMode.StackSameBuff;

        [LabelText("鏃堕棿鍒锋柊")]
        public ESBuffTimeRefreshMode timeRefreshMode = ESBuffTimeRefreshMode.ResetDuration;

        [LabelText("Group Conflict")]
        public ESBuffGroupConflictMode groupConflictMode = ESBuffGroupConflictMode.None;

        [LabelText("Max Stack")]
        [Min(1)]
        public int maxStack = 1;

        [Title("Tick")]
        [LabelText("Tick妯″紡")]
        public ESBuffTickMode tickMode = ESBuffTickMode.None;

        [LabelText("Tick闂撮殧")]
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

        [LabelText("鏉冮檺鍙樺寲")]
        [SerializeReference]
        public List<ESBuffPermitValueChangeBinding> permitChanges = new List<ESBuffPermitValueChangeBinding>();

    }

    [Serializable]
    public sealed class BuffVariableData : IDeepClone<BuffVariableData>
    {
        [LabelText("灞傛暟")]
        [Min(1)]
        public int stackCount = 1;

        [LabelText("鍓╀綑鏃堕棿")]
        public float remainingTime;

        [LabelText("Elapsed Time")]
        public float elapsedTime;

        [LabelText("Tick绱")]
        public float tickAccumulator;

        [LabelText("鏉ユ簮Key")]
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

        [LabelText("鍙樺寲")]
        public ESFloatValueChangeExpressionBinding change = new ESFloatValueChangeExpressionBinding();
    }

    [Serializable]
    public sealed class ESBuffPermitValueChangeBinding
    {
        [LabelText("Permit Key")]
        public string permitKey;

        [LabelText("鍙樺寲")]
        public ESPermitValueChangeExpressionBinding change = new ESPermitValueChangeExpressionBinding();
    }
}
