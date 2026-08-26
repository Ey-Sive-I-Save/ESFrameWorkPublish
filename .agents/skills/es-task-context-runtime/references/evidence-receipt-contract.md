# Evidence Receipt Contract

Every Skill execution receipt includes `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`. Strict validation also binds project-relative paths, source hashes, PlanHash, tool identity, capture time, and freshness. TaskContextRuntime Completion Receipts remain a separate platform contract and cannot be substituted by a Skill execution receipt.
