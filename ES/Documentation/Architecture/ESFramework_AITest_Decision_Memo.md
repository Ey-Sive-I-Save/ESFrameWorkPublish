# ESFramework.AITest 架构裁决备忘录

状态：裁决备忘录，不修改 asmdef。
日期：2026-08-09

## 事实

- 包路径：`Packages/com.esframework.aitest`
- 程序集：`ESFramework.AITest`
- 程序集位置：`Runtime`
- `includePlatforms`：空
- `autoReferenced`：true
- references：`Unity.ugui`
- 未发现引用 UnityEditor
- `ES_Logic.asmdef` 直接引用 `ESFramework.AITest`，无 `defineConstraints`
- `ES_Logic` Runtime 下存在直接使用 `ESFramework.ESAITest` 的源码
- 包内存在 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`

结论：它不是普通 Tests 程序集，而是会进入 Player、默认自动引用、并注册启动期入口的 Runtime Package。

## 方案 A：保留为正式 Runtime 硬依赖

继续保留 `ES_Logic -> ESFramework.AITest` 必须证明：

- AITest 是正式产品运行能力，不是开发验收能力。
- 默认自动引用和启动期注册没有非预期副作用。
- Player 确实需要在 `BeforeSceneLoad` 阶段执行 AITest 入口。
- 正式发布构建允许携带测试入口。
- 有明确关闭、裁剪和诊断方式。
- AITest 的稳定契约由 ES 核心拥有，而不是作为旁路能力自行发展。

风险：

- 正式 Runtime 反向依赖 AI 验收运行时。
- AITest 被无条件编入 Player。
- 启动期注册可能增加正式产品路径的行为面。

## 方案 B：改为可选开发/验收集成

推荐方向：

- `ES_Logic` 不再无条件直接依赖 `ESFramework.AITest`。
- `ESFramework.AITest` 依赖 ES 稳定 Contract，或通过 Adapter 接入 `ES_Logic`。
- 通过显式 Bootstrap、Feature Package 或独立程序集接入。
- 默认不进入正式发布闭包。
- AITest 只能在显式验收入口下激活。

优点：

- 正式 Runtime 不再被开发验收能力污染。
- AITest 可以独立演进、裁剪和版本化。
- 依赖方向恢复为 `AITest -> Contract/Adapter`，而不是 `Runtime -> AITest`。

## 推荐结论

当前主要消费者如果是开发验收和端到端 AI 测试，推荐方案 B。

如果未来 AITest 被正式定义为产品内置能力，再按方案 A 走完整 Release Contract，不能仅凭“包名包含 Test”保留在正式 Runtime 依赖中。

## 最终裁决（Checkpoint C 前置）

推荐采用方案 B，并按三层拆分：

```text
ES_Logic Runtime
        ↓
ESFramework.AITest.Contracts（稳定协议，最小、可安全引用）
        ↑
ESFramework.AITest.Runtime（开发/验收实现，默认不进正式发布闭包）
        ↑
ESFramework.AITest.Editor/Runner（Editor、Runner、执行入口）
```

职责：

- `Contracts`：稳定协议，可被正式 Runtime 安全引用。
- `Runtime`：开发/验收实现，默认不进入正式 Player 发布闭包。
- `Editor/Runner`：Editor、测试执行和验收入口。

禁止：

- `ES_Logic` 直接依赖 `ESFramework.AITest.Runtime`。
- `ESFramework.AITest.Runtime` 反向成为正式 Player 的无条件依赖。

当前 asmdef 已按该拆分实施；仍待 Unity 编译与 ReloadDomain 验证。

实际实施包含四个可选 AITest 程序集：

- `ESFramework.AITest.Contracts`
- `ESFramework.AITest.Runtime`
- `ESFramework.AITest.Editor`
- `ES_AITest.Runtime.Adapters`

## 修正后的最终结构

不能把 AITest.Contracts 直接作为 ES_Logic 的正式依赖，否则只是把强依赖从 Runtime 移到 Contracts。

修正结构：

```text
ES_Stand
  -> ESFramework.Diagnostics.*（Stand 程序集内命名空间）

ES_Logic
  -> ES_Stand

Packages/com.esframework.aitest
  -> Contracts/ESFramework.AITest.Contracts
  -> Runtime/ESFramework.AITest.Runtime
  -> Editor/ESFramework.AITest.Editor
```

依赖方向：

```text
ES_Logic -> ESFramework.Diagnostics.Contracts

AITest.Runtime
  -> ES_Stand
  -> ESFramework.AITest.Contracts

AITest.Editor
  -> AITest.Runtime
  -> ES_Stand
  -> ESFramework.AITest.Contracts
```

职责：

- `ESFramework.Diagnostics.*`：正式框架的通用诊断/观察契约，属于 `ES_Stand` 程序集，不单独创建热更程序集。
- `ESFramework.AITest.Contracts`：AITest 专用测试协议，只属于可选 AITest 包。
- `ESFramework.AITest.Runtime`：可选验收执行能力。
- `ESFramework.AITest.Editor`：Editor、Runner、测试工具入口。
- `ES_AITest.Runtime.Adapters`：ES 与 AITest.Runtime 之间的显式 Adapter。

特性分类：

- 通用运行时契约：放 `ESFramework.Diagnostics.Contracts`。
- AITest 专用标记：放 `ESFramework.AITest.Contracts` 或 `AITest.Editor`。
- Editor-only 特性：放 `AITest.Editor`。
- 专项优化：通过 Provider/Capability 注入，不能让 AITest Runtime 进入 ES 热路径。

## ES_Logic.Editor 建议

默认不直接依赖 `AITest.Editor`。

更稳妥：

```text
ES_Logic.Editor -> Stable Editor Contract / Adapter Registry
AITest.Editor -> Adapter Registry + AITest.Runtime + Stable Contracts
```

只有明确要求 ES 基础 Editor 默认必带 AITest 工具时，才允许：

```text
ES_Logic.Editor -> AITest.Editor
```

即使允许，也必须是 Editor-only，不进入 Player。

## 待裁决

- AITest 当前实际消费者是否只有开发验收。
- 是否允许正式 Player 携带 AITest 启动入口。
- `ES_Logic` 与 AITest 之间应由谁拥有稳定接口。
- 是否使用 `defineConstraints`、独立程序集或 Package 可选依赖隔离。

本轮不修改 asmdef。
