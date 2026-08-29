# 游戏 UI 视觉设计与 Token 决策

`KnowledgeId`: `es.project.game-ui-visual-design-system.v1`
`Authority`: `Current project source + governed UI authoring contracts + derived visual-system decisions + official source snapshot`
`RouteKeys`: `ui-automation`, `visual-qa`, `ui-visual-design`, `visual-design`, `design-token`, `color-role`, `typography-role`, `spacing-token`, `visual-hierarchy`, `information-density`, `rarity-visual`, `ui-material`
`HashSchema`: `v2`
`ContentHash`: `08ad3c814b81543da06277ccbcbbd3553727e18e2dec4deb91e40fc207c8c2d1`
`SourceSetHash`: `08ad3c814b81543da06277ccbcbbd3553727e18e2dec4deb91e40fc207c8c2d1`
`EntryBodyHash`: `16ed9f38231281342d40c5ad357a06923c6b7ab4eca43bdfe629568151c58e78`
`EvidenceLevel`: `S0`
`StaleWhen`: ScreenSpec v3 Token schema、Validator、Adapter、Materializer、字体/素材治理、视觉证据合同、官方来源锁或任一 SourceRef 哈希变化。

## Scope and authority layers

本条目拥有 UI 视觉角色、Token 命名、层级、密度、状态一致性、字体/Fallback 约束与素材视觉
合同。Token 命名、字体角色和密度档是设计级派生决策，因此条目整体保持 `S0`；当前模板、
Validator、Adapter、Materializer 与包版本事实可按 SourceRefs 做静态复核。

当前 ScreenSpec v3 模板只提供 `surface`、`text`、`accent` 三个示例值。它们不是经过验收的
品牌 Token 系统。Adapter 原样传递 `tokens`；Validator 已检查基础语义角色、对比度、消费者、
状态 Token 绑定与间距尺度，但 Materializer 的 `UiTokens`/`ParseToken` 仍只识别现有扁平字段和
少量颜色名。因此这不是完整品牌 Design System。没有消费者时，不能把 DTCG Token 文件直接
引入 Unity Runtime。

本条目不拥有 Canvas/Prefab/Input/Resource 生命周期、品牌色、字体许可证、稀有度业务词表或
Runtime Window。WCAG 与 DTCG 官方资料只提供外部校准，不证明目标 Unity 画面已测量或通过。

## Trigger and routing

- 自然语言触发：UI 字体、UI 颜色/配色、视觉层级、信息密度、间距、Design Token、稀有度视觉、UI 材质、焦点样式。
- 精确路由：`ui-visual-design`、`visual-design`、`design-token`、`color-role`、`typography-role`、`spacing-token`、`visual-hierarchy`、`information-density`、`rarity-visual`、`ui-material`。
- 邻近路由：Canvas/Layout 机制使用 Unity UI canonical 条目；组件 schema 使用 ScreenSpec Components；视觉验收使用 Visual Evidence；总量保持 1 至 3 条。
- 只有 Shader、SpriteAtlas、字体导入或无 UI 上下文的配色问题时，不由本条目单独拥有实现事实。

## Token minimum contract

| 命名空间 | 负责内容 | 决策规则 |
|---|---|---|
| `surface.*` | 页面、面板、浮层、选中面 | 用层级和明度区分所有权，不为单屏偏移制造近重复色 |
| `text.*` | 主文、次文、弱化、反色、禁用 | 对比、字形覆盖、最大行数和 profile 必须共同验证 |
| `action.*` | 主操作、次操作、危险操作、焦点 | 同一屏只保留一个主要动作层级；颜色不是唯一状态信号 |
| `feedback.*` | 成功、警告、错误、信息、加载 | 同时保留文案、图标或结构信号，避免只靠红绿区分 |
| `type.*` | 标题、正文、标签、说明、数值 | 角色数量有限；实际 Font Asset、字重、Fallback 和许可证必须绑定来源 |
| `space.*` | 区域、组件与行内间距 | 使用离散阶梯，不以一次性 offset 修复父级布局错误 |
| `radius.*` / `border.*` / `elevation.*` | 轮廓、分组与层级 | 优先间距、对比和分组；阴影/辉光不能掩盖结构问题 |
| `icon.*` | 功能、状态、货币、输入提示 | 图标盒、光学中心、交互矩形、来源和 Atlas owner 分开声明 |
| `rarity.<project-tier>` | 稀有度视觉角色 | tier 名称和排序必须来自业务配置；AI 不假设 common/rare 等词表 |

Token 保存语义角色，具体值来自项目视觉资产或已批准规范。DTCG 2025.10 描述 `$value`、
`$type`、组继承和引用解析，但不定义 ES 的字段名，也不提供 Unity 消费者。把上述命名空间写入
生产 ScreenSpec 保持 `Deferred`，直到 Schema、Validator、Adapter、Materializer 与迁移测试
在同一变更中闭合。

## Visual decision order

1. `Composition`：Safe Area、主区域、阅读顺序、profile、CanvasScaler 输入与遮挡预算。
2. `Hierarchy`：标题/正文/主操作、重复组件语法、sibling/layer 顺序。
3. `Geometry`：anchor、pivot、目标尺寸、列表节奏、文本宽度与裁剪。
4. `State parity`：selected、focused、disabled、loading、empty、error、long-content 和素材缺失边界。
5. `Material`：颜色、字体、边框、圆角、阴影、透明度、图片裁切和 icon role。
6. `Micro detail`：结构稳定后再处理光学居中、数值基线、边缘和本地化文本。

顺序不能反转。布局、阅读顺序或状态覆盖失败时，禁止用 Glow、Blur、Shadow 或装饰边框制造
完成感。

## Typography, density and focus

- 最小字体角色为 `title`、`body`、`label`、`caption`、`numeric`；同一组件只消费已声明角色。
- `numeric` 检查最大位数、宽度和对齐；正文/说明检查最大行数、截断/滚动和本地化长文本 fixture。
- 每种正式字体记录 Font Asset、来源、许可证、目标字符集、Atlas population mode 和局部/全局 Fallback 链。
- TMP 3.0.9 包源码支持 fallback 表，但默认 `HasCharacter(..., searchFallbacks=false)` 不搜索 Fallback；包能力不证明项目配置完成。
- 信息密度使用 `focus`、`standard`、`compact` 语义档，不按设备整体缩放；`compact` 仍需满足目标尺寸与可读性。
- `selected`、`focused`、`hover`、`disabled` 是不同状态。焦点不能用 selected 冒充，颜色不能是唯一差异。
- WCAG 2.2 的 4.5:1/3:1 文本对比、焦点面积/3:1 对比和 24x24 CSS px 目标只用于设计校准；CSS px 不与 ScreenSpec 44x44 自动换算。

## Asset and state contract

- Empty/Loading/Error 保留页面骨架，只替换负责的内容区，避免状态切换导致整屏跳动。
- 图片声明 aspect mode、focal alignment、窄屏保留区域和 fallback；高保真焦点主体额外用 `focalAssetPolicies` 把组件槽位、AssetManifest crop/focal-point/安全裁切区、正有限 `sourceAspectRatio` 与 `atlasRotationPolicy: disallow-rotation` 绑定为可检查合同。Resolver 在最终静态矩形上预测 cover UV 并阻断无法保留安全区的 profile；Materializer 再将该策略传给 `ESUIFocalCropRawImage`，拒绝旋转 SpriteAtlas、以实际 Sprite UV 宽高比交叉校验并写入 source/applied UV 和 `safeCropSatisfied`。这只证明静态与源码链，不证明实际 SpriteAtlas/GPU 构图。占位图只证明布局输入存在。
- 正式素材至少记录 `source`、`hash`、`provenance`、`license`、`fallback`；图标/图片追加 `atlasOwner`，字体追加 Font Asset 与 Fallback 链。
- 当前 Validator 只在 `--require-advanced-composition` 的焦点主体路径强制 crop/focal-point/safe-crop 与 manifest 一致；它不证明 Sprite 导入、实际裁切、许可证或商业视觉，其他素材字段也仍需各自质量门。
- 每次视觉修正只改变一个可归因原因；contract/source/baseline hash 变化后旧报告立即 stale。

## Failure-surface matrix

### `UI-VD-001` unconsumed token schema

- `severity`: `identity/authority`
- `erroneousBehavior`: 将 `surface.*`、`type.*` 或完整 DTCG 文件写入生产 ScreenSpec，并宣称 Materializer 会正确消费。
- `triggerAndSymptom`: Validator 仍通过但字段被 Adapter/Materializer 忽略、退回默认色或产生未知字段失败。
- `rootCause`: 把设计词汇或交换格式误当成已实现 Runtime schema。
- `preventionCheck`: 逐字段跟踪 Schema -> Validator -> Adapter -> Materializer -> 测试消费者。
- `correctAction`: 当前只使用已支持的扁平 Token；新 schema 必须同步实现整条消费者链和迁移。
- `recoveryAction`: 移除未消费字段，恢复到最后可验证 spec；将扩展保持 `Deferred`。
- `evidencePresent`: Template、Validator、Adapter 和 Materializer 当前静态实现。
- `evidenceMissing`: 语义 Token schema、Unity 消费者、迁移与视觉回执。
- `sourceRefs`: Template、Validator、Adapter、Materializer、official source lock。

### `UI-VD-002` silent font fallback or missing glyphs

- `severity`: `identity/authority`
- `erroneousBehavior`: 未绑定 Font Asset/字形/Fallback/许可证就使用默认字体或静默替换字体。
- `triggerAndSymptom`: 本地化文本出现方框、字重变化、Atlas 重建或不同机器渲染不同。
- `rootCause`: 把 TMP 包支持 Fallback 等同于项目字体链已配置。
- `preventionCheck`: 固定 TMP 版本、Font Asset identity、字符集、Fallback 顺序、population mode、来源与许可证；目标文本运行字形覆盖检查。
- `correctAction`: 补齐正式字体资产和 Fallback 链；证据不全时 `Blocked`。
- `recoveryAction`: 撤回视觉通过，恢复已知字体或占位状态，并使旧 baseline stale。
- `evidencePresent`: manifest 固定 TMP 3.0.9；版本化包源码声明 fallback 搜索能力。
- `evidenceMissing`: 项目 Font Asset、目标字形覆盖、许可证和当前 Unity capture。
- `sourceRefs`: Packages manifest、official source lock、visual brief。

### `UI-VD-003` color-only state and unmeasured contrast

- `severity`: `recoverable`
- `erroneousBehavior`: 仅用颜色区分焦点、禁用、成功/错误，或按目测宣称对比度达标。
- `triggerAndSymptom`: 色觉差异、暗背景或 profile 变化后状态不可辨；没有可重放测量值。
- `rootCause`: 把 Token 名或示例 hex 当成可访问性证据。
- `preventionCheck`: 每个状态同时声明结构/图标/文案信号；按同一 profile/state 测量文本与焦点对比。
- `correctAction`: 增加非颜色信号并生成绑定输入的测量/截图证据。
- `recoveryAction`: 降级为设计候选，修正一个负责原因后重测。
- `evidencePresent`: WCAG 官方校准阈值和项目状态合同。
- `evidenceMissing`: 目标 Unity 像素、字体渲染、背景与测量工具回执。
- `sourceRefs`: official source lock、visual brief、workflow。

### `UI-VD-004` incomplete profile/state or target geometry

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 只检查默认宽屏，忽略窄屏、long-content、focused、disabled 或交互目标尺寸。
- `triggerAndSymptom`: 文本裁剪、操作遮挡、焦点环被裁剪、目标过小或状态切换跳动。
- `rootCause`: profile/state 没有成为视觉 evidence identity 的一部分。
- `preventionCheck`: 固定 profile/state/captureKey 矩阵；先做结构检查再做像素比较。
- `correctAction`: 补齐缺失输入和 fixture；未覆盖项不得部分通过。
- `recoveryAction`: 保留最近 passing baseline，撤销无归因批改并单项重做。
- `evidencePresent`: Template、Validator、Materializer 与 workflow 的静态矩阵合同。
- `evidenceMissing`: 目标全部 profile/state 的新鲜结构快照与 GPU PNG。
- `sourceRefs`: Template、Validator、Materializer、workflow、official source lock。

### `UI-VD-005` asset provenance or atlas owner missing

- `severity`: `identity/authority`
- `erroneousBehavior`: 将占位图、搜索图片或 AI 生成图当成已授权商业素材，或忽略 Atlas owner。
- `triggerAndSymptom`: 产物缺少 source/hash/license，资源无法复现，合批/替换责任不明。
- `rootCause`: fallback 策略被误读为素材事实。
- `preventionCheck`: 每个正式素材检查 source、hash、provenance、license、fallback 和适用 owner 字段。
- `correctAction`: 取得可追溯素材或保留明确 placeholder/Blocked 状态。
- `recoveryAction`: 移除无权威素材，恢复 fallback，并使引用它的 baseline/报告 stale。
- `evidencePresent`: 当前 visual brief、workflow 与 UI automation 素材边界。
- `evidenceMissing`: 目标素材来源、许可证、Atlas 配置和 Runtime 加载回执。
- `sourceRefs`: Visual brief、workflow、UI automation entry。

### `UI-VD-006` static or stale evidence promoted to acceptance

- `severity`: `identity/authority`
- `erroneousBehavior`: 以 schema 通过、PNG 非空、低 diff ratio 或旧 baseline 宣称视觉 Accepted。
- `triggerAndSymptom`: 报告未绑定当前 contract/spec/profile/state/source hash，或来源变化后仍复用旧结论。
- `rootCause`: 混淆静态闭包、视觉信号和 Runtime 事实，并忽略来源漂移。
- `preventionCheck`: 校验 SourceRefs、v2 哈希、唯一 Index binding、capture identity、baseline hash 与原因归属。
- `correctAction`: 只报告实际覆盖层；缺少 Unity/GPU 回执时统一 `runtime-not-run`。
- `recoveryAction`: 旧计划和旧 baseline 标 stale，从当前源和同一输入重新生成证据。
- `evidencePresent`: 当前源码、合同、来源锁和可重算哈希。
- `evidenceMissing`: Unity、视觉、PlayMode、Profiler、Player、IL2CPP 与发布回执。
- `sourceRefs`: 本条目全部 SourceRefs。

## Execution checklist

- 开始前：确定 panel identity、profiles、安全区、Token 角色、字体/素材来源、状态矩阵和停止条件。
- 设计时：按 Composition -> Hierarchy -> Geometry -> State -> Material -> Micro detail 顺序工作。
- 生产前：确认每个 Token 有当前消费者；字体/Fallback/素材 provenance/许可证缺失时停止。
- 验证时：结构检查先于像素；同一 profile/state 绑定 baseline、capture 和输入哈希。
- 完成后：复算 SourceRefs、SourceSetHash、EntryBodyHash 与唯一 Index binding；分层报告 non-claims。

## Failure feedback from lobby evidence

The v18/v19 lobby batches are negative evidence and must remain routed into future visual work.
Apply the reusable rules in `.agents/skills/es-ui-prefab-authoring/references/ui-failure-feedback-rules.md`:

- `UI-FB-001`: authored hero art must preserve focal-subject identity and orientation; token tint
  or stale generated art is a hard stop.
- `UI-FB-002`: a commercial screen needs one visible primary action and an intentional hierarchy;
  equal-emphasis component grids are not sufficient.
- `UI-FB-003`: wide and narrow layouts require separate reflow decisions and a complete state matrix.
- `UI-FB-004`: static completion and non-empty PNGs cannot become visual acceptance.
- `UI-FB-005`: every review finding must change a spec/registry/validator/materializer field and
  name the evidence that will falsify the fix.
- `UI-FB-010`: visual hierarchy requires profile-specific content counts and required-zone
  coverage, not merely a non-empty component tree.
- `UI-FB-011`: every declared color/state/spacing token needs a concrete consumer and an
  executable state effect before the design contract can pass.
- `UI-FB-012`: ranked visual bands must cover every required component and contain the primary
  action in the strongest declared action band.
- `UI-FB-013`: each profile needs an explicit focus order beginning at the primary action, while
  loading and disabled states must reject duplicate activation.
- `UI-FB-014`: each state has a profile-relative affected-component budget so a local state cannot
  silently recolor or hide most of the screen.
- `UI-FB-015`: key component `anchorMin`, `anchorMax` and `pivot` values must match the
  Materializer's final Unity RectTransform projection.

`advancedComposition` now provides a static decision contract for one primary action, focal or
intentional no-focal treatment, focal crop/focal-point/safe-crop linkage to AssetManifest, key
alignment/clearance, post-layout action-density constraints, responsive semantic equivalence,
ranked hierarchy coverage, focus order, state impact scope and key RectTransform anchors. Screen
bounds use top-left coordinates; final Unity anchors use bottom-left coordinates, while the
explicit authored Unity pivot is preserved. These contracts prevent decisions from being omitted;
they do not evaluate visual taste or replace Unity pixel evidence.

These rules are derived from project evidence, not proof that the current materializer satisfies
them. A future batch must report the changed rule ID and the artifact field changed because of it.

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-visual-design-system.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/Editor/project-screen-spec-materializer/screen-spec-component-registration.md`
- `Documentation/AIKnowledge/Editor/project-screen-spec-materializer/visual-evidence-boundary.md`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md`
- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json`
- `.agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md`
- `.agents/skills/es-ui-prefab-authoring/references/commercial-ui-patterns.md`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIFocalCropRawImage.cs`
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md`
- `Packages/manifest.json`

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json` (`c90228507834858d10720385a17e03996aed2392b2cad35d63b3449d0c8c93bb`)
- `.agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md` (`744e99b7f133a90b8ee6ff11208717511f37a352a37c9f25d7ddb5c9fc220f6b`)
- `.agents/skills/es-ui-prefab-authoring/references/commercial-ui-patterns.md` (`85579b0750d426fc9f615b60d488ff90626956a6f68784ff232175fba5e5c248`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`c29986030b842af905536e699778ad7a5a267e415f8842b74ae42a7c80ed4739`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`28e29084d48d737a09eb281c2b26ee599d38c9e92a7d6ef081cbb59beea34668`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`342d371c49d4cfc31cc85c5fda216a52560363b313842a637e6422364d9bd795`)
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIFocalCropRawImage.cs` (`5653c9b8c5fe381a8f65412236adb3616e7460ece82c99efb76e47fbec91a4cc`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`b3010e989e3460643e72f442f339e73e89586f4840ce16ab1baf07ccd3aa4423`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `.agents/skills/es-ui-prefab-authoring/references/ui-failure-feedback-rules.md` (`c694ed9fa2a38cd1d207068c254bf548010c48e5ee6bc6bf1bfb05ac63858875`)

## Evidence boundary

本条目可证明当前模板、Validator、Adapter、Materializer、视觉工作流与外部官方校准所表达的
静态边界，并明确现有 Token/字体/素材治理缺口。它不能证明项目已有完整 Design System，
不能把示例颜色升级为品牌色，不能证明字体或素材已授权、Canvas 正确合批、视觉已经通过，
也不能证明 Unity、Player 或发布就绪。没有对应回执时统一为 `runtime-not-run`。
