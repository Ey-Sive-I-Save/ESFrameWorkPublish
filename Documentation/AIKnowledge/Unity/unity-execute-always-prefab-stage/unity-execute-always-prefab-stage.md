# Unity ExecuteAlways、Edit Mode 与 Prefab Stage 边界（2022.3.45f1）

`KnowledgeId`: `es.unity.execute-always-prefab-stage.v1`

`Authority`: `Unity 2022.3 official documentation + installed 2022.3.45f1 API documentation + AIWarnings P0`

`RouteKeys`: `unity`, `execute-always`, `execute-in-edit-mode`, `edit-mode`, `prefab-stage`, `prefab-mode`, `prefab-auto-save`, `application-is-playing`, `playing-world`

`ContentHash`: `4e61553946000bf12f49cf23d211d7c088f58dfb949026ad8e508333e345199b`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity 版本/revision、ExecuteAlways/Application.IsPlaying/PrefabStage API、Prefab Mode Auto Save 语义、编辑器资产写入 P0 或任一 SourceRef 哈希变化。

`RuntimeAcceptance`: `runtime-not-run`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`

## Scope

本条目负责 Unity `2022.3.45f1` 中 `ExecuteAlways`、`ExecuteInEditMode`、Edit Mode、
playing world 判断和 Prefab Stage 编辑上下文之间的决策边界。它帮助 AI 在执行任何副作用前
先判断对象属于运行世界、普通场景编辑态还是 Prefab Asset 编辑上下文。

本条目不负责：

- 普通 Runtime MonoBehaviour、Domain/Scene Reload 和 Script Execution Order；由
  `es.unity.lifecycle-domain-reload.v1` 负责。
- Prefab 正式保存、Apply/Revert、稳定身份和事务回滚；由
  `es.unity.editor-prefab-asset-transaction.v1` 负责。
- `SerializedObject`、Undo、Dirty 和多对象写入；由
  `es.unity.editor-serialized-undo-dirty.v1` 负责。
- GUID、local file ID、字段迁移和 Prefab 序列化身份；由
  `es.unity.serialization-prefab-identity.v1` 负责。
- EditorWindow、SessionState 和菜单恢复；由
  `es.unity.editor-window-lifecycle-menu.v1` 负责。

本条目拥有“当前对象能否执行 Play 副作用”和“Prefab Stage 是否改变写入风险”的稳定事实；
相邻条目只拥有实际写入事务、身份或窗口恢复，不复制本条目的 playing-world 判断。

## Trigger and routing

### 自然语言触发词

`ExecuteAlways`、`ExecuteInEditMode`、`编辑态执行`、`Edit Mode 回调`、`Prefab Stage`、
`Prefab Mode`、`预制体模式`、`Application.IsPlaying(gameObject)`、`playing world`、
`编辑 Prefab 时脚本自动改值`、`Prefab Auto Save`、`ExecuteAlways Update 不连续`。

### 精确路由

- Canonical routeKeys：`execute-always`、`execute-in-edit-mode`、`edit-mode`、
  `prefab-stage`、`prefab-mode`、`application-is-playing`、`playing-world`。
- 预期命中 1～3 条：机制判断首选本条目；发生正式 Prefab/字段写入时追加对应事务条目；
  只讨论普通 Play Mode 生命周期时回退到 lifecycle 条目。
- 可能误命中：宽泛 `prefab` 会进入序列化或资产事务，宽泛 `lifecycle` 会进入普通 Runtime，
  `editor-window` 会进入窗口恢复。必须用对象类型、执行环境和动作重新判定。
- 零命中或只命中相邻条目时，回到 AIBRAIN/KnowledgeIndex，以 `execute-always` 或
  `prefab-stage` 重规划；不得把普通 MonoBehaviour 摘要当替代来源。

## Decision rules

### 可以继续

1. Unity 版本、目标对象、程序集和当前上下文已确定，SourceRefs/ContentHash 未漂移。
2. 只做静态机制判断、代码审查或测试设计，并明确保持 `runtime-not-run`。
3. 副作用前已使用对象级 playing-world 判断，且编辑态分支是无副作用预览或进入正式写入事务。

### 必须先读取额外来源

1. 修改 Prefab Asset、场景、序列化字段或共享资源前，读取 Prefab transaction 与
   Serialized Undo/Dirty canonical 条目、当前源码和命中 P0。
2. 依赖具体 ESFramework `ExecuteAlways` 类型时，读取该类型、全部写入调用和相关测试；
   Knowledge 摘要不能证明其实现安全。
3. 要声明 Prefab Stage、Undo、Save、Domain Reload 或 PlayMode 实际通过时，必须取得当前
   AICommand/TaskContract 和对应 Unity 运行回执。

### 必须停止、Deferred 或 Blocked

1. 无法判断对象是否属于 playing world、Prefab Stage 或普通场景编辑态：停止副作用并标记
   `Deferred`，先补上下文检测。
2. Edit Mode 路径可能写正式资产但没有 Undo、Dirty、Save、Rollback 和幂等合同：标记
   `Blocked`，不得以“只是预览”继续。
3. SourceRef 缺失、哈希漂移、Unity 版本变化或官方资料冲突：条目标记 `stale` 并重新取证。
4. 只有文件、属性、按钮、测试源码或旧截图存在：保持 `runtime-not-run`，不得升级为可用性结论。
5. `planTask` 不可用时报告 `PlanTaskUnavailable`；没有匹配命令时报告
   `NoMatchingCommand`，两者都不扩大权限。

## Verified facts

| 事实 | 类型 | 来源 |
|---|---|---|
| `ExecuteAlways` 使脚本实例在 Play Mode 和编辑态都执行 | Unity API 事实 | `official-source-lock.md`：installed Core API XML |
| `Application.IsPlaying(Object)` 判断给定对象是否属于 playing world | Unity API 事实 | `official-source-lock.md`：installed Core API XML |
| 未区分 playing world 时，Play 逻辑可能修改并保存 Prefab Mode 中的对象 | Unity 官方警告 | `official-source-lock.md`：ExecuteAlways 官方页 |
| 非 playing-world 对象的 `Update` 不是持续每帧调用，而是在 Scene 发生变化时调用 | Unity 官方机制 | `official-source-lock.md`：ExecuteAlways 官方页 |
| Prefab Stage 是 Prefab Asset 的编辑上下文，可处于 isolation 或 context 模式 | Unity Editor API 事实 | `official-source-lock.md`：installed Editor API XML |
| `GetPrefabStage(GameObject)` 可解析对象所属 Prefab Stage | Unity Editor API 事实 | `official-source-lock.md`：installed Editor API XML |
| Prefab Mode 中的更改影响该 Prefab 的所有实例；Auto Save 默认开启并会自动写回 Prefab Asset | Unity 官方机制 | `official-source-lock.md`：Prefab Mode manual |
| 对某 Prefab Asset 的编辑只能在仍处于该 Prefab Mode 时撤销；退出该 Prefab Mode 后，相关编辑不再存在于 Undo history | Unity 官方机制 | `official-source-lock.md`：Prefab Mode manual |
| 编辑器资源与正式写入仍受 ES 的生命周期、Undo、Dirty、Save 和运行证据门禁约束 | AIWarnings P0 | `编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` |

以下是 Derived guidance，不冒充 Unity 官方逐字合同：在任何副作用前先分类
`PlayingWorld / SceneEdit / PrefabStage`；编辑态预览应可重复、可撤销、无泄漏，并与正式资产提交分离。

### 联网复核后的高危决策场景

场景：`[ExecuteAlways]` 组件在 Prefab Stage 中维护编辑态预览或缓存，同时用户可能开启
Prefab Auto Save、进入 Play Mode、重复修改 Inspector、关闭 Stage 或触发 Domain Reload。

| 当前目标状态 | 允许动作 | 必须停止或转交的动作 |
|---|---|---|
| `Application.IsPlaying(target) == true` | 只进入该目标的 Runtime 路径 | 不得因为 Editor 中还开着 Prefab Stage 就混入编辑态写入 |
| 目标不在 playing world，且 `GetPrefabStage(targetGameObject) == null` | 只执行无持久化副作用、可重复的 SceneEdit 预览 | 序列化字段、共享资源或场景写入必须转交 Serialized/Undo/Dirty 事务 |
| `GetPrefabStage(targetGameObject) != null` | 只执行与正式资产提交分离的 PrefabStage 预览 | 禁止从 `OnEnable`、`Update`、`OnValidate` 隐式提交；Auto Save 开启时先按潜在已落盘处理 |
| 无法解析目标、Stage 或 Owner | 无 | 立即 `Deferred`；不得用全局 Play 状态、当前 Stage 或旧缓存猜测归属 |

高危判定顺序：先判断目标对象是否属于 playing world，再判断该目标所属的 Prefab Stage。
`GetCurrentPrefabStage() != null` 只说明编辑器当前存在 Stage 上下文，不能证明任意目标对象属于该
Stage；必须使用对象级 `GetPrefabStage(targetGameObject)`，必要时再用
`IsPartOfPrefabContents(targetGameObject)` 确认目标属于已加载的 Prefab 内容。

对于“预览缓存”这一模糊说法，先分类缓存是否会进入序列化数据、Prefab 内容、共享 Material、
AssetDatabase 或场景层级。任一答案为是，都不再属于无副作用预览，必须追加对应 canonical 写入
条目和真实回滚证据。不得仅凭 `HideAndDontSave`、非公开字段或“之后会清理”推断不会污染资产。

### 共享 Material 场景决策卡

- `renderer.sharedMaterial`、Prefab 序列化材质字段或 `AssetDatabase` 写入都按正式资产副作用处理；
  `ExecuteAlways` 的 `OnEnable`、`Update`、`OnValidate` 不能作为隐式提交入口。
- 如果目标只是编辑器视觉预览，优先评估 `Renderer.SetPropertyBlock` + `MaterialPropertyBlock` 的
  逐 Renderer 临时覆盖；这不等于项目已经证明可用，也不能绕过 `Application.IsPlaying(target)`、
  Prefab Stage 归属、Owner/幂等和运行时证据门禁。
- `MaterialPropertyBlock` 方案必须单独记录 SRP Batcher 取舍并以目标 Unity/渲染管线实测；不能把
  “不改 Material Asset”直接升级成“性能更好”或“合批不受影响”。
- Auto Save 为开启、关闭或未知时，退出 Prefab Stage 前都先审计 Material/Prefab/实例差异；
  未完成正式事务或回滚回执时保持 `Blocked`，不能依赖退出 Stage 后的 Undo。
- Unity 2022.3 的 Prefab Mode 中，Auto Save 开启会自动写回 Prefab Asset；关闭时退出阶段仍可能
  需要处理未保存变更。两种状态都不能把共享 Material 写入当作无副作用预览，且 Prefab 资产
  变化可能影响该 Prefab 的所有实例。
- 最小可接受场景矩阵：playing world / 普通 SceneEdit / Prefab isolation / Prefab in-context，
  Auto Save 开关，重复 Inspector 修改，Stage 关闭重开，Domain Reload，Undo/Redo，资产 diff 和
  回滚。缺任一与目标副作用直接相关的回执，只能保持 `runtime-not-run`。

## Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作、恢复与缺失证据 |
|---|---|---|---|
| 只检查 `Application.isPlaying` | Play Mode 中打开 Prefab Stage 时执行游戏副作用；把 Editor 全局状态当对象归属 | 副作用前检查目标对象的 `Application.IsPlaying(target)` | 停止写入，恢复受影响资产；按对象世界分支；仍需 Prefab Stage 实测 |
| 只检查 `GetCurrentPrefabStage()` | 当前打开了 Stage，但目标可能属于 playing world、普通场景或另一个上下文；把编辑器焦点当对象归属 | 对目标 `GameObject` 调用 `GetPrefabStage`，必要时检查 `IsPartOfPrefabContents` | 停止目标写入，丢弃由当前 Stage 推导的缓存并重新解析 Owner；仍需 Stage 开关与目标切换实测 |
| 把 `ExecuteAlways` 当普通 MonoBehaviour | 编辑 Prefab 时运行初始化、生成对象或改字段 | 搜索所有回调和写操作，先分类上下文 | 把 Play 逻辑限制到 playing world；编辑态只保留显式预览/作者动作 |
| 假设 Edit Mode `Update` 每帧运行 | 预览刷新偶发、状态陈旧；套用 Runtime 帧循环 | 检查刷新是否依赖连续 Update | 使用明确的编辑器刷新/变更驱动入口；仍需实际重绘和交互证据 |
| playing/non-playing 实例共享静态状态 | Prefab Stage 操作污染 Play 实例或反向污染 | 列出静态字段、事件、单例和缓存 Owner | 按世界/稳定上下文隔离或移除共享；重载后重新验证 |
| 把 Prefab Stage 对象当场景实例 | 保存后所有 Prefab 实例异常变化 | 用 `GetPrefabStage(target)` / `IsPartOfPrefabContents` 确认归属 | 停止场景逻辑，回到 Prefab 事务；审计 Auto Save 已写入内容 |
| 在回调中直接写序列化字段 | Undo 不可用、Dirty/Save 不明确、重入反复写 | 检查写入是否走 Serialized/Undo/Dirty 合同 | 切换 canonical 写入事务，恢复前先保留当前差异；仍需 Undo/Save 实测 |
| 退出 Prefab Mode 后才尝试 Undo 自动副作用 | 对该 Prefab 的相关编辑已离开 Undo history，无法按原计划撤销；把 Undo 误当成跨 Stage 的持久回滚日志 | 关闭 Stage 前审计当前差异、Auto Save 状态和可重读基线，并在仍处于该 Prefab Mode 时完成 Undo 或显式回滚 | 停止后续写入；从版本控制、备份或已锁定基线恢复并重读资产；没有恢复来源时保持 `Blocked`，不得声称已回滚 |
| 回调反复创建或销毁对象 | Hierarchy 抖动、资源泄漏、每次刷新重复副作用 | 要求稳定 Owner、幂等 key 和创建/释放对称 | 停止自动生成，清点 Owner 后最小恢复；补重复刷新测试 |
| `OnValidate`/编辑回调自触发写回 | Inspector 修改后递归刷新或持续变脏 | 检查写入是否改变自身依赖输入 | 分离校验与提交，加入重入保护但不隐藏真实循环；补连续编辑证据 |
| 忘记 Domain Reload/Stage 关闭退订 | 回调倍增、旧对象引用、Stage 重开后误写 | 核对订阅/释放与稳定身份恢复 | 先释放旧订阅再注册；失效对象停止并重新解析；补 Reload/关闭重开测试 |
| 用静态编译或文件存在报告可用 | 没有实际 Prefab/Undo/Save 行为证据 | 要求当前 HEAD、对象、动作、结果和回执 | 降级为 S1/`runtime-not-run`；申请 Unity 验证后再升级 |

## Execution checklist

### 开始前

1. 读取 AGENTS、AIBRAIN、Start/CurrentStatus/RuleIndex、本条目及 requiredReads。
2. 固定 Unity 版本、目标类型、程序集、对象、Stage、Play 状态和预期副作用。
3. 验证 SourceRefs、ContentHash 和 KnowledgeIndex；漂移即停止。
4. 声明 Owner、稳定身份、预览数据、正式资产和回滚边界。

### 实施中

1. 每个副作用入口先判断对象级 playing world 和 Prefab Stage 归属。
2. 运行逻辑、编辑态预览和正式作者写入使用独立路径。
3. 写入正式对象时转入 Serialized/Undo/Dirty/Prefab transaction，不在本条目复制实现。
4. 所有订阅、临时对象、缓存和延迟任务保持幂等并有对称释放。
5. 覆盖取消、Stage 关闭、Domain Reload、重复刷新和目标失效路径。
6. Stage 关闭前完成差异审计，并在仍处于该 Prefab Mode 时完成所需 Undo/回滚；不得把退出 Stage 后的 Undo 当恢复方案。

### 完成后

1. 静态检查不存在无条件 Play 副作用、无 Owner 静态共享和回调内隐式资产提交。
2. 验证 route 只带入本条目及真正需要的写入/生命周期邻居。
3. 若获授权，在隔离 Prefab fixture 中覆盖 isolation/context、Auto Save 开/关、Undo/Redo、
   Stage 关闭重开、Domain Reload 和进入/退出 Play Mode。
4. 验证关闭 Stage 前的差异审计与恢复路径；关闭后只允许按已锁定基线重读验证，不假定 Undo history 仍保留。
5. 报告最高证据层、未运行项、失败恢复和残余资产污染风险。

不可跳过：SourceRef/ContentHash、严格 UTF-8、目标 diff、路由探针和索引闭包。

明确禁止：用全局 Play 状态替代对象归属；在不明 Stage 中执行 Play 副作用；无事务写正式资产；
把静态检查写成 Unity 可用；AI 在当前用户未点名时自行扩大 Unity、资产、Git 或发布动作。AICommand/TaskContract 只约束受管通道。

## Evidence boundary

### Static 已证明

- 当前项目声明 Unity `2022.3.45f1`。
- 5 个 Unity 2022.3 官方页面在本次取证返回 HTTP 200，响应哈希已锁定。
- 本机 Unity Core/Editor API XML 的版本和哈希已锁定，并含本条目引用的 API 描述。
- 本条目的路由、来源、ContentHash 和执行约束可由静态验证器检查。

### Runtime 尚未证明

- 未启动 Unity、进入 Prefab Mode、切换 Auto Save、触发 Domain Reload 或 Play Mode。
- 未验证任何当前 ESFramework `ExecuteAlways` 类型、Prefab Asset、Undo/Redo、Dirty、Save 或回滚。
- 未证明 isolation/context、Stage 关闭重开、静态隔离或编辑器性能。
- `runtime-not-run` 不影响本条目的 S1 静态闭包，但禁止 RuntimeAcceptance/ReleaseAcceptance 声明。

## Official documentation

- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ExecuteAlways.html
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application.IsPlaying.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/EditingInPrefabMode.html
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.PrefabStage.html
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.PrefabStageUtility.GetPrefabStage.html

## EvidenceRefs

- `Documentation/AIKnowledge/Unity/unity-execute-always-prefab-stage/authoritative-source-verification.receipt.json`

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)
- `Documentation/AIKnowledge/Unity/unity-execute-always-prefab-stage/official-source-lock.md` (`f4f52c312d95c86bc201dc1be1990b2403a3d9e009a702482116f99396fdd822`)
