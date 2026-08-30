using System;

namespace ES
{
    /// <summary>
    /// ES 模板到后端 Dry-Run 的纯数据计划。它只描述意图和可审计投影，不执行 Unity 写入。
    /// </summary>
    public readonly struct ESRenderTemplatePlan
    {
        private ESRenderTemplatePlan(
            string planId,
            ESRenderResolvedConfiguration configuration,
            ESRenderTemplateResourceBinding resources,
            ESRenderQualityPolicy qualityPolicy)
        {
            PlanId = planId;
            Configuration = configuration;
            Resources = resources;
            QualityPolicy = qualityPolicy;
        }

        public string PlanId { get; }
        public ESRenderResolvedConfiguration Configuration { get; }
        public ESRenderTemplateResourceBinding Resources { get; }
        public ESRenderQualityPolicy QualityPolicy { get; }
        public bool IsDryRun { get { return true; } }

        public static bool TryCreate(
            ESRenderVisualStyleId style,
            ESRenderSceneIntentId intent,
            ESRenderPlatformId platform,
            out ESRenderTemplatePlan plan,
            out string reason)
        {
            return TryCreate(style, intent, platform, ESRenderContentTypeId.RolePlaying, out plan, out reason);
        }

        public static bool TryCreate(
            ESRenderVisualStyleId style,
            ESRenderSceneIntentId intent,
            ESRenderPlatformId platform,
            ESRenderContentTypeId contentType,
            out ESRenderTemplatePlan plan,
            out string reason)
        {
            ESRenderResolvedConfiguration configuration;
            if (!ESRenderTemplateCatalog.TryResolve(style, intent, platform, contentType, out configuration, out reason))
            {
                plan = default(ESRenderTemplatePlan);
                return false;
            }

            ESRenderTemplateResourceBinding resources;
            if (!ESRenderTemplateResourceMap.TryGet(style, configuration.QualityProfile, out resources, out reason))
            {
                plan = default(ESRenderTemplatePlan);
                return false;
            }

            ESRenderQualityPolicy qualityPolicy = ESRenderQualityPolicy.Resolve(configuration.QualityProfile);
            if (!qualityPolicy.IsValid(out reason))
            {
                plan = default(ESRenderTemplatePlan);
                return false;
            }

            string planId = "es.urp.template." + style + "." + intent + "." + platform;
            plan = new ESRenderTemplatePlan(planId, configuration, resources, qualityPolicy);
            reason = string.Empty;
            return true;
        }
    }
}
