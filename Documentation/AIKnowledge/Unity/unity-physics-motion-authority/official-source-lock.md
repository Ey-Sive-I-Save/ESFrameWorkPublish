# Unity 2022.3 物理运动官方来源锁

本文件为 `unity-physics-motion-authority` 条目锁定 Unity `2022.3.45f1` 的官方来源身份、
响应哈希和最小适用语义。它不是 Unity 运行回执，也不证明任何场景、Prefab、碰撞结果或性能。

## Online documentation

以下 SHA-256 对 2026-08-24 读取到的 Unity 2022.3 官方页面 HTTP 响应正文按 UTF-8
字节计算。页面响应或 Unity 版本变化时，依赖本锁的 Knowledge 必须标记 stale 并重新核对。

| URL | HTTP | Raw UTF-8 SHA-256 | 支撑范围 |
|---|---:|---|---|
| https://docs.unity3d.com/2022.3/Documentation/Manual/TimeFrameManagement.html | 200 | `2fdad8930bb77d31b0e7b549f8c617311159c2b70fd1b2dfd2bc1b4a23a9ab46` | 固定时间步、FixedUpdate 与渲染帧不是一一对应 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/rigidbody-interpolation.html | 200 | `bff89784165b2fd8c74ce003ba3d5e41ca6b19165329c5c5b18ec5f71ef3cfcd` | Rigidbody 插值、视觉抖动与 Transform 同步边界 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rigidbody.MovePosition.html | 200 | `1e922184f4cc3c274a9bd339b15d4ab08563b1a82354b9b95c48d99e470c2cae` | Kinematic Rigidbody 的 MovePosition 提交语义 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.SyncTransforms.html | 200 | `b43f1e99e8340403a878427fc0305d11e0cba4dded2e330f0d9637b48c00564d` | 将 Transform 变化显式同步到物理世界 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.RaycastNonAlloc.html | 200 | `c909e7906c152cbb43e05c095a5f37ac5443ecb2103e04fc8a2c4f8fa62fd57a` | NonAlloc 查询向调用方缓冲写入命中 |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/QueryTriggerInteraction.html | 200 | `1ddd7a90ae5ca80d8944474e84668b5e98455940180061e254089f73b60b08e2` | 查询级 Trigger 策略覆盖全局设置 |
| https://docs.unity3d.com/2022.3/Documentation/Manual/collider-interactions-ontrigger.html | 200 | `305bf1d388fb76fc5df196b0c88bf310a792388bbfb270ee97505a264c98bcd1` | Trigger 交互矩阵与 Rigidbody/Collider 组合条件 |

## Installed API documentation

本机 Unity Hub 注册的 `2022.3.45f1` Editor API XML 已按完整文件校验：

- `UnityEngine.PhysicsModule.xml`：`8a14cbc0045fd8842ea1402298aa11c29eab1ab65677e9b2f72f851967e0a21c`
  - `Physics.autoSyncTransforms`：Transform 变化时是否自动同步物理世界。
  - `Physics.queriesHitTriggers`：Raycast、Cast 与 Overlap 等查询默认是否命中 Trigger。
  - `Physics.SyncTransforms()`：把 Transform 变化应用到物理引擎。
  - `QueryTriggerInteraction`：`Collide` 始终命中 Trigger，`Ignore` 始终忽略，
    `UseGlobal` 使用 `Physics.queriesHitTriggers`。
  - `RaycastNonAlloc`、`SphereCastNonAlloc` 与 `Overlap*NonAlloc` 将结果写入调用方缓冲。
  - `RaycastAll` 明确声明结果顺序未定义；需要最近命中时必须显式选择，不能依赖返回顺序。
  - `Rigidbody.interpolation` 用于管理运行时 Rigidbody 运动的视觉抖动。
  - `Rigidbody.MovePosition` 将 Kinematic Rigidbody 朝目标位置移动。
- `UnityEngine.CoreModule.xml`：`ce120ca131e9d371794fa1d453bdd97d8ed3d39dee97c40f8467f7cac2b1bbce`
  - `Time.fixedDeltaTime` 是物理和 `MonoBehaviour.FixedUpdate` 等固定帧更新的游戏时间间隔。

## Locked interpretation

- 固定步与渲染帧不是一一对应；业务不能假设每个 `Update` 前恰好执行一次 `FixedUpdate`。
- Rigidbody/Transform 必须先确定一个最终写入者和提交阶段。动态 Rigidbody、Kinematic
  Rigidbody、KCC 与纯 Transform 运动是不同后端，不能在同一对象上并行争写。
- 插值只解决渲染观察到的平滑性，不改变物理步本身。对启用插值的 Rigidbody 直接写
  Transform 会引入物理姿态与显示姿态同步问题；确需直接改 Transform 时必须显式评估
  `Physics.SyncTransforms()` 的时机和成本。
- Physics Query 必须显式给出 LayerMask、Trigger 策略、缓冲所有者、容量溢出处理和结果选择。
  `UseGlobal` 会受项目全局配置影响，不能当作跨项目稳定常量。
- NonAlloc 只证明调用方复用了结果缓冲；它不证明没有其他分配，也不保证缓冲容量充分。
  返回数量达到容量时应视为可能截断，并进入扩容、重试或降级策略。
- Trigger 回调的存在不证明当前 Layer 碰撞矩阵、Rigidbody/Collider 组合、启停时序或销毁退出路径
  已正确；这些仍需具体场景和 PlayMode 证据。

## Evidence boundary

本锁只证明 2026-08-24 读取到的官方页面响应、本机 API XML 身份和上述最小语义；
`runtime-not-run`。未启动 Unity，未执行 Physics Simulation、FixedUpdate、碰撞/Trigger、
PlayMode、Profiler、Player 或 IL2CPP 验证。
