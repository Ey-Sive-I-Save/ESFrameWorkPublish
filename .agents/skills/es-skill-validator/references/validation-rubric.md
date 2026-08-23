# Validation rubric

| Profile | Pass condition | Block condition |
|---|---|---|
| Structural | Skill contract, UTF-8, frontmatter and references pass | missing required file, invalid name, broken reference |
| Governance | metadata is valid and permission-conservative | missing/stale metadata, direct execution, missing controls |
| Catalog | exactly one record and current hashes | missing record, duplicate record, stale hash |
| Security | no unreviewed high-risk signal | credential access, exfiltration, guard bypass or hidden network behavior |
| Semantic | Skill is bound to ESFramework authority, AIBrain/Knowledge routes, Resource Index and Catalog semantics | missing authority source, route binding, Knowledge binding, stale Catalog metadata or governance mismatch |
| Boundary | AIWarnings refusal, AICommand matching, path/capability/evidence boundaries pass | any expansion, escape, undeclared external capability or evidence overclaim |
| Evidence | required cases have current, hash-bound and plan-bound receipts | missing, stale, contradictory or over-claimed evidence |

`Implemented-Unverified` may pass structural/governance checks while remaining ineligible for `Accepted`. `Stable` requires representative behavioral evidence and adversarial review.

`Boundary` is the project semantic decision layer, not another formatting rule. It compares executable Skill scripts and governance declarations with current project authority: an undeclared write or external capability returns `NoMatchingCommand`; AIWarnings refusal semantics, paths outside the project root, secret access, undeclared network or destructive operations, and static claims that Unity/Runtime/Player/IL2CPP/Release are verified all fail closed.
