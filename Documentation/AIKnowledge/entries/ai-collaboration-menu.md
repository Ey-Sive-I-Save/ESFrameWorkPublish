# AI 协作菜单与只读路由边界

`KnowledgeId`: `es.ai.collaboration-menu.v1`  
`Authority`: `es-ai-collaboration-menu contract and current project routing policy`  
`RouteKeys`: `menu`, `collaboration-menu`, `guidance`, `creation`, `iteration`, `framework-governance`, `evidence`, `context-discovery`, `session-coordination`  
`HashSchema`: `v2`  
`ContentHash`: `38760a9c65b60d06aec5ef184fbb072dab538af25b293bd9b25379ea1c04987e`
`SourceSetHash`: `38760a9c65b60d06aec5ef184fbb072dab538af25b293bd9b25379ea1c04987e`  
`EntryBodyHash`: `89505c519a82e7cf4ad1387d4d733922731b4e10f454e84c8b2febc063006c2f`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 菜单合同、选项表、会话子菜单、只读边界、验证器或任一 SourceRef 哈希变化。

## Scope

本条目只负责发现 `es-ai-collaboration-menu` 的确定性只读菜单与路由边界。它不执行菜单选项，不授予写入、Runtime、Git、网络、发布、会话 Fork 或窗口 Handoff 权限，也不把推荐项解释为用户选择。

## Routing contract

- `menu`、`collaboration-menu` 和 `guidance` 进入本条目。
- `creation`、`iteration`、`framework-governance`、`evidence`、`context-discovery` 和 `session-coordination` 只选择后续领域路由，不执行领域动作。
- 主菜单和子菜单必须使用稳定 ID；数字回复只是候选选择，仍由对应交互或领域入口解析。
- `session-fork` 与 `window-handoff` 保持不同语义。菜单只能转交 `es-codex-session-bootstrap`，不能自行启动、复制或关闭会话。

## Verified facts

- Skill 的执行模式是 `read-only-presentation`，`writePolicy` 为 `report-only`，`commandRequirement` 为 `none`。
- 菜单选项由 `menu-options.json` 提供稳定 ID、routeKeys、relatedSkills 和风险说明。
- 会话子菜单显式区分 Fork 与 Handoff；渲染器和静态测试只产生菜单及非持久决策回执。
- 当前静态验证器覆盖正常输入、非法输入、拒绝扩权、重复/幂等和确定性输出；这些证据不证明用户选择质量或任何 Runtime 行为。

## Decision rules

1. 菜单命中只能产生 `route-candidate` 或只读展示，不得产生业务执行、项目写入或完成状态。
2. 多个领域可能命中时必须保留可见选项或进入 `discover-context`，不得静默猜测执行顺序。
3. 选择涉及会话、Runtime、外部进程、网络、Git、发布或删除时，必须转交对应 Skill，并重新应用该动作的当前授权和合同。
4. 缺失或过期上下文只影响推荐与路由可信度，不能扩大成项目级 hard-block，也不能缩小当前用户明确请求。

## Failure prevention

| failureId | severity | erroneousBehavior | triggerAndSymptom | rootCause | preventionCheck | correctAction | recoveryAction | evidencePresent | evidenceMissing | sourceRefs |
|---|---|---|---|---|---|---|---|---|---|---|
| `menu-selection-auto-dispatch` | identity/authority | 把数字选择直接执行为写入或 Runtime | 用户回复编号后立即启动工具或修改项目 | 混淆路由候选与动作授权 | 检查输出只含稳定 ID、routeKeys 和 `requiresUserChoice` | 将选择交给对应解析/领域入口 | 停止副作用并重新确认目标动作 | menu contract、options、静态测试 | 真实宿主交互证据 | menu contract owner |
| `session-operation-collapsed` | lifecycle/partial | 把 Fork 与 Handoff 当成同一操作 | 菜单选择后复制或关闭了错误会话 | 忽略会话子菜单的身份与生命周期差异 | 校验 `session-fork` 与 `window-handoff` ID 和说明不同 | 只路由到 session Skill | 丢弃未执行候选并重新选择 | session submenu | 真实会话操作回执 | session submenu owner |
| `ambiguous-route-guessed` | recoverable | 多命中时静默选择一个领域 | 菜单未展示歧义且直接给出执行路线 | 把推荐排序当作唯一决策 | 校验完整有界选项和 `discover-context` 路径 | 展示候选或收集有界上下文 | 以相同输入重新渲染 | options、renderer test | 用户选择质量 | menu owner |
| `menu-status-promoted` | identity/authority | 把静态菜单通过写成项目或 Runtime Accepted | 静态测试绿灯后生成全局完成声明 | 跨 Profile 投影 Evidence | 检查 `runtime-not-run` 与 non-claims 保留 | 只声明菜单静态合同范围 | 撤回扩大声明并重发 scoped 结论 | Skill contract、validator | Runtime/Release evidence | Skill governance owner |

## External calibration

不适用。本条目只描述当前项目内 Skill、JSON 合同和确定性验证器，不包含版本敏感的外部 API 或供应商事实。

## Evidence boundary and non-claims

静态证据只证明菜单结构、稳定选项、只读边界和确定性回放。它不证明用户会选择正确、不证明后续 Skill 已执行，也不证明 Unity、外部进程、会话切换、Runtime 或发布行为。

## SourceRefs

- `.agents/skills/es-ai-collaboration-menu/SKILL.md` (`d67b94d06759e8e0cd39c39e82b6ba76f5098b47ab2efe7a789047a78f496bf8`)
- `.agents/skills/es-ai-collaboration-menu/governance.json` (`d67de3e817770c82951afc2a0bcecce0fbb5f4f42ff747072c578ed5b390b3e2`)
- `.agents/skills/es-ai-collaboration-menu/references/menu-contract.md` (`4bb1b8112eadfa2f074f9f18d8d1c2c38a9a13fc477b6d63ce91f92df80103c0`)
- `.agents/skills/es-ai-collaboration-menu/references/menu-options.json` (`80b3848719c75668a1018dcb830252a15045f9bf3b2218d35b47dd248301338d`)
- `.agents/skills/es-ai-collaboration-menu/references/session-submenu.json` (`a1fbc730f8d0091054822139a209494c6f63616a7dda324a6adc8e2a0d9e90b3`)
- `.agents/skills/es-ai-collaboration-menu/scripts/Test-ESCollaborationMenu.ps1` (`ff90394600b8e83069ca34f6ee6e25d4109c058dfcf0bc9d1b441ef451d3f55a`)

## RequiredReads

- `Documentation/AIKnowledge/entries/ai-collaboration-menu.md`
- `.agents/skills/es-ai-collaboration-menu/SKILL.md`
- `.agents/skills/es-ai-collaboration-menu/references/menu-contract.md`
- `.agents/skills/es-ai-collaboration-menu/references/menu-options.json`
- `.agents/skills/es-ai-collaboration-menu/references/session-submenu.json`

## StaleWhen

菜单合同、选项表、会话子菜单、Skill 治理元数据、静态验证器或任一 SourceRef 哈希变化。
