# 商业级对象池使用指南

## 概述

这是一个企业级的对象池实现，具有以下特性：

✅ **线程安全** - 使用lock机制保护并发访问  
✅ **容量管理** - 支持最大容量限制和自动清理  
✅ **统计监控** - 详细的性能统计和诊断信息  
✅ **错误检测** - 防止重复回收、空对象等常见错误  
✅ **生命周期回调** - onCreate、onDestroy等自定义钩子  
✅ **内存优化** - 对象追踪、预热、自动丢弃机制  

---

## 快速开始

### 1. 定义可池化对象

```csharp
public class GameData : IPoolable
{
    public int Id;
    public string Name;
    public bool IsRecycled { get; set; }
    
    public void OnResetAsPoolable()
    {
        Id = 0;
        Name = null;
    }
}
```

### 2. 创建对象池

```csharp
var pool = new ESSimplePool<GameData>(
    factoryMethod: () => new GameData(),  // 如何创建对象
    resetMethod: null,                     // 额外的重置逻辑（可选）
    initCount: 10,                        // 预热10个对象
    maxCount: 100                         // 最大容量100
);
```

### 3. 使用对象池

```csharp
// 从池中获取对象
var data = pool.GetInPool();
data.Id = 1001;
data.Name = "测试";

// 使用完毕后放回池中
pool.PushToPool(data);

// 查看统计信息
var stats = pool.GetStatistics();
Debug.Log($"池统计: {stats}");
```

---

## 单例对象池

适用于全局唯一的对象池：

```csharp
public class PlayerData : IPoolable, new()
{
    public int PlayerId;
    public bool IsRecycled { get; set; }
    
    public void OnResetAsPoolable()
    {
        PlayerId = 0;
    }
}

// 创建单例池（只需调用一次）
ESSimplePoolSingleton<PlayerData>.CreatePool(
    initCount: 20,
    maxCount: 200
);

// 在任何地方访问
var player = ESSimplePoolSingleton<PlayerData>.Instance.GetInPool();
ESSimplePoolSingleton<PlayerData>.Instance.PushToPool(player);
```

---

## 高级功能

### 生命周期回调

```csharp
var pool = new ESSimplePool<NetworkPacket>(
    factoryMethod: () => new NetworkPacket { Data = new byte[1024] },
    resetMethod: packet => packet.Length = 0,
    initCount: 50,
    maxCount: 500,
    
    // 创建时回调
    onCreate: packet => Debug.Log("创建新数据包"),
    
    // 销毁时回调
    onDestroy: packet => 
    {
        packet.Data = null;
        Debug.Log("销毁数据包");
    }
);
```

### 容量管理

```csharp
// 设置最大容量
pool.SetMaxCount(300, enableLimit: true);

// 预热对象池
pool.Prewarm(50);

// 清空对象池
pool.Clear(obj => Debug.Log($"清理对象: {obj}"));
```

### 统计信息

```csharp
var stats = pool.GetStatistics();

Debug.Log($"总创建数: {stats.TotalCreated}");
Debug.Log($"当前活跃: {stats.CurrentActive}");
Debug.Log($"池中数量: {stats.CurrentPooled}");
Debug.Log($"峰值活跃: {stats.PeakActive}");
Debug.Log($"被丢弃数: {stats.DiscardedCount}");

// 计算缓存命中率
float hitRate = (1f - (float)stats.TotalCreated / stats.TotalGets) * 100f;
Debug.Log($"缓存命中率: {hitRate:F1}%");
```

---

## 错误检测

对象池会自动检测并警告以下错误：

1. **重复回收**
```csharp
pool.PushToPool(data);
pool.PushToPool(data);  // ❌ 警告：对象已被回收
```

2. **回收null对象**
```csharp
pool.PushToPool(null);  // ❌ 警告：尝试回收null对象
```

3. **回收错误的对象**
```csharp
var otherPool = new ESSimplePool<GameData>(() => new GameData());
var otherData = otherPool.GetInPool();
pool.PushToPool(otherData);  // ❌ 警告：对象不属于此池
```

---

## 性能优化建议

### 1. 合理设置初始容量
```csharp
// 根据实际使用情况设置initCount
var pool = new ESSimplePool<BulletData>(
    factoryMethod: () => new BulletData(),
    initCount: 100,   // 预热100个，避免运行时频繁创建
    maxCount: 1000
);
```

### 2. 批量操作
```csharp
// 批量获取
var bullets = new List<BulletData>();
for (int i = 0; i < 50; i++)
{
    bullets.Add(pool.GetInPool());
}

// 批量回收
foreach (var bullet in bullets)
{
    pool.PushToPool(bullet);
}
```

### 3. 监控性能
```csharp
var stats = pool.GetStatistics();

// 如果命中率低，说明池容量不足
float hitRate = (1f - (float)stats.TotalCreated / stats.TotalGets) * 100f;
if (hitRate < 80f)
{
    Debug.LogWarning($"对象池命中率低: {hitRate:F1}%，建议增加initCount");
}

// 如果丢弃数高，说明maxCount设置过小
if (stats.DiscardedCount > 0)
{
    Debug.LogWarning($"有{stats.DiscardedCount}个对象被丢弃，建议增加maxCount");
}
```

---

## 线程安全

对象池的所有操作都是线程安全的，可以在多线程环境中使用：

```csharp
var pool = ESSimplePoolSingleton<GameData>.CreatePool(
    initCount: 100,
    maxCount: 1000
);

// 多线程并发访问
System.Threading.Tasks.Parallel.For(0, 1000, i =>
{
    var data = pool.GetInPool();
    data.Id = i;
    // ... 使用对象 ...
    pool.PushToPool(data);
});
```

---

## API 参考

### IPoolable 接口
```csharp
public interface IPoolable
{
    void OnResetAsPoolable();  // 重置对象状态
    bool IsRecycled { get; set; }  // 标记是否已回收
}
```

### Pool 主要方法
- `GetInPool()` - 从池中获取对象
- `PushToPool(obj)` - 将对象放回池中
- `Clear(onClearItem)` - 清空对象池
- `GetStatistics()` - 获取统计信息
- `Prewarm(count)` - 预热对象池
- `SetMaxCount(max, enable)` - 设置最大容量

### PoolStatistics 统计信息
- `TotalCreated` - 总创建数
- `TotalGets` - 总获取次数
- `TotalReturns` - 总回收次数
- `CurrentPooled` - 当前池中数量
- `CurrentActive` - 当前活跃数量
- `PeakActive` - 峰值活跃数量
- `DiscardedCount` - 被丢弃数量

---

## 最佳实践

1. **尽早初始化** - 在场景加载时创建并预热对象池
2. **合理设置容量** - 根据实际使用情况调整initCount和maxCount
3. **及时回收** - 对象使用完毕后立即放回池中
4. **监控统计** - 定期检查统计信息，优化池配置
5. **避免泄漏** - 确保所有获取的对象最终都会被回收
6. **正确重置** - 在OnResetAsPoolable()中彻底重置对象状态

---

## 与旧版本的兼容性

新版本完全向后兼容，旧代码无需修改即可正常工作：

```csharp
// 旧代码仍然有效
var pool = new ESSimplePool<GameData>(
    () => new GameData(),
    null,
    10
);

// 使用Pool属性访问单例（向后兼容）
var data = ESSimplePoolSingleton<GameData>.Pool.GetInPool();
```

---

## 升级内容

相比旧版本，新增以下特性：

- ✨ 线程安全支持
- ✨ 容量管理和限制
- ✨ 详细的统计监控
- ✨ 完善的错误检测
- ✨ 生命周期回调
- ✨ 对象追踪机制
- ✨ 预热功能
- ✨ 更完善的文档

---

## 常见问题

**Q: 为什么需要实现IsRecycled属性？**  
A: 用于防止重复回收，避免对象在池中被重复添加。

**Q: maxCount设置多少合适？**  
A: 根据实际使用峰值的1.5-2倍设置，可通过统计信息中的PeakActive判断。

**Q: 对象池线程安全吗？**  
A: 是的，所有操作都使用lock保护，可安全用于多线程环境。

**Q: 如何清理对象池占用的内存？**  
A: 调用Clear()方法清空池，或者对于单例池调用DestroyPool()。

**Q: 为什么有些对象被丢弃了？**  
A: 当池中对象数量达到maxCount时，新回收的对象会被丢弃，建议增加maxCount值。

---

## 技术支持

如有问题或建议，请联系技术团队。
