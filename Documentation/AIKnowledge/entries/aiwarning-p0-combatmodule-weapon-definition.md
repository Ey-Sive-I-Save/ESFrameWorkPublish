# CombatModule 武器定义迁移：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.combatmodule-weapon-definition.v1`  
`Authority`: `AIWarnings` 原文与当前 WeaponDefinition 注入/解析合同  
`RouteKeys`: `aiwarnings`, `p0`, `combatmodule`, `weapon-definition`, `weapon-key`, `acceptance`  
`HashSchema`: `v2`  
`ContentHash`: `7f410a2f7bb2f62f0e268fc58ae6f51e6ca9cf0cf22be1b08e2d2036419a37a5`  
`SourceSetHash`: `7f410a2f7bb2f62f0e268fc58ae6f51e6ca9cf0cf22be1b08e2d2036419a37a5`  
`EntryBodyHash`: `426a2476a435fab59c1214a025d5e054471ad1d2d8e3025c7f4c54ebe42831a7`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: Warning、Weapon Schema、Weapon Key 解析、注入验证或任一 SourceRef 哈希变化。

## 迁移说明

Warning 保留所有长期所有权、硬失败和验收边界；本条目承载对象关系、迁移矩阵、旧资产切换检查和原文快照。Knowledge 不授予武器迁移、运行时或发布权限。

## 对象关系与迁移矩阵

| 对象 | 权威职责 | 禁止内容 |
|---|---|---|
| Entity/WeaponSlot | 当前装备实例和 Stable Weapon Key | 裸运行时定义、平衡参数 |
| EntityWeaponBinding | hand/holster/fire/muzzle 挂点与表现绑定 | WeaponDefinition 的角色 IK/Transform |
| ItemWeaponSharedData / ESWeaponRuntimeData | 射击、后坐力、弹药、冷却、武器 Action 等可复用定义 | 角色状态、Animator、切枪 IK |
| ItemWeaponVariableData / WeaponRuntimeState | 弹药、过热、缓存、生命周期等实例状态 | 共享定义和全局平衡参数 |
| EntityBasicCombatModule | 读取解析后的定义并执行 | 新增武器定义参数 |

开火、命中和后坐力只能读取 `ItemWeaponSharedData.fire/recoil`。正式模式必须由 `ESRuntimeDataGameCore.Weapons` 解析 Weapon Key；缺失、未注入或非法 Schema 必须拒绝执行。作者层可保留对象选择体验，但 Bake/注入后 Player 只消费 Stable Key、编译快照或 Provider Handle。

### 验收矩阵

1. `ESWeaponGameCoreTable` 与 `ESWeaponConfigKeyTable` 注入时调用 `ValidateDefinition`，非法 interval、distance、recoil 硬失败。
2. 所有正式 `WeaponSlot.weaponKey` 均可由 `ESRuntimeDataGameCore.Weapons` 解析。
3. 新武器参数只能进入 SharedData 或 VariableData，Code Review 不得落入 CombatModule。
4. 每轮覆盖 Definition 注入、Key 解析、开火参数来源和回池状态复位；静态编译不替代 EditMode/PlayMode。
5. 旧资产切换前在 Unity 验证 interval、distance、LayerMask、TriggerInteraction、瞄准门禁和连发后坐力均来自新 Schema。

## 原文保真快照（迁移前）

```markdown
# 项目最高警告：P0 - CombatModule 武器定义迁移边界与验收门禁

> 状态：现行迁移约束。
> 级别：P0。
> 适用范围：`EntityBasicCombatModule`、`ItemWeaponSharedData`、`ESWeaponRuntimeData`、武器 ItemDataInfo、角色武器槽位和相关 Editor/测试。
> 最后核对：2026-08-10。首批射击/后坐力 Schema 已进入 `ItemWeaponSharedData`；不存在 CombatModule 兼容路径。Unity PlayMode 与全部正式资产配置尚未完成。

## 最高结论

禁止再向 `EntityBasicCombatModule` 新增武器定义参数。它是战斗执行、状态动画协调和 ActionRuntime 宿主，不是武器内容定义。

角色 Entity -> 当前武器槽位 / EquipmentInstance / 武器挂点容器
EntityWeaponBinding -> hand / holster / fire / muzzle 场景挂点与表现绑定
ItemWeaponSharedData / ESWeaponRuntimeData -> 射击、后坐力、弹药、冷却、武器专属 Action
ItemWeaponVariableData / WeaponRuntimeState -> 弹药、过热、临时缓存、生命周期状态
EntityBasicCombatModule -> 读取已解析定义并执行，不保存新的武器平衡参数

角色状态、Animator 参数、切枪 IK、角色挂点策略不得塞入 WeaponDefinition。

## 强制规则

开火、命中查询和后坐力只读取 `ItemWeaponSharedData.fire/recoil`。所有 WeaponSlot 必须配置可由 `ESRuntimeDataGameCore.Weapons` 解析的 Weapon Key；缺失 Key、未注入定义或非法 Schema 必须拒绝执行，禁止回退到 CombatModule 参数。

资源 Schema 可在作者层保留对象选择体验，但 Bake/注入后 Player 只能消费 Stable Key、编译快照或 Provider Handle；不得让 Prefab、AudioClip、SO 裸引用成为 Player 运行时武器协议。

## 验收门禁

`ESWeaponGameCoreTable` 和 `ESWeaponConfigKeyTable` 注入正式 `WeaponDefinition` 时必须调用 `ValidateDefinition`；设置了 `WeaponSlot.weaponKey` 的正式武器必须能被解析；新增参数不得落入 `EntityBasicCombatModule`；每轮覆盖注入、解析、参数来源和回池复位；旧资产切换前必须在 Unity 验证所有射击 Schema 字段来源。

## 明确不做

不把徒手默认 Attack/HeavyAttack 迁入 WeaponDefinition；不创建 EquipmentDomain、AI、Targeting、VFX 或 Camera 新系统；不把 `EntityWeaponBinding` 的 Transform、StateAniDataInfo、IK 辅助目标写入武器定义。
```

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_CombatModule武器定义迁移边界与验收门禁_AI协作警告.md` (`85dffa1ae38dbfbc0d11ddd53744e229e5cd9e1421bcf7fd5fa6cc199edc105b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`4d7da543d1f78eafbbc11e92ea76b4a1acdecee2a6c7ae1b59fb14a95e3d1dc3`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-combatmodule-weapon-definition.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
