# command authoring responsibility static gate

Acceptance id: command-authoring-static. Profile: authoring.

Required specialized static cases:

- asset-or-contract-identity: replay the asset-or-contract-identity contract from source/configuration and record deterministic evidence.
- mutation-boundary: replay the mutation-boundary contract from source/configuration and record deterministic evidence.
- denied-expansion: replay the denied-expansion contract from source/configuration and record deterministic evidence.
- repeat-idempotency: replay the repeat-idempotency contract from source/configuration and record deterministic evidence.
- recovery-evidence: replay the recovery-evidence contract from source/configuration and record deterministic evidence.

Static assertions cover boundary, deterministic replay, evidence integrity, static contract structure, and source/configuration claims. Runtime process, Unity, timing, visual, and release behavior remain unproven until separately authorized.

