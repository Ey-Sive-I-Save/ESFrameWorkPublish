# Skill 治理与 Creator 聚合

状态：第一阶段治理链已接入；当前为 S2 / Implemented-Unverified / Verifying。

`KnowledgeId`: `es.skill.governance-creator.v1`
`Authority`: `Derived`
`EvidenceLevel`: `S2`
`RouteKeys`: `skill`, `governance`, `creator`, `validation`, `evidence`, `tier`, `maturity`, `delivery`, `aibrain`, `risk`, `authority`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`, `commercial-coherence`, `delivery-tracking`, `evidence-receipt`, `report-hash`, `source-freshness`, `plan-hash`, `static-review`, `runtime-not-run`
`ContentHash`: `ea16720f7b0ab0839eb3449ff500f7ba1f40285ae1366a75a56f323e01abb219`

`SourceRefs`:

- `.agents/README.md` (`9f0cde755563deadd46fa31fa7a5e520603daeebaf7cd2c35482fbc3d1efb7a9`)
- `.agents/SKILL_RESOURCE_INDEX.yaml` (`9d5cc6d76069d7ec452300f957152d7e95fd39ddd0f31bf24c4ea187daf32116`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`72425a0e2703081f46d7f15c963f79ae24ebf2152ba1e3b61d2dbe3fb96fc6b4`)
- `.agents/skills/es-skill-governance/SKILL.md` (`ba35d6c913609117eb4883bcfcf6f3e664843ed7d0d863ef0551be8fcd93cc96`)
- `.agents/skills/es-skill-governance/agents/openai.yaml` (`381fd906710223483c30c9c6ac1ee48f9581ac69c4cc67e7883a634beb22a101`)
- `.agents/skills/es-skill-governance/governance.json` (`f13e9e0f74fd861970aecec77f3a44190785322734037b90c26065c83f699330`)
- `ES/Automation/Contracts/es-runtime-authorization.schema.json` (`4f0634d2af203a1dabbc509fb2af381c11d5148431aed670ae2ab3ba4ef10853`)
- `.agents/skills/es-skill-governance/references/tier-matrix.md` (`07296724e75a895135a6fa1ac9a6eff36733ed11ab2105afa24a3bbe56baa821`)
- `.agents/skills/es-skill-governance/references/evidence-and-acceptance.md` (`de24f72d7baadc9b8c4bbd393810ca37049b717911475a2572b032f0a629824c`)
- `.agents/skills/es-skill-governance/references/scale-patterns.md` (`c9d8dab8b6c9c8cbc0fd55ef2c247e65c3e696d546f48239f528c7f048a3a4f4`)
- `.agents/skills/es-skill-governance/references/commercial-controls.md` (`0c9aaa792bfadd98044c8292f634cead872c0aafddcee25a222e1eaf22adb951`)
- `.agents/skills/es-skill-governance/references/performance-controls.md` (`96c7ec827b2a7fa811ea3a5345f43ef0607799bfa42047c6fd739530f2dd2b9d`)
- `.agents/skills/es-skill-governance/references/aibrain-contract.md` (`048c73de6edbda59746a44ec7ee1c49b667bac545fcefcb2ea498e63760856a7`)
- `.agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1` (`4cfc7dd970afa37c7f519d6f2b40948841078441a04454fa7c9d0af8f9245e0e`)
- `.agents/skills/es-skill-creator/SKILL.md` (`c4c199332fc00b8b7e1768ba63f2b04b37897d7f2cd80923dad7a3b2b497cef3`)
- `.agents/skills/es-skill-creator/governance.json` (`c21cd92044d256af1ad32b12c03d2a69235731a58749ea9215b1656c2f6a1cc1`)
- `.agents/skills/es-skill-creator/scripts/quick_validate.py` (`5a4a7c524978a13fbd6e2cb3a2628d3eb9cd4f86956d1c2bb71513aebeada6c3`)
- `.agents/skills/es-skill-creator/scripts/init_skill.py` (`ef5d13955776124c56dd0dda0139e72638c80851eaac296b34779f7b70a606d1`)
- `.agents/skills/es-skill-creator/scripts/generate_openai_yaml.py` (`c0c3cf33adc71d7edac66255e29abcbd5e3bcf72ae1456cedb1bb6e7ab4d69c4`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`20b63b3db889b705ae740d366fa234b8ae49b50a60bf72056cd2a96b86db9b57`)

`EvidenceRefs`:

- `es-skill-creator/scripts/quick_validate.py`：官方结构验证器。
- `es-skill-governance/scripts/Test-ESSkillContract.ps1`：项目 Skill 合同、UTF-8、元数据和目录边界验证器。
- 当前验证范围：治理与 Creator 两个 Skill 的静态合同；未取得代表任务、拒绝扩权、重入/恢复、Unity 或发布证据。

## 权威聚合

```text
当前用户目标与动作授权
  -> AIWarnings 长期约束
  -> 可选 AICommand/AIBrain 受管通道合同
  -> es-skill-governance 分级/证据/恢复门禁
  -> es-skill-creator 初始化/更新/验证
  -> Project Skill 正式目录
```

- `SmallTool`、`Workflow`、`Engineering` 只表示范围和验收义务，不扩大权限。
- `Tier`、`Maturity`、`Delivery` 三条状态轴独立维护。
- Creator 自主运行只能生成、升级或验证当前用户范围内的 Skill；候选模式不得自行正式注册。当前用户已明确要求正式写入 `.agents/skills` 时可直接执行，Diff Review 是质量检查而不是二次批准。
- `quick_validate.py` 或 frontmatter 通过最多证明结构；稳定或生产声明必须有与目标层匹配的 S0-S6 证据。
- Skill 运行变慢、启动成本、重复扫描/Hash、缓存、Fast Path 或 Deep Path 问题统一路由到 `es-skill-governance` 的 `performance-controls.md`，不与 Unity Runtime 性能预算路由混用。
- AIBrain 通过 `KnowledgeIndex -> relatedSkills -> governance.json` 定向读取 Skill 的等级、成熟度、交付、证据和风险元数据；计划哈希绑定这些元数据，变化后必须重新规划。
- AIBrain 仍不拥有用户授权、Skill 或 AIWarnings 的权威，也不直接启动进程或写 `Assets/`；通过 AIBrain 执行时必须经过 AICommand、TaskContract 和 `ESAutomationFacade`，直接用户通道不受该传输协议阻断。

## 受管授权的当前源码语义

- `AuthorizationLifetimeMinutes`: `15`
- `TrustedHostProofLifetimeMinutes`: `5`
- `AuthorizationPolicyVersion`: `5`
- `AuthorizationStoreSchemaVersion`: `3`
- `UserDirectedLowRiskMaxUses`: `20`
- `CandidateOnlyL1L2MaxUses`: `5`
- `HighRiskMaxUses`: `1`
- `ReusableAuthorizationRequiresUniqueNonEmptyIdempotencyKey`: `true`
- `ExternalBridgeMayAssertUserDirected`: `false`
- `ExhaustedAuthorizationRequiresNewInvocation`: `true`
- `AuthorizationTerminalStates`: `Active | Exhausted | Expired`
- `CurrentProductionBridgeAuthorizationClass`: `ManagedAIBrain`
- `CurrentUserDirectHostIntegration`: `NotRegistered`

- 下面规则只描述 ManagedAIBrain/Worker 通道，不是直接用户工作的二次审批。`planTask` 只接受调用方计划的
  PlanHash 作为期望值，并从请求快照重建 canonical plan；授权绑定 Policy、完整 AICommand、TaskContract、
  Worker/Descriptor、证据、授权分类、Invocation 和输入，持久化期限为 15 分钟。
- 受信任进程内宿主必须用 5 分钟 proof 绑定 Host、Actor、Invocation、完整请求哈希和当前用户指令 SHA-256，
  才能取得 L1 低风险 `read-only` / `documentation-write` 的 20 次额度。L1/L2 `candidate-only` 最多 5 次，
  L3 或其他计划最多 1 次。当前 `ESAutomationAiBridge` 只绑定 `ManagedAIBrain`，项目中尚无已登记的
  `CurrentUserDirect` 生产宿主；因此 20 次分支是受测协议能力，不是当前 Bridge 已实际使用的能力。
- `maxUses > 1` 的每次调用都必须提供非空且此前未使用的 `idempotencyKey`；重复键、空键或消费记录持久化
  失败都会拒绝该次调用。单次高风险授权消费后不可再次运行。
- 外部 `ESAutomationAiBridge` JSON 没有 `userDirected` 字段，proof 为不可序列化的内部对象；外部输入不能借此
  冒充当前用户，也不能绕过 Skill eligibility/review。
- Policy v5 使用永久 `.lock` 文件的跨进程排他锁和受管原子替换写入 schema 3 Store。PlanHash 与 InvocationId
  双唯一；`Active` 达到次数后转为 `Exhausted`，到期转为 `Expired`，终态不能重签。schema 2 / Policy v4
  消费直接 stale；成功迁移时旧 InvocationId 进入持久退役集合，后续不得在 v5 中复用。损坏或未知代际 Store
  fail-closed 且不覆盖现场。
- 这些限制约束受管授权的传输、复用和重放，不得反向把当前用户明确请求降为候选、只读或待再次批准。

`StaleWhen`: 任一 SourceRef、Creator 初始化器、治理元数据合同、AIBrain 路由、验证脚本、Skill 目录合同或 AIWarnings/AICommand 权限边界变化。
