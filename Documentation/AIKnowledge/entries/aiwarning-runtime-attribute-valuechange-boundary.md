# ES 属性数值与 ValueChange 边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.runtime.attribute-valuechange-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Attribute/Effect 实现  
`RouteKeys`: `aiwarnings`, `runtime`, `attribute`, `valuechange`, `effect-lease`, `performance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `19196671fe607cce01b0fff96550a3711236ad09828d9d3a19d52b1163666857`  
`SourceSetHash`: `19196671fe607cce01b0fff96550a3711236ad09828d9d3a19d52b1163666857`  
`EntryBodyHash`: `44a9555ef35ae2cd1c3726f7f6af4276a69270e7383a7773e5c0b509fad1116d`  
`StaleWhen`: 属性 Schema、Bake/Catalog、ValueChange、EffectLease 或热路径实现变化。

## 迁移范围

Warning 保留唯一 Schema、稳定身份、Lease/generation、通知清理和热路径边界；本条目承载详细实现、生成/Bake、调试、池化和验收规则。

## 权威与生成

`ESSuperAttributeTable` 是定义权威；`GameCoreEditorGlobalData.characterAttributes/itemAttributes` 是唯一可编辑 Schema，BakeTable 只读，Catalog 负责注入/查询。Entity、Item、Buff、Prefab、DataInfo 不得复制属性类型、范围、Hot/Sparse 或显示名。`fixedApiName` 仅生成访问名，EnumKey/StringKey 才是稳定身份；空 HotSlot/Sparse 保持 Catalog 路径。生成文件、Enum/映射和默认投影只能按受控门禁更新，Bake 失败保留旧产物。Item 基础与专项投影必须各自稳定 Key、验证和事务边界。

## 运行时写入与清理

Float 用 `ESFloatValueChangeSet`，Permit 用 `ESPermitSet`；Tag/Buff/Stat 职责不可混用。外部效果持有 `ESEffectLease` 或自有 Token，写入瞬间校验 slot+generation 与 Host `ReferenceEquals`；旧 Lease、复制值、异步回调和其他 Host 只能失败。重绑定、销毁、池化前先归还外部 Lease，再清 Base/Fallback、Token、Owner 索引和 Changed 订阅；Sparse Set 移除活动 RuntimeKey。`ClearValueChanges` 期间回调只能读，禁止重建/修改集合。`BeginBatch` 只合并通知，不提供回滚。

ValueChange 状态先提交，单个 Changed 接收者异常不得阻断其他接收者、Lease 归还、Owner 清理或后续通知；业务必须自行预检。RuntimeKey/SetId/Token/OwnerId/SourceId 不进入配置、存档、网络或回放。Formula 当前必须为空。

## 性能、调试与验收

高频角色读取使用固定枚举 API；KCC/战斗 Tick/AI/UI 每帧禁止 StringKey、Catalog、Resolver、Token/Lease/List/LINQ/闭包。Hot 数组可常驻，Sparse/Lease/Modifier 索引延迟创建并预热已知 Hot 容量；首次扩容、日志、异常、快照不属于 0 GC 稳态。调试 Snapshot 是冷路径且由调用方复用容量，运行时面板只读并使用既定 `【ES】` 路径。必须回归重入、异常、Owner 释放、池化代际、Bounds、Bake 失败和 ValueChange 通知；本轮未运行 Unity/Runtime。

## 原文快照

迁移前原始文件为 58 行、8788 UTF-8 字节，原始 SHA-256 为 `8b4db36fcf4a870ec2b7a67eaff3aa90478b374f1d4fc019575406355fc4d505`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/属性数值与ValueChange边界_AI协作警告.md` (`5ced4eac1aae28b177afad9ff378042d524126de7ba5ee148f66f12b92b6ded5`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`dda651524daecf5d21ffffe00427f7f0960316ee86f4b3eb434f82cc94461f6c`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-runtime-attribute-valuechange-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）/属性数值与ValueChange边界_AI协作警告.md`
