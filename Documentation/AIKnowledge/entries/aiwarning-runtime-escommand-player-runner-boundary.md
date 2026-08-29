# ESCommand 运行时 Player/Runner：保真 Knowledge
`KnowledgeId`: `es.aiwarning.runtime.escommand-player-runner-boundary.v1`  
`Authority`: `AIWarnings` 与当前 ESCommand 运行时实现  
`RouteKeys`: `aiwarnings`, `runtime`, `escommand`, `player`, `runner`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `75192bbf4179819d18135d552bc6ef0ba0cbebb5995ca1447bb2cebaeaa59b57`  
`SourceSetHash`: `75192bbf4179819d18135d552bc6ef0ba0cbebb5995ca1447bb2cebaeaa59b57`  
`EntryBodyHash`: `2cf50139578e0c1c9dcd40cfef5f990f9772dd76f31776f79fcefa930bb73723`  
`StaleWhen`: ESCommandPlayer/Runner、Playable 或任一 SourceRef 哈希变化。

## 迁移范围
Warning 保留唯一 Tick 驱动、Play/Cancel/Stop 边界、对称清理和已知异常缺口；本条目承载运行主链、状态机、输入/RuntimeMode 语义、生命周期和测试矩阵。Knowledge 不授予执行权限。

## 主链与职责
`ESCommandPlayer.Play(event) → ESCommandPlayerRunner.Register(player) → MODULE_ESCommandModule.Update() → ESCommandPlayerRunner.TickAll(Time.time, Time.deltaTime) → Player.Tick → Command.InvokeCommand 或 IESCommandPlayable`。Player 持有事件、索引、取消标记和当前 Playable；Runner 用 `List + Dictionary` 管理活跃表并 swap-back 移除；`ESCommandServices` 仅是输入模块与 RuntimeMode 注入点，不是通用 Service Locator；非 Playable 命令同步 Invoke，Playable 使用 `OnPlayStart → TickPlay → OnPlayCancel`。

## 帧与停止
- `Play` 重置索引、Playable、取消标记和帧号；重播不会自动取消旧 Playable，调用方必须先 `Stop`，后续修复需补偿并回归。
- `tickImmediatelyOnPlay` 首帧以 `deltaTime=0` 推进；`lastTickFrame` 禁止同帧重复 Tick。`Cancel` 只登记请求，下次 Tick 处理；`Stop` 立即取消当前 Playable、注销 Runner 并进入 Canceled。
- Playable 返回 Running 保留位置，Failed/Canceled 终止 Player，其余状态继续；启动、持续、取消、结束必须幂等或有一次性状态。

## 输入、模式与异常
虚拟输入命令只能调用 UISet/UI Pulse/UI Clear/UISetVector2/UISetAxis 等输入 API，持续值必须对称清理，Pulse 与 Held 不可混淆。RuntimeMode Push/Pop/Remove 按值操作，Remove 从栈顶向下找 mode/tag，不能表达精确申请；多来源精确回收需 Handle/Lease 所有权。当前 Invoke/OnPlayStart/TickPlay/OnPlayCancel 尚无逐命令异常隔离，异常可能中断 TickAll；不得吞异常后报告成功。

## 生命周期与验收
场景切换、模块销毁或 Subsystem 重置须清 Runner 活跃表与 Services 注入；稳态容器低分配不代表命令无分配。验收需覆盖空事件、立即推进、同帧 Tick、普通命令失败、Playable 全状态、Disable/Stop、输入清理、RuntimeMode 多来源、回调异常及重置清退；补齐 PlayMode/Unity Test Runner 前，只能声明主链存在、异常隔离与全局清理待验收。

## 原文快照
迁移前台账：59 行、4300 字节，原始 SHA-256 `05d19860d7ab966b84b98e5c065404b8a6d62f8ebf05719ac18d8be450b53d18`；本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md` (`2d81b217e424c9170625025664ee00db2716a8b2071133cdc3dc0e6f4f21f960`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`c25ff614cc33feafce797e05ca89a1e9eb4ef633b15a129fe6638705c986a42d`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-runtime-escommand-player-runner-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/ESCommand运行时_PlayerRunner执行帧与服务边界_AI协作警告.md`
