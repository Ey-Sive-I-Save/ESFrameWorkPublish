using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES Composite 材质 Inspector。
    /// 设计基线参考 ESNative：按 Shader 属性声明顺序处理，使用状态机驱动分类、开关和隐藏，
    /// 同时保留 ES 的中文帮助、PropertyBlock 示例和 ESEditorPresentation 视觉体系。
    /// </summary>
    public sealed partial class ESCompositeShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (materialEditor == null || properties == null) return;
            Material material = materialEditor.target as Material;
            string shaderName = material != null && material.shader != null ? material.shader.name : string.Empty;
            InspectorViewLevel viewLevel = DrawStatus(materialEditor, properties, shaderName);
            DrawMaterialMigrationPanel(materialEditor);
            DrawPresetPanel(materialEditor, properties, shaderName);
            DrawProductionTools(materialEditor);
            DrawEnvironmentDiagnostics(materialEditor, properties, shaderName);
            DrawTextureImportDiagnostics(materialEditor, properties, shaderName);
            string effectFilter = DrawEffectNavigator(shaderName, properties);
            int propertySignatureBeforeDraw = GetMaterialPropertyValueSignature(properties);
            DrawPropertyStream(materialEditor, properties, shaderName, effectFilter, viewLevel);
            if (propertySignatureBeforeDraw != GetMaterialPropertyValueSignature(properties))
                SyncKeywords(materialEditor);
        }

        public override void ValidateMaterial(Material material)
        {
            if (material != null)
                SyncMaterialKeywords(material);
        }

        private static InspectorViewLevel DrawStatus(MaterialEditor editor, MaterialProperty[] properties, string shaderName)
        {
            int enabled = 0, effectCount = 0, mixedCount = 0;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty p = properties[i];
                if (IsAlwaysHidden(p)) continue;
                if (IsStatusFeatureToggle(p.name))
                {
                    effectCount++;
                    if (p.hasMixedValue)
                    {
                        mixedCount++;
                        continue;
                    }
                    if (p.floatValue > 0.5f)
                        enabled++;
                }
            }
            MaterialProperty quality = Find(properties, "_QualityTier");
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            GUILayout.Label(GetShaderDisplayName(shaderName), ESEditorPresentation.HeaderStyle);
            string mixedText = mixedCount > 0 ? "  ·  混合 " + mixedCount : string.Empty;
            string summary = "启用 " + enabled + "/" + effectCount + mixedText;
            if (quality != null)
                summary += "  ·  质量 " + (quality.hasMixedValue ? "混合" : QualityName(quality.floatValue));
            GUILayout.Label(summary, ESEditorPresentation.SubtitleStyle);

            if (quality != null && !quality.hasMixedValue && shaderName != "ES/3D/VFX Composite URP")
            {
                int requiredQuality = GetRequiredQuality(properties, shaderName);
                int currentQuality = Mathf.Clamp(Mathf.RoundToInt(quality.floatValue), 0, 2);
                if (currentQuality < requiredQuality)
                    EditorGUILayout.HelpBox("已启用效果至少需要“" + QualityName(requiredQuality) + "”质量，当前不会完整生效。", MessageType.Warning);
            }
            InspectorViewLevel viewLevel = DrawInspectorViewMode(shaderName);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
            return viewLevel;
        }

        private static string QualityName(float value)
        {
            switch (Mathf.Clamp(Mathf.RoundToInt(value), 0, 2))
            {
                case 0: return "基础";
                case 2: return "高质量";
                default: return "标准";
            }
        }

        private static string GetShaderDisplayName(string shaderName)
        {
            switch (shaderName)
            {
                case "ES/2D/Composite URP": return "ES 2D 综合材质";
                case "ES/3D/Lit Composite URP": return "ES 3D 光照材质";
                case "ES/3D/VFX Composite URP": return "ES 3D 特效材质";
                case "ES/UI/Composite URP": return "ES UI 综合材质";
                default: return "ES Composite 材质";
            }
        }
    }
}
