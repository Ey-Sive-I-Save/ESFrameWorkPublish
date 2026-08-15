using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES Composite Shader 的逐属性 C# 代码帮助器。
    /// 使用 ESAdvancedDialogWindow 承载内容，不创建第二套对话框实现。
    /// </summary>
    internal static class ESCompositeCodingHelper
    {
        private const string DialogIdPrefix = "es.shader.composite.coding.";
        private static readonly GUIContent CodeButton = new GUIContent("C#", "打开此属性的完整 C# 示例");
        private static readonly GUIContent ResetButton = new GUIContent("R", "恢复 Shader 默认值");
        private static readonly Dictionary<string, GUIContent> LabelCache = new Dictionary<string, GUIContent>(StringComparer.Ordinal);

        internal static bool DrawProperty(MaterialEditor editor, MaterialProperty property, string displayName, bool showReset, bool resetEnabled, string tooltip = null)
        {
            if (editor == null || property == null)
                return false;

            float buttonHeight = EditorGUIUtility.singleLineHeight;
            bool resetRequested = false;
            EditorGUILayout.BeginHorizontal();
            bool previousMixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMixedValue;
                // UnityEditor.MaterialProperty 不提供 tooltip 属性；提示信息由调用方的
                // displayName/自定义帮助表负责，因此这里只构造无额外 tooltip 的标签。
                GUIContent label = GetLabel(property.name, displayName, tooltip);
                if (IsBooleanProperty(property.name))
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.Toggle(label, property.floatValue > 0.5f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        editor.RegisterPropertyChangeUndo(displayName);
                        property.floatValue = enabled ? 1f : 0f;
                    }
                }
                else
                {
                    // 交回 MaterialEditor 处理 Range、Enum、HDR、Normal、纹理抽屉、
                    // 多目标、Undo 和未来新增的 Shader 属性特性。
                    editor.ShaderProperty(property, label);
                }
                if (showReset)
                {
                    using (new EditorGUI.DisabledScope(!resetEnabled))
                    {
                        if (GUILayout.Button(ResetButton, EditorStyles.miniButton, GUILayout.Width(22f), GUILayout.Height(buttonHeight)))
                            resetRequested = true;
                    }
                }
                bool openCode = GUILayout.Button(CodeButton, EditorStyles.miniButton, GUILayout.Width(28f), GUILayout.Height(buttonHeight));
                Rect codeButtonRect = GUILayoutUtility.GetLastRect();
                if (openCode)
                {
                    Material material = editor.target as Material;
                    Vector2? clickPosition = codeButtonRect.width > 0f
                        ? GUIUtility.GUIToScreenPoint(new Vector2(codeButtonRect.xMax, codeButtonRect.yMin))
                        : (Vector2?)null;
                    Open(property, displayName, material, clickPosition);
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixed;
                EditorGUILayout.EndHorizontal();
            }
            return resetRequested;
        }

        internal static void DrawProperty(MaterialEditor editor, MaterialProperty property, string displayName)
        {
            DrawProperty(editor, property, displayName, false, false);
        }

        internal static void DrawCompactBooleanProperty(MaterialEditor editor, MaterialProperty property, string displayName)
        {
            if (editor == null || property == null)
                return;

            bool previousMixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle(
                    property.floatValue > 0.5f,
                    GUILayout.Width(18f));
                if (EditorGUI.EndChangeCheck())
                {
                    editor.RegisterPropertyChangeUndo(displayName);
                    property.floatValue = enabled ? 1f : 0f;
                }

                bool openCode = GUILayout.Button(
                    CodeButton,
                    EditorStyles.miniButton,
                    GUILayout.Width(28f),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                Rect codeButtonRect = GUILayoutUtility.GetLastRect();
                if (openCode)
                {
                    Material material = editor.target as Material;
                    Vector2? clickPosition = codeButtonRect.width > 0f
                        ? GUIUtility.GUIToScreenPoint(new Vector2(codeButtonRect.xMax, codeButtonRect.yMin))
                        : (Vector2?)null;
                    Open(property, displayName, material, clickPosition);
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixed;
            }
        }

        private static GUIContent GetLabel(string propertyName, string displayName, string tooltip)
        {
            GUIContent content;
            if (!LabelCache.TryGetValue(propertyName, out content)
                || content.text != displayName
                || content.tooltip != tooltip)
            {
                content = new GUIContent(displayName, tooltip);
                LabelCache[propertyName] = content;
            }
            return content;
        }

        private static void Open(MaterialProperty property, string displayName, Material material, Vector2? clickPosition)
        {
            Shader shader = material != null ? material.shader : null;
            CodeSet code = BuildCode(property, displayName, shader);
            var request = new ESAdvancedDialogRequest
            {
                dialogId = DialogIdPrefix + (shader != null ? shader.name : "unknown") + "." + property.name,
                title = "ES Shader C# 代码示例",
                subtitle = displayName + "  ·  " + property.name,
                message = code.Description,
                detail = "类型：" + code.TypeLabel + "\n目标：" + code.TargetLabel + "\n写入方式：" + code.WriteMode + "\n\n" + code.Note,
                confirmText = "关闭",
                showCancel = false,
                preferredSize = new Vector2(720f, 680f),
                minSize = new Vector2(560f, 420f),
                owner = EditorWindow.mouseOverWindow != null
                    ? EditorWindow.mouseOverWindow
                    : EditorWindow.focusedWindow,
            };
            if (clickPosition.HasValue)
            {
                request.positionMode = ESAdvancedDialogPositionMode.CustomScreenPosition;
                request.customScreenPosition = clickPosition.Value + new Vector2(14f, 14f);
            }
            request.createCustomContent = _ => BuildContent(code);
            request.AddAuxiliaryAction(
                "copy.call",
                "复制属性调用",
                _ => EditorGUIUtility.systemCopyBuffer = code.PropertyCall,
                "只复制当前属性的最小调用语句。",
                ESAdvancedDialogActionRole.Secondary,
                false);
            request.AddAuxiliaryAction(
                "copy.all",
                "复制完整代码",
                _ => EditorGUIUtility.systemCopyBuffer = code.FullExample,
                "复制当前属性的完整 C# 示例到剪贴板。",
                ESAdvancedDialogActionRole.Primary,
                false);
            ESAdvancedDialogWindow.Show(request);
        }

        private static bool IsBooleanProperty(string propertyName)
        {
            return propertyName.StartsWith("_Enable", StringComparison.Ordinal)
                || propertyName.StartsWith("_Use", StringComparison.Ordinal)
                || propertyName == "_AlphaClip"
                || propertyName == "_ReceiveShadows"
                || propertyName.EndsWith("Toggle", StringComparison.Ordinal);
        }

        private static VisualElement BuildContent(CodeSet code)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;

            AddCodeBlock(root, "属性调用", code.PropertyCall);
            AddInfoBlock(root, "属性说明", code.Description + "\n\n推荐用法：" + code.RecommendedUsage);
            AddCodeBlock(root, code.IsUi
                ? "UI 独立材质完整示例"
                : (code.UsesMaterialInstance ? "Renderer 独立材质完整示例" : "Renderer + MaterialPropertyBlock 完整示例"), code.FullExample);
            return root;
        }

        private static void AddCodeBlock(VisualElement root, string title, string code)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.style.marginBottom = 6f;
            var text = new TextField { multiline = true, isReadOnly = true, value = code };
            text.style.minHeight = title == "属性调用" ? 64f : 330f;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            foldout.Add(text);
            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = code) { text = "复制此段" };
            copy.style.alignSelf = Align.FlexEnd;
            copy.style.marginTop = 4f;
            foldout.Add(copy);
            root.Add(foldout);
        }

        private static CodeSet BuildCode(MaterialProperty property, string displayName, Shader shader)
        {
            bool isUi = shader != null && shader.name == "ES/UI/Composite URP";
            PropertyHelp help = ResolveHelp(shader, property, displayName);
            string propertyId = GetPropertyId(shader, property.name);
            string declaration;
            string assignment;
            CodeSet keywordCode;
            if (TryBuildMaterialKeywordCode(shader, property.name, displayName, help, out keywordCode))
                return keywordCode;

            if (TryBuildEnumCode(shader, property.name, out declaration, out assignment))
            {
                string enumCall = (isUi ? "materialInstance" : "propertyBlock") + assignment;
                string enumFull = BuildFullExample(isUi, declaration, enumCall);
                string enumNote = isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例只克隆一次材质并缓存，避免逐帧访问或修改共享材质。"
                    : "该属性在 ES 中有强枚举语义；示例仍通过 MaterialPropertyBlock 写入，不污染共享 Material。";
                return new CodeSet(enumCall, enumFull, enumNote, isUi, help);
            }

            if (IsBooleanProperty(property.name))
            {
                declaration = "public bool propertyEnabled = false;";
                assignment = ".SetFloat(" + propertyId + ", propertyEnabled ? 1f : 0f);";
                string boolCall = (isUi ? "materialInstance" : "propertyBlock") + assignment;
                string boolFull = BuildFullExample(isUi, declaration, boolCall);
                string boolNote = isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例使用缓存的独立材质实例写入 0/1。"
                    : "Shader Toggle 在运行时仍是 0/1 浮点属性；示例用 bool 表达语义，再转换为浮点写入 MaterialPropertyBlock。";
                return new CodeSet(boolCall, boolFull, boolNote, isUi, help);
            }

            switch (property.type)
            {
                case MaterialProperty.PropType.Color:
                    declaration = "public Color 属性值 = Color.white;";
                    assignment = ".SetColor(" + propertyId + ", 属性值);";
                    break;
                case MaterialProperty.PropType.Vector:
                    declaration = "public Vector4 属性值 = Vector4.zero;";
                    assignment = ".SetVector(" + propertyId + ", 属性值);";
                    break;
                case MaterialProperty.PropType.Texture:
                    declaration = "public Texture 属性值;";
                    assignment = ".SetTexture(" + propertyId + ", 属性值);";
                    break;
                default:
                    declaration = "public float 属性值;";
                    assignment = ".SetFloat(" + propertyId + ", 属性值);";
                    break;
            }

            string call = (isUi ? "materialInstance" : "propertyBlock") + assignment;
            string full = BuildFullExample(isUi, declaration, call);

            string note = isUi
                ? "UI Graphic 没有 MaterialPropertyBlock；示例克隆并缓存独立材质，不修改共享材质，也不逐帧创建实例。"
                : "Renderer 示例使用 MaterialPropertyBlock，不实例化共享 Material，适合对象级运行时参数。";
            return new CodeSet(call, full, note, isUi, help);
        }

        private static bool TryBuildMaterialKeywordCode(Shader shader, string propertyName, string displayName, PropertyHelp sourceHelp, out CodeSet code)
        {
            code = null;
            if (shader == null) return false;

            string declaration;
            string call;
            string type;
            if (propertyName == "_QualityTier" && shader.name == "ES/3D/Lit Composite URP")
            {
                declaration = "public ESCompositeQualityTier 效果质量 = ESCompositeQualityTier.标准;";
                call = "ES3DLitCompositeURPProperties.SetQuality(materialInstance, 效果质量);";
                type = "枚举 + 材质关键词";
            }
            else if (propertyName == "_QualityTier" && shader.name == "ES/3D/VFX Composite URP")
            {
                declaration = "public ESCompositeQualityTier 效果质量 = ESCompositeQualityTier.标准;";
                call = "ES3DVFXCompositeURPProperties.SetQuality(materialInstance, 效果质量);";
                type = "枚举 + 材质关键词";
            }
            else if (propertyName == "_ReceiveShadows" && shader.name == "ES/3D/Lit Composite URP")
            {
                declaration = "public bool 接收阴影 = true;";
                call = "ES3DLitCompositeURPProperties.SetReceiveShadows(materialInstance, 接收阴影);";
                type = "开关 + 材质关键词";
            }
            else
            {
                return false;
            }

            PropertyHelp help = new PropertyHelp(
                displayName,
                sourceHelp.Description,
                type,
                "Renderer 独立材质实例",
                "Material 属性 + 本地 Shader Keyword",
                "该选项决定编译变体，必须写入独立 Material；MaterialPropertyBlock 不能切换关键词。",
                sourceHelp.Summary);
            string full = BuildMaterialFullExample(declaration, call);
            code = new CodeSet(call, full,
                "这是关键词驱动属性。示例只创建一次材质实例，并在销毁时恢复原材质；不要在 Update 中反复实例化材质。",
                false, help, true);
            return true;
        }

        private static void AddInfoBlock(VisualElement root, string title, string content)
        {
            var foldout = new Foldout { text = title, value = true };
            var label = new Label(content);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 6f;
            label.style.paddingRight = 6f;
            label.style.paddingTop = 4f;
            label.style.paddingBottom = 8f;
            foldout.Add(label);
            root.Add(foldout);
        }

        private static PropertyHelp ResolveHelp(Shader shader, MaterialProperty property, string displayName)
        {
            string shaderName = shader != null ? shader.name : string.Empty;
            string key = shaderName + ":" + property.name;
            PropertyHelp help;
            if (HelpByProperty.TryGetValue(key, out help))
                return help;

            string type = property.type == MaterialProperty.PropType.Color ? "颜色"
                : property.type == MaterialProperty.PropType.Vector ? "向量"
                : property.type == MaterialProperty.PropType.Texture ? "纹理" : "浮点/范围";
            string target = shaderName == "ES/UI/Composite URP" ? "UI Graphic / Image 独立材质" : "Renderer（Sprite、Mesh 或 VFX）";
            string mode = shaderName == "ES/UI/Composite URP" ? "缓存的 Material 实例" : "MaterialPropertyBlock";
            string semantic = DescribeFallback(property.name, displayName, type, shaderName);
            return new PropertyHelp(
                displayName,
                semantic,
                type,
                target,
                mode,
                "在不修改共享材质的前提下，仅为当前对象写入“" + displayName + "”。",
                property.name);
        }

        private static string DescribeFallback(string propertyName, string displayName, string type, string shaderName)
        {
            if (propertyName.IndexOf("Color", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "设置“" + displayName + "”的颜色值；HDR 颜色会作为发光或效果叠加参与最终输出。";
            if (propertyName.IndexOf("Tex", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Map", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "设置“" + displayName + "”使用的纹理资源；运行时可按对象替换而不改共享材质。";
            if (propertyName.IndexOf("Enable", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Use", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("AlphaClip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "控制“" + displayName + "”效果是否启用；0 表示关闭，1 表示启用。";
            if (propertyName.IndexOf("Progress", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "推进“" + displayName + "”对应的效果阶段，通常使用 0 到 1 的归一化值。";
            if (propertyName.IndexOf("Speed", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "控制“" + displayName + "”随时间变化的速度；可使用负值反向播放。";
            if (propertyName.IndexOf("Width", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Intensity", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Amount", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Strength", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "调节“" + displayName + "”的视觉幅度；数值越大，效果越明显，也应关注透明度和过绘成本。";
            if (propertyName.IndexOf("Scale", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Density", System.StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Frequency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "调节“" + displayName + "”的空间采样密度；过高的频率可能造成闪烁或噪声过密。";
            if (propertyName.IndexOf("Mode", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "选择“" + displayName + "”的算法分支；推荐使用对应 ES 强枚举保持数值稳定。";
            if (type == "向量")
                return "设置“" + displayName + "”的多维参数；不同分量分别控制对应方向、范围或坐标。";
            return "设置“" + displayName + "”的运行时数值，用于按对象覆盖“" + shaderName + "”的材质默认参数。";
        }

        private static readonly Dictionary<string, PropertyHelp> HelpByProperty = CreateHelpTable();

        private static Dictionary<string, PropertyHelp> CreateHelpTable()
        {
            var map = new Dictionary<string, PropertyHelp>();
            AddHelp(map, "ES/2D/Composite URP", "_MainTex", "主纹理", "2D 精灵的基础采样纹理。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_Color", "颜色", "与主纹理相乘的对象颜色和透明度。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_AnimationMode", "动画模式", "选择静态显示或按时间推进的序列帧模式。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/2D/Composite URP", "_SequenceFrame", "序列帧当前帧", "指定序列帧动画当前使用的帧索引。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_SequenceSpeed", "序列帧速度", "控制序列帧按场景时间自动推进的速度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_FadeMode", "渐隐模式", "选择无、方向遮罩、纹理遮罩或噪声溶解。", "枚举");
            AddHelp(map, "ES/2D/Composite URP", "_FadeProgress", "渐隐进度", "控制渐隐/遮罩/溶解效果推进到的归一化位置。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_FadeMask", "渐隐遮罩", "为遮罩模式提供逐像素的灰度控制纹理。", "纹理");
            AddHelp(map, "ES/2D/Composite URP", "_DissolveEdgeColor", "溶解边缘颜色", "设置溶解边界的发光颜色。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_EnableAddColor", "启用叠加颜色", "开启额外颜色叠加层。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableStrongTint", "启用强制染色", "开启覆盖原始颜色的强制染色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableColorReplace", "启用颜色替换", "按颜色距离将指定颜色替换为目标颜色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBrightness", "启用亮度", "开启对象亮度调整。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Brightness", "亮度", "控制最终颜色的整体亮度倍率。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableContrast", "启用对比度", "开启颜色对比度调整。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Contrast", "对比度", "控制颜色相对中性灰的对比度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableSaturation", "启用饱和度", "开启颜色饱和度调整。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Saturation", "饱和度", "控制颜色的鲜艳程度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableHue", "启用色相偏移", "开启 HSV 色相旋转。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_Hue", "色相偏移", "控制颜色在色环上的旋转量。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableNegative", "启用负片", "开启颜色反相效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableRainbow", "启用彩虹渐变", "开启沿坐标和时间变化的彩虹染色。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableInnerOutline", "启用内描边", "在精灵内部边缘绘制描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableOuterOutline", "启用外描边", "在精灵外部扩展透明区域绘制描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnablePixelOutline", "启用像素描边", "使用像素宽度绘制硬边描边。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableShine", "启用扫光", "开启沿指定角度移动的扫光带。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_ShineIntensity", "扫光强度", "控制扫光叠加到最终颜色的强度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableDistortion", "启用噪声扰动", "开启噪声驱动的 UV 扰动。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_DistortionStrength", "扰动强度", "控制噪声扰动造成的 UV 偏移量。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableHologram", "启用全息", "开启扫描线与最低透明度控制的全息效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_HologramColor", "全息颜色", "设置全息覆盖层的颜色。", "颜色");
            AddHelp(map, "ES/2D/Composite URP", "_EnableGlitch", "启用故障", "开启基于坐标和时间的随机 UV 横向抖动。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_GlitchIntensity", "故障强度", "控制故障效果的最大 UV 偏移。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFrozen", "启用冰冻", "开启冰冻颜色和冰晶高光效果。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBurn", "启用燃烧", "开启按噪声推进的燃烧边缘和裁剪。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_BurnProgress", "燃烧进度", "控制燃烧边缘在噪声场中的推进位置。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnablePoison", "启用中毒", "开启周期性中毒染色效果。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BaseMap", "基础颜色纹理", "URP Lit 表面的基础颜色采样纹理。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BaseColor", "基础颜色", "URP Lit 表面的基础颜色和透明度。", "颜色");
            AddHelp(map, "ES/3D/Lit Composite URP", "_NormalMap", "法线纹理", "改变光照法线方向的法线贴图。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseNormalMap", "启用法线纹理", "开启后才采样法线纹理；关闭时使用顶点法线并节省一次纹理采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_Metallic", "金属度", "控制表面从绝缘体到金属的反射响应。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_Smoothness", "光滑度", "控制高光的锐利程度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_UseEmission", "启用自发光", "开启后才采样并叠加自发光纹理。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveMode", "溶解模式", "选择噪声溶解或距离溶解算法。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveProgress", "溶解进度", "控制模型被溶解掉的归一化进度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRim", "启用边缘光", "按视角边缘为模型增加轮廓光。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_RimIntensity", "边缘光强度", "控制轮廓光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShine", "启用扫光", "开启沿模型表面移动的扫光高光。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ShineIntensity", "扫光强度", "控制扫光高光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBurn", "启用燃烧边缘", "开启溶解/燃烧交界处的边缘着色。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_AlphaClip", "启用透明裁剪", "按 Cutoff 阈值丢弃低透明度像素。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_QualityTier", "效果质量档位", "选择基础、标准或高质量效果变体；档位越高，片元效果和变体成本越高。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_MainTex", "VFX 主纹理", "粒子或特效卡片的主采样纹理。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_NoiseTex", "VFX 噪声纹理", "驱动扰动、溶解和故障的噪声来源。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_Distortion", "扰动强度", "控制噪声对 VFX UV 的偏移量。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DissolveMode", "VFX 溶解模式", "选择普通溶解或带边缘光的溶解。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DissolveProgress", "VFX 溶解进度", "控制特效透明区域的推进位置。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableHologram", "VFX 全息开关", "为特效卡片叠加扫描线全息效果。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableGlitch", "VFX 故障开关", "为特效卡片增加随机横向故障偏移。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_QualityTier", "VFX 效果质量档位", "选择基础、标准或高质量 VFX 变体；高质量档启用全息和故障。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/UI/Composite URP", "_MainTex", "UI 主纹理", "UI Graphic 的主采样纹理。", "纹理");
            AddHelp(map, "ES/UI/Composite URP", "_Color", "UI 颜色", "与 UI 顶点颜色和主纹理相乘的颜色。", "颜色");
            AddHelp(map, "ES/UI/Composite URP", "_EnableHologram", "UI 全息开关", "在 UI 上叠加动态扫描线。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableGlitch", "UI 故障开关", "在 UI 上叠加随机横向抖动。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_GlitchSpeed", "故障速度", "控制 UI 故障图样随时间变化的速度。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_AlphaClip", "UI 透明裁剪", "按阈值裁剪 UI 像素。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/UI/Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            return map;
        }

        private static void AddHelp(Dictionary<string, PropertyHelp> map, string shader, string property, string title, string description, string type)
        {
            map[shader + ":" + property] = new PropertyHelp(title, description, type,
                shader == "ES/UI/Composite URP" ? "UI Graphic / Image 独立材质" : "Renderer（Sprite、Mesh 或 VFX）",
                shader == "ES/UI/Composite URP" ? "缓存的 Material 实例" : "MaterialPropertyBlock",
                "按对象覆盖材质默认值，不修改共享材质。", "设置“" + title + "”的运行时值。");
        }

        private static string BuildFullExample(bool isUi, string declaration, string call)
        {
            if (isUi)
            {
                return "using UnityEngine;\nusing UnityEngine.UI;\nusing ES;\n\n"
                    + "public sealed class ESUIShaderExample : MonoBehaviour\n{\n    "
                    + declaration + "\n\n    private Graphic graphic;\n    private Material originalMaterial;\n    private Material materialInstance;\n\n    private void Awake()\n    {\n        graphic = GetComponent<Graphic>();\n        originalMaterial = graphic.material;\n        Material source = graphic.materialForRendering;\n        if (source == null) return;\n\n        materialInstance = new Material(source)\n        {\n            name = source.name + \" (ES UI Instance)\"\n        };\n        graphic.material = materialInstance;\n        Apply();\n    }\n\n    public void Apply()\n    {\n        if (materialInstance == null) return;\n        " + call + "\n        graphic.SetMaterialDirty();\n    }\n\n    private void OnDestroy()\n    {\n        if (graphic != null && graphic.material == materialInstance)\n            graphic.material = originalMaterial;\n        if (materialInstance != null)\n            Destroy(materialInstance);\n    }\n}";
            }

            return "using UnityEngine;\nusing ES;\n\n"
                + "public sealed class ESShaderExample : MonoBehaviour\n{\n    "
                + declaration + "\n\n    private Renderer targetRenderer;\n    private MaterialPropertyBlock propertyBlock;\n\n    private void Awake()\n    {\n        targetRenderer = GetComponent<Renderer>();\n        propertyBlock = new MaterialPropertyBlock();\n        Apply();\n    }\n\n    public void Apply()\n    {\n        if (targetRenderer == null) return;\n        targetRenderer.GetPropertyBlock(propertyBlock);\n        " + call + "\n        targetRenderer.SetPropertyBlock(propertyBlock);\n    }\n}";
        }

        private static string BuildMaterialFullExample(string declaration, string call)
        {
            return "using UnityEngine;\nusing ES;\n\n"
                + "public sealed class ESShaderMaterialKeywordExample : MonoBehaviour\n{\n    "
                + declaration + "\n\n    private Renderer targetRenderer;\n    private Material originalMaterial;\n    private Material materialInstance;\n\n    private void Awake()\n    {\n        targetRenderer = GetComponent<Renderer>();\n        originalMaterial = targetRenderer.sharedMaterial;\n        if (originalMaterial == null) return;\n\n        materialInstance = new Material(originalMaterial)\n        {\n            name = originalMaterial.name + \" (ES Instance)\"\n        };\n        targetRenderer.sharedMaterial = materialInstance;\n        Apply();\n    }\n\n    public void Apply()\n    {\n        if (materialInstance == null) return;\n        " + call + "\n    }\n\n    private void OnDestroy()\n    {\n        if (targetRenderer != null && targetRenderer.sharedMaterial == materialInstance)\n            targetRenderer.sharedMaterial = originalMaterial;\n        if (materialInstance != null)\n            Destroy(materialInstance);\n    }\n}";
        }

        private static bool TryBuildEnumCode(Shader shader, string propertyName, out string declaration, out string assignment)
        {
            declaration = null;
            assignment = null;
            if (shader == null)
                return false;

            switch (shader.name)
            {
                case "ES/2D/Composite URP":
                    if (propertyName == "_AnimationMode")
                    {
                        declaration = "public ES2DCompositeAnimationMode 动画模式 = ES2DCompositeAnimationMode.序列帧;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.AnimationMode, (float)动画模式);";
                        return true;
                    }
                    if (propertyName == "_FadeMode")
                    {
                        declaration = "public ES2DCompositeFadeMode 渐隐模式 = ES2DCompositeFadeMode.渐隐;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.FadeMode, (float)渐隐模式);";
                        return true;
                    }
                    if (propertyName == "_CoordinateMode")
                    {
                        declaration = "public ES2DCompositeCoordinateMode 坐标模式 = ES2DCompositeCoordinateMode.UV;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.CoordinateMode, (float)坐标模式);";
                        return true;
                    }
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ES2DCompositeTimeMode 时间模式 = ES2DCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES2DCompositeURPProperties.TimeMode, (float)时间模式);";
                        return true;
                    }
                    break;
                case "ES/3D/Lit Composite URP":
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.TimeMode, (float)时间来源);";
                        return true;
                    }
                    if (propertyName == "_QualityTier")
                    {
                        declaration = "public ESCompositeQualityTier 效果质量 = ESCompositeQualityTier.标准;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.QualityTier, (float)效果质量);";
                        return true;
                    }
                    if (propertyName == "_DissolveMode")
                    {
                        declaration = "public ES3DCompositeDissolveMode 溶解模式 = ES3DCompositeDissolveMode.噪声溶解;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.DissolveMode, (float)溶解模式);";
                        return true;
                    }
                    break;
                case "ES/3D/VFX Composite URP":
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.TimeMode, (float)时间来源);";
                        return true;
                    }
                    if (propertyName == "_QualityTier")
                    {
                        declaration = "public ESCompositeQualityTier 效果质量 = ESCompositeQualityTier.标准;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.QualityTier, (float)效果质量);";
                        return true;
                    }
                    if (propertyName == "_DissolveMode")
                    {
                        declaration = "public ES3DVFXDissolveMode 溶解模式 = ES3DVFXDissolveMode.溶解加边缘光;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.DissolveMode, (float)溶解模式);";
                        return true;
                    }
                    break;
                case "ES/UI/Composite URP":
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ESUICompositeURPProperties.TimeMode, (float)时间来源);";
                        return true;
                    }
                    break;
            }
            return false;
        }

        private static string GetPropertyId(Shader shader, string propertyName)
        {
            if (shader != null)
            {
                switch (shader.name)
                {
                    case "ES/2D/Composite URP":
                        if (propertyName == "_AnimationMode" || propertyName == "_FadeMode" || propertyName == "_FadeProgress" || propertyName == "_CoordinateMode" || propertyName == "_TimeMode" || propertyName == "_CustomTime" || propertyName == "_TimeScale" || propertyName == "_MainTexScaleOffset" || propertyName == "_SequenceFrame" || propertyName == "_SequenceSpeed" || propertyName == "_GlowIntensity" || propertyName == "_ShineIntensity" || propertyName == "_MainTex" || propertyName == "_Color" || propertyName == "_VertexColorStrength" || propertyName == "_DistortionStrength" || propertyName == "_EnableHologram" || propertyName == "_EnableGlitch" || propertyName == "_EnableBurn" || propertyName == "_EnablePoison" || propertyName == "_EnableFrozen")
                            return "ES2DCompositeURPProperties." + ToPascal(propertyName);
                        break;
                    case "ES/3D/Lit Composite URP":
                        if (propertyName == "_TimeMode" || propertyName == "_CustomTime" || propertyName == "_TimeScale" || propertyName == "_MainTexScaleOffset" || propertyName == "_QualityTier" || propertyName == "_DissolveMode" || propertyName == "_DissolveProgress" || propertyName == "_RimIntensity" || propertyName == "_ShineIntensity" || propertyName == "_BaseColor" || propertyName == "_Metallic" || propertyName == "_Smoothness" || propertyName == "_Occlusion" || propertyName == "_UseNormalMap" || propertyName == "_UseOcclusionMap" || propertyName == "_UseEmission" || propertyName == "_EnableRim" || propertyName == "_EnableBurn" || propertyName == "_AlphaClip" || propertyName == "_ReceiveShadows")
                            return "ES3DLitCompositeURPProperties." + ToPascal(propertyName);
                        break;
                    case "ES/3D/VFX Composite URP":
                        if (propertyName == "_TimeMode" || propertyName == "_CustomTime" || propertyName == "_TimeScale" || propertyName == "_MainTexScaleOffset" || propertyName == "_QualityTier" || propertyName == "_DissolveMode" || propertyName == "_DissolveProgress" || propertyName == "_EnableHologram" || propertyName == "_EnableGlitch" || propertyName == "_Color" || propertyName == "_VertexColorStrength" || propertyName == "_Distortion")
                            return "ES3DVFXCompositeURPProperties." + ToPascal(propertyName);
                        break;
                    case "ES/UI/Composite URP":
                        if (propertyName == "_TimeMode" || propertyName == "_CustomTime" || propertyName == "_TimeScale" || propertyName == "_MainTexScaleOffset" || propertyName == "_EnableHologram" || propertyName == "_EnableGlitch" || propertyName == "_GlitchSpeed" || propertyName == "_Color" || propertyName == "_VertexColorStrength" || propertyName == "_AlphaClip")
                            return "ESUICompositeURPProperties." + ToPascal(propertyName);
                        break;
                }
            }
            return "Shader.PropertyToID(\"" + propertyName + "\")";
        }

        private static string ToPascal(string propertyName)
        {
            string value = propertyName.TrimStart('_');
            switch (value)
            {
                case "EnableHologram": return "HologramEnabled";
                case "EnableGlitch": return "GlitchEnabled";
                case "EnableBurn": return "BurnEnabled";
                case "EnablePoison": return "PoisonEnabled";
                case "EnableFrozen": return "FrozenEnabled";
                case "EnableRim": return "RimEnabled";
                case "UseNormalMap": return "NormalMapEnabled";
                case "UseOcclusionMap": return "OcclusionMapEnabled";
                case "UseEmission": return "EmissionEnabled";
            }
            if (string.IsNullOrEmpty(value)) return "Property";
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private sealed class CodeSet
        {
            internal readonly string PropertyCall;
            internal readonly string FullExample;
            internal readonly string Note;
            internal readonly bool IsUi;
            internal readonly bool UsesMaterialInstance;
            internal readonly string Description;
            internal readonly string TypeLabel;
            internal readonly string TargetLabel;
            internal readonly string WriteMode;
            internal readonly string RecommendedUsage;

            internal CodeSet(string propertyCall, string fullExample, string note, bool isUi, PropertyHelp help, bool usesMaterialInstance = false)
            {
                PropertyCall = propertyCall;
                FullExample = fullExample;
                Note = note;
                IsUi = isUi;
                UsesMaterialInstance = usesMaterialInstance;
                Description = help.Description;
                TypeLabel = help.TypeLabel;
                TargetLabel = help.TargetLabel;
                WriteMode = help.WriteMode;
                RecommendedUsage = help.RecommendedUsage;
            }
        }

        private sealed class PropertyHelp
        {
            internal readonly string Title;
            internal readonly string Description;
            internal readonly string TypeLabel;
            internal readonly string TargetLabel;
            internal readonly string WriteMode;
            internal readonly string RecommendedUsage;
            internal readonly string Summary;

            internal PropertyHelp(
                string title,
                string description,
                string typeLabel,
                string targetLabel,
                string writeMode,
                string recommendedUsage,
                string summary)
            {
                Title = title;
                Description = description;
                TypeLabel = typeLabel;
                TargetLabel = targetLabel;
                WriteMode = writeMode;
                RecommendedUsage = recommendedUsage;
                Summary = summary;
            }
        }
    }
}
