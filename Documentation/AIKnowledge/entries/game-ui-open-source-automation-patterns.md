# 开源游戏 UI 自动化方案的 ES 适配规则

`KnowledgeId`: `es.project.game-ui-open-source-automation-patterns.v1`  
`Authority`: `External open-source workflow calibration + current ES UI authoring contracts`  
`RouteKeys`: `ui-automation`, `open-source-ui`, `design-to-unity`, `intermediate-representation`, `source-map`, `readiness-report`, `visual-diff`, `conformance`, `ui-flow`, `known-loss`, `ui-mcp`  
`HashSchema`: `v2`  
`ContentHash`: `de8d60942db8904aa5a970720e73c13136604fc9109e85ef4bb99b7b8b95bfb0`  
`SourceSetHash`: `de8d60942db8904aa5a970720e73c13136604fc9109e85ef4bb99b7b8b95bfb0`  
`EntryBodyHash`: `3309e28abae39e1bc4223cdb34b60bfe9106429f59f22007592a88e462ff5577`  
`EvidenceLevel`: `S0`  
`StaleWhen`: 固定开源仓库 commit、raw 文件哈希、许可证、UI IR/flow 协议、当前 ES ScreenSpec/AssetManifest/BehaviorSpec/Materializer 合同或任一 SourceRef 哈希变化。

## Scope and authority

本条目只负责把三个公开开源方案中可验证的工作流模式转换为 ES 的架构防错规则：

- `Crackerrrrrr/design-to-unity`：设计输入先进入 Design Implementation Packet，再由 source map、
  readiness report、Prefab verifier 和 visual diff 接力。
- `ProdaZhang/figkit`：版本化像素 IR 与独立 flow 声明，多后端 capability/conformance 和 known-loss。
- `phucnguyen752/unity-ui-mcp`：截图 -> AI JSON -> Unity UGUI Prefab 的直接入口及其独立测量规则。

它不拥有 Unity Canvas、RectTransform、Prefab、AssetManifest、输入业务或视觉验收事实；这些仍由
现有 canonical Knowledge 和当前源码负责。第三方仓库的 README、静态转换结果和示例截图只作为外部
校准，不授予项目写入权限，也不证明当前 ES 能力。

## Routing and stop rules

1. 输入同时出现 Figma/Lanhu/PSD/截图、设计节点、UI 自动装配、source map、IR、visual diff、
   多后端或 MCP 时，先读本条目和来源快照，再按目标系统追加 1 至 3 个 ES owner。
2. 需要把参考设计物化为 Prefab 时，必须先有输入身份、ScreenSpec、AssetManifest、LayoutPlan、
   profile/state 和 Materializer 边界；缺任一项只能输出候选或 `Blocked`。
3. 需要交互或业务状态时，单独声明 BehaviorSpec/Fixture/Bridge；prototype link、按钮名称和
   截图不能授权库存、经济、导航或服务端数据。
4. 需要视觉比较时，baseline 必须绑定原始设计输入或明确同源快照；不得把另一后端的输出当真值，
   也不得用静态 YAML、Prefab 路径或单一截图替代 Unity/GPU 证据。
5. 任何后端未声明的字段、属性或效果都必须产生 warning/known-loss 或停止；禁止静默丢弃后继续
   写 `Accepted`。

## Core failure firewall

### `UI-OSS-001` direct screenshot-to-Prefab without a stable intermediate

- `severity`: `identity/authority`
- `erroneousBehavior`: AI 从单张截图直接写 Unity Prefab，只保留坐标和颜色，不保留输入哈希、稳定
  节点/组件 ID、资源清单、布局约束、字段损失或 source map。
- `triggerAndSymptom`: 第二次生成无法增量修复；无法知道某个对象来自哪一块设计、哪份素材或哪次
  输入，Prefab diff 变成整棵树重建，旧截图也无法判断是否对应当前输入。
- `rootCause`: 把“能生成一个画面”误认为“拥有可重放的 UI 装配管线”，跳过了设计输入归一化和
  语义/渲染分离。
- `preventionCheck`: 检查 source identity/hash、stable id、ScreenSpec、AssetManifest、LayoutPlan、
  captureKey 和 source-to-output mapping 是否闭合；没有中间合同时拒绝正式物化。
- `correctAction`: 先生成候选 ScreenSpec 和映射，再由 Materializer 创建或局部更新 Prefab；保存
  warnings、readiness 和输入 hash。
- `recoveryAction`: 将无来源 Prefab 降级为候选，恢复最近可重读输入，重新建立稳定 ID 和资源映射；
  不用截图相似度掩盖身份丢失。
- `evidencePresent`: 开源 packet/source-map 模式、项目 ScreenSpec/AssetManifest/Materializer 合同。
- `evidenceMissing`: 当前目标的 Unity Prefab 导入、稳定引用、增量重跑和 GPU 截图回执。
- `sourceRefs`: 开源来源快照、UI automation entry、AssetManifest entry 和 Materializer contract。

### `UI-OSS-002` prototype flow mistaken for business semantics

- `severity`: `identity/authority`
- `erroneousBehavior`: 从 Figma prototype link、按钮名称、列表外观或弹窗截图直接生成库存、经济、
  导航、服务端请求、Guard 或持久化逻辑。
- `triggerAndSymptom`: 生成的 UI 带有未声明的业务字段/Presenter/回调；Fixture 的静态列表被误报为
  真实数据，视觉请求越界为业务系统实现。
- `rootCause`: 没有区分设计层的可观察交互、引擎机械和应用领域语义。
- `preventionCheck`: 将 prototype/flow 只映射到 BehaviorSpec 的 intent、状态转换和机械候选；所有
  业务 action 必须有明确 Bridge owner、输入合同和真实数据来源。
- `correctAction`: 用确定性 Fixture 演示状态和输入意图；真实业务通过 ES Bridge 接入，未授权时保持
  `visual-only`/`runtime-not-run`。
- `recoveryAction`: 移除越界脚本、数据源和回调，保留视觉和 intent 占位；重新计算 spec/Prefab 证据。
- `evidencePresent`: FigKit 的 flow/app-hook 分层、项目 BehaviorSpec 和 UI-AI-011 规则。
- `evidenceMissing`: 当前 ES 业务 Bridge、真实输入、网络/数据源和 PlayMode 回执。
- `sourceRefs`: 开源来源快照、BehaviorSpec entry、UI automation entry 和 AI failure-prevention entry。

### `UI-OSS-003` renderer-to-renderer comparison promoted to visual truth

- `severity`: `identity/authority`
- `erroneousBehavior`: 用 HTML、另一个引擎或另一个生成版本的截图作为唯一 baseline，或在比较前
  静默缩放/翻转图像后宣称高保真通过。
- `triggerAndSymptom`: 两个后端一起错却 diff 很小；字体、方向、尺寸重采样和复杂素材的差异被
  抹平，报告没有记录 comparison identity、warning 或 human review。
- `rootCause`: 把后端一致性当成设计正确性，混淆几何/文字渲染噪声和实际设计偏差。
- `preventionCheck`: baseline 绑定原始设计 hash、目标 profile/state 和 capture identity；分别报告
  geometry、text、known-loss、resize/orientation warning，并保留 diff artifact。
- `correctAction`: 先对同源设计输入比较，再用跨后端 conformance 作为次级一致性检查；超阈值或有
  自动调整时进入 `review`，不自动 Accepted。
- `recoveryAction`: 标记旧 baseline stale，从同一输入重新捕获；检查字体、profile、方向和素材后
  再做单变量修复。
- `evidencePresent`: design-to-unity visual diff、FigKit 设计基线和 conformance 分层。
- `evidenceMissing`: 当前 Unity/GPU capture、人工视觉复核、目标设备 profile 和同源 baseline 回执。
- `sourceRefs`: 开源来源快照、参考设计 evidence entry、visual evidence owner 和 AI failure-prevention entry。

### `UI-OSS-004` unsupported fields or effects silently dropped

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 后端不支持的阴影、渐变、路径、裁剪、文字换行、USS 属性或未来 IR 字段被
  静默忽略，生成器仍输出“完整还原”。
- `triggerAndSymptom`: 结构存在但视觉缺层；不同后端结果分叉，报表没有 known-loss、映射文档或
  可定位的降级原因。
- `rootCause`: 没有版本化 schema、后端 capability matrix 和字段归属检查。
- `preventionCheck`: 每个字段声明 `render`/`approx`/`known-loss`/`structural`；未知字段只告警，
  不得悄悄丢弃；坏 IR/flow 必须有一致拒绝用例。
- `correctAction`: 实现支持、显式降级并留痕，或将任务保持 `Blocked`；同步 mapping、Fixture 和
  golden/结构证据。
- `recoveryAction`: 根据 known-loss 报告定位字段，重新生成受影响后端和截图；旧证据标记 stale。
- `evidencePresent`: FigKit additive IR、backend declarations、conformance 和 design-to-unity readiness warnings。
- `evidenceMissing`: 当前 ES Adapter/Materializer 每字段闭合、Unity 导入和像素/交互证据。
- `sourceRefs`: 开源来源快照、ScreenSpec validator/adapter、Materializer contract 和 UI automation entry。

### `UI-OSS-005` static importer output mistaken for Unity acceptance

- `severity`: `identity/authority`
- `erroneousBehavior`: 因 source map/YAML 计数正确、Prefab 文件保存成功、C# 编译或静态 validator
  通过，就声称 Unity 导入、布局、输入和视觉已通过。
- `triggerAndSymptom`: 缺少 Unity Editor reimport、Panel/Canvas 绑定、GPU 非空像素、EventSystem 或
  PlayMode 回执；静态产物与实际画面/输入链不一致。
- `rootCause`: 把静态转换闭包、编辑器导入、运行时行为和视觉验收压成一个布尔状态。
- `preventionCheck`: 将 packet/readiness/source-map、Unity import、structure snapshot、GPU capture、
  input/PlayMode 和发布证据分层；任一层缺失都保留 `runtime-not-run` 或 `Deferred`。
- `correctAction`: 先报告静态结果和下一步证据，再在目标 Unity 版本中导入、运行和截图；不由生成器
  自授予 `Accepted`。
- `recoveryAction`: 标记旧报告 stale，绑定当前 spec/source/profile/state 重新执行缺失层；保留失败
  和 known-loss 记录。
- `evidencePresent`: 两个开源方案均将 verifier/visual diff/Unity run 分开，项目 evidence boundary。
- `evidenceMissing`: 当前目标 Unity Editor、GPU、输入、Profiler、Player、IL2CPP 和发布回执。
- `sourceRefs`: 开源来源快照、visual evidence owner、Materializer contract 和 AI failure-prevention entry。

## ES mapping and execution checklist

| 开源模式 | ES canonical owner | 必须保留 |
|---|---|---|
| Design packet / UI IR | `ScreenSpec` + `LayoutPlan` | spec/version、输入 hash、稳定节点 ID、geometry/semantics 分层 |
| Asset manifest / source map | `AssetManifest` + Materializer | asset hash/provenance/license、源节点到 Prefab 对象的映射 |
| Flow / app hook | `BehaviorSpec` + Bridge | intent、状态、焦点/导航、业务 owner 和 Fixture 数据边界 |
| Readiness / known-loss | Validator + evidence contracts | missing/approx/drop、停止条件、旧证据 stale 条件 |
| Visual diff / conformance | Fixture Driver + Visual Evidence | 同源 baseline、profile/state/capture identity、结构与 GPU 分层 |

执行前检查：

- 固定设计输入来源、哈希、目标系统和 Unity 版本；不从截图猜隐藏业务事实。
- 先生成可审阅的 ScreenSpec/AssetManifest/LayoutPlan/BehaviorSpec，再允许 Materializer。
- 每个组件、资源、状态和字段都必须有 owner、fallback、loss 或 `Deferred` 结论。
- 同一 source/profile/state 生成结构快照和 GPU evidence；跨后端结果只能作辅助 conformance。
- 报告中分开 Static、Unity import、Runtime/Input、Visual 和 Release；未运行项统一写明。

## Verified facts, assumptions and non-claims

### Verified facts

- 三个固定 commit 的公开文件包含本条目引用的 packet/IR/flow、source map、readiness、visual diff、
  conformance 和直接 JSON 装配模式。
- 当前 ES 已有 ScreenSpec、AssetManifest、BehaviorSpec、Fixture、Materializer 和证据 owner；本条目
  只提供外部模式到这些 owner 的映射，不复制实现。

### Assumptions

- 目标仍是 Unity 2022.3/UGUI 为主；FigKit 的 UI Toolkit 后端只作为跨系统校准，不能改变当前默认系统。
- 开源仓库会继续变化；所有结论以快照 commit 和文件哈希为边界。

### Non-claims

- 不声明任何第三方仓库适合生产、没有安全/许可证问题、能生成商业级 UI 或能替代 Unity Editor。
- 不声明当前 ES 已实现第三方 packet、IR、source map、visual diff 或 conformance 的全部功能。
- 不声明任何 Prefab、Fixture、Unity import、PlayMode、GPU、Profiler、Player、IL2CPP 或发布通过。

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-open-source-automation-patterns.md`
- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/UI/unity-ui-ai-failure-prevention.md`
- `Documentation/AIKnowledge/entries/game-ui-reference-design-evidence.md`
- `Documentation/AIKnowledge/entries/game-ui-asset-manifest.md`
- `Documentation/AIKnowledge/entries/game-ui-behavior-focus-navigation.md`
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md`

## SourceRefs

- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md` (`062317be13f5e6385307dc31ff5d0f1830798ffdc7c092dab3e8ede46ebbbac5`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`6785f682878cfaba2fb0f525e947eadace8cd8f31e5ba3cc0df62d3a4da5098d`)
- `Documentation/AIKnowledge/UI/unity-ui-ai-failure-prevention.md` (`1f48ec5d7dc61214d6b1dd35bd90d0e656db0f543eb4e04d63d32d67e683ce81`)
- `Documentation/AIKnowledge/entries/game-ui-reference-design-evidence.md` (`30812a40e9cf0ca57e658c73c0c76a08f8eb33a5d19e5386008c973a3e263531`)
- `Documentation/AIKnowledge/entries/game-ui-asset-manifest.md` (`598789e8246d7074318d77ef11b191ba68599f89ccacbec63b4eea1fd26f3c7f`)
- `Documentation/AIKnowledge/entries/game-ui-behavior-focus-navigation.md` (`68a02db420f87d097bc632b5c2e3e479628eccc62f83c678741d51937f2682e6`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)

## Evidence boundary

本条目是静态外部架构校准和 AI 防错路由。当前未执行第三方安装、Unity Editor、Prefab/Scene 导入、
PlayMode、GPU capture、输入交互、Profiler、Player、IL2CPP 或发布验收，统一为 `runtime-not-run`。
