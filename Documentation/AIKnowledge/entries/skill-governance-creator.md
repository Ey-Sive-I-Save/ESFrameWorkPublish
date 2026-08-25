# Skill 治理与 Creator 聚合

状态：第一阶段治理链已接入；当前为 S2 / Implemented-Unverified / Verifying。

`KnowledgeId`: `es.skill.governance-creator.v1`
`Authority`: `Derived`
`EvidenceLevel`: `S2`
`RouteKeys`: `skill`, `governance`, `creator`, `validation`, `evidence`, `tier`, `maturity`, `delivery`, `aibrain`, `risk`, `authority`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`, `commercial-coherence`, `delivery-tracking`, `evidence-receipt`, `report-hash`, `source-freshness`, `plan-hash`, `static-review`, `runtime-not-run`
`ContentHash`: `4303bfa0eb8727c8dc6907511c60c35b1e04144efac9fda076e53d5bf03ac95a`

`SourceRefs`:

- `.agents/README.md` (`140d048ede62efb430f5c345991aa68630bb7fb35aeec1e17c00f2d718f68d7d`)
- `.agents/SKILL_RESOURCE_INDEX.yaml` (`c228def28eb268015d89d0088bdf92a841d81ef4237035e8e103d0f36f085991`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`06e25a46e0c729f677954c44ebc5edafcba3967146f700418804707cc83119b9`)
- `.agents/skills/es-skill-governance/SKILL.md` (`8386b37114d34a57f6a065857ad139504de5aa7ba0e2b77f6e346559b9319184`)
- `.agents/skills/es-skill-governance/agents/openai.yaml` (`381fd906710223483c30c9c6ac1ee48f9581ac69c4cc67e7883a634beb22a101`)
- `.agents/skills/es-skill-governance/governance.json` (`f13e9e0f74fd861970aecec77f3a44190785322734037b90c26065c83f699330`)
- `ES/Automation/Contracts/es-runtime-authorization.schema.json` (`4f0634d2af203a1dabbc509fb2af381c11d5148431aed670ae2ab3ba4ef10853`)
- `.agents/skills/es-skill-governance/references/tier-matrix.md` (`07296724e75a895135a6fa1ac9a6eff36733ed11ab2105afa24a3bbe56baa821`)
- `.agents/skills/es-skill-governance/references/evidence-and-acceptance.md` (`de24f72d7baadc9b8c4bbd393810ca37049b717911475a2572b032f0a629824c`)
- `.agents/skills/es-skill-governance/references/scale-patterns.md` (`c9d8dab8b6c9c8cbc0fd55ef2c247e65c3e696d546f48239f528c7f048a3a4f4`)
- `.agents/skills/es-skill-governance/references/commercial-controls.md` (`ddb24159116e49b20fd9517316b3b3416f1e12247747ee7f1f0ecb79a1ce94a4`)
- `.agents/skills/es-skill-governance/references/performance-controls.md` (`a83420ccb429dea42826ff7dab7f3cbfbc4575624b2246da402f45bfe6ca6519`)
- `.agents/skills/es-skill-governance/references/aibrain-contract.md` (`80348cb1905e8badb7e1be59942086761332e01b1be5e9f2ac441a8dda28fc33`)
- `.agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1` (`600ac229aa87f4e7417551e07e3eeb689c5d5286be52b3fb05f24dd0ec934885`)
- `.agents/skills/es-skill-creator/SKILL.md` (`d01813e9053236a9e900d25f246368482fd1fd23555c254a0ee1f40dff7884f7`)
- `.agents/skills/es-skill-creator/governance.json` (`c21cd92044d256af1ad32b12c03d2a69235731a58749ea9215b1656c2f6a1cc1`)
- `.agents/skills/es-skill-creator/scripts/quick_validate.py` (`effb02b4a13ea2caee7d2eceded3d95b214ad92433de821e402564fc2e8b9654`)
- `.agents/skills/es-skill-creator/scripts/init_skill.py` (`b57f34722cacf52a0d9b1e31e4e4e4d077e26eae79054a4d67c9d2d90cb11193`)
- `.agents/skills/es-skill-creator/scripts/generate_openai_yaml.py` (`837c5c8a82589cd9e2e66714b57114cd4299480c83fa0a69bca124f68ab61a6d`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`d3f8e0d6944395a14e8f680d2e7cc6afcb74f4485e578d3a6bfb3ebd798fd86e`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`75fc4b138f360c75de3232bdec4dd18b8862a4d48eff6f588378bb47779fd0f8`)

`EvidenceRefs`:

- `es-skill-creator/scripts/quick_validate.py`：官方结构验证器。
- `es-skill-governance/scripts/Test-ESSkillContract.ps1`：项目 Skill 合同、UTF-8、元数据和目录边界验证器。
- 当前验证范围：治理与 Creator 两个 Skill 的静态合同；未取得代表任务、拒绝扩权、重入/恢复、Unity 或发布证据。

## 权威聚合

```text
AIWarnings 长期约束
  -> AICommand 单次授权
  -> es-skill-governance 分级/证据/恢复门禁
  -> es-skill-creator 初始化/更新/验证
  -> Project Skill 正式目录
```

- `SmallTool`、`Workflow`、`Engineering` 只表示范围和验收义务，不扩大权限。
- `Tier`、`Maturity`、`Delivery` 三条状态轴独立维护。
- Creator 只能生成、升级或验证明确授权路径内的 Skill；候选生成仍必须隔离在 `ES/Automation/Candidates/AgentAuthoring/`，经 Diff Review 和人工批准后才可进入 `.agents/skills`。
- `quick_validate.py` 或 frontmatter 通过最多证明结构；稳定或生产声明必须有与目标层匹配的 S0-S6 证据。
- Skill 运行变慢、启动成本、重复扫描/Hash、缓存、Fast Path 或 Deep Path 问题统一路由到 `es-skill-governance` 的 `performance-controls.md`，不与 Unity Runtime 性能预算路由混用。
- AIBrain 通过 `KnowledgeIndex -> relatedSkills -> governance.json` 定向读取 Skill 的等级、成熟度、交付、证据和风险元数据；计划哈希绑定这些元数据，变化后必须重新规划。
- AIBrain 仍不拥有 Skill 或 AIWarnings 的权威，不直接启动进程、不直接写 `Assets/`，执行必须经过 AICommand、TaskContract 和 `ESAutomationFacade`。

`StaleWhen`: 任一 SourceRef、Creator 初始化器、治理元数据合同、AIBrain 路由、验证脚本、Skill 目录合同或 AIWarnings/AICommand 权限边界变化。
