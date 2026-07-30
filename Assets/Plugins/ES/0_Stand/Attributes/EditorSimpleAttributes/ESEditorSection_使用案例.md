# ESEditorSection 使用案例

`ESEditorSection` 为字段、属性或 Odin 按钮方法声明其所属的编辑器内容分区。它不改变序列化、运行时生命周期或 Odin 对字段的正常绘制，只让 `ESEditorSectionNavigator` 在 Inspector 或嵌入式 `OdinEditor` 中提供内容目录。

可直接挂载的代码案例是 `ESEditorSectionNavigatorCase`；在任意 GameObject 的 Add Component 搜索这个类名，就能看到完整的配置目录。

这里的 `ESEditorSectionNavigator` 是一项编辑器行为，不是需要挂到 Entity 上的运行时对象。实际绘制类是 `ESEditorSectionNavigatorDrawer`；因此不会为导航再引入一个无职责的 `Config`、`Data` 或组件包装层。

## Entity 案例

`Entity` 是当前第一个接入对象。原先的 `TabGroup("生命体结构", ...)` 已替换为明确的业务分区：

```csharp
[ESEditorSection("core", "核心配置", -100f)]
[LabelText("主 Animator")]
public Animator animator;

[ESEditorSection("body", "身体基础", 10f)]
[HideLabel, HideReferenceObjectPicker, SerializeReference]
public EntityBasicDomain basicDomain = new EntityBasicDomain();

[ESEditorSection("ai", "意识 AI", 20f)]
[HideLabel, HideReferenceObjectPicker, SerializeReference]
public EntityAIDomain aiDomain = new EntityAIDomain();

[ESEditorSection("buff", "Buff", 30f)]
[HideLabel, HideReferenceObjectPicker, SerializeReference]
public EntityBuffDomain buffDomain = new EntityBuffDomain();

[ESEditorSection("attributes", "角色属性", 40f)]
[HideLabel, InlineProperty]
public ESSuperAttributeTable superAttributes;

[ESEditorSection("state", "状态表现", 50f)]
[HideLabel, HideReferenceObjectPicker, SerializeReference]
public EntityStateDomain stateDomain = new EntityStateDomain();

[ESEditorSection("body", "身体基础", 10f)]
[Title("身体运动核心（KCC，高频）")]
[HideLabel]
public EntityKCCData kcc = new EntityKCCData();
```

Inspector 目录的预期顺序：

```text
核心配置  ·  身体基础  ·  意识 AI  ·  Buff  ·  角色属性  ·  状态表现  ·  诊断
```

选择“身体基础”时，`EntityBasicDomain` 与 `EntityKCCData` 一起绘制，但各自仍使用原有 Odin Drawer 和自己的内部布局。Navigator 不会把 Domain 的字段搬到 Entity，也不会复制配置。

## 这个案例解决什么

假设角色配置逐步增长到一百多个字段。直接按声明顺序排布时，制作人员必须先记住字段属于哪个 Domain，再不停滚动寻找；而把所有内容改成 `TabGroup` 又会变成厚重的网页式页签，并且容易诱使 Entity 接管 Domain 内部的字段。

这个案例把信息顺序固定为：

```text
先完成角色自身的核心引用
    -> 再配置身体与运动
    -> 再配置控制来源和 Buff
    -> 再进入角色属性与状态表现
    -> 最后查看只读诊断
```

分区只解决“现在应该看哪一类信息”。每个分区内部仍按连续标题、细分隔线、稳定标签列宽、`FoldoutGroup` 和专用表格组织字段；不要在每层再套 `BoxGroup`。复杂集合继续使用 `TableList` 或它自己的编辑器，不挤回普通单行字段。

## Odin 执行链路

```text
Odin 为 Entity 建立 PropertyTree
    -> ESEditorSectionNavigatorDrawer 从宿主已声明的分区属性建立有序 SectionIndex
    -> 处理器注入的分区属性在各自 Group 被 Odin 绘制时安全地增量登记；Drawer 不会在布局期递归枚举同一棵 PropertyTree
    -> 用户在“内容目录”选择一个 SectionId
    -> Drawer 只对选中的 PropertyGroup 调用 Odin 的下一层 Drawer
    -> 该字段、Domain 或 TableList 仍由 Odin 原有 Drawer 链绘制
```

导航状态保存在当前 `PropertyTree` 的弱引用上下文中，选择结果只用 `SessionState` 暂存。它不是序列化字段，不写入 Prefab，不作为 ConfigKey、RuntimeKey、存档或网络身份。

因此切换目录不会触发资源扫描、场景修改、资产写入、配置重建或运行时对象创建。它只改变本次 GUI 绘制中哪些已有属性可见。

## 状态语义案例

目录标题只应表达用户需要处理的状态，不应承担装饰。后续为某个分区接入状态时，采用以下含义：

| 显示 | 含义 | 用户下一步 |
| --- | --- | --- |
| 无标记 | 配置完整或不需要检查 | 正常编辑 |
| `!1` | 有一个会阻止当前任务的问题 | 进入分区，看到原因和修复位置 |
| `3` | 有三项待补全的引用或配置 | 进入分区逐项完成 |
| 灰色 | 当前条件下不适用 | 保留上下文，不把它误报成错误 |

状态数必须来自宿主已经维护的 O(1) 结果，例如已有校验缓存或当前对象引用计数。目录重绘不得为了算 `!1` 扫描场景、资产目录或所有组件。当前 Entity 基础案例尚未声明分区状态，先保证分区和原有 Drawer 的兼容；接入状态时再由 `ESEditorSectionStatus` 承担这一项独立的展示职责。

错误区必须解释“为什么不能继续”和“到哪里修”。例如“状态表现 !1”展开后应直接给出缺失的 Animator 或状态定义位置，而不是只把主操作禁用。

## 为什么基础案例不先使用 AttributeProcessor

`OdinAttributeProcessor<T>` 很适合在不修改第三方类型、生成类型或遗留类型源码时，通过 `ProcessChildMemberAttributes` 给指定成员补充 `ESEditorSectionAttribute`。例如未来某个外部 Actor 类型不能直接加 Attribute 时，可在 Editor 程序集中用 Processor 维护它的成员到分区的映射。

`Entity` 是本项目拥有的核心类型，字段所属的业务 Domain 就在声明处。这里直接标注 `ESEditorSection` 更可读，也让重命名、删除字段和代码审查保持同一权威来源。不要为了“用了 Processor”把这份明确元数据挪到一个远处的字符串映射表。

Processor 只负责补充 Editor 属性，不能修改字段值、建立运行时状态、扫描资产或在域重载时执行业务动作。

## 接入规则

1. `sectionId` 使用稳定、简短的小写英文，例如 `body`、`state`、`diagnostics`。它只用于当前编辑器会话的导航状态，不进入存档、网络、Prefab 或 RuntimeKey。
2. `displayName` 使用直接、中文友好的业务名称，例如“身体基础”“状态表现”。
3. 同一个宿主对象中，同一 `sectionId` 必须使用相同显示名与排序值。
4. 一个字段只声明一个 `ESEditorSection`。不要再同时使用 `TabGroup` 作为同层导航。
5. `TitleGroup`、`FoldoutGroup`、`TableList` 继续用于分区内部。完整 Group 路径不能混用不同 Group 类型。
6. 需要显示错误数或待处理数时，由宿主提供已缓存的 O(1) 结果；目录重绘不得扫描资产、场景或组件。

## 窄宽度行为

目录总宽度小于 Inspector 可用宽度时，显示为文字目录和细下划线。空间不足时自动切换为“内容目录”下拉选择，不会挤成多行 Tab 或缩小文字。

## 验收步骤

1. 选择一个带有 `Entity` 的 GameObject。
2. 确认 Inspector 显示内容目录，而不是原“生命体结构”Tab。
3. 依次切换“身体基础”“意识 AI”“状态表现”，确认每个 Domain 的内部布局和原有 Odin 控件保持正常。
4. 缩窄 Inspector 宽度，确认目录切换为下拉选择，字段不重叠。
5. 修改任意可序列化字段后执行 Undo/Redo，确认 Odin 原有序列化和撤销行为不变。
6. 在含有大量字段的对象上，确认核心任务分区始终在目录前半段；高级配置、调试和历史信息只出现在后半段。
7. 若接入状态标记，确认每一个错误标记都能跳到具体字段并说明修复原因，且打开 Inspector 本身不会触发扫描或写入。

## 边界

`ESEditorSectionNavigator` 只决定当前绘制哪个内容分区。它不执行资产扫描、不修改场景、不重建配置、不创建运行时对象，也不替代 Domain 的 Inspector 绘制权。
