# Checkpoint C Implementation Status

状态：asmdef 拆分已实施；Unity 编译与 ReloadDomain 被生成工程刷新阻塞。
日期：2026-08-09

## 已实施

- `ESFramework.Diagnostics.*` 契约并入 `ES_Stand` 程序集，不新增独立热更程序集。
- 新增 `ESFramework.AITest.Contracts`。
- 重命名并调整 `ESFramework.AITest.Runtime`。
- 新增 `ESFramework.AITest.Editor`。
- 新增 `ES_AITest.Runtime.Adapters`。
- `ES_Logic` 移除对 AITest 的直接依赖。
- `ES_Logic.Editor` 移除对 AITest 的直接依赖。
- AITest 协议、属性、CapabilityProvider 契约移入 `AITest.Contracts`。
- AITest Editor 文件移入 `AITest.Editor`。

当前 AITest 程序集共四个，已全部启用：

- `ESFramework.AITest.Contracts`
- `ESFramework.AITest.Runtime`
- `ESFramework.AITest.Editor`
- `ES_AITest.Runtime.Adapters`

`ToUse / ToSee / ToVerify` 特性已移入 `ES_Stand`，所有 Logic 和常规脚本均可引用。

四个 AITest 程序集已全部启用并设为 `autoReferenced=true`，`Publish` 等 Runtime 类型也可被常规脚本引用。

部署模型：方案一（功能可选）。`AITest.Runtime` 始终进入 Player，默认不启动；已修复 `ESAITestConversationIpcBootstrap` 无条件创建对象的默认副作用。

生产策略：普通 Release Player 即使带 AITest 启动参数也拒绝激活；仅允许 Editor、DevelopmentBuild 或显式 `ES_AITEST_ACCEPTANCE` 构建激活。

## DryRun

- Assemblies inspected：44
- BaselineViolations：0
- PendingDecisions：0

## 编译证据

`dotnet build ES_Editor.csproj --no-restore` 失败：

```text
CSC : error CS2001: 未能找到源文件
  Packages\com.esframework.aitest\Runtime\ESAITestCapabilityAttributes.cs
  Packages\com.esframework.aitest\Runtime\AITestProtocol.cs
```

最新复核：

- `dotnet build ES_Editor.csproj --no-restore`：0 warning / 0 error
- 该结果只覆盖默认配置，不编译被 `ES_AITEST_ENABLED` 排除的可选 AITest 程序集
- Unity Editor 日志仍包含编译错误：
  - `CS2001`：旧 AITest 路径仍被引用
  - 旧日志中的 `InspectorUser_ScriptQuickFilter.cs` 错误，当前源码已不再包含 `RepaintInspectorWindow` 或 `GlobalObjectId.isValid`，判定为旧版本日志残留

原因：生成工程/Unity 导入状态仍包含旧 asmdef 快照。

处理原则：

- 不手改 Unity 生成的 `.csproj`。
- 需要 Unity 刷新/重新生成工程。
- 当前项目被 Unity 实例占用，无法安全执行独立 ReloadDomain。

## 下一步

1. Unity 刷新工程并重新生成 `.csproj`。
2. 执行 Unity 编译与 ReloadDomain。
3. 重跑 Gate DryRun。
4. 更新 asmdef 矩阵。
5. 通过后才评估 v0.1 是否升级为正式冻结候选。

## 边界

- 未写 `MODULE_AUDIT_STATE.md`。
- 未执行正式发布、上传或安装。
- 未删除旧程序集或旧资产。

## IncidentalBuildFix

- `ESEditorPresentationCore.cs` 中 `NotifyGlobalEditorSkinChanged` 调用已修正为显式 `ESEditorPresentation` 方法。
- 该修复用于恢复 Tools/Publish 默认编译路径，与 AITest 架构拆分无关，单列为 IncidentalBuildFix。

## 验收证据口径

默认启动无 AITest 所属的：

- 宿主 GameObject
- IPC Listener
- 后台 Task / Thread
- 文件监听
- 持续 Update
- 周期性日志
- 持续 GC Alloc

不写成“整个 Player 无线程、无 GC”。Profiler 证据必须归因到 AITest 的类型、Marker 或调用栈。

## 最终验收顺序

1. Unity Compile
2. ReloadDomain
3. Release 拒绝激活
4. Development / Acceptance 激活
5. Deactivate 清理与重复激活
6. Build Report 闭包
7. Domain Reload 恢复
8. Player / IL2CPP
9. 回填 Contract Evidence

上述证据完成前保持：

```text
Maturity: Verifying
Architecture Contract: v0.1 Draft
```
