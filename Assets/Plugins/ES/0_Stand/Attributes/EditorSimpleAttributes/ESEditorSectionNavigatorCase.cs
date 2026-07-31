using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ESEditorSection 的最小可运行案例。
    ///
    /// 使用方式：
    /// 1. 将本组件挂到任意 GameObject。
    /// 2. 在 Inspector 中依次选择“核心配置”“身体能力”“控制来源”等目录。
    /// 3. 观察目录外字段、简写分区、旧完整写法和独立 Foldout 是否互不干扰。
    ///
    /// 这个类只演示编辑器排版，不代表真实业务模型；所有字段都可以按同样方式替换成项目自己的数据。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESEditorSectionNavigatorCase : MonoBehaviour
    {
        // 没有 ESEditorSection 的成员不会进入内容目录，仍按普通 Odin Inspector 内容绘制。
        [PropertyOrder(950f)]
        [FoldoutGroup("目录外验证", false, 950f)]
        [Title("配置目录案例", "验证分区导航、目录外内容和普通 Odin 分组可以共存。", TitleAlignments.Left, true, true)]
        [LabelText("目录外说明")]
        public string ungroupedNote = "我没有 ESEditorSection，因此不会出现在任何配置目录中。";

        // ---------------------------------------------------------------------
        // 1. Begin / Continue / End：最推荐的连续配置写法
        // ---------------------------------------------------------------------

        // Begin 同时完成三件事：开启分区、声明分区名称、声明排序和副标题。
        // 之后的同一段成员不再重复 sectionId、显示名和 order。
        [ESEditorBeginSection("核心配置", -100f, "角色身份、动画入口与生成策略。")]
        [LabelText("角色 ID")]
        [Tooltip("稳定的业务标识；这里只用于展示，不参与运行时查表。")]
        public string characterId = "warrior_001";

        // 无参数 ESEditorSection 表示“继续最近仍活跃的分区”。
        [ESEditorSection]
        [LabelText("主 Animator")]
        public Animator animator;

        // 默认 EndMode 是 AfterMember：当前字段仍属于核心配置，绘制完它之后才关闭分区。
        [ESEditorEndSection]
        [LabelText("使用默认出生策略")]
        public bool useDefaultSpawnProfile = true;

        // End 之后没有分区属性，因此这里再次回到目录外。
        [PropertyOrder(951f)]
        [LabelText("目录外备注")]
        public string ungroupedAfterCore = "核心配置已结束；我不属于任何配置目录。";

        // ---------------------------------------------------------------------
        // 2. BeforeMember：结束分区，但让当前字段留在目录外
        // ---------------------------------------------------------------------

        [ESEditorBeginSection("body", "身体能力", 10f, "角色碰撞尺寸与基础移动参数。")]
        [LabelText("胶囊半径")]
        [MinValue(0.05f)]
        public float capsuleRadius = 0.35f;

        [ESEditorSection]
        [LabelText("胶囊高度")]
        [MinValue(0.1f)]
        public float capsuleHeight = 1.8f;

        // BeforeMember 会先关闭“身体能力”，所以移动速度故意作为目录外字段绘制。
        // 这只是语法演示；真实项目若希望它属于身体能力，应改成 [ESEditorSection]。
        [PropertyOrder(952f)]
        [ESEditorEndSection(ESEditorSectionEndMode.BeforeMember)]
        [LabelText("移动速度（目录外示例）")]
        public float moveSpeed = 4.5f;

        // ---------------------------------------------------------------------
        // 3. 旧完整写法：与简写完全兼容，可在同一个类型中混用
        // ---------------------------------------------------------------------

        // 完整写法适合非连续字段、跨区域字段，或需要长期固定 sectionId 的配置。
        [ESEditorSection("ai", "控制来源", 20f, "定义玩家、AI 或脚本对角色的控制归属。")]
        [LabelText("控制方式")]
        public ControlMode controlMode = ControlMode.Player;

        // 这里再次使用完整写法，表示两个字段明确属于同一业务分区。
        [ESEditorSection("ai", "控制来源", 20f)]
        [LabelText("允许自动攻击")]
        public bool allowAutoAttack = true;

        // 复杂集合继续交给 Odin 的 TableList；目录只负责决定它属于哪一类信息。
        [ESEditorSection("state", "状态表现", 30f, "状态名、Animator 状态与过渡时长。")]
        [LabelText("状态规则")]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = true)]
        public List<StateRule> stateRules = new List<StateRule>
        {
            new StateRule { stateName = "待机", animatorState = "Idle", transitionSeconds = 0.15f },
            new StateRule { stateName = "移动", animatorState = "Locomotion", transitionSeconds = 0.10f },
        };

        // ---------------------------------------------------------------------
        // 4. 资源引用：展示“字段 + 只读检查”在同一分区内的组合
        // ---------------------------------------------------------------------

        [ESEditorBeginSection("resources", "资源引用", 40f, "预览与运行时所需的外部资源。")]
        [LabelText("角色预制件")]
        public GameObject characterPrefab;

        // 继续沿用上面的“资源引用”，但不重复 sectionId、名称和排序值。
        [ESEditorSection]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("资源检查")]
        [InfoBox("无法应用预览：角色预制件为空。请在当前“资源引用”分区填写角色预制件。", InfoMessageType.Error, nameof(HasMissingCharacterPrefab))]
        private string ResourceCheck => HasMissingCharacterPrefab ? "缺少角色预制件" : "资源引用完整";

        // ---------------------------------------------------------------------
        // 5. End + 条件字段：结束标记不会破坏 Odin 原有 ShowIf 行为
        // ---------------------------------------------------------------------

        [ESEditorBeginSection("advanced", "扩展规则", 90f, "低频覆盖项，默认保持基础行为。")]
        [LabelText("覆盖转向速度")]
        public bool overrideTurnSpeed;

        // 当前字段是扩展规则的最后一项；字段是否显示仍由 Odin 的 ShowIf 决定。
        [ESEditorEndSection]
        [LabelText("转向速度")]
        [ShowIf(nameof(overrideTurnSpeed))]
        [MinValue(0f)]
        public float turnSpeed = 540f;

        // ---------------------------------------------------------------------
        // 6. 诊断分区：只读结果 + 一个明确触发的主操作
        // ---------------------------------------------------------------------

        [ESEditorSection("diagnostics", "诊断", 200f, "查看结果并手动触发预览；打开 Inspector 不会自动写入。")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("预览结果")]
        private string PreviewResult => previewResult;

        [ESEditorSection("diagnostics", "诊断", 200f)]
        [Button("应用预览")]
        [DisableIf(nameof(HasMissingCharacterPrefab))]
        private void ApplyPreview()
        {
            // 这是案例唯一的写入动作，必须由用户点击按钮明确触发。
            previewResult = "已应用：" + characterPrefab.name + " / " + characterId;
        }

        private bool HasMissingCharacterPrefab => characterPrefab == null;
        private string previewResult = "尚未应用预览";

        // ---------------------------------------------------------------------
        // 7. 普通 Odin 分组：验证 ESEditorSection 不会接管其他分组
        // ---------------------------------------------------------------------

        // 这个 Foldout 不是配置目录的一部分，始终作为独立 Odin 分组绘制。
        [PropertyOrder(1000f)]
        [FoldoutGroup("独立验证分组", false, 1000f)]
        [LabelText("独立开关")]
        public bool useIndependentGroup;

        [PropertyOrder(1001f)]
        [FoldoutGroup("独立验证分组", false, 1000f)]
        [LabelText("独立数值")]
        [ShowIf(nameof(useIndependentGroup))]
        [Range(0f, 1f)]
        public float independentValue = 0.5f;

        [Serializable]
        public sealed class StateRule
        {
            [TableColumnWidth(90, Resizable = false)]
            [LabelText("业务状态")]
            public string stateName;

            [LabelText("Animator 状态")]
            public string animatorState;

            [TableColumnWidth(100, Resizable = false)]
            [LabelText("过渡秒数")]
            public float transitionSeconds;
        }

        public enum ControlMode
        {
            Player = 0,
            AI = 1,
            Scripted = 2,
        }
    }
}
