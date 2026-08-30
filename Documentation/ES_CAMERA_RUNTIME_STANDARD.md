# ES Camera Runtime 标准

状态：Core、Track、Timeline、Preview 均有源码；临时全量 Runtime/Editor 构建为 0/0，但 Tests 程序集被无关音频测试错误阻断。当前 IDE 生成的 `.csproj` 未同步全部新增文件；它不是 Unity 编译输入，不能据此判断 Unity 是否收录或编译成功。Unity Editor 域重载、Test Runner、PlayMode、Profiler 与 Player 均未验收。
最后核对：2026-08-02（源码、asmdef 覆盖范围、临时 Runtime/Editor 构建与过期 IDE 工程复核）。
适用源码入口：`Assets/Scripts/ESLogic/Runtime/Camera`、`Assets/Scripts/ESLogic/Editor/Camera`、`ESGameManager.Camera`、`EntityCharacterIdentity`、`EntityAIDomain`。

## 权威边界

```text
Entity / Skill / Vehicle / Timeline / 输入
  -> ESCameraRequest + ESCameraLease
  -> ESCameraModule（本地观测权、模块生命周期、唯一业务门面）
  -> ESCameraDirector（每 View 活跃集合、仲裁、Modifier 合成）
  -> ESCameraCinemachine2ViewAdapter（唯一 CM2 写入点）
  -> CinemachineBrain / Virtual Camera
```

- `ESGameManager.Camera` 暴露的是 `ESCameraModule` 门面，不再裸 `new ESCameraDirector()`；Director 由模块持有并随模块启停 Dispose。
- 业务只可创建、更新、释放 `CameraRequest/Lease`，不得取得 VCam，也不得直接写 Priority、Follow、LookAt、Axis 或 Blend。
- `ESCameraDirector` 是仲裁权威；`ESCameraCinemachine2ViewAdapter` 是经其授权的唯一 CM2 写入者。
- `EntityCharacterIdentity` 只提供默认 Base 的内容意图；只有 `ESGameManager.LocalControl` 当前控制的 Entity 才能实际申请它。普通 AI/NPC 即使填了 DefinitionKey 也不能进入 `MainView` 仲裁。
- 当前版本没有回放、观战、剧情的外部观测授权能力：模块只接受 `LocalControl` 当前 Entity；没有公开或 `internal` 的外部 Owner 注册 API。对应受信 Bridge 尚未实现前，非 Entity Owner、AI 载具、AI 技能和 Modifier 请求均在模块门面直接拒绝。
- `ESCameraSceneBinding` 是每个 View 唯一场景挂载组件，保存 Output Camera、Brain、Catalog、RigRoot 与 Scene Epoch。角色、载具、技能不新增 Camera Controller MonoBehaviour。
- `RigRoot` 必须独立于输出 Camera 的变换层级；默认制作工具将二者建为场景级兄弟节点，禁止形成父子变换反馈。
- Core 请求/Lease 契约无 Cinemachine 类型；CM2 类型只可存在于 ES Camera 模块内部的 `Runtime/Camera/Cinemachine2`、`Runtime/Camera/Scene`、`Runtime/Camera/Preview` 与 Camera 编辑器工具中。

## 请求、租约与仲裁

- Base/Shot 维护活跃请求集合，不使用“压栈/弹栈恢复”。每次 Push、Update、Release、Owner 失效都会重算赢家。
- Base/Shot 的赢家顺序：`priority` → `Shot` 优于 `Base` → `submissionSequence`（确定性 Tie-break）。
- Lease 包含 `ViewId + SceneEpoch + Slot + Generation`。旧 Lease、Owner 回池、旧场景均无法释放复用槽位或新 View 的请求。
- `ESCameraLease.Dispose()`、`TrySetLook()` 和 `TrySetTarget()` 是业务的语义化入口；它们仍经模块做本地观测权、Scene Epoch 与 generation 校验。`TrySetLook()` 还要求 Lease Owner 与当前赢家 Owner 一致。
- Owner 被销毁或其 Component/GameObject 失活时，Director 会在提交点清理请求。
- 普通操作只标记脏状态，由 `ESGameManager.LateUpdate` 的 `Camera.LateTick()` 统一提交；`FlushNow` 仅允许明确剧情切镜边界。

## Modifier 合成

Modifier 不抢占 Base/Shot。它只对当前赢家且 `compatibleDefinitionKey` 匹配时生效。

| 字段 | 支持操作 | 规则 |
| --- | --- | --- |
| FOV | Override / Add / Multiply | Override 按优先级与稳定序号取胜；Add/Multiply 聚合 |
| Distance Scale | Override / Add / Multiply | CM2 FreeLook 基于 Rig 初始 Orbit 半径执行 |
| Shoulder Offset | Override / Add | 通过 CameraOffset 扩展执行 |
| Shake Amplitude | Override / Add / Multiply | 仅对配置 Perlin Noise 的 Rig 生效 |

相机视图定义提供字段基线；最终值为 `((Override 或 Base) + Add) * Multiply`。这不是依赖调用顺序的隐式覆盖。

## 内容与场景

- `ESCameraViewDefinition`：`DefinitionKey -> RigKey + 输入/镜头基线`。
- `ESCameraRigCatalog`：`RigKey -> Rig Prefab`；禁止保存场景实例。
- `ESCameraViewDefinitionCatalog`：`DefinitionKey -> 相机视图定义`。
- `ESCameraSceneRigRegistry`：仅属于一个 SceneBinding，按需实例化当前场景 Rig；销毁 Binding 时销毁全部实例。
- `ESCameraDefaultContentBuilder` 已有玩家/载具内容构建源码；实际 `player.third_person`、`vehicle.chase` 资产是否已生成并被测试场景引用，仍须在 Unity 内核验，构建器源码不能替代资产证据。

## TrackView 编辑器预览

```text
SkillTrackItem_Camera（Runtime 纯描述）
  -> ICameraTrackPreviewFactory（Runtime Contract）
  -> Editor Bootstrap / ESCameraTrackPreviewSession
  -> 独立 ESCameraPreviewView / Brain / RigRegistry / Lease
  -> ESCameraTrackPreviewWindow（只读渲染面板）
```

- Runtime 的 `ICameraTrackPreviewFactory` 只包含接口、请求描述与稳定 `DefinitionKey`；不引用 `UnityEditor`、`ESTrackViewWindow` 或 Cinemachine Editor 类型。
- Editor Bootstrap 使用 `[InitializeOnLoad]` 幂等注册 Factory。`SkillTrackItem_Camera` 没有 Factory 时退回常规轨道预览，不会在 Runtime 层创建相机对象。
- 每个 Camera Track 的 `ESCameraTrackPreviewSession` 自己拥有 Preview View、Brain、RigRoot、Director、Scene Epoch 与所有 Lease；不调用 `ESGameManager.Camera`，不修改制作场景中的正式 VCam。
- 轨道停止、TrackView 重建/关闭、PlayMode 切换、脚本重载和编辑器退出均通过既有预览生命周期统一 Dispose。独立输出面板关闭不销毁轨道会话，旧 Lease 不会跨预览会话生效。
- `ESCameraTrackPreviewWindow` 只从当前 `EditorSequencePlayer` 发现已有的 `CameraTrackEditorSampler` 并渲染其输出；它不创建 Request、不改 TrackView 播放状态，关闭该面板也不会停止轨道。
- 同一 Camera Track 的预览片段必须解析到同一对 `DefinitionCatalog/RigCatalog`；有重复 Key 或跨 Catalog 混用时拒绝猜测，显式提示拆轨或统一内容目录。

## Timeline 单一路径（待完整实现）

- `ESCameraTimelineRequestBridge` 已定义 `Push -> Update -> Release` 的唯一候选路径；尚无可用的 Timeline PlayableBehaviour/Clip Authoring，因此不得宣称 Timeline 相机已交付。
- `ESCameraTimelineShot` 只保存 `ViewId`、`DefinitionKey`、优先级、Owner、Follow 与 LookAt；它转换为普通 `Shot` 进入相同的 Director 活跃集合，不拥有旁路优先权。
- 后续 Timeline PlayableBehaviour 必须由模块私有的受信 Timeline Bridge 管理外部观测生命周期，再只调用这座桥；在该 Bridge 实现前 Timeline Request 一律不能影响正式 View。禁止引入 Cinemachine Timeline Track，禁止直接写 VCam Priority、Follow、LookAt、Axis 或全局 Blend。
- `CinemachineBlenderSettings` 的相机对 Blend 解析及内容验证尚未实现；当前不能宣称“按相机对 Blend”已经生效。

## 当前完成与未完成项

Core 源码已具备：View/Lease、Base/Shot 仲裁、Modifier 合成、Scene Epoch、CM2 Adapter、`ESCameraModule` 生命周期门面、仅当前本地 Entity 的请求与 Look、受控 Entity 的默认 Base、驾驶镜头接线、技能 Camera Clip（池化 `CameraClipRuntimeState` 释放 Lease）、Director Lease 行为测试，以及 Module 未登记 Owner/非本地 Entity/当前本地 Entity 测试源码。

源码存在但尚未由 Unity 生成工程收录/域重载验收：`ESCameraModule`、TrackView 独立 Preview Factory/Session/渲染面板、SkillTrackItem_Camera 与 Timeline Request Bridge。尚未交付：具体 Timeline PlayableBehaviour/Clip Authoring、相机对 Blend、已生成玩家/载具内容资产的场景证据、锁敌策略、遮挡预算、随机乱序 PlayMode、Profiler/Player/IL2CPP 证据。

不得把“源码存在”表述为“相机系统已冻结或已通过 PlayMode”。

## 相机需求支持矩阵（vNext）

本矩阵冻结“全部支持”的需求边界；每项必须能映射到现有
`ESCameraRequest / ESCameraLease / ESCameraDirector / ESCameraViewDefinition /
ESCameraCinemachine2ViewAdapter`，不得新增旁路 Camera Controller。

### Blend 支持边界

`ESCameraSceneBinding` 已将 `defaultBlend` 与 `CinemachineBlenderSettings` 注入同一
`CinemachineBrain`，因此 Blend 配置不会由业务 Request 旁路写入。当前支持边界为：

- 默认 Blend：由 Binding 的 `defaultBlend` 提供；
- 自定义 Blend：由 Binding 的 `blenderSettings` 提供；
- 业务切镜：仍只提交 Base/Shot Request，由 Director 选择唯一赢家；
- 禁止业务直接修改 Brain、VCam Priority 或全局 Blend。

尚未证明的部分：BlenderSettings 中的相机对是否覆盖全部 Definition/Rig 组合、切镜
时序是否符合目标曲线，以及 PlayMode/Player 下的实际视觉结果。未取得这些证据前，
Blend 只能标记为“源码已接线、运行时未验收”。

| 需求域 | 必须支持的行为 | 权威入口 | 最小验收证据 | 当前状态 |
| --- | --- | --- | --- | --- |
| 玩家跟随 | 目标枢轴、肩部偏移、距离、FOV、阻尼和帧率无关输入 | `ESCameraViewDefinition` + `ESCameraModule` | 不同刷新率与灵敏度回放；记录输入步长和最终轴值 | 源码存在；Runtime 未验收 |
| 遮挡回缩 | 球/胶囊等价探针；墙角、窄门、坡面回缩；无可见点安全降级 | `ESCameraViewDefinition` + CM2 Adapter Collider | 命中层、半径、最小距离、回缩/恢复耗时与截图/回放 | 配置与写入存在；Unity 未验收 |
| 目标生命周期 | 销毁、回池、失活、换场景、传送后旧 Lease 失效并转安全锚点 | `ESCameraDirector` SceneEpoch/Generation + Module | 目标失效事件、旧 Lease 拒绝、冻结/转移轨迹 | 静态合同存在；Runtime 未验收 |
| 请求仲裁 | `priority → Shot > Base → submissionSequence` 唯一赢家；Modifier 仅作用于赢家 | `ESCameraDirector` | 同帧多 Owner 重放，输出唯一且序列稳定 | 有 Director 测试；扩展场景未验收 |
| 构图适配 | FOV、宽高比、安全框、兴趣点在 16:9/21:9/窄屏下保持可读 | `ESCameraViewDefinition` + Rig 内容 | 分辨率矩阵截图；角色与目标不越安全框 | 字段存在；Player 未验收 |
| 模式扩展 | Player/Vehicle/Shot/Timeline/Preview 复用同一请求与资源链 | Module/Director/Catalog/Preview Session | 模式切换、释放、预览隔离和资源实例计数 | Player/Vehicle 部分存在；Timeline Authoring 未交付 |
| 性能预算 | 仅赢家执行避障；稳态不实例化 Rig；LateTick 同帧只提交一次 | Director + Adapter + RigRegistry | Profiler 记录查询次数、分配、实例化峰值和降级原因 | 静态约束存在；Profiler 未运行 |

### 统一失败处理

- 无有效目标：冻结最近安全姿态，随后平滑到默认安全锚点；禁止回退到未验证根节点或骨骼。
- 无可见点：按“最近可见点 → 替代视点 → 最后安全姿态”降级，并记录稳定失败码。
- Definition/Rig/Collider 缺失，或启用避障却未配置碰撞层：拒绝应用该请求并报告错误；不得静默创建组件、按路径加载或猜测替代资产。
- 旧 Lease、失活 Owner 或旧 SceneEpoch：拒绝更新/Look/Release 对新槽位的影响。

### 完成声明边界

满足本矩阵的静态字段、类型和测试，只能证明源码/合同覆盖；不能替代 Unity 域重载、Prefab 序列化、PlayMode、设备舒适度、Profiler、Player、IL2CPP 或发布证据。未取得对应证据的项必须保持 `runtime-not-run` 或 `unproven`。

## 3A体感强化合同（保持ES命名与权威）

本节只吸收外部AAA制作中的可迁移效果与验证方法，不复制外部团队组织、类名、目录或资源加载体系。现有 `ESCameraSceneBinding`、`ESCameraModule`、`ESCameraDirector`、`ESCameraViewDefinition`、`ESCameraViewDefinitionCatalog`、`ESCameraRigCatalog`、`ESCameraSceneRigRegistry`、`ESCameraCinemachine2ViewAdapter` 和 `ESCameraTimelineRequestBridge` 的身份保持不变。

### 权威不变量

- ES 请求、Lease、Director 和 Catalog 是唯一业务权威；Cinemachine 2.10.5 只是经 `ESCameraCinemachine2ViewAdapter` 授权的执行后端。
- 相机 Rig 只能由 ES 资源链解析：Resource/Manifest/Bundle Index → `ESCameraRigCatalog` → `ESCameraSceneRigRegistry`；业务代码和 Cinemachine 不得自行按路径加载相机资源。
- 每个正式 View 只能由一个 `ESCameraSceneBinding` 注册；Output Camera、CinemachineBrain、Catalog、BlendSettings 与 RigRoot 必须属于同一 Binding 生命周期。
- 所有相机输入、切镜和表现修饰均转为 `ESCameraRequest` / `ESCameraLease` / Modifier；禁止直接写 VCam Priority、Follow、LookAt、Axis、Blend。
- 预览和正式运行时拥有独立 Director、Binding、RigRegistry、SceneEpoch 与 Lease；预览不得修改正式 View。

### 体感目标（不新增类型）

- `ESCameraViewDefinition` 提供跟随距离、肩部偏移、FOV、输入灵敏度、阻尼和遮挡基线。
- `ESCameraDirector` 只负责请求仲裁与 Modifier 合成，不承载 Cinemachine 具体实现。
- `ESCameraCinemachine2ViewAdapter` 负责 Follow/Aim、Collider、Lens、Noise、Impulse 和 Blend 的执行写入。
- Player、Vehicle、Shot、Timeline 等模式通过现有 DefinitionKey、RigKey、Base/Shot/Modifier 和优先级表达，不建立平行模式系统。
- 墙角、窄门、斜坡、楼梯、目标丢失、快速转向和分辨率变化必须在代表性灰盒中提前验证。

### 性能与扩展预算

- `LateTick` 是唯一正常提交点；同帧重复调用不得重复写入 Adapter。
- 稳态 Push/Update/Release 不触发 Cinemachine Rig 实例化；实例化与 Modifier 基线建立只发生在 Binding 预热或首次解析阶段。
- 只有当前获胜 Rig 执行 Collider 和 Modifier 应用；不为非赢家持续执行避障或镜头修饰。
- 新增镜头模式必须复用现有 Request/Definition/Catalog/Adapter 链，不能增加第二个 Director 或资源入口。
- 未取得 Profiler、PlayMode 和 Player 证据前，不得声明零GC、商业级帧率、设备舒适度或发布通过。

### 强化验收矩阵

|范围|必须证明|禁止替代的弱证据|
|---|---|---|
|权威|业务只能通过 ES Module/Director/Lease 控制镜头|Cinemachine Inspector 可见或场景里存在 VCam|
|资源|RigKey 经 ES Catalog 和 SceneRigRegistry 解析、预热、释放|Prefab 路径存在或手工拖拽引用|
|体感|碰撞回缩、遮挡恢复、切镜、目标丢失和输入响应|静态字段配置或单张截图|
|性能|稳态无新增分配、查询预算和实例化峰值可重放|源码“看起来没有 new”|
|扩展|Player/Vehicle/Timeline/Preview 不旁路且可独立回收|复制一套 Camera Controller|
