# 六类机制复刻合同

## 目的

本文件是 `es-agent-mechanism-replication` 的静态语义补充，不复制外部仓库正文。外部材料在 Knowledge 条目中仅保留待绑定的 SourceRef；未绑定来源不能升级成 ES 事实、记忆或完成证据。

## 统一对象

| 对象 | 必填不变量 |
|---|---|
| GoalRevision | 目标、范围、非目标、输入快照和 revision 身份冻结 |
| RoutePlan | routeKey、KnowledgeId、requiredReads、RouteStage 顺序和权限边界固定 |
| RouteStage | `stageId` 唯一；声明 `requires`、`produces`、稳定失败码和可重放输入 |
| EvidenceSet | 每项 evidence 有来源、快照哈希、Verifier、状态和时间边界 |
| Receipt | 不可变、可寻址、绑定输入/输出哈希；不能由模型自报替代 |
| completionDecision | 仅说明目标合同是否满足；不等价于交付或 Runtime 通过 |
| deliveryAcceptance | 独立记录交付范围、外部验收和未证实项 |

## 机制检查表

1. **SWE-agent ACI**：工具调用必须是注册 RouteStage 的有界操作；非法路径、网络、宿主命令和写入都在授权门外。
2. **Reflexion**：反思只能成为候选 Evidence；必须经规则/Verifier 通过后再影响下一轮，不可直接改写 Knowledge 或完成状态。
3. **Tree of Thoughts**：分支复制只读快照，分支上下文隔离；OutcomeEvaluator 使用固定排序和可重放评分，失败分支不污染主分支。
4. **Petri**：Place/Transition 映射 TaskContext 合法状态迁移；CAS 防止重复消费，中断时保留可恢复 token/receipt。
5. **EnvTrustBench**：环境、工具版本、权限与观察证据显式进入 EvidenceSet；缺证据时保持 `review`/`blocked`，不采信自报可信度。
6. **AuditBench**：审计问题、命中规则、finding、证据、Receipt 与决定形成链；零 finding 是局部观察，不是项目总通过。

## ES 阶段闭环

`analysis`（requires `goal-revision`, produces `mechanism-design`）
→ `review`（requires `mechanism-design`, produces `mechanism-review`）
→ `knowledge-candidate`（requires `mechanism-review`, produces `knowledge-candidate`）
→ `knowledge-validation`（requires `knowledge-candidate`, produces `knowledge-validation`）。

对应的失败码至少覆盖：`mechanism-evidence-unbound`、`source-snapshot-stale`、`design-contract-invalid`、`branch-isolation-gap`、`evidence-boundary-overclaim`、`unreviewed-critical-risk`、`external-source-not-bound`、`route-collision`、`source-ref-unbound`、`content-hash-mismatch`、`route-top3-mismatch`、`source-hash-drift`。

## 关闭条件与恢复

闭环需要同时通过：目标冻结、路由/读取/快照一致、Verifier EvidenceSet、不可变 Receipt、权限 fail-closed、决定与交付分离、漂移失效重放。任何条件缺失时只报告对象级状态。中断恢复只能使用已经接受的不可变 Transcript/Context 和私有快照；不得用另一个 handoff 或 mutable sourceAbsolutePath 补齐证据。
