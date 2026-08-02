# ESCommand 运行时：Player、Runner、执行帧与服务边界 AI 协作警告

**状态：现行约束。** 本文规范运行时 `ESCommandPlayer` 链路，不是 AI Command 模板、编辑器命令或普通 `Operation` 生命周期规范。

最后核对：2026-08-02。

## 当前运行时主链

```text
ESCommandPlayer.Play(event)
-> ESCommandPlayerRunner.Register(player)
-> MODULE_ESCommandModule.Update()
-> ESCommandPlayerRunner.TickAll(Time.time, Time.deltaTime)
-> Player.Tick(frame, time, deltaTime)
-> 普通 Command.InvokeCommand() 或 IESCommandPlayable 生命周期
```

`TickAll` 当前由 `MODULE_ESCommandModule` 驱动。不得再在第二个 MonoBehaviour、Skill、UI 或业务循环中重复调用它。

## 职责边界

- `ESCommandPlayer`：单次命令事件的执行状态机，持有当前事件、索引、取消请求与当前 Playable。
- `ESCommandPlayerRunner`：全局活跃 Player 调度表；当前使用 `List + Dictionary` 和 swap-back 移除，正常移除不移动整表。
- `ESCommandServices`：仅保存输入模块与 RuntimeMode 服务的运行时注入点，不是通用 Service Locator，禁止继续塞入任意业务单例。
- `ESCommand`：一次命令定义。非 Playable 命令同步调用 `InvokeCommand()`。
- `IESCommandPlayable`：可跨帧命令，使用 `OnPlayStart -> TickPlay -> OnPlayCancel`。

## 执行帧与停止语义

1. `Play()` 重置索引、当前 Playable、取消标记和帧号；空事件进入 `Skipped`。**当前运行中再次 `Play()` 不会先调用旧 Playable 的 `OnPlayCancel()`，可能遗留外部副作用。** 现阶段调用方在重播前必须先 `Stop()`；后续运行时修复必须先补偿旧 Playable，并加入重播回归测试。
2. `tickImmediatelyOnPlay` 会在同一帧以 `deltaTime = 0` 推进到第一个等待点。延时命令不得因此扣除时间或产生重复副作用。
3. 每个 Player 以 `lastTickFrame` 防止同一帧重复 Tick。新实现不得绕过该门禁直接调用可播放命令。
4. `Cancel()` 只登记取消请求，下一次 Tick 处理；`Stop()` 立即调用当前 Playable 的取消、注销 Runner 并进入 `Canceled`。需要立即撤销外部副作用时必须使用明确 Stop/补偿路径，不能假设 Cancel 当场完成。
5. Playable 返回 `Running` 时保留当前位置；返回 `Failed` 或 `Canceled` 时终止整个 Player；其余完成状态继续下一条命令。
6. 命令的启动、持续写入、取消和结束必须幂等或具有自己的一次性状态，尤其不得让同一虚拟按键在停止后残留。

## 输入与 RuntimeMode 命令

- 虚拟输入命令只调用 `UISetButton`、`UIPulseButton`、`UIClear...`、`UISetVector2`、`UISetAxis` 等输入 API，不能直接执行角色、Buff、Skill 或交互业务。
- 持续按钮、向量、轴命令必须有对称的停止/取消清理；一次 Pulse 与持续 Held 语义不得混淆。
- 当前 RuntimeMode 命令按值 Push/Pop/Remove；`Remove` 从栈顶向下寻找相同 mode/tag，不能表达“撤销我自己的精确申请”。多来源并发的精确回收必须先补 Handle/Lease 所有权模型，禁止用“扫描相同枚举值”伪装安全。

## 异常、生命周期与验收

- 当前 Player 和 Runner 未对 `InvokeCommand`、`OnPlayStart`、`TickPlay`、`OnPlayCancel` 建立逐命令异常隔离。抛异常可能中断当帧 `TickAll`，不能写成已具备故障隔离。
- 新命令必须自行保证资源、输入和 RuntimeMode 的成对清理；不得吞掉异常后仍报告成功。
- 场景切换、模块销毁或 Subsystem 重置时必须清理 Runner 活跃表和 `ESCommandServices` 注入，不能留下静态 Player/Service 引用。
- 运行容器的稳态调度可低分配，但命令自身的分配、闭包/事件、日志、异常和首次容器扩容不自动是 0 GC。

## 必测场景

```text
空事件、立即推进、同帧重复 Tick、普通命令失败、Playable 运行/完成/取消、
Player Disable/Stop、虚拟输入取消清理、RuntimeMode 多来源、命令回调抛异常、
场景或模块重置后的 Runner/Services 清退。
```

在这些 PlayMode/Unity Test Runner 证据补齐前，ESCommand 只能按“主链已存在、异常隔离与全局清理待验收”使用。
