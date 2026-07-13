using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("空操作", OperationTypeRegistryNames.Common)]
    public sealed class OpCommon_NoOp : ESOutputOp
    {
        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic) { }
    }

    [Serializable, TypeRegistryItem("顺序执行", OperationTypeRegistryNames.Common)]
    public sealed class OpCommon_Sequence : ESOutputOp
    {
        [SerializeReference, HideReferenceObjectPicker, LabelText("操作列表")]
        [ESCompactEdit("操作列表")]
        public List<ESOutputOp> operations = new List<ESOutputOp>();

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (operations == null)
                return;

            for (int i = 0; i < operations.Count; i++)
                operations[i]?._TryStartOp(target, logic);
        }

        protected override void StopOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (operations == null)
                return;

            for (int i = operations.Count - 1; i >= 0; i--)
                operations[i]?._TryStopOp(target, logic);
        }
    }

    [Serializable, TypeRegistryItem("Bool 条件执行", OperationTypeRegistryNames.Condition)]
    public sealed class OpCondition_IfBool : ESOutputOp
    {
        [LabelText("条件")]
        public BoolExpressionSource condition = new BoolExpressionSource { directBool = true };

        [SerializeReference, HideReferenceObjectPicker, LabelText("成立时")]
        [ESCompactEdit("成立时")]
        public ESOutputOp onTrue;

        [SerializeReference, HideReferenceObjectPicker, LabelText("不成立时")]
        [ESCompactEdit("不成立时")]
        public ESOutputOp onFalse;

        private ESOutputOp runningOp;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            bool result = condition == null || condition.Evaluate(target, logic);
            runningOp = result ? onTrue : onFalse;
            runningOp?._TryStartOp(target, logic);
        }

        protected override void StopOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            runningOp?._TryStopOp(target, logic);
            runningOp = null;
        }
    }

    [Serializable, TypeRegistryItem("ContextBool 条件执行", OperationTypeRegistryNames.Condition)]
    public sealed class OpCondition_IfContextBool : ESOutputOp
    {
        [LabelText("Context Key")]
        public string key;

        [LabelText("默认值")]
        public bool defaultValue;

        [SerializeReference, HideReferenceObjectPicker, LabelText("为真")]
        [ESCompactEdit("为真")]
        public ESOutputOp onTrue;

        [SerializeReference, HideReferenceObjectPicker, LabelText("为假")]
        [ESCompactEdit("为假")]
        public ESOutputOp onFalse;

        private ESOutputOp runningOp;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            bool result = logic != null && logic.Context != null
                ? logic.Context.GetBool(key, defaultValue)
                : defaultValue;

            runningOp = result ? onTrue : onFalse;
            runningOp?._TryStartOp(target, logic);
        }

        protected override void StopOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            runningOp?._TryStopOp(target, logic);
            runningOp = null;
        }
    }
}
