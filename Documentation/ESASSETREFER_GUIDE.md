# ESAssetRefer - 业界顶级资源引用系统

> **对标并超越 Unity Addressables AssetReference**  
> 商业级品质 | 零学习成本 | 性能卓越

---

## 📋 目录

1. [核心特性](#核心特性)
2. [快速开始](#快速开始)
3. [API 参考](#api-参考)
4. [最佳实践](#最佳实践)
5. [性能优化](#性能优化)
6. [与 Addressables 对比](#与-addressables-对比)

---

## 🎯 核心特性

### 1. **原生般的编辑器体验**
- ✅ 像普通对象字段一样拖拽使用
- ✅ 实时状态显示和调试信息
- ✅ 资源验证和快速定位
- ✅ 美观的可视化界面

### 2. **智能生命周期管理**
- ✅ 自动引用计数系统
- ✅ 完整的加载状态追踪
- ✅ 智能缓存机制
- ✅ 防止资源泄漏

### 3. **现代化 API 设计**
- ✅ async/await 支持
- ✅ 链式调用
- ✅ 类型安全
- ✅ 零学习曲线

### 4. **高性能加载系统**
- ✅ 批量加载优化
- ✅ 预加载机制
- ✅ 资源复用
- ✅ 异步优先

### 5. **完善的错误处理**
- ✅ 详细错误信息
- ✅ 加载失败追踪
- ✅ 开发友好的日志
- ✅ 运行时调试支持

---

## 🚀 快速开始

### 安装

ESAssetRefer 已集成到 ES 框架中，无需额外安装。

### 基础使用

```csharp
using ES;
using UnityEngine;

public class MyScript : MonoBehaviour
{
    // 1. 在 Inspector 中像普通字段一样使用
    public ESAssetReferPrefab myPrefab;
    public ESAssetReferSprite myIcon;
    public ESAssetReferAudioClip mySound;

    async void Start()
    {
        // 2. 异步加载（推荐）
        var prefab = await myPrefab.LoadAsyncTask();
        if (prefab != null)
        {
            Instantiate(prefab);
        }

        // 3. 回调方式
        myIcon.LoadAsync((success, sprite) =>
        {
            if (success) GetComponent<SpriteRenderer>().sprite = sprite;
        });

        // 4. 便捷方法
        mySound.Play(GetComponent<AudioSource>());
    }

    void OnDestroy()
    {
        // 5. 释放资源
        myPrefab?.Release();
        myIcon?.Release();
        mySound?.Release();
    }
}
```

### 3 秒上手

1. **声明字段**: `public ESAssetReferPrefab myAsset;`
2. **Inspector拖拽**: 直接拖拽资源到字段
3. **异步加载**: `await myAsset.LoadAsyncTask();`
4. **完成！** 🎉

---

## 📖 API 参考

### 核心类型

#### ESAssetRefer<T>
泛型资源引用基类，支持任何 UnityEngine.Object 类型。

```csharp
public class ESAssetRefer<T> where T : UnityEngine.Object
```

#### 预定义类型（开箱即用）

| 类型 | 用途 | 便捷方法 |
|------|------|----------|
| `ESAssetRefer` | 通用对象引用 | - |
| `ESAssetReferPrefab` | GameObject 预制体 | `Instantiate()`, `InstantiateAsync()` |
| `ESAssetReferSprite` | 2D 精灵图 | `ApplyToImage()` |
| `ESAssetReferAudioClip` | 音频片段 | `Play()` |
| `ESAssetReferTexture2D` | 2D 贴图 | - |
| `ESAssetReferTexture` | 贴图基类 | - |
| `ESAssetReferMaterial` | 材质球 | - |
| `ESAssetReferScriptableObject` | 配置数据 | - |
| `ESAssetReferTextAsset` | 文本资源 | - |
| `ESAssetReferAnimationClip` | 动画片段 | - |
| `ESAssetReferAnimatorController` | 动画控制器 | - |

### 主要方法

#### 加载方法

```csharp
// 异步加载 - 回调版本（推荐）
void LoadAsync(Action<bool, T> onComplete, bool addReference = true)

// 异步加载 - Task 版本（现代化）
Task<T> LoadAsyncTask(bool addReference = true)

// 同步加载（仅必要时使用）
bool LoadSync(out T asset, bool addReference = true)

// 预加载（不增加引用计数）
void Preload(Action<bool> onComplete = null)

// 使用自定义 Loader（批量加载）
void LoadWithLoader(ESResLoader loader, Action<bool, T> onComplete, bool addReference = true)
```

#### 资源管理

```csharp
// 获取已缓存的资源（类型安全）
T GetAsset()

// 释放资源引用（减少引用计数）
void Release()

// 强制释放（忽略引用计数）
void ForceRelease()

// 验证资源是否存在（编辑器）
bool Validate()
```

#### 属性

```csharp
string GUID { get; }                    // 资源 GUID
Type AssetType { get; }                 // 资源类型
ESAssetLoadState LoadState { get; }     // 加载状态
UnityEngine.Object Asset { get; }       // 缓存的资源
bool IsValid { get; }                   // 是否有效
int ReferenceCount { get; }             // 引用计数
Exception LastException { get; }        // 最后的错误
```

#### 加载状态

```csharp
public enum ESAssetLoadState
{
    None,       // 未加载
    Loading,    // 加载中
    Loaded,     // 已加载
    Failed,     // 加载失败
    Released    // 已释放
}
```

---

## 🎯 最佳实践

### 1. 优先使用异步加载

```csharp
// ✅ 好 - 使用 async/await
async void LoadCharacter()
{
    var prefab = await characterPrefab.LoadAsyncTask();
    if (prefab != null)
    {
        Instantiate(prefab);
    }
}

// ✅ 好 - 使用回调
void LoadWeapon()
{
    weaponPrefab.LoadAsync((success, prefab) =>
    {
        if (success) EquipWeapon(prefab);
    });
}

// ⚠️ 避免 - 同步加载会卡顿
void LoadItem()
{
    if (itemPrefab.LoadSync(out var prefab))
    {
        Instantiate(prefab);  // 可能造成卡顿
    }
}
```

### 2. 及时释放资源

```csharp
public class PlayerController : MonoBehaviour
{
    public ESAssetReferPrefab weaponPrefab;
    private GameObject currentWeapon;

    void EquipWeapon()
    {
        weaponPrefab.LoadAsync((success, prefab) =>
        {
            currentWeapon = Instantiate(prefab);
            // 资源引用计数 +1
        });
    }

    void OnDestroy()
    {
        // 释放资源引用
        weaponPrefab?.Release();  // 引用计数 -1
        
        if (currentWeapon != null)
        {
            Destroy(currentWeapon);
        }
    }
}
```

### 3. 使用批量加载

```csharp
// ✅ 好 - 批量加载，显示进度
void LoadLevel()
{
    var loader = new ESAssetReferBatchLoader();
    
    loader.AddRange(enemies);
    loader.AddRange(items);
    loader.AddRange(effects);
    
    loader.LoadAllAsync(
        onProgress: (p) => UpdateLoadingBar(p),
        onComplete: (s) => StartGame()
    );
}

// ❌ 差 - 逐个加载，无进度
void LoadLevelBad()
{
    foreach (var enemy in enemies)
    {
        enemy.LoadAsync((s, e) => { /* ... */ });
    }
}
```

### 4. 预加载关键资源

```csharp
public class GameManager : MonoBehaviour
{
    [Header("关键资源")]
    public ESAssetReferPrefab playerPrefab;
    public ESAssetReferSprite uiAtlas;
    public List<ESAssetReferAudioClip> bgmList;

    void Start()
    {
        // 在加载界面预加载
        PreloadCriticalAssets(() =>
        {
            StartGame();
        });
    }

    void PreloadCriticalAssets(Action onComplete)
    {
        var loader = new ESAssetReferBatchLoader();
        
        loader.Add(playerPrefab);
        loader.Add(uiAtlas);
        loader.AddRange(bgmList);
        
        // 预加载不增加引用计数
        loader.LoadAllAsync(onComplete: (s) => onComplete?.Invoke());
    }
}
```

### 5. 利用引用计数

```csharp
public class ResourcePool
{
    private ESAssetReferPrefab bulletPrefab;
    private Queue<GameObject> pool = new Queue<GameObject>();

    // 初始化时加载一次
    public async void Initialize()
    {
        var prefab = await bulletPrefab.LoadAsyncTask(addReference: true);
        
        // 创建对象池
        for (int i = 0; i < 10; i++)
        {
            var bullet = Instantiate(prefab);
            pool.Enqueue(bullet);
        }
        // 即使后续多次获取，引用计数只增加一次
    }

    // 清理时释放一次
    public void Cleanup()
    {
        bulletPrefab?.Release();  // 引用计数 -1
        
        foreach (var bullet in pool)
        {
            Destroy(bullet);
        }
        pool.Clear();
    }
}
```

### 6. ScriptableObject 配置

```csharp
[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("敌人资源")]
    public ESAssetReferPrefab prefab;
    public ESAssetReferSprite icon;
    public ESAssetReferAudioClip spawnSound;
    
    [Header("属性")]
    public int health = 100;
    public float speed = 5f;

    // 加载所有资源
    public void LoadAllAssets(Action onComplete)
    {
        var loader = new ESAssetReferBatchLoader();
        loader.Add(prefab).Add(icon).Add(spawnSound);
        loader.LoadAllAsync(onComplete: (s) => onComplete?.Invoke());
    }

    // 释放资源
    public void UnloadAssets()
    {
        prefab?.Release();
        icon?.Release();
        spawnSound?.Release();
    }
}
```

---

## ⚡ 性能优化

### 1. 缓存机制

ESAssetRefer 会自动缓存已加载的资源：

```csharp
// 第一次加载会从磁盘读取
await myPrefab.LoadAsyncTask();  // ~100ms

// 后续访问直接从缓存获取
await myPrefab.LoadAsyncTask();  // ~0ms（瞬间）

// 或直接访问缓存
if (myPrefab.LoadState == ESAssetLoadState.Loaded)
{
    var cached = myPrefab.GetAsset();  // 立即获取
}
```

### 2. 批量加载优化

```csharp
// ✅ 好 - 批量加载，共享 Loader
var loader = new ESResLoader();
foreach (var asset in assets)
{
    asset.LoadWithLoader(loader, (s, a) => { /* ... */ });
}
loader.LoadAllAsync();  // 一次性加载

// ❌ 差 - 每个资源创建新 Loader
foreach (var asset in assets)
{
    asset.LoadAsync((s, a) => { /* ... */ });  // 每次都创建新 Loader
}
```

### 3. 预加载策略

```csharp
public class LevelManager : MonoBehaviour
{
    // 在进入关卡前预加载
    async void PrepareLevel()
    {
        ShowLoadingScreen();
        
        // 预加载所有资源但不增加引用计数
        levelData.bossPrefab.Preload();
        levelData.enemyPrefabs.ForEach(e => e.Preload());
        levelData.items.ForEach(i => i.Preload());
        
        await Task.Delay(100);  // 等待预加载完成
        
        HideLoadingScreen();
        StartLevel();  // 所有资源都在缓存中，瞬间可用
    }
}
```

### 4. 引用计数管理

```csharp
public class UIManager : MonoBehaviour
{
    public ESAssetReferSprite commonIcon;
    
    void ShowMultiplePanels()
    {
        // 多个面板使用同一资源
        commonIcon.LoadAsync((s, icon) => 
        {
            panel1.icon = icon;
        }, addReference: true);  // 引用计数 +1
        
        commonIcon.LoadAsync((s, icon) => 
        {
            panel2.icon = icon;
        }, addReference: true);  // 引用计数 +2
        
        commonIcon.LoadAsync((s, icon) => 
        {
            panel3.icon = icon;
        }, addReference: true);  // 引用计数 +3
    }
    
    void OnDestroy()
    {
        // 释放3次才会真正清理缓存
        commonIcon?.Release();  // -1
        commonIcon?.Release();  // -2
        commonIcon?.Release();  // -3, 缓存清理
    }
}
```

---

## 🆚 与 Addressables 对比

### API 对比

| 功能 | ESAssetRefer | Addressables |
|------|-----------|--------------|
| **声明字段** | `ESAssetReferPrefab myAsset` | `AssetReference myAsset` |
| **异步加载** | `await myAsset.LoadAsyncTask()` | `await myAsset.LoadAssetAsync<GameObject>()` |
| **释放资源** | `myAsset.Release()` | `Addressables.Release(handle)` |
| **引用计数** | ✅ 自动管理 | ⚠️ 需手动管理 Handle |
| **类型安全** | ✅ 编译时检查 | ⚠️ 需运行时转换 |
| **批量加载** | `ESAssetReferBatchLoader` | 需自行实现 |
| **预加载** | `Preload()` 一行搞定 | 需手动管理 |
| **状态追踪** | ✅ 完整状态枚举 | ⚠️ AsyncOperationStatus |
| **编辑器体验** | ✅ 原生对象字段 | ⚠️ 需特殊绘制器 |
| **便捷方法** | `Instantiate()`, `Play()` 等 | 无 |
| **错误处理** | ✅ 详细错误信息 | ⚠️ 需检查 Status |

### 代码对比

#### 基础加载

```csharp
// ESAssetRefer - 简洁优雅 ✨
await myPrefab.LoadAsyncTask();

// Addressables - 较繁琐 😕
var handle = myAssetRef.LoadAssetAsync<GameObject>();
await handle.Task;
```

#### 回调方式

```csharp
// ESAssetRefer - 直观明了 ✨
myPrefab.LoadAsync((success, prefab) =>
{
    if (success) Instantiate(prefab);
});

// Addressables - 复杂 😕
var handle = myAssetRef.LoadAssetAsync<GameObject>();
handle.Completed += (op) =>
{
    if (op.Status == AsyncOperationStatus.Succeeded)
    {
        var prefab = op.Result;
        Instantiate(prefab);
    }
};
```

#### 资源释放

```csharp
// ESAssetRefer - 智能引用计数 ✨
myPrefab.Release();  // 自动管理，不会过早释放

// Addressables - 必须保存 Handle 😕
Addressables.Release(handle);  // 丢失 Handle 就无法释放
```

#### 批量加载

```csharp
// ESAssetRefer - 开箱即用 ✨
var loader = new ESAssetReferBatchLoader();
loader.AddRange(assets);
loader.LoadAllAsync(
    onProgress: (p) => Debug.Log($"{p:P0}"),
    onComplete: (s) => OnComplete()
);

// Addressables - 需自行实现 😕
var handles = new List<AsyncOperationHandle>();
foreach (var asset in assets)
{
    handles.Add(asset.LoadAssetAsync<GameObject>());
}
// 需自己计算进度...
```

### 优势总结

#### ESAssetRefer 的独特优势

1. **零学习成本** - 像使用普通对象一样
2. **自动引用计数** - 防止资源泄漏
3. **完整类型支持** - 编译时类型安全
4. **优雅的 API** - 现代化设计
5. **强大的编辑器** - 实时调试信息
6. **性能优化** - 智能缓存机制
7. **开箱即用** - 丰富的便捷方法
8. **深度集成** - 与 ES 系统无缝配合

#### 适用场景

✅ **推荐使用 ESAssetRefer**:
- 使用 ES 资源系统的项目
- 需要简单易用的资源引用
- 追求开发效率
- 需要强类型安全
- 小中型项目

⚠️ **考虑使用 Addressables**:
- 需要远程资源更新
- 大型项目的复杂资源管理
- 需要 Unity 官方长期支持
- 跨项目资源共享

---

## 🔧 故障排除

### 常见问题

#### Q: 资源加载失败怎么办？

```csharp
myPrefab.LoadAsync((success, prefab) =>
{
    if (!success)
    {
        // 1. 检查 GUID 是否有效
        Debug.Log($"GUID: {myPrefab.GUID}");
        
        // 2. 查看错误信息
        if (myPrefab.LastException != null)
        {
            Debug.LogError(myPrefab.LastException.Message);
        }
        
        // 3. 验证资源
        bool valid = myPrefab.Validate();
        Debug.Log($"资源是否存在: {valid}");
    }
});
```

#### Q: 如何调试资源加载状态？

在编辑器中点击资源引用字段旁边的 **ℹ️ 信息按钮**，查看详细调试信息：
- GUID
- 加载状态
- 引用计数
- 错误信息

#### Q: 资源没有及时释放？

检查引用计数：

```csharp
Debug.Log($"当前引用计数: {myPrefab.ReferenceCount}");

// 强制释放（不推荐，除非确认没有其他地方使用）
myPrefab.ForceRelease();
```

#### Q: 如何处理加载超时？

```csharp
async void LoadWithTimeout()
{
    var loadTask = myPrefab.LoadAsyncTask();
    var timeoutTask = Task.Delay(5000);
    
    if (await Task.WhenAny(loadTask, timeoutTask) == timeoutTask)
    {
        Debug.LogError("加载超时");
    }
    else
    {
        var prefab = await loadTask;
        // 使用资源
    }
}
```

---

## 📚 更多资源

- 📖 [完整使用示例](ESAssetRefer_UsageGuide.cs)
- 🎮 [实战案例](../Examples/)
- 💬 [技术支持](#)
- 🐛 [问题反馈](#)

---

## 🎉 总结

ESAssetRefer 是一个 **商业级** 的资源引用系统，它：

- ✅ 使用简单 - 3秒上手
- ✅ 功能强大 - 对标 Addressables
- ✅ 性能卓越 - 智能优化
- ✅ 稳定可靠 - 完善的错误处理
- ✅ 开发友好 - 详细的调试信息

**立即使用 ESAssetRefer，让资源管理变得简单而优雅！** 🚀

---

*ESAssetRefer - 让资源加载成为一种享受*
