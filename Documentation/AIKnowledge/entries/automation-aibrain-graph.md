# AutomationCenter、AIBrain、Graph 与受管 Worker 完整机制

`KnowledgeId`: `es.project.automation-aibrain-graph.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `automation`, `aibrain`, `task-contract`, `worker`, `automation-run-record`, `agent-execution-graph`, `skill`, `aicommand`, `mcp`
`RequiredReads`: `ESAutomationCenter与受管Worker治理`, `AgentSkills与AICommands协作边界`
`ContentHash`: `f76cd45cf7f816f6406d99ed1208aab43c914facfe74d93c7bfbd74806482e64`

## Scope

本条目拥有 AIBrain PlanHash、TaskContract、受管 Worker、RunRecord、SkillCall 与执行 Graph 的协议和恢复边界。它不拥有用户授权，也不拥有 Stable Graph 的作者身份、`edge.order`、迁移、Undo、Snapshot/Bake 细节；这些事实由 `es.project.stable-graph-v2.v1` 负责。

## Trigger and routing

- 触发词：AIBrain、Automation、TaskContract、Worker、RunRecord、SkillCall、AISkill、FanOut/Join、取消、恢复。
- 精确路由：`agent-execution-graph` 区分执行编排图与作者 Graph；`automation-run-record` 区分自动化运行记录与性能证据记录。只有 TaskContract/RunRecord/SkillCall 等执行语义出现时才命中本条目。
- 相邻路由：稳定身份、Edge Order、Graph Undo、迁移或 Bake Snapshot 应回退到 Stable Graph 条目；文件读取 Snapshot 应回退到 `es.engineering.task-read-snapshot.v1`。
- 仅出现泛化 `graph` 且无法确定决策对象时必须停止并请求领域信息，不同时加载所有 Graph 条目。

## Decision rules

- 只有 AICommand、Skill、TaskContract、Worker Adapter 和输入 Schema 都能唯一解析且哈希闭合时，`ManagedAIBrain` 计划才能继续；这不阻断当前用户直接通道。
- 缺少命令返回 `NoMatchingCommand`；能力面未暴露 `planTask` 返回 `PlanTaskUnavailable`；两者不得互换。
- Plan 后任一绑定变化、PlanHash 过期或请求不一致时标记 stale 并重新规划；不得复用旧授权。
- RunRecord 身份、InvocationId 或 input hash 不完整时停止恢复，不按目录和时间戳猜测。
- 需要进程、Unity 或外部系统时，当前用户必须明确点名相应动作；通过 AIBrain/Worker 执行时还必须满足 AICommand 与 TaskContract 协议。真实运行回执约束完成声明，不是开工批准。

## Verified facts and responsibility layers

| 层 | 拥有的事实 | 明确不拥有 |
|---|---|---|
| AIWarnings | P0、实现约束和证据门槛 | 当前用户动作授权 |
| AICommand | 受管通道当前任务协议 | 用户授权或通用工作流实现 |
| Project Skill | 可复用工作流、references/scripts/evidence | 用户授权或自行扩大源码/外部范围 |
| AIBrain | 路由、知识选择、Skill/Command/TaskContract 校验、绑定 Invocation 的限时限次执行授权 | 用户授权、Process 启动、直接 Assets 写入 |
| AutomationFacade | 任务描述、能力核对、执行入口、观察/取消/输入 | 任意脚本命令行 |
| AutomationCenter | TaskContract、受信 Adapter、路径策略、进程与 RunRecord | AI 语义决策 |
| Execution Graph | 确定性执行结构与 SkillCall/Task 节点关系 | Stable Graph 作者事实或权限扩张 |

## TaskContract 与进程门禁

TaskContract 声明 taskId/version、Worker 类型/身份、允许能力、输入输出、read/write roots、timeout、DryRun 与幂等重试。Registry 只接受受信 C# 注册；ProcessRunner 只启动已注册 Adapter 产生的 `ProcessStartInfo`，调用方不能传解释器、脚本路径或任意命令行。路径读写、目录创建、复制和删除都经过 `ESAutomationPathPolicy` 的根目录约束。

RunRecord 使用有限状态迁移：`Created` 可进入 `Starting` 或前置失败终态；`Starting` 可进入由受控会话确认的 `Accepted`、兼容的 `Running` 或运行失败终态；`Accepted` 可直接 `Completed`，也可进入 `Running`；`Running` 再进入 `Completed`、`Failed`、`Cancelled` 或 `TimedOut`。`Blocked` 与 `DryRun` 只允许出现在源码声明的前置阶段，终态才写 `finishedAtUtc`。受管进程绑定输出上限、进程树、超时、ReloadDomain 终止与 registry 清理，避免孤儿 Worker。

## AIBrain 计划与限时限次授权

```text
listCapabilities
  -> 发现 AIWarnings / Knowledge / Skills / AICommands / Tasks / MCP host projection
planTask
  -> routeKeys 命中最小 Knowledge
  -> 验证 AICommand 正文和目录签名
  -> 验证 Skill SKILL.md/openai.yaml/governance.json
  -> 验证 TaskContract、Worker、capabilities、DryRun
  -> 计算 PlanHash
runTask
  -> 校验同一 PlanHash 与 Invocation
  -> 消费一个受 TTL、次数和幂等键约束的授权使用次数
  -> AutomationFacade.RunTask
```

PlanHash 绑定请求、Knowledge、AICommand、Skill 内容及治理元数据、TaskContract 和工作流。执行授权还绑定 Invocation、输入和授权策略代际，并把最大次数与过期时间纳入绑定哈希；存储同时记录已使用次数与已消费幂等键。有效期为 15 分钟；由受信宿主绑定当前用户指令的 L1 本地计划最多 20 次，L1/L2 `candidate-only` 计划最多 5 次，L3 或其他计划 1 次。任何可复用授权都要求每次调用提供新的非空 `idempotencyKey`；空键、重复键、过期、次数耗尽、缺失、篡改或请求不匹配都会被该通道拒绝。策略版本变化、授权存储缺失或存储策略代际不匹配会清空内存许可并要求重新签发；重复 PlanHash、非法次数、使用计数/幂等键不一致或异常延长 TTL 会使存储 fail-closed。同一授权额度耗尽后，重复签发不会重置计数，必须使用新的 Invocation 重新规划。外部 Bridge JSON 不能自报 `userDirected`，该标志只能由受信进程内宿主在直接构造 Coordinator 请求时设置。没有匹配 Command 时显式 `NoMatchingCommand`，不能借用其他合同；Knowledge 只能帮助选择 Skill，不能替代执行合同，也不能据此否决当前用户直接请求。

## Bridge 与 MCP

`ESAutomationAiBridge` 在 Unity 主线程处理文件队列和受信宿主调用，公开 `listCapabilities/planTask/runTask/getRun/cancelRun/submitInput`。Inbox 文件原子移入 Processing，响应后归档；失败时保留独立审计文件。MCP 列表是宿主能力投影，不伪造外部 MCP 已连接；场景写入还需要单独的 approval-waiting、approve/reject/revoke 生命周期。

## Execution Graph boundary

执行 Graph 消费已验证的 `ESAISkillExecutionSpec`，包含 Input、Task、SkillCall、Branch、ForEach、Approval、FanOut、Join 和 Output。这里的 canonical 事实是：每个 Task 节点仍须命中 TaskContract，每个 SkillCall 仍须通过 Project Skill 与 AIBrain 计划，运行恢复必须绑定有效 RunRecord、InvocationId 和 input hash。Stable Graph 如何保存身份、顺序、迁移和 Bake 由其 canonical 条目解释，本条目不复制。

Graph 是受管工作流的确定性表示，不是用户授权来源；在该执行通道内，每个 Task 节点仍必须命中 Automation TaskContract，每个 SkillCall 仍受 Project Skill 与 AIBrain 计划约束。

## Common AI failure modes

| 错误行为 | 典型症状 | 预防与恢复 |
|---|---|---|
| 把脚本或测试源码存在当作 Worker 可执行 | 没有 C# TaskContract/Adapter 仍尝试启动 Worker | 校验 Registry/Facade/Adapter；缺失则仅阻断该 Worker 通道，直接实现范围仍由当前用户指令决定 |
| 把 Knowledge 当执行合同 | 命中摘要后绕过 Command/Skill | 要求唯一 AICommand、完整 Skill 和 TaskContract；缺失时返回对应状态 |
| 混淆 `PlanTaskUnavailable` 与 `NoMatchingCommand` | 能力未暴露却声称目录无命令 | 分别记录能力探测与命令查询证据，不借其他命令扩权 |
| 使用 stale PlanHash | 元数据变化后重复 runTask | 废弃旧计划，以当前哈希重新 planTask |
| 按目录猜测恢复 Run | RunRecord 无效仍继续 | 保留失败证据并停止；只能用有效身份与输入哈希恢复 |
| 把 MCP 可见性当连接或授权 | 能力列表存在但调用失败 | 检查连接、权限和 TaskContract；未证明则不得执行 |

## Execution checklist

1. 开始前读取 Start/CurrentStatus/RuleIndex，并以 routeKeys 选择最多三个 Knowledge。
2. 核验 AICommand、Skill、TaskContract、Adapter、输入 Schema 和 write roots 的当前哈希。
3. 执行中绑定 PlanHash、InvocationId、idempotencyKey、RunRecord 和取消路径。
4. 完成后检查终态、结构化输出、失败/取消/超时和重复执行证据。
5. 禁止用文件、按钮、测试定义、目录或 MCP 列表存在声称执行成功。

## Evidence boundary

静态检查可以证明合同、路径、哈希和状态机定义存在；不能证明 Unity 主线程、Worker 进程、MCP、取消恢复或发布链实际通过。没有对应 RunRecord/运行回执时统一标记 `runtime-not-run`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md` (`842bc5d46a045f3e2f226426f005afb8f7114ba56646e623d245ea0f99a04166`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`6fc627e16930d541b1275bb5d687e1fdad8d96b751616002dbf2fbdbfa38fbc3`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs` (`e61a58d14237555a09207cf3e3c596b48e0ee2de6188d10584b950c62606d4d2`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`20b63b3db889b705ae740d366fa234b8ae49b50a60bf72056cd2a96b86db9b57`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs` (`0bc253ebba46f4deb28cc4820677ee02a7233a070d57b682bbe91242640fd13c`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESAISkillExecutionWorkflow.cs` (`7b17d81c0dc4bbf04d2e91df2c8e47e46c9b811de622dfef03c7f408476a192e`)

`EvidenceLevel`: `S1`（源码与静态构建可验证；未取得本次 Unity Worker/MCP 端到端回执）  
`StaleWhen`: AIBrain PlanHash、TaskContract、Worker Adapter、Bridge、Graph bake 或 RunRecord 合同变化。
