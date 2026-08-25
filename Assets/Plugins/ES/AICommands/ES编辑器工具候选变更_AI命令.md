# ES 编辑器工具候选变更 AI 命令

## 直接生效协议

读取全文后，必须先经 AIBrain `planTask`、编辑器 TaskContract 和 ES 兼容性静态验收。此命令只生成候选变更，不直接修改正式 Assets。

命令类型：候选内容生成：ES 编辑器工具候选变更。
默认改文件：仅允许 `ES/Automation/Candidates/EditorTooling/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

```text
.agents/skills/es-editor-tooling/SKILL.md
.agents/skills/es-editor-availability-validator/SKILL.md
.agents/skills/es-skill-governance/references/static-specialized-acceptance.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
```

## 执行要求

```text
1. 保留现有 ES 窗口基类、Facade、Section、Dialog 和生命周期入口。
2. 变更必须声明 targetKind、singletonPolicy、尺寸边界、滚动所有权、ownerKey、Unbind 和恢复策略。
3. 先执行 StaticDeepReplay：正常、非法、越界、幂等、缓存失效、恢复和确定性输出。
4. 只在候选目录生成差异、验证报告和 candidate-manifest.json。
5. 不直接打开 Unity、不运行游戏、不修改正式 Assets；Runtime 由独立授权阶段处理。
6. 候选报告必须分别列出 staticStatus、runtimeStatus、claimsNotProven。
```

## 交付格式

```text
1. 候选目录和差异摘要。
2. StaticDeepReplay 已执行案例及结果。
3. runtimeStatus、claimsNotProven 和待授权项。
4. 明确声明正式 Assets 尚未修改。
```

## 禁止事项

不得创建第二套窗口生命周期、绕过 ESAutomationFacade、猜测视觉 Runtime 结果或把静态 Ready 写成用户可用。
