# ES 工作台贡献注册与模块模板指引

状态：现行实现指引（源码级，已标注基础合同缺口，Unity 实机验收待完成）  
最后验证：2026-08-16（源码复核、`ES_Logic.Editor` 与 `ES_Logic.Editor.World.Tests` 静态构建；不替代 Unity 验收）  
适用源码入口：`Assets/Scripts/ESLogic/Editor/Workbench`、`Assets/Scripts/ESLogic/Editor/World`

本文说明 ES 专业工作台如何接入模块、页面、资源注册槽位和其他编辑器能力。

适用范围：

- 世界工作台；
- 底座综合集成测试工作台；
- 后续角色、特效、Shader、动画等专业工作台。

## 核心模型

工作台扩展分成两层：

```text
贡献注册目录：工作台可以提供哪些页面、工具、槽位和验证器
        ↓
模块模板：当前工作台期望启用哪些模块，以及期望顺序
        ↓
模块绑定策略：贡献显式声明或由 IsEnabled 判断是否启用
        ↓
窗口会话注入：按贡献依赖与优先级创建真实页面、槽位和工具
```

贡献描述只保存轻量元数据和工厂委托，不把委托、窗口引用或临时 Unity 对象写入资产。

当前源码必须注意：`ESWorkbenchContributionDescriptor` 尚无模块字段，注册表也不接收最终模块列表。World 页面贡献通过自己的 `IsEnabled` 委托查询模块开关；这不是基础注册表已经自动完成模块过滤和模块顺序注入。后续专业工作台不能只声明模块 List 就假设贡献会自动裁剪。

## 相关源码

- 贡献注册表：[ESWorkbenchContributionRegistry.cs](../../../Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchContributionRegistry.cs)
- 工作台基础类：[ESWorkbenchWindowBase.cs](../../../Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs)
- World 接入：[ESWorldBuilderWorkbenchWindow.cs](../../../Assets/Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs)
- 综合测试接入：[ESWorkbenchIntegrationTestWindow.cs](../../../Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchIntegrationTestWindow.cs)

## 模块枚举

标准模块类型是 `ESWorkbenchModuleKind`：

```csharp
public enum ESWorkbenchModuleKind : byte
{
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

模块枚举表示工作台功能边界；贡献分类 `ESWorkbenchContributionCategory` 表示能力归属。两者不要混用：一个模块可以由多个贡献共同实现，一个贡献也可以只提供模块中的一个入口。

## 默认模块与调整钩子

基础工作台提供默认模块模板：

```csharp
protected virtual List<ESWorkbenchModuleKind> ESWorkbench_DefaultModules
{
    get
    {
        return new List<ESWorkbenchModuleKind>
        {
            ESWorkbenchModuleKind.Overview,
            ESWorkbenchModuleKind.Terrain,
            ESWorkbenchModuleKind.Material,
            ESWorkbenchModuleKind.Vegetation,
            ESWorkbenchModuleKind.Prefab,
            ESWorkbenchModuleKind.Navigation,
            ESWorkbenchModuleKind.WaterWeather,
            ESWorkbenchModuleKind.Streaming,
            ESWorkbenchModuleKind.Collision,
            ESWorkbenchModuleKind.UGC
        };
    }
}

protected virtual void ESWorkbench_AdjustModules(
    List<ESWorkbenchModuleKind> modules)
{
}
```

派生工作台有两种调整方式。

### 直接替换默认模板

适合完全不同的专业工作台：

```csharp
protected override List<ESWorkbenchModuleKind>
    ESWorkbench_DefaultModules
{
    get
    {
        return new List<ESWorkbenchModuleKind>
        {
            ESWorkbenchModuleKind.Overview,
            ESWorkbenchModuleKind.Prefab,
            ESWorkbenchModuleKind.UGC
        };
    }
}
```

### 在默认模板上调整

适合 World 变体或测试变体：

```csharp
protected override void ESWorkbench_AdjustModules(
    List<ESWorkbenchModuleKind> modules)
{
    modules.Remove(ESWorkbenchModuleKind.WaterWeather);
    modules.Remove(ESWorkbenchModuleKind.Streaming);
    modules.Insert(1, ESWorkbenchModuleKind.Prefab);
    modules.Add(ESWorkbenchModuleKind.Navigation);
}
```

基础层会复制默认列表后再调用调整钩子，因此不会修改默认模板。调整完成后会自动去重并保留最终顺序。

## 注册贡献

贡献使用 `ESWorkbenchContributionDescriptor` 注册：

```csharp
ESWorkbenchContributionRegistry.RegisterOrUpdate(
    new ESWorkbenchContributionDescriptor(
        workbenchId: "world",
        contributionId: "world.terrain.unity",
        displayName: "Unity Terrain 地形",
        category: ESWorkbenchContributionCategory.Terrain,
        inject: context =>
        {
            ESWorldBuilderWorkbenchWindow window =
                context.Window as ESWorldBuilderWorkbenchWindow;
            if (window == null)
                throw new InvalidOperationException("缺少 World 窗口上下文。");

            context.RegisterPage(new ESWorkbenchPageDefinition(
                "terrain",
                "地形",
                "Unity Terrain / Heightfield",
                ESWorkbenchDirtyFlags.Authoring,
                () => window.DrawTerrain()));
            return null;
        },
        owner: "ES.World",
        priority: 100,
        revision: 1,
        isEnabled: context =>
        {
            ESWorldBuilderWorkbenchWindow window =
                context.Window as ESWorldBuilderWorkbenchWindow;
            return window != null
                && window.ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Terrain);
        }),
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

- `RegisterPage`：注入页面；
- `RegisterAssetSlot`：注入材质、Prefab 等资源注册槽位；
- `RegisterEntry`：注入贡献目录项；
- `ReportDiagnostic`：报告可见诊断信息。

后续可以按同一模式增加：

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

当前第一版采用“窗口打开时发现并注入”：

1. 工作台声明默认模块列表；
2. 执行模块调整钩子；
3. 注册或更新本工作台的贡献描述；
4. 贡献自己的 `IsEnabled` 可以读取最终模块列表并决定是否注入；当前通用注册表不会自动按模块过滤；
5. 注册表按 Priority、ContributionId 和依赖关系注入，不按模块 List 顺序排列；
6. 窗口关闭或重新装配时释放本次会话句柄并清理注入集合；当前基础实现尚未清理页面列表；
7. 重新绑定地图资产时重新生成与资产相关的槽位，并防止旧窗口闭包残留。

不要在静态初始化阶段创建正式 Scene 对象、加载大量资源或写入资产。程序集级注册可以只登记轻量描述；真正的 Unity 对象和资源解析必须发生在窗口主线程的注入阶段。

## World 与 Test 的约定

World 和底座综合 Test 都必须提供自己的 `ESWorkbench_DefaultModules`，不能依赖基础类的隐式默认值来表达业务范围。

World 当前启用全部标准模块，并使用贡献注入页面和材质/植被/Prefab 注册槽位。其页面贡献逐项设置 `IsEnabled` 完成模块过滤，但页面排序仍由贡献 Priority/ID 决定，不由模块 List 顺序决定。

Test 当前启用全部标准模块，模块下拉由枚举列表驱动，但页面仍由窗口直接调用 `ESWorkbench_RegisterPage` 注册，没有经过贡献注册主流程。当前测试窗口是 `sealed`，不存在通过派生测试窗口覆写 `ESWorkbench_AdjustModules` 的现成验证路径。

## 资产与委托边界

可以序列化：

- 模块枚举；
- 贡献 ID；
- StringKey；
- GUID、版本和资源路径；
- 页面或槽位的业务配置。

不能序列化：

- `Action` / `Func` 委托；
- 当前 `EditorWindow` 引用；
- PreviewScene 对象；
- 临时 GameObject；
- 运行时缓存实例。

资产只保存稳定身份，窗口打开时重新解析贡献和资源。

## 验证清单

新增工作台或贡献后的目标验证清单如下；其中模块顺序、通用裁剪和页面释放当前仍是待补证能力，不能把清单误读为已经通过：

- 相同贡献重复注册不会抛出重复 Key 异常；
- 不同 owner 使用相同 ID 会显示冲突；
- 删除模块后页面和槽位都不会注入；
- 新增模块后可以按调整列表顺序显示；
- 低优先级贡献依赖高优先级贡献时可以正确注入；
- 依赖失败时下游贡献被跳过；
- 重新绑定资产后槽位指向新资产；
- 关闭并重新打开窗口不会保留旧窗口委托；
- 预览对象仍然只属于 PreviewScene，不被误认为正式资产；
- 正式保存、收集和 Bake 仍由原有明确提交动作完成。

## 当前边界

这套底座已经形成模块候选 List、调整钩子、贡献稳定身份、去重、依赖和窗口注入骨架，但尚未在基础注册表中统一解决模块绑定、按模块顺序显示、页面释放和综合测试走贡献主流程的问题。

例如，Terrain 笔刷是否真实写入 `TerrainData`、Prefab 排版是否正式落盘、NavMesh 是否实际烘焙，仍必须由对应贡献的 AuthoringTool、BakeProvider 和验证测试完成。当前公开 Terrain Facade 仍封锁正式 `TerrainData` 输出，内部后端代码存在不能作为正式保存能力已经开放的证据。

## 世界对话工作台

世界工作台现在提供独立的对话作者工具，入口为：

```text
【ES】/内容制作/世界/对话工作台
```

也可以在世界构建工作台右侧检查器点击“打开对话工作台”。

相关源码：

- 对话图与放置数据：`Assets/Scripts/ESLogic/Runtime/World/Dialogue/ESWorldDialogueData.cs`；
- 对话图保存入口：`Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueAuthoringUtility.cs`；
- 对话工作台：`Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueWorkbenchWindow.cs`；
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
