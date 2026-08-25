# 游戏 UI 玩家目标与 IntentSpec 决策

`KnowledgeId`: `es.project.game-ui-player-intent-spec.v1`
`Authority`: `Governed UI intent Skill + IntentSpec contract + player-intent registry + validator source`
`RouteKeys`: `ui-automation`, `player-intent`, `player-goal`, `intent-spec`, `primary-action`, `ui-intent-clarification`, `business-bridge`
`HashSchema`: `v2`
`ContentHash`: `2262a2d4515411f263f11ff05347a3fce50e869868448c3258f592668f8d1731`
`SourceSetHash`: `2262a2d4515411f263f11ff05347a3fce50e869868448c3258f592668f8d1731`
`EntryBodyHash`: `07e1b8ab93aa88886973e85a60427f072b3d4f28cae9268179c432e9b202602f`
`EvidenceLevel`: `S1`
`StaleWhen`: IntentSpec schema、玩家意图 Registry、Validator、IntentSpec 到 ScreenSpec 的交接合同或任一 SourceRef 哈希变化。

## Scope and authority layers

本条目拥有从玩家自然语言目标到 `IntentSpec v1` 候选的语义收敛：区分目标与领域名词，选择
一个主动作、有限的辅助动作、已注册屏幕族、信息优先级、宽窄布局偏好、视觉状态、输入方式、
业务桥接标签和澄清条件。它是 `es-ui-intent-authoring` 的 canonical Knowledge owner，位于
ScreenSpec v3、Prefab 与 Fixture 物化之前。

本条目不拥有 ScreenSpec 组件树、Prefab/Scene 写入、Runtime Window、Presenter、输入绑定或
背包、经济、任务、战斗、保存等业务数据。`businessBridge` 只是未来集成标签，不是业务系统
存在或已连接的证据。IntentSpec 即使为 `confirmed`，也只表示静态语义候选满足当前合同；它
不授权 Unity 写入，不证明 ScreenSpec、布局、交互或视觉结果。

## Trigger and routing

- 自然语言触发：玩家想浏览、查看、选择、比较、装备、确认、取消、筛选、排序、领取、配置、
  追踪、回应、继续、重试或关闭某个 UI；把玩家目标转换为 UI 计划或 IntentSpec；询问主动作、
  缺失输入、澄清条件或业务桥接。
- 精确路由：`player-intent`、`player-goal`、`intent-spec`、`primary-action`、
  `ui-intent-clarification`、`business-bridge`。
- 只有“背包”“商店”“地图”等产品名词而没有动作时，不猜主动作；转到屏幕族知识并输出
  `needs-clarification`。只有 Runtime 菜单、输入绑定或业务逻辑时，转给相应 owner。
- 下游路由：目标确认后读取屏幕族决策；只有生成并验证 ScreenSpec v3 后，才进入 UI Prefab
  authoring 与 Materializer。

## IntentSpec minimum contract

| 字段 | 合同 | 停止条件 |
|---|---|---|
| `status` | `confirmed`、`needs-clarification` 或 `blocked` | 低于 `0.75` 的置信度、缺失输入或阻断条件不能标为 `confirmed` |
| `intentId` | 非空、小写、稳定语义 ID | 不能使用显示文案、瞬时对象 ID 或随机值 |
| `primaryAction` | Registry 中恰好一个动作 | 产品名词、多动作竞争或未注册动作必须澄清 |
| `secondaryActions` | 已注册、唯一，且不重复主动作 | 不能把另一个主要流程伪装成辅助动作 |
| `screenFamilies` | 至少一个已注册族，并与主动作兼容 | 不得发明 `inventory`、`shop` 等技术模板 |
| `informationPriority` | 非空语义槽位列表 | 不得塞入 item、price、stats 等业务 payload |
| `requiredStates` | Registry 中的视觉状态 | 缺失、未知或重复状态阻止交接 |
| `layoutPreferences` | 同时声明 `wide` 与 `narrow` | 只有单一截图或单一 profile 不算完整计划 |
| `inputModalities` | Registry 中的 pointer/keyboard/gamepad/touch | 这里只声明目标方式，不证明输入已绑定 |
| `businessBridge` | 稳定、非空的未来集成标签 | 不能伪装成 Presenter、数据源或运行回执 |
| `visualOnly` | 必须为 `true` | Runtime 行为或业务数据越界时 Validator 拒绝 |
| `missingInputs` / `blockedWhen` | 非确认状态必须解释原因 | 不得静默丢弃不确定性 |

当前 Registry 注册 17 个动作、10 个屏幕族、7 个状态与 4 种输入方式。精确集合必须从
`player-intent-registry.json` 读取，不能复制模型记忆或从 ScreenSpec 模板反推。

## Decision sequence

1. 从用户表述提取玩家要完成的动作，而不是把“背包、商店、地图、对话”直接当动作。
2. 对照 Registry 选择唯一 `primaryAction`；多个竞争动作时返回 `needs-clarification`。
3. 选择与主动作相容的最小屏幕族和信息优先级；未知族或未知动作返回 `blocked`。
4. 明确宽屏/窄屏布局偏好、required states、input modalities 与 future business bridge。
5. 保持 `visualOnly: true`，排除业务-shaped payload，并运行 `validate_intent_spec.py`。
6. 只有 Validator 返回 `passed` 且状态为 `confirmed`，才可把 IntentSpec 交给 ScreenSpec 设计。
7. 当前 `screen_spec_adapter.py` 不声明 IntentSpec 输入或自动转换合同；交接仍是显式设计步骤，
   不能把“可交接”写成已经存在确定性 adapter 或已经物化。

## Failure-surface matrix

### `UI-INTENT-001` noun promoted to action

- `severity`: `identity/authority`
- `erroneousBehavior`: 看见“背包”就选择 `equip`，或看见“商店”就选择购买/出售。
- `triggerAndSymptom`: 用户只描述界面名词，输出却是 `confirmed` 且含未经确认的主动作。
- `rootCause`: 混淆产品屏幕、玩家目标和业务变更。
- `preventionCheck`: 主动作必须有用户动词或明确确认；否则记录 missing input。
- `correctAction`: 返回 `needs-clarification`，提出一个有界的目标问题。
- `recoveryAction`: 废弃错误候选，移除由它派生的 ScreenSpec，再从原始目标重建。
- `evidencePresent`: IntentSpec contract、Registry 与 Validator 静态规则。
- `evidenceMissing`: 用户对具体主动作的确认。
- `sourceRefs`: Intent Skill、IntentSpec contract、player-intent registry、Validator。

### `UI-INTENT-002` business payload leakage

- `severity`: `permission/authority`
- `erroneousBehavior`: 为了填界面把价格、库存、属性、任务或存档值写入 IntentSpec。
- `triggerAndSymptom`: JSON 出现 Registry 禁止的业务字段，或 fixture 值被当成正式数据。
- `rootCause`: 把视觉计划当作 Presenter 或领域模型。
- `preventionCheck`: `visualOnly` 必须为 true；递归扫描 forbidden business field。
- `correctAction`: 只保留信息槽位和 `businessBridge` 标签，业务值交给独立 owner。
- `recoveryAction`: 清除泄漏字段并重新验证；任何下游产物标记 stale。
- `evidencePresent`: Validator 对未知字段、业务字段和 `visualOnly` 的拒绝路径。
- `evidenceMissing`: Runtime Presenter、领域数据和集成回执。
- `sourceRefs`: IntentSpec contract、player-intent registry、Validator。

### `UI-INTENT-003` clarification promoted to confirmed

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 低置信度、missing inputs 或 blocked conditions 仍进入物化。
- `triggerAndSymptom`: `confirmed` 与低于 `0.75` 的 confidence 或非空阻断字段同时出现。
- `rootCause`: 为推进自动化而丢弃不确定性。
- `preventionCheck`: 对每个候选运行 Validator，非确认状态必须解释原因。
- `correctAction`: 保持 `needs-clarification`/`blocked`，不调用 ScreenSpec/Materializer。
- `recoveryAction`: 停止下游，回到最近一次有效 IntentSpec 并补齐缺失输入。
- `evidencePresent`: Skill 与 Validator 的静态门禁。
- `evidenceMissing`: 玩家澄清或所需业务合同。
- `sourceRefs`: Intent Skill、IntentSpec contract、Validator。

### `UI-INTENT-004` handoff existence overstated

- `severity`: `identity/authority`
- `erroneousBehavior`: 把 confirmed IntentSpec 宣称为已自动转换、已生成 Prefab 或可运行 UI。
- `triggerAndSymptom`: 没有 ScreenSpec/Materializer 回执，却出现 Unity、视觉或 Runtime 完成声明。
- `rootCause`: 把语义候选、下游 adapter 和物化证据压平为同一状态。
- `preventionCheck`: 分别记录 Intent Validator、ScreenSpec Validator、Materializer 和视觉证据层。
- `correctAction`: 只报告 IntentSpec 静态通过；下游保持 `runtime-not-run`。
- `recoveryAction`: 撤回越级声明，按显式交接重新生成并验证后续工件。
- `evidencePresent`: 当前 Intent Validator 与 ScreenSpec adapter 静态边界。
- `evidenceMissing`: 确定性 IntentSpec-to-ScreenSpec consumer、Unity 物化与 GPU 证据。
- `sourceRefs`: Intent Skill、Validator、ScreenSpec adapter。

## Execution checklist

- 开始前：读取原始玩家目标、IntentSpec contract 与当前 Registry，确认目标不是单纯领域名词。
- 生成时：只选一个主动作，固定屏幕族、信息优先级、wide/narrow、states、modalities、bridge。
- 验证时：严格 JSON、无未知字段、无业务 payload、状态/置信度/阻断字段一致。
- 交接时：仅将 `confirmed` 且 Validator passed 的候选传给屏幕族与 ScreenSpec 设计。
- 报告时：Intent 静态证据与 ScreenSpec、Unity、视觉、Runtime、发布证据分层陈述。

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-player-intent-spec.md`
- `.agents/skills/es-ui-intent-authoring/SKILL.md`
- `.agents/skills/es-ui-intent-authoring/references/intent-spec.contract.md`
- `.agents/skills/es-ui-intent-authoring/references/player-intent-registry.json`
- `.agents/skills/es-ui-intent-authoring/scripts/validate_intent_spec.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`

## SourceRefs

- `.agents/skills/es-ui-intent-authoring/SKILL.md` (`d0532f5afb93df1b6371598819826b42d64063de1330501ac25633e776503cc3`)
- `.agents/skills/es-ui-intent-authoring/references/intent-spec.contract.md` (`b0244e91b116577ffb007ca2a91e423992335907d91e1f9c0c7213b36d29be10`)
- `.agents/skills/es-ui-intent-authoring/references/player-intent-registry.json` (`b51b4bdceb18b285aea9beb385a9101f0f733908370eb6847ad4c89dc7990578`)
- `.agents/skills/es-ui-intent-authoring/scripts/validate_intent_spec.py` (`498d1ae134997853f2b9c1d925e413b55afa08b333bbe8f7004266f416ea188b`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)

## Evidence boundary

本条目可证明当前 IntentSpec 字段、注册动作/屏幕族/状态/输入集合、静态拒绝条件与下游交接
边界。它不能证明用户已经确认歧义目标，不能证明 ScreenSpec 自动转换器、Prefab、Fixture、
Runtime Window、输入、业务数据、Unity 布局、视觉质量、Player 或发布通过。未运行的层统一为
`runtime-not-run`。
