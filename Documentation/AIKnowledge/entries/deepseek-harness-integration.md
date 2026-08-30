`KnowledgeId`: `es.deepseek.harness.integration.v1`
`Authority`: `Derived from current ES DSH Worker, TaskContract and AIWarnings`
`RouteKeys`: `automation, worker, external-agent, deepseek, harness, authority, evidence, editor, skill, knowledge`
`EvidenceLevel`: `S1`
`ContentHash`: `59008ab329fa980a273496c026751afdd85bed097be46418692b6f50dc69df47`
`StaleWhen`: `DSH Worker identity, TaskContract, runtime installer/checker, authority declaration, AIWarnings Automation/Editor rules or SourceRef hashes change`

`ProviderDeclaration`: `es-deepseek`

## SourceRefs

- `ES/Automation/Contracts/es-deepseek-integration-declaration-v1.json` (`13a472937bf2f015e718c233531fff70aeb5621751ab603c8fae892244573cd9`)
- `ES/Automation/Workers/Node/DeepSeekHarness/worker-manifest.json` (`00fa51f908cc8484d115c2cd09921f5598fed07d203fe53477dc4cc0a5b1aab4`)
- `ES/Automation/Workers/Node/DeepSeekHarness/package-lock.json` (`d1eeae12f90a7593575101c33b002dc5803aae4d60d778156aff7f9deb051c59`)
- `ES/Automation/Workers/Node/DeepSeekHarness/worker.js` (`2b840aa7c441ded006b44e87d32537ba0c51f58740f7b045244d07f98f755749`)
- `ES/Automation/Contracts/es-deepseek-harness-v1.schema.json` (`947d8cb2c2d1d6b1d4e1ba4c43e5899ca1305a07ccaa0d2b28cae6b93368fba4`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESDeepSeekHarnessAutomation.cs` (`b26804b672f9324d6e05f686d57ace5ac1f03490f59fecb3445b5afafdb7ca4c`)
- `Assets/Plugins/ES/AICommands/DeepSeekHarness受管开发_AI命令.md` (`2c03a53f4995c2f8dd0d532fa3c8e9cc3609453434fce1ce89cf6c308f9e90a6`)
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json` (`b21fe90d9333ea33e187400eb9e580be9678e06401cfd70facb4416bdc699208`)
- `ES/Automation/Workers/Node/DeepSeekHarness/Install-ESDeepSeekHarness.ps1` (`de4836ff590cfac3f6ecff19e10a75ef8f355abfba0ac89f51fbfd8711d53031`)
- `ES/Automation/Workers/Node/DeepSeekHarness/Test-ESDeepSeekHarness.ps1` (`a9176ee6073957e0e1761767352a6db78d7948583c1fa7bde07ccfa6780a6ae8`)
- `ES/Automation/Workers/Node/DeepSeekHarness/Test-ESDeepSeekHarnessUi.ps1` (`b1a0c293b84d9c933124481b10ef1701e742e2fb6c29c614c496155a8fab5b49`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`6f7998bac62c988384030ea434dc1166d0b5fa11c05f880baf6705321ea27485`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`c47dddf3f5110cb279ebb0f1ec963109d87b93692f4b9241ecda706ff138642a`)

## 当前可验证事实

- ES 注册 `deepseek.harness.execute` AICommand 与 `es.deepseek.harness@1`，Worker 允许 `dry-run`、`check-local` 和 `headless-prompt` 三类操作；Adapter 的 `collect-receipt` 通过 ES `getRun` 读取已有 RunRecord，不启动新的 DSH 进程。
- Worker 类型是受 C# Editor 注册的 `Other`，入口、版本、输入 Schema 和输出路径由 TaskContract 绑定；调用方不能提交解释器、脚本或任意命令行。
- `check-local` 只检查绝对 Node、项目内 `dsh.cmd`、`package-lock.json`、受管 Profile/DSH_HOME 和可选凭据存在性；凭据值不会被读取到报告中。
- `dry-run` 不启动 DSH、不访问网络、不修改项目资产；真实 headless 调用必须先通过 ES 本地接入状态检查。
- DSH 的职责是高权威开发贡献层：提供分析、实现候选和 Agent Loop；ES 仍拥有任务授权、路径边界、证据、恢复和最终完成判定。
- DSH 绑定的 Skill/Knowledge/合同必须声明 `ProviderDeclaration: es-deepseek` 或等价机器字段，避免把普通 AI 路由误认为 DSH 能力。

## 接入流程

1. 从项目根执行 `ES/Automation/Workers/Node/DeepSeekHarness/Install-ESDeepSeekHarness.ps1`。
2. 设置 `DEEPSEEK_API_KEY`（只在本机环境中提供，不写入仓库、请求或日志）。
3. 在 Unity 打开 `【ES】/自动化与开发/自动化中心/打开自动化中心`，查看 DSH 图标状态并点击“检查 DSH 链路”。
4. 选择 `deepseek.harness.execute`，先执行 `dry-run`；只有状态为 `Connected` 时才允许 `headless-prompt`。
5. 通过 `getRun` 读取 RunRecord/结构化输出；ES 根据证据决定 `Completed`、`Blocked` 或 `Failed`，不能把 DSH 文本直接当作 Accepted。

## 失败面与恢复

| 失败面 | 触发/症状 | 预防检查 | 恢复动作 | 当前证据 |
|---|---|---|---|---|
| 运行时缺失 | Node、dsh.cmd 或 lock 缺失 | `check-local` 和 Adapter 启动前复核 | 重新运行安装脚本，保持 `NotConnected` | Worker/Adapter 源码 |
| 凭据缺失 | Provider 返回未配置 | 只检查环境变量存在性 | 设置本机环境变量后重新检查，不把值写入文件 | Worker 输出合同 |
| 源漂移 | 入口或 Schema Hash 不匹配 | C# Worker 注册和结果身份校验 | 锁定版本并重新安装/重算哈希 | TaskContract/RunResult |
| 权威倒置 | DSH 直接写 Assets 或声称完成 | `ExternalWrite`/Assets 写入未授予，ES 完成权威固定 | 拒绝调用，回到 ES TaskContract | AIWarnings Automation |
| 进程中断 | 超时、域重载或 Unity 退出 | ProcessRunner 超时、进程树终止和 RunRecord | 标记 `Failed`，不重复猜测恢复 | C# Adapter |

## 未证实项

- 当前本机未证明 `npm install` 后的 DSH Provider、网络和真实 headless 调用成功。
- 本条目不证明 Unity 已完成导入、编译、ReloadDomain、窗口显示或 PlayMode 行为；这些属于独立 Runtime 证据。
- 官方 DSH 版本行为需以本项目锁定的包、依赖锁和入口哈希为准，不能仅凭在线文档替代本地版本验证。
