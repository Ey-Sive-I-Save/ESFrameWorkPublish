# Buff 职责边界：被动持续机制

状态：现行 P1 设计提醒。
适用范围：Buff 定义、Buff Op、BuffLogic、技能、AI、交互、区域和装备效果。
最后更新：2026-08-01。

## 一句话规则

> Buff 是挂在对象上的、可叠加且可撤销的持续机制；它没有独立的施放入口、瞄准流程、资源消耗、冷却或主动发起权。

Buff 可以在既有生命周期内产生效果，但不能反过来成为第二套 Skill 系统。

## 职责划分

| 类型 | 负责什么 | 不负责什么 |
| --- | --- | --- |
| Skill / AI / Interaction / Area / Equipment | 决定何时发起、选择目标、支付消耗、处理冷却或交互前置条件 | 不直接持有持续效果的 Tag、数值 Token 或计时 |
| Op | 编排一次动作；可把当前 TargetPack 与来源上下文传给 Buff | 不保存持续时间、层数、Lease 或长期资源 |
| Buff | 持续时间、层数、来源隔离、组冲突、刷新、Tick、结束与自身资源归还 | 不主动寻找目标、响应输入、消耗资源或启动独立施放流程 |
| BuffLogic | 仅处理确实需要独占状态的被动机制，例如护盾吸收、受击累计、事件订阅 | 不替代普通 Tag、ValueChange、Permit、固定 Tick 或 Op 配置 |

## 合法的 Buff 行为

- `OnApply`：写入 Tag、数值或 Permit，启动一次受控 Op。
- `OnRefresh`：随层数、等级或持续时间变化更新自身效果。
- `OnTick`：按 Buff 已配置的 Tick 时钟执行周期效果。
- `OnRemove`：停止自身 Apply Op、执行受控结束效果、归还资源。
- 事件回调：在已订阅的战斗事件中累计次数、吸收伤害、消耗层数或请求自身移除。

上述都是**被施加后的被动反应**。它们不构成新的施放入口。

## 禁止的膨胀方式

- 不在 Buff 内部轮询寻找敌人并自行施加自己；目标应由 Skill、AI、Area 或已有 Targeting Op 提供。
- 不把输入、前摇、施放、蓝量/弹药消耗或冷却塞进 Buff。
- 不让 Buff 直接保存外部短生命周期 `TargetPack` 或 `ESOpSupport`；运行期只使用 Buff 自己创建的 TargetPack 快照和 Support。长期快照只能使用 `TryCopySnapshotFrom()`，不得用部分 `CopyFrom()` 冒充完整复制。
- 不为普通中毒、减速、无敌、数值加成等效果创建 BuffLogic；优先使用 Tag、ValueChange、Permit、Tick 与生命周期 Op。
- 不让 Op 自己记录 Buff 层数、剩余时间或 Lease；这些状态只属于 Active Buff。

## 典型组合

```text
技能命中
  -> Targeting Op 选中目标
  -> 应用 Buff 到主目标 Op
  -> Buff 管理中毒的层数、时间与 Tick
  -> Tick Op 在正确时机调用伤害系统
```

```text
装备穿戴
  -> 装备流程向持有者施加光环 Buff
  -> Buff 持有对应 Tag / 数值 Token
  -> 卸下装备时 Buff 被移除并归还其资源
```

## BuffLogic 的后续收口

`ESBuffLogic` 是可配置的机制规则；`ESBuffLogicRuntime` 是一个 Active Buff 独占的状态与资源容器。框架通过 `Logic.OnApply(runtime)`、`OnRefresh(runtime)`、`OnTick(runtime, deltaTime)`、`OnRemove(runtime)` 和 `OnRelease(runtime)` 调用规则；Runtime 不得在多个 Active Buff 间共享。

不得把机制决策扩大到 Runtime 工厂中，也不得把运行状态写回共享 Logic 定义。

## 性能提醒

Buff 的稳态 Tick 是高频路径。普通 Buff 不得因为这条职责规则新增分配、反射、LINQ、闭包、字符串 Key 解析或全局扫描。复杂 BuffLogic 仅在确有独占状态时启用，并遵守同一条 0 GC 热路径要求。
