# P0：ES 活跃请求仲裁协议

Status: current
StableId: es.aiwarning.p0.active-request-arbitration.v1
Authority: AIWarnings（长期 P0 约束）；详细协议与领域投影见 Knowledge
RouteKeys: aiwarnings, p0, arbitration, active-request, lease, generation, commit, executor
Applicability: Camera、控制权、UI 焦点、Audio Voice 及其他多来源申请/集中决策/单点执行领域
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-active-request-arbitration.md
StaleWhen: Lease/Generation/Commit、领域仲裁、Camera 投影或任一 SourceRef 哈希变化。

## P0 长期约束

- 协议闭环固定为 `Request → Lease → Active Set → Arbitration → Commit → Executor`；它统一术语和安全纪律，不要求 Tag、Stat、Resource、Audio、Camera 继承万能 `ESRequestManager<T>`。
- 每个可独立释放 Request 必须有 `Owner + Token/Slot + Generation` 或等价不透明 Lease 身份；重复、过期、跨代、跨 Host/View 和复制品释放必须失败且不影响当前租期。Owner 销毁、回池、取消、场景/Provider 代际变更必须使旧 Lease 失效。
- `Push/Update/Release` 只改 Active Set 并标脏；领域唯一 Commit 清理失效请求、确定性重算并驱动 Executor。普通业务不得绕过仲裁器直写 Executor；同优先级必须使用稳定决胜键，不能依赖字典/对象 Hash/回调顺序。
- 单请求或适配层异常不得污染合成半成品、跳过清理或阻塞其他请求；多 View/玩家/输出域先按 ViewId 等价键隔离。诊断须解释 Owner、类型、优先级、决胜键、失效原因和最终提交；关闭诊断不得留下分配。
- `Winner` 仅表示唯一获胜主体；可叠加值称 `Modifier`/合成输入。Tag、Stat、Resource、Audio、Vehicle、State/Input 保留自身语义，只复用 Lease/代际/可解释性纪律；仅在至少两个领域状态机和异常清理完全相同且有证据时抽取极小身份组件。
- 预热后常规 Push/Update/Release/Commit 以 0 GC 为目标，但必须由 Unity Profiler 实测签收，静态代码不能宣称。
- 当前 Camera 首切片仍未完成 Unity/PlayMode/Profiler/Player 验收；不得宣称相机系统交付或冻结。

## Knowledge 导航

完整术语表、领域关系、Camera 投影、六类验收矩阵和 AI 实施前必答问题见 `es.aiwarning.p0.active-request-arbitration.v1`。本 Warning 不授予通用仲裁器设计、后端直写或运行时执行权限。
