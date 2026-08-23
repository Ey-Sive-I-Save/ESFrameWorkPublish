# fix compile error responsibility static gate

Acceptance id: fix-compile-error-static. Profile: engineering.

Required specialized static cases:

- dependency-closure: replay the dependency-closure contract from source/configuration and record deterministic evidence.
- boundary-rejection: replay the boundary-rejection contract from source/configuration and record deterministic evidence.
- deterministic-output: replay the deterministic-output contract from source/configuration and record deterministic evidence.
- interruption-recovery: replay the interruption-recovery contract from source/configuration and record deterministic evidence.
- evidence-freshness: replay the evidence-freshness contract from source/configuration and record deterministic evidence.

Static assertions cover boundary, deterministic replay, evidence integrity, static contract structure, and source/configuration claims. Runtime process, Unity, timing, visual, and release behavior remain unproven until separately authorized.

