# AIWarnings 当前状态

最后核对：2026-07-31。

## 已确认基线

- `ES_Design.csproj` 最近一次核对为 `0 warning, 0 error`。
- `ESGenericLife` 的 Pool 分部已完成代码接线：唯一 Root、按类型唯一 Extension、新建/预热 Despawn 基线、回调异常收口与 Spawn 内延迟归还均已实现；但 Unity 尚未刷新生成的 `.csproj`，因此 ES_Logic 与 Unity Test Runner 尚未对本轮代码形成最终验收证据。
- Entity 模板、挂点与武器挂点链已具备静态闭环；`EntityCharacterProfile` 是唯一的 Prefab 身份/DataInfo 入口，正式 Variant 自动绑定，通用池模板由租出方直接 `Entity.BindDefinition(...)`。仍需 Unity PlayMode 验证和发布门禁证据，不可仅凭编译签收。
- GameTag 的 `ESTagStableReference` 已统一使用 `ESSearchDropdown` Picker；`ItemDataInfo` 的旧 `ValueDropdown/GetTagOptions` 残留已移除。Tag 测试代码已按当前 NUnit / `IPoolable` 契约修正，但 Unity Test Runner 尚未实跑。
- 输入、对象池、物理查询、Item/Shot 与 Buff 都有运行时实现，是当前较成熟的底座。
- 资源系统已进入内部联调：资源计划 Scope 生命周期的 P6/P7/P9 和 IL2CPP Player 仍缺真实验收证据。

## 当前优先级

1. 刷新 Unity 工程文件，编译 ES_Logic，并运行 `ESGenericLifePoolTests` 与 Tag 相关 Unity Test Runner 用例。
2. 验证角色模板、挂点和武器绑定的 Unity 行为，并为基础模板/预览模型补齐发布门禁证据。
3. 在 `Entity + EntityAIDomain + ESGameManager.WorldDomain` 中收口稳定身份、控制源仲裁和世界注册。
4. 执行 ResourcePlan 的 P6/P7/P9 PlayMode 验收。
5. 完成 IL2CPP Player 发布验收。

## 状态解释

- `现行约束`：必须遵守，除非用户明确改变项目规则。
- `已实现事实`：当前源码中存在，仍需按任务验证。
- `联调中`：已有实现，但缺少完整运行或发布证据。
- `待验收提案`：仅为方向，不得宣称已落地。
- `历史复盘`：用于理解决策背景，若与源码冲突则源码优先。

此文件只记录高层状态。具体源码入口、验收标准和 P0 规则请从 `规则索引（RuleIndex）.md` 进入。
