---
name: es-module-lifecycle
description: Classify and govern ESFramework modules that are proposed, scaffolded, experimental, partially implemented, integrating, awaiting verification, stable, deprecated, or archived. Use when auditing unfinished modules, deciding whether a feature is truly usable, preventing experimental code from leaking into stable systems, defining readiness gates, recording an optional user-approved continuation checkpoint, or resuming implementation from a previously recorded audit state.
---

# Govern ES Module Lifecycle

Classify the module from current implementation and evidence. Never infer completion from directories, type names, TODO counts, documentation claims, or an external report.

## Workflow

1. Read the AIWarnings start files, the module-lifecycle warning, and the target domain rules selected by `RuleIndex`.
2. Select `检查_模块成熟度与半成品影响_AI命令.md` when the user has not granted a narrower execution command. Treat it as read-only.
3. Define the module boundary from its authority, entry points, registrations, consumers, and release path. Do not equate a folder with a module.
4. Inspect source, configuration, serialized assets, initialization, tests, documentation, generated artifacts, and release integration relevant to that boundary.
5. Assign exactly one maturity state: `Proposed`, `Scaffolded`, `Experimental`, `Implementing`, `Integrating`, `Verifying`, `Stable`, `Deprecated`, or `Archived`.
6. Record `Blocked` separately with the exact missing authority, dependency, decision, tool, platform, or evidence.
7. Detect unfinished-code leakage: default registration, automatic initialization, stable-module dependencies, serialized references, public compatibility claims, empty success paths, swallowed errors, and unrecoverable migrations.
8. Build an evidence matrix without upgrading `.csproj`, Console, Test Runner, PlayMode, Profiler, Player, IL2CPP, provider, or release evidence into another layer.
9. Recommend the smallest reversible action that satisfies the next transition gate. Do not implement, delete, migrate, stage, or publish without matching user and AICommand authority.
10. If authorized changes are made, preserve unrelated work, run `$es-utf8-guard`, and invoke `$es-unity-compile` or `$es-release-acceptance` only for evidence actually required by the target state.
11. After delivering the audit, enter `audit+checkpoint` only when the user already authorized a state write or explicitly accepts one concise checkpoint question. Otherwise remain `audit-only` and write nothing.
12. For a checkpoint, require an exact target file and region. Read [references/audit-state-contract.md](references/audit-state-contract.md), inspect overlap, and update only the authorized block. Never create a second authority when the module already has one.
13. On resume, treat the checkpoint as navigation rather than current truth. Recheck branch, HEAD, relevant worktree paths, latest AIWarnings, authority entry, activation, dependencies, and evidence before continuing.

## Decision rules

- Keep an unstarted direction in `Proposed`; do not create empty runtime structure merely to show progress.
- Require `Scaffolded`, `Experimental`, and `Implementing` code to remain compilable and explicitly isolated from default production activation.
- Do not mark `Integrating` until the main path and failure, cancellation, teardown, or rollback paths exist.
- Do not mark `Verifying` while feature scope is still expanding.
- Mark `Stable` only for the exact scope, platform, and evidence layers that passed.
- Downgrade or block a previously stable scope when current evidence reveals a regression or incomplete migration.
- Prevent stable modules from depending on experimental modules unless an explicit reviewed boundary isolates the dependency.
- A checkpoint write authorizes only the named state document region. It does not authorize source implementation, migration, deletion, staging, commit, push, Unity execution, or release operations.
- Mark a checkpoint stale when its branch/HEAD, relevant worktree fingerprint, authority entry, activation path, dependency boundary, or evidence layer no longer matches current facts.

## Continuation modes

- `audit-only`: return findings and the next action; do not ask repeatedly and do not write a state file.
- `audit+checkpoint`: after the audit, ask at most once when useful. Write only after the user confirms the target file and region, unless both were already explicitly provided.
- `resume`: read the named checkpoint, report stale fields, refresh facts from source, then continue only within the current user and AICommand authority.

## Required output

Return the module boundary, maturity state, blocked reason, committed scope, authority entry, activation mode, upstream dependencies, downstream consumers, unfinished-code leakage, evidence matrix, smallest next transition action, checkpoint status (`not-requested`, `offered`, `written`, `stale`, or `refused`), and the exact resume entry when one exists.
