# 项目最高警告：P0 - CombatModule 武器定义迁移边界与验收门禁

> 状态：现行迁移约束。
> 级别：P0。
> 适用范围：`EntityBasicCombatModule`、`ItemWeaponSharedData`、`ESWeaponRuntimeData`、武器 ItemDataInfo、角色武器槽位和相关 Editor/测试。
> 最后核对：2026-08-10。首批射击/后坐力 Schema 已进入 `ItemWeaponSharedData`；不存在 CombatModule 兼容路径。Unity PlayMode 与全部正式资产配置尚未完成。

## 最高结论

禁止再向 `EntityBasicCombatModule` 新增武器定义参数。它是战斗执行、状态动画协调和 ActionRuntime 宿主，不是武器内容定义。

```text
角色 Entity
  -> 当前武器槽位 / EquipmentInstance / 武器挂点容器
EntityWeaponBinding
  -> hand / holster / fire / muzzle 等场景挂点与表现绑定
ItemWeaponSharedData / ESWeaponRuntimeData
  -> 武器可复用定义：射击、后坐力、弹药、冷却、武器专属 Action
ItemWeaponVariableData / WeaponRuntimeState
  -> 实例可变状态：弹药、过热、临时缓存、生命周期状态
EntityBasicCombatModule
  -> 读取已解析定义并执行，不保存新的武器平衡参数
```

角色状态、Animator 参数、切枪 IK、角色挂点策略不得塞入 WeaponDefinition；它们仍由 State/Combat 执行器和 `EntityWeaponBinding` 拥有。

## 强制规则

开火、命中查询和后坐力只读取 `ItemWeaponSharedData.fire/recoil`。所有 WeaponSlot 必须配置可由 `ESRuntimeDataGameCore.Weapons` 解析的 Weapon Key；缺失 Key、未注入定义或非法 Schema 必须拒绝执行，禁止回退到 CombatModule 参数。

资源 Schema 可以在作者层保留对象选择体验，但 Bake/注入后 Player 只能消费 Stable Key、编译快照或 Provider Handle；不得让 Prefab、AudioClip、SO 裸引用成为 Player 运行时武器协议。

## 验收门禁

1. `ESWeaponGameCoreTable` 和 `ESWeaponConfigKeyTable` 注入正式 `WeaponDefinition` 时必须调用 `ValidateDefinition`；不合法 interval、distance、recoil 数据必须硬失败。
2. 任何设置了 `WeaponSlot.weaponKey` 的正式武器，在正式模式下必须能由 `ESRuntimeDataGameCore.Weapons` 解析。
3. 新增武器参数的 Code Review 必须归入 `ItemWeaponSharedData` 或 `ItemWeaponVariableData`，不得落入 `EntityBasicCombatModule`。
4. 每轮迁移至少覆盖：Definition 注入、Weapon Key 解析、开火参数来源、回池后的实例状态复位。静态编译不替代 Unity EditMode/PlayMode 证据。
5. 旧资产切换到 `WeaponDefinition` 前，必须在 Unity 中验证射击间隔、距离、LayerMask、TriggerInteraction、瞄准门禁与连发后坐力均来自新 Schema。

## 明确不做

- 本门禁不把徒手默认 Attack/HeavyAttack 迁入 WeaponDefinition；它属于角色 ActionBindings/Loadout。
- 本门禁不创建 EquipmentDomain、AI、Targeting、VFX 或 Camera 新系统。
- 本门禁不把 `EntityWeaponBinding` 的 Transform、StateAniDataInfo、IK 辅助目标写入武器定义。
