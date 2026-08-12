# ESAITest：直接启动 ESTEST AI 命令

## 直接生效协议

当用户把本文件路径发给 AI，或明确要求“启动 ESTEST / 运行 ESAITest / 中断 ESTEST”时，AI 必须：

```text
1. 读取本文件全文，不根据文件名猜测执行方式。
2. 确认当前仓库、工作树、Unity 版本和目标 Unity 实例。
3. 只调用既有 ESTEST 启动、观测、报告与安全取消入口。
4. 已有活动 Run 时不得并发启动第二个 Run。
5. 没有 UnityMCP、Player 路径或受权运行时桥时，明确报告阻断，不伪造启动成功。
6. 结束时报告执行入口、RunId、状态、报告路径与证据等级。
```

命令类型：安全执行。
默认改文件：否。
风险等级：L2，会进入 PlayMode 或启动 Player，并在 `persistentDataPath` 写运行报告。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md
Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md
Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
Packages/com.esframework.aitest/README.md
.agents/skills/es-start-estest/SKILL.md
```

## 执行入口

```text
Unity Editor 启动菜单：
【ES】/自动化与开发/自动化中心/ESAITest/直接启动 ESTEST

Unity Editor 安全取消菜单：
【ES】/自动化与开发/自动化中心/ESAITest/中断当前 ESTEST

Player / CI：
<明确的 Player 路径> -esTest [-esAITestQuit]

受权运行时 API：
ESAITestPlayerBootstrap.TryStartESTEST(out string error)
ESAITestPlayerBootstrap.TryStartESTEST(planPath, out error)
ESAITestPlayerBootstrap.TryStartESTEST(request, out error)
ESAITestPlayerBootstrap.RequestCancel()
```

优先使用 `$es-start-estest` 执行本命令。Skill 只提供工作流，不扩大本命令权限。

## 验收要求

```text
1. 启动证据必须来自 Runtime Dashboard、Unity Console、Player 进程或真实 Run 报告。
2. 报告目录应为 Application.persistentDataPath/ESAITest/<runId>/。
3. 至少确认 result.json；完整 Run 应同时包含 summary.md、request.json、manifest.json。
4. 启动失败、runtime_busy、取消与报告写入失败必须原样报告。
5. 不把源码存在、定向编译或菜单存在写成 ESTEST 已实际运行。
```

## 禁止事项

```text
- 禁止为启动 ESTEST 修改业务源码、场景、Prefab、输入配置或发布设置。
- 禁止绕过唯一 Runner、Capability Registry 或 ESInputModule 测试 Source。
- 禁止自动修复任务外编译错误。
- 禁止写 Git、AI 协作历程、模块审计状态或发布状态。
```

## 交付格式

```text
1. 执行入口：Unity 菜单 / Player 命令行 / 运行时 API。
2. 执行结果：已启动、已完成、已取消、runtime_busy 或阻断。
3. RunId 与报告：列出真实路径；没有证据时明确写无。
4. 验证等级：runtime-observation / player-run，以及缺失的后续证据。
5. 剩余风险：列出 UnityMCP、PlayMode、Player、Profiler 或 IL2CPP 缺口。
```

## 需求

```text
直接启动内建 ESTEST；如用户提供计划路径或 Request，则执行指定计划。需要中断时走安全取消入口。
```
