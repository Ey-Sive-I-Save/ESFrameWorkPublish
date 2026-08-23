# start estest responsibility static gate

Acceptance id: start-estest-static. Profile: testing.

Required specialized static cases:

- fixture-identity: replay the fixture-identity contract from source/configuration and record deterministic evidence.
- invalid-input: replay the invalid-input contract from source/configuration and record deterministic evidence.
- repeat-idempotency: replay the repeat-idempotency contract from source/configuration and record deterministic evidence.
- interruption-recovery: replay the interruption-recovery contract from source/configuration and record deterministic evidence.
- evidence-freshness: replay the evidence-freshness contract from source/configuration and record deterministic evidence.

Static assertions cover boundary, deterministic replay, evidence integrity, static contract structure, and source/configuration claims. Runtime process, Unity, timing, visual, and release behavior remain unproven until separately authorized.

