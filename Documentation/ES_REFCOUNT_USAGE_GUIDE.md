# ES引用计数使用指南

## 🎯 核心原则

### **黄金规则**
> **谁创建，谁释放；谁持有，谁负责**

```csharp
// ✅ 正确示例
var loader = ESResMaster.GetLoader();
loader.AddAsset2LoadByPathSourcer("Prefabs/Hero");
loader.LoadAllAsync(() => {
    // 使用资源...
});
// 使用完毕后释放
loader.ReleaseAll();

// ❌ 错误示例
var loader = ESResMaster.GetLoader();
loader.AddAsset2LoadByPathSourcer("Prefabs/Hero");
loader.LoadAllAsync(() => {
    // 使用资源但忘记释放 - 内存泄漏！
});
```

---

## 📊 引用计数机制详解

### 1. 两层架构

#### **全局层（ESResTable）**
```csharp
// 唯一真实的引用计数源
ESResMaster.ResTable.AcquireAssetRes(key);  // +1
ESResMaster.ResTable.ReleaseAssetRes(key, unloadWhenZero: true);  // -1
```

**特点**：
- ✅ 线程安全（有锁保护）
- ✅ 全局唯一，防止重复计数
- ✅ 自动同步到Source层

#### **Source层（ESResSource）**
```csharp
// 镜像计数，仅用于查询
int refCount = resSource.ReferenceCount;  // 只读
```

**特点**：
- ✅ 快速查询，无锁开销
- ✅ 自动同步，不需手动维护
- ❌ 不能直接修改

### 2. 生命周期

```
┌─────────────────────────────────────────────────────────┐
│                    资源生命周期                            │
└─────────────────────────────────────────────────────────┘

1. [创建] Loader.Add2LoadByKey()
   ↓
   ESResTable.TryRegister()  → RefCount = 0
   
2. [引用] Loader.LoadAllAsync()
   ↓
   ESResTable.Acquire()      → RefCount = 1
   
3. [使用] 资源处于Ready状态
   ↓
   RefCount >= 1, 资源常驻内存
   
4. [释放] Loader.ReleaseAll()
   ↓
   ESResTable.Release()      → RefCount = 0
   
5. [卸载] 当RefCount=0且unloadWhenZero=true
   ↓
   res.ReleaseTheResSource() → 卸载AB/Asset
   res.TryAutoPushedToPool() → 回收到对象池
```

---

## 🔧 常见使用场景

### 场景1：UI界面加载资源

```csharp
public class UIWindow : MonoBehaviour
{
    private ESResLoader _loader;
    private GameObject _prefabInstance;
    
    void OnEnable()
    {
        // 创建专用Loader
        _loader = ESResMaster.GetLoader();
        
        // 添加资源
        _loader.AddAsset2LoadByPathSourcer("UI/MainWindow");
        
        // 异步加载
        _loader.LoadAllAsync(() =>
        {
            // 实例化
            var prefab = _loader.LoadAssetSync<GameObject>("UI/MainWindow");
            _prefabInstance = Instantiate(prefab);
        });
    }
    
    void OnDisable()
    {
        // 销毁实例
        if (_prefabInstance != null)
        {
            Destroy(_prefabInstance);
        }
        
        // 🔥 关键：释放所有引用
        _loader?.ReleaseAll(unloadWhenZero: true);
        
        // Loader回池
        _loader?.TryAutoPushedToPool();
        _loader = null;
    }
}
```

**日志输出**：
```
[ESResTable.Acquire] UI/MainWindow | 引用计数: 0 → 1
[ESResTable.Release] UI/MainWindow | 引用计数: 1 → 0 | 卸载: 是
[ESResTable.Release] 即将卸载资源: UI/MainWindow
```

### 场景2：预加载（不增加引用计数）

```csharp
public class Preloader : MonoBehaviour
{
    void Start()
    {
        // 预加载Shader，避免运行时卡顿
        var loader = ESResMaster.GetLoader();
        
        loader.AddAsset2LoadByPathSourcer("Shaders/MyShader");
        loader.LoadAllAsync(() =>
        {
            Debug.Log("Shader预加载完成");
            
            // 🔥 关键：不释放，保持在内存中
            // loader.ReleaseAll();  // 不调用
            
            // 但Loader可以回池
            loader.TryAutoPushedToPool();
        });
    }
}
```

**效果**：
- ✅ 资源已加载到内存
- ✅ RefCount = 1，不会被卸载
- ✅ 其他地方使用时，RefCount = 2

### 场景3：共享资源

```csharp
public class CharacterManager : MonoBehaviour
{
    private ESResLoader _loader;
    
    void LoadCharacter(string charName)
    {
        if (_loader == null)
        {
            _loader = ESResMaster.GetLoader();
        }
        
        // 多次加载同一资源
        _loader.AddAsset2LoadByPathSourcer($"Characters/{charName}");
        _loader.LoadAllAsync(() =>
        {
            // 每次调用都会增加引用计数
            var prefab = _loader.LoadAssetSync<GameObject>($"Characters/{charName}");
            Instantiate(prefab);
        });
    }
    
    void UnloadAll()
    {
        // 🔥 ReleaseAll会释放所有本地引用
        // 如果资源被加载了3次，会调用Release 3次
        _loader?.ReleaseAll(unloadWhenZero: true);
    }
}
```

**引用计数变化**：
```
LoadCharacter("Hero");  // RefCount: 0→1
LoadCharacter("Hero");  // RefCount: 1→2
LoadCharacter("Hero");  // RefCount: 2→3
UnloadAll();            // RefCount: 3→0, 卸载
```

### 场景4：依赖资源自动管理

```csharp
// 加载材质时，自动加载其依赖的Shader和纹理
var loader = ESResMaster.GetLoader();
loader.AddAsset2LoadByPathSourcer("Materials/HeroSkin");
loader.LoadAllAsync(() =>
{
    var material = loader.LoadAssetSync<Material>("Materials/HeroSkin");
    // 材质的Shader AB和Texture AB也被自动加载
    // 并且引用计数已经+1
});

// 释放材质时，自动释放依赖
loader.ReleaseAll(unloadWhenZero: true);
// Shader AB和Texture AB的引用计数也会-1
```

---

## ⚠️ 常见陷阱

### 陷阱1：忘记释放

```csharp
// ❌ 错误：内存泄漏
void LoadResource()
{
    var loader = ESResMaster.GetLoader();
    loader.AddAsset2LoadByPathSourcer("Prefabs/Enemy");
    loader.LoadAllAsync(() => {
        var prefab = loader.LoadAssetSync<GameObject>("Prefabs/Enemy");
        Instantiate(prefab);
    });
    // 忘记调用 loader.ReleaseAll()
}

// ✅ 正确：及时释放
void LoadResource()
{
    var loader = ESResMaster.GetLoader();
    loader.AddAsset2LoadByPathSourcer("Prefabs/Enemy");
    loader.LoadAllAsync(() => {
        var prefab = loader.LoadAssetSync<GameObject>("Prefabs/Enemy");
        Instantiate(prefab);
        
        // 使用完立即释放
        loader.ReleaseAll();
        loader.TryAutoPushedToPool();
    });
}
```

### 陷阱2：过早释放

```csharp
// ❌ 错误：资源被提前卸载
void LoadResource()
{
    var loader = ESResMaster.GetLoader();
    loader.AddAsset2LoadByPathSourcer("Prefabs/Boss");
    loader.LoadAllAsync(() => {
        // 立即释放
        loader.ReleaseAll(unloadWhenZero: true);
        
        // 资源已经被卸载，这里会失败！
        var prefab = loader.LoadAssetSync<GameObject>("Prefabs/Boss");
    });
}

// ✅ 正确：在使用完之后再释放
void LoadResource()
{
    var loader = ESResMaster.GetLoader();
    loader.AddAsset2LoadByPathSourcer("Prefabs/Boss");
    loader.LoadAllAsync(() => {
        var prefab = loader.LoadAssetSync<GameObject>("Prefabs/Boss");
        Instantiate(prefab);
        
        // 使用完再释放
        loader.ReleaseAll(unloadWhenZero: true);
    });
}
```

### 陷阱3：跨Loader共享问题

```csharp
// ⚠️ 注意：不同Loader加载同一资源
var loader1 = ESResMaster.GetLoader();
var loader2 = ESResMaster.GetLoader();

loader1.AddAsset2LoadByPathSourcer("Shared/Config");
loader1.LoadAllAsync(() => {
    // RefCount = 1
});

loader2.AddAsset2LoadByPathSourcer("Shared/Config");
loader2.LoadAllAsync(() => {
    // RefCount = 2
});

// 🔥 关键：两个Loader都要释放
loader1.ReleaseAll();  // RefCount: 2→1
loader2.ReleaseAll();  // RefCount: 1→0, 卸载
```

---

## 🐛 调试工具

### 1. 查看引用计数

```csharp
// 方法1：通过Source查询
var res = ESResMaster.ResTable.GetAssetResByKey(key);
Debug.Log($"引用计数: {res.ReferenceCount}");

// 方法2：通过日志（自动输出）
// [ESResTable.Acquire] Hero | 引用计数: 0 → 1
// [ESResTable.Release] Hero | 引用计数: 1 → 0
```

### 2. 检查内存泄漏

```csharp
#if UNITY_EDITOR
[MenuItem("ES/检查引用计数")]
static void CheckRefCounts()
{
    var assets = ESResMaster.ResTable.SnapshotAssetEntries();
    
    Debug.Log($"===== 当前加载的资源 ({assets.Count}) =====");
    
    foreach (var pair in assets)
    {
        var res = pair.Value;
        string status = res.ReferenceCount == 0 ? "[待卸载]" : "[使用中]";
        Debug.Log($"{status} {res.ResName} | RefCount={res.ReferenceCount}");
    }
    
    // 找出可能的泄漏
    var leaks = assets.Where(p => p.Value.ReferenceCount > 10).ToList();
    if (leaks.Count > 0)
    {
        Debug.LogWarning($"===== 疑似泄漏 ({leaks.Count}) =====");
        foreach (var pair in leaks)
        {
            Debug.LogWarning($"⚠️ {pair.Value.ResName} | RefCount={pair.Value.ReferenceCount}");
        }
    }
}
#endif
```

### 3. 引用计数追踪

```csharp
// 启用详细日志
#define ES_LOG  // 在ESResSource.cs顶部

// 运行时输出所有引用计数变化
// [ESResTable.Acquire] UI/MainWindow | 引用计数: 0 → 1
// [ESResTable.Acquire] UI/MainWindow | 引用计数: 1 → 2
// [ESResTable.Release] UI/MainWindow | 引用计数: 2 → 1
// [ESResTable.Release] UI/MainWindow | 引用计数: 1 → 0 | 卸载: 是
```

---

## 🎓 最佳实践

### 1. Loader生命周期与UI一致

```csharp
public class UIBase : MonoBehaviour
{
    protected ESResLoader Loader { get; private set; }
    
    protected virtual void Awake()
    {
        Loader = ESResMaster.GetLoader();
    }
    
    protected virtual void OnDestroy()
    {
        Loader?.ReleaseAll(unloadWhenZero: true);
        Loader?.TryAutoPushedToPool();
        Loader = null;
    }
}
```

### 2. 使用using自动释放

```csharp
public class LoaderScope : IDisposable
{
    private ESResLoader _loader;
    
    public LoaderScope()
    {
        _loader = ESResMaster.GetLoader();
    }
    
    public ESResLoader Loader => _loader;
    
    public void Dispose()
    {
        _loader?.ReleaseAll(unloadWhenZero: true);
        _loader?.TryAutoPushedToPool();
    }
}

// 使用
using (var scope = new LoaderScope())
{
    scope.Loader.AddAsset2LoadByPathSourcer("Test");
    scope.Loader.LoadAllAsync(() => {
        // 使用资源...
    });
}  // 自动释放
```

### 3. 全局资源单例

```csharp
public class GlobalResources : MonoBehaviour
{
    private static GlobalResources _instance;
    private ESResLoader _loader;
    
    void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        _loader = ESResMaster.GetLoader();
        
        // 加载常驻资源
        _loader.AddAsset2LoadByPathSourcer("Global/CommonAtlas");
        _loader.AddAsset2LoadByPathSourcer("Global/Fonts");
        _loader.LoadAllAsync(() => {
            Debug.Log("全局资源加载完成");
            // 🔥 不释放，保持常驻
        });
    }
    
    void OnApplicationQuit()
    {
        // 应用退出时释放
        _loader?.ReleaseAll(unloadWhenZero: true);
    }
}
```

---

## 📈 性能优化建议

### 1. 批量加载

```csharp
// ✅ 好：批量加载
var loader = ESResMaster.GetLoader();
loader.AddAsset2LoadByPathSourcer("Hero1");
loader.AddAsset2LoadByPathSourcer("Hero2");
loader.AddAsset2LoadByPathSourcer("Hero3");
loader.LoadAllAsync();  // 一次加载

// ❌ 差：逐个加载
for (int i = 0; i < 3; i++)
{
    var loader = ESResMaster.GetLoader();
    loader.AddAsset2LoadByPathSourcer($"Hero{i}");
    loader.LoadAllAsync();  // 多次加载
}
```

### 2. 预加载高频资源

```csharp
void Start()
{
    var preloader = ESResMaster.GetLoader();
    
    // 预加载常用UI
    preloader.AddAsset2LoadByPathSourcer("UI/CommonButton");
    preloader.AddAsset2LoadByPathSourcer("UI/CommonText");
    preloader.LoadAllAsync(() => {
        Debug.Log("常用UI预加载完成");
        // 不释放，保持常驻
    });
}
```

### 3. 及时释放低频资源

```csharp
void LoadBossAssets()
{
    var loader = ESResMaster.GetLoader();
    loader.AddAsset2LoadByPathSourcer("Boss/Dragon");
    loader.LoadAllAsync(() => {
        // Boss战斗...
        
        // Boss战结束立即释放
        loader.ReleaseAll(unloadWhenZero: true);
    });
}
```

---

## 🎯 总结

| 规则 | 说明 |
|------|------|
| **创建Loader** | 谁创建谁负责释放 |
| **引用计数** | 自动管理，无需手动+1/-1 |
| **释放时机** | 资源使用完立即释放 |
| **常驻资源** | 不调用ReleaseAll，保持引用 |
| **调试工具** | 使用日志和快照检查泄漏 |
| **最佳实践** | Loader与UI生命周期一致 |

**核心思想**：让系统自动管理引用计数，开发者只需关心"何时需要"和"何时不需要"资源。
