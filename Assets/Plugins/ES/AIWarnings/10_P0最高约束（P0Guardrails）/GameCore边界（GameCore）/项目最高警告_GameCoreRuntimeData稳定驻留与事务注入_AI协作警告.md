# 项目最高警告：GameCore RuntimeData 稳定驻留与事务注入

Status: current
StableId: es.aiwarning.p0.gamecore-runtimedata-retention-transaction.v1
Authority: AIWarnings（长期 P0 约束）；详细事实与原文快照见 Knowledge
RouteKeys: aiwarnings, p0, gamecore, runtime-data, retained, transaction, ready, runtime-key
Applicability: GameCore RuntimeData、强类型 Table、InjectWith*、根 SO 注入、Consumer 与资源安全点
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-gamecore-runtimedata-retention-transaction.md
StaleWhen: RuntimeData/Table/InjectWith*、Ready/RuntimeKey、载荷释放、根 SO 注入或任一 SourceRef 哈希变化。

## P0 长期约束

- RuntimeData 是按业务 Key 稳定驻留的定义外壳，不是短生命周期实例；禁止 `IPoolableAuto`、对象池、同 Key 换实例和 Upsert 覆盖。Clear/Remove/Consumer 切换只释放重量级载荷并置 `Ready=false`，下一次注入复用同一外壳。
- 标准流程固定为 `AcquireRetained → try 准备全部载荷 → CommitRetained/TryCommitRetained → 写入实际 runtimeKey → Ready=true`；任何准备异常、Try 提前失败或放弃都必须幂等 `AbandonRetained`，不得把准备逻辑放到 try 外。
- 成功提交后先写实际槽位 RuntimeKey，最后 Ready；`MarkNotReady` 先置 false 再 `ReleaseRuntimePayload`。Ready=false 时禁止读取业务载荷，旧引用只能用于诊断/重新检查 Ready。
- 领域表必须复用 `ESRetainedConfigKeyTable<T>` / `ESGameCoreConfigKeyTable<T>` 的驻留与事务算法，不得复制 retained 映射或用普通 `ESConfigKeyTable<T>` 替代；底层入口与普通业务入口保持边界。
- RuntimeKey 仅限当前表、Catalog 生命周期和进程；存档、网络、Manifest、Catalog、SO 只保存 EnumKey/StringKey 或资产身份。禁止注册顺序、InstanceID、GUID、路径、显示名恢复或隐式创建。
- `ReleaseRuntimePayload` 必须断开 SO、SharedData、ExtraAsset、集合等重量级引用；Asset Lease/Handle 由 AssetScope 统一 Dispose，RuntimeData 不得重复释放。正常查询保持强类型字典 O(1)，Abandon 扫描只在失败冷路径发生。
- AI/Player 内容只能使用强类型稳定 Key 与结构化参数；RuntimeKey、Handle、InstanceID、委托、自由字符串和裸 Unity 对象不是权威输入。

## Knowledge 导航

详细模板、三层调用边界、Ready/载荷清理清单、RuntimeKey 规则、性能约束、权威源码入口、验收项及迁移前完整原文快照见 `es.aiwarning.p0.gamecore-runtimedata-retention-transaction.v1`。本 Warning 不授予写入、注入、运行时或发布权限。
