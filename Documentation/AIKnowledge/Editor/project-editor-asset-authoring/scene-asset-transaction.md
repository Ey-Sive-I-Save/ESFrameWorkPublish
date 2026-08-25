# Unity Editor Scene 资产事务

`KnowledgeId`: `es.unity.editor-scene-asset-transaction.v1`

`Topic`: 通用 Scene 作者操作、Active/Additive 所有权、Dirty/Save、现场恢复与重开验证

`Authority`: `Current project source + AIWarnings P0`

`RouteKeys`: `editor`, `scene-asset`, `editor-scene-manager`, `active-scene`, `additive-scene`, `scene-setup`, `unsaved-scene`, `mark-scene-dirty`, `save-scene`, `close-scene`, `restore-scene`, `rollback`

`ContentHash`: `df932f1d7c8c41dad4cfad7a7f6a97a2405c732d5e0c41841427511dfd120147`

`EvidenceLevel`: `S1`

`RuntimeAcceptance`: `runtime-not-run`

`StaleWhen`: Unity 版本、EditorSceneManager/SceneManager 合同、受管 Scene 修改控制、Scene Builder、Materializer、测试场景权威规则或任一 SourceRef 哈希变化。

`RelatedSkills`: `es-editor-tooling`, `es-test-fixture-authoring`

`RequiredReads`: `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`、`Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`、`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`

## Scope

本条目负责非 PlayMode 的通用 Scene 作者事务：识别明确目标 Scene、保护用户已加载/未保存现场、选择 Single/Additive、切换 Active Scene、建立 Undo/Dirty、检查保存结果、关闭临时 Scene、恢复现场并设计重开验证。

本条目不负责：

- 测试场景的 Builder/Fixture 布局权威、Prefab override 分类和备份分层；由 `es.unity.editor.project-scene-builder-authority.v1` 负责。
- Prefab Asset 的创建和保存；由 `es.unity.editor-prefab-asset-transaction.v1` 负责。
- SerializedProperty 多目标编辑；由 `es.unity.editor-serialized-undo-dirty.v1` 负责。
- PlayMode、运行时 Scene 加载、Player、Profiler 或发布验收。

## Trigger and routing

- 自然语言触发：创建/修改/保存 Unity Scene、切换 Active Scene、Additive 作者场景、临时 Fixture Scene、场景 Dirty、关闭并恢复场景、保护未保存场景、Scene 重开验证。
- 精确 routeKeys：`scene-asset`, `editor-scene-manager`, `active-scene`, `additive-scene`, `scene-setup`, `unsaved-scene`, `mark-scene-dirty`, `save-scene`, `close-scene`, `restore-scene`。
- 预期命中：本条目；若目标是测试 Builder 或 Prefab，再追加对应 canonical 条目，最多三条。
- 相邻误命中：`scene-validation`/`scene-builder` 转测试场景权威；`runtime scene loading` 转运行时领域；`fixture-scene` 若由 Materializer 生成，追加其专属条目。
- 回退：无法唯一确定目标 Scene、作者源、用户现场所有者或生成输出所有者时停止，不依赖当前 active Scene 猜测。

## Decision rules

### 可以继续

1. 已记录开始时的 Scene 现场：加载顺序、路径、是否加载、Active Scene、每个 Scene 的 Dirty/未保存状态，以及需要恢复的 Selection/编辑上下文。
2. 目标 Scene 由精确项目相对路径或当前事务创建的 Scene handle 标识；禁止仅凭显示名或“当前 active”选择。
3. 已明确 Single/Additive 的理由。交互式作者工具默认保护现有现场；只有明确允许替换当前现场或 BatchMode 隔离任务才能选择 Single。
4. 已定义 Undo、Dirty、保存和关闭策略；`SaveScene` 的布尔结果必须检查。
5. 已定义保存后的重载核对，以及失败/取消时是否关闭临时 Scene、恢复原 Active Scene 和完整加载现场。关闭重开只能作用于隔离 fixture、事务创建的 Scene，或用户明确授权用于重开验收的目标。

### 必须先读取额外来源

- 测试 Scene/Builder：读取 Scene Builder canonical 条目、对应 AIWarnings 和 Builder 源码。
- Prefab 实例或 override：读取 Prefab 事务及持久身份条目。
- Fixture/视觉捕获：读取 Materializer 与视觉证据条目；截图不能代替 Scene 持久化验证。
- 业务正式场景：读取实际作者工具和消费者，确认 Scene 是权威输入还是可重建输出。

### 必须停止

- 存在有内容的未保存 Scene，用户意图不明，或者任何已加载 Dirty Scene 可能被 Single 模式替换。
- 目标路径为空、位于 `Assets/` 外、与其他 Scene 冲突，或 Active Scene 在预检后变化。
- PlayMode/即将进入 PlayMode、编译/导入或并行 Scene 作者任务状态不明。
- `SetActiveScene`、`SaveScene`、关闭、重载或后置核对失败。
- 回滚只能恢复部分现场；必须报告 Partial，不得继续宣称原现场完整恢复。

### Stale、Deferred 和 Blocked

- SourceRef、Unity 版本或 Scene API/作者入口变化：`stale`，回读并重新规划。
- 未执行 Unity 保存、关闭和重开：持久化结论保持 `Deferred`/`runtime-not-run`。
- 缺少当前用户明确的资产写入/Unity 动作，或计划超出其范围：`Blocked`。AICommand、TaskContract 缺失只阻断选中的受管通道；用户已经明确要求时不再索取项目内部二次确认。
- AIBrain `planTask` 不可用：报告 `PlanTaskUnavailable`，不能冒充 `NoMatchingCommand`。

## Scene transaction state machine

```text
Capture
  -> ValidateTargetAndAuthority
  -> ProtectDirtyAndUnsavedScenes
  -> CreateOrOpenExplicitScene
  -> SetActiveAndVerify
  -> ApplyWithinUndoBoundary
  -> MarkSceneDirty
  -> SaveAndCheckResult
  -> Import/ReloadAndVerify
  -> CloseTemporaryScene
  -> RestoreOriginalSetupAndSelection
  -> ReportPerStageEvidence
```

任一阶段失败都进入 Recovery，不得跳到 Completed。保存成功后恢复失败仍是 `CompletedWithRecoveryFailure` 或 `Partial`，不是完全成功。

## Ownership and operation matrix

| 场景状态/意图 | 默认动作 | 必须保留的现场 | 禁止行为 |
|---|---|---|---|
| 已保存作者 Scene 正在编辑 | 以 Additive 打开/创建目标 Scene | 原加载集合、Active、Dirty、Selection | Single 隐式替换 |
| 空且无根对象的未命名 scratch Scene | 可在明确条件下使用 Single | 记录判定依据 | 仅以 `isDirty` 判断它是否有人工作 |
| 有内容的未保存 Scene | 停止并请求用户保存/关闭决策 | Scene handle、根对象、Dirty 状态 | 自动丢弃或覆盖 |
| BatchMode 隔离生成 | 可使用 Single，但目标和产物仍须显式 | 输入、目标路径、生成回执 | 把 BatchMode 当运行验收 |
| 修改当前已保存 Active Scene | 精确核对路径和稳定目标后修改 | Undo group、Dirty、原值 | 名称/Hierarchy 模糊匹配 |
| 临时 Additive Scene | `finally` 中关闭并恢复现场 | 原 Active Scene 和加载集合 | 只恢复 Active，不检查遗留 Scene |

## Verified facts

| 静态事实 | SourceRef |
|---|---|
| 受管 Scene 修改控制要求请求路径精确匹配当前已保存的 `Assets/...` Active Scene，并拒绝临时、包内、PlayMode 和歧义目标。 | `ESAutomationUnityEditorControl.cs` |
| 同一控制先解析稳定目标并建立 Undo，应用后记录 Prefab instance modification、SetDirty 和 MarkSceneDirty；请求保存时检查 `SaveScene` 返回值，异常时回滚 Undo group。 | `ESAutomationUnityEditorControl.cs` |
| 玩家控制器测试 Builder 区分空 scratch Scene 与有内容的未保存 Scene；交互模式使用 Additive，保存时检查布尔结果，随后 ImportAsset 并按路径重载 SceneAsset。 | `ESPlayerControllerTestSceneBuilder.cs` |
| 该 Builder 在 Additive 路径的 `finally` 中恢复先前 Active Scene 并关闭临时 Scene。 | `ESPlayerControllerTestSceneBuilder.cs` |
| 当前 UI Materializer 的 Fixture 路径保存 Scene 后继续执行，但没有检查该次 `SaveScene` 的布尔返回值；因此“后续逻辑继续”本身不能证明 Scene 已保存。 | `ESUIGameScreenMaterializer.cs` |
| 测试场景规则把 Builder 作为生成布局权威，并要求区分静态、PlayMode、Profiler、Player 和发布证据。 | `场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` |

以上为当前源码/规则事实，不证明这些路径已在本轮 Unity 中执行，也不证明所有 Scene 作者工具都满足本条目。

## Common AI failure modes

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确动作 | 失败恢复 | 缺少的证据 |
|---|---|---|---|---|---|---|
| 修改错误 Active Scene | 对象出现在用户 Scene 或另一个 Additive Scene | 依赖全局 Active 状态 | 精确记录目标 path/handle，写前再次核对 | 显式 SetActive 并检查结果 | Undo 目标修改，恢复原 Active | 当前 Scene handle/path 回执 |
| Single 覆盖用户现场 | 未保存内容消失或提示被绕过 | 没有捕获完整 Scene setup | 列出所有 loaded/dirty/unsaved Scene | 交互模式优先 Additive；歧义时停止 | 保留现场，不自动关闭；请求用户决策 | 保存/关闭选择证据 |
| 忘记 Dirty 或 Save | 内存看似正确，重开后消失 | 混淆对象状态与 Scene 持久化 | 为每项变更绑定 Undo/Dirty/Save | MarkSceneDirty，检查 SaveScene 返回 | Undo/重建并重新保存 | 关闭重开后的序列化结果 |
| 忽略 `SaveScene(false)` | 日志报告成功但磁盘未更新 | 未检查返回值 | 强制保存返回值进入状态机 | `false` 立即失败 | 不清理临时现场；保留错误和输入 | 保存失败原因、磁盘状态 |
| Additive Scene 未关闭 | 用户现场残留 Fixture 或 Active 错位 | `finally` 恢复不完整 | 捕获 loaded set 和 Active | 关闭仅由事务创建的 Scene，再恢复现场 | 报告残留 Scene，禁止继续写 | 恢复后 SceneSetup 比较 |
| 文件存在就声明完成 | 旧 Scene 文件被误认作新结果 | 没有绑定本次保存与内容 | 记录保存前后身份/时间/内容摘要 | 导入、关闭、重开并核对正式内容 | 标记 stale/partial，保留生成输入 | 本次重开验证回执 |
| 只验证内存 Hierarchy | 重开后引用、组件或 override 丢失 | 未跨序列化边界 | 明确重开验收清单 | 关闭并按路径重开核对 | 回到作者源修复并重新生成 | 重开后的对象/引用/override 清单 |

## Execution checklist

```text
开始前
[ ] 读 Start / CurrentStatus / RuleIndex 和目标领域规则
[ ] 验证 SourceRefs / ContentHash / Unity 版本
[ ] 捕获全部 loaded Scene、Active、path、Dirty、unsaved 和 Selection
[ ] 明确目标 Scene 权威、精确路径、Single/Additive 理由和权限

实施中
[ ] 先处理用户 Dirty/unsaved Scene；不做隐式丢弃
[ ] 创建/打开后核对 Scene handle、path、isLoaded 和 Active
[ ] 所有修改进入单一 Undo 边界并标记正确 Scene Dirty
[ ] 检查 SetActiveScene / SaveScene 等失败结果
[ ] finally 只关闭本事务拥有的 Scene，并恢复原现场

完成后
[ ] 保存结果为 true，目标路径可导入/加载
[ ] 在隔离或明确授权条件下关闭并重开目标 Scene，核对根对象、组件、引用和业务身份
[ ] 对比原 loaded set、Active、Dirty 和 Selection 是否恢复
[ ] 第二次执行无意外差异，或差异有明确输入原因
[ ] 分层报告 Static / Unity Import / Reload / PlayMode / Player

禁止
[ ] 不依赖 Scene 名称或当前 Active 状态猜目标
[ ] 不把文件存在、日志、Hierarchy 外观或截图当保存成功
[ ] 不用关闭 Scene 的清理动作掩盖保存/恢复失败
[ ] 不把测试 Scene Builder 规则扩张为所有正式业务 Scene 的权威
```

## Evidence boundary

### Static 可以证明

- 当前受管 Scene 控制和测试 Builder 源码包含精确目标、Undo/Dirty、保存结果检查、Additive 清理及部分恢复合同。
- 当前另一条 Fixture 作者路径存在未检查 `SaveScene` 返回值的静态风险，本条目的强制检查可直接阻止 AI 复制该模式后夸大成功。
- SourceRef 哈希和 ContentHash 可确定性重算。

### Runtime 尚未证明

- 未启动 Unity，未创建、打开、修改、保存、关闭或重开任何 Scene。
- 未验证完整 SceneSetup 恢复、Selection 恢复、Domain Reload、导入、取消、保存失败注入、重复幂等或并行作者任务。
- 未执行 EditMode/PlayMode Test Runner、视觉检查、Profiler、Player、IL2CPP 或发布。

因此，本条目只能支持 S1 静态决策，不能声明 Scene 作者事务已经在 Unity 中可用或验收通过。

## EvidenceRefs

- 当前仅有 S1 源码和 AIWarnings 规则证据；Unity/Editor 执行证据为 `runtime-not-run`。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/场景构建器权威_覆盖审计与项目内备份分层_AI协作警告.md` (`3bb8490dfdf42399110309ada24f51926fdd6b6894a7373f0ef583ec90c52cbc`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationUnityEditorControl.cs` (`86ecf9831780ed714f4f0dc4febcd33c1dd50a5913d4240a08d647c2f6cd1267`)
- `Assets/Scripts/ESLogic/Editor/CharacterTemplates/ESPlayerControllerTestSceneBuilder.cs` (`78354577b02f89838905e08ab966eff107981bd5b7c8520ee66ca70b52986b59`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
