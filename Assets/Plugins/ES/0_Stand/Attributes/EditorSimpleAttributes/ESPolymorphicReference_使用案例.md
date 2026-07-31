# ES 多态引用自动编辑

`ESPolymorphicReference` 现在不需要业务字段添加额外特性。只要字段使用 Unity 原生的
`[SerializeReference]`，Odin 建立 `PropertyTree` 后，ES 编辑器 Drawer 就会自动接管该节点的
类型选择与对象编辑。

```csharp
[SerializeReference]
public Effect effect;

[SerializeReference]
public List<Effect> effects;
```

## 选择器如何接管

Drawer 只匹配 Odin 的 `SerializationBackend.UnityPolymorphic`，因此不会影响普通 class、
`UnityEngine.Object` 引用、Odin 自己序列化的多态值或普通集合。

选择器本身使用 `ESSearchDropdown`。切换类型、创建对象和清空对象都是明确点击后才发生；
替换已有类型或覆盖缺失类型时会二次确认，清空则是一键操作并接入 Undo。打开 Inspector、刷新、编译和展开对象不会自动
创建实例、清空引用、扫描资产或写入场景。

界面采用连续的“对象编辑条”布局：

- 左侧整块区域都可以展开/折叠，不要求精确点中小箭头；
- 顶部显示业务字段名，右侧是有完整命中区的“重选类型”入口（已选对象也保持可重选）；
- 第二行显示“目录 / 类型 · CLR 类型”的稳定语义；
- 已配置、未配置、类型缺失分别使用蓝色、灰色、橙色状态线；
- 嵌套 managed-reference 会显示“嵌套 N”层级标题和缩进内容，子节点仍可继续重选；
- 普通子字段继续使用 Odin 原生 Drawer，不再额外套多层卡片或重复标题。

## 类型目录来源

候选类型以字段声明的基类为根，通过 Unity `TypeCache.GetTypesDerivedFrom(baseType)` 收集。
候选类型必须满足：

1. 具体、可序列化的 class；
2. 非抽象、非开放泛型、非 `UnityEngine.Object`；
3. 不属于 UnityEditor 命名空间；
4. 具有无参构造函数，且构造只在用户明确选择后执行。

业务目录优先读取项目已有的 Odin `TypeRegistryItemAttribute`：

```csharp
[Serializable]
[TypeRegistryItem("数值/固定伤害")]
public sealed class DamageEffect : Effect
{
    public float amount;
}
```

`/` 会直接生成选择器目录。没有 `TypeRegistryItem` 的类型不会被静默丢弃，而是进入“未登记类型”，
并显示 CLR 类型名与程序集信息。大型业务类型应沿用项目已有的 `TypeRegistryItem` 约定；不再
引入 `ESPolymorphicType` 这套重复元数据。

## 空值、集合和缺失类型

- 空值显示“选择类型创建”，不绑定打开界面或刷新自动创建。
- `List<T>` 或数组中的 managed-reference 元素沿用同一选择器；集合增删仍由 Odin 负责。
- 已保存但程序集/类型无法解析时，显示错误原因和原始类型名，不自动清空。
- 只有用户明确选择替代类型并确认后，才覆盖缺失引用。
- 清空已有对象是一键快速操作，保留 Unity/Odin 的 Undo 能力，可立即使用 `Ctrl+Z` 恢复。

## 性能边界

类型目录按声明基类缓存：

- 第一次打开某个基类的选择器时才查询 `TypeCache` 并建立目录；
- 普通 Inspector 重绘只读取缓存，不枚举程序集、不扫描资产、不扫描场景；
- 选择器真正打开时才创建菜单项；
- 编译完成或程序集重载时清空缓存，避免关闭 Domain Reload 后残留旧类型。

## 绘制方案切换

绘制方案目前通过 Unity 菜单切换：

```text
【ES】/开发与维护/多态引用/绘制方案/【ES】自定义渲染
ES/多态引用/绘制方案/Odin 默认动态渲染
```

方案保存在当前 Unity 项目的 `EditorPrefs` 中，不写入场景、Prefab 或资产。类型选择器保持
Unity 原生 `AdvancedDropdown` 的层级导航，顶部工具栏直接注入其标题区域。

## 案例

挂载 `ESPolymorphicReferenceCase` 到任意 GameObject：

- “触发效果”验证单个 `[SerializeReference]`；
- “效果序列”验证多态集合元素和 `CompositeEffect` 的二层嵌套；
- “未登记类型验证”验证没有 `TypeRegistryItem` 的候选类型；
- `DamageEffect`、`HealEffect`、`PlayAudioEffect` 和 `CompositeEffect` 验证业务目录树。

建议按以下顺序验收：

1. 在“触发效果”中重选类型，确认弹窗显示当前类型和目标类型；
2. 修改子字段后按 `Ctrl+Z`，确认类型和字段值一起恢复；
3. 点击右侧 `×` 快速清除，再按 `Ctrl+Z` 恢复；
4. 展开“二层嵌套”或“效果序列”中的“复合效果”，确认出现“嵌套 1”及其子节点，并继续操作“主效果”和“备用效果”；
5. 在“未登记类型验证”中打开目录，确认“未登记类型”分组可见。

若要验证缺失类型诊断，请先在测试场景中保存一个具体类型，再临时移动/重命名该类型的
程序集或完整类型名，重新编译后观察错误提示；不要在正式资产上做此实验。
