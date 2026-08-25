# Feishu 受管任务生命周期边界

`KnowledgeId`: `es.feishu.task-lifecycle.v1`
`Authority`: `Derived`
`RouteKeys`: `feishu, lark, task-monitor, task-dispatch, task-transition, virtual-team, external-write, dry-run`
`EvidenceLevel`: `S1`
`StaleWhen`: `飞书 Task v2 SDK/API、AICommand、TaskContract、Schema、Worker、AIBrain 绑定或 Runtime 证据变化`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Assets/Plugins/ES/AICommands/Feishu任务监控_AI命令.md` (`ada1a52a0f04c32c50600feee412ea25638cf081d2a7408097be7191678a3924`)
- `Assets/Plugins/ES/AICommands/Feishu任务派发与推进_AI命令.md` (`b711aed9c7c5eaa884d6e406b6bb5f6ec7ab1ace65a92fb218c0a4475b9ce796`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESFeishuTaskAutomation.cs` (`fdc0b137ccf8211ba482147979e9cc3d9e5831bae14f7a8c07d26caaf15ec25f`)
- `ES/Automation/Workers/Node/Feishu/task-worker.js` (`4dfd148e10fdc7e0bcd7167ec9f650f9e750d504bd0cf0764fc069401b6b066b`)
- `ES/Automation/Contracts/feishu-task-monitor-v1.schema.json` (`e76103d13e908f0e9466c77cfb74c10f76fe9eaed0211e8443a62a37ef293eef`)
- `ES/Automation/Contracts/feishu-task-dispatch-v1.schema.json` (`f8f33f8419f634b84ab6b0fd82e68fadc1bff0ba7c765502c4e907683b07480f`)
- `ES/Automation/Contracts/feishu-task-transition-v1.schema.json` (`da3ec68c42da076cd9c11cbce01a2d5eca6eda00b9275587822867c1cf88705f`)

`ContentHash`: `acb65b53e2515f17d61bb594e4813294b271b40d6e54c47ba01891ae6a788cda`

## Current Boundary

Three registered contracts separate monitoring, dispatch and transition. AIBrain enforces exact command bindings: `feishu.task.monitor -> es.feishu.task.monitor@1` and `feishu.task.mutate -> es.feishu.task.dispatch@1 | es.feishu.task.transition@1`.

Live dispatch/transition requires a new one-time plan and InvocationId plus `dryRunEvidenceRunId` bound to an accepted, hash-intact, same-input DryRun no older than 30 minutes. Transition writes (`task-update`, complete/reopen, member and reminder changes) use zero retry budget and stop as `UNCERTAIN_REMOTE_RESULT` when a transient response is lost. Dispatch task creation uses the worker's bounded retry policy, while task-list creation performs exact-name recovery; fixture creation can therefore produce an explicit `PARTIAL_SUCCESS`. Do not generalize the transition rule to every dispatch mutation.

The virtual-team fixture is one isolated task list and five role-labelled tasks. It does not create users, departments, chats or messages and does not auto-delete remote objects.

Task dispatch and member transitions may consume `claimedRoles`. C# resolves only same-AppId roles whose owners explicitly enabled task assignment, then binds the resolved member projection into DryRun/Live evidence. This role resolver does not expand task-write authority.

## Evidence Boundary

Source, contracts and hashes are Static evidence only. Feishu authentication, tenant permissions, Unity-managed execution, remote creation, timeout/cancellation and Domain Reload remain `runtime-not-run` until fresh managed RunRecords prove them.

Feishu task data remains `ExternalCollaboration`. It may support routing and progress views with SourceRefs and freshness, but it cannot override ES source, AIWarnings, AICommands, TaskContracts or execution authorization.
