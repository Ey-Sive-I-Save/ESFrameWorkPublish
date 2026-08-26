# 商业游戏 UI 屏幕族决策

`KnowledgeId`: `es.project.game-ui-screen-family-decisions.v1`
`Authority`: `Current project source + governed UI authoring contracts + derived screen-family decisions`
`RouteKeys`: `ui-automation`, `game-ui-screen-family`, `commercial-ui`, `hud-ui`, `inventory-ui`, `shop-ui`, `dialogue-ui`, `map-ui`, `progression-ui`, `result-ui`, `settings-ui`, `ui-information-architecture`
`HashSchema`: `v2`
`ContentHash`: `31688f658b350df9cb0673b15aa5e67654e75df06f4532e2a544dd3c7ed68485`
`SourceSetHash`: `31688f658b350df9cb0673b15aa5e67654e75df06f4532e2a544dd3c7ed68485`
`EntryBodyHash`: `c77910234aab1d80355ae501d6323a9d8b5e75fc5898fbc52f3a3d7120f0d90f`
`EvidenceLevel`: `S0`
`StaleWhen`: ScreenSpec v3、组件注册表、Validator、Adapter、Materializer、现有 UI canonical 条目、官方来源锁或任一 SourceRef 哈希变化。

## Scope and authority layers

本条目拥有产品语言中的 HUD、背包、商店、对话、地图、技能/任务、结果页和设置页到
当前十个通用 ScreenSpec 模板的受限映射，以及信息架构、状态覆盖和停止条件。映射属于
设计级派生决策，因此条目整体保持 `S0`；Registry、Validator、Adapter 与 Materializer 的
当前静态事实可从 SourceRefs 复核。

本条目不拥有背包容量、装备规则、货币结算、任务进度、对话分支、寻路、输入、导航栈、
保存或 Runtime Window 业务事实。它也不创建同名专用模板。当前 Registry 只注册：
`hud`、`navigation`、`modal`、`conversation`、`progression`、`collection`、`combat`、
`world`、`system`、`result`。

`es-ui-intent-authoring` 拥有玩家目标、单一 primary action、IntentSpec 状态与置信度门禁；
本条目只拥有确认目标之后的产品屏幕族映射与信息架构选择。名词“背包”或“商店”本身不授权
推断 `equip`、`buy`、`sell` 等业务动作；动作不明确时先生成 `needs-clarification`，不能直接物化。

Unity 官方资料只校准 Canvas、CanvasScaler、Auto Layout 与 EventSystem 的底层边界；它不定义
ES 的屏幕族。所有屏幕族映射都是对当前项目能力的显式派生，不是供应商事实。

## Trigger and routing

- 产品屏幕词：HUD、背包、仓库、图鉴、配装、商店、对话、地图、技能树、任务页、结算页、设置页、主菜单。
- 决策词：屏幕族、信息架构、主动作、模板选择、ScreenSpec、宽屏/窄屏、状态矩阵。
- 邻近路由：组件注册细节追加 `es.editor.project-screen-spec-materializer.screen-spec-components.v1`；Canvas 或交互机制追加对应 Unity UI canonical 条目；总量保持 1 至 3 条。
- 只有经济、任务、地图、对话或设置业务逻辑而没有 UI/ScreenSpec 意图时，不命中本条目。

## Screen-family mapping

| 产品屏幕 | 当前首选模板 | 最小信息架构 | 必须停止或拆分的情况 |
|---|---|---|---|
| HUD | `hud` | Safe Area、状态/资源读数、上下文提示 | 主动战斗操作占主导时改用 `combat`；HUD 不实现战斗输入 |
| 战斗覆盖层 | `combat` | Safe Area、目标信息、技能/冷却、行动区 | 背包、技能树或结算必须拆成独立屏幕 |
| 背包/仓库/图鉴/配装 | `collection` | 分类、Grid/List、选择、Detail、主操作 | 装备规则、容量、货币和保存由业务合同提供 |
| 商店 | `collection` + 独立 `modal` | 浏览/筛选、商品详情、购买入口、确认/错误弹窗 | 当前没有 `shop` 模板；库存、支付、价格结算不得由 fixture 发明 |
| 对话/选择 | `conversation` | 说话者、正文、选项、字幕 | 分支状态、条件和跳转不由 ScreenSpec 推断 |
| 世界地图 | `world` | 地图、标记、筛选/列表、Overlay | 缩放、寻路、传送权限和地图数据属于其他 owner |
| 技能树 | `progression` | 进度、节点/列表、详情、消耗提示 | Registry 没有树图节点/连线；真实树图先扩展整条消费者链 |
| 任务页 | `progression` | 任务列表、进度、奖励、主操作 | 多分类导航超出组件集合时拆导航壳或先扩展 Registry |
| 结果/奖励页 | `result` | 结果摘要、统计、奖励、继续/退出 | 奖励发放和重复领取保护必须来自业务回执 |
| 设置页 | `system` | 分类、设置项、滑杆/开关/下拉、应用/恢复 | 持久化、即时生效、设备能力和默认值不能猜测 |
| 主菜单/功能导航 | `navigation` | Header、Content、Tab/入口、焦点路径 | 深层导航栈和返回策略需要独立行为合同 |
| 确认/警告/阻断弹窗 | `modal` | 标题、正文、主次操作、Loading/Error | 复杂多步骤流程不能塞入单个 Modal |

## Decision rules

1. 先用 IntentSpec 固定用户目标、一个 primary action、缺失输入和业务 owner，再选模板；不能按截图中最显眼的组件或产品名词猜动作。
2. 一个 ScreenSpec 只声明一个主模板。浏览加确认拆为主屏与 Modal，不发明未注册的混合模板。
3. 内容密集屏保持“导航/筛选 -> 列表或网格 -> 选中详情 -> 主操作”的可复查阅读顺序。
4. 宽屏可以使用列表/网格与 Detail 双区；窄屏应分步、折叠或重排，不能把双栏整体压缩。
5. HUD/Combat 优先 Safe Area、边缘锚定和遮挡预算；World 优先可检查的地图主舞台与 Overlay。
6. 每个交互集合至少声明 `default`、`selected`、`disabled`；异步内容补 `loading`、`empty`、`error`，文本/列表补 `long-content`。
7. 当前 Validator 接受的固定 state 集不含 `missing-art`。素材缺失只能用 AssetManifest fallback/阻断证据表达，直到 Validator、Adapter 和 Materializer 同步扩展。
8. 找不到注册模板、组件、profile 或 state 时返回 `Blocked`/`Deferred`，并指出需要同步修改的 Registry、Validator、Adapter、Materializer 和测试。

## Responsive minimums

| 屏幕形态 | 宽屏 | 窄屏/竖屏 | 共同约束 |
|---|---|---|---|
| HUD/Combat | 信息分散到安全区边缘，保留中央玩法视野 | 合并次要信息，保持焦点/触控目标 | 不按单张横屏截图整体缩放 |
| Collection/Shop/Progression | 列表或网格 + Detail 双区 | 列表与 Detail 分步或折叠，主操作持续可达 | 长文本、空列表和缺图必须有 fixture/fallback 结论 |
| World | 地图主舞台 + 边缘 Overlay | Overlay 可折叠，地图仍为主区域 | Marker、Tooltip 与安全区不得互相遮挡 |
| Modal/Result/System | 受限最大宽度与明确 ActionBar | 正文可滚动，操作区不裁剪 | 键鼠/手柄焦点与移动端触达分别验证 |

CanvasScaler 的 Reference Resolution 与宽高匹配必须随 profile 记录。官方文档证明这些参数会影响
缩放，不证明当前 Prefab 的取值正确；Safe Area 也不能由单一分辨率截图推断。

## Failure-surface matrix

### `UI-SF-001` unsupported or invented template

- `severity`: `identity/authority`
- `erroneousBehavior`: 把 `shop`、`inventory` 或 `skill-tree` 写成已经注册的模板。
- `triggerAndSymptom`: Validator 报 template 未注册，或 Adapter/Materializer 退化为通用 panel 后仍宣称专用语义存在。
- `rootCause`: 产品屏幕名被错误等同于技术模板 identity。
- `preventionCheck`: 从 `game-ui-component-registry.json` 精确读取模板 key，并运行 Validator。
- `correctAction`: 使用表中的受限映射，或先同步扩展 Registry、Validator、Adapter、Materializer 与测试。
- `recoveryAction`: 丢弃虚构模板 spec，回到最近一次可验证 ScreenSpec，再重新选择模板。
- `evidencePresent`: 当前 Registry、Validator 与 Adapter 静态源码。
- `evidenceMissing`: 新模板消费者、Unity 物化和视觉回执。
- `sourceRefs`: Registry、Validator、Adapter、Materializer。

### `UI-SF-002` mixed screen responsibilities

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 在一个 ScreenSpec 中混合浏览、支付、奖励发放或复杂导航，并把视觉 fixture 当业务流程。
- `triggerAndSymptom`: 一个模板无法覆盖 required zones，状态切换重排整屏，或 fixture 包含无 owner 的业务结果。
- `rootCause`: 未先分离主屏、Modal 与业务 owner。
- `preventionCheck`: 每个 spec 只保留一个主模板、一个首要动作和显式业务依赖清单。
- `correctAction`: 拆分屏幕与 Modal；把经济、任务、保存等交给各自合同。
- `recoveryAction`: 保留纯展示结构，移除未绑定业务字段，并将缺口标记 `Deferred`。
- `evidencePresent`: 当前模板 required zones 与 UI automation 边界。
- `evidenceMissing`: 目标业务系统 Presenter/回执。
- `sourceRefs`: Registry、Materializer contract、UI automation entry。

### `UI-SF-003` incomplete profile or state matrix

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 只设计默认宽屏，把 selected/disabled/loading/empty/error/long-content 当隐式状态。
- `triggerAndSymptom`: 窄屏压缩双栏、操作区裁剪、局部状态导致整屏跳动，或缺失 fixture capture key。
- `rootCause`: profile/state 没有作为输入维度固定。
- `preventionCheck`: Validator 检查非空唯一 profile/state；按本条目最小矩阵逐项列出支持与缺口。
- `correctAction`: 为每个支持 profile/state 写独立布局或变体；不支持项显式 `Blocked`/`Deferred`。
- `recoveryAction`: 撤回部分通过结论，补全缺失 fixture 后重新生成对应证据。
- `evidencePresent`: Validator 对 profile/state 的静态检查和 Materializer 的 capture 循环。
- `evidenceMissing`: 当前目标全部 profile/state 的 Unity/GPU 产物。
- `sourceRefs`: Validator、Materializer、official source lock。

### `UI-SF-004` business facts invented by fixture

- `severity`: `identity/authority`
- `erroneousBehavior`: AI 为了填满界面自行生成价格、库存、奖励、任务进度、传送权限或设置默认值并当成正式事实。
- `triggerAndSymptom`: ScreenSpec 含无 SourceRef/owner 的业务值，视觉回执被下游当成可提交状态。
- `rootCause`: 把确定性 mock data 与业务权威混为一体。
- `preventionCheck`: 每个业务字段标记 fixture/mock 或绑定明确 owner 与回执。
- `correctAction`: 保留确定性占位数据，只用于布局；正式值由业务 Presenter/合同注入。
- `recoveryAction`: 清除未经绑定的正式声明，重建 fixture 数据边界。
- `evidencePresent`: Materializer contract 明确不拥有 business state。
- `evidenceMissing`: 各业务模块当前源码与 Runtime 回执。
- `sourceRefs`: Materializer contract、Materializer source、UI automation entry。

### `UI-SF-005` static evidence promoted to visual acceptance

- `severity`: `identity/authority`
- `erroneousBehavior`: 因 Validator 通过、Prefab 路径存在或 PNG 非空就宣称布局与视觉通过。
- `triggerAndSymptom`: 没有绑定同一 spec/profile/state 的 GPU capture、结构快照和 baseline，结论却写为 Accepted。
- `rootCause`: 混淆 Static、Runtime 与 Visual evidence。
- `preventionCheck`: 逐层列出实际证据；缺少当前 Unity 回执时强制 `runtime-not-run`。
- `correctAction`: 只报告 schema/源码静态闭包；视觉验收另运行正式工作流。
- `recoveryAction`: 降级错误完成声明，使旧截图/报告 stale，并重新绑定输入身份。
- `evidencePresent`: 当前静态源码、合同和 SourceRef 哈希。
- `evidenceMissing`: Unity 导入、布局、交互与 GPU capture。
- `sourceRefs`: UI automation entry、Materializer contract、Materializer source。

### `UI-SF-006` source or index drift

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 在 Registry、条目或 Index 被并发修改后沿用旧映射或旧哈希。
- `triggerAndSymptom`: SourceRef、SourceSetHash、EntryBodyHash 或 Index binding 不一致；路由结果使用混代内容。
- `rootCause`: 将一次读取或旧候选 receipt 当持续 lease。
- `preventionCheck`: 写前后重读全部 SourceRefs、目标 Entry 与唯一 Index block；运行 Entry/Index validator。
- `correctAction`: 标记旧计划 stale，从当前文件重建有界补丁和哈希。
- `recoveryAction`: 停止后续批次，补齐同一代 Entry/Index 或保留现场等待冲突裁决。
- `evidencePresent`: v2 哈希与当前验证器合同。
- `evidenceMissing`: 跨进程互斥和 crash-safe 多文件提交机制。
- `sourceRefs`: 本条目全部 SourceRefs。

## Execution checklist

- 开始前：确认产品目标、主动作、业务 owner、模板、required zones、正式输出路径和证据层。
- 设计时：固定 profile/state 矩阵、长内容、空/加载/错误、素材 fallback、Safe Area 与输入方式。
- 物化前：运行 ScreenSpec Validator；Adapter/Materializer 不支持的语义保持 `Blocked`/`Deferred`。
- 完成后：分别报告 Static、Runtime、Visual；复算 SourceRefs、v2 哈希和唯一 Index binding。
- 禁止：虚构模板或业务事实、用局部状态重排整屏、用静态检查冒充 Unity/视觉通过。

## Failure feedback from lobby evidence

The lobby evidence exposed a routing failure, not merely a styling defect. Before selecting a
template, apply `UI-FB-002` and `UI-FB-003` from
`.agents/skills/es-ui-prefab-authoring/references/ui-failure-feedback-rules.md`: choose the
screen family from player intent, define one primary action and required zones, then author
separate wide/narrow reflow constraints. A generic navigation template with a hero image is not
evidence of a commercial lobby. If the next ScreenSpec does not change the diagnosed family,
hierarchy, focal-art brief or profile constraints, stop with `feedback-not-incorporated`.

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-screen-family-decisions.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/Editor/project-screen-spec-materializer/screen-spec-component-registration.md`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md`
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md`
- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `.agents/skills/es-ui-intent-authoring/SKILL.md`
- `.agents/skills/es-ui-intent-authoring/references/intent-spec.contract.md`
- `.agents/skills/es-ui-intent-authoring/references/player-intent-registry.json`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`

## SourceRefs

- `.agents/skills/es-ui-intent-authoring/SKILL.md` (`d0532f5afb93df1b6371598819826b42d64063de1330501ac25633e776503cc3`)
- `.agents/skills/es-ui-intent-authoring/references/intent-spec.contract.md` (`b0244e91b116577ffb007ca2a91e423992335907d91e1f9c0c7213b36d29be10`)
- `.agents/skills/es-ui-intent-authoring/references/player-intent-registry.json` (`b51b4bdceb18b285aea9beb385a9101f0f733908370eb6847ad4c89dc7990578`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`92a7bbc479e1c056bb3b8993a7e9c2d3fccaefbfe990b45775bfc66871364277`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`ca98805423cd18b3e55d861041e6e48a73c91eeb70a36c9699de8d0566252fb1`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`7e64233bdcfd1e783a74085c456feab2409bb72511868e9b73e7176b566df3e3`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `.agents/skills/es-ui-prefab-authoring/references/ui-failure-feedback-rules.md` (`79e862b0b2da00b892270bbff342f64b9602bcb4c8e4c4be4092cfc8f993ca0a`)

## Evidence boundary

本条目可证明当前 Registry/Validator/Adapter/Materializer 声明的静态模板、组件、profile/state
输入和证据边界，并给出可回读的屏幕族映射。它不能证明专用背包、商店、技能树或设置系统
存在，不能证明 Presenter、输入、动画、资源解析、Unity 布局、视觉质量、Player 或发布通过。
没有当前运行回执时统一为 `runtime-not-run`。
