---
name: es-adversarial-review
description: Perform adversarial validation for ESFramework feasibility after solution design, quality validation after writing or modifying code, code review, and explicit robustness, risk, loophole, second-opinion, or critical-review requests. Load only when the current request or completed work stage matches this validation purpose. Do not load unconditionally for simple chat, trivial non-code execution, or formatting-only work.
---

# Run an Adversarial Review

Challenge whether the work achieves its stated intent. The review pass is read-only: it produces findings and evidence gaps and never silently changes the target. When it runs after an authorized implementation in the same task, accepted findings may be fixed under that original authorization, then revalidated before delivery.

## Conditional Routing

- Treat this as the ES `adversarial-review` route.
- Route completed solution design here for feasibility validation, and route written or modified code here for quality validation before reporting it as complete.
- Route direct requests for code review, robustness, risk, loopholes, a second opinion, or a critical challenge here.
- When both analysis and validation match, use `$es-first-principles-analysis` for generation first, then this Skill for validation second.
- Do not emit a full Skill report merely to prove activation. Give a full review when requested or when material findings, release risk, or evidence gaps justify it; otherwise integrate the validation concisely into the normal delivery.

## Required Context

1. Read `AIWarnings/00_开始阅读（Start）/README.md`, `当前状态（CurrentStatus）.md`, and `规则索引（RuleIndex）.md`.
2. Read only P0 and domain rules relevant to the review target.
3. Run the worktree audit when reviewing a dirty tree, a diff, or files shared by multiple responsibilities.
4. Identify the exact scope, intended outcome, authority boundaries, and available evidence before judging it.

## Review Lenses

Apply the needed lenses independently, then reconcile them.

| Lens | Challenge |
|---|---|
| Architecture | Responsibility, authority, lifecycle, coupling, scale, ordering, and future change boundaries. |
| Correctness | Inputs, states, error paths, cancellation, concurrency, data loss, security, and unproven behavior. |
| Minimality | Unnecessary abstraction, duplicate paths, configuration without a second use case, and avoidable user friction. |

Use all three for substantial architecture or high-risk changes. Use only the relevant lenses for narrow reviews. Do not manufacture findings to fill a lens.

## Protocol

1. State the author intent in a sentence. Review fitness for that intent, not a different goal invented by the reviewer.
2. Inspect source and evidence directly. Cite file and line for source findings; identify the exact missing evidence for verification gaps.
3. For each finding, provide severity, failure scenario, violated contract or P0 rule, and the smallest practical correction.
4. Separate source defects, design risks, and evidence gaps. Missing Unity/Player evidence is not a source defect.
5. Apply lead judgment after the challenge: accept, reject, or defer each material finding with a reason. Do not treat all adversarial claims as true.

## External Reviewer Policy

- Use an external or opposite-model reviewer only when it is available, the user authorizes it for this review, and its identity, prompt scope, and result can be reported honestly.
- Do not install, invoke, or claim Claude, DeepSeek, or another model merely because this Skill is active.
- If no independent reviewer is authorized and available, report `单模型多视角审查` rather than claiming adversarial-model independence.
- Internal subagents are not independent model evidence. They may assist with bounded evidence collection but must be labeled accordingly.

## Boundaries

- Do not edit source, Git, Unity, audit state, session history, or reports during the review pass. A finding may be fixed only under already valid task authorization, then the changed result must be reviewed again.
- Do not start, resume, fork, close, or message another Codex session without explicit current window intent.
- Do not promote an ordinary review into a formal module audit. `$es-module-lifecycle` requires the user's explicit audit intent.
- Do not claim Unity compilation, ReloadDomain, Test Runner, PlayMode, Player, IL2CPP, or release acceptance without the corresponding evidence.

## Delivery

Order findings by severity. Use this form:

```text
审查模式：独立模型 / 单模型多视角
意图
结论：通过 / 有条件通过 / 不通过
发现
  [严重度] 问题 - file:line
  场景：...
  依据：...
  建议：...
主审判断
未覆盖的证据
```

If no material issue is found, say so plainly and list remaining evidence gaps or residual risks.
