# TaskFocusContext 接入 ESFramework 方案

> 状态：Designed / 待实现
> 版本：v1
> 责任边界：全局 AI 协作上下文；不替代用户授权、AIWarnings、GoalRevision、RoutePlan 或 EvidenceSet。

## 1. 结论

ES 需要的是全局 `TaskFocusContext`，而不是“超级语义专属 FocusAuthority”。超级语义（`0分/零分/迭代/兼容`）只是提升注意力等级的触发源；普通明确任务、用户纠偏、交接接收、验证失败和高风险变更也可以建立或更新同一工作重心上下文。

注意力的工程含义是：动态决定本轮 AI 优先读取什么、优先分析什么、允许修改什么、必须回避什么，以及是否需要用户确认后才能继续。它不能直接改变模型底层推理算力；“增加脑力”通过更高的读取集合、分析阶段、方案比较、审查和验证门禁实现。

## 2. 已验证事实与未验证前提

### 已验证事实

- ES 已有 `GoalRevision -> RoutePlan -> RouteStage -> EvidenceSet -> Receipt -> completionDecision -> deliveryAcceptance` 主链。
- 超级语义中央索引为 `.agents/SUPER_SEMANTICS_REGISTRY.json`，解析器为 `.agents/scripts/Resolve-ESSuperSemantics.ps1`。
- `0分/零分`、`迭代`、`兼容` 已定义为需要深度用户指导的项目推进分叉口语义。
- `AGENTS.md` 要求超级语义先于普通路由解析，并禁止自由扩展。
- Letta 的核心记忆块可被动态编辑并重建后续系统上下文；LangGraph 支持可持久化状态、`interrupt` 和人工确认后恢复；OpenHands 提供事件驱动的会话状态管理；这些是外部设计参考，不是 ES 当前实现事实。

### 未验证前提

- 当前 Codex/其他宿主是否能自动把每次用户消息送入 ES 解析器。
- ES 是否需要跨会话持久化注意力上下文，还是只在当前任务生命周期内有效。
- 注意力上下文是否需要 Unity Editor 可视化面板；本方案默认先做文件/回执层。

## 3. 核心对象

对象名固定为 `TaskFocusContext`；`Focus` 表示当前工作重心，`attention` 只作为优先级和推理强度属性，避免把全局能力错误绑定到某个超级语义或误解为神经注意力。

```json
{
  "attentionId": "AT-20260828-0001",
  "scope": "conversation|task|project",
  "status": "candidate|pending-confirmation|confirmed|superseded|closed",
  "source": "user-request|super-semantic|verification-failure|handoff",
  "focus": "本轮唯一工作重心",
  "priority": "normal|elevated|critical",
  "reason": "为什么提升注意力",
  "goalRevision": "GR-...",
  "allowedScope": [],
  "forbiddenExpansion": [],
  "requiredReads": [],
  "requiredQuestions": [],
  "acceptanceSignals": [],
  "reasoningProfile": "standard|deep|critical-review",
  "authorityStatus": "unconfirmed|user-confirmed",
  "createdAtUtc": "...",
  "updatedAtUtc": "...",
  "supersedes": null,
  "sourceHashes": {}
}
```

### 字段语义

- `focus`：当前工作重心，不是永久项目事实。
- `allowedScope`：本轮允许读取/修改的范围；空值不能解释成全库。
- `forbiddenExpansion`：明确禁止 AI 自由发挥的范围。
- `requiredReads`：建立深度模式前必须回读的最小权威来源。
- `reasoningProfile`：流程深度配置，不声称控制模型物理算力。
- `authorityStatus`：只有用户确认后才允许进入实施阶段。
- `sourceHashes`：绑定产生该重点的项目规则、合同和用户确认快照。

## 4. 状态机

```text
NoAttention
  ↓  明确任务/超级语义/失败反馈
Candidate
  ↓  需要用户补充或回读上下文
PendingConfirmation
  ↓  用户确认目标、范围、禁止项、验收
Confirmed
  ↓  生成深度 RoutePlan
Active
  ↓  目标完成、被新重点替换或会话关闭
Closed / Superseded
```

### 强制迁移规则

1. `Candidate -> PendingConfirmation`：命中 `0分/零分/迭代/兼容` 或用户明确要求改变工作重心。
2. `PendingConfirmation -> Confirmed`：必须存在用户可重读的确认消息；AI 自己总结不能替代确认。
3. `Confirmed -> Active`：必须生成带 `GoalRevision`、读取集合、禁止扩展和验收信号的 RoutePlan。
4. `Active -> Superseded`：用户明确改变重点；旧注意力不删除，保留替代关系。
5. 任何状态不得直接跳到 `Active`，不得由模型自评或普通路由推荐建立权威。

## 5. 运行链路

```text
用户消息
  ↓
超级语义解析（短文本全文；长文本仅头尾）
  ↓
TaskFocusContext 更新
  ↓
普通意图/Skill 路由读取 TaskFocusContext
  ↓
用户指导确认（必要时 interrupt）
  ↓
GoalRevision + RoutePlan
  ↓
按 requiredReads 读取权威来源
  ↓
深度分析 / 对抗审查 / 有界实施
  ↓
EvidenceSet + Receipt + Closeout
```

### 与超级语义的关系

| 来源 | 注意力级别 | 默认动作 |
|---|---|---|
| 普通明确任务 | normal | 按目标路由 |
| 用户明确指定重点 | elevated | 建立候选重点并确认范围 |
| `迭代` | elevated | 确认本轮迭代目标和验收 |
| `兼容` | elevated | 确认兼容对象、版本和边界 |
| `0分/零分` | critical | 停止自由推进，先重对齐 |
| 高风险写入/发布/宿主动作 | critical | 提升验证和授权门禁 |

## 6. 注意力如何真正影响后续 AI

注意力必须投影到四个可观察位置，而不是只写一个标签：

1. **上下文投影**：把 `focus`、`requiredReads`、最近确认和 `forbiddenExpansion` 放入下一轮系统/任务上下文。
2. **读取裁剪**：只读取 `requiredReads` 与目标范围，长文本继续执行头尾采样；禁止因“深度”递归加载全库。
3. **工具边界**：`allowedScope` 外的文件、Unity、Runtime、网络、Git 和宿主进程仍需原有用户授权；注意力不能扩大权限。
4. **验证升级**：`standard` 至少静态合同；`deep` 增加方案比较和对抗审查；`critical-review` 必须保留未证实项并等待用户确认。

## 7. 与现有 ES 合同的接入点

| 现有对象 | 接入方式 |
|---|---|
| `GoalRevision` | `TaskFocusContext.goalRevision` 必须一对一绑定当前目标版本 |
| `RoutePlan` | 由 `focus + allowedScope + requiredReads + reasoningProfile` 生成，不能绕过 |
| `RouteStage` | 增加 `attentionStatus` 和 `focusRevision` 输入，阶段失败不自动改重点 |
| `TaskContext` | 保存当前 TaskFocusContext 的只读快照和版本号 |
| `EvidenceSet` | 记录注意力来源、确认回执、读取集合和验证结果 |
| `Receipt` | 记录状态迁移、用户确认、源哈希和替代关系 |
| `completionDecision` | 只能判断当前重点是否完成，不能推断用户的新重点 |
| `deliveryAcceptance` | 继续独立判断交付是否被接收 |
| `AIWarnings` | 提供禁止扩展、生命周期、权限和证据边界；不存临时用户重点 |
| `AGENTS.md` | 规定每轮解析和首行超级语义回执；不保存具体任务状态 |

## 8. 外部方案借鉴与取舍

### 采用 Letta 的部分

- 将“当前工作重心”作为可更新的核心上下文块。
- 重点变化后重建后续上下文，而不是只写历史日志。
- 采用短期工作上下文与长期项目知识分层。

### 采用 LangGraph 的部分

- 用显式状态迁移表达 `PendingConfirmation -> Confirmed`。
- 在高风险节点中断等待用户输入，确认后从持久化状态恢复。

### 采用 OpenHands 的部分

- 用事件记录注意力变化、上下文重建和工具边界变化。
- 将状态保存与事件输出分开，便于审计和回放。

### 不直接采用的部分

- 不把外部框架的记忆数据库、Prompt 模板或 Agent 身份直接复制到 ES。
- 不把模型自我编辑记忆视为用户授权。
- 不把上下文变长、调用次数增加或模型名称变化宣称为“分配了更多脑力”。

## 8.5 开源参考透明度

本方案不复制第三方框架源码；“全部内容可见”通过来源、版本/分支、观察范围和 ES 采用边界实现。任何未列入“已观察范围”的能力都不得写成已验证事实。

| 开源项目 | 原始来源 | 本方案已观察的具体内容 | ES 采用方式 | 未宣称内容 |
|---|---|---|---|---|
| LangGraph | [langgraph GitHub](https://github.com/langchain-ai/langgraph)、[interrupt / HITL 文档](https://github.com/langchain-ai/langgraph/blob/main/libs/langgraph/langgraph/types.py) | `StateUpdate`、`Command`、`interrupt`、checkpointer、人工确认后恢复 | Focus 状态迁移、用户确认中断、可恢复 RoutePlan | 不复制 LangGraph Runtime，不证明 ES 已有其持久化执行器 |
| Letta / MemGPT | [Letta memory schema](https://github.com/letta-ai/letta/blob/main/letta/schemas/memory.py)、[Letta memory docs](https://github.com/letta-ai/letta) | Core memory blocks、`update_block_value`、append/replace、上下文重建 | 将已确认 Focus 投影到后续上下文 | 不复制 Letta 数据库、Agent 身份或自动记忆写入权限 |
| OpenHands SDK | [ConversationState](https://github.com/OpenHands/software-agent-sdk/blob/main/openhands-sdk/openhands/sdk/conversation/state.py)、[Agent architecture](https://docs.openhands.dev/sdk/arch/agent) | 事件驱动 Agent 循环、会话状态、持久化与上下文管理 | 记录 Focus 变化事件和上下文投影事件 | 不证明 OpenHands 有独立 Focus 一等对象 |
| SWE-agent | [SWE-agent GitHub](https://github.com/SWE-agent/SWE-agent) | 受限工具/命令面、围绕任务目标的执行轨迹 | 用 `allowedScope` 与 `toolBoundary` 限制 ES 操作面 | 不把工具限制等同于动态 Focus 状态机 |

### 可见性要求

1. 每个外部来源必须保留原始链接、来源日期、分支/版本（若可得）和本方案引用的文件/章节。
2. 外部来源只作为设计参考；ES 的当前事实仍以源码、AIWarnings、合同和真实回执为准。
3. 不以摘要代替源码可见性；需要复核时，应沿原始链接读取对应实现。
4. 不复制第三方完整源码、许可证文本或不可验证的二手解读到 ES 项目。
5. 若外部链接、分支或实现发生变化，相关结论标记 `stale`，重新核对后才可更新方案。

## 8.6 开源框架适配性筛选与渐进获得策略

“来源可见”不等于“ES 已获得全部特性”，也不意味着四个框架都应该接入。第一原则是先判断适配性，再决定是否获得某项能力；禁止一次性引入四个外部运行时、四套依赖或四套 Adapter。

适配顺序固定为：

```text
候选盘点 → 适配性评分 → 单候选隔离 PoC → 失败复盘
→ 用户确认是否继续 → 小范围 Adapter → 专用验收
```

任一候选若在语言/runtime、许可证、生命周期、性能、离线性、可观测性或权限边界上不满足 ES 约束，应标记 `not-adaptable` 或 `reference-only`，不为其建立生产接入层。

### 特性分层

| 状态 | 含义 | 允许的结论 |
|---|---|---|
| `catalogued` | 已登记官方来源、版本/commit、许可证和能力描述 | 仅表示可追踪 |
| `source-available` | 已取得对应开源源码或官方 API 文档，并完成许可证审查 | 可以开始适配 |
| `adapted` | 已实现 ES Adapter，字段和错误语义完成映射 | 可在隔离测试中调用 |
| `enabled` | 在 ES 配置中显式启用，并声明权限/资源预算 | 可进入指定工作流 |
| `accepted` | 通过该特性的专用静态、集成和必要 Runtime 验收 | 只对指定版本/平台成立 |

### 候选适配性闸门

| 维度 | 必须回答的问题 | 不通过时 |
|---|---|---|
| 运行时边界 | 是否需要常驻 Python/Node 服务？能否与 Unity/ES 解耦？ | `reference-only` |
| 状态模型 | 能否映射到 `TaskContext/GoalRevision/RoutePlan`，而不引入第二套权威？ | `not-adaptable` |
| 上下文控制 | 是否真的能动态改变后续模型可见的 Focus，而非只保存日志？ | 不进入 Focus 核心链 |
| 权限与工具 | 是否能接入 ES 的用户授权、工具白名单和副作用边界？ | 禁止生产启用 |
| 依赖与许可证 | 依赖、许可证、版本锁定和离线安装是否可接受？ | `blocked-supply-chain` |
| 性能与部署 | 延迟、内存、进程和网络要求是否适合目标宿主？ | 只保留概念借鉴 |
| 证据与恢复 | 状态是否可回放、可恢复、可审计？ | 不能声称 Accepted |

### 第一批只选一个候选

基于当前目标“动态设置工作重心”，第一轮只做一个隔离 PoC，不同时接入四个框架：

1. **优先评估 Letta**：验证 Core Memory/Memory Block 修改后，后续上下文是否真正改变；不引入其数据库和 Agent 服务作为 ES 运行时依赖。
2. **备选评估 LangGraph**：若 ES 更需要状态迁移、中断和用户确认，则单独评估 `StateUpdate + interrupt + checkpointer`；不与 Letta 并行接入。
3. **OpenHands、SWE-agent 暂不接入**：先作为事件循环、工具边界和沙箱设计参考，待第一候选 PoC 得出适配结论后再决定是否评估。

PoC 的唯一目标是验证“Focus 变化能否可靠投影到下一轮上下文并受用户确认约束”，不是验证框架全部功能，也不是建立生产依赖。

### 必须新增的基础设施

1. `ExternalAgentFeatureCatalog`：逐项登记框架、能力 ID、来源 URL、版本/commit、许可证、依赖、输入/输出、失败语义、ES Adapter 和验收证据。
2. `IAgentFeatureAdapter`：统一适配接口，但不抹平各框架的状态、记忆、人工确认和权限差异。
3. `FeatureCapabilityMatrix`：记录每项能力在 ES 的 `catalogued/source-available/adapted/enabled/accepted` 状态，禁止汇总成“全部已支持”。
4. `FeatureConformanceReplay`：对每个 Adapter 运行正例、无效输入、拒绝越权、恢复、幂等和版本漂移测试。
5. `LicenseAndSupplyChainReceipt`：保留许可证、依赖锁定、来源哈希、变更日期和安全审查回执。

### 四个框架的完整特性边界

- LangGraph：图状态、持久化 Checkpointer、Interrupt/HITL、Command 更新、流式事件和恢复；每项单独登记，不能只接入 `interrupt` 就称为 LangGraph 全特性。
- Letta：Core Memory、Recall/Archival Memory、Memory Blocks、工具驱动记忆更新、上下文重建和持久化 Agent 状态；必须区分 ES Focus 投影与 Letta 自编辑记忆。
- OpenHands：事件模型、Agent 循环、工具执行、ConversationState、上下文管理和沙箱/执行边界；必须单独核对 SDK 与完整产品的授权和功能差异。
- SWE-agent：受限工具面、ACI、任务轨迹、补丁/测试循环和环境策略；工具受限不自动等于 ES 的 Focus 或权限合同。

### “获得某候选特性”的验收门槛

只有在适配性闸门通过、用户确认继续、并同时满足以下条件后，才可以对某个指定框架/版本的某个特性声称“ES 已获得”：

1. 官方能力目录已冻结，未登记能力数为零或明确列入排除清单。
2. 每项能力都有来源、许可证、版本/commit、Adapter 和 Feature ID。
3. 依赖、配置、权限、资源预算和失败语义均完成映射。
4. 每项能力通过专用 ConformanceReplay；关键执行能力另有 Runtime/Release 证据。
5. 框架升级触发 SourceHash/FeatureMatrix 漂移，旧结论自动变为 `stale` 并要求重放。
6. “未接入”“不可移植”“许可证不允许”“宿主专属”必须作为显式排除项，而不是静默遗漏。

因此，本方案当前只承诺“已建立适配性筛选和单候选渐进接入路径”，不承诺四个框架全部适配，也不承诺任何候选已经进入生产运行时。每次只推进一个候选，前一候选未完成适配结论前，不得启动下一个候选的实现。

## 9. 分阶段落地

### Phase 1：静态合同与解析投影

- 新增 `TaskFocusContext` JSON Schema。
- 将现有超级语义解析结果投影为 `Candidate/PendingConfirmation`。
- 增加正例、否定例、歧义例、重复回放和长文本头尾采样测试。

### Phase 2：用户确认与上下文投影

- 新增确认回执，记录用户原文、确认时间和源哈希。
- 将已确认重点投影到下一轮任务提示和 Skill 路由输入。
- 未确认时只允许分析和提问，不允许实施。

### Phase 3：RoutePlan / TaskContext 接入

- 将 TaskFocusContext 版本绑定到 GoalRevision 和 RoutePlan。
- RouteStage 检查重点版本，发现替代或漂移时停止旧计划并重规划。
- 记录 EvidenceSet 和不可变 Receipt。

### Phase 4：宿主适配与冷启动

- Codex、Claude Code、Gemini CLI、Cursor、Windsurf、Copilot 分别使用各自可发现的项目/用户指令入口。
- 宿主只负责把当前消息和确认结果交给 ES；权威仍在项目合同和用户确认。
- 新窗口必须验证 TaskFocusContext、欢迎块、菜单和超级语义回执均可发现。

## 10. 验收标准

### 静态验收

- schema 字段、状态迁移和禁止迁移完整。
- `0分/零分/迭代/兼容` 唯一命中进入正确注意力级别。
- 未确认重点不能生成 `Active` 或实施许可。
- 用户改变重点会产生新版本并保留 `supersedes`。
- 长文本中间触发词不会改变注意力状态。
- 相同输入和确认回执可确定性重放。

### 运行验收

- 新窗口能读取当前 TaskFocusContext 并在下一轮使用最新重点。
- 用户确认后，后续回复确实包含重点、范围和禁止扩展，而不是只显示标签。
- 用户改变重点后，旧 RoutePlan 不再继续执行。
- 工具边界、Unity/Runtime 和发布权限没有因注意力升级而扩大。

### 明确不宣称

- 不宣称控制模型底层 token 预算或物理推理算力。
- 不宣称注意力状态本身等于用户授权。
- 不宣称静态测试证明宿主、Unity、Runtime 或新窗口行为。

## 11. 推荐实施顺序

先做 Phase 1 和 Phase 2，再决定是否接入真实 `TaskContext/RoutePlan`。在没有用户确认回执和冷启动证据前，不应把 TaskFocusContext 写入长期 AIWarnings，也不应把它作为全局永久规则。
