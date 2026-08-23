# Evidence Receipt Contract

Authority: `.agents/skills/es-skill-governance` and `.agents/skills/es-skill-validator`.

This Skill's receipts prove only deterministic session discovery and binding invalidation. They do not prove that a model read, understood, executed, or accepted a Skill.

Every execution receipt must include:

- `skillName`, `case`, `status`, `evidenceLevel`, and project-relative `receiptPath`;
- `sourceRefs` and matching `sourceRefHashes`;
- `skillHash`, `governanceHash`, `validatorHash`, `planHash`, and `capturedUtc`;
- `runtimeStatus` or an explicit `runtime-not-run` boundary.

Allowed status values are `passed`, `blocked`, `failed`, and `not-run`. Missing, stale, or contradictory evidence requires a fresh snapshot comparison and, when a selected binding changed, a fresh AIBrain plan.
