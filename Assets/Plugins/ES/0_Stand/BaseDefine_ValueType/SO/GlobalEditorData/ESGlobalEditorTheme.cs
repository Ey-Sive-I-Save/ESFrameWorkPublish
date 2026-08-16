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

        [Title("全局 Editor 外观")]
        [LabelText("启用 ES 全局外观")]
        [Tooltip("为 Unity 公开 Editor 回调和 ES 窗口启用统一视觉；进入 PlayMode 时会自动停用，返回编辑模式后恢复。")]
        public bool enableGlobalEditorShell = true;

        [LabelText("启用 Unity 全局深度皮肤（实验）")]
        [Tooltip("为安全内容容器应用 ES 纯色表面，并染色已有控件背景；不填充窗口根节点，不遮挡原生内容。仅支持 Unity 2022.3，可随时恢复，进入 PlayMode 时自动停用。")]
        public bool enableDeepEditorSkin = false;

        [Title("ES 品牌字体")]
        [LabelText("启用品牌标题字体")]
        [Tooltip("使用随包分发的中文圆体，仅作用于 ES 自有窗口的标题、数字和徽章。正文、输入框、日志、代码与路径继续使用 Unity 默认字体。")]
        public bool enableBrandTypography = true;

        [Title("交互反馈与动效")]
        [LabelText("启用编辑器动效")]
        [Tooltip("控制选中呼吸、状态闪光和轻量扫光。关闭后所有界面仍保持完整信息和颜色语义。")]
        public bool enableMotion = true;

        [Range(0f, 1f)]
        [LabelText("动效强度")]
        [Tooltip("建议保持 0.65～0.85。数值越低越克制，不影响编辑数据。")]
        public float motionIntensity = 0.78f;

        [Title("深色皮肤色板")]
        [ColorUsage(false)]
        [LabelText("层级强调起始色")]
        public Color darkAccentStart = new Color(0.36f, 0.62f, 0.78f, 0.90f);

        [ColorUsage(false)]
        [LabelText("层级强调结束色")]
        public Color darkAccentEnd = new Color(0.13f, 0.42f, 0.72f, 0.96f);

        [ColorUsage(false)]
        [LabelText("警告色")]
        public Color darkWarning = new Color(0.68f, 0.48f, 0.24f, 0.92f);

        [ColorUsage(false)]
        [LabelText("错误色")]
        public Color darkError = new Color(0.92f, 0.40f, 0.24f, 0.96f);

        [Title("浅色皮肤色板")]
        [ColorUsage(false)]
        [LabelText("层级强调起始色")]
        public Color lightAccentStart = new Color(0.16f, 0.40f, 0.62f, 0.90f);

        [ColorUsage(false)]
        [LabelText("层级强调结束色")]
        public Color lightAccentEnd = new Color(0.04f, 0.24f, 0.56f, 0.96f);

        [ColorUsage(false)]
        [LabelText("警告色")]
        public Color lightWarning = new Color(0.58f, 0.33f, 0.10f, 0.92f);

        [ColorUsage(false)]
        [LabelText("错误色")]
        public Color lightError = new Color(0.78f, 0.24f, 0.10f, 0.96f);

        [Title("地图编辑器语义色")]
        [ColorUsage(false)]
        [LabelText("深色地形底色")]
        public Color darkMapTerrainBase = new Color(0.07f, 0.11f, 0.16f, 1f);
        [ColorUsage(false)]
        [LabelText("深色地图网格")]
        public Color darkMapGrid = new Color(0.42f, 0.52f, 0.64f, 0.22f);
        [ColorUsage(false)]
        [LabelText("深色区域色")]
        public Color darkMapRegion = new Color(0.18f, 0.48f, 0.86f, 0.22f);
        [ColorUsage(false)]
        [LabelText("深色 POI 色")]
        public Color darkMapPoi = new Color(0.96f, 0.70f, 0.24f, 0.96f);
        [ColorUsage(false)]
        [LabelText("深色选中点")]
        public Color darkMapSelection = new Color(0.32f, 0.88f, 0.68f, 0.96f);
        [ColorUsage(false)]
        [LabelText("深色高度低值")]
        public Color darkMapHeightLow = new Color(0.06f, 0.22f, 0.18f, 1f);
        [ColorUsage(false)]
        [LabelText("深色高度高值")]
        public Color darkMapHeightHigh = new Color(0.76f, 0.62f, 0.28f, 1f);

        [ColorUsage(false)]
        [LabelText("浅色地形底色")]
        public Color lightMapTerrainBase = new Color(0.92f, 0.94f, 0.97f, 1f);
        [ColorUsage(false)]
        [LabelText("浅色地图网格")]
        public Color lightMapGrid = new Color(0.22f, 0.34f, 0.48f, 0.24f);
        [ColorUsage(false)]
        [LabelText("浅色区域色")]
        public Color lightMapRegion = new Color(0.18f, 0.46f, 0.82f, 0.18f);
        [ColorUsage(false)]
        [LabelText("浅色 POI 色")]
        public Color lightMapPoi = new Color(0.78f, 0.40f, 0.04f, 1f);
        [ColorUsage(false)]
        [LabelText("浅色选中点")]
        public Color lightMapSelection = new Color(0.04f, 0.54f, 0.42f, 1f);
        [ColorUsage(false)]
        [LabelText("浅色高度低值")]
        public Color lightMapHeightLow = new Color(0.42f, 0.74f, 0.60f, 1f);
        [ColorUsage(false)]
        [LabelText("浅色高度高值")]
        public Color lightMapHeightHigh = new Color(0.86f, 0.70f, 0.30f, 1f);

        [Button("恢复 ES 默认主题", ButtonSizes.Medium)]
        [PropertyOrder(100)]
        public void RestoreDefault()
        {
            presetId = DefaultPresetId;
            density = 1f;
            showSectionSubtitle = true;
            useCustomPalette = true;
            enableGlobalEditorShell = true;
            enableDeepEditorSkin = false;
            enableBrandTypography = true;
            enableMotion = true;
            motionIntensity = 0.78f;

            darkAccentStart = new Color(0.36f, 0.62f, 0.78f, 0.90f);
            darkAccentEnd = new Color(0.13f, 0.42f, 0.72f, 0.96f);
            darkWarning = new Color(0.68f, 0.48f, 0.24f, 0.92f);
            darkError = new Color(0.92f, 0.40f, 0.24f, 0.96f);

            lightAccentStart = new Color(0.16f, 0.40f, 0.62f, 0.90f);
            lightAccentEnd = new Color(0.04f, 0.24f, 0.56f, 0.96f);
            lightWarning = new Color(0.58f, 0.33f, 0.10f, 0.92f);
            lightError = new Color(0.78f, 0.24f, 0.10f, 0.96f);

            darkMapTerrainBase = new Color(0.07f, 0.11f, 0.16f, 1f);
            darkMapGrid = new Color(0.42f, 0.52f, 0.64f, 0.22f);
            darkMapRegion = new Color(0.18f, 0.48f, 0.86f, 0.22f);
            darkMapPoi = new Color(0.96f, 0.70f, 0.24f, 0.96f);
            darkMapSelection = new Color(0.32f, 0.88f, 0.68f, 0.96f);
            darkMapHeightLow = new Color(0.06f, 0.22f, 0.18f, 1f);
            darkMapHeightHigh = new Color(0.76f, 0.62f, 0.28f, 1f);
            lightMapTerrainBase = new Color(0.92f, 0.94f, 0.97f, 1f);
            lightMapGrid = new Color(0.22f, 0.34f, 0.48f, 0.24f);
            lightMapRegion = new Color(0.18f, 0.46f, 0.82f, 0.18f);
            lightMapPoi = new Color(0.78f, 0.40f, 0.04f, 1f);
            lightMapSelection = new Color(0.04f, 0.54f, 0.42f, 1f);
            lightMapHeightLow = new Color(0.42f, 0.74f, 0.60f, 1f);
            lightMapHeightHigh = new Color(0.86f, 0.70f, 0.30f, 1f);
        }
    }
}
