# 项目最高警告：AssemblyStream 只做 Editor 特性注册解耦，禁止全量扫盘

最后核对：2026-07-22

职责：这是 ESFramework 给后续 AI 的项目最高警告。AssemblyStream 是编辑器程序集注册流，不是运行时框架入口，不是资源管理器，不是全项目扫盘器。

## 当前结论

AssemblyStream 现在只保留 Editor 侧能力：

- 扫描指定程序集。
- 发现 `ESAS_EditorRegister_AB` 派生注册器。
- 按 `Order` 构建注册处理器。
- 对类型、字段、属性、方法上的特性执行注册回调。
- 支持 `EditorInvoker_*` 这类编辑器初始化节点。

Runtime 程序集流已经整体砍掉。不要恢复 `RuntimeRegister_FOR_*`、`ESAS_RuntimeRegister_AB`、`RunTimePart`、`RuntimeInitializeOnLoadMethod`、运行时类型扫描、运行时热加载注册。

## 绝对禁止

- 禁止把 AssemblyStream 当 Player/IL2CPP 运行时注册系统。
- 禁止在 AssemblyStream 注册阶段做 `AssetDatabase.FindAssets` 全项目扫描。
- 禁止在 AssemblyStream 注册阶段递归扫 `Assets/`、`Packages/`、磁盘目录或超大资源文件夹。
- 禁止在注册器里加载大量 Prefab、Texture、Audio、AnimationClip、Material、Scene。
- 禁止在注册器里创建或修改场景对象。
- 禁止在注册器里写资产、保存资产、MarkSceneDirty、批量改 GUID/Path。
- 禁止把业务逻辑写进注册回调，让域重载变成业务运行入口。

## 已确认核心底层例外

以下位置已经人工确认，属于 ES 编辑器底层能力的根链路。后续 AI 不要把它们当成普通全量扫盘风险反复要求删除，但也不要把它们作为普通工具模板复制。

- `Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/-SoEditorLoader.cs`
  - `SoEditorIniter : EditorInvoker_Level0`
  - 当前会在编辑器程序集流阶段查找并初始化 `ESSO` 资产索引。
  - 定位：核心 SO 编辑器索引底层。可保留。
  - 约束：只允许服务 ES 编辑器数据索引，不允许扩展成 Prefab/贴图/音频/场景等大资源全量加载。

- `Assets/Plugins/ES/Editor/EditorTools/ESEditorToolBar/ESEditorToolBar.cs`
  - `CustomToolbarMenu`
  - 当前会维护编辑器工具栏入口和场景快捷缓存。
  - 定位：核心编辑器工具栏底层。可保留。
  - 约束：只允许维护轻量编辑器入口和场景路径缓存，不允许在静态构造或 ReloadDomain 阶段加载场景内容、Prefab 内容或大资源对象。

例外只针对上述明确文件和明确职责。新增文件、新增扫描类型、新增自动入口不自动继承这个例外。

## 正确使用

AssemblyStream 的正确定位是“元数据发现 + 解耦注册”。

推荐模式：

```csharp
[AttributeUsage(AttributeTargets.Field)]
public sealed class SomeEditorFeatureAttribute : Attribute
{
}

public sealed class ER_SomeEditorFeature : EditorRegister_FOR_FieldAttribute<SomeEditorFeatureAttribute>
{
    public override void Handle(SomeEditorFeatureAttribute attribute, FieldInfo fieldInfo)
    {
        SomeRegistry.Register(fieldInfo);
    }
}
```

注册器只做这些事：

- 收集 `Type / FieldInfo / PropertyInfo / MethodInfo` 元数据。
- 写入轻量注册表。
- 建立工具菜单、窗口入口、字段渲染规则、特性映射关系。
- 做可重复、可去重、无副作用的编辑器缓存初始化。

真正重操作必须延后：

- 用户点击按钮后执行。
- 打开具体窗口后按需执行。
- 针对明确文件夹、明确类型、明确 Library 执行。
- 使用缓存、增量、确认窗口和撤销支持。

## 全量扫盘审查规则

凡是出现以下 API 或行为，必须先停手审查：

- `AssetDatabase.FindAssets`
- `Directory.GetFiles`
- `Directory.EnumerateFiles`
- `AssetDatabase.LoadAssetAtPath` 大批量调用
- 遍历 `Assets/` 根目录
- 遍历所有 `ScriptableObject`
- 遍历所有 Prefab 并加载
- 在 ReloadDomain、InitializeOnLoad、AssemblyStream 注册器里做资源重建

只有满足全部条件才允许：

- 有明确用户触发，不是域重载自动执行。
- 有明确扫描范围，不是项目根目录。
- 有进度条或可取消机制。
- 有 Undo 或可回退策略。
- 有去重、防重复执行和异常保护。
- 有中文说明告诉开发者这一步会扫描哪些内容。

## 和资源系统的边界

资源注册、Library/Book/Page、AssetTable、构建清单、热更新清单不应该依赖 AssemblyStream 去自动全量生成。

合理方式是：

- Editor 面板手动收集。
- 指定 Library 或指定文件夹收集。
- 用 `ESDataBuildUsageAttribute` / 类型规则过滤。
- 收集结果写入 SO 或构建数据。
- GameManager 运行时读取已经烘焙好的表。

AssemblyStream 最多负责注册“收集器类型、菜单入口、窗口入口、字段规则”，不负责直接扫完整项目生成最终资产表。

## 给后续 AI 的结论

AssemblyStream 是编辑器解耦注册器，不是运行时系统，不是资源系统，不是全量扫盘系统。

新增功能优先通过特性注册解耦：注册器记录元数据，重操作交给明确 UI、明确按钮、明确文件夹、明确构建流程。

看到“域重载自动扫项目”“注册器里 FindAssets”“恢复 RuntimeRegister”三个信号，立即停止并改方案。
