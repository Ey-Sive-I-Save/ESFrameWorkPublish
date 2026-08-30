using System;

namespace ES
{
    /// <summary>
    /// 可发布的 ES 渲染模板包。模板包只持有纯数据与兼容声明，不承担 Unity 生命周期。
    /// </summary>
    [Serializable]
    public sealed class ESRenderTemplateBundle
    {
        public const int CurrentSchemaVersion = 1;

        public readonly int schemaVersion;
        public readonly string bundleId;
        public readonly string bundleVersion;
        public readonly string minimumUnityVersion;
        public readonly string maximumUnityVersion;
        public readonly string urpPackageVersion;
        private readonly ESRenderVisualStyleId[] styleIds;

        private ESRenderTemplateBundle(
            string bundleId,
            string bundleVersion,
            string minimumUnityVersion,
            string maximumUnityVersion,
            string urpPackageVersion,
            ESRenderVisualStyleId[] styleIds)
        {
            schemaVersion = CurrentSchemaVersion;
            this.bundleId = bundleId;
            this.bundleVersion = bundleVersion;
            this.minimumUnityVersion = minimumUnityVersion;
            this.maximumUnityVersion = maximumUnityVersion;
            this.urpPackageVersion = urpPackageVersion;
            this.styleIds = styleIds ?? new ESRenderVisualStyleId[0];
        }

        public int StyleCount
        {
            get { return styleIds.Length; }
        }

        public ESRenderVisualStyleId GetStyleIdAt(int index)
        {
            if (index < 0 || index >= styleIds.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Template style index is outside the bundle.");
            return styleIds[index];
        }

        public bool TryGetStyle(ESRenderVisualStyleId style, out ESRenderStylePreset preset)
        {
            for (int i = 0; i < styleIds.Length; i++)
            {
                if (styleIds[i] == style)
                    return ESRenderStyleCatalog.TryGet(style, out preset);
            }

            preset = default(ESRenderStylePreset);
            return false;
        }

        public bool IsCompatible(string unityVersion, string urpVersion, out string reason)
        {
            if (string.IsNullOrWhiteSpace(unityVersion) || string.IsNullOrWhiteSpace(urpVersion))
            {
                reason = "compatibility-version-required";
                return false;
            }

            if (!string.Equals(urpVersion, urpPackageVersion, StringComparison.OrdinalIgnoreCase))
            {
                reason = "urp-version-mismatch";
                return false;
            }

            // Version strings are intentionally compared by exact declared range endpoints;
            // Unity-specific semantic parsing remains an external validation concern.
            if (string.CompareOrdinal(unityVersion, minimumUnityVersion) < 0 ||
                string.CompareOrdinal(unityVersion, maximumUnityVersion) > 0)
            {
                reason = "unity-version-out-of-range";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool Validate(out string reason)
        {
            if (schemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(bundleVersion))
            {
                reason = "template-bundle-identity-required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(minimumUnityVersion) || string.IsNullOrWhiteSpace(maximumUnityVersion) || string.IsNullOrWhiteSpace(urpPackageVersion))
            {
                reason = "template-bundle-compatibility-required";
                return false;
            }

            if (styleIds.Length == 0)
            {
                reason = "template-bundle-styles-required";
                return false;
            }

            for (int i = 0; i < styleIds.Length; i++)
            {
                for (int j = i + 1; j < styleIds.Length; j++)
                {
                    if (styleIds[i] == styleIds[j])
                    {
                        reason = "template-bundle-style-duplicate";
                        return false;
                    }
                }

                ESRenderStylePreset preset;
                if (!TryGetStyle(styleIds[i], out preset) || !preset.IsValid(out reason))
                    return false;
            }

            reason = string.Empty;
            return true;
        }

        public static ESRenderTemplateBundle CreateBuiltIn()
        {
            return new ESRenderTemplateBundle(
                "es.urp.rendering.templates",
                "1.0.0",
                "2022.3.0",
                "6000.99.99",
                "14.0.11",
                new[]
                {
                    ESRenderVisualStyleId.NaturalPbr,
                    ESRenderVisualStyleId.StylizedToon,
                    ESRenderVisualStyleId.NoirContrast,
                    ESRenderVisualStyleId.NeonSciFi,
                    ESRenderVisualStyleId.FantasyAtmosphere,
                    ESRenderVisualStyleId.MobileFlat,
                    ESRenderVisualStyleId.RetroPixel,
                    ESRenderVisualStyleId.HorrorGrit,
                    ESRenderVisualStyleId.CozyPastel,
                    ESRenderVisualStyleId.TacticalRealism
                });
        }
    }
}
