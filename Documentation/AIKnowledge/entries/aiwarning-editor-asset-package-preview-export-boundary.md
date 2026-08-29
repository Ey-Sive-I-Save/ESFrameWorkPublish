# AssetPackage 预览与导出边界

`KnowledgeId`: `es.aiwarning.editor.asset-package-preview-export-boundary.v1`  
`Authority`: `AIWarnings + current AssetPackage editor/preview source`  
`RouteKeys`: `aiwarnings`, `editor`, `asset-package`, `preview`, `export`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `d8249e6fbbd460e5597925d94cdbaf14906111faee06b2741cc2610e504a831b`  
`SourceSetHash`: `d8249e6fbbd460e5597925d94cdbaf14906111faee06b2741cc2610e504a831b`  
`EntryBodyHash`: `97f30c78c9ec283a2816f9405e2a2b82dc4d494c1694e1feaae2b48e10a987f9`  
`StaleWhen`: AssetPackage 数据/窗口、PreviewWorkflow、导出事务、缓存策略或回退合同变化。

## 迁移范围

原 Warning 124 行、11847 UTF-8 字节；现行 Warning 保留编辑器/运行时边界、预览唯一底层、缓存与临时资产约束、导出链路和证据边界。详细界面标准、预览生命周期、导出事务、当前实现事实、过时方案和未完成风险集中在本条目，原文通过迁移台账及 SourceRefs 可回溯。

## 当前事实

- AssetPackage 窗口负责资源包烘焙、分类/搜索/排序、预览、标记使用、依赖通报、导出、链路记录和回退；它不是运行时资源加载系统。
- `ESAssetPackagePreviewWorkflow` 是预览生命周期入口；`ESAssetPackageBakeData`/`ESAssetPackageBakeWindow` 提供 EditorOnly 配置和窗口能力。当前源码显示小格子采用磁盘帧缓存，大预览采用实时渲染。
- 小格子队列应优先当前页，缓存位于项目根目录外部；不得恢复每帧 `PreviewRenderUtility`、临时 AnimatorController 或旧的大容量缓存设计。
- 导出使用源 GUID、依赖/文件 Hash、目标身份、配置指纹和操作原因形成 Resolution Snapshot；源/目标漂移需显式确认，写入支持 staging、backup、失败恢复、部分回退和链路失效治理。
- `ES选用_` 是默认可配置前缀，不是不可改写的业务常量；重复导出默认不覆盖、不自动生成 `_1`，有效性必须同时检查源 GUID 链路和目标存在。
- 页面应使用统一 ES Presentation Surface/Toolbar/Header/Meta/状态色和单一滚动容器；依赖树、独立 dry-run、批量链路治理和完整自动化矩阵仍是未完成风险。

## 过时方案与验收边界

已废弃：每个动画每帧实时渲染、临时 AnimatorController、仅凭目标存在判定导出、默认自动改名。静态脚本、UTF-8、一次截图或 `.csproj` 编译不能替代 Unity Editor 交互、Domain Reload、Undo/Redo、Profiler 和发布验收。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/资产包分离（AssetPackage）/资产包分离窗口_预览与导出链路_AI协作警告.md` (`ff0539d8769216b873190dfbba402e5a933524fb0d355969a65178f656e3d9aa`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESAssetPackageBakeData.cs` (`2ce2365230f6fef88489f6fa2095970bb47f4bf7d309e0e244fa5a8481010af0`)
- `Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs` (`4d0b09557c54ec808d6c9994d7904b7b2fb829517d1099048f12a939e3f47762`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewCore.cs` (`b1042b43ebb0c600b8e42f080c0c60b85e24b8e942c5f99f50f5c0165ff851de`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewUtility.cs` (`a3670b59f6a6924f9353ad6f7d95e232076eac0dd6b616558ae02d86bfbd44a1`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewResourceScope.cs` (`08e7c9afec2949bda66c578b5d472230955105e70bf9af0c702d1558504b68d3`)
- `ES/Tools/Validation/Test-ESMenuArchitecture.ps1` (`7f0a0f58d7f5bae052a708b6a39ab9583c02f37300c140aad9a9836c2a1bc345`)

## EvidenceRefs

- `ES/Tools/Validation/Test-ESMenuArchitecture.ps1`
- `runtime-not-run`
