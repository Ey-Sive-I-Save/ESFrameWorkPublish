# 执行：GameCore 根 SO 接入（P0 强约束）

命令类型：P0 游戏核心搭建。

默认改文件：允许，仅限目标 GameCore 根 SO、对应 GameCoreTable、Consumer 配置与必要测试。

风险等级：L3。错误实现会导致启动核心包漏载、重复注入或将 Key/嵌套数据误作为独立核心资产。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md
Assets/Plugins/ES/0_Stand/_Res/Runtime/ESScriptableObjectClassification.cs
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRuntimeDataModule.cs
```

## 允许目标

- 为 `SkillDefinitionDataInfo`、`BuffDefinitionDataInfo`、`ActorDataInfo`、`ItemDataInfo` 等领域 Info 接入 `IGameCoreSO`。
- 让抽象 `SoDataGroup<TInfo>` 与 `SoDataPack<TInfo>` 直接实现 `IGameCoreSO`；遍历 `Infos.Values` 时仅对 `info is IGameCoreSO` 的 Info 转发注入，其他 Info 正常跳过。
- 建立明确的 Key 校验、显式枚举分流、目标 Table 注入与重复 Key 失败策略。
- 将根 SO 收集到唯一 Consumer 启动核心包，并验证启动注入。

## 新类别固定模板

新增 GameCore 类别必须只在该类别自己的领域目录（`ES_Logic` 或热更程序集）增加以下内容：

```text
<Category>EnumKey / <Category>ConfigKey
<Category>RuntimeData : ESGameCoreRuntimeData
<Category>RuntimeTable : ESGameCoreConfigKeyTable<<Category>RuntimeData>
<Category>DataInfo : SoDataInfo, IGameCoreSO
可选：<Category>DataGroup / <Category>DataPack
```

`<Category>DataInfo.InjectGameCoreTables()` 必须直接校验 Key 并写入所属强类型 RuntimeTable。普通根 SO **类型**允许通过稳定、显式的领域枚举选择一个兼容 Table，单个资产实例只进入当前分支。若领域合同明确包含“基础定义 + 专项能力投影”，则可进入基础 Table 和枚举选中的唯一专项 Table；例如每个 `ItemDataInfo` 都进入 ItemTable，`ItemKind.Shot` 再进入 ShotTable，`ItemKind.Weapon` 再进入 WeaponTable。每个投影必须使用独立强类型 Key / RuntimeData，先全量预验证，再原子提交并在任一失败时回滚本轮结果；各表 RuntimeKey 不得跨表比较或互相解释。禁止无枚举、无独立 Schema、无事务边界地盲目写入多个无关 Table。Group/Pack 已在抽象基类中完成接口转发，因此新类别不得修改 `0_Stand`、不得添加反射注册、不得修改中央启动分发。

运行期业务代码必须持有强类型 `<Category>ConfigKey`，直接查询 `<Category>RuntimeTable`；不得用字符串类别、`Type` 或 `Dictionary<Type, object>` 作为热路径入口。

## 绝对禁止

```text
1. 不得让 ESGameCoreConfigKey<T>、EnumKey、StringKey 实现 IGameCoreSO。
2. 不得让 RuntimeData、SharedData、VariableData、AssetTable 记录实现 IGameCoreSO。
3. 不得在 InjectGameCoreTables 内加载 AB、下载资源、读取 Library 或依赖 RuntimeKey。
4. 不得为兼容旧 JSON 源、旧 ESResKey 恢复旧配置链路。
5. 不得把“一类根 SO”错误等同于“一类 GameCore”。允许由显式领域枚举做兼容分流，也允许领域合同明确的基础投影加唯一专项投影；禁止隐式猜测、字符串/类型名分发，禁止无独立 Key、Schema 和事务边界地盲目注入多个 Table。
6. 不得反射查找 InjectGameCoreTables、按类型名猜测注入资格，或强制所有 Info 实现 IGameCoreSO。
7. 不得为新增类别修改 `0_Stand`，不得新增 `ESRuntimeDataModule.InjectGameCoreRoot` 重载、中央 switch 或类型注册表。
8. 不得继承其他类别的 `*DataInfo`；类别相似只能复用可序列化组合数据，不能复用 DataInfo 继承层级。
9. `*SharedData` 必须是引用类型 `class`，表示多个运行实例共享的只读定义；不得使用 `struct` 冒充共享数据。
10. `*VariableData` 只有在字段全为值类型时才可使用 `struct`；禁止包含 `string`、数组、List、Dictionary、class、UnityEngine.Object、接口或 delegate。含引用字段时必须实现显式深拷贝。
11. 不得直接使用 `RegisterAndGetRuntimeKey`、Upsert 或新 RuntimeData 覆盖已有稳定 Key；成功必须 `CommitRetained/TryCommitRetained`。
12. 不得把 SO 字段复制、默认值解析、filler 或 `CreateRuntimeData` 放在 Acquire 后的 try 外；异常和 Try 提前失败必须 `AbandonRetained`。
13. 不得让 RuntimeData 只设置 `Ready=false` 却继续强引用 SO、SharedData、Prefab、集合或其他重量级载荷；必须完整实现 `ReleaseRuntimePayload`。
```

## 执行步骤

```text
1. 在新类别目录创建 Key、非池化稳定 RuntimeData、强类型驻留 RuntimeTable 和 DataInfo；不修改 0_Stand 或中央模块。
2. RuntimeData 继承 `ESGameCoreRuntimeData` 并完整实现 `ReleaseRuntimePayload`；Table 继承 `ESGameCoreConfigKeyTable<TData>` 并设置唯一 `GameCore.<Category>` KeyScope。禁止增加 `Rent`、`ResetRuntimeData` 或池接口。
3. 由 DataInfo 实现 IGameCoreSO.InjectGameCoreTables：校验根 SO 的领域枚举和本轮全部投影 Key、拒绝重复，随后对每个投影执行 `AcquireRetained → try 内复制载荷 → CommitRetained`；catch 必须 `AbandonRetained`。存在基础 + 专项双投影时，专项失败还必须撤销本轮已提交的基础投影，并验证回滚完整。
4. 可选创建 Group/Pack；抽象基类会遍历 Infos.Values，并只转发实际实现 IGameCoreSO 的 Info。
5. 配置唯一 Consumer，确认其收集到 Info、Group 或 Pack 时不混入普通资源、Key 或嵌套数据。
6. 编译，并验证启动期加载、强类型按 Key 查询、准备异常回滚、重复 Key 回滚、Clear/Remove 后 Ready=false 与载荷释放、同 Key 重建复用，以及非 GameCore Info 跳过场景。
```

命令 ID：`gamecore.root.execute`

## ContractCompleteness

```text
cancellation: before-commit only; after-commit requires compensation and RecoveryRequired.
recovery: retain/CAS transaction, AbandonRetained on failure, no blind replay.
validation: compile plus duplicate-key, rollback, Ready=false and payload-release checks.
evidenceRef: commandBodyHash, planHash, writeScope, test output and source SHA-256.
allowRoots: target GameCore domain directory and its necessary tests only.
denyPaths: AIWarnings, AICommands Catalog, Git/.git, ProjectSettings, Packages, release, Runtime and Library; deny-overrides.
```

## 交付格式

```text
根 SO：
稳定 Key：
目标 GameCoreTable：
Consumer：
重复 Key 策略：
事务回滚：
ReleaseRuntimePayload：
验证结果：
未处理风险：
```
