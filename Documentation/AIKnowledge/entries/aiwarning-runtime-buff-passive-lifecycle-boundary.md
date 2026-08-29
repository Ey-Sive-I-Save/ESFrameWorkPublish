# ES Buff 被动持续机制边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.runtime.buff-passive-lifecycle-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Buff/Op 实现  
`RouteKeys`: `aiwarnings`, `runtime`, `buff`, `passive`, `lifecycle`, `effect`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `8ed88c5d1ddc42f1d10bd3e2ae1dc7b363e90e53fd82984d9571626c5e9c49ae`  
`SourceSetHash`: `8ed88c5d1ddc42f1d10bd3e2ae1dc7b363e90e53fd82984d9571626c5e9c49ae`  
`EntryBodyHash`: `8dd7146825f528eeb06f44b19532d26fd7d943a1a19e88bb5433931fa9ff9abd`  
`StaleWhen`: Buff 生命周期、Op 编排、BuffLogicRuntime 或效果归还实现变化。

## 迁移范围

Warning 保留 Buff 被动职责、发起权边界、资源归还和热路径限制；本条目保存职责矩阵、回调语义、组合示例、BuffLogic 生命周期和原文快照。

## 职责矩阵与合法行为

Skill/AI/Interaction/Area/Equipment 决定何时发起、目标、消耗、冷却；Op 编排一次动作并传递 TargetPack/来源上下文。Buff 管理持续时间、层数、来源隔离、组冲突、刷新、Tick、结束和自身资源。`OnApply` 可写 Tag/数值/Permit 或启动受控 Op；`OnRefresh` 更新自身效果；`OnTick` 按已配置时钟执行；`OnRemove` 停止 Apply Op、执行结束效果并归还资源；事件回调只累计、吸收、消耗层数或请求自身移除。这些都是被施加后的被动反应，不构成新施放入口。

Buff 不轮询找敌人、不接输入/前摇/施放/蓝量/弹药/冷却、不保存外部短生命周期 `TargetPack` 或 `ESOpSupport`。运行期使用自己创建的快照与 Support，长期快照必须 `TryCopySnapshotFrom()`，不能用部分 `CopyFrom()` 冒充完整复制。普通中毒/减速/无敌/数值加成不创建 BuffLogic，优先 Tag、ValueChange、Permit、Tick 和生命周期 Op；Op 不保存 Buff 层数、剩余时间或 Lease。

典型组合：技能命中→Targeting Op 选目标→应用 Buff Op→Buff 管理层数/时间/Tick→Tick Op 调伤害系统；装备穿戴→流程施加光环 Buff→Buff 持有 Tag/数值 Token→卸下时移除并归还资源。

## BuffLogicRuntime 与性能

`ESBuffLogic` 是可配置机制规则；`ESBuffLogicRuntime` 是单个 Active Buff 独占的状态/资源容器。框架按 `OnApply(runtime)`、`OnRefresh(runtime)`、`OnTick(runtime, deltaTime)`、`OnRemove(runtime)`、`OnRelease(runtime)` 调用；不得跨 Active Buff 共享 Runtime，不把机制决策扩大进 Runtime 工厂或把状态写回共享 Logic。

Buff 稳态 Tick 是高频路径，普通 Buff 不新增分配、反射、LINQ、闭包、字符串 Key 解析或全局扫描；复杂 Logic 仅在确需独占状态时启用，并遵守相同热路径约束。

## 原文快照

迁移前原始文件为 66 行、3645 UTF-8 字节，原始 SHA-256 为 `6f8518f81bb15330013bf7829237954c2f84f373523c3f149071f11052523f76`。本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/Buff职责边界_被动持续机制_AI协作警告.md` (`2baf4921b912a745ad9ff70bc7fdc7632139658bacbb5022c3634a553c0c0a31`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`5aff3580f6861c4d1beb70c3341b86074c791e3d97b77e8de84ec5a198b9cdb8`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-runtime-buff-passive-lifecycle-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/Buff职责边界_被动持续机制_AI协作警告.md`
