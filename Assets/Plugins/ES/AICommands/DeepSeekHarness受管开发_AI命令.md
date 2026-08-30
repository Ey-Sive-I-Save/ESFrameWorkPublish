# DeepSeek Harness 受管开发 AI 命令

命令 ID：`deepseek.harness.execute`
命令类型：安全执行：DeepSeek Harness 候选分析与实现
默认改文件：否；外部受管运行仅允许由 ES 产生 `ES/Automation/Runs/DeepSeekHarness/<runId>/` 与 `ES/Automation/Temp/DeepSeekHarness/`，不直接改源码或 `Assets/`
风险等级：L3。
ProviderDeclaration：`es-deepseek`

## 定位与权威

DSH 是 ESFramework/ESAI 的高权威开发贡献层，负责模型调用、Agent Loop、候选分析和实现建议；ES 仍拥有用户授权、AIBrain 计划、允许目录、凭据边界、RunRecord、Evidence、恢复和最终 `CompletionDecision`。DSH 输出永远不是 ES `Accepted`。

## 输入与输出 schema

受管输入必须通过 `AIBrain planTask -> runTask -> ESAutomationFacade`，并绑定：

```text
taskId: es.deepseek.harness
taskVersion: 1
providerDeclaration: es-deepseek
operation: dry-run | check-local | headless-prompt
prompt: headless-prompt 时 1–12000 个字符
requireProvider: 布尔值；真实调用必须为 true
invocationId / idempotencyKey: 稳定、唯一、可回放
```

调用方不得提交 `nodePath`、`dshExecutable`、解释器、脚本、任意命令行或项目外路径。输出必须是 ES `ResultEnvelope` 与绑定 RunRecord，包含状态、RunId、输入/输出 Hash、Worker 身份、错误和未证实项；不得包含 API Key。

## 必须先读

- `AGENTS.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`
- `Documentation/AIKnowledge/entries/deepseek-harness-integration.md`
- `ES/Automation/Contracts/es-deepseek-integration-declaration-v1.json`
- `ES/Automation/Contracts/es-deepseek-harness-v1.schema.json`
- `ES/Automation/Workers/Node/DeepSeekHarness/worker-manifest.json`
- `Assets/Plugins/ES/Editor/ESAutomation/ESDeepSeekHarnessAutomation.cs`

## 执行边界

- 只调用已注册的 `es.deepseek.harness@1` 和固定的 ES 受信 Adapter；不接受外部 executable 或任意参数。
- Worker 只从项目内锁定入口启动 DSH，使用受管 `headless` Profile、固定 `DSH_HOME` 和固定 workspace。
- ES ProcessRunner 负责启动、stdout/stderr 限额、超时、取消、进程树终止和生命周期恢复。
- `dry-run` 不启动进程、不访问网络、不写正式资产；`check-local` 只检查本地 Node/DSH/Profile/锁文件及凭据存在性；`headless-prompt` 才允许 Provider 调用。
- 允许写入仅为当前 RunId 的 RunRecord、结果和项目内 Temp；禁止 `Assets/`、`.agents/`、Git、发布、任意外部路径和未声明插件。
- DSH 可返回候选结果，但不得扩大路径、写入凭据、宣称 ES 完成或绕过 AIWarnings/TaskContract。

## dry-run

建议先运行 `dry-run`，验证输入身份、Provider 声明、TaskContract、Worker/Schema Hash 和写入边界；默认预设的 `check-local` 用于检查本机 DSH 链路。dry-run 的成功只证明受管输入可接受，不证明 DSH、Provider、Unity 或最终交付成功。

## 确认

选择 `headless-prompt` 前，必须有当前用户明确目标、AIBrain `planHash`、当前命令正文 Hash、TaskContract、唯一 `invocationId/idempotencyKey` 和 `requireProvider=true`。这些是受管通道绑定，不构成对用户授权的替代或扩权。

## 取消

通过 `cancelRun` 在 Worker 活动期间请求取消。ES 终止受管 Worker 进程树并等待确认；取消未确认时返回失败/恢复所需状态，不伪造已取消。

## 恢复/回滚

- 相同 RunId 或幂等键只读取已有 RunRecord，不重复启动。
- 命令、Worker、Schema 或输入 Hash 漂移时返回 `StalePlan`/拒绝，重新规划，不猜测恢复。
- 超时、Unity ReloadDomain、编辑器退出或输出校验失败时保守标记 `Failed`/`RecoveryRequired`，不自动重试或猜测远端状态。
- DSH 只产生候选结果；ES 可拒绝、阻塞或要求人工修复，不能回滚用户未授权的正式资产。

## 验证命令

```powershell
& .\ES\Automation\Workers\Node\DeepSeekHarness\Test-ESDeepSeekHarnessContract.ps1 -ProjectRoot (Get-Location).Path
& .\ES\Automation\Workers\Node\DeepSeekHarness\Test-ESDeepSeekHarness.ps1 -ProjectRoot (Get-Location).Path -RequireProvider
& .\.agents\skills\es-aicommand-contract-authoring\scripts\Test-ESAICommandContract.ps1 -ProjectRoot (Get-Location).Path -CommandPath 'Assets/Plugins/ES/AICommands/DeepSeekHarness受管开发_AI命令.md'
```

这些命令只产生静态/本地链路证据；Unity 编译、ReloadDomain、Provider 网络调用和真实任务完成必须单独验证。

## evidenceRef

每次受管执行绑定 `commandBodyHash`、`planHash`、`taskContract`、`RunId`、`invocationId/idempotencyKey`、Worker/Schema/输入/输出 Hash 和 `ES/Automation/Runs/DeepSeekHarness/<runId>/` 下的 RunRecord/ResultEnvelope。凭据只从受管进程环境读取，绝不进入请求、日志、Knowledge 或交付文本。

## 完整流程

```text
用户目标
  -> AIBrain 路由/planTask
  -> 唯一 AICommand deepseek.harness.execute
  -> runTask + PlanHash + TaskContract
  -> ES 受信 Adapter/ProcessRunner
  -> DSH headless Agent Loop / Provider
  -> ES 收集 RunRecord、Evidence、错误和 Hash
  -> ES 决定 Completed、Blocked、Failed 或要求恢复
```

## 交付格式

返回命令 ID、ProviderDeclaration、PlanHash、TaskContract、RunId、operation、运行状态、证据路径/Hash、取消/恢复状态，以及明确的 `runtime-not-run` 和其他未证实项。不得把 DSH 文本直接写成 ES `Accepted`、Unity 通过或发布通过。
