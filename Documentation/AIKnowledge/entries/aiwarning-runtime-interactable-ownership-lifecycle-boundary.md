# ES Interactable 占用与生命周期边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.runtime.interactable-ownership-lifecycle-boundary.v1`  
`Authority`: `AIWarnings` 与当前交互源码主链  
`RouteKeys`: `aiwarnings`, `runtime`, `interaction`, `interactable`, `ownership`, `lifecycle`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `88dff8031363934ace57566d5a219a0a078c7c69788857b986003cfd0d78ba89`  
`SourceSetHash`: `88dff8031363934ace57566d5a219a0a078c7c69788857b986003cfd0d78ba89`  
`EntryBodyHash`: `2151f557baa78cd94de9edf7bb21e3490802c95be100ebe76332479a3f53a5fe`  
`StaleWhen`: 交互占用、结束原因、State/IK/SupportFlag 清理或 Tag Zone 实现变化。

## 迁移范围

Warning 保留交互意图边界、单 Owner 约束、结束收口、异常与证据限制；本条目保存完整主链、规则、Tag Zone 区分、性能口径、验收矩阵和原文快照。Knowledge 不替代源码或运行时证据。

## 交互主链与占用

主链为：Input Interact 意图 → `EntityBasicInteractionModule` 候选探测与 Check → `ESInteractable.TryAcquireInteraction(entity)` → State/MatchTarget/IK/SupportFlag → Started/Update → `EndInteraction(reason)` → Ended → 停止 IK/MatchTarget、退出 State、恢复 SupportFlag → `ReleaseInteraction`。距离、朝向、Tag、Permit、状态和占用判定不能塞回 Input Module。

`ESInteractable` 仅保存一个 `_interactionOwner`；同 Entity 可重入，其他 Entity 返回 `Occupied`，只有当前 Owner 可释放。成功取得占用后才建立交互资源，Begin 失败必须释放。当前语义是 first-acquire-wins，不支持排队、优先级抢占、超时、网络并发或 generation-safe lease；多人需求必须新建请求仲裁与 lease/generation 合同，不能临时扩张 bool/list。

## 结束、回调与 Tag Zone

结束原因至少区分 `Completed`、`UserCancelled`、`MovementCancelled`、`Timeout`、`TargetLost`、`StateExited`、`ModuleDisabled`、`BeginRejected`。目标/模块禁用、销毁、池化、意外 State 退出和探测丢失都走同一收口，不能只改 UI 或清 Owner。业务回调 `OnInteractStarted/Update/Completed/Ended` 不拥有最终清理权；当前没有框架级 try/finally，回调异常可能中断后续清理，因此不得宣称异常安全，回调也不能改写其他 Entity Owner 或恢复已结束资源。

`ESTagApplyZone` 使用 `Dictionary<Entity, Occupant>`、每 Entity 的 `ESTagLeaseSet` 和 Collider 计数管理“进入区域即写 Tag”，Disable 时清理 lease；它不代表交互进行中，不参与 `_interactionOwner`，也不能实现排队或独占。

## 性能与验收

候选/占用字典和 Tag Zone 可复用容器，但首次扩容、`GetComponentInParent` 缓存未命中、State 注入、日志和异常不属于 0 GC 稳态。PlayMode 必须覆盖双 Entity 竞争、Begin/Update/End 抛异常、目标 Disable、State 建立失败、TargetLost、模块池化/销毁释放和同 Entity 多 Collider 进出 Tag Zone。在证据补齐前只能说主链已接入，异常清理与多人竞争未验收。

## 原文快照

迁移前原始文件为 62 行、3667 UTF-8 字节，原始 SHA-256 为 `4a08dcbd6972f2aa451259b92723aaebd1bf461580eec5d92f30025630be46d9`。本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/交互运行时_Interactable占用生命周期与结束原因_AI协作警告.md` (`3ef8e2244e8dc1301d31c6ee19e246bb272474304e76c556e0308f62c930ace2`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`22d31a5289d99169d275f25f840d71f1efea38bd538a34f6847ab3bac8d19fd1`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-runtime-interactable-ownership-lifecycle-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/输入与交互（InputInteraction）/交互运行时_Interactable占用生命周期与结束原因_AI协作警告.md`
