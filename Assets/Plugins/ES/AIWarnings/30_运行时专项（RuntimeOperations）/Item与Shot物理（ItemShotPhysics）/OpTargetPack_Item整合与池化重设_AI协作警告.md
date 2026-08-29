# Op TargetPack：Item 整合与池化重设警告

> Status：current；StableId：`es.aiwarnings.runtime.op-targetpack-item-pooling`
> Authority：`AIWarnings`；RouteKeys：`aiwarnings`、`runtime`、`item`、`shot`、`pool`、`target-pack`
> Applicability：Item/Entity 目标装配、Operation 借用和 TargetPack 回池。
> EvidenceRef：`Documentation/AIKnowledge/entries/aiwarning-runtime-op-targetpack-item-pooling.md`；当前源码 SourceRefs。
> Owner：ES Runtime Operations；StaleWhen：TargetPack、Item 表达式、目标 Operation 或池化合同变化。
> Knowledge：`Documentation/AIKnowledge/entries/aiwarning-runtime-op-targetpack-item-pooling.md`

## 不可下放的长期边界

- `ESRuntimeTargetPack` 的 `userItem`、`itemMainTarget`、`targetItems` 是正式 Item 路径；禁止把 Item 仅塞入 `extras`。
- 回池必须清空 Entity/Item 目标、轻量运行时槽位和 `extras`；`OnResetAsPoolable` 必须保持幂等，防止跨租期引用污染。
- 创建层持有池租约并负责归还，Operation 只借用；长期 Owner 用 Pack 引用与 `Version` 联合校验，不能只看 `IsRecycled`。
- 高频 Tick 不临时扩容列表、不启用 `extras`，也不把伤害、VFX 或 Pool 控制混入 TargetPack。

详细字段、源码映射、容量策略、使用示例、原文快照和失败恢复见 Knowledge；静态证据不得升级为 Runtime/Profiler 通过。
