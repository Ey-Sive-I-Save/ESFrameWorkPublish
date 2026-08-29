# AssemblyStream Editor 注册与禁止全量扫盘：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.assembly-stream-editor-registration-only.v1`  
`Authority`: `AIWarnings` 与当前 AssemblyStream/Editor 资源边界合同  
`RouteKeys`: `aiwarnings`, `p0`, `editor`, `assembly-stream`, `metadata-registration`, `no-full-scan`, `runtime-boundary`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `4964fb79325b460260e258dda2b565667a2906c6811098735ae71d811b95ff7c`  
`SourceSetHash`: `4964fb79325b460260e258dda2b565667a2906c6811098735ae71d811b95ff7c`  
`EntryBodyHash`: `0373cba2ac1fd5dac5a1e4f108b01349f5b69b3ab0299416e7cfe28ab5d9978a`  
`StaleWhen`: AssemblyStream 注册器、编辑器索引、资源扫描 API、Runtime 流或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留 Editor-only 定位、Runtime 流禁令、全量扫描禁令和两个根链路例外；本条目承载注册模式、受控扫描条件、资源系统边界和历史快照。Knowledge 不授予扫描、资产写入、Runtime 或发布权限。

## 注册职责与 Runtime 边界

AssemblyStream 只负责扫描指定程序集、发现 `ESAS_EditorRegister_AB` 派生注册器、按 `Order` 构建处理器、对类型/字段/属性/方法特性执行回调及支持 `EditorInvoker_*`。它不是 Player/IL2CPP 注册系统、资源管理器或运行时框架入口；禁止恢复 `RuntimeRegister_FOR_*`、`ESAS_RuntimeRegister_AB`、`RunTimePart`、`RuntimeInitializeOnLoadMethod`、运行时类型扫描和热加载注册。

注册器只收集 `Type/FieldInfo/PropertyInfo/MethodInfo` 元数据、写轻量注册表、建立菜单/窗口/字段规则/特性映射和可重复去重缓存。禁止在注册阶段项目级 `AssetDatabase.FindAssets`、递归扫 `Assets/`/`Packages/`/磁盘、批量 `LoadAssetAtPath`、加载大量 Prefab/Texture/Audio/AnimationClip/Material/Scene、创建/修改场景、写/保存资产、MarkSceneDirty、批量改 GUID/Path 或执行业务逻辑。

真正重操作必须延后到用户按钮、具体窗口、明确文件夹/类型/Library，使用缓存/增量、进度条或取消、Undo/回退、去重和异常保护。出现 `FindAssets`、`Directory.GetFiles/EnumerateFiles`、大批量 LoadAsset、遍历 Assets/所有 ScriptableObject/Prefab 或域重载自动重建时先审查；只有用户触发、范围明确、可取消/回退、去重保护和中文说明齐备时才允许受控扫描。

## 明确例外与资源边界

`Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/-SoEditorLoader.cs` 中 `SoEditorIniter : EditorInvoker_Level0` 可维护 ESSO 编辑器索引，但不得扩展到 Prefab/贴图/音频/场景大资源；`Assets/Plugins/ES/Editor/EditorTools/ESEditorToolBar/ESEditorToolBar.cs` 的 `CustomToolbarMenu` 可维护轻量入口和场景路径缓存，但不得静态加载场景、Prefab 或大资源。例外只针对明确文件与职责，新文件不继承。

Library/Book/Page、AssetTable、构建清单和热更新清单应由 Editor 面板、指定范围和构建流程生成；AssemblyStream 最多注册收集器类型、菜单入口、窗口入口和字段规则，GameManager 运行时读取已烘焙表，不由注册流自动全量生成。

## 原文快照与验收

迁移前台账快照：125 行、5374 字节，原始 SHA-256 `b25a7f0aa36852bfd4096033de5aca12e12cec730e0437873eeb673da68434df`。验收需检查 Runtime 流未恢复、注册副作用、扫描范围/取消/Undo/去重/异常保护和资源边界，并执行 UTF-8、U+FFFD/乱码、`git diff --check`；Editor 域重载行为本轮未执行。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md` (`1ff7130253a32b13220afde5099c89060c255f8181670065902fcdeb99a44478`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`bc069265c0a7951948922df8ed2e5a6b50ecc7f1be607a8031665665c02c0371`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-assembly-stream-editor-registration-only.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md`
