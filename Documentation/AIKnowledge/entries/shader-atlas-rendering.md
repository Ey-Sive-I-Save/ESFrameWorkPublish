# Composite Shader 与 UI 图集分流机制

`KnowledgeId`: `es.project.shader-atlas-rendering.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `shader`, `composite-shader`, `material`, `shader-gui`, `sprite-atlas`, `dynamic-atlas`, `texture`, `ui`  
`ContentHash`: `0f473a6b166a6b42815e8649b4be2f89ed397c9fb421a3d04505c9e642e5da2c`

## 两条互不替代的路径

Composite Shader 解决材质表面、效果组合、质量档位和运行时参数写入；图集解决纹理来源、装箱与采样。二者可以组合，但不能把“用了 Composite Shader”解释成“已完成纹理生命周期管理”。

可预先打包的技能图标、物品图标等，即使运行时才由 `IconKey` 决定，也走 `SpriteAtlas -> Sprite -> Image.sprite`。`ESDynamicAtlasGraphic` 只面向远端头像、UGC、截图、临时 `Texture2D`/`RenderTexture` 等构建前无法收集的纹理。配置动态选择不等于纹理动态生成。

## Composite Shader 合同

运行时代码以集中式 `Shader.PropertyToID` 参数表写 `MaterialPropertyBlock`；交互风、挤压、淡化等组件先读取已有 PropertyBlock，再只修改自己拥有的属性，不能实例化共享材质，也不能清空其他写入者的状态。

`ESCompositeShaderGUI` 是作者入口：绘制属性流、同步关键字、校验材质并提供迁移/预设能力。材质属性、Shader 消费、运行时 PropertyId 和显式宿主状态必须闭环；Inspector 显示正常不是渲染正确的充分证据。质量 Keyword 是材质合同的一部分，默认档位和互斥规则由各 Shader 类型分别定义，不能凭统一经验硬编码。

## 动态图集合同

动态内容由 `DomainKey + ContentKey(value, revision) + Request` 定位。Request 固化 padding、颜色空间、透明度和过滤模式；DomainPolicy 限制页尺寸、页数、GPU 字节、每帧上传数/像素与闲置保留时间。

调用方持有可释放的 DomainLease 和 ContentLease。Lease 不缓存永久 UV，而是通过 token 解析当前 `texture/uvRect`，并带 `slotGeneration`、`placementRevision`、`pageGeneration`；重排、页面恢复或 Provider 切换后必须重新解析。状态区分 Ready、Retired、Recovering、Quarantined、Failed、Lost，Graphic 在非 Ready 状态显示占位，不把旧 UV 当成功结果。

上传路径区分 CopyTexture、PaddingShader 与延迟 Fence fallback。运行时还负责预算上传、GPU Fence/AsyncGPUReadback 完成、闲置回收、隔离与恢复；这是一套有所有权和故障状态的运行时资源系统，不是简单 Texture 缓存。

## 验收与禁区

- 合同测试可证明 Property/Keyword/PropertyBlock 约束，不能代替目标 URP 版本的视觉对比。
- 动态图集 EditMode/PlayMode 测试可覆盖分配、Lease、Provider 迁移与回退，仍需目标平台确认格式支持、GPU 同步和显存预算。
- 禁止把普通 SpriteAtlas 资产批量塞入动态图集；禁止长期缓存一次解析出的 UV；禁止以材质 Inspector 截图作为最终视觉证据。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md` (`743bd3b3b031ed527bbc6d76f04111bdf985cf423a2a092458385602b498863d`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md` (`207f74a74d0f5e9cdcf91c5dd23d4f5afb9f40e3899938460a6c159666d4b5c5`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESCompositeShaderParameters.cs` (`5a1072834cc9182b104be46ceffdd894efd32a75e1b3d935b94cc683386214bc`)
- `Assets/Plugins/ES/Editor/ESShader/ESCompositeShaderGUI.cs` (`9446e44a693a1861b25bb93798f9a05fa99ecb8adea39c10e527999b06e5386d`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasContracts.cs` (`0efeef56604386ae1f9bc174561d610e0a5b3838e6206bc524c10203262ce8bb`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasRuntime.cs` (`4ad8fafdcc1ed9a4e2d2b8516e6bbaafa0a192d897212886bdd6b168f13b34cf`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasGraphic.cs` (`b7fdb5bf72de1973e3e3085d8ceb0ea1e2cbd47657e05cdd1682b43650e95d0a`)

`EvidenceLevel`: `S1`; `StaleWhen`: Shader 属性/Keyword、材质迁移、动态图集 Lease、上传路径或预算策略变化。
