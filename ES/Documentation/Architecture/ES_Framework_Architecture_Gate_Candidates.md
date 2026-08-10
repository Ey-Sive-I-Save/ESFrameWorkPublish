# ES Framework Architecture Gate Candidates

状态：门禁候选，不实现。

## 规则

1. Runtime asmdef 不得引用 Editor asmdef。
2. Test/Developer runtime package 不得成为正式 Runtime 无条件依赖。
3. RuntimeKey 不得持久化到配置、存档、网络或跨进程数据。
4. 不得新增第二套 Catalog、Scope、Lease、Resource Provider 或 Command Runner。
5. 正式发布依赖闭包不得包含未裁决原型。
6. GameCore/Design 层不得反向依赖 Player/内容层。
7. Obsolete asmdef 不得被正式模块引用。
8. 新增 asmdef 或修改 references 必须经过架构审查。
9. 不手改 Unity 生成的 `.csproj`。
10. AssemblyStream 不得执行全量扫盘或重资源初始化。
11. `ES_Logic` 只允许依赖 `ESFramework.AITest.Contracts`，不得依赖 `ESFramework.AITest.Runtime`。
12. `ESFramework.AITest.Runtime` 默认不得进入正式发布闭包。
13. `ESFramework.AITest.Editor/Runner` 只能存在于 Editor 或 Tests 程序集。
14. `ES_Logic` 只允许依赖 `ESFramework.Diagnostics.Contracts`，默认不得依赖 `ESFramework.AITest.Contracts`。
15. `ES_Logic.Editor` 默认不得无条件依赖 `ESFramework.AITest.Editor`，应使用 Adapter/Registry。
16. `AITest.Runtime` 不得引用 Editor asmdef。
17. `AITest.Editor -> AITest.Runtime` 的 define 条件必须一致；Runtime 被 define 排除时，Editor 不能无条件引用。
18. 专项优化必须通过 Provider/Capability 注入，不能改变 ES_Logic 热路径默认执行者。
19. `AITest.Editor` 若引用被 define 约束的 `AITest.Runtime` / `Runtime.Adapters` / `Contracts`，其 defineConstraints 必须覆盖被引用程序集。
20. `AITest.Runtime` 不再作为可选程序集禁止 `ES_Logic` 引用；正式 Runtime 默认休眠，按需激活。
21. 禁止 AITest Runtime 默认创建宿主、IPC、Listener、网络连接、持续 Update 或 GC 副作用。
22. 生产策略禁止普通 Release Player 通过启动参数绕过授权激活 AITest。
23. `AITest.Editor` 不得进入 Player。
24. AITest Runtime 宿主必须受 `Activated/Deactivated` 生命周期控制，Deactivate 后清理宿主、事件、IPC 与静态状态。

## 执行分层

- Baseline Violations：当前已存在，只记录，不扩大。
- New Violations：本次变更新增，立即阻断。
- 每条规则需要：误报处理、允许例外、例外过期时间、测试或人工复核入口。

## 暂不实现

- 不创建扫描脚本。
- 不创建 CI 门禁。
- 不修改 asmdef。
- 不修改源码。
