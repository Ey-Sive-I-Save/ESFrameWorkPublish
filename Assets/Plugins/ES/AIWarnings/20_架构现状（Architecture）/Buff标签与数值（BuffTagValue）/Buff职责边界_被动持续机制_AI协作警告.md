# Buff 职责边界：被动持续机制

Status: current
StableId: es.aiwarning.runtime.buff-passive-lifecycle-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, runtime, buff, passive, lifecycle, effect
Applicability: Buff 定义、Buff Op、BuffLogic、Skill、AI、Interaction、Area 与装备效果
Owner: ESFramework Buff 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-buff-passive-lifecycle-boundary.md
StaleWhen: Buff 生命周期、Op 编排、BuffLogicRuntime 或效果归还实现变化。

## 长期约束

- Buff 是挂在对象上的、可叠加/撤销的持续机制；不拥有独立施放入口、瞄准、输入、资源消耗、冷却或主动发起权，不得成为第二套 Skill 系统。
- Skill/AI/Interaction/Area/Equipment 决定发起、目标、消耗和冷却；Op 只编排一次动作。Buff 管理持续时间、层数、来源隔离、组冲突、刷新、Tick、结束与自身资源归还。
- 普通中毒、减速、无敌和数值效果优先使用 Tag、ValueChange、Permit、Tick 与生命周期 Op；仅独占状态机制使用 BuffLogic。BuffLogicRuntime 只属于一个 Active Buff，不能跨实例共享或把状态写回 Logic 定义。
- Buff 不轮询找目标、不保存外部短生命周期 TargetPack/Support；长期快照使用完整 `TryCopySnapshotFrom()`。Op 不记录 Buff 层数、时间或 Lease。
- 普通 Buff Tick 禁止新增分配、反射、LINQ、闭包、字符串解析和全局扫描；运行时证据不足时不得宣称 0 GC 或完整验收。

## Knowledge 导航

完整职责矩阵、合法回调、组合示例、BuffLogic 生命周期和性能规则见 `es.aiwarning.runtime.buff-passive-lifecycle-boundary.v1`。
