# ES 资源 Scope Domain 运行边界交接

- 交接日期：2026-08-05（Asia/Shanghai）
- 来源窗口档案 ID：`ES-CODEX-20260804-052025`
- 来源窗口档案：[资源模式复核_ScopeRegistry实现_AIWarnings更新](../../AI协作历程（Codex）/2026-08-04_052025_资源模式复核_ScopeRegistry实现_AIWarnings更新.md)
- 目标职责：资源生命周期 Domain 权限、流程释放接线与 Unity 运行验收
- 状态：源码设计与 AIWarnings 权威已更新；运行级验收未完成。

## 一、接手后必须先读

1. `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
2. `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
3. `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
4. `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）/项目最高警告_资源加载底层_Library只属Editor_Runtime只认ManifestTable_AI协作警告.md`
5. `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/资源计划验收（ResourcePlanAcceptance）/资源计划_Scope生命周期绑定_商业项目验收标准.md`
6. 本交接文件。

新窗口必须建立自己的窗口档案 ID 和独立历程文件，从第一条任务开始记录；不得续写来源窗口档案。该授权只覆盖新窗口自身历程，不授予修改其他窗口档案的权限。

## 二、已经成立的源码事实

- `ESAssetDomain` 当前包含：`GameInternal`、`ApplicationSession`、`GameSession`、`Presentation`、`Scene`、`UI`、`Feature`。
- 默认 `ESAssets.LoadAsync(refer)` 进入自动创建/复用的 `GameSession` Registry Scope，不再隐式进入 Resident。
- 显式 Domain 和合法前缀 StringKey 首次加载自动创建 Scope。
- Registry 已有 Creating/Active/Closing、内部 Generation、父子级联释放和 Provider Transition 清理。
- Closing 占位保持到旧 Scope Dispose 同步回调结束，阻止回调中按同 Key 提前重建。
- 已捕获旧 Scope、TemporaryScope 和 Scene 新请求受 Provider Transition 门禁。
- Resident、Owner、Registry、ResourcePlan 私有 Scope 与 Temporary/Lease 保持独立所有权。
- Runtime Monitor 已显示 Registry 总数、隐式创建数和 Closing 数。
- 已增加默认自动创建、父子释放、Closing 重入、StringKey 和旧 Scope Transition 门禁测试源码。

主要源码：

- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs`
- `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetRuntimeDiagnostics.cs`
- `Assets/Plugins/ES/0_Stand/_Res/ResUse/ESAssetRefer.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESAssetScopePoolingTests.cs`
- `Assets/Plugins/ES/Editor/ESResPipeline/Windows/ESResourceRuntimeMonitorWindow.cs`

## 三、新窗口的首要任务

先回到源码和工作树，对照“ESAssetDomain 权威语义”形成 P0/P1/P2 缺口清单，然后实施证据明确、边界清晰的项目。不要因为交接结论直接改代码。

优先检查并推进：

1. `GameInternal` 当前仍能由普通公共 `LoadAsync(refer, domain)` 和 `ReleaseScope(domain)` 调用。设计并实现真正的框架内部权限边界；不考虑旧 API 兼容，但不得破坏 ResourcePlan、Owner、Resident 或 Temporary 主链。
2. `ApplicationSession`、`GameSession`、`Presentation`、`Scene`、`UI`、`Feature` 当前只有语义权威，尚未证明与对应流程管理器形成唯一释放接线。搜索实际 GameFlow、Scene、UI Root、Feature、Presentation 生命周期入口，只有在责任唯一且证据明确时接线。
3. 并行 Scene/UI/Feature 必须使用稳定 StringKey；检查是否存在动态拼字符串、同 Key 多释放责任方或长期未释放域。
4. Provider Transition、Closing 回调重入、旧 Pending 迟到完成以及同名新代重建必须保持安全。
5. `ReleaseScope()` 只表示逻辑所有权结束，不得伪报 Bundle、纹理、GPU 或 Unity 原生内存已经当帧归还。

## 四、建议的最小实现顺序

1. 只读搜索全部 `ESAssetDomain`、`CreateScope`、`LoadAsync(...domain)`、`ReleaseScope` 调用点。
2. 冻结 `GameInternal` 的内部入口与公共拒绝策略，并增加静态/Unity 测试。
3. 按真实流程逐个接入唯一释放责任；没有明确流程 Owner 的 Domain 保持缺口，不做猜测性自动释放。
4. 扩展 R11 测试：普通业务访问 GameInternal、流程结束释放、重复释放、Provider Transition、关闭 Domain Reload。
5. 最后执行 Unity Editor/Test Runner/PlayMode；Profiler、IL2CPP 和真实设备证据单独报告。

## 五、验证边界

- `git diff --check` 对本轮目标文件已通过，仅有 LF→CRLF 提示。
- 当前 Unity 生成的 `ES_Stand.csproj` 仍引用已迁移的 21 个 V1 文件，`dotnet build` 被 `CS2001` 阻断；禁止手改生成的 `.csproj`。
- Unity Editor 编译、Test Runner、PlayMode、Domain Reload 关闭、Profiler、IL2CPP 与真实设备仍未通过。
- 四种资源模式只能写“源码主控制流已形成”，不能据此宣称发布验收完成。

## 六、禁止事项

- 不覆盖其他 AI 的工作树修改。
- 不恢复 V1 资源系统，不为兼容旧 API 牺牲新版边界。
- 不把 `GameInternal`、ApplicationSession 或 Resident 合并成万能全局缓存。
- 不向普通业务暴露真实 Scope、Generation 或 scopeId。
- 不让 ResourcePlan 接受外部 Scope 注入。
- 不在没有明确生命周期 Owner 时猜测性自动释放。
- 不把生成工程编译、Unity Editor、Test Runner、PlayMode、Profiler、IL2CPP 和真实网络证据相互替代。

## 七、首条任务完成标准

- 给出基于当前源码的 Domain 权限/流程接线 P0/P1/P2 清单。
- 至少完成一个证据明确的边界强化或明确说明为何暂不实施。
- 对应 AIWarnings、CurrentStatus 和 R11 矩阵保持一致。
- 分层报告源码存在、静态编译、Unity、Test Runner、PlayMode、Profiler、IL2CPP 和真实设备证据。
