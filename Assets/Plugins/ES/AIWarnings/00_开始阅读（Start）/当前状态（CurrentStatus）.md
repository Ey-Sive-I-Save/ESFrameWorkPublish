# AIWarnings 当前状态

最后核对：2026-08-03。

## 已确认基线

- AIWarnings 采用按任务分层加载：`README -> CurrentStatus -> RuleIndex -> 命中的 P0 -> 当前领域专项 -> 必要的交接/复盘或提案`。普通任务禁止递归读取全目录；P0、现行状态和任务专项必须读取原文，分批摘要只能导航，不能替代规则权威。
- 模块成熟度治理已建立统一状态、半成品隔离规则和状态跃迁证据门禁；`$es-module-lifecycle` 与模块状态审计 AICommand 现支持 `audit-only`、可选 `audit+checkpoint` 和 `resume`。默认不写文件；用户确认精确文件与区域后，检查点才可记录当前状态、Git 基线、证据缺口、下一动作和失效条件。该检查点不代表全项目已分类，也不能向下次窗口授予实现权限。

- `ES_Design.csproj` 最近一次核对为 `0 warning, 0 error`。
- Camera Core 的 P0 源码骨架已补：`ESCameraModule` 持有 Director，`ESGameManager.Camera` 只暴露模块门面；当前版本只有 `LocalControl` 当前 Entity 能提交请求与 Look，根本没有外部 Owner 注册 API。回放/观战/剧情需等模块私有受信 Bridge 落地后才可申请正式 View。普通 AI/NPC、AI 驾驶载具及 AI 技能请求会在模块边界拒绝；技能相机以技能使用者而非主目标作为 Owner。`ESCameraLease` 已有 Dispose / Look / Target 语义 API，Core 及 Track/Timeline/Preview 源码均通过过一次临时全量程序集静态编译。当前 IDE 生成 `.csproj` 尚未同步新 Module、Skill Camera Track、Timeline 与 Preview 源码；它不是 Unity 编译输入，不能把其失败表述成“Unity 未收录”，也不能当作 Unity 已编译证据。
- `ESGenericLife` 的 Pool 分部已完成代码接线：唯一 Root、按类型唯一 Extension、新建/预热 Despawn 基线、回调异常收口与 Spawn 内延迟归还均已实现；但 Unity 尚未刷新生成的 `.csproj`，因此 ES_Logic 与 Unity Test Runner 尚未对本轮代码形成最终验收证据。
- Entity 模板、挂点与武器挂点链已具备静态闭环；`EntityCharacterProfile` 是唯一的 Prefab 身份/DataInfo 入口，正式 Variant 自动绑定，通用池模板由租出方直接 `Entity.BindDefinition(...)`。仍需 Unity PlayMode 验证和发布门禁证据，不可仅凭编译签收。
- GameTag 的 `ESTagStableReference` 已统一使用 `ESSearchDropdown` Picker；`ItemDataInfo` 的旧 `ValueDropdown/GetTagOptions` 残留已移除。Tag 测试代码已按当前 NUnit / `IPoolable` 契约修正，但 Unity Test Runner 尚未实跑。
- 输入、对象池、物理查询、Item/Shot 与 Buff 都有运行时实现，是当前较成熟的底座。
- ResourcePlan、Consumer/Library 增量激活、Raw `TextAsset` 入口和资源 Scope 生命周期均已有源码；完整 PlayMode、Provider 切换、P6/P7/P9 与 IL2CPP Player 证据仍缺失。
- 资源窗口已增加独立的“5. 发布到远端”入口：先读取第四步上传计划并执行只读预检；手动计划 Provider 不再伪报成功，真实 OSS/S3/HTTP Provider 安装前会明确阻断。Root Manifest 仍必须最后切换。
- 第五步窗口提供“初步验证远端隔离区”：真实 Provider 必须只在 `validationPrefix`（默认 `.es-validation`）执行探针上传、HEAD 校验与清理，不得用正式版本目录测试权限。
- 阿里云 OSS Provider 已接入原生签名、流式文件上传、`x-oss-meta-es-sha256` HEAD 校验与隔离探针协议；凭据仅从环境变量读取，Unity 资产不保存 Secret。仍需使用真实测试 Bucket 完成一次网络实跑。
- AI 协作历程已改为用户授权制：只有用户明确要求时才能创建、更新或恢复，普通任务不自动写入；连续约 10 轮后 AI 可以询问一次，但确认前不得修改或催促。已具备本地 Codex session 兜底工具：`Find-CodexSession.ps1` 从 `history.jsonl` 按 ID、主题、日期和 CWD 输出候选绝对路径，`Recover-CodexSessionHistory.ps1` 从已确认的 `rollout-*.jsonl` 重建逐任务时间线。候选分数仅辅助定位，档案归属仍需人工核对；详见对应 P0。

## Camera 当前阻断

- 当前生成的 `ES_Logic.csproj` 实测为 `0 warning / 1 error`：`ESGameManager.StaticCache.cs(28,23)` 找不到 `ESCameraModule`。它说明 IDE 项目生成快照尚未同步模块源文件；不得手改生成工程绕过，但这不能证明 Unity 的 `ES_Logic.asmdef` 没有收录该源码。
- 本地观测没有可调用的外部提权入口：模块只接受当前本地 Entity；未来 Replay/Spectator/Cutscene Bridge 必须作为模块私有生命周期实现。`ESCameraModuleAuthorizationTests` 已写入未登记 Owner、非本地 Entity、当前本地 Entity 三个测试源码；Tests 程序集目前被无关的 `ESAudioCueRuntimeTests.cs:217` 类型错误阻断，故该测试尚未获得程序集或 Unity Test Runner 执行证据。
- 因此 Camera 当前状态严格为“P0 源码骨架待 Unity 收录与运行验收”，不是“P0 已收口”或“稳定可用”。

## 构建与运行证据矩阵

源码存在不代表 Unity 已编译，`.csproj` 收录也不代表 Player 已验证。后续 AI 报告状态时必须按下表分层，不得把其中任一步替代另一步。

| 证据 | 当前状态 | 说明 |
| --- | --- | --- |
| 相关源码存在 | 部分已确认 | Raw、资源扩展、资源生命周期、发布 Provider，以及 Camera 首切片源码均存在。 |
| IDE 生成的 `.csproj` 收录 | 快照过期 | 已收录 Camera Core、Content、CM2 Adapter、SceneBinding、Entity/Vehicle 接线；尚未同步本轮 `ESCameraModule`、Skill Camera Track、Timeline Bridge、Preview Contracts/View 与 Editor Preview。临时收录仅用于静态检查，已撤销。该表不等同于 Unity asmdef 收录状态。 |
| `dotnet build ES_Stand.csproj` | 最近记录为 `0 warning / 0 error` | 仅代表 Stand 程序集的编译证据。 |
| 临时全量 Camera Runtime / Editor 构建 | 已通过 | 2026-08-02：临时收录所有新增 Camera Runtime、Track、Timeline、Preview 源码后，`ES_Logic`、`ES_Logic.Editor` 均为 `0 warning / 0 error`；随后已撤销生成工程修改。 |
| 临时全量 Tests 构建 | 被无关错误阻断 | `ESAudioCueRuntimeTests.cs:217` 将 `object` 传给要求 `ESAudioVoiceHandle` 的 API；`ESCameraModuleAuthorizationTests` 尚不能取得当前程序集编译或执行证据。 |
| Unity Editor 编译 / 域重载 | 未验收 | `.csproj` 编译不能替代 Unity Editor 实际导入与域重载。 |
| Unity Test Runner / PlayMode | 未验收 | `dotnet test` 没有产生 Unity Test Runner 执行结果；尚未得到真实运行证据。 |
| IL2CPP Player | 未验收 | 尚未构建和运行。 |
| 真实 OSS 网络 | 未验收 | Aliyun OSS 实现已存在，仍缺真实测试 Bucket。 |

因此本轮仅有“临时全量 Runtime / Editor 静态编译”通过；Tests 被无关错误阻断，当前 IDE 工程快照也因漏项而失败。应通过 Unity 的重新生成 IDE 工程恢复可重复的 .NET 编译证据；Unity 是否真正收录/编译则必须看 Editor 域重载与 Console。仍不得用任何 `.csproj` 编译替代 Unity Editor、Unity Test Runner、PlayMode、Profiler 或 Player 验收。不得恢复旧 Raw 类型或手工长期维护 Unity 生成的 `.csproj`。

## 当前优先级

1. 在 Unity 重新生成 IDE 工程以同步 `.csproj`，同时以 Editor 域重载/Console 确认 `ESCameraModule`、Track/Timeline/Preview 的真实编译状态；随后执行 `ESCameraDirectorTests` 与角色/相机场景的 PlayMode 验收。
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
