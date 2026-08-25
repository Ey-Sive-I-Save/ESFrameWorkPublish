# 确定性 Fixture、EditMode/PlayMode 与视觉 QA 证据

`KnowledgeId`: `es.engineering.fixture-visual-qa.v1`
`LifecycleStatus`: `Deprecated`
`DeprecatedOn`: `2026-08-23`
`DeprecationReason`: `项目所有者未确认该 Fixture 视觉域属于当前项目知识`
`ConsumptionPolicy`: `仅保留历史追溯；禁止作为现行 AIKnowledge 路由、RequiredRead、事实权威或实现指导`
`Authority`: `Source + AIWarnings + Unity package documentation + Skill contracts`
`RouteKeys`: `fixture`, `test-fixture`, `deterministic`, `editmode`, `playmode`, `screenshot`, `resolution`, `visual-qa`, `evidence`, `failure-recovery`
`ContentHash`: `94b48e38ea90951b4057a4fbae9900838460a3c5d5ea06b2e90e2ef617d624c6`

## 范围与索引状态

本条目整理 Unity `2022.3.45f1`、Unity Test Framework `1.1.33` 下的确定性测试
Fixture、EditMode/PlayMode 分层、分辨率截图、视觉 QA 和失败恢复证据。它只提供路由与
静态可追溯事实，不创建测试资产，也不证明 Unity、Test Runner、PlayMode、GPU 截图、
Profiler、Player 或发布已经运行。

本条目已标记弃用。当前 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md` 的投影退场曾被并发写入恢复，
因此“不可发现”尚未完成验收；后续 AI 即使被旧投影路由到此，也必须立即停止消费，不得引用或按原
RouteKeys 使用本条目。若任务确实涉及 Fixture 或视觉验收，必须从 AIWarnings Start 链、当前源码和
真实验证证据重新发现，并明确报告当前 AIKnowledge 覆盖缺口。

本条目不负责 Materializer 像素捕获实现、Scene Builder/Prefab override/备份规则、通用 Domain Reload
生命周期或发布验收阶梯。这些事实由下方 canonical 归属表指向的相邻条目拥有；本条目只决定何时转路由，
不复制其实现细节。

## AI 快速使用

### Fast Path

1. 先运行 `Test-ESKnowledgeEntry.ps1` 校验本条目的全部 SourceRef 和 `ContentHash`。通过后使用
   下表选择一条任务路由；失败则把条目标为 stale，回读漂移来源并停止沿用旧结论。
2. 默认只读本条目和命中行的最小来源，禁止为建立上下文加载全部 SourceRefs。
3. 在执行任何 Unity、资产或项目写入前，仍须读取当前 AIWarnings、确认当前用户目标/动作与工作树；只有受管通道再校验 AICommand/TaskContract
   和目标 Skill 门禁；Knowledge 不授予执行权限。
4. 输出结论时使用“证据结论”模板，不用“已完成”“可用”替代证据层级。

```powershell
& '.agents/skills/es-knowledge-creator/scripts/Test-ESKnowledgeEntry.ps1' `
  -ProjectRoot (Get-Location).Path `
  -EntryPath 'Documentation/AIKnowledge/Engineering/engineering-fixture-visual-qa/deterministic-fixture-visual-qa.md'
```

### 任务路由与最小读取集

| 任务意图 | 最小读取集 | 首选 Skill | 必须停止或降级的条件 |
|---|---|---|---|
| 设计 Fixture、场景或异常样本 | `es-test-fixture-authoring/SKILL.md`、`fixture-contract.md`；涉及测试场景时再读两条 SceneValidation AIWarnings | `es-test-fixture-authoring` | 无稳定 `fixtureId`、写入/清理边界或输入哈希 |
| 编写/判断 EditMode 测试 | `edit-mode-vs-play-mode-tests.md`、`workflow-create-test-assembly.md`；跨帧时再读 `reference-attribute-unitytest.md` | `es-test-fixture-authoring`、`es-unity-compile` | Test Assembly 模式不明确，或把 Editor 结果当运行时结果 |
| 编写/判断 PlayMode 测试 | `edit-mode-vs-play-mode-tests.md`、`reference-attribute-unitytest.md`、目标场景/运行时代码 | `es-unity-compile` | 没有当前 Test Runner 结果，或需要 Player 证据却只运行 Editor |
| 生成分辨率截图或视觉基线 | `es-ui-prefab-authoring/SKILL.md`、`game-ui-materializer-contract.md`、`ESUIGameScreenMaterializer.cs` | `es-ui-prefab-authoring` | 无 GPU、新鲜 PNG、实际尺寸或像素有效性结果 |
| 诊断截图失败或恢复中断 | `ESAITestObservation.cs`、`reference-setup-and-cleanup.md`、`extension-get-test-results.md` | `es-observability-evidence` | 清理残留、回执缺失、domain reload 后 callback 未重注册 |
| 给出发布或平台结论 | 本条目仅作导航；转读发布证据矩阵、目标 Player/平台回执 | `es-release-acceptance` | 只有源码、EditMode、PlayMode 或 Editor GPU 证据 |

表内文件名均指向下方 `SourceRefs` 中的精确项目路径。若任务同时命中多行，只合并这些行的最小读取集，
不要递归扩展到整个测试、UI 或发布目录。

### 自然语言路由探针

以下探针使用当前 `KnowledgeIndex.yaml` 做静态 routeKey 交集回放。`实际命中` 只列应进入上下文的前
1～3 个条目；`过宽` 表示原始候选超过 5 个，需要按“回退”列收口。它证明静态可发现性，不证明
`planTask` Runtime 行为。

为压缩表格，实际结果使用以下精确 KnowledgeId 简写：`F`=`es.engineering.fixture-visual-qa.v1`，
`V`=`es.editor.project-screen-spec-materializer.visual-evidence.v1`，`U`=`es.project.ui-automation-authoring.v1`，
`B`=`es.unity.editor.project-scene-builder-authority.v1`，`L`=`es.unity.lifecycle-domain-reload.v1`，
`C`=`es.unity.compile-player-il2cpp-evidence.v1`，`PF`=`es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1`，
`EA`=`es.project.editor-asset-authoring.v1`，`FR`=`es.function-area.release.v1`，
`SR`=`es.project.scene-release-evidence.v1`，`AB`=`es.aibrain.orchestration.v1`，
`RP`=`es.project.resource-pipeline-runtime.v1`，`RL`=`es.project.resource-runtime-lease-boundaries.v1`。

| 真实用户问题 | 预期 routeKeys | 预期 1～3 个条目 | 实际命中 | 零命中 | 过宽/误命中 | requiredReads/无关上下文 | 回退 |
|---|---|---|---|---|---|---|---|
| 给测试场景做可重复 Fixture，失败后可恢复 | `fixture, deterministic, failure-recovery` | 本条目、Scene Builder 权威 | `F, V, U, B`（4） | 否 | 否 | 本条目足够起步；只有写场景才补 Builder | 先锁定 `deterministic + failure-recovery`，再按是否写场景选择 Builder |
| 纯序列化校验应写 EditMode 还是 PlayMode | `editmode, playmode` | 本条目 | `F`（1） | 否 | 否 | 足够；无无关条目 | 无 |
| 协程、GameObject 生命周期和场景交互跑哪层 | `playmode` | 本条目 | `F`（1） | 否 | 否 | 足够；实现时再读目标运行代码 | 无 |
| 横竖屏各状态如何定义截图矩阵 | `screenshot, resolution` | 本条目 | `F`（1） | 否 | 否 | 足够；无无关条目 | 无 |
| PNG 存在但可能透明或单色，如何判视觉 QA | `visual-qa, visual-evidence` | 本条目、Materializer 视觉证据 | `V, F, U`（3） | 否 | 否 | 需补视觉证据条目；UI authoring 默认不读 | 需要像素结论时以 Materializer 视觉证据为 canonical |
| Domain Reload 后 Test Runner callback 丢失如何恢复 | `domain-reload, failure-recovery` | 本条目、Unity 生命周期 | `F, C, L`（3） | 否 | 否 | 两个目标条目足够；无编译问题时不读编译条目 | 先用本条目恢复测试回执；通用静态状态转 Unity 生命周期 |
| 重复生成 Fixture 会不会污染正式 Prefab/Scene | `fixture, prefab` | 本条目、Scene Builder 权威、Prefab 事务 | Top5=`U, PF, V, F, EA`（共 10） | 否 | 是，`prefab` 过宽 | 原始候选过量；只读三个预期条目 | 增加 `test-fixture + deterministic`，禁止只用 `prefab` |
| 取消截图后临时纹理和隐藏 UI 如何清理 | `screenshot, failure-recovery` | 本条目 | `F`（1） | 否 | 否 | 足够；无无关条目 | 无 |
| EditMode 通过能否说明 Player 和发布可用 | `editmode, player, release, evidence` | 本条目、编译/Player 证据、场景发布证据 | Top5=`F, FR, SR, C, AB`（共 13） | 否 | 是，`evidence/release` 过宽 | 原始候选过量；按目标平台最多补两个条目 | 先按证据层级选本条目，再只补一个目标平台条目 |
| Scene Builder 生成后如何做视觉证据和发布分层 | `scene-builder, visual-evidence, release` | Scene Builder 权威、Materializer 视觉证据、场景发布证据 | `V, FR, RP, RL, SR, B`（6） | 否 | 是，`RP/RL` 误入 | 三个预期条目足够；排除 Resource Pipeline | 锁定 `scene-builder + visual-evidence`，最后按目标平台补发布条目 |

单 routeKey 检查中，`fixture`、`test-fixture`、`deterministic`、`editmode`、`playmode`、`screenshot`、
`resolution`、`visual-qa`、`evidence` 均可由 AIBrain 入口发现；`failure-recovery` 当前只登记在索引，
未由 `AIBRAIN_ENTRY.md` 暴露。修复共享入口不属于本条目的写入范围；在共享路由修复前，所有恢复任务
必须同时提供 `fixture` 或 `screenshot`，零命中时回到 AIWarnings Start 链并报告覆盖缺口。

### 证据结论模板

```text
Verdict: StaticOnly | EditModeEvidence | PlayModeEvidence | GPUVisualEvidence | PlayerEvidence | Blocked
Scope: <fixtureId / assembly / profile-state-resolution / platform>
Inputs: <source/spec/fixture hashes>
EvidenceRefs: <XML / log / PNG / snapshots / receipt>
Missing: <尚未运行或不支持的更高证据>
示例字段：版本、来源、程序集、矩阵或环境变化时应使条目 stale。
```

只有 `EvidenceRefs` 中存在本次运行的原始产物时，才能选择高于 `StaticOnly` 的结论。任何缺少必需证据的
路径都返回 `Blocked` 或保持较低层级，不做模糊升级。

这些 `Verdict` 是本条目的任务内证据标签，不替代正式 `EvidenceLevel`（S0-S6）、模块成熟度或
`DeliveryVerdict`。`PlayerEvidence` 只表示存在指定 Player/平台原始证据，不自动表示 Accepted、Released
或发布通过。

### 执行决策门禁

| 条件 | AI 动作 |
|---|---|
| SourceRef、`ContentHash`、索引绑定均有效，且任务只要求静态设计/判断 | 继续；只读取命中行的最小来源 |
| 需要选择 EditMode/PlayMode，但目标生命周期或 Test Assembly 未确定 | 读取目标 asmdef、测试代码和运行对象；仍不明确则 `Blocked` |
| 需要创建/修改 Fixture、Scene、Prefab、PNG 或运行测试 | 当前用户明确目标/动作授权直接实施，并声明写入范围、清理和恢复边界；本条目本身不授权 AI 自行执行，受管通道才要求 AICommand/TaskContract |
| SourceRef 缺失、哈希漂移、版本/程序集/矩阵变化 | 立即标记 `stale`，回读权威来源并重新规划；禁止沿用旧截图或旧回执 |
| 只有文件存在、源码路径、按钮、测试定义或进程退出码 | 保持 `StaticOnly` 或 `Blocked`，不得升级为 Runtime 结论 |
| 请求 Player、Profiler、IL2CPP 或发布结论 | 转 `es-release-acceptance` 及目标平台证据条目；没有原始回执则停止 |

### Canonical 归属与去重

| 事实域 | canonicalEntry | duplicateEntries/相邻重叠 | 保留内容 | 删除或压缩内容 | 交叉引用方式 | 不可合并理由 |
|---|---|---|---|---|---|---|
| Fixture 身份、测试模式选择、截图矩阵、失败恢复 | 本条目 | Materializer 视觉证据、Scene Builder 权威、UI authoring | 可执行决策、最小证据和恢复动作 | 相邻条目不再复制本域测试分层 | 其他条目只链接适用条件 | 同属一次测试任务的跨层决策 |
| Materializer 捕获、像素有效性与视觉证据边界 | `Editor/project-screen-spec-materializer/visual-evidence-boundary.md` | 本条目、`entries/ui-automation-authoring.md` | 本条目仅保留“何时需要 GPU/像素证据” | 不复制 CaptureFixture 和像素门禁实现 | 需要像素结论时转 canonical | 实现所有者和证据对象不同 |
| Scene Builder、Prefab override 与项目内备份 | `Editor/project-scene-builder-authority/scene-builder-prefab-fixture-backup-authority.md` | 本条目、`entries/scene-release-evidence.md` | Builder 是布局权威及禁止污染正式 Prefab | 不复制 override 审计、保存和备份步骤 | 涉及生成、覆盖、保存、备份时转 canonical | 正式资产事务不归测试分层所有 |
| 场景 Guide 与发布证据阶梯 | `entries/scene-release-evidence.md` | 本条目、Scene Builder 权威 | 人工观察不得冒充断言及发布转路由 | 不复制 Guide 配置和发布阶梯 | 涉及 Guide/发布层级时转 canonical | 场景验收和发布拥有独立证据生命周期 |
| 通用 Domain Reload 静态状态 | `Unity/unity-lifecycle-domain-reload/unity-lifecycle-domain-reload.md` | 本条目、编译/Player 证据条目 | Test Runner callback 恢复 | 不复制通用静态状态和 Editor 生命周期 | 非测试专属状态转 canonical | 通用 Unity 生命周期不应复制到 Fixture 域 |

## 权威事实与派生规则

以下“当前版本、包合同、源码路径和现行 AIWarnings”是 SourceRefs 可复验事实；稳定用例身份、最小证据
和恢复步骤是从这些事实及 Skill 合同派生的执行规则。假设与非宣称单列在文末，不能混入事实结论。

### Fixture 身份与隔离

- Fixture manifest 的最小身份包含 `fixtureId`、目标程序集、平台、来源、写入范围、清理动作、
  Fixture 哈希、证据入口与失效条件。相同输入的生成与清理必须幂等，或明确拒绝重复执行。
- Fixture 是测试输入，不是产品内容。正式 Prefab、Scene、ScriptableObject 和发布资产不是默认
  写入面；测试对象必须有数量上限、所有者、超时、清理与回滚路径。
- ES 测试场景以官方 Builder 为布局权威，生成的 `.unity` 是可重建输出。不能手工修补生成场景
  来替代 Builder，也不能把测试导视或断言写进正式角色、载具、相机或技能 Prefab。
- 场景导视优先复用单个 `ESSceneValidationGuide`。人工观察项必须标记为
  `ManualObservation`，不能伪装成自动断言。

### EditMode 与 PlayMode

- Unity Test Framework 要求 EditMode 与 PlayMode 测试位于不同 Test Assembly。EditMode
  Assembly 只面向 `Editor`，可以引用 `UnityEditor.TestRunner`；PlayMode Assembly 不应依赖
  Editor-only 程序集。
- 普通同步合同优先使用 NUnit `[Test]`。只有需要跨帧、等待 Unity yield instruction、域重载
  或进入 PlayMode 时才使用 `[UnityTest]`。
- `[UnityTest]` 在 EditMode 中由 `EditorApplication.update` 推进，在 PlayMode 中作为协程推进。
  因此“同一测试方法可运行”不代表两种模式具有相同生命周期或相同证据含义。
- EditMode 适合纯数据、Parser、路径、哈希、序列化合同和 Editor 工具边界；PlayMode 才能观察
  GameObject 生命周期、帧末渲染、协程、物理周期、输入与场景交互。Player/目标平台仍是更高一层
  的独立证据。

### 分辨率截图矩阵

- 截图用例的稳定身份应至少由 `fixtureId + profileId + stateId + resolution + captureMode`
  组成。每个产物同时记录 Unity 版本、测试程序集、输入哈希、实际宽高、PNG 字节数、PNG SHA-256、
  帧号/时间、图形后端、日志与测试结果入口。
- UGUI 的 `CanvasScaler`、reference resolution、screen match mode、anchor 和 pivot 决定不同宽高比
  下的布局语义；仅缩放截图文件不能替代在目标分辨率重新布局和重新渲染。
- Materializer 自己的 profile/state、具体分辨率和 snapshot 输出由其视觉证据 canonical 条目拥有。
  本条目只要求截图矩阵从当前任务合同读取，不能把相邻实现中的示例尺寸升级为全局默认值。
- 当前 `ESAITestObservation` 在帧末调用 `ScreenCapture.CaptureScreenshotAsTexture`，把宽、高、
  字节数、SHA-256、帧号和 UTC ticks 写入截图 DTO；`superSize` 被限制在 `1..4`。这证明当前源码
  存在捕获与元数据路径，不证明任一当前运行已经成功产出截图。

### 视觉 QA 分层

| 层级 | 最小证据 | 可以支持的结论 | 不能替代 |
|---|---|---|---|
| 静态合同 | Spec/manifest/Builder/SourceRef 哈希 | 输入、身份、路径和预期矩阵确定 | Unity 导入、像素正确 |
| EditMode | Test Runner XML、断言、日志、结构 snapshot | Parser、布局合同、路径和确定性生成 | 帧末渲染、交互 |
| PlayMode | 场景、帧序列、运行态 snapshot、测试结果 | 生命周期、状态切换、输入或帧末行为 | GPU 像素质量、Player |
| GPU 视觉 | 每个 profile/state 的新鲜 PNG、实际尺寸、像素统计 | 本次渲染非空且可人工/自动比较 | 交互可用、性能、发布 |
| Player/平台 | 目标构建、设备截图、结果与环境回执 | 指定平台上的行为与画面 | 其他平台、发布全量验收 |

有效 PNG 不能只检查“文件存在”或“字节数大于零”。至少要验证：解码成功、声明尺寸等于实际尺寸、
像素不是全透明、像素 extrema 不是单一 clear color，并对预期可见区域执行结构边界或基线差异检查。
具体捕获实现、像素门禁和 Materializer 返回值语义由其视觉证据 canonical 条目拥有。本条目只保留
决策约束：没有独立像素验证器或当前运行回执时，不能仅凭 Materializer 返回成功宣称视觉 QA 通过。

## 确定性用例集

每个 Fixture 至少覆盖以下五类，用固定 ID 和固定输入哈希保存结果：

1. `positive`：合法 fixture、完整 profile/state 矩阵、结构和截图产物齐全。
2. `invalid-input`：非法尺寸、重复 ID、缺失程序集/资源或越界路径被确定性拒绝。
3. `denied-expansion`：写入正式资产、共享索引、发布目录或授权外路径被拒绝。
4. `repeat-idempotency`：相同输入重复生成得到相同语义 snapshot/manifest；时间戳类字段从比较中显式排除。
5. `interruption-recovery`：在生成、测试、截图或清理中断后，能够识别未完成状态、保留最后已知良好基线、
   清理临时对象并从相同输入哈希重跑。

视觉矩阵另加 `default`、`selected`、`disabled`、`empty`、`loading`、`error`、`long-content`
等实际声明状态；未声明状态不得由测试驱动静默发明。分辨率至少覆盖任务声明的横屏和竖屏 profile，
但具体尺寸必须来自当前测试合同，而不是复制本条目的示例值。

## 失败恢复与证据完整性

- Prebuild setup 与 post-build cleanup 有确定执行顺序；同一 setup/cleanup 被多个测试引用时只执行一次。
  Editor 内 cleanup 在相关测试结束后运行，standalone 测试在运行后立即清理。清理失败必须保留残留路径
  并阻断交付，不能把“测试断言通过”当成环境已恢复。
- Test Runner callback 不会跨 domain reload 自动保留，恢复后必须重新注册。运行无法启动时，应通过
  `IErrorCallbacks.OnError`、日志和结果回执区分“没有运行”与“运行后失败”。
- 命令行测试必须记录 `-testPlatform`、过滤器/程序集、`-testResults` XML 路径和日志。Unity 文档明确
  说明各被测组件没有统一退出码语义，因此诊断要读取 XML、错误消息与堆栈，不能只看进程退出码。
- `ESAITestObservation` 对并发截图返回 `RuntimeBusy` 或“等待当前帧”；纹理为空时记录错误；
  `finally` 恢复被临时隐藏的 Dashboard 并把 capture 标记完成。这是局部恢复机制。它没有替代
  文件落盘、PNG 解码、像素有效性和测试回执检查。
- 恢复重跑必须绑定原 `fixtureId`、输入哈希和目标矩阵。来源、Unity/包版本或目标测试程序集变化后，
  旧结果立即 stale，不能把旧截图复制成新鲜证据。

### 生命周期适用性与转路由

| 场景 | 本域动作 | 不适用或转路由条件 |
|---|---|---|
| 取消 | 停止排队新用例，标记未完成矩阵，清理临时对象并保留最后已知良好证据 | 进程/Worker 取消协议转 Automation TaskContract；不得只删输出文件 |
| 重复执行 | 比较 `fixtureId + inputHash + matrix`；相同输入必须幂等或确定性拒绝 | 输入、版本或程序集变化视为新运行，旧证据 stale |
| Domain Reload | 重新注册 Test Runner callback，核对运行 ID 和结果入口后再恢复 | 通用静态状态、EditorWindow 所有权转 Unity 生命周期条目 |
| Prefab/Scene 保存 | 默认不适用；Fixture 不拥有正式资产保存权 | 必须保存时转 Scene Builder/Prefab 事务条目，并重新取得写入、Undo/Dirty/Save/Rollback 合同 |
| ScriptableObject/AssetDatabase 写入 | 默认不适用；测试输出进入隔离目录 | 正式资产或内容注册转 Editor 资产事务条目；禁止以测试便利扩大写入面 |
| Player/Profiler/IL2CPP/发布 | 本域只提供缺失证据列表 | 转发布验收；无目标平台原始证据时保持 `runtime-not-run` 或 `Blocked` |

## AI 高频失败预防矩阵

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 仍缺证据 |
|---|---|---|---|---|---|---|
| 把文件存在或字节非零当作截图有效 | 透明图、单色 clear color、尺寸不符仍显示通过 | 用路径证据替代像素证据 | 解码、实际尺寸、alpha/extrema、预期区域或基线差异 | 取得每个矩阵单元的新鲜 PNG 和像素统计 | 作废该单元，保留日志并从原输入重拍 | GPU、图形后端和像素验证回执 |
| 用 EditMode 通过证明 PlayMode/Player | 生命周期、协程或输入在真实运行时失败 | 混淆测试模式和证据层级 | 核对被测对象是否跨帧、依赖场景/物理/输入及目标平台 | 按对象选择 PlayMode；平台结论另跑 Player | 降级 Verdict，列出未跑层级并补验 | 当前 Test Runner XML、Player/设备回执 |
| Fixture 写入正式 Prefab/Scene 或手工修补生成场景 | 重跑产生 override、污染产品资产、Builder 再生成后改动丢失 | 未区分测试输入与作者权威 | 检查写入根、Builder、Prefab 基线、清理和回滚范围 | 使用隔离 Fixture；场景布局只改 Builder 权威 | 停止写入、记录 diff，按正式事务回滚并重建 | Prefab override 审计、重建和重载证据 |
| Domain Reload 后继续等待旧 callback | 任务持续等待、无 XML、误报超时 | callback 不跨 reload 保留 | reload 后核对注册、runId、结果路径和 `IErrorCallbacks` | 重新注册并区分未启动与运行失败 | 关闭旧等待，保留日志，以同一输入重新发起 | reload 前后回执与 Test Runner 结果 |
| 相同任务重复执行但不校验身份 | 重复对象、覆盖基线、证据互相串台 | 缺少 `fixtureId + inputHash + matrix` 幂等键 | 执行前比较稳定身份、输出清单和完成标记 | 返回既有等价结果或确定性拒绝重复 | 隔离冲突产物，恢复最后已知良好基线后重跑 | 两次 manifest/snapshot 语义比较 |
| 取消或中断时只删除部分输出 | 隐藏 UI 未恢复、纹理泄漏、残留锁导致 `RuntimeBusy` | 没有 finally/取消阶段和恢复回执 | 核对当前阶段、临时对象、可见性、锁和已完成矩阵 | 先停止新工作，再按逆序清理并记录未完成项 | 恢复 UI/锁，保留残留路径，使用原输入恢复 | 取消回执、资源释放和后续可重入证据 |
| 用源码、按钮或退出码宣称运行/发布成功 | 没有 XML/PNG/设备结果却给出 Accepted | 把静态存在性当执行证据 | 按 Verdict 模板核对原始 EvidenceRefs 和平台范围 | 保持 `StaticOnly`/`Blocked`，只声明已证明层级 | 撤回越级结论，补跑对应验证并重新出具回执 | Unity、PlayMode、GPU、Profiler、Player/发布证据 |

## 执行检查表

### 开始前

- 按 `AIBRAIN_ENTRY.md -> KnowledgeIndex.yaml -> 本条目 requiredReads` 完成最小发现链；不得递归读取全部 Knowledge。
- 运行 Knowledge Entry 验证；任一 SourceRef、`ContentHash` 或索引绑定失败即标记 stale 并停止。
- 固定 `fixtureId`、输入哈希、Test Assembly、EditMode/PlayMode、profile/state/resolution 矩阵、写入范围、
  取消、清理、回滚和证据路径。

### 执行中

- 每个生成、测试和截图动作绑定稳定身份与当前运行 ID；重复调用执行幂等检查。
- Fixture 只写授权的隔离范围；正式 Prefab、Scene、ScriptableObject 和发布目录默认禁止。
- 保留未启动、运行失败、取消、清理失败和成功的不同状态，不用单一退出码覆盖原始 XML、日志或回执。

### 完成后

- 核对 manifest/snapshot、Test Runner XML、日志以及矩阵完整性；PNG 还要检查解码、实际尺寸和像素有效性。
- 确认临时对象、纹理、隐藏 UI、锁和回调已恢复；存在残留时结论为 `Blocked`。
- 重新运行 Knowledge、SourceRef/ContentHash、严格 UTF-8、routeKey 和 scoped diff 检查。

### 禁止事项

- 禁止把 Knowledge 摘要、旧快照、文件存在、按钮存在、源码存在或测试定义当作当前运行证据。
- 禁止手工修补 Builder 生成场景、污染正式 Prefab、扩大写入范围或用无关 AICommand 替代缺失权限。
- 禁止用 EditMode/PlayMode/Editor GPU 证据替代 Player、Profiler、IL2CPP 或发布证据。

## 证据边界

- **Static 可证明**：当前 SourceRef 字节、版本/包合同、测试模式规则、稳定身份字段、预期矩阵、路由和
  恢复约束闭合；静态验证通过只支持 `StaticOnly`。
- **Runtime 才能证明**：Unity 导入与 Domain Reload、EditMode/PlayMode 实际执行、帧末渲染、PNG 像素、
  交互、Profiler、Player、IL2CPP 和发布行为。每一层必须绑定本次运行的原始 EvidenceRefs。
- **本条目当前状态**：静态 Knowledge 合同可验证；Unity、Test Runner、GPU、Profiler、Player、IL2CPP
  与发布均未运行，保持 `runtime-not-run`。该状态不是静态失败，但禁止任何 Runtime/发布通过声明。

## 假设与非宣称

- 假设目标 UI 使用 UGUI；UI Toolkit、HDR、多显示器、XR、动态分辨率和平台安全区需要单独路由。
- 本条目没有运行 Unity、EditMode、PlayMode、GPU 截图、Profiler、Player 或 IL2CPP，证据状态为
  `runtime-not-run`。
- 没有创建 Fixture、Scene、Prefab、PNG、Test Runner XML 或运行回执，也没有验证当前 Unity 是否正在
  PlayMode。
- SourceRef 哈希只证明读取时的字节内容；任一来源漂移后，本条目必须重新计算 `ContentHash` 并复核结论。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Library/PackageCache/com.unity.test-framework@1.1.33/Documentation~/edit-mode-vs-play-mode-tests.md` (`47bd2ec911ce78f60cb50397701f5585e57bfa71eea8e2d8ad84293df7efd16d`)
- `Library/PackageCache/com.unity.test-framework@1.1.33/Documentation~/workflow-create-test-assembly.md` (`2e3833d409d3f9d40ca2f3b624a146fd23e95caadb9e42d8a9411482ac130dfa`)
- `Library/PackageCache/com.unity.test-framework@1.1.33/Documentation~/reference-attribute-unitytest.md` (`192cd282ca0c771dd331ce7555453784296079109dcdfb93a00935afb4596623`)
- `Library/PackageCache/com.unity.test-framework@1.1.33/Documentation~/reference-setup-and-cleanup.md` (`3d594cb661ae0d4fda0a17202a5e2657e0f6c54b7f39f829829770ffbe22246c`)
- `Library/PackageCache/com.unity.test-framework@1.1.33/Documentation~/reference-command-line.md` (`ab43d7d49e024b6f5b9aca5c822f89a5dd7ba54fc76b131538e981bd2c548439`)
- `Library/PackageCache/com.unity.test-framework@1.1.33/Documentation~/extension-get-test-results.md` (`8c6be4409177153e9302120278f74774a4b70950b8043d17f55328ffa314aa87`)
- `.agents/skills/es-test-fixture-authoring/SKILL.md` (`49400784a40a646a0fe05bba7b537c18febf31cc37ddab30e4bd331e50c0738c`)
- `.agents/skills/es-test-fixture-authoring/references/fixture-contract.md` (`2132e2c40cfb03ad7afc69ddf6ff649fd850e3a9e157d7c9bc8709933a0f3dd7`)
- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`662f898d15790781d808c4c4e14cd7c0a901a6b678e97d52c4f1ac0dc6fd24d3`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md` (`ab0c4852c76d57c727405cc8a4da597bfeb38a77875ff0b5c23abb1df06b1e8e`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` (`3bb8490dfdf42399110309ada24f51926fdd6b6894a7373f0ef583ec90c52cbc`)
- `Packages/com.esframework.aitest/Runtime/ESAITestObservation.cs` (`bccaa9fc07f2a9992d9c1a032682300f07d5a16bd8513060c0f134ff0b33b817`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/script-CanvasScaler.md` (`fb36337d6a4714789723165ea4d28c7c0448040667ba14d5fb867ed4e0a756b2`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/HOWTO-UIMultiResolution.md` (`fccc33bfb27d315db11d9c8c12e3e5758b2f05536d87de3c051894c67a013335`)

`EvidenceLevel`: `S2`
`StaleWhen`: Unity 或 Test Framework/UGUI 版本、Fixture/视觉证据合同、场景 Builder/Guide 规则、截图实现、测试程序集或任一 SourceRef 哈希变化。
