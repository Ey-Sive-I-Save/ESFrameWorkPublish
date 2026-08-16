# AI 自动化内容身份与 GameCoreKey 迁移

> 状态：现行迁移说明。本文记录当前工作树中的事实、风险和开放裁决，不代表相关源码已经通过 Unity、Player 或发布验收。
>
> 长期稳定规则以 `配置与稳定身份（IdentityConfig）/项目最高警告_P0_稳定Key_Catalog烘焙与RuntimeKey进程边界_AI协作警告.md` 和 `GameCore边界（GameCore）` 为权威；本文不重复其 Catalog、RuntimeKey、Info/Group 和资源边界全文。

## 模块定位

面向 AI 自动化制作内容，GameCoreKey 只标识可独立复用、校验、生成和迁移的内容定义。AI 只产出或选择稳定 Key 与结构化参数；运行期由领域 Table、Catalog 或资源 Provider 将其解析为 RuntimeData 或 Handle。

Key 不标识耐久、冷却、弹药、目标、仇恨、当前阶段等实例状态，也不兼任 Prefab、VFX、AudioClip、Bundle、资源地址或场景对象身份。

## 当前有效设计

- `ESAudioCueKey -> ESAudioCueInfo -> ESAudioCueGroup -> ESAudioCueConfigKeyTable -> ESAudioModule` 已具备可参考的强类型内容链形状；运行证据仍需按任务单独验证。
- Prefab、AudioClip 等资源身份继续使用对应的 `ESAssetRefer*ConfigKey`，由 ResourcePlan、AssetTable 和 Provider 解析。
- Action、Weapon、Skill 等定义应使用各自领域的 ConfigKey；跨定义引用不得退回显示名、资产名或自由字符串协议。
- 作者层可以保留对象选择体验，但 Bake/注入后必须以稳定身份或编译快照为权威，Player 消费者不得依赖对象选择本身解释业务身份。

## 当前迁移事实

- `ActionTemplateDataInfo` 已存在 `ESActionConfigKey`、Group、注入入口、RuntimeTable 和运行消费者，但 `comboTransitions.targetActionId`、`cancelRules.targetActionId` 仍是字符串，尚未完成跨 Action 稳定引用迁移。
- Weapon 已通过 `ItemDataInfo`、`ESWeaponConfigKeyTable` 和运行消费者形成部分内容链；`ItemWeaponSharedData.defaultShot` 已使用强类型 `ESShotConfigKey`，`ESWeaponRuntimeData.prefabKey` 已使用 `ESAssetReferPrefabConfigKey`，不再保存 `extraAsset`。这证明字段迁移，不等于完整资源发布闭环已验收。
- `ESSkillTrackConfigKey` 与 `ESSkillTrackConfigKeyTable` 当前只提供可查询身份和元数据；`ESSkillTrackRuntimeData` 尚未绑定真实轨道定义，`SkillTrackProcessInfo` 也没有独立稳定 Key 或 `IGameCoreSO` 注入入口。因此当前只能视为 Identity Scaffold，不能宣称已形成独立 GameCore 内容定义。
- `SkillDefinitionDataInfo` 与 `ESSkillRuntimeData` 仍直接持有 `SkillTrackProcessInfo`、`StateAniDataInfo`；`linkedSkills` 仍保存 `SkillDefinitionDataInfo` 对象引用，`tags` 仍是字符串列表。其 `casterTagCondition` 已使用现有稳定 Tag 条件，后续 Tag 迁移应复用该体系。

## 开放裁决

### SkillTrack

只有当轨道需要被多个 Action/Skill 独立复用、查询、版本化、迁移或由 AI 单独生成时，才保留 `ESSkillTrackConfigKey`，并同轮补齐正式 Track Definition、Group、Consumer 和含编译轨道内容的 RuntimeData。

若轨道只是 Action/Skill 的内部编排资产，应删除或停止扩张空身份表，改用 Owner Key 加稳定局部 ID，或在 Owner 的编译内容中持有轨道结果。

### State

当前不能因为 `baseStateInfo` 是裸 SO 就直接预建空 State ConfigKey。必须先裁决 State 是否是可独立复用和迁移的正式内容定义；否则应作为 SkillTrack/Skill 编译结果的一部分。裁决前禁止新增第二套 State 身份体系。

## 过时设计

以下做法只能视为迁移债务，禁止作为新内容模板：

- 使用 `targetActionId`、字符串 Tag 或资产名称表达跨定义身份；Weapon 默认 Shot 不得退回旧字符串协议。
- 让 RuntimeData 直接以 Prefab、`GameObject`、`UnityEngine.Object` 或作者 SO 作为 Player 内容身份与加载入口。
- 只新增 Key、空 RuntimeTable 或占位 DTO，就宣称领域内容链已完成。
- 为尚无正式定义和消费者的 Behavior、Perception、Targeting 预建万能 Key。
- 机械删除作者引用，却没有提供 Key 解析、资源 Provider 或迁移工具，导致运行时失去可验证入口。

## 主要风险

- 字符串引用无法可靠发现重命名、重复、缺失和跨版本迁移错误。
- 裸 SO 与直接 Prefab 引用会让 AI 协议、发布闭包和运行时加载权威不一致。
- 空身份表会制造“已有架构”的假象，却不能验证内容、生成 Consumer 或完成运行消费。
- Skill、Track、State 同时迁移时容易把领域裁决、序列化迁移和资源加载问题混在一起，应按垂直切片分批推进。

## 入口文件

- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ActionTemplateDataInfo.cs`
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Action/ESActionConfigKeyData.cs`
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Action/ESSkillTrackConfigKeyData.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/SKillDataInfo.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/SkillDefinitionDataInfo.cs`
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Skill/ESSkillConfigKeyData.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ItemDataParts/ItemKindSharedVariableData.cs`
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Weapon/ESWeaponConfigKeyData.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESAudioCueInfo.cs`
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Audio/ESAudioCueConfigKeyData.cs`

## 禁止事项

- 禁止新增字符串 Action、Shot、Tag 或其他跨定义身份协议。
- 禁止把 RuntimeKey、RuntimeHandle、InstanceID、委托或 Unity 对象写入 AI 生成协议和持久化内容。
- 禁止让行为 Key 兼任资源地址；资源必须走类型化 AssetKey 和 Provider。
- 禁止在 SkillTrack/State 裁决前扩大其全局 Key、表或消费者范围。
- 禁止把生成工程编译、类型存在或静态表存在报告为 Unity/Player 内容闭环已经通过。

## 下一步

1. 冻结引用迁移格式、旧值到新 Key 的可证明映射和失败策略。
2. 将 Action 的连段/取消目标迁移为可空 `ESActionConfigKey`；保持 Weapon 默认 Shot 的 `ESShotConfigKey` 与 Prefab 的类型化 AssetRefer，不得回退。
3. 裁决 SkillTrack 独立定义资格，再迁移 Skill 的 Track、State、LinkedSkill 与 Tag 引用。
4. 对 Weapon 的类型化 AssetRefer 继续补齐 Catalog、Provider、Scope 与发布证据，不把字段迁移冒充资源闭环完成。
5. 用首把近战武器完成 `Weapon -> Action -> 表现编排 -> Audio/VFX AssetKey -> Tag -> Runtime Consumer` 垂直切片，并分别记录静态、Unity、PlayMode、Player 和发布证据。
