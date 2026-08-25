# ScreenSpec v3 与组件注册边界

`KnowledgeId`: `es.editor.project-screen-spec-materializer.screen-spec-components.v1`
`Authority`: `Current project source + governed UI authoring contracts`
`RouteKeys`: `screen-spec-v3`, `ui-automation`, `component-registry`, `ui-component`, `semantic-preservation`, `materializer`
`ContentHash`: `8954652952371d14788a2fa0c95b5c6289c883e63d293548c5702fba7053feef`
`EvidenceLevel`: `S1`
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目负责 ScreenSpec v3 的 schema、组件注册、静态 Validator、C# Adapter 和
`ESUIComponentSemantic` 之间的决策边界。它回答“一个视觉组件能否安全进入物化输入”，不负责：

- Prefab/Fixture 的保存顺序与层级结构；由
  `es.editor.project-screen-spec-materializer.prefab-fixture-structure.v1` 负责。
- GPU 截图、快照与证据等级；由
  `es.editor.project-screen-spec-materializer.visual-evidence.v1` 负责。
- Runtime Window、Presenter、业务数据、输入、资源发布或正式美术验收。

## Trigger and routing

- 自然语言触发：ScreenSpec v3、组件注册、注册新 UI 组件、Adapter 投影、语义 ID、
  `assetSlots`、组件 Validator、Materializer 不认识组件。
- 精确 `routeKeys`：`screen-spec-v3`、`ui-automation`、`component-registry`、
  `ui-component`、`semantic-preservation`、`materializer`。
- 当前 route-pack：带 `ScreenSpec` 的自然语言通常推导 `ui-automation` 与 `screen-spec-v3`，
  本条目和 `es.project.ui-automation-authoring.v1` 会共同命中；本条目是组件注册决策的 canonical owner。
  涉及生成结构时再加载 prefab/fixture 条目，涉及证据时再加载视觉证据条目，总量不超过 3。
- 相邻误路由：泛化 `ui`、`prefab`、`fixture` 可能命中通用 UI 或资产事务条目。
- 回退：若自然语言没有生成 routeKey，停止并报告 `KnowledgeCoverageGap`；组件注册任务可显式补入
  `screen-spec-v3` + `component-registry` 后重新路由，只有本条目进入 Top 3 才算成功。
  不得用泛化 UI 摘要代替本条目。
- 路由验收：自然语言探针必须记录推导键、Top 3、预期 owner 和无关条目；仅因 `ui-automation`
  命中本条目时，不得把 Materializer 幂等、Prefab 事务或发布证据问题误归为组件注册。

## Required reads

- 常驻：`.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json`、
  `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`、
  `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` 和
  `Assets/Scripts/ESLogic/Runtime/UI/ESUIComponentSemantic.cs`。
- Index 闭包限制：当前机器 `requiredReads` 仅列本文；在共享 Index 获得独立授权并补齐前，
  执行者必须按本节手工补读注册表、Validator、Adapter 与语义组件，缺一项即停止。
- 新增/修改组件：再读 `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json`、
  `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` 与
  `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`。
- Unity 写入或验收：再读同目录 `materializer-prefab-fixture-structure.md`、
  `visual-evidence-boundary.md` 和本任务匹配的 AICommand/TaskContract。
- 任一 Required read 缺失、越界或哈希漂移时停止，不使用本文摘要补齐缺失事实。

## Decision rules

1. 只有 Start 链、当前 Index 绑定、本文 SourceRefs 和 ContentHash 均新鲜时，才可继续使用本文决策。
2. 调整已有组件前，必须同时读取注册表、Validator、Adapter 和 Materializer 合同；只读其中一层不足以继续。
3. 新增组件只有在注册要求、Validator fixture、Adapter 投影和 Materializer recipe 四层都有明确处理时，才可进入实现计划。
4. 未知 schema、未知字段、未注册类型、缺失稳定 `id`、资源引用不闭合或 SourceRef 漂移时必须停止。
5. 只有通用 primitive fallback、没有专用 recipe 时，标记 `Deferred`，不得宣称高保真组件已经支持。
6. 需要修改 registry、Validator、Adapter、Materializer、Assets 或 Unity 产物时，以当前用户明确目标和动作作为授权；Knowledge 本身不授权 AI 自行执行。选用受管通道时再满足其 AICommand 与 TaskContract。
7. 测试源码存在只能证明测试定义存在；必须取得实际输出后才能升级证据等级。

## Verified facts

- 当前项目版本为 Unity `2022.3.45f1`，revision `a13dfa44d684`。
  来源：`ProjectSettings/ProjectVersion.txt`。
- C# Adapter 只把 `schemaVersion == 3` 且 `components` 为数组的 JSON 识别为 ScreenSpec v3；
  它把 `screenId` 投影为 `panelId`，并提供缺省 Prefab/Scene 路径。
  来源：`Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs`。
- Adapter 递归保留稳定 `id`、`type`、`visualVariant`、`assetSlots`、内容、响应式范围、
  布局意图和子节点；`grid/list/flow` 会投影为容器布局参数。
  来源：`ESUIScreenSpecAdapter.cs` 与 ScreenSpec v3 模板。
- 注册表稳定身份为 `es.game-ui`，声明模板必需区域、允许组件和组件输入约束。
  来源：`game-ui-component-registry.json`。
- `ESUIComponentSemantic` 保存视觉语义而不拥有业务状态。
  来源：`Assets/Scripts/ESLogic/Runtime/UI/ESUIComponentSemantic.cs`。
- 组件被注册只证明它可进入静态校验范围，不证明 Materializer 有专用视觉 recipe、资源存在或 Unity 已运行。
  来源：注册表、Validator、Adapter 与 UI 作者工作流的联合静态边界。
- Python Validator 的元素 ID 集合会在每个父节点下重新建立；它能检查同级重复，但当前 Validator
  SourceRef 不能证明跨不同嵌套分支的 ID 全局唯一。不得把 Validator 单独通过写成该约束已经闭合。
  来源：`validate_game_ui_screen_spec.py`。

## Registration mechanism

```text
ScreenSpec v3
  -> component registry
  -> validate_game_ui_screen_spec.py
  -> ESUIScreenSpecAdapter
  -> ESUIGameScreenMaterializer
```

## Common AI failure modes

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 缺失证据 |
|---|---|---|---|---|---|---|
| 把注册项当成已实现组件 | Validator 通过但画面只有通用块 | 混淆能力声明与 recipe | 四层闭包检查 | 补齐或标记 Deferred | 回退到已注册 fallback 并披露 | Unity 物化与视觉证据 |
| 丢失稳定语义 | 快照无法关联组件 | Adapter 未保留 id/type/slot | 对照递归投影字段 | 修复规范或 Adapter 计划 | 废弃旧快照并重新生成 | 新鲜快照 |
| 把 assetSlots 当正式资源 | Prefab 使用白图/占位图 | 语义引用被误作资源解析 | 检查 AssetManifest 来源与 hash | 标记 placeholder | 补充资源解析后重跑 | 正式资源与发布证据 |
| 只修改 registry | 新类型落入通用 primitive | 忽略 Validator/Adapter/recipe | 同变更四层清单 | 同批补齐四层或停止 | 撤销新类型的可用性声明 | 四层静态与 Unity 证据 |
| SourceRef 漂移仍引用本文 | AI 使用旧 schema 事实 | 摘要被当权威 | 重算所有 SourceRef | 标记 stale 并回读 | 丢弃旧计划 | 新 ContentHash |
| 只靠 Validator 证明 ID 全局唯一 | 不同嵌套分支出现同名组件 | 子级去重集合按父节点重建 | 构造跨分支重复 ID 负例 | 增加全树去重检查，物化前保持阻断 | 修正 ID 后重跑 Validator 与 Materializer preflight | 跨分支负例执行回执 |

## Execution checklist

- 开始前：读取 Start/CurrentStatus/RuleIndex、本文、注册表、Validator、Adapter 和当前 Index 绑定；核对 SourceRef。
- 实施中：保持稳定 id、模板区域、输入约束、fallback 和四层实现关系；记录 Deferred 项。
- 完成后：运行 ScreenSpec Validator 的正向、非法类型、缺引用、重复 id 和重复执行用例。
- 不可跳过：若触及 Unity 物化，转入 prefab/fixture 与视觉证据条目并取得相应权限和新鲜回执。
- 禁止：从像素推断业务逻辑、手工修补生成 YAML、把注册/文件/测试存在写成执行成功。

## Evidence boundary

- Static 可证明：schema/registry/Validator/Adapter/语义组件的当前源码合同和 SourceRef 闭包。
- Static 不可证明：某份 ScreenSpec 已实际通过、Prefab/Scene 已保存、画面正确、交互可用或资源已发布。
- 当前固定结论：`S1`、`runtime-not-run`。未运行 ScreenSpec Validator、Unity BatchMode、Editor、PlayMode 或 Player。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json` (`4aba3b950fef2b9c45dc6b4ba6abc3b6a59517ddeb566ab86ede106d5facf38d`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`4d60216d8d3c870d243f01577074b7b16b5e2234cb8eff02f9f26231521def74`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`4688b2f94c887ffda48468492f39aad66a8a47cffb1a25f1ddd3e48e97e84158`)
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIComponentSemantic.cs` (`ace60512446d67449c11a9cc8352f008ee670371e71c4fed09f8f5b2a8a36839`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)

`StaleWhen`: Unity 项目版本、ScreenSpec v3 模板、组件注册表、Validator、C# Adapter、语义组件或 UI 作者工作流任一 SourceRef 变化。
