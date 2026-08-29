# ES Composite Shader URP 职责与材质检查器验收边界
Status: current
StableId: es.aiwarning.editor.composite-shader-urp-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, editor, shader, urp, material, inspector, variant
Applicability: ES Composite Shader、ShaderGUI、材质预设与运行时材质参数
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-editor-composite-shader-urp-boundary.md
StaleWhen: URP Shader、ShaderGUI、材质预设、Variant 或 SourceRef 变化。

- 仅承诺 URP；2D、3D Lit、3D VFX、UI 保持独立职责，不得混成万能 Shader 或声称 Built-in/HDRP 兼容。
- Renderer 实例参数优先 `MaterialPropertyBlock`；渲染状态和 UI 动态参数使用受生命周期管理的材质，禁止无条件克隆或每帧创建。
- Inspector 模式只改信息密度；父开关、时间/UV、质量档和 Variant 必须真正影响实现，预设支持选择性应用、Undo/Redo、多材质编辑。
- 禁止隐式全局/查找依赖、无界 Keyword；未有 Unity 逐类视觉、交互、性能和发布证据时保持 `Implemented-Unverified`。Knowledge：`es.aiwarning.editor.composite-shader-urp-boundary.v1`。
