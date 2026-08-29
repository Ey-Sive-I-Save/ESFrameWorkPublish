# AITalk 全自动 Codex 多启动

命令 ID：`codex.multilaunch.execute`

这是 AITalk 调用 ES Codex 多启动的唯一受管入口。AITalk 必须先调用 AIBrain `planTask`，再将返回的 `approvedPlanHash`、本次用户授权和唯一 `idempotencyKey` 传入 `runTask`；禁止直接运行 PowerShell、伪造 PlanHash 或把 Skill 名称当作权限。

命令类型：安全执行：受管 Codex 多启动编排。
默认改文件：否；仅允许受管 Worker 生成本次运行的 envelope、私有快照与 receipt，不直接修改项目源文件。
风险等级：L3。

## 取消（cancellation）

分波次启动提交前可取消；已启动波次只能通过受支持停止入口终止并等待终态。取消、超时或部分启动返回 `Cancelled` 或 `RecoveryRequired`，不得报告整批成功。

## 验证（validation）

执行前验证 approvedPlanHash、用户授权、TaskContract、envelope schema、目标职责与唯一 idempotencyKey；执行后核对每个波次的 process/window、ContextAccepted、RunId 和 receipt。

## evidenceRef

回执必须包含 commandId、commandBodyHash、planHash、InvocationId、每波次 envelope/私有快照 SHA-256、RunId、ContextAccepted 状态、失败码和未验证项；启动或进程存在不等于 Runtime 验收。

## 必须先读

- `.agents/skills/es-codex-session-bootstrap/SKILL.md` 及其会话启动合同。
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、当前状态和规则索引。
- `ES/Automation/Contracts/es-codex-multilaunch-request-v1.schema.json`。
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json`（机器发现权威）。

## 能力

- 根据 `launches[]` 计划分波次启动多个 Codex 职责窗口。
- 每项独立处理 `New`、`Handoff`、`Reissue`。
- 已存在活跃 Session 返回 `AlreadyRunning`。
- 旧 envelope 首次验收非零或 HEAD 漂移返回 `NeedsReissue`。
- 每项回收 `promptObserved`、`contextAccepted`、Session/Record 身份和终端映射。
- 只在全部必要 acceptance 证据满足时向 AITalk 返回可聚合结果；部分失败不得压平成成功。

## 输入

输入必须符合 `ES/Automation/Contracts/es-codex-multilaunch-request-v1.schema.json`，不得包含任意命令、解释器、外部 URL、sourceAbsolutePath 或未声明的路径。

## 自动化边界

“全自动”表示：用户一次授权后，AITalk 自动完成计划、分波次启动、等待验收、幂等检查、可安全重试分支和结果聚合；不表示绕过用户授权、AIBrain PlanHash、TaskContract、ExternalWrite 能力交集或 acceptance 门禁。

## 失败与恢复

`NeedsInputs`、`NeedsReissue`、`AlreadyRunning`、`PreflightFailed`、`Failed` 必须逐项保留。不得把失败项静默切换为 New，不得自动关闭源窗口，不得声称业务任务完成。恢复只能使用同一 `batchId`、新的唯一 `idempotencyKey`，并重新规划受漂移影响的项。

## 输出

Worker 输出 `multilaunch-result.json` 和 `run-record.json`。`runtimeStatus=runtime-executed` 只证明 Worker 执行过；业务完成仍需逐项 acceptance 和上层 `completionDecision`。

## 交付格式

返回批次指纹、每项 `taskKey`/`responsibilityKey`、`status`、`launchToken`/envelope、`contextAccepted` 与 receipt 路径；逐项保留 `AlreadyRunning`、`NeedsReissue`、`PreflightFailed` 或 `Failed`，不得将部分成功汇总为全部成功，也不得宣称 Unity/Runtime 已验收。
## 恢复（recovery）

失败或中断仅允许同一 batch 使用新的 idempotencyKey 重试；无法确认波次状态时返回 `NeedsReissue`，不得猜测重启或重复发送。
