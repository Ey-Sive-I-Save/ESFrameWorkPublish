# 项目最高警告：P0 - Info 必须对应 Group，Pack 不是默认聚合

> 状态：现行约束。
> 级别：P0。
> 适用范围：所有 `SoDataInfo`、`SoDataGroup<TInfo>`、`SoDataPack<TInfo>`、GameCore Consumer、SO 表格、内容库与新增领域配置。
> 最后验证：2026-08-02。已核对 `SoDataInfo`、`SoDataGroup<TInfo>`、`SoDataPack<TInfo>` 的当前实现；本规则不替代 Unity 内容迁移验收。
> 适用源码入口：`Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/0-SoDataInfo.cs`、`1-SoDataGroup.cs`、`2-SoDataPack.cs`；音频示例为 `Assets/Scripts/ESLogic/Runtime/Data/For_Info/GroupType/ESAudioCueGroup.cs`。
> 前置阅读：`项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md`。

## 最高结论

`SoDataInfo` 是单条内容定义；`SoDataGroup<TInfo>` 是该内容类型的标准编辑器组织和启动聚合根。二者必须成对设计。

```text
具体 Info 类型 TInfo
  <=> 对应具体 TInfoGroup : SoDataGroup<TInfo>

正式内容 Info 资产
  -> 恰有一个主 Group
  -> Group 负责编辑器组织和启动聚合
  -> Info 自己的显式 ConfigKey 负责运行时身份
```

`SoDataPack<TInfo>` 不是 Group 的同义词，不是新领域的默认容器，不是资源包、发布包、ResourcePlan 或运行时生命周期所有者。没有通过本规则定义的额外契约前，禁止新增业务对 Pack 的依赖。

## 默认路径与合理例外

`SoDataGroup` 是同类可枚举内容的首选聚合入口。`ActionTemplate`、`AudioCue`、武器、Buff、角色定义、`Presentation Mapping` 等会持续新增、多条配置共存、需要 Editor 列表管理或批量注入的 `SoDataInfo`，应优先通过对应 Group 进入 GameCore。

单例全局设置、明确由 GameCore Root 字段唯一持有的配置、场景绑定对象和纯运行时状态可以不使用 Group，但必须有清晰且可验证的显式根入口。

Builder 创建可枚举内容时，应优先创建或更新对应 Group；未能接入 Group 时必须明确标记为“孤立候选资产”，不能宣称已进入运行时 Table。

## P0：Info 类型闭包与资产归属

### 类型闭包

每新增一个具体 `SoDataInfo` 类型，必须在同一交付中新增对应的具体 Group 类型：

```csharp
public sealed class TInfo : SoDataInfo { }

public sealed class TInfoGroup : SoDataGroup<TInfo> { }
```

具体 Group 必须：

1. 使用 `SoDataGroup<TInfo>`，不得自行复制 `Infos` 字典、注入循环或表格接线。
2. 具有 `ESCreatePath`，能从 ES 标准 SO 创建路径建立内容库。
3. 放在领域的 `GroupType` 目录或同等明确的领域目录，不能藏在播放模块、业务组件或 Editor 临时目录。
4. 只容纳一个明确的 Info 类型；禁止 `SoDataGroup<ScriptableObject>`、字符串类型筛选或混合领域万能 Group。
5. 若 `TInfo` 是 GameCore，复用基类的 `IGameCoreSO` 转发；不得另建 Group RuntimeTable、第二套 Key 或反射注入。

`ESAudioCueInfo` 的对应类型是 `ESAudioCueGroup : SoDataGroup<ESAudioCueInfo>`。这是所有新 Info 类别应遵循的完整结构，不是音频特例。

### 资产归属

每个正式内容 Info 资产必须有且只有一个主 Group。主 Group 是策划编辑、SO 表格导入、Consumer 启动收集和错误定位的权威组织入口。

- 需要按标签、分类或搜索结果再次展示时，使用查询、只读索引或 Editor 视图；不得把同一 Info 复制进多个可编辑 Group 制造双权威。
- `SoDataInfo.KeyName` 仅是主 Group 字典、表格和编辑器定位键。它不是 ConfigKey、RuntimeKey、存档、网络或资源身份。
- `ESGameCoreConfigKey`、`ESAssetConfigKey` 等显式强类型 Key 仍是运行时业务身份；Group 改名、移动 Group、调整 `KeyName` 不得改变运行时查询结果。
- 现有未归属 Group 的 Info 是架构债务，不得作为新类别或新内容的先例。迁移应先创建匹配 Group、明确主归属，再逐步移动资产和 Consumer 收集入口。

纯框架全局设置、编辑器状态或不属于内容库的单例不应伪装成 `SoDataInfo` 以逃避这条规则；若确有既有 `SoDataInfo` 单例例外，必须在类型旁和验收记录中说明原因、生命周期和不设 Group 的替代组织方式。没有书面例外即按 P0 缺陷处理。

## Group 的职责与收益

Group 解决的是内容组织和启动聚合，不是运行时播放或资源生命周期。

```text
策划创建/修改 TInfo
  -> 进入 TInfoGroup
  -> SO 表格、Picker、内容库按 Group 组织
  -> Consumer 加载 Group
  -> Group 只转发组内 IGameCoreSO.Info 的注入
  -> RuntimeTable 仅按 Info 的显式 ConfigKey 建表
```

采用标准 Group 的收益：

- 内容人员拥有可见、可检查、可迁移的领域入口，不需要在 Project 全局搜索散落 Info。
- SO 表格导入导出能够按 Group 选择目标，避免将未知资产混入错误领域。
- Consumer 的启动边界清楚：加载哪个 Group，就聚合其哪些定义；不会靠全盘扫描或类型名猜测内容集合。
- Group 字典能在编辑期发现重复组织键；RuntimeTable 继续独立发现重复 ConfigKey，两层错误不会互相掩盖。
- 玩法、资源计划和运行时只引用 Info 的强类型 Key，不因 Group 的调整产生业务迁移。
- 一个领域可以有多个 Group 资产，例如按章节、地图或内容责任划分；这是多个库，不是多个 Info 身份。

Group 不得承担的职责：

- 不播放音频、不持有 Voice、不管理 `ESAssetScope`。
- 不作为 ResourcePlan、AssetBundle、热更新 Payload 或下载包。
- 不复制 Info 内容、不缓存第二套 RuntimeData、不根据 `KeyName` 补全 ConfigKey。
- 不因为“方便检索”而加入跨领域 Info、Prefab、场景对象或 Unity 资源直接引用。

## Pack 的现状、价值与风险

当前通用 `SoDataPack<TInfo>` 的技术行为是：引用若干 Group，并把其 Info 平铺到自己的字典后转发 `IGameCoreSO` 注入。它可用于旧编辑器工作流的聚合视图，但尚不足以成为框架中的默认内容包模型。

它可能有价值的场景：

- 编辑器侧临时汇总多个同类型 Group，辅助表格导入、审阅或批量检查。
- 明确定义的只读发布清单，在拥有独立版本、成员快照、差异校验和 Consumer 归属规则后，作为一个新的专用类型实现。

当前泛型 Pack 的主要风险：

- 平铺 `Infos` 与 Group 字典构成两份成员索引，移动、删除、重命名或重复加入后容易陈旧或产生冲突。
- 它使用 `SoDataInfo.KeyName` 作为编辑器组织键；一旦被误当作 ConfigKey 或发布身份，会破坏稳定身份边界。
- 同一 Info、Group 或 Pack 被多个 Consumer 收集时，缺少“唯一 Consumer 启动归属”的显式验证，容易造成重复注入、启动包膨胀和发布边界不清。
- 它不表达 AssetTable、Manifest、Bundle、Bank、下载、校验、Scope、引用计数、资源释放或热更新回退；因此绝不是资源发布包或 ResourcePlan 的替代物。
- `enableAutoRefresh`、缓存 Group 与平铺字典的权威关系没有形成完整、可验证的同步协议；不能据此推断成员集合始终正确。
- Pack 名称容易误导内容人员把“编辑器集合”“GameCore 启动根”“资源发布包”混为一件事，最终把内容组织、资源生命周期和发布职责耦合在同一个 SO。

因此，当前规则是：

```text
新内容领域：必须先有 Info + Group
跨 Group 的编辑器检索：优先查询/只读索引
区域/剧情资源预热：使用 ResourcePlan
资源发布、下载、回退：使用 Manifest / Release Payload
新增 SoDataPack 业务依赖：冻结，先完成专门设计和验收
```

## Pack 解冻的准入条件

若项目确实需要 Pack，不能直接复用当前泛型类并扩大使用。必须先以专门设计回答并实现以下问题：

1. Pack 的唯一职责是什么：编辑器视图、Consumer 启动根、发布清单或其他？一个 Pack 只能有一个主职责。
2. 谁拥有成员集合？成员是否快照、是否允许自动刷新、删除成员如何处理、冲突如何报告？
3. 同一 Info / Group / Pack 的 Consumer 归属如何验证为唯一，或如何显式声明共享并保证幂等？
4. Pack 的编辑器 `KeyName`、稳定 ConfigKey、资源 Manifest 身份和发布版本如何严格分层？
5. 它与 ResourcePlan、AssetScope、AssetTable、Release Manifest 的关系为何；哪些职责明确不属于它？
6. 成员变更、重复键、缺失资产、Consumer 重建、增量发布和回退分别有哪些自动化验证？
7. 是否需要新建专用 `GameCoreCollection`、`ReleasePayload` 或 Editor-only View 类型，而不是继续扩张 `SoDataPack<TInfo>`？

在这些问题有源码、验证和迁移方案前，Pack 只能维持现有存量用途，禁止作为新 GameCore、音频库、资源计划或发布流程的默认基础设施。

## 对音频与其他领域的落地要求

音频库的正确结构：

```text
ESAudioCueInfo                 单个 Cue 行为定义
ESAudioCueGroup                音频 Cue 内容库与启动聚合
ESAudioCueKey                  玩法/剧情/UI 的稳定引用
ESRuntimeDataGameCore.AudioCues 运行时强类型表
ESResourcePlanInfo.audioCues   区域/剧情预热入口
```

`ESAudioCueGroup` 不进入 `ESGameManager.Audio.Play...` 参数，不替代 CueKey，也不接管 Clip/Bank 资源。它只保证 Cue 有标准 ES 内容库入口并可由 Consumer 聚合。

任何新领域必须按同样顺序落地：`Info + Group -> 可选 GameCore Table -> 业务 Key -> ResourcePlan/资源系统（若需要）`。不要先引入 Pack 试图一次解决所有组织、启动和发布问题。

## 迁移与验收

### 迁移顺序

1. 清点所有具体 `SoDataInfo` 类型，列出是否已有匹配 Group、正式资产主归属和 Consumer 收集入口。
2. 为缺失的类型补充最小 `SoDataGroup<TInfo>`；不得在 Group 中复制 Info 字段或运行时逻辑。
3. 将新建和修改中的正式内容资产迁入唯一主 Group；旧资产按领域逐步迁移，避免一次性重写所有序列化引用。
4. 冻结新 Pack 使用；现有 Pack 先标注用途和 Consumer，再决定保留、替换为查询视图或按专门契约重构。
5. 迁移后验证 Group 注入只转发成员 Info，RuntimeTable 的身份仍只由显式 ConfigKey 决定。

### 验收清单

- 新增具体 `SoDataInfo` 的提交同时包含匹配 `SoDataGroup<TInfo>`、创建路径和 `.meta`。
- 至少存在一个可创建的 Group 资产，能添加该 Info 并维护编辑期组织键。
- Group 作为 GameCore Consumer 根时，成员 Info 正确进入目标强类型表；同一资产被重复转发时不产生第二份 RuntimeData。
- 改名 Group 或 `Info.KeyName` 后，显式 ConfigKey 和运行时查表结果不变。
- 资源预热仍走 ResourcePlan；不得以 Group 或 Pack 代替 Scope、Manifest 或发布 Payload。
- 新增 Pack 使用必须附带上述“解冻准入条件”的设计、迁移和自动化验证；否则拒绝合入。

违反“新增 Info 没有对应 Group”“正式 Info 没有主 Group”或“未完成专门设计就把 Pack 作为默认聚合”任一项，均按 P0 架构缺陷处理。
