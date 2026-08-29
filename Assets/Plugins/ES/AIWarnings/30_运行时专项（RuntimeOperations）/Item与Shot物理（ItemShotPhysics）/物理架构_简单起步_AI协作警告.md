# 物理查询架构边界
Status: current
StableId: es.aiwarning.runtime.physics-query-boundary.v1
Authority: AIWarnings；详见 Knowledge
RouteKeys: aiwarnings, runtime, physics, query, collision
Applicability: ESPhysicsQueryModule 与 Item/Shot/Trap/Interaction 查询
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-runtime-physics-query-boundary.md
StaleWhen: PhysicsQueryModule、查询 API 或 SourceRef 变化。
- 当前只建设可复用查询服务，不做大一统物理 Domain；业务仍由 Entity/Item/Skill/Op 消费结果。
- `TryGetModule<T>()` 只查询不创建；`GetOrCreateModule<T>()` 仅初始化期，废止 `GetModuleFast<T>()`。
- 查询须区分显式缓存与共享缓存；共享缓存非并发安全，禁止嵌套调用后长期持有其内容。
- 模块不承载角色移动、伤害、Buff、Shot 状态/必中、VFX、对象池、陷阱业务或武器释放。详细 API 与路线见 Knowledge。
Knowledge：`es.aiwarning.runtime.physics-query-boundary.v1`
