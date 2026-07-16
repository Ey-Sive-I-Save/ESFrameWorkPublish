using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 通用场景/Prefab ReadMe 标注组件。
    /// 只负责在 Inspector 中保存说明文字，不参与运行时逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/Stand/ReadMe Note")]
    public sealed class ESReadMeNote : MonoBehaviour
    {
        [Title("ReadMe")]
        [LabelText("标题")]
        public string title = "ReadMe";

        [LabelText("一句话说明")]
        [TextArea(2, 4)]
        public string summary;

        [LabelText("详细说明")]
        [TextArea(6, 16)]
        public string readMe;

        [LabelText("必须保留")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<string> requiredItems = new List<string>();

        [LabelText("注意事项")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<string> notes = new List<string>();

        [LabelText("负责人/系统")]
        public string ownerSystem;

        [LabelText("最后更新")]
        public string lastUpdated;

        public bool HasContent =>
            !string.IsNullOrWhiteSpace(title) ||
            !string.IsNullOrWhiteSpace(summary) ||
            !string.IsNullOrWhiteSpace(readMe) ||
            (requiredItems != null && requiredItems.Count > 0) ||
            (notes != null && notes.Count > 0);

        [Button("填充玩家层级模板说明", ButtonSizes.Medium)]
        private void FillPlayerHierarchyTemplateReadMe()
        {
            title = "玩家角色工业级层级模板";
            summary = "用于说明玩家角色 Prefab 的推荐层级：逻辑、模型、IK/挂点、装备、特效、音效、相机参考点分离。";
            readMe =
                "这个组件只是一块 ReadMe 说明牌，不参与运行时逻辑。\n\n" +
                "玩家角色应有一个总根，场景中移动、生成、销毁的单位是这个总根。\n" +
                "运行时逻辑与碰撞负责 Entity、KCC、Capsule、StateMachine、InputBridge 等控制能力。\n" +
                "模型表现只放模型、骨骼和网格，尽量不要把业务脚本直接塞进模型骨骼层级。\n" +
                "动画辅助负责 IK 目标和挂点，例如 AimTarget、LookAtTarget、左右手挂点、背部挂点。\n" +
                "装备负责当前武器和备用槽位，武器系统再决定挂到哪个挂点。\n" +
                "相机参考点不是 Main Camera 本体。Main Camera / Cinemachine 建议放在场景相机系统里，只引用玩家身上的 Follow/Aim 点。";

            requiredItems.Clear();
            requiredItems.Add("总根：代表完整角色实例，是生成、销毁、传送、整体移动的对象。");
            requiredItems.Add("运行时逻辑与碰撞：放 Entity、KCC、Capsule、StateMachine、InputBridge。");
            requiredItems.Add("模型表现：放模型实例、骨骼根、网格，不承担业务逻辑。");
            requiredItems.Add("动画辅助：放 IK 目标和挂点，供动画、武器、技能、MatchTarget 使用。");
            requiredItems.Add("相机参考点：只放 Follow/Aim 等目标点，不放 Main Camera 本体。");

            notes.Clear();
            notes.Add("不要把 Main Camera 直接放进玩家 Prefab，玩家只提供相机参考点。");
            notes.Add("不要直接把业务逻辑挂在导入模型的骨骼根上，换模型时会很痛。");
            notes.Add("KCC 角色如果需要 Rigidbody，通常应使用 Kinematic，避免物理系统自动推动主角。");
            notes.Add("武器、IK、VFX、Audio 尽量走专用分组，避免散落在骨骼和场景根下。");

            ownerSystem = "ES Stand / Character Prefab Guide";
            lastUpdated = "2026-07-16";
        }
    }
}
