# 内容注册与事务边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.content-registration-transaction.v1`  
`Authority`: `AIWarnings` 原文与当前内容注册、GameCore、MCP 合同  
`RouteKeys`: `aiwarnings`, `p0`, `content-registration`, `transaction`, `gamecore`, `mcp-evidence`  
`HashSchema`: `v2`  
`ContentHash`: `3c9709e53c057acb5d3cfee3d16c61c4a45af2756a11493e58511536093864cb`  
`SourceSetHash`: `3c9709e53c057acb5d3cfee3d16c61c4a45af2756a11493e58511536093864cb`  
`EntryBodyHash`: `72a789a024f76c11ebbcadd0512d6adbab9dbf1aaf6ff0f918f829781115df7a`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: Warning、注册 API、提交合同、GameCore 分流、Bake 阶段、MCP 证据或任一 SourceRef 哈希变化。

## 迁移说明

Warning 保留唯一入口、CAS 预检、事务回滚、阶段隔离、MCP 证据和稳定身份边界；本条目承载详细提交字段、GameCore 分流、MCP 上游缺陷复盘、ConfigKey 冲突策略和验收矩阵。Knowledge 不授予写资产、MCP、发布或其他权限。

## 详细合同

### 提交与回滚

统一入口为 `ESContentRegistrationAuthoring.Execute(ESContentRegistrationRequest)`。所有 UI、Inspector、Drawer、MCP 和 C# 自动化必须调用它；旧 Collect API、直接追加 Page/Key、`ManualGameCoreAssets` 和 `SaveAssets()` 事务伪装均禁止。`commit=false` 预检必须返回独立 requestId、GUID、LocalFileId、revision；`commit=true` 回传同一组值。Key 迁移需 `hasExpectedCurrentKey=true` 和当前 Key CAS。并行预检不得共享资格，提交成功或 CAS 失败都消费资格，重试重新预检。未保存编辑、revision/身份变化、Key 冲突或 Bake 中必须拒绝，并恢复内存快照、尝试精确落盘回滚。

### GameCore、阶段与身份

DataInfo 用 `RegisterGameCore`，正式根用 `RegisterGameCoreRoot`，GameCore 禁止进入普通 AssetTable。未定义正式事务的移动、复制、合并、清空和移除禁用。注册、Bake、规划、构建、发布独立；Bake 读取源期间冻结正式提交，Windows Mutex 只覆盖同机竞争。主资产身份为 GUID + LocalFileId(0)，子资产保存真实 LocalFileId；ConfigKey 定义/引用用 `ESConfigKeyUsage(Declaration)` 区分，同强类型 EnumKey/StringKey 占用必须阻止写入并让用户选择。

### MCP 证据门禁

Unity `HandleCommand`、`CommandRegistry` 或元数据只能证明适配器存在，不能证明客户端暴露。正式 MCP 闭环需同一客户端 `tools/list` 发现 `es_content_registration`（或精确工具资源），完成 commit=false/true 与幂等重放，返回 revision/GUID/LocalFileId/Stable Key，且 Server 日志无动态注册失败。上游 `mcpforunityserver` 10.0.0–10.1.3 beta 在 FastMCP 3.x 动态签名生成存在 `__annotations__`/`KeyError` 缺陷；缺陷解除前只能报告 Handler/C#/静态证据，MCP 客户端层为阻断，禁止删参数元数据、增加第二 bootstrap、改名参数或用 legacy TCP 冒充。

### 最低验收

定向编译 ES_Stand、ES_Editor、内容注册测试和 MCP 适配程序集；Unity 导入/ReloadDomain；EditMode 覆盖普通资产注册、Key CAS、GameCore、Root、Consumer、MCP、幂等和失败拒绝；MCP 还需真实客户端发现与调用；多 Unity 实例必须锁定精确 `Name@hash`，不得借用其他项目证据。

## 原文快照

迁移前完整 Warning（66 行、5371 字节）由以下 SourceRef 保留，原始 SHA-256 为 `341c140c6745bacedaae0a0efb5d15e7b3e6f577ddcf13d7420c11c92ab071f7`。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_统一内容注册唯一入口与事务边界_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_统一内容注册唯一入口与事务边界_AI协作警告.md` (`93365ba8696696d492931a3376b3aee5877e27b93b48bf4ea8944ab9343ca9eb`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`01ade9352cbd5a518e46b0f61ccc53aa049e4733708be13bf4ba5dfc93e60d07`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-content-registration-transaction.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
