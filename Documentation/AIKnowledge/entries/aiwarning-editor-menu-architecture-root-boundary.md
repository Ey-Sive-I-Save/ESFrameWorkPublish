# ES Unity 菜单架构与入口边界

`KnowledgeId`: `es.aiwarning.editor.menu-architecture-root-boundary.v1`  
`Authority`: `AIWarnings + current menu validation source`  
`RouteKeys`: `aiwarnings`, `editor`, `menu`, `architecture`, `validation`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `3a7b8a3c3303c2a8fae9e6453c06f7abfa0d0fa654ae6bd0dcb6a10e370b548f`  
`SourceSetHash`: `3a7b8a3c3303c2a8fae9e6453c06f7abfa0d0fa654ae6bd0dcb6a10e370b548f`  
`EntryBodyHash`: `122fb78bc359c7dddd7fc9d5c2d35b3eb1a7724f6edfe7f380d8d016bdce3fd3`
`StaleWhen`: 菜单路径常量、六域分类、兼容程序集边界、扫描门禁或 Unity 菜单合同变化。

## 迁移说明

原 Warning 190 行、8803 UTF-8 字节；现行 Warning 仅保留 P2 长期约束、机器身份、证据入口和运行时非声明。详细菜单树、迁移映射、快捷投影限制、副作用分类、兼容路径和静态扫描边界迁入本条目。原 Warning 的历史事实由迁移台账中的 sourceSha256 与当前 Knowledge SourceRefs 回溯；本条目不授权修改菜单或恢复旧实现。

## 当前规则事实

- ES 自有 `MenuItem`、`CreateAssetMenu`、`AddComponentMenu` 的根必须精确为 `【ES】/`，但三棵树的一级分类分别按顶部任务、资产类型和组件能力建模。
- 顶部菜单的正式域包括常用窗口、内容制作、项目配置、资源与发布、验证与诊断、自动化与开发；`Assets/Create` 与 Add Component 不得复制顶部业务域。
- 常用窗口只允许打开窗口的快捷投影，必须调用同一正式打开方法；高风险动作和第二份业务逻辑不得进入快捷投影。
- `验证与诊断` 只表达任务领域，不自动承诺只读；静态审计、修复、测试、运行时监视、清理必须按实际副作用拆分入口并遵守授权。
- 兼容实现必须显式启用，并置于遗留兼容分支；历史路径只能作为历史证据，不能当作当前实现或迁移授权。
- 菜单显示路径不是稳定业务身份。路径迁移需检查 `ExecuteMenuItem`、启动器、命令面板、AICommand、Skill、文档和测试断言。

## 验证边界

`ES/Tools/Validation/Test-ESMenuArchitecture.ps1` 扫描项目自有活跃源码、兼容源码和正式文档，区分字面量与符号参数，并检查旧根和分类。该扫描不执行条件编译，也不证明 Unity 实机菜单、排序、分隔线、ReloadDomain、运行时或发布行为。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/P2_编辑器菜单根必须使用【ES】_AI协作警告.md` (`f31ed883e4cdf3e30fb3f375b322e9b6814e83ccdbfee1d8d58e2f379a818dc7`)
- `ES/Tools/Validation/Test-ESMenuArchitecture.ps1` (`7f0a0f58d7f5bae052a708b6a39ab9583c02f37300c140aad9a9836c2a1bc345`)
- `Assets/Plugins/ES/Editor/Installer/MenuItemPathDefine.cs` (`5362604001c995ed63f7157c0475e3506057d164585bbacd60e9acea0e0ce846`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

## EvidenceRefs

- `ES/Tools/Validation/Test-ESMenuArchitecture.ps1`
- `runtime-not-run`
