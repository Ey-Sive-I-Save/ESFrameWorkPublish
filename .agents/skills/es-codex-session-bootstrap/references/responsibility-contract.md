# Responsibility contract

The receiving window responsibility describes the durable content it owns. It
must not describe the transfer operation (`handoff`, `resume`, `fork`,
`bootstrap`, `startup`, or their Chinese equivalents).

`Complete-ESCodexHandoff.ps1` runs
`scripts/Get-ESCodexResponsibilityAssessment.ps1` against every formal `T###`
node in the confirmed archive. It selects the dominant content profile using
deterministic keyword scores. A supplied `ResponsibilityKey` must match the
recommendation. When the archive is ambiguous or has insufficient nodes, the
orchestrator refuses to guess and requires a narrower archive or an explicit
review of the responsibility before launching.

The assessment is a routing guard, not proof that the receiving window has
implemented or verified the historical work. The new window must re-read the
private snapshot and current source state after acceptance.
