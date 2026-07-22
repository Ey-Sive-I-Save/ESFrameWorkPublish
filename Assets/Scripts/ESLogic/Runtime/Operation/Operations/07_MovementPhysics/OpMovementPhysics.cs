using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("Transform位移", OperationTypeRegistryNames.Movement)]
    public sealed class OpMovement_Translate : ESOutputOp
    {
        public bool useMainTarget;
        public Vector3ExpressionSource delta = new Vector3ExpressionSource { directVector3 = Vector3.forward };
        public Space space = Space.World;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            Transform transform = GetTransform(target, useMainTarget);
            if (transform != null)
                transform.Translate(delta != null ? delta.Evaluate(target, RuntimeSupport(scopeSupport, hostSupport)) : Vector3.zero, space);
        }

        private static Transform GetTransform(ESRuntimeTargetPack target, bool useMainTarget)
        {
            if (target == null)
                return null;

            return useMainTarget ? target.GetMainTargetTransform() : target.GetTransform();
        }
    }

    [Serializable, TypeRegistryItem("看向主目标", OperationTypeRegistryNames.Rotation)]
    public sealed class OpMovement_LookAtMainTarget : ESOutputOp
    {
        [LabelText("旋转使用者，否则旋转主目标")]
        public bool rotateUser = true;

        [LabelText("保持Y轴")]
        public bool keepY = true;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target == null)
                return;

            Transform self = rotateUser ? target.GetTransform() : target.GetMainTargetTransform();
            Transform other = rotateUser ? target.GetMainTargetTransform() : target.GetTransform();
            if (self == null || other == null)
                return;

            Vector3 point = other.position;
            if (keepY)
                point.y = self.position.y;
            self.LookAt(point);
        }
    }

    [Serializable, TypeRegistryItem("Rigidbody加力", OperationTypeRegistryNames.Physics)]
    public sealed class OpPhysics_AddForce : ESOutputOp
    {
        public GameObjectExpressionSource targetObject = new GameObjectExpressionSource();
        public Vector3ExpressionSource force = new Vector3ExpressionSource { directVector3 = Vector3.forward };
        public ForceMode forceMode = ForceMode.Impulse;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            GameObject obj = targetObject != null ? targetObject.Evaluate(target, support) : null;
            Rigidbody body = obj != null ? obj.GetComponent<Rigidbody>() : null;
            if (body != null)
                body.AddForce(force != null ? force.Evaluate(target, support) : Vector3.zero, forceMode);
        }
    }
}
