---
name: es-first-principles-analysis
description: Perform first-principles analysis for ESFramework conceptual decomposition, root-cause reasoning, complex technical analysis, and concrete design-to-implementation work. Load when the current request or work stage needs facts, assumptions, constraints, mechanisms, and a ground-up design decision. Do not load unconditionally for simple chat, trivial non-code execution, formatting-only work, routine compilation fixes, or formal module audits.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Analyze From First Principles

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request directly authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected.

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Do not use this Skill for permission expansion. Implement only the user's bounded goal; file count, byte size, path class, UTF-8 and diff checks affect review and evidence, not authorization. Missing `planTask` blocks only an AIBrain transport that technically requires it.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Use this skill to expose the mechanisms behind an ESFramework decision before recommending a direction. It is an analysis workflow, not a writing style and not authorization to edit.

## Conditional Routing

- Treat this as the ES `first-principles` route.
- Use it first when a request needs conceptual decomposition, root-cause analysis, a complex decision, or design that will lead to implementation.
- When the same work also needs feasibility or quality validation, finish generation here and then route the completed design or code to `$es-adversarial-review`.
- Keep simple chat, trivial non-code execution, and formatting-only work on the normal path unless the user explicitly asks for first-principles analysis.

## Required Context

1. Read `AIWarnings/00_开始阅读（Start）/README.md`, `当前状态（CurrentStatus）.md`, and `规则索引（RuleIndex）.md`.
2. Read only the P0 and domain rules matched by the target decision.
3. Inspect current source, configuration, evidence, and worktree state as needed. Do not treat prior summaries as proof.

## Analysis Protocol

1. Restate the decision and desired outcome in one or two sentences.
2. Separate observations into: verified facts, unverified assumptions, inherited defaults, and hard constraints.
3. Reduce the problem to concrete mechanisms. For engineering, consider authority, data flow, state ownership, lifecycle, failure modes, latency, resource limits, concurrency, and verification.
4. Derive the smallest viable options from those mechanisms. For each option, state the preserved or removed assumption, tradeoff, and failure condition.
5. Prefer a reversible experiment or evidence-collecting step before an irreversible redesign.
6. Give verification metrics or pass/fail evidence for any non-trivial recommendation.

## Boundaries

- Do not present an assumption as a fact or use "本质" without explaining the mechanism.
- Do not reject an existing ES pattern merely because it is conventional; retain it when its original conditions still hold.
- Do not turn a small, reversible question into a long methodology lecture.
- Do not append a default coaching, training, score, or self-reflection section.
- Do not create or update audit state, session history, AIWarnings, source, Git, Unity state, or a new conversation unless the user separately authorizes it.
- This analysis does not trigger `$es-module-lifecycle` or turn an ordinary question into a formal audit.

## Delivery

Use concise headings only when they clarify the decision:

```text
结论
已验证事实 / 未验证前提
底层机制
可选方向与取舍
失败条件
最小验证
```

Omit empty sections. Clearly mark unavailable evidence and distinguish recommendation from verified behavior.
