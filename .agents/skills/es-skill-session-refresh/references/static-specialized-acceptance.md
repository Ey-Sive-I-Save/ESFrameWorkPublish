# Skill session incremental discovery

Acceptance id: `skill-session-incremental-discovery`

Responsibility profile: session

Required specialized cases:

- `snapshot-identity`: the session, baseline and current snapshot hashes are bound to one project root.
- `metadata-delta`: changed Skill metadata is reported without reading unrelated document contents.
- `resource-delta`: changed references or scripts are reported as resource changes.
- `unrelated-change`: an unrelated Skill is recorded out-of-scope and does not invalidate the selected route.
- `replan-on-bound-change`: a selected Skill, governance, Knowledge or contract change marks the prior binding stale and requires re-plan.

Static boundary: these checks prove deterministic metadata discovery and invalidation. They do not prove that a model read, understood, executed or accepted a Skill.
