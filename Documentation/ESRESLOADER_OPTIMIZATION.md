# ESResLoader LoadAllAsync 优化说明

## 🔴 原有问题

### 问题1：回调丢失
```csharp
// ❌ 旧实现 - 单个回调变量
private Action mListener_ForLoadAllOK;

public void LoadAllAsync(Action listener = null)
{
    mListener_ForLoadAllOK = listener;  // 会覆盖之前的回调！
    DoLoadAsync();
}
```

**风险**：当多个 ESResRefer 使用同一个 Loader 时：
```csharp
var loader = new ESResLoader();

// 第1个回调
enemyPrefab.LoadAsync(loader, (s, p) => Debug.Log("Enemy加载完成"));

// 第2个回调会覆盖第1个！
iconSprite.LoadAsync(loader, (s, i) => Debug.Log("Icon加载完成"));

// 结果：只有 Icon 的回调会被执行，Enemy 的回调丢失！
```

### 问题2：重复触发加载
```csharp
// ❌ 每次调用都触发 LoadAllAsync
enemyPrefab.LoadAsync(loader, callback);  // 触发一次
iconSprite.LoadAsync(loader, callback);   // 又触发一次
bgmAudio.LoadAsync(loader, callback);     // 再触发一次
// 导致重复调度，浪费性能
```

---

## ✅ 优化方案

### 方案1：回调列表管理

```csharp
// ✅ 新实现 - 回调列表
private List<Action> mListeners_ForLoadAllOK;

public void LoadAllAsync(Action listener = null)
{
    // 添加到列表，而不是覆盖
    if (listener != null)
    {
        if (mListeners_ForLoadAllOK == null)
        {
            mListeners_ForLoadAllOK = new List<Action>();
        }
        
        if (!mListeners_ForLoadAllOK.Contains(listener))
        {
            mListeners_ForLoadAllOK.Add(listener);
        }
    }
    
    DoLoadAsync();
}

// 触发所有回调
private void InvokeAllLoadCompleteCallbacks()
{
    if (mListeners_ForLoadAllOK != null && mListeners_ForLoadAllOK.Count > 0)
    {
        var callbacks = new List<Action>(mListeners_ForLoadAllOK);
        mListeners_ForLoadAllOK.Clear();
        
        foreach (var callback in callbacks)
        {
            try
            {
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"回调执行异常: {ex.Message}");
            }
        }
    }
}
```

### 方案2：ESResRefer 添加 autoStartLoading 参数

```csharp
// ✅ 支持手动控制触发时机
public void LoadAsync(ESResLoader loader, Action<bool, T> onComplete, bool autoStartLoading = true)
{
    var targetLoader = loader ?? ESResMaster.GlobalResLoader;
    
    targetLoader.AddAsset2LoadByGUIDSourcer(_guid, (success, source) =>
    {
        onComplete?.Invoke(success, source.Asset as T);
    });

    // 只有在 autoStartLoading 为 true 时才触发
    if (autoStartLoading)
    {
        targetLoader.LoadAllAsync();
    }
}
```

---

## 📝 最佳实践

### ✅ 推荐：批量加载模式

```csharp
var loader = new ESResLoader();

// 添加资源到队列，但不立即触发加载
enemyPrefab.LoadAsync(loader, (s, p) => Debug.Log("Enemy loaded"), autoStartLoading: false);
iconSprite.LoadAsync(loader, (s, i) => Debug.Log("Icon loaded"), autoStartLoading: false);
bgmAudio.LoadAsync(loader, (s, a) => Debug.Log("Audio loaded"), autoStartLoading: false);

// 统一触发一次加载
loader.LoadAllAsync(() =>
{
    Debug.Log("所有资源加载完成！");
});
```

### ✅ 也可以：多次调用 LoadAllAsync

```csharp
var loader = new ESResLoader();

// 每次都会添加回调到列表
loader.LoadAllAsync(() => Debug.Log("回调1"));
loader.LoadAllAsync(() => Debug.Log("回调2"));
loader.LoadAllAsync(() => Debug.Log("回调3"));

// 加载完成时，三个回调都会被执行
```

### ⚠️ 避免：单个资源单独 Loader

```csharp
// ❌ 不推荐：每个资源都用全局 Loader 且自动触发
enemyPrefab.LoadAsync((s, p) => {});  // 触发一次
iconSprite.LoadAsync((s, i) => {});   // 又触发一次
bgmAudio.LoadAsync((s, a) => {});     // 再触发一次
// 虽然现在回调不会丢失，但仍会多次调度
```

---

## 🎯 使用场景

### 场景1：UI面板加载

```csharp
public class UIPanel : MonoBehaviour
{
    public ESResReferSprite icon1;
    public ESResReferSprite icon2;
    public ESResReferSprite icon3;
    
    private ESResLoader loader;
    
    void Start()
    {
        loader = new ESResLoader();
        LoadAllIcons();
    }
    
    void LoadAllIcons()
    {
        icon1.LoadAsync(loader, OnIcon1Loaded, autoStartLoading: false);
        icon2.LoadAsync(loader, OnIcon2Loaded, autoStartLoading: false);
        icon3.LoadAsync(loader, OnIcon3Loaded, autoStartLoading: false);
        
        // 统一触发，所有回调都会执行
        loader.LoadAllAsync(() =>
        {
            Debug.Log("所有图标加载完成");
            ShowPanel();
        });
    }
    
    void OnIcon1Loaded(bool success, Sprite sprite) { }
    void OnIcon2Loaded(bool success, Sprite sprite) { }
    void OnIcon3Loaded(bool success, Sprite sprite) { }
}
```

### 场景2：关卡资源管理

```csharp
public class LevelManager : MonoBehaviour
{
    public List<ESResReferPrefab> enemies;
    public List<ESResReferSprite> uiElements;
    
    private ESResLoader levelLoader;
    
    void LoadLevel()
    {
        levelLoader = new ESResLoader();
        
        // 批量添加
        foreach (var enemy in enemies)
        {
            enemy.LoadAsync(levelLoader, OnEnemyLoaded, autoStartLoading: false);
        }
        
        foreach (var ui in uiElements)
        {
            ui.LoadAsync(levelLoader, OnUILoaded, autoStartLoading: false);
        }
        
        // 显示进度的加载
        levelLoader.LoadAllAsync(() =>
        {
            Debug.Log("关卡资源加载完成");
            StartLevel();
        });
    }
}
```

---

## 📊 性能对比

### 优化前
- ❌ 回调丢失风险：100%
- ❌ 重复调度次数：N次（N=资源数量）
- ❌ 回调执行：只有最后一个

### 优化后
- ✅ 回调丢失风险：0%
- ✅ 重复调度次数：1次（手动控制）
- ✅ 回调执行：所有回调都执行

---

## 🔄 迁移指南

### 方式1：最小改动

如果你的代码是这样：
```csharp
// 旧代码
enemyPrefab.LoadAsync(loader, callback);
iconSprite.LoadAsync(loader, callback);
```

可以不做任何修改，新版本向后兼容。但建议优化为：

```csharp
// 优化后
enemyPrefab.LoadAsync(loader, callback, autoStartLoading: false);
iconSprite.LoadAsync(loader, callback, autoStartLoading: false);
loader.LoadAllAsync();
```

### 方式2：推荐用法

```csharp
// 创建专用 Loader
var loader = new ESResLoader();

// 批量添加资源
AddResourcesToLoader(loader);

// 统一触发
loader.LoadAllAsync(OnAllLoaded);
```

---

## ✅ 总结

1. **回调管理**：从单个变量改为列表，支持多个回调
2. **重复加载优化**：添加 autoStartLoading 参数，允许手动控制触发时机
3. **异常安全**：回调执行时捕获异常，避免一个失败影响其他
4. **向后兼容**：默认行为保持不变（autoStartLoading=true）
5. **最佳实践**：批量加载时设置 autoStartLoading=false，最后统一触发

这次优化解决了多个 ESResRefer 同时使用同一 Loader 时的回调丢失和重复加载问题，使系统更加健壮！
