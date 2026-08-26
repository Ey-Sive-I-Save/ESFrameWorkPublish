# Portfolio receipt contract

`case=portfolio-gate` is an aggregate receipt and is not a single Skill behavior receipt.

It must contain `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, `timestampUtc`,
`portfolioHash`, `catalogHash`, `resourceIndexHash`, `validatorHash`, `sourceRefHashes`,
`decisionStatus`, `effect`, `staticStatus`, `evidenceStatus`, `runtimeStatus`, and `blockingLayer`.
The aggregate hash binds the canonical child-result summary and the exact inner report hash.

- `blocked` is reserved for a missing inner result or a scoped child `failed`, `blocked`, or `not-run` result.
- `review` means no hard child failure exists, but evidence or another review-only condition limits claims.
- `passed` means the selected static Portfolio surface has neither a hard failure nor a review-only finding.

`runtime-not-run` and `evidence-pending` do not become project-global hard blocks. This contract does
not claim that every Skill has behavioral evidence; child Skill receipts are validated separately by
`Test-ESSkillEvidence.ps1`.
