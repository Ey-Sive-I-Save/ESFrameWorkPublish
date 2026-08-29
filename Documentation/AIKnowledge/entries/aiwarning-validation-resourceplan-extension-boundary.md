# ResourcePlan 扩展协议边界：保真 Knowledge
`KnowledgeId`: `es.aiwarning.validation.resourceplan-extension-boundary.v1`  
`Authority`: `AIWarnings` 与当前 ResourcePlan 实现  
`RouteKeys`: `aiwarnings`, `validation`, `resourceplan`, `extension`, `bake`, `publish`, `runtime`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `f493c50981135ec6ec50f77880a679738f97a4591777a6926cb49d3ae2dc6c0d`  
`SourceSetHash`: `f493c50981135ec6ec50f77880a679738f97a4591777a6926cb49d3ae2dc6c0d`  
`EntryBodyHash`: `dc018260ef03709c1dbb5e1e4344890aee2e1d3a17ed5c48d3a448bd37a28ba7`  
`StaleWhen`: ResourcePlan 扩展接口、Bake/Publish/Runtime 生命周期或任一 SourceRef 变化。

## 迁移范围
Warning 保留扩展不得越过 ES 资源发布核心、稳定身份、统一 Plan 生命周期和未实现接口禁写边界；本条目承载当前接口事实、Bake/Publish/Runtime 细节、Lease 释放语义与 FMOD 未来示例。Knowledge 不创建实现事实或执行授权。

## 当前实现与 Bake
`IESResourcePlanBakeExtension` 已实现，由可选模块 Editor 程序集初始化入口调用 `ESResourcePlanBakeExtensions.Register` 一次；`ProviderId` 必须全局唯一稳定，`SchemaVersion` 只在快照语义变化时递增。来源配置必须烘焙为 `ESResourcePlanBakedExtensionEntry`，Player 禁止重新扫描来源 SO；普通资源进入 `assets` 并沿已有 AssetKey/GUID 进入 Catalog、AB、Consumer 与下载流程。

## Publish 边界
Publisher 直接校验烘焙产物、资源闭包和发布计划，不是可选中间件的独立回调。当前没有 `IESResourcePlanPublishExtension`；需要新发布钩子时，必须先新增并验收明确接口与失败语义，不能先在文档或代码中虚构。

## Runtime 与 Lease
`IESResourcePlanRuntimeExtension` 与 `IESResourcePlanExtensionLease` 必须在统一 Plan 生命周期中 Prepare/Release；重复 retain 不重复 Prepare，最后一个 retain 释放后才 Release。Context 只能读取当前 Plan 已加载资产，不能触发额外加载或重新扫描；扩展不得自行创建 Scope、Provider、下载器或平行引用计数。Prepare 成功后才登记 Lease，失败/取消不得半登记；Plan/Scope 结束、Provider 切换、异常和 Dispose 均走统一释放路径。多个 Lease 按登记逆序释放，单个异常不得阻断其余归还；Lease 不得跨 Provider、Plan 或场景代际缓存。

## FMOD 与禁止项
FMOD 当前不在实施范围，不得据此创建依赖、Consumer、发布规则或加载代码；未来启用时由扩展负责 Event/Bank 闭包，ES Core 仍负责发布、下载、校验和生命周期。禁止字符串路径绕过 AssetLibrary/Catalog、Player 反射来源配置、ProviderId 重复覆盖、扩展缺失静默跳过已配置来源，或把未实现 Publish 扩展写入代码、文档和验收结论。

## 原文快照
迁移前原始文件按台账计为 59 行（PowerShell 内容行计数为 58）、3632 UTF-8 字节，原始 SHA-256 为 `7b59846935b1fa2e76251fb843a2e4e29a3374178bc0079b34577e0ab13b8adf`；本条目保留当前接口和边界语义。本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/ResourcePlan扩展协议_强制约束.md` (`8a1d85e8542b86e9b99608084e7ffcff7b335180431bc34edd04b809900112de`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`d78bf271dc3e56b35ac50aa652443682cb251eea3060079695453f07c688824e`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-validation-resourceplan-extension-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）/ResourcePlan扩展协议_强制约束.md`
