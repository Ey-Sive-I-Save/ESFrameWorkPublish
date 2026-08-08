# ES Audit Continuation State Contract

Use this contract only after the module audit has produced an evidence-backed result.

## Write authority

1. Default to no write.
2. Ask at most once after the audit when a checkpoint would materially help continuation.
3. Use only `ES/Documentation/Status/MODULE_AUDIT_STATE.md`; never ask the user to choose a path or region.
4. Treat “审计并记录” as explicit permission to update the identified module block. After plain “审计”, write only when the user accepts the single checkpoint question.
5. Treat permission as limited to that module block. Do not infer permission to edit source, assets, other documentation, Git state, Unity, or external systems.

## Managed block

Use stable markers so later updates cannot overwrite neighboring content:

```text
<!-- ES-AUDIT-STATE:BEGIN module=<stable-module-key> -->
...checkpoint fields...
<!-- ES-AUDIT-STATE:END module=<stable-module-key> -->
```

Reject a write when markers are duplicated, nested, malformed, or overlap another managed block. Preserve all content outside the exact block.

Derive `<stable-module-key>` from the module's stable architecture identity, not a transient display label, absolute path, branch name, or maturity state. Reuse an existing key for the same module. When identity is ambiguous, stop and ask only which module is in scope.

## Required fields

```markdown
### Audit continuation state

- Snapshot ID:
- Updated at:
- Module and committed scope:
- Maturity state:
- Blocked reason:
- Authority entry:
- Activation mode:
- Upstream dependencies:
- Downstream consumers:
- Unfinished-code leakage:
- Evidence present:
- Evidence missing:
- Branch / HEAD:
- Relevant worktree state:
- Last completed action:
- Smallest next action:
- Resume read list:
- Allowed next write scope:
- Invalidation triggers:
```

Use `none`, `not-run`, or `unknown` explicitly; never omit an inconvenient field. `Allowed next write scope` records previously discussed scope only and never grants future authority by itself.

## Invalidation and resume

A checkpoint is stale when any relevant source, serialized asset, configuration, registration, consumer, test, warning, branch, HEAD, or evidence layer changed after the snapshot.

On resume:

1. Read the current AIWarnings start chain and target-domain rules.
2. Read the checkpoint and its authority entry.
3. Compare branch, HEAD, relevant staged/unstaged/untracked paths, activation, dependencies, consumers, and evidence.
4. Report stale fields before using the checkpoint.
5. Refresh the block only after “审计并记录” or a current explicit confirmation permits the state write.
6. Continue implementation only under a current execution AICommand or explicit user authorization.

Never present a checkpoint as proof that Unity compilation, Test Runner, PlayMode, Profiler, Player, IL2CPP, provider, or release evidence still passes.
