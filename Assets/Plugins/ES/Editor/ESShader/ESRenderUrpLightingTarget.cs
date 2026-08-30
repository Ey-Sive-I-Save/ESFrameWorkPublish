using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ES.EditorInternal
{
    /// <summary>
    /// URP 14 的显式灯光目标适配器。
    /// 不搜索场景、不创建组件；调用方必须注入主光，并显式声明反射探针/接触阴影的宿主能力。
    /// </summary>
    public sealed class ESRenderUrpLightingTarget : IESRenderLightingTarget
    {
        private readonly Light mainLight;
        private readonly UniversalRenderPipelineAsset pipelineAsset;
        private readonly ESRenderVisualStyleId styleIdentity;
        private readonly bool reflectionProbesBound;
        private readonly bool contactShadowsBound;

        public ESRenderUrpLightingTarget(
            Light mainLight,
            UniversalRenderPipelineAsset pipelineAsset,
            ESRenderVisualStyleId styleIdentity,
            bool reflectionProbesBound,
            bool contactShadowsBound)
        {
            this.mainLight = mainLight;
            this.pipelineAsset = pipelineAsset;
            this.styleIdentity = styleIdentity;
            this.reflectionProbesBound = reflectionProbesBound;
            this.contactShadowsBound = contactShadowsBound;
        }

        /// <summary>
        /// 从当前 GraphicsSettings 构造目标；不会搜索或创建场景对象。
        /// </summary>
        public static bool TryCreate(
            Light mainLight,
            ESRenderVisualStyleId styleIdentity,
            bool reflectionProbesBound,
            bool contactShadowsBound,
            out ESRenderUrpLightingTarget target,
            out string reason)
        {
            target = null;
            if (mainLight == null)
            {
                reason = "main-light-binding-required";
                return false;
            }
            UniversalRenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline
                as UniversalRenderPipelineAsset;
            if (pipelineAsset == null)
            {
                reason = "current-render-pipeline-is-not-urp";
                return false;
            }
            target = new ESRenderUrpLightingTarget(
                mainLight,
                pipelineAsset,
                styleIdentity,
                reflectionProbesBound,
                contactShadowsBound);
            reason = string.Empty;
            return true;
        }

        public bool TryApply(ESRenderLightingRecipe target, out string reason)
        {
            if (!target.IsValid(out reason))
                return false;
            if (target.ContactShadows)
            {
                reason = contactShadowsBound
                    ? "urp14-contact-shadows-require-extension"
                    : "contact-shadow-binding-required";
                return false;
            }
            if (mainLight == null)
            {
                reason = "main-light-binding-required";
                return false;
            }
            if (pipelineAsset == null)
            {
                reason = "urp-pipeline-asset-binding-required";
                return false;
            }
            if (target.ReflectionProbes && !reflectionProbesBound)
            {
                reason = "reflection-probe-binding-required";
                return false;
            }

            SerializedObject serialized = new SerializedObject(pipelineAsset);
            SerializedProperty mainShadows = serialized.FindProperty("m_MainLightShadowsSupported");
            SerializedProperty additionalShadows = serialized.FindProperty("m_AdditionalLightShadowsSupported");
            SerializedProperty softShadows = serialized.FindProperty("m_SoftShadowsSupported");
            SerializedProperty additionalLights = serialized.FindProperty("m_AdditionalLightsPerObjectLimit");
            SerializedProperty shadowDistance = serialized.FindProperty("m_ShadowDistance");
            SerializedProperty shadowCascades = serialized.FindProperty("m_ShadowCascadeCount");
            SerializedProperty depthBias = serialized.FindProperty("m_ShadowDepthBias");
            SerializedProperty normalBias = serialized.FindProperty("m_ShadowNormalBias");
            SerializedProperty reflectionBlending = serialized.FindProperty("m_ReflectionProbeBlending");
            SerializedProperty reflectionBoxProjection = serialized.FindProperty("m_ReflectionProbeBoxProjection");
            if (mainShadows == null || additionalShadows == null || softShadows == null
                || additionalLights == null || shadowDistance == null || shadowCascades == null
                || depthBias == null || normalBias == null
                || reflectionBlending == null || reflectionBoxProjection == null)
            {
                reason = "urp-lighting-serialized-contract-missing";
                return false;
            }

            Undo.RecordObject(pipelineAsset, "Apply ES Lighting Recipe");
            Undo.RecordObject(mainLight, "Apply ES Lighting Recipe");
            mainShadows.boolValue = target.MainLightShadowsEnabled;
            additionalShadows.boolValue = target.AdditionalLightShadowsEnabled;
            softShadows.boolValue = target.SoftShadows;
            additionalLights.intValue = target.AdditionalLightsPerObject;
            shadowDistance.floatValue = target.ShadowDistance;
            shadowCascades.intValue = ToUrpCascadeCount(target.CascadeCount);
            depthBias.floatValue = target.ShadowBias;
            normalBias.floatValue = target.ShadowNormalBias;
            reflectionBlending.boolValue = target.ReflectionProbes;
            reflectionBoxProjection.boolValue = target.ReflectionProbes;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            mainLight.intensity = target.MainLightIntensity;
            mainLight.shadows = target.MainLightShadowsEnabled
                ? (target.SoftShadows ? LightShadows.Soft : LightShadows.Hard)
                : LightShadows.None;
            mainLight.shadowStrength = target.ShadowStrength;
            mainLight.shadowBias = target.ShadowBias;
            mainLight.shadowNormalBias = target.ShadowNormalBias;
            mainLight.lightmapBakeType = ToBakeType(target.ShadowMode);
            mainLight.useColorTemperature = target.UseColorTemperature;
            if (target.UseColorTemperature)
                mainLight.colorTemperature = target.MainLightTemperatureKelvin;
            RenderSettings.ambientIntensity = target.AmbientIntensity;
            RenderSettings.ambientLight = new Color(
                target.AmbientColor.Red,
                target.AmbientColor.Green,
                target.AmbientColor.Blue,
                1f);
            EditorUtility.SetDirty(mainLight);
            EditorUtility.SetDirty(pipelineAsset);
            reason = string.Empty;
            return true;
        }

        public bool TryCapture(out ESRenderLightingRecipe current, out string reason)
        {
            current = default(ESRenderLightingRecipe);
            if (mainLight == null)
            {
                reason = "main-light-binding-required";
                return false;
            }
            if (pipelineAsset == null)
            {
                reason = "urp-pipeline-asset-binding-required";
                return false;
            }

            SerializedObject serialized = new SerializedObject(pipelineAsset);
            SerializedProperty additionalLights = serialized.FindProperty("m_AdditionalLightsPerObjectLimit");
            SerializedProperty shadowDistance = serialized.FindProperty("m_ShadowDistance");
            SerializedProperty shadowCascades = serialized.FindProperty("m_ShadowCascadeCount");
            SerializedProperty softShadows = serialized.FindProperty("m_SoftShadowsSupported");
            SerializedProperty mainShadows = serialized.FindProperty("m_MainLightShadowsSupported");
            SerializedProperty additionalShadows = serialized.FindProperty("m_AdditionalLightShadowsSupported");
            SerializedProperty reflectionBlending = serialized.FindProperty("m_ReflectionProbeBlending");
            SerializedProperty reflectionBoxProjection = serialized.FindProperty("m_ReflectionProbeBoxProjection");
            if (additionalLights == null || shadowDistance == null || shadowCascades == null
                || softShadows == null || mainShadows == null || additionalShadows == null
                || reflectionBlending == null || reflectionBoxProjection == null)
            {
                reason = "urp-lighting-serialized-contract-missing";
                return false;
            }

            ESRenderShadowMode shadowMode = ToShadowMode(mainLight, mainShadows.boolValue);
            float capturedShadowStrength = shadowMode == ESRenderShadowMode.Disabled
                ? 0f
                : mainLight.shadowStrength;
            current = ESRenderLightingRecipe.Create(
                styleIdentity,
                shadowMode,
                additionalLights.intValue,
                shadowDistance.floatValue,
                shadowMode == ESRenderShadowMode.Disabled ? 0 : shadowCascades.intValue,
                softShadows.boolValue && mainLight.shadows == LightShadows.Soft,
                reflectionBlending.boolValue && reflectionBoxProjection.boolValue,
                mainLight.intensity,
                capturedShadowStrength,
                mainLight.shadowBias,
                mainLight.shadowNormalBias,
                false,
                RenderSettings.ambientIntensity,
                mainLight.useColorTemperature,
                mainLight.colorTemperature,
                new ESRenderRgbColor(
                    RenderSettings.ambientLight.r,
                    RenderSettings.ambientLight.g,
                    RenderSettings.ambientLight.b));
            if (!current.IsValid(out reason))
                return false;
            if (current.AdditionalLightShadowsEnabled && !additionalShadows.boolValue)
            {
                reason = "urp-additional-light-shadows-not-supported";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static int ToUrpCascadeCount(int cascadeCount)
        {
            if (cascadeCount >= 4)
                return 4;
            if (cascadeCount >= 2)
                return 2;
            return 1;
        }

        private static LightmapBakeType ToBakeType(ESRenderShadowMode mode)
        {
            switch (mode)
            {
                case ESRenderShadowMode.BakedOnly:
                    return LightmapBakeType.Baked;
                case ESRenderShadowMode.Mixed:
                    return LightmapBakeType.Mixed;
                default:
                    return LightmapBakeType.Realtime;
            }
        }

        private static ESRenderShadowMode ToShadowMode(Light light, bool mainShadowsSupported)
        {
            switch (light.lightmapBakeType)
            {
                case LightmapBakeType.Baked:
                    return ESRenderShadowMode.BakedOnly;
                case LightmapBakeType.Mixed:
                    return light.shadows == LightShadows.None
                        ? ESRenderShadowMode.BakedOnly
                        : ESRenderShadowMode.Mixed;
                default:
                    return mainShadowsSupported && light.shadows != LightShadows.None
                        ? ESRenderShadowMode.Realtime
                        : ESRenderShadowMode.Disabled;
            }
        }
    }
}
