# Portfolio receipt contract

`case=portfolio-gate` is an aggregate receipt and is not a single Skill behavior receipt.

It must contain `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, `timestampUtc`,
`portfolioHash`, `catalogHash`, `resourceIndexHash`, `validatorHash`, and `sourceRefHashes`.
The aggregate hash binds the ordered child result summary. `status` remains `blocked` when any
child is failed, blocked, or not-run. This contract does not claim that every Skill has behavioral
evidence; child Skill receipts are validated separately by `Test-ESSkillEvidence.ps1`.
