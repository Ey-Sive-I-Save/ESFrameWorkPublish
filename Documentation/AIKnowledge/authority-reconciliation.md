# AIBrain 与 AIWarnings 权威对账

状态：治理迁移第一阶段，保留兼容入口。

## 当前裁决

| 现有入口 | 当前职责 | 处理 | AIBrain 关系 |
|---|---|---|---|
| `Assets/Plugins/ES/AIWarnings` | P0、长期事实、禁止事项、证据标准 | `keep` | 执行前按 routeKeys 读取，不作为用户主要启动界面 |
| `Assets/Plugins/ES/AICommands` | 受管通道单次任务协议 | `keep` | AIBrain 选择，只约束该通道，不得扩大或缩小用户范围 |
| `.agents/skills` | 可复用执行工作流 | `keep` | AIBrain 定向路由；自主候选不能正式导入，当前用户明确要求正式写入时可直接实施 |
| `Assets/Plugins/ES/Editor/ESAutomation` | 注册任务、受管 Worker、RunRecord | `keep` | AIBrain 唯一执行协调边界 |
| `Documentation/AIKnowledge` | 定向检索索引和派生摘要 | `adopt` | AIBrain 的查询层，不拥有源事实 |
| `.zread/wiki` | 自动生成代码导航缓存 | `defer` | 只作为低优先级导航，不进入 P0 |
| Feishu | 外部协作/同步出口 | `defer` | 必须通过 ESAutomationCenter，不作为事实源 |

## 保留规则

本轮不删除、不移动、不重写 AIWarnings 现有文件。若后续要调整其启动说明，必须先重新审计现有并行修改并维护兼容指针；AIBrain 的新增入口不能使旧 AIWarnings 入口失效。

## 尚未完成

- AIBrain 启动器本身尚未实现。
- Unity Agent Authoring Graph 尚未重新 Bake 本请求。
- Feishu 适配器尚未注册 TaskContract。
- Knowledge 条目当前为人工维护，尚无自动 stale 扫描。
