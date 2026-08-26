# Feishu read contract

Authority: target contract constrained by the current AICommand and `es.feishu.read@1` source.
Scope: first-phase read-only inputs, normalized outputs, bounds and failure semantics.
StaleWhen: AICommand, TaskContract, Worker protocol, Feishu SDK/API or response schema changes.
Evidence: current source plus fresh Static or Runtime receipts; this document alone is not Runtime proof.

## Contract status

The registered source accepts only `operation`, `query`, `spaceId`, `documentId` and `pageSize`; `dryRun` and `invocationId` are invocation-level fields. Unknown fields are rejected. Search and pull now require a bounded `spaceId`; Live execution additionally checks it against `ES_FEISHU_ALLOWED_SPACE_IDS` and binds `ES_FEISHU_TENANT_ID` by hash. Multi-page retrieval, filters and retries remain target-only gaps.

Common identity is fixed to `commandId=feishu.read`, `taskId=es.feishu.read`, `taskVersion=1`. Unknown fields, credential material, executable paths and command-line fragments are invalid input.

## auth-status

Input:

| Field | Rule |
|---|---|
| `operation` | Literal `auth-status`. |
| `dryRun` | Defaults true at the Skill boundary; false requires Runtime authorization. |
| `invocationId` | N-format GUID when supplied; reuse requires the identical normalized invocation hash. |

Normalized output:

| Field | Rule |
|---|---|
| `authenticated` | Boolean only when a real API exchange completed; DryRun uses `null`/unknown. |
| `tenantIdHash` | Stable non-reversible tenant identifier; never return tenant secrets. |
| `appIdentityHash` | Non-secret application identity hash. |
| `grantedScopes` | Bounded, sorted scope names if the API supports inspection. |
| `checkedAtUtc`, `expiresAtUtc` | UTC timestamps; omit unavailable expiry rather than guessing. |

`auth-status` is read-only and idempotent for the same authorization context. Cache target: 60 seconds, subject to owner confirmation.

## knowledge-search

Input:

| Field | Rule |
|---|---|
| `operation` | Literal `knowledge-search`. |
| `query` | Required, trimmed, 1-512 UTF-8 characters after normalization. |
| `spaceId` | Required bounded identifier; Live must match exactly one identifier from the managed authorization allowlist. |
| `pageSize` | Integer 1-50; default 20. |
| `pageToken` | Opaque server token; target contract only, never parse or synthesize. |
| `filters` | Optional allowlisted document type/update-time filters; target contract only. |
| `maxPages`, `maxResults` | Hard caps 5 pages and 100 unique results per invocation. |

Normalized item fields: `SourceRef`, `spaceIdHash`, `objectTokenHash`, `objectType`, sanitized `title`, bounded `excerpt`, `updatedAtUtc`, `version`, `urlRefHash`, `classification`, `retrievedAtUtc` and `contentHash` when content is present.

Return `items`, `nextPageToken`, `hasMore`, `pagesRead`, `truncated`, and `deduplicatedCount`. Deduplicate by `(spaceId, objectToken, objectType)` and keep the newest version/update time. Stable-sort by remote relevance when provided, then update time descending, then SourceRef.

The current Worker fetches one implemented page, hashes the query and opaque next-page token, persists only normalized item fields, and marks `truncated=true` when the server reports more data. Do not emulate unsupported paging outside the registered Worker or claim the 5-page target is available.

## document-pull

Input:

| Field | Rule |
|---|---|
| `operation` | Literal `document-pull`. |
| `sourceRef` | Target requirement: validated search/authz SourceRef bound to tenant, space, object type and token. |
| `documentId` | Current adapter field; must resolve to the same allowlisted identity as SourceRef before Runtime use. |
| `spaceId` | Required and checked against the managed Live allowlist. The Worker also resolves the wiki node and verifies the document belongs to the same space before pulling content. |
| `maxContentBytes` | Current fixed limit is 256 KiB after sanitization; oversized normalized text is explicitly marked truncated. A caller-selectable limit remains target-only. |

Normalized output fields: `SourceRef`, sanitized `title`, bounded `content` or summary, `objectType`, `version`, `updatedAtUtc`, `retrievedAtUtc`, `classification`, `sanitizerVersion`, `contentHash`, `truncated`, and attachment/link metadata without downloading unapproved objects.

The Worker strips unnecessary controls, redacts credential patterns and bounds content before durable persistence. C# independently enforces a 512 KiB normalized-data limit, a 1 MiB aggregate output budget, output hashes and the external-evidence receipt. The SDK still returns the remote body in memory rather than through a streaming sanitizer, so bounded-memory streaming remains unimplemented.

## Time, retry, cancellation and idempotency

- Host total timeout: 60 seconds. Target per-request timeout: at most 15 seconds.
- Target retry: at most two transient retries for `429`, `5xx`, connection reset or request timeout, bounded by the original 60-second budget and `Retry-After`. Do not retry auth, permission, invalid input, not-found or oversized response errors.
- Current TaskContract declares `supportsRetry=false` and `maxRetryCount=0`; keep Runtime retries disabled until the registered implementation changes.
- Cancellation uses the registered endpoint. Report `CANCELLED` only after process-tree termination and a terminal RunRecord are confirmed.
- Duplicate InvocationId with the same normalized input returns the existing run; a different input hash returns `INVALID_INPUT`/conflict and never overwrites the directory.
- Side-effect-free remote reads are logically idempotent, but returned freshness may differ across invocations.

## Normalized errors

Use one of these stable codes and keep remote details sanitized:

`INVALID_INPUT`, `NETWORK_NOT_AUTHORIZED`, `CREDENTIAL_MISSING`, `AUTH_FAILED`, `SPACE_NOT_ALLOWED`, `PERMISSION_DENIED`, `NOT_FOUND`, `RATE_LIMITED`, `NETWORK_UNAVAILABLE`, `REMOTE_TIMEOUT`, `RESPONSE_TOO_LARGE`, `RESPONSE_INVALID`, `CANCELLED`, `HOST_TIMEOUT`, `SOURCE_DRIFT`, `EVIDENCE_INVALID`.

Every error includes `code`, sanitized `message`, `retryable`, `operation`, `runId`, `occurredAtUtc` and optional bounded `retryAfterSeconds`. Never place access tokens, app secrets, Authorization headers, cookies, raw remote bodies or credential source contents in the message.
