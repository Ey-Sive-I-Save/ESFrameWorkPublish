`KnowledgeId`: `es.automation.governance-contracts.v1`
`Authority`: `Derived`
`RouteKeys`: `automation, task-contract, capability, acceptance, receipt, completion-decision, source-drift`
`EvidenceLevel`: `S1`
`StaleWhen`: `ESAutomation TaskContract、CompletionDecision、Verifier 或 Receipt 合同变更`

## SourceRefs

- `Documentation/ES_AUTOMATION_CENTER_STANDARD.md` (`fc2da7d1f70575744515c6ecbabb878c407ccdeebacd9b4bd39f5da84aea89cf`)
- `Documentation/ES_AUTOMATION_GOVERNANCE_CONTRACTS.md` (`8084feb4a81812821a7b6e0c0bb1675a4c29d8e54cd6c4fdab7ac2f7ee18c29b`)
- `ES/Automation/Contracts/es-automation-task-contract.schema.json` (`ee34f8f5e8e79ac22ccab1345bea9cabe6cbf90340009187ad23076d95f9da12`)
- `ES/Automation/Contracts/es-automation-run-result.schema.json` (`65068ccad7fd2632703b53536971068c0b09dea79ea192b73ce19316c93b83ea`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`d1027d9905a34bc9c10215df61150eb1f4bfbb71c33fd5f83b90e9956aac296e`)

`ContentHash`: `6df06f9ded37a33cba1c32560f68c52bf71c46fd1c4a83e532fed98285f6ab5d`

当任务涉及 AICommand、TaskContract、Worker、权限、验收、Receipt、幂等、源漂移或商业级交付时，必须先阅读上述两个源文件。

现有 ES 入口保持兼容；治理合同通过可选 `acceptanceCriteria`、`capabilityEnvelope`、`executionSnapshot` 和 `completionDecision` 逐步接入。

`Completed` 只代表执行器结束，`Accepted` 必须由外部验证器根据新鲜、绑定且无冲突的证据确认。
