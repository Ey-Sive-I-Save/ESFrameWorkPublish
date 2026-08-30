# ES 编辑器窗口休眠生命周期状态

> 这是编辑器休眠能力的实现与验证导航卡，不是 AI 协作历程、模块审计状态、持续授权或 Unity 验收凭证。后续 AI 的接续资料位于 `ES/Automation/Handoffs/2026-08-24_编辑器休眠生命周期实现_运行时验收交接.md`；源码、测试与最新可重读回执优先于本文件。

## 当前范围

- 目标：PlayMode 暂停、退出 PlayMode、域重载/编译和面板替换期间，保留用户当前的休眠矩形与休眠语义；只有用户主动唤醒、真正关闭窗口或明确窗口重建，才恢复 `awakeBounds`。
- 实现对象：`Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs`。
- 回归对象：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/ESWindowSleepLifetimeTests.cs`。
- 当前状态：`Implementing / runtime-verification-blocked`。

## 已写入的实现方向

1. 生命周期停用不再等价于用户唤醒：`OnDisable`、PlayMode、域重载、编译和面板重建路径保留休眠几何，并暂停瞬态动画。
2. Overlay 在生命周期恢复后复用并重新安排可见动画；保持 `PickingMode.Position`，由生命周期门禁消费回调，不让点击、焦点、键盘、滚轮或延迟回调偷偷唤醒窗口。
3. 显式关闭路径使用强制恢复，确保真正关闭时仍能执行 `awakeBounds` 恢复和清理。
4. 测试覆盖了生命周期停用期间隐式恢复被拒绝、显式关闭仍可强制恢复，以及 Overlay/回调门禁的源码契约。

## 已有证据

- 目标文件 UTF-8 Guard：通过（2 个文件，`hardFailure=False`，`review=False`）。
- `git diff --check`：目标文件通过；工作副本存在既有换行提示。
- Editor boundary/static replay/skill evidence：通过；static status 为 `static-passed`，runtime 为 `runtime-not-run`。
- `ES.MenuTree.Editor.Tests.csproj` 隔离静态构建：成功，0 errors；保留 1 个既有 warning。
- 当前定位：branch `main`，HEAD `a31d58c740210f79eb346415168d7ba425037564`。目标文件工作树均为修改状态；本卡不把工作树差异解释为可提交补丁，也不覆盖其他窗口改动。

## 当前阻断与未声明事项

- Unity Editor 已由现有进程占用项目锁（PID 58528），批处理 Test Runner 因项目已打开而无法取得安全启动条件。
- 因此尚未证明 Unity 域重载、真实 PlayMode 进出、窗口视觉交互、Overlay 实际绘制、Profiler、Player 或发布行为。
- 本卡不声明“编译验收通过”“运行时已修复”或“项目级整改完成”；Shader 目录中已有的其他编译问题另行处理，不混入本休眠验收。

## 后续最小闭环

1. 由用户在 Unity 中保存现场并正常关闭占锁编辑器；不要强杀进程或删除锁文件。
2. 重新启动唯一 Unity 实例后，运行 `ESWindowSleepLifetimeTests` EditMode Test Runner，记录真实 Console/Test Runner 回执。
3. 做一组 PlayMode 矩阵：休眠→进入 PlayMode→退出 PlayMode；休眠→域重载/编译；面板替换；显式唤醒；真正关闭。逐项确认矩形、休眠状态、Overlay 和回调行为。
4. 若矩阵通过，再补充可重读证据并将状态提升为可验收；若失败，按“首次失败路径”定位，禁止以静态测试替代运行时结论。

## 失效条件

目标源码、回归测试、AIWarnings、Unity 生命周期实现、branch/HEAD 或上述证据发生变化后，本卡只作为导航，必须重新验证。
