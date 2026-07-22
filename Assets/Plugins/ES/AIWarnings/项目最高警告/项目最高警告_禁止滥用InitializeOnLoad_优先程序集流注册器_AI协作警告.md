# 项目最高警告：禁止滥用 InitializeOnLoad，优先程序集流注册器

最后核对：2026-07-21

职责：这是 ESFramework 给后续 AI 的项目最高警告。凡是编辑器初始化、域重载后自动执行、编译后自动注册的逻辑，默认优先使用 ES 的 AssemblyStream 程序集流注册器，不要随手写 Unity 原生 `[InitializeOnLoad]`、`[InitializeOnLoadMethod]` 或静态构造器挂 `EditorApplication.delayCall`。

## 最高原则

`[InitializeOnLoad]` 不是普通初始化工具，它会在 Unity 域重载、脚本编译、编辑器启动等阶段自动触发。随意使用会把局部工具逻辑塞进全项目 ReloadDomain 热路径，造成隐性卡顿、内存峰值、重复订阅、场景被误标脏、资源被误创建，以及难以追踪的初始化顺序问题。

ESFramework 已经有统一的程序集流注册机制。新增编辑器自动初始化能力时，优先走：

- `EditorInvoker_Level0 / Level1 / Level2 / Level50`
- `EditorRegister_FOR_Singleton<T>`
- `EditorRegister_FOR_ClassAttribute<TAttribute>`
- `EditorRegister_FOR_FieldAttribute<TAttribute>`
- `EditorRegister_FOR_PropertyAttribute<TAttribute>`
- `EditorRegister_FOR_MethodAttribute<TAttribute>`

只有 AssemblyStream 自身作为根引导入口，或 Unity/第三方插件必须要求的极少数全局桥接，才允许使用 Unity 原生 InitializeOnLoad。

## 禁止误操作

- 不要给普通工具、示例安装器、视频案例、窗口辅助类随手加 `[InitializeOnLoad]`。
- 不要在静态构造器里写 `EditorApplication.delayCall += SomeInstallOrScan` 作为自动入口。
- 不要在域重载入口里创建场景对象、扫描全项目资产、刷新窗口、打开窗口、写 EditorPrefs、MarkSceneDirty，除非有明确的全局职责和去重保护。
- 不要用 `InitializeOnLoadMethod` 注册一次性调试日志、版本日志或演示安装逻辑。
- 不要在自动入口里订阅 `EditorApplication.update` 后缺少对称退订、状态门控和重复订阅保护。
- 不要把 RuntimeWatch、SimpleTools、案例脚本、临时测试脚本接入 Unity 原生域重载入口。

## 正确做法

如果要在编辑器域重载后自动执行一次初始化：

```csharp
public class SomeEditorInitializer : EditorInvoker_Level2
{
    public override void InitInvoke()
    {
        // 轻量、可重复、无副作用的初始化。
    }
}
```

如果要注册带特性的字段、属性、方法：

```csharp
public class ER_SomeAttribute : EditorRegister_FOR_FieldAttribute<SomeAttribute>
{
    public override void Handle(SomeAttribute attribute, FieldInfo fieldInfo)
    {
        // 只记录元数据，不在这里做重扫描、实例查找或场景写入。
    }
}
```

自动入口必须满足：

- 可重复执行，不产生重复对象、重复订阅、重复缓存。
- 默认轻量，不做资产全量扫描、场景实例扫描、反射大展开。
- 不无条件修改场景、资产、EditorPrefs。
- 有明确职责命名和中文说明。
- 能解释为什么不能用 AssemblyStream；解释不了就不要用 Unity 原生入口。

## 当前扫描结果

以下是 2026-07-21 扫描并整改后的结果。后续 AI 不要按旧记忆恢复已经迁移掉的 Unity 原生入口。

### 应保留但严禁复制的根入口

- `Assets/Plugins/ES/0_Stand/Stand_Tools/AssemblyStream/-ESAssemblyStream.cs`
  - `EditorPart.EditorInitLoad()` 使用 `[InitializeOnLoadMethod]`。
  - 这是 AssemblyStream 根引导，属于特例。不要拿它当普通工具写法模板。

### 当前允许保留的桥接入口

- `Assets/Plugins/ES/Editor/Out/ToolbarExtender.cs`
  - 使用 `[InitializeOnLoad]` 作为 Unity Toolbar 注入桥接。
  - 这是 ToolbarExtender 根桥接，不要在这里塞业务菜单；业务菜单应通过 AssemblyStream 注册到 `ToolbarExtender.LeftToolbarGUI / RightToolbarGUI`。

### 已整改：迁移到 AssemblyStream 或取消自动运行

- `Assets/Plugins/ES/Obsolete/EditorTesting/testOberlay.cs`
  - 已取消 `[InitializeOnLoad]` 和静态 `delayCall`。
  - 现在只通过显式 Obsolete 菜单触发，不再域重载自动常驻 update。

- `Assets/Plugins/ES/Obsolete/AIPreview/Editor/EditorToolCollection.cs`
  - 已取消 SceneView 调试绘制的 `[InitializeOnLoad]`。
  - 现在通过显式 Obsolete 菜单开关注册/反注册 `SceneView.duringSceneGui`。

- `Assets/Plugins/ES/Editor/EditorTools/SceneHierarchyExpansionState.cs`
  - 已迁移到 `EditorInvoker_Level2`。
  - 保留必要的 scene/reload/playmode/quitting 回调，并补充重复订阅保护。

- `Assets/Plugins/ES/Editor/EditorTools/ESEditorHandle/ESEditorHandle.cs`
  - 已迁移到 `EditorInvoker_Level0`。
  - `AddSimpleHandleTask` / `AddRunningHandleTask` 会兜底注册 update，避免绕过程序集流时任务不推进。

- `Assets/Plugins/ES/Editor/EditorTools/ESEditorToolBar/ESEditorToolBar.cs`
  - 业务工具栏菜单已迁移到 `EditorInvoker_Level2`。
  - 注册前先从 ToolbarExtender 列表移除旧委托，避免域重载后重复添加。

- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SODataInfoWindow/ESSODataInfoWindow.cs`
  - `ESSODataWindowHelper` 已迁移到 `EditorInvoker_Level2`。
  - 只注册 `Selection.selectionChanged`，并保留重复订阅保护。

- `Assets/Plugins/ES/Editor/Installer/ESInstaller.cs`
  - 启动检查已迁移到 `EditorInvoker_Level2`。
  - 仍使用 `delayCall` 延迟检查，但不再用 Unity 原生初始化入口，并补充 `-=` 去重。

- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs`
  - 预览资源生命周期注册已迁移到 `EditorInvoker_Level2`。
  - 删除域重载版本日志，只保留 reload/quitting 资源释放注册。

- `Assets/Scripts/ESLogic/Editor/Preview/ESEditorPreviewResourceScope.cs`
  - 全局预览清理注册已迁移到 `EditorInvoker_Level2`。
  - 保留 playmode/reload/quitting 清理回调。

## delayCall / update 补充规则

`EditorApplication.delayCall` 和 `EditorApplication.update` 本身不是禁用 API，但不能作为绕过 AssemblyStream 的全局初始化入口。

允许场景：

- 用户点击按钮后延迟执行 UI 刷新。
- 窗口打开期间临时 update，窗口关闭或任务结束立即退订。
- 预览播放、拖拽监听、异步包管理等有明确生命周期的任务。

禁止场景：

- 静态构造器里无条件 delayCall。
- 域重载后无条件创建对象、扫描资产、打开窗口。
- update 常驻但没有运行条件、退订条件、异常保护。

## 给后续 AI 的结论

ESFramework 的编辑器自动初始化统一口径是：

根引导可以用 Unity InitializeOnLoad，普通业务和工具必须走 AssemblyStream 注册器。

能用 `EditorInvoker_*` 或 `EditorRegister_FOR_*` 表达的初始化，不要写 `[InitializeOnLoad]`。

看到 InitializeOnLoad 先停手审查，不要照抄。
