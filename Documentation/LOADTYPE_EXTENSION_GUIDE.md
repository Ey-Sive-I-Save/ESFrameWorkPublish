# LoadType系统扩展指南

## 快速开始：添加新的资源类型

本指南演示如何优雅地扩展ES资源系统，添加自定义资源类型。得益于工厂模式架构，添加新类型只需**3个步骤**，无需修改核心代码。

---

## 示例1：添加音频流类型（AudioStream）

假设你需要支持大型音频文件的流式加载。

### 步骤1：枚举中添加类型

```csharp
// 文件: ESResSource.cs（末尾的枚举定义处）
public enum ESResSourceLoadType
{
    // ... 现有类型 ...
    
    [InspectorName("音频流")]
    AudioStream = 30,  // ✅ 新增
}
```

**命名建议**：
- AssetBundle相关用0-9
- 内置资源用10-19
- 网络资源用20-29
- 特殊资源用30+

### 步骤2：创建实现类

```csharp
// 文件: ESAudioStreamSource.cs（新建文件）
using System;
using System.Collections;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 音频流资源源（支持大型音频文件的流式加载）
    /// </summary>
    public class ESAudioStreamSource : ESResSourceBase
    {
        private AudioClip _audioClip;
        private WWW _www;

        /// <summary>
        /// 获取已加载的音频
        /// </summary>
        public AudioClip GetAudioClip() => _audioClip;

        protected override void Initilized()
        {
            base.Initilized();
            // 初始化特定资源的状态
        }

        /// <summary>
        /// 同步加载（音频流不支持同步加载）
        /// </summary>
        public override bool LoadSync()
        {
            OnResLoadFaild("音频流不支持同步加载，请使用异步方式");
            return false;
        }

        /// <summary>
        /// 异步加载音频流
        /// </summary>
        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            // 1. 构造URL（从本地或网络）
            string audioPath = m_ResKey?.LocalABLoadPath ?? m_ResKey?.Path;
            if (string.IsNullOrEmpty(audioPath))
            {
                OnResLoadFaild("音频路径为空");
                finishCallback?.Invoke();
                yield break;
            }

            // 2. 使用WWW流式加载音频
            _www = new WWW("file://" + audioPath);

            // 3. 等待加载完成
            while (!_www.isDone)
            {
                ReportProgress(_www.progress);
                yield return null;
            }

            // 4. 检查错误
            if (!string.IsNullOrEmpty(_www.error))
            {
                OnResLoadFaild($"加载音频失败: {_www.error}");
                finishCallback?.Invoke();
                yield break;
            }

            // 5. 获取AudioClip
            _audioClip = _www.GetAudioClip(threeD: true, stream: true);
            if (_audioClip == null)
            {
                OnResLoadFaild("无法从WWW创建AudioClip");
                finishCallback?.Invoke();
                yield break;
            }

            // 6. 完成加载
            if (!CompleteWithAsset(_audioClip))
            {
                Debug.LogError($"加载音频失败: {ResName}");
                finishCallback?.Invoke();
                yield break;
            }

            ReportProgress(1f);
            finishCallback?.Invoke();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected override void TryReleaseRes()
        {
            if (_www != null)
            {
                _www.Dispose();
                _www = null;
            }

            if (_audioClip != null)
            {
                UnityEngine.Object.Destroy(_audioClip);
                _audioClip = null;
            }

            base.TryReleaseRes();
        }

        /// <summary>
        /// 回收到对象池（如果使用对象池）
        /// </summary>
        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
            // TODO: 如果有AudioStreamSource对象池，在这里回收
            // ESResMaster.Instance?.PoolForAudioStream.PushToPool(this);
        }
    }
}
```

### 步骤3：工厂注册

```csharp
// 文件: ESResSourceFactory.cs（RegisterBuiltInTypes方法中）
private static void RegisterBuiltInTypes()
{
    // ... 现有注册 ...

    // 音频流类型（直接new，不使用对象池）
    RegisterType(ESResSourceLoadType.AudioStream, () => 
        new ESAudioStreamSource());
}
```

**✅ 完成！** 现在可以使用新类型了。

---

## 使用示例

### 加载音频流

```csharp
using UnityEngine;
using ES;

public class AudioStreamExample : MonoBehaviour
{
    private void Start()
    {
        // 创建资源加载器
        var loader = ESResMaster.Instance.CreateResLoader();

        // 创建音频资源键
        var audioKey = new ESResKey("Musics/BGM_MainTheme.mp3", typeof(AudioClip));

        // 异步加载音频流
        loader.Add2LoadByKey(audioKey, ESResSourceLoadType.AudioStream, (source) =>
        {
            var audioSource = source as ESAudioStreamSource;
            if (audioSource != null)
            {
                var audioClip = audioSource.GetAudioClip();
                Debug.Log($"音频加载完成: {audioClip.name}, 长度: {audioClip.length}秒");

                // 播放音频
                var player = GetComponent<AudioSource>();
                player.clip = audioClip;
                player.Play();
            }
        });

        // 开始加载
        loader.LoadAsync();
    }
}
```

---

## 示例2：添加视频类型（VideoStream）

### 步骤1：枚举添加

```csharp
public enum ESResSourceLoadType
{
    // ... 现有类型 ...
    
    [InspectorName("视频流")]
    VideoStream = 31,  // ✅ 新增
}
```

### 步骤2：实现类

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace ES
{
    /// <summary>
    /// 视频流资源源
    /// </summary>
    public class ESVideoStreamSource : ESResSourceBase
    {
        private VideoClip _videoClip;

        public VideoClip GetVideoClip() => _videoClip;

        public override bool LoadSync()
        {
            OnResLoadFaild("视频流不支持同步加载");
            return false;
        }

        public override IEnumerator DoTaskAsync(Action finishCallback)
        {
            if (State == ResSourceState.Ready)
            {
                finishCallback?.Invoke();
                yield break;
            }

            BeginLoad();

            string videoPath = m_ResKey?.LocalABLoadPath ?? m_ResKey?.Path;
            if (string.IsNullOrEmpty(videoPath))
            {
                OnResLoadFaild("视频路径为空");
                finishCallback?.Invoke();
                yield break;
            }

            // 使用VideoPlayer异步加载
            var videoPlayer = new GameObject("TempVideoLoader").AddComponent<VideoPlayer>();
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = "file://" + videoPath;
            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            _videoClip = videoPlayer.clip;
            GameObject.Destroy(videoPlayer.gameObject);

            if (!CompleteWithAsset(_videoClip))
            {
                Debug.LogError($"加载视频失败: {ResName}");
                finishCallback?.Invoke();
                yield break;
            }

            ReportProgress(1f);
            finishCallback?.Invoke();
        }

        protected override void TryReleaseRes()
        {
            _videoClip = null;
            base.TryReleaseRes();
        }

        public override void TryAutoPushedToPool()
        {
            base.TryAutoPushedToPool();
        }
    }
}
```

### 步骤3：工厂注册

```csharp
RegisterType(ESResSourceLoadType.VideoStream, () => 
    new ESVideoStreamSource());
```

---

## 示例3：扩展扩展方法

为新类型添加查询方法：

```csharp
// 文件: ESResSource.cs（ESResSourceLoadTypeExtensions类）
public static class ESResSourceLoadTypeExtensions
{
    // ... 现有方法 ...

    /// <summary>
    /// 判断是否为流式媒体类型
    /// </summary>
    public static bool IsStreamingMediaType(this ESResSourceLoadType loadType)
    {
        return loadType == ESResSourceLoadType.AudioStream ||
               loadType == ESResSourceLoadType.VideoStream;
    }

    /// <summary>
    /// 判断是否需要网络连接
    /// </summary>
    public static bool RequiresNetwork(this ESResSourceLoadType loadType)
    {
        return loadType == ESResSourceLoadType.NetImageRes ||
               loadType.IsStreamingMediaType();  // 假设流媒体也可能从网络加载
    }

    /// <summary>
    /// 获取推荐的加载并发数
    /// </summary>
    public static int GetRecommendedConcurrency(this ESResSourceLoadType loadType)
    {
        switch (loadType)
        {
            case ESResSourceLoadType.AudioStream:
            case ESResSourceLoadType.VideoStream:
                return 1;  // 大文件，限制并发
            case ESResSourceLoadType.ABAsset:
            case ESResSourceLoadType.NetImageRes:
                return 5;  // 小文件，允许多个并发
            default:
                return 3;  // 默认并发数
        }
    }
}
```

---

## 高级用法：对象池支持

如果你的资源类型需要频繁创建/销毁，建议使用对象池。

### 步骤1：创建对象池

```csharp
// 文件: ESResMaster.cs
public partial class ESResMaster
{
    // 添加对象池字段
    public SimplePool<ESAudioStreamSource> PoolForAudioStream { get; private set; }

    private void InitializePools()
    {
        // ... 现有对象池初始化 ...

        PoolForAudioStream = new SimplePool<ESAudioStreamSource>(
            createFunc: () => new ESAudioStreamSource(),
            capacity: 10  // 初始容量
        );
    }
}
```

### 步骤2：修改工厂注册

```csharp
RegisterType(ESResSourceLoadType.AudioStream, () => 
{
    var source = ESResMaster.Instance.PoolForAudioStream.GetInPool();
    return source;
});
```

### 步骤3：修改TryAutoPushedToPool

```csharp
public override void TryAutoPushedToPool()
{
    base.TryAutoPushedToPool();
    ESResMaster.Instance?.PoolForAudioStream.PushToPool(this);
}
```

---

## 引用计数支持

如果新类型需要引用计数管理，按以下步骤添加：

### 步骤1：扩展ESResTable

```csharp
// 文件: ESResTable.cs
public partial class ESResTable
{
    private Dictionary<object, ESResSourceBase> _audioStreamTable = 
        new Dictionary<object, ESResSourceBase>();

    public bool TryRegisterAudioStream(object key, ESResSourceBase source)
    {
        if (_audioStreamTable.ContainsKey(key))
        {
            return false;
        }
        _audioStreamTable[key] = source;
        return true;
    }

    public ESResSourceBase GetAudioStreamByKey(object key)
    {
        _audioStreamTable.TryGetValue(key, out var source);
        return source;
    }

    public void AcquireAudioStream(object key)
    {
        if (_audioStreamTable.TryGetValue(key, out var source))
        {
            source.Retain();
        }
    }

    public void ReleaseAudioStream(object key, bool unloadWhenZero)
    {
        if (_audioStreamTable.TryGetValue(key, out var source))
        {
            source.Release(unloadWhenZero);
            if (source.ReferenceCount == 0 && unloadWhenZero)
            {
                _audioStreamTable.Remove(key);
            }
        }
    }
}
```

### 步骤2：更新ESResMaster

```csharp
// 文件: ESResMaster.cs
public ESResSourceBase GetResSourceByKey(object key, ESResSourceLoadType loadType, bool ifNullCreateNew = true)
{
    ESResSourceBase res = null;
    
    // 添加新类型的查询
    if (loadType == ESResSourceLoadType.AudioStream)
    {
        res = ResTable.GetAudioStreamByKey(key);
    }
    else if (loadType == ESResSourceLoadType.ABAsset)
    {
        res = ResTable.GetAssetResByKey(key);
    }
    // ... 其他类型 ...

    if (res != null)
    {
        AcquireResHandle(key, loadType);
        return res;
    }

    // ... 创建新资源 ...
    if (res != null)
    {
        bool registered = false;
        if (loadType == ESResSourceLoadType.AudioStream)
        {
            registered = ResTable.TryRegisterAudioStream(key, res);
        }
        // ... 其他类型 ...
    }

    return res;
}

private void AcquireResHandle(object key, ESResSourceLoadType loadType)
{
    switch (loadType)
    {
        case ESResSourceLoadType.AudioStream:
            ResTable.AcquireAudioStream(key);
            break;
        // ... 其他类型 ...
    }
}

internal void ReleaseResHandle(object key, ESResSourceLoadType loadType, bool unloadWhenZero)
{
    switch (loadType)
    {
        case ESResSourceLoadType.AudioStream:
            ResTable.ReleaseAudioStream(key, unloadWhenZero);
            break;
        // ... 其他类型 ...
    }
}
```

---

## 最佳实践

### 1. 命名规范

| 类型前缀 | 说明 | 示例 |
|---------|------|------|
| AB | AssetBundle相关 | ABAsset, ABScene |
| Net | 网络资源 | NetImageRes, NetAudioStream |
| Local | 本地文件 | LocalImageRes, LocalVideoFile |
| Raw | 原始文件（无反序列化） | RawFile, RawConfig |
| Internal | Unity内置资源 | InternalResource |

### 2. 错误处理

```csharp
public override IEnumerator DoTaskAsync(Action finishCallback)
{
    BeginLoad();

    try
    {
        // 加载逻辑
        yield return LoadOperation();

        if (!CompleteWithAsset(asset))
        {
            throw new Exception("资源加载失败");
        }
    }
    catch (Exception ex)
    {
        OnResLoadFaild($"加载失败: {ex.Message}");
    }
    finally
    {
        finishCallback?.Invoke();
    }
}
```

### 3. 进度报告

```csharp
while (!operation.isDone)
{
    // 报告0-100%的进度
    ReportProgress(operation.progress);
    yield return null;
}
ReportProgress(1f);  // 确保最后报告100%
```

### 4. 内存管理

```csharp
protected override void TryReleaseRes()
{
    // 1. 先释放托管资源
    _managedResource = null;

    // 2. 再释放Unity对象
    if (_unityObject != null)
    {
        UnityEngine.Object.Destroy(_unityObject);
        _unityObject = null;
    }

    // 3. 调用基类
    base.TryReleaseRes();
}
```

---

## 调试技巧

### 1. 查看已注册的类型

```csharp
var types = ESResSourceFactory.GetRegisteredTypes();
foreach (var type in types)
{
    Debug.Log($"已注册类型: {type.GetDisplayName()}");
}
```

### 2. 验证类型是否注册

```csharp
if (!ESResSourceFactory.IsTypeRegistered(ESResSourceLoadType.AudioStream))
{
    Debug.LogError("AudioStream类型未注册！");
}
```

### 3. 使用扩展方法查询特性

```csharp
var loadType = ESResSourceLoadType.AudioStream;

Debug.Log($"是否需要引用计数: {loadType.RequiresReferenceCount()}");
Debug.Log($"是否支持同步加载: {loadType.SupportsSyncLoad()}");
Debug.Log($"推荐并发数: {loadType.GetRecommendedConcurrency()}");
```

---

## 常见问题

### Q1: 新类型不需要引用计数怎么办？

在扩展方法中标记：

```csharp
public static bool RequiresReferenceCount(this ESResSourceLoadType loadType)
{
    return loadType != ESResSourceLoadType.ShaderVariant &&
           loadType != ESResSourceLoadType.AudioStream;  // ✅ 新增
}
```

### Q2: 如何支持运行时热注册类型？

工厂支持运行时注册：

```csharp
// 游戏启动时动态注册Mod类型
ESResSourceFactory.RegisterType(
    ESResSourceLoadType.ModAsset,
    () => new ESModAssetSource()
);
```

### Q3: 如何实现类型特定的加载策略？

在实现类中自定义：

```csharp
public override IEnumerator DoTaskAsync(Action finishCallback)
{
    // 大文件：分块加载
    if (GetFileSize() > 100 * 1024 * 1024)  // 100MB
    {
        yield return LoadInChunks();
    }
    else
    {
        yield return LoadNormally();
    }
}
```

---

## 总结

添加新资源类型只需3步：
1. ✅ 枚举中添加值
2. ✅ 创建ESResSourceBase子类
3. ✅ 工厂中注册

无需修改：
- ❌ ESResMaster核心代码
- ❌ ESResLoader加载逻辑
- ❌ 其他现有类型实现

**文档版本**: v1.0  
**更新日期**: 2025-01-24  
**参考文档**: [LOADTYPE_ARCHITECTURE.md](LOADTYPE_ARCHITECTURE.md)
