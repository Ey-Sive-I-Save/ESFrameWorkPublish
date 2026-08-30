---
name: es-codex-session-bootstrap
description: >-
  Manage ESFramework Codex session initialization, New/Resume/Fork/Focus/Close,
  read-only restore, formal handoff, multi-launch, responsibility routing,
  immutable launch-envelope acceptance, and context-first tab naming. Use for
  requests to open, hand off, restore, fork, focus, close, route, or repair a
  Codex window/session. Do not trigger for ordinary current-window work.
---

# ES Codex Session Bootstrap

## Non-negotiable boundaries

- Use the project copy of this Skill; global Skill directories are not authority.
- Read `AGENTS.md` first, then the referenced ES/AISpace and AIWarnings entries.
- User authorization, Git/history, Runtime, external windows, network, release,
  deletion, and audit writes remain separate permissions.
- Static evidence never proves Runtime or host-window behavior.
- Session transitions and handoff boundaries must remain deterministic and replayable; static evidence does not prove external-window or Runtime behavior.
- A launch envelope is a one-time acceptance gate. Consume only its immutable
  `handoffFiles.absolutePath`; never substitute `sourceAbsolutePath`.
- Context is highest authority for receiving duties and tab titles. Combine
  `taskPrompt`, `taskKey`, responsibility context, handoff summaries, and the
  private read-only packet before using a responsibility key as fallback.
  Operation words (`handoff`, `resume`, `fork`, `bootstrap`, and translations)
  are never receiving responsibilities.

## Workflow controls

- Check scope, authority, evidence freshness, and exact identity before any
  session operation.
- Keep New, Resume, Fork, Focus, Close, ReadOnlyRestore, and formal Handoff as
  separate modes; never infer one mode from arbitrary task metadata.
- Treat `terminalStarted`, `promptObserved`, and `contextAccepted` as separate
  evidence. Only the exact acceptance receipt proves context delivery.

## Workflow

Use the operation matrix in [session-bootstrap-workflow.md](session-bootstrap-workflow.md)
for the selected mode; do not load unrelated operation sections.

## Progressive disclosure routing

Read only the reference needed for the selected operation:

- Full operation matrix and launch examples: [session-bootstrap-workflow.md](session-bootstrap-workflow.md)
- Launch/recovery authority and immutable context: [session-bootstrap-contract.md](session-bootstrap-contract.md)
- Handoff boundaries and snapshots: [control-contract.md](control-contract.md)
- Trigger classification: [trigger-routing-cases.md](trigger-routing-cases.md)
- Closeout/evidence fields: [task-closeout-contract.md](task-closeout-contract.md), [evidence-receipt-contract.md](evidence-receipt-contract.md)
- Path and responsibility constraints: [path-boundary-contract.md](path-boundary-contract.md), [responsibility-contract.md](responsibility-contract.md)
- Governance-chain changes: run `scripts/Test-ESGovernanceChainContract.ps1`

Use `scripts/Invoke-ESCodexSession.ps1` as the natural-language dispatcher and
`scripts/Start-ESCodexSession.ps1 -Mode Validate -DryRun` before a first launch.
For formal handoff use `Complete-ESCodexHandoff.ps1`; for read-only historical
context use `New-ESCodexReadOnlyContext.ps1`, never Resume/Fork.

## Required disclosure

When this Skill is used, disclose it in the first progress update and final
response. Report only evidence actually produced, with `runtime-not-run` when
no external process/window/runtime was executed.

External process boundary: any session transition that launches a helper uses an exact executable allowlist (`powershell.exe` or the declared Codex host) and a one-time argument envelope; arbitrary executable paths, shell text, and inherited arguments are rejected. This declaration permits review of the bounded launch path, not proof that a Runtime or host process actually ran.

Plan files for bounded multi-launch may come only from the project root or the approved system Temp root; all other absolute paths and traversal forms are rejected before reading.

## Static acceptance

Run the Skill-local `scripts/Test-es-codex-session-bootstrap-StaticReplay.ps1`
and validate its receipt with `scripts/Test-ESSkillEvidence.ps1`. For Skill
changes also run the project Skill contract/catalog and UTF-8 validators.
