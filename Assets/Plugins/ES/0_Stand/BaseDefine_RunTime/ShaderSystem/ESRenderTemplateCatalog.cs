using System;

namespace ES
{
    /// <summary>
    /// ES 可调用模板目录：将风格、场景意图和平台能力组合成确定性的完整 URP 配置。
    /// 目录只负责纯数据解析，不创建 Unity 对象，也不触发 GameManager 或后端写入。
    /// </summary>
    public static class ESRenderTemplateCatalog
    {
        public static int CombinationCount
        {
            get
            {
                return ESRenderStyleCatalog.Count * 6 * 4;
            }
        }

        public static bool TryResolve(
            ESRenderVisualStyleId style,
            ESRenderSceneIntentId intent,
            ESRenderPlatformId platform,
            out ESRenderResolvedConfiguration configuration,
            out string reason)
        {
            return TryResolve(style, intent, platform, ESRenderContentTypeId.RolePlaying, out configuration, out reason);
        }

        public static bool TryResolve(
            ESRenderVisualStyleId style,
            ESRenderSceneIntentId intent,
            ESRenderPlatformId platform,
            ESRenderContentTypeId contentType,
            out ESRenderResolvedConfiguration configuration,
            out string reason)
        {
            ESRenderStylePreset preset;
            if (!ESRenderStyleCatalog.TryGet(style, out preset))
            {
                configuration = default(ESRenderResolvedConfiguration);
                reason = "template-style-unknown";
                return false;
            }

            ESRenderSceneIntent scene = new ESRenderSceneIntent(
                intent,
                style,
                preset.QualityProfile,
                true,
                true,
                1f);
            return ESRenderConfigurationResolver.TryResolve(
                scene,
                ESRenderPlatformProfile.Resolve(platform),
                contentType,
                out configuration,
                out reason);
        }

        public static bool ValidateBuiltIn(out string reason)
        {
            for (int styleIndex = 0; styleIndex < ESRenderStyleCatalog.Count; styleIndex++)
            {
                ESRenderVisualStyleId style = ESRenderStyleCatalog.GetStyleIdAt(styleIndex);
                for (int intentIndex = 0; intentIndex < 6; intentIndex++)
                {
                    ESRenderSceneIntentId intent = (ESRenderSceneIntentId)intentIndex;
                    for (int platformIndex = 0; platformIndex < 4; platformIndex++)
                    {
                        ESRenderResolvedConfiguration configuration;
                        if (!TryResolve(style, intent, (ESRenderPlatformId)platformIndex, out configuration, out reason))
                            return false;
                        if (configuration.SceneIntent != intent)
                        {
                            reason = "template-resolution-identity-drift";
                            return false;
                        }
                    }
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
