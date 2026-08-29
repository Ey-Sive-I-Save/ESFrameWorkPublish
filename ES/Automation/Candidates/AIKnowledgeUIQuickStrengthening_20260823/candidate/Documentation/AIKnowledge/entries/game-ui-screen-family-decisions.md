# 商业游戏 UI 屏幕族决策

状态：候选 canonical 条目；尚未注册到 `KnowledgeIndex.yaml`。

`KnowledgeId`: `es.project.game-ui-screen-family-decisions.v1`
`Authority`: `Derived from current UI authoring contracts and advisory references`
`EvidenceLevel`: `S0`
`RouteKeys`: `ui-automation`, `game-ui-screen-family`, `commercial-ui`, `hud-ui`, `inventory-ui`, `shop-ui`, `dialogue-ui`, `map-ui`, `progression-ui`, `result-ui`, `settings-ui`, `ui-information-architecture`
`ContentHash`: `f5b4b056c385d5412283bdc72febd1813158ac4f7f8b2c3867f4d3a14745fa58`
`StaleWhen`: 组件注册表、ScreenSpec v3、Materializer 合同、商业 UI 参考、现有 UI canonical 条目或任一 SourceRef 哈希变化。

`RequiredReads`:

- `Documentation/AIKnowledge/entries/game-ui-screen-family-decisions.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md`
- `.agents/skills/es-ui-prefab-authoring/references/high-fidelity-ui-recipes.md`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md`

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`3a9f45d41d00437f7484438ee0215440012f0de8b6660a1fefe2120fc429096e`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `.agents/skills/es-ui-prefab-authoring/references/high-fidelity-ui-recipes.md` (`f32b5e00263aca1bc6f7b3cd7d7116d09ee3df61aaeb378cccb317eadb100390`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`26e8b48610eae75c56e8dfc8d05638fb4508c28ddc04919bdb0f197576bb35a6`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`92def4cdbd7a83f9ae93764cf6f49019e6bfdedf260af3f0fb453ce610eb6541`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`6c6a0a27a3a926d75eacdb642ace3472c69821710edf284a0ab5357ec3311721`)

## Scope

本条目把产品语言中的 HUD、背包、商店、对话、地图、技能/任务、结果页和设置页，映射到当前已经注册的通用 ScreenSpec 模板，并给出信息架构、布局变体、状态覆盖和停止条件。映射和决策规则属于设计级派生结论，因此条目整体标记为 `S0`；其中引用的 Registry、Validator、Adapter 与 Materializer 现状仍可按 SourceRef 做静态复核。它只拥有屏幕分类与作者决策，不拥有背包、经济、任务、战斗、导航、输入或 Runtime Window 的业务事实。

当前注册表只有 `hud`、`navigation`、`modal`、`conversation`、`progression`、`collection`、`combat`、`world`、`system`、`result` 十个通用模板。下表是受限映射，不代表存在同名专用模板或完整业务系统。

`ui-automation` 只是与当前 AIBrain 推导规则的兼容桥接；产品屏幕的 canonical 语义仍由本条目的专用 routeKeys 拥有。`high-fidelity-ui-recipes.md` 是咨询性参考，其中提到的 `generate_ui_authoring_packet.py` 不在当前 Skill 资源清单中，不能作为可执行步骤或验收前提。

## 屏幕族映射

| 产品屏幕 | 当前首选模板 | 最小信息架构 | 必须停止或拆分的情况 |
|---|---|---|---|
| HUD | `hud` | Safe Area、状态/资源读数、上下文提示 | 主动战斗操作占主导时改用 `combat`；不能把 HUD 当战斗输入实现 |
| 战斗覆盖层 | `combat` | Safe Area、目标信息、技能/冷却、行动区 | 需要背包、技能树或结算时拆成独立屏幕 |
| 背包/仓库/图鉴/配装 | `collection` | 分类、Grid/List、选择、Detail、主操作 | 装备规则、容量、货币与保存均是业务合同，不得写进视觉 fixture |
| 商店 | `collection` + 独立 `modal` | 浏览/筛选、商品详情、购买入口、确认/错误弹窗 | 当前没有 `shop` 模板；价格结算、库存、支付或刷新逻辑必须由经济系统提供 |
| 对话/选择 | `conversation` | 说话者、正文、选项、字幕 | 分支剧情状态与跳转不由 ScreenSpec 推断 |
| 世界地图 | `world` | 地图、标记、筛选/列表、Overlay | 缩放、寻路、传送权限和地图数据来源不属于本条目 |
| 技能树 | `progression` | 进度、节点/列表、详情、消耗提示 | Registry 没有分支图节点/连线组件；需要真实树图时先扩展 Registry、Validator、Adapter、Materializer |
| 任务页 | `progression` | 任务列表、当前进度、奖励、主操作 | 多分类导航若超出模板组件集合，应拆分导航壳或先扩展 Registry |
| 结果/奖励页 | `result` | 结果摘要、统计、奖励、继续/退出 | 奖励发放和重复领取保护必须来自业务回执 |
| 设置页 | `system` | 分类、设置项、滑杆/开关/下拉、应用/恢复 | 持久化、即时生效、设备能力和默认值不由 UI 猜测 |
| 主菜单/功能导航 | `navigation` | Header、Content、Tab/入口、焦点路径 | 深层导航栈和返回策略需要独立行为合同 |
| 确认/警告/阻断弹窗 | `modal` | 标题、正文、主次操作、Loading/Error | 不得把复杂多步骤流程塞进单个 Modal |

## 决策规则

1. 先写用户目标和首要动作，再选模板；不能按截图里最显眼的组件猜 screen family。
2. 一个 ScreenSpec 只保留一个主模板。需要浏览加确认时拆成主屏与 Modal，而不是发明未注册的混合模板。
3. 内容密集屏优先固定阅读顺序：导航/筛选 -> 列表或网格 -> 选中详情 -> 主操作。窄屏不能保持不可读的压缩双栏，应声明列表与详情的布局变体。
4. HUD 和战斗覆盖层以 Safe Area、边缘锚定和遮挡预算为先；地图以可检查的主舞台和 Overlay 为先；结果页与 Modal 以清晰的主操作层级为先。
5. 每个交互集合必须声明 `default`、`selected`、`disabled`；异步内容补 `loading`、`empty`、`error`；文本或列表补 `long-content`。素材缺失当前只能作为 AssetManifest fallback/阻断证据条件；若需要专用 `missing-art` fixture state，保持 `Deferred`，直到 Validator、Adapter 与 Materializer 同步支持。
6. 选择、禁用、焦点和错误必须保持同一组件语法。不能通过重排整屏来表达一个局部状态。
7. 找不到注册模板、组件类型、profile 或状态时返回 `Blocked`/`Deferred`，并指出需要扩展的 Registry、Validator、Adapter 和 Materializer；不得临时拼层级冒充支持。

## 响应式最低决策

| 屏幕形态 | 宽屏 | 窄屏/竖屏 | 共同约束 |
|---|---|---|---|
| HUD/Combat | 分散到安全区边缘，保留中央玩法视野 | 合并次要信息，保持触控/焦点目标尺寸 | 不按单张横屏截图整体缩放 |
| Collection/Shop/Progression | 可使用列表/网格 + Detail 双区 | 列表与 Detail 分步或折叠，主操作持续可达 | 长文本、空列表必须有 fixture；缺图必须有 fallback/阻断证据 |
| World | 地图主舞台 + 边缘 Overlay | Overlay 可折叠，地图仍是主区域 | Marker、Tooltip 与安全区不得互相遮挡 |
| Modal/Result/System | 受限最大宽度与明确 ActionBar | 允许正文滚动，操作区不被裁剪 | 键鼠/手柄焦点与移动端触达分别验证 |

## Evidence Boundary

本条目可证明当前 Registry、Validator、Adapter 与 Materializer 源码声明的静态模板、组件和 fixture 语义，并给出从产品屏幕到现有能力的可复查映射。它不能证明专用背包/商店/技能树系统存在，也不能证明 Presenter、输入、动画、资源解析、Unity 布局、视觉质量、Player 或发布通过。没有当前 Unity 回执时统一为 `runtime-not-run`。
