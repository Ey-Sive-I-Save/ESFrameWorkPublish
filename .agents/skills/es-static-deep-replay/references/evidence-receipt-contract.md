# Evidence receipt contract

Every StaticDeepReplay execution must produce a machine-readable receipt with:

- `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`;
- `status` limited to `passed`, `failed`, `blocked`, or `not-run`;
- `sourceRefs` bound to the replay manifest, adapter, and authoritative Skill files;
- Runtime claims listed separately under `claimsNotProven`.

Missing, stale, or contradictory receipts block the portfolio gate. A static receipt proves only the declared static scope and never implies Unity, Runtime, visual, performance, or release acceptance.
