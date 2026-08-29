# UGC 工作台 Draft 与提交边界

`KnowledgeId`: `es.aiwarning.editor.ugc-workbench-draft-commit-boundary.v1`  
`Authority`: `AIWarnings + current Workbench/World editor source`  
`RouteKeys`: `aiwarnings`, `editor`, `ugc`, `workbench`, `draft`, `commit`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `caf28d59daeb17fa3840a964b1f91bc806aba3e306f31739f3eaffd5841aa1ab`  
`SourceSetHash`: `caf28d59daeb17fa3840a964b1f91bc806aba3e306f31739f3eaffd5841aa1ab`  
`EntryBodyHash`: `478f5c07459f79d81172a5a19e4afd8fdb4470b2253533aea4237140df59c00c`  
`StaleWhen`: Workbench Host、Contribution、Draft/Commit、Undo/Redo、拖放事件或 World 作者合同变化。

## 迁移范围

原 Warning 164 行、11731 UTF-8 字节；现行 Warning 保留作者工作台长期边界、Draft/Source 分层、稳定选择、拖放取消、Undo/Redo、预览生命周期、提交事务和证据限制。详细 UI 结构、World 当前实现、失败恢复、并发冲突、过时理解和验收矩阵迁入本条目。

## 当前事实

- `ESWorkbenchWindowBase`、`ESWorkbenchUIToolkitHost`、Contribution Registry、Authoring Contracts 和 Persistence Contract 构成 UI Toolkit 工作台底座；World 工作台通过 `ESWorldEditSession` 管理正式 Source 与 `HideAndDontSave` Draft。
- Draft 绑定 Baseline Hash、ChangeSet、SessionState 恢复和外部漂移阻断；提交通过显式 `TryCommit()`，失败路径尝试恢复正式 Source。保存作者态资产不等于 TerrainData、Scene/Prefab、碰撞、导航或运行时产物已完成。
- 工作台首屏应提供文档/状态/保存提交、资源与层级、2D/3D 视口、Inspector、问题/活动抽屉；窄屏折叠不得改变选择、编辑目标、Draft 或 Undo 归属。
- Host 统一承接拖放事件并幂等调用 `CancelWorkbenchDrag(true)`；预检不得改数据，正式执行必须解析坐标、层级、权限、锁定、预算和 Undo 目标。
- 多窗口各自拥有 Draft/Baseline；后提交者发现 Source 漂移必须阻断。Reload 后先验证 Source 身份和漂移，再恢复 Draft、稳定选择和布局。

## 未完成与证据边界

当前源码仍为 `Implemented-Unverified`；Undo/Redo 与领域 Draft 快照同步测试、真实 Unity 布局/拖放/锁定/Reload/冲突/失败注入、Profiler/Memory 证据和跨 World 层验收未完成。源码、UTF-8、`.csproj` 或截图不能替代这些门禁。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`5a102b615d65c97d036d8e837a6778a3f61fee73b1b0a50d390db9d303442e52`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs` (`5421e911b3755cb1ea6528ee088e6ef0dff08041ed98a8864012a485752c3026`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs` (`bfd421472dd2e8f91e3c90f6993137d3f2911dbf4401874c39a95fe686e96fe9`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchAuthoringContracts.cs` (`0fee85d38bd4e493cae51cfbb7bb3ba669a49273750ea913ec47aa57342f1fc9`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchContributionRegistry.cs` (`3868a78877bd62b0814abb79fa38abb7f6dbd07cad71f0d39823bd8b79e79978`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchPersistenceContract.cs` (`c3eee7f6deb1318904b06ebc0354886e5d85437829f448cc286347660c103c4e`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs` (`a4a8bc7e8ee0b5a353c89baceb0447e4f9723ed8e8c8dbd9dba62a6bb3b0dafd`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldEditSession.cs` (`fe0898624634285d6a7381a2a5001979ab46938d1ba71873bf3e5439cae84471`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldAuthoringViewport.cs` (`adf167839918070dd90e74813b23d2f3e773f8a9446004df5276ee8e7ac4277c`)

## EvidenceRefs

- `Assets/Scripts/ESLogic/Editor/World/ESWorldEditSession.cs`
- `runtime-not-run`
