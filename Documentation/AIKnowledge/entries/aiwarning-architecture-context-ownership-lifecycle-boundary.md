# ES Contextitecture 所有权与生命周期边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.arch.context-ownership-lifecycle-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Runtime/Context 实现  
`RouteKeys`: `aiwarnings`, `architecture`, `context`, `ownership`, `lifecycle`, `type-boundary`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `7049ef0e65c842fd832ae99c8fca6d44bbcc8e0ad731b1828821111c8442f41a`  
`SourceSetHash`: `7049ef0e65c842fd832ae99c8fca6d44bbcc8e0ad731b1828821111c8442f41a`  
`EntryBodyHash`: `2e71d1e5b277b95fdfa5ed2987b0e99f3765145150840f7fd7597191424e9e6e`  
`StaleWhen`: ContextPool、值池、Link 通道、清理或跨线程约束变化。

## 迁移范围

Warning 保留 Context 的局部语义、宿主所有权、清理、类型和线程边界；本条目保存实现事实、值类型表、事件/性能/扩展细节、系统分工、验收缺口和原文快照。

## 实现与所有权

`ContextPool` 按字符串 Key 持有 `IContextitectureValue` 和初始化原型；值由 `ESContextitectureValuePools` 的 `ESSimplePool` 租借/归还。`WillSendLink` 控制当前 Pool 的类型化 Link 通道，Float/Bool/Tag 独立。`ClearNonPersistentRuntimeValues()` 只清非持久值，`ClearAllRuntimeValues()` 清全部；`TryAddSameContextValueFromContextValue()` 复用对象，Copy 变体申请独立副本。这些事实不构成 Lease、generation、跨宿主所有权或线程安全保证。

每个 Pool 由一次 Operation、Skill 执行或局部流程唯一持有，创建者负责完整清理。宿主结束、取消、失败、回池或销毁时必须 ClearAll。`persistent` 仅是本 Pool 清理策略，不是跨 Entity/Item/Scene/存档/网络/Pool 租期持久化。跨域效果应以稳定数据重建本地值或使用领域 Lease/Scope/Handle。普通运行时用 Copy；Same 仅限非池化序列化原型，池化值不得跨 Pool 共享。租借值不得绕过 Pool 进入容器/静态字段，归还后不得读写。

## 类型、事件与性能

Float/Int/Bool/Vector3 用局部参数与执行期计算；String 用局部文本/稳定 Key；Object/ClassT/UnityObject 只是上下文借用引用；DynamicTag 仅限旧的 Context 局部语义。类型转换便利不改变业务语义，Key 选定类型后不得跨类型冒充。Link 仅通知本 Pool 内创建、变化、移除；订阅者不得持有将被清理的引用或写入已结束宿主，订阅建立者须在宿主结束前解绑，不能复制 Tag/Stat/Buff/输入系统。

预热字典/值池下的局部读写可低分配，但字符串字典不是固定 HotSlot；首次租借、扩容、新 Key、拼接、订阅和异常日志可能分配。新增值类型必须实现 Prepare、Reset、自动归还、类型化读写、Link、Copy、移除、重复清理和异常测试。Context 仅 Unity 主线程。

## 系统分工与验收

局部参数→ContextPool；可组合事实→`ESTagCollection`；角色/物品数值→Attribute Catalog/ValueChange；资源生命周期→Resource Scope/TemporaryLease；跨来源控制权→Request/Lease/Arbitration。当前尚无完整 Unity Test Runner 证据覆盖跨域引用、取消回调、域重载、长时间池化、存档和网络；不得把已有类型化值、对象池和 Link 通道当作这些场景已验收。

## 原文快照

迁移前原始文件为 64 行、5148 UTF-8 字节，原始 SHA-256 为 `ba38596bdf67ef81bb7179bb0f0345ef896a216e73d72e83e821df4e2dbc4f6e`。本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/Contextitecture上下文系统_所有权生命周期与类型边界_AI协作警告.md` (`e230624a6d6ef6f9c646eb105f051ed9dd416232b1aadac23aa2bb15be10a2e9`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`69d73fa0cf1ad80564057d69b439fba223dc93c05e5627564ff40e41b6f746c3`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-architecture-context-ownership-lifecycle-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/通用架构（GeneralArchitecture）/Contextitecture上下文系统_所有权生命周期与类型边界_AI协作警告.md`
