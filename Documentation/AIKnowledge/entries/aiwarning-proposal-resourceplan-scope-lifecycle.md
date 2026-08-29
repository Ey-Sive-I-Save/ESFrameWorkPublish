# 提案：ResourcePlan 与 Scope 生命周期绑定验收

`KnowledgeId`: `es.aiwarning.proposal.resourceplan-scope-lifecycle.v1`  
`Authority`: `AIWarnings proposal + current ResourcePlan source`  
`RouteKeys`: `aiwarnings`, `proposal`, `resourceplan`, `scope`, `lifecycle`, `release`, `acceptance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `bb0173965c6c88e036b8544c4f53103e8efa0e20e4c3b9423e6038ef52f92f59`  
`SourceSetHash`: `bb0173965c6c88e036b8544c4f53103e8efa0e20e4c3b9423e6038ef52f92f59`  
`EntryBodyHash`: `da47b1daab3628b07ee60966b6c636e77ea6de56527f2e87d85d9276d0c6050f`  
`StaleWhen`: `ResourcePlan/Scope/Provider 源码、生命周期合同或验收矩阵变化。`

## 保真迁移

原提案 95 行、7,682 UTF-8 字节；现 Warning 保留提案状态、生命周期不变量和未验收边界。P1-P10 场景矩阵、证据字段、商业级收口和准入标准迁移至本条目，不能把提案写成当前实现。

## 生命周期模型

- `Scope → Plan retain → Plan 内部资源 Scope → RuntimeBackend 资产/Bundle`。RuntimeBackend 仍拥有实际引用计数；Plan/Scope 只管理生命周期持有。最后一个 retain 归零进入 `ReleasePending`，冷却内重进复用 Context，安全点才可卸载。
- 普通 Scope 同一 identity 至多一次；显式 Scope 可多 retain 且每份只归还一次。`ReferenceCount` 与 `LeaseCount` 独立，取消只回滚本次 newly-acquired retain。`releaseOnExit=false` 仅显式 Release，不能被 Binder 擅自改写。
- Provider 切换/重建阻止新请求、推进 generation，旧 Scope/结果不得写回新状态；父 Scope 关闭时子 Scope 先关。同步 `ReleaseScope` 代表逻辑关闭，不等于物理请求全部静默。

## 建议验收与准入

- P1-P4 覆盖单 Scope、Plan 单独释放、同 Scope 重复 Apply、多 Scope 重叠；P5 Binder 零代码；P6-P7 取消回滚；P8 冷却重进与安全点；P9 Provider 重建；P10 手动常驻。记录 State、RetainCount、RequiredFailureCount、Provider 引用、Console、耗时和 GC。
- Required 失败必须无残留 retain，Optional 失败只报告；P8 拆为 Ready 冷却复用与加载中离开收尾。真实慢资源和真实 Provider 重建必须使用，禁止 Mock 代替。
- 准入要求所有 P1-P10 通过，并至少在 Editor PlayMode 与 IL2CPP Player 各执行 P6/P7/P9；当前未运行 Unity/Player/下载/发布。提案不新增 Loader、业务 Handle、弱类型入口或 GUID/RuntimeKey 后门。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeExtensions.cs`
- `Assets/Plugins/ES/1_Design/Link/Pool_Container/ActiveLinkList.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/资源计划_Scope生命周期绑定_待验收提案.md` (`26b9879ca154921479a2e6f6574a932f8b6beef4d519c567efc59c7e5d50257f`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeService.cs` (`b7d63add470de84de3516c374a5f85d41fb1f74181946664b520ec753b153b22`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESResourcePlanRuntimeExtensions.cs` (`c5f2f549fd8d1d0051dcd2ac15343e1fb7ee249e85fd7ea63c75d3f2bd211649`)
- `Assets/Plugins/ES/1_Design/Link/Pool_Container/ActiveLinkList.cs` (`8e0b83291545c91f91002c8253f52404cb7cea73faa51608450d3cce854f79cc`)
