# 交互运行时：Interactable 占用、生命周期与结束原因 AI 协作警告

**状态：现行约束，完整运行时验收未完成。** `ESInteractable`、`EntityBasicInteractionModule` 和 `ESTagApplyZone` 已有源码主链，但它们不是同一个所有权系统。

最后核对：2026-08-02。

## 交互主链

```text
Input 的 Interact 意图
-> EntityBasicInteractionModule 候选探测与 Check
-> ESInteractable.TryAcquireInteraction(entity)
-> State / MatchTarget / IK / SupportFlag 建立
-> OnInteractStarted / OnInteractUpdate
-> EndInteraction(reason)
-> OnInteractEnded
-> 关闭 IK、MatchTarget、State、SupportFlag
-> ReleaseInteraction(entity)
```

输入只产生“请求交互”的意图。距离、朝向、Tag 条件、Permit、状态可用性与占用判定必须在交互目标和 Entity 交互模块完成，不能塞回 Input Module。

## 占用规则

- `ESInteractable` 当前只保存一个 `_interactionOwner`。同一 Entity 可重入，其他 Entity 的请求返回 `Occupied`。
- `TryAcquireInteraction()` 成功后才可以建立 State、IK、MatchTarget 和交互期 SupportFlag；任何 Begin 失败路径都必须释放已取得的占用。
- `ReleaseInteraction(entity)` 只允许当前 Owner 释放，禁止任何旁观者直接清空目标占用。
- 当前实现是 first-acquire-wins，不支持排队、优先级抢占、超时仲裁、网络并发或 generation-safe lease。不要把单个 Entity 引用误写为通用多人占用协议。
- 多人竞争、排队或抢占需求必须新建明确的请求仲裁和 lease/generation 契约；禁止扩张 `_interactionOwner` 周边临时 bool/list 来拼凑。

## 生命周期与结束原因

结束必须使用明确 `ESInteractionEndReason`，至少区分：`Completed`、`UserCancelled`、`MovementCancelled`、`Timeout`、`TargetLost`、`StateExited`、`ModuleDisabled`、`BeginRejected`。

清理顺序必须保证一个结束出口收口全部资源：

```text
OnInteractEnded
-> IK / MatchTarget 停止
-> 退出或撤销交互 State
-> 恢复交互期 SupportFlag
-> ReleaseInteraction
```

目标禁用、Entity 模块禁用、销毁、池化、状态意外退出和探测目标丢失都必须进入相同的结束收口，不能只改 UI 提示或只清 `_interactionOwner`。

## 回调与异常边界

- `OnInteractStarted`、`OnInteractUpdate`、`OnInteractCompleted`、`OnInteractEnded` 是派生业务回调；它们不拥有占用、State、IK 或 SupportFlag 的最终清理权。
- 当前回调没有框架级 `try/finally` 异常隔离。回调抛异常可中断后续清理，因此该路径尚未通过完整验收；新增回调不能宣称“异常安全”。
- 不得在回调内直接把其他 Entity 设为 Owner，或在结束回调后再次恢复该次已结束的交互资源。

## Tag Zone 不是交互占用

`ESTagApplyZone` 用 `Dictionary<Entity, Occupant>`、每 Entity 的 `ESTagLeaseSet` 和 collider 计数管理“进入区域即写 Tag”。它在 Disable 时清理 Lease，但不代表正在交互、不会参与 `_interactionOwner`、也不能拿来实现排队或独占。

## 性能与验收

- 候选/占用字典和 Tag Zone 可复用其容器；首次扩容、`GetComponentInParent` 缓存未命中、State 注入、日志与异常不属于 0 GC 稳态。
- 必须补 PlayMode 覆盖：双 Entity 竞争、Begin/Update/End 抛异常、目标 Disable、State 建立失败、TargetLost、模块池化/销毁释放、同 Entity 多 Collider 进出 Tag Zone。
- 在这些证据补齐前，当前系统只能表述为“交互生命周期主链已接入，异常清理与多人竞争尚未验收”。
