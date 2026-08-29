# 测试场景导视与诊断复用
Status: current
StableId: es.aiwarning.validation.scene-guide-diagnostics.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, validation, scene, guide, diagnostics, playmode
Applicability: 测试/验收场景提示、导视、失败定位与运行态诊断面板
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-validation-scene-guide-diagnostics.md
StaleWhen: ESSceneValidationGuide、场景接入、输入绑定或 SourceRef 变化。

- 优先复用 `ESSceneValidationGuide` 与既有标准；已有 Guide 扩展 stages/checks，不得建立平行提示脚本；Guide 只能挂测试场景根节点或 Diagnostics 子节点。
- 优先使用既有自动检查和真实 `ESInputActionId`；场景私有断言才用 `External`/`ReportCheck(...)`。Guide 只读运行态，人工项标记 `ManualObservation`。
- 禁止一次性 OnGUI、硬编码键位、Camera.main、FindObjectOfType、全局单例和原始 Input API 隐式依赖；结果/阶段不变时不得重建面板，不能宣称零 GC。
- 必须区分源码/场景配置、Unity 编译、Test Runner、PlayMode 与 Profiler 证据。Knowledge：`es.aiwarning.validation.scene-guide-diagnostics.v1`。
