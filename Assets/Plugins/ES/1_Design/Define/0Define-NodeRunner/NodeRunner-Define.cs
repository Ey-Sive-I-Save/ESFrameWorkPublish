using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    #region 定义运行者和相关枚举
    public interface INodeRunner : INodeRunner_Origin
    {
        public void Editor_SetPos(Vector2 vector);
        public Vector2 Editor_GetPos();
        public bool EnableDrawIMGUI { get; }
        public void DrawIMGUI();

        void Execute();

        public NodePort GetInputNode();
        public List<NodePort> GetOutputNodes();

        public IEnumerable<INodeRunner> GetFlowTo();//对于多端输出 index -> Runner  对于单端多输出 端->Runners
        public void RemoveFlow(INodeRunner runner, int index = 0);
        public void SetFlow(INodeRunner runner, int index = 0);
        public void ConfirmFlow(int count);
        public string GetTitle();
        public ESNodeState State { get; set; }
        public bool MutiLineOut { get; }
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
        None = 0,
        Test = 1,
        Test2 = 1 << 1,
        SKill = 1 << 2,
    }

    #region 定义Port生成

    public class NodePort
    {
        public bool IsMutiConnect = false;
        public string Name = "端";

    }


    #endregion

    public class ESNodeUtility
    {
        public static KeyGroup<NodeEnvironment, (string, string, Type)> UseNodes = new KeyGroup<NodeEnvironment, (string, string, Type)>();

        public static Dictionary<INodeRunner, object> CacheMapping = new Dictionary<INodeRunner, object>();
        public static Action<object, int, object> CacheMappingRefresh = (a, level, usedara) => { };
        public static void PrintElementHierarchy(VisualElement element, string indent = "*")
        {
            if (element == null) return;

            // 打印当前元素信息
            string elementInfo = $"{indent}└─ {element.GetType().Name}";
            if (!string.IsNullOrEmpty(element.name))
                elementInfo += $" [name: {element.name}]";
            if (!string.IsNullOrEmpty(element.ClassListToString()))
                elementInfo += $" [class: {element.ClassListToString()}]";

            Debug.Log(elementInfo);

            // 递归子元素
            foreach (VisualElement child in element.Children())
            {
                PrintElementHierarchy(child, indent + "  *");
            }
        }
    }
    public static class EXS
    {
        /// <summary>
        /// 递归打印 VisualElement 的层次结构
        /// </summary>


        /// <summary>
        /// 将类列表转换为字符串
        /// </summary>
        public static string ClassListToString(this VisualElement element)
        {
            return string.Join(" ", element.GetClasses());
        }


    }


}
