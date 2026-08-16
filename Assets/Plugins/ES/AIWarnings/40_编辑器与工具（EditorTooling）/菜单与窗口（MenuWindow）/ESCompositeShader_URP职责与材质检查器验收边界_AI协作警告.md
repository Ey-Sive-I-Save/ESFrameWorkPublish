# ES Composite Shader URP 职责与材质检查器验收边界 AI 协作警告

> 状态：`Implemented-Unverified`。源码与编辑器入口已经存在，仍缺完整 Unity 视觉、交互、PlayMode、性能与发布验收。
>
> 适用范围：`ES2DCompositeURP`、`ES3DLitCompositeURP`、`ES3DVFXCompositeURP`、`ESUICompositeURP`、`ESCompositeShaderGUI`、材质预设、属性代码示例与运行时材质参数写入。

## 1. 管线与职责

- 当前实现只承诺 URP，不得声称兼容 Built-in 或 HDRP。
- 2D、3D Lit、3D VFX、UI 必须保持独立 Shader 职责与针对性面板；共享绘制和元数据可以复用，但不得用一个万能 Shader 混合四类渲染合同。
- 3D Lit 负责受光材质，3D VFX 负责透明/特效表现，2D 负责 Sprite 类表现，UI 负责 Canvas/Stencil/Mask 语义。跨类别能力必须逐类确认 Shader 属性、Pass、Keyword 与运行结果，不能因 Inspector 显示了同名属性就宣称生效。

## 2. 参数写入边界

- Renderer 上的实例级数值、颜色、向量和纹理覆盖优先使用 `MaterialPropertyBlock`，不得为每个对象无条件克隆 Material。
- Keyword、Render Queue、Pass、Blend、Cull、ZWrite 等渲染状态不属于 PropertyBlock 能力；需要对象级差异时使用独立且受生命周期管理的 Material。
- Unity UI `Graphic` 不提供 Renderer PropertyBlock 路径；UI 动态参数使用缓存材质实例，并在目标、原材质或 Shader 改变时正确重建和释放，禁止每帧创建材质。
- C# 示例必须直接说明所需外部参数和调用位置；可以把 Renderer、Graphic、Material 或 PropertyBlock 作为字段/参数传入，不得把每个示例都写成重复且喧宾夺主的“先获取 PropertyBlock”教程。

## 3. Inspector 模式与预设

- “标准 / 进阶 / 高级”只控制信息密度和属性显隐。切换模式不得写材质、切 Keyword、应用预设或改变渲染结果。
- 所有真实 Shader 属性都必须归入有业务目的的分组或子卡片；标题直接表达效果用途，禁止使用“效果卡片”等无信息名称，也禁止同组重复属性名和无意义说明句。
- Bool 语义即使底层由 Float 承载，也必须以清晰开关呈现；启用项应有独立、可辨识且符合主题的状态底色，分组应有与标题同色的细边框，同时保持深浅主题和窄面板可读性。
- 预设应用前必须能看到差异，只覆盖该预设负责的属性，并支持选择性应用、Undo/Redo 和多材质编辑；未选择或预设无关属性不得被重置。

## 4. 生效关系与 Variant

- 每个父开关必须在 Shader 代码中真正门控对应计算、纹理采样或输出；父功能未启用时，子参数不得改变结果，也不应承担不必要的高成本计算。
- 全局时间源、时间倍率、UV 缩放/偏移、缩放中心和各效果独立速度必须明确组合顺序；场景时间、非缩放时间与自定义时间不得互相冒充。
- 质量档必须映射为明确的计算或采样差异；仅隐藏属性、修改标签或保留同等成本不算质量档生效。
- Variant 必须有界。只把确实改变 Pass/平台编译路径且能显著节省运行成本的能力做 Keyword；高频连续参数、仅编辑器显隐和可用普通分支处理的低风险功能不得无限扩张本地/全局 Keyword 组合。

## 5. 编辑器可用性

- 材质 Inspector 必须在约 220px 窄宽、高 DPI、深浅主题、多选材质和属性缺失场景下保持可操作；右侧短数值与 C# /预览等按钮应使用紧凑、稳定的动作区，不能要求 Inspector 长期贴住屏幕边缘才能点击。
- 属性 C# 示例弹窗应靠近触发控件或指针，并夹取在可用显示区域；禁止固定出现在桌面左上角，也不得使用 `Screen.currentResolution` 推导 Editor 坐标。
- 缺失可选属性时应安全跳过或给出针对性诊断；不得因某一 Shader 没有另一类别的属性而让共享 ShaderGUI 整体异常。

## 6. 完成声明门禁

- 源码存在、文本搜索、`.csproj` 编译和历史 Console 记录只能证明各自范围，不能替代当前工作树的 Shader 导入与视觉结果。
- 宣称效果正确前，至少需要在 Unity 中逐类验证：Shader 无导入错误、父子开关、时间与 UV 组合、质量档、预设 Undo/多选、PropertyBlock 或 UI 材质实例、窄面板、高 DPI、典型透明/深度场景以及关闭功能后的成本变化。
- 未取得上述证据时，状态必须保持 `Implemented-Unverified`，并明确列出本轮没有重新验证的项目；不得用“接近/超越某商业 Shader”替代功能、画面与性能证据。
