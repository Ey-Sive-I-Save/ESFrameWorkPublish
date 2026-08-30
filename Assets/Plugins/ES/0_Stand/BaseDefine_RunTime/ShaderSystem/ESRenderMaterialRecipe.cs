using System;

namespace ES
{
    public enum ESRenderSurfaceModel
    {
        Opaque = 0,
        AlphaClip = 1,
        Transparent = 2,
        Additive = 3
    }

    /// <summary>
    /// 基础材质表现配方。它只描述 Composite/URP 材质应采用的受限参数，不直接实例化 Material。
    /// </summary>
    public readonly struct ESRenderMaterialRecipe
    {
        public ESRenderMaterialRecipe(
            ESRenderVisualStyleId style,
            ESRenderSurfaceModel surface,
            int normalQuality,
            float metallic,
            float roughness,
            float emission,
            float outlineWidth,
            bool receiveShadows)
        {
            Style = style;
            Surface = surface;
            NormalQuality = Math.Max(0, Math.Min(2, normalQuality));
            Metallic = ClampFinite(metallic, 0f, 1f, 0f);
            Roughness = ClampFinite(roughness, 0.05f, 1f, 0.5f);
            Emission = ClampFinite(emission, 0f, 4f, 0f);
            OutlineWidth = ClampFinite(outlineWidth, 0f, 0.05f, 0f);
            ReceiveShadows = receiveShadows;
        }

        public ESRenderVisualStyleId Style { get; }
        public ESRenderSurfaceModel Surface { get; }
        public int NormalQuality { get; }
        public float Metallic { get; }
        public float Roughness { get; }
        public float Emission { get; }
        public float OutlineWidth { get; }
        public bool ReceiveShadows { get; }

        public bool IsValid(out string reason)
        {
            if (!Enum.IsDefined(typeof(ESRenderVisualStyleId), Style))
            {
                reason = "material-style-unknown";
                return false;
            }

            if (Surface == ESRenderSurfaceModel.Additive && ReceiveShadows)
            {
                reason = "additive-material-cannot-receive-shadows";
                return false;
            }

            if (Style == ESRenderVisualStyleId.StylizedToon && OutlineWidth <= 0f)
            {
                reason = "toon-style-requires-outline";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderMaterialRecipe Resolve(ESRenderVisualStyleId style)
        {
            switch (style)
            {
                case ESRenderVisualStyleId.NaturalPbr:
                    return new ESRenderMaterialRecipe(style, ESRenderSurfaceModel.Opaque, 2, 0.35f, 0.5f, 0.1f, 0f, true);
                case ESRenderVisualStyleId.StylizedToon:
                    return new ESRenderMaterialRecipe(style, ESRenderSurfaceModel.Opaque, 1, 0f, 0.65f, 0.05f, 0.008f, true);
                case ESRenderVisualStyleId.NoirContrast:
                    return new ESRenderMaterialRecipe(style, ESRenderSurfaceModel.Opaque, 2, 0.15f, 0.4f, 0f, 0f, true);
                case ESRenderVisualStyleId.NeonSciFi:
                    return new ESRenderMaterialRecipe(style, ESRenderSurfaceModel.Opaque, 2, 0.25f, 0.35f, 1.25f, 0f, true);
                case ESRenderVisualStyleId.FantasyAtmosphere:
                    return new ESRenderMaterialRecipe(style, ESRenderSurfaceModel.AlphaClip, 2, 0.2f, 0.55f, 0.35f, 0f, true);
                case ESRenderVisualStyleId.MobileFlat:
                    return new ESRenderMaterialRecipe(style, ESRenderSurfaceModel.Opaque, 0, 0f, 0.75f, 0f, 0f, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown ES render style for material recipe.");
            }
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
