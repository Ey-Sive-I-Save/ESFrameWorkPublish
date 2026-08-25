---
name: es-skill-validator
description: Validate ESFramework project Skills before registration, acceptance, upgrade, release, or routing. Use when checking Skill structure, governance metadata, Catalog freshness, UTF-8 integrity, references, permission boundaries, security signals, or behavioral evidence.
---

## Verification boundary

For existing ES capabilities, validate compatibility with `../es-skill-governance/references/es-preservation-refactor-contract.md`. A static pass must not hide removed entry points, changed defaults, or an unrecorded breaking migration.

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`
- The aggregate `status=review` means the static layer has no hard failure but Evidence/Runtime remains pending. The validator may still return a non-zero process code as a CI follow-up signal; callers must read `staticStatus`, `decisionStatus`, `blockingLayer`, and `runtimeStatus` instead of translating that code into a source `Blocked` result.

# ES Skill Validator

这是一个只读治理门禁，不创建、不修改、不注册 Skill。它把“文件存在”与“可以被项目采用”分开，输出可审计的检查结果。

## Validation profiles

- `Structural`：frontmatter、命名、标准目录、引用路径、UTF-8、模板残留。
- `Governance`：`governance.json`、Tier/Maturity/Delivery、证据等级、权限边界、控制引用。
- `Catalog`：`SKILL_CATALOG.yaml` 中唯一记录、分类、状态与 Skill/governance 哈希。
- `Security`：提示注入、未声明网络/凭据读取、权限绕过、危险脚本和供应链信号；命中后必须人工复核。
- `Semantic`：ESFramework AIWarnings、AICommands、AIBrain、Knowledge、Resource Index 与 Catalog 的真实绑定关系。
- `Boundary`：把 Skill 的实际脚本行为与当前用户范围、AIWarnings 质量约束、所选授权 lane、项目根路径和证据声明做分级裁决。AI 自行扩大范围、项目越界、未被用户点名的秘密/网络/破坏性操作和证据冒充仍 fail-closed；`CurrentUserDirect` 下缺少 AICommand/TaskContract 仅是受管通道不可用的 `review`，不得阻断用户直接请求。
- `Architecture`：验证生命周期发现资格、Operational/CapabilityIndex/Audit 路由范围、通用 RouteKey 污染、Catalog/Resource/Knowledge/AICommand 元数据代际和 Skill Registry Manifest 闭包。
- `CapabilityMode`：默认按 `mutating` 处理；`.agents/skills/es-skill-governance/references/capability-mode-registry.json` 只能把明确的分析型 Skill 标成 `advisory` 或候选产物 Skill 标成 `candidate`。这些模式只约束 Skill 自主或 `ManagedAIBrain` 执行，不得把用户明确要求的直接实现降为候选；用户未点名的外部执行仍须拒绝。
- `Evidence`：requiredCases、receipt、SourceRef 和验证结果是否足以支持当前交付状态。
- `Static/Runtime`：将源码、配置、哈希和确定性脚本证据与 Unity/进程/显示器/时序等外部运行证据分轴裁决；`runtime-not-run` 不得被解释为 `static-blocked`。`Invoke-ESSkillValidation.ps1 -Profile VerificationSemantics` 会检查每个 Skill 的显式验证档案。
- `StaticDeepReplay-first`：验证器先审查静态深度回放、边界模拟和缓存复用；Runtime 未经开发者授权不得自动启动。每个 profile 的 `staticWeight` 必须不低于 `0.5`，且必须声明 Runtime 授权策略。`static-blocked` 只表示源码/边界缺陷；`runtime-blocked` 只表示外部证据门禁，不能让 AI 重写已经静态通过的代码。
- Runtime 授权必须通过 `es-skill-governance/scripts/Test-ESRuntimeAuthorization.ps1` 的一次性绑定合同；`developerAuthorizationRequired=true` 本身不是权限。
- Portfolio 聚合 Receipt 使用独立的 `scripts/Test-ESSkillPortfolioEvidence.ps1` 与 `references/portfolio-receipt-contract.md`；不得把聚合报告冒充单 Skill 行为 Receipt。

## Rules

- 默认只读；不得因验证而修改 Skill、Catalog、Knowledge、Git 或任何生成物。
- `PASS` 只表示该 profile 的检查通过；没有行为证据时不得声明 `Accepted`、`Stable` 或 `Released`。
- 任何高风险安全信号、哈希过期、缺失治理元数据或权限扩大都阻断验收。
- `Boundary` 的 `permission-expansion`、`path-boundary`、未被当前用户点名的 `secret-access` / `network-undeclared` / `destructive-undeclared`、`evidence-overclaim` 和 `exception-swallowing` 是阻断项；说明“禁止某行为”的文本不会被误判为执行行为。`NoMatchingCommand` 和受管绑定缺失在 `CurrentUserDirect` lane 只产生 `review`，在 `ManagedAIBrain` lane 才阻断该通道。
- 需要写入、网络、Unity/MCP 或进程能力的 Skill 在 `ManagedAIBrain` lane 必须由 `governance.json.commandBindings` 提供唯一 `commandId`、正文 Hash、协议元数据、必读 Authority refs 和必要 TaskContract；`CurrentUserDirect` lane 改以当前用户明确动作和范围为授权依据。routeKey、Skill 名称或关键词命中本身不授权 AI 自行扩展。
- Boundary 是静态语义门禁，不是安全沙箱。动态路径、别名/间接执行、编码混淆和非空异常吞掉必须保留逐项发现；其中有来源合同的只读路径可以是 `review`，无来源合同的路径、外部执行、编码混淆和异常吞掉仍 fail-closed。验证器不得将任何静态结果描述为完整安全证明。
- 外部规范只作为结构参考；AIWarnings、AICommands、AIBrain 和项目治理文件仍是本项目权威。

## Workflow

1. 读取 `.agents/README.md`、`SKILL_RESOURCE_INDEX.yaml`、`SKILL_CATALOG.yaml` 和目标 Skill。
2. 运行 `scripts/Invoke-ESSkillValidation.ps1`，默认执行 `Structural,Governance,Catalog,Security,Semantic,Boundary,Architecture`；默认 `AuthorizationLane=CurrentUserDirect`，只有审计 AIBrain/Worker 受管执行闭包时才显式选择 `ManagedAIBrain`。
3. 根据 Skill 的 `static-replay.manifest.json.responsibilityProfile` 选择适用证据案例；`governance.json.requiredCases` 是允许的上限，不再对所有 Skill 强制五案例。无适用案例时使用 `not-applicable`，不得伪造回执。
4. 对交付前的项目运行 `scripts/Test-ESSkillPortfolio.ps1`，把全量结果写成组合 Receipt；任何一个 Skill 的失败或安全阻断都会阻止组合通过。
5. 只在报告明确通过且证据等级足够时，允许 `es-skill-creator` 更新生命周期状态。

对已有 Skill 的升级，先读取 `es-skill-governance/scripts/Get-ESSkillChangeImpact.ps1` 的结果。`medium` 或 `major` 时，验证报告必须保留 `revalidationRequired=true`，并在缺少其 `requiredStages` 证据时将完成声明限制为 `Implemented-Unverified`/`NeedsReview`，不得投影为 `Accepted`。

## Output contract

报告必须区分 `passed`、`review`、`failed`、`blocked`、`not-run`，列出 profile、发现项、哈希、风险等级、证据来源和下一步；不得用总体绿色掩盖未运行的行为验证。报告同时必须包含 `staticStatus`、`staticCodeStatus`、`staticContractStatus`、`staticBoundaryStatus`、`evidenceStatus`、`runtimeStatus`、`overallVerdict`、`decisionStatus`、`blockingLayer`、`claimsNotProven` 和 `nextAction`。`StaticReview` 可以在 runtime 未运行时完成；Runtime 缺证据时要标成 `runtime-blocked` 或 `runtime-not-run`，而不是暗示源码失败。外部路径、进程、终端和会话副作用必须归入 `StaticBoundaryBlocked`，不能伪装成普通源码错误，也不能由 Runtime 收据绕过。`review` 表示静态证据有边界但仍需收据/人工确认，不等于 Accepted。

面向用户的收尾必须遵循共享菜单合同：把可继续动作投影为最多三项连续编号 `1.`、`2.`、`3.`，每项同时提供稳定 `id`、简短 `label` 和 `requiresUserChoice=true`。用户回复数字时，只能将其解析为候选动作，不得视为授权或自动执行；必须通过 `Resolve-ESNextStepSelection.ps1` 解析并绑定当前评估哈希。`nextAction` 是机器状态，不能替代用户菜单。渲染收尾时，`📌 下一步` 数字菜单必须是最后一个用户可见字段；其后不得追加 Skill、写入、Runtime、评分、未证实项或其他说明。详细合同见 `../es-ai-interaction-governance/references/interaction-governance-contract.md`。

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 只读检查；`ReportPath` 只有显式传入时才写报告。
- 目标必须位于项目 `.agents/skills` 直接子目录；拒绝越界路径和递归猜测。
- 退出码：`0` 全部要求检查通过，`1` 发现失败/阻断，`2` 参数或环境错误。

## Resources

- `references/validation-rubric.md`：检查矩阵和状态裁决。
- `references/security-signals.md`：安全信号分级与人工复核规则。
- `scripts/Invoke-ESSkillValidation.ps1`：只读验证入口。
- `../es-skill-governance/scripts/Test-ESSkillArchitecture.ps1`：组织架构、生命周期和注册清单门禁。
- `scripts/Test-ESSkillPortfolio.ps1`：全量 Skill 资产组合门禁与 Receipt 生成器。
- `scripts/Test-ESSkillPortfolioEvidence.ps1`：Portfolio 聚合 Receipt 的独立哈希与子结果合同验证器。
- `tests/Test-ESSkillValidatorRegression.ps1`：显式绑定、拒绝语义、动态路径等固定正反例回归 fixture。


## Specialized static acceptance

Acceptance ID: `validator-profile-isolation`

Guidance: `references/static-specialized-acceptance.md`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- profile isolation
- runtime-not-run
- StaticDeepReplay
- blocked scope
- catalog hash

Required specialized cases: `profile-isolation, negative-contract, catalog-hash-check, boundary-report, runtime-not-run-scope`
Guidance: `references/static-specialized-acceptance.md`
