using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("激活状态", OperationTypeRegistryNames.ReleaseFlow)]
    public sealed class OpStateMachine_ActivateState : ESOutputOp
    {
        [LabelText("状态Key")]
        public string stateKey;

        [LabelText("层级")]
        public StateLayerType layer = StateLayerType.NotClear;

        [LabelText("作用主目标")]
        public bool useMainTarget;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            StateMachine machine = GetStateMachine(target, useMainTarget);
            if (machine != null && !string.IsNullOrEmpty(stateKey))
                machine.TryActivateState(stateKey, layer);
        }

        private static StateMachine GetStateMachine(ESRuntimeTargetPack target, bool useMainTarget)
        {
            Entity entity = useMainTarget ? target?.entityMainTarget : target?.userEntity;
            return entity != null && entity.stateDomain != null ? entity.stateDomain.stateMachine : null;
        }
    }

    [Serializable, TypeRegistryItem("退出状态", OperationTypeRegistryNames.ReleaseFlow)]
    public sealed class OpStateMachine_DeactivateState : ESOutputOp
    {
        public string stateKey;
        public bool useMainTarget;

        protected override void StartOperation(ESRuntimeTargetPack target, IOperationRuntimeServices logic)
        {
            StateMachine machine = GetStateMachine(target, useMainTarget);
            if (machine != null && !string.IsNullOrEmpty(stateKey))
                machine.TryDeactivateState(stateKey);
        }

        private static StateMachine GetStateMachine(ESRuntimeTargetPack target, bool useMainTarget)
        {
            Entity entity = useMainTarget ? target?.entityMainTarget : target?.userEntity;
            return entity != null && entity.stateDomain != null ? entity.stateDomain.stateMachine : null;
        }
    }
}
