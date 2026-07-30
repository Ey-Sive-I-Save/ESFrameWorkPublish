using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 可直接挂到任意 GameObject 的配置目录案例。
    /// 用 Add Component 搜索 ESEditorSectionNavigatorCase，即可验证普通 Odin Inspector 的目录绘制。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESEditorSectionNavigatorCase : MonoBehaviour
    {
        [PropertyOrder(-200f)]
        [Title("配置目录案例", "验证未分区内容、配置目录与独立 Odin 分组可以在同一个 Inspector 中共存。", TitleAlignments.Left, true, true)]
        [LabelText("无分组说明")]
        public string ungroupedNote = "此字段没有 ESEditorSection，应始终保持为目录外的独立内容。";

        [ESEditorSection("core", "核心配置", -100f, "角色身份与动画入口。后续字段只需沿用同一个分区 ID。")]
        [LabelText("角色 ID"), Tooltip("稳定的业务标识。示例只展示编辑器排版，不参与运行时查表。")]
        public string characterId = "warrior_001";

        [ESEditorSection("core", "核心配置", -100f)]
        [LabelText("主 Animator")]
        public Animator animator;

        [ESEditorSection("body", "身体能力", 10f, "角色碰撞尺寸与基础移动参数。")]
        [LabelText("胶囊半径"), MinValue(0.05f)]
        public float capsuleRadius = 0.35f;

        [ESEditorSection("body", "身体能力", 10f)]
        [LabelText("胶囊高度"), MinValue(0.1f)]
        public float capsuleHeight = 1.8f;

        [ESEditorSection("body", "身体能力", 10f)]
        [LabelText("移动速度")]
        public float moveSpeed = 4.5f;

        [ESEditorSection("ai", "控制来源", 20f, "定义玩家、AI 或脚本对角色的控制归属。")]
        [LabelText("控制方式")]
        public ControlMode controlMode = ControlMode.Player;

        [ESEditorSection("ai", "控制来源", 20f)]
        [LabelText("允许自动攻击")]
        public bool allowAutoAttack = true;

        [ESEditorSection("state", "状态表现", 30f, "状态名、Animator 状态与过渡时长。")]
        [LabelText("状态规则")]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = true)]
        public List<StateRule> stateRules = new List<StateRule>
        {
            new StateRule { stateName = "待机", animatorState = "Idle", transitionSeconds = 0.15f },
            new StateRule { stateName = "移动", animatorState = "Locomotion", transitionSeconds = 0.1f },
        };

        [ESEditorSection("resources", "资源引用", 40f, "预览与运行时所需的外部资源。")]
        [LabelText("角色预制件")]
        public GameObject characterPrefab;

        [ESEditorSection("resources", "资源引用", 40f)]
        [ShowInInspector, ReadOnly, LabelText("资源检查")]
        [InfoBox("无法应用预览：角色预制件为空。请在当前“资源引用”分区填写角色预制件。", InfoMessageType.Error, nameof(HasMissingCharacterPrefab))]
        private string ResourceCheck => HasMissingCharacterPrefab ? "缺少角色预制件" : "资源引用完整";

        [ESEditorSection("advanced", "扩展规则", 90f, "低频覆盖项，默认保持基础行为。")]
        [LabelText("覆盖转向速度")]
        public bool overrideTurnSpeed;

        [ESEditorSection("advanced", "扩展规则", 90f)]
        [LabelText("转向速度"), ShowIf(nameof(overrideTurnSpeed)), MinValue(0f)]
        public float turnSpeed = 540f;

        [ESEditorSection("diagnostics", "诊断", 200f, "只读结果与需要明确触发的预览操作。")]
        [ShowInInspector, ReadOnly, LabelText("预览结果")]
        private string PreviewResult => previewResult;

        [ESEditorSection("diagnostics", "诊断", 200f)]
        [Button("应用预览")]
        [DisableIf(nameof(HasMissingCharacterPrefab))]
        private void ApplyPreview()
        {
            previewResult = "已应用：" + characterPrefab.name + " / " + characterId;
        }

        private bool HasMissingCharacterPrefab => characterPrefab == null;
        private string previewResult = "尚未应用预览";

        [PropertyOrder(1000f)]
        [FoldoutGroup("独立验证分组", true, 1000f)]
        [LabelText("独立开关")]
        public bool useIndependentGroup;

        [PropertyOrder(1001f)]
        [FoldoutGroup("独立验证分组", true, 1000f)]
        [LabelText("独立数值"), ShowIf(nameof(useIndependentGroup)), Range(0f, 1f)]
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
