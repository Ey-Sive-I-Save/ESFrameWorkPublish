# ESResourcePlan × Scope：商业项目验收标准

Status: current
StableId: es.aiwarning.validation.resourceplan-scope-commercial-acceptance
Authority: AIWarnings；ResourcePlan/Scope 当前源码与验收回执为事实权威。
RouteKeys: aiwarnings, validation, resourceplan, scope, provider, lease, release, acceptance
Applicability: ResourcePlan 生命周期、Plan/Temporary/Registry Scope、Provider 重建、取消、并发和发布准入。
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-validation-resourceplan-extension-boundary.md`
Owner: ES Resource/Release owners。
StaleWhen: ResourcePlan、Scope、Provider、Lease、Registry 或验收矩阵变化。

## 长期约束

- RuntimeBackend 是资产/Bundles 的唯一实际引用计数者；Plan 只持有自身内部 Scope，外部 Scope 只拥有自己的 Plan retain，Dispose 不得释放其他所有者。
- 释放必须经过 releaseDelay 与安全点；`releaseOnExit=false` 仅显式 Release 归还；Provider 重建须先使旧 Context/Scope/Token 失效，禁止旧回调写入新表。
- 运行时寻址保持 ConfigKey → AssetIdentity，不得恢复 GUID 回退加载；取消、迟到完成、重复 Dispose、并发 Apply/Release 和失败重试必须保持代际与计数平衡。
- Temporary Lease、Registry Scope、Resident/Owner/Plan/Scene 等所有权彼此独立，不得借用另一域的计数或释放权威。
- 验收等级必须区分代码可编译、Editor PlayMode、IL2CPP Player 与商业准入；任何 retain 泄漏、重复扣减、旧 Provider 回写或安全点竞态均不得升级。
- 本 Warning 只保留不可改变的生命周期与证据边界；完整 P1-P10/T1-T7/R1-R11 矩阵、记录字段、并发取消标准和发布步骤由 Knowledge 承接，不授予远端发布权限。
