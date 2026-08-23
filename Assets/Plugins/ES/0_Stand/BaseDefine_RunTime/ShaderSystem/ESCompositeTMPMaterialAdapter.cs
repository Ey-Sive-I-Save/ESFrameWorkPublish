using TMPro;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Converts a TMP font material into a managed ES UI Composite instance and restores the source on release.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("【ES】/UI/Composite TMP Material Adapter")]
    public sealed class ESCompositeTMPMaterialAdapter : MonoBehaviour
    {
        private const string CompositeShaderName = "ES/UI/Composite URP";

        private static readonly string[] FloatProperties =
        {
            "_FaceDilate", "_OutlineWidth", "_OutlineSoftness",
            "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlayDilate", "_UnderlaySoftness",
            "_WeightNormal", "_WeightBold", "_ScaleRatioA", "_ScaleRatioB", "_ScaleRatioC",
            "_GradientScale", "_Sharpness", "_TextureWidth", "_TextureHeight",
            "_ScaleX", "_ScaleY", "_PerspectiveFilter", "_VertexOffsetX", "_VertexOffsetY",
            "_MaskSoftnessX", "_MaskSoftnessY", "_ShaderFlags", "_CullMode",
            "_StencilComp", "_Stencil", "_StencilOp", "_StencilWriteMask", "_StencilReadMask", "_ColorMask"
        };

        private static readonly string[] ColorProperties =
        {
            "_FaceColor", "_OutlineColor", "_UnderlayColor"
        };

        private static readonly string[] VectorProperties =
        {
            "_ClipRect"
        };

        private static readonly string[] MirroredKeywords =
        {
            "UNDERLAY_ON", "UNDERLAY_INNER", "UNITY_UI_CLIP_RECT", "UNITY_UI_ALPHACLIP"
        };

        [SerializeField] private TMP_Text targetText;
        [SerializeField] private Material sourceFontMaterial;
        [SerializeField] private Material compositeTemplate;
        [SerializeField] private bool previewInEditMode = true;
        [SerializeField] private bool restoreOnDisable = true;

        private Material runtimeMaterial;

        public TMP_Text TargetText => targetText;
        public Material SourceFontMaterial => sourceFontMaterial;
        public Material RuntimeMaterial => runtimeMaterial;
        public bool HasInstance => runtimeMaterial != null;

        private void Reset()
        {
            ResolveTarget();
            CaptureSourceMaterial();
        }

        private void OnEnable()
        {
            if (Application.isPlaying || previewInEditMode)
                Acquire();
        }

        private void OnDisable()
        {
            if (restoreOnDisable)
                Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void OnValidate()
        {
            ResolveTarget();
            if (compositeTemplate != null && !IsCompositeTemplate(compositeTemplate))
                compositeTemplate = null;
        }

        public void Configure(TMP_Text text, Material source = null, Material template = null)
        {
            if (targetText == text && sourceFontMaterial == source && compositeTemplate == template)
                return;

            Release();
            targetText = text;
            sourceFontMaterial = source;
            compositeTemplate = IsCompositeTemplate(template) ? template : null;
            if (isActiveAndEnabled && (Application.isPlaying || previewInEditMode))
                Acquire();
        }

        public Material Acquire()
        {
            ResolveTarget();
            if (targetText == null)
                return null;

            if (runtimeMaterial != null && targetText.fontSharedMaterial == runtimeMaterial)
                return runtimeMaterial;

            DestroyRuntimeMaterial();
            CaptureSourceMaterial();
            runtimeMaterial = CreateRuntimeMaterial(sourceFontMaterial, compositeTemplate);
            if (runtimeMaterial == null)
                return null;

            targetText.fontSharedMaterial = runtimeMaterial;
            targetText.SetMaterialDirty();
            return runtimeMaterial;
        }

        public void Release()
        {
            if (targetText != null && runtimeMaterial != null && targetText.fontSharedMaterial == runtimeMaterial)
            {
                targetText.fontSharedMaterial = sourceFontMaterial;
                targetText.SetMaterialDirty();
            }

            DestroyRuntimeMaterial();
        }

        public static Material CreateRuntimeMaterial(Material source, Material template = null)
        {
            if (source == null)
                return null;

            Shader shader = Shader.Find(CompositeShaderName);
            if (shader == null)
                return null;

            Material result = IsCompositeTemplate(template) ? new Material(template) : new Material(shader);
            result.name = source.name + " (ES TMP Runtime)";
            result.hideFlags = HideFlags.DontSave;

            CopyTexture(source, result, "_MainTex");
            CopyProperties(source, result, FloatProperties, MaterialPropertyKind.Float);
            CopyProperties(source, result, ColorProperties, MaterialPropertyKind.Color);
            CopyProperties(source, result, VectorProperties, MaterialPropertyKind.Vector);

            result.SetFloat("_EnableTMPCompatibility", 1f);
            result.SetFloat("_EnableSDF", 0f);
            bool underlay = source.IsKeywordEnabled("UNDERLAY_ON") || source.IsKeywordEnabled("UNDERLAY_INNER");
            result.SetFloat("_EnableUnderlay", underlay ? 1f : 0f);
            SetKeyword(result, "OUTLINE_ON", result.GetFloat("_OutlineWidth") > 0.0001f);
            for (int i = 0; i < MirroredKeywords.Length; i++)
                SetKeyword(result, MirroredKeywords[i], source.IsKeywordEnabled(MirroredKeywords[i]));
            return result;
        }

        private void ResolveTarget()
        {
            if (targetText == null)
                targetText = GetComponent<TMP_Text>();
        }

        private void CaptureSourceMaterial()
        {
            if (sourceFontMaterial != null || targetText == null)
                return;

            Material current = targetText.fontSharedMaterial;
            if (current != null && current != runtimeMaterial)
                sourceFontMaterial = current;
        }

        private void DestroyRuntimeMaterial()
        {
            if (runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }

        private static bool IsCompositeTemplate(Material material)
        {
            return material != null && material.shader != null && material.shader.name == CompositeShaderName;
        }

        private static void CopyTexture(Material source, Material target, string propertyName)
        {
            if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName))
                return;

            target.SetTexture(propertyName, source.GetTexture(propertyName));
            target.SetTextureScale(propertyName, source.GetTextureScale(propertyName));
            target.SetTextureOffset(propertyName, source.GetTextureOffset(propertyName));
        }

        private static void CopyProperties(
            Material source,
            Material target,
            string[] properties,
            MaterialPropertyKind kind)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                string propertyName = properties[i];
                if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName))
                    continue;

                switch (kind)
                {
                    case MaterialPropertyKind.Color:
                        target.SetColor(propertyName, source.GetColor(propertyName));
                        break;
                    case MaterialPropertyKind.Vector:
                        target.SetVector(propertyName, source.GetVector(propertyName));
                        break;
                    default:
                        target.SetFloat(propertyName, source.GetFloat(propertyName));
                        break;
                }
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private enum MaterialPropertyKind
        {
            Float,
            Color,
            Vector
        }
    }
}
