---
name: es-game-logic-content-development
description: "Use when producing ESFramework gameplay content on top of existing logic systems: character variants, weapons, skills, enemies, encounters, missions, level rules, tuning, and content data. It enforces authority reads, optional Knowledge intent, ABCDN content planning, stable-state protection, and evidence-backed integration."
---

# ES 游戏逻辑内容开发

## 适用范围

面向“内容实例”而非底层系统：角色/敌人 Variant、武器、技能序列、目标、关卡遭遇、任务规则、数值配置、表现绑定、资源分组和内容验收。

## 强制读取链

每项内容开始前，完整读取：

1. 项目根 `AGENTS.md`、`.agents/README.md`；
2. `ES/AISpace/README.md`、`AISPACE_AUTHORITY.json`；
3. AIWarnings Start/CurrentStatus/RuleIndex 与命中的 P0/领域规则；
4. `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`KnowledgeIndex.yaml`，读取匹配条目的 `requiredReads`、正文和 SourceRefs；
5. `CHARACTER_PREFAB_CONTRACT.md`、目标内容类型的 DataInfo/Prefab/Config/Resource/State/Combat 权威文档和相邻测试；
6. 本 Skill references 与实际源码入口。

## 知识意向（可选）

可选意向：`character-variant`、`weapon-content`、`skill-sequence`、`enemy-content`、`encounter-level`、`mission-rule`、`tuning-stats`、`presentation-binding`、`resource-registration`、`content-validation`。意向只导航知识，不替代源码和契约；多命中必须消歧。

## ABCDN 内容工作流

- **A Authority**：确认内容身份、DataInfo、Prefab、ConfigKey、Consumer、ResourcePlan 和唯一运行时入口。
- **B Behavior**：把内容意图映射到既有输入/AI/Command/Equipment/State/Combat/Movement 链，明确成功、失败、取消、打断和重入。
- **C Content/Cost**：检查依赖、资源 Scope、池化、LOD、预算、数值边界、兼容和回退内容。
- **D Defense**：反驳“文件存在即可用”“Prefab 可实例化即已接入”“属性可直接改”“演示脚本可替代正式系统”等错误推断。
- **N Next**：输出最小内容包、配置顺序、验收矩阵、回滚点和后续菜单。

## 稳定系统保护

攻击、扣血、属性、位移、状态机、对象池、VFX/Audio/UI 和资源 Provider 默认只调用不重写。内容 Skill 不得增加同名系统、直接写 HP/Stats/Transform/KCC、在场景脚本中塞玩法逻辑，或把实验室/Demo 资产直接当正式内容。

## 写入与验证

默认只读/候选模式。用户明确授权后，按内容包声明写入 DataInfo、Prefab、配置、资源注册或 AISpace 投影；保持稳定身份、依赖可追踪、重复执行幂等、失败可恢复。静态、Unity 编译、PlayMode、视觉、Profiler、Player 证据分开；未运行必须标记 `runtime-not-run`。

## 会话任务分类约束

临时职责提示可放在 `ES/AISpace/Local/CodexSessionTasks/<YYYYMMDD>/<responsibility>/`，必须标记为 `temporary-task`，不得称为 Handoff、历史归档或长期权威；正式交接由 `es-codex-session-bootstrap` 管理，不在本 Skill 内复制会话协议。

## Engineering controls

本 Skill 采用 Engineering tier：要求内容身份、依赖、风险、供应链、可恢复执行、StaticDeepReplay 和分层证据。它不自动授予 Unity、Runtime、Git、网络或发布权限；任何运行时动作必须由用户明确授权并带停止条件。

## Skill 使用披露

遵循项目根 `AGENTS.md` 与 `.agents/README.md` 的披露规范；实际使用本 Skill 时，在首次进度更新和最终答复说明其职责。披露不是授权或验收证据。
