# 受管 Skill 候选创建 AI 命令

## 直接生效协议

读取全文后，必须先经 AIBrain `planTask` 和匹配的 TaskContract。此命令只生成候选，不代表正式 Skill 已注册或已发布。

命令类型：候选内容生成。
默认改文件：仅允许 `ES/Automation/Candidates/AgentAuthoring/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

```text
.agents/skills/es-skill-creator/SKILL.md
.agents/skills/es-skill-governance/SKILL.md
.agents/skills/es-knowledge-creator/SKILL.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
```

## 执行要求

```text
1. 只读取 GenerationSpec 声明的输入和 Reference。
2. 只在候选目录生成 SKILL.md、agents/openai.yaml、governance.json、验证报告和 candidate-manifest.json。
3. 不写入正式 .agents/skills，不修改 AICommand Catalog，不注册运行权限。
4. candidate-manifest.json 必须声明候选路径、正式目标路径、Skill 哈希和验证状态。
5. UTF-8、frontmatter、引用闭包和静态回放失败时停止，不得猜测修复。
6. 正式注册必须另行经过 Skill Creator、Skill Validator 和 Registry 更新。
```

## 交付格式

```ContractCompleteness
commandId: skill.create.candidate
cancellation: before commit; cancel leaves no formal Skill
recovery: isolated candidate cleanup; NeedsReissue on uncertain state; no replay
validation: candidate schema, content hash, and isolated-path checks
evidenceRef: candidate path, SHA-256, receipt, and Static/Runtime status
allowRoots: ES/Automation/Candidates/AgentAuthoring/<request-id>/candidate/ only
denyPaths: .agents/skills, Assets/Plugins/ES/AICommands, Assets, Runtime, Git, release
deny-overrides: true
```

```text
1. 已读规则
2. 候选目录和目标映射
3. 静态验证结果
4. 尚未注册/尚未发布的明确声明
5. 未验证的 Runtime 声明
```

## 禁止事项

不得直接修改正式 Skill、Git、Unity 状态、外部系统或启动任意 Worker。
