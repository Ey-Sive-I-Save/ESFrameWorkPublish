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
            if (renderersLosingStreams > 0 && !EditorUtility.DisplayDialog(
                    "替换粒子顶点流？",
                    "此操作会按 ES 合同整体替换并重排 " + renderers.Count + " 个 Renderer 的顶点流列表。\n\n"
                    + renderersLosingStreams + " 个 Renderer 将移除额外通道：" + removedStreams + "。\n\n"
                    + "顶点流顺序会影响 TEXCOORD 通道打包。是否继续？",
                    "替换",
                    "取消"))
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
