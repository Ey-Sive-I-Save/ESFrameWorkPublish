# 测试场景导视与诊断复用

**状态：现行复用约束；Unity 编译、PlayMode 与 Profiler 验收尚未完成。**

当需求包含“测试场景提示、操作引导、验收路线、运行态状态面板、键位说明、失败定位、场景区域导视”任一项时，AI 必须首先检查并优先复用：

```text
Assets/Scripts/ESLogic/Runtime/Developer/Diagnostics/ESSceneValidationGuide.cs
Documentation/ES_SCENE_VALIDATION_GUIDE_STANDARD.md
```

它是测试场景专用的可配置部件，不是正式游戏 HUD，也不是任意业务的全局诊断服务。

## 强制路由

1. 先确认目标是否为测试/验收场景；若是，先检查该场景是否已有 `ESSceneValidationGuide`。
2. 已有 Guide 时，扩展其 `stages`、`checks` 或场景构建器的 `ConfigureForAuthoring(...)`；不得另建平行提示脚本。
3. 新测试场景需要导视时，只在测试场景根节点或其 `Diagnostics` 子节点挂一个 Guide；不得写入角色、载具、相机 Rig、技能等正式 Prefab。
4. 先用既有自动检查类型表达框架、输入、LocalControl、MainView、Mounted、VehicleReady 与驾驶权；只有场景私有断言才使用 `External`，通过**该场景 Guide 实例**的 `ReportCheck(...)` 上报。
5. 必须让每个阶段回答：去哪里、做什么、预期结果、失败优先定位，以及真实 `ESInputActionId` 对应的有效输入绑定。

## 明确禁止

- 不得为同类需求新建一次性 `OnGUI`、`GUI.Label`、硬编码键位字符串或只在 `Handles.Label` 中显示的运行时说明。
- 不得使用 `Camera.main`、`FindObjectOfType`、全局单例、原始 `Input.GetKey*` 作为 Guide 的隐式依赖。
- Guide 只读 ES 运行态；不得写角色输入、车辆输入、Cinemachine Priority / Follow / LookAt，或反向驱动测试结果。
- 人工观察项必须标为 `ManualObservation`；不得把“看起来正常”伪造成自动化通过。
- 不得因为源码存在、`.csproj` 编译或静态检查通过，就宣称 Guide 已完成 Unity、PlayMode、Profiler 或 Player 验收。

## 性能口径

- Guide 仍按 `refreshInterval`（默认 0.2 秒）轮询运行态检查。
- **只有检查结果变化、当前阶段变化或显式调用 `InvalidatePresentation()` 时才重建面板文本。**
- Landmark 标签复用；稳态每帧仅投影已配置的少量目标，标签文字和激活色只在变化时写入 UI。
- 不得据此宣称零 GC 或固定 Canvas 成本；恢复 Unity 编译后，必须在目标场景用 Unity Profiler 实测签收。

## 当前样板与验收边界

`Assets/Scenes/Tests/ESPlayerControllerTest.unity` 已接入 Guide，作为玩家移动、翻越、攀爬、骑乘、驾驶与镜头恢复的场景样板。该接入不等于功能链已完成运行验收：当前工作树的 `ES_Stand` 缺失源文件阻断全量构建，且 Unity 尚未重新生成 `ES_Logic` 工程收录新脚本。

新 AI 的报告必须准确区分：**已配置源码与场景**、**Unity 已编译**、**Test Runner 已执行**、**PlayMode 已验证**、**Profiler 已签收**。
