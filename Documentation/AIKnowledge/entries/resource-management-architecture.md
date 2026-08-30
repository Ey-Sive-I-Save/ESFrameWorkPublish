# 资源加载架构下的资源管理方案

`KnowledgeId`: `es.project.resource-management-architecture.v1`
`Authority`: `Current ES resource source + AIWarnings P0`
`RouteKeys`: `resource`, `manifest`, `provider`, `scope`, `resource-plan`, `lease`, `runtime`
`ContentHash`: `b146bf82f7cc29114cc2396bf24c6baef28cdc5bf33813b0c292fa752034cea1`
`EvidenceLevel`: `S1`

## 权威链

Editor Library → Catalog/ReferenceGraph → ResourcePlan/Bake → Manifest/BundleIndex/RuntimeMap → Provider → Loader → AssetScope。Runtime 不扫描 Library、AssetDatabase 或项目目录。

## 管理原则

- ResourcePlan 管理计划级 retain、预热、取消和释放事务。
- Scope 只释放自身拥有的 Handle/Lease；ReferenceCount 与 LeaseCount 分离。
- Provider 切换推进 generation；旧代迟到结果不得写回新状态。
- Manifest、BundleIndex、Hash、CRC、依赖和平台必须在加载前校验。
- AssetPackage 只负责 Editor 聚合、预览、暂存和导出意图，不替代 Runtime Provider。

## 失败与恢复

缺 Manifest、依赖环、Hash/CRC 不匹配、LocalBuild 缺本地发布入口、半加载 Context 或重复释放必须失败闭合；失败/取消事务不能复活，需新建事务并保留可重放证据。

## Non-claims

S1 只证明源码和规则边界，不证明 Player、下载、Profiler、IL2CPP 或商业发布。

## SourceRefs

- `Documentation/AIKnowledge/entries/resource-pipeline-runtime.md` (`75d10298ffb5518f23353d9fed016fb643322da928c599c1f854153b11ea5593`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs` (`d1551ea4cbc8bccefd7f24038548fa2d650b70ffe6815600f7614b9a543d5ade`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetProviderFactory.cs` (`c63353575fa5f1824cdd4399965efbe47b3b47d0d298f89bf1f9f659f8212ee4`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESRuntimeAssetLoader.cs` (`f0f8ea295b57a8527d2c664ff4b19dc27c0497dab4f6959601df6de860835a6c`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs` (`b7d63add470de84de3516c374a5f85d41fb1f74181946664b520ec753b153b22`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/资源计划验收（ResourcePlanAcceptance）/资源计划_Scope生命周期绑定_商业项目验收标准.md` (`27962ad8eb6b2674e1b759448708afc316b913fcf20945b80a83fc44111b5acf`)

`StaleWhen`: Provider、Manifest、Scope、ResourcePlan 状态机或释放安全点变化。
