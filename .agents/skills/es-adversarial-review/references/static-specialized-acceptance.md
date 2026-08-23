# adversarial review responsibility static gate

Acceptance id: adversarial-review-static. Profile: governance.

Required specialized static cases:

- authority-routing: replay the authority-routing contract from source/configuration and record deterministic evidence.
- input-boundary: replay the input-boundary contract from source/configuration and record deterministic evidence.
- denied-expansion: replay the denied-expansion contract from source/configuration and record deterministic evidence.
- deterministic-output: replay the deterministic-output contract from source/configuration and record deterministic evidence.
- evidence-freshness: replay the evidence-freshness contract from source/configuration and record deterministic evidence.

Static assertions cover boundary, deterministic replay, evidence integrity, static contract structure, and source/configuration claims. Runtime process, Unity, timing, visual, and release behavior remain unproven until separately authorized.

