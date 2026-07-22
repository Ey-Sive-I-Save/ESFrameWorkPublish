# 项目最高警告：配置双键与 Inspector 分层

职责：这是 ESFramework 给后续 AI 的项目最高警告。凡是 Buff、Tag、State、Skill、Item、Camera、Mode 等可配置运行对象，配置层允许使用“枚举键 + 字符串键”双键体系，但运行时热路径必须优先使用已烘焙、已缓存的强类型键。

## 核心结论

配置不是只能靠字符串路径表达层级。

Unity 枚举字段可以使用 `[InspectorName("分类/名称")]` 在 Inspector 中显示成分层菜单，例如：

```csharp
public enum ESBuffKey : ushort
{
    None = 0,

    [InspectorName("控制/冰冻")]
    控制类_冰冻 = 1,

    [InspectorName("控制/眩晕")]
    控制类_眩晕 = 2,

    [InspectorName("伤害/燃烧")]
    伤害类_燃烧 = 20,
}
```

这比默认推广 `Buff.控制.冰冻` 这种运行时字符串路径更合适。

## 正确理解

配置层可以同时保留：

- 枚举键：高频、强类型、可编译期检查，适合核心游戏对象。
- 字符串键：扩展、热更新、外部表格、非核心低频配置。

两者可以共享 Inspector 分层表达：

- 枚举用 `[InspectorName("控制/冰冻")]`。
- 字符串配置可以用 `"控制/冰冻"` 作为编辑器分类路径。

但不要把字符串路径本身误当成运行时最高身份。

## Buff / Tag / RuntimeKey 规则

Buff 的身份可以是：

```csharp
ESBuffKey.控制类_冰冻
```

它在 Inspector 里显示为：

```text
控制/冰冻
```

它带来的事实状态可以是：

```csharp
ESGameTag.控制类_冰冻
ESGameTag.行为类_禁止移动
ESGameTag.行为类_禁止释放技能
```

注意：

- `ESBuffKey` 表示“这个 Buff 配置是谁”。
- `ESGameTag` 表示“实体当前拥有什么状态事实”。
- `RuntimeKey` 表示跨资产、跨配置、跨运行缓存的统一身份协议。

三者可以协作，但不能混成一个东西。

## 禁止误区

- 不要默认设计 `Buff.控制.冰冻` 这种点号字符串作为核心运行时 Key。
- 不要为了 Inspector 分层强行发明多层类、多层资产、多层字典。
- 不要让高频 Buff / Tag / State 查询依赖字符串查找。
- 不要在 Update、KCC、StateMachine Evaluate、IK 求解、Buff Tick 中做字符串转 Key。
- 不要把枚举中文名、Inspector 显示名、RuntimeKey、GameTag 语义混为一谈。

## 推荐默认方案

核心高频对象：

```csharp
public enum ESGameTag : ushort
public enum ESBuffKey : ushort
public enum ESSkillKey : ushort
public enum ESStateKey : ushort
```

Inspector 显示：

```csharp
[InspectorName("控制/眩晕")]
控制类_眩晕 = 1
```

运行时：

```csharp
entity.HasGameTag(ESGameTag.控制类_眩晕);
entity.buffDomain.HasBuff(ESBuffKey.控制类_眩晕);
```

扩展配置：

```csharp
string customKey = "控制/冰冻";
```

但字符串必须在编辑器、烘焙、初始化阶段转换成缓存 Key，不能进入核心热路径。

## 一句话

分类展示交给 Inspector，运行身份交给强类型键和 RuntimeKey；字符串用于配置扩展，不进入高频判断。

