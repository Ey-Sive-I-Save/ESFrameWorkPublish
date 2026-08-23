# Skill Discovery and Eligibility Contract

状态：现行组织架构合同。

## 目的

Skill 的存在、可发现、可规划和可运行是四个不同事实。Catalog 是导航投影，`governance.json` 是 Skill 自身治理元数据，统一策略位于 `.agents/SKILL_DISCOVERY_POLICY.json`。任何一层通过都不能替代其他层。

```text
存在 -> discoveryState -> planEligibility -> runtimeEligibility
```

## 生命周期语义

| maturity/delivery | discoveryState | planEligibility | runtimeEligibility |
| --- | --- | --- | --- |
| Proposed / Scaffolded / Designed | candidate | advisory-only | blocked |
| Implementing / Integrating / Verifying | operational-candidate | plan-authorized | not-proven |
| Stable / Accepted / Released | operational | plan-authorized | authorized-only |
| Deprecated | deprecated | none | blocked |
| Archived / Blocked / Failed | hidden or blocked | none | blocked |

`NeedsReview` 可以保留在 CapabilityIndex 中供治理和增量发现使用，但不得被解释为已验收或已发布。

## 路由选择

1. `Operational` 只选择 `operational` 和 `operational-candidate`。
2. `CapabilityIndex` 可以列出候选能力，但只返回元数据和状态，不自动读取正文。
3. `Audit` 才允许检查全部生命周期状态。
4. RouteKeys 为空时不得隐式读取整个 Skill Portfolio；调用方必须重新规划并提供任务路由。
5. 通用 RouteKey 只能作为辅助信号；至少一个领域 RouteKey 命中后才能选择正文。

## 运行权限

`discoveryState` 永远不授予权限。运行仍必须经过 AIBrain `planTask`、匹配的 AICommand、TaskContract、当前授权和运行证据。`not-proven` 只能表示静态可规划，不得声称运行可用。

## 变更与恢复

策略文件、Skill、治理、Catalog、Resource Index、Knowledge 或命令合同任一哈希变化，均使绑定的 PlanHash stale。增量刷新只返回受当前 RouteKeys 选择的变化项；无范围时返回 `replan`，不得回退到全量正文读取。
