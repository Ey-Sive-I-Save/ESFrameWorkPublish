# Evidence receipt contract

Authority: ES Skill evidence contract specialized for Feishu external reads.
Scope: machine-readable receipts consumed by this Skill.
StaleWhen: shared strict receipt schema, TaskContract identity or Feishu evidence fields change.
Evidence: validation by the shared strict evidence validator plus source/hash reconciliation.

Every receipt must contain `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, and `timestampUtc`. Status is one of `passed`, `failed`, `blocked`, or `not-run`.

For any Feishu run, also require `planHash`, `commandId`, `taskId`, `taskVersion`, `governanceHash`, `dryRun`, `operation`, `runId`, `invocationHash`, `inputManifestHash`, `outputHashes`, `evidenceScope`, `classification`, `sanitizerVersion`, `networkCalled`, `exitCode`, `startedAtUtc`, `completedAtUtc`, and `unresolvedGaps` where applicable. A live run additionally requires `runtimeAuthorizationRef`, `credentialSourceType`, `tenantHash`, and `spacePolicyHash`; a DryRun records these fields as `not-applicable` or omits optional fields according to the shared schema rather than inventing Runtime identity.

Credential values, raw Authorization material and secret-bearing excerpts invalidate the receipt. `sourceRefs` must point to authoritative files, sanitized managed outputs or command evidence; summaries and chat text are not evidence. Missing, stale, contradictory or non-terminal receipts block acceptance and force a new AIBrain plan.
