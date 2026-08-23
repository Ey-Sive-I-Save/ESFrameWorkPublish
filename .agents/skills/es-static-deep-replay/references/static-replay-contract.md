# StaticDeepReplay contract

StaticDeepReplay is a bounded, read-only replay of what source/configuration and deterministic scripts can prove. It is not a Unity run and cannot claim visual, PlayMode, timing, Profiler, Player, IL2CPP, or release behavior.

Required manifest fields:

- `schemaVersion`
- `skillName`
- `sourceRoots`
- `cases`
- `runtimeClaimsNotProven`
- `runtimeEscalation`
- `responsibilityProfile`
- `responsibilityChecks`
- `responsibilityScope`

The seven replay cases are the common floor, not the complete acceptance plan. Each Skill must select a responsibility profile (`governance`, `knowledge`, `editor`, `engineering`, `authoring`, `testing`, `session`, `release`, or `base`) and declare the checks that are specific to that responsibility. The runner executes those checks and reports them separately in `customCheckResults`; a common green result cannot hide a failed responsibility check.

All source paths are project-relative. Reports must include source hashes, case status, `staticStatus`, `overallVerdict`, `claimsNotProven`, and `nextAction`.
