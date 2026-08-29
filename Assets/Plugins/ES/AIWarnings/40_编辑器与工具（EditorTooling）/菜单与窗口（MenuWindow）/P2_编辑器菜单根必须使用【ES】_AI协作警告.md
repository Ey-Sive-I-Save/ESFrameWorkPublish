# P2：ES Unity 菜单信息架构与入口边界

Status: current
StableId: es.aiwarnings.editor.menu-architecture-root-boundary
Authority: AIWarnings
RouteKeys: aiwarnings, editor, menu, architecture, validation
Applicability: 新增或修改 ES 自有 MenuItem、CreateAssetMenu、AddComponentMenu、ExecuteMenuItem 或相关文档/测试断言时。
EvidenceRef: ES/Tools/Validation/Test-ESMenuArchitecture.ps1 -RouteId es.aiwarnings.editor.menu-architecture-root-boundary
Owner: ES Editor/Tooling
StaleWhen: 菜单路径常量、六域分类、兼容程序集边界、扫描门禁或 Unity 菜单合同变化。
Knowledge: es.aiwarning.editor.menu-architecture-root-boundary.v1

长期约束：
- ES 自有入口必须使用精确根 `【ES】/`；禁止 `【es】/`、`[ES]/`、`ES/`、`Window/ES/`、`Tools/ES/`。
- 顶部菜单、`Assets/Create/【ES】`、`Add Component/【ES】` 是三棵不同菜单树，分别遵守各自一级分类；不要机械替换磁盘路径、类型名、协议 ID 或普通文本。
- 常用窗口只能投影打开动作并复用正式入口；不得投影写资产、修复、清理、测试执行或外部进程动作。
- 验证与诊断不等于只读：审计、修复、测试、运行时监视和清理必须按真实副作用分别命名和授权。
- 兼容入口只能位于显式兼容程序集的 `【ES】/自动化与开发/遗留兼容/...`；历史路径保留为历史事实，不得恢复旧实现。
- 菜单路径不是业务身份；迁移必须同步显式路径、启动器、AICommand、Skill、文档和测试断言，不得复制第二套业务逻辑。

静态扫描仅证明可解析源与文档范围；Unity 实机菜单排序、分隔线、禁用状态、ReloadDomain 和运行时行为仍未证实。
