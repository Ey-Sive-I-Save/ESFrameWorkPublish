# ES 框架科学学习路线图

> **设计原则**：依赖驱动、从底向上、实战导向  
> **目标人群**：Unity开发者（有C#基础）  
> **预计学习时间**：4-6周（每天2-3小时）

---

## 一、学习路径总览

```
Level 0: 前置知识（1周）
    └─► C# 高级特性 + Unity 基础

Level 1: 基础设施（1周）
    ├─► ValueTypes（数据结构）
    ├─► IESWithLife（生命周期基类）
    └─► Pool（对象池）

Level 2: 核心架构（1-2周）
    ├─► Hosting（托管器）
    ├─► Module（模块）
    └─► ResLibrary（资源管理）

Level 3: 高级特性（1-2周）
    ├─► Link（消息系统）
    ├─► SafeCollections（并发安全集合）
    └─► Res 高级加载策略

Level 4: 项目实战（1-2周）
    ├─► UI框架集成
    ├─► GameManager 实现
    └─► 完整 Demo 项目

Level 5: 扩展与优化（进阶）
    ├─► Mod 系统
    ├─► 性能优化
    └─► Editor 工具开发
```

---

## 二、Level 0: 前置知识检查

### 2.1 必备技能清单

| 技能 | 重要度 | 验证方式 |
|------|--------|---------|
| **C# 泛型** | ⭐⭐⭐⭐⭐ | 能手写 `Pool<T>` |
| **C# 接口** | ⭐⭐⭐⭐⭐ | 理解接口继承、多态 |
| **C# 委托/事件** | ⭐⭐⭐⭐ | 理解 `Action<T>` 使用 |
| **Unity 生命周期** | ⭐⭐⭐⭐⭐ | 知道 Awake/Start/Update 区别 |
| **ScriptableObject** | ⭐⭐⭐⭐ | 能创建自定义 SO |
| **AssetBundle** | ⭐⭐⭐ | 知道基本加载流程 |

### 2.2 快速自测题

```csharp
// 题目1：以下代码输出什么？
public class Test : MonoBehaviour
{
    void Awake() => print("A");
    void OnEnable() => print("B");
    void Start() => print("C");
}
// 答案：B → A → C (错) 正确：A → B → C

// 题目2：以下代码有什么问题？
public class Manager<T>
{
    private static T instance;
    public static T Instance => instance ??= new T();
}
// 答案：T 没有 new() 约束，无法编译

// 题目3：ScriptableObject 与 MonoBehaviour 的核心区别？
// 答案：SO 是资产文件，不依附GameObject，不参与场景生命周期
```

**如果自测正确率 < 70%，建议先学习**：
- [C# 进阶教程](https://learn.microsoft.com/zh-cn/dotnet/csharp/)
- [Unity 官方教程](https://learn.unity.com/)

---

## 三、Level 1: 基础设施（Week 1）

### 3.1 Day 1-2: ValueTypes 与数据结构

**学习目标**：理解框架的基础数据类型

**核心文件**：
- `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ValueTypes/`

**必学内容**：
1. `ESTryResult` 枚举（操作结果三态：Success/Fail/ReTry）
2. `IntRange`, `FloatRange`（区间类型）
3. 自定义序列化类型

**实战练习**：
```csharp
// 练习1：使用 ESTryResult 实现安全的列表添加
public ESTryResult TryAddUnique<T>(List<T> list, T item)
{
    if (list.Contains(item))
        return ESTryResult.ReTry; // 已存在
    
    list.Add(item);
    return ESTryResult.Success;
}

// 练习2：实现一个 IntRange 的随机数生成器
public static class RangeExtensions
{
    public static int Random(this IntRange range)
    {
        return UnityEngine.Random.Range(range.min, range.max);
    }
}
```

---

### 3.2 Day 3-4: IESWithLife 生命周期

**学习目标**：掌握框架统一的生命周期管理

**核心文件**：
- `IESWithLife.cs`
- `BaseESModule.cs`（实现示例）

**关键概念**：
```csharp
public interface IESWithLife
{
    void TryEnableSelf();    // 激活
    void TryDisableSelf();   // 禁用
    void TryUpdateSelf();    // 更新
    void TryDestroySelf();   // 销毁
}
```

**为什么用 "Try" 前缀？**
- 表示操作可能失败（如已经 Enable 的对象重复 Enable）
- 内部会检查状态（如 `if (!enabled) { Enable(); }`）

**实战练习**：
```csharp
// 练习3：实现一个简单的生命周期对象
public class MyFeature : IESWithLife
{
    private bool isActive = false;
    
    public void TryEnableSelf()
    {
        if (isActive) return;
        isActive = true;
        Debug.Log("Feature Enabled");
    }
    
    public void TryDisableSelf()
    {
        if (!isActive) return;
        isActive = false;
        Debug.Log("Feature Disabled");
    }
    
    public void TryUpdateSelf()
    {
        if (!isActive) return;
        Debug.Log("Feature Updating");
    }
    
    public void TryDestroySelf()
    {
        isActive = false;
        Debug.Log("Feature Destroyed");
    }
}
```

---

### 3.3 Day 5-7: Pool 对象池

**学习目标**：理解对象池化，减少 GC

**核心文件**：
- `Poolable-Define.cs`
- `LinkRecievePool.cs`（实际应用示例）

**核心API**：
```csharp
public abstract class Pool<T> where T : class, IPoolable, new()
{
    protected Stack<T> mPool;
    protected Func<T> mFactory;
    
    public T GetInPool(); // 从池中获取或创建
    public abstract void PushToPool(T e); // 归还到池
}
```

**实战练习**：
```csharp
// 练习4：实现一个简单的子弹对象池
public class Bullet : MonoBehaviour, IPoolable
{
    public void OnGetInPool()
    {
        gameObject.SetActive(true);
    }
    
    public void OnPushToPool()
    {
        gameObject.SetActive(false);
    }
    
    public void ResetSelf()
    {
        transform.position = Vector3.zero;
        // 重置其他状态
    }
}

public class BulletPool : Pool<Bullet>
{
    private Transform poolParent;
    
    public BulletPool(GameObject bulletPrefab, Transform parent)
    {
        poolParent = parent;
        mFactory = () => GameObject.Instantiate(bulletPrefab, parent).GetComponent<Bullet>();
    }
    
    public override void PushToPool(Bullet e)
    {
        e.OnPushToPool();
        mPool.Push(e);
    }
}

// 使用示例
var bulletPool = new BulletPool(bulletPrefab, transform);
var bullet = bulletPool.GetInPool();
// ... 使用子弹 ...
bulletPool.PushToPool(bullet);
```

**理解难点**：
- **何时 Push 回池？** → 在不再需要对象时（如子弹击中目标）
- **池满了怎么办？** → 当前实现会丢弃（mMaxCount=12），可优化为动态扩容

---

## 四、Level 2: 核心架构（Week 2-3）

### 4.1 Day 8-10: Hosting 托管器

**学习目标**：理解如何统一管理多个对象的生命周期

**核心文件**：
- `IESHosting.cs`
- `BaseESHosting.cs`

**核心概念**：
```
Hosting = 管理者
Module = 被管理者

Hosting 的职责：
1. 维护 Module 列表
2. 在自己的 OnEnable 时调用所有 Module 的 EnableAsHosting
3. 在自己的 Update 时调用所有 Module 的 UpdateAsHosting（按间隔）
4. 在自己的 OnDisable 时调用所有 Module 的 DisableAsHosting
```

**实战练习**：
```csharp
// 练习5：实现一个简单的 Hosting
public class MyHosting : MonoBehaviour, IESHosting
{
    private List<IESModule> modules = new();
    
    public void _TryAddToListOnly(IESModule module)
    {
        if (!modules.Contains(module))
            modules.Add(module);
    }
    
    public void _TryRemoveFromListOnly(IESModule module)
    {
        modules.Remove(module);
    }
    
    void OnEnable()
    {
        foreach (var module in modules)
            module.TryEnableSelf();
    }
    
    void Update()
    {
        foreach (var module in modules)
            module.TryUpdateSelf();
    }
    
    void OnDisable()
    {
        foreach (var module in modules)
            module.TryDisableSelf();
    }
}
```

---

### 4.2 Day 11-14: Module 模块

**学习目标**：实现自己的功能模块

**核心文件**：
- `IESModule.cs`
- `BaseESModule.cs`

**典型使用场景**：
1. **UI 模块化**：一个 UIPanel = 1 个 Hosting，包含多个 Module（动画、输入、数据绑定）
2. **GameManager 模块化**：GameManager = 1 个 Hosting，包含 PlayerModule、EnemyModule、QuestModule 等

**实战练习**：
```csharp
// 练习6：实现一个玩家输入模块
public class PlayerInputModule : BaseESModule, IESModule<GameManager>
{
    public GameManager GetHost { get; private set; }
    
    public ESTryResult _TryRegisterToHost(GameManager host)
    {
        if (Signal_HasSubmit) return ESTryResult.ReTry;
        
        GetHost = host;
        Signal_HasSubmit = true;
        host._TryAddToListOnly(this);
        return ESTryResult.Success;
    }
    
    protected override void OnEnable()
    {
        // 订阅输入事件
        Input.onKeyDown += HandleKeyDown;
    }
    
    protected override void OnDisable()
    {
        Input.onKeyDown -= HandleKeyDown;
    }
    
    protected override void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GetHost.OnPlayerJump();
        }
    }
    
    private void HandleKeyDown(KeyCode key)
    {
        Debug.Log($"Key pressed: {key}");
    }
}

// 在 GameManager 中使用
public class GameManager : MonoBehaviour, IESHosting
{
    private PlayerInputModule inputModule;
    
    void Start()
    {
        inputModule = new PlayerInputModule();
        inputModule._TryRegisterToHost(this);
        inputModule.TryEnableSelf();
    }
    
    public void OnPlayerJump()
    {
        Debug.Log("Player jumped!");
    }
}
```

---

### 4.3 Day 15-17: ResLibrary 资源管理

**学习目标**：掌握 Library → Book → Page 三级资源组织

**核心文件**：
- `ESResLibrary.cs`
- `ESResBook.cs`
- `ESResPage.cs`

**概念对应**：
```
Library = 游戏整体资源库（如"角色库"）
Book    = 某一类资源的集合（如"战士角色集"）
Page    = 具体的资源页（如"初级战士"、"高级战士"）
```

**实战练习**：
```csharp
// 练习7：创建一个角色资源库

// 1. 创建 Library
[CreateAssetMenu(menuName = "Game/CharacterLibrary")]
public class CharacterLibrary : ESResLibrary
{
    // 可以添加库级别的配置
    public int maxCharacterCount = 100;
}

// 2. 创建 Book
[CreateAssetMenu(menuName = "Game/CharacterBook")]
public class CharacterBook : ESResBook
{
    public CharacterClass characterClass; // 战士/法师/射手
}

// 3. 创建 Page
[CreateAssetMenu(menuName = "Game/CharacterPage")]
public class CharacterPage : ESResPage
{
    public string characterId;
    public Sprite portrait;
    public GameObject prefab;
    public CharacterStats stats;
}

// 4. 使用
public class CharacterManager : MonoBehaviour
{
    public CharacterLibrary library;
    
    void Start()
    {
        // 加载"战士"Book
        var warriorBook = library.GetBook("Warrior");
        
        // 加载"初级战士"Page
        var noviceWarriorPage = warriorBook.GetPage("NoviceWarrior") as CharacterPage;
        
        // 实例化角色
        var character = Instantiate(noviceWarriorPage.prefab);
    }
}
```

---

## 五、Level 3: 高级特性（Week 3-4）

### 5.1 Day 18-21: Link 消息系统

**学习目标**：实现类型安全的模块间通信

**核心文件**：
- `IReceiveLink.cs`
- `LinkReceivePool.cs`
- `LinkReceiveList.cs`

**学习步骤**：

**Step 1：理解基础用法**
```csharp
// 定义消息类型
public struct PlayerDiedEvent
{
    public int playerId;
    public Vector3 deathPosition;
}

// 接收者
public class UIModule : MonoBehaviour, IReceiveLink<PlayerDiedEvent>
{
    void OnEnable()
    {
        LinkPool.AddReceive<PlayerDiedEvent>(this);
    }
    
    void OnDisable()
    {
        LinkPool.RemoveReceive<PlayerDiedEvent>(this);
    }
    
    public void OnLink(PlayerDiedEvent evt)
    {
        ShowDeathUI(evt.playerId, evt.deathPosition);
    }
}

// 发送者
public class Player : MonoBehaviour
{
    void Die()
    {
        LinkPool.SendLink(new PlayerDiedEvent
        {
            playerId = this.id,
            deathPosition = transform.position
        });
    }
}
```

**Step 2：理解 Action 包装器**
```csharp
// 不实现接口，直接用 Action
public class ScoreManager : MonoBehaviour
{
    void OnEnable()
    {
        LinkPool.AddReceive<PlayerDiedEvent>(OnPlayerDied);
    }
    
    void OnDisable()
    {
        LinkPool.RemoveReceive<PlayerDiedEvent>(OnPlayerDied);
    }
    
    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        DeductScore(evt.playerId, 10);
    }
}
```

**Step 3：理解 Channel 多路复用**
```csharp
// 使用 Channel 区分不同玩家的输入
public enum PlayerChannel { Player1, Player2 }

public class InputManager : MonoBehaviour, IReceiveChannelLink<PlayerChannel, JumpCommand>
{
    void OnEnable()
    {
        ChannelLinkPool.AddReceive<PlayerChannel, JumpCommand>(this);
    }
    
    public void OnLink(PlayerChannel channel, JumpCommand cmd)
    {
        if (channel == PlayerChannel.Player1)
            player1Controller.Jump();
        else
            player2Controller.Jump();
    }
}

// 发送消息
ChannelLinkPool.SendLink(PlayerChannel.Player1, new JumpCommand());
```

---

### 5.2 Day 22-24: SafeCollections 并发安全集合

**学习目标**：理解如何安全地在遍历时添加/删除元素

**核心文件**：
- `SafeNormalList.cs`
- `SafeKeyGroup.cs`

**核心问题**：
```csharp
// 错误示例：遍历时修改会崩溃
List<Enemy> enemies = new();
foreach (var enemy in enemies)
{
    if (enemy.hp <= 0)
        enemies.Remove(enemy); // ❌ InvalidOperationException
}
```

**SafeNormalList 解决方案**：
```csharp
SafeNormalList<Enemy> enemies = new();

// 遍历期间添加/删除会缓存
enemies.Add(newEnemy);      // 加入 AddBuffer
enemies.Remove(deadEnemy);  // 加入 RemoveBuffer

// 遍历前必须调用
enemies.ApplyBuffers();     // 将缓存应用到主列表

// 现在安全遍历
foreach (var enemy in enemies.ValuesNow)
{
    enemy.Update();
}
```

**实战练习**：
```csharp
// 练习8：实现一个安全的敌人管理器
public class EnemyManager : MonoBehaviour
{
    private SafeNormalList<Enemy> enemies = new();
    
    void Update()
    {
        // 应用缓存（新增/删除）
        enemies.ApplyBuffers();
        
        // 安全遍历
        foreach (var enemy in enemies.ValuesNow)
        {
            enemy.Update();
            
            // 遍历中可以安全删除
            if (enemy.hp <= 0)
                enemies.Remove(enemy);
        }
    }
    
    public void SpawnEnemy(Enemy enemy)
    {
        // 可以在任何时候调用
        enemies.Add(enemy);
    }
}
```

---

## 六、Level 4: 项目实战（Week 5）

### 6.1 综合项目：简易 RPG Demo

**项目需求**：
- 玩家角色（可移动、跳跃、攻击）
- 敌人AI（巡逻、追击、攻击）
- UI系统（血量条、得分、暂停菜单）
- 资源管理（角色/敌人/特效的动态加载）

**架构设计**：
```
GameManager (Hosting)
    ├─► PlayerModule (管理玩家输入与状态)
    ├─► EnemyModule (管理所有敌人)
    ├─► UIModule (管理UI显示)
    └─► ResourceModule (管理资源加载/卸载)

通信方式：
- PlayerModule → UIModule: PlayerHealthChangedEvent (Link)
- EnemyModule → UIModule: EnemyDefeatedEvent (Link)
- UIModule → PlayerModule: PauseGameEvent (Link)

资源结构：
CharacterLibrary
    ├─► PlayerBook
    │       ├─► WarriorPage (战士角色)
    │       └─► MagePage (法师角色)
    └─► EnemyBook
            ├─► GoblinPage (哥布林)
            └─► OrcPage (兽人)
```

**实现步骤（5天完成）**：

**Day 25：搭建 GameManager 与 Module 骨架**
```csharp
public class GameManager : MonoBehaviour, IESHosting
{
    private PlayerModule playerModule;
    private EnemyModule enemyModule;
    private UIModule uiModule;
    
    void Start()
    {
        playerModule = new PlayerModule();
        enemyModule = new EnemyModule();
        uiModule = new UIModule();
        
        playerModule._TryRegisterToHost(this);
        enemyModule._TryRegisterToHost(this);
        uiModule._TryRegisterToHost(this);
        
        playerModule.TryEnableSelf();
        enemyModule.TryEnableSelf();
        uiModule.TryEnableSelf();
    }
}
```

**Day 26：实现 PlayerModule**
```csharp
public class PlayerModule : BaseESModule
{
    private Player player;
    
    protected override void OnEnable()
    {
        // 加载玩家角色
        var playerPage = ResLibrary.GetPage("Player/Warrior");
        player = Instantiate(playerPage.prefab).GetComponent<Player>();
    }
    
    protected override void Update()
    {
        // 处理输入
        if (Input.GetKeyDown(KeyCode.Space))
        {
            player.Jump();
        }
        
        // 发送状态变化
        if (player.hpChanged)
        {
            LinkPool.SendLink(new PlayerHealthChangedEvent
            {
                currentHp = player.hp,
                maxHp = player.maxHp
            });
        }
    }
}
```

**Day 27：实现 EnemyModule**
```csharp
public class EnemyModule : BaseESModule
{
    private SafeNormalList<Enemy> enemies = new();
    
    protected override void OnEnable()
    {
        SpawnEnemies(5);
    }
    
    protected override void Update()
    {
        enemies.ApplyBuffers();
        
        foreach (var enemy in enemies.ValuesNow)
        {
            enemy.Update();
            
            if (enemy.hp <= 0)
            {
                LinkPool.SendLink(new EnemyDefeatedEvent { enemyId = enemy.id });
                enemies.Remove(enemy);
                Destroy(enemy.gameObject);
            }
        }
    }
    
    private void SpawnEnemies(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var enemyPage = ResLibrary.GetPage("Enemy/Goblin");
            var enemy = Instantiate(enemyPage.prefab).GetComponent<Enemy>();
            enemies.Add(enemy);
        }
    }
}
```

**Day 28：实现 UIModule**
```csharp
public class UIModule : BaseESModule, 
    IReceiveLink<PlayerHealthChangedEvent>,
    IReceiveLink<EnemyDefeatedEvent>
{
    private Slider healthBar;
    private Text scoreText;
    private int score = 0;
    
    protected override void OnEnable()
    {
        LinkPool.AddReceive<PlayerHealthChangedEvent>(this);
        LinkPool.AddReceive<EnemyDefeatedEvent>(this);
        
        // 初始化UI
        healthBar = GameObject.Find("HealthBar").GetComponent<Slider>();
        scoreText = GameObject.Find("ScoreText").GetComponent<Text>();
    }
    
    public void OnLink(PlayerHealthChangedEvent evt)
    {
        healthBar.value = (float)evt.currentHp / evt.maxHp;
    }
    
    public void OnLink(EnemyDefeatedEvent evt)
    {
        score += 10;
        scoreText.text = $"Score: {score}";
    }
}
```

**Day 29：优化与扩展**
- 添加对象池管理子弹/特效
- 实现暂停菜单（UIModule 发送 PauseGameEvent）
- 添加音效（使用 Link 触发音频播放）

---

## 七、Level 5: 扩展与优化（进阶）

### 7.1 性能优化专题

**主题1：减少 Link 系统的 UnityEngine.Object 判空开销**
```csharp
// 优化前：每次 SendLink 都判空
foreach (var receiver in receivers)
{
    if (receiver is UnityEngine.Object obj && obj == null)
        Remove(receiver); // 触发 Native 调用
    else
        receiver.OnLink(link);
}

// 优化后：分帧清理
private int cleanupFrameInterval = 60;
private int frameCounter = 0;

public void SendLink<Link>(Link link)
{
    foreach (var receiver in receivers)
    {
        receiver.OnLink(link); // 不判空
    }
    
    // 每60帧清理一次
    if (++frameCounter >= cleanupFrameInterval)
    {
        frameCounter = 0;
        CleanupDeadReceivers();
    }
}
```

**主题2：Module Update 的间隔优化**
```csharp
// 不是所有 Module 都需要每帧Update
public class MyModule : BaseESModule
{
    [SerializeField] private int updateInterval = 5; // 每5帧更新一次
    private int frameCounter = 0;
    
    protected override void Update()
    {
        if (++frameCounter < updateInterval)
            return;
        
        frameCounter = 0;
        DoActualUpdate();
    }
}
```

---

### 7.2 Editor 工具开发

**工具1：Module 可视化面板**
```csharp
[CustomEditor(typeof(GameManager))]
public class GameManagerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        var gm = (GameManager)target;
        EditorGUILayout.LabelField("Registered Modules", EditorStyles.boldLabel);
        
        foreach (var module in gm.GetAllModules())
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField(module.GetType().Name);
            EditorGUILayout.LabelField(module.Signal_IsActiveAndEnable ? "Enabled" : "Disabled");
            EditorGUILayout.EndHorizontal();
        }
    }
}
```

---

## 八、学习检查点 (Checkpoints)

### Checkpoint 1: Level 1 完成（第1周末）
- [ ] 能解释 `ESTryResult` 的三个状态含义
- [ ] 能手写一个简单的对象池
- [ ] 理解 `IESWithLife` 四个方法的调用时机

### Checkpoint 2: Level 2 完成（第2周末）
- [ ] 能实现一个自定义 Module
- [ ] 能将 Module 注册到 Hosting
- [ ] 能创建 Library/Book/Page 资源结构

### Checkpoint 3: Level 3 完成（第3周末）
- [ ] 能使用 Link 实现跨模块通信
- [ ] 理解 `SafeNormalList.ApplyBuffers()` 的必要性
- [ ] 能用 Channel 实现多路消息分发

### Checkpoint 4: Level 4 完成（第5周末）
- [ ] 完成 RPG Demo 项目
- [ ] 架构中至少包含 3 个 Module
- [ ] 所有模块间通信都使用 Link

---

## 九、常见问题解答

**Q1：Module 和 MonoBehaviour 有什么区别？**
- Module：纯C#类，轻量级，不依附GameObject，可复用
- MonoBehaviour：Unity组件，依附GameObject，有完整Unity生命周期

**Q2：什么时候用 Hosting，什么时候用普通 List？**
- 需要统一生命周期管理 → Hosting
- 只是简单的数据集合 → 普通 List

**Q3：Link 和 C# Event 有什么区别？**
- Link：类型安全、支持Channel、自动清理死亡对象
- Event：简单直接、但缺少高级特性

**Q4：ResLibrary 和 Addressable 能共存吗？**
- 可以！ResPage 内部可以用 Addressable 加载资源

---

## 十、推荐学习资源

1. **官方文档**（假设存在）：
   - ES Framework Wiki
   - API Reference

2. **示例项目**：
   - AIPreview 中的原型代码
   - ESTrackView（复杂的 Module 实战）

3. **相关技术**：
   - [C# 泛型深入](https://docs.microsoft.com/zh-cn/dotnet/csharp/programming-guide/generics/)
   - [Unity ScriptableObject 最佳实践](https://unity.com/how-to/architect-game-code-scriptable-objects)
   - [消息总线模式](https://www.oreilly.com/library/view/enterprise-integration-patterns/0321200683/)

---

**文档版本**：v2.0  
**更新日期**：2026-01-16  
**预计学习时间**：4-6周
