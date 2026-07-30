# ESFramework 职责收口规划

状态：`Reserved`，持续治理，不进行一次性大搬迁  
审计范围：`Assets/Plugins/ES`、`Assets/Scripts/ESLogic`、资源构建/运行时、GameCore、样例与文档

## 1. 总体判断

ESFramework 存在局部职责分散，但不是所有“大目录、多文件”都代表架构混乱。

当前真正需要关注的是：

1. 新旧资源运行链仍同时位于 `0_Stand/_Res`；
2. 新资源权威链跨 `ES_Stand`、`ES_Design`、`ES_Logic`、`ES_Editor`，部分生命周期编排集中在超大模块；
3. Runtime、Developer、Examples、Samples 的物理归属与是否进入 Player 程序集不完全一致；
4. 正式规则、未来提案、历史归档和旧 Documentation 缺少统一索引。

以下结构本身是合理的，不应为了“文件少”合并：

- 17 类 Asset ConfigData 各自强类型目录；
- GameCore 各领域独立 RuntimeData/Table；
- Editor 烘焙、构建、发布三个阶段分开；
- Entity、Item、State、Operation 按领域拆分。

## 2. 当前程序集职责

| 程序集 | 应有职责 | 当前风险 |
| --- | --- | --- |
| `ES_Stand` | 跨程序集稳定契约、身份值、Provider/RuntimeMap 基础协议、最小 Bootstrap 契约 | `_Res` 同时包含旧 ESResMaster/Loader 与新版下载、Provider、RuntimeMap 实现，稳定层偏重 |
| `ES_Design` | 通用 ConfigKey、Retained Table、领域无关设计原语 | 当前 ConfigKey 已较集中；禁止继续下沉 Loader/Handle 或业务类别 switch |
| `ES_Logic` | GameCore/Asset 强类型业务表、运行时领域逻辑、资源生命周期编排 | `MODULE_ESRuntimeDataModule.cs` 同时承载静态表、Catalog 注入、Bootstrap、Consumer、Resident、GameCore 预加载与查询门面 |
| `ES_Editor` | 资产烘焙、AB 规划/构建、发布、诊断窗口 | Pipeline 分阶段方向正确，但个别窗口文件体积过大，UI 与流程编排可能继续耦合 |

## 3. 高优先级收口：资源运行时权威链

当前资源职责分布：

```text
ES_Stand/_Res
  → AssetIdentity、RuntimeMap、Provider、Downloader、Bootstrap
  → 同时仍保留 ESResMaster、ESResLoader、ESResKey、ESResSource

ES_Design
  → ESConfigKeyTable / ESRetainedConfigKeyTable

ES_Logic/Data/AssetConfigKey
  → 17 类 Asset ConfigData 与 ESAssetConfigKeyTable

ES_Logic/GameManager/Modules/Runtime
  → Catalog 注入、Provider 绑定、Consumer/Library 激活、Resident/GameCore 预加载

ES_Editor/ESResPipeline
  → 烘焙、AB 构建、发布和运行诊断
```

这条链的分程序集本身有合理原因，问题是旧链仍在 `ES_Stand` 内保持大量可调用实现，使运行时存在重新形成双权威的风险。

未来收口顺序：

1. 冻结新版公开入口：业务只经 AssetTable/ESAssetScope/ResourcePlan 使用资源。
2. 完成四模式、发布下载、子资产、Consumer/Library 的端到端验收。
3. 扫描旧 `ESResMaster/ESResLoader/ESResKey/ESResSource` 的生产调用者。
4. 逐调用点迁移，不增加兼容门面。
5. 旧链无生产调用后再移出运行程序集或进入 Archive。
6. `0_Stand` 后续只允许增加稳定契约，不再增加新的业务编排职责。

## 4. 高优先级收口：MODULE_ESRuntimeDataModule

该文件约 1400 行，包含多个不同变化原因：

- GameCore 六类静态表生命周期；
- Asset 17 类静态表与 Catalog/Page 注入；
- Asset Loader 绑定和清理；
- Release Bootstrap、Consumer/Library 按需激活；
- Resident 与 GameCore 资产预加载；
- RuntimeData 查询门面。

未来应优先按职责拆分内部实现，但保持现有用户入口和程序集不变：

```text
AssetTableRegistry
  → 17 类表、Catalog/Page 预检与注入

AssetRuntimeLifecycle
  → Provider/Loader 绑定、资源环境初始化与释放

ConsumerActivation
  → Consumer/Library、Resident、GameCore 预加载

GameCoreTableRegistry
  → 六类 GameCore 表与查询
```

这可以先通过同程序集内部类型或 partial 文件完成。禁止增加新的全局权威门面，也禁止让普通用户学习更多 API。

## 5. 中优先级收口：Runtime、Developer 与 Samples

审计发现：

- `Assets/Scripts/ESLogic/Runtime/Developer` 约 79 个 C# 文件、约 3887 行；
- `Assets/Scripts/ESLogic/Samples` 约 10 个 C# 文件、约 1563 行；
- 它们位于 `ES_Logic.asmdef` 覆盖范围内，除非由条件编译剔除，否则会进入正式程序集。

不能直接搬走整个 `Developer`：其中 `Components`、`ValueEntry`、`Wrappers` 可能是正式公开能力。正确做法是逐类判定：

| 内容 | 未来归属 |
| --- | --- |
| 正式运行时组件/Wrapper/ValueEntry | 保留 Runtime，但改为能够表达职责的领域目录 |
| 仅演示用 MonoBehaviour | 移入 `3_Examples` 或独立 Samples asmdef |
| 资源流水线验收场景脚本 | 独立 Validation/Samples asmdef，不进入核心 Player 程序集 |
| 纯 Editor Gizmo/Preview | `ES_Logic.Editor` 或完整 `#if UNITY_EDITOR` 边界 |

迁移前必须扫描 Prefab/Scene/ScriptableObject 序列化引用，禁止只移动源码导致 Missing Script。

## 6. 中优先级收口：Editor 大文件

以下文件体积提示维护风险，但不能仅凭行数判定拆分：

- `ESAssetPackageBakeWindow.cs` 约 6800 行；
- `ESCmdAgentWindow.cs` 约 4900 行；
- `ESTrackViewWindow.cs` 约 3800 行。

未来应按“状态模型、流程服务、视图绘制、诊断输出”拆内部职责，保持现有窗口入口。优先提取已有重复业务逻辑，禁止为了行数创建大量只有一两个方法的脚本。

## 7. 低优先级：可选第三方依赖边界

`ES_Logic` 当前直接引用 KCC、RootMotion、DOTween、EasySave3、InputSystem 等依赖。对于完整游戏工程这是可接受的；若未来要把 ES 作为可裁剪商业包分发，才需要把角色运动、IK、Tween、存档等拆成可选集成程序集。

当前不应为了理论解耦立即拆程序集，因为会扩大 asmdef、序列化类型和用户安装复杂度。

## 8. 文档与规划治理

- 当前强制规则：`Assets/Plugins/ES/AIWarnings`；
- 当前执行命令：`Assets/Plugins/ES/AICommands`；
- 未来计划：`Documentation/FuturePlans`；
- 用户/架构说明：`Documentation`；
- 历史源码：根目录 `Archive`；
- Unity 生成物：`Library/Artifacts`，禁止人工写入。

未来计划批准实施后，应拆成 AICommand/任务清单；完成并冻结后，把最终规则迁入 AIWarnings，并将计划状态改为 `Implemented`。

## 9. 分阶段路线

### Phase A：现在

- 冻结预检保护重建和现有 AssetTable 用户 API。
- 不实施 RuntimeGeneration。
- 不移动 0_Stand、ES_Logic 或序列化脚本。
- 只建立职责索引和生产调用扫描。

### Phase B：新版资源链完整验收后

- 迁移旧 ESRes 生产调用者；
- 收口 `MODULE_ESRuntimeDataModule` 内部职责；
- 将示例/验收脚本从核心 Player 程序集分离；
- 保持用户 API 不变。

### Phase C：出现明确产品需求后

- 玩法中无停顿资源热插拔：立项完整 RuntimeGeneration；
- ES 商业包按功能裁剪：立项可选集成程序集；
- Editor 工具维护成本成为瓶颈：拆分窗口内部模型和服务。

## 10. 任何收口任务的验收原则

1. 先证明职责冲突或编译/运行成本，再移动文件。
2. 不改变 EnumKey/StringKey/GUID/RuntimeMap 的权威边界。
3. 不增加普通用户 API，不恢复旧兼容门面。
4. 不破坏 Unity 序列化引用和 `.meta` GUID。
5. 热路径保持 O(1) 与 `0 GC`。
6. 每个阶段独立编译、Unity Test Runner 和真实发布链验收。
7. 范围外编译错误单独记录，不借机扩大改动。

