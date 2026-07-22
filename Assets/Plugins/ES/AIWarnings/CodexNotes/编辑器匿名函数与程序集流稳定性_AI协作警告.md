# 编辑器匿名函数与程序集流稳定性_AI协作警告

作者职责：Codex，负责编辑器预览底层、资产包预览工作流、玩家/生命体模型重构协作中的工程稳定性审计。

更新时间：2026-07-22，中国时间。

## 匿名函数使用边界

不要把“匿名函数”一刀切禁掉。真正危险的是长期事件或全局事件上的匿名订阅，因为无法精准退订，容易在 ReloadDomain、窗口重建、程序集流重复初始化时产生重复注册或持有旧对象。

### 必须避免

- `Selection.selectionChanged += () => ...`
- `Editor.finishedDefaultHeaderGUI += ed => ...`
- `EditorApplication.update += () => ...`
- `EditorApplication.playModeStateChanged += state => ...`
- `AssemblyReloadEvents.beforeAssemblyReload += () => ...`
- `EditorApplication.quitting += () => ...`
- 静态工具、全局面板、程序集流初始化器中捕获窗口实例、SO、GameObject、大列表的匿名委托。

这些场景必须改成命名方法，并在注册前先 `-=` 再 `+=`。

### 可以接受

- `GenericMenu.AddItem(..., () => ...)`：菜单是一次性对象，通常安全。
- UI Toolkit 局部元素的回调：如果元素生命周期跟窗口一起释放，且不挂全局静态事件，通常可接受。
- `EditorApplication.delayCall += () => ...`：只在短延迟、一帧后执行、且不捕获大对象时可接受；如果会重复排队，建议改成命名方法并先 `-=`.
- 进程输出事件：当前 `ESCmdAgentWindow` 关闭时会 Stop/Dispose 进程，短期可接受；后续若支持后台常驻，应改为 Process->Tab 映射和命名事件处理器。

## 本轮已修正点

- `ESGraphViewWindow`：
  - 窗口自己的 `Selection.selectionChanged` 改为命名方法，`OnDisable` 退订。
  - 全局 `ERS` 注册改为先 `-=` 再 `+=`。
- `ER_ESEditorInspectorUser`：
  - `Editor.finishedDefaultHeaderGUI` 从匿名函数改为命名静态方法。
- `SceneHierarchyExpansionState`：
  - 延迟恢复从匿名 `delayCall` 改为命名方法。
  - 注册全局事件前统一先退订。
- `ESMenuTreeWindowAB`：
  - `OnClose += () => SaveData` 改为命名方法，避免重复挂闭包。
- `EditorInitAndUpdater`：
  - 三个全局 update 初始化均改为先 `-=` 再 `+=`。
- `ESInputBindingDefineDrawer`：
  - 输入监听 `RebindingOperation` 增加 ReloadDomain/退出/PlayMode 切换清理入口。

## 程序集流优化边界

`ESAssemblyStream` 是项目编辑器初始化根链路，不能随手大改。优化方向应该先做“稳定性小修”，再考虑结构升级。

### 已修正

- `ValidEditorAssembiles.OrderBy(...)` 原来没有赋值，排序不生效；已改成赋值后的排序列表。
- `asm.GetTypes()` 已增加安全加载：遇到 `ReflectionTypeLoadException` 时保留能加载的类型，避免一个坏类型拖垮整条程序集流。

### 后续可优化，但不要本能硬改

- 用 `HashSet<string>` 加速程序集白名单判断，同时保留 List 顺序用于排序。
- 为每个 Assembly 缓存类型扫描结果，但必须以 DomainReload 为边界清空。
- 将字段/属性/方法扫描结果缓存为 `MemberInfo[]`，避免同一类型在多个 handler 阶段反复反射。
- 给 AssemblyStream 增加耗时分段日志：程序集筛选、GetTypes、注册器发现、Singleton/SubClass/Attribute 六类处理分别计时。
- 给普通 `EditorInvoker_Level*` 加可选去重标记，避免同一类型重复 InitInvoke。

## 后续 AI 判断标准

看到匿名函数时先问三件事：

1. 它是否挂在全局静态事件上？
2. 它是否捕获窗口、SO、GameObject、大列表、Process？
3. 它是否可能在程序集流、ReloadDomain、窗口重建中重复注册？

如果答案有一个是“是”，优先改成命名方法，并做到注册前先退订。

