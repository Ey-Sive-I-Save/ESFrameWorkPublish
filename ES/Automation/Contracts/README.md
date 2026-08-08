# ESAutomationCenter Contracts

这些 JSON Schema 是 Python、PowerShell 和未来其他 Worker 共用的机器协议。它们不授予执行权限；权限由 C# Editor 的 `TaskRegistry`、`PathPolicy` 和当前用户授权共同决定。

## 文件

- `es-automation-task-contract.schema.json`：任务声明、输入范围、能力、超时、取消和输出合同。
- `es-automation-run-result.schema.json`：Worker 返回的结构化结果、Worker 身份、输入/输出 Hash、证据和错误。
- `es-automation-stage-result.schema.json`：分阶段 Worker 的检查点结果；Unity 只依据此文件决定是否展示已注册输入步骤或继续运行。
- `es-automation-input-response.schema.json`：由 C# Editor 规范化并写入的表单响应，回显 RunId、代次、StepId 和 SchemaHash 以拒绝过期提交。
- `es-scene-scan-report-options.schema.json`：首个 `es.scene.scan` 原型的已注册输入表单。它是固定协议，不允许 Worker 动态扩展字段或控件。
- `es-automation-ai-request.schema.json` / `es-automation-ai-response.schema.json`：本机受信 AI Bridge 的固定请求/响应信封；动作 payload 仍由 C# 按动作精确校验。
- `es-automation-python-runtime.schema.json`：项目受管 Python 解释器及可选依赖锁文件的身份与 SHA-256 锁定协议。

## 规则

- Schema 版本变化必须递增，并保留明确迁移策略。
- Worker 不得自行扩展能力名称或写入根目录。
- Worker 请求输入时必须退出并留下 `NeedsInput` 检查点；不得保持 Python 进程等待 Unity 对话框。
- C# 必须同时核对 `RunId + Generation + StepId + SchemaHash`，才可把输入响应交回下一阶段。
- AI 只能调用 C# 已注册且显式允许的 Task；`submitContentProposal` 也只能进入已注册领域内容入口，不能直接写 `Assets/`。
- `DryRun` 结果仍必须落盘报告，但不得修改业务目标目录。
- 结果 `Passed` 不等于发布通过；必须经过 C# `ReleaseGate`、受信 RunRecord 和目标平台验收证据。
