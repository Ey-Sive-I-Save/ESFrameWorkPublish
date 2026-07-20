using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("运行目标Float写入上下文", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_WriteRuntimeFloatToContext : ESOutputOp
    {
        public string key;
        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target != null)
            {
                ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
                support?.Context?.SetFloat(key, target.runtimeFloat, function, false);
            }
        }
    }

    [Serializable, TypeRegistryItem("运行目标Bool写入上下文", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_WriteRuntimeBoolToContext : ESOutputOp
    {
        public string key;
        public EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target != null)
            {
                ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
                support?.Context?.SetBool(key, target.runtimeBool, function, false);
            }
        }
    }

    [Serializable, TypeRegistryItem("设置运行目标Float", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_SetRuntimeFloat : ESOutputOp
    {
        public FloatExpressionSource value = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target != null)
            {
                ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
                target.runtimeFloat = value != null ? value.Evaluate(target, support) : 1f;
            }
        }
    }

    [Serializable, TypeRegistryItem("设置运行目标Bool", OperationTypeRegistryNames.Value)]
    public sealed class OpRuntimeTarget_SetRuntimeBool : ESOutputOp
    {
        public BoolExpressionSource value = new BoolExpressionSource { directBool = true };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target != null)
            {
                ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
                target.runtimeBool = value == null || value.Evaluate(target, support);
            }
        }
    }
}
