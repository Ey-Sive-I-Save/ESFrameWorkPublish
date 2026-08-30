---
name: es-web-generation-round-02-focus
description: Convert an accepted Round 01 requirement intake into a confirmed ES TaskFocusContext with one frozen work focus, allowed scope, forbidden expansion, required reads, and acceptance signals. Use before TaskContext creation or Knowledge routing in a WebPageStudio generation round.
---

# ES Web Generation Round 02 — FocusContext

## Purpose

Round 02 turns the immutable Round 01 intake into a single bounded focus. It resolves what this round is allowed to pursue and what it must not touch. It does not create TaskContext, route Knowledge, invoke SubAgents, run ABCD, or generate pages.

## SmallTool controls

- Read only the accepted Round 01 receipt and the explicitly listed TaskFocus contract/module.
- Write only the explicit Round 02 receipt path; reject missing intake, invalid hash, ambiguous focus, empty scope/signals, and revision conflicts.
- Require an explicit confirm/reject decision; never auto-chain into TaskContext or downstream generation.

## Required reads

Read the Round 01 `RequirementIntakeReceipt`, project `AGENTS.md`, `ES/AISpace/README.md`, and [`references/round-02-focus-contract.json`](references/round-02-focus-contract.json). Read the platform TaskFocus README before invoking the module.

## Workflow

1. Verify the intake receipt is `accepted` and its input hash is present.
2. Derive exactly one focus statement from the intake; do not invent a business objective.
3. Define allowed scope, forbidden expansion, required reads, and observable acceptance signals.
4. Create a proposal with `New-TaskFocusProposal`, confirm it with the expected revision, and emit the confirmed projection using [`scripts/Invoke-ESRound02Focus.ps1`](scripts/Invoke-ESRound02Focus.ps1).
5. Stop. Round 03 may start only after reading the confirmed FocusContext receipt. TaskContext creation is forbidden in this round.

## Hard controls

- One focus per GoalRevision; conflicting proposals return `ambiguous`.
- Focus revision, proposal hash, scope hash, required reads, and acceptance signals are immutable after confirmation.
- Scope expansion, missing intake, stale revision, or implicit confirmation returns a bounded block.
- The FocusContext runtime adapter may be used later to build a TaskContext request, but this Skill must not invoke TaskContext.

## Return contract

Return `FocusContextReceipt` with `focusContextId`, `focusRevision`, `proposalHash`, `focusScopeHash`, `allowedScope`, `forbiddenExpansion`, `requiredReads`, `acceptanceSignals`, `aiAnalysis`, `execution`, `decision`, `returnReceipt`, and `nonClaims`.

## Expected use in later rounds

Round 03 consumes this receipt to freeze TaskContext creation inputs. Knowledge, SubAgent, ABCD and page-generation rounds must reuse the same focus scope. A new focus is allowed only after an explicit GoalRevision or platform Reopen.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予网络、Unity、Git、删除或发布权限。
