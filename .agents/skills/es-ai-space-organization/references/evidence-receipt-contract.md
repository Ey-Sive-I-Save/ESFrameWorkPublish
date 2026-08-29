# Evidence receipt contract

Every execution must produce a machine-readable receipt with:
- `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`.
- `status` must be one of `passed`, `failed`, `blocked`, or `not-run`.
- `sourceRefs` must identify authoritative files or command output; summaries and chat text are not evidence.
- Missing, stale, or contradictory receipts block only the evidence claim they support.
