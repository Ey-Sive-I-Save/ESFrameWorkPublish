# Static / Runtime Verification Semantics

Every governed Skill has two independent evidence axes. They answer different questions and must not be collapsed into one `passed` or `blocked` result.

## Default execution order

The project is **StaticDeepReplay-first**. A Skill must exhaust source/configuration replay, contract simulation, boundary checks, deterministic fixtures, cache-backed analysis, and negative/recovery cases before proposing Runtime execution. Static evidence has a default portfolio weight of `0.7`; Runtime evidence has a default weight of `0.3`.

Runtime is never an implicit next step. Starting Unity, opening a project, entering PlayMode, changing scenes, launching a Player, or invoking an interactive, long-running, networked, or externally consequential process requires all of the following:

1. a current user instruction explicitly naming the Runtime/external action;
2. a declared Runtime evidence budget, target, timeout, and stop condition;
3. a reason StaticDeepReplay cannot answer the remaining claim;
4. when the selected execution channel is AIBrain/Worker, its plan and matching AICommand/TaskContract protocol.

Without the first three conditions the correct result is `runtime-not-authorized` or `runtime-not-run`; do not start Runtime “for completeness”. Missing managed-channel inputs block that channel only and do not require the user to approve the same action again.

Project-bundled deterministic static validators, parsers, compilers, and formatters that are strictly necessary to verify an already authorized target remain on the Static axis. They do not require a separately named process action when their inputs and timeout are bounded and they do not access the network, install dependencies, start Unity/Runtime, publish, or leave a resident service. This exception authorizes verification only; it cannot expand the project goal or evidence claim.

## Static axis

`Static` proves what can be established from source, configuration, contracts, hashes, deterministic scripts, and repository artifacts without starting Unity or another external runtime.

Statuses: `static-passed`, `static-partial`, `static-blocked`.

The static result has three independent diagnostic layers:

- `staticCodeStatus`: source structure, unsafe code, security signals, and semantic implementation defects;
- `staticContractStatus`: Skill governance, verification profiles, StaticDeepReplay, Catalog and contract defects;
- `staticBoundaryStatus`: external path, process, terminal, session, network, credential or permission-expansion boundaries.

`StaticBoundaryBlocked` is a hard stop, but it is not a claim that ES business code is defective. It means the declared external side effect cannot yet be proven safe and authorized from the available static evidence. A Runtime receipt cannot erase this boundary defect; the boundary contract or implementation must be tightened first.

`static-partial` also covers a deterministic static result with bounded `review` findings. A review finding is not an accepted security proof and is not equivalent to a pass: it means the source establishes a project-root or internal-tool boundary strongly enough to continue static work, while a receipt or focused human inspection is still required. Unproven dynamic paths, external execution, credential/network access, destructive operations, permission expansion, and evidence overclaim remain `static-blocked`.

Static evidence must never be phrased as runtime, visual, PlayMode, profiler, Player, IL2CPP, or release acceptance.

## Runtime axis

`Runtime` proves behavior that depends on Unity, a process host, a device, a display, timing, layout engine, serialization reload, or another external execution environment.

Statuses: `runtime-passed`, `runtime-not-run`, `runtime-blocked`, `runtime-failed`.

`runtime-not-run` is evidence absence, not source failure. It blocks only profiles that explicitly require runtime proof.

`static-blocked` means the source/design itself has a defect or an unsafe boundary and requires code/configuration work. `runtime-blocked` means the selected profile requires external evidence that has not been authorized, is missing, stale, or cannot run; it must not be reported as a source defect. `runtime-not-run` means the runtime phase was not selected or has not been proposed.

## Verification profiles

- `StaticReview`: requires static dimensions only and may finish with `static-passed` or `static-partial`.
- `EngineeringReadiness`: requires static safety and contracts; runtime may remain `runtime-not-run` when optional.
- `RuntimeAcceptance`: requires declared runtime dimensions and fresh receipts.
- `ReleaseAcceptance`: requires RuntimeAcceptance plus compatibility, performance, migration, and release evidence where applicable.

The overall result must include `profile`, `staticStatus`, `staticCodeStatus`, `staticContractStatus`, `staticBoundaryStatus`, `evidenceStatus`, `runtimeStatus`, `overallVerdict`, `decisionStatus`, `blockingLayer`, `claimsNotProven`, and `nextAction`. `StaticReview` may return `overallVerdict: StaticReviewComplete`; that verdict is scoped to static review and must never be read as editor/runtime availability. When Static passes and Runtime is absent, use `StaticCompleteRuntimePending` or `RuntimeRequiredForSelectedProfile`, not a generic source `Blocked` message. Missing behavioral receipts use `decisionStatus: evidence-pending` and `blockingLayer: evidence`; they must not be reported as a source defect.

Every verification profile must expose `staticWeight`, `runtimeWeight`, `staticDeepReplayRequired`, and `runtimeAuthorizationRequired`. `staticWeight` must be at least `0.5`; profiles that violate this rule are invalid.

For a Skill whose `RuntimeAcceptance.runtimeRequired` or `ReleaseAcceptance.runtimeRequired` is `true`, `runtime-not-run` or `runtime-not-authorized` is a hard `Blocked` result in that profile. No weighted score may promote it to `Ready`.

### Evidence receipt integrity

Every Runtime or behavioral receipt must be a project-relative JSON artifact with `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `toolId`, `unityVersion` (use `not-applicable` for non-Unity work), `capturedUtc`, `authorizationSource`, `sourceRefs`, and `sourceRefHashes`. A managed AIBrain receipt also requires `planHash`; a direct user-directed receipt records `planHash: not-applicable`. The validator must resolve paths, verify source hashes and freshness, and reject stale evidence. Receipt failure blocks the evidence claim, not the user's underlying project authorization.

## StaticDeepReplay completion contract

`StaticDeepReplay` is complete only when the applicable cases have deterministic, source-bound receipts:

- normal input;
- invalid input;
- denied expansion/path boundary;
- repeat/idempotency;
- source hash change and cache invalidation;
- interruption/recovery;
- deterministic output replay.

An inapplicable case must be explicitly marked `not-applicable` with a reason; it cannot be silently omitted.

## Runtime authorization contract

Runtime authorization is not inferred from a file-edit request. A direct Runtime action must bind the current user instruction, exact target, start/expiry, budget, timeout and stop condition. When AIBrain performs it, the same envelope additionally binds PlanHash, AICommand and TaskContract. Missing bindings yield `runtime-not-authorized`; they never trigger a request for duplicate user approval.

## Declaration rule

New or materially changed Skills should declare `verificationProfiles` in `governance.json`. Until migrated, the conservative default applies: StaticReview is required; RuntimeAcceptance is required only when the Skill claims or directly operates runtime-dependent behavior.

## Responsibility-specific static acceptance standard

The seven common StaticDeepReplay cases are a minimum floor, not a universal acceptance plan. Every registered Skill must carry a complete `specializedAcceptance` contract in `static-replay.manifest.json` and a discoverable `references/static-specialized-acceptance.md` guide.

Each specialized contract must declare:

- a stable acceptance ID and responsibility-specific title;
- at least five specialized static cases;
- evidence artifacts that exist under the Skill's managed root;
- at least three source assertions that can be replayed against the Skill source/configuration;
- an explicit Runtime boundary and non-claims statement.

The authoritative registry is `.agents/skills/es-static-deep-replay/references/specialized-acceptance-registry.json`. The shared runner reports specialized results in `specializedAcceptance`; `StaticDeepReplay` is incomplete when a registered Skill's specialized contract is missing, stale, or failed. A common seven-case pass cannot conceal a specialized failure, and a specialized static pass cannot claim Runtime behavior.
