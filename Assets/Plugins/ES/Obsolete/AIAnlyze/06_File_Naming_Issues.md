# ES 框架文件命名缺陷分析

> **检查范围**：Assets/Plugins/ES 核心框架代码  
> **检查维度**：命名规范、一致性、可读性、Unity最佳实践  
> **参考标准**：Microsoft C# 编码规范、Unity命名约定

---

## 一、命名规范缺陷汇总

### 1.1 中划线 (Hyphen) 使用问题 ❌

**问题文件**：
| 文件名 | 位置 | 问题 | 建议修改 |
|--------|------|------|----------|
| `Poolable-Define.cs` | 0_Stand/Stand_Tools | 中划线在C#中不规范 | `PoolableDefine.cs` 或 `IPoolable.cs` |
| `ContextPool-Define.cs` | 1_Design | 同上 | `ContextPoolDefine.cs` |
| `CacherPool-Define.cs` | 1_Design | 同上 | `CacherPoolDefine.cs` |
| `LinkRecievePool.cs` | 1_Design/Link | 拼写错误（Recieve→Receive） | `LinkReceivePool.cs` |

**问题说明**：
- C# 文件名应使用 PascalCase（大驼峰命名）
- 中划线 `-` 在文件系统中可能引起歧义（如命令行工具将其解析为参数）
- Unity官方推荐：单词间无分隔符或使用下划线（但下划线也非最佳）

**影响**：
- 🟡 **中等**：不影响编译，但降低代码可读性和专业性
- ⚠️ **潜在风险**：某些构建工具可能对特殊字符敏感

---

### 1.2 下划线 (Underscore) 滥用问题 ⚠️

**问题文件夹**：
| 文件夹名 | 位置 | 问题 | 建议修改 |
|----------|------|------|----------|
| `BaseDefine_RunTime` | 0_Stand | 混合下划线与PascalCase | `BaseDefineRuntime` 或拆分文件夹 |
| `Stand_Tools` | 0_Stand | 同上 | `StandTools` 或 `Tools` |
| `Link_Container` | 1_Design/Link | 同上 | `LinkContainer` 或 `Containers` |

**问题说明**：
- 下划线通常用于私有字段（如 `_privateField`），不应用于类型名
- Unity推荐：命名空间、文件夹、类名使用纯 PascalCase

**特殊情况**：
- ✅ **Editor专用部分类**：`ESResMaster.Editor.cs` 使用点号分隔是可接受的
- ✅ **测试文件**：`MyClassTests.cs` 或 `MyClass.Tests.cs` 都可以

---

### 1.3 拼写错误 ❌

| 文件名 | 错误 | 正确拼写 | 影响 |
|--------|------|---------|------|
| `LinkRecievePool.cs` | Recieve | **Receive** | 搜索困难、外部开发者困惑 |
| `Singal_Dirty` (字段名) | Singal | **Signal** | 代码审查时易被忽略 |

**问题说明**：
- 拼写错误会导致：
  - API文档中出现错误术语
  - 开发者搜索 "Receive" 找不到相关类
  - 团队协作时产生歧义

**建议工具**：
- 使用 Visual Studio / Rider 的拼写检查插件
- 配置 Code Spell Checker 扩展

---

### 1.4 前缀/后缀不一致 🟡

**接口命名**：
| 文件名 | 问题 | 说明 |
|--------|------|------|
| `IESHosting.cs` | ✅ 正确 | 接口以 `I` 开头 |
| `IESModule.cs` | ✅ 正确 | 同上 |
| `IPoolable.cs` | ✅ 正确 | 同上 |

**基类命名**：
| 文件名 | 问题 | 建议 |
|--------|------|------|
| `BaseESHosting.cs` | ⚠️ 混乱 | 基类应为抽象类时用 `Abstract` 前缀 |
| `BaseESModule.cs` | ⚠️ 混乱 | 若是具体实现，应去掉 `Base` |

**说明**：
- `Base` 前缀表示"可被继承的基类"，但：
  - 如果是抽象类 → 建议 `AbstractESModule` 或保持 `BaseESModule`
  - 如果是具体实现 → 建议去掉 `Base`（如 `DefaultESModule`）

**当前 BaseESModule 分析**：
```csharp
// BaseESModule.cs
public class BaseESModule : IESModule  // 非abstract，可实例化
{
    // ...实现代码
}
```
- ✅ **当前命名合理**（因为它是可实例化的基类）
- ⚠️ **改进建议**：如果子类必须override某些方法，应改为 `abstract class`

---

### 1.5 泛型类命名 ✅

**正确示例**：
| 文件名 | 命名 | 说明 |
|--------|------|------|
| `Pool<T>.cs` | ✅ 规范 | 泛型参数使用单字母 `T` |
| `IESModule<Host>.cs` | ✅ 清晰 | 泛型参数使用有意义的名称 |
| `SafeKeyGroup<TKey, TValue>.cs` | ✅ 规范 | 多个泛型参数加 `T` 前缀 |

**说明**：
- 单个泛型参数 → `T`
- 多个泛型参数 → `TKey`, `TValue`, `TItem`
- 约束特定类型 → `THost where THost : IESHosting`

---

## 二、Editor 文件命名规范

### 2.1 Inspector / Window 命名 ✅

**正确示例**：
| 文件名 | 说明 |
|--------|------|
| `ESTrackViewWindow.cs` | Editor Window，后缀 `Window` |
| `ESMenuTreeWindow.cs` | 同上 |
| `ESDevManagementWindow_V2.cs` | 版本号后缀可接受 |

**建议改进**：
```
ESDevManagementWindow_V2.cs  →  ESDevManagementWindowV2.cs
（避免下划线，直接PascalCase）
```

---

### 2.2 Custom Editor 命名 ⚠️

**问题**：未发现明确的 CustomEditor 文件（可能混在其他文件中）

**建议规范**：
```csharp
// 为 ESResMaster 创建的 Inspector
文件名：ESResMasterEditor.cs  或  ESResMasterInspector.cs

[CustomEditor(typeof(ESResMaster))]
public class ESResMasterEditor : Editor
{
    // ...
}
```

**后缀选择**：
- `Editor` 后缀 → Unity官方推荐（如 `GameObjectEditor`）
- `Inspector` 后缀 → 也可接受，更明确表示是Inspector面板

---

## 三、Partial Class 命名规范

### 3.1 当前实践 ⚠️

**问题示例**：
```
ESResMaster.cs                # Runtime部分
ESResMaster_BuildPart.cs      # Editor部分（假设）
```

**问题**：
- 下划线分隔不符合C#规范
- 无法通过文件名快速识别哪个是Runtime/Editor

---

### 3.2 推荐实践 ✅

**方案1：使用点号分隔（推荐）**
```
ESResMaster.cs                # Runtime部分
ESResMaster.Editor.cs         # Editor部分
```

**方案2：使用后缀**
```
ESResMaster.cs                # Runtime部分
ESResMasterEditor.cs          # Editor部分（但这会产生两个class定义，不推荐）
```

**说明**：
- 方案1 是 Unity 和 C# 社区的标准实践
- 文件系统会自动将相关文件分组显示
- 点号分隔清晰表示"这是同一个类的不同部分"

---

## 四、SO (ScriptableObject) 命名规范

### 4.1 当前实践 ✅

**正确示例**：
| 文件名 | 命名 | 说明 |
|--------|------|------|
| `ESResLibrary.cs` | ✅ 清晰 | Library 明确表示是资源库 |
| `ESResBook.cs` | ✅ 清晰 | Book 明确表示是资源书 |
| `ESResPage.cs` | ✅ 清晰 | Page 明确表示是资源页 |

---

### 4.2 建议增强 🟢

**可选优化**：添加后缀明确标识SO
```
ESResLibrary.cs  →  ESResLibrarySO.cs  （可选）
ESResBook.cs     →  ESResBookSO.cs     （可选）
```

**理由**：
- 某些团队习惯用 `SO` 后缀标识 ScriptableObject
- 但当前命名已足够清晰，无需强制修改

---

## 五、字段/属性命名规范

### 5.1 私有字段 ✅

**正确示例**（推测）：
```csharp
public class MyClass
{
    private int _myField;           // ✅ 下划线前缀
    private string m_myOtherField;  // ✅ Unity传统风格（m_前缀）
}
```

**说明**：
- `_` 前缀 → Microsoft C# 推荐
- `m_` 前缀 → Unity 传统风格
- **两者选其一并保持一致**

---

### 5.2 公共属性 ❌ 发现问题

**问题代码**（假设）：
```csharp
public class IESModule
{
    public bool Signal_IsActiveAndEnable; // ❌ 下划线在公共字段中不规范
    public bool Signal_HasSubmit;         // ❌ 同上
}
```

**问题说明**：
- 公共字段/属性应使用纯 PascalCase
- 下划线应仅用于私有字段

**建议修改**：
```csharp
// 方案1：去掉下划线
public bool SignalIsActiveAndEnable;
public bool SignalHasSubmit;

// 方案2：使用属性（推荐）
public bool IsActiveAndEnabled { get; private set; }
public bool HasSubmitted { get; private set; }

// 方案3：如果"Signal"是特殊前缀标记，改为命名空间
namespace ES.Signals
{
    public bool IsActiveAndEnable;
    public bool HasSubmit;
}
```

---

### 5.3 常量命名 ⚠️

**未发现问题，但建议检查**：
```csharp
// ✅ 正确示例
public const int MAX_POOL_SIZE = 12;        // 常量用全大写+下划线
public const string DEFAULT_NAME = "ES";    // 同上

// ❌ 错误示例（如果存在）
public const int maxPoolSize = 12;          // 应改为全大写
```

---

## 六、命名空间规范

### 6.1 当前命名空间 ✅

**正确示例**：
```csharp
namespace ES
{
    public class ESResMaster { }
}
```

**说明**：
- 简短的根命名空间 `ES` 是合理的（类似 Unity 的 `UnityEngine`）
- 避免过深的嵌套（如 `ES.Framework.Runtime.Systems.Resource.Management` 过于冗长）

---

### 6.2 建议增强 🟢

**可选优化**：为子系统添加命名空间
```csharp
namespace ES.Runtime.Resource
{
    public class ESResMaster { }
}

namespace ES.Runtime.Link
{
    public interface IReceiveLink<T> { }
}

namespace ES.Editor.Windows
{
    public class ESTrackViewWindow { }
}
```

**好处**：
- 避免类名冲突（如多个系统都有 `Manager` 类）
- 清晰的代码组织
- 支持 `using ES.Runtime.Resource;` 简化引用

---

## 七、修复优先级

### P0 - 立即修复（影响专业性）
1. **拼写错误**：
   - `LinkRecievePool.cs` → `LinkReceivePool.cs`
   - 代码中的 `Singal_Dirty` → `Signal_Dirty`

2. **中划线文件名**：
   - `Poolable-Define.cs` → `IPoolable.cs`
   - `ContextPool-Define.cs` → `ContextPoolDefine.cs`
   - `CacherPool-Define.cs` → `CacherPoolDefine.cs`

---

### P1 - 本周修复（提升可维护性）
3. **下划线文件夹**：
   - `BaseDefine_RunTime` → `BaseDefineRuntime`
   - `Stand_Tools` → `StandTools`

4. **Partial Class 命名**：
   - `ESResMaster_BuildPart.cs` → `ESResMaster.Editor.cs`

5. **公共字段下划线**：
   - `Signal_IsActiveAndEnable` → `IsActiveAndEnabled`（或保持但统一）

---

### P2 - 可选优化（长期改进）
6. **添加命名空间**：
   - 为各子系统添加独立命名空间

7. **SO后缀**：
   - `ESResLibrary` → `ESResLibrarySO`（可选）

---

## 八、自动化检查工具

### 8.1 命名规范检查脚本

```csharp
// Editor/Tools/NamingConventionChecker.cs
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;

public class NamingConventionChecker
{
    [MenuItem("ES/Tools/Check Naming Conventions")]
    public static void CheckNamingConventions()
    {
        var issues = new List<string>();
        
        // 检查1：中划线文件名
        var allScripts = Directory.GetFiles("Assets/Plugins/ES", "*.cs", SearchOption.AllDirectories);
        foreach (var file in allScripts)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Contains("-"))
            {
                issues.Add($"❌ Hyphen in filename: {file}");
            }
        }
        
        // 检查2：下划线文件夹
        var allFolders = Directory.GetDirectories("Assets/Plugins/ES", "*", SearchOption.AllDirectories);
        foreach (var folder in allFolders)
        {
            var folderName = Path.GetFileName(folder);
            if (folderName.Contains("_") && !folderName.StartsWith("0_") && !folderName.StartsWith("1_"))
            {
                issues.Add($"⚠️ Underscore in folder name: {folder}");
            }
        }
        
        // 检查3：拼写错误（简单检查）
        var commonMisspellings = new Dictionary<string, string>
        {
            { "Recieve", "Receive" },
            { "Singal", "Signal" }
        };
        
        foreach (var file in allScripts)
        {
            var content = File.ReadAllText(file);
            foreach (var kv in commonMisspellings)
            {
                if (content.Contains(kv.Key))
                {
                    issues.Add($"❌ Possible typo '{kv.Key}' (should be '{kv.Value}'): {file}");
                }
            }
        }
        
        // 输出结果
        if (issues.Count == 0)
        {
            Debug.Log("✅ No naming convention issues found!");
        }
        else
        {
            Debug.LogWarning($"Found {issues.Count} naming issues:\n" + string.Join("\n", issues));
        }
    }
}
#endif
```

---

### 8.2 批量重命名工具

```csharp
// Editor/Tools/FileRenamer.cs
#if UNITY_EDITOR
public class FileRenamer : EditorWindow
{
    private string searchPattern = "-";
    private string replaceWith = "";
    
    [MenuItem("ES/Tools/Batch Rename Files")]
    public static void ShowWindow()
    {
        GetWindow<FileRenamer>("Batch Rename");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Find and Replace in Filenames", EditorStyles.boldLabel);
        
        searchPattern = EditorGUILayout.TextField("Search Pattern:", searchPattern);
        replaceWith = EditorGUILayout.TextField("Replace With:", replaceWith);
        
        if (GUILayout.Button("Preview Changes"))
        {
            PreviewRenames();
        }
        
        if (GUILayout.Button("Apply Renames"))
        {
            ApplyRenames();
        }
    }
    
    private void PreviewRenames()
    {
        var files = Directory.GetFiles("Assets/Plugins/ES", "*" + searchPattern + "*.cs", SearchOption.AllDirectories);
        Debug.Log($"Will rename {files.Length} files:");
        foreach (var file in files)
        {
            var newName = file.Replace(searchPattern, replaceWith);
            Debug.Log($"  {file} → {newName}");
        }
    }
    
    private void ApplyRenames()
    {
        var files = Directory.GetFiles("Assets/Plugins/ES", "*" + searchPattern + "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var newName = file.Replace(searchPattern, replaceWith);
            AssetDatabase.MoveAsset(file, newName);
        }
        AssetDatabase.Refresh();
        Debug.Log($"✅ Renamed {files.Length} files successfully!");
    }
}
#endif
```

---

## 九、总结

### 主要问题
1. ❌ **拼写错误**：`Recieve` → `Receive`（影响搜索和API一致性）
2. ❌ **中划线使用**：`Poolable-Define.cs` 不符合C#规范
3. ⚠️ **下划线滥用**：文件夹和公共字段中使用下划线

### 改进效果
- **修复后**：代码专业性提升，符合C#和Unity社区规范
- **维护性**：统一命名风格降低团队协作成本
- **可读性**：清晰的命名让新开发者快速理解代码结构

### 推荐行动
1. **立即**：修复拼写错误和中划线文件名（P0）
2. **本周**：统一下划线使用规范（P1）
3. **长期**：引入自动化检查工具（P2）

---

**文档版本**：v2.0  
**检查日期**：2026-01-16  
**待修复文件数**：约15-20个
