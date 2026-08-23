using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        #region Lifecycle And Default Cache

        private static readonly Dictionary<Shader, Material> Defaults = new Dictionary<Shader, Material>();

        static ESCompositeShaderGUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseDefaults;
            EditorApplication.quitting += ReleaseDefaults;
        }

        #endregion

        #region Keyword And Render State Sync

        private static void SyncKeywords(MaterialEditor editor)
        {
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material != null && SyncMaterialKeywords(material))
                    EditorUtility.SetDirty(material);
            }
        }

        internal static bool SyncMaterialKeywords(Material material)
        {
            bool changed = false;
            string shaderName = material.shader != null ? material.shader.name : string.Empty;
            if (shaderName == "ES/2D/Composite URP")
            {
                changed |= DisableLegacyKeywords(material, Legacy2DKeywords);
                changed |= ES2DCompositeURPProperties.RefreshResourceProfile(material);
            }
            else if (shaderName == "ES/3D/Lit Composite URP")
            {
                changed |= DisableLegacyKeywords(material, LegacyLitKeywords);
                changed |= ES3DLitCompositeURPProperties.RefreshResourceProfile(material);
            }
            else if (shaderName == "ES/UI/Composite URP")
            {
                changed |= ESUICompositeURPProperties.RefreshResourceProfile(material);
            }

            if (material.HasProperty("_QualityTier"))
            {
                int tier = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QualityTier")), 0, 2);
                if (shaderName == "ES/2D/Composite URP")
                {
                    changed |= SetKeyword(material, "_ES_QUALITY_BASIC", tier == 0);
                    changed |= SetKeyword(material, "_ES_QUALITY_STANDARD", tier == 1);
                    changed |= SetKeyword(material, "_ES_QUALITY_HIGH", false);
                }
                else
                {
                    changed |= SetKeyword(material, "_ES_QUALITY_BASIC", false);
                    changed |= SetKeyword(material, "_ES_QUALITY_STANDARD", tier == 1);
                    changed |= SetKeyword(material, "_ES_QUALITY_HIGH", tier >= 2);
                }
            }

            if (material.HasProperty("_ReceiveShadows"))
            {
                bool receiveShadows = material.GetFloat("_ReceiveShadows") > 0.5f;
                changed |= SetKeyword(material, "_RECEIVE_SHADOWS_OFF", !receiveShadows);
            }

            if (shaderName == "ES/3D/Lit Composite URP" && material.HasProperty("_Surface"))
                changed |= SyncLitRenderState(material);

            if (material.HasProperty("_UseUIAlphaClip"))
                changed |= SetKeyword(material, "UNITY_UI_ALPHACLIP", material.GetFloat("_UseUIAlphaClip") > 0.5f);

            if (shaderName == "ES/UI/Composite URP" && material.HasProperty("_EnableTMPCompatibility"))
            {
                bool tmpEnabled = material.GetFloat("_EnableTMPCompatibility") > 0.5f;
                bool underlayEnabled = tmpEnabled && material.HasProperty("_EnableUnderlay")
                    && material.GetFloat("_EnableUnderlay") > 0.5f;
                bool outlineEnabled = tmpEnabled && material.HasProperty("_OutlineWidth")
                    && material.GetFloat("_OutlineWidth") > 0.0001f;
                changed |= SetKeyword(material, "UNDERLAY_ON", underlayEnabled);
                changed |= SetKeyword(material, "OUTLINE_ON", outlineEnabled);
            }

            if (material.HasProperty("_BlendMode")
                && material.HasProperty("_SrcBlend")
                && material.HasProperty("_DstBlend"))
            {
                int blendMode = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_BlendMode")), 0, 3);
                float sourceBlend;
                float destinationBlend;
                switch (blendMode)
                {
                    case 1:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.One;
                        break;
                    case 2:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.One;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                        break;
                    case 3:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.DstColor;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.Zero;
                        break;
                    default:
                        sourceBlend = (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
                        destinationBlend = (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                        break;
                }

                changed |= SetMaterialFloat(material, "_SrcBlend", sourceBlend);
                changed |= SetMaterialFloat(material, "_DstBlend", destinationBlend);
                changed |= SetMaterialFloat(material, "_BlendOp", (float)UnityEngine.Rendering.BlendOp.Add);
            }

            if (shaderName == "ES/3D/VFX Composite URP")
            {
                string renderType = material.GetTag("RenderType", false, string.Empty);
                if (!string.Equals(renderType, "Transparent", StringComparison.Ordinal))
                {
                    material.SetOverrideTag("RenderType", "Transparent");
                    changed = true;
                }

                int queueOffset = material.HasProperty("_QueueOffset")
                    ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QueueOffset")), -50, 50)
                    : 0;
                int renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
                if (material.renderQueue != renderQueue)
                {
                    material.renderQueue = renderQueue;
                    changed = true;
                }
            }
            return changed;
        }

        private static bool SyncLitRenderState(Material material)
        {
            bool changed = false;
            bool transparent = material.GetFloat("_Surface") > 0.5f;
            bool alphaClip = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;
            int queueOffset = material.HasProperty("_QueueOffset")
                ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QueueOffset")), -50, 50)
                : 0;
            if (material.HasProperty("_Cull"))
                changed |= SetMaterialFloat(material, "_Cull", Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_Cull")), 0, 2));
            if (material.HasProperty("_QueueOffset"))
                changed |= SetMaterialFloat(material, "_QueueOffset", queueOffset);

            changed |= SetMaterialFloat(material, "_SrcBlend", transparent
                ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha
                : (float)UnityEngine.Rendering.BlendMode.One);
            changed |= SetMaterialFloat(material, "_DstBlend", transparent
                ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                : (float)UnityEngine.Rendering.BlendMode.Zero);
            changed |= SetMaterialFloat(material, "_ZWrite", transparent ? 0f : 1f);

            string renderType = transparent ? "Transparent" : (alphaClip ? "TransparentCutout" : "Opaque");
            if (!string.Equals(material.GetTag("RenderType", false, string.Empty), renderType, StringComparison.Ordinal))
            {
                material.SetOverrideTag("RenderType", renderType);
                changed = true;
            }

            int baseQueue = transparent
                ? (int)UnityEngine.Rendering.RenderQueue.Transparent
                : (alphaClip
                    ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
                    : (int)UnityEngine.Rendering.RenderQueue.Geometry);
            int renderQueue = baseQueue + queueOffset;
            if (material.renderQueue != renderQueue)
            {
                material.renderQueue = renderQueue;
                changed = true;
            }

            bool opaquePasses = !transparent;
            changed |= SetShaderPassEnabled(material, "GBuffer", opaquePasses);
            changed |= SetShaderPassEnabled(material, "ShadowCaster", opaquePasses);
            changed |= SetShaderPassEnabled(material, "DepthOnly", opaquePasses);
            changed |= SetShaderPassEnabled(material, "DepthNormals", opaquePasses);
            changed |= SetShaderPassEnabled(material, "Meta", opaquePasses);
            return changed;
        }

        private static bool SetShaderPassEnabled(Material material, string passName, bool enabled)
        {
            if (material.GetShaderPassEnabled(passName) == enabled)
                return false;
            material.SetShaderPassEnabled(passName, enabled);
            return true;
        }

        private static bool DisableLegacyKeywords(Material material, string[] keywords)
        {
            bool changed = false;
            for (int i = 0; i < keywords.Length; i++)
                changed |= SetKeyword(material, keywords[i], false);
            return changed;
        }

        private static bool SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (!material.HasProperty(propertyName) || Mathf.Approximately(material.GetFloat(propertyName), value))
                return false;
            material.SetFloat(propertyName, value);
            return true;
        }

        private static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled && !material.IsKeywordEnabled(keyword))
            {
                material.EnableKeyword(keyword);
                return true;
            }
            if (!enabled && material.IsKeywordEnabled(keyword))
            {
                material.DisableKeyword(keyword);
                return true;
            }
            return false;
        }

        #endregion

        #region Defaults And Reset

        private static Material GetDefault(MaterialEditor editor)
        {
            Material source = editor.target as Material; if (source == null || source.shader == null) return null;
            if (!Defaults.TryGetValue(source.shader, out Material value) || value == null)
            {
                value = new Material(source.shader) { hideFlags = HideFlags.HideAndDontSave };
                Defaults[source.shader] = value;
            }
            return value;
        }

        private static void ReleaseDefaults()
        {
            foreach (KeyValuePair<Shader, Material> pair in Defaults)
            {
                if (pair.Value != null) UnityEngine.Object.DestroyImmediate(pair.Value);
            }
            Defaults.Clear();
            RouteCache.Clear();
            CategorySessionKeys.Clear();
            FeaturePurposeTitles.Clear();
            PresetCache.Clear();
            PresetNameCache.Clear();
            CachedSelectedParticleRenderers.Clear();
            DiagnosticParticleRenderers.Clear();
            CachedTargetMaterials.Clear();
            CachedParticleRendererIds.Clear();
            CachedHierarchyParticleRenderers.Clear();
            CachedRendererMaterials.Clear();
            CachedActiveParticleStreams.Clear();
            CachedDepthCameras.Clear();
            DiagnosticWarnings.Clear();
            InvalidateTextureImportSnapshot();
            particleConfigurationTarget = null;
            cachedParticleSelectionSignature = int.MinValue;
            cachedParticleSelectionTime = 0d;
        }

        private static bool IsDefault(MaterialProperty property, MaterialEditor editor)
        {
            if (property.hasMixedValue) return false;
            Material material = GetDefault(editor); if (material == null) return true;
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: return property.colorValue == material.GetColor(property.name);
                case MaterialProperty.PropType.Vector: return property.vectorValue == material.GetVector(property.name);
                case MaterialProperty.PropType.Texture: return property.textureValue == material.GetTexture(property.name);
                default: return Mathf.Approximately(property.floatValue, material.GetFloat(property.name));
            }
        }

        private static void Reset(MaterialProperty property, MaterialEditor editor)
        {
            Material material = GetDefault(editor); if (material == null) return;
            Undo.RecordObjects(editor.targets, "重置 ES Composite 属性");
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: property.colorValue = material.GetColor(property.name); break;
                case MaterialProperty.PropType.Vector: property.vectorValue = material.GetVector(property.name); break;
                case MaterialProperty.PropType.Texture: property.textureValue = material.GetTexture(property.name); break;
                default: property.floatValue = material.GetFloat(property.name); break;
            }
            for (int i = 0; i < editor.targets.Length; i++)
                if (editor.targets[i] != null) EditorUtility.SetDirty(editor.targets[i]);
        }

        #endregion
    }
}
