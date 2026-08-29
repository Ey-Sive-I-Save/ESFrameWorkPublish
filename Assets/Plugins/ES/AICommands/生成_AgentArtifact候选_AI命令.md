# 生成 Agent Artifact 候选

本命令用于读取 Agent Authoring Graph 烘焙出的 GenerationSpec，在隔离目录生成可审查的 AICommand 或 Agent Skill 候选。它不授权直接修改正式 AICommands、`.agents/skills`、运行时代码、Git 或发布状态。

命令类型：候选内容生成。
默认改文件：仅允许当前请求声明的 `ES/Automation/Candidates/AgentAuthoring/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

```text
.agents/skills/es-generate-agent-artifacts/SKILL.md
.agents/skills/es-generate-agent-artifacts/references/generation-contract.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
```

还必须读取 GenerationSpec 中所有 `required=true` 的 Reference。缺失路径、冲突规则或目标不明确时，停止生成并在 `validation-report.md` 中报告，不得猜测。

## 输入

请求目录必须包含：

```text
generation-request.json
generation-prompt.md
candidate/
```

以 `generation-request.json` 中的 Goal、Reference、Constraint、OutputArtifact、Validation 和 Relations 为唯一生成输入。不得擅自扩大目标文件范围。

## 执行要求

1. 只在当前请求的 `candidate/` 下创建候选。
2. AICommand 候选必须包含命令类型、默认改文件、风险等级、必须先读、执行要求和交付格式。
3. Agent Skill 候选必须使用 `.agents/skills/es-*/` 标准结构，至少包含 `SKILL.md` 和 `agents/openai.yaml`。
4. Skill 的 `SKILL.md` frontmatter 只包含 `name` 和 `description`。
5. 不得生成第二套运行时、隐藏 DLL、会话日志或自动批准逻辑。
6. 所有文本严格 UTF-8，无 BOM、无 U+FFFD。
7. 生成 `candidate-manifest.json`，逐文件声明候选路径、正式目标路径、产物类型和摘要。
8. 生成 `validation-report.md`，区分已执行、未执行和需要人工确认的验证。
9. 不得把生成完成描述成已经写入正式目录或已经通过人工批准。
10. 必须沿 Relations 从每个 OutputArtifact 回溯其 Goal、Reference 与 Constraint，保持思路图归属；禁止把所有节点无差别扁平拼接。
11. AICommand 必须使用 Graph 声明的预期输入、执行步骤与验收标准；Agent Skill 必须使用触发场景、非目标、工作流和验证步骤。
12. Relations 只表达需求组织，不得被解释为运行时执行顺序、第二套 Runner 或自动授权。

## candidate-manifest.json

```json
{
  "schemaVersion": 1,
  "requestId": "来自 generation-request.json",
  "summary": "候选包摘要",
  "files": [
    {
      "artifactKind": 0,
      "candidateRelativePath": "candidate/generated-command.md",
      "targetProjectPath": "Assets/Plugins/ES/AICommands/生成_示例_AI命令.md",
      "summary": "AICommand 候选"
    }
  ]
}
```

`artifactKind`：`0` 表示 AICommand，`1` 表示 Agent Skill。Agent Skill 的每个文件都必须单独列入 Manifest。

## 交付格式

```ContractCompleteness
commandId: agent-artifact.candidate
cancellation: before commit; cancel leaves no formal artifact
recovery: isolated candidate cleanup; NeedsReissue on uncertain state; no replay
validation: candidate schema, content hash, and isolated-path checks
evidenceRef: candidate path, SHA-256, receipt, and Static/Runtime status
allowRoots: ES/Automation/Candidates/AgentAuthoring/<request-id>/candidate/ only
denyPaths: .agents/skills, Assets/Plugins/ES/AICommands, Assets, Runtime, Git, release
deny-overrides: true
```

```text
1. 已读取：列出 AICommand、Skill、Graph Reference。
2. 候选目录：给出唯一请求目录。
3. 候选文件：逐项列出 candidate 与 target。
4. 验证结果：列出已运行和未运行的验证。
5. 人工确认：明确尚未批准、尚未写入正式目录。
```

## 需求

```text
读取当前 Agent Authoring generation-request.json，严格按 GenerationSpec 生成候选包。
```
