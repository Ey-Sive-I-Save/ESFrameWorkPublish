# 游戏 UI BehaviorSpec、焦点与导航

`KnowledgeId`: `es.project.game-ui-behavior-focus-navigation.v1`  
`Authority`: `Current ScreenSpec/validator/adapter/materializer source + Unity/Input System official source snapshot`  
`RouteKeys`: `ui-automation`, `ui-behavior-spec`, `behavior-spec`, `ui-binding`, `ui-interaction-intent`, `ui-focus`, `ui-navigation`, `input-modality`, `input-system-ui`  
`HashSchema`: `v2`  
`ContentHash`: `32bdb38333a10cc198e7bd2c8fbbf634d8f14e250bd560f5f6e9946532636ce3`  
`SourceSetHash`: `32bdb38333a10cc198e7bd2c8fbbf634d8f14e250bd560f5f6e9946532636ce3`  
`EntryBodyHash`: `a3d653056413c01d612d7175ffd1848b3132ea3a512946124c4d1ea58e4291d4`  
`EvidenceLevel`: `S0`  
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目是 ScreenSpec `behaviors`、`bindings`、组件 `interaction.intent`、视觉状态转换、焦点图、
Selectable Navigation 与输入 modality 的 canonical owner。它只定义 UI 表现层意图和 Fixture
驱动合同，不实现库存、战斗、经济、Presenter、Runtime Window 或项目输入动作。

## Trigger and routing

- 自然语言触发：UI BehaviorSpec、交互 intent/binding、按钮动作、键鼠/手柄/触屏 modality、
  焦点顺序、Selectable Navigation、selected/disabled/pressed 状态转换、Input System UI。
- 精确路由：`ui-behavior-spec`、`behavior-spec`、`ui-binding`、`ui-interaction-intent`、
  `ui-focus`、`ui-navigation`、`input-modality`、`input-system-ui`。
- 误路由边界：真实 InputAction/设备绑定由 `es-input-action` 及运行时输入 owner 负责；
  UI 组件是否注册由 screen-spec-components owner 负责；视觉截图由 visual-evidence owner 负责。

## Canonical BehaviorSpec model

| 层 | 最小字段 | 验收条件 |
|---|---|---|
| Intent | stable intent id、source component id、semantic action、enabled condition | intent 必须来自已注册表面动作，不携带业务执行代码 |
| Binding | binding id、intent id、input modality、action/control identity、display hint owner | 只描述连接点；运行时 action 存在必须另有当前证据 |
| Transition | from/to state、trigger、visual effects、guard、cancel/recovery | Fixture 转换不得伪装业务状态机 |
| Focus node | stable component id、focusable/enabled/visible 条件、focus visual | 隐藏/disabled 节点不得留在可达焦点路径 |
| Navigation edge | direction/next/previous、profile/state/modality 条件、fallback | 每个 profile/state 检查无死端、环路陷阱和越界目标 |
| Evidence | spec hash、profile/state/modality、focus path、event/action receipt | 静态图不证明 EventSystem 或输入模块执行 |

## Decision rules

1. 组件 `interaction.intent`、根 `behaviors` 和 `bindings` 必须使用稳定 id 显式关联；字符串存在不等于消费者闭合。
2. 每个可交互组件必须定义 focusable 条件、视觉焦点状态和按 profile/state/modality 的导航策略；
   pointer-only 元素也要声明键盘/手柄是否不可用以及替代路径。
3. 自动 Navigation 只有在实际布局和状态矩阵验证后才能接受；商业关键流程优先显式稳定边。
4. selected、focused、pressed、disabled、loading 和 error 不得被压成同一布尔值或只用颜色表达。
5. Fixture Driver 只能驱动确定性视觉状态；真实动作派发、Presenter 和业务状态必须由对应 runtime owner 验证。
6. EventSystem、Input Module 或 InputAction 任何一层未绑定时保持 `runtime-not-run`，不能从包安装或 Button 存在推导可用。

## Verified facts

- ScreenSpec 模板含空 `behaviors` 与 `bindings` 数组，但 Python Validator 只检查 `behaviors` 是数组，
  未校验元素合同，也完全未校验 `bindings`。
- Validator 对注册为 interactive 的组件只要求非空 `interaction.intent` 与至少 44x44 target；不检查
  intent 是否注册、binding、焦点路径、modality 或状态转换。
- Python/C# Adapter 将交互语义压缩为 `interactable: bool`；C# 执行形不保留 intent、behaviors、bindings。
- Materializer 可创建 Button、focus-ring 视觉和 Fixture selected/disabled 状态，但当前 Fixture Scene 使用
  `StandaloneInputModule`；项目同时安装 Input System 1.11.2，静态源码不能证明二者运行时兼容或已切换。
- Unity Selectable Navigation 与 Input System UI 官方资料证明可用 API/包合同，不证明当前 Prefab 焦点图或输入链通过。

## Required reads

- 本条目、ScreenSpec 模板/Registry/Validator、两个 Adapter、Materializer/source、UI 工作流和官方来源锁。
- 接入真实输入时追加 `es-input-action`、当前 InputAction asset/service、RuntimeMode 与设备绑定事实。
- 声明交互可用时追加 EventSystem/Input Module、PlayMode/Player 输入回执和 profile/state/modality 矩阵。

## Common AI failure modes

| 错误行为 | 触发/症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 当前证据 | 缺失证据 | Source owner |
|---|---|---|---|---|---|---|---|---|
| 非空 intent 被当成可执行 | Validator 通过即写“按钮可用” | 字符串校验替代消费者 | 查 intent registry/binding/dispatcher | 保持接口候选 | 补 runtime 绑定后重跑输入矩阵 | Validator/Adapter 静态源码 | InputAction/dispatcher 回执 | 本条目 + 输入 owner |
| 语义压成 interactable | 物化后丢失动作和状态转换 | Adapter 字段损失 | 对照原 spec 与 normalized JSON | 阻断行为完成声明 | 修复投影并重新物化 | 两个 Adapter 源码 | 语义保持测试 | Adapter owner |
| 自动导航形成陷阱 | 手柄焦点无法离开区域 | 只按视觉邻近推导 | 枚举 profile/state/modality 焦点图 | 显式边或有界 fallback | 恢复到最后已验证图 | Selectable 官方来源锁 | 当前 Prefab 焦点遍历回执 | 本条目 + UI runtime owner |
| 颜色是唯一状态信号 | focused/disabled 只换色 | 混淆视觉 Token 与交互状态 | 核对形状、轮廓、文本/图标等冗余信号 | 增加 focus-ring/非颜色提示 | 废弃旧视觉基线并重采 | WCAG Use of Color 来源锁 | 当前 profile/state PNG | visual owner |
| Fixture 状态冒充业务 | selected 截图被当成装备成功 | Fixture Driver 无业务所有权 | 检查 Presenter/业务事件/存档证据 | 只声明视觉状态可驱动 | 撤回业务结论并运行领域测试 | Materializer contract | Runtime/PlayMode 业务回执 | Fixture + 业务域 owner |
| Input System 包存在即通过 | manifest 有包但 UI 不响应 | 未验证 EventSystem/module/actions | 核对当前 Scene 和 action asset | 保持 `runtime-not-run` | 配置后按设备重测 | manifest + 官方来源锁 | Editor/Player 输入证据 | 输入 owner |

## Execution checklist

- 开始前：列出 intents、bindings、组件、profile/state/modality 与业务 owner。
- 静态检查：验证所有引用闭合、状态转换可取消、disabled/hidden 节点不在焦点路径。
- Fixture 检查：逐 profile/state/modality 记录起始焦点、方向边、确认/取消和丢焦恢复。
- 完成声明：分别报告视觉状态、EventSystem、Input Module、Action、Presenter 和 Player 证据，不跨层压平。

## Evidence boundary and non-claims

Static 只能证明当前字段存在、Validator 覆盖不足、Adapter 语义丢失和 Materializer 可创建的组件形状。
没有运行 EventSystem、Standalone/Input System UI Input Module、InputAction、PlayMode、设备或 Player。

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json` (`4aba3b950fef2b9c45dc6b4ba6abc3b6a59517ddeb566ab86ede106d5facf38d`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`4d60216d8d3c870d243f01577074b7b16b5e2234cb8eff02f9f26231521def74`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`4688b2f94c887ffda48468492f39aad66a8a47cffb1a25f1ddd3e48e97e84158`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)

## StaleWhen

ScreenSpec interaction/behaviors/bindings schema、Registry、Validator、任一 Adapter、Materializer/Fixture
Driver、EventSystem/Input System 版本或配置、官方来源锁或任一 SourceRef 哈希变化。
