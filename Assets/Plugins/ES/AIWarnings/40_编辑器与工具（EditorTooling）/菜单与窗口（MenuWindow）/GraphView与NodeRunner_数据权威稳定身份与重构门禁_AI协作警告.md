# GraphView 与 NodeRunner：数据权威、稳定身份与重构门禁 AI 协作警告

**状态：阻断性现状。** 当前 `ESGraphView` / `NodeRunner` 是历史实验性实现，GraphView 目前不具备可作为正式玩法、任务、技能或流程编辑器依赖的生产可用性。禁止新增业务依赖它。

最后核对：2026-08-02。

## 当前事实，不得粉饰

- `ESGraphViewWindow` 使用静态 Window / Container 关联；`ESGraphView_Part_FlowChart` 在 UI 变更中直接写 `Runner.Flows`、节点位置和 Container。
- `NodeContainerSO` 同时以 `[SerializeReference] List<NodeRunnerSO>` 保存列表，并把新节点作为 `ScriptableObject` 子资产加入 Asset；创建、复制、删除直接调用 `AssetDatabase.Refresh/SaveAssets`。
- 当前没有正式 NodeId、PortId、EdgeId、图 SchemaVersion、迁移协议、端口类型/容量语义、循环规则或运行时编译快照。
- `GetCompatiblePorts` 只排除同方向和同节点，不构成语义连线校验。
- 当前图文件中没有完整的 `Undo/RegisterCompleteObjectUndo` 事务链；Graph UI 回调、直接资产写入、LINQ/日志和静态状态也不适合作为正式编辑器基础。
- `NodeRunner` 当前只有最小 `Execute -> OnEnter` 和 `None/Running/Exit` 状态，不是完整调度、取消、异常隔离或可复现执行协议。

因此不得以“已有窗口、节点、搜索、Inspector、Runner”为由称它已可用。

## 禁止的补丁方式

- 禁止继续在当前静态 Window/Container、`graphViewChanged` 回调和直接 AssetDatabase 写入上叠加业务功能。
- 禁止让 GraphView 元素、`NodeRunnerSO` 可变对象或 UnityEditor API 进入 Player 执行链。
- 禁止把节点列表下标、对象引用或显示名称当成稳定身份；删除、复制、重排、重开资产与域重载后都不可靠。
- 禁止为补 UI 先添加更多按钮、搜索项或端口特效。数据模型、Undo、稳定身份和迁移未收口前，UI 增量只会放大不可恢复数据风险。

## 重新启用前的目标模型

```text
Graph Asset（唯一权威序列化模型）
  NodeRecord: NodeId + TypeId + Version + Payload + Position
  PortRecord: PortId + 定向 + 类型 + 容量
  EdgeRecord: EdgeId + OutputPortId + InputPortId
  SchemaVersion + 明确迁移记录
        |
        +-> GraphView：仅编辑投影，所有变更通过原子命令和 Undo 写回 Asset
        |
        +-> Runtime Snapshot / Compiled Plan：脱离 UnityEditor 与可变 SO 的执行数据
```

重建不要求造“万能图框架”，但每个正式图领域都必须具备上述最小身份、合法性、迁移、Undo 与运行时分离边界。

## 必须先通过的重构门禁

1. 图数据只能由明确的 Graph Asset 模型权威保存；创建、复制、删除、连线、断线、移动必须是可回滚的原子变更。
2. 节点、端口、边必须有稳定、序列化、不可依赖列表下标的身份；端口类型、方向、容量、重复边与循环策略必须由模型校验。
3. GraphView 重开、重选、域重载、窗口销毁、缺失节点类型和资产子对象清理必须可恢复，不得残留孤儿子资产或静态引用。
4. Undo/Redo 必须覆盖节点、边、位置、类型替换和复制；多选与深图操作不得按单个 UI 回调部分提交。
5. 运行时只能执行已验证的 snapshot/compiled plan；禁止运行时修改 Asset、`ScriptableObject` 图节点或依赖 `UnityEditor`。
6. 必测：创建、复制、删除、Undo/Redo、重开、域重载、缺失类型、深图、循环/容量规则、多选、资产子对象回收与运行时隔离。

在上述模型和测试完成前，唯一允许的工作是受控重构、数据导出和归档审计；不得把当前 GraphView 接入任何正式系统。
