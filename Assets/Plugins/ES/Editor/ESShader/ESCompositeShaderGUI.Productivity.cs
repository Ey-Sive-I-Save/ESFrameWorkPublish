using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        #region View And Preset Model

        private enum InspectorViewLevel
        {
            Standard = 0,
            Advanced = 1,
            Expert = 2
        }

        private enum PresetValueKind
        {
            Float,
            Color,
            Vector
        }
        private sealed class PresetAssignment
        {
            internal readonly string PropertyName;
            internal readonly PresetValueKind Kind;
            internal readonly Vector4 Value;

            internal PresetAssignment(string propertyName, float value)
            {
                PropertyName = propertyName;
                Kind = PresetValueKind.Float;
                Value = new Vector4(value, 0f, 0f, 0f);
            }

            internal PresetAssignment(string propertyName, Color value)
            {
                PropertyName = propertyName;
                Kind = PresetValueKind.Color;
                Value = value;
            }

            internal PresetAssignment(string propertyName, Vector4 value)
            {
                PropertyName = propertyName;
                Kind = PresetValueKind.Vector;
                Value = value;
            }

            internal bool IsDifferent(Material material)
            {
                if (material == null || !material.HasProperty(PropertyName)) return false;
                switch (Kind)
                {
                    case PresetValueKind.Color:
                        return !Approximately(material.GetColor(PropertyName), (Color)Value);
                    case PresetValueKind.Vector:
                        return !Approximately(material.GetVector(PropertyName), Value);
                    default:
                        return !Mathf.Approximately(material.GetFloat(PropertyName), Value.x);
                }
            }

            internal void Apply(Material material)
            {
                if (material == null || !material.HasProperty(PropertyName)) return;
                switch (Kind)
                {
                    case PresetValueKind.Color:
                        material.SetColor(PropertyName, (Color)Value);
                        break;
                    case PresetValueKind.Vector:
                        material.SetVector(PropertyName, Value);
                        break;
                    default:
                        material.SetFloat(PropertyName, Value.x);
                        break;
                }
            }

            internal string FormatTarget()
            {
                switch (Kind)
                {
                    case PresetValueKind.Color:
                        Color color = Value;
                        return string.Format("RGBA({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", color.r, color.g, color.b, color.a);
                    case PresetValueKind.Vector:
                        return string.Format("({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", Value.x, Value.y, Value.z, Value.w);
                    default:
                        return Value.x.ToString("0.###");
                }
            }
        }

        private sealed class CompositePreset
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly string Description;
            internal readonly string ShaderName;
            internal readonly PresetAssignment[] Assignments;

            internal CompositePreset(string id, string name, string description, string shaderName, params PresetAssignment[] assignments)
            {
                Id = id;
                Name = name;
                Description = description;
                ShaderName = shaderName;
                Assignments = assignments ?? Array.Empty<PresetAssignment>();
            }
        }

        private static readonly string[] ViewModeNames = { "标准", "进阶", "高级" };
        private static readonly GUIContent ClearFilterButtonContent = new GUIContent("清除筛选", "清除当前搜索与快捷分类筛选");
        private static readonly GUIContent SelectPresetDifferencesButtonContent = new GUIContent("全选差异");
        private static readonly GUIContent CancelPresetSelectionButtonContent = new GUIContent("全部取消");
        private static readonly GUIContent ApplyPresetSelectionButtonContent = new GUIContent("应用所选");
        // 显示级别是明确的编辑器元数据，不根据名称片段推断，避免新增属性被意外藏起来。
        // 未列出的属性保持“标准”可见；只有确实需要背景知识或较高成本的入口才提升级别。
        private static readonly HashSet<string> AdvancedViewProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_EnableSequence", "_SequencePlayback", "_SequenceColumns", "_SequenceRows", "_SequenceFrame", "_SequenceSpeed",
            "_EnablePolarUV", "_PolarCenter", "_PolarRadialScale", "_PolarAngularScale", "_PolarRotationSpeed",
            "_EnableFlowMap", "_FlowMap", "_FlowMapScale", "_FlowMapSpeed", "_FlowMapStrength",
            "_EnableVertexAnimation", "_VertexAnimationDirection", "_VertexAnimationAmplitude", "_VertexAnimationFrequency", "_VertexAnimationSpeed", "_VertexAnimationMask",
            "_EnableChromatic", "_ChromaticOffset", "_ChromaticIntensity", "_ChromaticEdgeOnly", "_ChromaticAngle",
            "_EnableRadialMask", "_RadialMaskCenter", "_RadialMaskRadius", "_RadialMaskSoftness", "_RadialMaskInvert",
            "_EnableFresnelMask", "_FresnelPower", "_FresnelMin", "_FresnelMax", "_FresnelAlphaInfluence", "_FresnelColor", "_FresnelIntensity",
            "_EnableSoftParticles", "_SoftParticleNear", "_SoftParticleFar",
            "_EnableDepthIntersection", "_DepthIntersectionColor", "_DepthIntersectionDistance", "_DepthIntersectionIntensity",
            "_StencilComp", "_Stencil", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask"
        };

        private static readonly HashSet<string> ExpertViewProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "_EnableVertexStreams", "_VertexStreamUVStrength", "_VertexStreamFrameStrength", "_VertexStreamDissolveStrength", "_VertexStreamEmissionStrength",
            "_EnableBlur", "_BlurRadius", "_BlurIntensity",
            "_EnableSparkle", "_SparkleColor", "_SparkleScale", "_SparkleSpeed", "_SparkleDensity", "_SparkleSharpness", "_SparkleIntensity",
            "_EnableHologram", "_HologramColor", "_HologramFrequency", "_HologramLineFrequency", "_HologramGap", "_HologramLineGap", "_HologramSpeed", "_HologramMinAlpha",
            "_EnableGlitch", "_GlitchAmount", "_GlitchIntensity", "_GlitchSpeed",
            "_BlendMode", "_ZWriteMode", "_ZTest", "_Cull", "_QueueOffset"
        };

        private static readonly CompositePreset[] BuiltInPresets =
        {
            new CompositePreset(
                "2d.shine", "2D 扫光强调", "用一条可控高光带突出按钮、卡牌和拾取物。", "ES/2D/Composite URP",
                new PresetAssignment("_EnableShine", 1f),
                new PresetAssignment("_ShineColor", new Color(1.8f, 1.5f, 0.65f, 1f)),
                new PresetAssignment("_ShineSpeed", 1.2f),
                new PresetAssignment("_ShineWidth", 0.14f),
                new PresetAssignment("_ShineIntensity", 1.35f)),
            new CompositePreset(
                "2d.dissolve", "2D 噪声消散", "以噪声边缘消散图片，进度可继续交给动画或业务代码。", "ES/2D/Composite URP",
                new PresetAssignment("_FadeMode", 3f),
                new PresetAssignment("_FadeProgress", 0.45f),
                new PresetAssignment("_FadeWidth", 0.09f),
                new PresetAssignment("_FadeNoiseFactor", 0.25f),
                new PresetAssignment("_DissolveEdgeColor", new Color(2.2f, 0.18f, 0.02f, 1f))),
            new CompositePreset(
                "lit.rim", "Lit 轮廓强调", "保持 URP Lit 光照，并用冷色边缘光强化角色或交互物轮廓。", "ES/3D/Lit Composite URP",
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.05f, 0.55f, 1.7f, 1f)),
                new PresetAssignment("_RimPower", 3.2f),
                new PresetAssignment("_RimIntensity", 1.5f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "lit.burn", "Lit 燃烧溶解", "以高亮燃烧边缘推进溶解，适合受击、死亡和场景转化。", "ES/3D/Lit Composite URP",
                new PresetAssignment("_DissolveMode", 1f),
                new PresetAssignment("_DissolveProgress", 0.4f),
                new PresetAssignment("_DissolveSoftness", 0.06f),
                new PresetAssignment("_EnableBurn", 1f),
                new PresetAssignment("_BurnProgress", 0.4f),
                new PresetAssignment("_BurnWidth", 0.12f),
                new PresetAssignment("_BurnEdgeColor", new Color(2.5f, 0.2f, 0.01f, 1f)),
                new PresetAssignment("_QualityTier", 2f)),
            new CompositePreset(
                "ui.shine", "UI 扫光反馈", "轻量扫光用于按钮确认、奖励展示和卡牌强调。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableShine", 1f),
                new PresetAssignment("_ShineColor", new Color(1.7f, 1.45f, 0.7f, 1f)),
                new PresetAssignment("_ShineSpeed", 1.1f),
                new PresetAssignment("_ShineWidth", 0.13f),
                new PresetAssignment("_ShineIntensity", 1.25f)),
            new CompositePreset(
                "ui.hologram", "UI 全息故障", "扫描线与轻微故障组合，用于终端、投影和科技感界面。", "ES/UI/Composite URP",
                new PresetAssignment("_EnableHologram", 1f),
                new PresetAssignment("_HologramColor", new Color(0.05f, 0.9f, 1.8f, 1f)),
                new PresetAssignment("_HologramFrequency", 64f),
                new PresetAssignment("_HologramSpeed", 1.25f),
                new PresetAssignment("_EnableGlitch", 1f),
                new PresetAssignment("_GlitchAmount", 0.018f)),
            new CompositePreset(
                "vfx.energy-flow", "能量流动", "沿 UV 持续流动并叠加青色边缘光，适合能量管线与技能轨迹。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnableFlow", 1f),
                new PresetAssignment("_FlowSpeed", new Vector4(0f, -0.65f, 0f, 0f)),
                new PresetAssignment("_FlowStrength", 0.85f),
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.05f, 0.75f, 1.8f, 1f)),
                new PresetAssignment("_RimPower", 2.5f),
                new PresetAssignment("_RimIntensity", 1.8f),
                new PresetAssignment("_EmissionColor", new Color(0.02f, 0.35f, 0.8f, 1f)),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.shockwave", "冲击波", "把纹理转换为极坐标并使用径向遮罩塑造扩散圆环。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnablePolarUV", 1f),
                new PresetAssignment("_PolarCenter", new Vector4(0.5f, 0.5f, 0f, 0f)),
                new PresetAssignment("_PolarRadialScale", 1.2f),
                new PresetAssignment("_PolarAngularScale", 1f),
                new PresetAssignment("_EnableRadialMask", 1f),
                new PresetAssignment("_RadialMaskCenter", new Vector4(0.5f, 0.5f, 0f, 0f)),
                new PresetAssignment("_RadialMaskRadius", 0.55f),
                new PresetAssignment("_RadialMaskSoftness", 0.08f),
                new PresetAssignment("_BlendMode", 1f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.portal", "传送门", "旋转极坐标、流动和高亮边缘组合，适合门扉与空间裂隙。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnablePolarUV", 1f),
                new PresetAssignment("_PolarRotationSpeed", 0.18f),
                new PresetAssignment("_EnableFlow", 1f),
                new PresetAssignment("_FlowSpeed", new Vector4(0.15f, -0.45f, 0f, 0f)),
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.35f, 0.05f, 2.1f, 1f)),
                new PresetAssignment("_RimIntensity", 2.4f),
                new PresetAssignment("_EnableFresnelMask", 1f),
                new PresetAssignment("_FresnelPower", 1.7f),
                new PresetAssignment("_FresnelIntensity", 1.2f),
                new PresetAssignment("_BlendMode", 1f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.shield", "护盾边缘", "用菲涅尔与深度交界强调护盾轮廓和接触区域。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnableRim", 1f),
                new PresetAssignment("_RimColor", new Color(0.05f, 0.85f, 2.2f, 1f)),
                new PresetAssignment("_RimPower", 3.8f),
                new PresetAssignment("_RimIntensity", 2.1f),
                new PresetAssignment("_EnableFresnelMask", 1f),
                new PresetAssignment("_FresnelPower", 2.8f),
                new PresetAssignment("_FresnelAlphaInfluence", 0.85f),
                new PresetAssignment("_EnableDepthIntersection", 1f),
                new PresetAssignment("_DepthIntersectionColor", new Color(0.1f, 0.9f, 2.4f, 1f)),
                new PresetAssignment("_DepthIntersectionDistance", 0.18f),
                new PresetAssignment("_DepthIntersectionIntensity", 2f),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.dissolve", "溶解消散", "带高亮边缘的噪声溶解，保留进度给动画或业务代码控制。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_DissolveMode", 2f),
                new PresetAssignment("_DissolveProgress", 0.45f),
                new PresetAssignment("_DissolveWidth", 0.09f),
                new PresetAssignment("_DissolveColor", new Color(2.4f, 0.22f, 0.015f, 1f)),
                new PresetAssignment("_QualityTier", 1f)),
            new CompositePreset(
                "vfx.hologram", "全息故障", "高质量扫描线、轻微故障与色差组合，适合投影和数字替身。", "ES/3D/VFX Composite URP",
                new PresetAssignment("_EnableHologram", 1f),
                new PresetAssignment("_HologramColor", new Color(0.05f, 1.1f, 2.2f, 1f)),
                new PresetAssignment("_HologramFrequency", 72f),
                new PresetAssignment("_HologramGap", 0.32f),
                new PresetAssignment("_HologramSpeed", 1.4f),
                new PresetAssignment("_EnableGlitch", 1f),
                new PresetAssignment("_GlitchAmount", 0.018f),
                new PresetAssignment("_GlitchSpeed", 3.5f),
                new PresetAssignment("_EnableChromatic", 1f),
                new PresetAssignment("_ChromaticOffset", 0.0025f),
                new PresetAssignment("_ChromaticIntensity", 0.7f),
                new PresetAssignment("_BlendMode", 1f),
                new PresetAssignment("_QualityTier", 2f))
        };

        private static readonly Dictionary<string, CompositePreset[]> PresetCache = new Dictionary<string, CompositePreset[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string[]> PresetNameCache = new Dictionary<string, string[]>(StringComparer.Ordinal);
        #endregion

        #region View And Preset Workflow

        private static InspectorViewLevel DrawInspectorViewMode(string shaderName)
        {
            string key = "ES.Composite.ViewLevel." + shaderName;
            InspectorViewLevel current = (InspectorViewLevel)Mathf.Clamp(SessionState.GetInt(key, 0), 0, 2);

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("显示范围", ESEditorPresentation.HeaderStyle, GUILayout.Width(72f));
            int next = GUILayout.Toolbar((int)current, ViewModeNames, EditorStyles.miniButton);
            EditorGUILayout.EndHorizontal();
            if (next != (int)current)
            {
                current = (InspectorViewLevel)next;
                SessionState.SetInt(key, next);
            }

            string guidance = current == InspectorViewLevel.Standard
                ? "常用参数"
                : current == InspectorViewLevel.Advanced
                    ? "坐标、遮罩与深度"
                    : "顶点流与渲染状态";
            GUILayout.Label(guidance + " · 仅改变显示，不修改材质", ESEditorPresentation.SubtitleStyle);
            return current;
        }

        private static bool PropertyPassesViewLevel(MaterialProperty property, string filter, InspectorViewLevel level)
        {
            if (property == null) return false;
            // 主动搜索或效果导航等同于用户明确寻找目标，临时越级展示但不改变当前模式。
            if (!string.IsNullOrEmpty(filter)) return true;
            return ResolveInspectorViewLevel(property.name) <= level;
        }

        private static InspectorViewLevel ResolveInspectorViewLevel(string propertyName)
        {
            if (ExpertViewProperties.Contains(propertyName)) return InspectorViewLevel.Expert;
            if (AdvancedViewProperties.Contains(propertyName)) return InspectorViewLevel.Advanced;
            return InspectorViewLevel.Standard;
        }

        private static int GetMaterialPropertyValueSignature(MaterialProperty[] properties)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < properties.Length; i++)
                {
                    MaterialProperty property = properties[i];
                    if (property == null) continue;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(property.name);
                    hash = hash * 31 + (property.hasMixedValue ? 1 : 0);
                    switch (property.type)
                    {
                        case MaterialProperty.PropType.Color:
                            hash = hash * 31 + property.colorValue.GetHashCode();
                            break;
                        case MaterialProperty.PropType.Vector:
                            hash = hash * 31 + property.vectorValue.GetHashCode();
                            break;
                        case MaterialProperty.PropType.Texture:
                            hash = hash * 31 + (property.textureValue == null ? 0 : property.textureValue.GetInstanceID());
                            hash = hash * 31 + property.textureScaleAndOffset.GetHashCode();
                            break;
                        default:
                            hash = hash * 31 + property.floatValue.GetHashCode();
                            break;
                    }
                }
                return hash;
            }
        }

        private static void DrawPresetPanel(MaterialEditor editor, MaterialProperty[] properties, string shaderName)
        {
            CompositePreset[] presets = GetPresets(shaderName);
            if (presets.Length == 0) return;

            string selectedKey = "ES.Composite.Preset.Selected." + shaderName;
            int selected = Mathf.Clamp(SessionState.GetInt(selectedKey, 0), 0, presets.Length - 1);
            string[] names = GetPresetNames(shaderName, presets);
            CompositePreset preset = presets[selected];
            string panelKey = "ES.Composite.Preset.Panel." + shaderName;
            bool expanded = SessionState.GetBool(panelKey, false);

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, "效果预设 · " + preset.Name, true);
            if (nextExpanded != expanded)
            {
                expanded = nextExpanded;
                SessionState.SetBool(panelKey, expanded);
            }
            if (!expanded)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("选择预设", ESEditorPresentation.HeaderStyle, GUILayout.MinWidth(56f), GUILayout.ExpandWidth(false));
            int next = EditorGUILayout.Popup(selected, names);
            EditorGUILayout.EndHorizontal();
            if (next != selected)
            {
                selected = next;
                SessionState.SetInt(selectedKey, selected);
            }

            preset = presets[selected];
            GUILayout.Label(preset.Description, ESEditorPresentation.SubtitleStyle);
            string foldoutKey = "ES.Composite.Preset.Preview." + shaderName;
            bool previewExpanded = SessionState.GetBool(foldoutKey, false);
            bool nextPreviewExpanded = EditorGUILayout.Foldout(previewExpanded, "预览并选择要应用的差异", true);
            if (nextPreviewExpanded != previewExpanded)
            {
                previewExpanded = nextPreviewExpanded;
                SessionState.SetBool(foldoutKey, previewExpanded);
            }

            int differenceCount = 0;
            int selectedCount = 0;
            if (previewExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (DrawContentSizedButton(SelectPresetDifferencesButtonContent, EditorStyles.miniButton))
                    SetPresetSelections(editor, preset, true, true);
                if (DrawContentSizedButton(CancelPresetSelectionButtonContent, EditorStyles.miniButton))
                    SetPresetSelections(editor, preset, false, false);
                EditorGUILayout.EndHorizontal();

                bool narrowComparison = EditorGUIUtility.currentViewWidth < 330f;
                for (int i = 0; i < preset.Assignments.Length; i++)
                {
                    PresetAssignment assignment = preset.Assignments[i];
                    bool different = IsDifferentForAnyTarget(editor, assignment);
                    if (different) differenceCount++;
                    string selectionKey = GetPresetSelectionKey(shaderName, preset.Id, assignment.PropertyName);
                    bool apply = different && SessionState.GetBool(selectionKey, true);
                    string displayName = GetPresetPropertyDisplayName(properties, assignment.PropertyName);
                    string comparison = FormatCurrentValue(editor, assignment) + "  →  " + assignment.FormatTarget();
                    bool nextApply;
                    if (narrowComparison)
                    {
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        nextApply = EditorGUILayout.Toggle(apply, GUILayout.Width(18f));
                        GUILayout.Label(displayName);
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Label(different ? comparison : "已一致", ESEditorPresentation.SubtitleStyle);
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        nextApply = EditorGUILayout.Toggle(apply, GUILayout.Width(18f));
                        GUILayout.Label(displayName, GUILayout.Width(Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.28f, 92f, 180f)));
                        GUILayout.Label(different ? comparison : "已一致", different ? EditorStyles.miniLabel : ESEditorPresentation.MetaStyle);
                        EditorGUILayout.EndHorizontal();
                    }
                    if (nextApply != apply)
                    {
                        apply = nextApply;
                        SessionState.SetBool(selectionKey, apply);
                    }
                    if (apply) selectedCount++;
                }
            }
            else
            {
                for (int i = 0; i < preset.Assignments.Length; i++)
                {
                    PresetAssignment assignment = preset.Assignments[i];
                    bool different = IsDifferentForAnyTarget(editor, assignment);
                    if (different) differenceCount++;
                    if (different && SessionState.GetBool(GetPresetSelectionKey(shaderName, preset.Id, assignment.PropertyName), true)) selectedCount++;
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("差异 " + differenceCount + " 项 · 已选择 " + selectedCount + " 项", ESEditorPresentation.MetaStyle);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (DrawContentSizedButton(ApplyPresetSelectionButtonContent))
                    ApplyPreset(editor, preset);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static CompositePreset[] GetPresets(string shaderName)
        {
            if (PresetCache.TryGetValue(shaderName, out CompositePreset[] cached)) return cached;
            var result = new List<CompositePreset>();
            for (int i = 0; i < BuiltInPresets.Length; i++)
                if (string.Equals(BuiltInPresets[i].ShaderName, shaderName, StringComparison.Ordinal))
                    result.Add(BuiltInPresets[i]);
            CompositePreset[] presets = result.ToArray();
            PresetCache[shaderName] = presets;
            return presets;
        }

        private static string[] GetPresetNames(string shaderName, CompositePreset[] presets)
        {
            if (PresetNameCache.TryGetValue(shaderName, out string[] cached)) return cached;
            string[] names = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++) names[i] = presets[i].Name;
            PresetNameCache[shaderName] = names;
            return names;
        }

        private static string GetPresetSelectionKey(string shaderName, string presetId, string propertyName)
        {
            return "ES.Composite.Preset.Apply." + shaderName + "." + presetId + "." + propertyName;
        }

        private static void SetPresetSelections(MaterialEditor editor, CompositePreset preset, bool selected, bool differencesOnly)
        {
            for (int i = 0; i < preset.Assignments.Length; i++)
            {
                PresetAssignment assignment = preset.Assignments[i];
                bool value = selected && (!differencesOnly || IsDifferentForAnyTarget(editor, assignment));
                SessionState.SetBool(GetPresetSelectionKey(preset.ShaderName, preset.Id, assignment.PropertyName), value);
            }
        }

        private static bool IsDifferentForAnyTarget(MaterialEditor editor, PresetAssignment assignment)
        {
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material != null && material.HasProperty(assignment.PropertyName) && assignment.IsDifferent(material)) return true;
            }
            return false;
        }

        private static string FormatCurrentValue(MaterialEditor editor, PresetAssignment assignment)
        {
            Material first = null;
            bool mixed = false;
            string firstValue = null;
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || !material.HasProperty(assignment.PropertyName)) continue;
                string value = FormatMaterialValue(material, assignment);
                if (first == null)
                {
                    first = material;
                    firstValue = value;
                }
                else if (!string.Equals(firstValue, value, StringComparison.Ordinal))
                {
                    mixed = true;
                    break;
                }
            }
            if (first == null) return "不支持";
            return mixed ? "多值" : firstValue;
        }

        private static string FormatMaterialValue(Material material, PresetAssignment assignment)
        {
            switch (assignment.Kind)
            {
                case PresetValueKind.Color:
                    Color color = material.GetColor(assignment.PropertyName);
                    return string.Format("RGBA({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", color.r, color.g, color.b, color.a);
                case PresetValueKind.Vector:
                    Vector4 value = material.GetVector(assignment.PropertyName);
                    return string.Format("({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", value.x, value.y, value.z, value.w);
                default:
                    return material.GetFloat(assignment.PropertyName).ToString("0.###");
            }
        }

        private static string GetPresetPropertyDisplayName(MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty property = Find(properties, propertyName);
            return property == null ? propertyName : GetDisplayName(property);
        }

        private static void ApplyPreset(MaterialEditor editor, CompositePreset preset)
        {
            var selected = new List<PresetAssignment>();
            for (int i = 0; i < preset.Assignments.Length; i++)
            {
                PresetAssignment assignment = preset.Assignments[i];
                bool different = IsDifferentForAnyTarget(editor, assignment);
                if (different && SessionState.GetBool(GetPresetSelectionKey(preset.ShaderName, preset.Id, assignment.PropertyName), true))
                    selected.Add(assignment);
            }
            if (selected.Count == 0) return;

            Undo.RecordObjects(editor.targets, "应用 ES Shader 预设：" + preset.Name);
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || material.shader == null || material.shader.name != preset.ShaderName) continue;
                for (int p = 0; p < selected.Count; p++) selected[p].Apply(material);
                SyncMaterialKeywords(material);
                EditorUtility.SetDirty(material);
            }
            for (int i = 0; i < selected.Count; i++)
                SessionState.EraseBool(GetPresetSelectionKey(preset.ShaderName, preset.Id, selected[i].PropertyName));
            editor.PropertiesChanged();
        }

        #endregion
    }
}
