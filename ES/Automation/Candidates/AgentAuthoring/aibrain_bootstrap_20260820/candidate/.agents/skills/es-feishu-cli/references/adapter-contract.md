# Feishu Adapter 合同

首批操作：

```text
auth status
knowledge search
knowledge pull
knowledge publish --dry-run
message send --confirm
```

所有操作必须经过 ESAutomationCenter 注册、TaskContract、Facade 和 RunRecord。凭据只来自安全存储；输出必须包含 RunId、状态、退出码、输入/输出 Hash 和错误。

Feishu 不是 ES 源事实。同步回 AIKnowledge 时必须保存外部来源、抓取时间、版本或内容哈希，并设置 stale 条件。
