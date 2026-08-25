# 受管 AIKnowledge 更新 AI 命令

## 直接生效协议

当前用户明确要求的有界 Knowledge 条目、KnowledgeIndex、AIBRAIN_ENTRY 或 SourceRef/路由投影修改可直接执行；`.agents/skills/es-skill-governance/scripts/Test-ESUserDirectedLowRiskPolicy.ps1` 只验证声明范围闭合。只有选择 AIBrain/Worker 受管通道时，才必须提供匹配的 `planTask`、TaskContract 和 SourceRef 校验回执；这些输入不是用户指令的二次审批。没有当前用户指令时，AI 自主路径只能输出候选或建议，不得修改正式权威。

命令类型：安全执行：受控知识写入。
默认改文件：允许用户明确声明的 `Documentation/AIKnowledge/entries/<entry>.md` 和对应索引投影。
风险等级：L2。

## 必须先读

```text
.agents/skills/es-knowledge-creator/SKILL.md
.agents/skills/es-skill-governance/SKILL.md
Documentation/AIKnowledge/KnowledgeIndex.yaml
Documentation/AIKnowledge/AIBRAIN_ENTRY.md
```

## 执行要求

```text
1. 每条事实必须绑定当前源码或证据 SourceRef 和 SHA-256。
2. Authority、EvidenceLevel、StaleWhen、RouteKeys、ContentHash 必须完整。
3. 不得把 Skill 摘要、模型推断或 Runtime 未执行写成源码事实。
4. 只修改当前用户声明或受管计划绑定的 Knowledge 条目、KnowledgeIndex、AIBRAIN_ENTRY 与 AIKnowledge 路由投影；本命令不得自行引申修改 Assets、运行时代码、Skill 正文或 AICommand Catalog。
5. 写入前执行 UTF-8、SourceRef、ContentHash 和 bounded-output 检查；失败则拒绝写入。
6. 旧条目发生源漂移时生成 replan/stale 报告，不覆盖冲突事实。
```

## 交付格式

```text
1. 已读来源
2. 目标条目与范围
3. SourceRef/ContentHash 验证结果
4. 写入文件
5. 未证明的 Runtime 或外部事实
```

## 禁止事项

不得伪造证据、删除冲突条目、写入秘密，或在没有当前用户指令时自主扩大输出范围；选择受管通道后不得绕过 AIBrain/Facade 协议。
