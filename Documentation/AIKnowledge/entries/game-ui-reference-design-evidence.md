# 游戏 UI 参考图与设计输入证据

`KnowledgeId`: `es.project.game-ui-reference-design-evidence.v1`  
`Authority`: `Current UI authoring contracts + adapter/materializer source + official source snapshot`  
`RouteKeys`: `ui-automation`, `ui-reference-evidence`, `design-evidence`, `reference-image`, `reference-provenance`, `source-region`, `vision-review`, `observation-assumption`  
`HashSchema`: `v2`  
`ContentHash`: `147429e7d057a356762258a72c6872580e10b84aa36d1cd0a4575e5eef81e591`  
`SourceSetHash`: `147429e7d057a356762258a72c6872580e10b84aa36d1cd0a4575e5eef81e591`  
`EntryBodyHash`: `a04d1ae94b90a18843003c017dccfdd037202e8b195bf864678ca65409e9ff91`  
`EvidenceLevel`: `S0`  
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目是 UI 自动化“输入侧设计证据”的 canonical owner：负责参考图/Brief 的身份、来源、
许可证状态、内容哈希、区域、观察、推导、假设、设计决策和 vision review 绑定。它不负责
Materializer 产出的 GPU PNG、结构快照或视觉验收；输出侧证据由
`es.editor.project-screen-spec-materializer.visual-evidence.v1` 负责。

## Trigger and routing

- 自然语言触发：UI 参考图、设计稿 provenance、截图区域、视觉观察/假设、design evidence、
  vision review、参考图能否作为 ScreenSpec 输入。
- 精确路由：`ui-reference-evidence`、`design-evidence`、`reference-image`、
  `reference-provenance`、`source-region`、`vision-review`、`observation-assumption`。
- 误路由边界：要求检查 Materializer 已生成 PNG、profile/state 矩阵或像素质量时，转到输出侧
  visual-evidence owner；仅讨论素材文件解析、许可证与 Atlas 时，转到 AssetManifest owner。

## Canonical evidence model

| 层 | 必需内容 | 接受边界 |
|---|---|---|
| Source identity | 原始路径或稳定 URI、不可变内容 SHA-256、获取时间、Owner、来源/许可证状态 | 空路径、零哈希、可变链接或未知权利不能标记 `complete` |
| Reference images | 每张图的 role、原始尺寸、裁剪/缩放记录、hash 与状态 | 衍生裁剪必须同时绑定原图和变换，不得冒充原始输入 |
| Source regions | 稳定 region id、归一化 bounds、role、major 标记、观察与置信度 | 只描述可观察像素；业务语义必须作为推导或假设 |
| Decisions | 关联 region/component、布局/Token/素材决策、理由与被拒方案 | 决策必须可追到观察，不能把模型偏好写成来源事实 |
| Assumptions | 未由输入证明的业务、交互、响应式、文本和素材假设 | 未解决假设保持 `candidate`/`Blocked`，不得静默固化 |
| Vision review | provider、model/version、method、reviewedAt、输入 imageHashes、覆盖率与 findings | review 元数据齐全不等于视觉通过，也不替代人工或 GPU 证据 |

## Decision rules

1. 先固定原始输入身份与哈希，再做区域拆解；不能先改图后把衍生物登记为原图。
2. `observation` 只记录可见结构、颜色、文字和几何；用途、业务状态、点击结果和运行逻辑必须进入
   `inference` 或 `assumption`，并指明需要谁确认。
3. major region 必须全部有稳定 id、合法 bounds 和至少一条观察；区域互相覆盖、超界或无法归因时停止。
4. `status: complete` 只允许非零 SHA-256、非空输入、已记录来源/权限、全部 major region 有覆盖，
   且 review 的 `imageHashes` 与当前输入一致；否则使用 `candidate`、`placeholder` 或 `Blocked`。
5. 参考图只能约束视觉和信息架构候选，不能授权 Unity 写入、推导业务事实或证明响应式/交互成立。
6. 输入变化后旧 region、decision、review 和 ScreenSpec 绑定全部 stale；不得只替换图片而沿用旧结论。

## Verified facts

- 当前 Python Adapter 在缺少 `designEvidence` 时生成零哈希、空路径但 `status: complete` 的缺省对象；
  这只是实现现状，是本条目明确禁止接受的失败状态。
- 当前 C# Adapter 不把原始 `designEvidence` 投影到 Materializer 执行形；Materializer 的 `UiSpec`
  也没有对应字段消费者。Python 与 C# 两条路径因此不等价。
- 当前工作流要求 reference/design evidence 包含来源区域、视觉决策、响应式决策和不确定性；
  `ai-visual-brief` 要求固定 profile、构图、组件、Fixture 和证据限制。
- Materializer 源码中字段白名单出现 `designEvidence` 词汇，不等于反序列化模型、Prefab 或快照已保留该对象。

## Required reads

- 本条目、`ai-visual-brief.md`、Python/C# Adapter、Materializer 源码和 UI 工作流。
- 若判断输出 PNG 或视觉接受，追加 visual-evidence owner 与当前运行证据。
- 若 reference 同时作为可发布 Sprite，追加 AssetManifest owner、当前资源源文件和发布合同。

## Common AI failure modes

| 错误行为 | 触发/症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 当前证据 | 缺失证据 | Source owner |
|---|---|---|---|---|---|---|---|---|
| 零哈希证据标成 complete | Adapter 自动补缺省对象 | 把 schema 形状当真实性 | 拒绝全零 hash、空路径和 placeholder | 标记 `Blocked` 并请求原始输入 | 废弃派生 regions/decisions 后重建 | Python Adapter 静态源码 | 非空原图与可信哈希 | `screen_spec_adapter.py` + 本条目 |
| 把推测写成观察 | 从像素宣称按钮业务或数据规则 | 未分离 observation/inference | 每条结论检查是否可直接从像素观察 | 移入 assumptions 并绑定确认方 | 回滚依赖该假设的 ScreenSpec 决策 | 参考图/Brief 合同 | 业务 owner 确认 | 本条目 + 业务域 owner |
| 裁剪图冒充原图 | hash 只绑定修改后的局部图 | provenance 链断裂 | 对照 originalHash、transform 与 derivedHash | 同时登记原图和确定性变换 | 重新取得原图并使旧 review stale | 静态文件身份 | 原始来源与许可证 | 本条目 |
| review 元数据冒充视觉通过 | 有 provider/model/status 即签收 | 混淆输入审查和输出证据 | 检查当前 runId/profile/state/GPU PNG | 转到 visual-evidence owner | 撤回接受声明并采集新鲜输出证据 | review 合同 | Unity/GPU 像素证据 | 本条目 + visual-evidence owner |
| C#/Python 路径被当成等价 | Python 保留 evidence，C# 结果丢失 | 未对照两个 Adapter 消费面 | 比较归一化 JSON 的字段集合与 hash | 披露字段损失并阻断完成声明 | 修复消费者后重跑两路径对照 | 两个 Adapter 静态源码 | Adapter 等价性执行回执 | Adapter owner |

## Execution checklist

- 开始前：固定原始输入路径/URI、SHA-256、来源、许可证、尺寸和目标 screen/profile。
- 拆解时：为 major regions 建稳定 id，分离观察、推导、决策与假设，记录裁剪/缩放变换。
- 交接时：绑定当前 ScreenSpec hash、review 输入 hashes、未决 assumptions 和 placeholder 清单。
- 结束时：确认没有零哈希 `complete`、没有把输入 review 写成输出视觉验收。

## Evidence boundary and non-claims

Static 只能证明合同、输入身份记录和当前 Adapter 字段损失；没有运行 Unity、视觉模型、GPU 捕获、
人工审图、PlayMode 或 Player。当前未证明任何参考图许可证有效、任何设计决策正确或任何画面通过。

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/references/ai-visual-brief.md` (`744e99b7f133a90b8ee6ff11208717511f37a352a37c9f25d7ddb5c9fc220f6b`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`df9aee267b62ba91fbb2e00cda6e6ec6bb05255bd287a67ffbf96aecf358e420`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`4688b2f94c887ffda48468492f39aad66a8a47cffb1a25f1ddd3e48e97e84158`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)

## StaleWhen

参考图/Brief schema、Python/C# Adapter、Materializer 输入模型、UI 工作流、vision review 合同、
官方来源锁或任一 SourceRef 哈希变化。
