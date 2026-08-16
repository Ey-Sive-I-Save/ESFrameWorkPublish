# ESFramework API 命名复核报告

状态：前五批低风险源码迁移已经形成，候选复核继续；不构成机械批量改名授权。

最后验证：2026-08-13，前五批均完成声明、实现、调用方、测试、资产文本与现行文档引用扫描；严格 UTF-8 与差异检查通过，生成工程证据见下文。

适用源码入口：`Assets/Plugins/ES`、`Assets/Scripts/ESLogic` 中的公开业务 API、可见工具入口及其直接调用链。

## 目的与证据边界

本报告承载会随源码变化的复杂用词候选、实现事实和迁移建议。长期命名原则以 `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md` 为准。

- 候选不等于违规；任何结论都必须同时回看声明、实现和调用点。
- 本轮只授权按 A/B/C/D 分级，在完整引用扫描后推进命名迁移；不授权机械批量改名、兼容包装或序列化迁移。
- UTF-8 与 `git diff --check` 只证明文档质量，不证明候选判断正确。
- 静态编译只能证明相应程序集源码可编译，不证明 Unity 资产迁移、ReloadDomain 或运行行为。
- `ValidatedNow` / `AcceptedContext` 只描述会话上下文，不属于命名治理验收证据。
- `Obsolete`、`Generated`、测试名称、第三方接口实现和 Unity/KCC/Odin 固定回调默认排除；只有它们泄漏到现行业务入口时才重新纳入。

## 当前结论

方向为“有条件通过”：项目级动词语义边界有价值，`Submit`、`Resolve`、`Process` 与 `Execute` 均不应机械禁用。前五批 11 个已确认治理问题已经完成源码与现行文本迁移，但尚无 Unity Editor、ReloadDomain 或运行证据；其余条目继续按调用链复核，不得宣称命名治理完成或可发布。

## 分级口径

| 等级 | 情况 | 处理 |
|---|---|---|
| A | 策划字段、Inspector、菜单、业务高频 API 明显难懂 | 优先整改 |
| B | 公共协议名与真实职责不符，可能误导后续架构 | 按完整调用链迁移 |
| C | 内部或低频名称不够好，但职责尚可理解 | 登记，相关修改中顺带处理 |
| D | 私有实现、第三方回调、生成代码、历史代码 | 默认不动 |

## 第一批已处理

| 原名称 | 分级与判定 | 当前名称 | 修改范围 | 剩余证据 |
|---|---|---|---|---|
| `VehicleController.SubmitDriverInput(...)` + `EntityMountable.SubmitDriverInput(...)` | A + B；每帧业务入口，校验驾驶权后设置输入，且属于成对协议 | 两层统一为 `TrySetDriverInput(...)` | Controller、Mountable、AI Domain 调用链、载具现行规则与合同 | Unity 编译、相关 EditMode/PlayMode 驾驶输入验证 |
| `Entity.SubmitCameraLook(...)` | A；本地控制权与 Lease 可能拒绝，效果是设置 Look 输入 | `TrySetCameraLook(...)` | Entity 与 AI Domain 调用链、载具合同 | Unity 编译、Camera Director/Lease 运行验证 |
| `ItemMotionModule.SubmitShotResult(...)` | B/C；模块间结果交接，但实现仅写 Pending 状态且没有提交裁决 | `SetPendingShotResult(...)` | Item Motion 声明与 Shot 调用点 | Unity 编译、Item/Shot EditMode 与 PlayMode 验证 |

本批没有保留旧名转发包装，也没有修改序列化字段、类型名、`.meta`、Prefab、Scene 或资产 YAML。

## 第二批已处理

| 原名称 | 分级与判定 | 当前名称 | 修改范围 | 剩余证据 |
|---|---|---|---|---|
| Track Editor Preview `SubmitClipState(...)` | B/C；每次 Clip 采样更新当前预览帧的请求状态并立即计算 `SetActive`，没有身份、版本、拒绝或事务提交边界 | `UpdateClipPreviewState(...)` | Track Sampler 声明与唯一 Clip Sampler 调用点 | Unity 编译、重叠 Clip 编辑器预览验证 |
| `ESPhysicsLayers.ResolveShotHitMask(...)` | A/C；单步返回显式配置或项目默认 Shot Mask，不存在多来源消歧 | `GetShotHitMask(...)` | Physics Layers 声明与 Item Shot 两个调用点 | Unity 编译、Shot 命中层 EditMode/PlayMode 验证 |
| Texture/Sprite Tool `ProcessSelectedTextures()` | A；可见按钮实际应用 TextureImporter 设置，来源同时包含 Project 选区和配置文件夹，旧名既抽象又错误限定来源 | `ApplyTextureImportSettings()` | 编辑器按钮调用与同页方法声明 | Unity 编译、工具页人工验证（会修改资产，未在本轮执行） |

本批同样没有保留旧名转发包装；没有修改序列化字段、类型名、`.meta`、Prefab、Scene 或资产 YAML。静态 HTML 受 `DOCUMENT_SYNC` 管理，未机械改写。

## 第三批已处理

| 原名称 | 分级与判定 | 当前名称 | 修改范围 | 剩余证据 |
|---|---|---|---|---|
| `StateMachine.ExecuteStateActivation(...)` | B；只由同一 StateMachine 的 `TryActivateState` 调用，把预检结果应用到状态机，并会因 Tag、Layer 或运行异常返回失败；不是 Runner 的通用执行入口 | `TryApplyStateActivation(...)` | StateMachine 声明、两个内部调用点、异常诊断文本与状态成本现行指南 | Unity 编译、状态激活/中断/回滚 EditMode 与 PlayMode 验证 |

本批没有保留旧名转发包装；没有修改序列化字段、类型名、`.meta`、Prefab、Scene 或资产 YAML。静态 HTML继续按 `DOCUMENT_SYNC` 边界保留当前快照。

## 第四批已处理

| 原名称 | 分级与判定 | 当前名称 | 修改范围 | 剩余证据 |
|---|---|---|---|---|
| `ESAudioClipPlayConfig.GetEffectiveCategory(...)` | C；只返回当前覆盖值或调用方默认值，`Effective` 没有增加业务信息 | `GetCategory(...)` | Audio Runtime 声明、唯一业务调用点与对应测试断言 | Unity 编译、Audio EditMode 测试 |
| `ESAudioClipPlayConfig.GetEffectiveSpatialMode(...)` | C；只返回当前覆盖值或调用方默认值，`Effective` 没有增加业务信息 | `GetSpatialMode(...)` | Audio Runtime 声明、唯一业务调用点与对应测试断言 | Unity 编译、Audio EditMode 测试 |
| Shot Inspector `HitResolver Tag 条件` | A；策划字段暴露内部 Resolver 架构词 | `命中 Tag 条件`，并将 Tooltip 改为“命中判定” | Inspector 可见 Label 与 Tooltip；序列化字段名保持不变 | Unity Inspector 人工验证 |

本批没有保留旧名转发包装；没有修改序列化字段名、类型名、`.meta`、Prefab、Scene 或资产 YAML。

## 第五批已处理

| 原名称 | 分级与判定 | 当前名称 | 修改范围 | 剩余证据 |
|---|---|---|---|---|
| `MatchTargetGizmosDrawer.Submit(...)` | C；Editor-only 诊断路径按 Key 覆盖本帧字典数据，没有身份、拒绝或状态推进 | `SetFrameData(...)` | Drawer 声明、文件说明、唯一 StateBase 调用点及相邻注释 | Unity 编译、MatchTarget Scene Gizmos 人工验证 |

本批没有保留旧名转发包装；没有修改序列化字段、类型名、`.meta`、Prefab、Scene 或资产 YAML。

验证现状：

- 前五批 11 个治理问题涉及的精确旧 API 与 Inspector 文案，在限定活跃范围内已归零；旧内容只保留在本报告的迁移记录、历史协作记录和尚未按同步台账刷新的静态 HTML 快照中。这里的 11 是治理问题数，不是唯一 C# 旧符号数：驾驶输入包含两个旧 API，另有一项是 Inspector 文案。
- `ES_Design.csproj` 的 `dotnet-build` 通过，0 警告、0 错误。`ES_Logic.csproj` 被当前共享工作树的 66 个既有缺失类型错误阻断，主要涉及未被生成工程收录的 Motion Influence、VFX、Enum/String Mirror Map 与 Transform Mapping Conflict 类型；`ES_Design.ConfigKey.Tests.csproj` 因依赖 `ES_Logic` 同样失败。失败列表暂未发现本轮精确旧 API 对应的 `CS1061` / `CS0117`；由于整体编译失败，这不能证明调用链已经完整编译。
- 当前环境没有可调用的 UnityMCP，未取得 Unity Editor Console、ReloadDomain、EditMode 或 PlayMode 证据。
- 项目内活跃调用已经同步，但这些方法原本是 `public`；若存在仓外程序集或尚未导入的包直接调用旧名，会在升级后产生源码兼容断点。本批按“不保留无职责永久兼容包装”的项目规则处理，尚无仓外消费者清单证据。

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

## 本轮复核后保留

| 名称 | 源码事实 | 结论 |
|---|---|---|
| `EntityWeaponBinding.ResolveHandMount(...)` | 根据 Hand Mount Policy 在武器挂点、角色稳定 Socket、默认枚举挂点和 Combat Fallback 间按顺序选择 | 真实跨来源策略解析，保留 |
| `ESInteractable.ResolveInteractionPoint(...)` | 在显式交互点、Collider ClosestPoint 和组件 Transform 三种来源间选择 | `Resolve` 能表达多来源回退，保留 |
| Odin `ProcessSelfAttributes(...)` / `ProcessChildMemberAttributes(...)` | Odin Attribute Processor 固定扩展回调 | 第三方协议名，D 级默认不动 |
| KCC `ProcessHitStabilityReport(...)` | KCC 固定控制器回调 | 第三方协议名，D 级默认不动 |
| `ESAutomationTaskContract.ResolveCapabilities()` | 把字符串协议集合解析为 Flags，拒绝重复项与未知项 | 存在协议解析与校验，保留 |
| SoTable `ExecuteAllEnabledBatches()` / `ExecuteUseBatch(...)` | 用户确认风险后执行导入、导出或组合批次，并包含计划预览分支 | `Execute` 对应真实工具命令，保留 |

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
