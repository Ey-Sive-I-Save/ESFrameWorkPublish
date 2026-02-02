using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("状态域")]
    public class EntityStateDomain : Domain<Entity, EntityStateModuleBase>
    {
        [Title("状态数据")]
        [LabelText("动画状态数据包")]
        public StateAniDataPack stateAniDataPack;

        [Title("状态机")]
        [LabelText("状态机")]
        public StateMachine stateMachine = new StateMachine();

        [LabelText("默认状态键")]
        public string defaultStateKey = "";

        [NonSerialized] private bool _stateMachineInitialized;
        [NonSerialized] private Animator _cachedAnimator;

        [NonSerialized] private StateAniDataPack _cachedPack;
        [NonSerialized] private bool _packDirty = true;
        [NonSerialized] private List<StateAniDataInfo> _cachedInfos = new List<StateAniDataInfo>(64);

        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
            InitializeStateAniDataPack();
            InitializeStateMachine();
        }

        protected override void Update()
        {
            if (_stateMachineInitialized)
            {
                stateMachine.UpdateStateMachine();
            }
            base.Update();
        }

        protected override void OnDestroy()
        {
            if (_stateMachineInitialized)
            {
                stateMachine.StopStateMachine();
                stateMachine.Dispose();
                _stateMachineInitialized = false;
            }
            base.OnDestroy();
        }

        public void MarkStatePackDirty()
        {
            _packDirty = true;
        }

        private void InitializeStateAniDataPack()
        {
            if (stateAniDataPack == null) return;
            if (_cachedPack != stateAniDataPack)
            {
                _cachedPack = stateAniDataPack;
                _packDirty = true;
            }

            if (!_packDirty) return;

            stateAniDataPack.Check();
            _cachedInfos.Clear();
            foreach (var info in stateAniDataPack.Infos.Values)
            {
                if (info != null)
                {
                    info.Initialize();
                    _cachedInfos.Add(info);
                }
            }

            _packDirty = false;
        }

        private void InitializeStateMachine()
        {
            if (MyCore == null) return;
            if (_cachedAnimator == null)
            {
                _cachedAnimator = MyCore.animator;
            }
            if (_cachedAnimator == null) return;

            if (stateMachine == null) stateMachine = new StateMachine();
            stateMachine.stateMachineKey = string.IsNullOrEmpty(defaultStateKey) ? "Entity" : defaultStateKey;
            stateMachine.Initialize(MyCore, _cachedAnimator);
            stateMachine.defaultStateKey = defaultStateKey;
            stateMachine.StartStateMachine();
            _stateMachineInitialized = true;
        }
    }
}
