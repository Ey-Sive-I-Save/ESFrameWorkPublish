# 项目最高警告：GameCore 根 SO 注入边界

> 级别：P0。适用于 GameCore 根 SO、其嵌套数据与 RuntimeData，以及 Prefab、GameObject、场景和普通内容资产之间的依赖设计。

## P0：GameCore 依赖只能由外向内

GameCore 是项目启动阶段建立并保持可用的核心定义层。它允许被 Prefab、场景对象、普通 SO 和业务配置直接引用，但不得反向直接引用 Prefab、GameObject、Component 或场景内容。

```text
项目启动
  → 加载并注入 GameCore
  → Prefab / 场景对象 / 普通内容资产引用 GameCore 定义

允许：Prefab / GameObject / 普通 SO ──> GameCore 根 SO
禁止：GameCore 根 SO / 嵌套数据 / RuntimeData ──> Prefab / GameObject / 场景内容
```

强制规则：

- 专用 Entity Prefab 可以直接拖入 `MonsterDataInfo`、`NpcDataInfo` 或 `ActorDataInfo` 作为定义入口；这不是资源边界破坏。
- Prefab 只保存定义引用，不得复制一份 GameCore 内容、固有 Tag 或其他定义字段；定义内容仍以 DataInfo 为唯一权威。
- 通用池模板可以不绑定具体定义，由明确的租出方在取出对象后直接调用 `Entity.BindDefinition(...)`；不得为了通用化强迫所有专用 Prefab 只保存 ConfigKey。
- GameCore 根 SO、其可序列化嵌套配置和 RuntimeData 禁止直接保存 `GameObject`、`Component`、Prefab 或场景对象引用。
- GameCore 若要表达内容资源需求，只能保存稳定的类型化资产 Key / `ESAssetRefer`，交给 ResourcePlan、AssetTable 或对应资源系统解析；禁止靠 Unity 直接引用形成反向 Bundle 依赖。
- `IGameCoreSO` 表示可注入的核心根，不表示“其他资产不得引用该 SO”。“Prefab 不得实现 `IGameCoreSO`”与“Prefab 可以引用 GameCore SO”必须同时成立。
- 若现有 GameCore RuntimeData 或注入代码仍含 `GameObject prefab` 等字段，只能视为待迁移旧结构，禁止作为新类别模板继续扩散。

违反该方向会让启动核心包反向拖入具体内容、扩大常驻内存、制造 Bundle 环和发布边界污染，按 P0 处理。

## 不可违反的事实

`IGameCoreSO` 的唯一职责是把一个**独立、可启动加载的 GameCore 根 ScriptableObject**注入目标 `GameCoreTable`：

```csharp
public interface IGameCoreSO
{
    void InjectGameCoreTables();
}
```

Consumer 启动核心包只允许收集并加载实现该接口的根 SO。运行时会直接调用 `InjectGameCoreTables()`；未实现接口的对象进入该包必须视为构建错误。

这只是 **Consumer 的 SO 启动 Provider 规则**，不代表 GameCore 必须依赖 SO。`ESGameCoreConfigKeyTable<TData>`、ConfigKey、RuntimeData 与运行时查询链必须保持纯 C# 可用。服务器数据、JSON/二进制、DLC/Mod、程序生成数据和测试数据可以不经过 `IGameCoreSO`，直接通过各领域强类型表的 `InjectWith/TryInjectWith` 注入。

```csharp
int skillRuntimeKey = ESRuntimeDataGameCore.Skills.InjectWith(skillKey);
bool added = ESRuntimeDataGameCore.Monsters.TryInjectWith(monsterKey, out int monsterRuntimeKey);
```

强制边界：

- `InjectWith` 必须由各领域自己的强类型 Table 提供并在内部创建 RuntimeData；普通调用者不需要知道 RuntimeData 类型和字段。
- `InjectWith` 是严格入口，失败或 Key 冲突必须抛异常；`TryInjectWith` 是安全入口，失败返回 false。
- 不得建立中央类别 switch、反射工厂或要求非 SO 数据伪装成 `IGameCoreSO`。
- Shared/Variable 数据作为领域参数传入是允许的，但它们自身仍不得实现 `IGameCoreSO` 或主动注入全局表。

## 必须实现 IGameCoreSO 的对象

- `SkillDefinitionDataInfo`：注入 SkillTable。
- `BuffDefinitionDataInfo`：注入 BuffTable。
- `ActorDataInfo`：非 GameCore 通用角色定义，不实现 `IGameCoreSO`；Player / Rider / StoryActor 等正常由普通 Actor Group/Pack 组织。
- `ItemDataInfo`：按 `ItemKind` 注入 Weapon / Shot 等对应 Item 领域 Table。
- 每个具体 `SoDataInfo` 类型必须有对应的具体 `SoDataGroup<TInfo>`；正式内容 Info 资产必须有唯一主 Group。详细强制规则见 `项目最高警告_P0_Info必须对应Group_Pack非默认聚合_AI协作警告.md`。
- 承载上述 Info 的 `SoDataGroup<TInfo>` 是可被 Consumer 直接收集的标准启动聚合根，必须直接实现 `IGameCoreSO`。
- `SoDataPack<TInfo>` 只有在通过其专门设计的准入和验收后才能作为 Consumer 启动根；它不是新领域的默认容器，也不是 ResourcePlan 或发布包。

Info 是最小领域定义；Group 是默认聚合启动根。Pack 是额外、显式验证的可选聚合，不得与 Group 等价看待。

## Group 的强制实现方式与 Pack 的条件边界

`SoDataGroup<TInfo>` 的抽象基类直接实现 `IGameCoreSO`，不允许每个具体 Group 重复手写一套。现有 `SoDataPack<TInfo>` 若保留 GameCore 转发能力，也必须由抽象基类统一实现，但这不构成新 Pack 的授权。

```text
Group.InjectGameCoreTables()
  → 遍历 Infos.Values
  → 若当前 Info is IGameCoreSO，直接调用 InjectGameCoreTables()
  → 否则跳过

Pack.InjectGameCoreTables()（仅既有或已获准的 Pack）
  → 遍历 Infos.Values
  → 若当前 Info is IGameCoreSO，直接调用 InjectGameCoreTables()
  → 否则跳过
```

Group 仅负责转发与聚合，不创建第二套 Key、不复制内容、不自行猜测表类别。Pack 还必须满足其专门成员权威和 Consumer 归属契约；当前泛型 Pack 不得被当作资源、发布或生命周期系统。Info 是否是 GameCore 是可选的；非 GameCore Info 正常保留，不注入即可。

严禁使用反射查找 `InjectGameCoreTables`、按类型名猜测注入资格，或为了统一调用强迫所有 Info 实现接口。只允许 C# 接口类型判断：`info is IGameCoreSO gameCore`。

## 新 GameCore 类别的固定扩展边界

新增类别必须在自己的领域目录内新增 Key、RuntimeData、强类型 Table 与 Info；Info 的 `InjectGameCoreTables()` 直接写入枚举选中的领域 Table。根 SO 类型不是 GameCore 类别本身：一个根 SO 类型可以通过稳定、显式的领域枚举，分流到多个兼容强类型 Table。

```text
<Category>RuntimeData : ESGameCoreRuntimeData
<Category>RuntimeTable : ESGameCoreConfigKeyTable<<Category>RuntimeData>
<Domain>DataInfo : SoDataInfo, IGameCoreSO
  → InjectGameCoreTables()
  → ExplicitDomainKind
  → selected <Category>RuntimeTable.AcquireRetained(...)
  → prepare in try
  → CommitRetained(...) / AbandonRetained(...)
```

不得为新类别修改 `0_Stand`，不得向 `ESRuntimeDataModule` 增加 `InjectGameCoreRoot(...)` 重载、switch 或类型注册表。若旧文档或旧分支仍出现中央重载，只能视为过时设计，禁止恢复。

不同 GameCore 类别严禁继承彼此的 `*DataInfo`。Monster 与 NPC 这类即使字段相似，也必须是独立 `SoDataInfo` 根类型；若确有稳定共享字段，只允许使用可序列化组合数据，不允许以继承伪造类别关系。

启动期接口转发只发生一次，不处于 Update/战斗/查表热路径；运行期必须直接使用新类别自己的 `ESGameCoreConfigKeyTable<T>`，不得走 `Type`、字符串类别或反射分发。GameCore 定义外壳稳定驻留且不得池化；运行实例按各领域独立池化。

## Shared / Variable 数据类型边界

`*SharedData` 是多个运行实例共同读取的定义对象，必须是 `[Serializable] class`，由 RuntimeData/Table 持有同一引用；运行期不得修改它。

`*VariableData` 是单个运行实例的可变状态模板。仅当其全部字段都是值类型时允许使用 `struct`；一旦包含任何引用字段，必须提供显式深拷贝，不允许依赖 struct 的浅拷贝。

## 严禁实现 IGameCoreSO 的对象

- `ESSkillConfigKey`、`ESWeaponConfigKey`、`ESShotConfigKey`、`ESBuffConfigKey`、`ESMonsterConfigKey`、`ESNpcConfigKey` 等 Key 值对象。
- `ESSkillRuntimeData`、`ESWeaponRuntimeData` 等运行时 DTO。
- `BuffSharedData`、`ItemWeaponSharedData`、`ItemShotSharedData`、`EntityMotionSharedData` 等嵌套共享/变量数据。
- Prefab、普通 AssetTable 资产、Library Catalog 记录。

Key 只负责业务寻址；RuntimeData 只负责运行时承载；嵌套数据只属于根 SO 的内容。它们都不能独立进入 Consumer 启动核心包，更不能自行注入全局表。

## 注入规则

1. 每个根 SO 必须校验自身 Key 有效、显式枚举类别与表匹配、目标表无冲突后再注入；一个资产实例只写入其当前枚举选中的分支。
2. 同一个 Info、Group 或 Pack 不得归属多个 Consumer 启动核心包。
3. 一个 Item 根 SO 若按 `ItemKind` 映射到 Weapon 或 Shot，只向该实际类别的表注入；禁止同一条数据盲目写入多个表。
4. GameCore 注入不得依赖 AssetBundle 名、路径、GUID 或 RuntimeKey；如需内容资产，只保存稳定的类型化 Asset Key，不直接保存 Prefab/GameObject/场景内容引用。
5. `ESAssetLibrary` 是编辑器组织工具；运行时启动仅认 Consumer 已收集的 `IGameCoreSO` 与其注入后的 GameCoreTable。
6. 根 SO 在 `AcquireRetained` 后复制 SO、SharedData、VariableData、Prefab 等载荷时，全部操作必须位于内层 `try`；异常时必须先 `Table.AbandonRetained(data)` 再抛出。
7. 成功必须通过 `Table.CommitRetained`；禁止直接 `RegisterAndGetRuntimeKey`、Upsert、创建新 RuntimeData 覆盖稳定 Key，或手工控制 `Ready/runtimeKey`。
8. RuntimeData 必须实现完整 `ReleaseRuntimePayload`；Consumer 清表后旧引用保持存在，但必须 `Ready=false` 且不再强引用重量级载荷。

## P0：SoDataInfo.KeyName 不是运行时 GameCore Key

`SoDataInfo.KeyName` 只用于编辑器数据组字典、策划识别、SO 表格与资源整理。它不是 GameCore 业务身份，不得影响运行时寻址。

权威等式：

```text
SoDataInfo.KeyName
    = 数据组字典键
    = 策划命名
    = SO 表格主列/合并定位
    = 编辑器搜索与定位信息
    != GameCore ConfigKey
    != RuntimeKey
    != 存档身份
    != 网络协议身份
    != 资源 GUID/LocalFileId
```

即使 `KeyName` 因 Unity 序列化而存在于运行时对象中，它也只能被视为来源元数据；存在字段不等于拥有运行时身份权威。

强制规则：

- GameCore 根必须在自己的 `ESGameCoreConfigKey<TEnumKey>` 中显式配置 `enumKey` 或 `stringKey`。
- `ESConfigKeyTable` 的 GameCore `Bake/Register/Upsert` 禁止接收 `KeyName` 作为 fallback。
- Picker、Consumer 校验、重复 Key 校验和 RuntimeData 构建都只认显式 `enumKey/stringKey`。
- `KeyName` 可以用于编辑器错误定位和策划显示，但不得生成 RuntimeKey、不得补全 StringKey、不得参与运行时查表。
- RuntimeData 若保留 `keyName` 调试字段，其内容必须由显式 ConfigKey 生成，不能复制 `SoDataInfo.KeyName`。
- 运行时初始化不得因为 `KeyName` 为空而失败，也不得因为 `KeyName` 相同而判定 GameCore 冲突。
- 修改或重命名 `KeyName` 不得改变任何运行时查询结果、存档解析结果或网络同步结果。
- Group/Pack 可以使用 `KeyName` 维护编辑期字典，但注入 GameCoreTable 时只能读取每个 Info 自己的显式 ConfigKey。

允许用途：

| 场景 | 是否允许使用 KeyName |
|---|---:|
| SODataGroup/Pack 编辑器字典 | 允许 |
| 策划表格导入、导出、合并 | 允许 |
| Inspector 展示、搜索、定位 | 允许 |
| 编辑器错误日志辅助定位 | 允许 |
| 生成 RuntimeKey | 禁止 |
| ConfigKey 的 StringKey fallback | 禁止 |
| 运行时查表、存档、网络协议 | 禁止 |
| GUID、LocalFileId、AssetBundle 身份 | 禁止 |

错误示例：

```csharp
Table.Bake(configKey, info.KeyName);
Table.Upsert(configKey, data, info.KeyName); // 若第三参数被解释为 fallback
```

正确示例：

```csharp
if (!configKey.IsConfigured) throw ...;
TData data = Table.AcquireRetained(configKey);
try
{
    CopyPayloadFromRootSo(data);
    Table.CommitRetained(configKey, data, debugName: info.name);
}
catch
{
    Table.AbandonRetained(data);
    throw;
}
```

这里的 `RuntimeKey` 只用于当前类型表、当前构建生命周期内的热路径加速。它允许在清表重建后变化，
禁止写入存档、网络协议或作为跨版本业务身份。必须保证的只是：注册完成后的当前表槽位与
`RuntimeData.runtimeKey` 在同一生命周期内一致。

验收要求：

1. 将任意 GameCore Info 的 `KeyName` 改名，不得改变显式 ConfigKey 对应的 RuntimeKey。
2. 两个 Info 的 `KeyName` 相同，只要显式 ConfigKey 不同，就不得产生运行时冲突。
3. 两个 Info 的显式 ConfigKey 相同，即使 `KeyName` 不同，也必须明确报重复 Key。
4. `KeyName` 非空但显式 ConfigKey 为空时，必须判定 GameCore 配置无效，不得回退加载。

## AI 执行前强制检查

改动任何 Skill / Buff / Actor / Item GameCore 定义前，必须先确认：

```text
该对象是否为独立 ScriptableObject 根资产？
该对象是否有稳定业务 Key？
它是否需要由显式领域枚举分流？当前资产实例注入哪个 Table？
它是否会被 Consumer 启动包收集？
```

任一答案不明确时，不得把 `IGameCoreSO` 加到 Key、RuntimeData 或嵌套数据上。

修改 RuntimeData 或注入事务前，还必须读取：

```text
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCoreRuntimeData稳定驻留与事务注入_AI协作警告.md
```
