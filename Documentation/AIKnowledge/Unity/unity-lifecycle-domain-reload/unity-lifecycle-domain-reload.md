# Unity MonoBehaviour 生命周期、静态状态与 Domain Reload（2022.3.45f1）

`KnowledgeId`: `es.unity.lifecycle-domain-reload.v1`

`Authority`: `Unity 2022.3 official manuals + installed 2022.3.45f1 API documentation + project settings`

`RouteKeys`: `unity`, `monobehaviour`, `lifecycle`, `static-state`, `domain-reload`, `scene-reload`, `enter-play-mode`, `script-execution-order`

`ContentHash`: `3716bb8d5063c78d4f5b84cd21de05989a27add1c522d8a92cf2e9c2e4c7991e`

## Scope

本条目说明 ESFramework 当前 Unity `2022.3.45f1` 环境中，MonoBehaviour 回调、静态状态、
Domain Reload、Configurable Enter Play Mode 和脚本执行顺序之间的边界。目标是帮助后续 AI
判断初始化与清理代码应归属哪个阶段，并识别只在第二次进入 Play Mode 才暴露的状态泄漏。

本条目负责普通 Runtime `MonoBehaviour` 的初始化/启停/销毁、脚本域静态状态、Enter Play
Mode 两个 Reload 轴，以及不同 `MonoBehaviour` 派生类型的相对执行顺序。不负责以下对象：

- Unity 编译、Reload 完成、Player、IL2CPP/AOT 或发布证据，由
  `es.unity.compile-player-il2cpp-evidence.v1` 负责。
- `EditorWindow`、`SessionState`、Assembly Reload 事件和菜单恢复，由
  `es.unity.editor-window-lifecycle-menu.v1` 负责。
- Pool、资源 Scope、Entity、Operation 或请求 Lease 的项目运行时所有权，由对应 ESFramework
  专项条目负责；本条目只提供 Unity 生命周期底层条件。
- `ExecuteAlways`、Edit Mode 与 Prefab Stage 的对象归属和副作用边界，由
  `es.unity.execute-always-prefab-stage.v1` 负责。
- DOTS/Entities、热更新域和目标平台 Player 启动，不得从本条目外推为已覆盖。

## Trigger and routing

### 自然语言触发词

`Awake/OnEnable/Start 顺序`、`OnDisable/OnDestroy 清理`、`第二次 Play 重复回调`、
`静态事件累积`、`关闭 Domain Reload`、`关闭 Scene Reload`、`Enter Play Mode Options`、
`SubsystemRegistration 重置`、`DefaultExecutionOrder`、`Script Execution Order`、
`RuntimeInitializeOnLoadMethod 顺序`。

### 精确路由

- Canonical routeKeys：`unity`、`monobehaviour`、`lifecycle`、`static-state`、
  `domain-reload`、`scene-reload`、`enter-play-mode`、`script-execution-order`。
- 预期最小命中：普通 Runtime 生命周期问题只命中本条目；涉及“如何证明 Reload 完成”时再加入
  `es.unity.compile-player-il2cpp-evidence.v1`；涉及 EditorWindow 恢复时改路由到
  `es.unity.editor-window-lifecycle-menu.v1`。
- `knowledge` 是发现分类，不得单独作为选择本条目的充分条件。

### 相邻误路由与回退

| 误路由信号 | 不应继续使用本条目的原因 | 回退动作 |
|---|---|---|
| `compile`、`console`、`player`、`il2cpp`、`release` | 决策对象是证据层而非生命周期机制 | 切换或追加 compile/player evidence 条目 |
| `editor-window`、`session-state`、`menu-item`、`owner-lifecycle` | 决策对象属于 Editor 会话和窗口所有权 | 切换到 EditorWindow lifecycle 条目 |
| `pool`、`lease`、`operation`、`entity` | Unity 回调不能替代项目所有权协议 | 路由到对应项目 Runtime 条目，本条目只作底层 RequiredRead |
| 只有宽泛的 `lifecycle` | 可能指模块成熟度、Entity、Pool 或命令生命周期 | 从对象、动作和风险重新推导精确 routeKey；仍有歧义则停止并请求澄清 |

## Decision rules

### 可以继续

- 已固定 Unity 版本、目标对象是普通 Runtime `MonoBehaviour`，且相关 SourceRef 哈希仍匹配。
- 结论只涉及 Unity 官方定义的回调/Reload/执行顺序机制，或已明确标记为 Derived guidance。
- 只提出源码检查或测试设计，并保持 `runtime-not-run`，不声称 Unity 行为已经执行。

### 必须先读取额外来源

- 结论涉及具体 ESFramework 类型时，先读该类型当前源码、所有权入口、调用方和相关 AIWarnings。
- 结论涉及 EditorWindow、Pool、Prefab、序列化、AssetDatabase、资源 Scope 或 Player 时，先读对应
  canonical Knowledge、P0 和当前源码。
- 要声称 Reload/PlayMode/Player 实际通过时，必须有当前用户明确运行要求和本次运行回执；选用受管通道时再读取并校验匹配 AICommand、TaskContract。

### 必须停止或降级

- SourceRef 缺失、哈希漂移、Unity 版本/项目设置改变或来源互相冲突：标记 `stale`，停止使用旧结论，
  回读权威来源并重新规划。
- 目标需要 Unity/PlayMode 证据但没有当前授权或可验证回执：标记 `Blocked` 或
  `runtime-not-authorized`，不能以静态结果代替。
- 机制明确但实现对象、Owner 或生命周期入口尚未确认：标记 `Deferred`，列出必须补读的源码。
- 没有匹配 AICommand 时只报告真实 `NoMatchingCommand`；`planTask` 能力不可用时报告
  `PlanTaskUnavailable`。二者都不得用于扩大权限。
- 只发现文件、按钮、测试源码、旧日志或旧快照：证据不足，停止升级“已执行/已通过”结论。

## Evidence classification

- `Verified source facts`：当前项目版本与 Enter Play Mode 配置、Unity 生命周期回调顺序、
  Domain/Scene Reload 行为、静态状态重置要求以及 Script Execution Order 边界，均由下方
  `EvidenceRefs` 中逐字命中的 Unity 官方原文或当前项目设置支持。
- `Derived guidance`：显式协调器、幂等订阅、稳定键排序、四组合测试矩阵和所有权检查表，
  是从已验证机制推导出的工程建议，不冒充 Unity 官方逐字要求。
- 文中的 C# 片段是对 Unity 官方 `SubsystemRegistration` 重置模式的最小示例，不是
  ESFramework 当前源码，也不构成运行验证。

## 当前项目配置事实

- `ProjectSettings/ProjectVersion.txt` 固定 Editor 为 `2022.3.45f1`，revision 为
  `a13dfa44d684`。
- `ProjectSettings/EditorSettings.asset` 当前为 `m_EnterPlayModeOptionsEnabled: 0`。
  因此 Configurable Enter Play Mode 未启用，进入 Play Mode 时采用默认的 Domain Reload 和
  Scene Reload。
- 同文件中的 `m_EnterPlayModeOptions: 3` 在开关关闭时不生效。不能仅根据这个序列化值声称
  当前禁用了 Domain Reload 或 Scene Reload。

## 生命周期机制

### 首个场景与运行时实例

| 阶段 | 可依赖的事实 | 不可依赖的假设 |
|---|---|---|
| `Awake` | 新实例进入其初始化路径时的首个生命周期回调；同一对象上先于 `OnEnable`；启动时非激活对象会延迟到激活后；场景资产中的可初始化对象其 Awake/OnEnable 整体先于任何 Start | 不同对象的 Awake 与 OnEnable 之间存在全局确定顺序，或非激活对象已在启动时完成 Awake |
| `OnEnable` | 对象启用且激活时调用；同一对象上位于 Awake 之后、Start 之前 | 每次 OnEnable 都代表新实例，或只会调用一次 |
| `Start` | 实例启用时，在其第一次帧更新前调用；首场景对象的所有 Start 先于任何 Update | 运行时从 Update 中实例化的对象也能满足全场景 Start-before-Update |
| `OnDisable` | 对象/组件离开启用激活状态时用于撤销当前启用期拥有的订阅和句柄 | 只有对象销毁时才调用，或一定紧邻 OnDestroy |
| `OnDestroy` | 对象生命周期结束时执行最终的实例级清理 | 它能清理由静态所有者持有且跨实例/Play 循环保留的全部状态 |

跨对象依赖不应靠“某个 Awake 恰好先运行”。需要所有场景对象准备完成的工作，应进入
`Start` 或一个显式的协调/注册阶段；运行时动态实例仍需独立的就绪协议。

### 实例状态与静态状态

MonoBehaviour 回调管理实例的启用期和销毁期；静态字段、静态事件和静态缓存属于脚本域。
默认 Domain Reload 会为每次 Play 循环提供新的脚本状态，并重置静态字段与静态事件处理器。
禁用 Domain Reload 后，这些静态对象不会自动回到声明时的初始值：

- 静态计数器、缓存、单例引用和集合可能带入下一次 Play。
- 对静态事件重复执行 `+=` 会累积相同处理器，造成第二次及后续 Play 多次回调。
- 实例的 `OnDisable`/`OnDestroy` 不能代替静态域的统一重置，因为静态所有权可能独立于该实例。

运行时代码需要支持禁用 Domain Reload 时，应在
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 回调中把静态
字段恢复为可重复初始状态，并对静态事件采用“先 `-=`、再按需要 `+=`”或等价的幂等注册。
Editor-only 静态状态使用 Editor 对应的 Enter Play Mode 初始化入口，不能把 Editor API 放进
Player 运行程序集。

```csharp
using System;
using UnityEngine;

static class RuntimeSessionState
{
    internal static int Generation;
    internal static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForRuntimeStart()
    {
        Generation = 0;
        Changed = null;
    }
}
```

该模式只解决本类型拥有的静态状态。它不授权清空其他模块的状态，也不替代资源 Scope、
对象池、场景对象或外部原生资源各自的所有权清理。

## Enter Play Mode 的两个独立轴

### 版本校准决策卡（Unity 2022.3）

- 同一 `MonoBehaviour` 实例的基本顺序可按 `Awake -> OnEnable -> Start` 理解；首场景对象的
  `Start` 先于该场景的 `Update`。这不等于不同对象或同类型实例之间存在可依赖的全局顺序。
- `Script Execution Order` / `DefaultExecutionOrder` 只表达不同脚本类型的相对阶段，不能为同一
  类型的多个实例建立稳定排序，也不能替 `RuntimeInitializeOnLoadMethod` 回调排序。
- Domain Reload 与 Scene Reload 是独立开关，必须分别覆盖四种组合；关闭 Domain Reload 时静态
  字段/事件/缓存不会自动清空，关闭 Scene Reload 时场景实例和部分非序列化状态可能保留。
- `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 只负责本类型拥有的静态状态重置；它不
  能证明场景对象、资源 Scope、外部原生资源或 Editor 状态已经被清理。

| Domain Reload | Scene Reload | 主要风险 |
|---|---|---|
| 开 | 开 | 默认路径；最接近一次新的 Editor Play 启动，但仍不是 Player 发布启动证据 |
| 关 | 开 | 静态字段、静态事件和托管缓存跨 Play 保留；必须显式重置脚本域状态 |
| 开 | 关 | 脚本域刷新，但场景不从磁盘完整重载；Unity 会重置场景修改并模拟所需回调 |
| 关 | 关 | 静态状态与场景对象状态同时需要幂等恢复；最容易暴露重复订阅和旧引用泄漏 |

禁用 Scene Reload 时，Unity 仍会调用或模拟 `OnEnable`、`OnDisable`、`OnDestroy` 等初始化
相关回调，但启动时序和耗时不再等价于构建启动。带 `ExecuteAlways`/`ExecuteInEditMode` 的脚本
还有额外差异，不能把普通运行时 MonoBehaviour 的假设直接套用。

## 脚本执行顺序

- Project Settings 的 Script Execution Order 与 `DefaultExecutionOrder` 都表达不同
  MonoBehaviour 派生类型之间的相对顺序。
- 顺序按事件类别分别应用，例如先按配置顺序调用一批 Awake，之后再按相应顺序调用 Update；
  它不会把 Awake 与 Update 混成一条任意排序队列。
- 同一类型的多个实例之间没有可配置的稳定先后；需要稳定结果时应由一个显式协调器排序数据，
  而不是依赖 Hierarchy、创建时机或当前观察到的回调顺序。
- Script Execution Order 不影响 `RuntimeInitializeOnLoadMethod` 回调，Unity 不提供这些运行时
  初始化方法之间的用户排序。存在依赖时，应合并到单一入口或建立显式阶段调用链。
- 不应为了掩盖循环依赖而堆叠大量 execution-order 数字。执行顺序只适合表达少量、稳定、
  单向的框架阶段关系。

## Common AI failure modes

| 错误行为与典型症状 | 根因 | 预防检查与正确动作 | 失败恢复与缺失证据 |
|---|---|---|---|
| 依赖不同对象的 `Awake` 先后；首帧偶发空引用 | 把单对象保证扩大成全局顺序 | 列出依赖对象；移到 `Start` 或显式协调阶段 | 移除隐式顺序，补动态实例就绪测试 |
| 每次 `OnEnable` 都 `+=`；第二次 Play 多次回调 | 没有声明订阅 Owner 和幂等边界 | 检查建立/释放对称性和静态事件重置 | 先停止重复订阅，清理当前 Owner，补连续两次 Play 证据 |
| 只在 `OnDestroy` 清静态缓存或资源 | 混淆实例、脚本域和资源所有权 | 给每份状态标注实例/静态/Scope Owner | 回到真实 Owner 的 reset/release 入口，补取消和中断路径 |
| 看到 `m_EnterPlayModeOptions: 3` 就说 Reload 已关闭 | 忽略启用开关 | 同时读取 `m_EnterPlayModeOptionsEnabled` | 撤回配置结论，重新读取项目设置 |
| 用 Script Execution Order 排同类型实例 | 把类型相对顺序误当实例稳定顺序 | 检查依赖是否同类型、多实例、动态创建 | 改用稳定键排序或集中调度，补重复运行顺序证据 |
| 假设 `RuntimeInitializeOnLoadMethod` 可配置顺序 | 把 MonoBehaviour 类型排序套到运行时初始化方法 | 搜索多个初始化入口及其依赖 | 合并入口或显式阶段调用，补初始化重放 |
| 用默认 Reload 路径证明关闭 Reload 也安全 | 测试矩阵缺失 | 覆盖 Domain/Scene Reload 四组合和重复 Play | 保持 `runtime-not-run`，直到四组合回执齐全 |
| 用 PlayMode 结果证明 Player 启动 | 证据层越级 | 先声明目标证据层 | 路由到 Player evidence 条目，补目标平台回执 |
| 把 `ExecuteAlways`/EditorWindow 套入普通 Runtime 规则 | 作用域未检查 | 检查程序集、对象类型和执行环境 | 切换对应专项条目，撤回不适用结论 |
| 看到测试源码或旧日志就报告通过 | 把定义/产物存在当实际执行 | 要求本次 HEAD、配置、时间和结果绑定 | 标记 `not-run` 或 stale，重新执行匹配验证 |

## Execution checklist

### 开始前

1. 固定 ProjectRoot、branch、HEAD、Unity 版本、Enter Play Mode 设置和目标类型。
2. 读取 AIWarnings Start、CurrentStatus、RuleIndex，以及本任务命中的最小 P0/专项规则。
3. 验证本条目 SourceRefs、ContentHash 和索引绑定；失败即 stale/停止。
4. 写明 Owner：实例、场景、静态脚本域、Editor 会话、Pool/资源 Scope 或外部系统。

### 实施中

1. 写明建立、取消和释放点；订阅、句柄、Lease 与缓存必须有真实 Owner。
2. 初始化和清理保持幂等，覆盖重复启用、取消、中断和对象复用。
3. 跨对象顺序使用显式阶段；同类型实例使用稳定键或集中调度。
4. 不把实例回调当作静态域、Pool、Prefab、AssetDatabase 或资源 Scope 的通用清理入口。

### 完成后

1. 静态检查 Owner、生命周期对称、失败路径、重复执行和恢复路径。
2. 若获授权，连续进入 Play 至少两次，并覆盖 Domain/Scene Reload 四组合。
3. 需要 Player 一致性时补目标平台 Player 证据；PlayMode 不可替代。
4. 报告最高已证明证据层以及 `not-run`、`Blocked`、`Deferred` 和残余风险。

不可跳过：SourceRef/ContentHash 复算、目标 diff、严格 UTF-8、匹配证据层的后置验证。
明确禁止：借执行顺序掩盖循环依赖、无 Owner 的静态状态、用文件/测试存在冒充执行成功、
无 AICommand/TaskContract 扩权，以及把旧上下文或临时扫描写成长久事实。

## Evidence boundary and non-claims

- 本条目假定目标是普通运行时 MonoBehaviour；`ExecuteAlways`、EditorWindow、Prefab Stage、
  DOTS/Entities 和热更新域需要各自专项规则。
- 未启动 Unity Editor，未切换 Enter Play Mode Options，未执行 Domain Reload/Scene Reload，
  未运行 EditMode/PlayMode 测试，也未采集 Profiler、Player 或 IL2CPP 证据。
- `runtime-not-run`：本文只完成官方文档、本机 API XML 与项目设置的静态核对，不声明
  ESFramework 任一具体运行时类型已经满足上述模式。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/DomainReloading.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/ConfigurableEnterPlayMode.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/SceneReloading.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/class-MonoManager.html

## EvidenceRefs

- `Documentation/AIKnowledge/Unity/unity-lifecycle-domain-reload/authoritative-source-verification.receipt.json`; `sha256: a884c672431d97604c76a265bb09c9772588e0640c1e46a19cf97dcb8a35e5a5`

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `ProjectSettings/EditorSettings.asset` (`901bf7853e3e65491b8292b0f1e03737e25f578c9dd95e4a8ff6655c19a2d3f4`)
- `Documentation/AIKnowledge/Unity/unity-lifecycle-domain-reload/official-source-lock.md` (`428560f7dd4251050f6bb6b77914a6886657648e09952c4823dcdf0d9f0ed25e`)

`EvidenceLevel`: `S1`

`StaleWhen`: Unity 版本/revision、Enter Play Mode 项目设置、Unity 2022.3 官方页面响应、本机 API XML、生命周期/Domain Reload/Script Execution Order 合同或任一 SourceRef 哈希变化。
