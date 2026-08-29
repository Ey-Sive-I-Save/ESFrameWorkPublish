# Interaction Session Binding Contract v1

Status: local static contract; production routing and global P0 integration are disabled.

## Responsibility split

```text
TaskContext public input
  -> InteractionBindingRef { bindingId, bindingHash }
  -> InteractionSessionBindingReceipt
  -> AuthorityProof
```

- `InteractionBindingRef` is the only planned public TaskContext input. It deliberately contains exactly two fields and never accepts a raw `interactionSessionId`.
- `InteractionSessionBindingReceipt` binds one Task, frozen GoalRevision, RoutePlan, accepted session record, and transcript prefix snapshot. Its eight top-level fields are the replay boundary, not a copy of authority evidence.
- `AuthorityProof` holds the platform-only evidence used to select that session: independent session-state and transcript trusted-root identities, project-root hash, launch-token hash, session-registry snapshot and selected record, acceptance receipt, process start identity, and ancestor-chain hash.

The semantic validator must receive the current trusted project-root hash, launch-token hash, PID, process start time, and ancestor-chain hash from the caller. A Proof is not accepted merely because its own fields and hash are internally consistent.

The Receipt omits `expiresUtc`: validity is determined by exact scope/hash replay and transcript-prefix drift, not an unrelated wall-clock lease. It uses `taskStartByteOffset` instead of a line number so a future verifier can stream a large JSONL transcript without loading it into memory.

## Hashing

- `proofHash` is SHA-256 over canonical UTF-8 JSON of AuthorityProof excluding `proofHash`.
- `bindingHash` is SHA-256 over canonical UTF-8 JSON of the Binding Receipt excluding `bindingHash`.
- Canonical object keys use ordinal case-sensitive order; array order is preserved; hashes are lowercase hexadecimal.
- The public reference must match the Receipt `bindingId + bindingHash`, and the Receipt must bind the exact `proofHash`.
- Hashes provide deterministic integrity and replay identity; they are not signatures. Authority exists only when the platform verifier rereads the trusted registry, acceptance receipt, current process identity, and transcript prefix. A caller-authored object with recomputed hashes is still non-authoritative.
- `interaction-authority-canonical-v1` fixes source normalization: file hashes cover raw bytes; transcript `prefixHash` covers exactly the first `snapshotLength` bytes; launch-token hash covers the exact UTF-8 token; project-root hash covers the resolved Windows path after separator normalization, trailing-separator removal, and invariant lowercase; process start times use UTC; ancestor-chain hash covers the ordered `{pid, processStartUtc, executablePathHash}` sequence.
- `taskStartByteOffset` must be within the frozen prefix and, in the later streaming probe, must resolve to the first byte of a complete JSONL record.
- `sessions.json` and rollout JSONL do not share one filesystem root. Registry and acceptance paths resolve only below `es-codex-session-state-v2`; transcript paths resolve only below `codex-home-sessions-v1`.
- A valid create-only acceptance receipt may be newer than mutable Registry lifecycle projection. This is recorded as `receipt-ahead-of-registry` and does not invalidate the accepted task-scoped binding unless the Registry has a terminal contradiction such as `Closed` or `LaunchFailed`.

## Scoped effects

| reasonCode | object / field | profile | scope | effect | outcome | recovery |
|---|---|---|---|---|---|---|
| `INTERACTION_BINDING.EVIDENCE_MISSING` | Binding artifacts | `interaction-observation` | `task-object` | `claim-cap` | `evidence-pending` | issue a platform binding |
| `INTERACTION_BINDING.CONTRACT_INVALID` | invalid artifact / schema | `interaction-observation` | `task-object` | `hard-block` | reject this binding request | correct only the artifact |
| `INTERACTION_BINDING.PROOF_HASH_MISMATCH` | AuthorityProof / `proofHash` | `interaction-observation` | `task-object` | `hard-block` | reject this binding request | reissue proof |
| `INTERACTION_BINDING.RECEIPT_HASH_MISMATCH` | Receipt / `bindingHash` | `interaction-observation` | `task-object` | `hard-block` | reject this binding request | reissue receipt and reference |
| `INTERACTION_BINDING.SCOPE_MISMATCH` | Receipt / `scope` | `interaction-observation` | `task-object` | `hard-block` | reject this binding request | bind current Task/Goal/Route |
| `INTERACTION_BINDING.SESSION_MISMATCH` | Receipt / `session` | `interaction-observation` | `task-object` | `hard-block` | reject this binding request | resolve one accepted session |
| `INTERACTION_BINDING.AUTHORITY_CONTEXT_MISMATCH` | AuthorityProof / `authority` or `process` | `interaction-observation` | `task-object` | `hard-block` | reject this binding request | resolve from the current project/process |

No reason in this contract creates a project-global block. Missing binding evidence caps only interaction-derived claims such as `humanCorrectionRate`; it does not block the core task or completion evidence from unrelated registered verifiers.

## Not integrated

- `New-ESTaskContextTask` still accepts its legacy raw session compatibility field; this contract does not make that field authoritative.
- No production TaskContext route imports this module.
- No global P0 consumes these results.
- Actual authority-root traversal, reparse-point checks, streaming transcript prefix hashing, PID reuse checks, registry race handling, and binding artifact persistence remain for the later read-only production probe.
- Planned persistence is create-only under the platform task store, for example `<TaskStore>/InteractionBindings/<bindingId>/`; the two-field reference never accepts a caller-selected path.
