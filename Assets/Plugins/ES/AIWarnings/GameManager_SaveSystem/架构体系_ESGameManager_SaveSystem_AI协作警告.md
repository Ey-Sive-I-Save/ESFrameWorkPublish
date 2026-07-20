# ESGameManager / SaveSystem / Module Inspector 协作警告

职责所在：架构体系

本文负责约束 `ESGameManager`、GameManager Domain 分层、保存系统入口、模块归属、模块 Inspector 渲染边界等架构级规则。它不是功能使用说明，也不是临时 TODO。后续 AI 修改相关代码前，应先阅读本文，避免重复创建旧架构、错误移动模块、破坏 Odin 编辑体验或绕开保存系统边界。

本文给后续协作 AI 使用。目标是记录已经确认过的工程事实，避免再次把 GameManager、保存系统、模块渲染结构改乱。不要把本文当成泛用架构建议；它只描述当前工程约束。

## 当前唯一 GameManager 位置

当前项目只允许一个游戏级入口：

```text
Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs
```

不要再恢复或新增：

```text
Assets/Plugins/ES/2_Feature/ESGameCore
ES_GameCore.asmdef
ES_GameCore.csproj
```

`ESGameManager` 属于当前项目逻辑层，不再放在 `Plugins/ES/2_Feature`。`ESCommand` 也已经迁入 `Assets/Scripts/ESLogic/Runtime/Command`，不要恢复 `ES_Feature` 或 `ES_GameCore` 中转程序集。

## 当前 GameManager 三域

`ESGameManager` 当前只有 3 个顶层 Domain：

```text
系统域 ESSystemDomain
流程域 ESFlowDomain
世界域 ESWorldDomain
```

严格区分方式：

```text
系统域：提供稳定能力，不决定当前怎么玩。
流程域：决定当前怎么玩，控制系统能力开关或运行阶段。
世界域：描述当前场景、地图、世界实例里有什么。
```

当前模块归属：

```text
ESGameSaveModule -> ESSystemModule -> 系统域
ESInputModule    -> ESSystemModule -> 系统域
ESCommandModule  -> ESRuntimeModule -> ESFlowModule -> 流程域
```

兼容基类当前含义：

```text
ESRuntimeModule      -> ESFlowModule
ESPlayerModule       -> ESWorldModule
ESPresentationModule -> ESFlowModule
```

不要恢复旧的 `GlobalDomain / GameRunDomain`，也不要恢复旧的四域：

```text
运行域
世界域
玩家域
表现域
```

注意：当前保留的 `ESWorldDomain` 是三域方案中的世界域，不是旧四域里的“世界域”复刻。不要再加玩家域/表现域作为 GameManager 顶层域；玩家和表现优先落在 Entity、UI、Camera、State 等更具体系统内。

## Domain 数量原则

GameManager 顶层 Domain 要尽量少，边界要尽量明显。Domain 不是普通文件夹，也不是“看起来像一类功能就新建一个”的分类标签。它是运行时模块的最高层归属，会影响初始化顺序、调度入口、Inspector 展示、跨模块依赖和后续 AI 的理解成本。

Domain 过多会带来几个直接问题：

```text
1. 模块归属变模糊：输入、流程、表现、玩家、世界很容易互相抢模块。
2. 调度顺序变复杂：顶层越多，初始化和释放顺序越难稳定。
3. Inspector 变拥挤：每个 Domain 都会带一组模块列表，主面板很快失控。
4. 协作成本升高：后续 AI 和开发者会开始按名字猜，而不是按职责判断。
5. 架构容易反复横跳：今天新建玩家域，明天又发现玩家其实应在 Entity 内。
```

当前三域的意义是把判断压缩成三个稳定问题：

```text
提供稳定能力，不决定怎么玩 -> 系统域
决定当前怎么玩，控制阶段和开关 -> 流程域
描述当前世界里有什么 -> 世界域
```

如果一个模块放不进去，优先检查模块职责是否太混杂，而不是立刻新建 Domain。只有当它具备长期稳定、顶层调度、清晰生命周期、无法自然归入三域的理由时，才考虑新增 Domain。

## Feature 理解

Feature 是可复用玩法能力包。它提供某一类通用能力，但不直接承担全局生命周期、主流程调度或具体业务规则。需要运行时调度时，由 GameManager/Domain 下的 Module 接入。

可以放 Feature 的内容：

```text
ESCommand
ContextFeature
通用交互辅助
通用挂点辅助
通用对象引用辅助
```

不该放 Feature 的内容：

```text
GameManager 生命周期
Domain / Module 调度
保存系统主流程
输入系统主流程
状态机主流程
技能主流程
具体角色业务
具体关卡业务
```

一句话标准：

```text
能被多个系统复用，但本身不拥有全局生命周期的功能包，才叫 Feature。
```

## RuntimeMode 归属

`RuntimeMode` 是 GameManager 的流程状态服务：

```csharp
ESGameManager.RuntimeMode
```

它不是输入模块的私有状态。输入模块会读取 RuntimeMode 结果来过滤输入，但“被流程影响”不等于“属于流程域”。

当前边界：

```text
RuntimeMode -> GameManager/流程状态服务
ESInputModule -> 系统域能力提供者
ESCommandModule -> 流程域调度者
```

不要因为 `ESInputModule` 读取 `ESGameManager.RuntimeMode` 就把输入模块重新改回流程域。

## 保存系统事实

保存系统当前位置：

```text
Assets/Scripts/ESLogic/Runtime/GameManager/SaveSystem/
```

当前公开入口是：

```csharp
ESGameSave.Set(...);
ESGameSave.Get(...);
ESGameSave.Save(...);
ESGameSave.Load(...);
ESGameSave.Has(...);
ESGameSave.Delete(...);
ESGameSave.Info(...);
```

不要恢复：

```csharp
ESGameManager.SaveModule
```

保存模块通过：

```csharp
ESGameManager.GetModuleFast<ESGameSaveModule>()
```

由 `ESGameSave` 静态门面获取。

当前语义：

```text
Set/Get = 内存缓存
Save/Load = 磁盘
```

`Set()` 当前会把业务对象序列化为 Json 字符串快照后存入缓存 Archive，不是长期持有业务对象引用。不要把缓存改成直接引用玩家、背包、任务等运行时对象，否则会引入生命周期和误修改风险。

稳定写入逻辑必须保留：

```text
写临时文件
写后读取校验
旧文件备份
临时文件替换正式文件
失败时按设置保留/清理临时文件
```

Easy Save 3 只是低层存储能力，不是项目保存架构本体。业务侧不要散写 ES3 key。

## 未来 Link 保存设计边界

用户倾向用 ES Link 系统组织保存/读档协作，而不是传统 Provider 列表。

如果继续推进保存系统，优先方向是：

```text
SaveModule 仍然负责 Archive、缓存、磁盘、版本迁移、加密压缩。
业务系统通过 Link 提交保存数据或按阶段应用读档数据。
Load 只负责把存档读入内存。
Apply 负责按阶段通知系统恢复自身。
```

建议阶段含义：

```text
Config
World
Player
Inventory
Quest
Runtime
Presentation
```

阶段用于确定读档应用顺序，不是磁盘读取顺序。

## Module Inspector 渲染约束

GameManager 的 Domain 中包含模块列表。模块往往自身带有大量 Odin `TabGroup`、列表、调试字段。如果在主面板内完整绘制模块，会发生：

```text
列表缩进 + 模块 Header + TabGroup 横向铺开
=> 可用宽度急剧变小
=> UI 被挤出右侧或渲染卡顿
```

当前解决方案：

```text
Assets/Scripts/ESLogic/Editor/Drawers/ESGameModuleCompactDrawers.cs
```

主面板模块行只显示轻量摘要：

```text
模块名 / 所属域 / 启用状态 / 详情 / 弹窗
```

`详情` 只允许显示轻量状态，不允许调用 `CallNextDrawer(GUIContent.none)` 绘制完整模块。完整编辑必须走 `弹窗`。

性能原则：

```text
主 GameManager Inspector 不递归绘制大模块。
主 GameManager Inspector 不反射扫描模块字段。
类型名可缓存。
模块列表分页。
完整 Odin 绘制只在用户主动打开弹窗时发生。
```

如果后续 AI 想“修复展开不能完整编辑”，不要直接把完整模块画回主面板。正确做法是优化弹窗标题、尺寸、定位、脏标记和上下文提示。

## Mono 脚本命名约束

Unity `MonoBehaviour` 脚本文件必须和类名保持一致。迁移或重命名功能目录时，不能只按分层前缀命名文件。

已确认案例：

```text
错误：SERVICE_ESCommandPlayer.cs
正确：ESCommandPlayer.cs
类名：public sealed class ESCommandPlayer : MonoBehaviour
```

当前 `ESCommand` 已迁入：

```text
Assets/Scripts/ESLogic/Runtime/Command/
    Components/ESCommandPlayer.cs
    Commands/
    Runtime/
    Docs/
```

Command 是运行时主干系统，不再放在 `Features/ESCommandPlay`。开发者最常挂的播放器组件放 `Components/`，具体命令实现放 `Commands/`，播放器运行服务、接口和播放帧放 `Runtime/`，说明文档放 `Docs/`。

该目录内 `ESCommandPlayer` 是 Mono 脚本，文件名必须保持：

```text
ESCommandPlayer.cs
ESCommandPlayer.cs.meta
```

不要再把 Mono 文件改成 `SERVICE_`、`MODULE_`、`COMMAND_` 等和类名不一致的文件名。非 Mono 普通 C# 类型可以继续使用项目既有前缀命名风格。

## 编码与文本

本工程此前多次出现中文乱码。编辑 C#、md、asset 文本时必须保持 UTF-8。不要用会按系统 ANSI 写文件的命令重写中文文件。

推荐：

```text
用 apply_patch 做文本修改。
读文件时 PowerShell 使用 -Encoding UTF8。
不要用 Set-Content 默认编码批量重写中文源文件。
```

看到乱码时不要继续复制乱码扩散；应回到原始语义重写成正常中文。

## 最低验证

改 GameManager、保存系统、输入模块、模块 Drawer 后至少运行：

```powershell
dotnet build "F:\aaProject\ESFrameWorkPublish\ES_Logic.csproj"
```

并搜索旧结构残留：

```powershell
rg -n "ES_GameCore|GlobalDomain|GameRunDomain|ESGameManager\.SaveModule|游戏核心/运行域" "F:\aaProject\ESFrameWorkPublish\Assets"
```

残留在历史说明、资产索引里不一定是编译错误，但代码和 asmdef 中不应再出现旧结构。
