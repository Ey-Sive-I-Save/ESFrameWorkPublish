# 对象池预热、Space 与 0GC 边界
Status: current
StableId: es.aiwarning.runtime.pool-prewarm-space-0gc.v1
Authority: AIWarnings；详见 Knowledge
RouteKeys: aiwarnings, runtime, pool, prewarm, space, gc
Applicability: ESGameObjectPoolModule 的预热与热路径
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-pool-prewarm-space-0gc.md
StaleWhen: 对象池模块、PrefabPrewarmData 或 SourceRef 变化。
- Scene/Space 只是预热作用域；对象池不升级为业务调度中心。
- `GetInPool/PushToPool` 不遍历配置、不检查作用域、不拼动态 key；高频对象必须提前注册/建池，禁止 Tick 首次建池或集合扩容。
- 对象池只负责创建、预热、借还、重设、清理、统计和修补，不承载伤害、Buff、VFX 或 Shot 语义。
- 分区回收通过预热作用域和外部通知扩展，不污染热路径；完整入口与验收见 Knowledge。
Knowledge：`es.aiwarning.runtime.pool-prewarm-space-0gc.v1`
