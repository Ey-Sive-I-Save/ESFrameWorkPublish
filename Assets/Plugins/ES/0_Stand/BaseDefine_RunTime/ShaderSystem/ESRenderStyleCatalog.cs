using System;

namespace ES
{
    /// <summary>
    /// ES 内置渲染风格目录。目录是纯托管数据，不依赖 MonoBehaviour、ScriptableObject 或 Editor。
    /// </summary>
    public static class ESRenderStyleCatalog
    {
        private static readonly ESRenderVisualStyleId[] OrderedStyles =
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
        };

        public static int Count
        {
            get { return OrderedStyles.Length; }
        }

        public static ESRenderVisualStyleId GetStyleIdAt(int index)
        {
            if (index < 0 || index >= OrderedStyles.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Style index is outside the ES catalog.");
            return OrderedStyles[index];
        }

        public static bool TryGet(ESRenderVisualStyleId style, out ESRenderStylePreset preset)
        {
            for (int i = 0; i < OrderedStyles.Length; i++)
            {
                if (OrderedStyles[i] != style)
                    continue;

                preset = ESRenderStylePreset.Resolve(style);
                return true;
            }

            preset = default(ESRenderStylePreset);
            return false;
        }

        /// <summary>
        /// 按质量意图选择第一个稳定模板；同一质量档的多个风格保持目录顺序，结果确定且可解释。
        /// </summary>
        public static bool TryGetFirstForQuality(ESRenderQualityProfileId qualityProfile, out ESRenderStylePreset preset)
        {
            for (int i = 0; i < OrderedStyles.Length; i++)
            {
                ESRenderStylePreset candidate = ESRenderStylePreset.Resolve(OrderedStyles[i]);
                if (candidate.QualityProfile != qualityProfile)
                    continue;

                preset = candidate;
                return true;
            }

            preset = default(ESRenderStylePreset);
            return false;
        }

        public static bool Validate(out string reason)
        {
            for (int i = 0; i < OrderedStyles.Length; i++)
            {
                ESRenderVisualStyleId style = OrderedStyles[i];
                for (int j = i + 1; j < OrderedStyles.Length; j++)
                {
                    if (OrderedStyles[j] == style)
                    {
                        reason = "style-catalog-duplicate";
                        return false;
                    }
                }

                ESRenderStylePreset preset;
                if (!TryGet(style, out preset) || !preset.IsValid(out reason))
                    return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
