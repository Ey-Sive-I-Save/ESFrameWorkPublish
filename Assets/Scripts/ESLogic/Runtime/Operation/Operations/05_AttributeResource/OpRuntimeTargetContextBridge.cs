using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("运行目标Float写入上下文", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_WriteRuntimeFloatToContext : ESOutputOp
    {
        public string key;
        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                logic?.Context?.SetFloat(key, target.runtimeFloat, function);
        }
    }

    [Serializable, TypeRegistryItem("运行目标Bool写入上下文", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_WriteRuntimeBoolToContext : ESOutputOp
    {
        public string key;
        public EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                logic?.Context?.SetBool(key, target.runtimeBool, function);
        }
    }

    [Serializable, TypeRegistryItem("设置运行目标Float", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_SetRuntimeFloat : ESOutputOp
    {
        public FloatExpressionSource value = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                target.runtimeFloat = value != null ? value.Evaluate(target, logic) : 1f;
        }
    }

    [Serializable, TypeRegistryItem("设置运行目标Bool", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_SetRuntimeBool : ESOutputOp
    {
        public BoolExpressionSource value = new BoolExpressionSource { directBool = true };

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                target.runtimeBool = value == null || value.Evaluate(target, logic);
        }
    }
}
