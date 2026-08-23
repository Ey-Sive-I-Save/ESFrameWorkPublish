---
name: es-publish-aitest-prompt
description: Quickly publish a one-time prioritized message to the running ESFramework ESAITest AI through the external prompt inbox. Use when the user says “你快告诉测试AI……”, “告诉测试 AI……”, “给测试AI发消息/提示”, asks to immediately notify the testing AI, or invokes $es-publish-aitest-prompt. Do not trigger merely to explain Publish or edit its implementation.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Publish an ESAITest AI prompt

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.
- External side-effect contract: the prompt inbox is outside the project root and is an explicitly authorized external-run target. The authorization must bind the current TaskContract, AIBrain PlanHash, target `PersistentDataPath`, TTL, wait budget and stop condition. A queued envelope is not proof of consumption.

## Workflow controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Execute only through AIBrain planTask and the matching AICommand; direct execution is denied.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Extract the message after the trigger phrase and send it through `scripts/Send-ESAITestPrompt.ps1`.

## Workflow

1. Require a non-empty message.
2. Use an explicit P0–P4 from the user when present.
3. Otherwise use P1 for words such as “快”, “立即”, “马上” or “紧急”; use P2 for an ordinary notification.
4. Run from the project root:

   ```powershell
     & '.agents\skills\es-publish-aitest-prompt\scripts\Send-ESAITestPrompt.ps1' `
     -Message '<message>' -Priority P1 -Source 'codex-chat' -WaitForPickupSeconds 2 `
     -AuthorizationPath 'ES/Output/ExternalRunAuthorizations/<authorization>.json'
   ```

5. Report the returned `promptId`, priority and status.

## Status meaning

- `picked_up`: the running ESAITest runtime moved the envelope into its prompt queue. This does not prove the AI has consumed it; `attention.snapshot` supplies consumption evidence.
- `queued`: the envelope was written atomically, but no active runtime picked it up during the short wait. It remains available until its TTL expires.

Do not modify a plan, create another Runner, write Git/history/audit state, or claim delivery beyond the returned evidence.

## External-run static acceptance

- Verify normal enqueue, empty/oversized message rejection, invalid priority rejection and path-boundary rejection.
- Verify TTL and wait-budget bounds, atomic temporary-to-envelope replacement, duplicate prompt identity and interruption cleanup.
- Keep `queued`, `picked_up`, `consumed` and `expired` distinct; missing runtime pickup evidence remains `runtime-not-run`.
- The external prompt contract is documented in `references/external-prompt-execution-contract.md` and must be read before changing the sender.
