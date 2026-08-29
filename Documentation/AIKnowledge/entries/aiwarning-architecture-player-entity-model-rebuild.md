# 玩家对象模型重构边界

`KnowledgeId`: `es.aiwarning.arch.player-entity-model-rebuild.v1`  
`Authority`: `AIWarnings + current Entity/Domain/Module source`  
`RouteKeys`: `aiwarnings`, `architecture`, `entity`, `player`, `domain`, `input`, `equipment`, `kcc`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `64a12239fc871912e2df1d921c7a203d42d77f1b42e3b1e01ff109830387e611`  
`SourceSetHash`: `64a12239fc871912e2df1d921c7a203d42d77f1b42e3b1e01ff109830387e611`  
`EntryBodyHash`: `939066566d80661e5f59d27d62ad4f110f021c99d17c6884a0154ab78262497e`  
`StaleWhen`: Entity 五域、输入链、ESGameManager 入口、KCC/模块表或相关 SourceRef 变化。

## 迁移范围与当前事实

原 Warning 89 行、7,682 UTF-8 字节；现 Warning 保留通用 Entity、五域职责、输入意图边界、KCC 高频路径和禁止新增平行控制器等长期约束。本条目承接重构方向、当前源码形状、风险热点、迁移规则和入口路径。

- 当前入口是 `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`；`Entity` 继承 `Core` 并实现 KCC 控制器，直接注册 Basic、AI、Buff、Equipment、State 五个序列化 Domain。
- 框架链为 `Core → Domain → Module`，`Core.Update` 更新 Domain，Domain 更新 Module，模块通过 `Core.ModuleTables` 类型键暴露；不得改变既有键语义。
- 输入主链为 `ESInputModule → ESInputService → EntityPlayerInputWriteModule → EntityAIDomain`，`inputState` 是 Awake 创建的运行态；Equipment 拥有库存/槽位/附件/装备效果，Basic 执行身体与战斗行为。
- `Entity.cs`、Basic/AI 大文件和 KCC 回调是高风险区域；禁止在高频回调引入 LINQ、反射、层级搜索或字符串工作。`Assets/Scripts/ESPlayer` 是空壳/历史材料，不是玩家实现入口。
- 不恢复已删除旧输入模块，不新增 PlayerActor/CharacterActor 或第二套输入/运动/战斗/装备控制器；新能力应兼容本地、AI、编辑器预览及未来回放/网络输入。

## EvidenceRefs

### evidence

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/玩家对象模型重构_AI协作说明.md` (`4398333388e4f0dff371e014b5f2227126fe3fd2993d3b135da1e20baa76f1de`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs` (`397d0c465f6b59069e388445d6d5724d190a0d08ec3d1719bfdfc9c6a1418c46`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs` (`1d2a4bd6f45cfc7841b6a0c226798370d85684fd92fc1303df70334b409a76f1`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs` (`081ce09d5ffa2bc24a58cd44babff349745b7e840a394290f046f1d43b241d6a`)
