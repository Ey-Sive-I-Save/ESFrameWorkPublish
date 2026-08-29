# Contextitecture 上下文系统：所有权、生命周期与类型边界 AI 协作警告

Status: current
StableId: es.aiwarning.arch.context-ownership-lifecycle-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, architecture, context, ownership, lifecycle, type-boundary
Applicability: Runtime/Context 的 ContextPool、Context Value、Link 与宿主生命周期
Owner: ESFramework Contextitecture 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-architecture-context-ownership-lifecycle-boundary.md
StaleWhen: ContextPool、值池、Link 通道、清理或跨线程约束变化。

## 长期约束

- Context 只承载局部执行参数/临时上下文，不替代 Tag、属性、Permit、资源租期、存档或网络语义；一个 Pool 必须有唯一宿主，结束/取消/失败/回池/销毁时调用 `ClearAllRuntimeValues()`。
- `persistent` 仅表示 `ClearNonPersistentRuntimeValues()` 时保留，不承诺跨 Entity、Item、Scene、存档、网络或 Pool 租期持久化。不得把可变值引用交给其他宿主或在归还后继续读写。
- 普通运行时使用 Copy 路径；`Same` 只限框架控制的非池化初始化原型。不得绕过 Pool 把租借值塞入字典、列表或静态字段。
- 值 Key 选定类型后不得用另一类型冒充事实；Object/UnityObject 不代表资源所有权，DynamicTag 不代表正式 Tag。Link 是 Pool 内通知，不是全局总线/可靠队列；订阅在宿主结束前解除。
- Context 仅允许 Unity 主线程；跨线程回写必须回到受控主线程并确认宿主有效。池化、首次扩容、订阅、异常和字符串查询不构成全生命周期 0 GC 证据。
- 未完成跨域引用、取消回调、域重载、长时间池化、存档/网络的 Unity 证据前，不得宣称这些场景已验收。

## Knowledge 导航

完整实现事实、值类型语义、事件边界、扩展要求、系统分工、验收缺口和原文快照见 `es.aiwarning.arch.context-ownership-lifecycle-boundary.v1`。
