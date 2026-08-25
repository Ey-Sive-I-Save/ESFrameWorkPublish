# Three-condition Knowledge effectiveness evaluation

## Authority and scope

This reference defines an advisory, read-only experiment for deciding whether one bounded AIKnowledge entry improves AI decisions. The deterministic validator remains the authority for UTF-8, SourceRef, ContentHash, index, route, and required-read closure. This experiment cannot promote a blocked static result or prove Unity, Runtime, Player, network, or release behavior.

`StaleWhen`: the target Knowledge, its requiredReads, evaluator model/version, task prompt, scoring rubric, external authority version, or isolation mechanism changes.

## When to offer it

Offer the experiment when a user asks whether Knowledge really lowers AI errors, when a static-valid entry may still lack execution value, or when a version-sensitive external contract could materially change the recommendation. Do not offer it during routine structural validation unless the user asks for effectiveness evidence.

The proposal must name the target entry, one realistic failure-bearing scenario, the three conditions, the intended external source class, the expected cost, and the fact that the deterministic validation result remains separate.

## Consent gate

Before execution, obtain current explicit user authorization for every requested side effect:

- creating fresh sessions or isolated evaluator contexts;
- invoking an external or different model;
- network access, including the allowed authority domains and version range;
- writing prompts, raw outputs, scorecards, or receipts to disk.

Without that authorization, the validator may only describe the proposed experiment. It must not silently reuse the current contaminated context, browse the web, start a child evaluator, or persist artifacts.

## Experimental conditions

Fix the scenario, model/version, sampling settings when controllable, output schema, token budget, and scoring rubric before reading any result.

### A. General-model baseline

Give a fresh evaluator only the domain-neutral scenario, constraints, requested deliverable, and output schema. Do not provide ES project identity, target Knowledge, requiredReads, project source, intended answer, earlier findings, or another condition's output.

### B. Knowledge-assisted

Give a second fresh evaluator the identical task plus the exact target Knowledge snapshot and its declared requiredReads. Do not provide condition A's answer or evaluator commentary. Record any stale SourceRef or missing index binding separately rather than repairing it inside the experiment.

### C. Knowledge plus external authority

Give a third fresh evaluator the identical B input plus current, version-matched, user-approved authoritative sources. Prefer official vendor documentation, local versioned package source, standards, or primary specifications. Record URLs or source identities, retrieval time when available, version, and content hashes. Third-party summaries are not a substitute when primary authority exists.

External sources calibrate API or platform facts. They do not override ES-specific ownership, permission, rollback, routing, or evidence rules unless the project authority itself changes.

Before execution, inventory the evidence already present in B. Condition C must add a distinct, current authority surface. If B already contains the same source and version, either select a genuinely independent primary source or mark C `non-discriminating` and stop; do not manufacture a three-way delta.

## Isolation and fairness

- Use three fresh contexts. A coordinator may compare outputs only after all three are frozen.
- Context isolation controls evidence leakage; it does not make the evaluators independent models. Report `single-model isolated contexts` unless a separately authorized and identified external model was actually used.
- Keep the task prompt and output schema byte-equivalent except for the declared evidence pack.
- Do not reveal expected improvements, suspected bugs, canonical answers, or previous outputs to evaluators.
- Use the same model and model version when the goal is to isolate Knowledge impact. If models differ, report the model change as a confounder.
- Do not score verbosity, confidence, or stylistic polish as correctness.
- Preserve raw outputs in conversation by default. Persist them only when the user explicitly authorizes file writes.

If fresh isolation is unavailable, label the result `single-model staged comparison`. If the baseline evaluator has already read the Knowledge, label it `counterfactual baseline`. Neither may be called blind, independent, or a true three-condition experiment.

## Scoring rubric

Score each condition against the same observable dimensions:

1. prerequisite and authority discovery;
2. mechanism or API correctness;
3. identity, ownership, and lifecycle safety;
4. stop and escalation conditions;
5. failure, cancellation, rollback, and partial-success handling;
6. idempotency and concurrent/external-drift handling;
7. postcondition and negative-path verification;
8. permission and scope discipline;
9. evidence-level honesty and unsupported-claim count;
10. actionable next step under insufficient evidence.

For each dimension record the relevant output excerpt or exact omission, severity, and whether the delta came from Knowledge, requiredReads, or external authority. A total score may summarize results but cannot hide a hard safety failure.

## Decision and reporting

Report:

- experiment status: `true-isolated`, `single-model-staged`, `counterfactual`, or `not-run`;
- target Knowledge identity and static validation result;
- fixed scenario, model/version, evidence packs, and confounders;
- per-condition solution summary and score matrix;
- improvements introduced by Knowledge;
- corrections introduced only by external authority;
- regressions, omissions, unsupported claims, and runtime evidence still absent;
- recommendation: retain, revise, split, reroute, mark stale, or block acceptance.

The Knowledge demonstrates practical value only when B materially improves decision correctness or failure prevention over A without increasing unsupported claims. C should primarily calibrate version-sensitive external facts. If C must repair core ES ownership, permission, rollback, or evidence rules absent from B, revise the Knowledge rather than declaring the experiment successful.
