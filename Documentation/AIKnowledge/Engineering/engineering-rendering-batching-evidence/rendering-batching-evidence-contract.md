# 渲染与合批证据合同

`KnowledgeId`: `es.engineering.rendering-batching-evidence.v1`  
`Authority`: `AIWarnings P0 + governed Skill contracts + current rendering fixture contract`  
`RouteKeys`: `rendering-evidence`, `batching-evidence`, `frame-debugger`, `draw-call`, `setpass`, `batch-break`, `profiler`, `canvas-rebuild`, `shader-variant-log`, `gpu-memory`, `rendering-baseline`, `single-variable-test`, `material-property-block`, `srp-batcher`  
`ContentHash`: `ac0708b6af73bdb4bae915c09846d8434c5addbeb612f48d104c1ba32f42c4da`
`EvidenceLevel`: `S1`  
`StaleWhen`: Unity 通用渲染知识、运行证据 P0、性能/可观察性/发布 Skill、Composite Shader Fixture 合同或任一 SourceRef 哈希变化。

## Scope

本条目只负责“怎样证明渲染、材质、图集或 Canvas 改动的结果”，不负责解释 Shader/Material/SpriteAtlas/Canvas 的通用机制，也不负责实现 Composite Shader 或 Dynamic Atlas。

机制判断转到 `es.unity.rendering-material-atlas.v1`；项目材质 API 转到 `es.project.composite-shader-material-contract.v1`；动态图集生命周期转到 `es.project.dynamic-atlas-runtime-contract.v1`。本条目拥有证据矩阵、单变量实验、采样口径和 non-claims。

## Trigger and routing

自然语言触发包括：“怎么证明合批了”“Frame Debugger 要看什么”“Draw Call 为什么断”“SetPass 降了算不算”“Profiler 怎么采样”“Canvas 拆分是否更快”“质量档成本差异”“图集显存是否达标”“需要哪些截图/日志才能验收”。

- 问“为什么可能断批”：通用机制条目主导，本条目补证据方案。
- 问“当前场景是否已经优化”：本条目主导，并要求目标场景/Prefab/Material/Atlas 的当前输入证据。
- 问 Composite Shader 或 Dynamic Atlas API：对应项目条目主导，本条目只定义完成后的验收。
- 缺少目标平台、场景、输入规模或基线时，不允许输出确定性能结论。

## Decision rules

### 先定义声明，再选证据

| 声明 | 最小证据 | 不能替代 |
|---|---|---|
| 某处批次中断原因 | 同一帧 Frame Debugger 事件、最终材质、主纹理、Canvas/排序、Mask/Stencil 状态 | Inspector 截图、基础 Material 名称 |
| Draw Call/SetPass 改善 | 同场景、同相机、同分辨率、同输入的前后捕获 | 不同帧或不同内容的数字 |
| Canvas 结构更快 | 预热后稳态 Profiler：Canvas rebuild、CPU Main/Render Thread、Draw Call，必要时 GPU | 单帧 Editor 观察 |
| Shader 质量档更便宜 | 同画面目标下的采样/指令或 GPU 对照，并记录视觉差异 | 属性隐藏或 Keyword 名称 |
| Variant 已保留/剥离 | 当前 URP/Quality/目标平台的 Shader 导入与 Player 构建日志 | 源码存在、`.csproj` 编译 |
| 动态图集预算达标 | 输入规模、页/字节/上传峰值 Snapshot + Profiler + 目标平台 | MonitorWindow 单快照 |
| 材质无泄漏 | 重复进入/退出/切换后的对象与内存趋势、明确 Destroy 路径 | 代码里出现 `Destroy` |

### 必须停止或降级

- 没有基线、目标平台、输入规模、预热定义或重复样本：标记 `Deferred`。
- 只有源码、测试定义、截图或单帧数字：只能报告静态事实/观察，不能报告性能通过。
- 目标场景、材质、Atlas、URP Asset 或 SourceRef 在采样后变化：旧证据 stale。
- 启动 Unity、清 Console、运行 Player/Profiler 必须由当前用户明确点名；用户明确要求修改场景或写证据产物时可直接实施。只有受管执行才要求相应 AICommand/TaskContract，缺失只阻断该通道。

## Evidence record schema

每次证据至少记录：`taskId`、branch/HEAD、Unity/URP/UGUI 版本、目标平台、场景与相机、分辨率、输入资产哈希、案例/对象数量、预热规则、采样时间窗、重复次数、唯一变化变量、工具与版本、原始产物路径/哈希、结果、失败/取消/恢复动作。

Frame Debugger 记录每个对照点的事件序号、Pass/Shader、最终 Material、主纹理、Canvas、排序、Mask/Stencil、实例化状态及工具显示的 batch-break 原因；看不到的字段明确写 `unavailable`，不得模型补全。

Profiler 分开记录首次、预热、稳态和扩容阶段；至少选择与声明直接相关的 CPU Main/Render Thread、Canvas rebuild、GC Alloc、GPU、显存或上传指标。平均值不得掩盖峰值，Editor 数据不得直接升级为目标 Player 结论。

## Single-variable matrix

| 变量 | 固定项 | 对照 | 必看结果 |
|---|---|---|---|
| Atlas | Material、Canvas、Mask、排序、对象数 | 独立纹理 / 同页图集 | 主纹理、Draw Call、视觉 |
| Material | 纹理、Canvas、层级、参数画面 | 共享材质 / 受管实例 | 最终材质、SetPass、实例数 |
| MPB / SRP Batcher | Mesh、Shader、纹理、相机、对象数、画面目标 | MPB / 受管 Material 或 Variant / 适用的 Instancing | SRP Batcher 状态、Draw Call/SetPass、CPU Render Thread、材质实例数、GC、视觉 |
| Mask/Stencil | 素材、材质、Canvas | 无 Mask / Stencil / RectMask2D | `materialForRendering`、批次、裁切 |
| Canvas | 元素、视觉顺序、分辨率 | 单 Canvas / 有理由的嵌套 Canvas | Rebuild、Draw Call、CPU/GPU |
| Sibling | 所有资源与 Canvas | 仅允许视觉等价顺序 | 遮挡截图、批次；视觉错误立即否决 |
| Quality/Keyword | 场景、视角、纹理、时间 | Basic / Standard / High | 画面、Keyword、GPU/采样、构建日志 |
| Dynamic Atlas budget | 内容集合、revision、Request | 基线 / 目标 policy | 页数、字节、上传峰值、失败/恢复 |

## Common AI failure modes

| 错误行为 | 症状/根因 | 正确动作 | 恢复 |
|---|---|---|---|
| 用 Inspector 截图证明合批 | 看不到原生最终批次 | 捕获 Frame Debugger 并绑定事件上下文 | 撤销结论，重新捕获 |
| 同时改 Atlas、Material、Canvas | 无法归因改善/回归 | 按单变量矩阵逐项实验 | 回到基线逐一重放 |
| 只报 Draw Call | SetPass、CPU rebuild、视觉或 GPU 回归被遗漏 | 指标随声明选择，保留视觉正确性硬门禁 | 补齐矩阵，不能平均抵消失败 |
| MPB 不克隆材质就判定更快 | 忽略 URP 下可能退出 SRP Batcher；把 Owner 正确性当性能证据 | 固定同一输入，对照 MPB、受管 Material/Variant 与适用 Instancing，并同时记录 SRP 状态和 CPU Render Thread | 撤销性能结论；缺目标平台稳态样本时 `Deferred` |
| 用 Editor 单帧报性能 | 首次导入、噪声和编辑器开销污染 | 明确预热与稳态窗口，目标平台重复采样 | 将旧数据降为观察 |
| 无基线说“降低” | 比较对象不存在 | 先冻结同输入 baseline | 无法恢复基线则 Deferred |
| 为合批重排 UI 导致遮挡错误 | 优化覆盖视觉合同 | 截图/像素对照先通过，再看批次 | 立即恢复正确顺序 |
| 测试源码存在即报通过 | 定义与执行证据混淆 | 绑定 Test Runner 结果、时间、HEAD 和日志 | 标记 runtime-not-run |
| Frame Debugger 数字外推 Player | 平台、驱动、构建配置不同 | 分开 Editor 与目标 Player 结论 | 目标平台未跑则 Blocked |
| Monitor Snapshot 当显存验收 | 无时间窗、规模和 GPU 证据 | Snapshot + Profiler + 输入规模 + 峰值 | 重采样并保留原始产物 |
| 只留总结不留原始产物 | 结论不可重放 | 保存日志/capture/截图及 SHA-256 | 缺原始证据则降低等级 |

## Execution checklist

### 开始前

- 写一句可证伪声明，并选择对应证据层级。
- 固定 HEAD、版本、平台、场景、相机、分辨率、资产哈希、对象数量和视觉基线。
- 定义首次/预热/稳态/扩容阶段、采样时间窗、重复次数、阈值和停止条件。
- 保存 baseline，确认取消/失败不会改写正式 Scene、Prefab、Material 或 Atlas。

### 采集中

- 一次只改一个变量；视觉错误立即否决性能收益。
- Frame Debugger 与 Profiler 捕获使用同一输入；记录工具无法显示的字段。
- 保留失败、取消、超时、Domain Reload 和恢复结果，不只保存成功样本。
- 不清除或覆盖用户现有 Console/证据，除非当前授权明确允许。

### 完成后

- 比较 baseline 与 candidate 的相同指标、相同阶段和相同平台。
- 保存原始产物、摘要、输入哈希和复现步骤；重复执行检查结果是否稳定。
- 将每条结论限定到实际通过的场景/平台/输入规模；缺失行保持 `not-run` 或 `Blocked`。
- 发布、Player、IL2CPP 与资源发布证据必须走各自验收，不由本条目自动升级。

## Routing probes

以下 10 个探针用于索引注册后的最小路由验收：

| 用户任务 | 预期主路由 | 预期条目 |
|---|---|---|
| 同一 SpriteAtlas 为什么还是两个 Draw Call | `batch-break`, `frame-debugger` | 通用渲染 + 本条目 |
| 两个 SpriteRenderer 用同材质做不同扫光 | `material-property-block`, `composite-shader` | Composite + 通用渲染 |
| UI 动态材质切换后是否泄漏 | `runtime-material-instance`, `rendering-evidence` | Composite + 本条目 |
| 远端头像更新后还是旧图 | `dynamic-atlas`, `atlas-lease` | Dynamic Atlas |
| Provider 重建时为什么显示占位 | `atlas-provider`, `atlas-recovery` | Dynamic Atlas |
| 动态图集 64MB 是否真的够 | `atlas-budget`, `gpu-memory` | Dynamic Atlas + 本条目 |
| RectMask2D 下批次在哪里断 | `rect-mask-2d`, `batch-break` | Composite + 通用渲染 + 本条目 |
| Basic/High 质量档真的更省吗 | `quality-tier`, `profiler` | Composite + 本条目 |
| Canvas 拆分后性能有没有改善 | `canvas-rebuild`, `profiler` | 通用渲染 + 本条目 |
| Shader Variant 是否在构建中剥离 | `shader-variant-log` | 通用渲染 + 本条目 |

`es.project.composite-shader-material-contract.v1`、`es.project.dynamic-atlas-runtime-contract.v1` 与本条目均已注册；旧合并条目只保留歧义路由投影。对应 routeKeys 可进入静态 top-3 选择，但在路由探针实际覆盖 Dynamic Atlas 的 Lease、预算和恢复场景前，只能声明静态索引闭包，不能声明自然语言路由已完成验收。

## Evidence boundary

本条目是证据设计合同，S1 静态来源只能证明记录要求、矩阵和 non-claims。它本身不证明任何 Draw Call、SetPass、GC、Canvas rebuild、GPU、显存、Variant、Player、IL2CPP 或发布结果。

`EvidenceRefs`: 未绑定本轮 Frame Debugger、Profiler、Test Runner、Player 或发布产物。`runtime-not-run`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md` (`ef80427c19ab315e9d69ec810caaabb0164a7a2b93f6406d7ee4c5cdd8b7d740`)
- `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md` (`663b07bd7624ea5ad1ce497fbd9487cb93405fa45377c2bbfa59c202f71bce3c`)
- `Assets/ESTestAssets/CompositeShaders/README.md` (`a5d383611245af23f2097a43a4d40540d5c2dd62638f7db8ebe47b8760d03686`)
- `.agents/skills/es-performance-budgeting/SKILL.md` (`ad425ea2d8e1d1d8fb2c71c37152c8b731462e8dadcc595d0834364b3df6bce1`)
- `.agents/skills/es-observability-evidence/SKILL.md` (`0c406d20958c00a1ed87358a0aa722a4b6b6066ff4b402ff5304d223cdc2bc55`)
- `.agents/skills/es-release-acceptance/SKILL.md` (`8cc50a64bf90c8c8302836255b7a022f2aa33040fb02065e1d4448755f8b27c6`)
