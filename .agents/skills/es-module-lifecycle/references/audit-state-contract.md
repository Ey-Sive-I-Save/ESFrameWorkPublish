# ES Audit Continuation State Contract

Use this contract only after the module audit has produced an evidence-backed result.

## Write authority

1. Default to no write.
2. Ask at most once after the audit when a checkpoint would materially help continuation.
3. Require explicit confirmation of the exact file and region unless the user already supplied both.
4. Treat permission as limited to that region. Do not infer permission to edit source, assets, other documentation, Git state, Unity, or external systems.
5. Prefer an existing module authority document. Create a new state document only when the user approves the path and no existing authority is suitable.

## Managed block

Use stable markers so later updates cannot overwrite neighboring content:

```text
<!-- ES-AUDIT-STATE:BEGIN module=<stable-module-key> -->
...checkpoint fields...
<!-- ES-AUDIT-STATE:END module=<stable-module-key> -->
```

Reject a write when markers are duplicated, nested, malformed, or overlap another managed block. Preserve all content outside the exact block.

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
5. Refresh the block only after current authorization permits that exact state write.
6. Continue implementation only under a current execution AICommand or explicit user authorization.

Never present a checkpoint as proof that Unity compilation, Test Runner, PlayMode, Profiler, Player, IL2CPP, provider, or release evidence still passes.
