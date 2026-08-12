# ESFramework API 命名复核报告

状态：候选复核中，不构成改名授权。

最后验证：2026-08-13，源码只读检查。

适用源码入口：`Assets/Plugins/ES`、`Assets/Scripts/ESLogic` 中的公开业务 API、可见工具入口及其直接调用链。

## 目的与证据边界

本报告承载会随源码变化的复杂用词候选、实现事实和迁移建议。长期命名原则以 `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` 为准。

- 候选不等于违规；任何结论都必须同时回看声明、实现和调用点。
- 本轮没有授权批量改名、兼容包装、序列化迁移或源码修改。
- UTF-8 与 `git diff --check` 只证明文档质量，不证明候选判断正确。
- 静态编译只能证明相应程序集源码可编译，不证明 Unity 资产迁移、ReloadDomain 或运行行为。
- `ValidatedNow` / `AcceptedContext` 只描述会话上下文，不属于命名治理验收证据。
- `Obsolete`、`Generated`、测试名称、第三方接口实现和 Unity/KCC/Odin 固定回调默认排除；只有它们泄漏到现行业务入口时才重新纳入。

## 当前结论

方向为“有条件通过”：项目级动词语义边界有价值，`Submit` 不应机械禁用。当前只有确认合理项和待复核候选，尚未形成新的“已确认问题”，也没有命名治理完成或发布结论。

## 确认合理

| 名称 | 源码事实 | 结论 |
|---|---|---|
| `ESMotion.AddVelocity(...)` | 对技能、Zone 和碰撞直接表达增加速度；接收器解析和运动权威隐藏在底层 | 高频入口清晰，保留 |
| `ESStoryModule.SubmitContinue(...)` / `SubmitOption(...)` | Submission 带实例、会话、Revision 和选项身份；接收方会拒绝过期输入并推进 Story 状态 | 符合 `Submit` 语义，保留 |
| `ESAutomationFacade.SubmitInput(...)` | 跨 Facade/Endpoint 权威边界，输入带运行身份和 Schema 约束，并有拒绝/等待流程 | 符合 `Submit` 语义，保留 |
| `EntityEquipmentAttachmentModule.TryCommit(...)` | 校验 Transition Stamp，挂载前后复核代际，失效时恢复原父级和局部变换 | 具备真实事务提交边界，保留 |
| `ESTagCollection.Acquire(...)` / `IESMotionInfluenceReceiver.TryAcquireField(...)` | 分别产生必须释放且带代际保护的 Tag Lease 与 Motion Field Lease | `Acquire` 表达真实所有权，保留 |
| `ESMotionInfluenceReceiverResolver.TryResolve(...)` | 在父级 Core、VehicleController 和显式 Receiver 间按权威顺序解析 | 真实多来源解析，且隐藏在简单业务入口后，保留 |
| `ESStoryDefinitionCatalog.TryResolve(...)` | 通过稳定 Key、内容版本和签名解析不可变 Snapshot | `Resolve` 准确，保留 |
| 私有 `VehicleController.DispatchVelocity(...)` | 遍历已注册运动能力并遵守确定顺序 | 属于底层派发实现，不是业务高频入口，保留 |
| `VehicleController.RegisterMotionFeature(...)` | 向多个 `ESWorkScheduler` 注册能力并返回可释放 Registration | 注册/注销生命周期真实存在，保留 |
| `VehicleController.ProcessHitStabilityReport(...)` | KCC 规定的接口回调 | 外部固定合同，不纳入改名 |

## 待复核候选

以下项目只表示“值得继续检查”，不预设最终名称或违规等级。

| 候选 | 已确认实现事实 | 尚缺检查 | 可评估方向 |
|---|---|---|---|
| `VehicleController.SubmitDriverInput(...)` + `EntityMountable.SubmitDriverInput(...)` | 两层都会校验当前驾驶者；底层最终调用 `inputState.Set(...)`，属于每帧输入链 | 全部调用方、公开合同、测试、文档与兼容影响；必须成对评估 | `TrySetDriverInput(...)` 或保留 `Submit`，取决于调用方是否需要理解权威拒绝语义 |
| `Entity.SubmitCameraLook(...)` | 检查本地控制权后调用 `ESCameraLease.TrySetLook(...)` | 输入链全部调用方、Camera 契约和测试；确认是否属于仲裁请求而非普通设置 | `TrySetCameraLook(...)` 或保留 `SubmitCameraLook(...)` |
| `ItemMotionModule.SubmitShotResult(...)` | 只写 `_pendingResult` 并设置 `_hasPendingResult` | Pending Result 消费时序、模块边界和是否会扩展拒绝语义 | `SetPendingShotResult(...)` 或 `ApplyShotResult(...)` |
| Track Editor Preview `SubmitClipState(...)` | 缓存原始 Active，合并同一采样帧多个 Clip 请求并写最终状态 | 多 Clip 冲突规则、调用频率和编辑器可见性 | `SetClipActiveRequest(...)`，或保留并补充仲裁职责说明 |
| `MatchTargetGizmosDrawer.Submit(...)` | 同一 Key 每帧覆盖开发诊断数据 | 无高优先级缺口；属于私有诊断路径 | 低优先级评估 `SetFrameData(...)`，也可维持现状 |

## 既有语义债务候选

这些名称已经进入资产、序列化或较大 API 面，只登记风险，不授权迁移：

- `ActionTemplateDataInfo` / `ActionTemplateDataGroup` 实际进入正式 Action Definition 注入链，后续需评估 Definition 正名与 ScriptableObject 迁移风险。
- `ESWeaponSceneTemplate.RuntimeBridgeSection` 当前只是作者引用分组，不是独立 Runtime Service；不得据此复制新的 `*RuntimeBridge` 转发层。
- `ESWeaponTemplateFireKind` 只是模板布局提示，正式武器身份仍以 `ItemWeaponSharedData.weaponKind` 为准。
- `EntityWeaponBinding.fireOrigin`、`fireStateKey` 是远程字段，不能为近战复用而冒充通用 Attack 绑定。

## 推进顺序

1. 继续扫描 Runtime 业务调用面中的 `Submit/Process/Execute/Resolve/Acquire/Commit`，逐项回看实现与调用点。
2. 扫描 Inspector、菜单和 AICommand 的可见文案，其优先级高于私有实现名称。
3. 检查 `Manager/Bridge/Context/Request/Result/Payload` 等后缀是否承担真实职责。
4. 对每个候选补齐调用频率、唯一权威、序列化风险、测试范围和兼容影响后，再决定“合理 / 问题 / 继续观察”。
5. 若要改名，必须按一个协议或一个完整调用链形成独立实施批次；不得与玩法开发混改。

## 潜在迁移门禁

- 涉及 Unity 序列化类型或字段时，先统计资产和引用，设计 `MovedFrom` / `FormerlySerializedAs` / `.meta` GUID 保留方案，并建立受控备份。
- VehicleController 与 EntityMountable 的驾驶输入属于成对协议，声明、调用方、接口、测试和文档必须同步评估。
- 静态编译、Unity 资产加载、ReloadDomain、EditMode、PlayMode 和 Player 证据必须分开记录。
- 禁止为了兼容新增只有转发作用的永久旧名包装；是否保留兼容入口需要当前明确授权。
