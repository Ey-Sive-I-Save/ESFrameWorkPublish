#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UIToolkitButton = UnityEngine.UIElements.Button;
using UIToolkitLabel = UnityEngine.UIElements.Label;
using UIToolkitScrollView = UnityEngine.UIElements.ScrollView;
using UIToolkitSlider = UnityEngine.UIElements.Slider;
using UIToolkitToggle = UnityEngine.UIElements.Toggle;
using UIToolkitVisualElement = UnityEngine.UIElements.VisualElement;

namespace ES.TestAssets
{
    /// <summary>
    /// Test-only Composite Shader observation console.
    /// Renderer cases use MaterialPropertyBlock; UI cases own disposable runtime material clones.
    /// The generated Material assets are never mutated by this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESCompositeShaderTestAnimator : MonoBehaviour
    {
        private const string ObservationUxmlPath = "Assets/ESTestAssets/CompositeShaders/UI/ESCompositeShaderObservationPanel.uxml";
        private const string ObservationUssPath = "Assets/ESTestAssets/CompositeShaders/UI/ESCompositeShaderObservationPanel.uss";

        [Serializable]
        public sealed class RendererFloatTrack
        {
            public Renderer target;
            public string propertyName;
            public float minimum;
            public float maximum = 1f;
            public float speed = 1f;
            public float phase;
        }

        [Serializable]
        public sealed class GraphicFloatTrack
        {
            public Graphic target;
            public string propertyName;
            public float minimum;
            public float maximum = 1f;
            public float speed = 1f;
            public float phase;
        }

        [Serializable]
        public sealed class FloatControl
        {
            public string propertyName;
            public string displayName;
            public float minimum;
            public float maximum = 1f;
            public float defaultValue;
            public int vectorComponent = -1;
            public bool colorComponent;
            public bool wholeNumbers;
            public string[] optionLabels;
        }

        [Serializable]
        public sealed class PreviewCase
        {
            public string id;
            public string displayName;
            public Renderer renderer;
            public Graphic graphic;
            public List<FloatControl> controls = new List<FloatControl>();

            [NonSerialized] public Material sourceMaterial;
            [NonSerialized] public Material runtimeMaterial;
            [NonSerialized] public bool[] manualOverrides;
            [NonSerialized] public float[] values;
            [NonSerialized] public HashSet<string> manualOverrideProperties;
        }

        private sealed class RendererTrackBatch
        {
            public Renderer target;
            public readonly List<RendererFloatTrack> tracks = new List<RendererFloatTrack>();
        }

        private sealed class GraphicTrackBatch
        {
            public Graphic target;
            public readonly List<GraphicFloatTrack> tracks = new List<GraphicFloatTrack>();
        }

        [SerializeField] private List<RendererFloatTrack> rendererTracks = new List<RendererFloatTrack>();
        [SerializeField] private List<GraphicFloatTrack> graphicTracks = new List<GraphicFloatTrack>();
        [SerializeField] private List<PreviewCase> previewCases = new List<PreviewCase>();
        [SerializeField] private List<Transform> rotatingTargets = new List<Transform>();
        [SerializeField] private Vector3 rotationDegreesPerSecond = new Vector3(0f, 24f, 0f);
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private string sceneTitle;
        [SerializeField] private string category;
        [SerializeField] private string verificationFocus;
        [SerializeField] private bool startPanelCollapsed;
        [SerializeField] private string currentScenePath;
        [SerializeField] private string overviewScenePath;
        [SerializeField] private string[] navigationScenePaths;
        [SerializeField] private string[] navigationSceneLabels;

        private MaterialPropertyBlock propertyBlock;
        private bool panelCollapsed;
        private UIDocument authoredDocument;
        private PanelSettings authoredPanelSettings;
        private UIToolkitVisualElement authoredUiRoot;
        private UIToolkitVisualElement authoredPanel;
        private UIToolkitVisualElement authoredBody;
        private UIToolkitScrollView authoredCaseList;
        private UIToolkitScrollView authoredControls;
        private UIToolkitVisualElement authoredSceneNavigation;
        private UIToolkitLabel authoredSceneTitle;
        private UIToolkitLabel authoredStatus;
        private UIToolkitLabel authoredSelectedCase;
        private UIToolkitButton authoredCollapseButton;
        private UIToolkitToggle authoredAutoToggle;
        private UIToolkitToggle authoredSoloToggle;
        private bool autoAnimate = true;
        private bool soloSelection;
        private int selectedCaseIndex;
        private float runtimeStartedAt;
        private float nextStatusUpdateAt;
        private float measuredFps;
        private float measuredFrameMs;
        private long measuredMemoryBytes;
        private readonly Dictionary<Transform, Quaternion> initialLocalRotations = new Dictionary<Transform, Quaternion>();
        [NonSerialized] private Dictionary<Renderer, PreviewCase> rendererCaseLookup;
        [NonSerialized] private Dictionary<Graphic, PreviewCase> graphicCaseLookup;
        [NonSerialized] private readonly List<RendererTrackBatch> rendererTrackBatches = new List<RendererTrackBatch>();
        [NonSerialized] private readonly List<GraphicTrackBatch> graphicTrackBatches = new List<GraphicTrackBatch>();
        [NonSerialized] private Dictionary<Renderer, RendererTrackBatch> rendererTrackBatchLookup;
        [NonSerialized] private Dictionary<Graphic, GraphicTrackBatch> graphicTrackBatchLookup;

        public int RendererTrackCount => rendererTracks == null ? 0 : rendererTracks.Count;
        public int GraphicTrackCount => graphicTracks == null ? 0 : graphicTracks.Count;

        public void ConfigureRuntimeTool(string title, string categoryName, string focus, bool compactStart = false)
        {
            sceneTitle = title;
            category = categoryName;
            verificationFocus = focus;
            startPanelCollapsed = compactStart;
        }

        public void ConfigureSceneNavigation(
            string currentPath,
            string overviewPath,
            string[] scenePaths,
            string[] sceneLabels)
        {
            currentScenePath = currentPath;
            overviewScenePath = overviewPath;
            navigationScenePaths = scenePaths;
            navigationSceneLabels = sceneLabels;
        }

        public void AddRendererCase(Renderer target, string displayName, Material sourceMaterial, string id = null)
        {
            if (target == null || sourceMaterial == null)
                return;
            previewCases.Add(CreatePreviewCase(target, null, displayName, sourceMaterial, id));
        }

        public void AddGraphicCase(Graphic target, string displayName, Material sourceMaterial, string id = null)
        {
            if (target == null || sourceMaterial == null)
                return;
            previewCases.Add(CreatePreviewCase(null, target, displayName, sourceMaterial, id));
        }

        public void AddRendererTrack(Renderer target, string propertyName, float minimum, float maximum, float speed, float phase = 0f)
        {
            rendererTracks.Add(new RendererFloatTrack { target = target, propertyName = propertyName, minimum = minimum, maximum = maximum, speed = speed, phase = phase });
        }

        public void AddGraphicTrack(Graphic target, string propertyName, float minimum, float maximum, float speed, float phase = 0f)
        {
            graphicTracks.Add(new GraphicFloatTrack { target = target, propertyName = propertyName, minimum = minimum, maximum = maximum, speed = speed, phase = phase });
        }

        public void AddRotatingTarget(Transform target)
        {
            if (target != null && !rotatingTargets.Contains(target))
            {
                rotatingTargets.Add(target);
                initialLocalRotations[target] = target.localRotation;
            }
        }

        private static PreviewCase CreatePreviewCase(Renderer renderer, Graphic graphic, string displayName, Material sourceMaterial, string id)
        {
            PreviewCase previewCase = new PreviewCase
            {
                id = string.IsNullOrEmpty(id) ? displayName : id,
                displayName = displayName,
                renderer = renderer,
                graphic = graphic,
                controls = BuildControls(sourceMaterial),
            };
            return previewCase;
        }

        private static List<FloatControl> BuildControls(Material material)
        {
            var result = new List<FloatControl>();
            Shader shader = material == null ? null : material.shader;
            if (shader == null)
                return result;

            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                UnityEngine.Rendering.ShaderPropertyType propertyType = shader.GetPropertyType(i);
                if ((shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                    continue;

                string propertyName = shader.GetPropertyName(i);
                if (!IsUserTunableProperty(propertyName) || !material.HasProperty(propertyName))
                    continue;

                if (!IsRelevantProperty(material, propertyName, propertyType))
                    continue;

                if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Vector || propertyType == UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    Vector4 vector = propertyType == UnityEngine.Rendering.ShaderPropertyType.Color
                        ? (Vector4)material.GetColor(propertyName)
                        : material.GetVector(propertyName);
                    AddVectorControls(result, propertyName, vector, propertyType == UnityEngine.Rendering.ShaderPropertyType.Color);
                    continue;
                }

                if (propertyType != UnityEngine.Rendering.ShaderPropertyType.Float && propertyType != UnityEngine.Rendering.ShaderPropertyType.Range)
                    continue;

                float minimum;
                float maximum;
                if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Range)
                {
                    Vector2 limits = shader.GetPropertyRangeLimits(i);
                    minimum = limits.x;
                    maximum = limits.y;
                }
                else
                {
                    GuessRange(propertyName, material.GetFloat(propertyName), out minimum, out maximum);
                }

                float value = material.GetFloat(propertyName);
                bool wholeNumbers = IsDiscreteProperty(propertyName);
                string[] optionLabels = BuildOptionLabels(shader.GetPropertyAttributes(i));
                if (optionLabels != null && optionLabels.Length > 0)
                {
                    minimum = 0f;
                    maximum = optionLabels.Length - 1;
                    wholeNumbers = true;
                }
                result.Add(new FloatControl
                {
                    propertyName = propertyName,
                    displayName = PrettyPropertyName(propertyName),
                    minimum = minimum,
                    maximum = maximum,
                    defaultValue = Mathf.Clamp(value, minimum, maximum),
                    wholeNumbers = wholeNumbers,
                    optionLabels = optionLabels,
                });
            }
            return result;
        }

        private static void AddVectorControls(List<FloatControl> result, string propertyName, Vector4 value, bool isColor)
        {
            string lower = propertyName.ToLowerInvariant();
            float minimum = isColor ? 0f : lower.Contains("center") ? 0f : lower.Contains("direction") ? -1f : -4f;
            float maximum = isColor ? 4f : lower.Contains("center") ? 1f : lower.Contains("direction") ? 1f : 4f;
            if (isColor)
                for (int component = 0; component < 4; component++)
                    maximum = Mathf.Max(maximum, Mathf.Abs(value[component]) * 1.25f);
            string[] suffixes = isColor ? new[] { "R", "G", "B", "A" } : new[] { "X", "Y", "Z", "W" };
            for (int component = 0; component < 4; component++)
            {
                result.Add(new FloatControl
                {
                    propertyName = propertyName,
                    displayName = PrettyPropertyName(propertyName) + " · " + suffixes[component],
                    minimum = minimum,
                    maximum = maximum,
                    defaultValue = Mathf.Clamp(value[component], minimum, maximum),
                    vectorComponent = component,
                    colorComponent = isColor,
                });
            }
        }

        private static bool IsRelevantProperty(Material material, string propertyName, UnityEngine.Rendering.ShaderPropertyType propertyType)
        {
            string lower = propertyName.ToLowerInvariant();
            if (lower == "_color" || lower == "_basecolor")
                return true;

            if (lower.StartsWith("_enable", StringComparison.Ordinal) || lower.Contains("toggle"))
                return material.GetFloat(propertyName) > 0.5f;

            if (lower == "_fademode" || lower.StartsWith("_fade", StringComparison.Ordinal) || lower.StartsWith("_dissolve", StringComparison.Ordinal))
                return material.HasProperty("_FadeMode") && material.GetFloat("_FadeMode") > 0.5f
                    || material.HasProperty("_DissolveMode") && material.GetFloat("_DissolveMode") > 0.5f;

            Shader shader = material.shader;
            for (int i = 0; shader != null && i < shader.GetPropertyCount(); i++)
            {
                string enableName = shader.GetPropertyName(i);
                if (!enableName.StartsWith("_Enable", StringComparison.OrdinalIgnoreCase)
                    || !material.HasProperty(enableName)
                    || material.GetFloat(enableName) <= 0.5f)
                    continue;

                string effectPrefix = "_" + enableName.Substring("_Enable".Length).ToLowerInvariant();
                string singularPrefix = effectPrefix.EndsWith("s", StringComparison.Ordinal)
                    ? effectPrefix.Substring(0, effectPrefix.Length - 1)
                    : effectPrefix;
                if (lower.StartsWith(effectPrefix, StringComparison.Ordinal)
                    || lower.StartsWith(singularPrefix, StringComparison.Ordinal))
                    return true;
            }

            // Several SSU contracts use one parent toggle for a shared parameter block.
            if (lower.StartsWith("_recolor", StringComparison.Ordinal) && material.HasProperty("_EnableRecolorRGB") && material.GetFloat("_EnableRecolorRGB") > 0.5f)
                return true;
            if (lower.StartsWith("_split", StringComparison.Ordinal) && material.HasProperty("_EnableSplitToning") && material.GetFloat("_EnableSplitToning") > 0.5f)
                return true;
            if (lower.StartsWith("_fade", StringComparison.Ordinal) || lower.StartsWith("_dissolve", StringComparison.Ordinal))
                return material.HasProperty("_FadeMode") && material.GetFloat("_FadeMode") > 0.5f
                    || material.HasProperty("_DissolveMode") && material.GetFloat("_DissolveMode") > 0.5f;

            return false;
        }

        private static string[] BuildOptionLabels(string[] attributes)
        {
            if (attributes == null)
                return null;
            for (int i = 0; i < attributes.Length; i++)
            {
                string attribute = attributes[i];
                attribute = attribute == null ? string.Empty : attribute.Trim().Trim('[', ']');
                if (string.Equals(attribute, "Toggle", StringComparison.OrdinalIgnoreCase))
                    return new[] { "关闭", "开启" };
                if (string.IsNullOrEmpty(attribute) || !attribute.StartsWith("Enum(", StringComparison.OrdinalIgnoreCase) || !attribute.EndsWith(")", StringComparison.Ordinal))
                    continue;

                string[] parts = attribute.Substring(5, attribute.Length - 6).Split(',');
                if (parts.Length < 2)
                    return null;
                var labels = new List<string>(parts.Length / 2);
                for (int part = 0; part + 1 < parts.Length; part += 2)
                    labels.Add(PrettyOptionLabel(parts[part].Trim()));
                return labels.ToArray();
            }
            return null;
        }

        private static string PrettyOptionLabel(string value)
        {
            switch (value)
            {
                case "LocalUV": return "局部 UV";
                case "WorldProjection": return "世界投影";
                case "Basic": return "Basic";
                case "Standard": return "Standard";
                case "High": return "High";
                default: return value;
            }
        }

        private static bool IsDiscreteProperty(string propertyName)
        {
            string lower = propertyName.ToLowerInvariant();
            return lower.StartsWith("_enable", StringComparison.Ordinal)
                || lower.Contains("toggle")
                || lower.EndsWith("mode", StringComparison.Ordinal)
                || lower.EndsWith("tier", StringComparison.Ordinal)
                || lower.EndsWith("space", StringComparison.Ordinal)
                || lower.EndsWith("playback", StringComparison.Ordinal);
        }

        private static bool IsUserTunableProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return false;
            string[] excluded = { "_Surface", "_Blend", "_ZWrite", "_Cull", "_SrcBlend", "_DstBlend", "_ZTest", "_ColorMask", "_Stencil", "_QueueOffset", "_AlphaToMask" };
            for (int i = 0; i < excluded.Length; i++)
                if (propertyName.IndexOf(excluded[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            return true;
        }

        private static void GuessRange(string propertyName, float value, out float minimum, out float maximum)
        {
            string lower = propertyName.ToLowerInvariant();
            if (lower.Contains("progress") || lower.Contains("mask") || lower.Contains("fade") || lower.Contains("dissolve") || lower.Contains("cutoff"))
            {
                minimum = 0f; maximum = 1f; return;
            }
            if (lower.Contains("frequency") || lower.Contains("tiling"))
            {
                minimum = 0f; maximum = Mathf.Max(4f, value * 2f); return;
            }
            if (lower.Contains("angle"))
            {
                minimum = -360f; maximum = 360f; return;
            }
            if (lower.Contains("offset") || lower.Contains("center"))
            {
                minimum = -1f; maximum = 1f; return;
            }
            minimum = 0f;
            maximum = Mathf.Max(2f, Mathf.Abs(value) * 2f);
        }

        private static string PrettyPropertyName(string propertyName)
        {
            string value = propertyName.TrimStart('_');
            string[,] names =
            {
                { "Enable", "启用" }, { "QualityTier", "质量档" }, { "Shine", "扫光" }, { "Hologram", "全息" },
                { "Glitch", "故障" }, { "Dissolve", "溶解" }, { "Fade", "渐隐" }, { "Frozen", "冰冻" },
                { "Burn", "燃烧" }, { "Poison", "中毒" }, { "Rim", "边缘光" }, { "Flow", "流动" },
                { "Vertex", "顶点" }, { "Sequence", "序列帧" }, { "Polar", "极坐标" }, { "Radial", "径向" },
                { "Intensity", "强度" }, { "Progress", "进度" }, { "Speed", "速度" }, { "Frequency", "频率" },
                { "Radius", "半径" }, { "Angle", "角度" }, { "Width", "宽度" }, { "Strength", "力度" },
                { "Color", "颜色" }, { "Direction", "方向" }, { "Space", "空间" }, { "Mode", "模式" },
            };
            for (int i = 0; i < names.GetLength(0); i++)
                value = value.Replace(names[i, 0], " " + names[i, 1]);
            value = value.Replace("  ", " ").Trim();
            return value;
        }

        private void OnEnable()
        {
            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            initialLocalRotations.Clear();
            for (int i = 0; i < rotatingTargets.Count; i++)
            {
                Transform target = rotatingTargets[i];
                if (target != null)
                    initialLocalRotations[target] = target.localRotation;
            }
            runtimeStartedAt = Time.unscaledTime;
            autoAnimate = true;
            PrepareRuntimeCases();
            if (TryCreateAuthoredRuntimePanel())
            {
                SelectCase(0);
                SetPanelCollapsed(startPanelCollapsed);
            }
        }

        private void PrepareRuntimeCases()
        {
            for (int i = 0; i < previewCases.Count; i++)
            {
                PreviewCase previewCase = previewCases[i];
                if (previewCase == null)
                    continue;
                previewCase.sourceMaterial = previewCase.graphic != null ? previewCase.graphic.material : previewCase.renderer == null ? null : previewCase.renderer.sharedMaterial;
                previewCase.values = new float[previewCase.controls.Count];
                previewCase.manualOverrides = new bool[previewCase.controls.Count];
                previewCase.manualOverrideProperties = new HashSet<string>(StringComparer.Ordinal);
                for (int c = 0; c < previewCase.controls.Count; c++)
                    previewCase.values[c] = previewCase.controls[c].defaultValue;

                if (previewCase.graphic != null && previewCase.sourceMaterial != null)
                {
                    previewCase.runtimeMaterial = new Material(previewCase.sourceMaterial)
                    {
                        name = previewCase.sourceMaterial.name + " (Test Runtime Instance)",
                        hideFlags = HideFlags.DontSave,
                    };
                    previewCase.graphic.material = previewCase.runtimeMaterial;
                }
            }
            RebuildRuntimeLookups();
        }

        private void RebuildRuntimeLookups()
        {
            rendererCaseLookup = new Dictionary<Renderer, PreviewCase>();
            graphicCaseLookup = new Dictionary<Graphic, PreviewCase>();
            rendererTrackBatchLookup = new Dictionary<Renderer, RendererTrackBatch>();
            graphicTrackBatchLookup = new Dictionary<Graphic, GraphicTrackBatch>();
            rendererTrackBatches.Clear();
            graphicTrackBatches.Clear();

            for (int i = 0; i < previewCases.Count; i++)
            {
                PreviewCase previewCase = previewCases[i];
                if (previewCase == null)
                    continue;
                if (previewCase.renderer != null)
                    rendererCaseLookup[previewCase.renderer] = previewCase;
                if (previewCase.graphic != null)
                    graphicCaseLookup[previewCase.graphic] = previewCase;
            }

            for (int i = 0; i < rendererTracks.Count; i++)
            {
                RendererFloatTrack track = rendererTracks[i];
                if (track == null || track.target == null)
                    continue;
                if (!rendererTrackBatchLookup.TryGetValue(track.target, out RendererTrackBatch batch))
                {
                    batch = new RendererTrackBatch { target = track.target };
                    rendererTrackBatchLookup.Add(track.target, batch);
                    rendererTrackBatches.Add(batch);
                }
                batch.tracks.Add(track);
            }

            for (int i = 0; i < graphicTracks.Count; i++)
            {
                GraphicFloatTrack track = graphicTracks[i];
                if (track == null || track.target == null)
                    continue;
                if (!graphicTrackBatchLookup.TryGetValue(track.target, out GraphicTrackBatch batch))
                {
                    batch = new GraphicTrackBatch { target = track.target };
                    graphicTrackBatchLookup.Add(track.target, batch);
                    graphicTrackBatches.Add(batch);
                }
                batch.tracks.Add(track);
            }
        }

        private void Update()
        {
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            if (autoAnimate)
            {
                ApplyRendererTrackBatches(time);
                ApplyGraphicTrackBatches(time);
                for (int i = 0; i < rotatingTargets.Count; i++)
                {
                    if (rotatingTargets[i] != null)
                        rotatingTargets[i].Rotate(rotationDegreesPerSecond * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime), Space.Self);
                }
            }
            if (Time.unscaledTime >= nextStatusUpdateAt)
            {
                nextStatusUpdateAt = Time.unscaledTime + 0.25f;
                MeasureRuntimeBudget();
                UpdateRuntimeStatusPanel();
            }
        }

        private void ApplyRendererTrackBatches(float time)
        {
            for (int i = 0; i < rendererTrackBatches.Count; i++)
            {
                RendererTrackBatch batch = rendererTrackBatches[i];
                if (batch == null || batch.target == null)
                    continue;
                rendererCaseLookup.TryGetValue(batch.target, out PreviewCase previewCase);
                batch.target.GetPropertyBlock(propertyBlock);
                bool changed = false;
                for (int t = 0; t < batch.tracks.Count; t++)
                {
                    RendererFloatTrack track = batch.tracks[t];
                    if (track == null || string.IsNullOrWhiteSpace(track.propertyName)
                        || previewCase != null && HasManualOverride(previewCase, track.propertyName))
                        continue;
                    propertyBlock.SetFloat(track.propertyName, Evaluate(track.minimum, track.maximum, track.speed, track.phase, time));
                    changed = true;
                }
                if (changed)
                    batch.target.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyGraphicTrackBatches(float time)
        {
            for (int i = 0; i < graphicTrackBatches.Count; i++)
            {
                GraphicTrackBatch batch = graphicTrackBatches[i];
                if (batch == null || batch.target == null || graphicCaseLookup == null
                    || !graphicCaseLookup.TryGetValue(batch.target, out PreviewCase previewCase)
                    || previewCase.runtimeMaterial == null)
                    continue;
                Material material = previewCase.runtimeMaterial;
                for (int t = 0; t < batch.tracks.Count; t++)
                {
                    GraphicFloatTrack track = batch.tracks[t];
                    if (track == null || string.IsNullOrWhiteSpace(track.propertyName)
                        || HasManualOverride(previewCase, track.propertyName))
                        continue;
                    if (material.HasProperty(track.propertyName))
                        material.SetFloat(track.propertyName, Evaluate(track.minimum, track.maximum, track.speed, track.phase, time));
                }
            }
        }

        private bool HasManualOverride(PreviewCase previewCase, string propertyName)
        {
            if (previewCase.manualOverrideProperties != null)
                return previewCase.manualOverrideProperties.Contains(propertyName);
            for (int i = 0; i < previewCase.controls.Count; i++)
                if (previewCase.controls[i].propertyName == propertyName)
                    return previewCase.manualOverrides[i];
            return false;
        }

        private bool TryCreateAuthoredRuntimePanel()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ObservationUxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ObservationUssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError(
                    string.Format(
                        "Composite Shader 观察台资源缺失，已禁用观察台。需要同时存在 UXML 与 USS：{0} / {1}",
                        ObservationUxmlPath,
                        ObservationUssPath),
                    this);
                return false;
            }

            GameObject documentObject = new GameObject("Authored Shader Observation View");
            documentObject.transform.SetParent(transform, false);
            authoredDocument = documentObject.AddComponent<UIDocument>();
            authoredPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            authoredPanelSettings.hideFlags = HideFlags.DontSave;
            authoredPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            authoredPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            authoredPanelSettings.sortingOrder = 500;
            authoredDocument.panelSettings = authoredPanelSettings;
            authoredDocument.visualTreeAsset = visualTree;
            authoredUiRoot = authoredDocument.rootVisualElement;
            if (authoredUiRoot == null)
            {
                Debug.LogError("Composite Shader 观察台 UXML 未创建 rootVisualElement，已禁用观察台。", this);
                DestroyAuthoredRuntimePanel();
                return false;
            }

            authoredUiRoot.styleSheets.Add(styleSheet);
            authoredPanel = authoredUiRoot.Q<UIToolkitVisualElement>("panel");
            authoredBody = authoredUiRoot.Q<UIToolkitVisualElement>("panel-body");
            authoredCaseList = authoredUiRoot.Q<UIToolkitScrollView>("case-list");
            authoredControls = authoredUiRoot.Q<UIToolkitScrollView>("control-list");
            authoredSceneNavigation = authoredUiRoot.Q<UIToolkitVisualElement>("scene-nav");
            authoredSceneTitle = authoredUiRoot.Q<UIToolkitLabel>("scene-title");
            authoredStatus = authoredUiRoot.Q<UIToolkitLabel>("status");
            authoredSelectedCase = authoredUiRoot.Q<UIToolkitLabel>("selected-case");
            authoredCollapseButton = authoredUiRoot.Q<UIToolkitButton>("collapse-button");
            authoredAutoToggle = authoredUiRoot.Q<UIToolkitToggle>("auto-toggle");
            authoredSoloToggle = authoredUiRoot.Q<UIToolkitToggle>("solo-toggle");

            if (authoredPanel == null || authoredBody == null || authoredCaseList == null || authoredControls == null
                || authoredSceneNavigation == null || authoredSceneTitle == null || authoredStatus == null || authoredSelectedCase == null
                || authoredCollapseButton == null || authoredAutoToggle == null || authoredSoloToggle == null)
            {
                Debug.LogError("Composite Shader 观察台 UXML 缺少必需节点，已禁用观察台。", this);
                DestroyAuthoredRuntimePanel();
                return false;
            }

            authoredSceneTitle.text = string.IsNullOrWhiteSpace(sceneTitle) ? "Composite Shader Observation" : sceneTitle;
            authoredCollapseButton.clicked += () => SetPanelCollapsed(!panelCollapsed);
            authoredAutoToggle.value = autoAnimate;
            authoredAutoToggle.RegisterValueChangedCallback(change => autoAnimate = change.newValue);
            authoredSoloToggle.value = soloSelection;
            authoredSoloToggle.RegisterValueChangedCallback(change =>
            {
                soloSelection = change.newValue;
                ApplySoloSelection();
            });
            UIToolkitButton baselineButton = authoredUiRoot.Q<UIToolkitButton>("baseline-button");
            if (baselineButton != null)
                baselineButton.clicked += SelectBaselineCase;
            UIToolkitButton resetSelectedButton = authoredUiRoot.Q<UIToolkitButton>("reset-selected");
            if (resetSelectedButton != null)
                resetSelectedButton.clicked += ResetSelectedCase;
            UIToolkitButton resetAllButton = authoredUiRoot.Q<UIToolkitButton>("reset-all");
            if (resetAllButton != null)
                resetAllButton.clicked += ResetAllCases;
            UIToolkitButton overviewButton = authoredUiRoot.Q<UIToolkitButton>("overview-button");
            if (overviewButton != null)
                overviewButton.clicked += () => LoadScenePath(overviewScenePath);

            RebuildAuthoredSceneNavigation();
            return true;
        }

        private void RebuildAuthoredSceneNavigation()
        {
            if (authoredSceneNavigation == null)
                return;
            authoredSceneNavigation.Clear();
            if (navigationScenePaths == null)
                return;
            for (int i = 0; i < navigationScenePaths.Length; i++)
            {
                string path = navigationScenePaths[i];
                string label = navigationSceneLabels != null && i < navigationSceneLabels.Length
                    ? navigationSceneLabels[i]
                    : (i + 1).ToString();
                UIToolkitButton button = new UIToolkitButton(() => LoadScenePath(path)) { text = label };
                button.AddToClassList("scene-button");
                bool sceneExists = IsScenePathAvailable(path);
                if (!sceneExists)
                {
                    button.text = label + " · 缺失";
                    button.tooltip = path;
                }
                if (string.Equals(path, currentScenePath, StringComparison.Ordinal))
                    button.AddToClassList("scene-button-current");
                button.SetEnabled(sceneExists && !string.Equals(path, currentScenePath, StringComparison.Ordinal));
                authoredSceneNavigation.Add(button);
            }
        }

        private static bool IsScenePathAvailable(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private void RebuildAuthoredCaseList()
        {
            if (authoredCaseList == null)
                return;
            authoredCaseList.contentContainer.Clear();
            for (int i = 0; i < previewCases.Count; i++)
            {
                int captured = i;
                bool selected = i == selectedCaseIndex;
                string label = (selected ? "▸  " : "    ") + previewCases[i].displayName;
                UIToolkitButton button = new UIToolkitButton(() => SelectCase(captured)) { text = label };
                button.AddToClassList(selected ? "case-button-selected" : "case-button");
                authoredCaseList.Add(button);
            }
        }

        private void RebuildAuthoredControls()
        {
            if (authoredControls == null || previewCases == null || previewCases.Count == 0)
                return;
            authoredControls.contentContainer.Clear();
            PreviewCase previewCase = previewCases[selectedCaseIndex];
            authoredSelectedCase.text = string.Format("{0}  ·  {1} 个可调参数", previewCase.displayName, previewCase.controls.Count);
            for (int i = 0; i < previewCase.controls.Count; i++)
            {
                int captured = i;
                FloatControl control = previewCase.controls[i];
                UIToolkitVisualElement row = new UIToolkitVisualElement { name = "control-row" };
                row.AddToClassList("control-row");
                row.Add(new UIToolkitLabel(control.displayName) { name = "control-label" });
                if (control.optionLabels != null && control.optionLabels.Length > 0)
                {
                    var choices = new List<string>(control.optionLabels);
                    int selectedIndex = Mathf.Clamp(Mathf.RoundToInt(previewCase.values[i]), 0, choices.Count - 1);
                    PopupField<string> popup = new PopupField<string>(choices, selectedIndex);
                    popup.AddToClassList("control-popup");
                    popup.RegisterValueChangedCallback(change =>
                    {
                        int index = choices.IndexOf(change.newValue);
                        SetControlValue(previewCase, captured, Mathf.Max(0, index));
                    });
                    row.Add(popup);
                    row.Add(CreateAuthoredResetButton(() => popup.index = Mathf.Clamp(Mathf.RoundToInt(control.defaultValue), 0, choices.Count - 1)));
                }
                else
                {
                    UIToolkitSlider slider = new UIToolkitSlider(control.minimum, control.maximum)
                    {
                        value = control.wholeNumbers ? Mathf.Round(previewCase.values[i]) : previewCase.values[i],
                        showInputField = true,
                    };
                    slider.AddToClassList("control-slider");
                    UIToolkitLabel valueLabel = new UIToolkitLabel(FormatControlValue(control, previewCase.values[i]));
                    valueLabel.AddToClassList("control-value");
                    slider.RegisterValueChangedCallback(change =>
                    {
                        float nextValue = control.wholeNumbers ? Mathf.Round(change.newValue) : change.newValue;
                        if (control.wholeNumbers && !Mathf.Approximately(nextValue, change.newValue))
                            slider.SetValueWithoutNotify(nextValue);
                        valueLabel.text = FormatControlValue(control, nextValue);
                        SetControlValue(previewCase, captured, nextValue);
                    });
                    row.Add(slider);
                    row.Add(valueLabel);
                    row.Add(CreateAuthoredResetButton(() => slider.value = control.defaultValue));
                }
                authoredControls.Add(row);
            }
        }

        private static UIToolkitButton CreateAuthoredResetButton(Action reset)
        {
            UIToolkitButton button = new UIToolkitButton(reset) { text = "↺" };
            button.AddToClassList("control-reset");
            return button;
        }

        private void DestroyAuthoredRuntimePanel()
        {
            if (authoredDocument != null)
            {
                GameObject documentObject = authoredDocument.gameObject;
                if (Application.isPlaying)
                    Destroy(documentObject);
                else
                    DestroyImmediate(documentObject);
            }
            if (authoredPanelSettings != null)
            {
                if (Application.isPlaying)
                    Destroy(authoredPanelSettings);
                else
                    DestroyImmediate(authoredPanelSettings);
            }
            authoredDocument = null;
            authoredPanelSettings = null;
            authoredUiRoot = null;
            authoredPanel = null;
            authoredBody = null;
            authoredCaseList = null;
            authoredControls = null;
            authoredSceneNavigation = null;
            authoredSceneTitle = null;
            authoredStatus = null;
            authoredSelectedCase = null;
            authoredCollapseButton = null;
            authoredAutoToggle = null;
            authoredSoloToggle = null;
        }

        private void SelectBaselineCase()
        {
            if (previewCases == null || previewCases.Count == 0)
                return;
            for (int i = 0; i < previewCases.Count; i++)
            {
                PreviewCase previewCase = previewCases[i];
                if (previewCase == null)
                    continue;
                string key = ((previewCase.id ?? string.Empty) + " " + (previewCase.displayName ?? string.Empty)).ToLowerInvariant();
                if (key.Contains("base") || key.Contains("neutral") || key.Contains("无效果") || key.Contains("原始"))
                {
                    SelectCase(i);
                    return;
                }
            }
        }

        private void SetPanelCollapsed(bool collapsed)
        {
            panelCollapsed = collapsed;
            if (authoredPanel == null)
                return;
            authoredPanel.EnableInClassList("is-collapsed", collapsed);
            if (authoredCollapseButton != null)
                authoredCollapseButton.text = collapsed ? "+" : "−";
        }

        private void SelectCase(int index)
        {
            if (previewCases == null || previewCases.Count == 0)
                return;
            selectedCaseIndex = Mathf.Clamp(index, 0, previewCases.Count - 1);
            ApplySoloSelection();
            RebuildAuthoredCaseList();
            RebuildAuthoredControls();
        }

        private void ApplySoloSelection()
        {
            for (int i = 0; i < previewCases.Count; i++)
            {
                PreviewCase previewCase = previewCases[i];
                if (previewCase == null)
                    continue;
                bool active = !soloSelection || i == selectedCaseIndex;
                if (previewCase.renderer != null)
                    FindPresentationRoot(previewCase.renderer.transform).gameObject.SetActive(active);
                if (previewCase.graphic != null)
                    FindPresentationRoot(previewCase.graphic.transform).gameObject.SetActive(active);
            }
        }

        private static Transform FindPresentationRoot(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                if (current.name.StartsWith("Case · ", StringComparison.Ordinal))
                    return current;
                current = current.parent;
            }
            return target;
        }

        private void SetControlValue(PreviewCase previewCase, int index, float value)
        {
            if (previewCase == null || index < 0 || index >= previewCase.controls.Count)
                return;
            previewCase.values[index] = value;
            previewCase.manualOverrides[index] = true;
            previewCase.manualOverrideProperties?.Add(previewCase.controls[index].propertyName);
            ApplyControl(previewCase, index, value);
        }

        private void ApplyControl(PreviewCase previewCase, int index, float value)
        {
            FloatControl control = previewCase.controls[index];
            string propertyName = control.propertyName;
            bool isVector = control.vectorComponent >= 0;
            Vector4 vector = isVector ? ComposeVector(previewCase, propertyName) : Vector4.zero;
            if (previewCase.renderer != null)
            {
                previewCase.renderer.GetPropertyBlock(propertyBlock);
                if (isVector)
                    propertyBlock.SetVector(propertyName, vector);
                else
                    propertyBlock.SetFloat(propertyName, value);
                previewCase.renderer.SetPropertyBlock(propertyBlock);
            }
            else if (previewCase.runtimeMaterial != null && previewCase.runtimeMaterial.HasProperty(propertyName))
            {
                if (isVector)
                {
                    if (control.colorComponent)
                        previewCase.runtimeMaterial.SetColor(propertyName, (Color)vector);
                    else
                        previewCase.runtimeMaterial.SetVector(propertyName, vector);
                }
                else
                    previewCase.runtimeMaterial.SetFloat(propertyName, value);
            }
        }

        private static Vector4 ComposeVector(PreviewCase previewCase, string propertyName)
        {
            Vector4 result = Vector4.zero;
            for (int i = 0; i < previewCase.controls.Count; i++)
            {
                FloatControl candidate = previewCase.controls[i];
                if (candidate.propertyName == propertyName && candidate.vectorComponent >= 0)
                    result[candidate.vectorComponent] = previewCase.values[i];
            }
            return result;
        }

        private void ResetSelectedCase()
        {
            if (previewCases == null || previewCases.Count == 0)
                return;
            PauseAutomaticAnimation();
            ResetCase(previewCases[selectedCaseIndex]);
            RebuildAuthoredControls();
        }

        private void ResetAllCases()
        {
            PauseAutomaticAnimation();
            for (int i = 0; i < previewCases.Count; i++)
                ResetCase(previewCases[i]);
            for (int i = 0; i < rotatingTargets.Count; i++)
            {
                Transform target = rotatingTargets[i];
                if (target != null && initialLocalRotations.TryGetValue(target, out Quaternion initialRotation))
                    target.localRotation = initialRotation;
            }
            RebuildAuthoredControls();
        }

        private void PauseAutomaticAnimation()
        {
            autoAnimate = false;
            if (authoredAutoToggle != null)
                authoredAutoToggle.SetValueWithoutNotify(false);
        }

        private void ResetCase(PreviewCase previewCase)
        {
            if (previewCase == null)
                return;

            // Restore the disposable UI clone first so animated properties that are not
            // exposed for this case cannot survive a reset.
            if (previewCase.runtimeMaterial != null && previewCase.sourceMaterial != null)
                previewCase.runtimeMaterial.CopyPropertiesFromMaterial(previewCase.sourceMaterial);

            for (int i = 0; i < previewCase.controls.Count; i++)
            {
                previewCase.values[i] = previewCase.controls[i].defaultValue;
                previewCase.manualOverrides[i] = false;
                ApplyControl(previewCase, i, previewCase.values[i]);
            }
            previewCase.manualOverrideProperties?.Clear();
            if (previewCase.renderer != null)
            {
                previewCase.renderer.SetPropertyBlock(null);
                for (int i = 0; i < previewCase.controls.Count; i++)
                    ApplyControl(previewCase, i, previewCase.values[i]);
            }
            ResetRotationForCase(previewCase);
        }

        private void ResetRotationForCase(PreviewCase previewCase)
        {
            Transform target = previewCase == null
                ? null
                : previewCase.renderer != null
                    ? previewCase.renderer.transform
                    : previewCase.graphic == null ? null : previewCase.graphic.transform;
            if (target == null)
                return;

            for (int i = 0; i < rotatingTargets.Count; i++)
            {
                Transform rotatingTarget = rotatingTargets[i];
                if (rotatingTarget == null || !IsSamePresentationRoot(rotatingTarget, target))
                    continue;
                if (initialLocalRotations.TryGetValue(rotatingTarget, out Quaternion initialRotation))
                    rotatingTarget.localRotation = initialRotation;
            }
        }

        private static bool IsSamePresentationRoot(Transform left, Transform right)
        {
            return FindPresentationRoot(left) == FindPresentationRoot(right);
        }

        private void LoadScenePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (!IsScenePathAvailable(path))
            {
                Debug.LogError("Composite Shader 测试场景不存在或未导入：" + path, this);
                if (authoredStatus != null)
                    authoredStatus.text = "场景跳转失败 · 资产不存在\n" + path;
                return;
            }
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        }

        private void OnDisable()
        {
            for (int i = 0; i < previewCases.Count; i++)
            {
                PreviewCase previewCase = previewCases[i];
                if (previewCase == null)
                    continue;
                if (previewCase.renderer != null)
                    previewCase.renderer.SetPropertyBlock(null);
                if (previewCase.graphic != null && previewCase.runtimeMaterial != null && previewCase.graphic.material == previewCase.runtimeMaterial)
                    previewCase.graphic.material = previewCase.sourceMaterial;
                if (previewCase.runtimeMaterial != null)
                {
                    if (Application.isPlaying)
                        Destroy(previewCase.runtimeMaterial);
                    else
                        DestroyImmediate(previewCase.runtimeMaterial);
                }
                previewCase.runtimeMaterial = null;
                previewCase.sourceMaterial = null;
            }
            initialLocalRotations.Clear();
            rendererCaseLookup?.Clear();
            graphicCaseLookup?.Clear();
            rendererTrackBatchLookup?.Clear();
            graphicTrackBatchLookup?.Clear();
            rendererTrackBatches.Clear();
            graphicTrackBatches.Clear();
            if (propertyBlock != null)
                propertyBlock.Clear();
            DestroyAuthoredRuntimePanel();
        }

        private void UpdateRuntimeStatusPanel()
        {
            if (authoredStatus == null)
                return;
            float elapsed = Mathf.Max(0f, Time.unscaledTime - runtimeStartedAt);
            authoredStatus.text = string.Format(
                "{0}  ·  {1}\nMPB {2}/{3}  ·  UI {4}  ·  {5:0.0} FPS  ·  {6:0.00} ms  ·  {7:0.0} MB  ·  {8:0}s",
                category,
                verificationFocus,
                RendererTrackCount,
                rendererTrackBatches.Count,
                FindGraphicCaseCount() > 0 ? "实例隔离" : "N/A",
                measuredFps,
                measuredFrameMs,
                measuredMemoryBytes / (1024f * 1024f),
                elapsed);
        }

        private void MeasureRuntimeBudget()
        {
            float delta = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            measuredFps = 1f / delta;
            measuredFrameMs = delta * 1000f;
            measuredMemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
        }

        private int FindGraphicCaseCount()
        {
            int count = 0;
            for (int i = 0; i < previewCases.Count; i++)
                if (previewCases[i] != null && previewCases[i].graphic != null) count++;
            return count;
        }

        private static float Evaluate(float minimum, float maximum, float speed, float phase, float time)
        {
            return Mathf.Lerp(minimum, maximum, 0.5f + 0.5f * Mathf.Sin(time * speed + phase));
        }

        private static string FormatControlValue(FloatControl control, float value)
        {
            if (control != null && control.wholeNumbers)
                return Mathf.RoundToInt(value).ToString();
            return value.ToString("0.00");
        }

    }
}
#endif
