---
name: es-game-logic-system-development
description: "Use when designing, implementing, integrating, or validating ESFramework game-logic runtime systems such as Entity domains, input/control, commands, state transitions, combat contracts, movement, pooling, GameCore, and ResourcePlan. It enforces project-authority reads, ABCDN planning, existing-entry reuse, bounded writes, and separated static/runtime evidence."
---

# ES 游戏逻辑系统开发

## 适用范围

面向“系统能力”而非具体内容：Entity/Domain 生命周期、输入与控制权、Command/Playable、状态与属性协议、Combat/Movement 入口、对象池、GameCore、ResourcePlan、运行时错误恢复和跨模块契约。

## 强制读取链

开始任何设计或修改前，完整读取：

1. 项目根 `AGENTS.md`、`.agents/README.md`；
2. `ES/AISpace/README.md`、`AISPACE_AUTHORITY.json`；
3. AIWarnings Start/CurrentStatus/RuleIndex 与命中的 P0/领域规则；
4. `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`KnowledgeIndex.yaml`，按对象选择 1–3 条并读取 `requiredReads`、正文和 SourceRefs；
5. 目标模块源码、相邻测试、当前 Skill references；禁止以索引或摘要代替权威正文。

不得先写代码再补读文档；不得因“已有组件很多”推断系统已可运行。

## 知识意向（可选）

开始时让用户选择或由任务语义唯一命中：`entity-lifecycle`、`input-control`、`command-runtime`、`combat-contract`、`movement`、`state-stats`、`pool-recovery`、`gamecore`、`resource-runtime`、`performance`、`integration-validation`。意向只缩小读取集合，不授予额外权限；无匹配时报告 `NoKnowledgeRoute` 并回读权威源码。

## ABCDN 工作流

- **A Architecture**：列出唯一权威、所有者、依赖方向、身份和禁止旁路。
- **B Behavior**：描述输入/请求→状态/命令→唯一执行入口→结果→反馈→清理的生命周期。
- **C Cost/Compatibility**：检查分配、并发、池化、资源 Scope、版本兼容和性能预算。
- **D Defense/Evidence**：用反例挑战取消、打断、失败、超时、禁用、回池、重入和权限越界。
- **N Navigation/Next**：给出最小实施批次、可回滚点、验证命令和下一步菜单。

## 既有支持优先

攻击、扣血、属性、位移、状态机、对象池和资源 Provider 默认视为稳定权威。先寻找现有接口并接入；不得复制简化系统、直接改 HP/Stats/Transform/KCC、绕过 EntityAIDomain、绕过 Command Runner 或建立第二个 GameCore/ResourcePlan 权威。

## 写入与验证

默认只读/设计模式。用户明确授权实现后，只写任务声明的文件和范围；保持单一写入者、幂等、取消和恢复。静态验证与 Unity/PlayMode/Profiler/Player 分离，未运行时必须标记 `runtime-not-run`。完成声明必须列出事实、假设、未证实项、阻断、diff 和证据 receipt。

## 会话任务分类约束

临时职责提示可放在 `ES/AISpace/Local/CodexSessionTasks/<YYYYMMDD>/<responsibility>/`，必须标记为 `temporary-task`，不得称为 Handoff、历史归档或长期权威；正式交接由 `es-codex-session-bootstrap` 管理，不在本 Skill 内复制会话协议。

## Engineering controls

本 Skill 采用 Engineering tier：要求风险登记、StaticDeepReplay、权限边界、可恢复执行和分层证据。它不自动授予 Unity、Runtime、Git、网络或发布权限；任何运行时动作必须由用户明确授权并带停止条件。

## Skill 使用披露

遵循项目根 `AGENTS.md` 与 `.agents/README.md` 的披露规范；实际使用本 Skill 时，在首次进度更新和最终答复说明其职责。披露不是授权或验收证据。
