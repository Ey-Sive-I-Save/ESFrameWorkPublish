# ES 工作台贡献注册与模块模板指引

状态：现行实现指引（源码级，已标注 Unity 实机验收边界）

最后验证：2026-08-18（V6 文档/作者模式合同已完成两项静态构建；本轮 Unity Domain Reload、EditMode 与视觉矩阵证据待重新生成，旧版截图和计数不得作为 V6 新鲜证据）
适用源码入口：`Assets/Scripts/ESLogic/Editor/Workbench`、`Assets/Scripts/ESLogic/Editor/World`

本文说明 ES 专业工作台如何接入模块、顶层文档、作者模式、展示外壳、底部通道、资源注册槽位和其他编辑器能力。

适用范围：

- 世界工作台；
- 底座综合集成测试工作台；
- 后续角色、特效、Shader、动画等专业工作台。

## 核心模型

工作台扩展分成两层：

```text
贡献注册目录：工作台可以提供哪些文档、作者模式、工具、槽位和验证器
        ↓
模块模板：当前工作台期望启用哪些模块，以及期望顺序
        ↓
模块强类型合同：每个工作台定义自己的 `TModule : struct, Enum`
        ↓
窗口会话注入：先按最终模块列表过滤，再按模块顺序、模块内优先级和依赖拓扑创建能力
```

贡献描述只保存轻量元数据和工厂委托，不把委托、窗口引用或临时 Unity 对象写入资产。

## 相关源码

- 贡献注册表：[ESWorkbenchContributionRegistry.cs](../../../Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchContributionRegistry.cs)
- 工作台基础类：[ESWorkbenchWindowBase.cs](../../../Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs)
- World 接入：[ESWorldBuilderWorkbenchWindow.cs](../../../Assets/Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs)
- 综合测试接入：[ESWorkbenchIntegrationTestWindow.cs](../../../Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchIntegrationTestWindow.cs)

## 模块枚举

底座不提供跨领域的统一模块枚举。每个工作台必须定义自己的模块合同；例如 World 使用 `ESWorldWorkbenchModule`：

```csharp
public enum ESWorldWorkbenchModule : byte
{
    Foundation,
    Overview,
    Terrain,
    Material,
    Vegetation,
    Prefab,
    Navigation,
    WaterWeather,
    Streaming,
    Collision,
    UGC
}
```

综合 Test 独立使用 `ESWorkbenchIntegrationTestModule`，不得借用 World 枚举。模块枚举表示工作台功能边界；贡献分类 `ESWorkbenchContributionCategory` 表示能力归属。两者不要混用：一个模块可以由多个贡献共同实现，一个贡献也可以只提供模块中的一个入口。

World 的 `Foundation` 承载通用视口、对象库、Inspector、工具与命令。常规 World 变体应保留它；只有明确要移除整套作者能力时才从最终模块列表删除。

## 默认模块与调整钩子

基础工作台提供默认模块模板：

```csharp
protected override List<ESWorldWorkbenchModule> ESWorkbench_DefaultModules
{
    get
    {
        return new List<ESWorldWorkbenchModule>
        {
            ESWorldWorkbenchModule.Foundation,
            ESWorldWorkbenchModule.Overview,
            ESWorldWorkbenchModule.Terrain,
            ESWorldWorkbenchModule.Material,
            ESWorldWorkbenchModule.Vegetation,
            ESWorldWorkbenchModule.Prefab,
            ESWorldWorkbenchModule.Navigation,
            ESWorldWorkbenchModule.WaterWeather,
            ESWorldWorkbenchModule.Streaming,
            ESWorldWorkbenchModule.Collision,
            ESWorldWorkbenchModule.UGC
        };
    }
}

protected virtual void ESWorkbench_AdjustModules(
    List<ESWorldWorkbenchModule> modules)
{
}
```

派生工作台有两种调整方式。

### 直接替换默认模板

适合完全不同的专业工作台：

```csharp
protected override List<ESWorldWorkbenchModule>
    ESWorkbench_DefaultModules
{
    get
    {
        return new List<ESWorldWorkbenchModule>
        {
            ESWorldWorkbenchModule.Overview,
            ESWorldWorkbenchModule.Prefab,
            ESWorldWorkbenchModule.UGC
        };
    }
}
```

### 在默认模板上调整

适合 World 变体或测试变体：

```csharp
protected override void ESWorkbench_AdjustModules(
    List<ESWorldWorkbenchModule> modules)
{
    modules.Remove(ESWorldWorkbenchModule.WaterWeather);
    modules.Remove(ESWorldWorkbenchModule.Streaming);
    modules.Insert(1, ESWorldWorkbenchModule.Prefab);
    modules.Add(ESWorldWorkbenchModule.Navigation);
}
```

基础层会复制默认列表后再调用调整钩子，因此不会修改默认模板。调整完成后会自动去重并保留最终顺序。

## 注册贡献

贡献使用与工作台模块类型一致的泛型描述注册，并且必须显式携带模块身份：

```csharp
ESWorkbenchContributionRegistry<ESWorldWorkbenchModule>.RegisterOrUpdate(
    new ESWorkbenchContributionDescriptor<ESWorldWorkbenchModule>(
        workbenchId: "world",
        contributionId: "world.terrain.unity",
        displayName: "Unity Terrain 地形",
        module: ESWorldWorkbenchModule.Terrain,
        category: ESWorkbenchContributionCategory.Terrain,
        inject: context =>
        {
            ESWorldBuilderWorkbenchWindow window =
                context.Window as ESWorldBuilderWorkbenchWindow;
            if (window == null)
                throw new InvalidOperationException("缺少 World 窗口上下文。");

            context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                "terrain",
                "地形",
                "Unity Terrain / 高度场",
                toolIds: new[] { "world.select", "world.terrain" },
                contentKinds: new[]
                {
                    ESWorkbenchContentKind.Brush,
                    ESWorkbenchContentKind.Terrain
                },
                defaultToolId: "world.terrain",
                priority: 1000,
                primary: true));
            return null;
        },
        owner: "ES.World",
        priority: 100,
        revision: 1),
    out string message);
```

贡献 ID 必须在同一工作台内稳定且唯一。推荐格式：

```text
<workbench>.<module>.<capability>
```

例如：

- `world.terrain.unity`
- `world.material.layer`
- `world.prefab.scatter`
- `character.motion.preview`
- `vfx.shader.property`

## 可注入内容

`ESWorkbenchContributionContext` 当前支持：

- `RegisterPresentation`：声明中文品牌、资产字段、视口文档与检查器标题；
- `RegisterBottomPanel`：注入带稳定 ID、顺序和确定性释放合同的底部通道；
- `RegisterDocument`：注入顶层文档；World 固定为“世界创作、世界总览、生产与发布”；
- `RegisterAuthoringMode`：注入作者模式及其工具、内容类型和模式 Inspector；
- `RegisterAssetSlot`：注入材质、Prefab 等资源注册槽位；
- `RegisterEntry`：注入贡献目录项；
- `RegisterViewport`：注入二维、三维、游戏或自定义视口；
- `RegisterObject` / `RegisterObjectSource`：注入对象库条目或动态来源；
- `RegisterHierarchy` / `RegisterHierarchySource`：注入作者层级或动态来源；
- `RegisterAuthoringAdapter`：注入正式作者事务适配器；
- `RegisterInspector`：注入上下文检查器；
- `RegisterTool` / `RegisterCommand`：注入工具与命令；
- `RegisterIssueSource`：注入问题、性能与安全状态来源；
- `ReportDiagnostic`：报告可见诊断信息。

后续仍可以按同一模式增加：

- PreviewAdapter；
- AuthoringTool；
- Validator；
- BakeProvider；
- Exporter；
- SampleContentProvider。

## 去重、冲突与版本

注册表以 `WorkbenchId + ContributionId` 作为稳定身份。

- 同 owner、同 revision：按幂等注册处理，并更新最新委托；
- 同 owner、新 revision：替换旧贡献；
- 不同 owner、相同 ID：报告冲突，不静默覆盖；
- 缺少依赖：跳过注入并报告诊断；
- 依赖注入失败：下游依赖不会继续注入；
- 循环依赖：整组贡献跳过并报告诊断；
- 单个贡献异常：隔离异常，不让整个工作台初始化失败。

不要使用普通 `Dictionary.Add` 直接组装模块入口；所有模块入口必须经过贡献注册表的稳定 ID 校验。

## 生命周期

工作台采用“窗口打开时发现并注入”的单一加载入口：

1. 工作台声明默认模块列表；
2. 执行模块调整钩子；
3. 注册或更新本工作台的强类型贡献描述；
4. 在首次贡献会话创建前恢复或创建待绑定资产；
5. `Open` 对最终模块列表去重并保留首次顺序，过滤未启用模块；
6. 贡献先按模块顺序、模块内 `Priority` 降序与 `ContributionId` 升序形成稳定基线，再解析依赖拓扑；
7. 作者模式切换不销毁持久视口，只切换工具、内容过滤和 Inspector；顶层文档切换只释放离开的当前文档；切换底部通道先释放旧通道内容；贡献重载、资产重绑和窗口关闭会释放当前文档、模式、视口与底部内容，清空定义并释放会话句柄；
8. 同 revision 重注册会替换为最新委托，避免 Disable Domain Reload 或窗口重开后保留旧窗口闭包。

不要在静态初始化阶段创建正式 Scene 对象、加载大量资源或写入资产。程序集级注册可以只登记轻量描述；真正的 Unity 对象和资源解析必须发生在窗口主线程的注入阶段。

## World 与 Test 的约定

World 和底座综合 Test 都必须提供自己的 `ESWorkbench_DefaultModules`，不能依赖基础类的隐式默认值来表达业务范围。

World 当前启用自己的全部模块，并使用 `ESWorkbenchContributionRegistry<ESWorldWorkbenchModule>` 注入三个顶层文档、十个作者模式和材质/植被/Prefab 注册槽位。资产恢复在首次 `Open` 之前完成，窗口没有第二次业务加载入口。

综合 Test 使用独立的 `ESWorkbenchIntegrationTestModule`，文档与作者模式全部通过正式贡献注册表注入；其 HideAndDontSave 内存资产也在首次贡献会话创建前完成绑定。

## 布局、自适应与命令保留

专业工作台不自行复制窗口外壳。领域通过 `ESWorkbenchHostPresentationDescriptor` 提供中文品牌、面板标题和 `ESWorkbenchResponsiveLayoutPolicy`，底座统一处理最小尺寸、中央视口保护、侧栏折叠、底部抽屉和溢出菜单。

默认响应式分为三档：

- `Wide`：左侧内容、中央视口、右侧 Inspector 同时显示；
- `Compact`：中央视口保留，只显示用户选择的一侧面板；
- `Narrow`：继续保护中央视口，文档、模式、通道、状态和非关键命令进入对应溢出菜单，活动项始终直接可见。

命令必须声明 `ESWorkbenchCommandRole`；需要在窄窗口仍直接可达的保存、撤销、重做、验证等动作再声明 `ESWorkbenchCommandVisibility.Pinned`。底座始终先显示全部 Pinned 命令，再按角色、优先级和稳定 ID 安排其余命令。危险动作应使用 `Dangerous`，不得为了优先级占用关键动作位置。

底座提供“标准创作、专注视口、内容整理、生产任务、诊断”五种布局预设。点击面板开关或拖动左栏、Inspector、底部抽屉分隔条后，布局自动变为 `Custom` 并保存实际尺寸；再次选择预设会恢复领域声明的理想尺寸。World 的浮动窗口最小尺寸为 `980 x 640`，默认尺寸为 `1440 x 900`。左侧内容中心和 Inspector 的首选宽度均为 `320`，最小 `280`，最大 `420`；中央作者区域至少保留 `600 x 340`，宽屏下至少占窗口宽度的 `50%`。默认尺寸直接进入完整三栏创作布局，`980～1179` 按 Compact 合同只显示一个侧栏。

底部只保留“问题、生产任务、状态与历史、诊断与验收”四组。它位于中央与 Inspector 的右工作区下方，不压缩左侧内容中心。收起时保留 `32px` 页签条并隐藏内容；提示态、标准展开和最大高度分别以 `112 / 220 / 320px` 为基线，同时受工作区高度 `34%` 和中央视口最小高度保护。通道仍通过 `ESWorkbenchBottomPanelDensity` 声明内容密度，用户手动调整的高度只在展开状态生效。

这些规则只证明源码合同和自动化结构可检查。宽屏、窄屏、高 DPI、深浅主题、长中文和真实拖拽仍必须在 Unity Editor 中逐项验收后，才能声明视觉达到商业交付标准。

## 内容中心、稳定选择与连续作者操作

左侧“内容库”是资源发现与作者动作入口，“当前结构”只展示已进入当前 Draft 的区域、POI、Prefab 放置等实例，两者不能混成一棵列表。内容描述必须使用 `ESWorkbenchContentKind` 标明预制件、笔刷、场景、区域、地形、植被或玩法类型，并使用 `ESWorkbenchContentDragMode` 声明放置、激活工具、应用模板、创建区域或仅查看语义。

内容库当前提供统一的深色预览井、真实 `AssetPreview` 异步缓存和 Editor-only 生成缩略图。没有资产预览的笔刷、区域与场景模板会使用 `ContentKind + BaseObjectId + PresetId` 生成同一背景语言下可辨识的专属缩略图，而不是让同类内容显示完全相同的占位图；生成图为 `192 × 128`、`HideAndDontSave`，上传 GPU 后释放 CPU 像素副本，并使用最多 256 项的 LRU 缓存限制长期浏览大型资源库的显存增长。缓存淘汰和窗口释放都会确定性销毁生成纹理，不得写入资源目录或发布清单。列表与大缩略图网格共享同一预览解析、稳定选择、收藏/常用/最近/推荐状态投影、Hover/选中层级和拖放合同。

内容发现默认按类型组织，稳定顺序为“类型 → 业务分类 → 贡献优先级 → 名称”。搜索栏下方必须常驻一级类型快捷栏，直接显示“全部”和当前实际存在的预制件、笔刷、场景、区域等类型及数量；宽度不足时只把额外类型收进“更多”，当前活动类型即使原本位于“更多”也必须提升到快捷栏。类型入口不得只藏在“筛选”菜单、业务分类树或排序菜单中。内容中心同时支持树形业务分类、面包屑、搜索、全部/收藏/最近/推荐范围，以及按类型、推荐、优先级、名称、最近使用和使用频率排序。收藏与使用记录只在 Workbench 专用 `EditorPrefs` 中保存 `WorkbenchId + BaseObjectId + favorite + lastUsedUtcTicks + useCount`，不保存 Unity 对象、描述器、Payload、预设变体 ID 或 InstanceId。窄栏低于内容浏览阈值时可以折叠业务分类轨，结果区继续保留完整宽度；组合“筛选”菜单作为业务分类和次级筛选的响应式补充，但不能替代一级类型快捷栏。

内容可以在列表和大图网格之间切换。大图模式下内容浏览宽度低于 `520` 时会主动收起业务分类轨，在常用 360px 左栏内保护真实双列缩略图；列表模式只在低于 `330` 时收起业务分类轨。一级类型快捷栏在两种视图中都保持可见，业务分类能力继续由同一“筛选”菜单补充，不会因响应式降级而删除。窗口可用高度低于 `760` 时进入纵向紧凑模式，高于 `790` 时退出，避免拖动窗口经过临界值时反复重建：紧凑模式移除与“内容库”页签重复的左栏总标题，将内容标题与摘要压成单行，隐藏说明文案，把全部/收藏/最近/推荐范围并入组合筛选菜单，并缩短大图卡片但保留缩略图、状态 Chip、标题、分类和参数预设。结果区进一步变窄时，两个固定切换按钮收口为“列表/大图”菜单，批量立即放置按钮也会进入“批选”菜单。批选菜单支持选择当前筛选结果、清空选择、设置 1/2/4/8 的放置间距和立即放置；批量放置仍必须经过完整预检，并在单一 Undo Group 内全部成功或整体回滚。笔刷和区域等内容变体通过 `BaseObjectId + PresetId` 保存选择，Inspector、拖放和批量放置使用当前有效描述器，稳定选择仍保持基础内容身份。

点击内容卡片时，统一稳定选择服务保存 `ObjectId + SelectionKind`，Inspector 读取内容描述器并显示类型、来源、稳定 ID、默认动作和不可用原因；不持久化描述器、资产对象或其他实时 Payload。ReloadDomain 后通过稳定 ID 从当前内容源重新解析，解析失败时清空旧引用并给出恢复提示。

“当前选择”和“活动内容”是两个状态：创建区域或 Prefab 后，当前选择会切换到新实例；活动笔刷、区域模板或 Prefab 仍保留在当前窗口实例中，因此可以连续绘制或放置。活动内容只保存稳定 ID，每个窗口独立持有，不能通过静态缓存在多窗口之间串线。

拖放必须先执行只读可接受性预检，再在释放时执行正式作者事务。支持诊断的视口实现 `IESWorkbenchViewportDropDiagnostics`，把只读游戏构图视图、根节点锁定、草稿缺失、注册 Key 失效或目标类型不匹配等原因直接投影到拖放反馈和状态栏；预检本身不得修改 Draft。World 的区域模板在靠近边缘释放时优先平移区域以保持模板尺寸，只有模板本身大于整个世界边界时才按世界尺寸收缩。

内容卡片在指针按下后即可直接启动拖拽，不以“已经进入稳定选择服务”为前置条件；未选中的列表项和大图卡片必须与已选中项具有相同拖入能力，单击选择只在未越过统一防手抖阈值且未进入 Started 阶段时提交。列表、卡片、2D/3D 变换和分隔条复用 `ESWorkbenchPointerDragState` 的 `Idle → Armed → Started` 合同，PointerCaptureOut、Esc、视口停用和窗口释放均幂等归零。活动视口可以实现 `IESWorkbenchDropPreviewViewport` 接收只读悬停请求：2D 画布绘制类型匹配的目标框、十字与批量阵列，3D 作者视口优先创建真实 Prefab PreviewScene 实例并为模板绘制尺寸框。游戏构图预览始终拒绝此请求。

同一工作台的内容源还必须共享 `ESWorkbenchPointerOwnershipGate`：任一列表行或卡片进入 `Armed` 后，其他内容源不得再取得第二个主指针；匹配的 `PointerUp`、`PointerCaptureOut`、重建、停用和窗口释放都必须释放同一所有权。该闸门只治理宿主级输入所有权，不持有描述器、Unity 资源或作者事务，后续 Scene、Prefab 和 Graph 工作台可以直接复用。

悬停预览与正式批量提交必须共同使用 `ESWorkbenchDropLayout` 计算居中阵列、间距下限和吸附结果，禁止领域视口复制另一套落点算法。内部拖动载荷带工作台会话私有令牌，宿主只能清理自己拥有的 `DragAndDrop` 数据；Project 面板同时拖入多个已注册资源时按资源顺序去重并进入正式批量预检。预览不得修改 Draft、注册 Undo 或保留 Unity 对象引用；拖离、取消、释放、切换文档/视口、贡献重载、资产重绑和窗口关闭都必须调用 `ClearDropPreview`。正式提交完成或拒绝后才统一清理反馈与载荷，不能在 `TryAccept` 前销毁落点预览。3D 真实预览实例由 `ESEditorPreviewModelHandle` 拥有并在清理时确定性释放。

World 的 3D 鼠标落点优先使用 PreviewScene 内 TerrainCollider 射线命中，只有无可用地形碰撞时才退化到世界基准平面，再按 Heightfield 重采样高度。地形工具要求真实命中地图范围，不能把地图外指针夹到边界后继续误画；普通对象放置仍可按领域策略夹取。地形笔刷支持目标高度、世界米制半径、强度和边缘衰减；2D 与 3D 都显示同一米制范围和中心标记，按住左键拖动形成连续笔划，一次笔划只登记一个 Undo，独立点击和从内容库拖入则各自形成独立 Undo。笔刷算法只修改半径内 Heightfield 采样，3D Terrain 以最高 20Hz 的受节流局部高度块即时跟随，松开时同步最后一笔；该 Terrain 始终只是作者投影，不能反向成为第二份权威数据。

拖放 Ghost 会显示 52px 当前内容缩略图、接受/拒绝符号、内容类型、业务分类、批量数量、间距、单 Undo 语义和拒绝原因；拒绝状态明确说明预检不会修改作者数据。内容卡片通过左侧稳定强调条、预览井明暗、颜色、边框、透明度和短时无布局位移脉冲区分默认、Hover 与稳定选中状态；预览左下角就近显示收藏、常用次数、最近或推荐原因。禁止通过改变卡片外部尺寸、字体大小或动态标签长度制造选中动画，以免虚拟化列表和视口宽度抖动。

一次显式内容源刷新只读取一次业务内容源，并形成当前刷新周期内的稳定快照。搜索、类型切换、业务分类、范围筛选、排序、预设、批选和响应式切换都只重算该快照的投影，不得再次调用业务内容源；只有资产变化、数据变化、Undo/Redo、显式刷新或作者事务确实可能改变内容目录时才重取来源。重复 `BaseObjectId` 在快照入口按首次出现稳定去重，并在内容摘要中显示去重数量，避免同一稳定选择映射到多份描述器。

## 资产与委托边界

可以序列化：

- 模块枚举；
- 贡献 ID；
- StringKey；
- GUID、版本和资源路径；
- 文档、作者模式或槽位的业务配置。

不能序列化：

- `Action` / `Func` 委托；
- 当前 `EditorWindow` 引用；
- PreviewScene 对象；
- 临时 GameObject；
- 运行时缓存实例。

资产只保存稳定身份，窗口打开时重新解析贡献和资源。

## 验证清单

新增工作台或贡献后的验证清单如下：

- 相同贡献重复注册不会抛出重复 Key 异常；
- 不同 owner 使用相同 ID 会显示冲突；
- 删除模块后文档、作者模式和槽位都不会注入；
- 新增模块后可以按调整列表顺序显示；
- 低优先级贡献依赖高优先级贡献时可以正确注入；
- 依赖失败时下游贡献被跳过；
- 重新绑定资产后槽位指向新资产；
- 关闭并重新打开窗口不会保留旧窗口委托；
- 预览对象仍然只属于 PreviewScene，不被误认为正式资产；
- 正式保存、收集和 Bake 仍由原有明确提交动作完成。

## 当前边界

这套底座已经收口模块强类型合同、模块候选 List、调整钩子、贡献稳定身份、去重、依赖、文档/作者模式生命周期和窗口注入主流程。新增工作台必须提供自己的模块枚举，并沿用同一泛型类型贯穿底座、描述、会话和注册表。禁止重新引入业务 Page API 或 Page→Mode 适配层。

例如，Terrain 笔刷、Prefab 排版和 NavMesh 烘焙仍必须由对应贡献的作者事务、构建提供器和验证测试负责。World 已提供显式确认的正式输出事务，但不能绕过预检、备份、重读验证和失败回滚，也不能把本地输出等同于资源发布成功。

## 世界对话编辑器

世界工作台现在提供独立的对话作者工具，入口为：

```text
【ES】/内容制作/世界/对话编辑器
```

也可以在世界构建工作台右侧检查器点击“打开对话编辑器”。

相关源码：

- 对话图与放置数据：`Assets/Scripts/ESLogic/Runtime/World/Dialogue/ESWorldDialogueData.cs`；
- 对话图保存入口：`Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueAuthoringUtility.cs`；
- 对话编辑器：`Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueWorkbenchWindow.cs` (`ESWorldDialogueEditorWindow`)；
- 对话图检查器：`Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueGraphAssetEditor.cs`；
- Scene 锚点检查器：`Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueAnchorEditor.cs`。

### 数据权威

对话内容分成三个明确层级：

```text
ESWorldDialogueGraphAsset
  节点、输出端口、边、入口节点、内容版本和内容 Hash
        ↓ 稳定 graphId / nodeId / portId / edgeId
ESWorldMapAsset.dialoguePlacements
  对话图 GUID、运行时 graphKey、2D/3D 坐标和 Scene 对象稳定 Key
        ↓ placementId
ESWorldDialogueAnchor
  正式 Scene 中的 2D/3D 空间投影
```

- 对话图资产是节点和数据流唯一权威；
- 地图资产是空间放置记录唯一权威；
- Scene 锚点不是第二份对话内容，只保存 `placementId`、Graph Key、资产 GUID 和空间投影；
- Editor 窗口、选择状态和 Graph 节点矩形都不能替代资产数据。

### Graph 数据流

1. 创建或绑定 `ESWorldDialogueGraphAsset`；
2. 点击“新增节点”；
3. 在节点检查器编辑中文标题、说话者、文本和输出端口；
4. 选择目标节点后点击“连接到目标节点”；
5. 点击“设为入口节点”指定图入口；
6. 点击“验证”检查节点、端口、边和入口引用；
7. 点击“保存”更新内容版本与 SHA-256 内容签名。

重复、缺失或悬空的稳定 ID 不允许静默覆盖。缺失 ID 可以在显式加载时修复；重复 ID 必须通过验证暴露，避免自动换 ID 后破坏既有边引用。

### 2D 地图放置

切换到“2D 地图”页后：

- 从 Project 直接拖入 `ESWorldDialogueGraphAsset`，在鼠标位置创建地图入口；
- 也可以先绑定对话图，再点击地图空白位置创建；
- 拖动已选入口修改地图坐标；
- 下方检查器可编辑显示名、Graph Key、入口节点、位置、旋转和缩放；
- “加载此入口的对话图”通过资产 GUID 恢复 Graph 资产，不依赖文件名或窗口缓存。

2D 地图入口保存在 `ESWorldMapAsset.dialoguePlacements`，不会自动创建正式 Scene 对象。

### Scene 2D/3D 拖放

切换到“Scene 2D/3D”页，将 `ESWorldDialogueGraphAsset` 直接拖入 SceneView：

- SceneView 为 2D 模式时，入口落在 XY 平面并记录为 `Scene2D`；
- 普通 SceneView 优先射线命中 Collider，未命中时落在 Y=0 平面并记录为 `Scene3D`；
- 创建真实 `ESWorldDialogueAnchor` GameObject；
- 同时向地图资产写入相同 `placementId` 的放置记录；
- 全程使用 Unity Undo，并标记当前 Scene 与地图资产 Dirty；
- 只有点击“保存地图与场景”才执行正式落盘。

“同步当前场景锚点”是显式扫描入口，只扫描当前已加载 Scene 的 `ESWorldDialogueAnchor`，不会在编辑器启动或窗口打开时全项目扫盘。

### 保存与加载

- 对话图保存由 `ESWorldDialogueAuthoringUtility.Save` 统一提交；
- 地图保存继续由 `ESWorldMapAuthoringUtility.Save` 统一提交；
- 两者都在保存时计算内容 Hash，并只在内容变化时推进内容版本；
- Graph 与 Map 资产通过 Asset GUID 在 Domain Reload 后恢复；
- Scene 锚点通过 `placementId + sceneObjectKey` 与地图放置记录关联；
- 窗口不持久化 `EditorWindow`、`SerializedObject`、Scene 对象或 InstanceId。

### 当前验证边界

已完成源码级独立编译校验，覆盖运行时数据模型、Unity 2022.3 Editor API、Graph 窗口、2D/3D 放置和检查器入口。

仍须在 Unity Editor 中实测：

- Project → Graph、2D 地图和 SceneView 的真实拖放；
- 2D/3D SceneView 射线位置；
- Undo/Redo、场景关闭重开和 Domain Reload；
- 多 Inspector 同时编辑；
- 保存后重新加载 Graph、Map 和 Scene 的三方关联；
- 窄窗口、中文长文本和高 DPI 布局。

## 世界商业工作台合同

World 工作台通过正式展示贡献把品牌声明为“ES 世界工作台”，宿主不写死业务品牌。二维地图、三维世界和游戏构图预览都由 World 模块贡献；游戏构图预览是只读透视构图，不允许拖放或修改作者数据。World 将世界状态、预览资源和商业验收合并进统一“诊断与验收”通道；底座统一保留问题、生产任务、状态与历史、诊断与验收四组。

活动信息分为三类项目级记录，保存在 `Library/ESWorkbench/activity-v1.json`：

- 操作历史：用户命令和状态变化；
- 日志：独立日志通道；
- 任务中心：以稳定任务 ID 更新预检、排队、运行、成功、失败和产物路径。

记录只包含字符串、状态和 UTC 时间，不保存 `UnityEngine.Object`、`EditorWindow`、委托或实例 ID；按需读取并限制总量，不使用 `InitializeOnLoad`。

正式 World 输出必须由用户点击“生成正式 Terrain / Scene / NavMesh”并再次确认。事务顺序固定为：

1. 验证地图定义、Assets 路径、目标扩展名和已保存地图资产；
2. 任一已加载 Scene 为 Dirty，或目标正式 Scene 仍处于加载状态时拒绝启动；
3. 在 `Library/ESWorkbench/Backups/<transactionId>` 备份地图资产和所有既有目标；
4. 生成或更新 TerrainData，创建包含 TerrainCollider 与 Prefab 放置的 staging Scene；
5. 使用 Unity `NavMeshBuilder` 从正式内容根收集来源并生成 NavMeshData；
6. 提交 Scene 后逐项重读 TerrainData、Scene 和 NavMeshData；
7. 任一步失败时恢复提交前文件并刷新 AssetDatabase。

本地正式输出不等于资源发布成功。资源收集和发布构建继续使用现有 `ESContentRegistration` 预检、提交和状态查询，不创建第二套 Manifest、Provider 或上传流程。

## 可复用 2D / 3D 视口底座

工作台二维与三维作者视图的导航和精确编辑不属于 World 私有能力。通用底座位于：

```text
Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchViewportFoundation.cs
```

职责边界如下：

- `ESWorkbenchCanvasNavigationState` 统一二维 XZ 投影、坐标往返、平移、鼠标锚点缩放、缩放夹限和稳定状态保存；
- `ESWorkbenchOrbitCameraState` 统一 PreviewScene 轨道相机的焦点、距离、旋转、平移和边界夹限，不持有 Camera 或临时场景对象；
- 连续输入由视口持有指针捕获和开始/结束回调，领域窗口只决定 Undo 边界和数据 mutation；同一笔划状态不得由 2D、3D 各自复制；
- `ESWorkbenchPrecisionTransformElement` 提供中文绝对值/增量编辑、位置/旋转/缩放吸附、输入校验和明确提交按钮；
- 所有数值变换只能通过 `ESWorkbenchAuthoringService` 提交，因此继续继承对象锁定、Undo、DirtyKey、失败回滚、选择刷新和领域适配器能力判断；
- World 只保留高度场采样、区域/POI/Prefab 投影、领域命中和正式草稿变更，不再拥有第二套二维导航或三维轨道相机算法；
- 世界区域的“尺寸”使用区域边界事务，不伪装为 Prefab Transform；靠近世界边界时保持请求尺寸并将中心约束到有效范围。

领域 Inspector 不得再用直接绑定的 `SerializedProperty` 空间字段绕过作者服务。非空间元数据仍可使用绑定字段；位置、旋转、缩放和区域尺寸必须使用精确变换面板或等价的正式作者事务。

新增领域视口时，应优先复用上述底座并提供自己的只读空间投影和作者适配器。只有渲染、命中或地表采样确实属于领域语义时才允许保留在领域模块内。
