# 执行 Stable Graph 单次 AI 任务

本命令用于执行已经通过 Stable Graph 烘焙与风险校验的即时或单次 AI 任务。Graph 内容指纹、AIBrain 计划和 Automation RunRecord 必须形成同一条可追踪链路。

命令类型：安全执行。
默认改文件：否；由当前 Graph 合同与用户授权共同决定，且本命令自身不扩大写入范围。
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

1. 只接受已经烘焙且带 64 位内容指纹的 Stable Graph 合同。
2. 必须先由 AIBrain 建立权威计划，再经 `ESAutomationFacade` 派发已注册任务。
3. 不接受脚本路径、解释器、命令行或任意输出目录。
4. Graph、AICommand、用户授权和 TaskContract 任一冲突时立即阻断。
5. 必须保留 InvocationId、PlanHash、Graph 指纹和 RunRecord；不得把已发送描述成已完成。
6. 不得借单次执行安装永久 Skill、修改正式 AICommand、提交 Git 或发布。

## 交付格式

```text
1. AIBrain 计划状态与 PlanHash。
2. Graph ID、内容指纹与任务合同。
3. Automation RunId 与当前状态。
4. 实际改动、实际验证和未验证范围。
```

## 需求

```text
执行当前已烘焙并通过风险门禁的 Stable Graph 单次 AI 任务。
```
