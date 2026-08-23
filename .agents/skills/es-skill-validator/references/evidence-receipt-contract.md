# Evidence receipt contract

Validation evidence must identify `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, `timestampUtc`, `skillHash`, `governanceHash`, `validatorHash`, `planHash`, and `sourceRefHashes`. `receiptPath` must point to the current project-relative receipt; every `sourceRef` must exist and have the matching hash in `sourceRefHashes`. `planHash` binds the receipt to the one-time AIBrain plan for governed Skills. A receipt is evidence of the validation run, not permission to execute the validated Skill. Missing or stale bindings are `blocked`, never silently legacy-accepted.

The `case=portfolio-gate` aggregate contract is separate and is validated by `Test-ESSkillPortfolioEvidence.ps1`; it must not be passed to the single-Skill validator.
