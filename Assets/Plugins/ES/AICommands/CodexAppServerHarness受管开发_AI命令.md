# Codex App Server Harness 受管开发

命令 ID：`codex.appserver.execute`

命令类型：安全执行：Codex App Server 会话、回合与候选输出。
默认改文件：否；只允许由 ES 生成当前 RunId 的 RunRecord、结构化事件回执和临时结果，不直接修改 `Assets/`、`.agents/`、Git 或发布产物。
风险等级：L3。
ProviderDeclaration：`es-codex`

## 定位与权威

Codex App Server 是外部执行/推理贡献层，负责线程、回合、流式事件和候选回答；ESFramework/ESAI 仍拥有用户授权、AIBrain PlanHash、AICommand、TaskContract、允许目录、凭据边界、RunRecord、证据、取消/恢复和最终 `CompletionDecision`。Codex 的 `Passed`、线程存在、回合完成或文本回答都不是 ES 业务完成，也不是正式资产接受。

## 输入与输出 schema

受管输入必须通过 `AIBrain planTask -> runTask -> ESAutomationFacade`，并只允许：

```text
operation: dry-run | check-local | start-thread | turn
prompt: 0–12000 字符；start-thread/turn 必须非空
threadId: turn 必填，必须是本次 ES 记录的精确身份
model: 可选的稳定模型标识（仅字符白名单）
```

调用方不得提交 `executable`、`cwd`、`sandbox`、`approvalPolicy`、权限、输出路径、脚本、URL 或任意命令行。Worker 会补齐固定的 `es-codex`、任务、RunId 和只读沙箱字段，并以 `es-codex-app-server-v1.schema.json` 再校验。

输出为 `codex-app-server-result.json` 与绑定的 ES RunRecord，包含 thread/session/turn 身份、输入/输出 Hash、事件上限、权限请求计数、错误和未证实项；不得包含 API Key。`runtime-executed` 只证明受管 Worker 运行过，不代表 Unity、业务逻辑或发布验收。

## 必须先读

- `AGENTS.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`
- `.agents/skills/es-codex-session-bootstrap/SKILL.md` 及其 App Server 合同
- `ES/Automation/Contracts/es-codex-app-server-integration-declaration-v1.json`
- `ES/Automation/Contracts/es-codex-app-server-v1.schema.json`
- `Assets/Plugins/ES/Editor/ESAutomation/ESCodexAppServerAutomation.cs`

## 执行边界

- 只调用已注册的 `es.codex.app-server@1` 和固定 PowerShell Worker；外部调用方不能指定解释器或 Codex 可执行入口。
- Worker 只从固定 `codex.cmd app-server --stdio` 启动 App Server；`ES_CODEX_CLI_PATH` 仅作为本机显式绝对 `codex.cmd` 覆盖，不进入请求或项目文件。
- App Server 连接先执行 `initialize`/`initialized`，再执行 `thread/start` 或精确 `thread/resume`，最后执行 `turn/start`；不会按标题、最近会话或 TUI 窗口猜测身份。
- `thread/start`/`turn/start` 使用固定 `approvalPolicy=never` 与只读、项目根受限沙箱；外部服务不能取得 ES Assets 写权限。
- 任何命令、文件变更、权限、用户输入或 MCP elicitation 请求都 fail-closed，终止本次 Worker 并记录 `Blocked`/错误；不自动批准、不自动扩大路径。
- ES ProcessRunner 统一负责启动、stdout/stderr 限额、超时、取消、进程树终止和 Editor 生命周期恢复。

## dry-run

`dry-run` 不启动 Codex、不调用 Provider、不访问网络、不产生业务修改，只验证输入字段、TaskContract、Worker/Schema Hash 和写入边界。

## 确认

选择 `start-thread` 或 `turn` 前，必须有当前用户目标、AIBrain `planHash`、当前命令正文 Hash、TaskContract、唯一 `invocationId/idempotencyKey` 和明确授权。它们是受管通道绑定，不替代用户授权；ES 的业务完成判定仍独立存在。

## 取消

通过 `cancelRun` 请求终止固定 Worker 进程树；取消调用本身只表示终止请求已发出，必须再次读取 RunRecord 观察终态。无法确认终止时返回失败/恢复所需状态，不伪造 `Cancelled`；App Server 的 `turn/interrupt` 不跨越一次性 Worker 连接自动猜测。

## 恢复/回滚

- 同一 RunId 或幂等键只读取既有 RunRecord，不重复启动。
- `turn` 恢复只接受 ES 已记录的精确 `threadId`；thread/session/turn 身份不匹配、命令/Worker/Schema/输入 Hash 漂移时返回 `StalePlan`/拒绝并重新规划。
- Editor 重启、域重载、超时、结果缺失或权限请求均保守标记 `Failed`/`Blocked`；不自动重试、不猜测远端状态。
- 本 Worker 不写正式资产，因此没有未授权资产回滚动作；候选内容必须回到 ES 领域提案/人工审查链。

## 验证命令

```powershell
& .\ES\Automation\Workers\PowerShell\Test-ESCodexAppServerContract.ps1 -ProjectRoot (Get-Location).Path -Json
& .agents/skills/es-aicommand-contract-authoring/scripts/Test-ESAICommandContract.ps1 -ProjectRoot (Get-Location).Path -CommandPath 'Assets/Plugins/ES/AICommands/CodexAppServerHarness受管开发_AI命令.md'
& .agents/skills/es-codex-session-bootstrap/scripts/Test-ESCodexAppServerCapability.ps1
```

以上分别只验证 AICommand 合同与一次有界 App Server 能力探针；Unity 编译、真实 Provider 回合、业务验收、PlayMode、Player、网络和发布必须单独验证。未配置 Codex 或未明确要求真实运行时，本命令保持 `runtime-not-run`。

## evidenceRef

每次受管执行绑定 `commandBodyHash`、`planHash`、`taskContract`、RunId、`invocationId/idempotencyKey`、Worker/Schema/输入/输出 Hash 和 `ES/Automation/Runs/CodexAppServer/<runId>/` 下的 RunRecord/Result。凭据只从受管宿主环境读取，绝不进入请求、日志、Knowledge 或交付文本。

## 完整流程

```text
用户目标
  -> AIBrain 路由/planTask
  -> 唯一 AICommand codex.appserver.execute
  -> runTask + PlanHash + TaskContract
  -> ES 受信 Adapter/ProcessRunner
  -> Codex App Server initialize/thread/turn
  -> ES 收集事件、身份、RunRecord、Evidence 和错误
  -> ES 决定 Completed、Blocked、Failed 或要求恢复
```

## 交付格式

返回命令 ID、ProviderDeclaration、PlanHash、TaskContract、RunId、operation、thread/session/turn（如有）、运行状态、证据路径/Hash、取消/恢复状态，以及明确的 `runtime-not-run`、候选-only 和其他未证实项。不得把 Codex 文本直接写成 ES `Accepted`、Unity 通过或发布通过。
