using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("打印Context值", OperationTypeRegistryNames.Debug)]
    public sealed class OpDebug_LogContextValue : ESOutputOp
    {
        public string key;
        public string prefix = "Context";

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            object value = support?.Context != null ? support.Context.GetValue(key) : null;
            Debug.Log($"[{prefix}] {key} = {value}");
        }
    }

    [Serializable, TypeRegistryItem("检查Bool表达式是否为真", OperationTypeRegistryNames.DebugAssert)]
    public sealed class OpDebug_AssertBool : ESOutputOp
    {
        public BoolExpressionSource condition = new BoolExpressionSource { directBool = true };
        public string message = "Operation assertion failed.";

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            bool result = condition == null || condition.Evaluate(target, RuntimeSupport(scopeSupport, hostSupport));
            if (!result)
                Debug.LogError(message);
        }
    }

    [Serializable, TypeRegistryItem("绘制DebugRay", OperationTypeRegistryNames.Debug)]
    public sealed class OpDebug_DrawRay : ESOutputOp
    {
        public Vector3ExpressionSource origin = new Vector3ExpressionSource { directVector3 = Vector3.zero };
        public Vector3ExpressionSource direction = new Vector3ExpressionSource { directVector3 = Vector3.forward };
        public Color color = Color.red;
        public FloatExpressionSource duration = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            Debug.DrawRay(
                origin != null ? origin.Evaluate(target, support) : Vector3.zero,
                direction != null ? direction.Evaluate(target, support) : Vector3.forward,
                color,
                duration != null ? duration.Evaluate(target, support) : 1f);
        }
    }
}
