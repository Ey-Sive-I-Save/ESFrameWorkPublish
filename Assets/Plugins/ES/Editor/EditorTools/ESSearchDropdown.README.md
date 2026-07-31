# ESSearchDropdown

ES 编辑器统一的可搜索选择器。它不依赖 ConfigKey、Catalog 或任何业务类型。

## 最简单：直接选择任意集合

```csharp
ESSearchDropdown.OpenItems(
    buttonRect,
    "选择场景",
    scenes,
    scene => scene.name,
    scene => OpenScene(scene));
```

## 常用：Builder

```csharp
ESSearchDropdown.Create("添加命令")
    .Add("创建角色", CreateActor, "角色", actorIcon)
    .Add("创建武器", CreateWeapon, "物品", weaponIcon)
    .AddSeparator()
    .AddDisabled("当前功能不可用")
    .Show(buttonRect);
```

## 结构化信息

```csharp
var entry = ESSearchDropdown.Entry.Item(
    label: "FireBall",
    onSelected: SelectFireBall,
    groupPath: "技能/火系",
    icon: fireIcon,
    subtitle: "SkillDefinitionDataInfo · Skill.FireBall",
    tooltip: "Assets/GameCore/Skills/FireBall.asset",
    keywords: "火球 fire projectile",
    badge: "子资产",
    selected: true);
```

- `groupPath`：使用 `/` 创建多级目录。
- `subtitle`：紧邻主名称显示类型、Key 或路径摘要。
- `keywords`：加入显示与搜索文本，适合中文别名和英文术语。
- `badge`：显示快捷键、状态或资源身份。
- `selected`：使用 `✓` 标识当前项。

## 延迟数据源

资源扫描较重时，不要提前构建列表：

```csharp
ESSearchDropdown.Open(
    buttonRect,
    "选择资源",
    () => ScanProjectAssets().Select(asset =>
        ESSearchDropdown.Entry.Item(
            asset.name,
            () => Select(asset),
            AssetDatabase.GetAssetPath(asset),
            AssetPreview.GetMiniThumbnail(asset))));
```

Provider 只有在 Dropdown 真正构建时才执行。Provider 或选择回调异常会被隔离并输出明确的 Console 错误。

## 兼容性

原有接口保持不变：

```csharp
ESSearchDropdown.Open(rect, title, entries);
new ESSearchDropdown.Entry(label, icon, callback, groupPath);
```

现有调用方不需要迁移即可继续使用。

## 顶部工具栏

需要诊断、刷新目录或复制路径时，可通过 `toolbarActions` 在原生 Advanced 顶部搜索/标题工具区右侧添加小按钮；
这些动作不会混入候选条目：

```csharp
ESSearchDropdown.Open(
    rect,
    "选择类型",
    provider,
    toolbarActions: new[]
    {
        new ESSearchDropdown.ToolbarAction("诊断", DumpDiagnostics, "输出当前选择器诊断信息"),
        new ESSearchDropdown.ToolbarAction("刷新", RefreshCatalog, "刷新候选目录")
    });
```

无论是否传入 `toolbarActions`，候选树、搜索、分组、返回和选中状态都继续使用 Unity 原生
`AdvancedDropdown`。工具栏动作会叠加到同一个原生 Advanced 窗口顶部，不另起弹层，也不占用搜索框。
