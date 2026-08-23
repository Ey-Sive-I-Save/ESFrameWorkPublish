# Skill Resource Index 知识条目

状态：基础组合导航；不拥有 Skill、AIWarnings、AICommand 或 MCP 权限。

`KnowledgeId`: `es.skill.resource-index.v1`
`Authority`: `Derived`
`EvidenceLevel`: `S1`
`StaleWhen`: Skill 资源组合、Catalog 门禁、AIBrain 路由或 MCP/证据合同变化。
`RouteKeys`: `skill`, `resource-index`, `catalog`, `validation`, `security`, `reference`, `script`, `mcp`, `evidence`, `knowledge-output`, `bounded-output`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`
`ContentHash`: `fd2091ad8c085ab55dadb8ccfe42df818f030141f37ff53c7431ab845094affc`

`SourceRefs`:

- `.agents/SKILL_RESOURCE_INDEX.yaml` (`dac562240b2eb1148def4f783ba4d4fdff4a119c6f620b2fdb57667a7e444a28`)
- `.agents/SKILL_CATALOG.yaml` (`a9c59adf468c637e696059c73d5687bf31121f319d8d8aaee864d797733c6fc5`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`9735b55bf6b2df8758050f2b84b053aabc0438ddf633c3c61ba43e4d684349d9`)
- `.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1` (`3ef83ca3b7f5bc7af05558dcc7821b3a3ecfd1d72e01eb5119d4feb4fc4c0b0f`)
- `.agents/skills/es-skill-validator/scripts/Test-ESSkillEvidence.ps1` (`03a581ab12344047ec74d334cc482b967877df4b9ebf976022793bc0400830ad`)
- `.agents/skills/es-skill-validator/references/validation-rubric.md` (`60a8b2e0072c86c457787bfa013073b057f6e4cdeda274de5814afec8eba5798`)
- `.agents/skills/es-skill-validator/references/boundary-decision-contract.md` (`650ad7003024aadcc9c3151a880e5370aa2876d198a0c98ff74808575bfa7a2c`)
- `.agents/skills/es-skill-governance/references/verification-semantics.md` (`8ab170ebd501d73998d8e550cc75094e59aa08d55a4223b43fb31cffc87ec32d`)
- `.agents/skills/es-skill-governance/references/capability-mode-registry.json` (`5f522626590fa967b1033c8e46db54ca9214711607f585dc604a519a585082df`)
- `.agents/skills/es-skill-governance/references/command-binding-registry.json` (`d859333b02c31b5f30e01e5ba8eb60a620f9cd006b823811612893d3fc478885`)

- `.agents/skills/es-static-deep-replay/SKILL.md` (`42f334a5b682ac0dfb935461b486f5ff840d4a93ba64ae8d35ce5c6ef190a169`)
- `.agents/skills/es-static-deep-replay/governance.json` (`3b4348dda4167745ab28adf17f9cf8dda0a39f07e94d4d6f83ae93734357e10a`)
- `.agents/skills/es-static-deep-replay/scripts/Invoke-ESStaticDeepReplay.ps1` (`f473636cea6b256fa4d34aa2a99049419e850ed398ad3856a65a9efd5129b130`)
- `.agents/skills/es-static-deep-replay/scripts/Test-ESStaticReplayManifest.ps1` (`5f39e5963bb7ee9f96b65cc91102b1121aa65734ffcb6bea0e503836d0ce5812`)
## 使用

先按 `routeKeys` 选择最小 Skill 集，再加载其 references/scripts/MCP 能力和证据合同。索引漂移时 PlanHash 失效，不能用旧组合继续执行。
