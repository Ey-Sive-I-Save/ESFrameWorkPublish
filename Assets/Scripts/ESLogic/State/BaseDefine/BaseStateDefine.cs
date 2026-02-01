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
    //写一个纳米的
    [Serializable, TypeRegistryItem("状态基类")]
    public abstract class StateBase 
    {
        #region  SV

        [NonSerialized]
        public BaseOriginalStateMachine host;

        public BaseOriginalStateMachine GetHost => host;

        [ShowInInspector, LabelText("标识键"), FoldoutGroup("只读属性")] public abstract string ThisKey { get; }
        [ShowInInspector, LabelText("标识键"), FoldoutGroup("只读属性"), ReadOnly] public StateSharedData SharedData { get => stateSharedData; set => stateSharedData = value; }
        [ShowInInspector, LabelText("标识键"), FoldoutGroup("只读属性"), ReadOnly] public StateVariableData VariableData { get => stateVariableData; set => stateVariableData = value; }

        [LabelText("共享数据", SdfIconType.Calendar2Date), FoldoutGroup("固有"), NonSerialized/*不让自动序列化*/] public StateSharedData stateSharedData = null;
        [LabelText("自变化数据", SdfIconType.Calendar3Range), FoldoutGroup("固有")] public StateVariableData stateVariableData;

        #endregion

        #region 状态生命周期
        public bool IsRunning { get; set; }

        public void OnStateEnter()
        {
            if (IsRunning) return;
            IsRunning = true;//直接进入准备

            // host._SelfRunningState = this;
            RunStatePreparedLogic();
        }

        public void OnStateUpdate()
        {
            RunStateUpdateLogic();
        }

        public void OnStateExit()
        {
            if (!IsRunning) return;
            IsRunning = false;//直接退出准备

            // if (host._SelfRunningState == this)
            // {
            //     host._SelfRunningState = null;
            // }
            RunStateExitLogic();
        }
        #endregion

        #region 键

        public StateBase AsThis { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool CheckThisStateCanUpdating => throw new NotImplementedException();

        public EnumStateRunningStatus RunningStatus => throw new NotImplementedException();

        public void SetKey(string _key)
        {
            this.key = _key;
        }

        public string GetKey()
        {
            return key;
        }

        public string key;
        #endregion

        #region 应用层重写逻辑


        protected virtual void RunStatePreparedLogic()
        {
            //默认的
        }
        protected virtual void RunStateExitLogic()
        {

        }

        protected virtual void RunStateUpdateLogic()
        {

        }
        #endregion

    }
}





