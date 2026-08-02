# Skill Operation 生命周期规范

状态：现行规范。

最后验证：2026-08-01，`ES_Logic` 0 warning / 0 error；Unity Test Runner、Player/IL2CPP 待实跑。

适用源码入口：

```text
Assets/Scripts/ESLogic/Runtime/Operation/Operations/ESOutputOp.cs
Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence/Tracks/SkillTrackItem_Operation.cs
Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence/Runtime/EntityState_Skill.cs
```

## 默认模型

绝大多数 Operation 是一次性命令。`ESOutputOp.NeedsStop` 默认返回 `false`，Skill 在 Clip Enter 执行 Start，但退出时不调用该 Op 的 Stop。

Clip Exit 本身不能省略，因为它还负责目标包写回、`ESRuntimeTargetPack` 归还、运行状态回池和异常补偿。优化边界是跳过无意义的 `StopOperation`，不是跳过 Skill Clip 生命周期清理。

## 需要 Stop 的 Operation

只有 Start 后持续持有资源或状态的 Op 才重写：

```csharp
public override bool NeedsStop => true;
```

当前典型类型包括循环音频和持续粒子。Stop 必须只释放本次执行获得的资源；Voice、Lease、实例等运行时凭证应存放在本次 Skill/Clip 的运行时 Support 或状态中，不得写入共享 Operation 配置对象。

`MustTriggerStop` 保留原职责：Op 在 Start 后被禁用时，仍允许执行必要清理。它只在 `NeedsStop == true` 时有意义。

## 运行路径

```text
Skill RuntimePlayer 构建
  -> 缓存 op.NeedsStop

Clip Enter
  -> 准备本次 TargetPack 与 Support
  -> StartOperation

Clip Exit / Skill 强制退出 / Enter 失败补偿
  -> NeedsStop 为 true 时调用 StopOperation
  -> 始终执行写回、TargetPack 归还与运行状态清理
```

## 接入检查

- 一次性 Op 保持默认 `NeedsStop == false`。
- 重写 `StopOperation` 时同时决定并声明 `NeedsStop`。
- 复合 Op 从子 Op 推导该标记。
- Stop 具备幂等或无有效凭证即返回的保护。
- 不在 Tick 中查询该标记，不使用闭包或临时集合组织退出任务。
- Unity Test Runner 至少覆盖无 Stop 跳过、正常 Stop、Stop 异常补偿和强制退出。

## TargetPack 所有权

每个实际 `ESRuntimeTargetPack` 租期只有一个回收 Owner，但可以有多个同步借用者：

```text
Skill 创建的 Pack -> Skill 回收
Track Copy/New 创建的 Pack -> Track 回收
Clip Copy/New 创建的 Pack -> Clip 回收
ReferenceSkill/ReferenceTrack -> 只借用，不回收
Operation -> 只借用，不回收
ESOpSupport.RentTargetPack() -> 对应 Support 回收
```

长期持有者必须同时保存 Pack 的 `Version`。归还统一通过框架内部的
`ESRuntimeTargetPack.TryReturnOwned(pack, rentedVersion)`；版本不一致表示该引用已经属于
其他租期，必须跳过。`ESRuntimeTargetPack.Pool` 与强制归还入口不对普通业务程序集公开。
为兼容通用编辑器采样器，`IPoolableAuto.TryAutoPushedToPool()` 保留为显式接口实现；普通
Operation 和表达式从具体 Pack API 上看不到归还入口。它不是 TargetPack 所有权转移 API。

`ESOpSupport` 只跟踪自己通过 `RentTargetPack()` 创建的 Pack，不接受外部 Pack 的所有权转移。
裸 `TargetPack` UserData 一律视为借用。异步逻辑不得跨越所属 Skill/Track/Clip 生命周期保存
裸 Pack；需要跨生命周期时复制所需值或建立独立、明确的运行时快照。

上述路径使用值类型版本记录和复用 List，热身后不会产生逐 Pack Lease 对象或稳态 GC。

## 已归档旧 Buffer 体系

生产目录不再保留 `OutputOperationBuffer`、Buffer Float 空壳、`IOpStoreKeyGroup` 或 `ESOpSupport.storeForBuffer`。静态扫描未发现具体生产派生类、调用者和序列化资产引用；历史源码只保留在 `Assets/Plugins/ES/Obsolete/Operation_OldSystem`，不得重新接回生产运行时。
