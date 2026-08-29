# Unity 游戏 UI AI 工作流分歧与质量门

`KnowledgeId`: `es.project.game-ui-ai-workflow-divergence-and-quality-gates.v1`  
`Authority`: `Pinned Unity official source snapshots + bounded open-source workflow snapshots + current ES UI contracts`  
`RouteKeys`: `ui-automation`, `ui-workflow`, `ui-layout`, `responsive`, `ui-asset-manifest`, `asset-provenance`, `ui-visual-evaluation`, `design-to-unity`  
`HashSchema`: `v2`  
`ContentHash`: `1e00de96de85c990840af38562c0b4fe3375f68d36699cc65f3a63e985493baf`
`SourceSetHash`: `1e00de96de85c990840af38562c0b4fe3375f68d36699cc65f3a63e985493baf`  
`EntryBodyHash`: `09f16733ee537e8688345f41372e6169390686a145ba9e263d926ce071d3ada8`
`EvidenceLevel`: `S0`  
`StaleWhen`: `Unity/UGUI/TMP/Input System version, ScreenSpec/Validator/Adapter/Materializer schema, AssetManifest/Token/Font contract, pinned external source hash, or visual evidence contract changes.`

## 目的与结论

本条目裁决“AI 生成游戏 UI”几条常见路线的分歧，并把可复用的质量要求接到 ES 的
ScreenSpec v3、AssetManifest、LayoutPlan、BehaviorSpec、Fixture Matrix 和 Evidence Gate。
它不是新的 Prefab 生成器，也不把第三方仓库 README 的能力描述升级成项目事实。

核心结论：

1. 截图直接写 UGUI 适合快速候选，不足以推断父子关系、锚点、业务语义、素材授权或响应式变体。
2. 结构化设计 IR/Packet 适合生产输入，但必须保留 source map、known-loss、字段 owner 和后端 conformance；IR 本身不是 Unity 运行证据。
3. Unity 官方 UI Skill 解决的是 UGUI/UI Toolkit/IMGUI 路由和 Unity 常见陷阱，不提供任意截图到 Prefab 的商业设计能力。
4. 真实可用贴图、抗缩放、配色和字体不能靠一个视觉模型一次决定；必须分别经过资源身份、布局约束、Token 消费、字体字形和 profile/state 证据门。
5. `static-passed`、PNG 非空、文件存在和第三方仓库测试不能升级为 Unity/GPU/输入/商业视觉通过。

## 三类方案的极大分歧

| 路线 | 强项 | 根本缺口 | ES 处理 |
|---|---|---|---|
| Screenshot -> AI JSON -> UGUI | 入口快，目标分辨率下可快速得到框 | 像素框不是父子/锚点真值；单分辨率不等于响应式；占位图容易被当正式素材 | 只输出带置信度的候选 LayoutPlan；必须补 profile、source map、AssetManifest 和 Unity 重读快照 |
| Figma/IR/Packet -> 多后端 | 稳定节点 ID、版本化字段、known-loss、后端 conformance 和 source map | 设计语义不等于业务语义；后端支持矩阵和 Unity 版本差异仍需验证 | 作为主输入优先；逐字段标记 render/approx/known-loss，Materializer 不重新猜设计 |
| Unity 官方 UI Skill/UGUI/UITK | 版本相关的 Canvas、LayoutGroup、EventSystem、PanelSettings 和常见错误边界清楚 | 不负责商业屏幕族、品牌视觉、素材生成、设计图测量或业务 Bridge | 作为 Unity 事实层；Unity 2022.3 项目默认沿用 UGUI，不把 Unity 6 UI Toolkit 指南直接移植 |

### 不能混为一谈的两组概念

- `UI Toolkit` 与 `uGUI`：官方 UI Toolkit Skill 的版本和 USS 能力不等于当前 Unity 2022.3 uGUI 的运行能力。
- `Prototype flow` 与 `BehaviorSpec`：设计原型的跳转只表达可观察意图，不能拥有库存、经济、战斗或服务端数据。
- `Asset slot` 与 `resolved asset`：槽位只说明需要什么角色；正式贴图还需要路径/GUID、哈希、来源、许可证、导入策略和 fallback 回执。
- `Token value` 与 `visual proof`：hex 值或 DTCG `$value` 只定义输入，必须有 Unity 消费追踪和像素/对比度测量。
- `Font fallback capability` 与 `font chain ready`：TMP 包支持 fallback 表不代表项目 Font Asset、字形覆盖、许可证和加载顺序完成。
- `normalized bounds` 与 `resolved RectTransform`：归一化 bounds 是设计意图；LayoutGroup、CanvasScaler、安全区和文本测量可能改变最终几何。

## 生产质量门

### 1. 真实可用贴图门

每个非占位图片必须有：`assetId`、`role`、`source`、`path/GUID`、内容 `hash`、`provenance`、
`license`、`fallback`、`importPolicy`、`aspectPolicy`、`focalPoint`、`cropPolicy`、`nineSlice`、
`atlasOwner` 和 `resolutionSet`。`generated-procedural` 与 `generated-placeholder` 只能标记为
候选或 deferred，不能称为商业素材完成。当前 ES resolver 已能验证项目内候选的身份，但不会授予商业授权。

AI 必须先判断素材角色：背景、主视觉、头像、图标、边框、状态图或装饰；主视觉必须记录焦点、
朝向和允许裁切。`visualVariant: none` 只表达“不用 Token 染图”，不等于素材已经存在。

### 2. 抗缩放与分辨率门

每个 ScreenSpec 必须声明 Canvas render mode、CanvasScaler mode、reference resolution、
match/expand/shrink 策略、安全区策略和 profile 矩阵。宽屏、窄屏和极端长文本必须是独立布局决策，
不能把一套 normalized bounds 均匀缩放到所有设备。

每个 profile 必须能回答：谁拥有横轴/纵轴、哪些区域 stack/reflow、最小尺寸、文本换行/滚动策略、
底部导航是否独立于 content scroll owner、主视觉在裁切后是否仍可辨识。最终必须重读 Unity 的
RectTransform，而不是只读 JSON。

### 3. 配色与状态门

Token 先按语义角色定义，再由消费者映射到 Unity。至少区分 `surface`、`text`、`action`、
`feedback`、`focus`、`border`、`icon` 和 `rarity`；一个屏幕只能有一个主要动作层级。

selected、focused、disabled、loading、success/error 必须同时有结构、文案、图标或焦点环信号；
颜色不能是唯一信号。文本和焦点对比度只能用同一 profile/state 的实际渲染像素测量，不能用 hex
静态目测或规范阈值直接签收。

### 4. 字体门

每个正式字体必须绑定 `fontAssetId`、Unity/TMP Font Asset 路径/GUID、内容哈希、来源/许可证、
字重、Atlas population mode、Fallback 顺序和目标字符集。Fixture 至少包含中文长文本、数字、
标点、英文混排；如项目支持 RTL/Bidi，还要加入对应 locale。

静态质量门现在会在项目根存在时读取声明的 Font Asset、distinct fallback 记录与生产素材路径，确认路径不越界并重新计算 SHA-256；
字符串字段齐全但文件不存在、哈希不匹配或 fallback 无法覆盖缺失字形仍然失败。该检查只证明输入 provenance，不能证明 Unity 导入或设备渲染。

`wrap`、`ellipsis`、`truncate`、`scroll` 是组件级决策；长文本变化不得遮挡主动作或改变无关区域的
几何 owner。字形缺失、静默 fallback、字体未加载和不同 profile 渲染差异都必须降级为 blocked 或
runtime-not-run。

## 推荐的 AI 执行顺序

```text
Task Classifier
  -> screen family + primary intent + confidence
Reference/IR Ingestor
  -> source map + measurement uncertainty + known-loss
Semantic Screen Planner
  -> ScreenSpec + stateSemantics + profileAvailability
Asset Resolver
  -> provenance/license/hash/import/crop/atlas receipt
Layout Solver
  -> Canvas/anchors/axis owners/profile reflow/conflicts
Token & Typography Resolver
  -> consumer trace + glyph/contrast/text fixture plan
Behavior/Fixture Planner
  -> intents, focus candidates, six-state matrix, long-content data
Unity Materializer
  -> Prefab/Fixture + resolved hierarchy snapshot
GPU/Runtime Evidence
  -> profile/state capture + input/geometry/visual metrics
Repair Planner
  -> field-level change tied to failure rule and falsification check
```

顺序不可反转：先用 glow、颜色或占位图掩盖布局、字体或主视觉问题是失败；先生成 Prefab 再猜
父子布局也是失败。低置信度分类必须请求补充，而不是静默选择 navigation 或 collection。

## 失败面矩阵

### `UI-AI-001` screenshot box promoted to layout truth

- `severity`: `identity/authority`
- `erroneousBehavior`: 把检测框直接写成父子、anchor、pivot 或 LayoutGroup 结果。
- `triggerAndSymptom`: 宽窄屏重叠、死区、文本裁切、同轴多 owner。
- `rootCause`: 缺少结构化源、source map 和不确定性记录。
- `preventionCheck`: 要求结构化 IR；截图入口必须保留 confidence、known-loss 和人工校正点。
- `correctAction`: 输出候选 LayoutPlan，执行 axis-owner/conflict check，再由 Unity 重读确认。
- `recoveryAction`: 废弃该 profile 的视觉结论，保留 source map 并重求解。
- `evidencePresent`: 官方 Canvas/Layout 快照、开源 Packet/IR 快照。
- `evidenceMissing`: 当前项目通用 detector、solver 和 Unity resolved snapshot。

### `UI-AI-002` placeholder promoted to commercial texture

- `severity`: `identity/authority`
- `erroneousBehavior`: 白图、procedural、搜索图或无许可证 AI 图被标记为正式素材。
- `triggerAndSymptom`: 主视觉不可辨识、缓存污染、来源不可复现、Atlas owner 缺失。
- `rootCause`: 只有槽位或未经解析的 fallback 时，模型会把可见候选误当正式素材；必须绑定 resolver/provenance gate。
- `preventionCheck`: strict quality gate 要求 hash、provenance、license、import/crop/atlas 和 resolutionSet。
- `correctAction`: 解析到已授权 Sprite/Texture，或显式保持 deferred/blocked。
- `recoveryAction`: 移除无来源素材，使引用它的 baseline stale，回退到标明用途的 placeholder。
- `evidencePresent`: AssetManifest 知识、`resolve_ui_asset_manifest.py` 10/10 静态回执、Unity Image/SpriteAtlas 来源锁、项目反馈规则。
- `evidenceMissing`: 商业许可证审核、Unity 导入和 GPU 辨识度复核。

### `UI-AI-003` uniform scaling across profiles

- `severity`: `lifecycle/partial`
- `erroneousBehavior`: 一套 bounds 按屏幕比例缩放，未做 profile-specific reflow。
- `triggerAndSymptom`: 窄屏挤压、底部导航与安全区冲突、长文本越界。
- `rootCause`: 没有 Reference Resolution、CanvasScaler、safeArea 和 profile owner 合同。
- `preventionCheck`: quality gate 要求 profile layout strategy、safeArea、reflowPolicy、minSize 和 long-content fixture。
- `correctAction`: 为每个 profile 选择 stack/flow/scroll/omit 变体并记录原因。
- `recoveryAction`: 回到上一个通过 profile，禁止只改 offset 或颜色。
- `evidencePresent`: Unity 2022.3 Canvas/CanvasScaler/Screen.safeArea 来源锁。
- `evidenceMissing`: 当前 Unity 多分辨率截图、resolved RectTransform 和真实设备安全区。

### `UI-AI-004` silent font fallback

- `severity`: `identity/authority`
- `erroneousBehavior`: 默认字体或静默 fallback 被视为字体已合理。
- `triggerAndSymptom`: 方框、字重漂移、数字宽度变化、不同设备结果不一致。
- `rootCause`: 把 TMP 包 API 能力当成项目 Font Asset 链事实。
- `preventionCheck`: quality gate 要求 Font Asset identity/hash/license/glyphs/fallback/locale fixtures。
- `correctAction`: 绑定可复现字体资产与 fallback 顺序，缺字形即阻断视觉结论。
- `recoveryAction`: 降级为 runtime-not-run，补字形和字体证据后重采集。
- `evidencePresent`: TMP 3.0.9 包源码和官方来源锁。
- `evidenceMissing`: 当前目标字体资产、许可证、字形覆盖和 Unity capture。

### `UI-AI-005` static quality gate promoted to visual acceptance

- `severity`: `identity/authority`
- `erroneousBehavior`: Validator、非空 PNG、低 diff 或旧 baseline 被当成真实 UI 可用。
- `triggerAndSymptom`: 无当前 spec/profile/state hash、无 GPU/输入回执却报告 Accepted。
- `rootCause`: 静态、物化、GPU 和 Runtime 层被压成一个布尔值。
- `preventionCheck`: quality gate/evidence ledger 要求每层独立 verdict 和当前输入身份。
- `correctAction`: 只报告 static-passed；没有 Unity/GPU 证据保持 runtime-not-run。
- `recoveryAction`: 标 stale 并从当前 spec/source hash 重采集。
- `evidencePresent`: ES evidence contract、官方 Skill 和 UI-FB-004/005。
- `evidenceMissing`: 当前大厅的 Unity/GPU/PlayMode/输入证据。

## ES 落地边界

- Knowledge 层负责路由、分歧和失败规则；不保存漂移的资产事实。
- ScreenSpec/Validator 可强制质量门字段完整，但不能伪造素材、字体或 Unity 结果。
- Materializer 只能消费已解析的路径/ID，不得把 placeholder 自动升级成商业素材；当前 path-first resolver 已实现，但商业验收仍 deferred。
- Fixture Driver 必须为每个 profile/state 提供确定性输入，但不拥有真实业务数据。
- Visual Evaluator/Repair Planner 必须改变 ScreenSpec、Registry、Validator 或 Materializer 字段；只改缓存、颜色或文件名视为未吸收反馈。

## RequiredReads

- `Documentation/AIKnowledge/entries/game-ui-ai-workflow-divergence-and-quality-gates.md`
- `Documentation/AIKnowledge/entries/game-ui-capability-readiness.md`
- `Documentation/AIKnowledge/entries/game-ui-visual-design-system.md`
- `Documentation/AIKnowledge/entries/game-ui-asset-manifest.md`
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md`
- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md`
- `Documentation/AIKnowledge/UI/unity-official-agent-skills-source-snapshot.md`
- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md`
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md`
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md`
- `.agents/skills/es-ui-prefab-authoring/SKILL.md`
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_asset_manifest.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_layout_plan.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_typography.py`
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs`
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`

## SourceRefs

- `Documentation/AIKnowledge/UI/game-ui-design-official-source-lock.md` (`d29ff698cd8fc3b0a3e014efe1780ef4a141e620b05cd3bf22be9d72ab3548de`)
- `Documentation/AIKnowledge/UI/unity-official-agent-skills-source-snapshot.md` (`d6ea6f0e721138b3f7bf1c43bc376fcbd72141ae72a68d98872abfbe1f24f4ce`)
- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md` (`062317be13f5e6385307dc31ff5d0f1830798ffdc7c092dab3e8ede46ebbbac5`)
- `Documentation/AIKnowledge/entries/game-ui-capability-readiness.md` (`bfe259f660df4f21b0ab376f66d0c51976dc69d63cf047251b7be8dd766293f6`)
- `Documentation/AIKnowledge/entries/game-ui-visual-design-system.md` (`a07d25d4224d6c19be6ddf3fc5001bd284111ad702e95d20fd761ebe00a21bf4`)
- `Documentation/AIKnowledge/entries/game-ui-asset-manifest.md` (`598789e8246d7074318d77ef11b191ba68599f89ccacbec63b4eea1fd26f3c7f`)
- `Documentation/AIKnowledge/entries/ui-automation-authoring.md` (`7e64233bdcfd1e783a74085c456feab2409bb72511868e9b73e7176b566df3e3`)
- `Documentation/AIKnowledge/UI/unity-ui-canvas-layout.md` (`71a8fa144fd889fa5f1f0e7ffb729dcc357631d25787d23be1f97485cb714aaa`)
- `Documentation/AIKnowledge/UI/unity-ui-interaction-rendering.md` (`516fd7bfd63c2cab9d715438c36d40235b2ad7634ba29de56f15423a97881ce2`)
- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`bdb62892fbb71c62ace08c2905f7ee8dae62bfdea2110a5344316b19fde8ac83`)
- `.agents/skills/es-ui-prefab-authoring/scripts/screen_spec_adapter.py` (`89a5c22371862264cf756c92f8cf56acf74b22a3469ee1af9b348d3982fc0176`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`8186a2cd36ef2083f833ba0a61029a800a72b15992991bc6948bd944d413bdb3`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIScreenSpecAdapter.cs` (`c151c56d1555cc4290e60ebd0d024d069d365724effea173bff9cf20cfc1ae25`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`1ae6e4ef04d6e6fb75774b4231727a7e1a514677c000812b0115f2ac6d68e7e4`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)

## Evidence boundary

本条目证明固定来源快照中描述的路线差异和当前 ES 静态合同边界。它不证明第三方仓库安全或可维护，
不证明任何 Sprite/Font Asset 已授权，不证明当前项目已经拥有 resolver、layout solver、Token consumer、
字体链、GPU 视觉评估或 Unity Runtime 输入闭环。没有绑定当前输入的 Unity/GPU/PlayMode/Profiler/Player/
IL2CPP 回执时，结论保持 `runtime-not-run`。
