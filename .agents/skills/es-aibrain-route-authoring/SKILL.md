---
name: es-aibrain-route-authoring
description: Design and validate AIBrain routeKeys, Knowledge bindings, Skill selection, MCP capability projections, and plan/evidence gates for ESFramework.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES AIBrain Route Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

维护 AIBrain 的定向发现，不让宽泛 route 或摘要绕过权限与证据门禁。

## Workflow

1. 读取 `ESAIBrainCoordinator`、`ESAutomationAiBridge`、KnowledgeIndex、Resource Index 和目标 AIWarnings/AICommand。
2. 为每个 route 定义触发词、功能区、最小 Knowledge、relatedSkills、MCP 能力、证据和 non-claims。
3. 检查 route 冲突、缺失 Skill、缺 SourceRef、MCP 未连接、authorityClass 和 PlanHash 绑定。
4. 用 `listCapabilities`/`planTask` 的只读输入回放；旧索引漂移时阻断并重新规划。

## RoutePlan V1 boundary

- `RoutePlan` is a read-only, snapshot-bound projection attached to `ESAIBrainPlan`; it does not replace or execute the legacy production route.
- A composable plan binds one frozen `GoalRevision`, an ordinal exact `routeKeys` set, current Git HEAD, normalized SourceRefs, SourceRefs Hash, and the central Route Stage Registry Hash.
- Every stage resolves by exact Skill/Profile/frozen-routeKey membership and declares `requires`, `produces`, and failure conditions. Core depth is 0, the default extension limit is 1, and depth 2 requires a registered directional `depthReasonCode`. The PowerShell consumer and validator replay this relation through `ES/Automation/RoutePlan/ESRoutePlanContract.psm1`; fixtures may not copy the canonical hash field set.
- The real `planTask` source path now emits one read-only shadow candidate only for `profile=governance` and `scope=task-object`. Its decision ID is derived from the frozen route decision plus GoalRevision, stages, issues, and snapshot; a new snapshot must produce a new ID. The C# producer cannot self-certify match or no-bypass: the shared PowerShell consumer independently recomputes the ID and derives `matched/no-bypass/rollback-available`. Every other Profile returns `not-selected` without changing the legacy decision.
- `executionEnabled` is always `false`. Missing evidence caps only the RoutePlan claim; malformed dependencies block only that RoutePlan Profile and do not become project-global P0 or narrow `CurrentUserDirect` authority.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `aibrain-route-contract`
- Required cases: `route-key-overlap, skill-discovery, knowledge-binding, route-collision, stale-route-hash`
- Static assertions: AIBRAIN_ENTRY; routeKeys; KnowledgeIndex; collision; stale
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不直接执行任务、不写 Assets、不启动 ProcessRunner；路由是导航和门禁。
- 禁止用 `reserved` 路由宣称实现、API 或权限；保留兼容指针和旧条目。
- 覆盖正向、无匹配 route、过宽 route、缺 Skill、MCP 断开和重复 PlanHash。

## Resources

- `references/route-contract.md`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs`


## Specialized static acceptance

Acceptance ID: `aibrain-route-contract`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- AIBRAIN_ENTRY
- routeKeys
- KnowledgeIndex
- collision
- stale

Required specialized cases: `route-key-overlap, skill-discovery, knowledge-binding, route-collision, stale-route-hash`
Guidance: `references/static-specialized-acceptance.md`
