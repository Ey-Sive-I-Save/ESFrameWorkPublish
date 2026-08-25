# ES Task Closeout Contract

## Purpose

定义任务结束时的最小中文摘要。AI 在会话启动或能力刷新时读取一次并缓存；除非本合同哈希变化，不得每个任务重复读取本文件。

## Trigger

任务涉及 Skill、写入、验证、交接或完成声明时必须输出；普通问答可省略。

## Required output

```text
🧩 使用Skill：<名称或无>
✍️ 写入：<范围或无>
🧪 运行时：<已运行/未运行/不适用>
⚠️ 未证实：<关键未证明项或无>
📊 证据评价状态：<aligned/partial/misaligned/unverifiable>
🔎 观察证据：<用户消息/AI消息/工具/变更/验证/纠正计数>
🚧 发现：<证据评估器 findings 或无>
🎯 提示评分：<仅填 evidence evaluator 的真实结果；无则“不可用”>
🔍 验证评分：<仅填 evidence evaluator 的真实结果；无则“不可用”>
🧭 目标清晰度：<清晰/部分清晰/不清晰>
📌 下一步：
1. <动作一>
2. <动作二>
3. <动作三>
```

`📌 下一步` 必须是用户可直接回复序号的菜单。最多三项；不存在的项省略。即使只有一个动作，也必须写成 `1. <动作>`，不得使用未编号的自由文本。序号只绑定当前收尾菜单，不授予写入、Runtime、网络或外部执行权限。

`📊`、`🔎`、`🚧` 是主结论；数字评分是可选投影，不得用手工估计替代 evidence-first 结果。最终收尾应由 `es-ai-interaction-governance/scripts/Invoke-ESInteractionCloseout.ps1` 提供状态、观察计数、发现和 non-claims。

`📌 下一步` 必须是最终用户可见摘要的最后一个字段。其后的任何文本都会使收尾合同失效；内部 JSON 的 `nextAction`/`nextSteps` 字段不改变这一呈现顺序要求。

## Boundary

本摘要不是验收收据，不替代 Skill 验证器、Runtime 收据、交接文件或发布报告。详细字段只在对应 Skill 被触发时读取。

## Cache binding

缓存键：`task-closeout-contract`；缓存必须绑定本文件 SHA-256。会话恢复、Skill 刷新或哈希变化时重新读取。
