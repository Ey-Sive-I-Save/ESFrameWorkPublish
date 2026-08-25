# Unity Shader、Material、SpriteAtlas 与 Canvas 合批边界（2022.3.45f1）

`KnowledgeId`: `es.unity.rendering-material-atlas.v1`
`Authority`: `Unity 2022.3 package documentation + package source + current official documentation risk note + project version`
`RouteKeys`: `unity`, `rendering`, `shader`, `material`, `shader-keyword`, `shader-variant`, `material-variant`, `sprite-atlas`, `ui-canvas`, `canvas-sorting`, `ui-batching`, `draw-call`, `frame-debugger`, `srp-batcher`, `material-property-block`, `mask`, `stencil`, `batch-break`
`ContentHash`: `ade199e17593a9ffe4c5ff2d85771aae62e09b56bb8c2623f403a2bfb06507aa`

## Scope

本条目整理 ESFramework 当前声明版本 `Unity 2022.3.45f1`、
`com.unity.render-pipelines.universal@14.0.11` 与 `com.unity.ugui@1.0.0` 下，
Shader、Material、Keyword、SpriteAtlas、Canvas 排序和合批之间的职责边界。
它是 Unity 官方随包文档与包源码的静态投影，负责回答“Unity 机制允许什么、哪些条件可能断批、需要什么证据才能下结论”。

本条目不负责：

- ESCompositeShader 的项目参数写入约定转到 `es.project.composite-shader-material-contract.v1`；ESDynamicAtlas 生命周期仍转到对应项目运行时条目。Unity 通用的 MPB、SRP Batcher 和 UGUI 提交机制由本条目持有。
- RectTransform、CanvasScaler 和响应式布局；转到 `es.unity.ui-canvas-layout.v1`。
- Unity 编译、Player、IL2CPP 或发布是否通过；转到对应编译/发布证据条目和 Skill。

一事实一归属：Unity 通用机制只在本条目维护；相邻条目只描述项目差异、适用条件和交叉链接，不复制本条目的通用结论。

## Trigger and routing

自然语言触发包括：“同一个图集为什么还不合批”“Draw Call 在哪里断了”“SRP Batcher 对 UGUI 有没有用”“Mask/Stencil 为什么换材质”“Material Variant 会不会减少批次”“Keyword/Variant 太多”“Frame Debugger 怎么定位 UI 断批”“能不能为合批调整 Sibling 顺序”。

当前索引 routeKeys 为：`unity`、`rendering`、`shader`、`material`、`shader-keyword`、`shader-variant`、`material-variant`、`sprite-atlas`、`ui-canvas`、`canvas-sorting`、`ui-batching`、`draw-call`、`frame-debugger`、`srp-batcher`、`material-property-block`、`mask`、`stencil`、`batch-break`。

- 纯 Unity 渲染/合批问题：首选本条目，最多再加载一个运行证据条目。
- 出现 `ESCompositeShader`、`ESDynamicAtlasGraphic`、远端头像、UGC、Lease 或动态图集恢复：本条目与 `es.project.shader-atlas-rendering.v1` 各司其职。
- 出现 RectTransform、CanvasScaler、锚点或响应式布局：改由 `es.unity.ui-canvas-layout.v1` 主导，本条目只补充 Canvas 渲染边界。
- 仅靠宽泛 `ui` 或 `material` 命中超过 3 个条目时，停止扩读；回到 `KnowledgeIndex.yaml`，用对象、动作和风险收窄路由。
- 零命中时读取 AIWarnings Start、CurrentStatus、RuleIndex 与当前包源码，并报告 Knowledge 覆盖缺口；禁止用相似条目或模型记忆替代。

## Decision rules

### 可以继续

- SourceRef 路径存在、SHA-256 与声明一致，ContentHash 重算通过，并且索引唯一绑定本 KnowledgeId。
- 问题只要求静态机制判断，结论明确标注为源码/官方文档事实，不声称实际 Draw Call、性能或构建结果。

### 必须先读

- 涉及项目 Composite Shader：先读 `es.project.composite-shader-material-contract.v1` 及其 requiredReads；涉及动态图集时读取对应项目运行时条目。
- 涉及目标场景、Prefab、Material 或 Atlas 资产状态：先读对应正式资产和当前配置，不能从本条目推断。
- 涉及实际合批、性能、Variant 剥离或发布：先取得 Frame Debugger、Profiler、Player 构建日志或发布回执。
- 涉及写入 Material、Shader、Atlas、Prefab、Scene 或索引：当前用户明确目标即授权直接修改；需要 AIBrain/Worker 的操作才必须匹配 AICommand 与当前 TaskContract。

### 必须停止或降级

- 任一 SourceRef 缺失或哈希漂移：标记 `stale`，丢弃旧计划，回读来源并重算 ContentHash。
- 静态来源无法证明目标场景状态：标记 `Deferred`，列出需要检查的资产或配置。
- 缺少所需运行证据会限制 Runtime/Release 结论；AICommand、TaskContract 或能力连接缺失只阻断选中的受管通道。`PlanTaskUnavailable` 不得写成 `NoMatchingCommand`，也不得写成当前用户未授权。
- 最终材质、主纹理、Canvas 边界或原生批次划分不可见时，不得给出确定 Draw Call 数量。

## Verified facts

以下事实均为静态事实。路径是本条目 `SourceRefs` 中的现有来源，不代表已执行 Unity。

### Shader、Material 与 Keyword

- Shader 定义渲染 Pass、GPU 程序和可由材质提供的属性；Material 选择 Shader 并保存该实例的属性与渲染状态。材质相同不自动证明对象会合批。[来源：`shaders-in-universalrp.md`、`configure-for-better-performance.md`]
- URP 使用 Shader Keyword 表达离散功能组合，并为组合编译 Shader Variant。只有所有构建所用 URP Asset 都关闭相关功能时，对应 Variant 才具备被剥离的前提。[来源：`shader-stripping.md`]
- Material Variant 是父材质与覆盖项的资产继承机制，不等同于运行时材质实例或 PropertyBlock，也不证明更少的 Draw Call。[来源：`materialvariant-URP.md`]
- SRP Batcher 减少 Draw Call 之间的状态设置成本，与 UGUI Canvas 合批不是同一机制。[来源：`configure-for-better-performance.md`]
- MPB 是 `Renderer.SetPropertyBlock` / `Graphics.RenderMesh` 的逐对象属性覆盖机制，不能改变渲染状态。Unity 当前 Scripting API 明确警告 MPB 与 SRP Batcher 不兼容；URP 14 的 Decal 文档也给出因使用 MPB 而不支持 SRP Batcher 的具体案例。因此“避免材质实例化”和“保持 SRP Batcher 性能路径”是两个可能冲突的目标，必须按当前版本与场景验证，不能从任一机制名称直接推导更快。[来源：`official-source-lock.md`、`renderer-feature-decal.md`]

### SpriteAtlas 与 UGUI Image

- `Image.mainTexture` 来自 `activeSprite.texture`；`Graphic.UpdateMaterial` 把 `materialForRendering` 和 `mainTexture` 分别提交给 `CanvasRenderer`。[来源：`Image.cs`、`Graphic.cs`]
- `Image` 监听 `SpriteAtlasManager.atlasRegistered`；可绑定的 Atlas 注册后会使 Image 重新标脏并重建。[来源：`Image.cs`]
- SpriteAtlas 只提供共享纹理页的机会，不会抹平最终材质、Stencil、裁剪、Canvas 或排序差异。[来源：`Image.cs`、`Graphic.cs`、`MaskableGraphic.cs`]

### Canvas 排序与合批边界

- 同一 Canvas 内，Hierarchy/Sibling 顺序首先是视觉排序合同；后出现且重叠的元素显示在前面。[来源：`UICanvas.md`]
- 嵌套 Canvas 继承父 Canvas 的 Render Mode，并可建立独立更新或排序范围；它同时也是需要单独验证的渲染边界。[来源：`UICanvas.md`、`class-Canvas.md`]
- `Graphic.materialForRendering` 依次经过 `IMaterialModifier`；`MaskableGraphic` 可以根据 Stencil 深度生成修改材质。[来源：`Graphic.cs`、`MaskableGraphic.cs`]
- C# 包源码可以证明提交给 `CanvasRenderer` 的关键输入和 Mask 修改材质路径，但最终批次划分位于原生渲染侧，不能仅靠这些源码推导 Draw Call 数量。[来源：`Graphic.cs`、`MaskableGraphic.cs`]

## Decision boundaries

| 需求 | 首选机制 | 必须警惕的边界 |
|---|---|---|
| 多个可预打包 UI Sprite | `Image + SpriteAtlas` | Atlas 只统一纹理机会；材质、Mask、Canvas 与排序仍可能断批 |
| Shader 的离散编译功能 | 有界 Keyword/Variant | Variant 组合、URP Asset 差异、构建剥离与目标平台 |
| 连续颜色、强度、UV 参数 | Material 属性或受管实例参数 | 不要为连续值制造 Keyword；UGUI 没有 Renderer PropertyBlock 路径 |
| Renderer 的逐对象连续参数 | 先按 Owner 与渲染管线比较 MPB、受管 Material 和可用 Instancing 路径 | MPB 可避免隐式材质实例，但可能退出 SRP Batcher；没有同输入 A/B 证据不得宣称最快 |
| 同 Canvas 内前后关系 | Hierarchy/Sibling 顺序 | 视觉正确性优先，不以重排遮挡换取未经证明的合批 |
| 独立排序或高频更新区域 | 经过证据支持的嵌套 Canvas | 新 Canvas 会建立额外边界，需以 Frame Debugger 和 Profiler 衡量收益 |
| Mask/Stencil UI | `MaskableGraphic` 的修改材质链 | 基础 Material 相同不代表 `materialForRendering` 相同 |

## Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查与正确动作 | 恢复与缺失证据 |
|---|---|---|---|
| 跳过 AIBrain/AIWarnings 发现链 | 直接搜索源码后套用旧经验；前置规则未加载 | 先读 AIBRAIN_ENTRY、KnowledgeIndex、Start、CurrentStatus、RuleIndex，再选最小条目 | 停止当前结论，补齐发现链并重新规划 |
| 一次加载过多 Knowledge | 多个宽泛 `ui/material` 条目互相污染 | 限制 1～3 个条目；按对象、动作、风险收窄 routeKeys | 丢弃无关上下文；零命中或仍过宽时报告覆盖缺口 |
| 把 Knowledge 摘要当源码事实 | 版本或项目状态已变但仍沿用条目结论 | 每个事实回到绑定 SourceRef，并按 Authority 顺序裁决 | 任一来源不闭合即标记 `stale`，不得继续引用旧结论 |
| 把临时扫描、旧上下文或旧快照固化 | 一次观察被写成长期机制 | 只固化当前权威来源能证明的稳定合同，瞬时结果写入运行证据而非 Knowledge | 删除夸大结论需要单独授权；先标记 `Deferred/Blocked` 并重新取证 |
| “进同一 Atlas 就会合批” | Draw Call 仍分裂；忽略最终材质、Stencil、Canvas 或排序 | 比较 `materialForRendering`、主纹理、Mask/Stencil、Canvas 和层级顺序 | 回退单变量实验；缺 Frame Debugger 记录时保持 `Deferred` |
| “开启 SRP Batcher 就能减少 UGUI Draw Call” | 开关已开但 UI 批次不变；混淆状态设置优化和 Canvas 合批 | 分开评估 SRP Batcher CPU 状态切换与 UGUI 批次条件 | 撤销结论；缺 Profiler/Frame Debugger 时不报性能收益 |
| “MPB 不实例化材质，所以在 URP 一定更快” | CPU 渲染路径回退或批处理结果变差；把所有权正确性误写成性能结论 | 先确认 Shader 的 SRP Batcher 兼容状态，再以同场景比较 MPB、受管 Material/Variant 和可用 Instancing | 保留正确的 Owner/销毁语义，撤销性能断言；缺目标平台稳态样本时标记 `Deferred` |
| 为连续参数创建 Keyword | Variant 数量膨胀；把数值状态当离散编译路径 | 先判断是否真正改变编译分支，再检查 URP Asset 和目标平台 | 改回属性；缺 Player 构建日志时不声称已剥离 |
| 把 Material Variant 当运行时实例或合批工具 | 继承关系正确但批次未减少 | 只把它用于资产继承；单独验证最终材质和批次 | 恢复父材质/覆盖关系；缺目标资产检查时标记 `Deferred` |
| 给 UGUI Image 套用 Renderer PropertyBlock 经验 | 参数未生效或误建材质实例；UGUI 走 CanvasRenderer 提交路径 | 回读 `Graphic.UpdateMaterial`，选择 UGUI 支持的受管材质路径 | 清理错误实例需要独立写权限；缺资产证据时停止 |
| 基础 Material 相同就断言最终材质相同 | Mask 区域意外断批；忽略 `IMaterialModifier` | 检查 `materialForRendering` 和 Stencil 深度 | 从 Mask 层级向 root sort override Canvas 追踪；缺运行捕获时不估算批次 |
| 为合批重排 Sibling | 遮挡顺序错误；把视觉合同让位给推测优化 | 先冻结正确视觉顺序，再做不改变结果的单变量实验 | 立即恢复原顺序；需要截图和 Frame Debugger 双证据 |
| 拆分/合并 Canvas 后直接宣称更快 | Rebuild 或 Draw Call 反而增加；缺少代表性采样 | 比较 Canvas rebuild、CPU Render Thread、Draw Call 和目标平台 GPU | 恢复原结构；缺稳态 Profiler 样本时标记 `Blocked` |
| 把文件、按钮或测试源码存在当成功 | 没有真实执行回执 | 将存在性只记为静态事实，运行结论绑定真实证据 | 撤销夸大结论，补运行记录或明确 `runtime-not-run` |
| `PlanTaskUnavailable` 当作 `NoMatchingCommand` | 未授权修改索引、资产或运行状态 | 分别记录命令匹配与能力连接状态 | 停止写入，等待 AIBrain/TaskContract，不借用其他命令 |

## Execution checklist

### 开始前

- 读取 `AIBRAIN_ENTRY.md` 和 `KnowledgeIndex.yaml`，只选 1～3 个最相关条目并完成 requiredReads。
- 校验 Unity/URP/UGUI 版本、SourceRef SHA-256、ContentHash、唯一 KnowledgeId 和索引绑定。
- 分类任务：静态解释、资产检查、运行验证或写入；后 3 类不得借静态知识扩权。

### 实施中

- 一次只改变 Atlas、Material、Mask/Stencil、Canvas 或 Sibling 顺序中的一个变量。
- 记录最终材质、主纹理、Canvas 边界、排序以及实际观察工具；不从基础材质名推断最终状态。
- Renderer 使用 MPB 时同时记录 SRP Batcher 兼容状态、实际批处理路径和替代方案；MPB 的正确 API 使用不自动通过性能门禁。
- 对 Keyword 记录组合、URP Asset/Quality 配置、目标平台和预期剥离条件。
- 取消或中断时停止在当前只读检查点；不保存半完成资产，不把临时扫描写成长久事实。

### 完成后

- 静态任务运行 Knowledge entry、SourceRef、ContentHash、索引和 UTF-8 验证，并重复一次确认幂等。
- 运行任务保存 Frame Debugger/Profiler/构建日志的可重放证据，并把测试定义与执行结果分开。
- 写入 Material、Shader、Atlas、Prefab 或 Scene 时，另行验证 Undo、Dirty、Save、Rollback 和 Domain Reload；这些事务不适用于本条目自身的只读使用。

### 禁止事项

- 禁止仅凭同 Atlas、同基础 Material、启用 SRP Batcher 或 Inspector 截图声明合批成功。
- 禁止用 Editor 单帧代替稳态 Profiler、Player 或目标平台证据。
- 禁止 AI 在当前用户范围外自行修改资产、索引、Unity 状态、Git、历史或发布状态；用户已经明确点名的动作不以匹配 AICommand 为前置条件。

## Evidence boundary

静态检查可以确认：版本与包锁定、SourceRef 哈希、Keyword/Variant 剥离合同、UGUI 提交材质与纹理的代码路径、SpriteAtlas 注册后的 Image 重建路径，以及 Mask 修改材质的路径。

要确认实际批次和性能，最小运行证据应包含：

1. 在目标 Canvas 和代表性 UI 层级中使用 Frame Debugger，记录 Draw Call、批次中断点、最终材质、主纹理和排序结果。
2. 分别切换 Atlas、Material、Mask/Stencil、嵌套 Canvas 与 Sibling 顺序，每次只改变一个变量。
3. 对 Keyword 组合执行当前 URP Asset/Quality 配置下的 Shader 导入和 Player 构建日志检查，确认保留与剥离的 Variant。
4. 使用 Profiler 对比 Canvas rebuild、CPU Render Thread 和目标平台 GPU 指标；不要用 Editor 单帧截图替代稳态采样。
5. 对 Renderer 逐对象参数固定同一 Shader、对象数、材质状态和观察窗口，对比 MPB、受管 Material/Variant 与适用的 Instancing 路径；同时记录材质实例数、SRP Batcher 状态、Draw Call/SetPass、CPU Render Thread、GC 与峰值。

`EvidenceRefs`: 本条目没有绑定 Runtime、Profiler、Player、IL2CPP 或发布执行回执。SourceRefs 只支持 S2 静态知识，不支持把目标场景标记为已验证。

## Assumptions and non-claims

- 本条目假设目标 UI 使用 UGUI，而不是 UI Toolkit。
- `runtime-not-run`：本轮未启动 Unity Editor，未执行 Shader 导入、Frame Debugger、PlayMode、Profiler、Player 或 IL2CPP 验证。
- 本条目不声明任何具体场景已经合批、Draw Call 已减少、Variant 已成功剥离、视觉效果正确、性能达标或发布通过。
- 本条目不证明项目现有 Composite Shader、动态 Atlas、Material 资产、Prefab 或 Scene 的当前运行状态。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/Manual/shader-keywords.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/shader-variants.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/materialvariant-landingpage.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/SRPBatcher.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/SpriteAtlasWorkflow.html
- https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/shader-stripping.html
- https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UICanvas.html
- https://docs.unity3d.com/ScriptReference/MaterialPropertyBlock.html
- https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Graphic.html
- https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.RectMask2D.html
- https://docs.unity3d.com/Manual/FrameDebugger.html
- https://docs.unity3d.com/Manual/Profiler.html

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Packages/packages-lock.json` (`6db87482785cd1b498aeb7386723c5b8f23fe7f79c8f3e2d409bf0206b48796f`)
- `Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Documentation~/shaders-in-universalrp.md` (`fa57fb1a4922f249dece046c99465863428eb1a81eb0b5c97910ea4733e95a55`)
- `Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Documentation~/shader-stripping.md` (`c17f9388ef66cee01a0f0b10663dc1ff2e0550bef63bd66d6a5490cc1dc5edf3`)
- `Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Documentation~/materialvariant-URP.md` (`88b100eb4adafa9497e5cab1b037773fbfb1f58fcdfa90e34f6de6f01944cb93`)
- `Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Documentation~/configure-for-better-performance.md` (`18d9a81fdfdc4c96fb40a41b26811825424866bc5ae585a2b7bc3d0e4b7526f7`)
- `Library/PackageCache/com.unity.render-pipelines.universal@14.0.11/Documentation~/renderer-feature-decal.md` (`6f3a9df6f251b274ec7b5ed1e99c204d62e2d3a6bc919bcca8f70386d46daa3c`)
- `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/official-source-lock.md` (`8a4bf0e28b1f1abba0d588485bbcc2276136f87ae77601b013dee97ed23fe818`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/UICanvas.md` (`724607c892472f573d6b6475794ebc08a62df7384dbbacc4c1817a0f3d88e0c4`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/class-Canvas.md` (`9d80505f0ac772763c36bdd6961122e4e9df068d1debd5fe95fafccbbcbc7857`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Graphic.cs` (`c23b303effecdb6693f791cbbe703f0c368fd92b1443934ae90d4d97c21dd9b0`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Image.cs` (`351dd72516d240f5722c55f72f8e95a0a92cf71e83358bbc922eee47bdb78502`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/MaskableGraphic.cs` (`96ad95f99e13503587432204eaaaf3110f6b73795715798162c60379ee46408b`)

`EvidenceLevel`: `S2`
`StaleWhen`: Unity 版本、URP/UGUI 包版本、官方页面响应、Shader Keyword/Variant 剥离合同、Material Variant、SpriteAtlas 绑定、Graphic 最终材质/主纹理提交、Mask/Stencil 或 Canvas 排序合同及任一 SourceRef 哈希变化。
