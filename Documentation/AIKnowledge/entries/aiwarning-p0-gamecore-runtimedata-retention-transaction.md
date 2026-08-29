# GameCore RuntimeData 驻留与事务注入：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.gamecore-runtimedata-retention-transaction.v1`  
`Authority`: `AIWarnings` 与当前 GameCore RuntimeData/Table 实现  
`RouteKeys`: `aiwarnings`, `p0`, `gamecore`, `runtime-data`, `retained`, `transaction`, `ready`, `runtime-key`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `8a124f561a00d905e0664bc7c2fc4683bf3993d180ed2ad84c711c80b0d0afaa`  
`SourceSetHash`: `8a124f561a00d905e0664bc7c2fc4683bf3993d180ed2ad84c711c80b0d0afaa`  
`EntryBodyHash`: `256572274f4df1da15031293716eb1a44eb6e9838fc124198a9f91cad7e9cb94`  
`StaleWhen`: RuntimeData/Table/InjectWith*、Ready/RuntimeKey、载荷释放、根 SO 注入或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留长期 P0 边界；本条目承载被压缩出的完整模型、调用模板、清理规则、源码入口、验收清单和迁移前原文快照。Knowledge 只提供可追溯事实，不授予注入、运行时或发布权限。

## 保真事实

RuntimeData 是按业务 Key 稳定驻留的定义外壳，不是短生命周期实例；Clear/Remove/Consumer 切换只释放重量级载荷并置 `Ready=false`，同 Key 下一次注入复用原外壳。标准闭环是 `AcquireRetained → try 准备全部载荷 → CommitRetained/TryCommitRetained → 写实际 runtimeKey → Ready=true`；准备异常、Try 提前失败和放弃均幂等 `AbandonRetained`，准备逻辑不得在 try 外。

提交成功先写实际槽位 RuntimeKey，最后 Ready；`MarkNotReady` 先置 false 再 `ReleaseRuntimePayload`。Release 必须断开 SO、SharedData、ExtraAsset、集合等重量级引用，Asset Lease/Handle 由 AssetScope 统一 Dispose。Ready=false 禁止读取业务载荷，RuntimeData 禁止池化、同 Key 换实例和 Upsert。

领域表复用 `ESRetainedConfigKeyTable<T>` / `ESGameCoreConfigKeyTable<T>` 的驻留与事务算法，不复制 retained 映射；普通业务只通过强类型查询和 `InjectWith*`，底层 Acquire/Commit 仅由领域 Table 使用。RuntimeKey 仅属于当前表/Catalog 生命周期和进程，持久化与网络只保存 EnumKey/StringKey 或资产身份；禁止顺序、InstanceID、GUID、路径、显示名恢复及隐式创建。

AI/Player 内容只使用强类型稳定 Key 与结构化参数；RuntimeKey、Handle、InstanceID、委托、自由字符串和裸 Unity 对象不是权威输入。查询保持强类型字典 O(1)，Abandon 扫描只在失败冷路径发生，禁止为事务引入协程、反射、中央工厂或热路径委托分配。

## 当前入口与验收

权威入口：`Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`、`Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs`、`Assets/Scripts/ESLogic/Data/GameCoreConfigKey/`、`Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs`。验收重点为 Acquire/Commit/Abandon 异常闭环、Ready 与载荷清理、同 Key 身份稳定、实际 RuntimeKey 写入、O(1) 查询及 UTF-8/diff 检查；Unity/EditMode 属运行时证据，本轮未执行。

## 原文快照

迁移前台账快照：175 行、8527 字节，原始 SHA-256 `3d237b03c1b8acf59368e6293a374010e624ede948299351b0b6b268e432a34b`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md` (`af48ba0543b77cfcd97bd9515576f06d7e09c41038bf5094f18c1ca167e0bcd6`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`91dc9df2aa6ec528145707465f9085117b1ae5f11cc89d2904afe3962c3dded0`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-gamecore-runtimedata-retention-transaction.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md`
