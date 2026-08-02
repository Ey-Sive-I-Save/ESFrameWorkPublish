# Contextitecture 上下文系统：所有权、生命周期与类型边界 AI 协作警告

**状态：现行约束。** 本文约束 `Runtime/Context` 的当前实现；它不是 Tag、属性、Permit、资源租期的替代品，也不表示 Context 已完成跨场景、存档或网络验收。

最后核对：2026-08-02。

## 当前实现事实

- `ContextPool` 按字符串 Key 持有 `IContextitectureValue`，并保留初始化原型集合。
- 值对象由 `ESContextitectureValuePools` 的 `ESSimplePool` 租借与归还；移除值时会执行 `RemoveReceivePool(pool)` 与 `TryAutoPushedToPool()`。
- `WillSendLink` 控制该值是否向当前 Pool 的类型化 Link 通道发送变化。Float、Bool、Tag 等通道彼此独立。
- `ClearNonPersistentRuntimeValues()` 只清除 `IsPersistent == false` 的运行时值；`ClearAllRuntimeValues()` 清除全部运行时值。
- `TryAddSameContextValueFromContextValue()` 复用传入的同一对象；`TryAddNewContextValueFromContextValueCopy()` 才申请独立副本。

这些事实不构成 Lease、generation、跨宿主所有权或线程安全保证。

## 所有权与清理

1. 一个 `ContextPool` 必须有明确的唯一宿主，例如一次 Operation、一次 Skill 执行、一个明确的局部流程对象。创建者负责其完整清理。
2. 宿主结束、取消、失败、回池或销毁时，必须调用 `ClearAllRuntimeValues()`；不能只清除非持久值后把 Pool 交给下一次运行。
3. `persistent` 的唯一当前语义是“跨本 Pool 的 `ClearNonPersistentRuntimeValues()` 保留”。它**不是**跨 Entity、跨 Item、跨 Scene、跨存档、跨网络或跨 Pool 租期的持久化承诺。
4. 禁止把一个可变 Context Value 的对象引用交给另一个宿主长期保存。跨域效果必须以稳定数据重新创建本地值，或使用对应领域的 Lease/Scope/Handle。
5. 普通运行时调用一律使用 Copy 路径。`Same` 只允许框架控制的、非池化的序列化初始化原型；池化值禁止跨 `ContextPool` 共享，直至底层具备引用所有权或 generation 保护。任一 Pool 的清理当前都可能归还该对象，短期只读也不能证明共享安全。
6. 不得绕开 `ContextPool` 直接把租借值塞入字典、列表或静态字段，也不得在归还后继续读取或写入该值。

## 值类型语义

| 类型 | 正确用途 | 禁止替代 |
|---|---|---|
| Float / Int / Bool / Vector3 | 局部流程参数、一次执行期计算与信号 | 正式角色或物品属性、长期叠加数值 |
| String | 局部文本或协议值；Key 必须稳定且不在热循环拼接 | 每帧动态状态、持久身份 |
| Object / ClassT / UnityObject | 当前上下文借用对象引用 | 资源所有权、跨生命周期缓存 |
| DynamicTag | 旧的时间型上下文值，仅限 Context 内局部语义 | 正式 `ESTagCollection`、Entity Tag 或 Tag Lease |

实现存在跨类型读取/写入的便捷转换，不等于业务语义可互相冒充。一个 Key 一旦选定类型，不得用同 Key 写入另一类型来表达不同事实。

## 事件边界

- Link 是**本 Pool 内**值创建、变化、移除的通知，不是全局事件总线，也不是可靠消息队列。
- 订阅者只可读取已约定的值并做局部响应；不得在回调中持有将被清理的值引用，或写入已结束的 Entity、Item、Pool Host。
- 订阅建立者必须在同一宿主结束前解除订阅。若回调需要写入宿主，宿主关闭路径必须先阻止新回调，再清理 Context。
- 不要借 Context Link 复制第二套 Tag、Stat、Buff 或输入事件。对应系统已有自己的状态与通知契约。

## 性能与扩展

- 已存在 Key、已热身字典和值池容量下的普通读写可作为低分配局部路径使用；字符串字典查询仍不是固定数组 HotSlot，也不应声明为逐帧极限热路径。
- 首次值池租借、Dictionary 扩容、新 Key、字符串拼接、首次订阅/异常日志均可能分配，不能计入“全生命周期 0 GC”。
- 新增值类型必须实现完整的 Prepare、Reset、自动归还、类型化读写和 Link 事件语义，并补齐 Copy、移除、重复清理、异常路径测试；禁止只加一个字段类型后绕过池协议。
- Context 当前只允许 Unity 主线程使用，任何跨线程/任务回调回写都必须先回到受控主线程并确认宿主仍有效。

## 不得混用的系统

```text
局部执行参数 / 临时上下文     -> ContextPool
可组合游戏事实 / 条件查询     -> ESTagCollection + SetTag / LeaseSet
角色或物品数值 / Permit       -> Attribute Catalog + ValueChange / EffectLease
资源引用生命周期              -> Resource Scope / TemporaryLease
跨来源控制权                  -> 对应领域的 Request / Lease / Arbitration
```

## 当前验收边界

源码已有类型化值、对象池、持久标记和 Link 通道；尚无完整 Unity Test Runner 证据覆盖跨域引用、取消期回调、域重载、长时间池化和存档/网络边界。新增业务不得据此宣称这些场景已经验收。
