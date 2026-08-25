# SkillChangeImpact Contract

`Get-ESSkillChangeImpact.ps1` is a read-only preflight for an existing or new
project Skill. It classifies the semantic surface of the current Skill diff; it
does not write files, update Catalog/Registry, run Runtime, or grant authority.

## Classes

- `small`: documentation/format-only changes; local contract validation is enough.
- `medium`: behavioral references, tests, or bounded workflow changes; Creator,
  Governance, Contract, Evidence, and StaticDeepReplay revalidation is required.
- `major`: new Skill or changes to triggers, scripts, schemas, permissions,
  routing, governance, or evidence contracts; the same chain plus Catalog/Registry
  refresh is required.

The classification is derived from project-relative changed paths and bounded
semantic markers. File count is not used as a substitute for semantic impact.

## Output and gate

The evaluator emits `skillChangeImpact`, `revalidationRequired`,
`requiredStages`, `changedFiles`, `decisionSource=derived`, and
`completionClaimAllowed`. A `medium` or `major` result sets
`completionClaimAllowed=false` until the declared stages produce fresh evidence.
This is a status gate, not a second user authorization.

## Scope

`-SkillPath` must be one direct child of the project `.agents/skills` directory.
The evaluator reads Git status/diff and files inside that Skill only. It fails
closed for an invalid root, missing Skill directory, or path escape. Re-running
with the same worktree is deterministic.
