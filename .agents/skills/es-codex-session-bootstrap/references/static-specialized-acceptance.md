# codex session bootstrap responsibility static gate

Acceptance id: codex-session-bootstrap-static. Profile: session.

Required specialized static cases:

- session-identity: replay the session-identity contract from source/configuration and record deterministic evidence.
- handoff-boundary: replay the handoff-boundary contract from source/configuration and record deterministic evidence.
- denied-expansion: replay the denied-expansion contract from source/configuration and record deterministic evidence.
- interruption-recovery: replay the interruption-recovery contract from source/configuration and record deterministic evidence.
- evidence-freshness: replay the evidence-freshness contract from source/configuration and record deterministic evidence.

Static assertions cover boundary, deterministic replay, evidence integrity, static contract structure, and source/configuration claims. Runtime process, Unity, timing, visual, and release behavior remain unproven until separately authorized.

