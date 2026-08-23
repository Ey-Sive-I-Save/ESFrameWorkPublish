# Scale patterns

## Child tools

Create a child tool only for repeated deterministic work. Give it one verb, one input contract, one output contract and one failure model. Keep it inside the parent Skill's `scripts/` or `references/` unless it independently matches the direct-child Skill trigger rules.

Recommended relationship:

```text
Parent Skill -> inspect (read-only) -> plan -> apply (authorized write) -> verify -> recover
```

Each child declares whether it writes, which paths it may touch and whether reruns are safe. A parent must not hide a write-capable child behind a read-only name.

## References

- Put stable facts, schemas and decision tables in `references/`.
- Link directly from `SKILL.md`; avoid A -> B -> C chains.
- Include authority/status notes and search hints for long references.
- Remove stale references rather than keeping contradictory copies.

## Scripts

Every script should state inputs/defaults, outputs/exit codes, read scope, write scope, idempotency, failure/recovery and evidence emitted. Prefer read-only audits. If mutation is essential, separate dry-run from apply and require an explicit write switch.

## Dependencies and maintenance

Depend on project authority paths and existing tools; do not vendor copies. Missing MCP or Unity must produce a precise blocked result. Test a representative fixture and a malformed fixture. Re-run validation after frontmatter, scripts, references or metadata changes. Retire stale Skills rather than silently leaving ambiguous triggers.
