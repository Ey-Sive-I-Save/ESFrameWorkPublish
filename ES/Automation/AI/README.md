# ES Automation AI Bridge

这是本机受信 AI 到 Unity Editor 的受管请求入口。它只分发 C# 已注册的 Task/Content Endpoint；不会执行 AI 提供的 Python、命令行、解释器或任意路径。

## 启用

在 Unity 中打开 `【ES】/自动化/中心`，勾选“授权本机 AI 请求收件箱”。默认关闭。它不是网络服务；只监视当前项目内的收件箱。

```text
ES/Automation/AI/
├─ Inbox/       # AI 原子提交 <RequestId>.request.json
├─ Processing/  # Unity 正在处理的请求
├─ Archive/     # 已处理请求，保留审计证据
└─ Responses/   # Unity 写出的 <RequestId>.response.json
```

AI 必须先写同目录 `.tmp` 文件，再原子重命名为 `<32位GUID>.request.json`。不要直接写最终文件，也不要重用 RequestId。单个请求上限 128 KiB。

## PlayMode 行为

进入或退出 PlayMode 时，Bridge 自动停止 Inbox 文件监听；这是临时暂停，不会撤销已经授予本机 Editor 的授权，回到 EditMode 会自动恢复。此时写入 Inbox 的请求不会在当前 PlayMode 执行，会留到返回 EditMode 后重新扫描；AI 应等待 EditMode 或经已受信的 UnityMCP 控制通道请求本次 PlayMode 临时恢复监听。它不会擅自杀掉进入 Play 前已启动的只读 Worker；各 Task 必须自行实现取消/暂停语义。

受信 UnityMCP 必须在 Unity 主线程调用：

```csharp
ESAutomationAiBridge.TrySetTrustedPlayModeListening(true, out string reason);
```

如果 UnityMCP 只支持 `ExecuteMenuItem`，可执行 `【ES】/自动化/AI 控制/PlayMode 临时恢复收件箱监听`；暂停菜单位于同一路径。这个 API 和菜单都不能开启首次用户授权、不能由 Inbox 请求调用，并会在退出 PlayMode 后自动失效。即使监听恢复，只有注册描述中明确 `allowInPlayMode = true` 的任务可启动；当前场景扫描不允许在 PlayMode 执行。

## 直接调用场景扫描

`requestId` 必须替换为新的 32 位十六进制 GUID：

```json
{
  "protocolVersion": 1,
  "requestId": "0123456789abcdef0123456789abcdef",
  "actorId": "codex.local",
  "action": "runTask",
  "payload": {
    "taskId": "es.scene.scan",
    "taskVersion": 1,
    "preset": "default",
    "input": {}
  }
}
```

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

Bridge 另外提供三个固定的 Editor 主线程动作；它们不是任意命令执行，也不能在 PlayMode 写入场景：

- `getUnityCompilationState`：读取自动编译、脚本编译、Editor 更新和 PlayMode 状态。
- `setUnityAutoCompilation`：用 `{ "enabled": true|false }` 开启或关闭本次 Editor 会话的自动刷新/程序集 Reload 锁。状态保存在 `SessionState`，不会伪造 Unity 全局设置。
- `triggerUnityCompilation`：用 `{ "forceRefresh": true|false }` 请求 AssetDatabase 刷新和脚本编译；自动编译关闭时明确返回阻断。
- `modifyActiveScene`：只允许当前已加载 Active Scene，使用显式 `scenePath`、`operations`、`save`、`dryRun`。操作白名单为 `setActive`、`setName`、`setTag`、`setLayer`，每项带 `targetPath` 和 `value`；真实修改经过 Undo、标记 Dirty，并仅在 `save=true` 时保存场景。

示例：

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
    "dryRun": false
  }
}
```

`dryRun=true` 只验证对象和操作计划，不写场景；`save=true` 不等于允许任意资产写入，只保存当前 Active Scene。AI 仍必须通过本机已授权 Inbox，Unity 实际导入、编译、ReloadDomain 和场景运行结果需要另外取得 Unity 验收证据。
