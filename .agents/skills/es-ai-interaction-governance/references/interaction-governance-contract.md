# Interaction Governance Contract

## Path and hook boundary

- Persistent `ReportPath` and `OutputPath` values must be project-relative or resolve inside the project, and are accepted only below `ES/Output/Interaction` after canonical containment and reparse-point checks; escaping paths are rejected.
- Transcript conversion may additionally write one short-lived intermediate file below the operating system Temp root; no other external output root is accepted.
- Read-only evidence inputs are limited to the current project or system Temp. Explicit Codex transcript paths remain read-only host inputs and must resolve to an existing `.jsonl` file.
- SessionId lookup may read only the approved external user-profile state root at `CODEX_HOME/sessions`; it never writes that root.
- `Invoke-ESInteractionCloseoutHook.ps1` may invoke only the exact built-in closeout script. `CloseoutScriptPath` is compatibility input for identity confirmation, not an extension point.

## Intent contract gate

For work that changes user-visible behavior, lifecycle, state, defaults, recovery, or an automatic action, produce a versioned intent contract before implementation. The contract is a task-scoped boundary, not a permanent AIWarning or an authorization token.

Required fields are defined by `intent-contract.schema.json`: `objective`, `scope`, `mustPreserve`, `allowedTransitions`, `forbiddenTransitions`, `acceptanceSignals`, `counterexamples`, `nonGoals`, `assumptions`, `unresolvedQuestions`, `intentAlignmentStatus`, `executionDecision`, `revision`, and `sourceRefs`.

`intentAlignmentStatus` is a hard execution gate:

- `aligned`: the domain Skill may implement within the contract.
- `partial`: read-only analysis and bounded design only; do not choose missing user semantics.
- `unverifiable`: gather evidence or ask one focused clarification; do not claim understanding.
- `misaligned`: stop the current design, revise the contract, and invalidate the stale plan.

`executionDecision` must be `allow` only for `aligned`; `partial` and `unverifiable` use `analyze-only`, while `misaligned` uses `deny`. A user correction increments `revision`, records the prior contradiction, and invalidates implementation/evidence derived from the older revision.

For lifecycle or state work, `forbiddenTransitions` and `counterexamples` are mandatory. A system event such as `OnDisable`, reload, PlayMode, or panel replacement must not be treated as a user action unless the contract explicitly says so. Validate contracts with `scripts/Test-ESIntentContract.ps1` before handing them to a domain Skill.

## Required result

```json
{
  "profile": "default",
  "promptScore": 0,
  "verificationScore": 0,
  "intentAlignmentScore": 0,
  "evidenceQualityScore": 0,
  "calibrationScore": 0,
  "confidenceScore": 0,
  "overallScore": 0,
  "scoreSource": "deterministic-assessment",
  "objectiveClarity": "unclear",
  "goalDrift": "none",
  "runtimeStatus": "not-run",
  "claimsNotProven": [],
  "nextSteps": []
}
```

Scores are 0-10 advisory measurements. `promptScore` measures request completeness; `intentAlignmentScore` measures the current interpretation (not historical failure); `verificationScore` measures evidence present for the requested claim; `evidenceQualityScore` measures claim-to-evidence correspondence; `calibrationScore` records repeated-high-score and prior-misalignment calibration; `confidenceScore` measures how much explicit evidence supports the assessment. `overallScore` is a weighted score shrunk toward 5 when evidence is sparse, so isolated missing inputs do not create artificial extremes. No score authorizes action or means Accepted.

- Missing intent evidence prevents a high `intentAlignmentScore`.
- Keyword presence alone is not verification evidence.
- A user correction or confirmed goal drift lowers alignment and calibration scores.
- Historical misalignment must be reported separately from current intent alignment; a correction alone must not force the current alignment to zero.
- Repeated high scores must be recalibrated using the prior score/streak; 9-10 requires strong intent and evidence support.
- Closeout fields must use the assessment result's scores. Do not hand-write optimistic `提示评分` or `验证评分`; if no assessment was run, report the scores as unavailable or conservatively no higher than 5.
- Chinese closeout must show the complete icon score line on one line: `📝提示完整度 / 🎯意图契合度 / 🔍验证充分度 / 📚证据质量 / ⚖️校准 / 🏁综合`.
- When `overallScore < 4`, emit a visible `🚨低评分风险` (`LOW_SCORE_RISK`) notice and do not claim the objective is complete or verified; when `overallScore` is 4-6, emit `⚠️证据/契合度有限` (`LIMITED_EVIDENCE`).

## Phase and root-cause priority

- First classify the work phase. During feature development, especially editor work, investigate the authoritative call chain, state ownership, lifecycle, and trigger before adding compatibility or fallback behavior.
- Add compatibility/fallback only when the root cause is outside the feature, an explicit legacy requirement exists, or the user requests it; record its boundary and what it may conceal.

## P0 feedback and execution standard

The following are independent triggers. Any single trigger enters `P0-feedback`; conjunction is not required:

1. The user gives a `0` score or states that the work is zero-quality.
2. The user explicitly requests verification.
3. The user explicitly requests running a Skill.

`P0-feedback` requires an evidence-first recheck of intent, observable actions, routed Skill, verification results, and `claimsNotProven`. Trigger 2 requires actually running the bounded verification path. Trigger 3 requires reading and executing the project Skill selected by the route. A plan, explanation, queued action, or self-authored evidence field is not a substitute. A zero score changes scrutiny and evidence thresholds only; it never grants side-effect authority. If execution is blocked, return the exact blocker and do not claim completion.
- Do not let a fallback mask an unresolved development defect or silently expand scope.

## State semantics

- `clear`: target and intended outcome are explicit enough to proceed read-only.
- `partial`: a bounded assumption is possible, but a key constraint or acceptance signal is missing.
- `unclear`: acting would risk choosing the wrong target or scope.
- `goalDrift`: `none`, `possible`, or `confirmed`; drift never grants permission to expand scope.
- `runtimeStatus`: `passed`, `failed`, `not-run`, or `not-applicable`.

## Next-step safety

Every suggestion must have a stable ID, a one-based contiguous `number`, priority, risk, reason, and `requiresUserChoice: true`. The engine returns at most three suggestions and never executes them. The user may select a suggestion by replying with its number (`1`, `2`, or `3`); the stable ID remains the machine-facing identity.
When rendered in the user-facing closeout, the numbered next-step menu must be the final section; no Skill, verification, Runtime, or explanatory closeout field may follow it.
`Resolve-ESNextStepSelection.ps1` is the selection boundary: it accepts only a number present in the current assessment, binds the result to the assessment hash, and returns `selectedId` with `execution=not-executed`. A selected ID still requires a separately authorized task handler.

### Bounded context collection

At task start, the deterministic assessment derives the recommendation from structured signals: `taskKind`, `routeStatus`, `contextFreshness`, `riskLevel`, `taskStarted`, and `alreadyCollected`. These lifecycle and context signals are host-provided observations; the evaluator consumes them and does not prove that the host event actually occurred. Ambiguous or missing routes, stale or unknown context, and high-risk write/release work recommend collection; a simple read-only task with a resolved fresh route does not. `taskStarted=false` and `alreadyCollected=true` suppress a recommendation and are reported in `suppressedBy`; trigger reasons may remain present for audit but must not be interpreted as an emitted recommendation. `offer-context-collection` is an opt-in suggestion, not a startup mandate. The available choices are `skill-only`, `knowledge-only`, `aiwarnings-only`, `skill-knowledge`, and `skill-knowledge-aiwarnings`. Unless a narrower contract applies, cap the read set at 3 Skills, 3 Knowledge entries, and 3 AIWarnings P0/domain rules. Do not scan the full Skill tree, Knowledge corpus, or AIWarnings directory. Results must expose `recommendationReasons`, `suppressedBy`, and `decisionSource=derived`; test-only overrides must explicitly use `decisionSource=test-override` and are restricted to offline tests, fixed fixtures, and replay—not production collection execution. Record `TaskKey`, `PlanHash`, the user's selection, actual read set, source hashes, stale findings, and non-claims; a declined or absent selection means no collection occurred.

After an explicit user selection, `scripts/Invoke-ESContextCollection.ps1` may create a bounded read-only receipt from caller-resolved project-relative paths. It enforces project-root containment, duplicate rejection, per-kind limits, selection-kind compatibility, SHA-256 source hashes, and a deterministic `readSetHash`. It does not infer routes, fill missing paths, authorize writes, run Runtime, access network, or claim that the selected set is complete.

## Evidence-first fast assessment

`Invoke-ESInteractionEvidenceAssessment.ps1` is the authoritative fast path for real-work observation. It consumes transcript events, tool events, file changes, verification events, user corrections, and requested scope. It reports `aligned`, `partial`, `misaligned`, or `unverifiable` and emits no score when evidence is insufficient. The numeric evaluator is advisory only and must not replace this observed status.

`Convert-CodexTranscriptToEvidence.ps1` is the bounded adapter for Codex JSONL. It excludes system/developer/world-state records, preserves source line numbers, records parse errors, and marks inferred scope as an observation rather than authorization.

The adapter also reports invocation-local `observationMetrics` (`recordsRead`, `elapsedMs`, and `textTruncated`). These describe the cost and boundedness of that read only; they are not a global performance or quality claim.

`writeTargetHints` contains at most 64 unique paths observed in mutating tool inputs. It is a bounded observation of attempted targets, not proof that a diff was applied; actual file state still requires a separate worktree/diff check.

`writeTargetResolution` resolves only those hints that are inside the project root and reports `exists`/`missing` plus a read-only current-worktree state (`modified`, `untracked`, or `unchanged`). Existing project files also carry byte length and SHA-256 for re-reading. Outside-project and unresolvable paths are reported without reading them. This is current-state evidence, not proof that the session's write succeeded or that a historical diff belongs to this turn.

`diagnosticCodes` is a bounded root-cause projection from observed findings: it explains why the current status is limited, but it does not replace the underlying evidence or claim semantic understanding. Runtime evaluation is opt-in only: pass `-RuntimeRequired` (or Hook `runtime_required=true`) to require it; absence never implies a runtime failure.

`Invoke-ESInteractionCloseout.ps1` is the closeout adapter. It consumes only the normalized evidence result and emits observed status, evidence counts, finding codes, next action, and non-claims; it does not convert assistant self-description into completion. For the low-loss fast path it may consume one real Codex JSONL via `-SessionPath`, or an explicit `-SessionId`; the latter selects the newest readable snapshot whose first metadata record carries that exact ID, never a global latest file, topic match, or handoff candidate. A timestamp tie is rejected. This is only a composition shortcut for the bounded transcript adapter plus the same evaluator, not a second evidence source. `-AllowWrites` and `-AllowRuntime` are explicit scope inputs and are never inferred from transcript text.

When `userCorrections` is non-zero, the closeout includes bounded `correctionEvidence` entries with source line, timestamp, and truncated user text. `correctionState=followup-observed` means an assistant response occurred after the latest correction; `accepted-followup` means a later user message contains an explicit acceptance signal. Acceptance can restore the current status to `aligned`, but it does not prove semantic resolution; `feedbackLoop.resolutionClaim` remains `not-inferred`. An unaccepted correction keeps the result `partial` without turning correction count into a quality score.

`Invoke-ESInteractionCloseoutHook.ps1` is an optional read-only Stop-hook adapter. It consumes only the Hook payload's absolute `transcript_path`, refuses missing/relative/unreadable paths and recursion (`stop_hook_active`), and emits a compact system observation. Hook payloads normally do not carry user authorization scope; when `allow_writes`/`allow_runtime` are absent it reports `missing-explicit-scope` and never classifies observed actions as unauthorized. If the last assistant output lacks an evidence-first closeout marker, it may return one bounded `decision=block` requesting that closeout; it never writes a report, scans for a replacement session, or turns Hook configuration into proof that the host loaded or trusted the Hook.
