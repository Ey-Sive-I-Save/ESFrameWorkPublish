# ES Framework 架构冻结 Checkpoint A

状态：Checkpoint A Final。只读事实盘点已关闭，不再扩大扫描范围；未写正式审计状态，未改项目源码、asmdef、Git 或 Unity 状态。
日期：2026-08-09

## 1. 冻结基线

- Git branch：`main...origin/main`
- HEAD：`f812e104595bec256b3f9929014ec9184cb537d7`
- HEAD message：`feat(editor): improve delivery workflows and project integration`
- Unity：`2022.3.45f1 (a13dfa44d684)`
- 工作树：存在大量 unstaged modified 与 untracked 文件；当前 status 输出未看到已暂存条目，但正式冻结前应再显式核对 `git diff --cached`。

当前工作树不能作为 Architecture Contract 的 Freeze Baseline。只有明确 HEAD 可以成为冻结基线；工作树内容必须标记为“未冻结的当前实现”。

## 2. 生成工程与运行证据现状

- 根目录存在多个 `.csproj`，最近更新时间包括 2026-08-09。
- 这些 `.csproj` 只能说明 IDE 生成工程快照较新，不能代替 Unity Editor 编译、ReloadDomain、Test Runner 或 PlayMode。
- 本次盘点未重跑 Unity；因此以下证据等级均未采集：
  - UnityCompiled
  - ReloadDomainVerified
  - TestRunnerVerified
  - PlayModeVerified
  - ReleaseVerified

## 3. 当前 asmdef 事实

### Runtime / 通用

`ES_Stand`

```json
references: Unity.Timeline, Unity.TextMeshPro, UniTask, HybridCLR.Runtime
```

`ES_Design`

```json
references: GUID:dc8f60a3ad5a07d4787d9e672b052700,
            GUID:2665a8d13d1b3f18800f46e256720795,
            Unity.InputSystem
```

`ES_Logic`

```json
references: ES_Stand, ES_Design, UniTask, Unity.TextMeshPro, Cinemachine,
            KCC, Unity.InputSystem, RootMotion, EasySave3, ESFramework.AITest
```

`ESPlayer`

```json
references: GUID:dc8f60a3ad5a07d4787d9e672b052700,
            GUID:1764b38706c31e34885d5988a5436060,
            GUID:14fd0e2674c4b6144985a0522c97c44b,
            GUID:6055be8ebefd69e48b49212b09b47b2f,
            GUID:9e7c1cf928ca1334982d7e61737420ec
```

### Editor

`ES_Editor`

```json
includePlatforms: Editor
references: ES_Stand, ES_Design, ES_Logic, UniTask,
            Unity.InputSystem, Unity.TextMeshPro, HybridCLR.Editor
```

`ESInstaller`

```json
includePlatforms: Editor
references: ES_Stand
```

`ES_Logic.Editor`

```json
includePlatforms: Editor
references: ES_Logic, ES_Editor, ES_Stand, ES_Design, Cinemachine,
            KCC, Unity.InputSystem, Unity.TextMeshPro, ESFramework.AITest
```

### Tests

`ES_Design.ConfigKey.Tests`

```json
includePlatforms: Editor
references: ES_Design, ES_Stand, ES_Logic, ES_Editor, UniTask, KCC
```

`ES_Logic.DynamicAtlas.Tests`

```json
includePlatforms: Editor
references: ES_Logic, ES_Stand, ES_Design, UniTask
```

`ES_Logic.DynamicAtlas.PlayMode.Tests`

```json
includePlatforms: []
references: ES_Logic, ES_Stand, ES_Design, UniTask
```

`ES_Logic.Story.Tests`

```json
includePlatforms: Editor
references: ES_Logic, ES_Stand, ES_Design
```

`ES_Logic.Editor.Generation.Tests`

```json
includePlatforms: Editor
references: ES_Logic.Editor, ES_Logic, ES_Stand, ES_Design
```

### GUID 解析结果

| GUID | 程序集名 | 分类 |
|---|---|---|
| `dc8f60a3ad5a07d4787d9e672b052700` | `ES_Stand` | Runtime/基础 |
| `1764b38706c31e34885d5988a5436060` | `ES_Design` | Design/配置 |
| `14fd0e2674c4b6144985a0522c97c44b` | `ES_Logic` | Runtime |
| `2665a8d13d1b3f18800f46e256720795` | `Unity.Burst` | Runtime/Package |
| `6055be8ebefd69e48b49212b09b47b2f` | `Unity.TextMeshPro` | Runtime/Package |
| `9e7c1cf928ca1334982d7e61737420ec` | `DOTween.Modules` | Runtime/Package |

解析后：

`ES_Design`

```json
references: ES_Stand, Unity.Burst, Unity.InputSystem
```

`ESPlayer`

```json
references: ES_Stand, ES_Design, ES_Logic, Unity.TextMeshPro, DOTween.Modules
```

### ESFramework.AITest 判定

- 包路径：`Packages/com.esframework.aitest`
- 程序集：`ESFramework.AITest`
- 程序集位置：`Runtime`
- `includePlatforms`：空，表示不排除 Player
- `autoReferenced`：true
- references：`Unity.ugui`
- 未发现引用 UnityEditor 的 Editor API
- `ES_Logic.asmdef` 直接引用 `ESFramework.AITest`，且无 `defineConstraints`，因此是编译期硬依赖
- `Assets/Scripts/ESLogic/Runtime/Developer/AITest` 下的 Runtime 源码直接使用 `ESFramework.ESAITest` 类型
- 包内存在多个 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`

结论：

- `ESFramework.AITest` 是 embedded runtime package，不是 Editor-only 或 Tests-only。
- 当前 `ES_Logic Runtime -> ESFramework.AITest` 是 Player 发布闭包内的硬依赖。
- 如果 AITest 属于验收/开发测试能力，当前方向存在架构边界风险。
- 更合理的依赖方向应是 `ESFramework.AITest -> ES_Logic`，或通过 `defineConstraints`/独立 Adapter 隔离，不能让正式 Runtime 反向依赖 AI 验收运行时。

当前没有统一“程序集角色表”，也没有“允许依赖 / 禁止依赖”门禁。

## 4. 八类核心语义证据表

| 契约项 | 权威入口 | 源码状态 | 静态测试 | Unity 编译 | PlayMode | Release | OutsideFreezeBaseline |
|---|---|---|---|---|---|---|---|
| 稳定身份 | `ESConfigKey`、`ESKeyCatalog`、GameCore Catalog | SourceOnly/已有实现 | 已有 ConfigKey/Tag/SchemaHash 测试源码 | 未采集 | 未采集 | 未采集 | 工作树包含相关改动 |
| DataInfo/Group/Pack/GameCore | AIWarnings P0、SoDataInfo/Group/Pack 规则 | SourceOnly/已有实现 | 已有 InfoGroup 契约测试源码 | 未采集 | 未采集 | 未采集 | 工作树包含相关改动 |
| Domain/Module/Service/Profile | Profile P0、GameManager Module 规则 | SourceOnly/已有实现 | 已有 GameManager/Profile 测试源码 | 未采集 | 未采集 | 未采集 | 工作树包含相关改动 |
| Entity/Pool/Scope/Context | `ES_GENERIC_LIFE`、Pool/Scope/Context 规则 | SourceOnly/已有实现 | 已有 GenericLife/Scope Pool 测试源码 | 未采集 | 未采集 | 未采集 | 工作树包含相关改动 |
| 请求仲裁/控制/输入/命令 | ES 活跃请求仲裁协议、Camera Director、ESCommand | SourceOnly/已有实现 | 已有 Camera/Command/Skill 测试源码 | 未采集 | 未采集 | 未采集 | 工作树包含相关改动 |
| Runtime/Editor 程序集边界 | asmdef | 已盘点，未建立门禁 | 无独立边界测试 | 未采集 | 未采集 | 未采集 | asmdef 文件存在于工作树 |
| 错误/取消/失效/恢复 | 领域局部 Lease/Cancel/Recovery | SourceOnly/局部实现 | 无统一核心测试 | 未采集 | 未采集 | 未采集 | 无统一实现 |
| 版本迁移 | Story/Key 等局部契约 | SourceOnly/局部实现 | 无框架级迁移测试 | 未采集 | 未采集 | 未采集 | 未形成统一契约 |

## 4. 现有核心语义权威

### 稳定身份

- 权威来源：
  - `Documentation/KEY_GOVERNANCE.md`
  - `ESConfigKey`
  - `ESKeyCatalog`
  - Tag / Attribute / Input / Camera Catalog 的 SchemaHash
- 现状：多个领域已有 RuntimeKey、SchemaHash 和双键语义，但没有单一稳定身份规范。

### DataInfo / Group / Pack / GameCore

- 权威来源：AIWarnings P0：Info 必须对应 Group，Pack 非默认聚合。
- 现状：规则存在，但没有统一归属矩阵。

### Domain / Module / Service / Profile

- 权威来源：Profile 装配 P0、GameManager Module 规则、Domain/Service 边界警告。
- 现状：术语有约束，但未形成统一边界矩阵。

### Entity / Pool / Scope / Context

- 权威来源：`ES_GENERIC_LIFE.md`、对象池规则、Scope Registry 规则、Context 规则。
- 现状：源码和专项规则多，未形成统一所有权图。

### 请求仲裁 / 控制权 / 输入 / 命令

- 权威来源：ES 活跃请求仲裁协议、Camera Director、LocalControl、ESCommand。
- 现状：协议已有，但 Camera 仍被视为首切片，未覆盖全部控制域。

### 错误 / 取消 / 失效 / 恢复

- 现状：各系统有局部 Lease/Cancel/Recovery 语义。
- 缺口：没有统一 `ESOperationRun / Journal / Lock / NeedsRecovery` 核心。

### 版本迁移

- 现状：Story 有切片迁移契约，Tag/Key 有 SchemaHash 治理。
- 缺口：没有框架级通用迁移协议。

## 5. 测试与审计现状

- 当前 `Assets` 下发现约 30 个 `*Tests.cs` 文件。
- 覆盖领域包括：ConfigKey、Tag、Camera、Asset Scope、GenericLife、Skill、Vehicle、DynamicAtlas、Story、Graph、Automation 等。
- `ES/Documentation/Status/MODULE_AUDIT_STATE.md` 已存在，当前包含模块块：
  - `story-non-player-quest`
  - `es-graph-authoring-bake`
  - `es-command-skills-graph-integration`
  - `es-command-runtime`
  - `es-skill-definition-runtime`
- 本次 Checkpoint A 不修改该文件。

## 6. 当前缺口

1. 没有单一 `ES Framework Architecture Contract`。
2. 没有统一核心生命周期图。
3. 没有正式模块依赖矩阵。
4. asmdef GUID 已解析，但没有依赖门禁。
5. Runtime/Editor 边界只有 asmdef，没有门禁。
6. 没有统一错误/取消/失效/恢复契约。
7. 没有框架级版本迁移协议。
8. 没有“新增违规”增量门禁。
9. Unity 编译、ReloadDomain、Test Runner、PlayMode 证据未采集。
10. `ES_Logic Runtime -> ESFramework.AITest` 是潜在反向依赖，需要契约裁决。

## 7. 下一步

Checkpoint A 还需要完成：

- GUID asmdef 引用已解析；下一步确认 `ESFramework.AITest` 应保留为正式 Runtime 依赖，还是改为 `ES_Logic -> AITest` 的隔离适配。
- 八类核心语义表已建立，但 Unity 证据仍全部未采集。
- 下一步输出正式 asmdef 现状报告，并把“新增违规”作为门禁基线。
- 不写正式审计状态。

Checkpoint A 完成后，再进入 Checkpoint B 写 Architecture Contract v0.1。
