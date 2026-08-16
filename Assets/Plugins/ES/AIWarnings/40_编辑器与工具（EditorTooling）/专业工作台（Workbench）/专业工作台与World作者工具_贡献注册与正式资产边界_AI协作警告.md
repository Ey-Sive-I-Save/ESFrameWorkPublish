# 专业工作台与 World 作者工具：贡献注册与正式资产边界

状态：现行约束；当前实现事实核对至 2026-08-16，Unity 实机验收待完成。

适用范围：`ESWorkbenchWindowBase`、工作台贡献注册、模块模板与裁剪、World/地图/UGC 作者工具、PreviewScene、Unity Terrain 后端、正式 Scene/Prefab/TerrainData 输出，以及后续角色、特效、Shader、动画等专业工作台。

## 一、基础层与业务层职责

专业工作台基础层必须负责可复用的窗口工作流：稳定贡献身份、模块启用策略、注入排序、依赖、去重、会话释放、页面/工具/视口/对象/Inspector/命令注册、选择与刷新、Undo/Dirty、预览生命周期、验证和失败恢复。

业务工作台负责领域逻辑与真实后端，例如地图数据结构、Terrain 笔刷、植被规则、Prefab 放置、NavMesh 烘焙、水体天气、资源收集和 UGC 配额。业务层不得复制基础注册表、页面生命周期、预览清理或通用布局状态机；基础层也不得把领域占位 UI 冒充真实业务能力。

## 二、模块模板与贡献注册不是同一层

- `ESWorkbenchModuleKind` 表示当前工作台启用哪些功能及其期望顺序。
- `ESWorkbenchContributionCategory` 只表示贡献的语义分类，不能代替模块身份。
- `WorkbenchId + ContributionId` 是贡献稳定身份；Owner、Revision、Dependencies、IsEnabled、Inject 和释放句柄共同构成窗口会话合同。
- AssemblyStream 或窗口打开阶段只能登记轻量描述和工厂委托；不得在注册阶段扫描全项目、创建正式 Scene 对象或写资产。
- 真实页面、工具、视口、对象源和资源槽位只在明确窗口会话中注入，并在重载、重新绑定或关闭时确定性释放。

当前源码中 `ESWorkbenchContributionDescriptor` 没有模块字段，`ESWorkbenchContributionRegistry.Open` 也不接收最终模块列表。因此“基础层已经按模块列表自动过滤并按模块顺序注入所有贡献”不是现行事实。

World 当前通过每个页面贡献自己的 `IsEnabled` 委托调用 `ESWorkbench_IsModuleEnabled` 完成本地过滤；这只能证明 World 当前页面贡献遵循模块开关，不能冒充所有专业工作台都自动获得同一保证。综合测试窗口仍直接调用 `ESWorkbench_RegisterPage`，不属于贡献注册主流程的完整案例。

在基础合同正式补齐前，文档和交付声明必须明确区分：

1. 模块列表已经计算；
2. 某个业务工作台手工按模块过滤；
3. 基础注册表原生绑定模块并统一排序。

只有第 3 项真实存在并通过测试后，才可声明模块裁剪和排序是所有工作台的基础能力。

## 三、排序、刷新与释放门禁

- 模块列表顺序必须真正驱动页面和相关能力的可见顺序；仅保存 List、显示模块名称或按贡献 Priority/ID 排序，不等于模块顺序已经生效。
- 一个宿主启用流程只能完成一次有效贡献会话装配。业务窗口不得在基础类已经装配后无条件再次装配。
- 重新装配前必须释放上一会话，并清理所有由该会话产生的页面、槽位、视口、对象、层级、适配器、Inspector、工具、命令和问题源。
- 释放 `IDisposable` 但保留旧页面，同样属于残留；模块关闭、贡献移除或排序改变后不得继续显示旧页面。
- 重新绑定资产时只重建真正依赖资产的状态；稳定窗口结构不得因刷新发生重复订阅、重复页面或旧闭包残留。
- 单个贡献失败必须隔离并形成可见诊断；依赖失败、循环依赖和不同 Owner 的稳定 ID 冲突不得静默覆盖。

当前源码仍存在两个待修正点：World 的宿主启用链在基础装配后再次调用 `ESWorkbench_LoadContributions()`；基础释放方法未清空页面列表。它们不必然在固定模块列表下立即产生重复页面，但会削弱动态裁剪、排序和可重复装配合同，不能按已收口描述。

## 四、作者态、预览与正式产物必须分层

地图工作流至少区分：

1. `ESWorldMapAsset` 作者态定义；
2. ES Heightfield 或其他地形数据源；
3. PreviewScene 中的临时 Terrain/GameObject；
4. Unity `TerrainData` 正式资产；
5. 正式 Unity Scene/Prefab；
6. NavMesh、碰撞、资源清单、运行时加载与发布产物。

只修改 Heightfield 或保存 `ESWorldMapAsset`，不得声明 `TerrainData` 已同步；PreviewScene 显示正确，不得声明正式 Scene、Prefab、碰撞或导航正确。

当前 Unity Terrain 后端内部存在创建/更新 `TerrainData` 和 Scene 的代码，但公开 Facade 的 `TryBakePersistent` 仍明确封锁正式输出，因为缺少未保存场景检查、覆盖备份、原子提交和失败回滚。不可绕过 Facade 直接调用内部后端，也不可用内部方法存在证明正式地形保存已经可用。

正式输出开放前至少必须具备：

- 明确目标路径与覆盖预检；
- 当前 Scene 未保存内容保护；
- Undo、备份或等价可恢复策略；
- TerrainData 与 Scene 的一致提交或失败回滚；
- 写入后重新加载并核对目标资产；
- 失败、部分成功和恢复动作的中文状态；
- Unity Editor 实机、Domain Reload 和目标工作流测试证据。

## 五、验证与完成声明

`.csproj` 静态构建只能证明生成工程当前可编译，不能证明 Unity 导入、窗口交互、Domain Reload、Undo、场景保存或发布链通过。

专业工作台至少验证：

- 默认模块替换、删除、新增和排序真实改变页面及能力；
- 重复注册、重复打开、资产重绑和 Domain Reload 不产生重复注入或旧状态；
- 贡献冲突、依赖失败和异常具备可恢复诊断；
- PreviewScene 关闭后临时对象和资源全部释放；
- 正式资产输出执行写后重读，并覆盖取消、失败回滚和已有资产更新；
- 深色/浅色主题、中文、窄窗口和高 DPI 下主流程可用；
- World、综合测试以及至少一个非 World 专业工作台复用同一基础合同，而不是各自复制临时逻辑。

在以上证据完成前，准确表述是“工作台基础骨架和 World 局部接入已形成，仍在集成与验证”，不得声明为商业级 UGC 工作台、完整 Terrain 作者链或所有专业工作台均可直接复用。
