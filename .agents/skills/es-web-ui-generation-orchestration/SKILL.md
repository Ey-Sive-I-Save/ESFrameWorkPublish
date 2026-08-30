---
name: es-web-ui-generation-orchestration
description: Orchestrate static and dynamic WebPageStudio generation as bounded child tasks with deterministic ready-queue scheduling, concurrency budgets, Lease/CAS admission, cancellation, retry/recovery and evidence aggregation. Use when a web UI generation request spans network, preview, visual and release layers or when scheduler/worker capability must be designed, replayed or validated.
---

# ES Web UI Generation Orchestration

## Engineering controls

- Supply-chain evidence is pinned in `references/open-source-source-manifest.json`; source snapshots stay outside the project and are consumed by hash.
- Every stage follows `references/ai-step-execution-contract.md` and returns a machine-readable receipt.
- Every stage receipt must also carry a unified `StageExecutionEnvelope`: `taskContextRef` (Task/Focus/Goal/Route/Source hashes), `abcdEnvelope` (ABCC parity capabilities, mappings, trust/state decision and evidence refs), `subAgentEnvelope` (child input/output hashes and decisions), and `actualUsage`. Planned counts or copied packet text are not execution evidence.
- Each generation step has a dedicated reference contract and must be read before execution: `step-01-intent-analysis.md`, `step-02-knowledge-synthesis.md`, `step-03-capability-analysis.md`, `step-04-information-architecture.md`, `step-05-interaction-design.md`, `step-06-visual-responsive-design.md`, `step-07-html-materialization.md`, and `step-08-quality-closeout.md`. The stage receipt must include `aiAnalysis`, `execution`, `returnReceipt`, and `sourceRefs`; missing fields are a block.

- Step 00 is mandatory: read [`step-00-authority-baseline.md`](references/step-00-authority-baseline.md) and validate [`authoritative-step-baseline.v1.json`](references/authoritative-step-baseline.v1.json) with `scripts/Test-ESWebAuthoritativeStepBaseline.ps1` before any business stage. TaskContext, TaskFocus, SubAgent contracts, ABCD/ABCC, Knowledge/AIBrain and RoutePlan are bound resources, not optional context. Their roles and authority boundaries must appear in the baseline receipt.
- Invocation timing is frozen: TaskFocus is proposed once from the latest user goal before intent/Knowledge; TaskContext is created once after accepted Focus + IntentLock and before Knowledge; both are reused by every later stage. Only explicit GoalRevision/Reopen creates a new pair. Completion is platform-evaluated after evidence closeout; no Skill, SubAgent or Worker may self-accept.

## Mandatory sequential gate

The canonical order is immutable: `intent-review -> knowledge-* -> open-source-capability-aggregation -> open-source-capability-compilation -> prompt-generation -> layout-thinking -> solution-finalization -> deep-design -> html-materialization -> quality-closeout`. A later stage is forbidden when any earlier stage is blocked, when a required reference was not read, or when its prior receipt is absent. `Invoke-ESWebPageStudioPreflight.ps1` propagates an upstream block and records `blockedByUpstream=true`; callers must not manually resume at a later stage. Each stage must record the exact files read, the analysis conclusion, the bounded execution, and an observable return receipt. “Accepted” metadata without these four fields is invalid.

## Scope and authority

This Skill describes the project-local orchestration boundary for WebPageStudio. It composes existing projection, schedule, admission and evidence contracts; it does not replace `ES/Automation/WebPageStudio` implementations, AIWarnings, AICommands or the TaskContext authority. The current user instruction authorizes only the declared project-local files and actions. Never infer permission for Unity, browser, network, Git, release, deletion or a resident process.

Read, in order, only the sources needed for the requested route:

1. `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` and the matching `KnowledgeIndex.yaml` entries.
2. `ES/Automation/WebPageStudio/Invoke-ESWebUiSubAgentProjection.ps1`, `Invoke-ESWebUiSubAgentSchedule.ps1` and `ESWebUiSubAgentScheduler.psm1`.
3. `ES/Automation/TaskCollaboration/ESTaskCollaborationContracts.psm1` and the v1 plan, child-registry, lease-CAS, result-envelope and parent-aggregation schemas.
4. `ES/Automation/WebPageStudio/Test-ESWebUiSubAgentAdmission.ps1`, `Test-ESWebUiSubAgentSchedule.ps1` and `Test-ESWebUiSubAgentScheduler.ps1`.

`SKILL.md` is workflow guidance, not an authorization token. Existing source and contracts remain authoritative when this document differs.

The capability status table and open-source pattern comparison live in
[`references/capability-coverage-matrix.yaml`](references/capability-coverage-matrix.yaml); read it when reporting coverage or planning a kernel upgrade.

## Capability route

1. Normalize a WebPageStudio request into four independent evidence children: `web-ui.network`, `web-ui.preview`, `web-ui.visual`, and `web-ui.release`.
2. Build a plan with serial `static-preparation`, parallel `layer-evidence`, serial `layer-validation`, then `evidence-aggregation`. Preserve dependency edges and bind `planHash`, `verificationHash`, parent task identity and context revision.
3. Run admission before dispatch. Reject duplicate child IDs, budget oversell, missing verification binding, invalid dependency DAGs, stale context, `runtime-not-run` candidates and receipt identity/hash drift.
4. Feed admitted children to a ready queue. Dispatch only tasks in the current wave and while `activeCount < maxParallel`; deterministic ordering is preferred for replay.
5. Claim a Lease/CAS tuple containing task revision and context version. A result is accepted only when the CAS observation is current, the lease is active and the result envelope references the same plan and parent.
6. Propagate cancellation to active and ready children. A cancelled task is terminal only after a valid lease observation; an expired lease is rejected and must be recovered or explicitly reported as unresolved.
7. Retry only failed, retryable work within `maxAttempts`; preserve attempt identity, reason code, idempotency key and evidence hashes. Never retry malformed, unauthorized, stale or non-retryable failures.
8. Aggregate only validated result envelopes. Keep `candidate`, `failed`, `cancelled`, `review`, `stale` and `runtime-not-run` distinct; aggregation acceptance is not release acceptance.
9. Emit a scheduler receipt with waves, active/completed counts, max observed concurrency, event chronology, plan/verification hashes and non-claims. Static replay may prove queue invariants, not cross-process throughput, browser rendering, Unity behavior or production deployment.

## Kernel status and safe claims

`ESWebUiSubAgentScheduler.psm1` is an in-memory deterministic replay kernel. It currently exercises ready-queue waves, an in-memory concurrency bound, lease observation, retry re-entry, cancellation and stale/expired lease rejection. Its receipt intentionally states `runtimeStatus: runtime-not-run` and that it does not start an external worker, provide persistent atomic CAS, prove process-level parallel speed-up or persist RunRecords.

Do not claim a production Worker/Scheduler until a separately authorized implementation supplies: a persistent LeaseStore with atomic compare-and-swap, worker handle/ProcessRunner ownership, durable RunRecord and recovery, retry policy with backoff/jitter and idempotency, cancellation acknowledgement/timeout, snapshot/restore, bounded resource isolation, and timing evidence from the target host. Runtime and Release profiles remain unproven when those actions are not explicitly requested and run.

## Aggregated open-source generation capability

The callable profile at `references/open-source-capability-profile.json` aggregates the core mechanisms of Next.js, Astro, Nuxt, SvelteKit, Remix and Qwik without vendoring their runtimes. Invoke `scripts/Invoke-ESWebOpenSourceCapabilityProfile.ps1 -RequestPath <project-relative-request>` during WebPageStudio preflight. The receipt exposes render policy, component boundary/hydration policy, route data contract, interaction state machine, progressive enhancement, resumability budget and measurable performance budget. A generator must consume the profile and bind its `profileId`, render policy, component boundary and state-machine requirement to the generated Page IR/HTML; profile labels alone are not acceptance evidence. Browser, framework-runtime and production behavior remain Runtime claims and require separate authorization.

The pinned source evidence is recorded in [`references/open-source-source-manifest.json`](references/open-source-source-manifest.json). It points to an external, immutable snapshot containing one bounded core-mechanism snippet and license text per framework. `scripts/Invoke-ESWebOpenSourceCapabilityCompiler.ps1` verifies the six entries and compiles concrete local strategies. The detailed per-step `AI analysis -> execution -> return` contract is [`references/ai-step-execution-contract.md`](references/ai-step-execution-contract.md); every stage must emit its receipt, and a missing receipt is a block rather than a prose assumption.

## Failure and recovery rules

- Fail closed on missing route scope, malformed contracts, hash mismatch, path escape, stale context, budget overflow or unauthorized side effects.
- On worker loss, retain the last lease/result receipt, mark the child `review` or `stale`, and re-plan from the accepted transcript; do not substitute another handoff source.
- On cancellation, stop dispatching new work, request cooperative cancellation, then classify acknowledgement timeout as unresolved rather than silently successful.
- On retry exhaustion, keep the original attempts and reason codes; aggregate a bounded failure with actionable recovery, never a fabricated candidate.
- Replays must be idempotent: the same plan and source hashes produce the same task partition and dependency graph. New source or governance hashes invalidate the prior plan.

## Verification boundary

Static evidence includes schema/contract validation, deterministic plan/admission/schedule/kernel replay, dependency and hash checks, UTF-8 checks and negative/recovery fixtures. Runtime evidence includes real worker processes, timing, browser/DOM/screenshot checks, Unity or deployment behavior. `runtime-not-run` is evidence absence, not static failure; it blocks only RuntimeAcceptance/ReleaseAcceptance claims.

## Skill 使用披露

使用本 Skill 时，首次用户可见进度需说明其用于 WebPageStudio 编排/调度边界；最终答复需列出本轮实际使用的 Skill 及作用。披露不等于授权、外部执行或验收证据。遵循项目根 `AGENTS.md` 与 `.agents/README.md` 的同名规范。

## Non-goals

This Skill does not start network, browser, Unity, Player or resident processes; modify Catalog, Git, history, release state or delete files; or copy AIWarnings/AICommands/knowledge bodies. It does not promote an in-memory replay into a production scheduler.
