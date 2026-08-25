# es-codex-session-bootstrap control contract

- Verify scope, authority, and source evidence before changing project state.
- Apply the central user-directed action authority: a current explicit user request authorizes its bounded action; only inferred expansion is denied. Action-specific side effects must be named, and AIBrain/AICommand inputs apply only when their managed channel is selected.
- Record positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery results.
- Stop on missing evidence, stale hashes, encoding failures, or ownership ambiguity.
- Handoff intent must route through `ES/AI协作历程（Codex）/Tools/Complete-ESCodexHandoff.ps1`; direct `Start-ESCodexSession.ps1 -Mode New` calls carrying handoff intent or any `-HandoffPath` are rejected. `-HandoffMode` additionally requires the orchestrator's short-lived authorization capability, so a manually supplied switch cannot bypass timeline coverage, private snapshots, or receipts.
- A handoff responsibility key and tab title must identify the receiving content responsibility, not the operation (`handoff`, `resume`, `fork`, `bootstrap`, `交接`, `恢复`, `分叉`, or `启动`).
