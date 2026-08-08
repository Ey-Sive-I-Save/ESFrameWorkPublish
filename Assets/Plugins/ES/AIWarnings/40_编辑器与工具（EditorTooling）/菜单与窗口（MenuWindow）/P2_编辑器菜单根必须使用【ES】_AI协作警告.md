# P2：ES Unity 用户菜单路径必须统一为【ES】

## 级别

P2——明确的编辑器规范约束。新增或修改 ES 菜单入口时必须执行，发现旧入口时应在当前相关改动中修正。

## 强制规则

- ES 自有 Unity 顶部菜单、`CreateAssetMenu.menuName` 与 `AddComponentMenu` 必须以精确字面量 `【ES】/` 开始；`【es】/`、`[ES]/`、`ES/` 都是错误路径。
- `【ES】` 中的 `ES` 必须大写，禁止根据显示语言、目录名或类型名改变大小写。
- 禁止新增 `ES/`、`Window/ES/`、`Tools/ES/` 等分散入口。
- `MenuItem`、`CreateAssetMenu.menuName`、`AddComponentMenu` 均遵守同一根路径规范。
- 编辑器程序集能够引用统一菜单常量时，优先复用 `MenuItemPathDefine.ROOT_MENU` 或其子路径常量。
- 运行时程序集中的 `CreateAssetMenu`、`AddComponentMenu` 无法引用 Editor 程序集时，直接写稳定字面量 `【ES】/...`。
- `Assets/Plugins/ES/...` 等磁盘路径、类型名、普通显示文本不属于菜单路径，禁止机械误改。

### AddComponentMenu 组件归类与配置命名

- `AddComponentMenu` 与 `MenuItem`、`CreateAssetMenu.menuName` 使用同一套 `【ES】/` 根路径和一级分类白名单；这是一条 P2 编辑器入口规范，不上升为 P0 架构约束。
- 普通挂在场景、Prefab 或 UI GameObject 上的运行时组件，默认归入 `【ES】/场景与对象/`；`UI`、`动态图集` 等可以作为二级或更深层用户意图名称。
- 资源绑定或发布辅助组件可归入 `【ES】/资源与发布/`；内容作者使用的制作组件可归入 `【ES】/内容制作/`；仅调试观测组件可归入 `【ES】/运行时诊断/`；测试专用组件可归入 `【ES】/示例与测试/`。
- `配置` 是允许的一级分类。配置资产和配置工具可以使用 `【ES】/配置/...`；如果配置明确属于项目设置、资源发布或内容制作，也可以分别使用 `【ES】/项目设置/...`、`【ES】/资源与发布/...` 或 `【ES】/内容制作/...`，不得为了“配置”再创建重复一级入口。
- ES 定义的 Profile 标准对象统一归入 `【ES】/配置/...`，不因它是 MonoBehaviour、ScriptableObject、Prefab 组件或是否参与生命周期而改放其他一级分类；`Profile`、业务领域和对象类型作为二级及以下路径表达。例如 `【ES】/配置/Profile/ES 通用 Profile`。普通相机内容定义应使用 `【ES】/配置/相机/相机视图定义`，不能仅因包含镜头参数而命名为 Profile。
- 组件路径应优先简短、稳定、可搜索，不必为表达 `UI` 或程序集层级增加多余嵌套。例如动态图集组件使用 `【ES】/场景与对象/动态图集 Graphic` 和 `【ES】/场景与对象/动态图集 Domain Owner` 即可；只有符合 ES Profile 标准的组件才使用 `【ES】/配置/...`。

正确示例：

```csharp
[AddComponentMenu("【ES】/场景与对象/动态图集 Graphic")]
[AddComponentMenu("【ES】/场景与对象/动态图集 Domain Owner")]
[AddComponentMenu("【ES】/配置/Profile/ES 通用 Profile")]
[CreateAssetMenu(menuName = "【ES】/配置/动态图集/动态图集策略")]
```

## 路径层级与显示语言

- 一级分类只能使用下表定义的精确中文名称；不得新增 `Automation`、`GameCore`、`数据`、`图与流程`、`审计`、`音频`、`运行时` 等实现名一级入口。
- 二级及以下路径以中文用户意图命名。`ESAITest`、`ESTEST`、`AI`、`VFX`、`GameCore`、`ConfigKey`、`RuntimeWatch`、`Player`、`PlayMode` 等稳定产品或技术术语允许保留；其余可读操作名应使用中文。
- 自动化是一级受管入口，唯一正式用户路径为 `【ES】/自动化/`；`ES/Automation/` 仍是磁盘、协议、报告和任务 ID 的稳定 ASCII 路径，二者不得互相替换。
- 一级“自动化”只提升发现性和治理入口，不授予 Worker、发布、资产写入、删除或上传权限；实际执行仍须经过 ESAutomation 的任务合同、Capability、PathPolicy 与当前授权。
- `Assets/Create/`、`Assets/`、`GameObject/` 等 Unity 宿主上下文菜单可以作为前缀；其后必须继续使用 `【ES】/<允许一级分类>/...`，例如 `Assets/Create/【ES】/内容制作/图与流程/...`。它们不是新的 ES 一级菜单。
- `已废弃` 下的历史入口仅可修正根路径和明显错误，禁止借迁移向正式分类重新引入已废弃功能。

## 当前迁移映射

| 旧一级路径 | 正式路径 |
|---|---|
| `【ES】/Automation/...`、`【ES】/开发与维护/自动化/...` | `【ES】/自动化/...` |
| `【ES】/GameCore/...`、`【ES】/数据/GameCore/...` | `【ES】/项目设置/GameCore/...` |
| `【ES】/图与流程/...` | `【ES】/内容制作/图与流程/...` |
| `【ES】/审计/...` | `【ES】/开发与维护/审计/...` |
| `【ES】/音频/...`、`【ES】/运行时/...`（组件） | `【ES】/场景与对象/...` |

## 唯一允许的一级分类

| 一级分类 | 用户意图 | 典型内容 |
|---|---|---|
| 常用窗口 | 高频窗口的独立入口 | RuntimeWatch、资源管理、轨道编辑器 |
| 自动化 | 受管任务、AI 协作、测试、Worker、编译控制与报告 | 自动化中心、ESAITest、AI 控制、受管 Worker |
| 内容制作 | 创建或编辑游戏内容 | 技能轨道、图编辑器、SO 数据、字体、角色与武器模板 |
| 资源与发布 | 资源组织到构建交付 | 资源管理、资源收集、AssetBundle、索引、发布配置 |
| 场景与对象 | 操作场景层级和 GameObject | 层级工具、预览清理、角色交互组件 |
| 运行时诊断 | 检查正在运行的游戏状态 | RuntimeWatch、交互面板、资源运行时监视器 |
| 项目设置 | 修改项目级稳定配置 | GameCore、输入、Tag、状态机、Luban、全局配置 |
| 配置 | 面向用户的独立配置资产和配置工具 | Profile、策略、预设和跨模块配置 |
| 开发与维护 | 维护工程和开发流程 | Cmd Agent、项目资产职责、综合工具、自检、文档 |
| 安装与集成 | 安装框架或外部依赖 | 安装管理器、依赖检查 |
| 示例与测试 | 演示、验收和测试数据 | RuntimeWatch 展示、资源卸载验收、Editor Solver 案例 |
| 已废弃 | 仅为迁移或历史参考保留 | AIPreview、VMCP、EditorLegacy、EditorTesting |

禁止用窗口名或实现名创建新的一级分类，例如 `Resource`、`GameCore`、`Runtime Data`、`Tools`、`Preview`。无法判断归属时，应先根据用户执行该功能的目的选择一级分类，再按业务对象建立二级分类。

`常用窗口`是独立一级分类，不得并入“开发与维护”或“运行时诊断”。窗口必须同时拥有一个表达真实业务归属的正式入口，不能只存在于常用窗口。

## 正确示例

```csharp
[MenuItem("【ES】/资源与发布/资源管理/打开资源工具")]
[CreateAssetMenu(menuName = "【ES】/项目设置/资源/资源配置")]
[AddComponentMenu("【ES】/场景与对象/资源/资源加载器")]
```

## 错误示例

```csharp
[MenuItem("ES/资源/打开资源工具")]
[MenuItem("Window/ES/Resource Monitor")]
[CreateAssetMenu(menuName = "ES/Resource/Config")]
[AddComponentMenu("ES/Runtime/Loader")]
```

## 修改后检查

至少扫描以下声明，确认不存在旧根：

```text
MenuItem("ES/
MenuItem("Window/ES/
CreateAssetMenu(... menuName = "ES/
AddComponentMenu("ES/
【es】/
【ES】/Automation/
【ES】/GameCore/
【ES】/数据/
【ES】/图与流程/
【ES】/审计/
【ES】/音频/
【ES】/运行时/
```

同时对所有 `【ES】/` 与 `Assets/Create/【ES】/` 声明提取一级分类，必须只落在本文件表中的允许值。`EditorApplication.ExecuteMenuItem(...)`、窗口启动器索引、AICommand、Agent Skill 和当前规范中出现的用户菜单路径必须在同一次迁移内同步；历史复盘只保留事实记录，不得重写。

菜单路径修改属于编辑器入口整理，不应改动序列化字段、资源 GUID、运行时协议或磁盘目录。
