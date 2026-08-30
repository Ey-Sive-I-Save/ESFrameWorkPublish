# Step 01 — Intent analysis

AI analysis must extract objective, audience, primary action, scenarios, constraints, success signals and unknowns from the request. It must call `Invoke-ESWebPageStudioPreflight.ps1` and return `intent-review` with `status`, `detail`, and `sourceRefs`. Missing objective or primary action is `blocked`.

## Execution

Required reads: current request, project `AGENTS.md`, and `ES/AISpace/README.md` for placement constraints.

## Return

Return `intent-review` with normalized fields, decision, input hash, and explicit unknowns/non-claims.
