# ES资源系统测试检查清单

## 🎯 测试准备完成检查

**日期**: 2026-01-29  
**测试环境**: Unity 2021+  
**测试范围**: LoadType系统 + Json构建流程 + Shader预热

---

## ✅ 代码完整性检查

### 1. 核心文件编译状态

| 文件 | 状态 | 说明 |
|------|------|------|
| ESResSource.cs | ✅ 通过 | 枚举+扩展方法 |
| ESResSourceFactory.cs | ✅ 通过 | 工厂类+ShaderVariant+RawFile |
| ESResMaster.cs | ✅ 通过 | 工厂模式+Shader API |
| ESShaderPreloader.cs | ✅ 存在 | Shader自动预热逻辑 |
| GameBootstrapExample.cs | ⚠️ 待重新编译 | Shader API调用 |

**注意**: GameBootstrapExample.cs的编译错误需要Unity重新编译即可解决（API已正确添加到ESResMaster）。

### 2. Shader预热API验证

在ESResMaster.cs中已添加以下静态方法（345-385行）：

```csharp
✅ public static void WarmUpAllShaders(Action onComplete = null)
✅ public static bool IsShadersWarmedUp()  
✅ public static string GetShaderStatistics()
```

**验证方法**:
```csharp
// 在Unity中打开任意脚本，输入以下代码验证智能提示
ESResMaster.IsShadersWarmedUp();
ESResMaster.GetShaderStatistics();
```

### 3. Json数据结构完整性

| Json类型 | 文件位置 | 字段 | 状态 |
|---------|---------|------|------|
| ESResJsonData_AssetsKeys | ESResJsonData.AssetKeys.cs | AssetKeys (List) | ✅ |
| ESResJsonData_ABMetadata | ESResJsonData.ABMetadata.cs | PreToHashes, ABKeys, Dependences | ✅ |
| ESResJsonData_LibIndentity | ESResJsonData.LibIdentity.cs | Version, Platform, BuildTime | ✅ |

**反序列化流程**（ResMaster.Runtime.Download.cs 870-990行）:
```
1. 读取Json文件 → File.ReadAllText()
2. 反序列化 → JsonConvert.DeserializeObject<T>()
3. 注入全局字典 → GlobalAssetKeys.Add() / GlobalABKeys[key] = value
4. 构建加载路径 → BuildLocalABLoadPath()
```

---

## 🧪 测试场景

### 场景1：LoadType工厂模式测试

**目标**: 验证新的工厂模式创建资源源

**测试步骤**:
```csharp
// 1. 创建AssetBundle类型
var abKey = new ESResKey("ui_mainmenu", typeof(AssetBundle));
var abSource = ESResSourceFactory.CreateResSource(abKey, ESResSourceLoadType.AssetBundle);
Assert.IsNotNull(abSource);
Assert.IsInstanceOf<ESABSource>(abSource);

// 2. 创建RawFile类型
var configKey = new ESResKey("config.json", typeof(TextAsset));
var rawSource = ESResSourceFactory.CreateResSource(configKey, ESResSourceLoadType.RawFile);
Assert.IsNotNull(rawSource);
Assert.IsInstanceOf<ESRawFileSource>(rawSource);

// 3. 查询已注册类型
var types = ESResSourceFactory.GetRegisteredTypes();
Debug.Log($"已注册类型数量: {types.Length}");
Assert.IsTrue(types.Length >= 4); // AssetBundle, ABAsset, ShaderVariant, RawFile
```

**预期结果**:
- ✅ 创建成功，无异常抛出
- ✅ 类型正确（ESABSource, ESRawFileSource）
- ✅ 注册类型数量 >= 4

### 场景2：Shader自动预热测试

**目标**: 验证Shader预热自动触发和API调用

**测试步骤**:
```csharp
// 1. 游戏启动时检查初始状态
bool initialState = ESResMaster.IsShadersWarmedUp();
Debug.Log($"初始预热状态: {initialState}");

// 2. 等待自动预热完成
yield return new WaitUntil(() => ESResMaster.IsShadersWarmedUp());

// 3. 获取统计信息
string stats = ESResMaster.GetShaderStatistics();
Debug.Log(stats);

// 4. 验证统计信息包含关键字段
Assert.IsTrue(stats.Contains("加载的AB包"));
Assert.IsTrue(stats.Contains("ShaderVariantCollection"));
Assert.IsTrue(stats.Contains("Shader数量"));
Assert.IsTrue(stats.Contains("变体总数"));
```

**预期结果**:
- ✅ 初始状态为false
- ✅ 自动预热在资源初始化后触发
- ✅ 预热完成后IsShadersWarmedUp()返回true
- ✅ 统计信息包含AB包数量、Shader数量、变体总数

### 场景3：Json加载流程测试

**目标**: 验证Json文件正确反序列化并注入全局字典

**前置条件**:
1. 已构建AB包（运行BuildPipeline）
2. 存在以下Json文件：
   - `[LibFolder]/LibIdentity.json`
   - `[LibFolder]/AssetKeys.json`
   - `[LibFolder]/ABMetadata.json`

**测试步骤**:
```csharp
// 1. 清空全局字典（测试环境）
ESResMaster.GlobalAssetKeys.Clear();
ESResMaster.GlobalABKeys.Clear();
ESResMaster.GlobalABPreToHashes.Clear();

// 2. 触发资源初始化
ESResMaster.Instance.StartCoroutine(ESResMaster.Instance.StartDownload());

// 3. 等待下载完成
yield return new WaitUntil(() => 
    ESResMaster.GlobalDownloadState == ESResGlobalDownloadState.AllReady
);

// 4. 验证全局字典数据
Assert.IsTrue(ESResMaster.GlobalAssetKeys.Count > 0, "AssetKeys未注入");
Assert.IsTrue(ESResMaster.GlobalABKeys.Count > 0, "ABKeys未注入");
Assert.IsTrue(ESResMaster.GlobalABPreToHashes.Count > 0, "Hashes未注入");

// 5. 验证单个Key的完整性
var sampleKey = ESResMaster.GlobalAssetKeys.Values.FirstOrDefault();
Assert.IsNotNull(sampleKey);
Assert.IsFalse(string.IsNullOrEmpty(sampleKey.ResName));
Assert.IsFalse(string.IsNullOrEmpty(sampleKey.ABName));
Assert.IsFalse(string.IsNullOrEmpty(sampleKey.LocalABLoadPath));
Assert.IsNotNull(sampleKey.TargetType);
```

**预期结果**:
- ✅ GlobalAssetKeys包含资源键数据
- ✅ GlobalABKeys包含AB包键数据
- ✅ GlobalABPreToHashes包含哈希映射
- ✅ 每个Key包含完整字段（ResName, ABName, LocalABLoadPath, TargetType）

### 场景4：BuildLocalABLoadPath测试

**目标**: 验证AB包路径构建正确

**测试步骤**:
```csharp
// 1. 模拟库信息
var lib = new RequiredLibrary
{
    FolderName = "GameCore",
    IsRemote = true
};

// 2. 模拟AB资源键
var assetKey = new ESResKey
{
    ResName = "UI_MainMenu_Prefab",
    ABName = "ui_mainmenu",
    SourceLoadType = ESResSourceLoadType.ABAsset
};

// 3. 添加哈希映射
ESResMaster.GlobalABPreToHashes["ui_mainmenu"] = "ui_mainmenu_a1b2c3d4";

// 4. 构建路径
string path = BuildLocalABLoadPath(lib, assetKey);
Debug.Log($"构建路径: {path}");

// 5. 验证路径格式
Assert.IsTrue(path.Contains("GameCore"));
Assert.IsTrue(path.Contains("ui_mainmenu_a1b2c3d4"));
Assert.IsTrue(Path.IsPathRooted(path)); // 绝对路径
```

**预期结果**:
- ✅ 路径包含库文件夹名
- ✅ 路径包含哈希化的AB包名
- ✅ 路径为绝对路径
- ✅ 格式：`[BasePath]/GameCore/ui_mainmenu_a1b2c3d4`

### 场景5：ShaderVariantCollection自动发现

**目标**: 验证系统能自动发现所有ShaderVariantCollection

**前置条件**:
1. 创建至少1个ShaderVariantCollection资源
2. 添加到ResLibrary的ShaderBook
3. 构建AB包

**测试步骤**:
```csharp
// 1. 触发资源初始化（会自动预热Shader）
yield return StartCoroutine(ESResMaster.Instance.StartDownload());

// 2. 等待预热完成
yield return new WaitUntil(() => ESResMaster.IsShadersWarmedUp());

// 3. 获取统计信息
string stats = ESResMaster.GetShaderStatistics();
Debug.Log(stats);

// 4. 验证找到了ShaderVariantCollection
Assert.IsTrue(stats.Contains("ShaderVariantCollection: "));
var collectionCount = ExtractNumber(stats, "ShaderVariantCollection: ");
Assert.IsTrue(collectionCount > 0, "未找到任何ShaderVariantCollection");

// 5. 验证Shader已预热
var shaderCount = ExtractNumber(stats, "Shader数量: ");
var variantCount = ExtractNumber(stats, "变体总数: ");
Assert.IsTrue(shaderCount > 0);
Assert.IsTrue(variantCount > 0);
```

**预期结果**:
- ✅ 自动发现所有ShaderVariantCollection
- ✅ AB包加载成功
- ✅ WarmUp()调用成功
- ✅ 统计信息包含准确的Shader和变体数量

---

## 📋 测试检查点

### 编译阶段

- [ ] 所有.cs文件无编译错误
- [ ] 所有.cs文件无编译警告（除Obsolete警告外）
- [ ] Unity控制台无红色错误
- [ ] GameBootstrapExample.cs智能提示正常

### 运行时阶段

- [ ] ESResMaster实例正确初始化
- [ ] GlobalAssetKeys包含数据
- [ ] GlobalABKeys包含数据
- [ ] GlobalABPreToHashes包含数据
- [ ] Shader预热自动触发
- [ ] IsShadersWarmedUp()从false变为true
- [ ] GetShaderStatistics()返回有效数据

### 功能验证

- [ ] 工厂模式创建资源源成功
- [ ] RawFile类型可正确加载
- [ ] ShaderVariant类型可正确加载
- [ ] Json数据完整注入
- [ ] AB包路径构建正确
- [ ] Shader预热无卡顿

---

## 🐛 常见问题排查

### 问题1：编译错误"未包含定义"

**症状**: 
```
CS0117: "ESResMaster"未包含"IsShadersWarmedUp"的定义
```

**解决方案**:
1. 确认ESResMaster.cs已保存（Ctrl+S）
2. Unity中 Assets > Refresh（Ctrl+R）
3. 清理缓存：Library/ScriptAssemblies删除
4. 重启Unity Editor

### 问题2：Shader预热不触发

**症状**: IsShadersWarmedUp()始终返回false

**排查步骤**:
```csharp
// 1. 检查自动触发代码
// 文件: ResMaster.Runtime.Download.cs (约293行)
GlobalDownloadState = ESResGlobalDownloadState.AllReady;
StartCoroutine(ESShaderPreloader.AutoWarmUpAllShaders(...)); // 这行是否存在？

// 2. 检查GlobalAssetKeys是否包含ShaderVariantCollection
var shaderKeys = ESResMaster.GlobalAssetKeys.Values
    .Where(k => k.TargetType == typeof(ShaderVariantCollection))
    .ToList();
Debug.Log($"找到Shader键数量: {shaderKeys.Count}");

// 3. 检查AB包文件是否存在
foreach (var key in shaderKeys)
{
    string abPath = Path.Combine(
        ESResMaster.DefaultPaths.GetLocalABBasePath(key.LibFolderName),
        key.ABName
    );
    Debug.Log($"AB包路径: {abPath}, 存在: {File.Exists(abPath)}");
}
```

### 问题3：Json加载失败

**症状**: GlobalAssetKeys.Count == 0

**排查步骤**:
```csharp
// 1. 检查Json文件是否存在
string libPath = "[YourLibFolder]";
string assetKeysPath = Path.Combine(libPath, "AssetKeys.json");
Debug.Log($"Json存在: {File.Exists(assetKeysPath)}");

// 2. 手动读取Json验证格式
string json = File.ReadAllText(assetKeysPath);
Debug.Log($"Json内容长度: {json.Length}");
var data = JsonConvert.DeserializeObject<ESResJsonData_AssetsKeys>(json);
Debug.Log($"反序列化Keys数量: {data.AssetKeys.Count}");

// 3. 检查库是否在RequiredLibraries列表中
var libs = ESResMaster.Instance.Settings.RequiredLibraries;
Debug.Log($"需要的库数量: {libs.Count}");
foreach (var lib in libs)
{
    Debug.Log($"库: {lib.FolderName}, 远程: {lib.IsRemote}");
}
```

### 问题4：BuildLocalABLoadPath返回空

**症状**: assetKey.LocalABLoadPath为null或空字符串

**排查步骤**:
```csharp
// 1. 检查GlobalABPreToHashes是否包含映射
Debug.Log($"Hashes数量: {ESResMaster.GlobalABPreToHashes.Count}");
var sampleAB = ESResMaster.GlobalABKeys.Values.FirstOrDefault();
if (sampleAB != null)
{
    bool hasHash = ESResMaster.GlobalABPreToHashes.TryGetValue(
        sampleAB.ResName, 
        out string hashedName
    );
    Debug.Log($"AB: {sampleAB.ResName}, 有哈希: {hasHash}, 哈希名: {hashedName}");
}

// 2. 检查SourceLoadType是否正确
Debug.Log($"LoadType: {assetKey.SourceLoadType}");
```

---

## 📊 性能基准

### 预期性能指标

| 指标 | 目标值 | 测量方法 |
|------|--------|---------|
| Shader预热时间 | < 1秒 | Time.realtimeSinceStartup |
| Json加载时间 | < 500ms | Stopwatch |
| 内存占用（Shader） | 7-30MB | Profiler Memory |
| 启动到Ready | < 3秒 | 总耗时 |

### 性能测试代码

```csharp
using System.Diagnostics;
using UnityEngine;

public class PerformanceTest : MonoBehaviour
{
    IEnumerator Start()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // 1. Json加载
        var jsonStart = stopwatch.ElapsedMilliseconds;
        yield return StartCoroutine(ESResMaster.Instance.StartDownload());
        var jsonEnd = stopwatch.ElapsedMilliseconds;
        Debug.Log($"Json加载耗时: {jsonEnd - jsonStart}ms");
        
        // 2. Shader预热
        var shaderStart = stopwatch.ElapsedMilliseconds;
        yield return new WaitUntil(() => ESResMaster.IsShadersWarmedUp());
        var shaderEnd = stopwatch.ElapsedMilliseconds;
        Debug.Log($"Shader预热耗时: {shaderEnd - shaderStart}ms");
        
        // 3. 总耗时
        stopwatch.Stop();
        Debug.Log($"总启动耗时: {stopwatch.ElapsedMilliseconds}ms");
        
        // 4. 内存占用
        var shaderMemory = Profiler.GetMonoUsedSizeLong();
        Debug.Log($"Shader内存占用: {shaderMemory / (1024 * 1024)}MB");
    }
}
```

---

## ✅ 测试通过标准

### 最低标准（必须全部通过）

1. ✅ 所有核心文件编译通过（0错误）
2. ✅ Json数据正确注入（GlobalAssetKeys > 0）
3. ✅ Shader预热成功触发（IsShadersWarmedUp = true）
4. ✅ AB包路径构建正确（非空且文件存在）
5. ✅ 工厂模式创建资源源成功

### 推荐标准（建议达到）

6. ✅ Shader预热时间 < 1秒
7. ✅ Json加载时间 < 500ms
8. ✅ 无内存泄漏（Profiler验证）
9. ✅ 无Console警告（除Obsolete外）
10. ✅ 所有测试场景通过

---

## 📝 测试报告模板

```markdown
# 测试报告

**测试日期**: 2026-01-29  
**测试人员**: [Your Name]  
**Unity版本**: 2021.3.x  
**平台**: Windows/Mac/iOS/Android

## 测试结果

| 测试场景 | 状态 | 耗时 | 备注 |
|---------|------|------|------|
| LoadType工厂模式 | ✅ | - | - |
| Shader自动预热 | ✅ | 800ms | - |
| Json加载流程 | ✅ | 450ms | - |
| AB包路径构建 | ✅ | - | - |
| Shader自动发现 | ✅ | - | 找到3个Collection |

## 性能数据

- Json加载: 450ms
- Shader预热: 800ms
- 总启动时间: 1.5秒
- Shader内存: 15MB

## 发现的问题

1. [问题描述]
   - 复现步骤: ...
   - 预期结果: ...
   - 实际结果: ...
   - 严重程度: P0/P1/P2

## 结论

测试通过 ✅ / 测试失败 ❌
```

---

**文档版本**: v1.0  
**最后更新**: 2026-01-29  
**下一步**: 运行Unity启动测试
