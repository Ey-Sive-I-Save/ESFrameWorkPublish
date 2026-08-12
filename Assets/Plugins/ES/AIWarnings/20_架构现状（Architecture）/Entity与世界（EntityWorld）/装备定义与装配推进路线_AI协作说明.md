# 装备定义与装配推进路线：EntityEquipmentDomain 与 Weapon 垂直切片

**状态：现行目标契约；`EntityEquipmentDomain` 已确定为 Entity 正式第五 Domain，但运行时尚未接线。Weapon 作者链路与主攻击仲裁为 Verifying。最后核对：2026-08-12。**

## 已批准但尚未实现的目标

`EntityEquipmentDomain` 是装备、背包、饰品和 Loadout 的正式域级权威，不再把这些生命周期继续扩张到 `EntityBasicCombatModule`。当前源码仍只有 Basic / AI / Buff / State 四个 Domain；在 `Entity.cs` 注册、生命周期、Prefab 作者链和 Unity 验收完成前，禁止把第五域写成已实现事实。

```text
Entity
├─ EntityBasicDomain
├─ EntityAIDomain
├─ EntityBuffDomain
├─ EntityStateDomain
└─ EntityEquipmentDomain                 <- 已批准目标，尚未接线
   ├─ EntityEquipmentInventoryModule     <- 背包实例、数量、容器与所有权
   ├─ EntityEquipmentSlotModule          <- 装备槽、饰品槽、主副武器与 Loadout
   ├─ EntityEquipmentAttachmentModule    <- 手持/背挂/腰挂/临时挂点与过渡事务
   └─ EntityEquipmentEffectModule        <- 装备效果来源句柄的申请与释放
```

这不是万能管理器。Domain 只聚合一个共同事务：物品实例从容器进入槽位、改变挂载与显隐、申请装备来源效果，并能原子回滚。物品定义、战斗执行、动画求解、Buff 计算和资源加载仍由各自权威系统负责。

## 正式职责与命名

| 类型 | 唯一职责 | 明确禁止 |
|---|---|---|
| `EntityEquipmentDomain` | 聚合 Inventory / Slot / Attachment / Effect，提交装备事务 | 执行攻击、直接操作 Animator/FinalIK、保存资源对象 |
| `EntityEquipmentInventoryModule` | 管理实体拥有的物品实例、堆叠、容器位置和转移 | 决定手持动作、命中或 Buff 数值 |
| `EntityEquipmentSlotModule` | 管理 Weapon/Armor/Accessory 等槽位占用、冲突、Loadout 和当前活动槽 | 修改 Transform、播放动画、结算攻击 |
| `EntityEquipmentAttachmentModule` | 根据已提交槽位状态驱动挂点、显隐和过渡令牌 | 拥有装备定义、直接调用 FinalIK Solver |
| `EntityEquipmentEffectModule` | 以装备实例/槽位为 source 持有 Buff/Tag/ValueChange/Permit 句柄并成对释放 | 复制 BuffDomain 的叠层、驱散和数值合成 |
| `EntityBasicCombatModule` | 消费当前武器能力并执行攻击、瞄准、命中/开火 | 长期拥有背包、装备槽和装配过渡 |
| `EntityStateDomain` / `StateMachine` | 动画生命周期、混合和 IK Pose 汇总 | 成为装备事实或背包权威 |
| `StateFinalIKDriver` | 消费最终 IK Pose/实时目标并统一求解 | 接收 Equipment 对 Solver 的直接写入 |

脚本应按上述类型拆分，禁止再创建同义的 `EquipmentManager`、`WeaponManager`、`LoadoutManager`、`AttachmentScheduler` 或把所有实现重新堆进一个 `EntityEquipmentModules.cs` 巨文件。兼容迁移期间旧 Combat 字段可暂时存在，但新功能不得继续写入旧权威。

## Item 与 Weapon 双投影契约

一份 `ItemDataInfo` 可以同时具备通用 Item 定义和 Weapon 定义，并向 GameCore 注入两种强类型投影：

```text
ItemDataInfo
├─ itemKey                         -> ESItemGameCoreTable / ItemRuntimeData
└─ ItemWeaponDataBlock.weaponKey   -> ESWeaponGameCoreTable / WeaponRuntimeData
```

- `ItemKey` 表达背包、堆叠、持有和通用物品身份；`WeaponKey` 表达战斗武器能力身份，二者可以共存但不能互相冒充。
- 一个作者 SO、两个运行时投影；背包解析链为 `ItemKey -> ItemRuntimeData -> WeaponKey -> WeaponRuntimeData`。
- 双投影注入必须先全量预验证，再一次性提交。任一 Key 冲突、引用缺失或定义无效时，两张表都不得留下半提交结果。
- RuntimeKey 只属于当前表生命周期，不进入 SO、存档或网络。存档和网络只保存稳定 ItemKey、WeaponKey、槽位与实例状态。
- 当前 `ESItemConfigKey`、Item RuntimeData 和 Item 强类型表仍未落地；本节是后续实现合同，不是现有源码事实。

## 装配状态与动画事件协议

挂点、显隐和过渡是三个正交状态，不得合成一个含糊的 `EquippedState`：

```text
AttachmentPose  : None / MainHand / OffHand / Back / Hip / Temporary
VisibilityState : Hidden / Visible / FadingIn / FadingOut
TransitionPhase : Idle / Equipping / Holstering / Switching
```

每次 Equip、Holster 或 Switch 都必须生成 `TransitionId + EntityGeneration + TargetRevision`。动画事件只有三者仍匹配时才能提交挂点、显隐或副手 IK 目标；旧动画回调、对象池上一代实体和已被替换的目标必须无副作用退出。

```text
Equipment 请求过渡
  -> StateMachine 播放对应 Action/状态
  -> 动画事件提交具名阶段
  -> Equipment 校验 TransitionId / Generation / Revision
  -> AttachmentModule 切换作者化 Socket
  -> StateMachine 汇总 IK Pose
  -> StateFinalIKDriver 最终求解
```

Equipment 只能提供稳定的握点、IK 目标和权重请求，不能直接写 Animator 内部状态或 FinalIK Solver。正式 CharacterVariant 必须作者化 MainHand、OffHand、Back、Hip 等业务 Socket；开发样例可生成并告警，正式发布资产缺失时必须阻止发布。

## 当前事实

项目现在有“武器定义 + 角色武器槽位 + 挂点绑定 + Combat 执行”的局部闭环，但不能把它称为完整装备系统：

```text
ItemDataInfo (ItemKind.Weapon)
  -> ItemWeaponDataBlock
  -> ESWeaponGameCoreTable / ESWeaponConfigKeyTable
  -> ESWeaponRuntimeData.sharedData

EntityBasicCombatModule.weaponSlots
  -> WeaponSlot.weaponKey
  -> EntityWeaponBinding
  -> 手持/背挂/瞄准/开火/切枪状态
```

- `ItemDataInfo` 是 Weapon 作者定义入口；必须使用 `ItemWeaponDataBlock`、显式 `ESWeaponConfigKey`、`ItemWeaponSharedData` 和 `ItemWeaponVariableData`。
- `ESWeaponGameCoreTable.Inject(...)` 会检查 SharedData、Key 和 `ValidateDefinition`；KeyName 只用于编辑器/策划识别，不能作为运行时身份。
- `EntityBasicCombatModule` 当前仍负责槽位顺序、装备/收枪/切换、枪械开火、状态过渡和装备 Tag，这是待迁移事实；目标权威为 `EntityEquipmentDomain`，新装备生命周期不得继续扩张 CombatModule。武器平衡参数同样不得塞回 CombatModule。
- `EntityBasicCombatModule.TryExecutePrimaryAttack()` 是当前唯一主攻击执行入口；`EntityAIDomain` 只消费一次 Attack，不再用“先尝试 melee、未注册再回退枪械”的注册状态分支做武器仲裁。
- `EntityPrimaryAttackSelector` 返回“执行类别 + 攻击来源”：Action 可承载徒手、近战武器和双持组合技；只有远程且 `fire.enabled` 才选择 WeaponFire。它不消费资源、不执行 Action，也不操作 Transform/KCC/物理；投掷、法器等后续能力应扩展明确选择结果，不能回到 AI Domain 堆第二套分支。
- `EntityWeaponBinding` 负责每把武器的手持、背挂、枪口、瞄准目标、双手握点、状态 AniInfo 和手持 Tag；不要把偏移写进 Humanoid 右手骨。
- `ESWeaponSceneTemplate` 只创建结构和挂点，不负责真实开火、弹药、命中或伤害。

## 当前垂直切片：大长条近战武器

2026-08-10 已生成首把 Weapon 样板“大长条”，当前成熟度仍为 `Verifying`：

- 定义资产 Key 为 `weapon.melee.long_bar`，使用 `ItemKind.Weapon`、`ItemWeaponKind.Melee`、显式 Weapon StringKey；`fire` 与 `recoil` 均关闭，不把近战伪装为枪械 Hitscan。
- 作者 Prefab 位于 `Assets/ESNormalAssets/WeaponPrototypes/大长条.prefab`，根节点显式包含 `Item`、`ESWeaponSceneTemplate` 与 `EntityWeaponBinding`；模型是无 Collider 的长条 Cube，没有 Rigidbody、2D 物理组件或第二运动后端。
- 正式大黑塔 Variant 已由 `ESFormalHertaPlayerVariantBuilder` 装配唯一 `EntityBasicCombatModule` 与唯一 Weapon Slot；武器作为嵌套 Prefab 放在 `06_装备_Equipment/EquipmentVisuals`，不依赖场景实例手工覆盖。
- 大长条模板身份已固定为 `weapon.melee.long_bar / 大长条 / Custom`；`ShellEject`、`Magazine`、`Chamber` 等标准引用与双手 Binding/攻击参考/切换辅助目标均由升级入口补齐并受严格校验。正式作者验证会拒绝缺失引用、Prefab 外部引用、重复模板/Binding，以及 Rigidbody、Rigidbody2D、Collider2D。
- 静态 Prefab 核验已确认 Weapon Binding、挂点和槽位存在；`ES_Logic.csproj` 与 `ES_Logic.Editor.csproj` 均取得 `0 warning / 0 error`。`EntityPrimaryAttackSelectorTests` 已覆盖徒手、主副手来源、显式双持 Action，以及投掷/法器即使误开 `fire.enabled` 也不得落入 Hitscan；这只证明攻击类型选择规则，不代表 PlayMode 中 Action、命中或伤害已可用。
- 生成器已按扩展性门禁加固：重复执行只验证并复用现有作者资产，不重置已有配置；固定路径或稳定 Key 若属于其他资产会硬失败；角色构建器只按 `weapon.melee.long_bar` 定向 upsert，不再 `Clear()` 其他武器槽位。Combat 的通用枪械入口保持开启，具体武器是否射击由各自 `WeaponDefinition.fire.enabled` 决定。
- 正式大黑塔视觉迁移会在构建器内识别并禁用旧预览根下的 `Cube1 / Cube1 (1) / Cube1 (2)`；仅当对象仍是直接子节点且具有 `MeshFilter + MeshRenderer` 时才处理，名称与真实模型冲突会硬失败。不得在生成后的 Prefab 或场景实例上手工修这三个对象。

## 生成器扩展性与隔离门禁

- 内容测试必须落在独立 ItemDataInfo、独立 DataGroup、独立作者 Prefab 和稳定业务 Key 中；删除该垂直切片不得要求修改 GameCore 表结构、输入服务、KCC 或通用 Combat 类型。
- “创建内容”菜单不得把已存在的作者资产重新初始化为默认值。资产存在时只做所有权、类型、Binding 与物理后端校验；需要破坏性重建时必须另设明确菜单并先备份。
- 固定路径被其他类型占用、Item Key/Weapon Key 不一致、DataGroup 同 Key 指向其他定义、Prefab 指向其他 ItemDataInfo 时必须硬失败，禁止为了通过测试静默改绑或覆盖。
- 正式 Variant 按稳定 Weapon Key 查找并替换自己的槽位，保留其他槽位及其顺序；同 Key 重复、槽位根跨出 `EquipmentVisuals`、只有同名 Transform 而没有 Key 所有权时必须硬失败。
- 角色级 Combat 只保留能力开关和槽位编排，近战/枪械差异由当前 WeaponDefinition 与 Action 决定。禁止让一把近战测试武器全局关闭未来枪械能力，也禁止让枪械测试把 Attack 输入永久改成第二套链路。
- 主攻击选择不得以 Action 是否注册作为近战/枪械回退条件。Action 型普攻缺少有效 Action Key 时必须明确失败并补内容接线；不能偷偷落入枪械路径。枪械定义只有在自身 `fire.enabled` 时才允许 WeaponFire。
- 所有生成/重建入口在 PlayMode、编译或域重载期间必须停止写入。静态编译、菜单返回成功和资产哈希稳定仍不等于 PlayMode 可玩。

## 当前语义冲突风险

- `enableGunFire`、`fireOnAttackInput`、`TryFireWeapon()` 只属于远程 WeaponFire 执行门禁，不能解释为所有武器的主攻击开关。主攻击总入口是 `TryExecutePrimaryAttack()`。
- `EntityWeaponBinding.fireOrigin`、`fireStateKey` 与 `ESWeaponSceneTemplate.ballistic` 是远程/弹道作者字段。大长条当前保留这些标准模板引用只是为了结构完整和未来工具兼容，不代表近战 Action 已经使用枪口、膛室或开火状态；禁止把它们误报成近战命中挂点。
- `ESWeaponTemplateFireKind.Custom` 只是作者模板的自定义执行提示，不等于 `ItemWeaponKind.Melee`，也不替代正式 WeaponDefinition。模板身份和 GameCore 武器类型必须分别校验。
- `WeaponFireDefinitionData` 出现在所有 Weapon SharedData 中是统一 Schema 的可选能力块；对近战必须保持 `enabled=false`。字段存在不代表运行时已经接入枪械。
- `EntityBasicCombatModule` 目前同时承载通用装备切换与远程执行字段，这是既有聚合债务；后续拆分必须以实际生命周期和执行权威为依据，不能仅靠重命名再包一层 `WeaponManager`、`EquipmentScheduler` 或 `AttackDispatcher`。

## 普攻生命周期、徒手与双持扩展边界

- 没有活动武器槽位时，角色只有在 `allowUnarmedPrimaryAttack=true` 且 `unarmedPrimaryAttackAction` 有效时才能徒手普攻。活动槽位存在但缺 Root、Binding 或 Weapon Key 属于装配错误，必须失败；禁止把坏槽位静默降级成徒手。
- Action 型武器的普攻 Action 按“当前 `WeaponSlot.primaryAttackActionOverride` → `ItemWeaponSharedData.primaryAttackAction` → 角色 `defaultPrimaryAttackAction`”解析。旧序列化字段通过 `FormerlySerializedAs` 迁移；覆盖只改变 Action 内容，不得改变 WeaponDefinition 的正式武器类型。
- `EntityPrimaryAttackEvent` 使用稳定的 `attackId` 发出 `Started -> HitResolved -> Finished` 生命周期，只描述攻击事实与来源，不声明任何具体玩法效果、数值、资源或触发规则。后续系统可按自身需求选择是否订阅，Combat 不提前规定实现方式。
- `TryExecutePrimaryAttack()` 返回成功或 Action 进入输入缓冲，只表示请求已接收，不等于攻击已经 `Started`。被覆盖且从未启动的缓冲攻击直接丢弃关联上下文；已经开始的攻击在完成、取消、打断、禁用或退池时必须收到 `Finished`，保证底层生命周期闭合。
- Combat 不保存后续玩法系统的状态，也不决定扩展效果。若未来出现具体需求，应由实际所有者通过现有生命周期接入；不得提前把尚无消费者的玩法字段塞进 CombatModule。
- 双持不是“同一次输入分别调用两次单手攻击”。双持装配最终应提供主手 Key、副手 Key 和一个显式成对 Action，并使用 `EntityPrimaryAttackSource.PairedWeapons`；一次 attackId 只能提交一次 Action并完成一次生命周期。交替主副手也应由明确 Policy/Action 决定，不能用第一把执行失败后回退第二把。
- 当前只建立了来源、成对 Action 选择和统一事件契约；双武器同时挂载、双手切换与实际伤害结算仍需后续 Equipment/Action 垂直切片验证。不得把契约存在误报成双持已经可玩。

以下阻塞仍未解除，不得据此宣称已可玩：

1. 既有 `Assets/ESNormalAssets/Data/Group/Item/新建物品数据组1566.asset` 中 `信息数据键2` 的 Shot 子定义原先缺少显式 Key，已补为唯一 `shot.test.data_key_2`；但 Unity 正式 Item GameCore 全量重建尚未取得回执，因此仍不能宣称“大长条”已注入正式 Weapon Table。不得删除该测试数据，也不得复用已有的 `数据键2`。
2. 本次生命周期枚举改名之前曾取得 Unity Editor 0 error、Selector 11/11、Action 脉冲/缓冲覆盖/取消清理定向用例 3/3、Weapon 默认注入用例 1/1 的历史基线；该基线不覆盖本次命名修订，必须重新触发 Unity 导入、域重载并复跑定向测试后才能恢复为当前证据。本轮没有进入 PlayMode。Equip、Holster、Attack 的实际 Action 消费、Tag、近战命中/伤害仍未完成运行验收，不能把 EditMode 契约测试写成“已可玩”。
3. 当前 `TryFireWeapon()` 的 Hitscan 路径不等于近战攻击实现。大长条主动攻击必须继续走既有 `ESInputService -> EntityAIDomain -> EntityBasicCombatModule -> Action/Combat` 入口；若近战 Action、命中采样或伤害消费尚缺，应作为下一阶段补齐，不得绕过 Entity KCC 直接写 Transform、Motor 或 Rigidbody。
4. `ESWeaponRuntimeData` 仍直接持有 `GameObject prefab` 与 `UnityEngine.Object extraAsset`，这是既有资源边界迁移债务；正式发布前应迁移到类型化 AssetKey/Provider，不应在新内容中继续扩大裸对象协议。
5. `EntityBasicCombatModule` 的自动补齐 `EntityWeaponBinding` 是运行时兜底，不是作者验收替代品；正式武器 Prefab 应显式带有正确 Binding 和挂点。

## 推荐推进顺序

### 阶段 0：冻结边界与迁移权威

- 先以 `weapon.melee.long_bar` 收口近战垂直切片；允许建立最小 `EntityEquipmentDomain` 骨架和迁移适配，但在垂直切片验证前不一次性实现背包 UI、枪械附件、网络同步或配件万能管理器。
- 迁移期必须保持单一写权威：同一阶段要么由旧 Combat 适配器提交，要么由 Equipment 提交，禁止两边同时修改槽位、挂点、Tag 或过渡状态。
- 近战攻击走既有 Action/Combat 意图入口；枪械 Hitscan 与 Projectile/Shot 是后续独立切片，不能借它们伪造近战可玩证据。
- 修改构建器、Prefab 或资产前退出 PlayMode，并在 `ES/Bak` 留 before 基线。

### 阶段 1：建立正式 Weapon 定义

1. 在正式 Item Data Group 中新建独立 `ItemDataInfo`。
2. 设置 `baseConfig.kind = Weapon`、显示名称、显式 `ESWeaponConfigKey`（推荐业务 StringKey）。
3. 填写 `ItemWeaponSharedData.weaponKind`、`fire`、`recoil`、冷却和初始 `ItemWeaponVariableData`。
4. 运行 `ValidateConfiguration` 和 Weapon GameCore 注入；重复 Key、无效 interval/distance/recoil 必须硬失败。
5. 记录 Definition Key、来源资产和运行时 Table 解析结果。

### 阶段 2：制作武器 Prefab 与挂点

1. 通过 `【ES】/内容制作/武器模板/创建通用武器场景模板` 创建结构。
2. 把真实模型放进 `30_表现资源/ModelRoot`，确认 `Muzzle`、`RayOrigin`、`RightHandGrip`、`LeftHandGrip`、`AimReference`、`RecoilPivot`。
3. 在武器根显式挂 `EntityWeaponBinding`，配置 `handMount`、`holsterMount`、`fireOrigin`、`aimTarget`、双手握点和状态映射。
4. 让作者 Prefab 的 `weaponKey` 与 ItemDataInfo 的显式 Key 一致；不使用显示名、Prefab 名或层级名代替 Key。

### 阶段 3：装配到正式玩家 Variant

- 在 `ESFormalHertaPlayerVariantBuilder.ConfigurePlayerModules(...)` 中按既有 Basic Domain 入口装配 Combat Module；禁止直接在场景实例临时添加。
- 通过 `WeaponSlot` 配置 Weapon Root 与 Weapon Key；正式 Variant 只保留需要的武器槽位，不空挂一套“万能装备组件”。
- `WeaponSocket` 由 `EntityTransformMapping` 缓存；运行时通过 `EntityWeaponBinding` 解析手持/背挂，不重新扫描 Humanoid 骨骼。

### 阶段 4：接通玩家输入与运行态消费

```text
Input System / ESInputService
  -> EntityPlayerInputWriteModule
  -> EntityAIDomain
  -> Equip/Switch/Holster/Attack intent
  -> EntityBasicCombatModule
  -> Weapon Key -> ESWeaponRuntimeData.sharedData
  -> State / IK / Hitscan
```

大长条第一轮只验收：启动背挂、装备、收枪、切换、Attack 意图、近战 Action、命中采样/伤害、手持 Tag、IK、回池/销毁清理。装备动作由 State/Action/IK 消费，ItemDataInfo 不写运行副作用；枪械的瞄准、Hitscan、连发间隔和后坐力另行验收。

### 阶段 5：再接 Projectile/Shot 与资源发布

- Weapon 只通过类型化 `ESShotConfigKey` 选择 Shot 定义；Shot 运动继续走 Item/Shot 模块，不能在 CombatModule 内复制一套飞行物。
- 武器模型、VFX、Audio、Shot Prefab 的运行时寻址迁移到 AssetKey + Provider；不能把 `GameObject` 裸引用继续扩展成发布协议。
- 完成 ResourcePlan、Manifest、PlayMode、Player 和对象池证据后，才把首把武器状态提升到 `Stable`。

## 装备与武器的边界

当前源码中的“装备”仍只落到武器槽位和武器挂载；正式目标已经确定为 `EntityEquipmentDomain`。护甲、饰品、背包和 Loadout 接入时，必须先定义稳定 ItemKey/类型化定义 Key、实例状态和所有权/Lease，再接入对应 Module；不得把所有类型塞进 `EntityBasicCombatModule`，也不得绕过第五域创建平行的万能 `EquipmentManager`。

推荐安全迁移顺序：

1. 先增加无行为接管的 Domain/Module 骨架、生命周期和只读查询，验证 Entity 注册、对象池重绑和 Inspector。
2. 把 WeaponSlot 数据所有权迁到 SlotModule，旧 Combat 通过只读适配器查询；此阶段不改变攻击入口。
3. 把 Equip/Holster/Switch 与挂点提交迁到 AttachmentModule，引入三重版本校验和作者 Socket 验证。
4. 把装备 Tag/ValueChange/Permit 迁到 EffectModule，以 source handle 成对释放，BuffDomain 仍是效果权威。
5. 增加 ItemKey 与 Item GameCore 双投影事务，再接 InventoryModule；没有稳定 Item 身份前不做存档或网络协议。
6. 最后接饰品、护甲、背包 UI、枪械二级附件和网络/存档，每类单独做垂直验收。

## 验收清单

```text
[x] 大长条 Weapon ItemDataInfo 独立存在，ItemKind/Shared/Variable/ConfigKey 静态一致
[ ] ESWeaponGameCoreTable 注入成功，ValidateDefinition 通过
[x] 大长条 Weapon Prefab 显式 EntityWeaponBinding，挂点无临时物理组件
[x] 正式玩家 Variant 由构建器装配 Combat Module 与 WeaponSlot
[x] Attack 输入只经现有输入链路进入单一 Combat 执行入口，近战/枪械不再按 Action 注册状态互相回退
[ ] Equip/Switch/Holster/Attack 的 PlayMode 实际消费均来自现有输入链路
[ ] 近战 Action、命中采样与伤害均来自现有 Combat/Action 链路
[ ] 手持 Tag、状态、IK、回池和销毁均成对清理
[x] 主攻击类型选择器覆盖徒手、主副手来源和显式双持 Action
[ ] PlayMode、Profiler、Player 证据分层记录
```

## 禁止事项

- 禁止把 WeaponDefinition 参数继续添加到 `EntityBasicCombatModule`。
- 禁止把角色 Animator、StateMachine、IK 目标、挂点策略写进 Weapon Data。
- 禁止把 `ItemDataInfo.KeyName`、Prefab 名或 Transform 名当跨定义身份。
- 禁止内容构建器调用 `weaponSlots.Clear()` 清除其他内容切片，或仅凭同名 Transform 删除无法证明所有权的对象。
- 禁止在 `EntityAIDomain` 或 Input 层恢复“先近战、失败再枪械”的回退链；攻击类型必须由当前 WeaponDefinition 选择。
- 禁止“创建”菜单重复执行时重置已存在的作者参数、改绑其他 Prefab，或覆盖冲突 Key。
- 禁止在首个 Weapon 垂直切片之前设计万能装备系统、配件系统或背包同步协议。
- 禁止把 `EntityEquipmentDomain` 的批准目标描述成当前已经接线、已经可玩或已经通过 Unity 验收。
- 禁止以模板存在、Key 表存在或静态编译通过宣称武器已可玩。
