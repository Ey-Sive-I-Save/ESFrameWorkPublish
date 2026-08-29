# 装备定义与装配推进路线：EntityEquipmentDomain 与 Weapon 垂直切片

Status: current
StableId: es.aiwarnings.architecture.entity-equipment-weapon.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, architecture, entity, equipment, inventory, weapon, attachment, combat, acceptance
Applicability: Entity 装备、Item/Weapon 定义、挂点、攻击入口与垂直切片
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-entity-equipment-weapon-boundary.md`
StaleWhen: Equipment Domain/Module、Weapon 定义投影、角色模板、Combat 入口或 SourceRefs 变化
Knowledge: `es.aiwarning.entity-equipment-weapon-boundary.v1`

## 长期约束

- `EntityEquipmentDomain` 是装备、背包、槽位、挂点和装备效果来源的事务聚合边界；不得把这些事实重新塞入 `EntityBasicCombatModule`，也不得创建 `EquipmentManager`/`WeaponManager` 等平行万能管理器。
- Inventory、Slot、Attachment、Effect 四模块各自持有实例/占用、挂点过渡和 Effect Lease；Equipment 只提交可回滚事务，不执行攻击、动画/IK 求解、Buff 数值或资源加载。Combat 只读消费当前活动槽。
- `ItemKey` 表示背包/实例身份，`WeaponKey` 表示战斗能力身份；一份 ItemDataInfo 可生成两种强类型 GameCore 投影，但 Key 不得互相冒充。RuntimeKey 不得进入 SO、存档或网络。
- Equip/Holster/Switch 必须携带 `TransitionId + EntityGeneration + TargetRevision`；动画回调和对象池旧代不匹配时无副作用退出。Attachment 只能提交作者化 Socket/IK 请求，不能直接写 Animator 或 FinalIK Solver。
- `EntityBasicCombatModule.TryExecutePrimaryAttack()` 是统一攻击入口；Action/WeaponFire 由明确的 WeaponDefinition/Selector 决定，不以 Action 注册失败偷偷回退另一类武器，不让近战测试关闭未来枪械能力。
- Weapon 模板、Prefab、DataInfo 和 GameCore 注入必须稳定 Key、类型化引用和全量预验证后原子提交；固定路径/Key 冲突、缺作者 Socket/Binding、重复模板或外部引用必须硬失败，禁止静默改绑/覆盖。
- 生成器重复执行只校验并复用既有资产，不清空其他槽位或重置作者配置；正式 Variant 中 Equipment 为唯一装配权威，Combat 为消费方。内容创建与运行时/PlayMode/发布写入隔离。
- 当前装备与实例事务仅为最小垂直切片，护甲、饰品、堆叠、UI、完整 Loadout、近战命中/伤害、动画/IK、网络/存档和资源发布仍需独立证据，不得把 EditMode/静态存在写成可玩。

## 证据边界

详细职责表、Item/Weapon 双投影、Transition 状态、长条近战切片、生成器门禁、失败条件和验收矩阵已迁移至 Knowledge。当前仅有源码与定向静态/EditMode 证据；未证明 Unity PlayMode、Profiler、Player、IL2CPP 或发布行为。
