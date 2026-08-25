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

AIBrain 的职责是定向发现、建立计划、验证边界、签发绑定 Invocation 的限时限次执行授权和把匹配证据传给 Facade。用户当前明确指令才是项目动作授权；AIBrain 授权只证明受管通道输入闭合。AIBrain 不是 Skill 执行器、ProcessRunner、Assets 写入器或新的 P0 权威。

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
- `allowDirectExecution` 对没有当前用户指令的 AIBrain/Skill 自主路由必须为 `false`；该字段不限制 current-user-direct 工作
- `authorityClass`: `standard | core-governed | project-gate`。它是路由/门禁权威，不是写权限。
- `core-governed` 与 `project-gate` 必须 `requiresBrainPlan=true`；`project-gate` 至少需要 S2 静态治理证据。
- Tier 与 authorityClass 独立：SmallTool 可以是 P0 级 `project-gate`，Engineering 也不能因此获得额外权限。

旧 Skill 暂无 `governance.json` 时仍可被发现，但只能按 legacy metadata 处理，不能被描述为完成了商业级治理。治理级 Skill 必须提供该文件。

## Plan and execution rules

1. `planTask` 必须携带明确 objective、routeKeys、AICommand、TaskContract 和 invocation identity。
2. 计划必须绑定 `SKILL.md`、`openai.yaml`、`governance.json`（若存在）的哈希及 Knowledge/AIWarnings/Command 证据。
3. 修改任一绑定文件后，旧 PlanHash 视为 stale，必须重新规划。
4. `runTask` 只能消费与 Invocation 和输入完全匹配的限时限次执行授权。授权有效期为 15 分钟；只有受信进程内宿主用不可序列化 proof 绑定 Host、Actor、Invocation、完整请求哈希和当前用户指令 SHA-256，L1 本地计划才可最多使用 20 次。L1/L2 `candidate-only` 最多 5 次，L3 或其他计划 1 次。当前文件 Bridge 只绑定 `ManagedAIBrain`，没有已登记的 `CurrentUserDirect` 生产宿主。外部 JSON 不得自报 `userDirected`。
5. Policy v5 / Store schema 3 必须用永久锁文件串行化跨进程读改写，并通过受管原子替换提交。PlanHash 与 InvocationId 双唯一；`Active`、`Exhausted`、`Expired` 是不可压平的状态，终态不得重签。Policy v4/schema 2 消费 stale，成功迁移后旧 InvocationId 必须持久退役。重复身份、非法次数/幂等键、未来终态时间、超长 TTL、未知代际或损坏 Store 必须 fail-closed，且不得覆盖损坏现场。
6. Facade 只有在 Endpoint、TaskContract、Capability、Snapshot、Path、AI 和 PlayMode 等确定性预检全部通过后，才可原子消费授权并立即派发；预检失败不得烧掉次数。
7. AIBrain 不得直接启动 ProcessRunner、写 `Assets/`、绕过 ESAutomationFacade 或把 Knowledge 摘要当作执行合同。
8. Skill 的确认结果只表示用户选择。当前用户指令提供动作授权；AICommand 与 TaskContract 只约束本次 AIBrain/Facade 执行协议。

## Two-phase execution gate

External AI `runTask` requests must include the immutable `approvedPlanHash` returned by `planTask`. The coordinator rebuilds the current plan only to compare hashes; a mismatch, missing hash, `NeedsReview` Skill, or non-`authorized-only` runtime eligibility is rejected before authorization. Capability drift is a read-only signal and requires route-scoped comparison plus re-planning.

## Current-user direct lane

The AIBrain two-phase gate is mandatory only when `runTask` is the selected transport. It is not a project-wide approval gate. A current explicit user request directly authorizes its bounded project action across control-plane, source, Assets, settings and generated/report paths. `UserDirectedLowRisk` is a compatibility name for the declared-scope validator; it has no path denylist and does not require `NoMatchingCommand` or PlanHash.

Delete, rename, Git, Runtime, external-process, network, release and credential actions must be named by the current user. Once named, no second project approval is required. If the selected AIBrain/Facade endpoint technically requires a plan, AICommand or TaskContract, satisfy that protocol or report that channel unavailable; do not reinterpret channel failure as lack of user authorization.

## Returned evidence

计划摘要应至少暴露 Skill 名称、Tier、Maturity、Delivery、EvidenceLevel、RiskClass 和 governance hash，供 UI、RunRecord 和后续审查使用。当前静态接入不等于 Unity、Worker、Profiler、Player、IL2CPP 或发布通过。

## Failure policy

治理元数据损坏、状态值非法、哈希变化、路由缺失、命令不匹配、TaskContract 不可用或证据不足时，AIBrain 必须阻断该受管计划并报告具体原因。不得自动选择另一个 Skill、Command 或外部缓存；也不得把该通道失败扩写成 direct-user lane 未授权。
