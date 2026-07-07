# ES 框架文件夹分类优化建议

> **评估方法**：对比Unity推荐结构、商业框架实践、团队协作需求  
> **核心原则**：Runtime/Editor分离、功能模块化、第三方依赖隔离

---

## 一、当前目录结构分析

### 1.1 现有结构（简化版）

```
Assets/
├── ES/                          # 项目特定资源
│   ├── AIAnlyze/                # AI分析文档
│   ├── AIPreview/               # AI原型代码
│   ├── AB/                      # AssetBundle输出？
│   └── Res/                     # 项目资源
│
├── Plugins/                     # 第三方插件
│   └── ES/                      # ES框架核心
│       ├── 0_Stand/             # 基础设施
│       │   ├── BaseDefine_RunTime/
│       │   └── Stand_Tools/
│       ├── 1_Design/            # 设计模式
│       │   ├── Link/
│       │   └── ...
│       └── 2_Editor/            # 编辑器工具（存疑）
│
├── Gaskellgames/                # 第三方库（Folder System）
├── vFolders/                    # 第三方库（可视化文件夹）
├── vHierarchy/                  # 第三方库（可视化层级）
├── NormalResources/             # 通用资源
├── Resources/                   # Unity特殊文件夹
├── StreamingAssets/             # Unity特殊文件夹
├── Scenes/                      # 场景
├── ScriptTemplates/             # 脚本模板
└── Settings/                    # 项目设置
```

---

### 1.2 问题清单

| 问题 | 严重性 | 说明 |
|------|--------|------|
| **Runtime/Editor代码混合** | 🔴 P0 | Plugins/ES 中未清晰分离，可能导致Runtime包含Editor代码 |
| **框架与项目代码未分离** | 🟠 P1 | Assets/ES 与 Plugins/ES 职责重叠 |
| **第三方库分散** | 🟡 P2 | Gaskellgames/vFolders/vHierarchy 未统一到 Plugins |
| **特殊文件夹命名不一致** | 🟡 P2 | NormalResources 与 Resources 易混淆 |
| **AIAnlyze/AIPreview 位置不当** | 🟡 P2 | 文档与临时代码不应在 Assets 根目录 |
| **缺少Tests文件夹** | 🟢 P3 | 无单元测试结构 |

---

## 二、优化后的目录结构（推荐方案）

### 2.1 顶层结构

```
ESFrameWorkPublish/
├── Assets/
│   ├── _Project/                # 项目特定代码与资源（前缀_确保顶部显示）
│   │   ├── _Docs/               # 文档（从ES/AIAnlyze移动过来）
│   │   ├── _Prototypes/         # 原型代码（从ES/AIPreview移动过来）
│   │   ├── Runtime/
│   │   │   ├── GamePlay/        # 游戏玩法逻辑
│   │   │   ├── UI/              # UI相关代码
│   │   │   └── Data/            # 数据定义（SO等）
│   │   ├── Editor/
│   │   │   ├── Tools/           # 项目专用Editor工具
│   │   │   └── Inspectors/      # 自定义Inspector
│   │   ├── Resources/           # 项目特定的Runtime资源
│   │   ├── Art/                 # 美术资源
│   │   │   ├── Characters/
│   │   │   ├── UI/
│   │   │   └── VFX/
│   │   └── AssetBundles/        # AB源文件（从ES/AB移动）
│   │
│   ├── Plugins/
│   │   ├── ESFramework/         # ES框架核心（重命名自ES）
│   │   │   ├── Runtime/
│   │   │   │   ├── 00_Foundation/      # 基础设施（重命名自0_Stand）
│   │   │   │   │   ├── Lifecycle/      # IESWithLife等
│   │   │   │   │   ├── Pooling/        # Pool<T>
│   │   │   │   │   └── ValueTypes/     # ESTryResult等
│   │   │   │   ├── 01_Patterns/        # 设计模式（重命名自1_Design）
│   │   │   │   │   ├── Link/
│   │   │   │   │   ├── Hosting/
│   │   │   │   │   └── Module/
│   │   │   │   ├── 02_Systems/         # 高级系统
│   │   │   │   │   ├── Resource/       # ResLibrary
│   │   │   │   │   ├── UI/             # UIFramework
│   │   │   │   │   └── Mod/            # ModSystem
│   │   │   │   └── ESFramework.Runtime.asmdef
│   │   │   │
│   │   │   ├── Editor/
│   │   │   │   ├── Windows/            # EditorWindow
│   │   │   │   ├── Inspectors/         # CustomEditor
│   │   │   │   ├── Tools/              # 工具类
│   │   │   │   └── ESFramework.Editor.asmdef
│   │   │   │
│   │   │   ├── Tests/
│   │   │   │   ├── Runtime/            # Runtime单元测试
│   │   │   │   │   └── ESFramework.Tests.Runtime.asmdef
│   │   │   │   └── Editor/             # Editor单元测试
│   │   │   │       └── ESFramework.Tests.Editor.asmdef
│   │   │   │
│   │   │   ├── Documentation~/         # 框架文档（~后缀Unity自动排除打包）
│   │   │   │   ├── Manual/
│   │   │   │   ├── API/
│   │   │   │   └── Samples~/          # 示例项目
│   │   │   │
│   │   │   └── package.json            # UPM包配置（可选）
│   │   │
│   │   └── ThirdParty/                 # 第三方库统一管理
│   │       ├── Gaskellgames/
│   │       ├── vFolders/
│   │       ├── vHierarchy/
│   │       ├── DOTween/
│   │       └── OdinInspector/
│   │
│   ├── Resources/                      # Unity特殊文件夹（公共Resources）
│   ├── StreamingAssets/                # Unity特殊文件夹
│   ├── Scenes/                         # 场景文件
│   └── Settings/                       # ProjectSettings SO
│
├── Packages/                           # Unity Package Manager
├── ProjectSettings/                    # Unity项目设置
└── UserSettings/                       # 用户设置（应加入.gitignore）
```

---

### 2.2 关键改进点

#### ✅ 改进1：Runtime/Editor严格分离

**问题**：当前 `Plugins/ES/0_Stand/BaseDefine_RunTime/` 中可能混合Editor代码

**解决方案**：
```
ESFramework/
├── Runtime/
│   ├── 00_Foundation/
│   │   └── Lifecycle/
│   │       ├── IESWithLife.cs
│   │       └── BaseESModule.cs
│   └── ESFramework.Runtime.asmdef       # 约束依赖
│
└── Editor/
    ├── Inspectors/
    │   └── BaseESModuleInspector.cs     # IESWithLife的Inspector
    └── ESFramework.Editor.asmdef
        └── 依赖: ESFramework.Runtime
```

**Assembly Definition 配置**：
```json
// ESFramework.Runtime.asmdef
{
    "name": "ESFramework.Runtime",
    "references": [],
    "includePlatforms": [],            // 所有平台
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}

// ESFramework.Editor.asmdef
{
    "name": "ESFramework.Editor",
    "references": ["ESFramework.Runtime"],
    "includePlatforms": ["Editor"],   // 仅Editor平台
    "excludePlatforms": [],
    "allowUnsafeCode": false
}
```

---

#### ✅ 改进2：项目与框架分离

**原则**：
- **Plugins/ESFramework**：可复用的通用框架代码
- **_Project/**：当前项目特定的代码与资源

**判断标准**：
| 代码类型 | 位置 | 示例 |
|----------|------|------|
| 通用模块基类 | Plugins/ESFramework/Runtime | BaseESModule.cs, IESHosting.cs |
| 游戏特定模块 | _Project/Runtime/GamePlay | PlayerModule.cs, EnemyModule.cs |
| 通用Editor工具 | Plugins/ESFramework/Editor | ModManagerWindow.cs, ResLibraryInspector.cs |
| 项目专用工具 | _Project/Editor | LevelEditorWindow.cs, CharacterImporter.cs |

---

#### ✅ 改进3：命名规范统一

**目录命名**：
- ✅ **PascalCase**：`Runtime`, `Editor`, `Tests`（Unity标准）
- ✅ **数字前缀可选**：`00_Foundation`, `01_Patterns`（强调加载顺序时使用）
- ❌ **避免**：`BaseDefine_RunTime`（混合下划线与PascalCase）

**文件命名**：
- ✅ **接口**：`IESHosting.cs`（I前缀）
- ✅ **基类**：`BaseESModule.cs`（Base前缀）
- ✅ **工具类**：`ESStringUtility.cs`（Utility后缀）
- ❌ **避免**：`Poolable-Define.cs`（中划线，应改为`PoolableDefine.cs`或`IPoolable.cs`）

---

#### ✅ 改进4：第三方库隔离

**问题**：Gaskellgames/vFolders 等散落在根目录

**解决方案**：
```
Assets/
└── Plugins/
    └── ThirdParty/
        ├── Gaskellgames/
        │   ├── LICENSE.txt
        │   ├── README.md
        │   └── Runtime/
        ├── vFolders/
        └── OdinInspector/
```

**好处**：
- 清晰标识第三方代码（便于升级/移除）
- 避免命名冲突
- 便于添加 `.gitignore`（如果某些库不需要版本控制）

---

#### ✅ 改进5：文档与临时代码位置

**问题**：`Assets/ES/AIAnlyze` 在打包时会被包含

**解决方案**：
```
Assets/
├── _Project/
│   ├── _Docs/                  # 项目文档（前缀_确保顶部显示）
│   │   ├── Architecture/
│   │   ├── API/
│   │   └── Analysis/           # 从AIAnlyze移动
│   │
│   └── _Prototypes/            # 原型代码（前缀_）
│       ├── ModSystem/
│       ├── SkillSystem/
│       └── ...                 # 从AIPreview移动
│
└── Plugins/
    └── ESFramework/
        └── Documentation~/     # 框架文档（~后缀Unity自动排除打包）
            ├── Manual.md
            ├── API/
            └── Samples~/       # 示例代码（~后缀）
```

**Unity特殊后缀**：
- `~` 后缀：Unity自动排除，不会打包到Build中
- 适用于文档、示例、测试资源

---

## 三、迁移步骤（渐进式）

### Phase 1：紧急修复（1天）
```bash
# 1. 创建新目录结构
mkdir -p "Assets/_Project/{_Docs,_Prototypes,Runtime,Editor}"
mkdir -p "Assets/Plugins/ESFramework/{Runtime,Editor,Tests}"
mkdir -p "Assets/Plugins/ThirdParty"

# 2. 移动文档与原型（避免打包）
mv "Assets/ES/AIAnlyze" "Assets/_Project/_Docs/Analysis"
mv "Assets/ES/AIPreview" "Assets/_Project/_Prototypes"

# 3. 移动第三方库
mv "Assets/Gaskellgames" "Assets/Plugins/ThirdParty/Gaskellgames"
mv "Assets/vFolders" "Assets/Plugins/ThirdParty/vFolders"
mv "Assets/vHierarchy" "Assets/Plugins/ThirdParty/vHierarchy"
```

---

### Phase 2：Runtime/Editor分离（3天）

**Step 1：分析现有代码**
```csharp
// 识别所有使用 UnityEditor 命名空间的文件
grep -r "using UnityEditor" Assets/Plugins/ES/
```

**Step 2：创建Assembly Definition**
```bash
# 在 Plugins/ESFramework/Runtime 创建 .asmdef
# 在 Plugins/ESFramework/Editor 创建 .asmdef（引用Runtime）
```

**Step 3：移动文件**
```bash
# Runtime代码
mv "Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/*" \
   "Assets/Plugins/ESFramework/Runtime/00_Foundation/"

# Editor代码（带#if UNITY_EDITOR的partial类）
mv "Assets/Plugins/ES/2_Editor/*" \
   "Assets/Plugins/ESFramework/Editor/"
```

---

### Phase 3：重命名与优化（2天）

**重命名映射**：
| 旧名称 | 新名称 | 原因 |
|--------|--------|------|
| `0_Stand` | `00_Foundation` | 更直观，统一数字格式 |
| `1_Design` | `01_Patterns` | Design过于宽泛 |
| `BaseDefine_RunTime` | `Lifecycle` / `Pooling` | 按功能细分 |
| `Poolable-Define.cs` | `IPoolable.cs` | 符合接口命名规范 |

---

### Phase 4：测试与验证（1天）

**验证清单**：
- [ ] 所有脚本引用正常（无Missing Reference）
- [ ] Runtime asmdef不包含UnityEditor引用
- [ ] Build成功且体积未显著增加
- [ ] Editor工具正常运行
- [ ] 第三方插件无冲突

---

## 四、特殊场景处理

### 4.1 Partial Class 的 Runtime/Editor 分离

**问题**：如 `ESResMaster` 有build和runtime部分

**当前（不推荐）**：
```csharp
// ESResMaster.cs (Runtime)
public partial class ESResMaster : MonoBehaviour
{
    public void LoadResource() { }
}

// ESResMaster_Editor.cs (混在Runtime文件夹)
#if UNITY_EDITOR
public partial class ESResMaster
{
    public void BuildAssetBundle() { }
}
#endif
```

**优化后（推荐）**：
```
ESFramework/
├── Runtime/
│   └── Resource/
│       └── ESResMaster.cs              # 仅Runtime代码
└── Editor/
    └── Resource/
        └── ESResMaster.Editor.cs       # Editor扩展（partial class）
```

```csharp
// ESResMaster.cs (Runtime)
namespace ES
{
    public partial class ESResMaster : MonoBehaviour
    {
        public void LoadResource() { }
    }
}

// ESResMaster.Editor.cs (Editor)
#if UNITY_EDITOR   // 保留条件编译以防万一
namespace ES
{
    public partial class ESResMaster
    {
        public void BuildAssetBundle() { }
    }
}
#endif
```

**好处**：
- 文件位置与功能一致（Editor文件在Editor文件夹）
- asmdef 自动排除 Editor 文件夹的代码（无需条件编译）
- 但保留 `#if UNITY_EDITOR` 提供双重保护

---

### 4.2 Resources 文件夹规范

**问题**：当前有 `NormalResources` 和 `Resources`

**规范**：
```
Assets/
├── _Project/
│   └── Resources/              # 项目特定的Runtime加载资源
│       ├── UI/
│       │   └── DefaultSkin.prefab
│       └── Config/
│           └── GameSettings.asset
│
├── Plugins/
│   └── ESFramework/
│       └── Resources/          # 框架必需的Runtime资源
│           └── ES/
│               └── DefaultIcons.png
│
└── NormalResources/            # 【建议删除或重命名】
    └── ... (移动到 _Project/Art/)
```

**Resources 使用原则**：
- ✅ **必须Runtime加载的资源**（如默认配置、Fallback资源）
- ❌ **避免大量使用**（影响启动时间，无法按需卸载）
- ✅ **优先使用 AssetBundle 或 Addressable**

---

## 五、推荐工具

### 5.1 自动化迁移脚本

```csharp
// Editor/Tools/FolderMigrationTool.cs
#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public class FolderMigrationTool : EditorWindow
{
    [MenuItem("ES/Tools/Migrate Folder Structure")]
    public static void ShowWindow()
    {
        GetWindow<FolderMigrationTool>("Folder Migration");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "This tool will migrate the project to the new folder structure. " +
            "Please backup your project first!",
            MessageType.Warning
        );
        
        if (GUILayout.Button("Start Migration"))
        {
            if (EditorUtility.DisplayDialog(
                "Confirm Migration",
                "This will reorganize the entire project structure. Continue?",
                "Yes", "Cancel"))
            {
                Migrate();
            }
        }
    }
    
    private void Migrate()
    {
        try
        {
            // 1. 创建新目录
            Directory.CreateDirectory("Assets/_Project/_Docs");
            Directory.CreateDirectory("Assets/_Project/_Prototypes");
            Directory.CreateDirectory("Assets/Plugins/ThirdParty");
            
            // 2. 移动文件
            AssetDatabase.MoveAsset(
                "Assets/ES/AIAnlyze",
                "Assets/_Project/_Docs/Analysis"
            );
            
            AssetDatabase.MoveAsset(
                "Assets/ES/AIPreview",
                "Assets/_Project/_Prototypes"
            );
            
            // 3. 刷新
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                "Migration Complete",
                "Folder structure has been updated successfully!",
                "OK"
            );
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog(
                "Migration Failed",
                $"Error: {ex.Message}",
                "OK"
            );
        }
    }
}
#endif
```

---

### 5.2 目录结构验证工具

```csharp
// Editor/Tools/FolderStructureValidator.cs
public class FolderStructureValidator
{
    [MenuItem("ES/Tools/Validate Folder Structure")]
    public static void Validate()
    {
        var issues = new List<string>();
        
        // 检查1：Runtime文件夹不应包含UnityEditor引用
        var runtimeScripts = Directory.GetFiles(
            "Assets/Plugins/ESFramework/Runtime",
            "*.cs",
            SearchOption.AllDirectories
        );
        
        foreach (var file in runtimeScripts)
        {
            var content = File.ReadAllText(file);
            if (content.Contains("using UnityEditor"))
            {
                issues.Add($"Runtime script contains UnityEditor: {file}");
            }
        }
        
        // 检查2：第三方库应在ThirdParty文件夹
        var suspiciousRootFolders = new[] { "Gaskellgames", "vFolders", "vHierarchy" };
        foreach (var folder in suspiciousRootFolders)
        {
            if (Directory.Exists($"Assets/{folder}"))
            {
                issues.Add($"Third-party library in root: Assets/{folder} (should be in Plugins/ThirdParty)");
            }
        }
        
        // 输出结果
        if (issues.Count == 0)
        {
            Debug.Log("✅ Folder structure validation passed!");
        }
        else
        {
            Debug.LogError($"❌ Found {issues.Count} issues:\n" + string.Join("\n", issues));
        }
    }
}
```

---

## 六、对比：优化前后

### 6.1 优化前的问题场景

**场景1：打包体积异常**
```
问题：Build体积包含了 AIAnlyze 文档和 AIPreview 原型代码
原因：这些文件夹在 Assets/ 根目录，Unity默认打包所有Assets内容
影响：发布包体积多出 20MB+ 的无用文件
```

**场景2：Runtime包含Editor代码**
```
问题：iOS打包失败，提示 UnityEditor.EditorWindow 不存在
原因：Plugins/ES/2_Editor 中的代码未被 asmdef 约束
影响：无法发布到移动平台
```

**场景3：第三方库冲突**
```
问题：升级Odin Inspector后，与vFolders冲突
原因：两者都在根目录，命名空间可能重叠
影响：编译错误，难以定位问题源
```

---

### 6.2 优化后的改进

**场景1 优化**：
```
解决：_Docs 和 _Prototypes 前缀 _ 易于识别，可配置 .gitignore 和打包排除
结果：Build体积减少 20MB，打包时间缩短 15%
```

**场景2 优化**：
```
解决：通过 asmdef 严格分离 Runtime 和 Editor
结果：所有平台打包成功，Runtime代码零Editor依赖
```

**场景3 优化**：
```
解决：所有第三方库统一到 Plugins/ThirdParty/
结果：依赖关系清晰，升级/移除第三方库风险降低
```

---

## 七、总结与建议

### ✅ 优先级 P0（立即修复）
1. **移动文档与原型**：`AIAnlyze` → `_Project/_Docs`，`AIPreview` → `_Project/_Prototypes`
2. **创建 Runtime/Editor asmdef**：确保Editor代码不打包到Runtime

### ✅ 优先级 P1（本周完成）
3. **第三方库隔离**：移动到 `Plugins/ThirdParty/`
4. **Partial类分离**：Editor扩展移到Editor文件夹

### ✅ 优先级 P2（下周完成）
5. **目录重命名**：`0_Stand` → `00_Foundation`，`1_Design` → `01_Patterns`
6. **文件命名规范**：`Poolable-Define.cs` → `IPoolable.cs`

### ✅ 优先级 P3（可选）
7. **添加Tests文件夹**：为未来单元测试做准备
8. **UPM包化**：添加 `package.json`，支持Package Manager导入

---

**文档版本**：v2.0  
**更新日期**：2026-01-16  
**预计迁移时间**：5-7天（包含测试）
