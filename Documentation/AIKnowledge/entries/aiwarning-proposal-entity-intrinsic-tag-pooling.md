# 提案：Entity 固有 Tag、Prefab 入口与池化闭环验收

`KnowledgeId`: `es.aiwarning.proposal.entity-intrinsic-tag-pooling.v1`  
`Authority`: `AIWarnings proposal + current Entity/Tag source`  
`RouteKeys`: `aiwarnings`, `proposal`, `entity`, `tag`, `datainfo`, `prefab`, `pooling`, `snapshot`, `acceptance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `4240f6f4bb8634d6809d8e095248e0286f69ba7e7c5f18d8959251827ed2770a`  
`SourceSetHash`: `4240f6f4bb8634d6809d8e095248e0286f69ba7e7c5f18d8959251827ed2770a`  
`EntryBodyHash`: `8adfda81720ae36a500c32198a32118369d4eec1b2bd6b40e5379c84cc8fd676`  
`StaleWhen`: `Entity/Tag/DataInfo/Prefab/Pool 源码、快照合同或验收证据变化。`

## 保真迁移

原提案 94 行、4,960 UTF-8 字节；现 Warning 保留 proposed 状态、固有/临时 Tag 所有权、池化 generation 和运行验收边界。状态机、快照规则、实现证据和测试矩阵迁移至本条目，不能把代码已存在写成运行验收已通过。

## 权威模型

- Actor/Monster/Npc DataInfo 的直接 `tags` 是出生固有事实的唯一配置入口；Prefab 仅引用定义。Entity 持有定义引用、`intrinsicTags` 与 `intrinsicTagLeases`，不复制配置。通用池模板可由租出方显式 `BindDefinition`。
- Buff、装备、区域和状态投影各自管理 Lease；`ESTagCollection` 只负责聚合 Count、HotSlot/Sparse、查询、快照和 Link，不感知业务对象或权限。固有与临时来源可以叠加，释放各自归还。
- `ApplyIntrinsicTags` 状态为 `Empty`、`Pending`、`Applied`、`Failed`。相同定义不抖动；替换先申请候选再归还旧 Lease，失败保留旧事实。Catalog 未绑定时订阅并延迟重试。

## 池化与快照

回池结束 Buff/固有 Lease、推进 Entity/Tag generation、调用 `ResetForReuse`；迟到 Lease 不能污染下一轮，且稳态路径不创建临时集合。稳定快照排除固有 Tag，只传 `ESTagStableReference` 与 `SchemaHash`；恢复先绑定定义并重建固有 Lease，再恢复 Buff/装备/任务等临时 Lease。

## 验收矩阵与边界

必须在 Unity Test Runner 覆盖：固有 Tag 与两个 Buff 叠加逐一释放、Catalog 延迟且只申请一次、重复回池/取出、专用 Prefab 与通用池、定义切换、快照恢复和热路径 GC。当前仅有源码/静态证据，未运行 Unity EditMode、PlayMode、Profiler、IL2CPP Player 或发布流程。GameCore/RuntimeData 反向持有 Prefab/Scene 的 P0 资源依赖迁移不属于本提案。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterIdentity.cs`
- `Assets/Scripts/ESLogic/Runtime/Tag/ESTagCollection.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESTagCatalogRuntimeTests.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/Entity固有Tag_DataInfo权威_Prefab入口与池化闭环_提案.md` (`bf8d9667929ce892e31f82f4cccfcd69f418199f6e0db505313ba360ae163ee4`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterIdentity.cs` (`11c0b7b888ca34faa87cee7afc2dc87db5452781ca5222f6111f9e0822b03304`)
- `Assets/Scripts/ESLogic/Runtime/Tag/ESTagCollection.cs` (`77d172c9fb88a7ec84a60a67b0f7846fe38d79583da7e0b633c2e97cf4c2b980`)
- `Assets/Plugins/ES/1_Design/Tests/ESTagCatalogRuntimeTests.cs` (`7a7e7ecba2fef233b2d487cd32d52cc4c3530b606fb0732e881e4416810f4c90`)
