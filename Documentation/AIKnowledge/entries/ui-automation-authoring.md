# ScreenSpec v3 游戏 UI 自动化装配

`KnowledgeId`: `es.project.ui-automation-authoring.v1`  
`Authority`: `Source + Skill contract + Unity evidence`  
`RouteKeys`: `ui-automation`, `screen-spec-v3`, `ui-prefab`, `ui-fixture-scene`, `ui-layout`, `responsive`, `visual-qa`, `asset-fallback`
`ContentHash`: `33a8494f7df449fe62f498842d8b547ca0be9de19c2b9700c920197bf88f058b`

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
| Reference/design evidence | Visual Brief；Python Adapter 有缺省对象，C# 路径不保留 | `es.project.game-ui-reference-design-evidence.v1` | 零哈希/空路径 `complete` 无效；输入审查不替代输出视觉证据 |
| ScreenSpec/component tree | Registry、Validator、Python/C# Adapter | `es.editor.project-screen-spec-materializer.screen-spec-components.v1` | 当前 Validator 覆盖不是全字段/全树语义证明 |
| AssetManifest | Validator 只检查 source 枚举和槽引用 | `es.project.game-ui-asset-manifest.v1` | 当前无正式 resolver；Materializer 使用白图 placeholder |
| LayoutPlan | Canvas/RectTransform/LayoutGroup 与 Adapter 布局投影 | `es.unity.ui-canvas-layout.v1`、`es.unity.ui-layout-clipping.v1` | 安全区、长内容和多 profile 仍需当前 Unity/GPU 证据 |
| BehaviorSpec/focus/navigation | Validator 只检查数组/intent/目标尺寸；Adapter 压成 bool | `es.project.game-ui-behavior-focus-navigation.v1` | 当前不证明 EventSystem、InputAction、焦点图或业务动作可用 |
| Fixture Driver | Materializer 可驱动固定视觉 state | `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1` | Fixture state 不拥有真实业务或输入状态 |
| Materializer | C# Adapter 与 `ESUIGameScreenMaterializer` | `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1` | 静态源码不证明 Prefab/Scene 保存、幂等或 rollback |
| Output visual evidence | editor/ui/scene 快照与 GPU PNG 合同 | `es.editor.project-screen-spec-materializer.visual-evidence.v1` | 文件/退出码不等于非空像素或视觉通过 |
| Text/localization resilience | TMP、long-content Fixture 与当前文本投影 | `es.project.game-ui-text-localization-resilience.v1` | 当前无 Localization/Accessibility 运行时闭环 |

路由只加载当前问题的 owner，通常不超过 3 条；本矩阵是分流表，不把所有 owner 递归并入每次任务。

## Decision rules

1. 校验 SourceRefs、ContentHash、requiredReads 和组件注册表哈希；漂移时标记 `stale`，回读权威来源并重新规划。
2. 将请求分类到已注册 screen family。信息或置信度不足时 `Blocked` 并请求缺失输入，不得静默选择 HUD、Collection 或 Menu。
3. 选择已注册 recipe、组件集合和布局策略，记录输入、优先级及冲突裁决。无注册能力时停止，不得用临时层级冒充正式组件。
4. 先生成并验证候选 ScreenSpec v3、AssetManifest、LayoutPlan、BehaviorSpec 及 profile/state 矩阵；候选不授予 Unity 写权限。
5. 当前用户明确要求写 Prefab/Scene 或运行 Materializer 时可在其范围实施；AI 自主运行时保持候选。只有选用受管通道才要求匹配 AICommand、AIBrain 计划与 TaskContract，通道不可用不得把用户请求降为 `Deferred`。
6. 只有结构快照与当前 GPU 截图同时覆盖所需 profile/state，且证据非空、可绑定当前输入，才能宣称视觉检查通过。

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
| 忽略 profile/state | 单一截图掩盖安全区、分辨率或状态缺口 | 固定 profile/state 覆盖矩阵与最小尺寸检查 | 补缺失 Fixture 和截图，禁止部分通过 |
| 重复生成破坏 Prefab | 未绑定稳定身份、Undo、Dirty、Save、Rollback | 写前检查幂等键、Owner 和事务边界 | 取消并回滚；重新读取正式资产 |
| 把静态检查写成视觉通过 | 未运行 Unity/GPU 捕获 | 同时要求结构快照与新鲜 GPU PNG | 标记 `runtime-not-run`，补当前运行证据 |

## Execution checklist

- 开始前：读取 AIWarnings Start、CurrentStatus、RuleIndex、requiredReads；校验 SourceRefs/ContentHash；确定 screen family、Owner、正式输出路径与上下文预算。
- 实施中：验证 ScreenSpec schema、稳定组件 ID、素材 provenance、profile/state、safe area、最小尺寸、取消和幂等；任何 Unity 写入必须走受权入口。
- 完成后：核对正式 Prefab/Fixture 路径、结构快照、GPU PNG、输入哈希、失败项和恢复结果。
- 不可跳过：未覆盖的 profile/state、空白截图、无效 anchor、safe-area 溢出、必需素材缺失均阻止视觉通过。
- 禁止：反射私有字段、直接修改生成结果绕过 Materializer、把文件/按钮/测试源码存在写成执行成功。

## Evidence boundary

Static 可证明 schema、Registry、Skill/contract 和 Materializer 源码边界；不能证明 Unity 序列化、Prefab/Fixture 正式资产、响应式布局、GPU 渲染或视觉质量。没有当前运行回执时必须报告 `runtime-not-run`。

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`662f898d15790781d808c4c4e14cd7c0a901a6b678e97d52c4f1ac0dc6fd24d3`)
- `.agents/skills/es-ui-prefab-authoring/governance.json` (`9aa4d91989aac6d900a214fa02485d2f8d07fc5669081836d0a4f5485f55b5ba`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)

`EvidenceLevel`: `S2` (protocol, registry and source boundary; Unity evidence must be supplied by the current run).  
`StaleWhen`: ScreenSpec schema, component registry, Materializer, Prefab/Fixture output or visual evidence contract changes.
