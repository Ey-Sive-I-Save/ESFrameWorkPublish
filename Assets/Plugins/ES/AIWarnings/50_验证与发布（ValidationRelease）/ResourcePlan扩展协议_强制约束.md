# ResourcePlan 扩展协议

最后核对：2026-08-02。

可选中间件不得修改 ES 资源发布核心，不得自行下载、缓存或管理平行引用计数。

## 当前实现状态

| 阶段 | 当前事实 | 约束 |
| --- | --- | --- |
| Bake | `IESResourcePlanBakeExtension` 已实现 | Editor 扩展把来源配置烘焙为稳定快照。 |
| Publish | Publisher 直接校验烘焙产物 | 当前没有 `IESResourcePlanPublishExtension`；不得宣称第三个独立扩展点已经存在。 |
| Runtime | `IESResourcePlanRuntimeExtension` 与 `IESResourcePlanExtensionLease` 已实现 | Prepare/Release 必须进入统一 Plan 生命周期。 |

## 编辑器 Bake 接入

1. 在可选模块的 Editor 程序集中实现 `IESResourcePlanBakeExtension`。
2. 由该模块的编辑器初始化入口调用 `ESResourcePlanBakeExtensions.Register` 一次。
3. `ProviderId` 全局唯一、稳定且不可改名；`SchemaVersion` 仅在快照语义变化时递增。
4. 扩展必须把来源配置转换为 `ESResourcePlanBakedExtensionEntry`，运行时禁止重新扫描来源 SO。
5. 发现的普通资源必须写入 `assets`，并使用已有 AssetKey/GUID 进入 Catalog、AB、Consumer 与下载流程。

## 发布边界

Publisher 直接验证 Bake 产物、资源闭包和发布计划；它不是可选中间件的独立扩展回调。需要新发布钩子时，先新增并验收明确接口与失败语义，再修改本文件；不得先在文档中虚构 `IESResourcePlanPublishExtension`。

## 运行时接入

运行时扩展必须在统一的 Plan 生命周期中 Prepare/Release，并返回 `IESResourcePlanExtensionLease`；禁止业务直接调用第三方加载与卸载 API。
同一 Plan 的重复 retain 不重复 Prepare；最后一个 retain 释放后才允许 Release。Extension Lease 必须随所属 Plan Scope 一同失效，不得跨 Provider、Plan 或场景生命周期复用。

`ESResourcePlanExtensionContext` 只提供当前 Plan Scope 已经成功加载并登记的资产：

- `TryGetLoadedAsset<T>` 只能读取当前 Plan 的已加载资产，不能据此触发额外加载；
- 扩展不得从 `ESAssetLibrary`、Consumer SO 或来源配置重新扫描；
- 扩展不得自行创建 `ESAssetScope`、Provider、下载器或平行引用计数；
- 扩展需要的普通资源必须在 Bake 产物的 `assets` 中声明，先由 ES Core 加载后再交给扩展。

扩展 Lease 的释放规则：

- `PrepareAsync` 成功后才登记 Lease；失败或取消不得留下半登记 Lease；
- Plan 释放、Scope 结束、Provider 切换、异常和强制 Dispose 都必须进入统一释放路径；
- 多个 Lease 释放时按登记逆序执行，单个 `Release()` 抛异常不能阻断其余 Lease、Plan Scope 或资源归还；
- 扩展不得把 Lease 缓存到下一个 Plan、Provider 或场景代际。

## FMOD 边界

FMOD 目前不在本项目实施范围。本节只保留为未来可选中间件示例，不能据此创建 `FMOD.*` 依赖、Consumer、发布规则或运行时加载代码。

未来若正式启用，FMOD 扩展负责 Event/Bank 到 Master、Strings、依赖 Bank 的闭包展开，以及 Bank 的实际加载/卸载；ES 核心仍只负责资源发布、下载、校验、Plan Scope 和生命周期。

## 禁止项

- 禁止使用字符串文件路径绕过 AssetLibrary/Catalog。
- 禁止在 Player 反射或读取扩展来源配置。
- 禁止 ProviderId 重复时后注册覆盖先注册。
- 禁止因扩展未安装而静默跳过已配置的 Plan 来源。
- 禁止把尚未实现的 Publish 扩展接口写入代码、文档或验收结论。
