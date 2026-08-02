# 项目最高警告：禁止滥用 InitializeOnLoad，优先程序集流注册器

最后核对：2026-07-22

职责：这是 ESFramework 给后续 AI 的项目最高警告。凡是编辑器初始化、域重载后自动执行、编译后自动注册的逻辑，默认优先使用 ES 的 AssemblyStream 程序集流注册器，不要随手写 Unity 原生 `[InitializeOnLoad]`、`[InitializeOnLoadMethod]` 或静态构造器挂 `EditorApplication.delayCall`。

## 最高原则

`[InitializeOnLoad]` 不是普通初始化工具。它会在 Unity 启动、脚本编译、ReloadDomain 等阶段自动触发。随手使用会把局部工具塞进全项目热路径，造成隐性卡顿、内存峰值、重复订阅、场景误标脏、资源误创建、初始化顺序不可控。

ESFramework 已经有统一的编辑器程序集流注册机制。新增编辑器自动初始化能力时，优先走：

- `EditorInvoker_Level0 / Level1 / Level2 / Level50`
- `EditorRegister_FOR_Singleton<T>`
- `EditorRegister_FOR_AsSubclass<T>`
- `EditorRegister_FOR_ClassAttribute<TAttribute>`
- `EditorRegister_FOR_FieldAttribute<TAttribute>`
- `EditorRegister_FOR_PropertyAttribute<TAttribute>`
- `EditorRegister_FOR_MethodAttribute<TAttribute>`

只有 AssemblyStream 自身作为根引导入口，或 Unity/第三方插件强制要求的极少数全局桥接，才允许使用 Unity 原生 InitializeOnLoad。

## 禁止误操作

- 不要给普通工具、示例安装器、窗口辅助类随手加 `[InitializeOnLoad]`。
- 不要在静态构造器里写 `EditorApplication.delayCall += SomeInstallOrScan` 作为自动入口。
- 不要在域重载入口里创建场景对象、扫描全项目资产、刷新窗口、打开窗口、写 EditorPrefs、MarkSceneDirty，除非有明确的全局职责和去重保护。
- 不要用 `InitializeOnLoadMethod` 注册一次性调试日志、版本日志或演示安装逻辑。
- 不要在自动入口里订阅 `EditorApplication.update` 后缺少对称退订、状态门控和重复订阅保护。
- 不要把 RuntimeWatch、SimpleTools、示例脚本、临时测试脚本接入 Unity 原生域重载入口。

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
- 默认轻量，不做资源全量扫描、场景实例扫描、反射大展开。
- 不无条件修改场景、资产、EditorPrefs。
- 有明确职责命名和中文说明。
- 能解释为什么不能用 AssemblyStream；解释不了就不要用 Unity 原生入口。

## 保留根入口

- `Assets/Plugins/ES/0_Stand/Stand_Tools/AssemblyStream/-ESAssemblyStream.cs`
  - 这是 AssemblyStream 根引导，属于特例。
  - 不要拿它当普通工具写法模板。

- `Assets/Plugins/ES/Editor/Out/ToolbarExtender.cs`
  - 这是 Unity Toolbar 注入桥接，属于特例。
  - 不要在这里塞业务菜单；业务菜单应通过 AssemblyStream 注册到工具栏列表。

## delayCall / update 补充规则

`EditorApplication.delayCall` 和 `EditorApplication.update` 本身不是禁用 API，但不能作为绕过 AssemblyStream 的全局初始化入口。

允许场景：

- 用户点击按钮后延迟执行 UI 刷新。
- 窗口打开期间临时 update，窗口关闭或任务结束立即退订。
- 预览播放、拖拽监听、异步包管理等有明确生命周期的任务。

禁止场景：

- 静态构造器里无条件 delayCall。
- 域重载后无条件创建对象、扫描资源、打开窗口。
- update 常驻但没有运行条件、退订条件、异常保护。

## ESSO 编辑器预加载边界

`[ESSOEditorPreLoad]` 不是“所有全局 SO 都预加载”的快捷开关。它先由程序集流在 Level0 前登记，再由 `SoEditorIniter` 消费；只有编辑器启动后立刻需要且能证明收益的 ESSO 才能标记。

当前明确允许的预加载类型只有：

```text
ESSceneGlobalData
ESGlobalProjectAssetGuideData
ESGlobalEditorLocation
ESGlobalEditorDefaultConfi
```

新增该标记前必须说明：为什么不能按需加载、预期资产量、是否会引入全项目扫描，以及如何验证域重载重复执行安全。普通 GameCore、资源库、示例、诊断数据和玩法 SO 不得因“方便”进入预加载集合。

性能报告必须分开解释“程序集流/类型登记”与“Unity 资产反序列化”。一次实测记录为：45 个 GUID、45 条路径、86 个 ESSO 实例、总计约 376ms，其中 `AssetDatabase.LoadAllAssetsAtPath` 约 362ms。该数据表明主要耗时在 SO 资产加载，不能误写为 SoEditorIniter 的程序集注册耗时；后续报告必须保留同类分项。

## 给后续 AI 的结论

ESFramework 的编辑器自动初始化统一路径是：

根引导可以用 Unity InitializeOnLoad，普通业务和工具必须走 AssemblyStream 注册器。

能用 `EditorInvoker_*` 或 `EditorRegister_FOR_*` 表达的初始化，不要写 `[InitializeOnLoad]`。

看到 InitializeOnLoad 先停手审查，不要照抄。
