# ES 编辑器匿名事件与程序集流稳定性：保真 Knowledge

`KnowledgeId`: `es.aiwarning.editor.anonymous-event-assembly-stream-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Editor/AssemblyStream 实现  
`RouteKeys`: `aiwarnings`, `editor`, `anonymous-function`, `event`, `assembly-stream`, `reload-domain`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `25624c3f87fda22acb212731ce219d1f967eefe39cb02207d69e0ef88eb0f482`  
`SourceSetHash`: `25624c3f87fda22acb212731ce219d1f967eefe39cb02207d69e0ef88eb0f482`  
`EntryBodyHash`: `23bc4bdeaf8f01cf5dd31e4661f8977367ddfe92b05a67b7e433f45dd558524c`  
`StaleWhen`: Editor 事件注册、窗口清理、ReloadDomain 或程序集扫描实现变化。

## 迁移范围

Warning 保留全局事件退订、捕获对象、ReloadDomain/窗口重建和程序集流稳定性边界；本条目保存例外、已修正点、优化方向和原文快照。

## 匿名函数边界

危险场景包括 `Selection.selectionChanged`、`Editor.finishedDefaultHeaderGUI`、`EditorApplication.update/playModeStateChanged/quitting`、`AssemblyReloadEvents.beforeAssemblyReload` 等长期事件的匿名订阅，以及静态工具、全局面板、程序集流初始化器捕获窗口、SO、GameObject、大列表或 Process。必须使用命名方法，注册前先退订。`GenericMenu.AddItem` 一次性回调、随窗口释放的局部 UI 回调通常可接受；`delayCall` 仅限短延迟、不捕获大对象、不重复排队；`ESCmdAgentWindow` 的进程输出在关闭时 Stop/Dispose，后台常驻需改为 Process→Tab 映射和命名处理器。

Stable Graph V2 窗口事件使用命名方法并在 OnDisable 退订；ERS 全局注册先 `-=` 再 `+=`。`ER_ESEditorInspectorUser`、`SceneHierarchyExpansionState`、`ESMenuTreeWindow<T>`、`ESOdinMenuTreeWindow<T>` 和 `EditorInitAndUpdater` 已按此修正；`ESInputBindingDefineDrawer` 的 RebindingOperation 覆盖 ReloadDomain、退出和 PlayMode 清理。已删除的 Legacy `ESMenuTreeWindowAB` 不是类型权威。

## ESAssemblyStream 边界

`ESAssemblyStream` 是编辑器初始化根链路，优化先稳定性小修。`ValidEditorAssembiles.OrderBy` 必须使用赋值后的排序列表；`asm.GetTypes()` 遇 `ReflectionTypeLoadException` 时保留可加载类型。可选优化包括 HashSet 白名单、DomainReload 清空的类型/成员缓存、筛选/GetTypes/注册器等阶段耗时日志和 EditorInvoker 去重标记，但不得本能硬改结构或把缓存越过 DomainReload。

判断匿名函数先问：是否全局静态事件？是否捕获窗口/SO/GameObject/大列表/Process？是否可能在程序集流、ReloadDomain、窗口重建中重复注册？任一为是，优先改命名方法并成对退订。

## 原文快照

迁移前原始文件为 73 行、4072 UTF-8 字节，原始 SHA-256 为 `f90c6fa4b8cb1146bff3fcd73d47b7980a37eb50f300e3c46de3c187fb487b0c`。本轮未运行 Unity/Runtime。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/程序集与稳定性（AssemblyStability）/编辑器匿名函数与程序集流稳定性_AI协作警告.md` (`551f6b031775e20b222fe1fd50373a87d6d716ee43aa8494845a49ab5fe05c0a`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`624998f43f239ddd6a7d7f0d75132c24c67eca0c4bc1451eecb062824be0abcd`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-editor-anonymous-event-assembly-stream-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/程序集与稳定性（AssemblyStability）/编辑器匿名函数与程序集流稳定性_AI协作警告.md`
