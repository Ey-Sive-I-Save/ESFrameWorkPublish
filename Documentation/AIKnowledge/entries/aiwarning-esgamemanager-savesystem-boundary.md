# ESGameManager 与 SaveSystem 架构边界

`KnowledgeId`: `es.aiwarning.esgamemanager-savesystem-boundary.v1`  
`Authority`: `AIWarnings + current GameManager/SaveSystem source`  
`RouteKeys`: `aiwarnings`, `architecture`, `gamemanager`, `domain`, `save`, `load`, `module`, `inspector`, `command`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `13077ee77b9e0f25379f36a014b0508ee422b27d4fa8831e44036f9cec845cb0`  
`SourceSetHash`: `13077ee77b9e0f25379f36a014b0508ee422b27d4fa8831e44036f9cec845cb0`  
`EntryBodyHash`: `a75ff488b7fb41685b485d0b226382d97c2d59f580d61d7671f447bac68dd5fc`  
`StaleWhen`: `ESGameManager 三域、ESGameSave 门面、SaveSystem、模块 Drawer、Command 目录或 SourceRef 哈希变化。`

## 迁移说明

原 Warning 339 行、10,451 UTF-8 字节；现 Warning 保留唯一 GameManager、三域归属、SaveSystem 入口、Inspector 轻量绘制、Command 文件名和 UTF-8 边界。本条目承接详细架构事实、保存语义、未来 Link 分层和旧结构禁令。

## GameManager 现行结构

- 唯一入口是 `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`；禁止恢复 `Plugins/ES/2_Feature/ESGameCore`、`ES_GameCore.asmdef/csproj` 或旧中转程序集。
- 顶层只有 `ESSystemDomain`、`ESFlowDomain`、`ESWorldDomain`：系统域提供稳定能力，流程域决定当前阶段/开关，世界域描述当前世界实例。不得恢复 GlobalDomain、GameRunDomain 或旧四域玩家/表现拆分。
- `ESGameSaveModule`、`ESInputModule` 属系统域；`ESCommandModule` 经 `ESRuntimeModule` 属流程域。RuntimeMode 是 GameManager 流程状态服务，不能因 Input 读取它就改变 Input 归属。
- Feature 只提供可复用能力，不拥有全局生命周期、主流程、保存/输入/状态机主流程或具体角色/关卡业务。顶层 Domain 必须少而清晰，放不下时先拆混合职责而非随意增域。

## SaveSystem 现行语义

- 保存系统位于 `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/`，公开门面为 `ESGameSave.Set/Get/Save/Load/Has/Delete/Info`；不得恢复 `ESGameManager.SaveModule` 或 `GetModuleFast<T>`。
- `Get/Has/Save/Delete/Info` 只查询既有模块；`Set/Load` 才通过 EnsureModule 明确允许初始化。`Set/Get` 是内存缓存，`Save/Load` 是磁盘操作；缓存保存 Json 字符串快照，不持有业务对象引用。
- 文件写入保持临时文件、写后读取校验、旧文件备份、替换正式文件和失败清理/保留策略。Easy Save 3 只是底层能力，业务不得散写 ES3 key。
- 未来 Link 设计中 SaveModule 仍拥有 Archive、缓存、磁盘、版本、加密压缩；业务经 Link 提交数据，Load 读入内存，Apply 按 Config/World/Player/Inventory/Quest/Runtime/Presentation 阶段恢复。阶段不是磁盘读取顺序。

## Inspector 与命名边界

- GameManager 主 Inspector 只显示模块名、归属域、启用状态、轻量详情和弹窗入口；不得递归绘制大型 Odin 模块、反射扫描字段或每次展开造成宽度/性能失控。完整编辑走 `ESGameModuleCompactDrawers` 弹窗。
- Unity MonoBehaviour 文件名必须与类名一致；`ESCommandPlayer.cs` 及 `.meta` 保持一致，Command 主干位于 `Assets/Scripts/ESLogic/Runtime/Command/`，禁止恢复 `SERVICE_` 等前缀文件名或 `Features/ESCommandPlay` 旧目录。
- 中文 C#、Markdown、Asset 修改必须使用严格 UTF-8；乱码不得继续扩散。静态编译和旧结构搜索可验证结构面，但不能证明 Unity/运行时/发布。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem`
- `Assets/Scripts/ESLogic/Editor/Drawers/ESGameModuleCompactDrawers.cs`
- `Assets/Scripts/ESLogic/Runtime/Command/Components/ESCommandPlayer.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/GameManager与存档（GameManagerSave）/架构体系_ESGameManager_SaveSystem_AI协作警告.md` (`5985dece2a14e5c9c6fe9ce66e42a01f43da242b1a70654c7d428b2f4ff69554`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs` (`081ce09d5ffa2bc24a58cd44babff349745b7e840a394290f046f1d43b241d6a`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/ESGameSave.cs` (`725f3c1ccbac9d433cb798fe1f90b2ef0a22a093ed3c37ccaafdd181ccd56f6a`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/ESGameSaveModule.cs` (`189e7c1342798f3163b166f80c5f9fa9f81e738b7646d41e7049f69aeafd1e60`)
- `Assets/Scripts/ESLogic/Runtime/Command/Components/ESCommandPlayer.cs` (`f1f4aa07b76a96160b157958bd5febeb8bfe6cc8e9c77fb779ce602e86c1b1db`)
