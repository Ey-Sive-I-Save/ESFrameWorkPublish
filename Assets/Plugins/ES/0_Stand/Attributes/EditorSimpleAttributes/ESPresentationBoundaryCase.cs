using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES 编辑器扩展边界案例。
    ///
    /// 用法：把该组件添加到多个 GameObject 后一起选中，分别验证：
    /// 1. 10 个以内的多目标多态编辑；
    /// 2. 超过 10 个目标时只读保护；
    /// 3. 数组、List、空槽和深层 SerializeReference 嵌套。
    /// 该脚本不参与运行时逻辑，只是稳定的 Inspector 回归入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESPresentationBoundaryCase : MonoBehaviour
    {
        [Title("ES 边界验证", "用于验证多目标、集合和深层嵌套，不代表业务数据结构。", TitleAlignments.Left, true, true)]
        [ESEditorBeginSection("multi", "多目标编辑", -100f, "选中 2～10 个实例后编辑；超过 10 个应进入只读保护。")]
        [SerializeReference]
        [LabelText("共享节点")]
        public BoundaryNode sharedNode = new ValueNode();

        [SerializeReference]
        [LabelText("混合节点")]
        public BoundaryNode mixedNode = new BranchNode();

        [ESEditorBeginSection("collection", "集合边界", 0f, "验证数组、List、空槽和不同元素类型。")]
        [SerializeReference]
        [ListDrawerSettings(DefaultExpandedState = true)]
        [LabelText("节点数组")]
        public BoundaryNode[] nodeArray =
        {
            new ValueNode(),
            null,
            new BranchNode(),
            new TextNode()
        };

        [SerializeReference]
        [ListDrawerSettings(DefaultExpandedState = true)]
        [LabelText("节点列表")]
        public List<BoundaryNode> nodeList = new List<BoundaryNode>
        {
            new TextNode(),
            null,
            new ValueNode(),
            new BranchNode()
        };

        [ESEditorBeginSection("nested", "深层嵌套", 100f, "默认构建 8 层，验证缩进、边框、标题和选择器不会错位。")]
        [SerializeReference]
        [LabelText("8 层嵌套根节点")]
        public BoundaryNode deepRoot = BuildChain(8);

        [ESEditorSection]
        [LabelText("当前测试说明")]
        [MultiLineProperty(3)]
        public string testNote =
            "建议：先复制 2 个实例测试批量修改，再复制到 11 个实例确认多态编辑被限制。";

        private static BoundaryNode BuildChain(int depth)
        {
            BoundaryNode current = new ValueNode();
            for (int i = 1; i < depth; i++)
                current = new BranchNode { child = current };

            return current;
        }

        [Serializable]
        public abstract class BoundaryNode
        {
            [LabelText("节点名称")]
            public string name = "Boundary Node";

            [SerializeReference]
            [LabelText("子节点")]
            public BoundaryNode child;
        }

        [Serializable]
        [TypeRegistryItem("基础/数值节点")]
        public sealed class ValueNode : BoundaryNode
        {
            [LabelText("数值")]
            public float value = 1f;
        }

        [Serializable]
        [TypeRegistryItem("基础/文字节点")]
        public sealed class TextNode : BoundaryNode
        {
            [LabelText("文字")]
            public string text = "Boundary";
        }

        [Serializable]
        [TypeRegistryItem("组合/分支节点")]
        public sealed class BranchNode : BoundaryNode
        {
            [SerializeReference]
            [LabelText("左分支")]
            public BoundaryNode left;

            [SerializeReference]
            [LabelText("右分支")]
            public BoundaryNode right;
        }
    }
}
