# ScreenSpec v3 游戏 UI 自动化装配

`KnowledgeId`: `es.project.ui-automation-authoring.v1`  
`Authority`: `Source + Skill contract + Unity evidence`  
`RouteKeys`: `ui-automation`, `screen-spec-v3`, `ui-prefab`, `ui-fixture-scene`, `ui-layout`, `responsive`, `visual-qa`, `asset-fallback`
`ContentHash`: `961b5be9ece1f541575199a017ef67fc08f04cc6da4704d5ef4ece37676e9577`
## Scope

本条目负责从视觉需求到 ScreenSpec v3 候选、组件注册、UI Materializer、UI Prefab/Fixture、响应式布局与视觉证据门禁。它不负责运行时 Window/Presenter、背包、战斗、经济、导航或输入领域逻辑；不负责非 UI Scene Builder、通用 Prefab Override 或场景备份；不负责发布验收结论。

相邻所有者：非 UI 场景构建由 `es.unity.editor.project-scene-builder-authority.v1` 持有；PlayMode、Profiler、Player 与发布证据由 `es.project.scene-release-evidence.v1` 持有。

## Trigger and routing

- 自然语言触发：根据截图制作游戏 UI、生成 ScreenSpec、装配 UI Prefab、UI Fixture Scene、响应式/安全区布局、视觉对比、素材降级。
- 精确路由：`ui-automation`、`screen-spec-v3`、`ui-prefab`、`ui-fixture-scene`、`ui-layout`、`responsive`、`visual-qa`、`asset-fallback`。
- 预期最小命中：本条目；组件注册细节可追加 ScreenSpec Component 条目，视觉回执可追加 Visual Evidence 条目，总量不得超过 3。
- 邻近误路由：只有 `fixture`、`layout` 或 `prefab` 而没有 UI/ScreenSpec/Canvas/视觉上下文时，不得命中本条目；回退到 Scene Builder 或 Prefab 事务条目。路由仍歧义时停止并请求目标资产类型。

## Authority layers

顶部 `Unity evidence` 只声明本条目可接受的证据类型，不表示当前任务已经产生 Unity、GPU、Prefab 或 Fixture 运行证据；没有绑定当前输入的回执时仍是 `runtime-not-run`。

| Layer | Authority | Boundary |
|---|---|---|
| Knowledge | routing, constraints, anti-patterns, source and stale policy | never stores drifting asset facts |
| Registry | component/template capability, layout recipe and fallback policy | never writes Unity assets or declares visual acceptance |
| ScreenSpec v3 | candidate semantic component tree, profiles, states and intent | candidate is not write authorization |
| Materializer | deterministic Prefab/Fixture serialization | never re-interprets design or owns business facts |
| Evidence gate | structure snapshots, GPU PNGs and fixture coverage | static checks cannot impersonate Unity runtime evidence |

## Capability owner matrix

| 能力层 | 当前静态消费者 | Canonical owner | 当前边界 |
|---|---|---|---|
| IntentSpec | Intent validator 与 Intent-to-ScreenSpec handoff | `es.project.game-ui-player-intent-spec.v1` | 只定义玩家目标、主动作和业务桥接，不生成视觉层级 |
| Screen family | player-intent registry、组件模板与派生决策 | `es.project.game-ui-screen-family-decisions.v1` | 屏幕族选择不证明项目已有对应业务系统 |
| Reference/design evidence | Visual Brief；Python/C# Adapter 无损保留设计证据、需求合同与状态合同 | `es.project.game-ui-reference-design-evidence.v1` | 零哈希/空路径 `complete` 无效；输入审查不替代输出视觉证据 |
| ScreenSpec/component tree | Registry、Validator、Python/C# Adapter | `es.editor.project-screen-spec-materializer.screen-spec-components.v1` | 当前 Validator 覆盖不是全字段/全树语义证明 |
| AssetManifest | Validator + 项目内 resolver 检查 source、槽引用、路径、GUID、哈希与 provenance；高保真焦点主体还必须由 `focalAssetPolicies` 显式绑定其 crop/focal-point/safe-crop 决策 | `es.project.game-ui-asset-manifest.v1` | resolver 身份回执与静态 crop 对齐不等于导入、商业授权、实际裁切或视觉验收；缺源时仍使用白图 placeholder |
| LayoutPlan | Canvas/RectTransform/LayoutGroup 与 Adapter 布局投影；严格 Validator 校验 edge/center/stretch 几何、profile 内 layer/siblingOrder、主动作色彩及 `safeArea: ignore` 的受限背景例外；每个状态强制 `preserveBounds`，不能产生第二套状态几何；Resolver 在布局组真实静态矩形上检查交互目标尺寸和组内最小间距 | `es.unity.ui-canvas-layout.v1`、`es.unity.ui-layout-clipping.v1` | 安全区、长内容、交互密度和多 profile 仍需当前 Unity/GPU 证据 |
| BehaviorSpec/focus/navigation | Validator 检查 intent、目标尺寸、双向状态绑定和每个目标的 `effects`；Python/C# Adapter 保留 interaction/stateVariants，Fixture 只在 `stateSemantics.affectedComponentIds` 内执行显式效果；Validator、Resolver 与 Materializer 都拒绝 state variant/effect 借 bounds、anchor、pivot、layout、size 或 safe area 改几何 | `es.project.game-ui-behavior-focus-navigation.v1` | 当前不证明 EventSystem、InputAction、焦点图、业务动作或真实状态 reducer 可用 |
| Fixture Driver | Materializer 可驱动固定视觉 state | `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1` | Fixture state 不拥有真实业务或输入状态 |
| Materializer | C# Adapter 与 `ESUIGameScreenMaterializer` | `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1` | 静态源码不证明 Prefab/Scene 保存、幂等或 rollback |
| Output visual evidence | editor/ui/scene 快照与 GPU PNG 合同 | `es.editor.project-screen-spec-materializer.visual-evidence.v1` | 文件/退出码不等于非空像素或视觉通过 |
| Text/localization resilience | `fixtureTextBindings`、TMP、long-content Fixture 与 profile 像素文本容量投影 | `es.project.game-ui-text-localization-resilience.v1` | 静态行数/截断/动作净空不证明 TMP 实际换行或 Localization/Accessibility 运行时闭环 |

路由只加载当前问题的 owner，通常不超过 3 条；本矩阵是分流表，不把所有 owner 递归并入每次任务。

## Decision rules

1. 校验 SourceRefs、ContentHash、requiredReads 和组件注册表哈希；漂移时标记 `stale`，回读权威来源并重新规划。
2. 将请求分类到已注册 screen family。信息或置信度不足时 `Blocked` 并请求缺失输入，不得静默选择 HUD、Collection 或 Menu。
3. 选择已注册 recipe、组件集合和布局策略，记录输入、优先级及冲突裁决。无注册能力时停止，不得用临时层级冒充正式组件。
4. 先生成并验证候选 ScreenSpec v3、AssetManifest、LayoutPlan、BehaviorSpec 及 profile/state 矩阵；候选不授予 Unity 写权限。
5. 当前用户明确要求写 Prefab/Scene 或运行 Materializer 时可在其范围实施；AI 自主运行时保持候选。只有选用受管通道才要求匹配 AICommand、AIBrain 计划与 TaskContract，通道不可用不得把用户请求降为 `Deferred`。
6. 只有结构快照与当前 GPU 截图同时覆盖所需 profile/state，capture 的像素完整性可复算且可绑定当前输入，才能声明受限的像素检查通过。每个 editor/UI 结构快照对必须具有同一非空 root、Canvas metadata、profile viewport 与唯一的 root-local path set；UI screen 尺寸、每个元素的 boolean active 及 editor `screenRect` 对 UI `screenX/Y/Width/Height` 必须逐项相等（0.01 像素容差），否则先以 `snapshot-*` 阻断，不能进入 PNG 检查。非 default 状态声明视觉变化时还必须相对 default 产生最小像素差，且至少 80% 的差异像素必须落在 default editor snapshot 中 `affectedComponentIds` 的唯一 profile-local `screenRect` 并集（优先 active；`visible: true` 可使用唯一 default-hidden 节点；含四像素描边/阴影容差）。每条 effects 还必须由 UI snapshot 的目标能力与实际 `active/interactable/alpha/RGBA/outline/wrap/text` 值逐字段证实；`graphicAlpha` 使用后代 Graphic 的共同 alpha 和每节点 trace，颜色/outline 只接受直接 Graphic，文本/换行只在共同值及每个后代 TMP_Text trace 都收敛时接受，且 trace 路径唯一并属于目标组件树。这些事实仍不能替代构图、品牌或商业视觉验收。

## Registry boundary

Component records must use stable IDs and declare input slots, state variants, minimum size,
supported profiles, resource dependencies and fallback. AssetManifest entries are per-screen
facts and must carry source, hash and provenance; registry fallback policy is not proof that an
asset exists. A new component or recipe requires a registry entry, validator coverage and a
materializer implementation in the same change.

## Verified facts

- 当前 Skill、governance、组件注册表和 Materializer contract 定义候选生成与执行边界；它们不证明 Unity 写入已经发生。
- 当前 Materializer 源码可证明已实现的静态入口与序列化逻辑；源码存在不证明生成的 Prefab/Fixture 有效。
- Authoring workflow 定义工作步骤；文档步骤不等于当前任务回执。
- 任何素材存在性、布局结果和视觉质量都必须由当前资产检查或运行证据证明，不能从 Registry fallback 推断。

## Common AI failure modes

| 错误行为 | 症状与根因 | 预防与替代动作 | 恢复和缺失证据 |
|---|---|---|---|
| 只有 UI 外观，没有正式资产 | 截图或层级草稿被当成 Prefab 完成 | 绑定正式路径、稳定身份、Owner 和 Materializer 回执 | 保留候选；补 Prefab/Fixture 生成证据 |
| 通用 Fixture/Layout 误命中 UI | 缺少领域上下文仍套用 ScreenSpec | 要求 UI、ScreenSpec、Canvas 或视觉语义同时成立 | 回退 Scene Builder/Prefab 事务路由 |
| fallback 被当成素材存在 | Registry 策略被误读为资产事实 | 检查 AssetManifest 来源、哈希和 provenance | 标记缺失素材；请求或生成受权资产 |
| `focal-cover` 只写进 JSON | 焦点、裁切安全区在 Unity 物化时丢失，或普通 `Image.preserveAspect` 只 contain 而不裁切 | `focalAssetPolicies` 与 AssetManifest 对齐后，必须声明正有限 `sourceAspectRatio` 与 `atlasRotationPolicy: disallow-rotation`；Resolver 以最终静态矩形预测 UV/安全区可行性，Materializer 将 focal point 和 `[left,bottom,right,top]` 传给 `ESUIFocalCropRawImage`、拒绝旋转的 Atlas Sprite，并以实际 Sprite UV 宽高比交叉校验 | 安全区不可能保留、Atlas 旋转或宽高比偏差超过 1% 时阻断；静态源码/快照字段不等于 GPU 视觉通过 |
| 忽略 profile/state | 单一截图掩盖安全区、分辨率或状态缺口 | 固定 profile/state 覆盖矩阵与最小尺寸检查 | 补缺失 Fixture 和截图，禁止部分通过 |
| 状态只换 ID、只改无关背景或未应用声明效果 | `selected`/`loading`/`error` 快照和 PNG 身份完整，却与 default 完全相同，目标组件不变而背景像素变化，或画面变化但 alpha/颜色/outline/文本等 effects 未真实落到目标 | 对有 `visualChanges` 或 visual effects 的 non-default state 计算同 profile default 差异并映射到声明区域；同时将每条 effect 与 UI snapshot capability/value 重放 | 零差异以 `state-pixel-undifferentiated` 阻断；无关区域以 `state-pixel-outside-affected-components` 阻断；缺目标能力或值不符以 `state-effect-evidence-*`/`state-effect-snapshot-mismatch` 阻断；修正 Spec、Fixture 或 Materializer 后重新采集 |
| 状态只写在组件变体里 | 非默认 `stateVariants` 未被 Fixture 的 `stateSemantics` 执行集接管，截图永远不会覆盖它 | 严格 Validator 双向校验；Generator 仅从 `affectedComponentIds` 派生非默认变体，`default` 是基线例外 | 移除孤立变体或把组件加入具体状态语义，重建 profile/state 证据矩阵 |
| 状态只有名称或自然语言 | 已声明 selected/loading/error，但没有确定会改到哪个组件、改什么属性 | 严格 Validator 要求每个执行目标都有白名单 `effects`；Materializer 只执行目标内效果且让显式效果覆盖兼容启发式 | 补齐每个目标的 `visible`、`interactable`、图形、文本、换行或 outline 变更，重新生成 Spec 与 Fixture 证据矩阵 |
| 状态借变体改变布局 | `preserveBounds` 已声明，但某个 stateVariant/effect 暗中改 bounds、anchor、pivot、layout、size 或 safe area，导致长文本盖住动作区 | Validator、独立 Resolver 与 Materializer 三处拒绝状态几何；状态只能在已解析矩形内改视觉、交互或文字换行 | 把几何改动移回基准 LayoutPlan；为 wide/narrow 分别求解后重跑状态矩阵 |
| fixtureData 未进入实际文本 | Fixture 有长文案，但依赖 key 名或节点名猜目标，Materializer 仍显示短文案 | 每个可见 fixture 字符串声明 `fixtureTextBindings(componentId, fixtureDataKey, overflowPolicy, maxLines, contentInsetsPx, reserveActionClearancePx)`；禁止绑定交互控件或与 `effects.text` 双写 | Validator 拒绝歧义绑定；Resolver 输出每 profile/state 的 `textFit`；Materializer 按绑定重放 |
| 把一行文本当两行可用 | 规格声称 wrap，却只有一行高度，导致覆盖血条或动作区 | 用 resolved pixel rect、字体 token、insets 和保守混合脚本字符宽度计算估计/可用行数；wrap 溢出阻断，ellipsis 显式记录截断 | 调整基准 LayoutPlan、缩短 Fixture、改为明确 ellipsis，或等待 scroll recipe；不得以状态几何绕过 |
| 重复生成破坏 Prefab | 未绑定稳定身份、Undo、Dirty、Save、Rollback | 写前检查幂等键、Owner 和事务边界 | 取消并回滚；重新读取正式资产 |
| 把静态检查写成视觉通过 | 未运行 Unity/GPU 捕获 | 同时要求结构快照与新鲜 GPU PNG | 标记 `runtime-not-run`，补当前运行证据 |

## Execution checklist

- 开始前：读取 AIWarnings Start、CurrentStatus、RuleIndex、requiredReads；校验 SourceRefs/ContentHash；确定 screen family、Owner、正式输出路径与上下文预算。
- 实施中：验证 ScreenSpec schema、稳定组件 ID、素材 provenance、profile/state、safe area、最小尺寸、状态变体与 `affectedComponentIds` 的双向绑定、每个目标的可执行 `effects`、fixture 文本绑定及其 profile 行数/动作净空，以及所有状态 `preserveBounds` 和零 state-local geometry；取消和幂等；任何 Unity 写入必须走受权入口。
- 高保真静态门：要求 `advancedComposition` 明确单一主操作、焦点主体或无焦点理由、关键对齐/净空、profile 语义等价、焦点资源的 crop/focal/safe-crop/source-aspect 策略和 post-layout 交互密度；Resolver 先基于最终矩形输出 `focalCropFeasibility` 并拒绝不可能的保护区，`focal-cover` 物化再用 `ESUIFocalCropRawImage` 依据实际 Sprite UV 校验源宽高比、按 focal point 选择 UV，可能时平移裁切窗口以包含安全区，并把实际 UV/安全区结果写回两类快照。子项属于 LayoutGroup 时不能把 authored bounds 当最终几何，必须回到 Resolver/Unity 证据。
- 完成后：核对正式 Prefab/Fixture 路径、结构快照、GPU PNG、输入哈希、失败项和恢复结果。
- 不可跳过：未覆盖的 profile/state、空白截图、无效 anchor、safe-area 溢出、必需素材缺失均阻止视觉通过。
- 禁止：反射私有字段、直接修改生成结果绕过 Materializer、把文件/按钮/测试源码存在写成执行成功。

## Evidence boundary

Static 可证明 schema、Registry、Skill/contract 和 Materializer 源码边界；不能证明 Unity 序列化、Prefab/Fixture 正式资产、响应式布局、GPU 渲染或视觉质量。没有当前运行回执时必须报告 `runtime-not-run`。

## Failure feedback contract

The v18/v19 lobby batches are a required negative case for this workflow. A rerun must not be
accepted merely because a Prefab path exists, the static validator passes, or PNG files are non-empty.
The authoring receipt must carry the prior evidence batch, one or more `UI-FB-*` rule IDs, the exact
changed ScreenSpec/registry/validator/materializer fields, expected visual effects and falsification
checks. Missing feedback metadata is `feedback-not-incorporated`; missing Unity/GPU evidence remains
`runtime-not-run` or `visualAcceptance: not-claimed`.

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`1be1c47841585b77c436c577b10c67c800c6d36c622acddefd1d21018445e169`)
- `.agents/skills/es-ui-prefab-authoring/governance.json` (`9aa4d91989aac6d900a214fa02485d2f8d07fc5669081836d0a4f5485f55b5ba`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`61ae4309d70ee9f9db5b4f237b9052160afa8bfc5752bcdd9227690737317a53`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_ui_snapshot_evidence.py` (`7a09dd4b1b6f14baf33b98b01176c936385b9e465a57fd96105ed27ea5af4714`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`ca8239c82a680a6112f7aa0e9ac2bd905b5e3f22fed98bfc2437ba2c8a93a311`)
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIFocalCropRawImage.cs` (`5653c9b8c5fe381a8f65412236adb3616e7460ece82c99efb76e47fbec91a4cc`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)

`EvidenceLevel`: `S2` (protocol, registry and source boundary; Unity evidence must be supplied by the current run).  
`StaleWhen`: ScreenSpec schema, component registry, Materializer, Prefab/Fixture output or visual evidence contract changes.
