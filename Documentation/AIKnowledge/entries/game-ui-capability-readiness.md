# 游戏 UI 自动化能力就绪矩阵

`KnowledgeId`: `es.project.game-ui-capability-readiness.v1`
`Authority`: `Current ES source/contracts + Unity 2022.3 package facts + bounded external source snapshots`
`RouteKeys`: `ui-automation`, `ui-capability-readiness`, `ui-reference-measurement`, `ui-layout-solver`, `ui-conflict-diagnosis`, `ui-asset-generation`, `ui-visual-design`, `ui-behavior-bridge`, `ui-visual-evaluation`, `ui-repair-loop`, `design-to-unity`, `ui-mcp`
`HashSchema`: `v2`
`ContentHash`: `46fa4a23dff6f56fab8e5eaca3a5ae3abab39796ad292e8223a5403d0dabda5e`
`SourceSetHash`: `46fa4a23dff6f56fab8e5eaca3a5ae3abab39796ad292e8223a5403d0dabda5e`
`EntryBodyHash`: `2be9ecdc77297c3be0191ced5e052504960d0c66103fa595f944694010bc22dc`
`EvidenceLevel`: `S0`
`StaleWhen`: ScreenSpec schema, Validator, Adapter, Materializer, component/asset registry, Unity/UGUI/Input System/TMP versions, visual evidence contract, external source snapshots or any SourceRef hash changes.

## Purpose and verdict vocabulary

本条目是七项 UI 自动化能力的 readiness owner。它只回答“从输入到可验收结果还缺哪些可执行层”，
不重复拥有 Canvas、AssetManifest、BehaviorSpec、视觉 Token 或 Materializer 的细节。每项能力必须
同时区分六个层级：

| 层级 | 含义 |
|---|---|
| Knowledge | AI 能读到规则、反模式和来源 |
| Protocol | 输入/输出字段已定义 |
| Validator | 能确定性拒绝一部分坏输入 |
| Executor | 有可重复的真实实现 |
| Unity evidence | 当前项目实际产生了 Unity/Editor/GPU/输入证据 |
| Commercial acceptance | 在声明的 profile/state/device/asset 条件下通过人工或确定性质量门 |

`Knowledge`、`Protocol` 或 `Validator` 单独存在时，能力状态最多是 `designed` 或
`implemented-unverified`，不能称为“AI 已会做”。

## Current capability matrix

| 能力 | 输入 | AI 决策 | 确定性执行器 | 当前 ES 状态 | 缺失模块 | 失败条件 | 最低证据 |
|---|---|---|---|---|---|---|---|
| 参考图测量 | 原图、像素尺寸、可选 OCR/检测/分割结果、来源 hash | 区域框、文本框、视觉层级、置信度、观察/假设分离 | `ingest_ui_reference.py`（哈希、尺寸、背景估计、候选连通区域、归一化 bounds/anchor hints） | 已有确定性候选测量 executor；无 OCR/语义 detector、人工校正和跨图跟踪 | detector/OCR/segmentation adapter、几何不确定性模型、人工校正入口、source-region validator | 把候选框当真实 RectTransform；阴影/透明层/重叠区域重复计数 | 原图 hash、每个区域 box/mask、置信度、人工复核记录；当前 receipt 仍是 candidate |
| 锚点/Canvas/父子布局推断 | 结构化设计 AST 优先；截图只能作候选 | Canvas owner、anchor/pivot、parent、axis owner、safe area、profile reflow | `Design IR`、约束求解器、UGUI LayoutPlan materializer | Canvas/RectTransform 规则与 Adapter 投影存在；严格 Validator 已拒绝 edge/center/stretch 几何矛盾、profile 内 sibling/layer 倒置、主动作色彩漂移及内容滥用 `safeArea: ignore`；状态也强制复用基准 LayoutPlan，Validator/Resolver/Materializer 均拒绝 state-local bounds/anchor/pivot/layout/size 变更 | IR importer、anchor solver、constraint graph、冲突诊断/修复 | 同轴多 owner；绝对像素复制；CanvasScaler 与安全区冲突；窄屏溢出 | 每 profile 的 resolved RectTransform、每状态 preserveBounds 回执、冲突列表为零或有明确降级、Unity 重读快照 |
| 布局冲突检测/修复 | LayoutPlan、组件最小尺寸、显式 `fixtureTextBindings`、文本/资产尺寸、profile/state 矩阵 | 冲突优先级、可移动区域、换行/截断/裁剪/变体选择 | `resolve_ui_layout_plan.py`、constraint graph checker、deterministic repair planner、re-solve loop | LayoutPlan 已检查 safe-area、布局组所有权、最小尺寸、同级重叠；现可按 profile 像素矩形计算绑定文本行容量、动作净空、布局组后交互目标尺寸及组内最小间距，仍无跨树自动 repair loop | 约束图、轴向 owner 分析、可解释修复补丁、回归基线 | 修复只改颜色/offset；循环布局；修复一个 profile 破坏另一个；无反事实检查 | 原始冲突、`textFit`、`interactionDensity`、修复 patch、每 profile/state 重算结果、失败回归证据 |
| 高质量素材选择/生成 | AssetManifest 槽、语义 role、来源/许可证、风格 brief、生成候选 | 选现有资产/请求生成/placeholder，保留 focal point、朝向、裁切、源宽高比、Atlas rotation policy 和 hash | `resolve_ui_asset_manifest.py`、Unity asset/GUID/hash receipt、`ESUIFocalCropRawImage`、Layout Resolver focal feasibility | AssetManifest + 项目内 resolver 已可执行；高保真焦点主体的 `focalAssetPolicies` 已静态绑定 manifest crop/focal-point/safe-crop/sourceAspectRatio/atlasRotationPolicy，Resolver 会阻断无法保护主体的 profile，Materializer 会拒绝旋转 Atlas Sprite、用实际 Sprite UV 交叉校验比例并输出 source/applied UV 与 `safeCropSatisfied`；AI 生成、许可证核验和商业素材闭环仍缺 | AI image generation adapter、license approval、SpriteAtlas owner/build receipt、Unity crop capture | 搜索图或缓存污染变成正式素材；Token 染暗主视觉；安全裁切无法容纳；SourceSpec 与 Sprite UV 漂移；Atlas 旋转；无许可证/hash | 每 asset 的 resolved path/GUID/hash/license/provenance、导入回执、profile/state Unity 快照、视觉辨识复核 |
| 商业字体/颜色/间距/Token | 屏幕族、品牌/项目 tokens、字体资产、locale/text fixtures、背景像素 | 语义角色、层级、密度、字体/Fallback、对比和非颜色状态信号 | `evaluate_ui_tokens.py`、`evaluate_ui_typography.py`、TMP mapping、contrast/target/text measurement | Token 角色消费和 WCAG 对比度已有静态执行器；TMP 字体 hash/Unicode/显式 text-binding/fallback 已有静态执行器；Resolver 的 profile/state 文本容量为静态近似，Unity/GPU 字形和完整 Token engine 仍未证明 | schema、迁移、TMP/font resolver、Unity material consumer trace | 设计 token 未被消费者读取；静默 fallback；只靠颜色表达状态；目测宣称对比 | 当前字体 asset/hash、字形覆盖、token consumer trace、`textFit` 与 Unity profile/state 测量回执 |
| 行为状态与业务 Bridge | BehaviorSpec、输入 modality、状态图、业务 owner、fixture data | selected/focused/disabled/loading/empty/error/long-content 的表现和 intent | Fixture state-effect executor、Behavior compiler、EventSystem/Input Module binding、Presenter/ViewModel/Data Adapter Bridge | ScreenSpec 的 interaction/stateVariants、`stateSemantics` 与 Fixture 执行集已无损穿过 Python/C# Adapter；严格 Validator 双向约束 stateVariants/affectedComponentIds，并要求每个目标具有白名单 `effects`；Materializer 源码执行这些效果。editor/UI 快照现在还逐 profile/state 交叉验证相同 root/Canvas/viewport、唯一 root-local 元素集合、active 状态与 screen rect，避免不同层级或不同几何的 JSON 进入 GPU gate。GPU evidence 对声明视觉变化的 non-default state 会拒绝与 default 零/低差异的截图，也会拒绝差异主要不在 default editor snapshot 的 declared affected-component 区域内的截图。对每个 effect，UI snapshot 还必须证明目标能力和最终值：`interactable` 对应 `hasButton`，`graphicAlpha` 对应后代 `hasDescendantGraphic`、共同 `descendantGraphicAlpha` 和每节点 `descendantGraphicAlphas[]`，`graphicColor`/`outline` 对应直接 `hasGraphic`，`wrapText`/`text` 对应 `hasText`、共同值及每节点 `descendantTextStates[]`，所有 trace 路径唯一并属于目标组件树，并逐字段比对 `active`、交互、RGBA、alpha、outline、换行和文本；业务 Bridge 未实现 | state reducer、focus graph executor、binding adapter、bridge contract/tests、Unity 状态截图回归 | Button 存在被当成可用；状态跳变丢失；不同 root/Canvas/viewport 或几何的快照被拼接；效果越出执行集；无关背景变化伪装状态；正确区域改像素却未执行声明效果；汇总字段掩盖后代节点未更新；跨组件 trace 注入；UI 偷藏库存/经济/战斗事实 | 静态 Validator/编译、Unity Fixture profile/state 截图、跨通道几何一致性回执、状态像素差、区域相关性、逐节点 effect snapshot 回执、PlayMode/Input receipt、focus traversal、bridge contract test、业务数据一致性 |
| 视觉评分后重新设计 | 当前 GPU capture、source/reference、profile/state、结构快照、失败规则 | 主视觉、层级、几何、状态和素材的归因诊断，选择下一轮变更 | Visual Evaluator、diff/rubric、Repair Planner、baseline ledger | Evidence contract + feedback gate；无自动评分/归因/闭环重设计 | pixel/semantic evaluator、failure attribution、repair planner、regression matrix | 低 diff/非空 PNG/旧 baseline 被接受；修复不改变 artifact；模型自评代替证据 | 同源 baseline/capture hash、结构+像素指标、规则命中、可复现 patch、人工复核 |

## Input reliability order

输入可靠性决定 AI 能做出的决策上限，顺序固定为：

1. 结构化设计源（Figma/Lanhu/PSD/IR）提供节点树、约束、文本、变体和资产身份；这是布局物化的主输入。
2. 参考图解析（检测、分割、OCR、视觉语言模型）只产生带置信度的候选区域和视觉证据；它不能单独授予父子关系或业务语义。
3. Unity 当前运行观察（结构快照、GPU frame、输入事件和状态 JSON）用于验证物化结果和驱动修复。

因此“截图 -> Prefab”可以是降级入口，但不能成为商业级默认入口。没有结构化源时，必须显式记录
`measurementUncertainty`、`knownLoss` 和人工复核点，并降低验收等级。

## Target architecture

```text
Reference Ingestor
  -> Design IR + Source Map + Uncertainty
  -> Semantic Screen Planner
  -> Layout Constraint Solver
  -> Asset Resolver / Generator
  -> Token & Typography Engine
  -> BehaviorSpec + Business Bridge candidate
  -> Unity Materializer
  -> Fixture Driver
  -> Visual Evaluator
  -> Repair Planner
  -> Evidence Ledger / next iteration
```

每个箭头都必须产生可重读 artifact。AI 只负责候选决策和解释，确定性执行器负责坐标、树、资产身份、
状态矩阵、Unity 序列化和证据绑定。Materializer 不应重新猜设计，也不应持有业务数据。

## Minimal implementation order

| 阶段 | 必须交付 | 停止条件 |
|---|---|---|
| P0 | Design IR/source map + reference uncertainty + profile/state identity + deterministic candidate ingest | 结构化源无法保留稳定节点/资产身份，或参考图没有 hash/候选测量回执 |
| P1 | Layout constraint graph + axis-owner/conflict validator + deterministic reflow | 任一 profile 仍有未解释冲突 |
| P2 | Asset Resolver + provenance/license/hash + focal/crop/9-slice checks | 仍只能产生白图或无来源图片 |
| P3 | Token/TMP consumer + text/locale/contrast measurement | Token 没有完整 Schema -> consumer 链 |
| P4 | Visual evaluator + baseline ledger + repair planner | 修复不能指出改变了哪个 artifact 字段 |
| P5 | Behavior compiler + focus/input + Business Bridge contract | 只有 Button/Selectable 静态存在，没有 PlayMode/input receipt |

不要同时实现十个屏幕族；先用主菜单、战斗 HUD、背包/Collection、对话四条切片验证同一平台核心。

## Failure-surface matrix

| failureId | 错误行为 | 预防检查 | 恢复动作 | 当前缺失证据 |
|---|---|---|---|---|
| `UI-CAP-001` | 从截图直接猜父子/锚点 | 要求结构化源或记录不确定性；禁止把 box 变成 layout truth | 降级为候选 LayoutPlan，补 source map/人工校正 | IR importer + profile Unity snapshots |
| `UI-CAP-002` | 同轴同时由 anchor、LayoutGroup、ContentSizeFitter、固定尺寸驱动 | 每轴唯一 owner + constraint graph cycle check | 回退最后通过 Plan，重新求解受影响轴 | 跨树 solver/负例回执 |
| `UI-CAP-003` | placeholder/搜索图/AI 图被当成商业素材 | hash/provenance/license/fallback/atlas owner gate | 移除资源，标记 Deferred/Blocked，baseline stale | resolver/import/license receipt |
| `UI-CAP-004` | 用默认字体或颜色 token 伪装商业视觉 | TMP asset/glyph/Fallback/contrast/role consumer trace | 恢复已验证字体和结构，重跑 text fixtures | font/contrast evaluator |
| `UI-CAP-005` | Button 存在被当成交互和业务闭环 | EventSystem + module + focus graph + Bridge contract + PlayMode | 降级为 visual intent，禁止业务成功声明 | runtime/input/bridge receipts |
| `UI-CAP-006` | 静态通过、非空 PNG、不同 root/Canvas/viewport 或元素几何的 editor/UI 快照拼接、状态零差异、无关背景差异、正确区域改像素却未执行声明 effect，或旧 baseline 被当成视觉接受 | capture/source/profile/state identity + root/Canvas/viewport/path/active/rect cross-check + pixel/structure rubric；有 visualChanges 的 non-default state 必须相对 default 产生最小像素差，且差异多数落在 default editor snapshot 的 declared affected-component 区域；每条 effect 还需在目标 UI snapshot 中核对 capability 和最终字段值 | 废弃该 evidence group，用新 runId 重采集 | automatic evaluator + repair ledger |
| `UI-CAP-007` | 失败迭代只改颜色、缓存 ID或装饰 | 强制 `priorEvidenceBatch/ruleIds/changedFields/expectedEffects/falsificationChecks` | 停止生成并报告 `feedback-not-incorporated` | repair planner and regression matrix |
| `UI-CAP-008` | 非默认状态只在组件 `stateVariants` 出现，未进入 Fixture 执行集 | 严格 Validator 要求 `stateSemantics.affectedComponentIds` 与非默认 `stateVariants` 双向闭合；Generator 只从执行集派生非默认变体 | 删除孤立变体或将组件纳入具体状态语义，重跑 state matrix | Unity Fixture/截图尚未重跑 |
| `UI-CAP-009` | `preserveBounds` 只是文字，状态变体或 effect 偷改 anchor、bounds、pivot、layout、size、safe area 或 sibling order | 三个静态入口拒绝 state-local geometry；Resolver 在 receipt 中列出每个 state 的 preserveBounds/allowedChanges | 将变更提升为基准 LayoutPlan 的 profile 重排，重解全部 profile 后再生成状态 Fixture | Unity Layout rebuild 与状态 GPU 截图尚未重跑 |
| `UI-CAP-010` | `fixtureData` 存在却不替换任何实际文本，或长内容靠节点名启发式写入 | Validator/Materializer 要求显式 `fixtureTextBindings`，Typography 只收集绑定值，Resolver 输出每 profile/state 的行数、截断与动作净空 | 修订 binding、基准 LayoutPlan 或 Fixture 字符串；wrap 超容量必须阻断，ellipsis 必须保留 `truncated` | TMP 实际 line break、Canvas rebuild 与 GPU 文字像素尚未重跑 |
| `UI-CAP-011` | 有锚点和颜色字段，却没有主操作层级、焦点裁切、关键对齐/净空、布局组后操作密度或宽窄屏语义对应 | `advancedComposition` 约束单一主操作、焦点/无焦点决定、AssetManifest 对齐的 crop/focal/safe-crop、对齐、净空、profile 映射和 post-layout interaction density；LayoutGroup 子项不把 authored bounds 当几何事实 | 修订 Composition contract 或回到 LayoutPlan/Resolver；没有 Runtime 几何时保持静态结论 | Unity LayoutGroup/TMP rebuild 与 GPU composition capture |

## Current ES verdict

- `Knowledge`: 已有七项相关 owner，新增本条目负责就绪判定。
- `Protocol`: 六层都有不同程度的 owner/字段约定；ScreenSpec、AssetManifest resolver、LayoutPlan resolver、Token/Typography evaluator 与 Fixture/Materializer 链已有静态实现。状态合同已具备 Adapter 无损传递、执行集限制、双向静态绑定、目标级 effect 合同和跨 Validator/Resolver/Materializer 的 `preserveBounds` 几何不变量；Design IR、通用 solver、业务 Bridge、自动视觉 evaluator 与 repair loop 尚未闭合为可执行合同。
- `Validator`: 有 ScreenSpec、组件注册、部分布局/素材/反馈检查；不是全树约束求解器。
- `Executor`: 有静态 packet/adapter、AssetManifest resolver、LayoutPlan resolver、Token/contrast evaluator、Typography/glyph evaluator 和部分 Unity Materializer；仍没有自动参考图测量、通用跨树 solver、业务 Bridge 或视觉修复闭环。
- `Unity evidence`: 只对曾经实际运行的具体批次成立；本条目本身没有运行新批次，当前结论为 `runtime-not-run`。
- `Commercial acceptance`: 不成立。当前不能声称“AI 已能从任意参考图自动生成商业级 UI”。

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-capability-readiness.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/entries/game-ui-open-source-automation-patterns.md`
- `Documentation/AIKnowledge/entries/game-ui-visual-design-system.md`
- `Documentation/AIKnowledge/UI/game-ui-vision-input-source-snapshot.md`
- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md`
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `Documentation/AIKnowledge/UI/unity-ui-layout-clipping.md`
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md`
- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_asset_manifest.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/ingest_ui_reference.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_layout_plan.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_tokens.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_typography.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_ui_gpu_evidence.py`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIFocalCropRawImage.cs`
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`

## SourceRefs

- `Documentation/AIKnowledge/UI/game-ui-vision-input-source-snapshot.md` (`dc89e92df2a59df58eafada5601ab03e734cdf535318f606104472bde5dac761`)
- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md` (`3baa955d2d54953166dbf8d51f31fbc7f77f825ced61989a057f11c208a2f0f1`)
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`6b02d281bbb339c15554b2b0541e26ce17a27b243e9fd85da5ce2000460e60f7`)
- `Documentation/AIKnowledge/entries/game-ui-open-source-automation-patterns.md` (`dbfd94f153a1707bf4d0c5df1b038cb8618961b1fa3eb92690b0b0644b68c96b`)
- `Documentation/AIKnowledge/entries/game-ui-visual-design-system.md` (`3df579ccadbf30678ba375c6ee2c4c5aedf9bbbe3783e98d0ce308b5ff9bc460`)
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md` (`71a8fa144fd889fa5f1f0e7ffb729dcc357631d25787d23be1f97485cb714aaa`)
- `Documentation/AIKnowledge/UI/unity-ui-layout-clipping.md` (`826c1d2ce2456aa4c5fdf14331643690afa3bb05a115982b5b664b3fd8cc392d`)
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md` (`516fd7bfd63c2cab9d715438c36d40235b2ad7634ba29de56f15423a97881ce2`)
- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`1be1c47841585b77c436c577b10c67c800c6d36c622acddefd1d21018445e169`)
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_asset_manifest.py` (`e435d150cc8f5a6928aa255a958c626af54ef977e02ddad01ace002badf36eb9`)
- `.agents/skills/es-ui-prefab-authoring/scripts/ingest_ui_reference.py` (`7c83f3f579e42f91d3d7424ed6c6b8dc43ea0bc6625925eabae500c9010e4a3c`)
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_layout_plan.py` (`49956b5d72e4e5068743a6eb5b8c38567a5c492de18597033ed852a357d254de`)
- `.agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_tokens.py` (`45d63560c59eb60687a7705d599e342954841c98133665eeac0fd14e58b637d5`)
- `.agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_typography.py` (`222f7171566f78cda15f90220455532c13410a240aa575962ad0d6471c7f91e9`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`28e29084d48d737a09eb281c2b26ee599d38c9e92a7d6ef081cbb59beea34668`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`b191bf200879dab3a7edd0b173d1065d59d7c0e2fc0b5cd5160285219ae3d136`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_ui_snapshot_evidence.py` (`7a09dd4b1b6f14baf33b98b01176c936385b9e465a57fd96105ed27ea5af4714`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_ui_gpu_evidence.py` (`46a26450e3d51bc5f972de7a96aa2600235d64623c89a46b34ae8f35b768c462`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`dad8470537b6236ad3cda2d9e78ac862eeaf513e63f4b799c2cc79fb23ca4a07`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`ca8239c82a680a6112f7aa0e9ac2bd905b5e3f22fed98bfc2437ba2c8a93a311`)
- `Assets/Scripts/ESLogic/Runtime/UI/ESUIFocalCropRawImage.cs` (`5653c9b8c5fe381a8f65412236adb3616e7460ece82c99efb76e47fbec91a4cc`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)

`EvidenceBoundary`: static contract and source calibration only; no new Unity/PlayMode/GPU/Input/Player/IL2CPP run was performed.
