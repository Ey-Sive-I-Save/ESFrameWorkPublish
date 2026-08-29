# P0：统一内容注册唯一入口与事务边界

`Status`: `current`
`StableId`: `es.aiwarning.p0.content-registration-transaction.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `content-registration`, `transaction`, `gamecore`, `mcp-evidence`
`Applicability`: 普通资产、GameCore DataInfo/Root、Consumer 同步、Catalog/ReferenceGraph Bake、Editor/MCP/C# 注册调用。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-content-registration-transaction.md`
`StaleWhen`: 注册 API、提交合同、GameCore 分流、Bake 阶段、MCP 证据或 SourceRefs 变化。

## 长期 P0 约束

- 所有作者态注册统一调用 `ESContentRegistrationAuthoring.Execute(ESContentRegistrationRequest)`；窗口、Inspector、ConfigKey Drawer、MCP 和自动化只能组装同一请求。禁止直接改 Page/Key、写 `ManualGameCoreAssets`、调用旧 Collect API 或以 `SaveAssets()` 冒充事务。
- 先 `commit=false` 预检，再以同一 `requestId`、GUID、LocalFileId、revision 执行 `commit=true`；Key 迁移必须 CAS 当前 Key。每次真实提交（成功或 CAS 失败）消费资格，重试必须重新预检。
- StringKey 按原值保存，不 Trim、不改大小写、不静默生成；未保存编辑、revision 改变、身份不符、Key 冲突或 Bake 进行中必须拒绝写入，并恢复内存快照、尝试精确回滚。
- GameCore DataInfo 用 `RegisterGameCore`，正式根用 `RegisterGameCoreRoot`；GameCore 不得进入普通 AssetTable。未定义正式事务的移除/移动/复制/合并/批量清空必须禁用。
- 注册、Bake、规划、构建、发布严格分阶段；注册成功不等于 Bake，静态编译不等于 PlayMode/发布。Bake 期间冻结正式提交；Mutex 只解决同机竞争，不是分布式锁。
- Unity Handler/CommandRegistry/元数据存在不等于 MCP 客户端可用。MCP 必须真实 `tools/list` 发现、同一客户端完成预检/提交/幂等重放、日志无动态注册失败；上游缺陷期间 MCP 客户端层标为阻断，禁止 legacy TCP 冒充。
- 主资产身份为 GUID + LocalFileId(0)，子资产使用真实 LocalFileId；ConfigKey 定义/引用分层并检查强类型占用，冲突必须阻止写入、由用户明确选择，禁止覆盖或静默合并。

详细提交合同、GameCore 分流、MCP 失败复盘、稳定身份和验收矩阵见 Knowledge：`es.aiwarning.p0.content-registration-transaction.v1`。
