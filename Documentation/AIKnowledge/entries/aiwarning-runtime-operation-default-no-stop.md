# Operation 默认无 Stop：保真 Knowledge
`KnowledgeId`: `es.aiwarning.runtime.operation-default-no-stop.v1`  
`Authority`: `AIWarnings` 与当前 Operation/Skill 运行时实现  
`RouteKeys`: `aiwarnings`, `runtime`, `operation`, `stop`, `ownership`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `d3b7e096c420265d746e70b8f4a4207a77c0e810ebb950bf789946c43bbf20ca`  
`SourceSetHash`: `d3b7e096c420265d746e70b8f4a4207a77c0e810ebb950bf789946c43bbf20ca`  
`EntryBodyHash`: `239ac308b89865c85ec7033038f5be33d6bdd3fb7f0f69ce88426b8d441346b3`  
`StaleWhen`: Operation/Clip/Pack 所有权实现或任一 SourceRef 哈希变化。

## 迁移范围
Warning 保留默认无 Stop、复合推导、MustTriggerStop、实例状态和 Pack 所有权边界；本条目承载源码入口、完整生命周期要求、废弃类型和静态性能声明。Knowledge 不授予运行时修改权限。

## 核心语义
`ESOutputOp` 默认 `public virtual bool NeedsStop => false`。只有确实持有跨时间状态或外部资源、并由 `StopOperation(...)` 归还所有权的 Op（循环音频、持续粒子、临时控制权等）才可为 `true`。一次性伤害、事件、日志、OneShot 音频和单次数值写入不得声明 Stop。`SkillOperationClipRuntimePlayer` 构建时缓存 NeedsStop；Clip 退出仍做目标写回和池化清理，不调用无意义 Stop。

## 复合与停止边界
顺序/条件包装 Op 的 NeedsStop 必须由子 Op 推导，包装层不得无条件返回 true，也不能遗漏需清理子 Op。`MustTriggerStop` 仅表示已开始且需要 Stop 的 Op 被禁用后仍要清理，不能把一次性 Op 转成生命周期 Op，也不能代替 NeedsStop。共享 Operation 配置不得保存施法实例的 Handle/running；不得因基类有 StopOperation 就给所有 Op 增加空 Stop；不得每帧判断 NeedsStop。新增 StopOperation 必须覆盖 Enter 成功、Enter 失败补偿、正常 Exit 和强制 Skill Exit。

## Pack 与废弃边界
`OutputOperationBuffer`、Buffer Float 空壳、`IOpStoreKeyGroup`、`ESOpSupport.storeForBuffer` 已退出生产代码，仅在 `Assets/Plugins/ES/Obsolete/Operation_OldSystem` 留档，不得恢复。Operation、表达式或借用者不能回收 `ESRuntimeTargetPack`；租期由创建 Pack 的 Skill/Track/Clip/Support 持有。ReferenceSkill/ReferenceTrack/裸 Pack UserData 永远借用；Copy/New 才保存 `createdTarget + targetVersion` 并负责归还。禁止恢复 `TrackTargetPack(existingPack)` 认领入口；Support 仅回收 `RentTargetPack()` 创建的 Pack。长期持有须同时保存 Version 并走内部版本门禁，异步任务不得跨 Skill/Track/Clip 持有裸 Pack。

## 原文快照与证据
迁移前台账：48 行、3219 字节，原始 SHA-256 `77b7554f3ca549c265f0e8fdd86be2ef6315b4d53fe4d9469bd6355f144f8704`。适用入口为 `ESOutputOp.cs` 与 `SkillTrackItem_Operation.cs`；性能结论仅限静态路径，CPU/GC 仍待 Unity Profiler、Player、IL2CPP 实测。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md` (`e831fd0ac59c1840b958dd1a5345beb60f45ffa8e2f83adc2391f64c8a49882f`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`5dd908b6cd37786c530ba09575f5f6a8efa4dee6651af519c8484d9dbeb57b12`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-runtime-operation-default-no-stop.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md`
