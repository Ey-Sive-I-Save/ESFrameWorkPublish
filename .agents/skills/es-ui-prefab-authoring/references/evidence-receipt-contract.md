# Evidence receipt contract

Every UI authoring or StaticDeepReplay execution must produce a machine-readable receipt with:

- `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`;
- `status` limited to `passed`, `failed`, `blocked`, or `not-run`;
- source references bound to the screen specification, materializer contract, validator and authoritative UI assets;
- Runtime visual, DPI, interaction, performance and release claims listed separately as unproven unless independently evidenced.

Missing, stale, or contradictory receipts block portfolio acceptance and require a new plan.
