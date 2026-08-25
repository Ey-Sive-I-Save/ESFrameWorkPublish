# External research and AI failure-surface policy

## Authority, scope, and stale conditions

This policy governs research effort for one bounded `detailed-entry`. Current project source, configuration, tests, AIWarnings, and real receipts remain authoritative for ES facts. External sources calibrate vendor, platform, language, package, protocol, and version contracts; they do not grant project permissions or prove Runtime behavior.

`StaleWhen`: the target domain, project source, vendor/product version, official page, local package source, retrieval hash, failure evidence, scoring rubric, or linked Knowledge Validator protocol changes.

## Quality weights

Weights measure curation attention and diagnostic quality, not fact authority. A hard blocker always overrides the score.

| Dimension | Weight | Required evidence |
|---|---:|---|
| AI failure prevention | 40 | material failure modes with trigger, prevention, recovery, and missing evidence |
| External primary-source calibration | 25 | current version-matched official docs/source, or an explicit not-applicable/deferred decision |
| Current project grounding | 25 | current source, AIWarnings, tests, commands, Skills, and receipts as applicable |
| Routing and execution value | 10 | precise routeKeys, requiredReads, decisions, stop rules, checklist, and non-claims |

An external calibration dimension is `not-applicable` only when the entry contains no external or version-sensitive contract. Record the reason and redistribute 10 points to project grounding and 15 points to failure prevention. Missing authorization or unavailable official evidence is `Deferred`, not `not-applicable` and not a fabricated pass.

Suggested interpretation: `>=80` is ready for independent validation, `60-79` requires revision, `<60` is not ready. The score cannot override missing SourceRefs, hash drift, unsupported evidence, permission expansion, route/index failure, or an unhandled irreversible safety risk.

## External authority discovery

External research is applicable when any of these is true:

- API, engine, package, language, platform, protocol, serialization, compiler, or vendor behavior is version-sensitive;
- project sources describe usage but do not prove the underlying external contract;
- local and remembered behavior conflict;
- an official Warning, Note, return-value contract, migration notice, Known Issue, or version change could alter the decision;
- the requested Knowledge will tell another AI which external API or platform action to take.

Prefer, in order: installed versioned package/source; official vendor documentation or source; primary standard/specification; official issue tracker or release notes. Use third-party material only to discover a primary source or when no primary source exists, and label its lower authority.

Before network access, propose the exact purpose, allowed official domains, product/version, maximum page count, timeout, and stop condition. Wait for current explicit user authorization. A prior permission to edit Knowledge is not blanket network permission.

For every retrieved source record URL, final URL after redirects, product/version, retrieval time when available, relevant quoted contract, and SHA-256 of the retrieved content. Reject versionless pages when the claim is version-sensitive unless the page explicitly governs the target version.

## Long-lived provenance boundary

The Knowledge validator accepts project-contained `SourceRefs`; a live URL alone is not a durable `SourceRef`.

- If the user authorizes a source snapshot, store only the bounded facts needed for the Knowledge, plus URL, product/version, retrieval time, content hash, quotation/context, and stale conditions. Do not copy an entire website or unclear-license content.
- Point the Knowledge `SourceRefs` to that project-local snapshot and hash it normally.
- If snapshot persistence is not authorized, cite the web source only in the current response. Mark the long-lived claim `external-source-not-bound` and `Deferred`, or omit it from verified facts.
- Network success proves retrieval only. It does not prove Unity, package import, Editor interaction, Player, performance, or release behavior.

## Failure-surface mining

Build the failure-surface matrix before drafting the entry. Inspect only the bounded target domain and its required reads.

Mine these surfaces:

1. thrown exceptions, error returns, null/empty results, ignored return values, and partial writes;
2. cancellation, timeout, interruption, retry, duplicate execution, and re-entry;
3. staging, commit, rollback, rollback failure, residue, and external drift;
4. stable identity, GUID/key/owner, lifecycle, disposal, Undo/Dirty/Save, reload, and serialization boundaries;
5. concurrency, ordering, callbacks, event subscription, domain reload, and state restoration;
6. path, permission, command/TaskContract, secret, destructive, network, and external-process boundaries;
7. static-versus-Runtime evidence promotion, file/button/test existence, stale snapshots, and unsupported success claims;
8. negative tests, malformed fixtures, bug regressions, official Warning/Note/Returns/Known Issue, and version migration notes.

Each material failure mode must state:

```text
failureId
severity: irreversible | identity/authority | lifecycle/partial | recoverable | advisory
erroneousBehavior
triggerAndSymptom
rootCause
preventionCheck
correctAction
recoveryAction
evidencePresent
evidenceMissing
sourceRefs
```

Prioritize severity over count: irreversible data loss, identity drift, permission expansion, and evidence overclaim receive factor 3; lifecycle, partial success, rollback, and concurrency receive factor 2; recoverable convenience mistakes receive factor 1. A detailed entry should normally contain at least three materially distinct failure modes. If evidence supports fewer, state that explicitly; never manufacture generic cautions to satisfy a quota.

## Completion and handoff

Before calling a candidate ready for validation:

- all verified facts have current project SourceRefs or an authorized project-local external-source snapshot;
- applicable official-source calibration is complete or explicitly Deferred;
- the failure-surface matrix covers the highest-severity plausible paths;
- every failure mode produces an executable prevention or recovery check;
- routing, requiredReads, stop rules, non-claims, and evidence boundaries are explicit;
- the weighted rubric is reported with hard blockers separately.

Then proactively offer the Knowledge Validator's three-condition comparison. The Creator must not score its own output as proof of practical effectiveness. The Validator compares an isolated general-model baseline, Knowledge-assisted condition, and Knowledge plus distinct external authority after the user authorizes the required contexts and network scope.
