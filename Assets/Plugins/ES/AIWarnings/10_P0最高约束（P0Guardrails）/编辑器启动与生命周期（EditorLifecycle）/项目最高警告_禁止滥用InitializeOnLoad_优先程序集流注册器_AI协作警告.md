# 项目最高警告：禁止滥用 InitializeOnLoad，优先程序集流注册器

Status: current
StableId: es.aiwarning.p0.editor-assembly-stream-initialization.v1
Authority: AIWarnings（长期 P0 约束）；详细生命周期与证据见 Knowledge
RouteKeys: aiwarnings, p0, editor, initialization, assembly-stream, domain-reload, delay-call
Applicability: 编辑器初始化、域重载、编译后注册、EditorWindow、ESSO 预加载和自动订阅
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-editor-assembly-stream-initialization.md
StaleWhen: AssemblyStream、EditorInvoker/Register、域重载、ESSO 预加载或任一 SourceRef 哈希变化。

## P0 长期约束

- 普通编辑器工具、安装器、窗口辅助类、RuntimeWatch、示例和临时测试不得随手使用 `[InitializeOnLoad]`、`[InitializeOnLoadMethod]`、静态构造器或静态 `delayCall`；默认走 `EditorInvoker_Level0/1/2/50` 与 `EditorRegister_FOR_*` AssemblyStream 注册器。
- Unity 原生入口仅限 AssemblyStream 根引导或 Unity/第三方强制的极少数全局桥接；必须说明为何不能走 AssemblyStream。`Internal_`、菜单或 Skill 不会扩大该例外。
- 自动入口必须轻量、可重复、去重且无不受控副作用：禁止全项目资产/场景扫描、创建对象、打开/刷新窗口、写 EditorPrefs、MarkSceneDirty；`update`/订阅必须有状态门控、异常保护和对称退订。
- `delayCall`/`update` 仅用于用户动作后的 UI 刷新、窗口/任务生命周期、预览/拖拽/异步包管理等有明确开始与结束的场景，不得成为域重载后的常驻全局入口。
- `[ESSOEditorPreLoad]` 只能用于启动即需且有收益的明确类型：`ESSceneGlobalData`、`ESGlobalProjectAssetGuideData`、`ESGlobalEditorLocation`、`ESGlobalEditorDefaultConfi`；普通 GameCore、资源库、示例和诊断 SO 不得因方便预加载。
- 报告必须区分程序集流/类型登记与 Unity 资产反序列化耗时；历史样本约 45 GUID、86 ESSO、376ms，其中 `LoadAllAssetsAtPath` 约 362ms，不得将其写成注册器耗时。

## Knowledge 导航

完整注册器类型、正确模板、根入口特例、ESSO 预加载门禁、delayCall/update 生命周期与历史测量快照见 `es.aiwarning.p0.editor-assembly-stream-initialization.v1`。本 Warning 不授予 Unity 执行或资产修改权限。
