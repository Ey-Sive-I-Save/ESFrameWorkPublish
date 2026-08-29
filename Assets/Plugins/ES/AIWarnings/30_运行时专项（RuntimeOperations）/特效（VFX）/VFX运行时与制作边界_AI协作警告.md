# VFX 运行时与制作边界 AI 协作警告

Status: current
StableId: es.aiwarning.runtime.vfx.production-runtime-boundary
Authority: P1 runtime/editor boundary; current implementation is `Implemented-Unverified`.
RouteKeys: `aiwarnings`, `runtime`, `vfx`, `particle`, `preview`, `resource`
Applicability: ESVfxInfo/Group/Key/GameCoreTable/Module/Handle, ParticleSystem/VFX Graph candidates, templates, preview, AssetPackage analysis and budgets.
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-runtime-vfx-production-runtime-boundary.md#evidence`
Owner: ES VFX runtime/editor owners.
StaleWhen: ESVfx contracts, module backend, Unity VFX package, resource/pool lifecycle or acceptance evidence changes.

## 长期约束

- 稳定链路必须保持：作者资产/模板 → `ESVfxInfo`/`ESVfxGroup` → `ESVfxKey`/GameCore Table → ResourcePlan/Provider/Scope → 播放请求 → `ESVfxModule` → 后端 → `ESVfxHandle`/状态/结束/回池/释放。
- 作者 Group 只负责内容聚合与注入；运行时预算、并发、抢占和降级归模块/运行策略，不得合并为同一权威对象。
- 当前可核对的运行后端只有 `ParticleSystem[]`；候选识别、预览、一次播放或 `.csproj` 编译不等于 VisualEffect/VFX Graph 已支持，也不等于 Runtime、Profiler、Player、IL2CPP 或发布通过。
- AI 可生成方案、参数合同、变体和预算建议，并经受控 Unity Editor API 修改类型化属性或预览实例；不得手写 YAML、绕过资产 API、静默覆盖正式资产或把预览/截图当正式资产。
- 正式保存须显式目标、版本、变体、差异、Undo/回滚和漂移检查；业务不得直接 `Instantiate` 绕过模块、Pool、Resource Scope 或既有 Handle/结束语义。
- VFX Graph 只有在包/asmdef、稳定身份、类型化属性、事件/结束判定、重置回池、GPU/Overdraw 预算及同一 Handle 合同均明确并有证据后才可准入；否则保持 ParticleSystem。
- VFX 不取得 Audio、Light、Decal、Camera Shake 等领域的最终写入权。

详细事实、入口源码、模板合同、准入清单、预览保存规则、验收矩阵、历史说明和证据见对应 Knowledge 条目；Knowledge 仅导航和回溯，不授予执行授权。
