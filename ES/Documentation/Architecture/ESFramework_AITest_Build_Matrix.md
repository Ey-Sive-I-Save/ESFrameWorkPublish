# ESFramework.AITest Build Matrix

状态：目标构建矩阵，不修改 asmdef。

## 四种构建

| 构建 | ES_Logic | ES_Stand/Diagnostics.* | AITest.Contracts | AITest.Runtime | AITest.Editor | Runtime.Adapters |
|---|---|---|---|---|---|---|
| 默认 Player | 有 | 有 | 有 | 有 | 无 | 有 |
| 验收 Player | 有 | 有 | 有 | 有 | 无 | 有 |
| 默认 Editor | 有 | 有 | 有 | 有 | 有 | 有 |
| 验收 Editor | 有 | 有 | 有 | 有 | 有 | 有 |

当前部署模型：方案一。`AITest.Runtime` 始终进入 Player，默认休眠，按需激活；`AITest.Editor` 只进入 Editor/测试闭包。

## 依赖约束

- `ES_Logic` 默认只依赖 `ES_Stand`，诊断契约位于 `ES_Stand` 内的 `ESFramework.Diagnostics.*` 命名空间。
- `ESFramework.AITest.Contracts` 只属于可选 AITest 包。
- `ESFramework.AITest.Runtime` 必须依赖 Diagnostics.Contracts 与 AITest.Contracts。
- `ESFramework.AITest.Editor` 必须依赖 AITest.Runtime、Diagnostics.Contracts 与 AITest.Contracts。
- `ES_Logic.Editor` 默认不直接依赖 AITest.Editor，应使用 Adapter/Registry。

## defineConstraints 注意点

- `defineConstraints` 可以控制程序集是否参与编译和装配，但不能替代依赖图设计。
- 如果 `AITest.Editor -> AITest.Runtime`，两者的 define 条件必须一致。
- `autoReferenced:false` 不能修复显式引用。
- 必须用实际 Unity Player 构建确认闭包，不能只看 asmdef 或 .csproj。

## 禁止

- 默认正式 Player 携带 AITest.Runtime。
- 默认正式 Player 携带 AITest.Editor。
- ES_Logic 热路径依赖 AITest Runtime 作为必要执行者。
- AITest 特性以“专项优化”名义直接改写 ES_Logic 热路径。
