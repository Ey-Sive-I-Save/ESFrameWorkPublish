# Runtime Authorization Contract

Runtime execution is opt-in and must be bound to one current task. A boolean such as `developerAuthorizationRequired=true` is only a declaration; it does not authorize execution.

## Required fields

```json
{
  "schemaVersion": 1,
  "taskId": "...",
  "planHash": "64 lowercase hex characters",
  "commandId": "...",
  "commandHash": "64 lowercase hex characters",
  "taskContractRef": "project-relative path",
  "taskContractHash": "64 lowercase hex characters",
  "targetPaths": ["project-relative path"],
  "issuedAtUtc": "ISO-8601 UTC",
  "expiresAtUtc": "ISO-8601 UTC",
  "timeBudgetSeconds": 1,
  "timeoutSeconds": 1,
  "stopCondition": "non-empty",
  "oneTime": true,
  "developerApproval": "explicit approval record"
}
```

`targetPaths` must stay inside the project root. `expiresAtUtc` must be later than `issuedAtUtc`; an expired or reused one-time authorization is invalid. The authorization grants only the declared runtime operation; it does not grant source, Git, publishing, deletion, network, or unrelated Unity permissions.

The validator also loads `ES/Automation/Contracts/es-runtime-authorization.schema.json` and checks that the schema itself declares the complete required field set. It requires `taskContractRef` to exist inside the project and its SHA-256 to equal `taskContractHash`; `timeoutSeconds` cannot exceed `timeBudgetSeconds`; task, command, stop-condition, approval, and target-path values cannot be empty. `Test-ESRuntimeAuthorization.ps1` is a read-only structural validator: `-Consume` is deliberately rejected until a governed one-time ledger is available, so validation cannot be mistaken for authorization consumption.
