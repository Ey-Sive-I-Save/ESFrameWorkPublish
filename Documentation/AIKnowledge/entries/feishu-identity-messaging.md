# Feishu 本地身份与单人消息边界

`KnowledgeId`: `es.feishu.identity-messaging.v1`
`Authority`: `Derived`
`RouteKeys`: `feishu, lark, identity-claim, bot-ownership, onboarding, message-send, notification, task-dispatch, external-write, dry-run`
`EvidenceLevel`: `S1`
`StaleWhen`: `本地身份 AICommand/TaskContract/Schema/Store、Feishu IM SDK/API、消息 Worker、AIBrain 绑定或 Runtime 证据变化`

## SourceRefs

- `Assets/Plugins/ES/AICommands/Feishu本地角色与机器人认领_AI命令.md` (`f3b85d270e46c2c2fd36afe32e37a260aa2e080a29061fad36855fd11f9ed54f`)
- `Assets/Plugins/ES/AICommands/Feishu单人文本消息发送_AI命令.md` (`3d33177c2da8d2b04b32d098f91c58b8d5ea213c7addec65072aee1b9978cc40`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESFeishuIdentityAutomation.cs` (`90e486d7de1d26228353b47ebf185718ff4300f5eb8cdadab24ba3b9ac82d948`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESFeishuTaskAutomation.cs` (`fdc0b137ccf8211ba482147979e9cc3d9e5831bae14f7a8c07d26caaf15ec25f`)
- `ES/Automation/Workers/DotNet/FeishuIdentity/worker-manifest.json` (`ca2b700505014eb6817c9b6b42d1b479cc28e6190713a99fe9dd911b1cc08d73`)
- `ES/Automation/Workers/Node/Feishu/task-worker.js` (`4dfd148e10fdc7e0bcd7167ec9f650f9e750d504bd0cf0764fc069401b6b066b`)
- `ES/Automation/Contracts/feishu-identity-claim-v1.schema.json` (`2c87970d691504566588d02621354a62c8e7c1342533730180618992ff575c7c`)
- `ES/Automation/Contracts/feishu-message-send-v1.schema.json` (`8e0d477c0f26236482bce5e6e2ec3ca5ac6b1f422e16dd58e6b28f58ac1089ee`)
- `ES/Automation/Contracts/feishu-task-dispatch-v1.schema.json` (`f8f33f8419f634b84ab6b0fd82e68fadc1bff0ba7c765502c4e907683b07480f`)
- `ES/Automation/Contracts/feishu-task-transition-v1.schema.json` (`da3ec68c42da076cd9c11cbce01a2d5eca6eda00b9275587822867c1cf88705f`)
- `.agents/skills/es-feishu-cli/references/identity-messaging-contract.md` (`2d71aa418b008f6a516dbe891a37ab4cdef3b11775630d6b22762f56e700c173`)

`ContentHash`: `8daace62b0684fb4e242df6c141ecc3c430de3aa6240c9b95874427d6df8208b`

## Current Boundary

`es.feishu.identity.claim@1` separates machine-local ownership from Feishu tenant identity. `setup-status` and `list-claims` are local diagnostics. `claim-role` and `release-role` require DryRun and write only the Git-ignored identity root. Claims are partitioned by AppId hash and bind a Windows principal hash plus AIBrain Actor hash; the Live write must match all three identities from the accepted DryRun. Explicit flags separately consent to task assignment and direct messages.

Task dispatch and member transitions may resolve `claimedRoles` in C# before the fixed Node Worker starts. Raw `members` and `claimedRoles` cannot be mixed. Long-lived bindings stay in the Git-ignored `FeishuIdentity` root; a resolved member or recipient is allowed only in the fixed Worker's private request envelope under the separately Git-ignored `FeishuTasks` root. Local resolution does not grant the task mutation command or external-write authorization.

`es.feishu.message.send@1` is a separate L3 external-write contract. Its public input is one role and bounded plain text. C# resolves the recipient from an opted-in claim; the Worker fixes `msg_type=text`, calls only `im.v1.message.create` and supplies a deterministic server `uuid`. Assignment and notification remain separate child runs with explicit partial-success reconciliation.

## Non-Claims

- A local role or bot claim does not create a Feishu user/application, enable bot capability, grant scopes, authenticate a tenant or authorize external writes.
- Static SDK fields, source hashes and DryRun do not prove Feishu message delivery, UUID semantics, recipient reachability, timeout/cancellation or Domain Reload recovery.
- The identity store and private task envelopes are Git-ignored, and local writes are atomic, but encryption, Windows ACLs, bounded retention, shared-account separation, crash-window reconciliation and multi-process contention remain Runtime/unimplemented gaps.
- Raw identity bindings and message content do not become AIKnowledge or ES project facts.
