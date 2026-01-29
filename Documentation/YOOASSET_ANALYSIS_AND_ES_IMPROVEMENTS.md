# YooAsset关键特性分析与ES系统改进方案

## 📌 问题1：为什么YooAsset强调Shader和渲染管线？

### 🎨 Shader的特殊性

#### **1. Shader变体问题**
```csharp
// 问题：Unity在首次使用Shader时会编译变体
Material mat = new Material(shader);  // ← 可能卡顿200-500ms！
```

**原因**：
- Shader有数百个变体（不同关键字组合）
- 首次使用时Unity会**运行时编译**
- 编译发生在渲染线程，导致严重卡顿

**YooAsset的解决方案**：
```csharp
// ShaderVariantCollection预加载
public class ShaderWarmup
{
    public void PreloadShaders()
    {
        // 1. 加载ShaderVariantCollection
        var svc = Resources.Load<ShaderVariantCollection>("AllShaders");
        
        // 2. 预热所有变体
        svc.WarmUp();  // ← 提前编译，避免运行时卡顿
        
        // 3. 保持常驻内存
        // 不释放，避免再次编译
    }
}
```

#### **2. Shader依赖问题**
```
Material.mat (材质)
  ↓ 依赖
Standard.shader (Shader)
  ↓ 依赖
UnityShaderVariables.cginc (公共头文件)
```

**问题**：
- Material加载时，Shader必须已加载
- Shader AB被错误卸载 → 材质变粉红
- 依赖关系复杂，手动管理困难

**YooAsset的处理**：
- ✅ Shader AB有**最高优先级**加载
- ✅ Material加载时**自动保持**Shader AB引用
- ✅ Shader AB永不卸载（除非手动强制）

### 🎬 渲染管线的特殊性

#### **1. URP/HDRP GlobalSettings**
```csharp
// UniversalRenderPipelineGlobalSettings.asset
// - 始终加载在内存
// - 场景切换不卸载
// - 影响整个渲染流程
```

**问题**：
- 如果被卸载 → 渲染错误/黑屏
- 需要在游戏启动时预加载
- 必须标记为"永不卸载"

**YooAsset的处理**：
```csharp
// 特殊标记
[AssetTag("AlwaysInclude")]
public UniversalRenderPipelineGlobalSettings globalSettings;

// 启动时预加载
void Awake()
{
    YooAssets.LoadAssetAsync<UniversalRenderPipelineGlobalSettings>(
        "URPGlobalSettings",
        loadMode: LoadMode.AlwaysResident  // ← 永不卸载
    );
}
```

#### **2. RenderPipelineAsset引用**
```csharp
// QualitySettings.renderPipeline 引用
// - 切换画质时更换RenderPipelineAsset
// - Asset必须常驻内存
// - 卸载会导致Unity崩溃
```

---

## 🚀 问题2：YooAsset有哪些功能需要立刻借鉴？

### 1️⃣ **资源标签系统（Tags）**

#### **YooAsset的实现**
```csharp
// 编辑器中标记资源
[AssetTag("Level1")]
public GameObject levelPrefab;

[AssetTag("UI", "MainMenu")]
public GameObject mainMenu;

// 运行时批量加载
var handle = YooAssets.LoadAssetsByTag<GameObject>("Level1");
```

**优势**：
- ✅ 按标签批量加载/卸载
- ✅ 灵活的资源分组
- ✅ 不依赖路径或AB名

#### **ES需要的改进**
```csharp
// 在ResPage中添加标签
[Serializable]
public class ResPage : PageBase
{
    [LabelText("资源标签")]
    public List<string> Tags = new List<string>();
    
    public UnityEngine.Object OB;
}

// 在ResLibrary中添加按标签查询
public class ResLibrary
{
    public List<ResPage> GetPagesByTag(string tag)
    {
        var results = new List<ResPage>();
        foreach (var book in GetAllUseableBooks())
        {
            foreach (var page in book.pages)
            {
                if (page.Tags != null && page.Tags.Contains(tag))
                {
                    results.Add(page);
                }
            }
        }
        return results;
    }
}

// 使用示例
var loader = ESResMaster.GetLoader();
var pages = library.GetPagesByTag("Level1");
foreach (var page in pages)
{
    loader.AddAsset2LoadByGUIDSourcer(page.GUID);
}
loader.LoadAllAsync();
```

---

### 2️⃣ **原生文件加载（RawFile）**

#### **YooAsset的实现**
```csharp
// 直接加载文件字节流，无需反序列化
var handle = YooAssets.LoadRawFileAsync("Config/config.json");
byte[] bytes = handle.GetRawFileData();
string json = Encoding.UTF8.GetString(bytes);
```

**优势**：
- ✅ 加载速度快（无Unity反序列化）
- ✅ 适合大文件（视频、音频、配置）
- ✅ 节省内存（不创建UnityEngine.Object）

#### **ES需要的改进**
```csharp
// 添加RawFile加载类型
public enum ESResSourceLoadType
{
    ABAsset,
    AssetBundle,
    RawFile  // ← 新增
}

// RawFile Source实现
public class ESRawFileSource : ESResSourceBase
{
    private byte[] m_RawData;
    
    public byte[] GetRawData() => m_RawData;
    
    public override bool LoadSync()
    {
        var path = Path.Combine(ESResMaster.ABDownloadPath, LibFolderName, ResName);
        if (File.Exists(path))
        {
            m_RawData = File.ReadAllBytes(path);
            State = ResSourceState.Ready;
            return true;
        }
        return false;
    }
    
    protected override void TryReleaseRes()
    {
        m_RawData = null;
    }
}

// Loader中添加支持
public void AddRawFile2Load(string fileName, Action<byte[]> callback)
{
    var source = new ESRawFileSource();
    source.Set(new ESResKey { ResName = fileName }, ESResSourceLoadType.RawFile);
    // ...
}
```

---

### 3️⃣ **资源包下载器（Downloader）**

#### **YooAsset的实现**
```csharp
// 创建下载器
var downloader = YooAssets.CreateResourceDownloader(
    downloadingMaxNum: 10,     // 最大并发数
    failedTryAgain: 3          // 失败重试次数
);

// 下载进度
downloader.OnDownloadProgress = (totalBytes, downloadedBytes) =>
{
    float progress = (float)downloadedBytes / totalBytes;
    progressBar.value = progress;
};

// 开始下载
downloader.BeginDownload();
await downloader.WaitForDownloadOver();
```

**优势**：
- ✅ 断点续传
- ✅ 并发控制
- ✅ 自动重试
- ✅ 下载队列管理

#### **ES当前的问题**
```csharp
// ES目前的下载比较简陋
UnityWebRequest.Get(url).SendWebRequest();  // 无断点续传、无重试
```

#### **ES需要的改进**
```csharp
public class ESDownloader
{
    private class DownloadTask
    {
        public string Url;
        public string SavePath;
        public long TotalBytes;
        public long DownloadedBytes;
        public int RetryCount;
    }
    
    private Queue<DownloadTask> _taskQueue = new Queue<DownloadTask>();
    private List<DownloadTask> _downloading = new List<DownloadTask>();
    
    public int MaxConcurrent = 5;
    public int MaxRetry = 3;
    
    public Action<float> OnProgress;  // 0-1
    public Action<bool> OnComplete;
    
    public void AddTask(string url, string savePath, long fileSize)
    {
        _taskQueue.Enqueue(new DownloadTask
        {
            Url = url,
            SavePath = savePath,
            TotalBytes = fileSize
        });
    }
    
    public void StartDownload()
    {
        StartCoroutine(DownloadCoroutine());
    }
    
    private IEnumerator DownloadCoroutine()
    {
        while (_taskQueue.Count > 0 || _downloading.Count > 0)
        {
            // 控制并发数
            while (_downloading.Count < MaxConcurrent && _taskQueue.Count > 0)
            {
                var task = _taskQueue.Dequeue();
                _downloading.Add(task);
                StartCoroutine(DownloadFile(task));
            }
            
            // 更新进度
            UpdateProgress();
            
            yield return null;
        }
        
        OnComplete?.Invoke(true);
    }
    
    private IEnumerator DownloadFile(DownloadTask task)
    {
        // 检查已下载字节（断点续传）
        if (File.Exists(task.SavePath))
        {
            task.DownloadedBytes = new FileInfo(task.SavePath).Length;
        }
        
        // 使用Range Header实现断点续传
        var request = UnityWebRequest.Get(task.Url);
        request.SetRequestHeader("Range", $"bytes={task.DownloadedBytes}-");
        
        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            task.DownloadedBytes += (long)(operation.progress * 1024);  // 粗略估算
            yield return null;
        }
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            // 追加写入（断点续传）
            using (var fs = File.Open(task.SavePath, FileMode.Append))
            {
                fs.Write(request.downloadHandler.data, 0, request.downloadHandler.data.Length);
            }
            _downloading.Remove(task);
        }
        else
        {
            // 失败重试
            task.RetryCount++;
            if (task.RetryCount < MaxRetry)
            {
                Debug.LogWarning($"下载失败，重试 {task.RetryCount}/{MaxRetry}: {task.Url}");
                _taskQueue.Enqueue(task);
            }
            else
            {
                Debug.LogError($"下载失败，已达最大重试次数: {task.Url}");
            }
            _downloading.Remove(task);
        }
    }
    
    private void UpdateProgress()
    {
        long totalBytes = 0;
        long downloadedBytes = 0;
        
        foreach (var task in _taskQueue)
        {
            totalBytes += task.TotalBytes;
        }
        
        foreach (var task in _downloading)
        {
            totalBytes += task.TotalBytes;
            downloadedBytes += task.DownloadedBytes;
        }
        
        OnProgress?.Invoke(totalBytes > 0 ? (float)downloadedBytes / totalBytes : 0);
    }
}
```

---

### 4️⃣ **资源卸载策略**

#### **YooAsset的三种策略**
```csharp
public enum UnloadMode
{
    // 立即卸载（适合一次性资源）
    UnloadImmediate,
    
    // 延迟卸载（3秒后，适合可能复用的资源）
    UnloadDeferred,
    
    // 场景切换时卸载（适合场景专属资源）
    UnloadOnSceneChange
}

// 使用
handle.Release(UnloadMode.UnloadDeferred);
```

#### **ES需要的改进**
```csharp
public class ESResTable
{
    // 延迟卸载队列
    private class DeferredUnload
    {
        public object Key;
        public float UnloadTime;
    }
    
    private List<DeferredUnload> _deferredUnloads = new List<DeferredUnload>();
    
    public enum ESUnloadMode
    {
        Immediate,      // 立即卸载
        Deferred,       // 延迟3秒
        OnSceneChange   // 场景切换时
    }
    
    public int ReleaseAssetRes(object key, ESUnloadMode mode = ESUnloadMode.Deferred)
    {
        var count = InternalRelease(_assetSources, _assetRefCounts, key);
        
        if (count == 0)
        {
            switch (mode)
            {
                case ESUnloadMode.Immediate:
                    TryRemoveEntry(_assetSources, _assetRefCounts, key, true);
                    break;
                    
                case ESUnloadMode.Deferred:
                    _deferredUnloads.Add(new DeferredUnload
                    {
                        Key = key,
                        UnloadTime = Time.unscaledTime + 3f
                    });
                    break;
                    
                case ESUnloadMode.OnSceneChange:
                    // 注册到场景切换事件
                    SceneManager.sceneUnloaded += (scene) =>
                    {
                        TryRemoveEntry(_assetSources, _assetRefCounts, key, true);
                    };
                    break;
            }
        }
        
        return count;
    }
    
    public void Update()
    {
        // 处理延迟卸载
        for (int i = _deferredUnloads.Count - 1; i >= 0; i--)
        {
            var item = _deferredUnloads[i];
            if (Time.unscaledTime >= item.UnloadTime)
            {
                // 再次检查引用计数（可能在延迟期间被再次引用）
                if (_assetRefCounts.TryGetValue(item.Key, out var count) && count == 0)
                {
                    TryRemoveEntry(_assetSources, _assetRefCounts, item.Key, true);
                }
                _deferredUnloads.RemoveAt(i);
            }
        }
    }
}
```

---

### 5️⃣ **资源加载优先级**

#### **YooAsset的实现**
```csharp
// 高优先级（UI、玩家角色）
var handle = YooAssets.LoadAssetAsync<GameObject>(
    "Hero", 
    priority: 100  // ← 优先级越高越先加载
);

// 低优先级（背景音乐、特效）
var bgm = YooAssets.LoadAssetAsync<AudioClip>(
    "BGM",
    priority: 10
);
```

#### **ES需要的改进**
```csharp
public class ESResLoader
{
    // 优先级队列（使用SortedList）
    private SortedList<int, LinkedList<ESResSourceBase>> _priorityQueue = 
        new SortedList<int, LinkedList<ESResSourceBase>>(
            Comparer<int>.Create((a, b) => b.CompareTo(a))  // 降序
        );
    
    public void Add2LoadByKey(object key, ESResSourceLoadType loadType, 
        Action<bool, ESResSourceBase> listener = null, 
        int priority = 0)  // ← 新增优先级参数
    {
        var res = ESResMaster.Instance.GetResSourceByKey(key, loadType);
        
        // 按优先级插入队列
        if (!_priorityQueue.ContainsKey(priority))
        {
            _priorityQueue[priority] = new LinkedList<ESResSourceBase>();
        }
        _priorityQueue[priority].AddLast(res);
        
        // ...
    }
    
    private void DoLoadAsync()
    {
        // 优先加载高优先级资源
        foreach (var kvp in _priorityQueue)  // 已按优先级降序排列
        {
            var queue = kvp.Value;
            while (queue.Count > 0 && mLoadingCount < MaxConcurrent)
            {
                var res = queue.First.Value;
                queue.RemoveFirst();
                
                res.LoadAsync();
                mLoadingCount++;
            }
        }
    }
}
```

---

### 6️⃣ **资源预加载和预热**

#### **YooAsset的实现**
```csharp
// 游戏启动时预加载
public class GameBootstrap : MonoBehaviour
{
    async void Start()
    {
        // 1. 预热Shader
        var shaders = await YooAssets.LoadAssetsByTag<ShaderVariantCollection>("Shaders");
        foreach (var svc in shaders)
        {
            svc.WarmUp();
        }
        
        // 2. 预加载常用资源
        var commonAssets = await YooAssets.LoadAssetsByTag<GameObject>("Common");
        
        // 3. 实例化对象池
        foreach (var prefab in commonAssets)
        {
            ObjectPool.Preload(prefab, 10);
        }
    }
}
```

#### **ES需要的改进**
```csharp
public class ESPreloadManager : MonoBehaviour
{
    [Header("预加载配置")]
    public List<string> PreloadTags = new List<string> { "Common", "UI", "Shaders" };
    
    public Action<float> OnProgress;
    public Action OnComplete;
    
    public IEnumerator PreloadAll()
    {
        int totalCount = 0;
        int loadedCount = 0;
        
        // 收集所有需要预加载的资源
        var loader = ESResMaster.GetLoader();
        foreach (var tag in PreloadTags)
        {
            var pages = GetLibrary().GetPagesByTag(tag);
            totalCount += pages.Count;
            
            foreach (var page in pages)
            {
                loader.AddAsset2LoadByGUIDSourcer(page.GUID, (success, res) =>
                {
                    loadedCount++;
                    OnProgress?.Invoke((float)loadedCount / totalCount);
                    
                    // Shader特殊处理
                    if (res.Asset is ShaderVariantCollection svc)
                    {
                        svc.WarmUp();
                        Debug.Log($"预热Shader变体集: {svc.name}");
                    }
                });
            }
        }
        
        loader.LoadAllAsync(() =>
        {
            Debug.Log($"预加载完成: {loadedCount}/{totalCount}");
            OnComplete?.Invoke();
            
            // 🔥 不释放，保持常驻内存
        });
        
        yield return null;
    }
}
```

---

## 🎯 ES系统立即需要实施的改进

### 优先级排序

| 优先级 | 功能 | 工作量 | 收益 |
|--------|------|--------|------|
| 🔥 **P0** | **Shader预加载机制** | 1天 | 避免运行时卡顿 |
| 🔥 **P0** | **资源永不卸载标记** | 0.5天 | 防止渲染管线崩溃 |
| 🔴 **P1** | **资源标签系统** | 2天 | 灵活的资源分组 |
| 🔴 **P1** | **延迟卸载机制** | 1天 | 提升复用率 |
| 🟡 **P2** | **下载器改进** | 3天 | 断点续传+重试 |
| 🟡 **P2** | **加载优先级** | 1天 | 优化加载顺序 |
| 🟢 **P3** | **RawFile加载** | 1天 | 加载大文件优化 |

---

## 📋 实施计划

### Week 1: 关键功能（P0）

#### Day 1: Shader预加载
```csharp
// 1. 在ResLibrary中添加ShaderBook
public ResBook DefaultShaderBook { get; }

// 2. 创建ShaderVariantCollection
// Assets/Resources/Shaders/AllShaders.shadervariants

// 3. 游戏启动时预热
public class GameInit : MonoBehaviour
{
    void Start()
    {
        ESResMaster.PreloadShaders(() =>
        {
            Debug.Log("Shader预热完成");
            // 开始游戏逻辑
        });
    }
}
```

#### Day 1.5: 永不卸载标记
```csharp
// ResPage添加字段
[Serializable]
public class ResPage : PageBase
{
    [LabelText("永不卸载")]
    public bool NeverUnload = false;
    
    public UnityEngine.Object OB;
}

// ResSource检查
public bool ReleaseTheResSource()
{
    if (Page?.NeverUnload == true)
    {
        Debug.Log($"资源 {ResName} 标记为永不卸载");
        return false;
    }
    // ...
}
```

### Week 2: 重要功能（P1）

#### Day 2-3: 资源标签系统
- 实现Tag查询
- 批量加载/卸载API
- 编辑器支持

#### Day 4: 延迟卸载机制
- 实现延迟队列
- Update循环处理
- 测试复用率

### Week 3: 优化功能（P2）

#### Day 5-7: 下载器改进
- 断点续传
- 并发控制
- 自动重试

#### Day 8: 加载优先级
- 优先级队列
- 动态调整
- 测试验证

---

## 📚 参考资料

1. **YooAsset文档**：https://www.yooasset.com/docs/
2. **Unity Shader变体**：https://docs.unity3d.com/Manual/shader-variants.html
3. **URP全局设置**：https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/
4. **AssetBundle最佳实践**：https://docs.unity3d.com/Manual/AssetBundles-BestPractice.html

---

## 🎉 总结

### YooAsset强调Shader/渲染管线的原因：
1. ✅ **Shader变体编译**会导致严重卡顿
2. ✅ **依赖关系复杂**，需要自动管理
3. ✅ **渲染管线资源**卸载会崩溃
4. ✅ 需要**特殊的生命周期管理**

### ES需要立即借鉴的功能：
1. 🔥 **Shader预加载** - 避免运行时卡顿
2. 🔥 **永不卸载标记** - 保护关键资源
3. 🔴 **资源标签系统** - 灵活分组管理
4. 🔴 **延迟卸载** - 提升资源复用率
5. 🟡 **下载器改进** - 断点续传+重试
6. 🟡 **加载优先级** - 优化加载顺序

按照优先级实施，ES系统将达到商业级资源管理水平！
