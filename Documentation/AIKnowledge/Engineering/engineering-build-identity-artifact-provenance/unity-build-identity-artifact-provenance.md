# Unity 构建身份与产物溯源合同

`KnowledgeId`: `es.unity.build-identity-artifact-provenance.v1`

`Authority`: `Project settings + package lock + Unity 2022.3 official documentation + AIWarnings P0 + build/evidence Skill contracts`

`RouteKeys`: `unity-build-identity`, `artifact-provenance`, `build-fingerprint`, `build-input-snapshot`, `build-output-hash`, `build-receipt`, `artifact-freshness`, `player-provenance`, `hybridclr-input-hash`, `build-reproducibility`

`ContentHash`: `92a108793b2787ba6f346314d54f917ed2dc2358e1aaa52e9210154615eab811`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity/PlayerSettings、包锁、HybridCLR 配置、构建或证据 Skill、AIWarnings P0、身份字段合同、规范化算法、ExternalEvidenceRefs 响应哈希或任一 SourceRef 哈希变化。

## Scope

本条目定义 ESFramework 中一次 Unity 构建的稳定身份、构建输入与执行回执如何绑定、输出产物如何追溯，以及两份日志、Player、IL2CPP/HybridCLR 生成物能否被判定为同一次构建的证据。目标是阻止旧产物、错平台产物、不同配置日志和并发工作树结果被混用。

本条目是以下问题的 canonical owner：

- 构建前后是否仍是同一份输入；
- 一份 BuildReport、日志或 Player 来自哪个 HEAD、工作树、Unity、平台与配置；
- 两份产物能否作为同条件回归对比；
- HybridCLR 生成物是否与最终 Player 共享同一输入身份；
- 来源或配置变化后，旧回执何时必须标记为 stale。

本条目不负责：

- 判断 Unity 编译、Domain Reload、Player、IL2CPP 或发布是否通过；该证据分层归 `es.unity.compile-player-il2cpp-evidence.v1`。
- 规定通用 Automation RunRecord 的全部字段；通用回执归 `es-observability-evidence`，本条目只增加 Unity 构建身份字段。
- 执行构建、清理输出、上传、回滚、修改 ProjectSettings、写 Git 或发布；任何动作仍需当前授权和匹配合同。
- 证明可复现构建。身份完整只说明输入和输出可比较；不同机器产生相同哈希才是额外的可复现证据。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `ProjectSettings/ProjectSettings.asset` (`2dc71bebcd685d7a4be6d11916192ce3b16910b2103678f01980fe2c223d785e`)
- `ProjectSettings/HybridCLRSettings.asset` (`8991725e1ebe7a3a77bdd93fb66c86b7170d8d858167d94f579775fc222fd497`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Packages/packages-lock.json` (`6db87482785cd1b498aeb7386723c5b8f23fe7f79c8f3e2d409bf0206b48796f`)
- `.agents/skills/es-unity-compile/SKILL.md` (`f21c07252d11cf2bb1b3f78fdf3b179a12140d9906654d484cb281a757b09df5`)
- `ES/Automation/Contracts/es-unity-build-identity-receipt-v1.schema.json` (`6d681e1746e39dfbe7a97253900ba84ad545c7b5dea1a765f3d9216dd545dfeb`)
- `.agents/skills/es-unity-compile/scripts/ESUnityBuildIdentity.Common.ps1` (`d86acbcb0ea7f8f918064c6c1681364eee4e66319ce9ade1e092a38055ca9daf`)
- `.agents/skills/es-unity-compile/scripts/New-ESUnityBuildIdentitySnapshot.ps1` (`43e4835e8d8a45aab4a3473d5df476ba9d52c80e0c00b0bcd0fb829f7701dc98`)
- `.agents/skills/es-unity-compile/scripts/Complete-ESUnityBuildIdentityReceipt.ps1` (`dba4fc07e05172699ccbca745d7f6391413cb47811a44f62e956e5ee4ff4aac5`)
- `.agents/skills/es-unity-compile/scripts/Test-ESUnityBuildIdentityReceipt.ps1` (`08623346a3cf9ff71e38c1623d9c715689028753041a9c85c4c97985c92e6043`)
- `.agents/skills/es-release-acceptance/SKILL.md` (`8cc50a64bf90c8c8302836255b7a022f2aa33040fb02065e1d4448755f8b27c6`)
- `.agents/skills/es-release-acceptance/references/evidence-matrix.md` (`b4e9b8e1c4614adbef1f52c0758e47728253374b4d43bb9c38d7a2b1a23e3d85`)
- `.agents/skills/es-observability-evidence/SKILL.md` (`0c406d20958c00a1ed87358a0aa722a4b6b6066ff4b402ff5304d223cdc2bc55`)
- `.agents/skills/es-observability-evidence/references/evidence-receipt-contract.md` (`bc4aa4619224223ad566d13473a28ce2a3073aad7f5262c7890bc37b260a5c7f`)
- `.agents/skills/es-worktree-audit/SKILL.md` (`c0df3ce39a4ea5882efe3cb1fa095ef9c5ee08e564b0276759d9d3a037fa5b25`)
- `.agents/skills/es-skill-governance/references/verification-semantics.md` (`6c6a124eec1561a8ad143628ffa57a629a6dbc00c4ce99c6e5bcd72fe5cc463a`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）/项目最高警告_IL2CPP工具链注册_禁止以编译器文件存在代替Unity可检测_AI协作警告.md` (`0b68750f825af7bf02cb55905043deba58abaa79aecef5751f760425f7d85fd5`)

## ExternalEvidenceRefs

Unity 2022.3 官方页面于 2026-08-24 读取，HTTP 200。响应 SHA-256 不参与项目相对 `SourceRefs` 的 `ContentHash`；页面或响应哈希变化后必须重新读取，不能继续复用本次外部结论：

- `BuildReport`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Build.Reporting.BuildReport.html`; SHA-256 `afebaa147a13aff347db9e67b5cbc65426f141d8028697a258d480f87e640386`
- `BuildSummary`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Build.Reporting.BuildSummary.html`; SHA-256 `a09b902e047f9a8744b9ac25cfbd29b767db20dda7647af113e781da3b8c94b9`

Unity 官方资料证明 `BuildPipeline.BuildPlayer` 会返回 `BuildReport`，且 `BuildSummary` 可提供 build GUID、起止时间、BuildOptions、outputPath、platform/platformGroup、result、错误/警告数和输出大小。这些字段应进入 `ExecutionIdentity`/`ArtifactIdentity`，但官方对象不包含 Git HEAD、脏工作树内容、包锁、ProjectSettings 或 HybridCLR 输入身份。因此 `BuildReport` 为成功只能证明对应 Unity 执行结果，不能单独证明“产物属于当前输入”。

## Trigger and routing

### 自然语言触发词

`这个 Player 是哪个 HEAD 构建的`、`日志和产物是不是一套`、`能不能复用旧构建`、`产物是否新鲜`、`两次构建能否对比`、`HybridCLR 生成物是否匹配最终 Player`、`同名输出是不是同一次构建`、`如何生成 build fingerprint`、`BuildReport 对应哪个配置`。

### 精确路由

- 只询问编译、Player、IL2CPP 是否成功：路由到 `es.unity.compile-player-il2cpp-evidence.v1`，不必加载本条目。
- 询问证据属于哪次构建、能否复用、是否 stale、能否比较：优先加载本条目。
- 同时询问“属于哪次构建”和“是否通过”：加载本条目与 compile/player evidence 条目，前者判断身份，后者判断证据层。
- 询问通用任务回执、日志、追踪或 RunRecord：先加载 observability 路由；只有对象是 Unity 构建或 Player 产物时追加本条目。
- `hash`、`build`、`evidence`、`unity` 都是宽泛词，单独出现时不得作为选择本条目的充分条件。

### 相邻 canonical owner

| 决策对象 | Canonical owner | 本条目的关系 |
|---|---|---|
| 编译、Reload、Player、IL2CPP/AOT、裁剪证据层 | `es.unity.compile-player-il2cpp-evidence.v1` | 提供证据所绑定的构建身份 |
| 通用 receipt、RunRecord、日志、失败与恢复字段 | `es-observability-evidence` | 增加 Unity 构建专属身份字段 |
| 发布范围与证据矩阵 | `es-release-acceptance` | 发布每一行必须引用本条目的输入/产物身份 |
| Git staged/unstaged/untracked 边界 | `es-worktree-audit` | 提供构建输入快照的工作树事实 |

## Core decision model

一次构建不是输出目录或时间戳，而是四层不可混淆的绑定：

```text
BuildIntent
  -> InputIdentity
  -> ExecutionIdentity
  -> ArtifactIdentity
```

- `BuildIntent`：计划构建什么目标和配置。它可以在执行前生成，但不能证明执行发生。
- `InputIdentity`：执行开始时实际消费的项目、源码、配置、包、场景和 HybridCLR 输入快照。
- `ExecutionIdentity`：谁以什么命令、Unity/工具链、时间、任务合同和结果执行。
- `ArtifactIdentity`：实际输出文件、BuildReport、日志及其大小和 SHA-256。

只有四层由同一个 `buildReceiptId` 显式连接，且执行前后输入未漂移，才能说“该产物来自该构建”。任一层缺失时只能使用 `identity-incomplete`、`provenance-unbound` 或 `stale`，不能根据目录名、修改时间或聊天摘要补全。

## Required identity fields

### 1. BuildIntent

| 字段 | 要求 | 缺失时的结论 |
|---|---|---|
| `projectId` | 项目稳定标识；同时记录规范化 ProjectRoot，但机器绝对路径不是跨机器身份 | `identity-incomplete` |
| `buildTarget` | Unity `BuildTarget` 精确值 | `target-unknown` |
| `buildTargetGroup` | 对应 TargetGroup | `target-unknown` |
| `architecture` | 目标架构或明确 `not-applicable` | `architecture-unknown` |
| `scriptingBackend` | Mono/IL2CPP 精确值 | `backend-unknown` |
| `development` | 布尔值 | `configuration-unknown` |
| `buildOptions` | 排序后的有效选项集合；空集合写 `[]` | `configuration-unknown` |
| `outputPath` | 执行时目标路径；不得把同名目录当稳定身份 | `output-unbound` |

### 2. InputIdentity

| 字段 | 要求 | 判定说明 |
|---|---|---|
| `gitHead` | 40 位 commit SHA | branch 名仅为导航，不可替代 HEAD |
| `worktreeState` | `clean` 或 `dirty` | dirty 不等于不可构建，但必须继续绑定 scoped manifest |
| `scopedChangeManifestHash` | 排序后的 staged/unstaged/untracked/deleted 目标路径、状态和内容哈希所形成的 SHA-256 | dirty 时必填；只记文件数量不够 |
| `unityVersion` / `unityRevision` | 来自 `ProjectVersion.txt` | 两者均参与比较 |
| `projectSettingsHash` | 当前 `ProjectSettings.asset` SHA-256 | 不用手抄若干字段替代 PlayerSettings 身份 |
| `projectConfigurationManifestHash` | 对目标构建实际生效的 ProjectSettings 文件清单、相对路径和逐文件 SHA-256 | 只哈希 `ProjectSettings.asset` 不足以覆盖场景、图形、质量或其他构建配置 |
| `packageManifestHash` / `packageLockHash` | `manifest.json` 与 `packages-lock.json` SHA-256 | lock 漂移使旧输入身份 stale |
| `sceneListHash` | 实际构建场景的规范化有序列表及启用状态哈希 | 场景相同但顺序变化仍是不同输入 |
| `defineSymbols` | 目标平台最终生效的符号集合，去重后 ordinal 排序 | 只读通用符号或字符串原顺序均不够 |
| `managedStrippingLevel` | 最终生效值和来源；无法解析时写 `unknown` | 不得用 Unity 默认值猜测 |
| `stripEngineCode` | 最终生效布尔值 | 与 managed stripping 分开记录 |
| `hybridClrInputIdentity` | HybridCLR 关闭时写 `not-applicable`；开启时使用下节字段 | 缺失则 HybridCLR 产物不可绑定 |

`scopedChangeManifestHash` 不要求默认哈希整个仓库。它要求先声明本次构建输入范围，再对范围内的脏文件记录规范化相对路径、Git 状态和内容 SHA-256。范围不完整比范围较小更危险：存在可能影响构建但未分类的脏文件时，状态必须是 `input-scope-unresolved`。

### 3. HybridCLR input identity

HybridCLR 开启时至少记录：

- `HybridCLRSettings.asset` SHA-256；
- HybridCLR 包版本、包源码身份或嵌入包内容快照；
- `BuildTarget`、architecture、development 与 scripting backend；
- 排序后的热更新程序集稳定身份及输入 DLL 哈希；
- 排序后的补充元数据/AOT 程序集身份；
- `link.xml`、AOT 泛型引用、桥接和 Reverse P/Invoke 生成输入哈希；
- 生成命令/版本、输出目录和每项输出哈希。

HybridCLR 生成物与最终 Player 只有在目标、配置、输入程序集和生成配置完全一致时才可绑定。同一天生成、文件存在、目录名称相同或 `GenerateAll()` 返回都不能替代输入哈希相等。

### 4. ExecutionIdentity

| 字段 | 要求 |
|---|---|
| `buildReceiptId` | 本次执行唯一稳定 ID；不得复用失败或上一次执行 ID |
| `actorId` / `taskId` | 执行者与任务身份 |
| `planHash` / `commandHash` / `skillHashes` | 受管执行时必填；不适用时写明确状态，不得伪造 |
| `startedAt` / `finishedAt` | UTC、可解析、结束不早于开始 |
| `unityExecutableHash` | 实际 Unity 可执行文件 SHA-256；只写版本不够定位二进制 |
| `toolchainIdentity` | IL2CPP 时记录 Unity 可检测的 VS 实例、MSVC 与 Windows SDK 版本；非 IL2CPP 写 `not-applicable` |
| `effectiveArguments` | 实际生效参数和选项的规范化表示；不得只记录 UI 按钮名称 |
| `inputIdentityHashBefore` / `inputIdentityHashAfter` | 执行前后复算；不一致则本次结果 `input-drifted` |
| `status` | `passed`、`failed`、`blocked`、`cancelled`、`interrupted` 或由 Finalize 强制产生的 `input-drifted` |
| `failure` / `recovery` / `staleWhen` | 成功也要保留可判定的空值或不适用状态 |

执行期间发生输入漂移时，已有日志可以作为诊断证据保留，但不能继续绑定为最终产物的完整来源。不得通过重新计算一个“结束时身份”掩盖中途混合输入。

### 5. ArtifactIdentity

每个输出必须记录以下语义；`createdByBuildReceiptId`、平台、架构、backend 与 development 可以由不可变的 enclosing receipt 统一绑定，不要求在每个 artifact 项内重复：

```text
artifactRole
projectRelativeOrDeclaredOutputPath
byteLength
sha256
createdByBuildReceiptId
platform
architecture
backend
development
```

以下对象分别记录，不能只对顶层目录计算一个模糊摘要：BuildReport/结构化结果、完整构建日志、Player 主可执行文件、数据目录/包、调试符号、IL2CPP/native 输出摘要、HybridCLR DLL/元数据/link/AOT 泛型输出。目录产物应使用“按 ordinal 相对路径排序的文件清单 + 每文件大小和 SHA-256”生成 manifest hash，并保留原始 manifest。

## Normalization and fingerprint rules

1. 所有路径先解析到声明根，再使用项目相对路径或声明的输出根相对路径；跨机器 fingerprint 不包含盘符大小写差异。
2. 路径分隔符规范化为 `/`；禁止保留 `.`、`..`、未解析环境变量或符号链接歧义。
3. 枚举、布尔和整数使用不依赖本地语言的固定表示；时间统一 UTC ISO 8601。
4. 集合只有在语义无序时才去重并按 ordinal 排序；构建场景、命令参数等有序序列必须保留顺序。
5. 缺失值写 `unknown`，不允许空字符串、默认值猜测或直接省略必填字段。真正不适用写 `not-applicable`。
6. 规范化对象使用固定字段名和稳定字段顺序编码为 UTF-8，再计算 SHA-256，得到 `buildInputFingerprint`。
7. `artifactManifestHash` 只覆盖输出；`buildInputFingerprint` 只覆盖输入。两者不得合并为一个无法区分输入/输出漂移的哈希。
8. 规范化算法必须记录 `fingerprintSchemaVersion`。版本变化后旧 fingerprint 不能直接按字符串比较，应先按原版本解析或标记 `schema-incompatible`。

## Executable protocol

当前可执行入口归现有 `$es-unity-compile`，不另建 Automation Worker：

```text
New-ESUnityBuildIdentitySnapshot.ps1
  -> 外部单独授权的 Unity Build
  -> Complete-ESUnityBuildIdentityReceipt.ps1
  -> Test-ESUnityBuildIdentityReceipt.ps1
```

- Capture 只读取项目/Git/配置/生成输入并可写 `ES/Output/BuildIdentity` 下的新 input receipt；输出目录必须位于 `ES/Output/Builds`。
- Finalize 从 immutable input receipt 重采输入，绑定执行字段并哈希声明产物；调用方报告 `passed` 时必须同时提供真实 `Unity.exe` 哈希及 `build-log` 或 `build-report` 角色。输入漂移强制返回 exit `2` 和 `input-drifted`。
- Validate 是只读消费者门禁：严格 UTF-8/JSON、当前 contract hash、内部 fingerprint/manifest、路径/reparse point、角色唯一性、实际 artifact 哈希，并默认重采当前输入。exit `0` 为当前静态身份通过，`1` 为非法/篡改/缺证据，`2` 为 stale/input-drifted。
- receipt 不可覆盖；失败写入保留唯一临时文件作为恢复现场，不由脚本静默删除。`-SkipCurrentInputCheck` 只允许取证式结构检查，永远不能证明 freshness。
- 这些入口不启动 Unity、不执行构建、不清理输出、不发布；它们产生的是 S1 provenance 证据，不能替代 compile/player/IL2CPP evidence 条目的目标层回执。

## Comparison verdicts

| Verdict | 充分条件 | 允许结论 |
|---|---|---|
| `SameBuildInput` | schema 兼容且全部必填 InputIdentity 字段、`buildInputFingerprint` 相等 | 输入身份相同；不保证输出相同或构建成功 |
| `SameBuildExecution` | 同一 `buildReceiptId`，前后输入哈希一致，ExecutionIdentity 完整 | 两份证据属于同一次执行 |
| `SameArtifact` | ArtifactIdentity 中角色、路径语义、大小和 SHA-256 相等 | 对应字节产物相同 |
| `ComparableBuilds` | 目标、平台、架构、backend、development、defines、裁剪、场景、HybridCLR 输入和 schema 均满足声明的比较策略 | 可进行限定范围回归对比 |
| `DifferentBuildInput` | 任一身份字段确定不相等 | 必须作为不同输入报告 |
| `InputDrifted` | 执行前后输入 fingerprint 不同 | 产物来源混合，不能接受为完整 provenance |
| `IdentityIncomplete` | 任一条件必填字段缺失或 unknown | 不得判定相同、可比较或新鲜 |
| `StaleArtifact` | SourceRef、输入、配置、工具链、回执或 freshness 条件已变化 | 旧产物只可隔离参考，不可代表当前构建 |

“相同 HEAD”最多证明已提交基线相同；dirty worktree、包锁、ProjectSettings、构建参数或工具链任一不同，仍可能是不同构建。“相同文件名/大小”也不能证明 `SameArtifact`，必须比较 SHA-256。

## Freshness and lifecycle

1. 构建计划生成时捕获 `BuildIntent`，执行开始前捕获并冻结 `InputIdentity`。
2. 生成输出使用新的、与 `buildReceiptId` 绑定的目录；若复用目录，必须先证明其中每个既有文件的来源并在 manifest 中区分，不得混合继承。
3. 失败、取消或中断时先保留日志、部分产物 manifest 和输入身份，再决定恢复。不得先清理现场来获得“干净结果”。
4. 执行结束后复算输入身份并生成 ArtifactIdentity；前后输入不同立即标记 `InputDrifted`。
5. 消费产物、做回归比较或发布前，重新检查 receipt、输入 fingerprint、输出哈希和 `staleWhen`。
6. 旧产物可以保留为历史或诊断材料，但必须隔离并标注 `not-current`；存在不等于可复用。
7. 恢复时从最早不一致层重放：Intent 不同则重新规划，Input 不同则重新快照，Execution 不完整则重建，Artifact 哈希不符则重新产出。不得只改 receipt 让旧产物看似匹配。

## Current verified source facts

- 项目固定 Unity `2022.3.45f1`，revision `a13dfa44d684`。
- `ProjectSettings.asset` 当前记录 `Standalone` scripting backend 为 `1`、`stripEngineCode: 1`；文件存在 `managedStrippingLevel` 映射但没有可据此确认的 Standalone 显式值，因此本条目不猜测最终裁剪等级。
- 当前 Standalone define symbols 包含项目设置中记录的符号集合；真正构建身份仍必须读取目标平台最终生效集合，而不是永久复制本条目的当前文本。
- HybridCLR 当前启用、使用项目内 IL2CPP，并声明热更新程序集、补充 AOT 元数据程序集及 link/AOT 泛型引用输出路径。
- `Packages/packages-lock.json` 中 HybridCLR 是 embedded package；其实际包版本还必须回读嵌入包的 `package.json` 或内容身份，不能只把 `file:` 引用当版本。
- 这些是 S1 配置事实，不证明当前 Unity 实例、工具链、生成物或 Player 已通过。

## Common AI failure modes

| 错误行为 | 根因 | 预防检查 | 正确动作与恢复 |
|---|---|---|---|
| 用 branch 名代替 HEAD | branch 可移动且不含脏改动 | 要求 40 位 HEAD 和工作树快照 | 降级为 `IdentityIncomplete`，重新捕获输入 |
| 只记录 HEAD，不记录 dirty worktree | 未提交内容实际参与构建 | 审计 staged/unstaged/untracked/deleted，并生成 scoped manifest | 隔离旧产物，补齐脏文件内容哈希后重建 |
| 用输出目录名或时间戳判定新鲜 | 名称可复用，时间可复制或失真 | 检查 receipt 和 ArtifactIdentity SHA-256 | 标记 `provenance-unbound`，不得复用 |
| 用 BuildReport/退出码 0 代替身份 | 成功状态不说明输入来源 | 同时验证 input fingerprint 与 receipt 绑定 | 保留成功证据但撤回“属于当前输入”结论 |
| 不记录最终 defines、裁剪或 BuildOptions | UI/脚本/平台可改变有效值 | 从执行时有效配置生成规范化字段 | 配置未知则不同构建不可比较 |
| HybridCLR 生成物与最终 Player 只按目录匹配 | 目标或程序集输入可能已漂移 | 比较目标、development、程序集及生成配置哈希 | 从最早漂移输入重新生成并构建 |
| 只记录 `cl.exe` 路径作为工具链身份 | Unity 可能无法检测未注册实例 | 记录 Unity 可检测 VS 实例、MSVC、SDK | 工具链身份不完整时停止 IL2CPP 结论 |
| 对整个目录只记最后修改时间或总大小 | 无法发现单文件替换 | 生成稳定文件 manifest 和每文件 SHA-256 | 重建 manifest；旧目录保持 `not-current` |
| 构建中输入变化后只采用结束时哈希 | 产物可能混合前后两份输入 | 比较执行前后 fingerprint | 标记 `InputDrifted`，使用新输出目录重放 |
| 比较两次性能/体积却忽略平台和配置 | 不同条件不可归因 | 先满足 `ComparableBuilds` | 只报告两个独立观察，不声称回归 |
| 缺字段时套用 Unity 默认值 | 默认值受版本、平台和调用方式影响 | 缺失写 `unknown` | 回读实际有效配置，不能猜测补齐 |
| receipt 指向聊天摘要或瞬时 Console | 证据不可重读、不可验哈希 | 要求项目内或声明输出根下可重读证据 | 降级并重放对应证据层 |
| 看见 dirty worktree 就先 clean/reset | 把输入身份问题误当成环境卫生问题，并可能删除并发改动 | dirty 时捕获 staged/unstaged/untracked/deleted 的路径、状态、大小和内容哈希 | 保留工作树；无法界定输入时写 `input-scope-unresolved`，不得擅自清理 |
| 把 BuildSummary 的 GUID、时间和 success 当完整构建身份 | Unity 执行摘要不包含 Git、包锁和 HybridCLR 输入 | 将官方字段绑定到同一 receipt，并独立校验 `InputIdentity` | 保留 Player 成功事实，但身份降级为 `provenance-unbound` |

## High-risk fixed decision scenario

固定场景：存在昨天的 `StandaloneWindows64 + IL2CPP` Player 和日志；今天 HEAD 或 dirty worktree 中的热更程序集已变化；没有绑定当前输入的 receipt；HybridCLR 已启用但目标平台 stripped AOT manifest 为空。

AI 必须按以下顺序裁决，任一回答缺少第 1～3 项即视为高危遗漏：

1. 旧 Player 立即标记 `provenance-unbound` 或 `stale`，不得复用、比较或发布；目录名、时间、BuildReport success 和 Unity 版本相同都不能恢复身份。
2. dirty worktree 不自动等于失败，也不授权 clean/reset；先 Capture 当前完整 scoped manifest。无法稳定捕获时停止为 `input-scope-unresolved`。
3. HybridCLR 启用而 stripped AOT manifest 为空时，当前输入为 `identity-incomplete`；不得把热更 DLL、`link.xml` 或 `GenerateAll()` 的旧日志当替代品。
4. 最小恢复是保留旧输出用于诊断，在新的隔离输出目录执行 `Capture -> 当前目标 HybridCLR 生成 -> Player Build -> Finalize -> Validate`；输入前后漂移时必须返回 `input-drifted`/exit `2`。
5. 即使身份最终为 current，发布结论仍必须由 `es.unity.compile-player-il2cpp-evidence.v1` 的目标层 Runtime/Release 证据另行证明。

## Execution checklist

开始前：

1. 读取 AIBRAIN_ENTRY，加载本条目及目标证据层的 canonical Knowledge。
2. 固定 ProjectRoot、branch、HEAD、工作树范围、Unity 版本、目标、架构、backend、development 和输出根。
3. 审计 staged/unstaged/untracked/deleted；无法界定构建输入影响面时停止为 `input-scope-unresolved`。
4. 解析最终场景顺序、defines、裁剪、BuildOptions、包锁及 HybridCLR 条件字段。
5. 生成带 schema 版本的 `BuildIntent` 与 `InputIdentity`，记录 `buildInputFingerprint`。

执行中：

1. 使用唯一 `buildReceiptId` 和隔离输出目录。
2. 记录实际 Unity、工具链、命令参数、Task/Plan/Command/Skill 身份及起止时间。
3. 不修改输入、切换目标或复用其他构建目录；发现漂移立即停止升级结论。
4. 对失败和中断先保留证据，再按最早失败层恢复。

完成后：

1. 复算输入 fingerprint；与开始值不同则 `InputDrifted`。
2. 为 BuildReport、日志、Player、IL2CPP 和 HybridCLR 输出生成逐项 ArtifactIdentity。
3. 按目标证据 profile 判定通过/失败；身份完整不能替代运行证据。
4. 消费、比较或发布前再次检查 freshness、schema 和哈希。
5. 报告 `claimsNotProven`、未验证层、残余风险和最小重放动作。

## AI answer template

```text
目标声明：要确认来源、比较、复用还是发布绑定
项目绑定：ProjectRoot、branch、HEAD、worktreeState、scopedChangeManifestHash
构建目标：BuildTarget、TargetGroup、architecture、backend、development、BuildOptions
配置身份：Unity/revision、ProjectSettings、packages、scenes、defines、stripping
HybridCLR 身份：not-applicable 或配置/程序集/生成输入与输出哈希
buildReceiptId：精确 ID
buildInputFingerprint：schemaVersion + SHA-256
执行身份：actor/task/plan/command/skills、Unity executable、toolchain、起止时间
产物身份：角色、路径、大小、SHA-256、artifactManifestHash
比较结论：SameBuildInput / SameBuildExecution / SameArtifact / ComparableBuilds / DifferentBuildInput / InputDrifted / IdentityIncomplete / StaleArtifact
最高证据层：S0-S6
Static 状态：static-passed / static-partial / static-blocked
Runtime 状态：runtime-passed / runtime-not-run / runtime-not-authorized / runtime-blocked / runtime-failed
未验证：缺失的身份字段或证据层
claimsNotProven：不能由当前身份或证据推出的结论
nextAction：补齐身份、重放构建或补目标层证据的最小动作
```

## Evidence boundary and non-claims

本条目静态证明的是身份字段合同、项目当前配置事实、可执行 Capture/Finalize/Validate 协议、路由边界、SourceRef 和 ContentHash。代表性测试可以生成隔离的合成 receipt、指纹与 artifact manifest，但不等于真实 Unity BuildReceipt，也不证明任一现有 Player 的来源。

本次未启动 Unity、未触发编译或 Domain Reload、未构建 Player、未运行 IL2CPP/HybridCLR 生成、未计算发布产物 manifest、未执行跨机器复现或发布流程。`runtime-not-run`；当前最高证据等级为 `S1`，不构成构建、产物或发布验收。
