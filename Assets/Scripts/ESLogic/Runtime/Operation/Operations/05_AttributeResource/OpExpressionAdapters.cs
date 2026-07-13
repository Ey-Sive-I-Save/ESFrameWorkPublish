using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("表达式结果写入上下文Float", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteFloatToContext : ESOutputOp
    {
        [LabelText("Key")]
        public string key;

        [SerializeReference, InlineProperty, LabelText("Float 表达式"), ESCompactEdit("Float 表达式")]
        public ESGetFloatExpression expression;

        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetFloat(key, expression != null ? expression.Evaluate(target, logic) : 0f, function);
        }
    }

    [Serializable, TypeRegistryItem("表达式结果写入上下文Int", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteIntToContext : ESOutputOp
    {
        [LabelText("Key")]
        public string key;

        [SerializeReference, InlineProperty, LabelText("Int 表达式"), ESCompactEdit("Int 表达式")]
        public ESGetIntExpression expression;

        public EnumCollect.HandleTwoNumber function = EnumCollect.HandleTwoNumber.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetInt(key, expression != null ? expression.Evaluate(target, logic) : 0, function);
        }
    }

    [Serializable, TypeRegistryItem("表达式结果写入上下文Bool", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteBoolToContext : ESOutputOp
    {
        [LabelText("Key")]
        public string key;

        [SerializeReference, InlineProperty, LabelText("Bool 表达式"), ESCompactEdit("Bool 表达式")]
        public ESGetBoolExpression expression;

        public EnumCollect.HandleTwoBool function = EnumCollect.HandleTwoBool.Set;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetBool(key, expression == null || expression.Evaluate(target, logic), function);
        }
    }

    [Serializable, TypeRegistryItem("表达式结果写入上下文String", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteStringToContext : ESOutputOp
    {
        [LabelText("Key")]
        public string key;

        [SerializeReference, InlineProperty, LabelText("String 表达式"), ESCompactEdit("String 表达式")]
        public ESGetStringExpression expression;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetStringDirect(key, expression != null ? expression.Evaluate(target, logic) : string.Empty);
        }
    }

    [Serializable, TypeRegistryItem("表达式结果写入上下文Vector3", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteVector3ToContext : ESOutputOp
    {
        [LabelText("Key")]
        public string key;

        [SerializeReference, InlineProperty, LabelText("Vector3 表达式"), ESCompactEdit("Vector3 表达式")]
        public ESGetVector3Expression expression;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            logic?.Context?.SetVectorDirect(key, expression != null ? expression.Evaluate(target, logic) : Vector3.zero);
        }
    }

    [Serializable, TypeRegistryItem("表达式结果写入运行目标Float", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteFloatToRuntimeTarget : ESOutputOp
    {
        [SerializeReference, InlineProperty, LabelText("Float 表达式"), ESCompactEdit("Float 表达式")]
        public ESGetFloatExpression expression;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                target.runtimeFloat = expression != null ? expression.Evaluate(target, logic) : 0f;
        }
    }

    [Serializable, TypeRegistryItem("表达式结果写入运行目标Bool", OperationTypeRegistryNames.Value)]
    public sealed class OpExpression_WriteBoolToRuntimeTarget : ESOutputOp
    {
        [SerializeReference, InlineProperty, LabelText("Bool 表达式"), ESCompactEdit("Bool 表达式")]
        public ESGetBoolExpression expression;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            if (target != null)
                target.runtimeBool = expression == null || expression.Evaluate(target, logic);
        }
    }
}
