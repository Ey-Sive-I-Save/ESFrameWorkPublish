# ResourcePlan 扩展协议
Status: current
StableId: es.aiwarning.validation.resourceplan-extension-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, validation, resourceplan, extension, bake, publish, runtime
Applicability: ResourcePlan 的可选 Editor Bake 扩展、Publisher 边界与 Runtime Extension Lease
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-validation-resourceplan-extension-boundary.md
StaleWhen: ResourcePlan 扩展接口、Bake/Publish/Runtime 生命周期或 SourceRef 变化。

- 可选扩展不得修改 ES 资源发布核心、平行下载/缓存/引用计数；Bake 必须经稳定唯一 ProviderId 烘焙为快照，Player 不得重新扫描来源配置。
- Publisher 只校验 Bake 产物、资源闭包和发布计划；当前不存在 `IESResourcePlanPublishExtension`，不得虚构未实现接口。
- Runtime 扩展必须随统一 Plan 生命周期 Prepare/Release，Lease 按登记逆序释放；不得跨 Plan/Provider/场景代际缓存或自行创建 Scope、Provider、下载器和引用计数。
- 禁止字符串路径绕过 Catalog、Player 反射来源配置、ProviderId 重复覆盖及扩展缺失静默跳过已配置来源。Knowledge：`es.aiwarning.validation.resourceplan-extension-boundary.v1`。
