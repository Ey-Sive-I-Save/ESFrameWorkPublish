# 项目最高警告：GameCore 根 SO 注入边界

## 不可违反的事实

`IGameCoreSO` 的唯一职责是把一个**独立、可启动加载的 GameCore 根 ScriptableObject**注入目标 `GameCoreTable`：

```csharp
public interface IGameCoreSO
{
    void InjectGameCoreTables();
}
```

Consumer 启动核心包只允许收集并加载实现该接口的根 SO。运行时会直接调用 `InjectGameCoreTables()`；未实现接口的对象进入该包必须视为构建错误。

## 必须实现 IGameCoreSO 的对象

- `SkillDefinitionDataInfo`：注入 SkillTable。
- `BuffDefinitionDataInfo`：注入 BuffTable。
- `ActorDataInfo`：按 `ActorDataKind` 注入 NPC / Monster 等对应 Actor Table。
- `ItemDataInfo`：按 `ItemKind` 注入 Weapon / Shot 等对应 Item 领域 Table。
- 所有承载上述 Info 的 `SoDataGroup<TInfo>` 与 `SoDataPack<TInfo>`：它们也是可被 Consumer 直接收集的启动根 SO，必须直接实现 `IGameCoreSO`。

Info 是最小领域定义；Group/Pack 是聚合启动根。两者都是独立资产、可被 Consumer 作为启动 GameCore 直接加载的根定义。

## Group / Pack 的强制实现方式

`SoDataGroup<TInfo>` 与 `SoDataPack<TInfo>` 的**抽象基类**直接实现 `IGameCoreSO`，不允许每个具体 Group/Pack 重复手写一套。

```text
Group.InjectGameCoreTables()
  → 遍历 Infos.Values
  → 若当前 Info is IGameCoreSO，直接调用 InjectGameCoreTables()
  → 否则跳过

Pack.InjectGameCoreTables()
  → 遍历 Infos.Values
  → 若当前 Info is IGameCoreSO，直接调用 InjectGameCoreTables()
  → 否则跳过
```

Group/Pack 仅负责转发与聚合，不创建第二套 Key、不复制内容、不自行猜测表类别。Info 是否是 GameCore 是可选的；非 GameCore Info 正常保留，不注入即可。

严禁使用反射查找 `InjectGameCoreTables`、按类型名猜测注入资格，或为了统一调用强迫所有 Info 实现接口。只允许 C# 接口类型判断：`info is IGameCoreSO gameCore`。

## 新 GameCore 类别的固定扩展边界

新增类别必须在自己的领域目录内新增 Key、RuntimeData、强类型 Table 与 Info；Info 的 `InjectGameCoreTables()` 直接写入自己的领域 Table。

```text
<NewCategory>DataInfo : SoDataInfo, IGameCoreSO
  → InjectGameCoreTables()
  → <NewCategory>RuntimeTable.Register(...)
```

不得为新类别修改 `0_Stand`，不得向 `ESRuntimeDataModule.InjectGameCoreRoot(...)` 增加重载、switch 或类型注册表。现有中央重载仅是旧类别过渡代码，不是新类别模板。

不同 GameCore 类别严禁继承彼此的 `*DataInfo`。Monster 与 NPC 这类即使字段相似，也必须是独立 `SoDataInfo` 根类型；若确有稳定共享字段，只允许使用可序列化组合数据，不允许以继承伪造类别关系。

启动期接口转发只发生一次，不处于 Update/战斗/查表热路径；运行期必须直接使用新类别自己的 `ESConfigKeyTable<T>`，不得走 `Type`、字符串类别或反射分发。

## 严禁实现 IGameCoreSO 的对象

- `ESSkillConfigKey`、`ESWeaponConfigKey`、`ESShotConfigKey`、`ESBuffConfigKey`、`ESMonsterConfigKey`、`ESNpcConfigKey` 等 Key 值对象。
- `ESSkillRuntimeData`、`ESWeaponRuntimeData` 等运行时 DTO。
- `BuffSharedData`、`ItemWeaponSharedData`、`ItemShotSharedData`、`EntityMotionSharedData` 等嵌套共享/变量数据。
- Prefab、普通 AssetTable 资产、Library Catalog 记录。

Key 只负责业务寻址；RuntimeData 只负责运行时承载；嵌套数据只属于根 SO 的内容。它们都不能独立进入 Consumer 启动核心包，更不能自行注入全局表。

## 注入规则

1. 每个根 SO 必须校验自身 Key 有效、类别与表匹配、目标表无冲突后再注入。
2. 同一个 Info、Group 或 Pack 不得归属多个 Consumer 启动核心包。
3. 一个 Item 根 SO 若按 `ItemKind` 映射到 Weapon 或 Shot，只向该实际类别的表注入；禁止同一条数据盲目写入多个表。
4. GameCore 注入不得依赖 AssetBundle 名、路径、GUID 或 RuntimeKey；如需 Unity 资产，只保存类型化 Asset Key。
5. `ESAssetLibrary` 是编辑器组织工具；运行时启动仅认 Consumer 已收集的 `IGameCoreSO` 与其注入后的 GameCoreTable。

## AI 执行前强制检查

改动任何 Skill / Buff / Actor / Item GameCore 定义前，必须先确认：

```text
该对象是否为独立 ScriptableObject 根资产？
该对象是否有稳定业务 Key？
它注入的唯一目标 Table 是什么？
它是否会被 Consumer 启动包收集？
```

任一答案不明确时，不得把 `IGameCoreSO` 加到 Key、RuntimeData 或嵌套数据上。
