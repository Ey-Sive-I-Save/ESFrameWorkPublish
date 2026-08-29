# Static Replay Adapter

Responsibility profile: governance

The adapter covers the exact manifest cases: `normal-input`, `invalid-input`,
`denied-expansion`, `repeat-idempotency`, `hash-change-cache-invalidation`,
`interruption-recovery`, and `deterministic-output`. It also covers the custom
checks `authority-routing`, `permission-boundary`, `deterministic-replay`, and
`evidence-contract`.

Replay checks deterministic auto-fast wrapping, high-risk upgrade to auto-safe, empty-input rejection, permission expansion denial, repeat stability, prompt-hash invalidation, interruption recovery by stateless rerun, and deterministic output. It proves local source/configuration behavior only. It does not prove model quality, semantic completeness, latency under production concurrency, Unity, Runtime, network, provider, or release behavior.
