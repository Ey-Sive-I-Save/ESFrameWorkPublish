# Evidence Receipt Contract

Every execution receipt must include `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`. `status` must be `passed`, `blocked`, or `failed`; `evidenceLevel` must be `S0` through `S6`. Missing, stale, or contradictory receipts block acceptance and require a fresh AIBrain plan.
