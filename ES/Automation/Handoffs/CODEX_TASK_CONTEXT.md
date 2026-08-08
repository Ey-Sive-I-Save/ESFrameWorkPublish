# Codex 新会话任务上下文

本文件是当前 AI 交给新 Codex 会话的任务上下文，不是 AITest 专属基础设施。当前交付主题是：将用户提供的通用 AI 驱动端到端测试方案按 ES 架构本地化，并开始最小纵向切片规划与实现。

## 用户目标

建立一个不依赖 Unity Test Runner 的 AI 辅助端到端/业务验收层。AI 负责观察、规划、决策和有限异常恢复；框架负责确定性执行、超时、取消、结果验证和证据采集。它不能替代 Unity Test Runner、PlayMode、Profiler、Player 或 IL2CPP 验收。

## 当前已确认的 ES 事实

- Unity 版本：2022.3.45f1；Input System、UGUI、TextMeshPro、UniTask、Unity MCP 已存在。
- `ESAutomationCenter` 是 Editor-only C# 自动化治理骨架，拥有 TaskContract、PathPolicy、RunRecord、Report 和 Worker 边界；当前 Worker 执行入口明确阻断，不能作为 Player Runtime。
- `ESInputService` / `ESInputSystemSource` 是 ES 正式输入链路；测试游戏输入必须经过专用测试输入源、RuntimeMode 和控制权边界，禁止直接写内部缓存。
- `ESRuntimeWatchRegistry` 当前是 Editor-only 观测/反射注册，不能扩张成 Player Capability Registry。
- `ESSceneValidationGuide` 已有测试场景阶段、检查状态和诊断报告入口，适合作为首个 ToSee/ToVerify 适配来源。
- `ESAdvancedDialog` 仅收集 Editor 输入，不能作为 Player 测试入口。
- Player 运行结果应写入 `Application.persistentDataPath/AITest/`；可信报告再由 Editor/CI 收集到 `ES/Automation/Reports/AITest/<runId>/`。

## 必须坚持的本地化边界

1. 通用核心可使用 `Packages/com.esframework.aitest/`，不得依赖 ES 程序集。
2. ES 适配放在 `Assets/Scripts/ESLogic/Runtime/Developer/AITest/` 与对应 Editor 目录，不新建第二套 Runner 或输入系统。
3. `ESAutomationCenter` 只在合同、报告和 CI 门禁层对接，不拥有 Player Runtime。
4. Player 至少支持离线确定性计划模式；在线 AI 模式另行通过受限本机传输协议实现，不能让 CI 依赖实时大模型。
5. Capability 返回纯 DTO，禁止返回 GameObject、Component、SO、Scope、Handle 或循环对象图。
6. Capability Provider 必须有显式注册、注销、场景代际和 runId 边界，禁止全场景反射查找。
7. UI 自动化与游戏输入分流：UI 走 EventSystem/InputSystem UI；游戏动作走 ESInputService 的测试 Source。

## 首个纵向切片

先冻结并实现：

1. Request / Plan / Step / Event / Result 协议与版本字段。
2. 确定性 Runner、单步/总超时、取消和统一状态码。
3. 显式 Capability Registry 与 Manifest 边界，先不做复杂特性魔法。
4. Player 安全报告写入 persistentDataPath，并使用原子文件落盘。
5. `see / verify / wait`。
6. UGUI Button 点击、Toggle 状态读取。
7. `ESSceneValidationGuide` 适配。
8. 一个 Player 启动参数或 Inbox 计划入口，返回 CI 退出码。

## 明确非目标

- 不把系统写成 Unity Test Runner 替代品。
- 不在第一切片加入任意自然语言直接控制、视觉模型、网络游戏扩展、存档压力测试或远程发布。
- 不修改生成 `.csproj`，不清理或覆盖其他工作树改动。
- 不把源码存在、静态编译或外部 AI 报告写成 Unity/Player/PlayMode/IL2CPP 已通过。

## 新会话执行顺序

1. 读取 AIWarnings README、CurrentStatus、RuleIndex 及命中的编辑器、输入、验证和 Automation 规则。
2. 读取本文件和当前 Git 状态。
3. 先用中文输出初始化结果、实施计划、边界和风险。
4. 仅实现最小、可编译或可静态验证的第一切片；保留未运行证据。
5. 结束时列出改动文件、验证命令、未完成项、阻断和下一最小动作。
