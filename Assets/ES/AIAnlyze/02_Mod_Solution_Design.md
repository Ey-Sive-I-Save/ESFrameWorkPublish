# ES 框架 Mod 系统一流解决方案

> **设计目标**：基于现有 ResLibrary 架构，实现热插拔、类型安全、支持多层依赖的 Mod 系统  
> **核心原则**：无侵入式集成、版本兼容性、错误隔离、性能优化  
> **参考标准**：Skyrim Creation Kit、Minecraft Forge、RimWorld Harmony

---

## 一、架构设计总览

### 1.1 核心概念模型

```
ModRuntime (全局管理器)
    │
    ├─► ModLibrary (继承 ResLibrary)
    │       ├─► ModBook (继承 ResBook)
    │       │       └─► ModPage (继承 ResPage)
    │       │               ├─► Assets (角色/道具/场景 AssetBundle)
    │       │               ├─► Scripts (Lua/C# 热更新代码)
    │       │               └─► Data (Json/ScriptableObject 配置)
    │       │
    │       └─► Dependencies (Mod依赖关系)
    │
    ├─► ModLifecycleManager (生命周期管理)
    │       ├─► Load → Initialize → Enable → Disable → Unload
    │       └─► Error Isolation (异常不影响核心游戏)
    │
    ├─► ModConflictResolver (冲突检测与解决)
    │       ├─► 资源命名冲突 → Priority System
    │       ├─► 依赖版本冲突 → Semantic Versioning
    │       └─► API版本不兼容 → Compatibility Layer
    │
    └─► ModCommunicationBus (Mod间通信)
            ├─► 基于 Link System (类型安全)
            └─► ModChannel<ModId, Message> (隔离通信域)
```

---

### 1.2 目录结构设计

```
Mods/                              # 游戏根目录的Mods文件夹
├── CoreMods/                      # 核心Mod（官方内容，优先级最高）
│   ├── BaseGame/
│   │   ├── ModManifest.json       # Mod元数据
│   │   ├── Assets/                # AssetBundle
│   │   ├── Scripts/               # 热更新代码
│   │   └── Data/                  # 配置文件
│   │
│   └── DLC_1/
│
├── CommunityMods/                 # 社区Mod（第三方内容）
│   ├── AwesomeCharacterPack/
│   │   ├── ModManifest.json
│   │   ├── Assets/
│   │   │   ├── Characters/
│   │   │   ├── Weapons/
│   │   │   └── UI/
│   │   ├── Scripts/
│   │   │   └── CharacterBehavior.dll  # 编译后的C#代码
│   │   └── Data/
│   │       ├── Characters.json        # 角色定义
│   │       └── Localization/          # 多语言
│   │
│   └── NewQuestMod/
│
└── ModCache/                      # 自动生成的缓存
    ├── DependencyGraph.json       # 依赖关系图
    └── LoadOrder.json             # 加载顺序
```

---

## 二、ModManifest 元数据标准

### 2.1 完整示例

```json
{
  "modId": "com.author.awesomecharacterpack",
  "version": "1.2.3",
  "displayName": "Awesome Character Pack",
  "author": "ModAuthor",
  "description": "Adds 20 new playable characters with unique abilities",
  "gameVersion": "1.0.0",                // 兼容的游戏版本
  "apiVersion": "2.1.0",                 // 使用的Mod API版本
  
  "dependencies": [
    {
      "modId": "com.game.base",
      "version": ">=1.0.0 <2.0.0",       // Semantic Versioning范围
      "required": true
    },
    {
      "modId": "com.author.animationlib",
      "version": "^1.5.0",
      "required": false                  // 可选依赖
    }
  ],
  
  "loadOrder": {
    "priority": 100,                     // 优先级 (0-1000, 越大越晚加载)
    "loadAfter": ["com.game.base"],      // 强制在某些Mod之后加载
    "loadBefore": ["com.author.ui"]      // 强制在某些Mod之前加载
  },
  
  "permissions": [                       // 权限申明
    "file_io",                           // 文件读写
    "network",                           // 网络访问
    "native_code"                        // 执行Native代码
  ],
  
  "resources": {
    "assetBundles": [
      "Assets/characters.bundle",
      "Assets/weapons.bundle"
    ],
    "scripts": [
      "Scripts/CharacterBehavior.dll"
    ],
    "data": [
      "Data/Characters.json",
      "Data/Localization/zh-CN.json"
    ]
  },
  
  "hooks": [                             // 游戏Hook点注册
    {
      "targetClass": "GameManager",
      "targetMethod": "OnGameStart",
      "hookMethod": "MyMod.OnGameStartHook",
      "priority": 10
    }
  ]
}
```

---

### 2.2 版本兼容性规则

**Semantic Versioning 支持**：
- `^1.5.0` → `>=1.5.0 <2.0.0` (兼容性更新)
- `~1.5.0` → `>=1.5.0 <1.6.0` (补丁更新)
- `>=1.0.0 <2.0.0` (范围表达式)

**版本检查实现**：
```csharp
public class ModVersion : IComparable<ModVersion>
{
    public int Major;
    public int Minor;
    public int Patch;
    
    public bool IsCompatibleWith(string versionRange)
    {
        // 解析 ">=1.0.0 <2.0.0" 格式
        var (minVersion, maxVersion) = ParseRange(versionRange);
        return this >= minVersion && this < maxVersion;
    }
}
```

---

## 三、ModLibrary 资源管理系统

### 3.1 扩展 ResLibrary 架构

```csharp
/// <summary>
/// Mod资源库：继承 ResLibrary，增加 Mod 特有功能
/// </summary>
[CreateAssetMenu(menuName = "ES/Mod/ModLibrary")]
public class ESModLibrary : ESResLibrary
{
    [Header("Mod Configuration")]
    public ModManifest Manifest;              // Mod元数据
    public ModLoadState LoadState;            // 当前加载状态
    public List<ESModLibrary> Dependencies;   // 依赖的其他Mod
    
    [Header("Override Settings")]
    public bool CanOverrideCore = false;      // 是否允许覆盖核心资源
    public int LoadPriority = 100;            // 加载优先级
    
    /// <summary>
    /// 加载 Mod 资源
    /// </summary>
    public async Task LoadModAsync()
    {
        try
        {
            LoadState = ModLoadState.Loading;
            
            // 1. 检查依赖
            foreach (var dep in Dependencies)
            {
                if (dep.LoadState != ModLoadState.Loaded)
                    throw new ModDependencyException($"Dependency {dep.Manifest.displayName} not loaded");
            }
            
            // 2. 加载AssetBundles
            foreach (var bundlePath in Manifest.resources.assetBundles)
            {
                var fullPath = Path.Combine(ModRootPath, bundlePath);
                var bundle = await AssetBundle.LoadFromFileAsync(fullPath);
                // 注册到 ResBook (复用现有加载系统)
                RegisterAssetBundle(bundle);
            }
            
            // 3. 加载热更新脚本 (可选)
            if (Manifest.resources.scripts != null)
            {
                foreach (var scriptPath in Manifest.resources.scripts)
                {
                    LoadAssembly(scriptPath);
                }
            }
            
            // 4. 加载配置数据
            LoadModData();
            
            // 5. 触发 Mod 初始化回调
            OnModLoaded?.Invoke(this);
            
            LoadState = ModLoadState.Loaded;
        }
        catch (Exception ex)
        {
            LoadState = ModLoadState.Failed;
            Debug.LogError($"[ModSystem] Failed to load {Manifest.displayName}: {ex}");
            throw new ModLoadException(Manifest.modId, ex);
        }
    }
    
    /// <summary>
    /// 获取资源时考虑 Mod 优先级
    /// </summary>
    public override T GetAsset<T>(string assetPath)
    {
        // 优先从当前 Mod 查找
        var asset = base.GetAsset<T>(assetPath);
        if (asset != null) return asset;
        
        // 回退到依赖的 Mod
        foreach (var dep in Dependencies)
        {
            asset = dep.GetAsset<T>(assetPath);
            if (asset != null) return asset;
        }
        
        return null;
    }
}

public enum ModLoadState
{
    Unloaded,
    Loading,
    Loaded,
    Enabled,
    Disabled,
    Failed
}
```

---

### 3.2 资源命名空间隔离

**问题**：两个Mod都定义了 `Characters/Warrior.prefab`，如何避免冲突？

**解决方案**：自动命名空间前缀
```csharp
public class ModResourceResolver
{
    /// <summary>
    /// 解析资源路径：自动添加 Mod 命名空间
    /// </summary>
    public string ResolveAssetPath(string rawPath, ESModLibrary ownerMod)
    {
        // 如果路径不包含命名空间，自动添加
        if (!rawPath.StartsWith("Mods/"))
        {
            return $"Mods/{ownerMod.Manifest.modId}/{rawPath}";
        }
        return rawPath;
    }
    
    /// <summary>
    /// 带优先级的资源查找
    /// </summary>
    public T GetAssetWithPriority<T>(string assetPath) where T : UnityEngine.Object
    {
        // 按优先级排序所有已加载的 Mod
        var sortedMods = ModRuntime.Instance.GetAllLoadedMods()
            .OrderByDescending(m => m.LoadPriority);
        
        foreach (var mod in sortedMods)
        {
            var fullPath = ResolveAssetPath(assetPath, mod);
            var asset = mod.GetAsset<T>(fullPath);
            if (asset != null)
            {
                Debug.Log($"[ModSystem] Asset '{assetPath}' resolved from mod '{mod.Manifest.displayName}'");
                return asset;
            }
        }
        
        Debug.LogWarning($"[ModSystem] Asset '{assetPath}' not found in any mod");
        return null;
    }
}
```

---

## 四、Mod 生命周期管理

### 4.1 状态机设计

```csharp
public class ModLifecycleManager
{
    private Dictionary<string, ModStateMachine> modStates = new();
    
    public async Task EnableModAsync(string modId)
    {
        var stateMachine = modStates[modId];
        
        // 状态转换：Loaded → Initializing → Initialized → Enabling → Enabled
        await stateMachine.TransitionTo(ModState.Initializing);
        
        // 调用 Mod 的初始化回调
        var modInstance = GetModInstance(modId);
        modInstance.OnInitialize();
        
        await stateMachine.TransitionTo(ModState.Initialized);
        
        // 启用 Mod
        await stateMachine.TransitionTo(ModState.Enabling);
        modInstance.OnEnable();
        
        await stateMachine.TransitionTo(ModState.Enabled);
    }
    
    public void DisableMod(string modId)
    {
        var stateMachine = modStates[modId];
        var modInstance = GetModInstance(modId);
        
        // 状态转换：Enabled → Disabling → Disabled
        stateMachine.TransitionTo(ModState.Disabling);
        modInstance.OnDisable();
        stateMachine.TransitionTo(ModState.Disabled);
        
        // 清理资源
        CleanupModResources(modId);
    }
}

public enum ModState
{
    Unloaded,
    Loading,
    Loaded,
    Initializing,
    Initialized,
    Enabling,
    Enabled,
    Disabling,
    Disabled,
    Failed
}
```

---

### 4.2 错误隔离机制

**设计原则**：单个 Mod 崩溃不应导致游戏崩溃

```csharp
public class ModSandbox
{
    /// <summary>
    /// 在隔离环境中执行 Mod 代码
    /// </summary>
    public void ExecuteModCode(Action modAction, string modId)
    {
        try
        {
            modAction();
        }
        catch (Exception ex)
        {
            // 记录错误但不传播
            Debug.LogError($"[ModSystem] Mod '{modId}' threw exception: {ex}");
            ModErrorReporter.Report(modId, ex);
            
            // 标记 Mod 为失败状态
            ModRuntime.Instance.SetModState(modId, ModState.Failed);
            
            // 可选：弹出UI提示玩家
            UIManager.ShowNotification($"Mod '{modId}' encountered an error and has been disabled.");
        }
    }
}

// 使用示例
public void UpdateAllMods()
{
    foreach (var mod in loadedMods)
    {
        ModSandbox.ExecuteModCode(() => mod.OnUpdate(), mod.Manifest.modId);
    }
}
```

---

## 五、Mod 热更新代码支持

### 5.1 C# 脚本热加载

**方案1：预编译 DLL（推荐）**
```csharp
public class ModAssemblyLoader
{
    public Assembly LoadModAssembly(string dllPath)
    {
        // 加载编译好的 DLL
        var assemblyBytes = File.ReadAllBytes(dllPath);
        var assembly = Assembly.Load(assemblyBytes);
        
        // 查找实现了 IModEntry 的类
        var entryType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IModEntry).IsAssignableFrom(t));
        
        if (entryType != null)
        {
            var entryInstance = (IModEntry)Activator.CreateInstance(entryType);
            return assembly;
        }
        
        throw new ModException($"No IModEntry found in {dllPath}");
    }
}

// Mod 开发者需要实现的接口
public interface IModEntry
{
    void OnInitialize(ModContext context);
    void OnEnable();
    void OnDisable();
    void OnUpdate();
}
```

**方案2：Lua 脚本（轻量级）**
```csharp
public class ModLuaRuntime
{
    private LuaEnv luaEnv;
    
    public void LoadModLua(string luaScriptPath)
    {
        luaEnv = new LuaEnv();
        var luaScript = File.ReadAllText(luaScriptPath);
        luaEnv.DoString(luaScript);
        
        // 调用 Lua 中的初始化函数
        var onInit = luaEnv.Global.Get<Action>("OnModInit");
        onInit?.Invoke();
    }
}
```

---

### 5.2 Mod API 设计

**核心API接口**：
```csharp
/// <summary>
/// Mod 开发者可访问的 API
/// </summary>
public class ModAPI
{
    /// <summary>
    /// 注册新角色
    /// </summary>
    public void RegisterCharacter(CharacterDefinition characterDef)
    {
        GameDatabase.Characters.Add(characterDef);
        // 发送 Link 消息通知其他系统
        LinkPool.SendLink(new CharacterRegisteredEvent(characterDef));
    }
    
    /// <summary>
    /// 注册新道具
    /// </summary>
    public void RegisterItem(ItemDefinition itemDef)
    {
        GameDatabase.Items.Add(itemDef);
    }
    
    /// <summary>
    /// 注册新任务
    /// </summary>
    public void RegisterQuest(QuestDefinition questDef)
    {
        QuestManager.Instance.AddQuest(questDef);
    }
    
    /// <summary>
    /// Hook 游戏事件
    /// </summary>
    public void HookEvent<T>(Action<T> callback) where T : struct
    {
        LinkPool.AddReceive(callback);
    }
    
    /// <summary>
    /// 加载 Mod 资源
    /// </summary>
    public T LoadAsset<T>(string assetPath) where T : UnityEngine.Object
    {
        return ModResourceResolver.GetAssetWithPriority<T>(assetPath);
    }
}
```

---

## 六、Mod 间通信与事件系统

### 6.1 基于 Link 的 Mod 通信

```csharp
/// <summary>
/// Mod 专用通信通道：隔离不同 Mod 的消息域
/// </summary>
public class ModCommunicationBus
{
    private LinkReceiveChannelPool<string, object> modChannelPool = new();
    
    /// <summary>
    /// Mod 发送消息到指定通道
    /// </summary>
    public void SendToMod<TMessage>(string targetModId, TMessage message)
    {
        modChannelPool.SendLink(targetModId, message);
    }
    
    /// <summary>
    /// Mod 订阅来自其他 Mod 的消息
    /// </summary>
    public void SubscribeFromMod<TMessage>(string sourceModId, Action<TMessage> callback)
    {
        modChannelPool.AddReceive<TMessage>(sourceModId, callback);
    }
    
    /// <summary>
    /// 广播消息到所有 Mod
    /// </summary>
    public void BroadcastToAllMods<TMessage>(TMessage message)
    {
        var allModIds = ModRuntime.Instance.GetAllLoadedMods()
            .Select(m => m.Manifest.modId);
        
        foreach (var modId in allModIds)
        {
            modChannelPool.SendLink(modId, message);
        }
    }
}

// 使用示例
public class QuestModEntry : IModEntry
{
    public void OnInitialize(ModContext context)
    {
        // 订阅角色 Mod 发送的事件
        context.CommunicationBus.SubscribeFromMod<CharacterLevelUpEvent>(
            "com.author.characterpack",
            OnCharacterLevelUp
        );
    }
    
    private void OnCharacterLevelUp(CharacterLevelUpEvent evt)
    {
        // 角色升级时触发新任务
        if (evt.NewLevel == 10)
        {
            QuestManager.UnlockQuest("legendary_quest");
        }
    }
}
```

---

## 七、Mod 冲突检测与解决

### 7.1 依赖图构建

```csharp
public class ModDependencyResolver
{
    /// <summary>
    /// 构建依赖图并计算加载顺序
    /// </summary>
    public List<ESModLibrary> ResolveLoadOrder(List<ESModLibrary> mods)
    {
        var graph = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();
        
        // 构建邻接表
        foreach (var mod in mods)
        {
            graph[mod.Manifest.modId] = new List<string>();
            inDegree[mod.Manifest.modId] = 0;
        }
        
        foreach (var mod in mods)
        {
            foreach (var dep in mod.Manifest.dependencies)
            {
                if (dep.required)
                {
                    graph[dep.modId].Add(mod.Manifest.modId);
                    inDegree[mod.Manifest.modId]++;
                }
            }
        }
        
        // 拓扑排序（Kahn算法）
        var queue = new Queue<string>();
        foreach (var kv in inDegree)
        {
            if (kv.Value == 0)
                queue.Enqueue(kv.Key);
        }
        
        var loadOrder = new List<string>();
        while (queue.Count > 0)
        {
            var modId = queue.Dequeue();
            loadOrder.Add(modId);
            
            foreach (var dependent in graph[modId])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }
        
        // 检测循环依赖
        if (loadOrder.Count != mods.Count)
        {
            throw new ModCyclicDependencyException("Cyclic dependency detected");
        }
        
        return loadOrder.Select(id => mods.First(m => m.Manifest.modId == id)).ToList();
    }
}
```

---

### 7.2 资源冲突检测

```csharp
public class ModConflictDetector
{
    /// <summary>
    /// 检测资源路径冲突
    /// </summary>
    public List<ResourceConflict> DetectConflicts(List<ESModLibrary> mods)
    {
        var conflicts = new List<ResourceConflict>();
        var resourceMap = new Dictionary<string, List<string>>(); // path → modIds
        
        foreach (var mod in mods)
        {
            foreach (var assetPath in mod.GetAllAssetPaths())
            {
                if (!resourceMap.ContainsKey(assetPath))
                    resourceMap[assetPath] = new List<string>();
                
                resourceMap[assetPath].Add(mod.Manifest.modId);
            }
        }
        
        // 找出冲突项
        foreach (var kv in resourceMap)
        {
            if (kv.Value.Count > 1)
            {
                conflicts.Add(new ResourceConflict
                {
                    ResourcePath = kv.Key,
                    ConflictingMods = kv.Value
                });
            }
        }
        
        return conflicts;
    }
    
    /// <summary>
    /// 自动解决冲突：按优先级选择
    /// </summary>
    public void ResolveConflictsByPriority(List<ResourceConflict> conflicts, List<ESModLibrary> mods)
    {
        var modPriorityMap = mods.ToDictionary(m => m.Manifest.modId, m => m.LoadPriority);
        
        foreach (var conflict in conflicts)
        {
            // 选择优先级最高的 Mod
            var winnerModId = conflict.ConflictingMods
                .OrderByDescending(id => modPriorityMap[id])
                .First();
            
            Debug.LogWarning($"[ModSystem] Resource conflict for '{conflict.ResourcePath}':");
            Debug.LogWarning($"  Winner: {winnerModId} (Priority: {modPriorityMap[winnerModId]})");
            Debug.LogWarning($"  Losers: {string.Join(", ", conflict.ConflictingMods.Where(id => id != winnerModId))}");
        }
    }
}
```

---

## 八、Editor 工具集成

### 8.1 Mod 管理面板

```csharp
public class ModManagerWindow : EditorWindow
{
    [MenuItem("ES/Mod Manager")]
    public static void ShowWindow()
    {
        GetWindow<ModManagerWindow>("Mod Manager");
    }
    
    private Vector2 scrollPos;
    private List<ESModLibrary> allMods;
    
    private void OnEnable()
    {
        allMods = LoadAllMods();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Installed Mods", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var mod in allMods)
        {
            EditorGUILayout.BeginHorizontal("box");
            
            // Mod 信息
            EditorGUILayout.LabelField(mod.Manifest.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"v{mod.Manifest.version}", GUILayout.Width(60));
            
            // 启用/禁用按钮
            var newEnabled = EditorGUILayout.Toggle(mod.LoadState == ModLoadState.Enabled, GUILayout.Width(20));
            if (newEnabled != (mod.LoadState == ModLoadState.Enabled))
            {
                if (newEnabled)
                    EnableMod(mod);
                else
                    DisableMod(mod);
            }
            
            // 详情按钮
            if (GUILayout.Button("Details", GUILayout.Width(80)))
            {
                ShowModDetails(mod);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        
        // 底部按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh"))
        {
            allMods = LoadAllMods();
        }
        if (GUILayout.Button("Check Conflicts"))
        {
            CheckConflicts();
        }
        EditorGUILayout.EndHorizontal();
    }
}
```

---

## 九、性能优化策略

### 9.1 延迟加载

```csharp
public class ModLazyLoader
{
    /// <summary>
    /// 仅加载 Mod 元数据，不加载资源
    /// </summary>
    public async Task PreloadModsAsync()
    {
        var modFolders = Directory.GetDirectories("Mods/");
        
        foreach (var folder in modFolders)
        {
            var manifestPath = Path.Combine(folder, "ModManifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonUtility.FromJson<ModManifest>(File.ReadAllText(manifestPath));
                RegisterModMetadata(manifest);
            }
        }
    }
    
    /// <summary>
    /// 按需加载 Mod 资源
    /// </summary>
    public async Task LoadModOnDemand(string modId)
    {
        var mod = GetModById(modId);
        if (mod.LoadState == ModLoadState.Loaded)
            return;
        
        await mod.LoadModAsync();
    }
}
```

---

### 9.2 资源卸载策略

```csharp
public class ModResourceManager
{
    private Dictionary<string, int> assetRefCounts = new();
    
    /// <summary>
    /// 引用计数管理
    /// </summary>
    public void RetainAsset(string assetPath)
    {
        if (!assetRefCounts.ContainsKey(assetPath))
            assetRefCounts[assetPath] = 0;
        assetRefCounts[assetPath]++;
    }
    
    public void ReleaseAsset(string assetPath)
    {
        if (!assetRefCounts.ContainsKey(assetPath))
            return;
        
        assetRefCounts[assetPath]--;
        if (assetRefCounts[assetPath] <= 0)
        {
            // 卸载资源
            UnloadAsset(assetPath);
            assetRefCounts.Remove(assetPath);
        }
    }
    
    /// <summary>
    /// 卸载未使用的 Mod
    /// </summary>
    public void UnloadUnusedMods()
    {
        foreach (var mod in ModRuntime.Instance.GetAllLoadedMods())
        {
            if (mod.LoadState == ModLoadState.Enabled)
                continue;
            
            // 检查是否有其他 Mod 依赖此 Mod
            if (!IsModRequired(mod))
            {
                UnloadMod(mod);
            }
        }
    }
}
```

---

## 十、总结与实施路线图

### ✅ 核心优势
1. **无缝集成 ResLibrary**：复用现有资源管理系统
2. **类型安全通信**：基于 Link 的 Mod 间通信
3. **错误隔离**：单个 Mod 崩溃不影响游戏
4. **工业级版本管理**：Semantic Versioning + 依赖解析
5. **Editor 友好**：完整的可视化管理工具

### 📋 实施优先级

**Phase 1 - 核心功能（2周）**
- [ ] 实现 ESModLibrary 扩展
- [ ] ModManifest 解析器
- [ ] ModLifecycleManager 基础生命周期
- [ ] 简单的加载/卸载功能

**Phase 2 - 高级特性（3周）**
- [ ] 依赖解析与拓扑排序
- [ ] 冲突检测与优先级系统
- [ ] C# DLL 热加载
- [ ] ModAPI 完整实现

**Phase 3 - 工具链（2周）**
- [ ] Mod Manager Editor Window
- [ ] Mod 创建向导
- [ ] 自动化打包工具

**Phase 4 - 优化与测试（2周）**
- [ ] 性能 Profiling
- [ ] 大规模 Mod 测试（100+ Mods）
- [ ] 错误隔离压力测试

---

**文档版本**：v2.0  
**设计日期**：2026-01-16  
**设计团队**：ES框架架构组
