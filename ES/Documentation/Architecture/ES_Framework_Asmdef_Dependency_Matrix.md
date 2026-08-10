# ES Framework asmdef Dependency Matrix

状态：只读事实矩阵。基于真实 `.asmdef`，不基于生成 `.csproj`。

## Core Runtime / Design / Player

| Assembly | Class | Direct References | Notes |
|---|---|---|---|
| `ES_Stand` | Runtime | Unity.Timeline; Unity.TextMeshPro; UniTask; HybridCLR.Runtime | 基础层 |
| `ES_Design` | Runtime | ES_Stand; Unity.Burst; Unity.InputSystem | 配置/身份层 |
| `ES_Logic` | Runtime | ES_Stand; ES_Design; UniTask; Unity.TextMeshPro; Cinemachine; KCC; Unity.InputSystem; RootMotion; EasySave3; ESFramework.AITest | `ESFramework.AITest` PendingDecision |
| `ESPlayer` | Player | ES_Stand; ES_Design; ES_Logic; Unity.TextMeshPro; DOTween.Modules | Player 层 |
| `ESFramework.AITest` | Runtime Package | Unity.ugui | embedded runtime package；自动引用；未发现 Editor API |

## Editor

| Assembly | Direct References |
|---|---|
| `ES_Editor` | ES_Stand; ES_Design; ES_Logic; UniTask; Unity.InputSystem; Unity.TextMeshPro; HybridCLR.Editor |
| `ES_Logic.Editor` | ES_Logic; ES_Editor; ES_Stand; ES_Design; Cinemachine; KCC; Unity.InputSystem; Unity.TextMeshPro; ESFramework.AITest |
| `ESInstaller` | ES_Stand |
| `KCC.Editor` | KCC |
| `RootMotionEditor` | RootMotion |
| `VFolders` / `VHierarchy` | 空/第三方 |

## Tests

| Assembly | Direct References |
|---|---|
| `ES_Design.ConfigKey.Tests` | ES_Design; ES_Stand; ES_Logic; ES_Editor; UniTask; KCC |
| `ES_Logic.DynamicAtlas.Tests` | ES_Logic; ES_Stand; ES_Design; UniTask |
| `ES_Logic.DynamicAtlas.PlayMode.Tests` | ES_Logic; ES_Stand; ES_Design; UniTask |
| `ES_Logic.Story.Tests` | ES_Logic; ES_Stand; ES_Design |
| `ES_Logic.Editor.Generation.Tests` | ES_Logic.Editor; ES_Logic; ES_Stand; ES_Design |
| `ES_Stand.ValueChange.Tests` | ES_Stand; ES_Logic; ES_Design |

## Examples / Obsolete

- Examples：`ES_Samples.*` 依赖 `ES_Logic` 或 `ES_Editor`。
- Obsolete：`ES.ResourceV1.Obsolete` 依赖 `ES_Stand`；`ESLogic_TestSoData_Obsolete` 依赖 `ES_Logic`；其余 Obsolete 多为空依赖或旧程序集内部依赖。

## PendingDecision

- 无。
- `ES_Logic` 已移除对 `ESFramework.AITest` 和 `ESFramework.AITest.Runtime` 的直接依赖。

## 当前 AITest 拆分

当前已实施：

| Assembly | Class | Expected References |
|---|---|---|
| `ESFramework.AITest.Contracts` | Optional Contracts | AITest 专用测试协议；不进入 `ES_Logic` 正式依赖 |
| `ESFramework.AITest.Runtime` | Runtime Optional | Diagnostics.Contracts + AITest.Contracts；默认不进正式发布闭包 |
| `ESFramework.AITest.Editor` | Editor/Runner | AITest.Runtime + Diagnostics.Contracts + AITest.Contracts；Editor-only |
| `ES_AITest.Runtime.Adapters` | Runtime Optional | AITest.Runtime + Diagnostics.Contracts + ES_Stand + ES_Design + ES_Logic |

`ESFramework.Diagnostics.*` 不单独作为 asmdef；它属于 `ES_Stand` 程序集内的稳定命名空间。

当前依赖方向：

```text
ES_Logic -> ESFramework.Diagnostics.Contracts

AITest.Runtime
  -> ESFramework.Diagnostics.Contracts
  -> ESFramework.AITest.Contracts

AITest.Editor
  -> AITest.Runtime
  -> ESFramework.Diagnostics.Contracts
  -> ESFramework.AITest.Contracts
```

禁止：

- `ES_Logic` 直接依赖 `ESFramework.AITest.Runtime`。
- `ES_Logic` 默认直接依赖 `ESFramework.AITest.Contracts`。
- `ES_Logic.Editor` 默认直接依赖 `ESFramework.AITest.Editor`。

## 候选禁止边

以下为候选门禁，不实现：

- Runtime 不得引用 Editor asmdef。
- `ES_Logic` 不得无条件依赖 Test/Developer runtime package。
- GameCore/Design 层不得反向依赖 `ES_Logic` 或 `ESPlayer`。
- Player 发布闭包不得包含 Editor-only asmdef。
- 未裁决原型不得进入正式依赖闭包。
- Obsolete asmdef 不得被正式模块引用。
