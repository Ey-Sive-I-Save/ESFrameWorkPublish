# 编辑器匿名函数与程序集流稳定性_AI协作警告

Status: current
StableId: es.aiwarning.editor.anonymous-event-assembly-stream-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, editor, anonymous-function, event, assembly-stream, reload-domain
Applicability: Editor 全局事件、窗口生命周期、Stable Graph V2 与 ESAssemblyStream
Owner: ESFramework EditorTooling 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-editor-anonymous-event-assembly-stream-boundary.md
StaleWhen: Editor 事件注册、窗口清理、ReloadDomain 或程序集扫描实现变化。

## 长期约束

- 不一刀切禁用匿名函数；全局/静态事件、程序集流初始化和长期面板中，若捕获窗口、SO、GameObject、大列表或 Process，必须改命名方法，并在注册前先 `-=` 再 `+=`，覆盖窗口重建、ReloadDomain、退出和 PlayMode 清理。
- 一次性 `GenericMenu`、随窗口释放的局部 UI 回调和短延迟 `delayCall` 可接受，但不得重复排队或捕获大对象；进程回调必须随窗口关闭 Stop/Dispose。
- `ESAssemblyStream` 只做稳定性小修；类型扫描异常需保留可加载类型，排序必须使用赋值结果。缓存以 DomainReload 为边界清空，不得把静态编译/扫描结果当运行时证据。
- Legacy Graph 已废止，Stable Graph V2 事件仍须命名方法、OnDisable 退订、ERS 先退订再注册。遇到全局匿名订阅，先判断静态事件、捕获对象和重复注册风险。

## Knowledge 导航

完整已修正点、可接受例外、程序集流优化边界、判断标准和原文快照见 `es.aiwarning.editor.anonymous-event-assembly-stream-boundary.v1`。
