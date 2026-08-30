# VFX 运行时与制作边界

`KnowledgeId`: `es.aiwarning.runtime.vfx.production-runtime-boundary.v1`  
`Authority`: `AIWarnings + current VFX runtime/editor source`  
`RouteKeys`: `aiwarnings`, `runtime`, `vfx`, `particle`, `preview`, `resource`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `3c2e2f23c8a1da98a50f4895a03589ae4272c647485a130755c14f13632fcbb5`
`SourceSetHash`: `3c2e2f23c8a1da98a50f4895a03589ae4272c647485a130755c14f13632fcbb5`
`EntryBodyHash`: `ebc3997e3559e4e338aa74477ea2063532e3a77651200e68ce20e0d06ab7922b`
`StaleWhen`: ESVfx contracts, module backend, Unity VFX package, resource/pool lifecycle or acceptance evidence changes.

## 迁移范围

原 Warning 137 行、8,506 UTF-8 字节；现 Warning 仅保留长期约束、当前后端边界、制作权限和证据边界。原有入口源码、模板路线、VFX Graph 准入条件、预览/正式保存规则、验收矩阵和历史说明在本条目保真承接。

## 当前事实与链路

- 稳定链路为：作者资产/模板 → `ESVfxInfo`/`ESVfxGroup` → `ESVfxKey`/GameCore Table → ResourcePlan/Provider/Scope → 播放请求 → `ESVfxModule` → 后端 → `ESVfxHandle`/状态/结束/回池/释放。
- `ESVfxGroup` 负责作者内容聚合与注入；运行时预算、并发、抢占和降级属于模块/运行策略。
- 当前源码可核对的实例后端是 `ParticleSystem[]`。AssetPackage 的 VFX Graph 候选识别或 EditorOnly 快照不代表 Graph 可播放、可调参、可回池或可发布。

## 制作与准入边界

- AI 可生成结构化制作方案、参数合同、变体与预算建议，并经受控 Unity Editor API 修改类型化属性或 `HideAndDontSave` 预览实例；不得手写 YAML、绕过资产 API、静默覆盖正式资产，或把截图/预览当正式资产。
- 正式保存须显式目标、版本、变体、差异、Undo/回滚和漂移检查；业务不得直接 `Instantiate` 绕过模块、Pool、Resource Scope 或既有 Handle/结束语义；VFX 不取得 Audio、Light、Decal、Camera Shake 的最终写入权。
- `VisualEffect` 后端只有在包/asmdef、稳定身份、类型化 Exposed Property、事件与结束判定、重置回池、GPU/Overdraw 预算及同一 Handle 合同均明确并有证据后才准入；否则保持 ParticleSystem，不用反射或字符串兜底。

## 验收与未证实项

模板参数、事件、时长/结束、Owner 销毁、场景切换、回池复用、预览关闭/Domain Reload、CPU/GPU/粒子/Overdraw/GC/内存预算、平台变体、缺包/缺资源降级及 Player/IL2CPP/发布均需分别取得证据。本迁移只完成静态来源整理，尚未执行 Unity、PlayMode、Profiler、Player、IL2CPP 或发布验证。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/特效（VFX）/VFX运行时与制作边界_AI协作警告.md` (`a6531ed0d60c4e137dad6c18db4481ee50d6f771b9649a5932b61fae887a1118`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESVfxInfo.cs` (`ad2a5bf071d8baf7e6c145e753626b18b2ada52f8dcc200b124314c68ac6c792`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/GroupType/ESVfxGroup.cs` (`905c38ab267181de9df38d4fb04ad01b878f963b6bdcc6752a769dfd4fbc442c`)
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/VFX/ESVfxConfigKeyData.cs` (`f58aaf0f55c4f29739c3fcbd234f2245561820b102d4167667005963899c1e17`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESVfxModule.cs` (`b3d0f17f566ae10a5983d3b4844d7aac7105d61cc867c5c4620b7f568c28ee24`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/09_GameObjectVFX/OpGameObjectVfx.cs` (`e3c9d637c5149a3a6f286ab41cb8a1dcd903e6875c6d5d3f8301989336162569`)

## EvidenceRefs

### evidence

- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESVfxModule.cs`
- `runtime-not-run`
