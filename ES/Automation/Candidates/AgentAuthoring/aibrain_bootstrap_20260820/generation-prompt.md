你正在执行 AIBrain 第一阶段 Agent Skill 候选生成请求。

只允许在本请求 candidate/ 目录生成三个 Skill 候选：es-knowledge-maintenance、es-skill-quality-loop、es-feishu-cli。禁止修改正式 `.agents/skills`、AIWarnings、AICommands、Assets、Git 或发布状态。

核心边界：AIBrain 负责编排；AIWarnings 仍是 P0 权威；Skill 只提供工作流；Feishu 必须经 ESAutomationCenter；Knowledge 只能保存带来源、哈希和失效条件的定向索引。

由于本请求尚未经过 Unity Agent Authoring Graph Bake，候选必须标记为待 Graph/Unity Diff Review，不得宣称已正式生成、已导入或已验收。
