---
name: es-repository-discovery
description: Build a bounded, evidence-linked map of an ESFramework repository before design or implementation. Use when the target area, authority, build path, tests, or dependency surface is unclear.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Repository Discovery

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

建立目标仓库的最小事实地图，不把摘要、旧交接或关键词命中当成源码事实。

## Workflow

1. 确认 Git root、branch、HEAD、工作树和用户指定目标；发现路径越界时停止。
2. 读取 `AGENTS.md`、`.agents/SKILL_RESOURCE_INDEX.yaml`、AIWarnings Start 链和目标 `KnowledgeIndex` 路由。
3. 按目标收集源码入口、程序集/包、配置、测试、脚本、MCP 能力和发布入口；每项记录相对路径与哈希。
4. 输出 `facts / assumptions / authorities / unknowns / next checks` 五段地图；不修改源码。
5. 目标范围变化、HEAD 变化或 SourceRef 漂移时废弃旧地图并重新发现。

## Workflow controls

- 只读；禁止递归读取凭据、用户目录、Library/Temp/Logs 或无关仓库。
- 最大扫描范围、目标路径和文件数必须显式；超过预算停止。
- 正向、非法 root、越界目标、重复发现和并发工作树变化都必须产生可解释结果。
- 证据等级最高为源码/静态地图；不宣称编译、Unity、运行或发布通过。

## Resources

- 组合规范：`.agents/SKILL_RESOURCE_INDEX.yaml`
- 项目权威：`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- 入口脚本：`references/discovery-contract.md`
