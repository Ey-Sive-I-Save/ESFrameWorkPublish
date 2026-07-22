# ESAssetRefer - ES资源系统便捷引用工具

> **依赖ES资源加载系统的辅助工具** | **简单易用** | **无负担**

---

## 📋 核心特点

### 1. **辅助性工具定位**
- ✅ 不创建临时Loader，使用传入Loader或全局Loader
- ✅ 不管理引用计数，由ES资源系统（ESResSource）统一管理
- ✅ 依赖ESResLoader完成资源加载
- ✅ 在Inspector中添加 `@` 符号标识便捷引用

### 2. **零学习成本**
- ✅ 像普通对象字段一样拖拽使用
- ✅ 简洁的API设计
- ✅ 支持async/await和回调两种方式

### 3. **类型安全**
- ✅ 编译时类型检查
- ✅ 预定义常用类型
- ✅ 支持自定义类型扩展

---

## 🚀 快速开始

### 基础使用

```csharp
using ES;
using UnityEngine;

public class Example : MonoBehaviour
{
    // 1. 在Inspector中拖拽资源（会显示 @ 符号）
    public ESAssetReferPrefab enemyPrefab;
    public ESAssetReferSprite icon;
    
    // 2. 可选：创建共享Loader（推荐批量加载时使用）
    private ESResLoader sharedLoader;
    
    void Start()
    {
        sharedLoader = new ESResLoader();
        LoadResources();
    }
    
    // 3. 异步加载（使用全局Loader）
    void LoadResources()
    {
        // 方式1：使用全局Loader
        enemyPrefab.LoadAsync((success, prefab) =>
        {
            if (success) Instantiate(prefab);
        });
        
        // 方式2：使用指定Loader（推荐）
        icon.LoadAsync(sharedLoader, (success, sprite) =>
        {
            if (success) GetComponent<SpriteRenderer>().sprite = sprite;
        });
        
        // 方式3：async/await
        LoadAsync();
    }
    
    async void LoadAsync()
    {
        var prefab = await enemyPrefab.LoadAsyncTask(sharedLoader);
        if (prefab != null) Instantiate(prefab);
    }
}
```

---

## 📖 API 参考

### 核心方法

```csharp
// 异步加载 - 使用指定Loader
void LoadAsync(ESResLoader loader, Action<bool, T> onComplete)

// 异步加载 - 使用全局Loader
void LoadAsync(Action<bool, T> onComplete)

// async/await方式
Task<T> LoadAsyncTask(ESResLoader loader = null)

// 同步加载（仅必要时使用）
bool LoadSync(ESResLoader loader, out T asset)
bool LoadSync(out T asset)

// 获取已加载的资源
T GetAsset()

// 验证资源
bool Validate()
```

### 预定义类型

| 类型 | 用途 |
|------|------|
| `ESAssetRefer` | 通用对象 |
| `ESAssetReferPrefab` | GameObject预制体 |
| `ESAssetReferSprite` | Sprite |
| `ESAssetReferAudioClip` | AudioClip |
| `ESAssetReferTexture2D` | Texture2D |
| `ESAssetReferTexture` | Texture |
| `ESAssetReferMaterial` | Material |

### 便捷方法

```csharp
// GameObject实例化
enemyPrefab.InstantiateAsync((go) => {
    // go已实例化
}, parent: transform, loader: sharedLoader);

// Sprite应用到Image
iconSprite.ApplyToImage(image, sharedLoader, () => {
    Debug.Log("应用完成");
});

// AudioClip播放
bgmAudio.Play(audioSource, sharedLoader, () => {
    Debug.Log("开始播放");
});
```

---

## 🎯 设计原则

### 1. 不创建临时Loader

```csharp
// ❌ 错误：不应该这样设计
// var loader = new ESResLoader();  // 临时创建
// loader.LoadAllAsync();

// ✅ 正确：使用传入Loader或全局Loader
myAsset.LoadAsync(sharedLoader, callback);  // 使用传入的Loader
myAsset.LoadAsync(callback);  // 使用全局Loader
```

### 2. 不管理引用计数

ESAssetRefer不具有独立的引用计数权限，所有引用计数由ES资源系统管理：

```csharp
// ESAssetRefer只是辅助工具，实际资源管理在ES系统内部
// - ESResSource管理资源状态
// - ESResTable管理引用计数
// - ESResMaster调度加载任务
```

### 3. 批量加载使用共享Loader

```csharp
// ✅ 好：使用共享Loader批量加载
var loader = new ESResLoader();

enemy1.LoadAsync(loader, (s, p) => {});
enemy2.LoadAsync(loader, (s, p) => {});
enemy3.LoadAsync(loader, (s, p) => {});
// Loader会统一调度

// ⚠️ 可以但不推荐：每个资源用全局Loader
enemy1.LoadAsync((s, p) => {});
enemy2.LoadAsync((s, p) => {});
enemy3.LoadAsync((s, p) => {});
// 会创建多次加载请求
```

---

## 📝 使用示例

### 示例1：MonoBehaviour中使用

```csharp
public class PlayerController : MonoBehaviour
{
    [Header("@ ES资源引用")]
    public ESAssetReferPrefab weaponPrefab;
    public ESAssetReferSprite abilityIcon;
    
    private ESResLoader loader;
    
    void Start()
    {
        loader = new ESResLoader();
        LoadPlayerAssets();
    }
    
    void LoadPlayerAssets()
    {
        weaponPrefab.LoadAsync(loader, (success, prefab) =>
        {
            if (success) EquipWeapon(prefab);
        });
    }
    
    void OnDestroy()
    {
        // ES系统会自动管理资源释放
        loader?.ReleaseAll(resumePooling: false);
    }
}
```

### 示例2：ScriptableObject配置

```csharp
[CreateAssetMenu]
public class EnemyData : ScriptableObject
{
    [Header("@ ES资源引用")]
    public ESAssetReferPrefab prefab;
    public ESAssetReferSprite icon;
    public ESAssetReferAudioClip spawnSound;
    
    public void LoadAll(ESResLoader loader, Action onComplete)
    {
        int count = 0;
        int total = 3;
        
        void Check() { if (++count >= total) onComplete?.Invoke(); }
        
        prefab.LoadAsync(loader, (s, p) => Check());
        icon.LoadAsync(loader, (s, i) => Check());
        spawnSound.LoadAsync(loader, (s, a) => Check());
    }
}
```

### 示例3：关卡资源管理

```csharp
public class LevelManager : MonoBehaviour
{
    [Header("@ 关卡资源")]
    public ESAssetReferPrefab bossPrefab;
    public List<ESAssetReferPrefab> enemies;
    public List<ESAssetReferSprite> uiIcons;
    
    private ESResLoader levelLoader;
    
    void LoadLevel()
    {
        levelLoader = new ESResLoader();
        
        // 批量加载所有资源
        bossPrefab.LoadAsync(levelLoader, OnBossLoaded);
        
        foreach (var enemy in enemies)
        {
            enemy.LoadAsync(levelLoader, OnEnemyLoaded);
        }
        
        foreach (var icon in uiIcons)
        {
            icon.LoadAsync(levelLoader, OnIconLoaded);
        }
    }
    
    void OnBossLoaded(bool success, GameObject boss) { }
    void OnEnemyLoaded(bool success, GameObject enemy) { }
    void OnIconLoaded(bool success, Sprite icon) { }
    
    void UnloadLevel()
    {
        levelLoader?.ReleaseAll(resumePooling: false);
    }
}
```

---

## ⚙️ 编辑器特性

### @ 符号标识

在Inspector中，ESAssetRefer字段会显示 `@` 符号前缀，表明这是ES资源系统的便捷引用工具。

### 快速定位

点击字段旁边的 `→` 按钮可以快速定位到引用的资源文件。

### 资源验证

在编辑器下可以验证资源引用是否有效：

```csharp
#if UNITY_EDITOR
void ValidateAssets()
{
    if (!enemyPrefab.Validate())
    {
        Debug.LogError("enemyPrefab引用无效");
    }
}
#endif
```

---

## 🔧 高级用法

### 自定义类型

```csharp
[Serializable]
public class ESAssetReferMyCustomAsset : ESAssetRefer<MyCustomAsset>
{
    // 可以添加自定义便捷方法
    public void DoSomething(Action onComplete)
    {
        LoadAsync((success, asset) =>
        {
            if (success)
            {
                // 自定义逻辑
            }
            onComplete?.Invoke();
        });
    }
}
```

### 全局Loader

ESResMaster提供了全局默认Loader：

```csharp
// 访问全局Loader
var globalLoader = ESResMaster.GlobalResLoader;

// ESAssetRefer在不传入Loader时会自动使用全局Loader
myAsset.LoadAsync((s, a) => {}); // 使用全局Loader
```

---

## ✅ 最佳实践

1. **批量加载优先使用共享Loader**
2. **长期使用的Loader可以作为成员变量保存**
3. **不需要手动管理引用计数，ES系统会自动处理**
4. **编辑器下使用Validate()验证资源有效性**
5. **运行时优先使用异步加载，避免卡顿**

---

## 📚 相关文档

- [ESResLoader 使用指南](ESResLoader.md)
- [ESResSource 架构说明](ESResSource.md)
- [ESResMaster 总管文档](ESResMaster.md)

---

*ESAssetRefer - 让资源引用变得简单*
