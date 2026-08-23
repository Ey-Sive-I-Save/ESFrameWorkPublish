# Skill 会话增量刷新

状态：现行工程路由；静态增量发现已实现，模型消费和 Unity 内运行回放待验收。

`KnowledgeId`: `es.engineering.skill-session-refresh.v1`
`Authority`: `Source + Skill contract`
`EvidenceLevel`: `S2`
`RouteKeys`: `skill`, `session`, `refresh`, `capability`, `delta`, `stale`, `routing`, `aibrain`, `understanding-drift`, `skill-understanding-refresh`, `capability-refresh`, `incremental-discovery`
`ContentHash`: `30c267bea295234dfacc59e87800701f23bd1d1760cb9fab0434752f4c69c911`
`StaleWhen`: Skill Catalog、Resource Index、Knowledge 路由、Skill 哈希、治理合同或当前会话 PlanHash 绑定变化。

`SourceRefs`:

- `.agents/skills/es-skill-session-refresh/SKILL.md` (`2eebecf99aec9151f186e123c6a55572e6c2752072fd29a759442edd03a502ed`)
- `.agents/skills/es-skill-session-refresh/governance.json` (`503b3e7da4abd56b95a29acc2cb356c5687119cd29351ca4fb0174b2a681d011`)
- `.agents/skills/es-skill-session-refresh/scripts/Invoke-ESSkillSessionRefresh.ps1` (`56e2a45a34d9a50a01a4f48ada81952602bffdfb2cf2e6c3b398969b70722906`)
- `.agents/skills/es-skill-session-refresh/references/session-refresh-contract.md` (`6fe00c95268b677c29bf21cbca2f5d9bddec77beaa2c4b8ce1c3d4316a138b29`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`5dedd6837210742df5fc4dd252ff7153a5b63e99a936f7b601819d5dd6aec205`)

`EvidenceRefs`: Skill StaticDeepReplay receipt；尚无 Unity 窗口内队列更新与 AIBrain 自动重新路由的 Runtime 回放证据。

## 职责

用户说“你的理解已经过时”“刷新一下技能理解”或等价表达时，AIBrain 应自动触发本能力，用户不需要点名 Skill。长运行 AI 窗口不应在每次队列更新时重新读取整个 Skill Portfolio。该能力先对 Resource Index、Catalog、Knowledge Index 和 Skill 资源做哈希级增量比较，再将变化与当前目标路由求交，只读取命中的变化项；未命中的变化只记录为 out-of-scope，不进入模型上下文。

绑定的 Skill、治理、AICommand、TaskContract、Knowledge SourceRef 或 Task Read Snapshot 发生变化时，旧 PlanHash 和旧结论必须标记 stale，并重新 planTask；无关 Skill 的变化只记录为 out-of-scope，不扩大上下文。
