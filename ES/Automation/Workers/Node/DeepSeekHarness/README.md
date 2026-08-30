# ES · DeepSeek Harness 一步接入

该目录是 ES 的受管 DeepSeek Harness 能力入口。DSH 是高权威开发贡献层，负责分析、实现候选和 Agent Loop；ES Automation/AIBrain 负责任务授权、路径限制、证据、恢复和最终完成判定。

## GitHub 拉取后的一步接入

前置条件：本机已有 Node.js 22+ 和配套 npm。项目不会静默安装全局 Node、修改 PATH 或使用用户默认 `~/.dsh`；如果 `node.exe` 不在 PATH，可在同一条安装命令中传入 `-NodePath <绝对路径>`。

在项目根 PowerShell 执行一次：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ES\Automation\Workers\Node\DeepSeekHarness\Install-ESDeepSeekHarness.ps1
```

安装脚本会：

- 使用显式或当前机器发现的 `node.exe`，执行受管 `npm install`；
- 在 Worker 目录生成/更新依赖锁和 `node_modules`；
- 在被 `.gitignore` 忽略的 `ES/Automation/Temp/DeepSeekHarness/runtime.local.json` 保存本机运行时路径；
- 创建隔离的 DSH_HOME 和工作区；
- 不保存、不打印 `DEEPSEEK_API_KEY`。

真实调用前，在当前 PowerShell 会话设置凭据：

```powershell
$env:DEEPSEEK_API_KEY = '<仅本机环境变量，不要写入仓库>'
```

然后检查链路：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ES\Automation\Workers\Node\DeepSeekHarness\Test-ESDeepSeekHarness.ps1 -RequireProvider

powershell -NoProfile -ExecutionPolicy Bypass -File .\ES\Automation\Workers\Node\DeepSeekHarness\Test-ESDeepSeekHarnessUi.ps1
```

输出为 `status=Connected` 才表示本机链路已接入；任何失败都会输出 `status=NotConnected`、`reasonCode` 和最小恢复动作。检查脚本不会调用 Provider API。

## Unity 入口

打开：`【ES】/自动化与开发/自动化中心/打开自动化中心`。

首屏的 DSH 图标显示：

- `DSH · 已接入`：本地 Node、DSH CLI、Profile、依赖锁和 Provider 凭据存在性均通过；
- `DSH · 未接入`：显示原因和恢复动作（通常是缺少 `DEEPSEEK_API_KEY`）；不会把源码存在或旧回执当作接入成功。

可执行的受管任务是 `es.deepseek.harness@1`：

- AIBrain 受管入口命令：`deepseek.harness.execute`；必须先选中该 AICommand，再执行 `planTask -> runTask`。

- `dry-run`：不启动外部进程、不访问网络；
- `check-local`：检查本地链路和可选凭据存在性；
- `headless-prompt`：经 ES TaskContract 和受信 Adapter 启动 DSH headless。

Adapter 还提供 `collect-receipt` 语义：通过 ES `getRun` 读取 RunRecord/结构化收据，不启动新的 DSH 进程；它不是 Worker 的第四种外部操作。

## 权威边界

```text
ES 用户授权 / AIBrain 计划
  -> ESAutomation TaskContract / PathPolicy
  -> 受信 DSH Adapter
  -> DSH Agent Loop / Provider
  -> ES RunRecord / Evidence
  -> ES CompletionDecision
```

DSH 输出是候选结果，不得直接写入 `Assets/`、扩大项目路径、输出凭据或自报 ES `Accepted`。任何与 DSH 强绑定的 Skill、Knowledge 或合同都必须声明 `es-deepseek`。

## 典型流程

1. 安装脚本完成本机 DSH 运行时准备。
2. `Test-ESDeepSeekHarness.ps1` 检查 Node/DSH/Profile/依赖锁；缺凭据时报告未接入。
3. ES 首先执行 `dry-run`，确认输入、路径和能力边界。
4. 状态为 `Connected` 后，ES 才允许 `headless-prompt`。
5. DSH 返回候选分析/代码建议，ES 记录 RunRecord、输出 Hash 和错误。
6. ES 根据当前规则和证据决定 `Completed`、`Blocked` 或 `Failed`；DSH 不拥有最终验收权。

## 故障恢复

- `RUNTIME_CONFIG_MISSING` / `RUNTIME_CONFIG_INVALID`：必须重新运行一步安装脚本；检查器不会因目录中残留的 node_modules 或旧回执而判定已接入。
- `RUNTIME_PATH_INVALID`：检查 runtime.local.json 的 DSH_HOME、workspace 是否为项目根内绝对路径，禁止改成用户默认目录或项目外路径。
- `PACKAGE_LOCK_MISSING`：重新运行一步安装脚本，依赖未冻结前保持未接入。
- `NODE_UNAVAILABLE`：传入 `-NodePath <绝对 node.exe>`，不会回退 PATH。
- `DSH_UNAVAILABLE`：确认安装完成且存在 `node_modules/.bin/dsh.cmd`。
- `PROVIDER_CREDENTIAL_MISSING`：只在本机环境设置 `DEEPSEEK_API_KEY`，重新检查，不写文件。
- `SOURCE_DRIFT` 或 Worker 结果身份错误：停止调用，重新安装并重新验证版本/Hash。
- 超时、域重载或 Unity 退出：RunRecord 保守标记失败，不猜测远端状态，不自动重试。

## 证据边界

静态检查只证明文件、配置和合同闭合；它不证明 Unity 编译、ReloadDomain、Provider 网络调用、PlayMode、Player 或发布验收。当前 DSH 版本、依赖锁和入口 Hash 发生变化时，旧接入状态自动视为 stale。
