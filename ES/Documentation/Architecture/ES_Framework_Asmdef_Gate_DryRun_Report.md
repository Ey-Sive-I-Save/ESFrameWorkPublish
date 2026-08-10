# ES Framework asmdef Gate DryRun Report

状态：DryRun 结果报告，不修改 asmdef 或源码。
日期：2026-08-09

## 命令

```powershell
& 'F:\aaProject\ESFrameWorkPublish\ES\Documentation\Architecture\ES_Framework_Asmdef_Gate_DryRun.ps1'
```

## 结果

- Assemblies inspected：44
- BaselineViolations：0
- PendingDecisions：0

## PendingDecision

```text
无
```

`ES_Logic` 已不再直接引用 `ESFramework.AITest` 或 `ESFramework.AITest.Runtime`。

当前拆分：

- `ESFramework.Diagnostics.Contracts`
- `ESFramework.AITest.Contracts`
- `ESFramework.AITest.Runtime`
- `ESFramework.AITest.Editor`
- `ES_AITest.Runtime.Adapters`

## 误报检查

- 未发现 Runtime/Player 引用 Editor 的误报。
- 未发现正式模块引用 Obsolete 的误报。
- Tests 引用 Editor/Runtime 未被误报。
- 已修正分类顺序：`Obsolete` 优先于 `Tests`，避免 `ESLogic_TestSoData_Obsolete` 被误判。

## 次要观察

- `ES_Logic.Editor` 已移除对 `ESFramework.AITest` 的直接引用。
- `ESFramework.AITest.Editor` 目前是独立 Editor-only 程序集。
- `ES_AITest.Runtime.Adapters` 是 ES 与 AITest.Runtime 之间的显式 Adapter。

## 边界

- 未修改 asmdef。
- 未修改源码。
- 未写 MODULE_AUDIT_STATE.md。
