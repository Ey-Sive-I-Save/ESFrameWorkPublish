# GameCoreGlobalData 与 AICommands 协作警告

## 当前结论

项目需要一个核心语义入口，而不是让开发者手写大量规则，也不是让 AI 盲改枚举、Layer、Input 或 Shot。

已新增：

```text
Assets/Scripts/ESLogic/Runtime/Data/Normal/GameCoreGlobalData.cs
Assets/Scripts/ESLogic/Editor/GameCoreGlobalDataMenu.cs
Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreGlobalData.asset
Assets/Plugins/ES/AICommands
```

`GameCoreGlobalData` 是全局 SO：

```text
CreateAssetMenu: ES/GameCoreGlobalData
ESCreatePath: 全局数据 / GameCore全局数据
Base: ESEditorGlobalSo<GameCoreGlobalData>
```

它不做代码生成。

## 资产与 ES 入口

固定资产路径：

```text
Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreGlobalData.asset
```

【ES】菜单入口：

```text
ES/GameCore/打开或创建GameCore全局数据
ES/GameCore/重置GameCore推荐规则
```

后续 AI 不要只新增 C# 类型却忘记资产和入口。GlobalData 必须能被开发者在 Unity 编辑器里直接找到。

## 管什么

集中描述：

```text
GameMode 语义
GameModeTag 语义
GameTag 语义和归属
InputActionCategory 分类规则
物理层语义
AI Command 模板
```

现有枚举不要重复造：

```text
ESRuntimeMode
ESRuntimeModeTag
ESGameTag
ESInputActionCategory
ESInputActionId
```

`GameCoreGlobalData` 的职责是说明和约束，不是替代这些枚举。

不要把 `StateMachineConfig` 挂进 `GameCoreGlobalData`。状态机全局配置仍由 `StateMachineConfig` 自己管理，GameCore 只管跨系统核心语义。

## AI 修改规则

遇到以下需求，先看 `GameCoreGlobalData` 和 `Assets/Plugins/ES/AICommands`：

```text
新增输入
新增 GameTag
新增/调整物理层
新增 Shot/飞行物类型
调整 GameMode 或输入过滤
```

不要直接：

```text
随意占用 Reserved GameTag
在业务脚本里硬编码 LayerMask
绕过 RuntimeMode 输入过滤
把 Shot 的每发变量写回 ItemDataInfo
新增一堆 Domain 来表达简单语义
```

## AICommands

`Assets/Plugins/ES/AICommands` 里是给开发者复制给 AI 的命令模板。开发者只需要补充需求，AI 应按模板先查规则、再改代码、最后编译。

这不是自动代码生成器，是“规范化 AI 改代码入口”。
