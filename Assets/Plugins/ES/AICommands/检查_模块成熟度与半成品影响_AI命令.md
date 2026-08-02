# 检查：模块成熟度与半成品影响 AI 命令

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按“命令类型”和“默认改文件”决定是否允许改代码。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和目标模块入口，避免误改其他 AI 或用户的改动。
5. 只做本文允许的事情；如果用户要求实现、删除、迁移或发布模块，必须获得对应执行授权。
6. 结束时必须给出：已读规则、状态矩阵、依赖影响、证据缺口、改动文件、验证结果、下一动作和续接检查点状态。
7. 默认只读。只有用户已明确指定状态文件与区域，或在审计结束后明确接受一次询问，才允许写入该区域。
```

命令类型：只读体检；可选受控状态登记。
默认改文件：否。可选状态登记只在用户确认精确文件与区域后生效；源码修复、模块迁移、删除、Git 操作、Unity 操作或发布仍必须另行明确授权。
风险等级：L1/L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
```

按目标模块继续读取规则索引命中的 P0、领域专项和现行开发文档；禁止递归加载全部 AIWarnings。

## 推荐 Skill

```text
$es-module-lifecycle
$es-worktree-audit
$es-unity-compile        # 只有目标状态需要编译或 Unity 证据时
$es-release-acceptance   # 只有目标状态涉及正式验收或发布时
```

Skill 只提供工作流，不扩大本命令的只读权限。

## 执行模式

```text
audit-only
  默认模式。完成审计与交付，不写状态文件。

audit+checkpoint
  仅在用户已提供目标文件/区域，或审计后接受一次询问时启用。
  只更新指定状态块，不修改其他内容。

resume
  从用户指定检查点恢复导航；先复核 Git 基线、相关工作树、最新规则和源码，报告 stale 字段后再决定是否继续。
```

## 执行要求

```text
1. 明确审计对象，不得把目录层级直接当作模块边界。
2. 找到源码、配置、资产、注册、初始化、测试、文档和发布入口。
3. 按 Proposed、Scaffolded、Experimental、Implementing、Integrating、Verifying、Stable、Deprecated、Archived 分类。
4. 单独记录 Blocked 原因，不把 Blocked 当成熟度状态。
5. 检查空实现、固定成功、吞异常、未接线入口、默认注册、序列化残留和稳定模块反向依赖。
6. 区分源码存在、程序集编译、Unity 编译、Test Runner、PlayMode、Profiler、Player、IL2CPP 和真实发布证据。
7. 给出进入下一状态的最小动作；未经授权不得代替用户实施。
8. 检查点写入前运行工作树重叠检查，确认目标文档没有未理解的并行修改。
9. 检查点必须记录 branch/HEAD、相关工作树、权威入口、激活、依赖、消费者、证据层、最后动作、最小下一动作、恢复必读路径和失效条件。
10. 下次接手不得把检查点当作持续授权；实现前必须重新取得当前用户与执行类 AICommand 的权限。
```

审计完成且用户尚未表态时，只允许询问一次：

```text
是否把本次审计检查点写入你指定的状态文档区域？如需要，请给出文件路径和区域/模块标识；不写也不影响本次审计结论。
```

## 交付格式

```text
1. 已读规则：列出实际读取的文件。
2. 状态结论：模块、当前状态、Blocked 原因、承诺范围。
3. 影响矩阵：权威入口、默认激活、上游依赖、下游消费者、半成品渗透风险。
4. 证据矩阵：已有证据、缺失证据、不能升级的结论。
5. 改动文件：只读时写“无”。
6. 验证结果：列出实际运行的命令或 Unity 证据；未运行必须明示。
7. 下一动作：进入下一状态所需的最小可验证步骤。
8. 续接检查点：`not-requested`、`offered`、`written`、`refused` 或 `stale`；已写入时列出精确文件与区域。
```

## 需求

```text
<用户在这里填写模块名、目录、预期状态、争议结论或需要审计的依赖范围>
```
