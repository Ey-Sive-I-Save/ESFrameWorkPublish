using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("设置Float", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetFloat : ESOutputOp
    {
        [LabelText("Key")]
        public string key;

        [LabelText("值")]
        public FloatExpressionSource value = new FloatExpressionSource { directFloat = 0f };

        [LabelText("操作")]
        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetFloat(key, value != null ? value.Evaluate(target, support) : 0f, function, false);
        }
    }

    [Serializable, TypeRegistryItem("设置Int", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetInt : ESOutputOp
    {
        public string key;
        public IntExpressionSource value = new IntExpressionSource { directInt = 0 };
        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetInt(key, value != null ? value.Evaluate(target, support) : 0, function, false);
        }
    }

    [Serializable, TypeRegistryItem("设置Bool", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetBool : ESOutputOp
    {
        public string key;
        public BoolExpressionSource value = new BoolExpressionSource { directBool = true };
        public EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetBool(key, value == null || value.Evaluate(target, support), function, false);
        }
    }

    [Serializable, TypeRegistryItem("设置String", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetString : ESOutputOp
    {
        public string key;
        public StringExpressionSource value = new StringExpressionSource { directString = "" };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetStringDirect(key, value != null ? value.Evaluate(target, support) : "", false, false);
        }
    }

    [Serializable, TypeRegistryItem("设置Vector3", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetVector3 : ESOutputOp
    {
        public string key;
        public Vector3ExpressionSource value = new Vector3ExpressionSource { directVector3 = Vector3.zero };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetVectorDirect(key, value != null ? value.Evaluate(target, support) : Vector3.zero, false, false);
        }
    }

    [Serializable, TypeRegistryItem("启用Tag", OperationTypeRegistryNames.StateTag)]
    public sealed class OpContext_EnableTag : ESOutputOp
    {
        public string key;
        [LabelText("有效时间")]
        public FloatExpressionSource duration = new FloatExpressionSource { directFloat = 5f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetTagQuick_SetUseableAndEnable(key, duration != null ? duration.Evaluate(target, support) : 5f, false);
        }
    }

    [Serializable, TypeRegistryItem("禁用Tag", OperationTypeRegistryNames.StateTag)]
    public sealed class OpContext_DisableTag : ESOutputOp
    {
        public string key;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetTagQuick_CancelUse(key, false);
        }
    }

    [Serializable, TypeRegistryItem("上下文Float写入运行目标", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_ReadFloatToRuntimeTarget : ESOutputOp
    {
        public string key;
        public float defaultValue;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target != null)
            {
                ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
                target.runtimeFloat = support?.Context != null ? support.Context.GetFloat(key, defaultValue) : defaultValue;
            }
        }
    }

    [Serializable, TypeRegistryItem("上下文Bool写入运行目标", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_ReadBoolToRuntimeTarget : ESOutputOp
    {
        public string key;
        public bool defaultValue;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target != null)
            {
                ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
                target.runtimeBool = support?.Context != null ? support.Context.GetBool(key, defaultValue) : defaultValue;
            }
        }
    }
}
