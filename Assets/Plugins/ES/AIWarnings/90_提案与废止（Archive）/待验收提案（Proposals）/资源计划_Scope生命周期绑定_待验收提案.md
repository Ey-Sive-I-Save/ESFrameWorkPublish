# 提案：ESResourcePlan × Scope 生命周期绑定

Status: proposed
StableId: es.aiwarnings.proposal.resourceplan-scope-lifecycle.v1
Authority: ESFramework AIWarnings / proposal
RouteKeys: aiwarnings, proposal, resourceplan, scope, lifecycle, release, acceptance
Applicability: ResourcePlan、Scope、Provider、Binder 生命周期设计与验收
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-proposal-resourceplan-scope-lifecycle.md`
StaleWhen: ResourcePlan/Scope/Provider 源码、生命周期合同或验收矩阵变化
Knowledge: `es.aiwarning.proposal.resourceplan-scope-lifecycle.v1`

## 提案边界（未验收）

- 生命周期 Scope 持有 Plan retain，Plan 持有内部资源 Scope，Provider 仍拥有真实资产/Bundle 引用计数；Scope 结束只归还自身 retain，最后归零后延迟释放和安全点卸载。
- ReferenceCount 与 LeaseCount 分离；取消只回滚本次获得的 retain；Provider 重建推进 generation，旧回调不得写回；`releaseOnExit=false` 保持显式释放语义。
- 建议覆盖单/多 Scope、重复 Apply/Release、Binder、取消、延迟重进、Provider 重建、常驻计划等 P1-P10 场景，并记录 State/RetainCount/Provider 引用和 Console/Profiler。
- 这是待验收提案，不能声称 Editor PlayMode、IL2CPP Player、下载、缓存、安全点或发布已通过；不得新增第二套 Loader、Handle、引用计数或 GUID/RuntimeKey 后门。

详细场景矩阵和准入标准见 Knowledge。
