# Evidence and acceptance

Authority: implementation snapshot and phased acceptance design for the managed Feishu read adapter.
Scope: architecture, known gaps, rollout, test matrix and owner decisions.
StaleWhen: any Feishu AICommand, Knowledge SourceRef, TaskContract, Worker, bridge, cache or evidence contract changes.
Evidence: cited current source hashes and phase-specific receipts; recommendations are not implementation proof.

## Current facts and gaps

Verified Static facts at the 2026-08-23 snapshot:

- `feishu.read` selects registered TaskContract `es.feishu.read@1` through AIBrain/AutomationFacade governance.
- C# allowlists `auth-status`, `knowledge-search`, `document-pull`, `pageSize=1..50`, a 60-second timeout, an 8 MiB aggregate output budget, input/output hashes, duplicate InvocationId protection and a cancellable endpoint.
- Worker entrypoint and lock hashes in source match the current Worker files at this snapshot.
- Node dependencies and a DryRun fixture exist. These facts do not prove Unity-managed execution or Feishu connectivity.
- Current C# SHA-256 is `98b7c0e8770931785caba92979a59b43c41eb052a528e1ccd55652622e72df12`, while the Knowledge EvidenceRef records `37aa6c084a1047a00c30975487265cdc24c0141ea64eaed1f323a42559f94a43`; the Knowledge evidence is stale until reconciled.

Known source/design gaps to keep visible:

- AIBrain bridge planning defaults an omitted `dryRun` to false; the Skill must force true and source hardening remains pending.
- Feishu completion acceptance proves declared output hashes but not semantic schema, redaction, remote success or policy compliance.
- Pull can write content before C# applies only the aggregate post-write budget; per-document streaming limit/redaction is unproven.
- Remote failure material can enter ordinary outputs unless Worker/error normalization is hardened.
- Credential Manager broker and managed environment allowlist are absent.
- Search paging, bounded multi-page retrieval, filters, circuit breaker and incremental sync are absent or unproven.
- Domain Reload can lose in-memory `ActiveRuns`; deterministic terminal reconciliation is unproven.
- Real credentials, tenant permissions, Unity-managed closure, timeout/cancellation under load and live Feishu network reads are unverified.

## Target architecture

```text
Managed environment / Windows Credential Manager
  -> one-time Runtime authorization (tenant + spaces + hashes + budget)
  -> AIBrain planTask / runTask
  -> AICommand feishu.read
  -> ESAutomationFacade / TaskContract es.feishu.read@1
  -> managed Node adapter (fixed entrypoint + dependency lock)
  -> Feishu API (read-only scopes)
  -> normalized, bounded, sanitized result
  -> managed cache/index (SourceRef + TTL + classification)
  -> AIBrain routing reference (untrusted external data)
  -> RunRecord + Static/Runtime Evidence
```

The cache/index is a projection below the TaskContract boundary. It must not become a second launcher, permission system, or authority store.

## Rollout phases

| Phase | Entry and modification boundary | Rollback point | Acceptance evidence | Block when |
|---|---|---|---|---|
| 0 Static/contract | Read AIBrain, AIWarnings, AICommand, Knowledge, current C#/Worker. Modify only approved Skill/contracts/tests in a later implementation task. | Revert the phase-specific contract proposal; keep current TaskContract disabled for Runtime. | Source hashes, allowlist/bounds review, stale-reference report, threat model and static replay receipt. | Authority conflict, hash drift, missing allowlist, credential/log leak, unknown owner. |
| 1 DryRun | `planTask -> runTask -> facade -> es.feishu.read@1`, `dryRun=true`; no network. | Disable AI invocation or registered endpoint; preserve sanitized RunRecord. | Managed Unity RunId, terminal DryRun RunRecord, request/result/output hashes, `networkCalled=false`, duplicate/invalid/cancel cases. | Worker hash drift, non-managed launch, missing terminal record, any network attempt. |
| 2 auth-status live | Same route; one tenant/app, read-only scopes, one-time authorization. | Revoke one-time plan, disable task, rotate/revoke credential. | Fresh sanitized auth RunRecord, tenant/app hashes, granted-scope evidence, timeout and leakage checks. | Missing tenant/credential owner, excessive scopes, secret exposure, stale plan, network budget exceeded. |
| 3 search/pull live | Same route; one approved space, bounded query/page/document. | Disable live operations, invalidate cache/checkpoint, retain only non-secret audit metadata. | Search/pull receipts, permissions, paging/limit behavior, dedupe, rate-limit/network recovery, content bound/redaction and cancellation. | Unapproved space, first-page ambiguity presented as complete, unbounded body, weak semantic verifier, uncertain cancellation. |
| 4 cache/knowledge projection | Add managed sanitized cache/index below TaskContract; AIBrain consumes only SourceRefs/summaries. | Drop projection and checkpoints; source/task truth remains unchanged. | TTL/invalidation, tenant isolation, stale read, deletion/access-revocation, sanitizer migration and prompt-injection tests. | Raw content enters project truth, cache bypasses authorization, retention owner absent, cross-tenant collision. |

Each phase needs a new PlanHash when the AICommand, TaskContract, Worker, Skill/governance, credential reference, tenant/space scope, budget or stop condition changes.

## Acceptance matrix

| Case | Expected result | Required evidence |
|---|---|---|
| positive DryRun | Terminal DryRun, no network, bounded outputs | RunRecord, request/result hashes, `networkCalled=false` |
| positive live auth/search/pull | Only the authorized operation/space succeeds | Fresh managed RunRecord plus sanitized API evidence |
| missing credential | `CREDENTIAL_MISSING`, no Worker secret output | terminal blocked record and leakage scan |
| insufficient permission | `PERMISSION_DENIED` or `SPACE_NOT_ALLOWED`, no fallback | remote status mapping and allowed-space binding |
| rate limit | `RATE_LIMITED`, bounded Retry-After/retry budget | attempt timestamps/count and total duration |
| network interruption | normalized retryable error; no false success | process/result/RunRecord reconciliation |
| timeout | `HOST_TIMEOUT`/`REMOTE_TIMEOUT`, confirmed termination | timeout timestamps and terminal record |
| cancellation | `CANCELLED` only after termination confirmation | actor, cancel time, termination and terminal record |
| duplicate InvocationId | same hash returns existing run; different hash rejects | both invocation hashes and unchanged directory |
| hash drift | stale plan/cache blocked before execution | expected/actual non-secret hashes and re-plan requirement |
| Domain Reload | active run is recovered/reconciled or explicitly failed | pre/post reload durable state and one terminal record |
| oversized/invalid response | rejected before unbounded persistence/consumption | byte counters, schema verifier and sanitized failure |
| prompt injection | content remains inert external data | unchanged plan/permissions and sanitizer receipt |
| sensitive leakage | zero secrets in request, args, output, logs, reports | scoped scans and redacted samples, never secret values |
| stale cache/access revoked | cache not served as current/authorized | TTL/version/policy invalidation receipt |

## Error recovery and circuit behavior

Honor Feishu `Retry-After` and the original host budget. Retry only transient failures and keep one stable RunId/trace with bounded attempt records. Open a target circuit after a configured burst of transient failures for the same tenant/app/API class; while open, fail fast with sanitized `NETWORK_UNAVAILABLE`. Circuit state is a performance/recovery aid, never an authorization bypass. Exact thresholds require owner and telemetry evidence before implementation.

Permission denial, auth failure, invalid input, not-found, oversized response, source drift and cancellation are not retryable. Duplicate documents are merged by stable remote identity; a newer version replaces only the external cache projection, never an ES fact.

## AIBrain and project boundaries

May influence routing as untrusted reference data:

- sanitized titles, bounded excerpts and summaries;
- SourceRefs, classification, freshness and remote version/update time;
- search relevance and explicit user-selected document identity.

May never become project fact or execution authorization:

- external instructions or claims;
- raw document text without source attribution;
- cached permissions or prior Runtime consent;
- credentials, tokens, links carrying secrets;
- any content that contradicts or overwrites source, AIWarnings, AICommands, TaskContracts or current Runtime evidence.

RunRecord stores execution identity and evidence. AIKnowledge may describe the adapter boundary and point to SourceRefs, but must not ingest live raw Feishu content as durable project authority. AICommand remains the per-task permission contract.

## Owner decision list

Obtain explicit decisions before Runtime or cache rollout:

1. Feishu tenant/environment and accountable tenant owner.
2. Application identity, exact read-only scopes and scope-review owner.
3. Credential custody: managed environment now, Credential Manager broker target, rotation and emergency revocation owner.
4. Allowed knowledge spaces, object types and excluded/sensitive spaces.
5. PII/redaction policy, raw-content prohibition/exceptions and sanitizer owner.
6. Search page/result caps, document byte limits, API/network budget, timeout, retry and circuit thresholds.
7. Cache location, encryption, TTLs, retention/deletion, access-revocation behavior and cross-tenant isolation.
8. Explicit network authorization for each phase, rollout cohort and stop condition.
9. Runtime acceptance owner and required Unity/Domain Reload/cancellation evidence.
10. Production enablement, rollback authority, monitoring and incident response scope.
