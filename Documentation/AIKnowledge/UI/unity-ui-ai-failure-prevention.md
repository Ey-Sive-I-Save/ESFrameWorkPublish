# Unity UI AI 防错适配层

`KnowledgeId`: `es.unity.ui-ai-failure-prevention.v1`  
`Authority`: `ES routing adapter over Unity UGUI/UITK canonical knowledge and project authoring contracts`  
`RouteKeys`: `ui-automation`, `ui-ai-failure-prevention`, `ui-system-selection`, `ui-layout`, `responsive`, `ui-clipping`, `ui-interaction`, `ui-rendering`, `ui-input`, `ui-toolkit`, `visual-evidence`, `evidence-boundary`  
`HashSchema`: `v2`  
`ContentHash`: `566b08cee999f290bd3a390fbb2f8080514de74660fdfbffa16323dda8d68675`
`SourceSetHash`: `566b08cee999f290bd3a390fbb2f8080514de74660fdfbffa16323dda8d68675`  
`EntryBodyHash`: `40cf2e709f8460c8a431b5ee5571faacca3565f79a391332ac1cad4346075ea9`  
`EvidenceLevel`: `S0`  
`StaleWhen`: Unity/UGUI/UI Toolkit/Input System 版本、ScreenSpec/Materializer/Validator 合同、任一 canonical UI 条目或 SourceRef 哈希变化。

## Scope and ownership

这是一个去重后的规范化适配层。它不复制 Canvas、自动布局、EventSystem 或 UI Toolkit
教程，也不拥有具体组件、素材、屏幕族或业务行为。它只把已有 canonical 条目转换为
AI 在生成或审查 UI 前必须执行的选择、拒绝和证据规则。

事实所有者保持不变：`es.unity.ui-canvas-layout.v1` 拥有 Canvas/RectTransform/
CanvasScaler，`es.unity.ui-layout-clipping.v1` 拥有自动布局/ScrollRect/裁剪，
`es.unity.ui-interaction-rendering.v1` 拥有 EventSystem/Raycaster/Selectable，
项目 UI 自动化条目拥有 ScreenSpec、Fixture、Materializer 和证据分层。本条目只负责
把这些 owner 在 AI 工作流中按风险顺序组合起来。

## Fast routing and stop rules

1. 先判断目标技术栈：`.prefab`、`Canvas`、`RectTransform`、`GraphicRaycaster` 命中 UGUI；
   `.uxml`、`.uss`、`UIDocument`、`PanelSettings` 命中 UI Toolkit；`OnGUI`、`EditorWindow`
   命中 IMGUI。混合信号时读取项目现有屏幕和包版本，不能凭截图猜系统。
2. UGUI 任务至少追加 Canvas/Layout；包含滚动或裁剪时追加 Layout/Clipping；包含点击、
   键鼠、手柄或触控时追加 Interaction/Rendering。一次通常只加载 1 至 3 个 owner 条目。
3. 没有明确 `Reference Resolution`、安全区、profile、state 或主动作时，输出
   `Blocked`/`needs-clarification`，不生成“看起来正确”的固定坐标方案。
4. 发现未注册组件、未绑定 AssetManifest、缺少 Fixture 或缺少当前 GPU/Unity 回执时，
   只保留候选或静态结论；不得宣称 Prefab、视觉或运行时通过。

## AI error firewall

### `UI-AI-001` wrong UI system

- `severity`: `identity/authority`
- `erroneousBehavior`: 把 UGUI Prefab 当 UI Toolkit，或把 UI Toolkit 的 USS/UXML 规则写进 UGUI。
- `triggerAndSymptom`: 输入同时出现 `Canvas` 与 `UIDocument`，生成物没有对应消费者，或运行时完全不可见。
- `rootCause`: 仅按“UI”关键词路由，没有按资产、组件和包版本判定系统。
- `preventionCheck`: 检查目标文件类型、场景组件、`Packages/manifest.json` 和现有屏幕 owner；冲突时拒绝自动选择。
- `correctAction`: 固定一个系统和对应 canonical 条目；确需混合时拆成两个明确 root，并记录桥接边界。
- `recoveryAction`: 删除错误候选输出，按当前系统重建 ScreenSpec/Prefab；保留原始输入哈希以便复盘。
- `evidencePresent`: 项目包配置、系统路由规则和现有 UI 条目。
- `evidenceMissing`: 当前目标在 Unity Editor 中的导入和运行结果。
- `sourceRefs`: 本条目 SourceRefs 中的 Canvas、Interaction、UI 自动化和官方来源锁。

### `UI-AI-002` canvas and scale omitted

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 只填绝对坐标和像素尺寸，省略 Canvas render mode、CanvasScaler、Reference Resolution、Match 或 safe area。
- `triggerAndSymptom`: 单一 Game 视图正常，换分辨率、方向或刘海设备后整体偏移、裁剪或遮挡。
- `rootCause`: 把参考图尺寸当成运行时布局合同。
- `preventionCheck`: ScreenSpec/LayoutPlan 必须声明 root Canvas、缩放策略、参考分辨率、方向和安全区输入。
- `correctAction`: 先定 anchor/pivot 和安全区容器，再投影布局；不支持的 profile 明确 `Deferred`。
- `recoveryAction`: 撤回单分辨率结论，补齐 profile Fixture 和当前分辨率证据后重新生成。
- `evidencePresent`: UGUI 包文档、项目 Canvas/Layout 条目和 Materializer 合同。
- `evidenceMissing`: 目标 Prefab 在所有声明 profile 下的 Unity/GPU 截图。
- `sourceRefs`: Canvas/Layout、官方来源锁和 UI 自动化条目。

### `UI-AI-003` anchor, pivot and sizeDelta conflated

- `severity`: `recoverable`
- `erroneousBehavior`: 用 `sizeDelta` 同时表达拉伸和固定尺寸，或把 pivot 当作锚点位置。
- `triggerAndSymptom`: 元素随父级变化跳动、边距反向、文本增长后溢出。
- `rootCause`: 未区分父级比例约束、自身旋转/缩放中心和 anchor 区间偏移。
- `preventionCheck`: 每个关键 RectTransform 写出 anchorMin/Max、pivot、边距/尺寸语义；同一轴只允许一个布局 owner。
- `correctAction`: 固定边缘用 anchor+pivot+offset，拉伸用 anchor 区间和受控偏移；让 LayoutGroup 或显式尺寸单独负责一轴。
- `recoveryAction`: 以最小 profile 重算 RectTransform，重新跑长文本和窄屏 Fixture。
- `evidencePresent`: RectTransform 合同和静态 ScreenSpec 校验器。
- `evidenceMissing`: 实际分辨率下的布局重建和像素证据。
- `sourceRefs`: Canvas/Layout 条目和 UI 自动化条目。

### `UI-AI-004` competing layout controllers

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 在同一轴同时使用 LayoutGroup、ContentSizeFitter、固定 sizeDelta 或互相驱动的 LayoutElement。
- `triggerAndSymptom`: 布局抖动、尺寸为零、重建顺序依赖或内容被压扁。
- `rootCause`: 父子控制器的输入/输出责任没有分层。
- `preventionCheck`: 静态检查每一轴的唯一尺寸 owner，并拒绝已知竞争组合；滚动 Content 额外检查可滚动轴尺寸来源。
- `correctAction`: 选择一种约束模型；需要包裹内容时将尺寸计算与排列拆到不同层级。
- `recoveryAction`: 禁用竞争组件，清理残留尺寸，再由单一 owner 重建并验证长内容。
- `evidencePresent`: UGUI Auto Layout 文档、布局裁剪条目和 Validator 规则。
- `evidenceMissing`: Unity LayoutRebuilder 实际重建、性能和极端内容运行结果。
- `sourceRefs`: Layout/Clipping 条目、UI 自动化条目和 Materializer contract。

### `UI-AI-005` scroll and clipping hierarchy wrong

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 把 Mask 当安全区或布局容器，或把 ScrollRect、Viewport、Content 层级/引用随意互换。
- `triggerAndSymptom`: 内容不可滚动、滚动条不跟随、边缘内容消失或点击区域与画面不一致。
- `rootCause`: 未区分滚动驱动、视口裁剪和安全区职责。
- `preventionCheck`: 检查 `ScrollRect -> Viewport -> Content` 引用、裁剪组件位置、滚动轴尺寸 owner 和安全区容器独立性。
- `correctAction`: 按职责建立三层层级；安全区放在 Canvas 下独立容器，不用 Mask 代替 safe area。
- `recoveryAction`: 重建引用和 Content 尺寸，先验证默认/空/长内容三态再恢复视觉结论。
- `evidencePresent`: ScrollRect、Mask、RectMask2D 和布局源码/文档。
- `evidenceMissing`: 当前 Unity 中拖拽滚动、裁剪边界和输入命中证据。
- `sourceRefs`: Layout/Clipping 条目、官方来源锁和 UI 自动化条目。

### `UI-AI-006` invisible or incorrectly ordered graphics

- `severity`: `recoverable`
- `erroneousBehavior`: 只创建 RectTransform，不验证 Image/Sprite/材质、Alpha、尺寸、激活状态和 sibling 顺序。
- `triggerAndSymptom`: 层级存在但截图空白、图标透明、遮罩盖住内容或点击落到错误层。
- `rootCause`: 把结构存在当成渲染存在，忽略 Graphic rebuild 和绘制顺序。
- `preventionCheck`: 对每个视觉槽检查非零尺寸、有效 Graphic/Sprite、颜色 Alpha、激活链、Canvas 层级与 fallback 标识。
- `correctAction`: 使用 AssetManifest 的稳定槽位和明确 fallback；需要独立排序时记录嵌套 Canvas 边界。
- `recoveryAction`: 先用可见 fallback 证明布局，再修复资源 provenance/import；空白 PNG 不能升级为通过。
- `evidencePresent`: Image/Sprite、Canvas 排序和 Materializer fallback 合同。
- `evidenceMissing`: 当前 GPU 非空像素、素材解析和正式资源授权证据。
- `sourceRefs`: 官方来源锁、Canvas/Layout 条目、UI 自动化条目和 Materializer contract。

### `UI-AI-007` interaction chain incomplete

- `severity`: `identity/authority`
- `erroneousBehavior`: 有 Button/Selectable 就声称可点击或可用手柄，忽略 EventSystem、Input Module、GraphicRaycaster、interactable 和导航图。
- `triggerAndSymptom`: 鼠标点击无响应、触控穿透、键盘焦点丢失、手柄无法回退。
- `rootCause`: 把组件声明投影成布尔 `interactable`，未验证完整输入链和 modality。
- `preventionCheck`: 检查 EventSystem、兼容 Input Module、Canvas Raycaster、Selectable 状态、focus/navigation 图和输入意图绑定。
- `correctAction`: 按 BehaviorSpec 补齐输入链；没有运行回执时只报告静态绑定，不报告运行时可用。
- `recoveryAction`: 重新建立最小 default/selected/disabled 导航路径，并在目标 modality 下补 Unity/PlayMode 证据。
- `evidencePresent`: Interaction 条目、Input System 官方来源锁和 BehaviorSpec 合同。
- `evidenceMissing`: 当前设备/输入方式的交互回执。
- `sourceRefs`: Interaction 条目、官方来源锁和 UI 自动化条目。

### `UI-AI-008` static evidence overclaimed

- `severity`: `identity/authority`
- `erroneousBehavior`: 因 YAML、Prefab 路径、Validator 或 PNG 文件存在就宣称视觉、布局或运行时通过。
- `triggerAndSymptom`: 证据没有绑定同一 ScreenSpec/profile/state，或 PNG 为空、全色、来自旧输入。
- `rootCause`: 混淆 Static、Runtime、Visual 三种证据等级和输入身份。
- `preventionCheck`: 每个结论绑定 spec/profile/state、结构快照、Unity/GPU 回执和来源哈希；缺失时强制 `runtime-not-run`。
- `correctAction`: 降级为静态候选/`Deferred`，列出未验证项，不由生成器自授予 Accepted。
- `recoveryAction`: 标记旧证据 stale，按当前输入重新物化并采集证据。
- `evidencePresent`: UI 自动化和 Materializer 的证据边界合同。
- `evidenceMissing`: 当前目标的 Unity 导入、布局、交互和视觉回执。
- `sourceRefs`: UI 自动化条目、Materializer contract 和官方来源锁。

### `UI-AI-009` reference import mistaken for an available generator

- `severity`: `identity/authority`
- `erroneousBehavior`: 看到 Figma/参考图就宣称 Unity 官方 Skill 会自动导入设计并生成 Prefab，或把截图直接当作布局事实。
- `triggerAndSymptom`: 没有 ScreenSpec、AssetManifest、LayoutPlan 和来源许可，输出却进入物化阶段；图像中的隐藏状态、字体和交互被静默猜测。
- `rootCause`: 把官方 UI 路由 Skill 的能力说明误读为设计导入服务或视觉验收证据。
- `preventionCheck`: 将参考图仅登记为有来源和哈希的输入证据；检查是否存在明确的视觉观察、推导、假设、ScreenSpec 和人工/运行复核。
- `correctAction`: 先生成候选语义结构和布局约束；缺少可验证尺寸、字体、资源或交互信息时保持 `Deferred`/`needs-clarification`。
- `recoveryAction`: 撤回自动导入/自动完成声明，补充参考区域、AssetManifest、profile/state 和对应 Unity 证据。
- `evidencePresent`: Unity 官方 UI Skill 明确没有客户端 Figma 自动导入；项目参考设计证据和 UI 自动化合同规定输入/输出边界。
- `evidenceMissing`: 当前参考图的授权、区域测量、字体/素材来源和 Unity 物化结果。
- `sourceRefs`: 官方 Agent Skills 来源快照、参考设计证据条目和 UI 自动化条目。

### `UI-AI-010` UI Toolkit guidance applied to an older uGUI project

- `severity`: `identity/authority`
- `erroneousBehavior`: 在 Unity 2022.3 uGUI 项目中生成 UXML/USS、要求 PanelSettings，或把 USS 的 flex/CSS 规则写进 Canvas Prefab。
- `triggerAndSymptom`: 生成文件没有运行时消费者，Canvas 层级缺失，或项目包/版本不支持所选 UI Toolkit API。
- `rootCause`: 只读取官方通用 `ui` 路由，没有校验 `ui-uitk` 的 Unity 6+ 版本边界和项目现有资产体系。
- `preventionCheck`: 在路由前检查 ProjectVersion、manifest、现有 `.uxml/.uss/UIDocument` 与 Canvas/Prefab；版本不匹配时强制 uGUI 或请求升级决策。
- `correctAction`: 当前项目默认使用 UGUI；只有目标版本和现有 UITK 消费者同时满足时才选择 UI Toolkit。
- `recoveryAction`: 停止并清理错误体系的候选文件，按项目既有系统重建；不要用不存在的 PanelSettings 或 USS 属性补救 uGUI。
- `evidencePresent`: 官方 Agent Skills 来源快照、项目 ProjectVersion/包锁和 UGUI canonical 条目。
- `evidenceMissing`: 当前目标在 Unity Editor 中的导入、PanelSettings/Canvas 绑定和运行结果。
- `sourceRefs`: 官方 Agent Skills 来源快照、Canvas/Layout 条目和 UI 自动化条目。

### `UI-AI-011` visual authoring silently expands into business logic

- `severity`: `identity/authority`
- `erroneousBehavior`: 用户只要求高保真 Prefab/场景内 UI，AI 却额外创建库存、商店、导航、输入或业务 Presenter 脚本，并把按钮“看起来可用”写成逻辑完成。
- `triggerAndSymptom`: 生成物出现未声明的 MonoBehaviour、事件回调、数据源或业务字段；Prefab 依赖隐藏脚本，后续无法区分 Fixture 与真实系统。
- `rootCause`: 把“working UI/proper button”误读为必须编写业务逻辑，忽略用户范围和 Materializer 的无业务所有权边界。
- `preventionCheck`: 生成前锁定输出类型（visual-only 或 functional），扫描候选文件和组件依赖；没有明确逻辑授权时禁止新增脚本、事件绑定和业务数据。
- `correctAction`: 只生成语义层级、视觉状态和确定性 Fixture；交互只保留 BehaviorSpec/intent 占位，不伪造业务回调。
- `recoveryAction`: 移除越界脚本和绑定，恢复纯视觉候选，重新计算 ScreenSpec/Prefab 证据；真实逻辑另走业务 Bridge 和独立合同。
- `evidencePresent`: 官方 UI Skill 的 scope discipline、项目 UI 自动化条目和 Materializer business-state boundary。
- `evidenceMissing`: 当前业务 Presenter、输入服务和 Runtime Bridge 的真实接入回执。
- `sourceRefs`: 官方 Agent Skills 来源快照、UI 自动化条目和 Materializer contract。

### `UI-AI-012` text component and font system crossed

- `severity`: `recoverable`
- `erroneousBehavior`: 在 uGUI 项目中生成 legacy `Text` 或把 TMP Font Asset 直接塞给 UI Toolkit；反过来也把 UI Toolkit TextCore 资源当成 `TextMeshProUGUI` 字体。
- `triggerAndSymptom`: 文本样式不一致、字形缺失、字体引用无法序列化，或生成物依赖未安装/不匹配的文字组件。
- `rootCause`: 只按“文字/字体”语义生成组件，没有根据 UI 系统和当前包版本选择文字消费者。
- `preventionCheck`: 先判定 UGUI/UITK；UGUI 检查 `com.unity.textmeshpro` 和具体 `TMP_FontAsset`，UITK 检查 TextCore/FontAsset 与 `PanelSettings`；禁止跨系统资源引用。
- `correctAction`: 当前项目 UGUI 默认使用 `TextMeshProUGUI`，并绑定字体、字形集、Fallback、来源和许可证；UI Toolkit 另走其专属 owner。
- `recoveryAction`: 替换错误组件/字体引用，重新运行 long-content、缺字和窄 profile Fixture；旧截图和结构证据标记 stale。
- `evidencePresent`: 官方 Agent Skills 的文字组件约束、项目 TMP 3.0.9 来源锁和文本韧性 canonical 条目。
- `evidenceMissing`: 当前目标字体资产导入、字形覆盖、许可证和 Unity/GPU 文本证据。
- `sourceRefs`: 官方 Agent Skills 来源快照、游戏 UI 设计来源锁和 `es.project.game-ui-text-localization-resilience.v1`。

### `UI-AI-013` UI Toolkit binding or UXML namespace copied across versions

- `severity`: `identity/authority`
- `erroneousBehavior`: 在不支持的 Unity 版本中生成 `PanelRenderer`/绑定 API，使用硬编码字符串绑定路径，或在 UXML 命名空间中写入程序集名。
- `triggerAndSymptom`: UXML 无法解析、绑定静默失效、重载后数据不再更新，或项目编译失败。
- `rootCause`: 把 UI Toolkit Skill 的示例代码当作跨版本通用 API，没有区分 UXML/USS 结构、数据源合同和 Unity 版本。
- `preventionCheck`: 只有命中 UI Toolkit 且版本满足时才读取 UITK binding/custom-element 资料；检查 `[CreateProperty]`、`nameof()`、datasource、binding mode、`partial` 和 namespace-only 约束。
- `correctAction`: 当前 2022.3 UGUI 任务不生成 UXML/USS/PanelRenderer；Unity 6+ UITK 任务将绑定和自定义元素作为独立候选能力，并记录版本。
- `recoveryAction`: 删除不兼容 API 或错误 namespace，回退到静态 UXML/USS 候选；重新导入并在 Unity Console/运行时验证绑定。
- `evidencePresent`: 官方 Agent Skills 的 UITK binding/custom-elements 来源快照和系统路由规则。
- `evidenceMissing`: 当前项目 UITK 包、UXML 导入、数据源、重载和运行时绑定回执。
- `sourceRefs`: 官方 Agent Skills 来源快照和 UI 自动化证据边界。

### `UI-AI-014` hierarchy rebuilt destructively during a local fix

- `severity`: `identity/authority`
- `erroneousBehavior`: 为修复一个尺寸、锚点或间距问题，AI 直接删除并重建整个 Canvas、Prefab
  或屏幕层级，丢失现有引用、稳定对象身份、Prefab override、Undo 历史或用户未提交的局部修改。
- `triggerAndSymptom`: 修复 diff 大于目标问题，原有绑定变成 Missing、对象 GUID/路径变化，或重载后
  已有业务/Fixture 引用指向新对象；即使截图相似，后续合并和回滚也不可预测。
- `rootCause`: 把“重新生成更快”当成布局修复策略，没有先读取现有层级和组件 owner，也没有把局部
  修改与全屏重建区分为不同授权和证据范围。
- `preventionCheck`: 生成前保存目标层级、组件引用和输入哈希；先定位最小受影响节点，比较预期 diff，
  拒绝无理由的 root 重建；只有明确的新屏幕/新 Prefab 请求才允许创建新 root。
- `correctAction`: 对现有 UI 做局部、可逆修改，保留稳定节点身份、Prefab 引用和用户改动；新屏幕
  使用新资产路径并记录与旧屏幕的隔离边界。
- `recoveryAction`: 停止当前候选，恢复到最近的可重读快照或 Undo 点，重新应用最小补丁；重新检查引用
  闭合和 ScreenSpec/Fixture 身份后，才可更新结构证据。不能用“截图看起来一样”掩盖身份丢失。
- `evidencePresent`: Unity 官方 UI Skill 的“先检查现有层级、做局部修改”约束，以及项目 Prefab/
  Materializer 的稳定身份和证据边界。
- `evidenceMissing`: 当前 Unity Editor 中的 Undo、Prefab override、序列化引用、合并结果和运行时
  视觉/交互回执。
- `sourceRefs`: 官方 Agent Skills 来源快照、UI 自动化条目和 Materializer contract。

### `UI-AI-015` full CSS or incomplete UI Toolkit runtime chain emitted

- `severity`: `identity/authority`
- `erroneousBehavior`: 把 USS 当成完整 CSS，生成未支持的 `gap`、`z-index`、`border` 简写、
  `pointer-events` 或 CSS gradient；或者生成 UXML/USS 却遗漏 USS 链接、顶层容器、UIDocument
  的 PanelSettings 或正确的运行时消费者。
- `triggerAndSymptom`: UI Toolkit 文件可以静态存在但无法解析、样式被静默忽略、运行时不渲染，或
  布局结果与 AI 依据浏览器 CSS 推断的结果不同。
- `rootCause`: 将网页 CSS 经验和官方 Skill 的示例当作跨版本、跨消费者的完整实现合同，忽略 USS
  是受限子集以及 UIDocument 的运行时装配链。
- `preventionCheck`: 只有在项目版本和 UI Toolkit 路由命中时才检查 UXML/USS；逐项核对受支持的
  USS 属性、USS 链接、单一顶层容器、UIDocument、PanelSettings 和现有运行时消费者；发现未支持
  属性或缺少链路时拒绝物化。
- `correctAction`: 将布局表达改写为当前版本支持的 USS/UXML 结构，补齐明确的 PanelSettings 和
  样式引用；当前 Unity 2022.3 UGUI 任务保持不生成 UITK 文件。
- `recoveryAction`: 移除未支持属性和孤立文件，回退到最后可验证的 UGUI 或静态 UITK 候选；在
  Unity Editor 重新导入并检查 Console/运行时后，才更新 UITK 结构证据。
- `evidencePresent`: 官方 Agent Skills 来源快照中的 USS 受限属性、UXML 链接、PanelSettings 和
  顶层容器约束，以及项目 UI 系统路由规则。
- `evidenceMissing`: 当前 Unity 版本的 UXML/USS 导入、PanelSettings 绑定、样式应用和 GPU/运行时
  渲染回执。
- `sourceRefs`: 官方 Agent Skills 来源快照、UI 自动化条目和 Materializer contract。

## Minimal preflight checklist

- 系统：确认 UGUI、UI Toolkit 或 IMGUI，且与项目版本/资产类型一致。
- 语义：确认 screen family、IntentSpec、primary action 和业务 owner；不从名词猜业务事实。
- 布局：声明 Canvas、CanvasScaler、Reference Resolution、profile、safe area、anchor/pivot 和每轴唯一 owner。
- 状态：至少覆盖 `default`、`selected`、`disabled`；异步/内容密集屏追加 `loading`、`empty`、`error`、`long-content`。
- 交互：声明 EventSystem、Input Module、Raycaster、焦点/导航图和 modality；静态存在不等于运行可用。
- 资源：每个槽位绑定 AssetManifest、哈希、provenance、fallback 和许可证边界。
- 证据：分别报告 Static、Runtime、Visual；没有当前 Unity/GPU 回执时写 `runtime-not-run`。

## Verified facts, assumptions and non-claims

### Verified facts

- 本条目引用的 canonical 条目和项目合同分别定义了 UGUI 布局、裁剪、交互、ScreenSpec、Materializer 和证据边界。
- 当前项目的 Knowledge 索引按 routeKeys 选择 1 至 3 个条目；本适配层不要求递归加载整个 UI 知识库。

### Assumptions

- 目标是当前项目 Unity 2022.3/UGUI 为主，若输入命中 UI Toolkit 或 IMGUI，必须切换对应 owner。
- 规则用于 AI 生成前置检查和审查，不替代 Unity Editor、PlayMode、GPU 或 Player 验证。

### Non-claims

- 不声明任何特定 Prefab、Fixture、业务系统、素材、字体、输入绑定、响应式布局、视觉质量、性能、Player、IL2CPP 或发布已通过。
- 官方网页/包能力和静态源码只能校准合同，不能证明当前目标运行时行为。

## SourceRefs

- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md` (`71a8fa144fd889fa5f1f0e7ffb729dcc357631d25787d23be1f97485cb714aaa`)
- `Documentation/AIKnowledge/UI/unity-ui-layout-clipping.md` (`826c1d2ce2456aa4c5fdf14331643690afa3bb05a115982b5b664b3fd8cc392d`)
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md` (`516fd7bfd63c2cab9d715438c36d40235b2ad7634ba29de56f15423a97881ce2`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `Documentation/AIKnowledge/UI/unity-official-agent-skills-source-snapshot.md` (`d6ea6f0e721138b3f7bf1c43bc376fcbd72141ae72a68d98872abfbe1f24f4ce`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`6785f682878cfaba2fb0f525e947eadace8cd8f31e5ba3cc0df62d3a4da5098d`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`4d60216d8d3c870d243f01577074b7b16b5e2234cb8eff02f9f26231521def74`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)

## Evidence boundary

本条目只提供静态路由、防错和证据裁决。当前未运行 Unity、Editor、PlayMode、GPU capture、
Profiler、Player、IL2CPP 或发布流程，统一状态为 `runtime-not-run`。
