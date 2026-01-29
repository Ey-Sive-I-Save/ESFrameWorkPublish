# LoadType系统快速参考

## 🎯 一分钟快速上手

### 查看所有类型
```csharp
var types = ESResSourceFactory.GetRegisteredTypes();
// 输出: AssetBundle, ABAsset, ABScene, ShaderVariant, RawFile, ...
```

### 使用现有类型
```csharp
// 加载AB包
var abKey = new ESResKey("ui_mainmenu", typeof(AssetBundle));
loader.Add2LoadByKey(abKey, ESResSourceLoadType.AssetBundle, ...);

// 加载配置文件
var configKey = new ESResKey("config.json", typeof(TextAsset));
loader.Add2LoadByKey(configKey, ESResSourceLoadType.RawFile, (source) =>
{
    var rawFile = source as ESRawFileSource;
    byte[] data = rawFile.GetRawData();
});
```

### 添加新类型（3步）
```csharp
// 1. 枚举添加
[InspectorName("音频流")] AudioStream = 30,

// 2. 创建实现类
public class ESAudioStreamSource : ESResSourceBase { }

// 3. 工厂注册
ESResSourceFactory.RegisterType(ESResSourceLoadType.AudioStream, 
    () => new ESAudioStreamSource());
```

---

## 📋 类型速查表

| 类型 | 值 | 用途 | 同步 | 引用计数 | 对象池 |
|------|---|------|-----|---------|-------|
| AssetBundle | 0 | AB包文件 | ✅ | ✅ | ✅ |
| ABAsset | 1 | AB资源 | ✅ | ✅ | ✅ |
| ABScene | 2 | AB场景 | ❌ | ✅ | ❌ |
| **ShaderVariant** | 3 | Shader变体 | ✅ | ❌ | ❌ |
| **RawFile** | 4 | 原始文件 | ✅ | ⚠️ | ❌ |
| InternalResource | 10 | Resources | ✅ | ✅ | ❌ |
| NetImageRes | 20 | 网络图片 | ❌ | ✅ | ❌ |
| LocalImageRes | 21 | 本地图片 | ✅ | ✅ | ❌ |

---

## 🔍 扩展方法速查

```csharp
// 类型分类
loadType.IsAssetBundleType()     // AB包、AB资源、AB场景
loadType.IsImageType()           // 网络图片、本地图片
loadType.IsNetworkResource()     // 需要网络连接

// 行为特性
loadType.RequiresReferenceCount()  // 是否需要引用计数
loadType.SupportsSyncLoad()        // 是否支持同步加载

// 工具方法
loadType.GetDisplayName()          // 中文显示名
loadType.GetPoolKey()              // 对象池键名
```

---

## 🏗️ 工厂API速查

```csharp
// 创建资源源
var source = ESResSourceFactory.CreateResSource(key, loadType);

// 注册类型
ESResSourceFactory.RegisterType(loadType, creator);

// 查询类型
bool registered = ESResSourceFactory.IsTypeRegistered(loadType);
LoadType[] types = ESResSourceFactory.GetRegisteredTypes();

// 取消注册（谨慎使用）
bool removed = ESResSourceFactory.UnregisterType(loadType);
```

---

## 📊 实现类模板

```csharp
using System;
using System.Collections;
using UnityEngine;

namespace ES
{
    public class ESCustomSource : ESResSourceBase
    {
        private YourAssetType _asset;

        protected override void Initilized()
        {
            base.Initilized();
            // 初始化特定状态
        }

        public override bool LoadSync()
        {
            if (State == ResSourceState.Ready)
                return true;

            BeginLoad();

            try
            {
                // 同步加载逻辑
                _asset = LoadYourAsset();
                return CompleteWithAsset(_asset);
            }
            catch (Exception ex)
            {
                OnResLoadFaild($"加载失败: {ex.Message}");
                return false;
            }
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            // 异步加载逻辑
            var operation = LoadYourAssetAsync();

            while (!operation.isDone)
            {
                ReportProgress(operation.progress);
                yield return null;
            }

            _asset = operation.asset;

            if (!CompleteWithAsset(_asset))
            {
                Debug.LogError($"加载失败: {ResName}");
            }

            ReportProgress(1f);
            finishCallback?.Invoke();
        }

        protected override void TryReleaseRes()
        {
            if (_asset != null)
            {
                // 释放资源
                UnityEngine.Object.Destroy(_asset);
                _asset = null;
            }
            base.TryReleaseRes();
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            // 如果使用对象池，在这里回收
            // ESResMaster.Instance?.PoolForCustom.PushToPool(this);
        }
    }
}
```

---

## 🐛 常见错误处理

### 错误1：类型未注册
```csharp
// 异常: NotSupportedException
// 解决: 在ESResSourceFactory.RegisterBuiltInTypes()中注册

RegisterType(ESResSourceLoadType.YourType, () => 
    new ESYourSource());
```

### 错误2：资源键类型错误
```csharp
// 异常: "资源键类型错误，必须是ESResKey"
// 解决: 使用ESResKey而非string

var key = new ESResKey("path/to/asset", typeof(YourType));  // ✅
// NOT: var key = "path/to/asset";  // ❌
```

### 错误3：加载失败
```csharp
// 检查清单:
// 1. 路径是否正确? m_ResKey?.LocalABLoadPath
// 2. 资源是否存在? File.Exists(path)
// 3. 类型是否匹配? key.TargetType
// 4. 是否调用了CompleteWithAsset()? 
```

---

## 💡 最佳实践

### 1. 错误处理
```csharp
try
{
    // 加载逻辑
}
catch (Exception ex)
{
    OnResLoadFaild($"加载失败: {ex.Message}");
}
finally
{
    finishCallback?.Invoke();
}
```

### 2. 进度报告
```csharp
while (!operation.isDone)
{
    ReportProgress(operation.progress);  // 0-1
    yield return null;
}
ReportProgress(1f);  // 确保最后是100%
```

### 3. 内存管理
```csharp
protected override void TryReleaseRes()
{
    // 1. 托管资源
    _managedData = null;

    // 2. Unity对象
    if (_unityObject != null)
    {
        UnityEngine.Object.Destroy(_unityObject);
        _unityObject = null;
    }

    // 3. 基类
    base.TryReleaseRes();
}
```

### 4. 同步 vs 异步
```csharp
public override bool LoadSync()
{
    // 网络资源、大文件不支持同步
    if (IsNetworkResource || IsLargeFile)
    {
        OnResLoadFaild("此资源不支持同步加载");
        return false;
    }

    // 实现同步加载
}
```

---

## 📚 完整文档

- **[LOADTYPE_README.md](LOADTYPE_README.md)** - 总入口文档
- **[LOADTYPE_ARCHITECTURE.md](LOADTYPE_ARCHITECTURE.md)** - 架构设计（5200行）
- **[LOADTYPE_EXTENSION_GUIDE.md](LOADTYPE_EXTENSION_GUIDE.md)** - 扩展指南（800行）
- **[LOADTYPE_REFACTOR_SUMMARY.md](LOADTYPE_REFACTOR_SUMMARY.md)** - 重构总结（900行）

---

## ⚡ 性能提示

### 对象池优化
```csharp
// 频繁创建的类型使用对象池
RegisterType(loadType, () => 
    ESResMaster.Instance.PoolForYourType.GetInPool());
```

### 并发控制
```csharp
// 大文件限制并发数
var concurrency = loadType.GetRecommendedConcurrency();
// AudioStream: 1, ABAsset: 5, Default: 3
```

### 内存监控
```csharp
// 检查引用计数
if (source.ReferenceCount == 0)
{
    source.Release(unloadWhenZero: true);
}
```

---

**最后更新**: 2025-01-24  
**快速参考版本**: v1.0
