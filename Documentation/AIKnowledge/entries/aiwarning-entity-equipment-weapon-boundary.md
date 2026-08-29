# Entity 装备、Weapon 定义与装配事务边界

`KnowledgeId`: `es.aiwarning.entity-equipment-weapon-boundary.v1`  
`Authority`: `AIWarnings + current Equipment/Weapon source`  
`RouteKeys`: `aiwarnings`, `architecture`, `entity`, `equipment`, `inventory`, `weapon`, `attachment`, `combat`, `acceptance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `c5dc34d2bab1e3be86b346b12f3132478f56ffca24d4336ab280e6c60dab22ca`  
`SourceSetHash`: `c5dc34d2bab1e3be86b346b12f3132478f56ffca24d4336ab280e6c60dab22ca`  
`EntryBodyHash`: `41ad0a4d639922e42b65243c26d2be7756f693c271b4d883d0f3d2d774ef2885`  
`StaleWhen`: `Equipment Domain/Module、Weapon 定义投影、角色模板、Combat 入口或 SourceRefs 变化。`

## 迁移说明

原 Warning 249 行、23,922 UTF-8 字节；现 Warning 保留长期事务、权限和证据边界，详细职责、历史状态、垂直切片与失败复盘迁移至本条目。原 Warning 与当前源码均保留 SourceRefs，可回溯但不替代源码或用户授权。

## 结构与所有权

- `EntityEquipmentDomain` 聚合 Inventory、Slot、Attachment、Effect 四模块，仅负责“物品实例从容器进入槽位、挂载/显隐、申请效果来源”的原子事务与回滚。Combat 不拥有装备事实；State/IK 不拥有背包或 Weapon 定义。
- Inventory 管理实例/堆叠/容器；Slot 管理槽位、冲突、Loadout、活动武器；Attachment 驱动作者化 Socket、显隐与过渡；Effect 持有按实例/槽位绑定的 Tag/ValueChange/Permit Lease 并成对释放。禁止把这些能力合并进万能 Manager。
- `ItemKey` 与 `WeaponKey` 是不同稳定身份；ItemDataInfo 可同时产生 Item 和 Weapon 强类型投影，注入先全量预验证、后一次提交。RuntimeKey 仅限当前表生命周期，不持久化。

## 过渡与攻击

- AttachmentPose、VisibilityState、TransitionPhase 正交；每次 Equip/Holster/Switch 带 TransitionId、EntityGeneration、TargetRevision。旧动画事件、退池对象或被替换目标必须无副作用退出。Equipment 只提供握点/IK 请求，StateMachine 汇总 Pose，StateFinalIKDriver 最终求解。
- `TryExecutePrimaryAttack()` 是统一入口；Selector 返回明确执行类别和来源。Action 型近战/徒手必须有有效 ActionKey，远程只有 `fire.enabled` 才可 WeaponFire；禁止以“Action 未注册”做近战/枪械回退。攻击请求成功不等于 Started，已开始攻击必须在完成/取消/打断/退池收到 Finished。
- 双持应由明确 PairedWeapons Action 和单一 attackId 表达，不能同一输入分别调用两次或失败后回退第二把。投掷/法器/Projectile/Shot 作为独立能力扩展，不复制 Combat 内的飞行物系统。

## 生成器、资产与证据

- 作者 Prefab 必须显式 `EntityWeaponBinding` 与业务 Socket；模板只建结构，不负责真实开火、命中或伤害。固定路径/Key/类型冲突、缺引用、外部引用、重复 Binding 或非法物理后端必须硬失败。重复生成只校验复用，不清空其他槽位。
- 长条近战 `weapon.melee.long_bar` 目前是 Verifying；已有源码/定向 EditMode 证据，但 Equip/Holster/Attack、动画事件、IK、命中/伤害、直接销毁与运行时资源发布仍未证明。`ESActionRuntime` 定义热切换若保留 `isRunning` 风险，必须显式取消旧 Runtime 并补测试。
- 删除垂直切片不得要求修改 GameCore 表结构、输入、KCC 或通用 Combat；生成器不得在 PlayMode/编译/域重载期间写入。完整验收需覆盖正式 Variant、输入链、装备过渡、近战命中、网络/存档、PlayMode/Profiler/Player 和资源发布。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentSlotModule.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityWeaponBinding.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityPrimaryAttackSelector.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/装备定义与装配推进路线_AI协作说明.md` (`91a80a2cd812798de343698ccf54e9b8c36049d2ad60897632893eebe0529121`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentDomain.cs` (`9a05fd7f643fc9dfc2d9e359a178cb133e9e0ef3cb3de9e9ab7901b6078b8d76`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityEquipmentSlotModule.cs` (`4d01dc406c342e246dc62d53ae7619bca770382ec13c8a9ece91501c87cf3383`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityWeaponBinding.cs` (`0d15d32aebca84521323e2b8f34d8224e082dd999cdbf98d82a33714aa180752`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs` (`397d0c465f6b59069e388445d6d5724d190a0d08ec3d1719bfdfc9c6a1418c46`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityPrimaryAttackSelector.cs` (`6612cfc4d41288cccfaa29106c142b16107a41518fed6479962d39afca1cad7c`)
