# ScreenSpec Materializer 视觉证据边界

`KnowledgeId`: `es.editor.project-screen-spec-materializer.visual-evidence.v1`
`Authority`: `AIWarnings P0 + current Materializer source + governed evidence contract`
`RouteKeys`: `ui-automation`, `visual-qa`, `visual-evidence`, `fixture`, `gpu-capture`, `snapshot`, `runtime-evidence`, `evidence-boundary`
`ContentHash`: `0dfe6ff12ee21e492661785a9b64cec3caa4dea156d1a1399f68d0ac5a3df909`
`EvidenceLevel`: `S1`
`RuntimeEvidence`: `runtime-not-run`

## Scope

本条目区分 ScreenSpec 静态合同、Unity 物化、结构快照、GPU PNG、真实交互和发布证据。
当前仅完成源码、合同和 P0 的静态核对；没有执行 Unity 或读取本次新鲜运行产物。

它负责 UI Materializer 专属证据产品、身份绑定、证据升级和报告边界；不负责 ScreenSpec
注册机制、Prefab/Fixture 保存事务、通用测试夹具治理、Presenter/业务正确性或发布签收。

## Trigger and routing

- 自然语言触发：截图是不是证据、PNG 是否有效、视觉 QA、GPU capture、结构快照、
  profile/state 证据、画面空白、静态检查能否证明 Unity、发布证据边界。
- 精确 `routeKeys`：`ui-automation`、`visual-qa`、`visual-evidence`、`fixture`、`gpu-capture`、
  `snapshot`、`runtime-evidence`、`evidence-boundary`。
- 目标 route-pack：本条目应为 ScreenSpec/Materializer 视觉证据判断的 canonical owner；需要生成结构时
  追加 prefab/fixture 条目，需要通用测试设计时追加 fixture-visual-qa，总量不超过 3。
- 当前发现合同：带 UI 领域信号的视觉、截图、GPU 或像素任务会推导 `ui-automation` +
  `visual-qa`；涉及证据/PNG/快照时还应推导 `visual-evidence`。本条目必须进入 Top 3，
  umbrella 或视觉设计条目不得替代输出证据判断。
- 相邻误路由：`snapshot` 可能命中任务读取快照，`fixture` 可能命中场景 Builder，`visual-qa` 可能命中通用 Fixture QA。
- 回退：要求同时出现 UI/ScreenSpec/Materializer 领域信号；若计划未返回本条目，必须标记
  `KnowledgeCoverageGap` 并显式补入 `visual-evidence` + `gpu-capture` 重新路由，只有本条目
  进入 Top 3 才算成功。禁止用 umbrella 或无关 snapshot 条目替代证据边界。
- 路由验收：自然语言探针必须记录推导键、Top 3、预期 owner 和无关条目；PNG、profile/state、
  placeholder 或静态发布判断缺少本条目即失败，即使已有其他证据类条目命中也不能算通过。

## Required reads

- 常驻：`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`、
  `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md`、
  `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` 与
  `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md`。
- Index 闭包限制：当前机器 `requiredReads` 仅列本文；在共享 Index 获得独立授权并补齐前，
  执行者必须按本节手工补读两条 P0、Materializer 与证据合同，缺一项即停止。
- 生成身份或层级问题：再读 prefab/fixture 条目及本次 ScreenSpec。
- 通用 Fixture 测试设计当前没有可消费的 canonical AIKnowledge 条目：必须回读 AIWarnings Start 链、
  当前源码和真实验证证据，并报告 Knowledge 覆盖缺口。`es.engineering.fixture-visual-qa.v1` 已弃用；
  即使旧投影仍将其列为候选，也只保留历史追溯，不得作为现行事实或实现指导。
- Player、IL2CPP、性能或发布结论：必须读取对应验收 Skill 并取得新鲜运行回执；选用受管通道时再校验 AICommand 与 TaskContract。

## Decision rules

1. 只做源码、合同或 SourceRef 检查时可以继续 Static 判断，但结论必须固定为 `runtime-not-run`。
2. spec hash、Unity 版本、runId、sceneGeneration、profile/state 矩阵不完整或互相不一致时，停止接受整组证据。
3. PNG 只有在 GPU 可用的当前运行生成、像素非空且逐 viewport/state 人工或确定性检查通过后，才可进入 `S3-visual` 子标签。
4. `-nographics`、文件存在、文件长度大于零、单色像素或日志写出成功均不能作为视觉通过。
5. 缺 Prefab/Scene 重读、结构快照或任一声明的 profile/state 时，标记 `Blocked`；存在 placeholder 时标记 `Deferred`，不能省略。
6. 真实输入、Presenter、业务数据、PlayMode、性能、Player、IL2CPP 和发布必须由当前用户明确点名并取得对应运行证据；只有受管执行时才要求各自 AICommand/TaskContract。
7. 证据根或 SourceRef 发生漂移时，废弃旧接受结论并重新规划；不得混用旧快照、旧 PNG 或旧 runId。

## Verified facts

- Materializer 通过 RenderTexture、Camera.Render、ReadPixels 与 PNG 编码生成像素产物。
  来源：`ESUIGameScreenMaterializer.cs`。
- 当前产物分为 editor、ui、scene 三类 JSON 快照和 PNG；每类只能证明其声明字段。
  来源：`ESUIGameScreenMaterializer.cs` 与 materializer contract。
- 快照包含 profileId、stateId、specHash、runId、sceneGeneration 等身份字段；字段存在不证明调用方绑定正确。
  来源：`ESUIGameScreenMaterializer.cs`。
- P0 明确禁止用源码、按钮、文件、静态编译或临时预览替代对应运行证据。
  来源：AI 交付声明 P0 与实际可玩闭环 P0。

## External authority cross-check（不提升项目证据等级）

以下结论来自 Unity 2022.3 官方 API 文档的联网复核，只用于约束检查方式，不能替代本项目运行回执：

- `Camera.Render` 只表示请求该相机手动渲染；调用返回不能证明 Canvas、材质或像素内容正确。
- `Texture2D.ReadPixels` 从当前激活的 GPU RenderTarget 读回像素，可能等待 GPU；必须记录 RenderTexture
  尺寸、激活目标、读回区域并做非空像素审查，不能只检查 PNG 文件长度。
- `PrefabUtility.SaveAsPrefabAsset` 返回保存对象不能替代保存后重新读取、导入完成、GUID/层级核对和资产验收。

官方交叉核对地址（当前版本）：
`https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Camera.Render.html`、
`https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.ReadPixels.html`、
`https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PrefabUtility.SaveAsPrefabAsset.html`。
这些 URL 不是项目 `SourceRefs`，页面变化时只触发人工复核，不得把网络访问本身写成项目事实。

## Evidence products

Materializer 对每个 `profile x state` 组合使用 RenderTexture、Camera.Render、ReadPixels 和
PNG 编码，并在同一证据根写入：

| 产物 | 当前源码表达的范围 | 不能替代 |
|---|---|---|
| `<profile>__<state>.editor.json` | 层级、父子关系、active、sibling、Anchor/Pivot、几何、组件、语义和布局组 | GPU 像素、真实交互 |
| `<profile>__<state>.ui.json` | active、Button interactable、Graphic raycast、文本、屏幕矩形和语义 | Presenter、业务数据、真实输入 |
| `<profile>__<state>.scene.json` | Fixture root、Canvas 和运行身份字段 | 场景加载、PlayMode、发布 |
| `<profile>__<state>.png` | 指定视口下由 Fixture Camera 捕获的像素文件 | 非空画面、视觉正确、交互或性能 |

快照同时携带 `profileId`、`stateId`、`specHash`、`runId` 和 `sceneGeneration` 等身份字段；
它们用于防止混用不同输入或不同运行的证据，但字段存在本身不证明值已被可信调用方正确绑定。

## Acceptance ladder

- `S1`：当前源码、合同和哈希已经静态核对；不包含编译或 Unity 运行事实。
- `S2`：按 P0 需要适用的静态编译或明确静态测试输出；本次没有执行 UI 源码编译。
- `S3`：当前运行实际完成 Unity 导入/Batch 物化，并重新核对 Prefab、Fixture Scene 与结构快照。
- `S3-visual`：这是 UI Materializer 合同在 P0 `S3` 内部使用的领域子标签，不是新的全局
  证据等级。它还要求 GPU 可用的当前运行生成 PNG，并逐个 profile/state 检查像素非空、
  内容可见、层级完整、无越界/重叠/裁剪问题；单色透明或仅文件非空不能通过，也不能升级为交互证据。
- `S4`：在 Unity Editor 中完成目标交互，仍不能替代 PlayMode、Player、性能或发布证据。
- `S5`：PlayMode、EditMode 测试或运行观察按目标范围通过。
- `S6`：Player/IL2CPP、资源、性能和发布链路按明确范围通过。

`-nographics` 可用于结构证明，但按当前合同不能产生有效视觉基线。Fixture 的 selected、disabled、
empty、loading、error、long-content 只是确定性视觉驱动，不是业务状态模拟成功。

## Common AI failure modes

| 错误行为 | 典型症状 | 根因 | 预防检查 | 正确动作 | 恢复动作 | 缺失证据 |
|---|---|---|---|---|---|---|
| PNG 存在即通过 | 透明/单色图被签收 | 文件证据替代像素证据 | 检查尺寸、alpha、极值和内容区域 | 逐 profile/state 做像素与布局检查 | 废弃 PNG 并 GPU 重采集 | 新鲜像素审查 |
| 混用旧快照 | specHash/runId 不一致 | 未绑定一次运行身份 | 对齐全部身份字段 | 只接受同一证据组 | 清空本次接受结论并重跑 | 同 runId 完整矩阵 |
| 结构快照冒充交互 | Button 存在但不能操作 | 组件存在被当作消费者闭环 | 检查真实输入与 Presenter | 转入交互/PlayMode 验证 | 降级为结构证据 | S4/S5 回执 |
| 漏掉异常状态 | 只截 default/wide | profile/state 矩阵不完整 | 与 spec 声明逐项对账 | 补齐 empty/error/long-content 等 | 标记 Blocked 后补采 | 完整矩阵 |
| placeholder 未披露 | 画面可见但非正式美术 | fallback 被当作成品 | 检查 AssetManifest provenance | 单列 placeholder/Deferred | 替换资源后重新物化 | 正式资源证据 |
| 静态通过冒充发布 | 报告写“商业可用” | 跨越证据等级 | 按 P0 逐级检查 | 保持 S1/runtime-not-run | 撤回越级结论 | Player/IL2CPP/发布回执 |
| `Camera.Render` 返回即接受视觉 | PNG 有输出但相机未绑定目标 Canvas、材质缺失或画面全透明 | 把渲染 API 调用成功误作视觉结果 | 记录相机、Canvas、RenderTexture、尺寸并做像素内容审查 | 先阻断 S3-visual，补齐绑定和逐状态像素检查 | 丢弃该 PNG，使用新 runId 重采集 | Unity/GPU 回执与人工或确定性像素检查 |
| `ReadPixels` 读回错误目标 | PNG 尺寸正确但内容来自旧 RenderTexture/错误 viewport | 未验证激活目标、读回区域或 GPU 同步 | 采集前后记录 active target、rect、width/height，校验像素分布 | 不接受该 PNG，重新绑定目标并重采 | 清理混用产物，保留失败诊断 | 当前运行 RenderTarget 与像素审查 |

## Execution checklist

- 开始前：读取 Start 链、两条 P0、本文、materializer contract 与本次 spec；核对 SourceRef 和证据根身份。
- 采集前：固定 Unity 版本、spec hash、runId、sceneGeneration、viewport、profile/state 和 placeholder 清单。
- 采集中：禁止 `-nographics` 冒充视觉基线；每个组合同时保留结构快照与 GPU PNG。
- 完成后：检查文件身份、像素非空、内容可见、层级完整、越界、重叠、裁剪和状态差异。
- 失败/取消：保留失败原因，不混入旧基线；重跑必须使用新的 runId 或明确的同输入重试关系。
- 禁止：用文件存在、进程退出码、单张截图或测试源码存在替代完整接受矩阵。

## Required reporting boundary

报告必须分别列出 spec hash、Unity 版本、profile/state 矩阵、Prefab/Scene 路径、快照路径、
PNG 路径、占位资源与未验证项。源码存在、生成入口返回、Prefab 保存、Scene 保存、PNG 文件存在
和视觉通过是不同事实，任何较低层证据都不能替换较高层证据。

## Evidence boundary

- Static 可证明：捕获代码路径、快照字段、证据合同和 P0 声明边界。
- S3 需要当前 Unity 运行完成物化并重读 Prefab/Scene/快照；S3-visual 还需要 GPU PNG 像素审查。
- S4/S5/S6 分别需要编辑器交互、运行测试、Player/IL2CPP/资源/性能/发布证据，不可互相替代。
- 本条目的当前结论固定为：`S1`、`runtime-not-run`。因此不能声明 Materializer 当前可运行、
  Prefab/Fixture 已验收、画面正确、输入可用、性能达标或发布通过。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`26c7a8382b5f95830cf13f26819faecbf89f4f84484ac3c1282c84fb6ab14801`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_实际可玩闭环与运行证据_AI协作警告.md` (`ef80427c19ab315e9d69ec810caaabb0164a7a2b93f6406d7ee4c5cdd8b7d740`)

`StaleWhen`: Unity 版本、Materializer 捕获/快照实现、证据等级合同、UI 作者工作流或 P0 交付/运行证据规则任一 SourceRef 变化。
