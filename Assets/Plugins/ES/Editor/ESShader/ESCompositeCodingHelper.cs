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
    internal static partial class ESCompositeCodingHelper
    {
        #region Control Metadata

        private const string DialogIdPrefix = "es.shader.composite.coding.";
        private static readonly GUIContent CodeButton = new GUIContent("C#", "打开此属性的完整 C# 示例");
        private static readonly GUIContent ResetButton = new GUIContent("R", "恢复 Shader 默认值");
        private static readonly Dictionary<string, GUIContent> LabelCache = new Dictionary<string, GUIContent>(StringComparer.Ordinal);
        private static readonly string[] CoordinateModeOptions = { "UV", "世界空间", "屏幕空间" };
        private static readonly string[] TimeModeOptions = { "场景时间", "非缩放时间", "自定义时间" };
        private static readonly string[] AnimationModeOptions = { "关闭", "序列帧" };
        private static readonly string[] FadeModeOptions =
        {
            "关闭",
            "方向透明渐隐",
            "纹理遮罩",
            "全局透明溶解",
            "方向发光渐隐",
            "方向扰动",
            "源点透明溶解",
            "源点发光溶解"
        };
        private static readonly string[] LitDissolveModeOptions = { "关闭", "噪声溶解", "距离溶解" };
        private static readonly string[] VfxDissolveModeOptions = { "关闭", "溶解", "溶解带边缘" };
        private static readonly string[] QualityTierOptions = { "基础", "标准", "高质量" };
        private static readonly string[] VertexColorMaskOptions = { "不使用遮罩", "红色通道", "绿色通道", "蓝色通道", "透明通道" };
        private static readonly string[] VfxSequencePlaybackOptions = { "手动帧", "按时间播放", "Custom1 Z 帧号" };
        private static readonly string[] VfxBlendModeOptions = { "透明混合", "叠加", "预乘透明", "正片叠底" };
        private static readonly string[] VfxDepthWriteOptions = { "关闭", "开启" };
        private static readonly string[] VfxDepthTestOptions = { "禁用", "从不", "小于", "等于", "小于等于", "大于", "不等于", "大于等于", "始终" };
        private static readonly string[] VfxCullOptions = { "双面", "剔除正面", "剔除背面" };

        #endregion

        #region Inspector And Dialog UI

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
                        ? GUIUtility.GUIToScreenPoint(new Vector2(codeButtonRect.xMin, codeButtonRect.yMin))
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
                        ? GUIUtility.GUIToScreenPoint(new Vector2(codeButtonRect.xMin, codeButtonRect.yMin))
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
            if (propertyName == "_FadeMode"
                && (shaderName == "ES/2D/Composite URP"
                    || shaderName == "ES/3D/Lit Composite URP"
                    || shaderName == "ES/UI/Composite URP"))
            {
                options = FadeModeOptions;
                return true;
            }

            if (shaderName == "ES/2D/Composite URP")
            {
                switch (propertyName)
                {
                    case "_CoordinateMode": options = CoordinateModeOptions; return true;
                    case "_AnimationMode": options = AnimationModeOptions; return true;
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
                // Material inspectors do not expose a stable owning
                // EditorWindow through this drawer callback. Leave the owner
                // unset rather than guessing from mouse/focus state.
                owner = null,
                allowMainWorkspaceFallback = true,
            };
            if (clickPosition.HasValue)
            {
                request.positionMode = ESAdvancedDialogPositionMode.CustomScreenPosition;
                request.customScreenPosition = CalculateCodeDialogTopLeft(
                    clickPosition.Value,
                    request.preferredSize);
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

        internal static Vector2 CalculateCodeDialogTopLeft(
            Vector2 buttonScreenTopLeft,
            Vector2 dialogSize)
        {
            // Material Inspector 通常停靠在屏幕右侧。这里直接计算最终窗口左上角：
            // 窗口完整位于按钮左侧，并略微高于按钮；工作区钳制只负责防止越屏。
            return new Vector2(
                buttonScreenTopLeft.x - dialogSize.x - 14f,
                buttonScreenTopLeft.y - 14f);
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

        #endregion
    }
}
