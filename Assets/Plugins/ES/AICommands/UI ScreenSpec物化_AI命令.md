# UI ScreenSpec 物化 AI 命令

## 必须先读

- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` 中命中的 UI 路由与 SourceRefs
- 本命令全文及其绑定的 `es.ui.materialize-screen@1` TaskContract

## 直接生效协议

当用户要求通过当前 Unity Editor 生成 UI Prefab、Fixture Scene 或视觉证据时，AI 必须：

```text
1. 先读取 es-ui-prefab-authoring Skill、UI Knowledge 路由和本命令全文。
2. 只能通过 AIBrain planTask -> runTask 调用 es.ui.materialize-screen@1。
3. 输入只能是项目 Assets/UI/Contracts 根下的 ScreenSpec v3，以及 ES/UIEvidence 下的安全证据目录。
4. 不接受脚本、命令行、绝对路径、任意 Prefab/Scene 输出路径或业务数据。
5. 当前 Unity Editor 必须保持打开并已由用户启用 AI Bridge；不得自动关闭或启动第二个 Editor。
6. 物化完成只证明 Unity 生成执行完成；截图和结构快照必须单独读取，不能伪称视觉验收通过。
```

命令类型：安全执行。
默认改文件：是，仅由受信 UI Materializer 写入固定 Generated UI 与 ES/UIEvidence 根。
风险等级：L2。

## 执行合同

```text
commandId: ui.materialize-screen
taskId: es.ui.materialize-screen
taskVersion: 1
入口：AIBrain planTask -> runTask -> ESAutomationFacade -> 当前 Unity Editor 主线程
能力：MaterializeUI（不等同通用 WriteAssets）
输入：specPath、evidenceRoot、profiles、states
输出：受 ScreenSpec 决定的 Prefab、Fixture Scene、结构快照和 GPU 证据路径

当前用户已明确要求本次运行时物化时，`planTask` 与 `runTask` 可以额外携带：

```text
userDirectedRuntime: true
userInstructionHash: 当前用户运行指令的 SHA-256
```

这不是全局跳过。AIBrain 只对本命令绑定的 `ui.materialize-screen` /
`es.ui.materialize-screen@1` 与 `MaterializeUI` 能力接受该一次性当前用户授权；
仍然要求 PlanHash、TaskContract、目标路径边界、幂等键和运行回执。其他 Skill、命令和
`runtimeEligibility` 不受影响。
```

## 禁止事项

```text
- 禁止直接调用 Unity CLI、Process、脚本或 Workbench 作为生产入口。
- 禁止传入 prefabPath、fixtureScenePath、resultPath 覆盖 ScreenSpec 或证据边界。
- 禁止把 Fixture 状态当成真实库存、战斗、经济或输入系统。
- 禁止把 placeholder、静态校验或 Materializer Completed 写成商业视觉验收通过。
```

## ContractCompleteness

```text
cancellation: before planTask/runTask commit may cancel; after Unity dispatch report Unknown and stop follow-up writes.
recovery: retain the invocation and evidence directory, use a new invocationId for retry, never replay an uncertain materialization blindly.
validation: ScreenSpec v3 schema, fixed input roots, PlanHash/TaskContractHash, idempotency, Unity response and separate visual-evidence status.
evidenceRef: planHash, taskContractHash, commandBodyHash, RunId, ScreenSpec SHA-256, generated paths and snapshot/PNG hashes; visualAcceptance remains not-claimed unless separately evidenced.
allowRoots: Assets/UI/Contracts ScreenSpec inputs and the declared ES/UIEvidence plus Materializer Generated UI roots only.
denyPaths: arbitrary scripts/commands, absolute paths, business data, sourceAbsolutePath, Git/.git, AIWarnings, AICommands, ProjectSettings, Packages, release, Runtime and Library; deny-overrides.
```

## 交付格式

```text
1. AIBrain PlanHash、TaskContractHash、CommandHash。
2. RunId、ScreenSpec 路径与 SHA-256、profiles/states、dryRun 和状态。
3. Prefab、Fixture Scene、快照与 PNG 路径；明确 visualAcceptance=not-claimed。
4. Unity 当前 Editor 是否真实响应；未运行时明确报告剩余证据缺口。
```
