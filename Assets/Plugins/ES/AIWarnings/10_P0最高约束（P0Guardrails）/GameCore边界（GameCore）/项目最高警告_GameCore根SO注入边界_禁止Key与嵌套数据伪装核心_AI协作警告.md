# 项目最高警告：GameCore 根 SO 注入边界

Status: current
StableId: es.aiwarning.p0.gamecore-root-so-injection-boundary.v1
Authority: AIWarnings（长期 P0 约束）；详细事实与迁移前快照见 Knowledge
RouteKeys: aiwarnings, p0, gamecore, root-so, injection, key, dependency, resource-boundary
Applicability: GameCore 根 SO、嵌套配置、RuntimeData、Prefab、场景和内容资产
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-gamecore-root-so-injection-boundary.md
StaleWhen: IGameCoreSO、GameCoreTable、ConfigKey、Consumer 收集、Group/Pack 注入或 SourceRef 哈希变化。

## P0 长期约束

- 依赖方向只能是 Prefab/场景/普通 SO → GameCore 根 SO；根 SO、嵌套数据和 RuntimeData 禁止反向保存 GameObject、Component、Prefab 或场景内容引用。
- GameCore 需要表达内容资源时只保存稳定类型化 Asset Key/ESAssetRefer，由 ResourcePlan、AssetTable 或资源系统解析；不得以 Unity 直接引用制造反向 Bundle 依赖。
- `IGameCoreSO` 只表示可被 Consumer 收集并注入的独立根 SO；Prefab 可以引用根 SO，但 Prefab、Key、RuntimeData、Shared/Variable 数据不得实现该接口。
- Info/Group/Pack 只按各自明确的启动聚合合同注入；不得复制内容、建立中央类别 switch/反射分发或把 Pack 当作默认资源/发布容器。
- 注入必须使用显式领域 ConfigKey 和强类型 Table；KeyName 只用于编辑器定位，不得作为运行时身份、RuntimeKey、存档、网络或资源身份。
- 新类别在自身领域定义 Key、RuntimeData、Table 和 Info，按 Acquire/准备/Commit 或 Abandon 事务提交；禁止修改 `0_Stand` 以增加中央根注入入口。
- 运行时根定义稳定驻留，查询走本领域强类型表；不得池化定义外壳、跨表解释 RuntimeKey 或让失败留下半提交数据。

## Knowledge 导航

详细事实、源码映射、迁移前语义快照与验收清单见 `es.aiwarning.p0.gamecore-root-so-injection-boundary.v1`。本 Warning 不授予写入、注入、运行时或发布权限。
