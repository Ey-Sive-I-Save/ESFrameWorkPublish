# ES Composite Shader URP 与材质检查器边界：保真 Knowledge
`KnowledgeId`: `es.aiwarning.editor.composite-shader-urp-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Shader/ShaderGUI 实现  
`RouteKeys`: `aiwarnings`, `editor`, `shader`, `urp`, `material`, `inspector`, `variant`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `11d3389ddeb771ab77e198036165e096f20edb90a4d9c125cbdf8b60149faac6`  
`SourceSetHash`: `11d3389ddeb771ab77e198036165e096f20edb90a4d9c125cbdf8b60149faac6`  
`EntryBodyHash`: `0fbd67e661abe407f232e1b8f6c54bc26e7f6681b90ccef6c0b9642210e99dc8`  
`StaleWhen`: URP Shader、ShaderGUI、材质预设、Variant 或任一 SourceRef 变化。

## 迁移范围
Warning 保留 URP-only、四类 Shader 职责、材质参数写入、Inspector/预设安全和验证门禁；本条目承载参数、模式、Variant、编辑器可用性与逐类验收细节。Knowledge 不替代当前 Shader 源码或 Unity 证据。

## 管线与参数
`ES2DCompositeURP`、`ES3DLitCompositeURP`、`ES3DVFXCompositeURP`、`ESUICompositeURP` 必须保持独立职责，不能声称 Built-in/HDRP 兼容或用万能 Shader 混合合同。Renderer 实例数值/颜色/向量/纹理优先用 `MaterialPropertyBlock`；Keyword、Render Queue、Pass、Blend、Cull、ZWrite 需要受生命周期管理的独立 Material。UI `Graphic` 使用缓存材质实例，目标/原材质/Shader 变化时重建释放，禁止每帧创建。示例必须说明外部参数和调用位置。

## Inspector、预设与生效关系
标准/进阶/高级只控制信息密度和显隐，不能写材质、Keyword 或渲染结果。真实属性须有语义分组、Bool 开关和主题可读状态；预设只覆盖自身属性并支持选择性应用、Undo/Redo 和多材质编辑。父开关必须真正门控计算/采样/输出；时间、UV、缩放中心和速度组合顺序要明确；质量档必须体现计算/采样差异。

## Variant 与编辑器可用性
Keyword 只用于确实改变 Pass/平台路径且节省成本的能力；连续参数和编辑器显隐不得无限扩张 Variant。Inspector 要覆盖窄宽、高 DPI、深浅主题、多选和缺失属性；示例弹窗应夹取在可用区域，不能固定左上角或用 `Screen.currentResolution` 推导 Editor 坐标。缺失可选属性应安全跳过并给出针对性诊断。

## 验收门禁
源码存在、文本搜索、`.csproj` 编译或历史 Console 只能证明局部范围。效果声明至少需要 Unity 中逐类验证导入错误、开关、时间/UV、质量、预设 Undo/多选、PropertyBlock/UI 材质、窄面板、高 DPI、透明/深度和关闭功能后的成本变化。未取得证据时保持 `Implemented-Unverified`，不得以商业 Shader 类比替代功能、画面和性能证据。

## 原文快照
迁移前原始文件为 44 行、4739 UTF-8 字节，原始 SHA-256 为 `743bd3b3b031ed527bbc6d76f04111bdf985cf423a2a092458385602b498863d`。本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md` (`ee28160bdcf928982f5a743ee9e670c529942f6eb819296bec6eecef2668d004`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`f6c16e6355ddc307515542bf72d4d2fdedb3bab9e04fb89732ac5507bac093eb`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-editor-composite-shader-urp-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md`
