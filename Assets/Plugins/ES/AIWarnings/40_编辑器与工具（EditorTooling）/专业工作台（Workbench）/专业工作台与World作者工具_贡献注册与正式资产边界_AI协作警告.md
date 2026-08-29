# 专业工作台与 World 作者工具：贡献注册与正式资产边界

Status: current
StableId: es.aiwarning.editor.workbench-contribution-asset-boundary
Authority: AIWarnings；当前 Workbench/World editor source 为事实权威。
RouteKeys: aiwarnings, editor, workbench, world, contribution, preview, asset, commit
Applicability: ESWorkbenchWindowBase、ContributionRegistry、模块裁剪、World/UGC 作者工具、PreviewScene 与正式 Scene/Prefab/TerrainData 输出。
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-editor-ugc-workbench-draft-commit-boundary.md`
Owner: ES Editor/Workbench owners。
StaleWhen: Workbench Host、Contribution、Draft/Commit、PreviewScene、World 作者或 SourceRef 变化。

## 长期约束

- 基础层统一负责稳定贡献身份、模块启用/排序、依赖去重、会话装配/释放、选择刷新、Undo/Dirty、预览生命周期、验证和失败恢复；业务层只实现领域后端，不复制注册表或把占位 UI 冒充能力。
- 生产 Workbench 必须具备稳定 WorkbenchId、作者会话与 Source/Baseline/Draft/ChangeSet/Commit 边界、ContributionRegistry 注册、统一选择/资源/视口/Inspector/命令/问题通道及 Reload/关闭/失败恢复释放证据；普通工具、控制台、预览或验证窗口不得冒用该产品名。
- `WorkbenchId + ContributionId` 是稳定身份；注册阶段只登记轻量描述/工厂，不扫描全项目、创建正式 Scene 或写资产。重装配前必须清理旧页面、槽位、视口、对象、订阅和闭包，冲突/依赖失败不得静默覆盖。
- 作者态、PreviewScene 临时对象和正式 Scene/Prefab/TerrainData、导航、碰撞、运行时加载与发布产物必须分层；保存作者态或预览正确不等于正式资产或发布正确。
- 正式输出必须显式目标、覆盖预检、未保存 Scene 保护、Undo/备份、原子提交/失败回滚、写后重读和 Unity 实机证据；不得绕过 Facade 直接调用内部后端。
- Knowledge 条目承接详细 UI/World 事实、历史校正、失败恢复和验收矩阵；Knowledge 只导航，不替代源码、运行时证据或用户授权。
