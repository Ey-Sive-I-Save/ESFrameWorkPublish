# GameCoreEditorGlobalData 与 AICommands 协作警告

> Status：current；StableId：`es.aiwarnings.arch.gamecore-editor-globaldata-aicommands`
> Authority：`AIWarnings`；RouteKeys：`aiwarnings`、`architecture`、`gamecore`、`aicommand`、`catalog`
> Applicability：编辑器全局语义入口、GameTag/属性 Schema、AICommand 模板与 Bake 投影。
> EvidenceRef：`Documentation/AIKnowledge/entries/aiwarning-architecture-gamecore-editor-globaldata-aicommands.md`；当前源码与资产 SourceRefs。
> Owner：ES GameCore/AI governance；StaleWhen：GameCoreEditorGlobalData、菜单、Catalog/Bake、AICommands 或稳定身份合同变化。
> Knowledge：`Documentation/AIKnowledge/entries/aiwarning-architecture-gamecore-editor-globaldata-aicommands.md`

## 不可下放的长期边界

- `GameCoreEditorGlobalData` 是编辑期唯一语义入口；运行时只消费 Bake 产物，不直接依赖该编辑器 SO，也不替代领域 DataInfo、Catalog、Table 或运行时 GameCore 根。
- GameMode、GameTag、角色/物品属性 Schema、Input 分类、物理层语义和 AICommand 模板集中维护；固定 API 代码生成只能由受控菜单触发，不能手改生成文件或把普通属性强行代码化。
- 配置只保存稳定 Key、版本和参数，禁止 `System.Type`、程序集限定名、委托、RuntimeKey 或场景实例；运行时创建/绑定/租出时解析并缓存，热路径禁止字符串查表、反射和按帧创建策略对象。
- BehaviorProfile、Domain Module、Policy/Strategy、StateMachine 各自拥有选择、生命周期、算法和互斥状态；不得借 GameCore 或“策略”名义新建万能依赖注入/跨领域事务系统。
- 新增输入、Tag、属性、物理层、Shot 或 GameMode 前必须同时检查当前源码、资产、菜单、Bake、Catalog、验证和 AICommand；禁止恢复旧 `GameCoreGlobalData` 类型、路径或菜单根。

详细字段、菜单动作、源码/资产映射、旧入口迁移、验证步骤和原文快照见 Knowledge；静态存在性不能证明 Unity 菜单、Bake 或发布行为已通过。
