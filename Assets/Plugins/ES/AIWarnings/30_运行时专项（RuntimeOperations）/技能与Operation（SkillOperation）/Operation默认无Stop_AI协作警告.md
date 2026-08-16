# Operation 默认无 Stop：AI 协作警告

状态：现行强约束。

最后验证：2026-08-01，`ES_Logic` 已有历史静态构建证据；Unity Test Runner、Player/IL2CPP 待实跑。具体诊断仅以当次构建回执为准，不在本规则记录数量。

适用源码入口：

```text
Assets/Scripts/ESLogic/Runtime/Operation/Operations/ESOutputOp.cs
Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence/Tracks/SkillTrackItem_Operation.cs
```

## 核心规则

`ESOutputOp` 默认是执行一次即结束的命令：

```csharp
public virtual bool NeedsStop => false;
```

只有确实持有跨时间运行状态或外部资源、并重写了 `StopOperation(...)` 归还该所有权的 Op，才允许重写为 `true`。例如循环音频、持续粒子、临时控制权或其他成对申请/释放行为。

一次性伤害、事件、日志、OneShot 音频、单次数值写入等不得为了形式完整而声明 Stop。`SkillOperationClipRuntimePlayer` 在构建时缓存 `NeedsStop`，Clip 退出仍执行目标写回和池化清理，但不会调用无意义的 Op Stop。

## 复合 Operation

顺序、条件等包装 Op 的 `NeedsStop` 必须由其子 Op 推导。包装层不得无条件返回 `true`，也不得在存在需要清理的子 Op 时返回 `false`。

## MustTriggerStop 边界

`MustTriggerStop` 只表示：当一个已开始且需要 Stop 的 Op 后来被禁用时，仍必须完成清理。它不能把一次性 Op 转换为生命周期 Op，也不能代替 `NeedsStop`。

## 禁止事项

- 禁止在共享的 Operation 配置对象上保存本次技能的 Handle、`running` 或其他施法实例状态。
- 禁止仅因为基类存在 `StopOperation` 就在所有 Op 上增加空 Stop。
- 禁止在每帧判断 `NeedsStop`；该能力在技能运行播放器构建阶段缓存。
- 新增 `StopOperation` 实现时，必须同时声明正确的 `NeedsStop`，并覆盖 Enter 成功、Enter 失败补偿、正常 Exit 和强制 Skill Exit。
- `OutputOperationBuffer`、Buffer Float 空壳、`IOpStoreKeyGroup` 与 `ESOpSupport.storeForBuffer` 已退出生产代码，只在 `Assets/Plugins/ES/Obsolete/Operation_OldSystem` 留档；禁止重新复制或建立无消费者的常驻 Buffer 容器。
- 禁止 Operation、表达式或借用者直接回收 `ESRuntimeTargetPack`。每个租期只有创建该 Pack 的 Skill、Track、Clip 或 Support 拥有回收权。
- `ReferenceSkill`、`ReferenceTrack` 和裸 Pack UserData 永远是借用；只有 Copy/New 路径保存 `createdTarget + targetVersion` 并负责归还。
- 禁止恢复公开的 `TrackTargetPack(existingPack)` 所有权认领入口。Support 只能回收自己通过 `RentTargetPack()` 创建的 Pack。
- 长期持有 Pack 时禁止只保存裸引用或只检查 `IsRecycled`；必须同时保存租用时 `Version`，并通过内部版本门禁归还，防止旧 Owner 回收已经重新租出的实例。
- 异步任务不得跨 Skill/Track/Clip 生命周期持有裸 Pack。需要异步延续时只复制必要数据或创建独立快照。

性能结论只限静态路径：一次性 Op 的退出虚调用已被移除；具体 CPU/GC 数值仍以 Unity Profiler、Player 和 IL2CPP 实测为准。
