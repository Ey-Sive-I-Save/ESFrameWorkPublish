# Static replay adapter

Responsibility profile: authoring
Responsibility scope: read-only parser routing and ProjectionPacket contract.

The fixed replay cases cover valid routing, malformed input, denied expansion, repeat idempotency, hash-change invalidation, interruption recovery, and deterministic output. Runtime, Unity, network, and release behavior are not claimed.

Cases: normal-input, invalid-input, denied-expansion, repeat-idempotency, hash-change-cache-invalidation, interruption-recovery, deterministic-output.
Custom checks: change-boundary, resource-projection, deterministic-replay, evidence-contract.
