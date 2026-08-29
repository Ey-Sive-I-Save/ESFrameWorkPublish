# 项目最高警告：AssemblyStream 仅作 Editor 特性注册，禁止全量扫盘

Status: current
StableId: es.aiwarning.p0.assembly-stream-editor-registration-only.v1
Authority: AIWarnings（长期 P0 约束）；详细扫描边界与例外见 Knowledge
RouteKeys: aiwarnings, p0, editor, assembly-stream, metadata-registration, no-full-scan, runtime-boundary
Applicability: Editor AssemblyStream、特性注册、域重载、编辑器索引和资源收集入口
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-assembly-stream-editor-registration-only.md
StaleWhen: AssemblyStream 注册器、编辑器索引、资源扫描 API、Runtime 流或任一 SourceRef 哈希变化。

## P0 长期约束

- AssemblyStream 仅是 Editor 元数据发现与解耦注册流：扫描指定程序集、发现 `ESAS_EditorRegister_AB`、按 Order 注册类型/字段/属性/方法特性和 `EditorInvoker_*` 节点。Runtime 流已移除，禁止恢复 `RuntimeRegister_FOR_*`、`ESAS_RuntimeRegister_AB`、`RunTimePart`、`RuntimeInitializeOnLoadMethod`、运行时类型扫描或热加载注册。
- 禁止在注册阶段使用 `AssetDatabase.FindAssets` 项目全量扫描、递归扫 `Assets/`/`Packages/`/磁盘、大量加载 Prefab/Texture/Audio/AnimationClip/Material/Scene、创建/修改场景、写/保存资产、MarkSceneDirty、批量改 GUID/Path 或塞入业务逻辑。
- 仅允许收集元数据、写轻量注册表、建立菜单/窗口/渲染规则映射和可重复去重缓存。重操作延后到用户按钮、具体窗口、明确文件夹/类型/Library，并提供缓存/增量、进度或取消、Undo/回退、去重和异常保护。
- `SoEditorIniter`（`-SoEditorLoader.cs`）的 ESSO 索引和 `CustomToolbarMenu`（`ESEditorToolBar.cs`）的轻量入口/场景路径缓存是明确根链路例外；只能维持各自职责，不得扩展为大资源加载，新增文件不继承例外。
- 发现 `FindAssets`、`Directory.GetFiles/EnumerateFiles`、大量 `LoadAssetAtPath`、遍历 Assets/所有 SO/Prefab 或域重载自动重建时必须停手审查。只有用户触发、范围明确、可取消/回退、去重保护和中文说明全部具备时才允许受控扫描。
- 资源 Library/Book/Page、AssetTable、构建/热更新清单由明确 Editor 面板、指定范围和构建流程生成；AssemblyStream 最多注册收集器、菜单、窗口和字段规则，GameManager 运行时只读已烘焙表。

## Knowledge 导航

完整注册器模式、API 审查信号、例外文件、受控扫描条件和资源系统边界见 `es.aiwarning.p0.assembly-stream-editor-registration-only.v1`。本 Warning 不授予扫描、资产写入、Runtime 或发布权限。
