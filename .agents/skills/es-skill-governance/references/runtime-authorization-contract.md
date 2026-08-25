# Runtime Authorization Contract

Runtime execution is opt-in and must be bound to one current user request. The user's explicit Runtime instruction is the authorization source; a boolean such as `developerAuthorizationRequired=true` is only a declaration. The manifest below is required for the managed AIBrain/Worker lane, not as a second approval for a direct user-directed Runtime action.

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

`targetPaths` must stay inside the project root. `expiresAtUtc` must be later than `issuedAtUtc`; an expired or reused managed execution token is invalid. The current user instruction grants only the declared Runtime operation; it does not imply source, Git, publishing, deletion, network, or unrelated Unity actions.

For the managed lane, the validator also loads `ES/Automation/Contracts/es-runtime-authorization.schema.json` and checks the complete field set, task contract hash, time relationships and target containment. `Test-ESRuntimeAuthorization.ps1` is a read-only structural validator: `-Consume` is deliberately rejected until a governed one-time ledger is available. A direct user-directed Runtime tool may use a host-native receipt instead; it must still bind the current request, exact action, target, budget, timeout and stop condition.
