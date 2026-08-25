# ESGameManager 与 Save 候选事务

`KnowledgeId`: `es.project.game-manager-save-transaction.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `game-manager`, `domain`, `module`, `runtime-mode`, `save`, `load`, `candidate`, `migration`, `rollback`  
`ContentHash`: `57e7a13d5ca6296638f1751bfbe3314426f53ec4a97634ecf1c97fa6485f8ca7`

## 唯一入口与三域

项目级入口是 ESLogic 下的 `ESGameManager`，顶层只有 System、Flow、World 三域。System 提供稳定能力，Flow 决定当前玩法阶段与能力开关，World 描述当前场景/地图实例。Domain 是生命周期、调度和 Inspector 的顶层边界，不是按文件名随意增加的分类。

`ESGameSaveModule`、Input 属于 System；Command 经 RuntimeModule/FlowModule 属于 Flow。RuntimeMode 是 GameManager 的流程状态服务，Input 只读取其结果，不因此改变模块归属。玩家与表现能力优先进入 Entity、UI、Camera、State 等具体系统，不恢复旧玩家域/表现域。

`TryGetModule<T>` 只查询，`GetOrCreateModule<T>` 才是初始化。当前 Save 门面只有 Set/Load 使用 `EnsureModule`；Get/Has/Save/Delete/Info 通过既有 Module 查询，不能因“顺手获取”改变运行时结构。

## Save 门面语义

公开门面是 `ESGameSave`，不经 `ESGameManager.SaveModule`。Set/Get 操作内存 Archive 的分区 JSON；Save/Load 才触及磁盘。Set、Load 允许 EnsureModule；Get、Has、Save、Delete、Info 只查询既有模块。Set 会序列化快照，不长期引用业务对象。

JSON.NET 的自动 `TypeNameHandling` 关闭；但分区包仍可能记录 `typeName` 元数据，不能把“关闭自动 CLR 类型反序列化”表述成“文件绝不出现 CLR 类型名”。多态必须使用明确 DTO、Converter 或分区版本迁移。ES3 只是 Archive 整包的加密/压缩存储后端，业务系统不能散写 ES3 key 绕开版本、报告和原子写入。

## 磁盘原子写入

Save 先通知 BeforeSave，整理 Archive metadata，再写临时文件；可选回读验证后备份旧主文件，删除主文件后再将临时文件重命名为主文件。异常时会尝试在主文件缺失时从备份恢复，但临时文件清理取决于具体分支；这里的“原子”只是实现的替换协议，不是已证明的 OS/文件系统原子耐久，必须通过故障注入验证。

Load 先读取整包 JSON，再按 archiveVersion 顺序执行 MigrationRule。缺规则默认在 `blockLoadWhenMigrationMissing=true` 时失败并保留报告；关闭该开关时源码允许放行旧版本，不能把它概括成无条件失败。迁移无进展、解析失败或版本不兼容仍应失败，而不是静默丢字段。

## 候选应用事务

磁盘读成功不等于运行时已切换。Module 建立 `ESGameSaveCandidate`，依次调用 Validate、Prepare，再按 Config、World、Player、Inventory、Quest、Runtime、Presentation 阶段 Commit。任一步失败时，按已提交调用的逆序和阶段执行 Rollback；成功后再 Finalize 并设置 currentCandidate。

参与系统应把候选数据、previous state 和 committed 标记保存在自己的 Prepared 记录中；当前框架只提供 participantData/CommittedCalls 容器，并不强制参与者实现 previous-state 保存或幂等。Validate 不改正式状态，Prepare 不提前对外可见，Commit 只处理自己的阶段，Rollback 可重复恢复是参与者合同而非已运行证明。WorldMap 等内容漂移检查需要对应参与者 SourceRef/回执，本条目当前不把它视为已闭合事实；当前也未见事务 ID、跨进程锁或重复 Load 的框架级去重。

`StaleWhen`: ESGameManager、Save/Load schema、Migration/Rollback 合同或任一 SourceRef 哈希变化。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/GameManager与存档（GameManagerSave）/架构体系_ESGameManager_SaveSystem_AI协作警告.md` (`910e8cd6a3af85ec96f3e38f796229047455aa22fb2e9660ffa97424cacf02d4`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs` (`081ce09d5ffa2bc24a58cd44babff349745b7e840a394290f046f1d43b241d6a`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.Domains.cs` (`5649698c989539429c89d7e52abfce8aed708d73f0137d4fc9c0762f2ec04ab0`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/ESGameSave.cs` (`725f3c1ccbac9d433cb798fe1f90b2ef0a22a093ed3c37ccaafdd181ccd56f6a`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/ESGameSaveModule.cs` (`189e7c1342798f3163b166f80c5f9fa9f81e738b7646d41e7049f69aeafd1e60`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/ESGameSaveTypes.cs` (`e25bc6ed9cdae1c95e2cd43af9e4baff98e4a83bc7048da5a50a97f4a1589f91`)
- `Assets/Plugins/ES/1_Design/Tests/ESGameManagerStaticModuleTests.cs` (`d0f88b5a91bf56e304dc2eeef9cfe286697aadf19e6a184bce2562ceae107717`)

`EvidenceLevel`: `S1`; `StaleWhen`: GameManager Domain、模块创建语义、Archive/Migration、磁盘替换或候选阶段协议变化。
