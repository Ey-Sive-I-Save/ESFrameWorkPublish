# ESAutomationCenter 与受管 Worker 治理 AI 协作警告

Status: current
StableId: es.aiwarning.arch.automation-center-worker-governance.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, architecture, automation, worker, task-contract, release-gate
Applicability: ESAutomationCenter、TaskContract、Facade、ProcessRunner、RunRecord 与 Python/PowerShell Worker
Owner: ESFramework Automation 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-architecture-automation-center-worker-governance.md
StaleWhen: AutomationCenter、TaskContract、PathPolicy、ProcessRunner、AI Bridge 或 ReleaseGate 实现变化。

## 长期约束

- 自动化必须先进入 C# Editor 权威 `ESAutomationCenter`（TaskRegistry→TaskContract→PathPolicy→Facade/AI Bridge→ProcessRunner→RunRecord/Report→ReleaseGate），Worker 只能执行注册、版本锁定、入口指纹匹配的任务。
- 默认只读；删除、上传、发布、覆盖、资产写入分别声明能力并取得授权。Worker 不写 `Assets/`，凭据不得进入命令行/输入/日志/报告；固定 Editor 控制动作不得伪装成 Worker。
- 真实任务必须有结构化输入输出、DryRun、超时、取消、退出码、报告、重试与 RunRecord；报告成功原子提升，失败不得留下可误发布半成品。RunId/Git/版本/ManifestHash/输出Hash/错误等证据必须绑定。
- AI 仅经 `ESAutomationFacade` 调用显式 `allowAiInvoke` Task；Bridge 默认关闭、固定 JSON 信封、本机收件箱、主线程和 PlayMode 边界受控。场景修改必须 dry-run、一次性批准、同计划执行并重新解析目标。
- 外部进程需异步有界排空、超时、Job Object/进程树终止、全局生命周期注册和 ReloadDomain 清理；同 RunId 并发拒绝。不得把静态门禁升级为 Unity/端到端/发布通过。
- 当前 Graph/AISkill、场景扫描和 Bridge 仅有源码接入证据；PowerShell Worker、上传/清理/发布仍禁止提前实现。

## Knowledge 导航

完整入口、控制动作、状态、证据字段、恢复边界和原文快照见 `es.aiwarning.arch.automation-center-worker-governance.v1`。
