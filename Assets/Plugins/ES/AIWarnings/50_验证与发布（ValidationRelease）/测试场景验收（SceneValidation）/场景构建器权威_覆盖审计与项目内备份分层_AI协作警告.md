# 场景构建器权威、覆盖审计与项目内备份分层

Status: current
StableId: es.aiwarning.validation.scene-builder-authority-backup-boundary
Authority: AIWarnings；Builder/Prefab/Scene 当前源码为事实权威。
RouteKeys: aiwarnings, validation, scene, builder, prefab, override, backup, evidence
Applicability: 测试场景构建器、Prefab 覆盖审计、玩家/载具验收场景、Preview/正式 Scene 与项目内备份。
EvidenceRef: `Documentation/AIKnowledge/Editor/project-scene-builder-authority/scene-builder-prefab-fixture-backup-authority.md`
Owner: ES Scene/Validation owners。
StaleWhen: Builder、Prefab 基线、场景布局、备份策略或 SourceRef 变化。

## 长期约束

- 构建器产物是场景布局、标题、出生点和验收导视的唯一权威；不得把旧场景手工修补当作当前功能事实。
- 角色/载具实例的非基线组件、字段和引用必须先按 Builder/Prefab 基线审计；3D KCC 角色不得混入 2D 物理或第二运动后端。
- 刷新顺序必须包含作者基线、项目内 before 备份与 SHA-256、官方 Builder 重建、Prefab override 审计、静态诊断和分层证据报告。
- 备份只允许 `ES/Bak/Local/<TaskKey>/` 或 `ES/Bak/Reviewed/<TaskKey>/`；before 与源文件必须同一版本，Reviewed 必须带 manifest，不得用项目外用户目录。
- 必须区分源码/配置、Unity 导入、静态诊断、PlayMode、Profiler、Player 与发布证据；静态或 `totalIssues: 0` 不得升级为可玩或发布通过。
- 详细 Builder/Fixture/Override/备份事实由 `es.unity.editor.project-scene-builder-authority.v1` Knowledge 承接；本 Warning 不授权写场景、Prefab、备份或运行验证。
