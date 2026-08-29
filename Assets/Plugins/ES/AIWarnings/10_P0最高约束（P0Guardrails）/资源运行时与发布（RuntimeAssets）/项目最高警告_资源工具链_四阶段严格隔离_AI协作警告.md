# 项目最高警告：资源工具链四阶段严格隔离

Status: current
StableId: es.aiwarning.p0.asset-pipeline-four-stage-boundary.v1
Authority: AIWarnings（长期 P0 约束）；详细事实与阶段验收见 Knowledge
RouteKeys: aiwarnings, p0, assets, resource-pipeline, bake, plan, build, publish, staging
Applicability: 资源引用烘焙、AB 规划/构建、本地发布、远端发布和资源管理窗口
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-asset-pipeline-four-stage-boundary.md
StaleWhen: 阶段 API、产物 Schema、菜单入口、Provider 状态、发布协议或任一 SourceRef 哈希变化。

## P0 长期约束

- 阶段必须独立且按顺序：`ESAssetReferenceBaker` 只分析并输出引用 Catalog/Graph；`ESAssetBundleBuildPlanner` 只消费烘焙结果并规划/标记 AB；`ESAssetBundleBuilder` 是唯一可调用 `BuildPipeline.BuildAssetBundles` 且只写 staging；`ESAssetBundlePublisher` 只校验 staging 并最后原子写 Release Manifest；远端发布只消费上传计划并执行预检、上传、验证。
- Baker 禁止改标签/构建/复制发布文件；Planner 是唯一可改 ES 管理范围 AB 标签的阶段；Builder/Publisher/远端发布不得回头执行前序职责。失败不得伪造下阶段产物或用旧产物冒充本次成功。
- 资产物理身份固定为主资源 `GUID + LocalFileId(0)`、子资源 `GUID + LocalFileId`；EnumKey/StringKey 只在资产类型内寻址，不能替代全局物理映射。业务 Key、BundleKey、发布文件身份保持边界。
- 唯一用户入口为 `【ES】/资源与发布/资源管理/资源管理窗口`，五个动作必须有明确阶段和前置产物验证；废弃 `ES/Resource/*`、`Window/ES/*`、`Tools/ES/*` 入口不得恢复。
- Provider 状态必须如实：ManualPlan 可用但不联网；Aliyun OSS 尚缺真实 Bucket 验收；S3Compatible、HttpPut、ExternalCommand 未实现。不得把接口或菜单写成已可用。
- Root Manifest 使用 `no-cache` 并最后切换；版本 Bundle/索引可 immutable 缓存；上传用 HEAD 校验长度与 `x-oss-meta-es-sha256`，ETag 不是内容 Hash。不得手工改 Unity 生成 `.csproj`。

## Knowledge 导航

详细阶段产物、调用边界、Provider/缓存规则、废弃菜单和验收清单见 `es.aiwarning.p0.asset-pipeline-four-stage-boundary.v1`。本 Warning 不授予构建、网络、发布或 Unity 执行权限。
