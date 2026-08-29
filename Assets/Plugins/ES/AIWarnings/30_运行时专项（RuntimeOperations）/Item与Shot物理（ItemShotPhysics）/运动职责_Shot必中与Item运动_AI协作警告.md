# 运动职责：Shot 必中与 Item 运动协作警告

> Status：current；StableId：`es.aiwarnings.runtime.shot-motion-responsibility`
> Authority：`AIWarnings`；RouteKeys：`aiwarnings`、`runtime`、`item`、`shot`、`motion`、`must-hit`
> Applicability：Entity/Item/Shot 运动模块、必中语义、Tick 与配置入口。
> EvidenceRef：`Documentation/AIKnowledge/entries/aiwarning-runtime-shot-motion-responsibility.md`；当前源码 SourceRefs。
> Owner：ES Runtime Operations；StaleWhen：ShotMotion、ItemDataInfo、碰撞查询、Tick 策略或 Operation 边界变化。
> Knowledge：`Documentation/AIKnowledge/entries/aiwarning-runtime-shot-motion-responsibility.md`

## 不可下放的长期边界

- Entity 管生命体运动，Item 管世界逻辑体，Shot 只是 Item 的飞行能力；运动层只负责飞行、追踪、碰撞/到达、过期和停止并输出候选事件。
- 伤害、Buff、VFX、音效、Pool、复杂剧情和全局调度由外层消费；高频 Tick 不跑 Op 链、反射、LINQ、字符串查找、临时数组或每帧 GetComponent。
- `MustHit` 表示战斗层已决定命中，Shot 到达目标点时产生命中候选，不等价于跳过架构；`WorldOnly` 等阻挡语义仍需真实 Layer 证据，不能冒充已实现。
- Shot 的共享模板与每发变量必须分离，`ItemDataInfo` 是统一配置入口；不得恢复旧大根或把每发状态写回共享资产。

详细参数、接口替换点、配置映射、源码事实和历史迁移说明见 Knowledge；静态证据不得升级为运行时或发布通过。
