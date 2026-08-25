# ES Knowledge 三态落地可用性评分规范

状态：`Rubric-Implemented-Unvalidated`。本文件定义评分方法，不证明任何一次三态实验已经通过。

## 目的

本规范用于比较同一任务在三种上下文下的真实决策价值。三态都可以读取当前项目源码、配置、Schema
和测试；差异只在于是否提供项目 Knowledge、AIWarnings 和 Skill 参考资料：

- **A：项目源码基线**，可读取当前源码、配置、Schema 和测试，但不得读取 `Documentation/AIKnowledge`、
  `Assets/Plugins/ES/AIWarnings`、`.agents/skills` 或前组答案；它测量 AI 自行发现项目事实的能力。
- **B：项目源码 + 本地 Knowledge**，在 A 的可读范围上加入目标条目及其 `requiredReads`；若 requiredReads
  指向 AIWarnings/Skill，必须在报告中单列“Knowledge 导航/治理增益”，不得把它伪装成源码发现。
- **C：本地 Knowledge + 外部权威资料**，外部资料必须与目标版本和场景相关。

三组必须使用相同任务、相同输出结构和相同评分者；不得读取前组答案。该实验分数不替代
`es-knowledge-validator` 的静态状态，也不把静态结果升级为 Runtime/Release 证据。

## 实验有效性与反偏置门禁

只有满足以下条件，结果才可标记为 `Three-State-Experiment-Validated`；否则保持
`Rubric-Implemented-Unvalidated`：

1. A/B/C 使用三个真正新建且相互隔离的上下文、同一模型/版本、同一任务和同一输出模板；
2. 在查看任何输出前冻结本规范和评分表；
3. 三组都包含同等数量的隐藏负例、恢复案例和反例，不能只测试 Knowledge 已覆盖的正例；
4. 评分者只看到匿名化答案，不知道 A/B/C 条件；
5. 每个关键结论绑定实际读取证据，记录文件、范围、SourceRef 或外部来源；
6. B 只有在新增 Knowledge 证据改变了正确动作、停止条件、恢复路径或读取成本时才能计增益；
7. 文本更长、停止条件更多、引用更多、哈希更多均不得自动加分；
8. 增加 counterfactual 对照：移除 B 的 Knowledge，替换为等量但无关的文本，检测上下文长度效应；
9. 执行器记录真实 `inputTokens`、`outputTokens`、`reasoningTokens`（若可得）、读取文件/字节、查询次数、
   网络字节和每组工具/模型耗时；缺少可比成本遥测时 `ReadEfficiency` 与 `Total` 必须为
   `uncalculated`。

实验报告必须附带 `IsolationCheck`、`RubricFreeze`、`BlindScoring`、`NegativeCaseParity`、
`EvidenceBindings`、`CounterfactualResult` 和 `TelemetryCompleteness` 字段。任何一项为 false 或
`unknown`，不得宣称三态实验已验证。

## 三项核心指标

三态是实验条件，不是评分轴：A 为项目源码基线，B 为源码加 Knowledge，C 为 B 加版本匹配的外部一手资料。
每个条件都输出以下三项核心指标；`EvidenceConfidence`、`Contradictions` 和 `CoverageGaps` 是解释字段，
不是额外评分轴。

### DecisionUtility（0-100，越高越好）

按固定子项加权：

| 子项 | 权重 | 判定重点 |
|---|---:|---|
| 决策正确性与任务完成性 | 40 | 是否选对当前项目的动作、API、路由和边界 |
| 错误预防与停止判断 | 25 | 是否阻止越权、资产污染、错误验收和不可恢复操作 |
| 恢复性与可重试性 | 20 | 失败、漂移、重复或取消时是否给出可执行恢复路径 |
| 事实边界表达 | 15 | 是否区分已知、推断、未运行和需要补证据的部分 |

每个子项按 0-10 评分，`DecisionUtility = round(10 × 加权平均)`。

### EvidenceCoverage（0-100，越高越好）

先为当前任务建立 evidence checklist，再按已闭合项目计分，不能按“读过多少文件”直接计分：

| 覆盖面 | 权重 | 判定重点 |
|---|---:|---|
| read coverage | 20 | 必要源码、Schema、测试、Knowledge SourceRef 是否实际读取 |
| decision coverage | 50 | 每个关键动作、停止条件和权限/所有权判断是否有证据支撑 |
| closure | 30 | 关键数据流、失败路径、所有权、幂等、恢复和非声明是否闭合 |

每项按“已闭合检查项 / 计划检查项”计算，`EvidenceCoverage = round(100 × 加权覆盖率)`。
`EvidenceConfidence` 作为解释字段报告证据权威和新鲜度：0-30 通用推断，31-60 部分源码，61-80
源码/Schema/测试闭合，81-100 另有匹配的真实运行或恢复回执。

### 条件间的覆盖、效率与可用性

B 不要求机械继承 A 的全部源码读取。Knowledge 的合理作用就是通过路由、摘要、RequiredReads 和停止卡
减少源码搜索成本；因此 B 可能以较低的源码深度换取更快、更聚焦的正确决策。证据置信度可以下降，
但必须明确这是“覆盖减少”还是“事实冲突”，不能混为一谈。

每个条件必须同时报告：

- `EvidenceCoverage`：实际覆盖的源码、Schema、测试、Knowledge SourceRef、调用方和恢复路径；
- `ReadCost`：读取文件数、源码深度或人工/模型检索负担的相对变化；
- `DecisionUtility`：在当前任务中是否仍能做出正确、安全、可恢复的决策；
- `Contradictions`：新增证据与已读事实的真实冲突；
- `CoverageGaps`：未覆盖但可能影响当前决策的深层实现。

只有出现真实 `Contradiction`、`SourceDrift`、`Misroute` 或 `UnsupportedClaim` 时，才把问题判为
知识错误；仅仅因为 B 少读源码，应报告为覆盖缺口，并在 `ReadCost`/`DecisionUtility` 中体现其收益或代价。

如果 Knowledge 明确保持 `runtime-not-run` 且没有声称运行通过，运行证据缺失只降低对应声明的置信度，
不否定其静态规则的可用性。

### ReadCost 与 ReadEfficiency

`ReadCost` 是原始成本，越低越好，不直接与 0-100 指标混写；每组必须记录文件数、读取字节数、检索次数
和耗时。相对 A 基线计算归一化 `ReadEfficiency`（越高越好）：

```text
costIndex = 0.35×fileRatio + 0.25×byteRatio + 0.20×queryRatio + 0.20×timeRatio
ReadEfficiency = clamp(0, 100, round(100×(1-costIndex)))
```

若 A 不是可比基线，报告 `ReadEfficiency: uncalculated`，不得猜测。ReadEfficiency 是成本解释分，
不改变三项核心指标的定义。

### 总分与阻断

为了需要排序时提供可复现总分，使用：

```text
Total = round(0.50×DecisionUtility + 0.35×EvidenceCoverage + 0.15×ReadEfficiency)
```

总分不能绕过阻断规则：

- 任一关键事实 `Contradiction`、SourceRef 漂移、误路由或 unsupported claim：`blocked`；
- `DecisionUtility < 60`：不得判为可用；
- `EvidenceCoverage < 50`：只能输出受限范围结论，不能升级为项目级建议；
- `CoverageGap` 不直接扣分，但必须限制声明范围；
- 没有匹配的真实 Runtime 回执时，不能判 `runtime-supported`。

## 结论门禁

- `blocked`：存在关键 Contradiction、SourceDrift、Misroute、UnsupportedClaim，或 `DecisionUtility < 60`。
- `limited-static`：未阻断，但 `DecisionUtility` 为 60-74 或 `EvidenceCoverage` 为 50-69；只能给出受限范围结论。
- `usable-static`：`DecisionUtility >= 75` 且 `EvidenceCoverage >= 70`，且任务不要求未取得的 Runtime 证明。
- `usable-with-runtime-gap`：`DecisionUtility >= 75` 且 `EvidenceCoverage >= 50`，但目标声明包含尚未执行的 Runtime 行为。
- `runtime-supported`：满足 `usable-with-runtime-gap` 的静态门槛，并存在与目标声明匹配的真实 Runtime 回执。

没有统一的“无 Runtime 最高只能多少分”硬上限；限制的是具体声明。例如，编辑器静态边界可凭
源码和合同获得高可用性分，但“Unity 已运行通过”必须有对应 Runtime 证据。

## 网络增益规则

外部资料只有在满足以下条件并实际改善决策时才增加证据置信度或可用性分：

1. 来源是目标版本的官方/一手资料；
2. 来源直接支持当前场景中的 API 或行为；
3. 新信息改变了动作、停止条件、兼容性或恢复策略。

通用 provenance、JSON Schema 或流程建议只能作为背景，不能证明 ESFramework 的 PlanHash、授权、
CompletionDecision、Receipt、canonical、stale 或真实运行行为。

## 报告格式

每个场景必须输出 `Decision`、`Stop conditions`、`Evidence`、`Uncertainties`，并同时给出：

```text
DecisionUtility: <0-100>
EvidenceCoverage: <0-100>
ReadCost: files=<n>, bytes=<n>, queries=<n>, elapsedMs=<n>
ReadEfficiency: <0-100 | uncalculated>
EvidenceConfidence: <0-100>
Total: <0-100>
Verdict: usable-static | usable-with-runtime-gap | runtime-supported | limited-static | blocked
Attribution: A/B/C 相比前一条件具体改善或退化了什么
```
