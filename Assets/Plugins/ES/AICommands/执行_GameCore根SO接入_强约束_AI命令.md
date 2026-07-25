# 执行：GameCore 根 SO 接入（P0 强约束）

命令类型：P0 游戏核心搭建。

默认改文件：允许，仅限目标 GameCore 根 SO、对应 GameCoreTable、Consumer 配置与必要测试。

风险等级：L3。错误实现会导致启动核心包漏载、重复注入或将 Key/嵌套数据误作为独立核心资产。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/README.md
Assets/Plugins/ES/AIWarnings/项目最高警告/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md
Assets/Plugins/ES/AIWarnings/项目最高警告/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md
Assets/Plugins/ES/0_Stand/_Res/Runtime/ESScriptableObjectClassification.cs
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
```

## 允许目标

- 为 `SkillDefinitionDataInfo`、`BuffDefinitionDataInfo`、`ActorDataInfo`、`ItemDataInfo` 等领域 Info 接入 `IGameCoreSO`。
- 让抽象 `SoDataGroup<TInfo>` 与 `SoDataPack<TInfo>` 直接实现 `IGameCoreSO`；遍历 `Infos.Values` 时仅对 `info is IGameCoreSO` 的 Info 转发注入，其他 Info 正常跳过。
- 建立明确的 Key 校验、唯一目标 Table 注入与重复 Key 失败策略。
- 将根 SO 收集到唯一 Consumer 启动核心包，并验证启动注入。

## 新类别固定模板

新增 GameCore 类别必须只在该类别自己的领域目录（`ES_Logic` 或热更程序集）增加以下内容：

```text
<Category>EnumKey / <Category>ConfigKey
<Category>RuntimeData
<Category>RuntimeTable : ESConfigKeyTable<<Category>RuntimeData>
<Category>DataInfo : SoDataInfo, IGameCoreSO
可选：<Category>DataGroup / <Category>DataPack
```

`<Category>DataInfo.InjectGameCoreTables()` 必须直接校验 Key 并写入 `<Category>RuntimeTable`。Group/Pack 已在抽象基类中完成接口转发，因此新类别不得修改 `0_Stand`、不得添加反射注册、不得修改中央启动分发。

运行期业务代码必须持有强类型 `<Category>ConfigKey`，直接查询 `<Category>RuntimeTable`；不得用字符串类别、`Type` 或 `Dictionary<Type, object>` 作为热路径入口。

## 绝对禁止

```text
1. 不得让 ESGameCoreConfigKey<T>、EnumKey、StringKey 实现 IGameCoreSO。
2. 不得让 RuntimeData、SharedData、VariableData、AssetTable 记录实现 IGameCoreSO。
3. 不得在 InjectGameCoreTables 内加载 AB、下载资源、读取 Library 或依赖 RuntimeKey。
4. 不得为兼容旧 JSON 源、旧 ESResKey 恢复旧配置链路。
5. 不得让同一个根 SO 注入多个不相干的领域 Table。
6. 不得反射查找 InjectGameCoreTables、按类型名猜测注入资格，或强制所有 Info 实现 IGameCoreSO。
7. 不得为新增类别修改 `0_Stand`，不得新增 `ESRuntimeDataModule.InjectGameCoreRoot` 重载、中央 switch 或类型注册表。
8. 不得继承其他类别的 `*DataInfo`；类别相似只能复用可序列化组合数据，不能复用 DataInfo 继承层级。
```

## 执行步骤

```text
1. 在新类别目录创建 Key、RuntimeData、强类型 RuntimeTable 和 DataInfo；不修改 0_Stand 或中央模块。
2. 由 DataInfo 实现 IGameCoreSO.InjectGameCoreTables：校验 Key、拒绝重复、直接注入本类别 Table。
3. 可选创建 Group/Pack；抽象基类会遍历 Infos.Values，并只转发实际实现 IGameCoreSO 的 Info。
4. 配置唯一 Consumer，确认其收集到 Info、Group 或 Pack 时不混入普通资源、Key 或嵌套数据。
5. 编译，并验证启动期加载、注入、强类型按 Key 查询、重复 Key 失败与非 GameCore Info 跳过场景。
```

## 交付格式

```text
根 SO：
稳定 Key：
目标 GameCoreTable：
Consumer：
重复 Key 策略：
验证结果：
未处理风险：
```
