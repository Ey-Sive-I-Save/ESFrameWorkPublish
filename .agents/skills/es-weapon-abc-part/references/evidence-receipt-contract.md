# Weapon ABCP evidence receipt contract

Every Weapon ABC Part run must emit a machine-readable receipt containing `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`.

`status` is one of `passed`, `failed`, `blocked`, or `not-run`. `sourceRefs` identify authoritative files or command output; prose and chat are not evidence. Missing, stale, or contradictory receipts limit only the evidence claim they support. Runtime and release claims require their own explicit authorization and fresh receipts.
