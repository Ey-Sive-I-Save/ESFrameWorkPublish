# ES Scene Validation Guide

状态：现行复用规范；Unity 编译、PlayMode 与 Profiler 验收待完成。
最后验证：2026-08-03；源码边界与空白检查通过，当前全量构建受 `ES_Stand` 缺失源文件阻断，Unity 尚未收录新脚本。
适用源码入口：`Assets/Scripts/ESLogic/Runtime/Developer/Diagnostics/ESSceneValidationGuide.cs`。

`ESSceneValidationGuide` 是测试场景专用的验收导视与诊断部件。它解决的不是游戏内 HUD，而是测试场景必须回答的闭环：**去哪里、做什么、预期是什么、当前是否成立、失败应先查哪一层**。

## 放置边界

- 仅挂在测试场景根节点或其 `Diagnostics` 子节点。
- 不挂到正式角色、载具、相机 Rig、技能 Prefab。
- 不创建全局单例，不创建 EventSystem，不使用 `Camera.main`，不直接写 Entity 输入、Vehicle 输入或 Cinemachine 状态。
- 它读取 `ESGameManager` 的输入、本地控制、相机输出和显式配置目标；自定义验收只能通过该场景 Guide 实例的 `ReportCheck` 回报。

## 一个场景的配置方式

1. 设置 `guideTitle`、`guideSubtitle`。
2. 明确绑定 `routeObserver` 与 `worldGuideCamera`。未绑定时可以只读 LocalControl 与 ES Camera MainView，但测试场景应显式绑定，避免隐藏依赖。
3. 将路线拆成 `ESSceneValidationStage`：Landmark、操作、预期、失败定位、真实 `ESInputActionId`、关联检查 ID。
4. 用 `ESSceneValidationCheck` 配置可自动判定的框架、输入、相机、本地控制、骑乘、驾驶权等检查；视觉或手感项用 `ManualObservation`，不能伪装为自动通过。
5. 对场景私有的 PlayMode 驱动器或测试，用 `ReportCheck(checkId, state, detail)` 上报 `External` 检查。

## 运行时行为

- 左侧固定面板只显示当前阶段的完整说明，避免把所有长文案压进一个不可读面板。
- 面板顶部显示全路线状态；当前阶段默认按观察者最近 Landmark 自动聚焦。
- 输入文字来自 `ESInputModule.GetRuntimeBindings()` 的有效绑定，而不是写死的键位说明。
- 场景 Landmark 会投影为运行时路线标签；未指定相机时只尝试 ES Camera MainView 输出，绝不退回 `Camera.main`。
- Inspector / Scene View 中会以 Gizmo 和编号标签呈现同一条路线。

## 性能约定

- Guide 仍按 `refreshInterval`（默认 0.2 秒）轮询运行态检查；只有检查结果变化、当前阶段变化或调用 `InvalidatePresentation()` 后重建面板文本，稳态轮询不反复拼接长字符串。
- 运行时每帧仅投影已配置的少量 Landmark；路线标签复用，标签文字和激活颜色只在变化时写入 UI。
- 不做 `FindObjectOfType`、`Camera.main` 查找、LINQ、每帧列表创建或每帧字符串格式化。
- 它是开发/验收设施而不是发布 HUD。仍应在目标平台的 Unity Profiler 中确认实际场景的 GC 与 Canvas 重建预算，不能仅据源码宣称“零 GC”。

## 验收语义

- `[通过]`：自动检查已通过。
- `[失败]`：配置缺失、模块未就绪或实际状态不符合约定；详情给出第一个排查边界。
- `[等待]`：需要发生的行为尚未发生，例如尚未上车。
- `[观察]`：有意保留给人工判断的视觉、连续性或手感结果；它不是自动化通过。

该部件是复用的测试支撑层，不替代正式的 EditMode、PlayMode、Profiler 或发布门禁。
