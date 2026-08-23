# Editor Workbench、草稿事务与正式资产边界

`KnowledgeId`: `es.project.editor-workbench-authoring.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `editor`, `workbench`, `ui-toolkit`, `draft`, `undo`, `session`, `contribution`, `world`, `terrain`, `acceptance`  
`ContentHash`: `47840604dce786ba7bf07b6c2ade04c422ba5f9ec0d9539a484107a948f477f1`

## Workbench 基座

`ESWorkbenchWindowBase<This,TAsset,TModule>` 统一管理目标资产、文档/模式定义、SessionState 恢复、刷新原因、Undo/Redo、动作宿主、贡献注册和内容注册入口。派生窗口注册领域贡献，不自行重建生命周期基础设施。

Contribution Registry 在 Host session 打开时装配 descriptor，窗口关闭或重载时释放 session；模块可以贡献工具栏、面板、Inspector、注册槽位和作者操作，但不能直接取得正式资产写权限。标准基座先建立宿主和核心动作，再加载贡献，避免派生窗口绝对定位覆盖或重复注册。

## 草稿与正式资产事务

World 作者工具使用 `ESWorldEditSession` 将 Source 与 Draft 分离：

1. Open 根据源资产稳定身份和 window owner 创建隔离 session。
2. Draft 修改不污染 Source；`NotifyDraftChanged(path)` 记录变化并更新恢复状态。
3. `HasUntrackedDraftMutation` 用实际序列化哈希发现绕过通知的写入。
4. Commit 前比较 source baseline hash；外部漂移时拒绝提交并保留本地草稿。
5. 成功提交只跨越一个正式资产 Undo 边界，并立即使其他窗口 session 标记 external conflict。
6. ReloadFromSource 显式接受外部版本并清理冲突；RevertDraft 只重置本地草稿。
7. Domain Reload 通过稳定 session identity 恢复草稿；不同 window owner 的并行草稿互不覆盖。

这意味着 UI 中显示的“已编辑”不等于正式资产已经改变；只有 TryCommit 成功、后置条件通过、Dirty/Save 完成后才能声明提交。

## Undo/Redo 与一致性

Workbench 订阅 `Undo.undoRedoPerformed`，同步状态并刷新，但不会把 Undo 回调再次当成新作者操作。World session 在 Undo/Redo 后从实际序列化差异重建 ChangeSet，并持久化当前草稿状态。ConsistencySnapshot 同时记录 source/draft hash、实际 draft hash、Dirty、session owners 和 external conflict，用于验证内存、持久化和 UI 状态一致。

## 正式输出边界

- Scene、Prefab、TerrainData、ResourcePlan 和发布资产是不同权威对象，不能把预览或草稿截图当作正式产物。
- 内容注册槽位仍调用统一 ContentRegistration preview/commit，不由 Workbench 直接改 Library。
- Terrain 正式输出必须经过对应 facade/backend；Viewport overlay 或临时 GameObject 不是 TerrainData 证据。
- 生成报告、截图和 acceptance receipt 必须可快速打开并记录路径，但它们不自动提升运行证据等级。

## 现有测试定义证明了什么

`ESWorldEditSessionTests` 定义了：草稿隔离、旧数据容器修复、未跟踪修改检测、Revert、外部漂移阻断、单 Undo 提交、Domain Reload 恢复、多窗口隔离、提交后其他窗口失效、Undo/Redo ChangeSet 重建和一致性快照。`ESWorldWorkbenchAcceptance` 提供 live window/visual evidence 捕获入口。除非 Unity Test Runner 与视觉证据本次实际运行，知识库只能记录“具备测试/验收实现”，不能记录“已通过”。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`3ecdce71803c7acf2f6e513a46c14952ce70ddcd9d701138db3bb15e6da36328`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`d08d443a6b8bc4712142904375adb627b420981d674643b0ec3166753c152c37`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/专业工作台（Workbench）/专业工作台与World作者工具_贡献注册与正式资产边界_AI协作警告.md` (`50a1f1bbc68e78a1ad129fbdb6d6e2a4843e1b8e92888420c957eb87f62b59c4`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs` (`e98e109c6a26d90ab8195ebe2826e2bbdcf35a49dd69b624dba274ebda07b049`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldEditSession.cs` (`8300cd18fd60715d75b5f1f74c7e6d2b023b5e4c59dd36df177cd665a0913f0b`)
- `Assets/Scripts/ESLogic/Editor/World/Tests/ESWorldEditSessionTests.cs` (`03b47dd936a972898fc1b96f40630ca06df2db22b5ea06789191e8d738ac1574`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldWorkbenchAcceptance.cs` (`c2b315874bb01e838e78b8abf5e6b2035ef171eadcd76e25d1afe7b756dcdd33`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchAuthoringContracts.cs` (`957128f1097fcbaef93154d73d99d6a726894b582e7fb9c9a51dde610fde4a42`)

`EvidenceLevel`: `S1`（源码和测试定义；未运行本次 Unity UI/视觉验收）  
`StaleWhen`: Workbench Host、Contribution、Draft/Source 事务、Undo、恢复或正式输出后端变化。
