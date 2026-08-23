# AutomationCenter、AIBrain、Graph 与受管 Worker 完整机制

`KnowledgeId`: `es.project.automation-aibrain-graph.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `automation`, `aibrain`, `task-contract`, `worker`, `run-record`, `graph`, `skill`, `aicommand`, `mcp`  
`ContentHash`: `ca9f4bd1416a528ee739601ce4a0f880cb1df0a7c8db0577c6d4a90827e6c1d3`

## 分层职责

| 层 | 拥有的事实 | 明确不拥有 |
|---|---|---|
| AIWarnings | P0、禁止事项、证据门槛 | 单次任务权限 |
| AICommand | 当前任务权限合同 | 通用工作流实现 |
| Project Skill | 可复用工作流、references/scripts/evidence | 源码或外部写权限 |
| AIBrain | 路由、知识选择、Skill/Command/TaskContract 校验、一次性计划授权 | Process 启动、直接 Assets 写入 |
| AutomationFacade | 任务描述、能力核对、执行入口、观察/取消/输入 | 任意脚本命令行 |
| AutomationCenter | TaskContract、受信 Adapter、路径策略、进程与 RunRecord | AI 语义决策 |
| Graph | 稳定图快照与烘焙工作流 | 权限扩张、旧 NodeRunner 恢复 |

## TaskContract 与进程门禁

TaskContract 声明 taskId/version、Worker 类型/身份、允许能力、输入输出、read/write roots、timeout、DryRun 与幂等重试。Registry 只接受受信 C# 注册；ProcessRunner 只启动已注册 Adapter 产生的 `ProcessStartInfo`，调用方不能传解释器、脚本路径或任意命令行。路径读写、目录创建、复制和删除都经过 `ESAutomationPathPolicy` 的根目录约束。

RunRecord 使用有限状态迁移：Awaiting/Starting/Running 只能转向允许的 Completed、Failed、Cancelled、TimedOut、Blocked 等终态。受管进程绑定输出上限、进程树、超时、ReloadDomain 终止与 registry 清理，避免孤儿 Worker。

## AIBrain 计划与一次性授权

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
  -> 消费一次性 PlanHash
  -> AutomationFacade.RunTask
```

PlanHash 绑定请求、Knowledge、AICommand、Skill 内容及治理元数据、TaskContract 和工作流。授权被消费后从表中删除；过期、缺失、重复使用或请求不匹配都被拒绝。没有匹配 Command 时显式 `NoMatchingCommand`，不能借用其他合同；Knowledge 只能帮助选择 Skill，不能替代执行合同。

## Bridge 与 MCP

`ESAutomationAiBridge` 在 Unity 主线程处理文件队列和受信宿主调用，公开 `listCapabilities/planTask/runTask/getRun/cancelRun/submitInput`。Inbox 文件原子移入 Processing，响应后归档；失败时保留独立审计文件。MCP 列表是宿主能力投影，不伪造外部 MCP 已连接；场景写入还需要单独的 approval-waiting、approve/reject/revoke 生命周期。

## Stable Graph 到执行计划

Graph 快照烘焙为 `ESAISkillExecutionSpec`：Input、Task、SkillCall、Branch、ForEach、Approval、FanOut、Join、Output 节点，以及稳定 control edge、data binding 和 fan-out/join 配对。Baker 检查唯一入口、至少一个结构化输出、节点身份、端口类型和拓扑。Graph endpoint 为每次运行创建独立目录和 RunRecord，InvocationId 已存在但记录无效时拒绝猜测恢复；取消和生命周期回执必须匹配 SessionId/messageId。

Graph 是工作流的确定性表示，不是权限来源；每个 Task 节点仍必须命中 Automation TaskContract，每个 SkillCall 仍受 Project Skill 与 AIBrain 计划约束。

## 失败模式

- Worker 有脚本但没有 C# TaskContract/Adapter：Blocked。
- Skill 目录存在但缺 openai/governance 或含模板占位标记：不进入生产力面。
- Knowledge 命中但没有完整 Skill：NoMatchingSkill。
- Plan 后 Skill/Command/TaskContract/Knowledge 改变：旧 PlanHash stale。
- Graph Run 目录存在但 RunRecord 身份或 input hash 无效：拒绝恢复。
- MCP 能力可见但未连接/未授权：不能执行。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md` (`8213b590650bbca456ce77f2545419e695ae736d979cfc03de08d17728c01cdf`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`d1027d9905a34bc9c10215df61150eb1f4bfbb71c33fd5f83b90e9956aac296e`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs` (`7f38d24a53d8f2e382821085c8d711d6fd6ba086d0493bd7d33fb355cf2d12bd`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`9735b55bf6b2df8758050f2b84b053aabc0438ddf633c3c61ba43e4d684349d9`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`42ce9f445dee210e9ff788ae20680f1b8ba5b2dda94da5d6060630d2a72441c5`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs` (`0bc253ebba46f4deb28cc4820677ee02a7233a070d57b682bbe91242640fd13c`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESAISkillExecutionWorkflow.cs` (`7b17d81c0dc4bbf04d2e91df2c8e47e46c9b811de622dfef03c7f408476a192e`)

`EvidenceLevel`: `S1`（源码与静态构建可验证；未取得本次 Unity Worker/MCP 端到端回执）  
`StaleWhen`: AIBrain PlanHash、TaskContract、Worker Adapter、Bridge、Graph bake 或 RunRecord 合同变化。
