# AIKnowledge 概念 Owner 与重复覆盖矩阵

状态：盘点报告；非权威领域知识，不参与自动路由。  
盘点基线：`Documentation/AIKnowledge/KnowledgeIndex.yaml` 当前索引（92 条 entry，含本轮新增 3 个通用 owner）；Knowledge Index 静态校验：通过；Runtime：未运行。

## 判定规则

- `Canonical`：该概念的长期事实只允许在此 owner 中维护。
- `Projection`：只保留路由、requiredReads 和边界，不复制 Canonical 正文。
- `Domain`：只写领域特有事实，通用规则必须回链 Canonical。
- `Gap`：当前没有单一、可验证的概念 owner。
- `Overlap`：多个条目共享 routeKey 不等于重复；只有事实正文或权威边界重复才计为冗余风险。

## 概念 Owner 矩阵

| 概念族 | Canonical owner | 现有相关条目 | 当前判定 | 整合动作 |
|---|---|---|---|---|
| Knowledge 路由、去重、新鲜度 | `es.knowledge.routing-quality.v1` | `es.knowledge.maintenance-transaction.v1`、`es.function-area.governance.v1` | Owner 清晰；治理功能区有 Projection 风险 | 保留 routing-quality；功能区只导航 |
| AIWarnings 权威分层 | `es.aiwarnings.domain-map.v1` | `es.aiwarnings.domain-inventory.v1`、`es.aibrain.authority-startup.v1` | Owner/清单职责可分，但易重复摘要 | inventory 只做目录，domain-map 只做路由 |
| EditorWindow / Editor 扩展可用性 | `es.engineering.editor-availability-validation.v1` | `es.unity.editor-window-lifecycle-menu.v1`、`es.function-area.editor-agent.v1` | 通用验证 owner 清晰；生命周期是专项 Domain | editor-agent 只做组合入口 |
| UI Toolkit / Editor 事件与输入 owner | `es.editor.editor-event-ownership.v1` | `es.engineering.editor-availability-validation.v1`、`es.project.editor-workbench-authoring.v1`、UGC Workbench AIWarning | 新增 Canonical；旧条目仍有 Projection 重叠 | Workbench/Editor availability 只引用通用 owner |
| 稳定选择、对象代际、正式 Scene 映射 | `es.editor.stable-selection-scene-identity.v1` | `es.project.editor-workbench-authoring.v1`、`es.unity.serialization-prefab-identity.v1`、`es.unity.editor-scene-asset-transaction.v1` | 新增 Canonical；领域条目仍需限制身份字段范围 | Scene/World/Workbench 只描述各自解析后端 |
| Scene 正式资产事务 | `es.unity.editor-scene-asset-transaction.v1` | `es.project.editor-asset-authoring.v1`、`es.unity.editor.project-scene-builder-authority.v1`、`es.project.scene-release-evidence.v1` | 事务 owner 清晰；Builder/Release 是 Domain/Evidence | umbrella 条目只导航 |
| Prefab 正式资产事务 | `es.unity.editor-prefab-asset-transaction.v1` | `es.project.editor-asset-authoring.v1`、`es.unity.serialization-prefab-identity.v1` | Owner 基本清晰；稳定身份边界有共享路由 | 事务与身份分开维护 |
| SerializedObject / Undo / Dirty | `es.unity.editor-serialized-undo-dirty.v1` | `es.project.editor-asset-authoring.v1`、`es.project.editor-workbench-authoring.v1` | Owner 清晰；Workbench 有摘要重复风险 | Workbench 只引用，不复述 |
| PreviewScene / 临时对象 / RT 生命周期 | `es.editor.preview-lifecycle.v1` | `es.engineering.editor-availability-validation.v1`、`es.project.editor-workbench-authoring.v1`、PreviewLifecycle AIWarning | 新增 Canonical；AssetPackage 仍是迁移中的 Domain | AssetPackage 不得新增第三套预览底层 |
| Workbench 装配、Contribution、模块裁剪 | `es.project.editor-workbench-authoring.v1` | 专业工作台 AIWarning、UGC Workbench AIWarning、`es.function-area.editor-agent.v1` | Workbench owner 应只负责装配，不承载通用规则 | 压薄 entry，保留组合路由 |
| 2D/3D/游戏视口适配与刷新 | **无单一 Canonical** | `es.project.editor-workbench-authoring.v1`、World AIWarnings、当前源码 | 明显 Gap；3D SelectionChanged 尚无 Knowledge owner | 新增通用 viewport/selection owner |
| AssetDatabase / 导入 / 批处理事务 | `es.unity.editor-assetdatabase-import-transaction.v1` | `es.project.editor-asset-authoring.v1`、资源 Pipeline 条目 | Owner 清晰；资源运行时条目不可反向覆盖编辑器事实 | 保持编辑器/运行时分层 |
| GameCore / ConfigKey / Content 注册身份 | `es.project.gamecore-identity-registration.v1` + 三个拆分条目 | `esframework.project.configkey-runtimekey-catalog.v1`、`esframework.project.gamecore-content-registration-transaction.v1`、`esframework.project.gamecore-root-runtime-data.v1` | 拆分合理；功能区条目是 Projection | 禁止在 Workbench 条目复制 GameCore 身份规则 |
| UI Automation / ScreenSpec | `es.project.ui-automation-authoring.v1` 及其明确子域条目 | 16 条共享 `ui-automation` 路由条目 | 不是简单重复，但共享路由过宽 | 依赖 routeKeys 二次筛选，不按关键词全注入 |
| Preview/视觉/Fixture/Release 证据 | `es.engineering.fixture-visual-qa.v1`、`es.project.scene-release-evidence.v1`、`es.unity.compile-player-il2cpp-evidence.v1` | `es.engineering.editor-availability-validation.v1`、各 UI 视觉条目 | Evidence owner 按证据类型分裂是合理的 | 禁止把静态 entry 投影成 Runtime/Release 通过 |
| Runtime 生命周期、Pool、Lease、仲裁 | `es.project.runtime-lifecycle-pool-arbitration.v1` | `es.project.pool-operation-skill-lifecycle.v1`、`es.project.resource-runtime-lease-boundaries.v1`、多个性能条目 | 按对象边界拆分合理；共享 lifecycle/pool 路由较密 | 以 owner identity 和对象类型消歧 |

## 重复覆盖风险矩阵

| 风险组 | 主要重叠 | 风险等级 | 错误注入场景 | 处置原则 |
|---|---|---:|---|---|
| Editor umbrella | `function-area.editor-agent` ↔ Editor availability ↔ Workbench | 中 | 把功能区摘要当成 EditorWindow 合同 | umbrella 只能 Projection |
| Workbench/Scene | Workbench ↔ Scene transaction ↔ Scene Builder ↔ Release evidence | 高 | 把 PreviewScene 显示正确注入成正式 Scene 已提交 | 必须按 `scene-transaction` / `scene-release-evidence` 分离 |
| Identity/Selection | Prefab identity ↔ Scene identity ↔ Workbench selection | 高 | 用 InstanceId、Preview 对象或同名对象恢复选择 | 缺 canonical selection owner 时禁止自动合并 |
| UI Automation | 16 条共享 `ui-automation` 路由 | 中 | 一次任务注入 Intent、ScreenSpec、视觉、Fixture 全部正文 | 先按对象/动作/风险再选 1～3 条 |
| Evidence | Editor availability ↔ Fixture QA ↔ Release evidence | 高 | 静态扫描被解释成 Unity/Release 通过 | EvidenceLevel 和 runtime boundary 强制保留 |
| Lifecycle | Domain Reload ↔ EditorWindow ↔ Preview ↔ Workbench | 中 | 只读到窗口生命周期，漏掉 Preview/临时对象释放 | 按 owner/lifetime 类型分路由 |
| Governance summaries | domain inventory/map ↔ function-area summaries | 中 | 摘要覆盖权威规则或改变权限解释 | 摘要不得定义新事实或权限 |

## 当前结论

1. 现有 Knowledge 不是“全部冗余”；大部分条目是合理的领域拆分。
2. 真正的冗余集中在 umbrella/function-area 条目与 Canonical 条目之间，以及高共享 routeKey 的 UI Automation/Evidence 群组。
3. 当前最危险的不是重复文本数量，而是三个通用 owner 曾经缺失；本轮已补齐对应导航条目，但尚未完成旧条目的 Projection 化。
4. 下一阶段不再新增同主题 Workbench 条目，改为把现有 Workbench、Editor Availability 和领域条目收敛为 Projection/Domain。

## 后续整合门禁

- 先为每个条目登记唯一 `canonicalTopic`（治理元数据，不作为权限）。
- 同一 `canonicalTopic` 只能有一个 Canonical；其它条目必须标为 Projection 或 Domain。
- Projection 条目不得新增事实性 SourceRef，只能引用 Canonical 和当前入口。
- 每次合并前运行 Knowledge Validator 的 Entry/Index、SourceRef/ContentHash、route-set 和 required-read 闭合检查。
- 对高风险路由添加负向用例：PreviewScene ≠ 正式 Scene、静态 ≠ Runtime、通用 Editor ≠ Workbench Domain。
- 任一 SourceRef 漂移时，旧条目和依赖路由标记 stale，禁止用旧摘要继续注入。
