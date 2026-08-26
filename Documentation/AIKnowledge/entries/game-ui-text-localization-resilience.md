# 游戏 UI 文本、本地化与布局韧性

`KnowledgeId`: `es.project.game-ui-text-localization-resilience.v1`  
`Authority`: `Current ScreenSpec/adapter/materializer source + TMP/package facts + official source snapshot`  
`RouteKeys`: `ui-automation`, `ui-text-resilience`, `ui-localization`, `long-content`, `text-wrapping`, `bidi`, `rtl`, `glyph-coverage`, `font-fallback`, `line-breaking`  
`HashSchema`: `v2`  
`ContentHash`: `0632d7088c2e111810f6dcec9514fe2072a3e14d89c6bb2615f34cd284696c8d`
`SourceSetHash`: `0632d7088c2e111810f6dcec9514fe2072a3e14d89c6bb2615f34cd284696c8d`
`EntryBodyHash`: `1fdd595cb1c67811e67bba92053e9496f9eddda1772493e3895482632d02e861`
`EvidenceLevel`: `S0`  
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目是 UI 自动化中 locale 文本、最长内容、换行/截断、字形覆盖、TMP Fallback、Bidi/RTL、
行分断和窄 profile 韧性的 canonical owner。它不拥有翻译内容、运行时语言切换、读屏/TTS 或
业务文案；视觉字体角色仍由 `es.project.game-ui-visual-design-system.v1` 负责。

## Trigger and routing

- 自然语言触发：UI 本地化、长文本、换行/截断、TMP 字形缺失、Fallback Font、RTL/Bidi、
  阿拉伯文/希伯来文、CJK 行分断、动态字号、窄屏文字溢出。
- 精确路由：`ui-text-resilience`、`ui-localization`、`long-content`、`text-wrapping`、`bidi`、
  `rtl`、`glyph-coverage`、`font-fallback`、`line-breaking`。
- 误路由边界：只选视觉字体/字号 Token 时转 visual-design-system；只做一般响应式几何时转
  Canvas/Layout owner；真实 Localization 服务或辅助技术需要独立 runtime owner 与证据。

## Canonical text fixture model

| 维度 | 必需输入 | 最小检查 |
|---|---|---|
| Locale | stable locale id、language/script、direction、fallback locale | 不以英语长度代表所有语言 |
| Content | normal/empty/longest/expansion/punctuation/mixed-script/numeric samples | 文本必须来自确定性 fixture，不依赖在线翻译 |
| Font | TMP Font Asset id/hash、授权、glyph set、local/global fallback chain | 对每个 fixture 实际检查缺字，不从配置存在推导覆盖 |
| Layout | profile、safe area、bounds、wrap、overflow、max lines、min/max size | 检查裁剪、重叠、不可达内容与动作位移 |
| Direction | LTR/RTL/Bidi、数字/标点/占位符顺序、图标镜像策略 | UAX 规则是校准，不证明 TMP/布局已实现 |
| Evidence | spec/font/text hashes、locale/profile/state、snapshot/PNG/findings | 每个组合独立；缺一项不能整体通过 |

## Decision rules

1. 先列出目标 locale/script 与确定性文本 fixtures，再决定宽度、换行、截断或缩放；禁止只以英文调布局。
2. 每个可见 fixture 字符串显式声明 `fixtureTextBindings.componentId/fixtureDataKey`，再为关键标题、正文、按钮、计数器和动态值分别声明 overflow/maxLines、像素 insets、动作净空与不可截断语义；不得由键名或节点名猜目标。
3. 自动缩小字体必须有可读性下限和失败状态；超过预算时优先重排、滚动或请求产品裁决，不能无限缩小。
4. TMP Font Asset、字形集、Fallback 顺序、来源和许可证必须绑定当前 hash；Fallback 命中不是主字体覆盖。
5. RTL/Bidi 需要文本顺序、标点/数字/占位符、对齐、导航方向和方向性图标的组合 Fixture；
   不能只把父节点水平镜像。
6. Reflow/Text Spacing/UAX #9/#14 作为设计校准；Unity profile、TMP 版本和项目消费者必须用当前运行证据验证。
7. locale、字体、文本或布局合同变化会使相关截图与接受结论 stale。

## Verified facts

- ScreenSpec 模板的 states 未默认列出 `long-content`，但 Materializer contract/Fixture Driver 支持该状态；
  AI visual brief 要求 `long-content/localized` Fixture。
- Validator 现在校验 fixture 文本的目标、键、溢出策略、最大行数、像素 insets、动作净空、状态执行集和 effect 文本所有权；`scroll` 在尚无注册容器 recipe 时拒绝。
- Python/C# Adapter 保留完整 `stateSemantics`，Materializer 按其中的显式 binding 替换 TMP 文本，不再由节点名启发式追加长文。
- Resolver 为每个 profile/state binding 记录像素矩形、保守行数估计、可用行数、ellipsis 截断与动作净空；这是静态近似而不是 TMP/GPU 排版证明。
- 项目固定 TMP 3.0.9，包源码提供 fallbackFontAssetTable 能力；项目 manifest 未声明 Unity Localization
  或 Accessibility 包，因此不能声称运行时语言切换、读屏或辅助技术已实现。

## Required reads

- 本条目、ScreenSpec 模板、Visual Brief、Validator、两个 Adapter、Materializer、UI 工作流、
  `Packages/manifest.json` 和官方来源锁。
- 真实 locale/翻译接入时追加当前本地化数据、字体资产、许可证、格式化/复数规则与运行时消费者。
- 视觉接受时追加 Canvas/Layout、visual-evidence owner 和完整 locale/profile/state GPU 证据。

## Common AI failure modes

| 错误行为 | 触发/症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 当前证据 | 缺失证据 | Source owner |
|---|---|---|---|---|---|---|---|---|
| 英文通过即宣称本地化 | 德语/CJK/RTL 溢出 | 单一 fixture 代表所有 locale | 列出 locale/script/expansion 矩阵 | 逐组合验证 | 撤回整体通过，补缺失 fixture | Brief/工作流静态合同 | locale/profile/state PNG | 本条目 + 文案 owner |
| Fallback 配置即宣称无缺字 | 运行时出现方框或错误字体 | 未实际枚举字形 | 以当前文本集调用字形覆盖检查 | 报告主字体与 fallback 命中 | 修复 Font Asset 后重采 | TMP 包能力来源锁 | 当前 Font Asset/字形回执 | 字体资产 owner |
| 无限缩小字体 | 按钮文字勉强塞入但不可读 | 用字号掩盖布局预算不足 | 固定最小字号和重排阈值 | 重排/换行/滚动或 Blocked | 恢复 Token 后调整布局 | Visual Brief | 可读性/视觉证据 | visual + layout owner |
| 镜像父节点冒充 RTL | 数字、标点、图标和焦点方向错误 | 混淆几何镜像与 Bidi | 运行 mixed-script/数字/占位符 fixture | 分别处理文本、布局和方向性图标 | 回退 LTR 并标记未支持 | UAX #9 来源锁 | 当前 TMP/输入 RTL 回执 | 本条目 + runtime owner |
| Validator 字符串检查冒充文本韧性 | schema 通过但换行/裁剪失败 | 静态类型检查覆盖过窄 | 对照 wrap/overflow/font/locale 合同 | 保持 `Implemented-Unverified` | 补 Validator/Adapter/Fixture 后重跑 | Validator/Adapter 源码 | 端到端布局证据 | 本条目 + Adapter owner |
| fixture 字符串未绑定渲染目标 | FixtureData 有最长文案，画面仍使用短文案或 Materializer 以名字猜节点 | 数据与组件之间没有稳定所有权 | 每个可见 fixture 文本使用一个 `fixtureTextBindings`；禁止绑定 action/control 或和 `effects.text` 双写 | 用 binding 驱动 Materializer 和 Typography | 修正 binding 后重算 profile/state `textFit` | Validator/Resolver/Materializer 静态链路 | TMP/GPU 文字快照 | 本条目 + layout owner |
| wrap 宣称安全却没有高度预算 | 文本框一行高，长文本换行后压住血条/操作 | `maxLines` 与实际 profile rect 脱节 | 以 resolved pixel rect、字体 token、content insets 和 reserveActionClearancePx 计算行容量/净空 | wrap 超容量阻断；ellipsis 记录 `truncated` | 改基准 LayoutPlan、文案或溢出策略，不改状态几何 | `textFit` 静态回执 | TMP 实际行分断/GPU capture | 本条目 + layout owner |
| 文本规避了单个 action 却挤压整组操作 | LayoutGroup 重排后按钮尺寸或间距不足，文字仍侵入高密度交互区 | 只检查 authored child bounds 或单一净空 | 对关键操作组声明 `interactionDensity`，按最终静态矩形检查 targetSize 与最小组内间距 | 密度不足阻断 LayoutPlan，不用缩字掩盖 | 改组布局、profile 变体或文本策略，再重算全部 profile | `interactionDensity` 静态回执 | Unity LayoutGroup/TMP rebuild 与 GPU capture | layout owner |
| 规范阈值直接映射 Unity 数值 | CSS px/WCAG 被当 ScreenSpec 单位 | 缺少单位和缩放映射 | 记录 CanvasScaler/profile/像素换算 | 只作校准并实测 | 废弃错误阈值结论 | 官方来源锁 | 当前设备/Canvas 测量 | layout owner |

## Execution checklist

- 开始前：固定 locale/script/direction、文本 hash、字体资产/hash/license 与 profile/state。
- Fixture：至少 normal、empty、longest、扩张文本、混合脚本、数字/标点、缺字和 RTL/Bidi 负例。
- 检查：换行、截断、滚动、重叠、焦点/动作可达、字形/Fallback、方向性图标和布局重排。
- 报告：逐组合列 pass/fail/Blocked；未安装或未运行的 Localization/Accessibility 明确 non-claim。

## Evidence boundary and non-claims

Static 只能证明包版本、合同缺口、Adapter 字段损失和 Fixture 入口；没有安装/运行 Localization 或
Accessibility，没有导入字体资产、切换 locale、执行 Bidi/RTL、渲染文本或运行 PlayMode/Player。

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json` (`c90228507834858d10720385a17e03996aed2392b2cad35d63b3449d0c8c93bb`)
- `.agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md` (`744e99b7f133a90b8ee6ff11208717511f37a352a37c9f25d7ddb5c9fc220f6b`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`b191bf200879dab3a7edd0b173d1065d59d7c0e2fc0b5cd5160285219ae3d136`)
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_layout_plan.py` (`49956b5d72e4e5068743a6eb5b8c38567a5c492de18597033ed852a357d254de`)
- `.agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_typography.py` (`222f7171566f78cda15f90220455532c13410a240aa575962ad0d6471c7f91e9`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`28e29084d48d737a09eb281c2b26ee599d38c9e92a7d6ef081cbb59beea34668`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`dad8470537b6236ad3cda2d9e78ac862eeaf513e63f4b799c2cc79fb23ca4a07`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`2e82399e64ed5833891b1d4237791f4552306354c6a2acfd5e496e0d856207cd`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)

## StaleWhen

ScreenSpec/text schema、Visual Brief、Validator、任一 Adapter、Materializer/Fixture Driver、TMP/Localization/
Accessibility 包或字体资产、官方来源锁、locale/text fixture 或任一 SourceRef 哈希变化。
