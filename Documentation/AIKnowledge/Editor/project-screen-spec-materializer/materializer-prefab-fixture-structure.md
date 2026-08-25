# Materializer、Prefab 与 Fixture Scene 结构

`KnowledgeId`: `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1`
`Authority`: `Current project source + Unity 2022.3 UGUI package source/documentation`
`RouteKeys`: `ui-automation`, `ui-prefab`, `ui-fixture-scene`, `materializer`, `prefab`, `fixture-scene`, `ui-hierarchy`, `canvas`, `canvas-scaler`, `profile-state`
`ContentHash`: `7d8119a7fa248da92fd4a85b5068792b655acf141cf7e8e80ff7398678319957`
`EvidenceLevel`: `S1`
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目负责 `ESUIGameScreenMaterializer` 的入口、路径门禁、生成层级、Prefab/Fixture 保存顺序、
临时对象清理以及当前缺失的资产事务证据。它不负责：

- ScreenSpec schema、注册表与 Adapter 语义；由 screen-spec-components 条目负责。
- GPU PNG、快照解释和视觉验收；由 visual-evidence 条目负责。
- 通用 Prefab Undo/Dirty/Save/Rollback 规则；由 `es.unity.editor-prefab-asset-transaction.v1` 负责。
- Runtime Window、Presenter、输入业务、资源发布或 Player 行为。

## Trigger and routing

- 自然语言触发：Materializer、生成 UI Prefab、Fixture Scene、CanvasScaler、物化失败、
  Prefab/Scene 部分保存、profile/state 结构、重复生成、回滚。
- 精确 `routeKeys`：`ui-automation`、`ui-prefab`、`ui-fixture-scene`、`materializer`、`prefab`、
  `fixture-scene`、`ui-hierarchy`、`canvas`、`canvas-scaler`、`profile-state`。
- 目标 route-pack：本条目应为 Prefab/Fixture/幂等/保存事务问题的 canonical owner；规范输入问题追加
  screen-spec-components，证据判断追加 visual-evidence，总量不超过 3。
- 当前发现合同：带 UI 领域信号的 Prefab/Fixture 自然语言应推导 `ui-automation` 与
  `ui-prefab`/`ui-fixture-scene`，本条目必须与 umbrella 一起进入 Top 3；显式
  `materializer`、`prefab`、`fixture-scene` 或 `canvas-scaler` 也应直接命中。
- 相邻误路由：泛化 `prefab/save/transaction/rollback` 可能命中通用资产事务或 GameCore 事务条目。
- 回退：若计划只返回 umbrella 条目，必须标记 `KnowledgeCoverageGap`，显式补入
  `materializer` + `prefab`/`fixture-scene` 后重新路由；只有本条目进入 Top 3 才算恢复成功。
  不得根据 umbrella 摘要猜测 Materializer 保存、回滚或幂等行为。
- 路由验收：自然语言探针必须记录推导键、Top 3、预期 owner 和无关条目；“非零命中”不等于
  “命中正确”。Prefab/Fixture、CanvasScaler、重复执行或保存事务任务缺少本条目即失败。

## Required reads

- 常驻：`Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`、
  `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` 与
  `Documentation/ES_UI_AUTHORING_WORKFLOW.md`。
- Index 闭包限制：当前机器 `requiredReads` 仅列本文；在共享 Index 获得独立授权并补齐前，
  执行者必须按本节手工补读上述来源，缺一项即停止，不得把 Index 验证通过解释为读取闭包完整。
- 输入/组件问题：再读 screen-spec-components 条目、组件注册表、Validator 与 Adapter。
- 保存、覆盖、Undo、Dirty、Rollback：再读
  `Documentation/AIKnowledge/Editor/project-editor-asset-authoring/prefab-asset-transaction.md`，不得由本文复制通用事务事实。
- 视觉或运行结论：再读同目录 `visual-evidence-boundary.md` 和当前运行回执；没有回执时保持 `runtime-not-run`。

## Decision rules

1. 静态阅读与 dry-run 规划可在 SourceRef 新鲜且路径合法时继续；任何 Unity 动作必须由当前用户明确点名。只有经受管通道执行时才额外要求当前计划、AICommand 和 TaskContract。
2. 输入必须位于 `Assets/UI/`，证据根必须位于 `ES/UIEvidence/`，结果 JSON 必须位于本次证据根；出现 `..` 或越界路径立即停止。
3. `contractHash/specHash` 当前只被检查为 SHA-256 格式；调用方未提供“从实际内容重算并相等”的证据时，必须标记 `Blocked`，不能把字段存在当可信绑定。
4. Prefab、Fixture Scene 和证据是分阶段写入；任一步失败且没有 last-known-good 恢复回执时，交付结论必须为 `Failed` 或 `Implemented-Unverified`。
5. `finally` 的临时 Scene/root 清理不等于资产级 rollback；不得据此声明事务安全或幂等。
6. 需要修改生成 YAML 时停止，回到 ScreenSpec/LayoutPlan 重新物化；不得直接修补派生产物。
7. Domain Reload、AssetDatabase 刷新、Prefab/Scene 重新读取或重复运行尚无当前证据时，保持 `runtime-not-run`。

## Verified facts

- 唯一固定批处理入口为 `ES.Editor.ESUIGameScreenMaterializer.RegenerateFromSpecBatchMode`。
  来源：`ESUIGameScreenMaterializer.cs` 与 materializer contract。
- Materializer 拒绝非 v3；在调用 Adapter 归一化后，执行形才接受字段白名单检查，并拒绝全树
  重复/空元素 ID、非法 Anchor/Pivot、非有限值、负尺寸和非法容器布局。因此本条目的 SourceRefs
  不能证明“原始 ScreenSpec 的未知字段必然被拒绝”，调用方必须把它保留为未证明项。
  来源：`ESUIGameScreenMaterializer.cs`。
- `dryRun` 返回预期 Prefab/Scene 身份、元素数和 profile/state，不写 Unity 产物。
  来源：`ESUIGameScreenMaterializer.cs`。
- 当前批处理只用 `IsSha256` 检查外部 `contractHash/specHash` 的格式，没有在该入口重算输入内容并比较。
  来源：`ESUIGameScreenMaterializer.cs`。
- 当前执行先保存 Prefab，再保存 Fixture Scene，随后逐 profile/state 采集证据；`finally` 负责关闭临时 Scene、恢复活动 Scene 和销毁临时 root。
  来源：`ESUIGameScreenMaterializer.cs`。

## External authority cross-check（不提升项目证据等级）

Unity 2022.3 官方文档对 `PrefabUtility.SaveAsPrefabAsset` 的定义只覆盖请求创建/保存 Prefab Asset
并返回结果对象，不承诺导入、序列化、GUID、层级或后续 Scene/PNG 阶段已经验收完成。因此该调用
必须被视为阶段性写入动作，保存后仍需重读资产并核对身份与层级。
官方交叉核对地址：
`https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html`。
该 URL 仅是外部解释来源，不是项目 `SourceRefs`，不能替代 Unity 当前运行回执。

## Entry and path gates

- 输入 spec：`Assets/UI/`。
- 证据根：`ES/UIEvidence/`。
- 结果 JSON：本次证据根内部。
- spec 读取：严格 UTF-8；结果 JSON：`ESManagedFileIO.WriteTextAtomic`。
- 所有身份与路径检查通过前，不得创建或覆盖 Unity 资产。

## Generated hierarchy

```text
Prefab root: <panelId>
  RectTransform + CanvasRenderer + Image + ESUIAdaptiveLayout
  Wide
    <递归组件树 + ESUIComponentSemantic>
  Narrow
    <递归组件树 + ESUIComponentSemantic>

Fixture Scene
  UI_Fixture_Canvas
    RectTransform + Canvas + CanvasScaler + GraphicRaycaster
    <Prefab instance>
  UI_Fixture_Camera
    Camera
  EventSystem
    EventSystem + StandaloneInputModule
```

Prefab root 缺省全拉伸，背景 Image 不接收 raycast。Fixture Canvas 使用
`ScreenSpaceCamera`、`ScaleWithScreenSize`、参考分辨率 `1920x1080` 和宽高匹配值 `0.5`；
相机为正交相机。该结构与本机 `com.unity.ugui@1.0.0` 的 CanvasScaler 和
GraphicRaycaster 实现绑定，包源码哈希变化会使本条目 stale。

## Materialization lifecycle

1. 校验 spec 与输出路径，创建 Prefab root，并从规范重新应用布局几何。
2. 通过 `PrefabUtility.SaveAsPrefabAsset` 保存 Prefab。
3. BatchMode 使用 `NewSceneMode.Single` 创建空 Fixture Scene；交互式路径使用 Additive，
   以避免替换用户当前场景。
4. 创建 Canvas、Camera、Prefab instance 和 EventSystem，保存 Fixture Scene。
5. 对声明的每个 `profile x state` 组合切换 Wide/Narrow 与视觉状态，再采集证据。
6. `finally` 中关闭临时 Fixture Scene、恢复先前有效场景并销毁临时 root。

Fixture state 只改变用于截图的视觉状态和交互 affordance，不拥有库存、战斗、经济、导航或
输入业务事实。生成 YAML 也不是手工修复点；布局问题应回到 ScreenSpec/LayoutPlan 后重新物化。

## Common AI failure modes

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 缺失证据 |
|---|---|---|---|---|---|---|
| 信任外部 specHash | 快照身份字段正确但对应错误输入 | 只校验格式未重算 | 对 spec 字节重算 SHA-256 | 不一致立即阻断 | 废弃本次证据并重跑 | 内容哈希绑定回执 |
| 把 finally 当 rollback | Prefab 已更新而 Scene/PNG 失败 | 混淆临时对象清理与资产恢复 | 分阶段资产清单 | 预留 last-known-good/提交点 | 恢复旧资产并重新导入 | 恢复后重读证据 |
| 把 Save 返回当完成 | 文件存在但导入/序列化失败 | 未重新读取权威对象 | 保存后重读 Prefab/Scene | 分对象报告状态 | 标记 Failed 并保留诊断 | Unity 导入与重读 |
| 把 `SaveAsPrefabAsset` 返回对象当验收 | Prefab 路径存在但 GUID、层级或序列化内容与本次 spec 不符 | 将 API 返回值的存在误作资产提交成功 | 保存后重新加载 Prefab，核对 GUID、根节点、语义 ID、specHash 和导入状态 | 任一不一致立即阻断后续 Scene/PNG 阶段 | 恢复 last-known-good 或废弃本次产物后重跑 | 当前 Unity 导入、重读和身份比对回执 |
| 重复运行被称为幂等 | 输出身份或内容漂移 | 未比较同输入双运行 | 比较路径、GUID、结构和哈希 | 只在等价时声明幂等 | 保留第一份并调查差异 | 双运行回执 |
| 手改生成 YAML | 下次物化覆盖修复 | 修改了派生物而非权威输入 | 检查修改对象身份 | 回到 ScreenSpec/LayoutPlan | 重新物化并复验 | 新鲜生成证据 |
| 假定原始未知字段已被拒绝 | 拼写错误或未支持语义未被当前证据发现 | 白名单检查发生在 Adapter 归一化之后 | 对原始 JSON 做独立 schema/字段审计 | 未识别字段立即阻断 | 修正规范后重新校验与物化 | 原始输入严格字段校验回执 |

## Execution checklist

- 开始前：读取 Start 链、本文、screen-spec-components、materializer contract 和通用 Prefab 事务条目；核对全部 SourceRef。
- Preflight：严格 UTF-8 读取 spec，重算 spec/contract hash，验证路径、schema、稳定 ID、profile/state 和输出冲突。
- 实施中：分别记录 Prefab、Scene、每个证据文件的创建/覆盖状态；保留 last-known-good 恢复边界。
- 完成后：重新读取 Prefab 与 Scene，核对 GUID/层级/profile/state，再执行同输入重复运行并比较结果。
- 失败/取消：停止后续阶段，关闭临时 Scene/root，恢复已覆盖资产或明确列出无法恢复的对象。
- 禁止：手改生成 YAML、静默覆盖视觉基线、把 dry-run/SaveAssets/文件存在写成 Unity 成功。

## Evidence boundary

- Static 可证明：入口、路径策略、校验分支、生成层级和源码中的保存/清理顺序。
- Static 同时证明当前风险：hash 只校验格式，且没有可见的资产级事务 rollback。
- Runtime 尚未证明：BatchMode、AssetDatabase、场景序列化、Domain Reload、重复幂等、取消恢复或正式输入链。
- 当前固定结论：`S1`、`runtime-not-run`；Fixture 中存在 EventSystem/Button 不等于真实输入可用。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Documentation~/UICanvas.md` (`724607c892472f573d6b6475794ebc08a62df7384dbbacc4c1817a0f3d88e0c4`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Layout/CanvasScaler.cs` (`c98311dbbec32228456e5f18cdf5682bdb84d21592244476b8be205cb40ab612`)
- `Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/GraphicRaycaster.cs` (`a7c7eadb563eece18edc99969a570b2421ac7fee0fd39b02ccafa4a8ddd2eee2`)

`StaleWhen`: Unity 项目或 UGUI 包版本、Materializer、物化合同、Prefab/Fixture 结构、路径策略、Canvas 配置或 UI 作者工作流任一 SourceRef 变化。
