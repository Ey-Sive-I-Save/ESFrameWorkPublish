using System;

namespace ES
{
    public readonly struct ESRenderTemplateResourceBinding
    {
        public ESRenderTemplateResourceBinding(
            string rendererAssetPath,
            string volumeProfileKey,
            string materialRecipeKey,
            string shaderFamilyKey)
        {
            RendererAssetPath = rendererAssetPath ?? string.Empty;
            VolumeProfileKey = volumeProfileKey ?? string.Empty;
            MaterialRecipeKey = materialRecipeKey ?? string.Empty;
            ShaderFamilyKey = shaderFamilyKey ?? string.Empty;
        }

        public string RendererAssetPath { get; }
        public string VolumeProfileKey { get; }
        public string MaterialRecipeKey { get; }
        public string ShaderFamilyKey { get; }
        public string VolumeAssetPath { get { return VolumeProfileKey; } }
        public string MaterialAssetPath { get { return MaterialRecipeKey; } }
        public string ShaderAssetPath { get { return ShaderFamilyKey; } }

        public bool IsComplete(out string reason)
        {
            if (string.IsNullOrWhiteSpace(RendererAssetPath) ||
                string.IsNullOrWhiteSpace(VolumeProfileKey) ||
                string.IsNullOrWhiteSpace(MaterialRecipeKey) ||
                string.IsNullOrWhiteSpace(ShaderFamilyKey))
            {
                reason = "template-resource-binding-incomplete";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 将纯数据模板投影到 ES 所有的 URP 资源身份。这里只返回稳定键，不加载或修改 Unity 资源。
    /// </summary>
    public static class ESRenderTemplateResourceMap
    {
        public static bool TryGet(
            ESRenderVisualStyleId style,
            ESRenderQualityProfileId quality,
            out ESRenderTemplateResourceBinding binding,
            out string reason)
        {
            if (!ESRenderStyleCatalog.TryGet(style, out ESRenderStylePreset preset))
            {
                binding = default(ESRenderTemplateResourceBinding);
                reason = "template-resource-style-unknown";
                return false;
            }

            string renderer = quality == ESRenderQualityProfileId.Performant || quality == ESRenderQualityProfileId.MobileStable
                ? "Assets/Settings/URP-Performant-Renderer.asset"
                : quality == ESRenderQualityProfileId.Balanced || quality == ESRenderQualityProfileId.CombatReadability
                    ? "Assets/Settings/URP-Balanced-Renderer.asset"
                    : "Assets/Settings/URP-HighFidelity-Renderer.asset";
            binding = new ESRenderTemplateResourceBinding(
                renderer,
                "Assets/Plugins/ES/0_Stand/Rendering/Volumes/ES-" + style + ".volume.json",
                "Assets/Plugins/ES/0_Stand/Rendering/Materials/ES-" + style + ".mat",
                "Assets/Plugins/ES/0_Stand/Rendering/ESStyleLit.shader");
            if (!binding.IsComplete(out reason))
                return false;

            reason = string.Empty;
            return true;
        }

        public static bool ValidateCatalog(out string reason)
        {
            for (int i = 0; i < ESRenderStyleCatalog.Count; i++)
            {
                ESRenderVisualStyleId style = ESRenderStyleCatalog.GetStyleIdAt(i);
                ESRenderStylePreset preset = ESRenderStylePreset.Resolve(style);
                ESRenderTemplateResourceBinding binding;
                if (!TryGet(style, preset.QualityProfile, out binding, out reason))
                    return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
