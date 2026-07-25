namespace ES
{
    /// <summary>
    /// 资源计划的集中配置入口。
    /// Group 只管理 PlanKey 到计划 SO 的编辑器/配置关系；资源条目只保存稳定 ConfigKey。
    /// </summary>
    [ESCreatePath("数据组", "资源计划组")]
    public sealed class ESResourcePlanGroup : SoDataGroup<ESResourcePlanInfo>
    {
    }
}
