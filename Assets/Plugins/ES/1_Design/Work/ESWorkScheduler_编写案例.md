# ESWorkScheduler 编写案例

`ESWorkScheduler<TTask>` 是 ES 的轻量工作调度器。它只负责固定表排序、每轮工作额度、更新期安全注册/移除，不规定任务接口，也不规定任务怎么执行。

它适合热路径里的“顺序执行 + 可抢占 + 可提前终止”，比如 KCC 运动、IK 顺序、Buff 影响、相机影响、状态外部控制。

## 初始化容量

固定模块数量明确时，容量就按固定数量预热。

```csharp
private ESWorkScheduler<IMyWork> scheduler;

public void Init()
{
    scheduler = new ESWorkScheduler<IMyWork>();

    // 4 个固定任务：地面 / 攀爬 / 飞行 / 骑乘，或者任意 4 个固定模块。
    // pendingCapacity 只给运行中临时注册留位置，不需要太大。
    scheduler.Warmup(capacity: 4, pendingCapacity: 2);

    scheduler.Register(groundWork, order: 100);
    scheduler.Register(climbWork, order: 200);
    scheduler.Register(flyWork, order: 300);
    scheduler.Register(mountWork, order: 400);
}
```

运行中超过 `Warmup()` 容量会扩容，扩容会产生 GC。商业热路径必须初始化时把固定容量准备好。

## 基本调度

```csharp
public void Tick(float deltaTime)
{
    if (!scheduler.Reset(value: 100))
        return;

    for (int i = 0; i < scheduler.Count && scheduler.ShouldContinue(); i++)
    {
        if (scheduler.Get(i).Run(owner, deltaTime))
        {
            scheduler.self = 0;
            scheduler.world = 0;
            scheduler.other = 0;
            break;
        }
    }
}
```

`Reset()` 会做三件事：

- 应用上一轮延迟注册/移除。
- 重置 `self / world / other`。
- 进入本轮更新期。

## Entity 运动案例：带代价

```csharp
public interface IEntityVelocityWork
{
    // 返回 true 表示完全接管速度，后续速度任务不再执行。
    bool UpdateVelocity(Entity owner, EntityKCCData kcc, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime);
}

public sealed class FlyModule : IEntityVelocityWork
{
    public bool UpdateVelocity(Entity owner, EntityKCCData kcc, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime)
    {
        if (!IsFlying(owner))
            return false;

        // 飞行属于自身主动控制，消耗 self。
        // self 不足时直接退出，让后续模块或默认运动继续处理。
        if (kcc.workSelf < 100)
            return false;

        kcc.workSelf -= 100;
        kcc.workWorld -= kcc.workWorld < 40 ? kcc.workWorld : 40; // 飞行仍可能保留部分世界影响。

        currentVelocity = CalculateFlyVelocity(owner, kcc, deltaTime);
        return true;
    }
}

public sealed class ExternalForceModule : IEntityVelocityWork
{
    public bool UpdateVelocity(Entity owner, EntityKCCData kcc, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime)
    {
        if (!HasExternalForce(owner))
            return false;

        int used = kcc.workOther < 30 ? kcc.workOther : 30;
        if (used <= 0)
            return false;

        kcc.workOther -= used;
        currentVelocity += GetExternalForce(owner) * (used / 100f);
        return false;
    }
}
```

上面是伪代码。真实 Entity 里如果调度器字段是 private，不要为了案例直接暴露字段，可以用内部方法或当前模块已有的 KCC 数据访问方式完成扣减。

## 三类代价建议

```csharp
self  // 自身主动意图：玩家输入、AI 意图、状态主动接管、骑乘控制。
world // 世界规则：重力、地面、斜坡、平台、碰撞、落地切换。
other // 外部影响：技能、Buff、击退、牵引、载具、脚本外力。
```

代价不是方向占比，也不是物理力大小。它表示“本轮某类控制权还剩多少”。任务可以消耗一部分，也可以一次消耗到 0。

## 动态注册和移除

```csharp
private ESWorkHandle handle;

public void AddRuntimeWork(IMyWork work)
{
    handle = scheduler.Register(work, order: 250);
    // 如果当前正在遍历，本次注册会延迟到下一次 Reset() 前生效。
}

public void RemoveRuntimeWork()
{
    scheduler.Unregister(handle);
    handle = ESWorkHandle.Invalid;
    // pending 任务会直接移除。
    // active 任务会标记死亡，下一次 Reset() 前压缩。
}
```

## 热路径规则

- 固定任务表用 `Get(i)`，最快。
- 允许遍历中移除活跃任务时，用 `TryGetAlive(i, out task)`。
- 不要在高频 Update 中反复注册/移除固定模块。
- 不要在热路径里使用 LINQ、反射、字符串拼接、临时 List。
- 核心依赖初始化时准备好，热路径默认存在，不靠大量判空掩盖初始化错误。
