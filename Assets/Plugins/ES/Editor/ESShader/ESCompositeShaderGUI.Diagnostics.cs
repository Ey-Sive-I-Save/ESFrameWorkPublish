using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ES.EditorInternal
{
    public sealed partial class ESCompositeShaderGUI
    {
        #region Diagnostic State

        private enum DepthTextureDiagnosticStatus
        {
            AvailableForCheckedCameras,
            PipelineDefaultEnabled,
            MixedCheckedCameras,
            UnavailableForCheckedCameras,
            CameraCoverageIncomplete,
            Unknown
        }

        private static readonly GUIContent LocateUrpAssetButtonContent = new GUIContent("定位 URP 资源");
        private static readonly GUIContent FixQualityButtonContent = new GUIContent("修正质量档");
        private static readonly GUIContent ConfigureParticleStreamsButtonContent = new GUIContent("配置顶点流");
        private static readonly string[] SharedSingleSampleEffects =
        {
            "_EnableShadow", "_EnableSmoke", "_EnableFlame", "_EnablePalette",
            "_EnableTextureLayer1", "_EnableTextureLayer2", "_EnableFlowMap",
            "_EnableFullAlphaDissolve", "_EnableSourceAlphaDissolve",
            "_EnableSourceGlowDissolve", "_EnableDirectionalAlphaFade",
            "_EnableDirectionalGlowFade", "_EnableFullGlowDissolve"
        };

        private static readonly string[] SharedMaskedEffectPairs =
        {
            "_EnableUVDistort", "_UVDistortMaskToggle",
            "_EnableAddColor", "_AddColorMaskToggle",
            "_EnableStrongTint", "_StrongTintMaskToggle",
            "_EnableRecolorRGB", "_RecolorRGBMaskToggle",
            "_EnableRecolorRGBYCP", "_RecolorRGBYCPMaskToggle",
            "_EnableAddHue", "_AddHueMaskToggle",
            "_EnableSineGlow", "_SineGlowMaskToggle",
            "_EnableMetal", "_MetalMaskToggle"
        };

        private static readonly List<ParticleSystemVertexStream> RequiredParticleStreams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.Custom1XYZW,
            ParticleSystemVertexStream.Custom2X
        };
        private static ParticleSystemRenderer particleConfigurationTarget;
        private static readonly List<ParticleSystemRenderer> CachedSelectedParticleRenderers = new List<ParticleSystemRenderer>();
        private static readonly List<ParticleSystemRenderer> DiagnosticParticleRenderers = new List<ParticleSystemRenderer>();
        private static readonly HashSet<Material> CachedTargetMaterials = new HashSet<Material>();
        private static readonly HashSet<int> CachedParticleRendererIds = new HashSet<int>();
        private static readonly List<ParticleSystemRenderer> CachedHierarchyParticleRenderers = new List<ParticleSystemRenderer>();
        private static readonly List<Material> CachedRendererMaterials = new List<Material>();
        private static readonly List<ParticleSystemVertexStream> CachedActiveParticleStreams = new List<ParticleSystemVertexStream>();
        private static readonly List<ParticleSystemVertexStream> CachedRemovedParticleStreams = new List<ParticleSystemVertexStream>();
        private static readonly List<Camera> CachedDepthCameras = new List<Camera>();
        private static readonly List<string> DiagnosticWarnings = new List<string>();
        private static int cachedParticleSelectionSignature = int.MinValue;
        private static double cachedParticleSelectionTime;

        #endregion

        #region Diagnostic Workflow

        private static void DrawEnvironmentDiagnostics(MaterialEditor editor, MaterialProperty[] properties, string shaderName)
        {
            DrawTextureSampleBudgetDiagnostics(editor, shaderName);
            if (shaderName == "ES/3D/Lit Composite URP")
            {
                DrawLitResourceDiagnostics(editor);
                return;
            }
            if (shaderName != "ES/3D/VFX Composite URP") return;

            DiagnosticWarnings.Clear();
            List<string> warnings = DiagnosticWarnings;
            bool depthEffectEnabled = IsEnabled(properties, "_EnableSoftParticles") || IsEnabled(properties, "_EnableDepthIntersection");
            UnityEngine.Object pipelineAsset;
            string depthState;
            DepthTextureDiagnosticStatus depthStatus = GetUrpDepthDiagnostic(out pipelineAsset, out depthState);
            if (depthEffectEnabled && IsDepthTextureWarning(depthStatus))
                warnings.Add("深度效果已启用，但 " + depthState);

            int requiredQuality;
            int underqualifiedMaterials = GetUnderqualifiedMaterialCount(editor, properties, shaderName, out requiredQuality);
            if (underqualifiedMaterials == 1 && editor.targets.Length == 1)
                warnings.Add("当前质量档不足：已启用效果至少需要“" + QualityName(requiredQuality) + "”。");
            else if (underqualifiedMaterials > 0)
                warnings.Add("选中的 " + underqualifiedMaterials + " 个材质质量档不足，最高需要“" + QualityName(requiredQuality) + "”。");

            if (GetRoundedValue(properties, "_ZWriteMode", 0) != 0)
                warnings.Add("透明 VFX 正在写入深度，可能遮挡后续透明物体。除非明确需要，建议关闭。 ");
            int zTest = GetRoundedValue(properties, "_ZTest", 4);
            if (zTest == 0 || zTest == 1 || zTest == 8)
                warnings.Add("深度测试处于“禁用 / 从不 / 始终”之一，容易产生不可见或穿透画面。 ");
            if (Mathf.Abs(GetFloatValue(properties, "_QueueOffset", 0f)) > 25f)
                warnings.Add("渲染队列偏移超过 ±25，可能破坏透明物体的稳定排序。 ");
            if (GetRoundedValue(properties, "_BlendMode", 0) == 3 && GetColorMagnitude(properties, "_EmissionColor") > 1.05f)
                warnings.Add("正片叠底与高强度自发光同时使用，亮度语义互相冲突，结果可能难以预测。 ");

            DiagnosticParticleRenderers.Clear();
            DiagnosticParticleRenderers.AddRange(FindSelectedParticleRenderers(editor));
            List<ParticleSystemRenderer> particleRenderers = DiagnosticParticleRenderers;
            if (particleConfigurationTarget != null && UsesAnyMaterial(particleConfigurationTarget, GetTargetMaterials(editor))
                && !particleRenderers.Contains(particleConfigurationTarget))
                particleRenderers.Add(particleConfigurationTarget);
            bool vertexStreamsEnabled = IsEnabled(properties, "_EnableVertexStreams");
            int configuredRenderers = 0;
            for (int i = 0; i < particleRenderers.Count; i++)
                if (HasRequiredParticleStreams(particleRenderers[i])) configuredRenderers++;
            if (vertexStreamsEnabled && particleRenderers.Count > 0 && configuredRenderers < particleRenderers.Count)
                warnings.Add("粒子顶点流已启用，但所选对象仍有 " + (particleRenderers.Count - configuredRenderers) + " 个 ParticleSystemRenderer 未匹配 ES 通道合同。 ");

            string panelKey = "ES.Composite.Diagnostics.Panel." + shaderName;
            bool hasWarnings = warnings.Count > 0;
            bool expanded = hasWarnings || SessionState.GetBool(panelKey, false);
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.BeginHorizontal();
            if (hasWarnings)
            {
                GUILayout.Label("环境与风险", ESEditorPresentation.HeaderStyle);
            }
            else
            {
                bool nextExpanded = EditorGUILayout.Foldout(expanded, "环境与风险", true);
                if (nextExpanded != expanded)
                {
                    expanded = nextExpanded;
                    SessionState.SetBool(panelKey, expanded);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(warnings.Count == 0 ? "未发现明显风险" : warnings.Count + " 项需要确认", ESEditorPresentation.MetaStyle);
            EditorGUILayout.EndHorizontal();
            if (!expanded)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                ClearEnvironmentDiagnosticCaches();
                return;
            }

            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);

            if (depthEffectEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(GetDepthTextureDiagnosticLabel(depthStatus), ESEditorPresentation.SubtitleStyle);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(pipelineAsset == null))
                {
                    if (DrawContentSizedButton(LocateUrpAssetButtonContent, EditorStyles.miniButton))
                    {
                        Selection.activeObject = pipelineAsset;
                        EditorGUIUtility.PingObject(pipelineAsset);
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Label(depthState, ESEditorPresentation.SubtitleStyle);
            }

            if (underqualifiedMaterials > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (DrawContentSizedButton(FixQualityButtonContent))
                    RaiseUnderqualifiedQualityTargets(editor, properties, shaderName);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("目标粒子", GUILayout.Width(58f));
            particleConfigurationTarget = EditorGUILayout.ObjectField(
                particleConfigurationTarget,
                typeof(ParticleSystemRenderer),
                true) as ParticleSystemRenderer;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            string particleState = particleRenderers.Count == 0
                ? "可拖入 Renderer，或锁定材质 Inspector 后选择粒子对象"
                : "所选粒子 " + configuredRenderers + "/" + particleRenderers.Count + " 已匹配";
            GUILayout.Label(particleState, ESEditorPresentation.SubtitleStyle);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(particleRenderers.Count == 0 || configuredRenderers == particleRenderers.Count))
            {
                if (DrawContentSizedButton(ConfigureParticleStreamsButtonContent))
                    ConfigureParticleStreams(particleRenderers);
            }
            EditorGUILayout.EndHorizontal();
            if (particleRenderers.Count > 0 && configuredRenderers < particleRenderers.Count)
            {
                int renderersLosingStreams;
                string removedStreams;
                GetParticleStreamReplacementImpact(particleRenderers, out renderersLosingStreams, out removedStreams);
                GUILayout.Label("配置会按 ES 合同整体替换并重排顶点流列表。", ESEditorPresentation.SubtitleStyle);
                if (renderersLosingStreams > 0)
                    EditorGUILayout.HelpBox(
                        renderersLosingStreams + " 个 Renderer 将移除额外通道：" + removedStreams + "。",
                        MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
            ClearEnvironmentDiagnosticCaches();
        }

        private static void DrawLitResourceDiagnostics(MaterialEditor editor)
        {
            int dynamicHighQualityCount = 0;
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || material.shader == null
                    || material.shader.name != "ES/3D/Lit Composite URP") continue;

                bool highQuality = material.HasProperty("_QualityTier")
                    && material.GetFloat("_QualityTier") > 1.5f;
                bool dynamicResources = !material.HasProperty("_ResourceProfile")
                    || material.GetFloat("_ResourceProfile") < 0.5f;
                if (highQuality && dynamicResources) dynamicHighQualityCount++;
            }

            DiagnosticWarnings.Clear();
            if (dynamicHighQualityCount > 0)
                DiagnosticWarnings.Add(
                    dynamicHighQualityCount + " 个 Lit 材质同时使用“高质量 + 动态完整”资源配置。"
                    + "若移动端运行时不通过 MaterialPropertyBlock 切换效果，建议改用“材质优化”并在改动开关后刷新资源配置。");
            if (DiagnosticWarnings.Count == 0) return;

            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("移动端资源预算", ESEditorPresentation.HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(DiagnosticWarnings.Count + " 项需要确认", ESEditorPresentation.MetaStyle);
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < DiagnosticWarnings.Count; i++)
                EditorGUILayout.HelpBox(DiagnosticWarnings[i], MessageType.Warning);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
            DiagnosticWarnings.Clear();
        }

        private static void DrawTextureSampleBudgetDiagnostics(MaterialEditor editor, string shaderName)
        {
            int materialCount = 0;
            int highestTotalSamples = 0;
            int highestBaseSamples = 0;
            int highestAdditionalSamples = 0;
            int elevatedMaterialCount = 0;
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || material.shader == null || material.shader.name != shaderName) continue;
                materialCount++;
                int baseSamples = EstimateBaseTextureSamples(material, shaderName);
                int additionalSamples = EstimateAdditionalTextureSamples(material, shaderName);
                int totalSamples = baseSamples + additionalSamples;
                if (totalSamples > highestTotalSamples)
                {
                    highestTotalSamples = totalSamples;
                    highestBaseSamples = baseSamples;
                    highestAdditionalSamples = additionalSamples;
                }
                if (totalSamples >= 12) elevatedMaterialCount++;
            }
            if (materialCount == 0) return;

            string panelKey = "ES.Composite.Diagnostics.SampleBudget." + shaderName;
            bool elevated = highestTotalSamples >= 12;
            bool expanded = elevated || SessionState.GetBool(panelKey, false);
            EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle);
            EditorGUILayout.BeginHorizontal();
            bool nextExpanded = EditorGUILayout.Foldout(expanded, "静态采样预算", true);
            if (nextExpanded != expanded)
            {
                expanded = nextExpanded;
                SessionState.SetBool(panelKey, expanded);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("最高约 " + highestTotalSamples + " 次/片元", ESEditorPresentation.MetaStyle);
            EditorGUILayout.EndHorizontal();
            if (expanded)
            {
                GUILayout.Label(
                    "主颜色 Pass 基础最高约 " + highestBaseSamples + " 次 + 当前效果附加最高约 "
                    + highestAdditionalSamples + " 次。" + GetTextureSamplePassDescription(shaderName),
                    ESEditorPresentation.SubtitleStyle);
                if (elevatedMaterialCount > 0)
                    EditorGUILayout.HelpBox(
                        elevatedMaterialCount + " 个材质的静态估算达到 12 次/片元以上。"
                        + "请在目标 GLES3/Vulkan 设备上用 GPU Profiler 核对；该数字不包含缓存命中、分支一致性、带宽、寄存器和 Overdraw。",
                        highestTotalSamples >= 20 ? MessageType.Warning : MessageType.Info);
                else
                    GUILayout.Label(
                        "这是按当前质量档与开关计算的保守估算，不是 GPU Profiler 实测。",
                        ESEditorPresentation.SubtitleStyle);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static int EstimateBaseTextureSamples(Material material, string shaderName)
        {
            if (shaderName == "ES/2D/Composite URP")
            {
                int samples = 2; // Main texture plus the Universal2D mask texture.
                if (UsesEtc1ExternalAlpha(material)) samples++;
                return samples;
            }
            return 1;
        }

        private static string GetTextureSamplePassDescription(string shaderName)
        {
            if (shaderName == "ES/2D/Composite URP")
                return " 2D 按 Universal2D 主颜色 Pass 估算；Forward 回退少 1 次，Normals Pass 另读 NormalMap。"
                    + "ETC1_EXTERNAL_ALPHA 变体的每次主纹理读取还会同步读取 AlphaTex。";
            if (shaderName == "ES/3D/Lit Composite URP")
                return " Lit 按 Forward/GBuffer 表面纹理估算；不含 URP 光照、阴影、GI 和反射探针读取。";
            return " 不含管线额外读取和 Overdraw。";
        }

        private static int EstimateAdditionalTextureSamples(Material material, string shaderName)
        {
            int quality = material.HasProperty("_QualityTier")
                ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QualityTier")), 0, 2)
                : 2;
            bool high = quality >= 2;
            bool standard = quality >= 1;
            bool exactContract = IsEnabledValue(material, "_ESNativeStatusContract");
            int mainTextureTapSamples = UsesEtc1ExternalAlpha(material) ? 2 : 1;
            int samples = 0;

            bool blurActive = IsPositiveEffect(material, "_EnableBlur", "_BlurIntensity")
                && IsPositiveValue(material, "_BlurRadius");
            bool sharpenActive = IsPositiveEffect(material, "_EnableSharpen", "_SharpenFade")
                && IsPositiveValue(material, "_SharpenAmount");
            bool chromaticActive = IsPositiveEffect(material, "_EnableChromatic", "_ChromaticIntensity")
                && HasNonZeroValue(material, "_ChromaticOffset");

            if (shaderName == "ES/2D/Composite URP" || shaderName == "ES/UI/Composite URP")
            {
                if (blurActive && high)
                    samples += (material.HasProperty("_BlurMode") && material.GetFloat("_BlurMode") > 0.5f ? 8 : 4)
                        * mainTextureTapSamples;
                if (sharpenActive && high) samples += 4 * mainTextureTapSamples;
                if (chromaticActive && standard) samples += 2 * mainTextureTapSamples;
                if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableUVDistort")) samples += 1;
                if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableFullDistortion")) samples += 2;
                samples += EstimateFadeSamples(material, shaderName);
                samples += EstimateOutlineSamples(material, shaderName, quality, mainTextureTapSamples);
                if (exactContract)
                {
                    if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableHologram")) samples += 2;
                    if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableGlitch")) samples += 4;
                }
                samples += EstimateStatusSamples(material, shaderName, exactContract, high);
                samples += EstimateSurfaceEffectSamples(material);
            }
            else if (shaderName == "ES/3D/Lit Composite URP")
            {
                if (blurActive) samples += 4;
                if (sharpenActive && high) samples += 4;
                if (chromaticActive) samples += 2;
                if (IsEnabledValue(material, "_UseNormalMap")) samples += 1;
                if (IsEnabledValue(material, "_UseMetallicMap")) samples += 1;
                if (IsEnabledValue(material, "_UseOcclusionMap")) samples += 1;
                if (IsEnabledValue(material, "_UseEmission") && material.HasProperty("_EmissionMap")) samples += 1;
                if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableUVDistort")) samples += 1;
                if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableFullDistortion")) samples += 2;
                if (standard && material.HasProperty("_DissolveMode")
                    && material.GetFloat("_DissolveMode") > 0.5f
                    && material.GetFloat("_DissolveMode") < 1.5f) samples++;
                samples += EstimateFadeSamples(material, shaderName);
                samples += EstimateOutlineSamples(material, shaderName, quality, 1);
                if (high && exactContract && IsEnabledValue(material, "_EnableHologram")) samples += 2;
                if (high && IsEnabledValue(material, "_EnableGlitch")) samples += 4;
                samples += EstimateStatusSamples(material, shaderName, exactContract, high);
                samples += EstimateSurfaceEffectSamples(material);
            }
            else if (shaderName == "ES/3D/VFX Composite URP")
            {
                if (blurActive && high) samples += 4;
                if (chromaticActive && standard) samples += 2;
                if (standard && (HasNonZeroValue(material, "_Distortion")
                    || HasPositiveMode(material, "_DissolveMode"))) samples += 1;
                if (IsEffectEnabledForQuality(material, shaderName, quality, "_EnableFlowMap")) samples += 1;
                if (high && exactContract)
                {
                    if (IsEnabledValue(material, "_EnableHologram")) samples += 2;
                    if (IsEnabledValue(material, "_EnableGlitch")) samples += 4;
                }
                if (IsEnabledValue(material, "_EnableSoftParticles")
                    || IsEnabledValue(material, "_EnableDepthIntersection")) samples += 1;
                return samples;
            }

            for (int i = 0; i < SharedSingleSampleEffects.Length; i++)
                if (IsEffectEnabledForQuality(material, shaderName, quality, SharedSingleSampleEffects[i]))
                    samples += SharedSingleSampleEffects[i] == "_EnableShadow" ? mainTextureTapSamples : 1;

            for (int i = 0; i + 1 < SharedMaskedEffectPairs.Length; i += 2)
                if (IsEffectEnabledForQuality(material, shaderName, quality, SharedMaskedEffectPairs[i])
                    && IsEnabledValue(material, SharedMaskedEffectPairs[i + 1])) samples++;
            return samples;
        }

        private static int EstimateFadeSamples(Material material, string shaderName)
        {
            int samples = 0;
            float fadeMode = material.HasProperty("_FadeMode") ? material.GetFloat("_FadeMode") : 0f;
            if (fadeMode > 0.5f)
            {
                bool textureMask = fadeMode > 1.5f && fadeMode < 2.5f;
                if (shaderName == "ES/3D/Lit Composite URP")
                {
                    samples++; // Lit always reads fade noise while a Fade mode is active.
                }
                else
                {
                    bool noiseMode = (fadeMode > 2.5f && fadeMode < 3.5f)
                        || (fadeMode > 4.5f && fadeMode < 5.5f);
                    if (noiseMode || IsPositiveValue(material, "_FadeNoiseFactor")) samples++;
                }
                if (textureMask) samples++;
            }
            if (IsEnabledValue(material, "_EnableCustomFade")) samples += 2;
            if (IsEnabledValue(material, "_EnableDirectionalDistortion")) samples += 3;
            return samples;
        }

        private static int EstimateSurfaceEffectSamples(Material material)
        {
            int samples = 0;
            if (IsEnabledValue(material, "_EnableInkSpread")) samples++;
            if (IsEnabledValue(material, "_EnableCamouflage"))
                samples += IsEnabledValue(material, "_CamouflageAnimationToggle") ? 3 : 2;
            if (IsEnabledValue(material, "_EnableMetal")) samples += 2;
            if (IsEnabledValue(material, "_EnableEnchanted")) samples += 2;
            return samples;
        }

        private static int EstimateStatusSamples(
            Material material,
            string shaderName,
            bool exactContract,
            bool high)
        {
            if (exactContract)
            {
                int exactSamples = 0;
                if (shaderName == "ES/2D/Composite URP"
                    && IsEnabledValue(material, "_EnableDistortion")) exactSamples++;
                if (IsEnabledValue(material, "_EnableFrozen")) exactSamples += 3;
                if (IsEnabledValue(material, "_EnableBurn")) exactSamples += 3;
                if (IsEnabledValue(material, "_EnableRainbow")) exactSamples++;
                if (IsEnabledValue(material, "_EnablePoison")) exactSamples++;
                if (IsEnabledValue(material, "_EnableShine")
                    && IsEnabledValue(material, "_ShineMaskToggle")) exactSamples++;
                return exactSamples;
            }

            bool frozen = IsEnabledValue(material, "_EnableFrozen");
            bool burn = IsEnabledValue(material, "_EnableBurn");
            bool poison = IsEnabledValue(material, "_EnablePoison");
            if (shaderName == "ES/2D/Composite URP")
                return IsEnabledValue(material, "_EnableDistortion") || frozen || burn || poison ? 1 : 0;
            if (shaderName == "ES/UI/Composite URP")
                return frozen || burn || poison ? 1 : 0;
            if (shaderName == "ES/3D/Lit Composite URP")
                return (frozen || poison ? 1 : 0) + (high && burn ? 1 : 0);
            return 0;
        }

        private static int EstimateOutlineSamples(
            Material material,
            string shaderName,
            int quality,
            int mainTextureTapSamples)
        {
            if (shaderName == "ES/UI/Composite URP"
                && (IsEnabledValue(material, "_EnableTMPCompatibility") || IsEnabledValue(material, "_EnableSDF")))
                return 0;

            bool exactContract = IsEnabledValue(material, "_ESNativeStatusContract");
            if (!exactContract)
            {
                if (shaderName == "ES/3D/Lit Composite URP")
                    return quality >= 2 ? EstimateLitLegacyOutlineSamples(material) : 0;

                int legacySamples = 0;
                if (IsEnabledValue(material, "_EnableInnerOutline"))
                    legacySamples += 2 * mainTextureTapSamples;
                if (IsEnabledValue(material, "_EnableOuterOutline")
                    || IsEnabledValue(material, "_EnablePixelOutline"))
                    legacySamples += 4 * mainTextureTapSamples;
                return legacySamples;
            }

            int exactMinimumQuality = shaderName == "ES/UI/Composite URP" ? 1 : 2;
            if (quality < exactMinimumQuality) return 0;
            int samples = 0;
            if (IsEnabledValue(material, "_EnableInnerOutline"))
            {
                samples += 8 * mainTextureTapSamples;
                if (IsEnabledValue(material, "_InnerOutlineDistortionToggle")) samples++;
                if (IsEnabledValue(material, "_InnerOutlineTextureToggle")) samples++;
            }
            if (IsEnabledValue(material, "_EnableOuterOutline"))
            {
                samples += 8 * mainTextureTapSamples;
                if (IsEnabledValue(material, "_OuterOutlineDistortionToggle")) samples++;
                if (IsEnabledValue(material, "_OuterOutlineTextureToggle")) samples++;
            }
            if (IsEnabledValue(material, "_EnablePixelOutline"))
            {
                samples += 4 * mainTextureTapSamples;
                if (IsEnabledValue(material, "_PixelOutlineTextureToggle")) samples++;
            }
            return samples;
        }

        private static int EstimateLitLegacyOutlineSamples(Material material)
        {
            int samples = 0;
            if (IsEnabledValue(material, "_EnableInnerOutline"))
            {
                samples += 8;
                if (IsEnabledValue(material, "_InnerOutlineDistortionToggle")) samples++;
                if (IsEnabledValue(material, "_InnerOutlineTextureToggle")) samples++;
            }

            bool pixel = IsEnabledValue(material, "_EnablePixelOutline");
            bool outer = IsEnabledValue(material, "_EnableOuterOutline");
            if (!pixel && !outer) return samples;
            samples += 8;
            if (pixel)
            {
                if (IsEnabledValue(material, "_PixelOutlineTextureToggle")) samples++;
            }
            else
            {
                if (IsEnabledValue(material, "_OuterOutlineDistortionToggle")) samples++;
                if (IsEnabledValue(material, "_OuterOutlineTextureToggle")) samples++;
            }
            return samples;
        }

        private static bool UsesEtc1ExternalAlpha(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name == "ES/2D/Composite URP"
                && material.IsKeywordEnabled("ETC1_EXTERNAL_ALPHA");
        }

        private static bool IsEffectEnabledForQuality(
            Material material,
            string shaderName,
            int quality,
            string toggleProperty)
        {
            return IsEnabledValue(material, toggleProperty)
                && quality >= GetMinimumQualityTier(shaderName, toggleProperty);
        }

        private static bool IsPositiveEffect(Material material, string toggleProperty, string valueProperty)
        {
            return IsEnabledValue(material, toggleProperty) && IsPositiveValue(material, valueProperty);
        }

        private static bool IsEnabledValue(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.5f;
        }

        private static bool IsPositiveValue(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.0001f;
        }

        private static bool HasNonZeroValue(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && Mathf.Abs(material.GetFloat(propertyName)) > 0.000001f;
        }

        private static bool HasPositiveMode(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.5f;
        }

        private static void ClearEnvironmentDiagnosticCaches()
        {
            DiagnosticParticleRenderers.Clear();
            DiagnosticWarnings.Clear();
            CachedTargetMaterials.Clear();
        }

        private static bool DrawContentSizedButton(GUIContent content)
        {
            return DrawContentSizedButton(content, GUI.skin.button);
        }

        private static bool DrawContentSizedButton(GUIContent content, GUIStyle style)
        {
            float minimumWidth = Mathf.Ceil(style.CalcSize(content).x) + 12f;
            return GUILayout.Button(content, style, GUILayout.MinWidth(minimumWidth), GUILayout.ExpandWidth(false));
        }

        private static int GetRequiredQuality(MaterialProperty[] properties, string shaderName)
        {
            int required = 0;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (property == null) continue;
                bool active = IsStatusFeatureToggle(property.name)
                    ? property.hasMixedValue || property.floatValue > 0.5f
                    : property.name == "_DissolveMode" && (property.hasMixedValue || property.floatValue > 0.5f);
                if (!active) continue;
                required = Mathf.Max(required, GetMinimumQualityTier(shaderName, property.name));
            }

            if (shaderName == "ES/3D/VFX Composite URP")
            {
                MaterialProperty distortion = Find(properties, "_Distortion");
                if (distortion != null && (distortion.hasMixedValue || Mathf.Abs(distortion.floatValue) > 0.00001f))
                    required = Mathf.Max(required, 1);
            }
            return required;
        }

        private static int GetRequiredQuality(Material material, MaterialProperty[] properties, string shaderName)
        {
            int required = 0;
            for (int i = 0; i < properties.Length; i++)
            {
                MaterialProperty property = properties[i];
                if (property == null || !material.HasProperty(property.name)) continue;
                bool active = IsStatusFeatureToggle(property.name)
                    ? material.GetFloat(property.name) > 0.5f
                    : property.name == "_DissolveMode" && material.GetFloat(property.name) > 0.5f;
                if (!active) continue;
                required = Mathf.Max(required, GetMinimumQualityTier(shaderName, property.name));
            }

            if (shaderName == "ES/3D/VFX Composite URP" && material.HasProperty("_Distortion")
                && Mathf.Abs(material.GetFloat("_Distortion")) > 0.00001f)
                required = Mathf.Max(required, 1);
            return required;
        }

        private static int GetUnderqualifiedMaterialCount(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string shaderName,
            out int highestRequiredQuality)
        {
            int count = 0;
            highestRequiredQuality = 0;
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || material.shader == null || material.shader.name != shaderName
                    || !material.HasProperty("_QualityTier")) continue;
                int required = GetRequiredQuality(material, properties, shaderName);
                int current = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QualityTier")), 0, 2);
                if (current >= required) continue;
                count++;
                highestRequiredQuality = Mathf.Max(highestRequiredQuality, required);
            }
            return count;
        }

        private static void RaiseUnderqualifiedQualityTargets(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string shaderName)
        {
            List<UnityEngine.Object> targets = new List<UnityEngine.Object>();
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material == null || material.shader == null || material.shader.name != shaderName
                    || !material.HasProperty("_QualityTier")) continue;
                int required = GetRequiredQuality(material, properties, shaderName);
                int current = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QualityTier")), 0, 2);
                if (current < required) targets.Add(material);
            }
            if (targets.Count == 0) return;

            Undo.RecordObjects(targets.ToArray(), "修正 ES Shader 质量档");
            for (int i = 0; i < targets.Count; i++)
            {
                Material material = (Material)targets[i];
                int required = GetRequiredQuality(material, properties, shaderName);
                material.SetFloat("_QualityTier", required);
                SyncMaterialKeywords(material);
                EditorUtility.SetDirty(material);
            }
            editor.PropertiesChanged();
        }

        private static DepthTextureDiagnosticStatus GetUrpDepthDiagnostic(out UnityEngine.Object pipelineAsset, out string state)
        {
            pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset == null) pipelineAsset = GraphicsSettings.defaultRenderPipeline;
            if (pipelineAsset == null)
            {
                state = "项目没有配置当前 Render Pipeline Asset。";
                return DepthTextureDiagnosticStatus.Unknown;
            }

            PropertyInfo property = pipelineAsset.GetType().GetProperty("supportsCameraDepthTexture", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool))
            {
                state = "当前管线不是可识别的 URP Asset，无法确认 Depth Texture。";
                return DepthTextureDiagnosticStatus.Unknown;
            }

            bool pipelineDefaultEnabled = (bool)property.GetValue(pipelineAsset, null);
            Type cameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            PropertyInfo requiresDepthProperty = cameraDataType == null
                ? null
                : cameraDataType.GetProperty("requiresDepthTexture", BindingFlags.Instance | BindingFlags.Public);
            if (cameraDataType == null || requiresDepthProperty == null || requiresDepthProperty.PropertyType != typeof(bool))
            {
                state = pipelineDefaultEnabled
                    ? "URP Asset 默认开启；无法读取 Camera 覆盖配置。"
                    : "URP Asset 默认关闭；无法读取 Camera 覆盖配置。";
                return pipelineDefaultEnabled
                    ? DepthTextureDiagnosticStatus.PipelineDefaultEnabled
                    : DepthTextureDiagnosticStatus.CameraCoverageIncomplete;
            }

            CachedDepthCameras.Clear();
            List<Camera> cameras = CachedDepthCameras;
            GameObject[] selectedObjects = Selection.gameObjects;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                Camera camera = selectedObjects[i].GetComponent<Camera>();
                if (camera != null && !cameras.Contains(camera)) cameras.Add(camera);
            }
            Camera mainCamera = Camera.main;
            if (mainCamera != null && !cameras.Contains(mainCamera)) cameras.Add(mainCamera);

            if (cameras.Count == 0)
            {
                state = pipelineDefaultEnabled
                    ? "URP Asset 默认开启；尚未检查实际渲染相机，相机仍可覆盖关闭。"
                    : "URP Asset 默认关闭，且未找到选中 Camera 或 Camera.main；无法确认实际渲染相机。";
                CachedDepthCameras.Clear();
                return pipelineDefaultEnabled
                    ? DepthTextureDiagnosticStatus.PipelineDefaultEnabled
                    : DepthTextureDiagnosticStatus.CameraCoverageIncomplete;
            }

            int enabledCameras = 0;
            for (int i = 0; i < cameras.Count; i++)
            {
                Component data = cameras[i].GetComponent(cameraDataType);
                bool enabled = data == null ? pipelineDefaultEnabled : (bool)requiresDepthProperty.GetValue(data, null);
                if (enabled) enabledCameras++;
            }
            int checkedCameraCount = cameras.Count;
            CachedDepthCameras.Clear();
            if (enabledCameras == checkedCameraCount)
            {
                state = "已检查 " + checkedCameraCount + " 个相机，当前均会请求 Depth Texture；未检查相机不在结论内。";
                return DepthTextureDiagnosticStatus.AvailableForCheckedCameras;
            }
            if (enabledCameras == 0)
            {
                state = "已检查 " + checkedCameraCount + " 个相机，当前均未请求 Depth Texture；未检查相机不在结论内。";
                return DepthTextureDiagnosticStatus.UnavailableForCheckedCameras;
            }

            state = "已检查 " + checkedCameraCount + " 个相机，其中 " + enabledCameras
                + " 个会请求 Depth Texture；请按实际渲染相机确认。";
            return DepthTextureDiagnosticStatus.MixedCheckedCameras;
        }

        private static bool IsDepthTextureWarning(DepthTextureDiagnosticStatus status)
        {
            return status != DepthTextureDiagnosticStatus.AvailableForCheckedCameras
                && status != DepthTextureDiagnosticStatus.PipelineDefaultEnabled;
        }

        private static string GetDepthTextureDiagnosticLabel(DepthTextureDiagnosticStatus status)
        {
            switch (status)
            {
                case DepthTextureDiagnosticStatus.AvailableForCheckedCameras:
                    return "Depth Texture：已检查相机可用";
                case DepthTextureDiagnosticStatus.PipelineDefaultEnabled:
                    return "Depth Texture：管线默认开启";
                case DepthTextureDiagnosticStatus.MixedCheckedCameras:
                    return "Depth Texture：已检查相机配置不一致";
                case DepthTextureDiagnosticStatus.UnavailableForCheckedCameras:
                    return "Depth Texture：已检查相机未开启";
                case DepthTextureDiagnosticStatus.CameraCoverageIncomplete:
                    return "Depth Texture：相机覆盖未确认";
                default:
                    return "Depth Texture：无法确认";
            }
        }

        private static List<ParticleSystemRenderer> FindSelectedParticleRenderers(MaterialEditor editor)
        {
            HashSet<Material> targetMaterials = GetTargetMaterials(editor);
            GameObject[] selectedObjects = Selection.gameObjects;
            int signature = 17;
            unchecked
            {
                for (int i = 0; i < editor.targets.Length; i++)
                    signature = signature * 31 + (editor.targets[i] == null ? 0 : editor.targets[i].GetInstanceID());
                for (int i = 0; i < selectedObjects.Length; i++)
                    signature = signature * 31 + (selectedObjects[i] == null ? 0 : selectedObjects[i].GetInstanceID());
            }

            double now = EditorApplication.timeSinceStartup;
            if (signature == cachedParticleSelectionSignature && now - cachedParticleSelectionTime < 0.5d)
                return CachedSelectedParticleRenderers;

            CachedSelectedParticleRenderers.Clear();
            CachedParticleRendererIds.Clear();
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                CachedHierarchyParticleRenderers.Clear();
                selectedObjects[i].GetComponentsInChildren(true, CachedHierarchyParticleRenderers);
                for (int r = 0; r < CachedHierarchyParticleRenderers.Count; r++)
                {
                    ParticleSystemRenderer renderer = CachedHierarchyParticleRenderers[r];
                    if (renderer == null || !UsesAnyMaterial(renderer, targetMaterials) || !CachedParticleRendererIds.Add(renderer.GetInstanceID())) continue;
                    CachedSelectedParticleRenderers.Add(renderer);
                }
            }
            CachedHierarchyParticleRenderers.Clear();
            cachedParticleSelectionSignature = signature;
            cachedParticleSelectionTime = now;
            return CachedSelectedParticleRenderers;
        }

        private static HashSet<Material> GetTargetMaterials(MaterialEditor editor)
        {
            CachedTargetMaterials.Clear();
            for (int i = 0; i < editor.targets.Length; i++)
            {
                Material material = editor.targets[i] as Material;
                if (material != null) CachedTargetMaterials.Add(material);
            }
            return CachedTargetMaterials;
        }

        private static bool UsesAnyMaterial(ParticleSystemRenderer renderer, HashSet<Material> targetMaterials)
        {
            CachedRendererMaterials.Clear();
            renderer.GetSharedMaterials(CachedRendererMaterials);
            bool usesTargetMaterial = false;
            for (int i = 0; i < CachedRendererMaterials.Count; i++)
            {
                if (CachedRendererMaterials[i] == null || !targetMaterials.Contains(CachedRendererMaterials[i])) continue;
                usesTargetMaterial = true;
                break;
            }
            CachedRendererMaterials.Clear();
            return usesTargetMaterial;
        }

        private static bool HasRequiredParticleStreams(ParticleSystemRenderer renderer)
        {
            if (renderer == null) return false;
            CachedActiveParticleStreams.Clear();
            renderer.GetActiveVertexStreams(CachedActiveParticleStreams);
            if (CachedActiveParticleStreams.Count != RequiredParticleStreams.Count) return false;
            for (int i = 0; i < CachedActiveParticleStreams.Count; i++)
                if (CachedActiveParticleStreams[i] != RequiredParticleStreams[i]) return false;
            return true;
        }

        private static void ConfigureParticleStreams(List<ParticleSystemRenderer> renderers)
        {
            if (renderers == null || renderers.Count == 0) return;
            int renderersLosingStreams;
            string removedStreams;
            GetParticleStreamReplacementImpact(renderers, out renderersLosingStreams, out removedStreams);
            if (renderersLosingStreams > 0 && !ESDialog.ConfirmModal(
                    "es.composite.particle-streams.replace",
                    "替换粒子顶点流？",
                    "此操作会按 ES 合同整体替换并重排 " + renderers.Count + " 个 Renderer 的顶点流列表。\n\n"
                    + renderersLosingStreams + " 个 Renderer 将移除额外通道：" + removedStreams + "。\n\n"
                    + "顶点流顺序会影响 TEXCOORD 通道打包。是否继续？",
                    "替换",
                    "取消",
                    tone: ESDialogTone.Warning,
                    host: ESDialogHost.Editor,
                    allowMainWorkspaceFallback: true))
                return;
            UnityEngine.Object[] targets = new UnityEngine.Object[renderers.Count];
            for (int i = 0; i < renderers.Count; i++) targets[i] = renderers[i];
            Undo.RecordObjects(targets, "配置 ES VFX 粒子顶点流");
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].SetActiveVertexStreams(RequiredParticleStreams);
                EditorUtility.SetDirty(renderers[i]);
            }
        }

        private static void GetParticleStreamReplacementImpact(
            List<ParticleSystemRenderer> renderers,
            out int renderersLosingStreams,
            out string removedStreams)
        {
            renderersLosingStreams = 0;
            CachedRemovedParticleStreams.Clear();
            for (int i = 0; i < renderers.Count; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null) continue;
                CachedActiveParticleStreams.Clear();
                renderer.GetActiveVertexStreams(CachedActiveParticleStreams);
                bool losesStream = false;
                for (int streamIndex = 0; streamIndex < CachedActiveParticleStreams.Count; streamIndex++)
                {
                    ParticleSystemVertexStream stream = CachedActiveParticleStreams[streamIndex];
                    if (RequiredParticleStreams.Contains(stream)) continue;
                    losesStream = true;
                    if (!CachedRemovedParticleStreams.Contains(stream)) CachedRemovedParticleStreams.Add(stream);
                }
                if (losesStream) renderersLosingStreams++;
            }
            removedStreams = CachedRemovedParticleStreams.Count == 0
                ? "无"
                : string.Join("、", CachedRemovedParticleStreams);
            CachedActiveParticleStreams.Clear();
            CachedRemovedParticleStreams.Clear();
        }

        private static bool IsEnabled(MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty property = Find(properties, propertyName);
            return property != null && (property.hasMixedValue || property.floatValue > 0.5f);
        }

        private static int GetRoundedValue(MaterialProperty[] properties, string propertyName, int fallback)
        {
            MaterialProperty property = Find(properties, propertyName);
            return property == null || property.hasMixedValue ? fallback : Mathf.RoundToInt(property.floatValue);
        }

        private static float GetFloatValue(MaterialProperty[] properties, string propertyName, float fallback)
        {
            MaterialProperty property = Find(properties, propertyName);
            return property == null || property.hasMixedValue ? fallback : property.floatValue;
        }

        private static float GetColorMagnitude(MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty property = Find(properties, propertyName);
            return property == null || property.hasMixedValue ? 0f : property.colorValue.maxColorComponent;
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y)
                && Mathf.Approximately(a.z, b.z) && Mathf.Approximately(a.w, b.w);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        #endregion
    }
}
