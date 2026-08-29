# 提案：Entity 固有 Tag、Prefab 入口与池化闭环
Status: proposed
StableId: es.aiwarnings.proposal.entity-intrinsic-tag-pooling.v1
Authority: ESFramework AIWarnings / proposal
RouteKeys: aiwarnings, proposal, entity, tag, datainfo, prefab, pooling, snapshot, acceptance
Applicability: Entity、Actor/Monster/Npc DataInfo、Prefab Profile、ESTagCollection 与对象池生命周期
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-proposal-entity-intrinsic-tag-pooling.md`
StaleWhen: Entity/Tag/DataInfo/Prefab/Pool 源码、快照合同或验收证据变化
Knowledge: `es.aiwarning.proposal.entity-intrinsic-tag-pooling.v1`

## 提案边界（代码已实现，运行验收未完成）
- DataInfo 是固有 Tag 唯一配置权威；Entity 只持有定义引用、直接 tags 引用和 intrinsicTagLeases，Prefab 不复制 Tag 列表；Buff/装备/区域等临时来源各自持有 Lease。
- `ApplyIntrinsicTags` 支持 Pending/Applied/Failed/Empty；替换先申请候选再释放旧 Lease，失败保留旧事实；`ReferenceCount` 与 Lease 生命周期分离，禁止恢复单字段 Tag 包装。
- 回池结束本轮 Lease、推进 generation、重置 Tag 聚合；迟到释放不得污染下一轮。稳定快照只传 StableReference/SchemaHash，恢复先固有后临时。
- 这是待验收提案：必须覆盖叠加、延迟 Catalog、重复 Apply、定义切换、回池复用、Prefab/通用池和快照恢复，并执行热路径 GC；不得将 P0 资源依赖重构混入本项。

详细状态机、代码证据、测试矩阵与未验证声明见 Knowledge。
