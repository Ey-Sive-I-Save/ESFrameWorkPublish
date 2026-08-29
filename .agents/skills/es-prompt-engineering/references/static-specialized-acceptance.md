# Prompt engineering static acceptance

- Acceptance id: `prompt-engineering-fast-wrapper`
- Profile: `governance`
- Required cases: `fast-wrap`, `safe-upgrade`, `invalid-input`, `permission-denial`, `idempotency`, `hash-invalidation`, `recovery`
- Source assertions: original prompt hash, versioned template, bounded reads/assertions, no external execution, structured verifier result
- Runtime boundary: static results do not prove model response quality or runtime performance.

