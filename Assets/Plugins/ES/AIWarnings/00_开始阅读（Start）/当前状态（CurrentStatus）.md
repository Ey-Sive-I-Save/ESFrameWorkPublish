# AIWarnings 当前状态

最后核对：2026-08-10。

## 已确认基线

- AIWarnings 采用按任务分层加载：`README -> CurrentStatus -> RuleIndex -> 命中的 P0 -> 当前领域专项 -> 必要的交接/复盘或提案`。普通任务禁止递归读取全目录；P0、现行状态和任务专项必须读取原文，分批摘要只能导航，不能替代规则权威。
- 模块成熟度治理已建立统一状态、半成品隔离规则和状态跃迁证据门禁；`$es-module-lifecycle` 与模块状态审计 AICommand 现支持三个直接触发词：“审计”默认只读并在结束后最多询问一次，“审计并记录”更新固定入口 `ES/Documentation/Status/MODULE_AUDIT_STATE.md` 的目标模块块，“继续审计”从该入口恢复并先复核事实。检查点记录当前状态、Git 基线、证据缺口、下一动作和失效条件，但不代表全项目已分类，也不能向下次窗口授予实现权限。

- 2026-08-10 的生成工程静态构建：`ES_Stand.csproj`、`ES_Design.csproj`、`ES_Logic.csproj` 均为 `0 warning, 0 error`；`ES_Design.ConfigKey.Tests.csproj` 为 `2` 个既有 `CS0649` 警告、`0 error`。这只是 `dotnet-build`，不替代 Unity、Test Runner 或 Player 证据。
- Camera Core 的 P0 源码骨架已补：`ESCameraModule` 持有 Director，`ESGameManager.Camera` 只暴露模块门面；当前版本只有 `LocalControl` 当前 Entity 能提交请求与 Look，根本没有外部 Owner 注册 API。回放/观战/剧情需等模块私有受信 Bridge 落地后才可申请正式 View。普通 AI/NPC、AI 驾驶载具及 AI 技能请求会在模块边界拒绝；技能相机以技能使用者而非主目标作为 Owner。`ESCameraLease` 已有 Dispose / Look / Target 语义 API。当前 `ES_Logic.csproj` 已收录 `ESCameraModule`，并在上述 `dotnet-build` 中通过；这不等于 Unity 已导入、域重载或运行验收通过。
- `ESGenericLife` 的 Pool 分部已完成代码接线：唯一 Root、按类型唯一 Extension、新建/预热 Despawn 基线、回调异常收口与 Spawn 内延迟归还均已实现；`ES_Logic.csproj` 已取得静态编译证据，但 Unity Editor、Unity Test Runner 和运行行为仍未验收。
- Entity 模板、挂点与武器挂点链已具备静态闭环；`EntityCharacterIdentity` 是唯一的 Prefab 身份/DataInfo 入口，正式 Variant 自动绑定，通用池模板由租出方直接 `Entity.BindDefinition(...)`。仍需 Unity PlayMode 验证和发布门禁证据，不可仅凭编译签收。
- GameTag 的 `ESTagStableReference` 已统一使用 `ESSearchDropdown` Picker；`ItemDataInfo` 的旧 `ValueDropdown/GetTagOptions` 残留已移除。Tag 测试代码已按当前 NUnit / `IPoolable` 契约修正，但 Unity Test Runner 尚未实跑。
- 输入、对象池、物理查询、Item/Shot 与 Buff 都有运行时实现，是当前较成熟的底座。
- ResourcePlan、Consumer/Library 增量激活、Raw `TextAsset` 入口和资源 Scope 生命周期均已有源码。Scope Registry 已强化为：显式 Domain/合法前缀 StringKey 首次加载自动创建、`CreateScope` 只负责提前登记和父子绑定、默认 `GameSession` 路由、`Presentation` 大型展示域、父子级联释放、内部 Creating/Active/Closing 与 Generation、Resident/Temporary 分名及 Provider Transition 清理；Closing 占位会保持到旧 Scope Dispose 完成，已捕获旧 Scope/TemporaryScope 与 Scene 新请求也统一受 Transition 门禁。真实 `ESAssetScope` 不通过 Registry API 暴露，Runtime Monitor 可观察 Registry/隐式创建/Closing 数量。完整 Unity 编译、PlayMode、父子释放、Provider 切换、Domain Reload、P6/P7/P9 与 IL2CPP Player 证据仍缺失。
- 四种资源模式的源码主控制流和后端分流已形成：EditorDirect 使用 AssetDatabase；EditorSimulateBuild 校验正式发布元数据与 RuntimeMap 后使用 AssetDatabase，不下载 Bundle；LocalBuild 使用本地正式 Bundle；HotUpdate/Net 使用远端清单、缓存、下载与回退。该结论只表示源码链路，不能替代四模式 Unity 运行验收。
- 默认 `ESAssets.LoadAsync(refer)` 已从隐式 Resident 改为自动创建/复用 `GameSession` Registry Scope；`PreloadAsync()` 明确进入 Resident。Owner Scope、ResourcePlan 私有 Scope 与 Temporary Lease 仍保持独立所有权语义。
- `ESAssetDomain` 已在资源运行时 P0 中建立唯一权威语义：`GameInternal` 为框架内部、`ApplicationSession` 为跨 GameSession 的产品会话、`GameSession` 为默认游戏流程、`Presentation` 为短时大内存展示，`Scene/UI/Feature` 仅代表单一共享域，多实例必须使用带前缀 StringKey。当前 Registry 已实现统一机制，但 `GameInternal` 权限限制和各 Domain 到流程管理器的自动释放接线尚未由源码强制，属于 P1 实施缺口，不能写成运行边界已经全部验收。
- Scope Registry 已增加默认自动创建/释放、父子级联、Closing 回调重入和旧 Scope Transition 门禁的 NUnit 测试源码。此前“21 个旧 V1 `CS2001` 路径阻断”的生成工程结论已失效；当前 `ES_Design.ConfigKey.Tests.csproj` 可静态编译，但 Unity Test Runner 尚未执行，不能把“测试已编写”或“程序集可编译”写成“测试已通过”。
- 资源窗口已增加独立的“5. 发布到远端”入口：先读取第四步上传计划并执行只读预检；手动计划 Provider 不再伪报成功，真实 OSS/S3/HTTP Provider 安装前会明确阻断。Root Manifest 仍必须最后切换。
- 第五步窗口提供“初步验证远端隔离区”：真实 Provider 必须只在 `validationPrefix`（默认 `.es-validation`）执行探针上传、HEAD 校验与清理，不得用正式版本目录测试权限。
- 阿里云 OSS Provider 已接入原生签名、流式文件上传、`x-oss-meta-es-sha256` HEAD 校验与隔离探针协议；凭据仅从环境变量读取，Unity 资产不保存 Secret。仍需使用真实测试 Bucket 完成一次网络实跑。
- AI 协作历程已改为用户授权制：只有用户明确要求时才能创建、更新或恢复，普通任务不自动写入；连续约 10 轮后 AI 可以询问一次，但确认前不得修改或催促。已具备本地 Codex session 兜底工具：`Find-CodexSession.ps1` 从 `history.jsonl` 按 ID、主题、日期和 CWD 输出候选绝对路径，`Recover-CodexSessionHistory.ps1` 从已确认的 `rollout-*.jsonl` 重建逐任务时间线，`Test-ESCodexTimelineCoverage.ps1` 对消息数、正式节点、阶段、编号与字段完整性执行机械门禁。恢复器已禁止重复运行时嵌套旧完整时间线或把旧摘要 `Txxx` 误计为正式节点。候选分数仅辅助定位，档案归属仍需人工核对；详见对应 P0。
- `ES/AI协作历程（Codex）/Tools/Complete-ESCodexHandoff.ps1` 已把覆盖校验、Bootstrap Validate、私有 handoff snapshot、新窗口启动、`ContextAccepted` 接收门禁、不可变 handoff receipt 与可选源窗口关闭编排为单一入口。默认不启动窗口；`-OpenNew` 才启动，`-CloseSource` 只有接收成功后才执行。当前仍需真实新窗口接收与关闭流程冒烟，不能把脚本存在当作端到端交接已验收。
- `$es-codex-session-bootstrap` 的当前源码入口固定为可见 `cmd.exe -> codex.cmd`，支持中文任务交接、稳定 `TaskKey`/任务指纹、同任务活跃窗口去重和显式 `-ForceNew`；启动成功可返回进程 ID并尝试从本地 history 登记 session ID。现有冒烟只证明启动、去重和登记路径，不证明新窗口任务内容或 Unity 工程验收通过。

## 角色控制与手感当前状态

- AI Domain 已收口为输入意图解析与写入，KCC 是地面速度和朝向响应的唯一身体执行入口；旧的二次 `moveSmooth`/`lookSmooth` 路径和 `EntityBasicDomain.groundedDefaults` 转发层不应恢复。
- 大黑塔作者基线已统一为 `GroundMovementSharpness=20`、`OrientationSharpness=18`，并同步正式 Prefab KCC、角色模板生成器和 ActorData 资产。
- ActorData 的 `motionShared` 已在 `Entity.BindDefinition(...)` 接入 KCC 作者默认值，包含地面/空中速度、地面响应、朝向响应和跳跃参数，并在 `ClearDefinition`/回池时恢复 Prefab 基线；旧缺省 `speedMultiplier=0` 会回退为 `1`。Character Attribute/ValueChange 仍是更高优先级运行时覆盖层，因此最终值必须由运行时诊断确认。能力开关和输入许可字段尚不能仅凭 DataInfo 字段宣称已接入运行时。
- 手感成熟度保持 `Verifying`。缺少 Unity PlayMode 的起步 T90、松手停止距离、180°反向完成时间，以及 30/60/120 FPS 和 Profiler 证据前，不得称为 Stable 或“3A 手感已完成”。

## Camera 证据缺口

- 2026-08-10 的 `ES_Logic.csproj` 生成工程静态构建为 `0 warning / 0 error`，`ESCameraModule` 已被收录。不得手改生成工程；该结果也不能证明 Unity 的 `ES_Logic.asmdef` 已由 Editor 实际导入。
- 本地观测没有可调用的外部提权入口：模块只接受当前本地 Entity；未来 Replay/Spectator/Cutscene Bridge 必须作为模块私有生命周期实现。`ESCameraModuleAuthorizationTests` 已写入未登记 Owner、非本地 Entity、当前本地 Entity 三个测试源码，并随 `ES_Design.ConfigKey.Tests.csproj` 静态编译；该项目当前有 `2` 个既有 `CS0649` 警告、`0 error`。尚未取得 Unity Test Runner 执行证据。
- 因此 Camera 当前状态严格为“源码与生成工程静态编译已取得证据，Unity 导入、域重载与运行验收待完成”，不是“P0 已收口”或“稳定可用”。

## 构建与运行证据矩阵

源码存在不代表 Unity 已编译，`.csproj` 收录也不代表 Player 已验证。后续 AI 报告状态时必须按下表分层，不得把其中任一步替代另一步。

| 证据 | 当前状态 | 说明 |
| --- | --- | --- |
| 相关源码存在 | 部分已确认 | Raw、资源扩展、资源生命周期、发布 Provider，以及 Camera 首切片源码均存在。 |
| IDE 生成的 `.csproj` 收录 | 部分当前静态证据 | 2026-08-10：`ES_Stand`、`ES_Design`、`ES_Logic` 的生成工程均可静态编译；其中 `ES_Logic` 已收录 `ESCameraModule`。该表不等同于 Unity asmdef 收录状态。 |
| `dotnet build ES_Stand.csproj` | 已通过 | 2026-08-10：`0 warning / 0 error`。禁止手改生成工程；该结果不能替代 Unity 编译。 |
| `dotnet build ES_Design.csproj` 与 `ES_Logic.csproj` | 已通过 | 2026-08-10：均为 `0 warning / 0 error`，仅证明对应生成工程静态编译。 |
| `dotnet build ES_Design.ConfigKey.Tests.csproj` | 已通过但有警告 | 2026-08-10：`2` 个既有 `CS0649` 警告、`0 error`；不是 Unity Test Runner 结果。 |
| Unity Editor 编译 / 域重载 | 未验收 | `.csproj` 编译不能替代 Unity Editor 实际导入与域重载。 |
| Unity Test Runner / PlayMode | 未验收 | `dotnet test` 没有产生 Unity Test Runner 执行结果；尚未得到真实运行证据。 |
| IL2CPP Player | 未验收 | 尚未构建和运行。 |
| 真实 OSS 网络 | 未验收 | Aliyun OSS 实现已存在，仍缺真实测试 Bucket。 |

本轮已取得当前生成工程的静态构建证据，旧的 `CS2001`、`ESCameraModule` 缺失和 `ESAudioCueRuntimeTests.cs:217` 阻断结论已失效。Unity 是否真正收录/编译仍必须看目标 Editor 的域重载与 Console；仍不得用任何 `.csproj` 编译替代 Unity Editor、Unity Test Runner、PlayMode、Profiler 或 Player 验收。不得恢复旧 Raw 类型或手工长期维护 Unity 生成的 `.csproj`。

## 当前优先级

1. 在目标 Unity Editor 实例中触发并观察实际导入、域重载与 Console，确认 `ESCameraModule`、Track/Timeline/Preview 的真实编译状态；随后执行 `ESCameraDirectorTests` 与角色/相机场景的 PlayMode 验收。
2. 验证角色模板、挂点和武器绑定的 Unity 行为，并为基础模板/预览模型补齐发布门禁证据。
3. 在 `Entity + EntityAIDomain + ESGameManager.WorldDomain` 中收口稳定身份、控制源仲裁和世界注册。
4. 执行 ResourcePlan 的 P6/P7/P9 PlayMode 验收。
5. 完成 IL2CPP Player 发布验收。

## 状态解释

- `现行约束`：必须遵守，除非用户明确改变项目规则。
- `已实现事实`：当前源码中存在，仍需按任务验证。
- `联调中`：已有实现，但缺少完整运行或发布证据。
- `待验收提案`：仅为方向，不得宣称已落地。
- `历史复盘`：用于理解决策背景，若与源码冲突则源码优先。

此文件只记录高层状态。具体源码入口、验收标准和 P0 规则请从 `规则索引（RuleIndex）.md` 进入。
