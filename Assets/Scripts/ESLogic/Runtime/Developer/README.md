# ES Developer Runtime

`Developer` 是开发者日常入口层，用来集中放置最常接触的架构说明、可挂组件、轻封装、示例和文档。

它不是新的底层系统，不替代 `Entity`、`GameManager`、`State`、`Operation`、`Shared`、`Feature` 等主干目录。

## 目录职责

```text
Architecture  常用架构说明、Core-Domain-Module 使用说明
Components    开发者常挂的 Mono 组件
Wrappers      轻封装、绑定器、Tracker 使用封装、ExpressionSource 到 ValueChange 的桥接
Examples      最小样例、输入触发技能、状态生命周期、ValueChange Buff 示例
Docs          当前主链路说明、废弃方案和协作注意事项
```

## 当前已收纳内容

```text
Wrappers/State
  StateLifecycleTracker
  StateLifecycleTrackerExtensions

Components/Context
  ContextPoolPlayer

Components/References
  GameObjectRefer

ValueEntry
  IValueEntry
  IStringValueEntry
  ISpriteValueEntry

Command 常用组件位于：
  Assets/Scripts/ESLogic/Runtime/Command/Components/ESCommandPlayer.cs

Wrappers/ValueChange
  ESFloatValueChangeExpressionBinding
  ESPermitValueChangeExpressionBinding

```

## 放置规则

- 底层协议继续放在 `Assets/Plugins/ES/0_Stand`。
- 游戏运行主干继续放在 `Entity`、`Item`、`GameManager`、`State`、`Operation`。
- 可挂载功能包继续放在 `Features`。
- `Developer` 只收开发者高频接触入口和薄封装，不做大型业务系统。
- 若一个类型会成为热路径核心实现，应优先放回对应主干目录，而不是放在这里。
- 编辑器预览辅助单独放在 `Assets/Scripts/ESLogic/Editor/Preview`，不要塞回 `Developer`。
- `ValueEntry` 当前偏数据展示、多语言和 UI 读取协议，不是纯底层协议，先放在 `Developer`。
