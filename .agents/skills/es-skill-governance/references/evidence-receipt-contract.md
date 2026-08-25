# Evidence receipt contract

Every strict execution receipt is UTF-8 JSON and contains:

- `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, `sourceRefHashes`, `toolId`, `unityVersion`, and `capturedUtc`.
- `status` as one of `passed`, `failed`, `blocked`, or `not-run`; `evidenceLevel` as `S0` through `S6`.
- A project-relative `receiptPath` that identifies the receipt itself.
- A non-empty `sourceRefs` string array whose project-relative files exist and match `sourceRefHashes`.
- A fresh ISO timestamp in `capturedUtc`. A caller-specific `timestampUtc` may coexist but does not replace this strict field.

## Authorization variants

`authorizationKind` is the discriminator. It records which authority context produced the evidence; it is not a permission token and cannot expand the user's request.

| `authorizationKind` | Required binding |
|---|---|
| `managed-aibrain` | `planHash` must be a 64-hex SHA-256 value. |
| `current-user-direct` | `userInstructionHash` must be 64-hex; `authorizedOperations` and `authorizedPaths` must be non-empty JSON string arrays. Every authorized path must be non-empty, project-relative, and remain inside `ProjectRoot` after normalization. A path may identify a not-yet-created target. |
| `read-only` | No action-authorization hash is required or consumed. |

Fields belonging to another variant do not satisfy the selected variant. For backward compatibility only, a receipt without `authorizationKind` is interpreted as `managed-aibrain` when it has a valid 64-hex `planHash`; otherwise the strict receipt is invalid.

## Failure meaning

`sourceRefs` identify authoritative files or captured command output; summaries and chat text are not evidence. Missing, stale, malformed, or contradictory receipts block only the evidence claim they support. A managed AIBrain execution must re-plan when its receipt binding is invalid. Receipt failure never revokes or narrows the current user's direct authorization for bounded project work, and this validator must not be used as an action-authorization gate.
