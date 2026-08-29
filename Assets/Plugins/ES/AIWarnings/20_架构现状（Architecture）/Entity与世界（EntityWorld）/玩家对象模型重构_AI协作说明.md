# Player Architecture Collaboration Notes

Status: current
StableId: es.aiwarning.arch.player-entity-model-rebuild
Authority: AIWarnings；当前 Entity/Domain/Module 源码为事实权威。
RouteKeys: aiwarnings, architecture, entity, player, domain, input, equipment, kcc
Applicability: 玩家/角色 Entity、五个序列化 Domain、输入、运动、战斗、装备、状态机、IK 与 KCC 协作。
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-architecture-player-entity-model-rebuild.md#evidence`
Owner: ES Entity/Player architecture owners。
StaleWhen: Entity 五域、输入链、ESGameManager 入口、KCC/模块表或相关 SourceRef 变化。

## 长期边界

- 保持 `Core → Domain → Module`，`Entity` 是通用实体、KCC 适配器和五域宿主，不新增第二套 PlayerActor/CharacterActor 或控制器栈。
- 当前五个序列化域为 Basic、AI、Buff、Equipment、State；Equipment 拥有库存、槽位、附件和装备效果，Basic 执行运动/交互/战斗，State/展示驱动动画与 IK。
- 输入源只写意图，主链为 `ESInputModule → ESInputService → EntityPlayerInputWriteModule → EntityAIDomain`；UI、回放、网络不得直接操纵 Basic、KCC、武器 Rigidbody 或根 Transform。
- `Entity.cs`、`EntityBasicModules.cs`、`EntityAIModules.cs` 属高风险/高频路径；禁止在 KCC 回调引入 LINQ、反射、层级搜索或字符串分配，并保持 `Core.ModuleTables` 的类型键兼容。
- 不恢复已删除的旧输入模块；不把 `Assets/Scripts/ESPlayer` 空壳程序集当作实现入口。玩家能力须扩展现有 Entity/Domain/Module 合同，并兼容本地、AI、编辑器预览及未来回放/网络输入。
- 本 Warning 只保留边界和禁止事项；重构方向、源码路径、当前事实、历史校正和迁移细节见 Knowledge，不替代源码、编译证据或用户授权。
