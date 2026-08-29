# 编辑器窗口外壳迁移边界

`KnowledgeId`: `es.aiwarning.editor.window-shell-migration-boundary.v1`  
`Authority`: `AIWarnings + current editor window shell source`  
`RouteKeys`: `aiwarnings`, `editor`, `window`, `menutree`, `singlepage`, `odin`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `47a3004d7f40cf4e6e41d1a5ff0b3e0151c22eeb574bce22f1cb7df98e79b47d`  
`SourceSetHash`: `47a3004d7f40cf4e6e41d1a5ff0b3e0151c22eeb574bce22f1cb7df98e79b47d`  
`EntryBodyHash`: `0c094663a10c12d08e6eb2e7c6342829be37155024de42afd3c260bae2840ea8`  
`StaleWhen`: MenuTree/SinglePage/Odin 外壳、页面上下文、Presentation 或窗口程序集边界变化。

## 迁移范围

原 Warning 137 行、8,247 UTF-8 字节；现 Warning 保留外壳选择、旧类型隔离、行为保真、页面动作层、临时检查器和证据边界。窗口清单、迁移事实、适用/谨慎/禁止迁移判断、TrackView 特例和验证步骤迁入本条目。

## 当前事实

- `-ESMenuTreeWindow.cs` 提供新版 Toolkit 菜单树、无导航单页、IMGUI 单页和 Odin 兼容外壳；`ESIndependentInspectorWindow` 负责独立检查器边界。
- 已迁入 IMGUI 单页的窗口包括 ESInstaller、多个 Solver 示例和交互调试窗口；AssetPackage 使用 Toolkit 菜单树。TrackView、Stable Graph 等复杂主体保留自身 UI Toolkit 主体，仅接共享 Presentation/动作宿主。
- TrackView 临时轨道/片段/技能编辑使用 `ESIndependentInspectorWindow` + Odin 兼容外壳，仍由业务窗口按需打开并保留保存、刷新、SetDirty 等原逻辑。
- 复杂 Graph/Timeline/播放/选择/拖拽/焦点窗口应谨慎迁移；弹出菜单、输入对话框和只需保持复杂交互的主体不应为统一外观强塞菜单树。
- 页面外壳统一动作层、稳定页面 ID、显式 owner/ownerKey 和半休眠；窗口尺寸不能作为状态推断。Unity 菜单和交互矩阵仍未验证。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器窗口迁移_ESMenuTreeWindowAB适配_AI协作警告.md` (`7278694b4f706ea2bd82c92fc30c0eb9d5db445a35e06784b8961bea7591a488`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs` (`80f81ff4d063fb6760d648eeb071ce3bf956cefcc3851a89053bce1d5e15b531`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESIndependentInspectorWindow.cs` (`d3a154c193c44a6077eeb4e1b91e1949e15c10f4c4315ed12f27ed19168de8cb`)
- `Assets/Plugins/ES/Editor/Installer/ESInstaller.cs` (`4288d5332ed9cd05af7e7cda3a516fb9ab92e92e8161be7d70eef97d96ad35c4`)
- `Assets/Scripts/ESLogic/Editor/EntityBasicInteractionDebugWindow.cs` (`b623fd8f512129796b5a29b6e47dd83ac2a1c4dafdb9d535f30b55c65b927b10`)
- `Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTemporaryInspectorWindow.cs` (`e500010bbb93cba6e09f18150e25e357a887703fb02199d6fcb43e0539400190`)

## EvidenceRefs

- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs`
- `runtime-not-run`
