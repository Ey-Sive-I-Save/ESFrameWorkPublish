using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Verifies two independent ESEditorSection directories on one host object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESEditorSectionDualNavigatorCase : MonoBehaviour
    {
        [Title("双配置目录案例", "同一个宿主中，作者配置与运行时配置各自拥有独立目录。", TitleAlignments.Left, true, true)]
        [LabelText("目录外说明")]
        public string outsideNote = "这个字段没有 ESEditorSection，应始终保持在两个目录之外。";

        [ESEditorSection("authoring", "identity", "身份", -100f, "作者需要先确认的业务身份与入口。")]
        [LabelText("配置名称")]
        public string configurationName = "角色配置";

        [ESEditorSection("authoring", "presentation", "表现", 10f, "美术与设计阶段调整的表现参数。")]
        [LabelText("显示颜色")]
        public Color displayColor = Color.white;

        [ESEditorSection("runtime", "execution", "执行策略", -100f, "运行时行为的调度与执行约束。")]
        [LabelText("启用执行")]
        public bool enableExecution = true;

        [ESEditorSection("runtime", "diagnostics", "运行诊断", 10f, "只读运行结果，不应压过执行配置。")]
        [ShowInInspector, ReadOnly, LabelText("运行状态")]
        private string RuntimeStatus => enableExecution ? "允许执行" : "已暂停";

        [FoldoutGroup("独立普通分组", true, 1000f)]
        [LabelText("普通开关")]
        public bool independentFoldout;
    }
}
