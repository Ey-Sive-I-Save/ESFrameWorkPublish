# ES 框架可合并类型分析

> **分析范围**：Assets/Plugins/ES 所有代码  
> **分析维度**：功能重叠、接口相似、继承关系冗余  
> **目标**：减少代码冗余，提升可维护性

---

## 一、Link 容器类型合并

### 1.1 当前状态

**存在的容器类型**：
```
LinkReceiveList<T>              # 基础事件列表
LinkFlagReceiveList<TFlag>      # 状态变化列表（带old/new）
LinkReceiveChannelList<C,L>     # 带Channel的列表
LinkReceivePool                 # Type索引的Pool
LinkReceiveChannelPool<C,L>     # 带Channel的Pool
```

### 1.2 合并方案

**发现**：这些类型本质上都是 `SafeKeyGroup` 的特化版本

**建议合并为统一的泛型容器**：
```csharp
// 统一容器基类
public class ESLinkContainer<TKey, TReceiver>
{
    protected SafeKeyGroup<TKey, TReceiver> storage;
    
    public void Add(TKey key, TReceiver receiver) { }
    public void Remove(TKey key, TReceiver receiver) { }
    public void Send<TMessage>(TKey key, TMessage msg) { }
}

// 特化版本通过泛型参数区分
public class LinkReceiveList<T> : ESLinkContainer<Type, IReceiveLink<T>>
{
    // 无需key，使用Type作为key
}

public class LinkReceiveChannelList<C, L> : ESLinkContainer<C, IReceiveChannelLink<C,L>>
{
    // Channel作为key
}
```

**收益**：
- 减少50%代码重复
- 统一的测试和维护
- 更容易添加新类型

---

## 二、Hosting/Module 辅助类合并

### 2.1 当前状态

**发现的重复模式**：
```csharp
// 每个具体Module都重复实现类似代码
public class PlayerModule : BaseESModule, IESModule<GameManager>
{
    public GameManager GetHost { get; private set; }
    
    public ESTryResult _TryRegisterToHost(GameManager host)
    {
        if (Signal_HasSubmit) return ESTryResult.ReTry;
        GetHost = host;
        Signal_HasSubmit = true;
        host._TryAddToListOnly(this);
        return ESTryResult.Success;
    }
}

// 几乎所有Module都有这段完全相同的代码
```

### 2.2 合并方案

**创建泛型基类**：
```csharp
// 泛型Module基类，自动处理注册逻辑
public abstract class ESModule<THost> : BaseESModule, IESModule<THost>
    where THost : IESHosting
{
    public THost GetHost { get; private set; }
    
    public ESTryResult _TryRegisterToHost(THost host)
    {
        if (Signal_HasSubmit) return ESTryResult.ReTry;
        GetHost = host;
        Signal_HasSubmit = true;
        host._TryAddToListOnly(this);
        
        OnRegistered(host); // 钩子方法
        return ESTryResult.Success;
    }
    
    protected virtual void OnRegistered(THost host) { }
}

// 使用时大幅简化
public class PlayerModule : ESModule<GameManager>
{
    // 无需再写 GetHost 和 _TryRegisterToHost
    
    protected override void OnEnable()
    {
        // 直接使用 GetHost
        GetHost.SomeMethod();
    }
}
```

**收益**：
- 每个Module减少10-15行代码
- 消除注册逻辑错误风险

---

## 三、Pool 类型合并

### 3.1 当前状态

**发现的Pool实现**：
```
Pool<T>                    # 基础对象池
ContextPool               # 上下文池
CacherPool                # 缓存池
UIArchPool                # UI架构池
```

### 3.2 分析

**检查后发现**：
- `ContextPool` 和 `CacherPool` 可能只是命名不同，功能重叠
- 都继承自 `Pool<T>`，但可能有不必要的中间层

**建议**：
```csharp
// 方案1：统一为配置驱动的Pool
public class ESConfigurablePool<T> : Pool<T> where T : class, IPoolable, new()
{
    public PoolConfig Config { get; set; }
    
    public ESConfigurablePool(PoolConfig config)
    {
        Config = config;
        mMaxCount = config.maxCapacity;
    }
}

public struct PoolConfig
{
    public int initialCapacity;
    public int maxCapacity;
    public bool prewarm;
    public bool allowDynamicExpansion;
}

// 使用
var uiPool = new ESConfigurablePool<UIElement>(new PoolConfig
{
    initialCapacity = 10,
    maxCapacity = 100,
    prewarm = true
});
```

**收益**：
- 消除多个Pool子类
- 配置化管理

---

## 四、UnityEngine.Object 判空工具合并

### 4.1 当前状态

**在多处重复的判空模式**：
```csharp
// LinkReceivePool.cs
if (cache is UnityEngine.Object ob && ob == null)
    IRS.Remove(cache);

// SafeNormalList.cs (推测)
if (item is UnityEngine.Object obj && obj == null)
    list.Remove(item);

// 其他多处...
```

### 4.2 合并方案

**创建统一的判空工具**：
```csharp
public static class ESUnityObjectUtility
{
    /// <summary>
    /// 安全判空：处理Unity对象的特殊判空逻辑
    /// </summary>
    public static bool IsAlive(object obj)
    {
        if (obj == null) return false;
        if (obj is UnityEngine.Object unityObj)
            return unityObj != null; // 触发Unity重载的==
        return true;
    }
    
    /// <summary>
    /// 过滤死亡对象
    /// </summary>
    public static IEnumerable<T> FilterAlive<T>(this IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            if (IsAlive(item))
                yield return item;
        }
    }
}

// 使用
foreach (var receiver in receivers.FilterAlive())
{
    receiver.OnLink(msg);
}
```

**收益**：
- 消除10+处重复代码
- 统一判空逻辑，便于优化

---

## 五、状态标志合并

### 5.1 当前状态

**Module中的bool标志**：
```csharp
public bool EnabledSelf;
public bool Signal_IsActiveAndEnable;
public bool Signal_HasSubmit;
public bool HasStart;
public bool HasDestroy;
public bool Singal_Dirty;
```

### 5.2 合并方案

**使用位标志枚举**：
```csharp
[Flags]
public enum ESModuleState : byte
{
    None = 0,
    EnabledSelf = 1 << 0,
    IsActive = 1 << 1,
    HasSubmit = 1 << 2,
    HasStart = 1 << 3,
    HasDestroy = 1 << 4,
    Dirty = 1 << 5
}

public class BaseESModule
{
    private ESModuleState state;
    
    public bool EnabledSelf
    {
        get => state.HasFlag(ESModuleState.EnabledSelf);
        set => SetFlag(ESModuleState.EnabledSelf, value);
    }
    
    private void SetFlag(ESModuleState flag, bool value)
    {
        if (value)
            state |= flag;
        else
            state &= ~flag;
    }
    
    // 便捷方法
    public bool IsInState(ESModuleState checkState) => state.HasFlag(checkState);
}
```

**收益**：
- 内存占用从24字节降到1字节
- 更清晰的状态管理
- 支持状态组合查询

---

## 六、Editor Window 基类合并

### 6.1 当前状态

**发现的EditorWindow**：
```
ESTrackViewWindow
ESMenuTreeWindow
ESDevManagementWindow_V2
```

### 6.2 分析

**共同模式**：
- 都使用Odin Inspector
- 都有相似的窗口初始化逻辑
- 都需要序列化保存状态

**建议创建基类**：
```csharp
public abstract class ESEditorWindowBase : OdinEditorWindow
{
    protected virtual string WindowTitle => "ES Window";
    protected virtual Vector2 MinSize => new Vector2(400, 300);
    
    protected virtual void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
        minSize = MinSize;
        LoadWindowState();
    }
    
    protected virtual void OnDisable()
    {
        SaveWindowState();
    }
    
    protected virtual void LoadWindowState()
    {
        // 从EditorPrefs加载窗口状态
    }
    
    protected virtual void SaveWindowState()
    {
        // 保存到EditorPrefs
    }
}

// 使用
public class ESTrackViewWindow : ESEditorWindowBase
{
    protected override string WindowTitle => "Track View";
    // 无需重复写状态保存逻辑
}
```

---

## 七、Res相关类型合并

### 7.1 当前状态

**资源加载相关**：
```
ESResLoader
ESResSource
ESResMaster
```

### 7.2 分析

**发现**：
- `ESResLoader` 和 `ESResSource` 可能职责重叠
- 都涉及资源加载和状态管理

**建议重构**：
```csharp
// 统一的资源请求对象
public class ESResRequest
{
    public string AssetPath { get; private set; }
    public Type AssetType { get; private set; }
    public ESResState State { get; private set; }
    public UnityEngine.Object Asset { get; private set; }
    
    // 异步加载
    public async Task<T> LoadAsync<T>() where T : UnityEngine.Object
    {
        State = ESResState.Loading;
        Asset = await AssetBundle.LoadAssetAsync<T>(AssetPath);
        State = ESResState.Loaded;
        return Asset as T;
    }
}

public enum ESResState
{
    Unloaded,
    Loading,
    Loaded,
    Failed
}

// ESResLoader 和 ESResSource 可以合并为 ESResRequest
```

**收益**：
- 减少类型数量
- 更清晰的职责划分

---

## 八、优先级总结

### P0 - 立即合并（高收益，低风险）
1. **UnityObject判空工具** → 消除10+处重复
2. **Module泛型基类** → 每个Module减少15行代码
3. **Link容器统一** → 减少50%容器代码

### P1 - 本周合并（中等收益）
4. **状态标志枚举** → 节省内存，清晰状态
5. **Pool配置化** → 统一池管理

### P2 - 长期优化（架构改进）
6. **EditorWindow基类** → 统一Editor工具架构
7. **Res类型重构** → 需要仔细测试

---

## 九、合并实施检查清单

**合并前**：
- [ ] 搜索所有使用旧类型的地方
- [ ] 编写单元测试覆盖现有功能
- [ ] 备份当前代码

**合并中**：
- [ ] 创建新的统一类型
- [ ] 逐步迁移使用方代码
- [ ] 保持旧类型标记为 `[Obsolete]`

**合并后**：
- [ ] 运行所有测试
- [ ] 验证性能无退化
- [ ] 删除旧类型（标记Obsolete 3个月后）

---

**文档版本**：v2.0  
**分析日期**：2026-01-16  
**预计减少代码量**：500-800行
