# P0 GameCore 根 SO 注入边界

`KnowledgeId`: `es.aiwarning.p0.gamecore-root-so-injection-boundary.v1`  
`Authority`: `AIWarnings + current GameCore source`  
`RouteKeys`: `aiwarnings`, `p0`, `gamecore`, `root-so`, `injection`, `key`, `dependency`, `resource-boundary`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `1b4462f3f693ea32cde1e88a0872c51cb0cc7e8c172fa620bffc15dd7bae5256`  
`SourceSetHash`: `1b4462f3f693ea32cde1e88a0872c51cb0cc7e8c172fa620bffc15dd7bae5256`  
`EntryBodyHash`: `9119bd42ceff7c8b91dd1c8de33dae451482e9b8e6b89095e0f0b59d2619f9c4`  
`StaleWhen`: `IGameCoreSO、GameCoreTable、ConfigKey、Consumer 收集、Group/Pack 注入或任一 SourceRef 哈希变化。`

## 迁移说明

原 Warning 234 行、14,663 UTF-8 字节；现 Warning 保留 P0 依赖方向、接口/Consumer 边界、显式 Key、事务注入和权限边界。本条目承接详细规则、源码证据、历史语义和迁移前结构，避免把长篇设计重新塞回 Warning。

## 当前事实与长期规则

- GameCore 是启动阶段建立并稳定可用的定义层。Prefab、场景对象、普通 SO 和业务配置可以引用根 SO；根 SO、可序列化嵌套配置和 RuntimeData 不得直接引用 GameObject、Component、Prefab 或场景对象。
- 专用 Entity Prefab 可以直接保存 Monster/Npc/Actor DataInfo 定义引用；通用池模板可在租出后调用 `Entity.BindDefinition`。Prefab 不应复制定义内容、固有 Tag 或第二份权威字段。
- 根 SO 表达资源需求时使用稳定类型化 Asset Key/ESAssetRefer，由 ResourcePlan、AssetTable 或对应资源系统解析。`IGameCoreSO` 表示 Consumer 的启动 Provider，不表示所有引用者都被禁止。
- `IGameCoreSO.InjectGameCoreTables()` 只属于独立、可启动加载的根 ScriptableObject。Consumer 只收集实现接口的根 SO；非 SO 数据可由各领域强类型 Table 的 `InjectWith/TryInjectWith` 直接注入，不得伪装为根 SO。
- `InjectWith` 是严格入口，冲突/失败抛出；`TryInjectWith` 返回 false。调用方不直接管理 RuntimeData，不得增加中央类别 switch、反射工厂或全局注册表。
- `SoDataGroup<TInfo>` 是默认启动聚合根，只做受约束的接口转发；`SoDataPack<TInfo>` 只有既有或明确验收通过的准入才能作为启动根，不能默认视作 ResourcePlan、Manifest、发布包或生命周期系统。
- 每个 Info 使用显式领域 ConfigKey；`KeyName` 只用于编辑器字典、表格和错误定位，不能回退生成 ConfigKey、RuntimeKey、存档、网络或资源身份。改名不应改变运行时寻址，显式 Key 冲突必须报错。
- 新类别在自己的领域目录声明 Key、RuntimeData、强类型 Table 和 Info；注入按显式枚举分流，使用 Acquire/准备/Commit 或 Abandon 事务。不得为新类别修改 `0_Stand` 或向 `ESRuntimeDataModule` 增加中央注入重载。
- SharedData 是运行实例共同读取且不可变的定义引用；VariableData 只有全值类型才可用 struct，含引用字段必须显式深拷贝。根定义外壳稳定驻留，不池化；失败不得遗留半提交或重量级引用。

## 迁移前语义快照

迁移前 Warning 的完整文本已经由本条目的 SourceRef 指向原路径并由迁移台账记录原始行数、字节数和哈希。不可丢失的判定集合是：依赖只能外向内；接口只给根 SO；Group/Pack 不复制内容；KeyName 不是运行时 Key；每个投影独立 Key、Table、验证和回滚；Prefab/GameObject/场景不能进入根 SO；RuntimeData 必须通过领域表事务注入并释放载荷。

## EvidenceRefs

- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESScriptableObjectClassification.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/1-SoDataGroup.cs`
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/2-SoDataPack.cs`
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）/项目最高警告_GameCore根SO注入边界_禁止Key与嵌套数据伪装核心_AI协作警告.md` (`156190de624ca1df4cdbdbebc41076ecef47b0cc8da7f83f0624537db7c588a7`)
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESScriptableObjectClassification.cs` (`93381a850908fa8c6025196170513dd1771faa8b86b3c34ebf277d1794dbbc93`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/1-SoDataGroup.cs` (`7e39c3e21dc67354fa174886398ee3f4fc3ec66e70903a7049f4c5a3f70f5cb9`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/2-SoDataPack.cs` (`b07d4dfb9f53dfd0ea3b36e6c9d0e9a00acca34954d30e1315d2f189d846205c`)
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs` (`08c4fda0e5ec09db552834ff2137314aec6244709ea7d40c9c0e276a9987c33e`)
