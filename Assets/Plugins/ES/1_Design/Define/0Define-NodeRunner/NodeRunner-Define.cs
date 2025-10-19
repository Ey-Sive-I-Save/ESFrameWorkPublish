using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    #region 定义运行者和相关枚举
    public interface INodeRunner
    {
        public void Editor_SetPos(Vector2 vector);
        public Vector2 Editor_GetPos();

        void Execute();
        void OnEnter();
        void OnRunning();
        void OnExit();

        public NodePort GetInputNode();
        public List<NodePort> GetOutputNodes();

        public IEnumerable<INodeRunner> GetFlowTo();//对于多端输出 index -> Runner  对于单端多输出 端->Runners
        public void RemoveFlow(INodeRunner runner,int index=0);
        public void SetFlow(INodeRunner runner, int index=0);
        public string GetTitle();


    }
    public enum ESNodeState
    {
        [LabelText("从未执行")]
        None,
        [LabelText("正在执行")]
        Running,
        [LabelText("正在退出")]
        Exit,
    }
    #endregion
    [Flags]
    public enum NodeEnvironment
    {
        None=0,
        Test=1,
        Test2=1<<1,
    }

    #region 定义Port生成

    public class NodePort
    {
        public bool IsMutiConnect = false;
        public string Name="端"; 
        
    }


    #endregion

    public class ESNodeUtility
    {
        public static KeyGroup<NodeEnvironment, (string,string, Type)> UseNodes = new KeyGroup<NodeEnvironment, (string,string, Type)>(); 
    }

}
