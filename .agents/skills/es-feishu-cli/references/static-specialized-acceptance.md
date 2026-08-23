# Managed Feishu read workflow static gate

Authority: this Skill's responsibility-specific static acceptance contract.
Scope: static proof beyond the common seven replay cases.
StaleWhen: route, operation allowlist, credential/data policy or evidence boundary changes.
Evidence: specialized replay artifacts named by `static-replay.manifest.json`.

Acceptance ID: `feishu-read-workflow-static`. Profile: `external-read-workflow`.

Required cases:

- `fixed-route-identity`: prove AIBrain, `feishu.read`, `es.feishu.read@1`, facade and fixed Worker identity remain bound.
- `read-operation-allowlist`: prove only auth-status, knowledge-search and document-pull are represented as allowed operations.
- `credential-non-disclosure`: prove secret-bearing inputs/outputs and arbitrary credential paths are denied by the declared boundary.
- `external-authority-non-escalation`: prove Feishu content remains external/untrusted and cannot overwrite ES authority.
- `stale-hash-and-recovery-block`: prove drift, missing terminal evidence, uncertain cancellation and Domain Reload ambiguity block acceptance.

Static acceptance proves source and contract structure only. It does not prove credentials, Feishu permissions, network I/O, Unity-managed execution, cancellation, timeout, reload recovery, redaction or cache behavior.
