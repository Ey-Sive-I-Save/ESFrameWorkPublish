# Skill Resource Index 知识条目

状态：基础组合导航；不拥有 Skill、AIWarnings、AICommand 或 MCP 权限。

`KnowledgeId`: `es.skill.resource-index.v1`
`Authority`: `Derived`
`EvidenceLevel`: `S1`
`StaleWhen`: Skill 资源组合、Catalog 门禁、AIBrain 路由或 MCP/证据合同变化。
`RouteKeys`: `skill`, `resource-index`, `catalog`, `validation`, `security`, `reference`, `script`, `mcp`, `evidence`, `evidence-pending`, `static-boundary`, `external-side-effect`, `blocking-layer`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`, `lifecycle`, `discovery`, `route-scope`, `registry`, `incremental-discovery`
`ContentHash`: `1728bb3bb98fd1aec1c5e4fd69ef00b745408c4106c01fe5fe3d1f46a99def17`

`SourceRefs`:

- `.agents/SKILL_RESOURCE_INDEX.yaml` (`9d5cc6d76069d7ec452300f957152d7e95fd39ddd0f31bf24c4ea187daf32116`)
- `.agents/SKILL_CATALOG.yaml` (`3552fb98815b34e44c9ff4580adaa089c3c595293cc10806eb60c2f75860c8b3`)
- `.agents/SKILL_DISCOVERY_POLICY.json` (`0399e899c6d890b94dfa06245f9a97445d45a0dfda7e550533b22d944385cba0`)
- `.agents/SKILL_REGISTRY.manifest.json` (`1ab97b0cf61bbe37738ff3a3399704a28213232602cd2acc5271da27b9dd5904`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`72425a0e2703081f46d7f15c963f79ae24ebf2152ba1e3b61d2dbe3fb96fc6b4`)
- `.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1` (`c7217fde26fc3ded687d1a4505157803297cd038363ea0518f385df1e6717ec2`)
- `.agents/skills/es-skill-validator/scripts/Test-ESSkillEvidence.ps1` (`6600968013509638fcdc98b1896c8fe4227cbb85ecbd1aec0c742d143bb7d78d`)
- `.agents/skills/es-skill-validator/references/validation-rubric.md` (`60a8b2e0072c86c457787bfa013073b057f6e4cdeda274de5814afec8eba5798`)
- `.agents/skills/es-skill-validator/references/boundary-decision-contract.md` (`650ad7003024aadcc9c3151a880e5370aa2876d198a0c98ff74808575bfa7a2c`)
- `.agents/skills/es-skill-governance/scripts/Test-ESSkillArchitecture.ps1` (`e2e4bbd06f851dedd34cd1d533542c285138c2158b4ea0904f5148f1ed47a87b`)
- `.agents/skills/es-skill-governance/scripts/Build-ESSkillRegistryManifest.ps1` (`52c62b349aa9192149c49c62465da8e661283573b65388c2871fb949658f5b9f`)
- `.agents/skills/es-skill-governance/references/verification-semantics.md` (`6c6a124eec1561a8ad143628ffa57a629a6dbc00c4ce99c6e5bcd72fe5cc463a`)
- `.agents/skills/es-skill-governance/references/capability-mode-registry.json` (`fe2ce3aa3cd27f956ed047d949fc5350602ccb8e21ddc4f768449cfce6622ff1`)
- `.agents/skills/es-skill-governance/references/command-binding-registry.json` (`41a07b9129c24ad36e04dce7328c51fcb8f3a098357510546acf874b5ea39f27`)

- `.agents/skills/es-static-deep-replay/SKILL.md` (`f75b8452ef23ecb09c9487a338df772239185183e72c2586fe99edcc66369014`)
- `.agents/skills/es-static-deep-replay/governance.json` (`50bbcd2cc57aac5d8b4b6987a3a40baa9d584bed0dafbbf8c0e1c6d3b3dd5a98`)
- `.agents/skills/es-static-deep-replay/scripts/Invoke-ESStaticDeepReplay.ps1` (`f473636cea6b256fa4d34aa2a99049419e850ed398ad3856a65a9efd5129b130`)
- `.agents/skills/es-static-deep-replay/scripts/Test-ESStaticReplayManifest.ps1` (`5f39e5963bb7ee9f96b65cc91102b1121aa65734ffcb6bea0e503836d0ce5812`)
- `.agents/skills/es-static-deep-replay/references/specialized-acceptance-registry.json` (`618ae7b9ea997e5fbbf6b78b5ddc06a66c572392f0167abb665515fe9f4ec8f2`)
- `.agents/skills/es-static-deep-replay/references/evidence-receipt-contract.md` (`ad417bc9ecf457c664830cd0e3ad7865ed1a6a6bd20244f4ed29d4bd933ad717`)
- `.agents/skills/es-static-deep-replay/static-replay.manifest.json` (`967246de0f24af11fa68ae3f7ccf8684531b3e4b75affce2596bf4cb75ce3b34`)
- `.agents/skills/es-static-deep-replay/references/static-replay-adapter.md` (`33f5205f1a665476f35b435b10ec44f0146c61029fdd1406d299df94da6a6102`)
- `.agents/skills/es-skill-validator/references/evidence-receipt-contract.md` (`c22eff377cd38083f6f1d0cc7b35a121ddef6722a7982d5c526f3d0b1cd03b46`)
- `.agents/skills/es-skill-validator/static-replay.manifest.json` (`fc41ba60d2544c61160327867e28f08efd773c98f13089b325673181a08772e3`)
- `.agents/skills/es-skill-validator/references/static-replay-adapter.md` (`8f97a0e1d3b5f67b9048ebaabf78df09e94278bda3f4a4114fe4f7cc1bffd348`)
## 使用

先按 `routeKeys` 选择最小 Skill 集，再加载其 references/scripts/MCP 能力和证据合同。索引漂移时 PlanHash 失效，不能用旧组合继续执行。
