using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("状态域模块基类")]
    public abstract class EntityStateModuleBase : Module<Entity, EntityStateDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable, TypeRegistryItem("状态机模块")]
    public class EntityStateMachineModule : EntityStateModuleBase
    {
        [Title("演示开关（默认关闭）")]
        public bool enableDemoLogic = false;

        public StateMachine stateMachine;
        public string defaultStateKey;

        public override void Start()
        {
            if (!enableDemoLogic) return;
            if (stateMachine == null)
            {
                stateMachine = new StateMachine();
            }

            if (MyCore != null)
            {
                stateMachine.stateMachineKey =  "Entity";
                stateMachine.Initialize(MyCore, MyCore.animator);
                stateMachine.defaultStateKey = defaultStateKey;
                stateMachine.StartStateMachine();
            }
        }

        protected override void Update()
        {
            if (!enableDemoLogic) return;
            stateMachine?.UpdateStateMachine();
        }

        public override void OnDestroy()
        {
            stateMachine?.StopStateMachine();
            stateMachine?.Dispose();
            stateMachine = null;
            base.OnDestroy();
        }
    }
}
