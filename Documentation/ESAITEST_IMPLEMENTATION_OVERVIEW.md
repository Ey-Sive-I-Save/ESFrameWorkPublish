# ESAITest 已实现能力、优势、创新点与使用说明

状态：待源码复验的实施总结，成熟度为 `Implementing / Verifying`。  
最后验证：2026-08-09。  
适用版本：`com.esframework.aitest 0.1.0`，Unity 2022.3。  
适用源码入口：

- [`Packages/com.esframework.aitest/Runtime`](../Packages/com.esframework.aitest/Runtime)
- [`Packages/com.esframework.aitest/README.md`](../Packages/com.esframework.aitest/README.md)
- [`Assets/Scripts/ESLogic/Runtime/Developer/AITest`](../Assets/Scripts/ESLogic/Runtime/Developer/AITest)
- [`Assets/Scripts/ESLogic/Editor/Developer/AITest`](../Assets/Scripts/ESLogic/Editor/Developer/AITest)

> 本文总结当前项目中已经存在的源码能力，不替代 Unity Console、Test Runner、PlayMode、Profiler、Player 或 IL2CPP 验收。源码存在、程序集生成和真实运行证据必须分层理解。

## 一句话定位

ESAITest 是一个面向 Unity Editor 与 Player 的**受控 AI 游戏验收运行时**：AI、外部策略或人工计划只负责给出有界步骤，确定性 Runner 负责校验协议、调用显式 Capability、控制超时与取消、约束输入所有权，并生成可以追溯到具体调用和场景代际的证据报告。

它不是：

- Unity 内置大模型；
- 允许自然语言直接执行任意代码的遥控器；
- Unity Test Runner 的替代品；
- 仅凭截图或日志文本猜测成功的自动化脚本。

## 当前整体完成度

| 层级 | 当前结论 | 说明 |
| --- | --- | --- |
| 核心协议与 Runner | 已实现 | `see / verify / wait / act`、计划校验、超时、取消、单 Run、调用预算和结果状态均有源码 |
| Capability 注册与身份信封 | 已实现 | Provider 显式注册，并绑定 `runId + sceneGeneration`；响应由 Registry 加盖真实调用身份 |
| UGUI、观察、Prompt | 已实现 | 包内自动能力可完成最小 ESTEST、注意力观察、截图、UI/Scene 快照和一次性提示 |
| ES 正式输入接入 | 已实现源码接线 | `es.input` 使用 ESInput 的 Owner/Token/Generation Lease，不复用跨 Run 控制权 |
| 受控自然语言与本机 IPC | 已实现 | 白名单意图、持久幂等账本、四段回执、TTL 和有界轮询均存在 |
| 有限自主与外部 Agent 桥 | 已实现 | 回合、失败、时限、心跳、可信可执行文件和一次性 nonce 等边界已建立 |
| 证据报告与诊断 | 已实现 | 五个主文件、Artifact SHA-256、首个失败 Step、执行/落盘双终态和原子提升均存在 |
| Editor 作者侧能力发现 | 已实现 | `ToUse / ToSee / ToVerify` 可由 AssemblyStream 收集和诊断，但不直接成为 Player 执行入口 |
| 当前源码静态收录 | 有证据 | 生成的 `ESFramework.AITest.csproj` 收录 12 个 Runtime 源码；现有 Unity 程序集也存在 |
| 专项自动回归 | 不完整 | 当前未发现覆盖完整 Runner、自然语言、IPC、自主桥与报告故障矩阵的正式测试程序集 |
| Unity 实际运行验收 | 待完成 | 尚不能仅凭本文宣称当前工作树已完成 ReloadDomain、PlayMode 连续 Run 与 Dashboard 一致性验收 |
| Profiler、Player、IL2CPP | 待完成 | 没有本次总结对应的当前目标平台数据，不能宣称性能或发布级稳定 |

准确结论是：**源码纵向切片已经相当完整，安全和证据设计明显强于普通自动化原型；但产品级运行验收仍未闭合，成熟度应保持 `Implementing / Verifying`。**

## 已经实现的主要能力

### 1. 确定性计划执行

Runner 接受版本化 JSON DTO，并支持四种操作：

```text
see     读取状态或证据
verify  单次验证
wait    在有界超时内轮询验证
act     执行动作，不自动重试业务副作用
```

现有协议限制包括：

- 每个计划 1 至 256 个唯一 Step；
- 总超时最多 1800 秒；
- 单 Step 最多 300 秒；
- 每个 Step 最多 32 个参数；
- 最短轮询间隔 50ms；
- 单 Step 最多 1024 次 Capability 调用；
- 同一进程只允许一个活动 Run；
- `continueOnFailure` 只允许继续收集证据，不会把失败 Run 改写成通过。

### 2. 严格 Capability 边界

Player 不进行全场景反射查找。Capability 必须显式注册，并声明：

- `CapabilityId`；
- `ProviderId`；
- `ProviderVersion`；
- 支持的命令；
- 当前 `runId`；
- 当前 `sceneGeneration`。

每次调用生成单调递增的 `invocationId`。Registry 会覆盖 Provider 自报的身份字段，以实际请求和已注册 Provider 为准。调用期间如果场景代际变化，返回值只保留为历史输出，不再作为当前场景成功证据。

### 3. 行动回执与业务验证关联

`ToUse` 行为可以返回 `ESAITestUseResultDto`，`ToVerify` 可以返回 `ESAITestVerifyResultDto`。后续验证通过 `verifyUseStepId` 显式关联先前动作。

Runner 会核对：

- RunId；
- 场景代际；
- invocationId；
- Step；
- Capability；
- Command；
- Target；
- 验证是否真实通过。

因此“调用返回成功”和“游戏业务结果已经发生”是两个不同结论。自主 Run 想以通过结束，实际产生游戏效果的行为必须具备匹配的后续 Verify/Wait 证据。

### 4. AI 注意力式 See

内建 `unity.observe` 提供：

```text
attention.snapshot
prompt.next
screen.capture
screen.latest
ui.snapshot
scene.snapshot
runtime.snapshot
snapshot.full
```

常规 AI 循环优先使用 `attention.snapshot`。默认预算为：

- UI 48；
- 场景对象 96；
- 深度 4；
- UI 最短刷新间隔 0.25 秒；
- Scene 最短刷新间隔 1 秒。

它保留上一次认知，默认不重复返回未变化的大数组。UI 焦点、场景代际、目标、强制刷新及 P0/P1 提示才触发对应刷新。结果包含采样耗时、样本年龄、刷新标记和记忆数量，为后续 Profiler 验收保留入口。

### 5. 一次性优先级提示

`ESAITestAIPrompt` 支持：

- P0 至 P4，P0 最高；
- 同级严格 FIFO；
- 锁保护并发 Publish/Consume；
- 最多 64 条；
- TTL；
- 满容量时淘汰最低等级中最旧的提示；
- `attention.snapshot` 或 `prompt.next` 一次消费。

外部 Inbox 只在活动 Run 期间轮询，空闲 0.5 秒、活跃 0.1 秒、每轮最多 8 条。文件按结果改名为 `.consumed`、`.expired` 或 `.rejected`，保留接收证据而不是静默删除。

### 6. UGUI 与 ES 正式输入

内建 `unity.ugui` 支持按钮点击、可交互状态和 Toggle 状态。点击回执记录目标、路由、时间、帧号、执行前后状态和 PointerClick Handler 是否命中。

边界必须明确：UGUI 点击是 `EventSystem.pointerClickHandler` 直接投递，不证明真实指针射线、Down/Up、遮挡、焦点或业务成功。

正式游戏控制使用 `es.input`：

```text
control.acquire
button.set
button.pulse
axis.set
vector2.set
action.clear
control.state
control.release
```

该能力接入 ESInputModule、RuntimeMode 和输入服务，使用 Owner/Token/Generation Lease。它拒绝跨 Run 复用或释放 Lease，也拒绝 NaN、Infinity 和非法 Vector2。输入写入只证明正式输入源已接受，游戏结果仍应通过后续业务 Verify 证明。

### 7. 有限自主运行

自主模式的基本循环是：

```text
目标
  -> 首次 attention.snapshot
  -> 等待下一回合决策
  -> Act / Verify / Wait
  -> 继续、探索、恢复或停止
```

支持 `goal / explore / recover / stop` 四种决策模式。每回合最多 16 个 Step，最多 256 回合，并受到总时间、调用预算、输入 Lease、取消和连续失败限制。持续失败会进入 `autonomy_stuck`，超回合会进入 `autonomy_turn_limit`。

### 8. 受控自然语言路由

自然语言不会直接变成按键或任意方法调用，而是先被规范化为白名单意图：

- 启动 ESTEST；
- 启动有限自主；
- 自动拉起外部 AI；
- 复用已有外部 AI；
- 仅准备外部 AI；
- Publish Prompt；
- 取消；
- 查询状态。

路由器拒绝多意图、缺少目标/消息、多个优先级和非法 TTL。稳定 `requestId` 会写入最多 256 条、保留 24 小时的幂等账本；域重载或 Player 重启后仍能拒绝重放。账本损坏会被隔离，不会继续使用不可信内容。

### 9. 外部 AI 持续桥接

Unity 不内置模型。需要外部 AI 时，ESAITest 可以创建受 Run 管理的文件 IPC 会话，并可选择自动启动一个受信 Agent。

自动启动要求：

- 环境变量提供绝对可执行文件路径；
- 环境变量提供匹配的 SHA-256；
- 只接受直接可执行文件；
- 拒绝 `cmd / bat / ps1 / sh`；
- 不继承 Unity 的完整敏感环境；
- 每个 Run 只启动一个直接子进程；
- 使用心跳、RunId、requestId、nonce、TTL、文件大小和单次消费校验；
- 结束或取消时写入 `stop.json` 并停止该 Run 持有的子进程。

这能防止误写、迟到和重放，但本机同一用户恶意进程仍不因此自动可信；生产环境仍需要受控账户、目录 ACL 和子进程治理。

### 10. Editor 作者侧能力发现

业务代码可以声明：

```csharp
[ESAITestToUse("game.inventory.use-item", "使用物品")]
[ESAITestToSee("game.player.health", "读取生命值")]
[ESAITestToVerify("game.player.alive", "验证玩家存活")]
```

AssemblyStream 会在 Editor 收集和校验这些方法，拒绝实例方法、泛型、`ref/out`、不安全返回类型、Unity Object、Scope、Handle、Lease 和循环 DTO。

重要边界：清单中的 `executionStatus=editor_discovery_only` 只证明 Editor 发现并校验了源码声明。Player 不携带 `MethodInfo`，实际执行仍必须接入显式 Capability Provider 或确定性适配器。

### 11. 可核查报告与诊断

每个 Run 生成：

```text
Application.persistentDataPath/ESAITest/<runId>/
  result.json
  summary.md
  diagnostics.json
  request.json
  manifest.json
  artifacts/
```

`diagnostics.json` 汇总首个失败 Step 的完整请求和参数、最近活动、最近 See、Prompt 队列及建议排查链。测试执行终态 `executionStatusCode` 与报告落盘终态 `reportStatusCode` 分离，写盘失败不会覆盖真实测试结论。

最终目录发布前会机械核对五个主文件、RunId、终态、首个失败 Step 和每个 Artifact。Artifact 包含相对路径、字节数与 SHA-256；不一致时拒绝原子提升。

## 主要优势

1. **确定性高**：AI 只负责提出有界计划或回合决策，Runner 掌握实际执行和状态机。
2. **不会轻易伪报成功**：动作回执、业务验证、提示接收、提示消费和报告落盘都有不同状态。
3. **适合 Player 验收**：核心执行层不依赖 Unity Test Runner，可从命令行、Inbox 或 API 启动。
4. **与 ES 权威链一致**：正式控制走 ESInput Lease，场景验证通过显式 Provider，不另造第二套输入权威。
5. **故障定位集中**：首个失败、完整请求、调用性能、最近观察和建议链路进入同一诊断对象。
6. **成本有上限**：计划数量、超时、轮询、调用、截图、UI/Scene 快照和 IPC 都有预算。
7. **扩展边界清晰**：Editor 作者发现与 Player 实际执行分开，便于增加业务能力而不引入 Player 全程序集反射。
8. **跨进程证据明确**：原子文件、分阶段回执、TTL、哈希和状态后缀能区分“已落盘”“已接收”“已执行”“已验证”。

## 有代表性的创新点

### 注意力记忆代替重复全量扫描

常规 AI 不必每回合重新读取整个 UI 和场景。`attention.snapshot` 通过焦点、场景代际、目标和高优先级提示决定刷新范围，并保留未变化认知。这比固定频率全量快照更接近真实的“注意力切换”，也更适合长时间自主测试。

### 调用身份与场景代际组成证据链

每次调用的 Run、Step、Invocation、Provider 和场景代际都被记录。场景切换期间迟到的结果不会被当成新场景成功证据，解决了异步游戏测试中常见的“旧结果误判当前状态”问题。

### 行为成功必须由后续业务证据确认

系统不把“按钮事件已经发出”或“输入源已接受”直接写成业务成功。通过 `verifyUseStepId` 将行为和后续 Verify/Wait 严格配对，使报告能够回答“做了什么”和“游戏是否真的发生预期变化”两个问题。

### 执行结论与报告可靠性分离

`executionStatusCode` 与 `reportStatusCode` 分开保存。即使磁盘写入失败，测试本身的终态也不会被覆盖；报告只有通过完整性校验后才原子提升为最终目录。

### 自然语言先编译成受控意图

自然语言不是直接执行入口，而是编译成有限意图，并经过冲突检测、参数校验和持久幂等。这在保留易用性的同时，避免了“聊天文本等于任意控制权限”。

### 外部模型与 Unity Runtime 解耦

模型、密钥和策略服务留在外部 Agent。Unity 只暴露有界请求、证据和决策协议，并用可执行文件哈希、最小环境、心跳和 nonce 管理持续会话。这比把模型 SDK、密钥和任意网络调用塞进 Player 更容易治理。

## 使用方式

### 方式一：Unity 菜单

```text
【ES】/自动化与开发/自动化中心/ESAITest/控制中心
【ES】/自动化与开发/自动化中心/ESAITest/直接启动 ESTEST
【ES】/自动化与开发/自动化中心/ESAITest/中断当前 ESTEST
【ES】/自动化与开发/自动化中心/ESAITest/收集 Player 报告
```

控制中心可以选择 Plan JSON、进入 PlayMode 并启动、查看当前 Run、取消运行，以及查看 Editor 作者侧 Capability 清单。

### 方式二：Player 命令行

```text
-esTest [-esAITestQuit]
-esAITestPlan <absolute-plan-path> [-esAITestQuit]
-esAITestInbox [optional-plan-path]
-esAITestAutonomy <目标文本>
-esAITestAutonomyExisting <目标文本>
-esAITestAutonomyPrepare <目标文本>
```

启动参数互斥，一次只能选择一个启动入口。

### 方式三：C# API

```csharp
ESAITestPlayerBootstrap.TryStartESTEST(out string error);
ESAITestPlayerBootstrap.TryStartESTEST(planPath, out error);
ESAITestPlayerBootstrap.TryStartESTEST(request, out error);

ESAITestPlayerBootstrap.TryStartAutonomy(
    "完成当前关卡并到达出口。",
    out error);

ESAITestPlayerBootstrap.RequestCancel();
```

### 方式四：受控自然语言

```csharp
ESAITestPlayerBootstrap.TryExecuteNaturalLanguage(
    "告诉测试 AI：先观察左侧红色按钮，P1，TTL=60",
    "chat-message-20260809-0001",
    out ESAITestNaturalLanguageExecutionResultDto result,
    out string error);
```

跨进程或聊天重试时应提供稳定 `requestId`。

### 方式五：游戏代码发布一次性提示

```csharp
ESAITestAIPrompt.Publish(
    "Boss 进入二阶段，优先躲避红圈。",
    ESAITestAIPromptPriority.P1,
    source: "BossPhaseController",
    timeToLiveSeconds: 20f);
```

测试 AI 使用 `see + attention.snapshot` 或 `see + prompt.next` 一次消费。

### 方式六：最小 Plan JSON

```json
{
  "protocolVersion": 1,
  "runId": "example-run-001",
  "quitOnComplete": false,
  "plan": {
    "protocolVersion": 1,
    "planId": "example.attention",
    "totalTimeoutSeconds": 30,
    "steps": [
      {
        "protocolVersion": 1,
        "stepId": "observe",
        "operation": "see",
        "capabilityId": "unity.observe",
        "command": "attention.snapshot",
        "timeoutSeconds": 5,
        "pollIntervalSeconds": 0.1,
        "arguments": [
          { "key": "attention", "value": "adaptive" }
        ]
      }
    ]
  }
}
```

### 推荐 AI 循环

```text
attention.snapshot
  -> 选择一个有界动作
  -> act
  -> 用 verifyUseStepId 关联 verify/wait
  -> 根据新证据继续、探索、恢复或停止
```

不要把 `snapshot.full` 放入高频循环，也不要把 UGUI 直接点击或输入写入本身当作业务成功。

## 当前缺口与验收清单

稳定签收前至少需要：

1. 当前 Unity 工程 Refresh、ReloadDomain 与 Console 无任务相关错误；
2. 内建 ESTEST 的真实 PlayMode 连续运行；
3. 成功、Provider 拒绝、Step 超时、总超时、取消和重复 RunId；
4. 场景切换期间迟到调用结果拒绝；
5. 输入 Lease 获取、跨 Run 拒绝、释放和异常退出清理；
6. Prompt 外部 Inbox 的 `.consumed / .expired / .rejected` 实跑；
7. 自然语言账本跨域重载与损坏恢复；
8. 外部 Agent 启动、哈希错误、心跳超时、进程退出和重放拒绝；
9. 报告写入失败、Artifact 漂移、RunId 冲突和原子提升失败；
10. Dashboard、`result.json`、`diagnostics.json` 和 `summary.md` 一致性；
11. 目标 Player 的持续运行与多次连续 Run；
12. Profiler CPU、GC、截图和 UI/Scene 大预算采样；
13. Player Build 与 IL2CPP。

在这些证据完成前，最准确的表述仍是：

> ESAITest 已形成受控 AI 游戏测试的完整源码纵向切片，具备确定性执行、证据关联、注意力观察、正式输入接入、自然语言治理和外部 Agent 桥；当前仍处于运行与发布验收阶段，不能宣称 Stable。
