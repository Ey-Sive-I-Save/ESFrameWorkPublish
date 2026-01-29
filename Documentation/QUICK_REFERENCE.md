# ES资源管理系统 - 快速参考

## 🚀 核心API速查

### 资源加载（强类型）
```csharp
// ✅ 新API - 强类型，有智能提示
var key = new ESResKey("ui_mainmenu", typeof(GameObject));
var res = ESResMaster.ResTable.GetAssetResByKey(key);
var abRes = ESResMaster.ResTable.GetABResByKey(key);
var rawFile = ESResMaster.ResTable.GetRawFileResByKey(key);

// ❌ 旧API - 仍可用但不推荐
var res = ESResMaster.ResTable.GetAssetResByKey((object)key);
```

### 资源检查
```csharp
// O(1)复杂度快速检查
if (ESResMaster.ResTable.ContainsAsset(myKey))
{
    // 资源已加载
}

if (ESResMaster.ResTable.ContainsAB(abKey))
{
    // AB包已加载
}
```

### 性能统计
```csharp
var stats = ESResMaster.ResTable.GetStatistics();
Debug.Log($"资产:{stats.assetCount} AB:{stats.abCount} RawFile:{stats.rawFileCount} 引用:{stats.totalRefCount}");
```

---

## 🔒 资源加密

### 初始化加密器
```csharp
// 方式1: XOR加密（快速）
ESResEncryptionHelper.SetEncryptor(new ESXOREncryptor("MyKey123"));

// 方式2: AES加密（安全）
ESResEncryptionHelper.SetEncryptor(new ESAESEncryptor("SecretKey2026!", "IV1234567890abcd"));

// 方式3: 不加密（测试）
ESResEncryptionHelper.SetEncryptor(new ESNoEncryptor());
```

### 加密文件
```csharp
// 单个文件加密
ESResEncryptionHelper.EncryptFile(
    "Assets/Build/ui.ab",
    "Encrypted/ui.encrypted",
    "CustomKey"
);

// 批量加密
var files = Directory.GetFiles("Assets/Build", "*.ab");
foreach (var file in files)
{
    string output = file.Replace("Build", "Build_Encrypted");
    ESResEncryptionHelper.EncryptFile(file, output);
}
```

### 解密和验证
```csharp
// 解密文件
byte[] data = ESResEncryptionHelper.DecryptFile("Encrypted/ui.encrypted");

// 验证完整性
bool valid = ESResEncryptionHelper.VerifyFileIntegrity(
    "Encrypted/ui.encrypted",
    "expected_md5_hash"
);
```

---

## 📥 下载控制

### 正常启动
```csharp
// 使用本地缓存，只下载更新
ESResMaster.Instance.GameInit_ResCompareAndDownload();
```

### 强制重新下载
```csharp
// 忽略本地缓存，全部重新下载
ESResMaster.Instance.GameInit_ResCompareAndDownload(
    forceRedownload: true,
    verifyIntegrity: true
);
```

### 资源修复
```csharp
[Button("修复资源")]
void RepairResources()
{
    // 1. 清除本地
    Directory.Delete(ESResMaster.DefaultPaths.LocalABBasePath, true);
    
    // 2. 强制下载
    ESResMaster.Instance.GameInit_ResCompareAndDownload(true, true);
}
```

---

## 🔧 常见问题

### Q1: 编译错误 "TryRegisterRawFileRes重复定义"
**A**: 已修复，删除了重复的方法定义

### Q2: 如何启用加密？
**A**: 
```csharp
// Awake中初始化
void Awake()
{
    ESResEncryptionHelper.SetEncryptor(new ESAESEncryptor("YourKey"));
}
```

### Q3: 如何检测资源是否存在？
**A**:
```csharp
bool exists = ESResMaster.ResTable.ContainsAsset(key);
```

### Q4: 如何获取内存统计？
**A**:
```csharp
var stats = ESResMaster.ResTable.GetStatistics();
```

### Q5: 强制下载会删除本地文件吗？
**A**: 不会删除，只是跳过版本对比直接下载覆盖

---

## ⚡ 性能建议

### 加密选择
- **配置文件**: AES加密
- **大型AB包**: XOR加密（性能优先）
- **非敏感资源**: 不加密

### 并行加载
```csharp
// 批量添加资源到同一Loader
var loader = new ESResLoader();
loader.AddAsset2LoadByPathSourcer("ui/button.prefab");
loader.AddAsset2LoadByPathSourcer("ui/panel.prefab");
loader.AddAsset2LoadByPathSourcer("ui/icon.png");

// 一次性并行加载
loader.LoadAllAsync(() => Debug.Log("全部完成"));
```

### 引用计数管理
```csharp
// 获取资源（引用+1）
var res = ESResMaster.Instance.GetResSourceByKey(key, loadType);

// 使用完毕释放（引用-1）
ESResMaster.Instance.ReleaseResHandle(key, loadType, unloadWhenZero: true);
```

---

## 📌 代码模板

### 完整加载流程
```csharp
using ES;
using UnityEngine;

public class ResourceLoader : MonoBehaviour
{
    void Start()
    {
        // 1. 初始化加密
        ESResEncryptionHelper.SetEncryptor(new ESAESEncryptor("MyKey"));
        
        // 2. 启动下载
        ESResMaster.Instance.GameInit_ResCompareAndDownload(
            forceRedownload: false,
            verifyIntegrity: true
        );
        
        // 3. 等待完成
        StartCoroutine(WaitForReady());
    }
    
    IEnumerator WaitForReady()
    {
        while (ESResMaster.Instance.GlobalDownloadState != ESResGlobalDownloadState.AllReady)
        {
            yield return null;
        }
        
        Debug.Log("资源准备完成！");
        LoadGameAssets();
    }
    
    void LoadGameAssets()
    {
        var loader = new ESResLoader();
        loader.AddAsset2LoadByPathSourcer("Prefabs/Player.prefab");
        loader.AddAsset2LoadByPathSourcer("Textures/UI.png");
        
        loader.LoadAllAsync(() =>
        {
            Debug.Log("游戏资源加载完成");
            StartGame();
        });
    }
    
    void StartGame()
    {
        // 游戏逻辑
    }
}
```

---

## 🎯 最佳实践

### ✅ 推荐
```csharp
// 使用强类型API
var res = ESResMaster.ResTable.GetAssetResByKey(esResKey);

// 使用ContainsAsset检查存在
if (ESResMaster.ResTable.ContainsAsset(key)) { }

// 批量加载用同一Loader
loader.AddAsset2LoadByPathSourcer("a.prefab");
loader.AddAsset2LoadByPathSourcer("b.prefab");
loader.LoadAllAsync();

// 释放时指定unloadWhenZero
ESResMaster.Instance.ReleaseResHandle(key, loadType, unloadWhenZero: true);
```

### ❌ 避免
```csharp
// 不要使用object类型参数（失去类型安全）
var res = ESResMaster.ResTable.GetAssetResByKey((object)key);

// 不要频繁GetStatistics（有锁开销）
for (int i = 0; i < 1000; i++)
{
    var stats = ESResMaster.ResTable.GetStatistics(); // ❌ 每帧调用
}

// 不要忘记释放引用
var res = ESResMaster.Instance.GetResSourceByKey(key, loadType);
// ... 使用资源
// ❌ 忘记调用 ReleaseResHandle -> 内存泄漏
```

---

**最后更新**: 2026年1月29日  
**适用版本**: ESFramework v2.0+
