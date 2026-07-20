# 模型重构_插件依赖边界_AI协作说明

职责：服务于玩家/角色对象“模型重构”。目标是让后续 AI 明确当前框架使用了哪些插件、哪些可以进入角色运行时核心、哪些必须隔离在适配层，避免在重建 `CharacterActor / Entity / Player` 体系时引入不可控耦合。

最后核对时间：2026-07-17。本文件基于本地 `Assets/Plugins`、`Packages/manifest.json`、asmdef 和源码 `rg` 结果整理；如源码、包版本、asmdef 变化，以当前源码为准。

## 已验证入口

- 插件根目录：`Assets/Plugins`
- 包清单：`Packages/manifest.json`
- 主要运行时程序集：`Assets/Scripts/ESLogic/ES_Logic.asmdef`
- 玩家空壳程序集：`Assets/Scripts/ESPlayer/ESPlayer.asmdef`
- 设计层程序集：`Assets/Plugins/ES/1_Design/ES_Design.asmdef`
- 角色现状主体：`Assets/Scripts/ESLogic/Runtime/Entity`
- 运动抽象：`Assets/Scripts/ESLogic/Runtime/Movement`
- 状态/IK：`Assets/Scripts/ESLogic/Runtime/State`
- 输入运行时：`Assets/Plugins/ES/1_Design/Input`、`Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs`

## 当前插件/包地图

### ES 自研框架

- 路径：`Assets/Plugins/ES`
- 角色相关定位：框架基础、Core/Domain/Module、输入设计层、RuntimeMode、编辑器工具、示例、生成数据。
- 当前硬事实：`ES_Stand` 几乎是基础层；`ES_Design` 引用 `ES_Stand`、Odin、`Unity.InputSystem`；`ES_Logic` 引用 `ES_Stand`、`ES_Design` 和多个第三方运行时。
- 模型重构建议：新角色体系应沿用 `Core -> Domain -> Module` 思想，但不要继续把所有功能塞进 `EntityBasicModules.cs`。角色上层应有 `CharacterActor` 或同级 facade，底层再桥接现有 `Entity`。

### Odin / Sirenix

- 路径：`Assets/Plugins/Sirenix`
- 使用范围：大量 `OdinInspector` 属性、`OdinSerialize`、编辑器 Drawer、工具面板、序列化辅助。
- 当前耦合程度：高。`ES_Design` 和大量 ES 基础类直接使用 Odin，短期不能移除。
- 模型重构边界：可以在 ScriptableObject 配置、Inspector 调试面板、编辑器装配工具中使用 Odin；不要让运行时协议、网络同步包、战斗结算数据依赖 Odin 序列化作为唯一格式。

### Kinematic Character Controller / KCC

- 路径：`Assets/KinematicCharacterController`
- 程序集：`KCC`
- 使用范围：`Entity.cs`、`EntityKCCData`、运动模块、攀爬/飞行/游泳/骑乘、MatchTarget、模板场景说明。
- 当前耦合程度：角色运动硬依赖。`ES_Logic.asmdef` 直接引用 `KCC`。
- 模型重构边界：KCC 应是 `CharacterMovementAdapter` 或 `EntityMotionDriverAdapter` 下面的物理运动实现，不应成为 `CharacterIntent`、剧情控制、AI 决策、网络快照的直接类型。所有输入/AI/剧情/网络位移请求应先转成统一 motion request，再由 KCC adapter 执行。

### Unity Input System

- 包版本：`com.unity.inputsystem` 1.11.2
- 使用范围：`ESInputRuntime / ESInputService / ESInputSystemSource`，以及旧 Entity AI 输入模块。
- 当前耦合程度：中高。`ES_Design` 和 `ES_Logic` 都引用 InputSystem。
- 模型重构边界：输入不等于玩家。新角色体系应接收 `CharacterIntent`，其来源可以是本地 InputSystem、AI、网络、剧情脚本、回放。不要让角色核心直接读 `InputAction`。

### RootMotion / FinalIK

- 路径：`Assets/Plugins/RootMotion`
- 程序集：`RootMotion`
- 使用范围：`StateFinalIKDriver`、`StateMachine` IK 姿态合成、足底/瞄准/LookAt/HitReaction/Recoil 等表现层。
- 当前耦合程度：状态表现层硬依赖，角色核心不应硬依赖。
- 模型重构边界：FinalIK 只能作为 `CharacterPresentation/IKAdapter` 的实现。状态机可以输出 IK pose/aim/look targets，但战斗命中、移动权威、剧情控制不应依赖 FinalIK solver 是否存在。

### Cinemachine

- 包版本：`com.unity.cinemachine` 2.10.5
- 使用范围：当前源码中角色侧直接引用较少，模板场景文档提到相机应在场景相机系统中引用玩家目标点。
- 当前耦合程度：低到中，更多是相机系统依赖角色锚点。
- 模型重构边界：不要把 Main Camera 或 Cinemachine Virtual Camera 挂进角色 prefab 核心层。角色只暴露 `CameraTarget / AimTarget / LookAtTarget` 等稳定 Transform 或接口，由场景相机系统绑定。

### DOTween / Demigiant

- 路径：`Assets/Plugins/Demigiant/DOTween`
- 程序集：`DOTween.Modules`
- 使用范围：ES 工具函数、编辑器 TrackView、安装器配置；`ES_Logic.asmdef` 当前引用了 DOTween.Modules。
- 当前耦合程度：框架可用，但角色核心不应依赖。
- 模型重构边界：可用于 UI、镜头过渡、编辑器工具、非权威表现动画；不要用 DOTween 驱动角色根位移、KCC 位置、网络同步状态或战斗判定窗口。

### Easy Save 3

- 路径：`Assets/Plugins/Easy Save 3`
- 程序集：`EasySave3`
- 使用范围：`ESGameSaveModule.cs`。
- 当前耦合程度：保存系统硬依赖，角色核心不应直接依赖。
- 模型重构边界：角色只提供可保存快照或状态导出，不直接调用 `ES3.Save/Load`。存档由 GameManager/SaveSystem 聚合，避免角色 prefab 变成存档入口。

### TextMeshPro / UGUI

- 包版本：`com.unity.textmeshpro` 3.0.9，`com.unity.ugui` 1.0.0
- 使用范围：UI、调试显示、可能的战斗飘字/交互提示。
- 模型重构边界：角色核心不依赖 TMP 组件。UI 通过角色状态查询、事件或 view-model 获取数据。

### Timeline / Playables

- 包版本：`com.unity.timeline` 1.7.6
- 使用范围：SkillSequence、动画片段运行时缓存、剧情/技能时间轴潜在入口。
- 当前耦合程度：技能/剧情层可用。
- 模型重构边界：Timeline 可以驱动 `CharacterIntent`、状态请求、动画请求和剧情锁定，但不要绕过 `ControlAuthority` 直接写角色根 Transform。

### UniTask

- 包版本：`com.cysharp.unitask` 2.5.11
- 当前观察：包已安装，角色主线源码引用不明显。
- 模型重构边界：可用于异步加载、剧情流程、资源准备；不要把每帧角色逻辑建立在 async/await 链上。热路径仍应是明确的 Tick/FixedTick/Module 生命周期。

### MemoryPack

- 包版本：`com.cysharp.memorypack` 1.21.4；同时 `Assets/Plugins/ES/ThirdParty/MemoryPack` 有文档/第三方残留。
- 当前观察：生成/数据链路相关痕迹较多，角色主线直接引用不明显。
- 模型重构边界：适合快照、配置、网络/存档中间格式评估；不要直接把 UnityEngine Object、Transform、Animator、KCC motor 放进 MemoryPack 数据结构。

### Luban

- 包：`com.code-philosophy.luban`
- 生成目录：`Assets/Plugins/ES/Generated/Luban`
- 使用范围：配置生成，已有 skill/text/config 示例 JSON/CSharp。
- 模型重构边界：角色职业、技能、成长、可切换角色配置可以走 Luban 或现有 Info/SO 桥接；运行时角色实例不应依赖生成表对象的可变状态。

### Whisper

- 包：`com.whisper.unity`
- 当前观察：包已安装，角色主线未见直接依赖。
- 模型重构边界：可作为语音输入/剧情交互扩展，不属于角色核心依赖。需要通过输入/剧情服务转成 intent 或 dialogue event。

### URP / Unity 模块

- 包版本：`com.unity.render-pipelines.universal` 14.0.11，另有 Unity 内置模块。
- 模型重构边界：角色表现层可依赖渲染材质、Renderer、Animator；角色逻辑核心不应依赖渲染管线实现。

### Gaskellgames、vHierarchy、vFolders

- 路径：`Assets/Gaskellgames`、`Assets/vHierarchy`、`Assets/vFolders`
- 定位：编辑器/项目管理/层级辅助工具。
- 模型重构边界：不能进入运行时角色核心。若写装配工具，可作为编辑器体验参考，但不要让运行时代码引用这些程序集。

## 新角色体系的依赖分层建议

1. 纯协议层：`CharacterIntent`、`ControlAuthority`、`CharacterId`、`CharacterRuntimeState`、`CharacterSnapshot`。不引用 KCC、FinalIK、Cinemachine、EasySave、DOTween、TMP。
2. 领域层：角色生命周期、切换、阵营、队伍、剧情锁定、战斗状态、技能请求。可以引用 ES Core/Domain/Module 和项目数据类型，但尽量不直接引用第三方组件。
3. 适配层：`KccMovementAdapter`、`FinalIkPresentationAdapter`、`CinemachineTargetProvider`、`InputSystemIntentSource`、`SaveSnapshotExporter`。
4. 表现层：Animator、模型骨骼、IK、特效、音频、UI 锚点、镜头目标点。允许依赖 Unity 组件和表现插件，但不能拥有移动/战斗/剧情权威。
5. 工具层：Odin Inspector、层级模板、批量绑定、校验器、Prefab 生成器。只写装配和检查结果，不参与运行时决策。

## 对“从 0 构建完整角色层级模板”的插件约束

- 角色根可挂 `CharacterActor`、身份/生命周期、模块注册器；不要直接挂相机、存档模块、UI 控件。
- 运动根可挂 KCC motor/capsule 和 KCC adapter；外部只通过 motion request 或 motion driver 操作。
- 表现根只放模型、Animator、骨骼、Renderer、StateMachine/IK driver 绑定；不要把 KCC/Input/Save 塞进骨骼节点。
- 目标点层必须稳定：`CameraTarget`、`AimTarget`、`LookAtTarget`、`InteractionProbe`、`HitSockets`、`WeaponSockets`。Cinemachine/FinalIK/战斗检测都绑定这些点，而不是运行时深层 `Find`。
- 输入源必须可替换：本地玩家、AI、网络代理、剧情导演、回放系统都写入统一 intent。
- 剧情/Timeline/切角色必须经过 `ControlAuthority`，不能直接禁用脚本或写 Transform。
- 存档只拿角色快照，EasySave3 不进入角色 prefab 核心组件。

## 禁止误操作

- 不要因为 `ESPlayer` 目录存在就认定玩家主逻辑在其中；当前主逻辑仍大量位于 `Assets/Scripts/ESLogic/Runtime/Entity`。
- 不要恢复旧 `Assets/Plugins/ES/2_Feature/ESGameCore` 作为新 GameManager。
- 不要继续扩大 `EntityBasicModules.cs` 承担所有运动/战斗/输入职责。
- 不要把 DOTween 用作角色权威位移。
- 不要让角色核心直接读取 `InputAction`、直接调用 `ES3`、直接控制 Cinemachine、直接依赖 FinalIK solver。
- 不要删除或重命名第三方插件目录来“简化依赖”；先通过 asmdef 和代码边界隔离。

## 可能过时点，必须源码复核

- `ES_Logic.asmdef` 的第三方引用可能继续变化，改架构前必须重读。
- `ESPlayer.asmdef` 当前使用 GUID 引用，后续若改为命名引用，需要重新确认对应程序集。
- Cinemachine 当前角色侧直接引用较少，但相机系统可能后续补齐；不要只按本文件判断最终耦合。
- Luban/MemoryPack/UniTask 当前更多是可用工具链，不代表已在角色主线形成稳定方案。
- `【必须】玩家_大黑塔_工业级层级模板` 当前更像场景内模板/说明对象，不是已经可直接复用的完整玩家 prefab。
