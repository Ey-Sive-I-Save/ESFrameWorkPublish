# ESFramework AI Test Runtime

状态：最小纵向切片，待 Unity Editor、Player 与 IL2CPP 验收。

该包提供不依赖 Unity Test Runner 的 Player 端确定性执行层。AI 或离线工具只负责生成计划；Runner 负责协议校验、显式 Capability 调用、单步/总超时、取消、状态码和证据报告。

## Player 入口

```text
-esAITestPlan <absolute-plan-path>
-esAITestInbox [optional-plan-path]
-esTest
-esAITestQuit
```

`-esAITestInbox` 未提供路径时读取：

```text
Application.persistentDataPath/ESAITest/inbox/plan.json
```

`-esTest` 不依赖计划文件，直接启动内建的 `ESTEST.Direct` 基线 Run。AI 或运行时代码也可直接调用：

```csharp
ESAITestPlayerBootstrap.TryStartESTEST(out string error);
ESAITestPlayerBootstrap.TryStartESTEST(planPath, out error);
ESAITestPlayerBootstrap.TryStartESTEST(request, out error);
```

AI 协作入口：

```text
AICommand: Assets/Plugins/ES/AICommands/ESAITest_直接启动ESTEST_AI命令.md
Skill: $es-start-estest
```

## 受控自主驱动

需要“自己玩”时，使用自主会话，而不是把任意自然语言直接投递给输入系统：

```csharp
ESAITestPlayerBootstrap.TryStartAutonomy("完成当前关卡并到达出口。", out string error);
```

该入口创建一个仍受单 Run 保护的 ESAITest Runner，先执行一次 `attention.snapshot`，随后等待对话 AI/外部策略桥逐回合提交决策：

```csharp
ESAITestPlayerBootstrap.SubmitAutonomyDecision(
    new ESAITestAutonomyDecisionDto
    {
        runId = "AUTONOMY-...",
        turnIndex = 1,
        decisionId = "decision-1",
        mode = "goal", // goal / explore / recover / stop
        rationale = "See 显示出口在右侧，先取得输入控制并向右移动。",
        steps = new[] { /* 有界 act/verify/wait Step */ },
    },
    out error);
```

每回合最多 16 个 Step，最多 256 回合；总时间、调用预算、输入 Lease、取消和连续失败保护仍由 Runner 强制执行。`explore` 用于有限探索，`recover` 用于卡死恢复；连续失败达到阈值会进入 `autonomy_stuck`。自主会话终止为通过时，所有产生实际游戏效果的 ToUse 回执都必须有身份匹配的后续 Verify/Wait，否则终态为失败。Dashboard 会显示目标、回合和等待决策状态。

对话 AI 的职责是读取 See/报告后提交下一回合有界决策；Unity 运行面或授权的外部桥仍是必要前提。系统不会把未定义自然语言直接当成任意按键或对象调用。

### 受控自然语言入口

所有自然语言必须先经过 `ESAITestNaturalLanguageRouter`，再进入已有 Bootstrap、Publish、Cancel 授权入口：

```csharp
ESAITestPlayerBootstrap.TryExecuteNaturalLanguage(
    "告诉测试 AI：先观察左侧红色按钮，P1，TTL=60",
    out ESAITestNaturalLanguageExecutionResultDto result,
    out string error);
```

跨进程或聊天重试时应传入稳定的消息 `requestId`：

```csharp
ESAITestPlayerBootstrap.TryExecuteNaturalLanguage(
    "继续推进当前关卡。",
    "chat-message-20260808-0001",
    out ESAITestNaturalLanguageExecutionResultDto result,
    out string error);
```

同一 `requestId` 在受控窗口内只会执行一次；Runner 状态变化时会因 RunId 冲突拒绝旧请求。

成功执行过的 `requestId` 会以 256 条/24 小时上限、命名 Mutex 和原子替换方式保存到 `Application.persistentDataPath/ESAITest/natural-language/request-ledger.json`；域重载或 Player 重启后继续拒绝仍在保留期内的请求。账本损坏时会改名隔离并从空账本安全恢复。执行结果会同时回传原始规范化文本、解析后的 intent/message/goal/P/TTL、拒绝原因、绑定 RunId、最终 RunId 和幂等账本是否写入成功。

当前白名单意图为：启动 ESTEST、启动有限自主、自动拉起外部 AI、复用已有 AI、仅准备外部 AI、Publish、取消和状态查询。路由器会规范化文本，拒绝多意图、缺少 goal/message、多个 P 等级和非法 TTL；未识别文本只返回拒绝/澄清，不会直接操作游戏。外部 AI 的 `mode`、Step、Capability、输入 Lease 和 Verify 约束仍由原有 Runner 强制执行。

对话式短句也支持受控自适应选择：运行中的 Runner 会把“继续推进/帮我玩”等指令作为一次 P2 Publish 交给当前 AI；没有运行中的 Runner 时，只有检测到绝对路径和 SHA-256 均通过的受信 Agent 才会自动选择外部 AI 启动，否则返回澄清，不会盲目拉起进程。

外部 Codex/Agent 对话 IPC 使用：

```text
Application.persistentDataPath/ESAITest/conversation/
  requests/*.json       # LLM 输出的 esaitest.conversation-intent/v1
  receipts/*.received.json
  receipts/*.accepted.json
  receipts/*.executed.json
  receipts/*.verified.json
  processed/*.processed.json
```

Unity 每 0.5 秒最多处理 2 个请求；有请求时短暂降为 0.1 秒，空目录不分配扫描数组。`route` 必须是白名单意图、置信度至少 0.75 且通过严格 DTO 校验。四段回执分别表示收到、授权接受、调用返回和验证状态；`verified` 可能是 `pending_prompt_consumption` 或 `runner_active_business_verify_pending`，不伪装成业务成功。

最小意图信封示例：

```json
{
  "schema": "esaitest.conversation-intent/v1",
  "protocolVersion": 1,
  "requestId": "chat-message-0001",
  "source": "external-agent",
  "createdUtcTicks": 638902000000000000,
  "timeToLiveSeconds": 60,
  "originalText": "告诉测试 AI：先观察左侧红色按钮",
  "route": {
    "schema": "esaitest.natural-language-route/v1",
    "protocolVersion": 1,
    "accepted": true,
    "requiresClarification": false,
    "intent": "PublishPrompt",
    "normalizedText": "告诉测试 AI：先观察左侧红色按钮",
    "message": "先观察左侧红色按钮",
    "priority": "P1",
    "ttlSeconds": 60,
    "confidence": 0.96
  }
}
```

### 外部 AI 自动拉起与持续桥接

需要让 Player 自动拉起一个持续运行的外部 AI Agent 时，先在启动 Unity/Player 的同一用户环境中配置：

```text
ESAITEST_AUTONOMY_AGENT_PATH=<受信 Agent 的绝对可执行文件路径>
ESAITEST_AUTONOMY_AGENT_SHA256=<该文件的 64 位 SHA-256（自动启动和准备均必填）>
```

然后使用：

```text
-esAITestAutonomy <目标文本> [-esAITestQuit]
```

或调用：

```csharp
ESAITestPlayerBootstrap.TryStartAutonomyWithExternalAi("完成当前关卡并到达出口。", out string error);
```

复用已经运行的外部 Agent 时使用手动桥接入口。它只创建一个新的受 Runner 约束的会话目录，绝不再拉起第二个 Agent 进程；已有 Agent 读取新会话的 `session.json` 后，按同一 `requests/decisions/status` 协议接入：

```text
-esAITestAutonomyExisting <目标文本>
```

或：

```csharp
ESAITestPlayerBootstrap.TryStartAutonomyUsingExistingAi("继续当前关卡并到达出口。", out string error);
```

如果只需要创建一个待测试 AI 的准备记录而暂不开始 Runner、外部进程或 AI 回合，使用：

```text
-esAITestAutonomyPrepare <目标文本>
```

或调用：

```csharp
ESAITestPlayerBootstrap.TryPrepareAutonomyExternalAi(
    "先观察场景并规划出口路线。", out string preparationPath, out string error);
```

准备记录只包含目标、受信 Agent 文件名及已校验 SHA-256，不包含脚本、命令、凭据或网络地址。

Player 只启动该环境变量指向的直接可执行文件，不接受计划、自然语言或收件箱提供的脚本、Shell、解释器、网络地址和凭据；`cmd/bat/ps1/sh` 会被拒绝。每个 Run 只启动一个 Agent 子进程，传入固定参数 `--esaitest-autonomy-session <session.json>`，随后由 Agent 持续读取 `requests/*.json` 并原子写入 `decisions/*.json`。Agent 不应再派生脱离本 Run 的常驻子进程；取消时 Player 会写停止信号并终止它持有的直接子进程。

Agent 必须在同一会话目录原子写入 `status/status.json`，内容使用 `esaitest.autonomy-bridge-status/v1`，`runId` 必须匹配，`sequence` 必须严格递增，`state` 为 `ready`/`alive`/`stopping`，并持续更新 `utcTicks`。Player 以 0.1/0.5 秒有界轮询处理请求和决策；启动超时、心跳超时、进程退出、RunId 冲突和非法决策都会进入同一 Runner 的明确失败终态。取消或 Run 完成时会原子写入 `control/stop.json` 并停止由本 Run 拉起的子进程。

会话目录位于：

```text
Application.persistentDataPath/ESAITest/autonomy/<RunId>/
  session.json
  requests/turn-0001.json ...
  decisions/*.json -> *.accepted / *.rejected
  status/status.json
  control/stop.json
```

外部 Agent 不需要轮询 Unity 屏幕；每个 `attention.snapshot` 的完整 JSON 值会保留在最近事件中并随回合请求发送。实际 AI 模型、密钥和模型服务仍由外部 Agent 自己管理，不会进入 Unity 计划或报告。

安全边界：自动启动只接受绝对路径、直接可执行文件和匹配的 SHA-256；启动时使用受控工作目录与最小化环境变量，不把 Unity 的凭据环境继承给 Agent。请求和决策采用原子文件、RunId、requestId、一次性 nonce、TTL、大小上限和单次消费校验，过期、重放、跨 Run 或伪造字段会被拒绝。文件桥属于同一用户本机 IPC：这些校验能防止误写、迟到和重放，但不能把同用户恶意进程变成可信来源；生产环境仍应使用受控账户/目录 ACL，并监控 Agent 派生的孙进程。

## 三类方法能力声明

业务程序集可以用三类方法特性声明可供 ESAITest 作者发现的能力：

```csharp
[ESAITestToUse("game.inventory.use-item", "使用一个物品", 1, "Inventory")]
public static ESAITestUseResultDto UseItem(string itemId) { ... }

[ESAITestToSee("game.player.health", "读取玩家生命值", 1, "Player")]
public static float SeePlayerHealth() { ... }

[ESAITestToVerify("game.player.alive", "验证玩家仍存活", 1, "Player")]
public static bool VerifyPlayerAlive() { ... }
```

Editor 通过 ES AssemblyStream 的三个独立注册器收集：

```text
ESAITestToUseMethodRegistration
ESAITestToSeeMethodRegistration
ESAITestToVerifyMethodRegistration
  -> ESAITestAttributedCapabilityRegistry
```

首个切片只接受 `public static` 同步方法；参数和返回值必须是基础类型、字符串、enum、一维数组或 `[Serializable]` 纯 DTO。`ToSee` 不能返回 `void`，`ToVerify` 只能返回 `bool` 或 `ESAITestVerifyResultDto`。需要可排障行动回执时，`ToUse` 应返回 `ESAITestUseResultDto`；需要完整期望/实际值和证据时间点时，`ToVerify` 应返回 `ESAITestVerifyResultDto`。重复 `capabilityId`、多特性、泛型、`async void`、`ref/out`、Unity Object、Scope、Handle、Lease 与循环对象图会进入拒绝诊断。

该 Registry 与 `ESRuntimeWatchRegistry` 完全独立。每个通过校验的 Attribute 清单项都会明确标记 `executionStatus=editor_discovery_only`：它只代表 Editor 作者侧源码发现证据；Player 不使用 AssemblyStream，不做全程序集反射，也不直接携带 `MethodInfo`。Player 的实际执行仍必须通过显式 `ESAITestCapabilityProvider` 或后续生成/烘焙的确定性适配器接入。

结果原子写入：

```text
Application.persistentDataPath/ESAITest/<runId>/
  result.json
  summary.md
  diagnostics.json
  request.json
  manifest.json
```

## 首片协议

- `protocolVersion` 当前为 `1`。
- Step 操作：`see`、`verify`、`wait`、`act`。
- 每份计划必须有 1–256 个唯一 Step；总超时最多 1800 秒，单 Step 最多 300 秒且不得超过总超时；每个 Step 最多 32 个大小受限且键不重复的参数。超时和轮询间隔必须是有限正数，轮询最短为 50ms。可轮询的 `see/wait` 计划在提交时会估算调用数，超过每 Step 1024 次直接拒绝；运行期仍有同一硬上限，避免畸形计划制造无界 CPU、GC 或报告体积。
- Capability 响应采用严格三态契约：拒绝时 `accepted=false` 且不可重试；成功时必须为 `passed`；已接受但条件未满足时必须为 `verification_failed`。不满足契约的 Provider 响应会被 Runner 转为 `internal_error`，不会伪装为测试通过。
- 每个 Capability 响应都会由 Registry 统一加盖 `esaitest.capability-response/v1` 信封：`runId`、`invocationId`、`sceneGeneration`、`stepId`、Capability、操作、命令、Provider 与版本均以运行时请求和实际注册 Provider 为准；Provider 自行填入的身份字段不会覆盖边界事实。Step 诊断同时保留最后完整响应，AI 不需要从散乱日志猜测“到底调用了谁”。
- 每个同步 Capability 调用都有 Run 内单调递增的 `invocationId`，并记录发起时的场景代际。每个 Step 会汇总首末调用 ID、场景代际、调用次数、重试次数、总/最坏调用耗时与最终 Provider 状态；相同条件的轮询不会逐次扩张事件时间线。调用期间发生场景代际变化时，原返回值只作为历史输出保留，Runner 将其拒绝为当前场景的有效证据。该摘要进入 `result.json`、`diagnostics.json` 和 `summary.md`，同时保留性能诊断入口。
- 内置 UGUI Capability：`unity.ugui`。
- UGUI 命令：`act + button.click`；`see/verify/wait + button.interactable/toggle.state`。
- UGUI `button.click` 会返回 `ESAITestUseResultDto` 回执，记录 Step、Capability、命令、`ExecuteEvents.Execute` 是否实际命中 PointerClick 处理器，以及目标 UI 的点击前/后活动和可交互状态；它只证明直接 EventSystem 投递，不证明真实指针射线、Down/Up、遮挡、焦点或业务成功。UGUI `verify/wait` 会返回 `ESAITestVerifyResultDto`，记录同一组身份字段、目标、期望值、实际值、证据类别、时间与帧号。二者均进入 Step 结果、事件时间线、`diagnostics.json` 与 `summary.md`。
- 若一个 `verify/wait` Step 要证明先前 `act` 的后续效果，必须显式传入 `{ "key": "verifyUseStepId", "value": "<act-step-id>" }`。该值必须引用位于当前 Step 之前的 `act` Step；Runner 还会核对行动回执与验证证据的 `runId`、场景代际、`invocationId`、Step、Capability、命令和目标，只有完整匹配且验证真实通过时才会将 `businessEffectVerified` 标为真。它允许用不同 Capability 验证真实业务效果，且不会把同目标的普通 UI 状态读取、跨场景旧输出或伪造回执误写成点击成功。

```json
{
  "stepId": "verify-game-entered",
  "operation": "wait",
  "capabilityId": "game.session",
  "command": "session.state",
  "target": "gameplay",
  "expectedValue": "true",
  "arguments": [
    { "key": "verifyUseStepId", "value": "click-start" }
  ]
}
```

- 正式游戏控制应使用既有 `es.input` Capability：`control.acquire` 取得 Owner/Token/Generation Lease，再执行 `button.set`、`button.pulse`、`axis.set` 或 `vector2.set`，最后 `control.release`。它走 ESInputModule、RuntimeMode 与 ESInputService；每项操作会写入行动回执（Owner、Generation、是否仍持有 Lease，不记录不透明 Token），并每次由模块重新校验 Owner/Token/Generation 与当前 Run 归属。轴和 Vector2 只接受有限数值；`control.state` 的 verify/wait 会写入验证证据。UGUI 直投与此链路是两种独立证据，不能互相替代；`es.input` 写入只证明正式输入源已接受，业务消费者的最终结果仍须在显式关联的后续 ToVerify 中验证。
- 同一进程一次只允许一个活动 Run；Runner 为一次性对象，避免 runId 与 Registry 被并发覆盖。
- `continueOnFailure` 只允许计划继续采集证据；只要存在失败 Step，Run 总结仍为 `failed`，取消与总超时始终保持对应的终态。
- Capability 必须显式注册，并绑定当前 `runId` 与场景代际；不进行全场景反射查找。
- Capability 与报告只返回纯 DTO，不返回 Unity 对象引用。

## AI See 观测层

内建 `unity.observe` Capability 会在 ESAITest Run 激活时自动注册，不需要向正式场景或 Prefab 添加组件。所有观测只在明确 Step 调用时执行，不进入常规每帧热路径。

```text
see + attention.snapshot 人类意识式增量观察：提示中断、UI 焦点、场景记忆和独立冷却
see + prompt.next        按 P0→P4、同级 FIFO 消费一条运行时一次性提示
see + screen.capture   帧末截取真实游戏屏幕，默认临时隐藏 ESAITest Dashboard
see + screen.latest    返回最近截图元数据
see + ui.snapshot      返回活动 UGUI 控件、文字、值、交互状态和屏幕矩形
see + scene.snapshot   返回有界场景层级、Transform、组件类型和屏幕投影
see + runtime.snapshot 返回场景、屏幕、时间、Camera、选中 UI 和最近截图状态
see + snapshot.full    一次返回 Runtime + Camera + UI + Scene 快照
```

常规 AI 循环应优先使用 `attention.snapshot`，不要重复调用 `snapshot.full`。默认注意力预算为 UI 48、场景对象 96、深度 4，UI 最短刷新间隔 0.25 秒、Scene 最短刷新间隔 1 秒，并且默认不重复回传仍在记忆中的大数组。UI 焦点变化、场景代际变化、显式 `target`、`forceRefresh=true`，以及 P0/P1 提示会触发对应的注意力刷新；其中 `forceRefresh=true` 必定无视 `minimal` 模式并刷新 UI 与场景缓存。
每次注意力结果都会返回 `samplingCostMilliseconds`、刷新标记、样本年龄和保留记忆数量；最新采样耗时还会进入 Runtime Dashboard 与 Run `diagnostics.json`，便于实际 Player/Profiler 验收，而不是仅凭静态代码宣称性能达标。

观测没有常驻每帧扫描：UI/场景只在明确的 See Step 和上述刷新条件下收集；基础相机快照最多每 0.1 秒更新一次并按场景代际失效。Runtime Dashboard 仅在状态事件或运行期间每 0.5 秒刷新，文本未变化时不会再次写入 Canvas。截图、全量 UI/场景快照仍属于按需的高成本操作，不能放入高频计划循环。

```text
attention=adaptive|minimal|focused|context
uiIntervalSeconds=0.1..10
sceneIntervalSeconds=0.25..30
forceRefresh=false
returnRetainedMemory=false
```

游戏代码可直接投递一次性 AI 提示；收件箱有锁防竞态、最多保留 64 条、支持 TTL，并在容量满时优先淘汰最低等级的最旧提示：

```csharp
ESAITestAIPrompt.Publish(
    "Boss 进入二阶段，优先躲避红圈。",
    ESAITestAIPromptPriority.P1,
    source: "BossPhaseController",
    timeToLiveSeconds: 20f);
```

提示只在 `attention.snapshot` 或 `prompt.next` 中消费一次。P0 最高、P4 最低；同等级严格按投递顺序消费。

AI 也可以在 ESAITest 计划中直接模拟游戏侧的 `Publish` 行为。该入口使用独立的 `unity.prompt` Capability，最终仍调用同一个线程安全收件箱；来源会强制添加 `ai.simulated/` 前缀，报告中可以区分真实游戏推送与 AI 模拟推送：

```json
{
  "stepId": "simulate-low-health-prompt",
  "operation": "act",
  "capabilityId": "unity.prompt",
  "command": "prompt.publish",
  "arguments": [
    { "key": "message", "value": "玩家生命值过低，寻找安全位置。" },
    { "key": "priority", "value": "P0" },
    { "key": "source", "value": "health-test" },
    { "key": "ttlSeconds", "value": "10" }
  ]
}
```

`message` 也可由 Step 的 `target` 或 `expectedValue` 提供。TTL 必须是有限数值。执行结果返回与调用关联的 `runId`、场景代际、`invocationId`、`promptId`、最终来源、P 等级、TTL、入队顺序、过期时刻、当前待消费数量以及（如发生）容量淘汰的提示 ID；Runtime Dashboard 同步显示最近一次 Publish。下一 Step 可使用 `see + attention.snapshot` 验证提示是否按优先级进入 AI 意识。

内建 `ESAITestPlayerBootstrap.TryStartESTEST()` 已包含同样的两步基线：AI 模拟发布一条 P1 提示，然后立即执行注意力观察。直接启动 ESTEST 即可覆盖 Publish → Inbox → Attention 的最小闭环。

从项目 Codex 对话中说“你快告诉测试AI……”时，项目 Skill `$es-publish-aitest-prompt` 会把后续正文原子写入：

```text
Application.persistentDataPath/ESAITest/prompt-inbox/*.json
```

活动 ESTEST 的 `ESAITestExternalPromptInbox` 只在 Run 生命周期内轮询：空闲间隔 0.5 秒，连续处理期间 0.1 秒，每轮最多 8 条。有效消息进入同一个 `ESAITestAIPrompt` 队列，文件改名为 `.consumed` 留下接收证据；过期或非法消息分别改名为 `.expired`、`.rejected`，不静默删除。

`screen.capture` 是异步帧末观测。Provider 首次返回 `retryable`，Runner 会在 Step 超时范围内继续轮询，直到 PNG 原子写入 Run 暂存区。最终报告包含：

```text
Application.persistentDataPath/ESAITest/<runId>/
  result.json
  summary.md
  diagnostics.json
  request.json
  manifest.json
  artifacts/screens/*.png
```

`diagnostics.json` 是按 RunId 生成的紧凑排障索引：首个失败 Step 的完整请求（含 arguments）、最后活动事件、最近 See、提示队列和建议先查的链路都在同一对象内；`summary.md` 首屏同步显示该索引。测试执行终态（`executionStatusCode`）与报告落盘终态（`reportStatusCode`）分开保存，报告写入失败不会覆盖原始测试结论。报告在临时目录内会机械核对五个主文件、RunId、执行/落盘终态、首个失败 Step 和每个 Artifact 路径；任一项不一致即拒绝原子提升。完整的原始证据仍以 `result.json`、事件时间线和 `artifacts/` 为准。

截图和 Artifact Manifest 包含相对路径、字节数与 SHA-256。Editor 报告收集器会连同 `artifacts/` 一并收集。

常用参数：

```text
screen.capture: superSize=1..4, includeDashboard=false
ui.snapshot: maxUi=1..512, maxTextLength=32..2048
scene.snapshot: maxObjects=1..1024, maxDepth=0..16, includeComponents=true
通用: includeInfrastructure=false；target 可按层级路径子串过滤
```

显式全量命令的默认硬上限是 UI 128、场景对象 256、层级深度 6、文本 256 字符。达到上限会在快照 `warnings` 中明确标记，避免“大量信息”退化为无界扫描或卡死 Player。所有扫描仍只发生在明确 See Step，绝不进入每帧热路径。

此层不替代 Unity Test Runner、PlayMode、Profiler、Player Build 或 IL2CPP 验收。
