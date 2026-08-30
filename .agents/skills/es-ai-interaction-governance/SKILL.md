---
name: es-ai-interaction-governance
description: "Evaluate user prompts and task closeout quality, classify objective clarity and unproven claims, and generate bounded next-step options from injectable behavior-tree rules. Use when a task needs prompt scoring, verification scoring, goal-drift detection, handoff summaries, or prioritized follow-up choices."
---

# ES AI Interaction Governance

## 项目推进分叉口超级语义

项目中央索引 `.agents/SUPER_SEMANTICS_REGISTRY.json` 还包含 `0分`/`零分`、`迭代`、`兼容` 三类推进分叉口语义。它们不是普通路由：命中后必须在第一条可见文本显示 `✨✅【已触发超级语义“XXX”】`，并先完成深度用户指导确认（目标、范围、禁止自由发挥项、验收信号），不得直接替用户扩大任务或修改方向。具体问题由 `.agents/references/project-progress-super-semantics.json` 提供。

每轮解析还必须执行文本采样：短文本全文扫描；超过中央索引 `textSampling.shortTextMaxChars` 的长文本只扫描开头和末尾，中间内容不参与超级语义触发，并在回执中记录扫描模式与字符计数。

当前交互超级语义由中央注册表统一发现（不得在本 Skill 另建触发词表）：`包装`（PromptAutoWrapToggle，当前职责内可处理；“帮我包装/包装给我/负责包装/让你包装”产生 `delegated-current-window`）、`给我看`（证据展示，命令行回退到绝对路径/文件夹）、`只读`（仅明确只读场景触发，必须一次确认，默认仍为 normal）、`聚焦/注意`（TaskFocusContext Proposal，确认前 pending），以及取消 Token `取消/C/不用/一般/快速`（仅取消当前活动超级语义并默认抑制三轮）。P0 和高危语义优先覆盖普通包装；同层多个高危语义必须进入澄清，不得静默择一。

Use this Skill for the interaction-control plane, not for business implementation. It owns prompt quality, objective clarity, verification sufficiency, uncertainty disclosure, goal drift, handoff summaries, and bounded next-step suggestions.

All persistent reports are restricted to `ES/Output/Interaction`. Transcript conversion may use only that report root or the operating system Temp directory for a short-lived intermediate file. Hook execution accepts only the built-in `Invoke-ESInteractionCloseout.ps1`; a caller-supplied script path cannot select another executable.

For semantic-risk work (lifecycle, user-visible state, defaults, automatic transitions, recovery, or a user correction), create and validate the versioned intent contract in `references/intent-contract.schema.json` before routing to a domain Skill. The contract freezes `mustPreserve`, `allowedTransitions`, `forbiddenTransitions`, acceptance signals and counterexamples. `aligned` is the only state that permits implementation; `partial`/`unverifiable` are analysis-only and `misaligned` is deny-and-revise. Validate it with `scripts/Test-ESIntentContract.ps1`.

For real-work evaluation, use `scripts/Convert-CodexTranscriptToEvidence.ps1` then `scripts/Invoke-ESInteractionEvidenceAssessment.ps1` first. This evaluates observable transcript/tool/diff/verification evidence; the numeric assessment is only a secondary projection. Run `scripts/Test-es-ai-interaction-governance-StaticReplay.ps1` for the Skill-level deterministic replay gate before Runtime escalation.

Use `scripts/Invoke-ESInteractionCloseout.ps1` to produce the final evidence-first closeout. For a fast bounded run, pass one real Codex JSONL with `-SessionPath`, or the current exact local session ID with `-SessionId` (which selects only the newest readable snapshot for that ID, and rejects a timestamp tie; pass explicit `-AllowWrites`/`-AllowRuntime` scope when applicable); otherwise pass normalized evidence with `-EvidenceInputPath`. Do not manually reconstruct its status, findings, or non-claims.

When a trusted host Stop Hook is explicitly configured, `scripts/Invoke-ESInteractionCloseoutHook.ps1` can provide the same observation from the Hook payload's absolute `transcript_path`. Hook payloads without explicit `allow_writes`/`allow_runtime` are reported as `missing-explicit-scope`, not treated as denied authorization. If the last assistant output has no evidence-first closeout marker, it requests one bounded continuation; `stop_hook_active` prevents recursion. Hook configuration is not Hook trust/load/delivery proof.

## Authority and boundary

- Read the project `AGENTS.md` and this Skill's `references/interaction-governance-contract.md` before assessment.
- A score is advisory evidence, never user authorization, acceptance, or a permission grant.
- Next-step rules may suggest or queue choices only; they never execute writes, Runtime, external processes, network, Git, or handoff actions.
- `nextSteps` are candidates. The current user must select or explicitly request a mutating/runtime action.
- After explicit selection, use `scripts/Invoke-ESContextCollection.ps1` only with caller-resolved project-relative paths. It creates a bounded read-only receipt and never infers missing routes or authorizes side effects.
- Consume a user's numeric reply with `scripts/Resolve-ESNextStepSelection.ps1`; it maps the current menu number to the stable `id` and never dispatches the action itself.
- Use `scripts/Invoke-ESNextStepDispatch.ps1` for the bounded dispatch boundary: `1` requests clarification, `2` invokes the explicitly supplied bounded collection inputs, and `3` requests static validation without guessing a target.
- At task start, evaluate whether bounded context collection is recommended. Offer Skill, Knowledge, and/or AIWarnings collection only when routing is ambiguous, project facts are unfamiliar/stale, or the task is high-risk. Never collect the whole repository or all AIWarnings by default; use the tree's limits and options, wait for user choice, and record the selected read set and source hashes.
- Do not infer a completed objective from a high score. Preserve `claimsNotProven` and `runtimeStatus` separately.
- Classify the work phase first; for feature/editor development, prefer root-cause analysis over premature compatibility or fallback. See the contract for exceptions.

## P0 feedback and execution triggers

Treat these as three independent P0 triggers; any one is sufficient:

- a user reports a score of `0`;
- a user explicitly requests verification;
- a user explicitly requests that a Skill be run.

After any trigger, stop ordinary closeout behavior. Re-check intent alignment, observable execution, Skill routing, verification evidence, and claims not proven. An explicit verification request requires the bounded validator to run; an explicit Skill request requires the routed project Skill to be read and executed. Do not replace either action with a plan, explanation, or self-authored claim. A zero score raises scrutiny and evidence requirements but does not grant write, Runtime, host, network, Git, deletion, or release authority.

## Workflow

1. Determine the active profile from `references/evaluation-profiles.json`; default to `default`.
2. Assess the prompt for objective, target, constraints, acceptance signal, and scope. Return `promptScore` and `objectiveClarity`.
3. Assess whether the current assistant interpretation matches the user's intent, separately record prior misalignment, then assess claim-to-evidence correspondence. Return `intentAlignmentScore`, `verificationScore`, `evidenceQualityScore`, `calibrationScore`, `confidenceScore`, `overallScore`, `diagnosticReasons`, `runtimeStatus`, and `claimsNotProven`; never convert missing evidence into failure without a profile rule.
4. Compare the current objective with the prior task snapshot when supplied. Mark `goalDrift` as `none`, `possible`, or `confirmed`.
5. Load `references/next-step-behavior-tree.json` and select at most three highest-priority rules whose conditions are met. Preserve `requiresUserChoice` and `risk` on every suggestion.
6. Emit the compact Chinese closeout fields required by `AGENTS.md`. Use the script for deterministic scoring and rule selection; scores remain machine-readable evidence and are not rendered as the user-summary headline. Render two independent observed role/status lines: `🐋 DeepSeek Harness：...` and `🧰 Codex Harness：...`; each role without invocation evidence must be `未调用`, never implying integration or execution. If `overallScore < 4`, show the script's `🚨低评分风险` notice and do not claim completion or verification. If no assessment ran, keep scores unavailable in machine output.

User-facing `使用Skill：` disclosure must contain stable Skill names only. Never
render relative paths, `SKILL.md`, `references/`, or `scripts/` as a Skill name;
those paths belong exclusively to internal evidence and read manifests.

## Workflow controls

- Keep assessment read-only and bounded to the supplied prompt, evidence, profile, and optional prior objective.
- Never execute a selected next step automatically; every candidate remains `requiresUserChoice`.
- On malformed profile or behavior-tree input, stop with a static contract error.
- Do not infer AI work quality from self-authored evidence fields when observable events are available; report `unverifiable` when the event set is insufficient.

## Static acceptance

Run `scripts/Test-ESInteractionGovernance.ps1` after changing profiles, behavior-tree rules, or the assessment script. It checks schema shape, unique IDs, priority ordering, bounded outputs, no-execute rules, and Chinese closeout labels. Runtime/UI behavior is outside this Skill's claim.

## Resources

- `references/interaction-governance-contract.md`: output contract and scoring boundaries.
- `references/evaluation-profiles.json`: replaceable evaluation profiles.
- `references/next-step-behavior-tree.json`: injectable, priority-ordered next-step rules.
- `references/intent-contract.schema.json`: task-scoped user-intent contract schema.
- `references/static-replay-adapter.md` and `references/static-specialized-acceptance.md`: StaticDeepReplay adapter and responsibility-specific cases.
- `scripts/Invoke-ESInteractionAssessment.ps1`: deterministic assessment and suggestion selection.
- `scripts/Test-ESInteractionGovernance.ps1`: static contract validator.
- `scripts/Test-ESIntentContract.ps1`: deterministic intent-contract validator.
- `scripts/Test-es-ai-interaction-governance-StaticReplay.ps1`: StaticDeepReplay entry point.
