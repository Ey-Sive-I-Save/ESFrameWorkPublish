using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Project-shared visual configuration for ES editor surfaces.
    /// The asset is editor-owned data and is excluded from runtime/AB collection by
    /// <see cref="ESOnlyEditorSOAttribute"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ESGlobalEditorTheme",
        menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "ES 编辑器主题")]
    [ESOnlyEditorSO("ES 编辑器主题只服务编辑器绘制，不应进入运行时构建或 AB 资源包。")]
    public sealed class ESGlobalEditorTheme : ESEditorGlobalSo<ESGlobalEditorTheme>
    {
        public const string DefaultPresetId = "ES.Default";

        [Title("ES 编辑器主题", "项目共享的 ES 界面视觉配置。个人窗口状态仍保存在 EditorPrefs。", TitleAlignments.Left, true, true)]
        [LabelText("主题预设")]
        public string presetId = DefaultPresetId;

        [Range(0.85f, 1.20f)]
        [LabelText("界面密度")]
        public float density = 1f;

        [LabelText("显示分区副标题")]
        public bool showSectionSubtitle = true;

        [LabelText("启用自定义色板")]
        public bool useCustomPalette = true;

        [Title("深色皮肤色板")]
        [ColorUsage(false)]
        [LabelText("层级强调起始色")]
        public Color darkAccentStart = new Color(0.48f, 0.78f, 1f, 0.92f);

        [ColorUsage(false)]
        [LabelText("层级强调结束色")]
        public Color darkAccentEnd = new Color(0.13f, 0.42f, 0.72f, 0.96f);

        [ColorUsage(false)]
        [LabelText("警告色")]
        public Color darkWarning = new Color(0.90f, 0.68f, 0.24f, 0.96f);

        [ColorUsage(false)]
        [LabelText("错误色")]
        public Color darkError = new Color(0.92f, 0.40f, 0.24f, 0.96f);

        [Title("浅色皮肤色板")]
        [ColorUsage(false)]
        [LabelText("层级强调起始色")]
        public Color lightAccentStart = new Color(0.12f, 0.46f, 0.82f, 0.92f);

        [ColorUsage(false)]
        [LabelText("层级强调结束色")]
        public Color lightAccentEnd = new Color(0.04f, 0.24f, 0.56f, 0.96f);

        [ColorUsage(false)]
        [LabelText("警告色")]
        public Color lightWarning = new Color(0.72f, 0.29f, 0.05f, 0.96f);

        [ColorUsage(false)]
        [LabelText("错误色")]
        public Color lightError = new Color(0.78f, 0.24f, 0.10f, 0.96f);

        [Button("恢复 ES 默认主题", ButtonSizes.Medium)]
        [PropertyOrder(100)]
        public void RestoreDefault()
        {
            presetId = DefaultPresetId;
            density = 1f;
            showSectionSubtitle = true;
            useCustomPalette = true;

            darkAccentStart = new Color(0.48f, 0.78f, 1f, 0.92f);
            darkAccentEnd = new Color(0.13f, 0.42f, 0.72f, 0.96f);
            darkWarning = new Color(0.90f, 0.68f, 0.24f, 0.96f);
            darkError = new Color(0.92f, 0.40f, 0.24f, 0.96f);

            lightAccentStart = new Color(0.12f, 0.46f, 0.82f, 0.92f);
            lightAccentEnd = new Color(0.04f, 0.24f, 0.56f, 0.96f);
            lightWarning = new Color(0.72f, 0.29f, 0.05f, 0.96f);
            lightError = new Color(0.78f, 0.24f, 0.10f, 0.96f);
        }
    }
}
