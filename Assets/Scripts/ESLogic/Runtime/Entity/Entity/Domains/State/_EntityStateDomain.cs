

using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    [Serializable, TypeRegistryItem("State Domain")]
    public partial class EntityStateDomain : Domain<Entity, EntityStateModuleBase>, IPreviewElement, IPreviewAreaModeProvider, IPreviewElementLifecycle, IPreviewElementEditorUpdate
    {
        private static readonly StateSupportFlags[] PreflightSupportFlags =
        {
            StateSupportFlags.Grounded,
            StateSupportFlags.Crouched,
            StateSupportFlags.Prone,
            StateSupportFlags.Swimming,
            StateSupportFlags.Flying,
            StateSupportFlags.Mounted,
            StateSupportFlags.Climbing,
            StateSupportFlags.SpecialInteraction,
            StateSupportFlags.Observer,
            StateSupportFlags.Dead,
            StateSupportFlags.Transition,
        };

        private sealed class PreflightFaultState : StateBase
        {
            protected override void OnStateEnterLogic()
            {
                throw new InvalidOperationException("PreflightFaultState.OnStateEnterLogic");
            }
        }

        [Title("State Data")]
        [LabelText("Animation State Data Pack")]
        public StateAniDataPack stateAniDataPack;

        [LabelText("Gun State Data Pack")]
        public GunStateAniDataPack gunStateAniDataPack;

        [LabelText("Additional State Data Packs")]
        public List<StateAniDataPack> additionalStateAniDataPacks = new List<StateAniDataPack>();

        [Title("State Machine")]
        [LabelText("State Machine")]
        public StateMachine stateMachine = new StateMachine();

        [LabelText("Default State Key (Optional)")]
        public string defaultStateKey = "";

        [LabelText("Initial Active State Name (Optional)")]
        [Tooltip("State activated automatically after state machine startup. Empty means no auto activation.")]
        public string initialStateName = "";

        [Title("Skill Runtime Test")]
        [Button("确保技能轨道运行时测试模块存在"), PropertyOrder(-10)]
        public void EnsureSkillRuntimeTestModuleExists()
        {
            if (FindMyModule<EntityStateSkillRuntimeTestModule>() != null)
                return;

            MyModules.Add(new EntityStateSkillRuntimeTestModule());
            MyModules.ApplyBuffers(true);
        }

        [NonSerialized] private bool _stateMachineInitialized;
        [NonSerialized] private Animator _cachedAnimator;
        [NonSerialized] private bool _warnedMissingCoreForStateMachineInit;
        [NonSerialized] private bool _warnedMissingAnimatorForStateMachineInit;

        [NonSerialized] private bool _packDirty = true;
        [NonSerialized] private List<StateAniDataInfo> _cachedInfos = new List<StateAniDataInfo>(64);
        [NonSerialized] private List<StateAniDataPack> _cachedPackSources = new List<StateAniDataPack>(4);
        [NonSerialized] private List<StateAniDataPack> _workingPackSources = new List<StateAniDataPack>(4);

        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
            // 蹇呴』鍏堝垵濮嬪寲StateMachine锛堝垱寤哄眰绾х瓑鍩虹璁炬柦锛夛紝鍐嶅垵濮嬪寲StateAniDataPack锛堟敞鍐岀姸鎬侊級
            InitializeStateMachine();
            InitializeStateAniDataPack();
            StartStateMachineAfterDataLoaded();
        }

        protected override void Update()
        {
            base.Update();

            if (_stateMachineInitialized)
            {
                stateMachine.UpdateStateMachine();
            }
        }

        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            DisposePreviewRender();
#endif
            if (_stateMachineInitialized)
            {
                stateMachine.StopStateMachine();
                stateMachine.Dispose();
                _stateMachineInitialized = false;
            }
            base.OnDestroy();
        }

    }
}
