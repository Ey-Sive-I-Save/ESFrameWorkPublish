# ES 编辑器 PropertyTree 多目标与迁移边界：保真 Knowledge
`KnowledgeId`: `es.aiwarning.editor.propertytree-multitarget-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Editor/PropertyTree 实现  
`RouteKeys`: `aiwarnings`, `editor`, `propertytree`, `serialize-reference`, `multitarget`, `undo`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `0c12b4baf0f1096d104baa89c091f6766676372d472e37a4ed7919414d7332f2`  
`SourceSetHash`: `0c12b4baf0f1096d104baa89c091f6766676372d472e37a4ed7919414d7332f2`  
`EntryBodyHash`: `e29ffd00cfcf10f9f00ce358668d08212010ea50a43248a6412d735cc800fe07`  
`StaleWhen`: PropertyTree、Drawer、类型目录、序列化迁移或任一 SourceRef 变化。

## 迁移范围
Warning 保留 Unity 序列化/`SerializeReference` 权威、窗口实例隔离、多目标安全、Undo/Dirty 和迁移证据边界；本条目承载 Drawer、Section Processor、SessionState、重建释放和测试矩阵细节。Knowledge 不成为业务数据真源。

## 权威与实例生命周期
Drawer、PropertyTree、VisualElement、类型目录和 SessionState 都是编辑投影；禁止缓存反写资产或跨窗口复用 `OdinEditor`、PropertyTree、SerializedObject 与桥接对象。重选、销毁、解绑和 Domain Reload 时释放旧对象，从当前资产与稳定身份重建。Section 语义由声明和 `ESEditorSectionAttributeProcessor` 解析，不能依赖绘制顺序或手工 GroupID；SessionState 只保存当前会话导航。

## 多态与多目标
`ESPolymorphicReferenceDrawer` 只能从 `ESTypeCatalog` 选择合法可序列化类型；缺失 `SerializeReference` 类型必须显示原始信息并由用户明确恢复或替代，禁止自动置空/静默丢弃。多目标仅允许相同 property path、兼容基类和可确认集合；不一致先显示 mixed，明确选择类型后才统一覆盖，并沿现有批量赋值与 Undo 路径。

## 写入、迁移与展示
资产写入前记录 Unity/Odin Undo，批量写入使用明确 Undo 组；`SerializedProperty` 写入后 `ApplyModifiedProperties()` 并 Dirty，直接 Odin 写入也同等处理。数组/嵌套依赖当前稳定 property path，禁止缓存索引对象跨重排使用。迁移必须显式、可恢复、可审计，Drawer 不得在 OnGUI 偷渡迁移。`ESFieldRow`/`ESTypeCatalog` 只负责布局、候选和展示，不承担业务验证、Bake、加载或运行时所有权。

## 验收矩阵
必须覆盖单/多目标一致与 mixed、类型切换、清空、Undo/Redo、缺失类型、数组重排、深层嵌套、重选、关闭重开、Domain Reload、旧数据迁移及多个窗口并看同一资产。现有源码仅具备部分处理；Unity Test Runner 完整回归仍未实测，不能把 Drawer 可显示等同于迁移验收。

## 原文快照
迁移前原始文件为 44 行、4344 UTF-8 字节，原始 SHA-256 为 `4730985784a02bc0d8f5f5aa5b2752c9637c0cf5604d09c0db52b454793d95ce`。本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ES编辑器绘制与序列化套件_PropertyTree多目标与迁移边界_AI协作警告.md` (`63d3c0c60146e0f89ad75347907c9e30adc6dfe13f0767add81f4dff1449c8d1`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`f0ca3d0c19d765487f09725809baf388421eac13063b1315abf6f56902977909`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-editor-propertytree-multitarget-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ES编辑器绘制与序列化套件_PropertyTree多目标与迁移边界_AI协作警告.md`
