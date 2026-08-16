# 执行：Stable Graph AI 术语与面板简化 AI 命令

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测执行方式。
2. 按“命令类型”和“默认改文件”确认本次修改边界。
3. 先读取本文列出的规则、Graph 源码和当前工作树。
4. 只修改本文允许的 Stable Graph V2 编辑器入口；不得扩大到 Runtime、Player、发布或正式 AICommand 内容。
5. 稳定身份、序列化字段、节点/端口/边 Key、运行状态协议和候选目录协议不得改名或漂移。
6. 术语简化优先作用于用户可见 UI、Inspector、菜单、帮助文本和诊断摘要；内部类型只有在无序列化风险且调用链完整时才可拆分。
7. 任何 Unity、Test Runner、Profiler 或真实 AI 会话证据必须单独报告，不能由静态编译替代。
8. 结束时必须给出：已读规则、执行内容、改动文件、验证结果、剩余风险。
```

命令类型：安全执行。
默认改文件：是，仅允许 Stable Graph V2 AICommand/AISkill 编辑器体验与对应测试的小范围修改。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）/项目最高警告_P0_高频命名清晰与P1_无意义包装禁止_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md
Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/GraphView与NodeRunner_数据权威稳定身份与重构门禁_AI协作警告.md
Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
Assets/Plugins/ES/AICommands/README.md
Assets/Plugins/ES/Editor/ESGraphViewV2/ESStableGraphViewWindow.cs
Assets/Plugins/ES/Editor/ESGraphViewV2/ESStableGraphInspector.cs
Assets/Plugins/ES/Editor/ESGraphViewV2/ESAgentAuthoringGraphIntegration.cs
```

## 允许修改范围

```text
Assets/Plugins/ES/Editor/ESGraphViewV2/ESStableGraphViewWindow.cs
Assets/Plugins/ES/Editor/ESGraphViewV2/ESStableGraphInspector.cs
Assets/Plugins/ES/Editor/ESGraphViewV2/ESAgentAuthoringGraphIntegration.cs
```

可一并修改与上述入口直接对应的 Editor 测试文件；需在交付中逐项列出。

以下内容默认禁止修改：Graph 设计层的稳定身份、序列化结构和模型校验；Automation 派发、TaskContract 和 RunRecord 协议；现有正式 AICommand 正文；项目 Skill；候选包；Git、历史、审计状态、发布产物以及 Player/Runtime 程序集。

## 术语目标

用户第一屏只显示以下简单词：

```text
图：作者编辑内容
快照：当前检查结果
命令：做一次
技能：可复用流程
任务：技能中的一步
运行：某次执行
候选：等待差异检查和批准的结果
```

高级详情可以继续显示真实内部字段，例如 GraphId、ContentSignature、TaskContract、RunId 和 Hash；不得删除审计信息。

## 执行要求

```text
1. 先建立旧术语到用户术语的局部映射，不得全仓批量替换。
2. 首屏必须能看到当前状态、主要结论和下一步动作，不得要求用户先阅读原始 JSON 或内部状态名。
3. 错误、阻断、候选未批准和运行中状态必须分别显示原因、影响和恢复动作。
4. “检查通过”只能说明静态门禁通过；“已接收”“已运行”“已完成”必须分别对应真实回执状态。
5. 所有按钮状态必须由当前 GraphId、ContentSignature、RunId 和审批状态决定，不得读取全局最新请求冒充当前图。
6. UI 文案调整不得改变 Bake、派发、候选、Diff、批准和再执行的真实顺序。
7. 修改后至少运行目标文本的 UTF-8 门禁、git diff --check 和相关静态构建；Unity 实机证据缺失时必须明确标记 Verifying。
```

## 交付格式

```text
1. 已读规则：列出规则和源码入口。
2. 执行结论：说明用户可见术语、按钮状态和信息层级做了哪些简化。
3. 改动文件：逐项列出，注明是否仅 UI/文案或涉及行为。
4. 验证结果：区分 UTF-8、diff、dotnet-build、Unity 编译、Test Runner、真实交互和 Profiler。
5. 剩余风险：列出未实跑的 Unity/交互/性能证据和任何序列化迁移风险。
```

## 需求

```text
用户补充本次要简化的面板、按钮、术语或交互问题。
```
