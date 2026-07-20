using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("使用者设为主目标", OperationTypeRegistryNames.TargetingUser)]
    public sealed class OpTarget_SetUserAsMainTarget : ESOutputOp
    {
        [LabelText("加入目标列表")]
        public bool addToTargets = true;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target == null)
                return;

            target.SetEntityMainTarget(target.userEntity);
            if (addToTargets)
                target.AddTarget(target.userEntity);
        }
    }

    [Serializable, TypeRegistryItem("表达式设主目标", OperationTypeRegistryNames.TargetingMain)]
    public sealed class OpTarget_SetMainTargetByExpression : ESOutputOp
    {
        [SerializeReference, LabelText("目标表达式"), ESCompactEdit("目标表达式")]
        public ESGetGameObjectExpression expression;

        [LabelText("加入目标列表")]
        public bool addToTargets = true;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target == null || expression == null)
                return;

            Entity entity = FindEntityInSelfOrParents(expression.Evaluate(target, RuntimeSupport(scopeSupport, hostSupport)));
            target.SetEntityMainTarget(entity);
            if (addToTargets)
                target.AddTarget(entity);
        }

        private static Entity FindEntityInSelfOrParents(GameObject gameObject)
        {
            Transform current = gameObject != null ? gameObject.transform : null;
            while (current != null)
            {
                Entity entity = current.GetComponent<Entity>();
                if (entity != null)
                    return entity;

                current = current.parent;
            }

            return null;
        }
    }

    [Serializable, TypeRegistryItem("主目标加入列表", OperationTypeRegistryNames.TargetingList)]
    public sealed class OpTarget_AddMainTargetToList : ESOutputOp
    {
        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            target?.AddTarget(target.entityMainTarget);
        }
    }

    [Serializable, TypeRegistryItem("清空目标列表", OperationTypeRegistryNames.TargetingList)]
    public sealed class OpTarget_ClearTargetList : ESOutputOp
    {
        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            target?.ClearTargets();
        }
    }

    [Serializable, TypeRegistryItem("首个列表目标设为主目标", OperationTypeRegistryNames.TargetingList)]
    public sealed class OpTarget_SetFirstListTargetAsMain : ESOutputOp
    {
        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            if (target == null || target.targetEntities.Count == 0)
                return;

            target.SetEntityMainTarget(target.targetEntities[0]);
        }
    }
}
