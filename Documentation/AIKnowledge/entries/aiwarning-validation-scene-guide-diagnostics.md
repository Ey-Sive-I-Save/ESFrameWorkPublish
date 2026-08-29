# 测试场景导视与诊断复用：保真 Knowledge
`KnowledgeId`: `es.aiwarning.validation.scene-guide-diagnostics.v1`  
`Authority`: `AIWarnings` 与 `ESSceneValidationGuide` 当前实现  
`RouteKeys`: `aiwarnings`, `validation`, `scene`, `guide`, `diagnostics`, `playmode`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `76e46bb219a724f9514fc39bcf249940684252c80c149f69304d0e359a9d2407`  
`SourceSetHash`: `76e46bb219a724f9514fc39bcf249940684252c80c149f69304d0e359a9d2407`  
`EntryBodyHash`: `db36864a68c3dc99097762dc59be6cbd6244d044bdfcbe71614f0147b10e294a`  
`StaleWhen`: `ESSceneValidationGuide`、场景接入、输入绑定或任一 SourceRef 变化。

## 迁移范围
Warning 保留测试/验收场景专用性、复用路由、Prefab 隔离、只读与证据边界；本条目承载配置方式、自动检查类型、性能呈现和当前样板状态。Knowledge 不授权把静态证据升级为 Unity 或运行时验收。

## 复用与配置
涉及提示、操作引导、验收路线、运行态面板、键位说明、失败定位或区域导视时，优先检查 `Assets/Scripts/ESLogic/Runtime/Developer/Diagnostics/ESSceneValidationGuide.cs` 与 `Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md`。测试场景已有 Guide 时扩展 `stages`、`checks` 或 `ConfigureForAuthoring(...)`，不要建立平行提示脚本；新场景只在根节点或 `Diagnostics` 子节点挂一个 Guide。

自动检查优先使用框架已有的 Framework、Input、LocalControl、MainView、Mounted、VehicleReady 与驾驶权类型；场景私有断言才使用 `External`，并由该 Guide 实例 `ReportCheck(...)` 上报。每阶段必须回答去哪里、做什么、预期结果、失败定位和真实 `ESInputActionId` 绑定。

## 禁止与性能
禁止一次性 `OnGUI`/`GUI.Label`、硬编码键位、`Handles.Label` 运行时说明、`Camera.main`、`FindObjectOfType`、全局单例和原始 `Input.GetKey*` 隐式依赖。Guide 只读 ES 运行态，不写输入、Cinemachine 或测试结果；人工项必须标记 `ManualObservation`。按 `refreshInterval`（默认 0.2 秒）轮询，只有结果/阶段变化或 `InvalidatePresentation()` 才重建文本；Landmark 标签复用，不能据此宣称零 GC。

## 当前样板与证据
`Assets/Scenes/Tests/ESPlayerControllerTest.unity` 已接入 Guide，但不等于功能链运行验收；当前工作树的 `ES_Stand` 缺失源文件，且 Unity 尚未重新生成 `ES_Logic` 工程收录新脚本。报告必须分别说明源码/场景配置、Unity 编译、Test Runner、PlayMode 和 Profiler 状态。

## 原文快照
迁移前原始文件为 41 行、3130 UTF-8 字节，原始 SHA-256 为 `ab0c4852c76d57c727405cc8a4da597bfeb38a77875ff0b5c23abb1df06b1e8e`。本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md` (`a55d464d511718c8e7f3024e75fbd14d34037ae9a6d9a35423ca0f61a6845e8e`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`48a925704aff2db370f23bcb4da82e2490ce82e093567854d44945058a5f0148`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-validation-scene-guide-diagnostics.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md`
