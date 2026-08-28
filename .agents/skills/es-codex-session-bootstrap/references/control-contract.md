# es-codex-session-bootstrap control contract

- Verify scope, authority, and source evidence before changing project state.
- Apply the central user-directed action authority: a current explicit user request authorizes its bounded action; only inferred expansion is denied. Action-specific side effects must be named, and AIBrain/AICommand inputs apply only when their managed channel is selected.
- Record positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery results.
- Stop on missing evidence, stale hashes, encoding failures, or ownership ambiguity.
- Natural-language requests must enter through `.agents/skills/es-codex-session-bootstrap/scripts/Invoke-ESCodexSession.ps1`, which resolves `New`, `Resume`, or `Handoff` before dispatch. Formal handoff delivery then routes through `Complete-ESCodexHandoff.ps1`. A normal `Start-ESCodexSession.ps1 -Mode New` is always a temporary independent task window and requires no archive or SessionId. Supplying `-HandoffPath` still requires orchestrator-only `-HandoffMode` and its short-lived authorization, so direct archive delivery cannot bypass timeline coverage, private snapshots, or receipts.
- A handoff responsibility key and tab title must identify the receiving content responsibility, not the operation (`handoff`, `resume`, `fork`, `bootstrap`, `交接`, `恢复`, `分叉`, or `启动`).
