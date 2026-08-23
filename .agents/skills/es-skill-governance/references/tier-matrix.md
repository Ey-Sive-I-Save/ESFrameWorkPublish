# Skill tier matrix

This matrix defines the minimum governance bar. It does not authorize any operation.

| Dimension | SmallTool | Workflow | Engineering |
|---|---|---|---|
| Purpose | One fast, bounded operation | Repeatable multi-step process | End-to-end project capability |
| Scope | One domain or artifact family | Multiple modules or project surfaces | Architecture, tooling, runtime/build/release as applicable |
| Side effect | Read-only; otherwise one explicit reversible write | Several staged writes with checkpoints | Broad writes or migration, each separately authorized |
| Entry | One prompt or command | Prompt plus parameters and phase | Framing plus plan, risk and acceptance scope |
| Safety | Preconditions, dry-run for writes, fail closed | Dry-run, confirmation, cancellation, idempotency, recovery | Permission matrix, change budget, rollback and compatibility |
| Resources | `SKILL.md`; optional one script/reference | References and deterministic scripts | References, scripts, evidence templates and dependency map |
| Child tools | Not by default | Single-purpose and locally governed | Allowed, versioned and independently validated |
| Evidence | S1 structure; S2 deterministic static behavior | S2 plus failure/recovery; S3/S4 for Editor claims | Highest relevant S-level; S5/S6 for runtime/release claims |
| Scale | Explain bounded scale | Record batching and performance risks | Quantify capacity, steady-state cost, concurrency and rollback |

## SmallTool acceptance

- One sentence states the result and non-goal.
- Inputs and outputs are bounded and inspectable.
- Missing prerequisites produce refusal or no-op.
- No hidden child process, network upload, broad scan or unrelated write.
- Positive, invalid-input and permission-denied examples exist.

## Workflow acceptance

- Phases have explicit inputs, outputs and checkpoint evidence.
- Re-running a phase is idempotent or deliberately rejected.
- Failure cannot leave an ambiguous partial state.
- Child scripts expose stable parameters and exit codes.
- An interrupted run has a documented recovery path.

## Engineering acceptance

- Lifecycle and acceptance owner are named.
- Architecture, data ownership, permissions, performance, compatibility and release boundaries are named.
- High-risk operations are separate commands or explicit confirmations; confirmation is not business authorization.
- A risk register maps each material risk to prevention, detection and recovery.
- Evidence is collected at the target layer; static code or preview cannot stand in for Unity, Player, Profiler, IL2CPP or release evidence.
