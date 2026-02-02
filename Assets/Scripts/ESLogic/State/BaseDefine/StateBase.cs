using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{

    [Serializable, TypeRegistryItem("状态基类")]
    public abstract class StateBase
    {
        #region  基础属性

        [NonSerialized]
        public StateMachine host;

        // 建议：添加初始化方法
        public virtual void Initialize(StateMachine machine)
        {
            host = machine;
        }

        [LabelText("共享数据", SdfIconType.Calendar2Date), FoldoutGroup("基础属性"), NonSerialized/*不让自动序列化*/] public StateSharedData stateSharedData = null;
        [LabelText("自变化数据", SdfIconType.Calendar3Range), FoldoutGroup("基础属性")] public StateVariableData stateVariableData;



        #endregion

        #region 键


        public string strKey;
        #endregion

        #region 状态生命周期
        public StateBaseStatus baseStatus = StateBaseStatus.Never;
        public StateRuntimePhase stateRuntimePhase = StateRuntimePhase.Running;
        public void OnStateEnter()
        {
            if (baseStatus == StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Running;
            stateRuntimePhase = StateRuntimePhase.Running;
            OnStateEnterLogic();
        }

        public void OnStateUpdate()
        {
            OnStateUpdateLogic();
        }

        public void OnStateExit()
        {
            if (baseStatus != StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Never;
            //这里需要编写释放逻辑
            OnStateExitLogic();
        }
        #endregion



        #region 应用层用户自己重写逻辑
        protected virtual void OnStateEnterLogic()
        {
            //默认的进入执行逻辑
        }
        protected virtual void OnStateUpdateLogic()
        {
            //默认的更新执行逻辑
        }
        protected virtual void OnStateExitLogic()
        {
            //默认的退出执行逻辑
        }


        #endregion

    }
}





