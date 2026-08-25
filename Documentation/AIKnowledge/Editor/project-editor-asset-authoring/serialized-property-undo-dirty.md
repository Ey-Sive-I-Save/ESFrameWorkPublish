# SerializedProperty、Undo 与 Dirty 写入合同

`KnowledgeId`: `es.unity.editor-serialized-undo-dirty.v1`

`Topic`: Unity SerializedObject/SerializedProperty、Undo、Dirty 与 Prefab override 写入合同

`Summary`: 约束单目标和多目标序列化写入的数据流、Undo 分组、Dirty、Prefab override 与异常回滚职责。

`Authority`: `Unity 2022.3 official documentation + AIWarnings + current source`

`RouteKeys`: `editor`, `serialized-object`, `serialized-property`, `multi-object`, `undo`, `dirty`, `prefab-override`, `rollback`

`ContentHash`: `754bd941ebab6f9fdfdbce0847c99936237fcec1b4cbf4a2f2a1096d5c1e9962`

`EvidenceLevel`: `S1`

`StaleWhen`: Unity Editor 版本、任一 UnityOfficialReferences 响应内容哈希、Unity SerializedObject/Undo/Dirty/Prefab override 合同、ES 多目标写入辅助器、相关测试定义或任一 SourceRef 哈希变化。

`RuntimeAcceptance`: `runtime-not-run`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`、`Documentation/ES_EDITOR_WINDOW_PRODUCTION_STANDARD.md`

## Scope

本条目负责 SerializedObject/SerializedProperty 数据流、单目标和多目标 Undo、Dirty、Prefab instance override 以及一次用户操作内的异常回滚。它不负责 Prefab Asset 创建、GUID/local file id 身份迁移、Workbench Draft 或 Scene 保存。

- Prefab Asset 创建和分阶段提交归 `es.unity.editor-prefab-asset-transaction.v1`。
- GUID、local file id、字段迁移和嵌套 Prefab 身份归 `es.unity.serialization-prefab-identity.v1`。
- EditorWindow 生命周期和菜单归 `es.unity.editor-window-lifecycle-menu.v1`。

## Trigger and routing

- 自然语言触发：Inspector 字段修改、多选编辑、mixed value、SerializedProperty、Undo/Redo、Dirty、Prefab override、批量写入失败回滚。
- 精确 routeKeys：`serialized-object`, `serialized-property`, `multi-object`, `undo`, `dirty`, `prefab-override`, `rollback`。
- 默认命中本条目；任务涉及创建/覆盖 Prefab Asset 时追加 Prefab 事务条目；涉及字段迁移或持久身份时改读序列化身份条目。
- 误路由回退：若目标是 Draft、Scene、Prefab Asset 或运行时对象所有权，停止套用本条目的写入表，回到索引按权威对象重新路由。

## Decision rules

1. SourceRef/官方响应哈希漂移、目标集合或 property path 无法完整解析时，标记 `stale` 或 `Blocked`，不得进入写阶段。
2. 属性型编辑默认使用 SerializedProperty；直接字段写入只有在目标和 API 边界明确时允许，并必须补齐 Undo、Dirty、Prefab override 和保存策略。
3. 多目标事务必须先验证全部目标，再创建单一 Undo group；任何目标失败都不得留下部分提交。
4. Dirty、Undo、磁盘保存和 Prefab override 是四个独立状态；缺少其中任一所需状态时不能声明完成。
5. 实际修改 Inspector、资产、Prefab 或 Scene 由当前用户明确目标授权；选用受管通道时再取得匹配 AICommand/TaskContract。测试源码存在不能提升 Runtime 证据。

## Core conclusion

属性型编辑默认走 `SerializedObject` / `SerializedProperty`，因为它同时承载序列化字段定位、多目标编辑、Undo、Dirty 和 Prefab override 语义。直接字段写入只有在明确知道目标和特殊 API 边界时才可采用，并必须分别补齐 Undo、Dirty、Prefab override 与失败恢复。

## Unity 数据流

1. 创建或取得绑定正确目标集合的 `SerializedObject`。
2. 若实例跨帧持有，在读取前调用 `Update()` 或适合的增量更新入口；多个指向相同目标的数据流彼此独立，必须显式同步。
3. 通过稳定 property path 解析 `SerializedProperty`，检查缺失字段、类型、数组和多目标 mixed value。
4. 写入属性值并调用 `ApplyModifiedProperties()`。未 Apply 的值可能在下一次 Update 时丢失。
5. 属性 setter 不是 SerializedProperty 的强制通道；需要数据不变量时使用序列化验证、`OnValidate` 或显式提交前验证，不能依赖普通 C# setter。

多目标读取时，`SerializedProperty` 的值 getter 只返回第一个目标的值；`hasMultipleDifferentValues` 才能表达 mixed value。写入会作用于全部绑定目标，因此批量操作必须在修改前完成目标集合和字段兼容性预检。

## Undo 与 Dirty 决策

| 修改通道 | Undo | Dirty | Prefab instance override |
|---|---|---|---|
| Inspector 中的 SerializedProperty | `ApplyModifiedProperties` 纳入 Undo | 自动处理 | 自动使用正确 override 样式/记录语义 |
| 直接修改普通属性 | 修改前 `Undo.RecordObject(s)` | Undo 通常同时标脏；按对象类型核对 | 修改后显式 `RecordPrefabInstancePropertyModifications` |
| 不需要 Undo 的直接修改 | 不创建 Undo | `EditorUtility.SetDirty` | 若可能是 Prefab instance，仍显式记录 override |
| 创建/销毁/加组件/改父级 | 使用专用 Undo API | 由对应操作核对 | 不能用普通 `RecordObject` 替代 |

`SetDirty` 只表达对象需要保存，不代表已写盘，也不自动提供 Undo。对支持 Undo 的用户操作，不应把 `SetDirty` 当成 `Undo.RecordObject` 的替代品。

## ES 多目标事务辅助器

`ESEditorSerializedMutation.TryApply` 的职责是一次用户触发的多目标提交：

- 提交前解析全部 target 与 `SerializedObject`，任一失效则不进入写阶段。
- 创建单一 Undo group，并对全部目标注册 complete-object Undo。
- 逐目标执行 mutation 和 `ApplyModifiedProperties`；全部成功后统一 Dirty、Prefab instance modifications、视图刷新并折叠 Undo group。
- 任一 mutation、Apply 或刷新失败时，回退整个 Undo group，再 `Update` 所有数据流并刷新视图；回滚失败和刷新失败分别进入错误文本。

这是一种项目级批量回滚策略，不是对所有单字段 Inspector 的通用模板。调用方仍负责 property path、输入合法性、目标类型、mixed value、外部漂移和保存策略。

## Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作、恢复与缺失证据 |
|---|---|---|---|
| 只改第一个多选目标 | UI 看似一致但其他目标未提交；忽略 mixed value | 验证 targetObjects 和字段兼容性 | 全量预检后统一提交；失败回滚全部；仍需多选 Inspector 实测 |
| 写值后忘记 Apply | 下一次 Update 丢失修改 | 检查每个 SerializedObject 的 Apply 结果 | Apply 后再进入后处理；失败回退 Undo group 并 Update |
| 用 SetDirty 替代 Undo | 资产可保存但用户无法撤销 | 按修改通道核对 Undo API | 修改前 Record；创建/销毁/父级使用专用 Undo；仍需 Undo/Redo 回放 |
| 直接改 Prefab 实例却不记录 override | 重载或保存后值丢失/不显示覆盖 | 检查是否属于 Prefab instance | 修改后记录 property modifications；失败后重读实例与源 |
| 批量处理中途异常仍继续 | 部分目标已变、部分未变 | 强制 `ValidateAll` 和单 Undo group | `RollbackAll -> UpdateAll -> RefreshAll`；回滚失败单独报告 |
| 把测试定义写成测试通过 | EvidenceLevel 被夸大 | 查找本次 Test Runner XML/回执 | 无回执保持 `definition-only` 和 `runtime-not-run` |

## Execution checklist

```text
开始前：读 Start/CurrentStatus/RuleIndex -> 验证 SourceRef -> 锁定全部目标、property path、对象类型和权限
实施中：Update -> ValidateAll -> RecordAll -> Mutate/ApplyAll -> Dirty/override/PostProcessAll
失败时：停止后续目标 -> 回滚 Undo group -> UpdateAll -> RefreshAll -> 分别报告回滚与刷新失败
完成后：核对 mixed value、Undo/Redo、Dirty、Prefab override、保存策略和外部漂移
不可跳过：Unity Test Runner + 多选 Inspector + Undo/Redo + Prefab override + 失败注入
禁止：部分成功后继续；用 SetDirty 代替 Undo；用静态源码或测试文件冒充运行通过
```

## Evidence boundary

### 已验证事实

- 当前项目版本为 Unity `2022.3.45f1`。
- Unity 官方文档明确：SerializedObject 可编辑一个或多个 Unity 对象，跨帧持有时必须同步；Apply 负责提交；Undo.RecordObject 必须在直接修改前调用；Prefab instance 的直接修改还要记录 property modifications。
- 当前 ES 源码存在单 Undo group、多目标提交和异常回滚实现；测试源码定义了成功 Undo、后续目标失败回滚、刷新失败回滚三类案例。

### 推导

- 一个批量编辑事务至少需要 `ValidateAll -> RecordAll -> Mutate/ApplyAll -> PostProcessAll -> RollbackAll`，否则中途失败会留下部分目标已提交。
- Dirty、Undo、磁盘保存和 Prefab override 是四个不同状态，任何一个都不能替代其余三个。

### 非声明

- `runtime-not-run`：本次未运行 Unity Test Runner，未实际执行 Undo/Redo、多选 Inspector、Prefab override 或失败注入。
- 测试源码存在只证明有可运行的测试定义，不证明当前 Unity 版本下已经通过。
- 未验证数组、多态托管引用、丢失脚本、Prefab Stage、嵌套 Prefab 或跨窗口并发编辑。

## UnityOfficialReferences

以下响应在 2026-08-23 返回 HTTP 200；响应内容哈希不参与本地 `ContentHash`。

| Unity 2022.3 官方文档 | SHA-256 |
|---|---|
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.html | `db76fae1f10d348c4bec39d964cb9f13aff5a5d524968cc17be4e9648486732d` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.ApplyModifiedProperties.html | `3d529a357c10585028a4bef767223142fe039817babf011be29f4b45a5f51665` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RecordObject.html | `64e22af38a58cc39f0ff2d8b1fe2723dad1a9cd147240ae772fd6b4cc721c107` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorUtility.SetDirty.html | `c36eeae8ad4e94915664e6c3df10a021de8ea93a6a78ec8777cd447dfc0d36f2` |
| https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.RecordPrefabInstancePropertyModifications.html | `f85e013cac748b171f1d2e6c86332cb31ae4ef637807605bddd5f83b094bb5f7` |

## EvidenceRefs

- `StaticReview`: 已交叉读取 Unity 2022.3 官方文档、AIWarnings 与 `ESEditorSerializedMutation` 当前源码。
- `TestDefinition`: `Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/ESEditorSerializedMutationTests.cs` 定义成功 Undo、后续目标失败回滚和刷新失败回滚案例；本次未运行这些测试。
- `Runtime`: `runtime-not-run`；没有 Unity Test Runner、Undo/Redo、多选 Inspector 或 Prefab override 回执。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)
- `Documentation/ES_EDITOR_WINDOW_PRODUCTION_STANDARD.md` (`88ce371c61194569d3a3738ec2c35e89b1ec5315d1e8b6d44e9c5313fb93b81e`)
- `Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSerializedMutation.cs` (`67f4e4077bb7cd504f4b22a2a926c72ce03bf3cd4370a9feeca1b5c0e8404091`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/ESEditorSerializedMutationTests.cs` (`03b377ada8b7d43385c58bf93401637220d96642202a98f86c12999b0d71fb40`)
