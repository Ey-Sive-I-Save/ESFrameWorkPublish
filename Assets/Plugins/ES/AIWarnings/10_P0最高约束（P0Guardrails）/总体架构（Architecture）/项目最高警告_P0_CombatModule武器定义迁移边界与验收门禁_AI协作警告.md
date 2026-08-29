# P0：CombatModule 武器定义迁移边界与验收门禁

`Status`: `current`
`StableId`: `es.aiwarning.p0.combatmodule-weapon-definition.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `combatmodule`, `weapon-definition`, `weapon-key`, `acceptance`
`Applicability`: EntityBasicCombatModule、WeaponDefinition/ItemWeaponSharedData、WeaponRuntimeState、武器槽位及相关 Editor/测试。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-combatmodule-weapon-definition.md`
`StaleWhen`: Weapon Schema、Weapon Key 解析、CombatModule 所有权、注入验证或 SourceRefs 变化。

## 长期 P0 约束

- `EntityBasicCombatModule` 只负责战斗执行、状态动画协调和 ActionRuntime，不得新增武器定义或平衡参数；角色状态、Animator、切枪 IK 和挂点仍由 State/Combat 与 `EntityWeaponBinding` 拥有。
- 可复用定义归 `ItemWeaponSharedData`/`ESWeaponRuntimeData`，实例状态归 `ItemWeaponVariableData`/`WeaponRuntimeState`；开火、命中和后坐力只读 `fire/recoil`。
- 正式 WeaponSlot 必须由 `ESRuntimeDataGameCore.Weapons` 通过 Stable Weapon Key 解析；缺 Key、未注入或非法 Schema 必须硬失败，禁止回退 CombatModule 参数或让裸 Prefab/SO 引用成为 Player 协议。
- `ESWeaponGameCoreTable`/`ESWeaponConfigKeyTable` 注入必须调用 `ValidateDefinition`；每轮迁移覆盖注入、Key 解析、开火参数来源和回池状态复位，静态检查不替代 Unity EditMode/PlayMode。
- 旧资产切换前须验证 interval、distance、LayerMask、TriggerInteraction、瞄准门禁和连发后坐力来自新 Schema；不得把徒手 Action、EquipmentDomain、AI、Targeting、VFX、Camera 或挂点辅助目标塞入 WeaponDefinition。

详细对象关系、验收矩阵和迁移前原文快照见 Knowledge：`es.aiwarning.p0.combatmodule-weapon-definition.v1`。
