# 编辑器窗口与扩展可用性验证

状态：现行工程验证路由；静态检查已实现，Unity 交互与视觉证据按目标单独验收。

`KnowledgeId`: `es.engineering.editor-availability-validation.v1`
`Authority`: `Source + AIWarnings + Skill contract`
`EvidenceLevel`: `S2`
`RouteKeys`: `editor`, `editor-window`, `editor-extension`, `inspector`, `drawer`, `dialog`, `popup`, `workbench`, `layout`, `responsive`, `high-dpi`, `single-axis-scroll`, `owner-lifecycle`, `reload-domain`, `undo-dirty`, `preview-lifecycle`, `editor-performance`, `window-production-standard`, `interaction`, `visual`, `availability`, `validation`, `evidence`

Static routing keywords also include `inspector`, `drawer`, `dialog`, `popup`, `workbench`, `layout`, `responsive`, `high-dpi`, `single-axis-scroll`, `owner-lifecycle`, `undo-dirty`, `preview-lifecycle`, `editor-performance`, and `window-production-standard`.
`ContentHash`: `61257ad60488d1cd9caa74ba5fc0d2005037fe8b4038fd07796783f272d4cf4f`
`StaleWhen`: 编辑器扩展规则、ReloadDomain/Undo/序列化边界、可用性矩阵、验证脚本或证据合同变化。
`RuntimeAcceptance`: `runtime-not-run`

`SourceRefs`:

- `.agents/skills/es-editor-availability-validator/SKILL.md` (`dedce060eb443109d3ac428ce0a73a3f1b527475d6129270697a5bda627ff815`)
- `.agents/skills/es-editor-availability-validator/governance.json` (`6119498a1b429c870bf6cd0aace32df922ab667587aa328631acafd025e78ac4`)
- `.agents/skills/es-editor-availability-validator/scripts/Invoke-ESEditorAvailability.ps1` (`85f14f990509c87415794fd6628ca2c0b53d9608036a931a5539516ea6b13618`)
- `.agents/skills/es-editor-availability-validator/references/availability-matrix.md` (`fd957446e1cf757da9a4ed814be08f6e1fa8de10cbf733285784e714e7050104`)
- `.agents/skills/es-editor-availability-validator/references/editor-rule-registry.json` (`3033ad086d2dff84cf4ad5f9b8c891b2dfd939eb93ba9211e061bfddc5c29247`)
- `Documentation/ES_EDITOR_WINDOW_PRODUCTION_STANDARD.md` (`88ce371c61194569d3a3738ec2c35e89b1ec5315d1e8b6d44e9c5313fb93b81e`)
- `.agents/skills/es-editor-tooling/SKILL.md` (`f906b6cae4c00a17f801812e115204bda40ad09cab03f39a36c3051d81e166ef`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)

## Scope

本条目只负责判断编辑器扩展在选定验证 profile 下是否有足够证据称为可用。它不拥有 EditorWindow 的 owner/SessionState 事实、SerializedProperty 写入规则或 Prefab 提交事务；这些事实必须路由到各自 canonical Knowledge，本条目只定义怎样验证它们。

- EditorWindow owner、单实例和 Reload 恢复：`es.unity.editor-window-lifecycle-menu.v1`。
- Undo/Dirty/多对象/Prefab override：`es.unity.editor-serialized-undo-dirty.v1`。
- 正式 Prefab Asset 提交与回滚：`es.unity.editor-prefab-asset-transaction.v1`。

## Trigger and routing

- 自然语言触发：窗口能不能用、Reload 后是否恢复、父子窗口是否串绑、UI 是否只是画出来、是否达到 Ready、交互/视觉/性能验收。
- 精确 routeKeys：`editor`, `editor-window`, `editor-extension`, `inspector`, `drawer`, `dialog`, `popup`, `workbench`, `layout`, `responsive`, `high-dpi`, `single-axis-scroll`, `owner-lifecycle`, `reload-domain`, `undo-dirty`, `preview-lifecycle`, `editor-performance`, `window-production-standard`, `interaction`, `visual`, `availability`, `validation`, `evidence`。
- 预期命中：一般只命中本条目；出现 owner/SessionState/单实例时追加 EditorWindow lifecycle 条目；出现资产写入时切换到对应事务条目。
- 误路由回退：若目标是 Runtime UI、Player、发布或普通 MonoBehaviour，不使用 EW 规则硬套，回到 `KnowledgeIndex.yaml` 按对象和证据层重新路由。

## Decision rules

1. 先分类 `TargetKind` 和验证 profile。`StaticReview` 只能给出 `StaticCompleteRuntimePending`；`Acceptance`/`Release` 缺少新鲜 Runtime receipt 时必须停止为 `runtime-blocked`。
2. SourceRef、规则注册表、目标源码或证据哈希漂移时标记 `stale`，不得沿用旧 Ready/Degraded 结论。
3. EditorWindow/Workbench 必须同时加载 EW-01 至 EW-20；`not-applicable` 必须有 TargetKind 依据，不能用来隐藏失败。
4. owner/Reload 场景必须先读取 `es.unity.editor-window-lifecycle-menu.v1`。该 canonical 条目 stale 时，本条目只能列验证需求，不能代替它重述或批准实现。
5. 文件、按钮、测试源码、一次窗口打开或截图都不是行为成功证据。每个 passed Runtime row 必须绑定本次目标、Unity 版本、时间、命令/测试和 source hash。

## High-risk failure-prevention matrix

| 高危场景 | Static 必查 | Runtime 必查 | 失败裁决与恢复 |
|---|---|---|---|
| `OnDisable` 被当成用户关闭 | `Suspend`、`Close`、Reload 清理入口分离且幂等 | 分别触发 Reload、真实关闭、普通重建 | 任一事件写错脱离意图即 `StaticBlocked`/`runtime-failed`；先恢复事件状态机再重测 |
| FollowOwner 暂不可解析时降级 Independent | 稳定 `ownerKey`、单一 pending、显式 register/resolve，无标题/最近窗口猜测 | 父先、子先、重复恢复、父真实关闭 | 暂不可解析必须保持 Pending；真实关闭后才独立；违反即高危阻断 |
| 状态放错存储层 | 对每个字段标注 owner identity、项目对象 ID、SessionState、EditorPrefs 或 transient | Reload、退出重开、目标删除/移动 Scene | managed/未保存/mixed selection 不得跨 Reload；解析失败不得同名兜底 |
| `[InitializeOnLoad]` 提前加载资产 | 自动入口只做轻量注册/延后信号，无扫盘和大资源加载 | Unity 启动、脚本重编译、导入尚未完成时恢复 | 资产解析失败或重复扫描即阻断；移到窗口打开、用户动作或可证明的导入后阶段 |
| 瞬时操作跨 Reload 继续 | Reload 前有取消/释放，活动引用、Popup、捕获、Task 不持久化 | 拖动/Busy/Popup/任务中触发 Reload | 新域继续旧副作用即失败；丢弃瞬时状态并从稳定描述重新开始 |
| 单实例只靠旧静态引用 | 正式入口使用 `GetWindow`/受管协调器，不在 Reload 全局扫窗关闭重复项 | 重复点击、Reload 后再开、关闭重开 | 出现第二实例即失败；修复打开入口，不猜测并销毁用户窗口 |
| 只验证“看起来恢复” | postcondition 同时检查 owner、目标、订阅数、失败提示和无意外写入 | 负向路径、取消、重复执行、恢复后主动作 | 只有视觉恢复但身份/副作用未闭合，仍为 `runtime-failed` |

## Execution checklist

```text
开始前：固定 TargetKind/profile/branch/HEAD/Unity 版本 -> 验证 SourceRef -> 加载适用 EW 规则和 canonical Knowledge
实施中：逐行记录 Static/Runtime/不适用 -> 先测负向与恢复 -> 证据绑定目标、时间、入口和 source hash
完成后：复核最弱必需维度 -> 区分 StaticBlocked 与 runtime-blocked -> 列出 not-run 和 unsupported claims
不可跳过：父先/子先、真实关闭、目标失效、Reload、重复打开、取消/恢复、窄宽/高 DPI（适用时）
禁止：用源码/测试/截图存在冒充执行；编辑报告把失败改成通过；用权重平均掩盖 critical failure
```

## Evidence boundary

编辑器工具的可用性不是“源码存在”或“窗口打开一次”。结构、静态边界、Unity 编译、ReloadDomain、交互、视觉、恢复和性能是独立维度；缺少必需 Unity 证据时输出 `Blocked` 或 `not-run`。截图不能证明序列化、Undo、资源生命周期或交互正确性。

- Static 可证明：目标分类、入口形状、显式 ownerKey/Pending API、禁止扫描信号、EW 规则覆盖和证据合同闭合。
- Static 不可证明：真实窗口恢复顺序、对象解析结果、点击/焦点/布局、回调是否重复、Profiler 或 Unity Console 结果。
- 本轮未启动 Unity、未触发 ReloadDomain、未运行 Test Runner、未采集视觉或 Profiler 证据；`RuntimeAcceptance` 保持 `runtime-not-run`。
