# GameObject 对象池：GameManager 模块协作警告

Status: current
StableId: es.aiwarning.runtime.gameobject-pool-gamemanager
Authority: AIWarnings；当前 ESGameObjectPoolModule 源码为事实权威。
RouteKeys: aiwarnings, runtime, pool, prewarm, spawn, despawn, lifecycle, zero-gc
Applicability: GameObject 池模块、预热、取用/归还、生命周期回调和对象池统计。
EvidenceRef: `Documentation/AIKnowledge/entries/pool-operation-skill-lifecycle.md`
Owner: ES Pool/GameManager owners。
StaleWhen: PoolModule、PushToPool/GetInPool、预热、生命周期或 Version/Lease 合同变化。

## 长期约束

- GameObject 池是 `ESGameManager` 运行模块，不属于 Item、Shot、VFX 或具体玩法；`TryGetModule<T>` 只查询，`GetOrCreateModule<T>` 仅初始化期使用，禁止恢复旧 `GetModuleFast<T>`。
- 高频 API 统一使用 `GetInPool`、`PushToPool`、`Prewarm`；Prefab 入口优先避免字符串分配，字符串 key 必须预热/注册阶段建立。
- Spawn/Despawn 必须沿 `IESGameObjectPoolLifecycle`/`ESGenericLife` 清理；回池不得只 `SetActive(false)`，必须处理粒子、Trail、输入、Tag、效果 Lease 和父级。
- 裸 `PushToPool(GameObject)` 不足以拒绝旧异步持有者；跨异步边界必须由调用方或带 Version/Lease 的 API 校验代数，禁止旧回调归还新一代实例。
- 预热完成不等于负载永不扩容；0 GC、容量、溢出销毁和性能结论必须有独立证据。池化对象不得绕过模块、Owner 生命周期或 Resource Scope。
- 详细池组计数、预热取消、生命周期回调、Operation/Skill Track 交互和失败模式由 `es.project.pool-operation-skill-lifecycle.v1` Knowledge 承接，不授予运行或发布权限。
