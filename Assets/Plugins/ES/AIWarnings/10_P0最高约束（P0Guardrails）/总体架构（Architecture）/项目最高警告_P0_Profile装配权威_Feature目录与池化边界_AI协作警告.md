# 项目最高警告：P0 - Profile 装配权威、Feature 目录与池化边界

> 状态：现行架构约束。本文冻结 Profile 的职责、数据边界、目录归属与 Editor/Player 分层；它不声明 `ESAudioProfile`、统一工作台或任何领域 Extension 已经实现。
>
> 级别：P0。
>
> 适用范围：所有 `XxxProfile`、Prefab/场景能力装配、Profile 编辑器、可选运行时预解析、对象池 Spawn/Despawn 接线，以及 Audio、Camera、Character、Quality 等领域后续接入。

## 最高结论

Profile 是逻辑体或场景对象的**能力装配权威**。它声明该对象天生携带哪些能力、这些能力怎样绑定、默认采用哪些策略，以及对象池每一代 Spawn/Despawn 应恢复或撤销哪些意图。

Profile 不是全局内容定义、资源生命周期 Owner、运行时服务、动态业务状态容器，也不是第二套对象池生命周期根。

```text
DataInfo / Cue
    = 全局内容定义权威

Profile
    = 某个 Prefab / 场景对象的能力装配与默认策略权威

Runtime State
    = 当前生命、Buff、目标、冷却、Voice 状态、网络快照等动态事实

Domain
    = 全局职责边界

Module
    = 运行时服务与仲裁

Feature
    = 可复用能力的使用侧与领域组件
```

任何实现不得混淆这些职责。

## Profile 架构术语独占权

`Profile` 是 ES 已冻结的一级架构术语，不是“配置很多”“带稳定 Key”“可被复用”或“看起来像预设”的通用后缀。类型、组件、资产、接口、Catalog、DTO、公共成员和用户菜单只有真正满足本文统一 Profile 规范时才允许使用 `Profile` 名称。

以下任一情况均禁止命名为 `XxxProfile`、`XxxProfileCatalog`、`I...Profile`，也禁止用 `ProfileKey`、`profileId`、`profileSettings` 等公共 API 继续传播错误语义：

- 全局内容定义、内容索引或 Key 到内容的 Catalog；
- 单纯参数集合、构建参数、安装选项、玩家覆盖档案；
- Editor 作者策略、模板、预设、校验器或执行 Policy；
- Prefab 身份、阵营、DataInfo 绑定等静态声明组件；
- Runtime Service、Module、Domain、Lease、Handle、动态状态或资源 Owner；
- 只有一个 `ProfileKey` 或 `SchemaVersion`，但没有完整 Header、Settings、Extension、RuntimeContext 与生命周期边界的对象。

不满足 Profile 标准时必须按真实职责命名：

```text
Definition   = 内容定义或静态描述
Catalog      = 稳定 Key 到 Definition/Asset 的索引
Settings     = 一组内嵌参数
Config       = 独立配置资产或项目配置
Preset       = 可选择、可替换的预设或玩家覆盖集合
Policy       = 决策、校验或作者行为策略
Identity     = Prefab/对象静态身份声明
Options      = 一次操作的可选参数
Plan         = 一次构建、安装或执行计划
```

菜单放在 `【ES】/配置/...` 只能说明它是用户配置入口，不能把普通 Config、Definition、Preset 或 Catalog 变成 Profile。禁止通过修改菜单、注释、显示名、增加 Key 或增加空壳 Header 来冒充标准 Profile。

### 机械门禁

新增、重命名或评审任何包含 `Profile` 的活跃类型时，必须先枚举非废弃源码中的声明与菜单入口。每个命中项都必须能逐项证明：

1. 持有 `ESProfileHeader`，且稳定 `ProfileKey`、SchemaVersion、启用状态和迁移边界完整；
2. 持有不可删除的强类型 `XxxProfileSettings`；
3. 持有可校验的单层 `XxxProfileExtension[]` 或等价强类型列表；
4. 持有非序列化 `XxxProfileRuntimeContext`，并记录真实生命周期进入状态；
5. 实现 Awake、Enable、Disable、Pool Spawn、Pool Despawn、Destroy 的明确转发与逆序清理；
6. 不承担 Definition、Catalog、Service、Domain、Runtime State 或资源所有权。

缺少任意一项即视为命名冒用，必须先改成真实职责名，不能以“以后补齐”为由保留 `Profile`。检查至少覆盖：

```text
class / struct / interface / enum *Profile*
CreateAssetMenu 与 AddComponentMenu 中的 Profile
公共字段、属性、参数和返回类型中的 ProfileKey / ProfileId / ProfileCatalog
非废弃现行文档中的架构名称
```

`MovedFrom`、`FormerlySerializedAs`、显式迁移器和兼容读取中的旧名称仅可作为受控迁移证据；它们必须标明旧来源，不能重新暴露为新 API，也不计为新的术语冒用。历史复盘和 `Obsolete` 目录可以保留历史事实，但不得作为当前实现示例。

### 当前整改映射

已完成或正在执行的活跃类型迁移必须遵循以下真实职责名；新增代码不得继续引入旧名：

| 旧名称 | 正式名称 | 真实职责 |
| --- | --- | --- |
| `ESCameraProfile` | `ESCameraViewDefinition` | 相机内容定义 |
| `ESCameraProfileCatalog` | `ESCameraViewDefinitionCatalog` | 相机定义索引 |
| `EntityCharacterProfile` | `EntityCharacterIdentity` | 角色 Prefab 身份组件 |
| `StateDefaultNumericParameterProfile` | `StateDefaultNumericParameterConfig` | 状态机默认参数配置 |
| `ESAudioSpatialProfile` | `ESAudioSpatialSettings` | 音频空间参数 |
| `WeaponRecoilProfile` | `WeaponRecoilSettings` | 武器后坐力参数 |

输入覆盖、字体构建、安装器和 Graph 作者策略等其他历史 `Profile` 命名，必须在对应编辑器/设计域整改时改为 `Preset`、`Config`、`Options`、`Plan` 或 `Policy`；在迁移完成前不得把它们复制为新的 Profile 示例，也不得宣称其符合本文标准。

## 统一 Profile 规范

所有 Profile 使用同一结构语义，但不强制统一领域运行时数据结构：

```text
XxxProfile
├─ ESProfileHeader
├─ XxxProfileSettings
├─ XxxProfileExtension[]
└─ XxxProfileRuntimeContext       非序列化
```

### ESProfileHeader

公共 Header 至少包含：

- 稳定 `ProfileKey`；
- `SchemaVersion`；
- 启用状态；
- 显示名称、摘要等编辑器信息；
- 迁移和配置版本信息。

`ProfileKey` 是 Profile 被其他配置引用时的稳定身份，由 Profile 在 `OnValidate/Awake` 自动补齐。不得在高频热路径临时拼接字符串；需要热路径查询时应在初始化阶段解析为领域强类型 Key 或 RuntimeKey。

`SchemaVersion` 只允许由 Editor 中用户显式触发的迁移事务推进。`OnValidate/Awake` 可以补齐 `ProfileKey`，但禁止把旧 Header 静默写成当前版本；Player 遇到旧版本、非法版本或未来版本必须阻止生命周期转发并给出明确错误。迁移必须遵守：

```text
检测源版本
    -> 在写入前规划完整且唯一的迁移链
    -> 对全部选中 Profile 建立一个 Undo 事务
    -> 逐步迁移并由事务服务推进 SchemaVersion
    -> 对每个结果执行完整 Profile 校验
    -> 全部成功后统一提交
    -> 任一步、任一目标或迁移后校验失败则整体回滚
```

迁移器是对应 `Editor/Profile` 领域的 Editor-only 扩展点，只能修改事务服务提供的 `SerializedObject`；不得自行登记 Undo、推进版本、Apply、标记 Dirty 或保存资产。Runtime 不发现、不选择也不执行迁移器，Drawer/OnGUI 不得为显示正常而隐式迁移。

迁移事务在建立 Undo 前必须拒绝 PlayMode、PlayMode 切换期、只读资产、未签出资产和不可编辑场景。失败回滚不得仅以 `Undo.RevertAllDownToGroup()` 未抛异常作为成功证据；必须复核迁移前后的序列化内容、Managed Reference 类型/顺序/字段、Unity Object 引用、Prefab Override 以及对象/场景 Dirty 状态。任一复核不一致都必须报告“状态不确定”，禁止显示“已完整回滚”。

### XxxProfileSettings

Settings 是该领域默认携带、不可删除的静态基础配置。例如：

```text
ESAudioProfileSettings
    = 显式 Source / Emitter Slot、默认 Cue、默认空间规则、生命周期接入策略

ESCameraProfileSettings
    = Rig、视角、灵敏度、默认避障规则

EntityCharacterProfileSettings
    = 身份、阵营、Definition 绑定和默认装配策略
```

Settings 不是动态状态容器。不得写入 HP、Buff、Cooldown、当前目标、网络状态或资源 Lease。

### XxxProfileExtension[]

Extension 是单层、可选、可校验的强化能力。它不允许再嵌套 Module/Domain 树。

```text
ESAudioProfile
    -> Zone、Mix/Music、Occlusion、Reverb、Surface Foley 等 Extension
```

新增 Extension 必须有稳定 TypeId、SchemaVersion、明确依赖和互斥关系。未知 Extension、重复的独占 Extension、缺失依赖或非法顺序均是编辑器/构建错误，不能在 Player 静默猜测。

禁止：

- `List<IProfileModule>` 作为 Player Runtime 分派链；
- 运行时反射发现 Extension；
- 运行时按 CLR 类型名、稳定 TypeId 或 JSON Payload 动态选择执行实现；
- 每个 Extension 一个 MonoBehaviour、一个 Update 或一个独立对象池 Root；
- Extension 再创建自己的 Extension/Domain 层级。

允许 Profile 持有一个显式序列化的具体 `XxxProfileExtensionSettings` List，并只在 `Awake/OnEnable/OnDisable/Pool Spawn/Pool Despawn/Destroy` 等低频生命周期边缘按稳定顺序调用对应虚方法。这不是通用 Module 分派链；它必须满足：

- List 是唯一配置权威，不再复制一份默认持久化 Snapshot；
- Extension 使用 `OnProfileAwake/OnProfileEnable/OnProfileDisable/OnProfilePoolSpawned/OnProfilePoolDespawned/OnProfileDestroy` 表达真实生命周期，不得压缩成语义模糊的通用 `Apply/Remove`；
- Awake、Enable、Pool Spawn 等开始阶段正序，Disable、Pool Despawn、Destroy 等结束阶段逆序；每个阶段独立幂等，失败只回滚对应阶段；
- 正常结束阶段不得只按当前 `Enabled` 过滤，否则运行中关闭 Extension 会漏清理；失败回滚只补偿本次已进入的 Extension；
- RuntimeContext 必须记录真实进入过的 Extension/阶段。旧版、非法版或未来版 Profile 若从未通过开始阶段门禁，`Destroy` 不得向其 Extension 派发；若对象已成功进入过生命周期后版本状态异常，结束阶段仍只清理实际进入过的 Extension，基础 Child、Pool 注册和内部对象收口不受该门禁阻断；
- `NotifyPoolSpawned/NotifyPoolDespawned` 必须独立维护 Pool Generation，使外部不经过 `ESGenericLife` 也能完整接管；新一代 Spawn 不得继承上一代未结束的 Pool Extension 状态；
- 不在 Update、热循环或高频查询中遍历 List；
- 不使用反射、CLR 类型名或 TypeId 决定运行行为；
- Auto Awake、Auto Enable、Auto Pool 可以分别关闭；外部必须通过 Profile 的 `NotifyAwake/NotifyEnable/NotifyDisable/NotifyPoolSpawned/NotifyPoolDespawned/NotifyDestroy` 对应入口明确接管，不能使用合并入口伪造生命周期；
- 复杂领域若确有构建冻结、跨版本迁移或高频访问需求，可以额外引入强类型 Runtime Data，但不得让它与 List 同时成为配置权威。

### XxxProfileRuntimeContext

RuntimeContext 只属于当前对象实例和当前池代，必须是非序列化数据。它可以保存：

- Pool Generation；
- Awake 是否完成、Enable 是否活跃、Pool 生命周期是否活跃、Destroy 是否完成等阶段状态；
- 激活、注册和异步请求状态；
- 用于 Stop/Cancel/Unregister 的 Handle；
- 当前代的临时表现引用。

它不能成为业务事实或下游服务状态的第二权威。以音频为例，Context 可以保留 `ESAudioVoiceHandle` 以便回收，但 Voice 的播放、淡化、预算、抢占和终态仍只由 `ESAudioModule` 权威维护。

## Editor、配置与 Player 边界

```text
Editor Extension Registry
    -> 模块菜单、标题、Icon、顺序、Drawer、校验、预览、迁移

Profile Settings
    -> 单一 Extension List 配置权威
    -> 稳定 TypeId + SchemaVersion 只用于身份、校验与迁移

Player Runtime
    -> 开始生命周期正序、结束生命周期逆序转发对应回调
    -> RuntimeContext 只记录当前实例运行状态
    -> Auto Awake / Auto Enable / Auto Pool 可关闭并由外部调用对应 Notify 接管
```

`ESProfileExtensionRegistry` 及其类型注册、反射、Profile 元数据和 UI 代码必须位于 Editor asmdef。模块顺序、标题、Icon、校验入口、依赖与互斥关系必须由显式注册表确定，禁止依赖程序集扫描返回顺序。

统一 UI 只统一制作体验，不统一领域运行状态。默认 Profile 使用 List 直接生命周期；Audio、Camera 等复杂领域只有在证明存在构建冻结、迁移或热路径需要时，才增加自己的强类型 Runtime Data。该 Runtime Data 是派生运行表示，不得反向成为第二份配置权威。

## 目录边界

`Runtime/Profile` 是一级架构目录，承载 Profile 定义、Settings、Extension 声明、RuntimeContext，以及确有必要的领域强类型 Runtime Data。它不能演变成包含所有领域运行服务的万能目录。

```text
Assets/Scripts/ESLogic/
├─ Runtime/
│  ├─ Profile/
│  │  ├─ Shared/
│  │  ├─ Audio/
│  │  ├─ Camera/
│  │  ├─ Character/
│  │  └─ Quality/
│  └─ GameManager/Modules/Runtime/
│     ├─ MODULE_ESAudioModule.cs
│     └─ MODULE_ESCameraModule.cs
│
└─ Feature/
   ├─ Audio/
   │  ├─ Components/              例如 ESAudioEmitter
   │  ├─ Runtime/
   │  └─ Integration/
   └─ Camera/
      ├─ Content/
      ├─ Scene/
      ├─ Cinemachine/
      ├─ Timeline/
      └─ Integration/
```

目录语义：

```text
Runtime/Profile
    = 对象如何装配能力

Feature/Audio、Feature/Camera
    = 对象、场景、剧情、技能如何使用对应能力

MODULE_ESAudioModule、MODULE_ESCameraModule
    = 全局服务、最终仲裁、资源与生命周期收口
```

`ESAudioEmitter` 若负责接管或恢复实际 `AudioSource`，必须归属 `Feature/Audio/Components`；只有 Emitter 的配置声明才可位于 `Runtime/Profile/Audio`。同理，Camera 的 Cinemachine 适配、Scene Binding、Timeline Bridge 属于 `Feature/Camera`，不属于 Profile。

已有文件迁移必须连同 `.meta` 一起进行 Unity 资产移动，保持 GUID、类名与命名空间稳定；禁止复制后删除、手工重建 `.meta` 或为迁移保留无意义兼容包装。

## Profile 与对象池

`ESGenericLife` 是对象所有权、Pool Generation 和 Spawn/Despawn 顺序的唯一权威。Profile 只声明默认策略，不得替代或并列第二个 Pool Root。

```text
Pool Spawn
    -> ESGenericLife Root 完成对象基础绑定
    -> Profile 作为 Extension 应用当前代默认策略

Pool Despawn
    -> Profile Extension 先撤销当前代意图
       Stop Voice、Unregister Zone、Cancel Request、清理临时 Handle
    -> ESGenericLife Root 再完成对象基础回收
```

规则：

- Entity 同根时，Entity 是唯一 Pool Root；Profile 只能作为 `ESGenericLife` Extension。
- 独立 Prefab 仅在没有其他合法 Root 时，Profile 才可成为唯一 Root。
- 一个 Profile 只管理显式序列化的 Source、Emitter、Collider、Transform 和 Binding；禁止在 Spawn/Despawn 扫描子树。
- 预热对象必须先经历一次 Despawn 基线；Profile 的 RuntimeContext 不能把上一次租用的 Handle、注册或异步续体带入新代。
- Profile 不创建 Scope、不拥有资源；Cue、Clip、Prefab 等资源仍由 ResourcePlan 或显式 Owner Scope 管理。

## 配置、内容库与编辑器体验

Profile 的完整编辑必须使用统一的 Editor-only Profile Workbench：

```text
Prefab Inspector
    -> 紧凑摘要、状态、打开配置、验证

Profile Workbench
    -> 左侧 Extension 导航
    -> 右侧当前 Settings / Extension 编辑
    -> 校验、预览、依赖、自动生命周期与运行观察
```

普通 Inspector 不得递归堆叠完整 Extension 页面。UI 必须中文友好，模块可见性、顺序、标题和 Icon 来自 Editor 注册表；Odin Group 路径必须保持同类 Group 一致。

若 Extension 需要新增 `SoDataInfo`，它必须按 P0 同时提供匹配 `SoDataGroup<TInfo>`，并通过 ConfigKey、GameCore、ResourcePlan 等既有链路接入。Profile 不得用内嵌 Key、Raw Unity 引用或 Pack 绕过内容库和资源边界。

## 验收与禁止事项

每次新增或迁移 Profile 时至少确认：

1. Header、Settings、Extension、RuntimeContext 的权威边界明确，动态事实未写入序列化 Profile。
2. Player 不依赖 Editor Registry、反射、CLR 类型名、TypeId 或 JSON 动态选择运行实现；List 只在生命周期边缘执行，不进入每帧热路径。
3. Pool Root/Extension 关系符合 `ESGenericLife`，Spawn/Despawn 不扫描子树。
4. Feature 执行组件与 Profile 装配声明没有混放。
5. Extension 的 TypeId、SchemaVersion、依赖、互斥和迁移已由 Editor 显式登记。
6. 新增 Info 已有唯一主 Group；资源仍由 ResourcePlan / Owner Scope 管理。
7. Profile Workbench 的完整页面不回流到普通 Prefab Inspector。
8. 迁移时 `.meta`、GUID、命名空间和 Mono 文件名均保持正确。
9. Auto Awake、Auto Enable、Auto Pool 与外部对应 Notify 接管不会造成重复通知、遗漏结束阶段、阶段串扰或顺序不对称。

违反 Profile 越权持有资源、保存动态业务状态、创建第二 Pool Root、Player 解析 Editor Registry、运行时反射模块、在热路径遍历 Extension、建立两份配置权威，或把 Feature 执行组件塞进 `Runtime/Profile` 任一项，均按 P0 架构缺陷处理。
