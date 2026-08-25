# Story 与 World 运行时/制作边界

`KnowledgeId`: `es.project.story-world-runtime-authoring.v1`  
`Authority`: `Source`  
`RouteKeys`: `story`, `quest`, `dialogue`, `world`, `world-map`, `save`, `catalog`, `authoring`, `content-drift`  
`ContentHash`: `835a51d816eca9e17d7a9044e83a223d0a89568394c5ada852ed7ad75721ba5e`

## Story 当前能力是受限切片

Story Definition 以稳定 StringKey 作为 `DefinitionId`，并携带 `ContentVersion + ContentSignature`。Catalog 使用 BeginBuild/EndBuild 候选代事务：先完整构建新的 retained key table，再原子替换 current generation；相同 ID 的版本或签名冲突会失败。解析可要求版本和签名同时匹配，避免把旧存档绑定到漂移内容。

Validator 检查入口、重复/不可达节点、循环、分支目标、ActionId、稳定 Tag 与本地化 Key。当前源码明确拒绝 enum 身份，警告 LegacyLiteral，并对 `ESStoryKind.Story` 报错：切片 A 尚未提供长期 Story 专用进度。因此当前可描述 Quest/Dialogue 切片，不能宣称完整长篇剧情系统。

## Story 实例并发与迟到提交

Instance 同时持有定义快照、Actor、InteractionBinding、当前节点、Revision、NodeVisitSequence、SessionId/Generation、ViewRevision、RuntimeModeLease、ExecutionTicket 和 QuestRecord。UI 提交必须同时匹配前台实例、WaitingForUI、实例 revision、session id/generation 与 view revision；只按 instanceId 判断会接受旧 UI、旧会话或上一节点的迟到输入。

ExecutionTicket 记录 expected revision、node visit sequence、action id 和 Prepared/Succeeded/Failed/Discarded 状态，用于把外部 Action 的准备与提交分开。最小 `ESStoryDialoguePresenter` 仅是可替换调试 UI；正式 UI 应实现接口，不能把 OnGUI 示例当产品界面。存档 schema 当前为 2，保存 QuestRecord 与 checkpoint metadata。

## World 定义、运行状态与存档

`ESWorldMapDefinition` 描述地图内容：来源模式、Terrain/Heightfield、Surface、材质/植被/散布、Navigation、Weather、Streaming、Collision、Build、UGC 限额、Region、POI、Prefab 与 Dialogue placement。`EnsureAuthoringContainers` 只补齐缺失容器，不应偷偷改写已有作者数据；`IsValid` 是进入运行或发布前的结构门。

`ESWorldMapModule` 分离 `CurrentDefinition` 与 `CurrentState`。加载定义后建立带 mapId/contentVersion/contentHash 的运行状态；区域发现、POI 解锁等变化写入 Save。候选存档采用 Validate/Prepare/Commit/Rollback 分阶段应用，并在 World phase 检查已加载 mapId 以及内容版本/Hash；内容漂移返回明确失败，而不是勉强套用旧状态。

## 编辑会话与正式输出

`ESWorldEditSession` 是 Source/Draft 事务边界，负责 baseline、dirty、validate、commit/rollback 与冲突处理；它不是运行时 WorldModule。Workbench、Viewport、Terrain facade 和 Dialogue 工具修改草稿，只有显式 Commit 后才进入正式资产。场景预览、procedural preview 或编辑器生成对象不等于 BuildSettings 指向的正式场景、导航数据和资源管线产物已发布。

## 当前非宣称

- 没有 Unity PlayMode/目标平台证据时，不宣称 Story 全流程、存档迁移或 World Streaming 已商业验收。
- World 定义包含 Navigation/Vegetation/Streaming 配置，不等于每个后端都已实现并接入发布。
- 当前 Story Validator 主动限制长期 Story；知识库不得用规划描述覆盖这个源码事实。

`StaleWhen`: Story/World Catalog、Save、编辑事务、运行时版本或任一 SourceRef 哈希变化。

## 静态测试证据

- `ESStorySliceATests.cs` 覆盖 RuntimeMode lease、Story 图验证、Catalog 候选发布/失败保留、迟到提交和 Save hook 边界。
- `ESWorldDialogueDataTests.cs` 覆盖重复节点、缺失目标边和 Dialogue placement 边界。
- `ESWorldEditSessionTests.cs` 覆盖外部基线漂移拒绝、Commit/Rollback、Domain Reload 恢复、多窗口冲突和 Undo/Redo 哈希一致性。

这些是源码与 EditMode 测试绑定；没有运行 Test Runner，因此不声明 Story 全流程或 World 运行时已通过。

## SourceRefs

- `Assets/Scripts/ESLogic/Runtime/Story/Definitions/ESStoryDefinitionCatalog.cs` (`df7be43d2e524d1c50a2bc3f6ab1c62831e64d6624b1c2d3ab0cf4f84db83231`)
- `Assets/Scripts/ESLogic/Runtime/Story/Instances/ESStoryRuntimeTypes.cs` (`0a991e9beac9aa4abdd6bdefccc967f9d6af395a529463cc51033b2b7cbfb01e`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESStoryModule.cs` (`900f1edc2c816b2e47e8a28fca08eb47e48c9f28ccbd645df548d190f788c78c`)
- `Assets/Scripts/ESLogic/Runtime/World/Map/ESWorldMapData.cs` (`a9ecd0961c53ca9a42105f33b5e160c4a0e8edab62dcf4176e0133083f3655c3`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/ESWorldMapModule.cs` (`da8038b0053e4306ecb1c2167452164be3d9fa7d25278661adad533e42ea8b76`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldEditSession.cs` (`8300cd18fd60715d75b5f1f74c7e6d2b023b5e4c59dd36df177cd665a0913f0b`)
- `Assets/Scripts/ESLogic/Tests/Story/EditMode/ESStorySliceATests.cs` (`94eda09935ba3bb094a606326447c69e986fea88a4e0f31f1221cee72d579202`)
- `Assets/Scripts/ESLogic/Tests/World/EditMode/ESWorldDialogueDataTests.cs` (`5f2967858401dc695d208a700b74971831a8876fa1aa0ecc7aee270e55913099`)
- `Assets/Scripts/ESLogic/Editor/World/Tests/ESWorldEditSessionTests.cs` (`87c39f5c2bb6008ccc362cc3b54fb3d61115a4aa43fcc616affaa3f8eb27cf88`)

`EvidenceLevel`: `S1`; `StaleWhen`: Story 切片范围、Catalog/提交防护、Save schema、World 定义/状态或编辑事务变化。
