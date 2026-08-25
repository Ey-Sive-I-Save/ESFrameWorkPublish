# Managed Feishu collaboration workflow static gate

Authority: this Skill's responsibility-specific static acceptance contract.
Scope: static proof beyond the common seven replay cases.
StaleWhen: route, operation allowlist, credential/data policy or evidence boundary changes.
Evidence: specialized replay artifacts named by `static-replay.manifest.json`.

Acceptance ID: `feishu-read-workflow-static`. Profile: `external-collaboration-workflow`.

Required cases:

- `fixed-route-identity`: prove AIBrain, exact Feishu AICommand, matching TaskContract, facade and fixed Worker identity remain bound.
- `read-operation-allowlist`: prove only auth-status, knowledge-search and document-pull are represented as allowed operations.
- `task-operation-allowlist`: prove monitor, dispatch and transition operations remain separated and delete/publish/upload/admin operations are absent.
- `identity-ownership-and-consent`: prove local roles are AppId-partitioned, owner-controlled, Git-ignored and independently consent to assignment/message resolution.
- `single-recipient-message-boundary`: prove messaging is one claimed role, plain text, fixed `im.v1.message.create` and server UUID only.
- `dryrun-before-external-write`: prove live dispatch/transition/message and local identity mutation require a fresh same-input accepted DryRun receipt.
- `command-task-exact-binding`: prove read, monitor, mutation, identity and message commands cannot authorize a different Feishu TaskContract.
- `credential-non-disclosure`: prove secret-bearing inputs/outputs and arbitrary credential paths are denied by the declared boundary.
- `external-authority-non-escalation`: prove Feishu content remains external/untrusted and cannot overwrite ES authority.
- `stale-hash-and-recovery-block`: prove drift, missing terminal evidence, uncertain cancellation and Domain Reload ambiguity block acceptance.

Static acceptance proves source and contract structure only. It does not prove credentials, Feishu permissions, network I/O, Unity-managed execution, cancellation, timeout, reload recovery, redaction or cache behavior.
