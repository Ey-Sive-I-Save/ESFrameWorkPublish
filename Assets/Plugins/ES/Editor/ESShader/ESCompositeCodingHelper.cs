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
        private static readonly string[] CoordinateModeOptions = { "UV", "世界空间", "屏幕空间" };
        private static readonly string[] TimeModeOptions = { "场景时间", "非缩放时间", "自定义时间" };
        private static readonly string[] AnimationModeOptions = { "关闭", "序列帧" };
        private static readonly string[] FadeModeOptions = { "关闭", "方向渐隐", "纹理遮罩", "噪声溶解" };
        private static readonly string[] LitDissolveModeOptions = { "关闭", "噪声溶解", "距离溶解" };
        private static readonly string[] VfxDissolveModeOptions = { "关闭", "溶解", "溶解带边缘" };
        private static readonly string[] QualityTierOptions = { "基础", "标准", "高质量" };
        private static readonly string[] VertexColorMaskOptions = { "不使用遮罩", "红色通道", "绿色通道", "蓝色通道", "透明通道" };
        private static readonly string[] VfxSequencePlaybackOptions = { "手动帧", "按时间播放", "Custom1 Z 帧号" };
        private static readonly string[] VfxBlendModeOptions = { "透明混合", "叠加", "预乘透明", "正片叠底" };
        private static readonly string[] VfxDepthWriteOptions = { "关闭", "开启" };
        private static readonly string[] VfxDepthTestOptions = { "禁用", "从不", "小于", "等于", "小于等于", "大于", "不等于", "大于等于", "始终" };
        private static readonly string[] VfxCullOptions = { "双面", "剔除正面", "剔除背面" };

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
                bool localizedEnumDrawn = TryDrawLocalizedEnum(editor, property, label);
                if (!localizedEnumDrawn && IsBooleanProperty(property.name))
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.Toggle(label, property.floatValue > 0.5f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        editor.RegisterPropertyChangeUndo(displayName);
                        property.floatValue = enabled ? 1f : 0f;
                    }
                }
                else if (!localizedEnumDrawn)
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

                // 给右侧小按钮留出自适应安全区，避免 Inspector 停靠在屏幕边缘时难以点击。
                GUILayout.Space(ESEditorPresentation.GetInspectorRightGutter(EditorGUIUtility.currentViewWidth));
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

        internal static void DrawCompactBooleanProperty(
            MaterialEditor editor,
            MaterialProperty property,
            string displayName,
            Rect toggleRect,
            Rect codeButtonRect)
        {
            if (editor == null || property == null)
                return;

            bool previousMixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUI.Toggle(toggleRect, property.floatValue > 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    editor.RegisterPropertyChangeUndo(displayName);
                    property.floatValue = enabled ? 1f : 0f;
                }

                bool openCode = GUI.Button(codeButtonRect, CodeButton, EditorStyles.miniButton);
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

        private static bool TryDrawLocalizedEnum(MaterialEditor editor, MaterialProperty property, GUIContent label)
        {
            Material material = editor.target as Material;
            Shader shader = material != null ? material.shader : null;
            string[] options;
            if (!TryGetLocalizedEnumOptions(shader, property.name, out options))
                return false;

            int selectedIndex = Mathf.Clamp(Mathf.RoundToInt(property.floatValue), 0, options.Length - 1);
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(label, selectedIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo(label.text);
                property.floatValue = nextIndex;
            }
            return true;
        }

        private static bool TryGetLocalizedEnumOptions(Shader shader, string propertyName, out string[] options)
        {
            options = null;
            if (shader == null)
                return false;

            string shaderName = shader.name;
            if (propertyName == "_VertexAnimationMask"
                && (shaderName == "ES/3D/Lit Composite URP" || shaderName == "ES/3D/VFX Composite URP"))
            {
                options = VertexColorMaskOptions;
                return true;
            }
            if (propertyName == "_TimeMode"
                && (shaderName == "ES/2D/Composite URP"
                    || shaderName == "ES/3D/Lit Composite URP"
                    || shaderName == "ES/3D/VFX Composite URP"
                    || shaderName == "ES/UI/Composite URP"))
            {
                options = TimeModeOptions;
                return true;
            }

            if (shaderName == "ES/2D/Composite URP")
            {
                switch (propertyName)
                {
                    case "_CoordinateMode": options = CoordinateModeOptions; return true;
                    case "_AnimationMode": options = AnimationModeOptions; return true;
                    case "_FadeMode": options = FadeModeOptions; return true;
                }
            }
            else if (shaderName == "ES/3D/Lit Composite URP")
            {
                if (propertyName == "_DissolveMode")
                {
                    options = LitDissolveModeOptions;
                    return true;
                }
                if (propertyName == "_QualityTier")
                {
                    options = QualityTierOptions;
                    return true;
                }
            }
            else if (shaderName == "ES/3D/VFX Composite URP")
            {
                if (propertyName == "_SequencePlayback")
                {
                    options = VfxSequencePlaybackOptions;
                    return true;
                }
                if (propertyName == "_BlendMode")
                {
                    options = VfxBlendModeOptions;
                    return true;
                }
                if (propertyName == "_ZWriteMode")
                {
                    options = VfxDepthWriteOptions;
                    return true;
                }
                if (propertyName == "_ZTest")
                {
                    options = VfxDepthTestOptions;
                    return true;
                }
                if (propertyName == "_Cull")
                {
                    options = VfxCullOptions;
                    return true;
                }
                if (propertyName == "_DissolveMode")
                {
                    options = VfxDissolveModeOptions;
                    return true;
                }
                if (propertyName == "_QualityTier")
                {
                    options = QualityTierOptions;
                    return true;
                }
            }
            return false;
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
                "复制推荐方法",
                _ => EditorGUIUtility.systemCopyBuffer = code.FocusedExample,
                "复制接收已准备参数块或材质实例的方法，不包含获取和回写流程。",
                ESAdvancedDialogActionRole.Secondary,
                false);
            request.AddAuxiliaryAction(
                "copy.all",
                "复制完整接线",
                _ => EditorGUIUtility.systemCopyBuffer = code.FullExample,
                "复制包含组件获取、生命周期和回写的完整参考。",
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
                || propertyName == "_RadialMaskInvert"
                || propertyName.EndsWith("Toggle", StringComparison.Ordinal);
        }

        private static VisualElement BuildContent(CodeSet code)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 4f;

            AddCodeBlock(root, "推荐写法 · 参数已准备", code.FocusedExample, true, 150f);
            AddCodeBlock(root, "最小调用", code.PropertyCall, false, 64f);
            AddInfoBlock(root, "属性说明", code.Description + "\n\n推荐用法：" + code.RecommendedUsage);
            AddCodeBlock(root, code.IsUi
                ? (code.UsesMaterialInstance ? "完整接线 · UI 独立材质" : "完整接线 · UI 组件")
                : (code.UsesMaterialInstance ? "完整接线 · Renderer 独立材质" : "完整接线 · Renderer 与参数块"), code.FullExample, false, 330f);
            return root;
        }

        private static void AddCodeBlock(VisualElement root, string title, string code, bool expanded, float minHeight)
        {
            var foldout = new Foldout { text = title, value = expanded };
            Color border = title.StartsWith("推荐写法", StringComparison.Ordinal)
                ? new Color(0.16f, 0.68f, 0.78f, 0.92f)
                : new Color(0.34f, 0.40f, 0.48f, 0.72f);
            Color surface = EditorGUIUtility.isProSkin
                ? new Color(0.105f, 0.12f, 0.145f, 0.96f)
                : new Color(0.90f, 0.92f, 0.95f, 0.96f);
            foldout.style.marginBottom = 8f;
            foldout.style.paddingLeft = 8f;
            foldout.style.paddingRight = 8f;
            foldout.style.paddingTop = 5f;
            foldout.style.paddingBottom = 7f;
            foldout.style.backgroundColor = surface;
            foldout.style.borderLeftWidth = title.StartsWith("推荐写法", StringComparison.Ordinal) ? 3f : 1f;
            foldout.style.borderRightWidth = 1f;
            foldout.style.borderTopWidth = 1f;
            foldout.style.borderBottomWidth = 1f;
            foldout.style.borderLeftColor = border;
            foldout.style.borderRightColor = border;
            foldout.style.borderTopColor = border;
            foldout.style.borderBottomColor = border;
            ESEditorPresentation.ApplyCornerRadius(
                foldout,
                ESEditorPresentation.ESCornerRadiusToken.Control);
            var text = new TextField { multiline = true, isReadOnly = true, value = code };
            text.style.minHeight = minHeight;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            text.style.marginTop = 5f;
            text.style.fontSize = 12f;
            foldout.Add(text);
            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = code) { text = "复制" };
            copy.style.alignSelf = Align.FlexEnd;
            copy.style.marginTop = 4f;
            copy.style.minWidth = 62f;
            foldout.Add(copy);
            root.Add(foldout);
        }

        private static CodeSet BuildCode(MaterialProperty property, string displayName, Shader shader)
        {
            bool isUi = shader != null && shader.name == "ES/UI/Composite URP";
            PropertyHelp help = ResolveHelp(shader, property, displayName);
            if (isUi && property.name == "_MainTex")
                return BuildUITextureCode(help);

            string propertyId = GetPropertyId(shader, property.name);
            string declaration;
            string assignment;
            CodeSet keywordCode;
            if (TryBuildMaterialKeywordCode(shader, property.name, displayName, help, out keywordCode))
                return keywordCode;

            if (TryBuildEnumCode(shader, property.name, out declaration, out assignment))
            {
                string enumCall = (isUi ? "materialInstance" : "propertyBlock") + assignment;
                string enumFocused = BuildFocusedExample(isUi, false, declaration, enumCall);
                string enumFull = BuildFullExample(isUi, declaration, enumCall);
                string enumNote = isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例只克隆一次材质并缓存，避免逐帧访问或修改共享材质。"
                    : "该属性在 ES 中有强枚举语义；示例仍通过 MaterialPropertyBlock 写入，不污染共享 Material。";
                return new CodeSet(enumCall, enumFocused, enumFull, enumNote, isUi, help, isUi);
            }

            if (IsBooleanProperty(property.name))
            {
                declaration = "public bool 是否启用 = false;";
                assignment = ".SetFloat(" + propertyId + ", 是否启用 ? 1f : 0f);";
                string boolCall = (isUi ? "materialInstance" : "propertyBlock") + assignment;
                string boolFocused = BuildFocusedExample(isUi, false, declaration, boolCall);
                string boolFull = BuildFullExample(isUi, declaration, boolCall);
                string boolNote = isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例使用缓存的独立材质实例写入 0/1。"
                    : "Shader Toggle 在运行时仍是 0/1 浮点属性；示例用 bool 表达语义，再转换为浮点写入 MaterialPropertyBlock。";
                return new CodeSet(boolCall, boolFocused, boolFull, boolNote, isUi, help, isUi);
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
            string focused = BuildFocusedExample(isUi, false, declaration, call);
            string full = BuildFullExample(isUi, declaration, call);

            string note = isUi
                ? "UI Graphic 没有 MaterialPropertyBlock；示例克隆并缓存独立材质，不修改共享材质，也不逐帧创建实例。"
                : "Renderer 示例使用 MaterialPropertyBlock，不实例化共享 Material，适合对象级运行时参数。";
            return new CodeSet(call, focused, full, note, isUi, help, isUi);
        }

        private static CodeSet BuildUITextureCode(PropertyHelp sourceHelp)
        {
            const string call = "rawImage.texture = 主纹理;\n        rawImage.SetMaterialDirty();";
            const string focused = "using UnityEngine;\nusing UnityEngine.UI;\n\n"
                + "public static class ESUI主纹理示例\n"
                + "{\n"
                + "    public static void 应用到RawImage(RawImage rawImage, Texture 主纹理)\n"
                + "    {\n"
                + "        if (rawImage == null) return;\n"
                + "        rawImage.texture = 主纹理;\n"
                + "        rawImage.SetMaterialDirty();\n"
                + "    }\n"
                + "}";
            const string full = "using UnityEngine;\nusing UnityEngine.UI;\n\n"
                + "[RequireComponent(typeof(RawImage))]\n"
                + "public sealed class ESUI主纹理接线示例 : MonoBehaviour\n"
                + "{\n"
                + "    public Texture 主纹理;\n\n"
                + "    private RawImage rawImage;\n\n"
                + "    private void Awake()\n"
                + "    {\n"
                + "        rawImage = GetComponent<RawImage>();\n"
                + "        Apply();\n"
                + "    }\n\n"
                + "    public void Apply()\n"
                + "    {\n"
                + "        if (rawImage == null) return;\n"
                + "        rawImage.texture = 主纹理;\n"
                + "        rawImage.SetMaterialDirty();\n"
                + "    }\n"
                + "}";
            var help = new PropertyHelp(
                sourceHelp.Title,
                "UI 主纹理由 CanvasRenderer 按对象提供；RawImage 写 texture，Image 则替换 sprite。",
                "UI 图像资源",
                "RawImage.texture / Image.sprite",
                "UI Graphic 数据",
                "不要直接写材质 _MainTex；它会被 CanvasRenderer 的 PerRendererData 覆盖。",
                sourceHelp.Summary);
            return new CodeSet(
                call,
                focused,
                full,
                "示例使用 RawImage。若目标是 Image，请把纹理制作成 Sprite 并赋给 Image.sprite。",
                true,
                help,
                false);
        }

        private static bool TryBuildMaterialKeywordCode(Shader shader, string propertyName, string displayName, PropertyHelp sourceHelp, out CodeSet code)
        {
            code = null;
            if (shader == null) return false;

            bool isUi = shader.name == "ES/UI/Composite URP";
            string declaration;
            string call;
            string type;
            bool renderState = false;
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
            else if (propertyName == "_UseUIAlphaClip" && shader.name == "ES/UI/Composite URP")
            {
                declaration = "public bool 使用UI透明裁剪 = false;";
                call = "ESUICompositeURPProperties.SetUIAlphaClip(materialInstance, 使用UI透明裁剪);";
                type = "开关 + UI 材质关键词";
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_BlendMode")
            {
                declaration = "public ES3DVFXBlendMode 混合模式 = ES3DVFXBlendMode.透明混合;";
                call = "ES3DVFXCompositeURPProperties.SetBlendMode(materialInstance, 混合模式);";
                type = "强枚举 + 材质混合状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_ZWriteMode")
            {
                declaration = "public ES3DVFXDepthWriteMode 深度写入 = ES3DVFXDepthWriteMode.关闭;";
                call = "ES3DVFXCompositeURPProperties.SetDepthWrite(materialInstance, 深度写入);";
                type = "强枚举 + 材质深度状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_ZTest")
            {
                declaration = "public ES3DVFXDepthTestMode 深度测试 = ES3DVFXDepthTestMode.小于等于;";
                call = "ES3DVFXCompositeURPProperties.SetDepthTest(materialInstance, 深度测试);";
                type = "强枚举 + 材质深度状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_Cull")
            {
                declaration = "public ES3DVFXCullMode 剔除模式 = ES3DVFXCullMode.双面;";
                call = "ES3DVFXCompositeURPProperties.SetCullMode(materialInstance, 剔除模式);";
                type = "强枚举 + 材质光栅状态";
                renderState = true;
            }
            else if (shader.name == "ES/3D/VFX Composite URP" && propertyName == "_QueueOffset")
            {
                declaration = "[Range(-50, 50)] public int 渲染队列偏移;";
                call = "ES3DVFXCompositeURPProperties.SetQueueOffset(materialInstance, 渲染队列偏移);";
                type = "整数 + 材质渲染队列";
                renderState = true;
            }
            else
            {
                return false;
            }

            PropertyHelp help = new PropertyHelp(
                displayName,
                sourceHelp.Description,
                type,
                isUi ? "UI Graphic / Image 独立材质" : "Renderer 独立材质实例",
                isUi ? "缓存的 Material 实例 + 本地 Shader Keyword" : "Material 属性 + 本地 Shader Keyword",
                renderState
                    ? "该选项属于 GPU 渲染状态，必须写入独立 Material；MaterialPropertyBlock 不能覆盖 Pass 状态。"
                    : "该选项决定编译变体，必须写入独立 Material；MaterialPropertyBlock 不能切换关键词。",
                sourceHelp.Summary);
            string full = isUi ? BuildFullExample(true, declaration, call) : BuildMaterialFullExample(declaration, call);
            string focused = BuildFocusedExample(isUi, !isUi, declaration, call);
            code = new CodeSet(call, focused, full,
                isUi
                    ? "UI Graphic 没有 MaterialPropertyBlock；示例缓存独立材质并同步关键词，修改后调用 SetMaterialDirty。"
                    : renderState
                    ? "这是材质级渲染状态。示例只创建一次材质实例，并在销毁时恢复原材质；不要用 MaterialPropertyBlock 设置。"
                    : "这是关键词驱动属性。示例只创建一次材质实例，并在销毁时恢复原材质；不要在 Update 中反复实例化材质。",
                isUi, help, true);
            return true;
        }

        private static void AddInfoBlock(VisualElement root, string title, string content)
        {
            var foldout = new Foldout { text = title, value = true };
            Color border = new Color(0.40f, 0.62f, 0.34f, 0.72f);
            foldout.style.marginBottom = 8f;
            foldout.style.paddingLeft = 8f;
            foldout.style.paddingRight = 8f;
            foldout.style.paddingTop = 5f;
            foldout.style.paddingBottom = 6f;
            foldout.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.11f, 0.14f, 0.12f, 0.92f)
                : new Color(0.91f, 0.95f, 0.90f, 0.96f);
            foldout.style.borderLeftWidth = 3f;
            foldout.style.borderLeftColor = border;
            ESEditorPresentation.ApplyCornerRadius(
                foldout,
                ESEditorPresentation.ESCornerRadiusToken.Control,
                ESEditorPresentation.ESCornerMask.TopLeft
                | ESEditorPresentation.ESCornerMask.BottomLeft);
            var label = new Label(content);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 6f;
            label.style.paddingRight = 6f;
            label.style.paddingTop = 4f;
            label.style.paddingBottom = 8f;
            foldout.Add(label);
            root.Add(foldout);
        }

        private static readonly Dictionary<string, string> CommonPropertyDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "_AddColor", "设置直接叠加到原颜色上的 HDR 颜色。" },
            { "_AddColorFade", "控制叠加颜色的混合强度，0 不叠加，1 完全按设定值叠加。" },
            { "_AlphaClip", "决定是否按裁剪阈值丢弃低透明度像素。" },
            { "_AlphaTint", "设置透明染色使用的目标颜色。" },
            { "_AlphaTintMin", "设置透明染色参与计算的最低透明度基准。" },
            { "_BurnEdgeColor", "设置燃烧或溶解交界处的 HDR 边缘颜色。" },
            { "_BurnInsideColor", "设置 2D 燃烧区域内部的颜色。" },
            { "_BurnProgress", "推进燃烧边界在噪声场中的位置。" },
            { "_BurnWidth", "控制燃烧高亮边缘的过渡宽度。" },
            { "_ChromaticAngle", "设置红蓝通道发生偏移的 UV 方向角。" },
            { "_ChromaticEdgeOnly", "控制色差是否集中在纹理边缘；0 为全图等量，1 为边缘增强。" },
            { "_ChromaticIntensity", "控制原颜色与 RGB 分离结果的混合比例。" },
            { "_ChromaticOffset", "设置红蓝通道相对原始 UV 的最大偏移距离。" },
            { "_Color", "设置与主纹理及顶点色相乘的基础颜色和透明度。" },
            { "_CoordinateMode", "选择效果坐标使用模型 UV、世界 XZ 或屏幕坐标。" },
            { "_CustomTime", "在自定义时间模式下提供由业务代码控制的时间值。" },
            { "_Cutoff", "设置透明裁剪阈值；透明度低于该值的像素会被丢弃。" },
            { "_DissolveColor", "设置 VFX 溶解边缘叠加的 HDR 颜色。" },
            { "_DissolveEdgeColor", "设置 Lit 或 2D 溶解边界的 HDR 颜色。" },
            { "_DissolveEdgeWidth", "控制 Lit 或 2D 溶解边界的可见宽度。" },
            { "_DissolveSoftness", "控制 Lit 溶解透明过渡的柔和程度。" },
            { "_DissolveWidth", "控制 VFX 溶解透明过渡及边缘区域的宽度。" },
            { "_EmissionColor", "设置不受场景光照衰减的 HDR 自发光颜色。" },
            { "_EmissionMap", "提供逐像素自发光纹理，并与自发光颜色相乘。" },
            { "_EnableAlphaTint", "决定是否按当前透明度向指定颜色染色。" },
            { "_EnablePingPongGlow", "决定是否在两种 HDR 颜色之间循环往返发光。" },
            { "_FadeNoiseFactor", "控制渐隐遮罩向噪声形状混合的比例。" },
            { "_FadePosition", "设置方向渐隐的坐标中心。" },
            { "_FadeWidth", "控制渐隐从可见到透明的过渡宽度。" },
            { "_FlowSpeed", "Vector4 的 XY 设置主纹理 UV 每秒流动的方向和速度。" },
            { "_FlowStrength", "缩放纹理流动速度，0 保持静止，1 使用完整速度。" },
            { "_FrozenColor", "设置冰冻状态覆盖主体的颜色。" },
            { "_FrozenDensity", "控制冰晶高光在噪声场中的出现比例。" },
            { "_FrozenHighlight", "设置冰晶闪烁部分叠加的 HDR 高光颜色。" },
            { "_FrozenSpeed", "控制冰晶高光随时间闪烁的速度。" },
            { "_GlitchAmount", "设置故障效果造成的最大横向 UV 偏移。" },
            { "_GlitchSpeed", "控制故障图样切换或移动的时间速度。" },
            { "_GlowFrequency", "控制往返发光每秒循环的角频率。" },
            { "_GlowFrom", "设置往返发光插值的起点 HDR 颜色。" },
            { "_GlowIntensity", "控制往返发光叠加到原颜色上的亮度。" },
            { "_GlowTo", "设置往返发光插值的终点 HDR 颜色。" },
            { "_HologramColor", "设置全息扫描线覆盖使用的 HDR 颜色。" },
            { "_HologramFrequency", "控制 VFX 或 UI 全息扫描线沿纵向的密度。" },
            { "_HologramGap", "控制 VFX 全息扫描线中不可见间隔的比例。" },
            { "_HologramLineFrequency", "控制 2D 全息扫描线沿纵向的密度。" },
            { "_HologramLineGap", "控制 2D 全息扫描线中不可见间隔的比例。" },
            { "_HologramMinAlpha", "设置全息扫描线间隔区域保留的最低透明度。" },
            { "_HologramSpeed", "控制全息扫描线沿坐标移动的速度。" },
            { "_InnerOutlineColor", "设置 2D 图形内部轮廓线的颜色。" },
            { "_InnerOutlineWidth", "设置内部轮廓采样相对纹理像素的宽度。" },
            { "_NegativeFade", "控制原颜色向负片颜色过渡的比例。" },
            { "_NoiseScale", "设置噪声采样坐标的缩放；Vector4 的后续分量可作为静态偏移。" },
            { "_NoiseSpeed", "Vector4 的 XY 控制噪声纹理随当前时间源移动的方向和速度。" },
            { "_NoiseTex", "提供扰动、溶解或状态效果使用的灰度噪声。" },
            { "_NormalScale", "控制法线纹理对最终光照法线的影响强度。" },
            { "_Occlusion", "控制环境遮挡纹理压低间接光的强度。" },
            { "_OcclusionMap", "使用绿色通道提供逐像素环境遮挡。" },
            { "_OuterOutlineColor", "设置扩展到原透明区域外部的轮廓颜色。" },
            { "_OuterOutlineWidth", "设置外部轮廓向周围纹理像素扩展的宽度。" },
            { "_PixelOutlineColor", "设置硬边像素轮廓的颜色。" },
            { "_PixelOutlineWidth", "设置硬边像素轮廓的纹理采样宽度。" },
            { "_PoisonColor", "设置中毒状态周期性叠加的颜色。" },
            { "_PoisonDensity", "控制中毒波纹在噪声场中的空间密度。" },
            { "_PoisonSpeed", "控制中毒颜色随时间脉动的速度。" },
            { "_RainbowBrightness", "控制彩虹渐变叠加后的亮度。" },
            { "_RainbowDensity", "控制彩虹色带沿坐标重复的密度。" },
            { "_RainbowSpeed", "控制彩虹色带随当前时间源移动的速度。" },
            { "_ReceiveShadows", "决定 Lit 材质是否应用主光源实时阴影衰减。" },
            { "_ReplaceFrom", "设置颜色替换要匹配的源颜色。" },
            { "_ReplaceRange", "设置源颜色可被匹配的颜色距离范围。" },
            { "_ReplaceSoftness", "控制颜色替换在匹配边界处的过渡柔和度。" },
            { "_ReplaceTo", "设置匹配成功后输出的目标颜色。" },
            { "_RimColor", "设置视角边缘叠加的 HDR 轮廓光颜色。" },
            { "_RimIntensity", "控制视角边缘光叠加到输出颜色的强度。" },
            { "_RimPower", "控制边缘光向轮廓集中的曲线；值越高，亮边越窄。" },
            { "_SequenceColumns", "设置序列帧图集的横向列数，Shader 至少按 1 列处理。" },
            { "_SequenceRows", "设置序列帧图集的纵向行数，Shader 至少按 1 行处理。" },
            { "_ShineAngle", "设置 2D 或 UI 扫光带在 UV 空间中的方向角。" },
            { "_ShineColor", "设置扫光带叠加的 HDR 颜色。" },
            { "_ShineDirection", "设置 Lit 扫光在世界表面投影使用的方向。" },
            { "_ShineIntensity", "控制扫光带叠加到输出颜色的亮度。" },
            { "_ShineSpeed", "控制扫光带沿指定方向移动的速度。" },
            { "_ShineWidth", "控制单条扫光带的可见宽度。" },
            { "_SparkleColor", "设置程序化闪点叠加的 HDR 颜色。" },
            { "_SparkleDensity", "控制可生成闪点的随机网格比例。" },
            { "_SparkleIntensity", "控制程序化闪点叠加到输出颜色的亮度。" },
            { "_SparkleScale", "控制闪点网格在 UV 空间中的密度。" },
            { "_SparkleSharpness", "控制闪点随时间和形状衰减的锐利程度。" },
            { "_SparkleSpeed", "控制闪点亮度随当前时间源闪烁的速度。" },
            { "_StrongTint", "设置强制染色使用的目标 HDR 颜色。" },
            { "_StrongTintFade", "控制原颜色向强制染色目标过渡的比例。" },
            { "_UseOcclusionMap", "决定是否读取环境遮挡纹理；关闭时不执行该纹理采样。" },
            { "_UseUIAlphaClip", "决定是否启用 Unity UI 的 0.001 固定阈值透明裁剪关键词。" },
            { "_VertexColorStrength", "控制顶点色参与最终颜色的比例；0 忽略顶点色，1 完整使用。" }
        };

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
            if (CommonPropertyDescriptions.TryGetValue(propertyName, out string description))
                return description;
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
                return "调节“" + displayName + "”的作用幅度；请结合材质预览确认边界值。";
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
            AddHelp(map, "ES/2D/Composite URP", "_EnableSparkle", "启用亮晶晶", "开启程序化闪点和闪烁高光。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_SparkleIntensity", "亮晶晶强度", "控制闪点叠加亮度。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableFlow", "启用纹理流动", "按时间推进主纹理 UV。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_FlowSpeed", "流动速度", "Vector2 的 XY 作为 UV 流动方向和速度。", "向量");
            AddHelp(map, "ES/2D/Composite URP", "_EnableChromatic", "启用色差", "通过 RGB 通道偏移产生轻量色差。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_ChromaticOffset", "色差偏移", "控制红蓝通道的 UV 偏移；会增加两次纹理采样。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_EnableBlur", "启用纹理模糊", "对主纹理执行轻量五点模糊，不读取屏幕背景。", "开关");
            AddHelp(map, "ES/2D/Composite URP", "_BlurRadius", "模糊半径", "控制五点采样的偏移范围。", "浮点/范围");
            AddHelp(map, "ES/2D/Composite URP", "_BlurIntensity", "模糊强度", "控制原图与模糊结果的混合比例。", "浮点/范围");
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
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveMode", "溶解模式", "选择噪声溶解或距离溶解算法；需要标准或高质量档。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_DissolveProgress", "溶解进度", "控制模型被溶解掉的归一化进度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableRim", "启用边缘光", "按视角边缘为模型增加轮廓光；需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_RimIntensity", "边缘光强度", "控制轮廓光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableShine", "启用扫光", "开启沿模型表面移动的扫光高光；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_ShineIntensity", "扫光强度", "控制扫光高光的叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableSparkle", "启用亮晶晶", "在高质量档位下开启程序化闪点。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFlow", "启用纹理流动", "沿顶点 UV 推进主纹理采样。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableChromatic", "启用色差", "对基础颜色纹理执行轻量 RGB 分离。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBlur", "启用纹理模糊", "对基础颜色纹理执行轻量五点模糊。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BlurRadius", "模糊半径", "控制 Lit 基础颜色纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_BlurIntensity", "模糊强度", "控制 Lit 基础颜色的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableBurn", "启用燃烧边缘", "开启溶解/燃烧交界处的边缘着色；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_AlphaClip", "启用透明裁剪", "按 Cutoff 阈值丢弃低透明度像素。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_QualityTier", "效果质量档位", "基础保留 Lit 主体；标准启用形变、流向、溶解和边缘光；高质量再启用扫光、闪点和燃烧边缘。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableVertexAnimation", "启用顶点动画", "在局部空间执行正弦顶点位移；标准/高质量档生效，并同步 Forward、ShadowCaster、DepthOnly 与 DepthNormals。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationDirection", "顶点动画局部方向", "XYZ 定义局部空间位移方向；零向量自动回退为局部 Y 轴。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationAmplitude", "顶点动画幅度", "控制顶点沿局部方向移动的最大距离。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationFrequency", "顶点动画频率", "控制波形在模型局部坐标中的疏密。", "浮点/范围");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationSpeed", "顶点动画速度", "控制正弦波随所选时间来源推进的速度。", "浮点");
            AddHelp(map, "ES/3D/Lit Composite URP", "_VertexAnimationMask", "顶点色动画遮罩", "选择顶点色通道限制形变；不使用遮罩时所有顶点等强度位移。", "强枚举");
            AddHelp(map, "ES/3D/Lit Composite URP", "_EnableFlowMap", "启用流向贴图", "用流向贴图 RG 通道扭曲主纹理 UV；标准/高质量档生效，并与透明裁剪的阴影和深度路径保持一致。", "开关");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMap", "流向贴图", "RG 通道按 0.5 为静止方向解码；建议关闭 sRGB 并使用可平铺纹理。", "纹理");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMapScale", "流向贴图缩放/偏移", "Vector4 的 XY 控制流向纹理缩放，ZW 控制静态偏移。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMapSpeed", "流向贴图速度", "Vector2 的 XY 控制流向纹理自身随时间移动的方向和速度。", "向量");
            AddHelp(map, "ES/3D/Lit Composite URP", "_FlowMapStrength", "流向贴图强度", "控制流向纹理对主纹理 UV 的最大偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_MainTex", "VFX 主纹理", "粒子或特效卡片的主采样纹理。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableSequence", "启用序列帧", "把主纹理按行列切分并选择当前帧；关闭时保持原始 UV。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequencePlayback", "序列帧播放方式", "选择手动帧、按当前时间源播放，或读取 ParticleSystem Custom1.z 作为帧号偏移。", "强枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceColumns", "序列帧列数", "设置图集横向帧数；Shader 会向下取整并保证至少为 1。", "浮点/整数语义");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceRows", "序列帧行数", "设置图集纵向帧数；帧顺序从左到右、从上到下。", "浮点/整数语义");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceFrame", "序列帧起始帧", "手动模式直接选择此帧；时间和顶点流模式以此值作为基础偏移。", "浮点/帧号");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SequenceSpeed", "序列帧速度", "按时间播放时每秒推进的帧数；允许负值反向播放。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnablePolarUV", "启用极坐标 UV", "把笛卡尔 UV 转换为角度与半径坐标；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarCenter", "极坐标中心", "设置极坐标转换的 UV 中心，通常使用 (0.5, 0.5)。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarRadialScale", "极坐标径向缩放", "缩放极坐标结果的半径轴；负值可反向径向流动。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarAngularScale", "极坐标角向缩放", "控制纹理沿圆周重复的次数和方向。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_PolarRotationSpeed", "极坐标旋转速度", "按当前时间源推进角度轴，生成旋转或涡流动画。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableVertexStreams", "启用粒子顶点流", "读取 ParticleSystem Renderer 的 Custom1.xyzw 与 Custom2.x；所有通道均为增量，零值不覆盖材质基础值。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamUVStrength", "Custom1 XY · UV 偏移", "缩放 Custom1.xy 对每粒子主纹理 UV 的偏移量。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamFrameStrength", "Custom1 Z · 帧号偏移", "缩放 Custom1.z 对序列帧基础帧号的增量。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamDissolveStrength", "Custom1 W · 溶解增量", "缩放 Custom1.w 并加到材质溶解进度，最终限制在 0 到 1。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexStreamEmissionStrength", "Custom2 X · 自发光增量", "把 Custom2.x 作为每粒子自发光倍率增量；零值保持材质自发光。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_NoiseTex", "VFX 噪声纹理", "驱动扰动、溶解和故障的噪声来源。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_Distortion", "扰动强度", "控制噪声对 VFX UV 的偏移量；非零值需要标准或高质量档。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFlow", "启用纹理流动", "按时间推进 VFX 主纹理 UV。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableShine", "启用扫光", "开启沿 VFX 卡片高度方向移动的扫光；需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableSparkle", "启用亮晶晶", "开启程序化闪点叠加；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableChromatic", "启用色差", "通过 RGB 通道偏移产生轻量色差；标准/高质量档执行，并增加两次主纹理采样。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableBlur", "启用纹理模糊", "对 VFX 主纹理执行轻量五点模糊；仅高质量档执行，并限制采样在当前序列帧内。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_BlurRadius", "模糊半径", "控制 VFX 主纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_BlurIntensity", "模糊强度", "控制 VFX 主纹理的柔化比例。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DissolveMode", "VFX 溶解模式", "选择普通溶解或带边缘光的溶解；需要标准或高质量档。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DissolveProgress", "VFX 溶解进度", "控制特效透明区域的推进位置。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableHologram", "VFX 全息开关", "为特效卡片叠加扫描线全息效果；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableGlitch", "VFX 故障开关", "为特效卡片增加随机横向故障偏移；仅高质量档执行。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_QualityTier", "VFX 效果质量档位", "基础保留序列帧、粒子流和径向遮罩；标准启用形变、噪声、溶解、视角与深度效果；高质量再启用模糊、闪点、全息和故障。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeMode", "时间来源", "选择场景时间、真正非缩放时间或由业务写入的自定义时间。", "枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_TimeScale", "时间倍率", "对当前时间来源统一乘倍率，各效果速度仍由各自参数控制。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_MainTexScaleOffset", "主纹理缩放/偏移", "用 Vector4 的 XY 设置缩放、ZW 设置 UV 偏移。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableVertexAnimation", "启用顶点动画", "在局部空间执行正弦顶点位移；标准/高质量档生效，适合网格特效和卡片摆动。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationDirection", "顶点动画局部方向", "XYZ 定义局部空间位移方向；零向量自动回退为局部 Y 轴。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationAmplitude", "顶点动画幅度", "控制顶点沿局部方向移动的最大距离。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationFrequency", "顶点动画频率", "控制波形在模型局部坐标中的疏密。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationSpeed", "顶点动画速度", "控制正弦波随所选时间来源推进的速度。", "浮点");
            AddHelp(map, "ES/3D/VFX Composite URP", "_VertexAnimationMask", "顶点色动画遮罩", "选择顶点色通道限制形变；不使用遮罩时所有顶点等强度位移。", "强枚举");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFlowMap", "启用流向贴图", "用流向贴图 RG 通道扭曲主纹理 UV；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMap", "流向贴图", "RG 通道按 0.5 为静止方向解码；建议关闭 sRGB 并使用可平铺纹理。", "纹理");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMapScale", "流向贴图缩放/偏移", "Vector4 的 XY 控制流向纹理缩放，ZW 控制静态偏移。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMapSpeed", "流向贴图速度", "Vector2 的 XY 控制流向纹理自身随时间移动的方向和速度。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FlowMapStrength", "流向贴图强度", "控制流向纹理对 VFX 主纹理 UV 的最大偏移。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableSoftParticles", "启用软粒子", "按相机深度柔化 VFX 与场景几何的交界；标准/高质量档生效，URP 必须开启 Depth Texture。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SoftParticleNear", "软粒子起始距离", "控制交界处从完全透明开始恢复的深度间隔。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_SoftParticleFar", "软粒子结束距离", "控制透明过渡结束的深度间隔，必须大于起始距离。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableRadialMask", "启用径向遮罩", "按原始主 UV 到指定中心的距离控制透明度；基础档可用。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskCenter", "径向遮罩中心", "设置圆形遮罩的 UV 中心。", "向量");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskRadius", "径向遮罩半径", "设置完整可见区域的半径。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskSoftness", "径向遮罩柔和度", "设置从可见到透明的过渡宽度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_RadialMaskInvert", "反转径向遮罩", "反转遮罩内外区域，制作圆环外扩或空心冲击波。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableFresnelMask", "启用菲涅尔遮罩", "按视线与表面法线夹角控制透明度和附加发光；标准/高质量档生效。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableRim", "启用边缘光", "按视线与表面法线夹角叠加边缘光；需要标准或高质量档。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelPower", "菲涅尔幂次", "控制菲涅尔从正面到边缘的曲线集中程度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelMin", "菲涅尔起点", "设置重映射的最低阈值；超过起点后开始显现。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelMax", "菲涅尔终点", "设置重映射的最高阈值；应高于起点。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelAlphaInfluence", "菲涅尔透明度影响", "0 只增加颜色，1 完全用菲涅尔遮罩乘透明度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelColor", "菲涅尔颜色", "设置视角边缘附加发光的 HDR 颜色。", "颜色");
            AddHelp(map, "ES/3D/VFX Composite URP", "_FresnelIntensity", "菲涅尔发光强度", "控制菲涅尔颜色叠加强度；设为 0 时只作为透明遮罩。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_EnableDepthIntersection", "启用深度交界发光", "在 VFX 接近场景几何时叠加交界光；与软粒子共用一次深度采样，要求 URP Depth Texture。", "开关");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DepthIntersectionColor", "深度交界颜色", "设置 VFX 与场景几何接触区域的 HDR 发光颜色。", "颜色");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DepthIntersectionDistance", "深度交界距离", "设置交界发光从接触面向外衰减的眼空间距离。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_DepthIntersectionIntensity", "深度交界强度", "控制交界发光叠加强度。", "浮点/范围");
            AddHelp(map, "ES/3D/VFX Composite URP", "_BlendMode", "混合模式", "选择透明、叠加、预乘透明或正片叠底；会同步底层 Blend 因子。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_ZWriteMode", "深度写入", "控制 Pass 是否写入深度；透明 VFX 通常关闭，封闭网格特效可按需开启。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_ZTest", "深度测试", "控制片元与相机深度缓冲的比较方式；常规 VFX 使用小于等于。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_Cull", "剔除模式", "选择双面、剔除正面或剔除背面；卡片粒子通常使用双面。", "强枚举/材质状态");
            AddHelp(map, "ES/3D/VFX Composite URP", "_QueueOffset", "渲染队列偏移", "在 Transparent 3000 基础上调整 -50 到 50，用于控制透明特效排序。", "整数/材质状态");
            AddHelp(map, "ES/UI/Composite URP", "_MainTex", "UI 主纹理", "由 CanvasRenderer 按对象提供；RawImage 使用 texture，Image 使用 sprite。", "纹理");
            AddHelp(map, "ES/UI/Composite URP", "_Color", "UI 颜色", "与 UI 顶点颜色和主纹理相乘的颜色。", "颜色");
            AddHelp(map, "ES/UI/Composite URP", "_EnableHologram", "UI 全息开关", "在 UI 上叠加动态扫描线。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableGlitch", "UI 故障开关", "在 UI 上叠加随机横向抖动。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableFlow", "UI 纹理流动", "按时间推进 UI 主纹理 UV。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableShine", "UI 扫光", "在 UI 表面叠加可控方向的扫光。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableSparkle", "UI 亮晶晶", "在 UI 上叠加程序化闪点。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableChromatic", "UI 色差", "对 UI 主纹理执行轻量 RGB 分离。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_EnableBlur", "UI 纹理模糊", "对 UI 主纹理执行轻量五点模糊，不等于背景毛玻璃。", "开关");
            AddHelp(map, "ES/UI/Composite URP", "_BlurRadius", "UI 模糊半径", "控制 UI 主纹理的采样偏移。", "浮点/范围");
            AddHelp(map, "ES/UI/Composite URP", "_BlurIntensity", "UI 模糊强度", "控制 UI 主纹理的柔化比例。", "浮点/范围");
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

        private static string BuildFocusedExample(bool isUi, bool usesMaterialInstance, string declaration, string call)
        {
            string targetType = isUi || usesMaterialInstance ? "Material" : "MaterialPropertyBlock";
            string targetName = isUi || usesMaterialInstance ? "materialInstance" : "propertyBlock";
            string methodName = isUi || usesMaterialInstance ? "应用到独立材质" : "写入参数块";
            string valueParameter = BuildValueParameter(declaration);
            return "using UnityEngine;\nusing ES;\n\n"
                + "public static class ESShader参数示例\n"
                + "{\n"
                + "    // " + targetName + " 由调用方传入；这里只负责当前属性，不获取 Renderer，也不回写参数块。\n"
                + "    public static void " + methodName + "(" + targetType + " " + targetName + ", " + valueParameter + ")\n"
                + "    {\n"
                + "        if (" + targetName + " == null) return;\n"
                + "        " + call + "\n"
                + "    }\n"
                + "}";
        }

        private static string BuildValueParameter(string declaration)
        {
            string value = declaration == null ? "float 属性值" : declaration.Trim();
            while (value.StartsWith("[", StringComparison.Ordinal))
            {
                int attributeEnd = value.IndexOf(']');
                if (attributeEnd < 0) break;
                value = value.Substring(attributeEnd + 1).TrimStart();
            }
            if (value.StartsWith("public ", StringComparison.Ordinal))
                value = value.Substring("public ".Length);
            int equals = value.IndexOf('=');
            if (equals >= 0) value = value.Substring(0, equals);
            return value.Trim().TrimEnd(';');
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
                    if (propertyName == "_VertexAnimationMask")
                    {
                        declaration = "public ESCompositeVertexColorMask 顶点色动画遮罩 = ESCompositeVertexColorMask.无;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.VertexAnimationMask, (float)顶点色动画遮罩);";
                        return true;
                    }
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES3DLitCompositeURPProperties.TimeMode, (float)时间来源);";
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
                    if (propertyName == "_SequencePlayback")
                    {
                        declaration = "public ES3DVFXSequencePlaybackMode 播放方式 = ES3DVFXSequencePlaybackMode.时间播放;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.SequencePlayback, (float)播放方式);";
                        return true;
                    }
                    if (propertyName == "_VertexAnimationMask")
                    {
                        declaration = "public ESCompositeVertexColorMask 顶点色动画遮罩 = ESCompositeVertexColorMask.无;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.VertexAnimationMask, (float)顶点色动画遮罩);";
                        return true;
                    }
                    if (propertyName == "_TimeMode")
                    {
                        declaration = "public ESCompositeTimeMode 时间来源 = ESCompositeTimeMode.场景时间;";
                        assignment = ".SetFloat(ES3DVFXCompositeURPProperties.TimeMode, (float)时间来源);";
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
                bool motionProperty = propertyName == "_EnableSparkle" || propertyName == "_SparkleColor" || propertyName == "_SparkleScale" || propertyName == "_SparkleSpeed" || propertyName == "_SparkleDensity" || propertyName == "_SparkleSharpness" || propertyName == "_SparkleIntensity"
                    || propertyName == "_EnableFlow" || propertyName == "_FlowSpeed" || propertyName == "_FlowStrength"
                    || propertyName == "_EnableFlowMap" || propertyName.StartsWith("_FlowMap", StringComparison.Ordinal)
                    || propertyName == "_EnableVertexAnimation" || propertyName.StartsWith("_VertexAnimation", StringComparison.Ordinal)
                    || propertyName == "_EnableSoftParticles" || propertyName.StartsWith("_SoftParticle", StringComparison.Ordinal)
                    || propertyName == "_EnableSequence" || propertyName.StartsWith("_Sequence", StringComparison.Ordinal)
                    || propertyName == "_EnablePolarUV" || propertyName.StartsWith("_Polar", StringComparison.Ordinal)
                    || propertyName == "_EnableVertexStreams" || propertyName.StartsWith("_VertexStream", StringComparison.Ordinal)
                    || propertyName == "_EnableRadialMask" || propertyName.StartsWith("_RadialMask", StringComparison.Ordinal)
                    || propertyName == "_EnableFresnelMask" || propertyName.StartsWith("_Fresnel", StringComparison.Ordinal)
                    || propertyName == "_EnableDepthIntersection" || propertyName.StartsWith("_DepthIntersection", StringComparison.Ordinal)
                    || propertyName == "_EnableChromatic" || propertyName == "_ChromaticOffset" || propertyName == "_ChromaticIntensity" || propertyName == "_ChromaticEdgeOnly" || propertyName == "_ChromaticAngle"
                    || propertyName == "_EnableBlur" || propertyName == "_BlurRadius" || propertyName == "_BlurIntensity"
                    || propertyName == "_EnableShine" || propertyName == "_ShineColor" || propertyName == "_ShineSpeed" || propertyName == "_ShineWidth" || propertyName == "_ShineIntensity" || propertyName == "_ShineAngle" || propertyName == "_ShineDirection";
                if (motionProperty)
                {
                    if (shader.name == "ES/2D/Composite URP") return "ES2DCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/3D/Lit Composite URP") return "ES3DLitCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/3D/VFX Composite URP") return "ES3DVFXCompositeURPProperties." + ToPascal(propertyName);
                    if (shader.name == "ES/UI/Composite URP") return "ESUICompositeURPProperties." + ToPascal(propertyName);
                }
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
                case "EnableShine": return "ShineEnabled";
                case "EnableSparkle": return "SparkleEnabled";
                case "EnableFlow": return "FlowEnabled";
                case "EnableFlowMap": return "FlowMapEnabled";
                case "EnableVertexAnimation": return "VertexAnimationEnabled";
                case "EnableSoftParticles": return "SoftParticlesEnabled";
                case "EnableSequence": return "SequenceEnabled";
                case "EnablePolarUV": return "PolarUVEnabled";
                case "EnableVertexStreams": return "VertexStreamsEnabled";
                case "EnableRadialMask": return "RadialMaskEnabled";
                case "EnableFresnelMask": return "FresnelMaskEnabled";
                case "EnableDepthIntersection": return "DepthIntersectionEnabled";
                case "EnableChromatic": return "ChromaticEnabled";
                case "EnableBlur": return "BlurEnabled";
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
            internal readonly string FocusedExample;
            internal readonly string FullExample;
            internal readonly string Note;
            internal readonly bool IsUi;
            internal readonly bool UsesMaterialInstance;
            internal readonly string Description;
            internal readonly string TypeLabel;
            internal readonly string TargetLabel;
            internal readonly string WriteMode;
            internal readonly string RecommendedUsage;

            internal CodeSet(string propertyCall, string focusedExample, string fullExample, string note, bool isUi, PropertyHelp help, bool usesMaterialInstance = false)
            {
                PropertyCall = propertyCall;
                FocusedExample = focusedExample;
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
