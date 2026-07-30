# P2：ES 编辑器菜单根必须使用【ES】

## 级别

P2——明确的编辑器规范约束。新增或修改 ES 菜单入口时必须执行，发现旧入口时应在当前相关改动中修正。

## 强制规则

- ES 自有 Unity 顶部菜单只能使用 `【ES】/` 作为根路径。
- 禁止新增 `ES/`、`Window/ES/`、`Tools/ES/` 等分散入口。
- `MenuItem`、`CreateAssetMenu.menuName`、`AddComponentMenu` 均遵守同一根路径规范。
- 编辑器程序集能够引用统一菜单常量时，优先复用 `MenuItemPathDefine.ROOT_MENU` 或其子路径常量。
- 运行时程序集中的 `CreateAssetMenu`、`AddComponentMenu` 无法引用 Editor 程序集时，直接写稳定字面量 `【ES】/...`。
- `Assets/Plugins/ES/...` 等磁盘路径、类型名、普通显示文本不属于菜单路径，禁止机械误改。

## 唯一允许的一级分类

| 一级分类 | 用户意图 | 典型内容 |
|---|---|---|
| 常用窗口 | 高频窗口的独立入口 | RuntimeWatch、资源管理、轨道编辑器 |
| 内容制作 | 创建或编辑游戏内容 | 技能轨道、图编辑器、SO 数据、字体、角色与武器模板 |
| 资源与发布 | 资源组织到构建交付 | 资源管理、资源收集、AssetBundle、索引、发布配置 |
| 场景与对象 | 操作场景层级和 GameObject | 层级工具、预览清理、角色交互组件 |
| 运行时诊断 | 检查正在运行的游戏状态 | RuntimeWatch、交互面板、资源运行时监视器 |
| 项目设置 | 修改项目级稳定配置 | GameCore、输入、Tag、状态机、Luban、全局配置 |
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
```

菜单路径修改属于编辑器入口整理，不应改动序列化字段、资源 GUID、运行时协议或磁盘目录。
