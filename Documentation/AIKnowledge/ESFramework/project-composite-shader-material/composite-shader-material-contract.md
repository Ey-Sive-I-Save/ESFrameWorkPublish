# ES Composite Shader 材质与参数写入契约

`KnowledgeId`: `es.project.composite-shader-material-contract.v1`  
`Authority`: `Current project source + AIWarnings + test definitions`  
`RouteKeys`: `composite-shader`, `es-composite-shader`, `shader-gui`, `material-property-block`, `srp-batcher`, `runtime-material-instance`, `shader-keyword`, `quality-tier`, `stencil`, `rect-mask-2d`, `particle-vertex-stream`, `material-lifecycle`  
`ContentHash`: `70e0b999e7693f46e28306ebe407ba77a5485aa0fb03b8c023dff7ba6a07e1b8`
`EvidenceLevel`: `S1`  
`StaleWhen`: Unity/URP 的 MPB 与 SRP Batcher 合同、ES Composite Shader 参数 API、ShaderGUI 材质状态同步、测试 Animator、测试资产生成合同、合同测试或任一 SourceRef 哈希变化。

## Scope

本条目负责 AI 在 ES Composite Shader 中选择参数 API、区分 Renderer 与 UGUI 材质写入路径、管理运行时材质实例，以及规划质量档、Stencil/RectMask2D、粒子顶点流和效果组合验收。

本条目不负责 Unity 通用 Shader/Material/Canvas 合批机制；该事实由 `es.unity.rendering-material-atlas.v1` 持有。本条目不负责动态图集 Lease、页预算或 Provider 恢复；转到 `es.project.dynamic-atlas-runtime-contract.v1`。实际 Draw Call 和性能结论转到 `es.engineering.rendering-batching-evidence.v1`。

当前索引中的 `es.project.shader-atlas-rendering.v1` 仍复制了本条目与 Dynamic Atlas 的部分事实。受控注册时应将旧条目压缩为共享路由投影，只保留分流条件和到两个 canonical 条目的链接；在该迁移完成前，本条目是详细 canonical 候选，不得声称已经接管正式路由。

## Trigger and routing

自然语言触发包括：“ESCompositeShader 参数怎么改”“为什么 MPB 不生效”“UI 材质实例怎么释放”“质量档只改了浮点值”“Stencil/RectMask2D 下材质异常”“粒子顶点流缺失”“同材质两个 Renderer 做不同效果”“效果顺序怎么验收”。

- Renderer 实例参数问题：本条目为 canonical；需要 Unity 通用合批原理时最多再加载 `es.unity.rendering-material-atlas.v1`。
- UI Mask/Stencil 问题：本条目负责项目材质生命周期，Unity 最终材质链由通用条目负责。
- Frame Debugger、Profiler、Draw Call 或 SetPass 结论：追加证据条目，不在本条目估算结果。
- 仅出现宽泛 `shader` 或 `material` 时，先按对象是否为 `ESCompositeShader` 收窄；不能确认则回退通用条目。

## Decision rules

### 可以继续

- SourceRef 与 ContentHash 闭合，且任务只要求静态 API/所有权判断。
- Renderer 的差异是数值、颜色、向量或纹理覆盖时，先以项目公开参数 API 和 `MaterialPropertyBlock` 保持属性 Owner、避免隐式 `renderer.material` 实例；这只是默认所有权路径，不是无条件性能结论。
- UGUI 动态参数由一个明确 Owner 持有运行材质实例，并有重建与销毁路径。

### 必须先读或停止

- 改 Keyword、RenderQueue、Pass、Blend、Cull、ZWrite 或其他渲染状态前，必须读取目标 Shader、ShaderGUI 同步逻辑和正式材质；这些状态不能由 MPB 替代。
- 使用质量档前，必须确认公开参数 API 同步了互斥 Keyword；只写 `_QualityTier` 浮点值不得继续宣称质量档已切换。
- UI 进入 Stencil/Mask 链时，必须检查 `materialForRendering` 的最终结果，不能从基础 Material 名称推断。
- 目标是“更快、更少批次或保持 SRP Batcher”时，必须追加读取 `es.unity.rendering-material-atlas.v1`。若 Renderer 使用 MPB 且目标 Shader/管线启用 SRP Batcher，必须停止性能推断，转入同输入 A/B：MPB、受管 Material/Variant 和适用的 Instancing；缺 Frame Debugger 与目标平台稳态 Profiler 时标记 `Deferred`。
- 当前用户明确要求修改 Shader、Material、Prefab、Scene 或生成器时可直接实施；Unity 状态动作必须被用户单独点名。只有选用受管通道时才要求匹配 AICommand/TaskContract，缺失只阻断该通道。
- 任一 SourceRef 漂移时标记 `stale`，重新读取来源并重算 ContentHash。

## Verified facts

- 项目公开参数表将 Shader 属性集中为 ID，并提供 2D、3D Lit、3D VFX、UI 四类 `MaterialPropertyBlock` 写入 API；四类 Shader 的职责、属性和质量规则不能互相假定。[来源：`ESCompositeShaderParameters.cs`、Composite Shader AIWarning]
- Renderer 实例级数值、颜色、向量和纹理覆盖优先使用 MPB；Keyword 与渲染状态需要受生命周期管理的独立 Material。[来源：Composite Shader AIWarning]
- UGUI `Graphic` 没有 Renderer MPB 路径。现有专用测试 Animator 对 UI 使用运行时材质实例，对 Renderer 使用 MPB，并在回正/销毁路径处理其所有权。[来源：`ESCompositeShaderTestAnimator.cs`、测试资产 README]
- ShaderGUI 的显示模式只允许改变信息密度；材质状态、Keyword、预设和迁移必须走各自明确动作，不能由面板模式切换隐式触发。[来源：Composite Shader AIWarning、`ESCompositeShaderGUI.MaterialState.cs`]
- 现有 Fixture 定义六个场景、57 个案例材质以及 Stencil、RectMask2D、质量档、效果顺序、MPB 差异和粒子顶点流观察点。它们是测试定义与静态资产事实，不是本轮执行通过证据。[来源：测试资产 README、合同测试]

## Common AI failure modes

| 错误行为 | 症状/根因 | 预防与正确动作 | 恢复与证据缺口 |
|---|---|---|---|
| 调用 `renderer.material` 改每对象参数 | 材质实例数量增加、共享材质失效 | `GetPropertyBlock` 后只覆盖当前 Owner 属性，再 `SetPropertyBlock` | 清理实例需独立授权；用 Frame Debugger/Profiler 复核 |
| 新建空 MPB 后覆盖 | 其他写入者参数消失 | 先读取 Renderer 现有 MPB，不调用无所有权的 `Clear` | 重建各 Owner 状态；缺运行回放时保持 Deferred |
| 因 MPB 不克隆 Material 就宣称 URP 性能更好 | 忽略 MPB 可能退出 SRP Batcher，所有权正确但 CPU 渲染成本回归 | 先读通用渲染条目并检查 Shader/管线状态，再执行同输入 A/B | 保留 MPB 的 Owner 语义但撤销性能结论；缺目标平台样本时 `Deferred` |
| 对 UGUI 套用 MPB | 参数不生效或另造错误路径 | 使用缓存运行材质实例；定义目标/源材质/Shader 变化时的重建 | 在 Disable/Destroy/替换时释放；检查最终材质链 |
| 每帧 `new Material` | GC/Native Material 泄漏、断批 | 仅在输入身份变化时重建，稳态复用 | 记录实例数与 Destroy 路径；未采样不得声称无泄漏 |
| 用 MPB 改 Keyword/Queue/Blend | 画面不变或状态仍共享 | 使用明确材质实例和项目 Keyword 同步 API | 回读目标 Shader/GUI；验证最终 Keyword/Pass |
| 只写质量浮点值 | Inspector 显示变化但 Variant 未切换 | 使用四类公开参数 API同步互斥质量 Keyword | 检查材质 Keyword 与画面对照；构建剥离仍未证明 |
| 看到同名属性就跨 Shader 复用 | 某类 Shader 无 Pass/消费逻辑 | 按 2D、UI、3D Lit、3D VFX 分别检查属性与 Pass | 回退到对应类 API；缺失属性则停止而非补猜测 |
| 把 Stencil/RectMask2D 异常归因于 Shader 参数 | 忽略 UGUI 修改材质链或宿主几何裁切 | 同时检查最终材质、Stencil 深度、RectMask2D 和几何边界 | 以单变量场景和视觉捕获复验 |
| 把测试场景存在当验收 | 未实际进入 PlayMode/观察/Profiler | 只报告 fixture available；执行后绑定回执 | 当前证据为 runtime-not-run |

## Execution checklist

### 开始前

- 确认目标属于 2D、UI、3D Lit 或 3D VFX，并读取目标 Shader、参数 API 和 AIWarning。
- 记录写入对象、属性 Owner、共享材质身份、是否需要 Keyword/渲染状态差异。
- 判断 Renderer MPB、UGUI 运行材质实例或独立资产材质三条路径，禁止混用。

### 实施中

- Renderer：读取现有 MPB，只写当前 Owner 属性，不实例化共享材质。
- 若任务含性能目标，同时记录 SRP Batcher 兼容状态；不得用“未实例化 Material”替代 Frame Debugger/Profiler 对照。
- UGUI：缓存实例；源材质、Shader 或目标变化时先释放旧实例再重建；禁止每帧创建。
- 质量档、父开关和子参数按公开 API 同步；粒子 VFX 同时核对所需 Vertex Streams。
- 每次只改变一个效果、质量档或材质生命周期变量，并保留无效果基准。

### 完成后

- 静态检查 PropertyId、Shader 属性、Keyword、ShaderGUI、材质 Owner 与销毁路径闭环。
- 在六场景 Fixture 中验证回正、Stencil、RectMask2D、效果顺序、MPB A/B、质量档与粒子顶点流。
- 需要性能结论时转入证据条目，记录 Frame Debugger 与 Profiler；需要发布结论时另走发布验收。

## Evidence boundary

静态证据可证明 API、Owner、测试定义和预期清理路径存在。它不能证明 Shader 已导入、视觉正确、Stencil/RectMask2D 实际兼容、材质未泄漏、Draw Call 减少、Variant 已剥离或 Player/IL2CPP 通过。

`EvidenceRefs`: 未绑定本轮 Unity、PlayMode、Frame Debugger、Profiler、Player 或发布回执；现有测试文件仅作为测试定义 SourceRef。`runtime-not-run`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md` (`743bd3b3b031ed527bbc6d76f04111bdf985cf423a2a092458385602b498863d`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESCompositeShaderParameters.cs` (`582012f2a6554d29de98ddd24b4e1ef21b13f5df462d1ab2a78fb3886a5dfc37`)
- `Assets/Plugins/ES/Editor/ESShader/ESCompositeShaderGUI.MaterialState.cs` (`dfb217189e6d3bda04adbce4608dd88cbc996ff802f8036902e2970cea6365aa`)
- `Assets/Plugins/ES/Editor/ESShader/Tests/ESCompositeShaderContractTests.cs` (`32dbdd9b35783beead5a76363cdf41d37b813f7da6fbcde7670be6acc6e2da35`)
- `Assets/ESTestAssets/CompositeShaders/README.md` (`a5d383611245af23f2097a43a4d40540d5c2dd62638f7db8ebe47b8760d03686`)
- `Assets/ESTestAssets/CompositeShaders/Runtime/ESCompositeShaderTestAnimator.cs` (`fce23db1cf20278f05b3acf0b1be6f058d2a7149b1f662341bce377a8e554bd3`)
