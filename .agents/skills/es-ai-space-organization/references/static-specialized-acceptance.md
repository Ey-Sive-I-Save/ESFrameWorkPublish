# Static specialized acceptance

Acceptance id: `space-organization-static`

This Skill proves only deterministic Local/Public ownership routing for AI-generated content.

Required cases:

- `route-minimality`: each content purpose maps to one canonical root.
- `authority-preservation`: ES/AISpace README remains the authority.
- `denied-expansion`: guidance cannot authorize delete, rename, Git, Unity, network, or release.
- `deterministic-routing`: identical path and purpose produce identical classification.
- `local-temp-routing`: private temporary screenshots and caches route to
  `ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/` while Unity-owned captures stay in
  their existing Assets-owned roots.
- `skill-aispace-binding`: generation/cache-capable Skills resolve a stable binding ID from
  `.agents/SKILL_AISPACE_BINDINGS.json`, and the AISpace relation projection points back to the
  Skill governance contract.
- `asset-purpose-disambiguation`: `Assets/Screenshots` is not treated as the default AI temp
  root; only an explicit Unity import/reference or test-fixture purpose keeps an Asset path.
- `stale-guidance`: changed authority text requires fresh classification.
- `authority-identity`: the machine-readable AISpace identity points to the canonical body and
  declares the single-root boundary.
- `discovery-closure`: every project discovery marker and AIBrain entrypoint points back to the
  canonical AISpace body.
- `non-redundant-body`: pointer/index and historical files do not duplicate the canonical body.
- `no-competing-root`: Unity's `Assets/ES/AISpace/Public` path is an import exit, not a competing
  AISpace authority, and no second root identity is accepted.
- `no-runtime-competition`: the policy has no runtime lease or last-write-wins; stale writes are
  rejected for reread.

These checks are static evidence and do not prove Unity import, runtime loading, or release behavior.
