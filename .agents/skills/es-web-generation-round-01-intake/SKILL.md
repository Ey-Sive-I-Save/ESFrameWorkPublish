---
name: es-web-generation-round-01-intake
description: Capture and freeze the raw WebPageStudio requirement, authorization boundary, unknowns, and acceptance entry before FocusContext, TaskContext, Knowledge, design, or generation. Use when starting a new WebPageStudio generation round or recovering from a rejected prior round.
---

# ES Web Generation Round 01 — Requirement Intake

## Purpose

Round 01 preserves the user's request and requires a real AI interpretation before it can be accepted. The AI must state the objective, goals, non-goals, interaction intent and acceptance signals; scripts only hash and persist that evidence. It does not select Knowledge, create FocusContext/TaskContext, invoke SubAgents, run ABCD, or generate artifacts.

## SmallTool controls

- Read and write only the explicit project-relative receipt path.
- Reject missing input, path escape, empty prompt, and scope expansion.
- Repeat the same input idempotently; never start downstream work.

## Required reads

Read project `AGENTS.md`, `ES/AISpace/README.md`, and [`references/round-01-intake-contract.json`](references/round-01-intake-contract.json). Do not load later-round references.

## Workflow

1. Preserve the exact user prompt and compute its strict UTF-8 SHA-256.
2. The current AI must provide an interpretation artifact with objectiveBrief, userGoals, nonGoals, acceptanceSignals and interactionIntent; missing evidence leaves the receipt in `review` and blocks Round 02.
3. Write one `RequirementIntakeReceipt` using [`scripts/Invoke-ESRound01Intake.ps1`](scripts/Invoke-ESRound01Intake.ps1).
4. Return `accepted` only when the interpretation is present and complete. Missing interpretation returns `review`/`blocked.round-01.ai-interpretation-required`, never synthetic acceptance.
5. Stop. Round 02 may begin only after reading this receipt; no automatic chaining is allowed.

## Return contract

The receipt must contain `roundId`, `stageId`, `inputHash`, `rawPrompt`, `allowedScope`, `forbiddenScope`, `unknowns`, `acceptanceSignals`, `aiAnalysis`, `execution`, `decision`, `returnReceipt`, and `nonClaims`. The receipt is evidence of intake only, not evidence that the requirement is understood or accepted by the user.

## Expected use in the complete workflow

Round 01 prevents later AI stages from silently filling gaps or substituting a template. Round 02 freezes FocusContext from this receipt; Round 03 locks intent and creates TaskContext; later rounds consume the resulting hashes. If Round 01 is rejected, the workflow stops at intake and asks for a corrected requirement.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予 Runtime、网络、Unity、Git、删除或发布权限。
