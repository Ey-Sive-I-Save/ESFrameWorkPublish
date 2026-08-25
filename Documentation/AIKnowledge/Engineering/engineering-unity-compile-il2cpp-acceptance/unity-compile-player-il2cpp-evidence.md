# Unity 编译、Player、IL2CPP/AOT 与裁剪证据边界

状态：现行派生知识；`runtime-not-run`。

`KnowledgeId`: `es.unity.compile-player-il2cpp-evidence.v1`
`Authority`: `Project settings + Unity 2022.3 official documentation + AIWarnings P0 + Skill contracts + HybridCLR package source`
`RouteKeys`: `unity`, `compile`, `csproj`, `domain-reload`, `player`, `il2cpp`, `aot`, `hybridclr`, `managed-stripping`, `link-xml`, `evidence`, `unity-build-stability`, `build-risk`, `build-recovery`, `build-regression`
`ContentHash`: `426cf291f48f278af32de3eb12f5210f0c03d999a386d4dc82bfc395383d0f2f`
`EvidenceLevel`: `S1`
`StaleWhen`: Unity、PlayerSettings、HybridCLR、编译/发布/风险/工作树 Skill、AIWarnings P0、ExternalEvidenceRefs 响应哈希或任一 SourceRef 哈希变化。

## Scope

本条目是“Unity 编译、生成 `.csproj`、Player Build、IL2CPP/AOT、HybridCLR 生成、Managed Stripping/`link.xml` 是否执行以及达到哪一证据层”的 canonical owner。它负责告诉 AI 应选择哪一层证据、何时停止升级结论、怎样隔离故障和恢复验证；“日志、Player、BuildReport 与 HybridCLR 生成物是否属于同一次构建输入、是否 stale”由 `es.unity.build-identity-artifact-provenance.v1` 负责。

本条目不负责 MonoBehaviour 回调、静态状态重置和 Enter Play Mode 的详细机制；这些事实由 `Documentation/AIKnowledge/Unity/unity-lifecycle-domain-reload/unity-lifecycle-domain-reload.md` 负责。本条目也不负责 Fixture、截图和视觉 QA 的构造方法，或热路径性能预算。Fixture 与视觉 QA 当前没有可消费的 canonical AIKnowledge 条目；`es.engineering.fixture-visual-qa.v1` 已弃用，即使旧投影仍将其列为候选，也必须回读 AIWarnings Start 链、当前源码和真实验证证据并报告 Knowledge 覆盖缺口。热路径性能预算由 `Documentation/AIKnowledge/Engineering/engineering-performance-evidence/hot-path-container-performance-evidence-contract.md` 负责。

`Documentation/AIKnowledge/entries/function-area-routing.md` 只提供功能区导航，不是本领域事实正文。相邻条目只能在适用条件下交叉引用本条目，不应复制证据分层、IL2CPP 工具链或裁剪结论。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `ProjectSettings/ProjectSettings.asset` (`2dc71bebcd685d7a4be6d11916192ce3b16910b2103678f01980fe2c223d785e`)
- `ProjectSettings/HybridCLRSettings.asset` (`8991725e1ebe7a3a77bdd93fb66c86b7170d8d858167d94f579775fc222fd497`)
- `.agents/skills/es-unity-compile/SKILL.md` (`f21c07252d11cf2bb1b3f78fdf3b179a12140d9906654d484cb281a757b09df5`)
- `.agents/skills/es-release-acceptance/SKILL.md` (`8cc50a64bf90c8c8302836255b7a022f2aa33040fb02065e1d4448755f8b27c6`)
- `.agents/skills/es-release-acceptance/references/evidence-matrix.md` (`b4e9b8e1c4614adbef1f52c0758e47728253374b4d43bb9c38d7a2b1a23e3d85`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）/项目最高警告_IL2CPP工具链注册_禁止以编译器文件存在代替Unity可检测_AI协作警告.md` (`0b68750f825af7bf02cb55905043deba58abaa79aecef5751f760425f7d85fd5`)
- `Packages/com.code-philosophy.hybridclr/package.json` (`f0926c7e429ba9df5785003096d4cf1295969080a3351a730efc00b9037644bb`)
- `Packages/com.code-philosophy.hybridclr/Editor/Commands/PrebuildCommand.cs` (`16e9a5d5ab96b0dba778a33fbfcea5560506dcd847bd247f7c0c45db93f769ce`)
- `Packages/com.code-philosophy.hybridclr/Editor/Commands/CompileDllCommand.cs` (`ac9c56dc4252b55e2feb8e062df3778bdc83d54f7ad8ebb5ab47c26f9aa1b52a`)
- `Packages/com.code-philosophy.hybridclr/Editor/Commands/LinkGeneratorCommand.cs` (`3e753f4b63b4ec76d34583c37c3eda3e9ceac46285269e5d012a32ccc3889604`)
- `.agents/skills/es-change-risk-register/SKILL.md` (`bdb34df848cd86f01a32f63f5387c77e683eaea1afc8d428cf0f2c7d72c6a6a0`)
- `.agents/skills/es-change-risk-register/references/risk-register-contract.md` (`3a9d2f5e47f596985ad7256ee5679ff6d75cefab251a9ec3a332d121d61656b6`)
- `.agents/skills/es-worktree-audit/SKILL.md` (`c0df3ce39a4ea5882efe3cb1fa095ef9c5ee08e564b0276759d9d3a037fa5b25`)
- `.agents/skills/es-skill-governance/references/verification-semantics.md` (`6c6a124eec1561a8ad143628ffa57a629a6dbc00c4ce99c6e5bcd72fe5cc463a`)

## ExternalEvidenceRefs

官方页面于 2026-08-24 读取，HTTP 200。响应 SHA-256 不参与项目相对 `SourceRefs` 的 `ContentHash`；使用时必须保留版本边界并在响应变化后重新读取：

- Unity 2022.3 IL2CPP overview: `https://docs.unity3d.com/2022.3/Documentation/Manual/IL2CPP.html`; SHA-256 `a4d220130ccf42f1c1af9ab3d64037ca78f906426d5de1024c27f7f0a1b438ac`
- Unity 2022.3 managed code stripping: `https://docs.unity3d.com/2022.3/Documentation/Manual/managed-code-stripping.html`; SHA-256 `da9400c6c4cd32bbc194c488039ef94b8a82ad44362127b4641b02f76a62ad80`
- HybridCLR package manual (`Next`): `https://www.hybridclr.cn/en/docs/basic/com.code-philosophy.hybridclr`; SHA-256 `188b1f7bf44520ac1d08e0cb90cc0cc206300c704c43fca4dda00fe4482e1b9e`
- HybridCLR build workflow (`Next`): `https://www.hybridclr.cn/en/docs/basic/buildpipeline`; SHA-256 `3ab9eda38eedc76c90cf5dce3847aed52674e52ff621a3dffa6a8743d07d5eb9`

HybridCLR 在线页面标记为 `Next`，只用于交叉核对通用机制，不能覆盖项目本地 `8.12.0` 包源码、当前配置和实际输出路径。两者冲突时，本条目必须标记外部校准不适用并回读本地版本化源码，不能用网页的新命令或目录名修补当前项目。

## Trigger and routing

| 自然语言触发 | 推导 routeKeys | 预期最小命中 | 相邻路由与回退 |
|---|---|---|---|
| “`.csproj` 0 error 算不算 Unity 编译通过” | `csproj, compile, unity, evidence` | 本条目 | 若命中发布导航，只保留其链接，不把它当事实正文 |
| “Console 无红错 / Reload 是否完成” | `unity, compile, domain-reload, evidence` | 本条目；涉及静态状态时追加 lifecycle 条目 | 先区分编译证据与生命周期机制，再选择 canonical owner |
| “Windows IL2CPP 找不到 VS，但有 cl.exe” | `unity, player, il2cpp, aot` | 本条目 | Command/Entity 生命周期条目属于误命中，应丢弃并回读 IL2CPP P0 |
| “HybridCLR GenerateAll 或 link.xml 是否仍新鲜” | `il2cpp, aot, link-xml, managed-stripping, evidence` | 本条目 | 不用 Scene Release 或 AIBrain 摘要代替生成源码与产物哈希 |
| “测试源码存在是否等于 PlayMode 通过” | `unity, evidence, playmode, test-fixture` | Fixture 条目 + 本条目 | Fixture 负责测试构造；本条目只负责证据层级 |
| “Profiler 能否证明 IL2CPP Player 性能” | `performance, profiler, player, il2cpp, evidence` | 本条目 + performance 条目 | 必须同时保留目标 Player 与性能预算两个 canonical owner |

当前正式索引与本条目原子登记顶部的 `RouteKeys`。其中 `hybridclr`, `unity-build-stability`, `build-risk`, `build-recovery`, `build-regression` 用于收窄生成物新鲜度、构建稳定性、风险、恢复和回归任务；登记只提供导航，不提升任何 Runtime 或发布证据等级。

Skill 选择必须随任务风险收窄：普通编译/Reload 读取 `es-unity-compile`，Player/IL2CPP/发布证据读取 `es-release-acceptance`；只要目标包含稳定性、并发改动、风险、回滚或恢复，还必须追加 Index binding 已登记的 `es-change-risk-register` 与 `es-worktree-audit`。

静态路由出现零命中时，回到 AIWarnings Start、CurrentStatus、RuleIndex 和当前源码并记录 Knowledge 覆盖缺口。出现超过三个候选或明显误命中时，删除仅由通用 `knowledge`、`unity`、`evidence` 带来的弱候选，保留对象、动作和风险同时重叠的 1～3 个 canonical 条目。`planTask` 不可用时只能报告 `PlanTaskUnavailable`，不得把静态排序模拟冒充真实计划回执。

## Decision rules

| 条件 | 决策 |
|---|---|
| SourceRefs、Index binding、ContentHash 和任务身份均新鲜，且目标只需静态判断 | 可以继续读取最小权威来源并给出 S1/S2 范围结论 |
| 问题涉及静态状态、Fixture/视觉或性能预算机制 | 先追加对应 canonical 条目及其 requiredReads，再继续 |
| SourceRef/索引哈希漂移、来源冲突、目标平台或 HEAD 未固定 | 标记 `stale` 或 `Blocked`，废弃旧计划和旧结论 |
| 需要写源码或索引 | 当前用户明确目标直接授权；受管通道才要求 AICommand、TaskContract 和 PlanHash |
| 需要 Unity/Player、外部进程或发布动作 | 当前用户必须单独点名该动作；受管通道才额外要求 AICommand、TaskContract 和 PlanHash |
| RuntimeAcceptance/ReleaseAcceptance 要求运行证据但未授权或未取得 | 停止在 `runtime-not-authorized` / `runtime-blocked`，不得用静态分数升级 |

读取本条目后，AI 必须按以下顺序工作；不得从中间步骤开始，也不得用经验跳过缺失证据：

1. **校验新鲜度**：运行 Knowledge 条目验证器或逐项复算全部 `SourceRefs`。任一路径缺失、哈希漂移或来源互相矛盾，立即把本条目标为 stale，回读当前权威来源并重新规划。
2. **固定任务身份**：记录 ProjectRoot、branch、HEAD、相关工作树路径、Unity 版本、目标平台和目标架构。验证期间任一绑定发生变化，旧结论失效。
3. **写出目标声明**：先用一句话写清用户要证明的是 Source、`.csproj`、Unity compile、Domain Reload、Test/Runtime、Player、IL2CPP/AOT、裁剪还是 Release。用户说“编译通过”但未指明层级时，不得自行按最高层解释。
4. **选择最低充分证据**：按下方证据表选择恰好能证明目标声明的证据行。相邻行的结果不能替代目标行，多个低层结果相加也不能升级成高层结果。
5. **检查执行权限**：静态读取/校验不授权启动 Unity、Player 或外部进程。Runtime 操作必须由当前用户明确点名，并具备目标、预算、超时和停止条件；通过 AIBrain/Worker 执行时还必须有 PlanHash 与匹配 AICommand/TaskContract。受管协议缺失只令该通道 `runtime-not-authorized`，不得要求用户二次批准。
6. **绑定并审查回执**：按 `es.unity.build-identity-artifact-provenance.v1` 校验回执是否绑定本次 HEAD/工作树范围、Unity 版本、平台、backend、命令/操作、时间、输入与产物哈希；本条目只继续判断回执证明了哪一证据层。进程退出码、日志片段、目录存在或截图不能单独成为通过回执。
7. **按最低已证明层交付**：报告最高已证明等级，同时列出 `not-run`、`blocked`、`failed` 和 `claimsNotProven`。缺证据时降低结论，不得写“基本完成”“应该可用”或“理论上通过”。

### 硬停止条件

出现以下任一情况时，AI 必须停止升级结论；先修复上下文或明确报告证据缺口：

- SourceRef 缺失、哈希漂移、条目字段无法解析，或来源之间冲突。
- ProjectRoot、Unity 项目实例、branch/HEAD、目标平台、架构或 scripting backend 未固定。
- 工作树在验证期间发生相关漂移，或回执绑定的是另一份源码/配置。
- 只存在生成 `.csproj`、旧日志、旧构建目录、旧 Player 或其他不可证明新鲜度的产物。
- Unity 仍在编译、Domain Reload 未确认结束、Console 基线不明，或禁用 Reload 的状态未披露。
- Windows IL2CPP 的 Visual Studio 注册、MSVC/SDK 检测或 native 编译阶段缺少任一必要证据。
- HybridCLR 生成物与最终 Player 的目标平台、development 配置或输入哈希不一致。
- 目标结论需要 Runtime/Release 证据，但本次没有匹配授权和可验证回执。

状态使用规则：尚未执行且本任务不要求 Runtime 时写 `runtime-not-run`；目标 profile 要求 Runtime 但未授权/证据缺失时写 `Blocked` 或 `runtime-not-authorized`；实际执行并失败才写 `Failed`。不得把证据缺失写成源码缺陷，也不得把静态通过写成 Runtime 通过。

## 稳定性维护职责

本职责参与维护的是 Unity 编译、Domain Reload、测试、Player、IL2CPP/AOT、裁剪及其相邻变更的稳定性。稳定性的可操作含义是：降低回归概率、限制故障影响面、尽早检测漂移，并保留可验证的恢复路径；它不等于“当前项目没有 Bug”。

AI 可以在用户授权范围内执行：只读基线检查、工作树边界识别、风险登记、最小修改、分层验证、回归对比和恢复建议。AI 不得仅凭本条目自动修改源码/资产、启动 Unity/Player、清理工作树、回滚他人改动、写 Git/审计/历史或发布；每类状态变更仍需当前用户明确动作，只有受管通道另需匹配合同。

### 维护闭环

1. **建立基线**：记录 branch、HEAD、staged/unstaged/untracked/deleted 计数、目标路径现状、已有失败和最近可重读证据。不得先清 Console、删除产物或修改环境再采集基线。
2. **识别所有权与影响面**：列出目标程序集、asmdef、Editor/Runtime 边界、调用方、平台、生成物和并发工作树路径。无法确认 owner 或存在目标重叠时保持 `Blocked`，不猜测归属。
3. **声明变更预算**：固定允许路径/对象数、最大重试、并发、超时、停止条件和恢复点。任何路径扩张必须重新授权与重新规划。
4. **登记风险**：每项风险必须含 `RiskId / Scenario / Prevention / Detection / Isolation / Recovery / Owner / EvidenceRef / ChangeBudget / Status`。缺 Prevention、Detection、Isolation、Recovery 或 EvidenceRef 时不得标记 Accepted。
5. **最小化修改**：只修改权威源，不编辑生成 `.csproj`、旧构建产物或缓存来伪造成功；保留兼容边界和其他职责的未提交内容。
6. **由低到高验证**：先完成静态与边界检查，再按目标 profile 和权限执行 Unity/Runtime/Release 证据。低层失败先隔离，不用高成本构建掩盖根因。
7. **比较回归**：将结果与基线按相同项目、平台、场景、配置和阈值比较；无法同条件比较时只报告新观察，不声称无回归。
8. **交付与恢复**：报告修改、最高证据层、残余风险、未验证范围、恢复动作和触发回滚的明确条件。再次审计工作树，确认没有任务外写入。

### 最小风险登记表

| RiskId | Scenario | Prevention | Detection | Isolation | Recovery | Owner | EvidenceRef | ChangeBudget | Status |
|---|---|---|---|---|---|---|---|---|---|
| `STAB-WORKTREE` | 覆盖其他职责的并发改动 | 修改前后审计目标路径 | HEAD/状态/目标 diff 对比 | 只写声明路径 | 停止并保留现场；只撤销本次可证明改动 | 当前任务 owner | 前后 worktree 输出 | 仅声明路径；不得清理/回滚任务外文件 | 未登记证据时 `Blocked` |
| `STAB-COMPILE` | 静态投影通过但 Unity 编译失败 | 分离 `.csproj` 与 Unity 证据 | 导入、编译、Reload、Console 回执 | 限定目标程序集 | 回到最后可重读源码基线 | 变更 owner | 编译与 Reload 回执 | 目标程序集、声明重试/超时 | 按目标 profile |
| `STAB-RELOAD` | Reload 后注册、缓存或序列化状态损坏 | 标记 reload-sensitive 入口 | reload 后定向测试/交互 | 禁止扩大到无关 Editor 状态 | 重启正确实例并按稳定身份恢复 | 子系统 owner | reload 与定向测试回执 | 目标 Editor 子系统、声明 reload 次数 | Runtime 未跑则 `runtime-evidence-outstanding` |
| `STAB-IL2CPP` | 工具链、AOT 或裁剪在目标平台失败 | 固定平台/backend/工具链与生成输入 | vswhere、native 日志、产物哈希、目标运行 | 使用新输出目录，隔离旧 Mono/IL2CPP 产物 | 修复注册/重新生成；不降级 Mono | 发布 owner | S6 目标平台回执 | 单一声明平台/架构、输出目录、超时 | 缺任一证据则 `Blocked` |
| `STAB-STALE` | 旧日志、缓存或旧 Player 被误当当前事实 | 绑定 HEAD、配置、时间和哈希 | SourceRef/receipt 新鲜度复算 | 不复用无法证明来源的产物 | 丢弃旧计划并重新验证 | 当前验证 owner | SourceRef/hash receipt | 当前任务来源与回执；不扫描无关产物 | 漂移即 stale |
| `STAB-REGRESSION` | 修复目标问题但破坏相邻行为 | 预先声明回归面和最低测试集 | 基线/结果同条件对比 | 分程序集、平台和场景判定 | 恢复到可验证基线并缩小变更 | 领域 owner | named tests 与对比回执 | 命名测试/场景/平台；声明重试与停止条件 | 未覆盖范围保持 open |

### 故障处理顺序

发生编译、Reload、Player 或 IL2CPP 故障时，按 `保留证据 -> 确认任务绑定 -> 区分既有/新引入 -> 缩小到最早失败层 -> 隔离相关路径/产物 -> 执行最小恢复 -> 重放同层验证` 处理。不得先清理、重装、全量重建或修改无关业务代码来碰运气；不得把工具链错误归因于资源、GameCore 或场景缺失。

### 稳定性结论边界

- 静态与边界检查通过，只允许写“静态完成、Runtime 证据未取得”或同等范围结论。
- 指定回归集在同条件下通过，只允许写 `NoRegressionObservedInScope`，不得写“项目无回归”。
- 一次 Unity/Player/IL2CPP 成功不自动证明长期稳定、跨平台稳定、性能稳定或发布稳定。
- `Accepted` 只属于本次明确范围；`Stable` 是模块成熟度结论，必须由模块治理与可重放证据支持，不能由本职责单次验收授予。
- 如果恢复路径未经演练，应写 `recovery-unverified`；存在备份、旧提交或回滚说明不等于恢复已验证。

## Verified facts

- 项目固定 Unity 版本为 `2022.3.45f1`，revision 为 `a13dfa44d684`。（来源：`ProjectSettings/ProjectVersion.txt`）
- `ProjectSettings.asset` 当前记录 `Standalone: 1` 的 scripting backend、`stripEngineCode: 1`；文件中没有显式的 Standalone `managedStrippingLevel` 项。本条目不推断缺省裁剪等级。（来源：`ProjectSettings/ProjectSettings.asset`）
- 项目启用了 HybridCLR；本地包版本为 `8.12.0`。当前配置使用项目内 IL2CPP（`useGlobalIl2cpp: 0`），并声明热更新程序集、补充元数据程序集、`HybridCLRGenerate/link.xml` 与 `HybridCLRGenerate/AOTGenericReferences.cs` 输出。（来源：HybridCLR package/config SourceRefs）
- `PrebuildCommand.GenerateAll()` 的源码顺序为：编译热更新 DLL、生成 IL2CPP 定义、生成 `link.xml`、生成裁剪后的 AOT DLL、生成桥接/Reverse PInvoke wrapper、生成 AOT 泛型引用。（来源：`PrebuildCommand.cs` 及对应 Commands SourceRefs）顺序存在不等于这些步骤本次已运行或产物仍新鲜。
- AIWarnings P0 禁止用 `.csproj`、文件存在、旧日志、`cl.exe` 或低层证据替代 Unity/Player/IL2CPP 的目标层证据。（来源：AI 交付声明与 IL2CPP 工具链 P0 SourceRefs）

### 官方校准后的高危盲点

- Unity 官方说明 IL2CPP 会把 MSIL 转换为 C++ 并生成目标平台 native binary。因此 C# DLL、热更 DLL、`BuildReport.result=Succeeded` 或 Editor 退出码都不能独立替代 IL2CPP/native 工具链与输出证据。
- Unity linker 的静态分析只覆盖构建时存在且可达的代码；反射、序列化、运行时加载和热更入口可能无法被静态发现。`link.xml` 存在只证明有保留声明，必须再证明本次 Player 消费了它，并运行覆盖动态入口的目标平台测试。
- HybridCLR 官方工作流要求热更 DLL 使用目标平台编译开关，并在打包前生成 link、裁剪 AOT、桥接和泛型相关输入。`Generate/All` 是生成入口，不是最终 Player 的消费回执。
- **硬停止**：HybridCLR 已启用而目标平台 stripped AOT manifest 为空时，禁止继续作出 Player/IL2CPP/发布通过结论；先恢复目标平台生成链，再用构建身份 receipt 绑定最终消费关系。

## Evidence boundary

Static 可证明：声明的文件、配置、源码合同、SourceRef 哈希、ContentHash、索引绑定以及证据层之间的静态边界。Static 不能证明 Unity 当前实例已导入或 Reload、测试已运行、Player 已构建、IL2CPP native 阶段已完成、裁剪后功能可运行、Profiler 达标或发布成功。

本条目当前为 `S1` 且 `runtime-not-run`。只有绑定当前项目身份、平台、配置和产物的独立 Runtime/Release 回执，才能提升对应目标的证据层；不存在“多个 S1/S2 相加自动成为 S3-S6”的规则。

### 证据分层

| 层级 | 最小证据 | 允许结论 | 不允许替代的上层结论 |
|---|---|---|---|
| Source | 目标源码、asmdef、配置和版本文件的当前哈希 | `source-present` | 编译成功 |
| Generated project | 精确 `.csproj`、命令、退出码和构建日志 | `dotnet-build` | Unity 收录、Unity Editor 编译或 Domain Reload 成功 |
| Unity Editor compile | 正确项目实例完成导入/刷新，编译结束，Domain Reload 完成，Console 基线与结果可重读 | `unity-editor-compile` | Test Runner、PlayMode、Player 或 IL2CPP 成功 |
| Test/Runtime | 命名的 EditMode/PlayMode 测试或可复现实机场景及结果 | `unity-test-runner` / `runtime-observation` | Profiler、Player、IL2CPP 或发布成功 |
| Player | Unity 版本、目标平台/架构、development 标志、输出路径、BuildReport/日志和产物哈希 | `player-build` | IL2CPP native 阶段或发布成功，除非证据明确覆盖 |
| IL2CPP/AOT | 目标平台与 backend、Unity 可检测的本机工具链、IL2CPP/native 编译日志、输出产物；HybridCLR 还需同目标的生成记录 | 对已测目标声明 `IL2CPP/AOT passed` | 其他平台、性能、资源或发布成功 |
| Release | 明确发布目标、已验收产物、上传/验证/回滚证据 | `release-validation` | 超出本次平台和范围的商业级结论 |

## 生成 `.csproj` 的边界

Unity 生成的 `.csproj` 是 IDE/定向静态构建投影。对指定工程执行构建可发现该投影中的 C# 错误，但不能证明 Unity 当前导入数据库、asmdef 闭包、编译管线或 Domain Reload 已成功。不得手工修改生成 `.csproj` 来让 Unity 看似收录源码；必须修复权威源码、asmdef 或 Unity 导入状态，并由 Unity 重新生成投影。

有效的 `.csproj` 证据至少绑定：Git HEAD/工作树范围、工程文件路径和哈希、Unity 版本、完整命令、退出码、日志路径、错误与警告计数。工程文件或相关源码变化后，旧结果 stale。

## Unity 编译与 Domain Reload

Unity 编译证据必须来自正确项目实例。一次受管刷新或批处理进程启动只证明请求/进程层发生，不能单独证明编译完成。可接受回执需要同时表明：编译已终止、没有目标范围内的编译错误、Domain Reload 已完成且未被禁用/中断、编译前后 Console 证据可区分。

Domain Reload 是独立观察点：`.csproj` 成功、脚本导入开始、进程退出码为零都不能替代 Reload 完成证据。涉及静态状态、注册、缓存、序列化恢复或 `InitializeOnLoad` 的功能，还需要 reload 后的定向交互或测试；仅 Console 无错误不足以证明状态恢复正确。

## Player 与 IL2CPP/AOT

Player Build 证据必须绑定目标平台、架构、backend、development 选项、输出目录和日志。Windows IL2CPP 还必须证明 Unity 能通过已注册的 Visual Studio 实例检测 MSVC 与 Windows SDK；仅发现 `cl.exe`、仅在 Developer Command Prompt 可运行、或存在安装目录均不够。

HybridCLR 的 `CompileDllCommand` 调用 Unity `PlayerBuildInterface.CompilePlayerScripts` 为当前 `BuildTarget` 生成脚本 DLL。这是 HybridCLR 生成链的一环，不等于最终 Player 或 IL2CPP native 编译成功。`GenerateAll()` 产生的 link、裁剪 AOT DLL、桥接和泛型引用也必须与最终 Player 的目标平台、配置和输入哈希一致；任一输入变化后应重新生成并重新验证。

## 链接裁剪证据

`LinkGeneratorCommand` 扫描热更新程序集引用并写出配置指定的 `HybridCLRGenerate/link.xml`，随后刷新 AssetDatabase。它证明的是生成逻辑和目标路径存在，不证明生成文件本次已生成、被当前 Player Build 消费、所有反射/泛型/序列化入口均保留，或最终裁剪产物可运行。

裁剪验收至少需要：当前热更新/AOT 程序集清单、生成命令与目标、`link.xml` 和 AOT 泛型引用文件哈希、裁剪后的 AOT DLL 清单、最终 IL2CPP Player 构建日志，以及覆盖动态访问入口的目标平台运行测试。`stripEngineCode: 1` 只是一项配置事实，不是裁剪安全证明。

## Common AI failure modes

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确替代动作 | 失败后恢复 | 仍缺证据 |
|---|---|---|---|---|---|---|
| 把源码、asmdef 或测试文件存在写成已通过 | 回答只有路径，没有执行记录 | 混淆定义与执行 | 先标记目标证据层并查找回执 | 只写 `source-present` | 撤回通过声明，回到最低已证层 | 编译、测试或 Runtime 回执 |
| 把 `.csproj` 0 error 写成 Unity 编译/Reload 通过 | 没有正确 Unity 实例、导入周期或 Reload 结束证据 | 把 IDE 投影当 Unity 编译权威 | 绑定工程哈希、HEAD，并检查 Unity 编译与 Reload 是否独立完成 | 只写该工程的 `dotnet-build` | 丢弃上层结论，重新请求 Unity 证据 | Unity 导入、Console 基线、Reload 回执 |
| 把 Console 无红错或进程退出码 0 当作本次验证 | 无法区分旧 Console、启动成功和目标任务完成 | 回执没有绑定任务输入和时间 | 检查编译起止、任务 ID、日志和结果文件 | 按目标层读取结构化结果 | 保留现场并重放最早缺失层 | 当前任务绑定的日志/结果 |
| 把 `cl.exe` 或安装目录存在写成 IL2CPP 工具链可用 | Unity 报 `ToolchainNotFoundException` | 忽略 Visual Studio Installer 注册和 SDK 检测 | 用带组件要求的 `vswhere` 校验实例、MSVC 和 SDK | 修复已注册实例并重启 Unity，再做最小 Player | 不降级 Mono；隔离旧输出后重试同平台 | Unity 可检测工具链和 native build 日志 |
| 把 `GenerateAll()` 返回、目录或 `link.xml` 存在写成新鲜/裁剪安全 | Player 出现 MissingMethod、反射或泛型入口丢失 | 未绑定生成输入、消费链和动态入口 | 比对平台、development 配置、输入/输出哈希和最终构建日志 | 重新生成并运行覆盖动态入口的目标 Player | 丢弃旧生成物结论，按最早漂移输入重放 | 生成日志、消费证据、目标平台运行测试 |
| 把旧 Player/Build 目录当当前 HEAD 产物 | 回答没有 BuildReport、时间或产物哈希 | 复用不可证明新鲜度的缓存 | 固定 HEAD、平台、backend、输出路径和时间 | 用新输出目录生成可绑定产物 | 隔离旧目录，不删除现场，重新构建 | 当前 BuildReport、日志和产物哈希 |
| 用 HybridCLR `Next` 在线文档覆盖本地 `8.12.0` 源码 | 命令、目录或顺序与项目安装版本不一致 | 把最新网页误当版本化项目事实 | 比较网页版本标签、本地 package.json 和 Commands 源码 | 通用机制可参考网页；精确行为回到本地包源码 | 撤回不匹配步骤，按本地版本重新规划 | 对应版本的源码、配置和执行回执 |
| 热更 DLL 和 `link.xml` 存在但 stripped AOT 为空仍继续发布 | 补充元数据输入缺失，动态入口可能在 Player 中失败 | 把部分生成物替代完整 Generate/All 消费链 | 检查目标平台热更/AOT manifest、link、泛型引用和最终 Player 日志 | 标记 `identity-incomplete`，从目标平台生成阶段重放 | 保留现场，使用新输出目录重新生成和构建 | 当前生成日志、AOT manifest、Player 消费与运行证据 |
| 把 EditMode/PlayMode 或 Editor Profiler 升级成 Player/IL2CPP/发布结论 | 测试平台、场景、时长和阈值不匹配 | 相邻证据替代目标证据 | 写清目标平台与 Profile，逐行检查证据矩阵 | 只声明命名测试或 Editor 采样范围 | 降低结论并补目标 Player 采样 | Player、IL2CPP、Profiler 或发布回执 |
| AI 在用户范围外修改索引、启动 Unity 或发布 | 没有当前用户对目标/动作的明确要求 | 混淆工具能力与用户授权 | 对照当前用户范围，并区分直接与受管通道 | 只在用户范围内工作；未点名的 Runtime/发布保持 `Blocked` | 停止扩权并请求用户明确动作 | 当前用户指令；受管执行时再加命令、TaskContract、PlanHash |

## AI 回答模板

AI 对本领域给出“完成、通过、可用、已修复、可发布”类回答时，不得省略以下字段：

```text
目标声明：要证明的精确层级、平台、架构和范围
证据 Profile：StaticReview / EngineeringReadiness / RuntimeAcceptance / ReleaseAcceptance
项目绑定：ProjectRoot、branch、HEAD、工作树范围、Unity 版本
当前最高等级：S0-S6（只写已证明范围）
Static 状态：static-passed / static-partial / static-blocked
Static 细分：staticCodeStatus、staticContractStatus、staticBoundaryStatus
Runtime 状态：runtime-passed / runtime-not-run / runtime-not-authorized / runtime-blocked / runtime-failed
证据状态：evidenceStatus（fresh / stale / missing / contradictory）
Profile 约束：staticWeight、runtimeWeight、staticDeepReplayRequired、runtimeAuthorizationRequired
已验证：精确命令或 Unity 操作、目标、回执/日志/结果路径、输入与产物哈希
未验证：所有目标相关但未执行的证据行；没有则写“无”
决策：overallVerdict、decisionStatus、blockingLayer
阻断或失败：原因、是否属于当前改动；边界/证据阻断不得冒充源码缺陷
claimsNotProven：明确列出不能声称的上层结论
结论：只复述当前最高已证明层，不使用模糊升级措辞
nextAction：补齐目标声明所缺的最小证据
```

`staticWeight` 必须至少为 `0.5`。选定 `RuntimeAcceptance` 或 `ReleaseAcceptance` 且其 `runtimeRequired=true` 时，`runtime-not-run` / `runtime-not-authorized` 必须使该 profile 的结论保持 `Blocked`；不得用静态权重或总分把它提升为 Ready。

快速自检：如果删除“已验证”中的任一回执后结论仍原样成立，说明结论可能没有真正绑定证据；必须重新检查证据替代或过度声明。

## Execution checklist

开始前：

1. 读取 AIBRAIN_ENTRY，按对象、动作、风险选择 1～3 个 Knowledge，并完成 requiredReads。
2. 读取 AIWarnings Start、CurrentStatus、RuleIndex、命中的 P0、唯一 AICommand 和 Skill 合同。
3. 固定 ProjectRoot、branch、HEAD、工作树范围、Unity 版本、目标平台/架构/backend 和证据 Profile。
4. 复算 SourceRefs、ContentHash 与唯一 Index binding；漂移即停止并重新规划。

实施中：

1. 只在授权路径和变更预算内操作，记录 owner、停止条件、重试和恢复点。
2. 按 Source -> `.csproj` -> Unity/Reload -> Test/Runtime -> Player -> IL2CPP -> Release 顺序验证，不跳层。
3. 对每个动作绑定命令、输入、时间、日志/回执和哈希；失败先保留证据再隔离。
4. 发现任务身份、SourceRef、工作树或 PlanHash 漂移时中止当前执行，不复用旧结果。

完成后：

1. 重新检查目标 diff、UTF-8、SourceRef、ContentHash、Index 和相关 Skill 验证。
2. 填写最高证据等级、Static/Runtime 状态、已验证、未验证、claimsNotProven、残余风险和恢复动作。
3. 只在相同项目、平台、场景、配置和阈值下写 `NoRegressionObservedInScope`。
4. 再次审计工作树，确认没有任务外写入；不适用项写 `not-required`，未执行项写 `not-run`。

禁止事项：不得修改生成 `.csproj` 或旧产物伪造通过；不得清理/回滚他人工作树；不得用文件、按钮、进程或测试源码存在代替执行；不得在缺少 Runtime 回执时扩大结论，也不得因受管合同或 PlanHash 缺失而缩小当前用户直接授权。

## 派生结论、假设与非声明

派生结论：应把 Unity 编译、Domain Reload、Player、IL2CPP/AOT、裁剪和发布建模为独立证据行；较低证据层不能升级为较高层结论。

假设：本条目只解释当前项目文件和本地 HybridCLR `8.12.0` 源码；未假设 Unity Editor 当前已打开、生成物仍新鲜、目标平台工具链可用或任何测试已经运行。

非声明：本次未启动 Unity、未触发编译或 Domain Reload、未构建 Player、未运行 IL2CPP/AOT、未执行链接裁剪运行验证、未运行 Test Runner/PlayMode/Profiler/发布流程。`runtime-not-run`；本条目的最高证据等级为 `S1`，不构成 S2-S6 验收。
