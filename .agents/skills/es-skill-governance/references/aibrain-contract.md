# AIBrain integration contract

状态：AIBrain 静态治理元数据接入实施中；执行闭环仍需 Unity/受管 Worker 验收。

## Current authority chain

```text
BrainContext
  -> AIWarnings P0 / required reads
  -> AIKnowledge routeKeys
  -> relatedSkills
  -> SKILL.md + agents/openai.yaml + optional governance.json
  -> AICommand contract
  -> Automation TaskContract
  -> ESAutomationFacade
  -> RunRecord / evidence
```

AIBrain 的职责是定向发现、建立只读计划、验证边界、签发一次性授权和把匹配证据传给 Facade。它不是 Skill 执行器、ProcessRunner、Assets 写入器或新的 P0 权威。

## Governed Skill metadata

`governance.json` 是可选兼容文件；存在时必须是严格 UTF-8、合法 JSON，并包含：

```text
schemaVersion, skillName, tier, maturity, delivery,
evidenceLevel, riskClass, executionMode,
requiresBrainPlan, allowDirectExecution, writePolicy,
authorityClass, owner, acceptanceOwner, routeKeys,
requiredCases, controlRefs
```

允许值由 `es-skill-governance` 固定：

- `tier`: `SmallTool | Workflow | Engineering`
- `maturity`: `Proposed | Scaffolded | Implementing | Integrating | Verifying | Stable | Deprecated | Archived`
- `delivery`: `Designed | Implemented-Unverified | Blocked | Failed | Accepted | Released`
- `evidenceLevel`: `S0` 到 `S6`
- `allowDirectExecution` 对 AIBrain 路由的正式 Skill 必须为 `false`
- `authorityClass`: `standard | core-governed | project-gate`。它是路由/门禁权威，不是写权限。
- `core-governed` 与 `project-gate` 必须 `requiresBrainPlan=true`；`project-gate` 至少需要 S2 静态治理证据。
- Tier 与 authorityClass 独立：SmallTool 可以是 P0 级 `project-gate`，Engineering 也不能因此获得额外权限。

旧 Skill 暂无 `governance.json` 时仍可被发现，但只能按 legacy metadata 处理，不能被描述为完成了商业级治理。治理级 Skill 必须提供该文件。

## Plan and execution rules

1. `planTask` 必须携带明确 objective、routeKeys、AICommand、TaskContract 和 invocation identity。
2. 计划必须绑定 `SKILL.md`、`openai.yaml`、`governance.json`（若存在）的哈希及 Knowledge/AIWarnings/Command 证据。
3. 修改任一绑定文件后，旧 PlanHash 视为 stale，必须重新规划。
4. `runTask` 只能消费与 Invocation 完全匹配的一次性计划授权；重复、过期、篡改或缺失 PlanHash 必须拒绝。
5. AIBrain 不得直接启动 ProcessRunner、写 `Assets/`、绕过 ESAutomationFacade 或把 Knowledge 摘要当作执行合同。
6. Skill 的确认结果只表示用户选择，不替代 AICommand、TaskContract 或业务层授权。

## Returned evidence

计划摘要应至少暴露 Skill 名称、Tier、Maturity、Delivery、EvidenceLevel、RiskClass 和 governance hash，供 UI、RunRecord 和后续审查使用。当前静态接入不等于 Unity、Worker、Profiler、Player、IL2CPP 或发布通过。

## Failure policy

治理元数据损坏、状态值非法、哈希变化、路由缺失、命令不匹配、TaskContract 不可用或证据不足时，AIBrain 必须阻断并报告具体原因。不得自动选择另一个 Skill、Command 或外部缓存。
