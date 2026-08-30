# ES 相机商业级验收矩阵

> 版本：v1；范围：全局相机策略、ViewDefinition、Rig 内容、请求仲裁、诊断回执。
> 本矩阵区分静态证据与 Unity 运行时证据；`runtime-not-run` 不等于失败，也不等于通过。

## 验收门

| 编号 | 责任角色 | 验收目标 | 必需证据 | 当前状态 |
|---|---|---|---|---|
| CAM-CFG-01 | 程序 | 全游戏只有一份输入/避障基础策略 | `ESCameraGlobalPolicy` 源码、`TryValidate` 测试 | static-passed |
| CAM-CFG-02 | 策划 | View 只表达镜头差异，不重复全局参数 | `ESCameraDefinition` Inspector、字段审计 | static-passed |
| CAM-CFG-03 | 程序 | 目录校验以 GlobalPolicy 为准，不信任隐藏旧字段 | `TryValidateRigDependencies(rigCatalog, globalPolicy)` + 回归测试 | static-passed |
| CAM-CFG-04 | 程序/策划 | 正式 SceneBinding 与编辑器 Preview 使用同一 GlobalPolicy | SceneBinding/Preview 绑定契约测试 | static-passed |
| CAM-RIG-01 | 美术 | 每个 Rig 根节点恰有一个 VCam | `ESCameraRigCatalog.TryValidateEntry`、Prefab 检查日志 | static-passed |
| CAM-RIG-02 | 美术 | 肩部偏移有 `CameraOffset`，避障有唯一 Collider | Rig 组件合同 + Prefab 证据 | warning/runtime-pending |
| CAM-ARB-01 | 程序 | Base/Shot/Modifier 使用确定性 Winner | `ESCameraDirectorTests`、Lease/Generation 断言 | static-passed |
| CAM-ARB-02 | 程序 | 旧 SceneEpoch/Lease 不能影响新 View | 过期 Lease 与注销测试 | static-passed |
| CAM-LIFE-01 | 程序 | SceneBinding 注册、注销、释放顺序可重放 | 生命周期测试、运行回执 | static-passed/runtime-pending |
| CAM-OBS-01 | 程序 | 避障层、半径、阻尼、查询预算来自全局策略 | Adapter 投影源码、策略校验测试 | static-passed |
| CAM-OBS-02 | 程序/美术 | 墙角、门洞、近距离目标不穿模且保持视线 | PlayMode 场景 + 截图/录屏 + 碰撞日志 | runtime-not-run |
| CAM-UX-01 | 策划 | 角色、载具、战斗、过场切换无跳变 | 固定输入脚本、帧级诊断回执、视觉基线 | runtime-not-run |
| CAM-UX-02 | 策划 | 16:9、21:9、4:3 与安全区构图稳定 | 分辨率矩阵截图及人工复核 | runtime-not-run |
| CAM-DIAG-01 | 程序/QA | 回执包含 Winner、请求数、Scene、平台、BuildId | `es-camera-diagnostic-receipt-v1.schema.json` 正/负例 | static-passed |
| CAM-PERF-01 | 程序/QA | LateUpdate、避障查询和 GC 在预算内 | Profiler 采样、帧时间/GC RunRecord | runtime-not-run |
| CAM-REL-01 | 发布 | 目标平台、IL2CPP、场景加载与回放一致 | 构建指纹、Player 回执、回归报告 | runtime-not-run |

## 证据规则

1. `static-passed` 只证明源码、配置、契约或确定性脚本检查通过。
2. 视觉、手感、碰撞、帧时间、Unity 序列化和发布必须提供对应运行时证据，不能由静态测试替代。
3. 每条运行时证据至少绑定分支/HEAD、Unity/Cinemachine 版本、场景、分辨率、输入脚本、资产哈希和回执路径。
4. 发现配置或来源哈希漂移时，旧验收结果只能保留为历史证据，必须重新采样。

## 当前阻塞与恢复动作

- 相机 Knowledge `aaa-camera-production-practice`：`ROUTE_SET_MISMATCH`；修复正文与索引的 `routeKeys` 后重新运行 Knowledge Validator。
- 相机 Knowledge `r2-camera-character-presentation`：`SOURCE_HASH_DRIFT`；重新计算 `Documentation/ES_CAMERA_RUNTIME_STANDARD.md` SourceRef 后再验收。
- Unity 运行时证据尚未采集；启动 Unity 前需确认目标场景、分辨率矩阵和 Profiler 预算。

## Unity 验收输入包（待采集）

| 场景 ID | 固定布置 | 输入脚本 | 关键断言 | 必留回执 |
|---|---|---|---|---|
| CAM-SCENE-01 | 角色、墙角、窄门、可碰撞遮挡体 | 跟随→快速转身→停留 10 秒 | 镜头不穿模、目标保持可见、阻尼无振荡 | `winnerDefinitionKey`、遮挡命中次数、帧时间 |
| CAM-SCENE-02 | 角色与载具同时可请求相机 | 角色请求→载具请求→释放载具 | Winner 按稳定优先级切换，旧 Lease 不回写 | `sceneEpoch`、`activeRequestCount`、Owner |
| CAM-SCENE-03 | 过场 Shot 与 Modifier 同帧竞争 | Base→Shot→Modifier→取消 Shot | 取消后恢复 Base，不残留 Modifier | 每帧诊断回执、请求序列号 |
| CAM-SCENE-04 | 16:9、21:9、4:3 与安全区 | 同一输入脚本重复播放 | FOV/构图/肩部偏移在安全区内一致 | 分辨率、场景路径、截图哈希 |
| CAM-SCENE-05 | 目标销毁、换场景、重复加载 | 目标销毁→切场景→重新注册 | 旧 ViewId/SceneEpoch/Lease 全部失效 | 失败原因、恢复动作、`sceneEpoch` |
| CAM-SCENE-06 | 代表性战斗空间与高频避障 | 固定 60 秒输入回放，预热后采样 | LateUpdate、GC、避障查询不超预算 | Profiler 原始数据、BuildId、平台 |

每个场景必须固定 Unity/Cinemachine 版本、分支/HEAD、目标平台、输入脚本版本、Prefab/配置哈希和采样窗口；截图或 Profiler 文件缺少这些绑定时，只能标为 `unproven`。

## 非声明

本矩阵不声明 Unity 编译、PlayMode、视觉质量、Profiler、Player、IL2CPP、网络或发布已通过；这些结论必须由对应证据单独支持。
