# 执行 AISkill Graph 工作流

本命令用于执行已经保存、烘焙并通过结构校验的 AISkill Graph。Graph 是本次工作流合同，AIBrain 负责逐个 Task 步骤的权威路由，Automation Facade 负责实际执行。

命令类型：安全执行。
默认改文件：否；由 AISkill Graph 中每个已注册 TaskContract 决定，且本命令自身不扩大能力。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md
```

## 执行要求

1. AISkill 必须绑定当前项目内可解析的 SourceAssetGuid、执行合同 Hash 与内容签名。
2. 每个 Task 步骤必须先通过 AIBrain，再调用 `ESAutomationFacade`；禁止直接调用 ProcessRunner。
3. AIBrain 不得扩大 TaskContract 的能力、路径、重试、PlayMode 或 AI 调用权限。
4. 子 Skill、重试、取消和恢复必须继续使用已有稳定 RunId 与执行记录。
5. Graph、AICommand、TaskContract 或当前用户授权任一失效时阻断当前步骤并保留失败证据。
6. 不得把源码或静态构建结果描述成 Unity Test Runner、PlayMode、Profiler 或发布通过。

## 交付格式

```text
1. AISkill ID、SourceAssetGuid、内容签名和执行合同 Hash。
2. AIBrain PlanHash 与命中的 AICommand/Knowledge。
3. 父子 RunId、步骤状态和证据路径。
4. 已完成、已阻断及未验证范围。
```

## 需求

```text
执行当前已保存并通过烘焙校验的 AISkill Graph 工作流。
```
