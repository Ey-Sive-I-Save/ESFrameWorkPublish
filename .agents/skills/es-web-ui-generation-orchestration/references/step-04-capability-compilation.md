# Step 04 — Capability compilation

## AI analysis

Map observed mechanisms to local strategies: render policy, component boundaries, route/data contracts, interaction state machines, progressive enhancement, resumability budgets, and performance budgets. Explain why each strategy serves the request.

## Execution

Run `scripts/Invoke-ESWebOpenSourceCapabilityCompiler.ps1` against the pinned manifest/profile. The compiler emits deterministic strategy IDs and hashes.

## Return

Return `compiledCapabilities` with `status`, `strategies`, `sourceEvidence`, `analysis`, `execution`, `strategyHashes`, and `nonClaims`. A profile label without a compiled strategy is rejected as `blocked.capability.not-materialized`.

