# External prompt execution contract

This Skill writes an external ESAITest prompt envelope. It is not a project-file write and must never be inferred from MCP visibility.

## Required authorization binding

Every authorized invocation binds:

- `TaskContractId` and AIBrain `PlanHash`;
- the exact `PersistentDataPath` target and its approved owner;
- priority, TTL, wait budget and stop condition;
- the matching AICommand id/hash and one-time authorization receipt.

The target must be an approved ESAITest inbox. Absolute paths outside the approved target, path traversal, reparse points, or implicit user-profile expansion are denied.

## Evidence states

`queued` means the envelope was atomically written. `picked_up` means the runtime reported pickup. `consumed` requires a matching receipt for the same `promptId`. `expired` means the TTL elapsed without consumption. None of these states may be represented as RuntimeAccepted without fresh runtime evidence.

## Static replay cases

The sender contract must cover normal enqueue, invalid message/priority, TTL and wait-budget limits, target-boundary rejection, duplicate identity, atomic interruption cleanup and deterministic envelope fields. Runtime pickup remains a separate authorized acceptance case.
