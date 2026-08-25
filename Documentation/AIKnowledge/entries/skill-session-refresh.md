# Skill 会话增量刷新

状态：现行工程路由；静态增量发现已实现，模型消费和 Unity 内运行回放待验收。

`KnowledgeId`: `es.engineering.skill-session-refresh.v1`
`Authority`: `Source + Skill contract`
`EvidenceLevel`: `S2`
`RouteKeys`: `skill`, `session`, `refresh`, `capability`, `delta`, `stale`, `routing`, `aibrain`, `understanding-drift`, `skill-understanding-refresh`, `capability-refresh`, `incremental-discovery`, `numeric-selection`, `next-step-dispatch`
`ContentHash`: `8c286d0c15dbf36c747bb78c0ec4aa47657d1416f84cd258ab734c87baf95009`
`StaleWhen`: Skill Catalog、Resource Index、Knowledge 路由、Skill 哈希、治理合同或当前会话 PlanHash 绑定变化。

`SourceRefs`:

- `.agents/skills/es-skill-session-refresh/SKILL.md` (`2c75a6447d18019f5251f665976ed8aaf071fac79f48592ab907a354982ddd3a`)
- `.agents/skills/es-skill-session-refresh/governance.json` (`ca7268ea9fe7e541cc775adb05718946e4aa579a66a000fa7494c4ac49f2683b`)
- `.agents/skills/es-skill-session-refresh/scripts/Invoke-ESSkillSessionRefresh.ps1` (`450fffe76e17f557c7991dd29db254e0847018dabf2ae2584258fe5a5594e567`)
- `.agents/skills/es-skill-session-refresh/references/session-refresh-contract.md` (`1aae82280ffb1074751638444f6aabd10a9a452659622db716c6a48f3cd325d9`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `.agents/SKILL_RESOURCE_INDEX.yaml` (`9d5cc6d76069d7ec452300f957152d7e95fd39ddd0f31bf24c4ea187daf32116`)
- `.agents/SKILL_CATALOG.yaml` (`3552fb98815b34e44c9ff4580adaa089c3c595293cc10806eb60c2f75860c8b3`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`72425a0e2703081f46d7f15c963f79ae24ebf2152ba1e3b61d2dbe3fb96fc6b4`)
- `.agents/skills/es-skill-governance/references/capability-mode-registry.json` (`fe2ce3aa3cd27f956ed047d949fc5350602ccb8e21ddc4f768449cfce6622ff1`)
- `.agents/skills/es-skill-governance/references/command-binding-registry.json` (`41a07b9129c24ad36e04dce7328c51fcb8f3a098357510546acf874b5ea39f27`)
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json` (`0e5e1b02be97d7ef42530c231e1495b2f535395dcf1b675492a7846853558e44`)

`EvidenceRefs`: Skill StaticDeepReplay receipt；尚无 Unity 窗口内队列更新与 AIBrain 自动重新路由的 Runtime 回放证据。

## 职责

用户说“你的理解已经过时”“刷新一下技能理解”或等价表达时，AIBrain 应自动触发本能力，用户不需要点名 Skill。长运行 AI 窗口不应在每次队列更新时重新读取整个 Skill Portfolio。该能力先对 Resource Index、Catalog、Knowledge Index 和 Skill 资源做哈希级增量比较，再将变化与当前目标路由求交，只读取命中的变化项；未命中的变化只记录为 out-of-scope，不进入模型上下文。

绑定的 Skill、治理、AICommand、TaskContract、Knowledge SourceRef 或 Task Read Snapshot 发生变化时，旧 PlanHash 和旧结论必须标记 stale，并重新 planTask；无关 Skill 的变化只记录为 out-of-scope，不扩大上下文。
