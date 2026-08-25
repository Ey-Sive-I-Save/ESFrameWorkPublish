# es-unity-compile control contract

- Verify scope, authority, and source evidence before changing project state.
- Apply the central user-directed action authority: a current explicit user request authorizes its bounded action; only inferred expansion is denied. Action-specific side effects must be named, and AIBrain/AICommand inputs apply only when their managed channel is selected.
- Record positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery results.
- Stop on missing evidence, stale hashes, encoding failures, or ownership ambiguity.
- Build identity Capture writes only an explicitly named immutable JSON receipt under `ES/Output/BuildIdentity`; Finalize writes a second immutable receipt there; Validate is read-only.
- Build artifacts must remain under the Capture-declared `ES/Output/Builds` path. Absolute paths, traversal, reparse points, receipt overwrites, duplicate artifact roles, and output-root expansion are denied.
- Build identity scripts do not launch Unity or claim build success. Validator exit codes are `0` current static identity, `1` invalid/tampered/missing evidence, and `2` stale/input-drifted; `-SkipCurrentInputCheck` never proves freshness.
