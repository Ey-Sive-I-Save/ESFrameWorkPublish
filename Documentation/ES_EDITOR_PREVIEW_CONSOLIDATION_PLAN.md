# ES 编辑器预览底座收口计划

状态：生产预览底座统一已落地；Unity/Runtime 验收未运行。
目标：不保留旧 Preview Context 兼容双轨；所有编辑器交互预览共享公共资源生命周期，业务状态与渲染资源分离。

## Source / Target

### 公共 Target

- `ESEditorPreviewLifecycleHub`
- `ESEditorPreviewRenderContext`
- `ESEditorPreviewModelHandle`
- `ESEditorPreviewUtility`
- `ESEditorPreviewResourceScope`
- `ESEditorPreviewPersistentFramePaths`
- `ESEditorPreviewEnhancerSet`（LowEnd=0；增强器按需组合）
- 接入范围：AssetPackage、Workbench、CameraTrack、Particle、EntityState、CharacterTemplate、Composite Shader、FontTools
- 公共 RT 预算：单 RT 上限 192 MiB；所有活动预览合计 512 MiB，超出时优先按 2 倍递减分辨率，不关闭业务功能。交互尺寸统一限制在 64–2048 像素并按 8 像素量化，避免窗口抖动造成重复分配。

### AssetPackage 现有 Source

- `ESAssetPackagePreviewUtility`
- `ESAssetPackagePreviewWorkflow`
- `ESAssetPackageMaterialPreviewPlayer`
- `ESAssetPackageAudioPreviewPlayer`
- `ESAssetPackageAnimationPreviewPlayer`
- `ESAssetPackageDynamicPreviewPlayer`
- `Library/ES/AssetPackagePreviewFrames/AssetPackageBake`

## 迁移分类

| 内容 | 处置 | 原因 |
|---|---|---|
| Camera/Light/RenderTexture/PreviewScene 创建与释放 | adopt | 统一资源所有权和清理边界 |
| HideFlags、Layer、临时对象标记 | adopt | 复用公共安全标记和销毁路径 |
| AssetPackage 分类、动画采样、音频播放、材质参数 | adapt | 这是业务播放器，不应塞入公共底座 |
| AssetPackage 专用帧缓存 | adapt | 先保留兼容路径，再评估迁移到公共缓存规则 |
| 现有播放器对外入口 | preserve | 保留业务入口，不保留旧 Preview Context 实现 |
| 新增第三套 PreviewScene/Camera 管理器 | exclude | 禁止继续扩散双轨结构 |

## 分批顺序

### Batch 0：只读基线

- 记录每个播放器创建的 Unity native 对象、拥有者和 Dispose 路径。
- 标记 `usePreviewScene=true/false` 的差异。
- 记录 RT 尺寸、缓存目录、帧缓存清理和窗口关闭回调。
- 不改行为。

### Batch 1：公共资源会话切片（已落地）

- 新增 `ESAssetPackagePreviewSession.cs`，直接委托 `ESEditorPreviewRenderContext`。
- `ESAssetPackageMaterialPreviewPlayer` 使用 `PreviewScene` 公共会话。
- `ESAssetPackageAudioPreviewPlayer` 使用 `HiddenObjectsInActiveScene` 公共会话，并通过会话级 AudioListener。
- `ESAssetPackageAnimationPreviewPlayer` 使用 `HiddenObjectsInActiveScene` 公共会话。
- Clone 后才允许进入公共 Preview ownership，源 Prefab/Scene 不可直接搬运。
- Camera、Light、Layer、HideFlags、RenderTexture 和 Preview 清理不再由动画播放器自建。
- 公共核心提供 `LowEnd`、`Full` 及位掩码组合增强器集合；增强器资源采用惰性创建，默认 Full 保持既有表现，低端可显式传入 `LowEnd`。
- `ESEditorPreviewEnhancerBudgets.ForQuality` 提供 Fast/Balanced/High 的统一预算映射，业务播放器无需重复定义设备档位策略。
- 旧 `ESAssetPackagePreviewSceneContext` 已从 AssetPackage 编辑器链路移除，不保留兼容双轨。
- AssetPackage 静态模型、Composite Shader 烘焙和 FontTools 字体预览也已移除 `PreviewRenderUtility`，统一使用公共 Context 的模型、Camera、RT、Snapshot/GUI Render 和 Dispose。

### Batch 2：窗口级生命周期验证

- 静态确认窗口 `Suspend`、`Close`、页面隐藏和 `ReleasePreviewResources` 的调用关系。
- 验证重复打开、页面切换、窗口关闭、ReloadDomain 期间不会重复释放或跨窗口清理。

### Batch 3：公共会话增强与验收

- 继续将 AudioListener 的跨会话策略下沉为公共 AudioLease（当前为会话级 listener，避免静态共享状态）。
- 网格动画帧缓存、材质参数、字体文本、Shader 时间驱动和音频播放保持业务专用，但临时对象与渲染资源纳入公共 Context/Scope。
- UI Fixture 证据生成、Weapon Shot Profiler 正式诊断场景和 DynamicAtlas 运行时图集不属于编辑器交互预览，保留其专用 Camera/RT 生命周期，避免误迁造成能力损失。
- Unity/Profiler 只负责证明可用性和性能，不决定是否保留旧 Context 双轨。
- 失败时按批次恢复 `ES/Bak/Local/AssetPackagePreviewMigration_<timestamp>` 备份。

## 风险与停止条件

| 风险 | 检测 | 停止条件 | 恢复 |
|---|---|---|---|
| 预览对象跨窗口互相清理 | 生命周期计数、Owner 标记、多窗口测试 | 任一窗口关闭导致另一窗口对象消失 | 恢复本批次备份 |
| RT 未 Release | 资源诊断、关闭重开、Profiler | RT 数量或内存不能回到基线 | 恢复本批次备份 |
| PreviewScene 绑定错误 | 场景归属和 CameraScene 检查 | 对象进入正式 Scene 或渲染层错误 | 取消本批次并恢复备份 |
| 动画帧缓存不兼容 | 帧路径、版本和缓存清理检查 | 缓存污染 Assets 或旧帧误用 | 保留旧缓存目录，禁止切换 |
| ReloadDomain/PlayMode 状态丢失 | 重新加载和退出路径 | 出现残留对象、僵尸回调或错误恢复 | 只恢复公共工具函数，不迁移 Context |

## 当前结论

本计划当前已完成生产预览路径的公共底座统一；没有 Unity/Profiler 证据时，不宣称 Runtime 可用。后续需要完成窗口级生命周期、跨窗口资源预算及 Unity 实机验收。

## 本次变更回退点

- 备份目录：`ES/Bak/Local/AssetPackagePreviewMigration_20260827_222555/`
- 备份哈希：见该目录 `backup.sha256.txt`
- 已新增：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackagePreviewSession.cs`
- 已切换：`ESAssetPackageMaterialPreviewPlayer`、`ESAssetPackageAudioPreviewPlayer`、`ESAssetPackageAnimationPreviewPlayer` 使用 `ESAssetPackagePreviewSession`，其资源生命周期统一委托公共 `ESEditorPreviewRenderContext`
- 回退动作：恢复备份的 `ESAssetPackageBakeWindow.cs`，删除新增 Session 文件及其 `.meta`，然后重新执行静态检查和 Unity 编译/测试。
