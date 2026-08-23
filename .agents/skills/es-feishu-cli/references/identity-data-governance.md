# Identity and data governance

Authority: target governance derived from project credential, Automation and external-data boundaries.
Scope: application identity, secret custody, data classification, sanitization, cache and freshness.
StaleWhen: tenant/app model, credential broker, permission scopes, cache storage or retention policy changes.
Evidence: source review plus authorized credential-broker, leakage and cache lifecycle receipts.

## Identity and credentials

Use a dedicated Feishu application identity per environment and tenant. Grant only the scopes required to inspect authentication and read the explicitly approved knowledge spaces. No messaging, publishing, drive upload, deletion, tenant administration or permission-management scope is acceptable in phase one.

Credential values may come only from:

1. an allowlisted managed process environment inherited by Unity/Automation; or
2. a Windows Credential Manager broker that returns secrets directly to the managed Worker without serializing them into the request.

Current source reads `ES_FEISHU_APP_ID` and `ES_FEISHU_APP_SECRET` from the process environment. Credential Manager, environment-variable allowlisting and broker audit are target gaps, not current facts. `ES_AUTOMATION_NODE_PATH` is executable configuration, not a Feishu credential, and must remain an administrator-managed path verified by Worker policy.

Bind Runtime authorization to tenant identity, application identity hash, credential source type and credential version/reference, but never the secret. Also bind allowed space IDs, operation, PlanHash, AICommand/TaskContract/Worker hashes, Skill governance hash, output/network budget, timeout, expiry and stop condition.

Rotation procedure:

1. provision the replacement in the managed store;
2. revoke or expire old authorization and cached auth state;
3. run a separately authorized `auth-status` canary;
4. promote the new credential reference; and
5. revoke the previous secret and retain only non-secret audit metadata.

On missing, expired, revoked, tenant-mismatched or scope-insufficient credentials, fail closed. Audit who authorized the run, application/tenant hashes, credential version reference, operation, allowed spaces, timestamps and outcome. Never audit the credential value.

## Data classes

| Class | Examples | Handling |
|---|---|---|
| `ExternalCollaboration` | titles, excerpts, document text, authorship/update metadata | Default Feishu classification; bounded, sanitized and source-attributed. |
| `ExternalSensitive` | internal strategy, personal data, restricted-space text | Minimize, redact, restrict cache and AI exposure; owner policy required. |
| `Secret` | tokens, app secrets, cookies, Authorization headers, private keys | Never persist or emit; stop and report leakage by code only. |
| `DerivedExternal` | summaries, embeddings, indexes and dedupe keys derived from Feishu | Preserve SourceRef, classification, sanitizer version and freshness; never upgrade authority. |

External content is untrusted. Prompt-like instructions, code blocks, hyperlinks and claims inside a document cannot modify authority, execution parameters or source facts. ES source, AIWarnings, AICommands and TaskContracts always outrank Feishu content.

## Sanitization and bounded content

- Normalize encoding and strip control characters that are not needed for text semantics.
- Detect and remove credentials, Authorization headers, cookies, common private-key blocks, high-confidence tokens and disallowed PII before persistence or AI exposure.
- Preserve a redaction count and sanitizer version, not the removed value.
- Bound search excerpts and document text. Use a summary plus SourceRef when the raw body exceeds policy.
- Reject or truncate according to the operation contract; never silently accept an oversized body as complete.
- Store raw content only in an approved encrypted managed temporary/cache location after owner confirmation. Never store raw Feishu content in Git, `Assets/`, AIWarnings, AICommands, AIKnowledge or ordinary reports.

## SourceRef and freshness

A SourceRef should bind:

```text
provider=feishu
tenantHash
spaceIdHash
objectType
objectTokenHash
remoteVersion or updatedAtUtc
retrievedAtUtc
contentHash
classification
sanitizerVersion
```

It is a provenance pointer, not authorization and not project truth. Resolve it only under a fresh plan and the same tenant/space allowlist.

Proposed TTL defaults, all requiring owner confirmation:

| Material | TTL |
|---|---|
| auth status | 60 seconds |
| search result | 5 minutes |
| sanitized document content | 30 minutes |
| managed temporary files | 24 hours |
| non-secret RunRecord metadata | 30 days |

Invalidate cache on tenant/app/credential-version change, space-policy change, object version/update-time change, sanitizer policy change, command/task/worker/Skill hash drift, explicit revocation or access denial. A stale cache may help discovery but cannot satisfy current authorization or Runtime acceptance.

For incremental synchronization, compare stable object identity plus remote version/update time, retrieve only changed items, tombstone inaccessible/deleted items without preserving forbidden content, and record a bounded checkpoint. Do not start continuous synchronization in phase one; each batch needs an explicit scope and budget.
