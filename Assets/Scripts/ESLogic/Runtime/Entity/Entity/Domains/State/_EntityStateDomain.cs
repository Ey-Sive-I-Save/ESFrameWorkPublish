

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
    [Serializable, TypeRegistryItem("状态表现域")]
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

        [TitleGroup("状态表现域/状态数据", "状态数据", Alignment = TitleAlignments.Left)]
        [LabelText("动画状态数据包")]
        public StateAniDataPack stateAniDataPack;

        [TitleGroup("状态表现域/状态数据")]
        [LabelText("枪械状态数据包")]
        public GunStateAniDataPack gunStateAniDataPack;

        [TitleGroup("状态表现域/状态数据")]
        [LabelText("附加状态数据包")]
        public List<StateAniDataPack> additionalStateAniDataPacks = new List<StateAniDataPack>();

        [TitleGroup("状态表现域/状态机", "状态机", Alignment = TitleAlignments.Left)]
        [LabelText("状态机")]
        [InlineProperty]
        public StateMachine stateMachine = new StateMachine();

        [TitleGroup("状态表现域/启动配置", "启动配置", Alignment = TitleAlignments.Left)]
        [LabelText("默认状态Key（可选）")]
        public string defaultStateKey = "";

        [TitleGroup("状态表现域/启动配置")]
        [LabelText("启动后激活状态名（可选）")]
        [Tooltip("状态机启动后自动激活的状态名。留空表示不自动激活。")]
        public string initialStateName = "";

        [TitleGroup("状态表现域/技能运行时测试", "技能运行时测试", Alignment = TitleAlignments.Left)]
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
