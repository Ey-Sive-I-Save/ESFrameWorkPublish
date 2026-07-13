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

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetFloat(key, value != null ? value.Evaluate(target, logic) : 0f, function);
        }
    }

    [Serializable, TypeRegistryItem("设置Int", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetInt : ESOutputOp
    {
        public string key;
        public IntExpressionSource value = new IntExpressionSource { directInt = 0 };
        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetInt(key, value != null ? value.Evaluate(target, logic) : 0, function);
        }
    }

    [Serializable, TypeRegistryItem("设置Bool", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetBool : ESOutputOp
    {
        public string key;
        public BoolExpressionSource value = new BoolExpressionSource { directBool = true };
        public EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetBool(key, value == null || value.Evaluate(target, logic), function);
        }
    }

    [Serializable, TypeRegistryItem("设置String", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetString : ESOutputOp
    {
        public string key;
        public StringExpressionSource value = new StringExpressionSource { directString = "" };

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetStringDirect(key, value != null ? value.Evaluate(target, logic) : "");
        }
    }

    [Serializable, TypeRegistryItem("设置Vector3", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_SetVector3 : ESOutputOp
    {
        public string key;
        public Vector3ExpressionSource value = new Vector3ExpressionSource { directVector3 = Vector3.zero };

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetVectorDirect(key, value != null ? value.Evaluate(target, logic) : Vector3.zero);
        }
    }

    [Serializable, TypeRegistryItem("启用Tag", OperationTypeRegistryNames.StateTag)]
    public sealed class OpContext_EnableTag : ESOutputOp
    {
        public string key;
        [LabelText("有效时间")]
        public FloatExpressionSource duration = new FloatExpressionSource { directFloat = 5f };

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetTagQuick_SetUseableAndEnable(key, duration != null ? duration.Evaluate(target, logic) : 5f);
        }
    }

    [Serializable, TypeRegistryItem("禁用Tag", OperationTypeRegistryNames.StateTag)]
    public sealed class OpContext_DisableTag : ESOutputOp
    {
        public string key;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetTagQuick_CancelUse(key);
        }
    }

    [Serializable, TypeRegistryItem("上下文Float写入运行目标", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_ReadFloatToRuntimeTarget : ESOutputOp
    {
        public string key;
        public float defaultValue;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                target.runtimeFloat = logic?.Context != null ? logic.Context.GetFloat(key, defaultValue) : defaultValue;
        }
    }

    [Serializable, TypeRegistryItem("上下文Bool写入运行目标", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_ReadBoolToRuntimeTarget : ESOutputOp
    {
        public string key;
        public bool defaultValue;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                target.runtimeBool = logic?.Context != null ? logic.Context.GetBool(key, defaultValue) : defaultValue;
        }
    }
}
