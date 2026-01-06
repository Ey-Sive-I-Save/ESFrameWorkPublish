using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngineInternal;

namespace ES
{
    //纳米状态，一定单层，一定单运行，不一定是RunTimeLogic
    public interface IState : IESOriginalModule
    {
        public bool IsRunning { get; set; }//因为OnEnable不意味着状态真的进入了捏

        #region 设计层面 准备-更新-退出 (忽略Enter和PowerOff，因为他们通常不会以接口名义调用)
        void OnStateEnter();
        void OnStateUpdate();
        void OnStateExit();
        #endregion

        #region 键
        void SetKey(string key);
        string GetKey();
        #endregion

        IState AsThis { get; set; }//自身状态--如果是状态机的话，这个有用的



        #region 自身状态--以Enter和Exit为界限
        public bool CheckThisStateCanUpdating { get; }//可以自定义是否要更新

        #endregion

        #region 固有
        IStateSharedData SharedData { get; set; } //共享数据
        IStateVariableData VariableData { get; set; } //变化数据
        #endregion

        [LabelText("标准状态")] public EnumStateRunningStatus RunningStatus { get; }//微型就有了
    }

    //状态机可被认为是一个标准状态,来允许无论任何情况下都能作为子状态- 
    //纳米状态机-只能同时有一个状态
    public interface IStateMachine : IState, IESOringinHosting
    {
        IState SelfRunningMainState { get; set; }
        IEnumerable<IState> SelfRunningStates { get; }
        public HashSet<IState> RootAllRunningStates { get; }//根部的运行
    }




    //状态自主生命周期--微型数据开始才有
    public enum EnumStateRunningStatus
    {
        [InspectorName("从未启动")] Never,
        [InspectorName("运行时")] StateUpdate,  //OnStateEnter=>触发
        [InspectorName("已退出")] StateExit //OnStateExit=>触发
    }
    //状态专属的共享数据--微型数据开始才有
    public interface IStateSharedData
    {
        int Order { get; }
        bool CanBeHit { get; }
        bool CanHit { get; }
        string[] BeHitWithoutCondition { get; }
        Enum Channel { get; }

    }
    //状态专属的变化数据--微型数据开始才有
    public interface IStateVariableData : IDeepClone<IStateVariableData>
    {

    }
}
