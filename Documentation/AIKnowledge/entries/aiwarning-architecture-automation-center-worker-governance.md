# ESAutomationCenter 与受管 Worker 治理：保真 Knowledge

`KnowledgeId`: `es.aiwarning.arch.automation-center-worker-governance.v1`  
`Authority`: `AIWarnings` 与当前 Automation/AI Bridge 实现  
`RouteKeys`: `aiwarnings`, `architecture`, `automation`, `worker`, `task-contract`, `release-gate`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `58eae1adc97b80415c016c7a649512f68fd1fdbf08125e36a63931f91894f3de`  
`SourceSetHash`: `58eae1adc97b80415c016c7a649512f68fd1fdbf08125e36a63931f91894f3de`  
`EntryBodyHash`: `71e5f3a225713eebcc256f02d31d020e36704798ea4cbcc9dbf79d8c66b5cd78`  
`StaleWhen`: AutomationCenter、TaskContract、PathPolicy、ProcessRunner、AI Bridge 或 ReleaseGate 实现变化。

## 迁移范围

Warning 保留 AutomationCenter 权威、Worker 权限、RunRecord/ReleaseGate、AI Bridge、进程生命周期和未验证边界；本条目承载完整入口、固定控制动作、当前状态和原文快照。

## 架构与 Worker 合同

权威链为 `ESAutomationCenter → TaskRegistry → TaskContract → PathPolicy → AutomationFacade/AI Bridge → ProcessRunner → RunRecord/ReportCenter → ReleaseGate → Worker`。Python/PowerShell 只能实现已注册 Worker，不能自定义任务权限。任务必须注册、版本锁定并有入口指纹；默认只读，删除/上传/发布/覆盖/资产写入单独声明能力并受授权。Worker 不写 `Assets/`，凭据不进命令行、输入、日志或报告。结构化输入输出、DryRun、超时、取消、退出码、报告、重试和 RunRecord 是必需合同；报告临时目录成功后原子提升，失败不留半发布目录。

RunRecord 绑定 actor、RunId、TaskId/版本、Git commit、Unity/Worker 版本、输入 ManifestHash、输出 Hash 与错误。分阶段 Worker 写检查点后退出，C# 同时验证 RunId/Generation/StepId/SchemaHash 才能继续。外部进程异步有界排空 stdout/stderr，受协议超时，优先 Job Object，回退 taskkill 必须确认进程树终止；登记全局生命周期表，ReloadDomain 统一终止，同 RunId 并发拒绝。

## AI Bridge 与固定 Editor 控制

AI 只能经 Facade 调用显式 `allowAiInvoke` Task；Bridge 默认关闭，固定 JSON 信封与本机收件箱，不开放网络端口，PlayMode 仅受信主线程临时恢复监听，Task 仍需 `allowInPlayMode`。固定控制动作仅 `getUnityCompilationState`、`setUnityAutoCompilation`、`triggerUnityCompilation`、`modifyActiveScene`。后者必须 dryRun 生成计划、人工一次性批准、同 actor/新 RequestId/相同计划执行，重新解析 Active Scene、层级路径、GlobalObjectId、Tag/Layer 和白名单操作，并使用 Undo/Dirty；审批和静态编译不替代 Unity 实机证据。

当前源码已建立 C# 管理骨架、`es.scene.scan@1`、AI Bridge、`es.agent.generate@1`/`es.agent.use@1` 和 AISkill 执行协调器的部分合同；普通 Graph Dispatch 未必稳定 InvocationId。PowerShell Worker、发布物审计、上传/清理/发布仍未实现。未取得 Unity Test Runner、真实受管会话、故障注入或 ReloadDomain 恢复证据前，不得宣称端到端幂等、运行级恢复或发布通过。

## 原文快照

迁移前原始文件为 70 行、8113 UTF-8 字节，原始 SHA-256 为 `a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`。本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`6f7998bac62c988384030ea434dc1166d0b5fa11c05f880baf6705321ea27485`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`31267eaef153bbe778f11b9f521738b45cb31a8984c66830d262801680a0af65`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-architecture-automation-center-worker-governance.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md`
