# ES UI 搭建与权威工作流

状态：`Implemented / Verifying`。源码、编译与定向 EditMode 已有证据，但尚未获得真实 PlayMode、Frame Debugger、Profiler、Player 或目标移动平台签收，不能升级为 Stable。

最后验证：2026-08-07。

- `dotnet-build`：`ES_Logic`、`ES_Logic.Editor`、`ES_Logic.DynamicAtlas.Tests` 与 `ES_Logic.DynamicAtlas.PlayMode.Tests` 均为 0 警告、0 错误。
- `unity-editor-compile`：Unity 2022.3.45f1 BatchMode 脚本导入/编译以返回码 0 结束；日志未出现 `error CS` 或 `Scripts have compiler errors`。
- `unity-test-runner`（EditMode）：动态图集定向测试 10/10 通过。
- 已确认：资源 Provider 未就绪时 Inspector 自动加载会等待；独立 Texture Copy 不依赖 Provider Ready。
- `PlayMode`：已具备 GPU Copy / GraphicsFence 测试程序集。筛选后的命令行重试完成脚本编译与程序集重载后仍未进入 Test Runner、也未产出结果 XML；无进展的本轮批处理已停止，该行保持阻断。
- `player-build` / `IL2CPP`：尝试的 Standalone Player 测试被“Currently selected scripting backend (IL2CPP) is not installed”阻断；未修改项目全局脚本后端。

适用源码入口：

- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasGraphic.cs`
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasRuntime.cs`
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasDomainOwner.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESDynamicAtlasModule.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs`
- `Assets/Scripts/ESLogic/Editor/UI/ESUIRiskAuditWindow.cs`

本页是给 UI 制作者和业务程序员使用的低门槛流程。它规定 UI 如何接入 ES，不能替代资源、输入、角色、战斗或剧情系统的运行时权威。

## 一、先记住一条权威链

```text
业务状态 / Presenter
        ↓ 只提供当前可表现数据
UI View（只表现，不保存业务事实）
        ↓
ES 资源引用（ESAssetRefer / ESAssetScope / Domain）
        ↓
渲染组件（Image / ESDynamicAtlasGraphic / RawImage）
        ↓
Canvas、材质、Mask、Stencil 与绘制顺序
        ↓
输入意图（ESInputModule）
        ↓
生命周期、Domain、诊断与发布验收
```

UI 可以保存显示状态、Lease 和本地缓存，但不能把角色血量、战斗结果、任务进度、剧情推进或输入设备状态复制成第二份权威。

## 二、组件怎么选

| 需求 | 首选 | 说明 |
| --- | --- | --- |
| 静态 Sprite、九宫格、Filled、Tiled、复杂 Sprite | `Image` | Unity UGUI 能力完整，适合长期存在的界面。 |
| 高频远端头像、动态下载图标、滚动列表纹理 | `ESDynamicAtlasGraphic` | 以 ES Domain/Lease 管理内容，使用动态图集页面和 UV，减少材质绑定与 Draw Call。 |
| RenderTexture、视频、相机输出、特殊 Shader | `RawImage` | 资源本身就是渲染目标时，不强行塞入动态图集。 |

简单判断：只要内容来自远端或运行时批量变化，优先考虑 `ESDynamicAtlasGraphic`；只要需要九宫格、Filled、Tight Sprite 几何或复杂 Image Type，优先使用 `Image`。

`Image` 当然可以显示 ES 资源系统热更新后的 Sprite；热更新解决的是“资源从哪里来、何时更新”，动态图集解决的是“运行时纹理如何合并和采样”。两者可以组合，但不能互相替代。

## 三、动态图集的最短使用路径

1. 在 UI 根节点添加 `ESDynamicAtlasDomainOwner`，绑定与页面相同的 Domain 生命周期。
2. 在目标节点添加 `ESDynamicAtlasGraphic`。
3. 选择资源内容 Key 和内容版本（例如头像的 ETag/Hash）；不要只使用用户 ID。
4. 运行时通过 `AcquireAsync`/`CopyAsync` 取得 `ESDynamicAtlasLease`。同一 Key 的并发请求由模块合并，调用者取消只取消自己的等待。
5. Graphic 每次重建网格时通过 Lease `TryResolve` 获取当前 Page、UV 和版本；Lease 失效时显示占位图或重新请求。
6. Domain 关闭、Provider 切换、Page Lost 或上传失败时，释放 Lease 并让 UI 回到占位状态。

上传任务在 GPU Fence 完成前临时持有源纹理 Lease；Fence 完成、取消或失败后释放源 Lease。源纹理 Lease 与图集 Lease 不得混用。

## 四、编辑器预览规则

- 预览字段只存在于 `UNITY_EDITOR`，不会创建运行时模块、上传任务或 Lease。
- 没有真实图标引用时，可以使用编辑器预览 Texture/Sprite 检查布局；这不是运行时资源绑定。
- Tight 或旋转 Sprite 的预览必须使用 Sprite 的顶点、三角形和 UV；不能把它粗暴当作矩形 UV。
- 监视器只读，不因查询而创建动态图集模块；无损预览显示真实 Page 的 RenderTexture 与 UV 区域。

## 五、Canvas、材质和合批边界

`CanvasGroup` 的透明度、交互和射线状态会影响 Graphic 的最终表现；它不会改变动态图集 Lease 的所有权。`Mask`、`RectMask2D`、Stencil、Canvas、材质、Alpha 模式和绘制顺序都可能阻断合批。

因此“同一动态图集”不等于“已经合批”。必须用 Frame Debugger 确认，监视器只提供可能的阻断原因。

Custom 材质由调用者拥有时，Graphic 在 Bind、Clear、失效、Disable/Enable 后都不得覆盖它；Auto/Straight/Premultiplied 模式才由 Graphic 选择 ES 图集材质。

## 六、UI 不能越权的边界

- Presenter 从角色、战斗、剧情或资源系统读取事实；View 只接收可表现数据。
- 点击、导航、确认、取消等只写入 `ESInputModule` 的输入意图，不直接调用角色内部状态字段。
- 远端资源必须绑定 `ESAssetScope`、Domain 或等价 Lease；禁止把原始 Texture 永久挂在 UI 上充当生命周期管理。
- 禁止建立万能 `UIManager` 作为所有业务的第二权威。需要编排时，使用明确职责的 Presenter、View、Domain Owner 和现有模块。

## 七、专项风险体检计划

体检器必须由用户从菜单显式启动，首阶段只扫描当前选中的 UI Root；禁止窗口打开、`OnGUI`、ReloadDomain 或程序集加载时全盘扫描。

ES 入口规划与当前实现：

- `【ES】/内容制作/UI/UI 工作台`：创建、绑定和检查 UI View，不承载运行时业务权威。
- `【ES】/开发与维护/审计/UI 风险体检`：已实现第一阶段版本，只扫描当前选中的 UI Root，输出可定位的风险项。
- `【ES】/运行时诊断/UI/UI 性能监视器`：只读显示 Canvas rebuild、材质/Page、Stencil 和列表可见窗口等运行数据。

除 UI 风险体检外，其余入口仍是专项工具的规划名；在对应代码和 Unity 验收完成前，不把菜单规划写成已存在功能。

### P0：先阻断正确性和生命周期问题

- Graphic 的 `OnValidate` 必须调用基类生命周期。
- 非 PlayMode 禁止公开资源请求触发 Runtime/Provider/上传。
- Lease、Fence、Page、Domain、Provider Generation 的失效路径必须可诊断。
- Custom 材质所有权、Mask/Stencil 兼容和占位回退必须有测试。

### P1：再处理可见性能风险

- Canvas rebuild、嵌套 Canvas、LayoutGroup + ContentSizeFitter。
- 非交互 Graphic 的 `raycastTarget`。
- Mask/Stencil 深度；矩形裁剪优先评估 `RectMask2D`。
- 材质/Page/Stencil 导致的合批阻断。
- Shadow/Outline 额外顶点。
- 大列表虚拟化、可见窗口和异步预取。
- TMP 字体图集、Tween/update 订阅以及 Disable/Destroy 清理。
- Editor Layout/Repaint 快照一致性和监视器自身 GC。

### P2：最后做平台与发布证据

- Frame Debugger 的真实 Draw Call/合批报告。
- Profiler 的 GC、Canvas rebuild、上传 p95/p99 和峰值显存。
- 500/2,000 张不同尺寸图片压力测试。
- Android Vulkan/GLES 回退、Player 与 IL2CPP 构建。

每个专项报告必须同时给出：扫描范围、发现项、修复建议、源码入口、运行证据和未验收边界。没有 Unity/Player 证据时，只能写“源码已实现”或“程序集已编译”，不能写“已稳定”。

## 八、验收顺序

```text
源码与命名检查
→ UTF-8 / diff 完整性
→ 生成程序集编译
→ Unity Editor Compile / ReloadDomain
→ EditMode / PlayMode
→ Frame Debugger / Profiler
→ Player / IL2CPP / 目标平台
```

当前动态图集成熟度：`Implemented / Verifying`。已取得 BatchMode 编译和 EditMode 10/10 证据；在真实 PlayMode、Frame Debugger、Profiler、Player/IL2CPP 和目标平台证据补齐前，不升级为 Stable。

## 九、ES UI 权威实施计划

这套计划把“搭得出来”“运行时不泄漏”“性能有证据”拆开，避免 UI 制作者需要理解所有底层类型才能开始工作。

| 阶段 | 交付物 | 谁负责 | 通过条件 |
| --- | --- | --- | --- |
| 0. 定义权威 | UI Root、Domain 命名、Presenter/View 边界和资源 Key 规则 | ES 架构维护者 | 没有第二份业务状态；Domain/Content Key 可追踪；配置不放运行时事实 |
| 1. 编辑器搭建 | UI 工作台入口、Graphic/Domain Owner 快速添加、Texture/Sprite 无损预览 | ES 编辑器工具 | 不进入 PlayMode 也能看到布局；预览不创建 Runtime/Lease；Undo、Prefab 覆盖和 ReloadDomain 不丢引用 |
| 2. 运行时接入 | `ESAssetRefer` → 临时源 Lease → GPU Copy/Fence → Atlas Lease → Graphic | 资源与运行时维护者 | 同 Key 并发合并；取消只影响自己的等待；Provider 切换、Domain 关闭、Page Lost 可回退占位并可诊断 |
| 3. 风险体检 | 当前 UI Root 扫描、定位对象、分级建议和修复记录 | UI 性能维护者 | 只在用户点击时扫描；覆盖 Canvas、Raycast、布局、Mask、顶点、列表等风险；不宣称实际合批 |
| 4. 运行证据 | PlayMode 场景、Frame Debugger、Profiler、Player/平台报告 | 验收维护者 | 真实观察 Draw Call、Canvas Rebuild、GC、上传 p95/p99、显存和回收；缺证据就保持 Verifying |

### 小白的五步路径

1. 在 UI 根节点挂 `ESDynamicAtlasDomainOwner`，保留默认 Domain。
2. 在显示节点挂 `ESDynamicAtlasGraphic`，先拖入 ES 纹理引用；没有运行时引用时只填“仅编辑器预览纹理/Sprite”。
3. 头像或远端图片填写稳定 Content Key 和版本号；换图只更新版本号，不复用旧 Key。
4. 进入 PlayMode 后观察 Graphic 状态；“等待资源系统就绪”是正常等待，不要在编辑器里点击运行时加载按钮。
5. 发布前选中 UI Root，运行 `【ES】/开发与维护/审计/UI 风险体检`，先处理“严重”和“警告”，再做 Frame Debugger/Profiler 验收。

### 权威边界速查

- `Profile/Definition/Config` 只描述可复用配置，不保存某个界面的运行时事实。
- `Presenter` 读取 ES 资源、角色、战斗或剧情事实；`View/Graphic` 只表现和持有自己的 Lease。
- `DomainOwner` 管页面级寿命；单个 Graphic 不得销毁 Page 或模块。
- `Image`、`ESDynamicAtlasGraphic`、`RawImage` 是渲染选择，不是三套资源权威。
- 风险体检是只读诊断；修复必须回到组件/Prefab/资源系统的权威入口，不能让体检器偷偷改场景。
