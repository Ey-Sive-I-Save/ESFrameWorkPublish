# GameCoreEditorGlobalData 与 AICommands 协作警告

> 文件名保留 `GameCoreGlobalData与AICommands...` 仅为兼容既有 AICommand 引用。当前唯一有效类型、资产和菜单均使用 `GameCoreEditorGlobalData`；禁止根据文件名恢复旧类型。

## 当前结论

项目需要一个核心语义入口，而不是让开发者手写大量规则，也不是让 AI 盲改枚举、Layer、Input 或 Shot。

当前权威入口：

```text
Assets/Scripts/ESLogic/Runtime/Data/Normal/GameCoreEditorGlobalData.cs
Assets/Scripts/ESLogic/Editor/GameCoreEditorGlobalDataMenu.cs
Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreEditorGlobalData.asset
Assets/Plugins/ES/AICommands
```

`GameCoreEditorGlobalData` 是编辑器语义入口 SO：

```text
CreateAssetMenu: 【ES】/项目设置/GameCore/编辑器全局数据
Base: ESEditorGlobalSo<GameCoreEditorGlobalData>
```

它集中维护编辑器可见的跨系统语义、推荐规则、GameTag 定义和 AI Command 模板，不进入运行时配置链，也不替代各领域真实 DataInfo、Catalog 或 Table。它不做代码生成。

## 资产与 ES 入口

固定资产路径：

```text
Assets/ESNormalAssets/Data/GlobalData/GameCore/GameCoreEditorGlobalData.asset
```

【ES】菜单入口：

```text
【ES】/项目设置/GameCore/打开或创建GameCore编辑器全局数据
【ES】/项目设置/GameCore/重置GameCore编辑器推荐规则
【ES】/项目设置/GameCore/补齐缺失的GameTag规则
【ES】/项目设置/GameCore/验证GameTag规则
【ES】/项目设置/GameCore/Bake并应用GameTag Catalog
【ES】/项目设置/GameCore/运行GameTag核心自检
【ES】/项目设置/GameCore/验证全部Buff的GameTag配置
【ES】/项目设置/GameCore/验证运行时Key Catalog Schema
【ES】/项目设置/GameCore/审计项目稳定Key治理
```

后续 AI 不要只新增 C# 类型却忘记资产、菜单、Bake 和审计入口。编辑器全局数据必须能被开发者直接找到，但运行时不得依赖该编辑器 SO。

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

`GameCoreEditorGlobalData` 的职责是说明、配置和验证编辑器语义，不是替代这些枚举，也不是运行时 GameCore 根。

不要把 `StateMachineConfig` 挂进 `GameCoreEditorGlobalData`。状态机全局配置仍由 `StateMachineConfig` 自己管理，GameCore 编辑器数据只管跨系统核心语义。

## AI 修改规则

遇到以下需求，先看 `GameCoreEditorGlobalData`、对应领域源码和 `Assets/Plugins/ES/AICommands`：

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

## 禁止恢复的旧入口

以下名称和路径均已废弃：

```text
GameCoreGlobalData
GameCoreGlobalDataMenu
GameCoreGlobalData.asset
ES/GameCore/...
CreateAssetMenu: ES/GameCoreGlobalData
```

修改后至少执行：

```text
搜索旧类型与旧资产路径，生产源码和当前事实段应为零命中
搜索 MenuItem/CreateAssetMenu/AddComponentMenu 的 ES/、Window/ES/、Tools/ES/ 旧根
确认 GameCoreEditorGlobalData.asset 唯一且菜单能定位
执行 GameTag 验证、Catalog Bake、稳定 Key 审计
```
