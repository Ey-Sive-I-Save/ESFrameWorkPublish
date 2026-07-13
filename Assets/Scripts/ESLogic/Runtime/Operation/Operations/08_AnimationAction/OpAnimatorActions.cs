using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("设置Trigger", OperationTypeRegistryNames.Animator)]
    public sealed class OpAnimator_SetTrigger : ESOutputOp
    {
        public string triggerName;
        public bool useMainTargetAnimator;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            Animator animator = OpAnimatorUtility.GetAnimator(target, useMainTargetAnimator);
            if (animator != null && !string.IsNullOrEmpty(triggerName))
                animator.SetTrigger(triggerName);
        }
    }

    [Serializable, TypeRegistryItem("设置Bool", OperationTypeRegistryNames.Animator)]
    public sealed class OpAnimator_SetBool : ESOutputOp
    {
        public string parameterName;
        public BoolExpressionSource value = new BoolExpressionSource { directBool = true };
        public bool useMainTargetAnimator;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            Animator animator = OpAnimatorUtility.GetAnimator(target, useMainTargetAnimator);
            if (animator != null && !string.IsNullOrEmpty(parameterName))
                animator.SetBool(parameterName, value == null || value.Evaluate(target, logic));
        }
    }

    [Serializable, TypeRegistryItem("设置Float", OperationTypeRegistryNames.Animator)]
    public sealed class OpAnimator_SetFloat : ESOutputOp
    {
        public string parameterName;
        public FloatExpressionSource value = new FloatExpressionSource { directFloat = 0f };
        public bool useMainTargetAnimator;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            Animator animator = OpAnimatorUtility.GetAnimator(target, useMainTargetAnimator);
            if (animator != null && !string.IsNullOrEmpty(parameterName))
                animator.SetFloat(parameterName, value != null ? value.Evaluate(target, logic) : 0f);
        }
    }

    [Serializable, TypeRegistryItem("播放状态", OperationTypeRegistryNames.Animator)]
    public sealed class OpAnimator_PlayState : ESOutputOp
    {
        public string stateName;
        public int layer = -1;
        [Range(0f, 1f)]
        public float normalizedTime = 0f;
        public bool useMainTargetAnimator;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            Animator animator = OpAnimatorUtility.GetAnimator(target, useMainTargetAnimator);
            if (animator != null && !string.IsNullOrEmpty(stateName))
                animator.Play(stateName, layer, normalizedTime);
        }
    }

    internal static class OpAnimatorUtility
    {
        public static Animator GetAnimator(ESRuntimeTargetPack target, bool useMainTarget)
        {
            Entity entity = useMainTarget ? target?.entityMainTarget : target?.userEntity;
            return entity != null ? entity.animator : null;
        }
    }
}
