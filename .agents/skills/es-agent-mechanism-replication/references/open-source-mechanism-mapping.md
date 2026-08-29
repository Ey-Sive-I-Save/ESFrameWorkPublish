# Open-source Agent mechanisms → ABCD divergence/audit/iteration mapping

本报告只复用已绑定到项目研究集的六类公开机制；外部仓库/论文仍是 `external-source-not-bound`，不构成 ES Runtime 事实。

| 来源机制 | ABCD 发散 | ABCD 审计 | ABCD 迭代 | ES 落点 |
|---|---|---|---|---|
| SWE-agent ACI | 产生受限工具动作候选 | 检查路径、权限、输入和工具调用 | 失败动作按同一 RoutePlan 重试 | RouteStage + bounded operation |
| Reflexion | 生成失败归因候选 | Verifier 检查反思是否有证据 | CorrectionCycle retry/replan | finding/verification Receipt |
| Tree of Thoughts | Expand、隔离、剪枝、回溯候选分支 | 比较分支证据和风险差异 | 选择分支后进入下一轮 | branch snapshot + OutcomeEvaluator |
| Petri | 并行 token/假设探索 | 独立 auditor/judge 检查迁移合法性 | token 恢复、重放和预算耗尽 | TaskContext CAS/event store |
| EnvTrustBench | 生成带环境假设的候选 | 重验环境、工具版本和观察证据 | 环境失败时 replan/stop | EvidenceSet + environment gate |
| AuditBench | 生成调查 probe/候选解释 | 比较工具输出、证据与 Agent 决策 | 根据审计 gap 进入下一轮 | audit receipt + claim-cap |

## 完整 ABCD 外循环

```text
IterationRound
  → Divergence (candidate generation/isolation/prune/backtrack)
  → Independent Audit (evidence/tool-decision consistency)
  → C selection or policy selection
  → CorrectionCycle (retry/replan/branch/stop)
  → Verification Receipt
  → next round or stop
```

`CorrectionCycle` 是内循环记录，不拥有 Task 生命周期。`TaskContextRuntime` 仍独占 Revision、CAS、EvidenceSet、Receipt 和 completionDecision。

## 最小闭环不变量

- 每个候选有 `branchId`、父分支、快照哈希、验证谓词和风险差异；分支不得共享可变状态。
- 每次审计绑定独立 `auditorRef`、EvidenceRefs 和可重放判断；零 finding 只表示局部观察。
- 每次尝试使用 `attemptNo` 与幂等键；提交前重新读取当前 CAS，不能复用旧版本号。
- 未通过验证的候选只能是 `candidate/review/claim-cap`，不能进入 completionDecision。
- SourceDrift、环境不可信、工具调用与决策不一致时，默认 `stop-and-report` 或 `create-new-plan`。
- `ABCD.Dynamic` fallback 仍须显式授权，不得静默切换。

## 场景覆盖

- 代码/资产修改：ACI bounded action + ToT branch isolation + Task CAS。
- 多候选架构方案：ToT expansion/prune + AuditBench comparison + C selection。
- 外部 Worker/工具调用：EnvTrustBench environment evidence + SWE-agent operation allowlist。
- 失败恢复：Reflexion attribution + CorrectionCycle bounded retry/replan。
- 审计与合规：Petri transition trace + AuditBench evidence/decision consistency。
- 来源漂移与过期回执：source hash invalidation + claim-cap + replan。

这些映射支持静态合同和事件回放；Unity、Worker、网络、性能和发布行为仍需独立 Runtime/Release 验收。

## 当前差距与验收边界

| 能力 | v1 状态 | 仍未证明 |
|---|---|---|
| 候选生成/隔离/剪枝/回溯 | `implemented-static`（ABCD orchestrator + event contract） | Unity 资产/跨进程真实隔离 |
| 独立审计与工具-决策一致性 | `implemented-static`（auditor/evidence 事件） | 外部 auditor、隐藏负例和人工校准 Runtime |
| 多轮预算/选择/停止 | `implemented-static`（round、attempt、stop 事件） | 长时运行、并发调度和恢复性能 |
| CAS/幂等/Receipt 门禁 | `implemented-static` + event-store fixture | TaskContextRuntime/Worker 真实持久化联测 |
| ABCD Dynamic fallback | `explicit-only` 声明 | 运行时 resolver 与 fallback 事件 |
| Unity、Worker、网络、发布 | `runtime-not-run` | 必须由对应 Runtime/Release profile 单独验收 |

因此，v1 可以宣称“具备可回放的发散—审计—迭代编排骨架”，不能宣称已经完成所有外部 Runtime 闭环。

## 工业级实现要点（ABCD v1.1）

公开框架中真正值得复用的不是名称，而是它们对“状态、预算、观察和决策”的分离：

1. **SWE-agent ACI 的动作表面最小化**：工具调用先归一为有限操作和参数形状，再由 RouteStage 决定是否可执行。ABCD 不把自然语言修复建议直接当作动作；动态控制器只接收结构化候选和审计记录。
2. **Reflexion 的反思必须成为可验证 Finding**：反思文本只是输入，不是结论。只有带 FindingReceipt、VerificationReceipt 和当前 CAS 的 CorrectionCycle 才能影响下一轮。
3. **Tree of Thoughts 的搜索预算显式化**：候选数量、轮次、尝试次数和剪枝/回溯状态都写入事件链；没有预算的“继续想”不是工业级迭代。
4. **Petri 的 token/transition 思路**：每个阶段消耗明确前置 token，事件转移矩阵拒绝越级；TaskContext 的 Revision/CAS 是唯一提交闸门，不复制第二套生命周期。
5. **EnvTrustBench 的信任校准**：环境、工具版本和外部观察必须独立重验；环境不可信只能 `replan` 或 `stop`，不能通过加权分数“平均成通过”。
6. **AuditBench 的独立审计**：审计者、验证器定义、授权证明和证据引用必须分开绑定；审计通过不等于 Task 完成，完成仍需平台 OutcomeEvaluator。

当前对应实现：

- `ES/Automation/ABCD/ESABCDDynamicController.psm1`：有界候选生成 → 审计 → 选择 → CorrectionCycle → 验证 → 推进。
- `ES/Automation/ABCD/ESABCDLearningReview.psm1`：验证后的学习候选进入人审/独立审查，固定 `promotionAllowed=false`。
- `ES/Automation/ABCD/ESABCDCertification.psm1`：按 DesignReview、RuntimeAcceptance、ReleaseAcceptance 分层，缺独立签名时只能 `conditional`。
- `ES/Automation/Contracts/es-abcd-learning-review-v1.schema.json`、`es-abcd-certification-assessment-v1.schema.json`：分别约束学习审查和认证资格评估。

这些实现把“发散—审计—迭代”从静态声明推进为可调用的事件编排切片，但仍不替代跨进程、Unity、Worker、宿主和发布 Runtime 证据。
