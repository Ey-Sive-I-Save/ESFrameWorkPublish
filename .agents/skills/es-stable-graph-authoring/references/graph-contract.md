# Stable Graph Contract

Graph 必须含 graphId、schemaVersion、node identities、ordered edges、consumer、migration、snapshot、evidenceRef、owner、staleWhen；Legacy GraphView/NodeRunner 是明确 non-claim。

## Stable ordering

- `ESGraphEdgeRecord.order` 是图关系的唯一作者顺序，必须进入迁移、Snapshot、内容签名、消费者专属 Spec、恢复校验和 Undo/Redo。
- `order` 的分组与兜底语义必须由消费者合同声明：有序输入按目标端点分组，其他多路关系按来源端点分组；组内先按显式 `order`，再仅在非法/重复顺序的迁移或诊断路径按 `EdgeId` 做确定性兜底。不能用画布位置、数组偶然顺序或 `EdgeId` 推断正常业务顺序。
- 新建边获得新 `EdgeId` 与新顺序；重连同一关系保留 `EdgeId` 和原顺序。顺序调整必须由 `ESGraphEditService` 在一个原子事务中完成。

## Identity-preserving migration

- Schema 迁移必须保留 `graphId`、`NodeId`、`PortId`、`EdgeId` 及消费者可观察的稳定引用；字段改名不得通过重新生成身份来掩盖迁移。
- 迁移先在隔离副本完成结构、身份、边闭包和签名检查，全部通过后才整体提交；任一失败都返回原作者资产不变、未 Dirty、未保存、无半成品 Snapshot 的结果。
- 重复执行迁移必须幂等；未知旧版本、重复身份、断边或无法保持引用时必须阻断并保留诊断，不能静默重置到入口或删除无法识别的节点。

## Undo, rollback, and dirty notification

- 创建、连接、插入、复制、粘贴、删除、编辑和顺序调整统一进入一个受控 Undo group；事务开始前捕获权威资产状态，事务失败时回滚内存投影和身份索引。
- 只有模型验证、Snapshot/签名更新和消费者兼容检查全部成功后，才允许 Dirty/AssetDatabase 保存通知对外可见；失败路径不得留下 Dirty、保存或 Undo 历史。
- 通知顺序必须是：模型提交 -> 索引/签名刷新 -> Snapshot 或消费者投影刷新 -> Dirty/保存通知 -> UI 重绘。GraphView 重绘不能反向成为数据提交依据。

## Snapshot signature coverage

- Snapshot 签名必须覆盖所有可观察图语义：`graphId`、schema 版本、节点/端口/边稳定身份、端点方向与类型、`edge.order`、节点 Payload、消费者模式、迁移结果和生成器版本。
- 只改变画布位置、选择状态、窗口布局或其他非语义 Editor 投影数据时，不应改变业务 Snapshot 签名；若某字段会影响消费者行为，必须明确列入签名覆盖并提升签名版本。
- 签名计算必须在迁移、规范化和排序完成后进行，并将源资产版本与消费者专属产物绑定；签名漂移必须使旧 Snapshot stale，不能继续执行或自动回退另一份图。

## Verifiable Legacy prohibition

静态验证至少应断言：

1. 项目源码、asmdef、菜单注册、`link.xml` 和正式资产交付路径中不存在可编译、可注册或可运行的 `ESGraphViewWindow`、`NodeRunnerSO`、`NodeContainerSO` 等 Legacy 入口；AIWarnings、Knowledge 和合同文档中明确标注“禁止恢复”的引用不算入口。
2. Graph 运行/交付路径只接受已验证的 Stable Graph Snapshot 或消费者专属不可变产物，不读取 GraphView 元素、窗口静态状态、`SerializedObject` 或 `UnityEditor` API。
3. 验证器拒绝 Legacy 类型、缺失稳定身份、重复节点/边、非法 `edge.order`、签名覆盖不足和迁移部分成功的图包。

这些是静态合同与可验证断言；没有 Unity Test Runner、真实执行闭环、失败恢复和性能证据时，仍必须保持 `Verifying` 与 `runtime-not-run`。
