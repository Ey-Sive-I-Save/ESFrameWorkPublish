# 游戏 UI 视觉设计与 Token 决策

状态：候选 canonical 条目；尚未注册到 `KnowledgeIndex.yaml`。

`KnowledgeId`: `es.project.game-ui-visual-design-system.v1`
`Authority`: `Derived from current UI authoring contracts and advisory references`
`EvidenceLevel`: `S0`
`RouteKeys`: `ui-automation`, `visual-qa`, `ui-visual-design`, `visual-design`, `design-token`, `color-role`, `typography-role`, `spacing-token`, `visual-hierarchy`, `information-density`, `rarity-visual`, `ui-material`
`ContentHash`: `bd830fc2270037fdd67c65d9e11cb83af48b1b1f0a2aba75c7f3dae068561807`
`StaleWhen`: 视觉参考、ScreenSpec v3 token schema、字体/素材治理、项目渲染约束或任一 SourceRef 哈希变化。

`RequiredReads`:

- `Documentation/AIKnowledge/entries/game-ui-visual-design-system.md`
- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `.agents/skills/es-ui-prefab-authoring/references/commercial-ui-patterns.md`
- `.agents/skills/es-ui-prefab-authoring/references/high-fidelity-ui-recipes.md`
- `.agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md`

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`3a9f45d41d00437f7484438ee0215440012f0de8b6660a1fefe2120fc429096e`)
- `.agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md` (`744e99b7f133a90b8ee6ff11208717511f37a352a37c9f25d7ddb5c9fc220f6b`)
- `.agents/skills/es-ui-prefab-authoring/references/commercial-ui-patterns.md` (`85579b0750d426fc9f615b60d488ff90626956a6f68784ff232175fba5e5c248`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json` (`ef0e99dee528eb741ea8c785dde4298c153de13b52d63748a15694d563bca3cf`)
- `.agents/skills/es-ui-prefab-authoring/references/high-fidelity-ui-recipes.md` (`f32b5e00263aca1bc6f7b3cd7d7116d09ee3df61aaeb378cccb317eadb100390`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`26e8b48610eae75c56e8dfc8d05638fb4508c28ddc04919bdb0f197576bb35a6`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`92def4cdbd7a83f9ae93764cf6f49019e6bfdedf260af3f0fb453ce610eb6541`)
- `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md` (`89f25310ac0bd84fcd7b643670ab8daa6b4678ac5c359c658b62d6111e4dc030`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`6c6a0a27a3a926d75eacdb642ace3472c69821710edf284a0ab5357ec3311721`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)

## Scope

本条目拥有 UI 视觉角色、Token 命名、层级、密度、材质克制、状态一致性与素材视觉合同。Token 命名、字体角色和密度档属于设计级派生决策，因此条目整体标记为 `S0`；其中引用的 schema、Validator、Adapter 与 Materializer 现状仍可按 SourceRef 做静态复核。它定义 AI 应如何做可复用的视觉决策，不把某个示例颜色、字号或截图升级成项目品牌事实，也不拥有 Canvas、Prefab、输入、资源生命周期或 Runtime Window 实现。

当前 ScreenSpec v3 模板只给出 `surface`、`text`、`accent` 三个示例值；它们是模板样例，不是已验收的项目 Token 系统。当前来源也没有闭合字体资产、Fallback 链、许可证、Atlas Owner 或项目稀有度词表，因此这些字段在取得权威来源前必须保持显式缺口。

`ui-automation` + `visual-qa` 只是与当前 AIBrain 视觉意图推导的兼容桥接，不表示本条目拥有视觉验收事实。`high-fidelity-ui-recipes.md` 是咨询性参考，其中提到的 `generate_ui_authoring_packet.py` 不在当前 Skill 资源清单中，不能执行或作为完成证据。

## Token 最小合同

| 命名空间 | 负责内容 | 决策规则 |
|---|---|---|
| `surface.*` | 页面、面板、浮层、选中面 | 用层级和明度区分所有权，禁止为单屏偏移创建近重复色 |
| `text.*` | 主文、次文、弱化、反色、禁用 | 对比与最大行数必须随状态/profile 验证，不能只凭字号强调 |
| `action.*` | 主操作、次操作、危险操作、焦点 | 同一屏只保留一个主要动作层级；颜色不能是唯一状态信号 |
| `feedback.*` | 成功、警告、错误、信息、加载 | 保留文案/图标/结构信号，避免只靠红绿区分 |
| `type.*` | 标题、正文、标签、说明、数值 | 角色数量保持有限；真实字体、字重和 Fallback 必须绑定资产来源 |
| `space.*` | 区域间距、组件间距、行内间距 | 使用离散阶梯，不用一次性 offset 修复父级布局错误 |
| `radius.*` / `border.*` / `elevation.*` | 轮廓、分组和层级 | 先用间距、对比和分组，阴影/辉光不能掩盖结构问题 |
| `icon.*` | 功能、状态、货币、输入提示 | 图标盒、像素光学中心、交互矩形和 Atlas Owner 分开声明 |
| `rarity.<project-tier>` | 稀有度视觉角色 | tier 名称和排序必须来自业务配置；AI 不得自行假设 common/rare 等词表 |

Token 保存语义角色，具体值由项目视觉资产或已批准规范提供。上述命名空间目前只是知识层的决策词汇，不是现有 ScreenSpec schema：当前 Adapter 原样传递 flat `tokens`，Validator 不验证 Token 角色，Materializer 的 `UiTokens`/`ParseToken` 只识别现有扁平字段和少量颜色名。把 `surface.*`、`type.*` 等直接写入生产 ScreenSpec 必须保持 `Deferred`，直到 Schema、Validator、Adapter 和 Materializer 同步实现。没有消费者时，不把 Web token 文件直接引入 Unity Runtime，也不把 Token 角色散落成 Scene YAML 魔法值。

## 视觉决策顺序

1. **Composition**：先确定 Safe Area、主区域、阅读顺序、profile 与遮挡预算。
2. **Hierarchy**：再确定标题/正文/主操作、重复组件语法和 sibling/layer 顺序。
3. **Geometry**：再确定 anchor、pivot、最小目标、列表节奏、文本宽度与裁剪。
4. **State parity**：补齐 selected、disabled、loading、empty、error、long-content；素材缺失另按 fallback/阻断证据处理，不能冒充当前未实现的 `missing-art` fixture state。
5. **Material**：最后应用颜色、字体、边框、圆角、阴影、透明度、图片裁切和 icon role。
6. **Micro detail**：只在结构稳定后处理光学居中、数值基线、1px 边缘和本地化文本。

顺序不可反转。若布局、阅读顺序或状态覆盖仍失败，禁止用 Glow、Blur、Shadow 或装饰边框提高“完成感”。

## 字体、密度与层级

- 最小字体角色为 `title`、`body`、`label`、`caption`、`numeric`；同一组件只消费已声明角色，不自行生成接近但不同的字号。
- `numeric` 需要检查数值宽度、对齐和最大位数；`body`/`caption` 需要最大行数、截断或滚动策略；本地化长文本必须是 fixture。
- 信息密度按任务选择 `focus`、`standard` 或 `compact` 语义档，而不是按设备整体缩放。`compact` 仍须满足最小交互目标和可读性。
- 视觉层级优先使用值对比、留白、字体角色与分组。每增加一层边框、阴影或发光，都必须说明它区分的所有权或状态。

## 状态与素材合同

- `selected`、`focused`、`hover`、`disabled` 是不同状态；键鼠/手柄焦点不得用 `selected` 冒充。
- Empty/Loading/Error 保留页面骨架，只替换负责的内容区，避免状态切换导致整屏跳动。
- 图片必须声明 aspect mode、focal alignment、窄屏保留区域和 fallback；占位图只能证明布局可渲染，不能冒充商业素材。
- 每个正式素材记录至少需要 `source`、`hash`、`provenance`、`license`、`fallback`；图标/图片还需 `atlasOwner`，字体还需字体资产与 Fallback 链。当前模板/Validator 未完整强制这些字段，不能把目标合同写成已实现事实。

## Acceptance And Failure

- 视觉 brief 必须声明 panel identity、profiles、安全区、Token 角色、composition、组件 variants、fixtures、baseline、迭代预算与停止条件。
- 结构检查先于像素比较；单张截图、非空 PNG 或较低 diff ratio 都不能单独证明通过。
- 缺少字体、图标、授权、Safe Area、长文本、Atlas Owner 或原因归属时返回 `Blocked`，不得静默换字体、换图或覆盖 baseline。
- 每次修正只改变一个可归因原因；来源或 contract hash 变化后旧 baseline/报告立即 stale。

## Evidence Boundary

本条目可证明当前视觉参考要求语义 Token、有限字体层级、状态矩阵、profile 证据和受限迭代，并可明确列出当前 schema 的缺口。它不能证明项目已有完整 Design System、任何样例颜色是品牌色、字体或素材已授权、Canvas 已正确合批、视觉已经通过、Unity/Player 已运行或发布就绪。没有对应回执时统一为 `runtime-not-run`。
