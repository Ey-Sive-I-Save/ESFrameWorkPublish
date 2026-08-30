# ES Automation AI Bridge

这是本机受信 AI 到 Unity Editor 的受管请求入口。它只分发 C# 已注册的 Task/Content Endpoint 或固定 Editor 控制动作；不会执行 AI 提供的 Python、命令行、解释器或任意路径。

## 启用

在 Unity 中打开 `【ES】/自动化与开发/自动化中心/打开自动化中心`，勾选“授权本机 AI 请求收件箱”。默认关闭。它不是网络服务；只监视当前项目内的收件箱。

```text
ES/Automation/AI/
├─ Inbox/       # AI 原子提交 <RequestId>.request.json
├─ Processing/  # Unity 正在处理的请求
├─ Archive/     # 已处理请求，保留审计证据
└─ Responses/   # Unity 写出的 <RequestId>.response.json
```

AI 必须先写同目录 `.tmp` 文件，再原子重命名为 `<32位GUID>.request.json`。不要直接写最终文件，也不要重用 RequestId。单个请求上限 128 KiB，且必须是严格 UTF-8；稳定但空、过大或编码非法的请求会被拒绝并归档，不会无限留在 Inbox 重试。

## PlayMode 行为

进入或退出 PlayMode 时，Bridge 自动停止 Inbox 文件监听；这是临时暂停，不会撤销已经授予本机 Editor 的授权，回到 EditMode 会自动恢复。此时写入 Inbox 的请求不会在当前 PlayMode 执行，会留到返回 EditMode 后重新扫描；AI 应等待 EditMode 或经已受信的 UnityMCP 控制通道请求本次 PlayMode 临时恢复监听。它不会擅自杀掉进入 Play 前已启动的只读 Worker；各 Task 必须自行实现取消/暂停语义。

受信 UnityMCP 必须在 Unity 主线程调用：

```csharp
ESAutomationAiBridge.TrySetTrustedPlayModeListening(true, out string reason);
```

如果 UnityMCP 只支持 `ExecuteMenuItem`，可执行 `【ES】/自动化与开发/AI 控制/PlayMode 临时恢复收件箱监听`；暂停菜单位于同一路径。这个 API 和菜单都不能开启首次用户授权、不能由 Inbox 请求调用，并会在退出 PlayMode 后自动失效。即使监听恢复，只有注册描述中明确 `allowInPlayMode = true` 的任务可启动；当前场景扫描不允许在 PlayMode 执行。

## 受 AIBrain 门禁的任务调用

`runTask` 不接受旧的“只给 taskId/taskVersion”直达格式。先用新的 `requestId` 调用 `planTask`，保存返回的 `planHash` 和 `invocationId`；执行时使用另一个新的 `requestId`，但携带相同的 `invocationId` 与 `approvedPlanHash=planHash`。只有受信进程内宿主绑定了当前用户指令的 L1 本地计划，授权才可在 15 分钟内有限复用（默认最多 20 次）；外部 Bridge JSON 不能自报 `userDirected`。L1/L2 `candidate-only` 计划默认最多 5 次，L3 或其他计划默认单次；每次可复用调用仍须使用新的非空 `idempotencyKey`。

```json
{
  "protocolVersion": 1,
  "requestId": "0123456789abcdef0123456789abcdef",
  "actorId": "codex.local",
  "action": "runTask",
  "payload": {
    "objective": "读取当前项目的场景扫描结果",
    "routeKeys": ["editor", "scene-validation"],
    "commandId": "scene.scan.review",
    "skillNames": ["es-editor-tooling"],
    "taskId": "es.scene.scan",
    "taskVersion": 1,
    "preset": "default",
    "input": {}
  }
}
```

`planTask` 返回的 `data.brainPlan` 中包含 `planHash`、`invocationId` 和 `planId`。随后执行：

```json
{
  "protocolVersion": 1,
  "requestId": "fedcba9876543210fedcba9876543210",
  "actorId": "codex.local",
  "action": "runTask",
  "payload": {
    "objective": "读取当前项目的场景扫描结果",
    "routeKeys": ["editor", "scene-validation"],
    "commandId": "scene.scan.review",
    "skillNames": ["es-editor-tooling"],
    "taskId": "es.scene.scan",
    "taskVersion": 1,
    "preset": "default",
    "input": {},
    "invocationId": "0123456789abcdef0123456789abcdef",
    "approvedPlanHash": "<planTask 返回的 planHash>",
    "idempotencyKey": "scene-scan-20260823-001"
  }
}
```

`commandId`、`skillNames` 和 `taskId@taskVersion` 必须分别命中当前 AICommand 目录、项目 `.agents/skills` 正式目录和 ESAutomationFacade 注册表；示例中的身份只用于说明字段合同，不能当作项目中已注册的能力。若任务尚未有匹配 AICommand、正式 Skill 或 Graph Workflow，AIBrain 必须返回阻断，调用者不得改用无关合同绕过。

Feishu 第一阶段只读任务的结构相同，例如 `taskId: "es.feishu.read"`、`taskVersion: 1`，并将 `input` 限制在已注册的 `operation`（`auth-status`、`knowledge-search`、`document-pull`）及其 Schema 内。它仍必须先经过 AIBrain、ESAutomationFacade 和 Feishu TaskContract；不得把 Node Worker 路径或凭据放入请求。

## DeepSeek Harness 受管开发入口

DeepSeek Harness 通过 `deepseek.harness.execute` AICommand 和 `es.deepseek.harness@1` 接入 ES Automation。前置条件是本机已有 Node.js 22+ 与 npm；项目不会静默安装全局 Node、修改 PATH 或使用用户默认 `~/.dsh`。GitHub 拉取后，在项目根执行一次：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ES\Automation\Workers\Node\DeepSeekHarness\Install-ESDeepSeekHarness.ps1
```

随后运行 `Test-ESDeepSeekHarness.ps1 -RequireProvider` 或打开 `【ES】/自动化与开发/自动化中心/打开自动化中心` 查看 DSH 图标。通过 AIBrain 执行时先选择 `deepseek.harness.execute`，再提交 `es.deepseek.harness@1`；显示 `NotConnected` 时只说明需要按 `reasonCode` 接入，不会把 DSH 源码存在或旧回执当作成功。

DSH 是高权威开发贡献层，不是 ES 最终裁决层。它可提供分析、实现候选和 Agent Loop；ES 仍控制任务授权、允许目录、凭据边界、RunRecord、Evidence、恢复以及 `CompletionDecision`。DSH 任务的 `dry-run` 不启动进程；`check-local` 不调用 Provider API；只有本地状态为 `Connected` 后才允许受管 `headless-prompt`。

与 DSH 强绑定的 Skill、AIKnowledge 或合同必须声明 `es-deepseek`，见 `ES/Automation/Contracts/es-deepseek-integration-declaration-v1.json` 和 `Documentation/AIKnowledge/entries/deepseek-harness-integration.md`。

`dryRun` 是可选布尔字段，默认 `false`。对于声明支持 DryRun 的任务（例如 `es.feishu.read`），它会沿 AI Bridge 传递到 TaskContract；不支持 DryRun 的任务会由 Facade 拒绝。

默认预设会直接启动：不包含未激活对象、摘要模式、组件 Top 10；不会弹人类对话框。响应中的 `runId` 可继续用于：

```json
{
  "protocolVersion": 1,
  "requestId": "fedcba9876543210fedcba9876543210",
  "actorId": "codex.local",
  "action": "getRun",
  "payload": { "runId": "<上一步返回的RunId>" }
}
```

若任务返回 `Blocked` 且含 `expectedGeneration` 与 `inputSchemaHash`，AI 只能按该任务已注册 Schema 使用 `submitInput`。不能猜测字段、复用旧 Generation 或改写 SchemaHash。

## Python 请求高级输入：`interactive` 预设

场景扫描还提供 `interactive` 预设。它不会让 AI 传入任意 Python 参数，而是让 Python 完成阶段 0 后，以已注册的 `StepId + SchemaHash` 请求 ES 高级对话框。人类可在 `ESAdvancedDialog` 填写；AI 也可轮询同一 `runId`，读取 `getRun.data.inputRequest` 的类型化字段描述后提交相同输入。

```json
{
  "protocolVersion": 1,
  "requestId": "00112233445566778899aabbccddeeff",
  "actorId": "codex.local",
  "action": "runTask",
  "payload": {
    "objective": "以交互预设读取场景扫描结果",
    "routeKeys": ["editor", "scene-validation"],
    "commandId": "scene.scan.review",
    "skillNames": ["es-editor-tooling"],
    "taskId": "es.scene.scan",
    "taskVersion": 1,
    "preset": "interactive",
    "input": {}
  }
}
```

到达检查点后，`getRun` 将返回：

```text
status = Blocked
data.inputRequest = {
  requestGeneration, stepId, schemaHash,
  schema: { title, fields: [{ fieldId, valueType, defaultValue, choices/range }] }
}
```

AI 只能逐字段按该 Schema 提交，随后由同一 `RunId` 继续 Python 阶段 1：

```json
{
  "protocolVersion": 1,
  "requestId": "11223344556677889900aabbccddeeff",
  "actorId": "codex.local",
  "action": "submitInput",
  "payload": {
    "runId": "<interactive 返回的 RunId>",
    "requestGeneration": 0,
    "stepId": "scene-scan.report-options",
    "schemaHash": "4bbaa61e9bf8a2e2664d3b9cf98944711aa26d5c714e911044298193f08a14cb",
    "accepted": true,
    "values": {
      "includeInactive": false,
      "detailMode": "summary",
      "topComponentCount": 10
    }
  }
}
```

同一 RunId 的人类对话框与 AI 提交会竞争同一代次；最先成功提交者继续 Worker，迟到提交稳定返回 `Blocked` 或 `Rejected`，不会重复生成报告。

## 内容补充入口

`submitContentProposal` 是领域扩展入口，不是通用“写 Assets”接口。AI 可提交的 `contentType@version` 必须由所属领域在 C# 中注册，并声明 SchemaHash；未注册类型一律 `Rejected`。

当前没有已注册的资产内容提案 Endpoint，因此 AI 不能通过此 Bridge 直接创建 Story、Prefab、配置或其他 Unity 资产。领域实现必须先完成：规范校验、幂等、目标路径策略、Undo/事务、审计回执和 PlayMode/构建验收。

## Unity Editor 受管控制

Bridge 提供四个固定的 Editor 主线程动作。它们不是 Worker、任意命令执行或任意场景/资产写入入口：

- `getUnityCompilationState`：读取自动编译、脚本编译、Editor 更新和 PlayMode 状态。
- `setUnityAutoCompilation`：用 `{ "enabled": true|false }` 设置本次 Editor 会话的自动刷新/程序集 Reload 锁。若 AI 曾关闭自动编译，用户关闭 Bridge 时只恢复该 AI 持有的抑制；人工菜单设置不会被误恢复。
- `triggerUnityCompilation`：用 `{ "forceRefresh": true|false }` 请求 AssetDatabase 刷新和脚本编译；自动编译关闭时明确返回阻断。
- `modifyActiveScene`：只允许当前已加载、已保存于 `Assets/` 的 Active Scene，白名单操作为 `setActive`、`setName`、`setTag`、`setLayer`。

Inbox 请求和受信宿主在 Unity 主线程调用的 `ExecuteJson(...)` 都必须先通过“授权本机 AI 请求收件箱”门禁。`ExecuteJson(...)` 不会代为切线程：后台线程调用会返回 `Rejected`，直接请求同样受 128 KiB 上限约束。固定控制动作还会以 `requestId` 在以下位置预写不可覆盖审计；同一 `requestId` 再次提交会被拒绝：

```text
ES/Automation/Runs/ControlActions/<requestId>.json
```

审计记录包含 actor、固定能力、输入 SHA-256、批准 ID/计划指纹、状态、时间、结果 SHA-256 和错误。审计无法完成时，Bridge 不会把可能已经发生的控制动作报告为成功。

### 场景计划的人工批准

`modifyActiveScene` 必须分两次请求，不能直接使用 `dryRun=false` 写场景：

1. AI 用 `dryRun=true` 提交精确 `scenePath`、`operations` 与 `save`。Bridge 解析无歧义层级路径、项目 Tag、已定义 Layer 和目标 `GlobalObjectId`，只返回计划、`approvalId`、指纹和 5 分钟到期时间，不修改场景。
2. 人工在 `【ES】/自动化与开发/自动化中心/打开自动化中心` 查看 actor、场景、每个目标和值后点击“批准一次”或拒绝。批准本身不修改场景。
3. 同一 actor 用**新的** `requestId`、完全相同的计划和返回的 `approvalId` 提交 `dryRun=false`。Bridge 再次解析目标，核对计划指纹和 Active Scene，并一次性消费批准后才在同一 Undo Group 内应用、标记 Dirty；仅 `save=true` 时保存当前场景。

以下任一事件都会使未消费批准失效：批准到期、人工撤销、Bridge 关闭、PlayMode 切换、Domain Reload 或已执行一次。Bridge 同时最多保留 32 条待批准计划，每个计划最多 64 个白名单操作；超过上限必须拆分为新的 dry-run 和批准。AI 不能复用批准 ID，也不能借目标路径重名、`..`、路径分隔符、临时场景、包内场景、未定义 Tag 或未定义 Layer 扩大写入范围。

dry-run 示例：

```json
{
  "protocolVersion": 1,
  "requestId": "0123456789abcdef0123456789abcdef",
  "actorId": "codex.local",
  "action": "modifyActiveScene",
  "payload": {
    "scenePath": "Assets/Scenes/Main.unity",
    "operations": [{
      "operation": "setActive",
      "targetPath": "Main/Gameplay",
      "value": true
    }],
    "save": true,
    "dryRun": true
  }
}
```

批准后，将同一 `scenePath`、`operations` 与 `save` 保持不变，改为 `"dryRun": false` 并增加 `"approvalId": "<dry-run 返回值>"`。`save=true` 不等于允许任意资产写入，只保存当前 Active Scene。

当前文档描述的是源码合同。Unity 实际导入、编译、ReloadDomain、审批交互、Undo/保存和场景运行结果仍需要单独取得 Unity 验收证据。
