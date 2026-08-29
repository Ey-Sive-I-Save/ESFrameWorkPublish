# Agent Skills 与 AICommands 协作边界 AI 协作警告

> 状态：现行 P0 边界；详细事实、历史快照、Skill 映射和后续展望集中维护在 Knowledge。
> Status：current。
> StableId：`es.aiwarnings.agent-skills-aicommands-boundary`
> Authority：`AIWarnings`
> RouteKeys：`aiwarnings`、`architecture`、`skill`、`aicommand`、`knowledge`、`routing`、`evidence-boundary`、`permission-boundary`
> 适用范围：`.agents/skills`、`Assets/Plugins/ES/AICommands`、`Assets/Plugins/ES/AIWarnings`、UnityMCP、确定性验证脚本。
> Applicability：上述协作入口的发现、路由、受管执行、证据分层与权限边界。
> EvidenceRef：`Documentation/AIKnowledge/entries/agent-skills-aicommands-boundary.md`；`.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1 -RouteId es.aiwarnings.agent-skills-aicommands-boundary`
> Owner：ES AIWarnings / AIKnowledge governance owner。
> StaleWhen：AIWarnings Start 链、AICommand/Skill 合同、Registry/Catalog、AIBrain 路由、KnowledgeIndex 或任一来源哈希变化。
> Knowledge：`Documentation/AIKnowledge/entries/agent-skills-aicommands-boundary.md`

## 不可下放的长期边界

- `.agents/skills` 是项目级 Skill 的发现权威；每个直接 Skill 以 `SKILL.md` 和 `agents/openai.yaml` 为入口。该目录位于 Unity `Assets` 外，不进入 AssetDatabase、`.meta`、AssetBundle、ResourcePlan 或 Player 发布内容。
- Codex 从项目根启动；明显匹配的 Skill 未出现在注入清单时，报告“清单注入缺口”并直接核对项目内 `SKILL.md`。新开窗口或重启不能证明 Skill 不存在。
- `AIWarnings` 负责长期事实、P0 边界、禁止事项和证据标准；`AICommands` 负责受管通道合同；`.agents/skills` 负责可复用工作流；UnityMCP、PowerShell 和编译器负责执行与证据采集。它们都不授予修改权限。
- 用户当前目标与动作授权优先；AICommand、Skill、Knowledge、Catalog 和工具不得扩大或缩小该授权。
- 读取顺序是 Start README → CurrentStatus → RuleIndex → 命中的 P0/专项 → 当前源码与证据。禁止递归加载全库 AIWarnings，也不得用缓存摘要替代当前 P0 或事实源。
- 静态文件、脚本或 `.csproj` 证据不得升级为 Unity、PlayMode、Profiler、Player、IL2CPP、Runtime 或发布通过。

## 协作链

```text
用户目标与动作授权
  -> 直接实施有界请求，或选择受管 AICommand
  -> Skill 提供工作流
  -> 工具执行并生成证据
  -> 按证据等级交付
```

## Knowledge 与 Registry 指针

- 详细的 Skill 映射、历史计数、已实现能力、未来展望、验收标准、失败面和恢复动作见 `Documentation/AIKnowledge/entries/agent-skills-aicommands-boundary.md`。
- 动态映射以实际 `.agents/skills`、`SKILL_CATALOG.yaml`、`SKILL_REGISTRY.manifest.json`、`SKILL_DISCOVERY_POLICY.json` 和各 `governance.json` 为准。
- 中文自然语言发现使用 `.agents/SKILL_ROUTE_ALIASES.zh-CN.json` 与 `Resolve-ESChineseSkillRoute.ps1`；别名只负责发现，不授予权限。
- AICommand 和 AIWarnings 的机器投影分别以 `AICommandCatalog.json` 和 `AIWarningsRouteCatalog.json` 为准；投影漂移时回读当前权威源。

## 证据边界

本 Warning 只保留不可下放的长期边界和导航指针。Knowledge 条目保存原有事实与提案的保真迁移，并绑定 `SourceRefs`、`ContentHash`、`EvidenceLevel` 和 `StaleWhen`。Knowledge 不拥有源事实，不构成动作授权。
